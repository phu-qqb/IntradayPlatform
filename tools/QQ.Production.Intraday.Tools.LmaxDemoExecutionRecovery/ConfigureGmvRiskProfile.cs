using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.SqlServer;

/// <summary>One authorized configuration migration while the exact no-send
/// session is irreversibly faulted. It cannot retire/restart a session, clear a
/// fault, write economic rows, acquire/take over owner.lock or call a broker.</summary>
internal static class ConfigureGmvRiskProfile
{
    private const string SessionId = "lmax-demo-20260918-full-usd-130841";
    private const string StartHash = "32DD0765C6204C60C57B1C023527F4CD8B169165B82216CDB3D997DF1C2E62D1";
    private const string FaultHash = "A39F328E598AE0FB175B3B586C3DD254AE6FF37A016BF9237DD64822B1FBA4BD";
    private static readonly ModelRunId FailedModel = new(Guid.Parse("4b375531-8d81-4560-ab55-eac6a172b321"));
    private static readonly Guid OldPolicy = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
    private static readonly DateTimeOffset SessionStart = DateTimeOffset.Parse("2026-09-18T13:16:15.1855364Z");

    private static object InspectQuarantine()
    {
        var path = Path.Combine(LmaxDemoSessionOwnership.RealAccountRoot, SessionId + ".journal.jsonl");
        using var journal = LmaxDemoSessionJournal.OpenForInspection(path);
        var session = LmaxDemoControlledSession.Inspect(journal);
        var entries = journal.Entries;
        Require(session.Start.SessionId == SessionId && session.Start.AccountId == "1754288005"
            && !session.Start.Simulated && !session.IsClosed && session.BlockingReason == "COORDINATOR_RECONCILIATION_REQUIRED"
            && entries[0].Sha256 == StartHash && entries.Count(x=>x.Kind=="Fault") == 1
            && entries.Single(x=>x.Kind=="Fault").Sha256 == FaultHash
            && entries.All(x=>x.Kind is "Start" or "Inbound" or "Fault")
            && session.KnownOrders.Count == 0 && session.FillDerivedPositions.Values.All(x=>x==0m), "EXACT_NO_SEND_QUARANTINE_REQUIRED");
        Require(!File.Exists(Path.Combine(LmaxDemoSessionOwnership.RealAccountRoot, SessionId, "final-observation.json")),
            "CONCURRENT_SESSION_CLOSURE_NOT_ALLOWED");
        return new { sessionId=SessionId, startHash=StartHash, faultHash=FaultHash, lastIndex=entries[^1].Index,
            lastHash=entries[^1].Sha256, brokerSends=0, faultRemains=true };
    }

    internal static async Task Run(string receiptPath, bool commit)
    {
        var full = Path.GetFullPath(receiptPath);
        Require(full.StartsWith(@"D:\data\lmax-demo-reference\",StringComparison.OrdinalIgnoreCase)
            && !File.Exists(full),"NEW_GMV_RECEIPT_REQUIRED");
        using var daily = new DailyLease();
        var quarantineBefore = InspectQuarantine();
        await using var db = new IntradayDbContext(new DbContextOptionsBuilder<IntradayDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=QQProductionIntraday;Integrated Security=true;TrustServerCertificate=true;Application Name=QQ84ApprovedGmvPolicy").Options);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var repo = new SqlServerIntradayRepository(db);
        var state = await repo.LoadStateAsync(default);
        var account = state.BrokerAccounts.Single(x=>x.IsEnabled);
        Require(account.AccountCode=="LMAX_DEMO_LOCAL" && account.ExternalAccountId=="1754288005", "EXACT_DEMO_ACCOUNT_REQUIRED");
        Require(state.ChildOrders.All(x=>x.Status is OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Rejected or OrderStatus.Expired)
            && !state.ChildOrders.Any(x=>x.CreatedAtUtc>=SessionStart) && !state.Fills.Any(x=>x.ReceivedAtUtc>=SessionStart)
            && state.PositionLedger.Where(x=>x.FundId==account.FundId).GroupBy(x=>x.InstrumentId).All(g=>g.Sum(x=>x.BaseQuantityDelta)==0m),
            "GMV_CHANGE_REQUIRES_RECONCILED_INTERNAL_NO_SEND_STATE");
        var intent = state.TradeIntents.Single(x=>x.ModelRunId==FailedModel);
        Require(!state.ParentOrders.Any(x=>x.TradeIntentId==intent.Id)
            && state.RiskDecisions.Single(x=>x.TradeIntentId==intent.Id).RejectReason==RiskRejectReason.MaxTradeNotionalExceeded,
            "EXACT_PRE_SEND_RISK_REJECTION_REQUIRED");
        var before = Config(state);
        var economicsBefore = Economics(state);
        var now = DateTimeOffset.UtcNow;
        var existing = state.RiskLimitSets.SingleOrDefault(x=>x.Id==LmaxDemoGmvRiskProfile.PolicyId);
        var alreadyApplied = existing is not null;
        OperatorAuditEventId? auditId = null;
        if (!alreadyApplied)
        {
            var old = state.RiskLimitSets.Single(x=>x.Id==OldPolicy && x.FundId==account.FundId
                && x.ModelName=="IntradayFxModel" && x.IsActive && x.Status==RiskLimitSetStatus.Active);
            Require(old.Version==1 && old.MaxGrossExposureUsd==2_000_000m && old.GlobalTradingEnabled,
                "ORIGINAL_POLICY_CHANGED");
            var oldRules = state.InstrumentRiskLimits.Where(x=>x.RiskLimitSetId==old.Id && x.IsEnabled).ToArray();
            var legIds = LmaxDemoUsdExecutionUniverse.Symbols.Select(s=>state.Instruments.Single(x=>x.Symbol==s).Id).ToHashSet();
            Require(oldRules.Length==14 && oldRules.Select(x=>x.InstrumentId).ToHashSet().SetEquals(legIds)
                && oldRules.All(x=>x.MaxExposureUsd==1_500_000m && x.MaxTradeNotionalUsd==500_000m && x.IsTradingEnabled),
                "ORIGINAL_14_LEG_POLICY_CHANGED");
            var venue = state.VenueRiskLimits.Single(x=>x.RiskLimitSetId==old.Id && x.IsEnabled);
            Require(venue.MaxTradeNotionalUsd==500_000m && venue.IsVenueEnabled,"ORIGINAL_VENUE_POLICY_CHANGED");
            var windows = state.TradingWindows.Where(x=>x.FundId==account.FundId && x.ModelName=="IntradayFxModel" && x.IsEnabled).ToArray();
            Require(windows.Length==3 && windows.Select(x=>x.DayOfWeek).ToHashSet().SetEquals(
                new[]{DayOfWeek.Monday,DayOfWeek.Wednesday,DayOfWeek.Thursday})
                && windows.All(x=>x.TradingEnabled && x.TimeZoneId=="UTC" && x.OpensAtUtc==TimeOnly.MinValue
                    && x.ClosesAtUtc==new TimeOnly(23,59,59) && x.NoNewOrdersAfterUtc==new TimeOnly(23,59,59)
                    && x.FlattenAtUtc is null),"ORIGINAL_CALENDAR_CHANGED");
            Modify(old with { IsActive=false,Status=RiskLimitSetStatus.Retired,EffectiveToUtc=now,RetiredAtUtc=now,RetiredBy="philippe-approved-gmv-20260918" });
            foreach(var window in windows) Modify(window with { IsEnabled=false,UpdatedAtUtc=now });
            // Release filtered unique active-policy/day indexes inside this same transaction.
            await db.SaveChangesAsync();
            db.RiskLimitSets.Add(old with { Id=LmaxDemoGmvRiskProfile.PolicyId,Name="LMAX Demo GMV 2M per position / 10M portfolio",Version=2,
                MaxGrossExposureUsd=LmaxDemoGmvRiskProfile.PortfolioGmvUsd,EffectiveFromUtc=now,EffectiveToUtc=null,
                CreatedAtUtc=now,CreatedBy="philippe-approved-gmv-20260918",ActivatedAtUtc=now,ActivatedBy="philippe-approved-gmv-20260918",
                RetiredAtUtc=null,RetiredBy=null,Description=LmaxDemoGmvRiskProfile.Approval });
            foreach(var rule in oldRules) db.InstrumentRiskLimits.Add(rule with { Id=Guid.NewGuid(),RiskLimitSetId=LmaxDemoGmvRiskProfile.PolicyId,
                MaxExposureUsd=LmaxDemoGmvRiskProfile.PositionGmvUsd,MaxTradeNotionalUsd=LmaxDemoGmvRiskProfile.MaximumReversalOrderUsd });
            db.VenueRiskLimits.Add(venue with { Id=Guid.NewGuid(),RiskLimitSetId=LmaxDemoGmvRiskProfile.PolicyId,
                MaxTradeNotionalUsd=LmaxDemoGmvRiskProfile.MaximumReversalOrderUsd });
            foreach(var rule in state.RiskLimits.Where(x=>x.RiskLimitSetId==old.Id))
                db.RiskLimits.Add(rule with { Id=Guid.NewGuid(),RiskLimitSetId=LmaxDemoGmvRiskProfile.PolicyId,Value=rule.Name switch {
                    "MaxTradeNotionalUsd"=>LmaxDemoGmvRiskProfile.MaximumReversalOrderUsd,
                    "MaxGrossExposureUsd"=>LmaxDemoGmvRiskProfile.PortfolioGmvUsd,_=>rule.Value } });
            foreach(var day in Enum.GetValues<DayOfWeek>()) db.TradingWindows.Add(windows[0] with { Id=Guid.NewGuid(),DayOfWeek=day,
                Version=2,CreatedAtUtc=now,UpdatedAtUtc=now });
            await db.SaveChangesAsync();
        }
        var after = await repo.LoadStateAsync(default);
        var issues = LmaxDemoUsdExecutionUniverse.ConfigurationIssues(after,now)
            .Concat(LmaxDemoGmvRiskProfile.ConfigurationIssues(after,now)).ToArray();
        Require(issues.Length==0,"GMV_POLICY_READBACK_FAILED:"+string.Join(",",issues));
        Require(Economics(after)==economicsBefore,"ECONOMIC_FACTS_MUST_REMAIN_UNCHANGED");
        if (!alreadyApplied)
        {
            auditId=OperatorAuditEventId.New();
            db.OperatorAuditEvents.Add(new(auditId.Value,now,OperatorAuditActorType.Operator,"philippe-approved-gmv-20260918",
                "Approved Demo GMV/calendar amendment",OperatorAuditEventType.RiskLimitSetActivated,OperatorAuditSeverity.Info,
                OperatorAuditResult.Succeeded,"RiskLimitSet",LmaxDemoGmvRiskProfile.PolicyId.ToString("D"),auditId.Value.Value.ToString("D"),
                null,null,"LMAX_DEMO_GMV_2M_10M_SEVEN_DAYS",LmaxDemoGmvRiskProfile.OwnerQuote,LmaxDemoGmvRiskProfile.Approval,
                before,Config(after),JsonSerializer.Serialize(new { quarantineBefore,programmeHoursChanged=false,
                    reversalOrderBoundUsd=LmaxDemoGmvRiskProfile.MaximumReversalOrderUsd,brokerCalls=0,sessionRetired=false,tradingStarted=false })));
            await db.SaveChangesAsync();
        }
        var quarantineAfter = InspectQuarantine();
        if(commit) await tx.CommitAsync(); else await tx.RollbackAsync();
        var receipt = new { marker=commit?"APPROVED_GMV_POLICY_APPLIED":"APPROVED_GMV_POLICY_VERIFIED_ROLLED_BACK",atUtc=now,
            account="1754288005",policyId=LmaxDemoGmvRiskProfile.PolicyId,version=2,alreadyApplied,auditId=auditId?.Value,
            positionGmvUsd=LmaxDemoGmvRiskProfile.PositionGmvUsd,portfolioGmvUsd=LmaxDemoGmvRiskProfile.PortfolioGmvUsd,
            maximumReversalOrderUsd=LmaxDemoGmvRiskProfile.MaximumReversalOrderUsd,authorizedDays=Enum.GetNames<DayOfWeek>(),
            instruments=LmaxDemoUsdExecutionUniverse.Symbols,approval=LmaxDemoGmvRiskProfile.Approval,remainingIssues=issues,
            quarantineBefore,quarantineAfter,originalRiskRowsRetained=true,economicFactsUnchanged=true,programmeHoursChanged=false,
            brokerCalls=0,sessionRetired=false,tradingStarted=false,requiresQualifiedWorkerForNettedGmvSemantics=true };
        using var output=new FileStream(full,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        JsonSerializer.Serialize(output,receipt,new JsonSerializerOptions{WriteIndented=true}); output.Flush(true);
        Console.WriteLine(JsonSerializer.Serialize(receipt));

        void Modify<T>(T row) where T:class { db.Attach(row); db.Entry(row).State=EntityState.Modified; }
    }

    private static string Config(PlatformState s)=>JsonSerializer.Serialize(new { s.RiskLimitSets,s.RiskLimits,s.InstrumentRiskLimits,s.VenueRiskLimits,s.TradingWindows });
    private static string Economics(PlatformState s)=>JsonSerializer.Serialize(new { s.ModelRuns,s.TargetWeights,s.TargetPositions,
        s.DriftSnapshots,s.TradeIntents,s.RiskDecisions,s.RiskDecisionDetails,s.ParentOrders,s.ChildOrders,s.ExecutionReports,s.Fills,
        s.PositionLedger,s.ReconciliationRuns,s.ReconciliationBreaks });
    private static void Require(bool ok,string code) { if(!ok) throw new InvalidOperationException(code); }
}
