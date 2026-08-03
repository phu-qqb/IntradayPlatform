using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bBoundedProcessOutput(
    long ByteCount,
    string Sha256,
    string Text,
    int SecretValueCountChecked,
    bool SecretScanPassed,
    bool RawOutputRecorded);

public sealed record Arch7bV2CommandExecutionResult(
    string CommandId,
    string StageId,
    int ProcessId,
    int ExitCode,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    long ElapsedMilliseconds,
    Arch7bBoundedProcessOutput StandardOutput,
    Arch7bBoundedProcessOutput StandardError,
    Arch7bNormalizedChildResult NormalizedResult,
    string MaterializedCommandSha256,
    string EvidenceSha256);

public static class Arch7bBoundedStreamReader
{
    public static async Task<Arch7bBoundedProcessOutput> ReadAsync(Stream stream, int maximumBytes,
        IReadOnlyCollection<string> exactSecrets, IReadOnlyCollection<string> forbiddenSignatures,
        CancellationToken cancellationToken)
    {
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        var strictUtf8 = new UTF8Encoding(false, true);
        var decoder = strictUtf8.GetDecoder();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var bytes = new byte[8192];
        var chars = new char[strictUtf8.GetMaxCharCount(bytes.Length)];
        var text = new StringBuilder(Math.Min(maximumBytes, 64 * 1024));
        var needles = exactSecrets.Where(value => !string.IsNullOrEmpty(value))
            .Concat(forbiddenSignatures.Where(value => !string.IsNullOrEmpty(value)))
            .Distinct(StringComparer.Ordinal).ToArray();
        var maximumNeedleLength = Math.Max(1, needles.Select(value => value.Length).DefaultIfEmpty(1).Max());
        var scanTail = string.Empty;
        long count = 0;
        while (true)
        {
            var read = await stream.ReadAsync(bytes, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            count += read;
            if (count > maximumBytes)
                throw new Arch7bQualificationException(Arch7bV2Blockers.ChildOutputLimitExceeded);
            hash.AppendData(bytes, 0, read);
            decoder.Convert(bytes, 0, read, chars, 0, chars.Length, false,
                out var bytesUsed, out var charsUsed, out _);
            if (bytesUsed != read)
                throw new Arch7bQualificationException(Arch7bBlockers.ChildOutputInvalid, "utf8");
            var chunk = new string(chars, 0, charsUsed);
            var scan = scanTail + chunk;
            if (needles.Any(value => scan.Contains(value, StringComparison.Ordinal)))
                throw new Arch7bQualificationException(Arch7bV2Blockers.ChildOutputSecretValueDetected);
            scanTail = scan.Length >= maximumNeedleLength - 1
                ? scan[^Math.Min(scan.Length, maximumNeedleLength - 1)..] : scan;
            text.Append(chunk);
        }
        decoder.Convert([], 0, 0, chars, 0, chars.Length, true,
            out _, out var flushedChars, out _);
        if (flushedChars > 0) text.Append(chars, 0, flushedChars);
        return new(count, Convert.ToHexStringLower(hash.GetHashAndReset()), text.ToString(),
            exactSecrets.Count, true, false);
    }
}

public sealed class Arch7bOneShotProcessRunnerV2
{
    private static readonly string[] InheritedSystemVariables = OperatingSystem.IsWindows()
        ? ["SystemRoot", "WINDIR", "TEMP", "TMP", "DOTNET_ROOT", "COMSPEC", "PATHEXT"]
        : ["HOME", "TMPDIR", "DOTNET_ROOT"];
    private static readonly string[] ForbiddenOutputSignatures =
    [
        "ARCH7B_SECRET_SENTINEL", "SecretString", "password=", "Password=",
        "connectionstring", "ConnectionString", "QQ_ARCH7B_POSITION_IMPORT_FAST_PATH="
    ];

    private readonly Arch7bRealCommandAdapterRegistry adapters;
    private readonly Dictionary<string, LongLivedHandle> longLived = new(StringComparer.Ordinal);

    public Arch7bOneShotProcessRunnerV2(Arch7bRealCommandAdapterRegistry adapters)
    {
        this.adapters = adapters;
    }

    public async Task<Arch7bV2CommandExecutionResult> InvokeAsync(
        Arch7bOneShotMaterializedCommand command,
        string runRoot,
        Arch7bTerminalCleanupSupervisor cleanup,
        IArch7bOneShotSecretLease secretLease,
        bool bracketStarted,
        CancellationToken cancellationToken = default)
    {
        ValidateExecutable(command);
        var secretEnvironment = secretLease.Acquire(command.CommandId, command.SecretVariableNames,
            bracketStarted);
        var secretValuesForScan = secretEnvironment.Values.Values.ToArray();
        Process? process = null;
        var resourceId = $"process:{command.CommandId}";
        RegisterCleanup(cleanup, resourceId, command, () => process);
        var startInfo = BuildStartInfo(command, secretEnvironment);
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            process = new Process { StartInfo = startInfo };
            if (!process.Start()) throw new Arch7bQualificationException(
                Arch7bBlockers.ChildProcessFailedUncatalogued, command.CommandId);
            cleanup.MarkCreated(resourceId);
            ClearSecretEnvironment(startInfo, secretEnvironment);
            secretLease.Release(secretEnvironment);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(command.TimeoutSeconds));
            var stdout = Arch7bBoundedStreamReader.ReadAsync(process.StandardOutput.BaseStream,
                command.StandardOutputLimitBytes, secretValuesForScan,
                ForbiddenOutputSignatures, timeout.Token);
            var stderr = Arch7bBoundedStreamReader.ReadAsync(process.StandardError.BaseStream,
                command.StandardErrorLimitBytes, secretValuesForScan,
                ForbiddenOutputSignatures, timeout.Token);
            try
            {
                var exit = process.WaitForExitAsync(timeout.Token);
                var first = await Task.WhenAny(exit, stdout, stderr).ConfigureAwait(false);
                if (first != exit && first.IsFaulted)
                    await first.ConfigureAwait(false);
                await exit.ConfigureAwait(false);
                var outputs = await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
                stopwatch.Stop();
                var adapter = adapters.Require(command.AdapterId);
                var normalized = await adapter.AdaptAsync(outputs[0].Text, command, runRoot,
                    cancellationToken).ConfigureAwait(false);
                var completedAt = DateTimeOffset.UtcNow;
                var canonical = string.Join('\n', command.CommandId, command.StageId, process.Id,
                    process.ExitCode, startedAt.ToString("O"), completedAt.ToString("O"),
                    stopwatch.ElapsedMilliseconds, outputs[0].Sha256, outputs[1].Sha256,
                    normalized.EvidenceSha256, command.EvidenceSha256);
                return new(command.CommandId, command.StageId, process.Id, process.ExitCode,
                    startedAt, completedAt, stopwatch.ElapsedMilliseconds, outputs[0], outputs[1],
                    normalized, command.EvidenceSha256, Arch7bOneShotContracts.Sha256(canonical));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Kill(process);
                throw new Arch7bQualificationException(Arch7bBlockers.ChildProcessTimeout,
                    command.CommandId);
            }
            catch
            {
                Kill(process);
                throw;
            }
        }
        finally
        {
            ClearSecretEnvironment(startInfo, secretEnvironment);
            secretLease.Release(secretEnvironment);
        }
    }

    public Arch7bLongLivedProcessEvidence StartLongLived(
        Arch7bOneShotMaterializedCommand command,
        string runRoot,
        string expectedReadyEvidence,
        IReadOnlyList<string> allowedSignals,
        string terminalStage,
        Arch7bTerminalCleanupSupervisor cleanup,
        Arch7bOneShotLongLivedProcessRegistry registry,
        IArch7bOneShotSecretLease secretLease,
        bool bracketStarted,
        CancellationToken cancellationToken = default)
    {
        ValidateExecutable(command);
        var processKey = command.LongLivedProcessKey ?? throw new Arch7bQualificationException(
            Arch7bV2Blockers.LongLivedProcessStateInvalid, command.CommandId);
        if (longLived.ContainsKey(processKey))
            throw new Arch7bQualificationException(Arch7bV2Blockers.DuplicateProcessKey, processKey);
        var secretEnvironment = secretLease.Acquire(command.CommandId, command.SecretVariableNames,
            bracketStarted);
        var secretValuesForScan = secretEnvironment.Values.Values.ToArray();
        var startInfo = BuildStartInfo(command, secretEnvironment);
        var process = new Process { StartInfo = startInfo };
        if (!process.Start()) throw new Arch7bQualificationException(
            Arch7bBlockers.ChildProcessFailedUncatalogued, command.CommandId);
        ClearSecretEnvironment(startInfo, secretEnvironment);
        secretLease.Release(secretEnvironment);
        var resourceId = $"process:{command.CommandId}";
        cleanup.Register(new(resourceId, command.CleanupResourceType, command.StageId, command.CommandId,
            false, "TERMINAL_ALWAYS", "KILL_PROCESS_TREE_AND_RELEASE_ENVIRONMENT", TimeSpan.FromSeconds(5),
            true, true, true, Arch7bCleanupState.Registered, null), async token =>
            {
                Kill(process);
                if (!process.HasExited) await process.WaitForExitAsync(token).ConfigureAwait(false);
                foreach (var marker in new[] { Path.Combine(runRoot, processKey + ".ready"),
                             Path.Combine(runRoot, processKey + ".COMPLETE.signal"),
                             Path.Combine(runRoot, processKey + ".ready.tmp") })
                    if (File.Exists(marker)) File.Delete(marker);
                return Arch7bOneShotContracts.Sha256(resourceId + ":released");
            });
        cleanup.MarkCreated(resourceId);
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stdout = Arch7bBoundedStreamReader.ReadAsync(process.StandardOutput.BaseStream,
            command.StandardOutputLimitBytes, secretValuesForScan,
            ForbiddenOutputSignatures, timeout.Token);
        var stderr = Arch7bBoundedStreamReader.ReadAsync(process.StandardError.BaseStream,
            command.StandardErrorLimitBytes, secretValuesForScan,
            ForbiddenOutputSignatures, timeout.Token);
        longLived.Add(processKey, new(process, stdout, stderr, timeout));
        registry.Register(processKey, command, process, expectedReadyEvidence, allowedSignals,
            terminalStage, resourceId, DateTimeOffset.UtcNow);
        var readyPath = Path.Combine(runRoot, processKey + ".ready");
        var readyDeadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!File.Exists(readyPath) && !process.HasExited && DateTimeOffset.UtcNow < readyDeadline)
            Thread.Sleep(10);
        if (!File.Exists(readyPath) || process.HasExited)
            throw new Arch7bQualificationException(Arch7bV2Blockers.LongLivedProcessExited, processKey);
        var readySha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(readyPath)));
        return registry.MarkReady(processKey, readySha);
    }

    public async Task<Arch7bNormalizedChildResult> StopLongLivedAsync(string processKey,
        Arch7bOneShotMaterializedCommand command, string runRoot,
        Arch7bOneShotLongLivedProcessRegistry registry, CancellationToken cancellationToken = default)
    {
        if (!longLived.TryGetValue(processKey, out var handle))
            throw new Arch7bQualificationException(Arch7bV2Blockers.LongLivedProcessStateInvalid, processKey);
        registry.Signal(processKey, "COMPLETE");
        var signalPath = Path.Combine(runRoot, processKey + ".COMPLETE.signal");
        await File.WriteAllTextAsync(signalPath, "COMPLETE", new UTF8Encoding(false), cancellationToken)
            .ConfigureAwait(false);
        handle.Timeout.CancelAfter(TimeSpan.FromSeconds(command.TimeoutSeconds));
        try
        {
            await handle.Process.WaitForExitAsync(handle.Timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Kill(handle.Process);
            throw new Arch7bQualificationException(Arch7bBlockers.ChildProcessTimeout, processKey);
        }
        await registry.StopAsync(processKey, cancellationToken).ConfigureAwait(false);
        if (File.Exists(signalPath)) File.Delete(signalPath);
        var outputs = await Task.WhenAll(handle.StandardOutput, handle.StandardError).ConfigureAwait(false);
        var result = await adapters.Require(command.AdapterId).AdaptAsync(outputs[0].Text, command,
            runRoot, cancellationToken).ConfigureAwait(false);
        handle.Timeout.Dispose();
        longLived.Remove(processKey);
        return result;
    }

    private static void RegisterCleanup(Arch7bTerminalCleanupSupervisor cleanup, string resourceId,
        Arch7bOneShotMaterializedCommand command, Func<Process?> process)
    {
        cleanup.Register(new(resourceId, command.CleanupResourceType, command.StageId, command.CommandId,
            false, "TERMINAL_ALWAYS", "KILL_PROCESS_TREE_AND_RELEASE_ENVIRONMENT", TimeSpan.FromSeconds(5),
            true, true, true, Arch7bCleanupState.Registered, null), async token =>
            {
                var value = process();
                if (value is not null)
                {
                    Kill(value);
                    if (!value.HasExited) await value.WaitForExitAsync(token).ConfigureAwait(false);
                    value.Dispose();
                }
                return Arch7bOneShotContracts.Sha256(resourceId + ":released");
            });
    }

    private static ProcessStartInfo BuildStartInfo(Arch7bOneShotMaterializedCommand command,
        Arch7bSecretEnvironmentLease secrets)
    {
        var value = new ProcessStartInfo
        {
            FileName = command.ExecutablePath,
            WorkingDirectory = command.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in command.ArgumentList) value.ArgumentList.Add(argument);
        value.Environment.Clear();
        foreach (var name in InheritedSystemVariables)
        {
            var inherited = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(inherited)) value.Environment[name] = inherited;
        }
        foreach (var pair in secrets.Values)
        {
            if (!command.SecretVariableNames.Contains(pair.Key, StringComparer.Ordinal))
                throw new Arch7bQualificationException(Arch7bV2Blockers.SecretCommandScopeMismatch,
                    command.CommandId);
            value.Environment[pair.Key] = pair.Value;
        }
        return value;
    }

    private static void ClearSecretEnvironment(ProcessStartInfo startInfo, Arch7bSecretEnvironmentLease secrets)
    {
        foreach (var name in secrets.Values.Keys) startInfo.Environment.Remove(name);
    }

    private static void ValidateExecutable(Arch7bOneShotMaterializedCommand command)
    {
        if (!Path.IsPathFullyQualified(command.ExecutablePath) || !File.Exists(command.ExecutablePath))
            throw new Arch7bQualificationException(Arch7bBlockers.LiveCommandAuthorityIncomplete,
                command.ExecutablePath);
        var actual = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(command.ExecutablePath)));
        if (actual != command.ExecutableSha256)
            throw new Arch7bQualificationException(Arch7bBlockers.ExecutableShaMismatch, command.CommandId);
        if (command.ArgumentList.Any(Arch7bV2ArgumentSafety.IsSecretArgumentValue))
            throw new Arch7bQualificationException(Arch7bBlockers.SecretInArgument, command.CommandId);
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(true);
        }
        catch (InvalidOperationException) { }
    }

    private sealed record LongLivedHandle(Process Process,
        Task<Arch7bBoundedProcessOutput> StandardOutput,
        Task<Arch7bBoundedProcessOutput> StandardError,
        CancellationTokenSource Timeout);
}
