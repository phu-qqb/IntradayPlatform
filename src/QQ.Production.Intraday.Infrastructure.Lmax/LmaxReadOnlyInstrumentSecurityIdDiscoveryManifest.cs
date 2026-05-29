namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyInstrumentSecurityIdDiscoveryIssueSeverity
{
    Error,
    Warning,
    Info
}

public enum LmaxReadOnlyInstrumentSecurityIdDiscoveryDecision
{
    PASS,
    PASS_WITH_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyInstrumentSecurityIdDiscoveryEntry(
    string Symbol,
    string SecurityId,
    string Source,
    bool IsPlaceholder,
    bool IsApprovedForExternalRun,
    string Notes);

public sealed record LmaxReadOnlyInstrumentSecurityIdDiscoverySafety(
    bool ExternalConnectionAttempted,
    bool ExternalApiCallAttempted,
    bool SchedulerOrPollingAdded,
    bool RuntimeShadowReplaySubmit,
    bool OrderSubmissionAdded,
    bool GatewayRegistrationAdded,
    bool TradingMutationAdded);

public sealed record LmaxReadOnlyInstrumentSecurityIdDiscoveryIssue(
    LmaxReadOnlyInstrumentSecurityIdDiscoveryIssueSeverity Severity,
    string Code,
    string Path,
    string Message);

public sealed record LmaxReadOnlyInstrumentSecurityIdDiscoveryValidationResult(
    LmaxReadOnlyInstrumentSecurityIdDiscoveryDecision Decision,
    IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdDiscoveryEntry> Entries,
    LmaxReadOnlyInstrumentSecurityIdDiscoverySafety Safety,
    IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdDiscoveryIssue> Issues)
{
    public IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdDiscoveryIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdDiscoveryIssueSeverity.Error).ToArray();

    public IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdDiscoveryIssue> Warnings =>
        Issues.Where(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdDiscoveryIssueSeverity.Warning).ToArray();
}

public sealed class LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest
{
    public LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest()
        : this(CreateDefaultEntries(), CreateDefaultSafety())
    {
    }

    public LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest(
        IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdDiscoveryEntry> entries,
        LmaxReadOnlyInstrumentSecurityIdDiscoverySafety safety)
    {
        Entries = entries;
        Safety = safety;
    }

    public IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdDiscoveryEntry> Entries { get; }

    public LmaxReadOnlyInstrumentSecurityIdDiscoverySafety Safety { get; }

    public string? GetCandidateSecurityId(string symbol)
        => Entries.FirstOrDefault(x => x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))?.SecurityId;

    public static IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdDiscoveryEntry> CreateDefaultEntries()
        =>
        [
            Placeholder("GBPUSD", "PHASE6D-DISCOVERY-PENDING-GBPUSD"),
            Placeholder("USDJPY", "PHASE6D-DISCOVERY-PENDING-USDJPY"),
            Placeholder("EURGBP", "PHASE6D-DISCOVERY-PENDING-EURGBP"),
            Placeholder("AUDUSD", "PHASE6D-DISCOVERY-PENDING-AUDUSD")
        ];

    public static LmaxReadOnlyInstrumentSecurityIdDiscoverySafety CreateDefaultSafety()
        => new(
            ExternalConnectionAttempted: false,
            ExternalApiCallAttempted: false,
            SchedulerOrPollingAdded: false,
            RuntimeShadowReplaySubmit: false,
            OrderSubmissionAdded: false,
            GatewayRegistrationAdded: false,
            TradingMutationAdded: false);

    private static LmaxReadOnlyInstrumentSecurityIdDiscoveryEntry Placeholder(string symbol, string securityId)
        => new(
            Symbol: symbol,
            SecurityId: securityId,
            Source: "LocalPhase6DPlanningPlaceholder",
            IsPlaceholder: true,
            IsApprovedForExternalRun: false,
            Notes: "Phase 6D local placeholder only. Not confirmed by an external LMAX lookup and not approved for external execution.");
}

public static class LmaxReadOnlyInstrumentSecurityIdDiscoveryManifestValidator
{
    public static LmaxReadOnlyInstrumentSecurityIdDiscoveryValidationResult Validate(
        LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest? manifest = null)
    {
        var candidateManifest = manifest ?? new LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest();
        var issues = new List<LmaxReadOnlyInstrumentSecurityIdDiscoveryIssue>();

        var allowlistSymbols = LmaxReadOnlyInstrumentAllowlist.CandidateEntries
            .Select(x => x.Symbol)
            .ToArray();

        foreach (var symbol in allowlistSymbols)
        {
            var entry = candidateManifest.Entries.FirstOrDefault(x => x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                issues.Add(Error("MissingSecurityIdEntry", "$.entries", $"Missing Phase 6D SecurityID entry for {symbol}."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.SecurityId))
            {
                issues.Add(Error("SecurityIdMissing", $"$.entries[{symbol}].securityId", $"SecurityID is missing for {symbol}."));
            }

            if (entry.IsApprovedForExternalRun)
            {
                issues.Add(Error("ExternalRunApprovalForbidden", $"$.entries[{symbol}].isApprovedForExternalRun", $"{symbol} must keep IsApprovedForExternalRun=false in Phase 6D."));
            }

            if (!entry.IsPlaceholder)
            {
                issues.Add(Warning("NonPlaceholderSecurityId", $"$.entries[{symbol}].isPlaceholder", $"{symbol} SecurityID is marked non-placeholder; ensure this was not sourced through an external runtime action."));
            }

            if (string.IsNullOrWhiteSpace(entry.Source))
            {
                issues.Add(Error("SourceMissing", $"$.entries[{symbol}].source", $"{symbol} must document the local planning source."));
            }
        }

        ValidateNoRuntimeActions(candidateManifest.Safety, issues);

        var errors = issues.Count(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdDiscoveryIssueSeverity.Error);
        var warnings = issues.Count(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdDiscoveryIssueSeverity.Warning);
        var decision = errors > 0
            ? LmaxReadOnlyInstrumentSecurityIdDiscoveryDecision.FAIL
            : warnings > 0
                ? LmaxReadOnlyInstrumentSecurityIdDiscoveryDecision.PASS_WITH_WARNINGS
                : LmaxReadOnlyInstrumentSecurityIdDiscoveryDecision.PASS;

        return new(decision, candidateManifest.Entries, candidateManifest.Safety, issues);
    }

    private static void ValidateNoRuntimeActions(
        LmaxReadOnlyInstrumentSecurityIdDiscoverySafety safety,
        List<LmaxReadOnlyInstrumentSecurityIdDiscoveryIssue> issues)
    {
        if (safety.ExternalConnectionAttempted) issues.Add(Error("ExternalConnectionForbidden", "$.safety.externalConnectionAttempted", "Phase 6D must not connect to LMAX."));
        if (safety.ExternalApiCallAttempted) issues.Add(Error("ExternalApiCallForbidden", "$.safety.externalApiCallAttempted", "Phase 6D must not call external APIs."));
        if (safety.SchedulerOrPollingAdded) issues.Add(Error("SchedulerPollingForbidden", "$.safety.schedulerOrPollingAdded", "Phase 6D must not add scheduler or polling."));
        if (safety.RuntimeShadowReplaySubmit) issues.Add(Error("RuntimeShadowReplaySubmitForbidden", "$.safety.runtimeShadowReplaySubmit", "Phase 6D must not submit to shadow replay from runtime."));
        if (safety.OrderSubmissionAdded) issues.Add(Error("OrderSubmissionForbidden", "$.safety.orderSubmissionAdded", "Phase 6D must not add order submission."));
        if (safety.GatewayRegistrationAdded) issues.Add(Error("GatewayRegistrationForbidden", "$.safety.gatewayRegistrationAdded", "Phase 6D must not add gateway registration."));
        if (safety.TradingMutationAdded) issues.Add(Error("TradingMutationForbidden", "$.safety.tradingMutationAdded", "Phase 6D must not mutate trading state."));
    }

    private static LmaxReadOnlyInstrumentSecurityIdDiscoveryIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyInstrumentSecurityIdDiscoveryIssueSeverity.Error, code, path, message);

    private static LmaxReadOnlyInstrumentSecurityIdDiscoveryIssue Warning(string code, string path, string message)
        => new(LmaxReadOnlyInstrumentSecurityIdDiscoveryIssueSeverity.Warning, code, path, message);
}
