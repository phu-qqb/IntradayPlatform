using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class OperatorAuditTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 02, 10, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Audit_event_persists_and_queries_by_entity_and_correlation()
    {
        var state = new PlatformState();
        var service = CreateService(state, "corr-1");

        var recorded = await service.RecordSucceededAsync(
            OperatorAuditEventType.ModelRunCreated,
            "UnitTest",
            "Created model run.",
            "ModelRun",
            "model-1",
            new { count = 1 },
            CancellationToken.None);

        Assert.NotNull(recorded);
        Assert.Single(state.OperatorAuditEvents);
        var byEntity = await service.GetByEntityAsync("ModelRun", "model-1", 10, CancellationToken.None);
        var byCorrelation = await service.GetByCorrelationIdAsync("corr-1", 10, CancellationToken.None);
        Assert.Single(byEntity);
        Assert.Single(byCorrelation);
        Assert.Equal(OperatorAuditResult.Succeeded, byEntity[0].Result);
    }

    [Fact]
    public async Task Audit_events_are_append_only()
    {
        var state = new PlatformState();
        var service = CreateService(state, "corr-2");

        await service.RecordSucceededAsync(OperatorAuditEventType.KillSwitchActivated, "UnitTest", "Activated.", "KillSwitch", "global", cancellationToken: CancellationToken.None);
        await service.RecordSucceededAsync(OperatorAuditEventType.KillSwitchCleared, "UnitTest", "Cleared.", "KillSwitch", "global", cancellationToken: CancellationToken.None);

        Assert.Equal(2, state.OperatorAuditEvents.Count);
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.KillSwitchActivated);
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.KillSwitchCleared);
    }

    [Fact]
    public void Audit_sanitizer_masks_secret_metadata()
    {
        var json = OperatorAuditService.SerializeSanitized(new
        {
            user = "demo",
            password = "do-not-store",
            nested = new
            {
                apiKey = "abc",
                token = "def",
                value = 42
            }
        });

        Assert.NotNull(json);
        Assert.DoesNotContain("do-not-store", json);
        Assert.DoesNotContain("abc", json);
        Assert.DoesNotContain("def", json);
        Assert.Contains("\"password\":\"***\"", json);
        Assert.Contains("\"apiKey\":\"***\"", json);
        Assert.Contains("\"token\":\"***\"", json);
    }

    private static IOperatorAuditService CreateService(PlatformState state, string correlationId)
        => new OperatorAuditService(
            new InMemoryOperatorAuditRepository(state),
            new StaticOperatorContext(OperatorAuditActorType.Operator, "local-dev", "Local Dev", correlationId, "request-1"),
            new FixedClock(Now));
}
