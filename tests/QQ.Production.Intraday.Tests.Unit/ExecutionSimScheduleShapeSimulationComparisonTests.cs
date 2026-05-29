using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimScheduleShapeSimulationComparisonTests
{
    [Fact]
    public void Required_r018_artifacts_exist_and_consume_r017_acceptance()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R018 artifact {artifact}");
        }

        var reference = ReadJson("phase-exec-sim-r018-r017-handoff-acceptance-reference.json");
        Assert.Equal("EXEC-SIM-R017", reference.RootElement.GetProperty("sourceAcceptancePhase").GetString());
        Assert.True(reference.RootElement.GetProperty("r017HandoffAcceptanceConsumed").GetBoolean());
        Assert.True(reference.RootElement.GetProperty("heldOrRejectedShapesExcluded").GetBoolean());
        Assert.True(reference.RootElement.GetProperty("noExternal").GetBoolean());
        AssertContains(reference, "acceptedShapesConsumed", "OpeningBuild");
        AssertContains(reference, "acceptedShapesConsumed", "BenchmarkOnly");
    }

    [Fact]
    public void Simulation_contract_run_result_and_result_lines_are_fixture_only_non_order_domain_outputs()
    {
        var contract = ReadJson("phase-exec-sim-r018-schedule-shape-simulation-contract.json");
        var run = ReadJson("phase-exec-sim-r018-schedule-shape-simulation-run-result.json");
        var lines = ReadJson("phase-exec-sim-r018-schedule-shape-simulation-result-lines.json");

        Assert.True(contract.RootElement.GetProperty("scheduleShapeSimulationContractCreated").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("simulationMayProduceFixtureOnlyTcaLines").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("simulationMayCreateOrders").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("simulationMayCreateFills").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("simulationMayCreateExecutionReports").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("simulationMayCreateExecutableSchedules").GetBoolean());
        AssertContains(contract, "requiredResultLineFields", "IsFill");
        AssertContains(contract, "requiredResultLineFields", "NoOvernightResidualPenalty");

        Assert.Equal("CompletedFixtureOnlyScheduleShapeComparison", run.RootElement.GetProperty("SimulationStatus").GetString());
        Assert.Equal("NoExternalNoOrderDomainOutput", run.RootElement.GetProperty("SafetyStatus").GetString());
        Assert.True(run.RootElement.GetProperty("NoApiCall").GetBoolean());
        Assert.True(run.RootElement.GetProperty("NoBrokerRuntime").GetBoolean());
        Assert.True(run.RootElement.GetProperty("NoOrderDomainOutput").GetBoolean());
        Assert.True(run.RootElement.GetProperty("NoExecutableSchedule").GetBoolean());
        Assert.True(run.RootElement.GetProperty("NoRealFill").GetBoolean());

        Assert.True(lines.RootElement.GetProperty("resultLinesCreated").GetBoolean());
        Assert.Equal("FixtureOnlyPaperTcaSimulationLine", lines.RootElement.GetProperty("resultLineEntityType").GetString());
        Assert.False(lines.RootElement.GetProperty("resultLinesAreFills").GetBoolean());
        Assert.False(lines.RootElement.GetProperty("resultLinesAreExecutionReports").GetBoolean());
        Assert.False(lines.RootElement.GetProperty("resultLinesAreOrders").GetBoolean());
        Assert.False(lines.RootElement.GetProperty("schedulePhasesAreChildSlices").GetBoolean());
        foreach (var line in lines.RootElement.GetProperty("lines").EnumerateArray())
        {
            Assert.True(line.GetProperty("FixtureOnly").GetBoolean());
            Assert.True(line.GetProperty("PaperOnly").GetBoolean());
            Assert.True(line.GetProperty("NonExecutable").GetBoolean());
            Assert.False(line.GetProperty("IsFill").GetBoolean());
            Assert.False(line.GetProperty("IsExecutionReport").GetBoolean());
            Assert.False(line.GetProperty("IsOrder").GetBoolean());
            Assert.False(line.GetProperty("IsChildSlice").GetBoolean());
            Assert.False(line.GetProperty("IsSubmitted").GetBoolean());
            Assert.False(line.GetProperty("HasBrokerRoute").GetBoolean());
        }
    }

    [Fact]
    public void Accepted_shapes_are_simulated_and_ineligible_shapes_remain_excluded()
    {
        var simulated = ReadJson("phase-exec-sim-r018-accepted-shapes-simulated.json");
        var excluded = ReadJson("phase-exec-sim-r018-excluded-shapes-preserved.json");
        var resultLines = ReadJson("phase-exec-sim-r018-schedule-shape-simulation-result-lines.json");

        Assert.True(simulated.RootElement.GetProperty("acceptedShapesSimulatedArtifactCreated").GetBoolean());
        AssertContains(simulated, "acceptedShapesSimulated", "OpeningBuild");
        AssertContains(simulated, "acceptedShapesSimulated", "IntradayRebalance");
        AssertContains(simulated, "acceptedShapesSimulated", "ClosingFlatten");
        AssertContains(simulated, "acceptedShapesSimulated", "USDJPYInverted");
        AssertContains(simulated, "acceptedShapesSimulated", "BenchmarkOnly");
        Assert.True(simulated.RootElement.GetProperty("allSimulatedShapesFixtureOnly").GetBoolean());
        Assert.True(simulated.RootElement.GetProperty("allSimulatedShapesNonExecutable").GetBoolean());
        Assert.True(simulated.RootElement.GetProperty("allSimulatedShapesHaveNoChildOrders").GetBoolean());
        Assert.True(simulated.RootElement.GetProperty("allSimulatedShapesHaveNoExecutableSlices").GetBoolean());

        Assert.True(excluded.RootElement.GetProperty("excludedShapesPreservedArtifactCreated").GetBoolean());
        Assert.False(excluded.RootElement.GetProperty("directCrossShapeProducedAcceptedResultLine").GetBoolean());
        Assert.False(excluded.RootElement.GetProperty("nonmajorMissingConventionShapeProducedAcceptedResultLine").GetBoolean());
        Assert.False(excluded.RootElement.GetProperty("wakettBlockedShapeProducedAcceptedResultLine").GetBoolean());
        Assert.False(excluded.RootElement.GetProperty("closingFlattenUnsafeFeedShapeProducedAcceptedResultLine").GetBoolean());
        Assert.True(excluded.RootElement.GetProperty("ineligibleShapesDoNotProduceAcceptedSimulationResultLines").GetBoolean());

        Assert.Contains(resultLines.RootElement.GetProperty("lines").EnumerateArray(), x => x.GetProperty("BarRole").GetString() == "OpeningBuild");
        Assert.Contains(resultLines.RootElement.GetProperty("lines").EnumerateArray(), x => x.GetProperty("BarRole").GetString() == "ClosingFlatten");
        Assert.Contains(resultLines.RootElement.GetProperty("lines").EnumerateArray(), x =>
            x.GetProperty("ExecutionTradableSymbol").GetString() == "USDJPY" &&
            x.GetProperty("PortfolioNormalizedSymbol").GetString() == "JPYUSD" &&
            x.GetProperty("RequiresInversion").GetBoolean());
        Assert.Contains(resultLines.RootElement.GetProperty("lines").EnumerateArray(), x =>
            x.GetProperty("PolicyFamily").GetString() == "TWAPBenchmarkOnly" &&
            x.GetProperty("SimulationOutcomeStatus").GetString() == "BenchmarkOnlyNonExecutable");
    }

    [Fact]
    public void Tca_reports_rankings_and_no_overnight_penalty_are_produced()
    {
        var perShape = ReadJson("phase-exec-sim-r018-per-shape-tca-reports.json");
        var opening = ReadJson("phase-exec-sim-r018-opening-build-schedule-shape-tca.json");
        var intraday = ReadJson("phase-exec-sim-r018-intraday-rebalance-schedule-shape-tca.json");
        var closing = ReadJson("phase-exec-sim-r018-closing-flatten-schedule-shape-tca.json");
        var aggregate = ReadJson("phase-exec-sim-r018-session-aggregate-schedule-shape-tca.json");
        var eurusd = ReadJson("phase-exec-sim-r018-per-instrument-schedule-shape-eurusd-report.json");
        var usdjpy = ReadJson("phase-exec-sim-r018-per-instrument-schedule-shape-usdjpy-report.json");
        var audusd = ReadJson("phase-exec-sim-r018-per-instrument-schedule-shape-audusd-report.json");
        var comparison = ReadJson("phase-exec-sim-r018-policy-shape-comparison-vs-r013-r015.json");
        var median = ReadJson("phase-exec-sim-r018-ranking-by-shape-and-bar-role-median-slippage.json");
        var p95 = ReadJson("phase-exec-sim-r018-ranking-by-shape-and-bar-role-p95-slippage.json");
        var fillRatio = ReadJson("phase-exec-sim-r018-ranking-by-shape-and-bar-role-fill-ratio.json");
        var residual = ReadJson("phase-exec-sim-r018-ranking-by-shape-and-bar-role-residual.json");
        var spread = ReadJson("phase-exec-sim-r018-ranking-by-shape-and-bar-role-spread-paid.json");
        var noOvernightPenalty = ReadJson("phase-exec-sim-r018-no-overnight-residual-penalty-report.json");

        Assert.True(perShape.RootElement.GetProperty("perShapeTcaReportsCreated").GetBoolean());
        Assert.Contains(perShape.RootElement.GetProperty("reports").EnumerateArray(), x => x.GetProperty("RecommendationForFutureReview").GetString() == "KeepForSimulation");
        Assert.True(opening.RootElement.GetProperty("reportCreated").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("preSessionExecutionAllowed").GetBoolean());
        Assert.True(intraday.RootElement.GetProperty("normalCloseSeekingBehaviorPreserved").GetBoolean());
        Assert.True(closing.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.Equal("NoOvernightCritical", closing.RootElement.GetProperty("ResidualPenaltyBucket").GetString());
        Assert.True(aggregate.RootElement.GetProperty("reportCreated").GetBoolean());
        Assert.True(aggregate.RootElement.GetProperty("noOvernightResidualPenaltyIncluded").GetBoolean());
        Assert.True(eurusd.RootElement.GetProperty("instrumentReportCreated").GetBoolean());
        Assert.Equal("JPYUSD", usdjpy.RootElement.GetProperty("PortfolioNormalizedSymbol").GetString());
        Assert.True(usdjpy.RootElement.GetProperty("RequiresInversion").GetBoolean());
        Assert.Equal("not failed", audusd.RootElement.GetProperty("audusdStatus").GetString());
        Assert.True(comparison.RootElement.GetProperty("comparisonCreated").GetBoolean());
        Assert.False(comparison.RootElement.GetProperty("r013R015NumericMetricsInvented").GetBoolean());
        Assert.True(median.RootElement.GetProperty("rankingCreated").GetBoolean());
        Assert.True(p95.RootElement.GetProperty("rankingCreated").GetBoolean());
        Assert.True(fillRatio.RootElement.GetProperty("rankingCreated").GetBoolean());
        Assert.True(fillRatio.RootElement.GetProperty("fillRatioDoesNotCreateFillEntity").GetBoolean());
        Assert.True(residual.RootElement.GetProperty("closingFlattenNoOvernightPenaltyIncluded").GetBoolean());
        Assert.True(spread.RootElement.GetProperty("rankingCreated").GetBoolean());
        Assert.True(noOvernightPenalty.RootElement.GetProperty("NoOvernightResidualPenaltyIncluded").GetBoolean());
        Assert.True(noOvernightPenalty.RootElement.GetProperty("ClosingFlattenResidualCostlierThanIntradayResidual").GetBoolean());
    }

    [Fact]
    public void Preservation_artifacts_keep_previous_evening_no_overnight_benchmark_direct_cross_wakett_cost_nonmajor_usd_pair_and_usdjpy_guards()
    {
        var previousEvening = ReadJson("phase-exec-sim-r018-opening-build-previous-evening-preservation.json");
        var closing = ReadJson("phase-exec-sim-r018-closing-flatten-no-overnight-preservation.json");
        var benchmark = ReadJson("phase-exec-sim-r018-benchmark-only-preservation.json");
        var directCross = ReadJson("phase-exec-sim-r018-direct-cross-exclusion-preservation.json");
        var wakett = ReadJson("phase-exec-sim-r018-wakett-blocked-shape-preservation.json");
        var cost = ReadJson("phase-exec-sim-r018-cost-guidance-preservation.json");
        var nonmajor = ReadJson("phase-exec-sim-r018-nonmajor-calibration-preservation.json");
        var normalization = ReadJson("phase-exec-sim-r018-usd-pair-normalization-preservation.json");
        var usdjpy = ReadJson("phase-exec-sim-r018-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r018-lmax-readonly-baseline-reference.json");

        Assert.True(previousEvening.RootElement.GetProperty("openingBuildPreviousEveningPreserved").GetBoolean());
        Assert.False(previousEvening.RootElement.GetProperty("PreSessionOrderAllowed").GetBoolean());
        Assert.False(previousEvening.RootElement.GetProperty("OvernightExposureBeforeSessionStartAllowed").GetBoolean());
        Assert.True(closing.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("BlindMarketFallbackAllowed").GetBoolean());
        Assert.True(benchmark.RootElement.GetProperty("BenchmarkOnly").GetBoolean());
        Assert.False(benchmark.RootElement.GetProperty("benchmarkOnlyCreatesFill").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("directCrossAcceptedResultLinesCreated").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("PureLimitUntilCloseDefaultAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("AlwaysMarketAtCloseAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("wakettPatternBlockWeakened").GetBoolean());
        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(nonmajor.RootElement.GetProperty("nonMajorEmScandiCnhRequireLiquidityCalibration").GetBoolean());
        Assert.Equal("USD-pair-only", normalization.RootElement.GetProperty("executionUniverse").GetString());
        Assert.False(normalization.RootElement.GetProperty("normalizationWeakened").GetBoolean());
        Assert.Equal("JPYUSD", usdjpy.RootElement.GetProperty("PortfolioNormalizedSymbol").GetString());
        Assert.Equal("USDJPY", usdjpy.RootElement.GetProperty("ExecutionTradableSymbol").GetString());
        Assert.True(usdjpy.RootElement.GetProperty("RequiresInversion").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.True(usdjpy.RootElement.GetProperty("caveatPreserved").GetBoolean());
        Assert.False(usdjpy.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        Assert.True(lmax.RootElement.GetProperty("referenceOnly").GetBoolean());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR018").GetBoolean());
        Assert.Contains("not failed", lmax.RootElement.GetProperty("audusdStatus").GetString());
    }

    [Fact]
    public void No_executable_schedule_child_slices_child_orders_api_runtime_or_order_domain_audits_are_clean()
    {
        var schedule = ReadJson("phase-exec-sim-r018-no-executable-schedule-audit.json");
        var slices = ReadJson("phase-exec-sim-r018-no-child-slices-audit.json");
        var childOrders = ReadJson("phase-exec-sim-r018-no-child-orders-audit.json");
        var fill = ReadJson("phase-exec-sim-r018-no-real-fill-audit.json");
        var report = ReadJson("phase-exec-sim-r018-no-execution-report-audit.json");
        var order = ReadJson("phase-exec-sim-r018-no-order-created-audit.json");
        var route = ReadJson("phase-exec-sim-r018-no-route-no-submission-audit.json");
        var api = ReadJson("phase-exec-sim-r018-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r018-no-broker-marketdata-runtime-audit.json");
        var noExternal = ReadJson("phase-exec-sim-r018-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r018-forbidden-actions-audit.json");

        Assert.False(schedule.RootElement.GetProperty("executableScheduleCreated").GetBoolean());
        Assert.False(slices.RootElement.GetProperty("childSlicesCreated").GetBoolean());
        Assert.False(slices.RootElement.GetProperty("schedulePhasesRepresentedAsChildSlices").GetBoolean());
        Assert.False(childOrders.RootElement.GetProperty("childOrdersCreated").GetBoolean());
        Assert.False(fill.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());
        Assert.False(fill.RootElement.GetProperty("simulationResultLinesRepresentedAsFills").GetBoolean());
        Assert.False(report.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(report.RootElement.GetProperty("simulationResultLinesRepresentedAsExecutionReports").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("simulationResultLinesRepresentedAsOrders").GetBoolean());
        Assert.False(route.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(route.RootElement.GetProperty("submissionsCreated").GetBoolean());
        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("brokerActivationDetected").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("socketOpened").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("apiWorkerLiveGatewayEnabled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("schedulerServiceTimerPollingBackgroundJobIntroduced").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("ordersFillsReportsRoutesSubmissionsCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("liveBrokerProductionTradingStateMutated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("paperLedgerStateCommitted").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r018-summary.md",
        "phase-exec-sim-r018-r017-handoff-acceptance-reference.json",
        "phase-exec-sim-r018-schedule-shape-simulation-contract.json",
        "phase-exec-sim-r018-schedule-shape-simulation-run-result.json",
        "phase-exec-sim-r018-schedule-shape-simulation-result-lines.json",
        "phase-exec-sim-r018-accepted-shapes-simulated.json",
        "phase-exec-sim-r018-excluded-shapes-preserved.json",
        "phase-exec-sim-r018-per-shape-tca-reports.json",
        "phase-exec-sim-r018-opening-build-schedule-shape-tca.json",
        "phase-exec-sim-r018-intraday-rebalance-schedule-shape-tca.json",
        "phase-exec-sim-r018-closing-flatten-schedule-shape-tca.json",
        "phase-exec-sim-r018-session-aggregate-schedule-shape-tca.json",
        "phase-exec-sim-r018-per-instrument-schedule-shape-eurusd-report.json",
        "phase-exec-sim-r018-per-instrument-schedule-shape-usdjpy-report.json",
        "phase-exec-sim-r018-per-instrument-schedule-shape-audusd-report.json",
        "phase-exec-sim-r018-policy-shape-comparison-vs-r013-r015.json",
        "phase-exec-sim-r018-ranking-by-shape-and-bar-role-median-slippage.json",
        "phase-exec-sim-r018-ranking-by-shape-and-bar-role-p95-slippage.json",
        "phase-exec-sim-r018-ranking-by-shape-and-bar-role-fill-ratio.json",
        "phase-exec-sim-r018-ranking-by-shape-and-bar-role-residual.json",
        "phase-exec-sim-r018-ranking-by-shape-and-bar-role-spread-paid.json",
        "phase-exec-sim-r018-no-overnight-residual-penalty-report.json",
        "phase-exec-sim-r018-opening-build-previous-evening-preservation.json",
        "phase-exec-sim-r018-closing-flatten-no-overnight-preservation.json",
        "phase-exec-sim-r018-benchmark-only-preservation.json",
        "phase-exec-sim-r018-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r018-wakett-blocked-shape-preservation.json",
        "phase-exec-sim-r018-cost-guidance-preservation.json",
        "phase-exec-sim-r018-nonmajor-calibration-preservation.json",
        "phase-exec-sim-r018-usd-pair-normalization-preservation.json",
        "phase-exec-sim-r018-no-executable-schedule-audit.json",
        "phase-exec-sim-r018-no-child-slices-audit.json",
        "phase-exec-sim-r018-no-child-orders-audit.json",
        "phase-exec-sim-r018-no-real-fill-audit.json",
        "phase-exec-sim-r018-no-execution-report-audit.json",
        "phase-exec-sim-r018-no-order-created-audit.json",
        "phase-exec-sim-r018-no-route-no-submission-audit.json",
        "phase-exec-sim-r018-no-polygon-api-call-audit.json",
        "phase-exec-sim-r018-no-lmax-call-audit.json",
        "phase-exec-sim-r018-no-external-api-call-audit.json",
        "phase-exec-sim-r018-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r018-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r018-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r018-no-external-audit.json",
        "phase-exec-sim-r018-forbidden-actions-audit.json",
        "phase-exec-sim-r018-next-phase-recommendation.json"
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
