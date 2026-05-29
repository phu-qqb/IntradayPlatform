using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimTcaReviewSessionAwareRequirementsTests
{
    [Fact]
    public void Required_r014_artifacts_exist_and_reference_r013()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R014 artifact {artifact}");
        }

        var review = ReadJson("phase-exec-sim-r014-r013-tca-review-summary.json");
        Assert.Equal("EXEC-SIM-R013", review.RootElement.GetProperty("sourcePhase").GetString());
        Assert.True(review.RootElement.GetProperty("reviewOnly").GetBoolean());
        Assert.False(review.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(review.RootElement.GetProperty("newSimulationResultLinesCreated").GetBoolean());
        Assert.False(review.RootElement.GetProperty("r013NumericMetricsInventedInR014").GetBoolean());
        Assert.Equal("CloseSeeking15mAdaptive", review.RootElement.GetProperty("bestOverallTradeoffPolicyFromR013").GetString());
    }

    [Fact]
    public void Session_model_and_bar_role_contracts_define_opening_intraday_and_closing_roles()
    {
        var session = ReadJson("phase-exec-sim-r014-session-model-contract.json");
        var barRole = ReadJson("phase-exec-sim-r014-bar-role-contract.json");

        Assert.Equal(15, session.RootElement.GetProperty("BarIntervalMinutes").GetInt32());
        Assert.False(session.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.True(session.RootElement.GetProperty("MustEndFlat").GetBoolean());
        AssertContains(barRole, "BarRoles", "OpeningBuild");
        AssertContains(barRole, "BarRoles", "IntradayRebalance");
        AssertContains(barRole, "BarRoles", "ClosingFlatten");
        AssertContains(barRole, "fields", "KnownAtTimestampUtc");
        AssertContains(barRole, "fields", "EarliestExecutionTimestampUtc");
        AssertContains(barRole, "blockedAlgoFamilies", "MechanicalFiveMarketSlicesAroundClose");
        AssertContains(barRole, "blockedAlgoFamilies", "AlwaysMarketAtCloseDefault");
    }

    [Fact]
    public void Opening_build_supports_previous_evening_knowledge_without_overnight_execution()
    {
        var opening = ReadJson("phase-exec-sim-r014-opening-build-requirements.json");
        var firstBar = ReadJson("phase-exec-sim-r014-first-bar-known-previous-evening.json");

        Assert.Equal("OpeningBuild", opening.RootElement.GetProperty("BarRole").GetString());
        Assert.True(opening.RootElement.GetProperty("targetMayBeKnownPreviousEvening").GetBoolean());
        Assert.True(opening.RootElement.GetProperty("EarliestExecutionTimestampUtcMustNotPrecedeAllowedSessionStart").GetBoolean());
        Assert.True(opening.RootElement.GetProperty("KnownAtTimestampDoesNotAuthorizeOvernightExposure").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("overnightExposureBeforeSessionStartAllowed").GetBoolean());
        Assert.Equal("High", opening.RootElement.GetProperty("ExpectedTurnoverBucket").GetString());
        Assert.True(firstBar.RootElement.GetProperty("EarliestExecutionTimestampUtcSeparateFromKnownAtTimestampUtc").GetBoolean());
        Assert.False(firstBar.RootElement.GetProperty("orderCreationPreviousEveningAllowed").GetBoolean());
        Assert.False(firstBar.RootElement.GetProperty("overnightExposureCreationAllowed").GetBoolean());
    }

    [Fact]
    public void Intraday_rebalance_preserves_normal_close_seeking_behavior()
    {
        var intraday = ReadJson("phase-exec-sim-r014-intraday-rebalance-requirements.json");

        Assert.Equal("IntradayRebalance", intraday.RootElement.GetProperty("BarRole").GetString());
        Assert.Equal("ExistingIntradayPosition", intraday.RootElement.GetProperty("CurrentPositionSource").GetString());
        Assert.Equal("TargetPositionMinusCurrentPosition", intraday.RootElement.GetProperty("DeltaDefinition").GetString());
        Assert.True(intraday.RootElement.GetProperty("normalCloseSeeking15mBehaviorPreserved").GetBoolean());
        Assert.Equal("Normal", intraday.RootElement.GetProperty("ResidualPenaltyBucket").GetString());
        AssertContains(intraday, "PreferredWhenFeedAndSpreadAllow", "CloseSeeking15mAdaptive");
    }

    [Fact]
    public void Closing_flatten_enforces_flat_target_and_blocks_blind_mechanical_policies()
    {
        var closing = ReadJson("phase-exec-sim-r014-closing-flatten-requirements.json");
        var noOvernight = ReadJson("phase-exec-sim-r014-no-overnight-flat-constraint.json");

        Assert.Equal("ClosingFlatten", closing.RootElement.GetProperty("BarRole").GetString());
        Assert.Equal("Flat", closing.RootElement.GetProperty("TargetPosition").GetString());
        Assert.Equal(0, closing.RootElement.GetProperty("TargetPositionValue").GetInt32());
        Assert.True(closing.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.Equal("NoOvernightCritical", closing.RootElement.GetProperty("ResidualPenaltyBucket").GetString());
        Assert.Equal("MustCompleteFlat", closing.RootElement.GetProperty("CompletionPriority").GetString());
        Assert.True(closing.RootElement.GetProperty("NoBlindFiveMarketSlicesAtClose").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("AlwaysMarketAtCloseDefaultAllowed").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("liveEmergencyExecutionCreatedInR014").GetBoolean());
        Assert.False(noOvernight.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.Equal("Flat", noOvernight.RootElement.GetProperty("RequiredEndOfSessionPosition").GetString());
        Assert.True(noOvernight.RootElement.GetProperty("ManualEscalationRequiredIfResidual").GetBoolean());
    }

    [Fact]
    public void Session_boundary_turnover_policy_parameters_and_tca_buckets_are_defined()
    {
        var turnover = ReadJson("phase-exec-sim-r014-session-boundary-turnover-requirements.json");
        var parameters = ReadJson("phase-exec-sim-r014-session-aware-policy-parameters.json");
        var buckets = ReadJson("phase-exec-sim-r014-session-aware-tca-buckets.json");

        Assert.True(turnover.RootElement.GetProperty("firstAndLastBarsMustBeReportedSeparately").GetBoolean());
        Assert.Contains(turnover.RootElement.GetProperty("barRoleTurnoverExpectations").EnumerateArray(), x =>
            x.GetProperty("BarRole").GetString() == "OpeningBuild" &&
            x.GetProperty("ExpectedTurnoverBucket").GetString() == "High");
        Assert.Contains(turnover.RootElement.GetProperty("barRoleTurnoverExpectations").EnumerateArray(), x =>
            x.GetProperty("BarRole").GetString() == "ClosingFlatten" &&
            x.GetProperty("ExpectedTurnoverBucket").GetString() == "VeryHigh");
        Assert.Equal("NoOvernightCritical", parameters.RootElement.GetProperty("ClosingFlattenPolicyParameters").GetProperty("ResidualPenaltyBucket").GetString());
        Assert.False(parameters.RootElement.GetProperty("AlwaysMarketAtCloseDefaultAllowed").GetBoolean());
        AssertContains(buckets, "tcaBuckets", "OpeningBuildTca");
        AssertContains(buckets, "tcaBuckets", "IntradayRebalanceTca");
        AssertContains(buckets, "tcaBuckets", "ClosingFlattenTca");
        AssertContains(buckets, "tcaBuckets", "SessionAggregateTca");
        AssertContains(buckets, "ResidualPenaltyBucket", "NoOvernightCritical");
        Assert.False(buckets.RootElement.GetProperty("firstLastBarAggregationWithoutSeparateBucketAllowed").GetBoolean());
    }

    [Fact]
    public void Future_simulation_wakett_risk_and_cost_guidance_are_session_aware()
    {
        var future = ReadJson("phase-exec-sim-r014-future-session-aware-simulation-requirements.json");
        var wakett = ReadJson("phase-exec-sim-r014-wakett-patterns-session-aware-risk.json");
        var cost = ReadJson("phase-exec-sim-r014-cost-guidance-by-bar-role.json");

        Assert.True(future.RootElement.GetProperty("mustSeparateFirstMiddleLastBars").GetBoolean());
        Assert.True(future.RootElement.GetProperty("mustNotAggregateFirstAndLastBarsWithNormalIntradayWithoutSeparateBuckets").GetBoolean());
        AssertContains(future, "requiredScenarioTests", "first-bar high-turnover build");
        AssertContains(future, "requiredScenarioTests", "middle-bar normal turnover rebalance");
        AssertContains(future, "requiredScenarioTests", "last-bar high-turnover flatten");
        Assert.True(future.RootElement.GetProperty("mustReportFiveUsdPerMillionPlausibilityByBarRole").GetBoolean());
        Assert.True(wakett.RootElement.GetProperty("wakettPatternsRemainBlockedAsDefaultPolicies").GetBoolean());
        Assert.True(wakett.RootElement.GetProperty("PureLimitUntilClose").GetProperty("blockedAsDefault").GetBoolean());
        Assert.True(wakett.RootElement.GetProperty("MechanicalMarketSlicesAroundClose").GetProperty("blockedAsDefault").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("AlwaysMarketAtCloseDefaultAllowed").GetBoolean());
        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(cost.RootElement.GetProperty("plausibilityMustBeReportedSeparatelyByBarRole").GetBoolean());
    }

    [Fact]
    public void Usd_pair_direct_cross_usdjpy_audusd_and_lmax_preservations_hold()
    {
        var normalization = ReadJson("phase-exec-sim-r014-usd-pair-normalization-preservation.json");
        var directCross = ReadJson("phase-exec-sim-r014-direct-cross-exclusion-preservation.json");
        var usdjpy = ReadJson("phase-exec-sim-r014-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r014-lmax-readonly-baseline-reference.json");

        Assert.Equal("USD-pair-only", normalization.RootElement.GetProperty("executionUniverse").GetString());
        Assert.True(normalization.RootElement.GetProperty("mandatoryNettingBeforeExecution").GetBoolean());
        Assert.False(normalization.RootElement.GetProperty("normalizationWeakened").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("directCrossIncludedInRequirementsAsExecutionInstrument").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("guidanceWeakened").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("caveatPreserved").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("requiresInversion").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.Contains("not failed", lmax.RootElement.GetProperty("audusdStatus").GetString());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR014").GetBoolean());
    }

    [Fact]
    public void No_new_backtest_no_api_runtime_or_order_domain_audits_are_clean()
    {
        var noBacktest = ReadJson("phase-exec-sim-r014-no-new-backtest-audit.json");
        var api = ReadJson("phase-exec-sim-r014-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r014-no-broker-marketdata-runtime-audit.json");
        var order = ReadJson("phase-exec-sim-r014-no-order-fill-report-route-audit.json");
        var noExternal = ReadJson("phase-exec-sim-r014-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r014-forbidden-actions-audit.json");

        Assert.False(noBacktest.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newQuoteFilesImported").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newSimulationResultLinesCreated").GetBoolean());
        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("brokerActivationDetected").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("socketOpened").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("schedulerServiceTimerPollingBackgroundJobIntroduced").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("fillsCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("executionReportsCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("submissionsCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("ordersFillsReportsRoutesSubmissionsCreated").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r014-summary.md",
        "phase-exec-sim-r014-r013-tca-review-summary.json",
        "phase-exec-sim-r014-session-model-contract.json",
        "phase-exec-sim-r014-bar-role-contract.json",
        "phase-exec-sim-r014-opening-build-requirements.json",
        "phase-exec-sim-r014-intraday-rebalance-requirements.json",
        "phase-exec-sim-r014-closing-flatten-requirements.json",
        "phase-exec-sim-r014-no-overnight-flat-constraint.json",
        "phase-exec-sim-r014-first-bar-known-previous-evening.json",
        "phase-exec-sim-r014-session-boundary-turnover-requirements.json",
        "phase-exec-sim-r014-session-aware-policy-parameters.json",
        "phase-exec-sim-r014-session-aware-tca-buckets.json",
        "phase-exec-sim-r014-future-session-aware-simulation-requirements.json",
        "phase-exec-sim-r014-wakett-patterns-session-aware-risk.json",
        "phase-exec-sim-r014-cost-guidance-by-bar-role.json",
        "phase-exec-sim-r014-usd-pair-normalization-preservation.json",
        "phase-exec-sim-r014-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r014-no-new-backtest-audit.json",
        "phase-exec-sim-r014-no-polygon-api-call-audit.json",
        "phase-exec-sim-r014-no-lmax-call-audit.json",
        "phase-exec-sim-r014-no-external-api-call-audit.json",
        "phase-exec-sim-r014-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r014-no-order-fill-report-route-audit.json",
        "phase-exec-sim-r014-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r014-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r014-no-external-audit.json",
        "phase-exec-sim-r014-forbidden-actions-audit.json",
        "phase-exec-sim-r014-next-phase-recommendation.json"
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
