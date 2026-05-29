namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxRealReadOnlyDependencyResult(
    LmaxTemporaryReadOnlySessionBoundaryStatus Status,
    string SanitizedStatus,
    string? SanitizedErrorCategory = null,
    string? SanitizedErrorMessage = null)
{
    public bool Succeeded =>
        Status is LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded
            or LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded;
}

public sealed record LmaxRealReadOnlySecretAccessResult(
    bool AccessAllowed,
    bool RealSecretMaterialLoaded,
    bool SensitiveMaterialReturned,
    bool SensitiveMaterialPrinted,
    bool SensitiveMaterialStored,
    string SanitizedStatus,
    string? SanitizedErrorCategory = null,
    string? SanitizedErrorMessage = null);

public interface ILmaxRealReadOnlyTcpConnector
{
    LmaxRealReadOnlyDependencyResult Connect(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);

    bool ShutdownRevert();
}

public interface ILmaxRealReadOnlyTlsAuthenticator
{
    LmaxRealReadOnlyDependencyResult Authenticate(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);
}

public interface ILmaxRealReadOnlyFixSessionDriver
{
    LmaxRealReadOnlyDependencyResult OpenLogon(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord secretRecord,
        CancellationToken cancellationToken = default);
}

public interface ILmaxRealReadOnlyMarketDataDriver
{
    LmaxReadOnlyMarketDataSessionClientResult ReadMarketData(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);
}

public interface ILmaxRealReadOnlySecretProvider
{
    LmaxRealReadOnlySecretAccessResult AccessDemoReadOnly(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialAccessPolicy policy,
        CancellationToken cancellationToken = default);
}

public sealed class LmaxRealReadOnlyCredentialDependency : ILmaxReadOnlyCredentialBoundary
{
    private readonly ILmaxRealReadOnlySecretProvider secretProvider;

    public LmaxRealReadOnlyCredentialDependency(ILmaxRealReadOnlySecretProvider secretProvider)
    {
        this.secretProvider = secretProvider ?? throw new ArgumentNullException(nameof(secretProvider));
    }

    public LmaxReadOnlyCredentialSanitizationRecord ValidatePolicy(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialAccessPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(policy);

        var issues = LmaxExecutableReadOnlyCredentialBoundary.ValidateScope(scope);
        if (issues.Count > 0)
        {
            return Blocked("SafetyConstraintFailed", string.Join("; ", issues.Select(x => x.Code)));
        }

        if (!policy.FutureApprovedRuntimeAttemptRequired ||
            !policy.RedactSensitiveFields ||
            !string.Equals(policy.Environment, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                "CredentialPolicyNotSafe",
                "Credential access policy must stay Demo/read-only, explicitly approved, and redacted.");
        }

        if (!policy.RealSecretMaterialAllowedNow)
        {
            return new LmaxReadOnlyCredentialSanitizationRecord(
                AccessPolicyAccepted: true,
                RealSecretMaterialLoaded: false,
                SensitiveMaterialReturned: false,
                SensitiveMaterialPrinted: false,
                SensitiveMaterialStored: false,
                "RealCredentialDependencyAcceptedNoSecretMaterialLoaded");
        }

        var access = secretProvider.AccessDemoReadOnly(scope, policy);
        return new LmaxReadOnlyCredentialSanitizationRecord(
            access.AccessAllowed,
            access.RealSecretMaterialLoaded,
            access.SensitiveMaterialReturned,
            access.SensitiveMaterialPrinted,
            access.SensitiveMaterialStored,
            Sanitize(access.SanitizedStatus) ?? "RealCredentialDependencyAccessSanitized",
            Sanitize(access.SanitizedErrorCategory),
            Sanitize(access.SanitizedErrorMessage));
    }

    private static LmaxReadOnlyCredentialSanitizationRecord Blocked(string category, string message)
        => new(
            AccessPolicyAccepted: false,
            RealSecretMaterialLoaded: false,
            SensitiveMaterialReturned: false,
            SensitiveMaterialPrinted: false,
            SensitiveMaterialStored: false,
            "RealCredentialDependencyBlockedBeforeSecretAccess",
            category,
            Sanitize(message));

    internal static string? Sanitize(string? value)
        => LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(value);
}

public sealed class LmaxRealReadOnlySocketDependency : ILmaxReadOnlySocketBoundaryTransport
{
    private readonly ILmaxRealReadOnlyTcpConnector tcpConnector;
    private readonly ILmaxRealReadOnlyTlsAuthenticator tlsAuthenticator;
    private bool tcpOpened;

    public LmaxRealReadOnlySocketDependency(
        ILmaxRealReadOnlyTcpConnector tcpConnector,
        ILmaxRealReadOnlyTlsAuthenticator tlsAuthenticator)
    {
        this.tcpConnector = tcpConnector ?? throw new ArgumentNullException(nameof(tcpConnector));
        this.tlsAuthenticator = tlsAuthenticator ?? throw new ArgumentNullException(nameof(tlsAuthenticator));
    }

    public LmaxReadOnlyBoundaryStepResult OpenTcp(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var blocked = Validate(scope);
        if (blocked is not null)
        {
            return blocked;
        }

        var result = tcpConnector.Connect(scope, cancellationToken);
        tcpOpened = result.Succeeded;
        return ToBoundary(result, "TcpBoundary");
    }

    public LmaxReadOnlyBoundaryStepResult OpenTls(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var blocked = Validate(scope);
        if (blocked is not null)
        {
            return blocked;
        }

        if (!tcpOpened)
        {
            return NotAttempted("TcpBoundaryNotOpened", "TCP boundary must succeed before TLS boundary.");
        }

        var result = tlsAuthenticator.Authenticate(scope, cancellationToken);
        return ToBoundary(result, "TlsBoundary");
    }

    public bool ShutdownRevert()
    {
        tcpOpened = false;
        return tcpConnector.ShutdownRevert();
    }

    private static LmaxReadOnlyBoundaryStepResult? Validate(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
    {
        var issues = LmaxExecutableReadOnlyCredentialBoundary.ValidateScope(scope);
        if (issues.Count == 0)
        {
            return null;
        }

        return NotAttempted("SafetyConstraintFailed", string.Join("; ", issues.Select(x => x.Code)));
    }

    private static LmaxReadOnlyBoundaryStepResult ToBoundary(
        LmaxRealReadOnlyDependencyResult result,
        string fallbackCategory)
        => new(
            result.Status,
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedStatus) ?? "ReadOnlyDependencyStatusSanitized",
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedErrorCategory) ?? fallbackCategory,
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedErrorMessage));

    private static LmaxReadOnlyBoundaryStepResult NotAttempted(string category, string message)
        => new(
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            "NotAttempted",
            category,
            LmaxRealReadOnlyCredentialDependency.Sanitize(message));
}

public sealed class LmaxRealReadOnlyFixSessionDependency : ILmaxReadOnlyFixFrameBoundary
{
    private readonly ILmaxRealReadOnlyFixSessionDriver fixSessionDriver;

    public LmaxRealReadOnlyFixSessionDependency(ILmaxRealReadOnlyFixSessionDriver fixSessionDriver)
    {
        this.fixSessionDriver = fixSessionDriver ?? throw new ArgumentNullException(nameof(fixSessionDriver));
    }

    public LmaxReadOnlyBoundaryStepResult OpenLogon(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord secretRecord,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var issues = LmaxExecutableReadOnlyCredentialBoundary.ValidateScope(scope);
        if (issues.Count > 0)
        {
            return new LmaxReadOnlyBoundaryStepResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
                "ReadOnlyFixDependencyBlockedBeforeFrameUse",
                "SafetyConstraintFailed",
                LmaxRealReadOnlyCredentialDependency.Sanitize(string.Join("; ", issues.Select(x => x.Code))));
        }

        if (!secretRecord.AccessPolicyAccepted || secretRecord.SensitiveMaterialReturned || secretRecord.SensitiveMaterialPrinted || secretRecord.SensitiveMaterialStored)
        {
            return new LmaxReadOnlyBoundaryStepResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
                "ReadOnlyFixDependencyBlockedBySecretPolicy",
                "CredentialPolicyNotSafe",
                "FIX session dependency requires sanitized secret-access evidence.");
        }

        var result = fixSessionDriver.OpenLogon(scope, secretRecord, cancellationToken);
        return new LmaxReadOnlyBoundaryStepResult(
            result.Status,
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedStatus) ?? "ReadOnlyFixDependencyStatusSanitized",
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedErrorCategory) ?? "FixLogonBoundary",
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedErrorMessage));
    }
}

public sealed class LmaxRealReadOnlyMarketDataDependency : ILmaxReadOnlyMarketDataFrameCodec
{
    private readonly ILmaxRealReadOnlyMarketDataDriver marketDataDriver;

    public LmaxRealReadOnlyMarketDataDependency(ILmaxRealReadOnlyMarketDataDriver marketDataDriver)
    {
        this.marketDataDriver = marketDataDriver ?? throw new ArgumentNullException(nameof(marketDataDriver));
    }

    public LmaxReadOnlyMarketDataSessionClientResult ReadMarketData(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var issues = LmaxExecutableReadOnlyCredentialBoundary.ValidateScope(scope);
        if (issues.Count > 0)
        {
            return new LmaxReadOnlyMarketDataSessionClientResult(
                [],
                "ReadOnlyMarketDataDependencyBlockedBeforeFrameUse",
                "SafetyConstraintFailed",
                LmaxRealReadOnlyCredentialDependency.Sanitize(string.Join("; ", issues.Select(x => x.Code))));
        }

        var result = marketDataDriver.ReadMarketData(scope, cancellationToken);
        return new LmaxReadOnlyMarketDataSessionClientResult(
            SanitizeStatuses(scope, result.InstrumentStatuses),
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedStatus) ?? "ReadOnlyMarketDataDependencyStatusSanitized",
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedErrorCategory),
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedErrorMessage))
        {
            MarketDataRequestWriteAttempted = result.MarketDataRequestWriteAttempted,
            MarketDataRequestWriteSucceeded = result.MarketDataRequestWriteSucceeded,
            MarketDataRequestResponseReadAttempted = result.MarketDataRequestResponseReadAttempted,
            MarketDataRequestReachedBoundedResponseClassification = result.MarketDataRequestReachedBoundedResponseClassification,
            MarketDataRejectSanitizedSubcategory = LmaxRealReadOnlyCredentialDependency.Sanitize(result.MarketDataRejectSanitizedSubcategory),
            SessionRejectSanitizedSubcategory = LmaxRealReadOnlyCredentialDependency.Sanitize(result.SessionRejectSanitizedSubcategory),
            RejectReasonExtractionSource = LmaxRealReadOnlyCredentialDependency.Sanitize(result.RejectReasonExtractionSource),
            SessionRejectRefTagIdSanitizedCategory = LmaxRealReadOnlyCredentialDependency.Sanitize(result.SessionRejectRefTagIdSanitizedCategory),
            SessionRejectReasonSanitizedCategory = LmaxRealReadOnlyCredentialDependency.Sanitize(result.SessionRejectReasonSanitizedCategory),
            SessionRejectRefMsgTypeSanitizedCategory = LmaxRealReadOnlyCredentialDependency.Sanitize(result.SessionRejectRefMsgTypeSanitizedCategory),
            MarketDataEntriesObserved = result.MarketDataEntriesObserved,
            MarketDataSanitizedEntryCount = result.MarketDataSanitizedEntryCount,
            MarketDataEntriesEvidenceCategory = LmaxRealReadOnlyCredentialDependency.Sanitize(result.MarketDataEntriesEvidenceCategory),
            MarketDataEntriesReportingSource = LmaxRealReadOnlyCredentialDependency.Sanitize(result.MarketDataEntriesReportingSource),
            MarketDataEntriesNotAvailableReason = LmaxRealReadOnlyCredentialDependency.Sanitize(result.MarketDataEntriesNotAvailableReason),
            LogoutObserved = result.LogoutObserved,
            LogoutSourceCategory = LmaxRealReadOnlyCredentialDependency.Sanitize(result.LogoutSourceCategory),
            LogoutReasonSanitizedCategory = LmaxRealReadOnlyCredentialDependency.Sanitize(result.LogoutReasonSanitizedCategory),
            LogoutTextPresentSanitized = result.LogoutTextPresentSanitized,
            LogoutAfterInstrument = LmaxRealReadOnlyCredentialDependency.Sanitize(result.LogoutAfterInstrument),
            LogoutAfterSecurityIdSanitized = LmaxRealReadOnlyCredentialDependency.Sanitize(result.LogoutAfterSecurityIdSanitized),
            LogoutTimingCategory = LmaxRealReadOnlyCredentialDependency.Sanitize(result.LogoutTimingCategory),
            LogoutReasonExtractionSource = LmaxRealReadOnlyCredentialDependency.Sanitize(result.LogoutReasonExtractionSource)
        };
    }

    private static IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> SanitizeStatuses(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> statuses)
    {
        var bySymbol = statuses.ToDictionary(x => x.Symbol, StringComparer.OrdinalIgnoreCase);
        return scope.Instruments.Select(instrument =>
        {
            if (!bySymbol.TryGetValue(instrument.Symbol, out var status))
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
                    "RealReadOnlyMarketDataDependencyStatusMissingSanitized",
                    "InstrumentStatusMissing",
                    "Approved instrument did not return sanitized market-data dependency evidence.",
                    instrument.Caveat);
            }

            return new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                instrument.Symbol,
                instrument.SecurityId,
                instrument.SecurityIdSource,
                status.MarketDataBoundary,
                status.MarketDataSnapshotCount,
                status.MarketDataRequestRejectCount,
                status.BusinessMessageRejectCount,
                status.SessionRejectCount,
                LmaxRealReadOnlyCredentialDependency.Sanitize(status.SanitizedStatus) ?? "ReadOnlyInstrumentStatusSanitized",
                LmaxRealReadOnlyCredentialDependency.Sanitize(status.SanitizedErrorCategory),
                LmaxRealReadOnlyCredentialDependency.Sanitize(status.SanitizedErrorMessage),
                instrument.Caveat);
        }).ToList();
    }
}

public sealed class LmaxRealReadOnlyLowLevelDependencySet
{
    public LmaxRealReadOnlyLowLevelDependencySet(
        LmaxRealReadOnlySocketDependency socketDependency,
        LmaxRealReadOnlyFixSessionDependency fixSessionDependency,
        LmaxRealReadOnlyMarketDataDependency marketDataDependency,
        LmaxRealReadOnlyCredentialDependency secretDependency)
    {
        SocketDependency = socketDependency ?? throw new ArgumentNullException(nameof(socketDependency));
        FixSessionDependency = fixSessionDependency ?? throw new ArgumentNullException(nameof(fixSessionDependency));
        MarketDataDependency = marketDataDependency ?? throw new ArgumentNullException(nameof(marketDataDependency));
        SecretDependency = secretDependency ?? throw new ArgumentNullException(nameof(secretDependency));
    }

    public LmaxRealReadOnlySocketDependency SocketDependency { get; }

    public LmaxRealReadOnlyFixSessionDependency FixSessionDependency { get; }

    public LmaxRealReadOnlyMarketDataDependency MarketDataDependency { get; }

    public LmaxRealReadOnlyCredentialDependency SecretDependency { get; }

    public LmaxExecutableReadOnlyMarketDataSessionClient CreateSessionClient(
        LmaxReadOnlyCredentialAccessPolicy? policy = null)
        => new LmaxExecutableReadOnlySessionStackFactory(
            SocketDependency,
            FixSessionDependency,
            MarketDataDependency,
            SecretDependency,
            policy).CreateSessionClient();
}

public sealed class LmaxRealReadOnlyLowLevelDependencyFactory
{
    private readonly ILmaxRealReadOnlyTcpConnector tcpConnector;
    private readonly ILmaxRealReadOnlyTlsAuthenticator tlsAuthenticator;
    private readonly ILmaxRealReadOnlyFixSessionDriver fixSessionDriver;
    private readonly ILmaxRealReadOnlyMarketDataDriver marketDataDriver;
    private readonly ILmaxRealReadOnlySecretProvider secretProvider;

    public LmaxRealReadOnlyLowLevelDependencyFactory(
        ILmaxRealReadOnlyTcpConnector tcpConnector,
        ILmaxRealReadOnlyTlsAuthenticator tlsAuthenticator,
        ILmaxRealReadOnlyFixSessionDriver fixSessionDriver,
        ILmaxRealReadOnlyMarketDataDriver marketDataDriver,
        ILmaxRealReadOnlySecretProvider secretProvider)
    {
        this.tcpConnector = tcpConnector ?? throw new ArgumentNullException(nameof(tcpConnector));
        this.tlsAuthenticator = tlsAuthenticator ?? throw new ArgumentNullException(nameof(tlsAuthenticator));
        this.fixSessionDriver = fixSessionDriver ?? throw new ArgumentNullException(nameof(fixSessionDriver));
        this.marketDataDriver = marketDataDriver ?? throw new ArgumentNullException(nameof(marketDataDriver));
        this.secretProvider = secretProvider ?? throw new ArgumentNullException(nameof(secretProvider));
    }

    public LmaxRealReadOnlyLowLevelDependencySet Create()
        => new(
            new LmaxRealReadOnlySocketDependency(tcpConnector, tlsAuthenticator),
            new LmaxRealReadOnlyFixSessionDependency(fixSessionDriver),
            new LmaxRealReadOnlyMarketDataDependency(marketDataDriver),
            new LmaxRealReadOnlyCredentialDependency(secretProvider));
}
