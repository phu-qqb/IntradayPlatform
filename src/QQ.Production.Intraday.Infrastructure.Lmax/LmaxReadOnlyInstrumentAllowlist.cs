namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyInstrumentAllowlistIssueSeverity
{
    Error,
    Warning,
    Info
}

public enum LmaxReadOnlyInstrumentDemoReadiness
{
    CandidateRequiresDemoSecurityIdConfirmation,
    DemoReadyValidated
}

public enum LmaxReadOnlyInstrumentAllowlistDecision
{
    PASS,
    PASS_WITH_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyInstrumentAllowlistEntry(
    string Instrument,
    string Symbol,
    string SlashSymbol,
    string SecurityId,
    string SecurityIdSource,
    string VenueProfileName,
    string EnvironmentName,
    string Venue,
    string LiquidityTier,
    LmaxReadOnlyInstrumentDemoReadiness DemoReadiness,
    bool IsAllowlistedForPlanning,
    bool IsApprovedForExternalRun,
    string EvidenceMode,
    string Notes);

public sealed record LmaxReadOnlyInstrumentAllowlistSafetyRules(
    bool SchedulerAllowed,
    bool PollingAllowed,
    bool RuntimeShadowReplaySubmitAllowed,
    bool OrderSubmissionAllowed,
    bool GatewayRegistrationAllowed,
    bool TradingMutationAllowed,
    bool ExternalConnectionAllowedByThisPhase,
    bool CredentialValuesAllowed);

public sealed record LmaxReadOnlyInstrumentAllowlistIssue(
    LmaxReadOnlyInstrumentAllowlistIssueSeverity Severity,
    string Code,
    string Path,
    string Message);

public sealed record LmaxReadOnlyInstrumentAllowlistValidationResult(
    LmaxReadOnlyInstrumentAllowlistDecision Decision,
    IReadOnlyList<LmaxReadOnlyInstrumentAllowlistEntry> Entries,
    IReadOnlyList<LmaxReadOnlyInstrumentAllowlistIssue> Issues,
    LmaxReadOnlyInstrumentAllowlistSafetyRules SafetyRules)
{
    public IReadOnlyList<LmaxReadOnlyInstrumentAllowlistIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyInstrumentAllowlistIssueSeverity.Error).ToArray();

    public IReadOnlyList<LmaxReadOnlyInstrumentAllowlistIssue> Warnings =>
        Issues.Where(x => x.Severity == LmaxReadOnlyInstrumentAllowlistIssueSeverity.Warning).ToArray();
}

public sealed record LmaxReadOnlyInstrumentRequestValidationResult(
    bool IsAllowlisted,
    bool CanRunExternallyInThisPhase,
    LmaxReadOnlyInstrumentAllowlistEntry? Entry,
    IReadOnlyList<LmaxReadOnlyInstrumentAllowlistIssue> Issues);

public static class LmaxReadOnlyInstrumentAllowlist
{
    public const string RequiredEvidenceMode = "MarketDataOnly";
    public const string RequiredEnvironmentName = "Demo";

    public static readonly LmaxReadOnlyInstrumentAllowlistSafetyRules Phase6BPlanningSafetyRules = new(
        SchedulerAllowed: false,
        PollingAllowed: false,
        RuntimeShadowReplaySubmitAllowed: false,
        OrderSubmissionAllowed: false,
        GatewayRegistrationAllowed: false,
        TradingMutationAllowed: false,
        ExternalConnectionAllowedByThisPhase: false,
        CredentialValuesAllowed: false);

    public static IReadOnlyList<LmaxReadOnlyInstrumentAllowlistEntry> CandidateEntries { get; } =
    [
        Candidate(
            instrument: "GBPUSD",
            slashSymbol: "GBP/USD",
            securityId: "TBD-LMAX-DEMO-GBPUSD",
            liquidityTier: "MajorFxHighLiquidity",
            notes: "Candidate major FX pair. SecurityID must be confirmed in a separate no-external-run design or lab-confirmation phase before any manual run is allowed."),
        Candidate(
            instrument: "USDJPY",
            slashSymbol: "USD/JPY",
            securityId: "TBD-LMAX-DEMO-USDJPY",
            liquidityTier: "MajorFxHighLiquidity",
            notes: "Candidate major FX pair. Yen quote precision and SecurityID must be confirmed before any manual run is allowed."),
        Candidate(
            instrument: "EURGBP",
            slashSymbol: "EUR/GBP",
            securityId: "TBD-LMAX-DEMO-EURGBP",
            liquidityTier: "MajorCrossFxLiquid",
            notes: "Candidate liquid cross. SecurityID and evidence expectations must be confirmed before any manual run is allowed."),
        Candidate(
            instrument: "AUDUSD",
            slashSymbol: "AUD/USD",
            securityId: "TBD-LMAX-DEMO-AUDUSD",
            liquidityTier: "MajorFxLiquid",
            notes: "Candidate major FX pair. SecurityID and Demo availability must be confirmed before any manual run is allowed.")
    ];

    private static LmaxReadOnlyInstrumentAllowlistEntry Candidate(
        string instrument,
        string slashSymbol,
        string securityId,
        string liquidityTier,
        string notes)
        => new(
            Instrument: instrument,
            Symbol: instrument,
            SlashSymbol: slashSymbol,
            SecurityId: securityId,
            SecurityIdSource: "TBD-LMAX-DEMO-CONFIRMATION-REQUIRED",
            VenueProfileName: LmaxReadOnlyVenueProfileName.DemoLondon.Value,
            EnvironmentName: RequiredEnvironmentName,
            Venue: "LMAX Demo",
            LiquidityTier: liquidityTier,
            DemoReadiness: LmaxReadOnlyInstrumentDemoReadiness.CandidateRequiresDemoSecurityIdConfirmation,
            IsAllowlistedForPlanning: true,
            IsApprovedForExternalRun: false,
            EvidenceMode: RequiredEvidenceMode,
            Notes: notes);
}

public static class LmaxReadOnlyInstrumentAllowlistValidator
{
    public static LmaxReadOnlyInstrumentAllowlistValidationResult Validate(
        IReadOnlyList<LmaxReadOnlyInstrumentAllowlistEntry>? entries = null,
        LmaxReadOnlyInstrumentAllowlistSafetyRules? safetyRules = null)
    {
        var candidateEntries = entries ?? LmaxReadOnlyInstrumentAllowlist.CandidateEntries;
        var rules = safetyRules ?? LmaxReadOnlyInstrumentAllowlist.Phase6BPlanningSafetyRules;
        var issues = new List<LmaxReadOnlyInstrumentAllowlistIssue>();

        if (candidateEntries.Count == 0)
        {
            issues.Add(Error("AllowlistEmpty", "$.entries", "At least one additional MarketData instrument candidate must be documented."));
        }

        for (var i = 0; i < candidateEntries.Count; i++)
        {
            ValidateEntry(candidateEntries[i], i, issues);
        }

        AddDuplicateIssues(candidateEntries, x => x.Instrument, "DuplicateInstrument", "$.entries[*].instrument", issues);
        AddDuplicateIssues(candidateEntries, x => x.Symbol, "DuplicateSymbol", "$.entries[*].symbol", issues);
        AddDuplicateIssues(candidateEntries, x => x.SecurityId, "DuplicateSecurityId", "$.entries[*].securityId", issues);

        if (candidateEntries.Any(x => x.Instrument.Equals("EURUSD", StringComparison.OrdinalIgnoreCase)
                                      || x.SecurityId.Equals("4001", StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(Error("BaselineInstrumentNotAdditional", "$.entries", "Phase 6B candidate allowlist must list additional instruments beyond the already-validated EURUSD / SecurityID 4001 baseline."));
        }

        ValidateSafetyRules(rules, issues);

        var errors = issues.Count(x => x.Severity == LmaxReadOnlyInstrumentAllowlistIssueSeverity.Error);
        var warnings = issues.Count(x => x.Severity == LmaxReadOnlyInstrumentAllowlistIssueSeverity.Warning);
        var decision = errors > 0
            ? LmaxReadOnlyInstrumentAllowlistDecision.FAIL
            : warnings > 0
                ? LmaxReadOnlyInstrumentAllowlistDecision.PASS_WITH_WARNINGS
                : LmaxReadOnlyInstrumentAllowlistDecision.PASS;

        return new(decision, candidateEntries, issues, rules);
    }

    public static LmaxReadOnlyInstrumentRequestValidationResult ValidatePlannedRequest(
        string? instrument,
        string? securityId,
        IReadOnlyList<LmaxReadOnlyInstrumentAllowlistEntry>? entries = null)
    {
        var candidateEntries = entries ?? LmaxReadOnlyInstrumentAllowlist.CandidateEntries;
        var issues = new List<LmaxReadOnlyInstrumentAllowlistIssue>();

        if (string.IsNullOrWhiteSpace(instrument) && string.IsNullOrWhiteSpace(securityId))
        {
            issues.Add(Error("InstrumentRequired", "$.instrument", "Instrument or SecurityID is required for allowlist validation."));
            return new(false, false, null, issues);
        }

        var entry = candidateEntries.FirstOrDefault(x =>
            (!string.IsNullOrWhiteSpace(instrument)
             && (x.Instrument.Equals(instrument, StringComparison.OrdinalIgnoreCase)
                 || x.Symbol.Equals(instrument, StringComparison.OrdinalIgnoreCase)
                 || x.SlashSymbol.Equals(instrument, StringComparison.OrdinalIgnoreCase)))
            || (!string.IsNullOrWhiteSpace(securityId)
                && x.SecurityId.Equals(securityId, StringComparison.OrdinalIgnoreCase)));

        if (entry is null)
        {
            issues.Add(Error("InstrumentNotAllowlisted", "$.instrument", "Only Phase 6B allowlisted candidate instruments can be requested in planning validation."));
            return new(false, false, null, issues);
        }

        if (!entry.IsApprovedForExternalRun)
        {
            issues.Add(Info("InstrumentPlanningOnly", "$.instrument", "Instrument is allowlisted for Phase 6B planning only and is not approved for any external run in this phase."));
        }

        return new(true, false, entry, issues);
    }

    private static void ValidateEntry(
        LmaxReadOnlyInstrumentAllowlistEntry entry,
        int index,
        List<LmaxReadOnlyInstrumentAllowlistIssue> issues)
    {
        var path = $"$.entries[{index}]";
        Require(entry.Instrument, "InstrumentRequired", $"{path}.instrument", "Instrument is required.", issues);
        Require(entry.Symbol, "SymbolRequired", $"{path}.symbol", "Symbol is required.", issues);
        Require(entry.SlashSymbol, "SlashSymbolRequired", $"{path}.slashSymbol", "SlashSymbol is required.", issues);
        Require(entry.SecurityId, "SecurityIdRequired", $"{path}.securityId", "SecurityID label is required, even when confirmation is still pending.", issues);
        Require(entry.SecurityIdSource, "SecurityIdSourceRequired", $"{path}.securityIdSource", "SecurityIDSource label is required.", issues);
        Require(entry.VenueProfileName, "VenueProfileRequired", $"{path}.venueProfileName", "Venue profile is required.", issues);
        Require(entry.EnvironmentName, "EnvironmentRequired", $"{path}.environmentName", "Environment is required.", issues);
        Require(entry.Venue, "VenueRequired", $"{path}.venue", "Venue is required.", issues);
        Require(entry.LiquidityTier, "LiquidityRequired", $"{path}.liquidityTier", "Liquidity tier is required.", issues);
        Require(entry.EvidenceMode, "EvidenceModeRequired", $"{path}.evidenceMode", "Evidence mode is required.", issues);
        Require(entry.Notes, "NotesRequired", $"{path}.notes", "Notes are required.", issues);

        if (!entry.EnvironmentName.Equals(LmaxReadOnlyInstrumentAllowlist.RequiredEnvironmentName, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("EnvironmentMustBeDemo", $"{path}.environmentName", "Phase 6B candidate instruments must remain Demo-only."));
        }

        if (!entry.VenueProfileName.Equals(LmaxReadOnlyVenueProfileName.DemoLondon.Value, StringComparison.OrdinalIgnoreCase)
            && !entry.VenueProfileName.Equals(LmaxReadOnlyVenueProfileName.LegacyDemoReadOnly.Value, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("VenueProfileNotDemo", $"{path}.venueProfileName", "Phase 6B candidate instruments must use a Demo read-only venue profile label."));
        }

        if (!entry.EvidenceMode.Equals(LmaxReadOnlyInstrumentAllowlist.RequiredEvidenceMode, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("EvidenceModeMustBeMarketDataOnly", $"{path}.evidenceMode", "Additional instruments must map to MarketDataOnly evidence previews."));
        }

        if (!entry.IsAllowlistedForPlanning)
        {
            issues.Add(Error("InstrumentNotPlanningAllowlisted", $"{path}.isAllowlistedForPlanning", "Candidate entry must be allowlisted for planning."));
        }

        if (entry.IsApprovedForExternalRun)
        {
            issues.Add(Error("ExternalRunApprovalForbidden", $"{path}.isApprovedForExternalRun", "Phase 6B is design-only; candidate instruments must not be approved for external runs."));
        }

        if (entry.DemoReadiness == LmaxReadOnlyInstrumentDemoReadiness.DemoReadyValidated)
        {
            issues.Add(Warning("DemoReadinessClaimsValidation", $"{path}.demoReadiness", "Phase 6B should not claim Demo readiness for additional instruments until a separate confirmation phase."));
        }
    }

    private static void ValidateSafetyRules(
        LmaxReadOnlyInstrumentAllowlistSafetyRules rules,
        List<LmaxReadOnlyInstrumentAllowlistIssue> issues)
    {
        if (rules.SchedulerAllowed) issues.Add(Error("SchedulerForbidden", "$.safety.schedulerAllowed", "Scheduler must remain disabled for Phase 6B."));
        if (rules.PollingAllowed) issues.Add(Error("PollingForbidden", "$.safety.pollingAllowed", "Automatic polling must remain disabled for Phase 6B."));
        if (rules.RuntimeShadowReplaySubmitAllowed) issues.Add(Error("RuntimeShadowReplaySubmitForbidden", "$.safety.runtimeShadowReplaySubmitAllowed", "Runtime shadow replay submit must remain forbidden for Phase 6B."));
        if (rules.OrderSubmissionAllowed) issues.Add(Error("OrderSubmissionForbidden", "$.safety.orderSubmissionAllowed", "Order submission must remain forbidden for Phase 6B."));
        if (rules.GatewayRegistrationAllowed) issues.Add(Error("GatewayRegistrationForbidden", "$.safety.gatewayRegistrationAllowed", "Real gateway registration must remain forbidden for Phase 6B."));
        if (rules.TradingMutationAllowed) issues.Add(Error("TradingMutationForbidden", "$.safety.tradingMutationAllowed", "Trading-state mutation must remain forbidden for Phase 6B."));
        if (rules.ExternalConnectionAllowedByThisPhase) issues.Add(Error("ExternalConnectionForbidden", "$.safety.externalConnectionAllowedByThisPhase", "Phase 6B must not approve an external connection."));
        if (rules.CredentialValuesAllowed) issues.Add(Error("CredentialValuesForbidden", "$.safety.credentialValuesAllowed", "Credential values must never be exposed by Phase 6B planning."));
    }

    private static void AddDuplicateIssues(
        IReadOnlyList<LmaxReadOnlyInstrumentAllowlistEntry> entries,
        Func<LmaxReadOnlyInstrumentAllowlistEntry, string> selector,
        string code,
        string path,
        List<LmaxReadOnlyInstrumentAllowlistIssue> issues)
    {
        var duplicates = entries
            .Select(selector)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToArray();

        foreach (var duplicate in duplicates)
        {
            issues.Add(Error(code, path, $"Duplicate allowlist value '{duplicate}' is not allowed."));
        }
    }

    private static void Require(
        string value,
        string code,
        string path,
        string message,
        List<LmaxReadOnlyInstrumentAllowlistIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(Error(code, path, message));
        }
    }

    private static LmaxReadOnlyInstrumentAllowlistIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyInstrumentAllowlistIssueSeverity.Error, code, path, message);

    private static LmaxReadOnlyInstrumentAllowlistIssue Warning(string code, string path, string message)
        => new(LmaxReadOnlyInstrumentAllowlistIssueSeverity.Warning, code, path, message);

    private static LmaxReadOnlyInstrumentAllowlistIssue Info(string code, string path, string message)
        => new(LmaxReadOnlyInstrumentAllowlistIssueSeverity.Info, code, path, message);
}
