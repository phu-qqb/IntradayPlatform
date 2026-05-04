using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Integration;

public sealed class GovernanceApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 04, 09, 30, 00, TimeSpan.Zero);

    [Fact]
    public async Task Governance_api_flow_requires_checker_and_executes_risk_activation_once()
    {
        var state = SeedData.Create(Now);
        await using var factory = CreateInMemoryFactory(state);
        using var client = factory.CreateClient();

        var active = await GetAsync<RiskLimitSetDto>(client, "/risk/limit-sets/active?fundCode=QQ_MASTER&modelName=IntradayFxModel", "local-risk");
        var draft = await PostAsync<RiskLimitSetDto>(client, $"/risk/limit-sets/{active.Id}/clone", "local-risk", new { reason = "API governance test draft." });
        Assert.Equal("Draft", draft.Status);

        var governed = await PostAsync<GovernedActionResultDto>(client, $"/risk/limit-sets/{draft.Id}/activate", "local-risk", new { reason = "API governance test activation." });
        Assert.True(governed.ApprovalRequired);
        Assert.False(governed.Executed);
        Assert.False(string.IsNullOrWhiteSpace(governed.ApprovalRequestId));

        var selfApprove = await SendJsonAsync(client, HttpMethod.Post, $"/approvals/{governed.ApprovalRequestId}/approve", "local-risk", new { reason = "Self approval should fail." });
        Assert.Equal(HttpStatusCode.BadRequest, selfApprove.StatusCode);

        var approved = await PostAsync<ApprovalRequestDto>(client, $"/approvals/{governed.ApprovalRequestId}/approve", "local-approver", new { reason = "Checker approval." });
        Assert.Equal("Approved", approved.Status);

        var executed = await PostAsync<GovernedActionResultDto>(client, $"/approvals/{governed.ApprovalRequestId}/execute", "local-approver", null);
        Assert.True(executed.Executed);

        var currentActive = await GetAsync<RiskLimitSetDto>(client, "/risk/limit-sets/active?fundCode=QQ_MASTER&modelName=IntradayFxModel", "local-risk");
        Assert.Equal(draft.Id, currentActive.Id);

        var secondExecute = await SendJsonAsync(client, HttpMethod.Post, $"/approvals/{governed.ApprovalRequestId}/execute", "local-approver", null);
        Assert.Equal(HttpStatusCode.BadRequest, secondExecute.StatusCode);

        var approvals = await GetAsync<ApprovalRequestDto[]>(client, "/approvals?limit=100", "local-admin");
        Assert.Contains(approvals, x => x.Id == governed.ApprovalRequestId && x.Status == "Executed");
        var audit = await GetAsync<OperatorAuditEventDto[]>(client, "/audit/events?limit=100", "local-admin");
        Assert.Contains(audit, x => x.EventType == "PermissionDenied");
        Assert.Contains(audit, x => x.EventType == "ApprovalRequestExecuted");
        Assert.Contains(audit, x => x.EventType == "RiskLimitSetActivated");
    }

    [Fact]
    public async Task Governance_api_flow_requires_approval_for_kill_switch_clear()
    {
        var state = SeedData.Create(Now);
        await using var factory = CreateInMemoryFactory(state);
        using var client = factory.CreateClient();

        var activeKill = await PostAsync<KillSwitchActivationDto>(client, "/admin/kill-switch", "local-operator", new { reason = "API governance test activate kill switch." });
        Assert.True(activeKill.Active);

        var clearRequest = await PostAsync<GovernedActionResultDto>(client, "/admin/kill-switch/clear", "local-risk", new { reason = "API governance test clear request." });
        Assert.True(clearRequest.ApprovalRequired);
        Assert.False(clearRequest.Executed);
        Assert.False(string.IsNullOrWhiteSpace(clearRequest.ApprovalRequestId));

        var stillActive = await GetAsync<KillSwitchDto>(client, "/admin/kill-switch", "local-risk");
        Assert.True(stillActive.IsActive);

        await PostAsync<ApprovalRequestDto>(client, $"/approvals/{clearRequest.ApprovalRequestId}/approve", "local-approver", new { reason = "Checker approval." });
        var executed = await PostAsync<GovernedActionResultDto>(client, $"/approvals/{clearRequest.ApprovalRequestId}/execute", "local-approver", null);
        Assert.True(executed.Executed);

        var cleared = await GetAsync<KillSwitchDto>(client, "/admin/kill-switch", "local-risk");
        Assert.False(cleared.IsActive);
    }

    private static WebApplicationFactory<Program> CreateInMemoryFactory(PlatformState state)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Persistence:Provider"] = "InMemory",
                    ["Safety:AllowExternalConnections"] = "false",
                    ["Safety:AllowLiveTrading"] = "false",
                    ["Safety:RequireFakeExecutionGateway"] = "true",
                    ["Governance:FourEyesEnabled"] = "true"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<PlatformState>();
                services.AddSingleton(state);
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(new FixedClock(Now));
            });
        });

    private static async Task<T> GetAsync<T>(HttpClient client, string path, string operatorId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Operator-Id", operatorId);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string path, string operatorId, object? body)
    {
        using var response = await SendJsonAsync(client, HttpMethod.Post, path, operatorId, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(HttpClient client, HttpMethod method, string path, string operatorId, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Operator-Id", operatorId);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return await client.SendAsync(request);
    }

    private sealed record RiskLimitSetDto(string Id, string Status, bool IsActive);
    private sealed record GovernedActionResultDto(bool Executed, bool ApprovalRequired, string? ApprovalRequestId, string Status, string Message, string EntityId, string? ResultEntityId, string? CorrelationId);
    private sealed record ApprovalRequestDto(string Id, string Type, string Status);
    private sealed record OperatorAuditEventDto(string Id, string EventType);
    private sealed record KillSwitchActivationDto(bool Active, string? Reason);
    private sealed record KillSwitchDto(string Id, bool IsActive, string? Reason, DateTimeOffset UpdatedAtUtc);
}
