using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimExpandedManifestValidationTests
{
    [Fact]
    public void Required_r022_artifacts_exist_and_contract_consumes_r021_authorization()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R022 artifact {artifact}");
        }

        var contract = ReadJson("phase-exec-sim-r022-manifest-validation-contract.json");
        Assert.True(contract.RootElement.GetProperty("manifestValidationContractCreated").GetBoolean());
        Assert.Equal("EXEC-SIM-R021", contract.RootElement.GetProperty("SourceAuthorizationPhase").GetString());
        Assert.Equal("PolygonOfflineFile", contract.RootElement.GetProperty("ExpectedProviderName").GetString());
        Assert.Equal("HistoricalBboQuotes", contract.RootElement.GetProperty("ExpectedProviderDatasetType").GetString());
        Assert.Equal("NDJSON", contract.RootElement.GetProperty("ExpectedFileFormat").GetString());
        Assert.Equal("IntradayRebalance", contract.RootElement.GetProperty("ExpectedSessionWindowCategory").GetString());
        Assert.True(contract.RootElement.GetProperty("NoRowLevelValidation").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoImport").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoBacktest").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoSimulation").GetBoolean());
        AssertContains(contract, "ValidationStatuses", "ManifestValidationQuarantinedProviderMismatch");
        AssertContains(contract, "ValidationStatuses", "ManifestValidationQuarantinedDatasetMismatch");
        AssertContains(contract, "ValidationStatuses", "ManifestValidationQuarantinedFormatMismatch");
        AssertContains(contract, "ValidationStatuses", "ManifestValidationQuarantinedTimeRangeMismatch");
        AssertContains(contract, "ValidationStatuses", "ManifestValidationQuarantinedSessionCategoryMismatch");
        AssertContains(contract, "ValidationStatuses", "ManifestValidationQuarantinedSecretRisk");
        AssertContains(contract, "ValidationStatuses", "ManifestValidationQuarantinedRawPayloadRisk");
        AssertContains(contract, "ValidationStatuses", "ManifestValidationQuarantinedDirectCrossExecutionDisabled");
    }

    [Fact]
    public void All_seven_authorized_files_and_manifest_results_are_represented()
    {
        var authorized = ReadJson("phase-exec-sim-r022-authorized-files-used.json");
        var manifestResults = ReadJson("phase-exec-sim-r022-manifest-validation-results.json");
        var fileResults = ReadJson("phase-exec-sim-r022-file-level-validation-results.json");
        var accepted = ReadJson("phase-exec-sim-r022-accepted-manifest-validation-outputs.json");
        var quarantined = ReadJson("phase-exec-sim-r022-quarantined-manifest-validation-outputs.json");
        var diagnostics = ReadJson("phase-exec-sim-r022-missing-incomplete-manifest-diagnostics.json");

        Assert.Equal(7, authorized.RootElement.GetProperty("authorizedEntryCount").GetInt32());
        Assert.Equal(7, fileResults.RootElement.GetProperty("resultCount").GetInt32());
        Assert.Equal(7, manifestResults.RootElement.GetProperty("totalManifests").GetInt32());
        Assert.Equal(7, manifestResults.RootElement.GetProperty("acceptedCount").GetInt32());
        Assert.Equal(7, manifestResults.RootElement.GetProperty("acceptedWithWarningsCount").GetInt32());
        Assert.Equal(0, manifestResults.RootElement.GetProperty("quarantinedCount").GetInt32());
        Assert.True(manifestResults.RootElement.GetProperty("allComputedHashesMatchManifest").GetBoolean());
        Assert.True(manifestResults.RootElement.GetProperty("allRowCountsDeclared").GetBoolean());
        Assert.Equal(7, accepted.RootElement.GetProperty("acceptedCount").GetInt32());
        Assert.Equal(0, quarantined.RootElement.GetProperty("quarantinedCount").GetInt32());
        Assert.Equal(0, diagnostics.RootElement.GetProperty("missingManifestCount").GetInt32());
        Assert.Equal(0, diagnostics.RootElement.GetProperty("missingQuoteFileCount").GetInt32());

        var symbols = fileResults.RootElement.GetProperty("results").EnumerateArray().Select(x => x.GetProperty("Symbol").GetString()).ToArray();
        foreach (var symbol in new[] { "EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF" })
        {
            Assert.Contains(symbol, symbols);
        }
    }

    [Fact]
    public void Symbol_specific_manifest_metadata_validates_with_inversion_and_audusd_not_failed()
    {
        var fileResults = ReadJson("phase-exec-sim-r022-file-level-validation-results.json");
        var inversion = ReadJson("phase-exec-sim-r022-symbol-inversion-validation.json");
        var results = fileResults.RootElement.GetProperty("results").EnumerateArray().ToArray();

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
        Assert.True(inversion.RootElement.GetProperty("usdJpyCaveatPreserved").GetBoolean());
        Assert.False(inversion.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
    }

    [Fact]
    public void Session_time_secret_raw_payload_direct_cross_cost_and_nonmajor_preservations_are_safe()
    {
        var session = ReadJson("phase-exec-sim-r022-session-category-validation.json");
        var timeRange = ReadJson("phase-exec-sim-r022-time-range-validation.json");
        var secret = ReadJson("phase-exec-sim-r022-secret-raw-payload-validation.json");
        var direct = ReadJson("phase-exec-sim-r022-direct-cross-exclusion-preservation.json");
        var cost = ReadJson("phase-exec-sim-r022-cost-guidance-preservation.json");
        var nonmajor = ReadJson("phase-exec-sim-r022-nonmajor-calibration-preservation.json");
        var usdjpy = ReadJson("phase-exec-sim-r022-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r022-lmax-readonly-baseline-reference.json");

        Assert.True(session.RootElement.GetProperty("allEntriesIntradayRebalance").GetBoolean());
        Assert.Equal("R021AuthorizationMetadata", session.RootElement.GetProperty("sessionCategorySource").GetString());
        Assert.False(session.RootElement.GetProperty("manifestJsonFieldPresent").GetBoolean());
        Assert.True(timeRange.RootElement.GetProperty("allManifestTimeRangesMatch").GetBoolean());
        Assert.True(secret.RootElement.GetProperty("allContainsSecretsFalse").GetBoolean());
        Assert.True(secret.RootElement.GetProperty("allContainsRawProviderPayloadFalse").GetBoolean());
        Assert.False(secret.RootElement.GetProperty("secretRiskDetected").GetBoolean());
        Assert.False(secret.RootElement.GetProperty("rawPayloadRiskDetected").GetBoolean());
        Assert.True(direct.RootElement.GetProperty("directCrossExclusionPreserved").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("directCrossesInExecutionBatch").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("guidanceWeakened").GetBoolean());
        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(nonmajor.RootElement.GetProperty("RequiresLiquidityCalibration").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("usdjpyCaveatPreserved").GetBoolean());
        Assert.False(usdjpy.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        Assert.True(lmax.RootElement.GetProperty("referenceOnly").GetBoolean());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR022").GetBoolean());
    }

    [Fact]
    public void No_row_validation_sanitized_rows_windows_benchmarks_backtest_tca_api_runtime_or_order_audits_are_clean()
    {
        var row = ReadJson("phase-exec-sim-r022-no-row-level-validation-audit.json");
        var sanitized = ReadJson("phase-exec-sim-r022-no-sanitized-quote-row-creation-audit.json");
        var windows = ReadJson("phase-exec-sim-r022-no-quote-window-close-benchmark-feed-quality-audit.json");
        var backtest = ReadJson("phase-exec-sim-r022-no-backtest-simulation-audit.json");
        var tca = ReadJson("phase-exec-sim-r022-no-tca-result-lines-audit.json");
        var api = ReadJson("phase-exec-sim-r022-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r022-no-broker-marketdata-runtime-audit.json");
        var order = ReadJson("phase-exec-sim-r022-no-order-fill-report-route-audit.json");
        var noExternal = ReadJson("phase-exec-sim-r022-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r022-forbidden-actions-audit.json");

        Assert.False(row.RootElement.GetProperty("quoteRowsReadForValidation").GetBoolean());
        Assert.False(row.RootElement.GetProperty("quoteRowsParsed").GetBoolean());
        Assert.False(row.RootElement.GetProperty("quoteRowsValidated").GetBoolean());
        Assert.False(sanitized.RootElement.GetProperty("sanitizedQuoteRowsCreated").GetBoolean());
        Assert.False(sanitized.RootElement.GetProperty("quotesImported").GetBoolean());
        Assert.False(windows.RootElement.GetProperty("quoteWindowsCreated").GetBoolean());
        Assert.False(windows.RootElement.GetProperty("closeBenchmarksCreated").GetBoolean());
        Assert.False(windows.RootElement.GetProperty("feedQualityResultsCreated").GetBoolean());
        Assert.False(backtest.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(backtest.RootElement.GetProperty("newSimulationExecuted").GetBoolean());
        Assert.False(tca.RootElement.GetProperty("tcaResultLinesProduced").GetBoolean());
        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataResponseRead").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("schedulerServiceTimerPollingBackgroundJobIntroduced").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("quoteRowsValidated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("tcaResultLinesProduced").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("ordersFillsReportsRoutesSubmissionsCreated").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    private static void AssertValidSymbol(JsonElement[] results, string symbol, string normalized, bool requiresInversion)
    {
        var entry = results.Single(x => x.GetProperty("Symbol").GetString() == symbol);
        Assert.True(entry.GetProperty("QuoteFileExists").GetBoolean());
        Assert.True(entry.GetProperty("ManifestExists").GetBoolean());
        Assert.True(entry.GetProperty("ManifestReadable").GetBoolean());
        Assert.Equal("PolygonOfflineFile", entry.GetProperty("ProviderName").GetString());
        Assert.Equal("HistoricalBboQuotes", entry.GetProperty("ProviderDatasetType").GetString());
        Assert.Equal(symbol, entry.GetProperty("ExecutionTradableSymbol").GetString());
        Assert.Equal(normalized, entry.GetProperty("NormalizedPortfolioSymbol").GetString());
        Assert.Equal(requiresInversion, entry.GetProperty("RequiresInversion").GetBoolean());
        Assert.Equal("IntradayRebalance", entry.GetProperty("SessionWindowCategory").GetString());
        Assert.Equal("2026-05-19T12:00:00Z", entry.GetProperty("TimeRangeStartUtc").GetString());
        Assert.Equal("2026-05-19T16:00:00Z", entry.GetProperty("TimeRangeEndUtc").GetString());
        Assert.Equal("NDJSON", entry.GetProperty("FileFormat").GetString());
        Assert.True(entry.GetProperty("FileHashPresent").GetBoolean());
        Assert.True(entry.GetProperty("FileHashMatches").GetBoolean());
        Assert.True(entry.GetProperty("RowCountDeclared").GetInt32() > 0);
        Assert.False(entry.GetProperty("ContainsSecrets").GetBoolean());
        Assert.False(entry.GetProperty("ContainsRawProviderPayload").GetBoolean());
        Assert.Equal("ManifestValidationAcceptedWithWarnings", entry.GetProperty("ValidationStatus").GetString());
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r022-summary.md",
        "phase-exec-sim-r022-manifest-validation-contract.json",
        "phase-exec-sim-r022-authorized-files-used.json",
        "phase-exec-sim-r022-manifest-validation-results.json",
        "phase-exec-sim-r022-file-level-validation-results.json",
        "phase-exec-sim-r022-accepted-manifest-validation-outputs.json",
        "phase-exec-sim-r022-quarantined-manifest-validation-outputs.json",
        "phase-exec-sim-r022-missing-incomplete-manifest-diagnostics.json",
        "phase-exec-sim-r022-symbol-inversion-validation.json",
        "phase-exec-sim-r022-session-category-validation.json",
        "phase-exec-sim-r022-time-range-validation.json",
        "phase-exec-sim-r022-secret-raw-payload-validation.json",
        "phase-exec-sim-r022-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r022-cost-guidance-preservation.json",
        "phase-exec-sim-r022-nonmajor-calibration-preservation.json",
        "phase-exec-sim-r022-no-row-level-validation-audit.json",
        "phase-exec-sim-r022-no-sanitized-quote-row-creation-audit.json",
        "phase-exec-sim-r022-no-quote-window-close-benchmark-feed-quality-audit.json",
        "phase-exec-sim-r022-no-backtest-simulation-audit.json",
        "phase-exec-sim-r022-no-tca-result-lines-audit.json",
        "phase-exec-sim-r022-no-polygon-api-call-audit.json",
        "phase-exec-sim-r022-no-lmax-call-audit.json",
        "phase-exec-sim-r022-no-external-api-call-audit.json",
        "phase-exec-sim-r022-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r022-no-order-fill-report-route-audit.json",
        "phase-exec-sim-r022-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r022-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r022-no-external-audit.json",
        "phase-exec-sim-r022-forbidden-actions-audit.json",
        "phase-exec-sim-r022-next-phase-recommendation.json"
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
