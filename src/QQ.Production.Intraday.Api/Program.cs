using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.Simulator;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration).WriteTo.Console());

var seededState = SeedData.Create();
builder.Services.AddSingleton(seededState);
builder.Services.AddSingleton<IIntradayRepository, InMemoryIntradayRepository>();
builder.Services.AddSingleton(new FakeLmaxOptions());
builder.Services.AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>();
builder.Services.AddSingleton<IBrokerPositionProvider, FakeBrokerPositionProvider>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ProcessModelRunService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", liveConnectivity = false, executionGateway = "FakeLmax" }));
app.MapGet("/model-runs", (PlatformState state) => state.ModelRuns.OrderByDescending(x => x.ReceivedAtUtc));
app.MapPost("/model-runs", async (CreateModelRunRequest request, IIntradayRepository repository, PlatformState state, IClock clock, CancellationToken cancellationToken) =>
{
    var fund = state.Funds.Single();
    var instrument = state.Instruments.Single(x => x.Symbol == request.Symbol);
    var now = clock.UtcNow;
    var run = new ModelRun(ModelRunId.New(), fund.Id, request.ModelName, request.AsOfUtc ?? now, now, request.EffectiveAtUtc ?? now, request.FrequencyMinutes <= 0 ? 15 : request.FrequencyMinutes, request.NavUsd <= 0 ? 1_000_000m : request.NavUsd, ModelRunStatus.Received, request.InputHash ?? Guid.NewGuid().ToString("N"), request.SourceFileName ?? "api", false, request.TargetQuantityMode);
    await repository.AddModelRunAsync(run, [new TargetWeight(run.Id, instrument.Id, request.Weight, request.Symbol)], cancellationToken);
    return Results.Created($"/model-runs/{run.Id.Value}", run);
});
app.MapPost("/model-runs/{id:guid}/process", async (Guid id, ProcessModelRunService service, CancellationToken cancellationToken) => await service.ProcessAsync(new ModelRunId(id), cancellationToken));
app.MapGet("/positions/internal", (PlatformState state) => state.PositionLedger.GroupBy(x => x.InstrumentId).Select(x => new { InstrumentId = x.Key.Value, BaseQuantity = x.Sum(y => y.BaseQuantityDelta) }));
app.MapGet("/positions/broker", async (PlatformState state, IBrokerPositionProvider provider, CancellationToken cancellationToken) => await provider.GetPositionsAsync(state.BrokerAccounts.Single().Id, cancellationToken));
app.MapGet("/reconciliation/breaks", (PlatformState state) => state.ReconciliationBreaks);
app.MapGet("/trade-intents", (PlatformState state) => state.TradeIntents);
app.MapGet("/orders", (PlatformState state) => new { ParentOrders = state.ParentOrders, ChildOrders = state.ChildOrders });
app.MapGet("/fills", (PlatformState state) => state.Fills);
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

public sealed record CreateModelRunRequest(string ModelName, string Symbol, decimal Weight, decimal NavUsd, TargetQuantityMode TargetQuantityMode, DateTimeOffset? AsOfUtc, DateTimeOffset? EffectiveAtUtc, int FrequencyMinutes, string? InputHash, string? SourceFileName);
public sealed record KillSwitchRequest(string? Reason);
