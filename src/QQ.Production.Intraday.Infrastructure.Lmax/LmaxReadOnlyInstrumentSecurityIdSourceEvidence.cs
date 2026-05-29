using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyInstrumentSecurityIdSourceEvidenceType
{
    OfficialLmaxDocument,
    ConnectivityLabSanitizedOutput,
    OperatorManualConfirmation,
    VendorSupportConfirmation,
    Other
}

public enum LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence
{
    Low,
    Medium,
    High,
    Confirmed
}

public enum LmaxReadOnlyInstrumentSecurityIdSourceEvidenceDecision
{
    Pending,
    AcceptedForPlanning,
    Rejected,
    NeedsMoreEvidence
}

public enum LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssueSeverity
{
    Error,
    Warning,
    Info
}

public enum LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReviewDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyInstrumentSecurityIdSourceEvidence(
    string Symbol,
    string SlashSymbol,
    string ProposedSecurityId,
    LmaxReadOnlyInstrumentSecurityIdSourceEvidenceType EvidenceSourceType,
    string EvidenceReference,
    string ReviewedBy,
    DateTimeOffset? ReviewedAtUtc,
    string ReviewReason,
    LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence Confidence,
    LmaxReadOnlyInstrumentSecurityIdSourceEvidenceDecision Decision,
    bool IsApprovedForExternalRun,
    bool NoSensitiveContent);

public sealed record LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssue(
    LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssueSeverity Severity,
    string Code,
    string Path,
    string Message);

public sealed record LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReview(
    IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdSourceEvidence> Evidence,
    bool ExternalConnectionAttempted,
    bool ExternalApiCallAttempted,
    bool MarketDataSnapshotAttempted,
    bool ReplayAttempted,
    bool SchedulerOrPollingAdded,
    bool RuntimeShadowReplaySubmit,
    bool OrderSubmissionAdded,
    bool GatewayRegistrationAdded,
    bool TradingMutationAdded);

public sealed record LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidationResult(
    LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReviewDecision Decision,
    IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdSourceEvidence> Evidence,
    IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssue> Issues)
{
    public IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssueSeverity.Error).ToArray();

    public IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssue> Warnings =>
        Issues.Where(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssueSeverity.Warning).ToArray();
}

public static class LmaxReadOnlyInstrumentSecurityIdEvidenceReviewManifest
{
    public static LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReview CreateDefault()
        => new(
            Evidence: LmaxReadOnlyInstrumentAllowlist.CandidateEntries
                .Select(entry => new LmaxReadOnlyInstrumentSecurityIdSourceEvidence(
                    Symbol: entry.Symbol,
                    SlashSymbol: entry.SlashSymbol,
                    ProposedSecurityId: $"PHASE6D-DISCOVERY-PENDING-{entry.Symbol}",
                    EvidenceSourceType: LmaxReadOnlyInstrumentSecurityIdSourceEvidenceType.Other,
                    EvidenceReference: "Pending local evidence review; no external lookup performed.",
                    ReviewedBy: string.Empty,
                    ReviewedAtUtc: null,
                    ReviewReason: "Phase 6E planning placeholder. Real Demo SecurityID evidence has not been accepted yet.",
                    Confidence: LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence.Low,
                    Decision: LmaxReadOnlyInstrumentSecurityIdSourceEvidenceDecision.NeedsMoreEvidence,
                    IsApprovedForExternalRun: false,
                    NoSensitiveContent: true))
                .ToArray(),
            ExternalConnectionAttempted: false,
            ExternalApiCallAttempted: false,
            MarketDataSnapshotAttempted: false,
            ReplayAttempted: false,
            SchedulerOrPollingAdded: false,
            RuntimeShadowReplaySubmit: false,
            OrderSubmissionAdded: false,
            GatewayRegistrationAdded: false,
            TradingMutationAdded: false);
}

public static class LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator
{
    private static readonly Regex SensitivePattern = new(
        "(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\\b553=|\\b554=)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OrderCapabilityPattern = new(
        "(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|production|uat)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidationResult Validate(
        LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReview? review = null)
    {
        var candidateReview = review ?? LmaxReadOnlyInstrumentSecurityIdEvidenceReviewManifest.CreateDefault();
        var issues = new List<LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssue>();
        var allowlist = LmaxReadOnlyInstrumentAllowlist.CandidateEntries;

        foreach (var expected in allowlist)
        {
            var records = candidateReview.Evidence
                .Where(x => x.Symbol.Equals(expected.Symbol, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (records.Length == 0)
            {
                issues.Add(Error("EvidenceRecordMissing", "$.evidence", $"Missing evidence review record for {expected.Symbol}."));
                continue;
            }

            foreach (var record in records)
            {
                ValidateRecord(record, expected, issues);
            }
        }

        foreach (var record in candidateReview.Evidence)
        {
            if (!allowlist.Any(x => x.Symbol.Equals(record.Symbol, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(Error("UnknownSymbol", $"$.evidence[{record.Symbol}].symbol", $"{record.Symbol} is not in the Phase 6B allowlist."));
            }
        }

        ValidateNoRuntimeActions(candidateReview, issues);

        if (candidateReview.Evidence.Any(x => x.Decision is LmaxReadOnlyInstrumentSecurityIdSourceEvidenceDecision.Pending or LmaxReadOnlyInstrumentSecurityIdSourceEvidenceDecision.NeedsMoreEvidence))
        {
            issues.Add(Warning("EvidencePending", "$.evidence", "One or more instruments still need SecurityID source evidence before planning values can be accepted."));
        }

        var errors = issues.Count(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssueSeverity.Error);
        var warnings = issues.Count(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssueSeverity.Warning);
        var decision = errors > 0
            ? LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReviewDecision.FAIL
            : warnings > 0
                ? LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReviewDecision.PASS_WITH_KNOWN_WARNINGS
                : LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReviewDecision.PASS;

        return new(decision, candidateReview.Evidence, issues);
    }

    private static void ValidateRecord(
        LmaxReadOnlyInstrumentSecurityIdSourceEvidence record,
        LmaxReadOnlyInstrumentAllowlistEntry expected,
        List<LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssue> issues)
    {
        var path = $"$.evidence[{record.Symbol}]";
        Require(record.Symbol, "SymbolRequired", $"{path}.symbol", "Symbol is required.", issues);
        Require(record.SlashSymbol, "SlashSymbolRequired", $"{path}.slashSymbol", "Slash symbol is required.", issues);
        Require(record.ProposedSecurityId, "ProposedSecurityIdRequired", $"{path}.proposedSecurityId", "Proposed SecurityID is required.", issues);
        Require(record.EvidenceReference, "EvidenceReferenceRequired", $"{path}.evidenceReference", "Evidence reference is required.", issues);
        Require(record.ReviewReason, "ReviewReasonRequired", $"{path}.reviewReason", "Review reason is required.", issues);

        if (!record.SlashSymbol.Equals(expected.SlashSymbol, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("SlashSymbolMismatch", $"{path}.slashSymbol", $"{record.Symbol} slash symbol must match the Phase 6B allowlist."));
        }

        if (record.IsApprovedForExternalRun)
        {
            issues.Add(Error("ExternalRunApprovalForbidden", $"{path}.isApprovedForExternalRun", "Phase 6E must keep IsApprovedForExternalRun=false."));
        }

        if (!record.NoSensitiveContent)
        {
            issues.Add(Error("SensitiveContentFlagFalse", $"{path}.noSensitiveContent", "Evidence review records must assert noSensitiveContent=true."));
        }

        var combined = string.Join(" ", record.Symbol, record.SlashSymbol, record.ProposedSecurityId, record.EvidenceReference, record.ReviewedBy, record.ReviewReason);
        if (SensitivePattern.IsMatch(combined))
        {
            issues.Add(Error("SensitiveContentDetected", path, "Evidence review record contains credential-shaped or sensitive content."));
        }

        if (OrderCapabilityPattern.IsMatch(combined))
        {
            issues.Add(Error("TradingAuthorizationImplied", path, "Evidence review record must not imply order, trade, Production, UAT, or execution authorization."));
        }

        if (record.Decision == LmaxReadOnlyInstrumentSecurityIdSourceEvidenceDecision.AcceptedForPlanning)
        {
            Require(record.ReviewedBy, "ReviewedByRequired", $"{path}.reviewedBy", "Accepted evidence requires a reviewer.", issues);

            if (record.ReviewedAtUtc is null)
            {
                issues.Add(Error("ReviewedAtRequired", $"{path}.reviewedAtUtc", "Accepted evidence requires reviewedAtUtc."));
            }

            if (record.Confidence is not (LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence.High or LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence.Confirmed))
            {
                issues.Add(Error("ConfidenceTooLow", $"{path}.confidence", "Accepted evidence requires High or Confirmed confidence."));
            }

            if (IsPhasePlaceholder(record.ProposedSecurityId))
            {
                issues.Add(Error("PlaceholderSecurityIdNotAccepted", $"{path}.proposedSecurityId", "Accepted evidence cannot use a Phase 6 placeholder SecurityID."));
            }
        }
    }

    private static bool IsPhasePlaceholder(string value)
        => value.StartsWith("PHASE6", StringComparison.OrdinalIgnoreCase)
           || value.StartsWith("TBD-", StringComparison.OrdinalIgnoreCase)
           || value.Contains("PENDING", StringComparison.OrdinalIgnoreCase);

    private static void ValidateNoRuntimeActions(
        LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReview review,
        List<LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssue> issues)
    {
        if (review.ExternalConnectionAttempted) issues.Add(Error("ExternalConnectionForbidden", "$.externalConnectionAttempted", "Phase 6E must not connect to LMAX."));
        if (review.ExternalApiCallAttempted) issues.Add(Error("ExternalApiCallForbidden", "$.externalApiCallAttempted", "Phase 6E must not call external APIs."));
        if (review.MarketDataSnapshotAttempted) issues.Add(Error("SnapshotForbidden", "$.marketDataSnapshotAttempted", "Phase 6E must not run market-data snapshots."));
        if (review.ReplayAttempted) issues.Add(Error("ReplayForbidden", "$.replayAttempted", "Phase 6E must not run replay."));
        if (review.SchedulerOrPollingAdded) issues.Add(Error("SchedulerPollingForbidden", "$.schedulerOrPollingAdded", "Phase 6E must not add scheduler or polling."));
        if (review.RuntimeShadowReplaySubmit) issues.Add(Error("RuntimeShadowReplaySubmitForbidden", "$.runtimeShadowReplaySubmit", "Phase 6E must not submit to shadow replay from runtime."));
        if (review.OrderSubmissionAdded) issues.Add(Error("OrderSubmissionForbidden", "$.orderSubmissionAdded", "Phase 6E must not add order submission."));
        if (review.GatewayRegistrationAdded) issues.Add(Error("GatewayRegistrationForbidden", "$.gatewayRegistrationAdded", "Phase 6E must not add gateway registration."));
        if (review.TradingMutationAdded) issues.Add(Error("TradingMutationForbidden", "$.tradingMutationAdded", "Phase 6E must not mutate trading state."));
    }

    private static void Require(
        string value,
        string code,
        string path,
        string message,
        List<LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(Error(code, path, message));
        }
    }

    private static LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssueSeverity.Error, code, path, message);

    private static LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssue Warning(string code, string path, string message)
        => new(LmaxReadOnlyInstrumentSecurityIdSourceEvidenceIssueSeverity.Warning, code, path, message);
}
