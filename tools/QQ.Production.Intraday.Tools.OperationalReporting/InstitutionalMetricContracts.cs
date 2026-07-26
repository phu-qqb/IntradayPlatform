using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class InstitutionalMetricContract
{
    public const string CatalogVersion = "anubis_infx_institutional_metric_catalog_v1";
    public const string RoadmapId = "hedge_fund_institutional_reporting_roadmap";
    public const string RoadmapVersion = "v1";
    public const string ExposureFormula = "target_exposure_usd_v1";
    public const string CurrencyFormula = "target_currency_leg_exposure_usd_v1";
    public const string ConcentrationFormula = "target_concentration_v1";
    public const string TurnoverFormula = "target_turnover_gross_change_usd_v1";
    public const string DriftFormula = "position_only_drift_v1";
    public const string NullCsvValue = "NULL";
}

public static class MetricAvailabilityStatus
{
    public const string SourceProven = "SOURCE_PROVEN";
    public const string DerivableProven = "DERIVABLE_PROVEN";
    public const string DerivableProbable = "DERIVABLE_PROBABLE";
    public const string BlockedMissingSource = "BLOCKED_MISSING_SOURCE";
    public const string BlockedAuthorityUnproven = "BLOCKED_AUTHORITY_UNPROVEN";
    public const string NotApplicable = "NOT_APPLICABLE";
    public const string Unknown = "UNKNOWN";
}

public sealed record InstitutionalMetricDefinition(
    string MetricCode,
    string Category,
    string Description,
    string Grain,
    string? Unit,
    string FormulaVersion,
    IReadOnlyList<string> RequiredFacts,
    string CurrentAvailability,
    string ExpectedAuthority,
    IReadOnlyList<string> AllowedAggregations,
    IReadOnlyList<string> ForbiddenAggregations,
    string RptPhase,
    string GeneralManifestCoverage);

public sealed record InstitutionalMetricAvailability(
    string MetricCode,
    string AvailabilityStatus,
    decimal? Value,
    string? Unit,
    string? Currency,
    IReadOnlyList<string> MissingRequiredFacts,
    string ActivationCondition,
    string Caveat,
    string AuthorityStatus,
    string DataQualityStatus);

public sealed record TargetExposureRow(
    Guid EconomicRevisionId,
    int RevisionNumber,
    string SlotId,
    DateTimeOffset AsOfUtc,
    string DimensionType,
    string DimensionId,
    string? StrategyId,
    Guid? ModelRunId,
    DateTimeOffset? TargetCloseUtc,
    Guid? InstrumentId,
    string? PmsSecurityId,
    string? LmaxInstrumentId,
    string? CanonicalSymbol,
    decimal GrossTargetNotionalUsd,
    decimal NetTargetNotionalUsd,
    decimal LongTargetNotionalUsd,
    decimal ShortTargetNotionalUsd,
    decimal AbsoluteWeight,
    string FormulaVersion,
    string AuthorityStatus,
    string EvidenceSha256);

public sealed record TargetCurrencyExposureRow(
    Guid EconomicRevisionId,
    int RevisionNumber,
    string SlotId,
    DateTimeOffset AsOfUtc,
    string Currency,
    decimal SignedTargetExposureUsd,
    decimal AbsoluteTargetExposureUsd,
    int SourceTargetCount,
    string FormulaVersion,
    string AuthorityStatus,
    string EvidenceSha256);

public sealed record TargetConcentrationRow(
    Guid EconomicRevisionId,
    string DimensionType,
    string DimensionId,
    decimal? Concentration,
    int Rank,
    decimal? TopNConcentration,
    decimal? Hhi,
    decimal? GrossNetRatio,
    string FormulaVersion,
    string DataQualityStatus,
    string Caveat);

public sealed record TargetTurnoverRow(
    Guid PreviousEconomicRevisionId,
    Guid EconomicRevisionId,
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    string DimensionType,
    string DimensionId,
    decimal TargetTurnoverUsd,
    int NewTargetCount,
    int ClosedTargetCount,
    int IncreaseCount,
    int ReductionCount,
    int InversionCount,
    string MetricCode,
    string FormulaVersion,
    string AvailabilityStatus,
    string EvidenceSha256);

public sealed record DriftSummaryRow(
    Guid EconomicRevisionId,
    string DimensionType,
    string DimensionId,
    decimal SignedDrift,
    decimal AbsoluteDrift,
    int SourceDriftCount,
    string PositionAuthority,
    string AvailabilityStatus,
    string FormulaVersion,
    string EvidenceSha256);

public sealed record InstitutionalDataQuality(
    DateTimeOffset AsOfUtc,
    string OverallStatus,
    Guid? LatestEconomicRevisionId,
    int LatestMarketObservationCount,
    int LatestTargetPositionCount,
    int LatestPositionOnlyDriftCount,
    IReadOnlyDictionary<string, int> SelectedInfxCounts,
    bool SelectedInfxComplete,
    bool MappingComplete,
    bool LineageComplete,
    string Freshness,
    int ActiveBreakCount,
    int UnknownBreakCount,
    string Arch7aAuthority,
    string Arch7bAuthority,
    string FillAuthority,
    string LedgerAuthority,
    string PositionAuthority,
    string AumNavAuthority,
    string CostAuthority,
    IReadOnlyList<string> Caveats);

public sealed record PmsRiskSummary(
    DateTimeOffset AsOfUtc,
    Guid? EconomicRevisionId,
    decimal? GrossTargetExposureUsd,
    decimal? NetTargetExposureUsd,
    decimal? LongTargetNotionalUsd,
    decimal? ShortTargetNotionalUsd,
    decimal? MaxPairConcentration,
    decimal? MaxStrategyConcentration,
    decimal? PairHhi,
    decimal? StrategyHhi,
    decimal? GrossNetRatio,
    decimal? AbsoluteDrift,
    decimal? TargetTurnoverUsd,
    string LeverageAvailability,
    string LeverageCaveat,
    string AuthorityStatus);

public sealed record PowerBiCsvContract(
    string File,
    string Grain,
    IReadOnlyList<string> LogicalPrimaryKey,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<string> Relations,
    string AdditiveStatus,
    string? Unit,
    string? Currency,
    string NullPolicy,
    string AsOfBehavior);

public sealed record InstitutionalMetricReportSet(
    DateTimeOffset AsOfUtc,
    string RepositoryCommit,
    ReportingDatabaseIdentity Database,
    string RoadmapSha256,
    IReadOnlyList<InstitutionalMetricDefinition> Catalog,
    IReadOnlyList<InstitutionalMetricAvailability> Availability,
    IReadOnlyList<TargetExposureRow> ExposureByRevision,
    IReadOnlyList<TargetExposureRow> ExposureByStrategy,
    IReadOnlyList<TargetExposureRow> ExposureByModel,
    IReadOnlyList<TargetExposureRow> ExposureByPair,
    IReadOnlyList<TargetCurrencyExposureRow> ExposureByCurrency,
    IReadOnlyList<TargetConcentrationRow> Concentrations,
    IReadOnlyList<TargetTurnoverRow> Turnover,
    IReadOnlyList<DriftSummaryRow> DriftByStrategy,
    IReadOnlyList<DriftSummaryRow> DriftByModel,
    IReadOnlyList<DriftSummaryRow> DriftByPair,
    PmsRiskSummary RiskSummary,
    InstitutionalDataQuality DataQuality,
    IReadOnlyList<OperationalBreak> ActiveBreaks,
    IReadOnlyList<PowerBiCsvContract> PowerBiContracts);

public sealed record InstitutionalBundleFile(string Path, long SizeBytes, string Sha256);

public sealed record InstitutionalBundleManifest(
    string ContractVersion,
    string RoadmapManifestId,
    string RoadmapManifestVersion,
    string RoadmapSha256,
    DateTimeOffset AsOfUtc,
    string RepositoryCommit,
    string SourceSnapshotId,
    string TargetProfileId,
    string TargetFingerprint,
    IReadOnlyList<string> FormulaVersions,
    IReadOnlyList<InstitutionalBundleFile> Files,
    string BundleSha256,
    bool NoOrder,
    bool ReadOnly,
    bool NoSecrets);

public sealed record InstitutionalBundleResult(
    string OutputDirectory,
    string BundleSha256,
    IReadOnlyList<InstitutionalBundleFile> Files);

internal sealed record InstitutionalTargetSource(
    PmsShadowIntradayEconomicProjection Revision,
    PmsShadowSlotTargetPosition Target,
    PmsShadowSecurityMappingRow Mapping,
    string CanonicalSymbol);
