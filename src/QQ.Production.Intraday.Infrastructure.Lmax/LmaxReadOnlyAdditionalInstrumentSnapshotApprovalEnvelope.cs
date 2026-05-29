using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision
{
    Draft,
    AcceptedForPlanning,
    Rejected,
    Invalid,
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope(
    string ApprovalEnvelopeId,
    DateTimeOffset CreatedAtUtc,
    string RequestedByOperatorId,
    string ReviewedByOperatorId,
    string Reason,
    string Symbol,
    string SlashSymbol,
    string PlanningSecurityId,
    string SecurityIdSource,
    string EnvironmentName,
    string VenueProfileName,
    string RequestMode,
    string SymbolEncodingMode,
    int MarketDepth,
    int MaxRuntimeSeconds,
    int MaxWaitSeconds,
    int MaxEventsPerRun,
    string SourcePreflightManifestPath,
    string SourcePreflightDecision,
    bool ConfirmsDemoOnly,
    bool ConfirmsReadOnlyMarketDataOnly,
    bool ConfirmsNoOrderSubmission,
    bool ConfirmsNoSchedulerOrPolling,
    bool ConfirmsNoRuntimeShadowReplaySubmit,
    bool ConfirmsNoTradingMutation,
    bool ConfirmsSingleInstrumentOnly,
    bool ConfirmsFutureExplicitManualRunRequired,
    bool IsApprovedForExternalRun,
    bool EligibleForManualSnapshotAttempt,
    bool CanRunExternalSnapshot,
    bool NoSensitiveContent,
    LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision Decision);

public sealed record LmaxReadOnlyAdditionalInstrumentSnapshotApprovalCheck(
    string Name,
    LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyAdditionalInstrumentSnapshotApprovalResult(
    LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision FinalDecision,
    LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope Envelope,
    IReadOnlyList<LmaxReadOnlyAdditionalInstrumentSnapshotApprovalCheck> Checks)
{
    public IReadOnlyList<LmaxReadOnlyAdditionalInstrumentSnapshotApprovalCheck> Errors =>
        Checks.Where(x => x.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.FAIL).ToArray();
}

public sealed record LmaxReadOnlyAdditionalInstrumentSnapshotApprovalReviewResult(
    LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision FinalDecision,
    IReadOnlyList<LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope> Envelopes,
    IReadOnlyList<LmaxReadOnlyAdditionalInstrumentSnapshotApprovalCheck> Issues,
    int TotalEnvelopeCount,
    int AcceptedForPlanningCount,
    int ConflictCount,
    int InvalidEnvelopeCount);

public static class LmaxReadOnlyAdditionalInstrumentSnapshotApprovalValidator
{
    private static readonly Regex SensitivePattern = new(
        "(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\\b553=|\\b554=|host=|user=|account)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AuthorizationPattern = new(
        "(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyAdditionalInstrumentSnapshotApprovalResult Validate(
        LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope envelope,
        LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifest preflightManifest)
    {
        var source = preflightManifest.Results.FirstOrDefault(x => x.Symbol.Equals(envelope.Symbol, StringComparison.OrdinalIgnoreCase));
        var checks = new List<LmaxReadOnlyAdditionalInstrumentSnapshotApprovalCheck>
        {
            Check("SymbolExistsInPreflightManifest", source is not null, $"{envelope.Symbol} exists in the Phase 6P preflight manifest."),
            Check("SourcePreflightDecisionPass", source is not null && source.FinalDecision == LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.PASS && envelope.SourcePreflightDecision == "PASS", "Source preflight decision must be PASS."),
            Check("SlashSymbolMatches", source is not null && envelope.SlashSymbol.Equals(source.SlashSymbol, StringComparison.OrdinalIgnoreCase), "Slash symbol must match source preflight."),
            Check("PlanningSecurityIdMatches", source is not null && envelope.PlanningSecurityId.Equals(source.PlanningSecurityId, StringComparison.OrdinalIgnoreCase) && HasSecurityId(envelope.PlanningSecurityId), "Planning SecurityID must match source preflight and be non-placeholder."),
            Check("SecurityIdSource8", envelope.SecurityIdSource == "8", "SecurityIDSource must be 8."),
            Check("DemoEnvironment", envelope.EnvironmentName == "Demo", "Environment must be Demo."),
            Check("DemoLondonVenueProfile", envelope.VenueProfileName == "DemoLondon", "Venue profile must be DemoLondon."),
            Check("RequestModeSnapshotPlusUpdates", envelope.RequestMode == "SnapshotPlusUpdates", "Request mode must be SnapshotPlusUpdates."),
            Check("SymbolEncodingModeSecurityIdOnly", envelope.SymbolEncodingMode == "SecurityIdOnly", "Symbol encoding mode must be SecurityIdOnly."),
            Check("MarketDepthOne", envelope.MarketDepth == 1, "MarketDepth must be 1."),
            Check("MaxRuntimeSecondsSafeCap", envelope.MaxRuntimeSeconds is >= 1 and <= 30, "MaxRuntimeSeconds must be 1..30."),
            Check("MaxWaitSecondsSafeCap", envelope.MaxWaitSeconds is >= 1 and <= 30, "MaxWaitSeconds must be 1..30."),
            Check("MaxEventsPerRunSafeCap", envelope.MaxEventsPerRun is >= 1 and <= 25, "MaxEventsPerRun must be 1..25."),
            Check("RequestedByRequired", !string.IsNullOrWhiteSpace(envelope.RequestedByOperatorId), "RequestedByOperatorId is required."),
            Check("ReasonRequired", !string.IsNullOrWhiteSpace(envelope.Reason), "Reason is required."),
            Check("IsApprovedForExternalRunFalse", !envelope.IsApprovedForExternalRun, "IsApprovedForExternalRun must remain false."),
            Check("EligibleForManualSnapshotAttemptFalse", !envelope.EligibleForManualSnapshotAttempt, "eligibleForManualSnapshotAttempt must remain false."),
            Check("CanRunExternalSnapshotFalse", !envelope.CanRunExternalSnapshot, "canRunExternalSnapshot must remain false."),
            Check("NoSensitiveContentTrue", envelope.NoSensitiveContent, "noSensitiveContent must be true.")
        };

        if (envelope.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.AcceptedForPlanning)
        {
            checks.Add(Check("ReviewedByRequired", !string.IsNullOrWhiteSpace(envelope.ReviewedByOperatorId), "AcceptedForPlanning envelopes require ReviewedByOperatorId."));
            checks.Add(Check("ConfirmsDemoOnly", envelope.ConfirmsDemoOnly, "AcceptedForPlanning requires Demo-only attestation."));
            checks.Add(Check("ConfirmsReadOnlyMarketDataOnly", envelope.ConfirmsReadOnlyMarketDataOnly, "AcceptedForPlanning requires read-only MarketData-only attestation."));
            checks.Add(Check("ConfirmsNoOrderSubmission", envelope.ConfirmsNoOrderSubmission, "AcceptedForPlanning requires no-order attestation."));
            checks.Add(Check("ConfirmsNoSchedulerOrPolling", envelope.ConfirmsNoSchedulerOrPolling, "AcceptedForPlanning requires no scheduler/polling attestation."));
            checks.Add(Check("ConfirmsNoRuntimeShadowReplaySubmit", envelope.ConfirmsNoRuntimeShadowReplaySubmit, "AcceptedForPlanning requires no runtime shadow replay submit attestation."));
            checks.Add(Check("ConfirmsNoTradingMutation", envelope.ConfirmsNoTradingMutation, "AcceptedForPlanning requires no trading mutation attestation."));
            checks.Add(Check("ConfirmsSingleInstrumentOnly", envelope.ConfirmsSingleInstrumentOnly, "AcceptedForPlanning requires single-instrument attestation."));
            checks.Add(Check("ConfirmsFutureExplicitManualRunRequired", envelope.ConfirmsFutureExplicitManualRunRequired, "AcceptedForPlanning requires future explicit manual run attestation."));
        }

        var combined = string.Join(" ", envelope.ApprovalEnvelopeId, envelope.RequestedByOperatorId, envelope.ReviewedByOperatorId, envelope.Reason, envelope.Symbol, envelope.SlashSymbol, envelope.PlanningSecurityId, envelope.SecurityIdSource, envelope.EnvironmentName, envelope.VenueProfileName, envelope.RequestMode, envelope.SymbolEncodingMode, envelope.Decision);
        if (SensitivePattern.IsMatch(combined))
        {
            checks.Add(Fail("NoSensitiveContent", "Approval envelope contains credential-shaped or sensitive content."));
        }

        if (AuthorizationPattern.IsMatch(combined))
        {
            checks.Add(Fail("NoTradingOrExternalAuthorizationLanguage", "Approval envelope must not imply order, trading, external run, Production, UAT, or execution authorization."));
        }

        var final = checks.Any(x => x.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.FAIL)
            ? LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.FAIL
            : LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.PASS;
        return new(final, envelope with
        {
            IsApprovedForExternalRun = false,
            EligibleForManualSnapshotAttempt = false,
            CanRunExternalSnapshot = false
        }, checks);
    }

    public static LmaxReadOnlyAdditionalInstrumentSnapshotApprovalReviewResult Review(
        IReadOnlyList<LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope> envelopes,
        LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifest preflightManifest)
    {
        var issues = new List<LmaxReadOnlyAdditionalInstrumentSnapshotApprovalCheck>();
        var validation = envelopes.Select(x => Validate(x, preflightManifest)).ToArray();
        foreach (var issue in validation.SelectMany(x => x.Checks).Where(x => x.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.FAIL))
        {
            issues.Add(issue);
        }

        var accepted = envelopes.Where(x => x.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.AcceptedForPlanning).ToArray();
        var conflicts = accepted.GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase).Where(x => x.Select(y => y.PlanningSecurityId).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1).ToArray();
        foreach (var conflict in conflicts)
        {
            issues.Add(Fail("ConflictingAcceptedEnvelopes", $"{conflict.Key} has conflicting accepted approval envelopes."));
        }

        var invalidCount = validation.Count(x => x.FinalDecision == LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.FAIL);
        var final = issues.Count > 0 || invalidCount > 0 || conflicts.Length > 0
            ? LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.FAIL
            : accepted.Length > 0
                ? LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.PASS
                : LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.PASS_WITH_KNOWN_WARNINGS;

        return new(final, envelopes, issues, envelopes.Count, accepted.Length, conflicts.Length, invalidCount);
    }

    private static bool HasSecurityId(string value)
        => !string.IsNullOrWhiteSpace(value)
           && !value.StartsWith("PHASE6C-", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("PHASE6D-", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("TBD", StringComparison.OrdinalIgnoreCase);

    private static LmaxReadOnlyAdditionalInstrumentSnapshotApprovalCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyAdditionalInstrumentSnapshotApprovalCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.FAIL, detail);
}
