using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class OperatorGovernanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 04, 09, 30, 00, TimeSpan.Zero);

    [Fact]
    public async Task Current_operator_resolves_permissions_from_seeded_roles()
    {
        var state = SeedData.Create(Now);
        var permissions = CreatePermissionService(state, "local-admin");

        var current = await permissions.GetCurrentOperatorAsync(CancellationToken.None);
        var operatorPermissions = await permissions.GetPermissionsAsync(current!.Id, CancellationToken.None);

        Assert.Equal("local-admin", current.OperatorId);
        Assert.Contains(OperatorPermission.Admin, operatorPermissions);
        Assert.Contains(OperatorPermission.ManageApprovals, operatorPermissions);
    }

    [Fact]
    public async Task Permission_denial_creates_audit_event()
    {
        var state = SeedData.Create(Now);
        var permissions = CreatePermissionService(state, "local-viewer");

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => permissions.RequirePermissionAsync(OperatorPermission.ClearKillSwitch, CancellationToken.None));

        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.PermissionDenied);
    }

    [Fact]
    public async Task Requester_cannot_approve_own_request()
    {
        var state = SeedData.Create(Now);
        var workflow = CreateWorkflow(state, "local-admin");

        var request = await workflow.CreateApprovalRequestAsync(new CreateApprovalRequestRequest(
            ApprovalRequestType.ActivateRiskLimitSet,
            "RiskLimitSet",
            state.RiskLimitSets.Single(x => x.IsActive).Id.ToString("D"),
            "Need checker approval.",
            new { action = "activate" }),
            CancellationToken.None);

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => workflow.ApproveAsync(request.Id, "Same user approval is blocked.", CancellationToken.None));
    }

    [Fact]
    public async Task Approver_can_approve_and_execution_is_once_only()
    {
        var state = SeedData.Create(Now);
        var riskWorkflow = CreateWorkflow(state, "local-risk");
        var active = state.RiskLimitSets.Single(x => x.IsActive);
        var draft = await CreateRiskService(state, "local-risk").CloneRiskLimitSetAsync(active.Id, "Prepare draft.", CancellationToken.None);
        var request = await riskWorkflow.CreateApprovalRequestAsync(new CreateApprovalRequestRequest(
            ApprovalRequestType.ActivateRiskLimitSet,
            "RiskLimitSet",
            draft.Id.ToString("D"),
            "Activate draft profile.",
            new { action = "activate" }),
            CancellationToken.None);

        var approverWorkflow = CreateWorkflow(state, "local-approver");
        var approved = await approverWorkflow.ApproveAsync(request.Id, "Checker approval.", CancellationToken.None);
        var executed = await approverWorkflow.ExecuteApprovedAsync(request.Id, CancellationToken.None);

        Assert.Equal(ApprovalRequestStatus.Approved, approved.Status);
        Assert.True(executed.Executed);
        var priorActive = state.RiskLimitSets.Single(x => x.Id == active.Id);
        var activatedDraft = state.RiskLimitSets.Single(x => x.Id == draft.Id);
        Assert.False(priorActive.IsActive);
        Assert.Equal(RiskLimitSetStatus.Retired, priorActive.Status);
        Assert.True(activatedDraft.IsActive);
        Assert.Equal(RiskLimitSetStatus.Active, activatedDraft.Status);
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.ApprovalRequestExecuted);
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.RiskLimitSetActivated);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => approverWorkflow.ExecuteApprovedAsync(request.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Rejected_request_cannot_execute()
    {
        var state = SeedData.Create(Now);
        var workflow = CreateWorkflow(state, "local-admin");
        var request = await workflow.CreateApprovalRequestAsync(new CreateApprovalRequestRequest(
            ApprovalRequestType.ClearKillSwitch,
            "KillSwitch",
            "global",
            "Clear after test.",
            new { action = "clearKillSwitch" }),
            CancellationToken.None);

        var approverWorkflow = CreateWorkflow(state, "local-approver");
        await approverWorkflow.RejectAsync(request.Id, "Not enough context.", CancellationToken.None);

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => approverWorkflow.ExecuteApprovedAsync(request.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Risk_manager_can_request_governed_kill_switch_clear_and_checker_executes()
    {
        var state = SeedData.Create(Now);
        await new InMemoryIntradayRepository(state).SetKillSwitchAsync(true, "Test active kill switch.", CancellationToken.None);
        var workflow = CreateWorkflow(state, "local-risk");

        var request = await workflow.CreateApprovalRequestAsync(new CreateApprovalRequestRequest(
            ApprovalRequestType.ClearKillSwitch,
            "KillSwitch",
            "global",
            "Clear after checker approval.",
            new { action = "clearKillSwitch" }),
            CancellationToken.None);

        Assert.Equal(ApprovalRequestStatus.Pending, request.Status);
        Assert.True(state.KillSwitch.IsActive);

        var approverWorkflow = CreateWorkflow(state, "local-approver");
        await approverWorkflow.ApproveAsync(request.Id, "Checker approval.", CancellationToken.None);
        var executed = await approverWorkflow.ExecuteApprovedAsync(request.Id, CancellationToken.None);

        Assert.True(executed.Executed);
        Assert.False(state.KillSwitch.IsActive);
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.ApprovalRequestCreated);
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.ApprovalRequestApproved);
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.ApprovalRequestExecuted);
    }

    private static IOperatorPermissionService CreatePermissionService(PlatformState state, string operatorId)
    {
        var context = new StaticOperatorContext(OperatorAuditActorType.Operator, operatorId, operatorId, "corr-test", "request-test");
        return new OperatorPermissionService(
            new InMemoryOperatorGovernanceRepository(state),
            context,
            new OperatorAuditService(new InMemoryOperatorAuditRepository(state), context, new FixedClock(Now)));
    }

    private static IApprovalWorkflowService CreateWorkflow(PlatformState state, string operatorId)
    {
        var context = new StaticOperatorContext(OperatorAuditActorType.Operator, operatorId, operatorId, "corr-test", "request-test");
        var clock = new FixedClock(Now);
        var audit = new OperatorAuditService(new InMemoryOperatorAuditRepository(state), context, clock);
        var intraday = new InMemoryIntradayRepository(state);
        return new ApprovalWorkflowService(
            new InMemoryOperatorGovernanceRepository(state),
            new OperatorPermissionService(new InMemoryOperatorGovernanceRepository(state), context, audit),
            context,
            audit,
            intraday,
            new RiskControlService(intraday, audit, context, clock),
            new ExceptionCaseService(new InMemoryExceptionCaseRepository(state), audit, context, clock, intraday),
            clock,
            new GovernanceOptions());
    }

    private static IRiskControlService CreateRiskService(PlatformState state, string operatorId)
    {
        var context = new StaticOperatorContext(OperatorAuditActorType.Operator, operatorId, operatorId, "corr-test", "request-test");
        var clock = new FixedClock(Now);
        var audit = new OperatorAuditService(new InMemoryOperatorAuditRepository(state), context, clock);
        return new RiskControlService(new InMemoryIntradayRepository(state), audit, context, clock);
    }
}
