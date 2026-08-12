using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bCoreRuntimePrequalificationAdapterTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),
        "arch7b-core-prequalification-adapter", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Native_qualification_and_three_manifest_artifacts_are_bound()
    {
        var now = DateTimeOffset.Parse("2026-08-10T12:00:00Z");
        var outputRoot = Path.Combine(root, "output");
        Directory.CreateDirectory(outputRoot);
        var sourceFiles = new JsonArray
        {
            new JsonObject
            {
                ["relative_path"] = "src/downloader.mjs",
                ["sha256"] = new string('1', 64)
            }
        };
        var qualification = new JsonObject
        {
            ["contract"] = Arch7bCoreRuntimePrequalificationAdapter.NativeContract,
            ["repository"] = "phu-qqb/QQ.Production.Core",
            ["core_head"] = Arch7bOneShotContracts.CoreCommit,
            ["core_tree"] = Arch7bOneShotContracts.CoreTree,
            ["worktree_clean"] = true,
            ["index_clean"] = true,
            ["downloader_version"] = "0.6.0",
            ["bracket_contract"] = "lmax_portal_bracketed_current_position_snapshot_v2",
            ["package_json_sha256"] = new string('2', 64),
            ["package_lock_sha256"] = new string('3', 64),
            ["runtime_source_set_sha256"] = ShaCanonical(sourceFiles),
            ["runtime_source_files"] = sourceFiles,
            ["node_version"] = "v24.0.0",
            ["node_executable_sha256"] = new string('4', 64),
            ["npm_version"] = "11.0.0",
            ["playwright_version"] = "1.54.0",
            ["aws_sdk_secrets_manager_version"] = "3.0.0",
            ["browser_runtime"] = new JsonObject
            {
                ["channel"] = "msedge",
                ["version"] = "138.0.0.0"
            },
            ["host"] = "PRIMARY",
            ["exact_test_command"] = "npm test",
            ["tests_passed"] =
                Arch7bOneShotContracts.ExpectedCorePrequalificationTestCount,
            ["tests_total"] =
                Arch7bOneShotContracts.ExpectedCorePrequalificationTestCount,
            ["syntax_checks"] = "PASS",
            ["npm_audit_omit_dev_vulnerabilities"] = 0,
            ["secret_sentinel_scan"] = "PASS",
            ["forbidden_route_scan"] = "PASS",
            ["git_diff_check"] = "PASS",
            ["no_order"] = true,
            ["no_fix"] = true,
            ["no_account_api"] = true,
            ["no_database_write"] = true,
            ["no_databento"] = true,
            ["completed_utc"] = now.ToString("O"),
            ["valid_until_utc"] = now.AddSeconds(1800).ToString("O"),
            ["maximum_age_seconds"] = 1800,
            ["contains_lmax_report_data"] = false
        };
        await File.WriteAllTextAsync(Path.Combine(outputRoot,
            "core-runtime-prequalification.json"), qualification.ToJsonString());
        await File.WriteAllTextAsync(Path.Combine(outputRoot,
            "runner-tests.stdout.log"), "tests 156\npass 156\nfail 0\n");
        await File.WriteAllTextAsync(Path.Combine(outputRoot,
            "runner-tests.stderr.log"), string.Empty);

        var files = new JsonArray();
        foreach (var name in new[] { "core-runtime-prequalification.json",
                     "runner-tests.stderr.log", "runner-tests.stdout.log" })
        {
            var path = Path.Combine(outputRoot, name);
            files.Add(new JsonObject
            {
                ["relative_path"] = name,
                ["bytes"] = checked((int)new FileInfo(path).Length),
                ["sha256"] = ShaFile(path)
            });
        }
        var manifest = new JsonObject
        {
            ["contract"] = Arch7bCoreRuntimePrequalificationAdapter.ManifestContract,
            ["repository"] = "phu-qqb/QQ.Production.Core",
            ["core_head"] = Arch7bOneShotContracts.CoreCommit,
            ["core_tree"] = Arch7bOneShotContracts.CoreTree,
            ["created_utc"] = now.ToString("O"),
            ["valid_until_utc"] = now.AddSeconds(1800).ToString("O"),
            ["maximum_age_seconds"] = 1800,
            ["file_count"] = 3,
            ["files"] = files,
            ["no_order"] = true,
            ["no_fix"] = true,
            ["no_account_api"] = true,
            ["no_database_write"] = true,
            ["no_databento"] = true,
            ["contains_lmax_report_data"] = false
        };
        manifest["prequalification_sha256"] = ShaCanonical(manifest);
        await File.WriteAllTextAsync(Path.Combine(outputRoot,
            "prequalification-manifest.json"), manifest.ToJsonString());
        var native = new JsonObject
        {
            ["qualification"] = qualification.DeepClone(),
            ["manifest"] = manifest.DeepClone()
        };

        var normalized = await new Arch7bCoreRuntimePrequalificationAdapter(
            new Arch7bTestTimeProvider(now)).AdaptAsync(
            native.ToJsonString(), Command(outputRoot), root);

        Assert.Equal("ARCH7B_CORE_RUNTIME_PREQUALIFICATION_QUALIFIED",
            normalized.ResultCode);
        Assert.Equal(3, normalized.NativeArtifactCount);
        Assert.Equal(3, normalized.ArtifactSha256.Distinct().Count());

        ((JsonObject)native["qualification"]!)["tests_passed"] = 154;
        ((JsonObject)native["qualification"]!)["tests_total"] = 154;
        var obsoleteCountFailure =
            await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
                new Arch7bCoreRuntimePrequalificationAdapter(
                    new Arch7bTestTimeProvider(now)).AdaptAsync(
                    native.ToJsonString(), Command(outputRoot), root));
        Assert.Equal(Arch7bV2Blockers.ChildAdapterContractMismatch,
            obsoleteCountFailure.BlockerCode);

        ((JsonObject)native["qualification"]!)["tests_passed"] =
            Arch7bOneShotContracts.ExpectedCorePrequalificationTestCount;
        ((JsonObject)native["qualification"]!)["tests_total"] =
            Arch7bOneShotContracts.ExpectedCorePrequalificationTestCount;
        ((JsonObject)native["qualification"]!)["package_json_sha256"] =
            new string('9', 64);
        var failure = await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
            new Arch7bCoreRuntimePrequalificationAdapter(
                new Arch7bTestTimeProvider(now)).AdaptAsync(
                native.ToJsonString(), Command(outputRoot), root));
        Assert.Equal(Arch7bBlockers.ChildOutputShaMismatch, failure.BlockerCode);
    }

    private static Arch7bOneShotMaterializedCommand Command(string outputRoot)
    {
        var executable = typeof(Arch7bCoreRuntimePrequalificationAdapter)
            .Assembly.Location;
        return new(Arch7bV2Contracts.MaterializedCommandVersion,
            "core-runtime-prequalification", "CORE_PREQUALIFICATION",
            Arch7bExecutionKind.ChildInvoke, executable, ShaFile(executable),
            ["--output-root", outputRoot], Path.GetDirectoryName(outputRoot)!,
            "core-prequalification-v1",
            Arch7bV2Contracts.ChildResultAdapterVersion,
            Arch7bCoreRuntimePrequalificationAdapter.NativeContract,
            30, 1_048_576, 1_048_576, "qualification-child-process",
            false, false, false, [], [], null,
            Path.Combine(Path.GetDirectoryName(outputRoot)!, "command-authority.json"),
            new string('a', 64), new string('b', 64));
    }

    private static string ShaCanonical(JsonNode value)
    {
        using var document = JsonDocument.Parse(value.ToJsonString());
        return Arch7bNativeAdapterJson.ShaText(
            Arch7bNativeAdapterJson.Canonical(document.RootElement));
    }

    private static string ShaFile(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
