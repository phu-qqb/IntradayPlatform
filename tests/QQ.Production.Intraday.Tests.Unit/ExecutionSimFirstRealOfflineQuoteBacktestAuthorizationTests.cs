using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimFirstRealOfflineQuoteBacktestAuthorizationTests
{
    [Fact]
    public void Required_r012_authorization_artifacts_exist()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R012 artifact {artifact}");
        }
    }

    [Fact]
    public void Authorization_contract_request_and_preflight_are_ready_and_authorization_only()
    {
        var contract = ReadJson("phase-exec-sim-r012-backtest-authorization-contract.json");
        var request = ReadJson("phase-exec-sim-r012-backtest-authorization-request.json");
        var preflight = ReadJson("phase-exec-sim-r012-backtest-preflight-contract.json");
        var result = ReadJson("phase-exec-sim-r012-backtest-authorization-result.json");

        Assert.True(contract.RootElement.GetProperty("authorizationOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("requiresAcceptedFileManifests").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("requiresSanitizedImportReadiness").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("requiresQuoteWindowReadiness").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("requiresCloseBenchmarkReadiness").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("requiresFeedQualityReadiness").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("noBacktest").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("noTcaPolicyResults").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("noSimulationResultLines").GetBoolean());
        Assert.True(request.RootElement.GetProperty("AuthorizationOnly").GetBoolean());
        Assert.True(request.RootElement.GetProperty("NoBacktest").GetBoolean());
        Assert.True(preflight.RootElement.GetProperty("preflightReady").GetBoolean());
        Assert.True(result.RootElement.GetProperty("AuthorizationReady").GetBoolean());
        Assert.Equal("FirstRealOfflineBacktestAuthorizationReadyNoExternal", result.RootElement.GetProperty("AuthorizationStatus").GetString());
    }

    [Fact]
    public void Accepted_files_and_readiness_outputs_are_authorized()
    {
        var accepted = ReadJson("phase-exec-sim-r012-accepted-files-authorized.json");
        var importReady = ReadJson("phase-exec-sim-r012-sanitized-import-readiness-authorized.json");
        var quoteWindow = ReadJson("phase-exec-sim-r012-quote-window-readiness-authorized.json");
        var closeBenchmark = ReadJson("phase-exec-sim-r012-close-benchmark-readiness-authorized.json");
        var feed = ReadJson("phase-exec-sim-r012-feed-quality-readiness-authorized.json");

        Assert.True(accepted.RootElement.GetProperty("allRequiredAcceptedFilesAuthorized").GetBoolean());
        AssertAuthorizedAcceptedFile(accepted, "EURUSD", 54694);
        AssertAuthorizedAcceptedFile(accepted, "USDJPY", 59368);
        AssertAuthorizedAcceptedFile(accepted, "AUDUSD", 60656);
        Assert.False(importReady.RootElement.GetProperty("sanitizedQuoteRowsCreated").GetBoolean());
        Assert.False(importReady.RootElement.GetProperty("importExecuted").GetBoolean());

        foreach (var result in importReady.RootElement.GetProperty("authorizedReadiness").EnumerateArray())
        {
            Assert.True(result.GetProperty("SanitizedImportReady").GetBoolean());
        }
        foreach (var result in quoteWindow.RootElement.GetProperty("authorizedQuoteWindowReadiness").EnumerateArray())
        {
            Assert.Equal("Ready", result.GetProperty("FeedWindowStatus").GetString());
        }
        foreach (var result in closeBenchmark.RootElement.GetProperty("authorizedCloseBenchmarkReadiness").EnumerateArray())
        {
            Assert.Equal("Available", result.GetProperty("CloseBenchmarkStatus").GetString());
        }
        foreach (var result in feed.RootElement.GetProperty("authorizedFeedQualityReadiness").EnumerateArray())
        {
            Assert.Equal("Good", result.GetProperty("FeedQualityBucket").GetString());
        }
    }

    [Fact]
    public void Quarantined_files_and_direct_crosses_are_excluded()
    {
        var quarantine = ReadJson("phase-exec-sim-r012-quarantined-files-excluded.json");
        var directCross = ReadJson("phase-exec-sim-r012-direct-cross-exclusion-preservation.json");

        Assert.False(quarantine.RootElement.GetProperty("quarantinedFilesIncluded").GetBoolean());
        Assert.Empty(quarantine.RootElement.GetProperty("quarantinedFileManifestIds").EnumerateArray());
        Assert.True(quarantine.RootElement.GetProperty("allFutureBacktestInputsMustComeFromAcceptedR011Manifests").GetBoolean());
        Assert.Equal("USD-pair-only", directCross.RootElement.GetProperty("executionUniverse").GetString());
        Assert.True(directCross.RootElement.GetProperty("rawQubesCrossesAreSignalInputsOnly").GetBoolean());
        Assert.True(directCross.RootElement.GetProperty("requiresNettingFirst").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("directCrossIncludedInBacktestAuthorization").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("guidanceWeakened").GetBoolean());
    }

    [Fact]
    public void Expected_policy_and_tca_report_lists_are_defined_without_results()
    {
        var policies = ReadJson("phase-exec-sim-r012-expected-policy-list.json");
        var reports = ReadJson("phase-exec-sim-r012-expected-tca-report-list.json");

        foreach (var policy in new[]
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
        })
        {
            Assert.Contains(policy, policies.RootElement.GetProperty("expectedPoliciesForR013").EnumerateArray().Select(x => x.GetString()));
        }

        Assert.True(policies.RootElement.GetProperty("wakettPatternsRemainNegativeBaselines").GetBoolean());
        Assert.False(policies.RootElement.GetProperty("policyResultsProducedInR012").GetBoolean());
        Assert.Contains("policy comparison", reports.RootElement.GetProperty("expectedTcaReportsForR013").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("median slippage ranking", reports.RootElement.GetProperty("expectedTcaReportsForR013").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("p95 slippage ranking", reports.RootElement.GetProperty("expectedTcaReportsForR013").EnumerateArray().Select(x => x.GetString()));
        Assert.False(reports.RootElement.GetProperty("tcaReportsProducedInR012").GetBoolean());
    }

    [Fact]
    public void Cost_usdjpy_and_audusd_preservations_hold()
    {
        var cost = ReadJson("phase-exec-sim-r012-cost-guidance-preservation.json");
        var usdjpy = ReadJson("phase-exec-sim-r012-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r012-lmax-readonly-baseline-reference.json");

        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(cost.RootElement.GetProperty("nonMajorEmScandiCnhRequireLiquidityCalibration").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("caveatPreserved").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("requiresInversion").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.Contains("not failed", lmax.RootElement.GetProperty("audusdStatus").GetString());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR012").GetBoolean());
    }

    [Fact]
    public void No_backtest_no_tca_no_simulation_no_api_runtime_or_order_audits_are_clean()
    {
        var noBacktest = ReadJson("phase-exec-sim-r012-no-backtest-execution-audit.json");
        var noTca = ReadJson("phase-exec-sim-r012-no-tca-policy-results-audit.json");
        var noLines = ReadJson("phase-exec-sim-r012-no-simulation-result-lines-audit.json");
        var api = ReadJson("phase-exec-sim-r012-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r012-no-broker-marketdata-runtime-audit.json");
        var order = ReadJson("phase-exec-sim-r012-no-order-fill-report-route-audit.json");
        var noExternal = ReadJson("phase-exec-sim-r012-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r012-forbidden-actions-audit.json");

        Assert.False(noBacktest.RootElement.GetProperty("backtestExecuted").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("importedQuoteBacktestOutputProduced").GetBoolean());
        Assert.False(noTca.RootElement.GetProperty("tcaPolicyResultsProduced").GetBoolean());
        Assert.False(noTca.RootElement.GetProperty("importedQuoteTcaPolicyResultsProduced").GetBoolean());
        Assert.False(noLines.RootElement.GetProperty("simulationResultLinesProduced").GetBoolean());
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
        Assert.False(noExternal.RootElement.GetProperty("backtestExecuted").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("tcaPolicyResultsProduced").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("simulationResultLinesProduced").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    private static void AssertAuthorizedAcceptedFile(JsonDocument accepted, string symbol, int rows)
    {
        var result = accepted.RootElement.GetProperty("acceptedFilesAuthorizedForFutureBacktest")
            .EnumerateArray()
            .Single(x => x.GetProperty("ExecutionTradableSymbol").GetString() == symbol);
        Assert.Equal(rows, result.GetProperty("Rows").GetInt32());
        Assert.Equal("AcceptedForSanitizedImport", result.GetProperty("ValidationStatus").GetString());
    }

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r012-summary.md",
        "phase-exec-sim-r012-backtest-authorization-contract.json",
        "phase-exec-sim-r012-backtest-authorization-request.json",
        "phase-exec-sim-r012-backtest-preflight-contract.json",
        "phase-exec-sim-r012-backtest-authorization-result.json",
        "phase-exec-sim-r012-accepted-files-authorized.json",
        "phase-exec-sim-r012-sanitized-import-readiness-authorized.json",
        "phase-exec-sim-r012-quote-window-readiness-authorized.json",
        "phase-exec-sim-r012-close-benchmark-readiness-authorized.json",
        "phase-exec-sim-r012-feed-quality-readiness-authorized.json",
        "phase-exec-sim-r012-quarantined-files-excluded.json",
        "phase-exec-sim-r012-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r012-expected-policy-list.json",
        "phase-exec-sim-r012-expected-tca-report-list.json",
        "phase-exec-sim-r012-cost-guidance-preservation.json",
        "phase-exec-sim-r012-no-backtest-execution-audit.json",
        "phase-exec-sim-r012-no-tca-policy-results-audit.json",
        "phase-exec-sim-r012-no-simulation-result-lines-audit.json",
        "phase-exec-sim-r012-no-polygon-api-call-audit.json",
        "phase-exec-sim-r012-no-lmax-call-audit.json",
        "phase-exec-sim-r012-no-external-api-call-audit.json",
        "phase-exec-sim-r012-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r012-no-order-fill-report-route-audit.json",
        "phase-exec-sim-r012-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r012-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r012-no-external-audit.json",
        "phase-exec-sim-r012-forbidden-actions-audit.json",
        "phase-exec-sim-r012-next-phase-recommendation.json"
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
