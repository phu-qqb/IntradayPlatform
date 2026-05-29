using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxCredentialConfigSourceBindingTests
{
    [Fact]
    public void R51_credential_config_binding_blocker_is_cleared_for_approved_demo_readonly_source()
    {
        var result = Validate(ValidRequest());

        Assert.True(result.Passed);
        Assert.False(result.NoApprovedR51CredentialConfigOperationBindingForSecretValueLoad);
        Assert.True(result.ApprovedDemoReadOnlyCredentialConfigSourceBindingProvable);
        Assert.True(result.SourcePresent);
        Assert.True(result.SourceStructurallyLoadable);
        Assert.False(result.CredentialValuesRead);
        Assert.False(result.CredentialValuesReturned);
    }

    [Fact]
    public void Approved_operation_binding_is_explicit_and_returns_no_credential_values()
    {
        var result = Validate(ValidRequest());
        var operation = new LmaxReadOnlyCredentialConfigOperationBinding(
            LmaxCredentialConfigSourceBinding.CreateApprovedOperation(result));

        var access = operation.Access(CredentialOptions(), ValidScope(), CredentialPolicy());

        Assert.True(access.AccessAllowed);
        Assert.False(access.RealSecretMaterialLoaded);
        Assert.False(access.SensitiveMaterialReturned);
        Assert.False(access.SensitiveMaterialPrinted);
        Assert.False(access.SensitiveMaterialStored);
        Assert.Contains("ValuesNotReturned", access.SanitizedErrorCategory, StringComparison.Ordinal);
        Assert.DoesNotContain("554=", access.SanitizedStatus, StringComparison.Ordinal);
        Assert.DoesNotContain("password", access.SanitizedStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_credential_config_source_is_rejected()
    {
        var result = Validate(ValidRequest(
            credentialOptions: new LmaxReadOnlyCredentialConfigOptions(
                "Production",
                DemoReadOnly: false,
                "ProductionConfigSource",
                ExternalCredentialAccessApproved: true),
            productionAccountForbidden: false,
            safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(ProductionAccountRequested: true)));

        Assert.False(result.Passed);
        Assert.True(result.ProductionAccountAllowedOrUsed);
        Assert.Contains(result.Issues, x => x.Code == "CredentialConfigSourceNotDemoReadOnly");
        Assert.Contains(result.Issues, x => x.Code == "ProductionAccountRisk");
    }

    [Fact]
    public void Credential_values_are_not_allowed_to_be_read_returned_printed_stored_or_serialized()
    {
        var result = Validate(ValidRequest(
            credentialValuesRead: true,
            credentialValuesReturned: true,
            credentialValuesPrinted: true,
            credentialValuesStored: true,
            credentialValuesSerialized: true));

        Assert.False(result.Passed);
        Assert.True(result.CredentialValuesRead);
        Assert.True(result.CredentialValuesReturned);
        Assert.True(result.CredentialValuesPrinted);
        Assert.True(result.CredentialValuesStored);
        Assert.True(result.CredentialValuesSerialized);
        Assert.Contains(result.Issues, x => x.Code == "CredentialValuesReturnedOrExposed");
    }

    [Fact]
    public void Binding_requires_the_bounded_executable_readonly_path()
    {
        var result = Validate(ValidRequest(
            adapterMode: LmaxTemporaryReadOnlyRuntimeAdapterMode.DryRunOnly,
            boundedExecutorApproved: false,
            runtimeDelegateBindingApproved: false));

        Assert.False(result.Passed);
        Assert.False(result.AdapterModeApprovedBoundedExecutableReadOnly);
        Assert.False(result.BoundedExecutorApproved);
        Assert.False(result.RuntimeDelegateBindingApproved);
        Assert.Contains(result.Issues, x => x.Code == "ApprovedBoundedExecutableReadOnlyModeMissing");
        Assert.Contains(result.Issues, x => x.Code == "BoundedExecutorApprovalMissing");
        Assert.Contains(result.Issues, x => x.Code == "RuntimeDelegateBindingApprovalMissing");
    }

    [Fact]
    public void Binding_is_not_reachable_outside_the_bounded_path_or_from_startup()
    {
        var result = Validate(ValidRequest(sourceReachableOnlyThroughBoundedPath: false));

        Assert.False(result.Passed);
        Assert.False(result.SourceReachableOnlyThroughBoundedPath);
        Assert.Contains(result.Issues, x => x.Code == "CredentialConfigSourceReachableOutsideBoundedPath");

        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));
        var workerPath = Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs");
        var workerProgram = File.Exists(workerPath) ? File.ReadAllText(workerPath) : string.Empty;

        Assert.DoesNotContain("LmaxCredentialConfigSourceBinding", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxCredentialConfigSourceBinding", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ApprovedBoundedExecutableReadOnly", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ApprovedBoundedExecutableReadOnly", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("phase-lmax-r52", appsettings, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FakeLmaxGateway", apiProgram, StringComparison.Ordinal);
    }

    [Fact]
    public void R50_phase_reservation_gate_remains_explicit_and_rejects_arbitrary_phases()
    {
        var result = Validate(ValidRequest(requestedNextApprovalPhase: "LMAX-R51"));
        var arbitrary = Validate(ValidRequest(requestedNextApprovalPhase: "LMAX-R999"));

        Assert.True(result.Passed);
        Assert.False(result.NoApprovedR51CredentialConfigOperationBindingForSecretValueLoad);
        Assert.False(arbitrary.Passed);
        Assert.True(arbitrary.NoApprovedR51CredentialConfigOperationBindingForSecretValueLoad);
        Assert.Contains(arbitrary.Issues, x => x.Code == "UnexpectedApprovedRetryPhase");
    }

    [Fact]
    public void Binding_does_not_attempt_credential_secret_read_or_external_boundaries()
    {
        var result = Validate(ValidRequest());

        Assert.True(result.Passed);
        Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, result.CredentialConfigBoundary);
        Assert.False(result.CredentialValuesRead);
        Assert.False(result.CredentialValuesReturned);
        Assert.False(result.ExternalBoundaryAttempted);
    }

    [Fact]
    public void Binding_blocks_forbidden_order_or_trading_scope()
    {
        var result = Validate(ValidRequest(
            safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(AllowOrderSubmission: true)));

        Assert.False(result.Passed);
        Assert.True(result.OrderTradingPathReachable);
        Assert.Contains(result.Issues, x => x.Code == "AllowOrderSubmission");
    }

    [Fact]
    public void UsdJpy_caveat_is_preserved_and_weakened_caveat_is_rejected()
    {
        var valid = Validate(ValidRequest());
        var instruments = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments
            .Select(x => x.Symbol == "USDJPY" ? x with { Caveat = null } : x)
            .ToList();
        var weakened = Validate(ValidRequest(instruments: instruments));

        Assert.True(valid.UsdJpyCaveatPreserved);
        Assert.True(valid.ApprovedInstrumentsExact);
        Assert.False(weakened.Passed);
        Assert.False(weakened.UsdJpyCaveatPreserved);
        Assert.Contains(weakened.Issues, x => x.Code == "UsdJpyCaveatMissing");
    }

    private static LmaxCredentialConfigSourceBindingResult Validate(
        LmaxCredentialConfigSourceBindingRequest request)
        => new LmaxCredentialConfigSourceBinding().Validate(request);

    private static LmaxCredentialConfigSourceBindingRequest ValidRequest(
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null,
        LmaxTemporaryReadOnlyRuntimeAdapterMode adapterMode = LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
        string requestedNextApprovalPhase = "LMAX-R51",
        bool boundedExecutorApproved = true,
        bool runtimeDelegateBindingApproved = true,
        LmaxReadOnlyCredentialConfigOptions? credentialOptions = null,
        LmaxReadOnlyCredentialAccessPolicy? credentialPolicy = null,
        bool sourceExplicitlyApproved = true,
        bool sourceReachableOnlyThroughBoundedPath = true,
        bool productionAccountForbidden = true,
        bool credentialValuesRead = false,
        bool credentialValuesReturned = false,
        bool credentialValuesPrinted = false,
        bool credentialValuesStored = false,
        bool credentialValuesSerialized = false,
        bool externalBoundaryAttempted = false)
    {
        var activationRequest = ValidActivationRequest(
            instruments,
            safetyFlags,
            adapterMode,
            requestedNextApprovalPhase,
            boundedExecutorApproved,
            runtimeDelegateBindingApproved);

        return new LmaxCredentialConfigSourceBindingRequest(
            activationRequest,
            credentialOptions ?? CredentialOptions(),
            credentialPolicy ?? CredentialPolicy(),
            LmaxReadOnlyCredentialProfileSourceKind.Environment,
            "DemoReadOnlyCredentialSourceBinding",
            LmaxReadOnlyCredentialRequiredKeyLabels.DemoReadOnlyEnvironmentLabels
                .Select(label => new LmaxCredentialConfigRequiredFieldPresence(label, Present: true))
                .ToList(),
            sourceExplicitlyApproved,
            sourceReachableOnlyThroughBoundedPath,
            boundedExecutorApproved,
            runtimeDelegateBindingApproved,
            NoApiWorkerStartupPath: true,
            NoLiveLauncher: true,
            NoHostedBackgroundService: true,
            NoSchedulerPolling: true,
            NoOrderTradingPath: true,
            productionAccountForbidden,
            credentialValuesRead,
            credentialValuesReturned,
            credentialValuesPrinted,
            credentialValuesStored,
            credentialValuesSerialized,
            externalBoundaryAttempted);
    }

    private static LmaxTemporaryReadOnlyRuntimeActivationRequest ValidActivationRequest(
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null,
        LmaxTemporaryReadOnlyRuntimeAdapterMode adapterMode = LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
        string requestedNextApprovalPhase = "LMAX-R51",
        bool boundedExecutorApproved = true,
        bool runtimeDelegateBindingApproved = true)
    {
        var harness = LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 13, 12, 00, 00, TimeSpan.Zero),
            LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate,
            instruments,
            safetyFlags));

        return LmaxTemporaryReadOnlyRuntimeActivationRequest.FromHarnessResult(
            harness,
            new DateTimeOffset(2026, 05, 13, 12, 05, 00, TimeSpan.Zero),
            adapterMode,
            requestedNextApprovalPhase) with
        {
            BoundedExecutorApproved = boundedExecutorApproved,
            RuntimeDelegateBindingApproved = runtimeDelegateBindingApproved
        };
    }

    private static LmaxTemporaryReadOnlyRuntimeActivationScope ValidScope()
        => ValidActivationRequest().HarnessResult.Scope;

    private static LmaxReadOnlyCredentialConfigOptions CredentialOptions()
        => new(
            "Demo/read-only",
            DemoReadOnly: true,
            "DemoReadOnlyCredentialSourceBinding",
            ExternalCredentialAccessApproved: true);

    private static LmaxReadOnlyCredentialAccessPolicy CredentialPolicy()
        => new(
            FutureApprovedRuntimeAttemptRequired: true,
            RealSecretMaterialAllowedNow: false,
            RedactSensitiveFields: true,
            Environment: "Demo/read-only");

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
}
