using QQ.Production.Intraday.Infrastructure.Lmax;
using QQ.Production.Intraday.Tools.LmaxReadOnlyActivation;
using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyActivationManualExecutionSurfaceTests
{
    private const string R61ApprovalPhrase =
        "I, Philippe, explicitly approve Phase LMAX-R61 for one temporary Demo read-only runtime market-data activation retry after the R60 approved manual execution surface for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority.";
    private const string R115ApprovalPhrase =
        "I, Philippe, explicitly approve Phase LMAX-R115 for one temporary QQ Workspace Demo read-only runtime market-data activation retry after the R114 FIX username/password tag binding fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority.";
    private const string R119ApprovalPhrase =
        "I, Philippe, explicitly approve Phase LMAX-R119 for one temporary QQ Workspace Demo read-only runtime market-data activation retry after the R117 FIX session identifier binding fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority.";
    private const string R123ApprovalPhrase =
        "I, Philippe, explicitly approve Phase LMAX-R123 for one temporary QQ Workspace Demo read-only runtime market-data activation retry after the R121 MarketDataRequest operation binding fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority.";
    private const string R127ApprovalPhrase =
        "I, Philippe, explicitly approve Phase LMAX-R127 for one temporary QQ Workspace Demo read-only runtime market-data activation retry after the R125 MarketDataResponse reader/parser binding fix for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority.";
    private const string R131ApprovalPhrase =
        "I, Philippe, explicitly approve Phase LMAX-R131 for one temporary QQ Workspace Demo weekday market-hours read-only runtime market-data activation retry after the R129 SessionReject reason enrichment and R130 readiness package for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, exactly one bounded attempt, and immediate abort authority.";
    private const string R137ApprovalPhrase =
        "I, Philippe, explicitly approve Phase LMAX-R137 for one temporary QQ Workspace Demo weekday market-hours read-only runtime market-data activation retry with sanitized SessionReject reason reporting after R133/R135/R136 readiness for GBPUSD, EURGBP, AUDUSD, and USDJPY with USDJPY caveat preserved, with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, exactly one bounded attempt, and immediate abort authority.";

    [Fact]
    public void R59_execute_once_not_invoked_blocker_is_resolved_for_next_retry()
    {
        var result = Surface().Validate(ValidCommand());

        Assert.True(result.Passed);
        Assert.True(result.ExecuteOnceNotInvokedByApprovedOperationalCallerInR59ResolvedForNextRetry);
        Assert.True(result.ApprovedManualExecutionSurfaceProvable);
        Assert.True(result.CallsManualOperationalCaller);
        Assert.True(result.CallOnceCallsInvokeOnce);
        Assert.True(result.InvokeOnceCallsExecuteOnce);
    }

    [Fact]
    public void Manual_execution_surface_calls_call_once_invoke_once_and_execute_once()
    {
        var result = Surface().ExecuteOnce(ValidCommand());

        Assert.True(result.Validation.Passed);
        Assert.True(result.CallOnceInvoked);
        Assert.True(result.InvokeOnceInvoked);
        Assert.True(result.ExecuteOnceInvoked);
        Assert.Equal(1, result.AttemptCount);
        Assert.False(result.Validation.ExternalBoundaryAttempted);
        Assert.False(result.Validation.CredentialValuesReturned);
    }

    [Fact]
    public void R61_root_cause_is_reproducible_default_surface_resolves_no_external_adapter()
    {
        var adapter = LmaxReadOnlyActivationManualExecutionSurfaceFactory.CreateAdapter(
            LmaxReadOnlyActivationManualExecutionSurfaceFactory.NoExternalBoundaryMode);

        Assert.IsType<LmaxReadOnlyActivationManualExecutionSurfaceNoExternalAdapter>(adapter);
    }

    [Fact]
    public void Next_retry_can_select_real_bounded_executable_readonly_adapter_without_executing_it()
    {
        var adapter = LmaxReadOnlyActivationManualExecutionSurfaceFactory.CreateAdapter(
            LmaxReadOnlyActivationManualExecutionSurfaceFactory.RealBoundedExecutableReadOnlyMode);
        var result = LmaxReadOnlyActivationManualExecutionSurfaceFactory
            .CreateForManualTool(LmaxReadOnlyActivationManualExecutionSurfaceFactory.RealBoundedExecutableReadOnlyMode)
            .Validate(ValidCommand());

        Assert.IsType<LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter>(adapter);
        Assert.True(result.Passed);
        Assert.False(result.ExternalBoundaryAttempted);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.TcpBoundary);
    }

    [Fact]
    public void R63_socket_connector_root_cause_is_reproduced_by_unbound_socket_client()
    {
        var result = new LmaxRealReadOnlySocketConnectionClient()
            .OpenTcp(ApprovedSocketOptions(), ApprovedRetryScope("LMAX-R63"));

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("SocketClientExecutionDependencyMissing", result.SanitizedStatus);
        Assert.Equal("SocketConnectorNotConfigured", result.SanitizedErrorCategory);
    }

    [Fact]
    public void R72_tls_progression_root_cause_is_reproduced_by_unbound_tls_client()
    {
        var result = new LmaxRealReadOnlyTlsHandshakeClient()
            .OpenTls(ApprovedTlsOptions(), ApprovedRetryScope("LMAX-R71"));

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("TlsClientExecutionDependencyMissing", result.SanitizedStatus);
        Assert.Equal("TlsHandshakeFactoryNotConfigured", result.SanitizedErrorCategory);
    }

    [Fact]
    public void R79_fix_progression_root_cause_is_reproduced_by_unbound_fix_client()
    {
        var result = new LmaxRealReadOnlyFixFrameClient()
            .OpenSessionLogon(ApprovedFixOptions(), ApprovedRetryScope("LMAX-R77"), ValidAccessRecord());

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("FixClientExecutionDependencyMissing", result.SanitizedStatus);
        Assert.Equal("FixSessionOperationNotConfigured", result.SanitizedErrorCategory);
    }

    [Fact]
    public void R82_fix_credential_material_root_cause_is_reproduced_by_validation_only_policy()
    {
        var access = LmaxCredentialConfigSourceBinding.CreateApprovedOperation(PassedCredentialBinding())(
            ApprovedCredentialOptions(),
            ApprovedRetryScope("LMAX-R81"),
            new LmaxReadOnlyCredentialAccessPolicy(
                FutureApprovedRuntimeAttemptRequired: true,
                RealSecretMaterialAllowedNow: false,
                RedactSensitiveFields: true,
                Environment: "Demo/read-only"),
            CancellationToken.None);

        Assert.True(access.AccessAllowed);
        Assert.False(access.RealSecretMaterialLoaded);
        Assert.False(access.SensitiveMaterialReturned);
        Assert.False(access.SensitiveMaterialPrinted);
        Assert.False(access.SensitiveMaterialStored);
        Assert.Equal("CredentialConfigSourceBindingApprovedNoSecretMaterialLoaded", access.SanitizedStatus);
    }

    [Fact]
    public void Approved_manual_real_bounded_path_enables_in_memory_fix_material_without_returning_values()
    {
        var phrase = ApprovalForPhase("LMAX-R83");
        var validation = LmaxReadOnlyActivationManualExecutionSurfaceFactory.ValidateFixCredentialMaterialBinding(
            "LMAX-R83",
            phrase,
            phrase);

        Assert.True(validation.ExactPerPhaseOperatorApprovalPresent);
        Assert.True(validation.RetryPhaseReserved);
        Assert.True(validation.ManualCliRequired);
        Assert.True(validation.ManualRealBoundedPathOnly);
        Assert.True(validation.ApprovedDemoReadOnlyInMemoryFixLogonCredentialMaterialReady);
        Assert.True(validation.RealSecretMaterialAllowedForApprovedManualRetry);
        Assert.True(validation.RealSecretMaterialLoadedInMemoryForFutureAttempt);
        Assert.False(validation.CredentialValuesReturned);
        Assert.False(validation.SensitiveMaterialReturned);
        Assert.False(validation.SensitiveMaterialPrinted);
        Assert.False(validation.SensitiveMaterialStored);
        Assert.False(validation.SensitiveMaterialSerialized);
        Assert.True(validation.ProductionAccountConfigExcluded);
        Assert.False(validation.ApiWorkerReachable);
        Assert.True(validation.MarketDataRequestBlockedUntilFixSuccess);
        Assert.False(validation.ExternalBoundaryAttemptedDuringValidation);
        Assert.Empty(validation.Issues);
    }

    [Fact]
    public void Manual_fix_material_binding_requires_exact_approval_and_retry_phase()
    {
        var validation = LmaxReadOnlyActivationManualExecutionSurfaceFactory.ValidateFixCredentialMaterialBinding(
            "LMAX-R83",
            ApprovalForPhase("LMAX-R83"),
            "I approve a different phase.");

        Assert.False(validation.ExactPerPhaseOperatorApprovalPresent);
        Assert.False(validation.ApprovedDemoReadOnlyInMemoryFixLogonCredentialMaterialReady);
        Assert.False(validation.RealSecretMaterialAllowedForApprovedManualRetry);
        Assert.False(validation.RealSecretMaterialLoadedInMemoryForFutureAttempt);
        Assert.False(validation.CredentialValuesReturned);
        Assert.Contains("ExactPerPhaseOperatorApprovalMissing", validation.Issues);
    }

    [Fact]
    public void Manual_real_bounded_factory_binds_credential_operation_without_serializing_raw_material()
    {
        var root = FindRepoRoot();
        var factory = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "QQ.Production.Intraday.Tools.LmaxReadOnlyActivation",
            "LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs"));
        var json = JsonSerializer.Serialize(
            LmaxReadOnlyActivationManualExecutionSurfaceFactory.ValidateFixCredentialMaterialBinding(
                "LMAX-R83",
                ApprovalForPhase("LMAX-R83"),
                ApprovalForPhase("LMAX-R83")));

        Assert.Contains("new LmaxRealReadOnlyCredentialConfigClient(", factory, StringComparison.Ordinal);
        Assert.Contains("LmaxCredentialConfigSourceBinding.CreateApprovedOperation(CredentialBindingResult())", factory, StringComparison.Ordinal);
        Assert.Contains("RealSecretMaterialAllowedNow: true", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("password=", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fix-marketdata", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lmax.com", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Real_bounded_mode_binds_manual_tcp_socket_connector_without_executing_it()
    {
        var validation = LmaxReadOnlyActivationManualTcpSocketConnector.ValidateBinding();
        var endpoint = LmaxReadOnlyActivationManualDemoEndpointBinding.CreateApprovedDemoMarketData().Validate();
        var root = FindRepoRoot();
        var factory = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "QQ.Production.Intraday.Tools.LmaxReadOnlyActivation",
            "LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs"));
        var connector = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "QQ.Production.Intraday.Tools.LmaxReadOnlyActivation",
            "LmaxReadOnlyActivationManualTcpSocketConnector.cs"));

        Assert.True(validation.SocketClientExecutionDependencyMissingCleared);
        Assert.True(validation.SocketConnectorNotConfiguredCleared);
        Assert.True(validation.RealSocketConnectorBindingReady);
        Assert.True(validation.TlsClientExecutionDependencyMissingCleared);
        Assert.True(validation.TlsHandshakeFactoryNotConfiguredCleared);
        Assert.True(validation.ConcreteTlsHandshakeOperationBindingReady);
        Assert.True(validation.TcpToTlsContinuationBindingReady);
        Assert.True(validation.FixClientExecutionDependencyMissingCleared);
        Assert.True(validation.FixSessionOperationNotConfiguredCleared);
        Assert.True(validation.ConcreteFixSessionOperationBindingReady);
        Assert.True(validation.TlsToFixContinuationBindingReady);
        Assert.True(validation.ConcreteDemoEndpointHostBindingReady);
        Assert.True(validation.ConcreteDemoEndpointPortBindingReady);
        Assert.False(validation.TlsDefaultGlobal);
        Assert.False(validation.FixDefaultGlobal);
        Assert.True(validation.PlaceholderHostEliminatedForRealBoundedMode);
        Assert.True(endpoint.EndpointApproved);
        Assert.True(endpoint.HostConcreteBinding);
        Assert.False(endpoint.HostWasPlaceholder);
        Assert.True(endpoint.PortConcreteBinding);
        Assert.True(endpoint.ProductionExcluded);
        Assert.False(validation.RealSocketConnectorDefaultGlobal);
        Assert.True(validation.NoExternalDefaultPreserved);
        Assert.False(validation.ExternalBoundaryAttemptedDuringValidation);
        Assert.False(validation.CredentialValuesReturned);
        Assert.Contains("LmaxReadOnlyActivationManualDemoEndpointBinding.CreateApprovedDemoMarketData()", factory, StringComparison.Ordinal);
        Assert.Contains("new LmaxReadOnlyActivationManualTcpSocketConnector(endpointBinding)", factory, StringComparison.Ordinal);
        Assert.Contains("new LmaxRealReadOnlySocketConnectionClient(", factory, StringComparison.Ordinal);
        Assert.Contains("socketConnector.Connect", factory, StringComparison.Ordinal);
        Assert.Contains("new LmaxRealReadOnlyTlsHandshakeClient(", factory, StringComparison.Ordinal);
        Assert.Contains("socketConnector.AuthenticateTls", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("new LmaxRealReadOnlyTlsHandshakeClient();", factory, StringComparison.Ordinal);
        Assert.Contains("new LmaxRealReadOnlyFixFrameClient(", factory, StringComparison.Ordinal);
        Assert.Contains("socketConnector.OpenFixSession", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("new LmaxRealReadOnlyFixFrameClient();", factory, StringComparison.Ordinal);
        Assert.Contains("System.Net.Sockets", connector, StringComparison.Ordinal);
        Assert.Contains("TcpClient", connector, StringComparison.Ordinal);
        Assert.Contains("System.Net.Security", connector, StringComparison.Ordinal);
        Assert.Contains("SslStream", connector, StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_tls_continuation_requires_tcp_success_without_attempting_tls_in_validation()
    {
        var connector = new LmaxReadOnlyActivationManualTcpSocketConnector();

        var result = connector.AuthenticateTls(ApprovedTlsOptions(), ApprovedRetryScope("LMAX-R75"), CancellationToken.None);

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("ManualTlsHandshakeConnectorBlockedBeforeTlsUse", result.SanitizedStatus);
        Assert.Equal("TcpBoundaryNotOpened", result.SanitizedErrorCategory);
    }

    [Fact]
    public void Manual_fix_continuation_requires_tls_success_without_attempting_fix_in_validation()
    {
        var connector = new LmaxReadOnlyActivationManualTcpSocketConnector();

        var result = connector.OpenFixSession(
            ApprovedFixOptions(),
            ApprovedRetryScope("LMAX-R81"),
            ValidAccessRecord(),
            CancellationToken.None);

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("ManualFixSessionConnectorBlockedBeforeFixUse", result.SanitizedStatus);
        Assert.Equal("TlsBoundaryNotSucceeded", result.SanitizedErrorCategory);
    }

    [Fact]
    public void R86_fix_frame_write_root_cause_is_reproduced_from_archived_evidence()
    {
        var root = FindRepoRoot();
        var evidence = File.ReadAllText(Path.Combine(
            root,
            "artifacts",
            "readiness",
            "lmax-runtime-enablement",
            "phase-lmax-r86-fix-frame-write-root-cause.json"));

        Assert.Contains("FixFrameWriteNotImplemented", evidence, StringComparison.Ordinal);
        Assert.Contains("LmaxReadOnlyActivationManualTcpSocketConnector", evidence, StringComparison.Ordinal);
        Assert.Contains("OpenFixSession", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void Approved_manual_fix_logon_frame_builder_is_session_logon_only()
    {
        var validation = LmaxReadOnlyActivationManualFixLogonFrameBuilder.ValidateBinding();
        var frame = new LmaxReadOnlyActivationManualFixLogonFrameBuilder(SyntheticCredentialReader)
            .BuildLogonFrame(ApprovedFixOptions(), ApprovedRetryScope("LMAX-R87"), LoadedAccessRecord());
        var json = JsonSerializer.Serialize(validation);

        Assert.True(validation.FixLogonBuilderReady);
        Assert.True(validation.SessionOnly);
        Assert.True(validation.LogonOnly);
        Assert.Equal(["Logon"], validation.SupportedMessageCategories);
        Assert.False(validation.OrderFramesSupported);
        Assert.False(validation.NewOrderSingleSupported);
        Assert.False(validation.CancelReplaceSupported);
        Assert.False(validation.TradingMutationSupported);
        Assert.True(validation.UsesInMemoryCredentialMaterialOnly);
        Assert.True(validation.UsernameTagRequired);
        Assert.True(validation.PasswordTagRequired);
        Assert.True(validation.UsernamePasswordTagsBoundFromInMemoryCredentialMaterial);
        Assert.False(validation.RawFixSerialized);
        Assert.False(validation.RawCredentialsSerialized);
        Assert.False(validation.CredentialValuesReturned);
        Assert.True(frame.Built);
        Assert.NotEmpty(frame.FrameBytes);
        Assert.DoesNotContain("password=", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void R110_fix_logout_root_cause_is_reproduced_from_archived_evidence()
    {
        var root = FindRepoRoot();
        var evidence = File.ReadAllText(Path.Combine(
            root,
            "artifacts",
            "readiness",
            "lmax-runtime-enablement",
            "phase-lmax-r110-fix-session-boundary-root-cause-summary.json"));

        Assert.Contains("FIX Logon field/session parameter", evidence, StringComparison.Ordinal);
        Assert.Contains("LogoutReasonNotCaptured", evidence, StringComparison.Ordinal);
        Assert.Contains("LMAX_R110_PASS_FIX_LOGON_FIELD_OR_SESSION_PARAMETER_SUSPECT_NO_EXTERNAL_ACTIVATION", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_fix_logon_session_parameter_mapping_requires_support_verification_without_exposing_values()
    {
        var validation = LmaxReadOnlyActivationManualFixLogonSessionParameterMapping.Validate(
            ApprovedFixOptions(),
            ApprovedRetryScope("LMAX-R109"),
            LoadedAccessRecord(),
            SyntheticCredentialReader);
        var json = JsonSerializer.Serialize(validation);

        Assert.True(validation.FixLogonSessionParameterMappingReady);
        Assert.False(validation.SupportVerificationNeeded);
        Assert.True(validation.RequirementsIdentified);
        Assert.True(validation.CredentialMaterialAvailableInMemory);
        Assert.True(validation.SenderCompIdPresent);
        Assert.True(validation.TargetCompIdPresent);
        Assert.True(validation.SenderCompIdBoundFromCredentialMaterial);
        Assert.True(validation.TargetCompIdBoundFromApprovedDemoConfig);
        Assert.True(validation.SessionIdentifierBoundFromApprovedMaterial);
        Assert.True(validation.CredentialTagsMapped);
        Assert.False(validation.CredentialTagMappingRequiresSupportVerification);
        Assert.False(validation.ActualInMemoryFrameUsesSanitizedPlaceholderLabels);
        Assert.True(validation.BeginStringPresent);
        Assert.True(validation.BodyLengthPresent);
        Assert.True(validation.MsgTypeLogonPresent);
        Assert.True(validation.MsgSeqNumPresent);
        Assert.True(validation.SendingTimePresent);
        Assert.True(validation.EncryptMethodPresent);
        Assert.True(validation.HeartBtIntPresent);
        Assert.True(validation.ResetSeqNumFlagPresent);
        Assert.True(validation.CheckSumPresent);
        Assert.True(validation.SequenceResetPolicyReviewed);
        Assert.False(validation.SequenceResetPolicySuspectNotProven);
        Assert.True(validation.SessionLogonOnly);
        Assert.False(validation.OrderFramesSupported);
        Assert.False(validation.NewOrderSingleSupported);
        Assert.False(validation.CancelReplaceSupported);
        Assert.False(validation.ExecutionReportsSupported);
        Assert.False(validation.FillsSupported);
        Assert.False(validation.OrderLifecycleSupported);
        Assert.False(validation.RawFixSerialized);
        Assert.False(validation.RawCredentialsSerialized);
        Assert.False(validation.CredentialValuesReturned);
        Assert.True(validation.ProductionExcluded);
        Assert.True(validation.MarketDataRequestBlockedUntilFixSuccess);
        Assert.False(validation.ExternalBoundaryAttemptedDuringValidation);
        Assert.Equal("FixLogonSessionIdentifierBindingReadySanitized", validation.SanitizedStatus);
        Assert.DoesNotContain("password=", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DemoReadOnlySenderCompId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DemoReadOnlyTargetCompId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("r117-synthetic-sender", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("r117-synthetic-target", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void R116_root_cause_is_reproduced_from_archived_session_identifier_evidence()
    {
        var root = FindRepoRoot();
        var evidence = File.ReadAllText(Path.Combine(
            root,
            "artifacts",
            "readiness",
            "lmax-runtime-enablement",
            "phase-lmax-r116-fix-compid-session-identifier-review.json"));

        Assert.Contains("\"actualInMemoryFrameUsesSanitizedPlaceholderLabels\": true", evidence, StringComparison.Ordinal);
        Assert.Contains("\"compIdOrSessionIdentifierMismatchSuspected\": true", evidence, StringComparison.Ordinal);
        Assert.Contains("\"rawCompIdOrSessionIdentifiersSerialized\": false", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_fix_logon_builder_rejects_missing_session_identifier_material_without_placeholder_fallback()
    {
        var builder = new LmaxReadOnlyActivationManualFixLogonFrameBuilder(MissingSessionIdentifierCredentialReader);

        var frame = builder.BuildLogonFrame(
            ApprovedFixOptions(),
            ApprovedRetryScope("LMAX-R109"),
            LoadedAccessRecord());
        var json = JsonSerializer.Serialize(frame);

        Assert.False(frame.Built);
        Assert.Empty(frame.FrameBytes);
        Assert.Equal("SessionIdentifierCredentialMaterialMissing", frame.SanitizedErrorCategory);
        Assert.DoesNotContain("DemoReadOnlySenderCompId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DemoReadOnlyTargetCompId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void R114_market_data_logon_username_password_tags_are_bound_from_in_memory_material_without_serializing_values()
    {
        var validation = LmaxReadOnlyActivationManualFixLogonUsernamePasswordTagBinding.Validate(
            ApprovedFixOptions(),
            ApprovedRetryScope("LMAX-R109"),
            LoadedAccessRecord(),
            SyntheticCredentialReader);
        var json = JsonSerializer.Serialize(validation);

        Assert.True(validation.UsernameTagRequired);
        Assert.True(validation.PasswordTagRequired);
        Assert.True(validation.UsernameTagPresent);
        Assert.True(validation.PasswordTagPresent);
        Assert.True(validation.UsernameTagBoundFromApprovedInMemoryCredentialMaterial);
        Assert.True(validation.PasswordTagBoundFromApprovedInMemoryCredentialMaterial);
        Assert.True(validation.UsernamePasswordBindingReady);
        Assert.True(validation.ResetSeqNumFlagYPresent);
        Assert.True(validation.SessionLogonOnly);
        Assert.False(validation.OrderFramesSupported);
        Assert.False(validation.NewOrderSingleSupported);
        Assert.False(validation.CancelReplaceSupported);
        Assert.False(validation.RawFixSerialized);
        Assert.False(validation.RawCredentialsSerialized);
        Assert.False(validation.UsernameValueSerialized);
        Assert.False(validation.PasswordValueSerialized);
        Assert.False(validation.CredentialValuesReturned);
        Assert.True(validation.ProductionExcluded);
        Assert.True(validation.MarketDataRequestBlockedUntilLogonAck);
        Assert.False(validation.ExternalBoundaryAttemptedDuringValidation);
        Assert.DoesNotContain("r114-synthetic-user", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("r114-synthetic-pass", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("553=", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Approved_manual_fix_frame_writer_writes_only_to_in_memory_sink_in_r87_tests()
    {
        using var sink = new SyntheticFixSessionStream(SyntheticSessionFrame("A"));
        var writer = new LmaxReadOnlyActivationManualFixLogonFrameWriter(
            new LmaxReadOnlyActivationManualFixLogonFrameBuilder(SyntheticCredentialReader));

        var result = writer.WriteLogonFrame(
            sink,
            ApprovedFixOptions(),
            ApprovedRetryScope("LMAX-R87"),
            LoadedAccessRecord(),
            CancellationToken.None);
        var json = JsonSerializer.Serialize(result);

        Assert.True(sink.WrittenBytes.Length > 0);
        var written = System.Text.Encoding.ASCII.GetString(sink.WrittenBytes);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, result.Status);
        Assert.Equal("ManualFixSessionAcknowledgementSucceededSanitized", result.SanitizedStatus);
        Assert.Null(result.SanitizedErrorCategory);
        Assert.DoesNotContain("DemoReadOnlySenderCompId", written, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DemoReadOnlyTargetCompId", written, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password=", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DemoReadOnlySenderCompId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DemoReadOnlyTargetCompId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void R106_fix_session_acknowledgement_root_cause_is_reproduced_from_archived_evidence()
    {
        var root = FindRepoRoot();
        var evidence = File.ReadAllText(Path.Combine(
            root,
            "artifacts",
            "readiness",
            "lmax-runtime-enablement",
            "phase-lmax-r106-fix-session-acknowledgement-root-cause.json"));

        Assert.Contains("FixSessionAcknowledgementNotImplemented", evidence, StringComparison.Ordinal);
        Assert.Contains("LmaxReadOnlyActivationManualFixLogonFrameWriter", evidence, StringComparison.Ordinal);
        Assert.Contains("WriteLogonFrame", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void Approved_manual_fix_acknowledgement_reader_is_session_level_only()
    {
        var validation = LmaxReadOnlyActivationManualFixSessionAcknowledgementReader.ValidateBinding();
        var json = JsonSerializer.Serialize(validation);

        Assert.True(validation.FixSessionAcknowledgementBlockerCleared);
        Assert.True(validation.FixAcknowledgementReaderReady);
        Assert.True(validation.FixSessionParserClassifierReady);
        Assert.True(validation.SessionOnly);
        Assert.Contains("FixLogonAcknowledged", validation.SupportedSanitizedCategories);
        Assert.False(validation.OrderMessagesSupported);
        Assert.False(validation.ExecutionReportsSupported);
        Assert.False(validation.FillsSupported);
        Assert.False(validation.OrderLifecycleSupported);
        Assert.False(validation.NewOrderSingleSupported);
        Assert.False(validation.CancelReplaceSupported);
        Assert.False(validation.TradingMutationSupported);
        Assert.False(validation.RawFixSerialized);
        Assert.False(validation.CredentialValuesReturned);
        Assert.False(validation.ExternalBoundaryAttemptedDuringValidation);
        Assert.True(validation.MarketDataRequestBlockedUntilFixSuccess);
        Assert.DoesNotContain("password=", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Synthetic_logon_acknowledgement_classifies_as_fix_session_success_without_raw_serialization()
    {
        var reader = new LmaxReadOnlyActivationManualFixSessionAcknowledgementReader();

        var result = reader.Classify(SyntheticSessionFrame("A"));
        var json = JsonSerializer.Serialize(result);

        Assert.True(result.Succeeded);
        Assert.Equal("ManualFixSessionAcknowledgementSucceededSanitized", result.SanitizedStatus);
        Assert.Equal("FixLogonAcknowledged", result.SanitizedCategory);
        Assert.True(result.SessionOnly);
        Assert.False(result.OrderMessageSupported);
        Assert.False(result.RawFixSerialized);
        Assert.False(result.CredentialValuesReturned);
        Assert.DoesNotContain("35=", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("8=FIX", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("5", "FixLogoutReceived")]
    [InlineData("3", "FixSessionRejectReceived")]
    [InlineData("0", "FixSessionHeartbeatReceived")]
    [InlineData("1", "FixSessionTestRequestReceived")]
    [InlineData("", "FixReadRemoteClosed")]
    public void Non_success_session_frames_classify_as_sanitized_non_success_categories(
        string messageType,
        string expectedCategory)
    {
        var reader = new LmaxReadOnlyActivationManualFixSessionAcknowledgementReader();

        var result = reader.Classify(string.IsNullOrWhiteSpace(messageType) ? [] : SyntheticSessionFrame(messageType));

        Assert.False(result.Succeeded);
        Assert.Equal("ManualFixSessionAcknowledgementFailedSanitized", result.SanitizedStatus);
        Assert.Equal(expectedCategory, result.SanitizedCategory);
        Assert.True(result.SessionOnly);
        Assert.False(result.OrderMessageSupported);
        Assert.False(result.RawFixSerialized);
    }

    [Theory]
    [InlineData("D")]
    [InlineData("F")]
    [InlineData("G")]
    [InlineData("H")]
    [InlineData("8")]
    [InlineData("AE")]
    [InlineData("AD")]
    public void Ack_reader_rejects_order_execution_and_marketdata_message_types(string messageType)
    {
        var reader = new LmaxReadOnlyActivationManualFixSessionAcknowledgementReader();

        var result = reader.Classify(SyntheticSessionFrame(messageType));

        Assert.False(result.Succeeded);
        Assert.Equal("FixMalformedSessionFrame", result.SanitizedCategory);
        Assert.True(result.SessionOnly);
        Assert.False(result.OrderMessageSupported);
        Assert.False(result.RawFixSerialized);
    }

    [Fact]
    public void Manual_fix_writer_does_not_fake_success_without_session_acknowledgement()
    {
        using var sink = new SyntheticFixSessionStream([]);
        var writer = new LmaxReadOnlyActivationManualFixLogonFrameWriter(
            new LmaxReadOnlyActivationManualFixLogonFrameBuilder(SyntheticCredentialReader));

        var result = writer.WriteLogonFrame(
            sink,
            ApprovedFixOptions() with { Timeout = TimeSpan.FromSeconds(1) },
            ApprovedRetryScope("LMAX-R105"),
            LoadedAccessRecord(),
            CancellationToken.None);

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.Failed, result.Status);
        Assert.Equal("FixReadRemoteClosed", result.SanitizedErrorCategory);
        Assert.True(sink.WrittenBytes.Length > 0);
    }

    [Fact]
    public void Manual_fix_writer_rejects_order_frame_categories()
    {
        using var sink = new MemoryStream();
        var writer = new LmaxReadOnlyActivationManualFixLogonFrameWriter();
        var options = ApprovedFixOptions() with
        {
            AllowedMessageTypes = ["Logon", "NewOrderSingle"]
        };

        var result = writer.WriteLogonFrame(
            sink,
            options,
            ApprovedRetryScope("LMAX-R87"),
            LoadedAccessRecord(),
            CancellationToken.None);

        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.Status);
        Assert.Equal("ManualFixFrameWriterConfigRejected", result.SanitizedStatus);
        Assert.Equal("ForbiddenFixMessageType", result.SanitizedErrorCategory);
        Assert.Equal(0, sink.Length);
    }

    [Fact]
    public void Manual_real_bounded_fix_writer_clears_frame_write_not_implemented_without_global_default()
    {
        var validation = LmaxReadOnlyActivationManualFixLogonFrameWriter.ValidateBinding();
        var connectorValidation = LmaxReadOnlyActivationManualTcpSocketConnector.ValidateBinding();
        var root = FindRepoRoot();
        var connector = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "QQ.Production.Intraday.Tools.LmaxReadOnlyActivation",
            "LmaxReadOnlyActivationManualTcpSocketConnector.cs"));
        var api = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var worker = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));

        Assert.True(validation.FixFrameWriteBlockerCleared);
        Assert.True(validation.FixSessionAcknowledgementBlockerCleared);
        Assert.True(validation.FixLogonFrameBuilderReady);
        Assert.True(validation.FixFrameWriterReady);
        Assert.True(validation.FixAcknowledgementReaderReady);
        Assert.True(validation.FixSessionParserClassifierReady);
        Assert.True(validation.SessionOnly);
        Assert.True(validation.ReaderSessionOnly);
        Assert.False(validation.OrderFramesSupported);
        Assert.False(validation.NewOrderSingleSupported);
        Assert.False(validation.CancelReplaceSupported);
        Assert.False(validation.ExecutionReportsSupported);
        Assert.False(validation.FillsSupported);
        Assert.False(validation.OrderLifecycleSupported);
        Assert.False(validation.RawFixSerialized);
        Assert.False(validation.RawCredentialsSerialized);
        Assert.False(validation.CredentialValuesReturned);
        Assert.False(validation.ExternalBoundaryAttemptedDuringValidation);
        Assert.True(validation.MarketDataRequestBlockedUntilFixSuccess);
        Assert.False(validation.ApiWorkerReachable);
        Assert.True(connectorValidation.FixFrameWriteBlockerCleared);
        Assert.True(connectorValidation.FixSessionAcknowledgementBlockerCleared);
        Assert.True(connectorValidation.FixLogonFrameBuilderReady);
        Assert.True(connectorValidation.FixFrameWriterReady);
        Assert.True(connectorValidation.FixAcknowledgementReaderReady);
        Assert.True(connectorValidation.FixSessionParserClassifierReady);
        Assert.True(connectorValidation.FixFrameWriterSessionLogonOnly);
        Assert.True(connectorValidation.FixAcknowledgementReaderSessionOnly);
        Assert.False(connectorValidation.OrderFramesSupported);
        Assert.False(connectorValidation.ExecutionReportsSupported);
        Assert.False(connectorValidation.FillsSupported);
        Assert.False(connectorValidation.OrderLifecycleSupported);
        Assert.False(connectorValidation.RawFixSerialized);
        Assert.Contains("LmaxReadOnlyActivationManualFixLogonFrameWriter", connector, StringComparison.Ordinal);
        Assert.DoesNotContain("FixFrameWriteNotImplemented", connector, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxReadOnlyActivationManualFixLogonFrameWriter", api, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxReadOnlyActivationManualFixLogonFrameWriter", worker, StringComparison.Ordinal);
    }

    [Fact]
    public void Transport_does_not_attempt_fix_when_tls_is_attempted_only()
    {
        var session = new TrackingSessionClient(
            new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, "TcpSucceeded", null, null),
            new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.Failed, "TlsBoundaryAttemptedSanitized", "TlsBoundaryAttemptedOnly", null),
            new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.Failed, "FixShouldNotRun", "UnexpectedFix", null));
        var transport = new LmaxRealReadOnlyMarketDataTransport(session);

        var result = transport.RunAsync(ApprovedRetryScope("LMAX-R81"));

        Assert.False(session.FixCalled);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.Failed, result.TlsBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.FixLogonBoundary);
        Assert.False(result.TlsEvidence.TlsSucceeded);
        Assert.Equal("AttemptedOnly", result.TlsEvidence.TlsResultCategory);
        Assert.False(result.TlsEvidence.TlsStreamAvailableForFix);
    }

    [Fact]
    public void Transport_attempts_fix_only_after_tls_succeeds()
    {
        var session = new TrackingSessionClient(
            new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, "TcpSucceeded", null, null),
            new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, "TlsSucceeded", null, null),
            new LmaxReadOnlyBoundaryStepResult(LmaxTemporaryReadOnlySessionBoundaryStatus.Failed, "FixBoundaryAttemptedSanitized", "FixCredentialMaterialUnavailable", null));
        var transport = new LmaxRealReadOnlyMarketDataTransport(session);

        var result = transport.RunAsync(ApprovedRetryScope("LMAX-R81"));

        Assert.True(session.FixCalled);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, result.TlsBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.Failed, result.FixLogonBoundary);
        Assert.True(result.TlsEvidence.TlsSucceeded);
        Assert.Equal("Succeeded", result.TlsEvidence.TlsResultCategory);
        Assert.True(result.TlsEvidence.TlsStreamAvailableForFix);
        Assert.False(session.MarketDataCalled);
    }

    [Theory]
    [InlineData("TlsHandshakeTimeout", "Timeout", true, "Timeout")]
    [InlineData("TlsHandshakeBoundaryFailed", "HandshakeException", false, "HandshakeException")]
    [InlineData("CertificateValidationFailure", "CertificateValidationFailure", false, "CertificateValidationFailure")]
    [InlineData("TcpBoundaryNotOpened", "StreamUnavailable", false, "StreamUnavailable")]
    [InlineData("TlsOperationCancelled", "CancelledOrAborted", false, "CancelledOrAborted")]
    [InlineData("TlsUnmappedFailure", "UnknownFailure", false, "UnknownFailure")]
    public void R91_tls_failure_categories_are_sanitized_and_distinct(
        string sourceCategory,
        string expectedCategory,
        bool expectedTimedOut,
        string expectedExceptionCategory)
    {
        var evidence = LmaxSanitizedTlsBoundaryClassifier.Classify(
            new LmaxReadOnlyBoundaryStepResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.Failed,
                "ManualTlsHandshakeConnectorFailedSanitized",
                sourceCategory,
                "Sanitized TLS boundary result."));
        var json = JsonSerializer.Serialize(evidence);

        Assert.True(evidence.TlsAttempted);
        Assert.False(evidence.TlsSucceeded);
        Assert.Equal(expectedCategory, evidence.TlsResultCategory);
        Assert.Equal(expectedTimedOut, evidence.TlsTimedOut);
        Assert.Equal(expectedExceptionCategory, evidence.TlsExceptionCategory);
        Assert.False(evidence.TlsStreamAvailableForFix);
        Assert.False(evidence.TlsRawMaterialSerialized);
        Assert.DoesNotContain("BEGIN", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("certificate dump", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void R91_manual_cli_emits_sanitized_tls_classification_fields_without_raw_material()
    {
        var root = FindRepoRoot();
        var program = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "QQ.Production.Intraday.Tools.LmaxReadOnlyActivation",
            "Program.cs"));
        var classifier = File.ReadAllText(Path.Combine(
            root,
            "src",
            "QQ.Production.Intraday.Infrastructure.Lmax",
            "LmaxSanitizedTlsBoundaryEvidence.cs"));

        Assert.Contains("tlsSucceeded=", program, StringComparison.Ordinal);
        Assert.Contains("tlsBoundaryStatus=", program, StringComparison.Ordinal);
        Assert.Contains("tlsResultCategory=", program, StringComparison.Ordinal);
        Assert.Contains("tlsFailureCategory=", program, StringComparison.Ordinal);
        Assert.Contains("tlsTimedOut=", program, StringComparison.Ordinal);
        Assert.Contains("tlsExceptionCategory=", program, StringComparison.Ordinal);
        Assert.Contains("tlsStreamAvailableForFix=", program, StringComparison.Ordinal);
        Assert.Contains("tlsRawMaterialSerialized=", program, StringComparison.Ordinal);
        Assert.Contains("AttemptedOnly", classifier, StringComparison.Ordinal);
        Assert.Contains("HandshakeException", classifier, StringComparison.Ordinal);
        Assert.Contains("CertificateValidationFailure", classifier, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoteCertificate", program + classifier, StringComparison.Ordinal);
        Assert.DoesNotContain("GetRawCertData", program + classifier, StringComparison.Ordinal);
    }

    [Fact]
    public void R66_placeholder_endpoint_label_is_eliminated_as_tcp_host_for_real_bounded_mode()
    {
        var root = FindRepoRoot();
        var connector = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "QQ.Production.Intraday.Tools.LmaxReadOnlyActivation",
            "LmaxReadOnlyActivationManualTcpSocketConnector.cs"));
        var endpoint = LmaxReadOnlyActivationManualDemoEndpointBinding.CreateApprovedDemoMarketData().Validate();

        Assert.True(endpoint.HostConcreteBinding);
        Assert.False(endpoint.HostWasPlaceholder);
        Assert.DoesNotContain("ConnectAsync(options.SanitizedEndpointLabel", connector, StringComparison.Ordinal);
        Assert.Contains("ConnectAsync(endpointBinding.RuntimeHost", connector, StringComparison.Ordinal);
    }

    [Fact]
    public void Demo_endpoint_binding_sanitized_validation_does_not_serialize_raw_endpoint_values()
    {
        var validation = LmaxReadOnlyActivationManualDemoEndpointBinding.CreateApprovedDemoMarketData().Validate();
        var json = JsonSerializer.Serialize(validation);

        Assert.True(validation.EndpointApproved);
        Assert.Equal("Demo", validation.EndpointMode);
        Assert.False(validation.RawHostSerialized);
        Assert.False(validation.RawPortSerialized);
        Assert.DoesNotContain("lmax.com", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fix-marketdata", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_endpoint_binding_is_rejected_without_opening_socket()
    {
        var ctor = typeof(LmaxReadOnlyActivationManualDemoEndpointBinding).GetConstructors(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Single();
        var binding = (LmaxReadOnlyActivationManualDemoEndpointBinding)ctor.Invoke(["fix-order.london-demo.lmax.com", 443]);
        var validation = binding.Validate();

        Assert.False(validation.EndpointApproved);
        Assert.False(validation.ProductionExcluded);
        Assert.False(validation.CredentialValuesReturned);
    }

    [Fact]
    public void No_external_default_remains_preserved_after_socket_connector_binding()
    {
        var defaultAdapter = LmaxReadOnlyActivationManualExecutionSurfaceFactory.CreateAdapter(
            LmaxReadOnlyActivationManualExecutionSurfaceFactory.NoExternalBoundaryMode);
        var realAdapter = LmaxReadOnlyActivationManualExecutionSurfaceFactory.CreateAdapter(
            LmaxReadOnlyActivationManualExecutionSurfaceFactory.RealBoundedExecutableReadOnlyMode);

        Assert.IsType<LmaxReadOnlyActivationManualExecutionSurfaceNoExternalAdapter>(defaultAdapter);
        Assert.IsType<LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter>(realAdapter);
    }

    [Fact]
    public void Real_bounded_adapter_is_not_the_global_default()
    {
        var defaultAdapter = LmaxReadOnlyActivationManualExecutionSurfaceFactory.CreateAdapter(
            LmaxReadOnlyActivationManualExecutionSurfaceFactory.NoExternalBoundaryMode);

        Assert.IsType<LmaxReadOnlyActivationManualExecutionSurfaceNoExternalAdapter>(defaultAdapter);
    }

    [Theory]
    [InlineData("")]
    [InlineData("live")]
    [InlineData("real")]
    [InlineData("prototype-lab")]
    public void Unapproved_adapter_modes_are_rejected(string adapterMode)
    {
        Assert.False(LmaxReadOnlyActivationManualExecutionSurfaceFactory.IsApprovedAdapterMode(adapterMode));
        Assert.Throws<ArgumentException>(() => LmaxReadOnlyActivationManualExecutionSurfaceFactory.CreateAdapter(adapterMode));
    }

    [Fact]
    public void Exact_per_phase_operator_approval_is_required()
    {
        var result = Surface().Validate(ValidCommand(operatorApprovalPhrase: "I approve something else."));

        Assert.False(result.Passed);
        Assert.False(result.ExactPerPhaseOperatorApprovalPresent);
        Assert.Contains(result.Issues, x => x.Code == "ExactPerPhaseOperatorApprovalMissing");
    }

    [Fact]
    public void R115_approval_phrase_allows_descriptive_username_password_tag_text_without_raw_secret_marker()
    {
        var result = Surface().Validate(ValidCommand("LMAX-R115", R115ApprovalPhrase));

        Assert.True(result.Passed);
        Assert.True(result.ExactPerPhaseOperatorApprovalPresent);
        Assert.True(result.RetryPhaseReserved);
    }

    [Fact]
    public void R119_approval_phrase_allows_session_identifier_binding_retry_without_raw_secret_marker()
    {
        var result = Surface().Validate(ValidCommand("LMAX-R119", R119ApprovalPhrase));

        Assert.True(result.Passed);
        Assert.True(result.ExactPerPhaseOperatorApprovalPresent);
        Assert.True(result.RetryPhaseReserved);
    }

    [Fact]
    public void R123_approval_phrase_allows_marketdata_request_operation_binding_retry_without_raw_secret_marker()
    {
        var result = Surface().Validate(ValidCommand("LMAX-R123", R123ApprovalPhrase));

        Assert.True(result.Passed);
        Assert.True(result.ExactPerPhaseOperatorApprovalPresent);
        Assert.True(result.RetryPhaseReserved);
    }

    [Fact]
    public void R127_approval_phrase_allows_marketdata_response_reader_parser_retry_without_raw_secret_marker()
    {
        var result = Surface().Validate(ValidCommand("LMAX-R127", R127ApprovalPhrase));

        Assert.True(result.Passed);
        Assert.True(result.ExactPerPhaseOperatorApprovalPresent);
        Assert.True(result.RetryPhaseReserved);
    }

    [Fact]
    public void R131_approval_phrase_allows_weekday_market_hours_retry_without_raw_secret_marker()
    {
        var result = Surface().Validate(ValidCommand("LMAX-R131", R131ApprovalPhrase));

        Assert.True(result.Passed);
        Assert.True(result.ExactPerPhaseOperatorApprovalPresent);
        Assert.True(result.RetryPhaseReserved);
    }

    [Fact]
    public void R137_approval_phrase_allows_weekday_market_hours_retry_with_sanitized_reason_reporting_without_raw_secret_marker()
    {
        var result = Surface().Validate(ValidCommand("LMAX-R137", R137ApprovalPhrase));

        Assert.True(result.Passed);
        Assert.True(result.ExactPerPhaseOperatorApprovalPresent);
        Assert.True(result.RetryPhaseReserved);
    }

    [Fact]
    public void Operator_approval_still_rejects_raw_password_markers()
    {
        var result = Surface().Validate(ValidCommand(
            "LMAX-R115",
            R115ApprovalPhrase + " password=not-allowed"));

        Assert.False(result.Passed);
        Assert.Contains(result.Issues, x => x.Code == "ExactPerPhaseOperatorApprovalMissing");
    }

    [Theory]
    [InlineData("LMAX-R60")]
    [InlineData("LMAX-R62")]
    [InlineData("LMAX-R101")]
    [InlineData("LMAX-R061")]
    [InlineData("LMAX-R61-extra")]
    [InlineData("NQ-R61")]
    public void Arbitrary_even_malformed_non_lmax_and_out_of_range_phases_are_rejected(string phase)
    {
        var result = Surface().Validate(ValidCommand(phase, ApprovalForPhase(phase)));

        Assert.False(result.Passed);
        Assert.False(result.RetryPhaseReserved);
        Assert.Contains(result.Issues, x => x.Code == "UnexpectedApprovedRetryPhase");
    }

    [Fact]
    public void Workspace_retry_phases_are_reserved_without_approving_decision_or_fix_phases()
    {
        var r105 = Surface().Validate(ValidCommand("LMAX-R105", ApprovalForPhase("LMAX-R105")));
        var r109 = Surface().Validate(ValidCommand("LMAX-R109", ApprovalForPhase("LMAX-R109")));
        var r115 = Surface().Validate(ValidCommand("LMAX-R115", ApprovalForPhase("LMAX-R115")));
        var r119 = Surface().Validate(ValidCommand("LMAX-R119", ApprovalForPhase("LMAX-R119")));
        var r123 = Surface().Validate(ValidCommand("LMAX-R123", ApprovalForPhase("LMAX-R123")));
        var r127 = Surface().Validate(ValidCommand("LMAX-R127", ApprovalForPhase("LMAX-R127")));
        var r131 = Surface().Validate(ValidCommand("LMAX-R131", ApprovalForPhase("LMAX-R131")));
        var r137 = Surface().Validate(ValidCommand("LMAX-R137", ApprovalForPhase("LMAX-R137")));

        Assert.True(r105.Passed);
        Assert.True(r105.RetryPhaseReserved);
        Assert.True(r109.Passed);
        Assert.True(r109.RetryPhaseReserved);
        Assert.True(r115.Passed);
        Assert.True(r115.RetryPhaseReserved);
        Assert.True(r119.Passed);
        Assert.True(r119.RetryPhaseReserved);
        Assert.True(r123.Passed);
        Assert.True(r123.RetryPhaseReserved);
        Assert.True(r127.Passed);
        Assert.True(r127.RetryPhaseReserved);
        Assert.True(r131.Passed);
        Assert.True(r131.RetryPhaseReserved);
        Assert.True(r137.Passed);
        Assert.True(r137.RetryPhaseReserved);
        Assert.False(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R101"));
        Assert.False(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R103"));
        Assert.False(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R107"));
        Assert.False(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R117"));
    }

    [Fact]
    public void Manual_confirmation_single_attempt_and_non_service_safety_flags_are_required()
    {
        var result = Surface().Validate(ValidCommand(
            manualOperatorConfirmation: false,
            singleAttemptOnly: false,
            noServiceSchedulerPolling: false));

        Assert.False(result.Passed);
        Assert.False(result.ManualOnly);
        Assert.False(result.SingleAttemptOnly);
        Assert.False(result.NoLauncherServiceSchedulerPolling);
        Assert.Contains(result.Issues, x => x.Code == "ManualOperatorConfirmationMissing");
        Assert.Contains(result.Issues, x => x.Code == "SingleAttemptProofMissing");
        Assert.Contains(result.Issues, x => x.Code == "ServiceSchedulerPollingRisk");
    }

    [Fact]
    public void Api_worker_default_startup_cannot_reach_execution_surface()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var apiAppsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.DoesNotContain("QQ.Production.Intraday.Tools.LmaxReadOnlyActivation", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxReadOnlyActivationManualExecutionSurface", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxReadOnlyActivationManualExecutionSurface", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxReadOnlyActivationManualExecutionSurface", apiAppsettings, StringComparison.Ordinal);
        Assert.Contains("FakeLmaxGateway", apiProgram, StringComparison.Ordinal);
    }

    [Fact]
    public void Execution_surface_is_not_service_scheduler_polling_or_order_path()
    {
        var root = FindRepoRoot();
        var toolRoot = Path.Combine(root, "tools", "QQ.Production.Intraday.Tools.LmaxReadOnlyActivation");
        var program = File.ReadAllText(Path.Combine(toolRoot, "Program.cs"));
        var surface = File.ReadAllText(Path.Combine(toolRoot, "LmaxReadOnlyActivationManualExecutionSurface.cs"));

        Assert.Contains("LmaxManualBoundedReadOnlyActivationCaller", surface, StringComparison.Ordinal);
        Assert.Contains("CallOnce", surface, StringComparison.Ordinal);
        Assert.Contains("InvokeOnce", surface, StringComparison.Ordinal);
        Assert.Contains("ExecuteOnce", surface, StringComparison.Ordinal);
        Assert.Contains("--manual-confirm", program, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService", program + surface, StringComparison.Ordinal);
        Assert.DoesNotContain(": BackgroundService", program + surface, StringComparison.Ordinal);
        Assert.DoesNotContain("IHostedService", program + surface, StringComparison.Ordinal);
        Assert.DoesNotContain("while (true)", program + surface, StringComparison.Ordinal);
        Assert.DoesNotContain("Timer", program + surface, StringComparison.Ordinal);
        Assert.DoesNotContain("TcpClient", program + surface, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", program + surface, StringComparison.Ordinal);
        Assert.DoesNotContain("NewOrderSingle", program + surface, StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_cli_reports_sanitized_sessionreject_reason_category_without_raw_fix_or_reject_text()
    {
        var root = FindRepoRoot();
        var program = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "QQ.Production.Intraday.Tools.LmaxReadOnlyActivation",
            "Program.cs"));

        Assert.Contains("sessionRejectSanitizedReasonCategory", program, StringComparison.Ordinal);
        Assert.Contains("SanitizedSessionRejectReasonCategory", program, StringComparison.Ordinal);
        Assert.DoesNotContain("58=", program, StringComparison.Ordinal);
        Assert.DoesNotContain("reject text", program, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validation_does_not_attempt_credential_tcp_tls_fix_or_marketdata_boundaries()
    {
        var result = Surface().Validate(ValidCommand());

        Assert.True(result.Passed);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.CredentialConfigBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.TcpBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.TlsBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.FixLogonBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.MarketDataRequestBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.MarketDataResponseBoundary);
    }

    [Fact]
    public void Approved_instruments_and_usdjpy_caveat_are_preserved()
    {
        var result = Surface().Validate(ValidCommand());

        Assert.True(result.ApprovedInstrumentsExact);
        Assert.True(result.UsdJpyCaveatPreserved);
    }

    private static LmaxReadOnlyActivationManualExecutionSurface Surface()
        => LmaxReadOnlyActivationManualExecutionSurfaceFactory.CreateForManualTool();

    private static LmaxReadOnlyActivationManualExecutionSurfaceCommand ValidCommand(
        string phase = "LMAX-R61",
        string? operatorApprovalPhrase = null,
        bool manualOperatorConfirmation = true,
        bool singleAttemptOnly = true,
        bool noServiceSchedulerPolling = true)
        => new(
            phase,
            ApprovalForPhase(phase),
            operatorApprovalPhrase ?? ApprovalForPhase(phase),
            ExecuteOnceRequested: true,
            ManualOperatorConfirmation: manualOperatorConfirmation,
            SingleAttemptOnly: singleAttemptOnly,
            NoApiWorkerStartup: true,
            NoServiceSchedulerPolling: noServiceSchedulerPolling,
            NoOrderTradingPath: true,
            NoCredentialOutput: true);

    private static string ApprovalForPhase(string phase)
        => phase == "LMAX-R61"
            ? R61ApprovalPhrase
            : phase == "LMAX-R115"
                ? R115ApprovalPhrase
            : phase == "LMAX-R119"
                ? R119ApprovalPhrase
            : phase == "LMAX-R123"
                ? R123ApprovalPhrase
            : phase == "LMAX-R127"
                ? R127ApprovalPhrase
            : phase == "LMAX-R131"
                ? R131ApprovalPhrase
            : phase == "LMAX-R137"
                ? R137ApprovalPhrase
            : $"I, Philippe, explicitly approve Phase {phase} for one temporary Demo read-only runtime market-data activation retry with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority.";

    private static byte[] SyntheticSessionFrame(string messageType)
        => System.Text.Encoding.ASCII.GetBytes($"8=FIX.4.4\u00019=5\u000135={messageType}\u000110=000\u0001");

    private static LmaxReadOnlySocketConnectionOptions ApprovedSocketOptions()
        => new(
            "Demo/read-only",
            "DemoReadOnlyEndpoint",
            443,
            TimeSpan.FromSeconds(15),
            DemoReadOnly: true,
            ExternalConnectionExecutionApproved: true);

    private static LmaxReadOnlyTlsConnectionOptions ApprovedTlsOptions()
        => new(
            "Demo/read-only",
            "DemoReadOnlyEndpoint",
            "DemoReadOnlyTargetHost",
            TimeSpan.FromSeconds(15),
            DemoReadOnly: true,
            "SystemDefaultValidation",
            ExternalTlsHandshakeExecutionApproved: true);

    private static LmaxReadOnlyFixSessionOptions ApprovedFixOptions()
        => new(
            "Demo/read-only",
            "DemoReadOnlySenderCompId",
            "DemoReadOnlyTargetCompId",
            30,
            TimeSpan.FromSeconds(15),
            DemoReadOnly: true,
            LmaxReadOnlyFixSessionOptions.DefaultAllowedReadOnlyMessageTypes,
            ExternalFixExecutionApproved: true);

    private static LmaxReadOnlyCredentialConfigOptions ApprovedCredentialOptions()
        => new(
            "Demo/read-only",
            DemoReadOnly: true,
            "DemoReadOnlyConfigSource",
            ExternalCredentialAccessApproved: true);

    private static LmaxReadOnlyCredentialSanitizationRecord ValidAccessRecord()
        => new(
            AccessPolicyAccepted: true,
            RealSecretMaterialLoaded: false,
            SensitiveMaterialReturned: false,
            SensitiveMaterialPrinted: false,
            SensitiveMaterialStored: false,
            "CredentialBoundaryPolicyAcceptedNoSecretMaterialLoaded");

    private static LmaxReadOnlyCredentialSanitizationRecord LoadedAccessRecord()
        => new(
            AccessPolicyAccepted: true,
            RealSecretMaterialLoaded: true,
            SensitiveMaterialReturned: false,
            SensitiveMaterialPrinted: false,
            SensitiveMaterialStored: false,
            "CredentialConfigSourceLoadApprovedValuesNotReturned");

    private static string? SyntheticCredentialReader(string key)
        => key switch
        {
            "LMAX_DEMO_SENDER_COMP_ID" => "r117-synthetic-sender",
            "LMAX_DEMO_TARGET_COMP_ID" => "r117-synthetic-target",
            "LMAX_DEMO_FIX_USERNAME" => "r114-synthetic-user",
            "LMAX_DEMO_FIX_PASSWORD" => "r114-synthetic-pass",
            _ => null
        };

    private static string? MissingSessionIdentifierCredentialReader(string key)
        => key switch
        {
            "LMAX_DEMO_FIX_USERNAME" => "r114-synthetic-user",
            "LMAX_DEMO_FIX_PASSWORD" => "r114-synthetic-pass",
            _ => null
        };

    private static LmaxCredentialConfigSourceBindingResult PassedCredentialBinding()
        => new(
            Passed: true,
            "CredentialConfigSourceBindingReadyNoExternalActivation",
            NoApprovedR51CredentialConfigOperationBindingForSecretValueLoad: false,
            ApprovedDemoReadOnlyCredentialConfigSourceBindingProvable: true,
            SourcePresent: true,
            SourceExplicitlyApprovedForBoundedReadOnlyActivation: true,
            SourceReachableOnlyThroughBoundedPath: true,
            SourceStructurallyLoadable: true,
            AdapterModeApprovedBoundedExecutableReadOnly: true,
            BoundedExecutorApproved: true,
            RuntimeDelegateBindingApproved: true,
            ApprovedInstrumentsExact: true,
            UsdJpyCaveatPreserved: true,
            ProductionAccountAllowedOrUsed: false,
            ApiWorkerStartupRequired: false,
            LiveLauncherRequired: false,
            HostedBackgroundServiceRequired: false,
            SchedulerPollingRequired: false,
            OrderTradingPathReachable: false,
            CredentialValuesRead: false,
            CredentialValuesReturned: false,
            CredentialValuesPrinted: false,
            CredentialValuesStored: false,
            CredentialValuesSerialized: false,
            ExternalBoundaryAttempted: false,
            CredentialConfigBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            [new LmaxCredentialConfigRequiredFieldPresence("DemoReadOnlyProfilePresent", true)],
            []);

    private static LmaxTemporaryReadOnlyRuntimeActivationScope ApprovedRetryScope(string phase)
        => new(
            phase,
            "Demo",
            DemoReadOnly: true,
            Temporary: true,
            InertValidatorOnly: true,
            LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments,
            new LmaxReadOnlyRuntimeSafetyFlags(),
            new LmaxReadOnlyRuntimeOperatorApproval(
                "Philippe",
                new DateTimeOffset(2026, 05, 13, 13, 40, 00, TimeSpan.Zero),
                ApprovalForPhase(phase),
                phase,
                "Demo/read-only",
                LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol).ToList()),
            new LmaxReadOnlyRuntimeShutdownRevertRecord(
                PlanPresent: true,
                ShutdownRequiredAfterAttempt: true,
                RevertRequiredAfterAttempt: true,
                "artifacts/readiness/lmax-runtime-enablement/r64-test-shutdown-revert.json"),
            MaxRuntimeSeconds: 30,
            "artifacts/readiness/lmax-runtime-enablement");

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "QQ.Production.Intraday.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed class TrackingSessionClient(
        LmaxReadOnlyBoundaryStepResult tcpResult,
        LmaxReadOnlyBoundaryStepResult tlsResult,
        LmaxReadOnlyBoundaryStepResult fixResult) : ILmaxReadOnlyMarketDataSessionClient
    {
        public bool FixCalled { get; private set; }

        public bool MarketDataCalled { get; private set; }

        public LmaxReadOnlyBoundaryStepResult OpenReadOnlyTcpBoundary(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
            => tcpResult;

        public LmaxReadOnlyBoundaryStepResult OpenReadOnlyTlsBoundary(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
            => tlsResult;

        public LmaxReadOnlyBoundaryStepResult OpenReadOnlyFixLogonBoundary(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            FixCalled = true;
            return fixResult;
        }

        public LmaxReadOnlyMarketDataSessionClientResult RequestReadOnlyMarketData(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            MarketDataCalled = true;
            return new LmaxReadOnlyMarketDataSessionClientResult(
                [],
                "MarketDataShouldNotRun",
                "UnexpectedMarketData",
                null);
        }

        public bool ShutdownRevert() => true;
    }

    private sealed class SyntheticFixSessionStream(byte[] readBytes) : Stream
    {
        private readonly MemoryStream reads = new(readBytes);
        private readonly MemoryStream writes = new();

        public byte[] WrittenBytes => writes.ToArray();

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => writes.Length;

        public override long Position
        {
            get => writes.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => reads.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => writes.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
            => writes.Write(buffer, offset, count);
    }
}
