using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public static class Arch6aOperationalPositionShadowContracts
{
    public const string BundleV1 = "operational_position_shadow_input_bundle_v1";
    public const string AccountV1 = "operational_account_snapshot_v1";
    public const string PositionV1 = "operational_position_snapshot_v1";
    public const string MarketDataV1 = "operational_market_data_snapshot_v1";
    public const string WorkingLeavesV1 = "broker_working_leaves_observation_v1";
    public const string QubesToLmaxMappingV1 = "qubes_security_id_to_lmax_market_instrument_mapping_v1";
    public const string Classification = "LMAX_OPERATIONAL_POSITION_SHADOW";
    public const string WorkingLeavesClassification = "BROKER_WORKING_LEAVES_UNAVAILABLE";
    public const string EvidenceClassification = "EVIDENCE_ONLY_NONACCOUNTING";
    public const string NoOrderClassification = "NO_ORDER";
    public const string WorkingLeavesUnavailable = "UNAVAILABLE_WITH_CURRENT_LMAX_INTERFACES";
    public const string WorkingLeavesReason = "NO_READ_ONLY_WORKING_ORDER_SOURCE_IN_AVAILABLE_LMAX_PORTAL_REPORTS_OR_MARKET_DATA_INTERFACE";
    public const string WorkingLeavesImpact = "BROKER_ADJUSTED_DRIFT_NOT_COMPUTABLE";
    public const string BrokerAdjustedBlocker = "BROKER_WORKING_LEAVES_UNOBSERVABLE";
}

public enum Arch6aOperationalShadowMode
{
    HISTORICAL_LMAX_OPERATIONAL_POSITION_SHADOW,
    LMAX_START_OF_DAY_POSITION_SHADOW
}

public sealed record Arch6aSourceFileEvidence(
    string LogicalName,
    string Sha256,
    DateTimeOffset ProducedAtUtc);

public sealed record OperationalAccountSnapshotV1(
    string ContractVersion,
    string AccountId,
    string AccountScope,
    string BaseCurrency,
    decimal NavOrEquity,
    DateOnly ReportDate,
    DateTimeOffset AsOfUtc,
    IReadOnlyList<Arch6aSourceFileEvidence> SourceFiles,
    string SnapshotSha256,
    string Authority,
    string CurrentOrHistorical);

public sealed record OperationalPositionV1(
    string SecurityId,
    string Symbol,
    decimal CurrentBaseQuantity);

public sealed record OperationalPositionSnapshotV1(
    string ContractVersion,
    string AccountId,
    DateOnly ReportDate,
    DateTimeOffset AsOfUtc,
    IReadOnlyList<OperationalPositionV1> Positions,
    bool EmptyStateWasExplicitlyObserved,
    bool EmptyStateWasInferred,
    bool BrokerAuthority,
    IReadOnlyList<Arch6aSourceFileEvidence> SourceFiles,
    string SnapshotSha256);

public sealed record OperationalMarketDataQuoteV1(
    string SecurityId,
    string LmaxInstrumentId,
    string Symbol,
    decimal Bid,
    decimal Ask,
    DateTimeOffset EventTimeUtc,
    DateTimeOffset ReceivedAtUtc,
    long StalenessMilliseconds,
    string SourceCaptureId,
    string SourceFileSha256,
    string SourceSystem,
    string ProjectionMethod,
    IReadOnlyList<string> ProjectionLegSecurityIds);

public sealed record OperationalMarketDataSnapshotV1(
    string ContractVersion,
    DateTimeOffset AsOfUtc,
    IReadOnlyList<OperationalMarketDataQuoteV1> Quotes,
    string SnapshotSha256,
    int MissingCount,
    int AmbiguousCount,
    int DuplicateCount);

public sealed record OperationalSecurityMappingV1(
    string SecurityId,
    Guid InstrumentId,
    Guid VenueId,
    Guid VenueInstrumentId,
    string Symbol,
    string LmaxInstrumentId,
    decimal QuantityMultiplier,
    decimal QuantityIncrement,
    decimal PriceIncrement);

public sealed record BrokerWorkingLeavesObservationV1(
    string ContractVersion,
    string Status,
    string SourceSystem,
    bool ObservationAttempted,
    bool EmptyStateObserved,
    bool EmptyStateInferred,
    bool BrokerAuthority,
    string Reason,
    string Impact);

public sealed record Arch6aTemporalPolicyV1(
    Arch6aOperationalShadowMode Mode,
    DateTimeOffset AccountPositionAsOfUtc,
    DateTimeOffset MarketDataAsOfUtc,
    bool AccountStateIsCurrent,
    bool StartOfDayAssumption,
    string BrokerWorkingLeavesStatus);

public sealed record Arch6aNoOrderSafetyV1(
    bool AccountingEligible,
    bool ExecutionAllowed,
    bool NotAnOrder,
    bool NoBrokerRoute,
    bool NoFixMessage,
    bool OrderEntryEnabled,
    int TradeIntentCount,
    string BrokerSendStatus,
    int BrokerSendCount,
    int FixOrderEntryCount,
    int AccountApiCallCount,
    int ApiKeyOperationCount,
    int DatabentoCallCount,
    int PolygonCallCount,
    int DbApplyCount,
    int GpuStartInstancesCount);

public sealed record OperationalPositionShadowInputBundleV1(
    string ContractVersion,
    string BundleSha256,
    string Classification,
    string WorkingLeavesClassification,
    string EvidenceClassification,
    string NoOrderClassification,
    Arch5bSessionLineageContractV1 PinnedModelRuns,
    int TargetWeightCount,
    string QubesToLmaxMappingContractVersion,
    string QubesToLmaxMappingSha256,
    OperationalAccountSnapshotV1 Account,
    OperationalPositionSnapshotV1 Positions,
    OperationalMarketDataSnapshotV1 MarketData,
    BrokerWorkingLeavesObservationV1 BrokerWorkingLeaves,
    IReadOnlyList<OperationalSecurityMappingV1> SecurityMappings,
    Arch6aTemporalPolicyV1 TemporalPolicy,
    Arch6aNoOrderSafetyV1 Safety);

public sealed record Arch6aOperationalPositionShadowValidation(
    bool IsValid,
    IReadOnlyList<string> Issues);

public sealed record Arch6aOperationalPositionShadowResult(
    OperationalPositionShadowInputBundleV1 InputBundle,
    Arch5bSessionLineagePreview Preview,
    int TargetPositionStageCount,
    int PositionOnlyDriftStageCount,
    int BrokerAdjustedDriftBlockedStageCount,
    bool CompletedNoExternal,
    int TradeIntentCount,
    bool AccountingEligible,
    bool ExecutionAllowed,
    string ResultSha256);

public static class Arch6aOperationalPositionShadowValidator
{
    public static Arch6aOperationalPositionShadowValidation Validate(
        OperationalPositionShadowInputBundleV1 bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        var issues = new List<string>();
        Require(bundle.ContractVersion == Arch6aOperationalPositionShadowContracts.BundleV1, "UNKNOWN_CONTRACT_VERSION", issues);
        Require(bundle.BundleSha256 == ComputeBundleSha256(bundle), "BUNDLE_SHA256_MISMATCH", issues);
        Require(bundle.Classification == Arch6aOperationalPositionShadowContracts.Classification, "CLASSIFICATION_INVALID", issues);
        Require(bundle.WorkingLeavesClassification == Arch6aOperationalPositionShadowContracts.WorkingLeavesClassification, "WORKING_LEAVES_CLASSIFICATION_INVALID", issues);
        Require(bundle.EvidenceClassification == Arch6aOperationalPositionShadowContracts.EvidenceClassification, "EVIDENCE_CLASSIFICATION_INVALID", issues);
        Require(bundle.NoOrderClassification == Arch6aOperationalPositionShadowContracts.NoOrderClassification, "NO_ORDER_CLASSIFICATION_INVALID", issues);

        var lineage = new Arch5bLineageContractValidator().Validate(bundle.PinnedModelRuns);
        issues.AddRange(lineage.Issues.Select(issue => $"MODEL_RUN:{issue}"));
        var requiredSecurityIds = bundle.PinnedModelRuns.Runs
            .SelectMany(run => run.TargetCloseWeights)
            .Select(weight => weight.SecurityId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var actualWeightCount = bundle.PinnedModelRuns.Runs.Sum(run => run.TargetCloseWeights.Count);
        Require(bundle.PinnedModelRuns.Runs.Count == 4, "FOUR_MODEL_RUNS_REQUIRED", issues);
        Require(actualWeightCount > 0 && bundle.TargetWeightCount == actualWeightCount, "TARGET_WEIGHT_COUNT_INVALID", issues);
        Require(
            bundle.QubesToLmaxMappingContractVersion == Arch6aOperationalPositionShadowContracts.QubesToLmaxMappingV1 &&
            Arch5bHashing.IsSha256(bundle.QubesToLmaxMappingSha256),
            "QUBES_TO_LMAX_MAPPING_BINDING_INVALID",
            issues);

        ValidateAccount(bundle.Account, issues);
        ValidatePositions(bundle.Positions, issues);
        ValidateMarketData(bundle.MarketData, requiredSecurityIds, issues);
        ValidateMappings(bundle.SecurityMappings, requiredSecurityIds, issues);
        issues.AddRange(ValidateWorkingLeaves(bundle.BrokerWorkingLeaves));
        ValidateTemporal(bundle, issues);
        ValidateSafety(bundle.Safety, issues);

        return new(issues.Count == 0, issues.Distinct(StringComparer.Ordinal).ToArray());
    }

    public static IReadOnlyList<string> ValidateWorkingLeaves(BrokerWorkingLeavesObservationV1 observation)
    {
        var issues = new List<string>();
        Require(observation.ContractVersion == Arch6aOperationalPositionShadowContracts.WorkingLeavesV1, "WORKING_LEAVES_CONTRACT_VERSION_INVALID", issues);
        Require(observation.Status == Arch6aOperationalPositionShadowContracts.WorkingLeavesUnavailable, "WORKING_LEAVES_STATUS_INVALID", issues);
        Require(observation.SourceSystem == "LMAX", "WORKING_LEAVES_SOURCE_INVALID", issues);
        Require(!observation.ObservationAttempted && !observation.EmptyStateObserved && !observation.EmptyStateInferred && !observation.BrokerAuthority, "WORKING_LEAVES_FALSE_EMPTY_OR_AUTHORITY", issues);
        Require(observation.Reason == Arch6aOperationalPositionShadowContracts.WorkingLeavesReason, "WORKING_LEAVES_REASON_INVALID", issues);
        Require(observation.Impact == Arch6aOperationalPositionShadowContracts.WorkingLeavesImpact, "WORKING_LEAVES_IMPACT_INVALID", issues);
        return issues;
    }

    public static string ComputeBundleSha256(OperationalPositionShadowInputBundleV1 bundle)
        => Arch5bHashing.HashCanonical(new
        {
            bundle.ContractVersion,
            bundle.Classification,
            bundle.WorkingLeavesClassification,
            bundle.EvidenceClassification,
            bundle.NoOrderClassification,
            bundle.PinnedModelRuns,
            bundle.TargetWeightCount,
            bundle.QubesToLmaxMappingContractVersion,
            bundle.QubesToLmaxMappingSha256,
            bundle.Account,
            bundle.Positions,
            bundle.MarketData,
            bundle.BrokerWorkingLeaves,
            bundle.SecurityMappings,
            bundle.TemporalPolicy,
            bundle.Safety
        });

    private static void ValidateAccount(OperationalAccountSnapshotV1 account, ICollection<string> issues)
    {
        Require(account.ContractVersion == Arch6aOperationalPositionShadowContracts.AccountV1, "ACCOUNT_CONTRACT_VERSION_INVALID", issues);
        Require(account.AccountId == Arch5bLineageContractVersions.TestAccountId && account.AccountId != Arch5bLineageContractVersions.RealAccountId, "REAL_OR_UNAPPROVED_ACCOUNT_REJECTED", issues);
        Require(account.AccountScope == Arch5bLineageContractVersions.TestAccountScope, "ACCOUNT_SCOPE_INVALID", issues);
        Require(!string.IsNullOrWhiteSpace(account.BaseCurrency) && account.NavOrEquity > 0m, "ACCOUNT_NAV_OR_CURRENCY_MISSING", issues);
        Require(account.Authority == "BROKER_PORTAL_EOD", "ACCOUNT_AUTHORITY_INVALID", issues);
        Require(account.CurrentOrHistorical is "CURRENT" or "HISTORICAL", "ACCOUNT_CURRENT_OR_HISTORICAL_INVALID", issues);
        Require(account.ReportDate != default && IsUtc(account.AsOfUtc), "ACCOUNT_TIMESTAMP_INVALID", issues);
        Require(Arch5bHashing.IsSha256(account.SnapshotSha256) && ValidSources(account.SourceFiles), "ACCOUNT_SOURCE_EVIDENCE_INVALID", issues);
    }

    private static void ValidatePositions(OperationalPositionSnapshotV1 positions, ICollection<string> issues)
    {
        Require(positions.ContractVersion == Arch6aOperationalPositionShadowContracts.PositionV1, "POSITION_CONTRACT_VERSION_INVALID", issues);
        Require(positions.AccountId == Arch5bLineageContractVersions.TestAccountId, "POSITION_ACCOUNT_INVALID", issues);
        Require(positions.ReportDate != default && IsUtc(positions.AsOfUtc), "POSITION_TIMESTAMP_INVALID", issues);
        Require(positions.BrokerAuthority, "POSITION_BROKER_AUTHORITY_REQUIRED", issues);
        Require(!positions.EmptyStateWasInferred, "POSITION_EMPTY_STATE_INFERRED", issues);
        Require(positions.Positions.Count > 0 || positions.EmptyStateWasExplicitlyObserved, "POSITION_EMPTY_STATE_NOT_EXPLICIT", issues);
        Require(positions.Positions.Count == 0 || !positions.EmptyStateWasExplicitlyObserved, "POSITION_NONEMPTY_MARKED_EMPTY", issues);
        Require(positions.Positions.Select(value => value.SecurityId).Distinct(StringComparer.Ordinal).Count() == positions.Positions.Count, "POSITION_SECURITY_DUPLICATE", issues);
        Require(positions.Positions.All(value => !string.IsNullOrWhiteSpace(value.SecurityId) && !string.IsNullOrWhiteSpace(value.Symbol)), "POSITION_IDENTITY_INVALID", issues);
        Require(Arch5bHashing.IsSha256(positions.SnapshotSha256) && ValidSources(positions.SourceFiles), "POSITION_SOURCE_EVIDENCE_INVALID", issues);
    }

    private static void ValidateMarketData(
        OperationalMarketDataSnapshotV1 marketData,
        IReadOnlyCollection<string> requiredSecurityIds,
        ICollection<string> issues)
    {
        Require(marketData.ContractVersion == Arch6aOperationalPositionShadowContracts.MarketDataV1, "MARKET_DATA_CONTRACT_VERSION_INVALID", issues);
        Require(IsUtc(marketData.AsOfUtc) && Arch5bHashing.IsSha256(marketData.SnapshotSha256), "MARKET_DATA_SNAPSHOT_EVIDENCE_INVALID", issues);
        Require(marketData.MissingCount == 0 && marketData.AmbiguousCount == 0 && marketData.DuplicateCount == 0, "MARKET_DATA_COVERAGE_COUNTER_INVALID", issues);
        var quotedIds = marketData.Quotes.Select(value => value.SecurityId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        Require(quotedIds.SequenceEqual(requiredSecurityIds, StringComparer.Ordinal), "MARKET_DATA_COVERAGE_INCOMPLETE", issues);
        Require(quotedIds.Distinct(StringComparer.Ordinal).Count() == quotedIds.Length, "MARKET_DATA_SECURITY_DUPLICATE", issues);
        foreach (var quote in marketData.Quotes)
        {
            Require(quote.SourceSystem == "LMAX", "NON_LMAX_MARKET_DATA_REJECTED", issues);
            Require(!string.IsNullOrWhiteSpace(quote.LmaxInstrumentId) && !string.IsNullOrWhiteSpace(quote.Symbol) &&
                    !string.IsNullOrWhiteSpace(quote.SourceCaptureId) && Arch5bHashing.IsSha256(quote.SourceFileSha256),
                "MARKET_DATA_PROVENANCE_INCOMPLETE", issues);
            Require(quote.Bid > 0m && quote.Ask >= quote.Bid, "MARKET_DATA_PRICE_INVALID", issues);
            Require(IsUtc(quote.EventTimeUtc) && IsUtc(quote.ReceivedAtUtc) && quote.StalenessMilliseconds >= 0, "MARKET_DATA_STALENESS_UNDECLARED", issues);
            Require(quote.EventTimeUtc <= quote.ReceivedAtUtc && quote.ReceivedAtUtc <= marketData.AsOfUtc, "MARKET_DATA_TIMESTAMP_INVALID", issues);
            Require(quote.ProjectionMethod is "LMAX_DIRECT" or "LMAX_DIRECT_INVERTED" or "LMAX_USD_TWO_LEG_CROSS_V1", "MARKET_DATA_PROJECTION_INVALID", issues);
            Require(quote.ProjectionMethod != "LMAX_USD_TWO_LEG_CROSS_V1" || quote.ProjectionLegSecurityIds.Count == 2, "MARKET_DATA_USD_PROVENANCE_INVALID", issues);
        }
    }

    private static void ValidateMappings(
        IReadOnlyList<OperationalSecurityMappingV1> mappings,
        IReadOnlyCollection<string> requiredSecurityIds,
        ICollection<string> issues)
    {
        var mappedIds = mappings.Select(value => value.SecurityId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        Require(mappedIds.SequenceEqual(requiredSecurityIds, StringComparer.Ordinal), "SECURITY_MAPPING_COVERAGE_INCOMPLETE", issues);
        Require(mappedIds.Distinct(StringComparer.Ordinal).Count() == mappedIds.Length, "SECURITY_MAPPING_DUPLICATE", issues);
        Require(mappings.All(value => value.InstrumentId != Guid.Empty && value.VenueId != Guid.Empty &&
                                      value.VenueInstrumentId != Guid.Empty && !string.IsNullOrWhiteSpace(value.Symbol) &&
                                      !string.IsNullOrWhiteSpace(value.LmaxInstrumentId) && value.QuantityMultiplier > 0m &&
                                      value.QuantityIncrement > 0m && value.PriceIncrement > 0m),
            "SECURITY_MAPPING_INVALID", issues);
    }

    private static void ValidateTemporal(OperationalPositionShadowInputBundleV1 bundle, ICollection<string> issues)
    {
        var temporal = bundle.TemporalPolicy;
        Require(temporal.AccountPositionAsOfUtc == bundle.Account.AsOfUtc &&
                temporal.AccountPositionAsOfUtc == bundle.Positions.AsOfUtc &&
                temporal.MarketDataAsOfUtc == bundle.MarketData.AsOfUtc,
            "TEMPORAL_SOURCE_BINDING_INVALID", issues);
        Require(temporal.BrokerWorkingLeavesStatus == Arch6aOperationalPositionShadowContracts.WorkingLeavesUnavailable, "TEMPORAL_WORKING_LEAVES_STATUS_INVALID", issues);
        if (temporal.Mode == Arch6aOperationalShadowMode.HISTORICAL_LMAX_OPERATIONAL_POSITION_SHADOW)
        {
            var marketDate = DateOnly.FromDateTime(bundle.MarketData.AsOfUtc.UtcDateTime);
            Require(bundle.Account.ReportDate == bundle.Positions.ReportDate && bundle.Account.ReportDate == marketDate, "HISTORICAL_COMMON_DATE_MISSING", issues);
            Require(!temporal.StartOfDayAssumption, "HISTORICAL_START_OF_DAY_ASSUMPTION_INVALID", issues);
        }
        else
        {
            Require(bundle.Account.AsOfUtc < bundle.MarketData.AsOfUtc && bundle.Positions.AsOfUtc < bundle.MarketData.AsOfUtc, "START_OF_DAY_ORDERING_INVALID", issues);
            Require(!temporal.AccountStateIsCurrent && temporal.StartOfDayAssumption, "START_OF_DAY_CLASSIFICATION_INVALID", issues);
        }
    }

    private static void ValidateSafety(Arch6aNoOrderSafetyV1 safety, ICollection<string> issues)
    {
        Require(!safety.AccountingEligible && !safety.ExecutionAllowed && safety.NotAnOrder &&
                safety.NoBrokerRoute && safety.NoFixMessage && !safety.OrderEntryEnabled &&
                safety.TradeIntentCount == 0 && safety.BrokerSendStatus == "DISABLED_NO_ORDER_ENTRY",
            "NO_ORDER_BOUNDARY_INVALID", issues);
        Require(safety.BrokerSendCount == 0 && safety.FixOrderEntryCount == 0 &&
                safety.AccountApiCallCount == 0 && safety.ApiKeyOperationCount == 0 &&
                safety.DatabentoCallCount == 0 && safety.PolygonCallCount == 0 &&
                safety.DbApplyCount == 0 && safety.GpuStartInstancesCount == 0,
            "FORBIDDEN_EXTERNAL_OPERATION_OBSERVED", issues);
    }

    private static bool ValidSources(IReadOnlyList<Arch6aSourceFileEvidence> sources)
        => sources.Count > 0 && sources.All(value =>
            !string.IsNullOrWhiteSpace(value.LogicalName) &&
            !Path.IsPathRooted(value.LogicalName) &&
            Arch5bHashing.IsSha256(value.Sha256) &&
            IsUtc(value.ProducedAtUtc));

    private static bool IsUtc(DateTimeOffset value)
        => value != default && value.Offset == TimeSpan.Zero;

    private static void Require(bool condition, string issue, ICollection<string> issues)
    {
        if (!condition)
        {
            issues.Add(issue);
        }
    }
}

public sealed class Arch6aOperationalPositionShadowService
{
    public Arch6aOperationalPositionShadowResult Build(OperationalPositionShadowInputBundleV1 bundle)
    {
        var validation = Arch6aOperationalPositionShadowValidator.Validate(bundle);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join(";", validation.Issues));
        }

        var marketSnapshotId = new MarketDataSnapshotId(
            Arch5bHashing.GuidFromSha256($"arch6a:market:{bundle.MarketData.SnapshotSha256}"));
        var boundContract = bundle.PinnedModelRuns with
        {
            Runs = bundle.PinnedModelRuns.Runs.Select(run => run with
            {
                MarketDataSnapshotId = marketSnapshotId.Value.ToString("D"),
                MarketDataSnapshotEvidenceSha256 = bundle.MarketData.SnapshotSha256,
                MarketDataSnapshotStatus = "CANONICAL_MARKET_DATA_SNAPSHOT_PRESENT"
            }).ToArray()
        };
        var quoteBySecurity = bundle.MarketData.Quotes.ToDictionary(value => value.SecurityId, StringComparer.Ordinal);
        var mappingBySecurity = bundle.SecurityMappings.ToDictionary(value => value.SecurityId, StringComparer.Ordinal);
        var positionBySecurity = bundle.Positions.Positions.ToDictionary(value => value.SecurityId, value => value.CurrentBaseQuantity, StringComparer.Ordinal);
        var inputsByStrategy = new Dictionary<string, Arch5bCanonicalPreviewInputs>(StringComparer.Ordinal);
        foreach (var run in boundContract.Runs)
        {
            var securities = run.TargetCloseWeights.ToDictionary(
                weight => weight.SecurityId,
                weight =>
                {
                    var quote = quoteBySecurity[weight.SecurityId];
                    var mapping = mappingBySecurity[weight.SecurityId];
                    var instrumentId = new InstrumentId(mapping.InstrumentId);
                    var venueId = new VenueId(mapping.VenueId);
                    var marketData = new MarketDataSnapshot(
                        marketSnapshotId,
                        instrumentId,
                        venueId,
                        quote.Bid,
                        quote.Ask,
                        (quote.Bid + quote.Ask) / 2m,
                        $"LMAX_OPERATIONAL_CAPTURE:{quote.SourceCaptureId}",
                        quote.EventTimeUtc,
                        quote.ReceivedAtUtc);
                    var venueMapping = new VenueInstrumentMapping(
                        new VenueInstrumentId(mapping.VenueInstrumentId),
                        venueId,
                        instrumentId,
                        mapping.Symbol,
                        mapping.LmaxInstrumentId,
                        mapping.QuantityMultiplier,
                        0m,
                        mapping.QuantityIncrement,
                        mapping.PriceIncrement);
                    return new Arch5bCanonicalSecurityPreviewInput(
                        weight.SecurityId,
                        instrumentId,
                        mapping.Symbol,
                        marketData,
                        venueMapping,
                        positionBySecurity.GetValueOrDefault(weight.SecurityId),
                        SignedReservedWorkingLeaves: 0m,
                        bundle.Positions.SnapshotSha256,
                        WorkingLeavesSnapshotSha256: string.Empty);
                },
                StringComparer.Ordinal);
            inputsByStrategy.Add(run.StrategyId, new Arch5bCanonicalPreviewInputs(
                bundle.Account.AccountId,
                bundle.Account.AccountScope,
                new FundId(Arch5bHashing.GuidFromSha256($"arch6a:fund:{bundle.Account.AccountId}")),
                bundle.Account.NavOrEquity,
                bundle.Account.SnapshotSha256,
                marketSnapshotId,
                bundle.MarketData.AsOfUtc,
                securities,
                bundle.BrokerWorkingLeaves));
        }

        var preview = new Arch5bQubesLineagePreviewService().Build(boundContract, inputsByStrategy);
        var targetStages = preview.Runs.Count(run => run.TargetPositions.ComputationStatus == Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW);
        var positionOnlyStages = preview.Runs.Count(run => run.DriftSnapshot.PositionOnlyDriftCalculated);
        var blockedStages = preview.Runs.Count(run =>
            !run.DriftSnapshot.BrokerAdjustedDriftCalculated &&
            run.DriftSnapshot.BrokerAdjustedDriftBlocker == Arch6aOperationalPositionShadowContracts.BrokerAdjustedBlocker);
        var completed = preview.Runs.All(run =>
            run.ManualPaperCycle.CompletedNoExternal &&
            run.R009.Status == "CompletedNoExternal" &&
            run.R009.ExecutionIntentCount == 0);
        if (targetStages != 4 || positionOnlyStages != 4 || blockedStages != 4 || !completed)
        {
            throw new InvalidDataException("ARCH6A_OPERATIONAL_POSITION_SHADOW_INCOMPLETE");
        }

        var resultHash = Arch5bHashing.HashCanonical(new
        {
            bundle.BundleSha256,
            preview.PreviewSha256,
            targetStages,
            positionOnlyStages,
            blockedStages,
            CompletedNoExternal = true,
            TradeIntentCount = 0,
            AccountingEligible = false,
            ExecutionAllowed = false
        });
        return new(
            bundle,
            preview,
            targetStages,
            positionOnlyStages,
            blockedStages,
            CompletedNoExternal: true,
            TradeIntentCount: 0,
            AccountingEligible: false,
            ExecutionAllowed: false,
            resultHash);
    }
}
