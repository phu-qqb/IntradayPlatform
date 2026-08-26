using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class CanonicalPostRiskTradingTests
{
    private static readonly DateTimeOffset At = new(2026, 8, 25, 9, 2, 0, TimeSpan.Zero);

    [Fact]
    public void Fresh_canonical_input_is_received_once_and_released_only_to_the_non_routing_boundary()
    {
        var consumer = new CanonicalPostRiskConsumptionService(new InMemoryCanonicalInputReceiptStore(), Service());

        var first = consumer.Consume(Input(), At, Safety());
        var duplicate = consumer.Consume(Input(), At, Safety());

        Assert.False(first.Duplicate);
        Assert.True(first.TradingResult!.Allowed);
        Assert.Equal(-1250m, first.TradingResult.TargetBaseQuantity);
        Assert.Equal(-125m, first.TradingResult.TargetVenueQuantity);
        Assert.Equal(TradeSide.Sell, first.TradingResult.Release!.Side);
        Assert.Equal(1250m, first.TradingResult.Release.BaseQuantity);
        Assert.True(duplicate.Duplicate);
        Assert.Null(duplicate.TradingResult);
    }

    [Theory]
    [InlineData(CanonicalRiskFreshness.Stale)]
    [InlineData(CanonicalRiskFreshness.Unknown)]
    public void Stale_or_unknown_canonical_risk_requires_a_fresh_canonical_decision(CanonicalRiskFreshness state)
    {
        var result = Service(state).Evaluate(Input(), At, Safety());

        Assert.False(result.Allowed);
        Assert.Equal("Fresh canonical Risk required.", result.Reason);
    }

    [Fact]
    public void Release_controls_block_minimum_quantity_and_unspecified_slicing()
    {
        var minimum = Service(control: Control(minimum: 1m)).Evaluate(Input(), At, Safety() with { CurrentBaseQuantity = -1249.5m });
        var maximum = Service(control: Control(maximum: 100m)).Evaluate(Input(), At, Safety());

        Assert.Equal("Below minimum executable quantity.", minimum.Reason);
        Assert.Equal("Maximum per-order notional requires slicing; release blocked.", maximum.Reason);
        Assert.Null(maximum.Release);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Retained_safety_controls_block_release(int control)
    {
        var safety = control switch
        {
            0 => Safety() with { KillSwitchActive = true },
            1 => Safety() with { TradingWindowOpen = false },
            2 => Safety() with { MarketDataFresh = false },
            3 => Safety() with { PositionsReconciled = false },
            4 => Safety() with { InstrumentEnabled = false },
            _ => Safety() with { VenueEnabled = false }
        };

        Assert.False(Service().Evaluate(Input(), At, safety).Allowed);
    }

    [Fact]
    public void Missing_or_cross_bound_context_and_lineage_fail_closed()
    {
        var input = Input();
        Assert.Throws<InvalidOperationException>(() => new InMemoryCanonicalExecutionContextResolver([], [], []).Resolve(input, At));
        var invalidLineage = Safety() with { Mapping = Mapping() with { VenueId = new VenueId(Guid.Parse("99999999-9999-9999-9999-999999999999")) } };

        Assert.Equal("Position or market lineage is invalid.", Service().Evaluate(input, At, invalidLineage).Reason);
    }

    [Fact]
    public void Adapter_contains_no_local_risk_or_order_side_entry()
    {
        var source = File.ReadAllText(RepositoryFile("src", "QQ.Production.Intraday.Application", "CanonicalPostRiskTrading.cs"));

        Assert.DoesNotContain("RiskEngine", source);
        Assert.DoesNotContain("ModelWeightPromotionService", source);
        Assert.DoesNotContain("ProcessModelRunService", source);
        Assert.DoesNotContain("new RiskDecision", source);
        Assert.DoesNotContain("IVenueExecutionGateway", source);
        Assert.DoesNotContain("AddOrdersAsync", source);
        Assert.Contains("no order was created or routed", source);
    }

    private static CanonicalPostRiskTradingService Service(CanonicalRiskFreshness state = CanonicalRiskFreshness.Fresh, CanonicalTradingReleaseControl? control = null)
        => new(new InMemoryCanonicalExecutionContextResolver([Mandate()], [Instrument()], [Context()]), new InMemoryCanonicalTradingReleaseControlResolver([control ?? Control()]), new FixedFreshnessGate(state));

    private static CanonicalPostRiskInput Input() => CanonicalPostRiskInputParser.Parse(Valid);
    private static CanonicalTradingReleaseControl Control(decimal minimum = 10m, decimal maximum = 10_000m) => new(Mandate().FundId, Instrument().VenueId, Instrument().InstrumentId, "release-control", 1, At.AddMinutes(-1), null, "test-release-controls", minimum, maximum);
    private static MandateFundMapping Mandate() => new("mandate-001", new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")), "mandate-map", 1, At.AddMinutes(-1), null, "test");
    private static InstrumentExecutionMapping Instrument() => new("instrument-001", new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")), new(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")), new(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")), "instrument-map", 1, At.AddMinutes(-1), null, "test");
    private static RetainedExecutionContext Context() => new(Mandate().FundId, Instrument().VenueId, 10_000m, 15, TargetQuantityMode.FxBaseCurrencyQuantity, new(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")), "route", "context", 1, At.AddMinutes(-1), null, "test");
    private static VenueInstrumentMapping Mapping() => new(Instrument().VenueInstrumentId, Instrument().VenueId, Instrument().InstrumentId, "TEST", "TEST", 10m, 1m, 0.1m, 0.01m);
    private static CanonicalPostRiskTradingSafety Safety() => new(false, true, true, true, true, true, 0m, Mapping(), new(new MarketDataSnapshotId(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")), Instrument().InstrumentId, Instrument().VenueId, 1.99m, 2.01m, null, "test-market", At.AddSeconds(-10), At.AddSeconds(-5)), At.AddSeconds(-5), "position-snapshot:test");

    private sealed class FixedFreshnessGate(CanonicalRiskFreshness state) : ICanonicalRiskFreshnessGate
    {
        public CanonicalRiskFreshnessAttestation Verify(CanonicalPostRiskInput input, ResolvedCanonicalExecutionContext context, DateTimeOffset asOfUtc)
            => new(input.RiskDecisionId, input.RiskRecordedAtUtc, input.KnowledgeCutoffUtc, input.Provenance, state);
    }

    private static string RepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return Path.Combine([directory!.FullName, .. parts]);
    }

    private const string Valid = """{"contractVersion":"v1","adapterInputId":"intraday-post-risk-001","revision":2,"supersedes":{"adapterInputId":"intraday-post-risk-001","revision":1},"mandateId":"mandate-001","instrumentId":"instrument-001","modelTargetId":"target-001","adjustmentState":"Overridden","overrideRevisionId":"override-001","riskApprovedTargetWeight":"-0.125","riskDecisionId":"risk-001","policyRevisionId":"risk-policy-001","riskInputSnapshot":{"snapshotId":"risk-input-001","effectiveAt":"2026-08-25T09:00:00.0000000+00:00","recordedAt":"2026-08-25T09:00:01.0000000+00:00","provenance":"risk-inputs:v1"},"riskEvaluatedAt":"2026-08-25T09:00:02.0000000+00:00","riskRecordedAt":"2026-08-25T09:00:03.0000000+00:00","riskRuleEvaluations":[{"ruleId":"rule-001","ruleVersion":"v1","outcome":"Pass","explanation":"approved"}],"participants":[{"strategyId":"strategy-001","strategyVersion":"v1","strategyRunId":"run-001","snapshotId":"snapshot-001","snapshotRevision":1,"snapshotFingerprint":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","resultingRunInput":"investment-snapshot:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","mappingSetId":"mapping-001","mappingRevision":1}],"effectiveAt":"2026-08-25T09:00:00.0000000+00:00","recordedAt":"2026-08-25T09:01:00.0000000+00:00","knowledgeCutoff":"2026-08-25T09:00:30.0000000+00:00","provenance":"risk-approved-target:sha256:example","decision":"intraday-adapter-decision-001","fingerprint":"4ab18dd8c13f6e6859e436ac6758d72ac06b824e5a9e601ff8fb7382d68a1eac"}""";
}
