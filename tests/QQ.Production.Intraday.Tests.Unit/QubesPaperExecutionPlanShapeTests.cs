using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesPaperExecutionPlanShapeTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task R012_lot_sized_candidates_can_feed_paper_execution_plan_shape_generation()
    {
        var context = await CreateContextAsync();
        var plan = CreatePlan(context);

        Assert.Equal(context.LotSizedBatch.CycleRunId, plan.CycleRunId);
        Assert.Equal(3, plan.Lines.Count);
    }

    [Fact]
    public async Task One_paper_execution_plan_batch_is_created_for_the_cycle()
    {
        var context = await CreateContextAsync();
        var plan = CreatePlan(context);

        Assert.Equal("cycle-r013-sample:paper-execution-plan", plan.PaperExecutionPlanId.Value);
        Assert.Equal(PaperExecutionPlanStatus.PaperPlanPartiallyReady, plan.PlanStatus);
    }

    [Fact]
    public async Task Audusd_buy_line_is_included()
    {
        var line = CreatePlan(await CreateContextAsync()).Lines.Single(x => x.NormalizedSymbol == "AUDUSD");

        Assert.Equal(IntentSide.Buy, line.Side);
        Assert.Equal(131000m, line.PaperBaseQuantity);
    }

    [Fact]
    public async Task Eurusd_buy_line_is_included()
    {
        var line = CreatePlan(await CreateContextAsync()).Lines.Single(x => x.NormalizedSymbol == "EURUSD");

        Assert.Equal(IntentSide.Buy, line.Side);
        Assert.Equal(124000m, line.PaperBaseQuantity);
    }

    [Fact]
    public async Task Gbpusd_sell_line_is_included()
    {
        var line = CreatePlan(await CreateContextAsync()).Lines.Single(x => x.NormalizedSymbol == "GBPUSD");

        Assert.Equal(IntentSide.Sell, line.Side);
        Assert.Equal(368000m, line.PaperBaseQuantity);
    }

    [Fact]
    public async Task Plan_preserves_cycle_run_id_and_qubes_run_id()
    {
        var plan = CreatePlan(await CreateContextAsync());

        Assert.Equal("cycle-r013-sample", plan.CycleRunId);
        Assert.Equal("qubes-r013-sample", plan.QubesRunId);
        Assert.All(plan.Lines, x => Assert.Equal(plan.CycleRunId, x.CycleRunId));
        Assert.All(plan.Lines, x => Assert.Equal(plan.QubesRunId, x.QubesRunId));
    }

    [Fact]
    public async Task Plan_preserves_operator_decision_id()
    {
        var context = await CreateContextAsync();
        var plan = CreatePlan(context);

        Assert.Equal(context.LotSizedBatch.OperatorDecisionId, plan.OperatorDecisionId);
        Assert.True(plan.OperatorDecisionLineagePreserved);
    }

    [Fact]
    public async Task Plan_preserves_source_paper_candidate_ids()
    {
        var plan = CreatePlan(await CreateContextAsync());

        Assert.All(plan.Lines, x => Assert.Contains(":paper-candidate", x.PaperOrderCandidateId.Value));
        Assert.True(plan.PaperCandidateLineagePreserved);
    }

    [Fact]
    public async Task Plan_preserves_source_rebalance_intent_lineage()
    {
        var plan = CreatePlan(await CreateContextAsync());

        Assert.All(plan.Lines, x => Assert.Contains(":rebalance-intent", x.SourceRebalanceIntentId));
        Assert.True(plan.RebalanceIntentLineagePreserved);
    }

    [Fact]
    public async Task Plan_preserves_risk_review_references()
    {
        var plan = CreatePlan(await CreateContextAsync());

        Assert.All(plan.Lines, x => Assert.Contains(":paper-risk-line", x.RiskReviewReference.SourceRiskReviewLineId));
        Assert.True(plan.RiskLineagePreserved);
    }

    [Fact]
    public async Task Plan_preserves_quantity_shapes_and_lot_sizing_metadata()
    {
        var plan = CreatePlan(await CreateContextAsync());

        Assert.True(plan.LotSizingLineagePreserved);
        Assert.All(plan.Lines, x => Assert.Equal(PaperLotSizingStatus.PaperSized, x.QuantityStatus));
        Assert.All(plan.Lines, x => Assert.Equal(1000m, x.LotSize));
        Assert.All(plan.Lines, x => Assert.Equal(PaperQuantityRoundingMode.RoundToNearestLot, x.QuantityRoundingMode));
        Assert.Contains(plan.Lines, x => x.QuantityCurrency == "AUD" && x.NotionalCurrency == "USD");
    }

    [Fact]
    public async Task Plan_is_paper_only()
    {
        var plan = CreatePlan(await CreateContextAsync());

        Assert.True(plan.PaperOnly);
        Assert.Contains(PaperExecutionPlanMode.PaperOnly, plan.PlanModes);
        Assert.All(plan.Lines, x => Assert.True(x.PaperOnly));
    }

    [Fact]
    public async Task Plan_is_non_executable()
    {
        var plan = CreatePlan(await CreateContextAsync());

        Assert.True(plan.NonExecutable);
        Assert.Contains(PaperExecutionPlanMode.NonExecutable, plan.PlanModes);
        Assert.All(plan.Lines, x => Assert.True(x.NonExecutable));
    }

    [Fact]
    public async Task Plan_is_not_an_order_not_submitted_and_no_broker_route()
    {
        var plan = CreatePlan(await CreateContextAsync());

        Assert.True(plan.NotAnOrder);
        Assert.True(plan.NotSubmitted);
        Assert.True(plan.NoBrokerRoute);
        Assert.All(plan.Lines, x => Assert.True(x.NotAnOrder && x.NotSubmitted && x.NoBrokerRoute));
    }

    [Fact]
    public async Task Execution_style_and_time_in_force_are_shape_only()
    {
        var plan = CreatePlan(await CreateContextAsync());

        Assert.All(plan.Lines, x => Assert.Equal(PaperExecutionStyleShape.MarketShapeOnly, x.ExecutionStyleShape));
        Assert.All(plan.Lines, x => Assert.Equal(PaperExecutionTimeInForceShape.DayShapeOnly, x.TimeInForceShape));
        Assert.All(plan.Lines, x => Assert.Equal("Paper execution plan shape only; not an OMS order, not a broker order, not submitted, and no broker route exists.", x.NonExecutableReason));
    }

    [Fact]
    public async Task Blocked_r011_lines_do_not_become_ready_plan_lines()
    {
        var plan = CreatePlan(await CreateContextAsync());

        Assert.Equal(10, plan.BlockedLines.Count);
        Assert.Equal(10, plan.BlockedLineCount);
        Assert.DoesNotContain(plan.Lines, x => plan.BlockedLines.Any(blocked => blocked.Symbol == x.NormalizedSymbol));
    }

    [Fact]
    public async Task Missing_stale_mark_warnings_are_preserved()
    {
        var plan = CreatePlan(await CreateContextAsync());

        Assert.True(plan.MissingStaleMarkWarningsPreserved);
    }

    [Fact]
    public async Task Drift_acknowledgement_is_preserved()
    {
        var plan = CreatePlan(await CreateContextAsync());

        Assert.True(plan.DriftAcknowledgementPreserved);
    }

    [Fact]
    public async Task No_oms_parent_child_or_broker_order_is_created()
    {
        var plan = CreatePlan(await CreateContextAsync());

        Assert.False(plan.CreatedOmsOrder);
        Assert.False(plan.CreatedParentOrder);
        Assert.False(plan.CreatedChildOrder);
        Assert.False(plan.CreatedBrokerOrder);
        Assert.DoesNotContain(plan.Lines, x => x.CreatesOmsOrder || x.CreatesParentOrder || x.CreatesChildOrder || x.CreatesBrokerOrder);
    }

    [Fact]
    public async Task No_fill_or_execution_report_is_introduced()
    {
        var plan = CreatePlan(await CreateContextAsync());

        Assert.False(plan.CreatedFill);
        Assert.False(plan.CreatedExecutionReport);
        Assert.DoesNotContain(plan.Lines, x => x.CreatesFill || x.CreatesExecutionReport);
    }

    [Fact]
    public async Task No_order_submission_path_is_introduced()
    {
        var plan = CreatePlan(await CreateContextAsync());

        Assert.False(plan.SubmittedOrders);
        Assert.All(plan.Lines, x => Assert.True(x.NotSubmitted));
    }

    [Fact]
    public void Plan_shape_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperExecutionPlanShape.cs"));

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
    public void Plan_shape_source_introduces_no_scheduler_timer_polling_or_background_job()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperExecutionPlanShape.cs"));

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
        var plan = CreatePlan(await CreateContextAsync());
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.Contains(plan.Lines, x => x.NormalizedSymbol == "AUDUSD");
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

    private static PaperExecutionPlanBatch CreatePlan(TestContext context)
        => new PaperExecutionPlanShapeService().Create(context.LotSizedBatch);

    private static async Task<TestContext> CreateContextAsync()
    {
        var services = CreateServices();
        var cycle = await services.Cycle.RunOneCycleAsync(new QubesIntradayCycleFixtureRequest(
            "cycle-r013-sample",
            new QubesRunId("qubes-r013-sample"),
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
                "No-external paper execution plan shape fixture.",
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

        return new TestContext(cycle, cycleArchive, review, paperReview, candidateBatch, candidateArchive, fixture, lotSized);
    }

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var normalized = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId("qubes-r013-reference"),
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
        PaperLotSizedCandidateBatch LotSizedBatch);

    private sealed record TestServices(
        PlatformState State,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
        QubesIntradayCycleFixtureService Cycle,
        IntradayCycleArchiveService Archive,
        IntradayCycleOperatorReviewService OperatorReview);
}
