using System.Text.Json;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public sealed record OperationalRunbookRunFilter(int Limit, OperationalRunbookType? RunbookType = null, OperationalRunbookStatus? Status = null, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null);
public sealed record RunOperationalRunbookRequest(OperationalRunbookType RunbookType, string Reason, object? Input = null, OperationalRunbookTriggerType TriggerType = OperationalRunbookTriggerType.Manual, OperationalRunbookRunId? RetryOfRunbookRunId = null, int? RetryCount = null);
public sealed record LocalSchedulerOptions(bool Enabled = false, int PollIntervalSeconds = 30);

public interface IOperationalRunbookRepository
{
    Task AddDefinitionAsync(OperationalRunbookDefinition definition, IReadOnlyList<OperationalRunbookStepDefinition> steps, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperationalRunbookDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken);
    Task<OperationalRunbookDefinition?> GetDefinitionAsync(OperationalRunbookDefinitionId id, CancellationToken cancellationToken);
    Task<OperationalRunbookDefinition?> GetDefinitionByTypeAsync(OperationalRunbookType runbookType, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperationalRunbookStepDefinition>> GetStepDefinitionsAsync(OperationalRunbookDefinitionId definitionId, CancellationToken cancellationToken);
    Task AddRunAsync(OperationalRunbookRun run, IReadOnlyList<OperationalRunbookStepRun> steps, CancellationToken cancellationToken);
    Task UpdateRunAsync(OperationalRunbookRun run, CancellationToken cancellationToken);
    Task<OperationalRunbookRun?> GetRunAsync(OperationalRunbookRunId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperationalRunbookRun>> GetRunsAsync(OperationalRunbookRunFilter filter, CancellationToken cancellationToken);
    Task UpdateStepRunAsync(OperationalRunbookStepRun step, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperationalRunbookStepRun>> GetStepRunsAsync(OperationalRunbookRunId runId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperationalScheduleDefinition>> GetSchedulesAsync(CancellationToken cancellationToken);
    Task UpsertScheduleAsync(OperationalScheduleDefinition schedule, CancellationToken cancellationToken);
}

public interface IOperationalRunbookRunner
{
    Task<IReadOnlyList<OperationalRunbookDefinition>> GetRunbookDefinitionsAsync(CancellationToken cancellationToken);
    Task<OperationalRunbookDefinition?> GetRunbookDefinitionAsync(OperationalRunbookDefinitionId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperationalRunbookStepDefinition>> GetRunbookStepDefinitionsAsync(OperationalRunbookDefinitionId id, CancellationToken cancellationToken);
    Task<OperationalRunbookRun> RunRunbookAsync(RunOperationalRunbookRequest request, CancellationToken cancellationToken);
    Task<OperationalRunbookRun> RunNextStepAsync(OperationalRunbookRunId id, string reason, CancellationToken cancellationToken);
    Task<OperationalRunbookRun> CompleteManualStepAsync(OperationalRunbookRunId id, OperationalRunbookStepRunId stepRunId, string reason, CancellationToken cancellationToken);
    Task<OperationalRunbookRun> CancelRunbookAsync(OperationalRunbookRunId id, string reason, CancellationToken cancellationToken);
    Task<OperationalRunbookRun> RetryRunbookAsync(OperationalRunbookRunId id, string reason, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperationalRunbookRun>> GetRunbookRunsAsync(OperationalRunbookRunFilter filter, CancellationToken cancellationToken);
    Task<OperationalRunbookRun?> GetRunbookRunAsync(OperationalRunbookRunId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperationalRunbookStepRun>> GetRunbookStepRunsAsync(OperationalRunbookRunId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperationalScheduleDefinition>> GetSchedulesAsync(CancellationToken cancellationToken);
    Task<OperationalScheduleDefinition> UpsertScheduleAsync(OperationalScheduleDefinition schedule, string reason, CancellationToken cancellationToken);
}

public sealed class OperationalRunbookRunner(
    IOperationalRunbookRepository repository,
    IOperationalJobRunner jobs,
    IOperatorAuditService audit,
    IOperatorContext context,
    IOperatorPermissionService permissions,
    IClock clock,
    IIntradayRepository intradayRepository,
    IExceptionCaseService exceptions) : IOperationalRunbookRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<OperationalRunbookDefinition>> GetRunbookDefinitionsAsync(CancellationToken cancellationToken)
    {
        var existing = await repository.GetDefinitionsAsync(cancellationToken);
        if (existing.Count > 0) return existing.OrderBy(x => x.RunbookType).ToList();
        foreach (var (definition, steps) in DefaultDefinitions(clock.UtcNow))
        {
            await repository.AddDefinitionAsync(definition, steps, cancellationToken);
        }
        return (await repository.GetDefinitionsAsync(cancellationToken)).OrderBy(x => x.RunbookType).ToList();
    }

    public Task<OperationalRunbookDefinition?> GetRunbookDefinitionAsync(OperationalRunbookDefinitionId id, CancellationToken cancellationToken)
        => repository.GetDefinitionAsync(id, cancellationToken);

    public Task<IReadOnlyList<OperationalRunbookStepDefinition>> GetRunbookStepDefinitionsAsync(OperationalRunbookDefinitionId id, CancellationToken cancellationToken)
        => repository.GetStepDefinitionsAsync(id, cancellationToken);

    public async Task<OperationalRunbookRun> RunRunbookAsync(RunOperationalRunbookRequest request, CancellationToken cancellationToken)
    {
        RequireReason(request.Reason, "A reason is required to run an operational runbook.");
        await permissions.RequirePermissionAsync(OperatorPermission.RunRunbooks, cancellationToken);
        var definitions = await GetRunbookDefinitionsAsync(cancellationToken);
        var definition = definitions.FirstOrDefault(x => x.RunbookType == request.RunbookType) ?? throw new DomainRuleViolationException($"Runbook '{request.RunbookType}' was not found.");
        if (!definition.IsEnabled) throw new DomainRuleViolationException($"Runbook '{request.RunbookType}' is disabled.");
        var stepDefinitions = await repository.GetStepDefinitionsAsync(definition.Id, cancellationToken);
        var now = clock.UtcNow;
        var actor = context.Current;
        var run = new OperationalRunbookRun(
            OperationalRunbookRunId.New(),
            definition.Id,
            definition.RunbookType,
            definition.Name,
            OperationalRunbookStatus.Running,
            request.TriggerType,
            actor.ActorId,
            actor.ActorDisplayName,
            now,
            null,
            null,
            context.CorrelationId,
            request.Reason,
            OperatorAuditService.SerializeSanitized(request.Input),
            null,
            null,
            request.RetryOfRunbookRunId,
            request.RetryCount ?? (request.RetryOfRunbookRunId is null ? 0 : 1),
            definition.IsRerunnable,
            now,
            now);
        var stepRuns = stepDefinitions.OrderBy(x => x.StepOrder)
            .Select(x => new OperationalRunbookStepRun(OperationalRunbookStepRunId.New(), run.Id, x.Id, x.StepOrder, x.Name, OperationalRunbookStepStatus.Pending, null, null, null, null, null, x.InputTemplateJson, null, null, now, now))
            .ToList();
        await repository.AddRunAsync(run, stepRuns, cancellationToken);
        await audit.RecordAsync(new(OperatorAuditEventType.RunbookStarted, OperatorAuditSeverity.Info, OperatorAuditResult.Started, "OperationalRunbookRunner", $"Runbook {run.RunbookType} started.", "OperationalRunbookRun", run.Id.Value.ToString("D"), request.Reason, Metadata: new { run.RunbookType, request.Input }), cancellationToken);
        return await ContinueAsync(run, stepDefinitions, request.Reason, cancellationToken);
    }

    public async Task<OperationalRunbookRun> RunNextStepAsync(OperationalRunbookRunId id, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason, "A reason is required to continue a runbook.");
        await permissions.RequirePermissionAsync(OperatorPermission.RunRunbooks, cancellationToken);
        var run = await RequireRunAsync(id, cancellationToken);
        if (run.Status is OperationalRunbookStatus.Succeeded or OperationalRunbookStatus.Failed or OperationalRunbookStatus.Cancelled) throw new DomainRuleViolationException("Completed runbooks cannot continue.");
        var steps = await repository.GetStepDefinitionsAsync(run.RunbookDefinitionId, cancellationToken);
        return await ContinueAsync(run with { Status = OperationalRunbookStatus.Running, UpdatedAtUtc = clock.UtcNow }, steps, reason, cancellationToken);
    }

    public async Task<OperationalRunbookRun> CompleteManualStepAsync(OperationalRunbookRunId id, OperationalRunbookStepRunId stepRunId, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason, "A reason is required to complete a manual runbook step.");
        await permissions.RequirePermissionAsync(OperatorPermission.CompleteRunbookManualGates, cancellationToken);
        var run = await RequireRunAsync(id, cancellationToken);
        var steps = await repository.GetStepRunsAsync(id, cancellationToken);
        var step = steps.FirstOrDefault(x => x.Id == stepRunId) ?? throw new DomainRuleViolationException("Runbook step was not found.");
        if (step.Status != OperationalRunbookStepStatus.WaitingForOperator) throw new DomainRuleViolationException("Only waiting manual steps can be completed.");
        var completedAt = clock.UtcNow;
        var completed = step with { Status = OperationalRunbookStepStatus.Succeeded, CompletedAtUtc = completedAt, DurationMs = Duration(step.StartedAtUtc ?? step.CreatedAtUtc, completedAt), Message = reason, OutputJson = OperatorAuditService.SerializeSanitized(new { confirmed = true, reason }), UpdatedAtUtc = completedAt };
        await repository.UpdateStepRunAsync(completed, cancellationToken);
        await audit.RecordAsync(new(OperatorAuditEventType.RunbookManualStepCompleted, OperatorAuditSeverity.Info, OperatorAuditResult.Succeeded, "OperationalRunbookRunner", "Runbook manual step completed.", "OperationalRunbookStepRun", completed.Id.Value.ToString("D"), reason, Metadata: new { runbookRunId = id.Value.ToString("D"), step.Name }), cancellationToken);
        return await ContinueAsync(run with { Status = OperationalRunbookStatus.Running, UpdatedAtUtc = completedAt }, await repository.GetStepDefinitionsAsync(run.RunbookDefinitionId, cancellationToken), reason, cancellationToken);
    }

    public async Task<OperationalRunbookRun> CancelRunbookAsync(OperationalRunbookRunId id, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason, "A reason is required to cancel a runbook.");
        await permissions.RequirePermissionAsync(OperatorPermission.CancelRunbooks, cancellationToken);
        var run = await RequireRunAsync(id, cancellationToken);
        var cancelledAt = clock.UtcNow;
        var cancelled = run with { Status = OperationalRunbookStatus.Cancelled, CompletedAtUtc = cancelledAt, DurationMs = Duration(run.StartedAtUtc, cancelledAt), ErrorMessage = reason, UpdatedAtUtc = cancelledAt };
        await repository.UpdateRunAsync(cancelled, cancellationToken);
        await audit.RecordAsync(new(OperatorAuditEventType.RunbookCancelled, OperatorAuditSeverity.Warning, OperatorAuditResult.Blocked, "OperationalRunbookRunner", $"Runbook {run.RunbookType} cancelled.", "OperationalRunbookRun", run.Id.Value.ToString("D"), reason), cancellationToken);
        return cancelled;
    }

    public async Task<OperationalRunbookRun> RetryRunbookAsync(OperationalRunbookRunId id, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason, "A reason is required to retry a runbook.");
        await permissions.RequirePermissionAsync(OperatorPermission.RetryRunbooks, cancellationToken);
        var prior = await RequireRunAsync(id, cancellationToken);
        if (!prior.CanRetry) throw new DomainRuleViolationException("This runbook is not retryable.");
        var retry = await RunRunbookAsync(new RunOperationalRunbookRequest(prior.RunbookType, reason, Deserialize(prior.InputJson), OperationalRunbookTriggerType.Manual, prior.Id, prior.RetryCount + 1), cancellationToken);
        await audit.RecordAsync(new(OperatorAuditEventType.RunbookRetried, OperatorAuditSeverity.Info, OperatorAuditResult.Succeeded, "OperationalRunbookRunner", "Runbook retry requested.", "OperationalRunbookRun", prior.Id.Value.ToString("D"), reason, Metadata: new { originalRunbookRunId = prior.Id.Value.ToString("D"), retryRunbookRunId = retry.Id.Value.ToString("D"), prior.RunbookType, retry.RetryCount }), cancellationToken);
        return retry;
    }

    public Task<IReadOnlyList<OperationalRunbookRun>> GetRunbookRunsAsync(OperationalRunbookRunFilter filter, CancellationToken cancellationToken) => repository.GetRunsAsync(filter, cancellationToken);
    public Task<OperationalRunbookRun?> GetRunbookRunAsync(OperationalRunbookRunId id, CancellationToken cancellationToken) => repository.GetRunAsync(id, cancellationToken);
    public Task<IReadOnlyList<OperationalRunbookStepRun>> GetRunbookStepRunsAsync(OperationalRunbookRunId id, CancellationToken cancellationToken) => repository.GetStepRunsAsync(id, cancellationToken);
    public Task<IReadOnlyList<OperationalScheduleDefinition>> GetSchedulesAsync(CancellationToken cancellationToken) => repository.GetSchedulesAsync(cancellationToken);

    public async Task<OperationalScheduleDefinition> UpsertScheduleAsync(OperationalScheduleDefinition schedule, string reason, CancellationToken cancellationToken)
    {
        RequireReason(reason, "A reason is required to update a local schedule.");
        await permissions.RequirePermissionAsync(OperatorPermission.ManageRunbookSchedules, cancellationToken);
        await repository.UpsertScheduleAsync(schedule, cancellationToken);
        await audit.RecordAsync(new(OperatorAuditEventType.ScheduleUpdated, OperatorAuditSeverity.Info, OperatorAuditResult.Succeeded, "OperationalRunbookRunner", "Local runbook schedule updated.", "OperationalScheduleDefinition", schedule.Id.Value.ToString("D"), reason, Metadata: new { schedule.Name, schedule.IsEnabled }), cancellationToken);
        return schedule;
    }

    private async Task<OperationalRunbookRun> ContinueAsync(OperationalRunbookRun run, IReadOnlyList<OperationalRunbookStepDefinition> stepDefinitions, string reason, CancellationToken cancellationToken)
    {
        var stepRuns = await repository.GetStepRunsAsync(run.Id, cancellationToken);
        foreach (var stepRun in stepRuns.Where(x => x.Status == OperationalRunbookStepStatus.Pending).OrderBy(x => x.StepOrder))
        {
            var definition = stepDefinitions.FirstOrDefault(x => x.Id == stepRun.StepDefinitionId);
            if (definition is null) continue;
            if (definition.GateType == OperationalRunbookGateType.ManualConfirmation)
            {
                var waiting = stepRun with { Status = OperationalRunbookStepStatus.WaitingForOperator, StartedAtUtc = clock.UtcNow, Message = "Waiting for local operator confirmation.", UpdatedAtUtc = clock.UtcNow };
                await repository.UpdateStepRunAsync(waiting, cancellationToken);
                var paused = run with { Status = OperationalRunbookStatus.WaitingForOperator, UpdatedAtUtc = clock.UtcNow, OutputJson = await BuildOutputJsonAsync(run.Id, null, cancellationToken) };
                await repository.UpdateRunAsync(paused, cancellationToken);
                await audit.RecordAsync(new(OperatorAuditEventType.RunbookWaitingForOperator, OperatorAuditSeverity.Info, OperatorAuditResult.Blocked, "OperationalRunbookRunner", $"Runbook {run.RunbookType} is waiting for operator confirmation.", "OperationalRunbookRun", run.Id.Value.ToString("D"), reason, Metadata: new { stepRunId = waiting.Id.Value.ToString("D"), waiting.Name }), cancellationToken);
                return paused;
            }

            var startedAt = clock.UtcNow;
            var running = stepRun with { Status = OperationalRunbookStepStatus.Running, StartedAtUtc = startedAt, UpdatedAtUtc = startedAt };
            await repository.UpdateStepRunAsync(running, cancellationToken);
            await audit.RecordAsync(new(OperatorAuditEventType.RunbookStepStarted, OperatorAuditSeverity.Info, OperatorAuditResult.Started, "OperationalRunbookRunner", $"Runbook step '{running.Name}' started.", "OperationalRunbookStepRun", running.Id.Value.ToString("D"), reason, Metadata: new { runbookRunId = run.Id.Value.ToString("D"), definition.JobType }), cancellationToken);

            var completed = await ExecuteStepAsync(run, definition, running, reason, cancellationToken);
            if (completed.Status is OperationalRunbookStepStatus.Failed or OperationalRunbookStepStatus.Blocked)
            {
                if (definition.IsRequired && !definition.ContinueOnFailure && definition.GateType != OperationalRunbookGateType.ContinueOnWarning)
                {
                    var failed = Complete(run, OperationalRunbookStatus.Failed, await BuildOutputJsonAsync(run.Id, completed, cancellationToken), completed.ErrorMessage ?? completed.Message);
                    await repository.UpdateRunAsync(failed, cancellationToken);
                    await audit.RecordAsync(new(OperatorAuditEventType.RunbookFailed, OperatorAuditSeverity.Critical, OperatorAuditResult.Failed, "OperationalRunbookRunner", $"Runbook {run.RunbookType} failed.", "OperationalRunbookRun", run.Id.Value.ToString("D"), failed.ErrorMessage, Metadata: new { failedStepRunId = completed.Id.Value.ToString("D"), completed.JobRunId }), cancellationToken);
                    await MaybeCreateExceptionAsync(failed, completed, cancellationToken);
                    return failed;
                }
            }
        }

        var allSteps = await repository.GetStepRunsAsync(run.Id, cancellationToken);
        var finalStatus = allSteps.Any(x => x.Status == OperationalRunbookStepStatus.Failed) ? OperationalRunbookStatus.PartiallySucceeded : OperationalRunbookStatus.Succeeded;
        var finished = Complete(run, finalStatus, await BuildOutputJsonAsync(run.Id, null, cancellationToken), null);
        await repository.UpdateRunAsync(finished, cancellationToken);
        await audit.RecordAsync(new(OperatorAuditEventType.RunbookCompleted, OperatorAuditSeverity.Info, finalStatus == OperationalRunbookStatus.Succeeded ? OperatorAuditResult.Succeeded : OperatorAuditResult.Blocked, "OperationalRunbookRunner", $"Runbook {run.RunbookType} completed with status {finalStatus}.", "OperationalRunbookRun", run.Id.Value.ToString("D"), reason, Metadata: new { finalStatus, stepCount = allSteps.Count }), cancellationToken);
        return finished;
    }

    private async Task<OperationalRunbookStepRun> ExecuteStepAsync(OperationalRunbookRun run, OperationalRunbookStepDefinition definition, OperationalRunbookStepRun stepRun, string reason, CancellationToken cancellationToken)
    {
        try
        {
            object output;
            OperationalJobRunId? jobRunId = null;
            if (definition.JobType is not null)
            {
                var input = Deserialize(definition.InputTemplateJson);
                var job = await jobs.RunJobAsync(new RunOperationalJobRequest(definition.JobType.Value, reason, input, OperationalJobTriggerType.Api), cancellationToken);
                jobRunId = job.Id;
                output = new { jobRunId = job.Id.Value.ToString("D"), job.JobType, job.Status, job.OutputJson, job.ErrorMessage };
                var stepStatus = JobToStepStatus(job.Status);
                var completed = stepRun with { Status = stepStatus, JobRunId = jobRunId, CompletedAtUtc = clock.UtcNow, DurationMs = Duration(stepRun.StartedAtUtc ?? stepRun.CreatedAtUtc, clock.UtcNow), Message = $"Linked job {job.JobType} completed with {job.Status}.", OutputJson = OperatorAuditService.SerializeSanitized(output), ErrorMessage = job.ErrorMessage, UpdatedAtUtc = clock.UtcNow };
                await repository.UpdateStepRunAsync(completed, cancellationToken);
                await audit.RecordAsync(new(stepStatus == OperationalRunbookStepStatus.Failed ? OperatorAuditEventType.RunbookStepFailed : OperatorAuditEventType.RunbookStepSucceeded, stepStatus == OperationalRunbookStepStatus.Failed ? OperatorAuditSeverity.Warning : OperatorAuditSeverity.Info, stepStatus == OperationalRunbookStepStatus.Failed ? OperatorAuditResult.Failed : OperatorAuditResult.Succeeded, "OperationalRunbookRunner", $"Runbook step '{stepRun.Name}' completed with {stepStatus}.", "OperationalRunbookStepRun", stepRun.Id.Value.ToString("D"), reason, Metadata: new { runbookRunId = run.Id.Value.ToString("D"), jobRunId = job.Id.Value.ToString("D"), job.Status }), cancellationToken);
                return completed;
            }

            output = await ExecuteChecklistStepAsync(definition, cancellationToken);
            var status = ChecklistToStepStatus(output);
            var done = stepRun with { Status = status, CompletedAtUtc = clock.UtcNow, DurationMs = Duration(stepRun.StartedAtUtc ?? stepRun.CreatedAtUtc, clock.UtcNow), Message = "Checklist step evaluated.", OutputJson = OperatorAuditService.SerializeSanitized(output), UpdatedAtUtc = clock.UtcNow };
            await repository.UpdateStepRunAsync(done, cancellationToken);
            await audit.RecordAsync(new(status == OperationalRunbookStepStatus.Failed ? OperatorAuditEventType.RunbookStepFailed : OperatorAuditEventType.RunbookStepSucceeded, status == OperationalRunbookStepStatus.Failed ? OperatorAuditSeverity.Warning : OperatorAuditSeverity.Info, status == OperationalRunbookStepStatus.Failed ? OperatorAuditResult.Failed : OperatorAuditResult.Succeeded, "OperationalRunbookRunner", $"Runbook checklist step '{stepRun.Name}' evaluated.", "OperationalRunbookStepRun", stepRun.Id.Value.ToString("D"), reason, Metadata: output), cancellationToken);
            return done;
        }
        catch (Exception ex)
        {
            var failed = stepRun with { Status = OperationalRunbookStepStatus.Failed, CompletedAtUtc = clock.UtcNow, DurationMs = Duration(stepRun.StartedAtUtc ?? stepRun.CreatedAtUtc, clock.UtcNow), ErrorMessage = ex.Message, UpdatedAtUtc = clock.UtcNow };
            await repository.UpdateStepRunAsync(failed, cancellationToken);
            await audit.RecordAsync(new(OperatorAuditEventType.RunbookStepFailed, OperatorAuditSeverity.Warning, OperatorAuditResult.Failed, "OperationalRunbookRunner", $"Runbook step '{stepRun.Name}' failed.", "OperationalRunbookStepRun", stepRun.Id.Value.ToString("D"), ex.Message, Metadata: new { runbookRunId = run.Id.Value.ToString("D") }), cancellationToken);
            return failed;
        }
    }

    private async Task<object> ExecuteChecklistStepAsync(OperationalRunbookStepDefinition definition, CancellationToken cancellationToken)
    {
        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        if (definition.Name.Contains("Active Risk", StringComparison.OrdinalIgnoreCase))
        {
            var hasActive = state.RiskLimitSets.Any(x => x.IsActive && x.Status == RiskLimitSetStatus.Active);
            return new { status = hasActive ? "Succeeded" : "Failed", activeRiskSetCount = state.RiskLimitSets.Count(x => x.IsActive), message = hasActive ? "Active risk profile is present." : "No active risk profile was found." };
        }

        var openBlocking = (await exceptions.GetCasesAsync(new ExceptionCaseFilter(500), cancellationToken))
            .Count(x =>
                (x.Status is ExceptionCaseStatus.Open or ExceptionCaseStatus.Acknowledged or ExceptionCaseStatus.Investigating)
                && (x.Severity is ExceptionCaseSeverity.Blocking or ExceptionCaseSeverity.Critical));
        return new { status = openBlocking > 0 ? "Failed" : "Succeeded", openBlockingExceptionCount = openBlocking, message = openBlocking > 0 ? "Open blocking or critical exceptions exist." : "No open blocking exceptions." };
    }

    private async Task MaybeCreateExceptionAsync(OperationalRunbookRun run, OperationalRunbookStepRun step, CancellationToken cancellationToken)
        => await exceptions.CreateManualCaseAsync(new CreateExceptionCaseRequest(ExceptionCaseSeverity.Critical, ExceptionCaseType.SystemHealth, ExceptionCaseSource.SystemHealth, $"Runbook failed: {run.Name}", step.ErrorMessage ?? step.Message ?? "Runbook failed.", "OperationalRunbookRun", run.Id.Value.ToString("D"), Metadata: new { run.RunbookType, failedStepRunId = step.Id.Value.ToString("D"), step.JobRunId }), cancellationToken);

    private async Task<OperationalRunbookRun> RequireRunAsync(OperationalRunbookRunId id, CancellationToken cancellationToken)
        => await repository.GetRunAsync(id, cancellationToken) ?? throw new DomainRuleViolationException("Runbook run not found.");

    private OperationalRunbookRun Complete(OperationalRunbookRun run, OperationalRunbookStatus status, string? outputJson, string? error)
    {
        var completedAt = clock.UtcNow;
        return run with { Status = status, CompletedAtUtc = completedAt, DurationMs = Duration(run.StartedAtUtc, completedAt), OutputJson = outputJson, ErrorMessage = error, UpdatedAtUtc = completedAt };
    }

    private async Task<string> BuildOutputJsonAsync(OperationalRunbookRunId runId, OperationalRunbookStepRun? lastStep, CancellationToken cancellationToken)
    {
        var steps = await repository.GetStepRunsAsync(runId, cancellationToken);
        return OperatorAuditService.SerializeSanitized(new { stepCount = steps.Count, succeeded = steps.Count(x => x.Status == OperationalRunbookStepStatus.Succeeded), failed = steps.Count(x => x.Status == OperationalRunbookStepStatus.Failed), waitingForOperator = steps.Count(x => x.Status == OperationalRunbookStepStatus.WaitingForOperator), lastStep = lastStep?.Name }) ?? "{}";
    }

    private static OperationalRunbookStepStatus JobToStepStatus(OperationalJobRunStatus status)
        => status switch
        {
            OperationalJobRunStatus.Succeeded or OperationalJobRunStatus.Skipped => OperationalRunbookStepStatus.Succeeded,
            OperationalJobRunStatus.PartiallySucceeded => OperationalRunbookStepStatus.Succeeded,
            _ => OperationalRunbookStepStatus.Failed
        };

    private static OperationalRunbookStepStatus ChecklistToStepStatus(object output)
    {
        var json = OperatorAuditService.SerializeSanitized(output) ?? "{}";
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("status", out var value) && value.GetString()?.Equals("Failed", StringComparison.OrdinalIgnoreCase) == true
            ? OperationalRunbookStepStatus.Failed
            : OperationalRunbookStepStatus.Succeeded;
    }

    private static long Duration(DateTimeOffset start, DateTimeOffset end) => (long)Math.Max(0, (end - start).TotalMilliseconds);
    private static object? Deserialize(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<object>(json, JsonOptions);
    private static void RequireReason(string reason, string message) { if (string.IsNullOrWhiteSpace(reason)) throw new DomainRuleViolationException(message); }

    public static IReadOnlyList<(OperationalRunbookDefinition Definition, IReadOnlyList<OperationalRunbookStepDefinition> Steps)> DefaultDefinitions(DateTimeOffset now)
    {
        var sod = Definition(OperationalRunbookType.StartOfDay, "Start of Day", "Local start-of-day operational readiness checks.", now);
        var intraday = Definition(OperationalRunbookType.IntradayCycle, "Intraday Cycle", "Local intraday model and execution processing cycle.", now);
        var eod = Definition(OperationalRunbookType.EndOfDay, "End of Day", "Local fake LMAX EOD import, reconciliation, and PnL checks.", now);
        return
        [
            (sod, [
                Step(sod.Id, 1, "Reference Data Integrity Check", "Validate required local reference data.", OperationalJobType.ReferenceDataIntegrityCheck, OperationalRunbookGateType.StopOnFailure, true, false, null, now),
                Step(sod.Id, 2, "Build Latest Market Data Bars", "Build latest local 15-minute bars.", OperationalJobType.BuildMarketDataBars, OperationalRunbookGateType.ContinueOnWarning, true, true, null, now),
                Step(sod.Id, 3, "Check Active Risk Profile", "Verify an active risk set is present.", null, OperationalRunbookGateType.StopOnFailure, true, false, null, now),
                Step(sod.Id, 4, "Check Open Exceptions", "Warn if open blocking exceptions exist.", null, OperationalRunbookGateType.ContinueOnWarning, false, true, null, now),
                Step(sod.Id, 5, "Manual Operator Confirmation", "Operator confirmed start-of-day checks.", null, OperationalRunbookGateType.ManualConfirmation, true, false, null, now)
            ]),
            (intraday, [
                Step(intraday.Id, 1, "Promote Ready Weight Batches", "Promote ready DB model weight batches.", OperationalJobType.PromoteReadyWeightBatches, OperationalRunbookGateType.StopOnFailure, true, false, null, now),
                Step(intraday.Id, 2, "Process Pending Model Runs", "Process pending model runs through FakeLmax only.", OperationalJobType.ProcessPendingModelRuns, OperationalRunbookGateType.StopOnFailure, true, false, null, now),
                Step(intraday.Id, 3, "Build Latest Market Data Bars", "Build latest local 15-minute bars.", OperationalJobType.BuildMarketDataBars, OperationalRunbookGateType.ContinueOnWarning, false, true, null, now),
                Step(intraday.Id, 4, "Check Exceptions", "Warn if new blocking exceptions exist.", null, OperationalRunbookGateType.ContinueOnWarning, false, true, null, now)
            ]),
            (eod, [
                Step(eod.Id, 1, "Generate Fake LMAX EOD Reports", "Generate local fake actual-schema LMAX EOD reports only.", OperationalJobType.GenerateFakeLmaxEodReports, OperationalRunbookGateType.ContinueOnWarning, false, true, null, now),
                Step(eod.Id, 2, "Import Generated LMAX EOD Reports", "Import generated local LMAX EOD reports only.", OperationalJobType.ImportGeneratedLmaxEodReports, OperationalRunbookGateType.ContinueOnWarning, true, true, null, now),
                Step(eod.Id, 3, "Run EOD Reconciliation", "Run local EOD reconciliation from imported reports.", OperationalJobType.RunEodReconciliation, OperationalRunbookGateType.StopOnFailure, true, false, null, now),
                Step(eod.Id, 4, "Calculate EOD PnL Summary", "Calculate USD wallet/PnL summary.", OperationalJobType.CalculateEodPnlSummary, OperationalRunbookGateType.ContinueOnWarning, true, true, null, now),
                Step(eod.Id, 5, "Check Open EOD Exceptions", "Warn if unresolved critical EOD cases exist.", null, OperationalRunbookGateType.ContinueOnWarning, false, true, null, now),
                Step(eod.Id, 6, "Manual Operator Confirmation", "Operator confirmed end-of-day checks.", null, OperationalRunbookGateType.ManualConfirmation, true, false, null, now)
            ])
        ];
    }

    private static OperationalRunbookDefinition Definition(OperationalRunbookType type, string name, string description, DateTimeOffset now)
        => new(new OperationalRunbookDefinitionId(DeterministicGuid($"runbook:{type}")), name, type, description, true, true, now);

    private static OperationalRunbookStepDefinition Step(OperationalRunbookDefinitionId definitionId, int order, string name, string description, OperationalJobType? jobType, OperationalRunbookGateType gateType, bool required, bool continueOnFailure, string? input, DateTimeOffset now)
        => new(new OperationalRunbookStepDefinitionId(DeterministicGuid($"runbook-step:{definitionId.Value}:{order}")), definitionId, order, name, description, jobType, gateType, required, continueOnFailure, input, now);

    private static Guid DeterministicGuid(string value)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        return new Guid(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"ops:{value}")));
    }
}

public sealed class InMemoryOperationalRunbookRepository(PlatformState state) : IOperationalRunbookRepository
{
    private readonly object sync = new();
    public Task AddDefinitionAsync(OperationalRunbookDefinition definition, IReadOnlyList<OperationalRunbookStepDefinition> steps, CancellationToken cancellationToken) { lock (sync) { if (!state.OperationalRunbookDefinitions.Any(x => x.Id == definition.Id)) state.OperationalRunbookDefinitions.Add(definition); foreach (var step in steps) if (!state.OperationalRunbookStepDefinitions.Any(x => x.Id == step.Id)) state.OperationalRunbookStepDefinitions.Add(step); } return Task.CompletedTask; }
    public Task<IReadOnlyList<OperationalRunbookDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken) { lock (sync) return Task.FromResult<IReadOnlyList<OperationalRunbookDefinition>>(state.OperationalRunbookDefinitions.ToList()); }
    public Task<OperationalRunbookDefinition?> GetDefinitionAsync(OperationalRunbookDefinitionId id, CancellationToken cancellationToken) { lock (sync) return Task.FromResult(state.OperationalRunbookDefinitions.FirstOrDefault(x => x.Id == id)); }
    public Task<OperationalRunbookDefinition?> GetDefinitionByTypeAsync(OperationalRunbookType runbookType, CancellationToken cancellationToken) { lock (sync) return Task.FromResult(state.OperationalRunbookDefinitions.FirstOrDefault(x => x.RunbookType == runbookType)); }
    public Task<IReadOnlyList<OperationalRunbookStepDefinition>> GetStepDefinitionsAsync(OperationalRunbookDefinitionId definitionId, CancellationToken cancellationToken) { lock (sync) return Task.FromResult<IReadOnlyList<OperationalRunbookStepDefinition>>(state.OperationalRunbookStepDefinitions.Where(x => x.RunbookDefinitionId == definitionId).OrderBy(x => x.StepOrder).ToList()); }
    public Task AddRunAsync(OperationalRunbookRun run, IReadOnlyList<OperationalRunbookStepRun> steps, CancellationToken cancellationToken) { lock (sync) { state.OperationalRunbookRuns.Add(run); state.OperationalRunbookStepRuns.AddRange(steps); } return Task.CompletedTask; }
    public Task UpdateRunAsync(OperationalRunbookRun run, CancellationToken cancellationToken) { lock (sync) { state.OperationalRunbookRuns.RemoveAll(x => x.Id == run.Id); state.OperationalRunbookRuns.Add(run); } return Task.CompletedTask; }
    public Task<OperationalRunbookRun?> GetRunAsync(OperationalRunbookRunId id, CancellationToken cancellationToken) { lock (sync) return Task.FromResult(state.OperationalRunbookRuns.FirstOrDefault(x => x.Id == id)); }
    public Task<IReadOnlyList<OperationalRunbookRun>> GetRunsAsync(OperationalRunbookRunFilter filter, CancellationToken cancellationToken) { lock (sync) { IEnumerable<OperationalRunbookRun> query = state.OperationalRunbookRuns; if (filter.RunbookType is not null) query = query.Where(x => x.RunbookType == filter.RunbookType); if (filter.Status is not null) query = query.Where(x => x.Status == filter.Status); if (filter.FromUtc is not null) query = query.Where(x => x.StartedAtUtc >= filter.FromUtc); if (filter.ToUtc is not null) query = query.Where(x => x.StartedAtUtc <= filter.ToUtc); return Task.FromResult<IReadOnlyList<OperationalRunbookRun>>(query.OrderByDescending(x => x.StartedAtUtc).Take(Math.Clamp(filter.Limit, 1, 500)).ToList()); } }
    public Task UpdateStepRunAsync(OperationalRunbookStepRun step, CancellationToken cancellationToken) { lock (sync) { state.OperationalRunbookStepRuns.RemoveAll(x => x.Id == step.Id); state.OperationalRunbookStepRuns.Add(step); } return Task.CompletedTask; }
    public Task<IReadOnlyList<OperationalRunbookStepRun>> GetStepRunsAsync(OperationalRunbookRunId runId, CancellationToken cancellationToken) { lock (sync) return Task.FromResult<IReadOnlyList<OperationalRunbookStepRun>>(state.OperationalRunbookStepRuns.Where(x => x.RunbookRunId == runId).OrderBy(x => x.StepOrder).ToList()); }
    public Task<IReadOnlyList<OperationalScheduleDefinition>> GetSchedulesAsync(CancellationToken cancellationToken) { lock (sync) return Task.FromResult<IReadOnlyList<OperationalScheduleDefinition>>(state.OperationalScheduleDefinitions.OrderBy(x => x.Name).ToList()); }
    public Task UpsertScheduleAsync(OperationalScheduleDefinition schedule, CancellationToken cancellationToken) { lock (sync) { state.OperationalScheduleDefinitions.RemoveAll(x => x.Id == schedule.Id); state.OperationalScheduleDefinitions.Add(schedule); } return Task.CompletedTask; }
}
