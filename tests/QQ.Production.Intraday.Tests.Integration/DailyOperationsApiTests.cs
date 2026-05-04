using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Integration;

public sealed class DailyOperationsApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 04, 09, 30, 00, TimeSpan.Zero);

    [Fact]
    public async Task Ops_job_api_records_reference_job_steps_and_audit_events()
    {
        var state = SeedData.Create(Now);
        await using var factory = CreateInMemoryFactory(state);
        using var client = factory.CreateClient();

        var badRequest = await SendJsonAsync(client, HttpMethod.Post, "/ops/jobs/run", "local-admin", new { jobType = "ReferenceDataIntegrityCheck", reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, badRequest.StatusCode);

        var run = await PostAsync<OperationalJobRunDto>(client, "/ops/jobs/run", "local-admin", new { jobType = "ReferenceDataIntegrityCheck", reason = "Ops API test reference check.", input = new { } });
        Assert.Equal("ReferenceDataIntegrityCheck", run.JobType);
        Assert.Equal("Succeeded", run.Status);
        Assert.False(string.IsNullOrWhiteSpace(run.Id));

        var steps = await GetAsync<OperationalJobStepDto[]>(client, $"/ops/jobs/runs/{run.Id}/steps", "local-admin");
        Assert.Contains(steps, x => x.StepName.Contains("reference data", StringComparison.OrdinalIgnoreCase) && x.Status == "Succeeded");

        var events = await GetAsync<OperationalJobRunEventDto[]>(client, $"/ops/jobs/runs/{run.Id}/events", "local-admin");
        Assert.Contains(events, x => x.Message.Contains("started", StringComparison.OrdinalIgnoreCase));

        var runs = await GetAsync<OperationalJobRunDto[]>(client, "/ops/jobs/runs?limit=20", "local-admin");
        Assert.Contains(runs, x => x.Id == run.Id);

        var summary = await GetAsync<DailyOperationsSummaryDto>(client, "/ops/daily-summary?date=2026-05-04", "local-admin");
        Assert.NotNull(summary.LatestReferenceIntegrity);
        Assert.Equal(run.Id, summary.LatestReferenceIntegrity!.Id);

        var checklist = await GetAsync<DailyChecklistItemDto[]>(client, "/ops/daily-checklist?date=2026-05-04", "local-admin");
        Assert.Contains(checklist, x => x.Name == "Reference data clean" && x.Status == "Complete");

        var retry = await PostAsync<OperationalJobRunDto>(client, $"/ops/jobs/runs/{run.Id}/retry", "local-admin", new { reason = "Retry reference data check in ops API test." });
        Assert.Equal(run.Id, retry.RetryOfJobRunId);
        Assert.NotEqual(run.Id, retry.Id);

        var audit = await GetAsync<OperatorAuditEventDto[]>(client, "/audit/events?limit=100", "local-admin");
        Assert.Contains(audit, x => x.EventType == "OperationalJobStarted");
        Assert.Contains(audit, x => x.EventType == "OperationalJobSucceeded");
        Assert.Contains(audit, x => x.EventType == "OperationalJobRetried");
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

    private sealed record OperationalJobRunDto(string Id, string JobType, string Status, string? RetryOfJobRunId);
    private sealed record OperationalJobStepDto(string StepName, string Status);
    private sealed record OperationalJobRunEventDto(string Message);
    private sealed record DailyOperationsSummaryDto(OperationalJobRunDto? LatestReferenceIntegrity);
    private sealed record DailyChecklistItemDto(string Name, string Status);
    private sealed record OperatorAuditEventDto(string EventType);
}
