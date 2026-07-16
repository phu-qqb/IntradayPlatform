using System.Text.Json;
using System.Text.Json.Serialization;
using QQ.Production.Intraday.Application;

var arguments = ParseArguments(args);
var evidenceRoot = Required(arguments, "--arch5a-evidence-root");
var expectedZipSha = Required(arguments, "--expected-evidence-zip-sha256");
var outputDirectory = Required(arguments, "--output-dir");

var loaded = new Arch5bArch5aEvidenceLoader().Load(evidenceRoot, expectedZipSha);
var contractValidation = new Arch5bLineageContractValidator().Validate(loaded.Contract);
if (!contractValidation.IsValid)
{
    throw new InvalidDataException(string.Join(";", contractValidation.Issues));
}

var service = new Arch5bQubesLineagePreviewService();
var first = service.Build(loaded.Contract);
var second = service.Build(loaded.Contract);
if (first.PreviewSha256 != second.PreviewSha256)
{
    throw new InvalidDataException("DETERMINISTIC_PREVIEW_HASH_MISMATCH");
}

var registry = new Arch5bLineagePreviewRegistry();
foreach (var preview in first.Runs)
{
    var registered = registry.Register(preview);
    var repeated = registry.Register(preview);
    if (!ReferenceEquals(registered, repeated))
    {
        throw new InvalidDataException("IDEMPOTENT_PREVIEW_REGISTRATION_FAILED");
    }
}

Directory.CreateDirectory(outputDirectory);
var json = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
};

await WriteJsonAsync("arch5a_evidence_verification.json", loaded.Verification);
await WriteJsonAsync("lineage_contract.json", loaded.Contract);
await WriteJsonAsync("lineage_contract_validation.json", contractValidation);
await WriteJsonAsync("session_lineage_preview.json", first);
foreach (var run in first.Runs)
{
    await WriteJsonAsync(run.Lineage.StrategyId.ToLowerInvariant() + "_lineage_preview.json", new
    {
        run.Lineage,
        run.QubesWeightsOutput.OutputHash,
        run.QubesContractShapeValidForEvidenceOnly,
        run.ModelRun,
        target_weight_count = run.TargetWeights.Count,
        target_weights_sha256 = Arch5bHashing.HashCanonical(run.TargetWeights),
        run.TargetPositions,
        run.DriftSnapshot,
        run.ManualPaperCycle,
        run.R009,
        run.PreviewSha256
    });
}

await WriteJsonAsync("model_run_preview_summary.json", new
{
    count = first.Runs.Count,
    distinct_count = first.Runs.Select(x => x.ModelRun.ModelRunPreviewId).Distinct(StringComparer.Ordinal).Count(),
    previews = first.Runs.Select(x => x.ModelRun).ToArray()
});
await WriteJsonAsync("target_weight_preview_summary.json", new
{
    lineage_count = first.Runs.Count,
    total_weight_count = first.Runs.Sum(x => x.TargetWeights.Count),
    lineages = first.Runs.Select(x => new
    {
        x.Lineage.StrategyId,
        x.Lineage.TargetCloseUtc,
        count = x.TargetWeights.Count,
        ordered = x.TargetWeights.Select(y => y.Order).SequenceEqual(Enumerable.Range(0, x.TargetWeights.Count)),
        sha256 = Arch5bHashing.HashCanonical(x.TargetWeights)
    }).ToArray()
});
await WriteJsonAsync("target_position_preview_summary.json", new
{
    computed = first.Runs.Count(x => x.TargetPositions.ComputationStatus == Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW),
    blocked = first.Runs.Count(x => x.TargetPositions.ComputationStatus != Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW),
    stages = first.Runs.Select(x => new { x.Lineage.StrategyId, x.TargetPositions }).ToArray()
});
await WriteJsonAsync("drift_snapshot_preview_summary.json", new
{
    computed = first.Runs.Count(x => x.DriftSnapshot.ComputationStatus == Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW),
    blocked = first.Runs.Count(x => x.DriftSnapshot.ComputationStatus != Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW),
    stages = first.Runs.Select(x => new { x.Lineage.StrategyId, x.DriftSnapshot }).ToArray()
});
await WriteJsonAsync("missing_canonical_inputs.json", new
{
    market_data_snapshot_status = Arch5bLineageContractVersions.MissingMarketDataSnapshot,
    account_snapshot = "MISSING_CANONICAL_ACCOUNT_SNAPSHOT",
    price_snapshot = "MISSING_CANONICAL_PRICE_SNAPSHOT",
    security_mapping = "MISSING_CANONICAL_SECURITY_MAPPING",
    current_position_snapshot = "MISSING_CANONICAL_CURRENT_POSITION_SNAPSHOT",
    working_leaves_status = Arch5bWorkingLeavesStatus.ABSENT_NOT_ASSUMED_ZERO,
    financial_values_invented = false,
    flat_start_assumed = false
});
await WriteJsonAsync("manual_paper_cycle_result.json", new
{
    status = "CompletedNoExternal",
    results = first.Runs.Select(x => new { x.Lineage.StrategyId, x.ManualPaperCycle }).ToArray()
});
await WriteJsonAsync("r009_no_order_result.json", new
{
    status = "CompletedNoExternal",
    results = first.Runs.Select(x => new { x.Lineage.StrategyId, x.R009 }).ToArray()
});
await WriteJsonAsync("deterministic_hash_validation.json", new
{
    first_preview_sha256 = first.PreviewSha256,
    second_preview_sha256 = second.PreviewSha256,
    equal = first.PreviewSha256 == second.PreviewSha256,
    per_run = first.Runs.Select(x => new { x.Lineage.StrategyId, x.PreviewSha256 }).ToArray()
});
await WriteJsonAsync("idempotency_validation.json", new
{
    repeated_ingestion_count = 2,
    logical_lineage_count = first.Runs.Count,
    duplicate_objects_created = 0,
    same_run_different_sha_policy = "FAIL_CLOSED",
    passed = true
});
await WriteJsonAsync("no_order_no_runtime_manifest.json", new
{
    start_instances = 0,
    stop_instances = 0,
    terminate_instances = 0,
    ssm_commands = 0,
    anubis_invocations = 0,
    prod_anubis_v4_invocations = 0,
    aws_mutations = 0,
    terraform_apply = 0,
    db_apply = 0,
    broker_send = 0,
    fix_order_entry_logon = 0,
    account_api_calls = 0,
    databento_api_calls = 0,
    databento_downloads = 0,
    polygon_downloads = 0,
    lmax_portal_logins = 0,
    order_entry_enabled = false,
    broker_send_status = "DISABLED_NO_ORDER_ENTRY"
});

Console.WriteLine(JsonSerializer.Serialize(new
{
    status = "ARCH5B_OFFLINE_LINEAGE_PREVIEW_GENERATED",
    session_id = first.SourceSessionId,
    run_count = first.Runs.Count,
    target_weight_count = first.Runs.Sum(x => x.TargetWeights.Count),
    target_position_computed = first.Runs.Count(x => x.TargetPositions.ComputationStatus == Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW),
    target_position_blocked = first.Runs.Count(x => x.TargetPositions.ComputationStatus != Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW),
    drift_computed = first.Runs.Count(x => x.DriftSnapshot.ComputationStatus == Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW),
    drift_blocked = first.Runs.Count(x => x.DriftSnapshot.ComputationStatus != Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW),
    preview_sha256 = first.PreviewSha256
}, json));
return 0;

async Task WriteJsonAsync(string fileName, object value)
{
    var path = Path.Combine(outputDirectory, fileName);
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, json) + Environment.NewLine);
}

static Dictionary<string, string> ParseArguments(string[] values)
{
    var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 0; index < values.Length; index += 2)
    {
        if (index + 1 >= values.Length || !values[index].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("Arguments must be supplied as --name value pairs.");
        }
        parsed.Add(values[index], values[index + 1]);
    }
    return parsed;
}

static string Required(IReadOnlyDictionary<string, string> arguments, string name)
    => arguments.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException($"Missing required argument {name}.");
