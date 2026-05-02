using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExceptionCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 02, 11, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Creating_manual_case_persists_action_and_audit()
    {
        var state = SeedData.Create(Now);
        var service = CreateService(state);

        var exceptionCase = await service.CreateManualCaseAsync(new(
            ExceptionCaseSeverity.Warning,
            ExceptionCaseType.SystemHealth,
            ExceptionCaseSource.Operator,
            "Investigate local warning",
            "Synthetic operator case.",
            Metadata: new { password = "never-store" }),
            CancellationToken.None);

        Assert.Single(state.ExceptionCases);
        Assert.Single(state.ExceptionCaseActions);
        Assert.Equal(ExceptionCaseStatus.Open, exceptionCase.Status);
        Assert.DoesNotContain("never-store", exceptionCase.MetadataJson);
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.ExceptionCaseCreated);
    }

    [Fact]
    public async Task Duplicate_source_break_does_not_create_duplicate_case()
    {
        var state = SeedData.Create(Now);
        var service = CreateService(state);
        var run = new ReconciliationRun(Guid.NewGuid(), ModelRunId.New(), ReconciliationPhase.PreTrade, Now, true);
        var reconciliationBreak = new ReconciliationBreak(Guid.NewGuid(), run.Id, ReconciliationBreakType.InternalBrokerPositionMismatch, ReconciliationBreakSeverity.Blocking, ReconciliationBreakStatus.Open, state.Instruments[0].Id, "Mismatch.");

        var first = await service.CreateOrUpdateFromReconciliationBreakAsync(run, reconciliationBreak, CancellationToken.None);
        var second = await service.CreateOrUpdateFromReconciliationBreakAsync(run, reconciliationBreak, CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Id, second.Id);
        Assert.Single(state.ExceptionCases);
        Assert.Single(state.ExceptionCaseLinks);
    }

    [Fact]
    public async Task Info_break_does_not_create_case_by_default()
    {
        var state = SeedData.Create(Now);
        var service = CreateService(state);
        var run = new ReconciliationRun(Guid.NewGuid(), ModelRunId.New(), ReconciliationPhase.PostTrade, Now, false);
        var reconciliationBreak = new ReconciliationBreak(Guid.NewGuid(), run.Id, ReconciliationBreakType.OrderAckedButNoFill, ReconciliationBreakSeverity.Info, ReconciliationBreakStatus.Open, state.Instruments[0].Id, "Informational.");

        var exceptionCase = await service.CreateOrUpdateFromReconciliationBreakAsync(run, reconciliationBreak, CancellationToken.None);

        Assert.Null(exceptionCase);
        Assert.Empty(state.ExceptionCases);
    }

    [Fact]
    public async Task Status_transitions_and_note_are_recorded()
    {
        var state = SeedData.Create(Now);
        var service = CreateService(state);
        var exceptionCase = await service.CreateManualCaseAsync(new(ExceptionCaseSeverity.Blocking, ExceptionCaseType.RiskBlock, ExceptionCaseSource.RiskEngine, "Risk block", "Risk blocked."), CancellationToken.None);

        exceptionCase = await service.AcknowledgeAsync(exceptionCase.Id, "Seen by operator.", CancellationToken.None);
        exceptionCase = await service.MarkInvestigatingAsync(exceptionCase.Id, "Checking source records.", CancellationToken.None);
        var note = await service.AddNoteAsync(exceptionCase.Id, "Broker report is under review.", CancellationToken.None);
        exceptionCase = await service.ResolveAsync(exceptionCase.Id, "Confirmed resolved after report correction.", CancellationToken.None);

        Assert.Equal(ExceptionCaseStatus.Resolved, exceptionCase.Status);
        Assert.Equal("Confirmed resolved after report correction.", exceptionCase.ResolutionReason);
        Assert.Equal("Broker report is under review.", note.Note);
        Assert.Equal(5, state.ExceptionCaseActions.Count);
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.ExceptionCaseResolved);
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.ExceptionCaseNoteAdded);
    }

    [Fact]
    public async Task Resolve_waive_and_false_positive_require_reason()
    {
        var state = SeedData.Create(Now);
        var service = CreateService(state);
        var exceptionCase = await service.CreateManualCaseAsync(new(ExceptionCaseSeverity.Warning, ExceptionCaseType.Other, ExceptionCaseSource.Operator, "Needs reason", "Reason required."), CancellationToken.None);

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => service.ResolveAsync(exceptionCase.Id, "", CancellationToken.None));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => service.WaiveAsync(exceptionCase.Id, "", CancellationToken.None));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => service.MarkFalsePositiveAsync(exceptionCase.Id, "", CancellationToken.None));
    }

    [Fact]
    public async Task Eod_blocking_break_creates_case()
    {
        var state = SeedData.Create(Now);
        var service = CreateService(state);
        var run = new EodReconciliationRun(Guid.NewGuid(), DateOnly.FromDateTime(Now.UtcDateTime), state.Venues[0].Id, state.BrokerAccounts[0].Id, Now, true);
        var reconciliationBreak = new EodReconciliationBreak(Guid.NewGuid(), run.Id, ReconciliationBreakType.QuantityMismatch, ReconciliationBreakSeverity.Blocking, ReconciliationBreakStatus.Open, state.Instruments[0].Id, "Quantity mismatch.", "exec-1", "fill-1", Now);

        var exceptionCase = await service.CreateOrUpdateFromEodBreakAsync(run, reconciliationBreak, CancellationToken.None);

        Assert.NotNull(exceptionCase);
        Assert.Equal(ExceptionCaseSource.EodReconciliation, exceptionCase.Source);
        Assert.Equal(ExceptionCaseType.QuantityMismatch, exceptionCase.Type);
    }

    [Fact]
    public async Task Query_filters_by_status_and_assignee()
    {
        var state = SeedData.Create(Now);
        var service = CreateService(state);
        var exceptionCase = await service.CreateManualCaseAsync(new(ExceptionCaseSeverity.Critical, ExceptionCaseType.SystemHealth, ExceptionCaseSource.SystemHealth, "Critical", "Critical.", AssignedTo: "ops"), CancellationToken.None);

        var cases = await service.GetCasesAsync(new ExceptionCaseFilter(100, Status: ExceptionCaseStatus.Open, AssignedTo: "ops"), CancellationToken.None);

        Assert.Single(cases);
        Assert.Equal(exceptionCase.Id, cases[0].Id);
    }

    private static IExceptionCaseService CreateService(PlatformState state)
    {
        var context = new StaticOperatorContext(OperatorAuditActorType.Operator, "local-dev", "Local Dev", "corr-ex", "req-ex");
        var clock = new FixedClock(Now);
        var audit = new OperatorAuditService(new InMemoryOperatorAuditRepository(state), context, clock);
        return new ExceptionCaseService(new InMemoryExceptionCaseRepository(state), audit, context, clock, new InMemoryIntradayRepository(state));
    }
}
