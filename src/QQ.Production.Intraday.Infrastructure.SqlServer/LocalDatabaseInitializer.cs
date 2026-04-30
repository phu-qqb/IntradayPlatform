using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Infrastructure.SqlServer;

public sealed class LocalDatabaseInitializer(IntradayDbContext dbContext, IClock clock)
{
    public Task ApplyMigrationsAsync(CancellationToken cancellationToken)
        => dbContext.Database.MigrateAsync(cancellationToken);

    public async Task SeedReferenceDataAsync(CancellationToken cancellationToken)
    {
        var seeded = SeedData.Create(new DateTimeOffset(2026, 04, 29, 09, 00, 00, TimeSpan.Zero));
        await UpsertAsync(dbContext.Funds, seeded.Funds, x => x.Id, cancellationToken);
        await UpsertAsync(dbContext.BrokerAccounts, seeded.BrokerAccounts, x => x.Id, cancellationToken);
        await UpsertAsync(dbContext.Instruments, seeded.Instruments, x => x.Id, cancellationToken);
        await UpsertAsync(dbContext.Venues, seeded.Venues, x => x.Id, cancellationToken);
        await UpsertAsync(dbContext.VenueInstrumentMappings, seeded.VenueInstrumentMappings, x => x.Id, cancellationToken);
        await UpsertAsync(dbContext.NavSnapshots, seeded.NavSnapshots, x => new { x.FundId, x.AsOfUtc }, cancellationToken);
        await UpsertAsync(dbContext.RiskLimitSets, seeded.RiskLimitSets, x => x.Id, cancellationToken);
        await UpsertAsync(dbContext.InstrumentRiskLimits, seeded.InstrumentRiskLimits, x => x.Id, cancellationToken);
        await UpsertAsync(dbContext.VenueRiskLimits, seeded.VenueRiskLimits, x => x.Id, cancellationToken);
        await UpsertAsync(dbContext.TradingWindows, seeded.TradingWindows, x => x.Id, cancellationToken);

        if (!await dbContext.KillSwitchStates.AnyAsync(cancellationToken))
        {
            dbContext.KillSwitchStates.Add(seeded.KillSwitch);
        }

        if (!await dbContext.PositionLedgerEvents.AnyAsync(x => x.ReferenceId == "SOD", cancellationToken))
        {
            dbContext.PositionLedgerEvents.AddRange(seeded.PositionLedger);
        }

        if (!await dbContext.MarketDataSnapshots.AnyAsync(x => x.Source == "Seed", cancellationToken))
        {
            dbContext.MarketDataSnapshots.AddRange(seeded.MarketData);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SeedDemoDataAsync(CancellationToken cancellationToken)
    {
        var seeded = SeedData.Create(new DateTimeOffset(2026, 04, 29, 09, 15, 00, TimeSpan.Zero));
        var run = seeded.ModelRuns.Single();
        if (!await dbContext.ModelRuns.AnyAsync(x => x.Id == run.Id, cancellationToken))
        {
            dbContext.ModelRuns.Add(run);
            dbContext.TargetWeights.AddRange(seeded.TargetWeights);
        }

        var instrument = seeded.Instruments.Single();
        var venue = seeded.Venues.Single();
        var start = new DateTimeOffset(2026, 04, 29, 09, 15, 00, TimeSpan.Zero);
        for (var i = 0; i < 15; i++)
        {
            var sourceTimestamp = start.AddMinutes(i);
            if (await dbContext.MarketDataSnapshots.AnyAsync(x => x.InstrumentId == instrument.Id && x.VenueId == venue.Id && x.SourceTimestampUtc == sourceTimestamp && x.Source == "DemoSeed", cancellationToken))
            {
                continue;
            }

            dbContext.MarketDataSnapshots.Add(new MarketDataSnapshot(
                MarketDataSnapshotId.New(),
                instrument.Id,
                venue.Id,
                1.10000m + i * 0.00001m,
                1.10020m + i * 0.00001m,
                null,
                "DemoSeed",
                sourceTimestamp,
                clock.UtcNow)
            {
                SequenceNumber = i + 1,
                IsSynthetic = true,
                CreatedAtUtc = clock.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetLocalDatabaseAsync(bool seedDemoData, CancellationToken cancellationToken)
    {
        await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await ApplyMigrationsAsync(cancellationToken);
        await SeedReferenceDataAsync(cancellationToken);
        if (seedDemoData)
        {
            await SeedDemoDataAsync(cancellationToken);
        }
    }

    private static async Task UpsertAsync<TEntity, TKey>(DbSet<TEntity> dbSet, IEnumerable<TEntity> values, Func<TEntity, TKey> keySelector, CancellationToken cancellationToken)
        where TEntity : class
    {
        var existing = await dbSet.AsNoTracking().ToListAsync(cancellationToken);
        var keys = existing.Select(keySelector).ToHashSet();
        foreach (var value in values)
        {
            if (!keys.Contains(keySelector(value)))
            {
                dbSet.Add(value);
            }
        }
    }
}

public sealed class IntradayDbContextFactory : IDesignTimeDbContextFactory<IntradayDbContext>
{
    public IntradayDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IntradayDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=QQProductionIntraday;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
            .Options;
        return new IntradayDbContext(options);
    }
}
