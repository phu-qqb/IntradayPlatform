namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxReadOnlyMarketDataRequestOptions(
    string EnvironmentLabel,
    bool DemoReadOnly,
    string RequestTypeLabel,
    string SnapshotModeLabel,
    TimeSpan Timeout,
    IReadOnlyList<string> AllowedMessageTypes,
    bool ExternalMarketDataRequestExecutionApproved = false)
{
    public static LmaxReadOnlyMarketDataRequestOptions DemoReadOnlyDisabled(
        string requestTypeLabel = "ReadOnlyMarketDataRequest",
        string snapshotModeLabel = "SnapshotOrStatus",
        TimeSpan? timeout = null)
        => new(
            "Demo/read-only",
            DemoReadOnly: true,
            requestTypeLabel,
            snapshotModeLabel,
            timeout ?? TimeSpan.FromSeconds(15),
            DefaultAllowedReadOnlyMessageTypes,
            ExternalMarketDataRequestExecutionApproved: false);

    public static IReadOnlyList<string> DefaultAllowedReadOnlyMessageTypes { get; } =
    [
        "MarketDataSnapshotFullRefresh",
        "MarketDataRequestReject",
        "BusinessMessageReject",
        "Reject",
        "Logout"
    ];
}

public interface ILmaxReadOnlyMarketDataFrameClient
{
    LmaxReadOnlyMarketDataSessionClientResult RequestReadOnlyStatus(
        LmaxReadOnlyMarketDataRequestOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);

    bool ShutdownRevert();
}

public sealed class LmaxRealReadOnlyMarketDataFrameBoundaryProvider : ILmaxRealReadOnlyMarketDataFrameBoundaryProvider
{
    private static readonly HashSet<string> AllowedMessageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "W",
        "Y",
        "j",
        "3",
        "5",
        "MarketDataSnapshotFullRefresh",
        "MarketDataRequestReject",
        "BusinessMessageReject",
        "Reject",
        "Logout"
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

    private readonly LmaxReadOnlyMarketDataRequestOptions options;
    private readonly ILmaxReadOnlyMarketDataFrameClient frameClient;

    public LmaxRealReadOnlyMarketDataFrameBoundaryProvider(
        LmaxReadOnlyMarketDataRequestOptions options,
        ILmaxReadOnlyMarketDataFrameClient frameClient)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.frameClient = frameClient ?? throw new ArgumentNullException(nameof(frameClient));
    }

    public LmaxReadOnlyMarketDataSessionClientResult RequestReadOnlyStatus(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
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
                scope,
                "MarketDataFrameBoundaryProviderBlockedBeforeRequestUse",
                "SafetyConstraintFailed",
                string.Join("; ", scopeIssues.Select(x => x.Code)));
        }

        if (!options.ExternalMarketDataRequestExecutionApproved)
        {
            return Blocked(
                scope,
                "MarketDataFrameBoundaryProviderExecutionNotApproved",
                "MarketDataExecutionNotApproved",
                "MarketData provider requires a future explicitly approved phase before constructing or sending read-only request frames.");
        }

        return Sanitize(scope, frameClient.RequestReadOnlyStatus(options, scope, cancellationToken), "MarketDataBoundary");
    }

    public bool ShutdownRevert() => frameClient.ShutdownRevert();

    private static LmaxReadOnlyMarketDataSessionClientResult? ValidateOptions(
        LmaxReadOnlyMarketDataRequestOptions options)
    {
        if (!string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                [],
                "MarketDataFrameBoundaryProviderConfigRejected",
                "NonDemoEnvironment",
                "MarketData provider environment must be Demo/read-only.");
        }

        if (!options.DemoReadOnly)
        {
            return Blocked(
                [],
                "MarketDataFrameBoundaryProviderConfigRejected",
                "ReadOnlyFlagMissing",
                "MarketData provider must be marked Demo/read-only.");
        }

        if (IsUnsafeLabel(options.RequestTypeLabel) || IsUnsafeLabel(options.SnapshotModeLabel))
        {
            return Blocked(
                [],
                "MarketDataFrameBoundaryProviderConfigRejected",
                "UnsafeMarketDataLabel",
                "MarketData provider labels must be sanitized.");
        }

        if (options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromSeconds(60))
        {
            return Blocked(
                [],
                "MarketDataFrameBoundaryProviderConfigRejected",
                "InvalidTimeout",
                "MarketData provider timeout must be between zero and sixty seconds.");
        }

        if (options.AllowedMessageTypes.Count == 0)
        {
            return Blocked(
                [],
                "MarketDataFrameBoundaryProviderConfigRejected",
                "AllowedMarketDataMessageTypesMissing",
                "MarketData provider must declare allowed read-only message categories.");
        }

        foreach (var messageType in options.AllowedMessageTypes)
        {
            if (ForbiddenMessageTypes.Contains(messageType))
            {
                return Blocked(
                    [],
                    "MarketDataFrameBoundaryProviderConfigRejected",
                    "ForbiddenMarketDataMessageType",
                    $"MarketData message category '{messageType}' is forbidden for read-only runtime activation.");
            }

            if (!AllowedMessageTypes.Contains(messageType))
            {
                return Blocked(
                    [],
                    "MarketDataFrameBoundaryProviderConfigRejected",
                    "UnsupportedMarketDataMessageType",
                    $"MarketData message category '{messageType}' is not in the approved read-only boundary set.");
            }
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
           label.Contains("554=", StringComparison.OrdinalIgnoreCase) ||
           label.Contains("35=D", StringComparison.OrdinalIgnoreCase) ||
           label.Contains("35=F", StringComparison.OrdinalIgnoreCase) ||
           label.Contains("35=H", StringComparison.OrdinalIgnoreCase) ||
           label.Contains("35=AE", StringComparison.OrdinalIgnoreCase) ||
           label.Contains("35=8", StringComparison.OrdinalIgnoreCase);

    private static LmaxReadOnlyMarketDataSessionClientResult Sanitize(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyMarketDataSessionClientResult result,
        string fallbackCategory)
        => new LmaxReadOnlyMarketDataSessionClientResult(
            SanitizeStatuses(scope, result.InstrumentStatuses),
            SanitizeMarketDataMaterial(result.SanitizedStatus) ?? "MarketDataFrameBoundaryProviderStatusSanitized",
            SanitizeMarketDataMaterial(result.SanitizedErrorCategory) ?? fallbackCategory,
            SanitizeMarketDataMaterial(result.SanitizedErrorMessage))
        {
            MarketDataRequestWriteAttempted = result.MarketDataRequestWriteAttempted,
            MarketDataRequestWriteSucceeded = result.MarketDataRequestWriteSucceeded,
            MarketDataRequestResponseReadAttempted = result.MarketDataRequestResponseReadAttempted,
            MarketDataRequestReachedBoundedResponseClassification = result.MarketDataRequestReachedBoundedResponseClassification,
            MarketDataRejectSanitizedSubcategory = SanitizeMarketDataMaterial(result.MarketDataRejectSanitizedSubcategory),
            SessionRejectSanitizedSubcategory = SanitizeMarketDataMaterial(result.SessionRejectSanitizedSubcategory),
            RejectReasonExtractionSource = SanitizeMarketDataMaterial(result.RejectReasonExtractionSource),
            SessionRejectRefTagIdSanitizedCategory = SanitizeMarketDataMaterial(result.SessionRejectRefTagIdSanitizedCategory),
            SessionRejectReasonSanitizedCategory = SanitizeMarketDataMaterial(result.SessionRejectReasonSanitizedCategory),
            SessionRejectRefMsgTypeSanitizedCategory = SanitizeMarketDataMaterial(result.SessionRejectRefMsgTypeSanitizedCategory),
            MarketDataEntriesObserved = result.MarketDataEntriesObserved,
            MarketDataSanitizedEntryCount = result.MarketDataSanitizedEntryCount,
            MarketDataEntriesEvidenceCategory = SanitizeMarketDataMaterial(result.MarketDataEntriesEvidenceCategory),
            MarketDataEntriesReportingSource = SanitizeMarketDataMaterial(result.MarketDataEntriesReportingSource),
            MarketDataEntriesNotAvailableReason = SanitizeMarketDataMaterial(result.MarketDataEntriesNotAvailableReason),
            LogoutObserved = result.LogoutObserved,
            LogoutSourceCategory = SanitizeMarketDataMaterial(result.LogoutSourceCategory),
            LogoutReasonSanitizedCategory = SanitizeMarketDataMaterial(result.LogoutReasonSanitizedCategory),
            LogoutTextPresentSanitized = result.LogoutTextPresentSanitized,
            LogoutAfterInstrument = SanitizeMarketDataMaterial(result.LogoutAfterInstrument),
            LogoutAfterSecurityIdSanitized = SanitizeMarketDataMaterial(result.LogoutAfterSecurityIdSanitized),
            LogoutTimingCategory = SanitizeMarketDataMaterial(result.LogoutTimingCategory),
            LogoutReasonExtractionSource = SanitizeMarketDataMaterial(result.LogoutReasonExtractionSource)
        };

    private static IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> SanitizeStatuses(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> statuses)
    {
        var statusBySymbol = statuses.ToDictionary(x => x.Symbol, StringComparer.OrdinalIgnoreCase);

        return scope.Instruments.Select(instrument =>
        {
            if (!statusBySymbol.TryGetValue(instrument.Symbol, out var status))
            {
                return new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                    instrument.Symbol,
                    instrument.SecurityId,
                    instrument.SecurityIdSource,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed,
                    MarketDataSnapshotCount: 0,
                    MarketDataRequestRejectCount: 0,
                    BusinessMessageRejectCount: 0,
                    SessionRejectCount: 0,
                    "MarketDataFrameBoundaryProviderInstrumentStatusMissingSanitized",
                    "InstrumentStatusMissing",
                    "Approved instrument did not return sanitized market-data frame evidence.",
                    instrument.Caveat);
            }

            return new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                instrument.Symbol,
                instrument.SecurityId,
                instrument.SecurityIdSource,
                status.MarketDataBoundary,
                Math.Max(0, status.MarketDataSnapshotCount),
                Math.Max(0, status.MarketDataRequestRejectCount),
                Math.Max(0, status.BusinessMessageRejectCount),
                Math.Max(0, status.SessionRejectCount),
                SanitizeMarketDataMaterial(status.SanitizedStatus) ?? "MarketDataFrameBoundaryProviderInstrumentStatusSanitized",
                SanitizeMarketDataMaterial(status.SanitizedErrorCategory),
                SanitizeMarketDataMaterial(status.SanitizedErrorMessage),
                instrument.Caveat);
        }).ToList();
    }

    private static LmaxReadOnlyMarketDataSessionClientResult Blocked(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        string status,
        string category,
        string message)
        => Blocked(scope.Instruments, status, category, message);

    private static LmaxReadOnlyMarketDataSessionClientResult Blocked(
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument> instruments,
        string status,
        string category,
        string message)
        => new(
            instruments.Select(instrument => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                instrument.Symbol,
                instrument.SecurityId,
                instrument.SecurityIdSource,
                LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
                MarketDataSnapshotCount: 0,
                MarketDataRequestRejectCount: 0,
                BusinessMessageRejectCount: 0,
                SessionRejectCount: 0,
                status,
                category,
                SanitizeMarketDataMaterial(message),
                instrument.Caveat)).ToList(),
            status,
            category,
            SanitizeMarketDataMaterial(message));

    private static string? SanitizeMarketDataMaterial(string? value)
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
            .Replace("554=", "[redacted-fix-tag]", StringComparison.OrdinalIgnoreCase);
    }
}
