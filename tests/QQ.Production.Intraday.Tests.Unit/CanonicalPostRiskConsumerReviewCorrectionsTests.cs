using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class CanonicalPostRiskConsumerReviewCorrectionsTests
{
    private static readonly DateTimeOffset At = new(2026, 8, 25, 9, 0, 0, TimeSpan.Zero);
    private const string Valid = """{"contractVersion":"v1","adapterInputId":"intraday-post-risk-001","revision":2,"supersedes":{"adapterInputId":"intraday-post-risk-001","revision":1},"mandateId":"mandate-001","instrumentId":"instrument-001","modelTargetId":"target-001","adjustmentState":"Overridden","overrideRevisionId":"override-001","riskApprovedTargetWeight":"-0.125","riskDecisionId":"risk-001","policyRevisionId":"risk-policy-001","riskInputSnapshot":{"snapshotId":"risk-input-001","effectiveAt":"2026-08-25T09:00:00.0000000+00:00","recordedAt":"2026-08-25T09:00:01.0000000+00:00","provenance":"risk-inputs:v1"},"riskEvaluatedAt":"2026-08-25T09:00:02.0000000+00:00","riskRecordedAt":"2026-08-25T09:00:03.0000000+00:00","riskRuleEvaluations":[{"ruleId":"rule-001","ruleVersion":"v1","outcome":"Pass","explanation":"approved"}],"participants":[{"strategyId":"strategy-001","strategyVersion":"v1","strategyRunId":"run-001","snapshotId":"snapshot-001","snapshotRevision":1,"snapshotFingerprint":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","resultingRunInput":"investment-snapshot:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","mappingSetId":"mapping-001","mappingRevision":1}],"effectiveAt":"2026-08-25T09:00:00.0000000+00:00","recordedAt":"2026-08-25T09:01:00.0000000+00:00","knowledgeCutoff":"2026-08-25T09:00:30.0000000+00:00","provenance":"risk-approved-target:sha256:example","decision":"intraday-adapter-decision-001","fingerprint":"4ab18dd8c13f6e6859e436ac6758d72ac06b824e5a9e601ff8fb7382d68a1eac"}""";

    [Fact]
    public void Canonical_fingerprint_accepts_equivalent_utc_z_spelling()
        => Assert.Equal("intraday-post-risk-001", CanonicalPostRiskInputParser.Parse(Valid.Replace("2026-08-25T09:00:02.0000000+00:00", "2026-08-25T09:00:02Z", StringComparison.Ordinal)).AdapterInputId);

    [Theory]
    [InlineData("\"adapterInputId\":\"intraday-post-risk-001\",\"revision\":1", "\"adapterInputId\":\"other\",\"revision\":1")]
    [InlineData("\"adapterInputId\":\"intraday-post-risk-001\",\"revision\":1", "\"adapterInputId\":\"intraday-post-risk-001\",\"revision\":2")]
    [InlineData("\"adapterInputId\":\"intraday-post-risk-001\",\"revision\":1", "\"adapterInputId\":\"intraday-post-risk-001\",\"revision\":3")]
    public void Invalid_supersedes_reference_fails_closed(string oldValue, string newValue)
        => Assert.Throws<ArgumentException>(() => CanonicalPostRiskInputParser.Parse(Valid.Replace(oldValue, newValue, StringComparison.Ordinal)));

    [Fact]
    public void Real_higher_revision_wire_correction_is_distinct()
    {
        var original = CanonicalPostRiskInputParser.Parse(Valid);
        var candidate = Valid.Replace("\"revision\":2,", "\"revision\":3,", StringComparison.Ordinal);
        candidate = candidate.Replace("4ab18dd8c13f6e6859e436ac6758d72ac06b824e5a9e601ff8fb7382d68a1eac", CanonicalPostRiskInputParser.CanonicalFingerprint(candidate), StringComparison.Ordinal);
        var correction = CanonicalPostRiskInputParser.Parse(candidate); var receipts = new InMemoryCanonicalInputReceiptStore();
        Assert.Equal(CanonicalInputReceiptResult.Accepted, receipts.Record(original));
        Assert.Equal(CanonicalInputReceiptResult.Accepted, receipts.Record(correction));
        Assert.Equal(3, correction.Revision);
    }

    [Fact]
    public void Execution_context_cannot_cross_bind_fund_or_venue()
    {
        var input = CanonicalPostRiskInputParser.Parse(Valid); var fund = new FundId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")); var venue = new VenueId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var mandate = new MandateFundMapping(input.MandateId, fund, "m", 1, At.AddMinutes(-1), null, "test");
        var instrument = new InstrumentExecutionMapping(input.InstrumentId, new InstrumentId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")), venue, new VenueInstrumentId(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")), "i", 1, At.AddMinutes(-1), null, "test");
        var wrongFund = Context(new FundId(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")), venue, "wrong-fund"); var right = Context(fund, venue, "right");
        var resolver = new InMemoryCanonicalExecutionContextResolver([mandate], [instrument], [wrongFund, right]);
        Assert.Equal("right", resolver.Resolve(input, At).Execution.ContextId);
        Assert.Throws<InvalidOperationException>(() => new InMemoryCanonicalExecutionContextResolver([mandate], [instrument], [wrongFund]).Resolve(input, At));
    }

    private static RetainedExecutionContext Context(FundId fund, VenueId venue, string id) => new(fund, venue, 1m, 15, TargetQuantityMode.FxBaseCurrencyQuantity, new BrokerAccountId(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")), "route", id, 1, At.AddMinutes(-1), null, "test");
}
