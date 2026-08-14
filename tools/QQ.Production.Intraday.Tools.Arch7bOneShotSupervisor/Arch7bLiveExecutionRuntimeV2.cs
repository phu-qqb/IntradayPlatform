using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

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

public sealed partial class Arch7bOneShotLiveExecutionRuntimeV2
{
    private readonly Arch7bOneShotCommandMaterializer materializer;
    private readonly Arch7bOneShotProcessRunnerV2 runner;
    private readonly Arch7bRealCommandAdapterRegistry adapters;
    private readonly IArch7bCoreRdsSecretBrokerClient? brokerClient;
    private readonly IPmsShadowCaptureClockAuthorityProducer clockAuthorityProducer;
    private readonly IArch7bStageWindowWaiter stageWindowWaiter;

    public Arch7bOneShotLiveExecutionRuntimeV2(Arch7bOneShotCommandMaterializer materializer,
        Arch7bOneShotProcessRunnerV2 runner, Arch7bRealCommandAdapterRegistry adapters,
        IArch7bCoreRdsSecretBrokerClient? brokerClient = null,
        IPmsShadowCaptureClockAuthorityProducer? clockAuthorityProducer = null,
        IArch7bStageWindowWaiter? stageWindowWaiter = null)
    {
        this.materializer = materializer;
        this.runner = runner;
        this.adapters = adapters;
        this.brokerClient = brokerClient;
        this.clockAuthorityProducer = clockAuthorityProducer ??
            new PmsShadowCaptureClockAuthorityProducer();
        this.stageWindowWaiter = stageWindowWaiter ??
            new Arch7bMonotonicStageWindowWaiter();
    }

    public async Task<Arch7bV2ExecutionEvidence> RunAsync(
        Arch7bOneShotLivePlanTemplate template,
        Arch7bOneShotLiveExecutionAuthorityV3 authority,
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
        var pendingBrokerExecutions = new Arch7bPendingBrokerExecutionState();
        var currentStage = Arch7bStages.All[0];
        Arch7bOneShotPrimaryFailureEvidence? primary = null;
        Arch7bPreparedCorePrequalificationConfig? preparedCoreConfig = null;
        var finalBlocker = Arch7bOneShotContracts.ExpectedFinalBlocker;
        try
        {
            if (template.StageContracts.Any(value => value.ProducedFactTypes.Contains(
                    "core_prequalification_config", StringComparer.Ordinal)))
                preparedCoreConfig = Arch7bCorePrequalificationConfigParser.Prepare(
                    template, runRoot);
            foreach (var stageContract in template.StageContracts)
            {
                currentStage = stageContract.StageId;
                ValidateStageEntry(stageContract, completed);
                await WaitForStageWindowAsync(currentStage, selectedSlot, timeProvider,
                    cancellationToken).ConfigureAwait(false);
                var startedAt = timeProvider.GetUtcNow();
                string? commandSha = null;
                string? resultSha = null;
                var produced = new List<string>();
                var resultCode = "SUCCESS";
                var brokerHandled = brokerClient is not null && await TryExecuteBrokerStageAsync(
                    stageContract, template, facts, budget, runRoot, startedAt, produced,
                    pendingBrokerExecutions, value => commandSha = value,
                    value => resultSha = value,
                    value => resultCode = value, cancellationToken).ConfigureAwait(false);
                if (!brokerHandled)
                {
                    switch (stageContract.StageId)
                    {
                        case "STATIC_AUTHORITY_VALIDATION":
                            produced.Add(facts.Append("static_authority_validation", currentStage,
                                new { authority = authority.EvidenceSha256 }, authority.EvidenceSha256,
                                startedAt).FactSha256);
                            produced.Add(facts.Append("core_commit", currentStage,
                                new { value = template.CoreCommit },
                                Arch7bOneShotContracts.Sha256(template.CoreCommit), startedAt).FactSha256);
                            produced.Add(facts.Append("intraday_commit", currentStage,
                                new { value = template.IntradayCommit },
                                Arch7bOneShotContracts.Sha256(template.IntradayCommit), startedAt).FactSha256);
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
                            if (stageContract.ProducedFactTypes.Contains(
                                    "core_prequalification_config",
                                    StringComparer.Ordinal))
                            {
                                var prepared = preparedCoreConfig ??
                                    throw new Arch7bQualificationException(
                                        Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch,
                                        "pre-slot-config-missing");
                                var coreConfigPath = Path.Combine(runRoot,
                                    "core-prequalification-config.json");
                                await WriteCreateNewBytesAsync(coreConfigPath, prepared.Bytes,
                                    cancellationToken).ConfigureAwait(false);
                                var writtenBytes = await File.ReadAllBytesAsync(coreConfigPath,
                                    cancellationToken).ConfigureAwait(false);
                                var writtenSha = Convert.ToHexStringLower(
                                    SHA256.HashData(writtenBytes));
                                if (writtenSha != prepared.Sha256 ||
                                    !writtenBytes.AsSpan().SequenceEqual(prepared.Bytes))
                                    throw new Arch7bQualificationException(
                                        Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch,
                                        "post-lock-byte-mismatch");
                                _ = Arch7bCorePrequalificationConfigParser.ParseAndValidate(
                                    writtenBytes,
                                    Arch7bCorePrequalificationConfigParser.Context(
                                        template, runRoot));
                                produced.Add(facts.Append("core_prequalification_config",
                                    currentStage, new
                                    {
                                        path = coreConfigPath,
                                        sha256 = writtenSha,
                                        contract_version =
                                            Arch7bCorePrequalificationConfigV1.ContractVersion,
                                        pre_slot_evidence_sha256 = prepared.EvidenceSha256
                                    }, writtenSha, startedAt).FactSha256);
                            }
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
                            var draftPath = Arch7bOneShotRunArtifactPath.ReservePositionMarketDraft(
                                runRoot, runId);
                            var lineagePath = Arch7bOneShotRunArtifactPath.ReservePositionMarketLineage(
                                runRoot, runId);
                            var revisionPath =
                                Arch7bOneShotRunArtifactPath.ReservePositionMarketRevisionBinding(
                                    runRoot, runId);
                            produced.Add(facts.Append("position_market_draft_output_path", currentStage,
                                draftPath, draftPath.EvidenceSha256, startedAt).FactSha256);
                            produced.Add(facts.Append("position_market_lineage_output_path", currentStage,
                                lineagePath, lineagePath.EvidenceSha256, startedAt).FactSha256);
                            produced.Add(facts.Append("position_market_revision_binding_output_path",
                                currentStage, revisionPath, revisionPath.EvidenceSha256,
                                startedAt).FactSha256);
                            break;
                        case "CLOCK_PREFLIGHT":
                        case "CLOCK_POST_CLOSE":
                            await ExecuteClockAuthorityStageAsync(stageContract, template, facts,
                                runRoot, selectedSlot, timeProvider, produced, cancellationToken)
                                .ConfigureAwait(false);
                            break;
                        case "CLOCK_CAPTURE_START":
                            await ExecuteClockAuthorityStageAsync(stageContract, template, facts,
                                runRoot, selectedSlot, timeProvider, produced, cancellationToken)
                                .ConfigureAwait(false);
                            var nonClockStage = stageContract with
                            {
                                ProducedFactTypes = stageContract.ProducedFactTypes.Where(value =>
                                    value != Arch7bClockFactContracts.CaptureStartFactType).ToArray()
                            };
                            await ExecuteStageAsync(nonClockStage, template, facts, cleanup,
                                longLived, secretLease, budget, runRoot, preparedCoreConfig, bracketStarted, startedAt,
                                produced, value => commandSha = value, value => resultSha = value,
                                value => resultCode = value, cancellationToken).ConfigureAwait(false);
                            break;
                        case "POSITION_MARKET_DRAFT" when stageContract.ProducedFactTypes.Contains(
                        "position_market_draft_artifact", StringComparer.Ordinal):
                            if (brokerClient is not null)
                                commandSha = await StartPmsImportPrearmAsync(template, facts,
                                    pendingBrokerExecutions, runRoot, startedAt,
                                    cancellationToken).ConfigureAwait(false);
                            ExecutePositionMarketDraftStage(facts, runRoot, startedAt, produced);
                            break;
                        case "POSITION_MARKET_LINEAGE" when stageContract.ProducedFactTypes.Contains(
                        "position_market_lineage_artifact", StringComparer.Ordinal):
                            ExecutePositionMarketLineageStage(facts, runRoot, startedAt, produced);
                            break;
                        case "ECONOMIC_REVISION" when stageContract.ProducedFactTypes.Contains(
                        "economic_revision_artifact", StringComparer.Ordinal):
                            ExecuteEconomicRevisionStage(facts, runRoot, startedAt, produced);
                            break;
                        case "REVISION_BINDING" when stageContract.ProducedFactTypes.Contains(
                        "position_market_revision_binding_artifact", StringComparer.Ordinal):
                            ExecuteRevisionBindingStage(facts, runRoot, startedAt, produced);
                            break;
                        default:
                            await ExecuteStageAsync(stageContract, template, facts, cleanup, longLived,
                                secretLease, budget, runRoot, preparedCoreConfig, bracketStarted, startedAt, produced,
                                value => commandSha = value, value => resultSha = value,
                                value => resultCode = value, cancellationToken).ConfigureAwait(false);
                            break;
                    }
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
        var brokerCleanupComplete = true;
        pendingBrokerExecutions.CancelPending();
        if (brokerClient is not null)
        {
            try
            {
                if (brokerClient.IsRunning)
                    _ = await brokerClient.ShutdownAsync(CancellationToken.None).ConfigureAwait(false);
                await brokerClient.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupFailure)
            {
                brokerCleanupComplete = false;
                if (primary is null)
                {
                    primary = new(Arch7bCoreRdsSecretBrokerBlockers.CleanupIncomplete,
                        currentStage, currentStage, cleanupFailure.GetType().Name,
                        Arch7bOneShotContracts.Sha256(cleanupFailure.Message),
                        stageEvidence.LastOrDefault()?.EvidenceSha256);
                    finalBlocker = primary.FirstBlockerCode;
                }
            }
        }
        var cleanupReport = await cleanup.CleanupAllAsync(finalBlocker, cancellationToken).ConfigureAwait(false);
        var residualProcesses = longLived.ResidualCount;
        var residualMarkers = Directory.EnumerateFiles(runRoot, "*.ready", SearchOption.AllDirectories).Count();
        var finalBudget = Snapshot(budget);
        var passed = primary is null && brokerCleanupComplete && cleanupReport.Complete && residualProcesses == 0 &&
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

    internal static async Task ValidateCorePrequalificationPreSpawnAsync(
        Arch7bOneShotMaterializedCommand command,
        Arch7bOneShotLivePlanTemplate template,
        string runRoot,
        Arch7bPreparedCorePrequalificationConfig? prepared,
        CancellationToken cancellationToken)
    {
        var configPath = Arch7bNativeAdapterJson.Option(command.ArgumentList, "--config");
        var failurePath = Path.Combine(Path.GetDirectoryName(command.AuthorityPath)!,
            "core-prequalification-pre-spawn-config-failure.json");
        try
        {
            if (prepared is null)
                throw new Arch7bQualificationException(
                    Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch,
                    "pre-slot-config-missing");
            var bytes = await File.ReadAllBytesAsync(configPath, cancellationToken)
                .ConfigureAwait(false);
            var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (sha != prepared.Sha256 || !bytes.AsSpan().SequenceEqual(prepared.Bytes))
                throw new Arch7bQualificationException(
                    Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch,
                    "pre-spawn-byte-mismatch");
            _ = Arch7bCorePrequalificationConfigParser.ParseAndValidate(bytes,
                Arch7bCorePrequalificationConfigParser.Context(template, runRoot));
            var modulePath = Path.Combine(command.WorkingDirectory,
                Arch7bChildEntrypointValidator.CorePrequalificationRelativeModulePath
                    .Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(modulePath))
                throw new Arch7bQualificationException(
                    Arch7bBlockers.LiveCommandAuthorityIncomplete,
                    "core-prequalification-module");
        }
        catch (Arch7bQualificationException failure)
        {
            var configSha = File.Exists(configPath)
                ? Convert.ToHexStringLower(SHA256.HashData(
                    await File.ReadAllBytesAsync(configPath, CancellationToken.None)
                        .ConfigureAwait(false)))
                : string.Empty;
            var provisional = new Arch7bCorePrequalificationPreSpawnFailureEvidence(
                "arch7b_core_prequalification_pre_spawn_config_failure_v1",
                failure.BlockerCode, "CORE_PREQUALIFICATION", configPath,
                configSha, prepared?.Sha256 ?? string.Empty, false, false, string.Empty);
            var evidence = provisional with
            {
                EvidenceSha256 = Arch7bOneShotContracts.Sha256(string.Join('\n',
                    provisional.ContractVersion, provisional.BlockerCode,
                    provisional.StageId, provisional.ConfigPath, provisional.ConfigSha256,
                    provisional.PreSlotConfigSha256, provisional.ChildProcessStarted,
                    provisional.ChildReceiptPresent))
            };
            await WriteCreateNewAsync(failurePath, evidence, CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
    }
    internal static string CoreNodeRepositoryRoot(Arch7bOneShotLivePlanTemplate template)
    {
        var packageRoot = template.FileAuthorities.TryGetValue(
            "core_node_runtime", out var authority)
            ? Path.GetFullPath(authority.Path).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : throw new Arch7bQualificationException(
                Arch7bV2Blockers.AuthorityBindingMismatch, "core_node_runtime");
        var package = new DirectoryInfo(packageRoot);
        if (!string.Equals(package.Name, "lmax_portal_reports_downloader",
                StringComparison.Ordinal) ||
            package.Parent is null ||
            !string.Equals(package.Parent.Name, "tools", StringComparison.Ordinal) ||
            package.Parent.Parent is null)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.AuthorityBindingMismatch,
                "core_node_runtime_repository_root");
        return package.Parent.Parent.FullName;
    }

    private Task WaitForStageWindowAsync(string stageId, Arch7bSlotLock? selectedSlot,
        TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        if (stageId is not ("CLOCK_CAPTURE_START" or "MARKET_CAPTURE" or
            "CLOCK_POST_CLOSE"))
            return Task.CompletedTask;
        if (selectedSlot is null)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.RequiredFactMissing, "selected_slot");

        var targetUtc = stageId switch
        {
            "CLOCK_CAPTURE_START" => selectedSlot.SlotStartUtc.AddSeconds(
                -Arch7bOperationalSlotSelector.CaptureClockLeadSeconds),
            "MARKET_CAPTURE" => selectedSlot.SlotStartUtc,
            _ => selectedSlot.SlotEndUtc
        };
        return stageWindowWaiter.WaitUntilAsync(stageId, targetUtc, timeProvider,
            stageId != "CLOCK_POST_CLOSE", cancellationToken);
    }

    private async Task ExecuteStageAsync(Arch7bOneShotStageContract stage,
        Arch7bOneShotLivePlanTemplate template, Arch7bOneShotLiveFactStore facts,
        Arch7bTerminalCleanupSupervisor cleanup, Arch7bOneShotLongLivedProcessRegistry longLived,
        IArch7bOneShotSecretLease secretLease, Arch7bOneShotBudget budget, string runRoot,
        Arch7bPreparedCorePrequalificationConfig? preparedCoreConfig,
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
            foreach (var factType in stage.ProducedFactTypes)
                produced.Add(facts.Append(factType, stage.StageId, new
                {
                    result = Arch7bOneShotContracts.ExpectedFinalBlocker,
                    broker_send_allowed = false,
                    order_entry_logons = 0,
                    orders = 0,
                    fills = 0,
                    ledger_events = 0,
                    no_order = true
                }, Arch7bOneShotContracts.Sha256(stage.StageId + ":" + factType + ":" +
                    Arch7bOneShotContracts.ExpectedFinalBlocker), observedUtc).FactSha256);
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
        if (stage.StageId == "CORE_PREQUALIFICATION" && preparedCoreConfig is not null)
            await ValidateCorePrequalificationPreSpawnAsync(command, template, runRoot,
                preparedCoreConfig, cancellationToken).ConfigureAwait(false);
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
            var processKey = stage.StageId == "MARKET_FINALIZATION"
                ? "market-recorder"
                : command.LongLivedProcessKey ?? throw new Arch7bQualificationException(
                    Arch7bV2Blockers.LongLivedProcessStateInvalid, command.CommandId);
            longLived.Signal(processKey, "COMPLETE");
            var producerResult = await runner.StopLongLivedAsync(processKey, command,
                runRoot, longLived,
                cancellationToken).ConfigureAwait(false);
            if (stage.StageId == "MARKET_FINALIZATION")
            {
                var execution = await runner.InvokeAsync(command, runRoot, cleanup,
                    secretLease, bracketStarted, cancellationToken).ConfigureAwait(false);
                normalized = execution.NormalizedResult;
            }
            else normalized = producerResult;
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

    internal static void ExecutePositionMarketDraftStage(Arch7bOneShotLiveFactStore facts,
        string runRoot, DateTimeOffset observedUtc, ICollection<string> produced)
    {
        var pathFact = facts.Require("position_market_draft_output_path",
            "ONE_SHOT_IDENTITIES_CREATED", observedUtc, int.MaxValue);
        var planned = JsonSerializer.Deserialize<Arch7bOneShotRunArtifactPath>(
            pathFact.ValueJson, Arch7bJson.CanonicalOptions)
            ?? throw new Arch7bQualificationException(Arch7bV2Blockers.FactInvalid,
                "position_market_draft_output_path");
        var runId = FactValue(facts.Require("run_identity", "ONE_SHOT_IDENTITIES_CREATED",
            observedUtc, int.MaxValue));
        var marketCaptureSessionId = FactValue(facts.Require("market_capture_session_identity",
            "ONE_SHOT_IDENTITIES_CREATED", observedUtc, int.MaxValue));
        planned.Validate(runRoot, runId);
        if (!File.Exists(planned.Path) ||
            !string.Equals(Path.GetFileName(planned.Path),
                Arch7bOneShotRunArtifactPath.PositionMarketDraftFilename,
                StringComparison.Ordinal))
            throw new Arch7bQualificationException(Arch7bV2Blockers.RequiredFactMissing,
                "position_market_draft_artifact");

        var fileSha256 = ShaFile(planned.Path);
        var draft = Arch7bPositionMarketLineageFileStore.ReadDraft(planned.Path, fileSha256);
        var selectedPositionSnapshotId = RequireRuntimeSelectionSnapshotId(
            facts, runRoot, observedUtc);
        if (!string.Equals(draft.RunId, runId, StringComparison.Ordinal) ||
            !string.Equals(draft.MarketCaptureSessionId, marketCaptureSessionId,
                StringComparison.Ordinal) ||
            draft.SelectedPositionSnapshotId != selectedPositionSnapshotId)
            throw new Arch7bQualificationException(Arch7bV2Blockers.AuthorityBindingMismatch,
                "position_market_draft_artifact");

        var evidenceSha256 = Arch7bOneShotContracts.Sha256(string.Join('\n', planned.Path,
            fileSha256, draft.EvidenceSha256, draft.SelectedPositionSnapshotId,
            draft.MarketCaptureSessionId));
        var artifact = new Arch7bPositionMarketDraftArtifactFact(planned.Path, fileSha256,
            evidenceSha256, draft.SelectedPositionSnapshotId, draft.MarketCaptureSessionId);
        produced.Add(facts.Append("position_market_draft_artifact", "POSITION_MARKET_DRAFT",
            artifact, evidenceSha256, observedUtc).FactSha256);
    }

    private static Guid RequireRuntimeSelectionSnapshotId(
        Arch7bOneShotLiveFactStore facts, string runRoot, DateTimeOffset observedUtc)
    {
        var fact = facts.Require("runtime_selection_artifact", "RUNTIME_SELECTION",
            observedUtc, int.MaxValue);
        using var factDocument = JsonDocument.Parse(fact.ValueJson);
        if (!factDocument.RootElement.TryGetProperty("artifact_paths", out var paths) ||
            paths.ValueKind != JsonValueKind.Array || paths.GetArrayLength() != 1)
            throw new Arch7bQualificationException(Arch7bV2Blockers.FactInvalid,
                "runtime_selection_artifact");
        var path = paths[0].GetString();
        if (string.IsNullOrWhiteSpace(path))
            throw new Arch7bQualificationException(Arch7bV2Blockers.FactInvalid,
                "runtime_selection_artifact");
        Arch7bOneShotAuthorityLoader.RequireAbsolute(path);
        Arch7bOneShotAuthorityLoader.RequireInside(runRoot, path);
        if (!File.Exists(path))
            throw new Arch7bQualificationException(Arch7bV2Blockers.RequiredFactMissing,
                "runtime_selection_artifact");
        using var artifact = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = artifact.RootElement;
        if (!root.TryGetProperty("contract", out var contract) ||
            contract.GetString() != "arch7b_position_snapshot_runtime_selection_v1" ||
            !root.TryGetProperty("selected_position_snapshot_id", out var snapshot) ||
            !Guid.TryParseExact(snapshot.GetString(), "D", out var snapshotId) ||
            snapshotId == Guid.Empty)
            throw new Arch7bQualificationException(Arch7bV2Blockers.FactInvalid,
                "runtime_selection_artifact");
        return snapshotId;
    }

    private static void ExecutePositionMarketLineageStage(
        Arch7bOneShotLiveFactStore facts, string runRoot, DateTimeOffset observedUtc,
        ICollection<string> produced)
    {
        var runId = FactValue(facts.Require("run_identity", "ONE_SHOT_IDENTITIES_CREATED",
            observedUtc, int.MaxValue));
        var planned = PlannedPath(facts, "position_market_lineage_output_path",
            runRoot, runId, observedUtc);
        planned.ValidatePositionMarketLineage(runRoot, runId);
        RequireArtifactFile(planned.Path, "position_market_lineage_artifact");
        var fileSha256 = ShaFile(planned.Path);
        var lineage = Arch7bPositionMarketLineageFileStore.ReadLineage(
            planned.Path, fileSha256);
        var draftFact = facts.Require("position_market_draft_artifact",
            "POSITION_MARKET_DRAFT", observedUtc, int.MaxValue);
        var draft = JsonSerializer.Deserialize<Arch7bPositionMarketDraftArtifactFact>(
            draftFact.ValueJson, Arch7bJson.CanonicalOptions)
            ?? throw new Arch7bQualificationException(Arch7bV2Blockers.FactInvalid,
                draftFact.FactType);
        if (lineage.RunId != runId ||
            lineage.SelectedPositionSnapshotId != draft.SelectedPositionSnapshotId ||
            lineage.MarketCaptureSessionId != draft.MarketCaptureSessionId)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.AuthorityBindingMismatch,
                "position_market_lineage_artifact");
        var artifact = new Arch7bContentAddressedArtifactFact(
            planned.Path, fileSha256, lineage.EvidenceSha256);
        produced.Add(facts.Append("position_market_lineage_artifact",
            "POSITION_MARKET_LINEAGE", artifact, lineage.EvidenceSha256,
            observedUtc).FactSha256);
    }

    private static void ExecuteEconomicRevisionStage(
        Arch7bOneShotLiveFactStore facts, string runRoot, DateTimeOffset observedUtc,
        ICollection<string> produced)
    {
        var runId = FactValue(facts.Require("run_identity", "ONE_SHOT_IDENTITIES_CREATED",
            observedUtc, int.MaxValue));
        var planned = PlannedPath(facts,
            "position_market_revision_binding_output_path", runRoot, runId, observedUtc);
        planned.ValidatePositionMarketRevisionBinding(runRoot, runId);
        RequireArtifactFile(planned.Path, "economic_revision_artifact");
        var fileSha256 = ShaFile(planned.Path);
        var binding = Arch7bPositionMarketLineageFileStore.ReadRevisionBinding(
            planned.Path, fileSha256);
        produced.Add(facts.Append("economic_revision_artifact", "ECONOMIC_REVISION",
            new
            {
                economic_revision_id = binding.ProjectionRevisionId.ToString("D"),
                path = planned.Path,
                sha256 = fileSha256,
                evidence_sha256 = binding.EvidenceSha256
            }, binding.EvidenceSha256, observedUtc).FactSha256);
    }

    private static void ExecuteRevisionBindingStage(
        Arch7bOneShotLiveFactStore facts, string runRoot, DateTimeOffset observedUtc,
        ICollection<string> produced)
    {
        var runId = FactValue(facts.Require("run_identity", "ONE_SHOT_IDENTITIES_CREATED",
            observedUtc, int.MaxValue));
        var planned = PlannedPath(facts,
            "position_market_revision_binding_output_path", runRoot, runId, observedUtc);
        planned.ValidatePositionMarketRevisionBinding(runRoot, runId);
        RequireArtifactFile(planned.Path, "position_market_revision_binding_artifact");
        var fileSha256 = ShaFile(planned.Path);
        var binding = Arch7bPositionMarketLineageFileStore.ReadRevisionBinding(
            planned.Path, fileSha256);
        var lineage = JsonSerializer.Deserialize<Arch7bContentAddressedArtifactFact>(
            facts.Require("position_market_lineage_artifact", "POSITION_MARKET_LINEAGE",
                observedUtc, int.MaxValue).ValueJson, Arch7bJson.CanonicalOptions)
            ?? throw new Arch7bQualificationException(Arch7bV2Blockers.FactInvalid,
                "position_market_lineage_artifact");
        if (binding.PositionMarketLineageEvidenceSha256 != lineage.EvidenceSha256)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.AuthorityBindingMismatch,
                "position_market_revision_binding_artifact");
        var artifact = new Arch7bContentAddressedArtifactFact(
            planned.Path, fileSha256, binding.EvidenceSha256);
        produced.Add(facts.Append("position_market_revision_binding_artifact",
            "REVISION_BINDING", artifact, binding.EvidenceSha256,
            observedUtc).FactSha256);
    }

    private static Arch7bOneShotRunArtifactPath PlannedPath(
        Arch7bOneShotLiveFactStore facts, string factType, string runRoot,
        string runId, DateTimeOffset observedUtc)
    {
        var fact = facts.Require(factType, "ONE_SHOT_IDENTITIES_CREATED",
            observedUtc, int.MaxValue);
        return JsonSerializer.Deserialize<Arch7bOneShotRunArtifactPath>(
                   fact.ValueJson, Arch7bJson.CanonicalOptions)
               ?? throw new Arch7bQualificationException(
                   Arch7bV2Blockers.FactInvalid, factType);
    }

    private static void RequireArtifactFile(string path, string factType)
    {
        if (!File.Exists(path))
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.RequiredFactMissing, factType);
    }

    private static string FactValue(Arch7bOneShotFact fact)
    {
        using var document = JsonDocument.Parse(fact.ValueJson);
        return document.RootElement.GetProperty("value").GetString()
            ?? throw new Arch7bQualificationException(Arch7bV2Blockers.FactInvalid,
                fact.FactType);
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

    private static async Task WriteCreateNewBytesAsync(string path, ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
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
