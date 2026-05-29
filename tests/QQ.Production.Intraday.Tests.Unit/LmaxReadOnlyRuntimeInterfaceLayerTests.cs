using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyRuntimeInterfaceLayerTests
{
    [Fact]
    public async Task Disabled_adapter_status_is_inert_and_safe_to_expose()
    {
        var adapter = new LmaxReadOnlyRuntimeAdapterDisabled();

        var status = await adapter.GetStatusAsync();

        Assert.Equal(LmaxReadOnlyRuntimeImplementationMode.DesignOnly, status.ImplementationMode);
        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Disabled, status.Status);
        Assert.False(status.Enabled);
        Assert.True(status.ReadOnly);
        Assert.False(status.AllowExternalConnections);
        Assert.False(status.AllowCredentialUse);
        Assert.False(status.AllowOrderSubmission);
        Assert.False(status.PersistRawFixMessages);
        Assert.False(status.PersistToTradingTables);
        Assert.False(status.SubmitToShadowReplay);
        Assert.False(status.SchedulerEnabled);
        Assert.Contains(status.SafetyGates, x => x.Name == "Enabled" && x.BlocksRun);
    }

    [Fact]
    public async Task Disabled_adapter_run_returns_disabled_or_blocked_without_batch_summary()
    {
        var adapter = new LmaxReadOnlyRuntimeAdapterDisabled();

        var result = await adapter.RunAsync(new LmaxReadOnlyRuntimeRunRequest("Phase 1 safety test"));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Disabled, result.Status);
        Assert.Null(result.BatchSummary);
        Assert.Contains("disabled/inert", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No sockets", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no credentials", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no orders", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no trading tables", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Safety_evaluation_fails_closed_for_dangerous_configuration()
    {
        var adapter = new LmaxReadOnlyRuntimeAdapterDisabled(new LmaxReadOnlyRuntimeAdapterOptions
        {
            Enabled = true,
            ImplementationMode = LmaxReadOnlyRuntimeImplementationMode.DisabledNoOp,
            AllowExternalConnections = false,
            AllowCredentialUse = false,
            AllowOrderSubmission = true,
            PersistToTradingTables = true,
            RequestedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit
        });

        var safety = await adapter.EvaluateSafetyAsync(new LmaxReadOnlyRuntimeRunRequest("dangerous config", DryRun: false));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, safety.RunStatus);
        Assert.Contains("AllowExternalConnections", safety.FailedGateNames);
        Assert.Contains("AllowCredentialUse", safety.FailedGateNames);
        Assert.Contains("AllowOrderSubmission", safety.FailedGateNames);
        Assert.Contains("PersistToTradingTables", safety.FailedGateNames);
        Assert.Contains("DryRun", safety.FailedGateNames);
        Assert.Contains("ActivationLevel", safety.FailedGateNames);
    }

    [Fact]
    public async Task Phase_1_blocks_activation_above_disabled_skeleton_even_with_safe_looking_options()
    {
        var adapter = new LmaxReadOnlyRuntimeAdapterDisabled(new LmaxReadOnlyRuntimeAdapterOptions
        {
            Enabled = true,
            ImplementationMode = LmaxReadOnlyRuntimeImplementationMode.DisabledNoOp,
            AllowExternalConnections = true,
            AllowCredentialUse = true,
            ReadOnly = true,
            AllowOrderSubmission = false,
            PersistRawFixMessages = false,
            PersistToTradingTables = false,
            OperationalReadinessPassed = true,
            GovernanceApproved = true,
            DryRun = true,
            RequestedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level2LocalManualNoExternal,
            MaxAllowedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level1DisabledSkeleton
        });

        var result = await adapter.RunAsync(new LmaxReadOnlyRuntimeRunRequest("phase escalation attempt"));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, result.Status);
        Assert.Contains("ActivationLevel", result.Safety.FailedGateNames);
    }

    [Fact]
    public async Task Disabled_evidence_sink_never_accepts_or_submits_evidence()
    {
        var sink = new LmaxReadOnlyRuntimeEvidenceSinkDisabled();

        var result = await sink.AcceptEvidenceBatchAsync(new LmaxReadOnlyRuntimeEvidenceBatchPreview(
            "batch-1",
            "lmax-fix-lifecycle-evidence-v1",
            "SyntheticLifecycle",
            DateTimeOffset.UtcNow,
            1,
            1,
            1,
            0,
            0,
            Sanitized: true,
            ContainsRawFix: false,
            []));

        Assert.False(result.Accepted);
        Assert.False(result.SubmittedToShadowReplay);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Noop_run_store_does_not_persist_run_history()
    {
        var store = new LmaxReadOnlyRuntimeRunStoreNoOp();
        var safety = LmaxReadOnlyRuntimeSafetyGate.Evaluate(new LmaxReadOnlyRuntimeAdapterOptions());
        await store.RecordRunAttemptAsync(new LmaxReadOnlyRuntimeRunResult(LmaxReadOnlyRuntimeRunStatus.Disabled, "blocked", safety, null));

        var runs = await store.GetRecentRunsAsync();

        Assert.Empty(runs);
    }

    [Fact]
    public void Runtime_interface_dtos_do_not_expose_secret_fields()
    {
        var types = new[]
        {
            typeof(LmaxReadOnlyRuntimeRunRequest),
            typeof(LmaxReadOnlyRuntimeRunResult),
            typeof(LmaxReadOnlyRuntimeStatus),
            typeof(LmaxReadOnlyRuntimeSafetyGateResult),
            typeof(LmaxReadOnlyRuntimeEvidenceBatchPreview),
            typeof(LmaxReadOnlyRuntimeEvidenceSinkResult)
        };

        foreach (var property in types.SelectMany(x => x.GetProperties(BindingFlags.Public | BindingFlags.Instance)))
        {
            Assert.DoesNotContain("password", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("apiKey", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("authorization", property.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Api_worker_and_runtime_interface_layer_do_not_reference_connectivity_lab_or_real_gateway_registration()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var interfaceLayer = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxReadOnlyRuntimeInterfaces.cs"));

        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", apiProgram, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectivityLab", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectivityLab", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectivityLab", interfaceLayer, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxVenueGateway", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxVenueGateway", workerProgram, StringComparison.Ordinal);
        Assert.Contains("/lmax-readonly-runtime/status", apiProgram, StringComparison.Ordinal);
        Assert.Contains("ILmaxReadOnlyRuntimeAdapter", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ILmaxReadOnlyRuntimeAdapter", workerProgram, StringComparison.Ordinal);
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
