using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class CanonicalPostRiskConsumerTests
{
    private static readonly DateTimeOffset At = new(2026, 8, 25, 9, 0, 0, TimeSpan.Zero);
    private const string Valid = """{"contractVersion":"v1","adapterInputId":"intraday-post-risk-001","revision":2,"supersedes":{"adapterInputId":"intraday-post-risk-001","revision":1},"mandateId":"mandate-001","instrumentId":"instrument-001","modelTargetId":"target-001","adjustmentState":"Overridden","overrideRevisionId":"override-001","riskApprovedTargetWeight":"-0.125","riskDecisionId":"risk-001","policyRevisionId":"risk-policy-001","riskInputSnapshot":{"snapshotId":"risk-input-001","effectiveAt":"2026-08-25T09:00:00.0000000+00:00","recordedAt":"2026-08-25T09:00:01.0000000+00:00","provenance":"risk-inputs:v1"},"riskEvaluatedAt":"2026-08-25T09:00:02.0000000+00:00","riskRecordedAt":"2026-08-25T09:00:03.0000000+00:00","riskRuleEvaluations":[{"ruleId":"rule-001","ruleVersion":"v1","outcome":"Pass","explanation":"approved"}],"participants":[{"strategyId":"strategy-001","strategyVersion":"v1","strategyRunId":"run-001","snapshotId":"snapshot-001","snapshotRevision":1,"snapshotFingerprint":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","resultingRunInput":"investment-snapshot:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","mappingSetId":"mapping-001","mappingRevision":1}],"effectiveAt":"2026-08-25T09:00:00.0000000+00:00","recordedAt":"2026-08-25T09:01:00.0000000+00:00","knowledgeCutoff":"2026-08-25T09:00:30.0000000+00:00","provenance":"risk-approved-target:sha256:example","decision":"intraday-adapter-decision-001","fingerprint":"4ab18dd8c13f6e6859e436ac6758d72ac06b824e5a9e601ff8fb7382d68a1eac"}""";

    [Fact] public void Fixture_is_accepted_exactly() => Assert.Equal(-0.125m, CanonicalPostRiskInputParser.Parse(Valid).RiskApprovedTargetWeight);

    [Theory]
    [InlineData("\"riskApprovedTargetWeight\":\"-0.125\"", "\"riskApprovedTargetWeight\":\"-0.25\"")]
    [InlineData("\"adjustmentState\":\"Overridden\"", "\"adjustmentState\":\"Unknown\"")]
    [InlineData("\"riskApprovedTargetWeight\":\"-0.125\"", "\"riskApprovedTargetWeight\":\"-0.1250\"")]
    public void Invalid_or_stale_material_fails_closed(string oldValue, string newValue) => Assert.Throws<ArgumentException>(() => CanonicalPostRiskInputParser.Parse(Valid.Replace(oldValue, newValue, StringComparison.Ordinal)));

    [Fact] public void Unknown_and_missing_fields_fail_closed()
    {
        Assert.Throws<ArgumentException>(() => CanonicalPostRiskInputParser.Parse(Valid.Replace("{\"contractVersion\"", "{\"unknown\":true,\"contractVersion\"", StringComparison.Ordinal)));
        Assert.Throws<ArgumentException>(() => CanonicalPostRiskInputParser.Parse(Valid.Replace("\"decision\":\"intraday-adapter-decision-001\",", string.Empty, StringComparison.Ordinal)));
    }

    [Fact] public void Receipt_is_idempotent_conflict_safe_and_revision_distinct()
    {
        var input = CanonicalPostRiskInputParser.Parse(Valid); var receipts = new InMemoryCanonicalInputReceiptStore();
        Assert.Equal(CanonicalInputReceiptResult.Accepted, receipts.Record(input)); Assert.Equal(CanonicalInputReceiptResult.Duplicate, receipts.Record(input));
        Assert.Throws<InvalidOperationException>(() => receipts.Record(input with { Fingerprint = new string('b', 64) }));
        Assert.Equal(CanonicalInputReceiptResult.Accepted, receipts.Record(input with { Revision = 3, Fingerprint = new string('c', 64) }));
    }

    [Fact] public void Resolver_requires_explicit_unique_effective_mappings()
    {
        var input = CanonicalPostRiskInputParser.Parse(Valid); var resolver = new InMemoryCanonicalExecutionContextResolver([Mandate()], [Instrument()], [Context()]);
        Assert.Equal(Mandate().FundId, resolver.Resolve(input, At).MandateFund.FundId);
        Assert.Throws<InvalidOperationException>(() => new InMemoryCanonicalExecutionContextResolver([], [], []).Resolve(input, At));
        Assert.Throws<InvalidOperationException>(() => new InMemoryCanonicalExecutionContextResolver([Mandate() with { MandateId = "fund-code" }], [Instrument()], [Context()]).Resolve(input, At));
        Assert.Throws<InvalidOperationException>(() => new InMemoryCanonicalExecutionContextResolver([Mandate()], [Instrument() with { EffectiveFromUtc = At.AddMinutes(1) }], [Context()]).Resolve(input, At));
        Assert.Throws<InvalidOperationException>(() => new InMemoryCanonicalExecutionContextResolver([Mandate()], [Instrument()], [Context(), Context()]).Resolve(input, At));
    }

    private static MandateFundMapping Mandate() => new("mandate-001", new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")), "mandate-map", 1, At.AddMinutes(-1), null, "test");
    private static InstrumentExecutionMapping Instrument() => new("instrument-001", new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")), new(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")), new(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")), "instrument-map", 1, At.AddMinutes(-1), null, "test");
    private static RetainedExecutionContext Context() => new(1000000m, 15, TargetQuantityMode.FxBaseCurrencyQuantity, new(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")), "route", "context", 1, At.AddMinutes(-1), null, "test");
}
