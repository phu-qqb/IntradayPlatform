namespace QQ.Production.Intraday.Application;

public enum ExecutionVenueCategory
{
    FixtureOnly,
    ReferenceOnly,
    InconclusiveSafe
}

public enum FxExecutionSide
{
    Buy,
    Sell,
    None
}

public enum UsdPairNormalizationStatus
{
    Ready,
    DirectCrossExecutionDisabled,
    MissingUsdPairExecutionMapping,
    MissingInversionTransform,
    MissingInstrumentConvention,
    UnsupportedInstrument,
    InconclusiveSafe
}

public enum ExecutionInstrumentConventionStatus
{
    Ready,
    RequiresInstrumentConvention,
    UnsupportedInstrument,
    InconclusiveSafe
}

public enum CostControlStatus
{
    Pass,
    Block,
    ManualReview,
    BenchmarkOnly,
    InconclusiveSafe
}

public enum CloseSeekingPhasePolicy
{
    PassiveOpportunisticWindow,
    AdaptiveUrgencyWindow,
    ControlledResidualCompletionWindow,
    BenchmarkOnly,
    ManualReviewRequired,
    DoNotTrade
}

public enum AlgoPolicyReasonCategory
{
    ReadyForPassiveUntilUrgency,
    ReadyForCloseSeeking15m,
    ReadyForCloseSeeking15mAdaptive,
    ReadyForControlledResidualCross,
    DirectCrossExecutionDisabled,
    MissingUsdPairExecutionMapping,
    MissingInversionTransform,
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
    WakettPatternBlocked,
    InconclusiveSafe
}

public enum PolicyCostStatus
{
    Acceptable,
    TooHigh,
    Missing,
    InconclusiveSafe
}

public sealed record MandatoryFxNettingScope(
    string QubesRunId,
    string BarId,
    DateTimeOffset TargetCloseTimestampUtc,
    string PortfolioAccountScope,
    bool NettingRequiredBeforeExecutionSelection,
    bool RawCrossesAreSignalsOnly,
    bool ExecutionUniverseUsdPairOnly,
    bool DirectCrossExecutionAllowedByDefault);

public sealed record ExecutionTradableSymbolMapping(
    string PortfolioCurrency,
    string PortfolioNormalizedSymbol,
    string? ExecutionTradableSymbol,
    ExecutionVenueCategory ExecutionVenueCategory,
    bool RequiresInversion,
    ExecutionInstrumentConventionStatus InstrumentConventionStatus,
    bool DirectCrossExecutionAllowed,
    string? DirectCrossBlockedReason,
    UsdPairNormalizationStatus NormalizationStatus);

public sealed record InversionSideTransform(
    string PortfolioNormalizedSymbol,
    string ExecutionTradableSymbol,
    bool RequiresInversion,
    FxExecutionSide PortfolioSide,
    FxExecutionSide ExecutionSide,
    string PortfolioQuantityCurrency,
    string ExecutionQuantityCurrency,
    string PortfolioNotionalCurrency,
    string ExecutionNotionalCurrency,
    bool TransformAvailable);

public sealed record UsdPairExecutionNormalizationLine(
    string PortfolioCurrency,
    string PortfolioNormalizedSymbol,
    string? ExecutionTradableSymbol,
    ExecutionVenueCategory ExecutionVenueCategory,
    bool RequiresInversion,
    FxExecutionSide PortfolioSide,
    FxExecutionSide ExecutionSide,
    string PortfolioQuantityCurrency,
    string ExecutionQuantityCurrency,
    string PortfolioNotionalCurrency,
    string ExecutionNotionalCurrency,
    string BenchmarkSymbol,
    string CloseBenchmarkSymbol,
    ExecutionInstrumentConventionStatus InstrumentConventionStatus,
    bool DirectCrossExecutionAllowed,
    string? DirectCrossBlockedReason,
    UsdPairNormalizationStatus NormalizationStatus);

public sealed record CloseSeekingAlgoPolicyLineInput(
    string AlgoSelectionDecisionId,
    string CycleRunId,
    string QubesRunId,
    string PaperExecutionPlanLineId,
    string InstrumentId,
    string PortfolioCurrency,
    string PortfolioNormalizedSymbol,
    decimal PaperBaseQuantity,
    DateTimeOffset TargetCloseTimestampUtc,
    DateTimeOffset KnownAtTimestampUtc,
    TimeSpan TimeKnownBeforeClose,
    CloseBenchmarkAvailabilityStatus BenchmarkAvailabilityStatus,
    FeedReadinessStatus FeedReadinessStatus,
    PolicyCostStatus SpreadCostStatus,
    PolicyCostStatus OpportunityCostStatus,
    PolicyCostStatus NonFillRiskStatus,
    PolicyCostStatus ResidualRiskStatus,
    decimal ExpectedSpreadCostBps,
    decimal ExpectedOpportunityCostBps,
    decimal ExpectedNonFillRiskBps,
    decimal ExpectedResidualRiskBps,
    decimal ExpectedCloseSlippageBps,
    decimal MaxAllowedCloseSlippageBps,
    decimal MaxSpreadBps,
    decimal MaxResidualAtClose,
    TimeSpan TimeToClose,
    decimal FillProbability,
    ExecutionAlgoFamily? RequestedFamily,
    bool RawDirectCrossExecutionInstrument,
    bool InversionTransformAvailable);

public sealed record CloseSeekingAlgoSelectionDecision(
    string AlgoSelectionDecisionId,
    string CycleRunId,
    string QubesRunId,
    string PaperExecutionPlanLineId,
    string InstrumentId,
    string PortfolioNormalizedSymbol,
    string? ExecutionTradableSymbol,
    bool RequiresInversion,
    FxExecutionSide PortfolioSide,
    FxExecutionSide ExecutionSide,
    decimal PaperBaseQuantity,
    DateTimeOffset TargetCloseTimestampUtc,
    DateTimeOffset KnownAtTimestampUtc,
    TimeSpan TimeKnownBeforeClose,
    FeedReadinessStatus FeedReadinessStatus,
    CloseBenchmarkAvailabilityStatus BenchmarkAvailabilityStatus,
    PolicyCostStatus SpreadCostStatus,
    PolicyCostStatus OpportunityCostStatus,
    PolicyCostStatus NonFillRiskStatus,
    PolicyCostStatus ResidualRiskStatus,
    ExecutionAlgoFamily SelectedAlgoFamily,
    CloseSeekingPhasePolicy SelectedPhasePolicy,
    AlgoPolicyReasonCategory ReasonCategory,
    CostControlStatus CostControlStatus,
    decimal ExpectedCloseSlippageBps,
    decimal MaxAllowedCloseSlippageBps,
    decimal MaxSpreadBps,
    decimal MaxResidualAtClose,
    bool IsDesignOnly,
    bool IsPaperOnly,
    bool IsExecutable,
    bool IsSubmitted,
    bool HasBrokerRoute,
    bool CreatesOrder,
    bool CreatesFill,
    bool CreatesExecutionReport,
    bool CreatesRoute,
    bool CreatesSubmission);

public sealed record CloseSeekingAlgoSelectionPolicyResult(
    MandatoryFxNettingScope NettingScope,
    IReadOnlyList<ExecutionTradableSymbolMapping> SymbolMappings,
    IReadOnlyList<CloseSeekingAlgoSelectionDecision> Decisions,
    bool DirectCrossExecutionDisabledByDefault,
    bool AllDecisionsDesignOnly,
    bool AllDecisionsPaperOnly,
    bool AllDecisionsNonExecutable,
    bool NoOrdersCreated,
    bool NoFillsCreated,
    bool NoExecutionReportsCreated,
    bool NoRoutesCreated,
    bool NoSubmissionsCreated);

public static class ExecutionAlgoR002UsdPairSelectionPolicy
{
    private static readonly IReadOnlyDictionary<string, (string Symbol, bool Inverted)> UsdPairMappings =
        new Dictionary<string, (string Symbol, bool Inverted)>(StringComparer.OrdinalIgnoreCase)
        {
            ["AUD"] = ("AUDUSD", false),
            ["EUR"] = ("EURUSD", false),
            ["GBP"] = ("GBPUSD", false),
            ["NZD"] = ("NZDUSD", false),
            ["JPY"] = ("USDJPY", true),
            ["CHF"] = ("USDCHF", true),
            ["CAD"] = ("USDCAD", true),
            ["MXN"] = ("USDMXN", true),
            ["CNH"] = ("USDCNH", true),
            ["NOK"] = ("USDNOK", true),
            ["SEK"] = ("USDSEK", true),
            ["ZAR"] = ("USDZAR", true)
        };

    public static MandatoryFxNettingScope CreateNettingScope()
        => new(
            "qubes-r002-exec-algo-fixture",
            "bar-r002-15m-close",
            new DateTimeOffset(2026, 05, 20, 15, 00, 00, TimeSpan.Zero),
            "QQ_MASTER",
            NettingRequiredBeforeExecutionSelection: true,
            RawCrossesAreSignalsOnly: true,
            ExecutionUniverseUsdPairOnly: true,
            DirectCrossExecutionAllowedByDefault: false);

    public static ExecutionTradableSymbolMapping MapCurrency(string portfolioCurrency)
    {
        if (portfolioCurrency.Equals("USD", StringComparison.OrdinalIgnoreCase))
        {
            return Missing(portfolioCurrency, UsdPairNormalizationStatus.MissingUsdPairExecutionMapping);
        }

        if (portfolioCurrency.Equals("SGD", StringComparison.OrdinalIgnoreCase))
        {
            return new ExecutionTradableSymbolMapping(
                portfolioCurrency,
                $"{portfolioCurrency}USD",
                null,
                ExecutionVenueCategory.FixtureOnly,
                RequiresInversion: false,
                ExecutionInstrumentConventionStatus.RequiresInstrumentConvention,
                DirectCrossExecutionAllowed: false,
                "SGD convention must be explicit before execution-normalized design selection.",
                UsdPairNormalizationStatus.MissingInstrumentConvention);
        }

        if (!UsdPairMappings.TryGetValue(portfolioCurrency, out var mapping))
        {
            return Missing(portfolioCurrency, UsdPairNormalizationStatus.UnsupportedInstrument);
        }

        return new ExecutionTradableSymbolMapping(
            portfolioCurrency,
            $"{portfolioCurrency}USD",
            mapping.Symbol,
            ExecutionVenueCategory.FixtureOnly,
            mapping.Inverted,
            ExecutionInstrumentConventionStatus.Ready,
            DirectCrossExecutionAllowed: false,
            null,
            UsdPairNormalizationStatus.Ready);
    }

    public static UsdPairExecutionNormalizationLine NormalizeExposure(
        string portfolioCurrency,
        decimal deltaQuantity,
        bool inversionTransformAvailable = true)
    {
        var mapping = MapCurrency(portfolioCurrency);
        var portfolioSide = deltaQuantity >= 0 ? FxExecutionSide.Buy : FxExecutionSide.Sell;
        var executionSide = mapping.RequiresInversion
            ? (portfolioSide == FxExecutionSide.Buy ? FxExecutionSide.Sell : FxExecutionSide.Buy)
            : portfolioSide;

        var status = mapping.NormalizationStatus;
        if (mapping.RequiresInversion && !inversionTransformAvailable)
        {
            status = UsdPairNormalizationStatus.MissingInversionTransform;
            executionSide = FxExecutionSide.None;
        }

        return new UsdPairExecutionNormalizationLine(
            mapping.PortfolioCurrency,
            mapping.PortfolioNormalizedSymbol,
            mapping.ExecutionTradableSymbol,
            mapping.ExecutionVenueCategory,
            mapping.RequiresInversion,
            portfolioSide,
            executionSide,
            mapping.PortfolioCurrency,
            mapping.RequiresInversion ? "USD" : mapping.PortfolioCurrency,
            "USD",
            "USD",
            mapping.ExecutionTradableSymbol ?? mapping.PortfolioNormalizedSymbol,
            mapping.ExecutionTradableSymbol ?? mapping.PortfolioNormalizedSymbol,
            mapping.InstrumentConventionStatus,
            mapping.DirectCrossExecutionAllowed,
            mapping.DirectCrossBlockedReason,
            status);
    }

    public static UsdPairExecutionNormalizationLine BlockRawDirectCross(string rawPair)
        => new(
            rawPair[..3],
            rawPair,
            null,
            ExecutionVenueCategory.FixtureOnly,
            RequiresInversion: false,
            FxExecutionSide.None,
            FxExecutionSide.None,
            rawPair[..3],
            rawPair[..3],
            "USD",
            "USD",
            rawPair,
            rawPair,
            ExecutionInstrumentConventionStatus.UnsupportedInstrument,
            DirectCrossExecutionAllowed: false,
            "Raw Qubes crosses are signal inputs only; mandatory currency netting and USD-pair normalization are required first.",
            UsdPairNormalizationStatus.DirectCrossExecutionDisabled);

    public static InversionSideTransform CreateInversionTransform(string portfolioCurrency, decimal deltaQuantity)
    {
        var line = NormalizeExposure(portfolioCurrency, deltaQuantity);
        return new InversionSideTransform(
            line.PortfolioNormalizedSymbol,
            line.ExecutionTradableSymbol ?? string.Empty,
            line.RequiresInversion,
            line.PortfolioSide,
            line.ExecutionSide,
            line.PortfolioQuantityCurrency,
            line.ExecutionQuantityCurrency,
            line.PortfolioNotionalCurrency,
            line.ExecutionNotionalCurrency,
            line.NormalizationStatus == UsdPairNormalizationStatus.Ready);
    }

    public static CloseSeekingAlgoSelectionDecision Select(CloseSeekingAlgoPolicyLineInput input)
    {
        var normalization = input.RawDirectCrossExecutionInstrument
            ? BlockRawDirectCross(input.PortfolioNormalizedSymbol)
            : NormalizeExposure(input.PortfolioCurrency, input.PaperBaseQuantity, input.InversionTransformAvailable);

        if (input.RequestedFamily is ExecutionAlgoFamily.TWAPBenchmarkOnly or ExecutionAlgoFamily.VWAPBenchmarkOnly)
        {
            return Decision(input, normalization, input.RequestedFamily.Value, CloseSeekingPhasePolicy.BenchmarkOnly, AlgoPolicyReasonCategory.ReadyForCloseSeeking15m, CostControlStatus.BenchmarkOnly);
        }

        if (input.RequestedFamily is ExecutionAlgoFamily.DoNotTrade && input.PortfolioNormalizedSymbol.Equals("PureLimitUntilClose", StringComparison.OrdinalIgnoreCase))
        {
            return Decision(input, normalization, ExecutionAlgoFamily.ManualReview, CloseSeekingPhasePolicy.ManualReviewRequired, AlgoPolicyReasonCategory.WakettPatternBlocked, CostControlStatus.Block);
        }

        if (input.RequestedFamily is ExecutionAlgoFamily.ImmediatePaperBenchmark && input.PortfolioNormalizedSymbol.Contains("FiveMarket", StringComparison.OrdinalIgnoreCase))
        {
            return Decision(input, normalization, ExecutionAlgoFamily.ManualReview, CloseSeekingPhasePolicy.ManualReviewRequired, AlgoPolicyReasonCategory.WakettPatternBlocked, CostControlStatus.Block);
        }

        if (input.RawDirectCrossExecutionInstrument || normalization.NormalizationStatus == UsdPairNormalizationStatus.DirectCrossExecutionDisabled)
        {
            return Decision(input, normalization, ExecutionAlgoFamily.ManualReview, CloseSeekingPhasePolicy.ManualReviewRequired, AlgoPolicyReasonCategory.DirectCrossExecutionDisabled, CostControlStatus.Block);
        }

        if (normalization.NormalizationStatus == UsdPairNormalizationStatus.MissingInstrumentConvention)
        {
            return Decision(input, normalization, ExecutionAlgoFamily.ManualReview, CloseSeekingPhasePolicy.ManualReviewRequired, AlgoPolicyReasonCategory.MissingInstrumentConvention, CostControlStatus.ManualReview);
        }

        if (normalization.NormalizationStatus is UsdPairNormalizationStatus.MissingUsdPairExecutionMapping or UsdPairNormalizationStatus.UnsupportedInstrument)
        {
            return Decision(input, normalization, ExecutionAlgoFamily.ManualReview, CloseSeekingPhasePolicy.ManualReviewRequired, AlgoPolicyReasonCategory.MissingUsdPairExecutionMapping, CostControlStatus.ManualReview);
        }

        if (normalization.NormalizationStatus == UsdPairNormalizationStatus.MissingInversionTransform)
        {
            return Decision(input, normalization, ExecutionAlgoFamily.ManualReview, CloseSeekingPhasePolicy.ManualReviewRequired, AlgoPolicyReasonCategory.MissingInversionTransform, CostControlStatus.ManualReview);
        }

        if (input.BenchmarkAvailabilityStatus != CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote)
        {
            return Decision(input, normalization, ExecutionAlgoFamily.ManualReview, CloseSeekingPhasePolicy.ManualReviewRequired, AlgoPolicyReasonCategory.MissingCloseBenchmark, CostControlStatus.ManualReview);
        }

        if (input.FeedReadinessStatus == FeedReadinessStatus.NoQuoteNearClose)
        {
            return Decision(input, normalization, ExecutionAlgoFamily.ManualReview, CloseSeekingPhasePolicy.ManualReviewRequired, AlgoPolicyReasonCategory.NoQuoteNearClose, CostControlStatus.ManualReview);
        }

        if (input.FeedReadinessStatus == FeedReadinessStatus.StaleQuotes)
        {
            return Decision(input, normalization, ExecutionAlgoFamily.ManualReview, CloseSeekingPhasePolicy.ManualReviewRequired, AlgoPolicyReasonCategory.StaleQuoteNearClose, CostControlStatus.ManualReview);
        }

        if (input.FeedReadinessStatus != FeedReadinessStatus.ReadyForCloseBenchmark)
        {
            return Decision(input, normalization, ExecutionAlgoFamily.ManualReview, CloseSeekingPhasePolicy.ManualReviewRequired, AlgoPolicyReasonCategory.MissingFeedContinuity, CostControlStatus.ManualReview);
        }

        if (input.SpreadCostStatus == PolicyCostStatus.TooHigh || input.ExpectedSpreadCostBps > input.MaxSpreadBps)
        {
            return Decision(input, normalization, ExecutionAlgoFamily.ManualReview, CloseSeekingPhasePolicy.ManualReviewRequired, AlgoPolicyReasonCategory.SpreadTooWide, CostControlStatus.ManualReview);
        }

        if (input.ExpectedCloseSlippageBps > input.MaxAllowedCloseSlippageBps)
        {
            return Decision(input, normalization, ExecutionAlgoFamily.ManualReview, CloseSeekingPhasePolicy.ManualReviewRequired, AlgoPolicyReasonCategory.SlippageLimitExceeded, CostControlStatus.ManualReview);
        }

        if (input.TimeToClose <= TimeSpan.FromMinutes(1) && input.ExpectedOpportunityCostBps > input.ExpectedSpreadCostBps && Math.Abs(input.PaperBaseQuantity) > input.MaxResidualAtClose)
        {
            return Decision(input, normalization, ExecutionAlgoFamily.ControlledResidualCross, CloseSeekingPhasePolicy.ControlledResidualCompletionWindow, AlgoPolicyReasonCategory.ReadyForControlledResidualCross, CostControlStatus.Pass);
        }

        if (Math.Abs(input.PaperBaseQuantity) >= 200000m && input.TimeToClose <= TimeSpan.FromMinutes(5))
        {
            return Decision(input, normalization, ExecutionAlgoFamily.CloseSeeking15mAdaptive, CloseSeekingPhasePolicy.AdaptiveUrgencyWindow, AlgoPolicyReasonCategory.ReadyForCloseSeeking15mAdaptive, CostControlStatus.Pass);
        }

        if (input.NonFillRiskStatus == PolicyCostStatus.Acceptable && input.ExpectedSpreadCostBps <= input.MaxSpreadBps / 2)
        {
            return Decision(input, normalization, ExecutionAlgoFamily.PassiveUntilUrgency, CloseSeekingPhasePolicy.PassiveOpportunisticWindow, AlgoPolicyReasonCategory.ReadyForPassiveUntilUrgency, CostControlStatus.Pass);
        }

        return Decision(input, normalization, ExecutionAlgoFamily.CloseSeeking15m, CloseSeekingPhasePolicy.AdaptiveUrgencyWindow, AlgoPolicyReasonCategory.ReadyForCloseSeeking15m, CostControlStatus.Pass);
    }

    public static CloseSeekingAlgoSelectionPolicyResult CreateFixturePolicyResult()
    {
        var scope = CreateNettingScope();
        var inputs = CreateFixtureInputs();
        var decisions = inputs.Select(Select).ToArray();
        var mappings = new[] { "AUD", "EUR", "GBP", "NZD", "JPY", "CHF", "CAD", "MXN", "CNH", "NOK", "SEK", "SGD", "ZAR" }
            .Select(MapCurrency)
            .ToArray();

        return new CloseSeekingAlgoSelectionPolicyResult(
            scope,
            mappings,
            decisions,
            DirectCrossExecutionDisabledByDefault: true,
            AllDecisionsDesignOnly: decisions.All(x => x.IsDesignOnly),
            AllDecisionsPaperOnly: decisions.All(x => x.IsPaperOnly),
            AllDecisionsNonExecutable: decisions.All(x => !x.IsExecutable),
            NoOrdersCreated: decisions.All(x => !x.CreatesOrder),
            NoFillsCreated: decisions.All(x => !x.CreatesFill),
            NoExecutionReportsCreated: decisions.All(x => !x.CreatesExecutionReport),
            NoRoutesCreated: decisions.All(x => !x.CreatesRoute),
            NoSubmissionsCreated: decisions.All(x => !x.CreatesSubmission));
    }

    public static IReadOnlyList<CloseSeekingAlgoPolicyLineInput> CreateFixtureInputs()
    {
        var targetClose = new DateTimeOffset(2026, 05, 20, 15, 00, 00, TimeSpan.Zero);
        var knownAt = targetClose.AddMinutes(-13);

        CloseSeekingAlgoPolicyLineInput Input(
            string id,
            string currency,
            string normalizedSymbol,
            decimal quantity,
            FeedReadinessStatus feed = FeedReadinessStatus.ReadyForCloseBenchmark,
            CloseBenchmarkAvailabilityStatus benchmark = CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote,
            PolicyCostStatus spread = PolicyCostStatus.Acceptable,
            decimal spreadBps = 0.8m,
            decimal opportunityBps = 1.2m,
            decimal closeSlipBps = 1.5m,
            TimeSpan? timeToClose = null,
            ExecutionAlgoFamily? requested = null,
            bool rawCross = false,
            bool inversionAvailable = true)
            => new(
                id,
                "cycle-r029-manual-paper-fixture",
                "qubes-r029-manual-fixture",
                $"{id}:paper-execution-plan-line",
                normalizedSymbol,
                currency,
                normalizedSymbol,
                quantity,
                targetClose,
                knownAt,
                TimeSpan.FromMinutes(13),
                benchmark,
                feed,
                spread,
                PolicyCostStatus.Acceptable,
                PolicyCostStatus.Acceptable,
                PolicyCostStatus.Acceptable,
                spreadBps,
                opportunityBps,
                0.5m,
                0.5m,
                closeSlipBps,
                3m,
                4m,
                10000m,
                timeToClose ?? TimeSpan.FromMinutes(13),
                0.7m,
                requested,
                rawCross,
                inversionAvailable);

        return
        [
            Input("scenario-direct-cross-eurgbp", "EUR", "EURGBP", 94000m, rawCross: true),
            Input("scenario-eur-good-feed", "EUR", "EURUSD", 124000m),
            Input("scenario-jpy-inversion", "JPY", "JPYUSD", 10000000m),
            Input("scenario-cad-inversion", "CAD", "CADUSD", -200000m),
            Input("scenario-sgd-missing-convention", "SGD", "SGDUSD", 100000m),
            Input("scenario-moderate-residual", "AUD", "AUDUSD", 220000m, timeToClose: TimeSpan.FromMinutes(4)),
            Input("scenario-high-residual-near-close", "GBP", "GBPUSD", -368000m, spreadBps: 0.9m, opportunityBps: 3.2m, timeToClose: TimeSpan.FromSeconds(45)),
            Input("scenario-wide-spread", "EUR", "EURUSD", 100000m, spread: PolicyCostStatus.TooHigh, spreadBps: 8m),
            Input("scenario-missing-close", "EUR", "EURUSD", 100000m, benchmark: CloseBenchmarkAvailabilityStatus.MissingCloseBenchmark),
            Input("scenario-missing-feed", "EUR", "EURUSD", 100000m, feed: FeedReadinessStatus.MissingFeedContinuity),
            Input("scenario-stale-quote", "EUR", "EURUSD", 100000m, feed: FeedReadinessStatus.StaleQuotes),
            Input("scenario-wakett-five-market-slices", "EUR", "FiveMarketSlicesAroundClose", 100000m, requested: ExecutionAlgoFamily.ImmediatePaperBenchmark),
            Input("scenario-wakett-pure-limit", "EUR", "PureLimitUntilClose", 100000m, requested: ExecutionAlgoFamily.DoNotTrade),
            Input("scenario-twap-benchmark", "EUR", "EURUSD", 100000m, requested: ExecutionAlgoFamily.TWAPBenchmarkOnly),
            Input("scenario-vwap-benchmark", "EUR", "EURUSD", 100000m, requested: ExecutionAlgoFamily.VWAPBenchmarkOnly)
        ];
    }

    private static CloseSeekingAlgoSelectionDecision Decision(
        CloseSeekingAlgoPolicyLineInput input,
        UsdPairExecutionNormalizationLine normalization,
        ExecutionAlgoFamily selectedFamily,
        CloseSeekingPhasePolicy phasePolicy,
        AlgoPolicyReasonCategory reason,
        CostControlStatus costControlStatus)
        => new(
            input.AlgoSelectionDecisionId,
            input.CycleRunId,
            input.QubesRunId,
            input.PaperExecutionPlanLineId,
            input.InstrumentId,
            normalization.PortfolioNormalizedSymbol,
            normalization.ExecutionTradableSymbol,
            normalization.RequiresInversion,
            normalization.PortfolioSide,
            normalization.ExecutionSide,
            input.PaperBaseQuantity,
            input.TargetCloseTimestampUtc,
            input.KnownAtTimestampUtc,
            input.TimeKnownBeforeClose,
            input.FeedReadinessStatus,
            input.BenchmarkAvailabilityStatus,
            input.SpreadCostStatus,
            input.OpportunityCostStatus,
            input.NonFillRiskStatus,
            input.ResidualRiskStatus,
            selectedFamily,
            phasePolicy,
            reason,
            costControlStatus,
            input.ExpectedCloseSlippageBps,
            input.MaxAllowedCloseSlippageBps,
            input.MaxSpreadBps,
            input.MaxResidualAtClose,
            IsDesignOnly: true,
            IsPaperOnly: true,
            IsExecutable: false,
            IsSubmitted: false,
            HasBrokerRoute: false,
            CreatesOrder: false,
            CreatesFill: false,
            CreatesExecutionReport: false,
            CreatesRoute: false,
            CreatesSubmission: false);

    private static ExecutionTradableSymbolMapping Missing(string portfolioCurrency, UsdPairNormalizationStatus status)
        => new(
            portfolioCurrency,
            $"{portfolioCurrency}USD",
            null,
            ExecutionVenueCategory.FixtureOnly,
            RequiresInversion: false,
            ExecutionInstrumentConventionStatus.UnsupportedInstrument,
            DirectCrossExecutionAllowed: false,
            "USD-pair execution mapping is missing or unsupported.",
            status);
}
