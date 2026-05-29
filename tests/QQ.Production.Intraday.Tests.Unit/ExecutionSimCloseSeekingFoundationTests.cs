using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimCloseSeekingFoundationTests
{
    [Fact]
    public void Quote_path_fixture_contract_exists()
    {
        var path = ExecutionSimR001CloseSeekingFoundation.CreateQuotePath(ExecutionSimQuotePathScenario.NormalLiquid);

        Assert.Equal(ExecutionSimQuotePathScenario.NormalLiquid, path.Scenario);
        Assert.True(path.FixtureOnly);
        Assert.True(path.NoExternal);
        Assert.True(path.NoLiveMarketData);
        Assert.NotEmpty(path.Quotes);
        Assert.All(path.Quotes, quote =>
        {
            Assert.Equal("EURUSD", quote.InstrumentId);
            Assert.Equal("EURUSD", quote.ExecutionTradableSymbol);
            Assert.True(quote.Bid < quote.Mid);
            Assert.True(quote.Mid < quote.Ask);
            Assert.Equal(quote.BarWindowEndUtc, quote.TargetCloseTimestampUtc);
            Assert.Equal(TimeSpan.FromMinutes(13), quote.TargetCloseTimestampUtc - quote.KnownAtTimestampUtc);
        });
    }

    [Fact]
    public void Close_benchmark_fixture_contract_exists()
    {
        var path = ExecutionSimR001CloseSeekingFoundation.CreateQuotePath(ExecutionSimQuotePathScenario.NormalLiquid);
        var benchmark = ExecutionSimR001CloseSeekingFoundation.CreateCloseBenchmark(path);

        Assert.Equal("EURUSD", benchmark.InstrumentId);
        Assert.Equal("EURUSD", benchmark.ExecutionTradableSymbol);
        Assert.Equal(CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote, benchmark.AvailabilityStatus);
        Assert.Equal(CloseConstructionMethod.FixtureClose, benchmark.ConstructionMethod);
        Assert.True(benchmark.FixtureOnly);
        Assert.NotNull(benchmark.CloseMid);
    }

    [Fact]
    public void Simulation_policy_contract_exists()
    {
        var contract = new ExecutionSimPolicyContract(
            ExecutionSimPolicy.CloseSeeking15mAdaptive,
            FixtureOnly: true,
            PaperOnly: true,
            NonExecutable: true,
            NotAnOrder: true,
            NotSubmitted: true,
            NoBrokerRoute: true,
            NoRealFill: true,
            NoExecutionReport: true,
            NegativeBaseline: false);

        Assert.True(contract.FixtureOnly);
        Assert.True(contract.PaperOnly);
        Assert.True(contract.NonExecutable);
        Assert.True(contract.NotAnOrder);
        Assert.True(contract.NotSubmitted);
        Assert.True(contract.NoBrokerRoute);
        Assert.True(contract.NoRealFill);
        Assert.True(contract.NoExecutionReport);
    }

    [Fact]
    public void Tca_report_contract_exists()
    {
        var report = ExecutionSimR001CloseSeekingFoundation.CreateTcaReport();

        Assert.True(report.IncludesSlippageVsClose);
        Assert.True(report.IncludesSpreadPaid);
        Assert.True(report.IncludesResidualAtClose);
        Assert.True(report.IncludesFillRatio);
        Assert.True(report.FixtureOnly);
        Assert.True(report.PaperOnly);
        Assert.True(report.NoOrdersCreated);
        Assert.True(report.NoRealFillsCreated);
        Assert.True(report.NoExecutionReportsCreated);
        Assert.True(report.NoRoutesCreated);
        Assert.True(report.NoSubmissionsCreated);
    }

    [Fact]
    public void Normal_quote_path_produces_valid_close15m_benchmark()
    {
        var benchmark = ExecutionSimR001CloseSeekingFoundation.CreateCloseBenchmark(
            ExecutionSimR001CloseSeekingFoundation.CreateQuotePath(ExecutionSimQuotePathScenario.NormalLiquid));

        Assert.Equal(CloseBenchmarkAvailabilityStatus.CloseConstructedFromValidQuote, benchmark.AvailabilityStatus);
        Assert.NotNull(benchmark.CloseBid);
        Assert.NotNull(benchmark.CloseAsk);
        Assert.True(benchmark.CloseSpreadBps <= 1m);
    }

    [Fact]
    public void Quote_gap_near_close_produces_safe_status()
    {
        var result = ExecutionSimR001CloseSeekingFoundation.Simulate(
            ExecutionSimPolicy.CloseSeeking15mAdaptive,
            ExecutionSimQuotePathScenario.QuoteGapNearClose);

        Assert.Equal(FeedGapCategory.NoQuoteNearClose, result.QuoteGapStatus);
        Assert.Equal(FeedReadinessStatus.NoQuoteNearClose, result.FeedReadinessStatus);
        Assert.Equal(CloseBenchmarkAvailabilityStatus.CloseUnavailable, result.BenchmarkAvailabilityStatus);
        Assert.Equal(SimulationOutcomeStatus.ManualReviewSafe, result.SimulationOutcomeStatus);
    }

    [Fact]
    public void Stale_quote_near_close_produces_safe_status()
    {
        var result = ExecutionSimR001CloseSeekingFoundation.Simulate(
            ExecutionSimPolicy.CloseSeeking15mAdaptive,
            ExecutionSimQuotePathScenario.StaleQuoteNearClose);

        Assert.Equal(SafeExecutionAlgoReasonCategory.StaleQuoteNearClose, result.StalenessStatus);
        Assert.Equal(FeedReadinessStatus.StaleQuotes, result.FeedReadinessStatus);
        Assert.Equal(SimulationOutcomeStatus.ManualReviewSafe, result.SimulationOutcomeStatus);
    }

    [Fact]
    public void Wide_spread_produces_safe_status()
    {
        var result = ExecutionSimR001CloseSeekingFoundation.Simulate(
            ExecutionSimPolicy.CloseSeeking15mAdaptive,
            ExecutionSimQuotePathScenario.WideSpreadNearClose);

        Assert.Equal(FeedReadinessStatus.SpreadTooWide, result.FeedReadinessStatus);
        Assert.Equal(CloseBenchmarkAvailabilityStatus.InconclusiveSafe, result.BenchmarkAvailabilityStatus);
        Assert.Equal(SimulationOutcomeStatus.ManualReviewSafe, result.SimulationOutcomeStatus);
    }

    [Fact]
    public void Wakett_pure_limit_until_close_produces_high_residual_and_nonfill_cost()
    {
        var result = ExecutionSimR001CloseSeekingFoundation.Simulate(
            ExecutionSimPolicy.WakettPureLimitUntilClose,
            ExecutionSimQuotePathScenario.LowPassiveFillProbability);

        Assert.Equal(SimulationOutcomeStatus.BlockedUnsafePattern, result.SimulationOutcomeStatus);
        Assert.True(result.ResidualAtClose >= 0.80m);
        Assert.True(result.EstimatedNonFillCost > 0m);
        Assert.True(result.EstimatedOpportunityCost > result.EstimatedSpreadCost);
    }

    [Fact]
    public void Wakett_five_market_slices_produces_repeated_spread_crossing_and_high_spread_cost()
    {
        var result = ExecutionSimR001CloseSeekingFoundation.Simulate(
            ExecutionSimPolicy.WakettFiveMarketSlicesAroundClose,
            ExecutionSimQuotePathScenario.NormalLiquid);

        Assert.Equal(SimulationOutcomeStatus.BlockedUnsafePattern, result.SimulationOutcomeStatus);
        Assert.Equal(1m, result.AggressiveFillRatio);
        Assert.Equal(0m, result.PassiveFillRatio);
        Assert.True(result.SpreadPaidBps >= 5m);
    }

    [Fact]
    public void Close_seeking_15m_adaptive_produces_design_only_simulated_result()
    {
        var result = ExecutionSimR001CloseSeekingFoundation.Simulate(
            ExecutionSimPolicy.CloseSeeking15mAdaptive,
            ExecutionSimQuotePathScenario.NormalLiquid);

        Assert.Equal(SimulationOutcomeStatus.CompletedFixtureOnly, result.SimulationOutcomeStatus);
        Assert.True(result.FixtureOnly);
        Assert.True(result.PaperOnly);
        Assert.True(result.NonExecutable);
        Assert.True(result.NotAnOrder);
        Assert.True(result.NotSubmitted);
        Assert.True(result.NoBrokerRoute);
        Assert.True(result.NoRealFill);
        Assert.True(result.NoExecutionReport);
    }

    [Fact]
    public void Controlled_residual_cross_only_activates_when_opportunity_cost_exceeds_spread_cost()
    {
        var justified = ExecutionSimR001CloseSeekingFoundation.Simulate(
            ExecutionSimPolicy.ControlledResidualCross,
            ExecutionSimQuotePathScenario.ResidualHighNearClose);
        var blocked = ExecutionSimR001CloseSeekingFoundation.Simulate(
            ExecutionSimPolicy.ControlledResidualCross,
            ExecutionSimQuotePathScenario.NormalLiquid);

        Assert.Equal(SimulationOutcomeStatus.CompletedFixtureOnly, justified.SimulationOutcomeStatus);
        Assert.True(justified.EstimatedOpportunityCost > justified.EstimatedSpreadCost);
        Assert.Equal(SimulationOutcomeStatus.ManualReviewSafe, blocked.SimulationOutcomeStatus);
        Assert.True(blocked.EstimatedOpportunityCost <= blocked.EstimatedSpreadCost);
    }

    [Fact]
    public void Blind_market_crossing_without_justification_is_blocked()
    {
        var result = ExecutionSimR001CloseSeekingFoundation.Simulate(
            ExecutionSimPolicy.ControlledResidualCross,
            ExecutionSimQuotePathScenario.NormalLiquid);

        Assert.Equal(SimulationOutcomeStatus.ManualReviewSafe, result.SimulationOutcomeStatus);
        Assert.True(result.NonExecutable);
        Assert.True(result.NotSubmitted);
        Assert.True(result.NoBrokerRoute);
    }

    [Theory]
    [InlineData(ExecutionSimPolicy.TWAPBenchmarkOnly)]
    [InlineData(ExecutionSimPolicy.VWAPBenchmarkOnly)]
    public void Benchmark_only_policies_are_non_executable(ExecutionSimPolicy policy)
    {
        var result = ExecutionSimR001CloseSeekingFoundation.Simulate(policy, ExecutionSimQuotePathScenario.NormalLiquid);

        Assert.Equal(SimulationOutcomeStatus.BenchmarkOnly, result.SimulationOutcomeStatus);
        Assert.True(result.NonExecutable);
        Assert.True(result.NotAnOrder);
        Assert.True(result.NoBrokerRoute);
        Assert.Equal(0m, result.FillRatio);
    }

    [Fact]
    public void Tca_report_includes_slippage_spread_residual_and_fill_ratio_metrics()
    {
        var report = ExecutionSimR001CloseSeekingFoundation.CreateTcaReport();

        Assert.All(report.Lines, line =>
        {
            Assert.True(line.SlippageVsCloseBps >= 0m);
            Assert.True(line.SpreadPaidBps >= 0m);
            Assert.InRange(line.ResidualAtClose, 0m, 1m);
            Assert.InRange(line.FillRatio, 0m, 1m);
        });
    }

    [Fact]
    public void Major_pair_5_usd_per_million_target_is_best_case_only()
    {
        var calibration = ExecutionSimR001CloseSeekingFoundation.CreateCostBucketCalibration();

        Assert.Equal(5m, calibration.BestCaseMajorTargetUsdPerMillion);
        Assert.True(calibration.FiveUsdPerMillionIsBestCaseOnly);
        Assert.False(calibration.FiveUsdPerMillionUniversalized);
        Assert.Equal(CostBucketStatus.MajorUsdPairCostBucket, calibration.MajorBucketStatus);
    }

    [Fact]
    public void Non_major_instruments_require_higher_bucket_or_calibration()
    {
        var calibration = ExecutionSimR001CloseSeekingFoundation.CreateCostBucketCalibration();

        Assert.Equal(CostBucketStatus.RequiresLiquidityCalibration, calibration.NonMajorBucketStatus);
        Assert.True(calibration.StressMajorTargetUsdPerMillionLow > calibration.BaseCaseMajorTargetUsdPerMillionHigh);
    }

    [Fact]
    public void All_simulation_results_are_paper_only_fixture_only_and_not_real_fills_or_reports()
    {
        var report = ExecutionSimR001CloseSeekingFoundation.CreateTcaReport();

        Assert.All(report.Lines, line =>
        {
            Assert.True(line.FixtureOnly);
            Assert.True(line.PaperOnly);
            Assert.True(line.NoRealFill);
            Assert.True(line.NoExecutionReport);
        });
    }

    [Fact]
    public void No_orders_routes_or_submissions_are_created()
    {
        var report = ExecutionSimR001CloseSeekingFoundation.CreateTcaReport();

        Assert.True(report.NoOrdersCreated);
        Assert.True(report.NoRoutesCreated);
        Assert.True(report.NoSubmissionsCreated);
        Assert.All(report.Lines, line =>
        {
            Assert.True(line.NotAnOrder);
            Assert.True(line.NotSubmitted);
            Assert.True(line.NoBrokerRoute);
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
