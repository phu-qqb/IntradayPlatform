namespace QQ.Production.Intraday.Domain;

public readonly record struct OperationalJobDefinitionId(Guid Value)
{
    public static OperationalJobDefinitionId New() => new(Guid.NewGuid());
}

public readonly record struct OperationalJobRunId(Guid Value)
{
    public static OperationalJobRunId New() => new(Guid.NewGuid());
}

public readonly record struct OperationalJobStepId(Guid Value)
{
    public static OperationalJobStepId New() => new(Guid.NewGuid());
}

public readonly record struct OperationalRunbookDefinitionId(Guid Value)
{
    public static OperationalRunbookDefinitionId New() => new(Guid.NewGuid());
}

public readonly record struct OperationalRunbookStepDefinitionId(Guid Value)
{
    public static OperationalRunbookStepDefinitionId New() => new(Guid.NewGuid());
}

public readonly record struct OperationalRunbookRunId(Guid Value)
{
    public static OperationalRunbookRunId New() => new(Guid.NewGuid());
}

public readonly record struct OperationalRunbookStepRunId(Guid Value)
{
    public static OperationalRunbookStepRunId New() => new(Guid.NewGuid());
}

public readonly record struct OperationalScheduleDefinitionId(Guid Value)
{
    public static OperationalScheduleDefinitionId New() => new(Guid.NewGuid());
}

public enum OperationalJobRunStatus { Pending, Running, Succeeded, Failed, Cancelled, Skipped, TimedOut, PartiallySucceeded }
public enum OperationalJobType { ReferenceDataIntegrityCheck, BuildMarketDataBars, PromoteReadyWeightBatches, ProcessPendingModelRuns, GenerateFakeLmaxEodReports, ImportGeneratedLmaxEodReports, RunEodReconciliation, CalculateEodPnlSummary, RunGovernanceSmoke, RunLocalSmoke, RunDbWeightsSmoke, RunLmaxEodSmoke, Custom }
public enum OperationalJobTriggerType { Manual, Worker, Api, System, ScheduledLocal, SmokeScript }
public enum OperationalJobSeverity { Info, Warning, Critical }
public enum OperationalJobStepStatus { Pending, Running, Succeeded, Failed, Skipped }
public enum OperationalRunbookType { StartOfDay, IntradayCycle, EndOfDay, Manual, Custom }
public enum OperationalRunbookStatus { NotStarted, Running, Succeeded, Failed, PartiallySucceeded, Cancelled, Blocked, WaitingForOperator, WaitingForApproval }
public enum OperationalRunbookStepStatus { Pending, Running, Succeeded, Failed, Skipped, Blocked, WaitingForOperator, WaitingForApproval }
public enum OperationalRunbookTriggerType { Manual, LocalScheduler, Api, Worker, System }
public enum OperationalRunbookGateType { None, ManualConfirmation, ApprovalRequired, StopOnFailure, ContinueOnWarning }
public enum OperationalChecklistStatus { NotStarted, Running, Complete, Warning, Failed, Blocked, WaitingForOperator }

public sealed record OperationalJobDefinition(
    OperationalJobDefinitionId Id,
    OperationalJobType JobType,
    string Name,
    string Description,
    bool IsEnabled,
    bool IsRerunnable,
    bool RequiresApproval,
    OperationalJobSeverity Severity,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc = null);

public sealed record OperationalJobRun(
    OperationalJobRunId Id,
    OperationalJobDefinitionId? JobDefinitionId,
    OperationalJobType JobType,
    string Name,
    OperationalJobRunStatus Status,
    OperationalJobTriggerType TriggerType,
    OperatorAuditActorType TriggeredByActorType,
    string? TriggeredByOperatorId,
    string? TriggeredByDisplayName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    long? DurationMs,
    string? CorrelationId,
    string? RequestId,
    string? InputJson,
    string? OutputJson,
    string? ErrorMessage,
    ExceptionCaseId? ExceptionCaseId,
    OperatorAuditEventId? AuditEventId,
    OperationalJobRunId? RetryOfJobRunId,
    int RetryCount,
    bool CanRetry,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc = null);

public sealed record OperationalJobStep(
    OperationalJobStepId Id,
    OperationalJobRunId JobRunId,
    string StepName,
    OperationalJobStepStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    long? DurationMs,
    string? Message,
    string? InputJson,
    string? OutputJson,
    string? ErrorMessage);

public sealed record OperationalJobRunEvent(
    Guid Id,
    OperationalJobRunId JobRunId,
    DateTimeOffset OccurredAtUtc,
    OperationalJobSeverity Severity,
    string Message,
    string? MetadataJson);

public sealed record OperationalRunbookDefinition(
    OperationalRunbookDefinitionId Id,
    string Name,
    OperationalRunbookType RunbookType,
    string Description,
    bool IsEnabled,
    bool IsRerunnable,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc = null);

public sealed record OperationalRunbookStepDefinition(
    OperationalRunbookStepDefinitionId Id,
    OperationalRunbookDefinitionId RunbookDefinitionId,
    int StepOrder,
    string Name,
    string Description,
    OperationalJobType? JobType,
    OperationalRunbookGateType GateType,
    bool IsRequired,
    bool ContinueOnFailure,
    string? InputTemplateJson,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc = null);

public sealed record OperationalRunbookRun(
    OperationalRunbookRunId Id,
    OperationalRunbookDefinitionId RunbookDefinitionId,
    OperationalRunbookType RunbookType,
    string Name,
    OperationalRunbookStatus Status,
    OperationalRunbookTriggerType TriggerType,
    string? TriggeredByOperatorId,
    string? TriggeredByDisplayName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    long? DurationMs,
    string? CorrelationId,
    string? Reason,
    string? InputJson,
    string? OutputJson,
    string? ErrorMessage,
    OperationalRunbookRunId? RetryOfRunbookRunId,
    int RetryCount,
    bool CanRetry,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc = null);

public sealed record OperationalRunbookStepRun(
    OperationalRunbookStepRunId Id,
    OperationalRunbookRunId RunbookRunId,
    OperationalRunbookStepDefinitionId? StepDefinitionId,
    int StepOrder,
    string Name,
    OperationalRunbookStepStatus Status,
    OperationalJobRunId? JobRunId,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    long? DurationMs,
    string? Message,
    string? InputJson,
    string? OutputJson,
    string? ErrorMessage,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc = null);

public sealed record OperationalScheduleDefinition(
    OperationalScheduleDefinitionId Id,
    string Name,
    OperationalRunbookDefinitionId RunbookDefinitionId,
    bool IsEnabled,
    string? CronExpression,
    int? FixedIntervalMinutes,
    string TimeZoneId,
    DateTimeOffset? NextRunAtUtc,
    DateTimeOffset? LastRunAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc = null);
