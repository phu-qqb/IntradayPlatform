using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bBoundedProcessOutput(
    long ByteCount,
    string Sha256,
    string Text,
    int SecretValueCountChecked,
    bool SecretScanPassed,
    bool RawOutputRecorded);

public sealed record Arch7bChildProcessOutputReceipt(
    string ContractVersion,
    string CommandId,
    string StageId,
    int ProcessId,
    int ExitCode,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    long ElapsedMilliseconds,
    long StdoutByteCount,
    string StdoutSha256,
    long StderrByteCount,
    string StderrSha256,
    bool Utf8Validated,
    bool SecretScanPassed,
    int SecretValueCountChecked,
    bool RawOutputRecorded,
    string AdapterId,
    string ExpectedNativeOutputContract,
    string MaterializedCommandSha256,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', ContractVersion, CommandId,
        StageId, ProcessId, ExitCode, StartedAtUtc.ToString("O"),
        CompletedAtUtc.ToString("O"), ElapsedMilliseconds, StdoutByteCount,
        StdoutSha256, StderrByteCount, StderrSha256, Utf8Validated,
        SecretScanPassed, SecretValueCountChecked, RawOutputRecorded, AdapterId,
        ExpectedNativeOutputContract, MaterializedCommandSha256);
}

public sealed record Arch7bChildAdapterFailureEvidence(
    string ContractVersion,
    string NativeBlocker,
    string AdapterId,
    string ReceiptPath,
    string ReceiptSha256,
    string ParseClassification,
    string ExceptionType,
    string ExceptionMessageSha256,
    bool RawOutputRecorded,
    string? SecondaryReceiptWriteFailureType,
    string? SecondaryReceiptWriteFailureMessageSha256,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', ContractVersion, NativeBlocker,
        AdapterId, ReceiptPath, ReceiptSha256, ParseClassification, ExceptionType,
        ExceptionMessageSha256, RawOutputRecorded,
        SecondaryReceiptWriteFailureType ?? string.Empty,
        SecondaryReceiptWriteFailureMessageSha256 ?? string.Empty);
}

public static class Arch7bChildOutputClassifier
{
    public const string PureExpected = "A_PURE_SINGLE_JSON_EXPECTED_SHAPE";
    public const string WrapperContamination =
        "B_STDOUT_NPM_OR_WRAPPER_PREFIX_CONTAMINATION";
    public const string MultipleDocuments =
        "C_MULTIPLE_JSON_DOCUMENTS_OR_LOG_LINES";
    public const string BomOnly = "D_UTF8_BOM_ONLY";
    public const string ShapeMismatch = "E_CORE_NATIVE_SHAPE_MISMATCH";
    public const string InvalidUtf8 = "F_INVALID_UTF8";
    public const string Other = "G_OTHER_EXACTLY_PROVEN";

    public static string Classify(string value)
    {
        if (value.StartsWith('\uFEFF')) return BomOnly;
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.EnumerateObject().Select(item => item.Name)
                       .Order(StringComparer.Ordinal).SequenceEqual(
                           new[] { "manifest", "qualification" }, StringComparer.Ordinal)
                ? PureExpected : ShapeMismatch;
        }
        catch (JsonException)
        {
            var trimmed = value.Trim();
            if (ContainsMultipleJsonDocuments(trimmed)) return MultipleDocuments;
            var first = trimmed.IndexOf('{');
            var last = trimmed.LastIndexOf('}');
            return first >= 0 && last > first &&
                   (first != 0 || last != trimmed.Length - 1)
                ? WrapperContamination : Other;
        }
    }

    private static bool ContainsMultipleJsonDocuments(string value)
    {
        for (var index = 0; index < value.Length - 1; index++)
        {
            if (value[index] != '}') continue;
            var next = index + 1;
            while (next < value.Length && char.IsWhiteSpace(value[next])) next++;
            if (next < value.Length && value[next] == '{') return true;
        }
        return false;
    }
}

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
    string OutputReceiptPath,
    string OutputReceiptSha256,
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
            int bytesUsed;
            int charsUsed;
            try
            {
                decoder.Convert(bytes, 0, read, chars, 0, chars.Length, false,
                    out bytesUsed, out charsUsed, out _);
            }
            catch (DecoderFallbackException)
            {
                throw new Arch7bQualificationException(
                    Arch7bBlockers.ChildOutputInvalid,
                    Arch7bChildOutputClassifier.InvalidUtf8);
            }
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
        int flushedChars;
        try
        {
            decoder.Convert([], 0, 0, chars, 0, chars.Length, true,
                out _, out flushedChars, out _);
        }
        catch (DecoderFallbackException)
        {
            throw new Arch7bQualificationException(
                Arch7bBlockers.ChildOutputInvalid,
                Arch7bChildOutputClassifier.InvalidUtf8);
        }
        if (flushedChars > 0) text.Append(chars, 0, flushedChars);
        return new(count, Convert.ToHexStringLower(hash.GetHashAndReset()), text.ToString(),
            exactSecrets.Count, true, false);
    }
}

public sealed class Arch7bOneShotProcessRunnerV2
{
    private static readonly string[] InheritedSystemVariables = OperatingSystem.IsWindows()
        ? ["SystemRoot", "WINDIR", "TEMP", "TMP", "COMSPEC", "PATHEXT"]
        : ["HOME", "TMPDIR"];
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
                var completedAt = DateTimeOffset.UtcNow;
                var receiptPath = Path.Combine(
                    Path.GetDirectoryName(command.AuthorityPath)!,
                    "child-process-output-receipt.json");
                string receiptSha256 = string.Empty;
                Exception? receiptFailure = null;
                try
                {
                    receiptSha256 = await WriteReceiptAsync(receiptPath, command,
                        process, startedAt, completedAt, stopwatch.ElapsedMilliseconds,
                        outputs[0], outputs[1], cancellationToken).ConfigureAwait(false);
                }
                catch (Exception failure)
                {
                    receiptFailure = failure;
                }
                var adapter = adapters.Require(command.AdapterId);
                Arch7bNormalizedChildResult normalized;
                try
                {
                    normalized = await adapter.AdaptAsync(outputs[0].Text, command,
                        runRoot, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception adapterFailure)
                {
                    await TryWriteAdapterFailureAsync(command, outputs[0].Text,
                        receiptPath, receiptSha256, receiptFailure, adapterFailure,
                        CancellationToken.None).ConfigureAwait(false);
                    throw;
                }
                if (receiptFailure is not null)
                    throw new Arch7bQualificationException(
                        Arch7bV2Blockers.ChildOutputReceiptWriteFailed,
                        Arch7bOneShotContracts.Sha256(receiptFailure.Message));
                if (process.ExitCode != 0)
                    throw new Arch7bQualificationException(
                        Arch7bBlockers.ChildProcessFailedUncatalogued,
                        command.CommandId);
                var canonical = string.Join('\n', command.CommandId, command.StageId, process.Id,
                    process.ExitCode, startedAt.ToString("O"), completedAt.ToString("O"),
                    stopwatch.ElapsedMilliseconds, outputs[0].Sha256, outputs[1].Sha256,
                    normalized.EvidenceSha256, command.EvidenceSha256, receiptSha256);
                return new(command.CommandId, command.StageId, process.Id, process.ExitCode,
                    startedAt, completedAt, stopwatch.ElapsedMilliseconds, outputs[0], outputs[1],
                    normalized, command.EvidenceSha256, receiptPath, receiptSha256,
                    Arch7bOneShotContracts.Sha256(canonical));
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
                             Path.Combine(runRoot, processKey + ".COMPLETE.signal.tmp"),
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
        longLived.Add(processKey, new(process, stdout, stderr, timeout, command));
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
        var signalTemporary = signalPath + ".tmp";
        await File.WriteAllTextAsync(signalTemporary, "COMPLETE", new UTF8Encoding(false), cancellationToken)
            .ConfigureAwait(false);
        File.Move(signalTemporary, signalPath);
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
        if (handle.Process.ExitCode != 0)
            throw new Arch7bQualificationException(Arch7bBlockers.ChildProcessFailedUncatalogued,
                command.CommandId);
        var outputs = await Task.WhenAll(handle.StandardOutput, handle.StandardError).ConfigureAwait(false);
        var producerCommand = handle.Command;
        var result = await adapters.Require(producerCommand.AdapterId).AdaptAsync(
            outputs[0].Text, producerCommand, runRoot, cancellationToken).ConfigureAwait(false);
        handle.Timeout.Dispose();
        longLived.Remove(processKey);
        return result;
    }

    private static async Task<string> WriteReceiptAsync(string path,
        Arch7bOneShotMaterializedCommand command, Process process,
        DateTimeOffset startedAt, DateTimeOffset completedAt,
        long elapsedMilliseconds, Arch7bBoundedProcessOutput stdout,
        Arch7bBoundedProcessOutput stderr, CancellationToken cancellationToken)
    {
        var provisional = new Arch7bChildProcessOutputReceipt(
            Arch7bV2Contracts.ChildProcessOutputReceiptVersion,
            command.CommandId, command.StageId, process.Id, process.ExitCode,
            startedAt, completedAt, elapsedMilliseconds, stdout.ByteCount,
            stdout.Sha256, stderr.ByteCount, stderr.Sha256, true,
            stdout.SecretScanPassed && stderr.SecretScanPassed,
            Math.Max(stdout.SecretValueCountChecked,
                stderr.SecretValueCountChecked), false, command.AdapterId,
            command.ExpectedNativeOutputContract, command.EvidenceSha256,
            string.Empty);
        var receipt = provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(
                provisional.Canonical())
        };
        return await WriteCreateNewAtomicAsync(path, receipt, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task TryWriteAdapterFailureAsync(
        Arch7bOneShotMaterializedCommand command, string stdout,
        string receiptPath, string receiptSha256, Exception? receiptFailure,
        Exception adapterFailure, CancellationToken cancellationToken)
    {
        var blocker = adapterFailure is Arch7bQualificationException qualification
            ? qualification.BlockerCode
            : Arch7bBlockers.ChildOutputInvalid;
        var provisional = new Arch7bChildAdapterFailureEvidence(
            Arch7bV2Contracts.ChildAdapterFailureVersion, blocker,
            command.AdapterId, receiptPath, receiptSha256,
            Arch7bChildOutputClassifier.Classify(stdout),
            adapterFailure.GetType().Name,
            Arch7bOneShotContracts.Sha256(adapterFailure.Message), false,
            receiptFailure?.GetType().Name,
            receiptFailure is null ? null :
                Arch7bOneShotContracts.Sha256(receiptFailure.Message),
            string.Empty);
        var evidence = provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(
                provisional.Canonical())
        };
        try
        {
            await WriteCreateNewAtomicAsync(Path.Combine(
                    Path.GetDirectoryName(command.AuthorityPath)!,
                    "child-adapter-failure.json"), evidence, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // The original adapter blocker remains authoritative.
        }
    }

    private static async Task<string> WriteCreateNewAtomicAsync<T>(
        string path, T value, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value,
            Arch7bJson.CanonicalOptions);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew,
                             FileAccess.Write, FileShare.None, 4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(temporary, path, false);
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
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
        Arch7bSealedNonSecretEnvironment.ValidateMaterialized(command.NonSecretEnvironment,
            command.ExecutablePath);
        foreach (var variable in command.NonSecretEnvironment)
            value.Environment[variable.VariableName] = variable.Value;
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
        CancellationTokenSource Timeout,
        Arch7bOneShotMaterializedCommand Command);
}
