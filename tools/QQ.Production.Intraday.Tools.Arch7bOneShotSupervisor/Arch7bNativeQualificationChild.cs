using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public static class Arch7bNativeQualificationChild
{
    public static async Task<int> RunAsync(IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken = default)
    {
        var runRoot = Path.GetFullPath(Required(options, "run-root"));
        var commandId = Required(options, "command-id");
        var contract = Required(options, "native-contract");
        var result = Required(options, "native-result");
        var behavior = options.GetValueOrDefault("behavior", "success");
        var artifactCount = int.Parse(options.GetValueOrDefault("artifact-count", "1"));
        Directory.CreateDirectory(runRoot);
        if (behavior == "timeout")
        {
            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false);
            return 3;
        }
        if (behavior == "overflow")
        {
            Console.Write(new string('x', 2_000_000));
            return 4;
        }
        if (behavior == "malformed")
        {
            Console.Write("{not-json");
            return 0;
        }
        if (behavior == "secret-value")
        {
            Console.Write(Environment.GetEnvironmentVariable("ARCH7B_TEST_SECRET") ??
                "ARCH7B_SECRET_SENTINEL");
            return 0;
        }
        if (behavior == "crash") return 5;

        var processKey = options.GetValueOrDefault("process-key");
        if (!string.IsNullOrEmpty(processKey))
        {
            var ready = Path.Combine(runRoot, processKey + ".ready");
            var readyTemporary = ready + ".tmp";
            await File.WriteAllTextAsync(readyTemporary, "READY", new UTF8Encoding(false), cancellationToken)
                .ConfigureAwait(false);
            File.Move(readyTemporary, ready);
            var signal = Path.Combine(runRoot, processKey + ".COMPLETE.signal");
            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while (!File.Exists(signal) && DateTimeOffset.UtcNow < deadline)
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            if (!File.Exists(signal)) return 6;
            File.Delete(signal);
            File.Delete(ready);
        }

        var actualCount = behavior == "wrong-cardinality" ? artifactCount + 1 : artifactCount;
        var artifacts = new List<Arch7bNativeArtifact>();
        for (var index = 0; index < actualCount; index++)
        {
            var path = Path.Combine(runRoot, "native", commandId, $"artifact-{index:D2}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var content = JsonSerializer.Serialize(new
            {
                contract,
                command_id = commandId,
                artifact_index = index,
                no_order = true
            }, Arch7bJson.CanonicalOptions);
            await File.WriteAllTextAsync(path, content, new UTF8Encoding(false), cancellationToken)
                .ConfigureAwait(false);
            var sha = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(path,
                cancellationToken).ConfigureAwait(false)));
            artifacts.Add(new(path, behavior == "wrong-sha" && index == 0 ? new string('0', 64) : sha,
                $"native-{index:D2}"));
        }
        if (behavior == "missing-artifact" && artifacts.Count > 0)
            File.Delete(artifacts[0].Path);
        var nativeResult = behavior == "unknown-status" ? "UNKNOWN_NATIVE_STATUS" : result;
        var payload = new
        {
            contract,
            result = nativeResult,
            artifacts,
            evidence_sha256 = Arch7bOneShotContracts.Sha256(commandId + ":" + nativeResult),
            counts = new { artifacts = artifacts.Count },
            no_order = true
        };
        Console.Write(JsonSerializer.Serialize(payload, Arch7bJson.CanonicalOptions));
        return 0;
    }

    private static string Required(IReadOnlyDictionary<string, string> options, string key) =>
        options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"MISSING_REQUIRED_ARGUMENT:{key}");
}
