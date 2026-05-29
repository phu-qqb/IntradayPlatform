using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesPaperLedgerPreviewArchiveTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task R022_paper_position_ledger_preview_can_be_archived_no_externally()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.True(result.Persisted);
        Assert.Equal(PaperLedgerPreviewArchiveStatus.ArchivedNoExternal, result.ArchiveRecord.ArchiveStatus);
    }

    [Fact]
    public async Task Archive_preserves_paper_position_ledger_preview_id()
    {
        var context = await CreateContextAsync();
        var result = await ArchiveAsync(context);

        Assert.Equal(context.LedgerPreview.Preview.PaperPositionLedgerPreviewId, result.ArchiveRecord.PaperPositionLedgerPreviewId);
    }

    [Fact]
    public async Task Archive_preserves_paper_position_preview_id()
    {
        var context = await CreateContextAsync();
        var result = await ArchiveAsync(context);

        Assert.Equal(context.LedgerPreview.Preview.PaperPositionPreviewId, result.ArchiveRecord.PaperPositionPreviewId);
    }

    [Fact]
    public async Task Archive_preserves_paper_simulation_result_id()
    {
        var context = await CreateContextAsync();
        var result = await ArchiveAsync(context);

        Assert.Equal(context.LedgerPreview.Preview.PaperSimulationResultId, result.ArchiveRecord.PaperSimulationResultId);
    }

    [Fact]
    public async Task Archive_preserves_paper_simulation_plan_id()
    {
        var context = await CreateContextAsync();
        var result = await ArchiveAsync(context);

        Assert.Equal(context.LedgerPreview.Preview.PaperSimulationPlanId, result.ArchiveRecord.PaperSimulationPlanId);
    }

    [Fact]
    public async Task Archive_preserves_paper_execution_plan_id()
    {
        var context = await CreateContextAsync();
        var result = await ArchiveAsync(context);

        Assert.Equal(context.LedgerPreview.Preview.PaperExecutionPlanId, result.ArchiveRecord.PaperExecutionPlanId);
    }

    [Fact]
    public async Task Archive_preserves_cycle_run_id_and_qubes_run_id()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.Equal("cycle-r023-sample", result.ArchiveRecord.CycleRunId);
        Assert.Equal("qubes-r023-sample", result.ArchiveRecord.QubesRunId);
    }

    [Fact]
    public async Task Archive_preserves_operator_decision_id()
    {
        var context = await CreateContextAsync();
        var result = await ArchiveAsync(context);

        Assert.Equal(context.LedgerPreview.Preview.OperatorDecisionId, result.ArchiveRecord.OperatorDecisionId);
    }

    [Fact]
    public async Task Preview_lines_include_audusd_delta()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.Contains(result.ArchiveRecord.Lines, x => x.CurrencyOrSymbol == "AUDUSD" && x.SimulatedDeltaQuantity == 131000m && x.PreviewEndingPaperQuantity == 131000m && x.QuantityCurrency == "AUD");
    }

    [Fact]
    public async Task Preview_lines_include_eurusd_delta()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.Contains(result.ArchiveRecord.Lines, x => x.CurrencyOrSymbol == "EURUSD" && x.SimulatedDeltaQuantity == 124000m && x.PreviewEndingPaperQuantity == 124000m && x.QuantityCurrency == "EUR");
    }

    [Fact]
    public async Task Preview_lines_include_gbpusd_delta()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.Contains(result.ArchiveRecord.Lines, x => x.CurrencyOrSymbol == "GBPUSD" && x.SimulatedDeltaQuantity == -368000m && x.PreviewEndingPaperQuantity == -368000m && x.QuantityCurrency == "GBP");
    }

    [Fact]
    public async Task Operator_ledger_report_includes_no_paper_ledger_commit_disclaimer()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.True(result.OperatorReport.IncludesNoPaperLedgerCommitDisclaimer);
        Assert.Contains("No paper ledger commit yet", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task Operator_ledger_report_includes_no_live_position_mutation_disclaimer()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.True(result.OperatorReport.IncludesNoLivePositionMutationDisclaimer);
        Assert.Contains("no live position mutation", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task Operator_ledger_report_includes_no_broker_position_mutation_disclaimer()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.True(result.OperatorReport.IncludesNoBrokerPositionMutationDisclaimer);
        Assert.Contains("no broker position mutation", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task Operator_ledger_report_includes_no_production_ledger_mutation_disclaimer()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.True(result.OperatorReport.IncludesNoProductionLedgerMutationDisclaimer);
        Assert.Contains("no production ledger mutation", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task Operator_ledger_report_includes_no_trading_state_mutation_disclaimer()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.True(result.OperatorReport.IncludesNoTradingStateMutationDisclaimer);
        Assert.Contains("no trading-state mutation", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task Operator_ledger_report_includes_no_order_fill_report_route_or_submission_disclaimer()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.True(result.OperatorReport.IncludesNoOrderDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoFillDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoExecutionReportDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoBrokerRouteDisclaimer);
        Assert.True(result.OperatorReport.IncludesNoSubmissionDisclaimer);
        Assert.Contains("no orders", result.OperatorReport.Markdown);
        Assert.Contains("no fills", result.OperatorReport.Markdown);
        Assert.Contains("no execution reports", result.OperatorReport.Markdown);
        Assert.Contains("no broker routes", result.OperatorReport.Markdown);
        Assert.Contains("no submissions", result.OperatorReport.Markdown);
    }

    [Fact]
    public async Task Hold_decision_is_recorded_safely()
    {
        var archive = await ArchiveAsync(await CreateContextAsync());
        var decision = await ReviewAsync(archive.ArchiveRecord, Request(PaperLedgerCommitDecisionType.Hold, PaperLedgerCommitReadinessReasonCategory.HeldDueToBlockedLines));

        Assert.True(decision.Persisted);
        Assert.Equal(PaperLedgerCommitDecisionStatus.Recorded, decision.Decision.DecisionStatus);
        Assert.Equal(PaperLedgerCommitReadinessStatus.HeldForBlockedLines, decision.Decision.ResultingReadinessStatus);
    }

    [Fact]
    public async Task Approve_paper_ledger_commit_readiness_is_recorded_safely()
    {
        var archive = await ArchiveAsync(await CreateContextAsync());
        var decision = await ReviewAsync(archive.ArchiveRecord, Request(PaperLedgerCommitDecisionType.ApprovePaperLedgerCommitReadiness));

        Assert.True(decision.Persisted);
        Assert.Equal(PaperLedgerCommitReadinessStatus.PaperLedgerCommitReadyNoExternal, decision.Decision.ResultingReadinessStatus);
    }

    [Fact]
    public async Task Approve_paper_ledger_commit_readiness_does_not_commit_paper_ledger_state()
    {
        var archive = await ArchiveAsync(await CreateContextAsync());
        var decision = await ReviewAsync(archive.ArchiveRecord, Request(PaperLedgerCommitDecisionType.ApprovePaperLedgerCommitReadiness));

        Assert.True(decision.Decision.NoPaperLedgerCommit);
        Assert.False(decision.ArchiveRecord.PaperLedgerStateCommitted);
    }

    [Fact]
    public async Task Approve_paper_ledger_commit_readiness_does_not_mutate_live_positions()
    {
        var decision = await ApproveAsync();

        Assert.True(decision.Decision.NoLivePositionMutation);
        Assert.False(decision.ArchiveRecord.LivePositionStateMutated);
    }

    [Fact]
    public async Task Approve_paper_ledger_commit_readiness_does_not_mutate_broker_positions()
    {
        var decision = await ApproveAsync();

        Assert.True(decision.Decision.NoBrokerPositionMutation);
        Assert.False(decision.ArchiveRecord.BrokerPositionStateMutated);
    }

    [Fact]
    public async Task Approve_paper_ledger_commit_readiness_does_not_mutate_production_ledger_state()
    {
        var decision = await ApproveAsync();

        Assert.True(decision.Decision.NoProductionLedgerMutation);
        Assert.False(decision.ArchiveRecord.ProductionLedgerStateMutated);
    }

    [Fact]
    public async Task Approve_paper_ledger_commit_readiness_does_not_mutate_trading_state()
    {
        var decision = await ApproveAsync();

        Assert.True(decision.Decision.NoTradingStateMutation);
        Assert.False(decision.ArchiveRecord.TradingStateMutated);
    }

    [Fact]
    public async Task Duplicate_preview_archive_and_duplicate_decision_handling_are_idempotent()
    {
        var context = await CreateContextAsync();
        var archiveRepository = new InMemoryPaperLedgerPreviewArchiveRepository();
        var archiveService = new PaperLedgerPreviewArchiveService(archiveRepository, new FixedClock(ProducedAt));
        var firstArchive = await archiveService.ArchiveAsync(context.LedgerPreview.Preview, CancellationToken.None);
        var secondArchive = await archiveService.ArchiveAsync(context.LedgerPreview.Preview, CancellationToken.None);
        var decisionRepository = new InMemoryPaperLedgerCommitReadinessDecisionRepository();
        var decisionService = new PaperLedgerCommitReadinessService(decisionRepository, new FixedClock(ProducedAt));
        var request = Request(PaperLedgerCommitDecisionType.ApprovePaperLedgerCommitReadiness);
        var firstDecision = await decisionService.ReviewAsync(firstArchive.ArchiveRecord, request, CancellationToken.None);
        var secondDecision = await decisionService.ReviewAsync(firstArchive.ArchiveRecord, request, CancellationToken.None);

        Assert.True(secondArchive.AlreadyArchived);
        Assert.Equal(PaperLedgerPreviewArchiveStatus.DuplicateReturned, secondArchive.ArchiveRecord.ArchiveStatus);
        Assert.True(secondDecision.AlreadyRecorded);
        Assert.Equal(PaperLedgerCommitDecisionStatus.DuplicateReturned, secondDecision.Decision.DecisionStatus);
        Assert.True(firstDecision.Persisted);
    }

    [Fact]
    public async Task Qubes_cycle_operator_preview_simulation_plan_candidate_risk_rebalance_and_lot_sizing_lineage_is_preserved()
    {
        var decision = await ApproveAsync();

        Assert.True(decision.Decision.QubesLineagePreserved);
        Assert.True(decision.Decision.CycleLineagePreserved);
        Assert.True(decision.Decision.OperatorDecisionLineagePreserved);
        Assert.True(decision.Decision.PositionPreviewLineagePreserved);
        Assert.True(decision.Decision.LedgerPreviewLineagePreserved);
        Assert.True(decision.Decision.SimulationResultLineagePreserved);
        Assert.True(decision.Decision.SimulationPlanLineagePreserved);
        Assert.True(decision.Decision.PaperExecutionPlanLineagePreserved);
        Assert.True(decision.Decision.PaperCandidateLineagePreserved);
        Assert.True(decision.Decision.RiskLineagePreserved);
        Assert.True(decision.Decision.RebalanceIntentLineagePreserved);
        Assert.True(decision.Decision.LotSizingLineagePreserved);
    }

    [Fact]
    public void Archive_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperLedgerPreviewArchive.cs"));

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
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperLedgerPreviewArchive.cs"));

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
        var decision = await ApproveAsync();

        Assert.False(decision.Decision.CreatesFill);
        Assert.False(decision.ArchiveRecord.FillCreated);
    }

    [Fact]
    public async Task No_execution_reports_are_created()
    {
        var decision = await ApproveAsync();

        Assert.False(decision.Decision.CreatesExecutionReport);
        Assert.False(decision.ArchiveRecord.ExecutionReportCreated);
    }

    [Fact]
    public async Task No_oms_or_broker_orders_are_created()
    {
        var decision = await ApproveAsync();

        Assert.False(decision.Decision.CreatesOmsOrder);
        Assert.False(decision.Decision.CreatesBrokerOrder);
        Assert.False(decision.ArchiveRecord.OmsOrderCreated);
        Assert.False(decision.ArchiveRecord.BrokerOrderCreated);
    }

    [Fact]
    public async Task Audusd_is_not_misclassified_as_failed()
    {
        var archive = await ArchiveAsync(await CreateContextAsync());
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.Contains(archive.ArchiveRecord.Lines, x => x.CurrencyOrSymbol == "AUDUSD");
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

    private static async Task<PaperLedgerPreviewArchiveResult> ArchiveAsync(TestContext context)
        => await new PaperLedgerPreviewArchiveService(
                new InMemoryPaperLedgerPreviewArchiveRepository(),
                new FixedClock(ProducedAt))
            .ArchiveAsync(context.LedgerPreview.Preview, CancellationToken.None);

    private static async Task<PaperLedgerCommitReadinessResult> ApproveAsync()
    {
        var archive = await ArchiveAsync(await CreateContextAsync());
        return await ReviewAsync(archive.ArchiveRecord, Request(PaperLedgerCommitDecisionType.ApprovePaperLedgerCommitReadiness));
    }

    private static async Task<PaperLedgerCommitReadinessResult> ReviewAsync(
        PaperLedgerPreviewRecord archive,
        PaperLedgerCommitReadinessRequest request)
        => await new PaperLedgerCommitReadinessService(
                new InMemoryPaperLedgerCommitReadinessDecisionRepository(),
                new FixedClock(ProducedAt))
            .ReviewAsync(archive, request, CancellationToken.None);

    private static PaperLedgerCommitReadinessRequest Request(
        PaperLedgerCommitDecisionType decisionType,
        PaperLedgerCommitReadinessReasonCategory reason = PaperLedgerCommitReadinessReasonCategory.AcceptedForPaperLedgerCommitReadiness)
        => new(
            OperatorDecisionId.New(),
            decisionType,
            "operator-placeholder",
            reason,
            "No-external paper ledger commit readiness only.",
            BlockedLinesAcknowledged: true,
            MissingStaleMarksAcknowledged: true,
            DriftAcknowledged: true);

    private static async Task<TestContext> CreateContextAsync()
    {
        var services = CreateServices();
        var cycle = await services.Cycle.RunOneCycleAsync(new QubesIntradayCycleFixtureRequest(
            "cycle-r023-sample",
            new QubesRunId("qubes-r023-sample"),
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
                "No-external paper ledger commit readiness.",
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
                    "No-external paper ledger commit readiness.",
                    BlockedLinesAcknowledged: true,
                    MissingStaleMarksAcknowledged: true,
                    DriftAcknowledged: true),
                CancellationToken.None);
        var positionArchive = await new PaperPositionPreviewArchiveService(
                new InMemoryPaperPositionPreviewArchiveRepository(),
                new FixedClock(ProducedAt))
            .ArchiveAsync(preview.PaperPositionPreview!, CancellationToken.None);
        var ledgerPreview = await new PaperPositionLedgerPreviewService(
                new InMemoryPaperPositionLedgerPreviewRepository(),
                new FixedClock(ProducedAt))
            .CreateAsync(positionArchive.ArchiveRecord, null, CancellationToken.None);

        return new TestContext(ledgerPreview);
    }

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var normalized = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId("qubes-r023-reference"),
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

    private sealed record TestContext(PaperPositionLedgerPreviewResult LedgerPreview);

    private sealed record TestServices(
        PlatformState State,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
        QubesIntradayCycleFixtureService Cycle,
        IntradayCycleArchiveService Archive,
        IntradayCycleOperatorReviewService OperatorReview);
}
