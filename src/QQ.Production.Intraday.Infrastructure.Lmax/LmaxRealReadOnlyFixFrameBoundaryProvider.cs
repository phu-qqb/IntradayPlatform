namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxReadOnlyFixSessionOptions(
    string EnvironmentLabel,
    string SenderCompIdLabel,
    string TargetCompIdLabel,
    int HeartbeatIntervalSeconds,
    TimeSpan Timeout,
    bool DemoReadOnly,
    IReadOnlyList<string> AllowedMessageTypes,
    bool ExternalFixExecutionApproved = false)
{
    public static LmaxReadOnlyFixSessionOptions DemoReadOnlyDisabled(
        string senderCompIdLabel,
        string targetCompIdLabel,
        int heartbeatIntervalSeconds = 30,
        TimeSpan? timeout = null)
        => new(
            "Demo/read-only",
            senderCompIdLabel,
            targetCompIdLabel,
            heartbeatIntervalSeconds,
            timeout ?? TimeSpan.FromSeconds(15),
            DemoReadOnly: true,
            DefaultAllowedReadOnlyMessageTypes,
            ExternalFixExecutionApproved: false);

    public static IReadOnlyList<string> DefaultAllowedReadOnlyMessageTypes { get; } =
    [
        "Logon",
        "Logout",
        "Heartbeat",
        "TestRequest",
        "MarketDataSnapshotFullRefresh",
        "MarketDataRequestReject",
        "BusinessMessageReject",
        "Reject"
    ];
}

public interface ILmaxReadOnlyFixFrameClient
{
    LmaxRealReadOnlyDependencyResult OpenSessionLogon(
        LmaxReadOnlyFixSessionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord accessRecord,
        CancellationToken cancellationToken = default);

    bool ShutdownRevert();
}

public sealed class LmaxRealReadOnlyFixFrameBoundaryProvider : ILmaxRealReadOnlyFixFrameBoundaryProvider
{
    private static readonly HashSet<string> AllowedMessageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "A",
        "0",
        "1",
        "5",
        "W",
        "Y",
        "j",
        "3",
        "Logon",
        "Logout",
        "Heartbeat",
        "TestRequest",
        "MarketDataSnapshotFullRefresh",
        "MarketDataRequestReject",
        "BusinessMessageReject",
        "Reject"
    };

    private static readonly HashSet<string> ForbiddenMessageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "D",
        "F",
        "H",
        "8",
        "AE",
        "AD",
        "NewOrderSingle",
        "OrderCancelRequest",
        "OrderStatusRequest",
        "ExecutionReport",
        "TradeCaptureReportRequest",
        "Replay",
        "ShadowReplay",
        "TradingMutation"
    };

    private readonly LmaxReadOnlyFixSessionOptions options;
    private readonly ILmaxReadOnlyFixFrameClient frameClient;

    public LmaxRealReadOnlyFixFrameBoundaryProvider(
        LmaxReadOnlyFixSessionOptions options,
        ILmaxReadOnlyFixFrameClient frameClient)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.frameClient = frameClient ?? throw new ArgumentNullException(nameof(frameClient));
    }

    public LmaxRealReadOnlyDependencyResult OpenSessionLogon(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord accessRecord,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(accessRecord);
        cancellationToken.ThrowIfCancellationRequested();

        var optionIssue = ValidateOptions(options);
        if (optionIssue is not null)
        {
            return optionIssue;
        }

        var scopeIssues = LmaxExecutableReadOnlyCredentialBoundary.ValidateScope(scope);
        if (scopeIssues.Count > 0)
        {
            return Blocked(
                "FixFrameBoundaryProviderBlockedBeforeFrameUse",
                "SafetyConstraintFailed",
                string.Join("; ", scopeIssues.Select(x => x.Code)));
        }

        var credentialIssue = ValidateCredentialRecord(accessRecord);
        if (credentialIssue is not null)
        {
            return credentialIssue;
        }

        if (!options.ExternalFixExecutionApproved)
        {
            return Blocked(
                "FixFrameBoundaryProviderExecutionNotApproved",
                "FixExecutionNotApproved",
                "FIX provider requires a future explicitly approved phase before opening a FIX session boundary.");
        }

        return Sanitize(frameClient.OpenSessionLogon(options, scope, accessRecord, cancellationToken), "FixLogonBoundary");
    }

    public bool ShutdownRevert() => frameClient.ShutdownRevert();

    private static LmaxRealReadOnlyDependencyResult? ValidateOptions(LmaxReadOnlyFixSessionOptions options)
    {
        if (!string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                "FixFrameBoundaryProviderConfigRejected",
                "NonDemoEnvironment",
                "FIX provider environment must be Demo/read-only.");
        }

        if (!options.DemoReadOnly)
        {
            return Blocked(
                "FixFrameBoundaryProviderConfigRejected",
                "ReadOnlyFlagMissing",
                "FIX provider must be marked Demo/read-only.");
        }

        if (IsUnsafeLabel(options.SenderCompIdLabel) || IsUnsafeLabel(options.TargetCompIdLabel))
        {
            return Blocked(
                "FixFrameBoundaryProviderConfigRejected",
                "UnsafeFixSessionLabel",
                "FIX sender and target labels must be sanitized.");
        }

        if (options.HeartbeatIntervalSeconds is <= 0 or > 60)
        {
            return Blocked(
                "FixFrameBoundaryProviderConfigRejected",
                "InvalidHeartbeatInterval",
                "FIX heartbeat interval must be between one and sixty seconds.");
        }

        if (options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromSeconds(60))
        {
            return Blocked(
                "FixFrameBoundaryProviderConfigRejected",
                "InvalidTimeout",
                "FIX provider timeout must be between zero and sixty seconds.");
        }

        if (options.AllowedMessageTypes.Count == 0)
        {
            return Blocked(
                "FixFrameBoundaryProviderConfigRejected",
                "AllowedFixMessageTypesMissing",
                "FIX provider must declare allowed read-only message categories.");
        }

        foreach (var messageType in options.AllowedMessageTypes)
        {
            if (ForbiddenMessageTypes.Contains(messageType))
            {
                return Blocked(
                    "FixFrameBoundaryProviderConfigRejected",
                    "ForbiddenFixMessageType",
                    $"FIX message category '{messageType}' is forbidden for read-only runtime activation.");
            }

            if (!AllowedMessageTypes.Contains(messageType))
            {
                return Blocked(
                    "FixFrameBoundaryProviderConfigRejected",
                    "UnsupportedFixMessageType",
                    $"FIX message category '{messageType}' is not in the approved read-only boundary set.");
            }
        }

        return null;
    }

    private static LmaxRealReadOnlyDependencyResult? ValidateCredentialRecord(
        LmaxReadOnlyCredentialSanitizationRecord accessRecord)
    {
        if (!accessRecord.AccessPolicyAccepted ||
            accessRecord.SensitiveMaterialReturned ||
            accessRecord.SensitiveMaterialPrinted ||
            accessRecord.SensitiveMaterialStored)
        {
            return Blocked(
                "FixFrameBoundaryProviderBlockedByCredentialPolicy",
                "CredentialPolicyNotSafe",
                "FIX provider requires accepted sanitized credential evidence with no returned, printed, or stored sensitive material.");
        }

        return null;
    }

    private static bool IsUnsafeLabel(string label)
        => string.IsNullOrWhiteSpace(label) ||
           label.Contains("://", StringComparison.Ordinal) ||
           label.Contains('@', StringComparison.Ordinal) ||
           label.Contains("password", StringComparison.OrdinalIgnoreCase) ||
           label.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
           label.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
           label.Contains("554=", StringComparison.OrdinalIgnoreCase);

    private static LmaxRealReadOnlyDependencyResult Sanitize(
        LmaxRealReadOnlyDependencyResult result,
        string fallbackCategory)
        => new(
            result.Status,
            SanitizeFixMaterial(result.SanitizedStatus) ?? "FixFrameBoundaryProviderStatusSanitized",
            SanitizeFixMaterial(result.SanitizedErrorCategory) ?? fallbackCategory,
            SanitizeFixMaterial(result.SanitizedErrorMessage));

    private static LmaxRealReadOnlyDependencyResult Blocked(string status, string category, string message)
        => new(
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            status,
            category,
            SanitizeFixMaterial(message));

    private static string? SanitizeFixMaterial(string? value)
    {
        var sanitized = LmaxRealReadOnlyCredentialDependency.Sanitize(value);
        if (sanitized is null)
        {
            return null;
        }

        return sanitized
            .Replace("RawFix", "[redacted-fix]", StringComparison.OrdinalIgnoreCase)
            .Replace("35=D", "[redacted-fix-type]", StringComparison.OrdinalIgnoreCase)
            .Replace("35=F", "[redacted-fix-type]", StringComparison.OrdinalIgnoreCase)
            .Replace("35=H", "[redacted-fix-type]", StringComparison.OrdinalIgnoreCase)
            .Replace("35=AE", "[redacted-fix-type]", StringComparison.OrdinalIgnoreCase)
            .Replace("35=8", "[redacted-fix-type]", StringComparison.OrdinalIgnoreCase)
            .Replace("session", "FIX-session", StringComparison.OrdinalIgnoreCase);
    }
}
