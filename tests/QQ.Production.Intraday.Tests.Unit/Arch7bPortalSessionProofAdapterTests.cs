using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bPortalSessionProofAdapterTests
{
    [Fact]
    public async Task Native_demo_session_proof_is_strict_fresh_and_secret_free()
    {
        var now = DateTimeOffset.Parse("2026-08-10T12:00:00Z");
        var proof = new JsonObject
        {
            ["contract"] = Arch7bPortalSessionProofAdapter.NativeContract,
            ["status"] = "ARCH7B_PORTAL_SESSION_PROVEN",
            ["environment"] = "LMAX_LONDON_DEMO",
            ["account_id"] = "1754288005",
            ["portal_origin"] = "https://account.london-demo.lmax.com",
            ["session_mode"] = "manual-session",
            ["authenticated"] = true,
            ["observed_at_utc"] = now.ToString("O"),
            ["valid_until_utc"] = now.AddSeconds(300).ToString("O"),
            ["maximum_age_seconds"] = 300,
            ["browser_context_closed"] = true,
            ["no_bracket"] = true,
            ["no_fix"] = true,
            ["no_order_entry"] = true,
            ["no_order"] = true,
            ["no_account_api"] = true,
            ["credentials_recorded"] = false,
            ["cookies_recorded"] = false,
            ["tokens_recorded"] = false,
            ["html_recorded"] = false,
            ["artifacts"] = new JsonArray()
        };
        proof["evidence_sha256"] = Sha(proof.ToJsonString());
        var runRoot = Path.Combine(Path.GetTempPath(), "arch7b-portal-adapter",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runRoot);
        try
        {
            var command = Command(runRoot);
            var normalized = await new Arch7bPortalSessionProofAdapter(
                new Arch7bTestTimeProvider(now)).AdaptAsync(
                    proof.ToJsonString(new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }), command, runRoot);

            Assert.Equal("ARCH7B_PORTAL_SESSION_PROVEN", normalized.ResultCode);
            Assert.Empty(normalized.ArtifactPaths);
            Assert.Equal(0, normalized.NativeArtifactCount);

            proof["cookies_recorded"] = true;
            var failure = await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
                new Arch7bPortalSessionProofAdapter(new Arch7bTestTimeProvider(now))
                    .AdaptAsync(proof.ToJsonString(), command, runRoot));
            Assert.Equal(Arch7bV2Blockers.ChildAdapterContractMismatch,
                failure.BlockerCode);
        }
        finally
        {
            Directory.Delete(runRoot, true);
        }
    }

    private static Arch7bOneShotMaterializedCommand Command(string runRoot)
    {
        var executable = typeof(Arch7bPortalSessionProofAdapter).Assembly.Location;
        return new(Arch7bV2Contracts.MaterializedCommandVersion,
            "prove-portal-session", "PORTAL_SESSION_PROVEN",
            Arch7bExecutionKind.ChildInvoke, executable,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(executable))),
            [], runRoot, "portal-session-v1",
            Arch7bV2Contracts.ChildResultAdapterVersion,
            Arch7bPortalSessionProofAdapter.NativeContract, 30, 1_048_576,
            1_048_576, "qualification-child-process", false, false, false,
            [], [], null, Path.Combine(runRoot, "command-authority.json"),
            new string('a', 64), new string('b', 64));
    }

    private static string Sha(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
