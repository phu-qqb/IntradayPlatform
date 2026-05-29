using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimExpandedOfflineQuoteBatchReadinessTests
{
    [Fact]
    public void Required_r020_artifacts_exist_and_contract_is_readiness_only()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R020 artifact {artifact}");
        }

        var contract = ReadJson("phase-exec-sim-r020-expanded-batch-readiness-contract.json");
        Assert.True(contract.RootElement.GetProperty("expandedBatchReadinessContractCreated").GetBoolean());
        Assert.Equal("EXEC-SIM-R019", contract.RootElement.GetProperty("SourceRecommendationPhase").GetString());
        Assert.True(contract.RootElement.GetProperty("AuthorizationOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoDownload").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoValidation").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoImport").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoBacktest").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoOrdersFillsReportsRoutes").GetBoolean());
        AssertContains(contract, "RequiredExistingSymbols", "EURUSD");
        AssertContains(contract, "RequiredNewSymbols", "USDCAD");
        AssertContains(contract, "RequiredWindowCategories", "ClosingFlatten");
        AssertContains(contract, "RequiredManifestFields", "SessionWindowCategory");
    }

    [Fact]
    public void Required_recommended_and_deferred_symbols_are_correct()
    {
        var existing = ReadJson("phase-exec-sim-r020-required-existing-symbols.json");
        var major = ReadJson("phase-exec-sim-r020-recommended-major-expansion-symbols.json");
        var deferred = ReadJson("phase-exec-sim-r020-deferred-calibration-symbols.json");
        var inversion = ReadJson("phase-exec-sim-r020-inversion-guidance.json");

        Assert.True(existing.RootElement.GetProperty("requiredExistingSymbolsCreated").GetBoolean());
        Assert.Contains(existing.RootElement.GetProperty("symbols").EnumerateArray(), x => x.GetProperty("ExecutionTradableSymbol").GetString() == "EURUSD");
        Assert.Contains(existing.RootElement.GetProperty("symbols").EnumerateArray(), x =>
            x.GetProperty("ExecutionTradableSymbol").GetString() == "USDJPY" &&
            x.GetProperty("NormalizedPortfolioSymbol").GetString() == "JPYUSD" &&
            x.GetProperty("RequiresInversion").GetBoolean());
        Assert.Contains(existing.RootElement.GetProperty("symbols").EnumerateArray(), x =>
            x.GetProperty("ExecutionTradableSymbol").GetString() == "AUDUSD" &&
            x.GetProperty("Status").GetString() == "not failed");

        Assert.True(major.RootElement.GetProperty("recommendedMajorExpansionSymbolsCreated").GetBoolean());
        Assert.Contains(major.RootElement.GetProperty("symbols").EnumerateArray(), x => x.GetProperty("ExecutionTradableSymbol").GetString() == "GBPUSD" && !x.GetProperty("RequiresInversion").GetBoolean());
        Assert.Contains(major.RootElement.GetProperty("symbols").EnumerateArray(), x => x.GetProperty("ExecutionTradableSymbol").GetString() == "NZDUSD" && !x.GetProperty("RequiresInversion").GetBoolean());
        Assert.Contains(major.RootElement.GetProperty("symbols").EnumerateArray(), x => x.GetProperty("ExecutionTradableSymbol").GetString() == "USDCAD" && x.GetProperty("NormalizedPortfolioSymbol").GetString() == "CADUSD" && x.GetProperty("RequiresInversion").GetBoolean());
        Assert.Contains(major.RootElement.GetProperty("symbols").EnumerateArray(), x => x.GetProperty("ExecutionTradableSymbol").GetString() == "USDCHF" && x.GetProperty("NormalizedPortfolioSymbol").GetString() == "CHFUSD" && x.GetProperty("RequiresInversion").GetBoolean());

        Assert.True(deferred.RootElement.GetProperty("requiresLiquidityCalibration").GetBoolean());
        AssertContains(deferred, "deferredCategories", "EM");
        AssertContains(deferred, "deferredSymbols", "USDCNH");
        Assert.True(inversion.RootElement.GetProperty("usdJpyCaveatPreserved").GetBoolean());
        Assert.True(inversion.RootElement.GetProperty("cadChfInversionGuidancePresent").GetBoolean());
    }

    [Fact]
    public void Session_window_historical_manifest_file_naming_and_operator_guidance_are_complete()
    {
        var session = ReadJson("phase-exec-sim-r020-session-window-category-requirements.json");
        var historical = ReadJson("phase-exec-sim-r020-historical-window-requirements.json");
        var manifest = ReadJson("phase-exec-sim-r020-manifest-requirements.json");
        var naming = ReadJson("phase-exec-sim-r020-file-naming-guidance.json");
        var operatorGuidance = ReadJson("phase-exec-sim-r020-operator-download-guidance.json");
        var needsInput = ReadJson("phase-exec-sim-r020-needs-operator-input.json");

        Assert.True(File.Exists(Path.Combine(ArtifactsDir(), "phase-exec-sim-r020-operator-download-guidance.md")));
        AssertContains(session, "requiredCategories", "OpeningBuild");
        AssertContains(session, "requiredCategories", "IntradayRebalance");
        AssertContains(session, "requiredCategories", "ClosingFlatten");
        Assert.Equal("NeedsOperatorInput", session.RootElement.GetProperty("OpeningBuild").GetProperty("exactSessionTimeStatus").GetString());
        Assert.True(historical.RootElement.GetProperty("requiresMoreThanOneFourHourWindow").GetBoolean());
        Assert.True(historical.RootElement.GetProperty("requiresMultipleDatesNotSingleDayOnly").GetBoolean());
        Assert.Equal("NeedsOperatorInput", historical.RootElement.GetProperty("exactDateRangesStatus").GetString());
        Assert.Equal("NeedsOperatorInput", historical.RootElement.GetProperty("exactSessionTimesStatus").GetString());
        AssertContains(manifest, "requiredFields", "SessionWindowCategory");
        Assert.True(manifest.RootElement.GetProperty("containsRawProviderPayloadMustBeFalse").GetBoolean());
        Assert.True(manifest.RootElement.GetProperty("containsSecretsMustBeFalse").GetBoolean());
        Assert.Contains("eurusd-", naming.RootElement.GetProperty("examples").EnumerateArray().First().GetString());
        Assert.True(operatorGuidance.RootElement.GetProperty("codexMustNotCallPolygon").GetBoolean());
        Assert.True(operatorGuidance.RootElement.GetProperty("apiKeyEnvironmentVariableOnly").GetBoolean());
        Assert.False(operatorGuidance.RootElement.GetProperty("apiKeyInRepoAllowed").GetBoolean());
        Assert.True(needsInput.RootElement.GetProperty("NeedsOperatorInput").GetBoolean());
        Assert.Contains(needsInput.RootElement.GetProperty("items").EnumerateArray(), x => x.GetProperty("Input").GetString() == "Exact date ranges");
    }

    [Fact]
    public void Direct_cross_cost_statuses_usdjpy_and_lmax_preservations_are_safe()
    {
        var direct = ReadJson("phase-exec-sim-r020-direct-cross-exclusion-preservation.json");
        var cost = ReadJson("phase-exec-sim-r020-cost-guidance-preservation.json");
        var statuses = ReadJson("phase-exec-sim-r020-readiness-statuses.json");
        var usdjpy = ReadJson("phase-exec-sim-r020-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r020-lmax-readonly-baseline-reference.json");

        Assert.True(direct.RootElement.GetProperty("directCrossExclusionPreserved").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("guidanceWeakened").GetBoolean());
        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(cost.RootElement.GetProperty("nonmajorEmScandiCnhRequireLiquidityCalibration").GetBoolean());
        AssertContains(statuses, "statuses", "ExpandedBatchNeedsOperatorDateRanges");
        AssertContains(statuses, "statuses", "ExpandedBatchNeedsSessionTimes");
        Assert.Equal("ExpandedBatchNeedsOperatorDateRanges", statuses.RootElement.GetProperty("currentSafeStatus").GetString());
        Assert.Equal("JPYUSD", usdjpy.RootElement.GetProperty("PortfolioNormalizedSymbol").GetString());
        Assert.True(usdjpy.RootElement.GetProperty("RequiresInversion").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.False(usdjpy.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        Assert.True(lmax.RootElement.GetProperty("referenceOnly").GetBoolean());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR020").GetBoolean());
        Assert.Contains("not failed", lmax.RootElement.GetProperty("audusdStatus").GetString());
    }

    [Fact]
    public void No_download_validation_import_backtest_api_runtime_or_order_audits_are_clean()
    {
        var noDownload = ReadJson("phase-exec-sim-r020-no-download-audit.json");
        var noValidation = ReadJson("phase-exec-sim-r020-no-validation-audit.json");
        var noImport = ReadJson("phase-exec-sim-r020-no-import-audit.json");
        var noBacktest = ReadJson("phase-exec-sim-r020-no-backtest-simulation-audit.json");
        var order = ReadJson("phase-exec-sim-r020-no-order-fill-report-route-audit.json");
        var api = ReadJson("phase-exec-sim-r020-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r020-no-broker-marketdata-runtime-audit.json");
        var noExternal = ReadJson("phase-exec-sim-r020-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r020-forbidden-actions-audit.json");

        Assert.False(noDownload.RootElement.GetProperty("quoteFilesDownloaded").GetBoolean());
        Assert.False(noValidation.RootElement.GetProperty("quoteFilesValidated").GetBoolean());
        Assert.False(noImport.RootElement.GetProperty("quoteFilesImported").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newSimulationExecuted").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("brokerActivationDetected").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("socketOpened").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("quoteFilesDownloaded").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("quoteFilesValidated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("quoteFilesImported").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("newSimulationExecuted").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("ordersFillsReportsRoutesSubmissionsCreated").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r020-summary.md",
        "phase-exec-sim-r020-expanded-batch-readiness-contract.json",
        "phase-exec-sim-r020-expanded-batch-scope.json",
        "phase-exec-sim-r020-required-existing-symbols.json",
        "phase-exec-sim-r020-recommended-major-expansion-symbols.json",
        "phase-exec-sim-r020-deferred-calibration-symbols.json",
        "phase-exec-sim-r020-session-window-category-requirements.json",
        "phase-exec-sim-r020-historical-window-requirements.json",
        "phase-exec-sim-r020-manifest-requirements.json",
        "phase-exec-sim-r020-file-naming-guidance.json",
        "phase-exec-sim-r020-operator-download-guidance.md",
        "phase-exec-sim-r020-operator-download-guidance.json",
        "phase-exec-sim-r020-inversion-guidance.json",
        "phase-exec-sim-r020-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r020-cost-guidance-preservation.json",
        "phase-exec-sim-r020-readiness-statuses.json",
        "phase-exec-sim-r020-needs-operator-input.json",
        "phase-exec-sim-r020-no-download-audit.json",
        "phase-exec-sim-r020-no-validation-audit.json",
        "phase-exec-sim-r020-no-import-audit.json",
        "phase-exec-sim-r020-no-backtest-simulation-audit.json",
        "phase-exec-sim-r020-no-order-fill-report-route-audit.json",
        "phase-exec-sim-r020-no-polygon-api-call-audit.json",
        "phase-exec-sim-r020-no-lmax-call-audit.json",
        "phase-exec-sim-r020-no-external-api-call-audit.json",
        "phase-exec-sim-r020-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r020-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r020-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r020-no-external-audit.json",
        "phase-exec-sim-r020-forbidden-actions-audit.json",
        "phase-exec-sim-r020-next-phase-recommendation.json"
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
