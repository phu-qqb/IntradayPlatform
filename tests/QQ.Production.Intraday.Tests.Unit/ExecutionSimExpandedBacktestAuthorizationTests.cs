using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimExpandedBacktestAuthorizationTests
{
    [Fact]
    public void Required_r024_artifacts_exist_and_contract_is_authorization_only()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R024 artifact {artifact}");
        }

        var contract = ReadJson("phase-exec-sim-r024-expanded-backtest-authorization-contract.json");
        var request = ReadJson("phase-exec-sim-r024-expanded-backtest-authorization-request.json");
        var preflight = ReadJson("phase-exec-sim-r024-expanded-backtest-preflight-contract.json");
        var result = ReadJson("phase-exec-sim-r024-expanded-backtest-authorization-result.json");

        Assert.True(contract.RootElement.GetProperty("expandedBacktestAuthorizationContractCreated").GetBoolean());
        Assert.Equal("EXEC-SIM-R023", contract.RootElement.GetProperty("SourceRowValidationPhase").GetString());
        Assert.Equal("EXEC-SIM-R025", contract.RootElement.GetProperty("IntendedNextPhase").GetString());
        Assert.True(contract.RootElement.GetProperty("AuthorizationOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoApiCall").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoRowRevalidation").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoBacktest").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoSimulation").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoImport").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoPersistedSanitizedQuoteRows").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoTcaResultLines").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoOrdersFillsReportsRoutes").GetBoolean());
        AssertContains(contract, "AuthorizationStatuses", "ExpandedBacktestAuthorizationReadyWithRejectedRowsNoExternal");

        Assert.Equal("EXEC-SIM-R025", request.RootElement.GetProperty("IntendedNextPhase").GetString());
        Assert.Equal(7, request.RootElement.GetProperty("AcceptedValidationResultIds").GetArrayLength());
        Assert.Equal(7, request.RootElement.GetProperty("QuoteWindowReadinessIds").GetArrayLength());
        Assert.Equal(7, request.RootElement.GetProperty("CloseBenchmarkReadinessIds").GetArrayLength());
        Assert.Equal(7, request.RootElement.GetProperty("FeedQualityReadinessIds").GetArrayLength());
        Assert.Equal(7, request.RootElement.GetProperty("SanitizedImportReadinessIds").GetArrayLength());
        Assert.True(preflight.RootElement.GetProperty("expandedBacktestPreflightContractCreated").GetBoolean());
        Assert.Equal("ExpandedBacktestAuthorizationReadyWithRejectedRowsNoExternal", preflight.RootElement.GetProperty("PreflightStatus").GetString());
        Assert.True(result.RootElement.GetProperty("authorizationResultCreated").GetBoolean());
        Assert.Equal("EXEC_SIM_R024_PASS_EXPANDED_BACKTEST_AUTHORIZATION_READY_NO_EXTERNAL", result.RootElement.GetProperty("AuthorizationStatus").GetString());
        AssertContains(result, "AdditionalClassifications", "EXEC_SIM_R024_PASS_EXPANDED_BACKTEST_AUTHORIZATION_WITH_REJECTED_ROWS_NO_EXTERNAL");
    }

    [Fact]
    public void R023_partial_row_validation_is_consumed_and_all_seven_symbols_are_authorized()
    {
        var reference = ReadJson("phase-exec-sim-r024-r023-row-validation-reference.json");
        var authorized = ReadJson("phase-exec-sim-r024-authorized-symbols.json");
        var rows = ReadJson("phase-exec-sim-r024-accepted-rejected-row-summary.json");

        Assert.True(reference.RootElement.GetProperty("r023RowValidationReferenceCreated").GetBoolean());
        Assert.Equal("EXEC-SIM-R023", reference.RootElement.GetProperty("SourcePhase").GetString());
        AssertContains(reference, "R023Classifications", "EXEC_SIM_R023_PARTIAL_ROW_VALIDATION_WITH_REJECTIONS_NO_EXTERNAL");
        Assert.Equal(7, reference.RootElement.GetProperty("rowValidationResultCount").GetInt32());
        Assert.Equal(7, reference.RootElement.GetProperty("totalRejectedRowCount").GetInt32());
        Assert.Equal(7, reference.RootElement.GetProperty("totalMalformedJsonRowCount").GetInt32());
        Assert.False(reference.RootElement.GetProperty("rowRevalidatedInR024").GetBoolean());

        Assert.Equal(7, authorized.RootElement.GetProperty("authorizedSymbolCount").GetInt32());
        AssertExpectedSymbols(authorized.RootElement.GetProperty("authorizedSymbols").EnumerateArray().ToArray());
        Assert.True(rows.RootElement.GetProperty("acceptedRejectedRowSummaryCreated").GetBoolean());
        Assert.True(rows.RootElement.GetProperty("rejectedRowsAcceptedForAuthorization").GetBoolean());
        Assert.Equal(1, rows.RootElement.GetProperty("rejectedRowsPerFile").GetInt32());
        Assert.Equal(7, rows.RootElement.GetProperty("totalRejectedRowCount").GetInt32());
        Assert.Equal(7, rows.RootElement.GetProperty("malformedRejectedRowCount").GetInt32());
        Assert.False(rows.RootElement.GetProperty("rejectedRowsPersisted").GetBoolean());
    }

    [Fact]
    public void Readiness_authorization_artifacts_cover_all_symbols_without_importing_rows()
    {
        var quoteWindows = ReadJson("phase-exec-sim-r024-quote-window-readiness-authorized.json");
        var closeBenchmarks = ReadJson("phase-exec-sim-r024-close-benchmark-readiness-authorized.json");
        var feedQuality = ReadJson("phase-exec-sim-r024-feed-quality-readiness-authorized.json");
        var importReadiness = ReadJson("phase-exec-sim-r024-sanitized-import-readiness-authorized.json");

        Assert.True(quoteWindows.RootElement.GetProperty("quoteWindowReadinessAuthorizedCreated").GetBoolean());
        Assert.Equal(7, quoteWindows.RootElement.GetProperty("symbolCount").GetInt32());
        Assert.Equal(112, quoteWindows.RootElement.GetProperty("evaluatedWindowCount").GetInt32());
        Assert.True(quoteWindows.RootElement.GetProperty("authorized").GetBoolean());
        Assert.Equal("AllAvailable15MinuteClosesWithinAuthorizedTimeRange", quoteWindows.RootElement.GetProperty("coverageMode").GetString());

        Assert.True(closeBenchmarks.RootElement.GetProperty("closeBenchmarkReadinessAuthorizedCreated").GetBoolean());
        Assert.Equal(7, closeBenchmarks.RootElement.GetProperty("symbolCount").GetInt32());
        Assert.Equal(112, closeBenchmarks.RootElement.GetProperty("resultCount").GetInt32());
        Assert.True(closeBenchmarks.RootElement.GetProperty("authorized").GetBoolean());

        Assert.True(feedQuality.RootElement.GetProperty("feedQualityReadinessAuthorizedCreated").GetBoolean());
        Assert.Equal(7, feedQuality.RootElement.GetProperty("symbolCount").GetInt32());
        Assert.Equal(7, feedQuality.RootElement.GetProperty("resultCount").GetInt32());
        Assert.True(feedQuality.RootElement.GetProperty("authorized").GetBoolean());
        Assert.All(feedQuality.RootElement.GetProperty("perSymbol").EnumerateArray(), entry => Assert.True(entry.GetProperty("Authorized").GetBoolean()));

        Assert.True(importReadiness.RootElement.GetProperty("sanitizedImportReadinessAuthorizedCreated").GetBoolean());
        Assert.True(importReadiness.RootElement.GetProperty("metadataOnly").GetBoolean());
        Assert.False(importReadiness.RootElement.GetProperty("persistedSanitizedQuoteRowsCreated").GetBoolean());
        Assert.False(importReadiness.RootElement.GetProperty("dbImportOccurred").GetBoolean());
        Assert.Equal(7, importReadiness.RootElement.GetProperty("authorizedSymbolCount").GetInt32());
    }

    [Fact]
    public void Direct_cross_inversion_cost_nonmajor_and_expected_r025_scope_are_preserved()
    {
        var direct = ReadJson("phase-exec-sim-r024-direct-cross-exclusion-preservation.json");
        var inversion = ReadJson("phase-exec-sim-r024-inversion-preservation.json");
        var policies = ReadJson("phase-exec-sim-r024-expected-r025-policy-list.json");
        var reports = ReadJson("phase-exec-sim-r024-expected-r025-report-list.json");
        var cost = ReadJson("phase-exec-sim-r024-cost-guidance-preservation.json");
        var nonmajor = ReadJson("phase-exec-sim-r024-nonmajor-calibration-preservation.json");
        var usdjpy = ReadJson("phase-exec-sim-r024-usdjpy-caveat-preservation.json");

        Assert.True(direct.RootElement.GetProperty("directCrossExclusionPreserved").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("directCrossIncluded").GetBoolean());
        Assert.True(direct.RootElement.GetProperty("rawQubesCrossesSignalOnly").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("guidanceWeakened").GetBoolean());

        Assert.True(inversion.RootElement.GetProperty("inversionPreservationCreated").GetBoolean());
        Assert.True(inversion.RootElement.GetProperty("usdJpyCaveatPreserved").GetBoolean());
        Assert.False(inversion.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        var validations = inversion.RootElement.GetProperty("validations").EnumerateArray().ToArray();
        AssertSymbol(validations, "USDJPY", "JPYUSD", true);
        AssertSymbol(validations, "USDCAD", "CADUSD", true);
        AssertSymbol(validations, "USDCHF", "CHFUSD", true);
        AssertSymbol(validations, "GBPUSD", "GBPUSD", false);
        AssertSymbol(validations, "NZDUSD", "NZDUSD", false);

        AssertContains(policies, "policies", "WakettPureLimitUntilClose");
        AssertContains(policies, "policies", "CloseSeeking15mAdaptive");
        AssertContains(policies, "policies", "ControlledResidualCross");
        AssertContains(policies, "policies", "VWAPBenchmarkOnly");
        AssertContains(reports, "reports", "PerSymbolTcaReportsForAllSeven");
        AssertContains(reports, "reports", "WakettBaselineComparison");
        AssertContains(reports, "reports", "ExpandedMajorSymbolComparison");
        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(nonmajor.RootElement.GetProperty("RequiresLiquidityCalibration").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("usdjpyCaveatPreserved").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
    }

    [Fact]
    public void No_revalidation_import_backtest_tca_api_runtime_or_order_audits_are_clean()
    {
        var noRowRevalidation = ReadJson("phase-exec-sim-r024-no-row-revalidation-audit.json");
        var noDb = ReadJson("phase-exec-sim-r024-no-db-import-audit.json");
        var noSanitized = ReadJson("phase-exec-sim-r024-no-sanitized-quote-row-creation-audit.json");
        var noBacktest = ReadJson("phase-exec-sim-r024-no-backtest-simulation-audit.json");
        var noTca = ReadJson("phase-exec-sim-r024-no-tca-result-lines-audit.json");
        var api = ReadJson("phase-exec-sim-r024-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r024-no-broker-marketdata-runtime-audit.json");
        var order = ReadJson("phase-exec-sim-r024-no-order-fill-report-route-audit.json");
        var noExternal = ReadJson("phase-exec-sim-r024-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r024-forbidden-actions-audit.json");
        var lmax = ReadJson("phase-exec-sim-r024-lmax-readonly-baseline-reference.json");

        Assert.False(noRowRevalidation.RootElement.GetProperty("quoteRowsValidatedAgain").GetBoolean());
        Assert.False(noRowRevalidation.RootElement.GetProperty("rowValidationReexecuted").GetBoolean());
        Assert.False(noRowRevalidation.RootElement.GetProperty("quoteRowsReadInR024").GetBoolean());
        Assert.False(noDb.RootElement.GetProperty("quotesImportedIntoDb").GetBoolean());
        Assert.False(noDb.RootElement.GetProperty("dbWriteOccurred").GetBoolean());
        Assert.False(noSanitized.RootElement.GetProperty("persistedSanitizedQuoteRowsCreated").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newSimulationExecuted").GetBoolean());
        Assert.False(noTca.RootElement.GetProperty("tcaResultLinesProduced").GetBoolean());
        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("brokerActivationDetected").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("quoteRowsValidatedAgain").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("ordersFillsReportsRoutesSubmissionsCreated").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
        Assert.True(lmax.RootElement.GetProperty("referenceOnly").GetBoolean());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR024").GetBoolean());
    }

    private static void AssertExpectedSymbols(JsonElement[] symbols)
    {
        AssertSymbol(symbols, "EURUSD", "EURUSD", false);
        AssertSymbol(symbols, "USDJPY", "JPYUSD", true);
        AssertSymbol(symbols, "AUDUSD", "AUDUSD", false);
        AssertSymbol(symbols, "GBPUSD", "GBPUSD", false);
        AssertSymbol(symbols, "NZDUSD", "NZDUSD", false);
        AssertSymbol(symbols, "USDCAD", "CADUSD", true);
        AssertSymbol(symbols, "USDCHF", "CHFUSD", true);

        Assert.All(symbols, symbol =>
        {
            Assert.True(symbol.GetProperty("EligibleForR025").GetBoolean());
            Assert.False(symbol.GetProperty("Quarantined").GetBoolean());
            Assert.True(symbol.GetProperty("AcceptedRowCount").GetInt32() > 0);
            Assert.Equal(1, symbol.GetProperty("RejectedRowCount").GetInt32());
            Assert.Equal("RowValidationAcceptedWithRejectedRows", symbol.GetProperty("ValidationStatus").GetString());
        });
    }

    private static void AssertSymbol(JsonElement[] symbols, string executionSymbol, string normalizedSymbol, bool requiresInversion)
    {
        var symbol = symbols.Single(x => x.GetProperty("ExecutionTradableSymbol").GetString() == executionSymbol);
        Assert.Equal(normalizedSymbol, symbol.GetProperty("NormalizedPortfolioSymbol").GetString());
        Assert.Equal(requiresInversion, symbol.GetProperty("RequiresInversion").GetBoolean());
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r024-summary.md",
        "phase-exec-sim-r024-expanded-backtest-authorization-contract.json",
        "phase-exec-sim-r024-expanded-backtest-authorization-request.json",
        "phase-exec-sim-r024-expanded-backtest-preflight-contract.json",
        "phase-exec-sim-r024-expanded-backtest-authorization-result.json",
        "phase-exec-sim-r024-r023-row-validation-reference.json",
        "phase-exec-sim-r024-authorized-symbols.json",
        "phase-exec-sim-r024-accepted-rejected-row-summary.json",
        "phase-exec-sim-r024-quote-window-readiness-authorized.json",
        "phase-exec-sim-r024-close-benchmark-readiness-authorized.json",
        "phase-exec-sim-r024-feed-quality-readiness-authorized.json",
        "phase-exec-sim-r024-sanitized-import-readiness-authorized.json",
        "phase-exec-sim-r024-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r024-inversion-preservation.json",
        "phase-exec-sim-r024-expected-r025-policy-list.json",
        "phase-exec-sim-r024-expected-r025-report-list.json",
        "phase-exec-sim-r024-cost-guidance-preservation.json",
        "phase-exec-sim-r024-nonmajor-calibration-preservation.json",
        "phase-exec-sim-r024-no-row-revalidation-audit.json",
        "phase-exec-sim-r024-no-db-import-audit.json",
        "phase-exec-sim-r024-no-sanitized-quote-row-creation-audit.json",
        "phase-exec-sim-r024-no-backtest-simulation-audit.json",
        "phase-exec-sim-r024-no-tca-result-lines-audit.json",
        "phase-exec-sim-r024-no-polygon-api-call-audit.json",
        "phase-exec-sim-r024-no-lmax-call-audit.json",
        "phase-exec-sim-r024-no-external-api-call-audit.json",
        "phase-exec-sim-r024-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r024-no-order-fill-report-route-audit.json",
        "phase-exec-sim-r024-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r024-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r024-no-external-audit.json",
        "phase-exec-sim-r024-forbidden-actions-audit.json",
        "phase-exec-sim-r024-next-phase-recommendation.json"
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
