using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.Simulator;
using QQ.Production.Intraday.Infrastructure.SqlServer;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration).WriteTo.Console());

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IOperatorContext, HttpOperatorContext>();
builder.Services.AddScoped<IOperatorAuditService, OperatorAuditService>();
builder.Services.AddScoped<IOperatorPermissionService, OperatorPermissionService>();
builder.Services.AddScoped<IApprovalWorkflowService, ApprovalWorkflowService>();
builder.Services.AddSingleton(new GovernanceOptions(
    builder.Configuration.GetValue("Governance:FourEyesEnabled", true),
    builder.Configuration.GetValue("Governance:RequireApprovalForRiskActivation", true),
    builder.Configuration.GetValue("Governance:RequireApprovalForRiskRetirement", true),
    builder.Configuration.GetValue("Governance:RequireApprovalForKillSwitchClear", true),
    builder.Configuration.GetValue("Governance:RequireApprovalForWaiveBlockingException", true),
    builder.Configuration.GetValue("Governance:RequireApprovalForFalsePositiveBlockingException", true),
    builder.Configuration.GetValue("Governance:RequireApprovalForResolveCriticalException", true),
    builder.Configuration.GetValue("Governance:ApprovalExpiryMinutes", 1440)));
builder.Services.AddSingleton(new OperatorContextOptions(
    builder.Configuration.GetValue("OperatorContext:DefaultOperatorId", "local-admin") ?? "local-admin",
    builder.Configuration.GetValue("OperatorContext:AllowHeaderOperatorOverride", true)));
builder.Services.AddSingleton(new FakeLmaxOptions());
builder.Services.AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>();
builder.Services.AddSingleton<IMarketDataProvider, FakeMarketDataProvider>();
builder.Services.AddSingleton(new BarBuilderOptions());
builder.Services.AddScoped<ProcessModelRunService>();
builder.Services.AddScoped<IReferenceDataIntegrityService, ReferenceDataIntegrityService>();
builder.Services.AddScoped<IExceptionCaseService, ExceptionCaseService>();
builder.Services.AddScoped<IRiskControlService, RiskControlService>();
builder.Services.AddScoped<IOperationalJobRunner, OperationalJobRunner>();
builder.Services.AddScoped<IOperationalRunbookRunner, OperationalRunbookRunner>();
builder.Services.AddScoped<IDailyOperationsService, DailyOperationsService>();
builder.Services.AddScoped<ILmaxShadowReplayService, LmaxShadowModeService>();
builder.Services.AddSingleton(new LocalSchedulerOptions(
    builder.Configuration.GetValue("LocalScheduler:Enabled", false),
    builder.Configuration.GetValue("LocalScheduler:PollIntervalSeconds", 30)));
builder.Services.AddScoped<IModelWeightPromotionService, ModelWeightPromotionService>();
builder.Services.AddScoped<IFakeModelWeightGenerator, FakeModelWeightGenerator>();
builder.Services.AddSingleton(new LmaxEodReportOptions());
builder.Services.AddScoped<ILmaxEodReportImportService, LmaxEodReportImportService>();
builder.Services.AddScoped<ILmaxReportPairConsistencyService, LmaxReportPairConsistencyService>();
builder.Services.AddScoped<IEodReconciliationService, EodReconciliationService>();
builder.Services.AddScoped<IEodPnlSummaryService, EodPnlSummaryService>();
builder.Services.AddScoped<IFakeLmaxEodReportGenerator, FakeLmaxEodReportGenerator>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalUiDevelopment", policy =>
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var persistenceProvider = builder.Environment.IsEnvironment("Testing")
    ? "InMemory"
    : builder.Configuration.GetValue("Persistence:Provider", "SqlServerLocal") ?? "SqlServerLocal";
if (string.Equals(persistenceProvider, "SqlServerLocal", StringComparison.OrdinalIgnoreCase))
{
    var connectionString = builder.Configuration.GetConnectionString("IntradaySqlServer")
        ?? "Server=(localdb)\\MSSQLLocalDB;Database=QQProductionIntraday;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
    builder.Services.AddDbContext<IntradayDbContext>(options => options.UseSqlServer(connectionString));
    builder.Services.AddScoped<IIntradayRepository, SqlServerIntradayRepository>();
    builder.Services.AddScoped<IMarketDataSnapshotRepository, SqlServerMarketDataSnapshotRepository>();
    builder.Services.AddScoped<IMarketDataBarRepository, SqlServerMarketDataBarRepository>();
    builder.Services.AddScoped<IBarBuildRunRepository, SqlServerBarBuildRunRepository>();
    builder.Services.AddScoped<IModelWeightBatchRepository, SqlServerModelWeightBatchRepository>();
    builder.Services.AddScoped<ILmaxEodReportRepository, SqlServerLmaxEodReportRepository>();
    builder.Services.AddScoped<IOperatorAuditRepository, SqlServerOperatorAuditRepository>();
    builder.Services.AddScoped<IOperatorGovernanceRepository, SqlServerOperatorGovernanceRepository>();
    builder.Services.AddScoped<IExceptionCaseRepository, SqlServerExceptionCaseRepository>();
    builder.Services.AddScoped<IOperationalJobRepository, SqlServerOperationalJobRepository>();
    builder.Services.AddScoped<IOperationalRunbookRepository, SqlServerOperationalRunbookRepository>();
    builder.Services.AddScoped<ILmaxShadowRepository, SqlServerLmaxShadowRepository>();
    builder.Services.AddScoped<IBrokerPositionProvider, SqlServerFakeBrokerPositionProvider>();
    builder.Services.AddScoped<IBarBuilderService, BarBuilderService>();
    builder.Services.AddScoped<LocalDatabaseInitializer>();
    builder.Services.AddScoped(_ => SeedData.Create(new DateTimeOffset(2026, 04, 29, 09, 00, 00, TimeSpan.Zero)));
}
else if (string.Equals(persistenceProvider, "InMemory", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton(SeedData.Create());
    builder.Services.AddSingleton<IIntradayRepository, InMemoryIntradayRepository>();
    builder.Services.AddSingleton<IMarketDataSnapshotRepository, InMemoryMarketDataSnapshotRepository>();
    builder.Services.AddSingleton<IMarketDataBarRepository, InMemoryMarketDataBarRepository>();
    builder.Services.AddSingleton<IBarBuildRunRepository, InMemoryBarBuildRunRepository>();
    builder.Services.AddSingleton<IModelWeightBatchRepository, InMemoryModelWeightBatchRepository>();
    builder.Services.AddSingleton<ILmaxEodReportRepository, InMemoryLmaxEodReportRepository>();
    builder.Services.AddSingleton<IOperatorAuditRepository, InMemoryOperatorAuditRepository>();
    builder.Services.AddSingleton<IOperatorGovernanceRepository, InMemoryOperatorGovernanceRepository>();
    builder.Services.AddSingleton<IExceptionCaseRepository, InMemoryExceptionCaseRepository>();
    builder.Services.AddSingleton<IOperationalJobRepository, InMemoryOperationalJobRepository>();
    builder.Services.AddSingleton<IOperationalRunbookRepository, InMemoryOperationalRunbookRepository>();
    builder.Services.AddSingleton<ILmaxShadowRepository, InMemoryLmaxShadowRepository>();
    builder.Services.AddSingleton<IBrokerPositionProvider, FakeBrokerPositionProvider>();
    builder.Services.AddSingleton<IBarBuilderService, BarBuilderService>();
}
else
{
    throw new InvalidOperationException($"Unsupported persistence provider '{persistenceProvider}'.");
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors("LocalUiDevelopment");
}

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers.TryGetValue("X-Correlation-Id", out var incoming) && !string.IsNullOrWhiteSpace(incoming)
        ? incoming.ToString()
        : Guid.NewGuid().ToString("N");
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-Id"] = correlationId;
    try
    {
        await next();
    }
    catch (DomainRuleViolationException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { message = ex.Message });
    }
});

await InitializeDatabaseAsync(app, persistenceProvider);
ValidateSafety(app, persistenceProvider);
if (args.Contains("--init-db", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var initializer = scope.ServiceProvider.GetRequiredService<LocalDatabaseInitializer>();
    await initializer.ApplyMigrationsAsync(CancellationToken.None);
    await initializer.SeedReferenceDataAsync(CancellationToken.None);
    if (args.Contains("--seed-demo", StringComparer.OrdinalIgnoreCase))
    {
        await initializer.SeedDemoDataAsync(CancellationToken.None);
    }

    return;
}

await ValidateReferenceDataAsync(app);
await RecordStartupAuditAsync(app, persistenceProvider);

app.MapGet("/health", async (IServiceProvider services, IWebHostEnvironment environment, IConfiguration configuration, IClock clock) =>
{
    var provider = configuration.GetValue("Persistence:Provider", "SqlServerLocal") ?? "SqlServerLocal";
    var gateway = services.GetRequiredService<IVenueExecutionGateway>();
    var marketDataProvider = services.GetRequiredService<IMarketDataProvider>();
    var databaseReachable = true;
    var pendingMigrationsCount = 0;
    var databaseTarget = "InMemory";

    if (string.Equals(provider, "SqlServerLocal", StringComparison.OrdinalIgnoreCase))
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IntradayDbContext>();
        databaseReachable = await db.Database.CanConnectAsync();
        pendingMigrationsCount = (await db.Database.GetPendingMigrationsAsync()).Count();
        databaseTarget = "LocalDB";
    }

    return Results.Ok(new HealthDto(
        "QQ.Production.Intraday.Api",
        environment.EnvironmentName,
        provider,
        databaseReachable,
        pendingMigrationsCount,
        databaseTarget,
        gateway.GetType().Name,
        marketDataProvider.GetType().Name,
        configuration.GetValue("Safety:AllowLiveTrading", false),
        configuration.GetValue("Safety:AllowExternalConnections", false),
        clock.UtcNow));
});

app.MapGet("/model-runs", async (IIntradayRepository repository, int? limit, string? status, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var query = state.ModelRuns.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(status))
    {
        query = query.Where(x => x.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase));
    }

    return query.OrderByDescending(x => x.ReceivedAtUtc).Take(ClampLimit(limit)).Select(ToModelRunDto);
});

app.MapGet("/model-weight-batches", async (IModelWeightBatchRepository repository, int? limit, string? status, string? sourceSystem, string? modelName, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken cancellationToken) =>
{
    var selectedStatus = ParseEnum<ModelWeightBatchStatus>(status);
    var selectedSource = ParseEnum<ModelWeightSourceSystem>(sourceSystem);
    var batches = await repository.GetRecentBatchesAsync(ClampLimit(limit), selectedStatus, selectedSource, modelName, fromUtc, toUtc, cancellationToken);
    return batches.Select(ToModelWeightBatchDto);
});

app.MapGet("/model-weight-batches/{id:guid}", async (Guid id, IModelWeightBatchRepository repository, CancellationToken cancellationToken) =>
{
    var batch = await repository.GetBatchAsync(new ModelWeightBatchId(id), cancellationToken);
    return batch is null ? Results.NotFound() : Results.Ok(ToModelWeightBatchDto(batch));
});

app.MapGet("/model-weight-batches/{id:guid}/rows", async (Guid id, IModelWeightBatchRepository repository, CancellationToken cancellationToken) =>
{
    var rows = await repository.GetRowsAsync(new ModelWeightBatchId(id), cancellationToken);
    return Results.Ok(rows.Select(ToModelWeightRowDto));
});

app.MapGet("/model-weight-batches/{id:guid}/validation-issues", async (Guid id, IModelWeightBatchRepository repository, CancellationToken cancellationToken) =>
{
    var issues = await repository.GetValidationIssuesAsync(new ModelWeightBatchId(id), cancellationToken);
    return Results.Ok(issues.Select(ToModelWeightValidationIssueDto));
});

app.MapPost("/model-weight-batches/fake", async (CreateFakeModelWeightBatchApiRequest request, IFakeModelWeightGenerator generator, IOperatorAuditService audit, CancellationToken cancellationToken) =>
{
    try
    {
        var batch = await generator.CreateFakeBatchAsync(ToApplicationRequest(request), cancellationToken);
        await audit.RecordSucceededAsync(OperatorAuditEventType.ModelWeightBatchCreated, "Api", "Created local fake model weight batch.", "ModelWeightBatch", batch.Id.Value.ToString("D"), new { batch.ExternalBatchId, batch.ModelName, batch.Status }, cancellationToken);
        return Results.Created($"/model-weight-batches/{batch.Id.Value}", ToModelWeightBatchDto(batch));
    }
    catch (DomainRuleViolationException ex)
    {
        await audit.RecordFailedAsync(OperatorAuditEventType.ModelWeightBatchCreated, "Api", "Failed to create local fake model weight batch.", ex.Message, metadata: new { request.ExternalBatchId, request.ModelName }, cancellationToken: cancellationToken);
        return Results.Conflict(new { message = ex.Message });
    }
});

app.MapPost("/model-weight-batches/{id:guid}/validate", async (Guid id, IModelWeightPromotionService service, IOperatorAuditService audit, CancellationToken cancellationToken) =>
{
    var result = await service.ValidateBatchAsync(new ModelWeightBatchId(id), cancellationToken);
    await audit.RecordAsync(new OperatorAuditRecordRequest(
        OperatorAuditEventType.ModelWeightBatchValidated,
        result.Succeeded ? OperatorAuditSeverity.Info : OperatorAuditSeverity.Warning,
        result.Succeeded ? OperatorAuditResult.Succeeded : OperatorAuditResult.Blocked,
        "Api",
        result.Succeeded ? "Model weight batch validated." : "Model weight batch validation produced blocking issues.",
        "ModelWeightBatch",
        id.ToString("D"),
        result.Succeeded ? null : result.Message,
        Metadata: new { result.ValidationIssueCount, result.Status }),
        cancellationToken);
    return Results.Ok(ToModelWeightPromotionResultDto(result));
});

app.MapPost("/model-weight-batches/{id:guid}/promote", async (Guid id, IModelWeightPromotionService service, IOperatorAuditService audit, CancellationToken cancellationToken) =>
{
    var result = await service.PromoteBatchAsync(new ModelWeightBatchId(id), cancellationToken);
    await audit.RecordAsync(new OperatorAuditRecordRequest(
        OperatorAuditEventType.ModelWeightBatchPromoted,
        result.Succeeded ? OperatorAuditSeverity.Info : OperatorAuditSeverity.Warning,
        result.Succeeded ? OperatorAuditResult.Succeeded : OperatorAuditResult.Blocked,
        "Api",
        result.Succeeded ? "Model weight batch promoted to model run." : "Model weight batch promotion blocked.",
        "ModelWeightBatch",
        id.ToString("D"),
        result.Succeeded ? null : result.Message,
        Metadata: new { result.ModelRunId, result.ValidationIssueCount, result.AlreadyPromoted }),
        cancellationToken);
    return Results.Ok(ToModelWeightPromotionResultDto(result));
});

app.MapPost("/model-weight-batches/promote-ready", async (PromoteReadyModelWeightBatchesRequest request, IModelWeightPromotionService service, IOperatorAuditService audit, CancellationToken cancellationToken) =>
{
    var results = await service.PromoteReadyBatchesAsync(ClampLimit(request.Limit), cancellationToken);
    await audit.RecordSucceededAsync(OperatorAuditEventType.ModelWeightBatchPromoted, "Api", "Promote-ready model weight batch command completed.", metadata: new { count = results.Count, succeeded = results.Count(x => x.Succeeded), blocked = results.Count(x => !x.Succeeded) }, cancellationToken: cancellationToken);
    return Results.Ok(results.Select(ToModelWeightPromotionResultDto));
});

app.MapGet("/lmax-eod/import-runs", async (ILmaxEodReportRepository repository, int? limit, DateOnly? reportDate, LmaxReportType? reportType, CancellationToken cancellationToken) =>
{
    var runs = await repository.GetImportRunsAsync(ClampLimit(limit), reportDate, reportType, cancellationToken);
    return runs.Select(ToLmaxReportImportRunDto);
});

app.MapGet("/lmax-eod/import-runs/{id:guid}", async (Guid id, ILmaxEodReportRepository repository, CancellationToken cancellationToken) =>
{
    var runs = await repository.GetImportRunsAsync(500, null, null, cancellationToken);
    var run = runs.FirstOrDefault(x => x.Id.Value == id);
    return run is null ? Results.NotFound() : Results.Ok(ToLmaxReportImportRunDto(run));
});

app.MapGet("/lmax-eod/validation-issues", async (ILmaxEodReportRepository repository, int? limit, Guid? importRunId, CancellationToken cancellationToken) =>
{
    var issues = await repository.GetValidationIssuesAsync(ClampLimit(limit), importRunId is null ? null : new LmaxReportImportRunId(importRunId.Value), cancellationToken);
    return issues.Select(ToLmaxReportValidationIssueDto);
});

app.MapGet("/lmax-eod/individual-trades", async (ILmaxEodReportRepository repository, DateOnly? reportDate, int? limit, CancellationToken cancellationToken) =>
{
    var trades = await repository.GetIndividualTradesAsync(reportDate, ClampLimit(limit), cancellationToken);
    return trades.Select(ToLmaxIndividualTradeDto);
});

app.MapGet("/lmax-eod/trade-summaries", async (ILmaxEodReportRepository repository, DateOnly? reportDate, int? limit, CancellationToken cancellationToken) =>
{
    var summaries = await repository.GetTradeSummariesAsync(reportDate, ClampLimit(limit), cancellationToken);
    return summaries.Select(ToLmaxTradeSummaryDto);
});

app.MapGet("/lmax-eod/currency-wallets", async (ILmaxEodReportRepository repository, DateOnly? reportDate, int? limit, CancellationToken cancellationToken) =>
{
    var wallets = await repository.GetCurrencyWalletsAsync(reportDate, ClampLimit(limit), cancellationToken);
    return wallets.Select(ToLmaxCurrencyWalletDto);
});

app.MapPost("/lmax-eod/generate-fake", async (GenerateFakeLmaxEodRequest request, IFakeLmaxEodReportGenerator generator, IOperatorAuditService audit, CancellationToken cancellationToken) =>
{
    var result = await generator.GenerateAsync(request.ReportDate, request.VenueName ?? "LMAX", request.BrokerAccountCode ?? "LMAX_DEMO_LOCAL", request.MutationMode ?? LmaxEodMutationMode.None, cancellationToken);
    await audit.RecordSucceededAsync(OperatorAuditEventType.EodReportGenerated, "Api", "Generated fake local LMAX EOD report set.", "LmaxEodReportSet", request.ReportDate.ToString("yyyy-MM-dd"), new { request.ReportDate, result.MutationMode, result.IndividualTradeCount, result.TradeSummaryCount, result.CurrencyWalletCount }, cancellationToken);
    return Results.Ok(ToFakeLmaxEodReportGenerationDto(result));
});

app.MapPost("/lmax-eod/import-generated", async (ImportGeneratedLmaxEodRequest request, ILmaxEodReportImportService importer, IOperatorAuditService audit, CancellationToken cancellationToken) =>
{
    var root = Path.GetFullPath(Path.Combine("data", "lmax-eod", "generated"));
    var reportDate = request.ReportDate;
    var stamp = reportDate.ToString("yyyyMMdd");
    var individual = Directory.GetFiles(root, $"individual-trades-{stamp}_*.csv").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    var summary = Directory.GetFiles(root, $"trades-{stamp}_*.csv").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    var wallet = Directory.GetFiles(root, $"currency-wallets-{stamp}_*.csv").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    if (individual is null || summary is null || wallet is null)
    {
        return Results.NotFound(new { message = "Generated LMAX EOD report set was not found for the requested date." });
    }

    var result = await importer.ImportReportSetAsync(individual, summary, wallet, reportDate, request.VenueName ?? "LMAX", request.BrokerAccountCode ?? "LMAX_DEMO_LOCAL", cancellationToken);
    await audit.RecordAsync(new OperatorAuditRecordRequest(OperatorAuditEventType.EodReportImported, result.BlockingIssueCount == 0 ? OperatorAuditSeverity.Info : OperatorAuditSeverity.Warning, result.BlockingIssueCount == 0 ? OperatorAuditResult.Succeeded : OperatorAuditResult.Blocked, "Api", result.BlockingIssueCount == 0 ? "Imported generated LMAX EOD report set." : "Generated LMAX EOD report set import had blocking validation issues.", "LmaxReportImportRun", result.ImportRunId.Value.ToString("D"), result.BlockingIssueCount == 0 ? null : result.Message, Metadata: new { result.RowCount, result.BlockingIssueCount }), cancellationToken);
    return Results.Ok(ToLmaxReportImportResultDto(result));
});

app.MapPost("/lmax-eod/import-report-set", async (ImportLmaxReportSetRequest request, ILmaxEodReportImportService importer, IOperatorAuditService audit, CancellationToken cancellationToken) =>
{
    var result = await importer.ImportReportSetAsync(request.IndividualTradesPath, request.TradesSummaryPath, request.CurrencyWalletsPath, request.ReportDate, request.VenueName ?? "LMAX", request.BrokerAccountCode ?? "LMAX_DEMO_LOCAL", cancellationToken);
    await audit.RecordAsync(new OperatorAuditRecordRequest(OperatorAuditEventType.EodReportImported, result.BlockingIssueCount == 0 ? OperatorAuditSeverity.Info : OperatorAuditSeverity.Warning, result.BlockingIssueCount == 0 ? OperatorAuditResult.Succeeded : OperatorAuditResult.Blocked, "Api", result.BlockingIssueCount == 0 ? "Imported LMAX EOD report set." : "LMAX EOD report set import had blocking validation issues.", "LmaxReportImportRun", result.ImportRunId.Value.ToString("D"), result.BlockingIssueCount == 0 ? null : result.Message, Metadata: new { request.ReportDate, result.RowCount, result.BlockingIssueCount }), cancellationToken);
    return Results.Ok(ToLmaxReportImportResultDto(result));
});

app.MapPost("/lmax-eod/import-individual-trades", async (ImportSingleLmaxReportRequest request, ILmaxEodReportImportService importer, CancellationToken cancellationToken) =>
{
    var result = await importer.ImportIndividualTradesAsync(request.FilePath, request.ReportDate, request.VenueName ?? "LMAX", request.BrokerAccountCode ?? "LMAX_DEMO_LOCAL", cancellationToken);
    return Results.Ok(ToLmaxReportImportResultDto(result));
});

app.MapPost("/lmax-eod/import-trades-summary", async (ImportSingleLmaxReportRequest request, ILmaxEodReportImportService importer, CancellationToken cancellationToken) =>
{
    var result = await importer.ImportTradesSummaryAsync(request.FilePath, request.ReportDate, request.VenueName ?? "LMAX", request.BrokerAccountCode ?? "LMAX_DEMO_LOCAL", cancellationToken);
    return Results.Ok(ToLmaxReportImportResultDto(result));
});

app.MapPost("/lmax-eod/import-currency-wallets", async (ImportSingleLmaxReportRequest request, ILmaxEodReportImportService importer, CancellationToken cancellationToken) =>
{
    var result = await importer.ImportCurrencyWalletsAsync(request.FilePath, request.ReportDate, request.VenueName ?? "LMAX", request.BrokerAccountCode ?? "LMAX_DEMO_LOCAL", cancellationToken);
    return Results.Ok(ToLmaxReportImportResultDto(result));
});

app.MapPost("/eod-reconciliation/run", async (RunEodReconciliationRequest request, IEodReconciliationService service, IOperatorAuditService audit, CancellationToken cancellationToken) =>
{
    var result = await service.RunAsync(request.ReportDate, request.VenueName ?? "LMAX", request.BrokerAccountCode ?? "LMAX_DEMO_LOCAL", cancellationToken);
    await audit.RecordAsync(new OperatorAuditRecordRequest(OperatorAuditEventType.EodReconciliationRun, result.BlockingBreakCount == 0 ? OperatorAuditSeverity.Info : OperatorAuditSeverity.Critical, result.BlockingBreakCount == 0 ? OperatorAuditResult.Succeeded : OperatorAuditResult.Blocked, "Api", result.BlockingBreakCount == 0 ? "EOD reconciliation completed without blocking breaks." : "EOD reconciliation created blocking breaks.", "EodReconciliationRun", result.RunId.ToString("D"), result.BlockingBreakCount == 0 ? null : "Blocking EOD reconciliation breaks exist.", Metadata: new { result.ReportDate, result.BreakCount, result.BlockingBreakCount }), cancellationToken);
    return Results.Ok(ToEodReconciliationResultDto(result));
});

app.MapGet("/eod-reconciliation/runs", async (ILmaxEodReportRepository repository, DateOnly? reportDate, int? limit, CancellationToken cancellationToken) =>
{
    var runs = await repository.GetEodReconciliationRunsAsync(reportDate, ClampLimit(limit), cancellationToken);
    return runs.Select(ToEodReconciliationRunDto);
});

app.MapGet("/eod-reconciliation/breaks", async (ILmaxEodReportRepository repository, DateOnly? reportDate, int? limit, CancellationToken cancellationToken) =>
{
    var breaks = await repository.GetEodReconciliationBreaksAsync(reportDate, ClampLimit(limit), cancellationToken);
    return breaks.Select(ToEodReconciliationBreakDto);
});

app.MapGet("/eod-pnl/summary", async (IEodPnlSummaryService service, DateOnly reportDate, string? venueName, string? brokerAccountCode, CancellationToken cancellationToken) =>
{
    var summary = await service.GetSummaryAsync(reportDate, venueName ?? "LMAX", brokerAccountCode ?? "LMAX_DEMO_LOCAL", cancellationToken);
    return summary is null ? Results.NotFound() : Results.Ok(ToEodPnlSummaryDto(summary));
});

app.MapPost("/model-runs", async (CreateModelRunRequest request, IIntradayRepository repository, IClock clock, IOperatorAuditService audit, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var fund = state.Funds.Single();
    var now = clock.UtcNow;
    var run = new ModelRun(ModelRunId.New(), fund.Id, request.ModelName ?? "IntradayFxModel", request.AsOfUtc ?? now, now, request.EffectiveAtUtc ?? request.AsOfUtc ?? now, request.FrequencyMinutes <= 0 ? 15 : request.FrequencyMinutes, request.NavUsd <= 0 ? 1_000_000m : request.NavUsd, ModelRunStatus.Received, request.InputHash ?? Guid.NewGuid().ToString("N"), request.SourceFileName ?? "api", false, request.TargetQuantityMode);
    var weights = request.Weights is { Count: > 0 }
        ? request.Weights
        : [new ModelRunWeightRequest(request.Symbol ?? "EURUSD", request.Weight ?? 0m, request.Symbol ?? "EURUSD")];
    var targetWeights = weights.Select(x =>
    {
        var instrument = state.Instruments.Single(i => i.Symbol == x.Symbol);
        return new TargetWeight(run.Id, instrument.Id, x.Weight, x.RawSecurityId ?? x.Symbol);
    }).ToList();
    await repository.AddModelRunAsync(run, targetWeights, cancellationToken);
    await audit.RecordSucceededAsync(OperatorAuditEventType.ModelRunCreated, "Api", "Created local model run and target weights.", "ModelRun", run.Id.Value.ToString("D"), new { run.ModelName, run.AsOfUtc, run.EffectiveAtUtc, targetWeightCount = targetWeights.Count }, cancellationToken);
    return Results.Created($"/model-runs/{run.Id.Value}", ToModelRunDto(run));
});

app.MapPost("/model-runs/{id:guid}/process", async (Guid id, ProcessModelRunService service, IOperatorAuditService audit, CancellationToken cancellationToken) =>
{
    var modelRunId = new ModelRunId(id);
    var result = await service.ProcessAsync(modelRunId, cancellationToken);
    var eventType = result.Status == ProcessModelRunStatus.Blocked ? OperatorAuditEventType.ModelRunBlocked : OperatorAuditEventType.ModelRunProcessed;
    var severity = result.Status switch
    {
        ProcessModelRunStatus.Failed => OperatorAuditSeverity.Critical,
        ProcessModelRunStatus.Blocked => OperatorAuditSeverity.Warning,
        _ => OperatorAuditSeverity.Info
    };
    var auditResult = result.Status switch
    {
        ProcessModelRunStatus.Failed => OperatorAuditResult.Failed,
        ProcessModelRunStatus.Blocked => OperatorAuditResult.Blocked,
        ProcessModelRunStatus.NoActionRequired => OperatorAuditResult.NoActionRequired,
        _ => OperatorAuditResult.Succeeded
    };
    await audit.RecordAsync(new OperatorAuditRecordRequest(eventType, severity, auditResult, "Api", result.Message ?? $"Model run process result: {result.Status}.", "ModelRun", id.ToString("D"), result.BlockedReason?.ToString(), Metadata: new { result.Status, result.TradeIntentCount, result.RiskDecisionCount, result.OrderCount, result.ExecutionReportCount, result.FillCount, result.ReconciliationBreakCount, result.IsAlreadyProcessed }), cancellationToken);
    return Results.Ok(new
    {
        modelRunId = id,
        result.Processed,
        status = result.Status.ToString(),
        blockedReason = result.BlockedReason?.ToString(),
        result.Message,
        result.TradeIntentCount,
        result.RiskDecisionCount,
        result.OrderCount,
        result.ExecutionReportCount,
        result.FillCount,
        result.ReconciliationBreakCount,
        result.IsAlreadyProcessed,
        result.CompletedAtUtc
    });
});

app.MapGet("/positions/internal", async (IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var symbols = state.Instruments.ToDictionary(x => x.Id, x => x.Symbol);
    return state.PositionLedger
        .GroupBy(x => x.InstrumentId)
        .Select(x => new PositionDto(x.Key.Value.ToString("D"), symbols.GetValueOrDefault(x.Key), x.Sum(y => y.BaseQuantityDelta), x.Max(y => y.CreatedAtUtc)));
});
app.MapGet("/positions/broker", async (IIntradayRepository repository, IBrokerPositionProvider provider, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var symbols = state.Instruments.ToDictionary(x => x.Id, x => x.Symbol);
    var positions = await provider.GetPositionsAsync(state.BrokerAccounts.Single().Id, cancellationToken);
    return positions.Select(x => new PositionDto(x.InstrumentId.Value.ToString("D"), symbols.GetValueOrDefault(x.InstrumentId), x.BaseQuantity, x.AsOfUtc));
});
app.MapGet("/target-positions", async (IIntradayRepository repository, int? limit, Guid? modelRunId, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var symbols = state.Instruments.ToDictionary(x => x.Id, x => x.Symbol);
    var query = state.TargetPositions.AsEnumerable();
    if (modelRunId is not null) query = query.Where(x => x.ModelRunId.Value == modelRunId.Value);
    return query.TakeLast(ClampLimit(limit)).Reverse().Select(x => new TargetPositionDto(x.ModelRunId.Value.ToString("D"), x.InstrumentId.Value.ToString("D"), symbols.GetValueOrDefault(x.InstrumentId), x.TargetNotionalUsd, x.TargetBaseQuantity, x.TargetVenueQuantity, x.TargetQuantityMode.ToString()));
});
app.MapGet("/drift-snapshots", async (IIntradayRepository repository, int? limit, Guid? modelRunId, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var symbols = state.Instruments.ToDictionary(x => x.Id, x => x.Symbol);
    var query = state.DriftSnapshots.AsEnumerable();
    if (modelRunId is not null) query = query.Where(x => x.ModelRunId.Value == modelRunId.Value);
    return query.TakeLast(ClampLimit(limit)).Reverse().Select(x => new DriftSnapshotDto(x.ModelRunId.Value.ToString("D"), x.InstrumentId.Value.ToString("D"), symbols.GetValueOrDefault(x.InstrumentId), x.TargetBaseQuantity, x.CurrentBaseQuantity, x.DriftBaseQuantity, x.TargetVenueQuantity, x.CurrentVenueQuantity, x.DriftVenueQuantity));
});
app.MapGet("/reconciliation/breaks", async (IIntradayRepository repository, int? limit, string? severity, string? status, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var symbols = state.Instruments.ToDictionary(x => x.Id, x => x.Symbol);
    var runs = state.ReconciliationRuns.ToDictionary(x => x.Id);
    var query = state.ReconciliationBreaks.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(severity)) query = query.Where(x => x.Severity.ToString().Equals(severity, StringComparison.OrdinalIgnoreCase));
    if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase));
    return query.TakeLast(ClampLimit(limit)).Reverse().Select(x => ToReconciliationBreakDto(x, runs, symbols));
});
app.MapGet("/trade-intents", async (IIntradayRepository repository, int? limit, string? status, Guid? modelRunId, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var symbols = state.Instruments.ToDictionary(x => x.Id, x => x.Symbol);
    var query = state.TradeIntents.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase));
    if (modelRunId is not null) query = query.Where(x => x.ModelRunId.Value == modelRunId.Value);
    return query.OrderByDescending(x => x.CreatedAtUtc).Take(ClampLimit(limit)).Select(x => ToTradeIntentDto(x, symbols));
});
app.MapGet("/risk-decisions", async (IIntradayRepository repository, int? limit, string? status, Guid? modelRunId, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var intents = state.TradeIntents.ToDictionary(x => x.Id);
    var symbols = state.Instruments.ToDictionary(x => x.Id, x => x.Symbol);
    var venues = state.Venues.ToDictionary(x => x.Id, x => x.Name);
    var riskSets = state.RiskLimitSets.ToDictionary(x => x.Id);
    var details = state.RiskDecisionDetails.GroupBy(x => x.RiskDecisionId).ToDictionary(x => x.Key, x => x.ToList());
    var query = state.RiskDecisions.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase));
    if (modelRunId is not null) query = query.Where(x => intents.TryGetValue(x.TradeIntentId, out var intent) && intent.ModelRunId.Value == modelRunId.Value);
    return query.OrderByDescending(x => x.CreatedAtUtc).Take(ClampLimit(limit)).Select(x => ToRiskDecisionDto(x, intents, symbols, venues, riskSets, details.GetValueOrDefault(x.Id) ?? []));
});
app.MapGet("/risk/decisions", async (IIntradayRepository repository, int? limit, string? status, Guid? modelRunId, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var intents = state.TradeIntents.ToDictionary(x => x.Id);
    var symbols = state.Instruments.ToDictionary(x => x.Id, x => x.Symbol);
    var venues = state.Venues.ToDictionary(x => x.Id, x => x.Name);
    var riskSets = state.RiskLimitSets.ToDictionary(x => x.Id);
    var details = state.RiskDecisionDetails.GroupBy(x => x.RiskDecisionId).ToDictionary(x => x.Key, x => x.ToList());
    var query = state.RiskDecisions.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase));
    if (modelRunId is not null) query = query.Where(x => intents.TryGetValue(x.TradeIntentId, out var intent) && intent.ModelRunId.Value == modelRunId.Value);
    return query.OrderByDescending(x => x.CreatedAtUtc).Take(ClampLimit(limit)).Select(x => ToRiskDecisionDto(x, intents, symbols, venues, riskSets, details.GetValueOrDefault(x.Id) ?? []));
});
app.MapGet("/risk/decisions/{id:guid}", async (Guid id, IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var decision = state.RiskDecisions.FirstOrDefault(x => x.Id == id);
    if (decision is null) return Results.NotFound();
    return Results.Ok(ToRiskDecisionDto(
        decision,
        state.TradeIntents.ToDictionary(x => x.Id),
        state.Instruments.ToDictionary(x => x.Id, x => x.Symbol),
        state.Venues.ToDictionary(x => x.Id, x => x.Name),
        state.RiskLimitSets.ToDictionary(x => x.Id),
        state.RiskDecisionDetails.Where(x => x.RiskDecisionId == decision.Id).ToList()));
});
app.MapGet("/orders", async (IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var tradeIntents = state.TradeIntents.ToDictionary(x => x.Id);
    var parentsById = state.ParentOrders.ToDictionary(x => x.Id);
    var parentDtos = state.ParentOrders
        .OrderBy(x => x.CreatedAtUtc)
        .Select(x =>
        {
            tradeIntents.TryGetValue(x.TradeIntentId, out var intent);
            return new ParentOrderDto(
                x.Id.Value.ToString("D"),
                x.TradeIntentId.Value.ToString("D"),
                intent?.InstrumentId.Value.ToString("D"),
                x.ClientOrderId.Value,
                x.Side.ToString(),
                x.BaseQuantity,
                x.Algo.ToString(),
                x.Status.ToString(),
                x.CreatedAtUtc);
        })
        .ToList();

    var childDtos = state.ChildOrders
        .OrderBy(x => x.CreatedAtUtc)
        .Select(x =>
        {
            parentsById.TryGetValue(x.ParentOrderId, out var parent);
            var instrumentId = parent is not null && tradeIntents.TryGetValue(parent.TradeIntentId, out var intent)
                ? intent.InstrumentId.Value.ToString("D")
                : null;
            var brokerOrderId = state.ExecutionReports
                .Where(r => r.ChildOrderId == x.Id && !string.IsNullOrWhiteSpace(r.BrokerOrderId))
                .OrderByDescending(r => r.ReceivedAtUtc)
                .Select(r => r.BrokerOrderId)
                .FirstOrDefault();

            return new ChildOrderDto(
                x.Id.Value.ToString("D"),
                x.ParentOrderId.Value.ToString("D"),
                x.VenueId.Value.ToString("D"),
                instrumentId,
                x.ClientOrderId.Value,
                brokerOrderId,
                x.Side.ToString(),
                x.OrderType.ToString(),
                x.TimeInForce.ToString(),
                x.BaseQuantity,
                x.VenueQuantity,
                x.Status.ToString(),
                x.CreatedAtUtc);
        })
        .ToList();

    return new OrdersResponse(parentDtos, childDtos);
});
app.MapGet("/fills", async (IIntradayRepository repository, int? limit, Guid? modelRunId, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var parentById = state.ParentOrders.ToDictionary(x => x.Id);
    var childById = state.ChildOrders.ToDictionary(x => x.Id);
    var symbols = state.Instruments.ToDictionary(x => x.Id, x => x.Symbol);
    var venues = state.Venues.ToDictionary(x => x.Id, x => x.Name);
    var query = state.Fills.AsEnumerable();
    if (modelRunId is not null)
    {
        var intentIds = state.TradeIntents.Where(x => x.ModelRunId.Value == modelRunId.Value).Select(x => x.Id).ToHashSet();
        var parentIds = state.ParentOrders.Where(x => intentIds.Contains(x.TradeIntentId)).Select(x => x.Id).ToHashSet();
        var childIds = state.ChildOrders.Where(x => parentIds.Contains(x.ParentOrderId)).Select(x => x.Id).ToHashSet();
        query = query.Where(x => childIds.Contains(x.ChildOrderId));
    }

    return query.OrderByDescending(x => x.ReceivedAtUtc).Take(ClampLimit(limit)).Select(x => ToFillDto(x, symbols, venues));
});
app.MapGet("/admin/reference-data/integrity", async (IReferenceDataIntegrityService service, IOperatorAuditService audit, CancellationToken cancellationToken) =>
{
    var result = await service.CheckAsync(cancellationToken);
    if (result.BlockingIssueCount > 0)
    {
        await audit.RecordBlockedAsync(
            OperatorAuditEventType.ReferenceDataIntegrityChecked,
            "Api",
            "Reference data integrity check found blocking issues.",
            $"{result.BlockingIssueCount} blocking issue(s).",
            "ReferenceDataIntegrity",
            result.CheckedAtUtc.ToString("O"),
            new { result.BlockingIssueCount, result.WarningIssueCount },
            cancellationToken);
    }

    return Results.Ok(new ReferenceDataIntegrityDto(
        result.CheckedAtUtc,
        result.BlockingIssueCount,
        result.WarningIssueCount,
        result.Issues.Select(x => new ReferenceDataIntegrityIssueDto(x.Id.ToString("D"), x.Type.ToString(), x.Severity.ToString(), x.Status.ToString(), x.Key, x.Description, x.CreatedAtUtc)).ToList()));
});
app.MapGet("/market-data/snapshots", async (IIntradayRepository repository, string? instrument, string? venue, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int? limit, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var query = state.MarketData.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(instrument)) query = query.Where(x => x.InstrumentId == state.Instruments.Single(i => i.Symbol == instrument).Id);
    if (!string.IsNullOrWhiteSpace(venue)) query = query.Where(x => x.VenueId == state.Venues.Single(v => v.Name == venue).Id);
    if (fromUtc is not null) query = query.Where(x => x.SourceTimestampUtc >= fromUtc.Value);
    if (toUtc is not null) query = query.Where(x => x.SourceTimestampUtc < toUtc.Value);
    var symbols = state.Instruments.ToDictionary(x => x.Id, x => x.Symbol);
    var venues = state.Venues.ToDictionary(x => x.Id, x => x.Name);
    return query.OrderByDescending(x => x.SourceTimestampUtc).Take(ClampLimit(limit)).Select(x => ToMarketDataSnapshotDto(x, symbols, venues));
});
app.MapPost("/market-data/fake-snapshots", async (FakeSnapshotsRequest request, IIntradayRepository intradayRepository, IMarketDataProvider provider, IMarketDataSnapshotRepository repository, CancellationToken cancellationToken) =>
{
    var state = await intradayRepository.LoadStateAsync(cancellationToken);
    var instrument = state.Instruments.Single(x => x.Symbol == (request.InstrumentSymbol ?? "EURUSD"));
    var venue = state.Venues.Single(x => x.Name == (request.VenueName ?? "LMAX"));
    var snapshots = await provider.GetSnapshotsAsync(instrument, venue, request.StartUtc, TimeSpan.FromSeconds(request.IntervalSeconds <= 0 ? 60 : request.IntervalSeconds), request.Count, request.Bid, request.Ask, request.BidStep ?? 0m, request.AskStep ?? 0m, cancellationToken);
    await repository.AddRangeAsync(snapshots, cancellationToken);
    return Results.Created("/market-data/snapshots", new { created = snapshots.Count });
});
app.MapGet("/market-data/bars", async (IIntradayRepository repository, string? instrument, string? venue, BarTimeframe? timeframe, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int? limit, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var selectedTimeframe = timeframe ?? BarTimeframe.FifteenMinutes;
    var query = state.MarketDataBars.Where(x => x.Timeframe == selectedTimeframe).AsEnumerable();
    if (!string.IsNullOrWhiteSpace(instrument)) query = query.Where(x => x.InstrumentId == state.Instruments.Single(i => i.Symbol == instrument).Id);
    if (!string.IsNullOrWhiteSpace(venue)) query = query.Where(x => x.VenueId == state.Venues.Single(v => v.Name == venue).Id);
    if (fromUtc is not null) query = query.Where(x => x.BarStartUtc >= fromUtc.Value);
    if (toUtc is not null) query = query.Where(x => x.BarStartUtc < toUtc.Value);
    var symbols = state.Instruments.ToDictionary(x => x.Id, x => x.Symbol);
    var venues = state.Venues.ToDictionary(x => x.Id, x => x.Name);
    return query.OrderByDescending(x => x.BarStartUtc).Take(ClampLimit(limit)).Select(x => ToMarketDataBarDto(x, symbols, venues));
});
app.MapPost("/market-data/build-bars", async (BuildBarsRequest request, IIntradayRepository repository, IBarBuilderService builderService, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var venue = state.Venues.Single(x => x.Name == (request.VenueName ?? "LMAX"));
    return Results.Ok(await builderService.BuildBarsAsync(venue.Id, request.Timeframe, request.StartUtc, request.EndUtc, cancellationToken));
});
app.MapGet("/risk/limit-sets", async (IRiskControlService service, CancellationToken cancellationToken) =>
    (await service.GetRiskLimitSetsAsync(cancellationToken)).Select(ToRiskLimitSetDto));
app.MapGet("/risk/limit-sets/{id:guid}", async (Guid id, IRiskControlService service, CancellationToken cancellationToken) =>
{
    var set = await service.GetRiskLimitSetAsync(id, cancellationToken);
    return set is null ? Results.NotFound() : Results.Ok(ToRiskLimitSetDto(set));
});
app.MapGet("/risk/limit-sets/active", async (string? fundCode, string? modelName, IRiskControlService service, CancellationToken cancellationToken) =>
{
    var set = await service.GetActiveRiskLimitSetAsync(string.IsNullOrWhiteSpace(fundCode) ? "QQ_MASTER" : fundCode, string.IsNullOrWhiteSpace(modelName) ? "IntradayFxModel" : modelName, cancellationToken);
    return set is null ? Results.NotFound() : Results.Ok(ToRiskLimitSetDto(set));
});
app.MapPost("/risk/limit-sets", async (CreateRiskLimitSetRequest request, IRiskControlService service, CancellationToken cancellationToken) =>
    Results.Created("/risk/limit-sets", ToRiskLimitSetDto(await service.CreateDraftRiskLimitSetAsync(request.FundCode ?? "QQ_MASTER", request.ModelName ?? "IntradayFxModel", request.Name, request.Description, request.Reason, cancellationToken))));
app.MapPost("/risk/limit-sets/{id:guid}/clone", async (Guid id, ReasonRequest request, IRiskControlService service, CancellationToken cancellationToken) =>
    Results.Ok(ToRiskLimitSetDto(await service.CloneRiskLimitSetAsync(id, request.Reason, cancellationToken))));
app.MapPost("/risk/limit-sets/{id:guid}/activate", async (Guid id, ReasonRequest request, IRiskControlService service, IApprovalWorkflowService approvals, GovernanceOptions governance, IOperatorContext operatorContext, CancellationToken cancellationToken) =>
{
    if (governance.FourEyesEnabled && governance.RequireApprovalForRiskActivation)
    {
        var approval = await approvals.CreateApprovalRequestAsync(new CreateApprovalRequestRequest(ApprovalRequestType.ActivateRiskLimitSet, "RiskLimitSet", id.ToString("D"), request.Reason, new { riskLimitSetId = id, action = "ActivateRiskLimitSet" }), cancellationToken);
        return Results.Ok(ToGovernedActionResultDto(PendingResult(approval, operatorContext)));
    }
    return Results.Ok(ToRiskLimitSetDto(await service.ActivateRiskLimitSetAsync(id, request.Reason, cancellationToken)));
});
app.MapPost("/risk/limit-sets/{id:guid}/retire", async (Guid id, ReasonRequest request, IRiskControlService service, IApprovalWorkflowService approvals, GovernanceOptions governance, IOperatorContext operatorContext, CancellationToken cancellationToken) =>
{
    if (governance.FourEyesEnabled && governance.RequireApprovalForRiskRetirement)
    {
        var approval = await approvals.CreateApprovalRequestAsync(new CreateApprovalRequestRequest(ApprovalRequestType.RetireRiskLimitSet, "RiskLimitSet", id.ToString("D"), request.Reason, new { riskLimitSetId = id, action = "RetireRiskLimitSet" }), cancellationToken);
        return Results.Ok(ToGovernedActionResultDto(PendingResult(approval, operatorContext)));
    }
    return Results.Ok(ToRiskLimitSetDto(await service.RetireRiskLimitSetAsync(id, request.Reason, cancellationToken)));
});
app.MapGet("/risk/limits", async (Guid riskLimitSetId, IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    return state.RiskLimits.Where(x => x.RiskLimitSetId == riskLimitSetId).OrderBy(x => x.Name).Select(ToRiskLimitDto);
});
app.MapPut("/risk/limits/{id:guid}", async (Guid id, UpdateRiskLimitRequest request, IRiskControlService service, CancellationToken cancellationToken) =>
    Results.Ok(ToRiskLimitDto(await service.UpdateRiskLimitAsync(id, request.Value, request.Unit, request.Reason, cancellationToken))));
app.MapGet("/risk/instrument-limits", async (Guid riskLimitSetId, IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var symbols = state.Instruments.ToDictionary(x => x.Id, x => x.Symbol);
    return state.InstrumentRiskLimits.Where(x => x.RiskLimitSetId == riskLimitSetId).OrderBy(x => symbols.GetValueOrDefault(x.InstrumentId)).Select(x => ToInstrumentRiskLimitDto(x, symbols));
});
app.MapPut("/risk/instrument-limits/{id:guid}", async (Guid id, UpdateInstrumentRiskLimitRequest request, IRiskControlService service, CancellationToken cancellationToken) =>
{
    var state = await service.UpdateInstrumentRiskLimitAsync(id, request.MaxTradeNotionalUsd, request.MaxExposureUsd, request.MinTradeQuantity, request.MaxOrdersPerDay, request.IsTradingEnabled, request.Reason, cancellationToken);
    return Results.Ok(ToInstrumentRiskLimitDto(state, new Dictionary<InstrumentId, string>()));
});
app.MapGet("/risk/venue-limits", async (Guid riskLimitSetId, IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var venues = state.Venues.ToDictionary(x => x.Id, x => x.Name);
    return state.VenueRiskLimits.Where(x => x.RiskLimitSetId == riskLimitSetId).OrderBy(x => venues.GetValueOrDefault(x.VenueId)).Select(x => ToVenueRiskLimitDto(x, venues));
});
app.MapPut("/risk/venue-limits/{id:guid}", async (Guid id, UpdateVenueRiskLimitRequest request, IRiskControlService service, CancellationToken cancellationToken) =>
    Results.Ok(ToVenueRiskLimitDto(await service.UpdateVenueRiskLimitAsync(id, request.MaxTradeNotionalUsd, request.MaxDailyTurnoverUsd, request.MaxOrdersPerMinute, request.IsVenueEnabled, request.Reason, cancellationToken), new Dictionary<VenueId, string>())));
app.MapGet("/risk/trading-windows", async (IIntradayRepository repository, string? modelName, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var query = state.TradingWindows.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(modelName)) query = query.Where(x => x.ModelName.Equals(modelName, StringComparison.OrdinalIgnoreCase));
    return query.OrderBy(x => x.ModelName).ThenBy(x => x.DayOfWeek).Select(ToTradingWindowDto);
});
app.MapPut("/risk/trading-windows/{id:guid}", async (Guid id, UpdateTradingWindowRequest request, IRiskControlService service, CancellationToken cancellationToken) =>
    Results.Ok(ToTradingWindowDto(await service.UpdateTradingWindowAsync(id, request.OpensAtUtc, request.ClosesAtUtc, request.NoNewOrdersAfterUtc, request.FlattenAtUtc, request.TradingEnabled, request.Reason, cancellationToken))));
app.MapGet("/risk/instruments", async (IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    return state.Instruments.OrderBy(x => x.Symbol).Select(x => ToRiskInstrumentDto(x, state.InstrumentAliases.Where(a => a.InstrumentId == x.Id).ToList(), state.VenueInstrumentMappings.Where(m => m.InstrumentId == x.Id).ToList()));
});
app.MapPut("/risk/instruments/{id:guid}/controls", async (Guid id, UpdateInstrumentControlsRequest request, IRiskControlService service, CancellationToken cancellationToken) =>
    Results.Ok(ToInstrumentDto(await service.UpdateInstrumentControlsAsync(new InstrumentId(id), request.IsTradingEnabled, request.IsReportImportEnabled, request.IsMarketDataEnabled, request.Reason, cancellationToken))));
app.MapGet("/risk/venues", async (IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    return state.Venues.OrderBy(x => x.Name).Select(ToRiskVenueDto);
});
app.MapPut("/risk/venues/{id:guid}/controls", async (Guid id, UpdateVenueControlsRequest request, IRiskControlService service, CancellationToken cancellationToken) =>
    Results.Ok(ToVenueDto(await service.UpdateVenueControlsAsync(new VenueId(id), request.IsTradingEnabled, request.IsReportImportEnabled, request.IsMarketDataEnabled, request.Reason, cancellationToken))));
app.MapPost("/admin/kill-switch", async (KillSwitchRequest request, IIntradayRepository repository, IOperatorAuditService audit, CancellationToken cancellationToken) =>
{
    await repository.SetKillSwitchAsync(true, request.Reason, cancellationToken);
    await audit.RecordSucceededAsync(OperatorAuditEventType.KillSwitchActivated, "Api", "Kill switch activated.", "KillSwitch", "global", new { request.Reason }, cancellationToken);
    return Results.Ok(new { active = true, request.Reason });
});
app.MapGet("/admin/kill-switch", async (IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    return Results.Ok(ToKillSwitchDto(state.KillSwitch));
});
app.MapPost("/admin/kill-switch/clear", async (KillSwitchRequest? request, IIntradayRepository repository, IOperatorAuditService audit, IApprovalWorkflowService approvals, GovernanceOptions governance, IOperatorContext operatorContext, CancellationToken cancellationToken) =>
{
    var reason = request?.Reason ?? "Clear local kill switch";
    if (governance.FourEyesEnabled && governance.RequireApprovalForKillSwitchClear)
    {
        var approval = await approvals.CreateApprovalRequestAsync(new CreateApprovalRequestRequest(ApprovalRequestType.ClearKillSwitch, "KillSwitch", "global", reason, new { action = "ClearKillSwitch" }), cancellationToken);
        return Results.Ok(ToGovernedActionResultDto(PendingResult(approval, operatorContext)));
    }
    await repository.SetKillSwitchAsync(false, null, cancellationToken);
    await audit.RecordSucceededAsync(OperatorAuditEventType.KillSwitchCleared, "Api", "Kill switch cleared.", "KillSwitch", "global", cancellationToken: cancellationToken);
    return Results.Ok(new { active = false });
});

app.MapGet("/operators/current", async (IOperatorPermissionService permissions, CancellationToken cancellationToken) =>
{
    var current = await permissions.GetCurrentOperatorAsync(cancellationToken);
    if (current is null) return Results.NotFound();
    var roles = await permissions.GetRolesAsync(current.Id, cancellationToken);
    var perms = await permissions.GetPermissionsAsync(current.Id, cancellationToken);
    return Results.Ok(ToOperatorUserDto(current, roles, perms));
});
app.MapGet("/operators", async (IOperatorGovernanceRepository repository, IOperatorPermissionService permissions, CancellationToken cancellationToken) =>
{
    var users = await repository.GetOperatorsAsync(cancellationToken);
    var rows = new List<OperatorUserDto>();
    foreach (var user in users)
    {
        var roles = await permissions.GetRolesAsync(user.Id, cancellationToken);
        var perms = await permissions.GetPermissionsAsync(user.Id, cancellationToken);
        rows.Add(ToOperatorUserDto(user, roles, perms));
    }
    return Results.Ok(rows);
});
app.MapGet("/operators/{operatorId}", async (string operatorId, IOperatorGovernanceRepository repository, IOperatorPermissionService permissions, CancellationToken cancellationToken) =>
{
    var user = await repository.GetOperatorByIdAsync(operatorId, cancellationToken);
    if (user is null) return Results.NotFound();
    var roles = await permissions.GetRolesAsync(user.Id, cancellationToken);
    var perms = await permissions.GetPermissionsAsync(user.Id, cancellationToken);
    return Results.Ok(ToOperatorUserDto(user, roles, perms));
});
app.MapGet("/operators/{operatorId}/permissions", async (string operatorId, IOperatorGovernanceRepository repository, IOperatorPermissionService permissions, CancellationToken cancellationToken) =>
{
    var user = await repository.GetOperatorByIdAsync(operatorId, cancellationToken);
    if (user is null) return Results.NotFound();
    var perms = await permissions.GetPermissionsAsync(user.Id, cancellationToken);
    return Results.Ok(perms.Select(x => x.ToString()).OrderBy(x => x));
});

app.MapGet("/approvals", async (string? status, string? type, string? requestedBy, string? entityType, string? entityId, int? limit, IApprovalWorkflowService approvals, CancellationToken cancellationToken) =>
{
    var rows = await approvals.GetApprovalRequestsAsync(new ApprovalRequestFilter(ClampLimit(limit), ParseEnum<ApprovalRequestStatus>(status), ParseEnum<ApprovalRequestType>(type), requestedBy, entityType, entityId), cancellationToken);
    return Results.Ok(rows.Select(ToApprovalRequestDto));
});
app.MapGet("/approvals/{id:guid}", async (Guid id, IApprovalWorkflowService approvals, CancellationToken cancellationToken) =>
{
    var approval = await approvals.GetApprovalRequestAsync(new ApprovalRequestId(id), cancellationToken);
    return approval is null ? Results.NotFound() : Results.Ok(ToApprovalRequestDto(approval));
});
app.MapPost("/approvals/{id:guid}/approve", async (Guid id, ReasonRequest request, IApprovalWorkflowService approvals, CancellationToken cancellationToken) =>
    Results.Ok(ToApprovalRequestDto(await approvals.ApproveAsync(new ApprovalRequestId(id), request.Reason, cancellationToken))));
app.MapPost("/approvals/{id:guid}/reject", async (Guid id, ReasonRequest request, IApprovalWorkflowService approvals, CancellationToken cancellationToken) =>
    Results.Ok(ToApprovalRequestDto(await approvals.RejectAsync(new ApprovalRequestId(id), request.Reason, cancellationToken))));
app.MapPost("/approvals/{id:guid}/cancel", async (Guid id, ReasonRequest request, IApprovalWorkflowService approvals, CancellationToken cancellationToken) =>
    Results.Ok(ToApprovalRequestDto(await approvals.CancelAsync(new ApprovalRequestId(id), request.Reason, cancellationToken))));
app.MapPost("/approvals/{id:guid}/execute", async (Guid id, IApprovalWorkflowService approvals, CancellationToken cancellationToken) =>
    Results.Ok(ToGovernedActionResultDto(await approvals.ExecuteApprovedAsync(new ApprovalRequestId(id), cancellationToken))));
app.MapGet("/approvals/{id:guid}/decisions", async (Guid id, IApprovalWorkflowService approvals, CancellationToken cancellationToken) =>
    Results.Ok((await approvals.GetDecisionsAsync(new ApprovalRequestId(id), cancellationToken)).Select(ToApprovalDecisionDto)));

app.MapGet("/audit/events", async (IOperatorAuditService audit, int? limit, string? severity, string? eventType, string? entityType, string? entityId, string? correlationId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken cancellationToken) =>
{
    var filter = new OperatorAuditEventFilter(
        ClampLimit(limit),
        ParseEnum<OperatorAuditSeverity>(severity),
        ParseEnum<OperatorAuditEventType>(eventType),
        entityType,
        entityId,
        correlationId,
        fromUtc,
        toUtc);
    var events = await audit.GetRecentAsync(filter, cancellationToken);
    return Results.Ok(events.Select(ToOperatorAuditEventDto));
});

app.MapGet("/audit/events/{id:guid}", async (Guid id, IOperatorAuditService audit, CancellationToken cancellationToken) =>
{
    var auditEvent = await audit.GetAsync(new OperatorAuditEventId(id), cancellationToken);
    return auditEvent is null ? Results.NotFound() : Results.Ok(ToOperatorAuditEventDto(auditEvent));
});

app.MapGet("/audit/events/by-entity", async (string entityType, string entityId, int? limit, IOperatorAuditService audit, CancellationToken cancellationToken) =>
{
    var events = await audit.GetByEntityAsync(entityType, entityId, ClampLimit(limit), cancellationToken);
    return Results.Ok(events.Select(ToOperatorAuditEventDto));
});

app.MapGet("/audit/events/by-correlation/{correlationId}", async (string correlationId, int? limit, IOperatorAuditService audit, CancellationToken cancellationToken) =>
{
    var events = await audit.GetByCorrelationIdAsync(correlationId, ClampLimit(limit), cancellationToken);
    return Results.Ok(events.Select(ToOperatorAuditEventDto));
});

app.MapPost("/lmax-shadow/replay", async (LmaxShadowReplayApiRequest request, ILmaxShadowReplayService shadow, CancellationToken cancellationToken) =>
{
    var result = await shadow.ReplayAsync(ToLmaxShadowReplayRequest(request), cancellationToken);
    return Results.Ok(ToLmaxShadowReplayRunDto(result));
});

app.MapGet("/lmax-shadow/replay-runs", async (ILmaxShadowReplayService shadow, int? limit, string? status, string? inputSource, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken cancellationToken) =>
{
    var runs = await shadow.GetReplayRunsAsync(new LmaxShadowReplayRunFilter(ClampLimit(limit), ParseEnum<LmaxShadowReplayStatus>(status), ParseEnum<LmaxShadowInputSource>(inputSource), fromUtc, toUtc), cancellationToken);
    return Results.Ok(runs.Select(ToLmaxShadowReplayRunDto));
});

app.MapGet("/lmax-shadow/replay-runs/{id:guid}", async (Guid id, ILmaxShadowReplayService shadow, CancellationToken cancellationToken) =>
{
    var run = await shadow.GetReplayRunAsync(new LmaxShadowReplayRunId(id), cancellationToken);
    return run is null ? Results.NotFound() : Results.Ok(ToLmaxShadowReplayRunDto(run));
});

app.MapGet("/lmax-shadow/observations", async (ILmaxShadowReplayService shadow, int? limit, Guid? replayRunId, string? severity, string? status, string? type, string? symbol, string? brokerExecutionId, string? clientOrderId, CancellationToken cancellationToken) =>
{
    var observations = await shadow.GetObservationsAsync(new LmaxShadowObservationFilter(
        ClampLimit(limit),
        replayRunId is null ? null : new LmaxShadowReplayRunId(replayRunId.Value),
        ParseEnum<LmaxShadowObservationSeverity>(severity),
        ParseEnum<LmaxShadowObservationStatus>(status),
        ParseEnum<LmaxShadowObservationType>(type),
        symbol,
        brokerExecutionId,
        clientOrderId), cancellationToken);
    return Results.Ok(observations.Select(ToLmaxShadowObservationDto));
});

app.MapPost("/lmax-shadow/observations/{id:guid}/acknowledge", async (Guid id, ReasonRequest request, ILmaxShadowReplayService shadow, CancellationToken cancellationToken) =>
    Results.Ok(ToLmaxShadowObservationDto(await shadow.AcknowledgeObservationAsync(new LmaxShadowObservationId(id), request.Reason, cancellationToken))));

app.MapPost("/lmax-shadow/observations/{id:guid}/resolve", async (Guid id, ReasonRequest request, ILmaxShadowReplayService shadow, CancellationToken cancellationToken) =>
    Results.Ok(ToLmaxShadowObservationDto(await shadow.ResolveObservationAsync(new LmaxShadowObservationId(id), request.Reason, cancellationToken))));

app.MapPost("/lmax-shadow/observations/{id:guid}/ignore", async (Guid id, ReasonRequest request, ILmaxShadowReplayService shadow, CancellationToken cancellationToken) =>
    Results.Ok(ToLmaxShadowObservationDto(await shadow.IgnoreObservationAsync(new LmaxShadowObservationId(id), request.Reason, cancellationToken))));

app.MapGet("/exceptions", async (IExceptionCaseService service, int? limit, string? status, string? severity, string? type, string? source, string? assignedTo, string? instrument, string? entityType, string? entityId, string? correlationId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken cancellationToken) =>
{
    var cases = await service.GetCasesAsync(new ExceptionCaseFilter(
        ClampLimit(limit),
        ParseEnum<ExceptionCaseStatus>(status),
        ParseEnum<ExceptionCaseSeverity>(severity),
        ParseEnum<ExceptionCaseType>(type),
        ParseEnum<ExceptionCaseSource>(source),
        assignedTo,
        instrument,
        entityType,
        entityId,
        correlationId,
        fromUtc,
        toUtc), cancellationToken);
    return Results.Ok(cases.Select(ToExceptionCaseDto));
});

app.MapGet("/exceptions/{id:guid}", async (Guid id, IExceptionCaseService service, CancellationToken cancellationToken) =>
{
    var exceptionCase = await service.GetCaseAsync(new ExceptionCaseId(id), cancellationToken);
    return exceptionCase is null ? Results.NotFound() : Results.Ok(ToExceptionCaseDto(exceptionCase));
});

app.MapGet("/exceptions/{id:guid}/actions", async (Guid id, IExceptionCaseService service, CancellationToken cancellationToken) =>
{
    var actions = await service.GetActionsAsync(new ExceptionCaseId(id), cancellationToken);
    return Results.Ok(actions.Select(ToExceptionCaseActionDto));
});

app.MapGet("/exceptions/{id:guid}/notes", async (Guid id, IExceptionCaseService service, CancellationToken cancellationToken) =>
{
    var notes = await service.GetNotesAsync(new ExceptionCaseId(id), cancellationToken);
    return Results.Ok(notes.Select(ToExceptionCaseNoteDto));
});

app.MapPost("/exceptions", async (CreateExceptionCaseApiRequest request, IExceptionCaseService service, CancellationToken cancellationToken) =>
{
    var exceptionCase = await service.CreateManualCaseAsync(new CreateExceptionCaseRequest(
        request.Severity,
        request.Type,
        request.Source,
        request.Title,
        request.Description,
        request.EntityType,
        request.EntityId,
        request.InstrumentId is null ? null : new InstrumentId(request.InstrumentId.Value),
        request.Symbol,
        request.AssignedTo,
        request.Metadata), cancellationToken);
    return Results.Created($"/exceptions/{exceptionCase.Id.Value}", ToExceptionCaseDto(exceptionCase));
});

app.MapPost("/exceptions/{id:guid}/acknowledge", async (Guid id, ExceptionCaseReasonRequest request, IExceptionCaseService service, CancellationToken cancellationToken) =>
    Results.Ok(ToExceptionCaseDto(await service.AcknowledgeAsync(new ExceptionCaseId(id), request.Reason, cancellationToken))));

app.MapPost("/exceptions/{id:guid}/assign", async (Guid id, ExceptionCaseAssignRequest request, IExceptionCaseService service, CancellationToken cancellationToken) =>
    Results.Ok(ToExceptionCaseDto(await service.AssignAsync(new ExceptionCaseId(id), request.AssignedTo, cancellationToken))));

app.MapPost("/exceptions/{id:guid}/investigate", async (Guid id, ExceptionCaseReasonRequest request, IExceptionCaseService service, CancellationToken cancellationToken) =>
    Results.Ok(ToExceptionCaseDto(await service.MarkInvestigatingAsync(new ExceptionCaseId(id), request.Reason, cancellationToken))));

app.MapPost("/exceptions/{id:guid}/resolve", async (Guid id, ExceptionCaseReasonRequest request, IExceptionCaseService service, IApprovalWorkflowService approvals, GovernanceOptions governance, IOperatorContext operatorContext, CancellationToken cancellationToken) =>
{
    var current = await service.GetCaseAsync(new ExceptionCaseId(id), cancellationToken);
    if (current is null) return Results.NotFound();
    if (governance.FourEyesEnabled && governance.RequireApprovalForResolveCriticalException && current.Severity is ExceptionCaseSeverity.Critical or ExceptionCaseSeverity.Blocking)
    {
        var approval = await approvals.CreateApprovalRequestAsync(new CreateApprovalRequestRequest(ApprovalRequestType.ResolveCriticalException, "ExceptionCase", id.ToString("D"), request.Reason ?? string.Empty, new { exceptionCaseId = id, action = "ResolveCriticalException", current.Severity }), cancellationToken);
        return Results.Ok(ToGovernedActionResultDto(PendingResult(approval, operatorContext)));
    }
    return Results.Ok(ToExceptionCaseDto(await service.ResolveAsync(new ExceptionCaseId(id), request.Reason ?? string.Empty, cancellationToken)));
});

app.MapPost("/exceptions/{id:guid}/false-positive", async (Guid id, ExceptionCaseReasonRequest request, IExceptionCaseService service, IApprovalWorkflowService approvals, GovernanceOptions governance, IOperatorContext operatorContext, CancellationToken cancellationToken) =>
{
    var current = await service.GetCaseAsync(new ExceptionCaseId(id), cancellationToken);
    if (current is null) return Results.NotFound();
    if (governance.FourEyesEnabled && governance.RequireApprovalForFalsePositiveBlockingException && current.Severity is ExceptionCaseSeverity.Critical or ExceptionCaseSeverity.Blocking)
    {
        var approval = await approvals.CreateApprovalRequestAsync(new CreateApprovalRequestRequest(ApprovalRequestType.MarkExceptionFalsePositive, "ExceptionCase", id.ToString("D"), request.Reason ?? string.Empty, new { exceptionCaseId = id, action = "MarkExceptionFalsePositive", current.Severity }), cancellationToken);
        return Results.Ok(ToGovernedActionResultDto(PendingResult(approval, operatorContext)));
    }
    return Results.Ok(ToExceptionCaseDto(await service.MarkFalsePositiveAsync(new ExceptionCaseId(id), request.Reason ?? string.Empty, cancellationToken)));
});

app.MapPost("/exceptions/{id:guid}/waive", async (Guid id, ExceptionCaseReasonRequest request, IExceptionCaseService service, IApprovalWorkflowService approvals, GovernanceOptions governance, IOperatorContext operatorContext, CancellationToken cancellationToken) =>
{
    var current = await service.GetCaseAsync(new ExceptionCaseId(id), cancellationToken);
    if (current is null) return Results.NotFound();
    if (governance.FourEyesEnabled && governance.RequireApprovalForWaiveBlockingException && current.Severity is ExceptionCaseSeverity.Critical or ExceptionCaseSeverity.Blocking)
    {
        var approval = await approvals.CreateApprovalRequestAsync(new CreateApprovalRequestRequest(ApprovalRequestType.WaiveException, "ExceptionCase", id.ToString("D"), request.Reason ?? string.Empty, new { exceptionCaseId = id, action = "WaiveException", current.Severity }), cancellationToken);
        return Results.Ok(ToGovernedActionResultDto(PendingResult(approval, operatorContext)));
    }
    return Results.Ok(ToExceptionCaseDto(await service.WaiveAsync(new ExceptionCaseId(id), request.Reason ?? string.Empty, cancellationToken)));
});

app.MapPost("/exceptions/{id:guid}/reopen", async (Guid id, ExceptionCaseReasonRequest request, IExceptionCaseService service, CancellationToken cancellationToken) =>
    Results.Ok(ToExceptionCaseDto(await service.ReopenAsync(new ExceptionCaseId(id), request.Reason, cancellationToken))));

app.MapPost("/exceptions/{id:guid}/notes", async (Guid id, ExceptionCaseNoteRequest request, IExceptionCaseService service, CancellationToken cancellationToken) =>
    Results.Ok(ToExceptionCaseNoteDto(await service.AddNoteAsync(new ExceptionCaseId(id), request.Note, cancellationToken))));

app.MapGet("/ops/jobs/definitions", async (IOperationalJobRunner runner, CancellationToken cancellationToken) =>
    Results.Ok((await runner.GetDefinitionsAsync(cancellationToken)).Select(ToOperationalJobDefinitionDto)));

app.MapGet("/ops/jobs/runs", async (IOperationalJobRunner runner, string? status, string? jobType, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int? limit, CancellationToken cancellationToken) =>
{
    var filter = new OperationalJobRunFilter(ClampLimit(limit), ParseEnum<OperationalJobRunStatus>(status), ParseEnum<OperationalJobType>(jobType), fromUtc, toUtc);
    return Results.Ok((await runner.GetRecentJobRunsAsync(filter, cancellationToken)).Select(ToOperationalJobRunDto));
});

app.MapGet("/ops/jobs/runs/{id:guid}", async (Guid id, IOperationalJobRunner runner, CancellationToken cancellationToken) =>
{
    var run = await runner.GetJobRunAsync(new OperationalJobRunId(id), cancellationToken);
    return run is null ? Results.NotFound() : Results.Ok(ToOperationalJobRunDto(run));
});

app.MapGet("/ops/jobs/runs/{id:guid}/steps", async (Guid id, IOperationalJobRunner runner, CancellationToken cancellationToken) =>
    Results.Ok((await runner.GetJobStepsAsync(new OperationalJobRunId(id), cancellationToken)).Select(ToOperationalJobStepDto)));

app.MapGet("/ops/jobs/runs/{id:guid}/events", async (Guid id, IOperationalJobRunner runner, CancellationToken cancellationToken) =>
    Results.Ok((await runner.GetJobEventsAsync(new OperationalJobRunId(id), cancellationToken)).Select(ToOperationalJobRunEventDto)));

app.MapPost("/ops/jobs/run", async (RunOperationalJobApiRequest request, IOperationalJobRunner runner, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { message = "A reason is required to run an operational job." });
    if (!Enum.TryParse<OperationalJobType>(request.JobType, true, out var jobType)) return Results.BadRequest(new { message = $"Unknown operational job type '{request.JobType}'." });
    try
    {
        return Results.Ok(ToOperationalJobRunDto(await runner.RunJobAsync(new RunOperationalJobRequest(jobType, request.Reason, request.Input), cancellationToken)));
    }
    catch (DomainRuleViolationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/ops/jobs/runs/{id:guid}/retry", async (Guid id, ReasonRequest request, IOperationalJobRunner runner, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { message = "A reason is required to retry an operational job." });
    try
    {
        return Results.Ok(ToOperationalJobRunDto(await runner.RetryJobAsync(new OperationalJobRunId(id), request.Reason, cancellationToken)));
    }
    catch (DomainRuleViolationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapGet("/ops/daily-summary", async (DateOnly? date, IDailyOperationsService service, IClock clock, CancellationToken cancellationToken) =>
    Results.Ok(ToDailyOperationsSummaryDto(await service.GetTodaySummaryAsync(date ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), cancellationToken))));

app.MapGet("/ops/daily-checklist", async (DateOnly? date, IDailyOperationsService service, IClock clock, CancellationToken cancellationToken) =>
    Results.Ok((await service.GetDailyChecklistAsync(date ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), cancellationToken)).Select(ToDailyChecklistItemDto)));

app.MapGet("/ops/timeline", async (DateOnly? date, IDailyOperationsService service, IClock clock, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetOperationalTimelineAsync(date ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), cancellationToken)));

app.MapPost("/ops/run-reference-check", async (ReasonRequest request, IOperationalJobRunner runner, CancellationToken cancellationToken) =>
    Results.Ok(ToOperationalJobRunDto(await runner.RunJobAsync(new RunOperationalJobRequest(OperationalJobType.ReferenceDataIntegrityCheck, request.Reason), cancellationToken))));

app.MapPost("/ops/build-bars", async (ReasonRequest request, IOperationalJobRunner runner, CancellationToken cancellationToken) =>
    Results.Ok(ToOperationalJobRunDto(await runner.RunJobAsync(new RunOperationalJobRequest(OperationalJobType.BuildMarketDataBars, request.Reason), cancellationToken))));

app.MapPost("/ops/promote-ready-weights", async (ReasonRequest request, IOperationalJobRunner runner, CancellationToken cancellationToken) =>
    Results.Ok(ToOperationalJobRunDto(await runner.RunJobAsync(new RunOperationalJobRequest(OperationalJobType.PromoteReadyWeightBatches, request.Reason), cancellationToken))));

app.MapPost("/ops/process-pending-model-runs", async (ReasonRequest request, IOperationalJobRunner runner, CancellationToken cancellationToken) =>
    Results.Ok(ToOperationalJobRunDto(await runner.RunJobAsync(new RunOperationalJobRequest(OperationalJobType.ProcessPendingModelRuns, request.Reason), cancellationToken))));

app.MapPost("/ops/run-eod-reconciliation", async (ReasonRequest request, IOperationalJobRunner runner, CancellationToken cancellationToken) =>
    Results.Ok(ToOperationalJobRunDto(await runner.RunJobAsync(new RunOperationalJobRequest(OperationalJobType.RunEodReconciliation, request.Reason), cancellationToken))));

app.MapGet("/ops/runbooks/definitions", async (IOperationalRunbookRunner runner, CancellationToken cancellationToken) =>
    Results.Ok((await runner.GetRunbookDefinitionsAsync(cancellationToken)).Select(ToOperationalRunbookDefinitionDto)));

app.MapGet("/ops/runbooks/definitions/{id:guid}", async (Guid id, IOperationalRunbookRunner runner, CancellationToken cancellationToken) =>
{
    var definition = await runner.GetRunbookDefinitionAsync(new OperationalRunbookDefinitionId(id), cancellationToken);
    if (definition is null) return Results.NotFound();
    var steps = await runner.GetRunbookStepDefinitionsAsync(definition.Id, cancellationToken);
    return Results.Ok(new { definition = ToOperationalRunbookDefinitionDto(definition), steps = steps.Select(ToOperationalRunbookStepDefinitionDto) });
});

app.MapGet("/ops/runbooks/runs", async (IOperationalRunbookRunner runner, string? runbookType, string? status, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int? limit, CancellationToken cancellationToken) =>
{
    var filter = new OperationalRunbookRunFilter(ClampLimit(limit), ParseEnum<OperationalRunbookType>(runbookType), ParseEnum<OperationalRunbookStatus>(status), fromUtc, toUtc);
    return Results.Ok((await runner.GetRunbookRunsAsync(filter, cancellationToken)).Select(ToOperationalRunbookRunDto));
});

app.MapGet("/ops/runbooks/runs/{id:guid}", async (Guid id, IOperationalRunbookRunner runner, CancellationToken cancellationToken) =>
{
    var run = await runner.GetRunbookRunAsync(new OperationalRunbookRunId(id), cancellationToken);
    return run is null ? Results.NotFound() : Results.Ok(ToOperationalRunbookRunDto(run));
});

app.MapGet("/ops/runbooks/runs/{id:guid}/steps", async (Guid id, IOperationalRunbookRunner runner, CancellationToken cancellationToken) =>
    Results.Ok((await runner.GetRunbookStepRunsAsync(new OperationalRunbookRunId(id), cancellationToken)).Select(ToOperationalRunbookStepRunDto)));

app.MapPost("/ops/runbooks/run", async (RunOperationalRunbookApiRequest request, IOperationalRunbookRunner runner, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { message = "A reason is required to run an operational runbook." });
    if (!Enum.TryParse<OperationalRunbookType>(request.RunbookType, true, out var type)) return Results.BadRequest(new { message = $"Unknown runbook type '{request.RunbookType}'." });
    try
    {
        return Results.Ok(ToOperationalRunbookRunDto(await runner.RunRunbookAsync(new RunOperationalRunbookRequest(type, request.Reason, request.Input), cancellationToken)));
    }
    catch (DomainRuleViolationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapPost("/ops/runbooks/runs/{id:guid}/run-next-step", async (Guid id, ReasonRequest request, IOperationalRunbookRunner runner, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { message = "A reason is required to continue an operational runbook." });
    try { return Results.Ok(ToOperationalRunbookRunDto(await runner.RunNextStepAsync(new OperationalRunbookRunId(id), request.Reason, cancellationToken))); }
    catch (DomainRuleViolationException ex) { return Results.BadRequest(new { message = ex.Message }); }
});

app.MapPost("/ops/runbooks/runs/{id:guid}/complete-manual-step", async (Guid id, CompleteManualRunbookStepRequest request, IOperationalRunbookRunner runner, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { message = "A reason is required to complete a manual runbook step." });
    if (!Guid.TryParse(request.StepRunId, out var stepRunId)) return Results.BadRequest(new { message = "A valid stepRunId is required." });
    try { return Results.Ok(ToOperationalRunbookRunDto(await runner.CompleteManualStepAsync(new OperationalRunbookRunId(id), new OperationalRunbookStepRunId(stepRunId), request.Reason, cancellationToken))); }
    catch (DomainRuleViolationException ex) { return Results.BadRequest(new { message = ex.Message }); }
});

app.MapPost("/ops/runbooks/runs/{id:guid}/cancel", async (Guid id, ReasonRequest request, IOperationalRunbookRunner runner, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { message = "A reason is required to cancel an operational runbook." });
    try { return Results.Ok(ToOperationalRunbookRunDto(await runner.CancelRunbookAsync(new OperationalRunbookRunId(id), request.Reason, cancellationToken))); }
    catch (DomainRuleViolationException ex) { return Results.BadRequest(new { message = ex.Message }); }
});

app.MapPost("/ops/runbooks/runs/{id:guid}/retry", async (Guid id, ReasonRequest request, IOperationalRunbookRunner runner, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { message = "A reason is required to retry an operational runbook." });
    try { return Results.Ok(ToOperationalRunbookRunDto(await runner.RetryRunbookAsync(new OperationalRunbookRunId(id), request.Reason, cancellationToken))); }
    catch (DomainRuleViolationException ex) { return Results.BadRequest(new { message = ex.Message }); }
});

app.MapGet("/ops/schedules", async (IOperationalRunbookRunner runner, LocalSchedulerOptions options, CancellationToken cancellationToken) =>
    Results.Ok(new { schedulerEnabled = options.Enabled, pollIntervalSeconds = options.PollIntervalSeconds, value = (await runner.GetSchedulesAsync(cancellationToken)).Select(ToOperationalScheduleDefinitionDto) }));

app.MapPost("/ops/schedules", async (OperationalScheduleDefinitionRequest request, IOperationalRunbookRunner runner, IClock clock, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { message = "A reason is required to create a local schedule." });
    if (!Guid.TryParse(request.RunbookDefinitionId, out var definitionId)) return Results.BadRequest(new { message = "RunbookDefinitionId must be a GUID." });
    try
    {
        var now = clock.UtcNow;
        var schedule = new OperationalScheduleDefinition(
            OperationalScheduleDefinitionId.New(),
            request.Name.Trim(),
            new OperationalRunbookDefinitionId(definitionId),
            request.IsEnabled,
            request.CronExpression,
            request.FixedIntervalMinutes,
            string.IsNullOrWhiteSpace(request.TimeZoneId) ? "UTC" : request.TimeZoneId.Trim(),
            request.NextRunAtUtc,
            request.LastRunAtUtc,
            now,
            now);
        return Results.Ok(ToOperationalScheduleDefinitionDto(await runner.UpsertScheduleAsync(schedule, request.Reason, cancellationToken)));
    }
    catch (DomainRuleViolationException ex) { return Results.BadRequest(new { message = ex.Message }); }
});

app.MapPut("/ops/schedules/{id:guid}", async (Guid id, OperationalScheduleDefinitionRequest request, IOperationalRunbookRunner runner, IClock clock, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { message = "A reason is required to update a local schedule." });
    if (!Guid.TryParse(request.RunbookDefinitionId, out var definitionId)) return Results.BadRequest(new { message = "RunbookDefinitionId must be a GUID." });
    try
    {
        var now = clock.UtcNow;
        var existing = (await runner.GetSchedulesAsync(cancellationToken)).FirstOrDefault(x => x.Id.Value == id);
        var schedule = new OperationalScheduleDefinition(
            new OperationalScheduleDefinitionId(id),
            request.Name.Trim(),
            new OperationalRunbookDefinitionId(definitionId),
            request.IsEnabled,
            request.CronExpression,
            request.FixedIntervalMinutes,
            string.IsNullOrWhiteSpace(request.TimeZoneId) ? "UTC" : request.TimeZoneId.Trim(),
            request.NextRunAtUtc,
            request.LastRunAtUtc,
            existing?.CreatedAtUtc ?? now,
            now);
        return Results.Ok(ToOperationalScheduleDefinitionDto(await runner.UpsertScheduleAsync(schedule, request.Reason, cancellationToken)));
    }
    catch (DomainRuleViolationException ex) { return Results.BadRequest(new { message = ex.Message }); }
});

app.MapGet("/instruments", async (IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    return state.Instruments.OrderBy(x => x.Symbol).Select(ToInstrumentDto);
});
app.MapGet("/venues", async (IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    return state.Venues.OrderBy(x => x.Name).Select(ToVenueDto);
});

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app, string persistenceProvider)
{
    if (!string.Equals(persistenceProvider, "SqlServerLocal", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    await using var scope = app.Services.CreateAsyncScope();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var initializer = scope.ServiceProvider.GetRequiredService<LocalDatabaseInitializer>();
    var db = scope.ServiceProvider.GetRequiredService<IntradayDbContext>();
    if (configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
    {
        await initializer.ApplyMigrationsAsync(CancellationToken.None);
    }

    if (configuration.GetValue("Database:SeedReferenceDataOnStartup", true))
    {
        if (!await db.Database.CanConnectAsync(CancellationToken.None))
        {
            app.Logger.LogWarning("Skipping reference seed because the LocalDB schema is not reachable. Run scripts/update-local-db.ps1 or enable Database:ApplyMigrationsOnStartup.");
            return;
        }

        await initializer.SeedReferenceDataAsync(CancellationToken.None);
    }

    if (configuration.GetValue("Database:SeedDemoDataOnStartup", false))
    {
        await initializer.SeedDemoDataAsync(CancellationToken.None);
    }
}

static void ValidateSafety(WebApplication app, string persistenceProvider)
{
    using var scope = app.Services.CreateScope();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var gateway = scope.ServiceProvider.GetRequiredService<IVenueExecutionGateway>();
    if (!configuration.GetValue("Safety:AllowLiveTrading", false) && gateway is not FakeLmaxGateway)
    {
        throw new InvalidOperationException("Live trading is disabled and the registered execution gateway is not FakeLmaxGateway.");
    }

    if (configuration.GetValue("Safety:RequireFakeExecutionGateway", true) && gateway is not FakeLmaxGateway)
    {
        throw new InvalidOperationException("Safety requires FakeLmaxGateway.");
    }

    if (!configuration.GetValue("Safety:AllowExternalConnections", false) && scope.ServiceProvider.GetRequiredService<IMarketDataProvider>() is not FakeMarketDataProvider)
    {
        throw new InvalidOperationException("External connections are disabled and the market data provider is not local/fake.");
    }

    app.Logger.LogInformation(
        "Startup safety: Environment={Environment} PersistenceProvider={PersistenceProvider} DatabaseTarget={DatabaseTarget} ExecutionGateway={ExecutionGateway} MarketDataProvider={MarketDataProvider} AllowExternalConnections={AllowExternalConnections} AllowLiveTrading={AllowLiveTrading}",
        app.Environment.EnvironmentName,
        persistenceProvider,
        string.Equals(persistenceProvider, "SqlServerLocal", StringComparison.OrdinalIgnoreCase) ? "LocalDB" : "InMemory",
        gateway.GetType().Name,
        scope.ServiceProvider.GetRequiredService<IMarketDataProvider>().GetType().Name,
        configuration.GetValue("Safety:AllowExternalConnections", false),
        configuration.GetValue("Safety:AllowLiveTrading", false));
}

static async Task ValidateReferenceDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    if (!configuration.GetValue("ReferenceDataIntegrity:CheckOnStartup", true))
    {
        return;
    }

    var check = await scope.ServiceProvider.GetRequiredService<IReferenceDataIntegrityService>().CheckAsync(CancellationToken.None);
    app.Logger.LogInformation("Reference data integrity checked: BlockingIssues={BlockingIssueCount} WarningIssues={WarningIssueCount}", check.BlockingIssueCount, check.WarningIssueCount);
    if (check.BlockingIssueCount > 0 && configuration.GetValue("ReferenceDataIntegrity:FailStartupOnBlockingIssues", true))
    {
        throw new InvalidOperationException($"Reference data integrity check failed with {check.BlockingIssueCount} blocking issue(s). Run scripts/check-reference-data.ps1 for details or reset the local dev database if it contains old duplicate seed rows.");
    }
}

static async Task RecordStartupAuditAsync(WebApplication app, string persistenceProvider)
{
    await using var scope = app.Services.CreateAsyncScope();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var gateway = scope.ServiceProvider.GetRequiredService<IVenueExecutionGateway>();
    var marketDataProvider = scope.ServiceProvider.GetRequiredService<IMarketDataProvider>();
    var audit = scope.ServiceProvider.GetRequiredService<IOperatorAuditService>();
    await audit.RecordAsync(new OperatorAuditRecordRequest(
        OperatorAuditEventType.SafetyStartupValidation,
        OperatorAuditSeverity.Info,
        OperatorAuditResult.Succeeded,
        "Api",
        "Startup safety validation completed.",
        "Startup",
        app.Environment.EnvironmentName,
        Metadata: new
        {
            environment = app.Environment.EnvironmentName,
            persistenceProvider,
            executionGateway = gateway.GetType().Name,
            marketDataProvider = marketDataProvider.GetType().Name,
            allowExternalConnections = configuration.GetValue("Safety:AllowExternalConnections", false),
            allowLiveTrading = configuration.GetValue("Safety:AllowLiveTrading", false)
        },
        Actor: new OperatorIdentity(OperatorAuditActorType.Api, "api", "QQ.Production.Intraday.Api")),
        CancellationToken.None);
}

static int ClampLimit(int? limit) => Math.Clamp(limit ?? 100, 1, 500);

static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct
    => !string.IsNullOrWhiteSpace(value) && Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : null;

static ModelRunDto ToModelRunDto(ModelRun x)
    => new(
        x.Id.Value.ToString("D"),
        x.FundId.Value.ToString("D"),
        x.ModelName,
        x.AsOfUtc,
        x.ReceivedAtUtc,
        x.EffectiveAtUtc,
        x.FrequencyMinutes,
        x.NavUsd,
        x.Status.ToString(),
        x.InputHash,
        x.SourceFileName,
        x.IsProcessed,
        x.TargetQuantityMode.ToString());

static LmaxReportImportRunDto ToLmaxReportImportRunDto(LmaxReportImportRun x)
    => new(x.Id.Value.ToString("D"), x.ReportType.ToString(), x.ReportDate, x.VenueId.Value.ToString("D"), x.BrokerAccountId.Value.ToString("D"), x.Status.ToString(), x.FileName, x.FilePath, x.FileHash, x.RowCount, x.CreatedAtUtc, x.StartedAtUtc, x.CompletedAtUtc, x.ArchivedPath, x.RejectedPath, x.Message);

static LmaxReportValidationIssueDto ToLmaxReportValidationIssueDto(LmaxReportValidationIssue x)
    => new(x.Id.ToString("D"), x.ImportRunId.Value.ToString("D"), x.IssueType.ToString(), x.Severity.ToString(), x.Message, x.RowNumber, x.RawLine, x.CreatedAtUtc);

static LmaxReportImportResultDto ToLmaxReportImportResultDto(LmaxReportImportResult x)
    => new(x.ImportRunId.Value.ToString("D"), x.Status.ToString(), x.RowCount, x.BlockingIssueCount, x.Issues.Select(ToLmaxReportValidationIssueDto).ToList(), x.Message);

static LmaxIndividualTradeDto ToLmaxIndividualTradeDto(LmaxIndividualTrade x)
    => new(x.Id.Value.ToString("D"), x.ImportRunId.Value.ToString("D"), x.ReportDate, x.VenueId.Value.ToString("D"), x.BrokerAccountId.Value.ToString("D"), x.ExecutionId, x.MtfExecutionId, x.TimestampUtc, x.TradeQuantity, x.TradePrice, x.TradeDate, x.LmaxInstrumentId, x.LmaxSymbol, x.InstrumentId?.Value.ToString("D"), x.InstructionId, x.OrderId, x.OrderType, x.TotalProfitLoss, x.TotalCommission, x.AccountId, x.UnitsBoughtSold, x.NotionalValue, x.TradeUti, x.CreatedAtUtc);

static LmaxTradeSummaryDto ToLmaxTradeSummaryDto(LmaxTradeSummary x)
    => new(x.Id.Value.ToString("D"), x.ImportRunId.Value.ToString("D"), x.ReportDate, x.VenueId.Value.ToString("D"), x.BrokerAccountId.Value.ToString("D"), x.DateTimeUtc, x.Instrument, x.InstrumentId?.Value.ToString("D"), x.Type, x.Currency, x.Contracts, x.AveragePrice, x.CommissionRounded, x.NotionalValue, x.LmaxSymbol, x.UserPlacingOrder, x.CommissionFullPrecision, x.AccountId, x.CreatedAtUtc);

static LmaxCurrencyWalletDto ToLmaxCurrencyWalletDto(LmaxCurrencyWallet x)
    => new(x.Id.Value.ToString("D"), x.ImportRunId.Value.ToString("D"), x.ReportDate, x.VenueId.Value.ToString("D"), x.BrokerAccountId.Value.ToString("D"), x.Currency, x.BalanceNetDeposits, x.Adjustments, x.InterAccountTransfers, x.ProfitLoss, x.Commission, x.Dividends, x.Financing, x.WalletBalance, x.RateToBaseCcy, x.BaseCurrency, x.BalanceNetDepositsBaseUsd, x.AdjustmentsBaseUsd, x.InterAccountTransfersBaseUsd, x.ProfitLossBaseUsd, x.CommissionBaseUsd, x.DividendsBaseUsd, x.FinancingBaseUsd, x.WalletBalanceBaseUsd, x.AccountId, x.CreatedAtUtc);

static FakeLmaxEodReportGenerationDto ToFakeLmaxEodReportGenerationDto(FakeLmaxEodReportGenerationResult x)
    => new(x.ReportDate, x.IndividualTradesPath, x.TradesSummaryPath, x.CurrencyWalletsPath, x.IndividualTradeCount, x.TradeSummaryCount, x.CurrencyWalletCount, x.MutationMode.ToString());

static EodReconciliationResultDto ToEodReconciliationResultDto(EodReconciliationResult x)
    => new(x.RunId.ToString("D"), x.ReportDate, x.BreakCount, x.BlockingBreakCount, x.Breaks.Select(ToEodReconciliationBreakDto).ToList());

static EodReconciliationRunDto ToEodReconciliationRunDto(EodReconciliationRun x)
    => new(x.Id.ToString("D"), x.ReportDate, x.VenueId.Value.ToString("D"), x.BrokerAccountId.Value.ToString("D"), x.CreatedAtUtc, x.HasBlockingBreaks);

static EodReconciliationBreakDto ToEodReconciliationBreakDto(EodReconciliationBreak x)
    => new(x.Id.ToString("D"), x.RunId.ToString("D"), x.Type.ToString(), x.Severity.ToString(), x.Status.ToString(), x.InstrumentId?.Value.ToString("D"), x.Description, x.BrokerExecutionId, x.InternalFillId, x.CreatedAtUtc);

static EodPnlSummaryDto ToEodPnlSummaryDto(EodPnlSummary x)
    => new(x.ReportDate, x.VenueName, x.BrokerAccountCode, x.TotalWalletBalanceUsd, x.TotalProfitLossUsd, x.TotalCommissionUsd, x.TotalDividendsUsd, x.TotalFinancingUsd, x.TotalNetPnlUsd, x.CurrencyRows.Select(row => new EodPnlCurrencyRowDto(row.Currency, row.WalletBalance, row.RateToBaseCcy, row.WalletBalanceBaseUsd, row.ProfitLoss, row.ProfitLossBaseUsd, row.Commission, row.CommissionBaseUsd, row.Dividends, row.DividendsBaseUsd, row.Financing, row.FinancingBaseUsd)).ToList());

static ModelWeightBatchDto ToModelWeightBatchDto(ModelWeightBatch x)
    => new(
        x.Id.Value.ToString("D"),
        x.ExternalBatchId,
        x.SourceSystem.ToString(),
        x.FundCode,
        x.FundId?.Value.ToString("D"),
        x.ModelName,
        x.AsOfUtc,
        x.EffectiveAtUtc,
        x.FrequencyMinutes,
        x.NavUsd,
        x.TargetQuantityMode.ToString(),
        x.Status.ToString(),
        x.ExpectedRowCount,
        x.ContentHash,
        x.CreatedAtUtc,
        x.ReadyAtUtc,
        x.AcceptedAtUtc,
        x.PromotedAtUtc,
        x.RejectedAtUtc,
        x.PromotedModelRunId?.Value.ToString("D"),
        x.Message);

static ModelWeightRowDto ToModelWeightRowDto(ModelWeightRow x)
    => new(x.Id.Value.ToString("D"), x.BatchId.Value.ToString("D"), x.RawSecurityId, x.Symbol, x.InstrumentId?.Value.ToString("D"), x.Weight, x.CreatedAtUtc);

static ModelWeightValidationIssueDto ToModelWeightValidationIssueDto(ModelWeightValidationIssue x)
    => new(x.Id.ToString("D"), x.BatchId.Value.ToString("D"), x.IssueType.ToString(), x.Severity.ToString(), x.Message, x.RowId?.Value.ToString("D"), x.RowNumber, x.CreatedAtUtc);

static ModelWeightPromotionResultDto ToModelWeightPromotionResultDto(ModelWeightPromotionResult x)
    => new(
        x.BatchId?.Value.ToString("D"),
        x.Status?.ToString(),
        x.PromotedModelRunId?.Value.ToString("D"),
        x.ModelRunId?.Value.ToString("D"),
        x.ValidationIssueCount,
        x.Issues.Select(ToModelWeightValidationIssueDto).ToList(),
        x.Message,
        x.Succeeded,
        x.AlreadyPromoted);

static CreateFakeModelWeightBatchRequest ToApplicationRequest(CreateFakeModelWeightBatchApiRequest request)
    => new(
        request.ExternalBatchId,
        request.SourceSystem,
        string.IsNullOrWhiteSpace(request.FundCode) ? "QQ_MASTER" : request.FundCode,
        string.IsNullOrWhiteSpace(request.ModelName) ? "IntradayFxModel" : request.ModelName,
        request.AsOfUtc,
        request.EffectiveAtUtc,
        request.FrequencyMinutes <= 0 ? 15 : request.FrequencyMinutes,
        request.NavUsd <= 0 ? 1_000_000m : request.NavUsd,
        request.TargetQuantityMode,
        request.Status == ModelWeightBatchStatus.Draft ? ModelWeightBatchStatus.Ready : request.Status,
        request.Weights is { Count: > 0 }
            ? request.Weights.Select(x => new CreateFakeModelWeightRowRequest(x.RawSecurityId, x.Symbol, x.Weight)).ToList()
            : [new CreateFakeModelWeightRowRequest("EURUSD", "EURUSD", -0.10m)]);

static InstrumentDto ToInstrumentDto(Instrument x)
    => new(x.Id.Value.ToString("D"), x.Symbol, x.AssetClass.ToString(), x.BaseCurrency.ToString(), x.QuoteCurrency.ToString(), x.PricePrecision, x.QuantityPrecision, x.IsEnabled, x.IsTradingEnabled, x.IsReportImportEnabled, x.IsMarketDataEnabled);

static VenueDto ToVenueDto(Venue x)
    => new(x.Id.Value.ToString("D"), x.Name, x.VenueType.ToString(), x.IsEnabled, x.IsTradingEnabled, x.IsReportImportEnabled, x.IsMarketDataEnabled);

static KillSwitchDto ToKillSwitchDto(KillSwitchState x)
    => new(x.Id.ToString("D"), x.IsActive, x.Reason, x.UpdatedAtUtc);

static ReconciliationBreakDto ToReconciliationBreakDto(ReconciliationBreak x, IReadOnlyDictionary<Guid, ReconciliationRun> runs, IReadOnlyDictionary<InstrumentId, string> symbols)
{
    runs.TryGetValue(x.ReconciliationRunId, out var run);
    var symbol = x.InstrumentId is null ? null : symbols.GetValueOrDefault(x.InstrumentId.Value);
    return new ReconciliationBreakDto(
        x.Id.ToString("D"),
        x.ReconciliationRunId.ToString("D"),
        run?.ModelRunId.Value.ToString("D"),
        run?.Phase.ToString(),
        x.Type.ToString(),
        x.Severity.ToString(),
        x.Status.ToString(),
        x.InstrumentId?.Value.ToString("D"),
        symbol,
        x.Description,
        run?.CreatedAtUtc);
}

static TradeIntentDto ToTradeIntentDto(TradeIntent x, IReadOnlyDictionary<InstrumentId, string> symbols)
    => new(x.Id.Value.ToString("D"), x.ModelRunId.Value.ToString("D"), x.FundId.Value.ToString("D"), x.InstrumentId.Value.ToString("D"), symbols.GetValueOrDefault(x.InstrumentId), x.Side.ToString(), x.RequestedBaseQuantity, x.RequestedVenueQuantity, x.Reason, x.Status.ToString(), x.CreatedAtUtc);

static RiskDecisionDto ToRiskDecisionDto(
    RiskDecision x,
    IReadOnlyDictionary<TradeIntentId, TradeIntent> intents,
    IReadOnlyDictionary<InstrumentId, string> symbols,
    IReadOnlyDictionary<VenueId, string> venues,
    IReadOnlyDictionary<Guid, RiskLimitSet> riskSets,
    IReadOnlyList<RiskDecisionDetail> details)
{
    intents.TryGetValue(x.TradeIntentId, out var intent);
    var instrumentId = x.InstrumentId ?? intent?.InstrumentId;
    var summary = SelectRiskDecisionSummary(x, details);
    var message = string.Equals(x.Explanation, RiskRejectReason.None.ToString(), StringComparison.OrdinalIgnoreCase)
        ? "All configured risk checks passed."
        : x.Explanation;
    RiskLimitSet? riskSet = null;
    if (x.RiskLimitSetId is Guid riskLimitSetId)
    {
        riskSets.TryGetValue(riskLimitSetId, out riskSet);
    }
    return new RiskDecisionDto(
        x.Id.ToString("D"),
        x.TradeIntentId.Value.ToString("D"),
        x.ModelRunId?.Value.ToString("D") ?? intent?.ModelRunId.Value.ToString("D"),
        instrumentId?.Value.ToString("D"),
        instrumentId is null ? null : symbols.GetValueOrDefault(instrumentId.Value),
        x.VenueId?.Value.ToString("D"),
        x.VenueId is null ? null : venues.GetValueOrDefault(x.VenueId.Value),
        x.RiskLimitSetId?.ToString("D"),
        riskSet?.Name,
        riskSet?.Version,
        x.Status.ToString(),
        x.RejectReason == RiskRejectReason.None ? null : x.RejectReason.ToString(),
        message,
        x.CreatedAtUtc,
        summary?.ObservedValue,
        summary?.LimitValue,
        summary?.Unit,
        summary?.CheckName,
        details.OrderBy(d => d.CreatedAtUtc).Select(ToRiskDecisionDetailDto).ToList());
}

static RiskDecisionDetail? SelectRiskDecisionSummary(RiskDecision decision, IReadOnlyList<RiskDecisionDetail> details)
{
    var failed = details.FirstOrDefault(x => x.Status is RiskDecisionCheckStatus.Failed or RiskDecisionCheckStatus.Blocked);
    if (failed is not null) return failed;

    var numeric = details
        .Where(x => x.ObservedValue is not null && x.LimitValue is not null && x.LimitValue != 0)
        .OrderByDescending(x => Math.Abs(x.ObservedValue!.Value / x.LimitValue!.Value))
        .FirstOrDefault();
    if (numeric is not null) return numeric;

    return details.FirstOrDefault();
}

static RiskDecisionDetailDto ToRiskDecisionDetailDto(RiskDecisionDetail x)
    => new(x.Id.ToString("D"), x.RiskDecisionId.ToString("D"), x.CheckName, x.Status.ToString(), x.RejectReason?.ToString(), x.ObservedValue, x.LimitValue, x.Unit, x.Message, x.CreatedAtUtc);

static RiskLimitSetDto ToRiskLimitSetDto(RiskLimitSet x)
    => new(x.Id.ToString("D"), x.FundId.Value.ToString("D"), x.ModelName, x.Name, x.Version, x.Status.ToString(), x.IsActive, x.EffectiveFromUtc, x.EffectiveToUtc, x.CreatedAtUtc, x.CreatedBy, x.ActivatedAtUtc, x.ActivatedBy, x.RetiredAtUtc, x.RetiredBy, x.Description, x.GlobalTradingEnabled, x.MaxGrossExposureUsd, (int)x.MaxModelRunAge.TotalSeconds, (int)x.MaxMarketDataAge.TotalSeconds, x.PositionToleranceBaseQuantity, x.MinDriftVenueQuantity);

static RiskLimitDto ToRiskLimitDto(RiskLimit x)
    => new(x.Id.ToString("D"), x.RiskLimitSetId.ToString("D"), x.Name, x.Value, x.Unit, x.Scope, x.IsEnabled);

static InstrumentRiskLimitDto ToInstrumentRiskLimitDto(InstrumentRiskLimit x, IReadOnlyDictionary<InstrumentId, string> symbols)
    => new(x.Id.ToString("D"), x.RiskLimitSetId.ToString("D"), x.InstrumentId.Value.ToString("D"), symbols.GetValueOrDefault(x.InstrumentId), x.MaxTradeNotionalUsd, x.MaxExposureUsd, x.MinTradeQuantity, x.MaxOrdersPerDay, x.IsEnabled, x.IsTradingEnabled);

static VenueRiskLimitDto ToVenueRiskLimitDto(VenueRiskLimit x, IReadOnlyDictionary<VenueId, string> venues)
    => new(x.Id.ToString("D"), x.RiskLimitSetId.ToString("D"), x.VenueId.Value.ToString("D"), venues.GetValueOrDefault(x.VenueId), x.MaxTradeNotionalUsd, x.MaxDailyTurnoverUsd, x.MaxOrdersPerMinute, x.IsEnabled, x.IsVenueEnabled);

static TradingWindowDto ToTradingWindowDto(TradingWindow x)
    => new(x.Id.ToString("D"), x.FundId.Value.ToString("D"), x.ModelName, x.DayOfWeek.ToString(), x.TimeZoneId, x.OpensAtUtc.ToString("HH:mm:ss"), x.ClosesAtUtc.ToString("HH:mm:ss"), x.NoNewOrdersAfterUtc.ToString("HH:mm:ss"), x.FlattenAtUtc?.ToString("HH:mm:ss"), x.IsEnabled, x.TradingEnabled, x.ScheduleName, x.Version, x.CreatedAtUtc, x.UpdatedAtUtc);

static RiskInstrumentDto ToRiskInstrumentDto(Instrument x, IReadOnlyList<InstrumentAlias> aliases, IReadOnlyList<VenueInstrumentMapping> mappings)
    => new(ToInstrumentDto(x), aliases.Select(a => new InstrumentAliasDto(a.Id.Value.ToString("D"), a.Source, a.ExternalSymbol, a.ExternalInstrumentId, a.IsEnabled)).ToList(), mappings.Select(m => new VenueInstrumentMappingDto(m.Id.Value.ToString("D"), m.VenueId.Value.ToString("D"), m.VenueSymbol, m.VenueInstrumentCode, m.IsEnabled)).ToList());

static RiskVenueDto ToRiskVenueDto(Venue x)
    => new(ToVenueDto(x));

static OperationalJobDefinitionDto ToOperationalJobDefinitionDto(OperationalJobDefinition x)
    => new(x.Id.Value.ToString("D"), x.JobType.ToString(), x.Name, x.Description, x.IsEnabled, x.IsRerunnable, x.RequiresApproval, x.Severity.ToString(), x.CreatedAtUtc, x.UpdatedAtUtc);

static OperationalJobRunDto ToOperationalJobRunDto(OperationalJobRun x)
    => new(
        x.Id.Value.ToString("D"),
        x.JobDefinitionId?.Value.ToString("D"),
        x.JobType.ToString(),
        x.Name,
        x.Status.ToString(),
        x.TriggerType.ToString(),
        x.TriggeredByActorType.ToString(),
        x.TriggeredByOperatorId,
        x.TriggeredByDisplayName,
        x.StartedAtUtc,
        x.CompletedAtUtc,
        x.DurationMs,
        x.CorrelationId,
        x.RequestId,
        x.InputJson,
        x.OutputJson,
        x.ErrorMessage,
        x.ExceptionCaseId?.Value.ToString("D"),
        x.AuditEventId?.Value.ToString("D"),
        x.RetryOfJobRunId?.Value.ToString("D"),
        x.RetryCount,
        x.CanRetry,
        x.CreatedAtUtc,
        x.UpdatedAtUtc);

static OperationalJobStepDto ToOperationalJobStepDto(OperationalJobStep x)
    => new(x.Id.Value.ToString("D"), x.JobRunId.Value.ToString("D"), x.StepName, x.Status.ToString(), x.StartedAtUtc, x.CompletedAtUtc, x.DurationMs, x.Message, x.InputJson, x.OutputJson, x.ErrorMessage);

static OperationalJobRunEventDto ToOperationalJobRunEventDto(OperationalJobRunEvent x)
    => new(x.Id.ToString("D"), x.JobRunId.Value.ToString("D"), x.OccurredAtUtc, x.Severity.ToString(), x.Message, x.MetadataJson);

static DailyOperationsSummaryDto ToDailyOperationsSummaryDto(DailyOperationsSummary x)
    => new(
        x.Date,
        x.LatestReferenceIntegrity is null ? null : ToOperationalJobRunDto(x.LatestReferenceIntegrity),
        x.LatestMarketDataBars is null ? null : ToOperationalJobRunDto(x.LatestMarketDataBars),
        x.LatestWeightPromotion is null ? null : ToOperationalJobRunDto(x.LatestWeightPromotion),
        x.LatestModelRunProcessing is null ? null : ToOperationalJobRunDto(x.LatestModelRunProcessing),
        x.LatestEodImport is null ? null : ToOperationalJobRunDto(x.LatestEodImport),
        x.LatestEodReconciliation is null ? null : ToOperationalJobRunDto(x.LatestEodReconciliation),
        x.LatestPnlSummary is null ? null : ToOperationalJobRunDto(x.LatestPnlSummary),
        x.OpenExceptionCount,
        x.OpenBlockingExceptionCount,
        x.FailedJobCount,
        x.PendingApprovalCount);

static DailyChecklistItemDto ToDailyChecklistItemDto(DailyChecklistItem x)
    => new(x.Name, x.Status.ToString(), x.Message, x.RelatedEntityType, x.RelatedEntityId);

static OperationalRunbookDefinitionDto ToOperationalRunbookDefinitionDto(OperationalRunbookDefinition x)
    => new(x.Id.Value.ToString("D"), x.Name, x.RunbookType.ToString(), x.Description, x.IsEnabled, x.IsRerunnable, x.CreatedAtUtc, x.UpdatedAtUtc);

static OperationalRunbookStepDefinitionDto ToOperationalRunbookStepDefinitionDto(OperationalRunbookStepDefinition x)
    => new(x.Id.Value.ToString("D"), x.RunbookDefinitionId.Value.ToString("D"), x.StepOrder, x.Name, x.Description, x.JobType?.ToString(), x.GateType.ToString(), x.IsRequired, x.ContinueOnFailure, x.InputTemplateJson, x.CreatedAtUtc, x.UpdatedAtUtc);

static OperationalRunbookRunDto ToOperationalRunbookRunDto(OperationalRunbookRun x)
    => new(x.Id.Value.ToString("D"), x.RunbookDefinitionId.Value.ToString("D"), x.RunbookType.ToString(), x.Name, x.Status.ToString(), x.TriggerType.ToString(), x.TriggeredByOperatorId, x.TriggeredByDisplayName, x.StartedAtUtc, x.CompletedAtUtc, x.DurationMs, x.CorrelationId, x.Reason, x.InputJson, x.OutputJson, x.ErrorMessage, x.RetryOfRunbookRunId?.Value.ToString("D"), x.RetryCount, x.CanRetry, x.CreatedAtUtc, x.UpdatedAtUtc);

static OperationalRunbookStepRunDto ToOperationalRunbookStepRunDto(OperationalRunbookStepRun x)
    => new(x.Id.Value.ToString("D"), x.RunbookRunId.Value.ToString("D"), x.StepDefinitionId?.Value.ToString("D"), x.StepOrder, x.Name, x.Status.ToString(), x.JobRunId?.Value.ToString("D"), x.StartedAtUtc, x.CompletedAtUtc, x.DurationMs, x.Message, x.InputJson, x.OutputJson, x.ErrorMessage, x.CreatedAtUtc, x.UpdatedAtUtc);

static OperationalScheduleDefinitionDto ToOperationalScheduleDefinitionDto(OperationalScheduleDefinition x)
    => new(x.Id.Value.ToString("D"), x.Name, x.RunbookDefinitionId.Value.ToString("D"), x.IsEnabled, x.CronExpression, x.FixedIntervalMinutes, x.TimeZoneId, x.NextRunAtUtc, x.LastRunAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc);

static FillDto ToFillDto(Fill x, IReadOnlyDictionary<InstrumentId, string> symbols, IReadOnlyDictionary<VenueId, string> venues)
    => new(x.Id.Value.ToString("D"), x.BrokerExecutionId, x.ChildOrderId.Value.ToString("D"), x.InstrumentId.Value.ToString("D"), symbols.GetValueOrDefault(x.InstrumentId), x.VenueId.Value.ToString("D"), venues.GetValueOrDefault(x.VenueId), x.Side.ToString(), x.BaseQuantity, x.VenueQuantity, x.Price, x.TradeDateUtc, x.ReceivedAtUtc);

static MarketDataSnapshotDto ToMarketDataSnapshotDto(MarketDataSnapshot x, IReadOnlyDictionary<InstrumentId, string> symbols, IReadOnlyDictionary<VenueId, string> venues)
    => new(x.Id.Value.ToString("D"), x.InstrumentId.Value.ToString("D"), symbols.GetValueOrDefault(x.InstrumentId), x.VenueId.Value.ToString("D"), venues.GetValueOrDefault(x.VenueId), x.Bid, x.Ask, x.Mid, x.Spread, x.Source, x.SourceTimestampUtc, x.ReceivedAtUtc, x.SequenceNumber, x.IsSynthetic, x.CreatedAtUtc);

static MarketDataBarDto ToMarketDataBarDto(MarketDataBar x, IReadOnlyDictionary<InstrumentId, string> symbols, IReadOnlyDictionary<VenueId, string> venues)
    => new(
        x.Id.Value.ToString("D"),
        x.InstrumentId.Value.ToString("D"),
        symbols.GetValueOrDefault(x.InstrumentId),
        x.VenueId.Value.ToString("D"),
        venues.GetValueOrDefault(x.VenueId),
        x.Timeframe.ToString(),
        x.BarStartUtc,
        x.BarEndUtc,
        x.Source,
        x.BidOpen,
        x.BidHigh,
        x.BidLow,
        x.BidClose,
        x.AskOpen,
        x.AskHigh,
        x.AskLow,
        x.AskClose,
        x.MidOpen,
        x.MidHigh,
        x.MidLow,
        x.MidClose,
        x.SpreadOpen,
        x.SpreadHigh,
        x.SpreadLow,
        x.SpreadClose,
        x.SpreadAverage,
        x.ObservationCount,
        x.FirstSnapshotUtc,
        x.LastSnapshotUtc,
        x.IsComplete,
        x.QualityStatus.ToString(),
        x.BuildRunId?.Value.ToString("D"),
        x.BuilderVersion,
        x.CreatedAtUtc);

static OperatorAuditEventDto ToOperatorAuditEventDto(OperatorAuditEvent x)
    => new(
        x.Id.Value.ToString("D"),
        x.OccurredAtUtc,
        x.ActorType.ToString(),
        x.ActorId,
        x.ActorDisplayName,
        x.EventType.ToString(),
        x.Severity.ToString(),
        x.Result.ToString(),
        x.EntityType,
        x.EntityId,
        x.CorrelationId,
        x.CausationId,
        x.RequestId,
        x.Source,
        x.Description,
        x.Reason,
        x.BeforeJson,
        x.AfterJson,
        x.MetadataJson);

static LmaxShadowReplayRequest ToLmaxShadowReplayRequest(LmaxShadowReplayApiRequest request)
    => new(
        request.InputSource,
        request.ExecutionReports?.Select(x => new LmaxShadowExecutionReportInput(x.ExecId, x.BrokerOrderId, x.ClientOrderId, x.ExecutionType, x.OrderStatus, ParseInstrumentId(x.InstrumentId), x.Symbol, x.Side, x.LastQty, x.LastPx, x.LeavesQty, x.CumQty, x.AvgPx, x.TransactTimeUtc, x.Payload)).ToList(),
        request.TradeCaptureReports?.Select(x => new LmaxShadowTradeCaptureInput(x.ExecId, x.SecondaryExecId, x.BrokerOrderId, x.ClientOrderId, ParseInstrumentId(x.InstrumentId), x.Symbol, x.Side, x.LastQty, x.LastPx, x.TradeDate, x.TransactTimeUtc, x.TradeUti, x.LastReportRequested, x.Payload)).ToList(),
        request.OrderStatuses?.Select(x => new LmaxShadowOrderStatusInput(x.BrokerOrderId, x.ClientOrderId, ParseInstrumentId(x.InstrumentId), x.Symbol, x.OrderStatus, x.CumQty, x.LeavesQty, x.TransactTimeUtc, x.Payload)).ToList(),
        request.ProtocolRejects?.Select(x => new LmaxShadowProtocolRejectInput(x.RefMsgType, x.RefTagId, x.ReasonCode, x.Text, x.ClientOrderId, x.BrokerOrderId, x.Payload)).ToList(),
        request.Reason);

static InstrumentId? ParseInstrumentId(string? id)
    => Guid.TryParse(id, out var parsed) ? new InstrumentId(parsed) : null;

static LmaxShadowReplayRunDto ToLmaxShadowReplayRunDto(LmaxShadowReplayRun x)
    => new(x.Id.Value.ToString("D"), x.InputSource.ToString(), x.Status.ToString(), x.StartedAtUtc, x.CompletedAtUtc, x.InputJson, x.OutputJson, x.ObservationCount, x.BlockingObservationCount, x.WarningObservationCount, x.Message, x.CorrelationId, x.CreatedAtUtc);

static LmaxShadowObservationDto ToLmaxShadowObservationDto(LmaxShadowObservation x)
    => new(x.Id.Value.ToString("D"), x.ReplayRunId?.Value.ToString("D"), x.ObservedAtUtc, x.Type.ToString(), x.Severity.ToString(), x.Status.ToString(), x.InstrumentId?.Value.ToString("D"), x.Symbol, x.BrokerExecutionId, x.BrokerOrderId, x.ClientOrderId, x.InternalFillId?.Value.ToString("D"), x.InternalOrderId?.Value.ToString("D"), x.Description, x.LmaxPayloadJson, x.InternalPayloadJson, x.DifferenceJson, x.CorrelationId, x.CreatedAtUtc);

static OperatorUserDto ToOperatorUserDto(OperatorUser user, IReadOnlySet<OperatorRole> roles, IReadOnlySet<OperatorPermission> permissions)
    => new(user.Id.Value.ToString("D"), user.OperatorId, user.DisplayName, user.Email, user.IsEnabled, user.CreatedAtUtc, user.UpdatedAtUtc, roles.Select(x => x.ToString()).OrderBy(x => x).ToList(), permissions.Select(x => x.ToString()).OrderBy(x => x).ToList());

static ApprovalRequestDto ToApprovalRequestDto(ApprovalRequest x)
    => new(
        x.Id.Value.ToString("D"),
        x.Type.ToString(),
        x.Status.ToString(),
        x.RequestedByOperatorId,
        x.RequestedByDisplayName,
        x.RequestedAtUtc,
        x.RequiredApproverRole.ToString(),
        x.EntityType,
        x.EntityId,
        x.Reason,
        x.PayloadJson,
        x.BeforeJson,
        x.AfterJson,
        x.CorrelationId,
        x.ExpiresAtUtc,
        x.ApprovedAtUtc,
        x.ApprovedByOperatorId,
        x.RejectedAtUtc,
        x.RejectedByOperatorId,
        x.ExecutedAtUtc,
        x.ExecutedByOperatorId,
        x.ResultMessage,
        x.CreatedAtUtc,
        x.UpdatedAtUtc);

static ApprovalDecisionDto ToApprovalDecisionDto(ApprovalDecision x)
    => new(x.Id.Value.ToString("D"), x.ApprovalRequestId.Value.ToString("D"), x.Decision.ToString(), x.DecidedByOperatorId, x.DecidedByDisplayName, x.Reason, x.DecidedAtUtc, x.CorrelationId);

static GovernedActionResult PendingResult(ApprovalRequest approval, IOperatorContext context)
    => new(false, true, approval.Id, approval.Status.ToString(), "Approval request created. The action has not been executed.", approval.EntityId, null, context.CorrelationId);

static GovernedActionResultDto ToGovernedActionResultDto(GovernedActionResult x)
    => new(x.Executed, x.ApprovalRequired, x.ApprovalRequestId?.Value.ToString("D"), x.Status, x.Message, x.EntityId, x.ResultEntityId, x.CorrelationId);

static ExceptionCaseDto ToExceptionCaseDto(ExceptionCase x)
    => new(
        x.Id.Value.ToString("D"),
        x.CreatedAtUtc,
        x.UpdatedAtUtc,
        x.Status.ToString(),
        x.Severity.ToString(),
        x.Type.ToString(),
        x.Source.ToString(),
        x.Title,
        x.Description,
        x.EntityType,
        x.EntityId,
        x.InstrumentId?.Value.ToString("D"),
        x.Symbol,
        x.CorrelationId,
        x.AssignedTo,
        x.AcknowledgedAtUtc,
        x.AcknowledgedBy,
        x.ResolvedAtUtc,
        x.ResolvedBy,
        x.ResolutionReason,
        x.WaiverReason,
        x.MetadataJson);

static ExceptionCaseActionDto ToExceptionCaseActionDto(ExceptionCaseAction x)
    => new(
        x.Id.Value.ToString("D"),
        x.CaseId.Value.ToString("D"),
        x.ActionType.ToString(),
        x.ActorId,
        x.ActorDisplayName,
        x.OccurredAtUtc,
        x.FromStatus?.ToString(),
        x.ToStatus?.ToString(),
        x.Reason,
        x.Note,
        x.MetadataJson,
        x.CorrelationId);

static ExceptionCaseNoteDto ToExceptionCaseNoteDto(ExceptionCaseNote x)
    => new(x.Id.Value.ToString("D"), x.CaseId.Value.ToString("D"), x.CreatedAtUtc, x.CreatedBy, x.Note, x.CorrelationId);

public sealed record HealthDto(string Application, string Environment, string PersistenceProvider, bool DatabaseReachable, int PendingMigrationsCount, string DatabaseTarget, string ExecutionGateway, string MarketDataMode, bool LiveTradingEnabled, bool ExternalConnectionsEnabled, DateTimeOffset UtcServerTime);
public sealed record ReferenceDataIntegrityDto(DateTimeOffset CheckedAtUtc, int BlockingIssueCount, int WarningIssueCount, IReadOnlyList<ReferenceDataIntegrityIssueDto> Issues);
public sealed record ReferenceDataIntegrityIssueDto(string Id, string Type, string Severity, string Status, string Key, string Description, DateTimeOffset CreatedAtUtc);
public sealed record ModelRunDto(string Id, string FundId, string ModelName, DateTimeOffset AsOfUtc, DateTimeOffset ReceivedAtUtc, DateTimeOffset EffectiveAtUtc, int FrequencyMinutes, decimal NavUsd, string Status, string InputHash, string SourceFileName, bool IsProcessed, string TargetQuantityMode);
public sealed record LmaxReportImportRunDto(string Id, string ReportType, DateOnly ReportDate, string VenueId, string BrokerAccountId, string Status, string? FileName, string? FilePath, string? FileHash, int? RowCount, DateTimeOffset CreatedAtUtc, DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc, string? ArchivedPath, string? RejectedPath, string? Message);
public sealed record LmaxReportValidationIssueDto(string Id, string ImportRunId, string IssueType, string Severity, string Message, int? RowNumber, string? RawLine, DateTimeOffset CreatedAtUtc);
public sealed record LmaxReportImportResultDto(string ImportRunId, string Status, int RowCount, int BlockingIssueCount, IReadOnlyList<LmaxReportValidationIssueDto> Issues, string Message);
public sealed record LmaxIndividualTradeDto(string Id, string ImportRunId, DateOnly ReportDate, string VenueId, string BrokerAccountId, string ExecutionId, string? MtfExecutionId, DateTimeOffset TimestampUtc, decimal TradeQuantity, decimal TradePrice, DateOnly TradeDate, string? LmaxInstrumentId, string LmaxSymbol, string? InstrumentId, string? InstructionId, string? OrderId, string OrderType, decimal? TotalProfitLoss, decimal TotalCommission, string AccountId, decimal UnitsBoughtSold, decimal NotionalValue, string TradeUti, DateTimeOffset CreatedAtUtc);
public sealed record LmaxTradeSummaryDto(string Id, string ImportRunId, DateOnly ReportDate, string VenueId, string BrokerAccountId, DateTimeOffset DateTimeUtc, string Instrument, string? InstrumentId, string Type, string Currency, decimal Contracts, decimal AveragePrice, decimal CommissionRounded, decimal NotionalValue, string LmaxSymbol, string? UserPlacingOrder, decimal CommissionFullPrecision, string AccountId, DateTimeOffset CreatedAtUtc);
public sealed record LmaxCurrencyWalletDto(string Id, string ImportRunId, DateOnly ReportDate, string VenueId, string BrokerAccountId, string Currency, decimal BalanceNetDeposits, decimal Adjustments, decimal InterAccountTransfers, decimal ProfitLoss, decimal Commission, decimal Dividends, decimal Financing, decimal WalletBalance, decimal RateToBaseCcy, string BaseCurrency, decimal BalanceNetDepositsBaseUsd, decimal AdjustmentsBaseUsd, decimal InterAccountTransfersBaseUsd, decimal ProfitLossBaseUsd, decimal CommissionBaseUsd, decimal DividendsBaseUsd, decimal FinancingBaseUsd, decimal WalletBalanceBaseUsd, string AccountId, DateTimeOffset CreatedAtUtc);
public sealed record FakeLmaxEodReportGenerationDto(DateOnly ReportDate, string IndividualTradesPath, string TradesSummaryPath, string CurrencyWalletsPath, int IndividualTradeCount, int TradeSummaryCount, int CurrencyWalletCount, string MutationMode);
public sealed record EodReconciliationRunDto(string Id, DateOnly ReportDate, string VenueId, string BrokerAccountId, DateTimeOffset CreatedAtUtc, bool HasBlockingBreaks);
public sealed record EodReconciliationBreakDto(string Id, string RunId, string Type, string Severity, string Status, string? InstrumentId, string Description, string? BrokerExecutionId, string? InternalFillId, DateTimeOffset CreatedAtUtc);
public sealed record EodReconciliationResultDto(string RunId, DateOnly ReportDate, int BreakCount, int BlockingBreakCount, IReadOnlyList<EodReconciliationBreakDto> Breaks);
public sealed record EodPnlCurrencyRowDto(string Currency, decimal WalletBalance, decimal RateToBaseCcy, decimal WalletBalanceBaseUsd, decimal ProfitLoss, decimal ProfitLossBaseUsd, decimal Commission, decimal CommissionBaseUsd, decimal Dividends, decimal DividendsBaseUsd, decimal Financing, decimal FinancingBaseUsd);
public sealed record EodPnlSummaryDto(DateOnly ReportDate, string VenueName, string BrokerAccountCode, decimal TotalWalletBalanceUsd, decimal TotalProfitLossUsd, decimal TotalCommissionUsd, decimal TotalDividendsUsd, decimal TotalFinancingUsd, decimal TotalNetPnlUsd, IReadOnlyList<EodPnlCurrencyRowDto> CurrencyRows);
public sealed record ModelWeightBatchDto(string Id, string ExternalBatchId, string SourceSystem, string FundCode, string? FundId, string ModelName, DateTimeOffset AsOfUtc, DateTimeOffset EffectiveAtUtc, int FrequencyMinutes, decimal NavUsd, string TargetQuantityMode, string Status, int? ExpectedRowCount, string? ContentHash, DateTimeOffset CreatedAtUtc, DateTimeOffset? ReadyAtUtc, DateTimeOffset? AcceptedAtUtc, DateTimeOffset? PromotedAtUtc, DateTimeOffset? RejectedAtUtc, string? PromotedModelRunId, string? Message);
public sealed record ModelWeightRowDto(string Id, string BatchId, string RawSecurityId, string Symbol, string? InstrumentId, decimal Weight, DateTimeOffset CreatedAtUtc);
public sealed record ModelWeightValidationIssueDto(string Id, string BatchId, string IssueType, string Severity, string Message, string? RowId, int? RowNumber, DateTimeOffset CreatedAtUtc);
public sealed record ModelWeightPromotionResultDto(string? BatchId, string? Status, string? PromotedModelRunId, string? ModelRunId, int ValidationIssueCount, IReadOnlyList<ModelWeightValidationIssueDto> Issues, string Message, bool Succeeded, bool AlreadyPromoted);
public sealed record TargetPositionDto(string ModelRunId, string InstrumentId, string? Symbol, decimal TargetNotionalUsd, decimal TargetBaseQuantity, decimal TargetVenueQuantity, string TargetQuantityMode);
public sealed record DriftSnapshotDto(string ModelRunId, string InstrumentId, string? Symbol, decimal TargetBaseQuantity, decimal CurrentBaseQuantity, decimal DriftBaseQuantity, decimal TargetVenueQuantity, decimal CurrentVenueQuantity, decimal DriftVenueQuantity);
public sealed record PositionDto(string InstrumentId, string? Symbol, decimal BaseQuantity, DateTimeOffset? AsOfUtc);
public sealed record ReconciliationBreakDto(string Id, string ReconciliationRunId, string? ModelRunId, string? Phase, string Type, string Severity, string Status, string? InstrumentId, string? Symbol, string Description, DateTimeOffset? CreatedAtUtc);
public sealed record TradeIntentDto(string Id, string ModelRunId, string FundId, string InstrumentId, string? Symbol, string Side, decimal RequestedBaseQuantity, decimal RequestedVenueQuantity, string Reason, string Status, DateTimeOffset CreatedAtUtc);
public sealed record RiskDecisionDto(
    string Id,
    string TradeIntentId,
    string? ModelRunId,
    string? InstrumentId,
    string? Symbol,
    string? VenueId,
    string? VenueName,
    string? RiskLimitSetId,
    string? RiskLimitSetName,
    int? RiskLimitSetVersion,
    string Status,
    string? RejectReason,
    string Message,
    DateTimeOffset CreatedAtUtc,
    decimal? SummaryObservedValue,
    decimal? SummaryLimitValue,
    string? SummaryUnit,
    string? SummaryCheckName,
    IReadOnlyList<RiskDecisionDetailDto> Details);
public sealed record RiskDecisionDetailDto(string Id, string RiskDecisionId, string CheckName, string Status, string? RejectReason, decimal? ObservedValue, decimal? LimitValue, string? Unit, string Message, DateTimeOffset CreatedAtUtc);
public sealed record RiskLimitSetDto(string Id, string FundId, string? ModelName, string Name, int Version, string Status, bool IsActive, DateTimeOffset? EffectiveFromUtc, DateTimeOffset? EffectiveToUtc, DateTimeOffset? CreatedAtUtc, string? CreatedBy, DateTimeOffset? ActivatedAtUtc, string? ActivatedBy, DateTimeOffset? RetiredAtUtc, string? RetiredBy, string? Description, bool GlobalTradingEnabled, decimal MaxGrossExposureUsd, int ModelStalenessSeconds, int MarketDataStalenessSeconds, decimal PositionToleranceBaseQuantity, decimal MinDriftVenueQuantity);
public sealed record RiskLimitDto(string Id, string RiskLimitSetId, string Name, decimal Value, string Unit, string Scope, bool IsEnabled);
public sealed record InstrumentRiskLimitDto(string Id, string RiskLimitSetId, string InstrumentId, string? Symbol, decimal MaxTradeNotionalUsd, decimal MaxExposureUsd, decimal MinTradeQuantity, int MaxOrdersPerDay, bool IsEnabled, bool IsTradingEnabled);
public sealed record VenueRiskLimitDto(string Id, string RiskLimitSetId, string VenueId, string? VenueName, decimal MaxTradeNotionalUsd, decimal MaxDailyTurnoverUsd, int MaxOrdersPerMinute, bool IsEnabled, bool IsVenueEnabled);
public sealed record TradingWindowDto(string Id, string FundId, string ModelName, string DayOfWeek, string TimeZoneId, string OpensAtUtc, string ClosesAtUtc, string NoNewOrdersAfterUtc, string? FlattenAtUtc, bool IsEnabled, bool TradingEnabled, string ScheduleName, int Version, DateTimeOffset? CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);
public sealed record OperationalJobDefinitionDto(string Id, string JobType, string Name, string Description, bool IsEnabled, bool IsRerunnable, bool RequiresApproval, string Severity, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);
public sealed record OperationalJobRunDto(string Id, string? JobDefinitionId, string JobType, string Name, string Status, string TriggerType, string TriggeredByActorType, string? TriggeredByOperatorId, string? TriggeredByDisplayName, DateTimeOffset StartedAtUtc, DateTimeOffset? CompletedAtUtc, long? DurationMs, string? CorrelationId, string? RequestId, string? InputJson, string? OutputJson, string? ErrorMessage, string? ExceptionCaseId, string? AuditEventId, string? RetryOfJobRunId, int RetryCount, bool CanRetry, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);
public sealed record OperationalJobStepDto(string Id, string JobRunId, string StepName, string Status, DateTimeOffset StartedAtUtc, DateTimeOffset? CompletedAtUtc, long? DurationMs, string? Message, string? InputJson, string? OutputJson, string? ErrorMessage);
public sealed record OperationalJobRunEventDto(string Id, string JobRunId, DateTimeOffset OccurredAtUtc, string Severity, string Message, string? MetadataJson);
public sealed record DailyOperationsSummaryDto(DateOnly Date, OperationalJobRunDto? LatestReferenceIntegrity, OperationalJobRunDto? LatestMarketDataBars, OperationalJobRunDto? LatestWeightPromotion, OperationalJobRunDto? LatestModelRunProcessing, OperationalJobRunDto? LatestEodImport, OperationalJobRunDto? LatestEodReconciliation, OperationalJobRunDto? LatestPnlSummary, int OpenExceptionCount, int OpenBlockingExceptionCount, int FailedJobCount, int PendingApprovalCount);
public sealed record DailyChecklistItemDto(string Name, string Status, string Message, string? RelatedEntityType, string? RelatedEntityId);
public sealed record OperationalRunbookDefinitionDto(string Id, string Name, string RunbookType, string Description, bool IsEnabled, bool IsRerunnable, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);
public sealed record OperationalRunbookStepDefinitionDto(string Id, string RunbookDefinitionId, int StepOrder, string Name, string Description, string? JobType, string GateType, bool IsRequired, bool ContinueOnFailure, string? InputTemplateJson, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);
public sealed record OperationalRunbookRunDto(string Id, string RunbookDefinitionId, string RunbookType, string Name, string Status, string TriggerType, string? TriggeredByOperatorId, string? TriggeredByDisplayName, DateTimeOffset StartedAtUtc, DateTimeOffset? CompletedAtUtc, long? DurationMs, string? CorrelationId, string? Reason, string? InputJson, string? OutputJson, string? ErrorMessage, string? RetryOfRunbookRunId, int RetryCount, bool CanRetry, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);
public sealed record OperationalRunbookStepRunDto(string Id, string RunbookRunId, string? StepDefinitionId, int StepOrder, string Name, string Status, string? JobRunId, DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc, long? DurationMs, string? Message, string? InputJson, string? OutputJson, string? ErrorMessage, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);
public sealed record OperationalScheduleDefinitionDto(string Id, string Name, string RunbookDefinitionId, bool IsEnabled, string? CronExpression, int? FixedIntervalMinutes, string TimeZoneId, DateTimeOffset? NextRunAtUtc, DateTimeOffset? LastRunAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);
public sealed record OperationalScheduleDefinitionRequest(string Name, string RunbookDefinitionId, bool IsEnabled, string? CronExpression, int? FixedIntervalMinutes, string? TimeZoneId, DateTimeOffset? NextRunAtUtc, DateTimeOffset? LastRunAtUtc, string Reason);
public sealed record FillDto(string Id, string BrokerExecutionId, string ChildOrderId, string InstrumentId, string? Symbol, string VenueId, string? VenueName, string Side, decimal BaseQuantity, decimal VenueQuantity, decimal Price, DateTimeOffset TradeDateUtc, DateTimeOffset ReceivedAtUtc);
public sealed record MarketDataSnapshotDto(string Id, string InstrumentId, string? Symbol, string VenueId, string? VenueName, decimal Bid, decimal Ask, decimal Mid, decimal Spread, string Source, DateTimeOffset SourceTimestampUtc, DateTimeOffset ReceivedAtUtc, long? SequenceNumber, bool IsSynthetic, DateTimeOffset CreatedAtUtc);
public sealed record MarketDataBarDto(string Id, string InstrumentId, string? Symbol, string VenueId, string? VenueName, string Timeframe, DateTimeOffset BarStartUtc, DateTimeOffset BarEndUtc, string Source, decimal BidOpen, decimal BidHigh, decimal BidLow, decimal BidClose, decimal AskOpen, decimal AskHigh, decimal AskLow, decimal AskClose, decimal MidOpen, decimal MidHigh, decimal MidLow, decimal MidClose, decimal SpreadOpen, decimal SpreadHigh, decimal SpreadLow, decimal SpreadClose, decimal SpreadAverage, int ObservationCount, DateTimeOffset? FirstSnapshotUtc, DateTimeOffset? LastSnapshotUtc, bool IsComplete, string QualityStatus, string? BuildRunId, string BuilderVersion, DateTimeOffset CreatedAtUtc);
public sealed record OperatorAuditEventDto(string Id, DateTimeOffset OccurredAtUtc, string ActorType, string ActorId, string ActorDisplayName, string EventType, string Severity, string Result, string? EntityType, string? EntityId, string? CorrelationId, string? CausationId, string? RequestId, string Source, string Description, string? Reason, string? BeforeJson, string? AfterJson, string? MetadataJson);
public sealed record LmaxShadowReplayRunDto(string Id, string InputSource, string Status, DateTimeOffset StartedAtUtc, DateTimeOffset? CompletedAtUtc, string? InputJson, string? OutputJson, int ObservationCount, int BlockingObservationCount, int WarningObservationCount, string? Message, string? CorrelationId, DateTimeOffset CreatedAtUtc);
public sealed record LmaxShadowObservationDto(string Id, string? ReplayRunId, DateTimeOffset ObservedAtUtc, string Type, string Severity, string Status, string? InstrumentId, string? Symbol, string? BrokerExecutionId, string? BrokerOrderId, string? ClientOrderId, string? InternalFillId, string? InternalOrderId, string Description, string? LmaxPayloadJson, string? InternalPayloadJson, string? DifferenceJson, string? CorrelationId, DateTimeOffset CreatedAtUtc);
public sealed record OperatorUserDto(string Id, string OperatorId, string DisplayName, string? Email, bool IsEnabled, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);
public sealed record ApprovalRequestDto(string Id, string Type, string Status, string RequestedByOperatorId, string RequestedByDisplayName, DateTimeOffset RequestedAtUtc, string RequiredApproverRole, string EntityType, string EntityId, string Reason, string PayloadJson, string? BeforeJson, string? AfterJson, string? CorrelationId, DateTimeOffset? ExpiresAtUtc, DateTimeOffset? ApprovedAtUtc, string? ApprovedByOperatorId, DateTimeOffset? RejectedAtUtc, string? RejectedByOperatorId, DateTimeOffset? ExecutedAtUtc, string? ExecutedByOperatorId, string? ResultMessage, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);
public sealed record ApprovalDecisionDto(string Id, string ApprovalRequestId, string Decision, string DecidedByOperatorId, string DecidedByDisplayName, string Reason, DateTimeOffset DecidedAtUtc, string? CorrelationId);
public sealed record GovernedActionResultDto(bool Executed, bool ApprovalRequired, string? ApprovalRequestId, string Status, string Message, string EntityId, string? ResultEntityId, string? CorrelationId);
public sealed record ExceptionCaseDto(string Id, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string Status, string Severity, string Type, string Source, string Title, string Description, string? EntityType, string? EntityId, string? InstrumentId, string? Symbol, string? CorrelationId, string? AssignedTo, DateTimeOffset? AcknowledgedAtUtc, string? AcknowledgedBy, DateTimeOffset? ResolvedAtUtc, string? ResolvedBy, string? ResolutionReason, string? WaiverReason, string? MetadataJson);
public sealed record ExceptionCaseActionDto(string Id, string CaseId, string ActionType, string ActorId, string ActorDisplayName, DateTimeOffset OccurredAtUtc, string? FromStatus, string? ToStatus, string? Reason, string? Note, string? MetadataJson, string? CorrelationId);
public sealed record ExceptionCaseNoteDto(string Id, string CaseId, DateTimeOffset CreatedAtUtc, string CreatedBy, string Note, string? CorrelationId);
public sealed record KillSwitchDto(string Id, bool IsActive, string? Reason, DateTimeOffset UpdatedAtUtc);
public sealed record InstrumentDto(string Id, string Symbol, string AssetClass, string BaseCurrency, string QuoteCurrency, int PricePrecision, int QuantityPrecision, bool IsEnabled, bool IsTradingEnabled, bool IsReportImportEnabled, bool IsMarketDataEnabled);
public sealed record VenueDto(string Id, string Name, string VenueType, bool IsEnabled, bool IsTradingEnabled, bool IsReportImportEnabled, bool IsMarketDataEnabled);
public sealed record InstrumentAliasDto(string Id, string Source, string ExternalSymbol, string? ExternalInstrumentId, bool IsEnabled);
public sealed record VenueInstrumentMappingDto(string Id, string VenueId, string VenueSymbol, string VenueInstrumentCode, bool IsEnabled);
public sealed record RiskInstrumentDto(InstrumentDto Instrument, IReadOnlyList<InstrumentAliasDto> Aliases, IReadOnlyList<VenueInstrumentMappingDto> VenueMappings);
public sealed record RiskVenueDto(VenueDto Venue);
public sealed record ReasonRequest(string Reason);
public sealed record LmaxShadowReplayApiRequest(LmaxShadowInputSource InputSource, IReadOnlyList<LmaxShadowExecutionReportApiInput>? ExecutionReports, IReadOnlyList<LmaxShadowTradeCaptureApiInput>? TradeCaptureReports, IReadOnlyList<LmaxShadowOrderStatusApiInput>? OrderStatuses, IReadOnlyList<LmaxShadowProtocolRejectApiInput>? ProtocolRejects, string Reason);
public sealed record LmaxShadowExecutionReportApiInput(string? ExecId, string? BrokerOrderId, string? ClientOrderId, string? ExecutionType, string? OrderStatus, string? InstrumentId, string? Symbol, string? Side, decimal? LastQty, decimal? LastPx, decimal? LeavesQty, decimal? CumQty, decimal? AvgPx, DateTimeOffset? TransactTimeUtc, object? Payload);
public sealed record LmaxShadowTradeCaptureApiInput(string? ExecId, string? SecondaryExecId, string? BrokerOrderId, string? ClientOrderId, string? InstrumentId, string? Symbol, string? Side, decimal? LastQty, decimal? LastPx, DateOnly? TradeDate, DateTimeOffset? TransactTimeUtc, string? TradeUti, bool? LastReportRequested, object? Payload);
public sealed record LmaxShadowOrderStatusApiInput(string? BrokerOrderId, string? ClientOrderId, string? InstrumentId, string? Symbol, string? OrderStatus, decimal? CumQty, decimal? LeavesQty, DateTimeOffset? TransactTimeUtc, object? Payload);
public sealed record LmaxShadowProtocolRejectApiInput(string? RefMsgType, int? RefTagId, int? ReasonCode, string? Text, string? ClientOrderId, string? BrokerOrderId, object? Payload);
public sealed record RunOperationalJobApiRequest(string JobType, string Reason, object? Input);
public sealed record RunOperationalRunbookApiRequest(string RunbookType, string Reason, object? Input);
public sealed record CompleteManualRunbookStepRequest(string StepRunId, string Reason);
public sealed record CreateRiskLimitSetRequest(string? FundCode, string? ModelName, string Name, string? Description, string Reason);
public sealed record UpdateRiskLimitRequest(decimal Value, string? Unit, string Reason);
public sealed record UpdateInstrumentRiskLimitRequest(decimal? MaxTradeNotionalUsd, decimal? MaxExposureUsd, decimal? MinTradeQuantity, int? MaxOrdersPerDay, bool? IsTradingEnabled, string Reason);
public sealed record UpdateVenueRiskLimitRequest(decimal? MaxTradeNotionalUsd, decimal? MaxDailyTurnoverUsd, int? MaxOrdersPerMinute, bool? IsVenueEnabled, string Reason);
public sealed record UpdateTradingWindowRequest(TimeOnly? OpensAtUtc, TimeOnly? ClosesAtUtc, TimeOnly? NoNewOrdersAfterUtc, TimeOnly? FlattenAtUtc, bool? TradingEnabled, string Reason);
public sealed record UpdateInstrumentControlsRequest(bool? IsTradingEnabled, bool? IsReportImportEnabled, bool? IsMarketDataEnabled, string Reason);
public sealed record UpdateVenueControlsRequest(bool? IsTradingEnabled, bool? IsReportImportEnabled, bool? IsMarketDataEnabled, string Reason);
public sealed record CreateModelRunRequest(string? ModelName, string? Symbol, decimal? Weight, decimal NavUsd, TargetQuantityMode TargetQuantityMode, DateTimeOffset? AsOfUtc, DateTimeOffset? EffectiveAtUtc, int FrequencyMinutes, string? InputHash, string? SourceFileName, List<ModelRunWeightRequest>? Weights);
public sealed record ModelRunWeightRequest(string Symbol, decimal Weight, string? RawSecurityId);
public sealed record CreateFakeModelWeightBatchApiRequest(string? ExternalBatchId, ModelWeightSourceSystem SourceSystem, string FundCode, string ModelName, DateTimeOffset? AsOfUtc, DateTimeOffset? EffectiveAtUtc, int FrequencyMinutes, decimal NavUsd, TargetQuantityMode TargetQuantityMode, ModelWeightBatchStatus Status, List<CreateFakeModelWeightRowApiRequest>? Weights);
public sealed record CreateFakeModelWeightRowApiRequest(string RawSecurityId, string Symbol, decimal Weight);
public sealed record PromoteReadyModelWeightBatchesRequest(int? Limit);
public sealed record GenerateFakeLmaxEodRequest(DateOnly ReportDate, string? VenueName, string? BrokerAccountCode, LmaxEodMutationMode? MutationMode);
public sealed record ImportGeneratedLmaxEodRequest(DateOnly ReportDate, string? VenueName, string? BrokerAccountCode);
public sealed record ImportLmaxReportSetRequest(string IndividualTradesPath, string TradesSummaryPath, string CurrencyWalletsPath, DateOnly ReportDate, string? VenueName, string? BrokerAccountCode);
public sealed record ImportSingleLmaxReportRequest(string FilePath, DateOnly ReportDate, string? VenueName, string? BrokerAccountCode);
public sealed record RunEodReconciliationRequest(DateOnly ReportDate, string? VenueName, string? BrokerAccountCode);
public sealed record CreateExceptionCaseApiRequest(ExceptionCaseSeverity Severity, ExceptionCaseType Type, ExceptionCaseSource Source, string Title, string Description, string? EntityType, string? EntityId, Guid? InstrumentId, string? Symbol, string? AssignedTo, object? Metadata);
public sealed record ExceptionCaseReasonRequest(string? Reason);
public sealed record ExceptionCaseAssignRequest(string AssignedTo);
public sealed record ExceptionCaseNoteRequest(string Note);
public sealed record KillSwitchRequest(string? Reason);
public sealed record FakeSnapshotsRequest(string? InstrumentSymbol, string? VenueName, DateTimeOffset StartUtc, int IntervalSeconds, int Count, decimal Bid, decimal Ask, decimal? BidStep, decimal? AskStep);
public sealed record BuildBarsRequest(string? VenueName, BarTimeframe Timeframe, DateTimeOffset StartUtc, DateTimeOffset EndUtc);
public sealed record OrdersResponse(IReadOnlyList<ParentOrderDto> ParentOrders, IReadOnlyList<ChildOrderDto> ChildOrders);
public sealed record ParentOrderDto(string Id, string TradeIntentId, string? InstrumentId, string ClientOrderId, string Side, decimal BaseQuantity, string Algo, string Status, DateTimeOffset CreatedAtUtc);
public sealed record ChildOrderDto(string Id, string ParentOrderId, string VenueId, string? InstrumentId, string ClientOrderId, string? BrokerOrderId, string Side, string OrderType, string TimeInForce, decimal BaseQuantity, decimal VenueQuantity, string Status, DateTimeOffset CreatedAtUtc);

public sealed class HttpOperatorContext(IHttpContextAccessor accessor, OperatorContextOptions options) : IOperatorContext
{
    public OperatorIdentity Current
    {
        get
        {
            var context = accessor.HttpContext;
            var actorId = options.AllowHeaderOperatorOverride ? context?.Request.Headers["X-Operator-Id"].FirstOrDefault() : null;
            var actorName = context?.Request.Headers["X-Operator-Name"].FirstOrDefault();
            actorId = string.IsNullOrWhiteSpace(actorId) ? options.DefaultOperatorId : actorId;
            actorName = string.IsNullOrWhiteSpace(actorName)
                ? actorId
                : actorName;
            return new OperatorIdentity(OperatorAuditActorType.Operator, actorId, actorName);
        }
    }

    public string? CorrelationId => accessor.HttpContext?.Items["CorrelationId"]?.ToString();
    public string? RequestId => accessor.HttpContext?.TraceIdentifier;
}

public partial class Program;
