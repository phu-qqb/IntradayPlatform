using System.Reflection;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxExecutableBoundaryOperationCompositionRequest(
    LmaxTemporaryReadOnlyRuntimeActivationRequest ActivationRequest,
    LmaxConcreteBoundedRuntimeActivationCompositionResult BoundedRuntimeComposition,
    LmaxReadOnlyExecutionOperationBindingSet? OperationBindings,
    LmaxReadOnlyProviderClientOperationSet? ProviderClients,
    bool BoundedExecutorApproved,
    bool RuntimeDelegateBindingApproved,
    bool ConcreteBoundedRuntimeCompositionUsed,
    bool NoApiWorkerStartupPath,
    bool NoLiveLauncher,
    bool NoHostedBackgroundService,
    bool NoSchedulerPolling,
    bool NoOrderTradingPath,
    bool ProductionAccountForbidden,
    bool ExternalBoundaryAttempted = false);

public sealed record LmaxExecutableBoundaryOperationCompositionResult(
    bool Passed,
    string Status,
    bool NoApprovedR45ExecutableBoundaryOperationComposition,
    bool ExecutableBoundaryOperationCompositionExplicit,
    bool ConcreteBoundedRuntimeCompositionUsed,
    bool AdapterModeApprovedBoundedExecutableReadOnly,
    bool BoundedExecutorApproved,
    bool RuntimeDelegateBindingApproved,
    bool CredentialConfigOperationPresent,
    bool TcpSocketOperationPresent,
    bool TlsOperationPresent,
    bool FixLogonSessionOperationPresent,
    bool MarketDataRequestOperationPresent,
    bool MarketDataResponseEntryCapturePresent,
    bool ShutdownRevertOperationPresent,
    bool ApprovedInstrumentsExact,
    bool UsdJpyCaveatPreserved,
    bool ProductionAccountAllowed,
    bool ApiWorkerStartupRequired,
    bool LiveLauncherRequired,
    bool HostedBackgroundServiceRequired,
    bool SchedulerPollingRequired,
    bool OrderTradingPathReachable,
    bool ExternalBoundaryAttempted,
    LmaxTemporaryReadOnlySessionBoundaryStatus TcpBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus TlsBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus FixLogonBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus MarketDataRequestBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus MarketDataResponseBoundary,
    LmaxTemporaryReadOnlySessionBoundaryStatus ShutdownRevertBoundary,
    IReadOnlyList<string> OperationSummary,
    IReadOnlyList<LmaxReadOnlyRuntimePreflightIssue> Issues);

public sealed class LmaxExecutableBoundaryOperationComposition
{
    public LmaxExecutableBoundaryOperationCompositionResult Validate(
        LmaxExecutableBoundaryOperationCompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var issues = new List<LmaxReadOnlyRuntimePreflightIssue>();
        var activationRequest = request.ActivationRequest;
        var scope = activationRequest.HarnessResult.Scope;

        if (!request.ConcreteBoundedRuntimeCompositionUsed || !request.BoundedRuntimeComposition.Passed)
        {
            Add(issues, "ConcreteBoundedRuntimeCompositionMissing", "$.boundedRuntimeComposition", "Executable boundary operation composition must use the R44 concrete bounded runtime activation composition.");
        }

        if (request.BoundedRuntimeComposition.NoApprovedR43BoundedExecutableRuntimeActivationComposition)
        {
            Add(issues, "R43BoundedRuntimeCompositionBlockerPresent", "$.boundedRuntimeComposition.noApprovedR43BoundedExecutableRuntimeActivationComposition", "R43 bounded runtime composition blocker must stay cleared.");
        }

        if (activationRequest.AdapterMode != LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly)
        {
            Add(issues, "ApprovedBoundedExecutableReadOnlyModeMissing", "$.activationRequest.adapterMode", "Executable boundary operation composition requires ApprovedBoundedExecutableReadOnly adapter mode.");
        }

        if (!request.BoundedExecutorApproved || !activationRequest.BoundedExecutorApproved)
        {
            Add(issues, "BoundedExecutorApprovalMissing", "$.boundedExecutorApproved", "Executable boundary operation composition requires bounded executor approval.");
        }

        if (!request.RuntimeDelegateBindingApproved || !activationRequest.RuntimeDelegateBindingApproved)
        {
            Add(issues, "RuntimeDelegateBindingApprovalMissing", "$.runtimeDelegateBindingApproved", "Executable boundary operation composition requires runtime delegate binding approval.");
        }

        if (!LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved(activationRequest.RequestedNextApprovalPhase))
        {
            Add(issues, "UnexpectedApprovedRetryPhase", "$.activationRequest.requestedNextApprovalPhase", "Executable boundary operation composition is reserved for LMAX-R45 or the next approved retry phase.");
        }

        var operationBindings = request.OperationBindings;
        var providerClients = request.ProviderClients;
        if (operationBindings is null)
        {
            Add(issues, "OperationBindingSetMissing", "$.operationBindings", "Operation binding set must be supplied by the approved runtime delegate composition.");
        }

        if (providerClients is null)
        {
            Add(issues, "ProviderClientSetMissing", "$.providerClients", "Provider client operation set must be supplied by the approved runtime delegate composition.");
        }

        var credentialPresent = operationBindings?.CredentialConfig is not null && providerClients?.CredentialConfigClient is not null;
        var socketPresent = operationBindings?.Socket is not null && providerClients?.SocketClient is not null;
        var tlsPresent = operationBindings?.Tls is not null && providerClients?.TlsClient is not null;
        var fixPresent = operationBindings?.Fix is not null && providerClients?.FixClient is not null;
        var marketDataPresent = operationBindings?.MarketData is not null && providerClients?.MarketDataClient is not null;
        var marketDataResponseCapturePresent = marketDataPresent &&
            typeof(LmaxReadOnlyMarketDataSessionClientResult).GetProperty(nameof(LmaxReadOnlyMarketDataSessionClientResult.InstrumentStatuses)) is not null &&
            typeof(LmaxTemporaryReadOnlyInstrumentMarketDataStatus).GetProperty(nameof(LmaxTemporaryReadOnlyInstrumentMarketDataStatus.MarketDataSnapshotCount)) is not null;
        var shutdownRevertPresent = providerClients is not null &&
            HasShutdownRevert(providerClients.SocketClient) &&
            HasShutdownRevert(providerClients.TlsClient) &&
            HasShutdownRevert(providerClients.FixClient) &&
            HasShutdownRevert(providerClients.MarketDataClient);

        if (!credentialPresent)
        {
            Add(issues, "CredentialConfigBoundaryOperationMissing", "$.providerClients.credentialConfigClient", "Credential/config boundary operation must be composed.");
        }

        if (!socketPresent)
        {
            Add(issues, "TcpSocketBoundaryOperationMissing", "$.providerClients.socketClient", "TCP/socket boundary operation must be composed.");
        }

        if (!tlsPresent)
        {
            Add(issues, "TlsBoundaryOperationMissing", "$.providerClients.tlsClient", "TLS boundary operation must be composed.");
        }

        if (!fixPresent)
        {
            Add(issues, "FixLogonBoundaryOperationMissing", "$.providerClients.fixClient", "FIX logon/session boundary operation must be composed.");
        }

        if (!marketDataPresent)
        {
            Add(issues, "MarketDataRequestBoundaryOperationMissing", "$.providerClients.marketDataClient", "MarketDataRequest boundary operation must be composed.");
        }

        if (!marketDataResponseCapturePresent)
        {
            Add(issues, "MarketDataResponseEntryCaptureMissing", "$.marketDataResponse", "MarketDataResponse/entry capture model must be present.");
        }

        if (!shutdownRevertPresent)
        {
            Add(issues, "ShutdownRevertBoundaryOperationMissing", "$.shutdownRevert", "Shutdown/revert boundary operation must be composed.");
        }

        if (!request.NoApiWorkerStartupPath)
        {
            Add(issues, "ApiWorkerStartupPathPresent", "$.noApiWorkerStartupPath", "Executable boundary operation composition must not require API/Worker startup.");
        }

        if (!request.NoLiveLauncher)
        {
            Add(issues, "LiveLauncherPresent", "$.noLiveLauncher", "Executable boundary operation composition must not create a live launcher.");
        }

        if (!request.NoHostedBackgroundService)
        {
            Add(issues, "HostedBackgroundServicePresent", "$.noHostedBackgroundService", "Executable boundary operation composition must not add a hosted/background service.");
        }

        if (!request.NoSchedulerPolling)
        {
            Add(issues, "SchedulerPollingPresent", "$.noSchedulerPolling", "Executable boundary operation composition must not require scheduler or polling.");
        }

        if (!request.NoOrderTradingPath)
        {
            Add(issues, "OrderTradingPathReachable", "$.noOrderTradingPath", "Executable boundary operation composition must not expose order or trading paths.");
        }

        if (!request.ProductionAccountForbidden)
        {
            Add(issues, "ProductionAccountNotForbidden", "$.productionAccountForbidden", "Production account use must remain forbidden.");
        }

        if (request.ExternalBoundaryAttempted)
        {
            Add(issues, "ExternalBoundaryAttempted", "$.externalBoundaryAttempted", "R46 composition validation must not attempt external boundaries.");
        }

        var approvedInstrumentsExact = ApprovedInstrumentsExact(scope);
        if (!approvedInstrumentsExact)
        {
            Add(issues, "ApprovedInstrumentListMismatch", "$.scope.instruments", "Executable boundary operation composition requires exactly GBPUSD, EURGBP, AUDUSD, and USDJPY.");
        }

        var usdJpyCaveatPreserved = UsdJpyCaveatPreserved(scope);
        if (!usdJpyCaveatPreserved)
        {
            Add(issues, "UsdJpyCaveatMissing", "$.scope.instruments[USDJPY].caveat", "USDJPY caveat must be preserved exactly.");
        }

        issues.AddRange(LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope).Issues);

        var passed = issues.Count == 0;
        return new LmaxExecutableBoundaryOperationCompositionResult(
            Passed: passed,
            Status: passed
                ? "ExecutableBoundaryOperationCompositionReadyNoExternalActivation"
                : "ExecutableBoundaryOperationCompositionRejected",
            NoApprovedR45ExecutableBoundaryOperationComposition: !passed,
            ExecutableBoundaryOperationCompositionExplicit: passed,
            ConcreteBoundedRuntimeCompositionUsed: request.ConcreteBoundedRuntimeCompositionUsed && request.BoundedRuntimeComposition.Passed,
            AdapterModeApprovedBoundedExecutableReadOnly: activationRequest.AdapterMode == LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
            BoundedExecutorApproved: request.BoundedExecutorApproved && activationRequest.BoundedExecutorApproved,
            RuntimeDelegateBindingApproved: request.RuntimeDelegateBindingApproved && activationRequest.RuntimeDelegateBindingApproved,
            CredentialConfigOperationPresent: credentialPresent,
            TcpSocketOperationPresent: socketPresent,
            TlsOperationPresent: tlsPresent,
            FixLogonSessionOperationPresent: fixPresent,
            MarketDataRequestOperationPresent: marketDataPresent,
            MarketDataResponseEntryCapturePresent: marketDataResponseCapturePresent,
            ShutdownRevertOperationPresent: shutdownRevertPresent,
            ApprovedInstrumentsExact: approvedInstrumentsExact,
            UsdJpyCaveatPreserved: usdJpyCaveatPreserved,
            ProductionAccountAllowed: !request.ProductionAccountForbidden || scope.SafetyFlags.ProductionAccountRequested,
            ApiWorkerStartupRequired: !request.NoApiWorkerStartupPath,
            LiveLauncherRequired: !request.NoLiveLauncher,
            HostedBackgroundServiceRequired: !request.NoHostedBackgroundService,
            SchedulerPollingRequired: !request.NoSchedulerPolling || scope.SafetyFlags.SchedulerEnabled || scope.SafetyFlags.PollingEnabled,
            OrderTradingPathReachable: !request.NoOrderTradingPath || scope.SafetyFlags.AllowOrderSubmission || scope.SafetyFlags.AllowLiveTrading || scope.SafetyFlags.IsTradingEnabled,
            ExternalBoundaryAttempted: request.ExternalBoundaryAttempted,
            TcpBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            TlsBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            FixLogonBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            MarketDataRequestBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            MarketDataResponseBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            ShutdownRevertBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            OperationSummary: BuildSummary(credentialPresent, socketPresent, tlsPresent, fixPresent, marketDataPresent, marketDataResponseCapturePresent, shutdownRevertPresent),
            Issues: issues);
    }

    private static bool ApprovedInstrumentsExact(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
    {
        var expected = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var actual = scope.Instruments
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return actual.Length == expected.Length &&
               actual.Zip(expected).All(pair =>
                   string.Equals(pair.First.Symbol, pair.Second.Symbol, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(pair.First.SecurityId, pair.Second.SecurityId, StringComparison.Ordinal) &&
                   string.Equals(pair.First.SecurityIdSource, pair.Second.SecurityIdSource, StringComparison.Ordinal));
    }

    private static bool UsdJpyCaveatPreserved(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
        => scope.Instruments
            .Where(x => string.Equals(x.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase))
            .All(x => string.Equals(x.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal));

    private static bool HasShutdownRevert(object client)
        => client.GetType().GetMethod("ShutdownRevert", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) is not null;

    private static IReadOnlyList<string> BuildSummary(
        bool credential,
        bool socket,
        bool tls,
        bool fix,
        bool marketData,
        bool marketDataResponse,
        bool shutdown)
        =>
        [
            $"CredentialConfig:{credential}",
            $"TcpSocket:{socket}",
            $"Tls:{tls}",
            $"FixLogonSession:{fix}",
            $"MarketDataRequest:{marketData}",
            $"MarketDataResponseEntries:{marketDataResponse}",
            $"ShutdownRevert:{shutdown}"
        ];

    private static void Add(List<LmaxReadOnlyRuntimePreflightIssue> issues, string code, string path, string message)
        => issues.Add(new LmaxReadOnlyRuntimePreflightIssue(code, path, message));
}
