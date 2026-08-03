using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bOneShotOperatorAuthorization(
    string ContractVersion,
    string OperatorAuthorizationId,
    string TargetEnvironment,
    bool NoOrder,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string EvidenceSha256);

public sealed record Arch7bOneShotLiveExecutionAuthority(
    string ContractVersion,
    string SupervisorCommit,
    string SupervisorTree,
    string CoreCommit,
    string CoreTree,
    string IntradayCommit,
    string IntradayTree,
    string FreezeManifestSha256,
    string FreezePacketSha256,
    string RuntimeInventorySha256,
    string CoreRepositoryAuthoritySha256,
    string CoreTrackedInventorySha256,
    string StaticAuthoritySetSha256,
    string CommandAuthoritySetSha256,
    string OperatorAuthorizationId,
    string TargetEnvironment,
    string AccountId,
    bool NoOrder,
    int MaximumSlots,
    int MaximumRdsReads,
    int MaximumCaptures,
    int MaximumRetries,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', ContractVersion, SupervisorCommit, SupervisorTree,
        CoreCommit, CoreTree, IntradayCommit, IntradayTree, FreezeManifestSha256, FreezePacketSha256,
        RuntimeInventorySha256, CoreRepositoryAuthoritySha256, CoreTrackedInventorySha256,
        StaticAuthoritySetSha256, CommandAuthoritySetSha256, OperatorAuthorizationId, TargetEnvironment,
        AccountId, NoOrder, MaximumSlots, MaximumRdsReads, MaximumCaptures, MaximumRetries,
        IssuedAtUtc.ToUniversalTime().ToString("O"), ExpiresAtUtc.ToUniversalTime().ToString("O"));

    public void Validate(Arch7bOneShotLivePlan plan, string expectedEvidenceSha256,
        string operatorAuthorizationId, DateTimeOffset nowUtc)
    {
        Require(ContractVersion == Arch7bOneShotContracts.LiveExecutionAuthorityVersion,
            Arch7bBlockers.LiveAuthorityMissing);
        Require(Arch7bOneShotContracts.Sha256(Canonical()) == EvidenceSha256 &&
            EvidenceSha256 == expectedEvidenceSha256, Arch7bBlockers.CommandAuthorityMismatch);
        Require(ExpiresAtUtc > nowUtc && IssuedAtUtc <= nowUtc, Arch7bBlockers.LiveAuthorityExpired);
        Require(OperatorAuthorizationId == operatorAuthorizationId,
            Arch7bBlockers.OperatorAuthorizationMismatch);
        Require(TargetEnvironment == "TEST", Arch7bBlockers.TargetEnvironmentNotTest);
        Require(NoOrder && plan.NoOrder, Arch7bBlockers.NoOrderRequired);
        Require(CoreCommit == plan.CoreCommit && CoreTree == plan.CoreTree &&
            IntradayCommit == plan.IntradayCommit && IntradayTree == plan.IntradayTree,
            Arch7bBlockers.LiveAuthorityCommitMismatch);
        Require(FreezeManifestSha256 == plan.FreezeManifestSha256 &&
            FreezePacketSha256 == plan.FreezePacketSha256, Arch7bBlockers.FreezeAuthorityMismatch);
        Require(CommandAuthoritySetSha256 == plan.CommandAuthoritySetSha256,
            Arch7bBlockers.CommandAuthorityMismatch);
        Require(MaximumSlots == 1 && MaximumRdsReads == 2 && MaximumCaptures == 1 &&
            MaximumRetries == 0, Arch7bBlockers.LiveCommandAuthorityIncomplete);
        Require(AccountId == "1754288005", Arch7bBlockers.LiveCommandAuthorityIncomplete);
    }

    private static void Require(bool condition, string blocker)
    {
        if (!condition) throw new Arch7bQualificationException(blocker);
    }
}

public sealed record Arch7bOneShotProcessEnvironmentAuthority(
    string ContractVersion,
    string CommandId,
    IReadOnlyList<string> EnvironmentAllowlist,
    IReadOnlyList<string> SecretVariableNames,
    IReadOnlyList<string> InheritedSystemVariables,
    string ParentEnvironmentSha256,
    string ChildEnvironmentNonSecretSha256,
    bool ParentEnvironmentMutated,
    bool MachineEnvironmentMutated,
    bool UserEnvironmentMutated,
    bool ChildEnvironmentReleased);

public sealed record Arch7bOneShotCommandAuthority(
    string CommandId,
    string StageId,
    string ExecutablePath,
    string ExecutableSha256,
    IReadOnlyList<string> ArgumentList,
    string WorkingDirectory,
    Arch7bOneShotProcessEnvironmentAuthority EnvironmentAuthority,
    string OutputContract,
    int TimeoutSeconds,
    string CleanupResourceType,
    bool CausesRdsRead,
    bool CausesCapture,
    bool ReadsSecret,
    string? MarkerPath,
    string EvidenceSha256);

public sealed record Arch7bOneShotLivePlan(
    string ContractVersion,
    string CoreCommit,
    string CoreTree,
    string IntradayCommit,
    string IntradayTree,
    string FreezeManifestSha256,
    string FreezePacketSha256,
    string CommandAuthoritySetSha256,
    string OperatorAuthorizationId,
    string RunId,
    string RunRoot,
    bool NoOrder,
    bool Synthetic,
    IReadOnlyList<Arch7bOneShotCommandAuthority> Commands,
    string EvidenceSha256);

public sealed record Arch7bOneShotBudgetSnapshot(int Slots, int RdsReads, int Captures, int Retries);

public sealed record Arch7bOneShotCommandResult(
    string ContractVersion,
    string StageId,
    string CommandId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    long ElapsedMilliseconds,
    int ProcessId,
    int ExitCode,
    long StdoutByteCount,
    long StderrByteCount,
    string StdoutSha256,
    string StderrSha256,
    string ResultCode,
    IReadOnlyList<string> OutputArtifactPaths,
    IReadOnlyList<string> OutputArtifactSha256,
    string SloStatus,
    string EvidenceSha256);

public sealed record Arch7bOneShotStageEvidence(
    string ContractVersion,
    string StageId,
    string CommandId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    long ElapsedMilliseconds,
    int ProcessId,
    int ExitCode,
    long StdoutByteCount,
    long StderrByteCount,
    string StdoutSha256,
    string StderrSha256,
    string ResultCode,
    IReadOnlyList<string> OutputArtifactPaths,
    IReadOnlyList<string> OutputArtifactSha256,
    string SloStatus,
    Arch7bOneShotBudgetSnapshot BudgetSnapshot,
    bool NoOrder,
    string EvidenceSha256);

public sealed record Arch7bOneShotPrimaryFailureEvidence(
    string FirstBlockerCode,
    string FailureStage,
    string CommandId,
    string SanitizedExceptionType,
    string MessageSha256,
    string? StageEvidenceSha256);

public sealed class Arch7bOneShotPrimaryFailure : Exception
{
    public Arch7bOneShotPrimaryFailure(string blocker, string stage, string commandId,
        Exception? inner = null, string? stageEvidenceSha256 = null)
        : base(blocker, inner)
    {
        Evidence = new(blocker, stage, commandId, inner?.GetType().Name ?? GetType().Name,
            Arch7bOneShotContracts.Sha256(inner?.Message ?? blocker), stageEvidenceSha256);
    }

    public Arch7bOneShotPrimaryFailureEvidence Evidence { get; }
}

public sealed record Arch7bOneShotLiveExecutionEvidence(
    string ContractVersion,
    string RunId,
    IReadOnlyList<Arch7bOneShotStageEvidence> Stages,
    Arch7bOneShotBudgetSnapshot Budget,
    string FinalBlocker,
    Arch7bOneShotPrimaryFailureEvidence? PrimaryFailure,
    Arch7bCleanupReport Cleanup,
    int ResidualProcessCount,
    int ResidualMarkerCount,
    bool Passed,
    Arch7bNoLiveSafetyCounters Safety,
    string EvidenceSha256);

public sealed record Arch7bOneShotSupervisorExecutionGap(
    string ContractVersion,
    string MasterCommit,
    bool QualificationOnlyRequired,
    bool RunOneShotModePresent,
    bool RealProcessRunnerPresent,
    bool RealChildEvidenceConsumed,
    string Verdict,
    string EvidenceSha256)
{
    public static Arch7bOneShotSupervisorExecutionGap Create(string masterCommit)
    {
        var canonical = string.Join('\n', Arch7bOneShotContracts.ExecutionGapVersion, masterCommit,
            true, false, false, false, Arch7bOneShotContracts.ExecutionGapVerdict);
        return new(Arch7bOneShotContracts.ExecutionGapVersion, masterCommit, true, false, false, false,
            Arch7bOneShotContracts.ExecutionGapVerdict, Arch7bOneShotContracts.Sha256(canonical));
    }
}

public static class Arch7bOneShotAuthorityLoader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static async Task<(Arch7bOneShotLiveExecutionAuthority Authority, string FileSha256)> LoadAuthorityAsync(
        string path, string expectedSha256, CancellationToken cancellationToken = default)
    {
        RequireAbsolute(path);
        if (!File.Exists(path)) throw new Arch7bQualificationException(Arch7bBlockers.LiveAuthorityMissing);
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        if (sha != expectedSha256) throw new Arch7bQualificationException(Arch7bBlockers.CommandAuthorityMismatch);
        var value = JsonSerializer.Deserialize<Arch7bOneShotLiveExecutionAuthority>(bytes, Options)
            ?? throw new Arch7bQualificationException(Arch7bBlockers.LiveAuthorityMissing);
        return (value, sha);
    }

    public static async Task<Arch7bOneShotLivePlan> LoadPlanAsync(string freezeRoot,
        string expectedManifestSha256, CancellationToken cancellationToken = default)
    {
        RequireAbsolute(freezeRoot);
        var manifest = Path.Combine(freezeRoot, "arch7b-one-shot-live-plan.json");
        if (!File.Exists(manifest)) throw new Arch7bQualificationException(Arch7bBlockers.LiveCommandAuthorityIncomplete);
        var bytes = await File.ReadAllBytesAsync(manifest, cancellationToken).ConfigureAwait(false);
        var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        if (sha != expectedManifestSha256) throw new Arch7bQualificationException(Arch7bBlockers.FreezeAuthorityMismatch);
        return JsonSerializer.Deserialize<Arch7bOneShotLivePlan>(bytes, Options)
            ?? throw new Arch7bQualificationException(Arch7bBlockers.LiveCommandAuthorityIncomplete);
    }

    public static void ValidatePlan(Arch7bOneShotLivePlan plan)
    {
        if (plan.ContractVersion != Arch7bOneShotContracts.LivePlanVersion || !plan.NoOrder ||
            plan.Commands.Count != Arch7bStages.All.Count ||
            !plan.Commands.Select(value => value.StageId).SequenceEqual(Arch7bStages.All, StringComparer.Ordinal))
            throw new Arch7bQualificationException(Arch7bBlockers.LiveCommandAuthorityIncomplete);
        RequireAbsolute(plan.RunRoot);
        foreach (var command in plan.Commands) ValidateCommand(command, plan.RunRoot);
        var commandSet = Arch7bOneShotContracts.Sha256(string.Join('\n', plan.Commands.Select(value => value.EvidenceSha256)));
        if (commandSet != plan.CommandAuthoritySetSha256)
            throw new Arch7bQualificationException(Arch7bBlockers.CommandAuthorityMismatch);
        var canonical = string.Join('\n', plan.ContractVersion, plan.CoreCommit, plan.CoreTree,
            plan.IntradayCommit, plan.IntradayTree, plan.FreezeManifestSha256, plan.FreezePacketSha256,
            plan.CommandAuthoritySetSha256, plan.OperatorAuthorizationId, plan.RunId, plan.RunRoot,
            plan.NoOrder, plan.Synthetic);
        if (Arch7bOneShotContracts.Sha256(canonical) != plan.EvidenceSha256)
            throw new Arch7bQualificationException(Arch7bBlockers.CommandAuthorityMismatch);
    }

    public static void ValidateCommand(Arch7bOneShotCommandAuthority command, string runRoot)
    {
        RequireAbsolute(command.ExecutablePath);
        RequireAbsolute(command.WorkingDirectory);
        if (!Arch7bOneShotContracts.IsSha256(command.ExecutableSha256) || command.TimeoutSeconds <= 0 ||
            string.IsNullOrWhiteSpace(command.OutputContract) || string.IsNullOrWhiteSpace(command.CleanupResourceType))
            throw new Arch7bQualificationException(Arch7bBlockers.LiveCommandAuthorityIncomplete);
        if (command.ArgumentList.Any(IsSecretArgument))
            throw new Arch7bQualificationException(Arch7bBlockers.SecretInArgument);
        var executable = Path.GetFileName(command.ExecutablePath);
        if (executable.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) ||
            executable.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase) ||
            executable.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase))
            throw new Arch7bQualificationException(Arch7bBlockers.AmbientPathForbidden);
        if (command.MarkerPath is not null)
        {
            RequireAbsolute(command.MarkerPath);
            RequireInside(runRoot, command.MarkerPath);
        }
    }

    public static void RequireAbsolute(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new Arch7bQualificationException(Arch7bBlockers.AbsolutePathRequired, path);
    }

    public static void RequireInside(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(path);
        if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new Arch7bQualificationException(Arch7bBlockers.CleanupPathOutsideRunRoot, candidate);
    }

    private static bool IsSecretArgument(string value)
    {
        var normalized = value.ToLowerInvariant();
        return normalized.Contains("password=", StringComparison.Ordinal) ||
            normalized.Contains("secret=", StringComparison.Ordinal) ||
            normalized.Contains("token=", StringComparison.Ordinal) ||
            normalized.Contains("connectionstring", StringComparison.Ordinal);
    }
}
