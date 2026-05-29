using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyFinalOperationCompletenessTests
{
    [Fact]
    public void Final_operation_completeness_check_passes_for_all_real_operation_bindings()
    {
        var result = LmaxReadOnlyExecutionOperationCompleteness.Validate();

        Assert.True(result.SocketOperationImplemented);
        Assert.True(result.TlsOperationImplemented);
        Assert.True(result.FixOperationImplemented);
        Assert.True(result.MarketDataOperationImplemented);
        Assert.True(result.CredentialConfigOperationImplemented);
        Assert.True(result.PublicSurfaceReadOnly, string.Join("; ", result.Issues));
        Assert.True(result.Passed, string.Join("; ", result.Issues));
    }

    [Fact]
    public void Operation_binding_classes_are_not_test_fakes()
    {
        var operationTypes = new[]
        {
            typeof(LmaxReadOnlySocketConnectOperationBinding),
            typeof(LmaxReadOnlyTlsHandshakeOperationBinding),
            typeof(LmaxReadOnlyFixSessionOperationBinding),
            typeof(LmaxReadOnlyMarketDataOperationBinding),
            typeof(LmaxReadOnlyCredentialConfigOperationBinding)
        };

        foreach (var type in operationTypes)
        {
            Assert.StartsWith("QQ.Production.Intraday.Infrastructure.Lmax", type.Namespace);
            Assert.DoesNotContain("Tests", type.FullName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Fake", type.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Stub", type.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Operation_completion_adds_no_api_worker_default_config_or_hosted_service_wiring()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.Contains("FakeLmaxGateway", apiProgram, StringComparison.Ordinal);
        foreach (var token in new[]
                 {
                     "LmaxReadOnlySocketConnectOperationBinding",
                     "LmaxReadOnlyTlsHandshakeOperationBinding",
                     "LmaxReadOnlyFixSessionOperationBinding",
                     "LmaxReadOnlyMarketDataOperationBinding",
                     "LmaxReadOnlyCredentialConfigOperationBinding",
                     "phase-lmax-r36"
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
