using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimExpandedOfflineQuoteBatchAuthorizationTests
{
    [Fact]
    public void Required_r021_artifacts_exist_and_authorization_contract_is_ready_safely()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R021 artifact {artifact}");
        }

        var contract = ReadJson("phase-exec-sim-r021-expanded-batch-authorization-contract.json");
        var result = ReadJson("phase-exec-sim-r021-authorization-result.json");

        Assert.True(contract.RootElement.GetProperty("expandedBatchAuthorizationContractCreated").GetBoolean());
        Assert.Equal("EXEC-SIM-R020", contract.RootElement.GetProperty("SourceReadinessPhase").GetString());
        Assert.True(contract.RootElement.GetProperty("AuthorizationOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoValidation").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoImport").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoSanitizedQuoteRowsCreated").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoBacktest").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoSimulation").GetBoolean());
        Assert.Equal("EXEC_SIM_R021_PASS_EXPANDED_OFFLINE_BATCH_AUTHORIZATION_READY_NO_EXTERNAL", contract.RootElement.GetProperty("AuthorizationStatus").GetString());
        Assert.Equal("EXEC_SIM_R021_PASS_EXPANDED_MAJOR_USD_PAIR_PREFLIGHT_READY_NO_EXTERNAL", contract.RootElement.GetProperty("ExpandedMajorUsdPairPreflightStatus").GetString());
        Assert.Equal("EXEC_SIM_R021_PASS_NO_VALIDATION_IMPORT_BACKTEST_GATE_READY_NO_EXTERNAL", contract.RootElement.GetProperty("NoValidationImportBacktestStatus").GetString());
        Assert.Equal("EXEC_SIM_R021_PASS_EXPANDED_OFFLINE_BATCH_AUTHORIZATION_READY_NO_EXTERNAL", result.RootElement.GetProperty("AuthorizationStatus").GetString());
        Assert.True(result.RootElement.GetProperty("AuthorizationReady").GetBoolean());
        Assert.Equal(7, result.RootElement.GetProperty("AcceptedEntryCount").GetInt32());
        Assert.Equal(0, result.RootElement.GetProperty("MissingOrIncompleteEntryCount").GetInt32());
        Assert.False(result.RootElement.GetProperty("MayProceedToValidationImportOrBacktest").GetBoolean());
        Assert.True(result.RootElement.GetProperty("MayProceedToFutureManifestValidationGate").GetBoolean());
    }

    [Fact]
    public void Authorization_request_and_missing_diagnostics_capture_complete_operator_paths()
    {
        var request = ReadJson("phase-exec-sim-r021-expanded-batch-authorization-request.json");
        var accepted = ReadJson("phase-exec-sim-r021-accepted-for-authorization-entries.json");
        var diagnostics = ReadJson("phase-exec-sim-r021-missing-input-diagnostics.json");

        Assert.True(request.RootElement.GetProperty("operatorProvidedEntriesVisibleInRequest").GetBoolean());
        Assert.Equal(7, request.RootElement.GetProperty("operatorProvidedEntryCount").GetInt32());
        Assert.Equal(7, request.RootElement.GetProperty("fileEntries").GetArrayLength());
        Assert.Equal("ReadyForAuthorizationNoExternal", request.RootElement.GetProperty("requestStatus").GetString());
        Assert.Equal(7, accepted.RootElement.GetProperty("acceptedEntryCount").GetInt32());
        Assert.Equal(7, accepted.RootElement.GetProperty("acceptedEntries").GetArrayLength());
        Assert.True(accepted.RootElement.GetProperty("authorizationReady").GetBoolean());
        Assert.False(diagnostics.RootElement.GetProperty("NeedsOperatorFilePaths").GetBoolean());
        Assert.False(diagnostics.RootElement.GetProperty("NeedsOperatorManifests").GetBoolean());
        Assert.False(diagnostics.RootElement.GetProperty("NeedsOperatorSessionWindowCategories").GetBoolean());
        Assert.False(diagnostics.RootElement.GetProperty("NeedsOperatorUtcTimeRanges").GetBoolean());
        Assert.Empty(diagnostics.RootElement.GetProperty("diagnostics").EnumerateArray());

        var requestSymbols = request.RootElement.GetProperty("fileEntries").EnumerateArray().Select(x => x.GetProperty("symbol").GetString()).ToArray();
        var acceptedSymbols = accepted.RootElement.GetProperty("acceptedEntries").EnumerateArray().Select(x => x.GetProperty("symbol").GetString()).ToArray();
        foreach (var symbol in new[] { "EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF" })
        {
            Assert.Contains(symbol, requestSymbols);
            Assert.Contains(symbol, acceptedSymbols);
        }

        Assert.All(accepted.RootElement.GetProperty("acceptedEntries").EnumerateArray(), entry =>
        {
            Assert.True(entry.GetProperty("authorizationOnly").GetBoolean());
            Assert.True(entry.GetProperty("quoteFilePathExistsAtAuthorizationCheck").GetBoolean());
            Assert.True(entry.GetProperty("manifestPathExistsAtAuthorizationCheck").GetBoolean());
            Assert.False(entry.GetProperty("quoteRowsRead").GetBoolean());
            Assert.False(entry.GetProperty("rowContentsValidated").GetBoolean());
            Assert.Equal("IntradayRebalance", entry.GetProperty("SessionWindowCategory").GetString());
        });
    }

    [Fact]
    public void Required_symbols_expanded_symbols_session_categories_and_entry_requirements_are_present()
    {
        var current = ReadJson("phase-exec-sim-r021-required-current-symbols.json");
        var expanded = ReadJson("phase-exec-sim-r021-expanded-major-symbols.json");
        var requirements = ReadJson("phase-exec-sim-r021-file-entry-requirements.json");
        var categories = ReadJson("phase-exec-sim-r021-session-window-category-handling.json");

        Assert.Contains(current.RootElement.GetProperty("symbols").EnumerateArray(), x => x.GetProperty("ExecutionTradableSymbol").GetString() == "EURUSD");
        Assert.Contains(current.RootElement.GetProperty("symbols").EnumerateArray(), x => x.GetProperty("ExecutionTradableSymbol").GetString() == "USDJPY" && x.GetProperty("NormalizedPortfolioSymbol").GetString() == "JPYUSD" && x.GetProperty("RequiresInversion").GetBoolean());
        Assert.Contains(current.RootElement.GetProperty("symbols").EnumerateArray(), x => x.GetProperty("ExecutionTradableSymbol").GetString() == "AUDUSD" && x.GetProperty("Status").GetString() == "RequiredCurrentSymbolNotFailed");

        Assert.Contains(expanded.RootElement.GetProperty("symbols").EnumerateArray(), x => x.GetProperty("ExecutionTradableSymbol").GetString() == "GBPUSD" && !x.GetProperty("RequiresInversion").GetBoolean());
        Assert.Contains(expanded.RootElement.GetProperty("symbols").EnumerateArray(), x => x.GetProperty("ExecutionTradableSymbol").GetString() == "NZDUSD" && !x.GetProperty("RequiresInversion").GetBoolean());
        Assert.Contains(expanded.RootElement.GetProperty("symbols").EnumerateArray(), x => x.GetProperty("ExecutionTradableSymbol").GetString() == "USDCAD" && x.GetProperty("NormalizedPortfolioSymbol").GetString() == "CADUSD" && x.GetProperty("RequiresInversion").GetBoolean());
        Assert.Contains(expanded.RootElement.GetProperty("symbols").EnumerateArray(), x => x.GetProperty("ExecutionTradableSymbol").GetString() == "USDCHF" && x.GetProperty("NormalizedPortfolioSymbol").GetString() == "CHFUSD" && x.GetProperty("RequiresInversion").GetBoolean());

        foreach (var field in new[] { "symbol", "sessionWindowCategory", "quoteFilePath", "manifestPath", "observedRows", "timeRangeStartUtc", "timeRangeEndUtc" })
        {
            AssertContains(requirements, "requiredFields", field);
        }

        foreach (var category in new[] { "OpeningBuild", "IntradayRebalance", "ClosingFlatten", "Mixed", "Unknown" })
        {
            AssertContains(categories, "allowedCategories", category);
        }
    }

    [Fact]
    public void Inversion_direct_cross_cost_nonmajor_usdjpy_and_lmax_preservations_are_safe()
    {
        var inversion = ReadJson("phase-exec-sim-r021-inversion-preservation.json");
        var direct = ReadJson("phase-exec-sim-r021-direct-cross-exclusion-preservation.json");
        var cost = ReadJson("phase-exec-sim-r021-cost-guidance-preservation.json");
        var nonmajor = ReadJson("phase-exec-sim-r021-nonmajor-calibration-preservation.json");
        var usdjpy = ReadJson("phase-exec-sim-r021-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r021-lmax-readonly-baseline-reference.json");

        Assert.True(inversion.RootElement.GetProperty("usdJpyCaveatPreserved").GetBoolean());
        Assert.False(inversion.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        Assert.Contains(inversion.RootElement.GetProperty("symbols").EnumerateArray(), x => x.GetProperty("ExecutionTradableSymbol").GetString() == "USDCAD" && x.GetProperty("NormalizedPortfolioSymbol").GetString() == "CADUSD" && x.GetProperty("RequiresInversion").GetBoolean());
        Assert.Contains(inversion.RootElement.GetProperty("symbols").EnumerateArray(), x => x.GetProperty("ExecutionTradableSymbol").GetString() == "USDCHF" && x.GetProperty("NormalizedPortfolioSymbol").GetString() == "CHFUSD" && x.GetProperty("RequiresInversion").GetBoolean());
        Assert.True(direct.RootElement.GetProperty("directCrossExclusionPreserved").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("guidanceWeakened").GetBoolean());
        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(nonmajor.RootElement.GetProperty("RequiresLiquidityCalibration").GetBoolean());
        Assert.Equal("JPYUSD", usdjpy.RootElement.GetProperty("PortfolioNormalizedSymbol").GetString());
        Assert.Equal("USDJPY", usdjpy.RootElement.GetProperty("ExecutionTradableSymbol").GetString());
        Assert.True(usdjpy.RootElement.GetProperty("RequiresInversion").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.False(usdjpy.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        Assert.True(lmax.RootElement.GetProperty("referenceOnly").GetBoolean());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR021").GetBoolean());
    }

    [Fact]
    public void No_validation_import_backtest_sanitized_rows_api_runtime_or_order_audits_are_clean()
    {
        var noValidation = ReadJson("phase-exec-sim-r021-no-validation-import-backtest-audit.json");
        var noSanitizedRows = ReadJson("phase-exec-sim-r021-no-sanitized-row-audit.json");
        var api = ReadJson("phase-exec-sim-r021-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r021-no-broker-marketdata-runtime-audit.json");
        var order = ReadJson("phase-exec-sim-r021-no-order-fill-report-route-audit.json");
        var noExternal = ReadJson("phase-exec-sim-r021-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r021-forbidden-actions-audit.json");

        Assert.False(noValidation.RootElement.GetProperty("quoteRowsValidated").GetBoolean());
        Assert.False(noValidation.RootElement.GetProperty("quoteFilesValidated").GetBoolean());
        Assert.False(noValidation.RootElement.GetProperty("quotesImported").GetBoolean());
        Assert.False(noValidation.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(noValidation.RootElement.GetProperty("newSimulationExecuted").GetBoolean());
        Assert.False(noValidation.RootElement.GetProperty("tcaResultLinesCreated").GetBoolean());
        Assert.False(noSanitizedRows.RootElement.GetProperty("sanitizedQuoteRowsCreated").GetBoolean());
        Assert.False(noSanitizedRows.RootElement.GetProperty("quoteFixturesCreated").GetBoolean());
        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("brokerActivationDetected").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataResponseRead").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("schedulerServiceTimerPollingBackgroundJobIntroduced").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("quoteFilesDownloaded").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("quoteFilesValidated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("quoteFilesImported").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("sanitizedQuoteRowsCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("ordersFillsReportsRoutesSubmissionsCreated").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r021-summary.md",
        "phase-exec-sim-r021-expanded-batch-authorization-contract.json",
        "phase-exec-sim-r021-expanded-batch-authorization-request.json",
        "phase-exec-sim-r021-expanded-batch-preflight-contract.json",
        "phase-exec-sim-r021-authorization-result.json",
        "phase-exec-sim-r021-required-current-symbols.json",
        "phase-exec-sim-r021-expanded-major-symbols.json",
        "phase-exec-sim-r021-file-entry-requirements.json",
        "phase-exec-sim-r021-accepted-for-authorization-entries.json",
        "phase-exec-sim-r021-missing-input-diagnostics.json",
        "phase-exec-sim-r021-session-window-category-handling.json",
        "phase-exec-sim-r021-inversion-preservation.json",
        "phase-exec-sim-r021-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r021-cost-guidance-preservation.json",
        "phase-exec-sim-r021-nonmajor-calibration-preservation.json",
        "phase-exec-sim-r021-no-validation-import-backtest-audit.json",
        "phase-exec-sim-r021-no-sanitized-row-audit.json",
        "phase-exec-sim-r021-no-polygon-api-call-audit.json",
        "phase-exec-sim-r021-no-lmax-call-audit.json",
        "phase-exec-sim-r021-no-external-api-call-audit.json",
        "phase-exec-sim-r021-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r021-no-order-fill-report-route-audit.json",
        "phase-exec-sim-r021-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r021-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r021-no-external-audit.json",
        "phase-exec-sim-r021-forbidden-actions-audit.json",
        "phase-exec-sim-r021-next-phase-recommendation.json"
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
