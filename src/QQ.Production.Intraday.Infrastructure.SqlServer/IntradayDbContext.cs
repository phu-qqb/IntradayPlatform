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
        modelBuilder.Entity<MarketDataSnapshot>().HasKey(nameof(MarketDataSnapshot.InstrumentId), nameof(MarketDataSnapshot.VenueId), nameof(MarketDataSnapshot.ReceivedAtUtc));
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
        modelBuilder.Entity<Fill>().HasIndex(x => new { x.VenueId, x.BrokerExecutionId }).IsUnique();
        modelBuilder.Entity<ParentOrder>().HasIndex(x => x.ClientOrderId).IsUnique();
        modelBuilder.Entity<ChildOrder>().HasIndex(x => x.ClientOrderId).IsUnique();

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

        modelBuilder.Entity<Fund>().OwnsOne(x => x.BaseCurrency);
        modelBuilder.Entity<Instrument>().OwnsOne(x => x.BaseCurrency);
        modelBuilder.Entity<Instrument>().OwnsOne(x => x.QuoteCurrency);
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
        state.InstrumentRiskLimits.AddRange(await dbContext.InstrumentRiskLimits.AsNoTracking().ToListAsync(cancellationToken));
        state.VenueRiskLimits.AddRange(await dbContext.VenueRiskLimits.AsNoTracking().ToListAsync(cancellationToken));
        state.TradingWindows.AddRange(await dbContext.TradingWindows.AsNoTracking().ToListAsync(cancellationToken));
        state.KillSwitch = await dbContext.KillSwitchStates.AsNoTracking().OrderByDescending(x => x.UpdatedAtUtc).FirstAsync(cancellationToken);
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
