using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ReferenceDataIntegrityTests
{
    private static readonly DateTimeOffset Now = new(2026, 04, 29, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Clean_seed_data_returns_zero_blocking_issues()
    {
        var check = await CheckAsync(SeedData.Create(Now));

        Assert.Equal(0, check.BlockingIssueCount);
        Assert.Empty(check.Issues);
    }

    [Fact]
    public async Task Duplicate_venue_instrument_mapping_returns_blocking_issue()
    {
        var state = SeedData.Create(Now);
        var mapping = state.VenueInstrumentMappings.Single();
        state.VenueInstrumentMappings.Add(mapping with { Id = VenueInstrumentId.New() });

        var check = await CheckAsync(state);

        Assert.Contains(check.Issues, x => x.Type == ReferenceDataIntegrityIssueType.DuplicateVenueInstrumentMapping && x.Severity == ReferenceDataIntegritySeverity.Blocking);
    }

    [Fact]
    public async Task Duplicate_risk_limit_set_returns_blocking_issue()
    {
        var state = SeedData.Create(Now);
        var riskLimitSet = state.RiskLimitSets.Single();
        state.RiskLimitSets.Add(riskLimitSet with { Id = Guid.NewGuid() });

        var check = await CheckAsync(state);

        Assert.Contains(check.Issues, x => x.Type == ReferenceDataIntegrityIssueType.DuplicateRiskLimitSet && x.Severity == ReferenceDataIntegritySeverity.Blocking);
    }

    [Fact]
    public async Task Duplicate_trading_window_returns_blocking_issue()
    {
        var state = SeedData.Create(Now);
        var window = state.TradingWindows.Single(x => x.ModelName == "IntradayFxModel");
        state.TradingWindows.Add(window with { Id = Guid.NewGuid() });

        var check = await CheckAsync(state);

        Assert.Contains(check.Issues, x => x.Type == ReferenceDataIntegrityIssueType.DuplicateTradingWindow && x.Severity == ReferenceDataIntegritySeverity.Blocking);
    }

    [Fact]
    public async Task Missing_required_lmax_eurusd_mapping_returns_blocking_issue()
    {
        var state = SeedData.Create(Now);
        state.VenueInstrumentMappings.Clear();

        var check = await CheckAsync(state);

        Assert.Contains(check.Issues, x => x.Type == ReferenceDataIntegrityIssueType.MissingRequiredReferenceData && x.Key == "LMAX:EURUSD");
    }

    [Fact]
    public async Task Duplicate_current_kill_switch_state_returns_blocking_issue()
    {
        var state = SeedData.Create(Now);
        state.KillSwitchStates.Add(state.KillSwitch with { Id = Guid.NewGuid() });

        var check = await CheckAsync(state);

        Assert.Contains(check.Issues, x => x.Type == ReferenceDataIntegrityIssueType.DuplicateKillSwitchState && x.Severity == ReferenceDataIntegritySeverity.Blocking);
    }

    private static Task<ReferenceDataIntegrityResult> CheckAsync(PlatformState state)
    {
        var repository = new InMemoryIntradayRepository(state);
        return new ReferenceDataIntegrityService(repository, new FixedClock(Now)).CheckAsync(CancellationToken.None);
    }
}
