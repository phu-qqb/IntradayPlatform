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
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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
    builder.Services.AddSingleton<IBrokerPositionProvider, FakeBrokerPositionProvider>();
    builder.Services.AddSingleton<IBarBuilderService, BarBuilderService>();
}
else
{
    throw new InvalidOperationException($"Unsupported persistence provider '{persistenceProvider}'.");
}

var app = builder.Build();

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

app.MapGet("/health", async (IServiceProvider services, IWebHostEnvironment environment, IConfiguration configuration, IClock clock) =>
{
    var provider = configuration.GetValue("Persistence:Provider", "SqlServerLocal") ?? "SqlServerLocal";
    var gateway = services.GetRequiredService<IVenueExecutionGateway>();
    var marketDataProvider = services.GetRequiredService<IMarketDataProvider>();
    var health = new Dictionary<string, object?>
    {
        ["application"] = "QQ.Production.Intraday.Api",
        ["environment"] = environment.EnvironmentName,
        ["persistenceProvider"] = provider,
        ["executionGateway"] = gateway.GetType().Name,
        ["marketDataMode"] = marketDataProvider.GetType().Name,
        ["liveTradingEnabled"] = configuration.GetValue("Safety:AllowLiveTrading", false),
        ["externalConnectionsEnabled"] = configuration.GetValue("Safety:AllowExternalConnections", false),
        ["utcServerTime"] = clock.UtcNow
    };

    if (string.Equals(provider, "SqlServerLocal", StringComparison.OrdinalIgnoreCase))
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IntradayDbContext>();
        health["databaseReachable"] = await db.Database.CanConnectAsync();
        health["pendingMigrationsCount"] = (await db.Database.GetPendingMigrationsAsync()).Count();
        health["databaseTarget"] = "LocalDB";
    }
    else
    {
        health["databaseReachable"] = true;
        health["pendingMigrationsCount"] = 0;
        health["databaseTarget"] = "InMemory";
    }

    return Results.Ok(health);
});

app.MapGet("/model-runs", async (IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    return state.ModelRuns.OrderByDescending(x => x.ReceivedAtUtc);
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
    return Results.Created($"/model-runs/{run.Id.Value}", run);
});

app.MapPost("/model-runs/{id:guid}/process", async (Guid id, ProcessModelRunService service, IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var modelRunId = new ModelRunId(id);
    var result = await service.ProcessAsync(modelRunId, cancellationToken);
    var state = await repository.LoadStateAsync(cancellationToken);
    return Results.Ok(new
    {
        modelRunId = id,
        result.Processed,
        result.Blocked,
        result.Message,
        tradeIntentCount = state.TradeIntents.Count(x => x.ModelRunId == modelRunId),
        orderCount = state.ParentOrders.Count(x => state.TradeIntents.Any(t => t.Id == x.TradeIntentId && t.ModelRunId == modelRunId)),
        fillCount = state.Fills.Count,
        reconciliationBreakCount = state.ReconciliationBreaks.Count(x => state.ReconciliationRuns.Any(r => r.Id == x.ReconciliationRunId && r.ModelRunId == modelRunId))
    });
});

app.MapGet("/positions/internal", async (IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    return state.PositionLedger.GroupBy(x => x.InstrumentId).Select(x => new { InstrumentId = x.Key.Value, BaseQuantity = x.Sum(y => y.BaseQuantityDelta) });
});
app.MapGet("/positions/broker", async (IIntradayRepository repository, IBrokerPositionProvider provider, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    return await provider.GetPositionsAsync(state.BrokerAccounts.Single().Id, cancellationToken);
});
app.MapGet("/reconciliation/breaks", async (IIntradayRepository repository, CancellationToken cancellationToken) => (await repository.LoadStateAsync(cancellationToken)).ReconciliationBreaks);
app.MapGet("/trade-intents", async (IIntradayRepository repository, CancellationToken cancellationToken) => (await repository.LoadStateAsync(cancellationToken)).TradeIntents);
app.MapGet("/orders", async (IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    return new { state.ParentOrders, state.ChildOrders };
});
app.MapGet("/fills", async (IIntradayRepository repository, CancellationToken cancellationToken) => (await repository.LoadStateAsync(cancellationToken)).Fills);
app.MapGet("/market-data/snapshots", async (IIntradayRepository repository, string? instrument, string? venue, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var query = state.MarketData.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(instrument)) query = query.Where(x => x.InstrumentId == state.Instruments.Single(i => i.Symbol == instrument).Id);
    if (!string.IsNullOrWhiteSpace(venue)) query = query.Where(x => x.VenueId == state.Venues.Single(v => v.Name == venue).Id);
    if (fromUtc is not null) query = query.Where(x => x.SourceTimestampUtc >= fromUtc.Value);
    if (toUtc is not null) query = query.Where(x => x.SourceTimestampUtc < toUtc.Value);
    return query.OrderBy(x => x.SourceTimestampUtc);
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
app.MapGet("/market-data/bars", async (IIntradayRepository repository, string? instrument, string? venue, BarTimeframe? timeframe, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken cancellationToken) =>
{
    var state = await repository.LoadStateAsync(cancellationToken);
    var selectedTimeframe = timeframe ?? BarTimeframe.FifteenMinutes;
    var query = state.MarketDataBars.Where(x => x.Timeframe == selectedTimeframe).AsEnumerable();
    if (!string.IsNullOrWhiteSpace(instrument)) query = query.Where(x => x.InstrumentId == state.Instruments.Single(i => i.Symbol == instrument).Id);
    if (!string.IsNullOrWhiteSpace(venue)) query = query.Where(x => x.VenueId == state.Venues.Single(v => v.Name == venue).Id);
    if (fromUtc is not null) query = query.Where(x => x.BarStartUtc >= fromUtc.Value);
    if (toUtc is not null) query = query.Where(x => x.BarStartUtc < toUtc.Value);
    return query.OrderBy(x => x.BarStartUtc);
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
app.MapPost("/admin/kill-switch/clear", async (IIntradayRepository repository, CancellationToken cancellationToken) =>
{
    await repository.SetKillSwitchAsync(false, null, cancellationToken);
    return Results.Ok(new { active = false });
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

public sealed record CreateModelRunRequest(string? ModelName, string? Symbol, decimal? Weight, decimal NavUsd, TargetQuantityMode TargetQuantityMode, DateTimeOffset? AsOfUtc, DateTimeOffset? EffectiveAtUtc, int FrequencyMinutes, string? InputHash, string? SourceFileName, List<ModelRunWeightRequest>? Weights);
public sealed record ModelRunWeightRequest(string Symbol, decimal Weight, string? RawSecurityId);
public sealed record KillSwitchRequest(string? Reason);
public sealed record FakeSnapshotsRequest(string? InstrumentSymbol, string? VenueName, DateTimeOffset StartUtc, int IntervalSeconds, int Count, decimal Bid, decimal Ask, decimal? BidStep, decimal? AskStep);
public sealed record BuildBarsRequest(string? VenueName, BarTimeframe Timeframe, DateTimeOffset StartUtc, DateTimeOffset EndUtc);

public partial class Program;
