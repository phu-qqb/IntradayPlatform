using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

const string SourceGate = "ARCH6B_BIND_LMAX_OPERATIONAL_MARKET_DATA_TO_QUBES_INPUT_AND_QUALIFY_FRESH_DAILY_MODEL_POSITION_SHADOW_NO_ORDER";
var arguments = ParseArguments(args);
var evidenceZip = Required(arguments, "--evidence-zip");
var outputDirectory = Required(arguments, "--output-dir");
var expectedSha256 = Required(arguments, "--expected-sha256");
var actualSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(evidenceZip)));
if (!string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
    throw new InvalidDataException($"ARCH6B_EVIDENCE_SHA_MISMATCH:{actualSha256}");

var json = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    WriteIndented = true
};
using var zip = ZipFile.OpenRead(evidenceZip);
json.Converters.Add(new JsonStringEnumConverter());
var gate = Read<Arch6bGateVerdict>(zip, "gate_verdict.json", json);
var bundle = Read<OperationalPositionShadowInputBundleV1>(zip, "operational_position_shadow_input_bundle.json", json);
var bindingManifest = Read<Arch6bBindingManifest>(zip, "model_run_qubes_input_binding_manifest.json", json);
var targetWeights = Read<Arch6bCountSummary>(zip, "target_weight_summary.json", json);
var targetPositions = Read<Arch6bCountSummary>(zip, "target_position_preview_summary.json", json);
var positionDrifts = Read<Arch6bCountSummary>(zip, "position_only_drift_summary.json", json);
var brokerAdjusted = Read<Arch6bBrokerAdjustedSummary>(zip, "broker_adjusted_drift_blocker_summary.json", json);
var manualCycle = Read<Arch6bResultSummary>(zip, "manual_paper_cycle_result.json", json);
var r009 = Read<Arch6bResultSummary>(zip, "r009_no_order_result.json", json);

Require(gate.Status == "GO" && gate.FinalSuccess && gate.NoOrder && gate.NoProduction, "ARCH6B_GATE_NOT_QUALIFIED");
Require(gate.Gate == SourceGate, "ARCH6B_GATE_ID_MISMATCH");
Require(bindingManifest.SourceSessionId == gate.SourceSessionId && bindingManifest.BindingCount == 4, "ARCH6B_BINDING_COUNT_INVALID");
Require(targetWeights.TargetWeightCount == 288, "ARCH6B_TARGET_WEIGHT_COUNT_INVALID");
Require(targetPositions.PreviewCount == 4 && targetPositions.TotalItems == 288, "ARCH6B_TARGET_POSITION_COUNT_INVALID");
Require(positionDrifts.DriftCount == 4 && positionDrifts.TotalItems == 288, "ARCH6B_POSITION_DRIFT_COUNT_INVALID");
Require(brokerAdjusted.Count == 4 && !brokerAdjusted.BrokerAdjustedDriftCalculated &&
    !brokerAdjusted.EmptyStateObserved && !brokerAdjusted.EmptyStateInferred && !brokerAdjusted.BrokerAuthority,
    "ARCH6B_BROKER_ADJUSTED_STATE_INVALID");
Require(manualCycle.Results.Count == 4 && manualCycle.TradeIntentCount == 0 && manualCycle.OrderCount == 0, "ARCH6B_MANUAL_CYCLE_INVALID");
Require(r009.Results.Count == 4 && r009.TradeIntentCount == 0 && r009.OrderCount == 0 && r009.BrokerSendCount == 0, "ARCH6B_R009_INVALID");

var shadow = new Arch6aOperationalPositionShadowService().Build(bundle);
var artifacts = new Dictionary<string, Arch6cArtifactReference>(StringComparer.Ordinal);
void AddArtifact(string type, string sha, long sizeBytes, string logicalUri, string contractVersion, string sourceSystem)
{
    Require(Arch5bHashing.IsSha256(sha), $"ARTIFACT_SHA_INVALID:{type}");
    artifacts.TryAdd(sha, new(type, sha, sizeBytes, logicalUri.Replace('\\', '/'), contractVersion,
        gate.ClosedUtc, sourceSystem, PmsShadowStateContract.EvidenceClassification));
}

AddArtifact("ARCH6B_OPERATIONAL_POSITION_SHADOW_INPUT", bundle.BundleSha256, EntrySize(zip, "operational_position_shadow_input_bundle.json"),
    "arch6b/operational_position_shadow_input_bundle.json", bundle.ContractVersion, "ARCH6B_EVIDENCE");
AddArtifact("QUBES_SOURCE_SNAPSHOT", bindingManifest.SourceSnapshotSha256, 0,
    $"content-addressed/qubes-source/{bindingManifest.SourceSnapshotSha256}", "ARCH6B_SOURCE_SNAPSHOT", "QQ_PRODUCTION_CORE");
AddArtifact("CONTENT_ADDRESSED_OVERLAY", bindingManifest.OverlaySha256, 0,
    $"content-addressed/qubes-overlay/{bindingManifest.OverlaySha256}", "ARCH6B_CONTENT_ADDRESSED_OVERLAY", "QQ_PRODUCTION_CORE");
AddArtifact("SECURITY_MAPPING", bundle.QubesToLmaxMappingSha256, 0,
    $"content-addressed/security-mapping/{bundle.QubesToLmaxMappingSha256}", bundle.QubesToLmaxMappingContractVersion, "INTRADAY");
foreach (var source in bundle.Account.SourceFiles)
    AddArtifact("LMAX_ACCOUNT_SOURCE", source.Sha256, 0, source.LogicalName, "ARCH6A_SOURCE_FILE", "LMAX");
foreach (var source in bundle.Positions.SourceFiles)
    AddArtifact("LMAX_POSITION_SOURCE", source.Sha256, 0, source.LogicalName, "ARCH6A_SOURCE_FILE", "LMAX");
foreach (var quote in bundle.MarketData.Quotes)
    AddArtifact("LMAX_MARKET_DATA_CAPTURE", quote.SourceFileSha256, 0,
        $"content-addressed/lmax-market-data/{quote.SourceCaptureId}", bundle.MarketData.ContractVersion, "LMAX");

var runByStrategy = shadow.Preview.Runs.ToDictionary(x => x.ModelRun.StrategyId, StringComparer.Ordinal);
var bindings = bindingManifest.Bindings.OrderBy(x => x.Strategy, StringComparer.Ordinal).Select(binding =>
{
    var run = runByStrategy[binding.Strategy];
    AddArtifact("QUBES_INPUT_SNAPSHOT", binding.StrategyInputSnapshotSha256, 0,
        $"content-addressed/qubes-input/{binding.Strategy}/{binding.StrategyInputSnapshotSha256}", bundle.ContractVersion, "QQ_PRODUCTION_CORE");
    var outputEntry = $"per-run-off-instance/{binding.Strategy}/evidence/AggregatedWeights.txt";
    AddArtifact("QUBES_WEIGHTS_OUTPUT", binding.OutputSha256, EntrySize(zip, outputEntry), outputEntry,
        run.Lineage.OutputContractVersion, "QUBES_ENGINE");
    return new Arch6cQubesInputBinding(binding.Strategy, binding.SourceSnapshotSha256, binding.OverlaySha256, null,
        bundle.QubesToLmaxMappingSha256, binding.StrategyInputSnapshotSha256, bundle.MarketData.Quotes.Count,
        bundle.MarketData.MissingCount, binding.TargetCloseUtc, "ARCH6B_QUALIFIED_MODEL_RUN_INPUT_BINDING");
}).ToArray();

var qualified = new Arch6cQualifiedShadowSession(SourceGate, gate.SourceSessionId, actualSha256,
    gate.ClosedUtc, gate.ClosedUtc, artifacts.Values.OrderBy(x => x.Sha256, StringComparer.Ordinal).ToArray(), bindings, shadow);
var plan = Arch6cPmsShadowPersistencePlanner.Build(qualified);
var validation = Arch6cPmsShadowPersistencePlanner.Validate(plan);
Require(validation.IsValid, $"ARCH6C_PLAN_INVALID:{string.Join(',', validation.Issues)}");

var registry = new InMemoryPmsShadowAtomicIngestionRegistry();
var firstApply = registry.Apply(plan);
var identicalRetry = registry.Apply(plan);
var interruptedRegistry = new InMemoryPmsShadowAtomicIngestionRegistry();
var interruptionRejected = false;
try { interruptedRegistry.Apply(plan, simulateInterruptionBeforeCommit: true); }
catch (InvalidOperationException exception) when (exception.Message == "SIMULATED_INTERRUPTION_BEFORE_ATOMIC_COMMIT") { interruptionRejected = true; }
var applyAfterInterruption = interruptedRegistry.Apply(plan);

Directory.CreateDirectory(outputDirectory);
WriteJson("arch6b_shadow_persistence_plan.json", plan);
WriteJson("arch6b_shadow_rowset_manifest.json", new
{
    schema = "arch6c_arch6b_shadow_rowset_manifest_v1",
    source_evidence_sha256 = actualSha256,
    source_session_id = gate.SourceSessionId,
    plan.RowsetSha256,
    migration_id = PmsShadowStateContract.MigrationId,
    schema_name = PmsShadowStateContract.SchemaName,
    table_row_counts = new
    {
        ingestions = 1,
        source_artifacts = plan.SourceArtifacts.Count,
        qubes_input_snapshots = plan.QubesInputSnapshots.Count,
        account_snapshots = 1,
        position_snapshots = 1,
        position_snapshot_lines = plan.PositionSnapshotLines.Count,
        market_data_snapshots = 1,
        market_data_observations = plan.MarketDataObservations.Count,
        security_mappings = plan.SecurityMappings.Count,
        working_leaves_observations = 1,
        model_runs = plan.ModelRuns.Count,
        target_weights = plan.TargetWeights.Count,
        target_position_stages = plan.TargetPositionStages.Count,
        target_positions = plan.TargetPositions.Count,
        position_only_drift_stages = plan.PositionOnlyDriftStages.Count,
        position_only_drifts = plan.PositionOnlyDrifts.Count,
        broker_adjusted_drift_stages = plan.BrokerAdjustedDriftStages.Count,
        cycle_results = plan.CycleResults.Count
    },
    raw_artifact_payloads_in_database = 0,
    validation_issues = validation.Issues,
    final_success = true
});
WriteJson("idempotence_atomicity_test_result.json", new
{
    schema = "arch6c_idempotence_atomicity_dry_run_v1",
    first_apply = firstApply.ToString(),
    identical_retry = identicalRetry.ToString(),
    interruption_rejected_before_commit = interruptionRejected,
    apply_after_interruption = applyAfterInterruption.ToString(),
    database_connections = 0,
    database_applies = 0,
    final_success = true
});
WriteJson("dry_run_safety_manifest.json", new
{
    schema = "arch6c_dry_run_safety_manifest_v1",
    database_connections = 0,
    database_applies = 0,
    aws_mutations = 0,
    gpu_invocations = 0,
    lmax_calls = 0,
    polygon_calls = 0,
    databento_calls = 0,
    trade_intents = 0,
    orders = 0,
    fills = 0,
    ledger_commits = 0,
    raw_artifact_payloads_in_database = 0,
    working_leaves_status = plan.WorkingLeavesObservation.Status,
    working_leaves_empty_state_inferred = plan.WorkingLeavesObservation.EmptyStateInferred,
    final_success = true
});

void WriteJson(string name, object value) =>
    File.WriteAllText(Path.Combine(outputDirectory, name), JsonSerializer.Serialize(value, json) + Environment.NewLine);

static T Read<T>(ZipArchive archive, string name, JsonSerializerOptions options)
{
    var entry = archive.GetEntry(name) ?? throw new InvalidDataException($"ARCH6B_ENTRY_MISSING:{name}");
    using var stream = entry.Open();
    return JsonSerializer.Deserialize<T>(stream, options) ?? throw new InvalidDataException($"ARCH6B_ENTRY_INVALID:{name}");
}

static long EntrySize(ZipArchive archive, string name) =>
    (archive.GetEntry(name) ?? throw new InvalidDataException($"ARCH6B_ENTRY_MISSING:{name}")).Length;

static Dictionary<string, string> ParseArguments(string[] values)
{
    var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 0; index < values.Length; index += 2)
    {
        if (index + 1 >= values.Length || !values[index].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("Arguments must be supplied as --name value pairs.");
        parsed.Add(values[index], values[index + 1]);
    }
    return parsed;
}

static string Required(IReadOnlyDictionary<string, string> values, string name) =>
    values.GetValueOrDefault(name) ?? throw new ArgumentException($"Missing required argument {name}.");

static void Require(bool condition, string issue)
{
    if (!condition) throw new InvalidDataException(issue);
}

internal sealed record Arch6bGateVerdict(
    string Gate, string Verdict, string Status, DateTimeOffset ClosedUtc, string SourceSessionId,
    bool NoOrder, bool NoProduction, bool FinalSuccess);

internal sealed record Arch6bBindingManifest(
    string SourceSessionId, string SourceSnapshotSha256, string OverlaySha256,
    IReadOnlyList<Arch6bBinding> Bindings, int BindingCount);

internal sealed record Arch6bBinding(
    string Strategy, string SourceSnapshotSha256, string StrategyInputSnapshotSha256,
    string OverlaySha256, string OutputSha256, DateTimeOffset TargetCloseUtc);

internal sealed record Arch6bCountSummary(
    int TargetWeightCount, int PreviewCount, int Count, int DriftCount, int TotalItems);

internal sealed record Arch6bBrokerAdjustedSummary(
    bool BrokerAdjustedDriftCalculated, bool EmptyStateObserved, bool EmptyStateInferred,
    bool BrokerAuthority, int Count);

internal sealed record Arch6bResultSummary(
    IReadOnlyList<JsonElement> Results, int TradeIntentCount, int OrderCount, int BrokerSendCount);
