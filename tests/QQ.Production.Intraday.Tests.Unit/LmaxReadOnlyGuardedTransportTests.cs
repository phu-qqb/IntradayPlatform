using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyGuardedTransportTests
{
    [Fact]
    public async Task Disabled_guarded_transport_blocks_connect_readonly_and_reports_no_capabilities()
    {
        var transport = new LmaxReadOnlyGuardedTransportDisabled(Phase4FutureLookingOptions());

        var status = await transport.GetStatusAsync();
        var result = await transport.ConnectReadOnlyAsync(new LmaxReadOnlyGuardedTransportRequest("phase 4f connect blocked"));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, status.Status);
        Assert.True(status.Disabled);
        Assert.False(status.Capabilities.NetworkTransportImplemented);
        Assert.False(status.Capabilities.SocketActivation);
        Assert.False(status.Capabilities.FixLogonImplemented);
        Assert.False(status.Capabilities.CredentialUseImplemented);
        Assert.False(status.Capabilities.OrderSubmissionImplemented);
        Assert.True(status.Capabilities.ReadOnlyOnly);
        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, result.Status);
        Assert.Equal("ConnectReadOnly", result.Operation);
        Assert.False(result.NetworkTransportImplemented);
        Assert.False(result.SocketOpened);
        Assert.False(result.FixLogonAttempted);
        Assert.False(result.CredentialsUsed);
        Assert.False(result.EventsRead);
        Assert.Empty(result.Events);
        Assert.Contains("GuardedTransportImplementationDisabled", result.Safety.FailedGateNames);
        Assert.Contains("SocketActivationAllowed", result.Safety.FailedGateNames);
    }

    [Fact]
    public async Task Disabled_guarded_transport_read_and_disconnect_return_no_events_or_side_effect_flags()
    {
        var transport = new LmaxReadOnlyGuardedTransportDisabled(Phase4FutureLookingOptions());

        var read = await transport.ReadEventsAsync(new LmaxReadOnlyGuardedTransportRequest("phase 4f read blocked"));
        var disconnect = await transport.DisconnectAsync(new LmaxReadOnlyGuardedTransportRequest("phase 4f disconnect blocked"));

        foreach (var result in new[] { read, disconnect })
        {
            Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, result.Status);
            Assert.False(result.NetworkTransportImplemented);
            Assert.False(result.SocketOpened);
            Assert.False(result.FixLogonAttempted);
            Assert.False(result.CredentialsUsed);
            Assert.False(result.EventsRead);
            Assert.Empty(result.Events);
            Assert.True(result.Safety.Capabilities.ReadOnlyOnly);
        }
    }

    [Fact]
    public async Task Disabled_guarded_transport_remains_blocked_even_when_future_external_gates_are_true()
    {
        var transport = new LmaxReadOnlyGuardedTransportDisabled(Phase4FutureLookingOptions() with
        {
            Enabled = true,
            ImplementationMode = LmaxReadOnlyRuntimeImplementationMode.FutureReadOnly,
            AllowExternalConnections = true,
            AllowCredentialUse = true,
            RequestedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit,
            MaxAllowedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit
        });

        var safety = await transport.EvaluateSafetyAsync(new LmaxReadOnlyGuardedTransportRequest("future gates still blocked"));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, safety.RunStatus);
        Assert.Contains("GuardedTransportInterfacePresent", safety.Gates.Select(x => x.Name));
        Assert.Contains("GuardedTransportImplementationDisabled", safety.FailedGateNames);
        Assert.Contains("SocketActivationAllowed", safety.FailedGateNames);
        Assert.Contains("FixLogonAllowed", safety.FailedGateNames);
        Assert.Contains("CredentialUseAllowed", safety.FailedGateNames);
        Assert.Contains("OrderSubmissionAllowed", safety.FailedGateNames);
        Assert.Contains("ShadowReplaySubmitAllowed", safety.FailedGateNames);
        Assert.Contains("TradingMutationAllowed", safety.FailedGateNames);
        Assert.Contains("SchedulerAllowed", safety.FailedGateNames);
        Assert.Contains("Phase4FStillNoSocket", safety.Gates.Where(x => !x.BlocksRun).Select(x => x.Name));
    }

    [Fact]
    public void Guarded_transport_contract_has_no_order_submission_methods()
    {
        var methodNames = typeof(ILmaxReadOnlyGuardedTransport)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(x => x.Name)
            .ToList();

        Assert.Contains("ConnectReadOnlyAsync", methodNames);
        Assert.Contains("DisconnectAsync", methodNames);
        Assert.Contains("ReadEventsAsync", methodNames);
        Assert.DoesNotContain(methodNames, x => x.Contains("Submit", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("NewOrder", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("Replace", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Guarded_transport_dtos_do_not_expose_secret_shaped_fields()
    {
        var types = new[]
        {
            typeof(LmaxReadOnlyGuardedTransportRequest),
            typeof(LmaxReadOnlyGuardedTransportResult),
            typeof(LmaxReadOnlyGuardedTransportStatus),
            typeof(LmaxReadOnlyGuardedTransportCapabilities),
            typeof(LmaxReadOnlyGuardedTransportSafetyReport)
        };

        foreach (var property in types.SelectMany(x => x.GetProperties(BindingFlags.Public | BindingFlags.Instance)))
        {
            Assert.DoesNotContain("password", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("apiKey", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("privateKey", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("credentialValue", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("authorization", property.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Guarded_transport_does_not_reference_connectivity_lab()
    {
        var references = typeof(LmaxReadOnlyGuardedTransportDisabled)
            .Assembly
            .GetReferencedAssemblies()
            .Select(x => x.Name ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(references, x => x.Contains("ConnectivityLab", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Guarded_transport_boundary_has_no_network_implementation_type_names()
    {
        var typeNames = typeof(LmaxReadOnlyGuardedTransportDisabled)
            .Assembly
            .GetTypes()
            .Where(x => x.Namespace == "QQ.Production.Intraday.Infrastructure.Lmax" && x.Name.Contains("GuardedTransport", StringComparison.Ordinal))
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain(typeNames, x => x.Contains("Tcp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeNames, x => x.Contains("Socket", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeNames, x => x.Contains("Ssl", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeNames, x => x.Contains("QuickFix", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeNames, x => x.Contains("WebSocket", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeNames, x => x.Contains("HttpClient", StringComparison.OrdinalIgnoreCase));
    }

    private static LmaxReadOnlyRuntimeAdapterOptions Phase4FutureLookingOptions()
        => new()
        {
            Enabled = true,
            ImplementationMode = LmaxReadOnlyRuntimeImplementationMode.FutureReadOnly,
            AllowExternalConnections = true,
            AllowCredentialUse = true,
            ReadOnly = true,
            AllowOrderSubmission = false,
            PersistRawFixMessages = false,
            PersistToTradingTables = false,
            SubmitToShadowReplay = false,
            SchedulerEnabled = false,
            EnvironmentName = "Demo",
            OperationalReadinessPassed = true,
            GovernanceApproved = true,
            LocalOnlyApi = true,
            DryRun = true,
            RequestedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit,
            MaxAllowedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit
        };
}
