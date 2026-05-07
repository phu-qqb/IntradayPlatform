using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public sealed record LmaxShadowReplayRunFilter(
    int Limit,
    LmaxShadowReplayStatus? Status = null,
    LmaxShadowInputSource? InputSource = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null);

public sealed record LmaxShadowObservationFilter(
    int Limit,
    LmaxShadowReplayRunId? ReplayRunId = null,
    LmaxShadowObservationSeverity? Severity = null,
    LmaxShadowObservationStatus? Status = null,
    LmaxShadowObservationType? Type = null,
    string? Symbol = null,
    string? BrokerExecutionId = null,
    string? BrokerOrderId = null,
    string? ClientOrderId = null,
    string? Fingerprint = null);

public sealed record LmaxShadowReplayRequest(
    LmaxShadowInputSource InputSource,
    IReadOnlyList<LmaxShadowExecutionReportInput>? ExecutionReports,
    IReadOnlyList<LmaxShadowTradeCaptureInput>? TradeCaptureReports,
    IReadOnlyList<LmaxShadowOrderStatusInput>? OrderStatuses,
    IReadOnlyList<LmaxShadowProtocolRejectInput>? ProtocolRejects,
    string Reason,
    string? EvidenceMode = null);

public enum LmaxShadowSourceEventType
{
    ExecutionReport,
    TradeCaptureReport,
    OrderStatus,
    ProtocolReject,
    MarketData
}

public sealed record LmaxShadowObservationPolicyDecision(
    LmaxShadowObservationType ObservationType,
    LmaxShadowObservationSeverity Severity,
    LmaxShadowObservationStatus DefaultStatus,
    string PolicyCode,
    string Message,
    string Rationale,
    string SuggestedOperatorAction,
    bool CreatesExceptionCase,
    bool CreatesAuditEvent,
    string EvidenceMode,
    LmaxShadowSourceEventType SourceEventType);

public static class LmaxShadowObservationPolicy
{
    public static LmaxShadowObservationPolicyDecision Decide(
        string evidenceMode,
        LmaxShadowSourceEventType sourceEventType,
        LmaxShadowObservationType requestedType,
        LmaxShadowObservationSeverity requestedSeverity,
        string message,
        string? executionType = null,
        bool internalOrderExists = false,
        bool internalFillExists = false,
        string? protocolRefMsgType = null,
        string? protocolText = null)
    {
        var normalizedMode = string.IsNullOrWhiteSpace(evidenceMode) ? "Unknown" : evidenceMode;
        var severity = requestedSeverity;
        var policyCode = $"LMAX_SHADOW_{requestedType.ToString().ToUpperInvariant()}";
        var rationale = "Default shadow observation policy.";
        var action = "Review the replay payload and compare it with internal OMS/fill state.";

        if (requestedType == LmaxShadowObservationType.TradeCaptureMissingInternalFill)
        {
            severity = LmaxShadowObservationSeverity.Warning;
            policyCode = "LMAX_SHADOW_TC_MISSING_INTERNAL_FILL_READONLY";
            rationale = "TradeCapture AE is read-only recovery evidence. In lab/read-only mode a missing internal fill is an investigation warning, not a runtime blocker.";
            action = "Check whether the evidence came from a lab/demo order or from an expected internal fill. Do not mutate trading state from replay.";
        }
        else if (requestedType == LmaxShadowObservationType.ExecutionReportMissingInternalFill)
        {
            severity = LmaxShadowObservationSeverity.Warning;
            policyCode = "LMAX_SHADOW_ER_FILL_MISSING_INTERNAL_LAB";
            rationale = "Offline lab/synthetic execution evidence can be ahead of internal state. Replay records the mismatch without blocking runtime.";
            action = "Inspect the originating evidence file and confirm whether this fill should exist internally.";
        }
        else if (requestedType == LmaxShadowObservationType.UnknownLmaxOrder)
        {
            severity = LmaxShadowObservationSeverity.Warning;
            policyCode = "LMAX_SHADOW_ORDER_STATUS_UNKNOWN_ORDER_READONLY";
            rationale = "OrderStatus ExecType=I is status-only. A missing internal order from read-only evidence is non-blocking in lab mode.";
            action = "Confirm the ClOrdID/BrokerOrderID and whether this status belongs to a lab-only order.";
        }
        else if (requestedType == LmaxShadowObservationType.OrderStatusMatchesInternalOrder)
        {
            severity = LmaxShadowObservationSeverity.Info;
            policyCode = "LMAX_SHADOW_ORDER_STATUS_MATCH";
            rationale = "LMAX status-only evidence matches the internal child order status.";
            action = "No operator action required.";
        }
        else if (requestedType == LmaxShadowObservationType.OrderStatusMismatch)
        {
            severity = LmaxShadowObservationSeverity.Warning;
            policyCode = "LMAX_SHADOW_ORDER_STATUS_MISMATCH";
            rationale = "LMAX status-only evidence differs from the internal child order status but does not imply a fill.";
            action = "Review the child order lifecycle and recovery evidence.";
        }
        else if (requestedType == LmaxShadowObservationType.ExecutionReportMatchesInternalFill)
        {
            severity = LmaxShadowObservationSeverity.Info;
            policyCode = "LMAX_SHADOW_ER_FILL_MATCH";
            rationale = "LMAX ExecutionReport fill evidence matches an internal fill by broker execution id.";
            action = "No operator action required.";
        }
        else if (requestedType == LmaxShadowObservationType.TradeCaptureMatchesInternalFill)
        {
            severity = LmaxShadowObservationSeverity.Info;
            policyCode = "LMAX_SHADOW_TC_FILL_MATCH";
            rationale = "LMAX TradeCapture recovery evidence matches an internal fill by broker execution id.";
            action = "No operator action required. Use EOD reports for official TradeUTI reconciliation.";
        }
        else if (requestedType == LmaxShadowObservationType.ProtocolRejectObserved)
        {
            var isReadOnlyReject = string.Equals(protocolRefMsgType, "AD", StringComparison.OrdinalIgnoreCase)
                || string.Equals(protocolRefMsgType, "H", StringComparison.OrdinalIgnoreCase)
                || string.Equals(protocolRefMsgType, "V", StringComparison.OrdinalIgnoreCase);
            severity = isReadOnlyReject ? LmaxShadowObservationSeverity.Warning : LmaxShadowObservationSeverity.Blocking;
            policyCode = isReadOnlyReject ? "LMAX_SHADOW_PROTOCOL_REJECT_READONLY" : "LMAX_SHADOW_PROTOCOL_REJECT_ORDER_PATH";
            rationale = isReadOnlyReject
                ? "The reject was for a read-only recovery or market-data request and should be reviewed without creating a runtime trading block."
                : "The reject references an order-submission or unknown path and is treated as blocking evidence until reviewed.";
            action = isReadOnlyReject
                ? "Review FIX tags, request window, and lab command inputs."
                : "Investigate protocol compatibility before any future runtime adapter activation.";
        }
        else if (requestedType == LmaxShadowObservationType.DuplicateExecutionObserved)
        {
            severity = LmaxShadowObservationSeverity.Warning;
            policyCode = "LMAX_SHADOW_DUPLICATE_EXECUTION";
            rationale = "Duplicate ExecID evidence must not create duplicate fills.";
            action = "Confirm idempotency handling and source evidence duplication.";
        }

        return new(
            requestedType,
            severity,
            LmaxShadowObservationStatus.Open,
            policyCode,
            message,
            rationale,
            action,
            severity == LmaxShadowObservationSeverity.Blocking,
            true,
            normalizedMode,
            sourceEventType);
    }
}

public sealed record LmaxShadowExecutionReportInput(
    string? ExecId,
    string? BrokerOrderId,
    string? ClientOrderId,
    string? ExecutionType,
    string? OrderStatus,
    InstrumentId? InstrumentId,
    string? Symbol,
    string? Side,
    decimal? LastQty,
    decimal? LastPx,
    decimal? LeavesQty,
    decimal? CumQty,
    decimal? AvgPx,
    DateTimeOffset? TransactTimeUtc,
    object? Payload = null);

public sealed record LmaxShadowTradeCaptureInput(
    string? ExecId,
    string? SecondaryExecId,
    string? BrokerOrderId,
    string? ClientOrderId,
    InstrumentId? InstrumentId,
    string? Symbol,
    string? Side,
    decimal? LastQty,
    decimal? LastPx,
    DateOnly? TradeDate,
    DateTimeOffset? TransactTimeUtc,
    string? TradeUti,
    bool? LastReportRequested,
    object? Payload = null);

public sealed record LmaxShadowOrderStatusInput(
    string? BrokerOrderId,
    string? ClientOrderId,
    InstrumentId? InstrumentId,
    string? Symbol,
    string? OrderStatus,
    decimal? CumQty,
    decimal? LeavesQty,
    DateTimeOffset? TransactTimeUtc,
    object? Payload = null);

public sealed record LmaxShadowProtocolRejectInput(
    string? RefMsgType,
    int? RefTagId,
    int? ReasonCode,
    string? Text,
    string? ClientOrderId,
    string? BrokerOrderId,
    object? Payload = null);

public interface ILmaxShadowRepository
{
    Task AddReplayRunAsync(LmaxShadowReplayRun run, CancellationToken cancellationToken);
    Task UpdateReplayRunAsync(LmaxShadowReplayRun run, CancellationToken cancellationToken);
    Task<LmaxShadowReplayRun?> GetReplayRunAsync(LmaxShadowReplayRunId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxShadowReplayRun>> GetReplayRunsAsync(LmaxShadowReplayRunFilter filter, CancellationToken cancellationToken);
    Task AddObservationAsync(LmaxShadowObservation observation, CancellationToken cancellationToken);
    Task UpdateObservationAsync(LmaxShadowObservation observation, CancellationToken cancellationToken);
    Task<LmaxShadowObservation?> GetObservationAsync(LmaxShadowObservationId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxShadowObservation>> GetObservationsAsync(LmaxShadowObservationFilter filter, CancellationToken cancellationToken);
}

public interface ILmaxShadowReplayService
{
    Task<LmaxShadowReplayRun> ReplayAsync(LmaxShadowReplayRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxShadowObservation>> CompareExecutionReportsAsync(IReadOnlyList<LmaxShadowExecutionReportInput> reports, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxShadowObservation>> CompareTradeCaptureReportsAsync(IReadOnlyList<LmaxShadowTradeCaptureInput> reports, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxShadowObservation>> CompareOrderStatusesAsync(IReadOnlyList<LmaxShadowOrderStatusInput> statuses, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxShadowReplayRun>> GetReplayRunsAsync(LmaxShadowReplayRunFilter filter, CancellationToken cancellationToken);
    Task<LmaxShadowReplayRun?> GetReplayRunAsync(LmaxShadowReplayRunId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<LmaxShadowObservation>> GetObservationsAsync(LmaxShadowObservationFilter filter, CancellationToken cancellationToken);
    Task<LmaxShadowObservation> AcknowledgeObservationAsync(LmaxShadowObservationId id, string reason, CancellationToken cancellationToken);
    Task<LmaxShadowObservation> ResolveObservationAsync(LmaxShadowObservationId id, string reason, CancellationToken cancellationToken);
    Task<LmaxShadowObservation> IgnoreObservationAsync(LmaxShadowObservationId id, string reason, CancellationToken cancellationToken);
}

public sealed class InMemoryLmaxShadowRepository(PlatformState state) : ILmaxShadowRepository
{
    private readonly object _sync = new();

    public Task AddReplayRunAsync(LmaxShadowReplayRun run, CancellationToken cancellationToken)
    {
        lock (_sync) state.LmaxShadowReplayRuns.Add(run);
        return Task.CompletedTask;
    }

    public Task UpdateReplayRunAsync(LmaxShadowReplayRun run, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var index = state.LmaxShadowReplayRuns.FindIndex(x => x.Id == run.Id);
            if (index >= 0) state.LmaxShadowReplayRuns[index] = run;
        }
        return Task.CompletedTask;
    }

    public Task<LmaxShadowReplayRun?> GetReplayRunAsync(LmaxShadowReplayRunId id, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult(state.LmaxShadowReplayRuns.FirstOrDefault(x => x.Id == id));
    }

    public Task<IReadOnlyList<LmaxShadowReplayRun>> GetReplayRunsAsync(LmaxShadowReplayRunFilter filter, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<LmaxShadowReplayRun>>(ApplyReplayFilter(state.LmaxShadowReplayRuns, filter).ToList());
        }
    }

    public Task AddObservationAsync(LmaxShadowObservation observation, CancellationToken cancellationToken)
    {
        lock (_sync) state.LmaxShadowObservations.Add(observation);
        return Task.CompletedTask;
    }

    public Task UpdateObservationAsync(LmaxShadowObservation observation, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var index = state.LmaxShadowObservations.FindIndex(x => x.Id == observation.Id);
            if (index >= 0) state.LmaxShadowObservations[index] = observation;
        }
        return Task.CompletedTask;
    }

    public Task<LmaxShadowObservation?> GetObservationAsync(LmaxShadowObservationId id, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult(state.LmaxShadowObservations.FirstOrDefault(x => x.Id == id));
    }

    public Task<IReadOnlyList<LmaxShadowObservation>> GetObservationsAsync(LmaxShadowObservationFilter filter, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<LmaxShadowObservation>>(ApplyObservationFilter(state.LmaxShadowObservations, filter).ToList());
        }
    }

    public static IEnumerable<LmaxShadowReplayRun> ApplyReplayFilter(IEnumerable<LmaxShadowReplayRun> runs, LmaxShadowReplayRunFilter filter)
    {
        var query = runs;
        if (filter.Status is not null) query = query.Where(x => x.Status == filter.Status);
        if (filter.InputSource is not null) query = query.Where(x => x.InputSource == filter.InputSource);
        if (filter.FromUtc is not null) query = query.Where(x => x.StartedAtUtc >= filter.FromUtc);
        if (filter.ToUtc is not null) query = query.Where(x => x.StartedAtUtc <= filter.ToUtc);
        return query.OrderByDescending(x => x.StartedAtUtc).Take(Math.Clamp(filter.Limit, 1, 500));
    }

    public static IEnumerable<LmaxShadowObservation> ApplyObservationFilter(IEnumerable<LmaxShadowObservation> observations, LmaxShadowObservationFilter filter)
    {
        var query = observations;
        if (filter.ReplayRunId is not null) query = query.Where(x => x.ReplayRunId == filter.ReplayRunId);
        if (filter.Severity is not null) query = query.Where(x => x.Severity == filter.Severity);
        if (filter.Status is not null) query = query.Where(x => x.Status == filter.Status);
        if (filter.Type is not null) query = query.Where(x => x.Type == filter.Type);
        if (!string.IsNullOrWhiteSpace(filter.Symbol)) query = query.Where(x => string.Equals(x.Symbol, filter.Symbol, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.BrokerExecutionId)) query = query.Where(x => string.Equals(x.BrokerExecutionId, filter.BrokerExecutionId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.BrokerOrderId)) query = query.Where(x => string.Equals(x.BrokerOrderId, filter.BrokerOrderId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.ClientOrderId)) query = query.Where(x => string.Equals(x.ClientOrderId, filter.ClientOrderId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.Fingerprint)) query = query.Where(x => string.Equals(x.Fingerprint, filter.Fingerprint, StringComparison.OrdinalIgnoreCase));
        return query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(filter.Limit, 1, 500));
    }
}

public sealed class LmaxShadowModeService(
    ILmaxShadowRepository repository,
    IIntradayRepository intradayRepository,
    IOperatorAuditService audit,
    IExceptionCaseService exceptionCaseService,
    IOperatorContext operatorContext,
    IClock clock) : ILmaxShadowReplayService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<LmaxShadowReplayRun> ReplayAsync(LmaxShadowReplayRequest request, CancellationToken cancellationToken)
    {
        RequireReason(request.Reason);
        var now = clock.UtcNow;
        var run = new LmaxShadowReplayRun(
            LmaxShadowReplayRunId.New(),
            request.InputSource,
            LmaxShadowReplayStatus.Running,
            now,
            null,
            OperatorAuditService.SerializeSanitized(request),
            null,
            InputEventCount(request),
            0,
            0,
            0,
            0,
            0,
            "Replay running.",
            operatorContext.CorrelationId,
            now);

        await repository.AddReplayRunAsync(run, cancellationToken);
        await AuditAsync(OperatorAuditEventType.LmaxShadowReplayStarted, OperatorAuditResult.Started, OperatorAuditSeverity.Info, "LMAX shadow replay started.", run.Id.Value.ToString("D"), request.Reason, request, cancellationToken);

        try
        {
            var state = await intradayRepository.LoadStateAsync(cancellationToken);
            var inputEventCount = InputEventCount(request);
            var uniqueEventCount = UniqueEventKeys(request).Count;
            var duplicateEventCount = Math.Max(0, inputEventCount - uniqueEventCount);
            var evidenceMode = InferEvidenceMode(request);
            var observations = DeduplicateObservations(BuildObservations(run.Id, request, state, now, evidenceMode));
            foreach (var observation in observations)
            {
                await repository.AddObservationAsync(observation, cancellationToken);
                await AuditAsync(OperatorAuditEventType.LmaxShadowObservationCreated, OperatorAuditResult.Succeeded, AuditSeverity(observation.Severity), observation.Description, observation.Id.Value.ToString("D"), null, observation, cancellationToken);
                if (ObservationCreatesExceptionCase(observation))
                {
                    await CreateExceptionCaseAsync(observation, cancellationToken);
                }
            }

            var warningCount = observations.Count(x => x.Severity == LmaxShadowObservationSeverity.Warning);
            var blockingCount = observations.Count(x => x.Severity == LmaxShadowObservationSeverity.Blocking);
            var status = blockingCount > 0 || warningCount > 0 ? LmaxShadowReplayStatus.CompletedWithWarnings : LmaxShadowReplayStatus.Completed;
            var completed = clock.UtcNow;
            run = run with
            {
                Status = status,
                CompletedAtUtc = completed,
                InputEventCount = inputEventCount,
                UniqueEventCount = uniqueEventCount,
                DuplicateEventCount = duplicateEventCount,
                ObservationCount = observations.Count,
                BlockingObservationCount = blockingCount,
                WarningObservationCount = warningCount,
                Message = status == LmaxShadowReplayStatus.Completed ? "Replay completed with matching shadow observations." : "Replay completed with shadow warnings.",
                OutputJson = OperatorAuditService.SerializeSanitized(new
                {
                    evidenceMode,
                    inputEventCount,
                    uniqueEventCount,
                    duplicateEventCount,
                    observationCount = observations.Count,
                    warningCount,
                    blockingCount,
                    countsBySeverity = observations.GroupBy(x => x.Severity.ToString()).ToDictionary(x => x.Key, x => x.Count()),
                    countsByObservationType = observations.GroupBy(x => x.Type.ToString()).ToDictionary(x => x.Key, x => x.Count()),
                    countsByPolicyCode = observations.Select(ExtractPolicyMetadata).GroupBy(x => x.PolicyCode ?? "Unknown").ToDictionary(x => x.Key, x => x.Count()),
                    blockingReasons = observations.Where(x => x.Severity == LmaxShadowObservationSeverity.Blocking).Select(x => x.Description).ToArray(),
                    fingerprints = observations.Select(x => x.Fingerprint).ToArray()
                })
            };
            await repository.UpdateReplayRunAsync(run, cancellationToken);
            await AuditAsync(OperatorAuditEventType.LmaxShadowReplayCompleted, OperatorAuditResult.Succeeded, warningCount + blockingCount > 0 ? OperatorAuditSeverity.Warning : OperatorAuditSeverity.Info, "LMAX shadow replay completed.", run.Id.Value.ToString("D"), null, run, cancellationToken);
            return run;
        }
        catch (Exception ex)
        {
            run = run with
            {
                Status = LmaxShadowReplayStatus.Failed,
                CompletedAtUtc = clock.UtcNow,
                Message = ex.Message,
                OutputJson = OperatorAuditService.SerializeSanitized(new { error = ex.Message })
            };
            await repository.UpdateReplayRunAsync(run, cancellationToken);
            await AuditAsync(OperatorAuditEventType.LmaxShadowReplayFailed, OperatorAuditResult.Failed, OperatorAuditSeverity.Critical, "LMAX shadow replay failed.", run.Id.Value.ToString("D"), ex.Message, run, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<LmaxShadowObservation>> CompareExecutionReportsAsync(IReadOnlyList<LmaxShadowExecutionReportInput> reports, CancellationToken cancellationToken)
    {
        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        var request = new LmaxShadowReplayRequest(LmaxShadowInputSource.ManualJson, reports, [], [], [], "Comparison only");
        return BuildObservations(null, request, state, clock.UtcNow, InferEvidenceMode(request));
    }

    public async Task<IReadOnlyList<LmaxShadowObservation>> CompareTradeCaptureReportsAsync(IReadOnlyList<LmaxShadowTradeCaptureInput> reports, CancellationToken cancellationToken)
    {
        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        var request = new LmaxShadowReplayRequest(LmaxShadowInputSource.ManualJson, [], reports, [], [], "Comparison only");
        return BuildObservations(null, request, state, clock.UtcNow, InferEvidenceMode(request));
    }

    public async Task<IReadOnlyList<LmaxShadowObservation>> CompareOrderStatusesAsync(IReadOnlyList<LmaxShadowOrderStatusInput> statuses, CancellationToken cancellationToken)
    {
        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        var request = new LmaxShadowReplayRequest(LmaxShadowInputSource.ManualJson, [], [], statuses, [], "Comparison only");
        return BuildObservations(null, request, state, clock.UtcNow, InferEvidenceMode(request));
    }

    public Task<IReadOnlyList<LmaxShadowReplayRun>> GetReplayRunsAsync(LmaxShadowReplayRunFilter filter, CancellationToken cancellationToken)
        => repository.GetReplayRunsAsync(filter, cancellationToken);

    public Task<LmaxShadowReplayRun?> GetReplayRunAsync(LmaxShadowReplayRunId id, CancellationToken cancellationToken)
        => repository.GetReplayRunAsync(id, cancellationToken);

    public Task<IReadOnlyList<LmaxShadowObservation>> GetObservationsAsync(LmaxShadowObservationFilter filter, CancellationToken cancellationToken)
        => repository.GetObservationsAsync(filter, cancellationToken);

    public Task<LmaxShadowObservation> AcknowledgeObservationAsync(LmaxShadowObservationId id, string reason, CancellationToken cancellationToken)
        => TransitionObservationAsync(id, LmaxShadowObservationStatus.Acknowledged, OperatorAuditEventType.LmaxShadowObservationAcknowledged, reason, cancellationToken);

    public Task<LmaxShadowObservation> ResolveObservationAsync(LmaxShadowObservationId id, string reason, CancellationToken cancellationToken)
        => TransitionObservationAsync(id, LmaxShadowObservationStatus.Resolved, OperatorAuditEventType.LmaxShadowObservationResolved, reason, cancellationToken);

    public Task<LmaxShadowObservation> IgnoreObservationAsync(LmaxShadowObservationId id, string reason, CancellationToken cancellationToken)
        => TransitionObservationAsync(id, LmaxShadowObservationStatus.Ignored, OperatorAuditEventType.LmaxShadowObservationIgnored, reason, cancellationToken);

    private async Task<LmaxShadowObservation> TransitionObservationAsync(LmaxShadowObservationId id, LmaxShadowObservationStatus nextStatus, OperatorAuditEventType eventType, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason);
        var current = await repository.GetObservationAsync(id, cancellationToken) ?? throw new DomainRuleViolationException("Shadow observation was not found.");
        if (current.Status == nextStatus)
        {
            throw new DomainRuleViolationException($"Shadow observation is already {nextStatus}.");
        }

        if (current.Status is LmaxShadowObservationStatus.Resolved or LmaxShadowObservationStatus.Ignored)
        {
            throw new DomainRuleViolationException($"Shadow observation cannot transition from {current.Status} to {nextStatus}.");
        }

        if (nextStatus == LmaxShadowObservationStatus.Acknowledged && current.Status != LmaxShadowObservationStatus.Open)
        {
            throw new DomainRuleViolationException("Only open shadow observations can be acknowledged.");
        }

        var updated = current with { Status = nextStatus };
        await repository.UpdateObservationAsync(updated, cancellationToken);
        await AuditAsync(eventType, OperatorAuditResult.Succeeded, OperatorAuditSeverity.Info, $"LMAX shadow observation marked {nextStatus}.", updated.Id.Value.ToString("D"), reason, new { observationId = updated.Id.Value.ToString("D"), updated.Fingerprint, updated.ReplayRunId, updated.Type, updated.Severity, previousStatus = current.Status, status = updated.Status }, cancellationToken);
        return updated;
    }

    private IReadOnlyList<LmaxShadowObservation> BuildObservations(LmaxShadowReplayRunId? replayRunId, LmaxShadowReplayRequest request, PlatformState state, DateTimeOffset now, string evidenceMode)
    {
        var observations = new List<LmaxShadowObservation>();
        var executionReports = request.ExecutionReports ?? [];
        var tradeReports = request.TradeCaptureReports ?? [];
        var orderStatuses = request.OrderStatuses ?? [];
        var protocolRejects = request.ProtocolRejects ?? [];
        var internalFillsByExecId = state.Fills.Where(x => !string.IsNullOrWhiteSpace(x.BrokerExecutionId)).ToDictionary(x => x.BrokerExecutionId, StringComparer.OrdinalIgnoreCase);
        var reportsByExecId = new Dictionary<string, LmaxShadowExecutionReportInput>(StringComparer.OrdinalIgnoreCase);

        foreach (var report in executionReports)
        {
            if (IsOrderStatus(report.ExecutionType))
            {
                observations.Add(CompareOrderStatus(replayRunId, evidenceMode, ToOrderStatus(report), state, now));
                continue;
            }

            if (!IsFill(report.ExecutionType, report.LeavesQty))
            {
                observations.Add(NewObservation(replayRunId, evidenceMode, LmaxShadowSourceEventType.ExecutionReport, LmaxShadowObservationType.UnknownLmaxExecution, LmaxShadowObservationSeverity.Info, report.InstrumentId, report.Symbol, report.ExecId, report.BrokerOrderId, report.ClientOrderId, null, null, "LMAX execution report observed but did not represent a fill candidate.", report, null, null, now, executionType: report.ExecutionType));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(report.ExecId) && !reportsByExecId.TryAdd(report.ExecId, report))
            {
                observations.Add(NewObservation(replayRunId, evidenceMode, LmaxShadowSourceEventType.ExecutionReport, LmaxShadowObservationType.DuplicateExecutionObserved, LmaxShadowObservationSeverity.Warning, report.InstrumentId, report.Symbol, report.ExecId, report.BrokerOrderId, report.ClientOrderId, null, null, "Duplicate LMAX execution report ExecID observed in replay payload.", report, reportsByExecId[report.ExecId], new { execId = report.ExecId }, now, executionType: report.ExecutionType));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(report.ExecId) && internalFillsByExecId.TryGetValue(report.ExecId, out var fill))
            {
                observations.Add(NewObservation(replayRunId, evidenceMode, LmaxShadowSourceEventType.ExecutionReport, LmaxShadowObservationType.ExecutionReportMatchesInternalFill, LmaxShadowObservationSeverity.Info, report.InstrumentId ?? fill.InstrumentId, report.Symbol, report.ExecId, report.BrokerOrderId, report.ClientOrderId, fill.Id, fill.ChildOrderId, "LMAX execution report fill matches an internal fill by BrokerExecutionId.", report, fill, null, now, executionType: report.ExecutionType, internalFillExists: true, internalOrderExists: true));
            }
            else
            {
                observations.Add(NewObservation(replayRunId, evidenceMode, LmaxShadowSourceEventType.ExecutionReport, LmaxShadowObservationType.ExecutionReportMissingInternalFill, LmaxShadowObservationSeverity.Warning, report.InstrumentId, report.Symbol, report.ExecId, report.BrokerOrderId, report.ClientOrderId, null, null, "LMAX execution report fill has no matching internal fill.", report, null, new { missingInternalFillForExecId = report.ExecId }, now, executionType: report.ExecutionType));
            }
        }

        if (executionReports.Any(IsFillLike))
        {
            var lmaxExecIds = executionReports.Select(x => x.ExecId).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var fill in state.Fills.Where(x => !lmaxExecIds.Contains(x.BrokerExecutionId)))
            {
                observations.Add(NewObservation(replayRunId, evidenceMode, LmaxShadowSourceEventType.ExecutionReport, LmaxShadowObservationType.InternalFillMissingInExecutionReports, LmaxShadowObservationSeverity.Warning, fill.InstrumentId, null, fill.BrokerExecutionId, null, null, fill.Id, fill.ChildOrderId, "Internal fill was not present in provided LMAX execution reports.", null, fill, new { fill.BrokerExecutionId }, now, internalFillExists: true, internalOrderExists: true));
            }
        }

        foreach (var report in tradeReports)
        {
            if (!string.IsNullOrWhiteSpace(report.ExecId) && internalFillsByExecId.TryGetValue(report.ExecId, out var fill))
            {
                observations.Add(NewObservation(replayRunId, evidenceMode, LmaxShadowSourceEventType.TradeCaptureReport, LmaxShadowObservationType.TradeCaptureMatchesInternalFill, LmaxShadowObservationSeverity.Info, report.InstrumentId ?? fill.InstrumentId, report.Symbol, report.ExecId, report.BrokerOrderId, report.ClientOrderId, fill.Id, fill.ChildOrderId, "LMAX trade capture report matches an internal fill by BrokerExecutionId.", report, fill, string.IsNullOrWhiteSpace(report.TradeUti) ? new { warning = "FIX trade capture did not include TradeUTI; EOD remains official source for TradeUTI." } : null, now, internalFillExists: true, internalOrderExists: true));
            }
            else
            {
                observations.Add(NewObservation(replayRunId, evidenceMode, LmaxShadowSourceEventType.TradeCaptureReport, LmaxShadowObservationType.TradeCaptureMissingInternalFill, LmaxShadowObservationSeverity.Warning, report.InstrumentId, report.Symbol, report.ExecId, report.BrokerOrderId, report.ClientOrderId, null, null, "LMAX trade capture report has no matching internal fill.", report, null, new { missingInternalFillForExecId = report.ExecId }, now));
            }
        }

        if (tradeReports.Count > 0)
        {
            var captureExecIds = tradeReports.Select(x => x.ExecId).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var fill in state.Fills.Where(x => !captureExecIds.Contains(x.BrokerExecutionId)))
            {
                observations.Add(NewObservation(replayRunId, evidenceMode, LmaxShadowSourceEventType.TradeCaptureReport, LmaxShadowObservationType.InternalFillMissingInTradeCapture, LmaxShadowObservationSeverity.Warning, fill.InstrumentId, null, fill.BrokerExecutionId, null, null, fill.Id, fill.ChildOrderId, "Internal fill was not present in provided LMAX trade capture reports.", null, fill, new { fill.BrokerExecutionId }, now, internalFillExists: true, internalOrderExists: true));
            }
        }

        foreach (var status in orderStatuses)
        {
            observations.Add(CompareOrderStatus(replayRunId, evidenceMode, status, state, now));
        }

        foreach (var reject in protocolRejects)
        {
            observations.Add(NewObservation(replayRunId, evidenceMode, LmaxShadowSourceEventType.ProtocolReject, LmaxShadowObservationType.ProtocolRejectObserved, LmaxShadowObservationSeverity.Blocking, null, null, null, reject.BrokerOrderId, reject.ClientOrderId, null, null, $"LMAX FIX protocol reject observed: {reject.Text ?? "No reject text provided."}", reject, null, new { reject.RefMsgType, reject.RefTagId, reject.ReasonCode }, now, protocolRefMsgType: reject.RefMsgType, protocolText: reject.Text));
        }

        return observations;
    }

    private LmaxShadowObservation CompareOrderStatus(LmaxShadowReplayRunId? replayRunId, string evidenceMode, LmaxShadowOrderStatusInput status, PlatformState state, DateTimeOffset now)
    {
        var child = FindChildOrder(status, state);
        if (child is null)
        {
            return NewObservation(replayRunId, evidenceMode, LmaxShadowSourceEventType.OrderStatus, LmaxShadowObservationType.UnknownLmaxOrder, LmaxShadowObservationSeverity.Warning, status.InstrumentId, status.Symbol, null, status.BrokerOrderId, status.ClientOrderId, null, null, "LMAX order status references an order that is not known internally.", status, null, new { status.OrderStatus }, now);
        }

        var matches = StatusMatches(status.OrderStatus, child.Status);
        return NewObservation(
            replayRunId,
            evidenceMode,
            LmaxShadowSourceEventType.OrderStatus,
            matches ? LmaxShadowObservationType.OrderStatusMatchesInternalOrder : LmaxShadowObservationType.OrderStatusMismatch,
            matches ? LmaxShadowObservationSeverity.Info : LmaxShadowObservationSeverity.Warning,
            status.InstrumentId,
            status.Symbol,
            null,
            status.BrokerOrderId,
            status.ClientOrderId,
            null,
            child.Id,
            matches ? "LMAX order status matches the internal child order status." : "LMAX order status differs from the internal child order status.",
            status,
            child,
            matches ? null : new { lmaxStatus = status.OrderStatus, internalStatus = child.Status.ToString() },
            now,
            internalOrderExists: true);
    }

    private ChildOrder? FindChildOrder(LmaxShadowOrderStatusInput status, PlatformState state)
    {
        if (!string.IsNullOrWhiteSpace(status.ClientOrderId))
        {
            var byClient = state.ChildOrders.FirstOrDefault(x => string.Equals(x.ClientOrderId.Value, status.ClientOrderId, StringComparison.OrdinalIgnoreCase));
            if (byClient is not null) return byClient;
        }

        if (!string.IsNullOrWhiteSpace(status.BrokerOrderId))
        {
            var report = state.ExecutionReports.FirstOrDefault(x => string.Equals(x.BrokerOrderId, status.BrokerOrderId, StringComparison.OrdinalIgnoreCase));
            if (report is not null) return state.ChildOrders.FirstOrDefault(x => x.Id == report.ChildOrderId);
        }

        return null;
    }

    private LmaxShadowObservation NewObservation(
        LmaxShadowReplayRunId? replayRunId,
        string evidenceMode,
        LmaxShadowSourceEventType sourceEventType,
        LmaxShadowObservationType type,
        LmaxShadowObservationSeverity severity,
        InstrumentId? instrumentId,
        string? symbol,
        string? brokerExecutionId,
        string? brokerOrderId,
        string? clientOrderId,
        FillId? internalFillId,
        ChildOrderId? internalOrderId,
        string description,
        object? lmaxPayload,
        object? internalPayload,
        object? difference,
        DateTimeOffset now,
        string? executionType = null,
        bool internalOrderExists = false,
        bool internalFillExists = false,
        string? protocolRefMsgType = null,
        string? protocolText = null)
    {
        var policy = LmaxShadowObservationPolicy.Decide(evidenceMode, sourceEventType, type, severity, description, executionType, internalOrderExists, internalFillExists, protocolRefMsgType, protocolText);
        var policyDifference = new
        {
            policy.PolicyCode,
            evidenceMode = policy.EvidenceMode,
            sourceEventType = policy.SourceEventType.ToString(),
            policy.Rationale,
            policy.SuggestedOperatorAction,
            policy.CreatesExceptionCase,
            policy.CreatesAuditEvent,
            details = difference
        };
        var lmaxJson = OperatorAuditService.SerializeSanitized(lmaxPayload);
        var internalJson = OperatorAuditService.SerializeSanitized(internalPayload);
        var differenceJson = OperatorAuditService.SerializeSanitized(policyDifference);
        var fingerprint = CreateFingerprint(policy.ObservationType, instrumentId, symbol, brokerExecutionId, brokerOrderId, clientOrderId, internalFillId, internalOrderId, differenceJson);
        return new(
            LmaxShadowObservationId.New(),
            replayRunId,
            now,
            policy.ObservationType,
            policy.Severity,
            policy.DefaultStatus,
            instrumentId,
            symbol,
            brokerExecutionId,
            brokerOrderId,
            clientOrderId,
            internalFillId,
            internalOrderId,
            policy.Message,
            lmaxJson,
            internalJson,
            differenceJson,
            fingerprint,
            operatorContext.CorrelationId,
            now);
    }

    private async Task CreateExceptionCaseAsync(LmaxShadowObservation observation, CancellationToken cancellationToken)
    {
        var policy = ExtractPolicyMetadata(observation);
        await exceptionCaseService.CreateManualCaseAsync(new CreateExceptionCaseRequest(
            ExceptionCaseSeverity.Blocking,
            ExceptionCaseType.Other,
            ExceptionCaseSource.Other,
            "Blocking LMAX shadow observation",
            observation.Description,
            "LmaxShadowObservation",
            observation.Id.Value.ToString("D"),
            observation.InstrumentId,
            observation.Symbol,
            null,
            new { observationId = observation.Id.Value.ToString("D"), observation.Type, observation.Severity, observation.Fingerprint, observation.BrokerExecutionId, observation.ClientOrderId, observation.ReplayRunId, policy.PolicyCode, policy.EvidenceMode, policy.SourceEventType }), cancellationToken);
    }

    private Task AuditAsync(OperatorAuditEventType eventType, OperatorAuditResult result, OperatorAuditSeverity severity, string description, string entityId, string? reason, object? metadata, CancellationToken cancellationToken)
        => audit.RecordAsync(new OperatorAuditRecordRequest(eventType, severity, result, "LmaxShadowModeService", description, "LmaxShadow", entityId, reason, Metadata: metadata), cancellationToken);

    private static void RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainRuleViolationException("A reason is required for this LMAX shadow action.");
    }

    private static bool IsFillLike(LmaxShadowExecutionReportInput input) => IsFill(input.ExecutionType, input.LeavesQty);

    private static bool IsFill(string? executionType, decimal? leavesQty)
    {
        if (string.IsNullOrWhiteSpace(executionType)) return false;
        return executionType.Equals("F", StringComparison.OrdinalIgnoreCase)
            || executionType.Equals("Trade", StringComparison.OrdinalIgnoreCase)
            || executionType.Equals("Fill", StringComparison.OrdinalIgnoreCase)
            || executionType.Equals("PartialFill", StringComparison.OrdinalIgnoreCase)
            || executionType.Equals("Partial", StringComparison.OrdinalIgnoreCase) && leavesQty > 0;
    }

    private static bool IsOrderStatus(string? executionType)
        => !string.IsNullOrWhiteSpace(executionType)
            && (executionType.Equals("I", StringComparison.OrdinalIgnoreCase) || executionType.Equals("OrderStatus", StringComparison.OrdinalIgnoreCase));

    private static LmaxShadowOrderStatusInput ToOrderStatus(LmaxShadowExecutionReportInput report)
        => new(report.BrokerOrderId, report.ClientOrderId, report.InstrumentId, report.Symbol, report.OrderStatus, report.CumQty, report.LeavesQty, report.TransactTimeUtc, report.Payload);

    private static bool StatusMatches(string? lmaxStatus, OrderStatus internalStatus)
    {
        if (string.IsNullOrWhiteSpace(lmaxStatus)) return false;
        return lmaxStatus.ToUpperInvariant() switch
        {
            "0" or "NEW" => internalStatus is OrderStatus.PendingNew or OrderStatus.Acked,
            "1" or "PARTIALLYFILLED" or "PARTIALLY_FILLED" => internalStatus == OrderStatus.PartiallyFilled,
            "2" or "FILLED" => internalStatus == OrderStatus.Filled,
            "4" or "CANCELED" or "CANCELLED" => internalStatus == OrderStatus.Cancelled,
            "8" or "REJECTED" => internalStatus == OrderStatus.Rejected,
            "C" or "EXPIRED" => internalStatus == OrderStatus.Expired,
            _ => lmaxStatus.Equals(internalStatus.ToString(), StringComparison.OrdinalIgnoreCase)
        };
    }

    private static OperatorAuditSeverity AuditSeverity(LmaxShadowObservationSeverity severity)
        => severity switch
        {
            LmaxShadowObservationSeverity.Blocking => OperatorAuditSeverity.Critical,
            LmaxShadowObservationSeverity.Warning => OperatorAuditSeverity.Warning,
            _ => OperatorAuditSeverity.Info
        };

    private static IReadOnlyList<LmaxShadowObservation> DeduplicateObservations(IEnumerable<LmaxShadowObservation> observations)
        => observations
            .GroupBy(x => x.Fingerprint, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

    private static int InputEventCount(LmaxShadowReplayRequest request)
        => (request.ExecutionReports?.Count ?? 0)
            + (request.TradeCaptureReports?.Count ?? 0)
            + (request.OrderStatuses?.Count ?? 0)
            + (request.ProtocolRejects?.Count ?? 0);

    private static string InferEvidenceMode(LmaxShadowReplayRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.EvidenceMode)) return request.EvidenceMode;
        var executionCount = request.ExecutionReports?.Count ?? 0;
        var tradeCount = request.TradeCaptureReports?.Count ?? 0;
        var statusCount = request.OrderStatuses?.Count ?? 0;
        var rejectCount = request.ProtocolRejects?.Count ?? 0;
        if (executionCount > 0 && tradeCount > 0 && statusCount > 0) return "SyntheticLifecycle";
        if (rejectCount > 0 && executionCount == 0 && tradeCount == 0 && statusCount == 0) return "ProtocolRejectOnly";
        if (tradeCount > 0 && executionCount == 0 && statusCount == 0 && rejectCount == 0) return "TradeCaptureOnly";
        if (statusCount > 0 && executionCount == 0 && tradeCount == 0 && rejectCount == 0) return "OrderStatusOnly";
        if (executionCount == 0 && tradeCount == 0 && statusCount == 0 && rejectCount == 0) return "EmptyReadOnly";
        return "MixedReadOnly";
    }

    private static HashSet<string> UniqueEventKeys(LmaxShadowReplayRequest request)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var report in request.ExecutionReports ?? [])
        {
            keys.Add($"ER|{report.ExecId}|{report.BrokerOrderId}|{report.ClientOrderId}|{report.ExecutionType}|{report.OrderStatus}");
        }

        foreach (var report in request.TradeCaptureReports ?? [])
        {
            keys.Add($"TC|{report.ExecId}|{report.BrokerOrderId}|{report.ClientOrderId}|{report.LastQty}|{report.LastPx}");
        }

        foreach (var status in request.OrderStatuses ?? [])
        {
            keys.Add($"OS|{status.BrokerOrderId}|{status.ClientOrderId}|{status.OrderStatus}|{status.CumQty}|{status.LeavesQty}");
        }

        foreach (var reject in request.ProtocolRejects ?? [])
        {
            keys.Add($"RJ|{reject.RefMsgType}|{reject.RefTagId}|{reject.ReasonCode}|{reject.Text}|{reject.ClientOrderId}|{reject.BrokerOrderId}");
        }

        return keys;
    }

    private static string CreateFingerprint(LmaxShadowObservationType type, InstrumentId? instrumentId, string? symbol, string? brokerExecutionId, string? brokerOrderId, string? clientOrderId, FillId? internalFillId, ChildOrderId? internalOrderId, string? differenceJson)
    {
        var canonical = string.Join("|", [
            type.ToString(),
            brokerExecutionId ?? "",
            brokerOrderId ?? "",
            clientOrderId ?? "",
            instrumentId?.Value.ToString("D") ?? "",
            symbol?.ToUpperInvariant() ?? "",
            internalFillId?.Value.ToString("D") ?? "",
            internalOrderId?.Value.ToString("D") ?? "",
            differenceJson ?? ""
        ]);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool ObservationCreatesExceptionCase(LmaxShadowObservation observation)
        => ExtractPolicyMetadata(observation).CreatesExceptionCase || observation.Severity == LmaxShadowObservationSeverity.Blocking;

    public static LmaxShadowObservationPolicyMetadata ExtractPolicyMetadata(LmaxShadowObservation observation)
    {
        if (string.IsNullOrWhiteSpace(observation.DifferenceJson))
        {
            return LmaxShadowObservationPolicyMetadata.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(observation.DifferenceJson);
            var root = document.RootElement;
            return new(
                GetString(root, "policyCode"),
                GetString(root, "evidenceMode"),
                GetString(root, "sourceEventType"),
                GetString(root, "rationale"),
                GetString(root, "suggestedOperatorAction"),
                root.TryGetProperty("createsExceptionCase", out var creates) && creates.ValueKind is JsonValueKind.True);
        }
        catch
        {
            return LmaxShadowObservationPolicyMetadata.Empty;
        }
    }

    private static string? GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

public sealed record LmaxShadowObservationPolicyMetadata(
    string? PolicyCode,
    string? EvidenceMode,
    string? SourceEventType,
    string? Rationale,
    string? SuggestedOperatorAction,
    bool CreatesExceptionCase)
{
    public static LmaxShadowObservationPolicyMetadata Empty { get; } = new(null, null, null, null, null, false);
}
