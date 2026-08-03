using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public interface IArch7bOneShotCommandRunner
{
    Task<Arch7bOneShotCommandResult> RunAsync(Arch7bOneShotCommandAuthority command,
        string runRoot, Arch7bTerminalCleanupSupervisor cleanup, CancellationToken cancellationToken = default);
}

public sealed record Arch7bChildOutputEnvelope(
    string ContractVersion,
    string ResultCode,
    IReadOnlyList<string> OutputArtifactPaths,
    IReadOnlyList<string> OutputArtifactSha256);

public sealed class Arch7bOneShotProcessCommandRunner : IArch7bOneShotCommandRunner
{
    private const int MaximumOutputBytes = 1_048_576;
    private const string SecretSentinel = "ARCH7B_SECRET_SENTINEL";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<Arch7bOneShotCommandResult> RunAsync(Arch7bOneShotCommandAuthority command,
        string runRoot, Arch7bTerminalCleanupSupervisor cleanup, CancellationToken cancellationToken = default)
    {
        Arch7bOneShotAuthorityLoader.ValidateCommand(command, runRoot);
        ValidateEnvironmentAuthority(command);
        if (!File.Exists(command.ExecutablePath))
            throw new Arch7bQualificationException(Arch7bBlockers.LiveCommandAuthorityIncomplete,
                command.ExecutablePath);
        var executableSha = Convert.ToHexStringLower(SHA256.HashData(
            await File.ReadAllBytesAsync(command.ExecutablePath, cancellationToken).ConfigureAwait(false)));
        if (executableSha != command.ExecutableSha256)
            throw new Arch7bQualificationException(Arch7bBlockers.ExecutableShaMismatch, command.CommandId);

        Process? process = null;
        var resourceId = $"process:{command.CommandId}";
        cleanup.Register(new(resourceId, command.CleanupResourceType, command.StageId, command.CommandId,
            false, "TERMINAL_ALWAYS", "KILL_PROCESS_TREE_AND_RELEASE_ENVIRONMENT", TimeSpan.FromSeconds(5),
            true, true, true, Arch7bCleanupState.Registered, null), async token =>
            {
                if (process is not null)
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                        await process.WaitForExitAsync(token).ConfigureAwait(false);
                    }
                    process.Dispose();
                }
                return Arch7bOneShotContracts.Sha256(resourceId + ":released");
            });

        if (command.MarkerPath is not null)
        {
            var markerId = $"marker:{command.CommandId}";
            cleanup.Register(new(markerId, "lease-marker", command.StageId, command.CommandId, false,
                "TERMINAL_ALWAYS", "REMOVE_MARKER", TimeSpan.FromSeconds(2), false, true, true,
                Arch7bCleanupState.Registered, null, command.MarkerPath), _ =>
                {
                    if (File.Exists(command.MarkerPath)) File.Delete(command.MarkerPath);
                    return Task.FromResult(Arch7bOneShotContracts.Sha256(markerId + ":removed"));
                });
        }

        var startInfo = BuildStartInfo(command);
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new Arch7bQualificationException(Arch7bBlockers.ChildProcessFailedUncatalogued,
                command.CommandId);
        cleanup.MarkCreated(resourceId);
        if (command.MarkerPath is not null) cleanup.MarkCreated($"marker:{command.CommandId}");
        var processId = process.Id;
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(command.TimeoutSeconds));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited) process.Kill(true);
            throw new Arch7bQualificationException(Arch7bBlockers.ChildProcessTimeout, command.CommandId);
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        stopwatch.Stop();
        var stdoutBytes = Encoding.UTF8.GetBytes(stdout);
        var stderrBytes = Encoding.UTF8.GetBytes(stderr);
        if (stdoutBytes.Length > MaximumOutputBytes || stderrBytes.Length > MaximumOutputBytes)
            throw new Arch7bQualificationException(Arch7bBlockers.ChildOutputInvalid, command.CommandId);
        if (stdout.Contains(SecretSentinel, StringComparison.Ordinal) ||
            stderr.Contains(SecretSentinel, StringComparison.Ordinal))
            throw new Arch7bQualificationException(Arch7bBlockers.ChildOutputSecretDetected, command.CommandId);

        Arch7bChildOutputEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<Arch7bChildOutputEnvelope>(stdout, JsonOptions)
                ?? throw new JsonException("empty envelope");
        }
        catch (JsonException exception)
        {
            throw new Arch7bQualificationException(process.ExitCode == 0
                ? Arch7bBlockers.ChildOutputInvalid : Arch7bBlockers.ChildProcessFailedUncatalogued,
                $"{command.CommandId}:{exception.GetType().Name}");
        }
        if (envelope.ContractVersion != command.OutputContract ||
            envelope.OutputArtifactPaths.Count != envelope.OutputArtifactSha256.Count)
            throw new Arch7bQualificationException(Arch7bBlockers.ChildOutputInvalid, command.CommandId);
        for (var index = 0; index < envelope.OutputArtifactPaths.Count; index++)
        {
            var path = envelope.OutputArtifactPaths[index];
            Arch7bOneShotAuthorityLoader.RequireAbsolute(path);
            Arch7bOneShotAuthorityLoader.RequireInside(runRoot, path);
            if (!File.Exists(path))
                throw new Arch7bQualificationException(Arch7bBlockers.ChildEvidenceMissing, command.CommandId);
            var actual = Convert.ToHexStringLower(SHA256.HashData(
                await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)));
            if (actual != envelope.OutputArtifactSha256[index])
                throw new Arch7bQualificationException(Arch7bBlockers.ChildOutputShaMismatch, command.CommandId);
        }

        var completedAt = DateTimeOffset.UtcNow;
        var sloStatus = stopwatch.Elapsed <= TimeSpan.FromSeconds(command.TimeoutSeconds) ? "PASS" : "FAIL";
        var canonical = string.Join('\n', Arch7bOneShotContracts.CommandResultVersion, command.StageId,
            command.CommandId, startedAt.ToString("O"), completedAt.ToString("O"), stopwatch.ElapsedMilliseconds,
            processId, process.ExitCode, stdoutBytes.Length, stderrBytes.Length,
            Convert.ToHexStringLower(SHA256.HashData(stdoutBytes)),
            Convert.ToHexStringLower(SHA256.HashData(stderrBytes)), envelope.ResultCode,
            string.Join('|', envelope.OutputArtifactPaths), string.Join('|', envelope.OutputArtifactSha256), sloStatus);
        return new(Arch7bOneShotContracts.CommandResultVersion, command.StageId, command.CommandId,
            startedAt, completedAt, stopwatch.ElapsedMilliseconds, processId, process.ExitCode,
            stdoutBytes.Length, stderrBytes.Length, Convert.ToHexStringLower(SHA256.HashData(stdoutBytes)),
            Convert.ToHexStringLower(SHA256.HashData(stderrBytes)), envelope.ResultCode,
            envelope.OutputArtifactPaths, envelope.OutputArtifactSha256, sloStatus,
            Arch7bOneShotContracts.Sha256(canonical));
    }

    private static ProcessStartInfo BuildStartInfo(Arch7bOneShotCommandAuthority command)
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
        foreach (var name in command.EnvironmentAuthority.InheritedSystemVariables)
        {
            var environmentValue = Environment.GetEnvironmentVariable(name);
            if (environmentValue is not null) value.Environment[name] = environmentValue;
        }
        return value;
    }

    private static void ValidateEnvironmentAuthority(Arch7bOneShotCommandAuthority command)
    {
        var value = command.EnvironmentAuthority;
        if (value.ContractVersion != Arch7bOneShotContracts.ProcessEnvironmentAuthorityVersion ||
            value.CommandId != command.CommandId || value.ParentEnvironmentMutated ||
            value.MachineEnvironmentMutated || value.UserEnvironmentMutated || !value.ChildEnvironmentReleased ||
            value.InheritedSystemVariables.Except(value.EnvironmentAllowlist, StringComparer.OrdinalIgnoreCase).Any())
            throw new Arch7bQualificationException(Arch7bBlockers.LiveCommandAuthorityIncomplete,
                command.CommandId);
    }
}
