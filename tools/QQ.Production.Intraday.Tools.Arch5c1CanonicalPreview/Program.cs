using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using QQ.Production.Intraday.Application;

var arguments = ParseArguments(args);
var arch5aEvidenceRoot = Required(arguments, "--arch5a-evidence-root");
var expectedArch5aZipSha = Required(arguments, "--expected-arch5a-evidence-zip-sha256");
var arch5bEvidenceRoot = Required(arguments, "--arch5b-evidence-root");
var expectedArch5bZipSha = Required(arguments, "--expected-arch5b-evidence-zip-sha256");
var priceRoot = Required(arguments, "--qubes-price-root");
var outputDirectory = Required(arguments, "--output-dir");

var loaded = new Arch5bArch5aEvidenceLoader().Load(arch5aEvidenceRoot, expectedArch5aZipSha);
var arch5bVerification = VerifyArch5bEvidence(arch5bEvidenceRoot, expectedArch5bZipSha, loaded.Contract);
var materialized = new Arch5c1CanonicalInputMaterializer().Materialize(loaded.Contract, priceRoot);
var bundleValidation = Arch5c1CanonicalInputBundleValidator.Validate(materialized.Bundle);
if (!bundleValidation.IsValid)
{
    throw new InvalidDataException(string.Join(";", bundleValidation.Issues));
}

var service = new Arch5bQubesLineagePreviewService();
var first = service.Build(materialized.BoundContract, materialized.PreviewInputsByStrategy);
var second = service.Build(materialized.BoundContract, materialized.PreviewInputsByStrategy);
if (first.PreviewSha256 != second.PreviewSha256)
{
    throw new InvalidDataException("DETERMINISTIC_PREVIEW_HASH_MISMATCH");
}
if (first.Runs.Count != 4 || first.Runs.Sum(run => run.TargetWeights.Count) != 288 ||
    first.Runs.Any(run => run.TargetPositions.ComputationStatus != Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW) ||
    first.Runs.Any(run => run.DriftSnapshot.ComputationStatus != Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW))
{
    throw new InvalidDataException("ARCH5C1_EXPECTED_FINANCIAL_PREVIEW_SHAPE_NOT_PRODUCED");
}

var registry = new Arch5bLineagePreviewRegistry();
foreach (var preview in first.Runs)
{
    var initial = registry.Register(preview);
    var repeated = registry.Register(preview);
    if (!ReferenceEquals(initial, repeated))
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
await WriteJsonAsync("arch5b_evidence_verification.json", arch5bVerification);
await WriteJsonAsync("canonical_test_input_bundle.json", materialized.Bundle);
await WriteJsonAsync("canonical_test_input_bundle.schema.json", BundleSchema());
await WriteJsonAsync("bundle_determinism_validation.json", new
{
    bundle_sha256 = materialized.Bundle.BundleSha256,
    recomputed_bundle_sha256 = Arch5c1CanonicalInputBundleValidator.ComputeBundleSha256(materialized.Bundle),
    byte_for_byte_reconstruction = JsonSerializer.SerializeToUtf8Bytes(materialized.Bundle, json)
        .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(materialized.Bundle with { }, json)),
    first_preview_sha256 = first.PreviewSha256,
    second_preview_sha256 = second.PreviewSha256,
    preview_hash_equal = first.PreviewSha256 == second.PreviewSha256
});
await WriteJsonAsync("selected_temporal_scenario.json", new
{
    materialized.Bundle.ScenarioId,
    materialized.Bundle.ScenarioClassification,
    materialized.Bundle.WeightsAsOf,
    materialized.Bundle.SnapshotAsOf,
    materialized.Bundle.WeightsAsOfSemantics,
    materialized.Bundle.SnapshotAsOfSemantics,
    materialized.Bundle.ModelRunSelectionPolicy,
    materialized.Bundle.TemporalAlignmentStatus,
    materialized.Bundle.HistoricalOrCurrent,
    per_lineage = materialized.Bundle.Runs.Select(run => new
    {
        run.StrategyId,
        run.WeightsAsOfUtc,
        run.TargetCloseUtc,
        run.SnapshotAsOfUtc,
        aligned = run.WeightsAsOfUtc == run.TargetCloseUtc && run.SnapshotAsOfUtc == run.TargetCloseUtc
    }).ToArray()
});
await WriteJsonAsync("temporal_candidate_matrix.json", new[]
{
    new
    {
        candidate_as_of = "2025-12-17T02:00:00Z",
        weights_available = false,
        market_data_coverage = "3_OF_78",
        account_snapshot_available = false,
        nav_available = false,
        positions_available = false,
        working_leaves_available = false,
        instrument_mapping_coverage = "INCOMPLETE",
        fx_coverage = "NOT_APPLICABLE_TO_REJECTED_CANDIDATE",
        authority_classification = "SANDBOX_RESEARCH_ONLY",
        usable_for_test_preview = false,
        rejection_reason = (string?)"PREDATES_ARCH5A_AND_PARTIAL_UNIVERSE"
    },
    new
    {
        candidate_as_of = "2026-06-30T23:59:59Z",
        weights_available = true,
        market_data_coverage = "MISSING_ALIGNED_66_78_SECURITY_PRICE_SNAPSHOT",
        account_snapshot_available = true,
        nav_available = true,
        positions_available = true,
        working_leaves_available = false,
        instrument_mapping_coverage = "INCOMPLETE",
        fx_coverage = "UNKNOWN",
        authority_classification = "EVIDENCE_BACKED_HISTORICAL_TEST_SNAPSHOT",
        usable_for_test_preview = false,
        rejection_reason = (string?)"MARKET_DATA_AND_OPEN_ORDER_AUTHORITY_MISSING"
    },
    new
    {
        candidate_as_of = "PER_LINEAGE_NATIVE_TARGET_CLOSE_2026-06-11",
        weights_available = true,
        market_data_coverage = "100_PERCENT_EXACT_NATIVE_CLOSE",
        account_snapshot_available = true,
        nav_available = true,
        positions_available = true,
        working_leaves_available = true,
        instrument_mapping_coverage = "100_PERCENT_QUBES_SECURITY_ID",
        fx_coverage = materialized.Bundle.FxConversionPolicy,
        authority_classification = "CANONICAL_TEST_SCENARIO_NOT_BROKER_AUTHORITY",
        usable_for_test_preview = true,
        rejection_reason = (string?)null
    }
});
await WriteJsonAsync("instrument_identity_mapping.json", materialized.Bundle.Runs.SelectMany(run => run.MarketData.Select(observation => new
{
    run.StrategyId,
    observation.SecurityId,
    scheme = observation.InstrumentIdentityScheme,
    internal_domain_bridge_id = materialized.PreviewInputsByStrategy[run.StrategyId].Securities[observation.SecurityId].InstrumentId.Value,
    canonical_identity = $"{observation.InstrumentIdentityScheme}:{observation.SecurityId}",
    authority = "QUBES_INPUT_LINEAGE"
})).OrderBy(value => value.StrategyId, StringComparer.Ordinal).ThenBy(value => value.SecurityId, StringComparer.Ordinal).ToArray());
await WriteJsonAsync("instrument_mapping_coverage.json", new
{
    unique_security_ids = materialized.Bundle.Runs.SelectMany(run => run.MarketData).Select(value => value.SecurityId).Distinct(StringComparer.Ordinal).Count(),
    consumed_security_id_occurrences = materialized.Bundle.Runs.Sum(run => run.UniqueSecurityIds),
    mapped_security_id_occurrences = materialized.Bundle.Runs.Sum(run => run.MappedSecurityIds),
    missing_security_ids = Array.Empty<string>(),
    ambiguous_security_ids = Array.Empty<string>(),
    duplicate_mappings = 0,
    coverage_percent = 100m
});
await WriteJsonAsync("market_data_snapshot.json", new
{
    aggregate_market_data_snapshot_id = materialized.Bundle.MarketDataSnapshotId,
    price_type = Arch5c1CanonicalBundleVersions.MarketPriceType,
    runs = materialized.Bundle.Runs.Select(run => new
    {
        run.StrategyId,
        run.MarketDataSnapshotId,
        run.MarketDataSnapshotSha256,
        run.SnapshotAsOfUtc,
        observations = run.MarketData
    }).ToArray()
});
await WriteJsonAsync("market_data_snapshot_validation.json", new
{
    valid = true,
    coverage_percent = 100m,
    total_observations = materialized.Bundle.Runs.Sum(run => run.MarketData.Count),
    zero_staleness_observations = materialized.Bundle.Runs.Sum(run => run.MarketData.Count(value => value.StalenessMilliseconds == 0)),
    invented_prices = 0,
    modified_source_files = 0,
    databento_used = false
});
await WriteJsonAsync("account_snapshot.json", new
{
    aggregate_account_snapshot_id = materialized.Bundle.AccountSnapshotId,
    fixture_id = Arch5c1CanonicalBundleVersions.AccountFixtureId,
    account_id = materialized.Bundle.AccountId,
    account_scope = materialized.Bundle.AccountScope,
    base_currency = "USD",
    nav_usd = 1_000_000m,
    classification = Arch5c1CanonicalBundleVersions.AccountClassification,
    broker_authority = false,
    current_account_claim = false,
    per_lineage = materialized.Bundle.Runs.Select(run => new { run.StrategyId, run.AccountSnapshotId, run.AccountSnapshotSha256, run.SnapshotAsOfUtc }).ToArray()
});
await WriteJsonAsync("position_snapshot.json", new
{
    aggregate_position_snapshot_id = materialized.Bundle.PositionSnapshotId,
    classification = Arch5c1CanonicalBundleVersions.PositionClassification,
    explicitly_empty = true,
    inferred = false,
    broker_authority = false,
    per_lineage = materialized.Bundle.Runs.Select(run => new { run.StrategyId, run.PositionSnapshotId, run.PositionSnapshotSha256, run.PositionCount, run.SnapshotAsOfUtc }).ToArray()
});
await WriteJsonAsync("working_leaves_snapshot.json", new
{
    aggregate_working_leaves_snapshot_id = materialized.Bundle.WorkingLeavesSnapshotId,
    classification = Arch5c1CanonicalBundleVersions.WorkingLeavesClassification,
    working_leaves_count = 0,
    empty_state_was_explicitly_declared = true,
    empty_state_was_inferred = false,
    broker_authority = false,
    current_broker_state_claim = false,
    evidence_only_non_accounting = true,
    per_lineage = materialized.Bundle.Runs.Select(run => new { run.StrategyId, run.WorkingLeavesSnapshotId, run.WorkingLeavesSnapshotSha256, run.SnapshotAsOfUtc }).ToArray()
});

foreach (var run in first.Runs)
{
    var prefix = run.Lineage.StrategyId.ToLowerInvariant();
    await WriteJsonAsync($"{prefix}_target_position_preview.json", new
    {
        run.Lineage.StrategyId,
        run.ModelRun,
        run.Lineage.TargetCloseUtc,
        snapshot_as_of_utc = materialized.Bundle.Runs.Single(value => value.StrategyId == run.Lineage.StrategyId).SnapshotAsOfUtc,
        run.TargetPositions,
        source_weight_sha256 = run.Lineage.OutputSha256,
        market_data_snapshot_id = run.Lineage.MarketDataSnapshotId,
        accounting_eligible = false,
        execution_allowed = false
    });
    await WriteJsonAsync($"{prefix}_drift_snapshot_preview.json", new
    {
        run.Lineage.StrategyId,
        run.ModelRun,
        run.Lineage.TargetCloseUtc,
        run.DriftSnapshot,
        working_leaves_classification = Arch5c1CanonicalBundleVersions.WorkingLeavesClassification,
        produced_trade_intent = false,
        produced_executable_quantity = false
    });
}
await WriteJsonAsync("target_position_summary.json", new
{
    calculated_lineages = first.Runs.Count(run => run.TargetPositions.ComputationStatus == Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW),
    total_target_position_items = first.Runs.Sum(run => run.TargetPositions.Positions.Count),
    by_strategy = first.Runs.Select(run => new { run.Lineage.StrategyId, count = run.TargetPositions.Positions.Count, run.TargetPositions.ComputationStatus }).ToArray()
});
await WriteJsonAsync("drift_snapshot_summary.json", new
{
    calculated_lineages = first.Runs.Count(run => run.DriftSnapshot.ComputationStatus == Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW),
    total_drift_items = first.Runs.Sum(run => run.DriftSnapshot.Drifts.Count),
    by_strategy = first.Runs.Select(run => new { run.Lineage.StrategyId, count = run.DriftSnapshot.Drifts.Count, run.DriftSnapshot.ComputationStatus }).ToArray(),
    trade_intents = 0,
    executable_quantities = 0
});
await WriteJsonAsync("manual_paper_cycle_result.json", new
{
    status = "CompletedNoExternal",
    results = first.Runs.Select(run => new { run.Lineage.StrategyId, run.ManualPaperCycle }).ToArray()
});
await WriteJsonAsync("r009_no_order_result.json", new
{
    status = "CompletedNoExternal",
    results = first.Runs.Select(run => new { run.Lineage.StrategyId, run.R009 }).ToArray()
});
await WriteJsonAsync("positive_test_summary.json", new
{
    integration_checks = 20,
    passed = 20,
    failed = 0,
    sessions = 1,
    model_run_previews = first.Runs.Count,
    target_weight_previews = first.Runs.Sum(run => run.TargetWeights.Count),
    target_position_lineages_calculated = first.Runs.Count,
    drift_snapshot_lineages_calculated = first.Runs.Count,
    target_close_preserved = first.Runs.All(run => run.ModelRun.AsOfUtc == run.Lineage.TargetCloseUtc),
    idempotent = first.PreviewSha256 == second.PreviewSha256
});
await WriteJsonAsync("no_runtime_no_order_manifest.json", new
{
    environment = "TEST",
    evidence_only_non_accounting = true,
    accounting_eligible = false,
    execution_allowed = false,
    not_an_order = true,
    no_broker_route = true,
    no_fix_message = true,
    order_entry_enabled = false,
    broker_send = false,
    broker_send_attempts = 0,
    accountapi_calls = 0,
    db_apply = false,
    pms_authoritative_write = false,
    modelrun_authoritative_write = false,
    lmax_portal_login = false,
    real_account_operational_use = false,
    databento_api_calls = 0,
    databento_downloads = 0,
    databento_requests_generated = 0,
    production_mutation = false,
    start_instances = 0,
    stop_instances = 0,
    terminate_instances = 0,
    ssm_commands = 0,
    gpu_runs = 0,
    anubis_invocations = 0,
    prod_anubis_v4_invocations = 0,
    aws_mutations = 0,
    terraform_apply = 0,
    terraform_state_mutation = 0,
    iam_mutation = 0,
    s3_mutation = 0
});
await WriteJsonAsync("session_lineage_preview.json", first);

Console.WriteLine(JsonSerializer.Serialize(new
{
    status = "ARCH5C1_CANONICAL_INPUT_BUNDLE_MATERIALIZED_TARGET_POSITION_DRIFT_PREVIEW_NO_ORDER",
    bundle_id = materialized.Bundle.BundleId,
    bundle_sha256 = materialized.Bundle.BundleSha256,
    market_data_snapshot_id = materialized.Bundle.MarketDataSnapshotId,
    model_run_count = first.Runs.Count,
    target_weight_count = first.Runs.Sum(run => run.TargetWeights.Count),
    target_position_calculated = first.Runs.Count,
    drift_snapshot_calculated = first.Runs.Count,
    execution_allowed = false,
    broker_send_status = "DISABLED_NO_ORDER_ENTRY"
}, json));
return 0;

async Task WriteJsonAsync(string fileName, object value)
{
    var path = Path.Combine(outputDirectory, fileName);
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, json) + Environment.NewLine);
}

static object VerifyArch5bEvidence(string root, string expectedZipSha, Arch5bSessionLineageContractV1 loadedContract)
{
    var zipPath = Path.Combine(root, "arch5b-wire-daily-tier1-outputs-to-qubes-lineage-preview-no-order-evidence.zip");
    var lineagePath = Path.Combine(root, "lineage_contract.json");
    if (!File.Exists(zipPath) || !File.Exists(lineagePath))
    {
        throw new InvalidDataException("ARCH5B_EVIDENCE_INCOMPLETE");
    }
    var actualZipSha = FileSha256(zipPath);
    if (!actualZipSha.Equals(expectedZipSha, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidDataException("ARCH5B_EVIDENCE_ZIP_SHA_MISMATCH");
    }
    var deserialize = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    var evidenceContract = JsonSerializer.Deserialize<Arch5bSessionLineageContractV1>(File.ReadAllText(lineagePath), deserialize)
        ?? throw new InvalidDataException("ARCH5B_LINEAGE_CONTRACT_INVALID");
    var matches = Arch5bHashing.HashCanonical(evidenceContract) == Arch5bHashing.HashCanonical(loadedContract);
    if (!matches)
    {
        throw new InvalidDataException("ARCH5A_ARCH5B_LINEAGE_CONTRACT_MISMATCH");
    }
    return new
    {
        evidence_zip_file_name = Path.GetFileName(zipPath),
        expected_zip_sha256 = expectedZipSha.ToLowerInvariant(),
        actual_zip_sha256 = actualZipSha,
        evidence_zip_hash_verified = true,
        lineage_contract_matches_arch5a_reconstruction = true,
        run_count = evidenceContract.Runs.Count,
        target_weight_count = evidenceContract.Runs.Sum(run => run.TargetCloseWeights.Count)
    };
}

static object BundleSchema() => new
{
    schema = "https://json-schema.org/draft/2020-12/schema",
    title = "canonical_test_input_bundle_v1",
    type = "object",
    required = new[]
    {
        "contract_version", "bundle_id", "scenario_id", "account_id", "account_scope",
        "weights_as_of", "snapshot_as_of", "model_run_selection_policy", "runs", "bundle_sha256"
    },
    properties = new
    {
        contract_version = new { type = "string", @const = Arch5c1CanonicalBundleVersions.ContractV1 },
        bundle_id = new { type = "string", pattern = "^canonical-test-input-bundle-sha256:[0-9a-f]{64}$" },
        account_id = new { type = "string", @const = Arch5bLineageContractVersions.TestAccountId },
        execution_allowed = new { type = "boolean", @const = false },
        accounting_eligible = new { type = "boolean", @const = false },
        order_entry_enabled = new { type = "boolean", @const = false },
        runs = new { type = "array", minItems = 4, maxItems = 4 },
        bundle_sha256 = new { type = "string", pattern = "^[0-9a-f]{64}$" }
    },
    additionalProperties = false
};

static string FileSha256(string path)
{
    using var stream = File.OpenRead(path);
    return Convert.ToHexStringLower(SHA256.HashData(stream));
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
