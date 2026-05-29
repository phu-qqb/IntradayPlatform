using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesPaperPositionLedgerPreviewTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task R021_archived_paper_position_preview_can_produce_paper_ledger_preview()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.True(result.Persisted);
        Assert.Equal(PaperPositionLedgerPreviewStatus.PaperLedgerPreviewReady, result.Preview.PreviewStatus);
    }

    [Fact]
    public async Task Ledger_preview_preserves_paper_position_preview_id()
    {
        var context = await CreateContextAsync();
        var result = await PreviewAsync(context);

        Assert.Equal(context.PositionArchive.ArchiveRecord.PaperPositionPreviewId, result.Preview.PaperPositionPreviewId);
    }

    [Fact]
    public async Task Ledger_preview_preserves_paper_simulation_result_id()
    {
        var context = await CreateContextAsync();
        var result = await PreviewAsync(context);

        Assert.Equal(context.PositionArchive.ArchiveRecord.PaperSimulationResultId, result.Preview.PaperSimulationResultId);
    }

    [Fact]
    public async Task Ledger_preview_preserves_cycle_run_id_and_qubes_run_id()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.Equal("cycle-r022-sample", result.Preview.CycleRunId);
        Assert.Equal("qubes-r022-sample", result.Preview.QubesRunId);
    }

    [Fact]
    public async Task Ledger_preview_preserves_operator_decision_id()
    {
        var context = await CreateContextAsync();
        var result = await PreviewAsync(context);

        Assert.Equal(context.PositionArchive.ArchiveRecord.OperatorDecisionId, result.Preview.OperatorDecisionId);
    }

    [Fact]
    public async Task Audusd_preview_line_applies_delta()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.Contains(result.Preview.Lines, x => x.CurrencyOrSymbol == "AUDUSD" && x.SimulatedDeltaQuantity == 131000m && x.PreviewEndingPaperQuantity == 131000m && x.QuantityCurrency == "AUD");
    }

    [Fact]
    public async Task Eurusd_preview_line_applies_delta()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.Contains(result.Preview.Lines, x => x.CurrencyOrSymbol == "EURUSD" && x.SimulatedDeltaQuantity == 124000m && x.PreviewEndingPaperQuantity == 124000m && x.QuantityCurrency == "EUR");
    }

    [Fact]
    public async Task Gbpusd_preview_line_applies_sell_delta()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.Contains(result.Preview.Lines, x => x.CurrencyOrSymbol == "GBPUSD" && x.SimulatedDeltaQuantity == -368000m && x.PreviewEndingPaperQuantity == -368000m && x.QuantityCurrency == "GBP");
    }

    [Fact]
    public async Task Starting_paper_quantities_come_from_no_external_fixture_not_broker_state()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.Equal("NoExternalZeroPaperLedgerBaselineFixture", result.Preview.BaselineFixture.BaselineSource);
        Assert.True(result.Preview.BaselineFixture.NoExternal);
        Assert.True(result.Preview.BaselineFixture.NotBrokerState);
        Assert.True(result.Preview.BaselineFixture.NotLiveProductionPositionState);
        Assert.True(result.Preview.BaselineFixture.NotPersistedProductionLedgerState);
        Assert.All(result.Preview.Lines, x => Assert.Equal(0m, x.StartingPaperQuantity));
    }

    [Fact]
    public async Task Ending_paper_quantities_are_computed_deterministically()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.All(result.Preview.Lines, x => Assert.Equal(x.StartingPaperQuantity + x.SimulatedDeltaQuantity, x.PreviewEndingPaperQuantity));
    }

    [Fact]
    public async Task Live_position_mutation_count_is_zero()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.Equal(0, result.Preview.Summary.LivePositionMutationCount);
    }

    [Fact]
    public async Task Broker_position_mutation_count_is_zero()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.Equal(0, result.Preview.Summary.BrokerPositionMutationCount);
    }

    [Fact]
    public async Task Trading_state_mutation_count_is_zero()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.Equal(0, result.Preview.Summary.TradingStateMutationCount);
    }

    [Fact]
    public async Task No_live_position_state_is_mutated()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.True(result.Preview.NoLivePositionMutation);
        Assert.False(result.Preview.LivePositionStateMutated);
    }

    [Fact]
    public async Task No_broker_position_state_is_mutated()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.True(result.Preview.NoBrokerPositionMutation);
        Assert.False(result.Preview.BrokerPositionStateMutated);
    }

    [Fact]
    public async Task No_production_ledger_state_is_mutated()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.True(result.Preview.NoProductionLedgerMutation);
        Assert.False(result.Preview.ProductionLedgerStateMutated);
        Assert.Equal(0, result.Preview.Summary.ProductionLedgerMutationCount);
    }

    [Fact]
    public async Task No_trading_state_is_mutated()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.True(result.Preview.NoTradingStateMutation);
        Assert.False(result.Preview.TradingStateMutated);
    }

    [Fact]
    public async Task No_fills_are_created()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.True(result.Preview.NoFillCreated);
        Assert.False(result.Preview.FillCreated);
    }

    [Fact]
    public async Task No_execution_reports_are_created()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.True(result.Preview.NoExecutionReportCreated);
        Assert.False(result.Preview.ExecutionReportCreated);
    }

    [Fact]
    public async Task No_oms_or_broker_orders_are_created()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.True(result.Preview.NoOrderCreated);
        Assert.False(result.Preview.OmsOrderCreated);
        Assert.False(result.Preview.BrokerOrderCreated);
        Assert.False(result.Preview.OrderStateCreated);
    }

    [Fact]
    public async Task No_order_submission_path_is_introduced()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.True(result.Preview.NotSubmitted);
        Assert.False(result.Preview.SubmittedOrders);
        Assert.False(result.Preview.BrokerRouteCreated);
    }

    [Fact]
    public async Task Duplicate_ledger_preview_handling_is_idempotent()
    {
        var context = await CreateContextAsync();
        var repository = new InMemoryPaperPositionLedgerPreviewRepository();
        var service = new PaperPositionLedgerPreviewService(repository, new FixedClock(ProducedAt));

        var first = await service.CreateAsync(context.PositionArchive.ArchiveRecord, null, CancellationToken.None);
        var second = await service.CreateAsync(context.PositionArchive.ArchiveRecord, null, CancellationToken.None);

        Assert.True(first.Persisted);
        Assert.False(first.AlreadyCreated);
        Assert.False(second.Persisted);
        Assert.True(second.AlreadyCreated);
        Assert.Equal(first.Preview.PaperPositionLedgerPreviewId, second.Preview.PaperPositionLedgerPreviewId);
    }

    [Fact]
    public async Task Qubes_cycle_operator_preview_simulation_plan_candidate_risk_rebalance_and_lot_sizing_lineage_is_preserved()
    {
        var result = await PreviewAsync(await CreateContextAsync());

        Assert.True(result.Preview.QubesLineagePreserved);
        Assert.True(result.Preview.CycleLineagePreserved);
        Assert.True(result.Preview.OperatorDecisionLineagePreserved);
        Assert.True(result.Preview.PositionPreviewLineagePreserved);
        Assert.True(result.Preview.SimulationResultLineagePreserved);
        Assert.True(result.Preview.SimulationPlanLineagePreserved);
        Assert.True(result.Preview.PaperExecutionPlanLineagePreserved);
        Assert.True(result.Preview.PaperCandidateLineagePreserved);
        Assert.True(result.Preview.RiskLineagePreserved);
        Assert.True(result.Preview.RebalanceIntentLineagePreserved);
        Assert.True(result.Preview.LotSizingLineagePreserved);
    }

    [Fact]
    public void Preview_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperPositionLedgerPreview.cs"));

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
    public void Preview_source_introduces_no_scheduler_timer_polling_or_background_job()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperPositionLedgerPreview.cs"));

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
        var result = await PreviewAsync(await CreateContextAsync());
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.Contains(result.Preview.Lines, x => x.CurrencyOrSymbol == "AUDUSD");
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

    private static async Task<PaperPositionLedgerPreviewResult> PreviewAsync(TestContext context)
        => await new PaperPositionLedgerPreviewService(
                new InMemoryPaperPositionLedgerPreviewRepository(),
                new FixedClock(ProducedAt))
            .CreateAsync(context.PositionArchive.ArchiveRecord, null, CancellationToken.None);

    private static async Task<TestContext> CreateContextAsync()
    {
        var services = CreateServices();
        var cycle = await services.Cycle.RunOneCycleAsync(new QubesIntradayCycleFixtureRequest(
            "cycle-r022-sample",
            new QubesRunId("qubes-r022-sample"),
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
                "No-external paper ledger preview.",
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
        var candidateBatch = new PaperOrderCandidateShapeService().Create(new PaperOrderCandidateShapeRequest(cycle, review, paperReview));
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
        var simulation = await new PaperSimulationFixtureExecutor(
                new InMemoryPaperSimulationFixtureResultRepository(),
                new FixedClock(ProducedAt))
            .ExecuteAsync(dryRun.Result, CancellationToken.None);
        var simulationArchive = await new PaperSimulationResultArchiveService(
                new InMemoryPaperSimulationResultArchiveRepository(),
                new FixedClock(ProducedAt))
            .ArchiveAsync(simulation.Result, CancellationToken.None);
        var preview = await new PaperSimulationResultOperatorReviewService(
                new InMemoryPaperSimulationResultOperatorDecisionRepository(),
                new InMemoryPaperPositionPreviewRepository(),
                new FixedClock(ProducedAt))
            .ReviewAsync(
                simulationArchive.ArchiveRecord,
                new PaperSimulationResultOperatorReviewRequest(
                    OperatorDecisionId.New(),
                    PaperSimulationResultDecisionType.PromoteToPaperPositionPreview,
                    "operator-placeholder",
                    PaperSimulationResultApprovalReasonCategory.AcceptedForPaperPositionPreview,
                    "No-external paper ledger preview.",
                    BlockedLinesAcknowledged: true,
                    MissingStaleMarksAcknowledged: true,
                    DriftAcknowledged: true),
                CancellationToken.None);
        var positionArchive = await new PaperPositionPreviewArchiveService(
                new InMemoryPaperPositionPreviewArchiveRepository(),
                new FixedClock(ProducedAt))
            .ArchiveAsync(preview.PaperPositionPreview!, CancellationToken.None);

        return new TestContext(positionArchive);
    }

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var normalized = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId("qubes-r022-reference"),
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
        var operatorReview = new IntradayCycleOperatorReviewService(new InMemoryIntradayCycleOperatorDecisionRepository(), clock);

        return new TestServices(state, idsBySymbol, cycle, archive, operatorReview);
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

    private sealed record TestContext(PaperPositionPreviewArchiveResult PositionArchive);

    private sealed record TestServices(
        PlatformState State,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
        QubesIntradayCycleFixtureService Cycle,
        IntradayCycleArchiveService Archive,
        IntradayCycleOperatorReviewService OperatorReview);
}
