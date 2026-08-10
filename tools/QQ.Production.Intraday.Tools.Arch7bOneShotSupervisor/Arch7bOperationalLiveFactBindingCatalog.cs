using QQ.Production.Intraday.Infrastructure.PostgreSql;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public enum Arch7bOperationalPlaceholderScope
{
    Fact,
    Artifact,
    Authority
}

public sealed record Arch7bOperationalLiveFactBinding(
    string ContractVersion,
    string BindingId,
    string CommandId,
    string StageId,
    int ArgumentIndex,
    string ArgumentName,
    Arch7bOperationalPlaceholderScope PlaceholderScope,
    string PlaceholderName,
    string PlaceholderField,
    Arch7bPlaceholderValueKind ValueKind,
    string? RequiredProducerStage,
    int MaximumAgeSeconds,
    bool MustBeInsideRunRoot,
    bool Required,
    string ConsumerParserContract,
    string ConsumerParserSourceFile,
    string ConsumerParserSymbol,
    string ProducerContract,
    string ProducerSourceFile,
    string ProducerSymbol,
    string Rationale,
    string EvidenceSha256)
{
    public string Placeholder => "$" + "{" +
        PlaceholderScope.ToString().ToLowerInvariant() + ":" + PlaceholderName +
        "." + PlaceholderField + "}";

    public string Canonical() => string.Join('\n', ContractVersion, BindingId, CommandId,
        StageId, ArgumentIndex, ArgumentName, PlaceholderScope, PlaceholderName,
        PlaceholderField, ValueKind, RequiredProducerStage ?? string.Empty,
        MaximumAgeSeconds, MustBeInsideRunRoot, Required, ConsumerParserContract,
        ConsumerParserSourceFile, ConsumerParserSymbol, ProducerContract,
        ProducerSourceFile, ProducerSymbol, Rationale);
}

public sealed record Arch7bOperationalCommandBindingSet(
    string CommandId,
    string StageId,
    string ExecutableProject,
    string Mode,
    IReadOnlyList<Arch7bOperationalLiveFactBinding> Bindings);

public sealed record Arch7bOperationalBindingCatalogDocument(
    string ContractVersion,
    int CommandCount,
    int BindingCount,
    IReadOnlyList<Arch7bOperationalCommandBindingSet> Commands,
    string EvidenceSha256);

public sealed record Arch7bV7CommandMarker(
    string ContractVersion,
    string CommandId,
    string Executable,
    string Mode,
    int ArgumentIndex,
    string ArgumentName,
    string CurrentMarkerValue,
    string ParserSourceFile,
    string ParserSymbol,
    string ParserType,
    string ConsumerStage,
    string SourceManifest,
    string SourceManifestSha256,
    string EvidenceSha256);

public sealed record Arch7bV7CommandMarkerInventory(
    string ContractVersion,
    int CommandCount,
    int MarkerCount,
    IReadOnlyList<Arch7bV7CommandMarker> Markers,
    string EvidenceSha256);

public static class Arch7bOperationalLiveFactBindingCatalog
{
    public const string ContractVersion = "arch7b_operational_live_fact_binding_catalog_v1";
    public const string MarkerInventoryVersion = "arch7b_v7_six_command_marker_inventory_v1";
    public const string Marker = "REGENERATE_JUST_BEFORE_LIVE_RUN";

    private const string HandoffContract = "arch7b_prearmed_fresh_slot_handoff_cli_v1";
    private const string HandoffSource =
        "tools/QQ.Production.Intraday.Tools.Arch6fEconomicReplay/Arch7bPrearmedFreshSlotHandoffCli.cs";
    private const string HandoffSymbol = "Arch7bPrearmedFreshSlotHandoffCli.RunAsync";
    private const string MarketContract = "arch6f_lmax_market_data_slot_capture_v1";
    private const string MarketSource =
        "tools/QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly/Program.cs";
    private const string MarketSymbol = "LmaxMarketDataCaptureOnly.Program";
    private const string Arch7aContract = "arch7a_arch7b_rds_shadow_qualification_v1";
    private const string Arch7aSource =
        "tools/QQ.Production.Intraday.Tools.Arch7aShadowQualification/Arch7aArch7bShadowQualification.cs";
    private const string Arch7aSymbol = "Arch7aArch7bShadowQualificationArguments.Parse";
    private const string ReportingContract = "anubis_infx_readonly_reporting_bundle_v1";
    private const string ReportingSource =
        "tools/QQ.Production.Intraday.Tools.OperationalReporting/Program.cs";
    private const string ReportingSymbol = "ReportingArguments.Parse";

    public static IReadOnlyList<Arch7bOperationalCommandBindingSet> Build()
    {
        var commands = new[]
        {
            new Arch7bOperationalCommandBindingSet("prearmed-importer", "PMS_IMPORT",
                "QQ.Production.Intraday.Tools.Arch6fEconomicReplay", "prearm-and-import",
                HandoffBindings("prearmed-importer", "PMS_IMPORT",
                    [
                        Spec(0, "--position-market-draft-path", Arch7bOperationalPlaceholderScope.Fact,
                            "position_market_draft_output_path", "path",
                            Arch7bPlaceholderValueKind.AbsolutePath, "ONE_SHOT_IDENTITIES_CREATED", -1, true,
                            "arch7b_one_shot_run_artifact_path_v1", RunPathSource(),
                            "Arch7bOneShotRunArtifactPath.ReservePositionMarketDraft",
                            "The existing prearm command is the create-new producer of the draft."),
                        Spec(1, "--position-market-lineage-path", Arch7bOperationalPlaceholderScope.Fact,
                            "position_market_lineage_output_path", "path",
                            Arch7bPlaceholderValueKind.AbsolutePath, "ONE_SHOT_IDENTITIES_CREATED", -1, true,
                            "arch7b_one_shot_run_artifact_path_v1", RunPathSource(),
                            "Arch7bOneShotRunArtifactPath.ReservePositionMarketLineage",
                            "The finalizer writes the canonical lineage to this reserved run path."),
                        Spec(2, "--position-market-revision-binding-path", Arch7bOperationalPlaceholderScope.Fact,
                            "position_market_revision_binding_output_path", "path",
                            Arch7bPlaceholderValueKind.AbsolutePath, "ONE_SHOT_IDENTITIES_CREATED", -1, true,
                            "arch7b_one_shot_run_artifact_path_v1", RunPathSource(),
                            "Arch7bOneShotRunArtifactPath.ReservePositionMarketRevisionBinding",
                            "The economic replay writes the canonical revision binding to this reserved run path."),
                        Spec(3, "--core-commit", Arch7bOperationalPlaceholderScope.Fact,
                            "core_commit", "value", Arch7bPlaceholderValueKind.GitCommit,
                            "STATIC_AUTHORITY_VALIDATION", -1, false,
                            Arch7bV2Contracts.LivePlanTemplateVersion, RuntimeSource(),
                            "Arch7bOneShotLiveExecutionRuntimeV2.STATIC_AUTHORITY_VALIDATION",
                            "The static template authority owns the exact Core commit."),
                        Spec(4, "--market-capture-session-id", Arch7bOperationalPlaceholderScope.Fact,
                            "market_capture_session_identity", "value", Arch7bPlaceholderValueKind.Guid,
                            "ONE_SHOT_IDENTITIES_CREATED", -1, false,
                            Arch7bV2Contracts.LiveFactStoreVersion, RuntimeSource(),
                            "Arch7bOneShotLiveExecutionRuntimeV2.ONE_SHOT_IDENTITIES_CREATED",
                            "The one-shot identity stage creates the immutable capture session identity."),
                        Spec(5, "--market-data-config-path", Arch7bOperationalPlaceholderScope.Authority,
                            "market_data_config", "path", Arch7bPlaceholderValueKind.AbsolutePath,
                            null, -1, false, "arch6f_market_data_config_authority_v1",
                            "freeze/arch7b-static-authorities.json", "market_data_config",
                            "The market data configuration is a static content-addressed freeze authority."),
                        Spec(6, "--expected-market-data-config-sha256",
                            Arch7bOperationalPlaceholderScope.Authority, "market_data_config", "sha256",
                            Arch7bPlaceholderValueKind.Sha256, null, -1, false,
                            "arch6f_market_data_config_authority_v1",
                            "freeze/arch7b-static-authorities.json", "market_data_config",
                            "The expected hash is owned by the same static configuration authority."),
                        ClockSnapshot(7, "--clock-authority-preflight-snapshot",
                            "clock_authority_preflight_snapshot", "CLOCK_PREFLIGHT")
                    ])),
            new Arch7bOperationalCommandBindingSet("capture-starter", "CLOCK_CAPTURE_START",
                "QQ.Production.Intraday.Tools.Arch6fEconomicReplay", "assert-prearmed",
                HandoffBindings("capture-starter", "CLOCK_CAPTURE_START",
                    [
                        DraftArtifact(0, "--position-market-draft-path", "path"),
                        DraftArtifact(1, "--expected-position-market-draft-sha256", "sha256"),
                        Spec(2, "--core-commit", Arch7bOperationalPlaceholderScope.Fact,
                            "core_commit", "value", Arch7bPlaceholderValueKind.GitCommit,
                            "STATIC_AUTHORITY_VALIDATION", -1, false,
                            Arch7bV2Contracts.LivePlanTemplateVersion, RuntimeSource(),
                            "Arch7bOneShotLiveExecutionRuntimeV2.STATIC_AUTHORITY_VALIDATION",
                            "The exact Core commit is static for the one-shot run."),
                        Spec(3, "--market-capture-session-id", Arch7bOperationalPlaceholderScope.Fact,
                            "market_capture_session_identity", "value", Arch7bPlaceholderValueKind.Guid,
                            "ONE_SHOT_IDENTITIES_CREATED", -1, false,
                            Arch7bV2Contracts.LiveFactStoreVersion, RuntimeSource(),
                            "Arch7bOneShotLiveExecutionRuntimeV2.ONE_SHOT_IDENTITIES_CREATED",
                            "The capture starter consumes the same immutable session identity as the draft."),
                        StaticMarketConfig(4, "--market-data-config-path", "path",
                            Arch7bPlaceholderValueKind.AbsolutePath),
                        StaticMarketConfig(5, "--expected-market-data-config-sha256", "sha256",
                            Arch7bPlaceholderValueKind.Sha256),
                        ClockSnapshot(6, "--clock-authority-capture-snapshot",
                            "clock_authority_capture_snapshot", "CLOCK_CAPTURE_START")
                    ])),
            new Arch7bOperationalCommandBindingSet("market-data-recorder", "MARKET_CAPTURE",
                "QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly", "live-market-data-only",
                MarketBindings("market-data-recorder", "MARKET_CAPTURE",
                    [
                        DraftArtifact(0, "--position-market-draft-path", "path"),
                        DraftArtifact(1, "--expected-position-market-draft-sha256", "sha256")
                    ])),
            new Arch7bOperationalCommandBindingSet("canonical-slot-finalizer", "MARKET_FINALIZATION",
                "QQ.Production.Intraday.Tools.Arch6fEconomicReplay", "publish-ready",
                HandoffBindings("canonical-slot-finalizer", "MARKET_FINALIZATION",
                    [
                        DraftArtifact(0, "--position-market-draft-path", "path"),
                        DraftArtifact(1, "--expected-position-market-draft-sha256", "sha256"),
                        Spec(2, "--position-market-lineage-path", Arch7bOperationalPlaceholderScope.Fact,
                            "position_market_lineage_output_path", "path",
                            Arch7bPlaceholderValueKind.AbsolutePath, "ONE_SHOT_IDENTITIES_CREATED", -1, true,
                            "arch7b_one_shot_run_artifact_path_v1", RunPathSource(),
                            "Arch7bOneShotRunArtifactPath.ReservePositionMarketLineage",
                            "The finalizer owns creation of the canonical lineage artifact."),
                        Spec(3, "--core-commit", Arch7bOperationalPlaceholderScope.Fact,
                            "core_commit", "value", Arch7bPlaceholderValueKind.GitCommit,
                            "STATIC_AUTHORITY_VALIDATION", -1, false,
                            Arch7bV2Contracts.LivePlanTemplateVersion, RuntimeSource(),
                            "Arch7bOneShotLiveExecutionRuntimeV2.STATIC_AUTHORITY_VALIDATION",
                            "The finalizer binds the lineage to the static Core commit."),
                        Spec(4, "--market-capture-session-id", Arch7bOperationalPlaceholderScope.Fact,
                            "market_capture_session_identity", "value", Arch7bPlaceholderValueKind.Guid,
                            "ONE_SHOT_IDENTITIES_CREATED", -1, false,
                            Arch7bV2Contracts.LiveFactStoreVersion, RuntimeSource(),
                            "Arch7bOneShotLiveExecutionRuntimeV2.ONE_SHOT_IDENTITIES_CREATED",
                            "The finalizer binds the lineage to the one-shot capture session."),
                        ClockSnapshot(5, "--clock-authority-post-close-snapshot",
                            "clock_authority_post_close_snapshot", "CLOCK_POST_CLOSE")
                    ])),
            new Arch7bOperationalCommandBindingSet("arch7a-qualification", "ARCH7A_QUALIFY_SHADOW",
                "QQ.Production.Intraday.Tools.Arch7aShadowQualification", "qualify-shadow",
                Arch7aBindings("arch7a-qualification", "ARCH7A_QUALIFY_SHADOW",
                    [
                        Spec(1, "--economic-revision-id", Arch7bOperationalPlaceholderScope.Artifact,
                            "economic_revision_artifact", "economic_revision_id",
                            Arch7bPlaceholderValueKind.Guid, "ECONOMIC_REVISION", -1, false,
                            "arch7b_economic_revision_artifact_v1", RuntimeSource(),
                            "Arch7bOneShotLiveExecutionRuntimeV2.ECONOMIC_REVISION",
                            "The validated revision binding owns the exact economic revision identifier."),
                        Spec(2, "--slot-id", Arch7bOperationalPlaceholderScope.Fact,
                            "selected_slot", "slot_id", Arch7bPlaceholderValueKind.String,
                            "SLOT_SELECTED", -1, false,
                            Arch7bOneShotContracts.OperationalSlotSelectionPolicyVersion,
                            RuntimeSource(), "Arch7bOperationalSlotSelector.SelectAndLock",
                            "The selected slot fact is the schedule authority for qualification."),
                        Spec(3, "--source-session-id", Arch7bOperationalPlaceholderScope.Fact,
                            "source_session_identity", "value", Arch7bPlaceholderValueKind.String,
                            "ONE_SHOT_IDENTITIES_CREATED", -1, false,
                            Arch7bV2Contracts.LiveFactStoreVersion, RuntimeSource(),
                            "Arch7bOneShotLiveExecutionRuntimeV2.ONE_SHOT_IDENTITIES_CREATED",
                            "The source session identity is immutable for the one-shot run."),
                        RevisionArtifact(4, "--position-market-revision-binding-path", "path"),
                        RevisionArtifact(5, "--expected-position-market-revision-binding-sha256", "sha256"),
                        Spec(16, "--repository-commit", Arch7bOperationalPlaceholderScope.Fact,
                            "intraday_commit", "value", Arch7bPlaceholderValueKind.GitCommit,
                            "STATIC_AUTHORITY_VALIDATION", -1, false,
                            Arch7bV2Contracts.LivePlanTemplateVersion, RuntimeSource(),
                            "Arch7bOneShotLiveExecutionRuntimeV2.STATIC_AUTHORITY_VALIDATION",
                            "The static template authority owns the exact Intraday commit."),
                        Spec(17, "--output-directory", Arch7bOperationalPlaceholderScope.Fact,
                            "runtime_run_root", "path", Arch7bPlaceholderValueKind.AbsolutePath,
                            "STATIC_AUTHORITY_VALIDATION", -1, true,
                            Arch7bV2Contracts.LiveFactStoreVersion, RuntimeSource(),
                            "Arch7bOneShotLiveExecutionRuntimeV2.STATIC_AUTHORITY_VALIDATION",
                            "The parser accepts an absolute output directory and the create-new RunRoot is authoritative.")
                    ])),
            new Arch7bOperationalCommandBindingSet("read-only-reporting", "REPORTING",
                "QQ.Production.Intraday.Tools.OperationalReporting", "report-operational-state",
                ReportingBindings("read-only-reporting", "REPORTING",
                    [
                        LineageArtifact(0, "--position-market-lineage-path", "path"),
                        LineageArtifact(1, "--expected-position-market-lineage-sha256", "sha256"),
                        RevisionArtifact(2, "--position-market-revision-binding-path", "path"),
                        RevisionArtifact(3, "--expected-position-market-revision-binding-sha256", "sha256")
                    ]))
        };
        Validate(commands);
        return commands;
    }

    public static Arch7bOperationalBindingCatalogDocument Document()
    {
        var commands = Build();
        var canonical = string.Join('\n', ContractVersion, commands.Count,
            commands.Sum(value => value.Bindings.Count),
            string.Join('|', commands.SelectMany(value => value.Bindings)
                .Select(value => value.EvidenceSha256)));
        return new(ContractVersion, commands.Count, commands.Sum(value => value.Bindings.Count),
            commands, Arch7bOneShotContracts.Sha256(canonical));
    }

    public static async Task<string> WriteJsonAsync(string path,
        CancellationToken cancellationToken = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(Document(), Arch7bJson.CanonicalOptions);
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    public static Arch7bV7CommandMarkerInventory InventoryMarkers(
        string sourceManifestPath, byte[] sourceManifestBytes)
    {
        var sourceSha = Convert.ToHexStringLower(SHA256.HashData(sourceManifestBytes));
        using var document = JsonDocument.Parse(sourceManifestBytes);
        var commands = document.RootElement.GetProperty("commands").EnumerateArray().ToArray();
        var catalog = Build();
        if (commands.Length != catalog.Count)
            throw new Arch7bQualificationException(Arch7bV2Blockers.CommandTemplateInvalid,
                "six-command-count");
        var markers = new List<Arch7bV7CommandMarker>();
        foreach (var command in commands)
        {
            var commandId = command.GetProperty("owner").GetString() ?? string.Empty;
            var executable = command.GetProperty("executable").GetString() ?? string.Empty;
            var mode = command.GetProperty("mode").GetString() ?? string.Empty;
            var commandCatalog = catalog.SingleOrDefault(value => value.CommandId == commandId)
                ?? throw new Arch7bQualificationException(Arch7bV2Blockers.CommandTemplateInvalid,
                    commandId);
            var index = 0;
            foreach (var argument in command.GetProperty("arguments").EnumerateObject())
            {
                if (argument.Value.GetString() == Marker)
                {
                    var binding = commandCatalog.Bindings.SingleOrDefault(value =>
                        value.ArgumentIndex == index && value.ArgumentName == argument.Name)
                        ?? throw new Arch7bQualificationException(
                            Arch7bV2Blockers.RequiredFactMissing,
                            commandId + ":" + argument.Name);
                    var canonical = string.Join('\n', MarkerInventoryVersion, commandId,
                        executable, mode, index, argument.Name, Marker,
                        binding.ConsumerParserSourceFile, binding.ConsumerParserSymbol,
                        binding.ValueKind, binding.StageId, sourceManifestPath, sourceSha);
                    markers.Add(new(MarkerInventoryVersion, commandId, executable, mode, index,
                        argument.Name, Marker, binding.ConsumerParserSourceFile,
                        binding.ConsumerParserSymbol, binding.ValueKind.ToString(), binding.StageId,
                        sourceManifestPath, sourceSha, Arch7bOneShotContracts.Sha256(canonical)));
                }
                index++;
            }
        }
        var expected = catalog.SelectMany(value => value.Bindings).Count();
        if (markers.Count != expected ||
            markers.Select(value => (value.CommandId, value.ArgumentIndex, value.ArgumentName))
                .Distinct().Count() != markers.Count)
            throw new Arch7bQualificationException(Arch7bV2Blockers.CommandTemplateInvalid,
                "marker-binding-bijection");
        var inventoryCanonical = string.Join('\n', MarkerInventoryVersion, commands.Length,
            markers.Count, string.Join('|', markers.Select(value => value.EvidenceSha256)));
        return new(MarkerInventoryVersion, commands.Length, markers.Count, markers,
            Arch7bOneShotContracts.Sha256(inventoryCanonical));
    }

    private static void Validate(IReadOnlyList<Arch7bOperationalCommandBindingSet> commands)
    {
        if (commands.Count != 6 || commands.Select(value => value.CommandId)
                .Distinct(StringComparer.Ordinal).Count() != 6)
            throw new Arch7bQualificationException(Arch7bV2Blockers.CommandTemplateInvalid,
                "six-command-catalog");
        var bindings = commands.SelectMany(value => value.Bindings).ToArray();
        if (bindings.Length != 34 ||
            bindings.Select(value => value.BindingId).Distinct(StringComparer.Ordinal).Count() != bindings.Length ||
            bindings.Select(value => (value.CommandId, value.ArgumentIndex)).Distinct().Count() != bindings.Length ||
            bindings.Any(value => !Arch7bStages.All.Contains(value.StageId, StringComparer.Ordinal) ||
                value.EvidenceSha256 != Arch7bOneShotContracts.Sha256(value.Canonical()) ||
                !value.Required))
            throw new Arch7bQualificationException(Arch7bV2Blockers.CommandTemplateInvalid,
                "binding-catalog");
    }

    private static IReadOnlyList<Arch7bOperationalLiveFactBinding> HandoffBindings(
        string commandId, string stageId, IReadOnlyList<BindingSpec> specs) =>
        Bindings(commandId, stageId, HandoffContract, HandoffSource, HandoffSymbol, specs);

    private static IReadOnlyList<Arch7bOperationalLiveFactBinding> MarketBindings(
        string commandId, string stageId, IReadOnlyList<BindingSpec> specs) =>
        Bindings(commandId, stageId, MarketContract, MarketSource, MarketSymbol, specs);

    private static IReadOnlyList<Arch7bOperationalLiveFactBinding> Arch7aBindings(
        string commandId, string stageId, IReadOnlyList<BindingSpec> specs) =>
        Bindings(commandId, stageId, Arch7aContract, Arch7aSource, Arch7aSymbol, specs);

    private static IReadOnlyList<Arch7bOperationalLiveFactBinding> ReportingBindings(
        string commandId, string stageId, IReadOnlyList<BindingSpec> specs) =>
        Bindings(commandId, stageId, ReportingContract, ReportingSource, ReportingSymbol, specs);

    private static IReadOnlyList<Arch7bOperationalLiveFactBinding> Bindings(
        string commandId, string stageId, string parserContract, string parserSource,
        string parserSymbol, IReadOnlyList<BindingSpec> specs) =>
        specs.Select(spec =>
        {
            var id = $"{commandId}:{spec.ArgumentIndex}:{spec.ArgumentName}";
            var provisional = new Arch7bOperationalLiveFactBinding(ContractVersion, id,
                commandId, stageId, spec.ArgumentIndex, spec.ArgumentName, spec.Scope,
                spec.Name, spec.Field, spec.Kind, spec.ProducerStage, spec.MaximumAgeSeconds,
                spec.InsideRunRoot, true, parserContract, parserSource, parserSymbol,
                spec.ProducerContract, spec.ProducerSource, spec.ProducerSymbol,
                spec.Rationale, string.Empty);
            return provisional with
            {
                EvidenceSha256 = Arch7bOneShotContracts.Sha256(provisional.Canonical())
            };
        }).ToArray();

    private static BindingSpec DraftArtifact(int index, string argument, string field) =>
        Spec(index, argument, Arch7bOperationalPlaceholderScope.Artifact,
            "position_market_draft_artifact", field,
            field == "path" ? Arch7bPlaceholderValueKind.AbsolutePath :
                Arch7bPlaceholderValueKind.Sha256,
            "POSITION_MARKET_DRAFT", -1, field == "path",
            "arch7b_position_market_slot_lineage_v1", RuntimeSource(),
            "Arch7bOneShotLiveExecutionRuntimeV2.POSITION_MARKET_DRAFT",
            "Only the validated post-creation draft artifact may supply this value.");

    private static BindingSpec LineageArtifact(int index, string argument, string field) =>
        Spec(index, argument, Arch7bOperationalPlaceholderScope.Artifact,
            "position_market_lineage_artifact", field,
            field == "path" ? Arch7bPlaceholderValueKind.AbsolutePath :
                Arch7bPlaceholderValueKind.Sha256,
            "POSITION_MARKET_LINEAGE", -1, field == "path",
            "arch7b_position_market_slot_lineage_v1", RuntimeSource(),
            "Arch7bOneShotLiveExecutionRuntimeV2.POSITION_MARKET_LINEAGE",
            "Only the content-addressed lineage gate may supply this value.");

    private static BindingSpec RevisionArtifact(int index, string argument, string field) =>
        Spec(index, argument, Arch7bOperationalPlaceholderScope.Artifact,
            "position_market_revision_binding_artifact", field,
            field == "path" ? Arch7bPlaceholderValueKind.AbsolutePath :
                Arch7bPlaceholderValueKind.Sha256,
            "REVISION_BINDING", -1, field == "path",
            "arch7b_position_market_revision_input_binding_v1", RuntimeSource(),
            "Arch7bOneShotLiveExecutionRuntimeV2.REVISION_BINDING",
            "Only the content-addressed revision binding gate may supply this value.");

    private static BindingSpec ClockSnapshot(int index, string argument, string fact,
        string producerStage) =>
        Spec(index, argument, Arch7bOperationalPlaceholderScope.Fact,
            fact, "path", Arch7bPlaceholderValueKind.AbsolutePath,
            producerStage,
            PmsShadowCaptureClockAuthorityContract.MaximumSnapshotAgeSeconds,
            true, PmsShadowCaptureClockAuthorityMeasurementContract.Version,
            "src/QQ.Production.Intraday.Infrastructure.PostgreSql/PmsShadowCaptureClockAuthorityProducer.cs",
            "PmsShadowCaptureClockAuthorityProducer.ProduceAsync",
            "The fresh clock snapshot is measured, validated and written atomically inside RunRoot.");

    private static BindingSpec StaticMarketConfig(int index, string argument, string field,
        Arch7bPlaceholderValueKind kind) =>
        Spec(index, argument, Arch7bOperationalPlaceholderScope.Authority,
            "market_data_config", field, kind, null, -1, false,
            "arch6f_market_data_config_authority_v1",
            "freeze/arch7b-static-authorities.json", "market_data_config",
            "The market data configuration is a static content-addressed freeze authority.");

    private static BindingSpec Spec(int index, string argument,
        Arch7bOperationalPlaceholderScope scope, string name, string field,
        Arch7bPlaceholderValueKind kind, string? producerStage, int maximumAgeSeconds,
        bool insideRunRoot, string producerContract, string producerSource,
        string producerSymbol, string rationale) =>
        new(index, argument, scope, name, field, kind, producerStage,
            maximumAgeSeconds, insideRunRoot, producerContract, producerSource,
            producerSymbol, rationale);

    private static string RuntimeSource() =>
        "tools/QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor/Arch7bLiveExecutionRuntimeV2.cs";

    private static string RunPathSource() =>
        "tools/QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor/Arch7bOneShotRunArtifactPath.cs";

    private sealed record BindingSpec(
        int ArgumentIndex,
        string ArgumentName,
        Arch7bOperationalPlaceholderScope Scope,
        string Name,
        string Field,
        Arch7bPlaceholderValueKind Kind,
        string? ProducerStage,
        int MaximumAgeSeconds,
        bool InsideRunRoot,
        string ProducerContract,
        string ProducerSource,
        string ProducerSymbol,
        string Rationale);
}
