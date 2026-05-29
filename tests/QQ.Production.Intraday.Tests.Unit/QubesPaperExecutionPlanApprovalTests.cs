using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesPaperExecutionPlanApprovalTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task R014_archived_paper_plan_can_enter_operator_approval_review()
    {
        var result = await ReviewAsync(await CreateContextAsync(), PaperExecutionPlanOperatorAction.Hold, PaperPlanApprovalReasonCategory.HeldDueToBlockedLines);

        Assert.True(result.Persisted);
        Assert.Equal("cycle-r015-sample", result.Decision.CycleRunId);
        Assert.Equal("qubes-r015-sample", result.Decision.QubesRunId);
    }

    [Fact]
    public async Task Approve_for_paper_simulation_records_decision_without_external_action()
    {
        var result = await ReviewAsync(
            await CreateContextAsync(),
            PaperExecutionPlanOperatorAction.ApproveForPaperSimulation,
            PaperPlanApprovalReasonCategory.AcceptedForSimulationReadiness,
            blockedAcknowledged: true,
            missingAcknowledged: true,
            driftAcknowledged: true);

        Assert.Equal(PaperExecutionPlanApprovalStatus.Recorded, result.Decision.DecisionStatus);
        Assert.False(result.Decision.CallsBrokerGateway);
        Assert.False(result.Decision.RequestsLiveMarketData);
        Assert.False(result.Decision.StartsApiOrWorker);
    }

    [Fact]
    public async Task Hold_records_decision_without_external_action()
    {
        var result = await ReviewAsync(await CreateContextAsync(), PaperExecutionPlanOperatorAction.Hold, PaperPlanApprovalReasonCategory.HeldDueToBlockedLines);

        Assert.Equal(PaperExecutionPlanApprovalStatus.Recorded, result.Decision.DecisionStatus);
        Assert.Equal(PaperSimulationReadinessStatus.HeldForMissingMarks, result.Decision.ResultingSimulationReadinessStatus);
        Assert.False(result.Decision.RunsPaperSimulation);
    }

    [Fact]
    public async Task Reject_records_decision_without_external_action()
    {
        var result = await ReviewAsync(await CreateContextAsync(), PaperExecutionPlanOperatorAction.Reject, PaperPlanApprovalReasonCategory.RejectedDueToValidationFailure);

        Assert.Equal(PaperSimulationReadinessStatus.Rejected, result.Decision.ResultingSimulationReadinessStatus);
        Assert.False(result.Decision.CreatesOmsOrder);
        Assert.False(result.Decision.CreatesBrokerOrder);
    }

    [Fact]
    public async Task Request_plan_fix_records_decision_safely()
    {
        var result = await ReviewAsync(await CreateContextAsync(), PaperExecutionPlanOperatorAction.RequestPlanFix, PaperPlanApprovalReasonCategory.InconclusiveSafe);

        Assert.Equal(PaperExecutionPlanApprovalStatus.Recorded, result.Decision.DecisionStatus);
        Assert.Equal(PaperSimulationReadinessStatus.InconclusiveSafe, result.Decision.ResultingSimulationReadinessStatus);
        Assert.Contains("Plan fix requested no-externally", result.Decision.GateMessage);
    }

    [Fact]
    public async Task Plan_with_blocked_lines_requires_acknowledgement_before_simulation_readiness()
    {
        var result = await ReviewAsync(
            await CreateContextAsync(),
            PaperExecutionPlanOperatorAction.ApproveForPaperSimulation,
            PaperPlanApprovalReasonCategory.InconclusiveSafe,
            blockedAcknowledged: false,
            missingAcknowledged: true,
            driftAcknowledged: true);

        Assert.False(result.Persisted);
        Assert.Equal(PaperExecutionPlanApprovalStatus.RejectedByGate, result.Decision.DecisionStatus);
        Assert.Equal(PaperSimulationReadinessStatus.HeldForBlockedLines, result.Decision.ResultingSimulationReadinessStatus);
    }

    [Fact]
    public async Task Plan_with_missing_stale_marks_requires_acknowledgement_before_simulation_readiness()
    {
        var result = await ReviewAsync(
            await CreateContextAsync(),
            PaperExecutionPlanOperatorAction.ApproveForPaperSimulation,
            PaperPlanApprovalReasonCategory.InconclusiveSafe,
            blockedAcknowledged: true,
            missingAcknowledged: false,
            driftAcknowledged: true);

        Assert.False(result.Persisted);
        Assert.Equal(PaperSimulationReadinessStatus.HeldForMissingMarks, result.Decision.ResultingSimulationReadinessStatus);
    }

    [Fact]
    public async Task Plan_with_drift_requires_acknowledgement_before_simulation_readiness()
    {
        var result = await ReviewAsync(
            await CreateContextAsync(),
            PaperExecutionPlanOperatorAction.ApproveForPaperSimulation,
            PaperPlanApprovalReasonCategory.InconclusiveSafe,
            blockedAcknowledged: true,
            missingAcknowledged: true,
            driftAcknowledged: false);

        Assert.False(result.Persisted);
        Assert.Equal(PaperSimulationReadinessStatus.HeldForDrift, result.Decision.ResultingSimulationReadinessStatus);
    }

    [Fact]
    public async Task Approval_produces_simulation_ready_no_external_only()
    {
        var result = await ApprovedAsync();

        Assert.Equal(PaperSimulationReadinessStatus.SimulationReadyNoExternal, result.Decision.ResultingSimulationReadinessStatus);
        Assert.True(result.Decision.SimulationReadinessOnly);
        Assert.True(result.Decision.NoExternal);
    }

    [Fact]
    public async Task Simulation_ready_no_external_does_not_create_fills()
    {
        var result = await ApprovedAsync();

        Assert.False(result.Decision.CreatesFill);
        Assert.False(result.Decision.CreatesSimulationFill);
    }

    [Fact]
    public async Task Simulation_ready_no_external_does_not_create_execution_reports()
    {
        var result = await ApprovedAsync();

        Assert.False(result.Decision.CreatesExecutionReport);
        Assert.False(result.Decision.CreatesSimulationExecutionReport);
    }

    [Fact]
    public async Task Simulation_ready_no_external_does_not_create_oms_orders()
    {
        var result = await ApprovedAsync();

        Assert.False(result.Decision.CreatesOmsOrder);
        Assert.False(result.Decision.CreatesParentOrder);
        Assert.False(result.Decision.CreatesChildOrder);
    }

    [Fact]
    public async Task Simulation_ready_no_external_does_not_create_broker_orders()
    {
        var result = await ApprovedAsync();

        Assert.False(result.Decision.CreatesBrokerOrder);
        Assert.False(result.Decision.CallsBrokerGateway);
    }

    [Fact]
    public async Task Simulation_ready_no_external_does_not_submit_anything()
    {
        var result = await ApprovedAsync();

        Assert.False(result.Decision.SubmitsOrders);
        Assert.True(result.Decision.NotSubmitted);
        Assert.True(result.Decision.NoBrokerRoute);
    }

    [Fact]
    public async Task Executable_routed_or_submitted_plan_is_rejected()
    {
        var context = await CreateContextAsync();
        var invalidArchive = context.PlanArchive.ArchiveRecord with { SubmittedOrders = true };
        var service = new PaperExecutionPlanApprovalService(new InMemoryPaperExecutionPlanApprovalRepository(), new FixedClock(ProducedAt));

        var result = await service.ReviewAsync(
            invalidArchive,
            Request(
                OperatorDecisionId.New(),
                PaperExecutionPlanOperatorAction.ApproveForPaperSimulation,
                PaperPlanApprovalReasonCategory.AcceptedForSimulationReadiness,
                blockedAcknowledged: true,
                missingAcknowledged: true,
                driftAcknowledged: true),
            CancellationToken.None);

        Assert.False(result.Persisted);
        Assert.Equal(PaperExecutionPlanApprovalStatus.RejectedByGate, result.Decision.DecisionStatus);
        Assert.Equal(PaperSimulationReadinessStatus.Rejected, result.Decision.ResultingSimulationReadinessStatus);
    }

    [Fact]
    public async Task Duplicate_operator_decision_id_handling_is_idempotent()
    {
        var context = await CreateContextAsync();
        var repository = new InMemoryPaperExecutionPlanApprovalRepository();
        var service = new PaperExecutionPlanApprovalService(repository, new FixedClock(ProducedAt));
        var decisionId = new OperatorDecisionId("decision-r015-duplicate");
        var request = Request(
            decisionId,
            PaperExecutionPlanOperatorAction.Hold,
            PaperPlanApprovalReasonCategory.HeldDueToBlockedLines);

        var first = await service.ReviewAsync(context.PlanArchive.ArchiveRecord, request, CancellationToken.None);
        var second = await service.ReviewAsync(context.PlanArchive.ArchiveRecord, request, CancellationToken.None);

        Assert.True(first.Persisted);
        Assert.False(first.AlreadyRecorded);
        Assert.False(second.Persisted);
        Assert.True(second.AlreadyRecorded);
        Assert.Equal(PaperExecutionPlanApprovalStatus.DuplicateReturned, second.Decision.DecisionStatus);
    }

    [Fact]
    public async Task Qubes_run_id_and_cycle_run_id_are_preserved()
    {
        var result = await ApprovedAsync();

        Assert.Equal("cycle-r015-sample", result.Decision.CycleRunId);
        Assert.Equal("qubes-r015-sample", result.Decision.QubesRunId);
    }

    [Fact]
    public async Task Paper_execution_plan_id_is_preserved()
    {
        var context = await CreateContextAsync();
        var result = await ReviewAsync(
            context,
            PaperExecutionPlanOperatorAction.ApproveForPaperSimulation,
            PaperPlanApprovalReasonCategory.AcceptedForSimulationReadiness,
            blockedAcknowledged: true,
            missingAcknowledged: true,
            driftAcknowledged: true);

        Assert.Equal(context.PlanArchive.ArchiveRecord.PaperExecutionPlanId, result.Decision.PaperExecutionPlanId);
    }

    [Fact]
    public async Task Candidate_risk_rebalance_and_lot_sizing_lineage_is_preserved()
    {
        var result = await ApprovedAsync();

        Assert.True(result.Decision.PaperCandidateLineagePreserved);
        Assert.True(result.Decision.RiskLineagePreserved);
        Assert.True(result.Decision.RebalanceIntentLineagePreserved);
        Assert.True(result.Decision.LotSizingLineagePreserved);
        Assert.True(result.Decision.PlanLineagePreserved);
    }

    [Fact]
    public void Approval_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperExecutionPlanApproval.cs"));

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
    public void Approval_source_introduces_no_scheduler_timer_polling_or_background_job()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperExecutionPlanApproval.cs"));

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
        var result = await ApprovedAsync();
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.True(result.Decision.NoExternal);
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

    private static async Task<PaperExecutionPlanApprovalResult> ApprovedAsync()
        => await ReviewAsync(
            await CreateContextAsync(),
            PaperExecutionPlanOperatorAction.ApproveForPaperSimulation,
            PaperPlanApprovalReasonCategory.AcceptedForSimulationReadiness,
            blockedAcknowledged: true,
            missingAcknowledged: true,
            driftAcknowledged: true);

    private static async Task<PaperExecutionPlanApprovalResult> ReviewAsync(
        TestContext context,
        PaperExecutionPlanOperatorAction decisionType,
        PaperPlanApprovalReasonCategory reason,
        bool blockedAcknowledged = false,
        bool missingAcknowledged = false,
        bool driftAcknowledged = false)
        => await new PaperExecutionPlanApprovalService(
                new InMemoryPaperExecutionPlanApprovalRepository(),
                new FixedClock(ProducedAt))
            .ReviewAsync(
                context.PlanArchive.ArchiveRecord,
                Request(
                    OperatorDecisionId.New(),
                    decisionType,
                    reason,
                    blockedAcknowledged,
                    missingAcknowledged,
                    driftAcknowledged),
                CancellationToken.None);

    private static PaperExecutionPlanApprovalRequest Request(
        OperatorDecisionId decisionId,
        PaperExecutionPlanOperatorAction decisionType,
        PaperPlanApprovalReasonCategory reason,
        bool blockedAcknowledged = false,
        bool missingAcknowledged = false,
        bool driftAcknowledged = false)
        => new(
            decisionId,
            decisionType,
            "operator-placeholder",
            reason,
            "No-external R015 plan approval fixture.",
            blockedAcknowledged,
            missingAcknowledged,
            driftAcknowledged);

    private static async Task<TestContext> CreateContextAsync()
    {
        var services = CreateServices();
        var cycle = await services.Cycle.RunOneCycleAsync(new QubesIntradayCycleFixtureRequest(
            "cycle-r015-sample",
            new QubesRunId("qubes-r015-sample"),
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
                "No-external paper execution plan approval fixture.",
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

        return new TestContext(cycle, cycleArchive, review, paperReview, candidateBatch, candidateArchive, fixture, lotSized, plan, planArchive);
    }

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var normalized = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId("qubes-r015-reference"),
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
        PaperExecutionPlanArchiveResult PlanArchive);

    private sealed record TestServices(
        PlatformState State,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
        QubesIntradayCycleFixtureService Cycle,
        IntradayCycleArchiveService Archive,
        IntradayCycleOperatorReviewService OperatorReview);
}
