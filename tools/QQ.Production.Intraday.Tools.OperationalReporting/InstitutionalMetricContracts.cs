using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class InstitutionalMetricContract
{
    public const string CatalogVersion = "anubis_infx_institutional_metric_catalog_v2";
    public const string RoadmapId = "hedge_fund_institutional_reporting_roadmap";
    public const string RoadmapVersion = "v1";
    public const string ExposureFormula = "target_exposure_usd_v2";
    public const string CurrencyFormula = "target_currency_leg_exposure_usd_v2";
    public const string GrossConcentrationFormula = "target_gross_concentration_v2";
    public const string NetConcentrationFormula = "target_net_concentration_v2";
    public const string TurnoverFormula = "target_turnover_canonical_symbol_v2";
    public const string DriftFormula = "position_only_drift_pair_grain_v2";
    public const string SourceSnapshotVersion = "institutional_source_snapshot_v1";
    public const string SupersededBundleSha256 =
        "fdfe8e2ec7555f885548665b09038d0eafb9507b629ab1868ebf56b56a3ac041";
    public const string SupersessionReason =
        "RPT2_METRIC_SEMANTICS_AND_SOURCE_AUTHORITY_CORRECTION_REQUIRED";
    public const int ConcentrationTopN = 3;
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
    string DataQualityStatus,
    string? ValueLocation,
    string? FactFile,
    int FactRowCount,
    bool ValueIsScalar,
    string Grain);

public sealed record TargetPositionFact(
    Guid EconomicRevisionId,
    int RevisionNumber,
    string SlotId,
    Guid TargetPositionId,
    string StrategyId,
    Guid ModelRunId,
    DateTimeOffset TargetCloseUtc,
    Guid InstrumentId,
    string PmsSecurityId,
    string LmaxInstrumentId,
    string CanonicalSymbol,
    decimal TargetNotionalUsd,
    decimal TargetBaseQuantity,
    decimal TargetVenueQuantity,
    DateTimeOffset SourceAsOfUtc,
    string SourceEvidenceSha256,
    string AuthorityStatus);

public sealed record PositionOnlyDriftFact(
    Guid EconomicRevisionId,
    Guid PositionOnlyDriftId,
    string StrategyId,
    Guid ModelRunId,
    DateTimeOffset TargetCloseUtc,
    Guid InstrumentId,
    string PmsSecurityId,
    string LmaxInstrumentId,
    string CanonicalSymbol,
    decimal Delta,
    string Unit,
    string PositionAuthority,
    string SourceEvidenceSha256,
    string AuthorityStatus);

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
    decimal GrossWeight,
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
    string Family,
    decimal DimensionGrossTargetNotionalUsd,
    decimal DimensionNetTargetNotionalUsd,
    decimal? Denominator,
    decimal? Share,
    int Rank,
    string FormulaVersion,
    string DataQualityStatus,
    string EvidenceSha256,
    string Caveat);

public sealed record TargetConcentrationSummaryRow(
    Guid EconomicRevisionId,
    string DimensionType,
    string Family,
    decimal? Denominator,
    int? TopN,
    decimal? TopNConcentration,
    decimal? Hhi,
    decimal? GrossNetRatio,
    string FormulaVersion,
    string DataQualityStatus,
    string EvidenceSha256,
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
    string PreviousMappingSetSha256,
    string CurrentMappingSetSha256,
    string MetricCode,
    string FormulaVersion,
    string AvailabilityStatus,
    string EvidenceSha256);

public sealed record DriftSummaryRow(
    Guid EconomicRevisionId,
    string DimensionType,
    string DimensionId,
    string CanonicalSymbol,
    string Unit,
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
    decimal? MaxPairGrossConcentration,
    decimal? MaxPairNetConcentration,
    decimal? MaxStrategyGrossConcentration,
    decimal? MaxStrategyNetConcentration,
    decimal? PairGrossHhi,
    decimal? PairNetHhi,
    decimal? StrategyGrossHhi,
    decimal? StrategyNetHhi,
    decimal? GrossNetRatio,
    IReadOnlyDictionary<string, decimal>? AbsoluteDriftByPair,
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

public sealed record InstitutionalSourceRevision(
    Guid EconomicRevisionId,
    string SlotId,
    int RevisionNumber,
    DateTimeOffset SlotEndUtc,
    DateTimeOffset CompletedAtUtc,
    Guid SourceIngestionId,
    string SourceSessionId,
    string MarketDataSnapshotSha256,
    string ManifestSha256,
    string TargetPositionsSha256,
    string DriftsSha256,
    IReadOnlyList<Guid> SelectedModelRunIds,
    IReadOnlyList<string> SelectedOutputSha256,
    IReadOnlyList<string> SelectedCoreCommitIds);

public sealed record InstitutionalSourceSnapshot(
    string ContractVersion,
    string RepositoryCommit,
    string RoadmapSha256,
    string TargetProfileId,
    string TargetFingerprint,
    ReportingDatabaseIdentity DatabaseIdentity,
    DateTimeOffset AsOfUtc,
    IReadOnlyList<InstitutionalSourceRevision> AuthoritativeRevisions,
    string MappingSetSha256,
    IReadOnlyList<string> ActiveOrUnknownBreakIds,
    string Rpt1SourceContractIdentity,
    string? Rpt1SourceBundleSha256);

public sealed record InstitutionalMetricReportSet(
    DateTimeOffset AsOfUtc,
    string RepositoryCommit,
    ReportingDatabaseIdentity Database,
    string RoadmapSha256,
    IReadOnlyList<InstitutionalMetricDefinition> Catalog,
    IReadOnlyList<InstitutionalMetricAvailability> Availability,
    IReadOnlyList<TargetPositionFact> TargetPositionFacts,
    IReadOnlyList<PositionOnlyDriftFact> PositionOnlyDriftFacts,
    IReadOnlyList<TargetExposureRow> ExposureByRevision,
    IReadOnlyList<TargetExposureRow> ExposureByStrategy,
    IReadOnlyList<TargetExposureRow> ExposureByModel,
    IReadOnlyList<TargetExposureRow> ExposureByPair,
    IReadOnlyList<TargetCurrencyExposureRow> ExposureByCurrency,
    IReadOnlyList<TargetConcentrationRow> GrossConcentrations,
    IReadOnlyList<TargetConcentrationRow> NetConcentrations,
    IReadOnlyList<TargetConcentrationSummaryRow> ConcentrationSummaries,
    IReadOnlyList<TargetTurnoverRow> Turnover,
    IReadOnlyList<DriftSummaryRow> DriftByStrategyPair,
    IReadOnlyList<DriftSummaryRow> DriftByModelPair,
    IReadOnlyList<DriftSummaryRow> DriftByPair,
    PmsRiskSummary RiskSummary,
    InstitutionalDataQuality DataQuality,
    IReadOnlyList<OperationalBreak> ActiveBreaks,
    IReadOnlyList<PowerBiCsvContract> PowerBiContracts,
    InstitutionalSourceSnapshot SourceSnapshot,
    string SourceSnapshotSha256);

public sealed record InstitutionalBundleFile(string Path, long SizeBytes, string Sha256);

public sealed record InstitutionalBundleManifest(
    string ContractVersion,
    string RoadmapManifestId,
    string RoadmapManifestVersion,
    string RoadmapSha256,
    DateTimeOffset AsOfUtc,
    string RepositoryCommit,
    string SourceSnapshotSha256,
    string TargetProfileId,
    string TargetFingerprint,
    IReadOnlyList<string> FormulaVersions,
    IReadOnlyList<InstitutionalBundleFile> Files,
    string BundleSha256,
    string SupersedesBundleSha256,
    string SupersessionReason,
    bool NoOrder,
    bool ReadOnly,
    bool NoSecrets);

public sealed record InstitutionalBundleResult(
    string OutputDirectory,
    string BundleSha256,
    string SourceSnapshotSha256,
    IReadOnlyList<InstitutionalBundleFile> Files);

internal sealed record InstitutionalTargetSource(
    PmsShadowIntradayEconomicProjection Revision,
    PmsShadowSlotTargetPosition Target,
    PmsShadowSecurityMappingRow Mapping,
    string CanonicalSymbol);

internal sealed record InstitutionalDriftSource(
    PmsShadowIntradayEconomicProjection Revision,
    PmsShadowSlotPositionOnlyDrift Drift,
    PmsShadowSecurityMappingRow Mapping,
    string CanonicalSymbol,
    DateTimeOffset TargetCloseUtc);

internal static class InstitutionalCanonicalJson
{
    private static readonly UTF8Encoding Utf8 = new(false);

    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Default
    };

    public static string SerializeFile(object value) =>
        JsonSerializer.Serialize(value, Options).Replace("\r\n", "\n", StringComparison.Ordinal) +
        "\n";

    public static string FileSha256(object value) =>
        Convert.ToHexStringLower(SHA256.HashData(Utf8.GetBytes(SerializeFile(value))));
}
