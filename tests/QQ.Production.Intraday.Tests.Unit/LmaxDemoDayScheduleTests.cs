using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxDemoDayScheduleTests
{
    [Theory]
    [InlineData("2026-09-16T18:45:00Z", 19)]
    [InlineData("2026-11-16T19:45:00Z", 20)]
    public void FinalExit_UsesNewYorkLocalClock(string value, int expectedUtcClose)
    {
        var cutoff = DateTimeOffset.Parse(value);
        Assert.True(LmaxDemoDaySchedule.IsFinalExit(cutoff));
        Assert.Equal(expectedUtcClose, LmaxDemoDaySchedule.FinalClose(cutoff).Hour);
        Assert.All(new[] { "INFX7", "INFX8", "INFX9", "INFX10" }, p => Assert.False(LmaxDemoDaySchedule.IsEligible(p, cutoff)));
    }

    [Fact]
    public void EuropeanExit_DoesNotCloseEligibleUnitedStatesContribution()
    {
        var cutoff = DateTimeOffset.Parse("2026-09-16T15:45:00Z");
        Assert.False(LmaxDemoDaySchedule.IsFinalExit(cutoff));
        Assert.True(LmaxDemoDaySchedule.IsEligible("INFX7", cutoff));
        Assert.False(LmaxDemoDaySchedule.IsEligible("INFX8", cutoff));
        Assert.False(LmaxDemoDaySchedule.IsEligible("INFX9", cutoff));
        Assert.False(LmaxDemoDaySchedule.IsEligible("INFX10", cutoff));
    }

    [Fact]
    public async Task ScheduledExit_IsZeroForObservedScopeWithExplicitPolicyLineage()
    {
        var at = DateTimeOffset.Parse("2026-09-16T18:45:00Z");
        var (service, state) = Fixture(at);
        var result = await service.IngestAsync(Request(at), CancellationToken.None);
        Assert.Equal(0, result.PresentProgrammeCount);
        Assert.Equal(4, result.AbsentProgrammeCount);
        Assert.Equal(0, result.SourceRowCount);
        Assert.All(state.ModelWeightRows, row => Assert.Equal(0m, row.Weight));
        Assert.Equal(1, result.ExecutableRowCount);
    }

    [Theory]
    [InlineData("2026-09-16T18:30:00Z")]
    [InlineData("2026-09-16T19:00:00Z")]
    public async Task ScheduledExit_CannotCreateAnEarlierOrLateSyntheticDecision(string value)
    {
        var at = DateTimeOffset.Parse(value);
        var (service, state) = Fixture(at);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => service.IngestAsync(Request(at), CancellationToken.None));
        Assert.Empty(state.ModelWeightBatches);
    }

    [Fact]
    public async Task FourAbsentProgrammesWithoutExplicitExit_StillFail()
    {
        var at = DateTimeOffset.Parse("2026-09-16T18:45:00Z");
        var (service, _) = Fixture(at);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => service.IngestAsync(Request(at) with { DemoScheduledExitScope = null }, CancellationToken.None));
    }

    [Theory]
    [InlineData(TradeSide.Sell, 1000, RiskDecisionStatus.Approved)]
    [InlineData(TradeSide.Buy, 1000, RiskDecisionStatus.Rejected)]
    [InlineData(TradeSide.Sell, 2000, RiskDecisionStatus.Rejected)]
    public void ExitWindow_AllowsOnlyReductionWithoutReversal(TradeSide side, int quantity, RiskDecisionStatus expected)
    {
        var at = DateTimeOffset.Parse("2026-09-16T18:45:00Z");
        var state = SeedData.Create(at);
        var run = state.ModelRuns.Single() with { AsOfUtc = at };
        var context = new RiskContext(state.Funds.Single(), state.Venues.Single(), state.Instruments.Single(), run,
            state.MarketData.Single(), 1000m, true, 1100m, at.AddMinutes(1), true);
        var intent = new TradeIntent(TradeIntentId.New(), run.Id, run.FundId, state.Instruments.Single().Id,
            side, quantity, quantity / 10000m, "Scheduled Demo reduction", TradeIntentStatus.Created, at);
        var window = state.TradingWindows.First() with { OpensAtUtc = new TimeOnly(13, 0), ClosesAtUtc = new TimeOnly(19, 0), NoNewOrdersAfterUtc = new TimeOnly(18, 45) };
        var decision = new RiskEngine().Evaluate(intent, context, state.RiskLimitSets.First(), state.InstrumentRiskLimits.First(), state.VenueRiskLimits.First(), window, state.KillSwitch);
        Assert.Equal(expected, decision.Status);
    }

    private static (LegacyAnubisPortfolioWeightIngestionService, PlatformState) Fixture(DateTimeOffset at)
    {
        var state = SeedData.Create(at);
        state.BrokerAccounts[0] = state.BrokerAccounts[0] with { AccountCode = "1754288005" };
        return (new(new InMemoryModelWeightBatchRepository(state), new InMemoryIntradayRepository(state), new FixedClock(at)), state);
    }
    private static LegacyAnubisPortfolioWeightIngestionRequest Request(DateTimeOffset at) => new(
        [Contribution("INFX7", 54, 10, "US", 15, 4.5m), Contribution("INFX8", 57, 11, "US", 30, 2.1m),
         Contribution("INFX9", 58, 12, "EU", 15, 1.4m), Contribution("INFX10", 59, 13, "EU", 60, .6m)],
        "QQ Intraday Fund", "IntradayFxPortfolio", at, at.AddMinutes(15), 1000000m, TargetQuantityMode.PortfolioBaseCurrencyNotional, ["EURUSD"]);
    private static LegacyAnubisProgrammeContribution Contribution(string name, int universe, int model, string session, int minutes, decimal coefficient)
        => new(name, universe, model, session, minutes, coefficient, LegacyAnubisProgrammeContributionState.Absent, Reason: "SCHEDULED_DEMO_SESSION_EXIT");
}
