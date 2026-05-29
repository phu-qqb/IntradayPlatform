namespace QQ.Production.Intraday.Infrastructure.Lmax;

public interface ILmaxRealReadOnlySocketBoundaryProvider
{
    LmaxRealReadOnlyDependencyResult OpenTcp(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);

    bool ShutdownRevert();
}

public interface ILmaxRealReadOnlyTlsBoundaryProvider
{
    LmaxRealReadOnlyDependencyResult OpenTls(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);
}

public interface ILmaxRealReadOnlyFixFrameBoundaryProvider
{
    LmaxRealReadOnlyDependencyResult OpenSessionLogon(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord accessRecord,
        CancellationToken cancellationToken = default);
}

public interface ILmaxRealReadOnlyMarketDataFrameBoundaryProvider
{
    LmaxReadOnlyMarketDataSessionClientResult RequestReadOnlyStatus(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);
}

public interface ILmaxRealReadOnlyCredentialConfigBoundaryProvider
{
    LmaxRealReadOnlySecretAccessResult AccessDemoReadOnlyConfig(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialAccessPolicy policy,
        CancellationToken cancellationToken = default);
}

public sealed class LmaxRealSocketProvider : ILmaxRealReadOnlyTcpConnector
{
    private readonly ILmaxRealReadOnlySocketBoundaryProvider boundaryProvider;

    public LmaxRealSocketProvider(ILmaxRealReadOnlySocketBoundaryProvider boundaryProvider)
    {
        this.boundaryProvider = boundaryProvider ?? throw new ArgumentNullException(nameof(boundaryProvider));
    }

    public LmaxRealReadOnlyDependencyResult Connect(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var blocked = Validate(scope, "SocketProviderBlockedBeforeTcpUse");
        if (blocked is not null)
        {
            return blocked;
        }

        return Sanitize(boundaryProvider.OpenTcp(scope, cancellationToken), "TcpBoundary");
    }

    public bool ShutdownRevert() => boundaryProvider.ShutdownRevert();

    private static LmaxRealReadOnlyDependencyResult? Validate(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        string status)
    {
        var issues = LmaxExecutableReadOnlyCredentialBoundary.ValidateScope(scope);
        if (issues.Count == 0)
        {
            return null;
        }

        return new LmaxRealReadOnlyDependencyResult(
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            status,
            "SafetyConstraintFailed",
            LmaxRealReadOnlyCredentialDependency.Sanitize(string.Join("; ", issues.Select(x => x.Code))));
    }

    internal static LmaxRealReadOnlyDependencyResult Sanitize(
        LmaxRealReadOnlyDependencyResult result,
        string fallbackCategory)
        => new(
            result.Status,
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedStatus) ?? "RealProviderBoundaryStatusSanitized",
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedErrorCategory) ?? fallbackCategory,
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedErrorMessage));
}

public sealed class LmaxRealTlsStreamProvider : ILmaxRealReadOnlyTlsAuthenticator
{
    private readonly ILmaxRealReadOnlyTlsBoundaryProvider boundaryProvider;

    public LmaxRealTlsStreamProvider(ILmaxRealReadOnlyTlsBoundaryProvider boundaryProvider)
    {
        this.boundaryProvider = boundaryProvider ?? throw new ArgumentNullException(nameof(boundaryProvider));
    }

    public LmaxRealReadOnlyDependencyResult Authenticate(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var issues = LmaxExecutableReadOnlyCredentialBoundary.ValidateScope(scope);
        if (issues.Count > 0)
        {
            return new LmaxRealReadOnlyDependencyResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
                "TlsProviderBlockedBeforeHandshakeUse",
                "SafetyConstraintFailed",
                LmaxRealReadOnlyCredentialDependency.Sanitize(string.Join("; ", issues.Select(x => x.Code))));
        }

        return LmaxRealSocketProvider.Sanitize(boundaryProvider.OpenTls(scope, cancellationToken), "TlsBoundary");
    }
}

public sealed class LmaxRealFixFrameProvider : ILmaxRealReadOnlyFixSessionDriver
{
    private readonly ILmaxRealReadOnlyFixFrameBoundaryProvider boundaryProvider;

    public LmaxRealFixFrameProvider(ILmaxRealReadOnlyFixFrameBoundaryProvider boundaryProvider)
    {
        this.boundaryProvider = boundaryProvider ?? throw new ArgumentNullException(nameof(boundaryProvider));
    }

    public LmaxRealReadOnlyDependencyResult OpenLogon(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord secretRecord,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var issues = LmaxExecutableReadOnlyCredentialBoundary.ValidateScope(scope);
        if (issues.Count > 0)
        {
            return new LmaxRealReadOnlyDependencyResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
                "FixProviderBlockedBeforeFrameUse",
                "SafetyConstraintFailed",
                LmaxRealReadOnlyCredentialDependency.Sanitize(string.Join("; ", issues.Select(x => x.Code))));
        }

        if (!secretRecord.AccessPolicyAccepted ||
            secretRecord.SensitiveMaterialReturned ||
            secretRecord.SensitiveMaterialPrinted ||
            secretRecord.SensitiveMaterialStored)
        {
            return new LmaxRealReadOnlyDependencyResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
                "FixProviderBlockedByAccessPolicy",
                "CredentialPolicyNotSafe",
                "FIX provider requires sanitized access evidence.");
        }

        return LmaxRealSocketProvider.Sanitize(
            boundaryProvider.OpenSessionLogon(scope, secretRecord, cancellationToken),
            "FixLogonBoundary");
    }
}

public sealed class LmaxRealMarketDataFrameProvider : ILmaxRealReadOnlyMarketDataDriver
{
    private readonly ILmaxRealReadOnlyMarketDataFrameBoundaryProvider boundaryProvider;

    public LmaxRealMarketDataFrameProvider(ILmaxRealReadOnlyMarketDataFrameBoundaryProvider boundaryProvider)
    {
        this.boundaryProvider = boundaryProvider ?? throw new ArgumentNullException(nameof(boundaryProvider));
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
                "MarketDataProviderBlockedBeforeRequestUse",
                "SafetyConstraintFailed",
                LmaxRealReadOnlyCredentialDependency.Sanitize(string.Join("; ", issues.Select(x => x.Code))));
        }

        var result = boundaryProvider.RequestReadOnlyStatus(scope, cancellationToken);
        return new LmaxReadOnlyMarketDataSessionClientResult(
            SanitizeStatuses(scope, result.InstrumentStatuses),
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedStatus) ?? "MarketDataProviderStatusSanitized",
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
                    "MarketDataProviderInstrumentStatusMissingSanitized",
                    "InstrumentStatusMissing",
                    "Approved instrument did not return sanitized provider evidence.",
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
                LmaxRealReadOnlyCredentialDependency.Sanitize(status.SanitizedStatus) ?? "MarketDataProviderInstrumentStatusSanitized",
                LmaxRealReadOnlyCredentialDependency.Sanitize(status.SanitizedErrorCategory),
                LmaxRealReadOnlyCredentialDependency.Sanitize(status.SanitizedErrorMessage),
                instrument.Caveat);
        }).ToList();
    }
}

public sealed class LmaxRealCredentialConfigProvider : ILmaxRealReadOnlySecretProvider
{
    private readonly ILmaxRealReadOnlyCredentialConfigBoundaryProvider boundaryProvider;

    public LmaxRealCredentialConfigProvider(ILmaxRealReadOnlyCredentialConfigBoundaryProvider boundaryProvider)
    {
        this.boundaryProvider = boundaryProvider ?? throw new ArgumentNullException(nameof(boundaryProvider));
    }

    public LmaxRealReadOnlySecretAccessResult AccessDemoReadOnly(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialAccessPolicy policy,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var issues = LmaxExecutableReadOnlyCredentialBoundary.ValidateScope(scope);
        if (issues.Count > 0)
        {
            return new LmaxRealReadOnlySecretAccessResult(
                AccessAllowed: false,
                RealSecretMaterialLoaded: false,
                SensitiveMaterialReturned: false,
                SensitiveMaterialPrinted: false,
                SensitiveMaterialStored: false,
                "CredentialConfigProviderBlockedBeforeSecretUse",
                "SafetyConstraintFailed",
                LmaxRealReadOnlyCredentialDependency.Sanitize(string.Join("; ", issues.Select(x => x.Code))));
        }

        if (!policy.FutureApprovedRuntimeAttemptRequired ||
            !policy.RedactSensitiveFields ||
            !string.Equals(policy.Environment, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return new LmaxRealReadOnlySecretAccessResult(
                AccessAllowed: false,
                RealSecretMaterialLoaded: false,
                SensitiveMaterialReturned: false,
                SensitiveMaterialPrinted: false,
                SensitiveMaterialStored: false,
                "CredentialConfigProviderPolicyRejected",
                "CredentialPolicyNotSafe",
                "Provider policy must stay Demo/read-only, explicitly approved, and redacted.");
        }

        var access = boundaryProvider.AccessDemoReadOnlyConfig(scope, policy, cancellationToken);
        return new LmaxRealReadOnlySecretAccessResult(
            access.AccessAllowed,
            access.RealSecretMaterialLoaded,
            access.SensitiveMaterialReturned,
            access.SensitiveMaterialPrinted,
            access.SensitiveMaterialStored,
            LmaxRealReadOnlyCredentialDependency.Sanitize(access.SanitizedStatus) ?? "CredentialConfigProviderAccessSanitized",
            LmaxRealReadOnlyCredentialDependency.Sanitize(access.SanitizedErrorCategory),
            LmaxRealReadOnlyCredentialDependency.Sanitize(access.SanitizedErrorMessage));
    }
}

public sealed class LmaxRealReadOnlyDependencyProviderSet
{
    public LmaxRealReadOnlyDependencyProviderSet(
        LmaxRealSocketProvider socketProvider,
        LmaxRealTlsStreamProvider tlsProvider,
        LmaxRealFixFrameProvider fixProvider,
        LmaxRealMarketDataFrameProvider marketDataProvider,
        LmaxRealCredentialConfigProvider credentialConfigProvider)
    {
        SocketProvider = socketProvider ?? throw new ArgumentNullException(nameof(socketProvider));
        TlsProvider = tlsProvider ?? throw new ArgumentNullException(nameof(tlsProvider));
        FixProvider = fixProvider ?? throw new ArgumentNullException(nameof(fixProvider));
        MarketDataProvider = marketDataProvider ?? throw new ArgumentNullException(nameof(marketDataProvider));
        CredentialConfigProvider = credentialConfigProvider ?? throw new ArgumentNullException(nameof(credentialConfigProvider));
    }

    public LmaxRealSocketProvider SocketProvider { get; }

    public LmaxRealTlsStreamProvider TlsProvider { get; }

    public LmaxRealFixFrameProvider FixProvider { get; }

    public LmaxRealMarketDataFrameProvider MarketDataProvider { get; }

    public LmaxRealCredentialConfigProvider CredentialConfigProvider { get; }

    public LmaxRealReadOnlyLowLevelDependencySet CreateLowLevelDependencySet()
        => new LmaxRealReadOnlyLowLevelDependencyFactory(
            SocketProvider,
            TlsProvider,
            FixProvider,
            MarketDataProvider,
            CredentialConfigProvider).Create();
}

public sealed class LmaxRealReadOnlyDependencyProviderFactory
{
    private readonly ILmaxRealReadOnlySocketBoundaryProvider socketBoundaryProvider;
    private readonly ILmaxRealReadOnlyTlsBoundaryProvider tlsBoundaryProvider;
    private readonly ILmaxRealReadOnlyFixFrameBoundaryProvider fixBoundaryProvider;
    private readonly ILmaxRealReadOnlyMarketDataFrameBoundaryProvider marketDataBoundaryProvider;
    private readonly ILmaxRealReadOnlyCredentialConfigBoundaryProvider credentialConfigBoundaryProvider;

    public LmaxRealReadOnlyDependencyProviderFactory(
        ILmaxRealReadOnlySocketBoundaryProvider socketBoundaryProvider,
        ILmaxRealReadOnlyTlsBoundaryProvider tlsBoundaryProvider,
        ILmaxRealReadOnlyFixFrameBoundaryProvider fixBoundaryProvider,
        ILmaxRealReadOnlyMarketDataFrameBoundaryProvider marketDataBoundaryProvider,
        ILmaxRealReadOnlyCredentialConfigBoundaryProvider credentialConfigBoundaryProvider)
    {
        this.socketBoundaryProvider = socketBoundaryProvider ?? throw new ArgumentNullException(nameof(socketBoundaryProvider));
        this.tlsBoundaryProvider = tlsBoundaryProvider ?? throw new ArgumentNullException(nameof(tlsBoundaryProvider));
        this.fixBoundaryProvider = fixBoundaryProvider ?? throw new ArgumentNullException(nameof(fixBoundaryProvider));
        this.marketDataBoundaryProvider = marketDataBoundaryProvider ?? throw new ArgumentNullException(nameof(marketDataBoundaryProvider));
        this.credentialConfigBoundaryProvider = credentialConfigBoundaryProvider ?? throw new ArgumentNullException(nameof(credentialConfigBoundaryProvider));
    }

    public LmaxRealReadOnlyDependencyProviderSet Create()
        => new(
            new LmaxRealSocketProvider(socketBoundaryProvider),
            new LmaxRealTlsStreamProvider(tlsBoundaryProvider),
            new LmaxRealFixFrameProvider(fixBoundaryProvider),
            new LmaxRealMarketDataFrameProvider(marketDataBoundaryProvider),
            new LmaxRealCredentialConfigProvider(credentialConfigBoundaryProvider));
}
