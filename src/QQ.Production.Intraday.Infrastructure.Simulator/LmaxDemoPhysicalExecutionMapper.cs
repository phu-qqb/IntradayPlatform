using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Infrastructure.Simulator;

/// <summary>Materializes each durable physical FIX child separately. It never
/// creates an acknowledgement, combines broker cumulative quantities, or drops cancels.</summary>
public static class LmaxDemoPhysicalExecutionMapper
{
    public static LmaxDemoExecutionPersistence Map(ChildOrder initial, decimal contractSize, string internalAccountCode,
        LmaxDemoSessionStart start, IReadOnlyList<LmaxDemoSessionOrder> orders, LmaxDemoStrategyExecutionResult result)
    {
        if (!result.Terminal || orders.Count == 0 || orders.Any(x => !x.Terminal) || result.ExecutionReports.Count == 0)
            throw new InvalidOperationException("DEMO_PHYSICAL_PERSISTENCE_REQUIRES_TERMINAL_FACTS");
        Guid Identity(string kind, string id) => new(SHA256.HashData(Encoding.UTF8.GetBytes(start.AccountId + "|" + start.SessionId + "|" + kind + "|" + id)).AsSpan(0, 16));
        var firstPhysicalId = result.ExecutionReports[0].OrigClOrdId ?? result.ExecutionReports[0].ClOrdId;
        var children = orders.Select(order =>
        {
            if (order.Intent.ParentId != initial.Id.Value.ToString("N") || order.Intent.MessageType != "D")
                throw new InvalidOperationException("DEMO_PHYSICAL_PERSISTENCE_PARENT_MISMATCH");
            var first = result.ExecutionReports.First(x => (x.OrigClOrdId ?? x.ClOrdId) == order.Intent.ClientOrderId);
            return initial with {
                Id = order.Intent.ClientOrderId == firstPhysicalId ? initial.Id : new ChildOrderId(Identity("child", order.Intent.ClientOrderId)),
                ClientOrderId = new ClientOrderId(order.Intent.ClientOrderId),
                BaseQuantity = order.Intent.VenueQuantity * contractSize,
                VenueQuantity = order.Intent.VenueQuantity,
                OrderType = order.Intent.OrderTypeRaw switch { "1" => OrderType.Market, "2" => OrderType.Limit, _ => throw new InvalidOperationException("DEMO_PHYSICAL_TYPE_MISSING") },
                TimeInForce = order.Intent.TimeInForceRaw switch { "0" => TimeInForce.GFD, "3" => TimeInForce.IOC, _ => throw new InvalidOperationException("DEMO_PHYSICAL_TIF_MISSING") },
                Status = order.Status switch { "2" => OrderStatus.Filled, "4" => OrderStatus.Cancelled, "8" => OrderStatus.Rejected, "C" => OrderStatus.Expired, _ => throw new InvalidOperationException("DEMO_PHYSICAL_STATE_NOT_TERMINAL") },
                CreatedAtUtc = first.TransactTimeUtc ?? first.ParsedAtUtc
            };
        }).ToArray();
        var reports = result.ExecutionReports.Select(report =>
        {
            var key = report.OrigClOrdId ?? report.ClOrdId;
            var child = children.Single(x => x.ClientOrderId.Value == key);
            var type = report.ExecType switch {
                LmaxFixExecutionReportType.New => ExecutionReportType.OrderAck,
                LmaxFixExecutionReportType.Trade when report.LeavesQty > 0m => ExecutionReportType.PartialFill,
                LmaxFixExecutionReportType.Trade => ExecutionReportType.Fill,
                LmaxFixExecutionReportType.Canceled => ExecutionReportType.CancelAck,
                LmaxFixExecutionReportType.Expired => ExecutionReportType.Expired,
                LmaxFixExecutionReportType.Rejected => ExecutionReportType.OrderReject,
                _ => throw new InvalidOperationException("DEMO_PHYSICAL_REPORT_TYPE_UNSUPPORTED")
            };
            return new ExecutionReport(new ExecutionReportId(Identity("report", report.ExecId!)), child.Id, child.VenueId,
                report.OrderId!, report.ExecId, child.ClientOrderId, type, report.LastQty ?? 0m, report.LastPx ?? 0m,
                report.LeavesQty ?? 0m, report.CumQty ?? 0m, report.AvgPx ?? 0m, report.TransactTimeUtc ?? report.ParsedAtUtc);
        }).ToArray();
        var parentStatus = result.CumulativeQuantity == result.RequestedQuantity ? OrderStatus.Filled
            : children.All(x => x.Status == OrderStatus.Rejected) ? OrderStatus.Rejected : OrderStatus.Expired;
        return new(start.AccountId, internalAccountCode, start.SessionId, initial.ParentOrderId, initial.Id, children, reports, parentStatus);
    }
}
