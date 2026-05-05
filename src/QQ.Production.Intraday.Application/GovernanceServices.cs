using System.Text.Json;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public sealed record GovernanceOptions(
    bool FourEyesEnabled = true,
    bool RequireApprovalForRiskActivation = true,
    bool RequireApprovalForRiskRetirement = true,
    bool RequireApprovalForKillSwitchClear = true,
    bool RequireApprovalForWaiveBlockingException = true,
    bool RequireApprovalForFalsePositiveBlockingException = true,
    bool RequireApprovalForResolveCriticalException = true,
    int ApprovalExpiryMinutes = 1440);

public sealed record OperatorContextOptions(string DefaultOperatorId = "local-admin", bool AllowHeaderOperatorOverride = true);

public sealed record ApprovalRequestFilter(
    int Limit,
    ApprovalRequestStatus? Status = null,
    ApprovalRequestType? Type = null,
    string? RequestedBy = null,
    string? EntityType = null,
    string? EntityId = null);

public sealed record GovernedActionResult(
    bool Executed,
    bool ApprovalRequired,
    ApprovalRequestId? ApprovalRequestId,
    string Status,
    string Message,
    string EntityId,
    string? ResultEntityId,
    string? CorrelationId);

public sealed record CreateApprovalRequestRequest(
    ApprovalRequestType Type,
    string EntityType,
    string EntityId,
    string Reason,
    object Payload,
    object? Before = null,
    object? After = null,
    OperatorRole RequiredApproverRole = OperatorRole.Approver);

public interface IOperatorGovernanceRepository
{
    Task<IReadOnlyList<OperatorUser>> GetOperatorsAsync(CancellationToken cancellationToken);
    Task<OperatorUser?> GetOperatorByIdAsync(string operatorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatorUserRole>> GetRolesAsync(OperatorUserId operatorUserId, CancellationToken cancellationToken);
    Task UpsertOperatorAsync(OperatorUser user, IReadOnlyList<OperatorUserRole> roles, CancellationToken cancellationToken);
    Task AddApprovalRequestAsync(ApprovalRequest request, CancellationToken cancellationToken);
    Task UpdateApprovalRequestAsync(ApprovalRequest request, CancellationToken cancellationToken);
    Task AddApprovalDecisionAsync(ApprovalDecision decision, CancellationToken cancellationToken);
    Task<ApprovalRequest?> GetApprovalRequestAsync(ApprovalRequestId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApprovalRequest>> GetApprovalRequestsAsync(ApprovalRequestFilter filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApprovalDecision>> GetApprovalDecisionsAsync(ApprovalRequestId id, CancellationToken cancellationToken);
}

public interface IOperatorPermissionService
{
    Task<OperatorUser?> GetCurrentOperatorAsync(CancellationToken cancellationToken);
    Task<IReadOnlySet<OperatorRole>> GetRolesAsync(OperatorUserId operatorUserId, CancellationToken cancellationToken);
    Task<IReadOnlySet<OperatorPermission>> GetPermissionsAsync(OperatorUserId operatorUserId, CancellationToken cancellationToken);
    Task<bool> HasPermissionAsync(OperatorPermission permission, CancellationToken cancellationToken);
    Task RequirePermissionAsync(OperatorPermission permission, CancellationToken cancellationToken);
}

public interface IApprovalWorkflowService
{
    Task<ApprovalRequest> CreateApprovalRequestAsync(CreateApprovalRequestRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApprovalRequest>> GetApprovalRequestsAsync(ApprovalRequestFilter filter, CancellationToken cancellationToken);
    Task<ApprovalRequest?> GetApprovalRequestAsync(ApprovalRequestId id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApprovalDecision>> GetDecisionsAsync(ApprovalRequestId id, CancellationToken cancellationToken);
    Task<ApprovalRequest> ApproveAsync(ApprovalRequestId id, string reason, CancellationToken cancellationToken);
    Task<ApprovalRequest> RejectAsync(ApprovalRequestId id, string reason, CancellationToken cancellationToken);
    Task<ApprovalRequest> CancelAsync(ApprovalRequestId id, string reason, CancellationToken cancellationToken);
    Task<GovernedActionResult> ExecuteApprovedAsync(ApprovalRequestId id, CancellationToken cancellationToken);
}

public sealed class OperatorPermissionService(
    IOperatorGovernanceRepository repository,
    IOperatorContext context,
    IOperatorAuditService audit) : IOperatorPermissionService
{
    public Task<OperatorUser?> GetCurrentOperatorAsync(CancellationToken cancellationToken)
        => repository.GetOperatorByIdAsync(context.Current.ActorId, cancellationToken);

    public async Task<IReadOnlySet<OperatorRole>> GetRolesAsync(OperatorUserId operatorUserId, CancellationToken cancellationToken)
        => (await repository.GetRolesAsync(operatorUserId, cancellationToken)).Select(x => x.Role).ToHashSet();

    public async Task<IReadOnlySet<OperatorPermission>> GetPermissionsAsync(OperatorUserId operatorUserId, CancellationToken cancellationToken)
    {
        var roles = await GetRolesAsync(operatorUserId, cancellationToken);
        return PermissionsForRoles(roles);
    }

    public async Task<bool> HasPermissionAsync(OperatorPermission permission, CancellationToken cancellationToken)
    {
        var current = await GetCurrentOperatorAsync(cancellationToken);
        if (current is null || !current.IsEnabled) return false;
        var permissions = await GetPermissionsAsync(current.Id, cancellationToken);
        return permissions.Contains(permission) || permissions.Contains(OperatorPermission.Admin);
    }

    public async Task RequirePermissionAsync(OperatorPermission permission, CancellationToken cancellationToken)
    {
        if (await HasPermissionAsync(permission, cancellationToken)) return;
        await audit.RecordBlockedAsync(
            OperatorAuditEventType.PermissionDenied,
            "OperatorPermissionService",
            $"Permission '{permission}' denied.",
            permission.ToString(),
            "Operator",
            context.Current.ActorId,
            new { permission },
            cancellationToken);
        throw new DomainRuleViolationException($"Operator '{context.Current.ActorId}' does not have permission '{permission}'.");
    }

    internal static IReadOnlySet<OperatorPermission> PermissionsForRoles(IEnumerable<OperatorRole> roles)
    {
        var set = new HashSet<OperatorPermission>();
        foreach (var role in roles)
        {
            switch (role)
            {
                case OperatorRole.Admin:
                    foreach (var permission in Enum.GetValues<OperatorPermission>()) set.Add(permission);
                    break;
                case OperatorRole.System:
                    set.Add(OperatorPermission.Admin);
                    set.Add(OperatorPermission.ManageApprovals);
                    break;
                case OperatorRole.Approver:
                    set.Add(OperatorPermission.ViewDashboard);
                    set.Add(OperatorPermission.ViewRiskConfig);
                    set.Add(OperatorPermission.ManageApprovals);
                    set.Add(OperatorPermission.ViewOperations);
                    set.Add(OperatorPermission.ViewJobHistory);
                    set.Add(OperatorPermission.ViewRunbooks);
                    break;
                case OperatorRole.RiskManager:
                    set.Add(OperatorPermission.ViewDashboard);
                    set.Add(OperatorPermission.ViewRiskConfig);
                    set.Add(OperatorPermission.DraftRiskConfig);
                    set.Add(OperatorPermission.ActivateRiskConfig);
                    set.Add(OperatorPermission.RetireRiskConfig);
                    set.Add(OperatorPermission.ManageTradingWindows);
                    set.Add(OperatorPermission.ManageInstrumentControls);
                    set.Add(OperatorPermission.ManageVenueControls);
                    set.Add(OperatorPermission.ManageExceptions);
                    set.Add(OperatorPermission.ResolveExceptions);
                    set.Add(OperatorPermission.WaiveExceptions);
                    set.Add(OperatorPermission.ClearKillSwitch);
                    set.Add(OperatorPermission.ViewOperations);
                    set.Add(OperatorPermission.ViewJobHistory);
                    set.Add(OperatorPermission.RunOperationalJobs);
                    set.Add(OperatorPermission.RetryOperationalJobs);
                    set.Add(OperatorPermission.ViewRunbooks);
                    set.Add(OperatorPermission.RunRunbooks);
                    set.Add(OperatorPermission.CompleteRunbookManualGates);
                    break;
                case OperatorRole.Operator:
                    set.Add(OperatorPermission.ViewDashboard);
                    set.Add(OperatorPermission.CreateModelWeightBatch);
                    set.Add(OperatorPermission.PromoteModelWeightBatch);
                    set.Add(OperatorPermission.ProcessModelRun);
                    set.Add(OperatorPermission.ManageExceptions);
                    set.Add(OperatorPermission.RunEodReconciliation);
                    set.Add(OperatorPermission.ManageEodReports);
                    set.Add(OperatorPermission.ActivateKillSwitch);
                    set.Add(OperatorPermission.ViewOperations);
                    set.Add(OperatorPermission.ViewJobHistory);
                    set.Add(OperatorPermission.RunOperationalJobs);
                    set.Add(OperatorPermission.ViewRunbooks);
                    set.Add(OperatorPermission.RunRunbooks);
                    set.Add(OperatorPermission.CompleteRunbookManualGates);
                    break;
                case OperatorRole.Viewer:
                    set.Add(OperatorPermission.ViewDashboard);
                    set.Add(OperatorPermission.ViewRiskConfig);
                    set.Add(OperatorPermission.ViewOperations);
                    set.Add(OperatorPermission.ViewJobHistory);
                    set.Add(OperatorPermission.ViewRunbooks);
                    break;
            }
        }
        return set;
    }
}

public sealed class ApprovalWorkflowService(
    IOperatorGovernanceRepository repository,
    IOperatorPermissionService permissions,
    IOperatorContext context,
    IOperatorAuditService audit,
    IIntradayRepository intradayRepository,
    IRiskControlService riskControlService,
    IExceptionCaseService exceptionCaseService,
    IClock clock,
    GovernanceOptions options) : IApprovalWorkflowService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ApprovalRequest> CreateApprovalRequestAsync(CreateApprovalRequestRequest request, CancellationToken cancellationToken)
    {
        RequireText(request.Reason, "A reason is required to request approval.");
        await permissions.RequirePermissionAsync(PermissionForRequestType(request.Type), cancellationToken);
        var operatorUser = await RequireCurrentOperatorAsync(cancellationToken);
        var now = clock.UtcNow;
        var approval = new ApprovalRequest(
            ApprovalRequestId.New(),
            request.Type,
            ApprovalRequestStatus.Pending,
            operatorUser.OperatorId,
            operatorUser.DisplayName,
            now,
            request.RequiredApproverRole,
            request.EntityType,
            request.EntityId,
            request.Reason.Trim(),
            Serialize(request.Payload) ?? "{}",
            Serialize(request.Before),
            Serialize(request.After),
            context.CorrelationId,
            now.AddMinutes(Math.Max(1, options.ApprovalExpiryMinutes)),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            now,
            null);
        await repository.AddApprovalRequestAsync(approval, cancellationToken);
        await audit.RecordAsync(new OperatorAuditRecordRequest(
            OperatorAuditEventType.ApprovalRequestCreated,
            OperatorAuditSeverity.Warning,
            OperatorAuditResult.Started,
            "ApprovalWorkflowService",
            "Sensitive action approval request created.",
            "ApprovalRequest",
            approval.Id.Value.ToString("D"),
            approval.Reason,
            Metadata: new { approval.Type, approval.EntityType, approval.EntityId, approval.RequestedByOperatorId }),
            cancellationToken);
        await audit.RecordAsync(new OperatorAuditRecordRequest(
            OperatorAuditEventType.SensitiveActionApprovalRequired,
            OperatorAuditSeverity.Warning,
            OperatorAuditResult.Blocked,
            "ApprovalWorkflowService",
            "Sensitive action is pending four-eyes approval.",
            approval.EntityType,
            approval.EntityId,
            approval.Reason,
            Metadata: new { approvalRequestId = approval.Id.Value, approval.Type }),
            cancellationToken);
        return approval;
    }

    public Task<IReadOnlyList<ApprovalRequest>> GetApprovalRequestsAsync(ApprovalRequestFilter filter, CancellationToken cancellationToken)
        => repository.GetApprovalRequestsAsync(filter, cancellationToken);

    public Task<ApprovalRequest?> GetApprovalRequestAsync(ApprovalRequestId id, CancellationToken cancellationToken)
        => repository.GetApprovalRequestAsync(id, cancellationToken);

    public Task<IReadOnlyList<ApprovalDecision>> GetDecisionsAsync(ApprovalRequestId id, CancellationToken cancellationToken)
        => repository.GetApprovalDecisionsAsync(id, cancellationToken);

    public async Task<ApprovalRequest> ApproveAsync(ApprovalRequestId id, string reason, CancellationToken cancellationToken)
    {
        RequireText(reason, "A reason is required to approve an approval request.");
        await permissions.RequirePermissionAsync(OperatorPermission.ManageApprovals, cancellationToken);
        var current = await RequireCurrentOperatorAsync(cancellationToken);
        var request = await RequireRequestAsync(id, cancellationToken);
        EnsurePending(request, clock.UtcNow);
        if (string.Equals(request.RequestedByOperatorId, current.OperatorId, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainRuleViolationException("Requester cannot approve their own approval request.");
        }
        var roles = await permissions.GetRolesAsync(current.Id, cancellationToken);
        if (!roles.Contains(OperatorRole.Admin) && !roles.Contains(request.RequiredApproverRole))
        {
            throw new DomainRuleViolationException($"Approval requires role '{request.RequiredApproverRole}'.");
        }
        var now = clock.UtcNow;
        var updated = request with { Status = ApprovalRequestStatus.Approved, ApprovedAtUtc = now, ApprovedByOperatorId = current.OperatorId, UpdatedAtUtc = now };
        await repository.UpdateApprovalRequestAsync(updated, cancellationToken);
        await repository.AddApprovalDecisionAsync(new ApprovalDecision(ApprovalDecisionId.New(), id, ApprovalDecisionType.Approved, current.OperatorId, current.DisplayName, reason.Trim(), now, context.CorrelationId), cancellationToken);
        await AuditApprovalAsync(OperatorAuditEventType.ApprovalRequestApproved, updated, reason, OperatorAuditResult.Succeeded, cancellationToken);
        return updated;
    }

    public async Task<ApprovalRequest> RejectAsync(ApprovalRequestId id, string reason, CancellationToken cancellationToken)
    {
        RequireText(reason, "A reason is required to reject an approval request.");
        await permissions.RequirePermissionAsync(OperatorPermission.ManageApprovals, cancellationToken);
        var current = await RequireCurrentOperatorAsync(cancellationToken);
        var request = await RequireRequestAsync(id, cancellationToken);
        EnsurePending(request, clock.UtcNow);
        var now = clock.UtcNow;
        var updated = request with { Status = ApprovalRequestStatus.Rejected, RejectedAtUtc = now, RejectedByOperatorId = current.OperatorId, ResultMessage = reason.Trim(), UpdatedAtUtc = now };
        await repository.UpdateApprovalRequestAsync(updated, cancellationToken);
        await repository.AddApprovalDecisionAsync(new ApprovalDecision(ApprovalDecisionId.New(), id, ApprovalDecisionType.Rejected, current.OperatorId, current.DisplayName, reason.Trim(), now, context.CorrelationId), cancellationToken);
        await AuditApprovalAsync(OperatorAuditEventType.ApprovalRequestRejected, updated, reason, OperatorAuditResult.Blocked, cancellationToken);
        return updated;
    }

    public async Task<ApprovalRequest> CancelAsync(ApprovalRequestId id, string reason, CancellationToken cancellationToken)
    {
        RequireText(reason, "A reason is required to cancel an approval request.");
        var current = await RequireCurrentOperatorAsync(cancellationToken);
        var request = await RequireRequestAsync(id, cancellationToken);
        EnsurePending(request, clock.UtcNow);
        var roles = await permissions.GetRolesAsync(current.Id, cancellationToken);
        if (!roles.Contains(OperatorRole.Admin) && !string.Equals(request.RequestedByOperatorId, current.OperatorId, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainRuleViolationException("Only the requester or admin can cancel an approval request.");
        }
        var updated = request with { Status = ApprovalRequestStatus.Cancelled, ResultMessage = reason.Trim(), UpdatedAtUtc = clock.UtcNow };
        await repository.UpdateApprovalRequestAsync(updated, cancellationToken);
        await AuditApprovalAsync(OperatorAuditEventType.ApprovalRequestCancelled, updated, reason, OperatorAuditResult.Blocked, cancellationToken);
        return updated;
    }

    public async Task<GovernedActionResult> ExecuteApprovedAsync(ApprovalRequestId id, CancellationToken cancellationToken)
    {
        await permissions.RequirePermissionAsync(OperatorPermission.ManageApprovals, cancellationToken);
        var current = await RequireCurrentOperatorAsync(cancellationToken);
        var request = await RequireRequestAsync(id, cancellationToken);
        if (request.Status != ApprovalRequestStatus.Approved)
        {
            throw new DomainRuleViolationException("Only approved requests can be executed.");
        }
        if (request.ExecutedAtUtc is not null)
        {
            throw new DomainRuleViolationException("Approval request has already been executed.");
        }
        string? resultEntityId = null;
        var message = "Approved request executed.";
        switch (request.Type)
        {
            case ApprovalRequestType.ActivateRiskLimitSet:
                var activated = await riskControlService.ActivateRiskLimitSetAsync(Guid.Parse(request.EntityId), request.Reason, cancellationToken);
                resultEntityId = activated.Id.ToString("D");
                message = $"Activated risk set {activated.Name} v{activated.Version}.";
                break;
            case ApprovalRequestType.RetireRiskLimitSet:
                var retired = await riskControlService.RetireRiskLimitSetAsync(Guid.Parse(request.EntityId), request.Reason, cancellationToken);
                resultEntityId = retired.Id.ToString("D");
                message = $"Retired risk set {retired.Name} v{retired.Version}.";
                break;
            case ApprovalRequestType.ClearKillSwitch:
                await intradayRepository.SetKillSwitchAsync(false, request.Reason, cancellationToken);
                resultEntityId = "global";
                message = "Kill switch cleared after approval.";
                break;
            case ApprovalRequestType.WaiveException:
                resultEntityId = (await exceptionCaseService.WaiveAsync(new ExceptionCaseId(Guid.Parse(request.EntityId)), request.Reason, cancellationToken)).Id.Value.ToString("D");
                message = "Exception case waived after approval.";
                break;
            case ApprovalRequestType.MarkExceptionFalsePositive:
                resultEntityId = (await exceptionCaseService.MarkFalsePositiveAsync(new ExceptionCaseId(Guid.Parse(request.EntityId)), request.Reason, cancellationToken)).Id.Value.ToString("D");
                message = "Exception case marked false positive after approval.";
                break;
            case ApprovalRequestType.ResolveCriticalException:
                resultEntityId = (await exceptionCaseService.ResolveAsync(new ExceptionCaseId(Guid.Parse(request.EntityId)), request.Reason, cancellationToken)).Id.Value.ToString("D");
                message = "Exception case resolved after approval.";
                break;
            default:
                throw new DomainRuleViolationException($"Execution is not implemented for approval type '{request.Type}'.");
        }
        var now = clock.UtcNow;
        var executed = request with { Status = ApprovalRequestStatus.Executed, ExecutedAtUtc = now, ExecutedByOperatorId = current.OperatorId, ResultMessage = message, UpdatedAtUtc = now };
        await repository.UpdateApprovalRequestAsync(executed, cancellationToken);
        await AuditApprovalAsync(OperatorAuditEventType.ApprovalRequestExecuted, executed, request.Reason, OperatorAuditResult.Succeeded, cancellationToken);
        return new GovernedActionResult(true, false, id, executed.Status.ToString(), message, request.EntityId, resultEntityId, context.CorrelationId);
    }

    private async Task<OperatorUser> RequireCurrentOperatorAsync(CancellationToken cancellationToken)
    {
        var current = await permissions.GetCurrentOperatorAsync(cancellationToken);
        if (current is null || !current.IsEnabled)
        {
            throw new DomainRuleViolationException($"Operator '{context.Current.ActorId}' is unknown or disabled for sensitive actions.");
        }
        return current;
    }

    private async Task<ApprovalRequest> RequireRequestAsync(ApprovalRequestId id, CancellationToken cancellationToken)
        => await repository.GetApprovalRequestAsync(id, cancellationToken) ?? throw new DomainRuleViolationException("Approval request was not found.");

    private static void EnsurePending(ApprovalRequest request, DateTimeOffset now)
    {
        if (request.Status != ApprovalRequestStatus.Pending) throw new DomainRuleViolationException($"Approval request is {request.Status} and cannot be decided.");
        if (request.ExpiresAtUtc is not null && request.ExpiresAtUtc < now) throw new DomainRuleViolationException("Approval request has expired.");
    }

    private Task AuditApprovalAsync(OperatorAuditEventType eventType, ApprovalRequest request, string reason, OperatorAuditResult result, CancellationToken cancellationToken)
        => audit.RecordAsync(new OperatorAuditRecordRequest(eventType, OperatorAuditSeverity.Warning, result, "ApprovalWorkflowService", $"Approval request {request.Status}.", "ApprovalRequest", request.Id.Value.ToString("D"), reason, Metadata: new { request.Type, request.EntityType, request.EntityId, request.RequestedByOperatorId, request.ApprovedByOperatorId, request.RejectedByOperatorId, request.ExecutedByOperatorId }), cancellationToken);

    private static OperatorPermission PermissionForRequestType(ApprovalRequestType type)
        => type switch
        {
            ApprovalRequestType.ActivateRiskLimitSet => OperatorPermission.ActivateRiskConfig,
            ApprovalRequestType.RetireRiskLimitSet => OperatorPermission.RetireRiskConfig,
            ApprovalRequestType.ClearKillSwitch => OperatorPermission.ClearKillSwitch,
            ApprovalRequestType.WaiveException => OperatorPermission.WaiveExceptions,
            ApprovalRequestType.MarkExceptionFalsePositive => OperatorPermission.ResolveExceptions,
            ApprovalRequestType.ResolveCriticalException => OperatorPermission.ResolveExceptions,
            _ => OperatorPermission.Admin
        };

    private static string? Serialize(object? value)
        => OperatorAuditService.SerializeSanitized(value);

    private static void RequireText(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new DomainRuleViolationException(message);
    }
}

public sealed class InMemoryOperatorGovernanceRepository(PlatformState state) : IOperatorGovernanceRepository
{
    private readonly object _sync = new();

    public Task<IReadOnlyList<OperatorUser>> GetOperatorsAsync(CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult<IReadOnlyList<OperatorUser>>(state.OperatorUsers.OrderBy(x => x.OperatorId).ToList());
    }

    public Task<OperatorUser?> GetOperatorByIdAsync(string operatorId, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult(state.OperatorUsers.FirstOrDefault(x => x.OperatorId.Equals(operatorId, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<IReadOnlyList<OperatorUserRole>> GetRolesAsync(OperatorUserId operatorUserId, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult<IReadOnlyList<OperatorUserRole>>(state.OperatorUserRoles.Where(x => x.OperatorUserId == operatorUserId).ToList());
    }

    public Task UpsertOperatorAsync(OperatorUser user, IReadOnlyList<OperatorUserRole> roles, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            Upsert(state.OperatorUsers, user, x => x.Id == user.Id || x.OperatorId.Equals(user.OperatorId, StringComparison.OrdinalIgnoreCase));
            state.OperatorUserRoles.RemoveAll(x => x.OperatorUserId == user.Id);
            state.OperatorUserRoles.AddRange(roles);
        }
        return Task.CompletedTask;
    }

    public Task AddApprovalRequestAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        lock (_sync) state.ApprovalRequests.Add(request);
        return Task.CompletedTask;
    }

    public Task UpdateApprovalRequestAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        lock (_sync) Upsert(state.ApprovalRequests, request, x => x.Id == request.Id);
        return Task.CompletedTask;
    }

    public Task AddApprovalDecisionAsync(ApprovalDecision decision, CancellationToken cancellationToken)
    {
        lock (_sync) state.ApprovalDecisions.Add(decision);
        return Task.CompletedTask;
    }

    public Task<ApprovalRequest?> GetApprovalRequestAsync(ApprovalRequestId id, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult(state.ApprovalRequests.FirstOrDefault(x => x.Id == id));
    }

    public Task<IReadOnlyList<ApprovalRequest>> GetApprovalRequestsAsync(ApprovalRequestFilter filter, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult<IReadOnlyList<ApprovalRequest>>(Apply(filter).ToList());
    }

    public Task<IReadOnlyList<ApprovalDecision>> GetApprovalDecisionsAsync(ApprovalRequestId id, CancellationToken cancellationToken)
    {
        lock (_sync) return Task.FromResult<IReadOnlyList<ApprovalDecision>>(state.ApprovalDecisions.Where(x => x.ApprovalRequestId == id).OrderBy(x => x.DecidedAtUtc).ToList());
    }

    private IEnumerable<ApprovalRequest> Apply(ApprovalRequestFilter filter)
    {
        IEnumerable<ApprovalRequest> query = state.ApprovalRequests;
        if (filter.Status is not null) query = query.Where(x => x.Status == filter.Status);
        if (filter.Type is not null) query = query.Where(x => x.Type == filter.Type);
        if (!string.IsNullOrWhiteSpace(filter.RequestedBy)) query = query.Where(x => x.RequestedByOperatorId.Equals(filter.RequestedBy, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.EntityType)) query = query.Where(x => x.EntityType.Equals(filter.EntityType, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.EntityId)) query = query.Where(x => x.EntityId.Equals(filter.EntityId, StringComparison.OrdinalIgnoreCase));
        return query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(filter.Limit, 1, 500));
    }

    private static void Upsert<T>(List<T> rows, T row, Func<T, bool> predicate)
    {
        var index = rows.FindIndex(x => predicate(x));
        if (index >= 0) rows[index] = row;
        else rows.Add(row);
    }
}
