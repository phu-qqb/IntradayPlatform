using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;
using DomainTargetWeight = QQ.Production.Intraday.Domain.TargetWeight;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesReconciliationComparatorFixtureTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task Persisted_qubes_target_weights_feed_reconciliation()
    {
        var result = await CreateResultAsync();

        Assert.True(result.UsedPersistedQubesLineage);
        Assert.NotEmpty(result.RawAuditRows);
        Assert.NotEmpty(result.NormalizedAuditRows);
        Assert.NotEmpty(result.ReconciliationLines);
        Assert.Equal(result.NormalizedOutputRowCount, result.NormalizedAuditRows.Count);
    }

    [Fact]
    public async Task Qubes_run_id_lineage_is_preserved_into_reports()
    {
        var result = await CreateResultAsync("qubes-r005-lineage");

        Assert.Equal("qubes-r005-lineage", result.QubesRunId.Value);
        Assert.Equal("qubes-r005-lineage", result.AuditBatch.QubesRunId);
        Assert.Equal(ModelWeightSourceSystem.Qubes, result.SourceSystem);
        Assert.Equal(ProducedAt, result.ProducedAtUtc);
        Assert.Equal(EffectiveAt, result.EffectiveAtUtc);
        Assert.Equal(15, result.CadenceMinutes);
    }

    [Fact]
    public async Task Modelweightbatch_modelrun_targetweight_linkage_is_preserved()
    {
        var result = await CreateResultAsync();

        Assert.NotNull(result.AuditBatch.ModelWeightBatchId);
        Assert.NotNull(result.AuditBatch.PromotedModelRunId);
        Assert.Contains(result.NormalizedAuditRows, x => x.ModelWeightBatchId == result.AuditBatch.ModelWeightBatchId);
        Assert.Contains(result.NormalizedAuditRows, x => x.ModelRunId == result.AuditBatch.PromotedModelRunId);
        Assert.Contains(result.NormalizedAuditRows, x => x.TargetWeightInstrumentId is not null);
    }

    [Fact]
    public async Task R003_target_portfolio_feeds_reconciliation()
    {
        var result = await CreateResultAsync();

        Assert.Equal(PortfolioStateSource.Theoretical, result.TheoreticalTargetPortfolioSnapshot.StateSource);
        Assert.Equal(13, result.TheoreticalTargetPortfolioSnapshot.Positions.Count);
        Assert.Contains(result.ReconciliationLines, x => x.Symbol == "AUDUSD" && x.Status == QubesReconciliationLineStatus.InSync);
    }

    [Fact]
    public async Task R004_theoretical_pnl_feeds_theoretical_vs_real_comparator()
    {
        var result = await CreateResultAsync();

        Assert.Equal(PnLSource.Theoretical, result.TheoreticalPnLSnapshot.Source);
        Assert.Equal(6804.66m, Math.Round(result.TheoreticalPnLSnapshot.PortfolioPnL.UnrealizedPnL, 2));
        Assert.NotEmpty(result.ComparatorLines);
        Assert.Contains(result.ComparatorLines, x => x.Symbol == "EURUSD" && x.PnLDifference == 100m);
    }

    [Fact]
    public async Task Actual_portfolio_fixture_is_no_external_and_not_broker_reported()
    {
        var result = await CreateResultAsync();

        Assert.True(result.UsedNoExternalActualPortfolioFixture);
        Assert.False(result.ActualFixtureIsBrokerReportedLiveState);
        Assert.Equal(PortfolioStateSource.Simulated, result.ActualPortfolioFixture.StateSource);
    }

    [Fact]
    public async Task Reconciliation_detects_overweight_drift()
    {
        var result = await CreateResultAsync();
        var eurusd = result.ReconciliationLines.Single(x => x.Symbol == "EURUSD");

        Assert.Equal(QubesReconciliationLineStatus.Drift, eurusd.Status);
        Assert.True(eurusd.WeightDifference > 0m);
        Assert.True(eurusd.NotionalDifference > 0m);
    }

    [Fact]
    public async Task Reconciliation_detects_underweight_drift()
    {
        var result = await CreateResultAsync();
        var gbpusd = result.ReconciliationLines.Single(x => x.Symbol == "GBPUSD");

        Assert.Equal(QubesReconciliationLineStatus.Drift, gbpusd.Status);
        Assert.True(gbpusd.WeightDifference < 0m);
        Assert.True(gbpusd.NotionalDifference < 0m);
    }

    [Fact]
    public async Task Reconciliation_detects_missing_actual_position()
    {
        var result = await CreateResultAsync();
        var jpyusd = result.ReconciliationLines.Single(x => x.Symbol == "JPYUSD");

        Assert.Equal(QubesReconciliationLineStatus.MissingActual, jpyusd.Status);
        Assert.Equal(ReconciliationSeverity.Blocking, jpyusd.Severity);
    }

    [Fact]
    public async Task Reconciliation_handles_missing_and_stale_marks_safely()
    {
        var result = await CreateResultAsync();

        Assert.Contains(result.ReconciliationLines, x => x.Symbol == "CADUSD" && x.Status == QubesReconciliationLineStatus.MissingMark);
        Assert.Contains(result.ReconciliationLines, x => x.Symbol == "NOKUSD" && x.Status == QubesReconciliationLineStatus.MissingMark);
        Assert.Contains(result.ComparatorLines, x => x.Symbol == "NOKUSD" && x.Status == QubesReconciliationLineStatus.MissingMark);
    }

    [Fact]
    public async Task Theoretical_vs_real_comparator_computes_pnl_difference()
    {
        var result = await CreateResultAsync();
        var gbpusd = result.ComparatorLines.Single(x => x.Symbol == "GBPUSD");

        Assert.Equal(-200m, gbpusd.PnLDifference);
        Assert.Equal(result.ActualPnlFixture.PortfolioPnL.TotalPnL - result.TheoreticalPnLSnapshot.PortfolioPnL.TotalPnL, result.ComparatorLines.Sum(x => x.PnLDifference));
    }

    [Fact]
    public async Task Comparator_emits_drift_when_differences_exceed_tolerance()
    {
        var result = await CreateResultAsync();

        Assert.Equal(TheoreticalVsRealStatus.Drift, result.FoundationTheoreticalVsRealReport.Status);
        Assert.Contains(result.ComparatorLines, x => x.Symbol == "EURUSD" && x.Status == QubesReconciliationLineStatus.Drift);
    }

    [Fact]
    public async Task Comparator_emits_insync_when_within_tolerance()
    {
        var result = await CreateResultAsync();

        Assert.Contains(result.ComparatorLines, x => x.Symbol == "AUDUSD" && x.Status == QubesReconciliationLineStatus.InSync);
    }

    [Fact]
    public async Task Actual_pnl_fixture_is_no_external_and_not_broker_reported_live_pnl()
    {
        var result = await CreateResultAsync();

        Assert.True(result.UsedNoExternalActualPnlFixture);
        Assert.Equal(PnLSource.Simulated, result.ActualPnlFixture.Source);
        Assert.Equal(PnLComputationStatus.MissingMark, result.ActualPnlFixture.Status);
    }

    [Fact]
    public async Task Rebalance_intents_remain_non_executable()
    {
        var result = await CreateResultAsync();

        Assert.True(result.RebalanceIntentsRemainNonExecutable);
        Assert.False(result.CreatedExecutableOrder);
    }

    [Fact]
    public void No_executable_order_or_broker_runtime_path_is_introduced()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesReconciliationComparatorFixture.cs"));

        Assert.DoesNotContain("SendOrderAsync", source);
        Assert.DoesNotContain("SubmitOrder", source);
        Assert.DoesNotContain("TcpClient", source);
        Assert.DoesNotContain("SslStream", source);
        Assert.DoesNotContain("MarketDataRequest", source);
        Assert.DoesNotContain("MarketDataResponse", source);
        Assert.DoesNotContain("FixSession", source);
    }

    [Fact]
    public async Task Audusd_and_usdjpy_live_validation_gaps_do_not_block_reconciliation()
    {
        var result = await CreateResultAsync();

        Assert.Contains(result.ReconciliationLines, x => x.Symbol == "AUDUSD");
        Assert.Contains(result.ReconciliationLines, x => x.Symbol == "JPYUSD" && x.Status == QubesReconciliationLineStatus.MissingActual);
        Assert.False(result.UsedLiveMarketData);
        Assert.False(result.CalledBrokerGateway);
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
    public void Api_and_worker_remain_fake_gateway_only()
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

    private static async Task<QubesReconciliationComparatorResult> CreateResultAsync(string runId = "qubes-r005-sample")
    {
        var pipeline = await CreatePipelineAsync(runId);
        var actualPortfolio = ActualPortfolioFixtureFactory.CreateWithDeterministicDrifts(
            pipeline.R003.TargetPortfolioSnapshot,
            pipeline.SymbolsByInstrument,
            EffectiveAt);
        var actualPnl = ActualPnlFixtureFactory.Create(
            actualPortfolio,
            pipeline.R004.TheoreticalPnLSnapshot,
            pipeline.SymbolsByInstrument,
            EffectiveAt);

        return new QubesReconciliationComparatorFixtureService().Create(new QubesReconciliationComparatorRequest(
            pipeline.R003,
            pipeline.R004,
            pipeline.Persistence,
            actualPortfolio,
            actualPnl,
            pipeline.SymbolsByInstrument,
            EffectiveAt,
            0.0001m,
            100m,
            10m));
    }

    private static async Task<TestPipeline> CreatePipelineAsync(string runId)
    {
        var qubes = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId(runId),
            ProducedAt,
            EffectiveAt,
            15,
            "QQ_MASTER",
            "IntradayFxModel",
            PortfolioNotional,
            TargetQuantityMode.PortfolioBaseCurrencyNotional,
            File.ReadAllLines(Path.Combine(RepoRoot(), "tests/fixtures/qubes-fx/qubes-fx-weights-r002-sample.csv"))));
        var state = SeedData.Create(ProducedAt);
        EnsureInstruments(state, qubes.NormalizedWeights.Select(x => x.Symbol));
        var idsBySymbol = qubes.NormalizedWeights.ToDictionary(
            x => x.Symbol,
            x => state.Instruments.Single(instrument => instrument.Symbol.Equals(x.Symbol, StringComparison.OrdinalIgnoreCase)).Id,
            StringComparer.OrdinalIgnoreCase);
        var symbolsByInstrument = idsBySymbol.ToDictionary(x => x.Value, x => x.Key);
        var batchRepository = new InMemoryModelWeightBatchRepository(state);
        var intradayRepository = new InMemoryIntradayRepository(state);
        var integrity = new ReferenceDataIntegrityService(intradayRepository, new FixedClock(ProducedAt));
        var promotion = new ModelWeightPromotionService(batchRepository, intradayRepository, integrity, new FixedClock(ProducedAt));
        var auditRepository = new InMemoryQubesWeightAuditRepository();
        var batch = await new FakeModelWeightGenerator(batchRepository, new FixedClock(ProducedAt)).CreateFakeBatchAsync(qubes.ModelWeightBatchRequest!, CancellationToken.None);
        var promoted = await promotion.PromoteBatchAsync(batch.Id, CancellationToken.None);
        Assert.True(promoted.Succeeded);
        var targetWeights = state.TargetWeights.Where(x => x.ModelRunId == promoted.ModelRunId).ToList();
        var persistence = await new QubesWeightPersistenceService(auditRepository, new FixedClock(ProducedAt))
            .PersistAsync(new PersistQubesWeightsRequest(qubes, batch, promoted, targetWeights), CancellationToken.None);
        var r003 = new QubesTheoreticalPortfolioDiffService().CreateDiff(new QubesTheoreticalPortfolioDiffRequest(
            qubes,
            idsBySymbol,
            CurrentPortfolioFixtureFactory.CreateFlat(EffectiveAt, PortfolioNotional),
            PortfolioNotional,
            EffectiveAt,
            0.0000000001m,
            1m,
            1));
        var marks = FixtureMarks(r003);
        var r004 = new QubesTheoreticalPnlFixtureService().MarkAndCompute(new QubesTheoreticalPnlFixtureRequest(
            r003,
            marks.Previous,
            marks.Current,
            EffectiveAt,
            TimeSpan.FromMinutes(20)));

        return new TestPipeline(r003, r004, persistence!, symbolsByInstrument);
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

    private static (IReadOnlyList<MarketDataMarkFixture> Previous, IReadOnlyList<MarketDataMarkFixture> Current) FixtureMarks(QubesTheoreticalPortfolioDiffResult r003)
    {
        var ids = r003.DiffLines.ToDictionary(x => x.Symbol, x => x.InstrumentId, StringComparer.OrdinalIgnoreCase);
        return (
            [
                Mark(ids["AUDUSD"], "AUDUSD", ProducedAt, 1.0000m),
                Mark(ids["EURUSD"], "EURUSD", ProducedAt, 1.1000m),
                Mark(ids["GBPUSD"], "GBPUSD", ProducedAt, 1.3000m),
                Mark(ids["JPYUSD"], "JPYUSD", ProducedAt, 0.0067m),
                Mark(ids["NOKUSD"], "NOKUSD", ProducedAt, 10.0000m)
            ],
            [
                Mark(ids["AUDUSD"], "AUDUSD", EffectiveAt, 1.0100m),
                Mark(ids["EURUSD"], "EURUSD", EffectiveAt, 1.1110m),
                Mark(ids["GBPUSD"], "GBPUSD", EffectiveAt, 1.2870m),
                Mark(ids["NOKUSD"], "NOKUSD", EffectiveAt.AddHours(-1), 10.1000m, MarketDataStalenessCategory.Stale)
            ]);
    }

    private static MarketDataMarkFixture Mark(
        InstrumentId instrumentId,
        string symbol,
        DateTimeOffset timestamp,
        decimal? mid,
        MarketDataStalenessCategory staleness = MarketDataStalenessCategory.Fresh)
        => new(instrumentId, symbol, timestamp, mid, "NoExternalR005Fixture", true, staleness);

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be found.");
    }

    private sealed record TestPipeline(
        QubesTheoreticalPortfolioDiffResult R003,
        QubesTheoreticalPnlFixtureResult R004,
        PersistQubesWeightsResult Persistence,
        IReadOnlyDictionary<InstrumentId, string> SymbolsByInstrument);
}
