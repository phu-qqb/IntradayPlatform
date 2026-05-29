using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionAlgoCloseSeekingFoundationTests
{
    [Fact]
    public void Existing_execution_oms_order_fill_report_slippage_tca_risk_and_paper_models_are_inventoried()
    {
        var foundation = ExecutionAlgoR001Foundation.CreateFixture();

        Assert.True(foundation.ExistingInventory.ExistingModelsInventoried);
        Assert.Contains("IVenueExecutionGateway", foundation.ExistingInventory.ExecutionModels);
        Assert.Contains("ParentOrder", foundation.ExistingInventory.OmsOrderModels);
        Assert.Contains("Fill", foundation.ExistingInventory.FillModels);
        Assert.Contains("ExecutionReport", foundation.ExistingInventory.ExecutionReportModels);
        Assert.Contains("TheoreticalVsRealReport", foundation.ExistingInventory.SlippageTcaModels);
        Assert.Contains("RiskDecision", foundation.ExistingInventory.RiskModels);
        Assert.Contains("PaperExecutionPlanShape", foundation.ExistingInventory.PaperExecutionModels);
    }

    [Fact]
    public void Close15m_benchmark_contract_exists()
    {
        var benchmark = ExecutionAlgoR001Foundation.CreateFixture().CloseBenchmark;

        Assert.Equal("bar-20260520-1445-1500", benchmark.BarId);
        Assert.Equal(TimeSpan.FromMinutes(15), benchmark.BarWindowEndUtc - benchmark.BarWindowStartUtc);
        Assert.Equal(benchmark.BarWindowEndUtc, benchmark.TargetCloseTimestampUtc);
        Assert.Equal(TimeSpan.FromMinutes(13), benchmark.TimeKnownBeforeClose);
        Assert.Equal(CloseConstructionMethod.InconclusiveSafe, benchmark.CloseConstructionMethod);
    }

    [Fact]
    public void Feed_continuity_contract_requires_bid_ask_mid_timestamp_and_instrument()
    {
        var requirement = ExecutionAlgoR001Foundation.CreateFixture().FeedContinuityRequirement;

        Assert.Contains("Bid", requirement.RequiredQuoteFields);
        Assert.Contains("Ask", requirement.RequiredQuoteFields);
        Assert.Contains("Mid", requirement.RequiredQuoteFields);
        Assert.Contains("Timestamp", requirement.RequiredQuoteFields);
        Assert.Contains("Instrument", requirement.RequiredQuoteFields);
        Assert.True(requirement.HeartbeatRequired);
        Assert.True(requirement.SessionContinuityRequired);
    }

    [Fact]
    public void No_quote_near_close_produces_safe_status()
    {
        var requirement = ExecutionAlgoR001Foundation.CreateFixture().FeedContinuityRequirement;

        var status = ExecutionAlgoR001Foundation.EvaluateFeedReadiness(true, true, 30, 0, TimeSpan.Zero, TimeSpan.Zero, 1m, requirement);

        Assert.Equal(FeedReadinessStatus.NoQuoteNearClose, status);
    }

    [Fact]
    public void Stale_quote_near_close_produces_safe_status()
    {
        var requirement = ExecutionAlgoR001Foundation.CreateFixture().FeedContinuityRequirement;

        var status = ExecutionAlgoR001Foundation.EvaluateFeedReadiness(true, true, 30, 4, TimeSpan.Zero, TimeSpan.FromSeconds(10), 1m, requirement);

        Assert.Equal(FeedReadinessStatus.StaleQuotes, status);
    }

    [Fact]
    public void Spread_too_wide_produces_safe_status()
    {
        var requirement = ExecutionAlgoR001Foundation.CreateFixture().FeedContinuityRequirement;

        var status = ExecutionAlgoR001Foundation.EvaluateFeedReadiness(true, true, 30, 4, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), 10m, requirement);

        Assert.Equal(FeedReadinessStatus.SpreadTooWide, status);
    }

    [Fact]
    public void Missing_bid_or_ask_produces_safe_status()
    {
        var requirement = ExecutionAlgoR001Foundation.CreateFixture().FeedContinuityRequirement;

        var status = ExecutionAlgoR001Foundation.EvaluateFeedReadiness(true, false, 30, 4, TimeSpan.Zero, TimeSpan.Zero, 1m, requirement);

        Assert.Equal(FeedReadinessStatus.MissingBidAsk, status);
    }

    [Fact]
    public void Execution_quality_contract_exists()
    {
        var report = ExecutionAlgoR001Foundation.CreateFixture().ExecutionQualityReport;

        Assert.True(report.HasCloseSlippageMeasurement);
        Assert.True(report.HasSpreadCostEstimate);
        Assert.True(report.HasOpportunityCostEstimate);
        Assert.True(report.HasNonFillRiskEstimate);
        Assert.True(report.HasResidualRiskEstimate);
        Assert.True(report.NonExecutable);
        Assert.True(report.NoBrokerRoute);
    }

    [Fact]
    public void Close_slippage_measurement_model_exists()
    {
        var measurement = ExecutionAlgoR001Foundation.CreateFixture().ExecutionQualityReport.Lines.Single().CloseSlippageMeasurement;

        Assert.Equal("bar-20260520-1445-1500", measurement.CloseBenchmarkId);
        Assert.Equal(0.8m, measurement.ExpectedSpreadCostBps);
        Assert.Equal(0.2m, measurement.ExpectedMarketImpactBps);
        Assert.Equal(1.0m, measurement.OpportunityCostBps);
        Assert.Equal(1.8m, measurement.ExpectedCloseSlippageBps);
        Assert.Equal(3.0m, measurement.MaxAllowedCloseSlippageBps);
        Assert.Equal("DesignOnlyNotRun", measurement.CompletionStatusPlaceholder);
    }

    [Fact]
    public void Spread_opportunity_nonfill_and_residual_cost_models_exist()
    {
        var estimate = ExecutionAlgoR001Foundation.CreateFixture().ExecutionQualityReport.Lines.Single().ExecutionCostEstimate;

        Assert.True(estimate.ExpectedSpreadCostBps > 0m);
        Assert.True(estimate.ExpectedOpportunityCostBps > 0m);
        Assert.True(estimate.ExpectedNonFillRiskBps > 0m);
        Assert.True(estimate.ExpectedResidualRiskBps > 0m);
        Assert.True(estimate.ExpectedMarketImpactBps > 0m);
    }

    [Fact]
    public void Algo_family_list_exists_and_is_design_only()
    {
        var families = ExecutionAlgoR001Foundation.CreateFixture().AlgoFamilies;

        foreach (var expected in Enum.GetValues<ExecutionAlgoFamily>())
        {
            Assert.Contains(families, x => x.Family == expected);
        }

        Assert.All(families, x =>
        {
            Assert.True(x.DesignOnly);
            Assert.True(x.PaperOnly);
            Assert.True(x.NonExecutable);
            Assert.True(x.NotAnOrder);
            Assert.True(x.NotSubmitted);
            Assert.True(x.NoBrokerRoute);
        });
    }

    [Fact]
    public void Pure_limit_until_close_is_not_accepted_as_default()
    {
        var blocked = ExecutionAlgoR001Foundation.CreateFixture().BlockedWakettPatterns;

        Assert.Contains(blocked, x => x.Pattern == WakettFailurePattern.PureLimitUntilClose && x.BlockedAsUnsafeDefault);
        Assert.DoesNotContain(ExecutionAlgoR001Foundation.CreateFixture().AlgoFamilies, x => x.Family.ToString() == "PureLimitUntilClose" && x.IsDefaultCandidate);
    }

    [Fact]
    public void Mechanical_five_market_slice_algo_is_blocked()
    {
        var blocked = ExecutionAlgoR001Foundation.CreateFixture().BlockedWakettPatterns;

        Assert.Contains(blocked, x => x.Pattern == WakettFailurePattern.MechanicalMarketSlicesAroundClose && x.BlockedAsUnsafeDefault);
        Assert.Contains(blocked, x => x.Pattern == WakettFailurePattern.BlindFiveMarketOrdersAtOneMinuteIntervals && x.BlockedAsUnsafeDefault);
        Assert.Contains(blocked, x => x.Pattern == WakettFailurePattern.BlindFiveMarketOrdersAroundClose && x.BlockedAsUnsafeDefault);
    }

    [Fact]
    public void Blind_market_crossing_is_blocked_unless_cost_model_justifies_residual_completion()
    {
        var blocked = ExecutionAlgoR001Foundation.CreateFixture().BlockedWakettPatterns;
        var controlled = ExecutionAlgoR001Foundation.CreateFixture().AlgoFamilies.Single(x => x.Family == ExecutionAlgoFamily.ControlledResidualCross);

        Assert.Contains(blocked, x => x.Pattern == WakettFailurePattern.BlindMarketExecution && x.BlockedAsUnsafeDefault);
        Assert.True(controlled.DesignOnly);
        Assert.True(controlled.NonExecutable);
    }

    [Fact]
    public void CloseSeeking15m_family_has_three_phases()
    {
        var phases = ExecutionAlgoR001Foundation.CreateFixture().CloseSeeking15mPhases;

        Assert.Equal(3, phases.Count);
        Assert.Contains(phases, x => x.PhaseName == CloseSeekingPhaseName.PassiveOpportunistic && x.StartsBeforeClose == TimeSpan.FromMinutes(13) && x.EndsBeforeClose == TimeSpan.FromMinutes(5));
        Assert.Contains(phases, x => x.PhaseName == CloseSeekingPhaseName.AdaptiveUrgency && x.StartsBeforeClose == TimeSpan.FromMinutes(5) && x.EndsBeforeClose == TimeSpan.FromMinutes(1));
        Assert.Contains(phases, x => x.PhaseName == CloseSeekingPhaseName.ControlledResidualCompletion && x.StartsBeforeClose == TimeSpan.FromMinutes(1) && x.EndsBeforeClose == TimeSpan.Zero);
        Assert.All(phases, x => Assert.True(x.NonExecutable));
    }

    [Fact]
    public void Algo_family_cannot_create_executable_orders()
    {
        var families = ExecutionAlgoR001Foundation.CreateFixture().AlgoFamilies;

        Assert.All(families, x =>
        {
            Assert.True(x.NonExecutable);
            Assert.True(x.NotAnOrder);
            Assert.True(x.NotSubmitted);
        });
    }

    [Fact]
    public void Algo_selection_decision_is_non_executable()
    {
        var decision = ExecutionAlgoR001Foundation.CreateFixture().SelectionDecision;

        Assert.True(decision.DesignOnly);
        Assert.True(decision.PaperOnly);
        Assert.True(decision.NonExecutable);
        Assert.True(decision.NotAnOrder);
        Assert.True(decision.NotSubmitted);
        Assert.True(decision.NoBrokerRoute);
        Assert.False(decision.CreatesOmsOrder);
        Assert.False(decision.CreatesParentOrder);
        Assert.False(decision.CreatesChildOrder);
        Assert.False(decision.CreatesBrokerOrder);
        Assert.False(decision.CreatesFill);
        Assert.False(decision.CreatesExecutionReport);
        Assert.False(decision.SubmitsOrder);
    }

    [Fact]
    public void Missing_close_benchmark_produces_safe_reason()
    {
        var decision = ExecutionAlgoR001Foundation.BlockDecision(SafeExecutionAlgoReasonCategory.MissingCloseBenchmark);

        Assert.Equal(SafeExecutionAlgoReasonCategory.MissingCloseBenchmark, decision.ReasonCategory);
        Assert.True(decision.NonExecutable);
    }

    [Fact]
    public void Missing_feed_continuity_produces_safe_reason()
    {
        var decision = ExecutionAlgoR001Foundation.BlockDecision(SafeExecutionAlgoReasonCategory.MissingFeedContinuity);

        Assert.Equal(SafeExecutionAlgoReasonCategory.MissingFeedContinuity, decision.ReasonCategory);
        Assert.True(decision.NonExecutable);
    }

    [Fact]
    public void Spread_too_wide_produces_safe_block_or_review_reason()
    {
        var decision = ExecutionAlgoR001Foundation.BlockDecision(SafeExecutionAlgoReasonCategory.SpreadTooWide);

        Assert.Equal(SafeExecutionAlgoReasonCategory.SpreadTooWide, decision.ReasonCategory);
        Assert.Equal(ExecutionAlgoFamily.ManualReview, decision.SelectedFamily);
    }

    [Fact]
    public void Slippage_limit_exceeded_produces_safe_block_or_review_reason()
    {
        var decision = ExecutionAlgoR001Foundation.BlockDecision(SafeExecutionAlgoReasonCategory.SlippageLimitExceeded);

        Assert.Equal(SafeExecutionAlgoReasonCategory.SlippageLimitExceeded, decision.ReasonCategory);
        Assert.Equal(ExecutionAlgoFamily.ManualReview, decision.SelectedFamily);
    }

    [Fact]
    public void Pms_lineage_is_preserved()
    {
        var lineage = ExecutionAlgoR001Foundation.CreateFixture().Lineage;

        Assert.True(lineage.PmsEmsOmsLineagePreserved);
        Assert.False(string.IsNullOrWhiteSpace(lineage.CycleRunId));
        Assert.False(string.IsNullOrWhiteSpace(lineage.QubesRunId));
        Assert.False(string.IsNullOrWhiteSpace(lineage.PaperExecutionPlanId));
        Assert.False(string.IsNullOrWhiteSpace(lineage.PaperCandidateId));
        Assert.False(string.IsNullOrWhiteSpace(lineage.RebalanceIntentId));
        Assert.False(string.IsNullOrWhiteSpace(lineage.RiskReviewId));
        Assert.False(string.IsNullOrWhiteSpace(lineage.LotSizingId));
    }

    [Fact]
    public void No_oms_parent_child_or_broker_order_is_created()
    {
        var foundation = ExecutionAlgoR001Foundation.CreateFixture();

        Assert.True(foundation.NoOrdersCreated);
        Assert.False(foundation.SelectionDecision.CreatesOmsOrder);
        Assert.False(foundation.SelectionDecision.CreatesParentOrder);
        Assert.False(foundation.SelectionDecision.CreatesChildOrder);
        Assert.False(foundation.SelectionDecision.CreatesBrokerOrder);
    }

    [Fact]
    public void No_fill_or_execution_report_is_introduced()
    {
        var foundation = ExecutionAlgoR001Foundation.CreateFixture();

        Assert.True(foundation.NoFillsCreated);
        Assert.True(foundation.NoExecutionReportsCreated);
        Assert.False(foundation.SelectionDecision.CreatesFill);
        Assert.False(foundation.SelectionDecision.CreatesExecutionReport);
    }

    [Fact]
    public void No_order_submission_path_is_introduced()
    {
        var foundation = ExecutionAlgoR001Foundation.CreateFixture();

        Assert.True(foundation.NoSubmission);
        Assert.False(foundation.SelectionDecision.SubmitsOrder);
        Assert.True(foundation.SelectionDecision.NotSubmitted);
        Assert.True(foundation.SelectionDecision.NoBrokerRoute);
    }

    [Fact]
    public void Source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(SourcePath());

        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MarketDataRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MarketDataResponse", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FixSession", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_and_worker_live_gateway_remain_disabled()
    {
        var apiSettings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Api/appsettings.json"))).RootElement;
        var workerSettings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Worker/appsettings.json"))).RootElement;

        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(apiSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(workerSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
    }

    [Fact]
    public void Source_introduces_no_scheduler_timer_polling_service_or_background_job()
    {
        var source = File.ReadAllText(SourcePath());

        Assert.DoesNotContain("IHostedService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BackgroundService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PeriodicTimer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Threading.Timer", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Audusd_is_not_misclassified_as_failed()
    {
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.Equal(ApprovedInstrumentValidationStatus.PausedTlsBoundaryInconclusiveNotFailed, audusd.ValidationStatus);
    }

    [Fact]
    public void Usdjpy_caveat_remains_preserved()
    {
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var usdjpy = universe.Single(x => x.InternalInstrumentKey == "USDJPY");

        Assert.Equal("4004", usdjpy.SecurityId);
        Assert.Equal("8", usdjpy.SecurityIdSource);
        Assert.Equal(ApprovedInstrumentValidationStatus.NotProvenNotFailed, usdjpy.ValidationStatus);
    }

    private static string SourcePath()
        => Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/ExecutionAlgoCloseSeekingFoundation.cs");

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
