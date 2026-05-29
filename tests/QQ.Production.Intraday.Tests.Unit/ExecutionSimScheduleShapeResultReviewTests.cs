using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimScheduleShapeResultReviewTests
{
    [Fact]
    public void Required_r019_artifacts_exist_and_contract_is_review_only()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R019 artifact {artifact}");
        }

        var contract = ReadJson("phase-exec-sim-r019-r018-result-review-contract.json");
        Assert.True(contract.RootElement.GetProperty("r018ReviewContractCreated").GetBoolean());
        Assert.Equal("EXEC-SIM-R018", contract.RootElement.GetProperty("SourceSimulationPhase").GetString());
        Assert.True(contract.RootElement.GetProperty("reviewRecommendationOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("noNewSimulation").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("noNewBacktest").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("noNewTcaResultLines").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("unsupportedNumericMetricsInvented").GetBoolean());
        AssertContains(contract, "requiredFields", "ScheduleShapeRecommendationId");
        AssertContains(contract, "recommendationStatuses", "KeepForFurtherOfflineTesting");
        AssertContains(contract, "recommendationStatuses", "RejectWakettPattern");
    }

    [Fact]
    public void Operator_report_and_recommendations_are_present_and_non_executable()
    {
        var report = ReadJson("phase-exec-sim-r019-operator-review-report.json");
        var shapeRecommendations = ReadJson("phase-exec-sim-r019-shape-recommendations.json");
        Assert.True(File.Exists(Path.Combine(ArtifactsDir(), "phase-exec-sim-r019-operator-review-report.md")));
        Assert.True(report.RootElement.GetProperty("operatorReviewReportCreated").GetBoolean());
        Assert.True(report.RootElement.GetProperty("noExecutableActionAuthorized").GetBoolean());
        Assert.False(report.RootElement.GetProperty("newSimulationRun").GetBoolean());
        Assert.False(report.RootElement.GetProperty("newTcaResultLinesProduced").GetBoolean());
        AssertContains(report, "candidateShapes", "IntradayRebalance CloseSeeking15mAdaptive");
        AssertContains(report, "benchmarkOnlyShapes", "TWAPBenchmarkOnly");
        AssertContains(report, "rejectedShapes", "DirectCrossExecution");

        Assert.True(shapeRecommendations.RootElement.GetProperty("shapeRecommendationsCreated").GetBoolean());
        Assert.False(shapeRecommendations.RootElement.GetProperty("unsupportedNumericMetricsInvented").GetBoolean());
        Assert.Contains(shapeRecommendations.RootElement.GetProperty("recommendations").EnumerateArray(), x =>
            x.GetProperty("RecommendationStatus").GetString() == "KeepForFurtherOfflineTesting" &&
            x.GetProperty("BarRole").GetString() == "OpeningBuild");
        Assert.Contains(shapeRecommendations.RootElement.GetProperty("recommendations").EnumerateArray(), x =>
            x.GetProperty("RecommendationStatus").GetString() == "KeepForBenchmarkOnlyComparison" &&
            x.GetProperty("PolicyFamily").GetString() == "TWAPBenchmarkOnly");
        foreach (var recommendation in shapeRecommendations.RootElement.GetProperty("recommendations").EnumerateArray())
        {
            Assert.True(recommendation.GetProperty("IsDesignOnly").GetBoolean());
            Assert.False(recommendation.GetProperty("IsExecutable").GetBoolean());
            Assert.False(recommendation.GetProperty("IsOrder").GetBoolean());
            Assert.False(recommendation.GetProperty("IsSubmitted").GetBoolean());
            Assert.False(recommendation.GetProperty("HasBrokerRoute").GetBoolean());
        }
    }

    [Fact]
    public void Recommendations_by_role_instrument_and_ranking_review_preserve_evidence_status()
    {
        var byRole = ReadJson("phase-exec-sim-r019-recommendations-by-bar-role.json");
        var byInstrument = ReadJson("phase-exec-sim-r019-recommendations-by-instrument.json");
        var ranking = ReadJson("phase-exec-sim-r019-ranking-review.json");
        var penalty = ReadJson("phase-exec-sim-r019-no-overnight-penalty-review.json");

        Assert.True(byRole.RootElement.GetProperty("recommendationsByBarRoleCreated").GetBoolean());
        Assert.Equal("KeepForFurtherOfflineTesting", byRole.RootElement.GetProperty("OpeningBuild").GetProperty("RecommendationStatus").GetString());
        Assert.False(byRole.RootElement.GetProperty("OpeningBuild").GetProperty("preSessionExecutionAuthorized").GetBoolean());
        Assert.True(byRole.RootElement.GetProperty("ClosingFlatten").GetProperty("MustEndFlat").GetBoolean());
        Assert.False(byRole.RootElement.GetProperty("ClosingFlatten").GetProperty("OvernightAllowed").GetBoolean());
        Assert.False(byRole.RootElement.GetProperty("ClosingFlatten").GetProperty("blindMarketFallbackAuthorized").GetBoolean());

        Assert.True(byInstrument.RootElement.GetProperty("recommendationsByInstrumentCreated").GetBoolean());
        Assert.Equal("JPYUSD", byInstrument.RootElement.GetProperty("USDJPY").GetProperty("PortfolioNormalizedSymbol").GetString());
        Assert.True(byInstrument.RootElement.GetProperty("USDJPY").GetProperty("RequiresInversion").GetBoolean());
        Assert.Equal("not failed", byInstrument.RootElement.GetProperty("AUDUSD").GetProperty("audusdStatus").GetString());
        Assert.True(ranking.RootElement.GetProperty("rankingReviewCreated").GetBoolean());
        Assert.Contains("PresentFromR018", ranking.RootElement.GetProperty("medianSlippageReview").GetString());
        Assert.False(ranking.RootElement.GetProperty("unsupportedNumericMetricsInvented").GetBoolean());
        Assert.True(penalty.RootElement.GetProperty("noOvernightPenaltyReviewCreated").GetBoolean());
        Assert.True(penalty.RootElement.GetProperty("ClosingFlattenResidualCostlierThanIntradayResidual").GetBoolean());
    }

    [Fact]
    public void Opening_closing_benchmark_excluded_wakett_direct_cross_missing_convention_reviews_are_safe()
    {
        var opening = ReadJson("phase-exec-sim-r019-opening-build-review.json");
        var closing = ReadJson("phase-exec-sim-r019-closing-flatten-review.json");
        var benchmark = ReadJson("phase-exec-sim-r019-benchmark-only-review.json");
        var excluded = ReadJson("phase-exec-sim-r019-excluded-shapes-review.json");
        var wakett = ReadJson("phase-exec-sim-r019-wakett-rejection-preservation.json");
        var direct = ReadJson("phase-exec-sim-r019-direct-cross-rejection-preservation.json");
        var missingConvention = ReadJson("phase-exec-sim-r019-missing-convention-review.json");

        Assert.True(opening.RootElement.GetProperty("openingBuildReviewCreated").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("PreSessionExecutionAuthorized").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("OvernightExposureAuthorized").GetBoolean());
        Assert.True(closing.RootElement.GetProperty("closingFlattenReviewCreated").GetBoolean());
        Assert.True(closing.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("BlindMarketFallbackAuthorized").GetBoolean());
        Assert.True(benchmark.RootElement.GetProperty("benchmarkOnlyReviewCreated").GetBoolean());
        Assert.Equal("KeepForBenchmarkOnlyComparison", benchmark.RootElement.GetProperty("RecommendationStatus").GetString());
        Assert.False(benchmark.RootElement.GetProperty("benchmarkOnlyCreatesFill").GetBoolean());
        Assert.True(excluded.RootElement.GetProperty("excludedShapesReviewCreated").GetBoolean());
        Assert.True(excluded.RootElement.GetProperty("excludedShapesDoNotAuthorizeExecution").GetBoolean());
        Assert.True(excluded.RootElement.GetProperty("excludedShapesDoNotCreateResultLines").GetBoolean());
        Assert.True(wakett.RootElement.GetProperty("wakettRejectionPreserved").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("AlwaysMarketAtCloseAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("wakettBlockWeakened").GetBoolean());
        Assert.True(direct.RootElement.GetProperty("directCrossRejectionPreserved").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("directCrossExclusionWeakened").GetBoolean());
        Assert.True(missingConvention.RootElement.GetProperty("missingConventionReviewCreated").GetBoolean());
        Assert.Equal("RejectMissingConvention", missingConvention.RootElement.GetProperty("RecommendationStatus").GetString());
        Assert.False(missingConvention.RootElement.GetProperty("MissingConventionShapesExecutable").GetBoolean());
    }

    [Fact]
    public void Future_requirements_cost_nonmajor_usd_pair_usdjpy_and_lmax_preservations_are_safe()
    {
        var future = ReadJson("phase-exec-sim-r019-future-offline-testing-requirements.json");
        var cost = ReadJson("phase-exec-sim-r019-cost-guidance-preservation.json");
        var nonmajor = ReadJson("phase-exec-sim-r019-nonmajor-calibration-preservation.json");
        var normalization = ReadJson("phase-exec-sim-r019-usd-pair-normalization-preservation.json");
        var usdjpy = ReadJson("phase-exec-sim-r019-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r019-lmax-readonly-baseline-reference.json");

        Assert.True(future.RootElement.GetProperty("futureOfflineTestingRequirementsCreated").GetBoolean());
        AssertContains(future, "requiredHistoricalExpansion", "More historical windows for EURUSD");
        AssertContains(future, "potentialFutureUsdPairs", "GBPUSD");
        AssertContains(future, "sessionBoundaryWindows", "closing flatten bars");
        Assert.True(future.RootElement.GetProperty("nonmajorEmScandiCnhCalibrationSeparate").GetBoolean());
        Assert.True(future.RootElement.GetProperty("noBrokerExecutionLiveTrading").GetBoolean());
        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(nonmajor.RootElement.GetProperty("nonMajorEmScandiCnhRequireLiquidityCalibration").GetBoolean());
        Assert.Equal("USD-pair-only", normalization.RootElement.GetProperty("executionUniverse").GetString());
        Assert.False(normalization.RootElement.GetProperty("normalizationWeakened").GetBoolean());
        Assert.Equal("JPYUSD", usdjpy.RootElement.GetProperty("PortfolioNormalizedSymbol").GetString());
        Assert.True(usdjpy.RootElement.GetProperty("RequiresInversion").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.True(usdjpy.RootElement.GetProperty("caveatPreserved").GetBoolean());
        Assert.False(usdjpy.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        Assert.True(lmax.RootElement.GetProperty("referenceOnly").GetBoolean());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR019").GetBoolean());
        Assert.Contains("not failed", lmax.RootElement.GetProperty("audusdStatus").GetString());
    }

    [Fact]
    public void No_new_simulation_backtest_result_schedule_slice_child_order_api_runtime_or_order_audits_are_clean()
    {
        var noSimulation = ReadJson("phase-exec-sim-r019-no-new-simulation-audit.json");
        var noBacktest = ReadJson("phase-exec-sim-r019-no-new-backtest-audit.json");
        var noLines = ReadJson("phase-exec-sim-r019-no-new-result-lines-audit.json");
        var schedule = ReadJson("phase-exec-sim-r019-no-executable-schedule-audit.json");
        var slices = ReadJson("phase-exec-sim-r019-no-child-slices-audit.json");
        var childOrders = ReadJson("phase-exec-sim-r019-no-child-orders-audit.json");
        var fill = ReadJson("phase-exec-sim-r019-no-real-fill-audit.json");
        var report = ReadJson("phase-exec-sim-r019-no-execution-report-audit.json");
        var order = ReadJson("phase-exec-sim-r019-no-order-created-audit.json");
        var route = ReadJson("phase-exec-sim-r019-no-route-no-submission-audit.json");
        var api = ReadJson("phase-exec-sim-r019-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r019-no-broker-marketdata-runtime-audit.json");
        var noExternal = ReadJson("phase-exec-sim-r019-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r019-forbidden-actions-audit.json");

        Assert.False(noSimulation.RootElement.GetProperty("newSimulationExecuted").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(noLines.RootElement.GetProperty("newTcaResultLinesProduced").GetBoolean());
        Assert.False(schedule.RootElement.GetProperty("executableScheduleCreated").GetBoolean());
        Assert.False(slices.RootElement.GetProperty("childSlicesCreated").GetBoolean());
        Assert.False(childOrders.RootElement.GetProperty("childOrdersCreated").GetBoolean());
        Assert.False(fill.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());
        Assert.False(report.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(route.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("brokerActivationDetected").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("socketOpened").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("newSimulationExecuted").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("newTcaResultLinesProduced").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("ordersFillsReportsRoutesSubmissionsCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("paperLedgerStateCommitted").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r019-summary.md",
        "phase-exec-sim-r019-r018-result-review-contract.json",
        "phase-exec-sim-r019-operator-review-report.md",
        "phase-exec-sim-r019-operator-review-report.json",
        "phase-exec-sim-r019-shape-recommendations.json",
        "phase-exec-sim-r019-recommendations-by-bar-role.json",
        "phase-exec-sim-r019-recommendations-by-instrument.json",
        "phase-exec-sim-r019-ranking-review.json",
        "phase-exec-sim-r019-no-overnight-penalty-review.json",
        "phase-exec-sim-r019-opening-build-review.json",
        "phase-exec-sim-r019-closing-flatten-review.json",
        "phase-exec-sim-r019-benchmark-only-review.json",
        "phase-exec-sim-r019-excluded-shapes-review.json",
        "phase-exec-sim-r019-wakett-rejection-preservation.json",
        "phase-exec-sim-r019-direct-cross-rejection-preservation.json",
        "phase-exec-sim-r019-missing-convention-review.json",
        "phase-exec-sim-r019-future-offline-testing-requirements.json",
        "phase-exec-sim-r019-cost-guidance-preservation.json",
        "phase-exec-sim-r019-nonmajor-calibration-preservation.json",
        "phase-exec-sim-r019-usd-pair-normalization-preservation.json",
        "phase-exec-sim-r019-no-new-simulation-audit.json",
        "phase-exec-sim-r019-no-new-backtest-audit.json",
        "phase-exec-sim-r019-no-new-result-lines-audit.json",
        "phase-exec-sim-r019-no-executable-schedule-audit.json",
        "phase-exec-sim-r019-no-child-slices-audit.json",
        "phase-exec-sim-r019-no-child-orders-audit.json",
        "phase-exec-sim-r019-no-real-fill-audit.json",
        "phase-exec-sim-r019-no-execution-report-audit.json",
        "phase-exec-sim-r019-no-order-created-audit.json",
        "phase-exec-sim-r019-no-route-no-submission-audit.json",
        "phase-exec-sim-r019-no-polygon-api-call-audit.json",
        "phase-exec-sim-r019-no-lmax-call-audit.json",
        "phase-exec-sim-r019-no-external-api-call-audit.json",
        "phase-exec-sim-r019-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r019-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r019-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r019-no-external-audit.json",
        "phase-exec-sim-r019-forbidden-actions-audit.json",
        "phase-exec-sim-r019-next-phase-recommendation.json"
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
