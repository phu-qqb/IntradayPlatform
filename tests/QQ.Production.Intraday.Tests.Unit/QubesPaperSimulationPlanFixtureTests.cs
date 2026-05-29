using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesPaperSimulationPlanFixtureTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task R015_simulation_ready_no_external_plan_can_create_paper_simulation_plan_fixture()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.True(result.Persisted);
        Assert.Equal(PaperSimulationPlanReadinessStatus.PaperSimulationPlanReady, result.Plan.ReadinessStatus);
        Assert.Equal(PaperSimulationPlanArchiveStatus.PreparedNoExternal, result.Plan.ArchiveStatus);
    }

    [Fact]
    public async Task Plan_preserves_paper_execution_plan_id()
    {
        var context = await CreateContextAsync();
        var result = await PrepareAsync(context);

        Assert.Equal(context.Approval.Decision.PaperExecutionPlanId, result.Plan.PaperExecutionPlanId);
    }

    [Fact]
    public async Task Plan_preserves_cycle_run_id_and_qubes_run_id()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.Equal("cycle-r016-sample", result.Plan.CycleRunId);
        Assert.Equal("qubes-r016-sample", result.Plan.QubesRunId);
    }

    [Fact]
    public async Task Plan_preserves_operator_decision_id()
    {
        var context = await CreateContextAsync();
        var result = await PrepareAsync(context);

        Assert.Equal(context.Approval.Decision.OperatorDecisionId, result.Plan.OperatorDecisionId);
        Assert.True(result.Plan.OperatorDecisionLineagePreserved);
    }

    [Fact]
    public async Task Plan_preserves_candidate_risk_rebalance_and_lot_sizing_lineage()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.True(result.Plan.PaperCandidateLineagePreserved);
        Assert.True(result.Plan.RiskLineagePreserved);
        Assert.True(result.Plan.RebalanceIntentLineagePreserved);
        Assert.True(result.Plan.LotSizingLineagePreserved);
    }

    [Fact]
    public async Task Audusd_buy_line_is_carried_into_simulation_plan()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.Contains(result.Plan.Lines, x => x.NormalizedSymbol == "AUDUSD" && x.Side == IntentSide.Buy && x.PaperBaseQuantity == 131000m);
    }

    [Fact]
    public async Task Eurusd_buy_line_is_carried_into_simulation_plan()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.Contains(result.Plan.Lines, x => x.NormalizedSymbol == "EURUSD" && x.Side == IntentSide.Buy && x.PaperBaseQuantity == 124000m);
    }

    [Fact]
    public async Task Gbpusd_sell_line_is_carried_into_simulation_plan()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.Contains(result.Plan.Lines, x => x.NormalizedSymbol == "GBPUSD" && x.Side == IntentSide.Sell && x.PaperBaseQuantity == 368000m);
    }

    [Fact]
    public async Task Simulation_assumptions_are_fixture_only_and_no_external()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.Equal("PaperNoExternal", result.Plan.Assumptions.SimulationMode);
        Assert.Equal("FixtureOnly", result.Plan.Assumptions.SlippageModel);
        Assert.Equal("FixtureOnly", result.Plan.Assumptions.FeeModel);
        Assert.Equal("FixtureOnly", result.Plan.Assumptions.MarketDataSource);
        Assert.Equal("None", result.Plan.Assumptions.ExecutionVenue);
        Assert.Equal("None", result.Plan.Assumptions.BrokerRoute);
        Assert.True(result.Plan.Assumptions.FixtureOnly);
        Assert.True(result.Plan.Assumptions.NoExternal);
    }

    [Fact]
    public async Task Simulation_plan_is_paper_only_no_external_and_non_executable()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.True(result.Plan.PaperOnly);
        Assert.True(result.Plan.NoExternal);
        Assert.True(result.Plan.NonExecutable);
        Assert.All(result.Plan.Lines, x => Assert.True(x.PaperOnly && x.NoExternal && x.NonExecutable));
    }

    [Fact]
    public async Task Simulation_plan_is_not_an_order_not_submitted_and_no_broker_route()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.True(result.Plan.NotAnOrder);
        Assert.True(result.Plan.NotSubmitted);
        Assert.True(result.Plan.NoBrokerRoute);
        Assert.All(result.Plan.Lines, x => Assert.True(x.NotAnOrder && x.NotSubmitted && x.NoBrokerRoute));
    }

    [Fact]
    public async Task Simulation_plan_explicitly_says_simulation_not_run()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.True(result.Plan.SimulationNotRun);
        Assert.False(result.Plan.RanPaperSimulation);
        Assert.Equal("NotRunYet", result.Plan.Assumptions.FillModel);
        Assert.All(result.Plan.Lines, x => Assert.True(x.SimulationNotRun));
    }

    [Fact]
    public async Task No_fills_are_created()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.True(result.Plan.NoFillCreated);
        Assert.False(result.Plan.CreatedFill);
        Assert.DoesNotContain(result.Plan.Lines, x => x.CreatesFill);
    }

    [Fact]
    public async Task No_execution_reports_are_created()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.True(result.Plan.NoExecutionReportCreated);
        Assert.False(result.Plan.CreatedExecutionReport);
        Assert.DoesNotContain(result.Plan.Lines, x => x.CreatesExecutionReport);
    }

    [Fact]
    public async Task No_oms_parent_child_or_broker_order_is_created()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.False(result.Plan.CreatedOmsOrder);
        Assert.False(result.Plan.CreatedParentOrder);
        Assert.False(result.Plan.CreatedChildOrder);
        Assert.False(result.Plan.CreatedBrokerOrder);
    }

    [Fact]
    public async Task No_order_submission_path_is_introduced()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.False(result.Plan.SubmittedOrders);
        Assert.All(result.Plan.Lines, x => Assert.True(x.NotSubmitted));
    }

    [Fact]
    public async Task Blocked_lines_are_preserved_separately()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.Equal(10, result.Plan.BlockedLines.Count);
        Assert.DoesNotContain(result.Plan.Lines, x => result.Plan.BlockedLines.Any(blocked => blocked.Symbol == x.NormalizedSymbol));
    }

    [Fact]
    public async Task Missing_stale_mark_warnings_are_preserved()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.True(result.Plan.MissingStaleMarkWarningsPreserved);
        Assert.True(result.Plan.MissingStaleMarkAcknowledgementPreserved);
    }

    [Fact]
    public async Task Drift_acknowledgement_is_preserved()
    {
        var result = await PrepareAsync(await CreateContextAsync());

        Assert.True(result.Plan.DriftAcknowledgementPreserved);
        Assert.True(result.Plan.OperatorApprovalAcknowledgementPreserved);
    }

    [Fact]
    public async Task Duplicate_simulation_plan_handling_is_idempotent()
    {
        var context = await CreateContextAsync();
        var repository = new InMemoryPaperSimulationPlanRepository();
        var service = new PaperSimulationPlanFixtureService(repository, new FixedClock(ProducedAt));

        var first = await service.PrepareAsync(context.Approval, CancellationToken.None);
        var second = await service.PrepareAsync(context.Approval, CancellationToken.None);

        Assert.True(first.Persisted);
        Assert.False(first.AlreadyPrepared);
        Assert.False(second.Persisted);
        Assert.True(second.AlreadyPrepared);
        Assert.Equal(PaperSimulationPlanArchiveStatus.DuplicateReturned, second.Plan.ArchiveStatus);
    }

    [Fact]
    public void Simulation_plan_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperSimulationPlanFixture.cs"));

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
    public void Simulation_plan_source_introduces_no_scheduler_timer_polling_or_background_job()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperSimulationPlanFixture.cs"));

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
        var result = await PrepareAsync(await CreateContextAsync());
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.Contains(result.Plan.Lines, x => x.NormalizedSymbol == "AUDUSD");
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

    private static async Task<PaperSimulationPlanFixtureResult> PrepareAsync(TestContext context)
        => await new PaperSimulationPlanFixtureService(
                new InMemoryPaperSimulationPlanRepository(),
                new FixedClock(ProducedAt))
            .PrepareAsync(context.Approval, CancellationToken.None);

    private static async Task<TestContext> CreateContextAsync()
    {
        var services = CreateServices();
        var cycle = await services.Cycle.RunOneCycleAsync(new QubesIntradayCycleFixtureRequest(
            "cycle-r016-sample",
            new QubesRunId("qubes-r016-sample"),
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
                "No-external paper simulation plan fixture.",
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

        return new TestContext(cycle, cycleArchive, review, paperReview, candidateBatch, candidateArchive, fixture, lotSized, plan, planArchive, approval);
    }

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var normalized = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId("qubes-r016-reference"),
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

    private sealed record TestContext(
        QubesIntradayCycleFixtureResult Cycle,
        IntradayCycleArchiveResult CycleArchive,
        IntradayCycleOperatorReviewResult OperatorReview,
        PaperOmsIntentReviewReport PaperReview,
        PaperOrderCandidateBatch CandidateBatch,
        PaperOrderCandidateArchiveResult CandidateArchive,
        PaperLotSizingFixtureContract Fixture,
        PaperLotSizedCandidateBatch LotSizedBatch,
        PaperExecutionPlanBatch Plan,
        PaperExecutionPlanArchiveResult PlanArchive,
        PaperExecutionPlanApprovalResult Approval);

    private sealed record TestServices(
        PlatformState State,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
        QubesIntradayCycleFixtureService Cycle,
        IntradayCycleArchiveService Archive,
        IntradayCycleOperatorReviewService OperatorReview);
}
