using System.Security.Cryptography;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bV2StageEvidence(
    string ContractVersion,
    string StageId,
    Arch7bExecutionKind ExecutionKind,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string? MaterializedCommandSha256,
    string? NormalizedChildResultSha256,
    IReadOnlyList<string> ProducedFactSha256,
    string SloStatus,
    Arch7bOneShotBudgetSnapshot Budget,
    string ResultCode,
    string EvidenceSha256);

public sealed record Arch7bV2ExecutionEvidence(
    string ContractVersion,
    string RunId,
    string SlotId,
    IReadOnlyList<Arch7bV2StageEvidence> Stages,
    Arch7bOneShotBudgetSnapshot Budget,
    string FinalBlocker,
    Arch7bOneShotPrimaryFailureEvidence? PrimaryFailure,
    Arch7bCleanupReport Cleanup,
    IReadOnlyList<Arch7bLongLivedProcessEvidence> LongLivedProcesses,
    int ResidualProcessCount,
    int ResidualMarkerCount,
    bool Passed,
    Arch7bNoLiveSafetyCounters Safety,
    string EvidenceSha256);

public sealed class Arch7bOneShotLiveExecutionRuntimeV2
{
    private readonly Arch7bOneShotCommandMaterializer materializer;
    private readonly Arch7bOneShotProcessRunnerV2 runner;
    private readonly Arch7bRealCommandAdapterRegistry adapters;

    public Arch7bOneShotLiveExecutionRuntimeV2(Arch7bOneShotCommandMaterializer materializer,
        Arch7bOneShotProcessRunnerV2 runner, Arch7bRealCommandAdapterRegistry adapters)
    {
        this.materializer = materializer;
        this.runner = runner;
        this.adapters = adapters;
    }

    public async Task<Arch7bV2ExecutionEvidence> RunAsync(
        Arch7bOneShotLivePlanTemplate template,
        Arch7bOneShotLiveExecutionAuthorityV2 authority,
        Arch7bOneShotOperatorAuthorizationV2 operatorAuthorization,
        string templateFileSha256,
        string runRoot,
        TimeProvider timeProvider,
        IArch7bOneShotSecretLease? secretLease = null,
        CancellationToken cancellationToken = default)
    {
        Arch7bLiveTemplateValidator.Validate(template, adapters);
        var nowUtc = timeProvider.GetUtcNow();
        authority.Validate(template, operatorAuthorization, templateFileSha256, nowUtc);
        runRoot = Path.GetFullPath(runRoot);
        if (Directory.Exists(runRoot))
            throw new Arch7bQualificationException(Directory.EnumerateFileSystemEntries(runRoot).Any()
                ? Arch7bBlockers.RunRootNotEmpty : Arch7bBlockers.RunRootReused);
        Directory.CreateDirectory(runRoot);
        secretLease ??= new Arch7bCoreOwnedSecretLease();
        var facts = new Arch7bOneShotLiveFactStore(runRoot);
        var cleanup = new Arch7bTerminalCleanupSupervisor(runRoot);
        var longLived = new Arch7bOneShotLongLivedProcessRegistry();
        var budget = new Arch7bOneShotBudget();
        var completed = new HashSet<string>(StringComparer.Ordinal);
        var stageEvidence = new List<Arch7bV2StageEvidence>();
        var chronology = Arch7bCrossRepositoryChronology.Validate(
            Arch7bCrossRepositoryChronology.CreateDefault(), Arch7bGlobalSloRegistry.CreateDefault());
        if (!chronology.IsValid || chronology.EvidenceSha256 != template.ChronologySha256)
            throw new Arch7bQualificationException(Arch7bV2Blockers.AuthorityBindingMismatch, "chronology");
        var selector = new Arch7bOperationalSlotSelector();
        Arch7bSlotLock? selectedSlot = null;
        string? runId = null;
        var bracketStarted = false;
        var currentStage = Arch7bStages.All[0];
        Arch7bOneShotPrimaryFailureEvidence? primary = null;
        var finalBlocker = Arch7bOneShotContracts.ExpectedFinalBlocker;
        try
        {
            foreach (var stageContract in template.StageContracts)
            {
                currentStage = stageContract.StageId;
                ValidateStageEntry(stageContract, completed);
                var startedAt = timeProvider.GetUtcNow();
                string? commandSha = null;
                string? resultSha = null;
                var produced = new List<string>();
                var resultCode = "SUCCESS";
                switch (stageContract.StageId)
                {
                    case "STATIC_AUTHORITY_VALIDATION":
                        produced.Add(facts.Append("static_authority_validation", currentStage,
                            new { authority = authority.EvidenceSha256 }, authority.EvidenceSha256,
                            startedAt).FactSha256);
                        produced.Add(facts.Append("runtime_run_root", currentStage,
                            new { path = runRoot }, Arch7bOneShotContracts.Sha256(runRoot),
                            startedAt).FactSha256);
                        break;
                    case "CALENDAR_LOADED":
                        produced.Add(facts.Append("calendar", currentStage,
                            new { authority = template.CalendarAuthoritySha256, authoritative = true },
                            template.CalendarAuthoritySha256, startedAt).FactSha256);
                        break;
                    case "SLOT_SELECTED":
                        selectedSlot = selector.SelectAndLock(startedAt, chronology.PreSlotCriticalPathSloSeconds);
                        var selectedPath = Path.Combine(runRoot, "selected-slot.json");
                        await WriteCreateNewAsync(selectedPath, new
                        {
                            contract = Arch7bOneShotContracts.OperationalSlotSelectionPolicyVersion,
                            slot_id = selectedSlot.SlotId,
                            observed_utc = selectedSlot.ObservedUtc,
                            slot_start_utc = selectedSlot.SlotStartUtc,
                            slot_end_utc = selectedSlot.SlotEndUtc,
                            required_margin_seconds = selectedSlot.RequiredPreparationMarginSeconds
                        }, cancellationToken).ConfigureAwait(false);
                        produced.Add(facts.Append("selected_slot", currentStage, new
                        {
                            slot_id = selectedSlot.SlotId,
                            slot_start_utc = selectedSlot.SlotStartUtc.ToString("O"),
                            slot_end_utc = selectedSlot.SlotEndUtc.ToString("O"),
                            path = selectedPath
                        }, ShaFile(selectedPath), startedAt).FactSha256);
                        break;
                    case "SLOT_LOCKED":
                        if (selectedSlot is null)
                            throw new Arch7bQualificationException(
                                Arch7bV2Blockers.RequiredFactMissing, "selected_slot");
                        budget.RecordSlot();
                        var lockPath = Path.Combine(runRoot, "slot-lock.json");
                        await WriteCreateNewAsync(lockPath, selectedSlot, cancellationToken).ConfigureAwait(false);
                        produced.Add(facts.Append("slot_lock", currentStage, new
                        {
                            slot_id = selectedSlot.SlotId,
                            lock_sha256 = selectedSlot.LockSha256,
                            path = lockPath
                        }, ShaFile(lockPath), startedAt).FactSha256);
                        break;
                    case "ONE_SHOT_IDENTITIES_CREATED":
                        _ = facts.Require("slot_lock", "SLOT_LOCKED", startedAt, int.MaxValue);
                        runId = $"arch7b-{selectedSlot!.SlotStartUtc:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}";
                        foreach (var item in new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["run_identity"] = runId,
                            ["owner_identity"] = Guid.NewGuid().ToString("D"),
                            ["future_authorization_identity"] = Guid.NewGuid().ToString("D"),
                            ["source_session_identity"] = Guid.NewGuid().ToString("D"),
                            ["market_capture_session_identity"] = Guid.NewGuid().ToString("D")
                        })
                            produced.Add(facts.Append(item.Key, currentStage, new { value = item.Value },
                                Arch7bOneShotContracts.Sha256(item.Value), startedAt).FactSha256);
                        break;
                    default:
                        await ExecuteStageAsync(stageContract, template, facts, cleanup, longLived,
                            secretLease, budget, runRoot, bracketStarted, startedAt, produced,
                            value => commandSha = value, value => resultSha = value,
                            value => resultCode = value, cancellationToken).ConfigureAwait(false);
                        break;
                }
                if (currentStage == "BRACKET_T0") bracketStarted = true;
                if (currentStage == "FINAL_WORKING_ORDER_PREFLIGHT")
                {
                    if (resultCode != Arch7bOneShotContracts.ExpectedFinalBlocker)
                        throw new Arch7bQualificationException(Arch7bV2Blockers.ChildNativeStatusUnknown,
                            resultCode);
                    finalBlocker = resultCode;
                }
                var completedAt = timeProvider.GetUtcNow();
                var sloStatus = ValidateStageExit(stageContract, startedAt, completedAt);
                var snapshot = Snapshot(budget);
                var canonical = string.Join('\n', Arch7bV2Contracts.StageValidatorVersion, currentStage,
                    stageContract.ExecutionKind, startedAt.ToString("O"), completedAt.ToString("O"),
                    commandSha ?? string.Empty, resultSha ?? string.Empty, string.Join('|', produced),
                    sloStatus, snapshot.Slots, snapshot.RdsReads, snapshot.Captures, snapshot.Retries, resultCode);
                stageEvidence.Add(new(Arch7bV2Contracts.StageValidatorVersion, currentStage,
                    stageContract.ExecutionKind, startedAt, completedAt, commandSha, resultSha, produced,
                    sloStatus, snapshot, resultCode, Arch7bOneShotContracts.Sha256(canonical)));
                completed.Add(currentStage);
            }
        }
        catch (Arch7bQualificationException failure)
        {
            primary = new(failure.BlockerCode, currentStage, currentStage, failure.GetType().Name,
                Arch7bOneShotContracts.Sha256(failure.Message), stageEvidence.LastOrDefault()?.EvidenceSha256);
            finalBlocker = primary.FirstBlockerCode;
        }
        catch (Exception failure)
        {
            primary = new(Arch7bBlockers.ChildProcessFailedUncatalogued, currentStage, currentStage,
                failure.GetType().Name, Arch7bOneShotContracts.Sha256(failure.Message),
                stageEvidence.LastOrDefault()?.EvidenceSha256);
            finalBlocker = primary.FirstBlockerCode;
        }
        var cleanupReport = await cleanup.CleanupAllAsync(finalBlocker, cancellationToken).ConfigureAwait(false);
        var residualProcesses = longLived.ResidualCount;
        var residualMarkers = Directory.EnumerateFiles(runRoot, "*.ready", SearchOption.AllDirectories).Count();
        var finalBudget = Snapshot(budget);
        var passed = primary is null && cleanupReport.Complete && residualProcesses == 0 &&
            residualMarkers == 0 && stageEvidence.Count == Arch7bStages.All.Count &&
            finalBlocker == Arch7bOneShotContracts.ExpectedFinalBlocker && finalBudget is
            { Slots: 1, RdsReads: 2, Captures: 1, Retries: 0 };
        var evidenceCanonical = string.Join('\n', Arch7bV2Contracts.LiveExecutionRuntimeVersion,
            runId ?? string.Empty, selectedSlot?.SlotId ?? string.Empty,
            string.Join('|', stageEvidence.Select(value => value.EvidenceSha256)), finalBlocker,
            primary?.MessageSha256 ?? string.Empty, cleanupReport.EvidenceSha256,
            residualProcesses, residualMarkers, passed);
        return new(Arch7bV2Contracts.LiveExecutionRuntimeVersion, runId ?? string.Empty,
            selectedSlot?.SlotId ?? string.Empty, stageEvidence, finalBudget, finalBlocker, primary,
            cleanupReport, longLived.Evidence, residualProcesses, residualMarkers, passed,
            Arch7bNoLiveSafetyCounters.Zero, Arch7bOneShotContracts.Sha256(evidenceCanonical));
    }

    private async Task ExecuteStageAsync(Arch7bOneShotStageContract stage,
        Arch7bOneShotLivePlanTemplate template, Arch7bOneShotLiveFactStore facts,
        Arch7bTerminalCleanupSupervisor cleanup, Arch7bOneShotLongLivedProcessRegistry longLived,
        IArch7bOneShotSecretLease secretLease, Arch7bOneShotBudget budget, string runRoot,
        bool bracketStarted, DateTimeOffset observedUtc, ICollection<string> produced,
        Action<string> commandSha, Action<string> resultSha, Action<string> resultCode,
        CancellationToken cancellationToken)
    {
        foreach (var factType in stage.RequiredFactTypes)
        {
            var producer = template.StageContracts.Single(value =>
                value.ProducedFactTypes.Contains(factType, StringComparer.Ordinal)).StageId;
            _ = facts.Require(factType, producer, observedUtc, int.MaxValue);
        }
        if (stage.ExecutionKind is Arch7bExecutionKind.Internal or Arch7bExecutionKind.FilesystemGate)
        {
            foreach (var factType in stage.ProducedFactTypes)
                produced.Add(facts.Append(factType, stage.StageId, new { stage = stage.StageId },
                    Arch7bOneShotContracts.Sha256(stage.StageId + ":" + factType), observedUtc).FactSha256);
            return;
        }
        if (stage.ExecutionKind == Arch7bExecutionKind.ChildAwaitEvidence)
        {
            var processKey = stage.StageId.Contains("MARKET", StringComparison.Ordinal)
                ? "market-recorder" : "preloaded-rds-lease";
            longLived.AssertReadyAndAlive(processKey);
            foreach (var factType in stage.ProducedFactTypes)
                produced.Add(facts.Append(factType, stage.StageId, new { process_key = processKey },
                    Arch7bOneShotContracts.Sha256(processKey + ":ready"), observedUtc).FactSha256);
            return;
        }
        if (stage.ExecutionKind == Arch7bExecutionKind.ChildSignal)
        {
            var processKey = stage.StageId.Contains("MARKET", StringComparison.Ordinal)
                ? "market-recorder" : "preloaded-rds-lease";
            longLived.Signal(processKey, "COMPLETE");
            return;
        }
        if (stage.ExecutionKind == Arch7bExecutionKind.ExpectedBlockerGate)
        {
            resultCode(Arch7bOneShotContracts.ExpectedFinalBlocker);
            return;
        }
        var commandTemplate = template.CommandTemplates.Single(value => value.StageId == stage.StageId);
        if (commandTemplate.CausesRdsRead)
        {
            if (bracketStarted) throw new Arch7bQualificationException(Arch7bBlockers.SecretReadAfterBracket);
            budget.RecordRdsRead();
        }
        if (commandTemplate.CausesCapture) budget.RecordCapture();
        var command = await materializer.MaterializeAsync(commandTemplate, facts,
            template.FileAuthorities, runRoot, observedUtc, cancellationToken).ConfigureAwait(false);
        commandSha(command.EvidenceSha256);
        if (stage.ExecutionKind == Arch7bExecutionKind.ChildStartLongLived)
        {
            var ready = Arch7bOneShotContracts.Sha256(command.CommandId + ":ready");
            var value = runner.StartLongLived(command, runRoot, ready, ["COMPLETE"],
                commandTemplate.StageId == "MARKET_PREARM" ? "MARKET_FINALIZATION" : "HANDOFF_V3",
                cleanup, longLived, secretLease, bracketStarted, cancellationToken);
            foreach (var factType in stage.ProducedFactTypes)
                produced.Add(facts.Append(factType, stage.StageId,
                    new { process_key = value.ProcessKey, ready_evidence_sha256 = ready }, ready,
                    observedUtc).FactSha256);
            return;
        }
        Arch7bNormalizedChildResult normalized;
        if (stage.ExecutionKind == Arch7bExecutionKind.ChildStop)
        {
            var processKey = command.LongLivedProcessKey ?? throw new Arch7bQualificationException(
                Arch7bV2Blockers.LongLivedProcessStateInvalid, command.CommandId);
            longLived.Signal(processKey, "COMPLETE");
            normalized = await runner.StopLongLivedAsync(processKey, command, runRoot, longLived,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var execution = await runner.InvokeAsync(command, runRoot, cleanup, secretLease,
                bracketStarted, cancellationToken).ConfigureAwait(false);
            normalized = execution.NormalizedResult;
        }
        resultSha(normalized.EvidenceSha256);
        resultCode(normalized.ResultCode);
        foreach (var factType in stage.ProducedFactTypes)
            produced.Add(facts.Append(factType, stage.StageId, new
            {
                result = normalized.ResultCode,
                evidence_sha256 = normalized.EvidenceSha256,
                artifact_paths = normalized.ArtifactPaths
            }, normalized.EvidenceSha256, observedUtc).FactSha256);
    }

    private static void ValidateStageEntry(Arch7bOneShotStageContract stage,
        IReadOnlySet<string> completed)
    {
        if (!stage.Predecessors.All(completed.Contains))
            throw new Arch7bQualificationException(Arch7bV2Blockers.StagePredecessorMissing,
                stage.StageId);
        if (string.IsNullOrWhiteSpace(stage.SloId) || string.IsNullOrWhiteSpace(stage.ValidatorId))
            throw new Arch7bQualificationException(Arch7bV2Blockers.StageSloMissing, stage.StageId);
    }

    private static string ValidateStageExit(Arch7bOneShotStageContract stage,
        DateTimeOffset startedAt, DateTimeOffset completedAt)
    {
        if (completedAt < startedAt)
            throw new Arch7bQualificationException(Arch7bV2Blockers.StageSloMissing, stage.StageId);
        return "PASS";
    }

    private static async Task WriteCreateNewAsync(string path, object value,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await JsonSerializer.SerializeAsync(stream, value, Arch7bJson.CanonicalOptions,
            cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ShaFile(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));

    private static Arch7bOneShotBudgetSnapshot Snapshot(Arch7bOneShotBudget value) =>
        new(value.Slots, value.RdsReads, value.Captures, value.Retries);
}
