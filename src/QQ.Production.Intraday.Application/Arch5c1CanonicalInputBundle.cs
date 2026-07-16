using System.Globalization;
using System.Security.Cryptography;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public static class Arch5c1CanonicalBundleVersions
{
    public const string ContractV1 = "canonical_test_input_bundle_v1";
    public const string ScenarioId = "arch5c1:pinned-arch5a-qualified-native-close-test-v1";
    public const string ScenarioClassification = "CANONICAL_TEST_SCENARIO";
    public const string HistoricalClassification = "HISTORICAL_TEST_PREVIEW";
    public const string ModelRunSelectionPolicy = "PINNED_ARCH5A_QUALIFIED_MODEL_RUNS";
    public const string TemporalAlignmentStatus = "TIME_ALIGNED_PER_LINEAGE_NATIVE_TARGET_CLOSE";
    public const string SnapshotAsOfSemantics = "MAX_OF_PER_LINEAGE_VALUATION_SNAPSHOTS";
    public const string InstrumentScheme = "QUBES_SECURITY_ID";
    public const string MarketPriceType = "QUBES_PRICE_MATRIX_100_CLOSE_VALUE";
    public const string FxConversionPolicy = "TARGET_POSITION_CALCULATOR_PORTFOLIO_BASE_CURRENCY_NOTIONAL_EXISTING_RULE_NO_SEPARATE_FX_INPUT";
    public const string AccountFixtureId = "arch5b_tracked_canonical_test_account_fixture_v1";
    public const string AccountClassification = "TRACKED_TEST_FIXTURE_NOT_BROKER_AUTHORITY";
    public const string PositionClassification = "TEST_SCENARIO_EXPLICIT_EMPTY_NO_ORDER";
    public const string WorkingLeavesClassification = "TEST_SCENARIO_EXPLICIT_EMPTY_NO_ORDER";
    public const string EvidenceClassification = "EVIDENCE_ONLY_NON_ACCOUNTING";
}

public sealed record Arch5c1CanonicalPriceObservation(
    string SecurityId,
    string InstrumentIdentityScheme,
    string ExactPriceText,
    string PriceType,
    DateTimeOffset EventTimeUtc,
    DateTimeOffset SnapshotAsOfUtc,
    string SourceFile,
    string SourceFileSha256,
    string SourceRowKey,
    string Currency,
    long StalenessMilliseconds,
    string AuthorityClassification);

public sealed record Arch5c1CanonicalRunInput(
    string StrategyId,
    string LogicalRunId,
    DateTimeOffset WeightsAsOfUtc,
    DateTimeOffset TargetCloseUtc,
    DateTimeOffset SnapshotAsOfUtc,
    string MarketDataSnapshotId,
    string MarketDataSnapshotSha256,
    string AccountSnapshotId,
    string AccountSnapshotSha256,
    string PositionSnapshotId,
    string PositionSnapshotSha256,
    string WorkingLeavesSnapshotId,
    string WorkingLeavesSnapshotSha256,
    string InstrumentMappingId,
    string InstrumentMappingSha256,
    decimal NavUsd,
    string BaseCurrency,
    bool PositionSnapshotExplicit,
    int PositionCount,
    bool WorkingLeavesSnapshotExplicit,
    bool EmptyStateWasExplicitlyDeclared,
    bool EmptyStateWasInferred,
    int WorkingLeavesCount,
    bool BrokerAuthority,
    bool CurrentBrokerStateClaim,
    int UniqueSecurityIds,
    int MappedSecurityIds,
    IReadOnlyList<string> MissingSecurityIds,
    IReadOnlyList<string> AmbiguousSecurityIds,
    IReadOnlyList<Arch5c1CanonicalPriceObservation> MarketData);

public sealed record Arch5c1CanonicalTestInputBundle(
    string ContractVersion,
    string BundleId,
    string ScenarioId,
    string ScenarioClassification,
    string AccountId,
    string AccountScope,
    DateTimeOffset WeightsAsOf,
    DateTimeOffset SnapshotAsOf,
    string WeightsAsOfSemantics,
    string SnapshotAsOfSemantics,
    string ModelRunSelectionPolicy,
    string TemporalAlignmentStatus,
    string HistoricalOrCurrent,
    IReadOnlyList<string> SelectedModelRuns,
    string MarketDataSnapshotId,
    string AccountSnapshotId,
    string PositionSnapshotId,
    string WorkingLeavesSnapshotId,
    string InstrumentMappingId,
    string? FxSnapshotId,
    string FxConversionPolicy,
    IReadOnlyList<string> SourceManifestHashes,
    IReadOnlyList<string> AuthorityClassifications,
    bool EvidenceOnlyNonAccounting,
    bool AccountingEligible,
    bool ExecutionAllowed,
    bool NotAnOrder,
    bool NoBrokerRoute,
    bool NoFixMessage,
    bool OrderEntryEnabled,
    bool BrokerSend,
    int BrokerSendAttempts,
    int AccountApiCalls,
    bool DbApply,
    bool PmsAuthoritativeWrite,
    bool ModelRunAuthoritativeWrite,
    bool LmaxPortalLogin,
    bool RealAccountOperationalUse,
    int DatabentoApiCalls,
    int DatabentoDownloads,
    bool ProductionMutation,
    DateTimeOffset CreatedAtUtc,
    string BundleSha256,
    IReadOnlyList<Arch5c1CanonicalRunInput> Runs);

public sealed record Arch5c1CanonicalBundleValidation(bool IsValid, IReadOnlyList<string> Issues);

public sealed record Arch5c1CanonicalMaterializationResult(
    Arch5c1CanonicalTestInputBundle Bundle,
    Arch5bSessionLineageContractV1 BoundContract,
    IReadOnlyDictionary<string, Arch5bCanonicalPreviewInputs> PreviewInputsByStrategy);

public sealed class Arch5c1QubesPriceMatrixReader
{
    public IReadOnlyList<Arch5c1CanonicalPriceObservation> Read(
        string priceRoot,
        string strategyId,
        DateTimeOffset expectedSnapshotAsOfUtc,
        IReadOnlyCollection<string> expectedSecurityIds)
    {
        if (expectedSnapshotAsOfUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException("SNAPSHOT_AS_OF_NOT_UTC");
        }

        var strategyRoot = Path.Combine(priceRoot, strategyId);
        var tickerPath = Path.Combine(strategyRoot, "specs", "ticker.txt");
        var pricePath = Path.Combine(strategyRoot, "data", "100.bin");
        if (!File.Exists(tickerPath) || !File.Exists(pricePath))
        {
            throw new InvalidDataException("QUBES_PRICE_MATRIX_SOURCE_MISSING");
        }

        var securityIds = File.ReadAllLines(tickerPath).Select(value => value.Trim()).ToArray();
        if (securityIds.Any(string.IsNullOrWhiteSpace) ||
            securityIds.Distinct(StringComparer.Ordinal).Count() != securityIds.Length)
        {
            throw new InvalidDataException("QUBES_PRICE_MATRIX_SECURITY_ID_AMBIGUOUS");
        }
        if (!securityIds.OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(expectedSecurityIds.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidDataException("QUBES_PRICE_MATRIX_COVERAGE_MISMATCH");
        }

        using var stream = File.OpenRead(pricePath);
        using var reader = new BinaryReader(stream);
        var columnCount = reader.ReadInt32();
        var rowCount = reader.ReadInt32();
        if (columnCount != securityIds.Length || rowCount <= 0)
        {
            throw new InvalidDataException("QUBES_PRICE_MATRIX_SHAPE_INVALID");
        }

        var timestamps = new long[rowCount];
        for (var index = 0; index < rowCount; index++)
        {
            timestamps[index] = reader.ReadInt64();
        }
        var expectedLength = checked(8L + (8L * rowCount) + (8L * columnCount * rowCount));
        if (stream.Length != expectedLength)
        {
            throw new InvalidDataException("QUBES_PRICE_MATRIX_LENGTH_INVALID");
        }

        var sourceTimestampText = timestamps[^1].ToString(CultureInfo.InvariantCulture);
        if (!DateTime.TryParseExact(sourceTimestampText, "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTimestamp))
        {
            throw new InvalidDataException("QUBES_PRICE_MATRIX_TIMESTAMP_INVALID");
        }
        var snapshotAsOfUtc = new DateTimeOffset(DateTime.SpecifyKind(parsedTimestamp, DateTimeKind.Utc));
        if (snapshotAsOfUtc != expectedSnapshotAsOfUtc)
        {
            throw new InvalidDataException("QUBES_PRICE_MATRIX_SNAPSHOT_AS_OF_MISMATCH");
        }

        var sourceSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(pricePath)));
        var logicalSource = $"arch3c-bundle/data/{strategyId}/data/100.bin";
        var observations = new List<Arch5c1CanonicalPriceObservation>(columnCount);
        for (var column = 0; column < columnCount; column++)
        {
            stream.Position = checked(8L + (8L * rowCount) + (8L * ((long)column * rowCount + rowCount - 1)));
            var price = reader.ReadDouble();
            if (!double.IsFinite(price) || price <= 0 || price == -999d)
            {
                throw new InvalidDataException("QUBES_PRICE_MATRIX_PRICE_INVALID");
            }
            observations.Add(new Arch5c1CanonicalPriceObservation(
                securityIds[column],
                Arch5c1CanonicalBundleVersions.InstrumentScheme,
                price.ToString("R", CultureInfo.InvariantCulture),
                Arch5c1CanonicalBundleVersions.MarketPriceType,
                snapshotAsOfUtc,
                snapshotAsOfUtc,
                logicalSource,
                sourceSha256,
                $"{sourceTimestampText}:{securityIds[column]}",
                "QUBES_NATIVE_QUOTE_CURRENCY",
                0,
                "EVIDENCE_BACKED_HISTORICAL_TEST_SNAPSHOT"));
        }
        return observations;
    }
}

public sealed class Arch5c1CanonicalInputMaterializer
{
    private static readonly decimal TrackedTestNavUsd = 1_000_000m;

    public Arch5c1CanonicalMaterializationResult Materialize(Arch5bSessionLineageContractV1 contract, string priceRoot)
    {
        var lineageValidation = new Arch5bLineageContractValidator().Validate(contract);
        if (!lineageValidation.IsValid)
        {
            throw new InvalidDataException(string.Join(";", lineageValidation.Issues));
        }

        var reader = new Arch5c1QubesPriceMatrixReader();
        var runBundles = new List<Arch5c1CanonicalRunInput>();
        var previewInputs = new Dictionary<string, Arch5bCanonicalPreviewInputs>(StringComparer.Ordinal);
        var boundRuns = new List<Arch5bRunLineageContractV1>();
        var fundId = new FundId(Arch5bHashing.GuidFromSha256("arch5c1:tracked-test-fund"));
        var venueId = new VenueId(Arch5bHashing.GuidFromSha256("arch5c1:qubes-price-matrix-venue"));

        foreach (var run in contract.Runs.OrderBy(value => value.StrategyId, StringComparer.Ordinal))
        {
            var expectedSecurityIds = run.TargetCloseWeights.Select(value => value.SecurityId).ToArray();
            var observations = reader.Read(priceRoot, run.StrategyId, run.TargetCloseUtc, expectedSecurityIds)
                .OrderBy(value => value.SecurityId, StringComparer.Ordinal)
                .ToArray();
            var marketDataSnapshotSha = Arch5bHashing.HashCanonical(new
            {
                contract_version = Arch5c1CanonicalBundleVersions.ContractV1,
                strategy_id = run.StrategyId,
                snapshot_as_of = run.TargetCloseUtc,
                observations
            });
            var marketDataSnapshotId = new MarketDataSnapshotId(Arch5bHashing.GuidFromSha256($"arch5c1:market:{marketDataSnapshotSha}"));
            var accountSnapshotSha = Arch5bHashing.HashCanonical(new
            {
                fixture_id = Arch5c1CanonicalBundleVersions.AccountFixtureId,
                account_id = contract.PreviewAccountId,
                account_scope = contract.PreviewAccountScope,
                snapshot_as_of = run.TargetCloseUtc,
                nav_usd = TrackedTestNavUsd,
                base_currency = "USD",
                authority = Arch5c1CanonicalBundleVersions.AccountClassification
            });
            var positionSnapshotSha = Arch5bHashing.HashCanonical(new
            {
                scenario_id = Arch5c1CanonicalBundleVersions.ScenarioId,
                strategy_id = run.StrategyId,
                snapshot_as_of = run.TargetCloseUtc,
                positions = Array.Empty<object>(),
                explicitly_declared = true,
                inferred = false
            });
            var workingLeavesSnapshotSha = Arch5bHashing.HashCanonical(new
            {
                scenario_id = Arch5c1CanonicalBundleVersions.ScenarioId,
                strategy_id = run.StrategyId,
                snapshot_as_of = run.TargetCloseUtc,
                working_leaves = Array.Empty<object>(),
                explicitly_declared = true,
                inferred = false,
                broker_authority = false
            });
            var mappingSha = Arch5bHashing.HashCanonical(observations.Select(value => new
            {
                value.SecurityId,
                scheme = Arch5c1CanonicalBundleVersions.InstrumentScheme
            }).ToArray());

            var securities = observations.ToDictionary(
                observation => observation.SecurityId,
                observation =>
                {
                    var instrumentId = new InstrumentId(Arch5bHashing.GuidFromSha256($"{Arch5c1CanonicalBundleVersions.InstrumentScheme}:{observation.SecurityId}"));
                    var venueInstrumentId = new VenueInstrumentId(Arch5bHashing.GuidFromSha256($"arch5c1:venue-instrument:{observation.SecurityId}"));
                    var price = decimal.Parse(observation.ExactPriceText, NumberStyles.Float, CultureInfo.InvariantCulture);
                    var symbol = $"QUBES_SECURITY_ID:{observation.SecurityId}";
                    return new Arch5bCanonicalSecurityPreviewInput(
                        observation.SecurityId,
                        instrumentId,
                        symbol,
                        new MarketDataSnapshot(
                            marketDataSnapshotId,
                            instrumentId,
                            venueId,
                            price,
                            price,
                            price,
                            $"{Arch5c1CanonicalBundleVersions.MarketPriceType}:{observation.SourceFile}:{observation.SourceRowKey}",
                            observation.EventTimeUtc,
                            observation.SnapshotAsOfUtc)
                        {
                            IsSynthetic = true,
                            CreatedAtUtc = observation.SnapshotAsOfUtc
                        },
                        new VenueInstrumentMapping(
                            venueInstrumentId,
                            venueId,
                            instrumentId,
                            symbol,
                            symbol,
                            1m,
                            0m,
                            0.0001m,
                            0.00001m),
                        CurrentBaseQuantity: 0m,
                        SignedReservedWorkingLeaves: 0m,
                        PositionSnapshotSha256: positionSnapshotSha,
                        WorkingLeavesSnapshotSha256: workingLeavesSnapshotSha);
                },
                StringComparer.Ordinal);

            previewInputs.Add(run.StrategyId, new Arch5bCanonicalPreviewInputs(
                contract.PreviewAccountId,
                contract.PreviewAccountScope,
                fundId,
                TrackedTestNavUsd,
                accountSnapshotSha,
                marketDataSnapshotId,
                run.TargetCloseUtc,
                securities));

            var accountSnapshotId = ContentId("account", accountSnapshotSha);
            var positionSnapshotId = ContentId("position", positionSnapshotSha);
            var workingLeavesSnapshotId = ContentId("working-leaves", workingLeavesSnapshotSha);
            var mappingId = ContentId("mapping", mappingSha);
            runBundles.Add(new Arch5c1CanonicalRunInput(
                run.StrategyId,
                run.LogicalRunId,
                run.TargetCloseUtc,
                run.TargetCloseUtc,
                run.TargetCloseUtc,
                marketDataSnapshotId.Value.ToString("D"),
                marketDataSnapshotSha,
                accountSnapshotId,
                accountSnapshotSha,
                positionSnapshotId,
                positionSnapshotSha,
                workingLeavesSnapshotId,
                workingLeavesSnapshotSha,
                mappingId,
                mappingSha,
                TrackedTestNavUsd,
                "USD",
                PositionSnapshotExplicit: true,
                PositionCount: 0,
                WorkingLeavesSnapshotExplicit: true,
                EmptyStateWasExplicitlyDeclared: true,
                EmptyStateWasInferred: false,
                WorkingLeavesCount: 0,
                BrokerAuthority: false,
                CurrentBrokerStateClaim: false,
                UniqueSecurityIds: observations.Length,
                MappedSecurityIds: observations.Length,
                MissingSecurityIds: [],
                AmbiguousSecurityIds: [],
                MarketData: observations));
            boundRuns.Add(run with
            {
                MarketDataSnapshotId = marketDataSnapshotId.Value.ToString("D"),
                MarketDataSnapshotEvidenceSha256 = marketDataSnapshotSha,
                MarketDataSnapshotStatus = "CANONICAL_MARKET_DATA_SNAPSHOT_PRESENT"
            });
        }

        var orderedRuns = runBundles.OrderBy(value => value.StrategyId, StringComparer.Ordinal).ToArray();
        var sourceHashes = orderedRuns
            .SelectMany(value => value.MarketData.Select(observation => observation.SourceFileSha256))
            .Append(contract.BundleArchiveSha256)
            .Append(contract.RunnerPackageSha256)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var draft = new Arch5c1CanonicalTestInputBundle(
            Arch5c1CanonicalBundleVersions.ContractV1,
            BundleId: string.Empty,
            Arch5c1CanonicalBundleVersions.ScenarioId,
            Arch5c1CanonicalBundleVersions.ScenarioClassification,
            contract.PreviewAccountId,
            contract.PreviewAccountScope,
            orderedRuns.Max(value => value.WeightsAsOfUtc),
            orderedRuns.Max(value => value.SnapshotAsOfUtc),
            "MAX_OF_PER_LINEAGE_WEIGHTS_AS_OF",
            Arch5c1CanonicalBundleVersions.SnapshotAsOfSemantics,
            Arch5c1CanonicalBundleVersions.ModelRunSelectionPolicy,
            Arch5c1CanonicalBundleVersions.TemporalAlignmentStatus,
            Arch5c1CanonicalBundleVersions.HistoricalClassification,
            orderedRuns.Select(value => value.LogicalRunId).ToArray(),
            AggregateId("market", orderedRuns.Select(value => value.MarketDataSnapshotSha256)),
            AggregateId("account", orderedRuns.Select(value => value.AccountSnapshotSha256)),
            AggregateId("position", orderedRuns.Select(value => value.PositionSnapshotSha256)),
            AggregateId("working-leaves", orderedRuns.Select(value => value.WorkingLeavesSnapshotSha256)),
            AggregateId("mapping", orderedRuns.Select(value => value.InstrumentMappingSha256)),
            FxSnapshotId: null,
            FxConversionPolicy: Arch5c1CanonicalBundleVersions.FxConversionPolicy,
            sourceHashes,
            [
                Arch5c1CanonicalBundleVersions.AccountClassification,
                Arch5c1CanonicalBundleVersions.PositionClassification,
                Arch5c1CanonicalBundleVersions.WorkingLeavesClassification,
                Arch5c1CanonicalBundleVersions.EvidenceClassification,
                "QUBES_INPUT_LINEAGE",
                "NOT_BROKER_AUTHORITY"
            ],
            EvidenceOnlyNonAccounting: true,
            AccountingEligible: false,
            ExecutionAllowed: false,
            NotAnOrder: true,
            NoBrokerRoute: true,
            NoFixMessage: true,
            OrderEntryEnabled: false,
            BrokerSend: false,
            BrokerSendAttempts: 0,
            AccountApiCalls: 0,
            DbApply: false,
            PmsAuthoritativeWrite: false,
            ModelRunAuthoritativeWrite: false,
            LmaxPortalLogin: false,
            RealAccountOperationalUse: false,
            DatabentoApiCalls: 0,
            DatabentoDownloads: 0,
            ProductionMutation: false,
            CreatedAtUtc: contract.Runs.Max(value => value.ProducedAtUtc),
            BundleSha256: string.Empty,
            Runs: orderedRuns);
        var bundleSha = Arch5c1CanonicalInputBundleValidator.ComputeBundleSha256(draft);
        var bundle = draft with
        {
            BundleId = $"canonical-test-input-bundle-sha256:{bundleSha}",
            BundleSha256 = bundleSha
        };
        var bundleValidation = Arch5c1CanonicalInputBundleValidator.Validate(bundle);
        if (!bundleValidation.IsValid)
        {
            throw new InvalidDataException(string.Join(";", bundleValidation.Issues));
        }

        return new Arch5c1CanonicalMaterializationResult(
            bundle,
            contract with { Runs = boundRuns.OrderBy(value => value.StrategyId, StringComparer.Ordinal).ToArray() },
            previewInputs);
    }

    private static string ContentId(string type, string sha256)
        => $"arch5c1:{type}:sha256:{sha256}";

    private static string AggregateId(string type, IEnumerable<string> hashes)
        => ContentId(type, Arch5bHashing.HashCanonical(hashes.OrderBy(value => value, StringComparer.Ordinal).ToArray()));
}

public static class Arch5c1CanonicalInputBundleValidator
{
    public static Arch5c1CanonicalBundleValidation Validate(Arch5c1CanonicalTestInputBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        var issues = new List<string>();
        Require(bundle.ContractVersion == Arch5c1CanonicalBundleVersions.ContractV1, "UNKNOWN_BUNDLE_CONTRACT_VERSION", issues);
        Require(bundle.ScenarioId == Arch5c1CanonicalBundleVersions.ScenarioId, "SCENARIO_ID_INVALID", issues);
        Require(bundle.ScenarioClassification == Arch5c1CanonicalBundleVersions.ScenarioClassification, "SCENARIO_CLASSIFICATION_INVALID", issues);
        Require(bundle.AccountId == Arch5bLineageContractVersions.TestAccountId && bundle.AccountId != Arch5bLineageContractVersions.RealAccountId, "REAL_OR_UNAPPROVED_ACCOUNT_REJECTED", issues);
        Require(bundle.AccountScope == Arch5bLineageContractVersions.TestAccountScope, "TEST_ACCOUNT_SCOPE_INVALID", issues);
        Require(bundle.Runs.Count == 4, "FOUR_RUN_INPUTS_REQUIRED", issues);
        Require(bundle.BundleSha256 == ComputeBundleSha256(bundle), "BUNDLE_SHA256_MISMATCH", issues);
        Require(bundle.BundleId == $"canonical-test-input-bundle-sha256:{bundle.BundleSha256}", "BUNDLE_ID_MISMATCH", issues);
        Require(bundle.WeightsAsOf != default && bundle.SnapshotAsOf != default, "TEMPORAL_FIELD_MISSING", issues);
        if (bundle.Runs.Count > 0)
        {
            Require(bundle.SnapshotAsOf == bundle.Runs.Max(value => value.SnapshotAsOfUtc), "SNAPSHOT_AS_OF_ENVELOPE_INVALID", issues);
            Require(bundle.WeightsAsOf == bundle.Runs.Max(value => value.WeightsAsOfUtc), "WEIGHTS_AS_OF_ENVELOPE_INVALID", issues);
        }
        Require(bundle.TemporalAlignmentStatus == Arch5c1CanonicalBundleVersions.TemporalAlignmentStatus, "TEMPORAL_ALIGNMENT_STATUS_INVALID", issues);
        Require(bundle.SnapshotAsOfSemantics == Arch5c1CanonicalBundleVersions.SnapshotAsOfSemantics, "SNAPSHOT_AS_OF_SEMANTICS_INVALID", issues);
        Require(bundle.ModelRunSelectionPolicy == Arch5c1CanonicalBundleVersions.ModelRunSelectionPolicy, "MODEL_RUN_SELECTION_POLICY_INVALID", issues);
        Require(bundle.SelectedModelRuns.SequenceEqual(bundle.Runs.Select(value => value.LogicalRunId), StringComparer.Ordinal), "SELECTED_MODEL_RUN_SET_INVALID", issues);
        Require(bundle.FxSnapshotId is null && bundle.FxConversionPolicy == Arch5c1CanonicalBundleVersions.FxConversionPolicy, "FX_CONVERSION_POLICY_INVALID", issues);
        Require(bundle.EvidenceOnlyNonAccounting && !bundle.AccountingEligible && !bundle.ExecutionAllowed && bundle.NotAnOrder && bundle.NoBrokerRoute && bundle.NoFixMessage, "NO_ORDER_BOUNDARY_INVALID", issues);
        Require(!bundle.OrderEntryEnabled && !bundle.BrokerSend && bundle.BrokerSendAttempts == 0 && bundle.AccountApiCalls == 0 && !bundle.DbApply, "EXTERNAL_OR_AUTHORITATIVE_ACTION_ENABLED", issues);
        Require(!bundle.PmsAuthoritativeWrite && !bundle.ModelRunAuthoritativeWrite && !bundle.LmaxPortalLogin && !bundle.RealAccountOperationalUse && bundle.DatabentoApiCalls == 0 && bundle.DatabentoDownloads == 0 && !bundle.ProductionMutation, "RUNTIME_BOUNDARY_INVALID", issues);

        foreach (var run in bundle.Runs)
        {
            Require(run.WeightsAsOfUtc != default && run.TargetCloseUtc != default && run.SnapshotAsOfUtc != default, "RUN_TEMPORAL_FIELD_MISSING", issues);
            Require(run.WeightsAsOfUtc == run.TargetCloseUtc && run.SnapshotAsOfUtc == run.TargetCloseUtc, "RUN_TEMPORAL_ALIGNMENT_INVALID", issues);
            var expectedMarketDataSha = MarketDataSha(run);
            var expectedMarketDataId = Arch5bHashing.GuidFromSha256($"arch5c1:market:{expectedMarketDataSha}").ToString("D");
            Require(run.MarketDataSnapshotSha256 == expectedMarketDataSha, "MARKET_DATA_SNAPSHOT_SHA_INVALID", issues);
            Require(run.MarketDataSnapshotId == expectedMarketDataId, "MARKET_DATA_SNAPSHOT_ID_INVALID", issues);
            Require(run.NavUsd == 1_000_000m && run.BaseCurrency == "USD", "TRACKED_TEST_ACCOUNT_FIXTURE_INVALID", issues);
            Require(run.PositionSnapshotExplicit && run.PositionCount == 0, "POSITION_SNAPSHOT_NOT_EXPLICIT_EMPTY", issues);
            Require(run.WorkingLeavesSnapshotExplicit && run.EmptyStateWasExplicitlyDeclared && !run.EmptyStateWasInferred && run.WorkingLeavesCount == 0, "WORKING_LEAVES_NOT_EXPLICIT_EMPTY", issues);
            Require(!run.BrokerAuthority && !run.CurrentBrokerStateClaim, "BROKER_AUTHORITY_CLAIM_INVALID", issues);
            Require(run.UniqueSecurityIds > 0 && run.MappedSecurityIds == run.UniqueSecurityIds && run.MissingSecurityIds.Count == 0 && run.AmbiguousSecurityIds.Count == 0, "INSTRUMENT_MAPPING_COVERAGE_INCOMPLETE", issues);
            Require(run.MarketData.Count == run.UniqueSecurityIds && run.MarketData.Select(value => value.SecurityId).Distinct(StringComparer.Ordinal).Count() == run.UniqueSecurityIds, "MARKET_DATA_COVERAGE_INCOMPLETE", issues);
            var expectedAccountSha = AccountSha(bundle, run);
            Require(run.AccountSnapshotSha256 == expectedAccountSha && run.AccountSnapshotId == ContentId("account", expectedAccountSha), "ACCOUNT_SNAPSHOT_CONTENT_ID_INVALID", issues);
            var expectedPositionSha = PositionSha(run);
            Require(run.PositionSnapshotSha256 == expectedPositionSha && run.PositionSnapshotId == ContentId("position", expectedPositionSha), "POSITION_SNAPSHOT_CONTENT_ID_INVALID", issues);
            var expectedWorkingLeavesSha = WorkingLeavesSha(run);
            Require(run.WorkingLeavesSnapshotSha256 == expectedWorkingLeavesSha && run.WorkingLeavesSnapshotId == ContentId("working-leaves", expectedWorkingLeavesSha), "WORKING_LEAVES_SNAPSHOT_CONTENT_ID_INVALID", issues);
            var expectedMappingSha = MappingSha(run);
            Require(run.InstrumentMappingSha256 == expectedMappingSha && run.InstrumentMappingId == ContentId("mapping", expectedMappingSha), "INSTRUMENT_MAPPING_CONTENT_ID_INVALID", issues);
            foreach (var observation in run.MarketData)
            {
                Require(observation.InstrumentIdentityScheme == Arch5c1CanonicalBundleVersions.InstrumentScheme, "INSTRUMENT_IDENTITY_SCHEME_INVALID", issues);
                Require(observation.PriceType == Arch5c1CanonicalBundleVersions.MarketPriceType, "MARKET_DATA_PRICE_TYPE_INVALID", issues);
                Require(observation.EventTimeUtc == run.SnapshotAsOfUtc && observation.SnapshotAsOfUtc == run.SnapshotAsOfUtc && observation.StalenessMilliseconds == 0, "MARKET_DATA_TIMESTAMP_OR_STALENESS_INVALID", issues);
                Require(decimal.TryParse(observation.ExactPriceText, NumberStyles.Float, CultureInfo.InvariantCulture, out var price) && price > 0, "MARKET_DATA_PRICE_INVALID", issues);
                Require(Arch5bHashing.IsSha256(observation.SourceFileSha256), "MARKET_DATA_SOURCE_SHA_INVALID", issues);
                Require(!Path.IsPathRooted(observation.SourceFile) && !observation.SourceFile.Contains(':', StringComparison.Ordinal), "ABSOLUTE_SOURCE_PATH_REJECTED", issues);
            }
        }
        Require(bundle.MarketDataSnapshotId == AggregateId("market", bundle.Runs.Select(value => value.MarketDataSnapshotSha256)), "AGGREGATE_MARKET_DATA_SNAPSHOT_ID_INVALID", issues);
        Require(bundle.AccountSnapshotId == AggregateId("account", bundle.Runs.Select(value => value.AccountSnapshotSha256)), "AGGREGATE_ACCOUNT_SNAPSHOT_ID_INVALID", issues);
        Require(bundle.PositionSnapshotId == AggregateId("position", bundle.Runs.Select(value => value.PositionSnapshotSha256)), "AGGREGATE_POSITION_SNAPSHOT_ID_INVALID", issues);
        Require(bundle.WorkingLeavesSnapshotId == AggregateId("working-leaves", bundle.Runs.Select(value => value.WorkingLeavesSnapshotSha256)), "AGGREGATE_WORKING_LEAVES_SNAPSHOT_ID_INVALID", issues);
        Require(bundle.InstrumentMappingId == AggregateId("mapping", bundle.Runs.Select(value => value.InstrumentMappingSha256)), "AGGREGATE_INSTRUMENT_MAPPING_ID_INVALID", issues);
        return new Arch5c1CanonicalBundleValidation(issues.Count == 0, issues.Distinct(StringComparer.Ordinal).ToArray());
    }

    public static string ComputeBundleSha256(Arch5c1CanonicalTestInputBundle bundle)
        => Arch5bHashing.HashCanonical(new
        {
            bundle.ContractVersion,
            bundle.ScenarioId,
            bundle.ScenarioClassification,
            bundle.AccountId,
            bundle.AccountScope,
            bundle.WeightsAsOf,
            bundle.SnapshotAsOf,
            bundle.WeightsAsOfSemantics,
            bundle.SnapshotAsOfSemantics,
            bundle.ModelRunSelectionPolicy,
            bundle.TemporalAlignmentStatus,
            bundle.HistoricalOrCurrent,
            bundle.SelectedModelRuns,
            bundle.MarketDataSnapshotId,
            bundle.AccountSnapshotId,
            bundle.PositionSnapshotId,
            bundle.WorkingLeavesSnapshotId,
            bundle.InstrumentMappingId,
            bundle.FxSnapshotId,
            bundle.FxConversionPolicy,
            bundle.SourceManifestHashes,
            bundle.AuthorityClassifications,
            bundle.EvidenceOnlyNonAccounting,
            bundle.AccountingEligible,
            bundle.ExecutionAllowed,
            bundle.NotAnOrder,
            bundle.NoBrokerRoute,
            bundle.NoFixMessage,
            bundle.OrderEntryEnabled,
            bundle.BrokerSend,
            bundle.BrokerSendAttempts,
            bundle.AccountApiCalls,
            bundle.DbApply,
            bundle.PmsAuthoritativeWrite,
            bundle.ModelRunAuthoritativeWrite,
            bundle.LmaxPortalLogin,
            bundle.RealAccountOperationalUse,
            bundle.DatabentoApiCalls,
            bundle.DatabentoDownloads,
            bundle.ProductionMutation,
            bundle.CreatedAtUtc,
            bundle.Runs
        });

    private static string MarketDataSha(Arch5c1CanonicalRunInput run)
        => Arch5bHashing.HashCanonical(new
        {
            contract_version = Arch5c1CanonicalBundleVersions.ContractV1,
            strategy_id = run.StrategyId,
            snapshot_as_of = run.SnapshotAsOfUtc,
            observations = run.MarketData
        });

    private static string AccountSha(Arch5c1CanonicalTestInputBundle bundle, Arch5c1CanonicalRunInput run)
        => Arch5bHashing.HashCanonical(new
        {
            fixture_id = Arch5c1CanonicalBundleVersions.AccountFixtureId,
            account_id = bundle.AccountId,
            account_scope = bundle.AccountScope,
            snapshot_as_of = run.SnapshotAsOfUtc,
            nav_usd = run.NavUsd,
            base_currency = run.BaseCurrency,
            authority = Arch5c1CanonicalBundleVersions.AccountClassification
        });

    private static string PositionSha(Arch5c1CanonicalRunInput run)
        => Arch5bHashing.HashCanonical(new
        {
            scenario_id = Arch5c1CanonicalBundleVersions.ScenarioId,
            strategy_id = run.StrategyId,
            snapshot_as_of = run.SnapshotAsOfUtc,
            positions = Array.Empty<object>(),
            explicitly_declared = true,
            inferred = false
        });

    private static string WorkingLeavesSha(Arch5c1CanonicalRunInput run)
        => Arch5bHashing.HashCanonical(new
        {
            scenario_id = Arch5c1CanonicalBundleVersions.ScenarioId,
            strategy_id = run.StrategyId,
            snapshot_as_of = run.SnapshotAsOfUtc,
            working_leaves = Array.Empty<object>(),
            explicitly_declared = true,
            inferred = false,
            broker_authority = false
        });

    private static string MappingSha(Arch5c1CanonicalRunInput run)
        => Arch5bHashing.HashCanonical(run.MarketData.Select(value => new
        {
            value.SecurityId,
            scheme = Arch5c1CanonicalBundleVersions.InstrumentScheme
        }).ToArray());

    private static string ContentId(string type, string sha256)
        => $"arch5c1:{type}:sha256:{sha256}";

    private static string AggregateId(string type, IEnumerable<string> hashes)
        => ContentId(type, Arch5bHashing.HashCanonical(hashes.OrderBy(value => value, StringComparer.Ordinal).ToArray()));

    private static void Require(bool condition, string issue, ICollection<string> issues)
    {
        if (!condition)
        {
            issues.Add(issue);
        }
    }
}
