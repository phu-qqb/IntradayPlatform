using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimOperatorQuoteFileBacktestDryRunTests
{
    [Fact]
    public void Backtest_dry_run_contract_reuses_r007_r004_and_r005_shapes_no_externally()
    {
        var contract = ExecutionSimR008OperatorQuoteFileBacktestDryRun.CreateContract();

        Assert.True(contract.ReusesR007AcceptedManifests);
        Assert.True(contract.ReusesR007QuarantinedManifests);
        Assert.True(contract.ReusesR004SanitizedImportPath);
        Assert.True(contract.ReusesR004QuoteWindowExtraction);
        Assert.True(contract.ReusesR004CloseBenchmarkConstruction);
        Assert.True(contract.ReusesR004FeedQualityScoring);
        Assert.True(contract.ReusesR005ImportedQuoteTcaBacktestFlow);
        Assert.True(contract.AcceptedFilesOnly);
        Assert.True(contract.QuarantinedFilesExcluded);
        Assert.True(contract.FixtureOnly);
        Assert.True(contract.PaperOnly);
        Assert.False(contract.PolygonApiCalled);
        Assert.False(contract.LmaxCalled);
        Assert.False(contract.ExternalApiCalled);
    }

    [Fact]
    public void Accepted_eurusd_usdjpy_and_audusd_files_feed_dry_run()
    {
        var package = ExecutionSimR008OperatorQuoteFileBacktestDryRun.CreatePackage();

        Assert.Contains(package.AcceptedManifestsUsed, x => x.ExecutionTradableSymbol == "EURUSD");
        Assert.Contains(package.AcceptedManifestsUsed, x => x.ExecutionTradableSymbol == "USDJPY");
        Assert.Contains(package.AcceptedManifestsUsed, x => x.ExecutionTradableSymbol == "AUDUSD");
        Assert.Contains(package.ImportedQuoteFixturesUsed, x => x.ExecutionTradableSymbol == "EURUSD");
        Assert.Contains(package.ImportedQuoteFixturesUsed, x => x.ExecutionTradableSymbol == "USDJPY" && x.RequiresInversion && x.NormalizedPortfolioSymbol == "JPYUSD");
        Assert.Contains(package.ImportedQuoteFixturesUsed, x => x.ExecutionTradableSymbol == "AUDUSD" && !x.RequiresInversion);
    }

    [Fact]
    public void Quarantined_direct_cross_missing_convention_secret_and_raw_payload_files_are_excluded()
    {
        var package = ExecutionSimR008OperatorQuoteFileBacktestDryRun.CreatePackage();

        Assert.True(package.DirectCrossExcluded);
        Assert.True(package.MissingConventionExcluded);
        Assert.True(package.SecretRiskExcluded);
        Assert.True(package.RawPayloadRiskExcluded);
        Assert.False(package.Result.QuarantinedFilesFeedBacktest);
        Assert.Contains(package.QuarantinedManifestsExcluded, x => x.ProviderSymbol == "C:EUR-GBP");
        Assert.Contains(package.QuarantinedManifestsExcluded, x => x.ProviderSymbol == "C:SGD-USD");
        Assert.DoesNotContain(package.PolicyResults, x => x.ExecutionTradableSymbol == "EURGBP");
        Assert.DoesNotContain(package.PolicyResults, x => x.ExecutionTradableSymbol == "SGDUSD");
    }

    [Fact]
    public void Dry_run_creates_quote_windows_close_benchmarks_feed_quality_policy_results_and_tca_reports()
    {
        var package = ExecutionSimR008OperatorQuoteFileBacktestDryRun.CreatePackage();

        Assert.Equal(3, package.QuoteWindowsCreated.Count);
        Assert.Equal(3, package.CloseBenchmarksCreated.Count);
        Assert.Equal(3, package.FeedQualityResults.Count);
        Assert.NotEmpty(package.PolicyResults);
        Assert.NotEmpty(package.TcaReports);
        Assert.All(package.QuoteWindowsCreated, x => Assert.True(x.QuoteCount > 0));
        Assert.All(package.CloseBenchmarksCreated, x => Assert.Equal(HistoricalCloseBenchmarkStatus.Available, x.CloseBenchmarkStatus));
        Assert.All(package.FeedQualityResults, x => Assert.Equal(HistoricalFeedQualityBucket.Good, x.FeedQualityBucket));
    }

    [Fact]
    public void Policy_results_include_wakett_baselines_close_seeking_adaptive_and_controlled_residual_cross()
    {
        var lines = ExecutionSimR008OperatorQuoteFileBacktestDryRun.CreatePackage().PolicyResults;

        Assert.Contains(lines, x => x.Policy == ExecutionSimPolicy.WakettPureLimitUntilClose && x.BlockReason == AlgoPolicyReasonCategory.WakettPatternBlocked);
        Assert.Contains(lines, x => x.Policy == ExecutionSimPolicy.WakettFiveMarketSlicesAroundClose && x.SpreadPaidBps >= 5m);
        Assert.Contains(lines, x => x.Policy == ExecutionSimPolicy.CloseSeeking15mAdaptive && x.SimulationOutcomeStatus == SimulationOutcomeStatus.CompletedFixtureOnly);
        Assert.Contains(lines, x => x.Policy == ExecutionSimPolicy.ControlledResidualCross && x.EstimatedOpportunityCost > x.EstimatedSpreadCost);
        Assert.Contains(lines, x => x.Policy == ExecutionSimPolicy.TWAPBenchmarkOnly && x.SimulationOutcomeStatus == SimulationOutcomeStatus.BenchmarkOnly);
        Assert.Contains(lines, x => x.Policy == ExecutionSimPolicy.VWAPBenchmarkOnly && x.SimulationOutcomeStatus == SimulationOutcomeStatus.BenchmarkOnly);
    }

    [Fact]
    public void Per_instrument_reports_exist_for_eurusd_usdjpy_and_audusd()
    {
        var reports = ExecutionSimR008OperatorQuoteFileBacktestDryRun.CreatePackage().PerInstrumentReports;

        Assert.Contains(reports, x => x.ExecutionTradableSymbol == "EURUSD" && x.NormalizedPortfolioSymbol == "EURUSD");
        Assert.Contains(reports, x => x.ExecutionTradableSymbol == "USDJPY" && x.NormalizedPortfolioSymbol == "JPYUSD" && x.RequiresInversion);
        Assert.Contains(reports, x => x.ExecutionTradableSymbol == "AUDUSD" && x.NormalizedPortfolioSymbol == "AUDUSD");
        Assert.All(reports, x =>
        {
            Assert.Equal(HistoricalCloseBenchmarkStatus.Available, x.CloseBenchmarkStatus);
            Assert.Equal(HistoricalFeedQualityBucket.Good, x.FeedQualityBucket);
            Assert.Contains("Wakett", x.PolicyComparisonSummary);
        });
    }

    [Fact]
    public void Policy_comparison_rankings_and_cost_controls_are_preserved()
    {
        var package = ExecutionSimR008OperatorQuoteFileBacktestDryRun.CreatePackage();

        Assert.NotEmpty(package.MedianSlippageRanking);
        Assert.NotEmpty(package.P95SlippageRanking);
        Assert.NotEmpty(package.FillRatioRanking);
        Assert.NotEmpty(package.ResidualRanking);
        Assert.NotEmpty(package.SpreadPaidRanking);
        Assert.True(package.FiveUsdPerMillionBestCaseOnly);
        Assert.False(package.FiveUsdPerMillionUniversalized);
        Assert.True(package.NonMajorCalibrationPreserved);
    }

    [Fact]
    public void Dry_run_outputs_are_fixture_only_non_executable_and_not_order_domain_records()
    {
        var package = ExecutionSimR008OperatorQuoteFileBacktestDryRun.CreatePackage();

        Assert.True(package.FixtureOnly);
        Assert.True(package.PaperOnly);
        Assert.True(package.NonExecutable);
        Assert.True(package.NotAnOrder);
        Assert.True(package.NotSubmitted);
        Assert.True(package.NoBrokerRoute);
        Assert.True(package.NoRealFill);
        Assert.True(package.NoExecutionReport);
        Assert.False(package.OrdersCreated);
        Assert.False(package.FillsCreated);
        Assert.False(package.ExecutionReportsCreated);
        Assert.False(package.RoutesCreated);
        Assert.False(package.SubmissionsCreated);
        Assert.All(package.PolicyResults, x =>
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
    public void No_polygon_lmax_broker_marketdata_runtime_api_worker_or_scheduler_is_introduced()
    {
        var package = ExecutionSimR008OperatorQuoteFileBacktestDryRun.CreatePackage();
        var source = File.ReadAllText(SourcePath());

        Assert.False(package.PolygonApiCalled);
        Assert.False(package.LmaxCalled);
        Assert.False(package.ExternalApiCalled);
        Assert.False(package.BrokerMarketDataRuntimeActionDetected);
        Assert.DoesNotContain("HttpClient", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GetAsync", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PostAsync", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SendAsync", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WebSocket", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TcpClient", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SslStream", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MarketDataRequest", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MarketDataResponse", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FixSession", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectAsync", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BackgroundService", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IHostedService", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PeriodicTimer", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Audusd_is_not_misclassified_as_failed_and_usdjpy_caveat_remains_preserved()
    {
        var package = ExecutionSimR008OperatorQuoteFileBacktestDryRun.CreatePackage();

        Assert.Contains(package.PerInstrumentReports, x => x.ExecutionTradableSymbol == "AUDUSD");
        Assert.Contains(package.PerInstrumentReports, x => x.ExecutionTradableSymbol == "USDJPY" && x.RequiresInversion && x.NormalizedPortfolioSymbol == "JPYUSD");
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
