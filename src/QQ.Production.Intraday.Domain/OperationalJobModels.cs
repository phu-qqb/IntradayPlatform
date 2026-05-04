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

public enum OperationalJobRunStatus { Pending, Running, Succeeded, Failed, Cancelled, Skipped, TimedOut, PartiallySucceeded }
public enum OperationalJobType { ReferenceDataIntegrityCheck, BuildMarketDataBars, PromoteReadyWeightBatches, ProcessPendingModelRuns, GenerateFakeLmaxEodReports, ImportGeneratedLmaxEodReports, RunEodReconciliation, CalculateEodPnlSummary, RunGovernanceSmoke, RunLocalSmoke, RunDbWeightsSmoke, RunLmaxEodSmoke, Custom }
public enum OperationalJobTriggerType { Manual, Worker, Api, System, ScheduledLocal, SmokeScript }
public enum OperationalJobSeverity { Info, Warning, Critical }
public enum OperationalJobStepStatus { Pending, Running, Succeeded, Failed, Skipped }

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
