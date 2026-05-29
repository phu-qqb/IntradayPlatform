using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesPaperSimulationResultArchiveTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task R018_paper_simulation_result_can_be_archived_no_externally()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.True(result.Persisted);
        Assert.Equal(PaperSimulationResultArchiveStatus.ArchivedWithBlockedLines, result.ArchiveRecord.ArchiveStatus);
    }

    [Fact]
    public async Task Archive_preserves_paper_simulation_result_id()
    {
        var context = await CreateContextAsync();
        var result = await ArchiveAsync(context);

        Assert.Equal(context.Simulation.Result.PaperSimulationResultId, result.ArchiveRecord.PaperSimulationResultId);
    }

    [Fact]
    public async Task Archive_preserves_paper_simulation_plan_id()
    {
        var context = await CreateContextAsync();
        var result = await ArchiveAsync(context);

        Assert.Equal(context.Simulation.Result.PaperSimulationPlanId, result.ArchiveRecord.PaperSimulationPlanId);
    }

    [Fact]
    public async Task Archive_preserves_paper_execution_plan_id()
    {
        var context = await CreateContextAsync();
        var result = await ArchiveAsync(context);

        Assert.Equal(context.Simulation.Result.PaperExecutionPlanId, result.ArchiveRecord.PaperExecutionPlanId);
    }

    [Fact]
    public async Task Archive_preserves_cycle_run_id_and_qubes_run_id()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.Equal("cycle-r019-sample", result.ArchiveRecord.CycleRunId);
        Assert.Equal("qubes-r019-sample", result.ArchiveRecord.QubesRunId);
    }

    [Fact]
    public async Task Archive_preserves_operator_decision_id()
    {
        var context = await CreateContextAsync();
        var result = await ArchiveAsync(context);

        Assert.Equal(context.Simulation.Result.OperatorDecisionId, result.ArchiveRecord.OperatorDecisionId);
    }

    [Fact]
    public async Task Result_lines_preserve_instruments_and_quantities()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.Contains(result.ArchiveRecord.Lines, x => x.NormalizedSymbol == "AUDUSD" && x.Side == IntentSide.Buy && x.SimulatedAppliedQuantity == 131000m && x.QuantityCurrency == "AUD");
        Assert.Contains(result.ArchiveRecord.Lines, x => x.NormalizedSymbol == "EURUSD" && x.Side == IntentSide.Buy && x.SimulatedAppliedQuantity == 124000m && x.QuantityCurrency == "EUR");
        Assert.Contains(result.ArchiveRecord.Lines, x => x.NormalizedSymbol == "GBPUSD" && x.Side == IntentSide.Sell && x.SimulatedAppliedQuantity == 368000m && x.QuantityCurrency == "GBP");
    }

    [Fact]
    public async Task Summary_preserves_zero_real_domain_counts()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.Equal(3, result.ArchiveRecord.SimulatedAppliedLines);
        Assert.Equal(10, result.ArchiveRecord.BlockedLines);
        Assert.Equal(0, result.ArchiveRecord.RealFillCount);
        Assert.Equal(0, result.ArchiveRecord.ExecutionReportCount);
        Assert.Equal(0, result.ArchiveRecord.OrderCount);
        Assert.Equal(0, result.ArchiveRecord.BrokerRouteCount);
    }

    [Fact]
    public async Task Operator_report_includes_paper_only_no_real_fill_no_order_disclaimer()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.True(result.OperatorReport.IncludesPaperOnlyDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoRealFillDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoExecutionReportDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoOmsOrderDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoBrokerOrderDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoSubmissionDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoBrokerRouteDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoLiveStateMutationDisclaimer);
        Assert.Contains("No real fills", result.OperatorReport.Markdown);
        Assert.Contains("no OMS orders", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task Operator_report_includes_blocked_lines_summary()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.True(result.OperatorReport.IncludesBlockedLinesSummary);
        Assert.Equal(10, result.OperatorReport.BlockedLines);
    }

    [Fact]
    public async Task Operator_report_includes_simulated_post_trade_preview()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.True(result.OperatorReport.IncludesPostTradePreview);
        Assert.True(result.ArchiveRecord.PostTradePreview.SimulatedOnly);
        Assert.False(result.ArchiveRecord.PostTradePreview.LivePositionStateMutated);
    }

    [Fact]
    public async Task Operator_report_includes_simulated_reconciliation_preview()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.True(result.OperatorReport.IncludesReconciliationPreview);
        Assert.False(result.ArchiveRecord.ReconciliationPreview.LiveReconciliationClaimCreated);
        Assert.False(result.ArchiveRecord.ReconciliationPreview.LivePositionStateMutated);
    }

    [Fact]
    public async Task Duplicate_archive_handling_is_idempotent()
    {
        var context = await CreateContextAsync();
        var repository = new InMemoryPaperSimulationResultArchiveRepository();
        var service = new PaperSimulationResultArchiveService(repository, new FixedClock(ProducedAt));

        var first = await service.ArchiveAsync(context.Simulation.Result, CancellationToken.None);
        var second = await service.ArchiveAsync(context.Simulation.Result, CancellationToken.None);

        Assert.True(first.Persisted);
        Assert.False(first.AlreadyArchived);
        Assert.False(second.Persisted);
        Assert.True(second.AlreadyArchived);
        Assert.Equal(PaperSimulationResultArchiveStatus.DuplicateReturned, second.ArchiveRecord.ArchiveStatus);
    }

    [Fact]
    public async Task No_real_fills_are_created()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.False(result.ArchiveRecord.RealFillEntityCreated);
        Assert.True(result.ArchiveRecord.NoRealFillCreated);
    }

    [Fact]
    public async Task No_execution_reports_are_created()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.False(result.ArchiveRecord.BrokerExecutionReportEntityCreated);
        Assert.True(result.ArchiveRecord.NoExecutionReportCreated);
    }

    [Fact]
    public async Task No_oms_parent_child_or_broker_order_is_created()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.False(result.ArchiveRecord.OmsOrderCreated);
        Assert.False(result.ArchiveRecord.ParentOrderCreated);
        Assert.False(result.ArchiveRecord.ChildOrderCreated);
        Assert.False(result.ArchiveRecord.BrokerOrderCreated);
        Assert.False(result.ArchiveRecord.OrderStateCreated);
    }

    [Fact]
    public async Task No_order_submission_path_is_introduced()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.False(result.ArchiveRecord.SubmittedOrders);
        Assert.True(result.ArchiveRecord.NotSubmitted);
    }

    [Fact]
    public async Task No_live_state_mutation_occurs()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.True(result.ArchiveRecord.NoLiveStateMutation);
        Assert.False(result.ArchiveRecord.MutatedLiveTradingState);
        Assert.False(result.ArchiveRecord.MutatedLivePositionState);
        Assert.False(result.ArchiveRecord.MutatedBrokerState);
    }

    [Fact]
    public void Archive_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperSimulationResultArchive.cs"));

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
    public void Archive_source_introduces_no_scheduler_timer_polling_or_background_job()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperSimulationResultArchive.cs"));

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
        var result = await ArchiveAsync(await CreateContextAsync());
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.Contains(result.ArchiveRecord.Lines, x => x.NormalizedSymbol == "AUDUSD");
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

    private static async Task<PaperSimulationResultArchiveResult> ArchiveAsync(TestContext context)
        => await new PaperSimulationResultArchiveService(
                new InMemoryPaperSimulationResultArchiveRepository(),
                new FixedClock(ProducedAt))
            .ArchiveAsync(context.Simulation.Result, CancellationToken.None);

    private static async Task<TestContext> CreateContextAsync()
    {
        var services = CreateServices();
        var cycle = await services.Cycle.RunOneCycleAsync(new QubesIntradayCycleFixtureRequest(
            "cycle-r019-sample",
            new QubesRunId("qubes-r019-sample"),
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
                "No-external paper simulation archive.",
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
        var simulation = await new PaperSimulationFixtureExecutor(
                new InMemoryPaperSimulationFixtureResultRepository(),
                new FixedClock(ProducedAt))
            .ExecuteAsync(dryRun.Result, CancellationToken.None);

        return new TestContext(simulation);
    }

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var normalized = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId("qubes-r019-reference"),
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

    private sealed record TestContext(PaperSimulationFixtureExecutionResult Simulation);

    private sealed record TestServices(
        PlatformState State,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
        QubesIntradayCycleFixtureService Cycle,
        IntradayCycleArchiveService Archive,
        IntradayCycleOperatorReviewService OperatorReview);
}
