using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyFinalCompositionCompletenessTests
{
    [Fact]
    public void Final_composition_completeness_check_passes()
    {
        var result = LmaxReadOnlyExecutionCompositionValidator.Validate(ApprovedCores(), ValidScope());

        Assert.True(result.ProviderCompletenessPassed);
        Assert.True(result.ClientCompletenessPassed);
        Assert.True(result.OperationCompletenessPassed);
        Assert.True(result.RealCoreCompositionPassed);
        Assert.True(result.BoundedExecutorCompositionPassed);
        Assert.True(result.OperationToCoreMappingExists);
        Assert.True(result.NoFakeOrTestOnlyCore);
        Assert.True(result.NoApiWorkerStartupRequired);
        Assert.True(result.NoLiveLauncherRequired);
        Assert.True(result.NoHostedBackgroundServiceRequired);
        Assert.True(result.NoDefaultConfigChangeRequired);
        Assert.True(result.PublicSurfaceReadOnly);
        Assert.True(result.EvidenceSanitized);
        Assert.Empty(result.Issues);
        Assert.True(result.Passed);
    }

    [Fact]
    public void Composition_adds_no_api_worker_default_config_launcher_or_hosted_service_wiring()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.Contains("FakeLmaxGateway", apiProgram, StringComparison.Ordinal);
        foreach (var token in new[]
                 {
                     "LmaxReadOnlyExecutionCompositionRoot",
                     "LmaxReadOnlyExecutionCoreBindingSet",
                     "LmaxReadOnlyExecutionCompositionValidator",
                     "phase-lmax-r38"
                 })
        {
            Assert.DoesNotContain(token, apiProgram, StringComparison.Ordinal);
            Assert.DoesNotContain(token, workerProgram, StringComparison.Ordinal);
            Assert.DoesNotContain(token, appsettings, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("AddHostedService<Lmax", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<Lmax", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Enabled\": true", appsettings, StringComparison.Ordinal);
    }

    private static LmaxReadOnlyExecutionCoreBindingSet ApprovedCores()
        => LmaxReadOnlyExecutionCoreBindingSet.CreateApproved(
            (_, _, _) => Success("Socket"),
            (_, _, _) => Success("Tls"),
            (_, _, _, _) => Success("Fix"),
            (_, scope, _) => new(
                scope.Instruments.Select(instrument => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                    instrument.Symbol,
                    instrument.SecurityId,
                    instrument.SecurityIdSource,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded,
                    1,
                    0,
                    0,
                    0,
                    "LocalInstrumentMarketDataSanitized",
                    null,
                    null,
                    instrument.Caveat)).ToList(),
                "LocalMarketDataSanitized",
                null,
                null),
            (_, _, _, _) => new(
                AccessAllowed: true,
                RealSecretMaterialLoaded: false,
                SensitiveMaterialReturned: false,
                SensitiveMaterialPrinted: false,
                SensitiveMaterialStored: false,
                "LocalCredentialSanitized",
                null,
                null));

    private static LmaxRealReadOnlyDependencyResult Success(string name)
        => new(LmaxTemporaryReadOnlySessionBoundaryStatus.FakeSucceeded, $"Local{name}Sanitized", null, null);

    private static LmaxTemporaryReadOnlyRuntimeActivationScope ValidScope()
        => new(
            "LMAX-R38",
            "Demo",
            DemoReadOnly: true,
            Temporary: true,
            InertValidatorOnly: true,
            LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments,
            new LmaxReadOnlyRuntimeSafetyFlags(),
            new LmaxReadOnlyRuntimeOperatorApproval(
                "Philippe",
                new DateTimeOffset(2026, 05, 12, 19, 00, 00, TimeSpan.Zero),
                "R38 local-only test approval marker",
                "LMAX-R38",
                "Demo/read-only",
                LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol).ToList()),
            new LmaxReadOnlyRuntimeShutdownRevertRecord(true, true, true, "artifacts/readiness/lmax-runtime-enablement/r38-test"),
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
}
