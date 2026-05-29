using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesPaperCandidateLotSizingTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task R011_archived_candidates_can_feed_r012_lot_sizing()
    {
        var context = await CreateContextAsync();
        var batch = Size(context);

        Assert.Equal(3, batch.SizedCandidates.Count);
        Assert.Equal(context.Archive.ArchiveRecord.PaperOrderCandidateBatchId, batch.PaperOrderCandidateBatchId);
    }

    [Fact]
    public async Task Audusd_buy_candidate_receives_paper_instrument_convention()
    {
        var context = await CreateContextAsync();
        var candidate = Size(context).SizedCandidates.Single(x => x.NormalizedSymbol == "AUDUSD");

        Assert.Equal(IntentSide.Buy, candidate.Side);
        Assert.NotNull(candidate.InstrumentConvention);
        Assert.Equal("AUDUSD", candidate.InstrumentConvention.PaperTradableSymbol);
    }

    [Fact]
    public async Task Eurusd_buy_candidate_receives_paper_instrument_convention()
    {
        var context = await CreateContextAsync();
        var candidate = Size(context).SizedCandidates.Single(x => x.NormalizedSymbol == "EURUSD");

        Assert.Equal(IntentSide.Buy, candidate.Side);
        Assert.NotNull(candidate.InstrumentConvention);
        Assert.Equal("EURUSD", candidate.InstrumentConvention.PaperTradableSymbol);
    }

    [Fact]
    public async Task Gbpusd_sell_candidate_receives_paper_instrument_convention()
    {
        var context = await CreateContextAsync();
        var candidate = Size(context).SizedCandidates.Single(x => x.NormalizedSymbol == "GBPUSD");

        Assert.Equal(IntentSide.Sell, candidate.Side);
        Assert.NotNull(candidate.InstrumentConvention);
        Assert.Equal("GBPUSD", candidate.InstrumentConvention.PaperTradableSymbol);
    }

    [Fact]
    public async Task Usd_quote_convention_sets_base_and_quote_correctly()
    {
        var context = await CreateContextAsync();
        var batch = Size(context);

        var audusd = batch.SizedCandidates.Single(x => x.NormalizedSymbol == "AUDUSD").InstrumentConvention!;
        Assert.Equal("AUD", audusd.BaseCurrency);
        Assert.Equal("USD", audusd.QuoteCurrency);
        Assert.Equal("USD", audusd.NormalizedQuoteCurrency);
        Assert.True(audusd.IsUsdQuoteNormalized);
        Assert.False(audusd.RequiresInversion);
        Assert.Equal("AUD", audusd.QuantityCurrency);
        Assert.Equal("USD", audusd.NotionalCurrency);
    }

    [Fact]
    public async Task Delta_notional_converts_to_base_quantity_shape_using_fixture_mid()
    {
        var context = await CreateContextAsync();
        var candidate = Size(context).SizedCandidates.Single(x => x.NormalizedSymbol == "AUDUSD");

        Assert.Equal(PaperLotSizingStatus.PaperSized, candidate.SizingStatus);
        Assert.Equal(0.66m, candidate.QuantityShape.FixtureMid);
        Assert.Equal(130572.727273m, Math.Round(candidate.QuantityShape.AbsoluteBaseQuantityUnrounded!.Value, 6));
    }

    [Fact]
    public async Task Lot_size_rounding_is_applied_deterministically()
    {
        var context = await CreateContextAsync();
        var batch = Size(context);

        Assert.Equal(131000m, batch.SizedCandidates.Single(x => x.NormalizedSymbol == "AUDUSD").QuantityShape.AbsoluteBaseQuantityRounded);
        Assert.Equal(124000m, batch.SizedCandidates.Single(x => x.NormalizedSymbol == "EURUSD").QuantityShape.AbsoluteBaseQuantityRounded);
        Assert.Equal(368000m, batch.SizedCandidates.Single(x => x.NormalizedSymbol == "GBPUSD").QuantityShape.AbsoluteBaseQuantityRounded);
        Assert.All(batch.SizedCandidates, x => Assert.Equal(PaperQuantityRoundingMode.RoundToNearestLot, x.QuantityShape.RoundingMode));
    }

    [Fact]
    public async Task Missing_fixture_mark_yields_requires_mark()
    {
        var context = await CreateContextAsync();
        var fixture = context.Fixture with
        {
            MarksByNormalizedSymbol = context.Fixture.MarksByNormalizedSymbol
                .Where(x => x.Key != "AUDUSD")
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
        };
        var batch = Size(context with { Fixture = fixture });

        Assert.Equal(PaperLotSizingStatus.RequiresMark, batch.SizedCandidates.Single(x => x.NormalizedSymbol == "AUDUSD").SizingStatus);
        Assert.Equal(1, batch.RequiresMarkCount);
    }

    [Fact]
    public async Task Missing_lot_convention_yields_requires_lot_sizing()
    {
        var context = await CreateContextAsync();
        var broken = context.Fixture.ConventionsByNormalizedSymbol["AUDUSD"] with
        {
            LotSize = null
        };
        var conventions = context.Fixture.ConventionsByNormalizedSymbol.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        conventions["AUDUSD"] = broken;
        var batch = Size(context with { Fixture = context.Fixture with { ConventionsByNormalizedSymbol = conventions } });

        Assert.Equal(PaperLotSizingStatus.RequiresLotSizing, batch.SizedCandidates.Single(x => x.NormalizedSymbol == "AUDUSD").SizingStatus);
        Assert.Equal(1, batch.RequiresLotSizingCount);
    }

    [Fact]
    public async Task Missing_instrument_convention_yields_requires_instrument_convention()
    {
        var context = await CreateContextAsync();
        var fixture = context.Fixture with
        {
            ConventionsByNormalizedSymbol = context.Fixture.ConventionsByNormalizedSymbol
                .Where(x => x.Key != "AUDUSD")
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
        };
        var batch = Size(context with { Fixture = fixture });

        Assert.Equal(PaperLotSizingStatus.RequiresInstrumentConvention, batch.SizedCandidates.Single(x => x.NormalizedSymbol == "AUDUSD").SizingStatus);
        Assert.Equal(1, batch.RequiresInstrumentConventionCount);
    }

    [Fact]
    public async Task Sized_candidates_remain_paper_only()
    {
        var context = await CreateContextAsync();
        var batch = Size(context);

        Assert.All(batch.SizedCandidates, x => Assert.True(x.PaperOnly));
        Assert.All(batch.SizedCandidates, x => Assert.True(x.QuantityShape.PaperOnly));
    }

    [Fact]
    public async Task Sized_candidates_remain_non_executable()
    {
        var context = await CreateContextAsync();
        var batch = Size(context);

        Assert.True(batch.SizedCandidatesAreNonExecutable);
        Assert.All(batch.SizedCandidates, x => Assert.True(x.NonExecutable));
        Assert.All(batch.SizedCandidates, x => Assert.True(x.QuantityShape.NonExecutable));
    }

    [Fact]
    public async Task Sized_candidates_remain_not_an_order_not_submitted_and_no_broker_route()
    {
        var context = await CreateContextAsync();
        var batch = Size(context);

        Assert.True(batch.SizedCandidatesAreNotOrders);
        Assert.True(batch.SizedCandidatesAreNotSubmitted);
        Assert.True(batch.SizedCandidatesHaveNoBrokerRoute);
        Assert.All(batch.SizedCandidates, x => Assert.True(x.NotAnOrder && x.NotSubmitted && x.NoBrokerRoute));
    }

    [Fact]
    public async Task No_oms_parent_child_or_broker_order_is_created()
    {
        var context = await CreateContextAsync();
        var batch = Size(context);

        Assert.False(batch.CreatedOmsOrder);
        Assert.False(batch.CreatedParentOrder);
        Assert.False(batch.CreatedChildOrder);
        Assert.False(batch.CreatedBrokerOrder);
        Assert.DoesNotContain(batch.SizedCandidates, x => x.CreatesOmsOrder || x.CreatesParentOrder || x.CreatesChildOrder || x.CreatesBrokerOrder);
    }

    [Fact]
    public async Task No_fill_or_execution_report_is_introduced()
    {
        var context = await CreateContextAsync();
        var batch = Size(context);

        Assert.False(batch.CreatedFill);
        Assert.False(batch.CreatedExecutionReport);
        Assert.DoesNotContain(batch.SizedCandidates, x => x.CreatesFill || x.CreatesExecutionReport);
    }

    [Fact]
    public async Task No_order_submission_path_is_introduced()
    {
        var context = await CreateContextAsync();
        var batch = Size(context);

        Assert.False(batch.SubmittedOrders);
        Assert.All(batch.SizedCandidates, x => Assert.True(x.NotSubmitted));
    }

    [Fact]
    public void Lot_sizing_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperCandidateLotSizing.cs"));

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
    public void Lot_sizing_source_introduces_no_scheduler_timer_polling_or_background_job()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperCandidateLotSizing.cs"));

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
        var batch = Size(context);
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.Contains(batch.SizedCandidates, x => x.NormalizedSymbol == "AUDUSD");
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

    private static PaperLotSizedCandidateBatch Size(TestContext context)
        => new PaperLotSizingService().Size(new PaperLotSizingRequest(context.Archive, context.Fixture));

    private static async Task<TestContext> CreateContextAsync()
    {
        var services = CreateServices();
        var cycle = await services.Cycle.RunOneCycleAsync(new QubesIntradayCycleFixtureRequest(
            "cycle-r012-sample",
            new QubesRunId("qubes-r012-sample"),
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
                "No-external paper lot sizing fixture.",
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

        return new TestContext(cycle, cycleArchive, review, paperReview, candidateBatch, candidateArchive, fixture);
    }

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var normalized = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId("qubes-r012-reference"),
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
        PaperOrderCandidateArchiveResult Archive,
        PaperLotSizingFixtureContract Fixture);

    private sealed record TestServices(
        PlatformState State,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
        QubesIntradayCycleFixtureService Cycle,
        IntradayCycleArchiveService Archive,
        IntradayCycleOperatorReviewService OperatorReview);
}
