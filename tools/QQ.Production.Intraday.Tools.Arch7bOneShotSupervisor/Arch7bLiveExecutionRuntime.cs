using System.Collections;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed class Arch7bOneShotLiveExecutionRuntime
{
    private static readonly ConcurrentDictionary<string, byte> UsedRunRoots = new(StringComparer.OrdinalIgnoreCase);
    private readonly IArch7bOneShotCommandRunner runner;

    public Arch7bOneShotLiveExecutionRuntime(IArch7bOneShotCommandRunner runner)
    {
        this.runner = runner;
    }

    public async Task<Arch7bOneShotLiveExecutionEvidence> RunOneShotAsync(Arch7bOneShotLivePlan plan,
        Arch7bOneShotLiveExecutionAuthority authority, string expectedAuthorityEvidenceSha256,
        string operatorAuthorizationId, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        Arch7bOneShotAuthorityLoader.ValidatePlan(plan);
        authority.Validate(plan, expectedAuthorityEvidenceSha256, operatorAuthorizationId, nowUtc);
        var runRoot = Path.GetFullPath(plan.RunRoot);
        if (Directory.Exists(runRoot))
            throw new Arch7bQualificationException(Directory.EnumerateFileSystemEntries(runRoot).Any()
                ? Arch7bBlockers.RunRootNotEmpty : Arch7bBlockers.RunRootReused);
        if (!UsedRunRoots.TryAdd(runRoot, 0))
            throw new Arch7bQualificationException(Arch7bBlockers.RunRootReused);
        Directory.CreateDirectory(runRoot);

        var cleanup = new Arch7bTerminalCleanupSupervisor(runRoot);
        var budget = new Arch7bOneShotBudget();
        var stages = new List<Arch7bOneShotStageEvidence>();
        Arch7bOneShotPrimaryFailureEvidence? primary = null;
        var currentStage = "STATIC_AUTHORITY_VALIDATION";
        var currentCommand = "STATIC_AUTHORITY_VALIDATION";
        var finalBlocker = Arch7bOneShotContracts.ExpectedFinalBlocker;
        var bracketStarted = false;
        budget.RecordSlot();
        try
        {
            foreach (var command in plan.Commands)
            {
                currentStage = command.StageId;
                currentCommand = command.CommandId;
                if (command.ReadsSecret && bracketStarted)
                    throw new Arch7bQualificationException(Arch7bBlockers.SecretReadAfterBracket, command.CommandId);
                if (command.CausesRdsRead) budget.RecordRdsRead();
                if (command.CausesCapture) budget.RecordCapture();
                var result = await runner.RunAsync(command, runRoot, cleanup, cancellationToken).ConfigureAwait(false);
                var snapshot = Snapshot(budget);
                var canonical = string.Join('\n', Arch7bOneShotContracts.StageEvidenceVersion,
                    result.EvidenceSha256, snapshot.Slots, snapshot.RdsReads, snapshot.Captures,
                    snapshot.Retries, plan.NoOrder);
                var stage = new Arch7bOneShotStageEvidence(Arch7bOneShotContracts.StageEvidenceVersion,
                    result.StageId, result.CommandId, result.StartedAtUtc, result.CompletedAtUtc,
                    result.ElapsedMilliseconds, result.ProcessId, result.ExitCode, result.StdoutByteCount,
                    result.StderrByteCount, result.StdoutSha256, result.StderrSha256, result.ResultCode,
                    result.OutputArtifactPaths, result.OutputArtifactSha256, result.SloStatus, snapshot,
                    plan.NoOrder, Arch7bOneShotContracts.Sha256(canonical));
                stages.Add(stage);
                if (result.ResultCode != "SUCCESS")
                {
                    if (command.StageId == "FINAL_WORKING_ORDER_PREFLIGHT" &&
                        result.ResultCode == Arch7bOneShotContracts.ExpectedFinalBlocker)
                    {
                        finalBlocker = result.ResultCode;
                    }
                    else
                    {
                        throw new Arch7bOneShotPrimaryFailure(result.ResultCode, command.StageId,
                            command.CommandId, stageEvidenceSha256: stage.EvidenceSha256);
                    }
                }
                if (command.StageId == "BRACKET_T0") bracketStarted = true;
            }
        }
        catch (Arch7bOneShotPrimaryFailure failure)
        {
            primary = failure.Evidence;
            finalBlocker = primary.FirstBlockerCode;
        }
        catch (Arch7bQualificationException failure)
        {
            primary = new(failure.BlockerCode, currentStage, currentCommand, failure.GetType().Name,
                Arch7bOneShotContracts.Sha256(failure.Message), stages.LastOrDefault()?.EvidenceSha256);
            finalBlocker = primary.FirstBlockerCode;
        }
        catch (Exception failure)
        {
            primary = new(Arch7bBlockers.ChildProcessFailedUncatalogued, currentStage, currentCommand,
                failure.GetType().Name, Arch7bOneShotContracts.Sha256(failure.Message),
                stages.LastOrDefault()?.EvidenceSha256);
            finalBlocker = primary.FirstBlockerCode;
        }

        var cleanupReport = await cleanup.CleanupAllAsync(finalBlocker, cancellationToken).ConfigureAwait(false);
        var residualProcesses = cleanup.Resources.Count(value => value.Created &&
            value.ResourceType.Contains("process", StringComparison.OrdinalIgnoreCase) &&
            value.CleanupState != Arch7bCleanupState.Cleaned);
        var residualMarkers = plan.Commands.Count(value => value.MarkerPath is not null && File.Exists(value.MarkerPath));
        var finalBudget = Snapshot(budget);
        var passed = primary is null && cleanupReport.Complete && stages.Count == Arch7bStages.All.Count &&
            stages.Select(value => value.StageId).SequenceEqual(Arch7bStages.All, StringComparer.Ordinal) &&
            finalBlocker == Arch7bOneShotContracts.ExpectedFinalBlocker && finalBudget is
            { Slots: 1, RdsReads: 2, Captures: 1, Retries: 0 } &&
            residualProcesses == 0 && residualMarkers == 0;
        var evidenceCanonical = string.Join('\n', Arch7bOneShotContracts.LiveExecutionRuntimeVersion,
            plan.RunId, string.Join('|', stages.Select(value => value.EvidenceSha256)),
            finalBudget.Slots, finalBudget.RdsReads, finalBudget.Captures, finalBudget.Retries,
            finalBlocker, primary?.MessageSha256 ?? string.Empty, cleanupReport.EvidenceSha256,
            residualProcesses, residualMarkers, passed);
        return new(Arch7bOneShotContracts.LiveExecutionRuntimeVersion, plan.RunId, stages, finalBudget,
            finalBlocker, primary, cleanupReport, residualProcesses, residualMarkers, passed,
            Arch7bNoLiveSafetyCounters.Zero, Arch7bOneShotContracts.Sha256(evidenceCanonical));
    }

    private static Arch7bOneShotBudgetSnapshot Snapshot(Arch7bOneShotBudget value) =>
        new(value.Slots, value.RdsReads, value.Captures, value.Retries);
}

public static class Arch7bSyntheticLiveExecutionFactory
{
    public static (Arch7bOneShotLivePlan Plan, Arch7bOneShotLiveExecutionAuthority Authority)
        Create(string executablePath, string runRoot, string runId,
            string? failureStage = null, string failureBehavior = "blocker")
    {
        executablePath = Path.GetFullPath(executablePath);
        runRoot = Path.GetFullPath(runRoot);
        var executableSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(executablePath)));
        var parentNames = Environment.GetEnvironmentVariables().Keys.Cast<object>()
            .Select(value => value.ToString() ?? string.Empty).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var parentSha = Arch7bOneShotContracts.Sha256(string.Join('|', parentNames));
        string[] inherited = OperatingSystem.IsWindows()
            ? ["SystemRoot", "WINDIR", "TEMP", "TMP", "DOTNET_ROOT"]
            : ["HOME", "TMPDIR", "DOTNET_ROOT"];
        var commands = new List<Arch7bOneShotCommandAuthority>();
        foreach (var stage in Arch7bStages.All)
        {
            var commandId = "synthetic-" + stage.ToLowerInvariant().Replace('_', '-');
            var behavior = stage == failureStage ? failureBehavior :
                stage == "FINAL_WORKING_ORDER_PREFLIGHT" ? "expected-blocker" : "success";
            var markerPath = behavior == "marker" ? Path.Combine(runRoot, commandId + ".marker") : null;
            var environment = new Arch7bOneShotProcessEnvironmentAuthority(
                Arch7bOneShotContracts.ProcessEnvironmentAuthorityVersion, commandId, inherited, [], inherited,
                parentSha, Arch7bOneShotContracts.Sha256(string.Join('|', inherited)), false, false, false, true);
            var arguments = new List<string>
            {
                "--mode", "fake-child", "--qualification-only", "true", "--stage", stage,
                "--command-id", commandId, "--run-root", runRoot, "--behavior", behavior
            };
            if (markerPath is not null)
            {
                arguments.Add("--marker-path");
                arguments.Add(markerPath);
            }
            var causesRdsRead = stage is "RDS_READ_1" or "RDS_READ_2";
            var causesCapture = stage == "MARKET_CAPTURE";
            var cleanupType = CleanupType(stage);
            var canonical = string.Join('\n', commandId, stage, executablePath, executableSha,
                string.Join('|', arguments), Path.GetDirectoryName(executablePath)!,
                Arch7bOneShotContracts.StageEvidenceVersion, 10, cleanupType, causesRdsRead,
                causesCapture, false, markerPath ?? string.Empty);
            commands.Add(new(commandId, stage, executablePath, executableSha, arguments,
                Path.GetDirectoryName(executablePath)!, environment, Arch7bOneShotContracts.StageEvidenceVersion,
                10, cleanupType, causesRdsRead, causesCapture, false, markerPath,
                Arch7bOneShotContracts.Sha256(canonical)));
        }
        var commandSet = Arch7bOneShotContracts.Sha256(string.Join('\n', commands.Select(value => value.EvidenceSha256)));
        var freezeManifest = Arch7bOneShotContracts.Sha256("synthetic-freeze-manifest-v1");
        var freezePacket = Arch7bOneShotContracts.Sha256("synthetic-freeze-packet-v1");
        var intradayCommit = Arch7bOneShotContracts.Sha256("synthetic-intraday-commit")[..40];
        var intradayTree = Arch7bOneShotContracts.Sha256("synthetic-intraday-tree")[..40];
        var planCanonical = string.Join('\n', Arch7bOneShotContracts.LivePlanVersion,
            Arch7bOneShotContracts.CoreCommit, Arch7bOneShotContracts.CoreTree,
            intradayCommit, intradayTree, freezeManifest, freezePacket,
            commandSet, "synthetic-operator-authorization", runId, runRoot, true, true);
        var plan = new Arch7bOneShotLivePlan(Arch7bOneShotContracts.LivePlanVersion,
            Arch7bOneShotContracts.CoreCommit, Arch7bOneShotContracts.CoreTree, intradayCommit, intradayTree,
            freezeManifest, freezePacket, commandSet, "synthetic-operator-authorization", runId, runRoot,
            true, true, commands, Arch7bOneShotContracts.Sha256(planCanonical));
        var now = DateTimeOffset.UtcNow;
        var authority = new Arch7bOneShotLiveExecutionAuthority(
            Arch7bOneShotContracts.LiveExecutionAuthorityVersion, intradayCommit, intradayTree,
            Arch7bOneShotContracts.CoreCommit, Arch7bOneShotContracts.CoreTree, intradayCommit, intradayTree,
            freezeManifest, freezePacket, Arch7bOneShotContracts.Sha256("synthetic-runtime"),
            Arch7bOneShotContracts.CoreRepositoryAuthoritySha256,
            Arch7bOneShotContracts.CoreTrackedInventorySha256,
            Arch7bOneShotContracts.Sha256("synthetic-static-authorities"), commandSet,
            plan.OperatorAuthorizationId, "TEST", "1754288005", true, 1, 2, 1, 0,
            now.AddMinutes(-1), now.AddHours(1), string.Empty);
        authority = authority with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(authority.Canonical()) };
        return (plan, authority);
    }

    private static string CleanupType(string stage) => stage switch
    {
        "CORE_PREQUALIFICATION" => "core-prequalification-process-env",
        "PORTAL_SESSION_PROVEN" => "portal-browser-context",
        "RDS_READ_1" or "RDS_READ_2" => "secret-clients",
        "ARM_IMPORT" => "arm-import-child",
        "PRELOADED_LEASE_READY" => "preloaded-lease-process",
        "BRACKET_T0" or "BRACKET_P1" or "BRACKET_T1" or "BRACKET_P2" or "BRACKET_T2" => "bracket-downloader-process",
        "CORE_FAST_SEAL" => "fast-seal-process",
        "HANDOFF_V3" => "handoff-child",
        "POSITION_APPLY" => "position-importer-process",
        "MARKET_PREARM" or "MARKET_CAPTURE" => "market-data-recorder",
        "PMS_IMPORT" => "pms-importer",
        "ARCH7A_QUALIFY_SHADOW" => "arch7a-child",
        "REPORTING" => "reporting-process",
        _ => "transient-output-roots"
    };
}

public sealed record Arch7bLiveProcessQualification(
    int IndependentRunCount,
    int IndependentPassCount,
    int SequentialCampaignCount,
    int SequentialCampaignPassCount,
    int RunsPerCampaign,
    int FailureInjectionCount,
    int FailureInjectionPassCount,
    int ResidualProcessCount,
    int ResidualMarkerCount,
    string EvidenceSha256);

public static class Arch7bLiveProcessQualifier
{
    public static async Task<Arch7bLiveProcessQualification> RunAsync(string executablePath,
        int independentRuns = 30, int campaigns = 10, int runsPerCampaign = 3,
        bool runFailureMatrix = true, CancellationToken cancellationToken = default)
    {
        var evidence = new List<string>();
        var independentPass = 0;
        for (var index = 0; index < independentRuns; index++)
        {
            var result = await RunOneAsync(executablePath, $"independent-{index:D3}", null, "success",
                cancellationToken).ConfigureAwait(false);
            if (result.Passed) independentPass++;
            evidence.Add(result.EvidenceSha256);
        }
        var campaignPass = 0;
        for (var campaign = 0; campaign < campaigns; campaign++)
        {
            var values = new List<Arch7bOneShotLiveExecutionEvidence>();
            for (var run = 0; run < runsPerCampaign; run++)
                values.Add(await RunOneAsync(executablePath, $"campaign-{campaign:D2}-{run:D2}", null,
                    "success", cancellationToken).ConfigureAwait(false));
            if (values.All(value => value.Passed) && values.Select(value => value.RunId).Distinct().Count() == values.Count)
                campaignPass++;
            evidence.AddRange(values.Select(value => value.EvidenceSha256));
        }
        var failurePass = 0;
        var failureCount = runFailureMatrix ? Arch7bStages.All.Count : 0;
        if (runFailureMatrix)
        {
            foreach (var stage in Arch7bStages.All)
            {
                var result = await RunOneAsync(executablePath, "failure-" + stage.ToLowerInvariant(), stage,
                    "blocker", cancellationToken).ConfigureAwait(false);
                var expected = "ARCH7B_" + stage + "_FAILED";
                if (!result.Passed && result.PrimaryFailure?.FirstBlockerCode == expected &&
                    result.PrimaryFailure.FailureStage == stage && result.Cleanup.Complete &&
                    result.Budget.Retries == 0) failurePass++;
                evidence.Add(result.EvidenceSha256);
            }
        }
        return new(independentRuns, independentPass, campaigns, campaignPass, runsPerCampaign,
            failureCount, failurePass, 0, 0,
            Arch7bOneShotContracts.Sha256(string.Join('\n', evidence)));
    }

    private static async Task<Arch7bOneShotLiveExecutionEvidence> RunOneAsync(string executablePath,
        string suffix, string? failureStage, string behavior, CancellationToken cancellationToken)
    {
        var rootParent = Path.Combine(Path.GetTempPath(), "qq-arch7b-live-runtime-qualification");
        Directory.CreateDirectory(rootParent);
        var runId = $"arch7b-live-{suffix}-{Guid.NewGuid():N}";
        var runRoot = Path.Combine(rootParent, runId);
        var fixture = Arch7bSyntheticLiveExecutionFactory.Create(executablePath, runRoot, runId,
            failureStage, behavior);
        var runtime = new Arch7bOneShotLiveExecutionRuntime(new Arch7bOneShotProcessCommandRunner());
        var result = await runtime.RunOneShotAsync(fixture.Plan, fixture.Authority,
            fixture.Authority.EvidenceSha256, fixture.Plan.OperatorAuthorizationId,
            DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (Directory.Exists(runRoot)) Directory.Delete(runRoot, true);
        return result;
    }
}
