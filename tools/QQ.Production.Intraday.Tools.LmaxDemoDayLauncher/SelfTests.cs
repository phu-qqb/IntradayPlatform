using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tools.LmaxDemoDayLauncher;

internal static class SelfTests
{
    internal static void Run()
    {
        var checks = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception("SELF_TEST_FAILED_" + name); checks++; }
        void Reject(Action action, string name)
        {
            try { action(); } catch (InvalidOperationException) { checks++; return; }
            throw new Exception("SELF_TEST_ACCEPTED_" + name);
        }
        var t = Files.Utc("2026-09-17T12:00:00Z");
        JsonElement Quote(string symbol, DateTimeOffset at, decimal bid, decimal ask, long seq) => JsonSerializer.SerializeToElement(new
        {
            event_type = "BBO_UPDATED", symbol, environment = "DEMO", source_component = "LMAX_MARKET_DATA_CAPTURE_ONLY", venue = "LMAX_DEMO_READ_ONLY",
            book_valid = true, source_timestamp_utc = at.ToString("o"), bid_price = bid, ask_price = ask, process_event_sequence = seq, event_id = seq.ToString()
        });
        var declared = JsonSerializer.SerializeToElement(new { event_type = "MARKET_DATA_SUBSCRIPTION_STATE", symbol = "EURUSD" });
        var events = new[] { declared, Quote("EURUSD", t.AddSeconds(-1), 1.10m, 1.12m, 1),
            Quote("EURUSD", t, 1.20m, 1.24m, 2), Quote("EURUSD", t, 1.22m, 1.26m, 3), Quote("EURUSD", t.AddTicks(1), 9m, 10m, 4) };
        var selected = Boundary.Select(events, t).Single();
        Check(selected.Sequence == 3 && selected.At == t, "CUTOFF_AND_SEQUENCE");
        Check((selected.Bid + selected.Ask) / 2m == 1.24m, "DECIMAL_MID");
        Check(Boundary.Select([declared, Quote("EURUSD", t.AddSeconds(-300), 1m, 2m, 1)], t).Count == 1, "AGE_INCLUSIVE");
        Reject(() => Boundary.Select([declared, Quote("EURUSD", t.AddSeconds(-300).AddTicks(-1), 1m, 2m, 1)], t), "STALE");
        Reject(() => Boundary.Select([declared, Quote("EURUSD", t.AddTicks(1), 1m, 2m, 1)], t), "FUTURE_ONLY");
        Reject(() => Boundary.Select([declared, Quote("EURUSD", t, 2m, 1m, 1)], t), "CROSSED");
        Reject(() => Boundary.Select([declared, Quote("GBPUSD", t, 1m, 2m, 1)], t), "MISSING_DECLARED");
        Reject(() => Boundary.Select([Quote("EURUSD", t, 1m, 2m, 1)], t), "UNDECLARED");
        Check(LmaxDemoDaySchedule.IsEligible("INFX9", t) && LmaxDemoDaySchedule.IsEligible("INFX10", t), "EU_HOUR");
        Check(!LmaxDemoDaySchedule.IsEligible("INFX7", t), "US_BEFORE_OPEN");
        Check(!LmaxDemoDaySchedule.IsEligible("INFX10", t.AddMinutes(15)), "NO_HOURLY_CARRY");
        Check(LmaxDemoDaySchedule.IsEligible("INFX9", t.AddDays(2)), "OWNER_ALL_DAYS");
        Check(!LmaxDemoDaySchedule.IsEligible("INFX9", Files.Utc("2026-09-17T15:45:00Z")), "EU_EXIT");
        Check(LmaxDemoDaySchedule.IsFinalExit(Files.Utc("2026-09-17T18:45:00Z")), "US_FINAL_EXIT");
        Check(LmaxDemoDaySchedule.FinalClose(Files.Utc("2026-01-15T12:00:00Z")) == Files.Utc("2026-01-15T20:00:00Z"), "WINTER_DST");
        Check(Pipeline.NextCutoff(t.AddMinutes(13)) == t.AddMinutes(30), "LATE_CAPTURE_SKIPPED");
        Reject(() => Pipeline.ValidateCutoff(t, t), "NO_REPLAY");
        Reject(() => Pipeline.ValidateCutoff(t.AddSeconds(1), t.AddMinutes(-5)), "NO_SHIFTED_CUTOFF");
        Reject(() => Pipeline.WaitUntil(DateTimeOffset.UtcNow.AddSeconds(5),
            () => throw new InvalidOperationException("FAULTED_OWNER")).GetAwaiter().GetResult(), "FAULTED_OWNER_ABORTS_PENDING_CAPTURE");
        var hash = new string('a', 64);
        var cycle = t.ToString("yyyyMMddTHHmmssZ");
        var result = JsonSerializer.SerializeToElement(new { schema = "lmax_demo_anubis_result_v1", cycle_id = cycle, programme = "INFX9",
            cutoff_utc = t.ToString("o"), effective_at_utc = t.AddMinutes(15).ToString("o"), aggregated_weights_sha256 = hash, no_order = true, broker_send_status = "DISABLED_NO_ORDER_ENTRY" });
        Pipeline.ValidateResult(result, "INFX9", cycle, t, hash); checks++;
        Reject(() => Pipeline.ValidateResult(result, "INFX10", cycle, t, hash), "WRONG_PROGRAMME");
        Reject(() => Pipeline.ValidateResult(result, "INFX9", cycle, t.AddMinutes(15), hash), "STALE_RESULT");
        Reject(() => Pipeline.ValidateResult(result, "INFX9", cycle, t, new string('b', 64)), "CHANGED_WEIGHTS");
        var secret = new Dictionary<string, string>();
        foreach (var prefix in new[] { "ORDER", "MARKETDATA" })
        {
            secret["QQ_LMAX_FIX_" + prefix + "_USERNAME"] = "simulated-username";
            secret["QQ_LMAX_FIX_" + prefix + "_PASSWORD"] = "simulated-not-a-credential";
            secret["QQ_LMAX_FIX_" + prefix + "_SENDER_COMP_ID"] = "legacy-sender";
            secret["QQ_LMAX_FIX_" + prefix + "_TARGET_COMP_ID"] = "simulated-target";
            secret["QQ_LMAX_FIX_" + prefix + "_PORT"] = "443";
        }
        secret["QQ_LMAX_FIX_ORDER_HOST"] = "fix-order.london-demo.lmax.com";
        secret["QQ_LMAX_FIX_MARKETDATA_HOST"] = "fix-marketdata.london-demo.lmax.com";
        var bound = SessionBindings.Bind(JsonSerializer.SerializeToElement(secret));
        Check(bound["QQ_LMAX_FIX_SENDER_COMP_ID"] == "simulated-username", "POST_LOGON_SENDER_CONTINUITY");
        Check(bound["QQ_LMAX_ALLOW_LIVE_TRADING"] == "false" && bound["Safety__AllowLiveTrading"] == "false", "DEMO_ONLY_BINDING");
        Check(bound["QQ_LMAX_DEMO_ORDER_CAPS_ENABLED"] == "false", "OWNER_REMOVED_DEMO_CAPS");
        Check(bound["QQ_LMAX_MARKET_DATA_REQUEST_MODE"] == "SnapshotPlusUpdates"
            && bound["QQ_LMAX_MARKET_DATA_SYMBOL_ENCODING_MODE"] == "SecurityId", "LMAX_ACCEPTED_BBO_REQUEST_BINDING");
        secret["QQ_LMAX_FIX_MARKETDATA_USERNAME"] = "different-simulated-user";
        Reject(() => SessionBindings.Bind(JsonSerializer.SerializeToElement(secret)), "MISMATCHED_MD_CREDENTIALS");
        var observation = new LmaxDemoSessionStart("simulated-test", "1754288005", "Demo", "test-approval", t, LmaxDemoDaySchedule.FinalClose(t),
            true, true, true, false, [new("EURUSD", "4001", 10000m)], 900, "LMAX_DEMO_LOCAL");
        SessionBindings.ValidateObservation(observation, t.AddSeconds(900)); checks++;
        Reject(() => SessionBindings.ValidateObservation(observation, t.AddSeconds(901)), "STALE_START_OBSERVATION");
        Reject(() => SessionBindings.ValidateObservation(observation with { Simulated = true }, t), "SIMULATED_START");
        Reject(() => SessionBindings.ValidateObservation(observation with { ObservedNoWorkingOrders = false }, t), "UNKNOWN_WORKING_ORDERS");
        var captureSecret = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["LMAX_DEMO_SENDER_COMP_ID"] = "simulated-sender", ["LMAX_DEMO_TARGET_COMP_ID"] = "simulated-target",
            ["LMAX_DEMO_FIX_USERNAME"] = "simulated-user", ["LMAX_DEMO_FIX_PASSWORD"] = "simulated-not-a-credential"
        });
        var captureBindings = Runtime.BindCaptureCredentials(captureSecret);
        Check(captureBindings.Count == 4 && captureBindings["LMAX_DEMO_FIX_USERNAME"] == "simulated-user", "CAPTURE_JSON_BINDINGS");
        Check(Runtime.BindCaptureCredentials(" \n\uFEFF" + captureSecret).OrderBy(x => x.Key).SequenceEqual(captureBindings.OrderBy(x => x.Key)), "CAPTURE_NATIVE_BOM");
        Reject(() => Runtime.BindCaptureCredentials("unknown-prefix" + captureSecret), "CAPTURE_UNKNOWN_PREFIX");
        Reject(() => Runtime.BindCaptureCredentials("{}"), "CAPTURE_MISSING_BINDINGS");
        var awsInfo = Runtime.StartInfo("aws.exe", ["--version"]);
        Check(awsInfo.StandardOutputEncoding?.CodePage == 65001 && awsInfo.Environment["PYTHONIOENCODING"] == "utf-8", "AWS_UTF8_TRANSPORT");
        var temp = Path.Combine(Path.GetTempPath(), "lmax-launcher-self-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var path = Path.Combine(temp, "result.json");
            Files.Atomic(path, new { value = 1 });
            var before = Files.Hash(path);
            try { Files.Atomic(path, new { value = 2 }); throw new Exception("OVERWRITE_ACCEPTED"); } catch (IOException) { checks++; }
            Check(Files.Hash(path) == before, "IMMUTABLE_FILE");
            var leasePath = Path.Combine(temp, "launcher.lock");
            var lease = Runtime.AcquireLauncherLease(leasePath);
            try { Reject(() => { using var duplicate = Runtime.AcquireLauncherLease(leasePath); }, "DUPLICATE_LAUNCHER"); }
            finally { Task.Run(() => lease.Dispose()).GetAwaiter().GetResult(); }
            using var reacquired = Runtime.AcquireLauncherLease(leasePath);
            Check(reacquired.CanWrite, "LEASE_RELEASED_ON_ANOTHER_THREAD");
        }
        finally { Directory.Delete(temp, true); }
        Console.WriteLine(JsonSerializer.Serialize(new { marker = "DEMO_LAUNCHER_SELF_TEST_PASS", checks, externalCalls = 0, brokerSends = 0 }));
    }
}
