using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionAlgoScheduleShapeContractTests
{
    [Fact]
    public void Required_r005_artifacts_exist_and_reference_r004()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R005 artifact {artifact}");
        }

        var reference = ReadJson("phase-exec-algo-r005-r004-preview-reference.json");
        Assert.Equal("EXEC-ALGO-R004", reference.RootElement.GetProperty("sourcePreviewPhase").GetString());
        Assert.True(reference.RootElement.GetProperty("r004PreviewsReferenced").GetBoolean());
        Assert.True(reference.RootElement.GetProperty("scheduleShapeOnly").GetBoolean());
        Assert.True(reference.RootElement.GetProperty("noSimulation").GetBoolean());
        Assert.True(reference.RootElement.GetProperty("noBacktest").GetBoolean());
    }

    [Fact]
    public void Schedule_shape_and_phase_contracts_are_design_only_and_non_executable()
    {
        var shape = ReadJson("phase-exec-algo-r005-schedule-shape-contract.json");
        var phase = ReadJson("phase-exec-algo-r005-schedule-phase-contract.json");

        Assert.True(shape.RootElement.GetProperty("scheduleShapeContractCreated").GetBoolean());
        AssertContains(shape, "requiredFields", "ScheduleShapeId");
        AssertContains(shape, "requiredFields", "HasChildOrders");
        AssertContains(shape, "requiredFields", "HasExecutableSlices");
        Assert.True(shape.RootElement.GetProperty("allScheduleShapesDesignOnly").GetBoolean());
        Assert.True(shape.RootElement.GetProperty("allScheduleShapesPaperOnly").GetBoolean());
        Assert.True(shape.RootElement.GetProperty("allScheduleShapesNonExecutable").GetBoolean());
        Assert.True(shape.RootElement.GetProperty("allScheduleShapesNotAnOrder").GetBoolean());
        Assert.True(shape.RootElement.GetProperty("allScheduleShapesNoBrokerRoute").GetBoolean());
        Assert.False(shape.RootElement.GetProperty("createsExecutableSchedule").GetBoolean());
        Assert.False(shape.RootElement.GetProperty("createsOrderDomainObjects").GetBoolean());

        Assert.True(phase.RootElement.GetProperty("schedulePhaseContractCreated").GetBoolean());
        AssertContains(phase, "phaseNames", "PassiveOpportunisticWindow");
        AssertContains(phase, "phaseNames", "AdaptiveUrgencyWindow");
        AssertContains(phase, "phaseNames", "ControlledResidualCompletionWindow");
        Assert.True(phase.RootElement.GetProperty("schedulePhasesAreMetadataOnly").GetBoolean());
        Assert.True(phase.RootElement.GetProperty("schedulePhasesAreNotChildSlices").GetBoolean());
        Assert.True(phase.RootElement.GetProperty("schedulePhasesAreNotExecutable").GetBoolean());
        Assert.True(phase.RootElement.GetProperty("schedulePhasesHaveNoBrokerRoute").GetBoolean());
    }

    [Fact]
    public void Opening_intraday_and_closing_schedule_shapes_preserve_bar_role_requirements()
    {
        var opening = ReadJson("phase-exec-algo-r005-opening-build-schedule-shape.json");
        var intraday = ReadJson("phase-exec-algo-r005-intraday-rebalance-schedule-shape.json");
        var closing = ReadJson("phase-exec-algo-r005-closing-flatten-schedule-shape.json");

        Assert.Equal("OpeningBuild", opening.RootElement.GetProperty("BarRole").GetString());
        Assert.True(opening.RootElement.GetProperty("KnownAtTimestampMayBePreviousEvening").GetBoolean());
        Assert.True(opening.RootElement.GetProperty("EarliestExecutionTimestampMustRemainSessionStartOrExplicitAllowedStart").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("PreSessionOrderAllowed").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("OvernightExposureBeforeSessionStartAllowed").GetBoolean());
        Assert.Equal(3, opening.RootElement.GetProperty("Phases").GetArrayLength());

        Assert.Equal("IntradayRebalance", intraday.RootElement.GetProperty("BarRole").GetString());
        Assert.True(intraday.RootElement.GetProperty("NormalCloseSeekingBehaviorPreserved").GetBoolean());
        Assert.True(intraday.RootElement.GetProperty("ThreePhaseCloseSeekingStructure").GetBoolean());
        Assert.Equal(3, intraday.RootElement.GetProperty("Phases").GetArrayLength());
        Assert.Contains(intraday.RootElement.GetProperty("Phases").EnumerateArray(), x =>
            x.GetProperty("PhaseStartOffsetFromClose").GetString() == "TMinus13Minutes" &&
            x.GetProperty("PhaseEndOffsetFromClose").GetString() == "TMinus5Minutes");

        Assert.Equal("ClosingFlatten", closing.RootElement.GetProperty("BarRole").GetString());
        Assert.Equal("Flat", closing.RootElement.GetProperty("TargetPosition").GetString());
        Assert.True(closing.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.Equal("NoOvernightCritical", closing.RootElement.GetProperty("ResidualPenaltyBucket").GetString());
        Assert.Equal("StrictlyLowerThanIntradayRebalance", closing.RootElement.GetProperty("MaxResidualAtClose").GetString());
        Assert.False(closing.RootElement.GetProperty("BlindMarketFallbackAllowed").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("FiveMarketSlicesDefaultAllowed").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("AlwaysMarketAtCloseDefaultAllowed").GetBoolean());
        Assert.Equal(3, closing.RootElement.GetProperty("Phases").GetArrayLength());

        AssertShapeIsNonExecutable(opening);
        AssertShapeIsNonExecutable(intraday);
        AssertShapeIsNonExecutable(closing);
        AssertPhasesAreNotSlices(opening);
        AssertPhasesAreNotSlices(intraday);
        AssertPhasesAreNotSlices(closing);
    }

    [Fact]
    public void Unsafe_direct_cross_usdjpy_nonmajor_wakett_and_benchmark_shapes_are_safe()
    {
        var unsafeFeed = ReadJson("phase-exec-algo-r005-closing-flatten-unsafe-feed-shape.json");
        var directCross = ReadJson("phase-exec-algo-r005-direct-cross-blocked-shape.json");
        var usdjpy = ReadJson("phase-exec-algo-r005-usdjpy-inverted-schedule-shape.json");
        var nonmajor = ReadJson("phase-exec-algo-r005-nonmajor-missing-convention-shape.json");
        var wakett = ReadJson("phase-exec-algo-r005-wakett-blocked-schedule-shapes.json");
        var benchmark = ReadJson("phase-exec-algo-r005-benchmark-only-schedule-shapes.json");

        Assert.Equal("ScheduleShapeRequiresManualReview", unsafeFeed.RootElement.GetProperty("ScheduleShapeStatus").GetString());
        Assert.True(unsafeFeed.RootElement.GetProperty("ManualReviewRequired").GetBoolean());
        Assert.False(unsafeFeed.RootElement.GetProperty("BlindMarketFallbackAllowed").GetBoolean());
        Assert.False(unsafeFeed.RootElement.GetProperty("ExecutableScheduleCreated").GetBoolean());
        Assert.False(unsafeFeed.RootElement.GetProperty("ChildSlicesCreated").GetBoolean());

        Assert.True(directCross.RootElement.GetProperty("RequiresNettingFirst").GetBoolean());
        Assert.True(directCross.RootElement.GetProperty("DirectCrossExecutionDisabled").GetBoolean());
        Assert.Equal("ScheduleShapeBlockedDirectCross", directCross.RootElement.GetProperty("ScheduleShapeStatus").GetString());
        Assert.True(directCross.RootElement.GetProperty("DirectCrossSignalOnlyHandlingPreserved").GetBoolean());

        Assert.Equal("JPYUSD", usdjpy.RootElement.GetProperty("PortfolioNormalizedSymbol").GetString());
        Assert.Equal("USDJPY", usdjpy.RootElement.GetProperty("ExecutionTradableSymbol").GetString());
        Assert.True(usdjpy.RootElement.GetProperty("RequiresInversion").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("UsdJpyCaveatPreserved").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("SecurityID").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("SecurityIDSource").GetString());
        AssertShapeIsNonExecutable(usdjpy);

        Assert.Equal("ScheduleShapeMissingInstrumentConvention", nonmajor.RootElement.GetProperty("ScheduleShapeStatus").GetString());
        Assert.True(nonmajor.RootElement.GetProperty("ManualReviewRequired").GetBoolean());
        Assert.False(nonmajor.RootElement.GetProperty("ScheduleReadyForExecution").GetBoolean());

        Assert.True(wakett.RootElement.GetProperty("WakettPatternBlocked").GetBoolean());
        Assert.Equal("ScheduleShapeBlockedWakettPattern", wakett.RootElement.GetProperty("ScheduleShapeStatus").GetString());
        Assert.False(wakett.RootElement.GetProperty("ScheduleShapeReadyForExecution").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("ExecutableScheduleCreated").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("ChildSlicesCreated").GetBoolean());
        AssertContains(wakett, "blockedFamilies", "PureLimitUntilCloseDefault");
        AssertContains(wakett, "blockedFamilies", "MechanicalMarketSlicesAroundClose");
        AssertContains(wakett, "blockedFamilies", "AlwaysMarketAtClose");

        Assert.True(benchmark.RootElement.GetProperty("benchmarkOnlyScheduleShapesCreated").GetBoolean());
        Assert.True(benchmark.RootElement.GetProperty("allBenchmarkOnly").GetBoolean());
        Assert.True(benchmark.RootElement.GetProperty("allDesignOnly").GetBoolean());
        Assert.True(benchmark.RootElement.GetProperty("allNonExecutable").GetBoolean());
        Assert.Contains(benchmark.RootElement.GetProperty("shapes").EnumerateArray(), x =>
            x.GetProperty("PolicyFamily").GetString() == "TWAPBenchmarkOnly" &&
            x.GetProperty("IsExecutable").GetBoolean() == false);
        Assert.Contains(benchmark.RootElement.GetProperty("shapes").EnumerateArray(), x =>
            x.GetProperty("PolicyFamily").GetString() == "VWAPBenchmarkOnly" &&
            x.GetProperty("HasBrokerRoute").GetBoolean() == false);
    }

    [Fact]
    public void Statuses_blocked_families_and_preservation_artifacts_are_complete()
    {
        var statuses = ReadJson("phase-exec-algo-r005-schedule-shape-statuses.json");
        var blocked = ReadJson("phase-exec-algo-r005-blocked-schedule-shape-families.json");
        var overnight = ReadJson("phase-exec-algo-r005-no-overnight-flatten-schedule-preservation.json");
        var firstBar = ReadJson("phase-exec-algo-r005-first-bar-previous-evening-schedule-preservation.json");
        var cost = ReadJson("phase-exec-algo-r005-cost-guidance-preservation.json");
        var nonmajor = ReadJson("phase-exec-algo-r005-nonmajor-calibration-preservation.json");
        var normalization = ReadJson("phase-exec-algo-r005-usd-pair-normalization-preservation.json");
        var directCross = ReadJson("phase-exec-algo-r005-direct-cross-exclusion-preservation.json");
        var wakett = ReadJson("phase-exec-algo-r005-wakett-pattern-block-preservation.json");

        AssertContains(statuses, "statuses", "ScheduleShapeReadyDesignOnly");
        AssertContains(statuses, "statuses", "ScheduleShapeBlockedDirectCross");
        AssertContains(statuses, "statuses", "ScheduleShapeBlockedWakettPattern");
        AssertContains(blocked, "blockedFamilies", "AnyScheduleShapeMarkedExecutable");
        AssertContains(blocked, "blockedFamilies", "AnyScheduleShapeWithChildOrders");
        Assert.False(blocked.RootElement.GetProperty("wakettPatternBlockWeakened").GetBoolean());
        Assert.False(blocked.RootElement.GetProperty("directCrossExclusionWeakened").GetBoolean());
        Assert.True(overnight.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.False(overnight.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.Equal("NoOvernightCritical", overnight.RootElement.GetProperty("ResidualPenaltyBucket").GetString());
        Assert.False(overnight.RootElement.GetProperty("ExecutableScheduleCreated").GetBoolean());
        Assert.False(overnight.RootElement.GetProperty("ChildOrdersCreated").GetBoolean());
        Assert.True(firstBar.RootElement.GetProperty("KnownAtTimestampMayBePreviousEvening").GetBoolean());
        Assert.False(firstBar.RootElement.GetProperty("PreSessionOrderAllowed").GetBoolean());
        Assert.False(firstBar.RootElement.GetProperty("OvernightExposureBeforeSessionStartAllowed").GetBoolean());
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
    public void No_executable_schedule_slices_child_orders_backtest_api_runtime_or_order_audits_are_clean()
    {
        var nonExecutable = ReadJson("phase-exec-algo-r005-non-executable-schedule-shape-audit.json");
        var schedule = ReadJson("phase-exec-algo-r005-no-executable-schedule-audit.json");
        var slices = ReadJson("phase-exec-algo-r005-no-child-slices-audit.json");
        var childOrders = ReadJson("phase-exec-algo-r005-no-child-orders-audit.json");
        var noBacktest = ReadJson("phase-exec-algo-r005-no-new-backtest-audit.json");
        var api = ReadJson("phase-exec-algo-r005-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-algo-r005-no-broker-marketdata-runtime-audit.json");
        var fill = ReadJson("phase-exec-algo-r005-no-real-fill-audit.json");
        var report = ReadJson("phase-exec-algo-r005-no-execution-report-audit.json");
        var order = ReadJson("phase-exec-algo-r005-no-order-created-audit.json");
        var route = ReadJson("phase-exec-algo-r005-no-route-no-submission-audit.json");
        var noExternal = ReadJson("phase-exec-algo-r005-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-algo-r005-forbidden-actions-audit.json");
        var usdjpy = ReadJson("phase-exec-algo-r005-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-algo-r005-lmax-readonly-baseline-reference.json");

        Assert.True(nonExecutable.RootElement.GetProperty("allScheduleShapesNonExecutable").GetBoolean());
        Assert.True(nonExecutable.RootElement.GetProperty("schedulePhasesAreNotChildSlices").GetBoolean());
        Assert.False(nonExecutable.RootElement.GetProperty("executableScheduleCreated").GetBoolean());
        Assert.False(schedule.RootElement.GetProperty("executableScheduleCreated").GetBoolean());
        Assert.False(schedule.RootElement.GetProperty("scheduleThatCanBeSubmittedCreated").GetBoolean());
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
        Assert.False(noExternal.RootElement.GetProperty("executableScheduleCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("childSlicesCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("childOrdersCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("ordersFillsReportsRoutesSubmissionsCreated").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("caveatPreserved").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("requiresInversion").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.False(usdjpy.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        Assert.Contains("not failed", lmax.RootElement.GetProperty("audusdStatus").GetString());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR005").GetBoolean());
    }

    private static void AssertShapeIsNonExecutable(JsonDocument shape)
    {
        Assert.True(shape.RootElement.GetProperty("IsDesignOnly").GetBoolean());
        Assert.True(shape.RootElement.GetProperty("IsPaperOnly").GetBoolean());
        Assert.False(shape.RootElement.GetProperty("IsExecutable").GetBoolean());
        Assert.False(shape.RootElement.GetProperty("IsOrder").GetBoolean());
        Assert.False(shape.RootElement.GetProperty("IsSubmitted").GetBoolean());
        Assert.False(shape.RootElement.GetProperty("HasBrokerRoute").GetBoolean());
        Assert.False(shape.RootElement.GetProperty("HasChildOrders").GetBoolean());
        Assert.False(shape.RootElement.GetProperty("HasExecutableSlices").GetBoolean());
    }

    private static void AssertPhasesAreNotSlices(JsonDocument shape)
    {
        foreach (var phase in shape.RootElement.GetProperty("Phases").EnumerateArray())
        {
            Assert.False(phase.GetProperty("IsExecutable").GetBoolean());
            Assert.False(phase.GetProperty("IsOrderSlice").GetBoolean());
            Assert.False(phase.GetProperty("IsSubmitted").GetBoolean());
            Assert.False(phase.GetProperty("HasBrokerRoute").GetBoolean());
        }
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-algo-r005-summary.md",
        "phase-exec-algo-r005-r004-preview-reference.json",
        "phase-exec-algo-r005-schedule-shape-contract.json",
        "phase-exec-algo-r005-schedule-phase-contract.json",
        "phase-exec-algo-r005-opening-build-schedule-shape.json",
        "phase-exec-algo-r005-intraday-rebalance-schedule-shape.json",
        "phase-exec-algo-r005-closing-flatten-schedule-shape.json",
        "phase-exec-algo-r005-closing-flatten-unsafe-feed-shape.json",
        "phase-exec-algo-r005-direct-cross-blocked-shape.json",
        "phase-exec-algo-r005-usdjpy-inverted-schedule-shape.json",
        "phase-exec-algo-r005-nonmajor-missing-convention-shape.json",
        "phase-exec-algo-r005-wakett-blocked-schedule-shapes.json",
        "phase-exec-algo-r005-benchmark-only-schedule-shapes.json",
        "phase-exec-algo-r005-schedule-shape-statuses.json",
        "phase-exec-algo-r005-blocked-schedule-shape-families.json",
        "phase-exec-algo-r005-no-overnight-flatten-schedule-preservation.json",
        "phase-exec-algo-r005-first-bar-previous-evening-schedule-preservation.json",
        "phase-exec-algo-r005-cost-guidance-preservation.json",
        "phase-exec-algo-r005-nonmajor-calibration-preservation.json",
        "phase-exec-algo-r005-usd-pair-normalization-preservation.json",
        "phase-exec-algo-r005-direct-cross-exclusion-preservation.json",
        "phase-exec-algo-r005-wakett-pattern-block-preservation.json",
        "phase-exec-algo-r005-non-executable-schedule-shape-audit.json",
        "phase-exec-algo-r005-no-executable-schedule-audit.json",
        "phase-exec-algo-r005-no-child-slices-audit.json",
        "phase-exec-algo-r005-no-child-orders-audit.json",
        "phase-exec-algo-r005-no-new-backtest-audit.json",
        "phase-exec-algo-r005-no-polygon-api-call-audit.json",
        "phase-exec-algo-r005-no-lmax-call-audit.json",
        "phase-exec-algo-r005-no-external-api-call-audit.json",
        "phase-exec-algo-r005-no-broker-marketdata-runtime-audit.json",
        "phase-exec-algo-r005-no-real-fill-audit.json",
        "phase-exec-algo-r005-no-execution-report-audit.json",
        "phase-exec-algo-r005-no-order-created-audit.json",
        "phase-exec-algo-r005-no-route-no-submission-audit.json",
        "phase-exec-algo-r005-usdjpy-caveat-preservation.json",
        "phase-exec-algo-r005-lmax-readonly-baseline-reference.json",
        "phase-exec-algo-r005-no-external-audit.json",
        "phase-exec-algo-r005-forbidden-actions-audit.json",
        "phase-exec-algo-r005-next-phase-recommendation.json"
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
