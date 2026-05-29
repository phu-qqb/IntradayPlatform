using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionAlgoSessionAwareParameterContractTests
{
    [Fact]
    public void Required_r003_artifacts_exist_and_reference_r016()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R003 artifact {artifact}");
        }

        var reference = ReadJson("phase-exec-algo-r003-r016-recommendation-reference.json");
        Assert.Equal("EXEC-SIM-R016", reference.RootElement.GetProperty("sourceRecommendationPhase").GetString());
        Assert.True(reference.RootElement.GetProperty("r016RecommendationsReused").GetBoolean());
        Assert.False(reference.RootElement.GetProperty("r016MetricsInventedInR003").GetBoolean());
        Assert.False(reference.RootElement.GetProperty("noNewBacktest").GetBoolean() is false);
        Assert.True(reference.RootElement.GetProperty("noNewSimulation").GetBoolean());
    }

    [Fact]
    public void Parameter_contract_and_versioning_are_non_executable()
    {
        var contract = ReadJson("phase-exec-algo-r003-session-aware-parameter-contract.json");
        var versioning = ReadJson("phase-exec-algo-r003-parameter-set-versioning.json");

        Assert.True(contract.RootElement.GetProperty("contractCreated").GetBoolean());
        Assert.Equal("ParameterContractReady", contract.RootElement.GetProperty("parameterContractStatus").GetString());
        Assert.Equal("EXEC-SIM-R016", contract.RootElement.GetProperty("sourceRecommendationPhase").GetString());
        Assert.Equal("USDPairOnly", contract.RootElement.GetProperty("appliesToExecutionUniverse").GetString());
        AssertContains(contract, "barRoles", "OpeningBuild");
        AssertContains(contract, "barRoles", "IntradayRebalance");
        AssertContains(contract, "barRoles", "ClosingFlatten");
        AssertContains(contract, "parameterCategories", "ResidualCrossThreshold");
        AssertContains(contract, "parameterCategories", "RequiredFeedQualityBucket");
        AssertContains(contract, "parameterStatuses", "RecommendedDesignOnly");
        AssertContains(contract, "safeReasonCategories", "DesignOnlyNotExecutable");
        Assert.True(contract.RootElement.GetProperty("isDesignOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("isPaperOnly").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("isExecutable").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("createsExecutableConfiguration").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("createsExecutionSchedule").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("createsOrders").GetBoolean());

        Assert.True(versioning.RootElement.GetProperty("versioningCreated").GetBoolean());
        Assert.Equal("1.0.0-design-only", versioning.RootElement.GetProperty("parameterSetVersion").GetString());
        Assert.False(versioning.RootElement.GetProperty("isExecutable").GetBoolean());
        Assert.Equal(3, versioning.RootElement.GetProperty("versionedParameterSets").GetArrayLength());
    }

    [Fact]
    public void All_bar_role_parameter_sets_are_design_only_paper_only_and_not_order_domain_outputs()
    {
        foreach (var artifact in new[]
                 {
                     "phase-exec-algo-r003-opening-build-parameter-set.json",
                     "phase-exec-algo-r003-intraday-rebalance-parameter-set.json",
                     "phase-exec-algo-r003-closing-flatten-parameter-set.json"
                 })
        {
            var parameterSet = ReadJson(artifact);
            Assert.True(parameterSet.RootElement.GetProperty("IsDesignOnly").GetBoolean());
            Assert.True(parameterSet.RootElement.GetProperty("IsPaperOnly").GetBoolean());
            Assert.False(parameterSet.RootElement.GetProperty("IsExecutable").GetBoolean());
            Assert.False(parameterSet.RootElement.GetProperty("IsSubmitted").GetBoolean());
            Assert.False(parameterSet.RootElement.GetProperty("HasBrokerRoute").GetBoolean());
            Assert.True(parameterSet.RootElement.GetProperty("NotAnOrder").GetBoolean());
            Assert.True(parameterSet.RootElement.GetProperty("NotSubmitted").GetBoolean());
            Assert.True(parameterSet.RootElement.GetProperty("NoBrokerRoute").GetBoolean());
            Assert.True(parameterSet.RootElement.GetProperty("RequiresManualApprovalForExecutableUse").GetBoolean());
            Assert.False(parameterSet.RootElement.GetProperty("CreatesExecutableSchedule").GetBoolean());
            Assert.False(parameterSet.RootElement.GetProperty("CreatesOrder").GetBoolean());
        }
    }

    [Fact]
    public void Opening_intraday_and_closing_parameter_sets_preserve_session_role_constraints()
    {
        var opening = ReadJson("phase-exec-algo-r003-opening-build-parameter-set.json");
        var intraday = ReadJson("phase-exec-algo-r003-intraday-rebalance-parameter-set.json");
        var closing = ReadJson("phase-exec-algo-r003-closing-flatten-parameter-set.json");

        Assert.Equal("OpeningBuild", opening.RootElement.GetProperty("AppliesToBarRole").GetString());
        Assert.True(opening.RootElement.GetProperty("TargetMayBeKnownPreviousEvening").GetBoolean());
        Assert.True(opening.RootElement.GetProperty("KnownAtTimestampSeparateFromEarliestExecutionTimestamp").GetBoolean());
        Assert.True(opening.RootElement.GetProperty("EarliestExecutionTimestampMustRemainSessionOpenOrExplicitAllowedStart").GetBoolean());
        Assert.True(opening.RootElement.GetProperty("PreviousEveningPreComputationOnly").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("OvernightExposureBeforeSessionStartAllowed").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("OrdersBeforeSessionStartAllowed").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("ForceBlindCrossingBecauseKnownPreviousEvening").GetBoolean());

        Assert.Equal("IntradayRebalance", intraday.RootElement.GetProperty("AppliesToBarRole").GetString());
        Assert.True(intraday.RootElement.GetProperty("NormalTargetVsCurrentRebalance").GetBoolean());
        Assert.True(intraday.RootElement.GetProperty("NormalCloseSeekingBehaviorPreserved").GetBoolean());
        Assert.True(intraday.RootElement.GetProperty("StandardTMinus13CloseSeekingBehavior").GetBoolean());
        Assert.Equal("Normal", intraday.RootElement.GetProperty("ResidualPenaltyBucket").GetString());
        Assert.True(intraday.RootElement.GetProperty("ControlledResidualCrossOnlyWhenOpportunityCostExceedsCrossingCost").GetBoolean());

        Assert.Equal("ClosingFlatten", closing.RootElement.GetProperty("AppliesToBarRole").GetString());
        Assert.Equal("Flat", closing.RootElement.GetProperty("TargetPosition").GetString());
        Assert.True(closing.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.Equal("NoOvernightCritical", closing.RootElement.GetProperty("ResidualPenaltyBucket").GetString());
        Assert.Equal("MustCompleteFlat", closing.RootElement.GetProperty("CompletionPriority").GetString());
        Assert.Equal("StrictlyLowerThanIntradayRebalance", closing.RootElement.GetProperty("MaxResidualAtClose").GetString());
        Assert.Equal("LowerThanOtherRoles", closing.RootElement.GetProperty("ResidualCrossThreshold").GetString());
        Assert.False(closing.RootElement.GetProperty("PureLimitUntilCloseDefaultAllowed").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("FiveMarketSlicesDefaultAllowed").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("AlwaysMarketAtCloseDefaultAllowed").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("BlindMarketCrossingAllowedWithoutCostJustification").GetBoolean());
    }

    [Fact]
    public void Validation_rules_policy_ladders_blocks_and_manual_review_triggers_are_present()
    {
        var rules = ReadJson("phase-exec-algo-r003-parameter-validation-rules.json");
        var recommendations = ReadJson("phase-exec-algo-r003-policy-recommendations-by-bar-role.json");
        var ladder = ReadJson("phase-exec-algo-r003-policy-fallback-ladder-by-bar-role.json");
        var blocked = ReadJson("phase-exec-algo-r003-blocked-policy-families.json");
        var manual = ReadJson("phase-exec-algo-r003-manual-review-triggers.json");

        Assert.True(rules.RootElement.GetProperty("validationRulesCreated").GetBoolean());
        Assert.True(rules.RootElement.GetProperty("allBarRolesHaveParameterSet").GetBoolean());
        Assert.True(rules.RootElement.GetProperty("allParameterSetsDesignOnly").GetBoolean());
        Assert.True(rules.RootElement.GetProperty("allParameterSetsPaperOnly").GetBoolean());
        Assert.True(rules.RootElement.GetProperty("allParameterSetsNonExecutable").GetBoolean());
        Assert.True(rules.RootElement.GetProperty("closingFlattenMaxResidualStricterThanIntraday").GetBoolean());
        Assert.False(rules.RootElement.GetProperty("openingBuildOvernightExposureAllowed").GetBoolean());
        Assert.False(rules.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());

        Assert.True(recommendations.RootElement.GetProperty("designOnly").GetBoolean());
        Assert.True(recommendations.RootElement.GetProperty("notExecutableConfiguration").GetBoolean());
        Assert.Contains(recommendations.RootElement.GetProperty("policyRecommendations").EnumerateArray(), x =>
            x.GetProperty("BarRole").GetString() == "ClosingFlatten" &&
            x.GetProperty("PreferredPolicyFamily").GetString() == "ControlledResidualCrossWhenJustified");
        AssertContains(ladder, "fallbackLadderLevels", "Preferred");
        AssertContains(ladder, "fallbackLadderLevels", "ManualReview");
        AssertContains(ladder, "fallbackLadderLevels", "DoNotTrade");
        AssertContains(blocked, "blockedPolicyFamilies", "PureLimitUntilCloseDefault");
        AssertContains(blocked, "blockedPolicyFamilies", "MechanicalMarketSlicesAroundClose");
        AssertContains(blocked, "blockedPolicyFamilies", "AlwaysMarketAtClose");
        AssertContains(blocked, "blockedPolicyFamilies", "BlindMarketCrossingWithoutCostJustification");
        Assert.False(blocked.RootElement.GetProperty("wakettPatternBlockWeakened").GetBoolean());
        AssertContains(manual, "manualReviewTriggers", "MissingCloseBenchmark");
        AssertContains(manual, "manualReviewTriggers", "ExtremeSpread");
        AssertContains(manual, "manualReviewTriggers", "ClosingFlattenResidualRisk");
        Assert.True(manual.RootElement.GetProperty("manualReviewIsNotAutomaticExecution").GetBoolean());
    }

    [Fact]
    public void Feed_benchmark_no_overnight_and_previous_evening_contracts_exist_by_role()
    {
        var feed = ReadJson("phase-exec-algo-r003-feed-quality-requirements-by-bar-role.json");
        var benchmark = ReadJson("phase-exec-algo-r003-close-benchmark-requirements-by-bar-role.json");
        var flatten = ReadJson("phase-exec-algo-r003-no-overnight-flatten-parameter-contract.json");
        var firstBar = ReadJson("phase-exec-algo-r003-first-bar-previous-evening-planning-contract.json");

        Assert.Equal(3, feed.RootElement.GetProperty("requirements").GetArrayLength());
        Assert.True(feed.RootElement.GetProperty("NoQuoteNearCloseTriggersManualReview").GetBoolean());
        Assert.True(feed.RootElement.GetProperty("StaleQuoteNearCloseTriggersManualReview").GetBoolean());
        Assert.Equal(3, benchmark.RootElement.GetProperty("requirements").GetArrayLength());
        Assert.True(benchmark.RootElement.GetProperty("MissingCloseBenchmarkTriggersManualReview").GetBoolean());
        Assert.True(benchmark.RootElement.GetProperty("MissingCloseBenchmarkBlocksBlindExecution").GetBoolean());

        Assert.Equal("ClosingFlatten", flatten.RootElement.GetProperty("BarRole").GetString());
        Assert.Equal("Flat", flatten.RootElement.GetProperty("TargetPosition").GetString());
        Assert.True(flatten.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.False(flatten.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.Equal("NoOvernightCritical", flatten.RootElement.GetProperty("ResidualPenaltyBucket").GetString());
        Assert.False(flatten.RootElement.GetProperty("PureLimitUntilCloseDefaultAllowed").GetBoolean());
        Assert.False(flatten.RootElement.GetProperty("AlwaysMarketAtCloseDefaultAllowed").GetBoolean());

        Assert.Equal("OpeningBuild", firstBar.RootElement.GetProperty("BarRole").GetString());
        Assert.True(firstBar.RootElement.GetProperty("FirstBarTargetKnownPreviousEveningSupported").GetBoolean());
        Assert.True(firstBar.RootElement.GetProperty("KnownAtTimestampUtcSeparateFromEarliestExecutionTimestampUtc").GetBoolean());
        Assert.False(firstBar.RootElement.GetProperty("PreviousEveningExecutionAllowed").GetBoolean());
        Assert.False(firstBar.RootElement.GetProperty("OvernightExposureBeforeSessionStartAllowed").GetBoolean());
        Assert.False(firstBar.RootElement.GetProperty("OrdersBeforeSessionStartAllowed").GetBoolean());
    }

    [Fact]
    public void Cost_nonmajor_usd_pair_direct_cross_wakett_usdjpy_and_audusd_are_preserved()
    {
        var cost = ReadJson("phase-exec-algo-r003-cost-guidance-by-bar-role.json");
        var nonMajor = ReadJson("phase-exec-algo-r003-nonmajor-calibration-preservation.json");
        var normalization = ReadJson("phase-exec-algo-r003-usd-pair-normalization-preservation.json");
        var directCross = ReadJson("phase-exec-algo-r003-direct-cross-exclusion-preservation.json");
        var wakett = ReadJson("phase-exec-algo-r003-wakett-pattern-block-preservation.json");
        var usdjpy = ReadJson("phase-exec-algo-r003-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-algo-r003-lmax-readonly-baseline-reference.json");

        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.Equal(3, cost.RootElement.GetProperty("costGuidanceByBarRole").GetArrayLength());
        Assert.True(nonMajor.RootElement.GetProperty("nonMajorEmScandiCnhRequireLiquidityCalibration").GetBoolean());
        Assert.True(nonMajor.RootElement.GetProperty("doNotExtrapolateEurusdUsdjpyAudusdResultsToNonMajor").GetBoolean());
        Assert.Equal("USD-pair-only", normalization.RootElement.GetProperty("executionUniverse").GetString());
        Assert.True(normalization.RootElement.GetProperty("mandatoryNettingBeforeExecution").GetBoolean());
        Assert.False(normalization.RootElement.GetProperty("normalizationWeakened").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.True(directCross.RootElement.GetProperty("directCrossSignalOnlyHandlingPreserved").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("guidanceWeakened").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("PureLimitUntilCloseDefaultAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("MechanicalMarketSlicesAroundCloseAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("AlwaysMarketAtCloseAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("BlindMarketCrossingWithoutCostJustificationAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("wakettPatternBlockWeakened").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("caveatPreserved").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("requiresInversion").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.False(usdjpy.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        Assert.Contains("not failed", lmax.RootElement.GetProperty("audusdStatus").GetString());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR003").GetBoolean());
    }

    [Fact]
    public void No_new_backtest_no_result_lines_no_api_runtime_or_order_audits_are_clean()
    {
        var nonExecutable = ReadJson("phase-exec-algo-r003-non-executable-parameter-contract-audit.json");
        var noBacktest = ReadJson("phase-exec-algo-r003-no-new-backtest-audit.json");
        var noLines = ReadJson("phase-exec-algo-r003-no-new-simulation-result-lines-audit.json");
        var api = ReadJson("phase-exec-algo-r003-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-algo-r003-no-broker-marketdata-runtime-audit.json");
        var fill = ReadJson("phase-exec-algo-r003-no-real-fill-audit.json");
        var report = ReadJson("phase-exec-algo-r003-no-execution-report-audit.json");
        var order = ReadJson("phase-exec-algo-r003-no-order-created-audit.json");
        var route = ReadJson("phase-exec-algo-r003-no-route-no-submission-audit.json");
        var noExternal = ReadJson("phase-exec-algo-r003-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-algo-r003-forbidden-actions-audit.json");

        Assert.True(nonExecutable.RootElement.GetProperty("allParameterSetsNonExecutable").GetBoolean());
        Assert.False(nonExecutable.RootElement.GetProperty("parameterSetExecutable").GetBoolean());
        Assert.False(nonExecutable.RootElement.GetProperty("executableConfigurationCreated").GetBoolean());
        Assert.False(nonExecutable.RootElement.GetProperty("executionScheduleCreated").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newSimulationExecuted").GetBoolean());
        Assert.False(noLines.RootElement.GetProperty("newSimulationResultLinesCreated").GetBoolean());
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
        "phase-exec-algo-r003-summary.md",
        "phase-exec-algo-r003-r016-recommendation-reference.json",
        "phase-exec-algo-r003-session-aware-parameter-contract.json",
        "phase-exec-algo-r003-parameter-set-versioning.json",
        "phase-exec-algo-r003-opening-build-parameter-set.json",
        "phase-exec-algo-r003-intraday-rebalance-parameter-set.json",
        "phase-exec-algo-r003-closing-flatten-parameter-set.json",
        "phase-exec-algo-r003-parameter-validation-rules.json",
        "phase-exec-algo-r003-policy-recommendations-by-bar-role.json",
        "phase-exec-algo-r003-policy-fallback-ladder-by-bar-role.json",
        "phase-exec-algo-r003-blocked-policy-families.json",
        "phase-exec-algo-r003-manual-review-triggers.json",
        "phase-exec-algo-r003-feed-quality-requirements-by-bar-role.json",
        "phase-exec-algo-r003-close-benchmark-requirements-by-bar-role.json",
        "phase-exec-algo-r003-no-overnight-flatten-parameter-contract.json",
        "phase-exec-algo-r003-first-bar-previous-evening-planning-contract.json",
        "phase-exec-algo-r003-cost-guidance-by-bar-role.json",
        "phase-exec-algo-r003-nonmajor-calibration-preservation.json",
        "phase-exec-algo-r003-usd-pair-normalization-preservation.json",
        "phase-exec-algo-r003-direct-cross-exclusion-preservation.json",
        "phase-exec-algo-r003-wakett-pattern-block-preservation.json",
        "phase-exec-algo-r003-non-executable-parameter-contract-audit.json",
        "phase-exec-algo-r003-no-new-backtest-audit.json",
        "phase-exec-algo-r003-no-new-simulation-result-lines-audit.json",
        "phase-exec-algo-r003-no-polygon-api-call-audit.json",
        "phase-exec-algo-r003-no-lmax-call-audit.json",
        "phase-exec-algo-r003-no-external-api-call-audit.json",
        "phase-exec-algo-r003-no-broker-marketdata-runtime-audit.json",
        "phase-exec-algo-r003-no-real-fill-audit.json",
        "phase-exec-algo-r003-no-execution-report-audit.json",
        "phase-exec-algo-r003-no-order-created-audit.json",
        "phase-exec-algo-r003-no-route-no-submission-audit.json",
        "phase-exec-algo-r003-usdjpy-caveat-preservation.json",
        "phase-exec-algo-r003-lmax-readonly-baseline-reference.json",
        "phase-exec-algo-r003-no-external-audit.json",
        "phase-exec-algo-r003-forbidden-actions-audit.json",
        "phase-exec-algo-r003-next-phase-recommendation.json"
    ];

    private static JsonDocument ReadJson(string fileName)
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(ArtifactsDir(), fileName)));

    private static string ArtifactsDir()
        => Path.Combine(RepoRoot(), "artifacts/readiness/execution-algo");

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
