using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyExternalSessionContractsTests
{
    [Fact]
    public async Task Phase4e_skeleton_always_blocks_and_reports_not_implemented_flags()
    {
        var session = new LmaxReadOnlyExternalSessionSkeleton(Phase4SafeLookingOptions());

        var status = await session.GetStatusAsync();
        var result = await session.RunAsync(new LmaxReadOnlyExternalSessionRequest("phase 4e skeleton blocked test"));
        var report = session.GetSkeletonSafetyReport();

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, status.Status);
        Assert.False(status.ExternalSessionImplementationAvailable);
        Assert.False(status.SocketImplementationAvailable);
        Assert.Contains("SkeletonOnly", status.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, result.Status);
        Assert.False(result.ExternalSessionImplementationAvailable);
        Assert.False(result.SocketOpened);
        Assert.False(result.CredentialsUsed);
        Assert.False(result.EvidenceCreated);
        Assert.False(result.SubmittedToShadowReplay);
        Assert.Equal("SkeletonOnly", report.ExternalSessionImplementationMode);
        Assert.False(report.SocketActivation);
        Assert.False(report.FixLogonImplemented);
        Assert.False(report.CredentialUseImplemented);
        Assert.False(report.OrderSubmissionImplemented);
        Assert.False(report.ShadowReplaySubmitImplemented);
        Assert.False(report.TradingMutationImplemented);
        Assert.False(report.SchedulerImplemented);
        Assert.False(report.RuntimeGatewayRegistrationImplemented);
        Assert.Contains("ExternalSessionSkeletonPresent", result.Safety.Gates.Select(x => x.Name));
        Assert.Contains("ExternalSessionImplementationStarted", result.Safety.FailedGateNames);
        Assert.Contains("SocketActivationAllowed", result.Safety.FailedGateNames);
        Assert.Contains("FixLogonAllowed", result.Safety.FailedGateNames);
    }

    [Fact]
    public async Task Phase4e_skeleton_remains_blocked_even_when_future_external_gates_are_requested()
    {
        var options = Phase4SafeLookingOptions() with
        {
            Enabled = true,
            ImplementationMode = LmaxReadOnlyRuntimeImplementationMode.FutureReadOnly,
            AllowExternalConnections = true,
            AllowCredentialUse = true,
            RequestedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit,
            MaxAllowedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit
        };
        var session = new LmaxReadOnlyExternalSessionSkeleton(options);

        var result = await session.RunAsync(new LmaxReadOnlyExternalSessionRequest("future gates still blocked"));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, result.Status);
        Assert.Contains("ExternalSessionImplementationStarted", result.Safety.FailedGateNames);
        Assert.Contains("SocketActivationAllowed", result.Safety.FailedGateNames);
        Assert.Contains("CredentialUseAllowed", result.Safety.FailedGateNames);
        Assert.Contains("ShadowReplaySubmitAllowed", result.Safety.FailedGateNames);
        Assert.False(result.SocketOpened);
        Assert.False(result.CredentialsUsed);
        Assert.False(result.EvidenceCreated);
        Assert.False(result.SubmittedToShadowReplay);
    }

    [Fact]
    public async Task Disabled_external_session_always_blocks_without_side_effects()
    {
        var session = new LmaxReadOnlyExternalSessionDisabled(Phase4SafeLookingOptions());

        var result = await session.RunAsync(new LmaxReadOnlyExternalSessionRequest("phase 4a blocked test"));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, result.Status);
        Assert.False(result.ExternalSessionImplementationAvailable);
        Assert.False(result.SocketOpened);
        Assert.False(result.CredentialsUsed);
        Assert.False(result.EvidenceCreated);
        Assert.False(result.SubmittedToShadowReplay);
        Assert.Contains("ExternalSessionImplementationAvailable", result.Safety.FailedGateNames);
        Assert.Contains("ExternalSessionSocketImplementation", result.Safety.FailedGateNames);
    }

    [Fact]
    public void External_session_safety_gate_fails_closed_by_default()
    {
        var gate = new LmaxReadOnlyExternalSessionSafetyGate();

        var evaluation = gate.Evaluate(new LmaxReadOnlyExternalSessionRequest("default blocked test"));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Disabled, evaluation.RunStatus);
        Assert.Contains("Enabled", evaluation.FailedGateNames);
        Assert.Contains("ImplementationMode", evaluation.FailedGateNames);
        Assert.Contains("ActivationLevel", evaluation.FailedGateNames);
        Assert.Contains("ExternalSessionImplementationAvailable", evaluation.FailedGateNames);
    }

    [Fact]
    public void Phase4_external_session_remains_blocked_because_implementation_is_not_started()
    {
        var gate = new LmaxReadOnlyExternalSessionSafetyGate();

        var evaluation = gate.Evaluate(Phase4SafeLookingOptions(), new LmaxReadOnlyExternalSessionRequest("manual demo read-only prototype"));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, evaluation.RunStatus);
        Assert.Contains("Phase4ImplementationNotStarted", evaluation.FailedGateNames);
        Assert.Contains("ExternalSessionImplementationAvailable", evaluation.FailedGateNames);
        Assert.Contains("ExternalSessionSocketImplementation", evaluation.FailedGateNames);
    }

    [Theory]
    [InlineData("AllowOrderSubmission")]
    [InlineData("PersistToTradingTables")]
    [InlineData("SchedulerEnabled")]
    [InlineData("SubmitToShadowReplay")]
    [InlineData("NonDemoEnvironment")]
    [InlineData("MissingReason")]
    public void External_session_blocks_unsafe_phase4_conditions(string condition)
    {
        var options = Phase4SafeLookingOptions() with
        {
            AllowOrderSubmission = condition == "AllowOrderSubmission",
            PersistToTradingTables = condition == "PersistToTradingTables",
            SchedulerEnabled = condition == "SchedulerEnabled",
            SubmitToShadowReplay = condition == "SubmitToShadowReplay",
            EnvironmentName = condition == "NonDemoEnvironment" ? "UAT" : "Demo"
        };
        var reason = condition == "MissingReason" ? "" : "manual phase 4a test";
        var gate = new LmaxReadOnlyExternalSessionSafetyGate();

        var evaluation = gate.Evaluate(options, new LmaxReadOnlyExternalSessionRequest(reason));

        var expectedGate = condition switch
        {
            "AllowOrderSubmission" => "AllowOrderSubmission",
            "PersistToTradingTables" => "PersistToTradingTables",
            "SchedulerEnabled" => "SchedulerEnabled",
            "SubmitToShadowReplay" => "SubmitToShadowReplay",
            "NonDemoEnvironment" => "Phase4EnvironmentName",
            "MissingReason" => "Phase4ReasonRequired",
            _ => throw new ArgumentOutOfRangeException(nameof(condition), condition, null)
        };
        Assert.Contains(expectedGate, evaluation.FailedGateNames);
        Assert.Contains("ExternalSessionImplementationAvailable", evaluation.FailedGateNames);
    }

    [Fact]
    public void External_session_event_types_are_read_only_only()
    {
        var names = Enum.GetNames<LmaxReadOnlyExternalSessionEventType>();

        Assert.Contains("MarketDataSnapshot", names);
        Assert.Contains("TradeCaptureReport", names);
        Assert.Contains("OrderStatusReport", names);
        Assert.Contains("ProtocolReject", names);
        Assert.DoesNotContain(names, x => x.Contains("NewOrderSingle", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, x => x.Contains("OrderCancel", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, x => x.Contains("CancelReplace", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void External_session_contract_types_do_not_expose_secret_shaped_fields()
    {
        var types = new[]
        {
            typeof(LmaxReadOnlyExternalSessionRequest),
            typeof(LmaxReadOnlyExternalSessionResult),
            typeof(LmaxReadOnlyExternalSessionStatus),
            typeof(LmaxReadOnlyExternalSessionEvent),
            typeof(LmaxReadOnlyExternalSessionReject),
            typeof(LmaxReadOnlyExternalSessionCounters),
            typeof(LmaxReadOnlyExternalSessionSkeletonSafetyReport)
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
    public void External_session_contracts_do_not_define_order_submission_methods()
    {
        var methodNames = typeof(ILmaxReadOnlyExternalSession)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(x => x.Name)
            .ToList();

        Assert.Contains("GetStatusAsync", methodNames);
        Assert.Contains("EvaluateSafetyAsync", methodNames);
        Assert.Contains("RunAsync", methodNames);
        Assert.DoesNotContain(methodNames, x => x.Contains("Submit", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("NewOrder", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methodNames, x => x.Contains("Replace", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void External_session_contracts_do_not_reference_connectivity_lab()
    {
        var references = typeof(LmaxReadOnlyExternalSessionDisabled)
            .Assembly
            .GetReferencedAssemblies()
            .Select(x => x.Name ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(references, x => x.Contains("ConnectivityLab", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void External_session_phase4_boundary_adds_no_forbidden_network_or_real_transport_implementation_types()
    {
        var externalSessionTypes = typeof(LmaxReadOnlyExternalSessionDisabled)
            .Assembly
            .GetTypes()
            .Where(x => x.Namespace == "QQ.Production.Intraday.Infrastructure.Lmax" && x.Name.Contains("ExternalSession", StringComparison.Ordinal))
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain(externalSessionTypes, x => x.Contains("Tcp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(externalSessionTypes, x => x.Contains("Network", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(externalSessionTypes, x => x.Contains("Transport", StringComparison.OrdinalIgnoreCase)
                                                         && !x.Contains("FakeTransport", StringComparison.OrdinalIgnoreCase)
                                                         && !x.Contains("GuardedTransportSummary", StringComparison.OrdinalIgnoreCase)
                                                         && !string.Equals(x, "ILmaxReadOnlyExternalSessionTransport", StringComparison.Ordinal));
        Assert.DoesNotContain(externalSessionTypes, x => x.Contains("FixSessionClient", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Phase4e_skeleton_type_names_do_not_define_order_submission_surface()
    {
        var members = typeof(LmaxReadOnlyExternalSessionSkeleton)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(x => x.Name)
            .Concat(typeof(LmaxReadOnlyExternalSessionSkeletonSafetyReport)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(x => x.Name))
            .ToList();

        Assert.DoesNotContain(members, x => x.Contains("NewOrder", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, x => x.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, x => x.Contains("Replace", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, x => x.Contains("SubmitOrder", StringComparison.OrdinalIgnoreCase));
    }

    private static LmaxReadOnlyRuntimeAdapterOptions Phase4SafeLookingOptions()
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
