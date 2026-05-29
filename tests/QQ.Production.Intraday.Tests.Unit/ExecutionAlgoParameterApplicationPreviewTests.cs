using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionAlgoParameterApplicationPreviewTests
{
    [Fact]
    public void Required_r004_artifacts_exist_and_reference_r003()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R004 artifact {artifact}");
        }

        var reference = ReadJson("phase-exec-algo-r004-r003-parameter-contract-reference.json");
        Assert.Equal("EXEC-ALGO-R003", reference.RootElement.GetProperty("sourceParameterPhase").GetString());
        Assert.True(reference.RootElement.GetProperty("r003ParameterContractReferenced").GetBoolean());
        Assert.Equal("1.0.0-design-only", reference.RootElement.GetProperty("parameterSetVersion").GetString());
        Assert.True(reference.RootElement.GetProperty("contractOnlyPreview").GetBoolean());
        Assert.True(reference.RootElement.GetProperty("noSimulation").GetBoolean());
        Assert.True(reference.RootElement.GetProperty("noBacktest").GetBoolean());
    }

    [Fact]
    public void Parameter_application_preview_contract_and_input_shape_are_preview_only()
    {
        var contract = ReadJson("phase-exec-algo-r004-parameter-application-preview-contract.json");
        var input = ReadJson("phase-exec-algo-r004-paper-execution-plan-line-input-shape.json");

        Assert.True(contract.RootElement.GetProperty("parameterApplicationPreviewContractCreated").GetBoolean());
        Assert.Equal("EXEC-ALGO-R003", contract.RootElement.GetProperty("sourceParameterPhase").GetString());
        AssertContains(contract, "previewStatuses", "ParameterPreviewReady");
        AssertContains(contract, "previewStatuses", "ParameterPreviewRequiresManualReview");
        AssertContains(contract, "previewStatuses", "ParameterPreviewBlockedDirectCross");
        AssertContains(contract, "requiredFields", "ParameterApplicationPreviewId");
        AssertContains(contract, "requiredFields", "PaperExecutionPlanLineId");
        AssertContains(contract, "requiredFields", "AppliedPolicyFamily");
        Assert.True(contract.RootElement.GetProperty("allPreviewsDesignOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("allPreviewsPaperOnly").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("allPreviewsNonExecutable").GetBoolean());
        Assert.True(contract.RootElement.GetProperty("allPreviewsNotAnOrder").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("createsExecutableSchedule").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("createsChildSlices").GetBoolean());
        Assert.False(contract.RootElement.GetProperty("createsOrders").GetBoolean());

        Assert.True(input.RootElement.GetProperty("inputShapeCreated").GetBoolean());
        Assert.True(input.RootElement.GetProperty("previewOnly").GetBoolean());
        Assert.False(input.RootElement.GetProperty("createsOrder").GetBoolean());
        Assert.Equal(3, input.RootElement.GetProperty("deterministicPaperLineFixtures").GetArrayLength());
    }

    [Fact]
    public void Opening_intraday_and_closing_valid_previews_apply_bar_role_parameters()
    {
        var opening = ReadJson("phase-exec-algo-r004-opening-build-application-preview.json");
        var intraday = ReadJson("phase-exec-algo-r004-intraday-rebalance-application-preview.json");
        var closing = ReadJson("phase-exec-algo-r004-closing-flatten-application-preview.json");

        Assert.Equal("OpeningBuild", opening.RootElement.GetProperty("BarRole").GetString());
        Assert.True(opening.RootElement.GetProperty("PreviousEveningPlanningAllowed").GetBoolean());
        Assert.True(opening.RootElement.GetProperty("KnownAtTimestampSeparateFromEarliestExecutionTimestamp").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("OvernightExposureBeforeSessionStartAllowed").GetBoolean());
        Assert.False(opening.RootElement.GetProperty("PreSessionOrderAllowed").GetBoolean());
        Assert.Equal("ParameterPreviewReady", opening.RootElement.GetProperty("PreviewStatus").GetString());

        Assert.Equal("IntradayRebalance", intraday.RootElement.GetProperty("BarRole").GetString());
        Assert.True(intraday.RootElement.GetProperty("NormalCloseSeekingBehaviorPreserved").GetBoolean());
        Assert.True(intraday.RootElement.GetProperty("PassiveUntilUrgencyFallbackAvailableWhenResidualRiskLow").GetBoolean());
        Assert.True(intraday.RootElement.GetProperty("ControlledResidualCrossOnlyWhenOpportunityCostExceedsCrossingCost").GetBoolean());
        Assert.Equal("ParameterPreviewReady", intraday.RootElement.GetProperty("PreviewStatus").GetString());

        Assert.Equal("ClosingFlatten", closing.RootElement.GetProperty("BarRole").GetString());
        Assert.Equal("Flat", closing.RootElement.GetProperty("TargetPosition").GetString());
        Assert.True(closing.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.Equal("NoOvernightCritical", closing.RootElement.GetProperty("ResidualPenaltyBucket").GetString());
        Assert.Equal("MustCompleteFlat", closing.RootElement.GetProperty("CompletionPriority").GetString());
        Assert.True(closing.RootElement.GetProperty("StrictResidualParameterApplied").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("BlindMarketScheduleCreated").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("FiveMarketSlicesDefaultAllowed").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("AlwaysMarketAtCloseDefaultAllowed").GetBoolean());
        Assert.False(closing.RootElement.GetProperty("PureLimitUntilCloseDefaultAllowed").GetBoolean());

        AssertPreviewIsNonExecutable(opening);
        AssertPreviewIsNonExecutable(intraday);
        AssertPreviewIsNonExecutable(closing);
    }

    [Fact]
    public void Unsafe_feed_direct_cross_usdjpy_nonmajor_and_wakett_previews_are_safe()
    {
        var unsafeFeed = ReadJson("phase-exec-algo-r004-closing-flatten-unsafe-feed-preview.json");
        var directCross = ReadJson("phase-exec-algo-r004-direct-cross-blocked-preview.json");
        var usdjpy = ReadJson("phase-exec-algo-r004-usdjpy-inverted-preview.json");
        var nonMajor = ReadJson("phase-exec-algo-r004-nonmajor-missing-convention-preview.json");
        var wakett = ReadJson("phase-exec-algo-r004-wakett-pattern-blocked-preview.json");

        Assert.Equal("ParameterPreviewRequiresManualReview", unsafeFeed.RootElement.GetProperty("PreviewStatus").GetString());
        Assert.Equal("Triggered", unsafeFeed.RootElement.GetProperty("ManualReviewTriggerStatus").GetString());
        Assert.False(unsafeFeed.RootElement.GetProperty("BlindExecutionCreated").GetBoolean());
        Assert.False(unsafeFeed.RootElement.GetProperty("ExecutableScheduleCreated").GetBoolean());
        Assert.False(unsafeFeed.RootElement.GetProperty("ChildSlicesCreated").GetBoolean());

        Assert.Equal("EURGBP", directCross.RootElement.GetProperty("RawQubesSymbol").GetString());
        Assert.True(directCross.RootElement.GetProperty("RequiresNettingFirst").GetBoolean());
        Assert.True(directCross.RootElement.GetProperty("DirectCrossExecutionDisabled").GetBoolean());
        Assert.Equal("ParameterPreviewBlockedDirectCross", directCross.RootElement.GetProperty("PreviewStatus").GetString());
        Assert.True(directCross.RootElement.GetProperty("DirectCrossSignalOnlyHandlingPreserved").GetBoolean());

        Assert.Equal("JPYUSD", usdjpy.RootElement.GetProperty("PortfolioNormalizedSymbol").GetString());
        Assert.Equal("USDJPY", usdjpy.RootElement.GetProperty("ExecutionTradableSymbol").GetString());
        Assert.True(usdjpy.RootElement.GetProperty("RequiresInversion").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("UsdJpyCaveatPreserved").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("SecurityID").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("SecurityIDSource").GetString());

        Assert.Equal("ParameterPreviewMissingInstrumentConvention", nonMajor.RootElement.GetProperty("PreviewStatus").GetString());
        Assert.Equal("MissingInstrumentConvention", nonMajor.RootElement.GetProperty("InstrumentConventionStatus").GetString());
        Assert.Equal("Triggered", nonMajor.RootElement.GetProperty("ManualReviewTriggerStatus").GetString());
        Assert.False(nonMajor.RootElement.GetProperty("ExecutablePreviewCreated").GetBoolean());

        Assert.True(wakett.RootElement.GetProperty("WakettPatternBlocked").GetBoolean());
        Assert.Equal("WakettPatternBlocked", wakett.RootElement.GetProperty("BlockedPolicyCheckStatus").GetString());
        Assert.False(wakett.RootElement.GetProperty("PureLimitUntilCloseDefaultAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("MechanicalMarketSlicesAroundCloseAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("AlwaysMarketAtCloseAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("ExecutableScheduleCreated").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("ChildSlicesCreated").GetBoolean());
    }

    [Fact]
    public void Applied_fields_fallback_manual_feed_benchmark_no_overnight_and_first_bar_applications_exist()
    {
        var fields = ReadJson("phase-exec-algo-r004-applied-parameter-preview-fields.json");
        var fallback = ReadJson("phase-exec-algo-r004-policy-fallback-application.json");
        var manual = ReadJson("phase-exec-algo-r004-manual-review-trigger-application.json");
        var feed = ReadJson("phase-exec-algo-r004-feed-quality-requirement-application.json");
        var benchmark = ReadJson("phase-exec-algo-r004-close-benchmark-requirement-application.json");
        var overnight = ReadJson("phase-exec-algo-r004-no-overnight-flatten-application.json");
        var firstBar = ReadJson("phase-exec-algo-r004-first-bar-previous-evening-application.json");

        AssertContains(fields, "fields", "PassiveWindowStartOffset");
        AssertContains(fields, "fields", "ResidualCrossThreshold");
        AssertContains(fields, "fields", "CompletionPriority");
        Assert.False(fields.RootElement.GetProperty("createsExecutableConfig").GetBoolean());
        AssertContains(fallback, "fallbackLadderLevels", "Preferred");
        AssertContains(fallback, "fallbackLadderLevels", "ManualReview");
        Assert.False(fallback.RootElement.GetProperty("createsOrder").GetBoolean());
        Assert.True(manual.RootElement.GetProperty("manualReviewIsNotAutomaticExecution").GetBoolean());
        Assert.False(manual.RootElement.GetProperty("manualReviewCreatesExecutableSchedule").GetBoolean());
        Assert.False(feed.RootElement.GetProperty("unsafeFeedCreatesBlindExecution").GetBoolean());
        Assert.False(benchmark.RootElement.GetProperty("missingCloseBenchmarkCreatesBlindExecution").GetBoolean());
        Assert.True(overnight.RootElement.GetProperty("MustEndFlat").GetBoolean());
        Assert.False(overnight.RootElement.GetProperty("OvernightAllowed").GetBoolean());
        Assert.Equal("NoOvernightCritical", overnight.RootElement.GetProperty("ResidualPenaltyBucket").GetString());
        Assert.False(overnight.RootElement.GetProperty("ExecutableScheduleCreated").GetBoolean());
        Assert.True(firstBar.RootElement.GetProperty("PreviousEveningPlanningAllowed").GetBoolean());
        Assert.True(firstBar.RootElement.GetProperty("KnownAtTimestampSeparateFromEarliestExecutionTimestamp").GetBoolean());
        Assert.False(firstBar.RootElement.GetProperty("OvernightExposureBeforeSessionStartAllowed").GetBoolean());
        Assert.False(firstBar.RootElement.GetProperty("PreSessionOrderAllowed").GetBoolean());
    }

    [Fact]
    public void Cost_nonmajor_usd_pair_direct_cross_wakett_audusd_and_usdjpy_are_preserved()
    {
        var cost = ReadJson("phase-exec-algo-r004-cost-guidance-preservation.json");
        var nonMajor = ReadJson("phase-exec-algo-r004-nonmajor-calibration-preservation.json");
        var normalization = ReadJson("phase-exec-algo-r004-usd-pair-normalization-preservation.json");
        var directCross = ReadJson("phase-exec-algo-r004-direct-cross-exclusion-preservation.json");
        var wakett = ReadJson("phase-exec-algo-r004-wakett-pattern-block-preservation.json");
        var usdjpy = ReadJson("phase-exec-algo-r004-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-algo-r004-lmax-readonly-baseline-reference.json");

        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseMajorOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(nonMajor.RootElement.GetProperty("nonMajorEmScandiCnhRequireLiquidityCalibration").GetBoolean());
        Assert.True(nonMajor.RootElement.GetProperty("missingConventionPreviewRequiresManualReview").GetBoolean());
        Assert.Equal("USD-pair-only", normalization.RootElement.GetProperty("executionUniverse").GetString());
        Assert.True(normalization.RootElement.GetProperty("mandatoryNettingBeforeExecution").GetBoolean());
        Assert.False(normalization.RootElement.GetProperty("normalizationWeakened").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.True(directCross.RootElement.GetProperty("directCrossSignalOnlyHandlingPreserved").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("guidanceWeakened").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("PureLimitUntilCloseDefaultAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("MechanicalMarketSlicesAroundCloseAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("AlwaysMarketAtCloseAllowed").GetBoolean());
        Assert.False(wakett.RootElement.GetProperty("wakettPatternBlockWeakened").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("caveatPreserved").GetBoolean());
        Assert.True(usdjpy.RootElement.GetProperty("requiresInversion").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.False(usdjpy.RootElement.GetProperty("audusdMisclassifiedFailed").GetBoolean());
        Assert.Contains("not failed", lmax.RootElement.GetProperty("audusdStatus").GetString());
        Assert.False(lmax.RootElement.GetProperty("lmaxCalledInR004").GetBoolean());
    }

    [Fact]
    public void No_executable_schedule_slices_backtest_api_runtime_or_order_audits_are_clean()
    {
        var nonExecutable = ReadJson("phase-exec-algo-r004-non-executable-preview-audit.json");
        var schedule = ReadJson("phase-exec-algo-r004-no-executable-schedule-audit.json");
        var slices = ReadJson("phase-exec-algo-r004-no-child-slices-audit.json");
        var noBacktest = ReadJson("phase-exec-algo-r004-no-new-backtest-audit.json");
        var api = ReadJson("phase-exec-algo-r004-no-external-api-call-audit.json");
        var runtime = ReadJson("phase-exec-algo-r004-no-broker-marketdata-runtime-audit.json");
        var fill = ReadJson("phase-exec-algo-r004-no-real-fill-audit.json");
        var report = ReadJson("phase-exec-algo-r004-no-execution-report-audit.json");
        var order = ReadJson("phase-exec-algo-r004-no-order-created-audit.json");
        var route = ReadJson("phase-exec-algo-r004-no-route-no-submission-audit.json");
        var noExternal = ReadJson("phase-exec-algo-r004-no-external-audit.json");
        var forbidden = ReadJson("phase-exec-algo-r004-forbidden-actions-audit.json");

        Assert.True(nonExecutable.RootElement.GetProperty("allPreviewsNonExecutable").GetBoolean());
        Assert.False(nonExecutable.RootElement.GetProperty("executableAlgoConfigurationCreated").GetBoolean());
        Assert.False(schedule.RootElement.GetProperty("executableScheduleCreated").GetBoolean());
        Assert.False(schedule.RootElement.GetProperty("scheduleThatCanBeSubmittedCreated").GetBoolean());
        Assert.False(slices.RootElement.GetProperty("childSlicesCreated").GetBoolean());
        Assert.False(slices.RootElement.GetProperty("childOrdersCreated").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newSimulationExecuted").GetBoolean());
        Assert.False(noBacktest.RootElement.GetProperty("newSimulationResultLinesCreated").GetBoolean());
        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("brokerActivationDetected").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("socketOpened").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(fill.RootElement.GetProperty("fillEntitiesCreated").GetBoolean());
        Assert.False(report.RootElement.GetProperty("executionReportEntitiesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("executableOrdersCreated").GetBoolean());
        Assert.False(route.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(route.RootElement.GetProperty("submissionsCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("executableScheduleCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("childSlicesCreated").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("ordersFillsReportsRoutesSubmissionsCreated").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    private static void AssertPreviewIsNonExecutable(JsonDocument preview)
    {
        Assert.True(preview.RootElement.GetProperty("IsDesignOnly").GetBoolean());
        Assert.True(preview.RootElement.GetProperty("IsPaperOnly").GetBoolean());
        Assert.False(preview.RootElement.GetProperty("IsExecutable").GetBoolean());
        Assert.False(preview.RootElement.GetProperty("IsOrder").GetBoolean());
        Assert.False(preview.RootElement.GetProperty("IsSubmitted").GetBoolean());
        Assert.False(preview.RootElement.GetProperty("HasBrokerRoute").GetBoolean());
        Assert.False(preview.RootElement.GetProperty("CreatesExecutableSchedule").GetBoolean());
        Assert.False(preview.RootElement.GetProperty("CreatesChildSlices").GetBoolean());
    }

    private static void AssertContains(JsonDocument document, string propertyName, string expected)
        => Assert.Contains(expected, document.RootElement.GetProperty(propertyName).EnumerateArray().Select(x => x.GetString()));

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-algo-r004-summary.md",
        "phase-exec-algo-r004-r003-parameter-contract-reference.json",
        "phase-exec-algo-r004-parameter-application-preview-contract.json",
        "phase-exec-algo-r004-paper-execution-plan-line-input-shape.json",
        "phase-exec-algo-r004-opening-build-application-preview.json",
        "phase-exec-algo-r004-intraday-rebalance-application-preview.json",
        "phase-exec-algo-r004-closing-flatten-application-preview.json",
        "phase-exec-algo-r004-closing-flatten-unsafe-feed-preview.json",
        "phase-exec-algo-r004-direct-cross-blocked-preview.json",
        "phase-exec-algo-r004-usdjpy-inverted-preview.json",
        "phase-exec-algo-r004-nonmajor-missing-convention-preview.json",
        "phase-exec-algo-r004-wakett-pattern-blocked-preview.json",
        "phase-exec-algo-r004-applied-parameter-preview-fields.json",
        "phase-exec-algo-r004-policy-fallback-application.json",
        "phase-exec-algo-r004-manual-review-trigger-application.json",
        "phase-exec-algo-r004-feed-quality-requirement-application.json",
        "phase-exec-algo-r004-close-benchmark-requirement-application.json",
        "phase-exec-algo-r004-no-overnight-flatten-application.json",
        "phase-exec-algo-r004-first-bar-previous-evening-application.json",
        "phase-exec-algo-r004-cost-guidance-preservation.json",
        "phase-exec-algo-r004-nonmajor-calibration-preservation.json",
        "phase-exec-algo-r004-usd-pair-normalization-preservation.json",
        "phase-exec-algo-r004-direct-cross-exclusion-preservation.json",
        "phase-exec-algo-r004-wakett-pattern-block-preservation.json",
        "phase-exec-algo-r004-non-executable-preview-audit.json",
        "phase-exec-algo-r004-no-executable-schedule-audit.json",
        "phase-exec-algo-r004-no-child-slices-audit.json",
        "phase-exec-algo-r004-no-new-backtest-audit.json",
        "phase-exec-algo-r004-no-polygon-api-call-audit.json",
        "phase-exec-algo-r004-no-lmax-call-audit.json",
        "phase-exec-algo-r004-no-external-api-call-audit.json",
        "phase-exec-algo-r004-no-broker-marketdata-runtime-audit.json",
        "phase-exec-algo-r004-no-real-fill-audit.json",
        "phase-exec-algo-r004-no-execution-report-audit.json",
        "phase-exec-algo-r004-no-order-created-audit.json",
        "phase-exec-algo-r004-no-route-no-submission-audit.json",
        "phase-exec-algo-r004-usdjpy-caveat-preservation.json",
        "phase-exec-algo-r004-lmax-readonly-baseline-reference.json",
        "phase-exec-algo-r004-no-external-audit.json",
        "phase-exec-algo-r004-forbidden-actions-audit.json",
        "phase-exec-algo-r004-next-phase-recommendation.json"
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
