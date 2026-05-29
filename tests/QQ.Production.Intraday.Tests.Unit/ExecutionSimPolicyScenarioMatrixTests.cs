using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimPolicyScenarioMatrixTests
{
    [Fact]
    public void Scenario_matrix_contract_exists()
    {
        var report = ExecutionSimR002PolicyScenarioMatrix.CreateReport();

        Assert.True(report.ScenarioMatrixCreated);
        Assert.NotEmpty(report.Lines);
        Assert.True(report.FixtureOnly);
        Assert.True(report.PaperOnly);
    }

    [Fact]
    public void Scenario_matrix_includes_required_dimensions()
    {
        var lines = ExecutionSimR002PolicyScenarioMatrix.CreateScenarioMatrix();

        Assert.Contains(lines, x => x.LiquidityBucket == ExecutionSimInstrumentLiquidityBucket.MajorUsdPair);
        Assert.Contains(lines, x => x.LiquidityBucket == ExecutionSimInstrumentLiquidityBucket.NonMajorUsdPair);
        Assert.Contains(lines, x => x.LiquidityBucket == ExecutionSimInstrumentLiquidityBucket.EmCnhHighCalibration);
        Assert.Contains(lines, x => x.LiquidityBucket == ExecutionSimInstrumentLiquidityBucket.MissingConvention);
        Assert.Contains(lines, x => x.LiquidityBucket == ExecutionSimInstrumentLiquidityBucket.DirectCrossSignalOnly);
        Assert.Contains(lines, x => x.SpreadRegime == ExecutionSimSpreadRegime.TightSpread);
        Assert.Contains(lines, x => x.SpreadRegime == ExecutionSimSpreadRegime.NormalSpread);
        Assert.Contains(lines, x => x.SpreadRegime == ExecutionSimSpreadRegime.WideSpread);
        Assert.Contains(lines, x => x.SpreadRegime == ExecutionSimSpreadRegime.ExtremeSpread);
        Assert.Contains(lines, x => x.ResidualSize == ExecutionSimResidualSize.SmallResidual);
        Assert.Contains(lines, x => x.ResidualSize == ExecutionSimResidualSize.MediumResidual);
        Assert.Contains(lines, x => x.ResidualSize == ExecutionSimResidualSize.LargeResidual);
        Assert.Contains(lines, x => x.ResidualSize == ExecutionSimResidualSize.HighResidualNearClose);
        Assert.Contains(lines, x => x.DriftRegime == ExecutionSimDriftRegime.FavorableDrift);
        Assert.Contains(lines, x => x.DriftRegime == ExecutionSimDriftRegime.AdverseDrift);
        Assert.Contains(lines, x => x.DriftRegime == ExecutionSimDriftRegime.FastAdverseDrift);
        Assert.Contains(lines, x => x.FeedQualityRegime == ExecutionSimFeedQualityRegime.GoodFeed);
        Assert.Contains(lines, x => x.FeedQualityRegime == ExecutionSimFeedQualityRegime.MinorGap);
        Assert.Contains(lines, x => x.FeedQualityRegime == ExecutionSimFeedQualityRegime.MajorGap);
        Assert.Contains(lines, x => x.FeedQualityRegime == ExecutionSimFeedQualityRegime.NoQuoteNearClose);
        Assert.Contains(lines, x => x.FeedQualityRegime == ExecutionSimFeedQualityRegime.StaleQuoteNearClose);
        Assert.Contains(lines, x => x.TimeToClosePhase == ExecutionSimTimeToClosePhase.TMinus13ToTMinus5);
        Assert.Contains(lines, x => x.TimeToClosePhase == ExecutionSimTimeToClosePhase.TMinus5ToTMinus1);
        Assert.Contains(lines, x => x.TimeToClosePhase == ExecutionSimTimeToClosePhase.TMinus1ToClose);
    }

    [Fact]
    public void Direct_cross_signals_are_not_simulated_as_direct_execution_instruments()
    {
        var directCrosses = ExecutionSimR002PolicyScenarioMatrix.CreateScenarioMatrix().Where(x => x.RawDirectCrossSignalOnly).ToArray();

        Assert.NotEmpty(directCrosses);
        Assert.All(directCrosses, x =>
        {
            Assert.False(x.DirectCrossExecutionAllowed);
            Assert.Null(x.ExecutionTradableSymbol);
            Assert.Equal(AlgoPolicyReasonCategory.DirectCrossExecutionDisabled, x.BlockReason);
            Assert.True(x.NonExecutable);
        });
    }

    [Fact]
    public void Usd_pair_normalization_is_used_for_simulation_lines()
    {
        var lines = ExecutionSimR002PolicyScenarioMatrix.CreateScenarioMatrix().Where(x => !x.RawDirectCrossSignalOnly && x.LiquidityBucket != ExecutionSimInstrumentLiquidityBucket.MissingConvention).ToArray();

        Assert.Contains(lines, x => x.PortfolioCurrency == "EUR" && x.ExecutionTradableSymbol == "EURUSD" && !x.RequiresInversion);
        Assert.Contains(lines, x => x.PortfolioCurrency == "JPY" && x.ExecutionTradableSymbol == "USDJPY" && x.RequiresInversion);
        Assert.Contains(lines, x => x.PortfolioCurrency == "CAD" && x.ExecutionTradableSymbol == "USDCAD" && x.RequiresInversion);
        Assert.All(lines, x => Assert.False(string.IsNullOrWhiteSpace(x.ExecutionTradableSymbol)));
    }

    [Fact]
    public void Inverted_usd_pairs_preserve_side_transform()
    {
        var jpy = Scenario("jpy-inverted-adaptive");
        var cad = Scenario("cad-inverted-controlled");

        Assert.True(jpy.RequiresInversion);
        Assert.Equal(FxExecutionSide.Buy, jpy.PortfolioSide);
        Assert.Equal(FxExecutionSide.Sell, jpy.ExecutionSide);
        Assert.True(cad.RequiresInversion);
        Assert.Equal(FxExecutionSide.Sell, cad.PortfolioSide);
        Assert.Equal(FxExecutionSide.Buy, cad.ExecutionSide);
    }

    [Fact]
    public void Wakett_baselines_are_present_and_negative()
    {
        var limit = Scenario("wakett-limit-low-fill");
        var slices = Scenario("wakett-five-slices-normal");

        Assert.Equal(ExecutionSimPolicy.WakettPureLimitUntilClose, limit.Policy);
        Assert.Equal(SimulationOutcomeStatus.BlockedUnsafePattern, limit.SimulationOutcomeStatus);
        Assert.Equal(AlgoPolicyReasonCategory.WakettPatternBlocked, limit.BlockReason);
        Assert.True(limit.ResidualAtClose >= 0.80m);
        Assert.True(limit.EstimatedNonFillCost > 0m);
        Assert.Equal(ExecutionSimPolicy.WakettFiveMarketSlicesAroundClose, slices.Policy);
        Assert.Equal(SimulationOutcomeStatus.BlockedUnsafePattern, slices.SimulationOutcomeStatus);
        Assert.True(slices.SpreadPaidBps >= 5m);
        Assert.Equal(1m, slices.AggressiveFillRatio);
    }

    [Fact]
    public void Close_seeking_adaptive_and_controlled_residual_cross_have_tca_results()
    {
        var adaptive = Scenario("eur-normal-medium-adaptive");
        var controlled = Scenario("cad-inverted-controlled");

        Assert.Equal(ExecutionSimPolicy.CloseSeeking15mAdaptive, adaptive.Policy);
        Assert.True(adaptive.FillRatio > 0m);
        Assert.True(adaptive.SlippageVsCloseBps > 0m);
        Assert.Equal(ExecutionSimPolicy.ControlledResidualCross, controlled.Policy);
        Assert.Equal(AlgoPolicyReasonCategory.ReadyForControlledResidualCross, controlled.BlockReason);
        Assert.True(controlled.EstimatedOpportunityCost > controlled.EstimatedSpreadCost);
    }

    [Fact]
    public void Controlled_residual_cross_only_activates_when_opportunity_exceeds_crossing_cost()
    {
        var report = ExecutionSimR002PolicyScenarioMatrix.CreateReport();
        var controlledLines = report.Lines.Where(x => x.Policy == ExecutionSimPolicy.ControlledResidualCross).ToArray();

        Assert.NotEmpty(controlledLines);
        Assert.All(controlledLines.Where(x => x.SimulationOutcomeStatus == SimulationOutcomeStatus.CompletedFixtureOnly), x =>
            Assert.True(x.EstimatedOpportunityCost > x.EstimatedSpreadCost));
    }

    [Fact]
    public void Unsafe_feed_spread_and_staleness_scenarios_block_or_manual_review_execution()
    {
        var wide = Scenario("wide-spread-block");
        var noQuote = Scenario("zar-em-noquote");
        var stale = Scenario("stale-quote-block");

        Assert.Equal(FeedReadinessStatus.SpreadTooWide, wide.FeedReadinessStatus);
        Assert.Equal(SimulationOutcomeStatus.ManualReviewSafe, wide.SimulationOutcomeStatus);
        Assert.Equal(AlgoPolicyReasonCategory.SpreadTooWide, wide.BlockReason);
        Assert.Equal(FeedReadinessStatus.NoQuoteNearClose, noQuote.FeedReadinessStatus);
        Assert.Equal(AlgoPolicyReasonCategory.NoQuoteNearClose, noQuote.BlockReason);
        Assert.Equal(FeedReadinessStatus.StaleQuotes, stale.FeedReadinessStatus);
        Assert.Equal(AlgoPolicyReasonCategory.StaleQuoteNearClose, stale.BlockReason);
    }

    [Fact]
    public void Major_pair_5_usd_per_million_is_best_case_only_and_not_universalized()
    {
        var report = ExecutionSimR002PolicyScenarioMatrix.CreateReport();

        Assert.True(report.FiveUsdPerMillionBestCaseOnly);
        Assert.False(report.FiveUsdPerMillionUniversalized);
    }

    [Fact]
    public void Nonmajor_em_cnh_and_scandi_buckets_require_calibration_or_higher_cost()
    {
        var lines = ExecutionSimR002PolicyScenarioMatrix.CreateScenarioMatrix();

        Assert.Contains(lines, x => x.LiquidityBucket == ExecutionSimInstrumentLiquidityBucket.NonMajorUsdPair && x.CostBucketStatus == CostBucketStatus.RequiresLiquidityCalibration);
        Assert.Contains(lines, x => x.LiquidityBucket == ExecutionSimInstrumentLiquidityBucket.EmCnhHighCalibration && x.CostBucketStatus == CostBucketStatus.RequiresLiquidityCalibration);
        Assert.Contains(lines, x => x.PortfolioCurrency == "NOK");
        Assert.Contains(lines, x => x.PortfolioCurrency == "CNH");
    }

    [Fact]
    public void Aggregated_policy_comparison_report_and_rankings_are_produced()
    {
        var report = ExecutionSimR002PolicyScenarioMatrix.CreateReport();

        Assert.NotEmpty(report.MedianSlippageRanking);
        Assert.NotEmpty(report.P95SlippageRanking);
        Assert.NotEmpty(report.FillRatioRanking);
        Assert.NotEmpty(report.ResidualRanking);
        Assert.NotEmpty(report.SpreadPaidRanking);
        Assert.NotEmpty(report.WorstCases);
        Assert.Contains(report.MedianSlippageRanking, x => x.Policy == ExecutionSimPolicy.CloseSeeking15mAdaptive);
        Assert.Contains(report.P95SlippageRanking, x => x.Policy == ExecutionSimPolicy.WakettFiveMarketSlicesAroundClose);
    }

    [Fact]
    public void Worst_case_scenarios_by_policy_are_reported()
    {
        var report = ExecutionSimR002PolicyScenarioMatrix.CreateReport();

        Assert.Contains(report.WorstCases, x => x.Policy == ExecutionSimPolicy.WakettPureLimitUntilClose && x.ScenarioId == "wakett-limit-low-fill");
        Assert.Contains(report.WorstCases, x => x.Policy == ExecutionSimPolicy.WakettFiveMarketSlicesAroundClose && x.ScenarioId == "wakett-five-slices-normal");
    }

    [Fact]
    public void All_simulation_outputs_are_fixture_only_paper_only_not_fills_reports_orders_routes_or_submissions()
    {
        var report = ExecutionSimR002PolicyScenarioMatrix.CreateReport();

        Assert.True(report.NoOrdersCreated);
        Assert.True(report.NoRealFillsCreated);
        Assert.True(report.NoExecutionReportsCreated);
        Assert.True(report.NoRoutesCreated);
        Assert.True(report.NoSubmissionsCreated);
        Assert.All(report.Lines, x =>
        {
            Assert.True(x.FixtureOnly);
            Assert.True(x.PaperOnly);
            Assert.True(x.NonExecutable);
            Assert.True(x.NotAnOrder);
            Assert.True(x.NotSubmitted);
            Assert.True(x.NoBrokerRoute);
            Assert.True(x.NoRealFill);
            Assert.True(x.NoExecutionReport);
        });
    }

    [Fact]
    public void Source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(SourcePath());

        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MarketDataRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MarketDataResponse", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FixSession", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_and_worker_live_gateway_remain_disabled()
    {
        var apiSettings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Api/appsettings.json"))).RootElement;
        var workerSettings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Worker/appsettings.json"))).RootElement;

        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(apiSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(workerSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
    }

    [Fact]
    public void Source_introduces_no_scheduler_timer_polling_service_or_background_job()
    {
        var source = File.ReadAllText(SourcePath());

        Assert.DoesNotContain("IHostedService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BackgroundService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PeriodicTimer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Threading.Timer", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Audusd_is_not_misclassified_as_failed()
    {
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.Equal(ApprovedInstrumentValidationStatus.PausedTlsBoundaryInconclusiveNotFailed, audusd.ValidationStatus);
    }

    [Fact]
    public void Usdjpy_caveat_remains_preserved()
    {
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var usdjpy = universe.Single(x => x.InternalInstrumentKey == "USDJPY");

        Assert.Equal("4004", usdjpy.SecurityId);
        Assert.Equal("8", usdjpy.SecurityIdSource);
        Assert.Equal(ApprovedInstrumentValidationStatus.NotProvenNotFailed, usdjpy.ValidationStatus);
    }

    private static ExecutionSimScenarioMatrixLine Scenario(string id)
        => ExecutionSimR002PolicyScenarioMatrix.CreateScenarioMatrix().Single(x => x.ScenarioId == id);

    private static string SourcePath()
        => Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/ExecutionSimCloseSeekingFoundation.cs");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
