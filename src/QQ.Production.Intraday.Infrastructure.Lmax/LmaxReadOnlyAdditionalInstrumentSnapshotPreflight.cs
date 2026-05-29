using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyAdditionalInstrumentSnapshotPreflightRequest(
    string PreflightId,
    DateTimeOffset CreatedAtUtc,
    string RequestedByOperatorId,
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
    bool AllowExternalConnections,
    bool ConfirmDemoReadOnly,
    bool AllowOrderSubmission,
    bool SchedulerEnabled,
    bool SubmitToShadowReplay,
    bool PersistToTradingTables,
    bool IsApprovedForExternalRun,
    bool EligibleForManualSnapshotAttempt,
    bool CanRunExternalSnapshot,
    bool NoSensitiveContent);

public sealed record LmaxReadOnlyAdditionalInstrumentSnapshotPreflightCheck(
    string Name,
    LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyAdditionalInstrumentSnapshotPreflightResult(
    string PreflightId,
    string Symbol,
    string SlashSymbol,
    string PlanningSecurityId,
    IReadOnlyList<LmaxReadOnlyAdditionalInstrumentSnapshotPreflightCheck> Checks,
    LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision FinalDecision,
    bool CanRunExternalSnapshot,
    bool RequiresFutureExplicitOperatorPrompt,
    bool IsApprovedForExternalRun,
    bool EligibleForManualSnapshotAttempt,
    bool NoSensitiveContent);

public sealed record LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifest(
    string ManifestId,
    DateTimeOffset CreatedAtUtc,
    string SourcePlanningManifestPath,
    string SourceSafetyGateManifestPath,
    IReadOnlyList<LmaxReadOnlyAdditionalInstrumentSnapshotPreflightRequest> Requests,
    IReadOnlyList<LmaxReadOnlyAdditionalInstrumentSnapshotPreflightResult> Results,
    int InstrumentCount,
    int PassCount,
    int WarningCount,
    int FailCount,
    bool AnyCanRunExternalSnapshot,
    bool AnyApprovedForExternalRun,
    bool AnyEligibleForManualSnapshotAttempt,
    bool RuntimeShadowReplaySubmit,
    bool SchedulerOrPolling,
    bool OrderSubmission,
    bool TradingTablePersistence,
    bool GatewayRegistration,
    bool TradingMutation,
    bool ExternalConnectionAttempted,
    bool SecurityListRequestAttempted,
    bool MarketDataSnapshotAttempted,
    bool ReplayAttempted,
    bool NoSensitiveContent,
    LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision FinalDecision);

public sealed record LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifestValidationResult(
    LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision Decision,
    LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifest Manifest,
    IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> Issues)
{
    public IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error).ToArray();
}

public static class LmaxReadOnlyAdditionalInstrumentSnapshotPreflightValidator
{
    private static readonly Regex SensitivePattern = new(
        "(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\\b553=|\\b554=|host=|user=|account)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AuthorizationPattern = new(
        "(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyAdditionalInstrumentSnapshotPreflightResult Validate(
        LmaxReadOnlyAdditionalInstrumentSnapshotPreflightRequest request,
        LmaxReadOnlyInstrumentSecurityIdPlanningManifest planningManifest,
        LmaxReadOnlyAdditionalInstrumentSafetyGateManifest safetyGateManifest)
    {
        var planningEntry = planningManifest.Instruments.FirstOrDefault(x => x.Symbol.Equals(request.Symbol, StringComparison.OrdinalIgnoreCase));
        var safetyGate = safetyGateManifest.Instruments.FirstOrDefault(x => x.Symbol.Equals(request.Symbol, StringComparison.OrdinalIgnoreCase));
        var checks = new List<LmaxReadOnlyAdditionalInstrumentSnapshotPreflightCheck>
        {
            Check("OperatorIdRequired", !string.IsNullOrWhiteSpace(request.RequestedByOperatorId), "Operator id is required."),
            Check("ReasonRequired", !string.IsNullOrWhiteSpace(request.Reason), "Reason is required."),
            Check("SymbolExistsInSafetyGateManifest", safetyGate is not null, $"{request.Symbol} exists in the Phase 6O safety gate manifest."),
            Check("SlashSymbolMatchesPlanningManifest", planningEntry is not null && request.SlashSymbol.Equals(planningEntry.SlashSymbol, StringComparison.OrdinalIgnoreCase), $"{request.Symbol} slash symbol matches planning manifest."),
            Check("PlanningSecurityIdMatches", planningEntry is not null && request.PlanningSecurityId.Equals(planningEntry.PlanningSecurityId, StringComparison.OrdinalIgnoreCase) && HasSecurityId(request.PlanningSecurityId), $"{request.Symbol} planning SecurityID matches Phase 6N."),
            Check("SecurityIdSource8", request.SecurityIdSource == "8", "SecurityIDSource must be 8."),
            Check("DemoEnvironment", request.EnvironmentName == "Demo", "Environment must be Demo."),
            Check("DemoLondonVenueProfile", request.VenueProfileName == "DemoLondon", "Venue profile must be DemoLondon."),
            Check("RequestModeSnapshotPlusUpdates", request.RequestMode == "SnapshotPlusUpdates", "Request mode must be SnapshotPlusUpdates."),
            Check("SymbolEncodingModeSecurityIdOnly", request.SymbolEncodingMode == "SecurityIdOnly", "Symbol encoding mode must be SecurityIdOnly."),
            Check("MarketDepthOne", request.MarketDepth == 1, "MarketDepth must be 1."),
            Check("MaxRuntimeSecondsSafeCap", request.MaxRuntimeSeconds is >= 1 and <= 30, "MaxRuntimeSeconds must be 1..30."),
            Check("MaxWaitSecondsSafeCap", request.MaxWaitSeconds is >= 1 and <= 30, "MaxWaitSeconds must be 1..30."),
            Check("MaxEventsPerRunSafeCap", request.MaxEventsPerRun is >= 1 and <= 25, "MaxEventsPerRun must be 1..25."),
            Check("AllowExternalConnectionsFalse", !request.AllowExternalConnections, "Phase 6P does not allow external connections."),
            Check("AllowOrderSubmissionFalse", !request.AllowOrderSubmission, "Order submission must remain false."),
            Check("SchedulerEnabledFalse", !request.SchedulerEnabled, "Scheduler must remain disabled."),
            Check("SubmitToShadowReplayFalse", !request.SubmitToShadowReplay, "Runtime shadow replay submit must remain false."),
            Check("PersistToTradingTablesFalse", !request.PersistToTradingTables, "Trading-table persistence must remain false."),
            Check("IsApprovedForExternalRunFalse", !request.IsApprovedForExternalRun, "IsApprovedForExternalRun must remain false."),
            Check("EligibleForManualSnapshotAttemptFalse", !request.EligibleForManualSnapshotAttempt, "eligibleForManualSnapshotAttempt must remain false."),
            Check("CanRunExternalSnapshotFalse", !request.CanRunExternalSnapshot, "canRunExternalSnapshot must remain false."),
            Check("NoSensitiveContentTrue", request.NoSensitiveContent, "noSensitiveContent must be true."),
            Check("SafetyGatePassed", safetyGate is not null && safetyGate.FinalDecision == LmaxReadOnlyPerInstrumentSafetyGateDecision.PASS, $"{request.Symbol} Phase 6O safety gate must be PASS."),
            Check("SafetyGateNonExecutable", safetyGate is not null && !safetyGate.IsApprovedForExternalRun && !safetyGate.EligibleForManualSnapshotAttempt, $"{request.Symbol} Phase 6O gate must remain non-executable.")
        };

        var combined = string.Join(" ", request.PreflightId, request.RequestedByOperatorId, request.Reason, request.Symbol, request.SlashSymbol, request.PlanningSecurityId, request.SecurityIdSource, request.EnvironmentName, request.VenueProfileName, request.RequestMode, request.SymbolEncodingMode);
        if (SensitivePattern.IsMatch(combined))
        {
            checks.Add(Fail("NoSensitiveContent", "Preflight request contains credential-shaped or sensitive content."));
        }

        if (AuthorizationPattern.IsMatch(combined))
        {
            checks.Add(Fail("NoTradingOrExternalAuthorizationLanguage", "Preflight request must not imply order, trading, external run, Production, UAT, or execution authorization."));
        }

        var finalDecision = checks.Any(x => x.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL)
            ? LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL
            : LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.PASS;

        return new(
            PreflightId: request.PreflightId,
            Symbol: request.Symbol,
            SlashSymbol: request.SlashSymbol,
            PlanningSecurityId: request.PlanningSecurityId,
            Checks: checks,
            FinalDecision: finalDecision,
            CanRunExternalSnapshot: false,
            RequiresFutureExplicitOperatorPrompt: true,
            IsApprovedForExternalRun: false,
            EligibleForManualSnapshotAttempt: false,
            NoSensitiveContent: request.NoSensitiveContent);
    }

    private static bool HasSecurityId(string value)
        => !string.IsNullOrWhiteSpace(value)
           && !value.StartsWith("PHASE6C-", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("PHASE6D-", StringComparison.OrdinalIgnoreCase)
           && !value.StartsWith("TBD", StringComparison.OrdinalIgnoreCase)
           && !value.Contains("<REAL_DEMO_SECURITY_ID>", StringComparison.OrdinalIgnoreCase);

    private static LmaxReadOnlyAdditionalInstrumentSnapshotPreflightCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyAdditionalInstrumentSnapshotPreflightCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL, detail);
}

public static class LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifestBuilder
{
    public static LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifest FromPlanningAndSafetyGates(
        LmaxReadOnlyInstrumentSecurityIdPlanningManifest planningManifest,
        LmaxReadOnlyAdditionalInstrumentSafetyGateManifest safetyGateManifest,
        string sourcePlanningManifestPath,
        string sourceSafetyGateManifestPath,
        string requestedByOperatorId,
        string reason,
        DateTimeOffset? createdAtUtc = null)
    {
        var stamp = createdAtUtc ?? DateTimeOffset.UtcNow;
        var requests = LmaxReadOnlyInstrumentAllowlist.CandidateEntries.Select(candidate =>
        {
            var planning = planningManifest.Instruments.FirstOrDefault(x => x.Symbol.Equals(candidate.Symbol, StringComparison.OrdinalIgnoreCase));
            return new LmaxReadOnlyAdditionalInstrumentSnapshotPreflightRequest(
                PreflightId: $"lmax-readonly-additional-snapshot-preflight-{candidate.Symbol}-{stamp:yyyyMMdd-HHmmss}",
                CreatedAtUtc: stamp,
                RequestedByOperatorId: requestedByOperatorId,
                Reason: reason,
                Symbol: candidate.Symbol,
                SlashSymbol: planning?.SlashSymbol ?? candidate.SlashSymbol,
                PlanningSecurityId: planning?.PlanningSecurityId ?? string.Empty,
                SecurityIdSource: planning?.SecurityIdSource ?? string.Empty,
                EnvironmentName: planning?.EnvironmentName ?? "Demo",
                VenueProfileName: planning?.VenueProfileName ?? "DemoLondon",
                RequestMode: "SnapshotPlusUpdates",
                SymbolEncodingMode: "SecurityIdOnly",
                MarketDepth: 1,
                MaxRuntimeSeconds: 30,
                MaxWaitSeconds: 30,
                MaxEventsPerRun: 25,
                AllowExternalConnections: false,
                ConfirmDemoReadOnly: false,
                AllowOrderSubmission: false,
                SchedulerEnabled: false,
                SubmitToShadowReplay: false,
                PersistToTradingTables: false,
                IsApprovedForExternalRun: false,
                EligibleForManualSnapshotAttempt: false,
                CanRunExternalSnapshot: false,
                NoSensitiveContent: true);
        }).ToArray();

        var results = requests
            .Select(x => LmaxReadOnlyAdditionalInstrumentSnapshotPreflightValidator.Validate(x, planningManifest, safetyGateManifest))
            .ToArray();
        var passCount = results.Count(x => x.FinalDecision == LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.PASS);
        var warningCount = results.Count(x => x.FinalDecision == LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.PASS_WITH_KNOWN_WARNINGS);
        var failCount = results.Count(x => x.FinalDecision == LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL);
        var finalDecision = failCount > 0
            ? LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL
            : warningCount > 0
                ? LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.PASS_WITH_KNOWN_WARNINGS
                : LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.PASS;

        return new(
            ManifestId: $"lmax-readonly-additional-instrument-snapshot-preflights-{stamp:yyyyMMdd-HHmmss}",
            CreatedAtUtc: stamp,
            SourcePlanningManifestPath: sourcePlanningManifestPath,
            SourceSafetyGateManifestPath: sourceSafetyGateManifestPath,
            Requests: requests,
            Results: results,
            InstrumentCount: results.Length,
            PassCount: passCount,
            WarningCount: warningCount,
            FailCount: failCount,
            AnyCanRunExternalSnapshot: false,
            AnyApprovedForExternalRun: false,
            AnyEligibleForManualSnapshotAttempt: false,
            RuntimeShadowReplaySubmit: false,
            SchedulerOrPolling: false,
            OrderSubmission: false,
            TradingTablePersistence: false,
            GatewayRegistration: false,
            TradingMutation: false,
            ExternalConnectionAttempted: false,
            SecurityListRequestAttempted: false,
            MarketDataSnapshotAttempted: false,
            ReplayAttempted: false,
            NoSensitiveContent: results.All(x => x.NoSensitiveContent),
            FinalDecision: finalDecision);
    }
}

public static class LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifestValidator
{
    public static LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifestValidationResult Validate(
        LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifest manifest)
    {
        var issues = new List<LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue>();
        foreach (var candidate in LmaxReadOnlyInstrumentAllowlist.CandidateEntries)
        {
            var results = manifest.Results.Where(x => x.Symbol.Equals(candidate.Symbol, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (results.Length == 0)
            {
                issues.Add(Error("PreflightMissing", $"$.results[{candidate.Symbol}]", $"{candidate.Symbol} is missing from the Phase 6P preflight manifest."));
                continue;
            }

            if (results.Length > 1)
            {
                issues.Add(Error("DuplicatePreflight", $"$.results[{candidate.Symbol}]", $"{candidate.Symbol} appears multiple times in the Phase 6P preflight manifest."));
            }

            var result = results[0];
            if (result.FinalDecision != LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.PASS)
            {
                issues.Add(Error("InstrumentPreflightFailed", $"$.results[{candidate.Symbol}]", $"{candidate.Symbol} preflight must pass."));
            }

            if (result.CanRunExternalSnapshot)
            {
                issues.Add(Error("CanRunExternalSnapshotForbidden", $"$.results[{candidate.Symbol}].canRunExternalSnapshot", $"{candidate.Symbol} must keep canRunExternalSnapshot=false."));
            }

            if (result.IsApprovedForExternalRun)
            {
                issues.Add(Error("ExternalRunApprovalForbidden", $"$.results[{candidate.Symbol}].isApprovedForExternalRun", $"{candidate.Symbol} must keep IsApprovedForExternalRun=false."));
            }

            if (result.EligibleForManualSnapshotAttempt)
            {
                issues.Add(Error("ManualSnapshotEligibilityForbidden", $"$.results[{candidate.Symbol}].eligibleForManualSnapshotAttempt", $"{candidate.Symbol} must keep eligibleForManualSnapshotAttempt=false."));
            }
        }

        if (manifest.InstrumentCount != LmaxReadOnlyInstrumentAllowlist.CandidateEntries.Count)
        {
            issues.Add(Error("InstrumentCountInvalid", "$.instrumentCount", "Phase 6P preflight manifest must include all four candidate instruments."));
        }

        if (manifest.AnyCanRunExternalSnapshot) issues.Add(Error("AggregateCanRunExternalSnapshotForbidden", "$.anyCanRunExternalSnapshot", "Phase 6P must not allow external snapshots."));
        if (manifest.AnyApprovedForExternalRun) issues.Add(Error("AggregateExternalRunApprovalForbidden", "$.anyApprovedForExternalRun", "Phase 6P must not approve external runs."));
        if (manifest.AnyEligibleForManualSnapshotAttempt) issues.Add(Error("AggregateManualSnapshotEligibilityForbidden", "$.anyEligibleForManualSnapshotAttempt", "Phase 6P must not make instruments eligible for manual snapshots."));
        if (manifest.RuntimeShadowReplaySubmit) issues.Add(Error("RuntimeShadowReplaySubmitForbidden", "$.runtimeShadowReplaySubmit", "Runtime shadow replay submit must remain false."));
        if (manifest.SchedulerOrPolling) issues.Add(Error("SchedulerPollingForbidden", "$.schedulerOrPolling", "Scheduler/polling must remain false."));
        if (manifest.OrderSubmission) issues.Add(Error("OrderSubmissionForbidden", "$.orderSubmission", "Order submission must remain false."));
        if (manifest.TradingTablePersistence) issues.Add(Error("TradingTablePersistenceForbidden", "$.tradingTablePersistence", "Trading-table persistence must remain false."));
        if (manifest.GatewayRegistration) issues.Add(Error("GatewayRegistrationForbidden", "$.gatewayRegistration", "Gateway registration must remain false."));
        if (manifest.TradingMutation) issues.Add(Error("TradingMutationForbidden", "$.tradingMutation", "Trading mutation must remain false."));
        if (manifest.ExternalConnectionAttempted) issues.Add(Error("ExternalConnectionForbidden", "$.externalConnectionAttempted", "Phase 6P must not connect to LMAX."));
        if (manifest.SecurityListRequestAttempted) issues.Add(Error("SecurityListRequestForbidden", "$.securityListRequestAttempted", "Phase 6P must not run SecurityListRequest."));
        if (manifest.MarketDataSnapshotAttempted) issues.Add(Error("MarketDataSnapshotForbidden", "$.marketDataSnapshotAttempted", "Phase 6P must not run snapshots."));
        if (manifest.ReplayAttempted) issues.Add(Error("ReplayForbidden", "$.replayAttempted", "Phase 6P must not run replay."));
        if (!manifest.NoSensitiveContent) issues.Add(Error("NoSensitiveContentFalse", "$.noSensitiveContent", "Phase 6P manifest must assert noSensitiveContent=true."));

        var decision = issues.Count == 0
            ? LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.PASS
            : LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL;
        return new(decision, manifest, issues);
    }

    private static LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error, code, path, message);
}
