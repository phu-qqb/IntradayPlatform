using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class OperationalReportingContract
{
    public const string Version = "anubis_infx_operational_reporting_v1";
    public const string BreakVersion = "anubis_infx_operational_break_v1";
    public const string NullCsvValue = "NULL";
    public const string TestEnvironment = "TEST";
    public const string RequiredAccountScope = "1754288005";
    public const string ShadowClassification = "SHADOW_ONLY";
    public const int ExpectedMarketObservationCount = 99;
    public const int ExpectedTargetPositionCount = 288;
    public const int ExpectedPositionOnlyDriftCount = 288;
    public const int ExpectedModelRunCount = 4;
    public const int ExpectedArch7aLineCount = 7;

    public static readonly string[] Strategies = ["INFX7", "INFX8", "INFX9", "INFX10"];
    public static readonly string[] FxSymbols =
        ["AUDUSD", "EURUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF", "USDJPY"];
}

public static class ReportingAuthority
{
    public const string Proven = "PROUVÉ";
    public const string Probable = "PROBABLE";
    public const string Unknown = "INCONNU";
    public const string Absent = "ABSENT";
    public const string Stale = "OBSOLÈTE";
}

public enum OperationalBreakSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public enum OperationalBreakStatus
{
    Active,
    Historical,
    ResolvedByLaterFact,
    Unknown
}

public sealed record OperationalStatusCodeDefinition(
    string ExactCode,
    string SourceComponent,
    string Category,
    OperationalBreakSeverity Severity,
    string Scope,
    string Description,
    string OperatorMeaning,
    bool AutomaticResolutionPossible,
    bool BlocksTrading,
    bool BlocksAccounting,
    string EvidenceRequirements,
    string? Supersedes,
    string IntroducedByContractVersion);

public sealed record OperationalBreak(
    string BreakId,
    string ExactCode,
    string Category,
    OperationalBreakSeverity Severity,
    OperationalBreakStatus Status,
    string Component,
    string ScopeType,
    string ScopeId,
    string? SlotId,
    string? StrategyId,
    Guid? EconomicRevisionId,
    Guid? InstrumentId,
    string? Symbol,
    Guid? TradeIntentId,
    Guid? QualificationRunId,
    string? OrderId,
    DateTimeOffset FirstObservedAtUtc,
    DateTimeOffset LastObservedAtUtc,
    string? EvidenceSha256,
    string AuthorityStatus,
    bool BlocksTrading,
    bool BlocksAccounting,
    string OperatorMeaning,
    string SuggestedInvestigation,
    string SourceTable,
    string SourceContractVersion);

public sealed record ReportingDatabaseIdentity(
    string Database,
    string PostgreSqlVersion,
    int PostgreSqlMajor,
    string Schema,
    int TableCount,
    long RowCount,
    IReadOnlyList<string> AppliedMigrations,
    bool TransactionReadOnly,
    bool PendingModelChanges,
    string TargetProfileId,
    string TargetFingerprint,
    string TargetKind,
    string TlsPolicy);

public sealed record ReportingSlotFact(
    string SlotId,
    DateTimeOffset SlotStartUtc,
    DateTimeOffset SlotEndUtc,
    string Status,
    DateTimeOffset ClaimedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? SourceSessionId,
    string? ArtifactSha256,
    string ClockAuthorityStatus,
    int? BboCoverageCount,
    int? InSlotEventCount,
    int? PostCloseExclusionCount,
    int? PolygonCount,
    string ReadyMarkerStatus,
    double? ImportStartLatencySeconds,
    double? ImportCompletionLatencySeconds,
    int? RevisionNumber,
    bool? Qualifying,
    bool NoOrder,
    string? ManifestSha256,
    string? FailureCode,
    string ContractVersion);

public sealed record ReportingModelRunFact(
    string StrategyId,
    Guid ModelRunId,
    Guid QubesInputSnapshotId,
    DateTimeOffset TargetCloseUtc,
    DateTimeOffset AsOfUtc,
    string OutputSha256,
    string CoreCommitId,
    string Classification,
    string FreshOrReusedStatus,
    string ScheduleStatus,
    int WeightCount,
    int TargetCount,
    int DriftCount,
    bool LineageComplete,
    string SourceContractVersion);

public sealed record ReportingEconomicRevisionFact(
    Guid EconomicRevisionId,
    int RevisionNumber,
    string SlotId,
    Guid SourceIngestionId,
    string SourceSessionId,
    string MarketDataSnapshotSha256,
    string? SupersedesManifestSha256,
    string Status,
    bool Qualifying,
    bool NoOrder,
    int ObservationCount,
    int TargetPositionCount,
    int PositionOnlyDriftCount,
    int ModelRunCount,
    string TargetSha256,
    string DriftSha256,
    string ManifestSha256,
    DateTimeOffset CompletedAtUtc);

public sealed record ReportingFxLineFact(
    Guid EconomicRevisionId,
    Guid InstrumentId,
    string PmsSecurityId,
    string CanonicalSymbol,
    string LmaxInstrumentId,
    string SecurityIdSource,
    string StrategyId,
    decimal TargetBaseQuantity,
    decimal TargetVenueQuantity,
    decimal? CurrentBaseQuantity,
    decimal? PositionOnlyDrift,
    decimal NetTarget,
    decimal NetDrift,
    string MappingAuthority,
    decimal? Bid,
    decimal? Ask,
    DateTimeOffset? PriceAsOfUtc,
    string Freshness);

public sealed record ReportingArch7aFact(
    Guid EconomicRevisionId,
    Guid TradeIntentId,
    Guid RiskDecisionId,
    Guid ParentOrderId,
    Guid ChildOrderId,
    string AccountScope,
    string Environment,
    string Classification,
    string ParentStatus,
    string ChildStatus,
    bool Actionable,
    bool ExecutionAllowed,
    bool BrokerRouteAllowed,
    bool BrokerSendAllowed,
    string PlanSha256,
    string ReplayResult,
    string Symbol,
    Guid InstrumentId);

public sealed record ReportingArch7bFact(
    Guid? QualificationRunId,
    string Status,
    string AuthorityStatus,
    string? AuthorizationPacketSha256,
    DateTimeOffset? LeaseExpiresAtUtc,
    int FixSessionEventCount,
    int OrderSendCount,
    int ExecutionReportCount,
    int FillCount,
    int PositionLedgerEventCount,
    int ReconciliationCount,
    decimal? KnownLeaves,
    decimal? FinalLedgerQuantity,
    decimal? BrokerResidualQuantity,
    int? CriticalBreakCount,
    string FinalGate,
    DateTimeOffset? CompletedAtUtc);

public sealed record OperationalReportingSnapshot(
    DateTimeOffset AsOfUtc,
    string RepositoryCommit,
    ReportingDatabaseIdentity Database,
    IReadOnlyList<ReportingSlotFact> Slots,
    IReadOnlyList<ReportingModelRunFact> ModelRuns,
    IReadOnlyList<ReportingEconomicRevisionFact> EconomicRevisions,
    IReadOnlyList<ReportingFxLineFact> FxLines,
    IReadOnlyList<ReportingArch7aFact> Arch7a,
    IReadOnlyList<ReportingArch7bFact> Arch7b,
    IReadOnlyList<string> ObservedCodes);

public sealed record OperationalSummary(
    DateTimeOffset GeneratedAtUtc,
    string RepositoryCommit,
    string TargetProfileId,
    string TargetFingerprint,
    string Database,
    string SchemaMigrationIdentity,
    string? LatestSlot,
    Guid? LatestQualifyingEconomicRevision,
    IReadOnlyList<string> LatestAnubisInfxModelSet,
    Guid? LatestArch7aQualification,
    Guid? LatestArch7bQualification,
    IReadOnlyDictionary<string, int> ActiveBreaksBySeverity,
    string GlobalOperationalStatus,
    string GlobalTradingReadiness,
    string GlobalReconciliationStatus,
    string SourceFreshness,
    IReadOnlyList<string> AuthorityGaps);

public sealed record ReconciliationReport(
    string Status,
    string AuthorityStatus,
    Guid? QualificationRunId,
    decimal? KnownWorkingLeaves,
    decimal? InternalLedgerQuantity,
    decimal? BrokerResidualQuantity,
    int CriticalBreakCount,
    string FinalGate,
    string? EvidenceSha256,
    DateTimeOffset? CompletedAtUtc);

public sealed record OperationalReportSet(
    OperationalSummary Summary,
    IReadOnlyList<OperationalStatusCodeDefinition> StatusCodeCatalog,
    IReadOnlyList<OperationalBreak> Breaks,
    IReadOnlyList<ReportingModelRunFact> ModelRuns,
    IReadOnlyList<ReportingSlotFact> Slots,
    IReadOnlyList<ReportingEconomicRevisionFact> EconomicRevisions,
    IReadOnlyList<ReportingFxLineFact> FxLines,
    IReadOnlyList<ReportingArch7aFact> Arch7a,
    IReadOnlyList<ReportingArch7bFact> Arch7b,
    ReconciliationReport Reconciliation);

public sealed record ReportingBundleFile(string Path, long SizeBytes, string Sha256);

public sealed record ReportingBundleManifest(
    string ContractVersion,
    string BreakContractVersion,
    DateTimeOffset AsOfUtc,
    string RepositoryCommit,
    string TargetProfileId,
    string TargetFingerprint,
    IReadOnlyList<ReportingBundleFile> Files,
    string BundleSha256,
    bool NoOrder,
    bool ReadOnly);

public sealed record ReportingBundleResult(
    string OutputDirectory,
    string BundleSha256,
    IReadOnlyList<ReportingBundleFile> Files);
