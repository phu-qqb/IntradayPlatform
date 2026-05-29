using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimExpandedTcaReviewDecisionTests
{
    [Fact]
    public void Required_r026_artifacts_exist_and_contract_is_review_decision_only()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R026 artifact {artifact}");
        }

        var contract = ReadJson("phase-exec-sim-r026-r025-tca-review-contract.json");
        var report = ReadJson("phase-exec-sim-r026-operator-review-report.json");

        Assert.True(contract.RootElement.GetProperty("r025TcaReviewContractCreated").GetBoolean());
        Assert.Equal("EXEC-SIM-R025", contract.RootElement.GetProperty("SourceSimulationPhase").GetString());
        Assert.True(contract.RootElement.GetProperty("ReviewDecisionOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoExternalApiCalls").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoNewSimulation").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoNewBacktest").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoNewTcaResultLines").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoDbImport").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoPersistedSanitizedRows").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("NoOrdersFillsReportsRoutes").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("UnsupportedNumericMetricsInvented").GetBoolean());
        Assert.Equal("MissingEvidence", contract.RootElement.GetProperty("UnsupportedMetricsPolicy").GetString());
        AssertContains(contract, "RecommendationStatuses", "MoreHistoricalWindowsRecommended");
        AssertContains(contract, "RecommendationStatuses", "OpeningClosingWindowsRecommended");
        AssertContains(contract, "RecommendationStatuses", "ParameterRefinementRecommended");

        Assert.True(File.Exists(Path.Combine(ArtifactsDir(), "phase-exec-sim-r026-operator-review-report.md")));
        Assert.True(report.RootElement.GetProperty("operatorReviewReportCreated").GetBoolean());
        Assert.True(report.RootElement.GetProperty("NoExecutableActionAuthorized").GetBoolean());
        Assert.True(report.RootElement.GetProperty("DirectCrossesRemainExcluded").GetBoolean());
        Assert.True(report.RootElement.GetProperty("FiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.Equal("not failed", report.RootElement.GetProperty("AudUsdStatus").GetString());
    }

    [Fact]
    public void Numeric_summary_reviews_r025_counts_rankings_and_usd_per_million_without_invented_metrics()
    {
        var numeric = ReadJson("phase-exec-sim-r026-numeric-tca-summary.json");
        var all = numeric.RootElement.GetProperty("BestWorstAllPolicies");
        var candidates = numeric.RootElement.GetProperty("BestWorstExecutableLikeCandidates");

        Assert.True(numeric.RootElement.GetProperty("numericTcaSummaryCreated").GetBoolean());
        Assert.Equal("EXEC-SIM-R025", numeric.RootElement.GetProperty("SourceSimulationPhase").GetString());
        Assert.Equal("EXEC-SIM-R025 artifacts", numeric.RootElement.GetProperty("NumericMetricsSource").GetString());
        Assert.False(numeric.RootElement.GetProperty("UnsupportedNumericMetricsInvented").GetBoolean());
        Assert.Equal(7, numeric.RootElement.GetProperty("R025RunCounts").GetProperty("SymbolCount").GetInt32());
        Assert.Equal(112, numeric.RootElement.GetProperty("R025RunCounts").GetProperty("QuoteWindowCount").GetInt32());
        Assert.Equal(1232, numeric.RootElement.GetProperty("R025RunCounts").GetProperty("TcaResultLineCount").GetInt32());
        Assert.Equal(1, numeric.RootElement.GetProperty("Scope").GetProperty("HistoricalWindowCount").GetInt32());
        Assert.Equal("TooSmallForProductionParameterConclusion", numeric.RootElement.GetProperty("Scope").GetProperty("SampleSizeConclusion").GetString());

        Assert.Equal("ImmediatePaperBenchmark", all.GetProperty("MedianSlippage").GetProperty("bestPolicy").GetString());
        Assert.Equal("WakettFiveMarketSlicesAroundClose", all.GetProperty("MedianSlippage").GetProperty("worstPolicy").GetString());
        Assert.Equal("ControlledResidualCross", candidates.GetProperty("P95Slippage").GetProperty("bestPolicy").GetString());
        Assert.Equal("PassiveUntilUrgency", candidates.GetProperty("P95Slippage").GetProperty("worstPolicy").GetString());
        Assert.Equal("ControlledResidualCross", candidates.GetProperty("FillRatio").GetProperty("bestPolicy").GetString());
        Assert.Equal("PassiveUntilUrgency", candidates.GetProperty("SpreadPaid").GetProperty("bestPolicy").GetString());

        Assert.Contains(numeric.RootElement.GetProperty("PerPolicyUsdPerMillionSummaries").EnumerateArray(), x =>
            x.GetProperty("PolicyFamily").GetString() == "CloseSeeking15mAdaptive" &&
            x.GetProperty("EvidenceStatus").GetString() == "PresentFromR025ResultLines");
        Assert.True(numeric.RootElement.GetProperty("FiveUsdPerMillionTargetComparison").GetProperty("BestCaseMajorOnly").GetBoolean());
        Assert.False(numeric.RootElement.GetProperty("FiveUsdPerMillionTargetComparison").GetProperty("Universalized").GetBoolean());
        Assert.False(numeric.RootElement.GetProperty("FiveUsdPerMillionTargetComparison").GetProperty("DemonstratedAsUniversalInR025").GetBoolean());
    }

    [Fact]
    public void Policy_symbol_wakett_close_seeking_and_inverted_reviews_answer_the_operator_questions()
    {
        var perSymbol = ReadJson("phase-exec-sim-r026-per-symbol-review.json");
        var wakettVs = ReadJson("phase-exec-sim-r026-wakett-vs-close-seeking-review.json");
        var adaptive = ReadJson("phase-exec-sim-r026-close-seeking-adaptive-review.json");
        var controlled = ReadJson("phase-exec-sim-r026-controlled-residual-cross-review.json");
        var passive = ReadJson("phase-exec-sim-r026-passive-until-urgency-review.json");
        var wakettLimit = ReadJson("phase-exec-sim-r026-wakett-limit-residual-risk-review.json");
        var wakettSlices = ReadJson("phase-exec-sim-r026-wakett-five-slices-spread-risk-review.json");
        var inverted = ReadJson("phase-exec-sim-r026-inverted-symbol-review.json");

        Assert.True(perSymbol.RootElement.GetProperty("perSymbolReviewCreated").GetBoolean());
        Assert.Equal(7, perSymbol.RootElement.GetProperty("comparisons").GetArrayLength());
        AssertContains(perSymbol, "EasiestSymbolsByP95", "USDCHF");
        AssertContains(perSymbol, "HardestSymbolsByP95", "NZDUSD");
        Assert.Equal("not failed", perSymbol.RootElement.GetProperty("AudUsdStatus").GetString());

        Assert.True(wakettVs.RootElement.GetProperty("CloseSeeking15mAdaptiveOutperformedWakettBaselines").GetBoolean());
        Assert.True(wakettVs.RootElement.GetProperty("DirectCrossesRemainExcluded").GetBoolean());
        Assert.True(wakettVs.RootElement.GetProperty("WakettPatternsRemainRejectedAsDefault").GetBoolean());
        Assert.Equal("KeepForFurtherOfflineTesting", adaptive.RootElement.GetProperty("RecommendationStatus").GetString());
        Assert.True(adaptive.RootElement.GetProperty("NeedsMoreHistoricalWindows").GetBoolean());
        Assert.True(adaptive.RootElement.GetProperty("NeedsOpeningClosingWindows").GetBoolean());
        Assert.True(controlled.RootElement.GetProperty("ConditionalUseOnly").GetBoolean());
        Assert.True(controlled.RootElement.GetProperty("UsefulWhereResidualOpportunityCostExceedsCrossingCost").GetBoolean());
        Assert.True(passive.RootElement.GetProperty("InsufficientWhereResidualMatters").GetBoolean());
        Assert.True(wakettLimit.RootElement.GetProperty("ResidualRiskHigh").GetBoolean());
        Assert.Equal("RejectWakettPattern", wakettLimit.RootElement.GetProperty("RecommendationStatus").GetString());
        Assert.True(wakettSlices.RootElement.GetProperty("SpreadPaidRiskHigh").GetBoolean());
        Assert.Equal("RejectWakettPattern", wakettSlices.RootElement.GetProperty("RecommendationStatus").GetString());

        Assert.True(inverted.RootElement.GetProperty("invertedSymbolReviewCreated").GetBoolean());
        Assert.True(inverted.RootElement.GetProperty("InvertedPairsBehavingSafelyInR025Review").GetBoolean());
        Assert.True(inverted.RootElement.GetProperty("UsdJpyCaveatPreserved").GetBoolean());
        Assert.False(inverted.RootElement.GetProperty("AudUsdMisclassifiedFailed").GetBoolean());
        Assert.Contains(inverted.RootElement.GetProperty("InvertedSymbols").EnumerateArray(), x =>
            x.GetProperty("ExecutionTradableSymbol").GetString() == "USDJPY" &&
            x.GetProperty("NormalizedPortfolioSymbol").GetString() == "JPYUSD" &&
            x.GetProperty("RequiresInversion").GetBoolean());
    }

    [Fact]
    public void Data_expansion_parameter_refinement_and_next_window_recommendations_are_sequenced_safely()
    {
        var sample = ReadJson("phase-exec-sim-r026-sample-size-and-coverage-review.json");
        var data = ReadJson("phase-exec-sim-r026-data-expansion-decision.json");
        var parameters = ReadJson("phase-exec-sim-r026-parameter-refinement-decision.json");
        var historical = ReadJson("phase-exec-sim-r026-next-historical-window-recommendation.json");
        var openingClosing = ReadJson("phase-exec-sim-r026-opening-closing-window-recommendation.json");
        var instruments = ReadJson("phase-exec-sim-r026-more-instrument-coverage-recommendation.json");
        var next = ReadJson("phase-exec-sim-r026-next-phase-recommendation.json");

        Assert.True(sample.RootElement.GetProperty("SampleTooSmallToConcludeProductionParameters").GetBoolean());
        Assert.False(sample.RootElement.GetProperty("OpeningBuildCovered").GetBoolean());
        Assert.False(sample.RootElement.GetProperty("ClosingFlattenCovered").GetBoolean());
        Assert.Equal("MoreHistoricalWindowsRecommended", data.RootElement.GetProperty("RecommendationStatus").GetString());
        Assert.True(data.RootElement.GetProperty("OpeningClosingWindowsRecommended").GetBoolean());
        Assert.True(data.RootElement.GetProperty("MoreHistoricalWindowsRecommended").GetBoolean());
        Assert.Equal("ParameterRefinementRecommended", parameters.RootElement.GetProperty("RecommendationStatus").GetString());
        Assert.Contains("After historical-window", parameters.RootElement.GetProperty("Sequence").GetString());
        Assert.True(parameters.RootElement.GetProperty("KeepCloseSeeking15mAdaptiveCandidate").GetBoolean());
        Assert.True(parameters.RootElement.GetProperty("KeepControlledResidualCrossConditional").GetBoolean());
        Assert.True(historical.RootElement.GetProperty("nextHistoricalWindowRecommendationCreated").GetBoolean());
        Assert.True(openingClosing.RootElement.GetProperty("openingClosingWindowRecommendationCreated").GetBoolean());
        Assert.True(instruments.RootElement.GetProperty("NonmajorEmScandiCnhCalibrationRequired").GetBoolean());
        Assert.Equal("EXEC-SIM-R027", next.RootElement.GetProperty("RecommendedNextPhase").GetString());
    }

    [Fact]
    public void Direct_cross_cost_nonmajor_usdjpy_lmax_and_no_external_preservations_are_clean()
    {
        var direct = ReadJson("phase-exec-sim-r026-direct-cross-exclusion-preservation.json");
        var cost = ReadJson("phase-exec-sim-r026-cost-guidance-preservation.json");
        var nonmajor = ReadJson("phase-exec-sim-r026-nonmajor-calibration-preservation.json");
        var fiveUsd = ReadJson("phase-exec-sim-r026-5usd-per-million-review.json");
        var usdjpy = ReadJson("phase-exec-sim-r026-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r026-lmax-readonly-baseline-reference.json");

        Assert.True(direct.RootElement.GetProperty("directCrossExclusionPreserved").GetBoolean());
        Assert.True(direct.RootElement.GetProperty("directCrossesSignalOnly").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.False(direct.RootElement.GetProperty("directCrossExclusionWeakened").GetBoolean());
        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(nonmajor.RootElement.GetProperty("nonMajorEmScandiCnhRequireLiquidityCalibration").GetBoolean());
        Assert.False(nonmajor.RootElement.GetProperty("calibrationRequirementWeakened").GetBoolean());
        Assert.True(fiveUsd.RootElement.GetProperty("BestCaseMajorOnly").GetBoolean());
        Assert.False(fiveUsd.RootElement.GetProperty("Universalized").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("usdjpyCaveatPreserved").GetBoolean());
        Assert.Equal("JPYUSD", usdjpy.RootElement.GetProperty("PortfolioNormalizedSymbol").GetString());
        Assert.Equal("USDJPY", usdjpy.RootElement.GetProperty("ExecutionTradableSymbol").GetString());
        Assert.True(usdjpy.RootElement.GetProperty("RequiresInversion").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.False(usdjpy.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        Assert.True(lmax.RootElement.GetProperty("referenceOnly").GetBoolean());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR026").GetBoolean());
    }

    [Fact]
    public void No_new_simulation_backtest_tca_import_schedule_child_order_api_runtime_or_order_audits_are_clean()
    {
        AssertAuditFalse("phase-exec-sim-r026-no-new-simulation-audit.json", "newSimulationExecuted");
        AssertAuditFalse("phase-exec-sim-r026-no-new-backtest-audit.json", "newBacktestExecuted");
        AssertAuditFalse("phase-exec-sim-r026-no-new-tca-lines-audit.json", "newTcaResultLinesProduced");
        AssertAuditFalse("phase-exec-sim-r026-no-db-import-audit.json", "quotesImportedIntoDb");
        AssertAuditFalse("phase-exec-sim-r026-no-persisted-sanitized-row-audit.json", "persistedSanitizedQuoteRowsCreated");
        AssertAuditFalse("phase-exec-sim-r026-no-executable-schedule-audit.json", "executableSchedulesCreated");
        AssertAuditFalse("phase-exec-sim-r026-no-child-slices-audit.json", "childSlicesCreated");
        AssertAuditFalse("phase-exec-sim-r026-no-child-orders-audit.json", "childOrdersCreated");
        AssertAuditFalse("phase-exec-sim-r026-no-real-fill-audit.json", "realFillsCreated");
        AssertAuditFalse("phase-exec-sim-r026-no-execution-report-audit.json", "executionReportEntitiesCreated");
        AssertAuditFalse("phase-exec-sim-r026-no-order-created-audit.json", "ordersCreated");
        AssertAuditFalse("phase-exec-sim-r026-no-route-no-submission-audit.json", "routesCreated");
        AssertAuditFalse("phase-exec-sim-r026-no-route-no-submission-audit.json", "submissionsCreated");
        AssertAuditFalse("phase-exec-sim-r026-no-polygon-api-call-audit.json", "polygonApiCalled");
        AssertAuditFalse("phase-exec-sim-r026-no-lmax-call-audit.json", "lmaxCalled");
        AssertAuditFalse("phase-exec-sim-r026-no-external-api-call-audit.json", "externalApiCalled");
        AssertAuditFalse("phase-exec-sim-r026-no-broker-marketdata-runtime-audit.json", "brokerActivationDetected");
        AssertAuditFalse("phase-exec-sim-r026-no-broker-marketdata-runtime-audit.json", "marketDataRequestSent");
        AssertAuditFalse("phase-exec-sim-r026-no-external-audit.json", "newSimulationExecuted");
        AssertAuditFalse("phase-exec-sim-r026-no-external-audit.json", "newTcaResultLinesProduced");
        AssertAuditFalse("phase-exec-sim-r026-no-external-audit.json", "ordersFillsReportsRoutesSubmissionsCreated");
        AssertAuditFalse("phase-exec-sim-r026-forbidden-actions-audit.json", "forbiddenActionsDetected");
    }

    private static void AssertAuditFalse(string fileName, string propertyName)
    {
        var document = ReadJson(fileName);
        Assert.False(document.RootElement.GetProperty(propertyName).GetBoolean());
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r026-summary.md",
        "phase-exec-sim-r026-r025-tca-review-contract.json",
        "phase-exec-sim-r026-operator-review-report.md",
        "phase-exec-sim-r026-operator-review-report.json",
        "phase-exec-sim-r026-numeric-tca-summary.json",
        "phase-exec-sim-r026-policy-ranking-review.json",
        "phase-exec-sim-r026-per-symbol-review.json",
        "phase-exec-sim-r026-wakett-vs-close-seeking-review.json",
        "phase-exec-sim-r026-close-seeking-adaptive-review.json",
        "phase-exec-sim-r026-controlled-residual-cross-review.json",
        "phase-exec-sim-r026-passive-until-urgency-review.json",
        "phase-exec-sim-r026-wakett-limit-residual-risk-review.json",
        "phase-exec-sim-r026-wakett-five-slices-spread-risk-review.json",
        "phase-exec-sim-r026-inverted-symbol-review.json",
        "phase-exec-sim-r026-5usd-per-million-review.json",
        "phase-exec-sim-r026-sample-size-and-coverage-review.json",
        "phase-exec-sim-r026-data-expansion-decision.json",
        "phase-exec-sim-r026-parameter-refinement-decision.json",
        "phase-exec-sim-r026-next-historical-window-recommendation.json",
        "phase-exec-sim-r026-opening-closing-window-recommendation.json",
        "phase-exec-sim-r026-more-instrument-coverage-recommendation.json",
        "phase-exec-sim-r026-direct-cross-exclusion-preservation.json",
        "phase-exec-sim-r026-cost-guidance-preservation.json",
        "phase-exec-sim-r026-nonmajor-calibration-preservation.json",
        "phase-exec-sim-r026-no-new-simulation-audit.json",
        "phase-exec-sim-r026-no-new-backtest-audit.json",
        "phase-exec-sim-r026-no-new-tca-lines-audit.json",
        "phase-exec-sim-r026-no-db-import-audit.json",
        "phase-exec-sim-r026-no-persisted-sanitized-row-audit.json",
        "phase-exec-sim-r026-no-executable-schedule-audit.json",
        "phase-exec-sim-r026-no-child-slices-audit.json",
        "phase-exec-sim-r026-no-child-orders-audit.json",
        "phase-exec-sim-r026-no-real-fill-audit.json",
        "phase-exec-sim-r026-no-execution-report-audit.json",
        "phase-exec-sim-r026-no-order-created-audit.json",
        "phase-exec-sim-r026-no-route-no-submission-audit.json",
        "phase-exec-sim-r026-no-polygon-api-call-audit.json",
        "phase-exec-sim-r026-no-lmax-call-audit.json",
        "phase-exec-sim-r026-no-external-api-call-audit.json",
        "phase-exec-sim-r026-no-broker-marketdata-runtime-audit.json",
        "phase-exec-sim-r026-usdjpy-caveat-preservation.json",
        "phase-exec-sim-r026-lmax-readonly-baseline-reference.json",
        "phase-exec-sim-r026-no-external-audit.json",
        "phase-exec-sim-r026-forbidden-actions-audit.json",
        "phase-exec-sim-r026-next-phase-recommendation.json"
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
