using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimExpandedOfflineTcaBacktestTests
{
    [Fact]
    public void Required_r025_artifacts_exist_and_run_contract_consumes_r024_authorization()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R025 artifact {artifact}");
        }

        var contract = ReadJson("phase-exec-sim-r025-expanded-backtest-execution-contract.json");
        var run = ReadJson("phase-exec-sim-r025-expanded-backtest-run-result.json");
        var r024 = ReadJson("phase-exec-sim-r025-r024-authorization-reference.json");
        var r023 = ReadJson("phase-exec-sim-r025-r023-row-validation-reference.json");

        Assert.True(contract.RootElement.GetProperty("expandedBacktestExecutionContractCreated").GetBoolean());
        Assert.Equal("EXEC-SIM-R024", contract.RootElement.GetProperty("SourceAuthorizationPhase").GetString());
        Assert.Equal("EXEC-SIM-R023", contract.RootElement.GetProperty("SourceRowValidationPhase").GetString());
        Assert.Equal("PolygonOfflineFile", contract.RootElement.GetProperty("ProviderName").GetString());
        Assert.Equal("HistoricalBboQuotes", contract.RootElement.GetProperty("DatasetType").GetString());
        Assert.Equal("IntradayRebalance", contract.RootElement.GetProperty("SessionWindowCategory").GetString());
        Assert.True(contract.RootElement.GetProperty("AcceptedRowSetOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("RejectedRowsExcluded").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoApiCall").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoDbImport").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoPersistedSanitizedRows").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoOrderDomainOutput").GetBoolean());

        Assert.True(run.RootElement.GetProperty("expandedBacktestRunResultCreated").GetBoolean());
        Assert.Equal(7, run.RootElement.GetProperty("SymbolCount").GetInt32());
        Assert.Equal(11, run.RootElement.GetProperty("PolicyCount").GetInt32());
        Assert.Equal(112, run.RootElement.GetProperty("QuoteWindowCount").GetInt32());
        Assert.Equal(112, run.RootElement.GetProperty("CloseBenchmarkCount").GetInt32());
        Assert.Equal(7, run.RootElement.GetProperty("FeedQualityResultCount").GetInt32());
        Assert.Equal(1232, run.RootElement.GetProperty("TcaResultLineCount").GetInt32());
        Assert.True(r024.RootElement.GetProperty("r024AuthorizationReferenceCreated").GetBoolean());
        AssertContains(r024, "R024Classifications", "EXEC_SIM_R024_PASS_EXPANDED_BACKTEST_AUTHORIZATION_READY_NO_EXTERNAL");
        Assert.False(r024.RootElement.GetProperty("R024BacktestExecuted").GetBoolean());
        Assert.True(r023.RootElement.GetProperty("r023RowValidationReferenceCreated").GetBoolean());
        Assert.Equal(7, r023.RootElement.GetProperty("TotalRejectedRowCount").GetInt32());
        Assert.True(r023.RootElement.GetProperty("RejectedMalformedRowsExcludedFromBacktest").GetBoolean());
    }

    [Fact]
    public void Accepted_rows_quote_windows_close_benchmarks_and_feed_quality_are_built_from_accepted_rows()
    {
        var rows = ReadJson("phase-exec-sim-r025-accepted-rows-used-rejected-rows-excluded.json");
        var windows = ReadJson("phase-exec-sim-r025-quote-windows.json");
        var close = ReadJson("phase-exec-sim-r025-close-benchmarks.json");
        var feed = ReadJson("phase-exec-sim-r025-feed-quality-results.json");

        Assert.True(rows.RootElement.GetProperty("AcceptedRowSetOnly").GetBoolean());
        Assert.True(rows.RootElement.GetProperty("RejectedRowsExcluded").GetBoolean());
        Assert.False(rows.RootElement.GetProperty("PersistedSanitizedQuoteRowsCreated").GetBoolean());
        Assert.False(rows.RootElement.GetProperty("DbImportOccurred").GetBoolean());
        Assert.Equal(410659, rows.RootElement.GetProperty("totalAcceptedRowCount").GetInt32());
        Assert.Equal(7, rows.RootElement.GetProperty("totalRejectedRowCount").GetInt32());
        Assert.All(rows.RootElement.GetProperty("perSymbol").EnumerateArray(), symbol =>
        {
            Assert.True(symbol.GetProperty("AcceptedRowCountUsedInMemory").GetInt32() > 0);
            Assert.Equal(1, symbol.GetProperty("RejectedRowCountExcluded").GetInt32());
            Assert.Equal(1, symbol.GetProperty("MalformedJsonRowCountExcluded").GetInt32());
        });

        Assert.True(windows.RootElement.GetProperty("quoteWindowsCreated").GetBoolean());
        Assert.True(windows.RootElement.GetProperty("BuiltFromAcceptedRows").GetBoolean());
        Assert.Equal(112, windows.RootElement.GetProperty("resultCount").GetInt32());
        Assert.True(close.RootElement.GetProperty("closeBenchmarksCreated").GetBoolean());
        Assert.True(close.RootElement.GetProperty("BuiltFromAcceptedRows").GetBoolean());
        Assert.Equal(112, close.RootElement.GetProperty("resultCount").GetInt32());
        Assert.True(feed.RootElement.GetProperty("feedQualityResultsCreated").GetBoolean());
        Assert.True(feed.RootElement.GetProperty("ComputedFromAcceptedRows").GetBoolean());
        Assert.Equal(7, feed.RootElement.GetProperty("resultCount").GetInt32());
    }

    [Fact]
    public void Tca_result_lines_cover_all_symbols_and_policies_as_fixture_only_non_order_output()
    {
        var contract = ReadJson("phase-exec-sim-r025-tca-result-line-contract.json");
        var lines = ReadJson("phase-exec-sim-r025-tca-result-lines.json");
        var allLines = lines.RootElement.GetProperty("lines").EnumerateArray().ToArray();

        Assert.True(contract.RootElement.GetProperty("tcaResultLineContractCreated").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("FixtureOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("PaperOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NonExecutable").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("IsFill").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("IsExecutionReport").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("IsOrder").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("IsChildSlice").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("IsSubmitted").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("HasBrokerRoute").GetBoolean());

        Assert.True(lines.RootElement.GetProperty("tcaResultLinesCreated").GetBoolean());
        Assert.Equal(1232, lines.RootElement.GetProperty("ResultLineCount").GetInt32());
        Assert.Equal(1232, allLines.Length);
        AssertExpectedSymbolLines(allLines, "EURUSD", "EURUSD", false);
        AssertExpectedSymbolLines(allLines, "USDJPY", "JPYUSD", true);
        AssertExpectedSymbolLines(allLines, "AUDUSD", "AUDUSD", false);
        AssertExpectedSymbolLines(allLines, "GBPUSD", "GBPUSD", false);
        AssertExpectedSymbolLines(allLines, "NZDUSD", "NZDUSD", false);
        AssertExpectedSymbolLines(allLines, "USDCAD", "CADUSD", true);
        AssertExpectedSymbolLines(allLines, "USDCHF", "CHFUSD", true);

        foreach (var policy in ExpectedPolicies)
        {
            Assert.Contains(allLines, line => line.GetProperty("PolicyFamily").GetString() == policy);
        }

        Assert.All(allLines, line =>
        {
            Assert.True(line.GetProperty("FixtureOnly").GetBoolean());
            Assert.True(line.GetProperty("PaperOnly").GetBoolean());
            Assert.True(line.GetProperty("NonExecutable").GetBoolean());
            Assert.False(line.GetProperty("IsFill").GetBoolean());
            Assert.False(line.GetProperty("IsExecutionReport").GetBoolean());
            Assert.False(line.GetProperty("IsOrder").GetBoolean());
            Assert.False(line.GetProperty("IsChildSlice").GetBoolean());
            Assert.False(line.GetProperty("IsSubmitted").GetBoolean());
            Assert.False(line.GetProperty("HasBrokerRoute").GetBoolean());
        });
    }

    [Fact]
    public void Per_symbol_policy_comparison_rankings_and_policy_reports_are_produced()
    {
        foreach (var symbol in ExpectedSymbols)
        {
            var report = ReadJson($"phase-exec-sim-r025-per-symbol-{symbol.ToLowerInvariant()}-report.json");
            Assert.True(report.RootElement.GetProperty("reportCreated").GetBoolean());
            Assert.Equal(symbol, report.RootElement.GetProperty("ExecutionTradableSymbol").GetString());
            Assert.Equal(176, report.RootElement.GetProperty("ResultLineCount").GetInt32());
            Assert.True(report.RootElement.GetProperty("FixtureOnly").GetBoolean());
            Assert.True(report.RootElement.GetProperty("PaperOnly").GetBoolean());
            Assert.True(report.RootElement.GetProperty("NonExecutable").GetBoolean());
            Assert.True(report.RootElement.GetProperty("NoOrderDomainOutput").GetBoolean());
        }

        var usdJpy = ReadJson("phase-exec-sim-r025-per-symbol-usdjpy-report.json");
        Assert.Equal("JPYUSD", usdJpy.RootElement.GetProperty("NormalizedPortfolioSymbol").GetString());
        Assert.True(usdJpy.RootElement.GetProperty("RequiresInversion").GetBoolean());
        Assert.Equal("4004", usdJpy.RootElement.GetProperty("SecurityID").GetString());
        Assert.Equal("8", usdJpy.RootElement.GetProperty("SecurityIDSource").GetString());

        var policy = ReadJson("phase-exec-sim-r025-expanded-policy-comparison-report.json");
        Assert.True(policy.RootElement.GetProperty("expandedPolicyComparisonReportCreated").GetBoolean());
        Assert.Equal(11, policy.RootElement.GetProperty("PolicyCount").GetInt32());
        Assert.Equal(1232, policy.RootElement.GetProperty("ResultLineCount").GetInt32());
        Assert.Equal(11, policy.RootElement.GetProperty("comparisons").GetArrayLength());

        foreach (var ranking in RankingArtifacts)
        {
            var artifact = ReadJson(ranking);
            Assert.True(artifact.RootElement.GetProperty("rankingCreated").GetBoolean());
            Assert.Equal(11, artifact.RootElement.GetProperty("rankings").GetArrayLength());
        }
    }

    [Fact]
    public void Wakett_close_seeking_benchmark_and_expanded_major_reports_are_present_and_safe()
    {
        var wakettLimit = ReadJson("phase-exec-sim-r025-wakett-limit-baseline-report.json");
        var wakettSlices = ReadJson("phase-exec-sim-r025-wakett-five-market-slices-report.json");
        var adaptive = ReadJson("phase-exec-sim-r025-close-seeking-adaptive-report.json");
        var controlled = ReadJson("phase-exec-sim-r025-controlled-residual-cross-report.json");
        var benchmark = ReadJson("phase-exec-sim-r025-benchmark-only-policy-report.json");
        var expandedMajor = ReadJson("phase-exec-sim-r025-expanded-major-symbol-comparison.json");

        Assert.True(wakettLimit.RootElement.GetProperty("NegativeBaseline").GetBoolean());
        Assert.True(wakettLimit.RootElement.GetProperty("BlockedAsProductionDefault").GetBoolean());
        Assert.True(wakettLimit.RootElement.GetProperty("ShowsResidualNonFillOpportunityCostRisk").GetBoolean());
        Assert.True(wakettSlices.RootElement.GetProperty("NegativeBaseline").GetBoolean());
        Assert.True(wakettSlices.RootElement.GetProperty("BlockedAsProductionDefault").GetBoolean());
        Assert.True(wakettSlices.RootElement.GetProperty("ShowsRepeatedSpreadCrossingRisk").GetBoolean());
        Assert.True(adaptive.RootElement.GetProperty("RemainsCandidateWhereFeedAndSpreadAreGood").GetBoolean());
        Assert.True(controlled.RootElement.GetProperty("ConditionalOnOpportunityCostExceedingCrossingCost").GetBoolean());
        Assert.True(benchmark.RootElement.GetProperty("benchmarkOnlyPolicyReportCreated").GetBoolean());
        Assert.Equal(3, benchmark.RootElement.GetProperty("policies").GetArrayLength());
        Assert.All(benchmark.RootElement.GetProperty("policies").EnumerateArray(), policy =>
        {
            Assert.True(policy.GetProperty("BenchmarkOnly").GetBoolean());
            Assert.True(policy.GetProperty("NonExecutable").GetBoolean());
            Assert.True(policy.GetProperty("NotOrderDomainOutput").GetBoolean());
        });
        Assert.True(expandedMajor.RootElement.GetProperty("expandedMajorSymbolComparisonCreated").GetBoolean());
        Assert.Equal(3, expandedMajor.RootElement.GetProperty("ExistingSymbols").GetArrayLength());
        Assert.Equal(4, expandedMajor.RootElement.GetProperty("ExpandedMajorSymbols").GetArrayLength());
        Assert.Equal(7, expandedMajor.RootElement.GetProperty("comparisons").GetArrayLength());
    }

    [Fact]
    public void Preservation_and_no_external_no_import_no_order_audits_are_clean()
    {
        var inversion = ReadJson("phase-exec-sim-r025-inversion-preservation.json");
        var direct = ReadJson("phase-exec-sim-r025-direct-cross-exclusion-preservation.json");
        var cost = ReadJson("phase-exec-sim-r025-cost-guidance-preservation.json");
        var nonmajor = ReadJson("phase-exec-sim-r025-nonmajor-calibration-preservation.json");
        var noDb = ReadJson("phase-exec-sim-r025-no-db-import-audit.json");
        var noRows = ReadJson("phase-exec-sim-r025-no-persisted-sanitized-row-audit.json");
        var noSchedule = ReadJson("phase-exec-sim-r025-no-executable-schedule-audit.json");
        var noSlices = ReadJson("phase-exec-sim-r025-no-child-slices-audit.json");
        var noOrders = ReadJson("phase-exec-sim-r025-no-child-orders-audit.json");
        var noFill = ReadJson("phase-exec-sim-r025-no-real-fill-audit.json");
        var noReport = ReadJson("phase-exec-sim-r025-no-execution-report-audit.json");
        var noOrderCreated = ReadJson("phase-exec-sim-r025-no-order-created-audit.json");
        var noRoute = ReadJson("phase-exec-sim-r025-no-route-no-submission-audit.json");
        var api = ReadJson("phase-exec-sim-r025-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r025-no-broker-marketdata-runtime-audit.json");
        var usdjpy = ReadJson("phase-exec-sim-r025-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r025-lmax-readonly-baseline-reference.json");
        var noExternal = ReadJson("phase-exec-sim-r025-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r025-forbidden-actions-audit.json");

        Assert.True(inversion.RootElement.GetProperty("usdJpyCaveatPreserved").GetBoolean());
        Assert.False(inversion.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        Assert.True(direct.RootElement.GetProperty("directCrossExclusionPreserved").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("directCrossIncluded").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(nonmajor.RootElement.GetProperty("RequiresLiquidityCalibration").GetBoolean());
        Assert.False(noDb.RootElement.GetProperty("quotesImportedIntoDb").GetBoolean());
        Assert.False(noRows.RootElement.GetProperty("persistedSanitizedQuoteRowsCreated").GetBoolean());
        Assert.False(noSchedule.RootElement.GetProperty("executableSchedulesCreated").GetBoolean());
        Assert.False(noSlices.RootElement.GetProperty("childSlicesCreated").GetBoolean());
        Assert.False(noOrders.RootElement.GetProperty("childOrdersCreated").GetBoolean());
        Assert.False(noFill.RootElement.GetProperty("realFillsCreated").GetBoolean());
        Assert.False(noReport.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(noOrderCreated.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(noRoute.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(noRoute.RootElement.GetProperty("submissionsCreated").GetBoolean());
        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("brokerActivationDetected").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataResponseRead").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("usdjpyCaveatPreserved").GetBoolean());
        Assert.False(usdjpy.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        Assert.True(lmax.RootElement.GetProperty("referenceOnly").GetBoolean());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR025").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("ordersFillsReportsRoutesSubmissionsCreated").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    private static void AssertExpectedSymbolLines(JsonElement[] lines, string executionSymbol, string normalizedSymbol, bool requiresInversion)
    {
        var matching = lines.Where(line => line.GetProperty("ExecutionTradableSymbol").GetString() == executionSymbol).ToArray();
        Assert.Equal(176, matching.Length);
        Assert.All(matching, line =>
        {
            Assert.Equal(normalizedSymbol, line.GetProperty("NormalizedPortfolioSymbol").GetString());
            Assert.Equal(requiresInversion, line.GetProperty("RequiresInversion").GetBoolean());
        });
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] ExpectedSymbols =
    [
        "EURUSD",
        "USDJPY",
        "AUDUSD",
        "GBPUSD",
        "NZDUSD",
        "USDCAD",
        "USDCHF"
    ];

    private static readonly string[] ExpectedPolicies =
    [
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
    ];

    private static readonly string[] RankingArtifacts =
    [
        "phase-exec-sim-r025-ranking-median-slippage.json",
        "phase-exec-sim-r025-ranking-p95-slippage.json",
        "phase-exec-sim-r025-ranking-fill-ratio.json",
        "phase-exec-sim-r025-ranking-residual.json",
        "phase-exec-sim-r025-ranking-spread-paid.json"
    ];

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r025-summary.md",
        "phase-exec-sim-r025-expanded-backtest-execution-contract.json",
        "phase-exec-sim-r025-expanded-backtest-run-result.json",
        "phase-exec-sim-r025-r024-authorization-reference.json",
        "phase-exec-sim-r025-r023-row-validation-reference.json",
        "phase-exec-sim-r025-accepted-rows-used-rejected-rows-excluded.json",
        "phase-exec-sim-r025-quote-windows.json",
        "phase-exec-sim-r025-close-benchmarks.json",
        "phase-exec-sim-r025-feed-quality-results.json",
        "phase-exec-sim-r025-tca-result-line-contract.json",
        "phase-exec-sim-r025-tca-result-lines.json",
        "phase-exec-sim-r025-per-symbol-eurusd-report.json",
        "phase-exec-sim-r025-per-symbol-usdjpy-report.json",
        "phase-exec-sim-r025-per-symbol-audusd-report.json",
        "phase-exec-sim-r025-per-symbol-gbpusd-report.json",
        "phase-exec-sim-r025-per-symbol-nzdusd-report.json",
        "phase-exec-sim-r025-per-symbol-usdcad-report.json",
        "phase-exec-sim-r025-per-symbol-usdchf-report.json",
        "phase-exec-sim-r025-expanded-policy-comparison-report.json",
        "phase-exec-sim-r025-ranking-median-slippage.json",
        "phase-exec-sim-r025-ranking-p95-slippage.json",
        "phase-exec-sim-r025-ranking-fill-ratio.json",
        "phase-exec-sim-r025-ranking-residual.json",
        "phase-exec-sim-r025-ranking-spread-paid.json",
        "phase-exec-sim-r025-wakett-limit-baseline-report.json",
        "phase-exec-sim-r025-wakett-five-market-slices-report.json",
        "phase-exec-sim-r025-passive-until-urgency-report.json",
        "phase-exec-sim-r025-close-seeking-15m-report.json",
        "phase-exec-sim-r025-close-seeking-adaptive-report.json",
        "phase-exec-sim-r025-controlled-residual-cross-report.json",
        "phase-exec-sim-r025-benchmark-only-policy-report.json",
        "phase-exec-sim-r025-expanded-major-symbol-comparison.json",
        "phase-exec-sim-r025-inversion-preservation.json",
        "phase-exec-sim-r025-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r025-cost-guidance-preservation.json",
        "phase-exec-sim-r025-nonmajor-calibration-preservation.json",
        "phase-exec-sim-r025-no-db-import-audit.json",
        "phase-exec-sim-r025-no-persisted-sanitized-row-audit.json",
        "phase-exec-sim-r025-no-executable-schedule-audit.json",
        "phase-exec-sim-r025-no-child-slices-audit.json",
        "phase-exec-sim-r025-no-child-orders-audit.json",
        "phase-exec-sim-r025-no-real-fill-audit.json",
        "phase-exec-sim-r025-no-execution-report-audit.json",
        "phase-exec-sim-r025-no-order-created-audit.json",
        "phase-exec-sim-r025-no-route-no-submission-audit.json",
        "phase-exec-sim-r025-no-polygon-api-call-audit.json",
        "phase-exec-sim-r025-no-lmax-call-audit.json",
        "phase-exec-sim-r025-no-external-api-call-audit.json",
        "phase-exec-sim-r025-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r025-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r025-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r025-no-external-audit.json",
        "phase-exec-sim-r025-forbidden-actions-audit.json",
        "phase-exec-sim-r025-next-phase-recommendation.json"
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
