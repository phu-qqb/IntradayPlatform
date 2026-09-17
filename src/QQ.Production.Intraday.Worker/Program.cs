using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.PostgreSql;
using QQ.Production.Intraday.Infrastructure.Simulator;
using QQ.Production.Intraday.Infrastructure.SqlServer;
using QQ.Production.Intraday.Lmax.ConnectivityLab;
using QQ.Production.Intraday.Worker;
using Serilog;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);
if (args.Contains("--demo-config-inspect=true", StringComparer.Ordinal))
{
    var inspected = LmaxConnectivityLabOptions.FromEnvironmentAndArgs(args);
    Console.WriteLine(JsonSerializer.Serialize(new {
        marker = "DEMO_CONFIG_INSPECTION_ONLY", host = Environment.MachineName,
        accountMatches = inspected.AccountCode == LmaxDemoControlledSession.DemoAccountId,
        demoEndpoint = inspected.FixOrderHost == "fix-order.london-demo.lmax.com" && inspected.FixOrderPort == 443 && inspected.UseTls,
        credentialsPresent = !string.IsNullOrWhiteSpace(inspected.FixUsername) && !string.IsNullOrWhiteSpace(inspected.FixPassword),
        senderMatches = !string.IsNullOrWhiteSpace(inspected.FixUsername) && inspected.FixSenderCompId == inspected.FixUsername,
        demoOrderCapsDisabled = !inspected.DemoOrderCapsEnabled,
        streamingMarketData = inspected.MarketDataRequestMode == LmaxFixMarketDataRequestMode.SnapshotPlusUpdates,
        securityIdMarketData = inspected.MarketDataSymbolEncodingMode == LmaxFixMarketDataSymbolEncodingMode.SecurityId,
        brokerConnectionOpened = false, databaseAccessed = false
    }));
    return;
}
if (args.Contains("--demo-marketdata-inspect=true", StringComparer.Ordinal))
{
    var options = LmaxConnectivityLabOptions.FromEnvironmentAndArgs(args);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var client = new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator());
    var quote = await client.GetTopOfBookAsync(options, TimeSpan.FromSeconds(60), timeout.Token);
    Console.WriteLine(JsonSerializer.Serialize(new { marker = "DEMO_MARKET_DATA_PREFLIGHT_PASS",
        quote.BestBid, quote.BestAsk, quote.Mid, quote.ObservedAtUtc,
        orderConnectionOpened = false, orderSends = 0, databaseAccessed = false }));
    return;
}
var demoStrategyBridgeEnabled = builder.Configuration.GetValue("LmaxDemoStrategyBridge:Enabled", false);
var demoContinuingEnabled = builder.Configuration.GetValue("LmaxDemoContinuing:Enabled", false);
if (demoContinuingEnabled && (!demoStrategyBridgeEnabled || !builder.Configuration.GetValue("LmaxDemoCycle:Enabled", false)))
    throw new InvalidOperationException("DEMO_CONTINUING_REQUIRES_EXPLICIT_DEMO_COORDINATOR");
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IOperatorContext>(new StaticOperatorContext(OperatorAuditActorType.Worker, "system", "Local Worker"));
builder.Services.AddScoped<IOperatorAuditService, OperatorAuditService>();
builder.Services.AddScoped<IOperatorPermissionService, OperatorPermissionService>();
builder.Services.AddScoped<IApprovalWorkflowService, ApprovalWorkflowService>();
builder.Services.AddSingleton(new GovernanceOptions());
builder.Services.AddSingleton(new LocalSchedulerOptions(
    builder.Configuration.GetValue("LocalScheduler:Enabled", false),
    builder.Configuration.GetValue("LocalScheduler:PollIntervalSeconds", 30)));
builder.Services.AddSingleton(new BarBuilderOptions());
builder.Services.AddSingleton(new FakeLmaxOptions());
builder.Services.AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>();
builder.Services.AddSingleton<IMarketDataProvider, FakeMarketDataProvider>();
builder.Services.AddScoped<ProcessModelRunService>();
builder.Services.AddScoped<IReferenceDataIntegrityService, ReferenceDataIntegrityService>();
builder.Services.AddScoped<IExceptionCaseService, ExceptionCaseService>();
builder.Services.AddScoped<IRiskControlService, RiskControlService>();
builder.Services.AddScoped<IOperationalJobRunner, OperationalJobRunner>();
builder.Services.AddScoped<IOperationalRunbookRunner, OperationalRunbookRunner>();
builder.Services.AddScoped<IModelWeightPromotionService, ModelWeightPromotionService>();
builder.Services.AddScoped<IFakeModelWeightGenerator, FakeModelWeightGenerator>();
builder.Services.AddScoped<ILegacyAnubisWeightIngestionService, LegacyAnubisWeightIngestionService>();
builder.Services.AddScoped<ILegacyAnubisPortfolioWeightIngestionService, LegacyAnubisPortfolioWeightIngestionService>();
builder.Services.AddScoped<ILmaxCanonicalSnapshotIngestionService, LmaxCanonicalSnapshotIngestionService>();
builder.Services.AddScoped<ILmaxDemoCycleCoordinator, LmaxDemoCycleCoordinator>();
builder.Services.AddScoped<QubesWeightPersistenceService>();
builder.Services.AddSingleton(new LmaxEodReportOptions());
builder.Services.AddScoped<ILmaxEodReportImportService, LmaxEodReportImportService>();
builder.Services.AddScoped<ILmaxReportPairConsistencyService, LmaxReportPairConsistencyService>();
builder.Services.AddScoped<IEodReconciliationService, EodReconciliationService>();
builder.Services.AddScoped<IEodPnlSummaryService, EodPnlSummaryService>();
builder.Services.AddScoped<IFakeLmaxEodReportGenerator, FakeLmaxEodReportGenerator>();

if (demoStrategyBridgeEnabled)
{
    var lmaxOptions = LmaxConnectivityLabOptions.FromEnvironmentAndArgs(args);
    LmaxDemoStrategyVenueExecutionGateway.EnsureDemoOnly(lmaxOptions);
    builder.Services.AddSingleton(lmaxOptions);
    builder.Services.AddSingleton<LmaxConnectivityLabSafetyValidator>();
    if (demoContinuingEnabled)
    {
        var startPath = builder.Configuration["LmaxDemoContinuing:StartObservationPath"]
            ?? throw new InvalidOperationException("DEMO_CONTINUING_START_OBSERVATION_REQUIRED");
        var observation = JsonSerializer.Deserialize<LmaxDemoSessionStart>(File.ReadAllText(startPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("DEMO_CONTINUING_START_OBSERVATION_INVALID");
        if (observation.Simulated || Environment.MachineName != "EC2AMAZ-1QPHTD8")
            throw new InvalidOperationException("DEMO_CONTINUING_REAL_HOST_REQUIRED");
        builder.Services.AddSingleton(observation);
        builder.Services.AddSingleton<ILmaxDemoFixTransport, LmaxDemoTlsTransport>();
        builder.Services.AddSingleton(provider => new LmaxDemoContinuingSession(lmaxOptions, observation,
            provider.GetRequiredService<ILmaxDemoFixTransport>(),
            new RawLmaxFixSessionClient(provider.GetRequiredService<LmaxConnectivityLabSafetyValidator>()),
            provider.GetRequiredService<IClock>()));
        builder.Services.AddSingleton<ILmaxDemoStrategySession>(p => p.GetRequiredService<LmaxDemoContinuingSession>());
    }
    else
        builder.Services.AddScoped<ILmaxDemoStrategySession>(provider =>
            new RawLmaxFixSessionClient(provider.GetRequiredService<LmaxConnectivityLabSafetyValidator>()));
}

if (builder.Configuration.GetValue("Intraday15m:Enabled", false))
{
    var pollInterval = builder.Configuration.GetValue("Worker:PollInterval", TimeSpan.FromMinutes(15));
    if (pollInterval <= TimeSpan.Zero || pollInterval > TimeSpan.FromMinutes(
        PmsShadowIntradayCadenceContract.MaximumStartDelayMinutes))
        throw new InvalidOperationException("INTRADAY_15M_POLL_INTERVAL_EXCEEDS_MAXIMUM_START_DELAY");
    var pmsConnection = builder.Configuration.GetConnectionString("PmsShadowPostgreSql")
        ?? throw new InvalidOperationException("PMS_SHADOW_POSTGRESQL_CONNECTION_REQUIRED");
    var captureRoot = builder.Configuration.GetValue<string>("Intraday15m:RealSlotCaptureRoot")
        ?? throw new InvalidOperationException("REAL_SLOT_CAPTURE_ROOT_REQUIRED");
    var sourceSessionId = builder.Configuration.GetValue<string>("Intraday15m:SourceSessionId")
        ?? throw new InvalidOperationException("SOURCE_SESSION_ID_REQUIRED");
    builder.Services.AddPooledDbContextFactory<PmsShadowDbContext>(options =>
        options.UseNpgsql(pmsConnection, npgsql => npgsql.SetPostgresVersion(16, 0)));
    builder.Services.AddSingleton<IPmsShadowIntradaySlotStore, EfPmsShadowIntradaySlotStore>();
    builder.Services.AddSingleton<IPmsShadowIntradayEconomicProjectionStore,
        EfPmsShadowIntradayEconomicProjectionStore>();
    builder.Services.AddSingleton<IPmsShadowIntradaySlotPipeline>(provider =>
        new PmsShadowIntradayEconomicRefreshPipeline(captureRoot, sourceSessionId,
            provider.GetRequiredService<IPmsShadowIntradayEconomicProjectionStore>()));
    builder.Services.AddSingleton<PmsShadowIntradayScheduler>();
}

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
    builder.Services.AddScoped<IModelWeightBatchRepository, SqlServerModelWeightBatchRepository>();
    builder.Services.AddScoped<IQubesWeightAuditRepository, SqlServerQubesWeightAuditRepository>();
    builder.Services.AddScoped<ILmaxEodReportRepository, SqlServerLmaxEodReportRepository>();
    builder.Services.AddScoped<IOperatorAuditRepository, SqlServerOperatorAuditRepository>();
    builder.Services.AddScoped<IOperatorGovernanceRepository, SqlServerOperatorGovernanceRepository>();
    builder.Services.AddScoped<IExceptionCaseRepository, SqlServerExceptionCaseRepository>();
    builder.Services.AddScoped<IOperationalJobRepository, SqlServerOperationalJobRepository>();
    builder.Services.AddScoped<IOperationalRunbookRepository, SqlServerOperationalRunbookRepository>();
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
    builder.Services.AddSingleton<IQubesWeightAuditRepository, InMemoryQubesWeightAuditRepository>();
    builder.Services.AddSingleton<ILmaxEodReportRepository, InMemoryLmaxEodReportRepository>();
    builder.Services.AddSingleton<IOperatorAuditRepository, InMemoryOperatorAuditRepository>();
    builder.Services.AddSingleton<IOperatorGovernanceRepository, InMemoryOperatorGovernanceRepository>();
    builder.Services.AddSingleton<IExceptionCaseRepository, InMemoryExceptionCaseRepository>();
    builder.Services.AddSingleton<IOperationalJobRepository, InMemoryOperationalJobRepository>();
    builder.Services.AddSingleton<IOperationalRunbookRepository, InMemoryOperationalRunbookRepository>();
    builder.Services.AddSingleton<IBrokerPositionProvider, FakeBrokerPositionProvider>();
    builder.Services.AddSingleton<IBarBuilderService, BarBuilderService>();
}
else
{
    throw new InvalidOperationException($"Unsupported persistence provider '{persistenceProvider}'.");
}

if (demoContinuingEnabled)
{
    builder.Services.AddScoped<IVenueExecutionGateway, LmaxDemoContinuingGateway>();
    builder.Services.AddScoped<IBrokerPositionProvider, LmaxDemoContinuingBrokerPositionProvider>();
}
else if (demoStrategyBridgeEnabled)
{
    builder.Services.AddScoped<IVenueExecutionGateway, LmaxDemoStrategyVenueExecutionGateway>();
    builder.Services.AddScoped<IBrokerPositionProvider>(provider =>
        new LmaxDemoOperatorBrokerStateAttestationProvider(
            provider.GetRequiredService<IIntradayRepository>(),
            provider.GetRequiredService<LmaxConnectivityLabOptions>(),
            provider.GetRequiredService<IClock>()));
}

builder.Services.AddHostedService<Worker>();
builder.Services.AddSerilog(new LoggerConfiguration().WriteTo.Console().CreateLogger());

var host = builder.Build();
ValidateSafety(host, persistenceProvider);
await InitializeDatabaseAsync(host, persistenceProvider);
await ValidateReferenceDataAsync(host);
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
    if (configuration.GetValue("LmaxDemoCycle:Enabled", false))
    {
        if (!configuration.GetValue("LmaxDemoStrategyBridge:Enabled", false))
            throw new InvalidOperationException("LMAX Demo cycle requires LmaxDemoStrategyBridge:Enabled=true.");
        if (configuration.GetValue("ModelWeights:PromoteReadyBatches", false))
            throw new InvalidOperationException("LMAX Demo cycle forbids bulk ready-batch promotion.");
        if (configuration.GetValue("LegacyAnubisPortfolio:Enabled", false) || configuration.GetValue("LegacyAnubisWeights:Enabled", false))
            throw new InvalidOperationException("LMAX Demo cycle owns the only permitted Legacy Anubis portfolio ingestion.");
        if (configuration.GetValue("LmaxCanonicalSnapshotIngestion:Enabled", false))
            throw new InvalidOperationException("LMAX Demo cycle owns the only permitted canonical snapshot ingestion.");
        if (configuration.GetValue("Intraday15m:Enabled", false))
            throw new InvalidOperationException("LMAX Demo cycle cannot activate the PMS Shadow Intraday15m path.");
        if (!configuration.GetValue("LmaxDemoContinuing:Enabled", false) && !configuration.GetValue("Worker:StopAfterInitialLmaxDemoCycle", false))
            throw new InvalidOperationException("LMAX Demo cycle must be an explicit one-cycle worker invocation.");
        if (configuration.GetValue("LmaxDemoContinuing:Enabled", false))
        {
            if (configuration.GetValue("Worker:StopAfterInitialLmaxDemoCycle", true)
                || configuration.GetValue("Database:ApplyMigrationsOnStartup", false)
                || configuration.GetValue("Database:SeedReferenceDataOnStartup", true)
                || configuration.GetValue("Database:SeedDemoDataOnStartup", false)
                || configuration.GetValue("LocalScheduler:Enabled", false)
                || persistenceProvider != "SqlServerLocal")
                throw new InvalidOperationException("DEMO_CONTINUING_STARTUP_CONFIGURATION_INVALID");
        }
    }
    if (gateway is LmaxDemoStrategyVenueExecutionGateway or LmaxDemoContinuingGateway)
    {
        if (configuration.GetValue("Safety:AllowLiveTrading", false))
            throw new InvalidOperationException("Demo strategy bridge requires Safety:AllowLiveTrading=false.");
        if (!configuration.GetValue("Safety:AllowExternalConnections", false))
            throw new InvalidOperationException("Demo strategy bridge requires Safety:AllowExternalConnections=true.");
        if (configuration.GetValue("Safety:RequireFakeExecutionGateway", true))
            throw new InvalidOperationException("Demo strategy bridge requires Safety:RequireFakeExecutionGateway=false.");
        LmaxDemoStrategyVenueExecutionGateway.EnsureDemoOnly(
            scope.ServiceProvider.GetRequiredService<LmaxConnectivityLabOptions>());
    }
    else
    {
        if (!configuration.GetValue("Safety:AllowLiveTrading", false) && gateway is not FakeLmaxGateway)
            throw new InvalidOperationException("Live trading is disabled and the registered execution gateway is not FakeLmaxGateway.");
        if (configuration.GetValue("Safety:RequireFakeExecutionGateway", true) && gateway is not FakeLmaxGateway)
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

static async Task ValidateReferenceDataAsync(IHost host)
{
    using var scope = host.Services.CreateScope();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    if (!configuration.GetValue("ReferenceDataIntegrity:CheckOnStartup", true))
    {
        return;
    }

    var check = await scope.ServiceProvider.GetRequiredService<IReferenceDataIntegrityService>().CheckAsync(CancellationToken.None);
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ReferenceDataIntegrity");
    logger.LogInformation("Reference data integrity checked: BlockingIssues={BlockingIssueCount} WarningIssues={WarningIssueCount}", check.BlockingIssueCount, check.WarningIssueCount);
    if (check.BlockingIssueCount > 0 && configuration.GetValue("ReferenceDataIntegrity:FailStartupOnBlockingIssues", true))
    {
        throw new InvalidOperationException($"Reference data integrity check failed with {check.BlockingIssueCount} blocking issue(s). Run scripts/check-reference-data.ps1 for details or reset the local dev database if it contains old duplicate seed rows.");
    }
}

