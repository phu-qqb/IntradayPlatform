using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesPaperExecutionPlanArchiveTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task R013_paper_execution_plan_can_be_archived_no_externally()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.True(result.Persisted);
        Assert.True(result.ArchiveRecord.NoExternal);
        Assert.Equal(PaperExecutionPlanArchiveStatus.ArchivedWithBlockedLines, result.ArchiveRecord.ArchiveStatus);
    }

    [Fact]
    public async Task Plan_archive_preserves_cycle_run_id_and_qubes_run_id()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.Equal("cycle-r014-sample", result.ArchiveRecord.CycleRunId);
        Assert.Equal("qubes-r014-sample", result.ArchiveRecord.QubesRunId);
        Assert.Equal(result.ArchiveRecord.CycleRunId, result.Blotter.CycleRunId);
        Assert.Equal(result.ArchiveRecord.QubesRunId, result.Blotter.QubesRunId);
    }

    [Fact]
    public async Task Plan_archive_preserves_operator_decision_id()
    {
        var context = await CreateContextAsync();
        var result = await ArchiveAsync(context);

        Assert.Equal(context.Plan.OperatorDecisionId, result.ArchiveRecord.OperatorDecisionId);
        Assert.True(result.ArchiveRecord.OperatorDecisionLineagePreserved);
    }

    [Fact]
    public async Task Plan_archive_preserves_paper_candidate_batch_id()
    {
        var context = await CreateContextAsync();
        var result = await ArchiveAsync(context);

        Assert.Equal(context.Plan.PaperOrderCandidateBatchId, result.ArchiveRecord.PaperOrderCandidateBatchId);
        Assert.True(result.ArchiveRecord.PaperCandidateLineagePreserved);
    }

    [Fact]
    public async Task Plan_lines_preserve_source_paper_candidate_ids()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.All(result.ArchiveRecord.PlanLines, x => Assert.Contains(":paper-candidate", x.PaperOrderCandidateId.Value));
        Assert.True(result.ArchiveRecord.PaperCandidateLineagePreserved);
    }

    [Fact]
    public async Task Plan_lines_preserve_source_rebalance_intent_ids()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.All(result.ArchiveRecord.PlanLines, x => Assert.Contains(":rebalance-intent", x.SourceRebalanceIntentId));
        Assert.True(result.ArchiveRecord.RebalanceIntentLineagePreserved);
    }

    [Fact]
    public async Task Plan_lines_preserve_risk_review_references()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.All(result.ArchiveRecord.PlanLines, x => Assert.Contains(":paper-risk-line", x.RiskReviewReference.SourceRiskReviewLineId));
        Assert.True(result.ArchiveRecord.RiskLineagePreserved);
    }

    [Fact]
    public async Task Plan_lines_preserve_lot_sizing_metadata()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.True(result.ArchiveRecord.LotSizingLineagePreserved);
        Assert.All(result.ArchiveRecord.PlanLines, x => Assert.Equal(PaperLotSizingStatus.PaperSized, x.QuantityStatus));
        Assert.All(result.ArchiveRecord.PlanLines, x => Assert.Equal(1000m, x.LotSize));
        Assert.All(result.ArchiveRecord.PlanLines, x => Assert.Equal(PaperQuantityRoundingMode.RoundToNearestLot, x.QuantityRoundingMode));
    }

    [Fact]
    public async Task Audusd_buy_line_appears_in_blotter()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.Contains(result.Blotter.Lines, x => x.Instrument == "AUDUSD" && x.Side == IntentSide.Buy && x.PaperBaseQuantity == 131000m);
    }

    [Fact]
    public async Task Eurusd_buy_line_appears_in_blotter()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.Contains(result.Blotter.Lines, x => x.Instrument == "EURUSD" && x.Side == IntentSide.Buy && x.PaperBaseQuantity == 124000m);
    }

    [Fact]
    public async Task Gbpusd_sell_line_appears_in_blotter()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.Contains(result.Blotter.Lines, x => x.Instrument == "GBPUSD" && x.Side == IntentSide.Sell && x.PaperBaseQuantity == 368000m);
    }

    [Fact]
    public async Task Blocked_r011_lines_are_preserved_separately()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.Equal(10, result.ArchiveRecord.BlockedLines.Count);
        Assert.Equal(10, result.ArchiveRecord.BlockedLineCount);
        Assert.DoesNotContain(result.ArchiveRecord.PlanLines, x => result.ArchiveRecord.BlockedLines.Any(blocked => blocked.Symbol == x.NormalizedSymbol));
    }

    [Fact]
    public async Task Blotter_includes_no_order_no_route_no_submission_no_fill_no_execution_report_disclaimer()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.Contains("No order created.", result.Blotter.Disclaimers);
        Assert.Contains("No broker route exists.", result.Blotter.Disclaimers);
        Assert.Contains("Plan lines are not executable.", result.Blotter.Disclaimers);
        Assert.Contains("Plan lines were not submitted.", result.Blotter.Disclaimers);
        Assert.Contains("No fills were created.", result.Blotter.Disclaimers);
        Assert.Contains("No execution reports were created.", result.Blotter.Disclaimers);
        Assert.Contains("No-External / No-Order / No-Route Disclaimer", result.BlotterMarkdown);
    }

    [Fact]
    public async Task Plan_remains_paper_only()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.Contains(PaperExecutionPlanMode.PaperOnly, result.ArchiveRecord.PlanModes);
        Assert.All(result.ArchiveRecord.PlanLines, x => Assert.True(x.PaperOnly));
    }

    [Fact]
    public async Task Plan_remains_non_executable()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.Contains(PaperExecutionPlanMode.NonExecutable, result.ArchiveRecord.PlanModes);
        Assert.All(result.ArchiveRecord.PlanLines, x => Assert.True(x.NonExecutable));
    }

    [Fact]
    public async Task Plan_remains_not_an_order_not_submitted_and_no_broker_route()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.All(result.ArchiveRecord.PlanLines, x => Assert.True(x.NotAnOrder && x.NotSubmitted && x.NoBrokerRoute));
        Assert.All(result.Blotter.Lines, x => Assert.True(x.NoOrderCreated && x.NotSubmitted && x.NoBrokerRoute));
    }

    [Fact]
    public async Task Duplicate_plan_archive_is_idempotent()
    {
        var context = await CreateContextAsync();
        var repository = new InMemoryPaperExecutionPlanArchiveRepository();
        var service = new PaperExecutionPlanArchiveService(repository, new FixedClock(ProducedAt));

        var first = await service.ArchiveAsync(context.Plan, CancellationToken.None);
        var second = await service.ArchiveAsync(context.Plan, CancellationToken.None);

        Assert.True(first.Persisted);
        Assert.False(first.AlreadyArchived);
        Assert.False(second.Persisted);
        Assert.True(second.AlreadyArchived);
        Assert.Equal(PaperExecutionPlanArchiveStatus.DuplicateReturned, second.ArchiveRecord.ArchiveStatus);
        Assert.Equal(first.ArchiveRecord.PaperExecutionPlanId, second.ArchiveRecord.PaperExecutionPlanId);
    }

    [Fact]
    public async Task No_oms_parent_child_or_broker_order_is_created()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.False(result.ArchiveRecord.CreatedOmsOrder);
        Assert.False(result.ArchiveRecord.CreatedParentOrder);
        Assert.False(result.ArchiveRecord.CreatedChildOrder);
        Assert.False(result.ArchiveRecord.CreatedBrokerOrder);
    }

    [Fact]
    public async Task No_fill_or_execution_report_is_introduced()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.False(result.ArchiveRecord.CreatedFill);
        Assert.False(result.ArchiveRecord.CreatedExecutionReport);
        Assert.All(result.Blotter.Lines, x => Assert.True(x.NoFill && x.NoExecutionReport));
    }

    [Fact]
    public async Task No_order_submission_path_is_introduced()
    {
        var result = await ArchiveAsync(await CreateContextAsync());

        Assert.False(result.ArchiveRecord.SubmittedOrders);
        Assert.All(result.ArchiveRecord.PlanLines, x => Assert.True(x.NotSubmitted));
    }

    [Fact]
    public void Archive_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperExecutionPlanArchive.cs"));

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
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperExecutionPlanArchive.cs"));

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

        Assert.Contains(result.Blotter.Lines, x => x.Instrument == "AUDUSD");
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

    private static async Task<PaperExecutionPlanArchiveResult> ArchiveAsync(TestContext context)
        => await new PaperExecutionPlanArchiveService(
                new InMemoryPaperExecutionPlanArchiveRepository(),
                new FixedClock(ProducedAt))
            .ArchiveAsync(context.Plan, CancellationToken.None);

    private static async Task<TestContext> CreateContextAsync()
    {
        var services = CreateServices();
        var cycle = await services.Cycle.RunOneCycleAsync(new QubesIntradayCycleFixtureRequest(
            "cycle-r014-sample",
            new QubesRunId("qubes-r014-sample"),
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
                "No-external paper execution plan archive fixture.",
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

        return new TestContext(cycle, cycleArchive, review, paperReview, candidateBatch, candidateArchive, fixture, lotSized, plan);
    }

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var normalized = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId("qubes-r014-reference"),
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
        PaperExecutionPlanBatch Plan);

    private sealed record TestServices(
        PlatformState State,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
        QubesIntradayCycleFixtureService Cycle,
        IntradayCycleArchiveService Archive,
        IntradayCycleOperatorReviewService OperatorReview);
}
