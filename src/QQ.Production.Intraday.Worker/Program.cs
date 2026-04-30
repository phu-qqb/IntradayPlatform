using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.Simulator;
using QQ.Production.Intraday.Infrastructure.SqlServer;
using QQ.Production.Intraday.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton(new BarBuilderOptions());
builder.Services.AddSingleton(new FakeLmaxOptions());
builder.Services.AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>();
builder.Services.AddSingleton<IMarketDataProvider, FakeMarketDataProvider>();
builder.Services.AddScoped<ProcessModelRunService>();

var persistenceProvider = builder.Configuration.GetValue("Persistence:Provider", "SqlServerLocal") ?? "SqlServerLocal";
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

builder.Services.AddHostedService<Worker>();
builder.Services.AddSerilog(new LoggerConfiguration().WriteTo.Console().CreateLogger());

var host = builder.Build();
await InitializeDatabaseAsync(host, persistenceProvider);
ValidateSafety(host, persistenceProvider);
host.Run();

static async Task InitializeDatabaseAsync(IHost host, string persistenceProvider)
{
    if (!string.Equals(persistenceProvider, "SqlServerLocal", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    await using var scope = host.Services.CreateAsyncScope();
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
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");
            logger.LogWarning("Skipping reference seed because the LocalDB schema is not reachable. Run scripts/update-local-db.ps1 or enable Database:ApplyMigrationsOnStartup.");
            return;
        }

        await initializer.SeedReferenceDataAsync(CancellationToken.None);
    }

    if (configuration.GetValue("Database:SeedDemoDataOnStartup", false))
    {
        await initializer.SeedDemoDataAsync(CancellationToken.None);
    }
}

static void ValidateSafety(IHost host, string persistenceProvider)
{
    using var scope = host.Services.CreateScope();
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

    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("StartupSafety");
    logger.LogInformation(
        "Startup safety: PersistenceProvider={PersistenceProvider} DatabaseTarget={DatabaseTarget} ExecutionGateway={ExecutionGateway} MarketDataProvider={MarketDataProvider} AllowExternalConnections={AllowExternalConnections} AllowLiveTrading={AllowLiveTrading}",
        persistenceProvider,
        string.Equals(persistenceProvider, "SqlServerLocal", StringComparison.OrdinalIgnoreCase) ? "LocalDB" : "InMemory",
        gateway.GetType().Name,
        scope.ServiceProvider.GetRequiredService<IMarketDataProvider>().GetType().Name,
        configuration.GetValue("Safety:AllowExternalConnections", false),
        configuration.GetValue("Safety:AllowLiveTrading", false));
}
