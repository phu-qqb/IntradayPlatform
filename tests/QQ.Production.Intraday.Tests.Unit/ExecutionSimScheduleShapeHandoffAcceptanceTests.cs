using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimScheduleShapeHandoffAcceptanceTests
{
    [Fact]
    public void Required_r017_artifacts_exist_and_reference_r006()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R017 artifact {artifact}");
        }

        var reference = ReadJson("phase-exec-sim-r017-r006-handoff-reference.json");
        Assert.Equal("EXEC-ALGO-R006", reference.RootElement.GetProperty("sourceHandoffPhase").GetString());
        Assert.True(reference.RootElement.GetProperty("r006HandoffArtifactsReferenced").GetBoolean());
        Assert.True(reference.RootElement.GetProperty("authorizationAcceptanceOnly").GetBoolean());
        Assert.True(reference.RootElement.GetProperty("noSimulation").GetBoolean());
        Assert.True(reference.RootElement.GetProperty("noBacktest").GetBoolean());
        Assert.True(reference.RootElement.GetProperty("noTcaResultsProduced").GetBoolean());
    }

    [Fact]
    public void Acceptance_contract_preflight_and_result_are_authorization_only()
    {
        var contract = ReadJson("phase-exec-sim-r017-schedule-shape-simulation-acceptance-contract.json");
        var preflight = ReadJson("phase-exec-sim-r017-schedule-shape-simulation-preflight-contract.json");
        var result = ReadJson("phase-exec-sim-r017-acceptance-result.json");

        Assert.True(contract.RootElement.GetProperty("acceptanceContractCreated").GetBoolean());
        Assert.Equal("EXEC-ALGO-R006", contract.RootElement.GetProperty("SourceHandoffPhase").GetString());
        Assert.Equal("EXEC-SIM-R018", contract.RootElement.GetProperty("IntendedNextPhase").GetString());
        Assert.Equal("ScheduleShapeSimulationHandoffAcceptedNoExternal", contract.RootElement.GetProperty("AcceptanceStatus").GetString());
        Assert.True(contract.RootElement.GetProperty("IsAuthorizationOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoSimulationRun").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoBacktestRun").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoTcaResultsProduced").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoOrdersFillsReportsRoutes").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoExternalApiCalls").GetBoolean());
        AssertContains(contract, "requiredFields", "ScheduleShapeSimulationAcceptanceId");
        AssertContains(contract, "requiredFields", "NoTcaResultsProduced");

        Assert.True(preflight.RootElement.GetProperty("preflightContractCreated").GetBoolean());
        Assert.True(preflight.RootElement.GetProperty("allRequiredEligibleShapesPresent").GetBoolean());
        Assert.True(preflight.RootElement.GetProperty("allAcceptedShapesNonExecutable").GetBoolean());
        Assert.True(preflight.RootElement.GetProperty("blockedShapesRemainExcluded").GetBoolean());
        Assert.True(preflight.RootElement.GetProperty("noSimulationRun").GetBoolean());
        Assert.True(preflight.RootElement.GetProperty("noTcaResultsProduced").GetBoolean());
        Assert.True(result.RootElement.GetProperty("acceptanceResultCreated").GetBoolean());
        Assert.Equal(5, result.RootElement.GetProperty("acceptedShapeCount").GetInt32());
        Assert.True(result.RootElement.GetProperty("isAuthorizationOnly").GetBoolean());
        Assert.True(result.RootElement.GetProperty("noSimulationRun").GetBoolean());
        Assert.True(result.RootElement.GetProperty("noSimulationResultLinesCreated").GetBoolean());
    }

    [Fact]
    public void Accepted_held_rejected_and_shape_summaries_are_correct()
    {
        var accepted = ReadJson("phase-exec-sim-r017-accepted-simulation-handoff-shapes.json");
        var held = ReadJson("phase-exec-sim-r017-held-simulation-handoff-shapes.json");
        var rejected = ReadJson("phase-exec-sim-r017-rejected-simulation-handoff-shapes.json");
        var eligible = ReadJson("phase-exec-sim-r017-eligible-shape-summary.json");
        var ineligible = ReadJson("phase-exec-sim-r017-ineligible-shape-summary.json");

        Assert.True(accepted.RootElement.GetProperty("acceptedHandoffShapesCreated").GetBoolean());
        Assert.True(accepted.RootElement.GetProperty("allAcceptedShapesDesignOnly").GetBoolean());
        Assert.True(accepted.RootElement.GetProperty("allAcceptedShapesPaperOnly").GetBoolean());
        Assert.True(accepted.RootElement.GetProperty("allAcceptedShapesNonExecutable").GetBoolean());
        Assert.True(accepted.RootElement.GetProperty("allAcceptedShapesNotOrders").GetBoolean());
        Assert.True(accepted.RootElement.GetProperty("allAcceptedShapesNotSubmitted").GetBoolean());
        Assert.True(accepted.RootElement.GetProperty("allAcceptedShapesNoBrokerRoute").GetBoolean());
        Assert.True(accepted.RootElement.GetProperty("allAcceptedShapesHaveNoChildOrders").GetBoolean());
        Assert.True(accepted.RootElement.GetProperty("allAcceptedShapesHaveNoExecutableSlices").GetBoolean());
        Assert.Contains(accepted.RootElement.GetProperty("acceptedShapes").EnumerateArray(), x => x.GetProperty("BarRole").GetString() == "OpeningBuild");
        Assert.Contains(accepted.RootElement.GetProperty("acceptedShapes").EnumerateArray(), x => x.GetProperty("BarRole").GetString() == "IntradayRebalance");
        Assert.Contains(accepted.RootElement.GetProperty("acceptedShapes").EnumerateArray(), x =>
            x.GetProperty("BarRole").GetString() == "ClosingFlatten" &&
            x.GetProperty("MustEndFlat").GetBoolean() &&
            !x.GetProperty("OvernightAllowed").GetBoolean());
        Assert.Contains(accepted.RootElement.GetProperty("acceptedShapes").EnumerateArray(), x =>
            x.TryGetProperty("PortfolioNormalizedSymbol", out var normalized) &&
            normalized.GetString() == "JPYUSD" &&
            x.GetProperty("ExecutionTradableSymbol").GetString() == "USDJPY" &&
            x.GetProperty("RequiresInversion").GetBoolean());
        Assert.Contains(accepted.RootElement.GetProperty("acceptedShapes").EnumerateArray(), x =>
            x.TryGetProperty("BenchmarkOnly", out var benchmarkOnly) && benchmarkOnly.GetBoolean() &&
            x.GetProperty("NonExecutable").GetBoolean());

        Assert.True(held.RootElement.GetProperty("heldHandoffShapesCreated").GetBoolean());
        Assert.False(held.RootElement.GetProperty("heldShapesCreateExecutableSchedules").GetBoolean());
        Assert.False(held.RootElement.GetProperty("heldShapesCreateOrderDomainObjects").GetBoolean());
        Assert.Contains(held.RootElement.GetProperty("heldShapes").EnumerateArray(), x => x.GetProperty("ReasonCategory").GetString() == "MissingInstrumentConvention");
        Assert.Contains(held.RootElement.GetProperty("heldShapes").EnumerateArray(), x => x.GetProperty("ReasonCategory").GetString() == "UnsafeFeed");
        Assert.True(rejected.RootElement.GetProperty("rejectedHandoffShapesCreated").GetBoolean());
        Assert.Contains(rejected.RootElement.GetProperty("rejectedShapes").EnumerateArray(), x => x.GetProperty("ReasonCategory").GetString() == "DirectCrossExecutionDisabled");
        Assert.Contains(rejected.RootElement.GetProperty("rejectedShapes").EnumerateArray(), x => x.GetProperty("ReasonCategory").GetString() == "WakettPatternBlocked");
        Assert.True(eligible.RootElement.GetProperty("OpeningBuildEligible").GetBoolean());
        Assert.True(eligible.RootElement.GetProperty("IntradayRebalanceEligible").GetBoolean());
        Assert.True(eligible.RootElement.GetProperty("ClosingFlattenEligible").GetBoolean());
        Assert.True(eligible.RootElement.GetProperty("ClosingFlattenRequiresNoOvernightPreserved").GetBoolean());
        Assert.True(ineligible.RootElement.GetProperty("DirectCrossExcluded").GetBoolean());
        Assert.True(ineligible.RootElement.GetProperty("WakettBlockedShapesExcluded").GetBoolean());
        Assert.False(ineligible.RootElement.GetProperty("anyExecutableShapeAccepted").GetBoolean());
    }

    [Fact]
    public void Future_simulation_inputs_outputs_are_defined_without_creating_results()
    {
        var inputs = ReadJson("phase-exec-sim-r017-expected-future-simulation-inputs.json");
        var outputs = ReadJson("phase-exec-sim-r017-expected-future-simulation-outputs.json");

        Assert.True(inputs.RootElement.GetProperty("expectedFutureSimulationInputsCreated").GetBoolean());
        AssertContains(inputs, "acceptedSimulationHandoffShapes", "OpeningBuild");
        AssertContains(inputs, "acceptedSimulationHandoffShapes", "USDJPYInverted");
        AssertContains(inputs, "acceptedRealOfflineQuoteFiles", "EURUSD");
        AssertContains(inputs, "acceptedRealOfflineQuoteFiles", "USDJPY");
        AssertContains(inputs, "acceptedRealOfflineQuoteFiles", "AUDUSD");
        AssertContains(inputs, "sessionAwareBarRoles", "ClosingFlatten");
        AssertContains(inputs, "scheduleShapePhases", "ControlledResidualCompletionWindow");
        Assert.True(inputs.RootElement.GetProperty("requiresExistingCloseBenchmarksAndFeedQualityReadiness").GetBoolean());
        Assert.True(inputs.RootElement.GetProperty("noNewQuoteImportInR017").GetBoolean());

        Assert.True(outputs.RootElement.GetProperty("expectedFutureSimulationOutputsCreated").GetBoolean());
        AssertContains(outputs, "expectedOutputs", "Schedule-shape simulation run result");
        AssertContains(outputs, "expectedOutputs", "Per-shape TCA report");
        AssertContains(outputs, "expectedOutputs", "No-overnight residual penalty report");
        AssertContains(outputs, "futureSimulationMustRemain", "FixtureOnly");
        AssertContains(outputs, "futureSimulationMustRemain", "NoRealFill");
        Assert.True(outputs.RootElement.GetProperty("noOutputsProducedInR017").GetBoolean());
        Assert.True(outputs.RootElement.GetProperty("noTcaResultsProducedInR017").GetBoolean());
        Assert.True(outputs.RootElement.GetProperty("noSimulationResultLinesCreatedInR017").GetBoolean());
    }

    [Fact]
    public void Preservation_artifacts_keep_no_overnight_previous_evening_usd_pair_direct_cross_wakett_cost_and_usdjpy_guards()
    {
        var noOvernight = ReadJson("phase-exec-sim-r017-no-overnight-preservation.json");
        var previousEvening = ReadJson("phase-exec-sim-r017-first-bar-previous-evening-preservation.json");
        var normalization = ReadJson("phase-exec-sim-r017-usd-pair-normalization-preservation.json");
        var directCross = ReadJson("phase-exec-sim-r017-direct-cross-exclusion-preservation.json");
        var wakett = ReadJson("phase-exec-sim-r017-wakett-pattern-block-preservation.json");
        var cost = ReadJson("phase-exec-sim-r017-cost-guidance-preservation.json");
        var nonmajor = ReadJson("phase-exec-sim-r017-nonmajor-calibration-preservation.json");
        var usdjpy = ReadJson("phase-exec-sim-r017-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r017-lmax-readonly-baseline-reference.json");

        Assert.True(noOvernight.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.False(noOvernight.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.False(noOvernight.RootElement.GetProperty("BlindMarketFallbackAllowed").GetBoolean());
        Assert.True(previousEvening.RootElement.GetProperty("KnownAtTimestampMayBePreviousEvening").GetBoolean());
        Assert.True(previousEvening.RootElement.GetProperty("EarliestExecutionTimestampMustRemainSessionStartOrExplicitAllowedStart").GetBoolean());
        Assert.False(previousEvening.RootElement.GetProperty("PreSessionOrderAllowed").GetBoolean());
        Assert.Equal("USD-pair-only", normalization.RootElement.GetProperty("executionUniverse").GetString());
        Assert.False(normalization.RootElement.GetProperty("normalizationWeakened").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.True(directCross.RootElement.GetProperty("directCrossBlockedShapeExcluded").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("PureLimitUntilCloseDefaultAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("AlwaysMarketAtCloseAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("wakettPatternBlockWeakened").GetBoolean());
        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(nonmajor.RootElement.GetProperty("nonMajorEmScandiCnhRequireLiquidityCalibration").GetBoolean());
        Assert.Equal("JPYUSD", usdjpy.RootElement.GetProperty("PortfolioNormalizedSymbol").GetString());
        Assert.Equal("USDJPY", usdjpy.RootElement.GetProperty("ExecutionTradableSymbol").GetString());
        Assert.True(usdjpy.RootElement.GetProperty("RequiresInversion").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.True(usdjpy.RootElement.GetProperty("caveatPreserved").GetBoolean());
        Assert.False(usdjpy.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        Assert.True(lmax.RootElement.GetProperty("referenceOnly").GetBoolean());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR017").GetBoolean());
        Assert.Contains("not failed", lmax.RootElement.GetProperty("audusdStatus").GetString());
    }

    [Fact]
    public void No_simulation_tca_result_schedule_slices_child_orders_api_runtime_or_order_audits_are_clean()
    {
        var noSimulation = ReadJson("phase-exec-sim-r017-no-simulation-backtest-audit.json");
        var tca = ReadJson("phase-exec-sim-r017-no-tca-results-audit.json");
        var lines = ReadJson("phase-exec-sim-r017-no-simulation-result-lines-audit.json");
        var schedule = ReadJson("phase-exec-sim-r017-no-executable-schedule-audit.json");
        var slices = ReadJson("phase-exec-sim-r017-no-child-slices-audit.json");
        var childOrders = ReadJson("phase-exec-sim-r017-no-child-orders-audit.json");
        var api = ReadJson("phase-exec-sim-r017-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r017-no-broker-marketdata-runtime-audit.json");
        var fill = ReadJson("phase-exec-sim-r017-no-real-fill-audit.json");
        var report = ReadJson("phase-exec-sim-r017-no-execution-report-audit.json");
        var order = ReadJson("phase-exec-sim-r017-no-order-created-audit.json");
        var route = ReadJson("phase-exec-sim-r017-no-route-no-submission-audit.json");
        var noExternal = ReadJson("phase-exec-sim-r017-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r017-forbidden-actions-audit.json");

        Assert.False(noSimulation.RootElement.GetProperty("newSimulationExecuted").GetBoolean());
        Assert.False(noSimulation.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(tca.RootElement.GetProperty("tcaResultsProduced").GetBoolean());
        Assert.False(lines.RootElement.GetProperty("simulationResultLinesCreated").GetBoolean());
        Assert.False(schedule.RootElement.GetProperty("executableScheduleCreated").GetBoolean());
        Assert.False(slices.RootElement.GetProperty("childSlicesCreated").GetBoolean());
        Assert.False(slices.RootElement.GetProperty("orderDomainSliceObjectsCreated").GetBoolean());
        Assert.False(childOrders.RootElement.GetProperty("childOrdersCreated").GetBoolean());
        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("brokerActivationDetected").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("socketOpened").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(fill.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());
        Assert.False(report.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(route.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("newSimulationExecuted").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("tcaResultsProduced").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("simulationResultLinesCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("executableScheduleCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("childOrdersCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("ordersFillsReportsRoutesSubmissionsCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("liveBrokerProductionTradingStateMutated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("paperLedgerStateCommitted").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r017-summary.md",
        "phase-exec-sim-r017-r006-handoff-reference.json",
        "phase-exec-sim-r017-schedule-shape-simulation-acceptance-contract.json",
        "phase-exec-sim-r017-schedule-shape-simulation-preflight-contract.json",
        "phase-exec-sim-r017-acceptance-result.json",
        "phase-exec-sim-r017-accepted-simulation-handoff-shapes.json",
        "phase-exec-sim-r017-held-simulation-handoff-shapes.json",
        "phase-exec-sim-r017-rejected-simulation-handoff-shapes.json",
        "phase-exec-sim-r017-eligible-shape-summary.json",
        "phase-exec-sim-r017-ineligible-shape-summary.json",
        "phase-exec-sim-r017-expected-future-simulation-inputs.json",
        "phase-exec-sim-r017-expected-future-simulation-outputs.json",
        "phase-exec-sim-r017-no-overnight-preservation.json",
        "phase-exec-sim-r017-first-bar-previous-evening-preservation.json",
        "phase-exec-sim-r017-usd-pair-normalization-preservation.json",
        "phase-exec-sim-r017-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r017-wakett-pattern-block-preservation.json",
        "phase-exec-sim-r017-cost-guidance-preservation.json",
        "phase-exec-sim-r017-nonmajor-calibration-preservation.json",
        "phase-exec-sim-r017-no-simulation-backtest-audit.json",
        "phase-exec-sim-r017-no-tca-results-audit.json",
        "phase-exec-sim-r017-no-simulation-result-lines-audit.json",
        "phase-exec-sim-r017-no-executable-schedule-audit.json",
        "phase-exec-sim-r017-no-child-slices-audit.json",
        "phase-exec-sim-r017-no-child-orders-audit.json",
        "phase-exec-sim-r017-no-polygon-api-call-audit.json",
        "phase-exec-sim-r017-no-lmax-call-audit.json",
        "phase-exec-sim-r017-no-external-api-call-audit.json",
        "phase-exec-sim-r017-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r017-no-real-fill-audit.json",
        "phase-exec-sim-r017-no-execution-report-audit.json",
        "phase-exec-sim-r017-no-order-created-audit.json",
        "phase-exec-sim-r017-no-route-no-submission-audit.json",
        "phase-exec-sim-r017-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r017-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r017-no-external-audit.json",
        "phase-exec-sim-r017-forbidden-actions-audit.json",
        "phase-exec-sim-r017-next-phase-recommendation.json"
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
