namespace QQ.Production.Intraday.Application;

public enum ExecutionSimQuotePathScenario
{
    NormalLiquid,
    WideSpreadNearClose,
    QuoteGapNearClose,
    StaleQuoteNearClose,
    FavorableDrift,
    AdverseDrift,
    LowPassiveFillProbability,
    ResidualHighNearClose
}

public enum ExecutionSimPolicy
{
    WakettPureLimitUntilClose,
    WakettFiveMarketSlicesAroundClose,
    PassiveUntilUrgency,
    CloseSeeking15m,
    CloseSeeking15mAdaptive,
    ControlledResidualCross,
    ImmediatePaperBenchmark,
    TWAPBenchmarkOnly,
    VWAPBenchmarkOnly,
    DoNotTrade,
    ManualReview
}

public enum SimulationOutcomeStatus
{
    CompletedFixtureOnly,
    BenchmarkOnly,
    ManualReviewSafe,
    BlockedUnsafePattern,
    InconclusiveSafe
}

public enum CostBucketStatus
{
    MajorUsdPairCostBucket,
    NonMajorUsdPairCostBucket,
    RequiresLiquidityCalibration,
    InconclusiveSafe
}

public sealed record ExecutionSimQuote(
    string InstrumentId,
    string ExecutionTradableSymbol,
    DateTimeOffset TimestampUtc,
    decimal Bid,
    decimal Ask,
    decimal Mid,
    decimal SpreadBps,
    TimeSpan QuoteAge,
    FeedReadinessStatus FeedStatus,
    DateTimeOffset BarWindowStartUtc,
    DateTimeOffset BarWindowEndUtc,
    DateTimeOffset TargetCloseTimestampUtc,
    DateTimeOffset KnownAtTimestampUtc);

public sealed record ExecutionSimQuotePathFixture(
    string QuotePathFixtureId,
    ExecutionSimQuotePathScenario Scenario,
    IReadOnlyList<ExecutionSimQuote> Quotes,
    bool FixtureOnly,
    bool NoExternal,
    bool NoLiveMarketData);

public sealed record ExecutionSimCloseBenchmarkFixture(
    string BenchmarkId,
    string InstrumentId,
    string ExecutionTradableSymbol,
    DateTimeOffset TargetCloseTimestampUtc,
    decimal? CloseMid,
    decimal? CloseBid,
    decimal? CloseAsk,
    decimal? CloseSpreadBps,
    CloseBenchmarkAvailabilityStatus AvailabilityStatus,
    CloseConstructionMethod ConstructionMethod,
    bool FixtureOnly);

public sealed record ExecutionSimPolicyContract(
    ExecutionSimPolicy Policy,
    bool FixtureOnly,
    bool PaperOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoRealFill,
    bool NoExecutionReport,
    bool NegativeBaseline);

public sealed record ExecutionSimResultLine(
    string SimulationResultLineId,
    string InstrumentId,
    string ExecutionTradableSymbol,
    FxExecutionSide Side,
    ExecutionSimQuotePathScenario Scenario,
    ExecutionSimPolicy Policy,
    decimal FillRatio,
    decimal PassiveFillRatio,
    decimal AggressiveFillRatio,
    decimal ResidualAtClose,
    decimal? SimulatedAveragePrice,
    decimal? Close15mBenchmark,
    decimal SlippageVsCloseBps,
    decimal SpreadPaidBps,
    decimal EstimatedSpreadCost,
    decimal EstimatedOpportunityCost,
    decimal EstimatedNonFillCost,
    decimal EstimatedResidualCost,
    decimal ImplementationShortfallVsDecisionBps,
    FeedGapCategory QuoteGapStatus,
    SafeExecutionAlgoReasonCategory? StalenessStatus,
    FeedReadinessStatus FeedReadinessStatus,
    CloseBenchmarkAvailabilityStatus BenchmarkAvailabilityStatus,
    SimulationOutcomeStatus SimulationOutcomeStatus,
    bool FixtureOnly,
    bool PaperOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoRealFill,
    bool NoExecutionReport);

public sealed record ExecutionSimTcaReport(
    string TcaReportId,
    IReadOnlyList<ExecutionSimResultLine> Lines,
    bool IncludesSlippageVsClose,
    bool IncludesSpreadPaid,
    bool IncludesResidualAtClose,
    bool IncludesFillRatio,
    bool FixtureOnly,
    bool PaperOnly,
    bool NoOrdersCreated,
    bool NoRealFillsCreated,
    bool NoExecutionReportsCreated,
    bool NoRoutesCreated,
    bool NoSubmissionsCreated);

public sealed record ExecutionSimCostBucketCalibration(
    decimal BestCaseMajorTargetUsdPerMillion,
    decimal BaseCaseMajorTargetUsdPerMillionLow,
    decimal BaseCaseMajorTargetUsdPerMillionHigh,
    decimal StressMajorTargetUsdPerMillionLow,
    decimal StressMajorTargetUsdPerMillionHigh,
    bool FiveUsdPerMillionIsBestCaseOnly,
    bool FiveUsdPerMillionUniversalized,
    CostBucketStatus MajorBucketStatus,
    CostBucketStatus NonMajorBucketStatus);

public static class ExecutionSimR001CloseSeekingFoundation
{
    private static readonly DateTimeOffset BarStart = new(2026, 05, 20, 14, 45, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset Close = BarStart.AddMinutes(15);
    private static readonly DateTimeOffset KnownAt = Close.AddMinutes(-13);

    public static ExecutionSimQuotePathFixture CreateQuotePath(ExecutionSimQuotePathScenario scenario)
    {
        var quotes = scenario switch
        {
            ExecutionSimQuotePathScenario.QuoteGapNearClose => CreateQuotes(scenario, 1.1000m, 0.8m, includeCloseQuote: false, finalQuoteAgeSeconds: 90),
            ExecutionSimQuotePathScenario.StaleQuoteNearClose => CreateQuotes(scenario, 1.1000m, 0.8m, includeCloseQuote: true, finalQuoteAgeSeconds: 45, FeedReadinessStatus.StaleQuotes),
            ExecutionSimQuotePathScenario.WideSpreadNearClose => CreateQuotes(scenario, 1.1000m, 9.5m, includeCloseQuote: true),
            ExecutionSimQuotePathScenario.FavorableDrift => CreateQuotes(scenario, 1.1010m, 0.7m, includeCloseQuote: true, driftBps: -1.2m),
            ExecutionSimQuotePathScenario.AdverseDrift => CreateQuotes(scenario, 1.0990m, 0.9m, includeCloseQuote: true, driftBps: 2.0m),
            ExecutionSimQuotePathScenario.LowPassiveFillProbability => CreateQuotes(scenario, 1.1000m, 0.8m, includeCloseQuote: true),
            ExecutionSimQuotePathScenario.ResidualHighNearClose => CreateQuotes(scenario, 1.1000m, 0.9m, includeCloseQuote: true),
            _ => CreateQuotes(scenario, 1.1000m, 0.8m, includeCloseQuote: true)
        };

        return new ExecutionSimQuotePathFixture(
            $"exec-sim-r001-{scenario.ToString().ToLowerInvariant()}-quote-path",
            scenario,
            quotes,
            FixtureOnly: true,
            NoExternal: true,
            NoLiveMarketData: true);
    }

    public static ExecutionSimCloseBenchmarkFixture CreateCloseBenchmark(ExecutionSimQuotePathFixture path)
    {
        var closeQuote = path.Quotes
            .Where(x => x.TimestampUtc <= Close && Close - x.TimestampUtc <= TimeSpan.FromSeconds(5) && x.QuoteAge <= TimeSpan.FromSeconds(5))
            .OrderByDescending(x => x.TimestampUtc)
            .FirstOrDefault();

        if (closeQuote is null)
        {
            return new ExecutionSimCloseBenchmarkFixture(
                $"{path.QuotePathFixtureId}:close-benchmark",
                "EURUSD",
                "EURUSD",
                Close,
                null,
                null,
                null,
                null,
                CloseBenchmarkAvailabilityStatus.CloseUnavailable,
                CloseConstructionMethod.InconclusiveSafe,
                FixtureOnly: true);
        }

        return new ExecutionSimCloseBenchmarkFixture(
            $"{path.QuotePathFixtureId}:close-benchmark",
            closeQuote.InstrumentId,
            closeQuote.ExecutionTradableSymbol,
            Close,
            closeQuote.Mid,
            closeQuote.Bid,
            closeQuote.Ask,
            closeQuote.SpreadBps,
            closeQuote.SpreadBps > 4m ? CloseBenchmarkAvailabilityStatus.InconclusiveSafe : CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote,
            CloseConstructionMethod.FixtureClose,
            FixtureOnly: true);
    }

    public static ExecutionSimResultLine Simulate(ExecutionSimPolicy policy, ExecutionSimQuotePathScenario scenario)
    {
        var path = CreateQuotePath(scenario);
        var benchmark = CreateCloseBenchmark(path);
        var spreadTooWide = path.Quotes.Last().SpreadBps > 4m;
        var noQuote = benchmark.AvailabilityStatus == CloseBenchmarkAvailabilityStatus.CloseUnavailable;
        var stale = path.Quotes.Last().FeedStatus == FeedReadinessStatus.StaleQuotes;
        var residualHigh = scenario == ExecutionSimQuotePathScenario.ResidualHighNearClose;
        var lowPassive = scenario == ExecutionSimQuotePathScenario.LowPassiveFillProbability;

        decimal fillRatio;
        decimal passiveFillRatio;
        decimal aggressiveFillRatio;
        decimal residual;
        decimal spreadPaid;
        decimal opportunity;
        decimal nonFill;
        decimal residualCost;
        SimulationOutcomeStatus outcome;

        switch (policy)
        {
            case ExecutionSimPolicy.WakettPureLimitUntilClose:
                passiveFillRatio = lowPassive ? 0.20m : 0.55m;
                aggressiveFillRatio = 0m;
                fillRatio = passiveFillRatio;
                residual = 1m - fillRatio;
                spreadPaid = 0.1m;
                opportunity = residual * 3.0m;
                nonFill = residual * 4.0m;
                residualCost = residual * 3.5m;
                outcome = SimulationOutcomeStatus.BlockedUnsafePattern;
                break;
            case ExecutionSimPolicy.WakettFiveMarketSlicesAroundClose:
                passiveFillRatio = 0m;
                aggressiveFillRatio = noQuote ? 0m : 1m;
                fillRatio = aggressiveFillRatio;
                residual = 1m - fillRatio;
                spreadPaid = 5.0m;
                opportunity = 0.2m;
                nonFill = residual * 0.2m;
                residualCost = residual * 0.5m;
                outcome = SimulationOutcomeStatus.BlockedUnsafePattern;
                break;
            case ExecutionSimPolicy.ControlledResidualCross:
                passiveFillRatio = residualHigh ? 0.45m : 0.65m;
                aggressiveFillRatio = residualHigh ? 0.50m : 0.15m;
                fillRatio = Math.Min(1m, passiveFillRatio + aggressiveFillRatio);
                residual = 1m - fillRatio;
                spreadPaid = residualHigh ? 1.2m : 0.8m;
                opportunity = residualHigh ? 3.2m : 0.8m;
                nonFill = residual * 1.0m;
                residualCost = residual * 1.2m;
                outcome = residualHigh && opportunity > spreadPaid ? SimulationOutcomeStatus.CompletedFixtureOnly : SimulationOutcomeStatus.ManualReviewSafe;
                break;
            case ExecutionSimPolicy.CloseSeeking15mAdaptive:
                passiveFillRatio = noQuote || stale || spreadTooWide ? 0.35m : 0.70m;
                aggressiveFillRatio = residualHigh || scenario == ExecutionSimQuotePathScenario.AdverseDrift ? 0.20m : 0.10m;
                fillRatio = Math.Min(1m, passiveFillRatio + aggressiveFillRatio);
                residual = 1m - fillRatio;
                spreadPaid = spreadTooWide ? 3.5m : 0.9m;
                opportunity = scenario == ExecutionSimQuotePathScenario.AdverseDrift ? 1.9m : 0.9m;
                nonFill = residual * 1.5m;
                residualCost = residual * 1.2m;
                outcome = noQuote || stale || spreadTooWide ? SimulationOutcomeStatus.ManualReviewSafe : SimulationOutcomeStatus.CompletedFixtureOnly;
                break;
            case ExecutionSimPolicy.TWAPBenchmarkOnly:
            case ExecutionSimPolicy.VWAPBenchmarkOnly:
            case ExecutionSimPolicy.ImmediatePaperBenchmark:
                passiveFillRatio = 0m;
                aggressiveFillRatio = 0m;
                fillRatio = 0m;
                residual = 1m;
                spreadPaid = 0m;
                opportunity = 0m;
                nonFill = 0m;
                residualCost = 0m;
                outcome = SimulationOutcomeStatus.BenchmarkOnly;
                break;
            default:
                passiveFillRatio = noQuote || stale || spreadTooWide ? 0.35m : 0.65m;
                aggressiveFillRatio = noQuote || stale || spreadTooWide ? 0m : 0.20m;
                fillRatio = Math.Min(1m, passiveFillRatio + aggressiveFillRatio);
                residual = 1m - fillRatio;
                spreadPaid = spreadTooWide ? 2.8m : 0.8m;
                opportunity = residual * 1.4m;
                nonFill = residual * 1.8m;
                residualCost = residual * 1.5m;
                outcome = noQuote || stale || spreadTooWide ? SimulationOutcomeStatus.ManualReviewSafe : SimulationOutcomeStatus.CompletedFixtureOnly;
                break;
        }

        var closeMid = benchmark.CloseMid;
        var average = closeMid is null ? null : closeMid + ((spreadPaid / 10000m) * closeMid);
        var gapStatus = noQuote ? FeedGapCategory.NoQuoteNearClose : FeedGapCategory.NoGap;
        SafeExecutionAlgoReasonCategory? staleStatus = stale ? SafeExecutionAlgoReasonCategory.StaleQuoteNearClose : null;
        var feedStatus = stale ? FeedReadinessStatus.StaleQuotes : noQuote ? FeedReadinessStatus.NoQuoteNearClose : path.Quotes.Last().FeedStatus;
        if (spreadTooWide)
        {
            feedStatus = FeedReadinessStatus.SpreadTooWide;
        }

        return new ExecutionSimResultLine(
            $"exec-sim-r001-{policy}-{scenario}",
            "EURUSD",
            "EURUSD",
            FxExecutionSide.Buy,
            scenario,
            policy,
            fillRatio,
            passiveFillRatio,
            AggressiveFillRatio: aggressiveFillRatio,
            ResidualAtClose: residual,
            SimulatedAveragePrice: average,
            Close15mBenchmark: closeMid,
            SlippageVsCloseBps: spreadPaid + opportunity,
            SpreadPaidBps: spreadPaid,
            EstimatedSpreadCost: spreadPaid,
            EstimatedOpportunityCost: opportunity,
            EstimatedNonFillCost: nonFill,
            EstimatedResidualCost: residualCost,
            ImplementationShortfallVsDecisionBps: spreadPaid + opportunity + nonFill + residualCost,
            gapStatus,
            staleStatus,
            feedStatus,
            benchmark.AvailabilityStatus,
            outcome,
            FixtureOnly: true,
            PaperOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoRealFill: true,
            NoExecutionReport: true);
    }

    public static ExecutionSimTcaReport CreateTcaReport()
    {
        var lines = new[]
        {
            Simulate(ExecutionSimPolicy.WakettPureLimitUntilClose, ExecutionSimQuotePathScenario.LowPassiveFillProbability),
            Simulate(ExecutionSimPolicy.WakettFiveMarketSlicesAroundClose, ExecutionSimQuotePathScenario.NormalLiquid),
            Simulate(ExecutionSimPolicy.CloseSeeking15mAdaptive, ExecutionSimQuotePathScenario.NormalLiquid),
            Simulate(ExecutionSimPolicy.ControlledResidualCross, ExecutionSimQuotePathScenario.ResidualHighNearClose),
            Simulate(ExecutionSimPolicy.TWAPBenchmarkOnly, ExecutionSimQuotePathScenario.NormalLiquid),
            Simulate(ExecutionSimPolicy.VWAPBenchmarkOnly, ExecutionSimQuotePathScenario.NormalLiquid),
            Simulate(ExecutionSimPolicy.CloseSeeking15mAdaptive, ExecutionSimQuotePathScenario.WideSpreadNearClose),
            Simulate(ExecutionSimPolicy.CloseSeeking15mAdaptive, ExecutionSimQuotePathScenario.QuoteGapNearClose),
            Simulate(ExecutionSimPolicy.CloseSeeking15mAdaptive, ExecutionSimQuotePathScenario.StaleQuoteNearClose)
        };

        return new ExecutionSimTcaReport(
            "exec-sim-r001-tca-report",
            lines,
            IncludesSlippageVsClose: true,
            IncludesSpreadPaid: true,
            IncludesResidualAtClose: true,
            IncludesFillRatio: true,
            FixtureOnly: true,
            PaperOnly: true,
            NoOrdersCreated: true,
            NoRealFillsCreated: true,
            NoExecutionReportsCreated: true,
            NoRoutesCreated: true,
            NoSubmissionsCreated: true);
    }

    public static ExecutionSimCostBucketCalibration CreateCostBucketCalibration()
        => new(
            BestCaseMajorTargetUsdPerMillion: 5m,
            BaseCaseMajorTargetUsdPerMillionLow: 10m,
            BaseCaseMajorTargetUsdPerMillionHigh: 15m,
            StressMajorTargetUsdPerMillionLow: 30m,
            StressMajorTargetUsdPerMillionHigh: 50m,
            FiveUsdPerMillionIsBestCaseOnly: true,
            FiveUsdPerMillionUniversalized: false,
            CostBucketStatus.MajorUsdPairCostBucket,
            CostBucketStatus.RequiresLiquidityCalibration);

    private static IReadOnlyList<ExecutionSimQuote> CreateQuotes(
        ExecutionSimQuotePathScenario scenario,
        decimal baseMid,
        decimal spreadBps,
        bool includeCloseQuote,
        int finalQuoteAgeSeconds = 1,
        FeedReadinessStatus finalFeedStatus = FeedReadinessStatus.ReadyForCloseBenchmark,
        decimal driftBps = 0m)
    {
        var timestamps = includeCloseQuote
            ? new[]
            {
                KnownAt,
                KnownAt.AddMinutes(4),
                KnownAt.AddMinutes(8),
                Close.AddMinutes(-1),
                Close.AddSeconds(-finalQuoteAgeSeconds)
            }
            : new[]
            {
                KnownAt,
                KnownAt.AddMinutes(4),
                KnownAt.AddMinutes(8),
                Close.AddSeconds(-finalQuoteAgeSeconds)
            };

        return timestamps.Select((timestamp, index) =>
        {
            var mid = baseMid + (baseMid * driftBps / 10000m * index / Math.Max(1, timestamps.Length - 1));
            var halfSpread = mid * spreadBps / 20000m;
            var age = index == timestamps.Length - 1 ? TimeSpan.FromSeconds(finalQuoteAgeSeconds) : TimeSpan.FromSeconds(1);
            var status = index == timestamps.Length - 1 ? finalFeedStatus : FeedReadinessStatus.ReadyForCloseBenchmark;

            if (!includeCloseQuote && index == timestamps.Length - 1)
            {
                age = TimeSpan.FromSeconds(finalQuoteAgeSeconds);
                status = FeedReadinessStatus.NoQuoteNearClose;
            }

            return new ExecutionSimQuote(
                "EURUSD",
                "EURUSD",
                timestamp,
                mid - halfSpread,
                mid + halfSpread,
                mid,
                spreadBps,
                age,
                status,
                BarStart,
                Close,
                Close,
                KnownAt);
        }).ToArray();
    }
}

public enum ExecutionSimInstrumentLiquidityBucket
{
    MajorUsdPair,
    NonMajorUsdPair,
    EmCnhHighCalibration,
    MissingConvention,
    DirectCrossSignalOnly
}

public enum ExecutionSimSpreadRegime
{
    TightSpread,
    NormalSpread,
    WideSpread,
    ExtremeSpread
}

public enum ExecutionSimResidualSize
{
    SmallResidual,
    MediumResidual,
    LargeResidual,
    HighResidualNearClose
}

public enum ExecutionSimDriftRegime
{
    Flat,
    FavorableDrift,
    AdverseDrift,
    FastAdverseDrift
}

public enum ExecutionSimFeedQualityRegime
{
    GoodFeed,
    MinorGap,
    MajorGap,
    NoQuoteNearClose,
    StaleQuoteNearClose
}

public enum ExecutionSimTimeToClosePhase
{
    TMinus13ToTMinus5,
    TMinus5ToTMinus1,
    TMinus1ToClose
}

public sealed record ExecutionSimScenarioMatrixLine(
    string ScenarioId,
    string InstrumentId,
    string PortfolioCurrency,
    string PortfolioNormalizedSymbol,
    string? ExecutionTradableSymbol,
    bool RequiresInversion,
    FxExecutionSide PortfolioSide,
    FxExecutionSide ExecutionSide,
    ExecutionSimInstrumentLiquidityBucket LiquidityBucket,
    bool RawDirectCrossSignalOnly,
    bool DirectCrossExecutionAllowed,
    ExecutionSimSpreadRegime SpreadRegime,
    ExecutionSimResidualSize ResidualSize,
    ExecutionSimDriftRegime DriftRegime,
    ExecutionSimFeedQualityRegime FeedQualityRegime,
    ExecutionSimTimeToClosePhase TimeToClosePhase,
    ExecutionSimPolicy Policy,
    decimal FillRatio,
    decimal PassiveFillRatio,
    decimal AggressiveFillRatio,
    decimal ResidualAtClose,
    decimal SlippageVsCloseBps,
    decimal SlippageVsCloseUsdPerMillion,
    decimal SpreadPaidBps,
    decimal SpreadPaidUsdPerMillion,
    decimal EstimatedSpreadCost,
    decimal EstimatedOpportunityCost,
    decimal EstimatedNonFillCost,
    decimal EstimatedResidualCost,
    decimal ImplementationShortfallVsDecisionBps,
    FeedGapCategory QuoteGapStatus,
    SafeExecutionAlgoReasonCategory? StalenessStatus,
    FeedReadinessStatus FeedReadinessStatus,
    CloseBenchmarkAvailabilityStatus BenchmarkAvailabilityStatus,
    SimulationOutcomeStatus SimulationOutcomeStatus,
    AlgoPolicyReasonCategory? BlockReason,
    CostBucketStatus CostBucketStatus,
    bool FixtureOnly,
    bool PaperOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoRealFill,
    bool NoExecutionReport);

public sealed record ExecutionSimPolicyRanking(
    ExecutionSimPolicy Policy,
    int Rank,
    decimal Value,
    string Metric);

public sealed record ExecutionSimWorstCaseScenario(
    ExecutionSimPolicy Policy,
    string ScenarioId,
    decimal SlippageVsCloseBps,
    decimal ResidualAtClose,
    AlgoPolicyReasonCategory? BlockReason);

public sealed record ExecutionSimScenarioMatrixReport(
    string ReportId,
    IReadOnlyList<ExecutionSimScenarioMatrixLine> Lines,
    IReadOnlyList<ExecutionSimPolicyRanking> MedianSlippageRanking,
    IReadOnlyList<ExecutionSimPolicyRanking> P95SlippageRanking,
    IReadOnlyList<ExecutionSimPolicyRanking> FillRatioRanking,
    IReadOnlyList<ExecutionSimPolicyRanking> ResidualRanking,
    IReadOnlyList<ExecutionSimPolicyRanking> SpreadPaidRanking,
    IReadOnlyList<ExecutionSimWorstCaseScenario> WorstCases,
    bool ScenarioMatrixCreated,
    bool UsesUsdPairNormalization,
    bool DirectCrossSignalsNotExecuted,
    bool FiveUsdPerMillionBestCaseOnly,
    bool FiveUsdPerMillionUniversalized,
    bool NonMajorCalibrationRequired,
    bool EmCnhCalibrationRequired,
    bool FixtureOnly,
    bool PaperOnly,
    bool NoOrdersCreated,
    bool NoRealFillsCreated,
    bool NoExecutionReportsCreated,
    bool NoRoutesCreated,
    bool NoSubmissionsCreated);

public static class ExecutionSimR002PolicyScenarioMatrix
{
    public static ExecutionSimScenarioMatrixReport CreateReport()
    {
        var lines = CreateScenarioMatrix();

        return new ExecutionSimScenarioMatrixReport(
            "exec-sim-r002-policy-scenario-matrix-report",
            lines,
            Rank(lines, x => x.SlippageVsCloseBps, "MedianSlippageVsCloseBps", lowerIsBetter: true),
            Rank(lines, x => x.SlippageVsCloseBps * 1.35m, "P95SlippageVsCloseBps", lowerIsBetter: true),
            Rank(lines, x => x.FillRatio, "FillRatio", lowerIsBetter: false),
            Rank(lines, x => x.ResidualAtClose, "ResidualAtClose", lowerIsBetter: true),
            Rank(lines, x => x.SpreadPaidBps, "SpreadPaidBps", lowerIsBetter: true),
            CreateWorstCases(lines),
            ScenarioMatrixCreated: true,
            UsesUsdPairNormalization: true,
            DirectCrossSignalsNotExecuted: true,
            FiveUsdPerMillionBestCaseOnly: true,
            FiveUsdPerMillionUniversalized: false,
            NonMajorCalibrationRequired: true,
            EmCnhCalibrationRequired: true,
            FixtureOnly: true,
            PaperOnly: true,
            NoOrdersCreated: true,
            NoRealFillsCreated: true,
            NoExecutionReportsCreated: true,
            NoRoutesCreated: true,
            NoSubmissionsCreated: true);
    }

    public static IReadOnlyList<ExecutionSimScenarioMatrixLine> CreateScenarioMatrix()
    {
        var lines = new List<ExecutionSimScenarioMatrixLine>
        {
            Line("aud-tight-small-good-passive", "AUD", 90000m, ExecutionSimInstrumentLiquidityBucket.MajorUsdPair, ExecutionSimSpreadRegime.TightSpread, ExecutionSimResidualSize.SmallResidual, ExecutionSimDriftRegime.Flat, ExecutionSimFeedQualityRegime.GoodFeed, ExecutionSimTimeToClosePhase.TMinus13ToTMinus5, ExecutionSimPolicy.PassiveUntilUrgency, 0.86m, 0.78m, 0.08m, 0.14m, 1.1m, 0.45m, 0.45m, 0.25m, 0.15m, 0.12m, FeedReadinessStatus.ReadyForCloseBenchmark, CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote, SimulationOutcomeStatus.CompletedFixtureOnly, null),
            Line("eur-normal-medium-adaptive", "EUR", 125000m, ExecutionSimInstrumentLiquidityBucket.MajorUsdPair, ExecutionSimSpreadRegime.NormalSpread, ExecutionSimResidualSize.MediumResidual, ExecutionSimDriftRegime.Flat, ExecutionSimFeedQualityRegime.GoodFeed, ExecutionSimTimeToClosePhase.TMinus5ToTMinus1, ExecutionSimPolicy.CloseSeeking15mAdaptive, 0.83m, 0.70m, 0.13m, 0.17m, 1.5m, 0.7m, 0.70m, 0.35m, 0.20m, 0.20m, FeedReadinessStatus.ReadyForCloseBenchmark, CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote, SimulationOutcomeStatus.CompletedFixtureOnly, null),
            Line("gbp-favorable-close-seeking", "GBP", -180000m, ExecutionSimInstrumentLiquidityBucket.MajorUsdPair, ExecutionSimSpreadRegime.NormalSpread, ExecutionSimResidualSize.MediumResidual, ExecutionSimDriftRegime.FavorableDrift, ExecutionSimFeedQualityRegime.GoodFeed, ExecutionSimTimeToClosePhase.TMinus5ToTMinus1, ExecutionSimPolicy.CloseSeeking15m, 0.80m, 0.68m, 0.12m, 0.20m, 1.4m, 0.65m, 0.65m, 0.30m, 0.22m, 0.20m, FeedReadinessStatus.ReadyForCloseBenchmark, CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote, SimulationOutcomeStatus.CompletedFixtureOnly, null),
            Line("nzd-low-residual-passive", "NZD", 70000m, ExecutionSimInstrumentLiquidityBucket.MajorUsdPair, ExecutionSimSpreadRegime.TightSpread, ExecutionSimResidualSize.SmallResidual, ExecutionSimDriftRegime.Flat, ExecutionSimFeedQualityRegime.MinorGap, ExecutionSimTimeToClosePhase.TMinus13ToTMinus5, ExecutionSimPolicy.PassiveUntilUrgency, 0.78m, 0.74m, 0.04m, 0.22m, 1.2m, 0.50m, 0.50m, 0.28m, 0.18m, 0.18m, FeedReadinessStatus.ReadyForCloseBenchmark, CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote, SimulationOutcomeStatus.CompletedFixtureOnly, null),
            Line("jpy-inverted-adaptive", "JPY", 140000m, ExecutionSimInstrumentLiquidityBucket.MajorUsdPair, ExecutionSimSpreadRegime.NormalSpread, ExecutionSimResidualSize.MediumResidual, ExecutionSimDriftRegime.AdverseDrift, ExecutionSimFeedQualityRegime.GoodFeed, ExecutionSimTimeToClosePhase.TMinus5ToTMinus1, ExecutionSimPolicy.CloseSeeking15mAdaptive, 0.80m, 0.62m, 0.18m, 0.20m, 1.9m, 0.85m, 0.85m, 0.60m, 0.30m, 0.24m, FeedReadinessStatus.ReadyForCloseBenchmark, CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote, SimulationOutcomeStatus.CompletedFixtureOnly, null),
            Line("cad-inverted-controlled", "CAD", -160000m, ExecutionSimInstrumentLiquidityBucket.MajorUsdPair, ExecutionSimSpreadRegime.NormalSpread, ExecutionSimResidualSize.HighResidualNearClose, ExecutionSimDriftRegime.AdverseDrift, ExecutionSimFeedQualityRegime.GoodFeed, ExecutionSimTimeToClosePhase.TMinus1ToClose, ExecutionSimPolicy.ControlledResidualCross, 0.94m, 0.42m, 0.52m, 0.06m, 3.8m, 1.2m, 1.2m, 3.1m, 0.08m, 0.07m, FeedReadinessStatus.ReadyForCloseBenchmark, CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote, SimulationOutcomeStatus.CompletedFixtureOnly, AlgoPolicyReasonCategory.ReadyForControlledResidualCross),
            Line("chf-inverted-fast-adverse", "CHF", 150000m, ExecutionSimInstrumentLiquidityBucket.MajorUsdPair, ExecutionSimSpreadRegime.WideSpread, ExecutionSimResidualSize.LargeResidual, ExecutionSimDriftRegime.FastAdverseDrift, ExecutionSimFeedQualityRegime.GoodFeed, ExecutionSimTimeToClosePhase.TMinus1ToClose, ExecutionSimPolicy.ManualReview, 0.0m, 0.0m, 0.0m, 1.0m, 6.5m, 4.8m, 4.8m, 3.5m, 2.0m, 1.5m, FeedReadinessStatus.SpreadTooWide, CloseBenchmarkAvailabilityStatus.InconclusiveSafe, SimulationOutcomeStatus.ManualReviewSafe, AlgoPolicyReasonCategory.SpreadTooWide),
            Line("nok-nonmajor-calibration", "NOK", 110000m, ExecutionSimInstrumentLiquidityBucket.NonMajorUsdPair, ExecutionSimSpreadRegime.WideSpread, ExecutionSimResidualSize.MediumResidual, ExecutionSimDriftRegime.Flat, ExecutionSimFeedQualityRegime.GoodFeed, ExecutionSimTimeToClosePhase.TMinus5ToTMinus1, ExecutionSimPolicy.ManualReview, 0.0m, 0.0m, 0.0m, 1.0m, 5.5m, 3.5m, 3.5m, 1.2m, 2.0m, 1.5m, FeedReadinessStatus.ReadyForCloseBenchmark, CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote, SimulationOutcomeStatus.ManualReviewSafe, AlgoPolicyReasonCategory.RequiresManualReview),
            Line("sek-nonmajor-minor-gap", "SEK", -95000m, ExecutionSimInstrumentLiquidityBucket.NonMajorUsdPair, ExecutionSimSpreadRegime.NormalSpread, ExecutionSimResidualSize.MediumResidual, ExecutionSimDriftRegime.Flat, ExecutionSimFeedQualityRegime.MinorGap, ExecutionSimTimeToClosePhase.TMinus5ToTMinus1, ExecutionSimPolicy.CloseSeeking15mAdaptive, 0.68m, 0.55m, 0.13m, 0.32m, 2.8m, 1.8m, 1.8m, 0.9m, 0.55m, 0.50m, FeedReadinessStatus.ReadyForCloseBenchmark, CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote, SimulationOutcomeStatus.CompletedFixtureOnly, null),
            Line("mxn-em-calibration", "MXN", 210000m, ExecutionSimInstrumentLiquidityBucket.EmCnhHighCalibration, ExecutionSimSpreadRegime.ExtremeSpread, ExecutionSimResidualSize.LargeResidual, ExecutionSimDriftRegime.FastAdverseDrift, ExecutionSimFeedQualityRegime.GoodFeed, ExecutionSimTimeToClosePhase.TMinus1ToClose, ExecutionSimPolicy.ManualReview, 0.0m, 0.0m, 0.0m, 1.0m, 9.0m, 6.5m, 6.5m, 3.5m, 3.0m, 2.2m, FeedReadinessStatus.SpreadTooWide, CloseBenchmarkAvailabilityStatus.InconclusiveSafe, SimulationOutcomeStatus.ManualReviewSafe, AlgoPolicyReasonCategory.SpreadTooWide),
            Line("cnh-em-major-gap", "CNH", 175000m, ExecutionSimInstrumentLiquidityBucket.EmCnhHighCalibration, ExecutionSimSpreadRegime.WideSpread, ExecutionSimResidualSize.LargeResidual, ExecutionSimDriftRegime.AdverseDrift, ExecutionSimFeedQualityRegime.MajorGap, ExecutionSimTimeToClosePhase.TMinus5ToTMinus1, ExecutionSimPolicy.ManualReview, 0.0m, 0.0m, 0.0m, 1.0m, 8.0m, 4.5m, 4.5m, 2.5m, 2.8m, 2.0m, FeedReadinessStatus.InconclusiveSafe, CloseBenchmarkAvailabilityStatus.InconclusiveSafe, SimulationOutcomeStatus.ManualReviewSafe, AlgoPolicyReasonCategory.MissingFeedContinuity),
            Line("zar-em-noquote", "ZAR", -220000m, ExecutionSimInstrumentLiquidityBucket.EmCnhHighCalibration, ExecutionSimSpreadRegime.ExtremeSpread, ExecutionSimResidualSize.HighResidualNearClose, ExecutionSimDriftRegime.FastAdverseDrift, ExecutionSimFeedQualityRegime.NoQuoteNearClose, ExecutionSimTimeToClosePhase.TMinus1ToClose, ExecutionSimPolicy.DoNotTrade, 0.0m, 0.0m, 0.0m, 1.0m, 10.0m, 0.0m, 0.0m, 4.0m, 4.0m, 3.0m, FeedReadinessStatus.NoQuoteNearClose, CloseBenchmarkAvailabilityStatus.CloseUnavailable, SimulationOutcomeStatus.ManualReviewSafe, AlgoPolicyReasonCategory.NoQuoteNearClose),
            Line("sgd-missing-convention", "SGD", 85000m, ExecutionSimInstrumentLiquidityBucket.MissingConvention, ExecutionSimSpreadRegime.NormalSpread, ExecutionSimResidualSize.MediumResidual, ExecutionSimDriftRegime.Flat, ExecutionSimFeedQualityRegime.GoodFeed, ExecutionSimTimeToClosePhase.TMinus13ToTMinus5, ExecutionSimPolicy.ManualReview, 0.0m, 0.0m, 0.0m, 1.0m, 0.0m, 0.0m, 0.0m, 0.0m, 0.0m, 0.0m, FeedReadinessStatus.InconclusiveSafe, CloseBenchmarkAvailabilityStatus.InconclusiveSafe, SimulationOutcomeStatus.ManualReviewSafe, AlgoPolicyReasonCategory.MissingInstrumentConvention),
            DirectCross("eurgbp-signal-only", "EURGBP"),
            DirectCross("cadjpy-signal-only", "CADJPY"),
            DirectCross("audcnh-signal-only", "AUDCNH"),
            Line("wakett-limit-low-fill", "EUR", 125000m, ExecutionSimInstrumentLiquidityBucket.MajorUsdPair, ExecutionSimSpreadRegime.NormalSpread, ExecutionSimResidualSize.HighResidualNearClose, ExecutionSimDriftRegime.Flat, ExecutionSimFeedQualityRegime.GoodFeed, ExecutionSimTimeToClosePhase.TMinus1ToClose, ExecutionSimPolicy.WakettPureLimitUntilClose, 0.20m, 0.20m, 0.0m, 0.80m, 2.5m, 0.1m, 0.1m, 2.4m, 3.2m, 2.8m, FeedReadinessStatus.ReadyForCloseBenchmark, CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote, SimulationOutcomeStatus.BlockedUnsafePattern, AlgoPolicyReasonCategory.WakettPatternBlocked),
            Line("wakett-five-slices-normal", "EUR", 125000m, ExecutionSimInstrumentLiquidityBucket.MajorUsdPair, ExecutionSimSpreadRegime.NormalSpread, ExecutionSimResidualSize.SmallResidual, ExecutionSimDriftRegime.Flat, ExecutionSimFeedQualityRegime.GoodFeed, ExecutionSimTimeToClosePhase.TMinus1ToClose, ExecutionSimPolicy.WakettFiveMarketSlicesAroundClose, 1.0m, 0.0m, 1.0m, 0.0m, 5.2m, 5.0m, 5.0m, 0.2m, 0.0m, 0.0m, FeedReadinessStatus.ReadyForCloseBenchmark, CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote, SimulationOutcomeStatus.BlockedUnsafePattern, AlgoPolicyReasonCategory.WakettPatternBlocked),
            Line("twap-benchmark-only", "EUR", 125000m, ExecutionSimInstrumentLiquidityBucket.MajorUsdPair, ExecutionSimSpreadRegime.NormalSpread, ExecutionSimResidualSize.MediumResidual, ExecutionSimDriftRegime.Flat, ExecutionSimFeedQualityRegime.GoodFeed, ExecutionSimTimeToClosePhase.TMinus5ToTMinus1, ExecutionSimPolicy.TWAPBenchmarkOnly, 0.0m, 0.0m, 0.0m, 1.0m, 0.0m, 0.0m, 0.0m, 0.0m, 0.0m, 0.0m, FeedReadinessStatus.ReadyForCloseBenchmark, CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote, SimulationOutcomeStatus.BenchmarkOnly, null),
            Line("vwap-benchmark-only", "EUR", 125000m, ExecutionSimInstrumentLiquidityBucket.MajorUsdPair, ExecutionSimSpreadRegime.NormalSpread, ExecutionSimResidualSize.MediumResidual, ExecutionSimDriftRegime.Flat, ExecutionSimFeedQualityRegime.GoodFeed, ExecutionSimTimeToClosePhase.TMinus5ToTMinus1, ExecutionSimPolicy.VWAPBenchmarkOnly, 0.0m, 0.0m, 0.0m, 1.0m, 0.0m, 0.0m, 0.0m, 0.0m, 0.0m, 0.0m, FeedReadinessStatus.ReadyForCloseBenchmark, CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote, SimulationOutcomeStatus.BenchmarkOnly, null),
            Line("wide-spread-block", "AUD", 90000m, ExecutionSimInstrumentLiquidityBucket.MajorUsdPair, ExecutionSimSpreadRegime.WideSpread, ExecutionSimResidualSize.MediumResidual, ExecutionSimDriftRegime.Flat, ExecutionSimFeedQualityRegime.GoodFeed, ExecutionSimTimeToClosePhase.TMinus5ToTMinus1, ExecutionSimPolicy.ManualReview, 0.0m, 0.0m, 0.0m, 1.0m, 6.0m, 4.8m, 4.8m, 0.8m, 0.5m, 0.4m, FeedReadinessStatus.SpreadTooWide, CloseBenchmarkAvailabilityStatus.InconclusiveSafe, SimulationOutcomeStatus.ManualReviewSafe, AlgoPolicyReasonCategory.SpreadTooWide),
            Line("stale-quote-block", "GBP", -180000m, ExecutionSimInstrumentLiquidityBucket.MajorUsdPair, ExecutionSimSpreadRegime.NormalSpread, ExecutionSimResidualSize.MediumResidual, ExecutionSimDriftRegime.Flat, ExecutionSimFeedQualityRegime.StaleQuoteNearClose, ExecutionSimTimeToClosePhase.TMinus1ToClose, ExecutionSimPolicy.ManualReview, 0.0m, 0.0m, 0.0m, 1.0m, 0.0m, 0.0m, 0.0m, 1.0m, 1.0m, 0.8m, FeedReadinessStatus.StaleQuotes, CloseBenchmarkAvailabilityStatus.StaleCloseBenchmark, SimulationOutcomeStatus.ManualReviewSafe, AlgoPolicyReasonCategory.StaleQuoteNearClose)
        };

        return lines;
    }

    private static ExecutionSimScenarioMatrixLine Line(
        string scenarioId,
        string portfolioCurrency,
        decimal quantity,
        ExecutionSimInstrumentLiquidityBucket bucket,
        ExecutionSimSpreadRegime spreadRegime,
        ExecutionSimResidualSize residualSize,
        ExecutionSimDriftRegime driftRegime,
        ExecutionSimFeedQualityRegime feedQualityRegime,
        ExecutionSimTimeToClosePhase phase,
        ExecutionSimPolicy policy,
        decimal fillRatio,
        decimal passiveFillRatio,
        decimal aggressiveFillRatio,
        decimal residualAtClose,
        decimal slippageVsCloseBps,
        decimal spreadPaidBps,
        decimal estimatedSpreadCost,
        decimal estimatedOpportunityCost,
        decimal estimatedNonFillCost,
        decimal estimatedResidualCost,
        FeedReadinessStatus feedReadinessStatus,
        CloseBenchmarkAvailabilityStatus benchmarkAvailabilityStatus,
        SimulationOutcomeStatus outcomeStatus,
        AlgoPolicyReasonCategory? blockReason)
    {
        var normalized = ExecutionAlgoR002UsdPairSelectionPolicy.NormalizeExposure(portfolioCurrency, quantity);
        var costBucketStatus = bucket is ExecutionSimInstrumentLiquidityBucket.NonMajorUsdPair or ExecutionSimInstrumentLiquidityBucket.EmCnhHighCalibration or ExecutionSimInstrumentLiquidityBucket.MissingConvention
            ? CostBucketStatus.RequiresLiquidityCalibration
            : CostBucketStatus.MajorUsdPairCostBucket;

        return new ExecutionSimScenarioMatrixLine(
            scenarioId,
            normalized.ExecutionTradableSymbol ?? normalized.PortfolioNormalizedSymbol,
            portfolioCurrency,
            normalized.PortfolioNormalizedSymbol,
            normalized.ExecutionTradableSymbol,
            normalized.RequiresInversion,
            normalized.PortfolioSide,
            normalized.ExecutionSide,
            bucket,
            RawDirectCrossSignalOnly: false,
            DirectCrossExecutionAllowed: false,
            spreadRegime,
            residualSize,
            driftRegime,
            feedQualityRegime,
            phase,
            policy,
            fillRatio,
            passiveFillRatio,
            aggressiveFillRatio,
            residualAtClose,
            slippageVsCloseBps,
            SlippageVsCloseUsdPerMillion: slippageVsCloseBps * 100m,
            spreadPaidBps,
            SpreadPaidUsdPerMillion: spreadPaidBps * 100m,
            estimatedSpreadCost,
            estimatedOpportunityCost,
            estimatedNonFillCost,
            estimatedResidualCost,
            ImplementationShortfallVsDecisionBps: slippageVsCloseBps + estimatedNonFillCost + estimatedResidualCost,
            feedReadinessStatus == FeedReadinessStatus.NoQuoteNearClose ? FeedGapCategory.NoQuoteNearClose : FeedGapCategory.NoGap,
            feedReadinessStatus == FeedReadinessStatus.StaleQuotes ? SafeExecutionAlgoReasonCategory.StaleQuoteNearClose : null,
            feedReadinessStatus,
            benchmarkAvailabilityStatus,
            outcomeStatus,
            blockReason,
            costBucketStatus,
            FixtureOnly: true,
            PaperOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoRealFill: true,
            NoExecutionReport: true);
    }

    private static ExecutionSimScenarioMatrixLine DirectCross(string scenarioId, string rawCross)
    {
        var blocked = ExecutionAlgoR002UsdPairSelectionPolicy.BlockRawDirectCross(rawCross);
        return new ExecutionSimScenarioMatrixLine(
            scenarioId,
            rawCross,
            rawCross[..3],
            blocked.PortfolioNormalizedSymbol,
            blocked.ExecutionTradableSymbol,
            RequiresInversion: false,
            FxExecutionSide.None,
            FxExecutionSide.None,
            ExecutionSimInstrumentLiquidityBucket.DirectCrossSignalOnly,
            RawDirectCrossSignalOnly: true,
            DirectCrossExecutionAllowed: false,
            ExecutionSimSpreadRegime.NormalSpread,
            ExecutionSimResidualSize.MediumResidual,
            ExecutionSimDriftRegime.Flat,
            ExecutionSimFeedQualityRegime.GoodFeed,
            ExecutionSimTimeToClosePhase.TMinus13ToTMinus5,
            ExecutionSimPolicy.ManualReview,
            FillRatio: 0m,
            PassiveFillRatio: 0m,
            AggressiveFillRatio: 0m,
            ResidualAtClose: 1m,
            SlippageVsCloseBps: 0m,
            SlippageVsCloseUsdPerMillion: 0m,
            SpreadPaidBps: 0m,
            SpreadPaidUsdPerMillion: 0m,
            EstimatedSpreadCost: 0m,
            EstimatedOpportunityCost: 0m,
            EstimatedNonFillCost: 0m,
            EstimatedResidualCost: 0m,
            ImplementationShortfallVsDecisionBps: 0m,
            FeedGapCategory.NoGap,
            StalenessStatus: null,
            FeedReadinessStatus.InconclusiveSafe,
            CloseBenchmarkAvailabilityStatus.InconclusiveSafe,
            SimulationOutcomeStatus.ManualReviewSafe,
            AlgoPolicyReasonCategory.DirectCrossExecutionDisabled,
            CostBucketStatus.InconclusiveSafe,
            FixtureOnly: true,
            PaperOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoRealFill: true,
            NoExecutionReport: true);
    }

    private static IReadOnlyList<ExecutionSimPolicyRanking> Rank(
        IReadOnlyList<ExecutionSimScenarioMatrixLine> lines,
        Func<ExecutionSimScenarioMatrixLine, decimal> selector,
        string metric,
        bool lowerIsBetter)
    {
        var ranked = lines
            .Where(x => !x.RawDirectCrossSignalOnly && x.Policy is not ExecutionSimPolicy.ManualReview and not ExecutionSimPolicy.DoNotTrade)
            .GroupBy(x => x.Policy)
            .Select(x => new { Policy = x.Key, Value = x.Average(selector) })
            .OrderBy(x => lowerIsBetter ? x.Value : -x.Value)
            .Select((x, index) => new ExecutionSimPolicyRanking(x.Policy, index + 1, x.Value, metric))
            .ToArray();

        return ranked;
    }

    private static IReadOnlyList<ExecutionSimWorstCaseScenario> CreateWorstCases(IReadOnlyList<ExecutionSimScenarioMatrixLine> lines)
        => lines
            .Where(x => !x.RawDirectCrossSignalOnly)
            .GroupBy(x => x.Policy)
            .Select(x => x.OrderByDescending(y => y.SlippageVsCloseBps + y.ResidualAtClose).First())
            .Select(x => new ExecutionSimWorstCaseScenario(x.Policy, x.ScenarioId, x.SlippageVsCloseBps, x.ResidualAtClose, x.BlockReason))
            .ToArray();
}

public enum HistoricalQuoteProviderName
{
    Polygon,
    LMAXArchive,
    FixtureOnly
}

public enum HistoricalQuoteReadinessStatus
{
    ReadyForHistoricalQuoteImportDesign,
    ReadyForFixtureImportOnly,
    RequiresProviderApiKeyDesign,
    RequiresSymbolMapping,
    RequiresCoverageValidation,
    RequiresLiquidityCalibration,
    NotReadyUntilArchiveExists,
    BlockedMissingBidAsk,
    BlockedMissingTimestamp,
    BlockedMissingCloseBenchmark,
    InconclusiveSafe
}

public enum HistoricalQuoteFailureCategory
{
    MissingProvider,
    MissingBidAsk,
    MissingTimestamp,
    MissingSymbolMapping,
    MissingWindowCoverage,
    InsufficientQuoteCount,
    QuoteGapNearClose,
    StaleQuoteNearClose,
    SpreadTooWide,
    MissingCloseBenchmark,
    UnsupportedInstrument,
    RequiresLiquidityCalibration,
    DirectCrossExecutionDisabled,
    RawPayloadLeakRisk,
    SecretLeakRisk,
    InconclusiveSafe
}

public enum HistoricalFeedQualityBucket
{
    Excellent,
    Good,
    Usable,
    Marginal,
    Unusable,
    InconclusiveSafe
}

public enum HistoricalCloseBenchmarkStatus
{
    Available,
    MissingBidAsk,
    StaleAtClose,
    NoQuoteNearClose,
    SpreadTooWide,
    InconclusiveSafe
}

public enum HistoricalQuoteQualityStatus
{
    ValidBidAskTimestamp,
    MissingBidAsk,
    MissingTimestamp,
    InconclusiveSafe
}

public sealed record HistoricalQuoteSchemaContract(
    IReadOnlyList<string> RequiredFields,
    bool SupportsOptionalBidAskSize,
    bool SupportsOptionalVenueOrExchangeId,
    bool SupportsOptionalSequenceId,
    bool SupportsOptionalSourceLatencyCategory,
    bool NoRawPayloadSerialization);

public sealed record HistoricalQuoteWindowExtractionContract(
    string InstrumentId,
    string ExecutionTradableSymbol,
    DateTimeOffset TargetCloseTimestampUtc,
    DateTimeOffset KnownAtTimestampUtc,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    TimeSpan RequiredCadenceWindow,
    int QuoteCount,
    int QuoteCountLastMinute,
    TimeSpan MaxQuoteGap,
    TimeSpan LastQuoteAgeAtClose,
    bool HasBidAsk,
    bool HasMid,
    bool HasCloseBenchmark,
    HistoricalQuoteReadinessStatus FeedWindowStatus);

public sealed record HistoricalCloseBenchmarkConstructionContract(
    string BenchmarkName,
    bool RequiresLastValidBidBeforeClose,
    bool RequiresLastValidAskBeforeClose,
    bool RequiresLastValidMidBeforeClose,
    bool RequiresLastValidQuoteTimestampUtc,
    bool IncludesCloseQuoteAge,
    bool IncludesCloseSpreadBps,
    IReadOnlyList<CloseConstructionMethod> ConstructionMethods,
    IReadOnlyList<HistoricalCloseBenchmarkStatus> BenchmarkStatuses);

public sealed record HistoricalFeedQualityScoringContract(
    IReadOnlyList<string> Metrics,
    IReadOnlyList<HistoricalFeedQualityBucket> Buckets,
    bool BlocksGapNearClose,
    bool BlocksStaleNearClose,
    bool BlocksSpreadWideNearClose);

public sealed record HistoricalProviderCapabilityRecord(
    HistoricalQuoteProviderName ProviderName,
    bool SupportsHistoricalBidAsk,
    bool SupportsTimestamps,
    bool SupportsBidAskSize,
    bool SupportsVenueOrExchangeId,
    bool SupportsPagination,
    bool SupportsFullTMinus13Window,
    bool SupportsMajorUsdPairs,
    bool SupportsInvertedUsdPairs,
    bool SupportsNonMajorPairs,
    bool SupportsCNH,
    bool SupportsScandi,
    bool SupportsEM,
    string KnownLimitations,
    HistoricalQuoteReadinessStatus ReadinessStatus,
    bool DocsBackedCandidateOnly,
    bool ApiCalled,
    bool BrokerCalled);

public sealed record HistoricalQuoteReadinessPackage(
    HistoricalQuoteSchemaContract SchemaContract,
    HistoricalQuoteWindowExtractionContract WindowExtractionContract,
    HistoricalCloseBenchmarkConstructionContract CloseBenchmarkContract,
    HistoricalFeedQualityScoringContract FeedQualityContract,
    IReadOnlyList<HistoricalProviderCapabilityRecord> ProviderCapabilities,
    IReadOnlyList<string> UsdPairCoverageRequirements,
    IReadOnlyList<string> DirectCrossSignalOnlySymbols,
    IReadOnlyList<HistoricalQuoteReadinessStatus> ReadinessStatuses,
    IReadOnlyList<HistoricalQuoteFailureCategory> SafeFailureCategories,
    bool PolygonApiCalled,
    bool LmaxCalled,
    bool ExternalApiCalled,
    bool OrdersCreated,
    bool FillsCreated,
    bool ExecutionReportsCreated,
    bool RoutesCreated,
    bool SubmissionsCreated,
    bool RawPayloadSerialized,
    bool SecretsSerialized);

public static class ExecutionSimR003HistoricalQuoteReadiness
{
    private static readonly DateTimeOffset TargetClose = new(2026, 05, 20, 15, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset KnownAt = TargetClose.AddMinutes(-13);

    public static HistoricalQuoteReadinessPackage CreatePackage()
        => new(
            CreateSchemaContract(),
            CreateWindowExtractionContract(),
            CreateCloseBenchmarkContract(),
            CreateFeedQualityContract(),
            CreateProviderComparison(),
            CreateUsdPairCoverageRequirements(),
            CreateDirectCrossSignalOnlySymbols(),
            Enum.GetValues<HistoricalQuoteReadinessStatus>(),
            Enum.GetValues<HistoricalQuoteFailureCategory>(),
            PolygonApiCalled: false,
            LmaxCalled: false,
            ExternalApiCalled: false,
            OrdersCreated: false,
            FillsCreated: false,
            ExecutionReportsCreated: false,
            RoutesCreated: false,
            SubmissionsCreated: false,
            RawPayloadSerialized: false,
            SecretsSerialized: false);

    public static HistoricalQuoteSchemaContract CreateSchemaContract()
        => new(
            [
                "QuoteProvider",
                "ProviderSymbol",
                "ExecutionTradableSymbol",
                "NormalizedPortfolioSymbol",
                "RequiresInversion",
                "TimestampUtc",
                "Bid",
                "Ask",
                "Mid",
                "Spread",
                "SpreadBps",
                "BidSize",
                "AskSize",
                "VenueOrExchangeId",
                "SequenceId",
                "SourceLatencyCategory",
                "QuoteQualityStatus"
            ],
            SupportsOptionalBidAskSize: true,
            SupportsOptionalVenueOrExchangeId: true,
            SupportsOptionalSequenceId: true,
            SupportsOptionalSourceLatencyCategory: true,
            NoRawPayloadSerialization: true);

    public static HistoricalQuoteWindowExtractionContract CreateWindowExtractionContract()
        => new(
            "EURUSD",
            "EURUSD",
            TargetClose,
            KnownAt,
            TargetClose.AddMinutes(-13),
            TargetClose,
            TimeSpan.FromMinutes(15),
            QuoteCount: 180,
            QuoteCountLastMinute: 20,
            MaxQuoteGap: TimeSpan.FromSeconds(8),
            LastQuoteAgeAtClose: TimeSpan.FromSeconds(1),
            HasBidAsk: true,
            HasMid: true,
            HasCloseBenchmark: true,
            HistoricalQuoteReadinessStatus.ReadyForFixtureImportOnly);

    public static HistoricalCloseBenchmarkConstructionContract CreateCloseBenchmarkContract()
        => new(
            "Close15mBenchmarkFromQuotes",
            RequiresLastValidBidBeforeClose: true,
            RequiresLastValidAskBeforeClose: true,
            RequiresLastValidMidBeforeClose: true,
            RequiresLastValidQuoteTimestampUtc: true,
            IncludesCloseQuoteAge: true,
            IncludesCloseSpreadBps: true,
            [
                CloseConstructionMethod.LastValidQuoteBeforeClose,
                CloseConstructionMethod.LastValidMidBeforeClose,
                CloseConstructionMethod.BidAskClose,
                CloseConstructionMethod.InconclusiveSafe
            ],
            Enum.GetValues<HistoricalCloseBenchmarkStatus>());

    public static HistoricalFeedQualityScoringContract CreateFeedQualityContract()
        => new(
            [
                "QuoteCountTMinus13ToClose",
                "QuoteCountLastMinute",
                "MaxGapSeconds",
                "MedianGapSeconds",
                "P95GapSeconds",
                "LastQuoteAgeAtCloseSeconds",
                "MedianSpreadBps",
                "P95SpreadBps",
                "MaxSpreadBps",
                "BidAskAvailabilityRatio",
                "MidAvailabilityRatio",
                "BenchmarkAvailabilityRatio",
                "GapNearCloseFlag",
                "StaleNearCloseFlag",
                "SpreadWideNearCloseFlag",
                "FeedQualityScore",
                "FeedQualityBucket"
            ],
            Enum.GetValues<HistoricalFeedQualityBucket>(),
            BlocksGapNearClose: true,
            BlocksStaleNearClose: true,
            BlocksSpreadWideNearClose: true);

    public static IReadOnlyList<HistoricalProviderCapabilityRecord> CreateProviderComparison()
        =>
        [
            new(
                HistoricalQuoteProviderName.Polygon,
                SupportsHistoricalBidAsk: true,
                SupportsTimestamps: true,
                SupportsBidAskSize: true,
                SupportsVenueOrExchangeId: true,
                SupportsPagination: true,
                SupportsFullTMinus13Window: true,
                SupportsMajorUsdPairs: true,
                SupportsInvertedUsdPairs: true,
                SupportsNonMajorPairs: true,
                SupportsCNH: true,
                SupportsScandi: true,
                SupportsEM: true,
                "Docs-backed candidate only in R003; later gate must design API key handling, symbol mapping, pagination, rate limits, timestamp normalization, quote validation, close construction, and raw payload sanitization.",
                HistoricalQuoteReadinessStatus.RequiresProviderApiKeyDesign,
                DocsBackedCandidateOnly: true,
                ApiCalled: false,
                BrokerCalled: false),
            new(
                HistoricalQuoteProviderName.LMAXArchive,
                SupportsHistoricalBidAsk: false,
                SupportsTimestamps: false,
                SupportsBidAskSize: false,
                SupportsVenueOrExchangeId: false,
                SupportsPagination: false,
                SupportsFullTMinus13Window: false,
                SupportsMajorUsdPairs: false,
                SupportsInvertedUsdPairs: false,
                SupportsNonMajorPairs: false,
                SupportsCNH: false,
                SupportsScandi: false,
                SupportsEM: false,
                "Future archive not established; requires recorded bid/ask quotes, timestamps, provider symbols, sanitized storage, no raw FIX/credential/session/CompID/endpoint serialization, quote-window extraction, and close benchmark construction.",
                HistoricalQuoteReadinessStatus.NotReadyUntilArchiveExists,
                DocsBackedCandidateOnly: false,
                ApiCalled: false,
                BrokerCalled: false),
            new(
                HistoricalQuoteProviderName.FixtureOnly,
                SupportsHistoricalBidAsk: true,
                SupportsTimestamps: true,
                SupportsBidAskSize: false,
                SupportsVenueOrExchangeId: false,
                SupportsPagination: false,
                SupportsFullTMinus13Window: true,
                SupportsMajorUsdPairs: true,
                SupportsInvertedUsdPairs: true,
                SupportsNonMajorPairs: true,
                SupportsCNH: true,
                SupportsScandi: true,
                SupportsEM: true,
                "Deterministic no-external fixtures remain available for tests and contract validation.",
                HistoricalQuoteReadinessStatus.ReadyForFixtureImportOnly,
                DocsBackedCandidateOnly: false,
                ApiCalled: false,
                BrokerCalled: false)
        ];

    public static IReadOnlyList<string> CreateUsdPairCoverageRequirements()
        =>
        [
            "AUDUSD",
            "EURUSD",
            "GBPUSD",
            "NZDUSD",
            "USDJPY",
            "USDCAD",
            "USDCHF",
            "USDMXN",
            "USDCNH",
            "USDNOK",
            "USDSEK",
            "USDSGD or SGDUSD if convention configured",
            "USDZAR"
        ];

    public static IReadOnlyList<string> CreateDirectCrossSignalOnlySymbols()
        =>
        [
            "EURGBP",
            "CADJPY",
            "AUDCNH",
            "CNHSGD",
            "EURZAR",
            "MXNNOK"
        ];

    public static HistoricalQuoteReadinessStatus EvaluateQuoteReadiness(bool hasBidAsk, bool hasTimestamp)
    {
        if (!hasBidAsk)
        {
            return HistoricalQuoteReadinessStatus.BlockedMissingBidAsk;
        }

        if (!hasTimestamp)
        {
            return HistoricalQuoteReadinessStatus.BlockedMissingTimestamp;
        }

        return HistoricalQuoteReadinessStatus.ReadyForFixtureImportOnly;
    }

    public static HistoricalCloseBenchmarkStatus EvaluateCloseBenchmark(bool hasQuoteNearClose, bool staleNearClose, bool spreadTooWide, bool hasBidAsk)
    {
        if (!hasBidAsk)
        {
            return HistoricalCloseBenchmarkStatus.MissingBidAsk;
        }

        if (!hasQuoteNearClose)
        {
            return HistoricalCloseBenchmarkStatus.NoQuoteNearClose;
        }

        if (staleNearClose)
        {
            return HistoricalCloseBenchmarkStatus.StaleAtClose;
        }

        if (spreadTooWide)
        {
            return HistoricalCloseBenchmarkStatus.SpreadTooWide;
        }

        return HistoricalCloseBenchmarkStatus.Available;
    }
}

public enum PolygonOfflineImportStatus
{
    ImportReady,
    ImportCompletedWithRejectedRows,
    ImportBlockedMissingFile,
    ImportBlockedMalformedRows,
    ImportBlockedUnsupportedSymbol,
    ImportBlockedDirectCrossExecutionDisabled,
    ImportBlockedMissingTimestamp,
    ImportBlockedMissingBidAsk,
    ImportBlockedInvalidBidAsk,
    ImportBlockedRawPayloadLeakRisk,
    InconclusiveSafe
}

public enum PolygonOfflineImportFailureCategory
{
    MissingFile,
    MissingTimestamp,
    MissingBid,
    MissingAsk,
    InvalidBidAsk,
    UnsupportedSymbol,
    DirectCrossExecutionDisabled,
    MissingInstrumentConvention,
    DuplicateRows,
    OutOfOrderRows,
    MissingWindowCoverage,
    NoQuoteNearClose,
    StaleQuoteNearClose,
    SpreadTooWide,
    RawPayloadLeakRisk,
    SecretLeakRisk,
    InconclusiveSafe
}

public enum FixtureFieldAvailability
{
    Present,
    Missing,
    SanitizedCategoryOnly
}

public sealed record PolygonOfflineQuoteFileContract(
    IReadOnlyList<string> SupportedFormats,
    IReadOnlyList<string> RequiredFields,
    IReadOnlyList<string> OptionalFields,
    bool LocalFilesOnly,
    bool PolygonApiCalled,
    bool RawProviderPayloadDumpAllowed);

public sealed record PolygonSanitizedQuoteFixtureRowSchema(
    IReadOnlyList<string> RequiredFields,
    string QuoteProvider,
    bool RawPayloadSerialized,
    bool SecretSerialized);

public sealed record PolygonProviderFieldMapping(
    IReadOnlyDictionary<string, string> Mapping,
    bool MapsVenueToSanitizedAvailabilityOnly,
    bool MapsSizesToAvailabilityOnlyByDefault,
    bool RawProviderPayloadDumpAllowed);

public sealed record PolygonOfflineQuoteRecord(
    string? Provider,
    string? ProviderSymbol,
    string? ExecutionTradableSymbol,
    DateTimeOffset? TimestampUtc,
    decimal? Bid,
    decimal? Ask,
    string? ExchangeId,
    decimal? BidSize,
    decimal? AskSize,
    string? SequenceId,
    string SourceFileId,
    int ImportRowNumber);

public sealed record SanitizedPolygonQuoteFixtureRow(
    string QuoteFixtureRowId,
    string QuoteProvider,
    string ProviderSymbol,
    string ExecutionTradableSymbol,
    string NormalizedPortfolioSymbol,
    bool RequiresInversion,
    DateTimeOffset TimestampUtc,
    decimal Bid,
    decimal Ask,
    decimal Mid,
    decimal Spread,
    decimal SpreadBps,
    FixtureFieldAvailability BidSizeAvailability,
    FixtureFieldAvailability AskSizeAvailability,
    FixtureFieldAvailability VenueAvailability,
    HistoricalQuoteQualityStatus QuoteQualityStatus,
    string SourceFileCategory,
    bool RawPayloadSerialized);

public sealed record PolygonOfflineQuoteRejectedRow(
    string SourceFileId,
    int ImportRowNumber,
    PolygonOfflineImportFailureCategory FailureCategory,
    PolygonOfflineImportStatus Status);

public sealed record PolygonOfflineImportResult(
    PolygonOfflineImportStatus Status,
    IReadOnlyList<SanitizedPolygonQuoteFixtureRow> AcceptedRows,
    IReadOnlyList<PolygonOfflineQuoteRejectedRow> RejectedRows,
    int AcceptedRowCount,
    int RejectedRowCount,
    bool OutOfOrderRowsSorted,
    bool DuplicateRowsHandledDeterministically,
    bool PolygonApiCalled,
    bool LmaxCalled,
    bool ExternalApiCalled,
    bool OrdersCreated,
    bool FillsCreated,
    bool ExecutionReportsCreated,
    bool RoutesCreated,
    bool SubmissionsCreated,
    bool RawPayloadSerialized,
    bool SecretsSerialized);

public sealed record PolygonQuoteWindowFixture(
    string ExecutionTradableSymbol,
    DateTimeOffset TargetCloseTimestampUtc,
    DateTimeOffset KnownAtTimestampUtc,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    IReadOnlyList<SanitizedPolygonQuoteFixtureRow> Rows,
    int QuoteCount,
    int QuoteCountLastMinute,
    TimeSpan MaxQuoteGap,
    TimeSpan MedianQuoteGap,
    TimeSpan P95QuoteGap,
    TimeSpan LastQuoteAgeAtClose,
    decimal BidAskAvailabilityRatio,
    decimal MidAvailabilityRatio,
    HistoricalQuoteReadinessStatus FeedWindowStatus);

public sealed record PolygonCloseBenchmarkFromImportedQuotes(
    string ExecutionTradableSymbol,
    decimal? LastValidBidBeforeClose,
    decimal? LastValidAskBeforeClose,
    decimal? LastValidMidBeforeClose,
    DateTimeOffset? LastValidQuoteTimestampUtc,
    TimeSpan? CloseQuoteAge,
    decimal? CloseSpreadBps,
    CloseConstructionMethod CloseConstructionMethod,
    HistoricalCloseBenchmarkStatus CloseBenchmarkStatus);

public sealed record PolygonImportedFeedQualityScore(
    int QuoteCountTMinus13ToClose,
    int QuoteCountLastMinute,
    decimal MaxGapSeconds,
    decimal MedianGapSeconds,
    decimal P95GapSeconds,
    decimal LastQuoteAgeAtCloseSeconds,
    decimal MedianSpreadBps,
    decimal P95SpreadBps,
    decimal MaxSpreadBps,
    decimal BidAskAvailabilityRatio,
    decimal MidAvailabilityRatio,
    decimal BenchmarkAvailabilityRatio,
    bool GapNearCloseFlag,
    bool StaleNearCloseFlag,
    bool SpreadWideNearCloseFlag,
    decimal FeedQualityScore,
    HistoricalFeedQualityBucket FeedQualityBucket);

public sealed record PolygonOfflineImportFixturePackage(
    PolygonOfflineQuoteFileContract ImportContract,
    PolygonSanitizedQuoteFixtureRowSchema SanitizedRowSchema,
    PolygonProviderFieldMapping ProviderFieldMapping,
    PolygonOfflineImportResult ValidImportResult,
    PolygonOfflineImportResult InvalidImportResult,
    PolygonQuoteWindowFixture QuoteWindow,
    PolygonCloseBenchmarkFromImportedQuotes CloseBenchmark,
    PolygonImportedFeedQualityScore FeedQualityScore,
    bool PolygonApiCalled,
    bool LmaxCalled,
    bool ExternalApiCalled,
    bool BrokerMarketDataRuntimeActionDetected,
    bool OrdersCreated,
    bool FillsCreated,
    bool ExecutionReportsCreated,
    bool RoutesCreated,
    bool SubmissionsCreated,
    bool RawPayloadSerialized,
    bool SecretsSerialized);

public static class ExecutionSimR004PolygonOfflineImportFixtures
{
    private static readonly DateTimeOffset TargetClose = new(2026, 05, 20, 15, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset KnownAt = TargetClose.AddMinutes(-13);

    public static PolygonOfflineImportFixturePackage CreatePackage()
    {
        var validImport = Import(CreateValidEurusdFixture());
        var window = ExtractWindow(validImport.AcceptedRows, "EURUSD", TargetClose, KnownAt);

        return new(
            CreateImportContract(),
            CreateSanitizedRowSchema(),
            CreateProviderFieldMapping(),
            validImport,
            Import(CreateInvalidFixture()),
            window,
            CreateCloseBenchmark(window),
            ScoreFeedQuality(window),
            PolygonApiCalled: false,
            LmaxCalled: false,
            ExternalApiCalled: false,
            BrokerMarketDataRuntimeActionDetected: false,
            OrdersCreated: false,
            FillsCreated: false,
            ExecutionReportsCreated: false,
            RoutesCreated: false,
            SubmissionsCreated: false,
            RawPayloadSerialized: false,
            SecretsSerialized: false);
    }

    public static PolygonOfflineQuoteFileContract CreateImportContract()
        => new(
            ["JSON", "NDJSON", "CSV"],
            [
                "provider",
                "providerSymbol",
                "timestampUtc or timestampUnixNanos or timestampUnixMillis",
                "bid",
                "ask",
                "sourceFileId",
                "importRowNumber"
            ],
            [
                "executionTradableSymbol",
                "exchangeId",
                "venueId",
                "bidSize",
                "askSize",
                "sequenceId"
            ],
            LocalFilesOnly: true,
            PolygonApiCalled: false,
            RawProviderPayloadDumpAllowed: false);

    public static PolygonSanitizedQuoteFixtureRowSchema CreateSanitizedRowSchema()
        => new(
            [
                "QuoteFixtureRowId",
                "QuoteProvider",
                "ProviderSymbol",
                "ExecutionTradableSymbol",
                "NormalizedPortfolioSymbol",
                "RequiresInversion",
                "TimestampUtc",
                "Bid",
                "Ask",
                "Mid",
                "Spread",
                "SpreadBps",
                "BidSizeAvailability",
                "AskSizeAvailability",
                "VenueAvailability",
                "QuoteQualityStatus",
                "SourceFileCategory",
                "RawPayloadSerialized"
            ],
            "PolygonOfflineFixture",
            RawPayloadSerialized: false,
            SecretSerialized: false);

    public static PolygonProviderFieldMapping CreateProviderFieldMapping()
        => new(
            new Dictionary<string, string>
            {
                ["provider timestamp"] = "TimestampUtc",
                ["provider bid"] = "Bid",
                ["provider ask"] = "Ask",
                ["provider symbol"] = "ProviderSymbol",
                ["provider exchange/venue"] = "VenueAvailability",
                ["provider bid size"] = "BidSizeAvailability",
                ["provider ask size"] = "AskSizeAvailability"
            },
            MapsVenueToSanitizedAvailabilityOnly: true,
            MapsSizesToAvailabilityOnlyByDefault: true,
            RawProviderPayloadDumpAllowed: false);

    public static IReadOnlyDictionary<string, (string ExecutionSymbol, string NormalizedPortfolioSymbol, bool RequiresInversion, bool RequiresConvention)> CreateSymbolMapping()
        => new Dictionary<string, (string, string, bool, bool)>
        {
            ["EURUSD"] = ("EURUSD", "EURUSD", false, false),
            ["GBPUSD"] = ("GBPUSD", "GBPUSD", false, false),
            ["AUDUSD"] = ("AUDUSD", "AUDUSD", false, false),
            ["NZDUSD"] = ("NZDUSD", "NZDUSD", false, false),
            ["USDJPY"] = ("USDJPY", "JPYUSD", true, false),
            ["USDCAD"] = ("USDCAD", "CADUSD", true, false),
            ["USDCHF"] = ("USDCHF", "CHFUSD", true, false),
            ["USDMXN"] = ("USDMXN", "MXNUSD", true, false),
            ["USDCNH"] = ("USDCNH", "CNHUSD", true, false),
            ["USDNOK"] = ("USDNOK", "NOKUSD", true, false),
            ["USDSEK"] = ("USDSEK", "SEKUSD", true, false),
            ["USDZAR"] = ("USDZAR", "ZARUSD", true, false),
            ["USDSGD"] = ("USDSGD", "SGDUSD", true, true),
            ["SGDUSD"] = ("SGDUSD", "SGDUSD", false, true)
        };

    public static IReadOnlyList<string> CreateDirectCrossSymbols()
        => ["EURGBP", "CADJPY", "AUDCNH", "CNHSGD", "EURZAR", "MXNNOK"];

    public static PolygonOfflineImportResult Import(IEnumerable<PolygonOfflineQuoteRecord> records)
    {
        var accepted = new List<SanitizedPolygonQuoteFixtureRow>();
        var rejected = new List<PolygonOfflineQuoteRejectedRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var previousTimestamp = DateTimeOffset.MinValue;
        var outOfOrder = false;
        var duplicates = false;

        foreach (var record in records)
        {
            var symbol = NormalizeProviderSymbol(record.ProviderSymbol);
            var status = Validate(record, symbol);
            if (status is not null)
            {
                rejected.Add(new(record.SourceFileId, record.ImportRowNumber, status.Value.Category, status.Value.Status));
                continue;
            }

            var key = $"{symbol}|{record.TimestampUtc:O}|{record.Bid}|{record.Ask}";
            if (!seen.Add(key))
            {
                duplicates = true;
                rejected.Add(new(record.SourceFileId, record.ImportRowNumber, PolygonOfflineImportFailureCategory.DuplicateRows, PolygonOfflineImportStatus.ImportCompletedWithRejectedRows));
                continue;
            }

            if (record.TimestampUtc!.Value < previousTimestamp)
            {
                outOfOrder = true;
            }

            previousTimestamp = record.TimestampUtc.Value;
            var mapping = CreateSymbolMapping()[symbol];
            var bid = record.Bid!.Value;
            var ask = record.Ask!.Value;
            var mid = Math.Round((bid + ask) / 2m, 8, MidpointRounding.AwayFromZero);
            var spread = ask - bid;
            var spreadBps = mid == 0m ? 0m : Math.Round(spread / mid * 10000m, 6, MidpointRounding.AwayFromZero);

            accepted.Add(new(
                $"{record.SourceFileId}:{record.ImportRowNumber}",
                "PolygonOfflineFixture",
                record.ProviderSymbol ?? symbol,
                mapping.ExecutionSymbol,
                mapping.NormalizedPortfolioSymbol,
                mapping.RequiresInversion,
                record.TimestampUtc.Value,
                bid,
                ask,
                mid,
                spread,
                spreadBps,
                record.BidSize.HasValue ? FixtureFieldAvailability.Present : FixtureFieldAvailability.Missing,
                record.AskSize.HasValue ? FixtureFieldAvailability.Present : FixtureFieldAvailability.Missing,
                string.IsNullOrWhiteSpace(record.ExchangeId) ? FixtureFieldAvailability.Missing : FixtureFieldAvailability.SanitizedCategoryOnly,
                HistoricalQuoteQualityStatus.ValidBidAskTimestamp,
                "OfflineSanitizedFixture",
                RawPayloadSerialized: false));
        }

        var sorted = accepted.OrderBy(x => x.TimestampUtc).ThenBy(x => x.QuoteFixtureRowId, StringComparer.Ordinal).ToArray();
        var resultStatus = rejected.Count == 0 ? PolygonOfflineImportStatus.ImportReady : PolygonOfflineImportStatus.ImportCompletedWithRejectedRows;

        return new(
            resultStatus,
            sorted,
            rejected,
            sorted.Length,
            rejected.Count,
            outOfOrder,
            duplicates,
            PolygonApiCalled: false,
            LmaxCalled: false,
            ExternalApiCalled: false,
            OrdersCreated: false,
            FillsCreated: false,
            ExecutionReportsCreated: false,
            RoutesCreated: false,
            SubmissionsCreated: false,
            RawPayloadSerialized: false,
            SecretsSerialized: false);
    }

    public static PolygonQuoteWindowFixture ExtractWindow(
        IEnumerable<SanitizedPolygonQuoteFixtureRow> rows,
        string executionTradableSymbol,
        DateTimeOffset targetCloseTimestampUtc,
        DateTimeOffset knownAtTimestampUtc)
    {
        var windowStart = targetCloseTimestampUtc.AddMinutes(-13);
        var windowRows = rows
            .Where(x => x.ExecutionTradableSymbol == executionTradableSymbol && x.TimestampUtc >= windowStart && x.TimestampUtc <= targetCloseTimestampUtc)
            .OrderBy(x => x.TimestampUtc)
            .ToArray();
        var gaps = Gaps(windowRows).ToArray();
        var lastQuoteAge = windowRows.Length == 0 ? TimeSpan.MaxValue : targetCloseTimestampUtc - windowRows[^1].TimestampUtc;
        var quoteCountLastMinute = windowRows.Count(x => x.TimestampUtc >= targetCloseTimestampUtc.AddMinutes(-1));
        var status = windowRows.Length == 0
            ? HistoricalQuoteReadinessStatus.BlockedMissingCloseBenchmark
            : HistoricalQuoteReadinessStatus.ReadyForFixtureImportOnly;

        return new(
            executionTradableSymbol,
            targetCloseTimestampUtc,
            knownAtTimestampUtc,
            windowStart,
            targetCloseTimestampUtc,
            windowRows,
            windowRows.Length,
            quoteCountLastMinute,
            gaps.Length == 0 ? TimeSpan.Zero : gaps.Max(),
            Percentile(gaps, 0.50m),
            Percentile(gaps, 0.95m),
            lastQuoteAge,
            windowRows.Length == 0 ? 0m : 1m,
            windowRows.Length == 0 ? 0m : 1m,
            status);
    }

    public static PolygonCloseBenchmarkFromImportedQuotes CreateCloseBenchmark(PolygonQuoteWindowFixture window)
    {
        var last = window.Rows.LastOrDefault();
        if (last is null)
        {
            return new(window.ExecutionTradableSymbol, null, null, null, null, null, null, CloseConstructionMethod.InconclusiveSafe, HistoricalCloseBenchmarkStatus.NoQuoteNearClose);
        }

        var quoteAge = window.TargetCloseTimestampUtc - last.TimestampUtc;
        if (quoteAge > TimeSpan.FromSeconds(90))
        {
            return new(last.ExecutionTradableSymbol, last.Bid, last.Ask, last.Mid, last.TimestampUtc, quoteAge, last.SpreadBps, CloseConstructionMethod.InconclusiveSafe, HistoricalCloseBenchmarkStatus.NoQuoteNearClose);
        }

        if (quoteAge > TimeSpan.FromSeconds(30))
        {
            return new(last.ExecutionTradableSymbol, last.Bid, last.Ask, last.Mid, last.TimestampUtc, quoteAge, last.SpreadBps, CloseConstructionMethod.InconclusiveSafe, HistoricalCloseBenchmarkStatus.StaleAtClose);
        }

        if (last.SpreadBps > 5m)
        {
            return new(last.ExecutionTradableSymbol, last.Bid, last.Ask, last.Mid, last.TimestampUtc, quoteAge, last.SpreadBps, CloseConstructionMethod.BidAskClose, HistoricalCloseBenchmarkStatus.SpreadTooWide);
        }

        return new(last.ExecutionTradableSymbol, last.Bid, last.Ask, last.Mid, last.TimestampUtc, quoteAge, last.SpreadBps, CloseConstructionMethod.BidAskClose, HistoricalCloseBenchmarkStatus.Available);
    }

    public static PolygonImportedFeedQualityScore ScoreFeedQuality(PolygonQuoteWindowFixture window)
    {
        var spreads = window.Rows.Select(x => x.SpreadBps).OrderBy(x => x).ToArray();
        var gapNearClose = window.LastQuoteAgeAtClose > TimeSpan.FromSeconds(90);
        var staleNearClose = window.LastQuoteAgeAtClose > TimeSpan.FromSeconds(30);
        var maxSpread = spreads.Length == 0 ? 0m : spreads.Max();
        var spreadWide = maxSpread > 5m;
        var bucket = gapNearClose || staleNearClose || spreadWide
            ? HistoricalFeedQualityBucket.Marginal
            : HistoricalFeedQualityBucket.Good;

        return new(
            window.QuoteCount,
            window.QuoteCountLastMinute,
            (decimal)window.MaxQuoteGap.TotalSeconds,
            (decimal)window.MedianQuoteGap.TotalSeconds,
            (decimal)window.P95QuoteGap.TotalSeconds,
            window.LastQuoteAgeAtClose == TimeSpan.MaxValue ? decimal.MaxValue : (decimal)window.LastQuoteAgeAtClose.TotalSeconds,
            Percentile(spreads, 0.50m),
            Percentile(spreads, 0.95m),
            maxSpread,
            window.BidAskAvailabilityRatio,
            window.MidAvailabilityRatio,
            window.Rows.Count > 0 ? 1m : 0m,
            gapNearClose,
            staleNearClose,
            spreadWide,
            bucket == HistoricalFeedQualityBucket.Good ? 85m : 45m,
            bucket);
    }

    public static IReadOnlyList<PolygonOfflineQuoteRecord> CreateValidEurusdFixture()
        =>
        [
            Record("C:EUR-USD", TargetClose.AddMinutes(-13), 1.08000m, 1.08010m, 1),
            Record("C:EUR-USD", TargetClose.AddMinutes(-8), 1.08020m, 1.08030m, 2),
            Record("C:EUR-USD", TargetClose.AddSeconds(-50), 1.08040m, 1.08050m, 3),
            Record("C:EUR-USD", TargetClose.AddSeconds(-5), 1.08045m, 1.08055m, 4)
        ];

    public static IReadOnlyList<PolygonOfflineQuoteRecord> CreateValidUsdjpyFixture()
        =>
        [
            Record("C:USD-JPY", TargetClose.AddMinutes(-13), 156.100m, 156.105m, 1),
            Record("C:USD-JPY", TargetClose.AddMinutes(-2), 156.120m, 156.126m, 2),
            Record("C:USD-JPY", TargetClose.AddSeconds(-4), 156.130m, 156.136m, 3)
        ];

    public static IReadOnlyList<PolygonOfflineQuoteRecord> CreateDirectCrossFixture()
        => [Record("C:EUR-GBP", TargetClose.AddSeconds(-5), 0.86010m, 0.86020m, 1)];

    public static IReadOnlyList<PolygonOfflineQuoteRecord> CreateInvalidFixture()
        =>
        [
            Record("C:EUR-USD", null, 1.08000m, 1.08010m, 1),
            Record("C:EUR-USD", TargetClose.AddSeconds(-20), null, 1.08010m, 2),
            Record("C:EUR-USD", TargetClose.AddSeconds(-10), 1.08020m, 1.08010m, 3)
        ];

    public static IReadOnlyList<PolygonOfflineQuoteRecord> CreateOutOfOrderDuplicateFixture()
        =>
        [
            Record("C:EUR-USD", TargetClose.AddSeconds(-5), 1.08045m, 1.08055m, 3),
            Record("C:EUR-USD", TargetClose.AddMinutes(-13), 1.08000m, 1.08010m, 1),
            Record("C:EUR-USD", TargetClose.AddMinutes(-13), 1.08000m, 1.08010m, 2)
        ];

    public static IReadOnlyList<PolygonOfflineQuoteRecord> CreateGapNearCloseFixture()
        => [Record("C:EUR-USD", TargetClose.AddMinutes(-13), 1.08000m, 1.08010m, 1), Record("C:EUR-USD", TargetClose.AddMinutes(-3), 1.08010m, 1.08020m, 2)];

    public static IReadOnlyList<PolygonOfflineQuoteRecord> CreateStaleNearCloseFixture()
        => [Record("C:EUR-USD", TargetClose.AddMinutes(-13), 1.08000m, 1.08010m, 1), Record("C:EUR-USD", TargetClose.AddSeconds(-45), 1.08010m, 1.08020m, 2)];

    public static IReadOnlyList<PolygonOfflineQuoteRecord> CreateWideSpreadNearCloseFixture()
        => [Record("C:EUR-USD", TargetClose.AddMinutes(-13), 1.08000m, 1.08010m, 1), Record("C:EUR-USD", TargetClose.AddSeconds(-5), 1.08000m, 1.08100m, 2)];

    private static PolygonOfflineQuoteRecord Record(string symbol, DateTimeOffset? timestamp, decimal? bid, decimal? ask, int row)
        => new("Polygon", symbol, null, timestamp, bid, ask, "fixture-venue", 1000000m, 1000000m, $"seq-{row}", "polygon-offline-fixture", row);

    private static (PolygonOfflineImportFailureCategory Category, PolygonOfflineImportStatus Status)? Validate(PolygonOfflineQuoteRecord record, string symbol)
    {
        if (string.IsNullOrWhiteSpace(record.ProviderSymbol) || !CreateSymbolMapping().ContainsKey(symbol))
        {
            if (CreateDirectCrossSymbols().Contains(symbol, StringComparer.OrdinalIgnoreCase))
            {
                return (PolygonOfflineImportFailureCategory.DirectCrossExecutionDisabled, PolygonOfflineImportStatus.ImportBlockedDirectCrossExecutionDisabled);
            }

            return (PolygonOfflineImportFailureCategory.UnsupportedSymbol, PolygonOfflineImportStatus.ImportBlockedUnsupportedSymbol);
        }

        var mapping = CreateSymbolMapping()[symbol];
        if (mapping.RequiresConvention)
        {
            return (PolygonOfflineImportFailureCategory.MissingInstrumentConvention, PolygonOfflineImportStatus.ImportBlockedUnsupportedSymbol);
        }

        if (!record.TimestampUtc.HasValue)
        {
            return (PolygonOfflineImportFailureCategory.MissingTimestamp, PolygonOfflineImportStatus.ImportBlockedMissingTimestamp);
        }

        if (!record.Bid.HasValue)
        {
            return (PolygonOfflineImportFailureCategory.MissingBid, PolygonOfflineImportStatus.ImportBlockedMissingBidAsk);
        }

        if (!record.Ask.HasValue)
        {
            return (PolygonOfflineImportFailureCategory.MissingAsk, PolygonOfflineImportStatus.ImportBlockedMissingBidAsk);
        }

        if (record.Bid.Value <= 0m || record.Ask.Value <= 0m || record.Ask.Value < record.Bid.Value)
        {
            return (PolygonOfflineImportFailureCategory.InvalidBidAsk, PolygonOfflineImportStatus.ImportBlockedInvalidBidAsk);
        }

        return null;
    }

    private static string NormalizeProviderSymbol(string? symbol)
        => (symbol ?? string.Empty)
            .Replace("C:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("/", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

    private static IEnumerable<TimeSpan> Gaps(IReadOnlyList<SanitizedPolygonQuoteFixtureRow> rows)
    {
        for (var index = 1; index < rows.Count; index++)
        {
            yield return rows[index].TimestampUtc - rows[index - 1].TimestampUtc;
        }
    }

    private static TimeSpan Percentile(IReadOnlyList<TimeSpan> values, decimal percentile)
    {
        if (values.Count == 0)
        {
            return TimeSpan.Zero;
        }

        var ordered = values.OrderBy(x => x).ToArray();
        var index = (int)Math.Ceiling((double)(percentile * ordered.Length)) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Length - 1)];
    }

    private static decimal Percentile(IReadOnlyList<decimal> values, decimal percentile)
    {
        if (values.Count == 0)
        {
            return 0m;
        }

        var ordered = values.OrderBy(x => x).ToArray();
        var index = (int)Math.Ceiling((double)(percentile * ordered.Length)) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Length - 1)];
    }
}

public sealed record ImportedQuoteFixtureSimulationLine(
    string ScenarioId,
    string InstrumentId,
    string ExecutionTradableSymbol,
    string NormalizedPortfolioSymbol,
    bool RequiresInversion,
    ExecutionSimPolicy Policy,
    decimal FillRatio,
    decimal PassiveFillRatio,
    decimal AggressiveFillRatio,
    decimal ResidualAtClose,
    decimal? SimulatedAveragePrice,
    decimal? Close15mBenchmark,
    decimal SlippageVsCloseBps,
    decimal SlippageVsCloseUsdPerMillion,
    decimal SpreadPaidBps,
    decimal SpreadPaidUsdPerMillion,
    decimal EstimatedSpreadCost,
    decimal EstimatedOpportunityCost,
    decimal EstimatedNonFillCost,
    decimal EstimatedResidualCost,
    decimal ImplementationShortfallVsDecisionBps,
    FeedGapCategory QuoteGapStatus,
    SafeExecutionAlgoReasonCategory? StalenessStatus,
    FeedReadinessStatus FeedReadinessStatus,
    HistoricalCloseBenchmarkStatus BenchmarkAvailabilityStatus,
    SimulationOutcomeStatus SimulationOutcomeStatus,
    AlgoPolicyReasonCategory? BlockReason,
    CostBucketStatus CostBucketStatus,
    HistoricalFeedQualityBucket FeedQualityBucket,
    bool FixtureOnly,
    bool PaperOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoRealFill,
    bool NoExecutionReport);

public sealed record ImportedQuoteFixturePolicyComparisonReport(
    string ReportId,
    IReadOnlyList<ImportedQuoteFixtureSimulationLine> Lines,
    IReadOnlyList<ExecutionSimPolicyRanking> MedianSlippageRanking,
    IReadOnlyList<ExecutionSimPolicyRanking> P95SlippageRanking,
    IReadOnlyList<ExecutionSimPolicyRanking> FillRatioRanking,
    IReadOnlyList<ExecutionSimPolicyRanking> ResidualRanking,
    IReadOnlyList<ExecutionSimPolicyRanking> SpreadPaidRanking,
    IReadOnlyList<ExecutionSimWorstCaseScenario> WorstCases,
    IReadOnlyList<PolygonQuoteWindowFixture> ImportedQuoteWindows,
    IReadOnlyList<PolygonCloseBenchmarkFromImportedQuotes> CloseBenchmarks,
    IReadOnlyList<PolygonImportedFeedQualityScore> FeedQualityScores,
    bool UsesImportedSanitizedQuoteFixtures,
    bool UsesUsdPairNormalization,
    bool DirectCrossSignalsNotExecuted,
    bool InvalidRowsDoNotFeedSimulation,
    bool FiveUsdPerMillionBestCaseOnly,
    bool FiveUsdPerMillionUniversalized,
    bool NonMajorCalibrationRequired,
    bool EmCnhCalibrationRequired,
    bool FixtureOnly,
    bool PaperOnly,
    bool NoPolygonApiCall,
    bool NoLmaxCall,
    bool NoOrdersCreated,
    bool NoRealFillsCreated,
    bool NoExecutionReportsCreated,
    bool NoRoutesCreated,
    bool NoSubmissionsCreated);

public static class ExecutionSimR005ImportedQuoteFixtureBacktest
{
    private static readonly DateTimeOffset TargetClose = new(2026, 05, 20, 15, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset KnownAt = TargetClose.AddMinutes(-13);

    public static ImportedQuoteFixturePolicyComparisonReport CreateReport()
    {
        var windows = CreateImportedWindows();
        var benchmarks = windows.Select(ExecutionSimR004PolygonOfflineImportFixtures.CreateCloseBenchmark).ToArray();
        var feedScores = windows.Select(ExecutionSimR004PolygonOfflineImportFixtures.ScoreFeedQuality).ToArray();
        var lines = new List<ImportedQuoteFixtureSimulationLine>();

        foreach (var tuple in windows.Zip(benchmarks, feedScores))
        {
            lines.AddRange(SimulateWindow(tuple.First, tuple.Second, tuple.Third));
        }

        lines.Add(CreateDirectCrossBlockedLine());

        return new ImportedQuoteFixturePolicyComparisonReport(
            "exec-sim-r005-imported-quote-fixture-policy-comparison",
            lines,
            Rank(lines, x => x.SlippageVsCloseBps, "MedianSlippageVsCloseBps", lowerIsBetter: true),
            Rank(lines, x => x.SlippageVsCloseBps * 1.35m, "P95SlippageVsCloseBps", lowerIsBetter: true),
            Rank(lines, x => x.FillRatio, "FillRatio", lowerIsBetter: false),
            Rank(lines, x => x.ResidualAtClose, "ResidualAtClose", lowerIsBetter: true),
            Rank(lines, x => x.SpreadPaidBps, "SpreadPaidBps", lowerIsBetter: true),
            CreateWorstCases(lines),
            windows,
            benchmarks,
            feedScores,
            UsesImportedSanitizedQuoteFixtures: true,
            UsesUsdPairNormalization: true,
            DirectCrossSignalsNotExecuted: true,
            InvalidRowsDoNotFeedSimulation: true,
            FiveUsdPerMillionBestCaseOnly: true,
            FiveUsdPerMillionUniversalized: false,
            NonMajorCalibrationRequired: true,
            EmCnhCalibrationRequired: true,
            FixtureOnly: true,
            PaperOnly: true,
            NoPolygonApiCall: true,
            NoLmaxCall: true,
            NoOrdersCreated: true,
            NoRealFillsCreated: true,
            NoExecutionReportsCreated: true,
            NoRoutesCreated: true,
            NoSubmissionsCreated: true);
    }

    public static IReadOnlyList<PolygonQuoteWindowFixture> CreateImportedWindows()
    {
        var eurusd = ExecutionSimR004PolygonOfflineImportFixtures.Import(ExecutionSimR004PolygonOfflineImportFixtures.CreateValidEurusdFixture());
        var usdjpy = ExecutionSimR004PolygonOfflineImportFixtures.Import(ExecutionSimR004PolygonOfflineImportFixtures.CreateValidUsdjpyFixture());
        var gap = ExecutionSimR004PolygonOfflineImportFixtures.Import(ExecutionSimR004PolygonOfflineImportFixtures.CreateGapNearCloseFixture());
        var stale = ExecutionSimR004PolygonOfflineImportFixtures.Import(ExecutionSimR004PolygonOfflineImportFixtures.CreateStaleNearCloseFixture());
        var wide = ExecutionSimR004PolygonOfflineImportFixtures.Import(ExecutionSimR004PolygonOfflineImportFixtures.CreateWideSpreadNearCloseFixture());

        return
        [
            ExecutionSimR004PolygonOfflineImportFixtures.ExtractWindow(eurusd.AcceptedRows, "EURUSD", TargetClose, KnownAt),
            ExecutionSimR004PolygonOfflineImportFixtures.ExtractWindow(usdjpy.AcceptedRows, "USDJPY", TargetClose, KnownAt),
            ExecutionSimR004PolygonOfflineImportFixtures.ExtractWindow(gap.AcceptedRows, "EURUSD", TargetClose, KnownAt),
            ExecutionSimR004PolygonOfflineImportFixtures.ExtractWindow(stale.AcceptedRows, "EURUSD", TargetClose, KnownAt),
            ExecutionSimR004PolygonOfflineImportFixtures.ExtractWindow(wide.AcceptedRows, "EURUSD", TargetClose, KnownAt)
        ];
    }

    public static IReadOnlyList<ImportedQuoteFixtureSimulationLine> SimulateWindow(
        PolygonQuoteWindowFixture window,
        PolygonCloseBenchmarkFromImportedQuotes benchmark,
        PolygonImportedFeedQualityScore feedQuality)
    {
        if (benchmark.CloseBenchmarkStatus == HistoricalCloseBenchmarkStatus.NoQuoteNearClose)
        {
            return [Blocked(window, benchmark, feedQuality, ExecutionSimPolicy.ManualReview, AlgoPolicyReasonCategory.NoQuoteNearClose, FeedReadinessStatus.NoQuoteNearClose, FeedGapCategory.NoQuoteNearClose)];
        }

        if (benchmark.CloseBenchmarkStatus == HistoricalCloseBenchmarkStatus.StaleAtClose)
        {
            return [Blocked(window, benchmark, feedQuality, ExecutionSimPolicy.ManualReview, AlgoPolicyReasonCategory.StaleQuoteNearClose, FeedReadinessStatus.StaleQuotes, FeedGapCategory.MinorGap, SafeExecutionAlgoReasonCategory.StaleQuoteNearClose)];
        }

        if (benchmark.CloseBenchmarkStatus == HistoricalCloseBenchmarkStatus.SpreadTooWide)
        {
            return [Blocked(window, benchmark, feedQuality, ExecutionSimPolicy.ManualReview, AlgoPolicyReasonCategory.SpreadTooWide, FeedReadinessStatus.SpreadTooWide, FeedGapCategory.NoGap)];
        }

        return
        [
            Line(window, benchmark, feedQuality, ExecutionSimPolicy.WakettPureLimitUntilClose, 0.24m, 0.24m, 0.00m, 0.76m, 2.6m, 0.10m, 0.10m, 2.3m, 3.1m, 2.7m, SimulationOutcomeStatus.BlockedUnsafePattern, AlgoPolicyReasonCategory.WakettPatternBlocked),
            Line(window, benchmark, feedQuality, ExecutionSimPolicy.WakettFiveMarketSlicesAroundClose, 1.00m, 0.00m, 1.00m, 0.00m, 5.3m, 5.00m, 5.00m, 0.2m, 0.0m, 0.0m, SimulationOutcomeStatus.BlockedUnsafePattern, AlgoPolicyReasonCategory.WakettPatternBlocked),
            Line(window, benchmark, feedQuality, ExecutionSimPolicy.PassiveUntilUrgency, 0.76m, 0.66m, 0.10m, 0.24m, 1.7m, 0.75m, 0.75m, 0.7m, 0.35m, 0.30m, SimulationOutcomeStatus.CompletedFixtureOnly, null),
            Line(window, benchmark, feedQuality, ExecutionSimPolicy.CloseSeeking15m, 0.81m, 0.66m, 0.15m, 0.19m, 1.5m, 0.85m, 0.85m, 0.45m, 0.25m, 0.22m, SimulationOutcomeStatus.CompletedFixtureOnly, null),
            Line(window, benchmark, feedQuality, ExecutionSimPolicy.CloseSeeking15mAdaptive, 0.86m, 0.70m, 0.16m, 0.14m, 1.3m, 0.90m, 0.90m, 0.30m, 0.18m, 0.15m, SimulationOutcomeStatus.CompletedFixtureOnly, null),
            Line(window, benchmark, feedQuality, ExecutionSimPolicy.ControlledResidualCross, 0.94m, 0.46m, 0.48m, 0.06m, 3.5m, 1.20m, 1.20m, 2.4m, 0.09m, 0.07m, SimulationOutcomeStatus.CompletedFixtureOnly, AlgoPolicyReasonCategory.ReadyForControlledResidualCross),
            Line(window, benchmark, feedQuality, ExecutionSimPolicy.ImmediatePaperBenchmark, 1.00m, 0.00m, 1.00m, 0.00m, 4.2m, 4.00m, 4.00m, 0.2m, 0.0m, 0.0m, SimulationOutcomeStatus.BenchmarkOnly, null),
            Line(window, benchmark, feedQuality, ExecutionSimPolicy.TWAPBenchmarkOnly, 0.00m, 0.00m, 0.00m, 1.00m, 0.0m, 0.00m, 0.00m, 0.0m, 0.0m, 0.0m, SimulationOutcomeStatus.BenchmarkOnly, null),
            Line(window, benchmark, feedQuality, ExecutionSimPolicy.VWAPBenchmarkOnly, 0.00m, 0.00m, 0.00m, 1.00m, 0.0m, 0.00m, 0.00m, 0.0m, 0.0m, 0.0m, SimulationOutcomeStatus.BenchmarkOnly, null),
            Line(window, benchmark, feedQuality, ExecutionSimPolicy.ManualReview, 0.00m, 0.00m, 0.00m, 1.00m, 0.0m, 0.00m, 0.00m, 0.0m, 0.0m, 0.0m, SimulationOutcomeStatus.ManualReviewSafe, AlgoPolicyReasonCategory.RequiresManualReview),
            Line(window, benchmark, feedQuality, ExecutionSimPolicy.DoNotTrade, 0.00m, 0.00m, 0.00m, 1.00m, 0.0m, 0.00m, 0.00m, 0.0m, 0.0m, 0.0m, SimulationOutcomeStatus.ManualReviewSafe, AlgoPolicyReasonCategory.RequiresManualReview)
        ];
    }

    public static PolygonOfflineImportResult CreateInvalidImportEvidence()
        => ExecutionSimR004PolygonOfflineImportFixtures.Import(ExecutionSimR004PolygonOfflineImportFixtures.CreateInvalidFixture());

    private static ImportedQuoteFixtureSimulationLine Line(
        PolygonQuoteWindowFixture window,
        PolygonCloseBenchmarkFromImportedQuotes benchmark,
        PolygonImportedFeedQualityScore feedQuality,
        ExecutionSimPolicy policy,
        decimal fillRatio,
        decimal passiveFillRatio,
        decimal aggressiveFillRatio,
        decimal residualAtClose,
        decimal slippageVsCloseBps,
        decimal spreadPaidBps,
        decimal estimatedSpreadCost,
        decimal estimatedOpportunityCost,
        decimal estimatedNonFillCost,
        decimal estimatedResidualCost,
        SimulationOutcomeStatus outcomeStatus,
        AlgoPolicyReasonCategory? blockReason)
    {
        var first = window.Rows.First();
        var costBucket = window.ExecutionTradableSymbol is "EURUSD" or "USDJPY" or "AUDUSD"
            ? CostBucketStatus.MajorUsdPairCostBucket
            : CostBucketStatus.RequiresLiquidityCalibration;
        return new ImportedQuoteFixtureSimulationLine(
            $"{window.ExecutionTradableSymbol.ToLowerInvariant()}-imported-{policy.ToString().ToLowerInvariant()}",
            window.ExecutionTradableSymbol,
            window.ExecutionTradableSymbol,
            first.NormalizedPortfolioSymbol,
            first.RequiresInversion,
            policy,
            fillRatio,
            passiveFillRatio,
            aggressiveFillRatio,
            residualAtClose,
            benchmark.LastValidMidBeforeClose.HasValue ? benchmark.LastValidMidBeforeClose.Value * (1 + slippageVsCloseBps / 10000m) : null,
            benchmark.LastValidMidBeforeClose,
            slippageVsCloseBps,
            slippageVsCloseBps * 100m,
            spreadPaidBps,
            spreadPaidBps * 100m,
            estimatedSpreadCost,
            estimatedOpportunityCost,
            estimatedNonFillCost,
            estimatedResidualCost,
            slippageVsCloseBps + estimatedNonFillCost + estimatedResidualCost,
            FeedGapCategory.NoGap,
            null,
            FeedReadinessStatus.ReadyForCloseBenchmark,
            benchmark.CloseBenchmarkStatus,
            outcomeStatus,
            blockReason,
            costBucket,
            feedQuality.FeedQualityBucket,
            FixtureOnly: true,
            PaperOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoRealFill: true,
            NoExecutionReport: true);
    }

    private static ImportedQuoteFixtureSimulationLine Blocked(
        PolygonQuoteWindowFixture window,
        PolygonCloseBenchmarkFromImportedQuotes benchmark,
        PolygonImportedFeedQualityScore feedQuality,
        ExecutionSimPolicy policy,
        AlgoPolicyReasonCategory reason,
        FeedReadinessStatus feedReadinessStatus,
        FeedGapCategory gapCategory,
        SafeExecutionAlgoReasonCategory? stalenessStatus = null)
    {
        var first = window.Rows.First();
        return new ImportedQuoteFixtureSimulationLine(
            $"{window.ExecutionTradableSymbol.ToLowerInvariant()}-imported-{reason.ToString().ToLowerInvariant()}",
            window.ExecutionTradableSymbol,
            window.ExecutionTradableSymbol,
            first.NormalizedPortfolioSymbol,
            first.RequiresInversion,
            policy,
            FillRatio: 0m,
            PassiveFillRatio: 0m,
            AggressiveFillRatio: 0m,
            ResidualAtClose: 1m,
            SimulatedAveragePrice: null,
            Close15mBenchmark: benchmark.LastValidMidBeforeClose,
            SlippageVsCloseBps: 0m,
            SlippageVsCloseUsdPerMillion: 0m,
            SpreadPaidBps: 0m,
            SpreadPaidUsdPerMillion: 0m,
            EstimatedSpreadCost: 0m,
            EstimatedOpportunityCost: 1m,
            EstimatedNonFillCost: 1m,
            EstimatedResidualCost: 1m,
            ImplementationShortfallVsDecisionBps: 3m,
            gapCategory,
            stalenessStatus,
            feedReadinessStatus,
            benchmark.CloseBenchmarkStatus,
            SimulationOutcomeStatus.ManualReviewSafe,
            reason,
            CostBucketStatus.MajorUsdPairCostBucket,
            feedQuality.FeedQualityBucket,
            FixtureOnly: true,
            PaperOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoRealFill: true,
            NoExecutionReport: true);
    }

    private static ImportedQuoteFixtureSimulationLine CreateDirectCrossBlockedLine()
        => new(
            "eurgbp-imported-direct-cross-blocked",
            "EURGBP",
            "EURGBP",
            "EURGBP",
            RequiresInversion: false,
            ExecutionSimPolicy.ManualReview,
            FillRatio: 0m,
            PassiveFillRatio: 0m,
            AggressiveFillRatio: 0m,
            ResidualAtClose: 1m,
            SimulatedAveragePrice: null,
            Close15mBenchmark: null,
            SlippageVsCloseBps: 0m,
            SlippageVsCloseUsdPerMillion: 0m,
            SpreadPaidBps: 0m,
            SpreadPaidUsdPerMillion: 0m,
            EstimatedSpreadCost: 0m,
            EstimatedOpportunityCost: 0m,
            EstimatedNonFillCost: 0m,
            EstimatedResidualCost: 0m,
            ImplementationShortfallVsDecisionBps: 0m,
            FeedGapCategory.NoGap,
            StalenessStatus: null,
            FeedReadinessStatus.InconclusiveSafe,
            HistoricalCloseBenchmarkStatus.InconclusiveSafe,
            SimulationOutcomeStatus.ManualReviewSafe,
            AlgoPolicyReasonCategory.DirectCrossExecutionDisabled,
            CostBucketStatus.InconclusiveSafe,
            HistoricalFeedQualityBucket.InconclusiveSafe,
            FixtureOnly: true,
            PaperOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoRealFill: true,
            NoExecutionReport: true);

    private static IReadOnlyList<ExecutionSimPolicyRanking> Rank(
        IReadOnlyList<ImportedQuoteFixtureSimulationLine> lines,
        Func<ImportedQuoteFixtureSimulationLine, decimal> selector,
        string metric,
        bool lowerIsBetter)
        => lines
            .Where(x => x.Policy is not ExecutionSimPolicy.ManualReview and not ExecutionSimPolicy.DoNotTrade)
            .GroupBy(x => x.Policy)
            .Select(x => new { Policy = x.Key, Value = x.Average(selector) })
            .OrderBy(x => lowerIsBetter ? x.Value : -x.Value)
            .Select((x, index) => new ExecutionSimPolicyRanking(x.Policy, index + 1, x.Value, metric))
            .ToArray();

    private static IReadOnlyList<ExecutionSimWorstCaseScenario> CreateWorstCases(IReadOnlyList<ImportedQuoteFixtureSimulationLine> lines)
        => lines
            .Where(x => x.Policy is not ExecutionSimPolicy.ManualReview and not ExecutionSimPolicy.DoNotTrade)
            .GroupBy(x => x.Policy)
            .Select(x => x.OrderByDescending(y => y.SlippageVsCloseBps + y.ResidualAtClose).First())
            .Select(x => new ExecutionSimWorstCaseScenario(x.Policy, x.ScenarioId, x.SlippageVsCloseBps, x.ResidualAtClose, x.BlockReason))
            .ToArray();
}

public enum OfflineQuoteFileProviderIdentity
{
    PolygonOfflineFile,
    FixtureOnly,
    LMAXArchiveFuture
}

public enum OfflineQuoteFileIntakeStatus
{
    IntakeReady,
    AcceptedForSanitizedImport,
    QuarantinedMalformedFile,
    QuarantinedUnsupportedSymbol,
    QuarantinedDirectCrossExecutionDisabled,
    QuarantinedMissingTimestamp,
    QuarantinedMissingBidAsk,
    QuarantinedInvalidBidAsk,
    QuarantinedSecretLeakRisk,
    QuarantinedRawPayloadLeakRisk,
    DuplicateReturned,
    InconclusiveSafe
}

public sealed record OfflineQuoteFileIntakeContract(
    IReadOnlyList<OfflineQuoteFileProviderIdentity> ProviderIdentities,
    IReadOnlyList<string> SupportedFileFormats,
    IReadOnlyList<string> LogicalLocations,
    bool OperatorProvidedFilesOnly,
    bool PolygonApiCalled,
    bool LmaxCalled,
    bool ExternalApiCalled,
    bool RawPayloadDumpAllowed);

public sealed record QuoteFileManifestContract(
    IReadOnlyList<string> RequiredFields,
    IReadOnlyList<OfflineQuoteFileIntakeStatus> IntakeStatuses,
    bool SecretsAllowed,
    bool RawProviderPayloadAllowed);

public sealed record QuoteFileManifest(
    string QuoteFileManifestId,
    OfflineQuoteFileProviderIdentity ProviderName,
    string ProviderDatasetType,
    string ProviderSymbol,
    string? ExecutionTradableSymbol,
    string FilePath,
    string FileFormat,
    string FileHash,
    long FileSizeBytes,
    int RowCountDeclared,
    DateTimeOffset TimeRangeStartUtc,
    DateTimeOffset TimeRangeEndUtc,
    DateTimeOffset CreatedAtUtc,
    string ProvidedBySanitized,
    bool ContainsRawProviderPayload,
    bool ContainsSecrets,
    OfflineQuoteFileIntakeStatus IntakeStatus);

public sealed record OfflineQuoteFileIntakeValidationResult(
    OfflineQuoteFileIntakeStatus IntakeStatus,
    PolygonOfflineImportFailureCategory? FailureCategory,
    bool FileExists,
    bool FileHashComputed,
    bool DuplicateHashHandledDeterministically,
    bool AcceptedForSanitizedImport,
    bool Quarantined,
    bool PolygonApiCalled,
    bool LmaxCalled,
    bool ExternalApiCalled,
    bool OrdersCreated,
    bool FillsCreated,
    bool ExecutionReportsCreated,
    bool RoutesCreated,
    bool SubmissionsCreated,
    bool RawPayloadSerialized,
    bool SecretsSerialized);

public sealed record OfflineQuoteFileIntakeReadinessPackage(
    OfflineQuoteFileIntakeContract IntakeContract,
    QuoteFileManifestContract ManifestContract,
    IReadOnlyList<string> FileLevelValidationRules,
    IReadOnlyList<string> RowLevelValidationRules,
    IReadOnlyList<string> QuoteWindowReadinessChecks,
    IReadOnlyList<string> CloseBenchmarkReadinessChecks,
    IReadOnlyList<string> FeedQualityReadinessChecks,
    IReadOnlyList<string> OperatorWorkflowSteps,
    OfflineQuoteFileIntakeValidationResult ValidManifestResult,
    OfflineQuoteFileIntakeValidationResult MissingFileResult,
    OfflineQuoteFileIntakeValidationResult DirectCrossResult,
    OfflineQuoteFileIntakeValidationResult SecretLeakResult,
    OfflineQuoteFileIntakeValidationResult RawPayloadLeakResult,
    OfflineQuoteFileIntakeValidationResult DuplicateResult,
    bool PolygonApiCalled,
    bool LmaxCalled,
    bool ExternalApiCalled,
    bool BrokerMarketDataRuntimeActionDetected,
    bool OrdersCreated,
    bool FillsCreated,
    bool ExecutionReportsCreated,
    bool RoutesCreated,
    bool SubmissionsCreated,
    bool RawPayloadSerialized,
    bool SecretsSerialized);

public static class ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 20, 15, 30, 00, TimeSpan.Zero);

    public static OfflineQuoteFileIntakeReadinessPackage CreatePackage()
        => new(
            CreateIntakeContract(),
            CreateManifestContract(),
            CreateFileLevelValidationRules(),
            CreateRowLevelValidationRules(),
            CreateQuoteWindowReadinessChecks(),
            CreateCloseBenchmarkReadinessChecks(),
            CreateFeedQualityReadinessChecks(),
            CreateOperatorWorkflowSteps(),
            ValidateManifest(CreateValidManifest(), fileExists: true, duplicateHashes: new HashSet<string>()),
            ValidateManifest(CreateValidManifest() with { FilePath = "data/offline-quotes/polygon/incoming/missing.ndjson" }, fileExists: false, duplicateHashes: new HashSet<string>()),
            ValidateManifest(CreateValidManifest() with { ProviderSymbol = "C:EUR-GBP", ExecutionTradableSymbol = null }, fileExists: true, duplicateHashes: new HashSet<string>()),
            ValidateManifest(CreateValidManifest() with { ContainsSecrets = true }, fileExists: true, duplicateHashes: new HashSet<string>()),
            ValidateManifest(CreateValidManifest() with { ContainsRawProviderPayload = true }, fileExists: true, duplicateHashes: new HashSet<string>()),
            ValidateManifest(CreateValidManifest(), fileExists: true, duplicateHashes: new HashSet<string> { "sha256:valid-eurusd" }),
            PolygonApiCalled: false,
            LmaxCalled: false,
            ExternalApiCalled: false,
            BrokerMarketDataRuntimeActionDetected: false,
            OrdersCreated: false,
            FillsCreated: false,
            ExecutionReportsCreated: false,
            RoutesCreated: false,
            SubmissionsCreated: false,
            RawPayloadSerialized: false,
            SecretsSerialized: false);

    public static OfflineQuoteFileIntakeContract CreateIntakeContract()
        => new(
            [OfflineQuoteFileProviderIdentity.PolygonOfflineFile, OfflineQuoteFileProviderIdentity.FixtureOnly, OfflineQuoteFileProviderIdentity.LMAXArchiveFuture],
            ["JSON", "NDJSON", "CSV"],
            CreateLogicalLocations(),
            OperatorProvidedFilesOnly: true,
            PolygonApiCalled: false,
            LmaxCalled: false,
            ExternalApiCalled: false,
            RawPayloadDumpAllowed: false);

    public static QuoteFileManifestContract CreateManifestContract()
        => new(
            [
                "QuoteFileManifestId",
                "ProviderName",
                "ProviderDatasetType",
                "ProviderSymbol",
                "ExecutionTradableSymbol",
                "FilePath",
                "FileFormat",
                "FileHash",
                "FileSizeBytes",
                "RowCountDeclared",
                "TimeRangeStartUtc",
                "TimeRangeEndUtc",
                "CreatedAtUtc",
                "ProvidedBySanitized",
                "ContainsRawProviderPayload",
                "ContainsSecrets",
                "IntakeStatus"
            ],
            Enum.GetValues<OfflineQuoteFileIntakeStatus>(),
            SecretsAllowed: false,
            RawProviderPayloadAllowed: false);

    public static IReadOnlyList<string> CreateLogicalLocations()
        =>
        [
            "data/offline-quotes/polygon/incoming/",
            "data/offline-quotes/polygon/quarantine/",
            "data/offline-quotes/polygon/accepted/",
            "data/offline-quotes/polygon/sanitized/",
            "data/offline-quotes/polygon/processed/",
            "artifacts/readiness/execution-sim/"
        ];

    public static IReadOnlyList<string> CreateFileLevelValidationRules()
        =>
        [
            "file exists",
            "file format supported: JSON / NDJSON / CSV",
            "manifest exists",
            "provider is supported",
            "symbol is mapped",
            "direct cross execution is disabled unless future explicit gate allows it",
            "time range covers requested T-minus-13-to-close windows",
            "file hash computed",
            "duplicate hash handled deterministically",
            "no secrets detected",
            "no raw payload dump emitted into artifacts"
        ];

    public static IReadOnlyList<string> CreateRowLevelValidationRules()
        =>
        [
            "timestamp parseable",
            "bid finite positive",
            "ask finite positive",
            "ask greater than or equal to bid",
            "provider symbol present",
            "execution symbol supported",
            "rows sorted or sortable by timestamp",
            "duplicate timestamps handled deterministically",
            "invalid rows counted and rejected"
        ];

    public static IReadOnlyList<string> CreateQuoteWindowReadinessChecks()
        =>
        [
            "requested TargetCloseTimestampUtc can be covered",
            "KnownAtTimestampUtc can be derived or supplied",
            "WindowStartUtc equals close minus 13 minutes",
            "quote count sufficient",
            "quote count last minute sufficient",
            "max gap acceptable",
            "last quote age at close acceptable",
            "bid/ask availability ratio acceptable"
        ];

    public static IReadOnlyList<string> CreateCloseBenchmarkReadinessChecks()
        =>
        [
            "last valid bid before close exists",
            "last valid ask before close exists",
            "last valid mid before close exists",
            "close quote age acceptable",
            "close spread acceptable",
            "benchmark status Available/MissingBidAsk/NoQuoteNearClose/StaleAtClose/SpreadTooWide/InconclusiveSafe"
        ];

    public static IReadOnlyList<string> CreateFeedQualityReadinessChecks()
        =>
        [
            "QuoteCountTMinus13ToClose",
            "QuoteCountLastMinute",
            "MaxGapSeconds",
            "MedianGapSeconds",
            "P95GapSeconds",
            "LastQuoteAgeAtCloseSeconds",
            "MedianSpreadBps",
            "P95SpreadBps",
            "MaxSpreadBps",
            "BidAskAvailabilityRatio",
            "MidAvailabilityRatio",
            "BenchmarkAvailabilityRatio",
            "GapNearCloseFlag",
            "StaleNearCloseFlag",
            "SpreadWideNearCloseFlag",
            "FeedQualityScore",
            "FeedQualityBucket"
        ];

    public static IReadOnlyList<string> CreateOperatorWorkflowSteps()
        =>
        [
            "Obtain quote files outside this system",
            "Place files in data/offline-quotes/polygon/incoming/",
            "Include sanitized manifest metadata without secrets",
            "Run no-external validation/import readiness",
            "Inspect accepted, quarantined, rejected, and duplicate results",
            "Use a later explicit gate to import sanitized files and run backtests"
        ];

    public static QuoteFileManifest CreateValidManifest()
        => new(
            "quote-file-manifest-r006-eurusd-valid",
            OfflineQuoteFileProviderIdentity.PolygonOfflineFile,
            "ForexHistoricalBboQuotes",
            "C:EUR-USD",
            "EURUSD",
            "data/offline-quotes/polygon/incoming/eurusd-20260520.ndjson",
            "NDJSON",
            "sha256:valid-eurusd",
            FileSizeBytes: 512,
            RowCountDeclared: 4,
            new DateTimeOffset(2026, 05, 20, 14, 47, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 05, 20, 15, 00, 00, TimeSpan.Zero),
            CreatedAt,
            "operator-sanitized",
            ContainsRawProviderPayload: false,
            ContainsSecrets: false,
            OfflineQuoteFileIntakeStatus.IntakeReady);

    public static OfflineQuoteFileIntakeValidationResult ValidateManifest(
        QuoteFileManifest manifest,
        bool fileExists,
        IReadOnlySet<string> duplicateHashes)
    {
        if (!fileExists)
        {
            return Quarantined(OfflineQuoteFileIntakeStatus.QuarantinedMalformedFile, PolygonOfflineImportFailureCategory.MissingFile, fileExists, duplicate: false);
        }

        if (manifest.ContainsSecrets)
        {
            return Quarantined(OfflineQuoteFileIntakeStatus.QuarantinedSecretLeakRisk, PolygonOfflineImportFailureCategory.SecretLeakRisk, fileExists, duplicate: false);
        }

        if (manifest.ContainsRawProviderPayload)
        {
            return Quarantined(OfflineQuoteFileIntakeStatus.QuarantinedRawPayloadLeakRisk, PolygonOfflineImportFailureCategory.RawPayloadLeakRisk, fileExists, duplicate: false);
        }

        if (duplicateHashes.Contains(manifest.FileHash))
        {
            return new(
                OfflineQuoteFileIntakeStatus.DuplicateReturned,
                PolygonOfflineImportFailureCategory.DuplicateRows,
                fileExists,
                FileHashComputed: true,
                DuplicateHashHandledDeterministically: true,
                AcceptedForSanitizedImport: false,
                Quarantined: false,
                PolygonApiCalled: false,
                LmaxCalled: false,
                ExternalApiCalled: false,
                OrdersCreated: false,
                FillsCreated: false,
                ExecutionReportsCreated: false,
                RoutesCreated: false,
                SubmissionsCreated: false,
                RawPayloadSerialized: false,
                SecretsSerialized: false);
        }

        var symbol = NormalizeSymbol(manifest.ProviderSymbol);
        if (ExecutionSimR004PolygonOfflineImportFixtures.CreateDirectCrossSymbols().Contains(symbol, StringComparer.OrdinalIgnoreCase))
        {
            return Quarantined(OfflineQuoteFileIntakeStatus.QuarantinedDirectCrossExecutionDisabled, PolygonOfflineImportFailureCategory.DirectCrossExecutionDisabled, fileExists, duplicate: false);
        }

        if (!ExecutionSimR004PolygonOfflineImportFixtures.CreateSymbolMapping().ContainsKey(symbol))
        {
            return Quarantined(OfflineQuoteFileIntakeStatus.QuarantinedUnsupportedSymbol, PolygonOfflineImportFailureCategory.UnsupportedSymbol, fileExists, duplicate: false);
        }

        return new(
            OfflineQuoteFileIntakeStatus.AcceptedForSanitizedImport,
            null,
            fileExists,
            FileHashComputed: true,
            DuplicateHashHandledDeterministically: false,
            AcceptedForSanitizedImport: true,
            Quarantined: false,
            PolygonApiCalled: false,
            LmaxCalled: false,
            ExternalApiCalled: false,
            OrdersCreated: false,
            FillsCreated: false,
            ExecutionReportsCreated: false,
            RoutesCreated: false,
            SubmissionsCreated: false,
            RawPayloadSerialized: false,
            SecretsSerialized: false);
    }

    private static OfflineQuoteFileIntakeValidationResult Quarantined(
        OfflineQuoteFileIntakeStatus status,
        PolygonOfflineImportFailureCategory category,
        bool fileExists,
        bool duplicate)
        => new(
            status,
            category,
            fileExists,
            FileHashComputed: fileExists,
            DuplicateHashHandledDeterministically: duplicate,
            AcceptedForSanitizedImport: false,
            Quarantined: true,
            PolygonApiCalled: false,
            LmaxCalled: false,
            ExternalApiCalled: false,
            OrdersCreated: false,
            FillsCreated: false,
            ExecutionReportsCreated: false,
            RoutesCreated: false,
            SubmissionsCreated: false,
            RawPayloadSerialized: false,
            SecretsSerialized: false);

    private static string NormalizeSymbol(string symbol)
        => symbol
            .Replace("C:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("/", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();
}

public sealed record OperatorQuoteValidationFixtureContract(
    OfflineQuoteFileIntakeContract IntakeContract,
    QuoteFileManifestContract ManifestContract,
    IReadOnlyList<string> ConcreteFixtureFormats,
    IReadOnlyList<string> ContractOnlyFixtureFormats,
    bool LocalFixtureFilesOnly,
    bool ValidatesAsOperatorProvidedOfflineFiles,
    bool RunsImportBacktest,
    bool PolygonApiCalled,
    bool LmaxCalled,
    bool ExternalApiCalled);

public sealed record LocalQuoteValidationFixtureFile(
    string FixtureFileId,
    string SafeFixturePathCategory,
    string FileFormat,
    string ProviderSymbol,
    string? ExecutionTradableSymbol,
    string ExpectedIntakeStatus,
    string ExpectedReasonCategory,
    bool ContainsRawProviderPayload,
    bool ContainsSecrets);

public sealed record OperatorQuoteFileValidationRun(
    string FileValidationRunId,
    OfflineQuoteFileProviderIdentity ProviderName,
    string SafeFixturePathCategory,
    string FileFormat,
    string FileHash,
    string ProviderSymbol,
    string? ExecutionTradableSymbol,
    OfflineQuoteFileIntakeStatus IntakeStatus,
    int AcceptedRowCount,
    int RejectedRowCount,
    PolygonOfflineImportFailureCategory? QuarantineReason,
    bool SanitizedImportReady,
    HistoricalQuoteReadinessStatus QuoteWindowReadinessStatus,
    HistoricalCloseBenchmarkStatus CloseBenchmarkReadinessStatus,
    HistoricalFeedQualityBucket FeedQualityReadinessStatus,
    bool RawPayloadSerialized,
    bool SecretMaterialDetected,
    bool SecretMaterialSerialized,
    bool PolygonApiCalled,
    bool LmaxCalled,
    bool ExternalApiCalled,
    bool OrdersCreated,
    bool FillsCreated,
    bool ExecutionReportsCreated,
    bool RoutesCreated,
    bool SubmissionsCreated);

public sealed record SanitizedImportReadinessOutput(
    string FileValidationRunId,
    string ProviderSymbol,
    string ExecutionTradableSymbol,
    string NormalizedPortfolioSymbol,
    bool RequiresInversion,
    int AcceptedRowCount,
    PolygonQuoteWindowFixture QuoteWindow,
    PolygonCloseBenchmarkFromImportedQuotes CloseBenchmark,
    PolygonImportedFeedQualityScore FeedQualityScore,
    bool SanitizedImportReady,
    bool FixtureOnly,
    bool RawPayloadSerialized,
    bool SecretMaterialSerialized);

public sealed record OperatorQuoteFileValidationSummary(
    string SummaryId,
    int AcceptedFileCount,
    int QuarantinedFileCount,
    int DuplicateFileCount,
    IReadOnlyList<string> OperatorReviewSteps,
    bool ExternalApiCalled,
    bool OrdersFillsReportsRoutesSubmissionsCreated);

public sealed record OperatorQuoteFileValidationFixturePackage(
    OperatorQuoteValidationFixtureContract ValidationFixtureContract,
    IReadOnlyList<LocalQuoteValidationFixtureFile> LocalFixtureFiles,
    IReadOnlyList<QuoteFileManifest> AcceptedFileManifests,
    IReadOnlyList<QuoteFileManifest> QuarantinedFileManifests,
    IReadOnlyList<SanitizedImportReadinessOutput> SanitizedImportReadinessOutputs,
    IReadOnlyList<OperatorQuoteFileValidationRun> ValidationRuns,
    IReadOnlyList<OperatorQuoteFileValidationRun> QuoteWindowReadinessResults,
    IReadOnlyList<OperatorQuoteFileValidationRun> CloseBenchmarkReadinessResults,
    IReadOnlyList<OperatorQuoteFileValidationRun> FeedQualityReadinessResults,
    OperatorQuoteFileValidationSummary OperatorValidationSummary,
    bool PolygonApiCalled,
    bool LmaxCalled,
    bool ExternalApiCalled,
    bool BrokerMarketDataRuntimeActionDetected,
    bool OrdersCreated,
    bool FillsCreated,
    bool ExecutionReportsCreated,
    bool RoutesCreated,
    bool SubmissionsCreated,
    bool RawPayloadSerialized,
    bool SecretMaterialSerialized);

public static class ExecutionSimR007OperatorQuoteFileValidationFixtures
{
    private static readonly DateTimeOffset TargetClose = new(2026, 05, 20, 15, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset KnownAt = TargetClose.AddMinutes(-13);
    private static readonly DateTimeOffset CreatedAt = new(2026, 05, 20, 16, 00, 00, TimeSpan.Zero);

    public static OperatorQuoteFileValidationFixturePackage CreatePackage()
    {
        var validationRuns = CreateValidationRuns();
        var accepted = validationRuns.Where(x => x.IntakeStatus == OfflineQuoteFileIntakeStatus.AcceptedForSanitizedImport).ToArray();
        var quarantined = validationRuns.Where(x => x.Quarantined()).ToArray();

        return new(
            CreateValidationFixtureContract(),
            CreateLocalFixtureFilesManifest(),
            accepted.Select(CreateAcceptedManifest).ToArray(),
            quarantined.Select(CreateQuarantinedManifest).ToArray(),
            CreateSanitizedImportReadinessOutputs(),
            validationRuns,
            accepted,
            accepted,
            accepted,
            new(
                "exec-sim-r007-operator-validation-summary",
                accepted.Length,
                quarantined.Length,
                validationRuns.Count(x => x.IntakeStatus == OfflineQuoteFileIntakeStatus.DuplicateReturned),
                [
                    "Review accepted manifests before later import",
                    "Review quarantined manifests and reason categories",
                    "Confirm sanitized import-readiness outputs contain no raw payloads or secrets",
                    "Use a later explicit gate for imported-quote backtest dry run"
                ],
                ExternalApiCalled: false,
                OrdersFillsReportsRoutesSubmissionsCreated: false),
            PolygonApiCalled: false,
            LmaxCalled: false,
            ExternalApiCalled: false,
            BrokerMarketDataRuntimeActionDetected: false,
            OrdersCreated: false,
            FillsCreated: false,
            ExecutionReportsCreated: false,
            RoutesCreated: false,
            SubmissionsCreated: false,
            RawPayloadSerialized: false,
            SecretMaterialSerialized: false);
    }

    public static OperatorQuoteValidationFixtureContract CreateValidationFixtureContract()
        => new(
            ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateIntakeContract(),
            ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateManifestContract(),
            ["NDJSON"],
            ["JSON", "CSV"],
            LocalFixtureFilesOnly: true,
            ValidatesAsOperatorProvidedOfflineFiles: true,
            RunsImportBacktest: false,
            PolygonApiCalled: false,
            LmaxCalled: false,
            ExternalApiCalled: false);

    public static IReadOnlyList<LocalQuoteValidationFixtureFile> CreateLocalFixtureFilesManifest()
        =>
        [
            FixtureFile("r007-valid-eurusd", "tests/fixtures/execution-sim/r007/operator-provided/valid-eurusd.ndjson", "C:EUR-USD", "EURUSD", OfflineQuoteFileIntakeStatus.AcceptedForSanitizedImport, "AcceptedForSanitizedImport"),
            FixtureFile("r007-valid-usdjpy", "tests/fixtures/execution-sim/r007/operator-provided/valid-usdjpy.ndjson", "C:USD-JPY", "USDJPY", OfflineQuoteFileIntakeStatus.AcceptedForSanitizedImport, "AcceptedForSanitizedImport"),
            FixtureFile("r007-valid-audusd", "tests/fixtures/execution-sim/r007/operator-provided/valid-audusd.ndjson", "C:AUD-USD", "AUDUSD", OfflineQuoteFileIntakeStatus.AcceptedForSanitizedImport, "AcceptedForSanitizedImport"),
            FixtureFile("r007-direct-cross-eurgbp", "tests/fixtures/execution-sim/r007/operator-provided/direct-cross-eurgbp.ndjson", "C:EUR-GBP", null, OfflineQuoteFileIntakeStatus.QuarantinedDirectCrossExecutionDisabled, "DirectCrossExecutionDisabled"),
            FixtureFile("r007-missing-convention-sgd", "tests/fixtures/execution-sim/r007/operator-provided/missing-convention-sgd.ndjson", "C:SGD-USD", "SGDUSD", OfflineQuoteFileIntakeStatus.QuarantinedUnsupportedSymbol, "MissingInstrumentConvention"),
            FixtureFile("r007-malformed-file", "tests/fixtures/execution-sim/r007/operator-provided/malformed-file.ndjson", "C:EUR-USD", "EURUSD", OfflineQuoteFileIntakeStatus.QuarantinedMalformedFile, "InconclusiveSafe"),
            FixtureFile("r007-missing-timestamp", "tests/fixtures/execution-sim/r007/operator-provided/missing-timestamp.ndjson", "C:EUR-USD", "EURUSD", OfflineQuoteFileIntakeStatus.QuarantinedMissingTimestamp, "MissingTimestamp"),
            FixtureFile("r007-missing-bidask", "tests/fixtures/execution-sim/r007/operator-provided/missing-bidask.ndjson", "C:EUR-USD", "EURUSD", OfflineQuoteFileIntakeStatus.QuarantinedMissingBidAsk, "MissingBid"),
            FixtureFile("r007-invalid-bidask", "tests/fixtures/execution-sim/r007/operator-provided/invalid-bidask.ndjson", "C:EUR-USD", "EURUSD", OfflineQuoteFileIntakeStatus.QuarantinedInvalidBidAsk, "InvalidBidAsk"),
            FixtureFile("r007-duplicate-hash", "tests/fixtures/execution-sim/r007/operator-provided/duplicate-valid-eurusd.ndjson", "C:EUR-USD", "EURUSD", OfflineQuoteFileIntakeStatus.DuplicateReturned, "DuplicateRows"),
            FixtureFile("r007-secret-risk", "tests/fixtures/execution-sim/r007/operator-provided/secret-risk.ndjson", "C:EUR-USD", "EURUSD", OfflineQuoteFileIntakeStatus.QuarantinedSecretLeakRisk, "SecretLeakRisk", containsSecrets: true),
            FixtureFile("r007-raw-payload-risk", "tests/fixtures/execution-sim/r007/operator-provided/raw-payload-risk.ndjson", "C:EUR-USD", "EURUSD", OfflineQuoteFileIntakeStatus.QuarantinedRawPayloadLeakRisk, "RawPayloadLeakRisk", containsRawProviderPayload: true)
        ];

    public static IReadOnlyList<OperatorQuoteFileValidationRun> CreateValidationRuns()
    {
        var accepted = new[]
        {
            AcceptedRun("r007-valid-eurusd", "C:EUR-USD", "EURUSD", "sha256:r007-valid-eurusd", ExecutionSimR004PolygonOfflineImportFixtures.CreateValidEurusdFixture()),
            AcceptedRun("r007-valid-usdjpy", "C:USD-JPY", "USDJPY", "sha256:r007-valid-usdjpy", ExecutionSimR004PolygonOfflineImportFixtures.CreateValidUsdjpyFixture()),
            AcceptedRun("r007-valid-audusd", "C:AUD-USD", "AUDUSD", "sha256:r007-valid-audusd", CreateValidAudusdFixture())
        };

        return
        [
            ..accepted,
            QuarantinedRun("r007-direct-cross-eurgbp", "C:EUR-GBP", null, "sha256:r007-direct-cross-eurgbp", OfflineQuoteFileIntakeStatus.QuarantinedDirectCrossExecutionDisabled, PolygonOfflineImportFailureCategory.DirectCrossExecutionDisabled),
            QuarantinedRun("r007-missing-convention-sgd", "C:SGD-USD", "SGDUSD", "sha256:r007-missing-convention-sgd", OfflineQuoteFileIntakeStatus.QuarantinedUnsupportedSymbol, PolygonOfflineImportFailureCategory.MissingInstrumentConvention),
            QuarantinedRun("r007-malformed-file", "C:EUR-USD", "EURUSD", "sha256:r007-malformed-file", OfflineQuoteFileIntakeStatus.QuarantinedMalformedFile, PolygonOfflineImportFailureCategory.InconclusiveSafe),
            RejectedRowRun("r007-missing-timestamp", "C:EUR-USD", "EURUSD", "sha256:r007-missing-timestamp", OfflineQuoteFileIntakeStatus.QuarantinedMissingTimestamp, PolygonOfflineImportFailureCategory.MissingTimestamp, [Record("C:EUR-USD", null, 1.08000m, 1.08010m, 1, "r007-missing-timestamp")]),
            RejectedRowRun("r007-missing-bidask", "C:EUR-USD", "EURUSD", "sha256:r007-missing-bidask", OfflineQuoteFileIntakeStatus.QuarantinedMissingBidAsk, PolygonOfflineImportFailureCategory.MissingBid, [Record("C:EUR-USD", TargetClose.AddSeconds(-5), null, 1.08010m, 1, "r007-missing-bidask")]),
            RejectedRowRun("r007-invalid-bidask", "C:EUR-USD", "EURUSD", "sha256:r007-invalid-bidask", OfflineQuoteFileIntakeStatus.QuarantinedInvalidBidAsk, PolygonOfflineImportFailureCategory.InvalidBidAsk, [Record("C:EUR-USD", TargetClose.AddSeconds(-5), 1.08020m, 1.08010m, 1, "r007-invalid-bidask")]),
            DuplicateRun(),
            QuarantinedRun("r007-secret-risk", "C:EUR-USD", "EURUSD", "sha256:r007-secret-risk", OfflineQuoteFileIntakeStatus.QuarantinedSecretLeakRisk, PolygonOfflineImportFailureCategory.SecretLeakRisk, secretDetected: true),
            QuarantinedRun("r007-raw-payload-risk", "C:EUR-USD", "EURUSD", "sha256:r007-raw-payload-risk", OfflineQuoteFileIntakeStatus.QuarantinedRawPayloadLeakRisk, PolygonOfflineImportFailureCategory.RawPayloadLeakRisk)
        ];
    }

    public static IReadOnlyList<SanitizedImportReadinessOutput> CreateSanitizedImportReadinessOutputs()
        => CreateValidationRuns()
            .Where(x => x.SanitizedImportReady)
            .Select(x =>
            {
                var rows = x.ProviderSymbol switch
                {
                    "C:USD-JPY" => ExecutionSimR004PolygonOfflineImportFixtures.Import(ExecutionSimR004PolygonOfflineImportFixtures.CreateValidUsdjpyFixture()).AcceptedRows,
                    "C:AUD-USD" => ExecutionSimR004PolygonOfflineImportFixtures.Import(CreateValidAudusdFixture()).AcceptedRows,
                    _ => ExecutionSimR004PolygonOfflineImportFixtures.Import(ExecutionSimR004PolygonOfflineImportFixtures.CreateValidEurusdFixture()).AcceptedRows
                };
                var window = ExecutionSimR004PolygonOfflineImportFixtures.ExtractWindow(rows, x.ExecutionTradableSymbol!, TargetClose, KnownAt);
                var benchmark = ExecutionSimR004PolygonOfflineImportFixtures.CreateCloseBenchmark(window);
                var feed = ExecutionSimR004PolygonOfflineImportFixtures.ScoreFeedQuality(window);
                var first = rows.First();
                return new SanitizedImportReadinessOutput(
                    x.FileValidationRunId,
                    x.ProviderSymbol,
                    x.ExecutionTradableSymbol!,
                    first.NormalizedPortfolioSymbol,
                    first.RequiresInversion,
                    x.AcceptedRowCount,
                    window,
                    benchmark,
                    feed,
                    SanitizedImportReady: true,
                    FixtureOnly: true,
                    RawPayloadSerialized: false,
                    SecretMaterialSerialized: false);
            })
            .ToArray();

    private static LocalQuoteValidationFixtureFile FixtureFile(
        string id,
        string path,
        string providerSymbol,
        string? executionSymbol,
        OfflineQuoteFileIntakeStatus expectedStatus,
        string reason,
        bool containsRawProviderPayload = false,
        bool containsSecrets = false)
        => new(id, path, "NDJSON", providerSymbol, executionSymbol, expectedStatus.ToString(), reason, containsRawProviderPayload, containsSecrets);

    private static OperatorQuoteFileValidationRun AcceptedRun(
        string id,
        string providerSymbol,
        string executionSymbol,
        string hash,
        IReadOnlyList<PolygonOfflineQuoteRecord> records)
    {
        var import = ExecutionSimR004PolygonOfflineImportFixtures.Import(records);
        var window = ExecutionSimR004PolygonOfflineImportFixtures.ExtractWindow(import.AcceptedRows, executionSymbol, TargetClose, KnownAt);
        var benchmark = ExecutionSimR004PolygonOfflineImportFixtures.CreateCloseBenchmark(window);
        var feed = ExecutionSimR004PolygonOfflineImportFixtures.ScoreFeedQuality(window);

        return BaseRun(
            id,
            providerSymbol,
            executionSymbol,
            hash,
            OfflineQuoteFileIntakeStatus.AcceptedForSanitizedImport,
            import.AcceptedRowCount,
            import.RejectedRowCount,
            null,
            SanitizedImportReady: true,
            window.FeedWindowStatus,
            benchmark.CloseBenchmarkStatus,
            feed.FeedQualityBucket,
            secretDetected: false);
    }

    private static OperatorQuoteFileValidationRun RejectedRowRun(
        string id,
        string providerSymbol,
        string executionSymbol,
        string hash,
        OfflineQuoteFileIntakeStatus status,
        PolygonOfflineImportFailureCategory reason,
        IReadOnlyList<PolygonOfflineQuoteRecord> records)
    {
        var import = ExecutionSimR004PolygonOfflineImportFixtures.Import(records);

        return BaseRun(
            id,
            providerSymbol,
            executionSymbol,
            hash,
            status,
            import.AcceptedRowCount,
            import.RejectedRowCount,
            reason,
            SanitizedImportReady: false,
            HistoricalQuoteReadinessStatus.InconclusiveSafe,
            HistoricalCloseBenchmarkStatus.InconclusiveSafe,
            HistoricalFeedQualityBucket.InconclusiveSafe,
            secretDetected: false);
    }

    private static OperatorQuoteFileValidationRun QuarantinedRun(
        string id,
        string providerSymbol,
        string? executionSymbol,
        string hash,
        OfflineQuoteFileIntakeStatus status,
        PolygonOfflineImportFailureCategory reason,
        bool secretDetected = false)
        => BaseRun(
            id,
            providerSymbol,
            executionSymbol,
            hash,
            status,
            acceptedRows: 0,
            rejectedRows: 1,
            reason,
            SanitizedImportReady: false,
            HistoricalQuoteReadinessStatus.InconclusiveSafe,
            HistoricalCloseBenchmarkStatus.InconclusiveSafe,
            HistoricalFeedQualityBucket.InconclusiveSafe,
            secretDetected);

    private static OperatorQuoteFileValidationRun DuplicateRun()
        => BaseRun(
            "r007-duplicate-hash",
            "C:EUR-USD",
            "EURUSD",
            "sha256:r007-valid-eurusd",
            OfflineQuoteFileIntakeStatus.DuplicateReturned,
            acceptedRows: 0,
            rejectedRows: 0,
            PolygonOfflineImportFailureCategory.DuplicateRows,
            SanitizedImportReady: false,
            HistoricalQuoteReadinessStatus.InconclusiveSafe,
            HistoricalCloseBenchmarkStatus.InconclusiveSafe,
            HistoricalFeedQualityBucket.InconclusiveSafe,
            secretDetected: false);

    private static OperatorQuoteFileValidationRun BaseRun(
        string id,
        string providerSymbol,
        string? executionSymbol,
        string hash,
        OfflineQuoteFileIntakeStatus status,
        int acceptedRows,
        int rejectedRows,
        PolygonOfflineImportFailureCategory? reason,
        bool SanitizedImportReady,
        HistoricalQuoteReadinessStatus windowStatus,
        HistoricalCloseBenchmarkStatus benchmarkStatus,
        HistoricalFeedQualityBucket feedStatus,
        bool secretDetected)
        => new(
            $"exec-sim-r007-{id}",
            OfflineQuoteFileProviderIdentity.PolygonOfflineFile,
            $"tests/fixtures/execution-sim/r007/operator-provided/{id}.ndjson",
            "NDJSON",
            hash,
            providerSymbol,
            executionSymbol,
            status,
            acceptedRows,
            rejectedRows,
            reason,
            SanitizedImportReady,
            windowStatus,
            benchmarkStatus,
            feedStatus,
            RawPayloadSerialized: false,
            SecretMaterialDetected: secretDetected,
            SecretMaterialSerialized: false,
            PolygonApiCalled: false,
            LmaxCalled: false,
            ExternalApiCalled: false,
            OrdersCreated: false,
            FillsCreated: false,
            ExecutionReportsCreated: false,
            RoutesCreated: false,
            SubmissionsCreated: false);

    private static QuoteFileManifest CreateAcceptedManifest(OperatorQuoteFileValidationRun run)
        => CreateManifest(run) with { IntakeStatus = OfflineQuoteFileIntakeStatus.AcceptedForSanitizedImport };

    private static QuoteFileManifest CreateQuarantinedManifest(OperatorQuoteFileValidationRun run)
        => CreateManifest(run) with { IntakeStatus = run.IntakeStatus };

    private static QuoteFileManifest CreateManifest(OperatorQuoteFileValidationRun run)
        => new(
            $"{run.FileValidationRunId}:manifest",
            run.ProviderName,
            "ForexHistoricalBboQuotes",
            run.ProviderSymbol,
            run.ExecutionTradableSymbol,
            run.SafeFixturePathCategory,
            run.FileFormat,
            run.FileHash,
            FileSizeBytes: 256,
            RowCountDeclared: run.AcceptedRowCount + run.RejectedRowCount,
            KnownAt,
            TargetClose,
            CreatedAt,
            "operator-sanitized",
            ContainsRawProviderPayload: run.IntakeStatus == OfflineQuoteFileIntakeStatus.QuarantinedRawPayloadLeakRisk,
            ContainsSecrets: run.SecretMaterialDetected,
            run.IntakeStatus);

    private static IReadOnlyList<PolygonOfflineQuoteRecord> CreateValidAudusdFixture()
        =>
        [
            Record("C:AUD-USD", TargetClose.AddMinutes(-13), 0.66300m, 0.66308m, 1, "r007-valid-audusd"),
            Record("C:AUD-USD", TargetClose.AddMinutes(-4), 0.66310m, 0.66318m, 2, "r007-valid-audusd"),
            Record("C:AUD-USD", TargetClose.AddSeconds(-8), 0.66314m, 0.66322m, 3, "r007-valid-audusd")
        ];

    private static PolygonOfflineQuoteRecord Record(string symbol, DateTimeOffset? timestamp, decimal? bid, decimal? ask, int row, string sourceFileId)
        => new("Polygon", symbol, null, timestamp, bid, ask, "fixture-venue", 1000000m, 1000000m, $"seq-{row}", sourceFileId, row);

    private static bool Quarantined(this OperatorQuoteFileValidationRun run)
        => run.IntakeStatus is OfflineQuoteFileIntakeStatus.QuarantinedMalformedFile
            or OfflineQuoteFileIntakeStatus.QuarantinedUnsupportedSymbol
            or OfflineQuoteFileIntakeStatus.QuarantinedDirectCrossExecutionDisabled
            or OfflineQuoteFileIntakeStatus.QuarantinedMissingTimestamp
            or OfflineQuoteFileIntakeStatus.QuarantinedMissingBidAsk
            or OfflineQuoteFileIntakeStatus.QuarantinedInvalidBidAsk
            or OfflineQuoteFileIntakeStatus.QuarantinedSecretLeakRisk
            or OfflineQuoteFileIntakeStatus.QuarantinedRawPayloadLeakRisk;
}

public sealed record OperatorQuoteBacktestDryRunContract(
    bool ReusesR007AcceptedManifests,
    bool ReusesR007QuarantinedManifests,
    bool ReusesR004SanitizedImportPath,
    bool ReusesR004QuoteWindowExtraction,
    bool ReusesR004CloseBenchmarkConstruction,
    bool ReusesR004FeedQualityScoring,
    bool ReusesR005ImportedQuoteTcaBacktestFlow,
    IReadOnlyList<ExecutionSimPolicy> ComparedPolicies,
    bool AcceptedFilesOnly,
    bool QuarantinedFilesExcluded,
    bool FixtureOnly,
    bool PaperOnly,
    bool PolygonApiCalled,
    bool LmaxCalled,
    bool ExternalApiCalled);

public sealed record OperatorQuoteBacktestDryRunResult(
    string BacktestDryRunId,
    IReadOnlyList<string> AcceptedManifestIds,
    IReadOnlyList<string> ExcludedQuarantinedManifestIds,
    IReadOnlyList<string> ImportedQuoteFixtureIds,
    IReadOnlyList<string> QuoteWindowIds,
    IReadOnlyList<string> CloseBenchmarkIds,
    IReadOnlyList<string> FeedQualityResultIds,
    IReadOnlyList<string> PolicyResultIds,
    IReadOnlyList<string> TcaReportIds,
    string SimulationStatus,
    string SafetyStatus,
    bool QuarantinedFilesFeedBacktest,
    bool PolygonApiCalled,
    bool LmaxCalled,
    bool ExternalApiCalled);

public sealed record OperatorQuotePerInstrumentDryRunReport(
    string ExecutionTradableSymbol,
    string NormalizedPortfolioSymbol,
    bool RequiresInversion,
    HistoricalQuoteReadinessStatus QuoteWindowStatus,
    HistoricalCloseBenchmarkStatus CloseBenchmarkStatus,
    HistoricalFeedQualityBucket FeedQualityBucket,
    string PolicyComparisonSummary,
    ExecutionSimPolicy BestPolicyByMedianSlippage,
    ExecutionSimPolicy BestPolicyByResidual,
    ExecutionSimPolicy WorstPolicyBySpreadPaid,
    string WakettLimitResidualSummary,
    string WakettFiveMarketSpreadPaidSummary,
    string CloseSeekingAdaptiveSummary,
    string ControlledResidualCrossSummary);

public sealed record OperatorQuoteBacktestDryRunPackage(
    OperatorQuoteBacktestDryRunContract Contract,
    OperatorQuoteBacktestDryRunResult Result,
    IReadOnlyList<QuoteFileManifest> AcceptedManifestsUsed,
    IReadOnlyList<QuoteFileManifest> QuarantinedManifestsExcluded,
    IReadOnlyList<SanitizedImportReadinessOutput> ImportedQuoteFixturesUsed,
    IReadOnlyList<PolygonQuoteWindowFixture> QuoteWindowsCreated,
    IReadOnlyList<PolygonCloseBenchmarkFromImportedQuotes> CloseBenchmarksCreated,
    IReadOnlyList<PolygonImportedFeedQualityScore> FeedQualityResults,
    IReadOnlyList<ImportedQuoteFixtureSimulationLine> PolicyResults,
    IReadOnlyList<ImportedQuoteFixtureSimulationLine> TcaReports,
    IReadOnlyList<OperatorQuotePerInstrumentDryRunReport> PerInstrumentReports,
    IReadOnlyList<ExecutionSimPolicyRanking> MedianSlippageRanking,
    IReadOnlyList<ExecutionSimPolicyRanking> P95SlippageRanking,
    IReadOnlyList<ExecutionSimPolicyRanking> FillRatioRanking,
    IReadOnlyList<ExecutionSimPolicyRanking> ResidualRanking,
    IReadOnlyList<ExecutionSimPolicyRanking> SpreadPaidRanking,
    bool DirectCrossExcluded,
    bool MissingConventionExcluded,
    bool SecretRiskExcluded,
    bool RawPayloadRiskExcluded,
    bool FiveUsdPerMillionBestCaseOnly,
    bool FiveUsdPerMillionUniversalized,
    bool NonMajorCalibrationPreserved,
    bool FixtureOnly,
    bool PaperOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute,
    bool NoRealFill,
    bool NoExecutionReport,
    bool PolygonApiCalled,
    bool LmaxCalled,
    bool ExternalApiCalled,
    bool BrokerMarketDataRuntimeActionDetected,
    bool OrdersCreated,
    bool FillsCreated,
    bool ExecutionReportsCreated,
    bool RoutesCreated,
    bool SubmissionsCreated);

public static class ExecutionSimR008OperatorQuoteFileBacktestDryRun
{
    public static OperatorQuoteBacktestDryRunPackage CreatePackage()
    {
        var validation = ExecutionSimR007OperatorQuoteFileValidationFixtures.CreatePackage();
        var imported = validation.SanitizedImportReadinessOutputs.ToArray();
        var windows = imported.Select(x => x.QuoteWindow).ToArray();
        var benchmarks = imported.Select(x => x.CloseBenchmark).ToArray();
        var feeds = imported.Select(x => x.FeedQualityScore).ToArray();
        var policyResults = imported
            .SelectMany(x => ExecutionSimR005ImportedQuoteFixtureBacktest.SimulateWindow(x.QuoteWindow, x.CloseBenchmark, x.FeedQualityScore))
            .ToArray();
        var perInstrument = imported
            .Select(x => CreatePerInstrumentReport(x, policyResults.Where(line => line.ExecutionTradableSymbol == x.ExecutionTradableSymbol).ToArray()))
            .ToArray();

        return new(
            CreateContract(),
            new(
                "exec-sim-r008-operator-quote-file-backtest-dry-run",
                validation.AcceptedFileManifests.Select(x => x.QuoteFileManifestId).ToArray(),
                validation.QuarantinedFileManifests.Select(x => x.QuoteFileManifestId).ToArray(),
                imported.Select(x => x.FileValidationRunId).ToArray(),
                windows.Select(x => $"{x.ExecutionTradableSymbol}:quote-window").ToArray(),
                benchmarks.Select(x => $"{x.ExecutionTradableSymbol}:close-benchmark").ToArray(),
                feeds.Select((x, index) => $"{windows[index].ExecutionTradableSymbol}:feed-quality").ToArray(),
                policyResults.Select(x => x.ScenarioId).ToArray(),
                perInstrument.Select(x => $"{x.ExecutionTradableSymbol}:tca-report").ToArray(),
                "CompletedFixtureOnlyDryRun",
                "NoExternalNoRealFillNoOrder",
                QuarantinedFilesFeedBacktest: false,
                PolygonApiCalled: false,
                LmaxCalled: false,
                ExternalApiCalled: false),
            validation.AcceptedFileManifests,
            validation.QuarantinedFileManifests,
            imported,
            windows,
            benchmarks,
            feeds,
            policyResults,
            policyResults,
            perInstrument,
            Rank(policyResults, x => x.SlippageVsCloseBps, "MedianSlippageVsCloseBps", lowerIsBetter: true),
            Rank(policyResults, x => x.SlippageVsCloseBps * 1.35m, "P95SlippageVsCloseBps", lowerIsBetter: true),
            Rank(policyResults, x => x.FillRatio, "FillRatio", lowerIsBetter: false),
            Rank(policyResults, x => x.ResidualAtClose, "ResidualAtClose", lowerIsBetter: true),
            Rank(policyResults, x => x.SpreadPaidBps, "SpreadPaidBps", lowerIsBetter: true),
            DirectCrossExcluded: true,
            MissingConventionExcluded: true,
            SecretRiskExcluded: true,
            RawPayloadRiskExcluded: true,
            FiveUsdPerMillionBestCaseOnly: true,
            FiveUsdPerMillionUniversalized: false,
            NonMajorCalibrationPreserved: true,
            FixtureOnly: true,
            PaperOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoRealFill: true,
            NoExecutionReport: true,
            PolygonApiCalled: false,
            LmaxCalled: false,
            ExternalApiCalled: false,
            BrokerMarketDataRuntimeActionDetected: false,
            OrdersCreated: false,
            FillsCreated: false,
            ExecutionReportsCreated: false,
            RoutesCreated: false,
            SubmissionsCreated: false);
    }

    public static OperatorQuoteBacktestDryRunContract CreateContract()
        => new(
            ReusesR007AcceptedManifests: true,
            ReusesR007QuarantinedManifests: true,
            ReusesR004SanitizedImportPath: true,
            ReusesR004QuoteWindowExtraction: true,
            ReusesR004CloseBenchmarkConstruction: true,
            ReusesR004FeedQualityScoring: true,
            ReusesR005ImportedQuoteTcaBacktestFlow: true,
            [
                ExecutionSimPolicy.WakettPureLimitUntilClose,
                ExecutionSimPolicy.WakettFiveMarketSlicesAroundClose,
                ExecutionSimPolicy.PassiveUntilUrgency,
                ExecutionSimPolicy.CloseSeeking15m,
                ExecutionSimPolicy.CloseSeeking15mAdaptive,
                ExecutionSimPolicy.ControlledResidualCross,
                ExecutionSimPolicy.ImmediatePaperBenchmark,
                ExecutionSimPolicy.TWAPBenchmarkOnly,
                ExecutionSimPolicy.VWAPBenchmarkOnly,
                ExecutionSimPolicy.ManualReview,
                ExecutionSimPolicy.DoNotTrade
            ],
            AcceptedFilesOnly: true,
            QuarantinedFilesExcluded: true,
            FixtureOnly: true,
            PaperOnly: true,
            PolygonApiCalled: false,
            LmaxCalled: false,
            ExternalApiCalled: false);

    private static OperatorQuotePerInstrumentDryRunReport CreatePerInstrumentReport(
        SanitizedImportReadinessOutput output,
        IReadOnlyList<ImportedQuoteFixtureSimulationLine> lines)
    {
        var bestMedian = lines
            .Where(IsComparable)
            .OrderBy(x => x.SlippageVsCloseBps)
            .First();
        var bestResidual = lines
            .Where(IsComparable)
            .OrderBy(x => x.ResidualAtClose)
            .First();
        var worstSpread = lines
            .Where(IsComparable)
            .OrderByDescending(x => x.SpreadPaidBps)
            .First();
        var limit = lines.First(x => x.Policy == ExecutionSimPolicy.WakettPureLimitUntilClose);
        var five = lines.First(x => x.Policy == ExecutionSimPolicy.WakettFiveMarketSlicesAroundClose);
        var adaptive = lines.First(x => x.Policy == ExecutionSimPolicy.CloseSeeking15mAdaptive);
        var residual = lines.First(x => x.Policy == ExecutionSimPolicy.ControlledResidualCross);

        return new(
            output.ExecutionTradableSymbol,
            output.NormalizedPortfolioSymbol,
            output.RequiresInversion,
            output.QuoteWindow.FeedWindowStatus,
            output.CloseBenchmark.CloseBenchmarkStatus,
            output.FeedQualityScore.FeedQualityBucket,
            "Accepted sanitized quote fixture dry-run compares Wakett baselines against CloseSeeking15m variants.",
            bestMedian.Policy,
            bestResidual.Policy,
            worstSpread.Policy,
            $"Low spread paid but residual remains {limit.ResidualAtClose}.",
            $"Completion is high but spread paid is {five.SpreadPaidBps} bps.",
            $"Adaptive close seeking fill ratio {adaptive.FillRatio} with residual {adaptive.ResidualAtClose}.",
            $"Controlled residual cross only when opportunity cost {residual.EstimatedOpportunityCost} exceeds spread cost {residual.EstimatedSpreadCost}.");
    }

    private static IReadOnlyList<ExecutionSimPolicyRanking> Rank(
        IReadOnlyList<ImportedQuoteFixtureSimulationLine> lines,
        Func<ImportedQuoteFixtureSimulationLine, decimal> selector,
        string metric,
        bool lowerIsBetter)
        => lines
            .Where(IsComparable)
            .GroupBy(x => x.Policy)
            .Select(x => new { Policy = x.Key, Value = x.Average(selector) })
            .OrderBy(x => lowerIsBetter ? x.Value : -x.Value)
            .Select((x, index) => new ExecutionSimPolicyRanking(x.Policy, index + 1, x.Value, metric))
            .ToArray();

    private static bool IsComparable(ImportedQuoteFixtureSimulationLine line)
        => line.Policy is not ExecutionSimPolicy.ManualReview and not ExecutionSimPolicy.DoNotTrade;
}
