using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimFirstRealOfflineQuoteBatchAuthorizationTests
{
    [Fact]
    public void Required_r010_authorization_artifacts_exist()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R010 artifact {artifact}");
        }
    }

    [Fact]
    public void Authorization_contract_request_and_preflight_are_authorization_only()
    {
        var contract = ReadJson("phase-exec-sim-r010-first-batch-authorization-contract.json");
        var request = ReadJson("phase-exec-sim-r010-first-batch-authorization-request.json");
        var preflight = ReadJson("phase-exec-sim-r010-first-batch-preflight-contract.json");

        Assert.True(contract.RootElement.GetProperty("authorizationOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("noApiCall").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("noValidationRun").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("noImport").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("noBacktest").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("noSanitizedQuoteRowsCreated").GetBoolean());
        Assert.Equal("PolygonOfflineFile", request.RootElement.GetProperty("ProviderName").GetString());
        Assert.Equal("HistoricalBboQuotes", request.RootElement.GetProperty("DatasetType").GetString());
        Assert.True(request.RootElement.GetProperty("AuthorizationOnly").GetBoolean());
        Assert.True(request.RootElement.GetProperty("NoApiCall").GetBoolean());
        Assert.True(request.RootElement.GetProperty("NoImport").GetBoolean());
        Assert.True(request.RootElement.GetProperty("NoBacktest").GetBoolean());
        Assert.True(preflight.RootElement.GetProperty("preflightContractReady").GetBoolean());
        Assert.True(preflight.RootElement.GetProperty("noValidationImportBacktestTriggered").GetBoolean());
    }

    [Fact]
    public void Required_symbols_include_eurusd_usdjpy_and_audusd_with_caveats()
    {
        var symbols = ReadJson("phase-exec-sim-r010-required-symbols.json");
        var required = symbols.RootElement.GetProperty("requiredSymbols").EnumerateArray().ToArray();

        Assert.Contains(required, x => x.GetProperty("executionTradableSymbol").GetString() == "EURUSD" && !x.GetProperty("requiresInversion").GetBoolean());
        Assert.Contains(required, x => x.GetProperty("executionTradableSymbol").GetString() == "USDJPY" && x.GetProperty("normalizedPortfolioSymbol").GetString() == "JPYUSD" && x.GetProperty("requiresInversion").GetBoolean());
        Assert.Contains(required, x => x.GetProperty("executionTradableSymbol").GetString() == "AUDUSD" && x.GetProperty("audusdStatus").GetString()!.Contains("not failed"));
        Assert.Contains(required, x => x.TryGetProperty("securityId", out var securityId) && securityId.GetString() == "4004");
        Assert.Contains(required, x => x.TryGetProperty("securityIdSource", out var source) && source.GetString() == "8");
    }

    [Fact]
    public void Supplied_file_paths_and_manifests_authorize_first_batch_without_execution()
    {
        var result = ReadJson("phase-exec-sim-r010-authorization-result.json");
        var diagnostics = ReadJson("phase-exec-sim-r010-missing-input-diagnostics.json");

        Assert.Equal("FirstBatchAuthorizationReadyNoExternal", result.RootElement.GetProperty("AuthorizationStatus").GetString());
        Assert.Equal("EXEC_SIM_R010_PASS_FIRST_REAL_OFFLINE_BATCH_AUTHORIZATION_READY_NO_EXTERNAL", result.RootElement.GetProperty("Classification").GetString());
        Assert.True(result.RootElement.GetProperty("AuthorizationReady").GetBoolean());
        Assert.False(result.RootElement.GetProperty("BlockedSafely").GetBoolean());
        Assert.True(result.RootElement.GetProperty("FileAndManifestPresenceCheckedOnly").GetBoolean());
        Assert.False(result.RootElement.GetProperty("QuoteFileContentsRead").GetBoolean());
        Assert.False(result.RootElement.GetProperty("ManifestContentsReadForValidation").GetBoolean());
        Assert.False(result.RootElement.GetProperty("ValidationRunExecuted").GetBoolean());
        Assert.False(result.RootElement.GetProperty("ImportExecuted").GetBoolean());
        Assert.False(result.RootElement.GetProperty("BacktestExecuted").GetBoolean());
        Assert.False(result.RootElement.GetProperty("SanitizedQuoteRowsCreated").GetBoolean());
        Assert.False(diagnostics.RootElement.GetProperty("r009TemplatesOnlyFound").GetBoolean());
        Assert.False(diagnostics.RootElement.GetProperty("safeStop").GetBoolean());
        Assert.True(diagnostics.RootElement.GetProperty("operatorFilePathsFound").GetBoolean());
        Assert.True(diagnostics.RootElement.GetProperty("operatorManifestPathsOrMetadataFound").GetBoolean());
        Assert.True(diagnostics.RootElement.GetProperty("validationImportBacktestNotAttempted").GetBoolean());

        Assert.Empty(result.RootElement.GetProperty("MissingFileEntries").EnumerateArray());
        Assert.Empty(result.RootElement.GetProperty("MissingManifestEntries").EnumerateArray());
        var entries = result.RootElement.GetProperty("AuthorizedFileEntries").EnumerateArray().ToArray();
        Assert.Contains(entries, x => x.GetProperty("ExecutionTradableSymbol").GetString() == "EURUSD" && x.GetProperty("RowCountDeclared").GetInt32() == 54694);
        Assert.Contains(entries, x => x.GetProperty("ExecutionTradableSymbol").GetString() == "USDJPY" && x.GetProperty("RowCountDeclared").GetInt32() == 59368 && x.GetProperty("RequiresInversion").GetBoolean());
        Assert.Contains(entries, x => x.GetProperty("ExecutionTradableSymbol").GetString() == "AUDUSD" && x.GetProperty("RowCountDeclared").GetInt32() == 60656 && x.GetProperty("AudUsdNotFailedPreserved").GetBoolean());
    }

    [Fact]
    public void Authorization_statuses_and_file_entry_requirements_are_complete()
    {
        var statuses = ReadJson("phase-exec-sim-r010-authorization-statuses.json");
        var requirements = ReadJson("phase-exec-sim-r010-file-entry-requirements.json");

        foreach (var status in new[]
        {
            "FirstBatchAuthorizationReadyNoExternal",
            "FirstBatchAuthorizationBlockedMissingFiles",
            "FirstBatchAuthorizationBlockedMissingManifests",
            "FirstBatchAuthorizationBlockedIncompleteMetadata",
            "FirstBatchAuthorizationBlockedUnsafeSecretRisk",
            "FirstBatchAuthorizationBlockedRawPayloadRisk",
            "FirstBatchAuthorizationBlockedUnsupportedSymbol",
            "FirstBatchAuthorizationBlockedDirectCrossExecution",
            "InconclusiveSafe"
        })
        {
            Assert.Contains(status, statuses.RootElement.GetProperty("authorizationStatuses").EnumerateArray().Select(x => x.GetString()));
        }

        Assert.Contains("QuoteFilePath", requirements.RootElement.GetProperty("fileEntryFields").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("ManifestPath or ManifestMetadata", requirements.RootElement.GetProperty("fileEntryFields").EnumerateArray().Select(x => x.GetString()));
        Assert.True(requirements.RootElement.GetProperty("missingFileReferenceBlocksAuthorization").GetBoolean());
        Assert.True(requirements.RootElement.GetProperty("missingManifestReferenceOrMetadataBlocksAuthorization").GetBoolean());
        Assert.True(requirements.RootElement.GetProperty("secretFlagBlocksAuthorization").GetBoolean());
        Assert.True(requirements.RootElement.GetProperty("rawPayloadFlagBlocksAuthorization").GetBoolean());
        Assert.True(requirements.RootElement.GetProperty("directCrossExecutionFileBlocksAuthorization").GetBoolean());
    }

    [Fact]
    public void Cost_guidance_and_direct_cross_exclusion_are_preserved()
    {
        var cost = ReadJson("phase-exec-sim-r010-cost-guidance-preservation.json");
        var directCross = ReadJson("phase-exec-sim-r010-direct-cross-exclusion-preservation.json");

        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(cost.RootElement.GetProperty("nonMajorEmScandiCnhRequireLiquidityCalibration").GetBoolean());
        Assert.Equal("USD-pair-only", directCross.RootElement.GetProperty("executionUniverse").GetString());
        Assert.True(directCross.RootElement.GetProperty("rawQubesCrossesAreSignalInputsOnly").GetBoolean());
        Assert.True(directCross.RootElement.GetProperty("requiresNettingFirst").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.True(directCross.RootElement.GetProperty("directCrossExecutionDisabledByDefault").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("guidanceWeakened").GetBoolean());
    }

    [Fact]
    public void No_execution_sanitized_row_external_runtime_or_order_audits_are_clean()
    {
        var noExecution = ReadJson("phase-exec-sim-r010-no-validation-import-backtest-execution-audit.json");
        var noRows = ReadJson("phase-exec-sim-r010-no-sanitized-quote-row-creation-audit.json");
        var noExternal = ReadJson("phase-exec-sim-r010-no-external-audit.json");
        var api = ReadJson("phase-exec-sim-r010-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r010-no-broker-marketdata-runtime-audit.json");
        var order = ReadJson("phase-exec-sim-r010-no-order-fill-report-route-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r010-forbidden-actions-audit.json");

        Assert.False(noExecution.RootElement.GetProperty("validationRunExecuted").GetBoolean());
        Assert.False(noExecution.RootElement.GetProperty("importExecuted").GetBoolean());
        Assert.False(noExecution.RootElement.GetProperty("backtestExecuted").GetBoolean());
        Assert.False(noExecution.RootElement.GetProperty("filesProcessed").GetBoolean());
        Assert.False(noRows.RootElement.GetProperty("sanitizedQuoteRowsCreated").GetBoolean());
        Assert.False(noRows.RootElement.GetProperty("quoteWindowsCreated").GetBoolean());
        Assert.False(noRows.RootElement.GetProperty("closeBenchmarksCreated").GetBoolean());
        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("brokerActivationDetected").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("socketOpened").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("automaticExecutionIntroduced").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("fillsCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("executionReportsCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("submissionsCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("rawPayloadSerialized").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("secretMaterialSerialized").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    [Fact]
    public void Usdjpy_lmax_and_audusd_statuses_remain_preserved()
    {
        var usdjpy = ReadJson("phase-exec-sim-r010-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r010-lmax-readonly-baseline-reference.json");

        Assert.True(usdjpy.RootElement.GetProperty("caveatPreserved").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.True(usdjpy.RootElement.GetProperty("requiresInversion").GetBoolean());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR010").GetBoolean());
        Assert.Contains("not failed", lmax.RootElement.GetProperty("audusdStatus").GetString());
        Assert.Contains("SecurityID=4004", lmax.RootElement.GetProperty("usdjpyStatus").GetString());
    }

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r010-summary.md",
        "phase-exec-sim-r010-first-batch-authorization-contract.json",
        "phase-exec-sim-r010-first-batch-authorization-request.json",
        "phase-exec-sim-r010-first-batch-preflight-contract.json",
        "phase-exec-sim-r010-required-symbols.json",
        "phase-exec-sim-r010-file-entry-requirements.json",
        "phase-exec-sim-r010-authorization-statuses.json",
        "phase-exec-sim-r010-authorization-result.json",
        "phase-exec-sim-r010-missing-input-diagnostics.json",
        "phase-exec-sim-r010-cost-guidance-preservation.json",
        "phase-exec-sim-r010-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r010-no-validation-import-backtest-execution-audit.json",
        "phase-exec-sim-r010-no-sanitized-quote-row-creation-audit.json",
        "phase-exec-sim-r010-no-polygon-api-call-audit.json",
        "phase-exec-sim-r010-no-lmax-call-audit.json",
        "phase-exec-sim-r010-no-external-api-call-audit.json",
        "phase-exec-sim-r010-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r010-no-order-fill-report-route-audit.json",
        "phase-exec-sim-r010-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r010-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r010-no-external-audit.json",
        "phase-exec-sim-r010-forbidden-actions-audit.json",
        "phase-exec-sim-r010-next-phase-recommendation.json"
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
