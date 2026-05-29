using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimSessionAwareTcaReviewParameterRecommendationTests
{
    [Fact]
    public void Required_r016_artifacts_exist_and_reference_r015()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R016 artifact {artifact}");
        }

        var review = ReadJson("phase-exec-sim-r016-r015-session-aware-tca-review-summary.json");
        Assert.Equal("EXEC-SIM-R015", review.RootElement.GetProperty("sourcePhase").GetString());
        Assert.True(review.RootElement.GetProperty("reviewOnly").GetBoolean());
        Assert.False(review.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(review.RootElement.GetProperty("newSimulationResultLinesCreated").GetBoolean());
        Assert.False(review.RootElement.GetProperty("r015NumericMetricsInventedInR016").GetBoolean());
    }

    [Fact]
    public void Parameter_recommendation_contract_is_design_only_and_complete()
    {
        var contract = ReadJson("phase-exec-sim-r016-parameter-recommendation-contract.json");

        Assert.True(contract.RootElement.GetProperty("designOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("notExecutableAlgoConfiguration").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("notProductionTradingConfiguration").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("noExternal").GetBoolean());
        AssertContains(contract, "barRoles", "OpeningBuild");
        AssertContains(contract, "barRoles", "IntradayRebalance");
        AssertContains(contract, "barRoles", "ClosingFlatten");
        AssertContains(contract, "parameterCategories", "ResidualCrossThreshold");
        AssertContains(contract, "parameterCategories", "RequiredFeedQualityBucket");
        AssertContains(contract, "recommendedParameterStatuses", "RecommendedDesignOnly");
        AssertContains(contract, "recommendedParameterStatuses", "NeedsMoreData");
        Assert.False(contract.RootElement.GetProperty("createsOrders").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("createsFills").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("createsExecutionReports").GetBoolean());
    }

    [Fact]
    public void Opening_intraday_and_closing_recommendations_preserve_bar_role_constraints()
    {
        var opening = ReadJson("phase-exec-sim-r016-opening-build-parameter-recommendations.json");
        var intraday = ReadJson("phase-exec-sim-r016-intraday-rebalance-parameter-recommendations.json");
        var closing = ReadJson("phase-exec-sim-r016-closing-flatten-parameter-recommendations.json");

        Assert.Equal("OpeningBuild", opening.RootElement.GetProperty("BarRole").GetString());
        Assert.True(opening.RootElement.GetProperty("targetMayBeKnownPreviousEvening").GetBoolean());
        Assert.True(opening.RootElement.GetProperty("EarliestExecutionTimestampUtcMustBeSessionStartOrExplicitAllowedStart").GetBoolean());
        Assert.True(opening.RootElement.GetProperty("previousEveningPreComputationAllowed").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("overnightExposureBeforeSessionStartAllowed").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("forceBlindCrossingBecauseKnownPreviousEvening").GetBoolean());

        Assert.Equal("IntradayRebalance", intraday.RootElement.GetProperty("BarRole").GetString());
        Assert.True(intraday.RootElement.GetProperty("normalCloseSeekingBehaviorPreserved").GetBoolean());
        Assert.Equal("Normal", intraday.RootElement.GetProperty("ResidualPenaltyBucket").GetString());
        Assert.True(intraday.RootElement.GetProperty("ControlledResidualCrossOnlyWhenOpportunityCostExceedsCrossingCost").GetBoolean());

        Assert.Equal("ClosingFlatten", closing.RootElement.GetProperty("BarRole").GetString());
        Assert.Equal("Flat", closing.RootElement.GetProperty("TargetPosition").GetString());
        Assert.True(closing.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.Equal("NoOvernightCritical", closing.RootElement.GetProperty("ResidualPenaltyBucket").GetString());
        Assert.Equal("StrictlyLowerThanIntradayRebalance", closing.RootElement.GetProperty("MaxResidualAtClose").GetString());
        Assert.Equal("StricterThanIntradayRebalance", closing.RootElement.GetProperty("ResidualCrossThreshold").GetString());
        Assert.False(closing.RootElement.GetProperty("PureLimitUntilCloseDefaultAllowed").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("FiveMarketSlicesDefaultAllowed").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("AlwaysMarketAtCloseDefaultAllowed").GetBoolean());
    }

    [Fact]
    public void Policy_recommendations_ladders_blocks_and_manual_review_triggers_are_present()
    {
        var recommendations = ReadJson("phase-exec-sim-r016-policy-recommendations-by-bar-role.json");
        var ladder = ReadJson("phase-exec-sim-r016-policy-fallback-ladder-by-bar-role.json");
        var blocked = ReadJson("phase-exec-sim-r016-blocked-policy-families-by-bar-role.json");
        var manual = ReadJson("phase-exec-sim-r016-manual-review-triggers-by-bar-role.json");

        Assert.True(recommendations.RootElement.GetProperty("designOnly").GetBoolean());
        Assert.True(recommendations.RootElement.GetProperty("notExecutableConfiguration").GetBoolean());
        Assert.Contains(recommendations.RootElement.GetProperty("policyRecommendations").EnumerateArray(), x =>
            x.GetProperty("BarRole").GetString() == "ClosingFlatten" &&
            x.GetProperty("Preferred").GetString() == "ControlledResidualCrossWhenJustified");
        AssertContains(ladder, "fallbackLadderLevels", "Preferred");
        AssertContains(ladder, "fallbackLadderLevels", "ManualReview");
        AssertContains(ladder, "fallbackLadderLevels", "DoNotTrade");
        AssertContains(blocked, "blockedPolicyFamilies", "PureLimitUntilCloseDefault");
        AssertContains(blocked, "blockedPolicyFamilies", "MechanicalMarketSlicesAroundClose");
        AssertContains(blocked, "blockedPolicyFamilies", "AlwaysMarketAtClose");
        Assert.False(blocked.RootElement.GetProperty("wakettPatternBlockWeakened").GetBoolean());
        AssertContains(manual, "manualReviewTriggers", "UnsafeFeed");
        AssertContains(manual, "manualReviewTriggers", "MissingCloseBenchmark");
        AssertContains(manual, "manualReviewTriggers", "ExtremeSpread");
        Assert.True(manual.RootElement.GetProperty("manualReviewIsNotAutomaticExecution").GetBoolean());
    }

    [Fact]
    public void Feed_benchmark_no_overnight_and_previous_evening_requirements_are_defined()
    {
        var feed = ReadJson("phase-exec-sim-r016-feed-quality-requirements-by-bar-role.json");
        var benchmark = ReadJson("phase-exec-sim-r016-close-benchmark-requirements-by-bar-role.json");
        var flatten = ReadJson("phase-exec-sim-r016-no-overnight-flatten-parameter-requirements.json");
        var firstBar = ReadJson("phase-exec-sim-r016-first-bar-previous-evening-planning-requirements.json");

        Assert.Equal(3, feed.RootElement.GetProperty("requirements").GetArrayLength());
        Assert.True(feed.RootElement.GetProperty("NoQuoteNearCloseTriggersManualReview").GetBoolean());
        Assert.True(feed.RootElement.GetProperty("StaleQuoteNearCloseTriggersManualReview").GetBoolean());
        Assert.Equal(3, benchmark.RootElement.GetProperty("requirements").GetArrayLength());
        Assert.True(benchmark.RootElement.GetProperty("MissingCloseBenchmarkTriggersManualReview").GetBoolean());
        Assert.Equal("Flat", flatten.RootElement.GetProperty("TargetPosition").GetString());
        Assert.True(flatten.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.False(flatten.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.Equal("NoOvernightCritical", flatten.RootElement.GetProperty("ResidualPenaltyBucket").GetString());
        Assert.False(flatten.RootElement.GetProperty("AlwaysMarketAtCloseDefaultAllowed").GetBoolean());
        Assert.True(firstBar.RootElement.GetProperty("firstBarTargetKnownPreviousEveningSupported").GetBoolean());
        Assert.True(firstBar.RootElement.GetProperty("KnownAtTimestampUtcSeparateFromEarliestExecutionTimestampUtc").GetBoolean());
        Assert.False(firstBar.RootElement.GetProperty("overnightExposureBeforeSessionStartAllowed").GetBoolean());
        Assert.False(firstBar.RootElement.GetProperty("ordersBeforeSessionStartAllowed").GetBoolean());
    }

    [Fact]
    public void Cost_nonmajor_usd_pair_direct_cross_wakett_and_caveats_are_preserved()
    {
        var cost = ReadJson("phase-exec-sim-r016-cost-guidance-by-bar-role.json");
        var nonMajor = ReadJson("phase-exec-sim-r016-nonmajor-calibration-preservation.json");
        var normalization = ReadJson("phase-exec-sim-r016-usd-pair-normalization-preservation.json");
        var directCross = ReadJson("phase-exec-sim-r016-direct-cross-exclusion-preservation.json");
        var wakett = ReadJson("phase-exec-sim-r016-wakett-pattern-block-preservation.json");
        var usdjpy = ReadJson("phase-exec-sim-r016-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r016-lmax-readonly-baseline-reference.json");

        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.Equal(3, cost.RootElement.GetProperty("costGuidanceByBarRole").GetArrayLength());
        Assert.True(nonMajor.RootElement.GetProperty("nonMajorEmScandiCnhRequireLiquidityCalibration").GetBoolean());
        Assert.True(nonMajor.RootElement.GetProperty("doNotExtrapolateEurusdUsdjpyAudusdResultsToNonMajor").GetBoolean());
        Assert.Equal("USD-pair-only", normalization.RootElement.GetProperty("executionUniverse").GetString());
        Assert.False(normalization.RootElement.GetProperty("normalizationWeakened").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("directCrossIncludedInRecommendationsAsExecutionInstrument").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("guidanceWeakened").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("PureLimitUntilCloseDefaultAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("MechanicalMarketSlicesAroundCloseAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("AlwaysMarketAtCloseAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("wakettPatternBlockWeakened").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("caveatPreserved").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("requiresInversion").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.Contains("not failed", lmax.RootElement.GetProperty("audusdStatus").GetString());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR016").GetBoolean());
    }

    [Fact]
    public void No_new_backtest_no_new_result_lines_no_api_runtime_or_order_audits_are_clean()
    {
        var noBacktest = ReadJson("phase-exec-sim-r016-no-new-backtest-audit.json");
        var noLines = ReadJson("phase-exec-sim-r016-no-new-simulation-result-lines-audit.json");
        var api = ReadJson("phase-exec-sim-r016-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r016-no-broker-marketdata-runtime-audit.json");
        var fill = ReadJson("phase-exec-sim-r016-no-real-fill-audit.json");
        var report = ReadJson("phase-exec-sim-r016-no-execution-report-audit.json");
        var order = ReadJson("phase-exec-sim-r016-no-order-created-audit.json");
        var route = ReadJson("phase-exec-sim-r016-no-route-no-submission-audit.json");
        var noExternal = ReadJson("phase-exec-sim-r016-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r016-forbidden-actions-audit.json");

        Assert.False(noBacktest.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newQuoteFilesImported").GetBoolean());
        Assert.False(noLines.RootElement.GetProperty("newSimulationResultLinesCreated").GetBoolean());
        Assert.False(noLines.RootElement.GetProperty("simulationResultLinesNamedAsFills").GetBoolean());
        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("brokerActivationDetected").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("socketOpened").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("schedulerServiceTimerPollingBackgroundJobIntroduced").GetBoolean());
        Assert.False(fill.RootElement.GetProperty("realFillsCreated").GetBoolean());
        Assert.False(fill.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());
        Assert.False(report.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("executableOrdersCreated").GetBoolean());
        Assert.False(route.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(route.RootElement.GetProperty("submissionsCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("newSimulationResultLinesCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("ordersFillsReportsRoutesSubmissionsCreated").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r016-summary.md",
        "phase-exec-sim-r016-r015-session-aware-tca-review-summary.json",
        "phase-exec-sim-r016-parameter-recommendation-contract.json",
        "phase-exec-sim-r016-opening-build-parameter-recommendations.json",
        "phase-exec-sim-r016-intraday-rebalance-parameter-recommendations.json",
        "phase-exec-sim-r016-closing-flatten-parameter-recommendations.json",
        "phase-exec-sim-r016-policy-recommendations-by-bar-role.json",
        "phase-exec-sim-r016-policy-fallback-ladder-by-bar-role.json",
        "phase-exec-sim-r016-blocked-policy-families-by-bar-role.json",
        "phase-exec-sim-r016-manual-review-triggers-by-bar-role.json",
        "phase-exec-sim-r016-feed-quality-requirements-by-bar-role.json",
        "phase-exec-sim-r016-close-benchmark-requirements-by-bar-role.json",
        "phase-exec-sim-r016-no-overnight-flatten-parameter-requirements.json",
        "phase-exec-sim-r016-first-bar-previous-evening-planning-requirements.json",
        "phase-exec-sim-r016-cost-guidance-by-bar-role.json",
        "phase-exec-sim-r016-nonmajor-calibration-preservation.json",
        "phase-exec-sim-r016-usd-pair-normalization-preservation.json",
        "phase-exec-sim-r016-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r016-wakett-pattern-block-preservation.json",
        "phase-exec-sim-r016-no-new-backtest-audit.json",
        "phase-exec-sim-r016-no-new-simulation-result-lines-audit.json",
        "phase-exec-sim-r016-no-polygon-api-call-audit.json",
        "phase-exec-sim-r016-no-lmax-call-audit.json",
        "phase-exec-sim-r016-no-external-api-call-audit.json",
        "phase-exec-sim-r016-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r016-no-real-fill-audit.json",
        "phase-exec-sim-r016-no-execution-report-audit.json",
        "phase-exec-sim-r016-no-order-created-audit.json",
        "phase-exec-sim-r016-no-route-no-submission-audit.json",
        "phase-exec-sim-r016-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r016-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r016-no-external-audit.json",
        "phase-exec-sim-r016-forbidden-actions-audit.json",
        "phase-exec-sim-r016-next-phase-recommendation.json"
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
