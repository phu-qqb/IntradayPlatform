using System.Text.Json;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bLivePlanMaterializationV2Tests
{
    [Fact]
    public void Static_template_declares_live_fact_schemas_without_prefilled_live_values()
    {
        var fixture = Fixture("static-template");
        var adapters = new Arch7bRealCommandAdapterRegistry();

        Arch7bLiveTemplateValidator.Validate(fixture.Template, adapters);

        Assert.Equal(40, fixture.Template.StageContracts.Count);
        Assert.Equal(15, fixture.Template.CommandTemplates.Count);
        Assert.Contains(fixture.Template.StageContracts, value =>
            value.ExecutionKind == Arch7bExecutionKind.ChildStartLongLived);
        Assert.Contains(fixture.Template.StageContracts, value =>
            value.ExecutionKind == Arch7bExecutionKind.Internal);
        using var document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(
            fixture.Template, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
        Assert.False(document.RootElement.TryGetProperty("selected_slot", out _));
        Assert.False(document.RootElement.TryGetProperty("run_id", out _));
        Assert.Equal(Arch7bV2Contracts.SecretLifecycleClassification,
            "CORE_LEASE_PROCESS_OWNS_SECRET_AND_SPAWNS_SECRET_CHILDREN");
    }

    [Fact]
    public void Fact_store_is_append_only_typed_producer_bound_and_staleness_aware()
    {
        var root = Root("fact-store");
        var store = new Arch7bOneShotLiveFactStore(root);
        var produced = DateTimeOffset.UtcNow;
        var evidence = Arch7bOneShotContracts.Sha256("selected-slot");
        store.Append("selected_slot", "SLOT_SELECTED", new { slot_id = "synthetic-slot" },
            evidence, produced);

        Assert.Equal("selected_slot", store.Require("selected_slot", "SLOT_SELECTED",
            produced.AddSeconds(1), 5).FactType);
        Assert.Equal(Arch7bV2Blockers.FactReplacementForbidden,
            Assert.Throws<Arch7bQualificationException>(() => store.Append("selected_slot",
                "SLOT_SELECTED", new { slot_id = "replacement" }, evidence, produced)).BlockerCode);
        Assert.Equal(Arch7bV2Blockers.FactProducerMismatch,
            Assert.Throws<Arch7bQualificationException>(() => store.Require("selected_slot",
                "SLOT_LOCKED", produced.AddSeconds(1), 5)).BlockerCode);
        Assert.Equal(Arch7bV2Blockers.FactStale,
            Assert.Throws<Arch7bQualificationException>(() => store.Require("selected_slot",
                "SLOT_SELECTED", produced.AddSeconds(10), 5)).BlockerCode);
        Assert.Equal(Arch7bV2Blockers.RequiredFactMissing,
            Assert.Throws<Arch7bQualificationException>(() => store.Require("missing",
                "SLOT_SELECTED", produced, 5)).BlockerCode);
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task Materializer_resolves_typed_run_root_and_publishes_content_addressed_authority()
    {
        var fixture = Fixture("materializer");
        Directory.CreateDirectory(fixture.RunRoot);
        var store = new Arch7bOneShotLiveFactStore(fixture.RunRoot);
        var now = DateTimeOffset.UtcNow;
        store.Append("runtime_run_root", "STATIC_AUTHORITY_VALIDATION",
            new { path = fixture.RunRoot }, Arch7bOneShotContracts.Sha256(fixture.RunRoot), now);
        var command = fixture.Template.CommandTemplates.Single(value =>
            value.StageId == "CORE_PREQUALIFICATION");

        var result = await new Arch7bOneShotCommandMaterializer().MaterializeAsync(command,
            store, fixture.Template.FileAuthorities, fixture.RunRoot, now);

        Assert.Contains(fixture.RunRoot, result.ArgumentList);
        Assert.True(Arch7bOneShotContracts.IsSha256(result.EvidenceSha256));
        Assert.True(Arch7bOneShotContracts.IsSha256(result.AuthorityFileSha256));
        Assert.True(File.Exists(result.AuthorityPath));
        Assert.StartsWith(Path.GetFullPath(fixture.RunRoot), Path.GetFullPath(result.AuthorityPath),
            StringComparison.OrdinalIgnoreCase);
        Directory.Delete(fixture.RunRoot, true);
    }

    [Fact]
    public void Full_authority_binds_template_operator_and_separated_freeze_hashes()
    {
        var fixture = Fixture("authority");

        fixture.Authority.Validate(fixture.Template, fixture.OperatorAuthorization,
            fixture.TemplateFileSha256, DateTimeOffset.UtcNow);

        var changed = fixture.Authority with { FreezePacketSha256 = new string('0', 64) };
        changed = changed with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(changed.Canonical()) };
        Assert.Equal(Arch7bV2Blockers.AuthorityBindingMismatch,
            Assert.Throws<Arch7bQualificationException>(() => changed.Validate(fixture.Template,
                fixture.OperatorAuthorization, fixture.TemplateFileSha256, DateTimeOffset.UtcNow)).BlockerCode);
    }

    [Fact]
    public async Task Complete_v2_runtime_traverses_40_stages_and_cleans_both_long_lived_processes()
    {
        var fixture = Fixture("full-runtime");
        var adapters = new Arch7bRealCommandAdapterRegistry();
        var runtime = new Arch7bOneShotLiveExecutionRuntimeV2(new(),
            new Arch7bOneShotProcessRunnerV2(adapters), adapters);

        var result = await runtime.RunAsync(fixture.Template, fixture.Authority,
            fixture.OperatorAuthorization, fixture.TemplateFileSha256, fixture.RunRoot,
            TimeProvider.System, new Arch7bCoreOwnedSecretLease());

        Assert.True(result.Passed, JsonSerializer.Serialize(result));
        Assert.Equal(40, result.Stages.Count);
        Assert.Equal(new Arch7bOneShotBudgetSnapshot(1, 2, 1, 0), result.Budget);
        Assert.Equal(2, result.LongLivedProcesses.Count);
        Assert.All(result.LongLivedProcesses, value =>
            Assert.Equal(Arch7bLongLivedProcessState.Cleaned, value.State));
        Assert.Equal(0, result.ResidualProcessCount);
        Assert.Equal(0, result.ResidualMarkerCount);
        Directory.Delete(fixture.RunRoot, true);
    }

    private static Arch7bV2QualificationFixture Fixture(string suffix) =>
        Arch7bV2QualificationFactory.Create(SupervisorExecutable(), Root(suffix));

    private static string Root(string suffix) => Path.Combine(Path.GetTempPath(),
        "qq-arch7b-v2-tests", suffix + "-" + Guid.NewGuid().ToString("N"));

    private static string SupervisorExecutable()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName,
                   "QQ.Production.Intraday.sln"))) current = current.Parent;
        var root = current?.FullName ?? throw new DirectoryNotFoundException("repository root");
        var extension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        return Path.Combine(root, "tools", "QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor",
            "bin", "Release", "net10.0", "QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor" + extension);
    }
}
