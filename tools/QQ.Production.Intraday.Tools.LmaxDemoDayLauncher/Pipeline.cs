using System.Diagnostics;
using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Application.CanonicalRecorder;
using QQ.Production.Intraday.Infrastructure.Lmax.MarketDataOnly;

namespace QQ.Production.Intraday.Tools.LmaxDemoDayLauncher;

internal sealed record Programme(string Name, int Universe, int Model, string Session, int Frequency, decimal Coefficient);
internal sealed record Contribution(string ProgramName, int UniverseId, int ModelId, string Session, int FrequencyMinutes,
    decimal Coefficient, int State, DateTimeOffset? AsOfUtc, string? ExecDeskWeightFilePath, string? ExpectedExecDeskWeightFileSha256,
    string? AggregatedWeightsFilePath, string? ExpectedAggregatedWeightsFileSha256, string? Reason);
internal sealed record CycleManifest(string SchemaVersion, string CycleId, string CaptureRunRoot, string ExpectedFinalManifestSha256,
    DateTimeOffset DecisionAtUtc, DateTimeOffset EffectiveAtUtc, int MaximumSourceAgeSeconds, string FundCode, string ModelName,
    decimal NavUsd, int TargetQuantityMode, IReadOnlyList<Contribution> Programmes, IReadOnlyList<string>? DemoScheduledExitScope);

internal static class Pipeline
{
    internal static readonly Programme[] Programmes =
    [new("INFX7", 54, 10, "US", 15, 4.5m), new("INFX8", 57, 11, "US", 30, 2.1m),
     new("INFX9", 58, 12, "EU", 15, 1.4m), new("INFX10", 59, 13, "EU", 60, .6m)];

    internal static DateTimeOffset NextCutoff(DateTimeOffset now)
    {
        var ticks = TimeSpan.FromMinutes(15).Ticks;
        var cutoff = new DateTimeOffset((now.UtcTicks / ticks + 1) * ticks, TimeSpan.Zero);
        // Collector preparation is completed before its two-minute pre-cutoff start.
        while (cutoff.AddMinutes(-2) <= now.AddSeconds(30)) cutoff = cutoff.AddMinutes(15);
        while (!Programmes.Any(p => LmaxDemoDaySchedule.IsEligible(p.Name, cutoff)) && !LmaxDemoDaySchedule.IsFinalExit(cutoff))
        {
            cutoff = cutoff.AddMinutes(15);
            Files.Require(cutoff < LmaxDemoDaySchedule.FinalClose(now), "NO_ELIGIBLE_CYCLE_REMAINS_TODAY");
        }
        return cutoff;
    }
    internal static void ValidateCutoff(DateTimeOffset cutoff, DateTimeOffset now)
    {
        Files.Require(cutoff.Offset == TimeSpan.Zero && cutoff.UtcTicks % TimeSpan.FromMinutes(15).Ticks == 0, "M15_CUTOFF_REQUIRED");
        Files.Require(cutoff.AddMinutes(-2) > now && cutoff.Date == now.UtcDateTime.Date
            && cutoff < LmaxDemoDaySchedule.FinalClose(now), "FUTURE_SAME_DAY_CAPTURE_REQUIRED");
        Files.Require(Programmes.Any(p => LmaxDemoDaySchedule.IsEligible(p.Name, cutoff)) || LmaxDemoDaySchedule.IsFinalExit(cutoff), "NO_ELIGIBLE_PROGRAMME");
    }
    internal static void ValidateResult(JsonElement manifest, string programme, string cycleId, DateTimeOffset cutoff, string weightHash)
    {
        Files.Require(manifest.GetProperty("schema").GetString() == "lmax_demo_anubis_result_v1"
            && manifest.GetProperty("cycle_id").GetString() == cycleId
            && manifest.GetProperty("programme").GetString() == programme
            && Files.Utc(manifest.GetProperty("cutoff_utc").GetString()!) == cutoff
            && Files.Utc(manifest.GetProperty("effective_at_utc").GetString()!) == cutoff.AddMinutes(15)
            && manifest.GetProperty("aggregated_weights_sha256").GetString() == weightHash
            && manifest.GetProperty("no_order").GetBoolean()
            && manifest.GetProperty("broker_send_status").GetString() == "DISABLED_NO_ORDER_ENTRY", "ANUBIS_RESULT_LINEAGE_INVALID");
    }
    internal static async Task WaitUntil(DateTimeOffset at, Action? requireOwner = null)
    {
        while (at > DateTimeOffset.UtcNow)
        {
            requireOwner?.Invoke();
            var left = at - DateTimeOffset.UtcNow;
            if (left > TimeSpan.Zero) await Task.Delay(left < TimeSpan.FromSeconds(10) ? left : TimeSpan.FromSeconds(10));
        }
    }
    internal static async Task<CycleManifest> Prepare(DateTimeOffset cutoff, Action? requireOwner = null)
    {
        ValidateCutoff(cutoff, DateTimeOffset.UtcNow);
        Runtime.VerifyLocal();
        await Runtime.VerifyTransport();
        ValidateCutoff(cutoff, DateTimeOffset.UtcNow);
        var cycleId = cutoff.ToString("yyyyMMddTHHmmssZ");
        var root = Path.Combine(Runtime.Root, cycleId);
        Files.Require(!Directory.Exists(root), "CYCLE_REPLAY_OR_UNRESOLVED");
        Directory.CreateDirectory(root);
        Files.Atomic(Path.Combine(root, "attempt.json"), new { cycleId, cutoffUtc = cutoff, effectiveAtUtc = cutoff.AddMinutes(15), startedAtUtc = DateTimeOffset.UtcNow,
            automaticRetryAllowed = false, document = Runtime.Document, documentVersion = "1", documentHash = Runtime.DocumentHash, target = Runtime.Gpu });
        void Stage(string name)
        {
            requireOwner?.Invoke();
            Console.WriteLine(JsonSerializer.Serialize(new { cycleId, stage = name, atUtc = DateTimeOffset.UtcNow }));
        }
        try
        {
            var configDirectory = Path.Combine(root, "config");
            Directory.CreateDirectory(configDirectory);
            var config = JsonSerializer.Deserialize<LmaxMarketDataOnlyPreflightConfig>(File.ReadAllText(Runtime.Template), CanonicalRecorderV2Constants.JsonOptions)!;
            config = config with { OutputRoot = Path.Combine(root, "capture"), ConfigHash = "" };
            config = config with { ConfigHash = LmaxMarketDataOnlyConfigHash.Compute(config) };
            var configPath = Path.Combine(configDirectory, "capture.json");
            await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config, CanonicalRecorderV2Constants.JsonOptions));
            File.Copy(Runtime.Catalog, Path.Combine(configDirectory, Path.GetFileName(Runtime.Catalog)), false);
            Stage("CAPTURE_PREFLIGHT");
            await Runtime.Run(Runtime.Dotnet, [Runtime.Collector, "preflight", "--config", configPath, "--output", Path.Combine(root, "preflight"),
                "--network-disabled", "--no-order-entry", "--no-account-api", "--no-db"], cutoff.AddMinutes(-2), "CAPTURE_PREFLIGHT", Path.Combine(root, "preflight-process"));
            var credentials = await Runtime.CaptureCredentials();
            Files.Require(DateTimeOffset.UtcNow < cutoff.AddMinutes(-2), "CAPTURE_PREPARATION_MISSED_START");
            Stage("WAITING_CAPTURE_START");
            await WaitUntil(cutoff.AddMinutes(-2), requireOwner);
            Stage("CAPTURING");
            try
            {
                await Runtime.Run(Runtime.Dotnet, [Runtime.Collector, "capture", "--config", configPath, "--operator-approved-market-data-fix-logon",
                    "--no-order-entry", "--no-account-api", "--no-db"], cutoff.AddMinutes(5), "CAPTURE", Path.Combine(root, "capture-process"), credentials);
            }
            finally { credentials.Clear(); }
            var captureResult = Files.Read(Path.Combine(config.OutputRoot, "m2c1b_capture_command_result.json"));
            Files.Require(captureResult.GetProperty("status").GetString() == "GO_M2C2_CAPTURE_VALIDATED", "CAPTURE_NOT_VALIDATED");
            Stage("NORMALIZING");
            var boundary = await Boundary.Normalize(config.OutputRoot, cutoff, cycleId, Path.Combine(root, "input"));
            var effective = cutoff.AddMinutes(15);
            var selected = Programmes.Where(p => LmaxDemoDaySchedule.IsEligible(p.Name, cutoff)).ToArray();
            var contributions = new List<Contribution>();
            if (selected.Length > 0)
            {
                Stage("UPLOADING_INPUT");
                foreach (var file in new[] { boundary.InputPath, boundary.EvidencePath })
                    await Runtime.Aws(effective, "INPUT_UPLOAD", "s3api", "put-object", "--bucket", Runtime.Bucket, "--key",
                        $"intraday/lmax-input-transfer/{cycleId}/{Path.GetFileName(file)}", "--body", file, "--if-none-match", "*");
                Files.Require(DateTimeOffset.UtcNow < effective, "CYCLE_DEADLINE_EXPIRED");
                // Persist a pre-send receipt. Never automatically repeat an ambiguous SendCommand.
                Files.Atomic(Path.Combine(root, "ssm-dispatch-attempt.json"), new { cycleId, atUtc = DateTimeOffset.UtcNow, automaticRetryAllowed = false });
                var parameters = JsonSerializer.Serialize(new Dictionary<string, string[]> { ["CycleId"] = [cycleId],
                    ["CutoffUtc"] = [cutoff.ToString("yyyy-MM-ddTHH:mm:ssZ")], ["Programmes"] = [string.Join(',', selected.Select(p => p.Name))] });
                using var dispatch = JsonDocument.Parse(await Runtime.Aws(effective, "SSM_DISPATCH", "ssm", "send-command", "--instance-ids", Runtime.Gpu,
                    "--document-name", Runtime.Document, "--document-version", "1", "--document-hash", Runtime.DocumentHash,
                    "--document-hash-type", "Sha256", "--parameters", parameters, "--timeout-seconds", "900"));
                var commandId = dispatch.RootElement.GetProperty("Command").GetProperty("CommandId").GetString()!;
                Files.Atomic(Path.Combine(root, "ssm-command.json"), new { cycleId, commandId, target = Runtime.Gpu, atUtc = DateTimeOffset.UtcNow });
                Stage("ANUBIS_RUNNING");
                while (true)
                {
                    Files.Require(DateTimeOffset.UtcNow < effective, "ANUBIS_DEADLINE_EXPIRED");
                    // list-command-invocations tolerates the command's initial eventual-consistency gap.
                    using var status = JsonDocument.Parse(await Runtime.Aws(effective, "SSM_POLL", "ssm", "list-command-invocations", "--command-id", commandId, "--instance-id", Runtime.Gpu));
                    var invocations = status.RootElement.GetProperty("CommandInvocations").EnumerateArray().ToArray();
                    if (invocations.Length > 0)
                    {
                        Files.Require(invocations.Length == 1, "SSM_INVOCATION_AMBIGUOUS");
                        var state = invocations[0].GetProperty("Status").GetString();
                        if (state == "Success") break;
                        Files.Require(state is "Pending" or "InProgress" or "Delayed", "ANUBIS_COMMAND_FAILED");
                    }
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }
            Stage("EXECDESK_HANDOFF");
            foreach (var p in Programmes)
            {
                if (!selected.Contains(p))
                {
                    contributions.Add(new(p.Name, p.Universe, p.Model, p.Session, p.Frequency, p.Coefficient, 1, null, null, null, null, null,
                        LmaxDemoDaySchedule.IsFinalExit(cutoff) ? "SCHEDULED_DEMO_SESSION_EXIT" : "NOT_ELIGIBLE_AT_CURRENT_CUTOFF"));
                    continue;
                }
                var resultRoot = Path.Combine(root, p.Name);
                Directory.CreateDirectory(resultRoot);
                foreach (var name in new[] { "v1-aggregated-weights.txt", "v1-result-manifest.json" })
                    await Runtime.Aws(effective, "RESULT_DOWNLOAD", "s3api", "get-object", "--bucket", Runtime.Bucket, "--key",
                        $"intraday/lmax-input-transfer/{cycleId}/anubis-results/{p.Name}/{name}", Path.Combine(resultRoot, name));
                var aggregated = Path.Combine(resultRoot, "v1-aggregated-weights.txt");
                var hash = Files.Hash(aggregated);
                ValidateResult(Files.Read(Path.Combine(resultRoot, "v1-result-manifest.json")), p.Name, cycleId, cutoff, hash);
                Runtime.VerifyLocal();
                var weights = Path.Combine(resultRoot, "execdesk-weights.txt");
                var evidence = Path.Combine(resultRoot, "execdesk-evidence.json");
                await Runtime.Run(Runtime.Writer, [p.Name, p.Universe.ToString(), aggregated, hash, weights, evidence], effective,
                    "EXECDESK", Path.Combine(resultRoot, "execdesk-process"));
                Files.Require(Files.Hash(aggregated) == hash, "ANUBIS_OUTPUT_MUTATED");
                contributions.Add(new(p.Name, p.Universe, p.Model, p.Session, p.Frequency, p.Coefficient, 0, cutoff,
                    weights, Files.Hash(weights), aggregated, hash, null));
            }
            Files.Require(DateTimeOffset.UtcNow < effective, "PREPARED_CYCLE_EXPIRED");
            var manifest = new CycleManifest("lmax-demo-cycle-v1", cycleId, boundary.CaptureRunRoot, boundary.FinalManifestSha256,
                cutoff, effective, 300, "QQ Intraday Fund", "IntradayFxModel", 1_000_000m, 0, contributions,
                LmaxDemoDaySchedule.IsFinalExit(cutoff) ? ["EURUSD"] : null);
            Files.Atomic(Path.Combine(root, "prepared-cycle.json"), manifest);
            Files.Atomic(Path.Combine(root, "prepared-result.json"), new { cycleId, status = "Prepared", completedAtUtc = DateTimeOffset.UtcNow,
                effectiveAtUtc = effective, symbolCount = boundary.SymbolCount, manifestSha256 = Files.Hash(Path.Combine(root, "prepared-cycle.json")), brokerHandoffPerformed = false });
            Stage("PREPARED");
            return manifest;
        }
        catch (Exception e)
        {
            Files.Atomic(Path.Combine(root, "blocked.json"), new { cycleId, status = "Blocked", atUtc = DateTimeOffset.UtcNow, errorType = e.GetType().Name,
                code = e is InvalidOperationException ? e.Message : "UNEXPECTED_STAGE_FAILURE", automaticRetryAllowed = false });
            throw;
        }
    }
}
