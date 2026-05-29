namespace QQ.Production.Intraday.Infrastructure.Lmax;

public delegate LmaxRealReadOnlyDependencyResult LmaxReadOnlySocketConnectOperation(
    LmaxReadOnlySocketConnectionOptions options,
    LmaxTemporaryReadOnlyRuntimeActivationScope scope,
    CancellationToken cancellationToken);

public delegate LmaxRealReadOnlyDependencyResult LmaxReadOnlyTlsHandshakeOperation(
    LmaxReadOnlyTlsConnectionOptions options,
    LmaxTemporaryReadOnlyRuntimeActivationScope scope,
    CancellationToken cancellationToken);

public delegate LmaxRealReadOnlyDependencyResult LmaxReadOnlyFixSessionOperation(
    LmaxReadOnlyFixSessionOptions options,
    LmaxTemporaryReadOnlyRuntimeActivationScope scope,
    LmaxReadOnlyCredentialSanitizationRecord accessRecord,
    CancellationToken cancellationToken);

public delegate LmaxReadOnlyMarketDataSessionClientResult LmaxReadOnlyMarketDataOperation(
    LmaxReadOnlyMarketDataRequestOptions options,
    LmaxTemporaryReadOnlyRuntimeActivationScope scope,
    CancellationToken cancellationToken);

public delegate LmaxRealReadOnlySecretAccessResult LmaxReadOnlyCredentialConfigOperation(
    LmaxReadOnlyCredentialConfigOptions options,
    LmaxTemporaryReadOnlyRuntimeActivationScope scope,
    LmaxReadOnlyCredentialAccessPolicy policy,
    CancellationToken cancellationToken);

public sealed class LmaxRealReadOnlySocketConnectionClient : ILmaxReadOnlySocketConnectionClient
{
    private readonly LmaxReadOnlySocketConnectOperation connectOperation;
    private readonly Func<bool> shutdownOperation;

    public LmaxRealReadOnlySocketConnectionClient(
        LmaxReadOnlySocketConnectOperation? connectOperation = null,
        Func<bool>? shutdownOperation = null)
    {
        this.connectOperation = connectOperation ?? DefaultNotConfigured;
        this.shutdownOperation = shutdownOperation ?? (() => true);
    }

    public LmaxRealReadOnlyDependencyResult OpenTcp(
        LmaxReadOnlySocketConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        var blocked = ValidateCommonScope(scope, "SocketClientBlockedBeforeTcpUse");
        if (blocked is not null)
        {
            return blocked;
        }

        if (!options.DemoReadOnly ||
            !string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                "SocketClientConfigRejected",
                "NonDemoReadOnlySocketConfig",
                "Socket client requires Demo/read-only options.");
        }

        if (options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromSeconds(60))
        {
            return Blocked(
                "SocketClientConfigRejected",
                "InvalidTimeout",
                "Socket client timeout must be between zero and sixty seconds.");
        }

        return Sanitize(connectOperation(options, scope, cancellationToken), "TcpBoundary");
    }

    public bool ShutdownRevert() => shutdownOperation();

    private static LmaxRealReadOnlyDependencyResult DefaultNotConfigured(
        LmaxReadOnlySocketConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken)
        => Blocked(
            "SocketClientExecutionDependencyMissing",
            "SocketConnectorNotConfigured",
            "Socket client is implemented but requires an explicitly supplied connector operation in a future approved phase.");

    internal static LmaxRealReadOnlyDependencyResult? ValidateCommonScope(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        string status)
    {
        var issues = LmaxExecutableReadOnlyCredentialBoundary.ValidateScope(scope);
        if (issues.Count == 0)
        {
            return null;
        }

        return Blocked(status, "SafetyConstraintFailed", string.Join("; ", issues.Select(x => x.Code)));
    }

    internal static LmaxRealReadOnlyDependencyResult Sanitize(
        LmaxRealReadOnlyDependencyResult result,
        string fallbackCategory)
        => new(
            result.Status,
            SanitizeProtocolMaterial(result.SanitizedStatus) ?? "ProviderClientStatusSanitized",
            SanitizeProtocolMaterial(result.SanitizedErrorCategory) ?? fallbackCategory,
            SanitizeProtocolMaterial(result.SanitizedErrorMessage));

    internal static LmaxRealReadOnlyDependencyResult Blocked(string status, string category, string message)
        => new(
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            status,
            category,
            SanitizeProtocolMaterial(message));

    internal static string? SanitizeProtocolMaterial(string? value)
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

public sealed class LmaxRealReadOnlyTlsHandshakeClient : ILmaxReadOnlyTlsHandshakeClient
{
    private readonly LmaxReadOnlyTlsHandshakeOperation handshakeOperation;
    private readonly Func<bool> shutdownOperation;

    public LmaxRealReadOnlyTlsHandshakeClient(
        LmaxReadOnlyTlsHandshakeOperation? handshakeOperation = null,
        Func<bool>? shutdownOperation = null)
    {
        this.handshakeOperation = handshakeOperation ?? DefaultNotConfigured;
        this.shutdownOperation = shutdownOperation ?? (() => true);
    }

    public LmaxRealReadOnlyDependencyResult OpenTls(
        LmaxReadOnlyTlsConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        var blocked = LmaxRealReadOnlySocketConnectionClient.ValidateCommonScope(scope, "TlsClientBlockedBeforeHandshakeUse");
        if (blocked is not null)
        {
            return blocked;
        }

        if (!options.DemoReadOnly ||
            !string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return LmaxRealReadOnlySocketConnectionClient.Blocked(
                "TlsClientConfigRejected",
                "NonDemoReadOnlyTlsConfig",
                "TLS client requires Demo/read-only options.");
        }

        if (options.CertificateValidationPolicyLabel.Contains("private", StringComparison.OrdinalIgnoreCase) ||
            options.CertificateValidationPolicyLabel.Contains("-----BEGIN", StringComparison.OrdinalIgnoreCase))
        {
            return LmaxRealReadOnlySocketConnectionClient.Blocked(
                "TlsClientConfigRejected",
                "UnsafeCertificatePolicy",
                "TLS certificate policy must not contain private material.");
        }

        return LmaxRealReadOnlySocketConnectionClient.Sanitize(
            handshakeOperation(options, scope, cancellationToken),
            "TlsBoundary");
    }

    public bool ShutdownRevert() => shutdownOperation();

    private static LmaxRealReadOnlyDependencyResult DefaultNotConfigured(
        LmaxReadOnlyTlsConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken)
        => LmaxRealReadOnlySocketConnectionClient.Blocked(
            "TlsClientExecutionDependencyMissing",
            "TlsHandshakeFactoryNotConfigured",
            "TLS client is implemented but requires an explicitly supplied handshake operation in a future approved phase.");
}

public sealed class LmaxRealReadOnlyFixFrameClient : ILmaxReadOnlyFixFrameClient
{
    private static readonly HashSet<string> ForbiddenOperationalTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "NewOrderSingle",
        "OrderCancelRequest",
        "OrderStatusRequest",
        "TradeCaptureReportRequest",
        "ExecutionReport",
        "Replay",
        "ShadowReplay",
        "TradingMutation",
        "35=D",
        "35=F",
        "35=H",
        "35=AE",
        "35=8"
    };

    private readonly LmaxReadOnlyFixSessionOperation sessionOperation;
    private readonly Func<bool> shutdownOperation;

    public LmaxRealReadOnlyFixFrameClient(
        LmaxReadOnlyFixSessionOperation? sessionOperation = null,
        Func<bool>? shutdownOperation = null)
    {
        this.sessionOperation = sessionOperation ?? DefaultNotConfigured;
        this.shutdownOperation = shutdownOperation ?? (() => true);
    }

    public LmaxRealReadOnlyDependencyResult OpenSessionLogon(
        LmaxReadOnlyFixSessionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord accessRecord,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(accessRecord);
        cancellationToken.ThrowIfCancellationRequested();

        var blocked = LmaxRealReadOnlySocketConnectionClient.ValidateCommonScope(scope, "FixClientBlockedBeforeLogonUse");
        if (blocked is not null)
        {
            return blocked;
        }

        if (!options.DemoReadOnly ||
            !string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return LmaxRealReadOnlySocketConnectionClient.Blocked(
                "FixClientConfigRejected",
                "NonDemoReadOnlyFixConfig",
                "FIX client requires Demo/read-only options.");
        }

        if (!accessRecord.AccessPolicyAccepted ||
            accessRecord.SensitiveMaterialReturned ||
            accessRecord.SensitiveMaterialPrinted ||
            accessRecord.SensitiveMaterialStored)
        {
            return LmaxRealReadOnlySocketConnectionClient.Blocked(
                "FixClientCredentialPolicyRejected",
                "CredentialPolicyNotSafe",
                "FIX client requires sanitized credential evidence.");
        }

        foreach (var messageType in options.AllowedMessageTypes)
        {
            if (ForbiddenOperationalTerms.Contains(messageType))
            {
                return LmaxRealReadOnlySocketConnectionClient.Blocked(
                    "FixClientForbiddenMessageTypeRejected",
                    "ForbiddenFixMessageType",
                    $"FIX message category '{messageType}' is forbidden for read-only runtime activation.");
            }
        }

        return LmaxRealReadOnlySocketConnectionClient.Sanitize(
            sessionOperation(options, scope, accessRecord, cancellationToken),
            "FixLogonBoundary");
    }

    public bool ShutdownRevert() => shutdownOperation();

    private static LmaxRealReadOnlyDependencyResult DefaultNotConfigured(
        LmaxReadOnlyFixSessionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord accessRecord,
        CancellationToken cancellationToken)
        => LmaxRealReadOnlySocketConnectionClient.Blocked(
            "FixClientExecutionDependencyMissing",
            "FixSessionOperationNotConfigured",
            "FIX client is implemented but requires an explicitly supplied read-only FIX operation in a future approved phase.");
}

public sealed class LmaxRealReadOnlyMarketDataFrameClient : ILmaxReadOnlyMarketDataFrameClient
{
    private readonly LmaxReadOnlyMarketDataOperation marketDataOperation;
    private readonly Func<bool> shutdownOperation;

    public LmaxRealReadOnlyMarketDataFrameClient(
        LmaxReadOnlyMarketDataOperation? marketDataOperation = null,
        Func<bool>? shutdownOperation = null)
    {
        this.marketDataOperation = marketDataOperation ?? DefaultNotConfigured;
        this.shutdownOperation = shutdownOperation ?? (() => true);
    }

    public LmaxReadOnlyMarketDataSessionClientResult RequestReadOnlyStatus(
        LmaxReadOnlyMarketDataRequestOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        var blocked = LmaxRealReadOnlySocketConnectionClient.ValidateCommonScope(scope, "MarketDataClientBlockedBeforeRequestUse");
        if (blocked is not null)
        {
            return Blocked(scope, blocked.SanitizedStatus, blocked.SanitizedErrorCategory, blocked.SanitizedErrorMessage);
        }

        if (!options.DemoReadOnly ||
            !string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                scope,
                "MarketDataClientConfigRejected",
                "NonDemoReadOnlyMarketDataConfig",
                "MarketData client requires Demo/read-only options.");
        }

        foreach (var instrument in scope.Instruments)
        {
            var approved = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Find(instrument.Symbol);
            if (approved is null ||
                !string.Equals(approved.SecurityId, instrument.SecurityId, StringComparison.Ordinal) ||
                !string.Equals(approved.SecurityIdSource, instrument.SecurityIdSource, StringComparison.Ordinal))
            {
                return Blocked(
                    scope,
                    "MarketDataClientInstrumentRejected",
                    "NonApprovedInstrument",
                    $"Instrument '{instrument.Symbol}' is not approved for read-only market-data request intent.");
            }

            if (string.Equals(instrument.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(instrument.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal))
            {
                return Blocked(
                    scope,
                    "MarketDataClientInstrumentRejected",
                    "UsdJpyCaveatMissing",
                    "USDJPY read-only request intent requires the approved caveat.");
            }
        }

        return Sanitize(scope, marketDataOperation(options, scope, cancellationToken));
    }

    public bool ShutdownRevert() => shutdownOperation();

    private static LmaxReadOnlyMarketDataSessionClientResult DefaultNotConfigured(
        LmaxReadOnlyMarketDataRequestOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken)
        => Blocked(
            scope,
            "MarketDataClientExecutionDependencyMissing",
            "MarketDataOperationNotConfigured",
            "MarketData client is implemented but requires an explicitly supplied read-only market-data operation in a future approved phase.");

    private static LmaxReadOnlyMarketDataSessionClientResult Sanitize(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyMarketDataSessionClientResult result)
        => new LmaxReadOnlyMarketDataSessionClientResult(
            result.InstrumentStatuses.Select(status => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                status.Symbol,
                status.SecurityId,
                status.SecurityIdSource,
                status.MarketDataBoundary,
                Math.Max(0, status.MarketDataSnapshotCount),
                Math.Max(0, status.MarketDataRequestRejectCount),
                Math.Max(0, status.BusinessMessageRejectCount),
                Math.Max(0, status.SessionRejectCount),
                LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(status.SanitizedStatus) ?? "MarketDataClientInstrumentStatusSanitized",
                LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(status.SanitizedErrorCategory),
                LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(status.SanitizedErrorMessage),
                scope.Instruments.FirstOrDefault(x => string.Equals(x.Symbol, status.Symbol, StringComparison.OrdinalIgnoreCase))?.Caveat)).ToList(),
            LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.SanitizedStatus) ?? "MarketDataClientStatusSanitized",
            LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.SanitizedErrorCategory),
            LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.SanitizedErrorMessage))
        {
            MarketDataRequestWriteAttempted = result.MarketDataRequestWriteAttempted,
            MarketDataRequestWriteSucceeded = result.MarketDataRequestWriteSucceeded,
            MarketDataRequestResponseReadAttempted = result.MarketDataRequestResponseReadAttempted,
            MarketDataRequestReachedBoundedResponseClassification = result.MarketDataRequestReachedBoundedResponseClassification,
            MarketDataRejectSanitizedSubcategory = LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.MarketDataRejectSanitizedSubcategory),
            SessionRejectSanitizedSubcategory = LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.SessionRejectSanitizedSubcategory),
            RejectReasonExtractionSource = LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.RejectReasonExtractionSource),
            SessionRejectRefTagIdSanitizedCategory = LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.SessionRejectRefTagIdSanitizedCategory),
            SessionRejectReasonSanitizedCategory = LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.SessionRejectReasonSanitizedCategory),
            SessionRejectRefMsgTypeSanitizedCategory = LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.SessionRejectRefMsgTypeSanitizedCategory),
            MarketDataEntriesObserved = result.MarketDataEntriesObserved,
            MarketDataSanitizedEntryCount = result.MarketDataSanitizedEntryCount,
            MarketDataEntriesEvidenceCategory = LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.MarketDataEntriesEvidenceCategory),
            MarketDataEntriesReportingSource = LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.MarketDataEntriesReportingSource),
            MarketDataEntriesNotAvailableReason = LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.MarketDataEntriesNotAvailableReason),
            LogoutObserved = result.LogoutObserved,
            LogoutSourceCategory = LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.LogoutSourceCategory),
            LogoutReasonSanitizedCategory = LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.LogoutReasonSanitizedCategory),
            LogoutTextPresentSanitized = result.LogoutTextPresentSanitized,
            LogoutAfterInstrument = LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.LogoutAfterInstrument),
            LogoutAfterSecurityIdSanitized = LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.LogoutAfterSecurityIdSanitized),
            LogoutTimingCategory = LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.LogoutTimingCategory),
            LogoutReasonExtractionSource = LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.LogoutReasonExtractionSource)
        };

    private static LmaxReadOnlyMarketDataSessionClientResult Blocked(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        string status,
        string? category,
        string? message)
        => new(
            scope.Instruments.Select(instrument => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
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
                LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(message),
                instrument.Caveat)).ToList(),
            status,
            category,
            LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(message));
}

public sealed class LmaxRealReadOnlyCredentialConfigClient : ILmaxReadOnlyCredentialConfigClient
{
    private readonly LmaxReadOnlyCredentialConfigOperation credentialOperation;

    public LmaxRealReadOnlyCredentialConfigClient(
        LmaxReadOnlyCredentialConfigOperation? credentialOperation = null)
    {
        this.credentialOperation = credentialOperation ?? DefaultNotConfigured;
    }

    public LmaxRealReadOnlySecretAccessResult AccessDemoReadOnlyConfig(
        LmaxReadOnlyCredentialConfigOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialAccessPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();

        var blocked = LmaxRealReadOnlySocketConnectionClient.ValidateCommonScope(scope, "CredentialConfigClientBlockedBeforeSecretUse");
        if (blocked is not null)
        {
            return Blocked(blocked.SanitizedStatus, blocked.SanitizedErrorCategory, blocked.SanitizedErrorMessage);
        }

        if (!options.DemoReadOnly ||
            !string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(policy.Environment, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                "CredentialConfigClientConfigRejected",
                "NonDemoReadOnlyCredentialConfig",
                "Credential/config client requires Demo/read-only options and policy.");
        }

        if (!policy.FutureApprovedRuntimeAttemptRequired || !policy.RedactSensitiveFields)
        {
            return Blocked(
                "CredentialConfigClientPolicyRejected",
                "CredentialPolicyNotSafe",
                "Credential/config client requires future approval and redaction.");
        }

        return Sanitize(credentialOperation(options, scope, policy, cancellationToken));
    }

    private static LmaxRealReadOnlySecretAccessResult DefaultNotConfigured(
        LmaxReadOnlyCredentialConfigOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialAccessPolicy policy,
        CancellationToken cancellationToken)
        => new(
            AccessAllowed: true,
            RealSecretMaterialLoaded: false,
            SensitiveMaterialReturned: false,
            SensitiveMaterialPrinted: false,
            SensitiveMaterialStored: false,
            "CredentialConfigClientExecutionDependencyMissing",
            "CredentialConfigOperationNotConfigured",
            "Credential/config client is implemented but requires an explicitly supplied secure source operation in a future approved phase.");

    private static LmaxRealReadOnlySecretAccessResult Sanitize(LmaxRealReadOnlySecretAccessResult result)
        => new(
            result.AccessAllowed,
            result.RealSecretMaterialLoaded,
            result.SensitiveMaterialReturned,
            result.SensitiveMaterialPrinted,
            result.SensitiveMaterialStored,
            LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.SanitizedStatus) ?? "CredentialConfigClientAccessSanitized",
            LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.SanitizedErrorCategory),
            LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(result.SanitizedErrorMessage));

    private static LmaxRealReadOnlySecretAccessResult Blocked(string status, string? category, string? message)
        => new(
            AccessAllowed: false,
            RealSecretMaterialLoaded: false,
            SensitiveMaterialReturned: false,
            SensitiveMaterialPrinted: false,
            SensitiveMaterialStored: false,
            status,
            category,
            LmaxRealReadOnlySocketConnectionClient.SanitizeProtocolMaterial(message));
}

public static class LmaxReadOnlyProviderClientCompleteness
{
    private static readonly string[] ForbiddenPublicMethodTerms =
    [
        "Order",
        "Cancel",
        "TradeCapture",
        "OrderStatus",
        "ExecutionReport",
        "Replay",
        "ShadowReplay",
        "TradingMutation",
        "HostedService",
        "BackgroundWorker"
    ];

    public static LmaxReadOnlyProviderClientCompletenessResult Validate()
    {
        var clientTypes = new[]
        {
            typeof(LmaxRealReadOnlySocketConnectionClient),
            typeof(LmaxRealReadOnlyTlsHandshakeClient),
            typeof(LmaxRealReadOnlyFixFrameClient),
            typeof(LmaxRealReadOnlyMarketDataFrameClient),
            typeof(LmaxRealReadOnlyCredentialConfigClient)
        };

        var issues = new List<string>();
        foreach (var type in clientTypes)
        {
            if (type.Namespace?.Contains(".Tests.", StringComparison.OrdinalIgnoreCase) == true ||
                type.FullName?.Contains("+Fake", StringComparison.OrdinalIgnoreCase) == true ||
                type.Name.Contains("Fake", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"{type.Name}:TestFakeOnly");
            }

            foreach (var method in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (ForbiddenPublicMethodTerms.Any(term => method.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
                {
                    issues.Add($"{type.Name}:{method.Name}:ForbiddenPublicMethod");
                }
            }
        }

        _ = new LmaxRealReadOnlySocketConnectionClient();
        _ = new LmaxRealReadOnlyTlsHandshakeClient();
        _ = new LmaxRealReadOnlyFixFrameClient();
        _ = new LmaxRealReadOnlyMarketDataFrameClient();
        _ = new LmaxRealReadOnlyCredentialConfigClient();

        return new LmaxReadOnlyProviderClientCompletenessResult(
            SocketClientImplemented: typeof(ILmaxReadOnlySocketConnectionClient).IsAssignableFrom(typeof(LmaxRealReadOnlySocketConnectionClient)),
            TlsClientImplemented: typeof(ILmaxReadOnlyTlsHandshakeClient).IsAssignableFrom(typeof(LmaxRealReadOnlyTlsHandshakeClient)),
            FixClientImplemented: typeof(ILmaxReadOnlyFixFrameClient).IsAssignableFrom(typeof(LmaxRealReadOnlyFixFrameClient)),
            MarketDataClientImplemented: typeof(ILmaxReadOnlyMarketDataFrameClient).IsAssignableFrom(typeof(LmaxRealReadOnlyMarketDataFrameClient)),
            CredentialConfigClientImplemented: typeof(ILmaxReadOnlyCredentialConfigClient).IsAssignableFrom(typeof(LmaxRealReadOnlyCredentialConfigClient)),
            PublicSurfaceReadOnly: issues.Count == 0,
            Issues: issues);
    }
}

public sealed record LmaxReadOnlyProviderClientCompletenessResult(
    bool SocketClientImplemented,
    bool TlsClientImplemented,
    bool FixClientImplemented,
    bool MarketDataClientImplemented,
    bool CredentialConfigClientImplemented,
    bool PublicSurfaceReadOnly,
    IReadOnlyList<string> Issues)
{
    public bool Passed =>
        SocketClientImplemented &&
        TlsClientImplemented &&
        FixClientImplemented &&
        MarketDataClientImplemented &&
        CredentialConfigClientImplemented &&
        PublicSurfaceReadOnly;
}
