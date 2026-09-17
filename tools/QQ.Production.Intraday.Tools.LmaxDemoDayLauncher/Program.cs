using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tools.LmaxDemoDayLauncher;

internal static class Program
{
    internal static async Task<int> Main(string[] args)
    {
        try
        {
            var command = args.FirstOrDefault() ?? "plan";
            string Arg(string name)
            {
                var indexes = Enumerable.Range(0, args.Length).Where(i => args[i] == name).ToArray();
                Files.Require(indexes.Length == 1 && indexes[0] + 1 < args.Length, "ARGUMENT_REQUIRED_" + name);
                return args[indexes[0] + 1];
            }
            if (command == "self-test") { SelfTests.Run(); return 0; }
            if (command == "normalize")
            {
                var cutoff = Files.Utc(Arg("--cutoff"));
                Console.WriteLine(JsonSerializer.Serialize(await Boundary.Normalize(Arg("--capture-root"), cutoff, cutoff.ToString("yyyyMMddTHHmmssZ"), Arg("--output")), Files.Json));
                return 0;
            }
            if (command == "plan")
            {
                var cutoff = Pipeline.NextCutoff(DateTimeOffset.UtcNow);
                Console.WriteLine(JsonSerializer.Serialize(new { mode = "PLAN_ONLY", cutoffUtc = cutoff, captureAtUtc = cutoff.AddMinutes(-2), effectiveAtUtc = cutoff.AddMinutes(15),
                    programmes = Pipeline.Programmes.Where(p => LmaxDemoDaySchedule.IsEligible(p.Name, cutoff)).Select(p => p.Name), externalCalls = 0, brokerHandoffPerformed = false }, Files.Json));
                return 0;
            }
            Runtime.VerifyLocal();
            if (command == "inspect-worker") { await SessionBindings.Inspect(); return 0; }
            if (command == "start-worker") { await SessionBindings.Start(Arg("--observation")); return 0; }
            if (command == "verify-capture-credentials")
            {
                await Runtime.VerifyRole();
                var credentials = await Runtime.CaptureCredentials();
                credentials.Clear();
                Console.WriteLine("DEMO_CAPTURE_CREDENTIAL_BINDINGS_VERIFIED_NO_ORDER");
                return 0;
            }
            if (command == "verify-runtime")
            {
                await Runtime.VerifyTransport();
                Console.WriteLine("DEMO_LAUNCHER_RUNTIME_VERIFIED_NO_ORDER");
                return 0;
            }
            Files.Require(command is "prepare-cycle" or "run-day", "UNKNOWN_COMMAND");
            // File ownership survives await continuations and is released on process exit.
            using var lease = Runtime.AcquireLauncherLease(Path.Combine(Runtime.Root, "launcher.lock"));
            if (command == "prepare-cycle")
            {
                await Pipeline.Prepare(Files.Utc(Arg("--cutoff")));
                Console.WriteLine("PREPARED_ONLY_NO_WORKER_HANDOFF");
            }
            else await RunDay(Arg("--session-id"));
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new { status = "Blocked", errorType = error.GetType().Name,
                code = error is InvalidOperationException ? error.Message : "UNEXPECTED_LAUNCHER_FAILURE", automaticRetryAllowed = false }));
            return 1;
        }
    }

    internal static void VerifyOwner(string sessionId)
    {
        Files.Require(sessionId.Length is > 0 and <= 100 && sessionId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'), "SESSION_ID_INVALID");
        var root = LmaxDemoSessionOwnership.RealAccountRoot;
        // Opening this file successfully would prove that no continuing Worker owns the account.
        var locked = false;
        try { using var probe = new FileStream(Path.Combine(root, "owner.lock"), FileMode.Open, FileAccess.Read, FileShare.None); }
        catch (IOException e) when ((e.HResult & 0xffff) is 32 or 33) { locked = true; }
        Files.Require(locked, "CONTINUING_WORKER_OWNERSHIP_REQUIRED");
        using var journal = LmaxDemoSessionJournal.OpenForInspection(Path.Combine(root, sessionId + ".journal.jsonl"));
        var session = LmaxDemoControlledSession.Inspect(journal);
        var now = DateTimeOffset.UtcNow;
        Files.Require(!session.Start.Simulated && session.Start.SessionId == sessionId && session.Start.Environment == "Demo"
            && session.Start.AccountId == "1754288005" && session.Start.InternalBrokerAccountCode == "LMAX_DEMO_LOCAL"
            && session.Start.DeadlineUtc == LmaxDemoDaySchedule.FinalClose(now) && now < session.Start.DeadlineUtc
            && session.Start.Instruments.Count == 1 && session.Start.Instruments[0] == new LmaxDemoSessionInstrument("EURUSD", "4001", 10000m)
            && !session.IsClosed && session.BlockingReason is null, "CONTINUING_SESSION_NOT_ADMISSIBLE");
        var lastInbound = journal.Entries.LastOrDefault(x => x.Kind is "Inbound" or "Report" or "DuplicateReport");
        Files.Require(lastInbound is not null && now >= lastInbound.AtUtc && now - lastInbound.AtUtc <= TimeSpan.FromSeconds(90), "CONTINUING_FIX_RECEPTION_NOT_CURRENT");
    }

    private static async Task RunDay(string sessionId)
    {
        VerifyOwner(sessionId);
        var inbox = Path.Combine(LmaxDemoSessionOwnership.RealAccountRoot, sessionId, "inbox");
        Files.Require(Directory.Exists(inbox) && !Directory.EnumerateFiles(inbox, "*.cycle.json").Any(), "DAY_LAUNCHER_RESTART_REQUIRES_RECONCILIATION");
        var close = LmaxDemoDaySchedule.FinalClose(DateTimeOffset.UtcNow);
        string? previous = null;
        while (DateTimeOffset.UtcNow < close.AddMinutes(-15))
        {
            VerifyOwner(sessionId);
            var cutoff = Pipeline.NextCutoff(DateTimeOffset.UtcNow);
            var manifest = await Pipeline.Prepare(cutoff);
            VerifyOwner(sessionId);
            if (previous is not null)
            {
                var result = Files.Read(previous + ".result.json");
                Files.Require(result.GetProperty("status").GetString() == "Completed"
                    && result.GetProperty("manifestSha256").GetString()!.Equals(Files.Hash(previous), StringComparison.OrdinalIgnoreCase), "PREVIOUS_CYCLE_NOT_COMPLETED");
            }
            Files.Require(DateTimeOffset.UtcNow >= manifest.DecisionAtUtc && DateTimeOffset.UtcNow < manifest.EffectiveAtUtc, "HANDOFF_DEADLINE_EXPIRED");
            var path = Path.Combine(inbox, manifest.CycleId + ".cycle.json");
            Files.Atomic(path, manifest);
            Files.Atomic(Path.Combine(Runtime.Root, manifest.CycleId, "worker-handoff.json"), new { manifest.CycleId, sessionId, handedAtUtc = DateTimeOffset.UtcNow,
                manifestPath = path, manifestSha256 = Files.Hash(path), orderCount = (int?)null });
            Console.WriteLine(JsonSerializer.Serialize(new { stage = "WORKER_HANDOFF", manifest.CycleId, sessionId }));
            previous = path;
            if (LmaxDemoDaySchedule.IsFinalExit(cutoff)) break;
        }
        // The Worker and its FIX receiver remain running for reconciliation and the genuine final UI observation.
        Console.WriteLine("DAY_HANDOFFS_ENDED_FINAL_OBSERVATION_STILL_REQUIRED");
    }
}
