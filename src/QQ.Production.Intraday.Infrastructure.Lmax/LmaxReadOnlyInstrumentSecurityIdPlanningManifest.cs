using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxReadOnlyInstrumentSecurityIdPlanningEntry(
    string Symbol,
    string SlashSymbol,
    string PlanningSecurityId,
    string SecurityIdSource,
    string EvidenceSource,
    string EvidenceReference,
    string ConfirmationRecordId,
    string ConfirmationRecordPath,
    LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision Decision,
    bool IsApprovedForExternalRun,
    string EnvironmentName,
    string VenueProfileName,
    bool NoSensitiveContent);

public sealed record LmaxReadOnlyInstrumentSecurityIdPlanningManifest(
    string ManifestId,
    DateTimeOffset CreatedAtUtc,
    string EnvironmentName,
    string VenueProfileName,
    IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdPlanningEntry> Instruments,
    bool IsApprovedForExternalRun,
    bool ExternalConnectionAttempted,
    bool ExternalApiCallAttempted,
    bool SecurityListRequestAttempted,
    bool MarketDataSnapshotAttempted,
    bool ReplayAttempted,
    bool RuntimeShadowReplaySubmit,
    bool SchedulerOrPollingAdded,
    bool OrderSubmissionAdded,
    bool GatewayRegistrationAdded,
    bool TradingMutationAdded,
    bool NoSensitiveContent);

public sealed record LmaxReadOnlyInstrumentSecurityIdPlanningManifestValidationResult(
    LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision Decision,
    LmaxReadOnlyInstrumentSecurityIdPlanningManifest Manifest,
    IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> Issues)
{
    public IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error).ToArray();
}

public static class LmaxReadOnlyInstrumentSecurityIdPlanningManifestBuilder
{
    public static LmaxReadOnlyInstrumentSecurityIdPlanningManifest FromAcceptedRecords(
        IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecord> records,
        IReadOnlyDictionary<string, string>? recordPaths = null,
        DateTimeOffset? createdAtUtc = null,
        string environmentName = "Demo",
        string venueProfileName = "DemoLondon")
    {
        var accepted = records
            .Where(x => x.Decision == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.AcceptedForPlanning)
            .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(x => x.ReviewedAtUtc ?? x.CreatedAtUtc).First())
            .ToArray();

        var entries = LmaxReadOnlyInstrumentAllowlist.CandidateEntries
            .Select(candidate =>
            {
                var record = accepted.FirstOrDefault(x => x.Symbol.Equals(candidate.Symbol, StringComparison.OrdinalIgnoreCase));
                return new LmaxReadOnlyInstrumentSecurityIdPlanningEntry(
                    Symbol: candidate.Symbol,
                    SlashSymbol: candidate.SlashSymbol,
                    PlanningSecurityId: record?.ProposedSecurityId ?? string.Empty,
                    SecurityIdSource: "8",
                    EvidenceSource: record?.EvidenceSourceType.ToString() ?? string.Empty,
                    EvidenceReference: record?.EvidenceReference ?? string.Empty,
                    ConfirmationRecordId: record?.RecordId ?? string.Empty,
                    ConfirmationRecordPath: record is not null && recordPaths is not null && recordPaths.TryGetValue(record.RecordId, out var path) ? path : string.Empty,
                    Decision: record?.Decision ?? LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.NeedsMoreEvidence,
                    IsApprovedForExternalRun: false,
                    EnvironmentName: environmentName,
                    VenueProfileName: venueProfileName,
                    NoSensitiveContent: record?.NoSensitiveContent ?? true);
            })
            .ToArray();

        var stamp = createdAtUtc ?? DateTimeOffset.UtcNow;
        return new(
            ManifestId: $"lmax-readonly-securityid-planning-manifest-{stamp:yyyyMMdd-HHmmss}",
            CreatedAtUtc: stamp,
            EnvironmentName: environmentName,
            VenueProfileName: venueProfileName,
            Instruments: entries,
            IsApprovedForExternalRun: false,
            ExternalConnectionAttempted: false,
            ExternalApiCallAttempted: false,
            SecurityListRequestAttempted: false,
            MarketDataSnapshotAttempted: false,
            ReplayAttempted: false,
            RuntimeShadowReplaySubmit: false,
            SchedulerOrPollingAdded: false,
            OrderSubmissionAdded: false,
            GatewayRegistrationAdded: false,
            TradingMutationAdded: false,
            NoSensitiveContent: true);
    }
}

public static class LmaxReadOnlyInstrumentSecurityIdPlanningManifestValidator
{
    private static readonly Regex SensitivePattern = new(
        "(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\\b553=|\\b554=|host=|user=|account)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AuthorizationPattern = new(
        "(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyInstrumentSecurityIdPlanningManifestValidationResult Validate(
        LmaxReadOnlyInstrumentSecurityIdPlanningManifest manifest,
        IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecord>? records = null)
    {
        var issues = new List<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue>();
        if (manifest.EnvironmentName != "Demo")
        {
            issues.Add(Error("EnvironmentMustBeDemo", "$.environmentName", "Planning manifest environmentName must be Demo."));
        }

        if (manifest.VenueProfileName != "DemoLondon")
        {
            issues.Add(Error("VenueProfileMustBeDemoLondon", "$.venueProfileName", "Planning manifest venueProfileName must be DemoLondon."));
        }

        if (manifest.IsApprovedForExternalRun)
        {
            issues.Add(Error("ExternalRunApprovalForbidden", "$.isApprovedForExternalRun", "Planning manifest must keep IsApprovedForExternalRun=false."));
        }

        ValidateNoRuntimeFlags(manifest, issues);

        foreach (var candidate in LmaxReadOnlyInstrumentAllowlist.CandidateEntries)
        {
            var entries = manifest.Instruments.Where(x => x.Symbol.Equals(candidate.Symbol, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (entries.Length == 0)
            {
                issues.Add(Error("PlanningEntryMissing", $"$.instruments[{candidate.Symbol}]", $"{candidate.Symbol} is missing from the planning manifest."));
                continue;
            }

            if (entries.Length > 1)
            {
                issues.Add(Error("DuplicatePlanningEntry", $"$.instruments[{candidate.Symbol}]", $"{candidate.Symbol} appears multiple times in the planning manifest."));
            }

            var entry = entries[0];
            if (!entry.SlashSymbol.Equals(candidate.SlashSymbol, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("SlashSymbolMismatch", $"$.instruments[{candidate.Symbol}].slashSymbol", $"{candidate.Symbol} slash symbol must match the allowlist."));
            }

            if (string.IsNullOrWhiteSpace(entry.PlanningSecurityId) || IsPlaceholder(entry.PlanningSecurityId))
            {
                issues.Add(Error("PlanningSecurityIdInvalid", $"$.instruments[{candidate.Symbol}].planningSecurityId", $"{candidate.Symbol} must have a non-placeholder planning SecurityID."));
            }

            if (entry.SecurityIdSource != "8")
            {
                issues.Add(Error("SecurityIdSourceInvalid", $"$.instruments[{candidate.Symbol}].securityIdSource", $"{candidate.Symbol} must use SecurityIDSource=8."));
            }

            if (entry.Decision != LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.AcceptedForPlanning)
            {
                issues.Add(Error("DecisionMustBeAcceptedForPlanning", $"$.instruments[{candidate.Symbol}].decision", $"{candidate.Symbol} must be AcceptedForPlanning."));
            }

            if (entry.IsApprovedForExternalRun)
            {
                issues.Add(Error("InstrumentExternalRunApprovalForbidden", $"$.instruments[{candidate.Symbol}].isApprovedForExternalRun", $"{candidate.Symbol} must keep IsApprovedForExternalRun=false."));
            }

            if (entry.EnvironmentName != "Demo" || entry.VenueProfileName != "DemoLondon")
            {
                issues.Add(Error("InstrumentProfileInvalid", $"$.instruments[{candidate.Symbol}]", $"{candidate.Symbol} must be scoped to Demo/DemoLondon."));
            }

            if (!entry.NoSensitiveContent)
            {
                issues.Add(Error("NoSensitiveContentFalse", $"$.instruments[{candidate.Symbol}].noSensitiveContent", $"{candidate.Symbol} must assert noSensitiveContent=true."));
            }

            var combined = string.Join(" ", entry.Symbol, entry.SlashSymbol, entry.PlanningSecurityId, entry.EvidenceSource, entry.EvidenceReference, entry.ConfirmationRecordId);
            if (SensitivePattern.IsMatch(combined))
            {
                issues.Add(Error("SensitiveContentDetected", $"$.instruments[{candidate.Symbol}]", $"{candidate.Symbol} planning entry contains sensitive-shaped content."));
            }

            if (AuthorizationPattern.IsMatch(combined))
            {
                issues.Add(Error("TradingAuthorizationImplied", $"$.instruments[{candidate.Symbol}]", $"{candidate.Symbol} planning entry must not imply order, trading, external run, Production, UAT, or execution authorization."));
            }

            if (records is not null)
            {
                var matchingRecords = records
                    .Where(x => x.Symbol.Equals(candidate.Symbol, StringComparison.OrdinalIgnoreCase)
                                && x.Decision == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.AcceptedForPlanning)
                    .ToArray();
                if (matchingRecords.Length == 0)
                {
                    issues.Add(Error("AcceptedRecordMissing", $"$.records[{candidate.Symbol}]", $"{candidate.Symbol} does not have an accepted confirmation record."));
                }
                else if (matchingRecords.Select(x => x.ProposedSecurityId).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                {
                    issues.Add(Error("ConflictingAcceptedRecords", $"$.records[{candidate.Symbol}]", $"{candidate.Symbol} has conflicting accepted confirmation records."));
                }
                else if (!matchingRecords[0].ProposedSecurityId.Equals(entry.PlanningSecurityId, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(Error("PlanningValueDoesNotMatchRecord", $"$.instruments[{candidate.Symbol}].planningSecurityId", $"{candidate.Symbol} planning value does not match its accepted confirmation record."));
                }
            }
        }

        var decision = issues.Any(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error)
            ? LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL
            : LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS;

        return new(decision, manifest, issues);
    }

    private static void ValidateNoRuntimeFlags(
        LmaxReadOnlyInstrumentSecurityIdPlanningManifest manifest,
        List<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> issues)
    {
        if (manifest.ExternalConnectionAttempted) issues.Add(Error("ExternalConnectionForbidden", "$.externalConnectionAttempted", "Phase 6N must not connect to LMAX."));
        if (manifest.ExternalApiCallAttempted) issues.Add(Error("ExternalApiCallForbidden", "$.externalApiCallAttempted", "Phase 6N must not call external APIs."));
        if (manifest.SecurityListRequestAttempted) issues.Add(Error("SecurityListRequestForbidden", "$.securityListRequestAttempted", "Phase 6N must not run SecurityListRequest."));
        if (manifest.MarketDataSnapshotAttempted) issues.Add(Error("MarketDataSnapshotForbidden", "$.marketDataSnapshotAttempted", "Phase 6N must not run snapshots."));
        if (manifest.ReplayAttempted) issues.Add(Error("ReplayForbidden", "$.replayAttempted", "Phase 6N must not replay evidence."));
        if (manifest.RuntimeShadowReplaySubmit) issues.Add(Error("RuntimeShadowReplaySubmitForbidden", "$.runtimeShadowReplaySubmit", "Phase 6N must not submit to shadow replay."));
        if (manifest.SchedulerOrPollingAdded) issues.Add(Error("SchedulerPollingForbidden", "$.schedulerOrPollingAdded", "Phase 6N must not add scheduler or polling."));
        if (manifest.OrderSubmissionAdded) issues.Add(Error("OrderSubmissionForbidden", "$.orderSubmissionAdded", "Phase 6N must not add order submission."));
        if (manifest.GatewayRegistrationAdded) issues.Add(Error("GatewayRegistrationForbidden", "$.gatewayRegistrationAdded", "Phase 6N must not add gateway registration."));
        if (manifest.TradingMutationAdded) issues.Add(Error("TradingMutationForbidden", "$.tradingMutationAdded", "Phase 6N must not mutate trading state."));
        if (!manifest.NoSensitiveContent) issues.Add(Error("NoSensitiveContentFalse", "$.noSensitiveContent", "Planning manifest must assert noSensitiveContent=true."));
    }

    private static bool IsPlaceholder(string value)
        => value.StartsWith("PHASE6C-", StringComparison.OrdinalIgnoreCase)
           || value.StartsWith("PHASE6D-", StringComparison.OrdinalIgnoreCase)
           || value.StartsWith("TBD", StringComparison.OrdinalIgnoreCase)
           || value.Contains("<REAL_DEMO_SECURITY_ID>", StringComparison.OrdinalIgnoreCase);

    private static LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error, code, path, message);
}
