using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using QQ.Production.Intraday.Tools.LmaxReadOnlyActivation;

namespace QQ.Production.Intraday.Infrastructure.Lmax.MarketDataOnly;

public sealed record LmaxMarketDataOnlyGuardedWriteEvent(
    string MsgType,
    bool Allowed,
    string Reason,
    int ByteCount,
    DateTimeOffset RecordedAtUtc);

public sealed class LmaxMarketDataOnlyGuardedWriteStream : Stream
{
    private readonly Stream inner;
    private readonly List<LmaxMarketDataOnlyGuardedWriteEvent> events = [];
    private readonly LmaxMarketDataOnlyFixFrameBuffer outboundFrames = new();
    public LmaxMarketDataOnlyGuardedWriteStream(Stream inner) => this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    public int ForbiddenOutboundCount { get; private set; }
    public IReadOnlyList<LmaxMarketDataOnlyGuardedWriteEvent> Events => events;
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.CanSeek ? inner.Length : 0;
    public override long Position { get => inner.CanSeek ? inner.Position : 0; set => throw new NotSupportedException(); }
    public override void Flush() => inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => WriteGuarded(buffer.AsSpan(offset, count));
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        foreach (var frame in Inspect(buffer.Span)) await inner.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
    }
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}
    private void WriteGuarded(ReadOnlySpan<byte> bytes){foreach(var frame in Inspect(bytes))inner.Write(frame);}
    private IReadOnlyList<byte[]> Inspect(ReadOnlySpan<byte> bytes)
    {
        var extracted=outboundFrames.Append(bytes, DateTimeOffset.UtcNow, 0);
        if(extracted.Malformed) Fail("(malformed)", extracted.MalformedReason ?? "malformed_fix_frame", bytes.Length);
        var allowedFrames=new List<byte[]>();
        foreach(var frame in extracted.Frames)
        {
            var result=LmaxMarketDataOnlyOutboundFixMessageGuard.InspectOutboundMessage(frame.RawFixMessage);
            events.Add(new LmaxMarketDataOnlyGuardedWriteEvent(result.MsgType,result.Allowed,result.Reason,frame.FrameBytes.Length,DateTimeOffset.UtcNow));
            if(!result.Allowed)
            {
                ForbiddenOutboundCount++;
                throw new InvalidOperationException($"market_data_only_forbidden_outbound_fix_msg_type:{result.MsgType}:{result.Reason}");
            }
            allowedFrames.Add(frame.FrameBytes);
        }
        return allowedFrames;
    }
    private void Fail(string msgType,string reason,int byteCount)
    {
        events.Add(new LmaxMarketDataOnlyGuardedWriteEvent(msgType,false,reason,byteCount,DateTimeOffset.UtcNow));
        ForbiddenOutboundCount++;
        throw new InvalidOperationException($"market_data_only_forbidden_outbound_fix_msg_type:{msgType}:{reason}");
    }
}

public sealed class LmaxMarketDataOnlyManualSessionConnector
{
    private readonly LmaxReadOnlyActivationManualDemoEndpointBinding endpointBinding;
    private readonly LmaxReadOnlyActivationManualFixLogonFrameWriter fixLogonFrameWriter;
    private readonly LmaxReadOnlyActivationManualMarketDataRequestOperation marketDataRequestOperation;
    private TcpClient? client;
    private Stream? tlsStream;
    private LmaxMarketDataOnlyGuardedWriteStream? guardedFixStream;
    private bool fixSessionOpened;

    public LmaxMarketDataOnlyManualSessionConnector()
        : this(
            LmaxReadOnlyActivationManualDemoEndpointBinding.CreateApprovedDemoMarketData(),
            new LmaxReadOnlyActivationManualFixLogonFrameWriter(),
            new LmaxReadOnlyActivationManualMarketDataRequestOperation())
    {
    }

    public LmaxMarketDataOnlyManualSessionConnector(
        LmaxReadOnlyActivationManualDemoEndpointBinding endpointBinding,
        LmaxReadOnlyActivationManualFixLogonFrameWriter fixLogonFrameWriter,
        LmaxReadOnlyActivationManualMarketDataRequestOperation marketDataRequestOperation)
    {
        this.endpointBinding = endpointBinding ?? throw new ArgumentNullException(nameof(endpointBinding));
        this.fixLogonFrameWriter = fixLogonFrameWriter ?? throw new ArgumentNullException(nameof(fixLogonFrameWriter));
        this.marketDataRequestOperation = marketDataRequestOperation ?? throw new ArgumentNullException(nameof(marketDataRequestOperation));
    }

    public IReadOnlyList<LmaxMarketDataOnlyGuardedWriteEvent> GuardedWriteEvents => guardedFixStream?.Events ?? [];

    public int ForbiddenOutboundCount => guardedFixStream?.ForbiddenOutboundCount ?? 0;

    public LmaxRealReadOnlyDependencyResult Connect(
        LmaxReadOnlySocketConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsApprovedMarketDataOnlyScope(scope))
        {
            return Blocked("MarketDataOnlyTcpConnectorBlockedBeforeTcpUse", "ScopeNotApproved");
        }

        if (!options.ExternalConnectionExecutionApproved || !options.DemoReadOnly)
        {
            return Blocked("MarketDataOnlyTcpConnectorConfigRejected", "SocketExecutionNotApproved");
        }

        var endpoint = endpointBinding.Validate();
        if (!endpoint.EndpointApproved || options.Port != endpointBinding.RuntimePort)
        {
            return Blocked("MarketDataOnlyTcpConnectorConfigRejected", "DemoEndpointBindingInvalid");
        }

        try
        {
            client = new TcpClient();
            client.ConnectAsync(endpointBinding.RuntimeHost, endpointBinding.RuntimePort, cancellationToken)
                .AsTask()
                .WaitAsync(options.Timeout, cancellationToken)
                .GetAwaiter()
                .GetResult();

            return Succeeded("MarketDataOnlyTcpConnectorSucceededSanitized");
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException or OperationCanceledException or IOException or ArgumentException)
        {
            ShutdownRevert();
            return Failed("MarketDataOnlyTcpConnectorFailedSanitized", ex is TimeoutException ? "TcpSocketTimeout" : "TcpSocketBoundaryFailed");
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

        if (!IsApprovedMarketDataOnlyScope(scope))
        {
            return Blocked("MarketDataOnlyTlsConnectorBlockedBeforeTlsUse", "ScopeNotApproved");
        }

        if (!options.ExternalTlsHandshakeExecutionApproved || !options.DemoReadOnly)
        {
            return Blocked("MarketDataOnlyTlsConnectorConfigRejected", "TlsExecutionNotApproved");
        }

        if (client is null || !client.Connected)
        {
            return Blocked("MarketDataOnlyTlsConnectorBlockedBeforeTlsUse", "TcpBoundaryNotOpened");
        }

        try
        {
            var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
            ssl.AuthenticateAsClientAsync(endpointBinding.RuntimeHost)
                .WaitAsync(options.Timeout, cancellationToken)
                .GetAwaiter()
                .GetResult();
            tlsStream = ssl;
            guardedFixStream = new LmaxMarketDataOnlyGuardedWriteStream(ssl);
            return Succeeded("MarketDataOnlyTlsConnectorSucceededSanitized");
        }
        catch (Exception ex) when (ex is AuthenticationException or IOException or SocketException or TimeoutException or OperationCanceledException or InvalidOperationException)
        {
            ShutdownRevert();
            return Failed("MarketDataOnlyTlsConnectorFailedSanitized", ex is TimeoutException ? "TlsHandshakeTimeout" : "TlsHandshakeBoundaryFailed");
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

        if (!IsApprovedMarketDataOnlyScope(scope))
        {
            return Blocked("MarketDataOnlyFixConnectorBlockedBeforeFixUse", "ScopeNotApproved");
        }

        if (!options.ExternalFixExecutionApproved || !options.DemoReadOnly)
        {
            return Blocked("MarketDataOnlyFixConnectorConfigRejected", "FixExecutionNotApproved");
        }

        if (!accessRecord.AccessPolicyAccepted ||
            accessRecord.SensitiveMaterialReturned ||
            accessRecord.SensitiveMaterialPrinted ||
            accessRecord.SensitiveMaterialStored)
        {
            return Blocked("MarketDataOnlyFixConnectorCredentialPolicyRejected", "CredentialPolicyNotSafe");
        }

        if (guardedFixStream is null || tlsStream is null)
        {
            return Blocked("MarketDataOnlyFixConnectorBlockedBeforeFixUse", "AuthenticatedFixSessionStreamUnavailable");
        }

        var result = fixLogonFrameWriter.WriteLogonFrame(
            guardedFixStream,
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

        if (!IsApprovedMarketDataOnlyScope(scope))
        {
            return BlockedMarketData(scope, "MarketDataOnlyMarketDataRequestBlockedBeforeWrite", "ScopeNotApproved");
        }

        if (!options.ExternalMarketDataRequestExecutionApproved || !options.DemoReadOnly)
        {
            return BlockedMarketData(scope, "MarketDataOnlyMarketDataRequestConfigRejected", "MarketDataExecutionNotApproved");
        }

        if (!fixSessionOpened || guardedFixStream is null)
        {
            return BlockedMarketData(scope, "MarketDataOnlyMarketDataRequestBlockedBeforeWrite", "FixSessionAcknowledgementRequired");
        }

        return marketDataRequestOperation.RequestReadOnlyMarketData(
            guardedFixStream,
            fixSessionOpened,
            options,
            scope,
            cancellationToken);
    }

    public bool ShutdownRevert()
    {
        guardedFixStream?.Dispose();
        guardedFixStream = null;
        tlsStream = null;
        client?.Dispose();
        client = null;
        fixSessionOpened = false;
        return true;
    }

    private static bool IsApprovedMarketDataOnlyScope(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
        => scope.DemoReadOnly &&
           string.Equals(scope.Environment, "Demo", StringComparison.OrdinalIgnoreCase) &&
           LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved(scope.Phase) &&
           scope.SafetyFlags is
           {
               AllowOrderSubmission: false,
               AllowLiveTrading: false,
               IsTradingEnabled: false,
               SchedulerEnabled: false,
               TradingMutationEnabled: false,
               OrderGatewayRegistered: false,
               TradingGatewayRegistered: false
           };

    private static LmaxRealReadOnlyDependencyResult Succeeded(string status)
        => new(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, status, null, null);

    private static LmaxRealReadOnlyDependencyResult Failed(string status, string category)
        => new(LmaxTemporaryReadOnlySessionBoundaryStatus.Failed, status, category, "Market-data-only boundary failed with sanitized evidence.");

    private static LmaxRealReadOnlyDependencyResult Blocked(string status, string category)
        => new(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, status, category, null);

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
                status,
                category,
                null,
                instrument.Caveat)).ToList(),
            status,
            category,
            null);
}

public static class LmaxMarketDataOnlyRealRuntimeFactory
{
    public static LmaxRealReadOnlyMarketDataTransport CreateTransport(bool allowCredentialMaterial)
    {
        var connector = new LmaxMarketDataOnlyManualSessionConnector();
        var socketClient = new LmaxRealReadOnlySocketConnectionClient(connector.Connect, connector.ShutdownRevert);
        var tlsClient = new LmaxRealReadOnlyTlsHandshakeClient(connector.AuthenticateTls, connector.ShutdownRevert);
        var fixClient = new LmaxRealReadOnlyFixFrameClient(connector.OpenFixSession, connector.ShutdownRevert);
        var marketDataClient = new LmaxRealReadOnlyMarketDataFrameClient(connector.RequestMarketData, connector.ShutdownRevert);
        var credentialClient = new LmaxRealReadOnlyCredentialConfigClient((_, _, policy, _) =>
        {
            var hasCredentialMaterial =
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LMAX_DEMO_SENDER_COMP_ID")) &&
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LMAX_DEMO_TARGET_COMP_ID")) &&
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LMAX_DEMO_FIX_USERNAME")) &&
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LMAX_DEMO_FIX_PASSWORD"));
            var allowed = allowCredentialMaterial &&
                          policy.FutureApprovedRuntimeAttemptRequired &&
                          policy.RedactSensitiveFields &&
                          string.Equals(policy.Environment, "Demo/read-only", StringComparison.OrdinalIgnoreCase);
            return new LmaxRealReadOnlySecretAccessResult(
                AccessAllowed: allowed,
                RealSecretMaterialLoaded: allowed && hasCredentialMaterial,
                SensitiveMaterialReturned: false,
                SensitiveMaterialPrinted: false,
                SensitiveMaterialStored: false,
                allowed ? "MarketDataOnlyCredentialMaterialReadySanitized" : "MarketDataOnlyCredentialMaterialNotLoaded",
                allowed && hasCredentialMaterial ? null : "CredentialMaterialUnavailable",
                null);
        });

        var endpoint = LmaxReadOnlyActivationManualDemoEndpointBinding.CreateApprovedDemoMarketData();
        var dependencyProviders = new LmaxRealReadOnlyDependencyProviderFactory(
            new LmaxRealReadOnlySocketBoundaryProvider(
                new LmaxReadOnlySocketConnectionOptions(
                    "Demo/read-only",
                    LmaxReadOnlyActivationManualDemoEndpointBinding.SanitizedEndpointLabel,
                    endpoint.RuntimePort,
                    TimeSpan.FromSeconds(15),
                    DemoReadOnly: true,
                    ExternalConnectionExecutionApproved: true),
                socketClient),
            new LmaxRealReadOnlyTlsBoundaryProvider(
                new LmaxReadOnlyTlsConnectionOptions(
                    "Demo/read-only",
                    LmaxReadOnlyActivationManualDemoEndpointBinding.SanitizedEndpointLabel,
                    LmaxReadOnlyActivationManualDemoEndpointBinding.SanitizedTargetHostLabel,
                    TimeSpan.FromSeconds(15),
                    DemoReadOnly: true,
                    "SystemDefaultValidation",
                    ExternalTlsHandshakeExecutionApproved: true),
                tlsClient),
            new LmaxRealReadOnlyFixFrameBoundaryProvider(
                new LmaxReadOnlyFixSessionOptions(
                    "Demo/read-only",
                    "DemoReadOnlySenderCompId",
                    "DemoReadOnlyTargetCompId",
                    30,
                    TimeSpan.FromSeconds(15),
                    DemoReadOnly: true,
                    LmaxReadOnlyFixSessionOptions.DefaultAllowedReadOnlyMessageTypes,
                    ExternalFixExecutionApproved: true),
                fixClient),
            new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(
                new LmaxReadOnlyMarketDataRequestOptions(
                    "Demo/read-only",
                    DemoReadOnly: true,
                    "ReadOnlyMarketDataRequest",
                    "M2C1B_PARAMETERIZED_EURUSD_REQUEST_BUILT_BY_CAPTURE_RUNNER",
                    TimeSpan.FromSeconds(15),
                    LmaxReadOnlyMarketDataRequestOptions.DefaultAllowedReadOnlyMessageTypes,
                    ExternalMarketDataRequestExecutionApproved: true),
                marketDataClient),
            new LmaxRealReadOnlyCredentialConfigBoundaryProvider(
                new LmaxReadOnlyCredentialConfigOptions(
                    "Demo/read-only",
                    DemoReadOnly: true,
                    "DemoReadOnlyConfigSource",
                    ExternalCredentialAccessApproved: true),
                new LmaxRealReadOnlyCredentialConfigClient((options, scope, policy, cancellationToken) =>
                    credentialClient.AccessDemoReadOnlyConfig(options, scope, policy, cancellationToken)))).Create();

        return new LmaxRealReadOnlyMarketDataTransport(
            dependencyProviders.CreateLowLevelDependencySet().CreateSessionClient(new LmaxReadOnlyCredentialAccessPolicy(
                FutureApprovedRuntimeAttemptRequired: true,
                RealSecretMaterialAllowedNow: allowCredentialMaterial,
                RedactSensitiveFields: true,
                Environment: "Demo/read-only")));
    }
}
