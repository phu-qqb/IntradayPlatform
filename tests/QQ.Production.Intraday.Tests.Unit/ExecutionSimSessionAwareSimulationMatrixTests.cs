using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimSessionAwareSimulationMatrixTests
{
    [Fact]
    public void Required_r015_artifacts_exist()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R015 artifact {artifact}");
        }
    }

    [Fact]
    public void Session_aware_contract_and_bar_role_scenarios_are_ready()
    {
        var contract = ReadJson("phase-exec-sim-r015-session-aware-simulation-contract.json");
        var scenarios = ReadJson("phase-exec-sim-r015-bar-role-scenarios.json");

        Assert.Equal("EXEC-SIM-R014", contract.RootElement.GetProperty("sourceRequirementPhase").GetString());
        Assert.Equal("EXEC-SIM-R013", contract.RootElement.GetProperty("sourceTcaPhase").GetString());
        Assert.True(contract.RootElement.GetProperty("usesAcceptedOfflineQuoteFiles").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("noExternal").GetBoolean());
        AssertContains(contract, "barRoles", "OpeningBuild");
        AssertContains(contract, "barRoles", "IntradayRebalance");
        AssertContains(contract, "barRoles", "ClosingFlatten");
        Assert.True(contract.RootElement.GetProperty("outputsFixtureOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("outputsPaperOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("outputsNonExecutable").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("simulationResultLinesAreFills").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());

        Assert.Equal(9, scenarios.RootElement.GetProperty("perInstrumentScenarioCount").GetInt32());
        Assert.False(scenarios.RootElement.GetProperty("directCrossExecutionIncluded").GetBoolean());
        Assert.Contains(scenarios.RootElement.GetProperty("barRoles").EnumerateArray(), x => x.GetProperty("BarRole").GetString() == "OpeningBuild");
        Assert.Contains(scenarios.RootElement.GetProperty("barRoles").EnumerateArray(), x => x.GetProperty("BarRole").GetString() == "IntradayRebalance");
        Assert.Contains(scenarios.RootElement.GetProperty("barRoles").EnumerateArray(), x => x.GetProperty("BarRole").GetString() == "ClosingFlatten");
    }

    [Fact]
    public void Opening_intraday_and_closing_scenarios_preserve_required_bar_role_semantics()
    {
        var opening = ReadJson("phase-exec-sim-r015-opening-build-scenarios.json");
        var intraday = ReadJson("phase-exec-sim-r015-intraday-rebalance-scenarios.json");
        var closing = ReadJson("phase-exec-sim-r015-closing-flatten-scenarios.json");

        Assert.Equal("OpeningBuild", opening.RootElement.GetProperty("BarRole").GetString());
        Assert.True(opening.RootElement.GetProperty("KnownAtTimestampAndEarliestExecutionAreDistinct").GetBoolean());
        Assert.True(opening.RootElement.GetProperty("PreComputationPreviousEveningAllowed").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("ExecutionBeforeSessionStartAllowed").GetBoolean());
        foreach (var scenario in opening.RootElement.GetProperty("scenarios").EnumerateArray())
        {
            Assert.Equal("Flat", scenario.GetProperty("CurrentPosition").GetString());
            Assert.Equal("High", scenario.GetProperty("ExpectedTurnoverBucket").GetString());
            Assert.Equal("Elevated", scenario.GetProperty("ResidualPenaltyBucket").GetString());
            Assert.False(scenario.GetProperty("OvernightExposureAuthorized").GetBoolean());
        }

        Assert.Equal("IntradayRebalance", intraday.RootElement.GetProperty("BarRole").GetString());
        Assert.True(intraday.RootElement.GetProperty("NormalCloseSeeking15mBehaviorPreserved").GetBoolean());
        foreach (var scenario in intraday.RootElement.GetProperty("scenarios").EnumerateArray())
        {
            Assert.Equal("Normal", scenario.GetProperty("ResidualPenaltyBucket").GetString());
            Assert.Equal("TargetPositionMinusCurrentPosition", scenario.GetProperty("DeltaDefinition").GetString());
        }

        Assert.Equal("ClosingFlatten", closing.RootElement.GetProperty("BarRole").GetString());
        Assert.True(closing.RootElement.GetProperty("PureLimitUntilCloseDefaultBlocked").GetBoolean());
        Assert.True(closing.RootElement.GetProperty("FiveMarketSlicesDefaultBlocked").GetBoolean());
        Assert.True(closing.RootElement.GetProperty("AlwaysMarketAtCloseDefaultBlocked").GetBoolean());
        foreach (var scenario in closing.RootElement.GetProperty("scenarios").EnumerateArray())
        {
            Assert.Equal("Flat", scenario.GetProperty("TargetPosition").GetString());
            Assert.True(scenario.GetProperty("MustEndFlat").GetBoolean());
            Assert.False(scenario.GetProperty("OvernightAllowed").GetBoolean());
            Assert.Equal("NoOvernightCritical", scenario.GetProperty("ResidualPenaltyBucket").GetString());
        }
    }

    [Fact]
    public void Tca_buckets_and_per_instrument_bar_role_reports_are_present()
    {
        var buckets = ReadJson("phase-exec-sim-r015-session-aware-tca-buckets.json");
        AssertContains(buckets, "tcaBuckets", "OpeningBuildTca");
        AssertContains(buckets, "tcaBuckets", "IntradayRebalanceTca");
        AssertContains(buckets, "tcaBuckets", "ClosingFlattenTca");
        AssertContains(buckets, "tcaBuckets", "SessionAggregateTca");
        AssertContains(buckets, "ResidualPenaltyBucket", "NoOvernightCritical");
        Assert.True(buckets.RootElement.GetProperty("firstLastBarsReportedSeparately").GetBoolean());

        AssertInstrumentReport("phase-exec-sim-r015-per-instrument-bar-role-eurusd-report.json", "EURUSD", false);
        AssertInstrumentReport("phase-exec-sim-r015-per-instrument-bar-role-usdjpy-report.json", "USDJPY", true);
        AssertInstrumentReport("phase-exec-sim-r015-per-instrument-bar-role-audusd-report.json", "AUDUSD", false);
    }

    [Fact]
    public void Policy_results_rankings_and_role_reports_are_fixture_only_not_order_domain_outputs()
    {
        var policy = ReadJson("phase-exec-sim-r015-session-aware-policy-results.json");
        Assert.True(policy.RootElement.GetProperty("allExpectedPoliciesComparedByBarRole").GetBoolean());
        Assert.False(policy.RootElement.GetProperty("simulationResultLinesAreFills").GetBoolean());
        Assert.False(policy.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());
        Assert.False(policy.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(policy.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(policy.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(policy.RootElement.GetProperty("submissionsCreated").GetBoolean());
        foreach (var row in policy.RootElement.GetProperty("policyResults").EnumerateArray())
        {
            Assert.True(row.GetProperty("FixtureOnly").GetBoolean());
            Assert.True(row.GetProperty("PaperOnly").GetBoolean());
            Assert.True(row.GetProperty("NonExecutable").GetBoolean());
            Assert.True(row.GetProperty("NotAnOrder").GetBoolean());
            Assert.True(row.GetProperty("NoRealFill").GetBoolean());
            Assert.True(row.GetProperty("NoExecutionReport").GetBoolean());
        }

        foreach (var file in RankingArtifacts)
        {
            var ranking = ReadJson(file);
            Assert.NotEmpty(ranking.RootElement.GetProperty("rankingsByBarRole").EnumerateArray());
        }

        var closingReport = ReadJson("phase-exec-sim-r015-closing-flatten-tca-report.json");
        Assert.True(closingReport.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.Equal("NoOvernightCritical", closingReport.RootElement.GetProperty("ResidualPenaltyBucket").GetString());
        Assert.True(closingReport.RootElement.GetProperty("FiveMarketSlicesStillBlocked").GetBoolean());
        Assert.True(closingReport.RootElement.GetProperty("AlwaysMarketAtCloseDefaultBlocked").GetBoolean());
    }

    [Fact]
    public void No_overnight_wakett_close_seeking_and_cost_outputs_are_session_aware()
    {
        var noOvernight = ReadJson("phase-exec-sim-r015-no-overnight-residual-penalty-report.json");
        var wakett = ReadJson("phase-exec-sim-r015-wakett-session-aware-risk-report.json");
        var closeSeeking = ReadJson("phase-exec-sim-r015-close-seeking-session-aware-comparison.json");
        var cost = ReadJson("phase-exec-sim-r015-major-pair-5usd-bestcase-by-bar-role.json");

        Assert.False(noOvernight.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.True(noOvernight.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.True(noOvernight.RootElement.GetProperty("ClosingFlattenResidualMoreExpensive").GetBoolean());
        Assert.True(noOvernight.RootElement.GetProperty("ManualEscalationRequiredIfClosingResidual").GetBoolean());
        Assert.True(wakett.RootElement.GetProperty("PureLimitUntilClose").GetProperty("blockedAsDefault").GetBoolean());
        Assert.True(wakett.RootElement.GetProperty("WakettFiveMarketSlicesAroundClose").GetProperty("RepeatedSpreadCrossing").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("AlwaysMarketAtCloseDefaultAllowed").GetBoolean());
        Assert.Equal("CloseSeeking15mAdaptive", closeSeeking.RootElement.GetProperty("OpeningBuild").GetProperty("preferredPolicy").GetString());
        Assert.Equal("ControlledResidualCrossWhenJustified", closeSeeking.RootElement.GetProperty("ClosingFlatten").GetProperty("preferredPolicy").GetString());
        Assert.True(closeSeeking.RootElement.GetProperty("barRoleSpecificThresholdsRequired").GetBoolean());
        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.Equal(3, cost.RootElement.GetProperty("reportedByBarRole").GetArrayLength());
    }

    [Fact]
    public void Known_previous_evening_no_overnight_direct_cross_nonmajor_and_caveats_are_preserved()
    {
        var opening = ReadJson("phase-exec-sim-r015-opening-build-known-previous-evening-preservation.json");
        var noOvernight = ReadJson("phase-exec-sim-r015-no-overnight-flat-constraint-preservation.json");
        var directCross = ReadJson("phase-exec-sim-r015-direct-cross-exclusion-preservation.json");
        var nonMajor = ReadJson("phase-exec-sim-r015-nonmajor-calibration-preservation.json");
        var usdjpy = ReadJson("phase-exec-sim-r015-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r015-lmax-readonly-baseline-reference.json");

        Assert.True(opening.RootElement.GetProperty("KnownAtTimestampUtcMayBePreviousEvening").GetBoolean());
        Assert.True(opening.RootElement.GetProperty("EarliestExecutionTimestampUtcSeparateFromKnownAtTimestampUtc").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("OvernightExposureBeforeSessionStartAllowed").GetBoolean());
        Assert.False(noOvernight.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.True(noOvernight.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("directCrossIncludedInSessionMatrix").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("guidanceWeakened").GetBoolean());
        Assert.True(nonMajor.RootElement.GetProperty("nonMajorEmScandiCnhRequireLiquidityCalibration").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("caveatPreserved").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("requiresInversion").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.Contains("not failed", lmax.RootElement.GetProperty("audusdStatus").GetString());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR015").GetBoolean());
    }

    [Fact]
    public void No_external_runtime_order_fill_report_route_audits_are_clean()
    {
        var api = ReadJson("phase-exec-sim-r015-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r015-no-broker-marketdata-runtime-audit.json");
        var fill = ReadJson("phase-exec-sim-r015-no-real-fill-audit.json");
        var report = ReadJson("phase-exec-sim-r015-no-execution-report-audit.json");
        var order = ReadJson("phase-exec-sim-r015-no-order-created-audit.json");
        var route = ReadJson("phase-exec-sim-r015-no-route-no-submission-audit.json");
        var noExternal = ReadJson("phase-exec-sim-r015-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r015-forbidden-actions-audit.json");

        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("brokerActivationDetected").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("socketOpened").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("schedulerServiceTimerPollingBackgroundJobIntroduced").GetBoolean());
        Assert.False(fill.RootElement.GetProperty("simulationResultLinesAreFills").GetBoolean());
        Assert.False(fill.RootElement.GetProperty("realFillsCreated").GetBoolean());
        Assert.False(fill.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());
        Assert.False(report.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("executableOrdersCreated").GetBoolean());
        Assert.False(route.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(route.RootElement.GetProperty("submissionsCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("ordersFillsReportsRoutesSubmissionsCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("liveBrokerProductionTradingStateMutated").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    private static void AssertInstrumentReport(string fileName, string symbol, bool requiresInversion)
    {
        var report = ReadJson(fileName);
        Assert.Equal(symbol, report.RootElement.GetProperty("ExecutionTradableSymbol").GetString());
        Assert.Equal(requiresInversion, report.RootElement.GetProperty("RequiresInversion").GetBoolean());
        Assert.Equal(3, report.RootElement.GetProperty("barRoleReports").GetArrayLength());
        Assert.Contains(report.RootElement.GetProperty("barRoleReports").EnumerateArray(), x => x.GetProperty("BarRole").GetString() == "OpeningBuild");
        Assert.Contains(report.RootElement.GetProperty("barRoleReports").EnumerateArray(), x => x.GetProperty("BarRole").GetString() == "IntradayRebalance");
        Assert.Contains(report.RootElement.GetProperty("barRoleReports").EnumerateArray(), x => x.GetProperty("BarRole").GetString() == "ClosingFlatten");
        Assert.True(report.RootElement.GetProperty("FixtureOnly").GetBoolean());
        Assert.True(report.RootElement.GetProperty("PaperOnly").GetBoolean());
        Assert.True(report.RootElement.GetProperty("NoRealFill").GetBoolean());
        Assert.True(report.RootElement.GetProperty("NoExecutionReport").GetBoolean());
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] RankingArtifacts =
    [
        "phase-exec-sim-r015-policy-ranking-by-bar-role-median-slippage.json",
        "phase-exec-sim-r015-policy-ranking-by-bar-role-p95-slippage.json",
        "phase-exec-sim-r015-policy-ranking-by-bar-role-fill-ratio.json",
        "phase-exec-sim-r015-policy-ranking-by-bar-role-residual.json",
        "phase-exec-sim-r015-policy-ranking-by-bar-role-spread-paid.json"
    ];

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r015-summary.md",
        "phase-exec-sim-r015-session-aware-simulation-contract.json",
        "phase-exec-sim-r015-bar-role-scenarios.json",
        "phase-exec-sim-r015-opening-build-scenarios.json",
        "phase-exec-sim-r015-intraday-rebalance-scenarios.json",
        "phase-exec-sim-r015-closing-flatten-scenarios.json",
        "phase-exec-sim-r015-session-aware-policy-results.json",
        "phase-exec-sim-r015-session-aware-tca-buckets.json",
        "phase-exec-sim-r015-opening-build-tca-report.json",
        "phase-exec-sim-r015-intraday-rebalance-tca-report.json",
        "phase-exec-sim-r015-closing-flatten-tca-report.json",
        "phase-exec-sim-r015-session-aggregate-tca-report.json",
        "phase-exec-sim-r015-per-instrument-bar-role-eurusd-report.json",
        "phase-exec-sim-r015-per-instrument-bar-role-usdjpy-report.json",
        "phase-exec-sim-r015-per-instrument-bar-role-audusd-report.json",
        "phase-exec-sim-r015-policy-ranking-by-bar-role-median-slippage.json",
        "phase-exec-sim-r015-policy-ranking-by-bar-role-p95-slippage.json",
        "phase-exec-sim-r015-policy-ranking-by-bar-role-fill-ratio.json",
        "phase-exec-sim-r015-policy-ranking-by-bar-role-residual.json",
        "phase-exec-sim-r015-policy-ranking-by-bar-role-spread-paid.json",
        "phase-exec-sim-r015-no-overnight-residual-penalty-report.json",
        "phase-exec-sim-r015-wakett-session-aware-risk-report.json",
        "phase-exec-sim-r015-close-seeking-session-aware-comparison.json",
        "phase-exec-sim-r015-opening-build-known-previous-evening-preservation.json",
        "phase-exec-sim-r015-no-overnight-flat-constraint-preservation.json",
        "phase-exec-sim-r015-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r015-major-pair-5usd-bestcase-by-bar-role.json",
        "phase-exec-sim-r015-nonmajor-calibration-preservation.json",
        "phase-exec-sim-r015-no-polygon-api-call-audit.json",
        "phase-exec-sim-r015-no-lmax-call-audit.json",
        "phase-exec-sim-r015-no-external-api-call-audit.json",
        "phase-exec-sim-r015-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r015-no-real-fill-audit.json",
        "phase-exec-sim-r015-no-execution-report-audit.json",
        "phase-exec-sim-r015-no-order-created-audit.json",
        "phase-exec-sim-r015-no-route-no-submission-audit.json",
        "phase-exec-sim-r015-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r015-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r015-no-external-audit.json",
        "phase-exec-sim-r015-forbidden-actions-audit.json",
        "phase-exec-sim-r015-next-phase-recommendation.json"
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
