using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxDemoGmvRiskTests
{
    private static readonly DateTimeOffset Friday = new(2026,9,18,13,37,0,TimeSpan.Zero);

    [Theory]
    [InlineData(DayOfWeek.Sunday)]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Tuesday)]
    [InlineData(DayOfWeek.Wednesday)]
    [InlineData(DayOfWeek.Thursday)]
    [InlineData(DayOfWeek.Friday)]
    [InlineData(DayOfWeek.Saturday)]
    public void ActualRejectedGbpSizeFitsAmendedPolicyOnEveryAuthorizedDay(DayOfWeek day)
    {
        var now = Friday.AddDays((int)day - (int)Friday.DayOfWeek);
        var result = Evaluate(0m, 616_000m, 0m, now, "GBPUSD", 1.334895m);
        Assert.Equal(RiskDecisionStatus.Approved, result.Decision.Status);
        Assert.Equal(822_295.32m, result.Details.Single(x => x.CheckName == "MaxTradeNotionalUsd").ObservedValue);
    }

    [Theory]
    [InlineData(0, 2000000, 0, RiskRejectReason.None)]
    [InlineData(0, 2000001, 0, RiskRejectReason.MaxInstrumentExposureExceeded)]
    [InlineData(0, -2000001, 0, RiskRejectReason.MaxInstrumentExposureExceeded)]
    [InlineData(2000000, -4000000, 10000000, RiskRejectReason.None)]
    [InlineData(-2000000, 4000000, 10000000, RiskRejectReason.None)]
    [InlineData(2000000, -500000, 10000000, RiskRejectReason.None)]
    [InlineData(1000000, 1000000, 9000000, RiskRejectReason.None)]
    [InlineData(1000000, 1000000, 9000001, RiskRejectReason.MaxGrossExposureExceeded)]
    [InlineData(0, 1000000, 9500000, RiskRejectReason.MaxGrossExposureExceeded)]
    public void NettedPositionAndPortfolioGmvAreBoundedIncludingReservedIncreases(
        decimal current, decimal signedOrder, decimal grossIncludingReserved, RiskRejectReason reason)
    {
        var result = Evaluate(current, signedOrder, grossIncludingReserved, Friday);
        Assert.Equal(reason, result.Decision.RejectReason);
        Assert.Equal(Math.Abs(current + signedOrder), result.Details.Single(x => x.CheckName == "MaxInstrumentExposureUsd").ObservedValue);
    }

    [Fact]
    public void ReversalDoesNotCreateFourMillionOfPositionExposure()
    {
        var result = Evaluate(2_000_000m, -4_000_000m, 10_000_000m, Friday);
        Assert.Equal(4_000_000m, result.Details.Single(x => x.CheckName == "MaxTradeNotionalUsd").ObservedValue);
        Assert.Equal(2_000_000m, result.Details.Single(x => x.CheckName == "MaxInstrumentExposureUsd").ObservedValue);
        Assert.Equal(10_000_000m, result.Details.Single(x => x.CheckName == "MaxGrossExposureUsd").ObservedValue);
    }

    [Fact]
    public void TradingWindowStillRejectsWrongDayAndClosedWindows()
    {
        Assert.Equal(RiskRejectReason.TradingWindowClosed, Evaluate(0,1000,0,Friday, wrongDay:true).Decision.RejectReason);
        Assert.Equal(RiskRejectReason.TradingWindowClosed, Evaluate(0,1000,0,Friday, closed:true).Decision.RejectReason);
    }

    [Fact]
    public void NonNettedRiskSemanticsAreUnchanged()
        => Assert.Equal(RiskRejectReason.MaxInstrumentExposureExceeded,
            Evaluate(2_000_000m,-500_000m,2_000_000m,Friday,"EURUSD",1m,netted:false).Decision.RejectReason);

    private static (RiskDecision Decision, IReadOnlyList<RiskDecisionDetail> Details) Evaluate(
        decimal current, decimal signedOrder, decimal gross, DateTimeOffset now, string symbol="USDJPY",
        decimal mid=150m, bool wrongDay=false, bool closed=false, bool netted=true)
    {
        var state = SeedData.Create(now);
        var instrument = state.Instruments[0] with { Symbol=symbol, BaseCurrency=new(symbol[..3]), QuoteCurrency=new(symbol[3..]) };
        var market = state.MarketData[0] with { InstrumentId=instrument.Id, Bid=mid, Ask=mid, ExplicitMid=null,
            SourceTimestampUtc=now, ReceivedAtUtc=now };
        var run = state.ModelRuns[0] with { AsOfUtc=now, SourceFileName="legacy-anubis:"+LmaxDemoUsdNetting.BatchPrefix+"gmv-test" };
        var intent = new TradeIntent(TradeIntentId.New(),run.Id,run.FundId,instrument.Id,signedOrder>0?TradeSide.Buy:TradeSide.Sell,
            Math.Abs(signedOrder),Math.Abs(signedOrder)/10000m,"SIMULATED_GMV_TEST",TradeIntentStatus.Created,now);
        var context = new RiskContext(state.Funds[0],state.Venues[0],instrument,run,market,current,true,gross,now,DemoUsdNetting:netted);
        return new RiskEngine().EvaluateDetailed(intent,context,
            state.RiskLimitSets[0] with { MaxGrossExposureUsd=LmaxDemoGmvRiskProfile.PortfolioGmvUsd },
            state.InstrumentRiskLimits[0] with { InstrumentId=instrument.Id, MaxExposureUsd=LmaxDemoGmvRiskProfile.PositionGmvUsd,
                MaxTradeNotionalUsd=LmaxDemoGmvRiskProfile.MaximumReversalOrderUsd },
            state.VenueRiskLimits[0] with { MaxTradeNotionalUsd=LmaxDemoGmvRiskProfile.MaximumReversalOrderUsd },
            state.TradingWindows[0] with { DayOfWeek=wrongDay?DayOfWeek.Monday:now.DayOfWeek,
                OpensAtUtc=TimeOnly.MinValue,ClosesAtUtc=new TimeOnly(23,59,59),NoNewOrdersAfterUtc=new TimeOnly(23,59,59),
                IsEnabled=true,TradingEnabled=!closed },state.KillSwitch);
    }
}
