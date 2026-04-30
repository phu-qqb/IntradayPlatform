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
        var seedFund = seeded.Funds.Single();
        var fund = await dbContext.Funds.FirstOrDefaultAsync(x => x.Name == seedFund.Name, cancellationToken);
        if (fund is null)
        {
            fund = seedFund;
            dbContext.Funds.Add(fund);
        }

        var seedAccount = seeded.BrokerAccounts.Single();
        if (!await dbContext.BrokerAccounts.AnyAsync(x => x.FundId == fund.Id && x.AccountCode == seedAccount.AccountCode, cancellationToken))
        {
            dbContext.BrokerAccounts.Add(seedAccount with { FundId = fund.Id });
        }

        var seedInstrument = seeded.Instruments.Single();
        var instrument = await dbContext.Instruments.FirstOrDefaultAsync(x => x.Symbol == seedInstrument.Symbol && x.AssetClass == seedInstrument.AssetClass, cancellationToken);
        if (instrument is null)
        {
            instrument = seedInstrument;
            dbContext.Instruments.Add(instrument);
        }

        var seedVenue = seeded.Venues.Single();
        var venue = await dbContext.Venues.FirstOrDefaultAsync(x => x.Name == seedVenue.Name, cancellationToken);
        if (venue is null)
        {
            venue = seedVenue;
            dbContext.Venues.Add(venue);
        }

        var seedMapping = seeded.VenueInstrumentMappings.Single();
        if (!await dbContext.VenueInstrumentMappings.AnyAsync(x => x.VenueId == venue.Id && x.InstrumentId == instrument.Id, cancellationToken)
            && !await dbContext.VenueInstrumentMappings.AnyAsync(x => x.VenueId == venue.Id && x.VenueSymbol == seedMapping.VenueSymbol, cancellationToken))
        {
            dbContext.VenueInstrumentMappings.Add(seedMapping with { VenueId = venue.Id, InstrumentId = instrument.Id });
        }

        if (!await dbContext.NavSnapshots.AnyAsync(x => x.FundId == fund.Id && x.Source == NavSource.Seed, cancellationToken))
        {
            dbContext.NavSnapshots.Add(seeded.NavSnapshots.Single() with { FundId = fund.Id });
        }

        var seedRiskLimitSet = seeded.RiskLimitSets.Single();
        var riskLimitSet = await dbContext.RiskLimitSets.FirstOrDefaultAsync(x => x.FundId == fund.Id, cancellationToken);
        if (riskLimitSet is null)
        {
            riskLimitSet = seedRiskLimitSet with { FundId = fund.Id };
            dbContext.RiskLimitSets.Add(riskLimitSet);
        }

        var seedInstrumentRiskLimit = seeded.InstrumentRiskLimits.Single();
        if (!await dbContext.InstrumentRiskLimits.AnyAsync(x => x.RiskLimitSetId == riskLimitSet.Id && x.InstrumentId == instrument.Id, cancellationToken))
        {
            dbContext.InstrumentRiskLimits.Add(seedInstrumentRiskLimit with { RiskLimitSetId = riskLimitSet.Id, InstrumentId = instrument.Id });
        }

        var seedVenueRiskLimit = seeded.VenueRiskLimits.Single();
        if (!await dbContext.VenueRiskLimits.AnyAsync(x => x.RiskLimitSetId == riskLimitSet.Id && x.VenueId == venue.Id, cancellationToken))
        {
            dbContext.VenueRiskLimits.Add(seedVenueRiskLimit with { RiskLimitSetId = riskLimitSet.Id, VenueId = venue.Id });
        }

        var seedWindow = seeded.TradingWindows.Single(x => x.ModelName == "Sample FX Intraday");
        if (!await dbContext.TradingWindows.AnyAsync(x => x.FundId == fund.Id && x.ModelName == seedWindow.ModelName && x.DayOfWeek == seedWindow.DayOfWeek, cancellationToken))
        {
            dbContext.TradingWindows.Add(seedWindow with { FundId = fund.Id });
        }

        var currentWindow = CreateCurrentIntradayWindow(fund.Id);
        if (!await dbContext.TradingWindows.AnyAsync(x => x.FundId == currentWindow.FundId && x.ModelName == currentWindow.ModelName && x.DayOfWeek == currentWindow.DayOfWeek, cancellationToken))
        {
            dbContext.TradingWindows.Add(currentWindow);
        }

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

    private TradingWindow CreateCurrentIntradayWindow(FundId fundId)
    {
        var day = clock.UtcNow.DayOfWeek;
        var id = Guid.Parse($"88888888-8888-8888-8888-88888888888{(int)day:X}");
        return new TradingWindow(id, fundId, "IntradayFxModel", "UTC", day, TimeOnly.MinValue, new TimeOnly(23, 59, 59), new TimeOnly(23, 59, 59), null);
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
