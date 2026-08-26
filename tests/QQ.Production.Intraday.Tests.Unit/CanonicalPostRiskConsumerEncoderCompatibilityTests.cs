using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class CanonicalPostRiskConsumerEncoderCompatibilityTests
{
    [Fact]
    public void Canonical_fingerprint_accepts_html_sensitive_and_non_ascii_material()
    {
        const string placeholder = "0000000000000000000000000000000000000000000000000000000000000000";
        var wire = """{"contractVersion":"v1","adapterInputId":"input-1","revision":2,"supersedes":{"adapterInputId":"input-1","revision":1},"mandateId":"mandate-1","instrumentId":"instrument-1","modelTargetId":"target-1","adjustmentState":"NoOverride","overrideRevisionId":null,"riskApprovedTargetWeight":"0.1","riskDecisionId":"risk-1","policyRevisionId":"policy-1","riskInputSnapshot":{"snapshotId":"snapshot-1","effectiveAt":"2026-08-25T09:00:00.0000000+00:00","recordedAt":"2026-08-25T09:00:01.0000000+00:00","provenance":"risk<&>é"},"riskEvaluatedAt":"2026-08-25T09:00:02.0000000+00:00","riskRecordedAt":"2026-08-25T09:00:03.0000000+00:00","riskRuleEvaluations":[{"ruleId":"rule-1","ruleVersion":"v1","outcome":"Pass","explanation":"approved <&>é"}],"participants":[{"strategyId":"strategy-1","strategyVersion":"v1","strategyRunId":"run-1","snapshotId":"snapshot-1","snapshotRevision":1,"snapshotFingerprint":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","resultingRunInput":"input-1","mappingSetId":"map-1","mappingRevision":1}],"effectiveAt":"2026-08-25T09:00:00.0000000+00:00","recordedAt":"2026-08-25T09:01:00.0000000+00:00","knowledgeCutoff":"2026-08-25T09:00:30.0000000+00:00","provenance":"consumer<&>é","decision":"decision<&>é","fingerprint":"0000000000000000000000000000000000000000000000000000000000000000"}""";
        const string expected = "03d45465a7bf09df3b1fa74281c3f8c8195009ca0c9ca919af1df9d68bd97543";
        Assert.Equal(expected, CanonicalPostRiskInputParser.CanonicalFingerprint(wire));
        var accepted = CanonicalPostRiskInputParser.Parse(wire.Replace(placeholder, expected, StringComparison.Ordinal));
        Assert.Equal(expected, accepted.Fingerprint);
    }
}
