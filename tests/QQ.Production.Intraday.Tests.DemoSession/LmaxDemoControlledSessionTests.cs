using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.DemoSession;

/// <summary>Simulated lifecycle facts only. No broker, credentials, database or Worker.</summary>
public sealed class LmaxDemoControlledSessionTests
{
    [Fact]
    public void TwoCyclesBeyondInitialFreshness_ReconcileFillsWorkingLeavesAndDuplicates()
    {
        using var f = new Fixture();
        var observed = f.Session.Start.ObservedAtUtc;
        f.AdvanceWithHeartbeats(TimeSpan.FromMinutes(16));
        Assert.True(f.Now - observed > TimeSpan.FromSeconds(900));
        f.Cycle("cycle-1", 10_000m);
        f.Intent("buy-1", "BUY", 1m);
        f.Complete("buy-1");
        f.Report("buy-1", "ack-1", "0", "0", 0m, 1m);
        var partial = f.Report("buy-1", "fill-1", "F", "1", .4m, .6m, .4m);
        f.AdvanceWithHeartbeats(TimeSpan.FromMinutes(16));
        f.Cycle("cycle-2", 12_000m);

        var beforeDuplicate = f.Session.GetTargetDelta("EURUSD", f.Now);
        Assert.Equal(new LmaxDemoSessionTargetDelta(12_000m, 4_000m, 6_000m, 2_000m, true), beforeDuplicate);
        f.Session.RecordExecutionReport(partial with { PossDup = true, RawMessageSha256 = new string('B', 64) }, f.Now);
        f.Session.RecordExecutionReport(partial with { PossDup = true, SequenceNumber = ++f.Sequence }, f.Now);
        Assert.Equal(beforeDuplicate, f.Session.GetTargetDelta("EURUSD", f.Now));
        AssertError("SEND_NOT_RECONCILED_TO_TARGET", () => f.Intent("unsafe-extra", "BUY", .2m));

        f.Cancel("cancel-1", "buy-1");
        f.Complete("cancel-1");
        AssertError("SEND_OR_CANCEL_UNRESOLVED", () => f.Session.GetTargetDelta("EURUSD", f.Now));
        f.Report("buy-1", "canceled-1", "4", "4", .4m, 0m, clientId: "cancel-1", originalId: "buy-1");
        Assert.Equal(new LmaxDemoSessionTargetDelta(12_000m, 4_000m, 0m, 8_000m, false), f.Session.GetTargetDelta("EURUSD", f.Now));

        f.Intent("buy-2", "BUY", .8m);
        f.Complete("buy-2");
        f.Report("buy-2", "fill-2", "F", "2", .8m, 0m, .8m);
        Assert.Equal(12_000m, f.Session.FillDerivedPositions["EURUSD"]);
        Assert.Equal(0m, f.Session.GetTargetDelta("EURUSD", f.Now).DeltaBaseQuantity);
        Assert.Equal(observed, f.Session.Start.ObservedAtUtc);
        Assert.Equal(900, f.Session.Start.InitialObservationMaxAgeSeconds);
        Assert.True(f.Session.Start.Simulated);

        using var inspection = LmaxDemoSessionJournal.OpenForInspection(f.Path);
        var replay = LmaxDemoControlledSession.Inspect(inspection);
        Assert.Equal(12_000m, replay.FillDerivedPositions["EURUSD"]);
        Assert.All(replay.KnownOrders, order => Assert.True(order.Terminal));
        Assert.Equal(2, inspection.Entries.Count(e => e.Kind == "Cycle"));
        AssertError("RESTART_REQUIRES_RECONCILIATION", () => replay.BeginCycle("cycle-3", Targets(0m), f.Now));
    }

    [Fact]
    public void FillWhileCancelPending_IsCountedBeforeResidualIsRecomputed()
    {
        using var f = WorkingPartial();
        f.Cancel("cancel-1", "buy-1");
        f.Complete("cancel-1");
        f.Report("buy-1", "late-fill", "F", "6", .6m, .4m, .2m);
        Assert.Equal(6_000m, f.Session.FillDerivedPositions["EURUSD"]);
        AssertError("SEND_OR_CANCEL_UNRESOLVED", () => f.Session.BeginCycle("next", Targets(0m), f.Now));
        f.Report("buy-1", "canceled", "4", "4", .6m, 0m, clientId: "cancel-1", originalId: "buy-1");
        Assert.Equal(4_000m, f.Session.GetTargetDelta("EURUSD", f.Now).DeltaBaseQuantity);
        AssertError("SEND_IDENTITY_OR_REPLAY_INVALID", () => f.Intent("cancel-1", "BUY", .4m));
    }

    [Fact]
    public void EarlierCanceledChild_DoesNotMakeALaterWorkingChildTerminal()
    {
        using var f = WorkingPartial();
        f.Cancel("cancel-1", "buy-1");
        f.Complete("cancel-1");
        f.Report("buy-1", "canceled", "4", "4", .4m, 0m, clientId: "cancel-1", originalId: "buy-1");
        f.Intent("buy-2", "BUY", .6m);
        f.Complete("buy-2");
        f.Report("buy-2", "ack-2", "0", "0", 0m, .6m);
        Assert.False(f.Session.KnownOrders.Single(o => o.Intent.ClientOrderId == "buy-2").Terminal);
        Assert.Equal(6_000m, f.Session.GetTargetDelta("EURUSD", f.Now).KnownWorkingBaseQuantity);
        AssertError("FINAL_RECONCILIATION_REQUIRED", () => f.Session.CloseAfterFinalObservation(f.Now, "simulated-final-ui", true, true, f.Now));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptedSend_RemainsDurableAndRestartNeverCreatesEmptyState(bool socketCompleted)
    {
        using var f = WorkingPartial();
        f.Cancel("cancel-1", "buy-1");
        f.Complete("cancel-1");
        f.Report("buy-1", "canceled", "4", "4", .4m, 0m, clientId: "cancel-1", originalId: "buy-1");
        f.AdvanceWithHeartbeats(TimeSpan.FromMinutes(16));
        f.Cycle("cycle-2", 12_000m);
        f.Intent("ambiguous-buy", "BUY", .8m);
        if (socketCompleted) f.Complete("ambiguous-buy");

        using (var inspection = LmaxDemoSessionJournal.OpenForInspection(f.Path))
        {
            var recovered = LmaxDemoControlledSession.Inspect(inspection);
            var uncertain = Assert.Single(recovered.KnownOrders.Where(order => !order.Acknowledged));
            Assert.Equal(4_000m, recovered.FillDerivedPositions["EURUSD"]);
            Assert.False(uncertain.Acknowledged);
            Assert.False(uncertain.Terminal);
            Assert.Equal(socketCompleted, uncertain.SendCompleted);
            Assert.Equal("ambiguous-buy", uncertain.Intent.ClientOrderId);
            Assert.Contains(inspection.Entries, e => e.Kind == "SendIntent");
            AssertError("RESTART_REQUIRES_RECONCILIATION", () => recovered.BeginCycle("retry", Targets(10_000m), f.Now));
            AssertError("RESTART_REQUIRES_RECONCILIATION", () => LmaxDemoControlledSession.Begin(inspection, f.Session.Start, f.Now));
        }
        Assert.Throws<IOException>(() => LmaxDemoSessionJournal.CreateNew(f.Path));
        AssertError("SEND_OR_CANCEL_UNRESOLVED", () => f.Intent("retry-buy", "BUY", 1m));
        f.Session.RecordTransportLost(f.Now);
        f.Session.RecordInboundControl(++f.Sequence, "0", f.Now);
        AssertError("TRANSPORT_LOST_RECONCILIATION_REQUIRED", () => f.Session.RequireContinuity(f.Now));
    }

    [Fact]
    public void UnacknowledgedCancel_RemainsUnresolvedAfterRestart()
    {
        using var f = WorkingPartial();
        f.Cancel("uncertain-cancel", "buy-1");
        using var inspection = LmaxDemoSessionJournal.OpenForInspection(f.Path);
        var replay = LmaxDemoControlledSession.Inspect(inspection);
        Assert.False(Assert.Single(replay.KnownOrders).Terminal);
        Assert.Equal(4_000m, replay.FillDerivedPositions["EURUSD"]);
        Assert.Contains(inspection.Entries, e => e.Kind == "SendIntent" && e.Data.Contains("uncertain-cancel"));
        AssertError("RESTART_REQUIRES_RECONCILIATION", () => replay.RequireContinuity(f.Now));
    }

    [Fact]
    public void ConflictingDuplicate_BlocksNewSubmissionsAndPreservesBothFacts()
    {
        using var f = WorkingPartial();
        var conflict = f.LastReport! with { SequenceNumber = ++f.Sequence, LastPx = 1.2m };
        AssertError("CONFLICTING_EXECUTION_ID", () => f.Session.RecordExecutionReport(conflict, f.Now));
        Assert.Equal(4_000m, f.Session.FillDerivedPositions["EURUSD"]);
        Assert.Equal("RejectedReport", f.Journal.Entries[^1].Kind);
        AssertError("CONFLICTING_EXECUTION_ID", () => f.Session.BeginCycle("next", Targets(0m), f.Now));
    }

    [Theory]
    [InlineData("account", "UNTRACKED_REPORT")]
    [InlineData("session", "UNTRACKED_REPORT")]
    [InlineData("unknown-client", "REPORT_CLIENT_ID_MISMATCH")]
    [InlineData("security", "REPORT_BINDING_MISMATCH")]
    [InlineData("side", "REPORT_BINDING_MISMATCH")]
    [InlineData("quantity", "REPORT_BINDING_MISMATCH")]
    [InlineData("hash", "REPORT_BINDING_MISMATCH")]
    [InlineData("future", "REPORT_TIME_INVALID")]
    [InlineData("cumulative-gap", "CUMULATIVE_FILL_GAP")]
    [InlineData("leaves-gap", "LEAVES_STATE_INCONSISTENT")]
    [InlineData("regression", "REPORT_STATE_INCONSISTENT")]
    [InlineData("mismatched-terminal", "REPORT_STATE_INCONSISTENT")]
    [InlineData("sequence-gap", "FIX_SEQUENCE_GAP")]
    [InlineData("unseen-replay", "UNSEEN_REPLAY_REQUIRES_RECONCILIATION")]
    public void UnprovenLifecycleFacts_AreJournaledButNeverUsedAsPositionTruth(string defect, string reason)
    {
        using var f = WorkingPartial();
        var report = f.MakeReport("buy-1", "next-fill", "F", "1", .6m, .4m, .2m);
        report = defect switch
        {
            "account" => report with { AccountId = "different-account" },
            "session" => report with { SessionId = "different-session" },
            "unknown-client" => report with { ClOrdId = "untracked-cancel", OrigClOrdId = "buy-1" },
            "security" => report with { SecurityId = "different-security" },
            "side" => report with { Side = "SELL" },
            "quantity" => report with { OrderQty = 2m },
            "hash" => report with { RawMessageSha256 = "missing" },
            "future" => report with { TransactTimeUtc = f.Now.AddSeconds(1) },
            "cumulative-gap" => report with { CumQty = .7m, LeavesQty = .3m },
            "leaves-gap" => report with { LeavesQty = .3m },
            "regression" => report with { ExecType = "0", OrdStatus = "0", LastQty = 0m, CumQty = .4m, LeavesQty = .6m },
            "mismatched-terminal" => report with { ExecType = "0", OrdStatus = "4", LastQty = 0m, CumQty = .4m, LeavesQty = 0m },
            "sequence-gap" => report with { SequenceNumber = report.SequenceNumber + 1 },
            "unseen-replay" => report with { SequenceNumber = report.SequenceNumber - 1, PossDup = true },
            _ => throw new ArgumentException(defect)
        };
        AssertError(reason, () => f.Session.RecordExecutionReport(report, f.Now));
        Assert.Equal(4_000m, f.Session.FillDerivedPositions["EURUSD"]);
        Assert.Equal(reason, f.Session.BlockingReason);
        Assert.Contains("next-fill", f.Journal.Entries[^1].Data);
        AssertError(reason, () => f.Session.BeginCycle("next", Targets(0m), f.Now));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LateInboundCannotHealLostContinuity(bool reportInsteadOfHeartbeat)
    {
        using var f = WorkingPartial();
        f.Now = f.Now.AddSeconds(91);
        if (reportInsteadOfHeartbeat)
            AssertError("INBOUND_CONTINUITY_LOST", () => f.Session.RecordExecutionReport(f.MakeReport("buy-1", "late", "F", "2", 1m, 0m, .6m), f.Now));
        else
            AssertError("INBOUND_CONTINUITY_LOST", () => f.Session.RecordInboundControl(++f.Sequence, "0", f.Now));
        AssertError("INBOUND_CONTINUITY_LOST", () => f.Session.BeginCycle("next", Targets(0m), f.Now));
        Assert.False(Assert.Single(f.Session.KnownOrders).Terminal);
    }

    [Theory]
    [InlineData("production")]
    [InlineData("account")]
    [InlineData("stale")]
    [InlineData("extended-ttl")]
    [InlineData("outside-activity")]
    [InlineData("working")]
    [InlineData("nonflat")]
    [InlineData("placeholder")]
    public void InitialQualificationCannotBeWeakened(string defect)
    {
        using var f = new Fixture(begin: false);
        var start = Fixture.StartAt(f.Now);
        start = defect switch
        {
            "production" => start with { Environment = "Production" },
            "account" => start with { AccountId = "921640160" },
            "stale" => start with { ObservedAtUtc = f.Now.AddSeconds(-901) },
            "extended-ttl" => start with { InitialObservationMaxAgeSeconds = 901 },
            "outside-activity" => start with { ExclusiveOrderActivityDeclared = false },
            "working" => start with { ObservedNoWorkingOrders = false },
            "nonflat" => start with { ObservedFlat = false },
            "placeholder" => start with { OwnerApprovalId = "TBD" },
            _ => throw new ArgumentException(defect)
        };
        Assert.Throws<InvalidOperationException>(() => LmaxDemoControlledSession.Begin(f.Journal, start, f.Now));
        Assert.Empty(f.Journal.Entries);
    }

    [Fact]
    public void FinalObservationMustFollowTheLastFill_AndClosureIsPossibleAfterSubmissionDeadline()
    {
        using var f = new Fixture();
        var startObservation = f.Now;
        f.Cycle("open", 10_000m);
        f.Intent("buy", "BUY", 1m);
        f.Complete("buy");
        f.Report("buy", "filled-buy", "F", "2", 1m, 0m, 1m);
        f.Now = f.Now.AddSeconds(1);
        f.Cycle("flat", 0m);
        f.Intent("sell", "SELL", 1m);
        f.Complete("sell");
        f.Report("sell", "filled-sell", "F", "2", 1m, 0m, 1m);
        Assert.Equal(0m, f.Session.FillDerivedPositions["EURUSD"]);
        AssertError("FINAL_RECONCILIATION_REQUIRED", () => f.Session.CloseAfterFinalObservation(startObservation, "simulated-final-ui", true, true, f.Now));
        f.AdvanceWithHeartbeats(f.Session.Start.DeadlineUtc - f.Now);
        AssertError("DAY_DEADLINE_REACHED", () => f.Cycle("too-late", 10_000m));
        f.Session.CloseAfterFinalObservation(f.Now, "simulated-final-ui", true, true, f.Now);
        Assert.True(f.Session.IsClosed);
        AssertError("CLOSED", () => f.Session.RequireContinuity(f.Now));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JournalCorruptionOrTruncatedTail_PreventsReplay(bool truncatedTail)
    {
        using var f = WorkingPartial();
        f.Journal.Dispose();
        if (truncatedTail) File.AppendAllText(f.Path, "{\"Index\":");
        else File.WriteAllText(f.Path, File.ReadAllText(f.Path).Replace("simulated-owner-ui", "tampered-owner-ui"));
        Assert.ThrowsAny<Exception>(() => LmaxDemoSessionJournal.OpenForInspection(f.Path));
    }

    private static Fixture WorkingPartial()
    {
        var f = new Fixture();
        f.Cycle("cycle-1", 10_000m);
        f.Intent("buy-1", "BUY", 1m);
        f.Complete("buy-1");
        f.Report("buy-1", "ack-1", "0", "0", 0m, 1m);
        f.Report("buy-1", "partial-1", "F", "1", .4m, .6m, .4m);
        return f;
    }

    private static Dictionary<string, decimal> Targets(decimal amount) => new() { ["EURUSD"] = amount };
    private static void AssertError(string expected, Action action)
        => Assert.Equal("DEMO_SESSION_" + expected, Assert.Throws<InvalidOperationException>(action).Message);

    private sealed class Fixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lmax-demo-simulated-" + Guid.NewGuid() + ".jsonl");
        public LmaxDemoSessionJournal Journal { get; }
        public LmaxDemoControlledSession Session { get; } = null!;
        public DateTimeOffset Now = new(2026, 9, 16, 9, 0, 0, TimeSpan.Zero);
        public long Sequence;
        private string cycle = "";
        public Arch7bExecutionReportEvent? LastReport;

        public Fixture(bool begin = true)
        {
            Journal = LmaxDemoSessionJournal.CreateNew(Path);
            if (!begin) return;
            Session = LmaxDemoControlledSession.Begin(Journal, StartAt(Now), Now);
            Session.RecordInboundControl(++Sequence, "A", Now);
        }

        public static LmaxDemoSessionStart StartAt(DateTimeOffset now) => new(
            "simulated-day", LmaxDemoControlledSession.DemoAccountId, "Demo", "simulated-owner-ui", now, now.AddHours(1),
            true, true, true, true, [new("EURUSD", "4001", 10_000m)]);

        public void Cycle(string id, decimal target) { Session.BeginCycle(id, Targets(target), Now); cycle = id; }
        public void Intent(string id, string side, decimal quantity)
            => Session.RecordSendIntent(new(cycle, cycle + "-EURUSD", id, "D", null, "EURUSD", "4001", side, quantity, new string('A', 64)), Now);
        public void Cancel(string id, string originalId)
        {
            var order = Session.KnownOrders.Single(o => o.Intent.ClientOrderId == originalId);
            Session.RecordSendIntent(new(cycle, order.Intent.ParentId, id, "F", originalId, "EURUSD", "4001", order.Intent.Side, order.LeavesQuantity, new string('A', 64)), Now);
        }
        public void Complete(string id) => Session.RecordSendCompleted(id, Now);
        public void AdvanceWithHeartbeats(TimeSpan duration)
        {
            var until = Now + duration;
            while (Now < until)
            {
                Now = Now.AddSeconds(Math.Min(60, (until - Now).TotalSeconds));
                Session.RecordInboundControl(++Sequence, "0", Now);
            }
        }
        public Arch7bExecutionReportEvent Report(string orderId, string execId, string execType, string status, decimal cumulative, decimal leaves, decimal last = 0m, string? clientId = null, string? originalId = null)
        {
            var report = MakeReport(orderId, execId, execType, status, cumulative, leaves, last, clientId, originalId);
            Session.RecordExecutionReport(report, Now);
            LastReport = report;
            return report;
        }
        public Arch7bExecutionReportEvent MakeReport(string orderId, string execId, string execType, string status, decimal cumulative, decimal leaves, decimal last = 0m, string? clientId = null, string? originalId = null)
        {
            var intent = Session.KnownOrders.Single(o => o.Intent.ClientOrderId == orderId).Intent;
            return new("simulated-day", ++Sequence, LmaxDemoControlledSession.DemoAccountId, "broker-" + orderId,
                clientId ?? orderId, originalId, execId, execType, status, intent.Symbol, intent.SecurityId, intent.Side,
                intent.VenueQuantity, cumulative, leaves, last, last == 0m ? 0m : 1.1m, cumulative == 0m ? 0m : 1.1m,
                1.1m, Now, false, new string('A', 64));
        }
        public void Dispose() { Journal.Dispose(); File.Delete(Path); }
    }
}
