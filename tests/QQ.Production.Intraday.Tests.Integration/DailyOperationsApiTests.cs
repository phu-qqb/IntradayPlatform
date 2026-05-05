using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

        const string retryReason = "Retry reference data check in ops API test.";
        var retry = await PostAsync<OperationalJobRunDto>(client, $"/ops/jobs/runs/{run.Id}/retry", "local-admin", new { reason = retryReason });
        Assert.Equal(run.Id, retry.RetryOfJobRunId);
        Assert.NotEqual(run.Id, retry.Id);
        Assert.Equal(1, retry.RetryCount);

        var retryBadRequest = await SendJsonAsync(client, HttpMethod.Post, $"/ops/jobs/runs/{run.Id}/retry", "local-admin", new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, retryBadRequest.StatusCode);

        var viewerDenied = await SendJsonAsync(client, HttpMethod.Post, "/ops/jobs/run", "local-viewer", new { jobType = "ReferenceDataIntegrityCheck", reason = "Viewer should not be able to run jobs.", input = new { } });
        Assert.Equal(HttpStatusCode.BadRequest, viewerDenied.StatusCode);

        var audit = await GetAsync<OperatorAuditEventDto[]>(client, "/audit/events?limit=100", "local-admin");
        Assert.Contains(audit, x => x.EventType == "OperationalJobStarted");
        Assert.Contains(audit, x => x.EventType == "OperationalJobSucceeded");
        var retried = Assert.Single(audit, x => x.EventType == "OperationalJobRetried");
        Assert.Equal(run.Id, retried.EntityId);
        Assert.Equal(retryReason, retried.Reason);
        Assert.Contains(run.Id, retried.MetadataJson);
        Assert.Contains(retry.Id, retried.MetadataJson);
        Assert.Contains("\"originalJobRunId\"", retried.MetadataJson);
        Assert.Contains("\"retryJobRunId\"", retried.MetadataJson);
        Assert.Contains(audit, x => x.EventType == "PermissionDenied");
    }

    [Fact]
    public async Task Critical_reference_data_job_failure_creates_exception_case_and_output_summary()
    {
        var state = SeedData.Create(Now);
        state.Venues.Clear();
        await using var factory = CreateInMemoryFactory(state);
        using var client = factory.CreateClient();

        var run = await PostAsync<OperationalJobRunDto>(client, "/ops/jobs/run", "local-admin", new { jobType = "ReferenceDataIntegrityCheck", reason = "Ops API test broken reference data.", input = new { } });
        Assert.Equal("Failed", run.Status);
        Assert.False(string.IsNullOrWhiteSpace(run.ExceptionCaseId));
        Assert.Contains("blockingIssueCount", run.OutputJson);

        var events = await GetAsync<OperationalJobRunEventDto[]>(client, $"/ops/jobs/runs/{run.Id}/events", "local-admin");
        Assert.Contains(events, x => x.Message.Contains("completed with status Failed", StringComparison.OrdinalIgnoreCase));

        var cases = await GetAsync<ExceptionCaseDto[]>(client, "/exceptions?limit=50", "local-admin");
        Assert.Contains(cases, x => x.Id == run.ExceptionCaseId && x.EntityType == "OperationalJobRun" && x.EntityId == run.Id);
    }

    [Fact]
    public async Task Build_market_data_bars_job_output_serializes_and_updates_existing_bar()
    {
        var state = SeedData.Create(Now);
        await using var factory = CreateInMemoryFactory(state);
        using var client = factory.CreateClient();
        await CreateFakeSnapshotsAsync(client, Now.AddMinutes(-14), 4);

        var first = await PostAsync<OperationalJobRunDto>(client, "/ops/jobs/run", "local-admin", new { jobType = "BuildMarketDataBars", reason = "Create latest bar in ops API test.", input = new { } });
        Assert.Equal("Succeeded", first.Status);
        Assert.Contains("\"barBuildStatus\"", first.OutputJson);
        Assert.Contains("\"barsCreated\"", first.OutputJson);
        Assert.DoesNotContain("\"Status\"", first.OutputJson);

        var second = await PostAsync<OperationalJobRunDto>(client, "/ops/jobs/run", "local-admin", new { jobType = "BuildMarketDataBars", reason = "Update latest bar in ops API test.", input = new { } });
        Assert.Equal("Succeeded", second.Status);
        using var output = JsonDocument.Parse(second.OutputJson!);
        Assert.Equal("Succeeded", output.RootElement.GetProperty("jobStatus").GetString());
        Assert.Equal("Completed", output.RootElement.GetProperty("barBuildStatus").GetString());
        Assert.Equal(0, output.RootElement.GetProperty("barsCreated").GetInt32());
        Assert.Equal(1, output.RootElement.GetProperty("barsUpdated").GetInt32());

        var loaded = await GetAsync<OperationalJobRunDto>(client, $"/ops/jobs/runs/{second.Id}", "local-admin");
        Assert.Equal(second.Id, loaded.Id);
        Assert.Equal("BuildMarketDataBars", loaded.JobType);

        var runs = await GetAsync<OperationalJobRunDto[]>(client, "/ops/jobs/runs?limit=20", "local-admin");
        Assert.Contains(runs, x => x.Id == second.Id);
    }

    [Fact]
    public async Task Runbook_definitions_seed_and_start_of_day_pauses_for_manual_gate()
    {
        var state = SeedData.Create(Now);
        await using var factory = CreateInMemoryFactory(state);
        using var client = factory.CreateClient();

        var definitions = await GetAsync<OperationalRunbookDefinitionDto[]>(client, "/ops/runbooks/definitions", "local-admin");
        Assert.Contains(definitions, x => x.RunbookType == "StartOfDay");
        Assert.Contains(definitions, x => x.RunbookType == "IntradayCycle");
        Assert.Contains(definitions, x => x.RunbookType == "EndOfDay");

        var startOfDay = definitions.Single(x => x.RunbookType == "StartOfDay");
        var definitionDetail = await GetAsync<RunbookDefinitionDetailDto>(client, $"/ops/runbooks/definitions/{startOfDay.Id}", "local-admin");
        Assert.Contains(definitionDetail.Steps, x => x.Name == "Reference Data Integrity Check" && x.JobType == "ReferenceDataIntegrityCheck");
        Assert.Contains(definitionDetail.Steps, x => x.GateType == "ManualConfirmation");

        var badRequest = await SendJsonAsync(client, HttpMethod.Post, "/ops/runbooks/run", "local-admin", new { runbookType = "StartOfDay", reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, badRequest.StatusCode);

        var run = await PostAsync<OperationalRunbookRunDto>(client, "/ops/runbooks/run", "local-admin", new { runbookType = "StartOfDay", reason = "Run SOD in integration test.", input = new { } });
        Assert.Equal("StartOfDay", run.RunbookType);
        Assert.Equal("WaitingForOperator", run.Status);

        var steps = await GetAsync<OperationalRunbookStepRunDto[]>(client, $"/ops/runbooks/runs/{run.Id}/steps", "local-admin");
        Assert.Contains(steps, x => x.JobRunId is not null && x.Name == "Reference Data Integrity Check" && x.Status == "Succeeded");
        var manual = Assert.Single(steps, x => x.Status == "WaitingForOperator");

        var completed = await PostAsync<OperationalRunbookRunDto>(client, $"/ops/runbooks/runs/{run.Id}/complete-manual-step", "local-admin", new { stepRunId = manual.Id, reason = "Operator confirmed SOD in integration test." });
        Assert.Equal("Succeeded", completed.Status);

        var audit = await GetAsync<OperatorAuditEventDto[]>(client, "/audit/events?limit=100", "local-admin");
        Assert.Contains(audit, x => x.EventType == "RunbookStarted");
        Assert.Contains(audit, x => x.EventType == "RunbookWaitingForOperator");
        Assert.Contains(audit, x => x.EventType == "RunbookManualStepCompleted");
        Assert.Contains(audit, x => x.EventType == "RunbookCompleted");
    }

    [Fact]
    public async Task Runbook_permissions_retry_and_scheduler_defaults_are_safe()
    {
        var state = SeedData.Create(Now);
        await using var factory = CreateInMemoryFactory(state);
        using var client = factory.CreateClient();

        var viewerDenied = await SendJsonAsync(client, HttpMethod.Post, "/ops/runbooks/run", "local-viewer", new { runbookType = "IntradayCycle", reason = "Viewer should not run runbooks.", input = new { } });
        Assert.Equal(HttpStatusCode.BadRequest, viewerDenied.StatusCode);

        var run = await PostAsync<OperationalRunbookRunDto>(client, "/ops/runbooks/run", "local-admin", new { runbookType = "IntradayCycle", reason = "Run intraday cycle in integration test.", input = new { } });
        Assert.True(run.Status is "Succeeded" or "PartiallySucceeded");

        var retry = await PostAsync<OperationalRunbookRunDto>(client, $"/ops/runbooks/runs/{run.Id}/retry", "local-admin", new { reason = "Retry intraday cycle in integration test." });
        Assert.Equal(run.Id, retry.RetryOfRunbookRunId);
        Assert.NotEqual(run.Id, retry.Id);
        Assert.Equal(1, retry.RetryCount);

        var runs = await GetAsync<OperationalRunbookRunDto[]>(client, "/ops/runbooks/runs?limit=20", "local-admin");
        Assert.Contains(runs, x => x.Id == run.Id);
        Assert.Contains(runs, x => x.Id == retry.Id);

        var schedules = await GetAsync<ScheduleListDto>(client, "/ops/schedules", "local-admin");
        Assert.False(schedules.SchedulerEnabled);
        Assert.Empty(schedules.Value);

        var audit = await GetAsync<OperatorAuditEventDto[]>(client, "/audit/events?limit=100", "local-admin");
        Assert.Contains(audit, x => x.EventType == "RunbookRetried");
        Assert.Contains(audit, x => x.EventType == "PermissionDenied");
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
                    ["Governance:FourEyesEnabled"] = "true",
                    ["ReferenceDataIntegrity:FailStartupOnBlockingIssues"] = "false"
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

    private static async Task CreateFakeSnapshotsAsync(HttpClient client, DateTimeOffset startUtc, int count)
    {
        using var response = await SendJsonAsync(client, HttpMethod.Post, "/market-data/fake-snapshots", "local-admin", new
        {
            startUtc,
            intervalSeconds = 60,
            count,
            bid = 1.10m,
            ask = 1.1002m,
            bidStep = 0.0001m,
            askStep = 0.0001m
        });
        response.EnsureSuccessStatusCode();
    }

    private sealed record OperationalJobRunDto(string Id, string JobType, string Status, string? OutputJson, string? ExceptionCaseId, string? RetryOfJobRunId, int RetryCount);
    private sealed record OperationalJobStepDto(string StepName, string Status);
    private sealed record OperationalJobRunEventDto(string Message);
    private sealed record DailyOperationsSummaryDto(OperationalJobRunDto? LatestReferenceIntegrity);
    private sealed record DailyChecklistItemDto(string Name, string Status);
    private sealed record OperatorAuditEventDto(string EventType, string? EntityType, string? EntityId, string? CorrelationId, string? Reason, string MetadataJson);
    private sealed record ExceptionCaseDto(string Id, string? EntityType, string? EntityId);
    private sealed record OperationalRunbookDefinitionDto(string Id, string Name, string RunbookType, bool IsEnabled);
    private sealed record OperationalRunbookStepDefinitionDto(string Id, int StepOrder, string Name, string? JobType, string GateType);
    private sealed record RunbookDefinitionDetailDto(OperationalRunbookDefinitionDto Definition, OperationalRunbookStepDefinitionDto[] Steps);
    private sealed record OperationalRunbookRunDto(string Id, string RunbookType, string Status, string? RetryOfRunbookRunId, int RetryCount);
    private sealed record OperationalRunbookStepRunDto(string Id, string Name, string Status, string? JobRunId);
    private sealed record ScheduleListDto(bool SchedulerEnabled, OperationalScheduleDefinitionDto[] Value);
    private sealed record OperationalScheduleDefinitionDto(string Id, string Name, bool IsEnabled);
}
