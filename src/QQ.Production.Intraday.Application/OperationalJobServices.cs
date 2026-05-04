using System.Text.Json;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public sealed record OperationalJobRunFilter(int Limit, OperationalJobRunStatus? Status = null, OperationalJobType? JobType = null, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null);
public sealed record RunOperationalJobRequest(OperationalJobType JobType, string Reason, object? Input = null, OperationalJobTriggerType TriggerType = OperationalJobTriggerType.Manual, OperationalJobRunId? RetryOfJobRunId = null);
public sealed record DailyOperationsSummary(DateOnly Date, OperationalJobRun? LatestReferenceIntegrity, OperationalJobRun? LatestMarketDataBars, OperationalJobRun? LatestWeightPromotion, OperationalJobRun? LatestModelRunProcessing, OperationalJobRun? LatestEodImport, OperationalJobRun? LatestEodReconciliation, OperationalJobRun? LatestPnlSummary, int OpenExceptionCount, int OpenBlockingExceptionCount, int FailedJobCount, int PendingApprovalCount);
public enum DailyChecklistItemStatus { NotStarted, Running, Complete, Warning, Failed, Blocked }
public sealed record DailyChecklistItem(string Name, DailyChecklistItemStatus Status, string Message, string? RelatedEntityType = null, string? RelatedEntityId = null);

public interface IOperationalJobRepository
{
    Task AddDefinitionAsync(OperationalJobDefinition definition, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperationalJobDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken);
    Task<OperationalJobDefinition?> GetDefinitionAsync(OperationalJobDefinitionId id, CancellationToken cancellationToken);
    Task<OperationalJobDefinition?> GetDefinitionByTypeAsync(OperationalJobType jobType, CancellationToken cancellationToken);
    Task AddRunAsync(OperationalJobRun run, CancellationToken cancellationToken);
    Task UpdateRunAsync(OperationalJobRun run, CancellationToken cancellationToken);
    Task<OperationalJobRun?> GetRunAsync(OperationalJobRunId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperationalJobRun>> GetRunsAsync(OperationalJobRunFilter filter, CancellationToken cancellationToken);
    Task AddStepAsync(OperationalJobStep step, CancellationToken cancellationToken);
    Task UpdateStepAsync(OperationalJobStep step, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperationalJobStep>> GetStepsAsync(OperationalJobRunId jobRunId, CancellationToken cancellationToken);
    Task AddEventAsync(OperationalJobRunEvent jobEvent, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperationalJobRunEvent>> GetEventsAsync(OperationalJobRunId jobRunId, CancellationToken cancellationToken);
}

public interface IOperationalJobRunner
{
    Task<IReadOnlyList<OperationalJobDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken);
    Task<OperationalJobRun> RunJobAsync(RunOperationalJobRequest request, CancellationToken cancellationToken);
    Task<OperationalJobRun> RetryJobAsync(OperationalJobRunId id, string reason, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperationalJobRun>> GetRecentJobRunsAsync(OperationalJobRunFilter filter, CancellationToken cancellationToken);
    Task<OperationalJobRun?> GetJobRunAsync(OperationalJobRunId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperationalJobStep>> GetJobStepsAsync(OperationalJobRunId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperationalJobRunEvent>> GetJobEventsAsync(OperationalJobRunId id, CancellationToken cancellationToken);
}

public interface IDailyOperationsService
{
    Task<DailyOperationsSummary> GetTodaySummaryAsync(DateOnly date, CancellationToken cancellationToken);
    Task<IReadOnlyList<DailyChecklistItem>> GetDailyChecklistAsync(DateOnly date, CancellationToken cancellationToken);
    Task<IReadOnlyList<object>> GetOperationalTimelineAsync(DateOnly date, CancellationToken cancellationToken);
}

public sealed class OperationalJobRunner(
    IOperationalJobRepository repository,
    IOperatorAuditService audit,
    IOperatorContext context,
    IOperatorPermissionService permissions,
    IClock clock,
    IReferenceDataIntegrityService referenceDataIntegrity,
    IIntradayRepository intradayRepository,
    IBarBuilderService barBuilder,
    IModelWeightPromotionService modelWeightPromotion,
    ProcessModelRunService processModelRunService,
    IFakeLmaxEodReportGenerator fakeLmaxEodReportGenerator,
    ILmaxEodReportImportService lmaxEodReportImport,
    IEodReconciliationService eodReconciliation,
    IEodPnlSummaryService eodPnlSummary,
    IExceptionCaseService exceptionCases) : IOperationalJobRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<OperationalJobDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        var definitions = await repository.GetDefinitionsAsync(cancellationToken);
        if (definitions.Count > 0) return definitions.OrderBy(x => x.JobType).ToList();
        var seeded = DefaultDefinitions(clock.UtcNow);
        foreach (var definition in seeded)
        {
            await repository.AddDefinitionAsync(definition, cancellationToken);
        }
        return seeded;
    }

    public async Task<OperationalJobRun> RunJobAsync(RunOperationalJobRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new DomainRuleViolationException("A reason is required to run an operational job.");
        await permissions.RequirePermissionAsync(OperatorPermission.RunOperationalJobs, cancellationToken);
        var definitions = await GetDefinitionsAsync(cancellationToken);
        var definition = definitions.FirstOrDefault(x => x.JobType == request.JobType);
        if (definition is not null && !definition.IsEnabled) throw new DomainRuleViolationException($"Operational job '{request.JobType}' is disabled.");
        var now = clock.UtcNow;
        var actor = context.Current;
        var run = new OperationalJobRun(
            OperationalJobRunId.New(),
            definition?.Id,
            request.JobType,
            definition?.Name ?? request.JobType.ToString(),
            OperationalJobRunStatus.Running,
            request.TriggerType,
            actor.ActorType,
            actor.ActorId,
            actor.ActorDisplayName,
            now,
            null,
            null,
            context.CorrelationId,
            context.RequestId,
            OperatorAuditService.SerializeSanitized(request.Input),
            null,
            null,
            null,
            null,
            request.RetryOfJobRunId,
            request.RetryOfJobRunId is null ? 0 : 1,
            definition?.IsRerunnable ?? true,
            now,
            now);
        await repository.AddRunAsync(run, cancellationToken);
        var startAudit = await audit.RecordAsync(new(OperatorAuditEventType.OperationalJobStarted, OperatorAuditSeverity.Info, OperatorAuditResult.Started, "OperationalJobRunner", $"Operational job {run.JobType} started.", "OperationalJobRun", run.Id.Value.ToString("D"), request.Reason, Metadata: new { run.JobType, request.Input }), cancellationToken);
        if (startAudit is not null)
        {
            run = run with { AuditEventId = startAudit.Id };
            await repository.UpdateRunAsync(run, cancellationToken);
        }
        await AddEventAsync(run.Id, OperationalJobSeverity.Info, "Job started.", new { run.JobType }, cancellationToken);

        try
        {
            var output = await ExecuteAsync(run, request.Input, cancellationToken);
            var status = DetermineStatus(run.JobType, output);
            var completed = Complete(run, status, output, null);
            await repository.UpdateRunAsync(completed, cancellationToken);
            await audit.RecordAsync(new(status == OperationalJobRunStatus.Succeeded ? OperatorAuditEventType.OperationalJobSucceeded : OperatorAuditEventType.OperationalJobFailed, status == OperationalJobRunStatus.Succeeded ? OperatorAuditSeverity.Info : OperatorAuditSeverity.Warning, status == OperationalJobRunStatus.Succeeded ? OperatorAuditResult.Succeeded : OperatorAuditResult.Blocked, "OperationalJobRunner", $"Operational job {run.JobType} completed with status {status}.", "OperationalJobRun", run.Id.Value.ToString("D"), request.Reason, Metadata: output), cancellationToken);
            return completed;
        }
        catch (Exception ex)
        {
            var failed = Complete(run, OperationalJobRunStatus.Failed, null, ex.Message);
            await repository.UpdateRunAsync(failed, cancellationToken);
            await AddEventAsync(run.Id, definition?.Severity == OperationalJobSeverity.Critical ? OperationalJobSeverity.Critical : OperationalJobSeverity.Warning, ex.Message, null, cancellationToken);
            await audit.RecordFailedAsync(OperatorAuditEventType.OperationalJobFailed, "OperationalJobRunner", $"Operational job {run.JobType} failed.", ex.Message, "OperationalJobRun", run.Id.Value.ToString("D"), new { run.JobType }, cancellationToken);
            if (definition?.Severity == OperationalJobSeverity.Critical)
            {
                var exceptionCase = await exceptionCases.CreateManualCaseAsync(new CreateExceptionCaseRequest(ExceptionCaseSeverity.Critical, ExceptionCaseType.SystemHealth, ExceptionCaseSource.SystemHealth, $"Operational job failed: {run.Name}", ex.Message, "OperationalJobRun", run.Id.Value.ToString("D"), Metadata: new { run.JobType }), cancellationToken);
                failed = failed with { ExceptionCaseId = exceptionCase.Id };
                await repository.UpdateRunAsync(failed, cancellationToken);
            }
            return failed;
        }
    }

    public async Task<OperationalJobRun> RetryJobAsync(OperationalJobRunId id, string reason, CancellationToken cancellationToken)
    {
        await permissions.RequirePermissionAsync(OperatorPermission.RetryOperationalJobs, cancellationToken);
        var prior = await repository.GetRunAsync(id, cancellationToken) ?? throw new DomainRuleViolationException("Operational job run not found.");
        if (!prior.CanRetry) throw new DomainRuleViolationException("This operational job is not retryable.");
        await audit.RecordSucceededAsync(OperatorAuditEventType.OperationalJobRetried, "OperationalJobRunner", "Operational job retry requested.", "OperationalJobRun", prior.Id.Value.ToString("D"), new { prior.JobType }, cancellationToken);
        return await RunJobAsync(new RunOperationalJobRequest(prior.JobType, reason, Deserialize(prior.InputJson), prior.TriggerType, prior.Id), cancellationToken);
    }

    public Task<IReadOnlyList<OperationalJobRun>> GetRecentJobRunsAsync(OperationalJobRunFilter filter, CancellationToken cancellationToken) => repository.GetRunsAsync(filter, cancellationToken);
    public Task<OperationalJobRun?> GetJobRunAsync(OperationalJobRunId id, CancellationToken cancellationToken) => repository.GetRunAsync(id, cancellationToken);
    public Task<IReadOnlyList<OperationalJobStep>> GetJobStepsAsync(OperationalJobRunId id, CancellationToken cancellationToken) => repository.GetStepsAsync(id, cancellationToken);
    public Task<IReadOnlyList<OperationalJobRunEvent>> GetJobEventsAsync(OperationalJobRunId id, CancellationToken cancellationToken) => repository.GetEventsAsync(id, cancellationToken);

    private async Task<object> ExecuteAsync(OperationalJobRun run, object? input, CancellationToken cancellationToken)
        => run.JobType switch
        {
            OperationalJobType.ReferenceDataIntegrityCheck => await RunStepAsync(run.Id, "Check reference data integrity", input, async () =>
            {
                var result = await referenceDataIntegrity.CheckAsync(cancellationToken);
                return new { result.BlockingIssueCount, result.WarningIssueCount };
            }, cancellationToken),
            OperationalJobType.BuildMarketDataBars => await RunStepAsync(run.Id, "Build latest 15-minute bars", input, async () =>
            {
                var state = await intradayRepository.LoadStateAsync(cancellationToken);
                var venue = state.Venues.FirstOrDefault(x => x.Name == "LMAX") ?? throw new DomainRuleViolationException("LMAX venue reference data was not found.");
                var result = await barBuilder.BuildLatestFifteenMinuteBarsAsync(venue.Id, cancellationToken);
                return new { result.RunId, result.BarsCreated, result.BarsUpdated, result.Status, result.ErrorMessage };
            }, cancellationToken),
            OperationalJobType.PromoteReadyWeightBatches => await RunStepAsync(run.Id, "Promote ready weight batches", input, async () =>
            {
                var results = await modelWeightPromotion.PromoteReadyBatchesAsync(10, cancellationToken);
                return new { promotedCount = results.Count(x => x.Succeeded), rejectedCount = results.Count(x => !x.Succeeded), issueCount = results.Sum(x => x.ValidationIssueCount), results = results.Select(x => new { x.BatchId, x.Status, x.ModelRunId, x.Succeeded, x.Message }) };
            }, cancellationToken),
            OperationalJobType.ProcessPendingModelRuns => await RunStepAsync(run.Id, "Process pending model runs", input, async () =>
            {
                var processed = 0;
                var blocked = 0;
                var noAction = 0;
                for (var i = 0; i < 25; i++)
                {
                    var result = await processModelRunService.ProcessNextAsync(cancellationToken);
                    if (result.Status == ProcessModelRunStatus.NoActionRequired) { noAction++; break; }
                    if (result.Processed) processed++;
                    if (result.Blocked) blocked++;
                    if (!result.Processed && !result.Blocked) break;
                }
                return new { processedCount = processed, blockedCount = blocked, noActionCount = noAction };
            }, cancellationToken),
            OperationalJobType.GenerateFakeLmaxEodReports => await RunStepAsync(run.Id, "Generate fake LMAX EOD reports", input, async () =>
            {
                var reportDate = ReadDate(input) ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
                var result = await fakeLmaxEodReportGenerator.GenerateAsync(reportDate, "LMAX", "LMAX_DEMO_LOCAL", LmaxEodMutationMode.None, cancellationToken);
                return result;
            }, cancellationToken),
            OperationalJobType.RunEodReconciliation => await RunStepAsync(run.Id, "Run EOD reconciliation", input, async () =>
            {
                var reportDate = ReadDate(input) ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
                var result = await eodReconciliation.RunAsync(reportDate, "LMAX", "LMAX_DEMO_LOCAL", cancellationToken);
                return new { result.RunId, result.ReportDate, result.BreakCount, result.BlockingBreakCount };
            }, cancellationToken),
            OperationalJobType.CalculateEodPnlSummary => await RunStepAsync(run.Id, "Calculate EOD PnL summary", input, async () =>
            {
                var reportDate = ReadDate(input) ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
                var summary = await eodPnlSummary.GetSummaryAsync(reportDate, "LMAX", "LMAX_DEMO_LOCAL", cancellationToken);
                return summary is null ? new { status = "NoSummary" } : new { summary.TotalWalletBalanceUsd, summary.TotalProfitLossUsd, summary.TotalCommissionUsd, summary.TotalDividendsUsd, summary.TotalFinancingUsd, summary.TotalNetPnlUsd };
            }, cancellationToken),
            OperationalJobType.ImportGeneratedLmaxEodReports => await RunStepAsync(run.Id, "Import generated LMAX EOD reports", input, async () =>
            {
                var doc = ToJson(input);
                var individual = doc?.RootElement.TryGetProperty("individualTradesPath", out var it) == true ? it.GetString() : null;
                var trades = doc?.RootElement.TryGetProperty("tradesSummaryPath", out var ts) == true ? ts.GetString() : null;
                var wallets = doc?.RootElement.TryGetProperty("currencyWalletsPath", out var cw) == true ? cw.GetString() : null;
                var reportDate = ReadDate(input) ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
                if (string.IsNullOrWhiteSpace(individual) || string.IsNullOrWhiteSpace(trades) || string.IsNullOrWhiteSpace(wallets))
                {
                    return new { status = "Skipped", message = "Generated report paths were not supplied." };
                }
                var result = await lmaxEodReportImport.ImportReportSetAsync(individual, trades, wallets, reportDate, "LMAX", "LMAX_DEMO_LOCAL", cancellationToken);
                return new { result.ImportRunId, result.Status, result.RowCount, result.BlockingIssueCount, result.Message };
            }, cancellationToken),
            _ => await RunStepAsync(run.Id, "Custom job", input, () => Task.FromResult<object>(new { status = "Skipped", message = "No local job wrapper is implemented for this job type." }), cancellationToken)
        };

    private async Task<object> RunStepAsync(OperationalJobRunId runId, string stepName, object? input, Func<Task<object>> action, CancellationToken cancellationToken)
    {
        var started = clock.UtcNow;
        var step = new OperationalJobStep(OperationalJobStepId.New(), runId, stepName, OperationalJobStepStatus.Running, started, null, null, null, OperatorAuditService.SerializeSanitized(input), null, null);
        await repository.AddStepAsync(step, cancellationToken);
        try
        {
            var output = await action();
            var completed = step with { Status = OperationalJobStepStatus.Succeeded, CompletedAtUtc = clock.UtcNow, DurationMs = Duration(started, clock.UtcNow), OutputJson = OperatorAuditService.SerializeSanitized(output), Message = "Step succeeded." };
            await repository.UpdateStepAsync(completed, cancellationToken);
            return output;
        }
        catch (Exception ex)
        {
            var failed = step with { Status = OperationalJobStepStatus.Failed, CompletedAtUtc = clock.UtcNow, DurationMs = Duration(started, clock.UtcNow), ErrorMessage = ex.Message };
            await repository.UpdateStepAsync(failed, cancellationToken);
            throw;
        }
    }

    private async Task AddEventAsync(OperationalJobRunId runId, OperationalJobSeverity severity, string message, object? metadata, CancellationToken cancellationToken)
        => await repository.AddEventAsync(new OperationalJobRunEvent(Guid.NewGuid(), runId, clock.UtcNow, severity, message, OperatorAuditService.SerializeSanitized(metadata)), cancellationToken);

    private OperationalJobRun Complete(OperationalJobRun run, OperationalJobRunStatus status, object? output, string? error)
    {
        var completed = clock.UtcNow;
        return run with { Status = status, CompletedAtUtc = completed, DurationMs = Duration(run.StartedAtUtc, completed), OutputJson = OperatorAuditService.SerializeSanitized(output), ErrorMessage = error, UpdatedAtUtc = completed };
    }

    private static OperationalJobRunStatus DetermineStatus(OperationalJobType jobType, object output)
    {
        var json = OperatorAuditService.SerializeSanitized(output) ?? string.Empty;
        if (json.Contains("\"status\":\"Skipped\"", StringComparison.OrdinalIgnoreCase)) return OperationalJobRunStatus.Skipped;
        if (json.Contains("\"blockingIssueCount\":0", StringComparison.OrdinalIgnoreCase) || !json.Contains("blockingIssueCount", StringComparison.OrdinalIgnoreCase)) return OperationalJobRunStatus.Succeeded;
        return jobType == OperationalJobType.ReferenceDataIntegrityCheck ? OperationalJobRunStatus.Failed : OperationalJobRunStatus.PartiallySucceeded;
    }

    private static long Duration(DateTimeOffset start, DateTimeOffset end) => (long)Math.Max(0, (end - start).TotalMilliseconds);
    private static object? Deserialize(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<object>(json, JsonOptions);
    private static JsonDocument? ToJson(object? input) => input is null ? null : JsonDocument.Parse(JsonSerializer.Serialize(input, JsonOptions));
    private static DateOnly? ReadDate(object? input)
    {
        using var doc = ToJson(input);
        if (doc?.RootElement.TryGetProperty("reportDate", out var value) == true && DateOnly.TryParse(value.GetString(), out var date)) return date;
        if (doc?.RootElement.TryGetProperty("date", out value) == true && DateOnly.TryParse(value.GetString(), out date)) return date;
        return null;
    }

    public static IReadOnlyList<OperationalJobDefinition> DefaultDefinitions(DateTimeOffset now) =>
    [
        Definition(OperationalJobType.ReferenceDataIntegrityCheck, "Reference Data Integrity Check", "Validate required local reference data.", true, true, false, OperationalJobSeverity.Critical, now),
        Definition(OperationalJobType.BuildMarketDataBars, "Build Latest 15m Bars", "Build latest local 15-minute market data bars.", true, true, false, OperationalJobSeverity.Warning, now),
        Definition(OperationalJobType.PromoteReadyWeightBatches, "Promote Ready Weight Batches", "Validate and promote ready DB model weight batches.", true, true, false, OperationalJobSeverity.Warning, now),
        Definition(OperationalJobType.ProcessPendingModelRuns, "Process Pending Model Runs", "Process pending model runs through FakeLmax only.", true, true, false, OperationalJobSeverity.Warning, now),
        Definition(OperationalJobType.GenerateFakeLmaxEodReports, "Generate Fake LMAX EOD Reports", "Generate local fake actual-schema LMAX EOD reports.", true, true, false, OperationalJobSeverity.Info, now),
        Definition(OperationalJobType.ImportGeneratedLmaxEodReports, "Import Generated LMAX EOD Reports", "Import generated local LMAX EOD report files.", true, true, false, OperationalJobSeverity.Warning, now),
        Definition(OperationalJobType.RunEodReconciliation, "Run EOD Reconciliation", "Run EOD reconciliation from imported local reports.", true, true, false, OperationalJobSeverity.Critical, now),
        Definition(OperationalJobType.CalculateEodPnlSummary, "Calculate EOD PnL Summary", "Calculate USD wallet/PnL summary from imported currency wallets.", true, true, false, OperationalJobSeverity.Info, now)
    ];

    private static OperationalJobDefinition Definition(OperationalJobType type, string name, string description, bool enabled, bool rerunnable, bool requiresApproval, OperationalJobSeverity severity, DateTimeOffset now)
        => new(new OperationalJobDefinitionId(DeterministicGuid(type.ToString())), type, name, description, enabled, rerunnable, requiresApproval, severity, now);

    private static Guid DeterministicGuid(string value)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        return new Guid(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"ops:{value}")));
    }
}

public sealed class DailyOperationsService(IOperationalJobRunner jobs, IExceptionCaseService exceptions, IApprovalWorkflowService approvals, IOperatorAuditService audit) : IDailyOperationsService
{
    public async Task<DailyOperationsSummary> GetTodaySummaryAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = from.AddDays(1);
        var runs = await jobs.GetRecentJobRunsAsync(new OperationalJobRunFilter(500, FromUtc: from, ToUtc: to), cancellationToken);
        var cases = await exceptions.GetCasesAsync(new ExceptionCaseFilter(500), cancellationToken);
        var pending = await approvals.GetApprovalRequestsAsync(new ApprovalRequestFilter(500, ApprovalRequestStatus.Pending), cancellationToken);
        await audit.RecordSucceededAsync(OperatorAuditEventType.DailyOperationsSummaryViewed, "DailyOperationsService", "Daily operations summary viewed.", metadata: new { date }, cancellationToken: cancellationToken);
        return new(date,
            Latest(runs, OperationalJobType.ReferenceDataIntegrityCheck),
            Latest(runs, OperationalJobType.BuildMarketDataBars),
            Latest(runs, OperationalJobType.PromoteReadyWeightBatches),
            Latest(runs, OperationalJobType.ProcessPendingModelRuns),
            Latest(runs, OperationalJobType.ImportGeneratedLmaxEodReports),
            Latest(runs, OperationalJobType.RunEodReconciliation),
            Latest(runs, OperationalJobType.CalculateEodPnlSummary),
            cases.Count(x => x.Status is ExceptionCaseStatus.Open or ExceptionCaseStatus.Acknowledged or ExceptionCaseStatus.Investigating),
            cases.Count(x =>
                (x.Status is ExceptionCaseStatus.Open or ExceptionCaseStatus.Acknowledged or ExceptionCaseStatus.Investigating)
                && (x.Severity is ExceptionCaseSeverity.Blocking or ExceptionCaseSeverity.Critical)),
            runs.Count(x => x.Status == OperationalJobRunStatus.Failed),
            pending.Count);
    }

    public async Task<IReadOnlyList<DailyChecklistItem>> GetDailyChecklistAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var summary = await GetTodaySummaryAsync(date, cancellationToken);
        await audit.RecordSucceededAsync(OperatorAuditEventType.DailyChecklistViewed, "DailyOperationsService", "Daily operations checklist viewed.", metadata: new { date }, cancellationToken: cancellationToken);
        return
        [
            Item("Reference data clean", summary.LatestReferenceIntegrity, "Run reference check"),
            Item("Market data available", summary.LatestMarketDataBars, "Build latest 15m bars"),
            Item("Weight batches promoted", summary.LatestWeightPromotion, "Promote ready weights"),
            Item("Model runs processed", summary.LatestModelRunProcessing, "Process pending model runs"),
            Item("EOD reports imported", summary.LatestEodImport, "Import generated EOD reports"),
            Item("EOD reconciliation clean", summary.LatestEodReconciliation, "Run EOD reconciliation"),
            Item("PnL summary available", summary.LatestPnlSummary, "Calculate PnL summary"),
            new("No open blocking exceptions", summary.OpenBlockingExceptionCount == 0 ? DailyChecklistItemStatus.Complete : DailyChecklistItemStatus.Blocked, $"{summary.OpenBlockingExceptionCount} open blocking/critical exceptions"),
            new("Governance approvals clear", summary.PendingApprovalCount == 0 ? DailyChecklistItemStatus.Complete : DailyChecklistItemStatus.Warning, $"{summary.PendingApprovalCount} pending approvals")
        ];
    }

    public async Task<IReadOnlyList<object>> GetOperationalTimelineAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = from.AddDays(1);
        var runs = await jobs.GetRecentJobRunsAsync(new OperationalJobRunFilter(100, FromUtc: from, ToUtc: to), cancellationToken);
        return runs.OrderByDescending(x => x.StartedAtUtc).Select(x => (object)new { type = "JobRun", x.Id, x.JobType, x.Status, occurredAtUtc = x.StartedAtUtc, x.Name }).ToList();
    }

    private static OperationalJobRun? Latest(IReadOnlyList<OperationalJobRun> runs, OperationalJobType type)
        => runs.Where(x => x.JobType == type).OrderByDescending(x => x.StartedAtUtc).FirstOrDefault();

    private static DailyChecklistItem Item(string name, OperationalJobRun? run, string notStarted)
        => run is null
            ? new(name, DailyChecklistItemStatus.NotStarted, notStarted)
            : new(name, run.Status switch
            {
                OperationalJobRunStatus.Running or OperationalJobRunStatus.Pending => DailyChecklistItemStatus.Running,
                OperationalJobRunStatus.Succeeded => DailyChecklistItemStatus.Complete,
                OperationalJobRunStatus.PartiallySucceeded or OperationalJobRunStatus.Skipped => DailyChecklistItemStatus.Warning,
                OperationalJobRunStatus.Failed or OperationalJobRunStatus.TimedOut => DailyChecklistItemStatus.Failed,
                _ => DailyChecklistItemStatus.Warning
            }, $"{run.JobType} is {run.Status}.", "OperationalJobRun", run.Id.Value.ToString("D"));
}

public sealed class InMemoryOperationalJobRepository(PlatformState state) : IOperationalJobRepository
{
    private readonly object sync = new();
    public Task AddDefinitionAsync(OperationalJobDefinition definition, CancellationToken cancellationToken) { lock (sync) if (!state.OperationalJobDefinitions.Any(x => x.Id == definition.Id)) state.OperationalJobDefinitions.Add(definition); return Task.CompletedTask; }
    public Task<IReadOnlyList<OperationalJobDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken) { lock (sync) return Task.FromResult<IReadOnlyList<OperationalJobDefinition>>(state.OperationalJobDefinitions.ToList()); }
    public Task<OperationalJobDefinition?> GetDefinitionAsync(OperationalJobDefinitionId id, CancellationToken cancellationToken) { lock (sync) return Task.FromResult(state.OperationalJobDefinitions.FirstOrDefault(x => x.Id == id)); }
    public Task<OperationalJobDefinition?> GetDefinitionByTypeAsync(OperationalJobType jobType, CancellationToken cancellationToken) { lock (sync) return Task.FromResult(state.OperationalJobDefinitions.FirstOrDefault(x => x.JobType == jobType)); }
    public Task AddRunAsync(OperationalJobRun run, CancellationToken cancellationToken) { lock (sync) state.OperationalJobRuns.Add(run); return Task.CompletedTask; }
    public Task UpdateRunAsync(OperationalJobRun run, CancellationToken cancellationToken) { lock (sync) { state.OperationalJobRuns.RemoveAll(x => x.Id == run.Id); state.OperationalJobRuns.Add(run); } return Task.CompletedTask; }
    public Task<OperationalJobRun?> GetRunAsync(OperationalJobRunId id, CancellationToken cancellationToken) { lock (sync) return Task.FromResult(state.OperationalJobRuns.FirstOrDefault(x => x.Id == id)); }
    public Task<IReadOnlyList<OperationalJobRun>> GetRunsAsync(OperationalJobRunFilter filter, CancellationToken cancellationToken) { lock (sync) { IEnumerable<OperationalJobRun> query = state.OperationalJobRuns; if (filter.Status is not null) query = query.Where(x => x.Status == filter.Status); if (filter.JobType is not null) query = query.Where(x => x.JobType == filter.JobType); if (filter.FromUtc is not null) query = query.Where(x => x.StartedAtUtc >= filter.FromUtc); if (filter.ToUtc is not null) query = query.Where(x => x.StartedAtUtc <= filter.ToUtc); return Task.FromResult<IReadOnlyList<OperationalJobRun>>(query.OrderByDescending(x => x.StartedAtUtc).Take(Math.Clamp(filter.Limit, 1, 500)).ToList()); } }
    public Task AddStepAsync(OperationalJobStep step, CancellationToken cancellationToken) { lock (sync) state.OperationalJobSteps.Add(step); return Task.CompletedTask; }
    public Task UpdateStepAsync(OperationalJobStep step, CancellationToken cancellationToken) { lock (sync) { state.OperationalJobSteps.RemoveAll(x => x.Id == step.Id); state.OperationalJobSteps.Add(step); } return Task.CompletedTask; }
    public Task<IReadOnlyList<OperationalJobStep>> GetStepsAsync(OperationalJobRunId jobRunId, CancellationToken cancellationToken) { lock (sync) return Task.FromResult<IReadOnlyList<OperationalJobStep>>(state.OperationalJobSteps.Where(x => x.JobRunId == jobRunId).OrderBy(x => x.StartedAtUtc).ToList()); }
    public Task AddEventAsync(OperationalJobRunEvent jobEvent, CancellationToken cancellationToken) { lock (sync) state.OperationalJobRunEvents.Add(jobEvent); return Task.CompletedTask; }
    public Task<IReadOnlyList<OperationalJobRunEvent>> GetEventsAsync(OperationalJobRunId jobRunId, CancellationToken cancellationToken) { lock (sync) return Task.FromResult<IReadOnlyList<OperationalJobRunEvent>>(state.OperationalJobRunEvents.Where(x => x.JobRunId == jobRunId).OrderBy(x => x.OccurredAtUtc).ToList()); }
}
