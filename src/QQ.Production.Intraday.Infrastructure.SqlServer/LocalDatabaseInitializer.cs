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

        foreach (var seedInstrument in seeded.Instruments)
        {
            await UpsertInstrumentAsync(seedInstrument, cancellationToken);
        }

        var seedVenue = seeded.Venues.Single();
        var venue = await dbContext.Venues.FirstOrDefaultAsync(x => x.Name == seedVenue.Name, cancellationToken);
        if (venue is null)
        {
            venue = seedVenue;
            dbContext.Venues.Add(venue);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var seedMapping in seeded.VenueInstrumentMappings)
        {
            var mappingInstrument = await GetInstrumentByIdAsync(seedMapping.InstrumentId, cancellationToken);
            await UpsertVenueInstrumentMappingAsync(seedMapping with { VenueId = venue.Id, InstrumentId = mappingInstrument.Id }, cancellationToken);
        }

        foreach (var seedAlias in seeded.InstrumentAliases)
        {
            await UpsertInstrumentAliasAsync(seedAlias, cancellationToken);
        }

        await SeedLmaxReportInstrumentUniverseAsync(venue, cancellationToken);

        if (!await dbContext.NavSnapshots.AnyAsync(x => x.FundId == fund.Id && x.Source == NavSource.Seed, cancellationToken))
        {
            dbContext.NavSnapshots.Add(seeded.NavSnapshots.Single() with { FundId = fund.Id });
        }

        var seedRiskLimitSet = seeded.RiskLimitSets.Single();
        var riskLimitSet = await dbContext.RiskLimitSets.FirstOrDefaultAsync(x => x.FundId == fund.Id && x.ModelName == seedRiskLimitSet.ModelName && x.IsActive, cancellationToken);
        if (riskLimitSet is null)
        {
            riskLimitSet = seedRiskLimitSet with { FundId = fund.Id };
            dbContext.RiskLimitSets.Add(riskLimitSet);
        }

        foreach (var seedRiskLimit in seeded.RiskLimits)
        {
            if (!await dbContext.RiskLimits.AnyAsync(x => x.RiskLimitSetId == riskLimitSet.Id && x.Name == seedRiskLimit.Name, cancellationToken))
            {
                dbContext.RiskLimits.Add(seedRiskLimit with { RiskLimitSetId = riskLimitSet.Id });
            }
        }

        var seedInstrumentRiskLimit = seeded.InstrumentRiskLimits.Single();
        var instrument = await dbContext.Instruments.SingleAsync(x => x.Symbol == "EURUSD" && x.AssetClass == AssetClass.FxSpot, cancellationToken);
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

        foreach (var seedOperator in seeded.OperatorUsers)
        {
            var roles = seeded.OperatorUserRoles.Where(x => x.OperatorUserId == seedOperator.Id).ToList();
            await UpsertOperatorAsync(seedOperator, roles, cancellationToken);
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

    private async Task SeedLmaxReportInstrumentUniverseAsync(Venue venue, CancellationToken cancellationToken)
    {
        var now = new DateTimeOffset(2026, 04, 29, 09, 00, 00, TimeSpan.Zero);
        var instruments = new[]
        {
            new ReportInstrumentSeed("AUDUSD", "AUD/USD", "4007", "AUD", "USD"),
            new ReportInstrumentSeed("EURUSD", "EUR/USD", "4001", "EUR", "USD"),
            new ReportInstrumentSeed("GBPUSD", "GBP/USD", "4002", "GBP", "USD"),
            new ReportInstrumentSeed("NZDUSD", "NZD/USD", "100613", "NZD", "USD"),
            new ReportInstrumentSeed("USDCAD", "USD/CAD", "4013", "USD", "CAD"),
            new ReportInstrumentSeed("USDCHF", "USD/CHF", "4010", "USD", "CHF"),
            new ReportInstrumentSeed("USDJPY", "USD/JPY", "4004", "USD", "JPY")
        };

        foreach (var seed in instruments)
        {
            var instrument = await FindInstrumentAsync(seed.InternalSymbol, AssetClass.FxSpot, cancellationToken);

            if (instrument is null)
            {
                instrument = new Instrument(
                    StableInstrumentId(seed.ExternalInstrumentId),
                    seed.InternalSymbol,
                    AssetClass.FxSpot,
                    new Currency(seed.BaseCurrency),
                    new Currency(seed.QuoteCurrency),
                    seed.QuoteCurrency == "JPY" ? 3 : 5,
                    2,
                    IsEnabled: seed.InternalSymbol == "EURUSD",
                    IsTradingEnabled: seed.InternalSymbol == "EURUSD",
                    IsReportImportEnabled: true,
                    IsMarketDataEnabled: seed.InternalSymbol == "EURUSD");
                dbContext.Instruments.Add(instrument);
            }

            await UpsertVenueInstrumentMappingAsync(new VenueInstrumentMapping(
                StableVenueInstrumentId(seed.ExternalInstrumentId),
                venue.Id,
                instrument.Id,
                seed.InternalSymbol,
                seed.ExternalSymbol,
                10000m,
                0.1m,
                0.1m,
                seed.QuoteCurrency == "JPY" ? 0.001m : 0.00001m,
                IsEnabled: seed.InternalSymbol == "EURUSD"), cancellationToken);

            await UpsertInstrumentAliasAsync(new InstrumentAlias(
                StableInstrumentAliasId(seed.ExternalInstrumentId),
                instrument.Id,
                "LMAX_REPORT",
                seed.ExternalSymbol,
                seed.ExternalInstrumentId,
                true,
                now), cancellationToken);
        }
    }

    private async Task<Instrument> UpsertInstrumentAsync(Instrument seedInstrument, CancellationToken cancellationToken)
    {
        var existing = await FindInstrumentAsync(seedInstrument.Symbol, seedInstrument.AssetClass, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        dbContext.Instruments.Add(seedInstrument);
        return seedInstrument;
    }

    private async Task<Instrument?> FindInstrumentAsync(string symbol, AssetClass assetClass, CancellationToken cancellationToken)
    {
        var local = dbContext.Instruments.Local.FirstOrDefault(x => x.Symbol == symbol && x.AssetClass == assetClass);
        return local ?? await dbContext.Instruments.FirstOrDefaultAsync(x => x.Symbol == symbol && x.AssetClass == assetClass, cancellationToken);
    }

    private async Task<Instrument> GetInstrumentByIdAsync(InstrumentId instrumentId, CancellationToken cancellationToken)
    {
        var local = dbContext.Instruments.Local.FirstOrDefault(x => x.Id == instrumentId);
        return local ?? await dbContext.Instruments.SingleAsync(x => x.Id == instrumentId, cancellationToken);
    }

    private async Task UpsertVenueInstrumentMappingAsync(VenueInstrumentMapping candidate, CancellationToken cancellationToken)
    {
        var byInstrument = await FindVenueInstrumentMappingByInstrumentAsync(candidate.VenueId, candidate.InstrumentId, cancellationToken);
        var bySymbol = await FindVenueInstrumentMappingBySymbolAsync(candidate.VenueId, candidate.VenueSymbol, cancellationToken);
        var existing = ResolveReferenceRow(byInstrument, bySymbol, "venue instrument mapping", candidate.VenueSymbol);

        if (existing is null)
        {
            dbContext.VenueInstrumentMappings.Add(candidate);
        }
    }

    private async Task<VenueInstrumentMapping?> FindVenueInstrumentMappingByInstrumentAsync(VenueId venueId, InstrumentId instrumentId, CancellationToken cancellationToken)
    {
        var local = dbContext.VenueInstrumentMappings.Local.FirstOrDefault(x => x.VenueId == venueId && x.InstrumentId == instrumentId);
        return local ?? await dbContext.VenueInstrumentMappings.FirstOrDefaultAsync(x => x.VenueId == venueId && x.InstrumentId == instrumentId, cancellationToken);
    }

    private async Task<VenueInstrumentMapping?> FindVenueInstrumentMappingBySymbolAsync(VenueId venueId, string venueSymbol, CancellationToken cancellationToken)
    {
        var local = dbContext.VenueInstrumentMappings.Local.FirstOrDefault(x => x.VenueId == venueId && x.VenueSymbol == venueSymbol);
        return local ?? await dbContext.VenueInstrumentMappings.FirstOrDefaultAsync(x => x.VenueId == venueId && x.VenueSymbol == venueSymbol, cancellationToken);
    }

    private async Task UpsertInstrumentAliasAsync(InstrumentAlias candidate, CancellationToken cancellationToken)
    {
        var bySymbol = await FindInstrumentAliasBySymbolAsync(candidate.Source, candidate.ExternalSymbol, cancellationToken);
        var byInstrumentId = string.IsNullOrWhiteSpace(candidate.ExternalInstrumentId)
            ? null
            : await FindInstrumentAliasByExternalInstrumentIdAsync(candidate.Source, candidate.ExternalInstrumentId, cancellationToken);
        var existing = ResolveReferenceRow(bySymbol, byInstrumentId, "instrument alias", $"{candidate.Source}:{candidate.ExternalSymbol}/{candidate.ExternalInstrumentId}");

        if (existing is null)
        {
            dbContext.InstrumentAliases.Add(candidate);
        }
    }

    private async Task UpsertOperatorAsync(OperatorUser candidate, IReadOnlyList<OperatorUserRole> roles, CancellationToken cancellationToken)
    {
        var existing = dbContext.OperatorUsers.Local.FirstOrDefault(x => x.OperatorId == candidate.OperatorId)
            ?? await dbContext.OperatorUsers.FirstOrDefaultAsync(x => x.OperatorId == candidate.OperatorId, cancellationToken);
        if (existing is null)
        {
            dbContext.OperatorUsers.Add(candidate);
        }
        else
        {
            dbContext.Entry(existing).CurrentValues.SetValues(candidate);
        }

        var roleUserId = existing?.Id ?? candidate.Id;
        var existingRoles = dbContext.OperatorUserRoles.Local.Where(x => x.OperatorUserId == roleUserId).ToList();
        existingRoles.AddRange(await dbContext.OperatorUserRoles.Where(x => x.OperatorUserId == roleUserId).ToListAsync(cancellationToken));
        foreach (var existingRole in existingRoles.DistinctBy(x => x.Id))
        {
            dbContext.OperatorUserRoles.Remove(existingRole);
        }

        dbContext.OperatorUserRoles.AddRange(roles.Select(x => x with { OperatorUserId = roleUserId }));
    }

    private async Task<InstrumentAlias?> FindInstrumentAliasBySymbolAsync(string source, string externalSymbol, CancellationToken cancellationToken)
    {
        var local = dbContext.InstrumentAliases.Local.FirstOrDefault(x => x.Source == source && x.ExternalSymbol == externalSymbol);
        return local ?? await dbContext.InstrumentAliases.FirstOrDefaultAsync(x => x.Source == source && x.ExternalSymbol == externalSymbol, cancellationToken);
    }

    private async Task<InstrumentAlias?> FindInstrumentAliasByExternalInstrumentIdAsync(string source, string externalInstrumentId, CancellationToken cancellationToken)
    {
        var local = dbContext.InstrumentAliases.Local.FirstOrDefault(x => x.Source == source && x.ExternalInstrumentId == externalInstrumentId);
        return local ?? await dbContext.InstrumentAliases.FirstOrDefaultAsync(x => x.Source == source && x.ExternalInstrumentId == externalInstrumentId, cancellationToken);
    }

    private static T? ResolveReferenceRow<T>(T? first, T? second, string referenceType, string key)
        where T : class
    {
        if (first is not null && second is not null && !ReferenceEquals(first, second) && !Equals(first, second))
        {
            throw new DomainRuleViolationException($"Ambiguous seeded {referenceType} for {key}.");
        }

        return first ?? second;
    }

    private static InstrumentId StableInstrumentId(string externalInstrumentId)
        => new(Guid.Parse($"11111111-1111-1111-1111-{int.Parse(externalInstrumentId):D12}"));

    private static VenueInstrumentId StableVenueInstrumentId(string externalInstrumentId)
        => new(Guid.Parse($"22222222-2222-2222-2222-{int.Parse(externalInstrumentId):D12}"));

    private static InstrumentAliasId StableInstrumentAliasId(string externalInstrumentId)
        => new(Guid.Parse($"33333333-3333-3333-3333-{int.Parse(externalInstrumentId):D12}"));

    private sealed record ReportInstrumentSeed(
        string InternalSymbol,
        string ExternalSymbol,
        string ExternalInstrumentId,
        string BaseCurrency,
        string QuoteCurrency);
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
