using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QQ.Production.Intraday.Infrastructure.PostgreSql;
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
        Assert.Equal(14, fixture.Template.CommandTemplates.Count);
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
        var timeProvider = new Arch7bTestTimeProvider(DateTimeOffset.UtcNow);
        var stageWindowWaiter = new Arch7bTestStageWindowWaiter(timeProvider);
        var runtime = new Arch7bOneShotLiveExecutionRuntimeV2(new(),
            new Arch7bOneShotProcessRunnerV2(adapters), adapters,
            clockAuthorityProducer: new Arch7bTestClockAuthorityProducer(timeProvider),
            stageWindowWaiter: stageWindowWaiter);

        var result = await runtime.RunAsync(fixture.Template, fixture.Authority,
            fixture.OperatorAuthorization, fixture.TemplateFileSha256, fixture.RunRoot,
            timeProvider, new Arch7bCoreOwnedSecretLease());

        Assert.True(result.Passed, JsonSerializer.Serialize(result));
        Assert.Equal(40, result.Stages.Count);
        Assert.Equal(new Arch7bOneShotBudgetSnapshot(1, 2, 1, 0), result.Budget);
        Assert.Single(result.LongLivedProcesses);
        Assert.All(result.LongLivedProcesses, value =>
            Assert.Equal(Arch7bLongLivedProcessState.Cleaned, value.State));
        Assert.Equal(0, result.ResidualProcessCount);
        Assert.Equal(0, result.ResidualMarkerCount);
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(
            result.Stages.Single(value => value.StageId == "CLOCK_POST_CLOSE")
                .StartedAtUtc);
        Assert.Collection(stageWindowWaiter.Waits,
            value =>
            {
                Assert.Equal("CLOCK_CAPTURE_START", value.StageId);
                Assert.Equal(slot.SlotStartUtc.AddSeconds(
                    -Arch7bOperationalSlotSelector.CaptureClockLeadSeconds), value.TargetUtc);
                Assert.True(value.EnforceMaximumWakeLateness);
            },
            value =>
            {
                Assert.Equal("MARKET_CAPTURE", value.StageId);
                Assert.Equal(slot.SlotStartUtc, value.TargetUtc);
                Assert.True(value.EnforceMaximumWakeLateness);
            },
            value =>
            {
                Assert.Equal("CLOCK_POST_CLOSE", value.StageId);
                Assert.Equal(slot.SlotEndUtc, value.TargetUtc);
                Assert.False(value.EnforceMaximumWakeLateness);
            });
        Assert.Equal(Arch7bExecutionKind.Internal, result.Stages.Single(value =>
            value.StageId == "MARKET_PREARM").ExecutionKind);
        Assert.Equal(Arch7bExecutionKind.ChildInvoke, result.Stages.Single(value =>
            value.StageId == "MARKET_CAPTURE").ExecutionKind);
        Assert.Equal(Arch7bExecutionKind.ChildInvoke, result.Stages.Single(value =>
            value.StageId == "MARKET_FINALIZATION").ExecutionKind);
        foreach (var fileName in new[]
                 {
                     "clock_authority_preflight.json",
                     "clock_authority_capture.json",
                     "clock_authority_post_close.json"
                 })
            Assert.True(File.Exists(Path.Combine(fixture.RunRoot, fileName)), fileName);
        var facts = File.ReadAllText(Path.Combine(fixture.RunRoot, "live-facts.jsonl"));
        Assert.Contains(Arch7bClockFactContracts.PreflightFactType, facts,
            StringComparison.Ordinal);
        Assert.Contains(Arch7bClockFactContracts.CaptureStartFactType, facts,
            StringComparison.Ordinal);
        Assert.Contains(Arch7bClockFactContracts.PostCloseFactType, facts,
            StringComparison.Ordinal);
        Assert.Contains(result.Stages, value =>
            value.StageId == "PORTAL_SESSION_PROVEN");
        Assert.Contains(result.Stages, value =>
            value.StageId == "MARKET_CAPTURE");
        Assert.Contains(result.Stages, value =>
            value.StageId == "MARKET_FINALIZATION");
        Assert.Contains(Arch7bClockFactContracts.PreflightFactType,
            fixture.Template.StageContracts.Single(value =>
                value.StageId == "PORTAL_SESSION_PROVEN").RequiredFactTypes);
        Assert.Contains(Arch7bClockFactContracts.CaptureStartFactType,
            fixture.Template.StageContracts.Single(value =>
                value.StageId == "MARKET_CAPTURE").RequiredFactTypes);
        Assert.Contains(Arch7bClockFactContracts.PostCloseFactType,
            fixture.Template.StageContracts.Single(value =>
                value.StageId == "MARKET_FINALIZATION").RequiredFactTypes);
        Assert.Empty(Directory.EnumerateFiles(fixture.RunRoot, "*.tmp",
            SearchOption.AllDirectories));
        Directory.Delete(fixture.RunRoot, true);
    }

    [Fact]
    public async Task Static_template_v2_is_perishable_identity_free_and_materializes_create_new_authorities()
    {
        var fixture = Fixture("materialization");
        var freeze = await WriteFreezeAsync(fixture, "materialization");
        var output = Root("materialized-authorities");
        var now = DateTimeOffset.UtcNow;

        var result = await Arch7bLiveAuthorityMaterializer.MaterializeAsync(freeze.Root, freeze.ManifestSha,
            freeze.PacketSha, freeze.TemplateSha, "operator-materialization-a", now.AddSeconds(-1),
            now.AddMinutes(10), output, "TEST", "1754288005", true);

        Assert.True(File.Exists(result.OperatorAuthorizationPath));
        Assert.True(File.Exists(result.LiveExecutionAuthorityPath));
        Assert.True(File.Exists(result.ManifestPath));
        Assert.DoesNotContain("operator_authorization_id", await File.ReadAllTextAsync(freeze.TemplatePath),
            StringComparison.Ordinal);
        var authorization = await Arch7bLiveAuthorityLoaderV2.LoadOperatorAsync(result.OperatorAuthorizationPath,
            result.OperatorAuthorizationSha256);
        var authority = await Arch7bLiveAuthorityLoaderV2.LoadAuthorityAsync(result.LiveExecutionAuthorityPath,
            result.LiveExecutionAuthoritySha256);
        authority.Value.Validate(freeze.Template, authorization.Value, result.TemplateSha256, DateTimeOffset.UtcNow);
        Assert.Equal(Arch7bNoLiveSafetyCounters.Zero, result.Safety);

        var error = await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
            Arch7bLiveAuthorityMaterializer.MaterializeAsync(freeze.Root, freeze.ManifestSha, freeze.PacketSha,
                freeze.TemplateSha, "operator-materialization-b", DateTimeOffset.UtcNow.AddSeconds(-1),
                DateTimeOffset.UtcNow.AddMinutes(10), output, "TEST", "1754288005", true));
        Assert.Equal(Arch7bBlockers.RunRootNotEmpty, error.BlockerCode);
        Directory.Delete(freeze.Root, true);
        Directory.Delete(output, true);
    }

    [Fact]
    public async Task Versioned_program_mode_materializes_the_v2_v3_v2_triplet_without_external_access()
    {
        var fixture = Fixture("program-mode");
        var freeze = await WriteFreezeAsync(fixture, "program-mode");
        var output = Root("program-mode-output");
        var now = DateTimeOffset.UtcNow;
        var exit = await Program.Main([
            "--mode", "materialize-live-run-authorities",
            "--qualification-only", "false",
            "--freeze-root", freeze.Root,
            "--expected-freeze-manifest-sha256", freeze.ManifestSha,
            "--expected-freeze-packet-sha256", freeze.PacketSha,
            "--expected-live-plan-template-sha256", freeze.TemplateSha,
            "--operator-authorization-id", "operator-program-mode",
            "--issued-at-utc", now.AddSeconds(-1).ToString("O"),
            "--expires-at-utc", now.AddMinutes(10).ToString("O"),
            "--output-root", output,
            "--target-environment", "TEST",
            "--account-id", "1754288005",
            "--no-order", "true"
        ]);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(output,
            Arch7bLiveAuthorityMaterializer.OperatorAuthorizationFileName)));
        Assert.True(File.Exists(Path.Combine(output,
            Arch7bLiveAuthorityMaterializer.LiveExecutionAuthorityFileName)));
        Directory.Delete(freeze.Root, true);
        Directory.Delete(output, true);
    }
    [Theory]
    [InlineData("operator_authorization_id")]
    [InlineData("selected_slot")]
    [InlineData("run_id")]
    [InlineData("secret_version_id")]
    public void Static_template_rejects_perishable_properties(string property)
    {
        var fixture = Fixture("perishable-" + property);
        var node = JsonNode.Parse(JsonSerializer.Serialize(fixture.Template,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }))!.AsObject();
        node[property] = "forbidden";

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bLiveTemplateValidator.ValidateStaticDocument(Encoding.UTF8.GetBytes(node.ToJsonString())));

        Assert.Equal(Arch7bV2Blockers.CommandTemplateInvalid, error.BlockerCode);
    }

    [Fact]
    public async Task Expired_authorization_and_distinct_operator_ids_are_handled_fail_closed()
    {
        var fixture = Fixture("expiry-and-identity");
        var freeze = await WriteFreezeAsync(fixture, "expiry-and-identity");
        var expiredOutput = Root("expired-authority");
        var now = DateTimeOffset.UtcNow;
        var expired = await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
            Arch7bLiveAuthorityMaterializer.MaterializeAsync(freeze.Root, freeze.ManifestSha, freeze.PacketSha,
                freeze.TemplateSha, "operator-expired", now.AddMinutes(-10), now.AddMinutes(-1), expiredOutput,
                "TEST", "1754288005", true));
        Assert.Equal(Arch7bBlockers.OperatorAuthorizationMismatch, expired.BlockerCode);

        var first = await Arch7bLiveAuthorityMaterializer.MaterializeAsync(freeze.Root, freeze.ManifestSha,
            freeze.PacketSha, freeze.TemplateSha, "operator-first", now.AddSeconds(-1), now.AddMinutes(10),
            Root("authority-first"), "TEST", "1754288005", true);
        var second = await Arch7bLiveAuthorityMaterializer.MaterializeAsync(freeze.Root, freeze.ManifestSha,
            freeze.PacketSha, freeze.TemplateSha, "operator-second", now.AddSeconds(-1), now.AddMinutes(10),
            Root("authority-second"), "TEST", "1754288005", true);
        Assert.Equal(first.TemplateSha256, second.TemplateSha256);
        Assert.NotEqual(first.OperatorAuthorizationSha256, second.OperatorAuthorizationSha256);
        Assert.NotEqual(first.LiveExecutionAuthoritySha256, second.LiveExecutionAuthoritySha256);
        Directory.Delete(freeze.Root, true);
        Directory.Delete(Path.GetDirectoryName(first.OperatorAuthorizationPath)!, true);
        Directory.Delete(Path.GetDirectoryName(second.OperatorAuthorizationPath)!, true);
    }

    private static async Task<(string Root, string TemplatePath, string ManifestSha, string PacketSha,
        string TemplateSha, Arch7bOneShotLivePlanTemplate Template)> WriteFreezeAsync(
        Arch7bV2QualificationFixture fixture, string suffix)
    {
        var root = Root("freeze-" + suffix);
        Directory.CreateDirectory(root);
        var manifestBytes = Encoding.UTF8.GetBytes("manifest-" + suffix);
        var packetBytes = Encoding.UTF8.GetBytes("packet-" + suffix);
        var manifestSha = Arch7bOneShotContracts.Sha256("manifest-" + suffix);
        var packetSha = Arch7bOneShotContracts.Sha256("packet-" + suffix);
        await File.WriteAllBytesAsync(Path.Combine(root, "arch7b-final-operational-freeze-v7-manifest.json"), manifestBytes);
        await File.WriteAllBytesAsync(Path.Combine(root, "ARCH7B-next-operational-run-packet-v7.json"), packetBytes);
        var template = fixture.Template with
        {
            FreezeManifestSha256 = manifestSha,
            FreezePacketSha256 = packetSha,
            EvidenceSha256 = string.Empty
        };
        template = template with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(template.Canonical()) };
        var templatePath = Path.Combine(root, Arch7bLiveAuthorityMaterializer.TemplateFileName);
        var templateBytes = JsonSerializer.SerializeToUtf8Bytes(template,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        await File.WriteAllBytesAsync(templatePath, templateBytes);
        return (root, templatePath, manifestSha, packetSha,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(templateBytes)), template);
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
