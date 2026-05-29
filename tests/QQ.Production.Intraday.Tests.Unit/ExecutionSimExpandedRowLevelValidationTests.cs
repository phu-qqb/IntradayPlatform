using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimExpandedRowLevelValidationTests
{
    [Fact]
    public void Required_r023_artifacts_exist_and_contract_consumes_r022_results()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R023 artifact {artifact}");
        }

        var contract = ReadJson("phase-exec-sim-r023-row-level-validation-contract.json");
        var used = ReadJson("phase-exec-sim-r023-r022-accepted-files-used.json");

        Assert.True(contract.RootElement.GetProperty("rowLevelValidationContractCreated").GetBoolean());
        Assert.Equal("EXEC-SIM-R022", contract.RootElement.GetProperty("SourceManifestValidationPhase").GetString());
        Assert.Equal("AllAvailable15MinuteClosesWithinAuthorizedTimeRange", contract.RootElement.GetProperty("CoverageMode").GetString());
        Assert.True(contract.RootElement.GetProperty("NoDbImport").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoPersistedSanitizedQuoteRows").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoBacktest").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoSimulation").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoTcaResultLines").GetBoolean());
        AssertContains(contract, "ValidationStatuses", "RowValidationAcceptedWithRejectedRows");
        AssertContains(contract, "ValidationStatuses", "RowValidationQuarantinedRawPayloadRisk");
        AssertContains(contract, "ReadinessStatuses", "QuoteWindowReady");
        AssertContains(contract, "ReadinessStatuses", "CloseBenchmarkAvailable");
        AssertContains(contract, "ReadinessStatuses", "FeedQualityGood");
        Assert.Equal(7, used.RootElement.GetProperty("acceptedFileCount").GetInt32());
    }

    [Fact]
    public void All_seven_symbols_validate_with_expected_inversion_and_row_count_comparison()
    {
        var rowResults = ReadJson("phase-exec-sim-r023-row-level-validation-results.json");
        var counts = ReadJson("phase-exec-sim-r023-row-count-comparison.json");
        var inversion = ReadJson("phase-exec-sim-r023-symbol-inversion-validation.json");

        Assert.Equal(7, rowResults.RootElement.GetProperty("resultCount").GetInt32());
        Assert.True(counts.RootElement.GetProperty("allObservedCountsMatchManifest").GetBoolean());
        Assert.True(inversion.RootElement.GetProperty("allSymbolsPresent").GetBoolean());
        Assert.True(inversion.RootElement.GetProperty("usdJpyCaveatPreserved").GetBoolean());
        Assert.False(inversion.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());

        var results = rowResults.RootElement.GetProperty("results").EnumerateArray().ToArray();
        AssertValidSymbol(results, "EURUSD", "EURUSD", false);
        AssertValidSymbol(results, "USDJPY", "JPYUSD", true);
        AssertValidSymbol(results, "AUDUSD", "AUDUSD", false);
        AssertValidSymbol(results, "GBPUSD", "GBPUSD", false);
        AssertValidSymbol(results, "NZDUSD", "NZDUSD", false);
        AssertValidSymbol(results, "USDCAD", "CADUSD", true);
        AssertValidSymbol(results, "USDCHF", "CHFUSD", true);

        var usdJpy = results.Single(x => x.GetProperty("Symbol").GetString() == "USDJPY");
        Assert.Equal("4004", usdJpy.GetProperty("SecurityID").GetString());
        Assert.Equal("8", usdJpy.GetProperty("SecurityIDSource").GetString());
    }

    [Fact]
    public void Rejected_rows_duplicates_out_of_order_mid_spread_and_raw_payload_handling_are_reported()
    {
        var rejected = ReadJson("phase-exec-sim-r023-rejected-row-summary.json");
        var duplicate = ReadJson("phase-exec-sim-r023-duplicate-out-of-order-handling.json");
        var rowResults = ReadJson("phase-exec-sim-r023-row-level-validation-results.json");

        Assert.True(rejected.RootElement.GetProperty("rejectedRowSummaryCreated").GetBoolean());
        Assert.Equal(7, rejected.RootElement.GetProperty("TotalRejectedRowCount").GetInt32());
        Assert.Equal(7, rejected.RootElement.GetProperty("MalformedJsonRowCount").GetInt32());
        Assert.Equal(0, rejected.RootElement.GetProperty("InvalidBidAskRowCount").GetInt32());
        Assert.Equal(0, rejected.RootElement.GetProperty("AskLessThanBidRowCount").GetInt32());
        Assert.Equal(0, rejected.RootElement.GetProperty("RawPayloadSerializedTrueRowCount").GetInt32());
        Assert.False(rejected.RootElement.GetProperty("rejectedRowsPersisted").GetBoolean());
        Assert.True(duplicate.RootElement.GetProperty("deterministicHandling").GetBoolean());
        Assert.True(duplicate.RootElement.GetProperty("duplicateTimestampTotal").GetInt32() > 0);
        Assert.True(duplicate.RootElement.GetProperty("duplicateRowTotal").GetInt32() > 0);
        Assert.Equal(0, duplicate.RootElement.GetProperty("outOfOrderRowTotal").GetInt32());

        Assert.All(rowResults.RootElement.GetProperty("results").EnumerateArray(), row =>
        {
            Assert.True(row.GetProperty("MidSpreadSpreadBpsDerived").GetBoolean());
            Assert.Equal(0, row.GetProperty("AskLessThanBidRowCount").GetInt32());
            Assert.Equal(0, row.GetProperty("RawPayloadSerializedTrueRowCount").GetInt32());
        });
    }

    [Fact]
    public void Quote_window_close_benchmark_feed_quality_and_session_warning_outputs_exist()
    {
        var windows = ReadJson("phase-exec-sim-r023-quote-window-readiness-results.json");
        var close = ReadJson("phase-exec-sim-r023-close-benchmark-readiness-results.json");
        var feed = ReadJson("phase-exec-sim-r023-feed-quality-readiness-results.json");
        var importReadiness = ReadJson("phase-exec-sim-r023-sanitized-import-readiness-metadata.json");
        var sessionWarning = ReadJson("phase-exec-sim-r023-session-category-warning-preservation.json");

        Assert.Equal("AllAvailable15MinuteClosesWithinAuthorizedTimeRange", windows.RootElement.GetProperty("CoverageMode").GetString());
        Assert.Equal(112, windows.RootElement.GetProperty("evaluatedWindowCount").GetInt32());
        Assert.Equal(112, close.RootElement.GetProperty("resultCount").GetInt32());
        Assert.Equal("LastValidQuoteBeforeClose", close.RootElement.GetProperty("constructionMethod").GetString());
        Assert.Equal(7, feed.RootElement.GetProperty("resultCount").GetInt32());
        Assert.True(importReadiness.RootElement.GetProperty("metadataOnly").GetBoolean());
        Assert.False(importReadiness.RootElement.GetProperty("persistedSanitizedQuoteRowsCreated").GetBoolean());
        Assert.False(importReadiness.RootElement.GetProperty("dbImportOccurred").GetBoolean());
        Assert.True(sessionWarning.RootElement.GetProperty("sessionCategoryWarningPreserved").GetBoolean());
        Assert.Equal("R021AuthorizationMetadata", sessionWarning.RootElement.GetProperty("SessionWindowCategorySource").GetString());
        Assert.False(sessionWarning.RootElement.GetProperty("warningWeakened").GetBoolean());

        Assert.All(feed.RootElement.GetProperty("results").EnumerateArray(), result =>
        {
            Assert.True(result.GetProperty("QuoteCountTMinus13ToClose").GetInt32() > 0);
            Assert.True(result.GetProperty("BenchmarkAvailabilityRatio").GetDouble() > 0.99);
            Assert.Contains(result.GetProperty("FeedQualityBucket").GetString(), new[] { "FeedQualityExcellent", "FeedQualityGood", "FeedQualityUsable" });
        });
    }

    [Fact]
    public void Preservation_and_no_external_no_import_no_order_audits_are_clean()
    {
        var direct = ReadJson("phase-exec-sim-r023-direct-cross-exclusion-preservation.json");
        var cost = ReadJson("phase-exec-sim-r023-cost-guidance-preservation.json");
        var nonmajor = ReadJson("phase-exec-sim-r023-nonmajor-calibration-preservation.json");
        var noDb = ReadJson("phase-exec-sim-r023-no-db-import-audit.json");
        var noSanitized = ReadJson("phase-exec-sim-r023-no-persisted-sanitized-row-audit.json");
        var noBacktest = ReadJson("phase-exec-sim-r023-no-backtest-simulation-audit.json");
        var noTca = ReadJson("phase-exec-sim-r023-no-tca-result-lines-audit.json");
        var api = ReadJson("phase-exec-sim-r023-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r023-no-broker-marketdata-runtime-audit.json");
        var order = ReadJson("phase-exec-sim-r023-no-order-fill-report-route-audit.json");
        var usdjpy = ReadJson("phase-exec-sim-r023-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r023-lmax-readonly-baseline-reference.json");
        var noExternal = ReadJson("phase-exec-sim-r023-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r023-forbidden-actions-audit.json");

        Assert.True(direct.RootElement.GetProperty("directCrossExclusionPreserved").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("guidanceWeakened").GetBoolean());
        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(nonmajor.RootElement.GetProperty("RequiresLiquidityCalibration").GetBoolean());
        Assert.False(noDb.RootElement.GetProperty("quotesImportedIntoDb").GetBoolean());
        Assert.False(noDb.RootElement.GetProperty("dbWriteOccurred").GetBoolean());
        Assert.False(noSanitized.RootElement.GetProperty("persistedSanitizedQuoteRowsCreated").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newSimulationExecuted").GetBoolean());
        Assert.False(noTca.RootElement.GetProperty("tcaResultLinesProduced").GetBoolean());
        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataResponseRead").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("usdjpyCaveatPreserved").GetBoolean());
        Assert.False(usdjpy.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        Assert.True(lmax.RootElement.GetProperty("referenceOnly").GetBoolean());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR023").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("quotesImportedIntoDb").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("persistedSanitizedQuoteRowsCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("ordersFillsReportsRoutesSubmissionsCreated").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    private static void AssertValidSymbol(JsonElement[] results, string symbol, string normalized, bool requiresInversion)
    {
        var row = results.Single(x => x.GetProperty("Symbol").GetString() == symbol);
        Assert.Equal(symbol, row.GetProperty("ExecutionTradableSymbol").GetString());
        Assert.Equal(normalized, row.GetProperty("NormalizedPortfolioSymbol").GetString());
        Assert.Equal(requiresInversion, row.GetProperty("RequiresInversion").GetBoolean());
        Assert.True(row.GetProperty("RowCountDeclared").GetInt32() > 0);
        Assert.True(row.GetProperty("RowCountObserved").GetInt32() > 0);
        Assert.Equal(row.GetProperty("RowCountDeclared").GetInt32(), row.GetProperty("RowCountObserved").GetInt32());
        Assert.True(row.GetProperty("AcceptedRowCount").GetInt32() > 0);
        Assert.Equal("RowValidationAcceptedWithRejectedRows", row.GetProperty("ValidationStatus").GetString());
        Assert.NotNull(row.GetProperty("FirstTimestampUtc").GetString());
        Assert.NotNull(row.GetProperty("LastTimestampUtc").GetString());
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r023-summary.md",
        "phase-exec-sim-r023-row-level-validation-contract.json",
        "phase-exec-sim-r023-r022-accepted-files-used.json",
        "phase-exec-sim-r023-row-level-validation-results.json",
        "phase-exec-sim-r023-row-count-comparison.json",
        "phase-exec-sim-r023-rejected-row-summary.json",
        "phase-exec-sim-r023-duplicate-out-of-order-handling.json",
        "phase-exec-sim-r023-eurusd-row-validation-result.json",
        "phase-exec-sim-r023-usdjpy-row-validation-result.json",
        "phase-exec-sim-r023-audusd-row-validation-result.json",
        "phase-exec-sim-r023-gbpusd-row-validation-result.json",
        "phase-exec-sim-r023-nzdusd-row-validation-result.json",
        "phase-exec-sim-r023-usdcad-row-validation-result.json",
        "phase-exec-sim-r023-usdchf-row-validation-result.json",
        "phase-exec-sim-r023-quote-window-readiness-results.json",
        "phase-exec-sim-r023-close-benchmark-readiness-results.json",
        "phase-exec-sim-r023-feed-quality-readiness-results.json",
        "phase-exec-sim-r023-sanitized-import-readiness-metadata.json",
        "phase-exec-sim-r023-session-category-warning-preservation.json",
        "phase-exec-sim-r023-symbol-inversion-validation.json",
        "phase-exec-sim-r023-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r023-cost-guidance-preservation.json",
        "phase-exec-sim-r023-nonmajor-calibration-preservation.json",
        "phase-exec-sim-r023-no-db-import-audit.json",
        "phase-exec-sim-r023-no-persisted-sanitized-row-audit.json",
        "phase-exec-sim-r023-no-backtest-simulation-audit.json",
        "phase-exec-sim-r023-no-tca-result-lines-audit.json",
        "phase-exec-sim-r023-no-polygon-api-call-audit.json",
        "phase-exec-sim-r023-no-lmax-call-audit.json",
        "phase-exec-sim-r023-no-external-api-call-audit.json",
        "phase-exec-sim-r023-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r023-no-order-fill-report-route-audit.json",
        "phase-exec-sim-r023-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r023-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r023-no-external-audit.json",
        "phase-exec-sim-r023-forbidden-actions-audit.json",
        "phase-exec-sim-r023-next-phase-recommendation.json"
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
