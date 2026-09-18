using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

/// <summary>One logical parent's physical FIX children. Cancellation leaves and
/// remaining target quantity are different facts. This projection opens no socket.</summary>
public sealed class LmaxDemoParentLifecycle(decimal requestedQuantity)
{
    private sealed record Child(decimal Quantity, decimal Cumulative, decimal Leaves, bool Terminal, string? OrderId);
    private readonly Dictionary<string, Child> children = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> cancelIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> executions = new(StringComparer.Ordinal);
    public decimal CumulativeQuantity => children.Values.Sum(x => x.Cumulative);
    public decimal RemainingQuantity => requestedQuantity - CumulativeQuantity;
    public string? WorkingClientOrderId => children.SingleOrDefault(x => !x.Value.Terminal).Key;
    public decimal WorkingOrderQuantity => WorkingClientOrderId is { } id ? children[id].Quantity : 0m;
    public decimal WorkingLeavesQuantity => WorkingClientOrderId is { } id ? children[id].Leaves : 0m;
    public bool AllChildrenTerminal => children.Count > 0 && children.Values.All(x => x.Terminal);

    public void RegisterChild(string id, decimal quantity)
    {
        if (string.IsNullOrWhiteSpace(id) || children.ContainsKey(id) || cancelIds.ContainsKey(id)
            || WorkingClientOrderId is not null || quantity <= 0m || quantity != RemainingQuantity)
            throw new InvalidOperationException("DEMO_STRATEGY_CHILD_NOT_RECONCILED");
        children.Add(id, new(quantity, 0m, quantity, false, null));
    }

    public void RegisterCancel(string id, string originalId)
    {
        if (string.IsNullOrWhiteSpace(id) || children.ContainsKey(id) || cancelIds.ContainsKey(id)
            || !children.TryGetValue(originalId, out var child) || child.Terminal
            || cancelIds.Values.Contains(originalId))
            throw new InvalidOperationException("DEMO_STRATEGY_CANCEL_NOT_RECONCILED");
        cancelIds.Add(id, originalId);
    }

    /// <returns>False for an identical execution replay; true for a new fact.</returns>
    public bool Observe(LmaxFixExecutionReport report)
    {
        if (string.IsNullOrWhiteSpace(report.ExecId) || string.IsNullOrWhiteSpace(report.ClOrdId)
            || string.IsNullOrWhiteSpace(report.OrderId) || report.OrderQty is null
            || report.CumQty is null || report.LeavesQty is null)
            throw new InvalidOperationException("DEMO_STRATEGY_REPORT_INCOMPLETE");
        var semantic = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            report.ExecId, report.OrderId, report.ClOrdId, report.OrigClOrdId, report.Account,
            report.SecurityId, report.Symbol, report.SideRaw, report.ExecTypeRaw, report.OrdStatusRaw,
            report.OrderQty, report.CumQty, report.LeavesQty, report.LastQty, report.LastPx,
            report.AvgPx, report.Price, report.TransactTimeUtc
        }))));
        if (executions.TryGetValue(report.ExecId, out var previous))
        {
            if (previous != semantic) throw new InvalidOperationException("DEMO_STRATEGY_CONFLICTING_EXECUTION_ID");
            return false;
        }
        var id = report.ClOrdId;
        if (cancelIds.TryGetValue(id, out var original))
        {
            if (report.OrigClOrdId != original) throw new InvalidOperationException("DEMO_STRATEGY_CANCEL_REPORT_BINDING_INVALID");
            id = original;
        }
        else if (report.OrigClOrdId is not null && report.OrigClOrdId != id)
            throw new InvalidOperationException("DEMO_STRATEGY_UNTRACKED_REPORT");
        if (!children.TryGetValue(id, out var child) || child.Terminal || report.OrderQty != child.Quantity
            || (child.OrderId is not null && child.OrderId != report.OrderId))
            throw new InvalidOperationException("DEMO_STRATEGY_UNTRACKED_OR_TERMINAL_CHILD");

        var terminal = report.OrdStatus is LmaxFixOrderStatus.Filled or LmaxFixOrderStatus.Canceled
            or LmaxFixOrderStatus.Rejected or LmaxFixOrderStatus.Expired;
        var coherentState = report.ExecTypeRaw switch
        {
            "0" => report.OrdStatusRaw == "0" && report.CumQty == 0m,
            "A" => report.OrdStatusRaw == "A" && report.CumQty == 0m,
            "F" => report.OrdStatusRaw is "1" or "2" or "6",
            "4" => report.OrdStatusRaw == "4",
            "8" => report.OrdStatusRaw == "8" && report.CumQty == 0m,
            "C" => report.OrdStatusRaw == "C",
            "6" => report.OrdStatusRaw == "6" && cancelIds.Values.Contains(id),
            "I" => report.OrdStatusRaw is "0" or "1" or "2" or "4" or "8" or "C" or "A" or "6",
            _ => false
        };
        if (!coherentState) throw new InvalidOperationException("DEMO_STRATEGY_REPORT_STATE_INCONSISTENT");
        if ((report.OrdStatusRaw is "0" or "A" or "8" && report.CumQty != 0m)
            || (report.OrdStatusRaw == "1" && (report.CumQty <= 0m || report.CumQty >= child.Quantity))
            || (report.OrdStatusRaw == "6" && !cancelIds.Values.Contains(id)))
            throw new InvalidOperationException("DEMO_STRATEGY_REPORT_STATE_INCONSISTENT");
        var trade = report.ExecType == LmaxFixExecutionReportType.Trade;
        var last = report.LastQty ?? 0m;
        if ((trade && (last <= 0m || report.LastPx is not > 0m)) || (!trade && last != 0m)
            || report.CumQty != child.Cumulative + (trade ? last : 0m)
            || report.CumQty < 0m || report.CumQty > child.Quantity || report.LeavesQty < 0m
            || (terminal && report.LeavesQty != 0m)
            || (!terminal && report.CumQty + report.LeavesQty != child.Quantity)
            || (report.OrdStatus == LmaxFixOrderStatus.Filled && report.CumQty != child.Quantity))
            throw new InvalidOperationException("DEMO_STRATEGY_FILL_OR_LEAVES_INCONSISTENT");
        children[id] = child with { Cumulative = report.CumQty.Value, Leaves = report.LeavesQty.Value, Terminal = terminal, OrderId = report.OrderId };
        executions.Add(report.ExecId, semantic);
        return true;
    }
}
