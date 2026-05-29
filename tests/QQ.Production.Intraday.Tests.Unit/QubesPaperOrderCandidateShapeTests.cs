using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesPaperOrderCandidateShapeTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task Accepted_r009_review_lines_create_paper_order_candidates()
    {
        var context = await CreateContextAsync();
        var batch = CreateBatch(context);

        Assert.Equal(3, batch.Candidates.Count);
        Assert.Equal(3, batch.AcceptedPaperReviewLineCount);
        Assert.All(batch.Candidates, x => Assert.Equal(PaperPreTradeRiskResultCategory.AcceptedForPaperReview, x.RiskReviewReference.RiskResult));
    }

    [Fact]
    public async Task Blocked_r009_review_lines_do_not_create_ready_candidates()
    {
        var context = await CreateContextAsync();
        var batch = CreateBatch(context);

        Assert.Equal(10, batch.BlockedLines.Count);
        Assert.DoesNotContain(batch.Candidates.Select(x => x.NormalizedSymbol), symbol => batch.BlockedLines.Any(x => x.Symbol == symbol));
    }

    [Fact]
    public async Task Candidate_references_cycle_run_id_and_qubes_run_id()
    {
        var context = await CreateContextAsync();
        var candidate = CreateBatch(context).Candidates.Single(x => x.NormalizedSymbol == "AUDUSD");

        Assert.Equal("cycle-r010-sample", candidate.CycleRunId);
        Assert.Equal("qubes-r010-sample", candidate.QubesRunId);
    }

    [Fact]
    public async Task Candidate_references_operator_decision_id()
    {
        var context = await CreateContextAsync();
        var batch = CreateBatch(context);

        Assert.All(batch.Candidates, x => Assert.Equal(context.OperatorReview.Decision.OperatorDecisionId, x.OperatorDecisionId));
        Assert.True(batch.OperatorDecisionLineagePreserved);
    }

    [Fact]
    public async Task Candidate_references_source_rebalance_intent()
    {
        var context = await CreateContextAsync();
        var candidate = CreateBatch(context).Candidates.Single(x => x.NormalizedSymbol == "EURUSD");

        Assert.Contains("EURUSD:rebalance-intent", candidate.SourceRebalanceIntentId);
        Assert.True(CreateBatch(context).RebalanceIntentLineagePreserved);
    }

    [Fact]
    public async Task Candidate_references_paper_risk_review_result()
    {
        var context = await CreateContextAsync();
        var candidate = CreateBatch(context).Candidates.Single(x => x.NormalizedSymbol == "GBPUSD");

        Assert.Contains("GBPUSD:paper-risk-line", candidate.RiskReviewReference.SourceRiskReviewLineId);
        Assert.Equal(PaperOmsReviewStatus.AcceptedForPaperReview, candidate.RiskReviewReference.RiskReviewStatus);
        Assert.True(CreateBatch(context).RiskLineagePreserved);
    }

    [Fact]
    public async Task Positive_delta_creates_buy_candidate()
    {
        var context = await CreateContextAsync();
        var candidate = CreateBatch(context).Candidates.Single(x => x.NormalizedSymbol == "AUDUSD");

        Assert.True(candidate.DeltaWeight > 0m);
        Assert.Equal(IntentSide.Buy, candidate.Side);
    }

    [Fact]
    public async Task Negative_delta_creates_sell_candidate()
    {
        var context = await CreateContextAsync();
        var candidate = CreateBatch(context).Candidates.Single(x => x.NormalizedSymbol == "GBPUSD");

        Assert.True(candidate.DeltaWeight < 0m);
        Assert.Equal(IntentSide.Sell, candidate.Side);
    }

    [Fact]
    public async Task Zero_delta_creates_no_candidate_by_convention()
    {
        var context = await CreateContextAsync();
        var zero = context.PaperReview.Lines.First(x => x.Symbol == "AUDUSD") with
        {
            DeltaWeight = 0m,
            DeltaNotional = 0m
        };
        var report = context.PaperReview with { Lines = [zero] };
        var batch = CreateBatch(context with { PaperReview = report });

        Assert.Empty(batch.Candidates);
        Assert.Equal(1, batch.AcceptedPaperReviewLineCount);
    }

    [Fact]
    public async Task Candidate_is_explicitly_non_executable()
    {
        var context = await CreateContextAsync();
        var batch = CreateBatch(context);

        Assert.True(batch.CandidatesAreNonExecutable);
        Assert.All(batch.Candidates, x => Assert.True(x.NonExecutable));
        Assert.All(batch.Candidates, x => Assert.Equal(PaperOrderTypeShapeCategory.NotExecutable, x.OrderTypeShapeCategory));
        Assert.All(batch.Candidates, x => Assert.Equal(PaperTimeInForceShapeCategory.NotExecutable, x.TimeInForceShapeCategory));
    }

    [Fact]
    public async Task Candidate_is_not_an_order_not_submitted_and_has_no_broker_route()
    {
        var context = await CreateContextAsync();
        var batch = CreateBatch(context);

        Assert.True(batch.CandidatesAreNotOrders);
        Assert.True(batch.CandidatesAreNotSubmitted);
        Assert.True(batch.CandidatesHaveNoBrokerRoute);
        Assert.All(batch.Candidates, x => Assert.True(x.NotAnOrder && x.NotSubmitted && x.NoBrokerRoute));
    }

    [Fact]
    public async Task Quantity_remains_safe_shape_when_live_mark_or_lot_sizing_is_unavailable()
    {
        var context = await CreateContextAsync();
        var batch = CreateBatch(context);

        Assert.All(batch.Candidates, x => Assert.Equal(PaperQuantityShapeCategory.QuantityRequiresMarkOrLotSizing, x.QuantityShapeCategory));
        Assert.All(batch.Candidates, x => Assert.Equal(PaperOrderCandidateStatus.PaperCandidateRequiresLotSizing, x.CandidateStatus));
    }

    [Fact]
    public async Task No_oms_parent_child_or_broker_order_is_created()
    {
        var context = await CreateContextAsync();
        var batch = CreateBatch(context);

        Assert.False(batch.CreatedOmsOrder);
        Assert.False(batch.CreatedParentOrder);
        Assert.False(batch.CreatedChildOrder);
        Assert.False(batch.CreatedBrokerOrder);
        Assert.DoesNotContain(batch.Candidates, x => x.CreatesOmsOrder || x.CreatesParentOrder || x.CreatesChildOrder || x.CreatesBrokerOrder);
    }

    [Fact]
    public async Task No_order_submission_path_is_introduced()
    {
        var context = await CreateContextAsync();
        var batch = CreateBatch(context);

        Assert.False(batch.SubmittedOrders);
        Assert.All(batch.Candidates, x => Assert.True(x.NotSubmitted));
    }

    [Fact]
    public async Task No_fill_or_execution_report_is_introduced()
    {
        var context = await CreateContextAsync();
        var batch = CreateBatch(context);

        Assert.False(batch.CreatedFill);
        Assert.False(batch.CreatedExecutionReport);
        Assert.DoesNotContain(batch.Candidates, x => x.CreatesFill || x.CreatesExecutionReport);
    }

    [Fact]
    public void Candidate_shape_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperOrderCandidateShape.cs"));

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
    public void Candidate_shape_source_introduces_no_scheduler_timer_polling_or_background_job()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperOrderCandidateShape.cs"));

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
        var context = await CreateContextAsync();
        var batch = CreateBatch(context);
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.Contains(batch.Candidates, x => x.NormalizedSymbol == "AUDUSD");
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

    private static PaperOrderCandidateBatch CreateBatch(TestContext context)
        => new PaperOrderCandidateShapeService().Create(new PaperOrderCandidateShapeRequest(
            context.Cycle,
            context.OperatorReview,
            context.PaperReview));

    private static async Task<TestContext> CreateContextAsync()
    {
        var services = CreateServices();
        var cycle = await services.Cycle.RunOneCycleAsync(new QubesIntradayCycleFixtureRequest(
            "cycle-r010-sample",
            new QubesRunId("qubes-r010-sample"),
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
        var archive = await services.Archive.ArchiveAsync(cycle, CancellationToken.None);
        var review = await services.OperatorReview.ReviewAsync(
            archive.ArchiveRecord,
            new IntradayCycleOperatorDecisionRequest(
                OperatorDecisionId.New(),
                OperatorDecisionType.PromoteToPaperReady,
                "operator-placeholder",
                OperatorDecisionReasonCategory.AcceptedForPaperReview,
                "No-external paper order candidate shape fixture.",
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

        return new TestContext(cycle, archive, review, paperReview);
    }

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var normalized = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId("qubes-r010-reference"),
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
        IntradayCycleArchiveResult Archive,
        IntradayCycleOperatorReviewResult OperatorReview,
        PaperOmsIntentReviewReport PaperReview);

    private sealed record TestServices(
        PlatformState State,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
        QubesIntradayCycleFixtureService Cycle,
        IntradayCycleArchiveService Archive,
        IntradayCycleOperatorReviewService OperatorReview);
}
