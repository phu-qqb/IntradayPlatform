using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxConnectivityLabTests
{
    [Fact]
    public void Default_config_blocks_external_calls_and_order_submission()
    {
        var options = new LmaxConnectivityLabOptions();
        var validator = new LmaxConnectivityLabSafetyValidator();

        Assert.Contains(validator.ValidateForExternalCall(options), x => x.Contains("AllowExternalConnections=false", StringComparison.Ordinal));
        Assert.Contains(validator.ValidateForOrderSubmission(options, explicitConfirmation: false), x => x.Contains("AllowOrderSubmission=false", StringComparison.Ordinal));
    }

    [Fact]
    public void AllowLiveTrading_is_rejected()
    {
        var options = new LmaxConnectivityLabOptions { AllowExternalConnections = true, AllowLiveTrading = true };

        var issues = new LmaxConnectivityLabSafetyValidator().ValidateForExternalCall(options);

        Assert.Contains(issues, x => x.Contains("AllowLiveTrading=true", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_environment_order_submission_is_rejected()
    {
        var options = new LmaxConnectivityLabOptions
        {
            EnvironmentName = "Production",
            AllowExternalConnections = true,
            AllowOrderSubmission = true,
            AllowLiveTrading = false,
            DryRun = false
        };

        var issues = new LmaxConnectivityLabSafetyValidator().ValidateForOrderSubmission(options, explicitConfirmation: true);

        Assert.Contains(issues, x => x.Contains("Production", StringComparison.Ordinal));
    }

    [Fact]
    public void Demo_or_uat_dry_run_command_is_allowed_without_network()
    {
        var runner = CreateRunner();
        var result = runner.OrderLifecycleDryRun(new LmaxConnectivityLabOptions { EnvironmentName = "Demo" });

        Assert.Equal("Ok", result.Status);
        Assert.Contains(result.SafetyDecisions, x => x.Contains("No order was submitted", StringComparison.Ordinal));
    }

    [Fact]
    public void Order_lifecycle_demo_requires_explicit_confirmation()
    {
        var runner = CreateRunner();
        var options = new LmaxConnectivityLabOptions
        {
            EnvironmentName = "Demo",
            AllowExternalConnections = true,
            AllowOrderSubmission = true,
            AllowLiveTrading = false,
            DryRun = false
        };

        var result = runner.OrderLifecycleDemo(options, explicitConfirmation: false);

        Assert.Equal("Blocked", result.Status);
        Assert.Contains("explicit", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Print_config_masks_secrets()
    {
        var options = new LmaxConnectivityLabOptions
        {
            AccountApiKey = "secret-api-key",
            FixUsername = "fix-user",
            FixSenderCompId = "sender"
        };

        var safe = options.ToSafeDictionary();

        Assert.Equal("********", safe["AccountApiKey"]);
        Assert.Equal("********", safe["FixUsername"]);
        Assert.Equal("********", safe["FixSenderCompId"]);
        Assert.DoesNotContain("secret-api-key", string.Join("|", safe.Values), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Commands_skip_without_config_or_external_permission()
    {
        var publicData = new PlaceholderLmaxPublicDataClient();
        var account = new PlaceholderLmaxAccountClient();
        var fix = new PlaceholderLmaxFixSessionClient();
        var options = new LmaxConnectivityLabOptions();

        var publicResult = await publicData.SmokeAsync(options, CancellationToken.None);
        var accountResult = await account.SmokeAsync(new LmaxConnectivityLabOptions { AllowExternalConnections = true }, CancellationToken.None);
        var fixResult = fix.Validate(options, marketData: false);

        Assert.Equal("Skipped", publicResult.Status);
        Assert.Equal("Skipped", accountResult.Status);
        Assert.Equal("Skipped", fixResult.Status);
    }

    [Fact]
    public void Api_and_worker_do_not_reference_connectivity_lab()
    {
        var root = FindRepoRoot();
        var apiProject = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "QQ.Production.Intraday.Api.csproj"));
        var workerProject = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "QQ.Production.Intraday.Worker.csproj"));

        Assert.DoesNotContain("ConnectivityLab", apiProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectivityLab", workerProject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Api_and_worker_still_register_fake_lmax_gateway_only()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));

        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", apiProgram, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton<IVenueExecutionGateway, LmaxVenueGateway>", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton<IVenueExecutionGateway, LmaxVenueGateway>", workerProgram, StringComparison.Ordinal);
    }

    private static LmaxConnectivityLabRunner CreateRunner()
        => new(new PlaceholderLmaxPublicDataClient(), new PlaceholderLmaxAccountClient(), new PlaceholderLmaxFixSessionClient(), new LmaxConnectivityLabSafetyValidator());

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
