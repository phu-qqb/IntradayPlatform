using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesTheoreticalPortfolioDiffTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private static readonly DateTimeOffset CreatedAt = EffectiveAt;
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public void R002_normalized_qubes_weights_can_be_consumed_by_r003_diff_pipeline()
    {
        var qubes = Parse(SampleLines());
        var result = CreateDiff(qubes);

        Assert.Equal(13, result.TargetPortfolioSnapshot.Positions.Count);
        Assert.Equal(13, result.DiffLines.Count);
        Assert.Equal(13, result.RebalanceIntents.Count);
        Assert.True(result.UsesExistingModelWeightBatchMapping);
    }

    [Fact]
    public void Qubes_run_id_source_and_cadence_metadata_are_preserved()
    {
        var qubes = Parse(["EURUSD Curncy;0.10"], runId: "qubes-r003-run-001");
        var result = CreateDiff(qubes);

        Assert.Equal("qubes-r003-run-001", result.QubesRunId.Value);
        Assert.Equal(ModelWeightSourceSystem.Qubes, result.SourceSystem);
        Assert.Equal(ProducedAt, result.ProducedAtUtc);
        Assert.Equal(EffectiveAt, result.EffectiveAtUtc);
        Assert.Equal(15, result.CadenceMinutes);
        Assert.Equal(1, result.RawInputRowCount);
        Assert.Equal(1, result.NormalizedOutputRowCount);
    }

    [Fact]
    public void Target_portfolio_snapshot_is_created_from_normalized_usd_quote_weights()
    {
        var qubes = Parse(["EURUSD Curncy;0.10", "GBPUSD Curncy;-0.25"]);
        var result = CreateDiff(qubes);

        Assert.Equal(PortfolioStateSource.Theoretical, result.TargetPortfolioSnapshot.StateSource);
        Assert.Equal(PortfolioNotional, result.TargetPortfolioSnapshot.PortfolioNotional);
        Assert.Contains(result.DiffLines, x => x.Symbol == "EURUSD" && x.TargetWeight == 0.10m && x.TargetNotional == 100_000m);
        Assert.Contains(result.DiffLines, x => x.Symbol == "GBPUSD" && x.TargetWeight == -0.25m && x.TargetNotional == -250_000m);
    }

    [Fact]
    public void Current_portfolio_fixture_is_used_not_live_broker_state()
    {
        var result = CreateDiff(Parse(["EURUSD Curncy;0.10"]));

        Assert.True(result.UsedCurrentPortfolioFixture);
        Assert.False(result.UsedLiveBrokerState);
        Assert.Equal(PortfolioStateSource.Simulated, result.CurrentPortfolioSnapshot.StateSource);
        Assert.Empty(result.CurrentPortfolioSnapshot.Positions);
        Assert.Contains(result.CurrentPortfolioSnapshot.CashComponents, x => x.Currency == "USD" && x.Amount == PortfolioNotional);
    }

    [Fact]
    public void Flat_current_portfolio_fixture_produces_delta_equal_to_target_weight()
    {
        var result = CreateDiff(Parse(["EURUSD Curncy;0.10"]));
        var diff = Assert.Single(result.DiffLines);

        Assert.Equal(0m, diff.CurrentWeight);
        Assert.Equal(0.10m, diff.TargetWeight);
        Assert.Equal(0.10m, diff.DeltaWeight);
        Assert.Equal(0m, diff.CurrentNotional);
        Assert.Equal(100_000m, diff.TargetNotional);
        Assert.Equal(100_000m, diff.DeltaNotional);
        Assert.Equal(TheoreticalPortfolioDiffCategory.EnterTarget, diff.Category);
    }

    [Fact]
    public void Existing_current_portfolio_fixture_produces_correct_current_vs_target_delta()
    {
        var qubes = Parse(["EURUSD Curncy;0.10"]);
        var ids = InstrumentIdsBySymbol(qubes);
        var instrumentId = ids["EURUSD"];
        var current = new PortfolioSnapshot(
            CreatedAt,
            PortfolioStateSource.Simulated,
            PortfolioNotional,
            [new PortfolioPosition(instrumentId, 0m, null, 40_000m, 0.04m, new Dictionary<string, decimal> { ["USD"] = 40_000m })],
            [new CashComponent("USD", 960_000m)]);

        var result = CreateDiff(qubes, current, ids);
        var diff = Assert.Single(result.DiffLines);

        Assert.Equal(0.04m, diff.CurrentWeight);
        Assert.Equal(0.10m, diff.TargetWeight);
        Assert.Equal(0.06m, diff.DeltaWeight);
        Assert.Equal(60_000m, diff.DeltaNotional);
        Assert.Equal(TheoreticalPortfolioDiffCategory.IncreaseTarget, diff.Category);
    }

    [Fact]
    public void Non_executable_rebalance_intents_are_generated()
    {
        var result = CreateDiff(Parse(["EURUSD Curncy;0.10"]));
        var intent = Assert.Single(result.RebalanceIntents);

        Assert.False(intent.IsExecutable);
        Assert.Equal("EURUSD", intent.Symbol);
        Assert.Equal(0.10m, intent.DeltaWeight);
        Assert.Equal(100_000m, intent.DeltaNotional);
        Assert.False(result.ExistingNonExecutableIntentEnvelope.IsExecutable);
        Assert.False(result.CreatedExecutableOrder);
        Assert.False(result.CalledBrokerGateway);
    }

    [Fact]
    public void Intent_side_is_buy_sell_or_none_based_on_delta_sign()
    {
        var qubes = Parse(["EURUSD Curncy;0.10", "GBPUSD Curncy;-0.25", "AUDUSD Curncy;0.05"]);
        var ids = InstrumentIdsBySymbol(qubes);
        var current = new PortfolioSnapshot(
            CreatedAt,
            PortfolioStateSource.Simulated,
            PortfolioNotional,
            [
                new PortfolioPosition(ids["AUDUSD"], 0m, null, 50_000m, 0.05m, new Dictionary<string, decimal> { ["USD"] = 50_000m })
            ],
            []);

        var result = CreateDiff(qubes, current, ids);

        Assert.Equal(IntentSide.Buy, result.RebalanceIntents.Single(x => x.Symbol == "EURUSD").IntentSide);
        Assert.Equal(IntentSide.Sell, result.RebalanceIntents.Single(x => x.Symbol == "GBPUSD").IntentSide);
        Assert.Equal(IntentSide.None, result.RebalanceIntents.Single(x => x.Symbol == "AUDUSD").IntentSide);
    }

    [Fact]
    public void Rebalance_intents_are_theoretical_only_not_executable_and_blocked_no_oms()
    {
        var result = CreateDiff(Parse(["EURUSD Curncy;0.10"]));
        var intent = Assert.Single(result.RebalanceIntents);

        Assert.Contains(IntentStatus.TheoreticalOnly, intent.IntentStatuses);
        Assert.Contains(IntentStatus.NotExecutable, intent.IntentStatuses);
        Assert.Contains(IntentStatus.BlockedNoOMS, intent.IntentStatuses);
        Assert.False(intent.IsExecutable);
        Assert.Equal(IntentStatus.NotExecutable, result.ExistingNonExecutableIntentEnvelope.IntentStatus);
    }

    [Fact]
    public async Task Modelweightbatch_and_targetweight_linkage_remains_existing_pipeline_only()
    {
        var services = CreateServices();
        var qubes = Parse(["EURUSD Curncy;0.10"], runId: "qubes-r003-promote");

        var batch = await services.Generator.CreateFakeBatchAsync(qubes.ModelWeightBatchRequest!, CancellationToken.None);
        var promotion = await services.Promotion.PromoteBatchAsync(batch.Id, CancellationToken.None);
        var targetWeight = Assert.Single(services.State.TargetWeights, x => x.ModelRunId == promotion.ModelRunId);
        var current = CurrentPortfolioFixtureFactory.CreateFlat(CreatedAt, PortfolioNotional);
        var result = CreateDiff(qubes, current, new Dictionary<string, InstrumentId> { ["EURUSD"] = targetWeight.InstrumentId });

        Assert.True(promotion.Succeeded);
        Assert.Equal(ModelWeightSourceSystem.Qubes, batch.SourceSystem);
        Assert.True(result.UsesExistingModelWeightBatchMapping);
        Assert.Equal(targetWeight.InstrumentId, Assert.Single(result.TargetPortfolioSnapshot.Positions).InstrumentId);
        Assert.Empty(services.State.TradeIntents);
        Assert.Empty(services.State.ParentOrders);
    }

    [Fact]
    public void No_order_submission_path_is_introduced()
    {
        var result = CreateDiff(Parse(["EURUSD Curncy;0.10"]));

        Assert.False(result.CreatedExecutableOrder);
        Assert.DoesNotContain(result.RebalanceIntents, x => x.IsExecutable);
        Assert.DoesNotContain(result.RebalanceIntents, x => x.IntentStatuses.Count == 0 || !x.IntentStatuses.Contains(IntentStatus.BlockedNoOMS));
    }

    [Fact]
    public void Diff_implementation_contains_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesTheoreticalPortfolioDiff.cs"));

        Assert.DoesNotContain("TcpClient", source);
        Assert.DoesNotContain("SslStream", source);
        Assert.DoesNotContain("RawFix", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FixSession", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MarketDataRequest", source);
        Assert.DoesNotContain("MarketDataResponse", source);
        Assert.DoesNotContain("SendOrderAsync", source);
        Assert.DoesNotContain("SubmitOrder", source);
    }

    [Fact]
    public void Audusd_and_usdjpy_live_validation_gaps_do_not_block_theoretical_diff()
    {
        var qubes = Parse(["AUDUSD Curncy;0.10", "USDJPY Curncy;0.20"]);
        var result = CreateDiff(qubes);

        Assert.Contains(result.DiffLines, x => x.Symbol == "AUDUSD");
        Assert.Contains(result.DiffLines, x => x.Symbol == "JPYUSD");
        Assert.False(result.UsedLiveBrokerState);
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
    public void No_raw_broker_payloads_or_market_prices_are_serialized()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesTheoreticalPortfolioDiff.cs"));

        Assert.DoesNotContain("BrokerPositionProvider", source);
        Assert.DoesNotContain("MarketDataSnapshot", source);
        Assert.DoesNotContain("Bid", source);
        Assert.DoesNotContain("Ask", source);
        Assert.DoesNotContain("Mid", source);
        Assert.DoesNotContain("Price", source);
    }

    [Fact]
    public void Api_and_worker_remain_fake_lmax_gateway_only()
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

    private static QubesTheoreticalPortfolioDiffResult CreateDiff(QubesFxWeightsIngestionResult qubes)
        => CreateDiff(qubes, CurrentPortfolioFixtureFactory.CreateFlat(CreatedAt, PortfolioNotional), InstrumentIdsBySymbol(qubes));

    private static QubesTheoreticalPortfolioDiffResult CreateDiff(QubesFxWeightsIngestionResult qubes, PortfolioSnapshot current)
        => CreateDiff(qubes, current, InstrumentIdsBySymbol(qubes));

    private static QubesTheoreticalPortfolioDiffResult CreateDiff(QubesFxWeightsIngestionResult qubes, PortfolioSnapshot current, IReadOnlyDictionary<string, InstrumentId> instrumentIds)
        => new QubesTheoreticalPortfolioDiffService().CreateDiff(new QubesTheoreticalPortfolioDiffRequest(
            qubes,
            instrumentIds,
            current,
            PortfolioNotional,
            CreatedAt,
            0.0000000001m,
            1m,
            1));

    private static IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol(QubesFxWeightsIngestionResult qubes)
        => qubes.NormalizedWeights.ToDictionary(x => x.Symbol, _ => InstrumentId.New(), StringComparer.OrdinalIgnoreCase);

    private static QubesFxWeightsIngestionResult Parse(IReadOnlyList<string> lines, string runId = "qubes-r003-sample")
        => new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId(runId),
            ProducedAt,
            EffectiveAt,
            15,
            "QQ_MASTER",
            "IntradayFxModel",
            PortfolioNotional,
            TargetQuantityMode.PortfolioBaseCurrencyNotional,
            lines));

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

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var clock = new FixedClock(ProducedAt);
        var intradayRepository = new InMemoryIntradayRepository(state);
        var batchRepository = new InMemoryModelWeightBatchRepository(state);
        var integrity = new ReferenceDataIntegrityService(intradayRepository, clock);
        return new TestServices(state, new FakeModelWeightGenerator(batchRepository, clock), new ModelWeightPromotionService(batchRepository, intradayRepository, integrity, clock));
    }

    private sealed record TestServices(PlatformState State, IFakeModelWeightGenerator Generator, IModelWeightPromotionService Promotion);
}
