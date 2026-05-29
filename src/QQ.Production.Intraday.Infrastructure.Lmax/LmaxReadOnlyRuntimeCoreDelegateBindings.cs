using System.Reflection;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyRuntimeCoreDelegateValidationMode
{
    FinalProduction,
    LocalFakeTest
}

public sealed record LmaxReadOnlyRuntimeCoreDelegateDescriptor(
    string Name,
    string Operation,
    bool Present,
    bool DemoReadOnlyScoped,
    bool ReadOnly,
    bool SupportsSanitization,
    bool SupportsCancellationOrTimeout,
    bool TestOnly,
    bool ProductionAccountCapable,
    bool OrderTradingReplayCapable,
    bool RequiresApiWorkerStartup,
    bool RequiresLiveLauncher,
    bool RequiresHostedService);

public sealed record LmaxReadOnlyRuntimeCoreDelegateBindingSet(
    LmaxSocketConnectOperationCore? SocketConnect,
    LmaxTlsHandshakeOperationCore? TlsHandshake,
    LmaxFixSessionOperationCore? FixSession,
    LmaxMarketDataOperationCore? MarketData,
    LmaxCredentialConfigOperationCore? CredentialConfig,
    LmaxReadOnlyRuntimeCoreDelegateDescriptor SocketDescriptor,
    LmaxReadOnlyRuntimeCoreDelegateDescriptor TlsDescriptor,
    LmaxReadOnlyRuntimeCoreDelegateDescriptor FixDescriptor,
    LmaxReadOnlyRuntimeCoreDelegateDescriptor MarketDataDescriptor,
    LmaxReadOnlyRuntimeCoreDelegateDescriptor CredentialConfigDescriptor)
{
    public IReadOnlyList<LmaxReadOnlyRuntimeCoreDelegateDescriptor> Descriptors =>
    [
        SocketDescriptor,
        TlsDescriptor,
        FixDescriptor,
        MarketDataDescriptor,
        CredentialConfigDescriptor
    ];

    public static LmaxReadOnlyRuntimeCoreDelegateBindingSet CreateApproved(
        LmaxSocketConnectOperationCore socketConnect,
        LmaxTlsHandshakeOperationCore tlsHandshake,
        LmaxFixSessionOperationCore fixSession,
        LmaxMarketDataOperationCore marketData,
        LmaxCredentialConfigOperationCore credentialConfig)
        => new(
            socketConnect,
            tlsHandshake,
            fixSession,
            marketData,
            credentialConfig,
            Approved("LmaxApprovedSocketConnectRuntimeDelegate", "SocketConnect"),
            Approved("LmaxApprovedTlsHandshakeRuntimeDelegate", "TlsHandshake"),
            Approved("LmaxApprovedFixSessionRuntimeDelegate", "FixSession"),
            Approved("LmaxApprovedMarketDataRuntimeDelegate", "MarketData"),
            Approved("LmaxApprovedCredentialConfigRuntimeDelegate", "CredentialConfig"));

    public static LmaxReadOnlyRuntimeCoreDelegateBindingSet CreateLocalFake(
        LmaxSocketConnectOperationCore socketConnect,
        LmaxTlsHandshakeOperationCore tlsHandshake,
        LmaxFixSessionOperationCore fixSession,
        LmaxMarketDataOperationCore marketData,
        LmaxCredentialConfigOperationCore credentialConfig)
        => new(
            socketConnect,
            tlsHandshake,
            fixSession,
            marketData,
            credentialConfig,
            LocalFake("FakeSocketConnectRuntimeDelegate", "SocketConnect"),
            LocalFake("FakeTlsHandshakeRuntimeDelegate", "TlsHandshake"),
            LocalFake("FakeFixSessionRuntimeDelegate", "FixSession"),
            LocalFake("FakeMarketDataRuntimeDelegate", "MarketData"),
            LocalFake("FakeCredentialConfigRuntimeDelegate", "CredentialConfig"));

    private static LmaxReadOnlyRuntimeCoreDelegateDescriptor Approved(string name, string operation)
        => new(
            name,
            operation,
            Present: true,
            DemoReadOnlyScoped: true,
            ReadOnly: true,
            SupportsSanitization: true,
            SupportsCancellationOrTimeout: true,
            TestOnly: false,
            ProductionAccountCapable: false,
            OrderTradingReplayCapable: false,
            RequiresApiWorkerStartup: false,
            RequiresLiveLauncher: false,
            RequiresHostedService: false);

    private static LmaxReadOnlyRuntimeCoreDelegateDescriptor LocalFake(string name, string operation)
        => Approved(name, operation) with { TestOnly = true };
}

public sealed class LmaxReadOnlyRuntimeCoreDelegateBindingFactory
{
    private readonly LmaxReadOnlyRuntimeCoreDelegateBindingSet bindings;
    private readonly LmaxReadOnlyRuntimeCoreDelegateValidationMode validationMode;

    public LmaxReadOnlyRuntimeCoreDelegateBindingFactory(
        LmaxReadOnlyRuntimeCoreDelegateBindingSet bindings,
        LmaxReadOnlyRuntimeCoreDelegateValidationMode validationMode = LmaxReadOnlyRuntimeCoreDelegateValidationMode.FinalProduction)
    {
        this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        this.validationMode = validationMode;
    }

    public LmaxReadOnlyRuntimeCoreDelegateBindingResult Bind(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        bool boundedExecutorPresent,
        bool noApiWorkerWiring,
        bool noLiveLauncher,
        bool noHostedBackgroundService)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var delegateIssues = LmaxReadOnlyRuntimeCoreDelegateBindingValidator.ValidateDelegateBindings(
            bindings,
            scope,
            validationMode).ToList();
        if (delegateIssues.Count > 0)
        {
            return LmaxReadOnlyRuntimeCoreDelegateBindingResult.Rejected(delegateIssues);
        }

        var coreBindingSet = LmaxReadOnlyExecutionCoreBindingSet.CreateApproved(
            bindings.SocketConnect!,
            bindings.TlsHandshake!,
            bindings.FixSession!,
            bindings.MarketData!,
            bindings.CredentialConfig!);

        var composition = new LmaxReadOnlyExecutionCompositionRoot(coreBindingSet).Compose(
            scope,
            boundedExecutorPresent,
            noApiWorkerWiring,
            noLiveLauncher,
            noHostedBackgroundService);

        if (!composition.Passed)
        {
            return LmaxReadOnlyRuntimeCoreDelegateBindingResult.Rejected(composition.Issues);
        }

        return LmaxReadOnlyRuntimeCoreDelegateBindingResult.Bound(
            coreBindingSet,
            composition,
            bindings.Descriptors.Select(x => $"{x.Operation}:{x.Name}").ToList());
    }
}

public sealed record LmaxReadOnlyRuntimeCoreDelegateBindingResult(
    bool Passed,
    string Status,
    bool RuntimeDelegateBindingPassed,
    bool BoundedExecutorCompositionPassed,
    bool OperationToDelegateMappingExists,
    bool ContainsFakeOrTestOnlyDelegate,
    bool ApiWorkerStartupRequired,
    bool LiveLauncherRequired,
    bool HostedBackgroundServiceRequired,
    bool DefaultConfigChangeRequired,
    bool EvidenceSanitized,
    IReadOnlyList<string> MappingSummary,
    IReadOnlyList<string> Issues,
    LmaxReadOnlyExecutionCoreBindingSet? CoreBindingSet,
    LmaxReadOnlyExecutionCompositionResult? CompositionResult)
{
    public static LmaxReadOnlyRuntimeCoreDelegateBindingResult Bound(
        LmaxReadOnlyExecutionCoreBindingSet coreBindingSet,
        LmaxReadOnlyExecutionCompositionResult composition,
        IReadOnlyList<string> mappingSummary)
        => new(
            Passed: true,
            Status: "RuntimeCoreDelegateBindingsComplete",
            RuntimeDelegateBindingPassed: true,
            BoundedExecutorCompositionPassed: composition.BoundedExecutorCompositionPassed,
            OperationToDelegateMappingExists: true,
            ContainsFakeOrTestOnlyDelegate: false,
            ApiWorkerStartupRequired: false,
            LiveLauncherRequired: false,
            HostedBackgroundServiceRequired: false,
            DefaultConfigChangeRequired: false,
            EvidenceSanitized: true,
            MappingSummary: mappingSummary,
            Issues: [],
            CoreBindingSet: coreBindingSet,
            CompositionResult: composition);

    public static LmaxReadOnlyRuntimeCoreDelegateBindingResult Rejected(IReadOnlyList<string> issues)
        => new(
            Passed: false,
            Status: "RuntimeCoreDelegateBindingsRejected",
            RuntimeDelegateBindingPassed: false,
            BoundedExecutorCompositionPassed: false,
            OperationToDelegateMappingExists: false,
            ContainsFakeOrTestOnlyDelegate: issues.Any(x => x.Contains("Fake", StringComparison.OrdinalIgnoreCase) || x.Contains("Test", StringComparison.OrdinalIgnoreCase)),
            ApiWorkerStartupRequired: issues.Any(x => x.Contains("ApiWorker", StringComparison.OrdinalIgnoreCase)),
            LiveLauncherRequired: issues.Any(x => x.Contains("LiveLauncher", StringComparison.OrdinalIgnoreCase)),
            HostedBackgroundServiceRequired: issues.Any(x => x.Contains("HostedService", StringComparison.OrdinalIgnoreCase)),
            DefaultConfigChangeRequired: false,
            EvidenceSanitized: true,
            MappingSummary: [],
            Issues: issues,
            CoreBindingSet: null,
            CompositionResult: null);
}

public static class LmaxReadOnlyRuntimeCoreDelegateBindingValidator
{
    private static readonly string[] ForbiddenPublicMethodTerms =
    [
        "Order",
        "Cancel",
        "TradeCapture",
        "OrderStatus",
        "ExecutionReport",
        "Replay",
        "ShadowReplay",
        "TradingMutation",
        "HostedService",
        "BackgroundWorker"
    ];

    public static LmaxReadOnlyFinalDelegateBindingCompletenessResult ValidateCompleteness(
        LmaxReadOnlyRuntimeCoreDelegateBindingSet bindings,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyRuntimeCoreDelegateValidationMode validationMode = LmaxReadOnlyRuntimeCoreDelegateValidationMode.FinalProduction)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(scope);

        var compositionCompleteness = LmaxReadOnlyExecutionCompositionValidator.Validate(
            LmaxReadOnlyExecutionCoreBindingSet.CreateApproved(
                bindings.SocketConnect ?? NotConfiguredSocket,
                bindings.TlsHandshake ?? NotConfiguredTls,
                bindings.FixSession ?? NotConfiguredFix,
                bindings.MarketData ?? NotConfiguredMarketData,
                bindings.CredentialConfig ?? NotConfiguredCredential),
            scope);

        var binding = new LmaxReadOnlyRuntimeCoreDelegateBindingFactory(bindings, validationMode).Bind(
            scope,
            boundedExecutorPresent: true,
            noApiWorkerWiring: true,
            noLiveLauncher: true,
            noHostedBackgroundService: true);
        var issues = new List<string>();
        issues.AddRange(ValidateDelegateBindings(bindings, scope, validationMode));
        issues.AddRange(ValidateReadOnlyPublicSurface());

        return new LmaxReadOnlyFinalDelegateBindingCompletenessResult(
            ProviderCompletenessPassed: compositionCompleteness.ProviderCompletenessPassed,
            ClientCompletenessPassed: compositionCompleteness.ClientCompletenessPassed,
            OperationCompletenessPassed: compositionCompleteness.OperationCompletenessPassed,
            CoreCompositionPassed: compositionCompleteness.RealCoreCompositionPassed,
            RuntimeDelegateBindingPassed: binding.Passed,
            BoundedExecutorCompositionPassed: binding.BoundedExecutorCompositionPassed,
            OperationToDelegateMappingExists: binding.OperationToDelegateMappingExists,
            NoFakeOrTestOnlyDelegate: !binding.ContainsFakeOrTestOnlyDelegate,
            NoApiWorkerStartupRequired: !binding.ApiWorkerStartupRequired,
            NoLiveLauncherRequired: !binding.LiveLauncherRequired,
            NoHostedBackgroundServiceRequired: !binding.HostedBackgroundServiceRequired,
            NoDefaultConfigChangeRequired: !binding.DefaultConfigChangeRequired,
            PublicSurfaceReadOnly: issues.Count == 0,
            EvidenceSanitized: binding.EvidenceSanitized,
            MappingSummary: binding.MappingSummary,
            Issues: issues);
    }

    public static IReadOnlyList<string> ValidateDelegateBindings(
        LmaxReadOnlyRuntimeCoreDelegateBindingSet bindings,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyRuntimeCoreDelegateValidationMode validationMode)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(scope);

        var issues = new List<string>();
        issues.AddRange(LmaxExecutableReadOnlyCredentialBoundary.ValidateScope(scope).Select(x => x.Code));
        ValidateDelegate(bindings.SocketConnect, bindings.SocketDescriptor, "SocketConnect", validationMode, issues);
        ValidateDelegate(bindings.TlsHandshake, bindings.TlsDescriptor, "TlsHandshake", validationMode, issues);
        ValidateDelegate(bindings.FixSession, bindings.FixDescriptor, "FixSession", validationMode, issues);
        ValidateDelegate(bindings.MarketData, bindings.MarketDataDescriptor, "MarketData", validationMode, issues);
        ValidateDelegate(bindings.CredentialConfig, bindings.CredentialConfigDescriptor, "CredentialConfig", validationMode, issues);
        return issues;
    }

    private static void ValidateDelegate(
        Delegate? runtimeDelegate,
        LmaxReadOnlyRuntimeCoreDelegateDescriptor descriptor,
        string expectedOperation,
        LmaxReadOnlyRuntimeCoreDelegateValidationMode validationMode,
        List<string> issues)
    {
        if (runtimeDelegate is null || !descriptor.Present)
        {
            issues.Add($"{expectedOperation}:MissingDelegate");
            return;
        }

        if (!string.Equals(descriptor.Operation, expectedOperation, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"{descriptor.Name}:OperationMismatch");
        }

        if (!descriptor.DemoReadOnlyScoped)
        {
            issues.Add($"{descriptor.Name}:NonDemoReadOnlyDelegate");
        }

        if (!descriptor.ReadOnly)
        {
            issues.Add($"{descriptor.Name}:NonReadOnlyDelegate");
        }

        if (!descriptor.SupportsSanitization)
        {
            issues.Add($"{descriptor.Name}:SanitizationMissing");
        }

        if (!descriptor.SupportsCancellationOrTimeout)
        {
            issues.Add($"{descriptor.Name}:CancellationOrTimeoutMissing");
        }

        if (descriptor.TestOnly &&
            validationMode == LmaxReadOnlyRuntimeCoreDelegateValidationMode.FinalProduction)
        {
            issues.Add($"{descriptor.Name}:FakeTestOnlyDelegateRejected");
        }

        if (descriptor.ProductionAccountCapable)
        {
            issues.Add($"{descriptor.Name}:ProductionDelegateRejected");
        }

        if (descriptor.OrderTradingReplayCapable)
        {
            issues.Add($"{descriptor.Name}:OrderTradingReplayDelegateRejected");
        }

        if (descriptor.RequiresApiWorkerStartup)
        {
            issues.Add($"{descriptor.Name}:ApiWorkerRequired");
        }

        if (descriptor.RequiresLiveLauncher)
        {
            issues.Add($"{descriptor.Name}:LiveLauncherRequired");
        }

        if (descriptor.RequiresHostedService)
        {
            issues.Add($"{descriptor.Name}:HostedServiceRequired");
        }
    }

    private static IReadOnlyList<string> ValidateReadOnlyPublicSurface()
    {
        var types = new[]
        {
            typeof(LmaxReadOnlyRuntimeCoreDelegateBindingSet),
            typeof(LmaxReadOnlyRuntimeCoreDelegateBindingFactory),
            typeof(LmaxReadOnlyRuntimeCoreDelegateBindingValidator),
            typeof(LmaxReadOnlyRuntimeCoreDelegateBindingResult)
        };

        var issues = new List<string>();
        foreach (var type in types)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (ForbiddenPublicMethodTerms.Any(term => method.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
                {
                    issues.Add($"{type.Name}:{method.Name}:ForbiddenPublicMethod");
                }
            }
        }

        return issues;
    }

    private static LmaxRealReadOnlyDependencyResult NotConfiguredSocket(
        LmaxReadOnlySocketConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken)
        => new(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, "SocketDelegateNotConfigured", "MissingDelegate", null);

    private static LmaxRealReadOnlyDependencyResult NotConfiguredTls(
        LmaxReadOnlyTlsConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken)
        => new(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, "TlsDelegateNotConfigured", "MissingDelegate", null);

    private static LmaxRealReadOnlyDependencyResult NotConfiguredFix(
        LmaxReadOnlyFixSessionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord accessRecord,
        CancellationToken cancellationToken)
        => new(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, "FixDelegateNotConfigured", "MissingDelegate", null);

    private static LmaxReadOnlyMarketDataSessionClientResult NotConfiguredMarketData(
        LmaxReadOnlyMarketDataRequestOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken)
        => new([], "MarketDataDelegateNotConfigured", "MissingDelegate", null);

    private static LmaxRealReadOnlySecretAccessResult NotConfiguredCredential(
        LmaxReadOnlyCredentialConfigOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialAccessPolicy policy,
        CancellationToken cancellationToken)
        => new(false, false, false, false, false, "CredentialDelegateNotConfigured", "MissingDelegate", null);
}

public sealed record LmaxReadOnlyFinalDelegateBindingCompletenessResult(
    bool ProviderCompletenessPassed,
    bool ClientCompletenessPassed,
    bool OperationCompletenessPassed,
    bool CoreCompositionPassed,
    bool RuntimeDelegateBindingPassed,
    bool BoundedExecutorCompositionPassed,
    bool OperationToDelegateMappingExists,
    bool NoFakeOrTestOnlyDelegate,
    bool NoApiWorkerStartupRequired,
    bool NoLiveLauncherRequired,
    bool NoHostedBackgroundServiceRequired,
    bool NoDefaultConfigChangeRequired,
    bool PublicSurfaceReadOnly,
    bool EvidenceSanitized,
    IReadOnlyList<string> MappingSummary,
    IReadOnlyList<string> Issues)
{
    public bool Passed =>
        ProviderCompletenessPassed &&
        ClientCompletenessPassed &&
        OperationCompletenessPassed &&
        CoreCompositionPassed &&
        RuntimeDelegateBindingPassed &&
        BoundedExecutorCompositionPassed &&
        OperationToDelegateMappingExists &&
        NoFakeOrTestOnlyDelegate &&
        NoApiWorkerStartupRequired &&
        NoLiveLauncherRequired &&
        NoHostedBackgroundServiceRequired &&
        NoDefaultConfigChangeRequired &&
        PublicSurfaceReadOnly &&
        EvidenceSanitized &&
        Issues.Count == 0;
}
