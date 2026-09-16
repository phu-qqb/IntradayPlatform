using System.Globalization;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxDemoParentLifecycleTests
{
    [Fact]
    public void CancelLeavesZero_DoesNotEraseTheUnfilledTarget()
    {
        var state = Partial();
        state.RegisterCancel("cancel-1", "passive");
        Assert.Equal(1m, state.WorkingOrderQuantity);
        Assert.Equal(.6m, state.WorkingLeavesQuantity);
        state.Observe(Report("cancel-1", "cancel-ack", "4", "4", 1m, .4m, 0m, original: "passive"));
        Assert.True(state.AllChildrenTerminal);
        Assert.Null(state.WorkingClientOrderId);
        Assert.Equal(.4m, state.CumulativeQuantity);
        Assert.Equal(.6m, state.RemainingQuantity);
        state.RegisterChild("residual", .6m);
        Assert.False(state.AllChildrenTerminal);
        state.Observe(Report("residual", "residual-fill", "F", "2", .6m, .6m, 0m, .6m));
        Assert.Equal(1m, state.CumulativeQuantity);
        Assert.Equal(0m, state.RemainingQuantity);
        Assert.True(state.AllChildrenTerminal);
    }

    [Fact]
    public void MissingCancelAck_CannotPermitAReplacement()
    {
        var state = Partial();
        state.RegisterCancel("cancel-1", "passive");
        Assert.Equal("passive", state.WorkingClientOrderId);
        Assert.False(state.AllChildrenTerminal);
        Assert.Throws<InvalidOperationException>(() => state.RegisterChild("replacement", .6m));
    }

    [Fact]
    public void LateFillDuringCancel_ReducesTheNextChildQuantity()
    {
        var state = Partial();
        state.RegisterCancel("cancel-1", "passive");
        state.Observe(Report("passive", "late-fill", "F", "6", 1m, .6m, .4m, .2m));
        state.Observe(Report("cancel-1", "cancel-ack", "4", "4", 1m, .6m, 0m, original: "passive"));
        Assert.Equal(.4m, state.RemainingQuantity);
        Assert.Throws<InvalidOperationException>(() => state.RegisterChild("wrong-residual", .6m));
        state.RegisterChild("right-residual", .4m);
    }

    [Fact]
    public void FullFillDuringCancel_LeavesNothingToReplace()
    {
        var state = Partial();
        state.RegisterCancel("cancel-1", "passive");
        state.Observe(Report("passive", "final-fill", "F", "2", 1m, 1m, 0m, .6m));
        Assert.True(state.AllChildrenTerminal);
        Assert.Null(state.WorkingClientOrderId);
        Assert.Equal(0m, state.RemainingQuantity);
        Assert.Throws<InvalidOperationException>(() => state.RegisterChild("unwanted", .6m));
    }

    [Fact]
    public void IdenticalReplay_DoesNotCountAnotherFill()
    {
        var state = new LmaxDemoParentLifecycle(1m);
        state.RegisterChild("passive", 1m);
        var fill = Report("passive", "fill", "F", "1", 1m, .4m, .6m, .4m);
        Assert.True(state.Observe(fill));
        Assert.False(state.Observe(fill with { PossDup = true, FixSequenceNumber = 25, ParsedAtUtc = fill.ParsedAtUtc.AddSeconds(10), RawMessageSha256 = new string('F', 64) }));
        Assert.Equal(.4m, state.CumulativeQuantity);
        Assert.Throws<InvalidOperationException>(() => state.Observe(fill with { LastPx = 1.2m }));
    }

    [Fact]
    public void EarlierTerminalChild_DoesNotCompleteALaterUnacknowledgedChild()
    {
        var state = new LmaxDemoParentLifecycle(1m);
        state.RegisterChild("rejected", 1m);
        state.Observe(Report("rejected", "reject", "8", "8", 1m, 0m, 0m));
        Assert.True(state.AllChildrenTerminal);
        state.RegisterChild("new", 1m);
        Assert.False(state.AllChildrenTerminal);
        Assert.Equal("new", state.WorkingClientOrderId);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("unknown-cancel")]
    [InlineData("cumulative-gap")]
    [InlineData("leaves-gap")]
    [InlineData("false-filled")]
    [InlineData("new-after-partial")]
    [InlineData("missing-exec-id")]
    [InlineData("pending-without-cancel")]
    [InlineData("status-new-after-partial")]
    public void UnprovenReport_DoesNotChangeFillQuantity(string defect)
    {
        var state = Partial();
        var report = Report("passive", "next", "F", "1", 1m, .6m, .4m, .2m);
        report = defect switch
        {
            "unknown" => report with { ClOrdId = "untracked" },
            "unknown-cancel" => report with { ClOrdId = "untracked", OrigClOrdId = "passive" },
            "cumulative-gap" => report with { CumQty = .7m },
            "leaves-gap" => report with { LeavesQty = .3m },
            "false-filled" => report with { OrdStatus = LmaxFixOrderStatus.Filled, OrdStatusRaw = "2", LeavesQty = 0m },
            "new-after-partial" => report with { ExecType = LmaxFixExecutionReportType.New, ExecTypeRaw = "0", OrdStatusRaw = "0", LastQty = 0m },
            "missing-exec-id" => report with { ExecId = null },
            "pending-without-cancel" => report with { OrdStatusRaw = "6" },
            "status-new-after-partial" => report with { ExecTypeRaw = "I", OrdStatusRaw = "0", LastQty = 0m, CumQty = .4m, LeavesQty = .6m },
            _ => throw new ArgumentException(defect)
        };
        Assert.Throws<InvalidOperationException>(() => state.Observe(report));
        Assert.Equal(.4m, state.CumulativeQuantity);
        Assert.Equal(.6m, state.RemainingQuantity);
    }

    private static LmaxDemoParentLifecycle Partial()
    {
        var state = new LmaxDemoParentLifecycle(1m);
        state.RegisterChild("passive", 1m);
        state.Observe(Report("passive", "partial-fill", "F", "1", 1m, .4m, .6m, .4m));
        return state;
    }

    private static LmaxFixExecutionReport Report(string client, string exec, string type, string status,
        decimal qty, decimal cumulative, decimal leaves, decimal last = 0m, string? original = null)
    {
        static string N(decimal n) => n.ToString(CultureInfo.InvariantCulture);
        var originalClient = original ?? client;
        var fields = new List<(string Tag, string Value)>
        {
            ("1", "1754288005"), ("11", client), ("37", "broker-" + originalClient), ("17", exec),
            ("150", type), ("39", status), ("55", "EURUSD"), ("48", "4001"), ("22", "8"), ("54", "1"),
            ("38", N(qty)), ("14", N(cumulative)), ("151", N(leaves)), ("32", N(last)),
            ("31", last > 0m ? "1.1" : "0"), ("6", cumulative > 0m ? "1.1" : "0"), ("60", "20260916-10:00:00.000")
        };
        if (original is not null) fields.Add(("41", original));
        var frame = LmaxFixMarketDataCodec.BuildMessage("8", 2, "SIMULATED_BROKER", "SIMULATED_CLIENT", fields);
        return LmaxFixRecoveryCodec.NormalizeExecutionReport(frame, new LmaxConnectivityLabOptions()).Report;
    }
}
