using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public sealed record PmsArch7bQualificationRunRow(
    Guid QualificationRunId,
    Guid ChildOrderId,
    string Gate,
    string Scope,
    string Environment,
    string AccountId,
    string Symbol,
    string SecurityId,
    string SecurityIdSource,
    string OpeningSide,
    decimal VenueQuantity,
    decimal QuantityIncrement,
    decimal PriceIncrement,
    string OpeningClientOrderId,
    string FlattenClientOrderId,
    string CancelClientOrderId,
    string PolicySha256,
    string AuthorizationPacketSha256,
    string OwnerId,
    DateTimeOffset LeaseExpiresAtUtc,
    string ExternalOrManualOrderCoverage,
    DateTimeOffset RegisteredAtUtc);

public sealed record PmsArch7bFixSessionEventRow(
    Guid SessionEventId,
    Guid QualificationRunId,
    string SessionId,
    string EventType,
    long? FixSequenceNumber,
    string EventSha256,
    DateTimeOffset OccurredAtUtc);

public sealed record PmsArch7bOrderSendLedgerRow(
    Guid SendLedgerId,
    Guid QualificationRunId,
    string LifecycleRole,
    string MessageType,
    string ClientOrderId,
    string? OriginalClientOrderId,
    string Symbol,
    string SecurityId,
    string Side,
    decimal Quantity,
    decimal? LimitPrice,
    string BboSnapshotSha256,
    string PayloadSha256,
    DateTimeOffset IntentRecordedAtUtc);

public sealed record PmsArch7bExecutionReportRow(
    Guid ExecutionReportId,
    Guid QualificationRunId,
    string SessionId,
    long FixSequenceNumber,
    string AccountId,
    string OrderId,
    string ClientOrderId,
    string? OriginalClientOrderId,
    string ExecId,
    string ExecType,
    string OrderStatus,
    string Symbol,
    string SecurityId,
    string Side,
    decimal OrderQuantity,
    decimal CumulativeQuantity,
    decimal LeavesQuantity,
    decimal LastQuantity,
    decimal LastPrice,
    decimal AveragePrice,
    decimal? LimitPrice,
    DateTimeOffset TransactTimeUtc,
    bool PossDup,
    string RawMessageSha256);

public sealed record PmsArch7bFillRow(
    Guid FillId,
    Guid QualificationRunId,
    Guid ExecutionReportId,
    string ExecId,
    string OrderId,
    string ClientOrderId,
    string Symbol,
    string SecurityId,
    string Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset TransactTimeUtc,
    string RawMessageSha256,
    string FeeStatus,
    decimal? FeeAmount,
    string? FeeCurrency);

public sealed record PmsArch7bPositionLedgerEventRow(
    Guid PositionLedgerEventId,
    Guid QualificationRunId,
    Guid FillId,
    string ExecId,
    string Symbol,
    string SecurityId,
    string InstrumentCurrency,
    string SettlementCurrency,
    decimal SignedQuantity,
    decimal Price,
    DateTimeOffset EventTimeUtc,
    string SourceMessageSha256,
    string EventSha256);

public sealed record PmsArch7bFinalReconciliationRow(
    Guid ReconciliationId,
    Guid QualificationRunId,
    string Status,
    string BrokerEvidenceAuthority,
    decimal OpeningCumulativeQuantity,
    decimal OpeningFillQuantity,
    decimal FlattenCumulativeQuantity,
    decimal FlattenFillQuantity,
    decimal KnownWorkingLeaves,
    decimal InternalLedgerQuantity,
    decimal BrokerResidualQuantity,
    decimal ResidualQuantity,
    int CriticalBreakCount,
    string BreaksJson,
    decimal? RealizedPnlBeforeFees,
    string FeeStatus,
    string EvidenceSha256,
    DateTimeOffset CompletedAtUtc);

public enum Arch7bPostgreSqlWriteResult
{
    Persisted,
    AlreadyPersistedIdentical
}

public sealed record Arch7bQualificationRegistration(
    Guid QualificationRunId,
    Guid ChildOrderId,
    Arch7bPreflightDecision Preflight,
    string AuthorizationPacketSha256,
    Arch7bExclusivityDeclaration Exclusivity,
    DateTimeOffset RegisteredAtUtc,
    Arch7bKnownOrderExecutionProfile ExecutionProfile);

public sealed record Arch7bFixSessionLedgerEvent(
    Guid SessionEventId,
    Guid QualificationRunId,
    string SessionId,
    string EventType,
    long? FixSequenceNumber,
    string EventSha256,
    DateTimeOffset OccurredAtUtc);

public sealed record Arch7bOrderSendIntent(
    Guid SendLedgerId,
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

public sealed record Arch7bFinalReconciliationEvidence(
    Guid ReconciliationId,
    Guid QualificationRunId,
    Arch7bLifecycleEvaluation Lifecycle,
    string BrokerEvidenceAuthority,
    decimal BrokerResidualQuantity,
    string EvidenceSha256,
    DateTimeOffset CompletedAtUtc);

public sealed record Arch7bPostgreSqlRecoveryState(
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
    string? OpeningMarketObservationId,
    string? FlattenMarketObservationId);

public sealed class EfArch7bKnownOrderLifecycleStore(
    IDbContextFactory<PmsShadowDbContext> contextFactory)
{
    public async Task<Arch7bPostgreSqlWriteResult> RegisterRunAsync(
        Arch7bQualificationRegistration registration,
        CancellationToken cancellationToken = default)
    {
        if (!registration.Preflight.Allowed || registration.Preflight.Blockers.Count != 0)
            throw new InvalidOperationException("ARCH7B_PREFLIGHT_NOT_ALLOWED");
        RequireSha(registration.Preflight.PolicySha256, "ARCH7B_POLICY_SHA256_INVALID");
        RequireSha(registration.AuthorizationPacketSha256, "ARCH7B_AUTHORIZATION_PACKET_SHA256_INVALID");
        RequireUtc(registration.RegisteredAtUtc, "ARCH7B_REGISTERED_TIME_NOT_UTC");
        ArgumentNullException.ThrowIfNull(registration.ExecutionProfile);
        if (!registration.Exclusivity.AdvisoryLeaseHeld ||
            registration.Exclusivity.ExpiresAtUtc <= registration.RegisteredAtUtc)
            throw new InvalidOperationException("ARCH7B_EXCLUSIVITY_LEASE_NOT_HELD");

        var row = new PmsArch7bQualificationRunRow(
            registration.QualificationRunId,
            registration.ChildOrderId,
            registration.ExecutionProfile.Gate,
            registration.ExecutionProfile.Scope,
            registration.ExecutionProfile.Environment,
            registration.ExecutionProfile.AccountId,
            registration.ExecutionProfile.Symbol,
            registration.ExecutionProfile.SecurityId,
            registration.ExecutionProfile.SecurityIdSource,
            registration.ExecutionProfile.OpeningSide,
            registration.ExecutionProfile.VenueQuantity,
            registration.ExecutionProfile.QuantityIncrement,
            registration.ExecutionProfile.PriceIncrement,
            registration.Preflight.OpeningClientOrderId,
            registration.Preflight.FlattenClientOrderId,
            registration.Preflight.CancelClientOrderId,
            registration.Preflight.PolicySha256,
            registration.AuthorizationPacketSha256,
            registration.Exclusivity.OwnerId,
            registration.Exclusivity.ExpiresAtUtc,
            registration.ExecutionProfile.ExternalOrManualOrderCoverage,
            registration.RegisteredAtUtc);

        return await InsertIdenticalAsync(
            registration.QualificationRunId,
            context => context.Arch7bQualificationRuns.AsNoTracking()
                .SingleOrDefaultAsync(value =>
                    value.QualificationRunId == registration.QualificationRunId, cancellationToken),
            row,
            context => context.Arch7bQualificationRuns.Add(row),
            "ARCH7B_QUALIFICATION_RUN_IDEMPOTENCY_CONFLICT",
            cancellationToken);
    }

    public Task<Arch7bPostgreSqlWriteResult> RecordFixSessionEventAsync(
        Arch7bFixSessionLedgerEvent value,
        CancellationToken cancellationToken = default)
    {
        RequireSha(value.EventSha256, "ARCH7B_FIX_SESSION_EVENT_SHA256_INVALID");
        RequireUtc(value.OccurredAtUtc, "ARCH7B_FIX_SESSION_EVENT_TIME_NOT_UTC");
        if (string.IsNullOrWhiteSpace(value.SessionId) || string.IsNullOrWhiteSpace(value.EventType))
            throw new InvalidOperationException("ARCH7B_FIX_SESSION_EVENT_INCOMPLETE");
        var row = new PmsArch7bFixSessionEventRow(
            value.SessionEventId,
            value.QualificationRunId,
            value.SessionId,
            value.EventType,
            value.FixSequenceNumber,
            value.EventSha256,
            value.OccurredAtUtc);
        return InsertIdenticalAsync(
            value.QualificationRunId,
            context => context.Arch7bFixSessionEvents.AsNoTracking()
                .SingleOrDefaultAsync(item => item.SessionEventId == value.SessionEventId, cancellationToken),
            row,
            context => context.Arch7bFixSessionEvents.Add(row),
            "ARCH7B_FIX_SESSION_EVENT_IDEMPOTENCY_CONFLICT",
            cancellationToken);
    }

    public async Task<Arch7bPostgreSqlWriteResult> RecordSendIntentAsync(
        Arch7bOrderSendIntent value,
        CancellationToken cancellationToken = default)
    {
        RequireSha(value.BboSnapshotSha256, "ARCH7B_SEND_BBO_SHA256_INVALID");
        RequireSha(value.PayloadSha256, "ARCH7B_SEND_PAYLOAD_SHA256_INVALID");
        RequireUtc(value.IntentRecordedAtUtc, "ARCH7B_SEND_INTENT_TIME_NOT_UTC");
        if (value.Quantity < 0m)
            throw new InvalidOperationException("ARCH7B_SEND_QUANTITY_OUT_OF_BOUNDS");
        if (value.MessageType is not ("D" or "F" or "H"))
            throw new InvalidOperationException("ARCH7B_SEND_MESSAGE_TYPE_FORBIDDEN");

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await LockRunAsync(context, value.QualificationRunId, cancellationToken);
        var run = await context.Arch7bQualificationRuns.AsNoTracking()
            .SingleAsync(item => item.QualificationRunId == value.QualificationRunId, cancellationToken);
        if (value.Quantity > run.VenueQuantity || value.Quantity % run.QuantityIncrement != 0m)
            throw new InvalidOperationException("ARCH7B_SEND_QUANTITY_OUT_OF_BOUNDS");
        ValidateKnownSend(run, value);

        var existing = await context.Arch7bOrderSendLedger.AsNoTracking()
            .SingleOrDefaultAsync(item => item.SendLedgerId == value.SendLedgerId, cancellationToken);
        var row = new PmsArch7bOrderSendLedgerRow(
            value.SendLedgerId,
            value.QualificationRunId,
            value.LifecycleRole,
            value.MessageType,
            value.ClientOrderId,
            value.OriginalClientOrderId,
            run.Symbol,
            run.SecurityId,
            value.Side,
            value.Quantity,
            value.LimitPrice,
            value.BboSnapshotSha256,
            value.PayloadSha256,
            value.IntentRecordedAtUtc);
        if (existing is not null)
        {
            if (existing != row)
                throw new InvalidOperationException("ARCH7B_SEND_LEDGER_IDEMPOTENCY_CONFLICT");
            await transaction.CommitAsync(cancellationToken);
            return Arch7bPostgreSqlWriteResult.AlreadyPersistedIdentical;
        }

        var sends = await context.Arch7bOrderSendLedger.AsNoTracking()
            .Where(item => item.QualificationRunId == value.QualificationRunId)
            .ToArrayAsync(cancellationToken);
        var proposed = sends.Append(row).ToArray();
        Arch7bKnownOrderQualification.ValidateBudget(new(
            proposed.Count(item => item.MessageType == "D"),
            proposed.Count(item => item.MessageType == "F"),
            proposed.Count(item => item.MessageType == "G"),
            proposed.Count(item => item.MessageType == "H")));
        if (proposed.Count(item => item.MessageType == "D" && item.LifecycleRole == "OPEN") > 1 ||
            proposed.Count(item => item.MessageType == "D" && item.LifecycleRole == "FLATTEN") > 1)
            throw new InvalidOperationException("ARCH7B_SECOND_NEW_ORDER_SINGLE_FORBIDDEN");

        context.Arch7bOrderSendLedger.Add(row);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Arch7bPostgreSqlWriteResult.Persisted;
    }

    public async Task<Arch7bPostgreSqlWriteResult> PersistExecutionReportsAsync(
        Guid qualificationRunId,
        IReadOnlyList<Arch7bExecutionReportEvent> reports,
        CancellationToken cancellationToken = default)
    {
        if (reports.Count == 0)
            throw new InvalidOperationException("ARCH7B_EXECUTION_REPORT_BATCH_EMPTY");
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await LockRunAsync(context, qualificationRunId, cancellationToken);
        var run = await context.Arch7bQualificationRuns.AsNoTracking()
            .SingleAsync(value => value.QualificationRunId == qualificationRunId, cancellationToken);

        var inserted = false;
        foreach (var report in reports.OrderBy(value => value.SessionId, StringComparer.Ordinal)
                     .ThenBy(value => value.SequenceNumber))
        {
            ValidateReport(run, report);
            var executionReportId = DeterministicGuid($"arch7b-execution-report|{report.RawMessageSha256}");
            var row = ToRow(qualificationRunId, executionReportId, report);
            var byHash = await context.Arch7bExecutionReports.AsNoTracking()
                .SingleOrDefaultAsync(value => value.RawMessageSha256 == report.RawMessageSha256, cancellationToken);
            var byExec = await context.Arch7bExecutionReports.AsNoTracking()
                .SingleOrDefaultAsync(value =>
                    value.AccountId == report.AccountId && value.ExecId == report.ExecId, cancellationToken);
            if (byHash is not null)
            {
                if (byHash != row)
                    throw new InvalidOperationException("ARCH7B_EXECUTION_REPORT_IDEMPOTENCY_CONFLICT");
                continue;
            }
            if (byExec is not null)
            {
                if (!EquivalentPossDupReplay(byExec, row) ||
                    !(byExec.PossDup || row.PossDup))
                    throw new InvalidOperationException("ARCH7B_EXECUTION_REPORT_IDEMPOTENCY_CONFLICT");
                continue;
            }

            context.Arch7bExecutionReports.Add(row);
            inserted = true;
            if (!IsValidatedFill(report))
                continue;

            var fillId = DeterministicGuid($"arch7b-fill|{report.ExecId}|{report.RawMessageSha256}");
            var fill = new PmsArch7bFillRow(
                fillId,
                qualificationRunId,
                executionReportId,
                report.ExecId,
                report.OrderId,
                report.ClOrdId,
                report.Symbol,
                report.SecurityId,
                report.Side,
                report.LastQty,
                report.LastPx,
                report.TransactTimeUtc,
                report.RawMessageSha256,
                "BROKER_FEES_UNAVAILABLE_NOT_ASSUMED_ZERO",
                null,
                null);
            context.Arch7bFills.Add(fill);
            var signedQuantity = report.Side == "BUY" ? report.LastQty : -report.LastQty;
            var eventSha = Sha256(string.Join("|",
                fillId.ToString("D"),
                report.ExecId,
                report.Symbol,
                signedQuantity.ToString("G29", CultureInfo.InvariantCulture),
                report.LastPx.ToString("G29", CultureInfo.InvariantCulture),
                report.TransactTimeUtc.ToString("O"),
                report.RawMessageSha256));
            context.Arch7bPositionLedgerEvents.Add(new(
                DeterministicGuid($"arch7b-position-ledger|{fillId:D}"),
                qualificationRunId,
                fillId,
                report.ExecId,
                report.Symbol,
                report.SecurityId,
                report.Symbol[..3],
                report.Symbol[3..],
                signedQuantity,
                report.LastPx,
                report.TransactTimeUtc,
                report.RawMessageSha256,
                eventSha));
        }

        if (inserted)
            await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return inserted
            ? Arch7bPostgreSqlWriteResult.Persisted
            : Arch7bPostgreSqlWriteResult.AlreadyPersistedIdentical;
    }

    public async Task<Arch7bPostgreSqlRecoveryState> ReadRecoveryStateAsync(
        Guid qualificationRunId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var run = await context.Arch7bQualificationRuns.AsNoTracking()
            .SingleAsync(value => value.QualificationRunId == qualificationRunId, cancellationToken);
        var sends = await context.Arch7bOrderSendLedger.AsNoTracking()
            .Where(value => value.QualificationRunId == qualificationRunId)
            .ToArrayAsync(cancellationToken);
        var reports = await context.Arch7bExecutionReports.AsNoTracking()
            .Where(value => value.QualificationRunId == qualificationRunId)
            .OrderBy(value => value.TransactTimeUtc)
            .ThenBy(value => value.FixSequenceNumber)
            .ToArrayAsync(cancellationToken);
        var opening = LatestRelated(reports, run.OpeningClientOrderId);
        var flatten = LatestRelated(reports, run.FlattenClientOrderId);
        return new(
            sends.Any(value =>
                value.MessageType == "D" &&
                value.LifecycleRole == "OPEN"),
            sends.Any(value =>
                value.MessageType == "F" &&
                value.LifecycleRole == "OPEN_RESIDUAL_CANCEL"),
            sends.Any(value =>
                value.MessageType == "D" &&
                value.LifecycleRole == "FLATTEN"),
            sends.Count(value => value.MessageType == "H"),
            opening?.CumulativeQuantity ?? 0m,
            opening?.LeavesQuantity ?? 0m,
            opening is not null && IsTerminalOrderStatus(opening.OrderStatus),
            flatten?.CumulativeQuantity ?? 0m,
            flatten?.LeavesQuantity ?? 0m,
            flatten is not null && IsTerminalOrderStatus(flatten.OrderStatus),
            sends.SingleOrDefault(value =>
                value.MessageType == "D" &&
                value.LifecycleRole == "OPEN")?.BboSnapshotSha256,
            sends.SingleOrDefault(value =>
                value.MessageType == "D" &&
                value.LifecycleRole == "FLATTEN")?.BboSnapshotSha256);

        static PmsArch7bExecutionReportRow? LatestRelated(
            IEnumerable<PmsArch7bExecutionReportRow> values,
            string clientOrderId)
            => values
                .Where(value =>
                    value.ClientOrderId == clientOrderId ||
                    value.OriginalClientOrderId == clientOrderId)
                .OrderBy(value => value.TransactTimeUtc)
                .ThenBy(value => value.FixSequenceNumber)
                .LastOrDefault();
    }

    public async Task<decimal> ReadValidatedFillQuantityAsync(
        Guid qualificationRunId,
        string clientOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Arch7bFills.AsNoTracking()
            .Where(value =>
                value.QualificationRunId == qualificationRunId &&
                value.ClientOrderId == clientOrderId)
            .SumAsync(value => value.Quantity, cancellationToken);
    }

    public async Task<Arch7bLifecycleEvaluation> ReadLifecycleEvaluationAsync(
        Guid qualificationRunId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var run = await context.Arch7bQualificationRuns.AsNoTracking()
            .SingleAsync(value => value.QualificationRunId == qualificationRunId, cancellationToken);
        var sequenceValidatedSessionIds = await context.Arch7bFixSessionEvents.AsNoTracking()
            .Where(value =>
                value.QualificationRunId == qualificationRunId &&
                value.EventType == "FIX_SESSION_SEQUENCE_CONTINUITY_VALIDATED")
            .Select(value => value.SessionId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var rows = await context.Arch7bExecutionReports.AsNoTracking()
            .Where(value => value.QualificationRunId == qualificationRunId)
            .OrderBy(value => value.TransactTimeUtc)
            .ThenBy(value => value.FixSequenceNumber)
            .ToArrayAsync(cancellationToken);
        if (rows.Length == 0 ||
            rows.Select(value => value.SessionId)
                .Distinct(StringComparer.Ordinal)
                .Any(value => !sequenceValidatedSessionIds.Contains(value, StringComparer.Ordinal)))
            throw new InvalidOperationException("ARCH7B_FIX_SEQUENCE_CONTINUITY_NOT_VALIDATED");
        var reports = rows.Select(value => new Arch7bExecutionReportEvent(
            value.SessionId,
            value.FixSequenceNumber,
            value.AccountId,
            value.OrderId,
            value.ClientOrderId,
            value.OriginalClientOrderId,
            value.ExecId,
            value.ExecType,
            value.OrderStatus,
            value.Symbol,
            value.SecurityId,
            value.Side,
            value.OrderQuantity,
            value.CumulativeQuantity,
            value.LeavesQuantity,
            value.LastQuantity,
            value.LastPrice,
            value.AveragePrice,
            value.LimitPrice,
            value.TransactTimeUtc,
            value.PossDup,
            value.RawMessageSha256)).ToArray();
        return Arch7bKnownOrderQualification.EvaluateLifecycle(
            reports,
            run.OpeningClientOrderId,
            run.FlattenClientOrderId,
            ExecutionProfile(run),
            run.CancelClientOrderId,
            fullFixSessionSequenceValidated: true);
    }

    public async Task<Arch7bPostgreSqlWriteResult> PersistFinalReconciliationAsync(
        Arch7bFinalReconciliationEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        if (!evidence.Lifecycle.Qualified || !evidence.Lifecycle.Flat ||
            evidence.BrokerResidualQuantity != 0m ||
            evidence.BrokerEvidenceAuthority == "INTERNAL_LEDGER_ONLY")
            throw new InvalidOperationException("ARCH7B_FINAL_RECONCILIATION_NOT_FLAT");
        RequireSha(evidence.EvidenceSha256, "ARCH7B_RECONCILIATION_SHA256_INVALID");
        RequireUtc(evidence.CompletedAtUtc, "ARCH7B_RECONCILIATION_TIME_NOT_UTC");

        await using var read = await contextFactory.CreateDbContextAsync(cancellationToken);
        var openingCumQty = await LatestCumulativeQuantity(
            read, evidence.QualificationRunId, "OPEN", cancellationToken);
        var flattenCumQty = await LatestCumulativeQuantity(
            read, evidence.QualificationRunId, "FLATTEN", cancellationToken);
        var row = new PmsArch7bFinalReconciliationRow(
            evidence.ReconciliationId,
            evidence.QualificationRunId,
            "FLAT_RECONCILED",
            evidence.BrokerEvidenceAuthority,
            openingCumQty,
            evidence.Lifecycle.OpeningFilledQuantity,
            flattenCumQty,
            evidence.Lifecycle.FlattenFilledQuantity,
            evidence.Lifecycle.KnownWorkingOrderCount,
            evidence.Lifecycle.InternalPosition,
            evidence.BrokerResidualQuantity,
            evidence.Lifecycle.ResidualQuantity,
            evidence.Lifecycle.CriticalBreakCount,
            System.Text.Json.JsonSerializer.Serialize(evidence.Lifecycle.Issues),
            evidence.Lifecycle.RealizedPnlBeforeFees,
            evidence.Lifecycle.FeeStatus,
            evidence.EvidenceSha256,
            evidence.CompletedAtUtc);
        await read.DisposeAsync();

        return await InsertIdenticalAsync(
            evidence.QualificationRunId,
            context => context.Arch7bFinalReconciliations.AsNoTracking()
                .SingleOrDefaultAsync(value =>
                    value.ReconciliationId == evidence.ReconciliationId, cancellationToken),
            row,
            context => context.Arch7bFinalReconciliations.Add(row),
            "ARCH7B_RECONCILIATION_IDEMPOTENCY_CONFLICT",
            cancellationToken);
    }

    private async Task<Arch7bPostgreSqlWriteResult> InsertIdenticalAsync<TRow>(
        Guid qualificationRunId,
        Func<PmsShadowDbContext, Task<TRow?>> readExisting,
        TRow row,
        Action<PmsShadowDbContext> add,
        string conflict,
        CancellationToken cancellationToken)
        where TRow : class
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await LockRunAsync(context, qualificationRunId, cancellationToken);
        var existing = await readExisting(context);
        if (existing is not null)
        {
            if (!existing.Equals(row))
                throw new InvalidOperationException(conflict);
            await transaction.CommitAsync(cancellationToken);
            return Arch7bPostgreSqlWriteResult.AlreadyPersistedIdentical;
        }
        add(context);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Arch7bPostgreSqlWriteResult.Persisted;
    }

    private static void ValidateKnownSend(PmsArch7bQualificationRunRow run, Arch7bOrderSendIntent send)
    {
        var valid = send switch
        {
            { MessageType: "D", LifecycleRole: "OPEN" } =>
                send.ClientOrderId == run.OpeningClientOrderId &&
                send.OriginalClientOrderId is null &&
                send.Side == run.OpeningSide &&
                send.Quantity == run.VenueQuantity &&
                send.LimitPrice is > 0m,
            { MessageType: "D", LifecycleRole: "FLATTEN" } =>
                send.ClientOrderId == run.FlattenClientOrderId &&
                send.OriginalClientOrderId is null &&
                send.Side == Opposite(run.OpeningSide) &&
                send.Quantity is > 0m &&
                send.Quantity <= run.VenueQuantity &&
                send.LimitPrice is > 0m,
            { MessageType: "F", LifecycleRole: "OPEN_RESIDUAL_CANCEL" } =>
                send.ClientOrderId == run.CancelClientOrderId &&
                send.OriginalClientOrderId == run.OpeningClientOrderId &&
                send.Quantity == run.VenueQuantity,
            { MessageType: "H", LifecycleRole: "OPEN_STATUS" } =>
                send.ClientOrderId == run.OpeningClientOrderId,
            { MessageType: "H", LifecycleRole: "FLATTEN_STATUS" } =>
                send.ClientOrderId == run.FlattenClientOrderId,
            _ => false
        };
        if (!valid)
            throw new InvalidOperationException("ARCH7B_SEND_NOT_IN_KNOWN_ORDER_REGISTRY");
    }

    private static void ValidateReport(
        PmsArch7bQualificationRunRow run,
        Arch7bExecutionReportEvent report)
    {
        var known = report.ClOrdId == run.OpeningClientOrderId ||
                    report.ClOrdId == run.FlattenClientOrderId ||
                    report.ClOrdId == run.CancelClientOrderId ||
                    report.OrigClOrdId == run.OpeningClientOrderId ||
                    report.OrigClOrdId == run.FlattenClientOrderId;
        if (!known)
            throw new InvalidOperationException("ARCH7B_UNKNOWN_CLORDID");
        if (report.AccountId != run.AccountId)
            throw new InvalidOperationException(run.AccountId ==
                Arch7bKnownOrderQualificationPolicy.DemoAccountId
                ? "ARCH7B_DEMO_ACCOUNT_IDENTITY_MISMATCH"
                : "ARCH7B_EXECUTION_REPORT_ACCOUNT_BINDING_MISMATCH");
        if (report.Symbol != run.Symbol || report.SecurityId != run.SecurityId)
            throw new InvalidOperationException("ARCH7B_EXECUTION_REPORT_INSTRUMENT_MISMATCH");
        if (report.SequenceNumber <= 0 || string.IsNullOrWhiteSpace(report.ExecId) ||
            string.IsNullOrWhiteSpace(report.OrderId))
            throw new InvalidOperationException("ARCH7B_EXECUTION_REPORT_IDENTITY_INCOMPLETE");
        RequireSha(report.RawMessageSha256, "ARCH7B_RAW_MESSAGE_SHA256_INVALID");
        RequireUtc(report.TransactTimeUtc, "ARCH7B_EXECUTION_REPORT_TIME_NOT_UTC");
        var quantityValid = report.OrderQty <= 0m || report.OrdStatus switch
        {
            "2" => report.LeavesQty == 0m && report.CumQty == report.OrderQty,
            "4" or "8" or "C" =>
                report.LeavesQty == 0m && report.CumQty <= report.OrderQty,
            _ => Math.Abs(report.OrderQty - report.CumQty - report.LeavesQty) <= 0.00000001m
        };
        if (report.OrderQty < 0m ||
            report.CumQty < 0m ||
            report.LeavesQty < 0m ||
            report.LastQty < 0m ||
            report.LastPx < 0m ||
            !quantityValid)
            throw new InvalidOperationException("ARCH7B_EXECUTION_REPORT_NUMERIC_INVALID");
    }

    private static bool IsValidatedFill(Arch7bExecutionReportEvent report)
        => report.ExecType == "F" &&
           report.OrdStatus is "1" or "2" &&
           report.LastQty > 0m &&
           report.LastPx > 0m;

    private static Arch7bKnownOrderExecutionProfile ExecutionProfile(
        PmsArch7bQualificationRunRow run)
        => new(
            run.Gate,
            run.Scope,
            run.Environment,
            run.AccountId,
            run.Symbol,
            run.SecurityId,
            run.SecurityIdSource,
            run.OpeningSide,
            run.VenueQuantity,
            run.QuantityIncrement,
            run.PriceIncrement,
            Arch7bKnownOrderQualificationPolicy.CollarPips,
            Arch7bKnownOrderQualificationPolicy.MaximumBboAgeSeconds,
            Arch7bKnownOrderQualificationPolicy.MaximumLifecycleSeconds,
            Arch7bKnownOrderQualificationPolicy.MaximumNewOrderSingleCount,
            Arch7bKnownOrderQualificationPolicy.MaximumCancelCount,
            Arch7bKnownOrderQualificationPolicy.MaximumReplaceCount,
            Arch7bKnownOrderQualificationPolicy.MaximumOrderStatusRequestCount,
            Arch7bKnownOrderQualificationPolicy.OpeningLimitPolicy,
            Arch7bKnownOrderQualificationPolicy.FlattenLimitPolicy,
            run.ExternalOrManualOrderCoverage);

    private static PmsArch7bExecutionReportRow ToRow(
        Guid qualificationRunId,
        Guid executionReportId,
        Arch7bExecutionReportEvent report)
        => new(
            executionReportId,
            qualificationRunId,
            report.SessionId,
            report.SequenceNumber,
            report.AccountId,
            report.OrderId,
            report.ClOrdId,
            report.OrigClOrdId,
            report.ExecId,
            report.ExecType,
            report.OrdStatus,
            report.Symbol,
            report.SecurityId,
            report.Side,
            report.OrderQty,
            report.CumQty,
            report.LeavesQty,
            report.LastQty,
            report.LastPx,
            report.AvgPx,
            report.Price,
            report.TransactTimeUtc,
            report.PossDup,
            report.RawMessageSha256);

    private static async Task<decimal> LatestCumulativeQuantity(
        PmsShadowDbContext context,
        Guid qualificationRunId,
        string role,
        CancellationToken cancellationToken)
    {
        var run = await context.Arch7bQualificationRuns.AsNoTracking()
            .SingleAsync(value => value.QualificationRunId == qualificationRunId, cancellationToken);
        var clientOrderId = role == "OPEN" ? run.OpeningClientOrderId : run.FlattenClientOrderId;
        return await context.Arch7bExecutionReports.AsNoTracking()
            .Where(value => value.QualificationRunId == qualificationRunId &&
                            (value.ClientOrderId == clientOrderId ||
                             value.OriginalClientOrderId == clientOrderId))
            .OrderByDescending(value => value.TransactTimeUtc)
            .ThenByDescending(value => value.FixSequenceNumber)
            .Select(value => value.CumulativeQuantity)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool EquivalentPossDupReplay(
        PmsArch7bExecutionReportRow left,
        PmsArch7bExecutionReportRow right)
        => left.QualificationRunId == right.QualificationRunId &&
           left.SessionId == right.SessionId &&
           left.AccountId == right.AccountId &&
           left.OrderId == right.OrderId &&
           left.ClientOrderId == right.ClientOrderId &&
           left.OriginalClientOrderId == right.OriginalClientOrderId &&
           left.ExecId == right.ExecId &&
           left.ExecType == right.ExecType &&
           left.OrderStatus == right.OrderStatus &&
           left.Symbol == right.Symbol &&
           left.SecurityId == right.SecurityId &&
           left.Side == right.Side &&
           left.OrderQuantity == right.OrderQuantity &&
           left.CumulativeQuantity == right.CumulativeQuantity &&
           left.LeavesQuantity == right.LeavesQuantity &&
           left.LastQuantity == right.LastQuantity &&
           left.LastPrice == right.LastPrice &&
           left.AveragePrice == right.AveragePrice &&
           left.LimitPrice == right.LimitPrice &&
           left.TransactTimeUtc == right.TransactTimeUtc;

    private static bool IsTerminalOrderStatus(string orderStatus)
        => orderStatus is "2" or "4" or "8" or "C";

    private static async Task LockRunAsync(
        PmsShadowDbContext context,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var lockKey = BitConverter.ToInt64(
            SHA256.HashData(Encoding.UTF8.GetBytes($"arch7b-run|{runId:D}")), 0);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken);
    }

    private static string Opposite(string side) => side == "BUY" ? "SELL" : "BUY";

    private static void RequireSha(string value, string error)
    {
        if (value.Length != 64 || !value.All(Uri.IsHexDigit) ||
            !value.Equals(value.ToLowerInvariant(), StringComparison.Ordinal))
            throw new InvalidOperationException(error);
    }

    private static void RequireUtc(DateTimeOffset value, string error)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new InvalidOperationException(error);
    }

    private static Guid DeterministicGuid(string identity)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes(identity))[..16]);

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

public sealed class Arch7bPostgreSqlExclusiveLease : IAsyncDisposable
{
    private readonly PmsShadowDbContext context;
    private readonly long lockKey;
    private bool released;

    private Arch7bPostgreSqlExclusiveLease(
        PmsShadowDbContext context,
        long lockKey,
        string ownerId,
        DateTimeOffset acquiredAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        this.context = context;
        this.lockKey = lockKey;
        OwnerId = ownerId;
        AcquiredAtUtc = acquiredAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public string OwnerId { get; }
    public DateTimeOffset AcquiredAtUtc { get; }
    public DateTimeOffset ExpiresAtUtc { get; }

    public static async Task<Arch7bPostgreSqlExclusiveLease> AcquireAsync(
        IDbContextFactory<PmsShadowDbContext> contextFactory,
        string ownerId,
        DateTimeOffset acquiredAtUtc,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
        => await AcquireAsync(
            contextFactory,
            ownerId,
            acquiredAtUtc,
            duration,
            Arch7bKnownOrderQualificationPolicy.DemoAccountId,
            cancellationToken);

    public static async Task<Arch7bPostgreSqlExclusiveLease> AcquireAsync(
        IDbContextFactory<PmsShadowDbContext> contextFactory,
        string ownerId,
        DateTimeOffset acquiredAtUtc,
        TimeSpan duration,
        string accountId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || acquiredAtUtc.Offset != TimeSpan.Zero ||
            string.IsNullOrWhiteSpace(accountId) || duration <= TimeSpan.Zero ||
            duration > TimeSpan.FromSeconds(Arch7bKnownOrderQualificationPolicy.MaximumLifecycleSeconds))
            throw new InvalidOperationException("ARCH7B_EXCLUSIVITY_REQUEST_INVALID");
        var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.OpenConnectionAsync(cancellationToken);
        var lockKey = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"arch7b-exclusive|{accountId}")), 0);
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@lock_key)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "lock_key";
        parameter.Value = lockKey;
        command.Parameters.Add(parameter);
        var acquired = await command.ExecuteScalarAsync(cancellationToken) as bool?;
        if (acquired != true)
        {
            await context.DisposeAsync();
            throw new InvalidOperationException("ARCH7B_CONCURRENT_FIX_ORDER_ENTRY_SESSION_DETECTED");
        }
        return new(context, lockKey, ownerId, acquiredAtUtc, acquiredAtUtc.Add(duration));
    }

    public Arch7bExclusivityDeclaration Declaration()
        => new(
            OwnerId,
            AcquiredAtUtc,
            ExpiresAtUtc,
            AdvisoryLeaseHeld: !released,
            NoManualOrdersDeclared: true,
            NoOtherBotDeclared: true,
            NoOtherGatewayDeclared: true,
            NoOtherUserDeclared: true,
            NoOtherTestDeclared: true,
            NoConcurrentFixOrderEntrySessionDeclared: true,
            OneRunOnly: true,
            OneDemoAccountOnly: true);

    public async ValueTask DisposeAsync()
    {
        if (released)
            return;
        released = true;
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT pg_advisory_unlock(@lock_key)";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "lock_key";
            parameter.Value = lockKey;
            command.Parameters.Add(parameter);
            await command.ExecuteScalarAsync();
        }
        finally
        {
            await context.DisposeAsync();
        }
    }
}
