using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision
{
    Draft,
    AcceptedForPlanning,
    Rejected,
    NeedsMoreEvidence
}

public enum LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public enum LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity
{
    Error,
    Warning,
    Info
}

public sealed record LmaxReadOnlyInstrumentSecurityIdConfirmationRecord(
    string RecordId,
    DateTimeOffset CreatedAtUtc,
    string Symbol,
    string SlashSymbol,
    string ProposedSecurityId,
    LmaxReadOnlyInstrumentSecurityIdSourceEvidenceType EvidenceSourceType,
    string EvidenceReference,
    string CapturedBy,
    string ReviewedBy,
    DateTimeOffset? ReviewedAtUtc,
    string ReviewReason,
    LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence Confidence,
    LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision Decision,
    bool IsApprovedForExternalRun,
    bool NoSensitiveContent,
    string Notes);

public sealed record LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue(
    LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity Severity,
    string Code,
    string Path,
    string Message);

public sealed record LmaxReadOnlyInstrumentSecurityIdConfirmationRecordResult(
    LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision Decision,
    LmaxReadOnlyInstrumentSecurityIdConfirmationRecord Record,
    IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> Issues)
{
    public IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error).ToArray();

    public IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> Warnings =>
        Issues.Where(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Warning).ToArray();
}

public sealed record LmaxReadOnlyInstrumentSecurityIdConfirmationRecordsReviewResult(
    LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision Decision,
    IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecord> Records,
    IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> Issues)
{
    public IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error).ToArray();

    public IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> Warnings =>
        Issues.Where(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Warning).ToArray();
}

public static class LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator
{
    private static readonly Regex SensitivePattern = new(
        "(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\\b553=|\\b554=|host=|user=)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AuthorizationPattern = new(
        "(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyInstrumentSecurityIdConfirmationRecordResult Validate(
        LmaxReadOnlyInstrumentSecurityIdConfirmationRecord record)
    {
        var issues = new List<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue>();
        var allowlistEntry = LmaxReadOnlyInstrumentAllowlist.CandidateEntries
            .FirstOrDefault(x => x.Symbol.Equals(record.Symbol, StringComparison.OrdinalIgnoreCase));
        var path = $"$.records[{record.Symbol}]";

        Require(record.RecordId, "RecordIdRequired", "$.recordId", "RecordId is required.", issues);
        Require(record.Symbol, "SymbolRequired", "$.symbol", "Symbol is required.", issues);
        Require(record.SlashSymbol, "SlashSymbolRequired", "$.slashSymbol", "SlashSymbol is required.", issues);
        Require(record.ProposedSecurityId, "ProposedSecurityIdRequired", "$.proposedSecurityId", "ProposedSecurityId is required.", issues);
        Require(record.EvidenceReference, "EvidenceReferenceRequired", "$.evidenceReference", "EvidenceReference is required.", issues);
        Require(record.CapturedBy, "CapturedByRequired", "$.capturedBy", "CapturedBy is required.", issues);

        if (allowlistEntry is null)
        {
            issues.Add(Error("UnknownSymbol", "$.symbol", $"{record.Symbol} is not in the Phase 6B allowlist."));
        }
        else if (!record.SlashSymbol.Equals(allowlistEntry.SlashSymbol, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("SlashSymbolMismatch", "$.slashSymbol", $"{record.Symbol} slash symbol must match the Phase 6B allowlist."));
        }

        if (record.IsApprovedForExternalRun)
        {
            issues.Add(Error("ExternalRunApprovalForbidden", "$.isApprovedForExternalRun", "Phase 6F confirmation records must keep IsApprovedForExternalRun=false."));
        }

        if (!record.NoSensitiveContent)
        {
            issues.Add(Error("SensitiveContentFlagFalse", "$.noSensitiveContent", "Confirmation records must assert noSensitiveContent=true."));
        }

        var combined = string.Join(
            " ",
            record.RecordId,
            record.Symbol,
            record.SlashSymbol,
            record.ProposedSecurityId,
            record.EvidenceReference,
            record.CapturedBy,
            record.ReviewedBy,
            record.ReviewReason,
            record.Notes);
        if (SensitivePattern.IsMatch(combined))
        {
            issues.Add(Error("SensitiveContentDetected", path, "Confirmation record contains credential-shaped or sensitive content."));
        }

        if (AuthorizationPattern.IsMatch(combined))
        {
            issues.Add(Error("TradingAuthorizationImplied", path, "Confirmation record must not imply order, trading, external run, Production, UAT, or execution authorization."));
        }

        if (record.Decision == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.AcceptedForPlanning)
        {
            Require(record.ReviewedBy, "ReviewedByRequired", "$.reviewedBy", "Accepted records require ReviewedBy.", issues);
            Require(record.ReviewReason, "ReviewReasonRequired", "$.reviewReason", "Accepted records require ReviewReason.", issues);

            if (record.ReviewedAtUtc is null)
            {
                issues.Add(Error("ReviewedAtRequired", "$.reviewedAtUtc", "Accepted records require ReviewedAtUtc."));
            }

            if (record.Confidence is not (LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence.High or LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence.Confirmed))
            {
                issues.Add(Error("ConfidenceTooLow", "$.confidence", "Accepted records require High or Confirmed confidence."));
            }

            if (IsPlaceholder(record.ProposedSecurityId))
            {
                issues.Add(Error("PlaceholderSecurityIdNotAccepted", "$.proposedSecurityId", "Accepted records cannot use PHASE6C, PHASE6D, or TBD placeholder SecurityIDs."));
            }
        }

        var errors = issues.Count(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error);
        var warnings = issues.Count(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Warning);
        var decision = errors > 0
            ? LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL
            : warnings > 0
                ? LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS_WITH_KNOWN_WARNINGS
                : LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS;

        return new(decision, record, issues);
    }

    public static LmaxReadOnlyInstrumentSecurityIdConfirmationRecordsReviewResult ReviewRecords(
        IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecord> records)
    {
        var issues = new List<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue>();
        var validationResults = records.Select(Validate).ToArray();

        foreach (var issue in validationResults.SelectMany(x => x.Issues))
        {
            issues.Add(issue);
        }

        foreach (var entry in LmaxReadOnlyInstrumentAllowlist.CandidateEntries)
        {
            var symbolRecords = records
                .Where(x => x.Symbol.Equals(entry.Symbol, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var acceptedRecords = symbolRecords
                .Where(x => x.Decision == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.AcceptedForPlanning)
                .ToArray();

            if (acceptedRecords.Length == 0)
            {
                issues.Add(Warning("AcceptedRecordMissing", $"$.records[{entry.Symbol}]", $"{entry.Symbol} does not yet have an AcceptedForPlanning confirmation record."));
            }

            var conflictingIds = acceptedRecords
                .Select(x => x.ProposedSecurityId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (conflictingIds.Length > 1)
            {
                issues.Add(Error("ConflictingProposedSecurityIds", $"$.records[{entry.Symbol}].proposedSecurityId", $"{entry.Symbol} has conflicting accepted proposed SecurityIDs."));
            }
        }

        var errors = issues.Count(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error);
        var warnings = issues.Count(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Warning);
        var decision = errors > 0
            ? LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL
            : warnings > 0
                ? LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS_WITH_KNOWN_WARNINGS
                : LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS;

        return new(decision, records, issues);
    }

    private static bool IsPlaceholder(string value)
        => value.StartsWith("PHASE6C-", StringComparison.OrdinalIgnoreCase)
           || value.StartsWith("PHASE6D-", StringComparison.OrdinalIgnoreCase)
           || value.StartsWith("TBD-", StringComparison.OrdinalIgnoreCase);

    private static void Require(
        string value,
        string code,
        string path,
        string message,
        List<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(Error(code, path, message));
        }
    }

    private static LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error, code, path, message);

    private static LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue Warning(string code, string path, string message)
        => new(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Warning, code, path, message);
}
