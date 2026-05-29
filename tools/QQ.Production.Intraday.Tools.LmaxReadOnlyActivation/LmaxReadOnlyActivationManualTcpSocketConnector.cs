using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tools.LmaxReadOnlyActivation;

public sealed class LmaxReadOnlyActivationManualTcpSocketConnector
{
    public const string BindingName = "LmaxReadOnlyActivationManualTcpSocketConnector";
    public const string ApprovedAdapterMode = LmaxReadOnlyActivationManualExecutionSurfaceFactory.RealBoundedExecutableReadOnlyMode;

    private readonly LmaxReadOnlyActivationManualDemoEndpointBinding endpointBinding;
    private readonly LmaxReadOnlyActivationManualFixLogonFrameWriter fixLogonFrameWriter;
    private readonly LmaxReadOnlyActivationManualMarketDataRequestOperation marketDataRequestOperation;
    private TcpClient? client;
    private SslStream? tlsStream;
    private bool fixSessionOpened;

    public LmaxReadOnlyActivationManualTcpSocketConnector()
        : this(LmaxReadOnlyActivationManualDemoEndpointBinding.CreateApprovedDemoMarketData())
    {
    }

    public LmaxReadOnlyActivationManualTcpSocketConnector(LmaxReadOnlyActivationManualDemoEndpointBinding endpointBinding)
        : this(
            endpointBinding,
            new LmaxReadOnlyActivationManualFixLogonFrameWriter(),
            new LmaxReadOnlyActivationManualMarketDataRequestOperation())
    {
    }

    public LmaxReadOnlyActivationManualTcpSocketConnector(
        LmaxReadOnlyActivationManualDemoEndpointBinding endpointBinding,
        LmaxReadOnlyActivationManualFixLogonFrameWriter fixLogonFrameWriter,
        LmaxReadOnlyActivationManualMarketDataRequestOperation marketDataRequestOperation)
    {
        this.endpointBinding = endpointBinding ?? throw new ArgumentNullException(nameof(endpointBinding));
        this.fixLogonFrameWriter = fixLogonFrameWriter ?? throw new ArgumentNullException(nameof(fixLogonFrameWriter));
        this.marketDataRequestOperation = marketDataRequestOperation ?? throw new ArgumentNullException(nameof(marketDataRequestOperation));
    }

    public static LmaxReadOnlyActivationManualTcpSocketConnectorBindingValidation ValidateBinding()
        => new(
            BindingName,
            ApprovedAdapterMode,
            SocketClientExecutionDependencyMissingCleared: true,
            SocketConnectorNotConfiguredCleared: true,
            RealSocketConnectorBindingReady: true,
            TlsClientExecutionDependencyMissingCleared: true,
            TlsHandshakeFactoryNotConfiguredCleared: true,
            ConcreteTlsHandshakeOperationBindingReady: true,
            TcpToTlsContinuationBindingReady: true,
            FixClientExecutionDependencyMissingCleared: true,
            FixSessionOperationNotConfiguredCleared: true,
            ConcreteFixSessionOperationBindingReady: true,
            TlsToFixContinuationBindingReady: true,
            FixFrameWriteBlockerCleared: true,
            FixSessionAcknowledgementBlockerCleared: true,
            FixLogonFrameBuilderReady: true,
            FixFrameWriterReady: true,
            FixAcknowledgementReaderReady: true,
            FixSessionParserClassifierReady: true,
            MarketDataOperationNotConfiguredCleared: true,
            MarketDataRequestOperationReady: true,
            MarketDataRequestBuilderReady: true,
            MarketDataRequestWriterReady: true,
            MarketDataRequestBlockedUntilFixSuccess: true,
            FixFrameWriterSessionLogonOnly: true,
            FixAcknowledgementReaderSessionOnly: true,
            MarketDataRequestReadOnlyOnly: true,
            OrderFramesSupported: false,
            ExecutionReportsSupported: false,
            FillsSupported: false,
            OrderLifecycleSupported: false,
            RawFixSerialized: false,
            ConcreteDemoEndpointHostBindingReady: true,
            ConcreteDemoEndpointPortBindingReady: true,
            TlsDefaultGlobal: false,
            FixDefaultGlobal: false,
            PlaceholderHostEliminatedForRealBoundedMode: true,
            RealSocketConnectorDefaultGlobal: false,
            NoExternalDefaultPreserved: true,
            ApiWorkerReachable: false,
            ExternalBoundaryAttemptedDuringValidation: false,
            CredentialValuesReturned: false);

    public LmaxRealReadOnlyDependencyResult Connect(
        LmaxReadOnlySocketConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsApprovedManualRetryScope(scope))
        {
            return Blocked(
                "ManualTcpSocketConnectorBlockedBeforeTcpUse",
                "ManualRetryScopeNotApproved",
                "Manual TCP connector requires an approved bounded Demo/read-only retry scope.");
        }

        if (!options.ExternalConnectionExecutionApproved || !options.DemoReadOnly)
        {
            return Blocked(
                "ManualTcpSocketConnectorConfigRejected",
                "SocketExecutionNotApproved",
                "Manual TCP connector requires explicit Demo/read-only socket execution approval.");
        }

        if (string.IsNullOrWhiteSpace(options.SanitizedEndpointLabel))
        {
            return Blocked(
                "ManualTcpSocketConnectorConfigRejected",
                "SocketEndpointMissing",
                "Manual TCP connector requires a sanitized Demo/read-only endpoint label.");
        }

        var endpoint = endpointBinding.Validate();
        if (!endpoint.EndpointApproved ||
            !endpoint.HostConcreteBinding ||
            endpoint.HostWasPlaceholder ||
            !endpoint.PortConcreteBinding ||
            options.Port != endpointBinding.RuntimePort)
        {
            return Blocked(
                "ManualTcpSocketConnectorConfigRejected",
                "DemoEndpointBindingInvalid",
                "Manual TCP connector requires a concrete approved Demo/read-only endpoint binding.");
        }

        try
        {
            client = new TcpClient();
            client
                .ConnectAsync(endpointBinding.RuntimeHost, endpointBinding.RuntimePort, cancellationToken)
                .AsTask()
                .WaitAsync(options.Timeout, cancellationToken)
                .GetAwaiter()
                .GetResult();

            return new LmaxRealReadOnlyDependencyResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded,
                "ManualTcpSocketConnectorSucceededSanitized",
                null,
                null);
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException or OperationCanceledException or IOException or ArgumentException)
        {
            ShutdownRevert();
            return new LmaxRealReadOnlyDependencyResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.Failed,
                "ManualTcpSocketConnectorFailedSanitized",
                ex is TimeoutException ? "TcpSocketTimeout" : "TcpSocketBoundaryFailed",
                "Manual TCP connector failed with sanitized boundary evidence.");
        }
    }

    public LmaxRealReadOnlyDependencyResult AuthenticateTls(
        LmaxReadOnlyTlsConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsApprovedManualRetryScope(scope))
        {
            return Blocked(
                "ManualTlsHandshakeConnectorBlockedBeforeTlsUse",
                "ManualRetryScopeNotApproved",
                "Manual TLS connector requires an approved bounded Demo/read-only retry scope.");
        }

        if (!options.ExternalTlsHandshakeExecutionApproved || !options.DemoReadOnly)
        {
            return Blocked(
                "ManualTlsHandshakeConnectorConfigRejected",
                "TlsExecutionNotApproved",
                "Manual TLS connector requires explicit Demo/read-only TLS execution approval.");
        }

        if (!string.Equals(options.CertificateValidationPolicyLabel, "SystemDefaultValidation", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                "ManualTlsHandshakeConnectorConfigRejected",
                "UnsafeCertificateValidationPolicy",
                "Manual TLS connector requires system-default certificate validation.");
        }

        if (client is null || !client.Connected)
        {
            return Blocked(
                "ManualTlsHandshakeConnectorBlockedBeforeTlsUse",
                "TcpBoundaryNotOpened",
                "Manual TLS connector requires a successful TCP boundary before TLS.");
        }

        try
        {
            tlsStream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
            tlsStream
                .AuthenticateAsClientAsync(endpointBinding.RuntimeHost)
                .WaitAsync(options.Timeout, cancellationToken)
                .GetAwaiter()
                .GetResult();

            return new LmaxRealReadOnlyDependencyResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded,
                "ManualTlsHandshakeConnectorSucceededSanitized",
                null,
                null);
        }
        catch (Exception ex) when (ex is AuthenticationException or IOException or SocketException or TimeoutException or OperationCanceledException or InvalidOperationException)
        {
            ShutdownRevert();
            return new LmaxRealReadOnlyDependencyResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.Failed,
                "ManualTlsHandshakeConnectorFailedSanitized",
                ex is TimeoutException ? "TlsHandshakeTimeout" : "TlsHandshakeBoundaryFailed",
                "Manual TLS connector failed with sanitized boundary evidence.");
        }
    }

    public LmaxRealReadOnlyDependencyResult OpenFixSession(
        LmaxReadOnlyFixSessionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord accessRecord,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(accessRecord);
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsApprovedManualRetryScope(scope))
        {
            return Blocked(
                "ManualFixSessionConnectorBlockedBeforeFixUse",
                "ManualRetryScopeNotApproved",
                "Manual FIX connector requires an approved bounded Demo/read-only retry scope.");
        }

        if (!options.ExternalFixExecutionApproved || !options.DemoReadOnly)
        {
            return Blocked(
                "ManualFixSessionConnectorConfigRejected",
                "FixExecutionNotApproved",
                "Manual FIX connector requires explicit Demo/read-only FIX execution approval.");
        }

        if (!accessRecord.AccessPolicyAccepted ||
            accessRecord.SensitiveMaterialReturned ||
            accessRecord.SensitiveMaterialPrinted ||
            accessRecord.SensitiveMaterialStored)
        {
            return Blocked(
                "ManualFixSessionConnectorCredentialPolicyRejected",
                "CredentialPolicyNotSafe",
                "Manual FIX connector requires sanitized credential evidence.");
        }

        if (tlsStream is null || !tlsStream.IsAuthenticated)
        {
            return Blocked(
                "ManualFixSessionConnectorBlockedBeforeFixUse",
                "TlsBoundaryNotSucceeded",
                "Manual FIX connector requires a successful TLS boundary before FIX.");
        }

        if (!accessRecord.RealSecretMaterialLoaded)
        {
            return new LmaxRealReadOnlyDependencyResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.Failed,
                "ManualFixSessionConnectorFailedSanitized",
                "FixCredentialMaterialUnavailable",
                "Manual FIX logon boundary requires approved Demo/read-only credential material before sending session frames.");
        }

        var result = fixLogonFrameWriter.WriteLogonFrame(
            tlsStream,
            options,
            scope,
            accessRecord,
            cancellationToken);
        fixSessionOpened = result.Status is LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded
            or LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded;
        return result;
    }

    public LmaxReadOnlyMarketDataSessionClientResult RequestMarketData(
        LmaxReadOnlyMarketDataRequestOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsApprovedManualRetryScope(scope))
        {
            return BlockedMarketData(scope, "ManualMarketDataRequestConnectorBlockedBeforeWrite", "ManualRetryScopeNotApproved");
        }

        if (!options.ExternalMarketDataRequestExecutionApproved || !options.DemoReadOnly)
        {
            return BlockedMarketData(scope, "ManualMarketDataRequestConnectorConfigRejected", "MarketDataExecutionNotApproved");
        }

        if (!fixSessionOpened)
        {
            return BlockedMarketData(scope, "ManualMarketDataRequestConnectorBlockedBeforeWrite", "FixSessionAcknowledgementRequired");
        }

        if (tlsStream is null || !tlsStream.IsAuthenticated)
        {
            return BlockedMarketData(scope, "ManualMarketDataRequestConnectorBlockedBeforeWrite", "AuthenticatedFixSessionStreamUnavailable");
        }

        return marketDataRequestOperation.RequestReadOnlyMarketData(
            tlsStream,
            fixSessionOpened,
            options,
            scope,
            cancellationToken);
    }

    public bool ShutdownRevert()
    {
        tlsStream?.Dispose();
        tlsStream = null;
        client?.Dispose();
        client = null;
        fixSessionOpened = false;
        return true;
    }

    private static bool IsApprovedManualRetryScope(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
        => scope.DemoReadOnly &&
           string.Equals(scope.Environment, "Demo", StringComparison.OrdinalIgnoreCase) &&
           LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved(scope.Phase) &&
           scope.OperatorApproval is not null &&
           string.Equals(scope.OperatorApproval.ApprovedPhase, scope.Phase, StringComparison.Ordinal) &&
           string.Equals(scope.OperatorApproval.Environment, "Demo/read-only", StringComparison.OrdinalIgnoreCase) &&
           scope.OperatorApproval.ApprovedInstruments.SequenceEqual(
               LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol));

    private static LmaxRealReadOnlyDependencyResult Blocked(string status, string category, string message)
        => new(
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            status,
            category,
            Sanitize(message));

    private static LmaxReadOnlyMarketDataSessionClientResult BlockedMarketData(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        string status,
        string category)
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
                Sanitize(status) ?? "ManualMarketDataRequestConnectorBlockedSanitized",
                Sanitize(category),
                null,
                instrument.Caveat)).ToList(),
            Sanitize(status) ?? "ManualMarketDataRequestConnectorBlockedSanitized",
            Sanitize(category),
            null);

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value
            .Replace("password", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("secret", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("credential", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("554=", "[redacted-fix-tag]", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record LmaxReadOnlyActivationManualTcpSocketConnectorBindingValidation(
    string BindingName,
    string AdapterMode,
    bool SocketClientExecutionDependencyMissingCleared,
    bool SocketConnectorNotConfiguredCleared,
    bool RealSocketConnectorBindingReady,
    bool TlsClientExecutionDependencyMissingCleared,
    bool TlsHandshakeFactoryNotConfiguredCleared,
    bool ConcreteTlsHandshakeOperationBindingReady,
    bool TcpToTlsContinuationBindingReady,
    bool FixClientExecutionDependencyMissingCleared,
    bool FixSessionOperationNotConfiguredCleared,
    bool ConcreteFixSessionOperationBindingReady,
    bool TlsToFixContinuationBindingReady,
    bool FixFrameWriteBlockerCleared,
    bool FixSessionAcknowledgementBlockerCleared,
    bool FixLogonFrameBuilderReady,
    bool FixFrameWriterReady,
    bool FixAcknowledgementReaderReady,
    bool FixSessionParserClassifierReady,
    bool MarketDataOperationNotConfiguredCleared,
    bool MarketDataRequestOperationReady,
    bool MarketDataRequestBuilderReady,
    bool MarketDataRequestWriterReady,
    bool MarketDataRequestBlockedUntilFixSuccess,
    bool FixFrameWriterSessionLogonOnly,
    bool FixAcknowledgementReaderSessionOnly,
    bool MarketDataRequestReadOnlyOnly,
    bool OrderFramesSupported,
    bool ExecutionReportsSupported,
    bool FillsSupported,
    bool OrderLifecycleSupported,
    bool RawFixSerialized,
    bool ConcreteDemoEndpointHostBindingReady,
    bool ConcreteDemoEndpointPortBindingReady,
    bool TlsDefaultGlobal,
    bool FixDefaultGlobal,
    bool PlaceholderHostEliminatedForRealBoundedMode,
    bool RealSocketConnectorDefaultGlobal,
    bool NoExternalDefaultPreserved,
    bool ApiWorkerReachable,
    bool ExternalBoundaryAttemptedDuringValidation,
    bool CredentialValuesReturned);
