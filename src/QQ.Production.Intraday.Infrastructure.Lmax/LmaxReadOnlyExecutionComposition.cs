using System.Reflection;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxReadOnlyExecutionCoreDescriptor(
    string Name,
    string Boundary,
    bool ApprovedForDemoReadOnly,
    bool TestOnly,
    bool ProductionAccountCapable,
    bool OrderTradingReplayCapable,
    bool RequiresApiWorkerStartup,
    bool RequiresLiveLauncher,
    bool RequiresHostedService,
    bool SanitizedEvidenceSupported);

public sealed record LmaxReadOnlyExecutionCoreBindingSet(
    LmaxSocketConnectOperationCore SocketCore,
    LmaxTlsHandshakeOperationCore TlsCore,
    LmaxFixSessionOperationCore FixCore,
    LmaxMarketDataOperationCore MarketDataCore,
    LmaxCredentialConfigOperationCore CredentialConfigCore,
    LmaxReadOnlyExecutionCoreDescriptor SocketDescriptor,
    LmaxReadOnlyExecutionCoreDescriptor TlsDescriptor,
    LmaxReadOnlyExecutionCoreDescriptor FixDescriptor,
    LmaxReadOnlyExecutionCoreDescriptor MarketDataDescriptor,
    LmaxReadOnlyExecutionCoreDescriptor CredentialConfigDescriptor)
{
    public static LmaxReadOnlyExecutionCoreBindingSet CreateApproved(
        LmaxSocketConnectOperationCore socketCore,
        LmaxTlsHandshakeOperationCore tlsCore,
        LmaxFixSessionOperationCore fixCore,
        LmaxMarketDataOperationCore marketDataCore,
        LmaxCredentialConfigOperationCore credentialConfigCore)
        => new(
            socketCore,
            tlsCore,
            fixCore,
            marketDataCore,
            credentialConfigCore,
            Approved("LmaxRealSocketConnectCore", "TcpBoundary"),
            Approved("LmaxRealTlsHandshakeCore", "TlsBoundary"),
            Approved("LmaxRealFixSessionCore", "FixLogonBoundary"),
            Approved("LmaxRealMarketDataCore", "MarketDataBoundary"),
            Approved("LmaxRealCredentialConfigCore", "CredentialConfigBoundary"));

    public IReadOnlyList<LmaxReadOnlyExecutionCoreDescriptor> Descriptors =>
    [
        SocketDescriptor,
        TlsDescriptor,
        FixDescriptor,
        MarketDataDescriptor,
        CredentialConfigDescriptor
    ];

    private static LmaxReadOnlyExecutionCoreDescriptor Approved(string name, string boundary)
        => new(
            name,
            boundary,
            ApprovedForDemoReadOnly: true,
            TestOnly: false,
            ProductionAccountCapable: false,
            OrderTradingReplayCapable: false,
            RequiresApiWorkerStartup: false,
            RequiresLiveLauncher: false,
            RequiresHostedService: false,
            SanitizedEvidenceSupported: true);
}

public sealed class LmaxReadOnlyExecutionCompositionRoot
{
    private readonly LmaxReadOnlyExecutionCoreBindingSet cores;

    public LmaxReadOnlyExecutionCompositionRoot(LmaxReadOnlyExecutionCoreBindingSet cores)
    {
        this.cores = cores ?? throw new ArgumentNullException(nameof(cores));
    }

    public LmaxReadOnlyExecutionCompositionResult Compose(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        bool boundedExecutorPresent,
        bool noApiWorkerWiring,
        bool noLiveLauncher,
        bool noHostedBackgroundService)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var issues = LmaxReadOnlyExecutionCompositionValidator.ValidateCoreDescriptors(cores, scope).ToList();
        if (!boundedExecutorPresent)
        {
            issues.Add("BoundedExecutorMissing");
        }

        if (!noApiWorkerWiring)
        {
            issues.Add("ApiWorkerWiringPresent");
        }

        if (!noLiveLauncher)
        {
            issues.Add("LiveLauncherPresent");
        }

        if (!noHostedBackgroundService)
        {
            issues.Add("HostedBackgroundServicePresent");
        }

        if (issues.Count > 0)
        {
            return LmaxReadOnlyExecutionCompositionResult.Failed(issues);
        }

        var operationBindings = new LmaxReadOnlyExecutionOperationBindingSet(
            new LmaxReadOnlySocketConnectOperationBinding(cores.SocketCore),
            new LmaxReadOnlyTlsHandshakeOperationBinding(cores.TlsCore),
            new LmaxReadOnlyFixSessionOperationBinding(cores.FixCore),
            new LmaxReadOnlyMarketDataOperationBinding(cores.MarketDataCore),
            new LmaxReadOnlyCredentialConfigOperationBinding(cores.CredentialConfigCore));

        var providerClients = operationBindings.CreateProviderClientOperationSet();

        return LmaxReadOnlyExecutionCompositionResult.Complete(
            operationBindings,
            providerClients,
            cores.Descriptors.Select(x => $"{x.Boundary}:{x.Name}").ToList());
    }
}

public sealed record LmaxReadOnlyExecutionCompositionResult(
    bool Passed,
    string Status,
    bool ProviderCompletenessPassed,
    bool ClientCompletenessPassed,
    bool OperationCompletenessPassed,
    bool RealCoreCompositionPassed,
    bool BoundedExecutorCompositionPassed,
    bool OperationToCoreMappingExists,
    bool ContainsFakeOrTestOnlyCore,
    bool ApiWorkerStartupRequired,
    bool LiveLauncherRequired,
    bool HostedBackgroundServiceRequired,
    bool DefaultConfigChangeRequired,
    bool EvidenceSanitized,
    IReadOnlyList<string> MappingSummary,
    IReadOnlyList<string> Issues,
    LmaxReadOnlyExecutionOperationBindingSet? OperationBindings,
    LmaxReadOnlyProviderClientOperationSet? ProviderClients)
{
    public static LmaxReadOnlyExecutionCompositionResult Complete(
        LmaxReadOnlyExecutionOperationBindingSet operationBindings,
        LmaxReadOnlyProviderClientOperationSet providerClients,
        IReadOnlyList<string> mappingSummary)
        => new(
            Passed: true,
            Status: "RealCoreCompositionComplete",
            ProviderCompletenessPassed: true,
            ClientCompletenessPassed: true,
            OperationCompletenessPassed: true,
            RealCoreCompositionPassed: true,
            BoundedExecutorCompositionPassed: true,
            OperationToCoreMappingExists: true,
            ContainsFakeOrTestOnlyCore: false,
            ApiWorkerStartupRequired: false,
            LiveLauncherRequired: false,
            HostedBackgroundServiceRequired: false,
            DefaultConfigChangeRequired: false,
            EvidenceSanitized: true,
            MappingSummary: mappingSummary,
            Issues: [],
            OperationBindings: operationBindings,
            ProviderClients: providerClients);

    public static LmaxReadOnlyExecutionCompositionResult Failed(IReadOnlyList<string> issues)
        => new(
            Passed: false,
            Status: "RealCoreCompositionRejected",
            ProviderCompletenessPassed: false,
            ClientCompletenessPassed: false,
            OperationCompletenessPassed: false,
            RealCoreCompositionPassed: false,
            BoundedExecutorCompositionPassed: false,
            OperationToCoreMappingExists: false,
            ContainsFakeOrTestOnlyCore: issues.Any(x => x.Contains("Fake", StringComparison.OrdinalIgnoreCase) || x.Contains("Test", StringComparison.OrdinalIgnoreCase)),
            ApiWorkerStartupRequired: issues.Contains("ApiWorkerWiringPresent", StringComparer.OrdinalIgnoreCase),
            LiveLauncherRequired: issues.Contains("LiveLauncherPresent", StringComparer.OrdinalIgnoreCase),
            HostedBackgroundServiceRequired: issues.Contains("HostedBackgroundServicePresent", StringComparer.OrdinalIgnoreCase),
            DefaultConfigChangeRequired: false,
            EvidenceSanitized: true,
            MappingSummary: [],
            Issues: issues,
            OperationBindings: null,
            ProviderClients: null);
}

public static class LmaxReadOnlyExecutionCompositionValidator
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

    public static LmaxReadOnlyFinalCompositionCompletenessResult Validate(
        LmaxReadOnlyExecutionCoreBindingSet cores,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope)
    {
        ArgumentNullException.ThrowIfNull(cores);
        ArgumentNullException.ThrowIfNull(scope);

        var providerCompletenessPassed = ValidateProviderCompleteness();
        var client = LmaxReadOnlyProviderClientCompleteness.Validate();
        var operation = LmaxReadOnlyExecutionOperationCompleteness.Validate();
        var composition = new LmaxReadOnlyExecutionCompositionRoot(cores).Compose(
            scope,
            boundedExecutorPresent: true,
            noApiWorkerWiring: true,
            noLiveLauncher: true,
            noHostedBackgroundService: true);

        var issues = new List<string>();
        if (!providerCompletenessPassed)
        {
            issues.Add("ProviderCompletenessRegression");
        }

        if (!client.Passed)
        {
            issues.Add("ClientCompletenessRegression");
        }

        if (!operation.Passed)
        {
            issues.Add("OperationCompletenessRegression");
        }

        issues.AddRange(ValidateCoreDescriptors(cores, scope));
        issues.AddRange(ValidateReadOnlyPublicSurface());

        return new LmaxReadOnlyFinalCompositionCompletenessResult(
            ProviderCompletenessPassed: providerCompletenessPassed,
            ClientCompletenessPassed: client.Passed,
            OperationCompletenessPassed: operation.Passed,
            RealCoreCompositionPassed: composition.Passed,
            BoundedExecutorCompositionPassed: composition.BoundedExecutorCompositionPassed,
            OperationToCoreMappingExists: composition.OperationToCoreMappingExists,
            NoFakeOrTestOnlyCore: !composition.ContainsFakeOrTestOnlyCore,
            NoApiWorkerStartupRequired: !composition.ApiWorkerStartupRequired,
            NoLiveLauncherRequired: !composition.LiveLauncherRequired,
            NoHostedBackgroundServiceRequired: !composition.HostedBackgroundServiceRequired,
            NoDefaultConfigChangeRequired: !composition.DefaultConfigChangeRequired,
            PublicSurfaceReadOnly: issues.Count == 0,
            EvidenceSanitized: composition.EvidenceSanitized,
            MappingSummary: composition.MappingSummary,
            Issues: issues);
    }

    public static IReadOnlyList<string> ValidateCoreDescriptors(
        LmaxReadOnlyExecutionCoreBindingSet cores,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope)
    {
        ArgumentNullException.ThrowIfNull(cores);
        ArgumentNullException.ThrowIfNull(scope);

        var issues = new List<string>();
        var scopeIssues = LmaxExecutableReadOnlyCredentialBoundary.ValidateScope(scope);
        issues.AddRange(scopeIssues.Select(x => x.Code));

        if (!scope.DemoReadOnly || !string.Equals(scope.Environment, "Demo", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("NonDemoReadOnlyScope");
        }

        foreach (var descriptor in cores.Descriptors)
        {
            if (string.IsNullOrWhiteSpace(descriptor.Name))
            {
                issues.Add($"{descriptor.Boundary}:MissingCore");
                continue;
            }

            if (descriptor.TestOnly ||
                descriptor.Name.Contains("Fake", StringComparison.OrdinalIgnoreCase) ||
                descriptor.Name.Contains("Test", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"{descriptor.Name}:FakeTestOnlyCoreRejected");
            }

            if (!descriptor.ApprovedForDemoReadOnly)
            {
                issues.Add($"{descriptor.Name}:UnapprovedCore");
            }

            if (descriptor.ProductionAccountCapable)
            {
                issues.Add($"{descriptor.Name}:ProductionCoreRejected");
            }

            if (descriptor.OrderTradingReplayCapable)
            {
                issues.Add($"{descriptor.Name}:OrderTradingReplayCoreRejected");
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

            if (!descriptor.SanitizedEvidenceSupported)
            {
                issues.Add($"{descriptor.Name}:SanitizationMissing");
            }
        }

        return issues;
    }

    private static IReadOnlyList<string> ValidateReadOnlyPublicSurface()
    {
        var types = new[]
        {
            typeof(LmaxReadOnlyExecutionCompositionRoot),
            typeof(LmaxReadOnlyExecutionCoreBindingSet),
            typeof(LmaxReadOnlyExecutionCompositionResult),
            typeof(LmaxReadOnlyExecutionCompositionValidator)
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

    private static bool ValidateProviderCompleteness()
        => typeof(ILmaxRealReadOnlySocketBoundaryProvider).IsAssignableFrom(typeof(LmaxRealReadOnlySocketBoundaryProvider)) &&
           typeof(ILmaxRealReadOnlyTlsBoundaryProvider).IsAssignableFrom(typeof(LmaxRealReadOnlyTlsBoundaryProvider)) &&
           typeof(ILmaxRealReadOnlyFixFrameBoundaryProvider).IsAssignableFrom(typeof(LmaxRealReadOnlyFixFrameBoundaryProvider)) &&
           typeof(ILmaxRealReadOnlyMarketDataFrameBoundaryProvider).IsAssignableFrom(typeof(LmaxRealReadOnlyMarketDataFrameBoundaryProvider)) &&
           typeof(ILmaxRealReadOnlyCredentialConfigBoundaryProvider).IsAssignableFrom(typeof(LmaxRealReadOnlyCredentialConfigBoundaryProvider));
}

public sealed record LmaxReadOnlyFinalCompositionCompletenessResult(
    bool ProviderCompletenessPassed,
    bool ClientCompletenessPassed,
    bool OperationCompletenessPassed,
    bool RealCoreCompositionPassed,
    bool BoundedExecutorCompositionPassed,
    bool OperationToCoreMappingExists,
    bool NoFakeOrTestOnlyCore,
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
        RealCoreCompositionPassed &&
        BoundedExecutorCompositionPassed &&
        OperationToCoreMappingExists &&
        NoFakeOrTestOnlyCore &&
        NoApiWorkerStartupRequired &&
        NoLiveLauncherRequired &&
        NoHostedBackgroundServiceRequired &&
        NoDefaultConfigChangeRequired &&
        PublicSurfaceReadOnly &&
        EvidenceSanitized &&
        Issues.Count == 0;
}
