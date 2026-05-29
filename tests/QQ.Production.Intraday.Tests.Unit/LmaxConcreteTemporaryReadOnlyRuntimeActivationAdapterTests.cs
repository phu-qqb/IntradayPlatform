using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapterTests
{
    [Fact]
    public void Valid_fake_transport_activation_succeeds_locally()
    {
        var transport = FakeTransport.Success();
        var adapter = new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(transport);

        var result = adapter.ValidateAsync(ValidRequest());

        Assert.True(result.Passed);
        Assert.Equal(LmaxTemporaryReadOnlyRuntimeActivationOutcome.DryRunAccepted, result.Outcome);
        Assert.True(result.HarnessOutputConsumed);
        Assert.True(result.HarnessPreflightPassed);
        Assert.True(result.ApprovedInstrumentsOnly);
        Assert.True(result.UsdJpyCaveatPreserved);
        Assert.Equal(1, transport.RunCount);
        Assert.Equal(1, transport.ShutdownRevertCount);
        Assert.False(result.SafetySnapshot.ExternalRunExecuted);
        Assert.False(result.SafetySnapshot.RealSocketOpened);
        Assert.False(result.SafetySnapshot.CredentialsLoaded);
    }

    [Fact]
    public void Dry_run_only_r12_behavior_remains_available_for_inert_local_validation()
    {
        var transport = FakeTransport.Success();
        var adapter = new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(transport);

        var result = adapter.ValidateAsync(ValidRequest());

        Assert.Equal(LmaxTemporaryReadOnlyRuntimeAdapterMode.DryRunOnly, result.AdapterMode);
        Assert.True(result.DryRunOnly);
        Assert.True(result.FutureR10ApprovalRequired);
        Assert.True(result.Passed);
        Assert.False(result.SafetySnapshot.ExternalRunExecuted);
        Assert.Equal(1, transport.RunCount);
    }

    [Fact]
    public void Approved_bounded_executable_readonly_path_is_not_blocked_as_concrete_adapter_still_dryrun_only()
    {
        var transport = FakeTransport.Success();
        var adapter = new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(transport);

        var result = adapter.ValidateAsync(ValidRequest(
            adapterMode: LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
            requestedNextApprovalPhase: "LMAX-R43",
            boundedExecutorApproved: true,
            runtimeDelegateBindingApproved: true));

        Assert.Equal(LmaxTemporaryReadOnlyRuntimeActivationOutcome.BoundedExecutableReadOnlyAccepted, result.Outcome);
        Assert.False(result.DryRunOnly);
        Assert.False(result.FutureR10ApprovalRequired);
        Assert.DoesNotContain(result.Issues, x => x.Code is "RealTransportNotAllowedInR12" or "ConcreteAdapterStillDryRunOnly");
        Assert.Equal(1, transport.RunCount);
        Assert.Equal(1, transport.ShutdownRevertCount);
        Assert.False(result.SafetySnapshot.ExternalRunExecuted);
        Assert.False(result.SafetySnapshot.RealSocketOpened);
        Assert.False(result.SafetySnapshot.TcpConnectionAttempted);
        Assert.False(result.SafetySnapshot.TlsHandshakeAttempted);
        Assert.False(result.SafetySnapshot.FixLogonAttempted);
        Assert.False(result.SafetySnapshot.MarketDataRequestSent);
    }

    [Theory]
    [InlineData("LMAX-R49")]
    [InlineData("LMAX-R51")]
    [InlineData("LMAX-R53")]
    [InlineData("LMAX-R55")]
    [InlineData("LMAX-R105")]
    [InlineData("LMAX-R109")]
    [InlineData("LMAX-R115")]
    [InlineData("LMAX-R119")]
    [InlineData("LMAX-R123")]
    [InlineData("LMAX-R127")]
    [InlineData("LMAX-R131")]
    [InlineData("LMAX-R137")]
    [InlineData("LMAX-R141")]
    [InlineData("LMAX-R147")]
    [InlineData("LMAX-R151")]
    [InlineData("LMAX-R155")]
    [InlineData("LMAX-R161")]
    [InlineData("LMAX-R167")]
    [InlineData("LMAX-R171")]
    [InlineData("LMAX-R175")]
    [InlineData("LMAX-R177")]
    [InlineData("LMAX-R181")]
    [InlineData("LMAX-R187")]
    [InlineData("LMAX-R191")]
    [InlineData("LMAX-R195")]
    [InlineData("LMAX-R199")]
    [InlineData("LMAX-R203")]
    [InlineData("LMAX-R207")]
    [InlineData("LMAX-R211")]
    [InlineData("LMAX-R215")]
    [InlineData("LMAX-R221")]
    public void Approved_bounded_executable_readonly_path_accepts_consolidated_explicit_retry_phase_reservations(string phase)
    {
        var transport = FakeTransport.Success();
        var adapter = new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(transport);

        var result = adapter.ValidateAsync(ValidRequest(
            adapterMode: LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
            requestedNextApprovalPhase: phase,
            boundedExecutorApproved: true,
            runtimeDelegateBindingApproved: true));

        Assert.Equal(LmaxTemporaryReadOnlyRuntimeActivationOutcome.BoundedExecutableReadOnlyAccepted, result.Outcome);
        Assert.DoesNotContain(result.Issues, x => x.Code == "UnexpectedExecutableApprovalPhase");
        Assert.Equal(1, transport.RunCount);
        Assert.False(result.SafetySnapshot.ExternalRunExecuted);
    }

    [Fact]
    public void Approved_bounded_executable_readonly_path_requires_bounded_executor_and_delegate_binding_approval()
    {
        var transport = FakeTransport.Success();
        var adapter = new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(transport);

        var result = adapter.ValidateAsync(ValidRequest(
            adapterMode: LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
            requestedNextApprovalPhase: "LMAX-R43"));

        Assert.Equal(LmaxTemporaryReadOnlyRuntimeActivationOutcome.SafetyConstraintFailed, result.Outcome);
        Assert.Contains(result.Issues, x => x.Code == "BoundedExecutorApprovalMissing");
        Assert.Contains(result.Issues, x => x.Code == "RuntimeDelegateBindingApprovalMissing");
        Assert.False(result.DryRunOnly);
        Assert.Equal(0, transport.RunCount);
        Assert.False(result.SafetySnapshot.ExternalRunExecuted);
    }

    [Fact]
    public void Approved_bounded_executable_readonly_path_rejects_unexpected_followup_phase_before_transport()
    {
        var transport = FakeTransport.Success();
        var adapter = new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(transport);

        var result = adapter.ValidateAsync(ValidRequest(
            adapterMode: LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
            requestedNextApprovalPhase: "LMAX-R42",
            boundedExecutorApproved: true,
            runtimeDelegateBindingApproved: true));

        Assert.Contains(result.Issues, x => x.Code == "UnexpectedExecutableApprovalPhase");
        Assert.Equal(0, transport.RunCount);
        Assert.False(result.SafetySnapshot.ExternalRunExecuted);
    }

    [Fact]
    public void Approved_bounded_executable_retry_phase_reservations_reject_arbitrary_phase_names()
    {
        Assert.Contains("LMAX-R49", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R51", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R53", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R55", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R105", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R109", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R115", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R119", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R123", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R127", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R131", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R137", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R147", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R151", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R155", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R161", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R167", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R171", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R175", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R177", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R181", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R187", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R191", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R195", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R199", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R203", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R207", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R211", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R215", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.Contains("LMAX-R221", LmaxApprovedBoundedExecutableRetryPhaseReservations.ExplicitlyReservedPhases);
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R53"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R55"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R105"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R109"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R115"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R119"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R123"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R127"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R131"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R137"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R147"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R151"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R155"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R161"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R167"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R171"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R175"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R177"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R181"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R187"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R191"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R195"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R199"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R203"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R207"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R211"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R215"));
        Assert.True(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R221"));
        Assert.False(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R101"));
        Assert.False(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R103"));
        Assert.False(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R107"));
        Assert.False(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R117"));
        Assert.False(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R999"));
        Assert.False(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R51-extra"));
        Assert.False(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R54"));
        Assert.False(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("NQ-R55"));
        Assert.False(LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved("LMAX-R055"));
    }

    [Fact]
    public void Approved_instruments_only_are_passed_to_fake_transport_and_usdjpy_caveat_is_preserved()
    {
        var transport = FakeTransport.Success();
        var adapter = new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(transport);

        var result = adapter.ValidateAsync(ValidRequest());

        Assert.Equal(["GBPUSD", "EURGBP", "AUDUSD", "USDJPY"], transport.CapturedSymbols);
        var usdJpy = Assert.Single(result.InstrumentStatuses, x => x.Symbol == "USDJPY");
        Assert.Equal("4004", usdJpy.SecurityId);
        Assert.Equal("8", usdJpy.SecurityIdSource);
        Assert.Equal(LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, usdJpy.Caveat);
    }

    [Fact]
    public void Fake_success_boundaries_are_represented_in_sanitized_result()
    {
        var transport = FakeTransport.Success();
        var adapter = new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(transport);

        var result = adapter.ValidateAsync(ValidRequest());

        Assert.All(result.InstrumentStatuses, status =>
        {
            Assert.Equal(LmaxTemporaryReadOnlyRuntimeBoundaryStatus.NotApplicableForInertValidation, status.BoundaryStatus);
            Assert.Equal("FakeMarketDataStatusSucceededNoNetwork", status.SanitizedStatus);
            Assert.Null(status.SanitizedErrorCategory);
        });
    }

    [Theory]
    [InlineData("MissingHarnessValidation")]
    [InlineData("NonApprovedInstrument")]
    [InlineData("UsdJpyWithoutCaveat")]
    [InlineData("ProductionAccount")]
    [InlineData("OrdersEnabled")]
    [InlineData("LiveTradingEnabled")]
    [InlineData("SchedulerEnabled")]
    [InlineData("PollingEnabled")]
    [InlineData("ReplayEnabled")]
    [InlineData("ShadowReplayEnabled")]
    [InlineData("TradingMutationEnabled")]
    [InlineData("PersistentRuntimeEnablement")]
    [InlineData("DefaultGatewayRegistrationChange")]
    [InlineData("MissingShutdownRevertPlan")]
    public void Unsafe_scope_fails_before_fake_transport_is_invoked(string condition)
    {
        var transport = FakeTransport.Success();
        var adapter = new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(transport);

        var request = condition switch
        {
            "MissingHarnessValidation" => ValidRequest(approvalPhrase: "wrong template"),
            "NonApprovedInstrument" => ValidRequest(instruments: [.. LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments, new("EURUSD", "4001", "8", "prior_workflow_closed", false, null)]),
            "UsdJpyWithoutCaveat" => ValidRequest(instruments: LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol == "USDJPY" ? x with { Caveat = null } : x).ToList()),
            "ProductionAccount" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(ProductionAccountRequested: true)),
            "OrdersEnabled" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(AllowOrderSubmission: true)),
            "LiveTradingEnabled" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(AllowLiveTrading: true)),
            "SchedulerEnabled" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(SchedulerEnabled: true)),
            "PollingEnabled" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(PollingEnabled: true)),
            "ReplayEnabled" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(ReplayEnabled: true)),
            "ShadowReplayEnabled" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(ShadowReplayEnabled: true)),
            "TradingMutationEnabled" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(TradingMutationEnabled: true)),
            "PersistentRuntimeEnablement" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(PersistentRuntimeEnablementRequested: true)),
            "DefaultGatewayRegistrationChange" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(DefaultGatewayRegistrationChangeRequested: true)),
            "MissingShutdownRevertPlan" => ValidRequest(shutdownRevert: new LmaxReadOnlyRuntimeShutdownRevertRecord(false, true, true, "artifacts/readiness/lmax-runtime-enablement/missing.json")),
            _ => throw new InvalidOperationException(condition)
        };

        var result = adapter.ValidateAsync(request);

        Assert.False(result.Passed);
        Assert.Equal(LmaxTemporaryReadOnlyRuntimeActivationOutcome.SafetyConstraintFailed, result.Outcome);
        Assert.NotEmpty(result.Issues);
        Assert.Equal(0, transport.RunCount);
        Assert.Equal(0, transport.ShutdownRevertCount);
    }

    [Fact]
    public void Transport_failure_returns_sanitized_failure_result()
    {
        var transport = FakeTransport.Failure();
        var adapter = new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(transport);

        var result = adapter.ValidateAsync(ValidRequest());

        Assert.False(result.Passed);
        Assert.Equal(LmaxTemporaryReadOnlyRuntimeActivationOutcome.SafetyConstraintFailed, result.Outcome);
        Assert.Contains(result.Issues, x => x.Code == "ShutdownRevertNotCompleted");
        Assert.Contains(result.InstrumentStatuses, x => x.SanitizedErrorCategory == "FakeMarketDataBoundaryFailed");
        Assert.Equal(1, transport.RunCount);
        Assert.Equal(1, transport.ShutdownRevertCount);
    }

    [Fact]
    public void Real_transport_boundary_categories_are_preserved_as_sanitized_activation_evidence()
    {
        var transport = FakeTransport.FixFailure();
        var adapter = new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(transport);

        var result = adapter.ValidateAsync(ValidRequest(
            adapterMode: LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
            requestedNextApprovalPhase: "LMAX-R109",
            boundedExecutorApproved: true,
            runtimeDelegateBindingApproved: true));

        Assert.False(result.Passed);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, result.TcpBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, result.TlsBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.Failed, result.FixLogonBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.MarketDataBoundary);
        Assert.Equal("ManualFixSessionAcknowledgementFailedSanitized", result.FixBoundarySanitizedStatus);
        Assert.Equal("FixReadTimeout", result.FixBoundarySanitizedErrorCategory);
        Assert.Equal("ReadOnlyFixLogonBoundaryFailedSanitized", result.TransportSanitizedStatus);
        Assert.Equal("FixReadTimeout", result.TransportSanitizedErrorCategory);
        Assert.False(result.SafetySnapshot.MarketDataRequestSent);
    }

    [Fact]
    public void Real_bounded_executable_provider_stack_propagates_marketdata_state_fields_for_sanitized_session_reject()
    {
        var providers = new LmaxRealReadOnlyDependencyProviderFactory(
            new FakeRealSocketBoundaryProvider(),
            new FakeRealTlsBoundaryProvider(),
            new FakeRealFixBoundaryProvider(),
            new FakeRealMarketDataBoundaryProvider(),
            new FakeRealCredentialBoundaryProvider()).Create();
        var dependencies = providers.CreateLowLevelDependencySet();
        var sessionClient = dependencies.CreateSessionClient(new LmaxReadOnlyCredentialAccessPolicy(
            FutureApprovedRuntimeAttemptRequired: true,
            RealSecretMaterialAllowedNow: true,
            RedactSensitiveFields: true,
            Environment: "Demo/read-only"));
        var adapter = new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(new LmaxRealReadOnlyMarketDataTransport(sessionClient));

        var result = adapter.ValidateAsync(ValidRequest(
            adapterMode: LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
            requestedNextApprovalPhase: "LMAX-R171",
            boundedExecutorApproved: true,
            runtimeDelegateBindingApproved: true));

        Assert.True(result.SafetySnapshot.ExternalRunExecuted);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, result.TcpBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, result.TlsBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, result.FixLogonBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, result.MarketDataBoundary);
        Assert.Equal("SessionRejectObservedWithSanitizedReason", result.MarketDataBoundarySanitizedErrorCategory);
        Assert.True(result.MarketDataRequestWriteAttempted);
        Assert.True(result.MarketDataRequestWriteSucceeded);
        Assert.True(result.MarketDataRequestResponseReadAttempted);
        Assert.True(result.MarketDataRequestReachedBoundedResponseClassification);
        Assert.Equal("RejectReasonNotAvailable", result.MarketDataRejectSanitizedSubcategory);
        Assert.Equal("SessionRejectRefMsgTypeMarketDataRequest", result.SessionRejectSanitizedSubcategory);
        Assert.Equal("MsgType3Tags371372373", result.RejectReasonExtractionSource);
        Assert.Equal("RefTagID_MDUpdateType_265", result.SessionRejectRefTagIdSanitizedCategory);
        Assert.Equal("SessionRejectReason_ValueIncorrect", result.SessionRejectReasonSanitizedCategory);
        Assert.Equal("RefMsgType_MarketDataRequest", result.SessionRejectRefMsgTypeSanitizedCategory);
        Assert.False(result.MarketDataRequestSentLegacyFlag);
        Assert.True(result.SafetySnapshot.MarketDataRequestWriteAttempted);
        Assert.True(result.SafetySnapshot.MarketDataRequestWriteSucceeded);
        Assert.True(result.SafetySnapshot.MarketDataRequestResponseReadAttempted);
        Assert.True(result.SafetySnapshot.MarketDataRequestReachedBoundedResponseClassification);
        Assert.False(result.SafetySnapshot.MarketDataRequestSentLegacyFlag);
        Assert.False(result.SafetySnapshot.CredentialsPrinted);
        Assert.False(result.SafetySnapshot.CredentialsStored);
    }

    [Fact]
    public void Real_bounded_executable_full_provider_stack_preserves_state_fields_through_marketdata_frame_boundary_sanitizer()
    {
        var providers = new LmaxRealReadOnlyDependencyProviderFactory(
            new FakeRealSocketBoundaryProvider(),
            new FakeRealTlsBoundaryProvider(),
            new FakeRealFixBoundaryProvider(),
            new LmaxRealReadOnlyMarketDataFrameBoundaryProvider(
                new LmaxReadOnlyMarketDataRequestOptions(
                    "Demo/read-only",
                    DemoReadOnly: true,
                    "ReadOnlyMarketDataRequest",
                    "UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument",
                    TimeSpan.FromSeconds(15),
                    LmaxReadOnlyMarketDataRequestOptions.DefaultAllowedReadOnlyMessageTypes,
                    ExternalMarketDataRequestExecutionApproved: true),
                new FakeMarketDataFrameClient()),
            new FakeRealCredentialBoundaryProvider()).Create();
        var dependencies = providers.CreateLowLevelDependencySet();
        var sessionClient = dependencies.CreateSessionClient(new LmaxReadOnlyCredentialAccessPolicy(
            FutureApprovedRuntimeAttemptRequired: true,
            RealSecretMaterialAllowedNow: true,
            RedactSensitiveFields: true,
            Environment: "Demo/read-only"));
        var adapter = new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(new LmaxRealReadOnlyMarketDataTransport(sessionClient));

        var result = adapter.ValidateAsync(ValidRequest(
            adapterMode: LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
            requestedNextApprovalPhase: "LMAX-R177",
            boundedExecutorApproved: true,
            runtimeDelegateBindingApproved: true));

        Assert.True(result.SafetySnapshot.ExternalRunExecuted);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, result.TcpBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, result.TlsBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, result.FixLogonBoundary);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed, result.MarketDataBoundary);
        Assert.Equal("SessionRejectObservedWithSanitizedReason", result.MarketDataBoundarySanitizedErrorCategory);
        Assert.True(result.MarketDataRequestWriteAttempted);
        Assert.True(result.MarketDataRequestWriteSucceeded);
        Assert.True(result.MarketDataRequestResponseReadAttempted);
        Assert.True(result.MarketDataRequestReachedBoundedResponseClassification);
        Assert.Equal("RejectReasonNotAvailable", result.MarketDataRejectSanitizedSubcategory);
        Assert.Equal("SessionRejectRefMsgTypeMarketDataRequest", result.SessionRejectSanitizedSubcategory);
        Assert.Equal("MsgType3Tags371372373", result.RejectReasonExtractionSource);
        Assert.Equal("RefTagID_MDUpdateType_265", result.SessionRejectRefTagIdSanitizedCategory);
        Assert.Equal("SessionRejectReason_ValueIncorrect", result.SessionRejectReasonSanitizedCategory);
        Assert.Equal("RefMsgType_MarketDataRequest", result.SessionRejectRefMsgTypeSanitizedCategory);
        Assert.True(result.SafetySnapshot.MarketDataRequestWriteAttempted);
        Assert.True(result.SafetySnapshot.MarketDataRequestWriteSucceeded);
        Assert.True(result.SafetySnapshot.MarketDataRequestResponseReadAttempted);
        Assert.True(result.SafetySnapshot.MarketDataRequestReachedBoundedResponseClassification);
        Assert.False(result.SafetySnapshot.CredentialsPrinted);
        Assert.False(result.SafetySnapshot.CredentialsStored);
    }

    [Fact]
    public void Real_bounded_executable_pretransport_block_keeps_marketdata_state_fields_false()
    {
        var adapter = new LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter(FakeTransport.Success());

        var result = adapter.ValidateAsync(ValidRequest(
            adapterMode: LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
            requestedNextApprovalPhase: "LMAX-R171"));

        Assert.Contains(result.Issues, x => x.Code == "BoundedExecutorApprovalMissing");
        Assert.Contains(result.Issues, x => x.Code == "RuntimeDelegateBindingApprovalMissing");
        Assert.False(result.SafetySnapshot.ExternalRunExecuted);
        Assert.False(result.SafetySnapshot.MarketDataRequestWriteAttempted);
        Assert.False(result.SafetySnapshot.MarketDataRequestWriteSucceeded);
        Assert.False(result.SafetySnapshot.MarketDataRequestResponseReadAttempted);
        Assert.False(result.SafetySnapshot.MarketDataRequestReachedBoundedResponseClassification);
        Assert.False(result.SafetySnapshot.MarketDataRequestSentLegacyFlag);
    }

    [Fact]
    public void Concrete_adapter_source_has_no_credential_network_or_api_worker_wiring_dependency()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter.cs"));
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Net.Sockets", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new Socket", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NetworkStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CredentialProfileResolver", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SessionPassword", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxConcreteTemporaryReadOnlyRuntimeActivationAdapter", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Enabled\": true", appsettings, StringComparison.Ordinal);
    }

    private static LmaxTemporaryReadOnlyRuntimeActivationRequest ValidRequest(
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null,
        LmaxReadOnlyRuntimeShutdownRevertRecord? shutdownRevert = null,
        string? approvalPhrase = null,
        LmaxTemporaryReadOnlyRuntimeAdapterMode adapterMode = LmaxTemporaryReadOnlyRuntimeAdapterMode.DryRunOnly,
        string requestedNextApprovalPhase = "LMAX-R10",
        bool boundedExecutorApproved = false,
        bool runtimeDelegateBindingApproved = false)
    {
        var harness = LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 12, 17, 00, 00, TimeSpan.Zero),
            approvalPhrase ?? LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate,
            instruments,
            safetyFlags,
            shutdownRevert));

        return LmaxTemporaryReadOnlyRuntimeActivationRequest.FromHarnessResult(
            harness,
            new DateTimeOffset(2026, 05, 12, 20, 00, 00, TimeSpan.Zero),
            adapterMode,
            requestedNextApprovalPhase) with
            {
                BoundedExecutorApproved = boundedExecutorApproved,
                RuntimeDelegateBindingApproved = runtimeDelegateBindingApproved
            };
    }

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

    private sealed class FakeTransport : ILmaxTemporaryReadOnlyMarketDataTransport
    {
        private readonly bool success;
        private readonly bool fixFailure;

        private FakeTransport(bool success, bool fixFailure = false)
        {
            this.success = success;
            this.fixFailure = fixFailure;
        }

        public int RunCount { get; private set; }
        public int ShutdownRevertCount { get; private set; }
        public IReadOnlyList<string> CapturedSymbols { get; private set; } = [];

        public static FakeTransport Success() => new(success: true);

        public static FakeTransport Failure() => new(success: false);

        public static FakeTransport FixFailure() => new(success: false, fixFailure: true);

        public LmaxTemporaryReadOnlyTransportResult RunAsync(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunCount++;
            CapturedSymbols = scope.Instruments.Select(x => x.Symbol).ToList();

            if (fixFailure)
            {
                return new LmaxTemporaryReadOnlyTransportResult(
                    new DateTimeOffset(2026, 05, 12, 20, 00, 00, TimeSpan.Zero),
                    new DateTimeOffset(2026, 05, 12, 20, 00, 01, TimeSpan.Zero),
                    LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.Failed,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
                    [],
                    OutputSanitized: true,
                    CredentialsLoaded: true,
                    CredentialsPrinted: false,
                    CredentialsStored: false,
                    ShutdownRevertCompleted: true,
                    "ReadOnlyFixLogonBoundaryFailedSanitized",
                    "FixReadTimeout",
                    "Synthetic sanitized FIX acknowledgement timeout.") with
                    {
                        TcpBoundarySanitizedStatus = "ManualTcpSocketConnectorSucceededSanitized",
                        TlsBoundarySanitizedStatus = "ManualTlsHandshakeConnectorSucceededSanitized",
                        FixBoundarySanitizedStatus = "ManualFixSessionAcknowledgementFailedSanitized",
                        FixBoundarySanitizedErrorCategory = "FixReadTimeout",
                        MarketDataBoundarySanitizedStatus = "NotAttempted",
                        MarketDataBoundarySanitizedErrorCategory = "MarketDataNotAttempted"
                    };
            }

            var status = success
                ? LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded
                : LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed;
            var instruments = scope.Instruments.Select(x => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                x.Symbol,
                x.SecurityId,
                x.SecurityIdSource,
                status,
                success ? 1 : 0,
                MarketDataRequestRejectCount: 0,
                BusinessMessageRejectCount: 0,
                SessionRejectCount: 0,
                success ? "FakeMarketDataStatusSucceededNoNetwork" : "FakeMarketDataStatusFailedNoNetwork",
                success ? null : "FakeMarketDataBoundaryFailed",
                success ? null : "Fake transport failure for local-only test.",
                x.Caveat)).ToList();

            return new LmaxTemporaryReadOnlyTransportResult(
                new DateTimeOffset(2026, 05, 12, 20, 00, 00, TimeSpan.Zero),
                new DateTimeOffset(2026, 05, 12, 20, 00, 01, TimeSpan.Zero),
                status,
                status,
                status,
                status,
                instruments,
                OutputSanitized: true,
                CredentialsLoaded: false,
                CredentialsPrinted: false,
                CredentialsStored: false,
                ShutdownRevertCompleted: success,
                success ? "FakeTransportCompletedNoNetwork" : "FakeTransportFailedNoNetwork",
                success ? null : "FakeTransportFailure",
                success ? null : "Fake transport failure for local-only test.");
        }

        public void ShutdownRevert() => ShutdownRevertCount++;
    }

    private sealed class FakeRealSocketBoundaryProvider : ILmaxRealReadOnlySocketBoundaryProvider
    {
        public LmaxRealReadOnlyDependencyResult OpenTcp(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
            => new(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, "ManualTcpSocketConnectorSucceededSanitized");

        public bool ShutdownRevert() => true;
    }

    private sealed class FakeRealTlsBoundaryProvider : ILmaxRealReadOnlyTlsBoundaryProvider
    {
        public LmaxRealReadOnlyDependencyResult OpenTls(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
            => new(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, "ManualTlsHandshakeConnectorSucceededSanitized");
    }

    private sealed class FakeRealFixBoundaryProvider : ILmaxRealReadOnlyFixFrameBoundaryProvider
    {
        public LmaxRealReadOnlyDependencyResult OpenSessionLogon(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            LmaxReadOnlyCredentialSanitizationRecord accessRecord,
            CancellationToken cancellationToken = default)
            => new(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, "ManualFixFIX-sessionAcknowledgementSucceededSanitized");
    }

    private sealed class FakeRealMarketDataBoundaryProvider : ILmaxRealReadOnlyMarketDataFrameBoundaryProvider
    {
        public LmaxReadOnlyMarketDataSessionClientResult RequestReadOnlyStatus(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
            => new(
                scope.Instruments.Select(instrument => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                    instrument.Symbol,
                    instrument.SecurityId,
                    instrument.SecurityIdSource,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed,
                    MarketDataSnapshotCount: 0,
                    MarketDataRequestRejectCount: 0,
                    BusinessMessageRejectCount: 0,
                    SessionRejectCount: 1,
                    "ManualSessionRejectObservedWithSanitizedReason",
                    "SessionRejectObservedWithSanitizedReason",
                    "MalformedOrUnsupportedMarketDataRequestPlausible",
                    instrument.Caveat)).ToList(),
                "ManualSessionRejectObservedWithSanitizedReason",
                "SessionRejectObservedWithSanitizedReason",
                "MalformedOrUnsupportedMarketDataRequestPlausible")
            {
                MarketDataRequestWriteAttempted = true,
                MarketDataRequestWriteSucceeded = true,
                MarketDataRequestResponseReadAttempted = true,
                MarketDataRequestReachedBoundedResponseClassification = true,
                MarketDataRejectSanitizedSubcategory = "RejectReasonNotAvailable",
                SessionRejectSanitizedSubcategory = "SessionRejectRefMsgTypeMarketDataRequest",
                RejectReasonExtractionSource = "MsgType3Tags371372373",
                SessionRejectRefTagIdSanitizedCategory = "RefTagID_MDUpdateType_265",
                SessionRejectReasonSanitizedCategory = "SessionRejectReason_ValueIncorrect",
                SessionRejectRefMsgTypeSanitizedCategory = "RefMsgType_MarketDataRequest"
            };
    }

    private sealed class FakeMarketDataFrameClient : ILmaxReadOnlyMarketDataFrameClient
    {
        public LmaxReadOnlyMarketDataSessionClientResult RequestReadOnlyStatus(
            LmaxReadOnlyMarketDataRequestOptions options,
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            CancellationToken cancellationToken = default)
            => new(
                scope.Instruments.Select(instrument => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                    instrument.Symbol,
                    instrument.SecurityId,
                    instrument.SecurityIdSource,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed,
                    MarketDataSnapshotCount: 0,
                    MarketDataRequestRejectCount: 0,
                    BusinessMessageRejectCount: 0,
                    SessionRejectCount: 1,
                    "ManualSessionRejectObservedWithSanitizedReason",
                    "SessionRejectObservedWithSanitizedReason",
                    "MalformedOrUnsupportedMarketDataRequestPlausible",
                    instrument.Caveat)).ToList(),
                "ManualSessionRejectObservedWithSanitizedReason",
                "SessionRejectObservedWithSanitizedReason",
                "MalformedOrUnsupportedMarketDataRequestPlausible")
            {
                MarketDataRequestWriteAttempted = true,
                MarketDataRequestWriteSucceeded = true,
                MarketDataRequestResponseReadAttempted = true,
                MarketDataRequestReachedBoundedResponseClassification = true,
                MarketDataRejectSanitizedSubcategory = "RejectReasonNotAvailable",
                SessionRejectSanitizedSubcategory = "SessionRejectRefMsgTypeMarketDataRequest",
                RejectReasonExtractionSource = "MsgType3Tags371372373",
                SessionRejectRefTagIdSanitizedCategory = "RefTagID_MDUpdateType_265",
                SessionRejectReasonSanitizedCategory = "SessionRejectReason_ValueIncorrect",
                SessionRejectRefMsgTypeSanitizedCategory = "RefMsgType_MarketDataRequest"
            };

        public bool ShutdownRevert() => true;
    }

    private sealed class FakeRealCredentialBoundaryProvider : ILmaxRealReadOnlyCredentialConfigBoundaryProvider
    {
        public LmaxRealReadOnlySecretAccessResult AccessDemoReadOnlyConfig(
            LmaxTemporaryReadOnlyRuntimeActivationScope scope,
            LmaxReadOnlyCredentialAccessPolicy policy,
            CancellationToken cancellationToken = default)
            => new(
                AccessAllowed: true,
                RealSecretMaterialLoaded: true,
                SensitiveMaterialReturned: false,
                SensitiveMaterialPrinted: false,
                SensitiveMaterialStored: false,
                "CredentialConfigProviderAccessSanitized");
    }
}
