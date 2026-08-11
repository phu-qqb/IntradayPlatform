using System.Security.Cryptography;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public static class Arch7bFinalStageExecutionClasses
{
    public const string Internal = "A_INTERNAL";
    public const string FilesystemGate = "B_FILESYSTEM_GATE";
    public const string ExpectedBlockerGate = "C_EXPECTED_BLOCKER_GATE";
    public const string CoreChild = "D_CORE_CHILD";
    public const string IntradayChild = "E_INTRADAY_CHILD";
    public const string BrokerChild = "F_BROKER_CHILD";
    public const string LongLivedChildStart = "G_LONG_LIVED_CHILD_START";
    public const string LongLivedChildSignalOrStop = "H_LONG_LIVED_CHILD_SIGNAL_OR_STOP";
}

public sealed record Arch7bFinalStageExecutionEntry(
    string StageId,
    string ExecutionClass,
    Arch7bExecutionKind ExecutionKind,
    bool HasCommandTemplate,
    string? CommandId,
    string? Repository,
    string? ExecutableOrModule,
    string? Mode,
    string? NativeContract,
    string? AdapterId,
    string? OutputShape,
    IReadOnlyList<string> ArtifactSet,
    string QualificationRoute,
    string InternalLogic,
    string EvidenceSha256);

public sealed record Arch7bFinalStageExecutionClassification(
    string ContractVersion,
    int StageCount,
    int CommandTemplateCount,
    IReadOnlyDictionary<string, int> ClassCounts,
    IReadOnlyList<Arch7bFinalStageExecutionEntry> Stages,
    string EvidenceSha256);

public static class Arch7bFinalStageExecutionCatalog
{
    public const string ContractVersion = "arch7b_final_stage_execution_classification_v1";
    public const string FileName = "arch7b-final-stage-execution-classification-v1.json";

    private static readonly IReadOnlyList<Arch7bFinalStageExecutionEntry> Entries = Build();

    public static IReadOnlyList<Arch7bFinalStageExecutionEntry> All => Entries;
    public static int CommandTemplateCount => Entries.Count(value => value.HasCommandTemplate);

    public static Arch7bFinalStageExecutionEntry Require(string stageId) =>
        Entries.Single(value => value.StageId == stageId);

    public static Arch7bFinalStageExecutionClassification Document()
    {
        var classes = Entries.GroupBy(value => value.ExecutionClass, StringComparer.Ordinal)
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToDictionary(value => value.Key, value => value.Count(), StringComparer.Ordinal);
        var canonical = string.Join('\n', ContractVersion, Entries.Count, CommandTemplateCount,
            string.Join('|', classes.Select(value => $"{value.Key}:{value.Value}")),
            string.Join('|', Entries.Select(value => value.EvidenceSha256)));
        return new(ContractVersion, Entries.Count, CommandTemplateCount, classes, Entries,
            Arch7bOneShotContracts.Sha256(canonical));
    }

    public static async Task<string> WriteAsync(string outputPath,
        CancellationToken cancellationToken = default)
    {
        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await using (var stream = new FileStream(outputPath, FileMode.CreateNew,
                         FileAccess.Write, FileShare.None, 4096,
                         FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, Document(),
                Arch7bJson.CanonicalOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        return Convert.ToHexStringLower(SHA256.HashData(
            await File.ReadAllBytesAsync(outputPath, cancellationToken).ConfigureAwait(false)));
    }

    private static IReadOnlyList<Arch7bFinalStageExecutionEntry> Build()
    {
        var values = new[]
        {
            Internal("STATIC_AUTHORITY_VALIDATION", "Validate static authority and append immutable commit/run-root facts."),
            Internal("CALENDAR_LOADED", "Load the versioned calendar authority."),
            Internal("SLOT_SELECTED", "Select one operational slot and write its create-new authority."),
            Internal("SLOT_LOCKED", "Consume the one-slot budget and write the slot lock."),
            Child("CORE_PREQUALIFICATION", Arch7bFinalStageExecutionClasses.CoreChild,
                Arch7bExecutionKind.ChildInvoke, true, "core-runtime-prequalification",
                "phu-qqb/QQ.Production.Core", "src/fast-seal-cli.mjs",
                "prequalify-bracket-runtime", "lmax_portal_core_runtime_prequalification_v1",
                "core-prequalification-v1", "qualification + manifest",
                ["core-runtime-prequalification.json", "runner-tests.stderr.log", "runner-tests.stdout.log"],
                "Core prequalification output and manifest are validated byte-for-byte."),
            Internal("CLOCK_PREFLIGHT", "Produce and validate an independent preflight clock snapshot."),
            Child("PORTAL_SESSION_PROVEN", Arch7bFinalStageExecutionClasses.CoreChild,
                Arch7bExecutionKind.ChildInvoke, true, "prove-portal-session",
                "phu-qqb/QQ.Production.Core", "src/downloader.mjs", "prove-portal-session",
                "lmax_portal_demo_session_proof_v1", "portal-session-v1", "strict proof JSON",
                [], "Existing London Demo manual session is proven and the browser context is closed."),
            Internal("ONE_SHOT_IDENTITIES_CREATED", "Create run-scoped identities and reserve create-new artifact paths."),
            Child("RDS_READ_1", Arch7bFinalStageExecutionClasses.CoreChild,
                Arch7bExecutionKind.ChildInvoke, true, "arm-import-orchestrator",
                "phu-qqb/QQ.Production.Core", "src/arch7b-operational-orchestrator-cli.mjs",
                "qualify-arm-import-operational-orchestrator",
                "arch7b_operational_orchestrator_lifecycle_v1", "rds-arm-orchestrator-v1",
                "orchestrator lifecycle JSON", ["orchestrator-manifest.json", "orchestrator-evidence.json"],
                "Core qualifies the first bounded RDS read and arm lifecycle."),
            Internal("ARM_IMPORT", "Validate the arm transition produced by the first RDS read."),
            Child("RDS_READ_2", Arch7bFinalStageExecutionClasses.LongLivedChildStart,
                Arch7bExecutionKind.ChildStartLongLived, false, "core-rds-secret-broker",
                "phu-qqb/QQ.Production.Core", "src/rds-secret-child-command-broker-cli.mjs",
                "prepare-rds-secret-lease-and-serve-authorized-children",
                "arch7b_rds_secret_child_command_broker_v1", null, "broker authority",
                [], "Broker client materializes and starts the Core-owned process; no stage command template."),
            Child("PRELOADED_LEASE_READY", Arch7bFinalStageExecutionClasses.LongLivedChildSignalOrStop,
                Arch7bExecutionKind.ChildAwaitEvidence, false, "core-rds-secret-broker",
                "phu-qqb/QQ.Production.Core", "src/rds-secret-child-command-broker-cli.mjs",
                "ready", "arch7b_rds_secret_preloaded_lease_v1", null, "broker ready frame",
                [], "ReadReadyAsync validates the already-running broker lease; no child command."),
            Child("BRACKET_T0", Arch7bFinalStageExecutionClasses.LongLivedChildSignalOrStop,
                Arch7bExecutionKind.ChildSignal, false, "core-rds-secret-broker",
                "phu-qqb/QQ.Production.Core", "src/rds-secret-child-command-broker-cli.mjs",
                "mark-bracket-started", "arch7b_rds_secret_child_command_broker_v1", null,
                "broker transition frame", [], "MarkBracketStartedAsync signals the known broker process."),
            Gate("BRACKET_P1", "Validate create-new P1 bracket artifact."),
            Gate("BRACKET_T1", "Validate the non-overlapping bracket transition."),
            Gate("BRACKET_P2", "Validate create-new P2 bracket artifact."),
            Child("BRACKET_T2", Arch7bFinalStageExecutionClasses.CoreChild,
                Arch7bExecutionKind.ChildInvoke, true, "lmax-bracket-capture",
                "phu-qqb/QQ.Production.Core", "src/downloader.mjs", "capture-bracketed-snapshot",
                "lmax_portal_bracketed_current_position_snapshot_v2", "lmax-bracket-v1",
                "downloader manifest with bracketed snapshot", ["final-evidence-index.json", "contract.json", "manifest.json"],
                "Core downloader validates and emits the bracketed global-flat snapshot."),
            Gate("COMPLEMENTARY_REPORTS", "Validate the complementary report set already captured by Core."),
            Child("CORE_FAST_SEAL", Arch7bFinalStageExecutionClasses.CoreChild,
                Arch7bExecutionKind.ChildInvoke, true, "core-fast-seal",
                "phu-qqb/QQ.Production.Core", "src/fast-seal-cli.mjs",
                "run-bracket-fast-seal-and-hand-off", "lmax_portal_bracket_fast_seal_v1",
                "core-fast-seal-v1", "fast-seal summary and index", ["final-evidence-index.json"],
                "Validate the native fast-seal summary before normalization."),
            Child("HANDOFF_V3", Arch7bFinalStageExecutionClasses.LongLivedChildSignalOrStop,
                Arch7bExecutionKind.ChildSignal, false, "core-rds-secret-broker",
                "phu-qqb/QQ.Production.Core", "src/rds-secret-child-command-broker-cli.mjs",
                "post-bracket-handoff", "lmax_portal_core_to_intraday_fast_handoff_v3", null,
                "broker handoff frame", [], "Validate the broker-owned post-bracket handoff; no child command."),
            Gate("POSITION_PACKAGE", "Validate the content-addressed position package."),
            Gate("POSITION_READY", "Validate the importer ready marker."),
            Gate("POSITION_PLAN", "Validate the append-only +1/+99 import plan."),
            Child("POSITION_APPLY", Arch7bFinalStageExecutionClasses.BrokerChild,
                Arch7bExecutionKind.ChildInvoke, true, "position-apply",
                "phu-qqb/IntradayPlatform", "QQ.Production.Intraday.Tools.Arch7bPositionSnapshotImport",
                "run-fresh-position-import-fast-path", "arch7b_fresh_position_import_fast_path_v1",
                "position-import-v1", "broker child response", ["position-import-result.json", "append-only-timeline.json"],
                "Core broker executes the bounded append-only import command."),
            Child("RUNTIME_SELECTION", Arch7bFinalStageExecutionClasses.IntradayChild,
                Arch7bExecutionKind.ChildInvoke, true, "runtime-selection",
                "phu-qqb/IntradayPlatform", "QQ.Production.Intraday.Tools.Arch7bPositionSnapshotImport",
                "qualify-runtime-selection", "arch7b_position_snapshot_runtime_selection_v1",
                "runtime-selection-v1", "strict result JSON", ["runtime-selection.json"],
                "Select only the current-run package snapshot and verify 99/99 lineage."),
            Internal("POSITION_MARKET_DRAFT", "Read and validate the create-new position-market draft."),
            Internal("MARKET_PREARM",
                "Validate the bounded market-data-only inputs without starting the recorder."),
            Child("CLOCK_CAPTURE_START", Arch7bFinalStageExecutionClasses.IntradayChild,
                Arch7bExecutionKind.ChildInvoke, true, "capture-starter",
                "phu-qqb/IntradayPlatform", "QQ.Production.Intraday.Tools.Arch6fEconomicReplay",
                "assert-prearmed", "arch7b_prearmed_fresh_slot_handoff_cli_v1",
                "prearmed-handoff-v1", "prearm assertion", ["clock_authority_capture.json"],
                "Produce the capture clock snapshot and assert the prearmed handoff."),
            Child("MARKET_CAPTURE", Arch7bFinalStageExecutionClasses.IntradayChild,
                Arch7bExecutionKind.ChildInvoke, true, "market-data-recorder",
                "phu-qqb/IntradayPlatform", "QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly",
                "live-market-data-only", "arch6f_lmax_market_data_slot_capture_v1",
                "market-recorder-v1", "synchronous recorder result",
                ["market-ready.json", "market-capture.json"],
                "Run the single bounded market-data-only recorder synchronously at slot start."),
            Internal("CLOCK_POST_CLOSE", "Produce and validate the independent post-close clock snapshot."),
            Child("MARKET_FINALIZATION", Arch7bFinalStageExecutionClasses.IntradayChild,
                Arch7bExecutionKind.ChildInvoke, true, "canonical-slot-finalizer",
                "phu-qqb/IntradayPlatform", "QQ.Production.Intraday.Tools.Arch6fEconomicReplay",
                "publish-ready", "arch7b_prearmed_fresh_slot_handoff_cli_v1",
                "prearmed-handoff-v1", "canonical finalization output", ["position-market-lineage.json"],
                "Publish canonical slot evidence after the recorder has completed."),
            Internal("POSITION_MARKET_LINEAGE", "Read and validate canonical position-market lineage."),
            Gate("MARKET_READY_MARKER", "Validate the final market ready marker."),
            Child("PMS_IMPORT", Arch7bFinalStageExecutionClasses.BrokerChild,
                Arch7bExecutionKind.ChildInvoke, true, "prearmed-importer",
                "phu-qqb/IntradayPlatform", "QQ.Production.Intraday.Tools.Arch6fEconomicReplay",
                "prearm-and-import", "arch7b_prearmed_fresh_slot_handoff_cli_v1",
                "prearmed-handoff-v1", "broker child response", ["position-market-revision-binding.json"],
                "Core broker completes the prearmed PMS import."),
            Internal("ECONOMIC_REVISION", "Read the economic revision from the canonical binding."),
            Internal("REVISION_BINDING", "Validate revision-to-lineage equality."),
            Child("ARCH7A_QUALIFY_SHADOW", Arch7bFinalStageExecutionClasses.BrokerChild,
                Arch7bExecutionKind.ChildInvoke, true, "arch7a-qualification",
                "phu-qqb/IntradayPlatform", "QQ.Production.Intraday.Tools.Arch7aShadowQualification",
                "qualify-shadow", "arch7a_arch7b_rds_shadow_qualification_v1",
                "arch7a-shadow-v1", "broker child response", ["arch7a-shadow-qualification.json"],
                "Core broker executes no-order ARCH7A shadow qualification."),
            Child("REPORTING", Arch7bFinalStageExecutionClasses.BrokerChild,
                Arch7bExecutionKind.ChildInvoke, true, "read-only-reporting",
                "phu-qqb/IntradayPlatform", "QQ.Production.Intraday.Tools.OperationalReporting",
                "report-operational-state", "anubis_infx_readonly_reporting_bundle_v1",
                "operational-reporting-v1", "broker child response", ["operational-reporting.json"],
                "Core broker executes read-only reporting and closes terminally."),
            ExpectedBlocker("FINAL_WORKING_ORDER_PREFLIGHT",
                "Return ARCH7B_WORKING_ORDER_AUTHORITY_MISSING with broker send disabled."),
            Internal("TERMINAL_CLEANUP", "Run terminal cleanup and prove zero residual process/marker."),
        };
        Validate(values);
        return values;
    }

    private static Arch7bFinalStageExecutionEntry Internal(string stage, string logic) =>
        Entry(stage, Arch7bFinalStageExecutionClasses.Internal, Arch7bExecutionKind.Internal,
            false, null, null, null, null, null, null, null, [], "runtime-internal", logic);

    private static Arch7bFinalStageExecutionEntry Gate(string stage, string logic) =>
        Entry(stage, Arch7bFinalStageExecutionClasses.FilesystemGate,
            Arch7bExecutionKind.FilesystemGate, false, null, null, null, null, null, null,
            null, [], "content-addressed-filesystem-gate", logic);

    private static Arch7bFinalStageExecutionEntry ExpectedBlocker(string stage, string logic) =>
        Entry(stage, Arch7bFinalStageExecutionClasses.ExpectedBlockerGate,
            Arch7bExecutionKind.ExpectedBlockerGate, false, null, null, null, null, null, null,
            null, [], "runtime-expected-blocker-gate", logic);

    private static Arch7bFinalStageExecutionEntry Child(string stage, string executionClass,
        Arch7bExecutionKind kind, bool hasCommand, string commandId, string repository,
        string executable, string mode, string nativeContract, string? adapter,
        string outputShape, IReadOnlyList<string> artifacts, string route) =>
        Entry(stage, executionClass, kind, hasCommand, commandId, repository, executable,
            mode, nativeContract, adapter, outputShape, artifacts, route, string.Empty);

    private static Arch7bFinalStageExecutionEntry Entry(string stage, string executionClass,
        Arch7bExecutionKind kind, bool hasCommand, string? commandId, string? repository,
        string? executable, string? mode, string? nativeContract, string? adapter,
        string? outputShape, IReadOnlyList<string> artifacts, string route, string logic)
    {
        var canonical = string.Join('\n', ContractVersion, stage, executionClass, kind,
            hasCommand, commandId ?? string.Empty, repository ?? string.Empty,
            executable ?? string.Empty, mode ?? string.Empty, nativeContract ?? string.Empty,
            adapter ?? string.Empty, outputShape ?? string.Empty, string.Join('|', artifacts),
            route, logic);
        return new(stage, executionClass, kind, hasCommand, commandId, repository,
            executable, mode, nativeContract, adapter, outputShape, artifacts, route, logic,
            Arch7bOneShotContracts.Sha256(canonical));
    }

    private static void Validate(IReadOnlyList<Arch7bFinalStageExecutionEntry> values)
    {
        if (values.Count != Arch7bStages.All.Count ||
            !values.Select(value => value.StageId).SequenceEqual(Arch7bStages.All,
                StringComparer.Ordinal) || values.Select(value => value.StageId).Distinct(
                StringComparer.Ordinal).Count() != values.Count)
            throw new Arch7bQualificationException(
                Arch7bBlockers.LiveCommandAuthorityIncomplete,
                "final-stage-execution-classification");
        foreach (var value in values)
        {
            if (value.HasCommandTemplate && (string.IsNullOrWhiteSpace(value.CommandId) ||
                    string.IsNullOrWhiteSpace(value.Repository) ||
                    string.IsNullOrWhiteSpace(value.ExecutableOrModule) ||
                    string.IsNullOrWhiteSpace(value.Mode) ||
                    string.IsNullOrWhiteSpace(value.NativeContract) ||
                    string.IsNullOrWhiteSpace(value.AdapterId)))
                throw new Arch7bQualificationException(
                    Arch7bV2Blockers.CommandTemplateInvalid, value.StageId);
            if (!value.HasCommandTemplate && value.ExecutionClass is
                    Arch7bFinalStageExecutionClasses.Internal or
                    Arch7bFinalStageExecutionClasses.FilesystemGate or
                    Arch7bFinalStageExecutionClasses.ExpectedBlockerGate &&
                new[] { value.CommandId, value.Repository, value.ExecutableOrModule,
                    value.Mode, value.NativeContract, value.AdapterId }.Any(item => item is not null))
                throw new Arch7bQualificationException(
                    Arch7bV2Blockers.CommandTemplateInvalid, value.StageId);
        }
    }
}
