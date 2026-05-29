using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyRuntimeStaticIntegrationSafetyTests
{
    [Fact]
    public void Api_and_worker_default_execution_gateway_remains_fake_only()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));

        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", apiProgram, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton<IVenueExecutionGateway, Lmax", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton<IVenueExecutionGateway, Lmax", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxReadOnlyRuntime", workerProgram, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_appsettings_keep_lmax_runtime_and_unsafe_paths_disabled()
    {
        var root = FindRepoRoot();
        var appSettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.Contains("\"AllowExternalConnections\": false", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"AllowLiveTrading\": false", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"RequireFakeExecutionGateway\": true", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"Enabled\": false", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"ImplementationMode\": \"DesignOnly\"", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"AllowOrderSubmission\": false", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"SchedulerEnabled\": false", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"SubmitToShadowReplay\": false", appSettings, StringComparison.Ordinal);
        Assert.Contains("\"DryRun\": true", appSettings, StringComparison.Ordinal);
    }

    [Fact]
    public void R5_inert_path_has_no_live_transport_or_credential_dependencies()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxTemporaryReadOnlyRuntimeActivation.cs"));

        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Socket", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NetworkStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("QuickFix", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CredentialProfileResolver", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SessionPassword", source, StringComparison.Ordinal);
    }

    [Fact]
    public void R5_inert_validator_can_be_used_without_network_dependencies()
    {
        var scope = new LmaxTemporaryReadOnlyRuntimeActivationScope(
            "LMAX-R6",
            "Demo",
            DemoReadOnly: true,
            Temporary: true,
            InertValidatorOnly: true,
            LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments,
            new LmaxReadOnlyRuntimeSafetyFlags(),
            new LmaxReadOnlyRuntimeOperatorApproval(
                "Philippe",
                new DateTimeOffset(2026, 05, 12, 16, 30, 00, TimeSpan.Zero),
                "R6 static integration safety review",
                "LMAX-R6",
                "Demo/read-only",
                LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol).ToList()),
            new LmaxReadOnlyRuntimeShutdownRevertRecord(
                PlanPresent: true,
                ShutdownRequiredAfterAttempt: true,
                RevertRequiredAfterAttempt: true,
                "artifacts/readiness/lmax-runtime-enablement/future-shutdown-revert-record.json"),
            MaxRuntimeSeconds: 30,
            OutputRoot: "artifacts/readiness/lmax-runtime-enablement");

        var gate = LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope);

        Assert.True(gate.Passed);
        Assert.Empty(gate.Issues);
    }

    [Fact]
    public void Approved_allowlist_still_preserves_usdjpy_caveat()
    {
        var usdJpy = Assert.Single(LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments, x => x.Symbol == "USDJPY");

        Assert.Equal("4004", usdJpy.SecurityId);
        Assert.Equal("8", usdJpy.SecurityIdSource);
        Assert.Equal("prior failed-safe root cause remains unproven", usdJpy.Caveat);
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
}
