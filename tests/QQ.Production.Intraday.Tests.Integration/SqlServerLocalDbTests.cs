using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.Simulator;
using QQ.Production.Intraday.Infrastructure.SqlServer;

namespace QQ.Production.Intraday.Tests.Integration;

public sealed class SqlServerLocalDbTests
{
    private static readonly DateTimeOffset Now = new(2026, 04, 29, 09, 30, 00, TimeSpan.Zero);

    [Fact]
    public async Task LocalDb_migrates_seeds_and_processes_sample_model_run_with_fake_lmax()
    {
        if (!await IsLocalDbAvailableAsync()) return;

        await using var fixture = await LocalDbFixture.CreateAsync();
        await fixture.Initializer.SeedDemoDataAsync(CancellationToken.None);
        await AddSampleModelRunAsync(fixture);
        var state = await fixture.IntradayRepository.LoadStateAsync(CancellationToken.None);
        Assert.NotEmpty(state.Funds);
        Assert.NotEmpty(state.MarketData);

        var service = new ProcessModelRunService(fixture.IntradayRepository, new FakeLmaxGateway(new FakeLmaxOptions(), fixture.Clock), fixture.BrokerPositionProvider, fixture.Clock, new ReferenceDataIntegrityService(fixture.IntradayRepository, fixture.Clock));
        var result = await service.ProcessNextAsync();
        state = await fixture.IntradayRepository.LoadStateAsync(CancellationToken.None);

        Assert.True(result.Processed);
        Assert.Single(state.Fills);
        Assert.Contains(state.PositionLedger, x => x.Type == PositionLedgerEventType.Fill);
    }

    [Fact]
    public async Task LocalDb_persists_snapshots_and_bars_with_unique_upsert()
    {
        if (!await IsLocalDbAvailableAsync()) return;

        await using var fixture = await LocalDbFixture.CreateAsync();
        var state = await fixture.IntradayRepository.LoadStateAsync(CancellationToken.None);
        var provider = new FakeMarketDataProvider(fixture.Clock);
        var instrument = EurUsd(state);
        var venue = Lmax(state);
        var snapshots = await provider.GetSnapshotsAsync(instrument, venue, Now.AddMinutes(-15), TimeSpan.FromMinutes(1), 3, 1.1m, 1.1002m, 0.0001m, 0.0001m, CancellationToken.None);
        await fixture.SnapshotRepository.AddRangeAsync(snapshots, CancellationToken.None);

        var latest = await fixture.SnapshotRepository.GetLatestAsync(instrument.Id, venue.Id, CancellationToken.None);
        Assert.NotNull(latest);

        var first = await fixture.BarBuilder.BuildBarsAsync(venue.Id, BarTimeframe.FifteenMinutes, Now.AddMinutes(-15), Now, CancellationToken.None);
        var second = await fixture.BarBuilder.BuildBarsAsync(venue.Id, BarTimeframe.FifteenMinutes, Now.AddMinutes(-15), Now, CancellationToken.None);
        var bars = await fixture.BarRepository.GetRangeAsync(instrument.Id, venue.Id, BarTimeframe.FifteenMinutes, Now.AddMinutes(-15), Now, CancellationToken.None);

        Assert.Equal(1, first.BarsCreated);
        Assert.Equal(1, second.BarsUpdated);
        Assert.Single(bars);
    }

    [Fact]
    public async Task LocalDb_duplicate_fill_is_blocked_safely()
    {
        if (!await IsLocalDbAvailableAsync()) return;

        await using var fixture = await LocalDbFixture.CreateAsync();
        await fixture.Initializer.SeedDemoDataAsync(CancellationToken.None);
        await AddSampleModelRunAsync(fixture);
        var state = await fixture.IntradayRepository.LoadStateAsync(CancellationToken.None);
        var instrument = EurUsd(state);
        var venue = Lmax(state);
        var intent = new TradeIntent(TradeIntentId.New(), state.ModelRuns.Single().Id, state.Funds.Single().Id, instrument.Id, TradeSide.Buy, 10_000m, 1m, "test", TradeIntentStatus.Created, Now);
        await fixture.IntradayRepository.AddTradeIntentAsync(intent, CancellationToken.None);
        var parent = new ParentOrder(ParentOrderId.New(), intent.Id, new ClientOrderId("SQL-P-1"), OrderSide.Buy, 10_000m, ExecutionAlgo.MarketImmediate, OrderStatus.Created, Now);
        var child = new ChildOrder(ChildOrderId.New(), parent.Id, venue.Id, new ClientOrderId("SQL-C-1"), OrderSide.Buy, OrderType.Market, TimeInForce.IOC, 10_000m, 1m, OrderStatus.PendingNew, Now);
        await fixture.IntradayRepository.AddOrdersAsync(parent, child, CancellationToken.None);
        var fill = new Fill(FillId.New(), "SQL-DUP-1", child.Id, instrument.Id, venue.Id, TradeSide.Buy, 10_000m, 1m, 1.1m, Now, Now);

        Assert.True(await fixture.IntradayRepository.TryAddFillAsync(fill, CancellationToken.None));
        Assert.False(await fixture.IntradayRepository.TryAddFillAsync(fill with { Id = FillId.New() }, CancellationToken.None));
    }

    [Fact]
    public async Task LocalDb_demo_seed_does_not_create_stale_model_run()
    {
        if (!await IsLocalDbAvailableAsync()) return;

        await using var fixture = await LocalDbFixture.CreateAsync();
        await fixture.Initializer.SeedDemoDataAsync(CancellationToken.None);
        await fixture.Initializer.SeedDemoDataAsync(CancellationToken.None);
        var state = await fixture.IntradayRepository.LoadStateAsync(CancellationToken.None);

        Assert.Empty(state.ModelRuns);
        Assert.Empty(state.TargetWeights);
    }

    [Fact]
    public async Task LocalDb_reference_seed_is_idempotent_by_business_keys()
    {
        if (!await IsLocalDbAvailableAsync()) return;

        await using var fixture = await LocalDbFixture.CreateAsync();
        await fixture.Initializer.SeedReferenceDataAsync(CancellationToken.None);
        var state = await fixture.IntradayRepository.LoadStateAsync(CancellationToken.None);
        var integrity = await new ReferenceDataIntegrityService(fixture.IntradayRepository, fixture.Clock).CheckAsync(CancellationToken.None);

        Assert.Equal(0, integrity.BlockingIssueCount);
        var eurUsd = EurUsd(state);
        var usdJpy = state.Instruments.Single(x => x.Symbol == "USDJPY" && x.AssetClass == AssetClass.FxSpot);
        var venue = Lmax(state);
        Assert.Single(state.Instruments, x => x.Symbol == "EURUSD" && x.AssetClass == AssetClass.FxSpot);
        Assert.Single(state.Venues, x => x.Name == "LMAX");
        Assert.Single(state.VenueInstrumentMappings, x => x.VenueId == venue.Id && x.InstrumentId == eurUsd.Id && x.IsEnabled);
        Assert.Single(state.InstrumentAliases, x => x.Source == "LMAX_REPORT" && x.ExternalSymbol == "EUR/USD" && x.IsEnabled);
        Assert.Single(state.InstrumentAliases, x => x.Source == "LMAX_REPORT" && x.ExternalInstrumentId == "4001" && x.IsEnabled);
        Assert.Equal(eurUsd.Id, state.InstrumentAliases.Single(x => x.Source == "LMAX_REPORT" && x.ExternalSymbol == "EUR/USD").InstrumentId);
        Assert.Equal(usdJpy.Id, state.InstrumentAliases.Single(x => x.Source == "LMAX_REPORT" && x.ExternalSymbol == "USD/JPY").InstrumentId);
        Assert.Single(state.RiskLimitSets, x => x.FundId == state.Funds.Single().Id);
        Assert.Single(state.TradingWindows, x => x.FundId == state.Funds.Single().Id && x.ModelName == "IntradayFxModel" && x.DayOfWeek == Now.DayOfWeek);
        Assert.Single(state.KillSwitchStates);
    }

    [Fact]
    public async Task LocalDb_persists_qubes_raw_and_normalized_weight_lineage()
    {
        if (!await IsLocalDbAvailableAsync()) return;

        await using var fixture = await LocalDbFixture.CreateAsync();
        var producedAt = new DateTimeOffset(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
        var effectiveAt = producedAt.AddMinutes(15);
        var ingestion = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QQ.Production.Intraday.Domain.PmsEmsOmsFoundation.QubesRunId("qubes-r004b-localdb"),
            producedAt,
            effectiveAt,
            15,
            "QQ_MASTER",
            "IntradayFxModel",
            1_000_000m,
            TargetQuantityMode.PortfolioBaseCurrencyNotional,
            ["EURUSD Curncy;0.10"]));
        var generator = new FakeModelWeightGenerator(fixture.ModelWeightBatchRepository, new FixedClock(producedAt));
        var promotionService = new ModelWeightPromotionService(
            fixture.ModelWeightBatchRepository,
            fixture.IntradayRepository,
            new ReferenceDataIntegrityService(fixture.IntradayRepository, fixture.Clock),
            new FixedClock(producedAt));

        var batch = await generator.CreateFakeBatchAsync(ingestion.ModelWeightBatchRequest!, CancellationToken.None);
        var promotion = await promotionService.PromoteBatchAsync(batch.Id, CancellationToken.None);
        var state = await fixture.IntradayRepository.LoadStateAsync(CancellationToken.None);
        var targetWeights = state.TargetWeights.Where(x => x.ModelRunId == promotion.ModelRunId).ToList();
        var persistence = new QubesWeightPersistenceService(fixture.QubesWeightAuditRepository, new FixedClock(producedAt));

        var result = await persistence.PersistAsync(new PersistQubesWeightsRequest(ingestion, batch, promotion, targetWeights), CancellationToken.None);
        var audit = await fixture.QubesWeightAuditRepository.GetByRunIdAsync("qubes-r004b-localdb", CancellationToken.None);
        var raw = await fixture.QubesWeightAuditRepository.GetRawRowsAsync(audit!.Id, CancellationToken.None);
        var normalized = await fixture.QubesWeightAuditRepository.GetNormalizedRowsAsync(audit.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(promotion.Succeeded);
        Assert.Equal(ModelWeightSourceSystem.Qubes, audit.SourceSystem);
        Assert.Equal(15, audit.CadenceMinutes);
        Assert.Equal(batch.Id, audit.ModelWeightBatchId);
        Assert.Equal(promotion.ModelRunId, audit.PromotedModelRunId);
        Assert.Single(raw, x => x.BloombergTicker == "EURUSD Curncy" && x.Weight == 0.10m);
        Assert.Single(normalized, x => x.NormalizedTicker == "EURUSD Curncy" && x.ModelWeightBatchId == batch.Id && x.ModelRunId == promotion.ModelRunId && x.TargetWeightInstrumentId == targetWeights.Single().InstrumentId);
    }

    private static async Task<bool> IsLocalDbAvailableAsync()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("sqllocaldb", "info") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true });
            process?.WaitForExit(3000);
            if (process?.ExitCode != 0)
            {
                return false;
            }

            await using var connection = new SqlConnection("Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True");
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task AddSampleModelRunAsync(LocalDbFixture fixture)
    {
        var state = await fixture.IntradayRepository.LoadStateAsync(CancellationToken.None);
        var run = new ModelRun(ModelRunId.New(), state.Funds.Single().Id, "IntradayFxModel", Now, Now, Now, 15, 1_000_000m, ModelRunStatus.Received, Guid.NewGuid().ToString("N"), "test", false);
        var weight = new TargetWeight(run.Id, EurUsd(state).Id, -0.10m, "EURUSD");
        await fixture.IntradayRepository.AddModelRunAsync(run, [weight], CancellationToken.None);
    }

    private static Instrument EurUsd(PlatformState state)
        => state.Instruments.Single(x => x.Symbol == "EURUSD" && x.AssetClass == AssetClass.FxSpot);

    private static Venue Lmax(PlatformState state)
        => state.Venues.Single(x => x.Name == "LMAX");

    private sealed class LocalDbFixture : IAsyncDisposable
    {
        private readonly IntradayDbContext _dbContext;
        public FixedClock Clock { get; }
        public LocalDatabaseInitializer Initializer { get; }
        public SqlServerIntradayRepository IntradayRepository { get; }
        public SqlServerMarketDataSnapshotRepository SnapshotRepository { get; }
        public SqlServerMarketDataBarRepository BarRepository { get; }
        public SqlServerModelWeightBatchRepository ModelWeightBatchRepository { get; }
        public SqlServerQubesWeightAuditRepository QubesWeightAuditRepository { get; }
        public SqlServerFakeBrokerPositionProvider BrokerPositionProvider { get; }
        public BarBuilderService BarBuilder { get; }

        private LocalDbFixture(IntradayDbContext dbContext, FixedClock clock)
        {
            _dbContext = dbContext;
            Clock = clock;
            Initializer = new LocalDatabaseInitializer(dbContext, clock);
            IntradayRepository = new SqlServerIntradayRepository(dbContext);
            SnapshotRepository = new SqlServerMarketDataSnapshotRepository(dbContext);
            BarRepository = new SqlServerMarketDataBarRepository(dbContext);
            ModelWeightBatchRepository = new SqlServerModelWeightBatchRepository(dbContext);
            QubesWeightAuditRepository = new SqlServerQubesWeightAuditRepository(dbContext);
            var buildRunRepository = new SqlServerBarBuildRunRepository(dbContext, clock);
            BrokerPositionProvider = new SqlServerFakeBrokerPositionProvider(dbContext, clock);
            BarBuilder = new BarBuilderService(SeedData.Create(Now), SnapshotRepository, BarRepository, buildRunRepository, clock, new BarBuilderOptions { FifteenMinuteMinimumObservationCount = 3 });
        }

        public static async Task<LocalDbFixture> CreateAsync()
        {
            var databaseName = $"QQProductionIntraday_Test_{Guid.NewGuid():N}";
            var options = new DbContextOptionsBuilder<IntradayDbContext>()
                .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
                .Options;
            var dbContext = new IntradayDbContext(options);
            var fixture = new LocalDbFixture(dbContext, new FixedClock(Now));
            await fixture.Initializer.ApplyMigrationsAsync(CancellationToken.None);
            await fixture.Initializer.SeedReferenceDataAsync(CancellationToken.None);
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await _dbContext.Database.EnsureDeletedAsync();
            await _dbContext.DisposeAsync();
        }
    }
}
