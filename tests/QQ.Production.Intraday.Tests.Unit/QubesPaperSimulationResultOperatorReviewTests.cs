using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesPaperSimulationResultOperatorReviewTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task R019_archived_paper_simulation_result_can_enter_operator_review()
    {
        var result = await ReviewAsync(await CreateContextAsync(), Request(PaperSimulationResultDecisionType.ApprovePaperSimulationResult));

        Assert.True(result.Persisted);
        Assert.Equal(PaperSimulationResultDecisionStatus.Recorded, result.Decision.DecisionStatus);
    }

    [Fact]
    public async Task Approve_paper_simulation_result_records_decision_without_external_action()
    {
        var result = await ReviewAsync(await CreateContextAsync(), Request(PaperSimulationResultDecisionType.ApprovePaperSimulationResult));

        Assert.Equal(PaperSimulationResultDecisionType.ApprovePaperSimulationResult, result.Decision.DecisionType);
        Assert.False(result.Decision.CallsBrokerGateway);
        Assert.False(result.Decision.RequestsLiveMarketData);
    }

    [Fact]
    public async Task Hold_records_decision_without_external_action()
    {
        var result = await ReviewAsync(await CreateContextAsync(), Request(PaperSimulationResultDecisionType.Hold, PaperSimulationResultApprovalReasonCategory.HeldDueToBlockedLines));

        Assert.Equal(PaperSimulationResultDecisionStatus.Recorded, result.Decision.DecisionStatus);
        Assert.Equal(PaperPositionPreviewStatus.HeldForBlockedLines, result.Decision.ResultingPaperPositionPreviewStatus);
        Assert.Null(result.PaperPositionPreview);
    }

    [Fact]
    public async Task Reject_records_decision_without_external_action()
    {
        var result = await ReviewAsync(await CreateContextAsync(), Request(PaperSimulationResultDecisionType.Reject, PaperSimulationResultApprovalReasonCategory.RejectedDueToValidationFailure));

        Assert.Equal(PaperSimulationResultDecisionStatus.Recorded, result.Decision.DecisionStatus);
        Assert.Equal(PaperPositionPreviewStatus.Rejected, result.Decision.ResultingPaperPositionPreviewStatus);
    }

    [Fact]
    public async Task Request_simulation_fix_records_decision_safely()
    {
        var result = await ReviewAsync(await CreateContextAsync(), Request(PaperSimulationResultDecisionType.RequestSimulationFix, PaperSimulationResultApprovalReasonCategory.InconclusiveSafe));

        Assert.Equal(PaperSimulationResultDecisionStatus.Recorded, result.Decision.DecisionStatus);
        Assert.False(result.Decision.MutatesLivePositionState);
    }

    [Fact]
    public async Task Promote_to_paper_position_preview_creates_paper_preview_only()
    {
        var result = await ReviewAsync(await CreateContextAsync(), Request(PaperSimulationResultDecisionType.PromoteToPaperPositionPreview));

        Assert.NotNull(result.PaperPositionPreview);
        Assert.True(result.PaperPositionPreview.PaperOnly);
        Assert.True(result.PaperPositionPreview.SimulatedOnly);
    }

    [Fact]
    public async Task Paper_preview_includes_audusd_simulated_delta()
    {
        var result = await PromoteAsync();

        Assert.Contains(result.PaperPositionPreview!.Lines, x => x.NormalizedSymbol == "AUDUSD" && x.SimulatedPositionDelta == 131000m && x.QuantityCurrency == "AUD");
    }

    [Fact]
    public async Task Paper_preview_includes_eurusd_simulated_delta()
    {
        var result = await PromoteAsync();

        Assert.Contains(result.PaperPositionPreview!.Lines, x => x.NormalizedSymbol == "EURUSD" && x.SimulatedPositionDelta == 124000m && x.QuantityCurrency == "EUR");
    }

    [Fact]
    public async Task Paper_preview_includes_gbpusd_sell_delta()
    {
        var result = await PromoteAsync();

        Assert.Contains(result.PaperPositionPreview!.Lines, x => x.NormalizedSymbol == "GBPUSD" && x.SimulatedPositionDelta == -368000m && x.QuantityCurrency == "GBP");
    }

    [Fact]
    public async Task Paper_preview_does_not_mutate_live_positions()
    {
        var result = await PromoteAsync();

        Assert.False(result.PaperPositionPreview!.LivePositionStateMutated);
        Assert.True(result.PaperPositionPreview.NoLivePositionMutation);
        Assert.All(result.PaperPositionPreview.Lines, x => Assert.True(x.NoLivePositionMutation));
    }

    [Fact]
    public async Task Paper_preview_does_not_mutate_broker_positions()
    {
        var result = await PromoteAsync();

        Assert.False(result.PaperPositionPreview!.BrokerPositionStateMutated);
        Assert.True(result.PaperPositionPreview.NoBrokerPositionMutation);
    }

    [Fact]
    public async Task Paper_preview_does_not_mutate_trading_state()
    {
        var result = await PromoteAsync();

        Assert.False(result.PaperPositionPreview!.TradingStateMutated);
        Assert.True(result.PaperPositionPreview.NoTradingStateMutation);
    }

    [Fact]
    public async Task Real_fill_count_zero_is_required()
    {
        var context = await CreateContextAsync();
        var bad = context.Archive.ArchiveRecord with { RealFillCount = 1 };
        var result = await ReviewArchiveAsync(bad, Request(PaperSimulationResultDecisionType.PromoteToPaperPositionPreview));

        Assert.False(result.Persisted);
        Assert.Equal(PaperSimulationResultDecisionStatus.RejectedByGate, result.Decision.DecisionStatus);
    }

    [Fact]
    public async Task Execution_report_count_zero_is_required()
    {
        var context = await CreateContextAsync();
        var bad = context.Archive.ArchiveRecord with { ExecutionReportCount = 1 };
        var result = await ReviewArchiveAsync(bad, Request(PaperSimulationResultDecisionType.PromoteToPaperPositionPreview));

        Assert.False(result.Persisted);
    }

    [Fact]
    public async Task Order_count_zero_is_required()
    {
        var context = await CreateContextAsync();
        var bad = context.Archive.ArchiveRecord with { OrderCount = 1 };
        var result = await ReviewArchiveAsync(bad, Request(PaperSimulationResultDecisionType.PromoteToPaperPositionPreview));

        Assert.False(result.Persisted);
    }

    [Fact]
    public async Task Broker_route_count_zero_is_required()
    {
        var context = await CreateContextAsync();
        var bad = context.Archive.ArchiveRecord with { BrokerRouteCount = 1 };
        var result = await ReviewArchiveAsync(bad, Request(PaperSimulationResultDecisionType.PromoteToPaperPositionPreview));

        Assert.False(result.Persisted);
    }

    [Fact]
    public async Task Blocked_lines_require_acknowledgement()
    {
        var result = await ReviewAsync(await CreateContextAsync(), Request(PaperSimulationResultDecisionType.PromoteToPaperPositionPreview, blockedAck: false));

        Assert.False(result.Persisted);
        Assert.Equal(PaperPositionPreviewStatus.HeldForBlockedLines, result.Decision.ResultingPaperPositionPreviewStatus);
    }

    [Fact]
    public async Task Missing_stale_marks_require_acknowledgement()
    {
        var result = await ReviewAsync(await CreateContextAsync(), Request(PaperSimulationResultDecisionType.PromoteToPaperPositionPreview, missingAck: false));

        Assert.False(result.Persisted);
        Assert.Equal(PaperPositionPreviewStatus.HeldForMissingMarks, result.Decision.ResultingPaperPositionPreviewStatus);
    }

    [Fact]
    public async Task Drift_requires_acknowledgement()
    {
        var result = await ReviewAsync(await CreateContextAsync(), Request(PaperSimulationResultDecisionType.PromoteToPaperPositionPreview, driftAck: false));

        Assert.False(result.Persisted);
        Assert.Equal(PaperPositionPreviewStatus.HeldForRiskReview, result.Decision.ResultingPaperPositionPreviewStatus);
    }

    [Fact]
    public async Task Duplicate_decision_or_preview_handling_is_idempotent()
    {
        var context = await CreateContextAsync();
        var decisionRepository = new InMemoryPaperSimulationResultOperatorDecisionRepository();
        var previewRepository = new InMemoryPaperPositionPreviewRepository();
        var service = new PaperSimulationResultOperatorReviewService(decisionRepository, previewRepository, new FixedClock(ProducedAt));
        var request = Request(PaperSimulationResultDecisionType.PromoteToPaperPositionPreview);

        var first = await service.ReviewAsync(context.Archive.ArchiveRecord, request, CancellationToken.None);
        var second = await service.ReviewAsync(context.Archive.ArchiveRecord, request, CancellationToken.None);

        Assert.True(first.Persisted);
        Assert.False(first.AlreadyRecorded);
        Assert.False(second.Persisted);
        Assert.True(second.AlreadyRecorded);
        Assert.Equal(PaperSimulationResultDecisionStatus.DuplicateReturned, second.Decision.DecisionStatus);
        Assert.NotNull(second.PaperPositionPreview);
    }

    [Fact]
    public async Task Qubes_run_id_and_cycle_run_id_are_preserved()
    {
        var result = await PromoteAsync();

        Assert.Equal("cycle-r020-sample", result.Decision.CycleRunId);
        Assert.Equal("qubes-r020-sample", result.Decision.QubesRunId);
        Assert.Equal("cycle-r020-sample", result.PaperPositionPreview!.CycleRunId);
        Assert.Equal("qubes-r020-sample", result.PaperPositionPreview.QubesRunId);
    }

    [Fact]
    public async Task Simulation_result_lineage_is_preserved()
    {
        var result = await PromoteAsync();

        Assert.True(result.Decision.SimulationResultLineagePreserved);
        Assert.True(result.PaperPositionPreview!.SimulationResultLineagePreserved);
    }

    [Fact]
    public async Task Plan_candidate_risk_rebalance_and_lot_sizing_lineage_is_preserved()
    {
        var result = await PromoteAsync();

        Assert.True(result.PaperPositionPreview!.SimulationPlanLineagePreserved);
        Assert.True(result.PaperPositionPreview.PaperExecutionPlanLineagePreserved);
        Assert.True(result.PaperPositionPreview.PaperCandidateLineagePreserved);
        Assert.True(result.PaperPositionPreview.RiskLineagePreserved);
        Assert.True(result.PaperPositionPreview.RebalanceIntentLineagePreserved);
        Assert.True(result.PaperPositionPreview.LotSizingLineagePreserved);
    }

    [Fact]
    public void Review_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperSimulationResultOperatorReview.cs"));

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
    public void Review_source_introduces_no_scheduler_timer_polling_or_background_job()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperSimulationResultOperatorReview.cs"));

        Assert.DoesNotContain("AddHostedService", source);
        Assert.DoesNotContain("IHostedService", source);
        Assert.DoesNotContain("BackgroundService", source);
        Assert.DoesNotContain("PeriodicTimer", source);
        Assert.DoesNotContain("Task.Delay", source);
        Assert.DoesNotContain("System.Threading.Timer", source);
    }

    [Fact]
    public async Task No_fills_are_created()
    {
        var result = await PromoteAsync();

        Assert.False(result.PaperPositionPreview!.FillCreated);
        Assert.All(result.PaperPositionPreview.Lines, x => Assert.True(x.NoFillCreated));
    }

    [Fact]
    public async Task No_execution_reports_are_created()
    {
        var result = await PromoteAsync();

        Assert.False(result.PaperPositionPreview!.ExecutionReportCreated);
        Assert.All(result.PaperPositionPreview.Lines, x => Assert.True(x.NoExecutionReportCreated));
    }

    [Fact]
    public async Task No_oms_or_broker_orders_are_created()
    {
        var result = await PromoteAsync();

        Assert.False(result.PaperPositionPreview!.OmsOrderCreated);
        Assert.False(result.PaperPositionPreview.BrokerOrderCreated);
        Assert.False(result.PaperPositionPreview.OrderStateCreated);
    }

    [Fact]
    public async Task Audusd_is_not_misclassified_as_failed()
    {
        var result = await PromoteAsync();
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.Contains(result.PaperPositionPreview!.Lines, x => x.NormalizedSymbol == "AUDUSD");
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

    private static Task<PaperSimulationResultOperatorReviewResult> PromoteAsync()
        => ReviewAsync(CreateContextAsync().GetAwaiter().GetResult(), Request(PaperSimulationResultDecisionType.PromoteToPaperPositionPreview));

    private static async Task<PaperSimulationResultOperatorReviewResult> ReviewAsync(
        TestContext context,
        PaperSimulationResultOperatorReviewRequest request)
        => await ReviewArchiveAsync(context.Archive.ArchiveRecord, request);

    private static async Task<PaperSimulationResultOperatorReviewResult> ReviewArchiveAsync(
        PaperSimulationResultRecord archive,
        PaperSimulationResultOperatorReviewRequest request)
        => await new PaperSimulationResultOperatorReviewService(
                new InMemoryPaperSimulationResultOperatorDecisionRepository(),
                new InMemoryPaperPositionPreviewRepository(),
                new FixedClock(ProducedAt))
            .ReviewAsync(archive, request, CancellationToken.None);

    private static PaperSimulationResultOperatorReviewRequest Request(
        PaperSimulationResultDecisionType type,
        PaperSimulationResultApprovalReasonCategory reason = PaperSimulationResultApprovalReasonCategory.AcceptedForPaperPositionPreview,
        bool blockedAck = true,
        bool missingAck = true,
        bool driftAck = true)
        => new(
            OperatorDecisionId.New(),
            type,
            "operator-placeholder",
            reason,
            "No-external paper position preview gate.",
            blockedAck,
            missingAck,
            driftAck);

    private static async Task<TestContext> CreateContextAsync()
    {
        var services = CreateServices();
        var cycle = await services.Cycle.RunOneCycleAsync(new QubesIntradayCycleFixtureRequest(
            "cycle-r020-sample",
            new QubesRunId("qubes-r020-sample"),
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
                "No-external paper position preview gate.",
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
        var archive = await new PaperSimulationResultArchiveService(
                new InMemoryPaperSimulationResultArchiveRepository(),
                new FixedClock(ProducedAt))
            .ArchiveAsync(simulation.Result, CancellationToken.None);

        return new TestContext(archive);
    }

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var normalized = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId("qubes-r020-reference"),
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

    private sealed record TestContext(PaperSimulationResultArchiveResult Archive);

    private sealed record TestServices(
        PlatformState State,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
        QubesIntradayCycleFixtureService Cycle,
        IntradayCycleArchiveService Archive,
        IntradayCycleOperatorReviewService OperatorReview);
}
