using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

public sealed record LmaxFixArch7bOutboundIntent(
    Guid QualificationRunId,
    string LifecycleRole,
    string MessageType,
    string ClientOrderId,
    string? OriginalClientOrderId,
    string Side,
    decimal Quantity,
    decimal? LimitPrice,
    string BboSnapshotSha256,
    string PayloadSha256,
    DateTimeOffset IntentRecordedAtUtc);

public sealed record LmaxFixArch7bRecoveryState(
    bool OpeningSendIntentExists,
    bool CancelSendIntentExists,
    bool FlattenSendIntentExists,
    int OrderStatusRequestCount,
    decimal OpeningCumulativeQuantity,
    decimal OpeningLeavesQuantity,
    bool OpeningTerminal,
    decimal FlattenCumulativeQuantity,
    decimal FlattenLeavesQuantity,
    bool FlattenTerminal,
    string? OpeningMarketObservationId = null,
    string? FlattenMarketObservationId = null);

public interface ILmaxFixArch7bQualificationSession
{
    Task InitializeAsync(
        LmaxFixArch7bKnownOrderRequest request,
        CancellationToken cancellationToken);

    Task<LmaxFixArch7bRecoveryState> LoadRecoveryStateAsync(
        LmaxFixArch7bKnownOrderRequest request,
        CancellationToken cancellationToken);

    Task<decimal> ReadValidatedFillQuantityAsync(
        LmaxFixArch7bKnownOrderRequest request,
        string clientOrderId,
        CancellationToken cancellationToken);

    Task<Arch7bLifecycleEvaluation> FinalizeReconciliationAsync(
        LmaxFixArch7bKnownOrderRequest request,
        CancellationToken cancellationToken);

    ValueTask CompleteAsync(
        LmaxFixArch7bKnownOrderRequest request,
        CancellationToken cancellationToken);
}

public interface ILmaxFixArch7bLifecycleObserver
{
    bool IsDurable { get; }

    Task RecordSessionEventAsync(
        LmaxFixArch7bKnownOrderRequest request,
        string eventType,
        long? fixSequenceNumber,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);

    Task RecordSendIntentAsync(
        LmaxFixArch7bKnownOrderRequest request,
        LmaxFixArch7bOutboundIntent intent,
        CancellationToken cancellationToken);

    Task RecordExecutionReportAsync(
        LmaxFixArch7bKnownOrderRequest request,
        LmaxFixExecutionReport report,
        CancellationToken cancellationToken);
}

public static class LmaxFixArch7bReportMapper
{
    public static Arch7bExecutionReportEvent Map(
        LmaxFixArch7bKnownOrderRequest request,
        LmaxFixExecutionReport report,
        string persistedFixSessionId)
    {
        if (report.FixSequenceNumber is null ||
            string.IsNullOrWhiteSpace(report.Account) ||
            string.IsNullOrWhiteSpace(report.OrderId) ||
            string.IsNullOrWhiteSpace(report.ClOrdId) ||
            string.IsNullOrWhiteSpace(report.ExecId) ||
            string.IsNullOrWhiteSpace(report.Symbol) ||
            string.IsNullOrWhiteSpace(report.SecurityId) ||
            string.IsNullOrWhiteSpace(report.SideRaw) ||
            report.OrderQty is null ||
            report.CumQty is null ||
            report.LeavesQty is null ||
            report.TransactTimeUtc is null)
        {
            throw new InvalidOperationException("ARCH7B_EXECUTION_REPORT_PERSISTENCE_FIELDS_INCOMPLETE");
        }

        return new(
            persistedFixSessionId,
            report.FixSequenceNumber.Value,
            report.Account,
            report.OrderId,
            report.ClOrdId,
            report.OrigClOrdId,
            report.ExecId,
            report.ExecTypeRaw ?? string.Empty,
            report.OrdStatusRaw ?? string.Empty,
            report.Symbol,
            report.SecurityId,
            report.SideRaw == "1" ? "BUY" : report.SideRaw == "2" ? "SELL" : report.SideRaw,
            report.OrderQty.Value,
            report.CumQty.Value,
            report.LeavesQty.Value,
            report.LastQty ?? 0m,
            report.LastPx ?? 0m,
            report.AvgPx ?? 0m,
            report.Price,
            report.TransactTimeUtc.Value,
            report.PossDup,
            report.RawMessageSha256);
    }
}
