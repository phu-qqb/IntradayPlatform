using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyPerInstrumentSafetyGateDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyPerInstrumentSafetyGateCheck(
    string Name,
    LmaxReadOnlyPerInstrumentSafetyGateDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyPerInstrumentSafetyGateResult(
    string Symbol,
    string SlashSymbol,
    string PlanningSecurityId,
    string SecurityIdSource,
    string EnvironmentName,
    string VenueProfileName,
    LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision EvidenceDecision,
    bool IsApprovedForExternalRun,
    bool EligibleForManualSnapshotAttempt,
    bool NoSensitiveContent,
    IReadOnlyList<LmaxReadOnlyPerInstrumentSafetyGateCheck> Checks,
    LmaxReadOnlyPerInstrumentSafetyGateDecision FinalDecision);

public sealed record LmaxReadOnlyAdditionalInstrumentSafetyGateManifest(
    string ManifestId,
    DateTimeOffset CreatedAtUtc,
    string SourcePlanningManifestPath,
    IReadOnlyList<LmaxReadOnlyPerInstrumentSafetyGateResult> Instruments,
    int InstrumentCount,
    int PassCount,
    int WarningCount,
    int FailCount,
    bool AllApprovedForExternalRun,
    bool AnyEligibleForManualSnapshotAttempt,
    bool RuntimeShadowReplaySubmit,
    bool SchedulerOrPolling,
    bool OrderSubmission,
    bool GatewayRegistration,
    bool TradingMutation,
    bool ExternalConnectionAttempted,
    bool SecurityListRequestAttempted,
    bool MarketDataSnapshotAttempted,
    bool ReplayAttempted,
    bool NoSensitiveContent,
    LmaxReadOnlyPerInstrumentSafetyGateDecision FinalDecision);

public sealed record LmaxReadOnlyAdditionalInstrumentSafetyGateManifestValidationResult(
    LmaxReadOnlyPerInstrumentSafetyGateDecision Decision,
    LmaxReadOnlyAdditionalInstrumentSafetyGateManifest Manifest,
    IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> Issues)
{
    public IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error).ToArray();
}

public static class LmaxReadOnlyPerInstrumentSafetyGateValidator
{
    private static readonly Regex SensitivePattern = new(
        "(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\\b553=|\\b554=|host=|user=|account)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AuthorizationPattern = new(
        "(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyPerInstrumentSafetyGateResult Validate(
        LmaxReadOnlyInstrumentSecurityIdPlanningEntry entry)
    {
        var checks = new List<LmaxReadOnlyPerInstrumentSafetyGateCheck>
        {
            Check("HasAcceptedSecurityIdPlanningValue", HasSecurityId(entry.PlanningSecurityId), $"{entry.Symbol} has a non-placeholder planning SecurityID."),
            Check("HasSecurityIdSource8", entry.SecurityIdSource == "8", $"{entry.Symbol} uses SecurityIDSource=8."),
            Check("HasDemoEnvironment", entry.EnvironmentName == "Demo", $"{entry.Symbol} is scoped to Demo."),
            Check("HasDemoLondonVenueProfile", entry.VenueProfileName == "DemoLondon", $"{entry.Symbol} is scoped to DemoLondon."),
            Check("IsMarketDataOnly", true, $"{entry.Symbol} remains MarketDataOnly planning."),
            Check("IsNotApprovedForExternalRun", !entry.IsApprovedForExternalRun, $"{entry.Symbol} remains IsApprovedForExternalRun=false."),
            Check("NoOrderCapability", true, "No order capability is part of this per-instrument gate."),
            Check("NoRuntimeShadowReplaySubmit", true, "Runtime shadow replay submit remains forbidden."),
            Check("NoSchedulerOrPolling", true, "Scheduler and polling remain forbidden."),
            Check("NoTradingMutation", true, "Trading-state mutation remains forbidden."),
            Check("RequiresFutureExplicitOperatorPrompt", true, "A later explicit operator prompt is required before any manual snapshot attempt can be considered.")
        };

        if (entry.Decision != LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.AcceptedForPlanning)
        {
            checks.Add(Fail("EvidenceDecisionAcceptedForPlanning", $"{entry.Symbol} evidence decision must be AcceptedForPlanning."));
        }

        if (!entry.NoSensitiveContent)
        {
            checks.Add(Fail("NoSensitiveContent", $"{entry.Symbol} must assert noSensitiveContent=true."));
        }

        var combined = string.Join(" ", entry.Symbol, entry.SlashSymbol, entry.PlanningSecurityId, entry.SecurityIdSource, entry.EvidenceSource, entry.EvidenceReference, entry.ConfirmationRecordId, entry.Decision, entry.EnvironmentName, entry.VenueProfileName);
        if (SensitivePattern.IsMatch(combined))
        {
            checks.Add(Fail("NoSensitiveContent", $"{entry.Symbol} contains credential-shaped or sensitive content."));
        }

        if (AuthorizationPattern.IsMatch(combined))
        {
            checks.Add(Fail("NoTradingOrExternalAuthorizationLanguage", $"{entry.Symbol} must not imply order, trading, external run, Production, UAT, or execution authorization."));
        }

        var finalDecision = checks.Any(x => x.Decision == LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL)
            ? LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL
            : LmaxReadOnlyPerInstrumentSafetyGateDecision.PASS;

        return new(
            Symbol: entry.Symbol,
            SlashSymbol: entry.SlashSymbol,
            PlanningSecurityId: entry.PlanningSecurityId,
            SecurityIdSource: entry.SecurityIdSource,
            EnvironmentName: entry.EnvironmentName,
            VenueProfileName: entry.VenueProfileName,
            EvidenceDecision: entry.Decision,
            IsApprovedForExternalRun: entry.IsApprovedForExternalRun,
            EligibleForManualSnapshotAttempt: false,
            NoSensitiveContent: entry.NoSensitiveContent,
            Checks: checks,
            FinalDecision: finalDecision);
    }

    private static bool HasSecurityId(string value)
        => !string.IsNullOrWhiteSpace(value)
           && !value.StartsWith("PHASE6C-", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("PHASE6D-", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("TBD", StringComparison.OrdinalIgnoreCase)
           && !value.Contains("<REAL_DEMO_SECURITY_ID>", StringComparison.OrdinalIgnoreCase);

    private static LmaxReadOnlyPerInstrumentSafetyGateCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyPerInstrumentSafetyGateDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyPerInstrumentSafetyGateCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL, detail);
}

public static class LmaxReadOnlyAdditionalInstrumentSafetyGateManifestBuilder
{
    public static LmaxReadOnlyAdditionalInstrumentSafetyGateManifest FromPlanningManifest(
        LmaxReadOnlyInstrumentSecurityIdPlanningManifest planningManifest,
        string sourcePlanningManifestPath,
        DateTimeOffset? createdAtUtc = null)
    {
        var gates = planningManifest.Instruments
            .Select(LmaxReadOnlyPerInstrumentSafetyGateValidator.Validate)
            .ToArray();
        var stamp = createdAtUtc ?? DateTimeOffset.UtcNow;
        var passCount = gates.Count(x => x.FinalDecision == LmaxReadOnlyPerInstrumentSafetyGateDecision.PASS);
        var warningCount = gates.Count(x => x.FinalDecision == LmaxReadOnlyPerInstrumentSafetyGateDecision.PASS_WITH_KNOWN_WARNINGS);
        var failCount = gates.Count(x => x.FinalDecision == LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL);
        var finalDecision = failCount > 0
            ? LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL
            : warningCount > 0
                ? LmaxReadOnlyPerInstrumentSafetyGateDecision.PASS_WITH_KNOWN_WARNINGS
                : LmaxReadOnlyPerInstrumentSafetyGateDecision.PASS;

        return new(
            ManifestId: $"lmax-readonly-additional-instrument-safety-gates-{stamp:yyyyMMdd-HHmmss}",
            CreatedAtUtc: stamp,
            SourcePlanningManifestPath: sourcePlanningManifestPath,
            Instruments: gates,
            InstrumentCount: gates.Length,
            PassCount: passCount,
            WarningCount: warningCount,
            FailCount: failCount,
            AllApprovedForExternalRun: gates.All(x => x.IsApprovedForExternalRun),
            AnyEligibleForManualSnapshotAttempt: gates.Any(x => x.EligibleForManualSnapshotAttempt),
            RuntimeShadowReplaySubmit: planningManifest.RuntimeShadowReplaySubmit,
            SchedulerOrPolling: planningManifest.SchedulerOrPollingAdded,
            OrderSubmission: planningManifest.OrderSubmissionAdded,
            GatewayRegistration: planningManifest.GatewayRegistrationAdded,
            TradingMutation: planningManifest.TradingMutationAdded,
            ExternalConnectionAttempted: planningManifest.ExternalConnectionAttempted,
            SecurityListRequestAttempted: planningManifest.SecurityListRequestAttempted,
            MarketDataSnapshotAttempted: planningManifest.MarketDataSnapshotAttempted,
            ReplayAttempted: planningManifest.ReplayAttempted,
            NoSensitiveContent: planningManifest.NoSensitiveContent && gates.All(x => x.NoSensitiveContent),
            FinalDecision: finalDecision);
    }
}

public static class LmaxReadOnlyAdditionalInstrumentSafetyGateManifestValidator
{
    public static LmaxReadOnlyAdditionalInstrumentSafetyGateManifestValidationResult Validate(
        LmaxReadOnlyAdditionalInstrumentSafetyGateManifest manifest)
    {
        var issues = new List<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue>();
        foreach (var candidate in LmaxReadOnlyInstrumentAllowlist.CandidateEntries)
        {
            var entries = manifest.Instruments.Where(x => x.Symbol.Equals(candidate.Symbol, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (entries.Length == 0)
            {
                issues.Add(Error("SafetyGateMissing", $"$.instruments[{candidate.Symbol}]", $"{candidate.Symbol} is missing from the Phase 6O safety gate manifest."));
                continue;
            }

            if (entries.Length > 1)
            {
                issues.Add(Error("DuplicateSafetyGate", $"$.instruments[{candidate.Symbol}]", $"{candidate.Symbol} appears multiple times in the Phase 6O safety gate manifest."));
            }

            var entry = entries[0];
            if (entry.FinalDecision == LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL)
            {
                issues.Add(Error("InstrumentSafetyGateFailed", $"$.instruments[{candidate.Symbol}]", $"{candidate.Symbol} has a failing per-instrument safety gate."));
            }

            if (entry.IsApprovedForExternalRun)
            {
                issues.Add(Error("ExternalRunApprovalForbidden", $"$.instruments[{candidate.Symbol}].isApprovedForExternalRun", $"{candidate.Symbol} must remain IsApprovedForExternalRun=false."));
            }

            if (entry.EligibleForManualSnapshotAttempt)
            {
                issues.Add(Error("ManualSnapshotEligibilityForbidden", $"$.instruments[{candidate.Symbol}].eligibleForManualSnapshotAttempt", $"{candidate.Symbol} must remain eligibleForManualSnapshotAttempt=false in Phase 6O."));
            }
        }

        if (manifest.InstrumentCount != LmaxReadOnlyInstrumentAllowlist.CandidateEntries.Count)
        {
            issues.Add(Error("InstrumentCountInvalid", "$.instrumentCount", "Phase 6O safety gate manifest must include all four candidate instruments."));
        }

        if (manifest.AllApprovedForExternalRun)
        {
            issues.Add(Error("AggregateExternalRunApprovalForbidden", "$.allApprovedForExternalRun", "Aggregate safety gate must not approve external runs."));
        }

        if (manifest.AnyEligibleForManualSnapshotAttempt)
        {
            issues.Add(Error("AggregateManualSnapshotEligibilityForbidden", "$.anyEligibleForManualSnapshotAttempt", "Phase 6O must not make any instrument eligible for a manual snapshot attempt."));
        }

        if (manifest.RuntimeShadowReplaySubmit) issues.Add(Error("RuntimeShadowReplaySubmitForbidden", "$.runtimeShadowReplaySubmit", "Runtime shadow replay submit must remain false."));
        if (manifest.SchedulerOrPolling) issues.Add(Error("SchedulerPollingForbidden", "$.schedulerOrPolling", "Scheduler/polling must remain false."));
        if (manifest.OrderSubmission) issues.Add(Error("OrderSubmissionForbidden", "$.orderSubmission", "Order submission must remain false."));
        if (manifest.GatewayRegistration) issues.Add(Error("GatewayRegistrationForbidden", "$.gatewayRegistration", "Gateway registration must remain false."));
        if (manifest.TradingMutation) issues.Add(Error("TradingMutationForbidden", "$.tradingMutation", "Trading mutation must remain false."));
        if (manifest.ExternalConnectionAttempted) issues.Add(Error("ExternalConnectionForbidden", "$.externalConnectionAttempted", "Phase 6O must not connect to LMAX."));
        if (manifest.SecurityListRequestAttempted) issues.Add(Error("SecurityListRequestForbidden", "$.securityListRequestAttempted", "Phase 6O must not run SecurityListRequest."));
        if (manifest.MarketDataSnapshotAttempted) issues.Add(Error("MarketDataSnapshotForbidden", "$.marketDataSnapshotAttempted", "Phase 6O must not run snapshots."));
        if (manifest.ReplayAttempted) issues.Add(Error("ReplayForbidden", "$.replayAttempted", "Phase 6O must not run replay."));
        if (!manifest.NoSensitiveContent) issues.Add(Error("NoSensitiveContentFalse", "$.noSensitiveContent", "Phase 6O manifest must assert noSensitiveContent=true."));

        var decision = issues.Count == 0
            ? LmaxReadOnlyPerInstrumentSafetyGateDecision.PASS
            : LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL;
        return new(decision, manifest, issues);
    }

    private static LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error, code, path, message);
}
