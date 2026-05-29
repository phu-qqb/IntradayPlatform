using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimFirstRealOfflineQuoteFileValidationTests
{
    [Fact]
    public void Required_r011_validation_artifacts_exist()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R011 artifact {artifact}");
        }
    }

    [Fact]
    public void R010_authorization_artifacts_are_consumed()
    {
        var authorized = ReadJson("phase-exec-sim-r011-authorized-files-used.json");

        Assert.Equal("EXEC-SIM-R010", authorized.RootElement.GetProperty("sourceAuthorizationPhase").GetString());
        var files = authorized.RootElement.GetProperty("authorizedFilesUsed").EnumerateArray().ToArray();
        Assert.Contains(files, x => x.GetProperty("ExecutionTradableSymbol").GetString() == "EURUSD" && x.GetProperty("ObservedRowsFromDownload").GetInt32() == 54694);
        Assert.Contains(files, x => x.GetProperty("ExecutionTradableSymbol").GetString() == "USDJPY" && x.GetProperty("NormalizedPortfolioSymbol").GetString() == "JPYUSD" && x.GetProperty("RequiresInversion").GetBoolean());
        Assert.Contains(files, x => x.GetProperty("ExecutionTradableSymbol").GetString() == "AUDUSD" && x.GetProperty("AudusdNotFailedPreserved").GetBoolean());
        Assert.False(authorized.RootElement.GetProperty("directCrossExecutionIncluded").GetBoolean());
    }

    [Fact]
    public void File_level_validation_accepts_eurusd_usdjpy_and_audusd()
    {
        var fileLevel = ReadJson("phase-exec-sim-r011-file-level-validation-results.json");
        var results = fileLevel.RootElement.GetProperty("results").EnumerateArray().ToArray();

        AssertAcceptedFile(results, "EURUSD", 54694);
        AssertAcceptedFile(results, "USDJPY", 59368);
        AssertAcceptedFile(results, "AUDUSD", 60656);
    }

    [Fact]
    public void Row_level_validation_checks_timestamps_bidask_derivations_and_raw_payload_safety()
    {
        var rowLevel = ReadJson("phase-exec-sim-r011-row-level-validation-results.json");

        Assert.True(rowLevel.RootElement.GetProperty("bomNormalizationApplied").GetBoolean());
        foreach (var result in rowLevel.RootElement.GetProperty("results").EnumerateArray())
        {
            Assert.Equal(0, result.GetProperty("RejectedRows").GetInt32());
            Assert.Equal(0, result.GetProperty("MissingTimestampRows").GetInt32());
            Assert.Equal(0, result.GetProperty("MissingBidRows").GetInt32());
            Assert.Equal(0, result.GetProperty("MissingAskRows").GetInt32());
            Assert.Equal(0, result.GetProperty("InvalidBidAskRows").GetInt32());
            Assert.Equal(0, result.GetProperty("RawPayloadSerializedRows").GetInt32());
            Assert.Equal(0, result.GetProperty("DerivedMidSpreadMismatchRows").GetInt32());
            Assert.True(result.GetProperty("TimestampParsingValidated").GetBoolean());
            Assert.True(result.GetProperty("BidAskValidationPerformed").GetBoolean());
            Assert.True(result.GetProperty("AskGreaterThanOrEqualBidValidated").GetBoolean());
            Assert.True(result.GetProperty("MidSpreadSpreadBpsDerivationValidated").GetBoolean());
        }
    }

    [Fact]
    public void Accepted_and_quarantine_outputs_are_consistent()
    {
        var accepted = ReadJson("phase-exec-sim-r011-accepted-file-manifests.json");
        var quarantined = ReadJson("phase-exec-sim-r011-quarantined-file-manifests.json");
        var rejected = ReadJson("phase-exec-sim-r011-rejected-row-summary.json");
        var readiness = ReadJson("phase-exec-sim-r011-sanitized-import-readiness-outputs.json");

        Assert.Equal(3, accepted.RootElement.GetProperty("acceptedFileManifests").GetArrayLength());
        Assert.Empty(quarantined.RootElement.GetProperty("quarantinedFileManifests").EnumerateArray());
        Assert.True(quarantined.RootElement.GetProperty("quarantinePathExistsByContract").GetBoolean());
        Assert.True(rejected.RootElement.GetProperty("bomNormalizedRowsAreNotRejected").GetBoolean());
        Assert.False(readiness.RootElement.GetProperty("sanitizedQuoteRowsCreated").GetBoolean());
        Assert.False(readiness.RootElement.GetProperty("importExecuted").GetBoolean());
        foreach (var output in readiness.RootElement.GetProperty("outputs").EnumerateArray())
        {
            Assert.True(output.GetProperty("SanitizedImportReady").GetBoolean());
            Assert.Equal("AcceptedForSanitizedImport", output.GetProperty("ReadinessStatus").GetString());
            Assert.False(output.GetProperty("RawPayloadSerialized").GetBoolean());
        }
    }

    [Fact]
    public void Quote_window_close_benchmark_and_feed_quality_are_ready()
    {
        var quoteWindows = ReadJson("phase-exec-sim-r011-quote-window-readiness-results.json");
        var benchmarks = ReadJson("phase-exec-sim-r011-close-benchmark-readiness-results.json");
        var feed = ReadJson("phase-exec-sim-r011-feed-quality-readiness-results.json");

        foreach (var result in quoteWindows.RootElement.GetProperty("results").EnumerateArray())
        {
            Assert.Equal("Ready", result.GetProperty("FeedWindowStatus").GetString());
            Assert.True(result.GetProperty("QuoteCount").GetInt32() > 0);
            Assert.True(result.GetProperty("QuoteCountLastMinute").GetInt32() > 0);
            Assert.True(result.GetProperty("LastQuoteAgeAtCloseSeconds").GetDouble() <= 1);
        }

        foreach (var result in benchmarks.RootElement.GetProperty("results").EnumerateArray())
        {
            Assert.Equal("Available", result.GetProperty("CloseBenchmarkStatus").GetString());
            Assert.Equal("LastValidQuoteBeforeClose", result.GetProperty("CloseConstructionMethod").GetString());
            Assert.True(result.GetProperty("LastValidBidBeforeClose").GetDouble() > 0);
            Assert.True(result.GetProperty("LastValidAskBeforeClose").GetDouble() >= result.GetProperty("LastValidBidBeforeClose").GetDouble());
        }

        foreach (var result in feed.RootElement.GetProperty("results").EnumerateArray())
        {
            Assert.Equal("Good", result.GetProperty("FeedQualityBucket").GetString());
            Assert.False(result.GetProperty("GapNearCloseFlag").GetBoolean());
            Assert.False(result.GetProperty("StaleNearCloseFlag").GetBoolean());
            Assert.False(result.GetProperty("SpreadWideNearCloseFlag").GetBoolean());
        }
    }

    [Fact]
    public void Row_counts_duplicates_and_out_of_order_handling_are_recorded()
    {
        var counts = ReadJson("phase-exec-sim-r011-row-count-comparison.json");
        var duplicate = ReadJson("phase-exec-sim-r011-duplicate-out-of-order-handling.json");

        foreach (var comparison in counts.RootElement.GetProperty("comparisons").EnumerateArray())
        {
            Assert.True(comparison.GetProperty("RowCountMatches").GetBoolean());
            Assert.False(comparison.GetProperty("RowCountMismatchClassified").GetBoolean());
        }

        foreach (var result in duplicate.RootElement.GetProperty("results").EnumerateArray())
        {
            Assert.True(result.GetProperty("DuplicateRows").GetInt32() > 0);
            Assert.True(result.GetProperty("DuplicateTimestamps").GetInt32() > 0);
            Assert.Equal(0, result.GetProperty("OutOfOrderRows").GetInt32());
            Assert.Equal("RecordedDeterministically", result.GetProperty("DuplicateHandlingStatus").GetString());
        }
    }

    [Fact]
    public void No_backtest_no_api_no_runtime_and_no_order_audits_are_clean()
    {
        var noBacktest = ReadJson("phase-exec-sim-r011-no-backtest-execution-audit.json");
        var noTca = ReadJson("phase-exec-sim-r011-no-imported-tca-policy-results-audit.json");
        var api = ReadJson("phase-exec-sim-r011-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r011-no-broker-marketdata-runtime-audit.json");
        var order = ReadJson("phase-exec-sim-r011-no-order-fill-report-route-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r011-forbidden-actions-audit.json");

        Assert.False(noBacktest.RootElement.GetProperty("backtestExecuted").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("tcaReportsProduced").GetBoolean());
        Assert.False(noTca.RootElement.GetProperty("importedQuoteTcaPolicyResultsProduced").GetBoolean());
        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("brokerActivationDetected").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("socketOpened").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("fillsCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("executionReportsCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("submissionsCreated").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    [Fact]
    public void Usdjpy_audusd_cost_and_direct_cross_preservations_hold()
    {
        var usdjpy = ReadJson("phase-exec-sim-r011-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r011-lmax-readonly-baseline-reference.json");
        var cost = ReadJson("phase-exec-sim-r011-cost-guidance-preservation.json");
        var directCross = ReadJson("phase-exec-sim-r011-direct-cross-exclusion-preservation.json");

        Assert.True(usdjpy.RootElement.GetProperty("caveatPreserved").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.True(usdjpy.RootElement.GetProperty("requiresInversion").GetBoolean());
        Assert.Contains("not failed", lmax.RootElement.GetProperty("audusdStatus").GetString());
        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.Equal("USD-pair-only", directCross.RootElement.GetProperty("executionUniverse").GetString());
        Assert.False(directCross.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("guidanceWeakened").GetBoolean());
    }

    private static void AssertAcceptedFile(JsonElement[] results, string symbol, int rows)
    {
        var result = results.Single(x => x.GetProperty("ExecutionTradableSymbol").GetString() == symbol);
        Assert.True(result.GetProperty("FileExists").GetBoolean());
        Assert.True(result.GetProperty("ManifestExists").GetBoolean());
        Assert.True(result.GetProperty("HashMatchesManifest").GetBoolean());
        Assert.True(result.GetProperty("RowCountMatches").GetBoolean());
        Assert.Equal(rows, result.GetProperty("ObservedRows").GetInt32());
        Assert.False(result.GetProperty("ContainsSecrets").GetBoolean());
        Assert.False(result.GetProperty("ContainsRawProviderPayload").GetBoolean());
        Assert.Equal("AcceptedForSanitizedImport", result.GetProperty("ValidationStatus").GetString());
    }

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r011-summary.md",
        "phase-exec-sim-r011-validation-contract.json",
        "phase-exec-sim-r011-authorized-files-used.json",
        "phase-exec-sim-r011-file-level-validation-results.json",
        "phase-exec-sim-r011-row-level-validation-results.json",
        "phase-exec-sim-r011-accepted-file-manifests.json",
        "phase-exec-sim-r011-quarantined-file-manifests.json",
        "phase-exec-sim-r011-rejected-row-summary.json",
        "phase-exec-sim-r011-sanitized-import-readiness-outputs.json",
        "phase-exec-sim-r011-eurusd-validation-result.json",
        "phase-exec-sim-r011-usdjpy-validation-result.json",
        "phase-exec-sim-r011-audusd-validation-result.json",
        "phase-exec-sim-r011-quote-window-readiness-results.json",
        "phase-exec-sim-r011-close-benchmark-readiness-results.json",
        "phase-exec-sim-r011-feed-quality-readiness-results.json",
        "phase-exec-sim-r011-row-count-comparison.json",
        "phase-exec-sim-r011-duplicate-out-of-order-handling.json",
        "phase-exec-sim-r011-cost-guidance-preservation.json",
        "phase-exec-sim-r011-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r011-no-backtest-execution-audit.json",
        "phase-exec-sim-r011-no-imported-tca-policy-results-audit.json",
        "phase-exec-sim-r011-no-polygon-api-call-audit.json",
        "phase-exec-sim-r011-no-lmax-call-audit.json",
        "phase-exec-sim-r011-no-external-api-call-audit.json",
        "phase-exec-sim-r011-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r011-no-order-fill-report-route-audit.json",
        "phase-exec-sim-r011-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r011-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r011-no-external-audit.json",
        "phase-exec-sim-r011-forbidden-actions-audit.json",
        "phase-exec-sim-r011-next-phase-recommendation.json"
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
