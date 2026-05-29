using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimHistoricalWindowExpansionAuthorizationTests
{
    [Fact]
    public void Required_r027_artifacts_exist_and_reference_r026_decision()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R027 artifact {artifact}");
        }

        var reference = ReadJson("phase-exec-sim-r027-r026-data-expansion-decision-reference.json");
        var contract = ReadJson("phase-exec-sim-r027-historical-window-expansion-authorization-contract.json");
        var request = ReadJson("phase-exec-sim-r027-historical-window-expansion-request.json");
        var preflight = ReadJson("phase-exec-sim-r027-historical-window-expansion-preflight-contract.json");

        Assert.True(reference.RootElement.GetProperty("r026DataExpansionDecisionReferenceCreated").GetBoolean());
        Assert.Equal("EXEC-SIM-R026", reference.RootElement.GetProperty("SourceDecisionPhase").GetString());
        Assert.True(reference.RootElement.GetProperty("R026OpeningClosingWindowsRecommended").GetBoolean());
        AssertContains(reference, "R026RequiredCoverage", "OpeningBuild windows");
        AssertContains(reference, "R026RequiredCoverage", "ClosingFlatten windows");

        Assert.True(contract.RootElement.GetProperty("historicalWindowExpansionAuthorizationContractCreated").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("AuthorizationOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoExternalApiCalls").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoDownload").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoRowValidation").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoDbImport").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoBacktest").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoSimulation").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoTcaResultLines").GetBoolean());
        AssertContains(contract, "AuthorizationStatuses", "HistoricalWindowExpansionBlockedMissingFiles");
        AssertContains(contract, "AuthorizationStatuses", "HistoricalWindowExpansionNeedsOperatorDateRangesOrSessionTimes");

        Assert.True(request.RootElement.GetProperty("historicalWindowExpansionRequestCreated").GetBoolean());
        Assert.True(request.RootElement.GetProperty("OperatorFileEntriesSupplied").GetBoolean());
        Assert.False(request.RootElement.GetProperty("OperatorPlaceholdersSupplied").GetBoolean());
        Assert.Equal(14, request.RootElement.GetProperty("OperatorFileEntryCount").GetInt32());
        Assert.Equal("EXEC-SIM-R028", request.RootElement.GetProperty("IntendedNextPhaseIfFilesSupplied").GetString());

        Assert.True(preflight.RootElement.GetProperty("historicalWindowExpansionPreflightContractCreated").GetBoolean());
        Assert.True(preflight.RootElement.GetProperty("PathPresenceCheckOnlyWhenSupplied").GetBoolean());
        Assert.True(preflight.RootElement.GetProperty("ManifestContentValidationDeferredToR028").GetBoolean());
        Assert.True(preflight.RootElement.GetProperty("QuoteRowValidationDeferredPastR028").GetBoolean());
    }

    [Fact]
    public void Required_symbols_session_categories_and_file_entry_requirements_are_complete()
    {
        var symbols = ReadJson("phase-exec-sim-r027-required-symbols.json");
        var categories = ReadJson("phase-exec-sim-r027-required-session-window-categories.json");
        var requirements = ReadJson("phase-exec-sim-r027-file-entry-requirements.json");

        Assert.True(symbols.RootElement.GetProperty("requiredSymbolsCreated").GetBoolean());
        Assert.Equal(7, symbols.RootElement.GetProperty("RequiredSymbolCount").GetInt32());
        AssertSymbol(symbols, "EURUSD", "EURUSD", false);
        AssertSymbol(symbols, "USDJPY", "JPYUSD", true);
        AssertSymbol(symbols, "AUDUSD", "AUDUSD", false);
        AssertSymbol(symbols, "GBPUSD", "GBPUSD", false);
        AssertSymbol(symbols, "NZDUSD", "NZDUSD", false);
        AssertSymbol(symbols, "USDCAD", "CADUSD", true);
        AssertSymbol(symbols, "USDCHF", "CHFUSD", true);
        Assert.Equal("not failed", symbols.RootElement.GetProperty("AudUsdStatus").GetString());

        Assert.True(categories.RootElement.GetProperty("requiredSessionWindowCategoriesCreated").GetBoolean());
        AssertContains(categories, "RequiredCategories", "OpeningBuild");
        AssertContains(categories, "RequiredCategories", "ClosingFlatten");
        AssertContains(categories, "OptionalSupportedCategories", "IntradayRebalance");
        AssertContains(categories, "OptionalSupportedCategories", "Mixed");
        AssertContains(categories, "OptionalSupportedCategories", "Unknown");
        Assert.True(categories.RootElement.GetProperty("OpeningBuildRequiredOrRequested").GetBoolean());
        Assert.True(categories.RootElement.GetProperty("ClosingFlattenRequiredOrRequested").GetBoolean());
        Assert.True(categories.RootElement.GetProperty("NeedsOperatorDateRangesOrSessionTimes").GetBoolean());

        Assert.True(requirements.RootElement.GetProperty("fileEntryRequirementsCreated").GetBoolean());
        foreach (var field in RequiredFileEntryFields)
        {
            AssertContains(requirements, "RequiredFields", field);
        }
        Assert.Equal("NDJSON", requirements.RootElement.GetProperty("FileFormat").GetString());
        Assert.Equal("PolygonOfflineFile", requirements.RootElement.GetProperty("ProviderName").GetString());
        Assert.Equal("HistoricalBboQuotes", requirements.RootElement.GetProperty("ProviderDatasetType").GetString());
        Assert.True(requirements.RootElement.GetProperty("ContainsSecretsMustBeFalse").GetBoolean());
        Assert.True(requirements.RootElement.GetProperty("ContainsRawProviderPayloadMustBeFalse").GetBoolean());
        Assert.False(requirements.RootElement.GetProperty("DirectCrossesAllowed").GetBoolean());
    }

    [Fact]
    public void Concrete_operator_paths_and_manifests_are_authorized_for_r028_when_present()
    {
        var result = ReadJson("phase-exec-sim-r027-authorization-result.json");
        var accepted = ReadJson("phase-exec-sim-r027-accepted-for-authorization-entries.json");
        var diagnostics = ReadJson("phase-exec-sim-r027-missing-input-diagnostics.json");

        Assert.True(result.RootElement.GetProperty("authorizationResultCreated").GetBoolean());
        Assert.Equal("HistoricalWindowExpansionAuthorizationReadyNoExternal", result.RootElement.GetProperty("AuthorizationStatus").GetString());
        Assert.Equal("HistoricalWindowExpansionSessionWindowPreflightReadyNoExternal", result.RootElement.GetProperty("AdditionalStatus").GetString());
        AssertContains(result, "Classifications", "EXEC_SIM_R027_PASS_HISTORICAL_WINDOW_EXPANSION_AUTHORIZATION_READY_NO_EXTERNAL");
        AssertContains(result, "Classifications", "EXEC_SIM_R027_PASS_SESSION_WINDOW_EXPANSION_PREFLIGHT_READY_NO_EXTERNAL");
        AssertContains(result, "Classifications", "EXEC_SIM_R027_PASS_NO_DOWNLOAD_NO_BACKTEST_GATE_READY_NO_EXTERNAL");
        Assert.Equal(14, result.RootElement.GetProperty("AuthorizedEntryCount").GetInt32());
        Assert.Equal(0, result.RootElement.GetProperty("BlockedEntryCount").GetInt32());
        Assert.True(result.RootElement.GetProperty("OperatorFileEntriesSupplied").GetBoolean());
        Assert.False(result.RootElement.GetProperty("OperatorPlaceholdersSupplied").GetBoolean());
        Assert.False(result.RootElement.GetProperty("SafeBlocked").GetBoolean());
        Assert.True(result.RootElement.GetProperty("ReadyForR028").GetBoolean());

        Assert.True(accepted.RootElement.GetProperty("acceptedForAuthorizationEntriesCreated").GetBoolean());
        Assert.Equal(14, accepted.RootElement.GetProperty("AcceptedEntryCount").GetInt32());
        var entries = accepted.RootElement.GetProperty("Entries").EnumerateArray().ToArray();
        Assert.Equal(14, entries.Length);
        Assert.Contains(entries, item =>
            item.GetProperty("ExecutionTradableSymbol").GetString() == "USDJPY" &&
            item.GetProperty("SessionWindowCategory").GetString() == "OpeningBuild" &&
            item.GetProperty("NormalizedPortfolioSymbol").GetString() == "JPYUSD" &&
            item.GetProperty("RequiresInversion").GetBoolean() &&
            item.GetProperty("QuoteFileExists").GetBoolean() &&
            item.GetProperty("ManifestExists").GetBoolean());
        Assert.All(entries, item =>
        {
            Assert.True(item.GetProperty("QuoteFileExists").GetBoolean());
            Assert.True(item.GetProperty("ManifestExists").GetBoolean());
            Assert.True(item.GetProperty("PathPresenceCheckedOnly").GetBoolean());
            Assert.False(item.GetProperty("QuoteRowsRead").GetBoolean());
            Assert.False(item.GetProperty("ManifestContentRead").GetBoolean());
            Assert.False(item.GetProperty("ContainsSecrets").GetBoolean());
            Assert.False(item.GetProperty("ContainsRawProviderPayload").GetBoolean());
        });

        Assert.True(diagnostics.RootElement.GetProperty("missingInputDiagnosticsCreated").GetBoolean());
        Assert.Equal(0, diagnostics.RootElement.GetProperty("MissingDiagnosticsCount").GetInt32());
        Assert.False(diagnostics.RootElement.GetProperty("MissingFilePathsBlockSafely").GetBoolean());
        Assert.False(diagnostics.RootElement.GetProperty("MissingManifestPathsBlockSafely").GetBoolean());
        Assert.False(diagnostics.RootElement.GetProperty("MissingDateRangesOrSessionTimesNeedOperatorInput").GetBoolean());
        Assert.False(diagnostics.RootElement.GetProperty("OperatorPlaceholdersSupplied").GetBoolean());
        Assert.Empty(diagnostics.RootElement.GetProperty("Diagnostics").EnumerateArray());
    }

    [Fact]
    public void Inversion_direct_cross_cost_nonmajor_usdjpy_and_lmax_preservations_are_safe()
    {
        var inversion = ReadJson("phase-exec-sim-r027-inversion-preservation.json");
        var direct = ReadJson("phase-exec-sim-r027-direct-cross-exclusion-preservation.json");
        var cost = ReadJson("phase-exec-sim-r027-cost-guidance-preservation.json");
        var nonmajor = ReadJson("phase-exec-sim-r027-nonmajor-calibration-preservation.json");
        var usdjpy = ReadJson("phase-exec-sim-r027-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r027-lmax-readonly-baseline-reference.json");

        Assert.True(inversion.RootElement.GetProperty("inversionPreservationCreated").GetBoolean());
        Assert.Equal("JPYUSD", inversion.RootElement.GetProperty("UsdJpy").GetProperty("NormalizedPortfolioSymbol").GetString());
        Assert.True(inversion.RootElement.GetProperty("UsdJpy").GetProperty("RequiresInversion").GetBoolean());
        Assert.Equal("CADUSD", inversion.RootElement.GetProperty("UsdCad").GetProperty("NormalizedPortfolioSymbol").GetString());
        Assert.True(inversion.RootElement.GetProperty("UsdCad").GetProperty("RequiresInversion").GetBoolean());
        Assert.Equal("CHFUSD", inversion.RootElement.GetProperty("UsdChf").GetProperty("NormalizedPortfolioSymbol").GetString());
        Assert.True(inversion.RootElement.GetProperty("UsdChf").GetProperty("RequiresInversion").GetBoolean());
        Assert.False(inversion.RootElement.GetProperty("AudUsdMisclassifiedFailed").GetBoolean());

        Assert.True(direct.RootElement.GetProperty("directCrossExclusionPreserved").GetBoolean());
        Assert.True(direct.RootElement.GetProperty("directCrossesSignalOnly").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("directCrossEntriesAccepted").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("directCrossExclusionWeakened").GetBoolean());
        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(nonmajor.RootElement.GetProperty("nonMajorEmScandiCnhRequireLiquidityCalibration").GetBoolean());
        Assert.False(nonmajor.RootElement.GetProperty("calibrationRequirementWeakened").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("usdjpyCaveatPreserved").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.False(usdjpy.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        Assert.True(lmax.RootElement.GetProperty("referenceOnly").GetBoolean());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR027").GetBoolean());
    }

    [Fact]
    public void No_download_row_validation_import_backtest_tca_order_api_or_runtime_audits_are_clean()
    {
        AssertAuditFalse("phase-exec-sim-r027-no-download-audit.json", "filesDownloaded");
        AssertAuditFalse("phase-exec-sim-r027-no-row-validation-audit.json", "quoteRowsValidated");
        AssertAuditFalse("phase-exec-sim-r027-no-db-import-audit.json", "quotesImportedIntoDb");
        AssertAuditFalse("phase-exec-sim-r027-no-sanitized-row-audit.json", "persistedSanitizedQuoteRowsCreated");
        AssertAuditFalse("phase-exec-sim-r027-no-backtest-simulation-audit.json", "backtestExecuted");
        AssertAuditFalse("phase-exec-sim-r027-no-backtest-simulation-audit.json", "simulationExecuted");
        AssertAuditFalse("phase-exec-sim-r027-no-tca-result-lines-audit.json", "tcaResultLinesProduced");
        AssertAuditFalse("phase-exec-sim-r027-no-order-fill-report-route-audit.json", "ordersCreated");
        AssertAuditFalse("phase-exec-sim-r027-no-order-fill-report-route-audit.json", "fillsCreated");
        AssertAuditFalse("phase-exec-sim-r027-no-order-fill-report-route-audit.json", "executionReportsCreated");
        AssertAuditFalse("phase-exec-sim-r027-no-order-fill-report-route-audit.json", "routesCreated");
        AssertAuditFalse("phase-exec-sim-r027-no-polygon-api-call-audit.json", "polygonApiCalled");
        AssertAuditFalse("phase-exec-sim-r027-no-lmax-call-audit.json", "lmaxCalled");
        AssertAuditFalse("phase-exec-sim-r027-no-external-api-call-audit.json", "externalApiCalled");
        AssertAuditFalse("phase-exec-sim-r027-no-broker-marketdata-runtime-audit.json", "brokerActivationDetected");
        AssertAuditFalse("phase-exec-sim-r027-no-broker-marketdata-runtime-audit.json", "marketDataRequestSent");
        AssertAuditFalse("phase-exec-sim-r027-no-broker-marketdata-runtime-audit.json", "schedulerServiceTimerPollingBackgroundJobIntroduced");
        AssertAuditFalse("phase-exec-sim-r027-no-external-audit.json", "filesDownloaded");
        AssertAuditFalse("phase-exec-sim-r027-no-external-audit.json", "dbImportOccurred");
        AssertAuditFalse("phase-exec-sim-r027-no-external-audit.json", "ordersFillsReportsRoutesSubmissionsCreated");
        AssertAuditFalse("phase-exec-sim-r027-forbidden-actions-audit.json", "forbiddenActionsDetected");
    }

    [Fact]
    public void Next_phase_recommendation_matches_blocked_or_file_supplied_paths()
    {
        var next = ReadJson("phase-exec-sim-r027-next-phase-recommendation.json");

        Assert.True(next.RootElement.GetProperty("nextPhaseRecommendationCreated").GetBoolean());
        Assert.Contains("Operator should supply OpeningBuild and ClosingFlatten", next.RootElement.GetProperty("IfBlocked").GetString());
        Assert.Equal("EXEC-SIM-R028", next.RootElement.GetProperty("IfFilesSupplied").GetString());
        Assert.Contains("file/manifests only", next.RootElement.GetProperty("R028Scope").GetString());
    }

    private static void AssertSymbol(JsonDocument document, string executionSymbol, string normalizedSymbol, bool requiresInversion)
    {
        var match = document.RootElement.GetProperty("Symbols").EnumerateArray()
            .Single(x => x.GetProperty("ExecutionTradableSymbol").GetString() == executionSymbol);
        Assert.Equal(normalizedSymbol, match.GetProperty("NormalizedPortfolioSymbol").GetString());
        Assert.Equal(requiresInversion, match.GetProperty("RequiresInversion").GetBoolean());
    }

    private static void AssertAuditFalse(string fileName, string propertyName)
    {
        var document = ReadJson(fileName);
        Assert.False(document.RootElement.GetProperty(propertyName).GetBoolean());
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] RequiredFileEntryFields =
    [
        "Symbol",
        "ProviderSymbol",
        "ExecutionTradableSymbol",
        "NormalizedPortfolioSymbol",
        "RequiresInversion",
        "QuoteFilePath",
        "ManifestPath",
        "FileFormat",
        "ProviderName",
        "ProviderDatasetType",
        "TimeRangeStartUtc",
        "TimeRangeEndUtc",
        "SessionWindowCategory",
        "ContainsSecrets",
        "ContainsRawProviderPayload"
    ];

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r027-summary.md",
        "phase-exec-sim-r027-r026-data-expansion-decision-reference.json",
        "phase-exec-sim-r027-historical-window-expansion-authorization-contract.json",
        "phase-exec-sim-r027-historical-window-expansion-request.json",
        "phase-exec-sim-r027-historical-window-expansion-preflight-contract.json",
        "phase-exec-sim-r027-authorization-result.json",
        "phase-exec-sim-r027-required-symbols.json",
        "phase-exec-sim-r027-required-session-window-categories.json",
        "phase-exec-sim-r027-file-entry-requirements.json",
        "phase-exec-sim-r027-accepted-for-authorization-entries.json",
        "phase-exec-sim-r027-missing-input-diagnostics.json",
        "phase-exec-sim-r027-inversion-preservation.json",
        "phase-exec-sim-r027-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r027-cost-guidance-preservation.json",
        "phase-exec-sim-r027-nonmajor-calibration-preservation.json",
        "phase-exec-sim-r027-no-download-audit.json",
        "phase-exec-sim-r027-no-row-validation-audit.json",
        "phase-exec-sim-r027-no-db-import-audit.json",
        "phase-exec-sim-r027-no-sanitized-row-audit.json",
        "phase-exec-sim-r027-no-backtest-simulation-audit.json",
        "phase-exec-sim-r027-no-tca-result-lines-audit.json",
        "phase-exec-sim-r027-no-order-fill-report-route-audit.json",
        "phase-exec-sim-r027-no-polygon-api-call-audit.json",
        "phase-exec-sim-r027-no-lmax-call-audit.json",
        "phase-exec-sim-r027-no-external-api-call-audit.json",
        "phase-exec-sim-r027-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r027-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r027-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r027-no-external-audit.json",
        "phase-exec-sim-r027-forbidden-actions-audit.json",
        "phase-exec-sim-r027-next-phase-recommendation.json"
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
