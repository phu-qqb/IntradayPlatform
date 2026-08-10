using QQ.Production.Intraday.Infrastructure.PostgreSql;
using System.Security.Cryptography;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bV2QualificationFixture(
    Arch7bOneShotLivePlanTemplate Template,
    Arch7bOneShotLiveExecutionAuthorityV3 Authority,
    Arch7bOneShotOperatorAuthorizationV2 OperatorAuthorization,
    string TemplateFileSha256,
    string RunRoot);

public static class Arch7bV2QualificationFactory
{
    private sealed record CommandProfile(string Stage, Arch7bExecutionKind Kind, string Adapter,
        string Contract, string Result, int Artifacts, string? ProcessKey = null,
        bool RdsRead = false, bool Capture = false);

    private static readonly IReadOnlyList<CommandProfile> Profiles =
    [
        new("CORE_PREQUALIFICATION", Arch7bExecutionKind.ChildInvoke, "core-prequalification-v1",
            "arch7b_core_runtime_prequalification_v1", "ARCH7B_CORE_RUNTIME_PREQUALIFICATION_QUALIFIED", 1),
        new("PORTAL_SESSION_PROVEN", Arch7bExecutionKind.ChildInvoke, "portal-session-v1",
            "arch7b_portal_session_recovery_v1", "ARCH7B_PORTAL_SESSION_PROVEN", 1),
        new("RDS_READ_1", Arch7bExecutionKind.ChildInvoke, "rds-arm-orchestrator-v1",
            "arch7b_operational_orchestrator_lifecycle_v1",
            "ARCH7B_ARM_IMPORT_OPERATIONAL_ORCHESTRATOR_QUALIFIED", 2, RdsRead: true),
        new("PRELOADED_LEASE_READY", Arch7bExecutionKind.ChildStartLongLived, "handoff-v3",
            "arch7b_lmax_portal_core_to_intraday_preloaded_rds_secret_handoff_v3",
            "CORE_BRACKET_HANDOFF_PRELOADED_RDS_SECRET_LEASE_QUALIFIED", 3,
            "preloaded-rds-lease", RdsRead: true),
        new("BRACKET_T2", Arch7bExecutionKind.ChildInvoke, "lmax-bracket-v1",
            "lmax_portal_bracketed_current_position_snapshot_v2",
            "ARCH7B_BRACKETED_GLOBAL_FLAT_POSITION_SNAPSHOT_CREATED", 3),
        new("CORE_FAST_SEAL", Arch7bExecutionKind.ChildInvoke, "core-fast-seal-v1",
            "arch7b_lmax_bracket_fast_seal_v1", "ARCH7B_CORE_FAST_SEAL_QUALIFIED", 4),
        new("HANDOFF_V3", Arch7bExecutionKind.ChildStop, "handoff-v3",
            "arch7b_lmax_portal_core_to_intraday_preloaded_rds_secret_handoff_v3",
            "CORE_BRACKET_HANDOFF_PRELOADED_RDS_SECRET_LEASE_QUALIFIED", 3,
            "preloaded-rds-lease"),
        new("POSITION_APPLY", Arch7bExecutionKind.ChildInvoke, "position-import-v1",
            "arch7b_fresh_position_import_fast_path_v1", "ARCH7B_POSITION_IMPORT_APPLIED", 2),
        new("RUNTIME_SELECTION", Arch7bExecutionKind.ChildInvoke, "runtime-selection-v1",
            "arch7b_position_snapshot_runtime_selection_v1", "ARCH7B_RUNTIME_POSITION_SNAPSHOT_SELECTED", 1),
        new("MARKET_PREARM", Arch7bExecutionKind.ChildStartLongLived, "market-recorder-v1",
            "arch6f_lmax_market_data_slot_capture_v1", "ARCH7B_MARKET_CAPTURE_QUALIFIED", 2,
            "market-recorder", Capture: true),
        new("MARKET_FINALIZATION", Arch7bExecutionKind.ChildStop, "market-recorder-v1",
            "arch6f_lmax_market_data_slot_capture_v1", "ARCH7B_MARKET_CAPTURE_QUALIFIED", 2,
            "market-recorder"),
        new("PMS_IMPORT", Arch7bExecutionKind.ChildInvoke, "pms-economic-replay-v1",
            "arch6f_economic_replay_v2", "ARCH7B_PMS_ECONOMIC_REPLAY_QUALIFIED", 2),
        new("ARCH7A_QUALIFY_SHADOW", Arch7bExecutionKind.ChildInvoke, "arch7a-shadow-v1",
            "arch7a_arch7b_shadow_qualification_v1", "ARCH7A_SHADOW_QUALIFICATION_PERSISTED", 2),
        new("REPORTING", Arch7bExecutionKind.ChildInvoke, "operational-reporting-v1",
            "anubis_infx_readonly_reporting_bundle_v1", "ANUBIS_INFX_READONLY_REPORTING_BUNDLE_CREATED", 2),
        new("FINAL_WORKING_ORDER_PREFLIGHT", Arch7bExecutionKind.ChildInvoke,
            "working-order-preflight-v1", "arch7b_working_order_preflight_v1",
            Arch7bOneShotContracts.ExpectedFinalBlocker, 1)
    ];

    public static Arch7bV2QualificationFixture Create(string executablePath, string runRoot,
        string? failureStage = null, string failureBehavior = "success", string? dotnetRoot = null)
    {
        executablePath = Path.GetFullPath(executablePath);
        runRoot = Path.GetFullPath(runRoot);
        var executableSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(executablePath)));
        var workingDirectory = Path.GetDirectoryName(executablePath)!;
        var authorities = new Dictionary<string, Arch7bFileAuthority>(StringComparer.Ordinal)
        {
            ["supervisor_executable"] = new("supervisor_executable", executablePath, executableSha, true, false),
            ["supervisor_working_directory"] = new("supervisor_working_directory", workingDirectory,
                Arch7bOneShotContracts.Sha256("directory:" + workingDirectory), true, false)
        };
        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            dotnetRoot = Path.GetFullPath(dotnetRoot);
            var dotnetExecutable = Path.Combine(dotnetRoot, "dotnet.exe");
            if (!Directory.Exists(dotnetRoot) || !File.Exists(dotnetExecutable))
                throw new Arch7bQualificationException(Arch7bV2Blockers.CommandNonSecretEnvironmentAuthorityMissing);
            authorities["dotnet_root"] = new("dotnet_root", dotnetRoot,
                Arch7bOneShotContracts.Sha256("arch7b_dotnet_root_authority_v1" + "\n" + dotnetRoot), true, false);
            authorities["dotnet_executable"] = new("dotnet_executable", dotnetExecutable,
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(dotnetExecutable))), true, false);
        }
        var nonSecretEnvironment = string.IsNullOrWhiteSpace(dotnetRoot) ? [] :
            Arch7bSealedNonSecretEnvironment.ForDotnetRoot(authorities);
        var commands = Profiles.Select(profile => CreateCommand(profile, failureStage, failureBehavior,
            nonSecretEnvironment)).ToArray();
        var commandSet = Arch7bOneShotContracts.Sha256(string.Join('\n', commands.Select(value => value.EvidenceSha256)));
        var adapters = new Arch7bRealCommandAdapterRegistry();
        var registry = Arch7bGlobalSloRegistry.CreateDefault();
        var chronology = Arch7bCrossRepositoryChronology.Validate(
            Arch7bCrossRepositoryChronology.CreateDefault(), registry);
        var stageContracts = CreateStageContracts();
        var supervisorCommit = Arch7bOneShotContracts.Sha256("v2-supervisor-commit")[..40];
        var supervisorTree = Arch7bOneShotContracts.Sha256("v2-supervisor-tree")[..40];
        var intradayCommit = Arch7bOneShotContracts.Sha256("v2-intraday-commit")[..40];
        var intradayTree = Arch7bOneShotContracts.Sha256("v2-intraday-tree")[..40];
        var values = Enumerable.Range(0, 10).Select(index =>
            Arch7bOneShotContracts.Sha256("authority-" + index)).ToArray();
        var template = new Arch7bOneShotLivePlanTemplate(
            Arch7bV2Contracts.LivePlanTemplateVersion, supervisorCommit, supervisorTree,
            Arch7bOneShotContracts.CoreCommit, Arch7bOneShotContracts.CoreTree,
            intradayCommit, intradayTree, values[0], values[1], values[2],
            Arch7bOneShotContracts.CoreRepositoryAuthoritySha256,
            Arch7bOneShotContracts.CoreTrackedInventorySha256, values[3], commandSet,
            adapters.EvidenceSha256, values[4], values[5], values[6], registry.EvidenceSha256,
            chronology.EvidenceSha256, values[7], "TEST",
            "1754288005", true, 1, 2, 1, 0, authorities, commands, stageContracts, string.Empty);
        template = template with { EvidenceSha256 = HashTemplate(template) };
        var templateBytes = JsonSerializer.SerializeToUtf8Bytes(template, Arch7bJson.CanonicalOptions);
        var templateFileSha = Convert.ToHexStringLower(SHA256.HashData(templateBytes));
        var now = DateTimeOffset.UtcNow;
        var authorization = new Arch7bOneShotOperatorAuthorizationV2(
            Arch7bV2Contracts.OperatorAuthorizationVersion, "offline-operator-authorization", "TEST",
            "1754288005", true, 1, 2, 1, 0, now.AddMinutes(-1), now.AddHours(2), string.Empty);
        authorization = authorization with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(authorization.Canonical())
        };
        var authority = new Arch7bOneShotLiveExecutionAuthorityV3(
            Arch7bV2Contracts.LiveExecutionAuthorityVersion, template.SupervisorCommit,
            template.SupervisorTree, template.CoreCommit, template.CoreTree, template.IntradayCommit,
            template.IntradayTree, template.FreezeManifestSha256, template.FreezePacketSha256,
            templateFileSha, template.RuntimeInventorySha256, template.CoreRepositoryAuthoritySha256,
            template.CoreTrackedInventorySha256, template.StaticAuthoritySetSha256,
            template.CommandTemplateSetSha256, template.AdapterSetSha256,
            template.RootCaAuthoritySha256, template.PrivilegeAuthoritySha256,
            template.CalendarAuthoritySha256, template.SloRegistrySha256, template.ChronologySha256,
            template.CleanupAuthoritySha256, authorization.OperatorAuthorizationId, "TEST", "1754288005",
            true, 1, 2, 1, 0, authorities, now.AddMinutes(-1), now.AddHours(2), string.Empty);
        authority = authority with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(authority.Canonical()) };
        return new(template, authority, authorization, templateFileSha, runRoot);
    }

    private static Arch7bOneShotCommandTemplate CreateCommand(CommandProfile profile,
        string? failureStage, string failureBehavior,
        IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> nonSecretEnvironment)
    {
        var commandId = "offline-" + profile.Stage.ToLowerInvariant().Replace('_', '-');
        var behavior = profile.Stage == failureStage ? failureBehavior : "success";
        var arguments = new List<Arch7bCommandTemplateArgument>();
        void Literal(string value) => arguments.Add(new(value, Arch7bPlaceholderValueKind.Literal,
            null, -1, false));
        Literal("--mode"); Literal("fake-native-child");
        Literal("--qualification-only"); Literal("true");
        Literal("--run-root");
        arguments.Add(new("${fact:runtime_run_root.path}", Arch7bPlaceholderValueKind.AbsolutePath,
            "STATIC_AUTHORITY_VALIDATION", int.MaxValue, true));
        Literal("--command-id"); Literal(commandId);
        Literal("--native-contract"); Literal(profile.Contract);
        Literal("--native-result"); Literal(profile.Result);
        Literal("--artifact-count"); Literal(profile.Artifacts.ToString());
        Literal("--behavior"); Literal(behavior);
        if (profile.ProcessKey is not null)
        {
            Literal("--process-key"); Literal(profile.ProcessKey);
        }
        var canonical = string.Join('\n', Arch7bV2Contracts.CommandTemplateVersion, commandId,
            profile.Stage, profile.Kind, "supervisor_executable", string.Join('|', arguments.Select(value => value.Value)),
            "supervisor_working_directory", profile.Adapter, Arch7bV2Contracts.ChildResultAdapterVersion,
            profile.Contract, 30, 1_048_576, 1_048_576, "qualification-child-process",
            profile.RdsRead, profile.Capture, false,
            Arch7bSealedNonSecretEnvironment.Canonical(nonSecretEnvironment), profile.ProcessKey ?? string.Empty);
        return new(Arch7bV2Contracts.CommandTemplateVersion, commandId, profile.Stage, profile.Kind,
            "supervisor_executable", arguments, "supervisor_working_directory", profile.Adapter,
            Arch7bV2Contracts.ChildResultAdapterVersion, profile.Contract, 30, 1_048_576, 1_048_576,
            "qualification-child-process", profile.RdsRead, profile.Capture, false, [], nonSecretEnvironment,
            profile.ProcessKey, Arch7bOneShotContracts.Sha256(canonical));
    }

    private static IReadOnlyList<Arch7bOneShotStageContract> CreateStageContracts()
    {
        var contracts = new List<Arch7bOneShotStageContract>();
        for (var index = 0; index < Arch7bStages.All.Count; index++)
        {
            var stage = Arch7bStages.All[index];
            var profile = Profiles.SingleOrDefault(value => value.Stage == stage);
            var kind = profile?.Kind ?? stage switch
            {
                "BRACKET_T0" or "MARKET_CAPTURE" => Arch7bExecutionKind.ChildAwaitEvidence,
                "CLOCK_PREFLIGHT" or "BRACKET_P1" or "BRACKET_T1" or "BRACKET_P2" or
                    "COMPLEMENTARY_REPORTS" or "POSITION_READY" or "MARKET_READY_MARKER" =>
                    Arch7bExecutionKind.FilesystemGate,
                _ => Arch7bExecutionKind.Internal
            };
            var produced = ProducedFacts(stage);
            var previous = index == 0 ? [] : ProducedFacts(Arch7bStages.All[index - 1]).TakeLast(1).ToArray();
            var predecessors = index == 0 ? [] : new[] { Arch7bStages.All[index - 1] };
            var canonical = string.Join('\n', stage, kind, string.Join('|', predecessors),
                string.Join('|', previous), string.Join('|', produced),
                "GLOBAL_TERMINAL_CLEANUP_DEADLINE_SECONDS", "stage-semantic-" + stage.ToLowerInvariant());
            contracts.Add(new(stage, kind, predecessors, previous, produced,
                "GLOBAL_TERMINAL_CLEANUP_DEADLINE_SECONDS", "stage-semantic-" + stage.ToLowerInvariant(),
                Arch7bOneShotContracts.Sha256(canonical)));
        }
        return contracts;
    }

    private static string[] ProducedFacts(string stage) => stage switch
    {
        "STATIC_AUTHORITY_VALIDATION" => ["static_authority_validation", "core_commit",
            "intraday_commit", "runtime_run_root"],
        "CALENDAR_LOADED" => ["calendar"],
        "SLOT_SELECTED" => ["selected_slot"],
        "SLOT_LOCKED" => ["slot_lock"],
        "CLOCK_PREFLIGHT" => ["clock_authority_preflight_snapshot"],
        "CLOCK_CAPTURE_START" => ["clock_authority_capture_snapshot"],
        "CLOCK_POST_CLOSE" => ["clock_authority_post_close_snapshot"],
        "ONE_SHOT_IDENTITIES_CREATED" => ["run_identity", "owner_identity",
            "future_authorization_identity", "source_session_identity", "market_capture_session_identity",
            "position_market_draft_output_path", "position_market_lineage_output_path",
            "position_market_revision_binding_output_path"],
        _ => [stage.ToLowerInvariant() + "_evidence"]
    };

    private static string HashTemplate(Arch7bOneShotLivePlanTemplate value) =>
        Arch7bOneShotContracts.Sha256(value.Canonical());
}

public sealed record Arch7bV2ProcessQualification(
    int IndependentRuns,
    int IndependentPasses,
    int Campaigns,
    int CampaignPasses,
    int RunsPerCampaign,
    int ResidualProcesses,
    int ResidualMarkers,
    string EvidenceSha256);

public static class Arch7bV2ProcessQualifier
{
    public static async Task<Arch7bV2ProcessQualification> RunAsync(string executablePath,
        int independentRuns, int campaigns, int runsPerCampaign,
        CancellationToken cancellationToken = default)
    {
        var evidence = new List<string>();
        var independentPasses = 0;
        for (var index = 0; index < independentRuns; index++)
        {
            var result = await RunOneAsync(executablePath, $"independent-{index:D2}", cancellationToken)
                .ConfigureAwait(false);
            if (result.Passed) independentPasses++;
            evidence.Add(result.EvidenceSha256);
        }
        var campaignPasses = 0;
        for (var campaign = 0; campaign < campaigns; campaign++)
        {
            var values = new List<Arch7bV2ExecutionEvidence>();
            for (var run = 0; run < runsPerCampaign; run++)
                values.Add(await RunOneAsync(executablePath, $"campaign-{campaign:D2}-{run:D2}",
                    cancellationToken).ConfigureAwait(false));
            if (values.All(value => value.Passed) && values.Select(value => value.RunId).Distinct().Count() == values.Count)
                campaignPasses++;
            evidence.AddRange(values.Select(value => value.EvidenceSha256));
        }
        return new(independentRuns, independentPasses, campaigns, campaignPasses, runsPerCampaign,
            0, 0, Arch7bOneShotContracts.Sha256(string.Join('\n', evidence)));
    }

    public static Task<Arch7bV2ExecutionEvidence> RunSingleAsync(string executablePath,
        string suffix, CancellationToken cancellationToken = default,
        TimeProvider? timeProvider = null,
        IPmsShadowCaptureClockAuthorityProducer? clockAuthorityProducer = null) =>
        RunOneAsync(executablePath, suffix, cancellationToken, timeProvider,
            clockAuthorityProducer);

    private static async Task<Arch7bV2ExecutionEvidence> RunOneAsync(string executablePath,
        string suffix, CancellationToken cancellationToken,
        TimeProvider? timeProvider = null,
        IPmsShadowCaptureClockAuthorityProducer? clockAuthorityProducer = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "qq-arch7b-v2-rehearsal",
            suffix + "-" + Guid.NewGuid().ToString("N"));
        var fixture = Arch7bV2QualificationFactory.Create(executablePath, root);
        var adapters = new Arch7bRealCommandAdapterRegistry();
        var runtime = new Arch7bOneShotLiveExecutionRuntimeV2(new(),
            new Arch7bOneShotProcessRunnerV2(adapters), adapters,
            clockAuthorityProducer: clockAuthorityProducer);
        var result = await runtime.RunAsync(fixture.Template, fixture.Authority,
            fixture.OperatorAuthorization, fixture.TemplateFileSha256, root,
            timeProvider ?? TimeProvider.System,
            new Arch7bCoreOwnedSecretLease(), cancellationToken).ConfigureAwait(false);
        if (Directory.Exists(root)) Directory.Delete(root, true);
        return result;
    }
}
