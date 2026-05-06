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
    string? ClientOrderId = null);

public sealed record LmaxShadowReplayRequest(
    LmaxShadowInputSource InputSource,
    IReadOnlyList<LmaxShadowExecutionReportInput>? ExecutionReports,
    IReadOnlyList<LmaxShadowTradeCaptureInput>? TradeCaptureReports,
    IReadOnlyList<LmaxShadowOrderStatusInput>? OrderStatuses,
    IReadOnlyList<LmaxShadowProtocolRejectInput>? ProtocolRejects,
    string Reason);

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
        if (!string.IsNullOrWhiteSpace(filter.ClientOrderId)) query = query.Where(x => string.Equals(x.ClientOrderId, filter.ClientOrderId, StringComparison.OrdinalIgnoreCase));
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
            var observations = BuildObservations(run.Id, request, state, now);
            foreach (var observation in observations)
            {
                await repository.AddObservationAsync(observation, cancellationToken);
                await AuditAsync(OperatorAuditEventType.LmaxShadowObservationCreated, OperatorAuditResult.Succeeded, AuditSeverity(observation.Severity), observation.Description, observation.Id.Value.ToString("D"), null, observation, cancellationToken);
                if (observation.Severity == LmaxShadowObservationSeverity.Blocking)
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
                ObservationCount = observations.Count,
                BlockingObservationCount = blockingCount,
                WarningObservationCount = warningCount,
                Message = status == LmaxShadowReplayStatus.Completed ? "Replay completed with matching shadow observations." : "Replay completed with shadow warnings.",
                OutputJson = OperatorAuditService.SerializeSanitized(new { observationCount = observations.Count, warningCount, blockingCount })
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
            await AuditAsync(OperatorAuditEventType.LmaxShadowReplayCompleted, OperatorAuditResult.Failed, OperatorAuditSeverity.Critical, "LMAX shadow replay failed.", run.Id.Value.ToString("D"), ex.Message, run, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<LmaxShadowObservation>> CompareExecutionReportsAsync(IReadOnlyList<LmaxShadowExecutionReportInput> reports, CancellationToken cancellationToken)
    {
        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        return BuildObservations(null, new LmaxShadowReplayRequest(LmaxShadowInputSource.ManualJson, reports, [], [], [], "Comparison only"), state, clock.UtcNow);
    }

    public async Task<IReadOnlyList<LmaxShadowObservation>> CompareTradeCaptureReportsAsync(IReadOnlyList<LmaxShadowTradeCaptureInput> reports, CancellationToken cancellationToken)
    {
        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        return BuildObservations(null, new LmaxShadowReplayRequest(LmaxShadowInputSource.ManualJson, [], reports, [], [], "Comparison only"), state, clock.UtcNow);
    }

    public async Task<IReadOnlyList<LmaxShadowObservation>> CompareOrderStatusesAsync(IReadOnlyList<LmaxShadowOrderStatusInput> statuses, CancellationToken cancellationToken)
    {
        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        return BuildObservations(null, new LmaxShadowReplayRequest(LmaxShadowInputSource.ManualJson, [], [], statuses, [], "Comparison only"), state, clock.UtcNow);
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
        var updated = current with { Status = nextStatus };
        await repository.UpdateObservationAsync(updated, cancellationToken);
        await AuditAsync(eventType, OperatorAuditResult.Succeeded, OperatorAuditSeverity.Info, $"LMAX shadow observation marked {nextStatus}.", updated.Id.Value.ToString("D"), reason, updated, cancellationToken);
        return updated;
    }

    private IReadOnlyList<LmaxShadowObservation> BuildObservations(LmaxShadowReplayRunId? replayRunId, LmaxShadowReplayRequest request, PlatformState state, DateTimeOffset now)
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
                observations.Add(CompareOrderStatus(replayRunId, ToOrderStatus(report), state, now));
                continue;
            }

            if (!IsFill(report.ExecutionType, report.LeavesQty))
            {
                observations.Add(NewObservation(replayRunId, LmaxShadowObservationType.UnknownLmaxExecution, LmaxShadowObservationSeverity.Info, report.InstrumentId, report.Symbol, report.ExecId, report.BrokerOrderId, report.ClientOrderId, null, null, "LMAX execution report observed but did not represent a fill candidate.", report, null, null, now));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(report.ExecId) && !reportsByExecId.TryAdd(report.ExecId, report))
            {
                observations.Add(NewObservation(replayRunId, LmaxShadowObservationType.DuplicateExecutionObserved, LmaxShadowObservationSeverity.Warning, report.InstrumentId, report.Symbol, report.ExecId, report.BrokerOrderId, report.ClientOrderId, null, null, "Duplicate LMAX execution report ExecID observed in replay payload.", report, reportsByExecId[report.ExecId], new { execId = report.ExecId }, now));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(report.ExecId) && internalFillsByExecId.TryGetValue(report.ExecId, out var fill))
            {
                observations.Add(NewObservation(replayRunId, LmaxShadowObservationType.ExecutionReportMatchesInternalFill, LmaxShadowObservationSeverity.Info, report.InstrumentId ?? fill.InstrumentId, report.Symbol, report.ExecId, report.BrokerOrderId, report.ClientOrderId, fill.Id, fill.ChildOrderId, "LMAX execution report fill matches an internal fill by BrokerExecutionId.", report, fill, null, now));
            }
            else
            {
                observations.Add(NewObservation(replayRunId, LmaxShadowObservationType.ExecutionReportMissingInternalFill, LmaxShadowObservationSeverity.Warning, report.InstrumentId, report.Symbol, report.ExecId, report.BrokerOrderId, report.ClientOrderId, null, null, "LMAX execution report fill has no matching internal fill.", report, null, new { missingInternalFillForExecId = report.ExecId }, now));
            }
        }

        if (executionReports.Any(IsFillLike))
        {
            var lmaxExecIds = executionReports.Select(x => x.ExecId).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var fill in state.Fills.Where(x => !lmaxExecIds.Contains(x.BrokerExecutionId)))
            {
                observations.Add(NewObservation(replayRunId, LmaxShadowObservationType.InternalFillMissingInExecutionReports, LmaxShadowObservationSeverity.Warning, fill.InstrumentId, null, fill.BrokerExecutionId, null, null, fill.Id, fill.ChildOrderId, "Internal fill was not present in provided LMAX execution reports.", null, fill, new { fill.BrokerExecutionId }, now));
            }
        }

        foreach (var report in tradeReports)
        {
            if (!string.IsNullOrWhiteSpace(report.ExecId) && internalFillsByExecId.TryGetValue(report.ExecId, out var fill))
            {
                observations.Add(NewObservation(replayRunId, LmaxShadowObservationType.TradeCaptureMatchesInternalFill, LmaxShadowObservationSeverity.Info, report.InstrumentId ?? fill.InstrumentId, report.Symbol, report.ExecId, report.BrokerOrderId, report.ClientOrderId, fill.Id, fill.ChildOrderId, "LMAX trade capture report matches an internal fill by BrokerExecutionId.", report, fill, string.IsNullOrWhiteSpace(report.TradeUti) ? new { warning = "FIX trade capture did not include TradeUTI; EOD remains official source for TradeUTI." } : null, now));
            }
            else
            {
                observations.Add(NewObservation(replayRunId, LmaxShadowObservationType.TradeCaptureMissingInternalFill, LmaxShadowObservationSeverity.Warning, report.InstrumentId, report.Symbol, report.ExecId, report.BrokerOrderId, report.ClientOrderId, null, null, "LMAX trade capture report has no matching internal fill.", report, null, new { missingInternalFillForExecId = report.ExecId }, now));
            }
        }

        if (tradeReports.Count > 0)
        {
            var captureExecIds = tradeReports.Select(x => x.ExecId).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var fill in state.Fills.Where(x => !captureExecIds.Contains(x.BrokerExecutionId)))
            {
                observations.Add(NewObservation(replayRunId, LmaxShadowObservationType.InternalFillMissingInTradeCapture, LmaxShadowObservationSeverity.Warning, fill.InstrumentId, null, fill.BrokerExecutionId, null, null, fill.Id, fill.ChildOrderId, "Internal fill was not present in provided LMAX trade capture reports.", null, fill, new { fill.BrokerExecutionId }, now));
            }
        }

        foreach (var status in orderStatuses)
        {
            observations.Add(CompareOrderStatus(replayRunId, status, state, now));
        }

        foreach (var reject in protocolRejects)
        {
            observations.Add(NewObservation(replayRunId, LmaxShadowObservationType.ProtocolRejectObserved, LmaxShadowObservationSeverity.Blocking, null, null, null, reject.BrokerOrderId, reject.ClientOrderId, null, null, $"LMAX FIX protocol reject observed: {reject.Text ?? "No reject text provided."}", reject, null, new { reject.RefMsgType, reject.RefTagId, reject.ReasonCode }, now));
        }

        return observations;
    }

    private LmaxShadowObservation CompareOrderStatus(LmaxShadowReplayRunId? replayRunId, LmaxShadowOrderStatusInput status, PlatformState state, DateTimeOffset now)
    {
        var child = FindChildOrder(status, state);
        if (child is null)
        {
            return NewObservation(replayRunId, LmaxShadowObservationType.UnknownLmaxOrder, LmaxShadowObservationSeverity.Warning, status.InstrumentId, status.Symbol, null, status.BrokerOrderId, status.ClientOrderId, null, null, "LMAX order status references an order that is not known internally.", status, null, new { status.OrderStatus }, now);
        }

        var matches = StatusMatches(status.OrderStatus, child.Status);
        return NewObservation(
            replayRunId,
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
            now);
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

    private LmaxShadowObservation NewObservation(LmaxShadowReplayRunId? replayRunId, LmaxShadowObservationType type, LmaxShadowObservationSeverity severity, InstrumentId? instrumentId, string? symbol, string? brokerExecutionId, string? brokerOrderId, string? clientOrderId, FillId? internalFillId, ChildOrderId? internalOrderId, string description, object? lmaxPayload, object? internalPayload, object? difference, DateTimeOffset now)
        => new(
            LmaxShadowObservationId.New(),
            replayRunId,
            now,
            type,
            severity,
            LmaxShadowObservationStatus.Open,
            instrumentId,
            symbol,
            brokerExecutionId,
            brokerOrderId,
            clientOrderId,
            internalFillId,
            internalOrderId,
            description,
            OperatorAuditService.SerializeSanitized(lmaxPayload),
            OperatorAuditService.SerializeSanitized(internalPayload),
            OperatorAuditService.SerializeSanitized(difference),
            operatorContext.CorrelationId,
            now);

    private async Task CreateExceptionCaseAsync(LmaxShadowObservation observation, CancellationToken cancellationToken)
    {
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
            new { observation.Type, observation.BrokerExecutionId, observation.ClientOrderId, observation.ReplayRunId }), cancellationToken);
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
}
