using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public static class Arch5bLineageContractVersions
{
    public const string LineageV1 = "arch5b_qubes_lineage_preview_v1";
    public const string SourceQubesWeightsOutputV1 = "QubesWeightsOutputV1";
    public const string OutputQubesWeightsOutputV1 = "QubesWeightsOutputV1";
    public const string EvidenceOnlyClassification = "EVIDENCE_ONLY_NON_ACCOUNTING";
    public const string MissingMarketDataSnapshot = "MISSING_CANONICAL_MARKET_DATA_SNAPSHOT";
    public const string TestAccountId = "1754288005";
    public const string TestAccountScope = "LMAX_TEST_EOD_ONLY";
    public const string RealAccountId = "921640160";
}

public enum Arch5bComputationStatus
{
    COMPUTED_CANONICAL_PREVIEW,
    BLOCKED_MISSING_CANONICAL_MARKET_DATA_SNAPSHOT,
    BLOCKED_MISSING_CANONICAL_ACCOUNT_SNAPSHOT,
    BLOCKED_MISSING_CANONICAL_PRICE_SNAPSHOT,
    BLOCKED_MISSING_SECURITY_MAPPING,
    BLOCKED_MISSING_CANONICAL_CURRENT_POSITION,
    BLOCKED_MISSING_CANONICAL_WORKING_LEAVES
}

public enum Arch5bWorkingLeavesStatus
{
    CANONICAL_SNAPSHOT_PRESENT,
    ABSENT_NOT_ASSUMED_ZERO
}

public sealed record Arch5bTargetCloseWeightV1(
    string SecurityId,
    string ExactWeightText,
    double Weight,
    int Order,
    string SourceRowKey,
    string EntrySha256);

public sealed record Arch5bRunLineageContractV1(
    string LineageContractVersion,
    string SourceContract,
    string SourceSessionId,
    string SourceRunId,
    string LogicalRunId,
    string StrategyId,
    decimal BenchmarkParameter,
    string SourceMasterSha,
    string RunnerPackageSha256,
    string BundleArchiveSha256,
    string BundleVersionId,
    string ExecutableSha256,
    string OutputSha256,
    long OutputSizeBytes,
    string OutputRelativePath,
    string OutputContractVersion,
    DateTimeOffset ProducedAtUtc,
    DateTimeOffset OutputAsOfUtc,
    DateTimeOffset TargetCloseUtc,
    string TargetCloseSourceValue,
    string TargetCloseSelectionRule,
    string R083Status,
    int MaterialDifferenceCount,
    int SignFlipCount,
    bool TransferVerified,
    string? MarketDataSnapshotId,
    string? MarketDataSnapshotEvidenceSha256,
    string MarketDataSnapshotStatus,
    string NoOrderClassification,
    bool EvidenceOnlyNonAccounting,
    bool AccountingEligible,
    bool ExecutionAllowed,
    IReadOnlyList<Arch5bTargetCloseWeightV1> TargetCloseWeights);

public sealed record Arch5bSessionLineageContractV1(
    string LineageContractVersion,
    string SourceContract,
    string SourceSessionId,
    string PreviewAccountId,
    string PreviewAccountScope,
    string SourceMasterSha,
    string RunnerPackageSha256,
    string BundleArchiveSha256,
    string BundleVersionId,
    DateTimeOffset PreviewGeneratedAtUtc,
    string NoOrderClassification,
    bool EvidenceOnlyNonAccounting,
    bool AccountingEligible,
    bool ExecutionAllowed,
    IReadOnlyList<Arch5bRunLineageContractV1> Runs);

public sealed record Arch5bLineageContractValidationResult(
    bool IsValid,
    IReadOnlyList<string> Issues);

public sealed record Arch5bModelRunPreview(
    string ModelRunPreviewId,
    string SourceDomainModel,
    string SourceSessionId,
    string SourceRunId,
    string LogicalRunId,
    string StrategyId,
    DateTimeOffset AsOfUtc,
    DateTimeOffset EffectiveAtUtc,
    string InputHash,
    string ContractVersion,
    string PersistenceStatus,
    bool AccountingEligible,
    bool ExecutionAllowed);

public sealed record Arch5bTargetWeightPreview(
    string ModelRunPreviewId,
    string SourceDomainModel,
    string SecurityId,
    string? InstrumentId,
    string? Symbol,
    double Weight,
    string ExactWeightText,
    int Order,
    DateTimeOffset TargetCloseUtc,
    string SourceRowKey,
    string SourceOutputSha256,
    string MappingStatus,
    bool AccountingEligible,
    bool ExecutionAllowed);

public sealed record Arch5bTargetPositionPreviewItem(
    string ModelRunPreviewId,
    string InstrumentId,
    string Symbol,
    decimal TargetNotionalUsd,
    decimal TargetBaseQuantity,
    decimal TargetVenueQuantity);

public sealed record Arch5bTargetPositionPreviewStage(
    Arch5bComputationStatus ComputationStatus,
    IReadOnlyList<Arch5bComputationStatus> BlockingReasons,
    IReadOnlyList<Arch5bTargetPositionPreviewItem> Positions,
    bool UsedCanonicalInputs,
    bool AccountingEligible,
    bool ExecutionAllowed);

public sealed record Arch5bDriftSnapshotPreviewItem(
    string ModelRunPreviewId,
    string InstrumentId,
    string Symbol,
    decimal TargetBaseQuantity,
    decimal CurrentBaseQuantity,
    decimal SignedReservedWorkingLeaves,
    decimal RemainingDeltaBaseQuantity);

public sealed record Arch5bDriftSnapshotPreviewStage(
    Arch5bComputationStatus ComputationStatus,
    Arch5bWorkingLeavesStatus WorkingLeavesStatus,
    IReadOnlyList<Arch5bComputationStatus> BlockingReasons,
    IReadOnlyList<Arch5bDriftSnapshotPreviewItem> Drifts,
    bool UsedCanonicalInputs,
    bool ProducedTradeIntent,
    bool ProducedExecutableQuantity,
    bool AccountingEligible,
    bool ExecutionAllowed);

public sealed record Arch5bManualPaperCycleIntegrationResult(
    ManualPaperCycleCliStatus Status,
    string IntegrationMode,
    bool EconomicCycleExecuted,
    bool CompletedNoExternal,
    bool CreatedOrder,
    bool CreatedFill,
    bool CreatedExecutionReport,
    bool CreatedRoute,
    bool SubmittedOrder,
    bool UsedBrokerOrLiveInput,
    bool MutatedAuthoritativeState);

public sealed record Arch5bR009NoOrderIntegrationResult(
    string Status,
    int ExecutionIntentCount,
    int DecisionPreviewCount,
    bool ExecutionAllowed,
    bool NotAnOrder,
    bool NoBrokerRoute,
    bool NoFixMessage,
    bool OrderEntryEnabled,
    string BrokerSendStatus,
    bool NoPaperLedgerCommit);

public sealed record Arch5bRunLineagePreview(
    Arch5bRunLineageContractV1 Lineage,
    QubesWeightsOutputManifest QubesWeightsOutput,
    QubesWeightsOutputValidationResult QubesProductionAccountingValidation,
    bool QubesContractShapeValidForEvidenceOnly,
    Arch5bModelRunPreview ModelRun,
    IReadOnlyList<Arch5bTargetWeightPreview> TargetWeights,
    Arch5bTargetPositionPreviewStage TargetPositions,
    Arch5bDriftSnapshotPreviewStage DriftSnapshot,
    Arch5bManualPaperCycleIntegrationResult ManualPaperCycle,
    Arch5bR009NoOrderIntegrationResult R009,
    string PreviewSha256);

public sealed record Arch5bSessionLineagePreview(
    string LineageContractVersion,
    string SourceSessionId,
    IReadOnlyList<Arch5bRunLineagePreview> Runs,
    bool AccountingEligible,
    bool ExecutionAllowed,
    bool FourIndependentLineages,
    bool CrossStrategyAggregationUsed,
    string PreviewSha256);

public sealed record Arch5bCanonicalSecurityPreviewInput(
    string SecurityId,
    InstrumentId InstrumentId,
    string Symbol,
    MarketDataSnapshot MarketData,
    VenueInstrumentMapping VenueMapping,
    decimal CurrentBaseQuantity,
    decimal SignedReservedWorkingLeaves,
    string PositionSnapshotSha256,
    string WorkingLeavesSnapshotSha256);

public sealed record Arch5bCanonicalPreviewInputs(
    string AccountId,
    string AccountScope,
    FundId FundId,
    decimal NavUsd,
    string AccountSnapshotSha256,
    MarketDataSnapshotId MarketDataSnapshotId,
    DateTimeOffset AsOfUtc,
    IReadOnlyDictionary<string, Arch5bCanonicalSecurityPreviewInput> Securities);

public sealed record Arch5bParsedWeightsMatrix(
    int DataRowCount,
    int SecurityIdCount,
    DateTimeOffset TargetCloseUtc,
    string TargetCloseSourceValue,
    IReadOnlyList<Arch5bTargetCloseWeightV1> TargetCloseWeights);

public sealed class Arch5bAggregatedWeightsParser
{
    public Arch5bParsedWeightsMatrix Parse(string path, string outputSha256)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException("QUBES_OUTPUT_MISSING");
        }

        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var header = reader.ReadLine() ?? throw new InvalidDataException("QUBES_OUTPUT_HEADER_MISSING");
        var headerParts = header.Split(';');
        if (headerParts.Length < 2 || headerParts[0].Length != 0)
        {
            throw new InvalidDataException("QUBES_OUTPUT_HEADER_MALFORMED");
        }

        var securityIds = headerParts.Skip(1).Select(x => x.Trim()).ToArray();
        if (securityIds.Any(string.IsNullOrWhiteSpace) ||
            securityIds.Any(x => !int.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out _)) ||
            securityIds.Distinct(StringComparer.Ordinal).Count() != securityIds.Length)
        {
            throw new InvalidDataException("QUBES_OUTPUT_SECURITY_ID_AMBIGUOUS");
        }

        string? line;
        string[]? lastParts = null;
        DateTimeOffset? previousTimestamp = null;
        var rowCount = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                throw new InvalidDataException("QUBES_OUTPUT_BLANK_DATA_ROW");
            }

            var parts = line.Split(';');
            if (parts.Length != securityIds.Length + 1)
            {
                throw new InvalidDataException("QUBES_OUTPUT_ROW_SHAPE_MALFORMED");
            }

            var timestamp = ParseTimestamp(parts[0]);
            if (previousTimestamp is not null && timestamp <= previousTimestamp.Value)
            {
                throw new InvalidDataException("QUBES_OUTPUT_TIMESTAMP_ORDER_INVALID");
            }

            for (var index = 1; index < parts.Length; index++)
            {
                if (!double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var weight) || !double.IsFinite(weight))
                {
                    throw new InvalidDataException("QUBES_OUTPUT_NON_FINITE_OR_INVALID_WEIGHT");
                }
            }

            previousTimestamp = timestamp;
            lastParts = parts;
            rowCount++;
        }

        if (lastParts is null || previousTimestamp is null)
        {
            throw new InvalidDataException("QUBES_OUTPUT_DATA_ROWS_MISSING");
        }

        var targetCloseSource = lastParts[0];
        var weights = securityIds.Select((securityId, index) =>
        {
            var exact = lastParts[index + 1];
            var weight = double.Parse(exact, NumberStyles.Float, CultureInfo.InvariantCulture);
            var rowKey = $"{targetCloseSource}:{securityId}";
            var entryHash = Arch5bHashing.Sha256Hex($"{outputSha256}:{rowKey}:{index}:{exact}");
            return new Arch5bTargetCloseWeightV1(securityId, exact, weight, index, rowKey, entryHash);
        }).ToArray();

        return new Arch5bParsedWeightsMatrix(rowCount, securityIds.Length, previousTimestamp.Value, targetCloseSource, weights);
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        if (!DateTime.TryParseExact(value, "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw new InvalidDataException("QUBES_OUTPUT_TIMESTAMP_MALFORMED");
        }

        return new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Utc));
    }
}

public sealed class Arch5bLineageContractValidator
{
    private static readonly IReadOnlyDictionary<string, decimal> ExpectedStrategies = new Dictionary<string, decimal>(StringComparer.Ordinal)
    {
        ["INFX7"] = 4.5m,
        ["INFX8"] = 2.1m,
        ["INFX9"] = 1.4m,
        ["INFX10"] = 0.6m
    };

    public Arch5bLineageContractValidationResult Validate(Arch5bSessionLineageContractV1 contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var issues = new List<string>();

        Require(contract.LineageContractVersion == Arch5bLineageContractVersions.LineageV1, "UNKNOWN_LINEAGE_CONTRACT_VERSION", issues);
        Require(contract.SourceContract == Arch5bLineageContractVersions.SourceQubesWeightsOutputV1, "UNKNOWN_SOURCE_CONTRACT_VERSION", issues);
        Require(!string.IsNullOrWhiteSpace(contract.SourceSessionId), "SOURCE_SESSION_ID_MISSING", issues);
        Require(contract.PreviewAccountId == Arch5bLineageContractVersions.TestAccountId, "REAL_OR_UNAPPROVED_ACCOUNT_REJECTED", issues);
        Require(contract.PreviewAccountScope == Arch5bLineageContractVersions.TestAccountScope, "TEST_ACCOUNT_SCOPE_INVALID", issues);
        Require(contract.PreviewAccountId != Arch5bLineageContractVersions.RealAccountId, "REAL_ACCOUNT_OPERATIONAL_USE_REJECTED", issues);
        Require(IsGitCommit(contract.SourceMasterSha), "SOURCE_MASTER_SHA_INVALID", issues);
        Require(IsSha256(contract.RunnerPackageSha256), "RUNNER_PACKAGE_SHA_INVALID", issues);
        Require(IsSha256(contract.BundleArchiveSha256), "BUNDLE_ARCHIVE_SHA_INVALID", issues);
        Require(!string.IsNullOrWhiteSpace(contract.BundleVersionId), "BUNDLE_VERSION_ID_MISSING", issues);
        Require(contract.NoOrderClassification == Arch5bLineageContractVersions.EvidenceOnlyClassification, "LINEAGE_CLASSIFICATION_INVALID", issues);
        Require(contract.EvidenceOnlyNonAccounting && !contract.AccountingEligible && !contract.ExecutionAllowed, "ACCOUNTING_OR_EXECUTION_MUST_BE_DISABLED", issues);
        Require(contract.Runs.Count == 4, "FOUR_RUNS_REQUIRED", issues);
        Require(contract.Runs.Select(x => x.StrategyId).Distinct(StringComparer.Ordinal).Count() == contract.Runs.Count, "DUPLICATE_STRATEGY_LINEAGE", issues);
        Require(contract.Runs.Select(x => x.LogicalRunId).Distinct(StringComparer.Ordinal).Count() == contract.Runs.Count, "DUPLICATE_LOGICAL_RUN_ID", issues);
        Require(contract.Runs.Select(x => $"{x.SourceRunId}|{x.StrategyId}").Distinct(StringComparer.Ordinal).Count() == contract.Runs.Count, "DUPLICATE_SOURCE_RUN_STRATEGY_IDENTITY", issues);
        Require(contract.Runs.Select(x => x.StrategyId).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(ExpectedStrategies.Keys.OrderBy(x => x, StringComparer.Ordinal)), "STRATEGY_SET_INVALID", issues);

        foreach (var run in contract.Runs)
        {
            ValidateRun(contract, run, issues);
        }

        return new Arch5bLineageContractValidationResult(issues.Count == 0, issues.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static void ValidateRun(Arch5bSessionLineageContractV1 session, Arch5bRunLineageContractV1 run, List<string> issues)
    {
        Require(run.LineageContractVersion == session.LineageContractVersion, "RUN_LINEAGE_CONTRACT_VERSION_MISMATCH", issues);
        Require(run.SourceContract == session.SourceContract, "RUN_SOURCE_CONTRACT_MISMATCH", issues);
        Require(run.SourceSessionId == session.SourceSessionId, "RUN_SESSION_ID_MISMATCH", issues);
        Require(!string.IsNullOrWhiteSpace(run.SourceRunId), "RUN_ID_MISSING", issues);
        Require(!string.IsNullOrWhiteSpace(run.LogicalRunId), "LOGICAL_RUN_ID_MISSING", issues);
        Require(ExpectedStrategies.TryGetValue(run.StrategyId, out var benchmark) && run.BenchmarkParameter == benchmark, "BENCHMARK_PARAMETER_DIVERGENT", issues);
        Require(run.SourceMasterSha == session.SourceMasterSha, "RUN_MASTER_SHA_MISMATCH", issues);
        Require(run.RunnerPackageSha256 == session.RunnerPackageSha256, "RUNNER_PACKAGE_SHA_MISMATCH", issues);
        Require(run.BundleArchiveSha256 == session.BundleArchiveSha256, "RUN_BUNDLE_SHA_MISMATCH", issues);
        Require(run.BundleVersionId == session.BundleVersionId, "RUN_BUNDLE_VERSION_MISMATCH", issues);
        Require(IsSha256(run.ExecutableSha256), "EXECUTABLE_SHA_INVALID", issues);
        Require(IsSha256(run.OutputSha256), "OUTPUT_SHA_INVALID", issues);
        Require(run.OutputSizeBytes > 0, "OUTPUT_SIZE_INVALID", issues);
        Require(run.OutputRelativePath == $"outputs/{run.StrategyId}/AggregatedWeights.txt", "OUTPUT_RELATIVE_PATH_INVALID", issues);
        Require(run.OutputContractVersion == Arch5bLineageContractVersions.OutputQubesWeightsOutputV1, "OUTPUT_CONTRACT_VERSION_UNKNOWN", issues);
        Require(run.ProducedAtUtc.Offset == TimeSpan.Zero, "PRODUCED_AT_NOT_UTC", issues);
        Require(run.OutputAsOfUtc == run.TargetCloseUtc, "OUTPUT_AS_OF_TARGET_CLOSE_MISMATCH", issues);
        Require(run.TargetCloseUtc.Offset == TimeSpan.Zero && !string.IsNullOrWhiteSpace(run.TargetCloseSourceValue), "TARGET_CLOSE_MISSING_OR_INVALID", issues);
        Require(run.TargetCloseSelectionRule == "PRODMANAGERV4_LAST_CHRONOLOGICAL_DATA_ROW", "TARGET_CLOSE_SELECTION_RULE_INVALID", issues);
        Require(run.R083Status == "PASS", "R083_NOT_PASSED", issues);
        Require(run.MaterialDifferenceCount == 0, "R083_MATERIAL_DIFFERENCE_NONZERO", issues);
        Require(run.SignFlipCount == 0, "R083_SIGN_FLIP_NONZERO", issues);
        Require(run.TransferVerified, "TRANSFER_INCOMPLETE", issues);
        if (run.MarketDataSnapshotId is null)
        {
            Require(run.MarketDataSnapshotEvidenceSha256 is null, "MARKET_DATA_SNAPSHOT_PROOF_WITHOUT_ID", issues);
            Require(run.MarketDataSnapshotStatus == Arch5bLineageContractVersions.MissingMarketDataSnapshot, "MARKET_DATA_SNAPSHOT_STATUS_INVALID", issues);
        }
        else
        {
            Require(Guid.TryParse(run.MarketDataSnapshotId, out _), "MARKET_DATA_SNAPSHOT_ID_INVALID", issues);
            Require(IsSha256(run.MarketDataSnapshotEvidenceSha256), "MARKET_DATA_SNAPSHOT_ID_UNPROVEN", issues);
            Require(run.MarketDataSnapshotStatus == "CANONICAL_MARKET_DATA_SNAPSHOT_PRESENT", "MARKET_DATA_SNAPSHOT_STATUS_INVALID", issues);
        }
        Require(run.NoOrderClassification == session.NoOrderClassification, "RUN_NO_ORDER_CLASSIFICATION_MISMATCH", issues);
        Require(run.EvidenceOnlyNonAccounting && !run.AccountingEligible && !run.ExecutionAllowed, "RUN_ACCOUNTING_OR_EXECUTION_MUST_BE_DISABLED", issues);
        Require(run.TargetCloseWeights.Count > 0, "TARGET_WEIGHTS_MISSING", issues);
        Require(run.TargetCloseWeights.Select(x => x.SecurityId).Distinct(StringComparer.Ordinal).Count() == run.TargetCloseWeights.Count, "SECURITY_ID_AMBIGUOUS", issues);
        Require(run.TargetCloseWeights.Select(x => x.Order).OrderBy(x => x).SequenceEqual(Enumerable.Range(0, run.TargetCloseWeights.Count)), "WEIGHT_ORDER_NON_DETERMINISTIC", issues);
        Require(run.TargetCloseWeights.All(x => double.IsFinite(x.Weight)), "NON_FINITE_WEIGHT", issues);
        Require(run.TargetCloseWeights.All(x => x.SourceRowKey.StartsWith(run.TargetCloseSourceValue + ":", StringComparison.Ordinal)), "WEIGHT_SOURCE_ROW_KEY_INVALID", issues);
        Require(run.TargetCloseWeights.All(x => IsSha256(x.EntrySha256)), "WEIGHT_ENTRY_HASH_INVALID", issues);
    }

    private static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool IsGitCommit(string? value)
        => value is not null && value.Length is 40 or 64 && value.All(Uri.IsHexDigit);

    private static void Require(bool condition, string issue, List<string> issues)
    {
        if (!condition)
        {
            issues.Add(issue);
        }
    }
}

public sealed class Arch5bQubesLineagePreviewService
{
    public Arch5bSessionLineagePreview Build(
        Arch5bSessionLineageContractV1 contract,
        Arch5bCanonicalPreviewInputs? canonicalInputs = null)
    {
        if (canonicalInputs is not null)
        {
            ValidateCanonicalInputs(contract.Runs, canonicalInputs);
        }

        return BuildCore(contract, _ => canonicalInputs);
    }

    public Arch5bSessionLineagePreview Build(
        Arch5bSessionLineageContractV1 contract,
        IReadOnlyDictionary<string, Arch5bCanonicalPreviewInputs> canonicalInputsByStrategy)
    {
        ArgumentNullException.ThrowIfNull(canonicalInputsByStrategy);
        var expectedStrategies = contract.Runs.Select(run => run.StrategyId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var suppliedStrategies = canonicalInputsByStrategy.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (!expectedStrategies.SequenceEqual(suppliedStrategies, StringComparer.Ordinal))
        {
            throw new InvalidDataException("CANONICAL_RUN_INPUT_SET_MISMATCH");
        }

        foreach (var run in contract.Runs)
        {
            ValidateCanonicalInputs([run], canonicalInputsByStrategy[run.StrategyId]);
        }

        return BuildCore(contract, run => canonicalInputsByStrategy[run.StrategyId]);
    }

    private static Arch5bSessionLineagePreview BuildCore(
        Arch5bSessionLineageContractV1 contract,
        Func<Arch5bRunLineageContractV1, Arch5bCanonicalPreviewInputs?> resolveCanonicalInputs)
    {
        var validation = new Arch5bLineageContractValidator().Validate(contract);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join(";", validation.Issues));
        }

        var runPreviews = contract.Runs
            .OrderBy(x => x.StrategyId, StringComparer.Ordinal)
            .Select(run => BuildRun(contract, run, resolveCanonicalInputs(run)))
            .ToArray();

        var sessionHash = Arch5bHashing.HashCanonical(new
        {
            contract.LineageContractVersion,
            contract.SourceSessionId,
            RunHashes = runPreviews.Select(x => x.PreviewSha256).ToArray(),
            AccountingEligible = false,
            ExecutionAllowed = false,
            FourIndependentLineages = true,
            CrossStrategyAggregationUsed = false
        });

        return new Arch5bSessionLineagePreview(
            contract.LineageContractVersion,
            contract.SourceSessionId,
            runPreviews,
            AccountingEligible: false,
            ExecutionAllowed: false,
            FourIndependentLineages: true,
            CrossStrategyAggregationUsed: false,
            sessionHash);
    }

    private static Arch5bRunLineagePreview BuildRun(
        Arch5bSessionLineageContractV1 session,
        Arch5bRunLineageContractV1 run,
        Arch5bCanonicalPreviewInputs? canonicalInputs)
    {
        var output = new QubesWeightsOutputManifest(
            QubesWeightsOutputId: $"arch5b:{run.LogicalRunId}:{run.OutputSha256}",
            QubesRunId: run.LogicalRunId,
            QubesInputSnapshotId: $"arch5a-bundle-sha256:{run.BundleArchiveSha256}",
            MarketDataSnapshotId: run.MarketDataSnapshotId is null
                ? null
                : new MarketDataSnapshotId(Guid.Parse(run.MarketDataSnapshotId)),
            EngineId: $"PRODAnubisV4:{run.ExecutableSha256}",
            OutputContractVersion: run.OutputContractVersion,
            OutputHash: run.OutputSha256,
            ProducedAtUtc: run.ProducedAtUtc,
            EngineKind: QubesEngineKind.RealEngine,
            WeightEntries: run.TargetCloseWeights.Select(x => new QubesWeightEntry(x.SecurityId, x.Weight, x.Order, x.EntrySha256)).ToArray(),
            NativeArtifactRefs: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["bundle_archive_sha256"] = run.BundleArchiveSha256,
                ["output_relative_path"] = run.OutputRelativePath,
                ["source_master_sha"] = run.SourceMasterSha,
                ["source_session_id"] = run.SourceSessionId,
                ["source_run_id"] = run.SourceRunId,
                ["strategy_id"] = run.StrategyId,
                ["target_close"] = run.TargetCloseSourceValue
            });
        var outputValidation = new QubesWeightsOutputManifestValidator().Validate(output);
        var expectedEvidenceOnlyShape = outputValidation.IsValid ||
            (!outputValidation.IsValid &&
             outputValidation.Issues.Count == 1 &&
             outputValidation.Issues[0].Code == QubesWeightsOutputIssueCode.MissingMarketDataSnapshotId);
        if (!expectedEvidenceOnlyShape)
        {
            throw new InvalidDataException("QUBES_OUTPUT_CONTRACT_SHAPE_INVALID");
        }

        var modelRunId = $"model-run-preview-sha256:{Arch5bHashing.Sha256Hex($"{run.SourceSessionId}|{run.SourceRunId}|{run.StrategyId}|{run.OutputSha256}")}";
        var modelRun = new Arch5bModelRunPreview(
            modelRunId,
            SourceDomainModel: nameof(ModelRun),
            run.SourceSessionId,
            run.SourceRunId,
            run.LogicalRunId,
            run.StrategyId,
            run.OutputAsOfUtc,
            run.TargetCloseUtc,
            run.OutputSha256,
            run.OutputContractVersion,
            PersistenceStatus: "NOT_PERSISTED_PREVIEW_ONLY",
            AccountingEligible: false,
            ExecutionAllowed: false);

        var targetWeights = run.TargetCloseWeights.Select(weight => new Arch5bTargetWeightPreview(
            modelRunId,
            SourceDomainModel: nameof(TargetWeight),
            weight.SecurityId,
            canonicalInputs?.Securities.GetValueOrDefault(weight.SecurityId)?.InstrumentId.Value.ToString("D"),
            canonicalInputs?.Securities.GetValueOrDefault(weight.SecurityId)?.Symbol,
            weight.Weight,
            weight.ExactWeightText,
            weight.Order,
            run.TargetCloseUtc,
            weight.SourceRowKey,
            run.OutputSha256,
            canonicalInputs?.Securities.ContainsKey(weight.SecurityId) == true ? "CANONICAL_SECURITY_MAPPING_PRESENT" : "BLOCKED_MISSING_SECURITY_MAPPING",
            AccountingEligible: false,
            ExecutionAllowed: false)).ToArray();

        var stages = BuildFinancialStages(run, modelRunId, canonicalInputs);
        var manualPaper = BuildManualPaperCycleIntegration(stages.TargetPositions, stages.Drift);
        var r009 = BuildR009Integration(stages.Drift);
        var previewHash = Arch5bHashing.HashCanonical(new
        {
            Lineage = run,
            ModelRun = modelRun,
            TargetWeights = targetWeights,
            stages.TargetPositions,
            stages.Drift,
            ManualPaperCycle = manualPaper,
            R009 = r009
        });

        return new Arch5bRunLineagePreview(
            run,
            output,
            outputValidation,
            expectedEvidenceOnlyShape,
            modelRun,
            targetWeights,
            stages.TargetPositions,
            stages.Drift,
            manualPaper,
            r009,
            previewHash);
    }

    private static (Arch5bTargetPositionPreviewStage TargetPositions, Arch5bDriftSnapshotPreviewStage Drift) BuildFinancialStages(
        Arch5bRunLineageContractV1 run,
        string modelRunPreviewId,
        Arch5bCanonicalPreviewInputs? canonicalInputs)
    {
        var positionReasons = ResolvePositionBlockingReasons(run, canonicalInputs);
        var driftReasons = ResolveDriftBlockingReasons(positionReasons, canonicalInputs);
        if (positionReasons.Count > 0 || canonicalInputs is null)
        {
            var positionStatus = positionReasons.FirstOrDefault(Arch5bComputationStatus.BLOCKED_MISSING_CANONICAL_ACCOUNT_SNAPSHOT);
            var driftStatus = driftReasons.FirstOrDefault(Arch5bComputationStatus.BLOCKED_MISSING_CANONICAL_WORKING_LEAVES);
            return (
                new Arch5bTargetPositionPreviewStage(positionStatus, positionReasons, [], false, false, false),
                new Arch5bDriftSnapshotPreviewStage(
                    driftStatus,
                    Arch5bWorkingLeavesStatus.ABSENT_NOT_ASSUMED_ZERO,
                    driftReasons,
                    [],
                    false,
                    ProducedTradeIntent: false,
                    ProducedExecutableQuantity: false,
                    AccountingEligible: false,
                    ExecutionAllowed: false));
        }

        var modelRunId = new ModelRunId(Arch5bHashing.GuidFromSha256(modelRunPreviewId));
        var domainRun = new ModelRun(
            modelRunId,
            canonicalInputs.FundId,
            run.StrategyId,
            run.OutputAsOfUtc,
            canonicalInputs.AsOfUtc,
            run.TargetCloseUtc,
            15,
            canonicalInputs.NavUsd,
            ModelRunStatus.Received,
            run.OutputSha256,
            run.OutputRelativePath,
            IsProcessed: false,
            TargetQuantityMode.PortfolioBaseCurrencyNotional);
        var calculator = new TargetPositionCalculator();
        var positionItems = new List<Arch5bTargetPositionPreviewItem>();
        var driftItems = new List<Arch5bDriftSnapshotPreviewItem>();

        foreach (var weight in run.TargetCloseWeights.OrderBy(x => x.Order))
        {
            var input = canonicalInputs.Securities[weight.SecurityId];
            var domainWeight = new TargetWeight(modelRunId, input.InstrumentId, checked((decimal)weight.Weight), weight.SecurityId);
            var target = calculator.Calculate(domainRun, domainWeight, input.MarketData, input.VenueMapping);
            positionItems.Add(new Arch5bTargetPositionPreviewItem(
                modelRunPreviewId,
                input.InstrumentId.Value.ToString("D"),
                input.Symbol,
                target.TargetNotionalUsd,
                target.TargetBaseQuantity,
                target.TargetVenueQuantity));
            if (driftReasons.Count == 0)
            {
                driftItems.Add(new Arch5bDriftSnapshotPreviewItem(
                    modelRunPreviewId,
                    input.InstrumentId.Value.ToString("D"),
                    input.Symbol,
                    target.TargetBaseQuantity,
                    input.CurrentBaseQuantity,
                    input.SignedReservedWorkingLeaves,
                    target.TargetBaseQuantity - input.CurrentBaseQuantity - input.SignedReservedWorkingLeaves));
            }
        }

        var targetPositionStage = new Arch5bTargetPositionPreviewStage(
            Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW,
            [],
            positionItems,
            true,
            false,
            false);
        if (driftReasons.Count > 0)
        {
            return (
                targetPositionStage,
                new Arch5bDriftSnapshotPreviewStage(
                    driftReasons[0],
                    Arch5bWorkingLeavesStatus.ABSENT_NOT_ASSUMED_ZERO,
                    driftReasons,
                    [],
                    false,
                    ProducedTradeIntent: false,
                    ProducedExecutableQuantity: false,
                    AccountingEligible: false,
                    ExecutionAllowed: false));
        }

        return (
            targetPositionStage,
            new Arch5bDriftSnapshotPreviewStage(
                Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW,
                Arch5bWorkingLeavesStatus.CANONICAL_SNAPSHOT_PRESENT,
                [],
                driftItems,
                true,
                ProducedTradeIntent: false,
                ProducedExecutableQuantity: false,
                AccountingEligible: false,
                ExecutionAllowed: false));
    }

    private static IReadOnlyList<Arch5bComputationStatus> ResolvePositionBlockingReasons(
        Arch5bRunLineageContractV1 run,
        Arch5bCanonicalPreviewInputs? inputs)
    {
        var reasons = new List<Arch5bComputationStatus>();
        if (run.MarketDataSnapshotId is null)
        {
            reasons.Add(Arch5bComputationStatus.BLOCKED_MISSING_CANONICAL_MARKET_DATA_SNAPSHOT);
        }
        if (inputs is null || string.IsNullOrWhiteSpace(inputs.AccountSnapshotSha256) || inputs.NavUsd <= 0)
        {
            reasons.Add(Arch5bComputationStatus.BLOCKED_MISSING_CANONICAL_ACCOUNT_SNAPSHOT);
        }
        if (inputs is null || run.TargetCloseWeights.Any(x => !inputs.Securities.ContainsKey(x.SecurityId)))
        {
            reasons.Add(Arch5bComputationStatus.BLOCKED_MISSING_SECURITY_MAPPING);
        }
        if (inputs is null || run.TargetCloseWeights.Any(x => !inputs.Securities.TryGetValue(x.SecurityId, out var value) || value.MarketData.Id != inputs.MarketDataSnapshotId))
        {
            reasons.Add(Arch5bComputationStatus.BLOCKED_MISSING_CANONICAL_PRICE_SNAPSHOT);
        }
        if (inputs is null || run.TargetCloseWeights.Any(x => !inputs.Securities.TryGetValue(x.SecurityId, out var value) || string.IsNullOrWhiteSpace(value.PositionSnapshotSha256)))
        {
            reasons.Add(Arch5bComputationStatus.BLOCKED_MISSING_CANONICAL_CURRENT_POSITION);
        }
        return reasons;
    }

    private static IReadOnlyList<Arch5bComputationStatus> ResolveDriftBlockingReasons(
        IReadOnlyList<Arch5bComputationStatus> positionReasons,
        Arch5bCanonicalPreviewInputs? inputs)
    {
        var reasons = positionReasons.ToList();
        if (inputs is null || inputs.Securities.Values.Any(x => string.IsNullOrWhiteSpace(x.WorkingLeavesSnapshotSha256)))
        {
            reasons.Add(Arch5bComputationStatus.BLOCKED_MISSING_CANONICAL_WORKING_LEAVES);
        }
        return reasons.Distinct().ToArray();
    }

    private static Arch5bManualPaperCycleIntegrationResult BuildManualPaperCycleIntegration(
        Arch5bTargetPositionPreviewStage targetPositions,
        Arch5bDriftSnapshotPreviewStage drift)
    {
        var contract = ManualPaperCycleCliSurface.Contract;
        var safe = contract.NoOrders && contract.NoFills && contract.NoRoutes && contract.NoSubmissions &&
            targetPositions.ExecutionAllowed == false && drift.ExecutionAllowed == false && !drift.ProducedTradeIntent;
        if (!safe)
        {
            throw new InvalidDataException("MANUAL_PAPER_CYCLE_NO_ORDER_CONTRACT_REGRESSION");
        }
        return new Arch5bManualPaperCycleIntegrationResult(
            ManualPaperCycleCliStatus.CompletedNoExternal,
            IntegrationMode: "EVIDENCE_ONLY_LINEAGE_PREVIEW_BLOCKED_BEFORE_ECONOMIC_CYCLE",
            EconomicCycleExecuted: false,
            CompletedNoExternal: true,
            CreatedOrder: false,
            CreatedFill: false,
            CreatedExecutionReport: false,
            CreatedRoute: false,
            SubmittedOrder: false,
            UsedBrokerOrLiveInput: false,
            MutatedAuthoritativeState: false);
    }

    private static Arch5bR009NoOrderIntegrationResult BuildR009Integration(Arch5bDriftSnapshotPreviewStage drift)
    {
        var flags = R009LiveFeatureFlags.DisabledDefaults;
        var boundary = R009DisabledBoundaryGuard.Disabled;
        var safe = flags.R009DryRunOnly && !flags.R009LiveTradingEnabled && !flags.R009BrokerRoutingEnabled &&
            !flags.R009OrderSubmissionEnabled && !boundary.OrderCreationAllowed && !boundary.SubmissionAllowed &&
            !boundary.FillCreationAllowed && !drift.ProducedTradeIntent;
        if (!safe)
        {
            throw new InvalidDataException("R009_NO_ORDER_CONTRACT_REGRESSION");
        }
        return new Arch5bR009NoOrderIntegrationResult(
            Status: "CompletedNoExternal",
            ExecutionIntentCount: 0,
            DecisionPreviewCount: 0,
            ExecutionAllowed: false,
            NotAnOrder: true,
            NoBrokerRoute: true,
            NoFixMessage: true,
            OrderEntryEnabled: false,
            BrokerSendStatus: "DISABLED_NO_ORDER_ENTRY",
            NoPaperLedgerCommit: true);
    }

    private static void ValidateCanonicalInputs(IEnumerable<Arch5bRunLineageContractV1> runs, Arch5bCanonicalPreviewInputs inputs)
    {
        if (inputs.AccountId != Arch5bLineageContractVersions.TestAccountId ||
            inputs.AccountScope != Arch5bLineageContractVersions.TestAccountScope ||
            inputs.AccountId == Arch5bLineageContractVersions.RealAccountId)
        {
            throw new InvalidDataException("REAL_OR_UNAPPROVED_ACCOUNT_REJECTED");
        }
        if ((!string.IsNullOrWhiteSpace(inputs.AccountSnapshotSha256) && !Arch5bHashing.IsSha256(inputs.AccountSnapshotSha256)) ||
            inputs.NavUsd < 0 || inputs.AsOfUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException("CANONICAL_ACCOUNT_SNAPSHOT_INVALID");
        }
        foreach (var run in runs)
        {
            if (run.MarketDataSnapshotId is not null && run.MarketDataSnapshotId != inputs.MarketDataSnapshotId.Value.ToString("D"))
            {
                throw new InvalidDataException("CANONICAL_MARKET_DATA_SNAPSHOT_MISMATCH");
            }
        }
        foreach (var input in inputs.Securities.Values)
        {
            if (input.MarketData.Id != inputs.MarketDataSnapshotId || input.MarketData.InstrumentId != input.InstrumentId ||
                input.VenueMapping.InstrumentId != input.InstrumentId ||
                (!string.IsNullOrWhiteSpace(input.PositionSnapshotSha256) && !Arch5bHashing.IsSha256(input.PositionSnapshotSha256)) ||
                (!string.IsNullOrWhiteSpace(input.WorkingLeavesSnapshotSha256) && !Arch5bHashing.IsSha256(input.WorkingLeavesSnapshotSha256)))
            {
                throw new InvalidDataException("CANONICAL_SECURITY_INPUT_INVALID");
            }
        }
    }
}

public sealed class Arch5bLineagePreviewRegistry
{
    private readonly Dictionary<string, Arch5bRunLineagePreview> previews = new(StringComparer.Ordinal);

    public Arch5bRunLineagePreview Register(Arch5bRunLineagePreview preview)
    {
        var key = preview.Lineage.LogicalRunId;
        if (!previews.TryGetValue(key, out var existing))
        {
            previews.Add(key, preview);
            return preview;
        }
        if (existing.Lineage.OutputSha256 != preview.Lineage.OutputSha256 || existing.PreviewSha256 != preview.PreviewSha256)
        {
            throw new InvalidDataException("SAME_RUN_ID_DIFFERENT_SHA_REJECTED");
        }
        return existing;
    }
}

public static class Arch5bHashing
{
    private static readonly JsonSerializerOptions CanonicalJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public static string Sha256Hex(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string HashCanonical<T>(T value)
        => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, CanonicalJson)));

    public static Guid GuidFromSha256(string value)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));

    public static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
