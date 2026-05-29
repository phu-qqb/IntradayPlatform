using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesWeightPersistenceTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);

    [Fact]
    public async Task Valid_qubes_raw_fixture_persists_a_qubes_audit_batch()
    {
        var services = CreateServices();
        var ingestion = Parse(SampleLines(), "qubes-r004b-sample");
        var batch = await services.Generator.CreateFakeBatchAsync(ingestion.ModelWeightBatchRequest!, CancellationToken.None);

        var result = await services.Persistence.PersistAsync(new PersistQubesWeightsRequest(ingestion, batch, null, []), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Persisted);
        Assert.Equal("qubes-r004b-sample", result.AuditBatch.QubesRunId);
        Assert.Equal(ModelWeightSourceSystem.Qubes, result.AuditBatch.SourceSystem);
        Assert.Equal(17, result.AuditBatch.RawRowCount);
        Assert.Equal(13, result.AuditBatch.NormalizedRowCount);
        Assert.Equal(batch.Id, result.AuditBatch.ModelWeightBatchId);
    }

    [Fact]
    public async Task Raw_input_rows_are_persisted_and_retrievable()
    {
        var services = CreateServices();
        var ingestion = Parse(["NZDUSD Curncy;-0.119864", "EURGBP Curncy;0.094338"], "qubes-r004b-raw");
        var batch = await services.Generator.CreateFakeBatchAsync(ingestion.ModelWeightBatchRequest!, CancellationToken.None);

        var result = await services.Persistence.PersistAsync(new PersistQubesWeightsRequest(ingestion, batch, null, []), CancellationToken.None);
        var rawRows = await services.AuditRepository.GetRawRowsAsync(result!.AuditBatch.Id, CancellationToken.None);

        Assert.Equal(2, rawRows.Count);
        Assert.Contains(rawRows, x => x.RowNumber == 1 && x.BloombergTicker == "NZDUSD Curncy" && x.BaseCurrency == "NZD" && x.QuoteCurrency == "USD" && x.Weight == -0.119864m);
        Assert.Contains(rawRows, x => x.RowNumber == 2 && x.BloombergTicker == "EURGBP Curncy" && x.BaseCurrency == "EUR" && x.QuoteCurrency == "GBP" && x.Weight == 0.094338m);
    }

    [Fact]
    public async Task Normalized_usd_quote_weights_are_persisted()
    {
        var services = CreateServices();
        var ingestion = Parse(["EURGBP Curncy;0.25", "GBPUSD Curncy;0.10"], "qubes-r004b-normalized");
        var batch = await services.Generator.CreateFakeBatchAsync(ingestion.ModelWeightBatchRequest!, CancellationToken.None);

        var result = await services.Persistence.PersistAsync(new PersistQubesWeightsRequest(ingestion, batch, null, []), CancellationToken.None);
        var normalized = await services.AuditRepository.GetNormalizedRowsAsync(result!.AuditBatch.Id, CancellationToken.None);

        Assert.Contains(normalized, x => x.NormalizedTicker == "EURUSD Curncy" && x.Symbol == "EURUSD" && x.Currency == "EUR" && x.Weight == 0.25m);
        Assert.Contains(normalized, x => x.NormalizedTicker == "GBPUSD Curncy" && x.Symbol == "GBPUSD" && x.Currency == "GBP" && x.Weight == -0.15m);
        Assert.DoesNotContain(normalized, x => x.Symbol == "USDUSD");
    }

    [Fact]
    public async Task Qubes_run_id_links_raw_rows_to_normalized_rows()
    {
        var services = CreateServices();
        var ingestion = Parse(["EURGBP Curncy;0.25", "GBPUSD Curncy;0.10"], "qubes-r004b-lineage");
        var batch = await services.Generator.CreateFakeBatchAsync(ingestion.ModelWeightBatchRequest!, CancellationToken.None);

        await services.Persistence.PersistAsync(new PersistQubesWeightsRequest(ingestion, batch, null, []), CancellationToken.None);
        var audit = await services.AuditRepository.GetByRunIdAsync("qubes-r004b-lineage", CancellationToken.None);
        var raw = await services.AuditRepository.GetRawRowsAsync(audit!.Id, CancellationToken.None);
        var normalized = await services.AuditRepository.GetNormalizedRowsAsync(audit.Id, CancellationToken.None);

        Assert.Equal("qubes-r004b-lineage", audit.QubesRunId);
        Assert.All(raw, x => Assert.Equal(audit.Id, x.AuditBatchId));
        Assert.All(normalized, x => Assert.Equal(audit.Id, x.AuditBatchId));
    }

    [Fact]
    public async Task Normalized_rows_link_to_modelweightbatch_modelrun_and_targetweight_after_promotion()
    {
        var services = CreateServices();
        var ingestion = Parse(["EURUSD Curncy;0.10"], "qubes-r004b-promoted");
        var batch = await services.Generator.CreateFakeBatchAsync(ingestion.ModelWeightBatchRequest!, CancellationToken.None);
        var promotion = await services.Promotion.PromoteBatchAsync(batch.Id, CancellationToken.None);
        var targetWeights = services.State.TargetWeights.Where(x => x.ModelRunId == promotion.ModelRunId).ToList();

        var result = await services.Persistence.PersistAsync(new PersistQubesWeightsRequest(ingestion, batch, promotion, targetWeights), CancellationToken.None);
        var normalized = Assert.Single(result!.NormalizedRows);

        Assert.True(promotion.Succeeded);
        Assert.Equal(batch.Id, normalized.ModelWeightBatchId);
        Assert.Equal(promotion.ModelRunId, normalized.ModelRunId);
        Assert.Equal(targetWeights.Single().InstrumentId, normalized.TargetWeightInstrumentId);
        Assert.Equal("Promoted", normalized.PromotionStatus);
        Assert.Empty(services.State.ParentOrders);
        Assert.Empty(services.State.TradeIntents);
    }

    [Fact]
    public async Task Cadence_fifteen_minutes_is_persisted_and_validated()
    {
        var services = CreateServices();
        var ingestion = Parse(["EURUSD Curncy;0.10"], "qubes-r004b-cadence");
        var batch = await services.Generator.CreateFakeBatchAsync(ingestion.ModelWeightBatchRequest!, CancellationToken.None);

        var result = await services.Persistence.PersistAsync(new PersistQubesWeightsRequest(ingestion, batch, null, []), CancellationToken.None);

        Assert.Equal(15, result!.AuditBatch.CadenceMinutes);
        Assert.Equal(15, batch.FrequencyMinutes);
    }

    [Fact]
    public async Task Missing_run_id_does_not_persist_valid_batch()
    {
        var services = CreateServices();
        var ingestion = Parse(["EURUSD Curncy;0.10"], "");

        var result = await services.Persistence.PersistAsync(new PersistQubesWeightsRequest(ingestion, null, null, []), CancellationToken.None);

        Assert.Null(result);
        Assert.Null(await services.AuditRepository.GetByRunIdAsync("", CancellationToken.None));
    }

    [Fact]
    public async Task Malformed_ticker_does_not_persist_promoted_target_weights()
    {
        var services = CreateServices();
        var ingestion = Parse(["EURGBP Equity;0.10"], "qubes-r004b-malformed");

        var result = await services.Persistence.PersistAsync(new PersistQubesWeightsRequest(ingestion, null, null, []), CancellationToken.None);

        Assert.Null(result);
        Assert.DoesNotContain(services.State.TargetWeights, x => x.RawSecurityId.Contains("Equity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Duplicate_or_repeated_ingestion_is_idempotent()
    {
        var services = CreateServices();
        var ingestion = Parse(["EURUSD Curncy;0.10"], "qubes-r004b-duplicate");
        var batch = await services.Generator.CreateFakeBatchAsync(ingestion.ModelWeightBatchRequest!, CancellationToken.None);

        var first = await services.Persistence.PersistAsync(new PersistQubesWeightsRequest(ingestion, batch, null, []), CancellationToken.None);
        var second = await services.Persistence.PersistAsync(new PersistQubesWeightsRequest(ingestion, batch, null, []), CancellationToken.None);
        var raw = await services.AuditRepository.GetRawRowsAsync(first!.AuditBatch.Id, CancellationToken.None);

        Assert.True(first.Persisted);
        Assert.True(second!.AlreadyPersisted);
        Assert.Equal(first.AuditBatch.Id, second.AuditBatch.Id);
        Assert.Single(raw);
    }

    [Fact]
    public async Task Audusd_and_usdjpy_live_validation_gaps_do_not_block_persistence()
    {
        var services = CreateServices();
        var ingestion = Parse(["AUDUSD Curncy;0.10", "USDJPY Curncy;0.20"], "qubes-r004b-gaps");
        var batch = await services.Generator.CreateFakeBatchAsync(ingestion.ModelWeightBatchRequest!, CancellationToken.None);

        var result = await services.Persistence.PersistAsync(new PersistQubesWeightsRequest(ingestion, batch, null, []), CancellationToken.None);
        var normalized = await services.AuditRepository.GetNormalizedRowsAsync(result!.AuditBatch.Id, CancellationToken.None);

        Assert.Contains(normalized, x => x.Symbol == "AUDUSD");
        Assert.Contains(normalized, x => x.Symbol == "JPYUSD");
    }

    [Fact]
    public void Usdjpy_caveat_remains_preserved()
    {
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var usdjpy = universe.Single(x => x.InternalInstrumentKey == "USDJPY");
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.Equal("4004", usdjpy.SecurityId);
        Assert.Equal("8", usdjpy.SecurityIdSource);
        Assert.Equal(ApprovedInstrumentValidationStatus.NotProvenNotFailed, usdjpy.ValidationStatus);
        Assert.Equal(ApprovedInstrumentValidationStatus.PausedTlsBoundaryInconclusiveNotFailed, audusd.ValidationStatus);
    }

    [Fact]
    public void No_order_trading_or_broker_path_is_introduced()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesWeightPersistence.cs"));

        Assert.DoesNotContain("SendOrderAsync", source);
        Assert.DoesNotContain("SubmitOrder", source);
        Assert.DoesNotContain("TcpClient", source);
        Assert.DoesNotContain("SslStream", source);
        Assert.DoesNotContain("MarketDataRequest", source);
        Assert.DoesNotContain("MarketDataResponse", source);
    }

    private static QubesFxWeightsIngestionResult Parse(IReadOnlyList<string> rows, string runId)
        => new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId(runId),
            ProducedAt,
            EffectiveAt,
            15,
            "QQ_MASTER",
            "IntradayFxModel",
            1_000_000m,
            TargetQuantityMode.PortfolioBaseCurrencyNotional,
            rows));

    private static IReadOnlyList<string> SampleLines()
        => File.ReadAllLines(Path.Combine(RepoRoot(), "tests/fixtures/qubes-fx/qubes-fx-weights-r002-sample.csv"));

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var clock = new FixedClock(ProducedAt);
        var intradayRepository = new InMemoryIntradayRepository(state);
        var batchRepository = new InMemoryModelWeightBatchRepository(state);
        var auditRepository = new InMemoryQubesWeightAuditRepository();
        var integrity = new ReferenceDataIntegrityService(intradayRepository, clock);
        return new TestServices(
            state,
            new FakeModelWeightGenerator(batchRepository, clock),
            new ModelWeightPromotionService(batchRepository, intradayRepository, integrity, clock),
            auditRepository,
            new QubesWeightPersistenceService(auditRepository, clock));
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be found.");
    }

    private sealed record TestServices(
        PlatformState State,
        IFakeModelWeightGenerator Generator,
        IModelWeightPromotionService Promotion,
        InMemoryQubesWeightAuditRepository AuditRepository,
        QubesWeightPersistenceService Persistence);
}
