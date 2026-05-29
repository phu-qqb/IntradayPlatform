namespace QQ.Production.Intraday.Tools.LmaxReadOnlyActivation;

public sealed class LmaxReadOnlyActivationManualDemoEndpointBinding
{
    public const string BindingName = "LmaxReadOnlyActivationManualDemoEndpointBinding";
    public const string EndpointMode = "Demo";
    public const string SanitizedEndpointLabel = "DemoReadOnlyEndpoint";
    public const string SanitizedTargetHostLabel = "DemoReadOnlyTargetHost";

    private const string ApprovedDemoMarketDataHost = "fix-marketdata.london-demo.lmax.com";
    private const int ApprovedDemoMarketDataPort = 443;

    private LmaxReadOnlyActivationManualDemoEndpointBinding(string runtimeHost, int runtimePort)
    {
        RuntimeHost = runtimeHost;
        RuntimePort = runtimePort;
    }

    public string RuntimeHost { get; }

    public int RuntimePort { get; }

    public static LmaxReadOnlyActivationManualDemoEndpointBinding CreateApprovedDemoMarketData()
        => new(ApprovedDemoMarketDataHost, ApprovedDemoMarketDataPort);

    public LmaxReadOnlyActivationManualDemoEndpointBindingValidation Validate()
    {
        var hostPresent = !string.IsNullOrWhiteSpace(RuntimeHost);
        var portPresent = RuntimePort > 0;
        var hostWasPlaceholder = string.Equals(RuntimeHost, SanitizedEndpointLabel, StringComparison.Ordinal) ||
                                 string.Equals(RuntimeHost, SanitizedTargetHostLabel, StringComparison.Ordinal);
        var productionExcluded = RuntimeHost.Contains("london-demo.lmax.com", StringComparison.OrdinalIgnoreCase) &&
                                 !RuntimeHost.Contains("fix-order", StringComparison.OrdinalIgnoreCase);

        return new LmaxReadOnlyActivationManualDemoEndpointBindingValidation(
            BindingName,
            EndpointMode,
            EndpointPresent: hostPresent && portPresent,
            HostPresent: hostPresent,
            HostConcreteBinding: hostPresent && !hostWasPlaceholder,
            HostWasPlaceholder: hostWasPlaceholder,
            PortPresent: portPresent,
            PortConcreteBinding: RuntimePort == ApprovedDemoMarketDataPort,
            ProductionExcluded: productionExcluded,
            EndpointApproved: hostPresent &&
                              !hostWasPlaceholder &&
                              RuntimePort == ApprovedDemoMarketDataPort &&
                              productionExcluded,
            RawHostSerialized: false,
            RawPortSerialized: false,
            CredentialValuesReturned: false);
    }
}

public sealed record LmaxReadOnlyActivationManualDemoEndpointBindingValidation(
    string BindingName,
    string EndpointMode,
    bool EndpointPresent,
    bool HostPresent,
    bool HostConcreteBinding,
    bool HostWasPlaceholder,
    bool PortPresent,
    bool PortConcreteBinding,
    bool ProductionExcluded,
    bool EndpointApproved,
    bool RawHostSerialized,
    bool RawPortSerialized,
    bool CredentialValuesReturned);
