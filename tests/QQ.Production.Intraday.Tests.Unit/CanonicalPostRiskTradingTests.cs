using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class CanonicalPostRiskTradingTests
{
    private static readonly DateTimeOffset At = new(2026, 8, 25, 9, 2, 0, TimeSpan.Zero);

    [Fact]
    public void Public_wire_entry_parses_before_receipt_and_creates_dormant_parent_and_child_once()
    {
        var boundary = new InMemoryCanonicalPostRiskOrderBoundary();
        var consumer = Consumer(boundary);
        var first = consumer.Consume(Wire("-0.125"), At);
        var duplicate = consumer.Consume(Wire("-0.125"), At);

        Assert.True(first.Allowed);
        Assert.False(first.Duplicate);
        Assert.Equal(-1250m, first.Evidence!.Target.TargetBaseQuantity);
        Assert.Equal(-125m, first.Evidence.Target.TargetVenueQuantity);
        Assert.Equal(OrderSide.Sell, first.Evidence.ParentOrder.Side);
        Assert.Equal(first.Evidence.ParentOrder.Id, first.Evidence.ChildOrder.ParentOrderId);
        Assert.Equal(1, boundary.CreatedOrderCount);
        Assert.True(duplicate.Duplicate);
        Assert.Equal(1, boundary.CreatedOrderCount);
        Assert.Throws<ArgumentException>(() => consumer.Consume(Wire("-0.125", corruptFingerprint: true), At));
    }

    [Fact]
    public void Conflicting_same_revision_stops_before_effect_and_higher_revision_is_distinct()
    {
        var boundary = new InMemoryCanonicalPostRiskOrderBoundary();
        var consumer = Consumer(boundary);
        Assert.True(consumer.Consume(Wire("-0.125"), At).Allowed);
        Assert.Throws<InvalidOperationException>(() => consumer.Consume(Wire("-0.25"), At));
        Assert.Equal(1, boundary.CreatedOrderCount);
        Assert.True(consumer.Consume(Wire("0.125", revision: 3, supersededRevision: 2), At).Allowed);
        Assert.Equal(2, boundary.CreatedOrderCount);
    }

    [Theory]
    [InlineData("0.125", 1250)]
    [InlineData("-0.125", -1250)]
    [InlineData("0.0125", 125)]
    public void Canonical_weights_use_shared_retained_sizing_exactly(string weight, decimal targetBase)
    {
        var result = Consumer(new InMemoryCanonicalPostRiskOrderBoundary()).Consume(Wire(weight), At);
        Assert.True(result.Allowed);
        Assert.Equal(targetBase, result.Evidence!.Target.TargetBaseQuantity);
        var expected = RetainedTargetPositionSizing.Calculate(TargetQuantityMode.FxBaseCurrencyQuantity, decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture), 10_000m, State().MarketData, Mapping());
        Assert.Equal(expected, result.Evidence.Target);
    }

    [Fact]
    public void Zero_drift_and_non_executable_drift_create_no_order()
    {
        var zeroBoundary = new InMemoryCanonicalPostRiskOrderBoundary();
        var zero = Consumer(zeroBoundary, State() with { CurrentBaseQuantity = -1250m }).Consume(Wire("-0.125"), At);
        var smallBoundary = new InMemoryCanonicalPostRiskOrderBoundary();
        var small = Consumer(smallBoundary, State() with { CurrentBaseQuantity = -1245m }).Consume(Wire("-0.125"), At);
        var zeroWeightBoundary = new InMemoryCanonicalPostRiskOrderBoundary();
        var zeroWeight = Consumer(zeroWeightBoundary).Consume(Wire("0"), At);
        var invalidBoundary = new InMemoryCanonicalPostRiskOrderBoundary();
        var invalid = Consumer(invalidBoundary, State() with { Mapping = Mapping() with { QuantityStep = 0m } }).Consume(Wire("-0.125"), At);

        Assert.Equal("No executable drift.", zero.Reason);
        Assert.Equal("No executable drift.", zeroWeight.Reason);
        Assert.Equal("Drift venue quantity is below the retained minimum order quantity.", small.Reason);
        Assert.Equal("Retained position or market state is invalid.", invalid.Reason);
        Assert.Equal(0, zeroBoundary.CreatedOrderCount);
        Assert.Equal(0, smallBoundary.CreatedOrderCount);
        Assert.Equal(0, invalidBoundary.CreatedOrderCount);
        Assert.Equal(0, zeroWeightBoundary.CreatedOrderCount);
    }

    [Theory]
    [InlineData(CanonicalBoundRiskFreshness.Stale)]
    [InlineData(CanonicalBoundRiskFreshness.Unknown)]
    public void Stale_or_unknown_freshness_fails_closed(CanonicalBoundRiskFreshness freshness)
        => Assert.Equal("Fresh canonical Risk required.", Consumer(new InMemoryCanonicalPostRiskOrderBoundary(), freshness: freshness).Consume(Wire("-0.125"), At).Reason);

    [Fact]
    public void Freshness_must_bind_exact_state_and_time()
    {
        var state = State();
        Assert.False(Consumer(new InMemoryCanonicalPostRiskOrderBoundary(), state, mismatch: true).Consume(Wire("-0.125"), At).Allowed);
        Assert.False(Consumer(new InMemoryCanonicalPostRiskOrderBoundary(), state with { PositionAsOfUtc = At.AddMinutes(-2) }).Consume(Wire("-0.125"), At).Allowed);
        Assert.False(Consumer(new InMemoryCanonicalPostRiskOrderBoundary(), state with { MarketData = state.MarketData with { ReceivedAtUtc = At.AddMinutes(1) } }).Consume(Wire("-0.125"), At).Allowed);
        Assert.False(Consumer(new InMemoryCanonicalPostRiskOrderBoundary(), state, future: true).Consume(Wire("-0.125"), At).Allowed);
        var invalidTiming = Wire("-0.125").Replace("2026-08-25T09:00:03.0000000+00:00", "2026-08-25T09:00:40.0000000+00:00", StringComparison.Ordinal);
        invalidTiming = invalidTiming.Replace("4ab18dd8c13f6e6859e436ac6758d72ac06b824e5a9e601ff8fb7382d68a1eac", CanonicalPostRiskInputParser.CanonicalFingerprint(invalidTiming), StringComparison.Ordinal);
        Assert.False(Consumer(new InMemoryCanonicalPostRiskOrderBoundary()).Consume(invalidTiming, At).Allowed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Retained_safety_provider_controls_cannot_be_bypassed_by_wire_caller(int control)
    {
        var state = control switch
        {
            0 => State() with { KillSwitchActive = true },
            1 => State() with { TradingWindowOpen = false },
            2 => State() with { MarketDataFresh = false },
            3 => State() with { PositionsReconciled = false },
            4 => State() with { InstrumentEnabled = false },
            _ => State() with { VenueEnabled = false }
        };
        Assert.False(Consumer(new InMemoryCanonicalPostRiskOrderBoundary(), state).Consume(Wire("-0.125"), At).Allowed);
    }

    [Fact]
    public void Adr_007_release_controls_block_without_target_mutation_or_slicing()
    {
        var belowMinimum = Consumer(new InMemoryCanonicalPostRiskOrderBoundary(), State() with { CurrentBaseQuantity = -1240m }, control: Control(minimum: 20m)).Consume(Wire("-0.125"), At);
        var overMaximum = Consumer(new InMemoryCanonicalPostRiskOrderBoundary(), control: Control(maximum: 100m)).Consume(Wire("-0.125"), At);
        Assert.Equal("Below minimum executable quantity.", belowMinimum.Reason);
        Assert.Equal("Maximum per-order notional requires slicing; release blocked.", overMaximum.Reason);
    }

    [Fact]
    public void Wire_entry_has_no_public_safety_or_live_venue_send_path()
    {
        var source = File.ReadAllText(RepositoryFile("src", "QQ.Production.Intraday.Application", "CanonicalPostRiskOrderBoundary.cs"));
        Assert.Contains("Consume(string canonicalWire, DateTimeOffset asOfUtc)", source);
        Assert.Contains("RetainedTargetPositionSizing.Calculate", source);
        Assert.DoesNotContain("SendOrderAsync", source);
        Assert.DoesNotContain("IVenueExecutionGateway", source);
    }

    private static CanonicalPostRiskConsumptionService Consumer(InMemoryCanonicalPostRiskOrderBoundary boundary, CanonicalPostRiskRetainedState? state = null, CanonicalBoundRiskFreshness freshness = CanonicalBoundRiskFreshness.Fresh, bool mismatch = false, CanonicalTradingReleaseControl? control = null, bool future = false)
        => new(new InMemoryCanonicalInputReceiptStore(), new InMemoryCanonicalExecutionContextResolver([Mandate()], [Instrument()], [Context()]), new InMemoryCanonicalTradingReleaseControlResolver([control ?? Control()]), new FixedStateProvider(state ?? State()), new FixedFreshnessGate(freshness, mismatch, future), boundary);

    private static CanonicalTradingReleaseControl Control(decimal minimum = 10m, decimal maximum = 10_000m) => new(Mandate().FundId, Instrument().VenueId, Instrument().InstrumentId, "release-control", 1, At.AddMinutes(-1), null, "test-release-controls", minimum, maximum);
    private static MandateFundMapping Mandate() => new("mandate-001", new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")), "mandate-map", 1, At.AddMinutes(-1), null, "test");
    private static InstrumentExecutionMapping Instrument() => new("instrument-001", new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")), new(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")), new(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")), "instrument-map", 1, At.AddMinutes(-1), null, "test");
    private static RetainedExecutionContext Context() => new(Mandate().FundId, Instrument().VenueId, 10_000m, 15, TargetQuantityMode.FxBaseCurrencyQuantity, new(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")), "route", "context", 1, At.AddMinutes(-1), null, "test");
    private static VenueInstrumentMapping Mapping() => new(Instrument().VenueInstrumentId, Instrument().VenueId, Instrument().InstrumentId, "TEST", "TEST", 10m, 1m, 0.1m, 0.01m);
    private static CanonicalPostRiskRetainedState State() => new(false, true, true, true, true, true, 0m, Mapping(), new(new MarketDataSnapshotId(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")), Instrument().InstrumentId, Instrument().VenueId, 1.99m, 2.01m, null, "test-market", At.AddSeconds(-10), At.AddSeconds(-5)), At.AddSeconds(-5), "position-snapshot:test", "market-snapshot:test");

    private sealed class FixedStateProvider(CanonicalPostRiskRetainedState state) : ICanonicalPostRiskRetainedStateProvider
    {
        public CanonicalPostRiskRetainedState Resolve(CanonicalPostRiskInput input, ResolvedCanonicalExecutionContext context, DateTimeOffset asOfUtc) => state;
    }

    private sealed class FixedFreshnessGate(CanonicalBoundRiskFreshness result, bool mismatch, bool future) : ICanonicalBoundRiskFreshnessGate
    {
        public CanonicalBoundRiskFreshnessAttestation Verify(CanonicalPostRiskInput input, ResolvedCanonicalExecutionContext context, CanonicalPostRiskRetainedState state, DateTimeOffset asOfUtc)
            => new(input.RiskDecisionId, input.RiskRecordedAtUtc, input.KnowledgeCutoffUtc, input.Provenance, mismatch ? "wrong-position" : state.PositionReference, state.PositionAsOfUtc, state.MarketReference, state.MarketData.Id, state.MarketData.ReceivedAtUtc, future ? asOfUtc.AddSeconds(1) : asOfUtc, result);
    }

    private static string Wire(string weight, long revision = 2, long supersededRevision = 1, bool corruptFingerprint = false)
    {
        var wire = Valid.Replace("\"riskApprovedTargetWeight\":\"-0.125\"", "\"riskApprovedTargetWeight\":\"" + weight + "\"", StringComparison.Ordinal).Replace("\"revision\":2", "\"revision\":" + revision, StringComparison.Ordinal).Replace("\"revision\":1}", "\"revision\":" + supersededRevision + "}", StringComparison.Ordinal);
        var fingerprint = CanonicalPostRiskInputParser.CanonicalFingerprint(wire);
        return wire.Replace("4ab18dd8c13f6e6859e436ac6758d72ac06b824e5a9e601ff8fb7382d68a1eac", corruptFingerprint ? new string('b', 64) : fingerprint, StringComparison.Ordinal);
    }

    private static string RepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return Path.Combine([directory!.FullName, .. parts]);
    }

    private const string Valid = """{"contractVersion":"v1","adapterInputId":"intraday-post-risk-001","revision":2,"supersedes":{"adapterInputId":"intraday-post-risk-001","revision":1},"mandateId":"mandate-001","instrumentId":"instrument-001","modelTargetId":"target-001","adjustmentState":"Overridden","overrideRevisionId":"override-001","riskApprovedTargetWeight":"-0.125","riskDecisionId":"risk-001","policyRevisionId":"risk-policy-001","riskInputSnapshot":{"snapshotId":"risk-input-001","effectiveAt":"2026-08-25T09:00:00.0000000+00:00","recordedAt":"2026-08-25T09:00:01.0000000+00:00","provenance":"risk-inputs:v1"},"riskEvaluatedAt":"2026-08-25T09:00:02.0000000+00:00","riskRecordedAt":"2026-08-25T09:00:03.0000000+00:00","riskRuleEvaluations":[{"ruleId":"rule-001","ruleVersion":"v1","outcome":"Pass","explanation":"approved"}],"participants":[{"strategyId":"strategy-001","strategyVersion":"v1","strategyRunId":"run-001","snapshotId":"snapshot-001","snapshotRevision":1,"snapshotFingerprint":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","resultingRunInput":"investment-snapshot:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","mappingSetId":"mapping-001","mappingRevision":1}],"effectiveAt":"2026-08-25T09:00:00.0000000+00:00","recordedAt":"2026-08-25T09:01:00.0000000+00:00","knowledgeCutoff":"2026-08-25T09:00:30.0000000+00:00","provenance":"risk-approved-target:sha256:example","decision":"intraday-adapter-decision-001","fingerprint":"4ab18dd8c13f6e6859e436ac6758d72ac06b824e5a9e601ff8fb7382d68a1eac"}""";
}
