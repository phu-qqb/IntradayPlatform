using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyFinalDelegateBindingCompletenessTests
{
    [Fact]
    public void Final_delegate_binding_completeness_passes_for_approved_inert_runtime_delegates()
    {
        var result = LmaxReadOnlyRuntimeCoreDelegateBindingValidator.ValidateCompleteness(
            ApprovedBindings(),
            ValidScope());

        Assert.True(result.Passed);
        Assert.True(result.ProviderCompletenessPassed);
        Assert.True(result.ClientCompletenessPassed);
        Assert.True(result.OperationCompletenessPassed);
        Assert.True(result.CoreCompositionPassed);
        Assert.True(result.RuntimeDelegateBindingPassed);
        Assert.True(result.BoundedExecutorCompositionPassed);
        Assert.True(result.OperationToDelegateMappingExists);
        Assert.True(result.NoFakeOrTestOnlyDelegate);
        Assert.True(result.NoApiWorkerStartupRequired);
        Assert.True(result.NoLiveLauncherRequired);
        Assert.True(result.NoHostedBackgroundServiceRequired);
        Assert.True(result.NoDefaultConfigChangeRequired);
        Assert.True(result.PublicSurfaceReadOnly);
        Assert.True(result.EvidenceSanitized);
        Assert.Empty(result.Issues);
        Assert.Contains(result.MappingSummary, x => x.Contains("SocketConnect:LmaxApprovedSocketConnectRuntimeDelegate", StringComparison.Ordinal));
        Assert.Contains(result.MappingSummary, x => x.Contains("CredentialConfig:LmaxApprovedCredentialConfigRuntimeDelegate", StringComparison.Ordinal));
    }

    [Fact]
    public void Api_worker_default_gateway_and_appsettings_remain_fake_only()
    {
        var root = FindRepositoryRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));
        var workerProgramPath = Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs");
        var workerProgram = File.Exists(workerProgramPath) ? File.ReadAllText(workerProgramPath) : string.Empty;

        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", apiProgram);
        Assert.Contains("\"RequireFakeExecutionGateway\": true", appsettings);
        Assert.DoesNotContain("LmaxReadOnlyRuntimeCoreDelegateBindingFactory", apiProgram);
        Assert.DoesNotContain("LmaxReadOnlyRuntimeCoreDelegateBindingFactory", workerProgram);
        Assert.DoesNotContain("LmaxReadOnlyRuntimeCoreDelegateBindingFactory", appsettings);
        Assert.DoesNotContain("\"Enabled\": true", appsettings);
    }

    private static LmaxReadOnlyRuntimeCoreDelegateBindingSet ApprovedBindings()
        => LmaxReadOnlyRuntimeCoreDelegateBindingSet.CreateApproved(
            (_, _, _) => Dependency("SocketDelegateBoundButNotExternallyExecuted"),
            (_, _, _) => Dependency("TlsDelegateBoundButNotExternallyExecuted"),
            (_, _, _, _) => Dependency("FixDelegateBoundButNotExternallyExecuted"),
            (_, scope, _) => new LmaxReadOnlyMarketDataSessionClientResult(
                scope.Instruments.Select(instrument => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                    instrument.Symbol,
                    instrument.SecurityId,
                    instrument.SecurityIdSource,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
                    0,
                    0,
                    0,
                    0,
                    "MarketDataDelegateBoundButNotExternallyExecuted",
                    null,
                    null,
                    instrument.Caveat)).ToList(),
                "MarketDataDelegateBoundButNotExternallyExecuted",
                null,
                null),
            (_, _, _, _) => new LmaxRealReadOnlySecretAccessResult(
                true,
                false,
                false,
                false,
                false,
                "CredentialConfigDelegateBoundButNotExternallyExecuted",
                null,
                null));

    private static LmaxRealReadOnlyDependencyResult Dependency(string status)
        => new(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, status, null, null);

    private static LmaxTemporaryReadOnlyRuntimeActivationScope ValidScope()
        => new(
            "LMAX-R40",
            "Demo",
            DemoReadOnly: true,
            Temporary: true,
            InertValidatorOnly: true,
            LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments,
            new LmaxReadOnlyRuntimeSafetyFlags(),
            new LmaxReadOnlyRuntimeOperatorApproval(
                "Philippe",
                new DateTimeOffset(2026, 05, 12, 19, 00, 00, TimeSpan.Zero),
                "R40 delegate binding validation only",
                "LMAX-R40",
                "Demo/read-only",
                LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol).ToList()),
            new LmaxReadOnlyRuntimeShutdownRevertRecord(true, true, true, "artifacts/readiness/lmax-runtime-enablement/r40-test"),
            MaxRuntimeSeconds: 30,
            "artifacts/readiness/lmax-runtime-enablement");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
