using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxDemoUsdNettingTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 18, 10, 0, 0, TimeSpan.Zero);
    private static IReadOnlyDictionary<string, decimal> Net((string, decimal)[] rows, params string[] scope)
        => LmaxDemoUsdNetting.Net(rows, scope, At, At.AddMinutes(15), 1_000_000m);

    [Fact]
    public void CrossesNetByCurrencyBeforeNativeUsdOrientation()
    {
        var weights = Net([("EURGBP", .25m), ("GBPUSD", .1m), ("GBPJPY", .05m)], "EURUSD", "GBPUSD", "USDJPY");
        Assert.Equal(.25m, weights["EURUSD"]);
        Assert.Equal(-.10m, weights["GBPUSD"]);
        Assert.Equal(.05m, weights["USDJPY"]);
        Assert.Equal(3, weights.Count);
    }

    [Fact]
    public void OppositeContributionsNetToExplicitZeroTargetsWithoutRescaling()
    {
        var weights = Net([("EURGBP", .25m), ("EURGBP", -.25m)], "EURUSD", "GBPUSD", "USDJPY");
        Assert.Equal(3, weights.Count);
        Assert.All(weights.Values, value => Assert.Equal(0m, value));
    }

    [Fact]
    public void AllUsAndEuCurrenciesAreRepresentedIncludingEasternEurope()
    {
        string[] scope = ["EURUSD", "GBPUSD", "AUDUSD", "NZDUSD", "USDJPY", "USDCAD", "USDCHF",
            "USDMXN", "USDNOK", "USDSEK", "USDZAR", "USDHUF", "USDPLN", "USDRON"];
        var weights = Net(scope.Select(x => (LmaxDemoUsdNetting.Currency(x) + "USD", .1m)).ToArray(), scope);
        Assert.Equal(14, weights.Count);
        Assert.Equal(.1m, weights["NZDUSD"]);
        Assert.Equal(-.1m, weights["USDHUF"]);
        Assert.Equal(-.1m, weights["USDPLN"]);
        Assert.Equal(-.1m, weights["USDRON"]);
    }

    [Fact]
    public void MissingCurrencyCannotBeSilentlyFilteredBeforeOrAfterNetting()
    {
        var error = Assert.Throws<DomainRuleViolationException>(() => Net([("EURGBP", .25m)], "EURUSD"));
        Assert.Contains("GBP", error.Message);
        // A zero at this cutoff is not authority to erase that programme currency from the day scope.
        Assert.Throws<DomainRuleViolationException>(() => Net([("EURGBP", 0m)], "EURUSD"));
    }

    [Theory]
    [InlineData("EURGBP")]
    [InlineData("USDUSD")]
    [InlineData("eurusd")]
    public void DirectCrossOrAmbiguousScopeIsRejected(string symbol)
        => Assert.Throws<DomainRuleViolationException>(() => Net([("EURUSD", .1m)], symbol));

    [Fact]
    public void OppositeOrientationsOfSameCurrencyCannotBothBeExecuted()
        => Assert.Throws<DomainRuleViolationException>(() => Net([("JPYUSD", .1m)], "JPYUSD", "USDJPY"));

    [Fact]
    public void JpyExposureUsesUsdBaseQuantityNotJpyPriceAsUsdConversion()
    {
        var f = Fixture("USDJPY", 150m);
        var weight = new TargetWeight(f.Run.Id, f.Instrument.Id, -.1m, "USDJPY Curncy");
        var target = LmaxDemoUsdNetting.Size(f.Run, weight, f.Instrument, f.Market, f.Mapping);
        Assert.Equal(-100_000m, target.TargetBaseQuantity);
        Assert.Equal(-10m, target.TargetVenueQuantity);
        Assert.Equal(-100_000m, target.TargetNotionalUsd);
        Assert.Equal(1m, LmaxDemoUsdNetting.BaseUnitValueUsd(f.Instrument, f.Market));
    }

    [Fact]
    public void DirectUsdPairUsesPriceAndVenueQuantityStep()
    {
        var f = Fixture("EURUSD", 1.25m);
        var target = LmaxDemoUsdNetting.Size(f.Run, new(f.Run.Id, f.Instrument.Id, .1m, "EURUSD Curncy"), f.Instrument, f.Market, f.Mapping);
        Assert.Equal(80_000m, target.TargetBaseQuantity);
        Assert.Equal(8m, target.TargetVenueQuantity);
    }

    [Fact]
    public void ConflictingNativeVenueOrientationCannotReachSizing()
    {
        var f = Fixture("USDJPY", 150m);
        Assert.Throws<DomainRuleViolationException>(() => LmaxDemoUsdNetting.Size(f.Run,
            new(f.Run.Id, f.Instrument.Id, -.1m, "USDJPY Curncy"), f.Instrument, f.Market,
            f.Mapping with { VenueInstrumentCode = "JPY/USD" }));
    }

    [Fact]
    public void GrossAddsAbsoluteUsdValuesPerInstrumentRatherThanNettingUnrelatedBaseUnits()
    {
        var eur = Fixture("EURUSD", 1.25m);
        var jpy = Fixture("USDJPY", 150m);
        var state = SeedData.Create(At);
        state.Instruments.Clear(); state.Instruments.AddRange([eur.Instrument, jpy.Instrument]);
        state.MarketData.Clear(); state.MarketData.AddRange([eur.Market, jpy.Market with { VenueId = eur.Mapping.VenueId }]);
        state.PositionLedger.Clear();
        state.PositionLedger.Add(new(Guid.NewGuid(), eur.Run.FundId, eur.Instrument.Id, PositionLedgerEventType.Fill, 80_000m, "TEST", At));
        state.PositionLedger.Add(new(Guid.NewGuid(), eur.Run.FundId, jpy.Instrument.Id, PositionLedgerEventType.Fill, -100_000m, "TEST", At));
        Assert.Equal(200_000m, LmaxDemoUsdNetting.GrossExposure(state, eur.Run.FundId, eur.Mapping.VenueId, At, TimeSpan.FromMinutes(1)));
        Assert.Throws<DomainRuleViolationException>(() => LmaxDemoUsdNetting.GrossExposure(state, eur.Run.FundId, eur.Mapping.VenueId, At.AddMinutes(2), TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void LegacyUnnettedModelCannotAcquireNettedSizingSemantics()
    {
        var f = Fixture("USDJPY", 150m);
        Assert.Throws<DomainRuleViolationException>(() => LmaxDemoUsdNetting.Size(f.Run with { SourceFileName = "legacy-anubis:old" },
            new(f.Run.Id, f.Instrument.Id, .1m, "USDJPY Curncy"), f.Instrument, f.Market, f.Mapping));
    }

    [Fact]
    public void JpyTradeRiskIsComparedInUsdRatherThanInJpy()
    {
        var f = Fixture("USDJPY", 150m);
        var state = SeedData.Create(At);
        var intent = new TradeIntent(TradeIntentId.New(), f.Run.Id, f.Run.FundId, f.Instrument.Id,
            TradeSide.Sell, 100_000m, 10m, "SIMULATED_NETTED_TEST", TradeIntentStatus.Created, At);
        var context = new RiskContext(state.Funds[0], state.Venues[0], f.Instrument, f.Run, f.Market,
            0m, true, 0m, At, DemoUsdNetting: true);
        var result = new RiskEngine().EvaluateDetailed(intent, context, state.RiskLimitSets[0],
            state.InstrumentRiskLimits[0] with { InstrumentId = f.Instrument.Id }, state.VenueRiskLimits[0], state.TradingWindows[0], state.KillSwitch);
        Assert.Equal(100_000m, result.Details.Single(x => x.CheckName == "MaxTradeNotionalUsd").ObservedValue);
    }

    private static (Instrument Instrument, MarketDataSnapshot Market, VenueInstrumentMapping Mapping, ModelRun Run) Fixture(string symbol, decimal mid)
    {
        var state = SeedData.Create(At);
        var instrument = state.Instruments[0] with { Id = InstrumentId.New(), Symbol = symbol, BaseCurrency = new(symbol[..3]), QuoteCurrency = new(symbol[3..]) };
        var mapping = state.VenueInstrumentMappings[0] with { InstrumentId = instrument.Id, VenueSymbol = symbol,
            VenueInstrumentCode = symbol.Insert(3, "/"), ContractSize = 10000m, QuantityStep = .1m, MinOrderQuantity = .1m };
        var market = state.MarketData[0] with { InstrumentId = instrument.Id, Bid = mid - .0001m, Ask = mid + .0001m, ExplicitMid = null };
        var run = state.ModelRuns[0] with { NavUsd = 1_000_000m, SourceFileName = "legacy-anubis:" + LmaxDemoUsdNetting.BatchPrefix + "test" };
        return (instrument, market, mapping, run);
    }
}
