using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.SqlServer;

/// <summary>Exact Demo reference extension. Reuses the existing conservative USD
/// instrument ceilings for the owner-requested native legs; global/venue ceilings stay fixed.</summary>
internal static class ConfigureUsdReference
{
    internal static async Task Run(string sourceCsv, string receiptPath, bool commit)
    {
        Require(Path.GetFullPath(sourceCsv).StartsWith(@"D:\data\lmax-demo-reference\", StringComparison.OrdinalIgnoreCase)
            && LmaxDemoRecoveredSendRetirement.HashFile(sourceCsv) == LmaxDemoUsdExecutionUniverse.SourceSha256, "OFFICIAL_REFERENCE_HASH_REQUIRED");
        Require(Path.GetFullPath(receiptPath).StartsWith(@"D:\data\lmax-demo-reference\", StringComparison.OrdinalIgnoreCase)
            && !File.Exists(receiptPath), "NEW_REFERENCE_RECEIPT_REQUIRED");
        using var owner = new FileStream(Path.Combine(LmaxDemoSessionOwnership.RealAccountRoot, "owner.lock"), FileMode.Open, FileAccess.Read, FileShare.None);
        using var daily = new DailyLease();
        await using var db = new IntradayDbContext(new DbContextOptionsBuilder<IntradayDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=QQProductionIntraday;Integrated Security=true;TrustServerCertificate=true;Application Name=QQ84NativeUsdReference").Options);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var repo = new SqlServerIntradayRepository(db);
        var state = await repo.LoadStateAsync(default);
        var account = state.BrokerAccounts.Single(x => x.IsEnabled);
        Require(account.AccountCode == "LMAX_DEMO_LOCAL" && account.ExternalAccountId == "1754288005", "EXACT_DEMO_ACCOUNT_REQUIRED");
        Require(state.ChildOrders.All(x => x.Status is OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Rejected or OrderStatus.Expired)
            && state.PositionLedger.Where(x => x.FundId == account.FundId).GroupBy(x => x.InstrumentId).All(g => g.Sum(x => x.BaseQuantityDelta) == 0m),
            "REFERENCE_CHANGE_REQUIRES_RECONCILED_INTERNAL_STATE");
        var venue = state.Venues.Single(x => x.Name == "LMAX" && x.IsEnabled);
        var before = JsonSerializer.Serialize(new { state.Instruments, state.VenueInstrumentMappings, state.InstrumentAliases });
        var riskBefore = JsonSerializer.Serialize(new { state.RiskLimitSets, state.InstrumentRiskLimits, state.VenueRiskLimits, state.TradingWindows });
        var fixedRiskBefore = JsonSerializer.Serialize(new { state.RiskLimitSets, state.VenueRiskLimits, state.TradingWindows });
        var policy = state.RiskLimitSets.Single(x => x.Id == Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")
            && x.FundId == account.FundId && x.ModelName == "IntradayFxModel" && x.IsActive && x.Status == RiskLimitSetStatus.Active);
        var eur = state.Instruments.Single(x => x.Symbol == "EURUSD");
        var template = state.InstrumentRiskLimits.Single(x => x.RiskLimitSetId == policy.Id && x.InstrumentId == eur.Id && x.IsEnabled);
        Require(policy.MaxGrossExposureUsd == 2_000_000m && template.MaxTradeNotionalUsd == 500_000m
            && template.MaxExposureUsd == 1_500_000m && template.MinTradeQuantity == 0m && template.MaxOrdersPerDay == 100
            && template.IsTradingEnabled, "EXISTING_CONSERVATIVE_DEMO_POLICY_CHANGED");
        var changes = new List<object>();
        var now = DateTimeOffset.UtcNow;
        foreach (var leg in LmaxDemoUsdExecutionUniverse.Legs)
        {
            var oldInstrument = state.Instruments.SingleOrDefault(x => x.Symbol == leg.Symbol);
            if (oldInstrument is not null)
                Require(oldInstrument.AssetClass == AssetClass.FxSpot && oldInstrument.BaseCurrency.Code == leg.Symbol[..3]
                    && oldInstrument.QuoteCurrency.Code == leg.Symbol[3..], "CANONICAL_INSTRUMENT_CONFLICT");
            var instrument = oldInstrument is null
                ? new Instrument(InstrumentId.New(), leg.Symbol, AssetClass.FxSpot, new(leg.Symbol[..3]), new(leg.Symbol[3..]),
                    leg.TickSize == .001m ? 3 : 5, 1)
                : oldInstrument with { IsEnabled = true, IsTradingEnabled = true, IsReportImportEnabled = true, IsMarketDataEnabled = true };
            if (oldInstrument is null) { db.Instruments.Add(instrument); changes.Add(new { kind="InstrumentAdded", symbol=leg.Symbol, instrument.Id }); }
            else if (oldInstrument != instrument) { db.Attach(instrument); db.Entry(instrument).State = EntityState.Modified; changes.Add(new { kind="InstrumentEnabled", symbol=leg.Symbol, instrument.Id }); }
            var oldMapping = state.VenueInstrumentMappings.SingleOrDefault(x => x.InstrumentId == instrument.Id && x.VenueId == venue.Id);
            if (oldMapping is not null)
                Require(oldMapping.VenueSymbol == leg.Symbol && oldMapping.VenueInstrumentCode == leg.SlashSymbol && oldMapping.ContractSize == leg.ContractSize
                    && oldMapping.MinOrderQuantity == leg.MinQuantity && oldMapping.QuantityStep == leg.QuantityStep && oldMapping.PriceTickSize == leg.TickSize,
                    "EXISTING_EXECUTION_CONTRACT_CONFLICT");
            var mapping = oldMapping is null
                ? new VenueInstrumentMapping(VenueInstrumentId.New(), venue.Id, instrument.Id, leg.Symbol, leg.SlashSymbol, leg.ContractSize, leg.MinQuantity, leg.QuantityStep, leg.TickSize)
                : oldMapping with { IsEnabled = true };
            if (oldMapping is null) { db.VenueInstrumentMappings.Add(mapping); changes.Add(new { kind="MappingAdded", symbol=leg.Symbol, mapping.Id }); }
            else if (mapping != oldMapping) { db.Attach(mapping); db.Entry(mapping).State = EntityState.Modified; changes.Add(new { kind="MappingEnabled", symbol=leg.Symbol, mapping.Id }); }
            var oldAlias = state.InstrumentAliases.SingleOrDefault(x => x.Source == "LMAX_REPORT" && x.InstrumentId == instrument.Id);
            Require(!state.InstrumentAliases.Any(x => x.Source == "LMAX_REPORT" && x.InstrumentId != instrument.Id
                && (x.ExternalInstrumentId == leg.SecurityId || x.ExternalSymbol == leg.SlashSymbol)), "REPORT_IDENTITY_COLLISION");
            if (oldAlias is not null) Require(oldAlias.ExternalInstrumentId == leg.SecurityId && oldAlias.ExternalSymbol == leg.SlashSymbol, "REPORT_ALIAS_CONFLICT");
            var alias = oldAlias is null
                ? new InstrumentAlias(InstrumentAliasId.New(), instrument.Id, "LMAX_REPORT", leg.SlashSymbol, leg.SecurityId, true, now)
                : oldAlias with { IsEnabled = true };
            if (oldAlias is null) { db.InstrumentAliases.Add(alias); changes.Add(new { kind="ReportAliasAdded", symbol=leg.Symbol, alias.Id }); }
            else if (alias != oldAlias) { db.Attach(alias); db.Entry(alias).State = EntityState.Modified; changes.Add(new { kind="ReportAliasEnabled", symbol=leg.Symbol, alias.Id }); }
            var existingLimit = state.InstrumentRiskLimits.SingleOrDefault(x => x.RiskLimitSetId == policy.Id && x.InstrumentId == instrument.Id);
            if (existingLimit is null)
            {
                var inherited = template with { Id = Guid.NewGuid(), InstrumentId = instrument.Id };
                db.InstrumentRiskLimits.Add(inherited);
                changes.Add(new { kind="ExistingInstrumentPolicyBound", symbol=leg.Symbol, inherited.Id, sourceRuleId=template.Id,
                    inherited.MaxTradeNotionalUsd, inherited.MaxExposureUsd, inherited.MinTradeQuantity, inherited.MaxOrdersPerDay });
            }
            else Require(existingLimit.IsEnabled && existingLimit.IsTradingEnabled && existingLimit.MaxTradeNotionalUsd == template.MaxTradeNotionalUsd
                && existingLimit.MaxExposureUsd == template.MaxExposureUsd && existingLimit.MinTradeQuantity == template.MinTradeQuantity
                && existingLimit.MaxOrdersPerDay == template.MaxOrdersPerDay, "EXISTING_INSTRUMENT_RISK_CONFLICT");
        }
        await db.SaveChangesAsync();
        var after = await repo.LoadStateAsync(default);
        var issues = LmaxDemoUsdExecutionUniverse.ConfigurationIssues(after, now);
        Require(issues.Count == 0, "REFERENCE_READBACK_FAILED");
        Require(fixedRiskBefore == JsonSerializer.Serialize(new { after.RiskLimitSets, after.VenueRiskLimits, after.TradingWindows })
            && state.InstrumentRiskLimits.All(old => after.InstrumentRiskLimits.Single(x=>x.Id==old.Id) == old), "EXISTING_RISK_CONFIGURATION_MUST_REMAIN_UNCHANGED");
        var auditId = OperatorAuditEventId.New();
        if (changes.Count > 0)
        {
            db.OperatorAuditEvents.Add(new(auditId, now, OperatorAuditActorType.Operator, "philippe-authorized-demo-reference", "Authorized full FX Demo reference",
                OperatorAuditEventType.InstrumentControlUpdated, OperatorAuditSeverity.Info, OperatorAuditResult.Succeeded,
                "LmaxDemoUsdExecutionUniverse", "1754288005", auditId.Value.ToString("D"), null, null, "LMAX_DEMO_NATIVE_USD_REFERENCE_V1",
                "Owner-requested full native USD scope: official contracts/report identities and existing conservative USD risk-rule bindings. No ceiling increased.",
                "https://github.com/phu-qqb/IntradayPlatform/issues/84#issuecomment-5729092266", JsonSerializer.Serialize(new { reference=before, risk=riskBefore }),
                JsonSerializer.Serialize(new { after.Instruments, after.VenueInstrumentMappings, after.InstrumentAliases, after.InstrumentRiskLimits }),
                JsonSerializer.Serialize(new { source=LmaxDemoUsdExecutionUniverse.SourceUrl, sha256=LmaxDemoUsdExecutionUniverse.SourceSha256,
                    quantityStepPolicy="QQ_ORDER_QUANTUM_ONE_PUBLISHED_MINIMUM_LOT", changes, remainingRiskIssues=issues, brokerCalls=0 })));
            await db.SaveChangesAsync();
        }
        if (commit) await tx.CommitAsync(); else await tx.RollbackAsync();
        var receipt = new { marker=commit?"NATIVE_USD_REFERENCE_APPLIED":"NATIVE_USD_REFERENCE_VERIFIED_ROLLED_BACK", atUtc=now,
            account="1754288005", sourceSha256=LmaxDemoUsdExecutionUniverse.SourceSha256, legs=LmaxDemoUsdExecutionUniverse.Legs.Count,
            changes, auditId=changes.Count>0?auditId.Value:(Guid?)null, existingRiskCeilingsChanged=false, sourceRiskRuleId=template.Id, remainingIssues=issues,
            brokerCalls=0, retirementPerformed=false, tradingStarted=false };
        using var output = new FileStream(receiptPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(output, receipt, new JsonSerializerOptions { WriteIndented=true }); output.Flush(true);
        Console.WriteLine(JsonSerializer.Serialize(receipt));
    }
    private static void Require(bool ok, string code) { if (!ok) throw new InvalidOperationException(code); }
}
