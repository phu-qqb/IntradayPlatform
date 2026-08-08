using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public static class Arch7bQualificationFakeChild
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<int> RunAsync(IReadOnlyDictionary<string, string> options)
    {
        var stage = Required(options, "stage");
        var commandId = Required(options, "command-id");
        var runRoot = Path.GetFullPath(Required(options, "run-root"));
        var behavior = Required(options, "behavior");
        Directory.CreateDirectory(runRoot);
        if (behavior == "timeout")
        {
            await Task.Delay(TimeSpan.FromMinutes(5)).ConfigureAwait(false);
            return 3;
        }
        if (behavior == "invalid-json")
        {
            Console.Write("not-json");
            return 0;
        }
        if (behavior == "secret-sentinel")
        {
            Console.Write("ARCH7B_SECRET_SENTINEL");
            return 0;
        }

        var artifactPath = Path.Combine(runRoot, commandId + ".evidence.json");
        var artifact = JsonSerializer.Serialize(new
        {
            contractVersion = Arch7bOneShotContracts.StageEvidenceVersion,
            stage,
            commandId,
            noOrder = true
        }, JsonOptions) + Environment.NewLine;
        await File.WriteAllTextAsync(artifactPath, artifact, new UTF8Encoding(false)).ConfigureAwait(false);
        var artifactSha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(artifact)));
        if (behavior == "marker")
        {
            var marker = Path.GetFullPath(Required(options, "marker-path"));
            await File.WriteAllTextAsync(marker, "synthetic-marker", new UTF8Encoding(false)).ConfigureAwait(false);
        }
        var resultCode = behavior switch
        {
            "blocker" => "ARCH7B_" + stage + "_FAILED",
            "expected-blocker" => Arch7bOneShotContracts.ExpectedFinalBlocker,
            _ => "SUCCESS"
        };
        var paths = behavior == "missing-evidence" ? new[] { artifactPath + ".missing" } : new[] { artifactPath };
        var hashes = behavior == "bad-sha" ? new[] { new string('0', 64) } : new[] { artifactSha };
        Console.Write(JsonSerializer.Serialize(new Arch7bChildOutputEnvelope(
            Arch7bOneShotContracts.StageEvidenceVersion, resultCode, paths, hashes), JsonOptions));
        return behavior is "blocker" or "expected-blocker" ? 2 : 0;
    }

    private static string Required(IReadOnlyDictionary<string, string> options, string key) =>
        options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"MISSING_REQUIRED_ARGUMENT:{key}");
}
