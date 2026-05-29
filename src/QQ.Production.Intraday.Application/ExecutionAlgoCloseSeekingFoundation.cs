namespace QQ.Production.Intraday.Application;

public enum CloseConstructionMethod
{
    LastValidQuoteBeforeClose,
    LastValidMidBeforeClose,
    BidAskClose,
    FixtureClose,
    InconclusiveSafe
}

public enum CloseBenchmarkAvailabilityStatus
{
    CloseConstructedFromValidQuote,
    MissingCloseBenchmark,
    StaleCloseBenchmark,
    CloseUnavailable,
    InconclusiveSafe
}

public enum CloseSourceCategory
{
    Fixture,
    SanitizedQuoteSummary,
    InconclusiveSafe
}

public enum FeedGapCategory
{
    NoGap,
    MinorGap,
    MajorGap,
    NoQuoteNearClose,
    InconclusiveSafe
}

public enum FeedReadinessStatus
{
    ReadyForCloseBenchmark,
    MissingBidAsk,
    StaleQuotes,
    InsufficientQuotes,
    SpreadTooWide,
    NoQuoteNearClose,
    MissingFeedContinuity,
    InconclusiveSafe
}

public enum ExecutionAlgoFamily
{
    DoNotTrade,
    ManualReview,
    PassiveUntilUrgency,
    CloseSeeking15m,
    CloseSeeking15mAdaptive,
    ControlledResidualCross,
    ImmediatePaperBenchmark,
    TWAPBenchmarkOnly,
    VWAPBenchmarkOnly
}

public enum ExecutionAlgoDesignStatus
{
    DesignOnly,
    BlockedUnsafeDefault,
    InconclusiveSafe
}

public enum CloseSeekingPhaseName
{
    PassiveOpportunistic,
    AdaptiveUrgency,
    ControlledResidualCompletion
}

public enum SafeExecutionAlgoReasonCategory
{
    MissingCloseBenchmark,
    MissingFeedContinuity,
    MissingBidAsk,
    MissingMark,
    StaleMark,
    StaleQuoteNearClose,
    NoQuoteNearClose,
    SpreadTooWide,
    SlippageLimitExceeded,
    NonFillRiskTooHigh,
    OpportunityCostTooHigh,
    ResidualTooLargeNearClose,
    NotionalTooSmall,
    NotionalTooLarge,
    UnsupportedInstrument,
    MissingInstrumentConvention,
    RequiresManualReview,
    InconclusiveSafe
}

public enum WakettFailurePattern
{
    PureLimitUntilClose,
    MechanicalMarketSlicesAroundClose,
    BlindFiveMarketOrdersAroundClose,
    BlindFiveMarketOrdersAtOneMinuteIntervals,
    AlwaysMarketAtClose,
    BlindMarketExecution
}

public sealed record Close15mBenchmark(
    string BarId,
    DateTimeOffset BarWindowStartUtc,
    DateTimeOffset BarWindowEndUtc,
    DateTimeOffset TargetCloseTimestampUtc,
    DateTimeOffset DecisionTimestampUtc,
    DateTimeOffset KnownAtTimestampUtc,
    TimeSpan TimeKnownBeforeClose,
    decimal? CloseMid,
    decimal? CloseBid,
    decimal? CloseAsk,
    decimal? CloseSpreadBps,
    CloseSourceCategory CloseSourceCategory,
    CloseBenchmarkAvailabilityStatus BenchmarkAvailabilityStatus,
    CloseConstructionMethod CloseConstructionMethod,
    SafeExecutionAlgoReasonCategory? MissingCloseBenchmarkHandling,
    SafeExecutionAlgoReasonCategory? StaleCloseBenchmarkHandling);

public sealed record FeedContinuityRequirement(
    IReadOnlyList<string> RequiredQuoteFields,
    int MinimumQuoteCountInWindow,
    int MinimumQuoteCountLastMinute,
    TimeSpan MaxQuoteGapBeforeClose,
    TimeSpan MaxQuoteStaleness,
    decimal MaxSpreadBps,
    bool HeartbeatRequired,
    bool SessionContinuityRequired,
    FeedGapCategory GapCategory,
    FeedReadinessStatus FeedReadinessStatus);

public sealed record CloseSlippageMeasurement(
    string CloseBenchmarkId,
    decimal? CloseBenchmarkMid,
    decimal? DecisionMidBenchmark,
    decimal? ArrivalMidBenchmark,
    decimal ExpectedSpreadCostBps,
    decimal ExpectedMarketImpactBps,
    decimal TimingCostBps,
    decimal OpportunityCostBps,
    decimal ImplementationShortfallVs15mCloseBps,
    decimal ExpectedCloseSlippageBps,
    decimal MaxAllowedCloseSlippageBps,
    decimal? FillRatioPlaceholder,
    decimal? ResidualQuantityPlaceholder,
    string CompletionStatusPlaceholder);

public sealed record ExecutionCostEstimate(
    decimal ExpectedSpreadCostBps,
    decimal ExpectedOpportunityCostBps,
    decimal ExpectedNonFillRiskBps,
    decimal ExpectedResidualRiskBps,
    decimal ExpectedMarketImpactBps);

public sealed record ExecutionQualityLine(
    string InstrumentId,
    string Side,
    decimal PaperBaseQuantity,
    string QuantityCurrency,
    CloseSlippageMeasurement CloseSlippageMeasurement,
    ExecutionCostEstimate ExecutionCostEstimate,
    bool NotAnOrder,
    bool NonExecutable,
    bool NotSubmitted,
    bool NoBrokerRoute);

public sealed record ExecutionQualityReport(
    string ExecutionQualityReportId,
    IReadOnlyList<ExecutionQualityLine> Lines,
    bool HasCloseSlippageMeasurement,
    bool HasSpreadCostEstimate,
    bool HasOpportunityCostEstimate,
    bool HasNonFillRiskEstimate,
    bool HasResidualRiskEstimate,
    string CompletionPolicy,
    bool PaperOnly,
    bool NonExecutable,
    bool NotSubmitted,
    bool NoBrokerRoute);

public sealed record ExecutionAlgoFamilyDesign(
    ExecutionAlgoFamily Family,
    ExecutionAlgoDesignStatus DesignStatus,
    bool DesignOnly,
    bool PaperOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool IsDefaultCandidate);

public sealed record CloseSeeking15mPhase(
    CloseSeekingPhaseName PhaseName,
    TimeSpan StartsBeforeClose,
    TimeSpan EndsBeforeClose,
    string Purpose,
    IReadOnlyList<string> DecisionInputs,
    bool NonExecutable,
    bool NotAnOrder,
    bool NoBrokerRoute);

public sealed record BlockedWakettPattern(
    WakettFailurePattern Pattern,
    string BlockReason,
    bool BlockedAsUnsafeDefault,
    bool NonExecutable,
    bool NotSubmitted,
    bool NoBrokerRoute);

public sealed record ExecutionAlgoSelectionInput(
    string CycleRunId,
    string QubesRunId,
    string PaperExecutionPlanId,
    string PaperCandidateId,
    string RebalanceIntentId,
    string RiskReviewId,
    string LotSizingId,
    decimal ResidualQuantity,
    TimeSpan TimeToClose,
    decimal ExpectedSpreadCostBps,
    decimal ExpectedOpportunityCostBps,
    decimal ExpectedCloseSlippageBps,
    decimal FillProbability,
    decimal MaxSpreadBps,
    decimal MaxCloseSlippageBps,
    decimal MaxResidualAllowedAtClose,
    FeedContinuityRequirement MinimumQuoteContinuityRequirement,
    decimal ManualReviewThresholdBps,
    decimal DoNotTradeThresholdBps);

public sealed record ExecutionAlgoSelectionDecision(
    ExecutionAlgoFamily SelectedFamily,
    SafeExecutionAlgoReasonCategory? ReasonCategory,
    bool DesignOnly,
    bool PaperOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool CreatesOmsOrder,
    bool CreatesParentOrder,
    bool CreatesChildOrder,
    bool CreatesBrokerOrder,
    bool CreatesFill,
    bool CreatesExecutionReport,
    bool SubmitsOrder);

public sealed record ExistingExecutionModelInventory(
    IReadOnlyList<string> ExecutionModels,
    IReadOnlyList<string> OmsOrderModels,
    IReadOnlyList<string> FillModels,
    IReadOnlyList<string> ExecutionReportModels,
    IReadOnlyList<string> SlippageTcaModels,
    IReadOnlyList<string> RiskModels,
    IReadOnlyList<string> PaperExecutionModels,
    bool ExistingModelsInventoried);

public sealed record ExecutionAlgoFoundationLineage(
    string CycleRunId,
    string QubesRunId,
    string PaperExecutionPlanId,
    string PaperCandidateId,
    string RebalanceIntentId,
    string RiskReviewId,
    string LotSizingId,
    bool PmsEmsOmsLineagePreserved);

public sealed record ExecutionAlgoCloseSeekingFoundation(
    ExistingExecutionModelInventory ExistingInventory,
    Close15mBenchmark CloseBenchmark,
    FeedContinuityRequirement FeedContinuityRequirement,
    ExecutionQualityReport ExecutionQualityReport,
    IReadOnlyList<ExecutionAlgoFamilyDesign> AlgoFamilies,
    IReadOnlyList<CloseSeeking15mPhase> CloseSeeking15mPhases,
    IReadOnlyList<BlockedWakettPattern> BlockedWakettPatterns,
    ExecutionAlgoSelectionInput SelectionInput,
    ExecutionAlgoSelectionDecision SelectionDecision,
    ExecutionAlgoFoundationLineage Lineage,
    bool NoExternal,
    bool NoOrdersCreated,
    bool NoFillsCreated,
    bool NoExecutionReportsCreated,
    bool NoBrokerRoute,
    bool NoSubmission);

public static class ExecutionAlgoR001Foundation
{
    public static ExecutionAlgoCloseSeekingFoundation CreateFixture()
    {
        var barStart = new DateTimeOffset(2026, 05, 20, 14, 45, 00, TimeSpan.Zero);
        var barEnd = barStart.AddMinutes(15);
        var decisionAt = barEnd.AddMinutes(-13);

        var closeBenchmark = new Close15mBenchmark(
            "bar-20260520-1445-1500",
            barStart,
            barEnd,
            barEnd,
            decisionAt,
            decisionAt,
            TimeSpan.FromMinutes(13),
            CloseMid: null,
            CloseBid: null,
            CloseAsk: null,
            CloseSpreadBps: null,
            CloseSourceCategory.Fixture,
            CloseBenchmarkAvailabilityStatus.InconclusiveSafe,
            CloseConstructionMethod.InconclusiveSafe,
            SafeExecutionAlgoReasonCategory.MissingCloseBenchmark,
            SafeExecutionAlgoReasonCategory.StaleMark);

        var feedRequirement = new FeedContinuityRequirement(
            ["Bid", "Ask", "Mid", "Timestamp", "Instrument"],
            MinimumQuoteCountInWindow: 30,
            MinimumQuoteCountLastMinute: 4,
            MaxQuoteGapBeforeClose: TimeSpan.FromSeconds(20),
            MaxQuoteStaleness: TimeSpan.FromSeconds(5),
            MaxSpreadBps: 4.0m,
            HeartbeatRequired: true,
            SessionContinuityRequired: true,
            FeedGapCategory.InconclusiveSafe,
            FeedReadinessStatus.InconclusiveSafe);

        var slippage = new CloseSlippageMeasurement(
            closeBenchmark.BarId,
            CloseBenchmarkMid: null,
            DecisionMidBenchmark: null,
            ArrivalMidBenchmark: null,
            ExpectedSpreadCostBps: 0.8m,
            ExpectedMarketImpactBps: 0.2m,
            TimingCostBps: 0.0m,
            OpportunityCostBps: 1.0m,
            ImplementationShortfallVs15mCloseBps: 0.0m,
            ExpectedCloseSlippageBps: 1.8m,
            MaxAllowedCloseSlippageBps: 3.0m,
            FillRatioPlaceholder: null,
            ResidualQuantityPlaceholder: null,
            CompletionStatusPlaceholder: "DesignOnlyNotRun");

        var qualityLine = new ExecutionQualityLine(
            "AUDUSD",
            "Buy",
            131000m,
            "AUD",
            slippage,
            new ExecutionCostEstimate(0.8m, 1.0m, 1.2m, 0.5m, 0.2m),
            NotAnOrder: true,
            NonExecutable: true,
            NotSubmitted: true,
            NoBrokerRoute: true);

        var families = Enum.GetValues<ExecutionAlgoFamily>()
            .Select(family => new ExecutionAlgoFamilyDesign(
                family,
                ExecutionAlgoDesignStatus.DesignOnly,
                DesignOnly: true,
                PaperOnly: true,
                NonExecutable: true,
                NotAnOrder: true,
                NotSubmitted: true,
                NoBrokerRoute: true,
                IsDefaultCandidate: family is ExecutionAlgoFamily.CloseSeeking15m or ExecutionAlgoFamily.CloseSeeking15mAdaptive))
            .ToArray();

        var phases = new[]
        {
            new CloseSeeking15mPhase(
                CloseSeekingPhaseName.PassiveOpportunistic,
                TimeSpan.FromMinutes(13),
                TimeSpan.FromMinutes(5),
                "Attempt spread capture without accepting high non-fill risk.",
                ["residual", "spread", "feed continuity", "fill probability", "expected opportunity cost"],
                NonExecutable: true,
                NotAnOrder: true,
                NoBrokerRoute: true),
            new CloseSeeking15mPhase(
                CloseSeekingPhaseName.AdaptiveUrgency,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(1),
                "Increase urgency only when residual, drift, spread, continuity, fill probability, and opportunity cost justify it.",
                ["residual", "spread", "drift", "feed continuity", "fill probability", "expected opportunity cost", "expected close slippage"],
                NonExecutable: true,
                NotAnOrder: true,
                NoBrokerRoute: true),
            new CloseSeeking15mPhase(
                CloseSeekingPhaseName.ControlledResidualCompletion,
                TimeSpan.FromMinutes(1),
                TimeSpan.Zero,
                "Consider residual completion only when cost model justifies crossing near the benchmark close.",
                ["residual", "time to close", "expected spread cost", "expected opportunity cost", "max close slippage bps", "max residual allowed at close"],
                NonExecutable: true,
                NotAnOrder: true,
                NoBrokerRoute: true)
        };

        var blocked = new[]
        {
            new BlockedWakettPattern(WakettFailurePattern.PureLimitUntilClose, "High non-fill risk, high residual risk, and high opportunity cost; not allowed as default.", true, true, true, true),
            new BlockedWakettPattern(WakettFailurePattern.MechanicalMarketSlicesAroundClose, "Repeated bid/ask crossing creates systematic spread and slippage cost.", true, true, true, true),
            new BlockedWakettPattern(WakettFailurePattern.BlindFiveMarketOrdersAroundClose, "Five blind market orders around the close are blocked.", true, true, true, true),
            new BlockedWakettPattern(WakettFailurePattern.BlindFiveMarketOrdersAtOneMinuteIntervals, "One-minute blind market slicing repeats spread crossing without cost justification.", true, true, true, true),
            new BlockedWakettPattern(WakettFailurePattern.AlwaysMarketAtClose, "Always crossing at close is blocked unless residual completion is cost justified.", true, true, true, true),
            new BlockedWakettPattern(WakettFailurePattern.BlindMarketExecution, "Crossing spread without residual and opportunity-cost justification is blocked.", true, true, true, true)
        };

        var lineage = new ExecutionAlgoFoundationLineage(
            "cycle-r029-manual-paper-fixture",
            "qubes-r029-manual-fixture",
            "paper-execution-plan-r014-sample",
            "paper-candidate-r011-sample",
            "rebalance-intent-r003-sample",
            "paper-risk-review-r009-sample",
            "lot-sizing-r012-sample",
            PmsEmsOmsLineagePreserved: true);

        var selectionInput = new ExecutionAlgoSelectionInput(
            lineage.CycleRunId,
            lineage.QubesRunId,
            lineage.PaperExecutionPlanId,
            lineage.PaperCandidateId,
            lineage.RebalanceIntentId,
            lineage.RiskReviewId,
            lineage.LotSizingId,
            ResidualQuantity: 131000m,
            TimeToClose: TimeSpan.FromMinutes(13),
            ExpectedSpreadCostBps: 0.8m,
            ExpectedOpportunityCostBps: 1.0m,
            ExpectedCloseSlippageBps: 1.8m,
            FillProbability: 0.65m,
            MaxSpreadBps: 4.0m,
            MaxCloseSlippageBps: 3.0m,
            MaxResidualAllowedAtClose: 0m,
            MinimumQuoteContinuityRequirement: feedRequirement,
            ManualReviewThresholdBps: 2.5m,
            DoNotTradeThresholdBps: 5.0m);

        var selectionDecision = new ExecutionAlgoSelectionDecision(
            ExecutionAlgoFamily.CloseSeeking15m,
            SafeExecutionAlgoReasonCategory.InconclusiveSafe,
            DesignOnly: true,
            PaperOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            CreatesOmsOrder: false,
            CreatesParentOrder: false,
            CreatesChildOrder: false,
            CreatesBrokerOrder: false,
            CreatesFill: false,
            CreatesExecutionReport: false,
            SubmitsOrder: false);

        return new ExecutionAlgoCloseSeekingFoundation(
            CreateInventory(),
            closeBenchmark,
            feedRequirement,
            new ExecutionQualityReport(
                "exec-algo-r001-execution-quality-contract",
                [qualityLine],
                HasCloseSlippageMeasurement: true,
                HasSpreadCostEstimate: true,
                HasOpportunityCostEstimate: true,
                HasNonFillRiskEstimate: true,
                HasResidualRiskEstimate: true,
                CompletionPolicy: "DesignOnlyControlledResidualCompletion",
                PaperOnly: true,
                NonExecutable: true,
                NotSubmitted: true,
                NoBrokerRoute: true),
            families,
            phases,
            blocked,
            selectionInput,
            selectionDecision,
            lineage,
            NoExternal: true,
            NoOrdersCreated: true,
            NoFillsCreated: true,
            NoExecutionReportsCreated: true,
            NoBrokerRoute: true,
            NoSubmission: true);
    }

    public static FeedReadinessStatus EvaluateFeedReadiness(
        bool hasBid,
        bool hasAsk,
        int quoteCountInWindow,
        int quoteCountLastMinute,
        TimeSpan gapBeforeClose,
        TimeSpan quoteStaleness,
        decimal spreadBps,
        FeedContinuityRequirement requirement)
    {
        if (!hasBid || !hasAsk)
        {
            return FeedReadinessStatus.MissingBidAsk;
        }

        if (quoteCountLastMinute <= 0)
        {
            return FeedReadinessStatus.NoQuoteNearClose;
        }

        if (quoteStaleness > requirement.MaxQuoteStaleness)
        {
            return FeedReadinessStatus.StaleQuotes;
        }

        if (quoteCountInWindow < requirement.MinimumQuoteCountInWindow ||
            quoteCountLastMinute < requirement.MinimumQuoteCountLastMinute ||
            gapBeforeClose > requirement.MaxQuoteGapBeforeClose)
        {
            return FeedReadinessStatus.InsufficientQuotes;
        }

        if (spreadBps > requirement.MaxSpreadBps)
        {
            return FeedReadinessStatus.SpreadTooWide;
        }

        return FeedReadinessStatus.ReadyForCloseBenchmark;
    }

    public static ExecutionAlgoSelectionDecision BlockDecision(SafeExecutionAlgoReasonCategory reason)
        => new(
            ExecutionAlgoFamily.ManualReview,
            reason,
            DesignOnly: true,
            PaperOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            CreatesOmsOrder: false,
            CreatesParentOrder: false,
            CreatesChildOrder: false,
            CreatesBrokerOrder: false,
            CreatesFill: false,
            CreatesExecutionReport: false,
            SubmitsOrder: false);

    private static ExistingExecutionModelInventory CreateInventory()
        => new(
            ExecutionModels:
            [
                "IVenueExecutionGateway",
                "FakeLmaxGateway",
                "PaperExecutionPlanShape",
                "PaperExecutionPlanArchive",
                "ManualPaperCycleFixtureRunner"
            ],
            OmsOrderModels:
            [
                "ParentOrder",
                "ChildOrder",
                "PaperOrderCandidateShape"
            ],
            FillModels:
            [
                "Fill",
                "PaperSimulationFixtureExecutionResult"
            ],
            ExecutionReportModels:
            [
                "ExecutionReport",
                "PaperSimulationExecutionReportAudit"
            ],
            SlippageTcaModels:
            [
                "TheoreticalPnLFixture",
                "TheoreticalVsRealReport",
                "TargetVsCurrentDiff"
            ],
            RiskModels:
            [
                "RiskDecision",
                "PaperOmsIntentReview",
                "PaperExecutionPlanApproval"
            ],
            PaperExecutionModels:
            [
                "PaperOrderCandidateShape",
                "PaperCandidateLotSizing",
                "PaperExecutionPlanShape",
                "PaperSimulationPlanFixture",
                "PaperSimulationFixtureExecution",
                "PaperPositionPreview",
                "PaperLedgerState"
            ],
            ExistingModelsInventoried: true);
}
