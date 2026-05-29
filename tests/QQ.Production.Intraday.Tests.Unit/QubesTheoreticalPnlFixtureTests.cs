using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesTheoreticalPnlFixtureTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public void R003_theoretical_portfolio_can_be_marked_using_no_external_mark_fixture()
    {
        var result = CreatePnlResult();

        Assert.True(result.UsedNoExternalMarkFixture);
        Assert.Equal(13, result.MarkedPortfolioSnapshot.Positions.Count);
        Assert.Contains(result.InstrumentDetails, x => x.Symbol == "EURUSD" && x.PnLStatus == PnLComputationStatus.Computed);
        Assert.Contains(result.InstrumentDetails, x => x.Symbol == "GBPUSD" && x.PnLStatus == PnLComputationStatus.Computed);
        Assert.Contains(result.InstrumentDetails, x => x.Symbol == "AUDUSD" && x.PnLStatus == PnLComputationStatus.Computed);
    }

    [Fact]
    public void Qubes_metadata_is_preserved_into_marked_and_pnl_snapshots()
    {
        var result = CreatePnlResult("qubes-r004-run-001");

        Assert.Equal("qubes-r004-run-001", result.QubesRunId.Value);
        Assert.Equal(ModelWeightSourceSystem.Qubes, result.SourceSystem);
        Assert.Equal(ProducedAt, result.ProducedAtUtc);
        Assert.Equal(EffectiveAt, result.EffectiveAtUtc);
        Assert.Equal(15, result.CadenceMinutes);
        Assert.Equal(result.QubesRunId, result.MarkedPortfolioSnapshot.QubesRunId);
        Assert.Equal(result.SourceSystem, result.MarkedPortfolioSnapshot.SourceSystem);
        Assert.Equal(result.CadenceMinutes, result.MarkedPortfolioSnapshot.CadenceMinutes);
    }

    [Fact]
    public void Theoretical_pnl_snapshot_is_produced_without_broker_calls()
    {
        var result = CreatePnlResult();

        Assert.Equal(PnLSource.Theoretical, result.TheoreticalPnLSnapshot.Source);
        Assert.False(result.UsedLiveBrokerMarketData);
        Assert.False(result.CalledBrokerGateway);
        Assert.False(result.CreatedExecutableOrder);
        Assert.NotEmpty(result.TheoreticalPnLSnapshot.Instruments);
    }

    [Fact]
    public void Mark_fixture_is_explicitly_no_external()
    {
        var r003 = CreateR003Diff();
        var fixtures = FixtureMarks(r003);

        Assert.All(fixtures.Previous.Concat(fixtures.Current), mark =>
        {
            Assert.True(mark.IsNoExternalFixture);
            Assert.Equal("NoExternalR004Fixture", mark.FixtureSource);
        });
    }

    [Fact]
    public void Missing_mark_produces_safe_missingmark_status()
    {
        var result = CreatePnlResult();
        var missing = result.InstrumentDetails.Single(x => x.Symbol == "JPYUSD");

        Assert.Equal(MarkAvailabilityStatus.MissingCurrentMark, missing.MarkAvailabilityStatus);
        Assert.Equal(PnLComputationStatus.MissingMark, missing.PnLStatus);
        Assert.Equal(0m, missing.UnrealizedPnL);
        Assert.Null(missing.MarkedNotionalExposure);
    }

    [Fact]
    public void Stale_mark_produces_safe_stalemark_status()
    {
        var result = CreatePnlResult();
        var stale = result.InstrumentDetails.Single(x => x.Symbol == "NOKUSD");

        Assert.Equal(MarkAvailabilityStatus.StaleCurrentMark, stale.MarkAvailabilityStatus);
        Assert.Equal(PnLComputationStatus.StaleMark, stale.PnLStatus);
        Assert.Equal(0m, stale.UnrealizedPnL);
    }

    [Fact]
    public void Portfolio_level_pnl_aggregates_computed_instrument_pnl_only()
    {
        var result = CreatePnlResult();

        Assert.Equal(6804.66m, Math.Round(result.TheoreticalPnLSnapshot.PortfolioPnL.UnrealizedPnL, 2));
        Assert.Equal(6804.66m, Math.Round(result.TheoreticalPnLSnapshot.PortfolioPnL.TotalPnL, 2));
        Assert.Equal(0m, result.TheoreticalPnLSnapshot.PortfolioPnL.RealizedPnL);
        Assert.Equal(PnLComputationStatus.MissingMark, result.TheoreticalPnLSnapshot.Status);
    }

    [Fact]
    public void No_raw_broker_payloads_are_serialized()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesTheoreticalPnlFixture.cs"));

        Assert.DoesNotContain("RawFix", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MDReqID", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SenderCompID", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TargetCompID", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BrokerPayload", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void No_live_lmax_data_is_requested()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesTheoreticalPnlFixture.cs"));

        Assert.DoesNotContain("Lmax", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MarketDataRequest", source);
        Assert.DoesNotContain("MarketDataResponse", source);
        Assert.DoesNotContain("TcpClient", source);
        Assert.DoesNotContain("SslStream", source);
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
    public void No_executable_order_is_created()
    {
        var result = CreatePnlResult();

        Assert.False(result.CreatedExecutableOrder);
        Assert.True(result.RebalanceIntentsRemainNonExecutable);
    }

    [Fact]
    public void Rebalance_intents_remain_non_executable()
    {
        var r003 = CreateR003Diff();

        Assert.NotEmpty(r003.RebalanceIntents);
        Assert.DoesNotContain(r003.RebalanceIntents, x => x.IsExecutable);
        Assert.All(r003.RebalanceIntents, x => Assert.Contains(IntentStatus.BlockedNoOMS, x.IntentStatuses));
    }

    [Fact]
    public void Audusd_live_validation_gap_does_not_block_fixture_pnl()
    {
        var result = CreatePnlResult();
        var audusd = result.InstrumentDetails.Single(x => x.Symbol == "AUDUSD");

        Assert.Equal(PnLComputationStatus.Computed, audusd.PnLStatus);
        Assert.Equal(MarkAvailabilityStatus.Available, audusd.MarkAvailabilityStatus);
        Assert.False(result.UsedLiveBrokerMarketData);
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

    private static QubesTheoreticalPnlFixtureResult CreatePnlResult(string runId = "qubes-r004-sample")
    {
        var r003 = CreateR003Diff(runId);
        var fixtures = FixtureMarks(r003);
        return new QubesTheoreticalPnlFixtureService().MarkAndCompute(new QubesTheoreticalPnlFixtureRequest(
            r003,
            fixtures.Previous,
            fixtures.Current,
            EffectiveAt,
            TimeSpan.FromMinutes(20)));
    }

    private static QubesTheoreticalPortfolioDiffResult CreateR003Diff(string runId = "qubes-r004-sample")
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
        var ids = qubes.NormalizedWeights.ToDictionary(x => x.Symbol, _ => InstrumentId.New(), StringComparer.OrdinalIgnoreCase);
        return new QubesTheoreticalPortfolioDiffService().CreateDiff(new QubesTheoreticalPortfolioDiffRequest(
            qubes,
            ids,
            CurrentPortfolioFixtureFactory.CreateFlat(EffectiveAt, PortfolioNotional),
            PortfolioNotional,
            EffectiveAt,
            0.0000000001m,
            1m,
            1));
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
        => new(instrumentId, symbol, timestamp, mid, "NoExternalR004Fixture", true, staleness);

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be found.");
    }
}
