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
builder.Services.AddSingleton(new FakeLmaxOptions());
builder.Services.AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>();
builder.Services.AddSingleton<IMarketDataProvider, FakeMarketDataProvider>();
builder.Services.AddSingleton(new BarBuilderOptions());
builder.Services.AddScoped<ProcessModelRunService>();
builder.Services.AddScoped<IReferenceDataIntegrityService, ReferenceDataIntegrityService>();
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

app.MapPost("/model-weight-batches/fake", async (CreateFakeModelWeightBatchApiRequest request, IFakeModelWeightGenerator generator, CancellationToken cancellationToken) =>
{
    try
    {
        var batch = await generator.CreateFakeBatchAsync(ToApplicationRequest(request), cancellationToken);
        return Results.Created($"/model-weight-batches/{batch.Id.Value}", ToModelWeightBatchDto(batch));
    }
    catch (DomainRuleViolationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
});

app.MapPost("/model-weight-batches/{id:guid}/validate", async (Guid id, IModelWeightPromotionService service, CancellationToken cancellationToken) =>
{
    var result = await service.ValidateBatchAsync(new ModelWeightBatchId(id), cancellationToken);
    return Results.Ok(ToModelWeightPromotionResultDto(result));
});

app.MapPost("/model-weight-batches/{id:guid}/promote", async (Guid id, IModelWeightPromotionService service, CancellationToken cancellationToken) =>
{
    var result = await service.PromoteBatchAsync(new ModelWeightBatchId(id), cancellationToken);
    return Results.Ok(ToModelWeightPromotionResultDto(result));
});

app.MapPost("/model-weight-batches/promote-ready", async (PromoteReadyModelWeightBatchesRequest request, IModelWeightPromotionService service, CancellationToken cancellationToken) =>
{
    var results = await service.PromoteReadyBatchesAsync(ClampLimit(request.Limit), cancellationToken);
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

app.MapPost("/lmax-eod/generate-fake", async (GenerateFakeLmaxEodRequest request, IFakeLmaxEodReportGenerator generator, CancellationToken cancellationToken) =>
{
    var result = await generator.GenerateAsync(request.ReportDate, request.VenueName ?? "LMAX", request.BrokerAccountCode ?? "LMAX_DEMO_LOCAL", request.MutationMode ?? LmaxEodMutationMode.None, cancellationToken);
    return Results.Ok(ToFakeLmaxEodReportGenerationDto(result));
});

app.MapPost("/lmax-eod/import-generated", async (ImportGeneratedLmaxEodRequest request, ILmaxEodReportImportService importer, CancellationToken cancellationToken) =>
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
    return Results.Ok(ToLmaxReportImportResultDto(result));
});

app.MapPost("/lmax-eod/import-report-set", async (ImportLmaxReportSetRequest request, ILmaxEodReportImportService importer, CancellationToken cancellationToken) =>
{
    var result = await importer.ImportReportSetAsync(request.IndividualTradesPath, request.TradesSummaryPath, request.CurrencyWalletsPath, request.ReportDate, request.VenueName ?? "LMAX", request.BrokerAccountCode ?? "LMAX_DEMO_LOCAL", cancellationToken);
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

app.MapPost("/eod-reconciliation/run", async (RunEodReconciliationRequest request, IEodReconciliationService service, CancellationToken cancellationToken) =>
{
    var result = await service.RunAsync(request.ReportDate, request.VenueName ?? "LMAX", request.BrokerAccountCode ?? "LMAX_DEMO_LOCAL", cancellationToken);
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

app.MapPost("/model-runs", async (CreateModelRunRequest request, IIntradayRepository repository, IClock clock, CancellationToken cancellationToken) =>
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
    return Results.Created($"/model-runs/{run.Id.Value}", ToModelRunDto(run));
});

app.MapPost("/model-runs/{id:guid}/process", async (Guid id, ProcessModelRunService service, IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var modelRunId = new ModelRunId(id);
    var result = await service.ProcessAsync(modelRunId, cancellationToken);
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
    var query = state.RiskDecisions.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase));
    if (modelRunId is not null) query = query.Where(x => intents.TryGetValue(x.TradeIntentId, out var intent) && intent.ModelRunId.Value == modelRunId.Value);
    return query.OrderByDescending(x => x.CreatedAtUtc).Take(ClampLimit(limit)).Select(x => ToRiskDecisionDto(x, intents, symbols));
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
app.MapGet("/admin/reference-data/integrity", async (IReferenceDataIntegrityService service, CancellationToken cancellationToken) =>
{
    var result = await service.CheckAsync(cancellationToken);
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
app.MapPost("/admin/kill-switch", async (KillSwitchRequest request, IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    await repository.SetKillSwitchAsync(true, request.Reason, cancellationToken);
    return Results.Ok(new { active = true, request.Reason });
});
app.MapGet("/admin/kill-switch", async (IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    return Results.Ok(ToKillSwitchDto(state.KillSwitch));
});
app.MapPost("/admin/kill-switch/clear", async (IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    await repository.SetKillSwitchAsync(false, null, cancellationToken);
    return Results.Ok(new { active = false });
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
    => new(x.Id.Value.ToString("D"), x.Symbol, x.AssetClass.ToString(), x.BaseCurrency.ToString(), x.QuoteCurrency.ToString(), x.PricePrecision, x.QuantityPrecision, x.IsEnabled);

static VenueDto ToVenueDto(Venue x)
    => new(x.Id.Value.ToString("D"), x.Name, x.VenueType.ToString(), x.IsEnabled);

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

static RiskDecisionDto ToRiskDecisionDto(RiskDecision x, IReadOnlyDictionary<TradeIntentId, TradeIntent> intents, IReadOnlyDictionary<InstrumentId, string> symbols)
{
    intents.TryGetValue(x.TradeIntentId, out var intent);
    return new RiskDecisionDto(
        x.Id.ToString("D"),
        x.TradeIntentId.Value.ToString("D"),
        intent?.ModelRunId.Value.ToString("D"),
        intent?.InstrumentId.Value.ToString("D"),
        intent is null ? null : symbols.GetValueOrDefault(intent.InstrumentId),
        x.Status.ToString(),
        x.RejectReason.ToString(),
        x.Explanation,
        x.CreatedAtUtc);
}

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
public sealed record RiskDecisionDto(string Id, string TradeIntentId, string? ModelRunId, string? InstrumentId, string? Symbol, string Status, string RejectReason, string Explanation, DateTimeOffset CreatedAtUtc);
public sealed record FillDto(string Id, string BrokerExecutionId, string ChildOrderId, string InstrumentId, string? Symbol, string VenueId, string? VenueName, string Side, decimal BaseQuantity, decimal VenueQuantity, decimal Price, DateTimeOffset TradeDateUtc, DateTimeOffset ReceivedAtUtc);
public sealed record MarketDataSnapshotDto(string Id, string InstrumentId, string? Symbol, string VenueId, string? VenueName, decimal Bid, decimal Ask, decimal Mid, decimal Spread, string Source, DateTimeOffset SourceTimestampUtc, DateTimeOffset ReceivedAtUtc, long? SequenceNumber, bool IsSynthetic, DateTimeOffset CreatedAtUtc);
public sealed record MarketDataBarDto(string Id, string InstrumentId, string? Symbol, string VenueId, string? VenueName, string Timeframe, DateTimeOffset BarStartUtc, DateTimeOffset BarEndUtc, string Source, decimal BidOpen, decimal BidHigh, decimal BidLow, decimal BidClose, decimal AskOpen, decimal AskHigh, decimal AskLow, decimal AskClose, decimal MidOpen, decimal MidHigh, decimal MidLow, decimal MidClose, decimal SpreadOpen, decimal SpreadHigh, decimal SpreadLow, decimal SpreadClose, decimal SpreadAverage, int ObservationCount, DateTimeOffset? FirstSnapshotUtc, DateTimeOffset? LastSnapshotUtc, bool IsComplete, string QualityStatus, string? BuildRunId, string BuilderVersion, DateTimeOffset CreatedAtUtc);
public sealed record KillSwitchDto(string Id, bool IsActive, string? Reason, DateTimeOffset UpdatedAtUtc);
public sealed record InstrumentDto(string Id, string Symbol, string AssetClass, string BaseCurrency, string QuoteCurrency, int PricePrecision, int QuantityPrecision, bool IsEnabled);
public sealed record VenueDto(string Id, string Name, string VenueType, bool IsEnabled);
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
public sealed record KillSwitchRequest(string? Reason);
public sealed record FakeSnapshotsRequest(string? InstrumentSymbol, string? VenueName, DateTimeOffset StartUtc, int IntervalSeconds, int Count, decimal Bid, decimal Ask, decimal? BidStep, decimal? AskStep);
public sealed record BuildBarsRequest(string? VenueName, BarTimeframe Timeframe, DateTimeOffset StartUtc, DateTimeOffset EndUtc);
public sealed record OrdersResponse(IReadOnlyList<ParentOrderDto> ParentOrders, IReadOnlyList<ChildOrderDto> ChildOrders);
public sealed record ParentOrderDto(string Id, string TradeIntentId, string? InstrumentId, string ClientOrderId, string Side, decimal BaseQuantity, string Algo, string Status, DateTimeOffset CreatedAtUtc);
public sealed record ChildOrderDto(string Id, string ParentOrderId, string VenueId, string? InstrumentId, string ClientOrderId, string? BrokerOrderId, string Side, string OrderType, string TimeInForce, decimal BaseQuantity, decimal VenueQuantity, string Status, DateTimeOffset CreatedAtUtc);

public partial class Program;
