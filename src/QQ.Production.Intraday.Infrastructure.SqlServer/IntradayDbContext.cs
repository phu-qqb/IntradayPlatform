using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Infrastructure.SqlServer;

public sealed class IntradayDbContext(DbContextOptions<IntradayDbContext> options) : DbContext(options)
{
    public DbSet<Fund> Funds => Set<Fund>();
    public DbSet<BrokerAccount> BrokerAccounts => Set<BrokerAccount>();
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<VenueInstrumentMapping> VenueInstrumentMappings => Set<VenueInstrumentMapping>();
    public DbSet<NavSnapshot> NavSnapshots => Set<NavSnapshot>();
    public DbSet<ModelRun> ModelRuns => Set<ModelRun>();
    public DbSet<TargetWeight> TargetWeights => Set<TargetWeight>();
    public DbSet<TargetPosition> TargetPositions => Set<TargetPosition>();
    public DbSet<DriftSnapshot> DriftSnapshots => Set<DriftSnapshot>();
    public DbSet<MarketDataSnapshot> MarketDataSnapshots => Set<MarketDataSnapshot>();
    public DbSet<MarketDataBar> MarketDataBars => Set<MarketDataBar>();
    public DbSet<BarBuildRun> BarBuildRuns => Set<BarBuildRun>();
    public DbSet<PositionLedgerEvent> PositionLedgerEvents => Set<PositionLedgerEvent>();
    public DbSet<ReconciliationRun> ReconciliationRuns => Set<ReconciliationRun>();
    public DbSet<ReconciliationBreak> ReconciliationBreaks => Set<ReconciliationBreak>();
    public DbSet<TradeIntent> TradeIntents => Set<TradeIntent>();
    public DbSet<RiskDecision> RiskDecisions => Set<RiskDecision>();
    public DbSet<ParentOrder> ParentOrders => Set<ParentOrder>();
    public DbSet<ChildOrder> ChildOrders => Set<ChildOrder>();
    public DbSet<ExecutionReport> ExecutionReports => Set<ExecutionReport>();
    public DbSet<Fill> Fills => Set<Fill>();
    public DbSet<RiskLimitSet> RiskLimitSets => Set<RiskLimitSet>();
    public DbSet<RiskLimit> RiskLimits => Set<RiskLimit>();
    public DbSet<InstrumentRiskLimit> InstrumentRiskLimits => Set<InstrumentRiskLimit>();
    public DbSet<VenueRiskLimit> VenueRiskLimits => Set<VenueRiskLimit>();
    public DbSet<TradingWindow> TradingWindows => Set<TradingWindow>();
    public DbSet<KillSwitchState> KillSwitchStates => Set<KillSwitchState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureStronglyTypedIds(modelBuilder);

        modelBuilder.Entity<Fund>().HasKey(x => x.Id);
        modelBuilder.Entity<BrokerAccount>().HasKey(x => x.Id);
        modelBuilder.Entity<Instrument>().HasKey(x => x.Id);
        modelBuilder.Entity<Venue>().HasKey(x => x.Id);
        modelBuilder.Entity<VenueInstrumentMapping>().HasKey(x => x.Id);
        modelBuilder.Entity<NavSnapshot>().HasKey(nameof(NavSnapshot.FundId), nameof(NavSnapshot.AsOfUtc));
        modelBuilder.Entity<ModelRun>().HasKey(x => x.Id);
        modelBuilder.Entity<TargetWeight>().HasKey(nameof(TargetWeight.ModelRunId), nameof(TargetWeight.InstrumentId));
        modelBuilder.Entity<TargetPosition>().HasKey(nameof(TargetPosition.ModelRunId), nameof(TargetPosition.InstrumentId));
        modelBuilder.Entity<DriftSnapshot>().HasKey(nameof(DriftSnapshot.ModelRunId), nameof(DriftSnapshot.InstrumentId));
        modelBuilder.Entity<MarketDataSnapshot>().HasKey(x => x.Id);
        modelBuilder.Entity<MarketDataBar>().HasKey(x => x.Id);
        modelBuilder.Entity<BarBuildRun>().HasKey(x => x.Id);
        modelBuilder.Entity<PositionLedgerEvent>().HasKey(x => x.Id);
        modelBuilder.Entity<ReconciliationRun>().HasKey(x => x.Id);
        modelBuilder.Entity<ReconciliationBreak>().HasKey(x => x.Id);
        modelBuilder.Entity<TradeIntent>().HasKey(x => x.Id);
        modelBuilder.Entity<RiskDecision>().HasKey(x => x.Id);
        modelBuilder.Entity<ParentOrder>().HasKey(x => x.Id);
        modelBuilder.Entity<ChildOrder>().HasKey(x => x.Id);
        modelBuilder.Entity<ExecutionReport>().HasKey(x => x.Id);
        modelBuilder.Entity<Fill>().HasKey(x => x.Id);
        modelBuilder.Entity<RiskLimitSet>().HasKey(x => x.Id);
        modelBuilder.Entity<RiskLimit>().HasKey(x => x.Id);
        modelBuilder.Entity<InstrumentRiskLimit>().HasKey(x => x.Id);
        modelBuilder.Entity<VenueRiskLimit>().HasKey(x => x.Id);
        modelBuilder.Entity<TradingWindow>().HasKey(x => x.Id);
        modelBuilder.Entity<KillSwitchState>().HasKey(x => x.Id);

        modelBuilder.Entity<ModelRun>().HasIndex(x => x.Id).IsUnique();
        modelBuilder.Entity<Fund>().HasIndex(x => x.Name).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<BrokerAccount>().HasIndex(x => new { x.FundId, x.AccountCode }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<Instrument>().HasIndex(x => new { x.Symbol, x.AssetClass }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<Venue>().HasIndex(x => x.Name).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<VenueInstrumentMapping>().HasIndex(x => new { x.VenueId, x.InstrumentId }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<VenueInstrumentMapping>().HasIndex(x => new { x.VenueId, x.VenueSymbol }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<RiskLimitSet>().HasIndex(x => x.FundId).IsUnique();
        modelBuilder.Entity<RiskLimit>().HasIndex(x => new { x.RiskLimitSetId, x.Name }).IsUnique();
        modelBuilder.Entity<InstrumentRiskLimit>().HasIndex(x => new { x.RiskLimitSetId, x.InstrumentId }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<VenueRiskLimit>().HasIndex(x => new { x.RiskLimitSetId, x.VenueId }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<TradingWindow>().HasIndex(x => new { x.FundId, x.ModelName, x.DayOfWeek }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<Fill>().HasIndex(x => new { x.VenueId, x.BrokerExecutionId }).IsUnique();
        modelBuilder.Entity<ParentOrder>().HasIndex(x => x.ClientOrderId).IsUnique();
        modelBuilder.Entity<ChildOrder>().HasIndex(x => x.ClientOrderId).IsUnique();
        modelBuilder.Entity<MarketDataSnapshot>().HasIndex(x => new { x.InstrumentId, x.VenueId, x.ReceivedAtUtc });
        modelBuilder.Entity<MarketDataBar>().HasIndex(x => new { x.InstrumentId, x.VenueId, x.Timeframe, x.BarStartUtc }).IsUnique();
        modelBuilder.Entity<ModelRun>().HasIndex(x => new { x.FundId, x.IsProcessed, x.ReceivedAtUtc });
        modelBuilder.Entity<PositionLedgerEvent>().HasIndex(x => new { x.FundId, x.InstrumentId, x.CreatedAtUtc });
        modelBuilder.Entity<ReconciliationBreak>().HasIndex(x => new { x.ReconciliationRunId, x.Severity, x.Status });
        modelBuilder.Entity<TradeIntent>().HasIndex(x => new { x.ModelRunId, x.InstrumentId });
        modelBuilder.Entity<ExecutionReport>().HasIndex(x => new { x.ChildOrderId, x.ReceivedAtUtc });

        modelBuilder.Entity<BrokerAccount>().HasOne<Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<NavSnapshot>().HasOne<Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ModelRun>().HasOne<Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TargetWeight>().HasOne<ModelRun>().WithMany().HasForeignKey(x => x.ModelRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TargetWeight>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TargetPosition>().HasOne<ModelRun>().WithMany().HasForeignKey(x => x.ModelRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TargetPosition>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DriftSnapshot>().HasOne<ModelRun>().WithMany().HasForeignKey(x => x.ModelRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DriftSnapshot>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VenueInstrumentMapping>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VenueInstrumentMapping>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MarketDataSnapshot>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MarketDataSnapshot>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MarketDataBar>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MarketDataBar>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PositionLedgerEvent>().HasOne<Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PositionLedgerEvent>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReconciliationRun>().HasOne<ModelRun>().WithMany().HasForeignKey(x => x.ModelRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReconciliationBreak>().HasOne<ReconciliationRun>().WithMany().HasForeignKey(x => x.ReconciliationRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TradeIntent>().HasOne<ModelRun>().WithMany().HasForeignKey(x => x.ModelRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TradeIntent>().HasOne<Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TradeIntent>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RiskDecision>().HasOne<TradeIntent>().WithMany().HasForeignKey(x => x.TradeIntentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ParentOrder>().HasOne<TradeIntent>().WithMany().HasForeignKey(x => x.TradeIntentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChildOrder>().HasOne<ParentOrder>().WithMany().HasForeignKey(x => x.ParentOrderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChildOrder>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ExecutionReport>().HasOne<ChildOrder>().WithMany().HasForeignKey(x => x.ChildOrderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ExecutionReport>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Fill>().HasOne<ChildOrder>().WithMany().HasForeignKey(x => x.ChildOrderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Fill>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Fill>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RiskLimitSet>().HasOne<Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InstrumentRiskLimit>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VenueRiskLimit>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TradingWindow>().HasOne<Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties().Where(x => x.ClrType == typeof(decimal) || x.ClrType == typeof(decimal?)))
            {
                property.SetPrecision(28);
                property.SetScale(10);
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }

        modelBuilder.Entity<Fund>().Property(x => x.BaseCurrency).HasConversion(x => x.Code, x => new Currency(x)).HasMaxLength(3);
        modelBuilder.Entity<Instrument>().Property(x => x.BaseCurrency).HasConversion(x => x.Code, x => new Currency(x)).HasMaxLength(3);
        modelBuilder.Entity<Instrument>().Property(x => x.QuoteCurrency).HasConversion(x => x.Code, x => new Currency(x)).HasMaxLength(3);
    }

    private static void ConfigureStronglyTypedIds(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Fund>().Property(x => x.Id).HasConversion(x => x.Value, x => new FundId(x));
        modelBuilder.Entity<BrokerAccount>().Property(x => x.Id).HasConversion(x => x.Value, x => new BrokerAccountId(x));
        modelBuilder.Entity<BrokerAccount>().Property(x => x.FundId).HasConversion(x => x.Value, x => new FundId(x));
        modelBuilder.Entity<Instrument>().Property(x => x.Id).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<Venue>().Property(x => x.Id).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<VenueInstrumentMapping>().Property(x => x.Id).HasConversion(x => x.Value, x => new VenueInstrumentId(x));
        modelBuilder.Entity<VenueInstrumentMapping>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<VenueInstrumentMapping>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<NavSnapshot>().Property(x => x.FundId).HasConversion(x => x.Value, x => new FundId(x));
        modelBuilder.Entity<ModelRun>().Property(x => x.Id).HasConversion(x => x.Value, x => new ModelRunId(x));
        modelBuilder.Entity<ModelRun>().Property(x => x.FundId).HasConversion(x => x.Value, x => new FundId(x));
        modelBuilder.Entity<TargetWeight>().Property(x => x.ModelRunId).HasConversion(x => x.Value, x => new ModelRunId(x));
        modelBuilder.Entity<TargetWeight>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<TargetPosition>().Property(x => x.ModelRunId).HasConversion(x => x.Value, x => new ModelRunId(x));
        modelBuilder.Entity<TargetPosition>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<DriftSnapshot>().Property(x => x.ModelRunId).HasConversion(x => x.Value, x => new ModelRunId(x));
        modelBuilder.Entity<DriftSnapshot>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<MarketDataSnapshot>().Property(x => x.Id).HasConversion(x => x.Value, x => new MarketDataSnapshotId(x));
        modelBuilder.Entity<MarketDataSnapshot>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<MarketDataSnapshot>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<MarketDataBar>().Property(x => x.Id).HasConversion(x => x.Value, x => new MarketDataBarId(x));
        modelBuilder.Entity<MarketDataBar>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<MarketDataBar>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<MarketDataBar>().Property(x => x.BuildRunId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new BarBuildRunId(x.Value) : null);
        modelBuilder.Entity<BarBuildRun>().Property(x => x.Id).HasConversion(x => x.Value, x => new BarBuildRunId(x));
        modelBuilder.Entity<PositionLedgerEvent>().Property(x => x.FundId).HasConversion(x => x.Value, x => new FundId(x));
        modelBuilder.Entity<PositionLedgerEvent>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<ReconciliationRun>().Property(x => x.ModelRunId).HasConversion(x => x.Value, x => new ModelRunId(x));
        modelBuilder.Entity<ReconciliationBreak>().Property(x => x.InstrumentId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new InstrumentId(x.Value) : null);
        modelBuilder.Entity<TradeIntent>().Property(x => x.Id).HasConversion(x => x.Value, x => new TradeIntentId(x));
        modelBuilder.Entity<TradeIntent>().Property(x => x.ModelRunId).HasConversion(x => x.Value, x => new ModelRunId(x));
        modelBuilder.Entity<TradeIntent>().Property(x => x.FundId).HasConversion(x => x.Value, x => new FundId(x));
        modelBuilder.Entity<TradeIntent>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<RiskDecision>().Property(x => x.TradeIntentId).HasConversion(x => x.Value, x => new TradeIntentId(x));
        modelBuilder.Entity<ParentOrder>().Property(x => x.Id).HasConversion(x => x.Value, x => new ParentOrderId(x));
        modelBuilder.Entity<ParentOrder>().Property(x => x.TradeIntentId).HasConversion(x => x.Value, x => new TradeIntentId(x));
        modelBuilder.Entity<ParentOrder>().Property(x => x.ClientOrderId).HasConversion(x => x.Value, x => new ClientOrderId(x));
        modelBuilder.Entity<ChildOrder>().Property(x => x.Id).HasConversion(x => x.Value, x => new ChildOrderId(x));
        modelBuilder.Entity<ChildOrder>().Property(x => x.ParentOrderId).HasConversion(x => x.Value, x => new ParentOrderId(x));
        modelBuilder.Entity<ChildOrder>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<ChildOrder>().Property(x => x.ClientOrderId).HasConversion(x => x.Value, x => new ClientOrderId(x));
        modelBuilder.Entity<ExecutionReport>().Property(x => x.Id).HasConversion(x => x.Value, x => new ExecutionReportId(x));
        modelBuilder.Entity<ExecutionReport>().Property(x => x.ChildOrderId).HasConversion(x => x.Value, x => new ChildOrderId(x));
        modelBuilder.Entity<ExecutionReport>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<ExecutionReport>().Property(x => x.ClientOrderId).HasConversion(x => x.Value, x => new ClientOrderId(x));
        modelBuilder.Entity<Fill>().Property(x => x.Id).HasConversion(x => x.Value, x => new FillId(x));
        modelBuilder.Entity<Fill>().Property(x => x.ChildOrderId).HasConversion(x => x.Value, x => new ChildOrderId(x));
        modelBuilder.Entity<Fill>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<Fill>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<RiskLimitSet>().Property(x => x.FundId).HasConversion(x => x.Value, x => new FundId(x));
        modelBuilder.Entity<RiskLimitSet>().Property(x => x.MaxModelRunAge).HasConversion(x => x.Ticks, x => TimeSpan.FromTicks(x));
        modelBuilder.Entity<RiskLimitSet>().Property(x => x.MaxMarketDataAge).HasConversion(x => x.Ticks, x => TimeSpan.FromTicks(x));
        modelBuilder.Entity<InstrumentRiskLimit>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<VenueRiskLimit>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<TradingWindow>().Property(x => x.FundId).HasConversion(x => x.Value, x => new FundId(x));
    }
}

public sealed class SqlServerIntradayRepository(IntradayDbContext dbContext) : IIntradayRepository
{
    public async Task<PlatformState> LoadStateAsync(CancellationToken cancellationToken)
    {
        var state = new PlatformState();
        state.Funds.AddRange(await dbContext.Funds.AsNoTracking().ToListAsync(cancellationToken));
        state.BrokerAccounts.AddRange(await dbContext.BrokerAccounts.AsNoTracking().ToListAsync(cancellationToken));
        state.Instruments.AddRange(await dbContext.Instruments.AsNoTracking().ToListAsync(cancellationToken));
        state.Venues.AddRange(await dbContext.Venues.AsNoTracking().ToListAsync(cancellationToken));
        state.VenueInstrumentMappings.AddRange(await dbContext.VenueInstrumentMappings.AsNoTracking().ToListAsync(cancellationToken));
        state.NavSnapshots.AddRange(await dbContext.Set<NavSnapshot>().AsNoTracking().ToListAsync(cancellationToken));
        state.ModelRuns.AddRange(await dbContext.ModelRuns.AsNoTracking().ToListAsync(cancellationToken));
        state.TargetWeights.AddRange(await dbContext.TargetWeights.AsNoTracking().ToListAsync(cancellationToken));
        state.TargetPositions.AddRange(await dbContext.TargetPositions.AsNoTracking().ToListAsync(cancellationToken));
        state.DriftSnapshots.AddRange(await dbContext.DriftSnapshots.AsNoTracking().ToListAsync(cancellationToken));
        state.MarketData.AddRange(await dbContext.MarketDataSnapshots.AsNoTracking().ToListAsync(cancellationToken));
        state.MarketDataBars.AddRange(await dbContext.MarketDataBars.AsNoTracking().ToListAsync(cancellationToken));
        state.BarBuildRuns.AddRange(await dbContext.BarBuildRuns.AsNoTracking().ToListAsync(cancellationToken));
        state.PositionLedger.AddRange(await dbContext.PositionLedgerEvents.AsNoTracking().ToListAsync(cancellationToken));
        state.ReconciliationRuns.AddRange(await dbContext.ReconciliationRuns.AsNoTracking().ToListAsync(cancellationToken));
        state.ReconciliationBreaks.AddRange(await dbContext.ReconciliationBreaks.AsNoTracking().ToListAsync(cancellationToken));
        state.TradeIntents.AddRange(await dbContext.TradeIntents.AsNoTracking().ToListAsync(cancellationToken));
        state.RiskDecisions.AddRange(await dbContext.RiskDecisions.AsNoTracking().ToListAsync(cancellationToken));
        state.ParentOrders.AddRange(await dbContext.ParentOrders.AsNoTracking().ToListAsync(cancellationToken));
        state.ChildOrders.AddRange(await dbContext.ChildOrders.AsNoTracking().ToListAsync(cancellationToken));
        state.ExecutionReports.AddRange(await dbContext.ExecutionReports.AsNoTracking().ToListAsync(cancellationToken));
        state.Fills.AddRange(await dbContext.Fills.AsNoTracking().ToListAsync(cancellationToken));
        state.RiskLimitSets.AddRange(await dbContext.RiskLimitSets.AsNoTracking().ToListAsync(cancellationToken));
        state.RiskLimits.AddRange(await dbContext.RiskLimits.AsNoTracking().ToListAsync(cancellationToken));
        state.InstrumentRiskLimits.AddRange(await dbContext.InstrumentRiskLimits.AsNoTracking().ToListAsync(cancellationToken));
        state.VenueRiskLimits.AddRange(await dbContext.VenueRiskLimits.AsNoTracking().ToListAsync(cancellationToken));
        state.TradingWindows.AddRange(await dbContext.TradingWindows.AsNoTracking().ToListAsync(cancellationToken));
        state.KillSwitchStates.AddRange(await dbContext.KillSwitchStates.AsNoTracking().ToListAsync(cancellationToken));
        state.KillSwitch = state.KillSwitchStates.OrderByDescending(x => x.UpdatedAtUtc).FirstOrDefault() ?? state.KillSwitch;
        return state;
    }

    public Task<ModelRun?> GetNextUnprocessedModelRunAsync(CancellationToken cancellationToken)
        => dbContext.ModelRuns.OrderBy(x => x.ReceivedAtUtc).FirstOrDefaultAsync(x => !x.IsProcessed, cancellationToken);

    public Task<ModelRun?> GetModelRunAsync(ModelRunId modelRunId, CancellationToken cancellationToken)
        => dbContext.ModelRuns.FirstOrDefaultAsync(x => x.Id == modelRunId, cancellationToken);

    public async Task AddModelRunAsync(ModelRun modelRun, IReadOnlyList<TargetWeight> weights, CancellationToken cancellationToken)
    {
        if (await dbContext.ModelRuns.AnyAsync(x => x.Id == modelRun.Id, cancellationToken))
        {
            return;
        }

        dbContext.ModelRuns.Add(modelRun);
        dbContext.TargetWeights.AddRange(weights);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkModelRunProcessedAsync(ModelRunId modelRunId, ModelRunStatus status, CancellationToken cancellationToken)
    {
        var run = await dbContext.ModelRuns.FirstAsync(x => x.Id == modelRunId, cancellationToken);
        dbContext.Entry(run).CurrentValues.SetValues(run with { IsProcessed = true, Status = status });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveReconciliationAsync(ReconciliationRun run, IReadOnlyList<ReconciliationBreak> breaks, CancellationToken cancellationToken)
    {
        if (await dbContext.ReconciliationRuns.AnyAsync(x => x.ModelRunId == run.ModelRunId && x.Phase == run.Phase, cancellationToken))
        {
            return;
        }

        dbContext.ReconciliationRuns.Add(run);
        dbContext.ReconciliationBreaks.AddRange(breaks);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveTargetAndDriftAsync(TargetPosition targetPosition, DriftSnapshot driftSnapshot, CancellationToken cancellationToken)
    {
        if (!await dbContext.TargetPositions.AnyAsync(x => x.ModelRunId == targetPosition.ModelRunId && x.InstrumentId == targetPosition.InstrumentId, cancellationToken))
        {
            dbContext.TargetPositions.Add(targetPosition);
        }

        if (!await dbContext.DriftSnapshots.AnyAsync(x => x.ModelRunId == driftSnapshot.ModelRunId && x.InstrumentId == driftSnapshot.InstrumentId, cancellationToken))
        {
            dbContext.DriftSnapshots.Add(driftSnapshot);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddTradeIntentAsync(TradeIntent intent, CancellationToken cancellationToken)
    {
        dbContext.TradeIntents.Add(intent);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRiskDecisionAsync(RiskDecision decision, CancellationToken cancellationToken)
    {
        if (await dbContext.RiskDecisions.AnyAsync(x => x.TradeIntentId == decision.TradeIntentId, cancellationToken))
        {
            return;
        }

        dbContext.RiskDecisions.Add(decision);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddOrdersAsync(ParentOrder parentOrder, ChildOrder childOrder, CancellationToken cancellationToken)
    {
        dbContext.ParentOrders.Add(parentOrder);
        dbContext.ChildOrders.Add(childOrder);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddExecutionReportAsync(ExecutionReport report, CancellationToken cancellationToken)
    {
        if (await dbContext.ExecutionReports.AnyAsync(x => x.Id == report.Id, cancellationToken))
        {
            return;
        }

        dbContext.ExecutionReports.Add(report);
        var child = await dbContext.ChildOrders.FirstOrDefaultAsync(x => x.Id == report.ChildOrderId, cancellationToken);
        if (child is not null)
        {
            var childStatus = new OrderStateMachine().Transition(child.Status, report.ExecutionReportType);
            dbContext.Entry(child).CurrentValues.SetValues(child with { Status = childStatus });

            var parent = await dbContext.ParentOrders.FirstOrDefaultAsync(x => x.Id == child.ParentOrderId, cancellationToken);
            if (parent is not null)
            {
                var parentStatus = report.ExecutionReportType switch
                {
                    ExecutionReportType.OrderReject => OrderStatus.Rejected,
                    ExecutionReportType.Fill => OrderStatus.Filled,
                    ExecutionReportType.PartialFill => OrderStatus.PartiallyFilled,
                    ExecutionReportType.Expired when parent.Status == OrderStatus.PartiallyFilled => OrderStatus.PartiallyFilled,
                    ExecutionReportType.Expired => OrderStatus.Expired,
                    _ => parent.Status
                };
                dbContext.Entry(parent).CurrentValues.SetValues(parent with { Status = parentStatus });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryAddFillAsync(Fill fill, CancellationToken cancellationToken)
    {
        if (await dbContext.Fills.AnyAsync(x => x.VenueId == fill.VenueId && x.BrokerExecutionId == fill.BrokerExecutionId, cancellationToken))
        {
            return false;
        }

        dbContext.Fills.Add(fill);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task AddPositionLedgerEventAsync(PositionLedgerEvent ledgerEvent, CancellationToken cancellationToken)
    {
        dbContext.PositionLedgerEvents.Add(ledgerEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetKillSwitchAsync(bool isActive, string? reason, CancellationToken cancellationToken)
    {
        dbContext.KillSwitchStates.Add(new KillSwitchState(Guid.NewGuid(), isActive, reason, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SqlServerMarketDataSnapshotRepository(IntradayDbContext dbContext) : IMarketDataSnapshotRepository
{
    public async Task AddAsync(MarketDataSnapshot snapshot, CancellationToken cancellationToken)
    {
        snapshot.Validate();
        dbContext.MarketDataSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IReadOnlyList<MarketDataSnapshot> snapshots, CancellationToken cancellationToken)
    {
        foreach (var snapshot in snapshots)
        {
            snapshot.Validate();
        }

        dbContext.MarketDataSnapshots.AddRange(snapshots);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<MarketDataSnapshot?> GetLatestAsync(InstrumentId instrumentId, VenueId venueId, CancellationToken cancellationToken)
        => dbContext.MarketDataSnapshots.AsNoTracking().Where(x => x.InstrumentId == instrumentId && x.VenueId == venueId).OrderByDescending(x => x.SourceTimestampUtc).ThenByDescending(x => x.ReceivedAtUtc).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MarketDataSnapshot>> GetRangeAsync(InstrumentId instrumentId, VenueId venueId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
        => await dbContext.MarketDataSnapshots.AsNoTracking()
            .Where(x => x.InstrumentId == instrumentId && x.VenueId == venueId && x.SourceTimestampUtc >= startUtc && x.SourceTimestampUtc < endUtc)
            .OrderBy(x => x.SourceTimestampUtc)
            .ToListAsync(cancellationToken);
}

public sealed class SqlServerMarketDataBarRepository(IntradayDbContext dbContext) : IMarketDataBarRepository
{
    public async Task<BarUpsertResult> UpsertAsync(MarketDataBar bar, CancellationToken cancellationToken)
    {
        bar.Validate();
        var existing = await dbContext.MarketDataBars.FirstOrDefaultAsync(x => x.InstrumentId == bar.InstrumentId && x.VenueId == bar.VenueId && x.Timeframe == bar.Timeframe && x.BarStartUtc == bar.BarStartUtc, cancellationToken);
        if (existing is null)
        {
            dbContext.MarketDataBars.Add(bar);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new BarUpsertResult(true);
        }

        dbContext.Entry(existing).CurrentValues.SetValues(bar with { Id = existing.Id });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new BarUpsertResult(false);
    }

    public Task<MarketDataBar?> GetAsync(InstrumentId instrumentId, VenueId venueId, BarTimeframe timeframe, DateTimeOffset barStartUtc, CancellationToken cancellationToken)
        => dbContext.MarketDataBars.AsNoTracking().FirstOrDefaultAsync(x => x.InstrumentId == instrumentId && x.VenueId == venueId && x.Timeframe == timeframe && x.BarStartUtc == barStartUtc, cancellationToken);

    public async Task<IReadOnlyList<MarketDataBar>> GetRangeAsync(InstrumentId instrumentId, VenueId venueId, BarTimeframe timeframe, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
        => await dbContext.MarketDataBars.AsNoTracking()
            .Where(x => x.InstrumentId == instrumentId && x.VenueId == venueId && x.Timeframe == timeframe && x.BarStartUtc >= startUtc && x.BarStartUtc < endUtc)
            .OrderBy(x => x.BarStartUtc)
            .ToListAsync(cancellationToken);
}

public sealed class SqlServerBarBuildRunRepository(IntradayDbContext dbContext, IClock clock) : IBarBuildRunRepository
{
    public async Task AddAsync(BarBuildRun run, CancellationToken cancellationToken)
    {
        dbContext.BarBuildRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkCompletedAsync(BarBuildRunId runId, int barsCreated, int barsUpdated, CancellationToken cancellationToken)
    {
        var run = await dbContext.BarBuildRuns.FirstAsync(x => x.Id == runId, cancellationToken);
        dbContext.Entry(run).CurrentValues.SetValues(run with { Status = BarBuildRunStatus.Completed, CompletedAtUtc = clock.UtcNow, BarsCreated = barsCreated, BarsUpdated = barsUpdated });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(BarBuildRunId runId, string errorMessage, CancellationToken cancellationToken)
    {
        var run = await dbContext.BarBuildRuns.FirstAsync(x => x.Id == runId, cancellationToken);
        dbContext.Entry(run).CurrentValues.SetValues(run with { Status = BarBuildRunStatus.Failed, CompletedAtUtc = clock.UtcNow, ErrorMessage = errorMessage });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SqlServerFakeBrokerPositionProvider(IntradayDbContext dbContext, IClock clock) : IBrokerPositionProvider
{
    public async Task<IReadOnlyList<BrokerPositionSnapshot>> GetPositionsAsync(BrokerAccountId brokerAccountId, CancellationToken cancellationToken)
    {
        var account = await dbContext.BrokerAccounts.AsNoTracking().SingleAsync(x => x.Id == brokerAccountId, cancellationToken);
        return await dbContext.PositionLedgerEvents.AsNoTracking()
            .Where(x => x.FundId == account.FundId)
            .GroupBy(x => x.InstrumentId)
            .Select(x => new BrokerPositionSnapshot(brokerAccountId, x.Key, x.Sum(y => y.BaseQuantityDelta), clock.UtcNow))
            .ToListAsync(cancellationToken);
    }
}
