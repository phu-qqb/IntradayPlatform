using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimFirstRealOfflineTcaBacktestTests
{
    [Fact]
    public void Required_r013_backtest_artifacts_exist()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R013 artifact {artifact}");
        }
    }

    [Fact]
    public void Backtest_contract_and_run_result_are_fixture_only_no_external_outputs()
    {
        var contract = ReadJson("phase-exec-sim-r013-backtest-execution-contract.json");
        var result = ReadJson("phase-exec-sim-r013-backtest-run-result.json");

        Assert.Equal("EXEC-SIM-R012", contract.RootElement.GetProperty("sourceAuthorizationPhase").GetString());
        Assert.Equal("EXEC-SIM-R011", contract.RootElement.GetProperty("sourceValidationPhase").GetString());
        Assert.True(contract.RootElement.GetProperty("noExternal").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("importsIntoSanitizedFixtureOnlyQuoteWindows").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("resultsFixtureOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("resultsPaperOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("resultsNonExecutable").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("simulationResultLinesAreFills").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("submissionsCreated").GetBoolean());

        Assert.Equal("CompletedFixtureOnlyPaperOnly", result.RootElement.GetProperty("SimulationStatus").GetString());
        Assert.Equal("NoExternalNoRealFillNoOrder", result.RootElement.GetProperty("SafetyStatus").GetString());
        Assert.True(result.RootElement.GetProperty("NoApiCall").GetBoolean());
        Assert.True(result.RootElement.GetProperty("NoOrderDomainOutput").GetBoolean());
        Assert.Equal(3, result.RootElement.GetProperty("AcceptedManifestIds").GetArrayLength());
        Assert.Equal(3, result.RootElement.GetProperty("QuoteWindowIds").GetArrayLength());
        Assert.Equal(3, result.RootElement.GetProperty("CloseBenchmarkIds").GetArrayLength());
        Assert.Equal(3, result.RootElement.GetProperty("FeedQualityResultIds").GetArrayLength());
    }

    [Fact]
    public void Accepted_files_imported_windows_benchmarks_and_feed_quality_are_present()
    {
        var accepted = ReadJson("phase-exec-sim-r013-accepted-files-used.json");
        var fixtures = ReadJson("phase-exec-sim-r013-imported-quote-fixtures.json");
        var windows = ReadJson("phase-exec-sim-r013-quote-windows.json");
        var benchmarks = ReadJson("phase-exec-sim-r013-close-benchmarks.json");
        var feed = ReadJson("phase-exec-sim-r013-feed-quality-results.json");

        Assert.False(accepted.RootElement.GetProperty("quarantinedFilesUsed").GetBoolean());
        Assert.False(accepted.RootElement.GetProperty("directCrossFilesUsed").GetBoolean());
        AssertAcceptedFile(accepted, "EURUSD", "EURUSD", false, 54694);
        AssertAcceptedFile(accepted, "USDJPY", "JPYUSD", true, 59368);
        AssertAcceptedFile(accepted, "AUDUSD", "AUDUSD", false, 60656);
        Assert.Equal("4004", FindBySymbol(accepted, "acceptedFilesUsed", "USDJPY").GetProperty("SecurityID").GetString());
        Assert.Equal("8", FindBySymbol(accepted, "acceptedFilesUsed", "USDJPY").GetProperty("SecurityIDSource").GetString());
        Assert.Contains("not failed", FindBySymbol(accepted, "acceptedFilesUsed", "AUDUSD").GetProperty("AudusdStatus").GetString());

        Assert.True(fixtures.RootElement.GetProperty("sanitizedFixtureOnlyQuoteWindowsCreated").GetBoolean());
        Assert.False(fixtures.RootElement.GetProperty("orderDomainEntitiesCreated").GetBoolean());
        AssertFixture(fixtures, "EURUSD");
        AssertFixture(fixtures, "USDJPY");
        AssertFixture(fixtures, "AUDUSD");

        foreach (var symbol in new[] { "EURUSD", "USDJPY", "AUDUSD" })
        {
            Assert.Equal("Ready", FindBySymbol(windows, "quoteWindows", symbol).GetProperty("FeedWindowStatus").GetString());
            Assert.Equal("Available", FindBySymbol(benchmarks, "closeBenchmarks", symbol).GetProperty("CloseBenchmarkStatus").GetString());
            Assert.Equal("Good", FindBySymbol(feed, "feedQualityResults", symbol).GetProperty("FeedQualityBucket").GetString());
        }
    }

    [Fact]
    public void All_expected_policies_are_reported_as_non_executable_tca_outputs_not_fills()
    {
        var policies = ReadJson("phase-exec-sim-r013-policy-results.json");
        var expected = new[]
        {
            "WakettPureLimitUntilClose",
            "WakettFiveMarketSlicesAroundClose",
            "PassiveUntilUrgency",
            "CloseSeeking15m",
            "CloseSeeking15mAdaptive",
            "ControlledResidualCross",
            "ImmediatePaperBenchmark",
            "TWAPBenchmarkOnly",
            "VWAPBenchmarkOnly",
            "ManualReview",
            "DoNotTrade"
        };

        foreach (var policy in expected)
        {
            var row = FindByPolicy(policies, policy);
            Assert.True(row.GetProperty("FixtureOnly").GetBoolean());
            Assert.True(row.GetProperty("PaperOnly").GetBoolean());
            Assert.True(row.GetProperty("NonExecutable").GetBoolean());
            Assert.True(row.GetProperty("NotAnOrder").GetBoolean());
            Assert.True(row.GetProperty("NoRealFill").GetBoolean());
            Assert.True(row.GetProperty("NoExecutionReport").GetBoolean());
        }

        Assert.False(policies.RootElement.GetProperty("simulationResultLinesAreFills").GetBoolean());
        Assert.False(policies.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());
        Assert.False(policies.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.Equal("NegativeBaselineHighResidual", FindByPolicy(policies, "WakettPureLimitUntilClose").GetProperty("SimulationOutcomeStatus").GetString());
        Assert.Equal("NegativeBaselineRepeatedSpreadCrossing", FindByPolicy(policies, "WakettFiveMarketSlicesAroundClose").GetProperty("SimulationOutcomeStatus").GetString());
        Assert.Equal("BestTradeoffPassiveFillResidualControl", FindByPolicy(policies, "CloseSeeking15mAdaptive").GetProperty("SimulationOutcomeStatus").GetString());
        Assert.Equal("HelpfulOnlyWhenOpportunityCostExceedsCrossingCost", FindByPolicy(policies, "ControlledResidualCross").GetProperty("SimulationOutcomeStatus").GetString());
    }

    [Fact]
    public void Tca_reports_per_instrument_reports_and_rankings_are_produced()
    {
        var tca = ReadJson("phase-exec-sim-r013-tca-reports.json");
        var comparison = ReadJson("phase-exec-sim-r013-policy-comparison-report.json");

        Assert.True(tca.RootElement.GetProperty("FixtureOnly").GetBoolean());
        Assert.True(tca.RootElement.GetProperty("PaperOnly").GetBoolean());
        Assert.True(tca.RootElement.GetProperty("NonExecutable").GetBoolean());
        Assert.True(tca.RootElement.GetProperty("NoRealFill").GetBoolean());
        Assert.True(tca.RootElement.GetProperty("NoExecutionReport").GetBoolean());
        Assert.Contains("policy comparison", tca.RootElement.GetProperty("reportsProduced").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("median slippage ranking", tca.RootElement.GetProperty("reportsProduced").EnumerateArray().Select(x => x.GetString()));

        foreach (var file in new[]
        {
            "phase-exec-sim-r013-per-instrument-eurusd-report.json",
            "phase-exec-sim-r013-per-instrument-usdjpy-report.json",
            "phase-exec-sim-r013-per-instrument-audusd-report.json",
            "phase-exec-sim-r013-policy-ranking-median-slippage.json",
            "phase-exec-sim-r013-policy-ranking-p95-slippage.json",
            "phase-exec-sim-r013-policy-ranking-fill-ratio.json",
            "phase-exec-sim-r013-policy-ranking-residual.json",
            "phase-exec-sim-r013-policy-ranking-spread-paid.json"
        })
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), file)), $"Missing report {file}");
        }

        Assert.Equal("CloseSeeking15mAdaptive", comparison.RootElement.GetProperty("BestOverallTradeoffPolicy").GetString());
        Assert.Equal("WakettPureLimitUntilClose", comparison.RootElement.GetProperty("WorstResidualPolicy").GetString());
        Assert.Equal("WakettFiveMarketSlicesAroundClose", comparison.RootElement.GetProperty("WorstSpreadPaidPolicy").GetString());
    }

    [Fact]
    public void Policy_specific_reports_preserve_expected_qualitative_outcomes()
    {
        var limit = ReadJson("phase-exec-sim-r013-wakett-limit-baseline-report.json");
        var slices = ReadJson("phase-exec-sim-r013-wakett-five-market-slices-report.json");
        var passive = ReadJson("phase-exec-sim-r013-passive-until-urgency-report.json");
        var closeSeeking = ReadJson("phase-exec-sim-r013-close-seeking-15m-report.json");
        var adaptive = ReadJson("phase-exec-sim-r013-close-seeking-adaptive-report.json");
        var controlled = ReadJson("phase-exec-sim-r013-controlled-residual-cross-report.json");
        var benchmark = ReadJson("phase-exec-sim-r013-benchmark-only-policy-report.json");

        Assert.Equal("NegativeBaseline", limit.RootElement.GetProperty("BaselineType").GetString());
        Assert.True(limit.RootElement.GetProperty("ResidualAtClose").GetDecimal() > 0.3m);
        Assert.Equal("High", limit.RootElement.GetProperty("EstimatedNonFillCost").GetString());
        Assert.True(slices.RootElement.GetProperty("RepeatedSpreadCrossing").GetBoolean());
        Assert.True(slices.RootElement.GetProperty("SpreadPaidBps").GetDecimal() > 0.5m);
        Assert.Contains("good feed", passive.RootElement.GetProperty("Conclusion").GetString());
        Assert.Contains("Balanced", closeSeeking.RootElement.GetProperty("Conclusion").GetString());
        Assert.Contains("passive fill and residual control", adaptive.RootElement.GetProperty("Conclusion").GetString());
        Assert.True(controlled.RootElement.GetProperty("OpportunityCostExceedsCrossingCostRequired").GetBoolean());
        Assert.True(benchmark.RootElement.GetProperty("benchmarkOnly").GetBoolean());
        Assert.True(benchmark.RootElement.GetProperty("NonExecutable").GetBoolean());
        Assert.True(benchmark.RootElement.GetProperty("NotAnOrder").GetBoolean());
    }

    [Fact]
    public void Cost_direct_cross_usdjpy_audusd_and_lmax_preservations_hold()
    {
        var cost = ReadJson("phase-exec-sim-r013-major-pair-5usd-bestcase-only.json");
        var nonMajor = ReadJson("phase-exec-sim-r013-nonmajor-calibration-preservation.json");
        var directCross = ReadJson("phase-exec-sim-r013-direct-cross-exclusion-preservation.json");
        var usdjpy = ReadJson("phase-exec-sim-r013-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r013-lmax-readonly-baseline-reference.json");

        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(nonMajor.RootElement.GetProperty("nonMajorEmScandiCnhRequireLiquidityCalibration").GetBoolean());
        Assert.Equal("USD-pair-only", directCross.RootElement.GetProperty("executionUniverse").GetString());
        Assert.False(directCross.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("directCrossIncludedInBacktest").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("guidanceWeakened").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("caveatPreserved").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("requiresInversion").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.Contains("not failed", lmax.RootElement.GetProperty("audusdStatus").GetString());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR013").GetBoolean());
    }

    [Fact]
    public void No_api_runtime_order_fill_report_route_audits_are_clean()
    {
        var noExternal = ReadJson("phase-exec-sim-r013-no-external-audit.json");
        var api = ReadJson("phase-exec-sim-r013-no-external-api-call-audit.json");
        var polygon = ReadJson("phase-exec-sim-r013-no-polygon-api-call-audit.json");
        var lmax = ReadJson("phase-exec-sim-r013-no-lmax-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r013-no-broker-marketdata-runtime-audit.json");
        var fill = ReadJson("phase-exec-sim-r013-no-real-fill-audit.json");
        var report = ReadJson("phase-exec-sim-r013-no-execution-report-audit.json");
        var order = ReadJson("phase-exec-sim-r013-no-order-created-audit.json");
        var route = ReadJson("phase-exec-sim-r013-no-route-no-submission-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r013-forbidden-actions-audit.json");

        Assert.False(noExternal.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("ordersFillsReportsRoutesSubmissionsCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("realFillsCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("liveBrokerProductionTradingStateMutated").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(polygon.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataResponseRead").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("schedulerServiceTimerPollingBackgroundJobIntroduced").GetBoolean());
        Assert.False(fill.RootElement.GetProperty("realFillsCreated").GetBoolean());
        Assert.False(fill.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());
        Assert.False(report.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("executableOrdersCreated").GetBoolean());
        Assert.False(route.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(route.RootElement.GetProperty("submissionsCreated").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    private static void AssertAcceptedFile(JsonDocument document, string executionSymbol, string normalizedSymbol, bool requiresInversion, int rows)
    {
        var entry = FindBySymbol(document, "acceptedFilesUsed", executionSymbol);
        Assert.Equal(normalizedSymbol, entry.GetProperty("NormalizedPortfolioSymbol").GetString());
        Assert.Equal(requiresInversion, entry.GetProperty("RequiresInversion").GetBoolean());
        Assert.Equal(rows, entry.GetProperty("Rows").GetInt32());
        Assert.Equal("AcceptedForSanitizedImport", entry.GetProperty("ValidationStatus").GetString());
    }

    private static void AssertFixture(JsonDocument document, string executionSymbol)
    {
        var entry = FindBySymbol(document, "importedQuoteFixtures", executionSymbol);
        Assert.True(entry.GetProperty("RowsImportedIntoFixtureWindow").GetInt32() > 0);
        Assert.True(entry.GetProperty("FixtureOnly").GetBoolean());
        Assert.True(entry.GetProperty("PaperOnly").GetBoolean());
        Assert.False(entry.GetProperty("RawPayloadSerialized").GetBoolean());
    }

    private static JsonElement FindBySymbol(JsonDocument document, string propertyName, string symbol)
        => document.RootElement.GetProperty(propertyName)
            .EnumerateArray()
            .Single(x => x.GetProperty("ExecutionTradableSymbol").GetString() == symbol);

    private static JsonElement FindByPolicy(JsonDocument document, string policy)
        => document.RootElement.GetProperty("policyResults")
            .EnumerateArray()
            .Single(x => x.GetProperty("Policy").GetString() == policy);

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r013-summary.md",
        "phase-exec-sim-r013-backtest-execution-contract.json",
        "phase-exec-sim-r013-backtest-run-result.json",
        "phase-exec-sim-r013-accepted-files-used.json",
        "phase-exec-sim-r013-imported-quote-fixtures.json",
        "phase-exec-sim-r013-quote-windows.json",
        "phase-exec-sim-r013-close-benchmarks.json",
        "phase-exec-sim-r013-feed-quality-results.json",
        "phase-exec-sim-r013-policy-results.json",
        "phase-exec-sim-r013-tca-reports.json",
        "phase-exec-sim-r013-per-instrument-eurusd-report.json",
        "phase-exec-sim-r013-per-instrument-usdjpy-report.json",
        "phase-exec-sim-r013-per-instrument-audusd-report.json",
        "phase-exec-sim-r013-policy-comparison-report.json",
        "phase-exec-sim-r013-policy-ranking-median-slippage.json",
        "phase-exec-sim-r013-policy-ranking-p95-slippage.json",
        "phase-exec-sim-r013-policy-ranking-fill-ratio.json",
        "phase-exec-sim-r013-policy-ranking-residual.json",
        "phase-exec-sim-r013-policy-ranking-spread-paid.json",
        "phase-exec-sim-r013-wakett-limit-baseline-report.json",
        "phase-exec-sim-r013-wakett-five-market-slices-report.json",
        "phase-exec-sim-r013-passive-until-urgency-report.json",
        "phase-exec-sim-r013-close-seeking-15m-report.json",
        "phase-exec-sim-r013-close-seeking-adaptive-report.json",
        "phase-exec-sim-r013-controlled-residual-cross-report.json",
        "phase-exec-sim-r013-benchmark-only-policy-report.json",
        "phase-exec-sim-r013-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r013-major-pair-5usd-bestcase-only.json",
        "phase-exec-sim-r013-nonmajor-calibration-preservation.json",
        "phase-exec-sim-r013-no-polygon-api-call-audit.json",
        "phase-exec-sim-r013-no-lmax-call-audit.json",
        "phase-exec-sim-r013-no-external-api-call-audit.json",
        "phase-exec-sim-r013-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r013-no-real-fill-audit.json",
        "phase-exec-sim-r013-no-execution-report-audit.json",
        "phase-exec-sim-r013-no-order-created-audit.json",
        "phase-exec-sim-r013-no-route-no-submission-audit.json",
        "phase-exec-sim-r013-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r013-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r013-no-external-audit.json",
        "phase-exec-sim-r013-forbidden-actions-audit.json",
        "phase-exec-sim-r013-next-phase-recommendation.json"
    ];

    private static JsonDocument ReadJson(string fileName)
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(ArtifactsDir(), fileName)));

    private static string ArtifactsDir()
        => Path.Combine(RepoRoot(), "artifacts/readiness/execution-sim");

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
