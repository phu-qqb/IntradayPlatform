using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using QQ.Production.Intraday.Application.CanonicalRecorder;

namespace QQ.Production.Intraday.Tools.LmaxDemoDayLauncher;

internal static class Files
{
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    internal static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }
    internal static JsonElement Read(string path) => JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    internal static void Atomic(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, value, Json);
            stream.Flush(true);
        }
        File.Move(temp, path, false);
    }
    internal static DateTimeOffset Utc(string value)
    {
        var result = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
        Require(result.Offset == TimeSpan.Zero, "UTC_REQUIRED");
        return result;
    }
}

internal sealed record BoundaryResult(string CaptureRunRoot, string FinalManifestSha256, string InputPath, string EvidencePath, int SymbolCount);
internal sealed record Quote(string Symbol, decimal Bid, decimal Ask, DateTimeOffset At, long Sequence, string EventId);

internal static class Boundary
{
    // Same decimal arithmetic and at-or-before selection as Normalize-LmaxDemoBoundary.ps1.
    // The production path additionally requires the canonical replay validator to pass.
    internal static IReadOnlyList<Quote> Select(IEnumerable<JsonElement> events, DateTimeOffset cutoff)
    {
        var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var latest = new Dictionary<string, Quote>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in events)
        {
            var type = e.GetProperty("event_type").GetString();
            var symbol = e.TryGetProperty("symbol", out var sym) ? sym.GetString() : null;
            if (type == "MARKET_DATA_SUBSCRIPTION_STATE" && !string.IsNullOrWhiteSpace(symbol))
            {
                declared.Add(symbol);
                continue;
            }
            if (type != "BBO_UPDATED" || string.IsNullOrWhiteSpace(symbol)
                || e.GetProperty("environment").GetString() != "DEMO"
                || e.GetProperty("source_component").GetString() != "LMAX_MARKET_DATA_CAPTURE_ONLY"
                || e.GetProperty("venue").GetString() != "LMAX_DEMO_READ_ONLY"
                || e.GetProperty("book_valid").ValueKind != JsonValueKind.True) continue;
            if (!e.TryGetProperty("source_timestamp_utc", out var timestamp) || timestamp.ValueKind != JsonValueKind.String) continue;
            var at = Files.Utc(timestamp.GetString()!);
            if (at > cutoff || cutoff - at > TimeSpan.FromSeconds(300)) continue;
            if (!e.TryGetProperty("bid_price", out var bidValue) || !bidValue.TryGetDecimal(out var bid)
                || !e.TryGetProperty("ask_price", out var askValue) || !askValue.TryGetDecimal(out var ask)
                || bid <= 0m || ask <= bid) continue;
            var quote = new Quote(symbol, bid, ask, at, e.GetProperty("process_event_sequence").GetInt64(), e.GetProperty("event_id").GetString()!);
            if (!latest.TryGetValue(symbol, out var old) || at > old.At || at == old.At && quote.Sequence > old.Sequence)
                latest[symbol] = quote;
        }
        Files.Require(declared.Count > 0, "CAPTURE_DECLARED_SYMBOLS_MISSING");
        Files.Require(declared.All(latest.ContainsKey), "CAPTURE_BBO_MISSING_OR_STALE");
        return declared.Order(StringComparer.Ordinal).Select(x => latest[x]).ToArray();
    }

    internal static async Task<BoundaryResult> Normalize(string root, DateTimeOffset cutoff, string cycleId, string output)
    {
        Files.Require(cycleId == cutoff.ToString("yyyyMMddTHHmmssZ"), "CYCLE_CUTOFF_MISMATCH");
        var manifests = Directory.GetFiles(root, "final_manifest.json", SearchOption.AllDirectories);
        Files.Require(manifests.Length == 1, "CAPTURE_FINAL_MANIFEST_AMBIGUOUS");
        var finalPath = manifests[0];
        var captureRoot = Path.GetDirectoryName(finalPath)!;
        var final = Files.Read(finalPath);
        Files.Require(final.GetProperty("finalized").GetBoolean() && final.GetProperty("environment").GetString() == "DEMO"
            && final.GetProperty("writer_state").GetString() == "OK", "CAPTURE_FINALIZATION_INVALID");
        var replay = await new CanonicalRecorderV2Replayer().ReplaySnapshotAsync(captureRoot, CancellationToken.None);
        Files.Require(replay.ReplayReport.Status == "PASS", "CAPTURE_REPLAY_FAILED");
        IEnumerable<JsonElement> Events()
        {
            foreach (var chunk in final.GetProperty("chunks").EnumerateArray())
            {
                var relative = chunk.GetProperty("file").GetString()!;
                var path = Path.GetFullPath(Path.Combine(captureRoot, relative));
                Files.Require(!Path.IsPathRooted(relative) && path.StartsWith(captureRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "CAPTURE_CHUNK_PATH_INVALID");
                Files.Require(Files.Hash(path) == chunk.GetProperty("sha256").GetString()!.ToLowerInvariant(), "CAPTURE_CHUNK_HASH_MISMATCH");
                foreach (var line in File.ReadLines(path).Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    using var document = JsonDocument.Parse(line);
                    yield return document.RootElement.Clone();
                }
            }
        }
        var quotes = Select(Events(), cutoff);
        var runHash = final.GetProperty("run_manifest_sha256").GetString()!.ToLowerInvariant();
        var finalHash = Files.Hash(finalPath);
        var inputPath = Path.Combine(output, "lmax-bbo-boundary.json");
        Files.Atomic(inputPath, new
        {
            contract_version = "lmax_bbo_boundary_input_v1",
            boundary_utc = cutoff.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            selection_rule = "latest BBO_UPDATED per recorder-declared LMAX Demo symbol at or before boundary, within 300 seconds",
            capture = new { cycle_id = cycleId, final_manifest_path = finalPath, run_manifest_sha256 = runHash, finalized = true, writer_state = "OK" },
            quotes = quotes.Select(x => new { symbol = x.Symbol, price = (x.Bid + x.Ask) / 2m, bid_price = x.Bid, ask_price = x.Ask,
                source_timestamp_utc = x.At.ToString("o"), event_id = x.EventId }).ToArray()
        });
        var evidencePath = Path.Combine(output, "lmax-bbo-boundary-evidence.json");
        Files.Atomic(evidencePath, new
        {
            schema = "lmax_bbo_boundary_evidence_v1", normalized_input_sha256 = Files.Hash(inputPath),
            source_capture = new
            {
                recorder_run_id = final.GetProperty("recorder_run_id").GetString(),
                started_at_utc = final.GetProperty("start_utc").GetString(), ended_at_utc = final.GetProperty("end_utc").GetString(),
                sha256 = runHash, final_manifest_sha256 = finalHash,
                data_quality_report_sha256 = Files.Hash(Path.Combine(captureRoot, "health", "data_quality_report.json")),
                environment = "DEMO", mode = final.GetProperty("mode").GetString(), event_counts = final.GetProperty("event_counts")
            }
        });
        return new(captureRoot, finalHash, inputPath, evidencePath, quotes.Count);
    }
}
