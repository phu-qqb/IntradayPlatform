using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionAlgoUsdPairSelectionPolicyTests
{
    [Fact]
    public void Mandatory_netting_scope_contract_exists()
    {
        var scope = ExecutionAlgoR002UsdPairSelectionPolicy.CreateNettingScope();

        Assert.True(scope.NettingRequiredBeforeExecutionSelection);
        Assert.True(scope.RawCrossesAreSignalsOnly);
        Assert.True(scope.ExecutionUniverseUsdPairOnly);
        Assert.False(scope.DirectCrossExecutionAllowedByDefault);
        Assert.False(string.IsNullOrWhiteSpace(scope.QubesRunId));
        Assert.False(string.IsNullOrWhiteSpace(scope.BarId));
    }

    [Fact]
    public void Usd_pair_execution_normalization_contract_exists()
    {
        var line = ExecutionAlgoR002UsdPairSelectionPolicy.NormalizeExposure("EUR", 100000m);

        Assert.Equal("EUR", line.PortfolioCurrency);
        Assert.Equal("EURUSD", line.PortfolioNormalizedSymbol);
        Assert.Equal("EURUSD", line.ExecutionTradableSymbol);
        Assert.False(line.RequiresInversion);
        Assert.Equal(UsdPairNormalizationStatus.Ready, line.NormalizationStatus);
        Assert.False(line.DirectCrossExecutionAllowed);
    }

    [Fact]
    public void Raw_direct_crosses_are_not_accepted_as_execution_instruments_by_default()
    {
        var line = ExecutionAlgoR002UsdPairSelectionPolicy.BlockRawDirectCross("EURGBP");

        Assert.False(line.DirectCrossExecutionAllowed);
        Assert.Equal(UsdPairNormalizationStatus.DirectCrossExecutionDisabled, line.NormalizationStatus);
        Assert.Null(line.ExecutionTradableSymbol);
    }

    [Fact]
    public void Eurgbp_raw_input_is_blocked_from_direct_execution_or_requires_netting_first()
    {
        var decision = Scenario("scenario-direct-cross-eurgbp");

        Assert.Equal(AlgoPolicyReasonCategory.DirectCrossExecutionDisabled, decision.ReasonCategory);
        Assert.Equal(ExecutionAlgoFamily.ManualReview, decision.SelectedAlgoFamily);
        Assert.False(decision.IsExecutable);
    }

    [Theory]
    [InlineData("EUR", "EURUSD")]
    [InlineData("GBP", "GBPUSD")]
    [InlineData("AUD", "AUDUSD")]
    [InlineData("NZD", "NZDUSD")]
    public void Usd_quote_exposures_map_without_inversion(string currency, string expectedSymbol)
    {
        var line = ExecutionAlgoR002UsdPairSelectionPolicy.NormalizeExposure(currency, 100000m);

        Assert.Equal(expectedSymbol, line.ExecutionTradableSymbol);
        Assert.False(line.RequiresInversion);
        Assert.Equal(FxExecutionSide.Buy, line.PortfolioSide);
        Assert.Equal(FxExecutionSide.Buy, line.ExecutionSide);
    }

    [Theory]
    [InlineData("JPY", "USDJPY")]
    [InlineData("CAD", "USDCAD")]
    [InlineData("CHF", "USDCHF")]
    [InlineData("MXN", "USDMXN")]
    [InlineData("CNH", "USDCNH")]
    [InlineData("NOK", "USDNOK")]
    [InlineData("SEK", "USDSEK")]
    [InlineData("ZAR", "USDZAR")]
    public void Inverted_usd_pairs_map_with_inversion(string currency, string expectedSymbol)
    {
        var line = ExecutionAlgoR002UsdPairSelectionPolicy.NormalizeExposure(currency, 100000m);

        Assert.Equal(expectedSymbol, line.ExecutionTradableSymbol);
        Assert.True(line.RequiresInversion);
        Assert.Equal(FxExecutionSide.Buy, line.PortfolioSide);
        Assert.Equal(FxExecutionSide.Sell, line.ExecutionSide);
    }

    [Theory]
    [InlineData("JPY", 100000, FxExecutionSide.Buy, FxExecutionSide.Sell)]
    [InlineData("JPY", -100000, FxExecutionSide.Sell, FxExecutionSide.Buy)]
    [InlineData("CAD", 100000, FxExecutionSide.Buy, FxExecutionSide.Sell)]
    [InlineData("CHF", -100000, FxExecutionSide.Sell, FxExecutionSide.Buy)]
    public void Inversion_flips_execution_side_correctly(string currency, int quantity, FxExecutionSide portfolioSide, FxExecutionSide executionSide)
    {
        var transform = ExecutionAlgoR002UsdPairSelectionPolicy.CreateInversionTransform(currency, quantity);

        Assert.True(transform.RequiresInversion);
        Assert.Equal(portfolioSide, transform.PortfolioSide);
        Assert.Equal(executionSide, transform.ExecutionSide);
        Assert.True(transform.TransformAvailable);
    }

    [Fact]
    public void Missing_inversion_transform_blocks_or_requires_manual_review()
    {
        var input = BaseInput("missing-inversion", "JPY", "JPYUSD", 100000m) with { InversionTransformAvailable = false };

        var decision = ExecutionAlgoR002UsdPairSelectionPolicy.Select(input);

        Assert.Equal(AlgoPolicyReasonCategory.MissingInversionTransform, decision.ReasonCategory);
        Assert.Equal(ExecutionAlgoFamily.ManualReview, decision.SelectedAlgoFamily);
        Assert.False(decision.IsExecutable);
    }

    [Fact]
    public void Missing_usd_pair_mapping_blocks_or_requires_manual_review()
    {
        var input = BaseInput("missing-mapping", "TRY", "TRYUSD", 100000m);

        var decision = ExecutionAlgoR002UsdPairSelectionPolicy.Select(input);

        Assert.Equal(AlgoPolicyReasonCategory.MissingUsdPairExecutionMapping, decision.ReasonCategory);
        Assert.Equal(ExecutionAlgoFamily.ManualReview, decision.SelectedAlgoFamily);
    }

    [Fact]
    public void Sgd_missing_configured_convention_requires_manual_review()
    {
        var decision = Scenario("scenario-sgd-missing-convention");

        Assert.Equal(AlgoPolicyReasonCategory.MissingInstrumentConvention, decision.ReasonCategory);
        Assert.Equal(ExecutionAlgoFamily.ManualReview, decision.SelectedAlgoFamily);
    }

    [Fact]
    public void Algo_selection_policy_contract_exists()
    {
        var result = ExecutionAlgoR002UsdPairSelectionPolicy.CreateFixturePolicyResult();

        Assert.NotEmpty(result.Decisions);
        Assert.True(result.DirectCrossExecutionDisabledByDefault);
    }

    [Fact]
    public void Policy_consumes_close_benchmark_readiness()
    {
        var decision = Scenario("scenario-missing-close");

        Assert.Equal(CloseBenchmarkAvailabilityStatus.MissingCloseBenchmark, decision.BenchmarkAvailabilityStatus);
        Assert.Equal(AlgoPolicyReasonCategory.MissingCloseBenchmark, decision.ReasonCategory);
    }

    [Fact]
    public void Policy_consumes_feed_readiness()
    {
        var decision = Scenario("scenario-missing-feed");

        Assert.Equal(FeedReadinessStatus.MissingFeedContinuity, decision.FeedReadinessStatus);
        Assert.Equal(AlgoPolicyReasonCategory.MissingFeedContinuity, decision.ReasonCategory);
    }

    [Fact]
    public void Policy_consumes_spread_cost()
    {
        var decision = Scenario("scenario-wide-spread");

        Assert.Equal(PolicyCostStatus.TooHigh, decision.SpreadCostStatus);
        Assert.Equal(AlgoPolicyReasonCategory.SpreadTooWide, decision.ReasonCategory);
    }

    [Fact]
    public void Policy_consumes_opportunity_nonfill_and_residual_cost()
    {
        var decision = Scenario("scenario-high-residual-near-close");

        Assert.Equal(PolicyCostStatus.Acceptable, decision.OpportunityCostStatus);
        Assert.Equal(PolicyCostStatus.Acceptable, decision.NonFillRiskStatus);
        Assert.Equal(PolicyCostStatus.Acceptable, decision.ResidualRiskStatus);
        Assert.Equal(ExecutionAlgoFamily.ControlledResidualCross, decision.SelectedAlgoFamily);
    }

    [Fact]
    public void Good_feed_tight_spread_selects_passive_until_urgency_or_close_seeking()
    {
        var decision = Scenario("scenario-eur-good-feed");

        Assert.Contains(decision.SelectedAlgoFamily, new[] { ExecutionAlgoFamily.PassiveUntilUrgency, ExecutionAlgoFamily.CloseSeeking15m });
        Assert.Equal(CostControlStatus.Pass, decision.CostControlStatus);
    }

    [Fact]
    public void Moderate_residual_selects_close_seeking_15m_adaptive()
    {
        var decision = Scenario("scenario-moderate-residual");

        Assert.Equal(ExecutionAlgoFamily.CloseSeeking15mAdaptive, decision.SelectedAlgoFamily);
        Assert.Equal(CloseSeekingPhasePolicy.AdaptiveUrgencyWindow, decision.SelectedPhasePolicy);
    }

    [Fact]
    public void High_residual_near_close_with_opportunity_cost_greater_than_spread_cost_selects_controlled_residual_cross()
    {
        var decision = Scenario("scenario-high-residual-near-close");

        Assert.Equal(ExecutionAlgoFamily.ControlledResidualCross, decision.SelectedAlgoFamily);
        Assert.Equal(CloseSeekingPhasePolicy.ControlledResidualCompletionWindow, decision.SelectedPhasePolicy);
        Assert.Equal(AlgoPolicyReasonCategory.ReadyForControlledResidualCross, decision.ReasonCategory);
    }

    [Fact]
    public void Wide_spread_blocks_or_requires_manual_review()
    {
        var decision = Scenario("scenario-wide-spread");

        Assert.Equal(ExecutionAlgoFamily.ManualReview, decision.SelectedAlgoFamily);
        Assert.Equal(CostControlStatus.ManualReview, decision.CostControlStatus);
    }

    [Fact]
    public void Missing_close_benchmark_blocks_or_requires_manual_review()
    {
        var decision = Scenario("scenario-missing-close");

        Assert.Equal(ExecutionAlgoFamily.ManualReview, decision.SelectedAlgoFamily);
        Assert.False(decision.IsExecutable);
    }

    [Fact]
    public void Missing_feed_continuity_blocks_or_requires_manual_review()
    {
        var decision = Scenario("scenario-missing-feed");

        Assert.Equal(ExecutionAlgoFamily.ManualReview, decision.SelectedAlgoFamily);
        Assert.False(decision.IsExecutable);
    }

    [Fact]
    public void Stale_quote_near_close_blocks_or_requires_manual_review()
    {
        var decision = Scenario("scenario-stale-quote");

        Assert.Equal(AlgoPolicyReasonCategory.StaleQuoteNearClose, decision.ReasonCategory);
        Assert.Equal(ExecutionAlgoFamily.ManualReview, decision.SelectedAlgoFamily);
    }

    [Fact]
    public void Pure_limit_until_close_is_blocked_as_default()
    {
        var decision = Scenario("scenario-wakett-pure-limit");

        Assert.Equal(AlgoPolicyReasonCategory.WakettPatternBlocked, decision.ReasonCategory);
        Assert.Equal(ExecutionAlgoFamily.ManualReview, decision.SelectedAlgoFamily);
    }

    [Fact]
    public void Mechanical_five_market_slice_pattern_is_blocked()
    {
        var decision = Scenario("scenario-wakett-five-market-slices");

        Assert.Equal(AlgoPolicyReasonCategory.WakettPatternBlocked, decision.ReasonCategory);
        Assert.Equal(ExecutionAlgoFamily.ManualReview, decision.SelectedAlgoFamily);
    }

    [Fact]
    public void Blind_market_crossing_is_blocked_unless_residual_opportunity_cost_model_justifies_it()
    {
        var blocked = Scenario("scenario-wakett-five-market-slices");
        var controlled = Scenario("scenario-high-residual-near-close");

        Assert.Equal(AlgoPolicyReasonCategory.WakettPatternBlocked, blocked.ReasonCategory);
        Assert.Equal(ExecutionAlgoFamily.ControlledResidualCross, controlled.SelectedAlgoFamily);
        Assert.Equal(AlgoPolicyReasonCategory.ReadyForControlledResidualCross, controlled.ReasonCategory);
    }

    [Fact]
    public void Twap_benchmark_only_is_benchmark_only_and_non_executable()
    {
        var decision = Scenario("scenario-twap-benchmark");

        Assert.Equal(ExecutionAlgoFamily.TWAPBenchmarkOnly, decision.SelectedAlgoFamily);
        Assert.Equal(CloseSeekingPhasePolicy.BenchmarkOnly, decision.SelectedPhasePolicy);
        Assert.False(decision.IsExecutable);
        Assert.False(decision.CreatesOrder);
    }

    [Fact]
    public void Vwap_benchmark_only_is_benchmark_only_and_non_executable()
    {
        var decision = Scenario("scenario-vwap-benchmark");

        Assert.Equal(ExecutionAlgoFamily.VWAPBenchmarkOnly, decision.SelectedAlgoFamily);
        Assert.Equal(CloseSeekingPhasePolicy.BenchmarkOnly, decision.SelectedPhasePolicy);
        Assert.False(decision.IsExecutable);
        Assert.False(decision.CreatesOrder);
    }

    [Fact]
    public void All_algo_decisions_are_design_only_paper_only_non_executable_not_order_not_submitted_no_broker_route()
    {
        var decisions = ExecutionAlgoR002UsdPairSelectionPolicy.CreateFixturePolicyResult().Decisions;

        Assert.All(decisions, x =>
        {
            Assert.True(x.IsDesignOnly);
            Assert.True(x.IsPaperOnly);
            Assert.False(x.IsExecutable);
            Assert.False(x.IsSubmitted);
            Assert.False(x.HasBrokerRoute);
        });
    }

    [Fact]
    public void Algo_selection_does_not_create_orders_fills_execution_reports_routes_or_submissions()
    {
        var result = ExecutionAlgoR002UsdPairSelectionPolicy.CreateFixturePolicyResult();

        Assert.True(result.NoOrdersCreated);
        Assert.True(result.NoFillsCreated);
        Assert.True(result.NoExecutionReportsCreated);
        Assert.True(result.NoRoutesCreated);
        Assert.True(result.NoSubmissionsCreated);
        Assert.All(result.Decisions, x =>
        {
            Assert.False(x.CreatesOrder);
            Assert.False(x.CreatesFill);
            Assert.False(x.CreatesExecutionReport);
            Assert.False(x.CreatesRoute);
            Assert.False(x.CreatesSubmission);
        });
    }

    [Fact]
    public void Pms_ems_oms_lineage_is_preserved()
    {
        var decision = Scenario("scenario-eur-good-feed");

        Assert.Equal("cycle-r029-manual-paper-fixture", decision.CycleRunId);
        Assert.Equal("qubes-r029-manual-fixture", decision.QubesRunId);
        Assert.Contains("paper-execution-plan-line", decision.PaperExecutionPlanLineId);
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

    private static CloseSeekingAlgoSelectionDecision Scenario(string id)
        => ExecutionAlgoR002UsdPairSelectionPolicy.CreateFixturePolicyResult().Decisions.Single(x => x.AlgoSelectionDecisionId == id);

    private static CloseSeekingAlgoPolicyLineInput BaseInput(string id, string currency, string normalizedSymbol, decimal quantity)
        => ExecutionAlgoR002UsdPairSelectionPolicy.CreateFixtureInputs()[1] with
        {
            AlgoSelectionDecisionId = id,
            PortfolioCurrency = currency,
            PortfolioNormalizedSymbol = normalizedSymbol,
            PaperBaseQuantity = quantity
        };

    private static string SourcePath()
        => Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/ExecutionAlgoUsdPairSelectionPolicy.cs");

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
