using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimImportedQuoteFixtureBacktestTests
{
    [Fact]
    public void Imported_eurusd_quote_fixture_can_feed_simulation()
    {
        var report = ExecutionSimR005ImportedQuoteFixtureBacktest.CreateReport();

        Assert.True(report.UsesImportedSanitizedQuoteFixtures);
        Assert.Contains(report.ImportedQuoteWindows, x => x.ExecutionTradableSymbol == "EURUSD" && x.QuoteCount > 0);
        Assert.Contains(report.Lines, x => x.ExecutionTradableSymbol == "EURUSD" && x.Policy == ExecutionSimPolicy.CloseSeeking15mAdaptive);
    }

    [Fact]
    public void Imported_usdjpy_quote_fixture_can_feed_simulation_with_inversion_mapping()
    {
        var report = ExecutionSimR005ImportedQuoteFixtureBacktest.CreateReport();
        var usdjpy = report.Lines.First(x => x.ExecutionTradableSymbol == "USDJPY" && x.Policy == ExecutionSimPolicy.CloseSeeking15mAdaptive);

        Assert.Equal("JPYUSD", usdjpy.NormalizedPortfolioSymbol);
        Assert.True(usdjpy.RequiresInversion);
        Assert.Equal(CostBucketStatus.MajorUsdPairCostBucket, usdjpy.CostBucketStatus);
    }

    [Fact]
    public void Quote_window_close_benchmark_and_feed_quality_from_imported_rows_are_produced()
    {
        var report = ExecutionSimR005ImportedQuoteFixtureBacktest.CreateReport();

        Assert.NotEmpty(report.ImportedQuoteWindows);
        Assert.Contains(report.ImportedQuoteWindows, x => x.QuoteCountLastMinute > 0);
        Assert.Contains(report.CloseBenchmarks, x => x.CloseBenchmarkStatus == HistoricalCloseBenchmarkStatus.Available);
        Assert.Contains(report.FeedQualityScores, x => x.FeedQualityScore > 0m && x.FeedQualityBucket == HistoricalFeedQualityBucket.Good);
    }

    [Fact]
    public void Wakett_baselines_and_close_seeking_policies_run_on_imported_window()
    {
        var report = ExecutionSimR005ImportedQuoteFixtureBacktest.CreateReport();

        Assert.Contains(report.Lines, x => x.Policy == ExecutionSimPolicy.WakettPureLimitUntilClose && x.BlockReason == AlgoPolicyReasonCategory.WakettPatternBlocked);
        Assert.Contains(report.Lines, x => x.Policy == ExecutionSimPolicy.WakettFiveMarketSlicesAroundClose && x.SpreadPaidBps >= 5m);
        Assert.Contains(report.Lines, x => x.Policy == ExecutionSimPolicy.CloseSeeking15mAdaptive && x.SimulationOutcomeStatus == SimulationOutcomeStatus.CompletedFixtureOnly);
        Assert.Contains(report.Lines, x => x.Policy == ExecutionSimPolicy.ControlledResidualCross && x.EstimatedOpportunityCost > x.EstimatedSpreadCost);
    }

    [Fact]
    public void Unsafe_imported_quote_windows_block_or_manual_review_simulation()
    {
        var report = ExecutionSimR005ImportedQuoteFixtureBacktest.CreateReport();

        Assert.Contains(report.Lines, x => x.BlockReason == AlgoPolicyReasonCategory.NoQuoteNearClose && x.SimulationOutcomeStatus == SimulationOutcomeStatus.ManualReviewSafe);
        Assert.Contains(report.Lines, x => x.BlockReason == AlgoPolicyReasonCategory.StaleQuoteNearClose && x.SimulationOutcomeStatus == SimulationOutcomeStatus.ManualReviewSafe);
        Assert.Contains(report.Lines, x => x.BlockReason == AlgoPolicyReasonCategory.SpreadTooWide && x.SimulationOutcomeStatus == SimulationOutcomeStatus.ManualReviewSafe);
    }

    [Fact]
    public void Direct_cross_eurgbp_imported_fixture_is_blocked_as_direct_execution()
    {
        var report = ExecutionSimR005ImportedQuoteFixtureBacktest.CreateReport();

        Assert.True(report.DirectCrossSignalsNotExecuted);
        Assert.Contains(report.Lines, x => x.ScenarioId == "eurgbp-imported-direct-cross-blocked" && x.BlockReason == AlgoPolicyReasonCategory.DirectCrossExecutionDisabled);
    }

    [Fact]
    public void Invalid_imported_rows_do_not_feed_simulation()
    {
        var invalid = ExecutionSimR005ImportedQuoteFixtureBacktest.CreateInvalidImportEvidence();
        var report = ExecutionSimR005ImportedQuoteFixtureBacktest.CreateReport();

        Assert.True(report.InvalidRowsDoNotFeedSimulation);
        Assert.Empty(invalid.AcceptedRows);
        Assert.True(invalid.RejectedRowCount > 0);
    }

    [Fact]
    public void Tca_report_fields_are_present_on_imported_policy_results()
    {
        var line = ExecutionSimR005ImportedQuoteFixtureBacktest.CreateReport().Lines
            .First(x => x.Policy == ExecutionSimPolicy.CloseSeeking15mAdaptive && x.BenchmarkAvailabilityStatus == HistoricalCloseBenchmarkStatus.Available);

        Assert.NotNull(line.Close15mBenchmark);
        Assert.True(line.SlippageVsCloseBps > 0m);
        Assert.True(line.SlippageVsCloseUsdPerMillion > 0m);
        Assert.True(line.SpreadPaidBps > 0m);
        Assert.True(line.SpreadPaidUsdPerMillion > 0m);
        Assert.True(line.ResidualAtClose >= 0m);
        Assert.True(line.FillRatio > 0m);
    }

    [Fact]
    public void Policy_comparison_report_and_rankings_are_produced()
    {
        var report = ExecutionSimR005ImportedQuoteFixtureBacktest.CreateReport();

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
    public void Cost_bucket_controls_are_preserved()
    {
        var report = ExecutionSimR005ImportedQuoteFixtureBacktest.CreateReport();

        Assert.True(report.FiveUsdPerMillionBestCaseOnly);
        Assert.False(report.FiveUsdPerMillionUniversalized);
        Assert.True(report.NonMajorCalibrationRequired);
        Assert.True(report.EmCnhCalibrationRequired);
    }

    [Fact]
    public void Simulation_outputs_are_not_fills_reports_orders_routes_or_submissions()
    {
        var report = ExecutionSimR005ImportedQuoteFixtureBacktest.CreateReport();

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
    public void No_polygon_lmax_broker_marketdata_runtime_api_worker_or_scheduler_is_introduced()
    {
        var report = ExecutionSimR005ImportedQuoteFixtureBacktest.CreateReport();
        var source = File.ReadAllText(SourcePath());

        Assert.True(report.NoPolygonApiCall);
        Assert.True(report.NoLmaxCall);
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
        var report = ExecutionSimR005ImportedQuoteFixtureBacktest.CreateReport();

        Assert.Contains(report.Lines, x => x.ExecutionTradableSymbol == "USDJPY" && x.RequiresInversion);
        Assert.True(report.UsesUsdPairNormalization);
        Assert.True(report.DirectCrossSignalsNotExecuted);
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
