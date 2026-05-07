using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public sealed record LmaxShadowReaderOptions
{
    public bool Enabled { get; init; } = false;
    public bool AllowExternalConnections { get; init; } = false;
    public bool AllowCredentialUse { get; init; } = false;
    public bool ReadOnly { get; init; } = true;
    public bool AllowOrderSubmission { get; init; } = false;
    public bool PersistRawFixMessages { get; init; } = false;
    public bool PersistToTradingTables { get; init; } = false;
    public int MaxEventsPerRun { get; init; } = 25;
    public bool DryRun { get; init; } = true;
}

public enum LmaxShadowReaderStatus
{
    Disabled,
    Blocked,
    Ready,
    Running,
    Completed,
    Failed
}

public enum LmaxShadowReaderSafetyGateStatus
{
    Passed,
    Failed,
    Warning,
    Informational
}

public sealed record LmaxShadowReaderRunRequest(string Reason, int? MaxEvents = null, bool DryRun = true);

public sealed record LmaxShadowReaderSafetyCheckResult(
    string Gate,
    LmaxShadowReaderSafetyGateStatus Status,
    bool Passed,
    string ObservedValue,
    string ExpectedValue,
    string Message);

public sealed record LmaxShadowReaderRunResult(
    LmaxShadowReaderStatus Status,
    string BlockedReason,
    bool Executed,
    bool Connected,
    bool ExternalConnectionAttempted,
    bool CredentialsUsed,
    bool OrdersSubmitted,
    bool PersistedToTradingTables,
    int EventsRead,
    string Message,
    IReadOnlyList<LmaxShadowReaderSafetyCheckResult> SafetyChecks);

public interface ILmaxShadowReader
{
    Task<LmaxShadowReaderRunResult> GetStatusAsync(CancellationToken cancellationToken);
    Task<LmaxShadowReaderRunResult> RunAsync(LmaxShadowReaderRunRequest request, CancellationToken cancellationToken);
}

public sealed class DisabledLmaxShadowReader(
    LmaxShadowReaderOptions options,
    IOperatorAuditService audit) : ILmaxShadowReader
{
    public Task<LmaxShadowReaderRunResult> GetStatusAsync(CancellationToken cancellationToken)
    {
        var checks = EvaluateSafety();
        return Task.FromResult(ToResult(checks, "LMAX shadow reader is disabled by default."));
    }

    public async Task<LmaxShadowReaderRunResult> RunAsync(LmaxShadowReaderRunRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new DomainRuleViolationException("A reason is required for LMAX shadow reader run requests.");
        }

        var checks = EvaluateSafety(request);
        var result = ToResult(checks, "LMAX shadow reader did not run. It is a disabled, read-only skeleton.");
        var failedGateNames = result.SafetyChecks
            .Where(x => x.Status == LmaxShadowReaderSafetyGateStatus.Failed)
            .Select(x => x.Gate)
            .ToArray();
        await audit.RecordAsync(new OperatorAuditRecordRequest(
            OperatorAuditEventType.LmaxShadowReaderRunBlocked,
            OperatorAuditSeverity.Warning,
            OperatorAuditResult.Blocked,
            "LmaxShadowReader",
            result.Message,
            "LmaxShadowReader",
            null,
            request.Reason,
            Metadata: new
            {
                result.Status,
                result.BlockedReason,
                failedGateNames,
                result.Executed,
                result.Connected,
                result.ExternalConnectionAttempted,
                result.CredentialsUsed,
                result.OrdersSubmitted,
                result.PersistedToTradingTables,
                request.MaxEvents,
                request.DryRun,
                safetyChecks = result.SafetyChecks
            }), cancellationToken);
        return result;
    }

    private IReadOnlyList<LmaxShadowReaderSafetyCheckResult> EvaluateSafety(LmaxShadowReaderRunRequest? request = null)
    {
        var maxEvents = request?.MaxEvents ?? options.MaxEventsPerRun;
        var requestedDryRun = request?.DryRun ?? true;
        return
        [
            Gate("Enabled", options.Enabled, options.Enabled, true, options.Enabled ? "Reader is enabled." : "Reader is disabled by default."),
            Gate("DryRun", options.DryRun && requestedDryRun, $"{options.DryRun}/{requestedDryRun}", "true/true", options.DryRun && requestedDryRun ? "Dry-run mode is enforced." : "Dry-run mode must remain enabled for the skeleton and request."),
            Gate("ReadOnly", options.ReadOnly, options.ReadOnly, true, options.ReadOnly ? "Reader is read-only." : "Reader must be read-only."),
            Gate("AllowExternalConnections", options.AllowExternalConnections, options.AllowExternalConnections, true, options.AllowExternalConnections ? "External connections explicitly allowed." : "External connections are not allowed."),
            Gate("AllowCredentialUse", options.AllowCredentialUse, options.AllowCredentialUse, true, options.AllowCredentialUse ? "Credential use explicitly allowed for a future reader." : "Credential use is not allowed."),
            Gate("AllowOrderSubmission", !options.AllowOrderSubmission, options.AllowOrderSubmission, false, options.AllowOrderSubmission ? "Order submission is forbidden for shadow reader." : "Order submission is disabled."),
            Gate("PersistToTradingTables", !options.PersistToTradingTables, options.PersistToTradingTables, false, options.PersistToTradingTables ? "Trading table persistence is forbidden." : "Trading table persistence is disabled."),
            Gate("PersistRawFixMessages", !options.PersistRawFixMessages, options.PersistRawFixMessages, false, options.PersistRawFixMessages ? "Raw FIX persistence is forbidden for this skeleton." : "Raw FIX persistence is disabled."),
            Gate("MaxEventsPerRun", maxEvents > 0 && maxEvents <= Math.Max(1, options.MaxEventsPerRun), maxEvents, $"1..{Math.Max(1, options.MaxEventsPerRun)}", $"Requested max events {maxEvents}; configured max {options.MaxEventsPerRun}."),
            Gate("RuntimeGatewayRegistration", true, "FakeLmaxGatewayOnly", "FakeLmaxGatewayOnly", "API and Worker remain FakeLmax-only."),
            Info("EnvironmentName", "LocalDisabled", "LocalDisabled", "No live environment is selected by the no-op reader."),
            Gate("ImplementationMode", false, "DisabledNoOpSkeleton", "FutureExplicitReaderImplementation", "Live shadow reading is not implemented in runtime; this skeleton is intentionally blocked.")
        ];
    }

    private static LmaxShadowReaderRunResult ToResult(IReadOnlyList<LmaxShadowReaderSafetyCheckResult> checks, string message)
    {
        var failedGates = checks.Where(x => x.Status == LmaxShadowReaderSafetyGateStatus.Failed).Select(x => x.Gate).ToArray();
        var blockedReason = failedGates.Length == 0
            ? "No blocking safety gates failed."
            : "Blocked by safety gates: " + string.Join(", ", failedGates) + ".";
        return new(
            checks.Any(x => x.Gate == "Enabled" && x.Status == LmaxShadowReaderSafetyGateStatus.Failed) ? LmaxShadowReaderStatus.Disabled : failedGates.Length == 0 ? LmaxShadowReaderStatus.Ready : LmaxShadowReaderStatus.Blocked,
            blockedReason,
            Executed: false,
            Connected: false,
            ExternalConnectionAttempted: false,
            CredentialsUsed: false,
            OrdersSubmitted: false,
            PersistedToTradingTables: false,
            EventsRead: 0,
            message,
            checks);
    }

    private static LmaxShadowReaderSafetyCheckResult Gate(string gate, bool passed, object? observed, object? expected, string message)
        => new(gate, passed ? LmaxShadowReaderSafetyGateStatus.Passed : LmaxShadowReaderSafetyGateStatus.Failed, passed, Convert.ToString(observed, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, Convert.ToString(expected, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, message);

    private static LmaxShadowReaderSafetyCheckResult Info(string gate, object? observed, object? expected, string message)
        => new(gate, LmaxShadowReaderSafetyGateStatus.Informational, true, Convert.ToString(observed, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, Convert.ToString(expected, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, message);
}
