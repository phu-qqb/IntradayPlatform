using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionAlgoScheduleShapeReviewHandoffTests
{
    [Fact]
    public void Required_r006_artifacts_exist_and_reference_r005()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R006 artifact {artifact}");
        }

        var reference = ReadJson("phase-exec-algo-r006-r005-schedule-shape-reference.json");
        Assert.Equal("EXEC-ALGO-R005", reference.RootElement.GetProperty("sourcePhase").GetString());
        Assert.True(reference.RootElement.GetProperty("r005ScheduleShapesReferenced").GetBoolean());
        Assert.True(reference.RootElement.GetProperty("reviewHandoffOnly").GetBoolean());
        Assert.True(reference.RootElement.GetProperty("noSimulation").GetBoolean());
        Assert.True(reference.RootElement.GetProperty("noBacktest").GetBoolean());
    }

    [Fact]
    public void Operator_review_contract_report_statuses_and_actions_exist()
    {
        var contract = ReadJson("phase-exec-algo-r006-schedule-shape-operator-review-contract.json");
        var report = ReadJson("phase-exec-algo-r006-schedule-shape-operator-review-report.json");
        var statuses = ReadJson("phase-exec-algo-r006-review-statuses.json");
        var actions = ReadJson("phase-exec-algo-r006-review-actions.json");

        Assert.True(File.Exists(Path.Combine(ArtifactsDir(), "phase-exec-algo-r006-schedule-shape-operator-review-report.md")));
        Assert.True(contract.RootElement.GetProperty("operatorReviewContractCreated").GetBoolean());
        AssertContains(contract, "requiredFields", "ScheduleShapeReviewId");
        AssertContains(contract, "requiredFields", "SimulationHandoffStatus");
        Assert.False(contract.RootElement.GetProperty("isExecutable").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("isOrder").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("hasBrokerRoute").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("hasChildOrders").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("hasExecutableSlices").GetBoolean());

        Assert.True(report.RootElement.GetProperty("operatorReviewReportCreated").GetBoolean());
        AssertContains(report, "eligibleForSimulationHandoff", "OpeningBuild");
        AssertContains(report, "eligibleForSimulationHandoff", "ClosingFlatten");
        AssertContains(report, "ineligibleForSimulationHandoff", "DirectCrossBlocked");
        Assert.True(report.RootElement.GetProperty("scheduleShapesAreNotOrders").GetBoolean());
        Assert.True(report.RootElement.GetProperty("schedulePhasesAreNotChildSlices").GetBoolean());
        Assert.False(report.RootElement.GetProperty("executionAuthorized").GetBoolean());
        AssertContains(statuses, "statuses", "AcceptedForSimulationOnly");
        AssertContains(statuses, "statuses", "HeldForManualReview");
        AssertContains(statuses, "statuses", "RejectedDirectCross");
        AssertContains(actions, "actions", "AcceptForSimulationHandoff");
        AssertContains(actions, "actions", "HoldForManualReview");
        AssertContains(actions, "actions", "RequestInstrumentConventionFix");
    }

    [Fact]
    public void Simulation_handoff_contract_and_eligible_ineligible_sets_are_safe()
    {
        var contract = ReadJson("phase-exec-algo-r006-simulation-handoff-contract.json");
        var statuses = ReadJson("phase-exec-algo-r006-simulation-handoff-statuses.json");
        var eligible = ReadJson("phase-exec-algo-r006-eligible-simulation-handoff-shapes.json");
        var ineligible = ReadJson("phase-exec-algo-r006-ineligible-simulation-handoff-shapes.json");

        Assert.True(contract.RootElement.GetProperty("simulationHandoffContractCreated").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("isForSimulationOnly").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("isExecutable").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("isOrder").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("isSubmitted").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("hasBrokerRoute").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("hasChildOrders").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("hasExecutableSlices").GetBoolean());
        Assert.Equal("EXEC-SIM-R017", contract.RootElement.GetProperty("intendedNextPhase").GetString());
        AssertContains(statuses, "statuses", "SimulationHandoffReadyNoExternal");
        AssertContains(statuses, "statuses", "SimulationHandoffBlockedDirectCross");
        Assert.True(eligible.RootElement.GetProperty("allEligibleShapesDesignOnly").GetBoolean());
        Assert.True(eligible.RootElement.GetProperty("allEligibleShapesSimulationOnly").GetBoolean());
        Assert.True(eligible.RootElement.GetProperty("allEligibleShapesNonExecutable").GetBoolean());
        Assert.Contains(eligible.RootElement.GetProperty("eligibleShapes").EnumerateArray(), x =>
            x.GetProperty("BarRole").GetString() == "OpeningBuild" &&
            x.GetProperty("ReviewStatus").GetString() == "AcceptedForSimulationOnly");
        Assert.Contains(eligible.RootElement.GetProperty("eligibleShapes").EnumerateArray(), x =>
            x.TryGetProperty("MustEndFlat", out var mustEndFlat) && mustEndFlat.GetBoolean() &&
            x.TryGetProperty("OvernightAllowed", out var overnight) && !overnight.GetBoolean());
        Assert.Contains(ineligible.RootElement.GetProperty("ineligibleShapes").EnumerateArray(), x =>
            x.GetProperty("ReasonCategory").GetString() == "DirectCrossExecutionDisabled" &&
            x.GetProperty("SimulationHandoffStatus").GetString() == "SimulationHandoffBlockedDirectCross");
        Assert.False(ineligible.RootElement.GetProperty("anyExecutableShapeEligible").GetBoolean());
    }

    [Fact]
    public void Accepted_held_rejected_direct_cross_and_benchmark_examples_are_correct()
    {
        var accepted = ReadJson("phase-exec-algo-r006-accepted-simulation-handoff-examples.json");
        var held = ReadJson("phase-exec-algo-r006-held-manual-review-examples.json");
        var rejected = ReadJson("phase-exec-algo-r006-rejected-unsafe-shape-examples.json");
        var direct = ReadJson("phase-exec-algo-r006-direct-cross-blocked-handoff.json");
        var benchmark = ReadJson("phase-exec-algo-r006-benchmark-only-handoff.json");

        Assert.True(accepted.RootElement.GetProperty("acceptedExamplesCreated").GetBoolean());
        foreach (var example in accepted.RootElement.GetProperty("examples").EnumerateArray())
        {
            Assert.Equal("SimulationHandoffReadyNoExternal", example.GetProperty("HandoffStatus").GetString());
            Assert.True(example.GetProperty("IsForSimulationOnly").GetBoolean());
            Assert.False(example.GetProperty("IsExecutable").GetBoolean());
            Assert.False(example.GetProperty("IsOrder").GetBoolean());
            Assert.False(example.GetProperty("HasBrokerRoute").GetBoolean());
            Assert.False(example.GetProperty("HasChildOrders").GetBoolean());
            Assert.False(example.GetProperty("HasExecutableSlices").GetBoolean());
        }

        Assert.True(held.RootElement.GetProperty("heldExamplesCreated").GetBoolean());
        Assert.False(held.RootElement.GetProperty("heldExamplesCreateExecutableSchedules").GetBoolean());
        Assert.Contains(held.RootElement.GetProperty("examples").EnumerateArray(), x =>
            x.GetProperty("ReviewStatus").GetString() == "HeldForManualReview" &&
            x.GetProperty("ReasonCategory").GetString() == "UnsafeFeed");
        Assert.True(rejected.RootElement.GetProperty("rejectedExamplesCreated").GetBoolean());
        Assert.False(rejected.RootElement.GetProperty("rejectedExamplesCreateOrderDomainObjects").GetBoolean());
        Assert.True(direct.RootElement.GetProperty("directCrossBlockedHandoffCreated").GetBoolean());
        Assert.Equal("SimulationHandoffBlockedDirectCross", direct.RootElement.GetProperty("SimulationHandoffStatus").GetString());
        Assert.True(direct.RootElement.GetProperty("RequiresNettingFirst").GetBoolean());
        Assert.True(direct.RootElement.GetProperty("DirectCrossSignalOnlyHandlingPreserved").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("IsExecutable").GetBoolean());
        Assert.True(benchmark.RootElement.GetProperty("benchmarkOnlyHandoffCreated").GetBoolean());
        Assert.True(benchmark.RootElement.GetProperty("BenchmarkOnly").GetBoolean());
        Assert.False(benchmark.RootElement.GetProperty("IsExecutable").GetBoolean());
        Assert.False(benchmark.RootElement.GetProperty("HasBrokerRoute").GetBoolean());
        Assert.False(benchmark.RootElement.GetProperty("HasChildOrders").GetBoolean());
    }

    [Fact]
    public void Preservation_artifacts_keep_opening_closing_usdjpy_cost_nonmajor_usd_pair_direct_cross_and_wakett_guards()
    {
        var opening = ReadJson("phase-exec-algo-r006-opening-build-review-preservation.json");
        var closing = ReadJson("phase-exec-algo-r006-closing-flatten-review-preservation.json");
        var usdjpyHandoff = ReadJson("phase-exec-algo-r006-usdjpy-inverted-handoff-preservation.json");
        var cost = ReadJson("phase-exec-algo-r006-cost-guidance-preservation.json");
        var nonmajor = ReadJson("phase-exec-algo-r006-nonmajor-calibration-preservation.json");
        var normalization = ReadJson("phase-exec-algo-r006-usd-pair-normalization-preservation.json");
        var directCross = ReadJson("phase-exec-algo-r006-direct-cross-exclusion-preservation.json");
        var wakett = ReadJson("phase-exec-algo-r006-wakett-pattern-block-preservation.json");

        Assert.True(opening.RootElement.GetProperty("KnownAtTimestampMayBePreviousEvening").GetBoolean());
        Assert.True(opening.RootElement.GetProperty("EarliestExecutionTimestampMustRemainSessionStartOrExplicitAllowedStart").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("PreSessionOrderAllowed").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("OvernightExposureBeforeSessionStartAllowed").GetBoolean());
        Assert.True(closing.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.Equal("NoOvernightCritical", closing.RootElement.GetProperty("ResidualPenaltyBucket").GetString());
        Assert.False(closing.RootElement.GetProperty("BlindMarketFallbackAllowed").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("AlwaysMarketAtCloseDefaultAllowed").GetBoolean());
        Assert.Equal("JPYUSD", usdjpyHandoff.RootElement.GetProperty("PortfolioNormalizedSymbol").GetString());
        Assert.Equal("USDJPY", usdjpyHandoff.RootElement.GetProperty("ExecutionTradableSymbol").GetString());
        Assert.True(usdjpyHandoff.RootElement.GetProperty("RequiresInversion").GetBoolean());
        Assert.Equal("4004", usdjpyHandoff.RootElement.GetProperty("SecurityID").GetString());
        Assert.Equal("8", usdjpyHandoff.RootElement.GetProperty("SecurityIDSource").GetString());
        Assert.False(usdjpyHandoff.RootElement.GetProperty("IsExecutable").GetBoolean());
        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(nonmajor.RootElement.GetProperty("nonMajorEmScandiCnhRequireLiquidityCalibration").GetBoolean());
        Assert.Equal("USD-pair-only", normalization.RootElement.GetProperty("executionUniverse").GetString());
        Assert.False(normalization.RootElement.GetProperty("normalizationWeakened").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("guidanceWeakened").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("PureLimitUntilCloseDefaultAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("AlwaysMarketAtCloseAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("wakettPatternBlockWeakened").GetBoolean());
    }

    [Fact]
    public void No_executable_handoff_schedule_slices_child_orders_backtest_api_runtime_or_order_audits_are_clean()
    {
        var handoff = ReadJson("phase-exec-algo-r006-non-executable-handoff-audit.json");
        var schedule = ReadJson("phase-exec-algo-r006-no-executable-schedule-audit.json");
        var slices = ReadJson("phase-exec-algo-r006-no-child-slices-audit.json");
        var childOrders = ReadJson("phase-exec-algo-r006-no-child-orders-audit.json");
        var noBacktest = ReadJson("phase-exec-algo-r006-no-new-backtest-audit.json");
        var api = ReadJson("phase-exec-algo-r006-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-algo-r006-no-broker-marketdata-runtime-audit.json");
        var fill = ReadJson("phase-exec-algo-r006-no-real-fill-audit.json");
        var report = ReadJson("phase-exec-algo-r006-no-execution-report-audit.json");
        var order = ReadJson("phase-exec-algo-r006-no-order-created-audit.json");
        var route = ReadJson("phase-exec-algo-r006-no-route-no-submission-audit.json");
        var noExternal = ReadJson("phase-exec-algo-r006-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-algo-r006-forbidden-actions-audit.json");
        var usdjpy = ReadJson("phase-exec-algo-r006-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-algo-r006-lmax-readonly-baseline-reference.json");

        Assert.True(handoff.RootElement.GetProperty("allHandoffsSimulationOnly").GetBoolean());
        Assert.True(handoff.RootElement.GetProperty("allHandoffsNonExecutable").GetBoolean());
        Assert.False(handoff.RootElement.GetProperty("simulationHandoffExecutable").GetBoolean());
        Assert.False(handoff.RootElement.GetProperty("simulationHandoffCreatesOrder").GetBoolean());
        Assert.False(schedule.RootElement.GetProperty("executableScheduleCreated").GetBoolean());
        Assert.False(slices.RootElement.GetProperty("childSlicesCreated").GetBoolean());
        Assert.False(slices.RootElement.GetProperty("orderDomainSliceObjectsCreated").GetBoolean());
        Assert.False(childOrders.RootElement.GetProperty("childOrdersCreated").GetBoolean());
        Assert.False(childOrders.RootElement.GetProperty("omsChildOrdersCreated").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newSimulationExecuted").GetBoolean());
        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("brokerActivationDetected").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("socketOpened").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(fill.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());
        Assert.False(report.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("omsChildOrdersCreated").GetBoolean());
        Assert.False(route.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(route.RootElement.GetProperty("submissionsCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("newSimulationExecuted").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("executableScheduleCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("childOrdersCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("ordersFillsReportsRoutesSubmissionsCreated").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("caveatPreserved").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("requiresInversion").GetBoolean());
        Assert.False(usdjpy.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        Assert.Contains("not failed", lmax.RootElement.GetProperty("audusdStatus").GetString());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR006").GetBoolean());
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-algo-r006-summary.md",
        "phase-exec-algo-r006-r005-schedule-shape-reference.json",
        "phase-exec-algo-r006-schedule-shape-operator-review-contract.json",
        "phase-exec-algo-r006-schedule-shape-operator-review-report.md",
        "phase-exec-algo-r006-schedule-shape-operator-review-report.json",
        "phase-exec-algo-r006-review-statuses.json",
        "phase-exec-algo-r006-review-actions.json",
        "phase-exec-algo-r006-simulation-handoff-contract.json",
        "phase-exec-algo-r006-simulation-handoff-statuses.json",
        "phase-exec-algo-r006-eligible-simulation-handoff-shapes.json",
        "phase-exec-algo-r006-ineligible-simulation-handoff-shapes.json",
        "phase-exec-algo-r006-accepted-simulation-handoff-examples.json",
        "phase-exec-algo-r006-held-manual-review-examples.json",
        "phase-exec-algo-r006-rejected-unsafe-shape-examples.json",
        "phase-exec-algo-r006-direct-cross-blocked-handoff.json",
        "phase-exec-algo-r006-benchmark-only-handoff.json",
        "phase-exec-algo-r006-opening-build-review-preservation.json",
        "phase-exec-algo-r006-closing-flatten-review-preservation.json",
        "phase-exec-algo-r006-usdjpy-inverted-handoff-preservation.json",
        "phase-exec-algo-r006-cost-guidance-preservation.json",
        "phase-exec-algo-r006-nonmajor-calibration-preservation.json",
        "phase-exec-algo-r006-usd-pair-normalization-preservation.json",
        "phase-exec-algo-r006-direct-cross-exclusion-preservation.json",
        "phase-exec-algo-r006-wakett-pattern-block-preservation.json",
        "phase-exec-algo-r006-non-executable-handoff-audit.json",
        "phase-exec-algo-r006-no-executable-schedule-audit.json",
        "phase-exec-algo-r006-no-child-slices-audit.json",
        "phase-exec-algo-r006-no-child-orders-audit.json",
        "phase-exec-algo-r006-no-new-backtest-audit.json",
        "phase-exec-algo-r006-no-polygon-api-call-audit.json",
        "phase-exec-algo-r006-no-lmax-call-audit.json",
        "phase-exec-algo-r006-no-external-api-call-audit.json",
        "phase-exec-algo-r006-no-broker-marketdata-runtime-audit.json",
        "phase-exec-algo-r006-no-real-fill-audit.json",
        "phase-exec-algo-r006-no-execution-report-audit.json",
        "phase-exec-algo-r006-no-order-created-audit.json",
        "phase-exec-algo-r006-no-route-no-submission-audit.json",
        "phase-exec-algo-r006-usdjpy-caveat-preservation.json",
        "phase-exec-algo-r006-lmax-readonly-baseline-reference.json",
        "phase-exec-algo-r006-no-external-audit.json",
        "phase-exec-algo-r006-forbidden-actions-audit.json",
        "phase-exec-algo-r006-next-phase-recommendation.json"
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
