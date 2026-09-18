using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.SqlServer;
using Retirement=QQ.Production.Intraday.Application.LmaxDemoRiskRejectedRetirement;

internal static class RetireRiskRejectedSession
{
    internal static async Task Run(string declarationPath,string declarationHash)
    {
        if(!Path.GetFullPath(declarationPath).StartsWith(@"D:\data\lmax-demo-owner-observations\",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("OWNER_DECLARATION_PATH_REQUIRED");
        using var owner=new FileStream(Path.Combine(LmaxDemoSessionOwnership.RealAccountRoot,"owner.lock"),FileMode.Open,FileAccess.Read,FileShare.None);
        using var daily=new DailyLease();
        var path=Path.Combine(LmaxDemoSessionOwnership.RealAccountRoot,Retirement.SessionId+".journal.jsonl");
        if(LmaxDemoRecoveredSendRetirement.HashFile(path)!=Retirement.JournalHash)throw new InvalidOperationException("STOPPED_JOURNAL_CHANGED");
        await using var db=new IntradayDbContext(new DbContextOptionsBuilder<IntradayDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=QQProductionIntraday;Integrated Security=true;TrustServerCertificate=true;Application Name=QQ84RiskRejectedRetirement").Options);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var s=await new SqlServerIntradayRepository(db).LoadStateAsync(default);
        var account=s.BrokerAccounts.Single(x=>x.IsEnabled);
        if(account.AccountCode!="LMAX_DEMO_LOCAL" || account.ExternalAccountId!="1754288005")throw new InvalidOperationException("EXACT_DEMO_ACCOUNT_REQUIRED");
        var id=new ModelRunId(Guid.Parse(Retirement.FailedModelRunId));
        var run=s.ModelRuns.Single(x=>x.Id==id && x.FundId==account.FundId && x.ModelName=="IntradayFxModel");
        var intent=s.TradeIntents.Single(x=>x.ModelRunId==id);
        if(s.Instruments.Single(x=>x.Id==intent.InstrumentId).Symbol!="GBPUSD" || intent.RequestedBaseQuantity!=616000m
            || s.RiskDecisions.Single(x=>x.TradeIntentId==intent.Id).RejectReason!=RiskRejectReason.MaxTradeNotionalExceeded
            || s.ParentOrders.Any(x=>x.TradeIntentId==intent.Id) || LmaxDemoGmvRiskProfile.ConfigurationIssues(s,DateTimeOffset.UtcNow).Count!=0)
            throw new InvalidOperationException("EXACT_RISK_REJECTION_AND_APPROVED_POLICY_REQUIRED");
        var start=DateTimeOffset.Parse("2026-09-18T13:16:15.1855364Z");
        var evidence=new LmaxDemoRetirementDatabaseEvidence(DateTimeOffset.UtcNow,account.AccountCode,Retirement.FailedModelRunId,
            run.IsProcessed,s.TargetWeights.Count(x=>x.ModelRunId==id),s.TargetPositions.Count(x=>x.ModelRunId==id),
            s.TradeIntents.Count(x=>x.ModelRunId==id),s.RiskDecisions.Count(x=>x.ModelRunId==id),
            s.ParentOrders.Count(x=>x.CreatedAtUtc>=start),s.ChildOrders.Count(x=>x.CreatedAtUtc>=start),
            s.ExecutionReports.Count(x=>x.ReceivedAtUtc>=start),s.Fills.Count(x=>x.ReceivedAtUtc>=start),
            s.ChildOrders.Count(x=>x.Status is not(OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Rejected or OrderStatus.Expired)),
            s.PositionLedger.Where(x=>x.FundId==account.FundId).GroupBy(x=>x.InstrumentId).Count(g=>g.Sum(x=>x.BaseQuantityDelta)!=0m));
        var c=Retirement.Prepare(path,declarationPath,declarationHash,evidence,DateTimeOffset.UtcNow);
        Retirement.WriteUnderOwnerLease(path,c,DateTimeOffset.UtcNow);
        using var journal=LmaxDemoSessionJournal.OpenForInspection(path);
        if(!Retirement.IsValidated(path,journal,DateTimeOffset.UtcNow))throw new InvalidOperationException("RETIREMENT_READBACK_FAILED");
        await tx.RollbackAsync();
        Console.WriteLine(JsonSerializer.Serialize(new {marker="EXACT_RISK_REJECTED_SESSION_RETIRED",retiredAtUtc=c.Evidence.RetiredAtUtc,
            certificatePath=Retirement.CertificatePath(path),certificateSha256=LmaxDemoRecoveredSendRetirement.HashFile(Retirement.CertificatePath(path)),
            journalSha256=Retirement.JournalHash,journalUnchanged=true,evidence,databaseWrites=0,brokerCalls=0,tradingStarted=false}));
    }
}
