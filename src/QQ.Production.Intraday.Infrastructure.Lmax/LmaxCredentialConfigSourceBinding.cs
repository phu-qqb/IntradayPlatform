namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxCredentialConfigRequiredFieldPresence(
    string FieldLabel,
    bool Present);

public sealed record LmaxCredentialConfigSourceBindingRequest(
    LmaxTemporaryReadOnlyRuntimeActivationRequest ActivationRequest,
    LmaxReadOnlyCredentialConfigOptions CredentialConfigOptions,
    LmaxReadOnlyCredentialAccessPolicy CredentialAccessPolicy,
    LmaxReadOnlyCredentialProfileSourceKind SourceKind,
    string SanitizedSourceLabel,
    IReadOnlyList<LmaxCredentialConfigRequiredFieldPresence> RequiredFields,
    bool SourceExplicitlyApprovedForBoundedReadOnlyActivation,
    bool SourceReachableOnlyThroughBoundedPath,
    bool BoundedExecutorApproved,
    bool RuntimeDelegateBindingApproved,
    bool NoApiWorkerStartupPath,
    bool NoLiveLauncher,
    bool NoHostedBackgroundService,
    bool NoSchedulerPolling,
    bool NoOrderTradingPath,
    bool ProductionAccountForbidden,
    bool CredentialValuesRead = false,
    bool CredentialValuesReturned = false,
    bool CredentialValuesPrinted = false,
    bool CredentialValuesStored = false,
    bool CredentialValuesSerialized = false,
    bool ExternalBoundaryAttempted = false);

public sealed record LmaxCredentialConfigSourceBindingResult(
    bool Passed,
    string Status,
    bool NoApprovedR51CredentialConfigOperationBindingForSecretValueLoad,
    bool ApprovedDemoReadOnlyCredentialConfigSourceBindingProvable,
    bool SourcePresent,
    bool SourceExplicitlyApprovedForBoundedReadOnlyActivation,
    bool SourceReachableOnlyThroughBoundedPath,
    bool SourceStructurallyLoadable,
    bool AdapterModeApprovedBoundedExecutableReadOnly,
    bool BoundedExecutorApproved,
    bool RuntimeDelegateBindingApproved,
    bool ApprovedInstrumentsExact,
    bool UsdJpyCaveatPreserved,
    bool ProductionAccountAllowedOrUsed,
    bool ApiWorkerStartupRequired,
    bool LiveLauncherRequired,
    bool HostedBackgroundServiceRequired,
    bool SchedulerPollingRequired,
    bool OrderTradingPathReachable,
    bool CredentialValuesRead,
    bool CredentialValuesReturned,
    bool CredentialValuesPrinted,
    bool CredentialValuesStored,
    bool CredentialValuesSerialized,
    bool ExternalBoundaryAttempted,
    LmaxTemporaryReadOnlySessionBoundaryStatus CredentialConfigBoundary,
    IReadOnlyList<LmaxCredentialConfigRequiredFieldPresence> RequiredFields,
    IReadOnlyList<LmaxReadOnlyRuntimePreflightIssue> Issues);

public sealed class LmaxCredentialConfigSourceBinding
{
    private static readonly string[] UnsafeLabelTokens =
    [
        "://",
        "@",
        "password",
        "secret",
        "token",
        "554=",
        "-----BEGIN",
        "private"
    ];

    public LmaxCredentialConfigSourceBindingResult Validate(
        LmaxCredentialConfigSourceBindingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var issues = new List<LmaxReadOnlyRuntimePreflightIssue>();
        var activationRequest = request.ActivationRequest;
        var scope = activationRequest.HarnessResult.Scope;

        if (activationRequest.AdapterMode != LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly)
        {
            Add(issues, "ApprovedBoundedExecutableReadOnlyModeMissing", "$.activationRequest.adapterMode", "Credential/config source binding requires ApprovedBoundedExecutableReadOnly adapter mode.");
        }

        if (!request.BoundedExecutorApproved || !activationRequest.BoundedExecutorApproved)
        {
            Add(issues, "BoundedExecutorApprovalMissing", "$.boundedExecutorApproved", "Credential/config source binding requires bounded executor approval.");
        }

        if (!request.RuntimeDelegateBindingApproved || !activationRequest.RuntimeDelegateBindingApproved)
        {
            Add(issues, "RuntimeDelegateBindingApprovalMissing", "$.runtimeDelegateBindingApproved", "Credential/config source binding requires runtime delegate binding approval.");
        }

        if (!LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved(activationRequest.RequestedNextApprovalPhase))
        {
            Add(issues, "UnexpectedApprovedRetryPhase", "$.activationRequest.requestedNextApprovalPhase", "Credential/config source binding is reserved for an explicitly approved bounded retry phase.");
        }

        ValidateCredentialOptions(request, issues);
        ValidateRequiredFields(request.RequiredFields, issues);
        ValidateCommonSafety(request, scope, issues);

        var approvedInstrumentsExact = ApprovedInstrumentsExact(scope);
        if (!approvedInstrumentsExact)
        {
            Add(issues, "ApprovedInstrumentListMismatch", "$.scope.instruments", "Credential/config source binding requires exactly GBPUSD, EURGBP, AUDUSD, and USDJPY.");
        }

        var usdJpyCaveatPreserved = UsdJpyCaveatPreserved(scope);
        if (!usdJpyCaveatPreserved)
        {
            Add(issues, "UsdJpyCaveatMissing", "$.scope.instruments[USDJPY].caveat", "USDJPY caveat must be preserved exactly.");
        }

        issues.AddRange(LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope).Issues);

        var sourcePresent = request.RequiredFields.Count > 0 && request.RequiredFields.All(x => x.Present);
        var sourceStructurallyLoadable =
            sourcePresent &&
            request.SourceExplicitlyApprovedForBoundedReadOnlyActivation &&
            request.SourceReachableOnlyThroughBoundedPath &&
            request.CredentialConfigOptions.ExternalCredentialAccessApproved &&
            !request.CredentialValuesRead &&
            !request.CredentialValuesReturned &&
            !request.CredentialValuesPrinted &&
            !request.CredentialValuesStored &&
            !request.CredentialValuesSerialized;
        var passed = issues.Count == 0 && sourceStructurallyLoadable;

        return new LmaxCredentialConfigSourceBindingResult(
            Passed: passed,
            Status: passed
                ? "CredentialConfigSourceBindingReadyNoExternalActivation"
                : "CredentialConfigSourceBindingRejected",
            NoApprovedR51CredentialConfigOperationBindingForSecretValueLoad: !passed,
            ApprovedDemoReadOnlyCredentialConfigSourceBindingProvable: passed,
            SourcePresent: sourcePresent,
            SourceExplicitlyApprovedForBoundedReadOnlyActivation: request.SourceExplicitlyApprovedForBoundedReadOnlyActivation,
            SourceReachableOnlyThroughBoundedPath: request.SourceReachableOnlyThroughBoundedPath,
            SourceStructurallyLoadable: sourceStructurallyLoadable,
            AdapterModeApprovedBoundedExecutableReadOnly: activationRequest.AdapterMode == LmaxTemporaryReadOnlyRuntimeAdapterMode.ApprovedBoundedExecutableReadOnly,
            BoundedExecutorApproved: request.BoundedExecutorApproved && activationRequest.BoundedExecutorApproved,
            RuntimeDelegateBindingApproved: request.RuntimeDelegateBindingApproved && activationRequest.RuntimeDelegateBindingApproved,
            ApprovedInstrumentsExact: approvedInstrumentsExact,
            UsdJpyCaveatPreserved: usdJpyCaveatPreserved,
            ProductionAccountAllowedOrUsed: !request.ProductionAccountForbidden || scope.SafetyFlags.ProductionAccountRequested,
            ApiWorkerStartupRequired: !request.NoApiWorkerStartupPath,
            LiveLauncherRequired: !request.NoLiveLauncher,
            HostedBackgroundServiceRequired: !request.NoHostedBackgroundService,
            SchedulerPollingRequired: !request.NoSchedulerPolling || scope.SafetyFlags.SchedulerEnabled || scope.SafetyFlags.PollingEnabled,
            OrderTradingPathReachable: !request.NoOrderTradingPath || scope.SafetyFlags.AllowOrderSubmission || scope.SafetyFlags.AllowLiveTrading || scope.SafetyFlags.IsTradingEnabled,
            request.CredentialValuesRead,
            request.CredentialValuesReturned,
            request.CredentialValuesPrinted,
            request.CredentialValuesStored,
            request.CredentialValuesSerialized,
            request.ExternalBoundaryAttempted,
            CredentialConfigBoundary: LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            RequiredFields: request.RequiredFields,
            Issues: issues);
    }

    public static LmaxCredentialConfigOperationCore CreateApprovedOperation(
        LmaxCredentialConfigSourceBindingResult binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        return (options, scope, policy, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!binding.Passed)
            {
                return Rejected("CredentialConfigSourceBindingNotApproved", "Credential/config source binding proof is not approved.");
            }

            var issues = LmaxExecutableReadOnlyCredentialBoundary.ValidateScope(scope);
            if (issues.Count > 0)
            {
                return Rejected("SafetyConstraintFailed", string.Join("; ", issues.Select(x => x.Code)));
            }

            if (!options.DemoReadOnly ||
                !string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase) ||
                !options.ExternalCredentialAccessApproved ||
                !policy.FutureApprovedRuntimeAttemptRequired ||
                !policy.RedactSensitiveFields ||
                !string.Equals(policy.Environment, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
            {
                return Rejected("CredentialConfigOperationNotApproved", "Credential/config operation requires approved Demo/read-only options and redaction policy.");
            }

            return new LmaxRealReadOnlySecretAccessResult(
                AccessAllowed: true,
                RealSecretMaterialLoaded: policy.RealSecretMaterialAllowedNow,
                SensitiveMaterialReturned: false,
                SensitiveMaterialPrinted: false,
                SensitiveMaterialStored: false,
                policy.RealSecretMaterialAllowedNow
                    ? "CredentialConfigSourceLoadApprovedValuesNotReturned"
                    : "CredentialConfigSourceBindingApprovedNoSecretMaterialLoaded",
                "CredentialValuesNotReturned",
                null);
        };
    }

    private static void ValidateCredentialOptions(
        LmaxCredentialConfigSourceBindingRequest request,
        List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        var options = request.CredentialConfigOptions;
        var policy = request.CredentialAccessPolicy;

        if (!string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase) ||
            !options.DemoReadOnly ||
            !string.Equals(policy.Environment, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            Add(issues, "CredentialConfigSourceNotDemoReadOnly", "$.credentialConfigOptions", "Credential/config source must be Demo/read-only.");
        }

        if (!options.ExternalCredentialAccessApproved || !request.SourceExplicitlyApprovedForBoundedReadOnlyActivation)
        {
            Add(issues, "CredentialConfigSourceApprovalMissing", "$.credentialConfigOptions.externalCredentialAccessApproved", "Credential/config source must be explicitly approved for the bounded read-only path.");
        }

        if (!policy.FutureApprovedRuntimeAttemptRequired || !policy.RedactSensitiveFields)
        {
            Add(issues, "CredentialConfigPolicyNotSafe", "$.credentialAccessPolicy", "Credential/config policy must require future approval and redaction.");
        }

        if (policy.RealSecretMaterialAllowedNow)
        {
            Add(issues, "CredentialSecretMaterialLoadNotAllowedInR52", "$.credentialAccessPolicy.realSecretMaterialAllowedNow", "R52 may bind the source but must not perform a real secret-value read.");
        }

        if (UnsafeLabel(options.SanitizedConfigSourceLabel) || UnsafeLabel(request.SanitizedSourceLabel))
        {
            Add(issues, "CredentialConfigSourceLabelUnsafe", "$.sanitizedSourceLabel", "Credential/config source labels must be sanitized.");
        }

        if (request.SourceKind is LmaxReadOnlyCredentialProfileSourceKind.None)
        {
            Add(issues, "CredentialConfigSourceMissing", "$.sourceKind", "Credential/config source kind must be present.");
        }
    }

    private static void ValidateRequiredFields(
        IReadOnlyList<LmaxCredentialConfigRequiredFieldPresence> requiredFields,
        List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        if (requiredFields.Count == 0)
        {
            Add(issues, "CredentialConfigRequiredFieldsMissing", "$.requiredFields", "Credential/config source binding must include required field presence booleans.");
            return;
        }

        foreach (var field in requiredFields)
        {
            if (!KnownRequiredFieldLabel(field.FieldLabel) && UnsafeLabel(field.FieldLabel))
            {
                Add(issues, "CredentialConfigFieldLabelUnsafe", "$.requiredFields", "Credential/config required field labels must be sanitized.");
            }

            if (!field.Present)
            {
                Add(issues, "CredentialConfigRequiredFieldMissing", "$.requiredFields", "Credential/config required field is missing.");
            }
        }
    }

    private static void ValidateCommonSafety(
        LmaxCredentialConfigSourceBindingRequest request,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        List<LmaxReadOnlyRuntimePreflightIssue> issues)
    {
        if (!request.SourceReachableOnlyThroughBoundedPath)
        {
            Add(issues, "CredentialConfigSourceReachableOutsideBoundedPath", "$.sourceReachableOnlyThroughBoundedPath", "Credential/config source must only be reachable through the bounded path.");
        }

        if (!request.NoApiWorkerStartupPath)
        {
            Add(issues, "ApiWorkerStartupPathPresent", "$.noApiWorkerStartupPath", "Credential/config source binding must not require API/Worker startup.");
        }

        if (!request.NoLiveLauncher)
        {
            Add(issues, "LiveLauncherPresent", "$.noLiveLauncher", "Credential/config source binding must not create a live launcher.");
        }

        if (!request.NoHostedBackgroundService)
        {
            Add(issues, "HostedBackgroundServicePresent", "$.noHostedBackgroundService", "Credential/config source binding must not add a hosted/background service.");
        }

        if (!request.NoSchedulerPolling)
        {
            Add(issues, "SchedulerPollingPresent", "$.noSchedulerPolling", "Credential/config source binding must not require scheduler or polling.");
        }

        if (!request.NoOrderTradingPath)
        {
            Add(issues, "OrderTradingPathReachable", "$.noOrderTradingPath", "Credential/config source binding must not expose order or trading paths.");
        }

        if (!request.ProductionAccountForbidden || scope.SafetyFlags.ProductionAccountRequested)
        {
            Add(issues, "ProductionAccountRisk", "$.productionAccountForbidden", "Production account/config must remain forbidden.");
        }

        if (request.CredentialValuesRead ||
            request.CredentialValuesReturned ||
            request.CredentialValuesPrinted ||
            request.CredentialValuesStored ||
            request.CredentialValuesSerialized)
        {
            Add(issues, "CredentialValuesReturnedOrExposed", "$.credentialEvidence", "R52 must not read, return, print, store, or serialize credential values.");
        }

        if (request.ExternalBoundaryAttempted)
        {
            Add(issues, "ExternalBoundaryAttempted", "$.externalBoundaryAttempted", "R52 must not attempt external boundaries.");
        }
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

    private static bool UnsafeLabel(string value)
        => string.IsNullOrWhiteSpace(value) ||
           UnsafeLabelTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static bool KnownRequiredFieldLabel(string value)
        => LmaxReadOnlyCredentialRequiredKeyLabels.DemoReadOnlyEnvironmentLabels
            .Any(label => string.Equals(label, value, StringComparison.Ordinal));

    private static LmaxRealReadOnlySecretAccessResult Rejected(string category, string message)
        => new(
            AccessAllowed: false,
            RealSecretMaterialLoaded: false,
            SensitiveMaterialReturned: false,
            SensitiveMaterialPrinted: false,
            SensitiveMaterialStored: false,
            "CredentialConfigSourceOperationRejected",
            category,
            LmaxRealReadOnlyCredentialDependency.Sanitize(message));

    private static void Add(List<LmaxReadOnlyRuntimePreflightIssue> issues, string code, string path, string message)
        => issues.Add(new LmaxReadOnlyRuntimePreflightIssue(code, path, message));
}
