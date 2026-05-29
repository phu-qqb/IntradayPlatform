using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyRuntimeFakeInMemoryTests
{
    [Fact]
    public async Task Default_fake_adapter_is_disabled_and_blocked()
    {
        var adapter = new LmaxReadOnlyRuntimeAdapterFakeInMemory();

        var result = await adapter.RunAsync(new LmaxReadOnlyRuntimeRunRequest("default fake run"));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Disabled, result.Status);
        Assert.Contains("Enabled", result.Safety.FailedGateNames);
        Assert.Contains("FakeInMemoryImplementationMode", result.Safety.FailedGateNames);
    }

    [Theory]
    [InlineData(nameof(LmaxReadOnlyRuntimeAdapterOptions.AllowExternalConnections))]
    [InlineData(nameof(LmaxReadOnlyRuntimeAdapterOptions.AllowCredentialUse))]
    [InlineData(nameof(LmaxReadOnlyRuntimeAdapterOptions.AllowOrderSubmission))]
    [InlineData(nameof(LmaxReadOnlyRuntimeAdapterOptions.PersistToTradingTables))]
    [InlineData(nameof(LmaxReadOnlyRuntimeAdapterOptions.SchedulerEnabled))]
    public async Task Fake_adapter_blocks_dangerous_flags(string flagName)
    {
        var options = SafeFakeOptions() with
        {
            AllowExternalConnections = flagName == nameof(LmaxReadOnlyRuntimeAdapterOptions.AllowExternalConnections),
            AllowCredentialUse = flagName == nameof(LmaxReadOnlyRuntimeAdapterOptions.AllowCredentialUse),
            AllowOrderSubmission = flagName == nameof(LmaxReadOnlyRuntimeAdapterOptions.AllowOrderSubmission),
            PersistToTradingTables = flagName == nameof(LmaxReadOnlyRuntimeAdapterOptions.PersistToTradingTables),
            SchedulerEnabled = flagName == nameof(LmaxReadOnlyRuntimeAdapterOptions.SchedulerEnabled)
        };
        var adapter = new LmaxReadOnlyRuntimeAdapterFakeInMemory(options);

        var result = await adapter.RunAsync(new LmaxReadOnlyRuntimeRunRequest("dangerous flag"));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, result.Status);
        Assert.Contains(flagName == nameof(LmaxReadOnlyRuntimeAdapterOptions.SchedulerEnabled) ? "SchedulerEnabled" : flagName, result.Safety.FailedGateNames);
    }

    [Fact]
    public async Task Fake_adapter_blocks_activation_beyond_phase_2()
    {
        var options = SafeFakeOptions() with
        {
            RequestedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level3LabExternalCaptureToFile
        };

        var result = await new LmaxReadOnlyRuntimeAdapterFakeInMemory(options).RunAsync(new LmaxReadOnlyRuntimeRunRequest("phase 3 attempt"));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, result.Status);
        Assert.Contains("Phase2ActivationLevel", result.Safety.FailedGateNames);
    }

    [Fact]
    public async Task Missing_fixture_path_fails_clearly()
    {
        var options = SafeFakeOptions() with { FixtureEvidenceFile = Path.Combine(FindRepoRoot(), "missing-evidence.json") };

        var result = await new LmaxReadOnlyRuntimeAdapterFakeInMemory(options).RunAsync(new LmaxReadOnlyRuntimeRunRequest("missing fixture"));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, result.Status);
        Assert.Contains("Fixture evidence file was not found", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, result.ValidationErrorCount);
    }

    [Fact]
    public async Task Invalid_fixture_evidence_fails_clearly()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lmax-invalid-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{ \"schemaVersion\": \"bad\", \"password\": \"nope\" }");
        try
        {
            var options = SafeFakeOptions() with { FixtureEvidenceFile = path };

            var result = await new LmaxReadOnlyRuntimeAdapterFakeInMemory(options).RunAsync(new LmaxReadOnlyRuntimeRunRequest("invalid fixture"));

            Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, result.Status);
            Assert.True(result.ValidationErrorCount > 0);
            Assert.Contains("validation failed", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("lmax-readonly-empty-evidence-v1.json", "EmptyReadOnly", 0)]
    [InlineData("lmax-marketdata-only-evidence-v1.json", "MarketDataOnly", 1)]
    [InlineData("lmax-tradecapture-only-evidence-v1.json", "TradeCaptureOnly", 1)]
    [InlineData("lmax-orderstatus-only-evidence-v1.json", "OrderStatusOnly", 1)]
    [InlineData("lmax-protocolreject-only-evidence-v1.json", "ProtocolRejectOnly", 1)]
    [InlineData("lmax-mixed-readonly-evidence-v1.json", "MixedReadOnly", 3)]
    [InlineData("lmax-fix-lifecycle-evidence-v1.json", "SyntheticLifecycle", 3)]
    public async Task Valid_fixture_evidence_validates_and_produces_preview_counts(string fixtureName, string expectedMode, int expectedEvents)
    {
        var options = SafeFakeOptions() with { FixtureEvidenceFile = FixturePath(fixtureName) };

        var result = await new LmaxReadOnlyRuntimeAdapterFakeInMemory(options).RunAsync(new LmaxReadOnlyRuntimeRunRequest("fixture preview"));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Completed, result.Status);
        Assert.Equal(LmaxReadOnlyRuntimeRunMode.FakeInMemoryFixtureOnly, result.RunMode);
        Assert.Equal(expectedMode, result.EvidenceMode);
        Assert.Equal(expectedEvents, result.InputEventCount);
        Assert.Equal(0, result.ValidationErrorCount);
        Assert.NotNull(result.BatchSummary);
        Assert.False(result.BatchSummary!.SubmittedToShadowReplay);
        Assert.Equal(0, result.ObservationCount);
        Assert.Null(result.ReplayRunId);
    }

    [Fact]
    public async Task Submit_to_shadow_replay_true_is_blocked_in_phase_2_preview_mode()
    {
        var options = SafeFakeOptions() with { SubmitToShadowReplay = true };

        var result = await new LmaxReadOnlyRuntimeAdapterFakeInMemory(options).RunAsync(new LmaxReadOnlyRuntimeRunRequest("submit attempt"));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, result.Status);
        Assert.Contains("SubmitToShadowReplay", result.Safety.FailedGateNames);
    }

    [Fact]
    public async Task In_memory_run_store_records_fake_runs_only_in_memory()
    {
        var store = new LmaxReadOnlyRuntimeRunStoreInMemory();
        var adapter = new LmaxReadOnlyRuntimeAdapterFakeInMemory(SafeFakeOptions(), runStore: store);

        await adapter.RunAsync(new LmaxReadOnlyRuntimeRunRequest("first"));
        await adapter.RunAsync(new LmaxReadOnlyRuntimeRunRequest("second"));

        var runs = await store.GetRecentRunsAsync();

        Assert.Equal(2, runs.Count);
        Assert.All(runs, x => Assert.Equal(LmaxReadOnlyRuntimeRunMode.FakeInMemoryFixtureOnly, x.RunMode));
    }

    [Fact]
    public void Fake_runtime_dtos_do_not_expose_secret_fields()
    {
        var types = new[]
        {
            typeof(LmaxReadOnlyRuntimeAdapterOptions),
            typeof(LmaxReadOnlyRuntimeFixturePreview),
            typeof(LmaxReadOnlyRuntimeEvidenceBatchPreview),
            typeof(LmaxReadOnlyRuntimeRunResult)
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
    public void Fake_adapter_does_not_reference_connectivity_lab_and_api_worker_remain_fake_lmax_only()
    {
        var root = FindRepoRoot();
        var fakeAdapter = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxReadOnlyRuntimeFakeInMemory.cs"));
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));

        Assert.DoesNotContain("ConnectivityLab", fakeAdapter, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", apiProgram, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", workerProgram, StringComparison.Ordinal);
        Assert.Contains("/lmax-readonly-runtime/status", apiProgram, StringComparison.Ordinal);
        Assert.Contains("ILmaxReadOnlyRuntimeAdapter", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ILmaxReadOnlyRuntimeAdapter", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxVenueGateway", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxVenueGateway", workerProgram, StringComparison.Ordinal);
    }

    private static LmaxReadOnlyRuntimeAdapterOptions SafeFakeOptions()
        => new()
        {
            Enabled = true,
            ImplementationMode = LmaxReadOnlyRuntimeImplementationMode.FakeInMemory,
            AllowExternalConnections = false,
            AllowCredentialUse = false,
            ReadOnly = true,
            AllowOrderSubmission = false,
            PersistRawFixMessages = false,
            PersistToTradingTables = false,
            SubmitToShadowReplay = false,
            SchedulerEnabled = false,
            EnvironmentName = "Local",
            OperationalReadinessPassed = true,
            GovernanceApproved = true,
            DryRun = true,
            RequestedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level2LocalManualNoExternal,
            MaxAllowedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level2LocalManualNoExternal,
            FixtureEvidenceFile = FixturePath("lmax-mixed-readonly-evidence-v1.json")
        };

    private static string FixturePath(string fixtureName)
        => Path.Combine(FindRepoRoot(), "tests", "fixtures", "lmax-shadow", fixtureName);

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
