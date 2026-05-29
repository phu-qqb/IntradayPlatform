namespace QQ.Production.Intraday.Infrastructure.Lmax;

public interface ILmaxReadOnlySocketConnectOperationBinding
{
    LmaxRealReadOnlyDependencyResult Connect(
        LmaxReadOnlySocketConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);
}

public interface ILmaxReadOnlyTlsHandshakeOperationBinding
{
    LmaxRealReadOnlyDependencyResult Handshake(
        LmaxReadOnlyTlsConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);
}

public interface ILmaxReadOnlyFixSessionOperationBinding
{
    LmaxRealReadOnlyDependencyResult Open(
        LmaxReadOnlyFixSessionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord accessRecord,
        CancellationToken cancellationToken = default);
}

public interface ILmaxReadOnlyMarketDataOperationBinding
{
    LmaxReadOnlyMarketDataSessionClientResult Read(
        LmaxReadOnlyMarketDataRequestOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);
}

public interface ILmaxReadOnlyCredentialConfigOperationBinding
{
    LmaxRealReadOnlySecretAccessResult Access(
        LmaxReadOnlyCredentialConfigOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialAccessPolicy policy,
        CancellationToken cancellationToken = default);
}

public delegate LmaxRealReadOnlyDependencyResult LmaxSocketConnectOperationCore(
    LmaxReadOnlySocketConnectionOptions options,
    LmaxTemporaryReadOnlyRuntimeActivationScope scope,
    CancellationToken cancellationToken);

public delegate LmaxRealReadOnlyDependencyResult LmaxTlsHandshakeOperationCore(
    LmaxReadOnlyTlsConnectionOptions options,
    LmaxTemporaryReadOnlyRuntimeActivationScope scope,
    CancellationToken cancellationToken);

public delegate LmaxRealReadOnlyDependencyResult LmaxFixSessionOperationCore(
    LmaxReadOnlyFixSessionOptions options,
    LmaxTemporaryReadOnlyRuntimeActivationScope scope,
    LmaxReadOnlyCredentialSanitizationRecord accessRecord,
    CancellationToken cancellationToken);

public delegate LmaxReadOnlyMarketDataSessionClientResult LmaxMarketDataOperationCore(
    LmaxReadOnlyMarketDataRequestOptions options,
    LmaxTemporaryReadOnlyRuntimeActivationScope scope,
    CancellationToken cancellationToken);

public delegate LmaxRealReadOnlySecretAccessResult LmaxCredentialConfigOperationCore(
    LmaxReadOnlyCredentialConfigOptions options,
    LmaxTemporaryReadOnlyRuntimeActivationScope scope,
    LmaxReadOnlyCredentialAccessPolicy policy,
    CancellationToken cancellationToken);

public sealed class LmaxReadOnlySocketConnectOperationBinding : ILmaxReadOnlySocketConnectOperationBinding
{
    private readonly LmaxSocketConnectOperationCore connectCore;

    public LmaxReadOnlySocketConnectOperationBinding(LmaxSocketConnectOperationCore? connectCore = null)
    {
        this.connectCore = connectCore ?? DefaultNotConfigured;
    }

    public LmaxRealReadOnlyDependencyResult Connect(
        LmaxReadOnlySocketConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        var blocked = LmaxReadOnlyExecutionOperationSafety.ValidateScope(scope, "SocketOperationBlockedBeforeTcpUse");
        if (blocked is not null)
        {
            return blocked;
        }

        if (!options.DemoReadOnly ||
            !string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return LmaxReadOnlyExecutionOperationSafety.Blocked(
                "SocketOperationConfigRejected",
                "NonDemoReadOnlySocketConfig",
                "Socket operation requires Demo/read-only options.");
        }

        if (options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromSeconds(60))
        {
            return LmaxReadOnlyExecutionOperationSafety.Blocked(
                "SocketOperationConfigRejected",
                "InvalidTimeout",
                "Socket operation timeout must be between zero and sixty seconds.");
        }

        return LmaxReadOnlyExecutionOperationSafety.Sanitize(connectCore(options, scope, cancellationToken), "TcpBoundary");
    }

    public LmaxReadOnlySocketConnectOperation AsClientOperation() => Connect;

    private static LmaxRealReadOnlyDependencyResult DefaultNotConfigured(
        LmaxReadOnlySocketConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken)
        => LmaxReadOnlyExecutionOperationSafety.Blocked(
            "SocketOperationCoreNotConfigured",
            "SocketConnectorNotConfigured",
            "Socket operation binding is present, but a concrete connector core must be supplied in a future approved phase.");
}

public sealed class LmaxReadOnlyTlsHandshakeOperationBinding : ILmaxReadOnlyTlsHandshakeOperationBinding
{
    private readonly LmaxTlsHandshakeOperationCore handshakeCore;

    public LmaxReadOnlyTlsHandshakeOperationBinding(LmaxTlsHandshakeOperationCore? handshakeCore = null)
    {
        this.handshakeCore = handshakeCore ?? DefaultNotConfigured;
    }

    public LmaxRealReadOnlyDependencyResult Handshake(
        LmaxReadOnlyTlsConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        var blocked = LmaxReadOnlyExecutionOperationSafety.ValidateScope(scope, "TlsOperationBlockedBeforeHandshakeUse");
        if (blocked is not null)
        {
            return blocked;
        }

        if (!options.DemoReadOnly ||
            !string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return LmaxReadOnlyExecutionOperationSafety.Blocked(
                "TlsOperationConfigRejected",
                "NonDemoReadOnlyTlsConfig",
                "TLS operation requires Demo/read-only options.");
        }

        if (options.CertificateValidationPolicyLabel.Contains("private", StringComparison.OrdinalIgnoreCase) ||
            options.CertificateValidationPolicyLabel.Contains("-----BEGIN", StringComparison.OrdinalIgnoreCase))
        {
            return LmaxReadOnlyExecutionOperationSafety.Blocked(
                "TlsOperationConfigRejected",
                "UnsafeCertificatePolicy",
                "TLS operation certificate policy must not contain private material.");
        }

        return LmaxReadOnlyExecutionOperationSafety.Sanitize(handshakeCore(options, scope, cancellationToken), "TlsBoundary");
    }

    public LmaxReadOnlyTlsHandshakeOperation AsClientOperation() => Handshake;

    private static LmaxRealReadOnlyDependencyResult DefaultNotConfigured(
        LmaxReadOnlyTlsConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken)
        => LmaxReadOnlyExecutionOperationSafety.Blocked(
            "TlsOperationCoreNotConfigured",
            "TlsHandshakeCoreNotConfigured",
            "TLS operation binding is present, but a concrete TLS core must be supplied in a future approved phase.");
}

public sealed class LmaxReadOnlyFixSessionOperationBinding : ILmaxReadOnlyFixSessionOperationBinding
{
    private static readonly HashSet<string> ForbiddenCategories = new(StringComparer.OrdinalIgnoreCase)
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

    private readonly LmaxFixSessionOperationCore fixCore;

    public LmaxReadOnlyFixSessionOperationBinding(LmaxFixSessionOperationCore? fixCore = null)
    {
        this.fixCore = fixCore ?? DefaultNotConfigured;
    }

    public LmaxRealReadOnlyDependencyResult Open(
        LmaxReadOnlyFixSessionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord accessRecord,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(accessRecord);
        cancellationToken.ThrowIfCancellationRequested();

        var blocked = LmaxReadOnlyExecutionOperationSafety.ValidateScope(scope, "FixOperationBlockedBeforeLogonUse");
        if (blocked is not null)
        {
            return blocked;
        }

        if (!options.DemoReadOnly ||
            !string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return LmaxReadOnlyExecutionOperationSafety.Blocked(
                "FixOperationConfigRejected",
                "NonDemoReadOnlyFixConfig",
                "FIX operation requires Demo/read-only options.");
        }

        if (!accessRecord.AccessPolicyAccepted ||
            accessRecord.SensitiveMaterialReturned ||
            accessRecord.SensitiveMaterialPrinted ||
            accessRecord.SensitiveMaterialStored)
        {
            return LmaxReadOnlyExecutionOperationSafety.Blocked(
                "FixOperationCredentialPolicyRejected",
                "CredentialPolicyNotSafe",
                "FIX operation requires sanitized credential evidence.");
        }

        foreach (var messageType in options.AllowedMessageTypes)
        {
            if (ForbiddenCategories.Contains(messageType))
            {
                return LmaxReadOnlyExecutionOperationSafety.Blocked(
                    "FixOperationForbiddenMessageTypeRejected",
                    "ForbiddenFixMessageType",
                    $"FIX category '{messageType}' is forbidden for read-only operation.");
            }
        }

        return LmaxReadOnlyExecutionOperationSafety.Sanitize(fixCore(options, scope, accessRecord, cancellationToken), "FixLogonBoundary");
    }

    public LmaxReadOnlyFixSessionOperation AsClientOperation() => Open;

    private static LmaxRealReadOnlyDependencyResult DefaultNotConfigured(
        LmaxReadOnlyFixSessionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord accessRecord,
        CancellationToken cancellationToken)
        => LmaxReadOnlyExecutionOperationSafety.Blocked(
            "FixOperationCoreNotConfigured",
            "FixSessionCoreNotConfigured",
            "FIX operation binding is present, but a concrete read-only FIX core must be supplied in a future approved phase.");
}

public sealed class LmaxReadOnlyMarketDataOperationBinding : ILmaxReadOnlyMarketDataOperationBinding
{
    private readonly LmaxMarketDataOperationCore marketDataCore;

    public LmaxReadOnlyMarketDataOperationBinding(LmaxMarketDataOperationCore? marketDataCore = null)
    {
        this.marketDataCore = marketDataCore ?? DefaultNotConfigured;
    }

    public LmaxReadOnlyMarketDataSessionClientResult Read(
        LmaxReadOnlyMarketDataRequestOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        var blocked = LmaxReadOnlyExecutionOperationSafety.ValidateScope(scope, "MarketDataOperationBlockedBeforeRequestUse");
        if (blocked is not null)
        {
            return Blocked(scope, blocked.SanitizedStatus, blocked.SanitizedErrorCategory, blocked.SanitizedErrorMessage);
        }

        if (!options.DemoReadOnly ||
            !string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                scope,
                "MarketDataOperationConfigRejected",
                "NonDemoReadOnlyMarketDataConfig",
                "MarketData operation requires Demo/read-only options.");
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
                    "MarketDataOperationInstrumentRejected",
                    "NonApprovedInstrument",
                    $"Instrument '{instrument.Symbol}' is not approved for read-only market-data operation.");
            }

            if (string.Equals(instrument.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(instrument.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal))
            {
                return Blocked(
                    scope,
                    "MarketDataOperationInstrumentRejected",
                    "UsdJpyCaveatMissing",
                    "USDJPY read-only operation requires the approved caveat.");
            }
        }

        return Sanitize(scope, marketDataCore(options, scope, cancellationToken));
    }

    public LmaxReadOnlyMarketDataOperation AsClientOperation() => Read;

    private static LmaxReadOnlyMarketDataSessionClientResult DefaultNotConfigured(
        LmaxReadOnlyMarketDataRequestOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken)
        => Blocked(
            scope,
            "MarketDataOperationCoreNotConfigured",
            "MarketDataCoreNotConfigured",
            "MarketData operation binding is present, but a concrete read-only market-data core must be supplied in a future approved phase.");

    private static LmaxReadOnlyMarketDataSessionClientResult Sanitize(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyMarketDataSessionClientResult result)
        => new(
            result.InstrumentStatuses.Select(status => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                status.Symbol,
                status.SecurityId,
                status.SecurityIdSource,
                status.MarketDataBoundary,
                Math.Max(0, status.MarketDataSnapshotCount),
                Math.Max(0, status.MarketDataRequestRejectCount),
                Math.Max(0, status.BusinessMessageRejectCount),
                Math.Max(0, status.SessionRejectCount),
                LmaxReadOnlyExecutionOperationSafety.SanitizeText(status.SanitizedStatus) ?? "MarketDataOperationInstrumentStatusSanitized",
                LmaxReadOnlyExecutionOperationSafety.SanitizeText(status.SanitizedErrorCategory),
                LmaxReadOnlyExecutionOperationSafety.SanitizeText(status.SanitizedErrorMessage),
                scope.Instruments.FirstOrDefault(x => string.Equals(x.Symbol, status.Symbol, StringComparison.OrdinalIgnoreCase))?.Caveat)).ToList(),
            LmaxReadOnlyExecutionOperationSafety.SanitizeText(result.SanitizedStatus) ?? "MarketDataOperationStatusSanitized",
            LmaxReadOnlyExecutionOperationSafety.SanitizeText(result.SanitizedErrorCategory),
            LmaxReadOnlyExecutionOperationSafety.SanitizeText(result.SanitizedErrorMessage));

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
                0,
                0,
                0,
                0,
                status,
                category,
                LmaxReadOnlyExecutionOperationSafety.SanitizeText(message),
                instrument.Caveat)).ToList(),
            status,
            category,
            LmaxReadOnlyExecutionOperationSafety.SanitizeText(message));
}

public sealed class LmaxReadOnlyCredentialConfigOperationBinding : ILmaxReadOnlyCredentialConfigOperationBinding
{
    private readonly LmaxCredentialConfigOperationCore credentialCore;

    public LmaxReadOnlyCredentialConfigOperationBinding(LmaxCredentialConfigOperationCore? credentialCore = null)
    {
        this.credentialCore = credentialCore ?? DefaultNotConfigured;
    }

    public LmaxRealReadOnlySecretAccessResult Access(
        LmaxReadOnlyCredentialConfigOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialAccessPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();

        var blocked = LmaxReadOnlyExecutionOperationSafety.ValidateScope(scope, "CredentialOperationBlockedBeforeSecretUse");
        if (blocked is not null)
        {
            return Blocked(blocked.SanitizedStatus, blocked.SanitizedErrorCategory, blocked.SanitizedErrorMessage);
        }

        if (!options.DemoReadOnly ||
            !string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(policy.Environment, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                "CredentialOperationConfigRejected",
                "NonDemoReadOnlyCredentialConfig",
                "Credential/config operation requires Demo/read-only options and policy.");
        }

        if (!policy.FutureApprovedRuntimeAttemptRequired || !policy.RedactSensitiveFields)
        {
            return Blocked(
                "CredentialOperationPolicyRejected",
                "CredentialPolicyNotSafe",
                "Credential/config operation requires future approval and redaction.");
        }

        return Sanitize(credentialCore(options, scope, policy, cancellationToken));
    }

    public LmaxReadOnlyCredentialConfigOperation AsClientOperation() => Access;

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
            "CredentialOperationCoreNotConfigured",
            "CredentialConfigCoreNotConfigured",
            "Credential/config operation binding is present, but a concrete secure source core must be supplied in a future approved phase.");

    private static LmaxRealReadOnlySecretAccessResult Sanitize(LmaxRealReadOnlySecretAccessResult result)
        => new(
            result.AccessAllowed,
            result.RealSecretMaterialLoaded,
            result.SensitiveMaterialReturned,
            result.SensitiveMaterialPrinted,
            result.SensitiveMaterialStored,
            LmaxReadOnlyExecutionOperationSafety.SanitizeText(result.SanitizedStatus) ?? "CredentialOperationAccessSanitized",
            LmaxReadOnlyExecutionOperationSafety.SanitizeText(result.SanitizedErrorCategory),
            LmaxReadOnlyExecutionOperationSafety.SanitizeText(result.SanitizedErrorMessage));

    private static LmaxRealReadOnlySecretAccessResult Blocked(string status, string? category, string? message)
        => new(
            AccessAllowed: false,
            RealSecretMaterialLoaded: false,
            SensitiveMaterialReturned: false,
            SensitiveMaterialPrinted: false,
            SensitiveMaterialStored: false,
            status,
            category,
            LmaxReadOnlyExecutionOperationSafety.SanitizeText(message));
}

public sealed class LmaxReadOnlyExecutionOperationBindingSet
{
    public LmaxReadOnlyExecutionOperationBindingSet(
        LmaxReadOnlySocketConnectOperationBinding socket,
        LmaxReadOnlyTlsHandshakeOperationBinding tls,
        LmaxReadOnlyFixSessionOperationBinding fix,
        LmaxReadOnlyMarketDataOperationBinding marketData,
        LmaxReadOnlyCredentialConfigOperationBinding credentialConfig)
    {
        Socket = socket ?? throw new ArgumentNullException(nameof(socket));
        Tls = tls ?? throw new ArgumentNullException(nameof(tls));
        Fix = fix ?? throw new ArgumentNullException(nameof(fix));
        MarketData = marketData ?? throw new ArgumentNullException(nameof(marketData));
        CredentialConfig = credentialConfig ?? throw new ArgumentNullException(nameof(credentialConfig));
    }

    public LmaxReadOnlySocketConnectOperationBinding Socket { get; }
    public LmaxReadOnlyTlsHandshakeOperationBinding Tls { get; }
    public LmaxReadOnlyFixSessionOperationBinding Fix { get; }
    public LmaxReadOnlyMarketDataOperationBinding MarketData { get; }
    public LmaxReadOnlyCredentialConfigOperationBinding CredentialConfig { get; }

    public LmaxReadOnlyProviderClientOperationSet CreateProviderClientOperationSet()
        => new(
            new LmaxRealReadOnlySocketConnectionClient(Socket.AsClientOperation()),
            new LmaxRealReadOnlyTlsHandshakeClient(Tls.AsClientOperation()),
            new LmaxRealReadOnlyFixFrameClient(Fix.AsClientOperation()),
            new LmaxRealReadOnlyMarketDataFrameClient(MarketData.AsClientOperation()),
            new LmaxRealReadOnlyCredentialConfigClient(CredentialConfig.AsClientOperation()));
}

public sealed record LmaxReadOnlyProviderClientOperationSet(
    LmaxRealReadOnlySocketConnectionClient SocketClient,
    LmaxRealReadOnlyTlsHandshakeClient TlsClient,
    LmaxRealReadOnlyFixFrameClient FixClient,
    LmaxRealReadOnlyMarketDataFrameClient MarketDataClient,
    LmaxRealReadOnlyCredentialConfigClient CredentialConfigClient);

public static class LmaxReadOnlyExecutionOperationCompleteness
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

    public static LmaxReadOnlyExecutionOperationCompletenessResult Validate()
    {
        var operationTypes = new[]
        {
            typeof(LmaxReadOnlySocketConnectOperationBinding),
            typeof(LmaxReadOnlyTlsHandshakeOperationBinding),
            typeof(LmaxReadOnlyFixSessionOperationBinding),
            typeof(LmaxReadOnlyMarketDataOperationBinding),
            typeof(LmaxReadOnlyCredentialConfigOperationBinding)
        };

        var issues = new List<string>();
        foreach (var type in operationTypes)
        {
            if (type.Namespace?.Contains(".Tests.", StringComparison.OrdinalIgnoreCase) == true ||
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

        _ = new LmaxReadOnlySocketConnectOperationBinding();
        _ = new LmaxReadOnlyTlsHandshakeOperationBinding();
        _ = new LmaxReadOnlyFixSessionOperationBinding();
        _ = new LmaxReadOnlyMarketDataOperationBinding();
        _ = new LmaxReadOnlyCredentialConfigOperationBinding();

        return new LmaxReadOnlyExecutionOperationCompletenessResult(
            SocketOperationImplemented: typeof(ILmaxReadOnlySocketConnectOperationBinding).IsAssignableFrom(typeof(LmaxReadOnlySocketConnectOperationBinding)),
            TlsOperationImplemented: typeof(ILmaxReadOnlyTlsHandshakeOperationBinding).IsAssignableFrom(typeof(LmaxReadOnlyTlsHandshakeOperationBinding)),
            FixOperationImplemented: typeof(ILmaxReadOnlyFixSessionOperationBinding).IsAssignableFrom(typeof(LmaxReadOnlyFixSessionOperationBinding)),
            MarketDataOperationImplemented: typeof(ILmaxReadOnlyMarketDataOperationBinding).IsAssignableFrom(typeof(LmaxReadOnlyMarketDataOperationBinding)),
            CredentialConfigOperationImplemented: typeof(ILmaxReadOnlyCredentialConfigOperationBinding).IsAssignableFrom(typeof(LmaxReadOnlyCredentialConfigOperationBinding)),
            PublicSurfaceReadOnly: issues.Count == 0,
            Issues: issues);
    }
}

public sealed record LmaxReadOnlyExecutionOperationCompletenessResult(
    bool SocketOperationImplemented,
    bool TlsOperationImplemented,
    bool FixOperationImplemented,
    bool MarketDataOperationImplemented,
    bool CredentialConfigOperationImplemented,
    bool PublicSurfaceReadOnly,
    IReadOnlyList<string> Issues)
{
    public bool Passed =>
        SocketOperationImplemented &&
        TlsOperationImplemented &&
        FixOperationImplemented &&
        MarketDataOperationImplemented &&
        CredentialConfigOperationImplemented &&
        PublicSurfaceReadOnly;
}

internal static class LmaxReadOnlyExecutionOperationSafety
{
    public static LmaxRealReadOnlyDependencyResult? ValidateScope(
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

    public static LmaxRealReadOnlyDependencyResult Sanitize(
        LmaxRealReadOnlyDependencyResult result,
        string fallbackCategory)
        => new(
            result.Status,
            SanitizeText(result.SanitizedStatus) ?? "ExecutionOperationStatusSanitized",
            SanitizeText(result.SanitizedErrorCategory) ?? fallbackCategory,
            SanitizeText(result.SanitizedErrorMessage));

    public static LmaxRealReadOnlyDependencyResult Blocked(string status, string category, string message)
        => new(
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            status,
            category,
            SanitizeText(message));

    public static string? SanitizeText(string? value)
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
