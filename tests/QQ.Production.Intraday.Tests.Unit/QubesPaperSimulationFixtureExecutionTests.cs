using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesPaperSimulationFixtureExecutionTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task R016_R017_simulation_ready_plan_can_produce_no_external_paper_simulation_fixture_result()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        Assert.True(result.Persisted);
        Assert.Equal(PaperSimulationResultStatus.CompletedNoExternalFixture, result.Result.ResultStatus);
        Assert.Equal("CompletedNoExternalFixture", result.Result.Summary.SimulationState);
    }

    [Fact]
    public async Task Audusd_buy_line_is_simulated_as_paper_only()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        var line = Assert.Single(result.Result.Lines, x => x.NormalizedSymbol == "AUDUSD");
        Assert.Equal(IntentSide.Buy, line.Side);
        Assert.True(line.ResultIsPaperOnly);
        Assert.Equal(PaperSimulationOutcomeCategory.PaperApplied, line.SimulatedOutcomeCategory);
    }

    [Fact]
    public async Task Eurusd_buy_line_is_simulated_as_paper_only()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        var line = Assert.Single(result.Result.Lines, x => x.NormalizedSymbol == "EURUSD");
        Assert.Equal(IntentSide.Buy, line.Side);
        Assert.True(line.ResultIsPaperOnly);
        Assert.Equal(PaperSimulationOutcomeCategory.PaperApplied, line.SimulatedOutcomeCategory);
    }

    [Fact]
    public async Task Gbpusd_sell_line_is_simulated_as_paper_only()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        var line = Assert.Single(result.Result.Lines, x => x.NormalizedSymbol == "GBPUSD");
        Assert.Equal(IntentSide.Sell, line.Side);
        Assert.True(line.ResultIsPaperOnly);
        Assert.Equal(PaperSimulationOutcomeCategory.PaperApplied, line.SimulatedOutcomeCategory);
    }

    [Fact]
    public async Task Simulated_applied_quantities_match_paper_quantities()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        Assert.Contains(result.Result.Lines, x => x.NormalizedSymbol == "AUDUSD" && x.SimulatedAppliedQuantity == 131000m);
        Assert.Contains(result.Result.Lines, x => x.NormalizedSymbol == "EURUSD" && x.SimulatedAppliedQuantity == 124000m);
        Assert.Contains(result.Result.Lines, x => x.NormalizedSymbol == "GBPUSD" && x.SimulatedAppliedQuantity == 368000m);
    }

    [Fact]
    public async Task Simulation_summary_reports_real_fill_count_zero()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        Assert.Equal(0, result.Result.Summary.RealFillCount);
        Assert.True(result.Result.NoRealFillCreated);
    }

    [Fact]
    public async Task Execution_report_count_is_zero()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        Assert.Equal(0, result.Result.Summary.ExecutionReportCount);
        Assert.True(result.Result.NoExecutionReportCreated);
    }

    [Fact]
    public async Task Order_count_is_zero()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        Assert.Equal(0, result.Result.Summary.OrderCount);
        Assert.True(result.Result.NoOrderCreated);
    }

    [Fact]
    public async Task Broker_route_count_is_zero()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        Assert.Equal(0, result.Result.Summary.BrokerRouteCount);
        Assert.True(result.Result.NoBrokerRoute);
    }

    [Fact]
    public async Task No_real_fill_entities_are_created()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        Assert.False(result.Result.RealFillEntityCreated);
        Assert.DoesNotContain(result.Result.Lines, x => x.RealFillEntityCreated);
    }

    [Fact]
    public async Task No_execution_report_entities_are_created()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        Assert.False(result.Result.BrokerExecutionReportEntityCreated);
        Assert.DoesNotContain(result.Result.Lines, x => x.BrokerExecutionReportEntityCreated);
    }

    [Fact]
    public async Task No_oms_parent_child_or_broker_order_is_created()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        Assert.False(result.Result.OmsOrderCreated);
        Assert.False(result.Result.ParentOrderCreated);
        Assert.False(result.Result.ChildOrderCreated);
        Assert.False(result.Result.BrokerOrderCreated);
    }

    [Fact]
    public async Task No_order_state_is_created()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        Assert.False(result.Result.OrderStateCreated);
        Assert.DoesNotContain(result.Result.Lines, x => x.OrderStateCreated);
    }

    [Fact]
    public async Task No_order_submission_path_is_introduced()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        Assert.False(result.Result.SubmittedOrders);
        Assert.DoesNotContain(result.Result.Lines, x => x.Submitted);
    }

    [Fact]
    public async Task Post_trade_preview_is_simulated_only_and_does_not_mutate_live_state()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        Assert.True(result.Result.PostTradePreview.SimulatedOnly);
        Assert.False(result.Result.PostTradePreview.LivePositionStateMutated);
        Assert.False(result.Result.PostTradePreview.BrokerStateMutated);
        Assert.False(result.Result.PostTradePreview.TradingStateMutated);
        Assert.Contains(result.Result.PostTradePreview.PositionDeltas, x => x.NormalizedSymbol == "GBPUSD" && x.PaperQuantityDelta == -368000m);
    }

    [Fact]
    public async Task Blocked_lines_are_preserved_separately()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        Assert.Equal(10, result.Result.BlockedLines.Count);
        Assert.Equal(10, result.Result.Summary.BlockedLines);
    }

    [Fact]
    public async Task Missing_stale_mark_warnings_are_preserved()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        Assert.True(result.Result.MissingStaleMarkWarningsPreserved);
        Assert.True(result.Result.MissingStaleMarkAcknowledgementPreserved);
    }

    [Fact]
    public async Task Drift_acknowledgement_is_preserved()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        Assert.True(result.Result.DriftAcknowledgementPreserved);
        Assert.True(result.Result.OperatorApprovalAcknowledgementPreserved);
    }

    [Fact]
    public async Task Duplicate_simulation_result_handling_is_idempotent()
    {
        var context = await CreateContextAsync();
        var repository = new InMemoryPaperSimulationFixtureResultRepository();
        var service = new PaperSimulationFixtureExecutor(repository, new FixedClock(ProducedAt));

        var first = await service.ExecuteAsync(context.DryRun.Result, CancellationToken.None);
        var second = await service.ExecuteAsync(context.DryRun.Result, CancellationToken.None);

        Assert.True(first.Persisted);
        Assert.False(first.AlreadyExecuted);
        Assert.False(second.Persisted);
        Assert.True(second.AlreadyExecuted);
        Assert.Equal(PaperSimulationResultStatus.DuplicateReturned, second.Result.ResultStatus);
    }

    [Fact]
    public async Task Qubes_cycle_operator_plan_candidate_risk_rebalance_and_lot_sizing_lineage_is_preserved()
    {
        var result = await ExecuteAsync(await CreateContextAsync());

        Assert.True(result.Result.QubesLineagePreserved);
        Assert.True(result.Result.CycleLineagePreserved);
        Assert.True(result.Result.OperatorDecisionLineagePreserved);
        Assert.True(result.Result.PlanLineagePreserved);
        Assert.True(result.Result.PaperCandidateLineagePreserved);
        Assert.True(result.Result.RiskLineagePreserved);
        Assert.True(result.Result.RebalanceIntentLineagePreserved);
        Assert.True(result.Result.LotSizingLineagePreserved);
    }

    [Fact]
    public void Simulation_fixture_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperSimulationFixtureExecution.cs"));

        Assert.DoesNotContain("SendOrderAsync", source);
        Assert.DoesNotContain("SubmitOrder", source);
        Assert.DoesNotContain("TcpClient", source);
        Assert.DoesNotContain("SslStream", source);
        Assert.DoesNotContain("MarketDataRequest", source);
        Assert.DoesNotContain("MarketDataResponse", source);
        Assert.DoesNotContain("FixSession", source);
    }

    [Fact]
    public void Api_and_worker_live_gateway_remain_disabled()
    {
        var apiProgram = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Api/Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Worker/Program.cs"));
        var apiSettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Api/appsettings.json"))).RootElement;
        var workerSettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Worker/appsettings.json"))).RootElement;

        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", apiProgram);
        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", workerProgram);
        Assert.DoesNotContain("RealLmaxGateway", apiProgram + workerProgram);
        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(apiSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(workerSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
    }

    [Fact]
    public void Simulation_fixture_source_introduces_no_scheduler_timer_polling_or_background_job()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperSimulationFixtureExecution.cs"));

        Assert.DoesNotContain("AddHostedService", source);
        Assert.DoesNotContain("IHostedService", source);
        Assert.DoesNotContain("BackgroundService", source);
        Assert.DoesNotContain("PeriodicTimer", source);
        Assert.DoesNotContain("Task.Delay", source);
        Assert.DoesNotContain("System.Threading.Timer", source);
    }

    [Fact]
    public async Task Audusd_is_not_misclassified_as_failed()
    {
        var result = await ExecuteAsync(await CreateContextAsync());
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.Contains(result.Result.Lines, x => x.NormalizedSymbol == "AUDUSD");
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

    private static async Task<PaperSimulationFixtureExecutionResult> ExecuteAsync(TestContext context)
        => await new PaperSimulationFixtureExecutor(
                new InMemoryPaperSimulationFixtureResultRepository(),
                new FixedClock(ProducedAt))
            .ExecuteAsync(context.DryRun.Result, CancellationToken.None);

    private static async Task<TestContext> CreateContextAsync()
    {
        var services = CreateServices();
        var cycle = await services.Cycle.RunOneCycleAsync(new QubesIntradayCycleFixtureRequest(
            "cycle-r018-sample",
            new QubesRunId("qubes-r018-sample"),
            ProducedAt,
            EffectiveAt,
            "QQ_MASTER",
            "IntradayFxModel",
            PortfolioNotional,
            TargetQuantityMode.PortfolioBaseCurrencyNotional,
            SampleLines(),
            services.InstrumentIdsBySymbol,
            ProducedAt,
            EffectiveAt,
            0.0001m,
            100m,
            10m,
            TimeSpan.FromMinutes(20),
            1),
            CancellationToken.None);
        var cycleArchive = await services.Archive.ArchiveAsync(cycle, CancellationToken.None);
        var review = await services.OperatorReview.ReviewAsync(
            cycleArchive.ArchiveRecord,
            new IntradayCycleOperatorDecisionRequest(
                OperatorDecisionId.New(),
                OperatorDecisionType.PromoteToPaperReady,
                "operator-placeholder",
                OperatorDecisionReasonCategory.AcceptedForPaperReview,
                "No-external paper simulation fixture execution.",
                MissingStaleMarksAcknowledged: true,
                DriftAcknowledged: true),
            CancellationToken.None);
        var paperReview = new PaperOmsIntentReviewService().Review(new PaperOmsIntentReviewRequest(
            cycle,
            review,
            new PaperPreTradeRiskLimits(
                2m,
                2m,
                5_000_000m,
                2_000_000m,
                cycle.TheoreticalPortfolioDiff.RebalanceIntents.Select(x => x.Symbol).ToHashSet(StringComparer.OrdinalIgnoreCase))));
        var candidateBatch = new PaperOrderCandidateShapeService().Create(new PaperOrderCandidateShapeRequest(
            cycle,
            review,
            paperReview));
        var candidateArchive = await new PaperOrderCandidateArchiveService(
                new InMemoryPaperOrderCandidateArchiveRepository(),
                new FixedClock(ProducedAt))
            .ArchiveAsync(candidateBatch, CancellationToken.None);
        var fixture = PaperLotSizingService.CreateDefaultFixture(candidateArchive.ArchiveRecord.CandidateLines.Select(x => x.NormalizedSymbol));
        var lotSized = new PaperLotSizingService().Size(new PaperLotSizingRequest(candidateArchive, fixture));
        var plan = new PaperExecutionPlanShapeService().Create(lotSized);
        var planArchive = await new PaperExecutionPlanArchiveService(
                new InMemoryPaperExecutionPlanArchiveRepository(),
                new FixedClock(ProducedAt))
            .ArchiveAsync(plan, CancellationToken.None);
        var approval = await new PaperExecutionPlanApprovalService(
                new InMemoryPaperExecutionPlanApprovalRepository(),
                new FixedClock(ProducedAt))
            .ReviewAsync(
                planArchive.ArchiveRecord,
                new PaperExecutionPlanApprovalRequest(
                    OperatorDecisionId.New(),
                    PaperExecutionPlanOperatorAction.ApproveForPaperSimulation,
                    "operator-placeholder",
                    PaperPlanApprovalReasonCategory.AcceptedForSimulationReadiness,
                    "No-external simulation readiness only.",
                    BlockedLinesAcknowledged: true,
                    MissingStaleMarksAcknowledged: true,
                    DriftAcknowledged: true),
                CancellationToken.None);
        var simulationPlan = await new PaperSimulationPlanFixtureService(
                new InMemoryPaperSimulationPlanRepository(),
                new FixedClock(ProducedAt))
            .PrepareAsync(approval, CancellationToken.None);
        var dryRun = await new PaperSimulationDryRunResultShapeService(
                new InMemoryPaperSimulationDryRunResultRepository(),
                new FixedClock(ProducedAt))
            .CreateAsync(simulationPlan.Plan, CancellationToken.None);

        return new TestContext(dryRun);
    }

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var normalized = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId("qubes-r018-reference"),
            ProducedAt,
            EffectiveAt,
            15,
            "QQ_MASTER",
            "IntradayFxModel",
            PortfolioNotional,
            TargetQuantityMode.PortfolioBaseCurrencyNotional,
            SampleLines())).NormalizedWeights;
        EnsureInstruments(state, normalized.Select(x => x.Symbol));
        var idsBySymbol = normalized.ToDictionary(
            x => x.Symbol,
            x => state.Instruments.Single(instrument => instrument.Symbol.Equals(x.Symbol, StringComparison.OrdinalIgnoreCase)).Id,
            StringComparer.OrdinalIgnoreCase);
        var clock = new FixedClock(ProducedAt);
        var intradayRepository = new InMemoryIntradayRepository(state);
        var batchRepository = new InMemoryModelWeightBatchRepository(state);
        var auditRepository = new InMemoryQubesWeightAuditRepository();
        var integrity = new ReferenceDataIntegrityService(intradayRepository, clock);
        var cycle = new QubesIntradayCycleFixtureService(
            new FakeModelWeightGenerator(batchRepository, clock),
            new ModelWeightPromotionService(batchRepository, intradayRepository, integrity, clock),
            new QubesWeightPersistenceService(auditRepository, clock));
        var archive = new IntradayCycleArchiveService(new InMemoryIntradayCycleArchiveRepository(), clock);
        var review = new IntradayCycleOperatorReviewService(new InMemoryIntradayCycleOperatorDecisionRepository(), clock);

        return new TestServices(state, idsBySymbol, cycle, archive, review);
    }

    private static void EnsureInstruments(PlatformState state, IEnumerable<string> symbols)
    {
        foreach (var symbol in symbols.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (state.Instruments.Any(x => x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            state.Instruments.Add(new Instrument(
                InstrumentId.New(),
                symbol,
                AssetClass.FxSpot,
                new Currency(symbol[..3]),
                Currency.Usd,
                5,
                1));
        }
    }

    private static IReadOnlyList<string> SampleLines()
        => File.ReadAllLines(Path.Combine(RepoRoot(), "tests/fixtures/qubes-fx/qubes-fx-weights-r002-sample.csv"));

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be found.");
    }

    private sealed record TestContext(PaperSimulationDryRunResultShapeResult DryRun);

    private sealed record TestServices(
        PlatformState State,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
        QubesIntradayCycleFixtureService Cycle,
        IntradayCycleArchiveService Archive,
        IntradayCycleOperatorReviewService OperatorReview);
}
