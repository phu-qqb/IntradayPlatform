using System.Text.Json;
using QQ.Production.Intraday.Application;
using R=QQ.Production.Intraday.Application.LmaxDemoRiskRejectedRetirement;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxDemoRiskRejectedRetirementTests
{
    [Fact]
    public void AuditedNoSendBridgeAllowsOneNewOwnerAndPreservesOriginalFault()
    {
        using var f=new Fixture();var original=File.ReadAllBytes(f.Path);
        Assert.Throws<InvalidOperationException>(()=>LmaxDemoSessionOwnership.Begin(f.Next,f.Now,f.Root));
        R.WriteUnderOwnerLease(f.Path,f.Prepare(),f.Now,true);
        Assert.Equal(original,File.ReadAllBytes(f.Path));
        using var j=LmaxDemoSessionJournal.OpenForInspection(f.Path);
        Assert.Equal("COORDINATOR_RECONCILIATION_REQUIRED",LmaxDemoControlledSession.Inspect(j).BlockingReason);
        Assert.True(R.IsValidated(f.Path,j,f.Now.AddDays(1),true));
        using var next=LmaxDemoSessionOwnership.Begin(f.Next,f.Now,f.Root);
        Assert.Throws<IOException>(()=>LmaxDemoSessionOwnership.Begin(f.Next with{SessionId="duplicate"},f.Now,f.Root));
    }
    [Theory]
    [InlineData("fill")][InlineData("parent")][InlineData("position")][InlineData("working")]
    [InlineData("processed")][InlineData("weights")][InlineData("targets")][InlineData("db-age")]
    public void AnyEconomicAmbiguityRejectsRetirement(string defect)
    {
        using var f=new Fixture();var d=defect switch {
            "fill"=>f.Database with{NewFills=1},"parent"=>f.Database with{NewParentOrders=1},
            "position"=>f.Database with{NonZeroPositions=1},"working"=>f.Database with{OpenChildOrders=1},
            "processed"=>f.Database with{FailedRunProcessed=true},"weights"=>f.Database with{FullPortfolioWeightCount=13},
            "targets"=>f.Database with{FailedRunTargets=2},_=>f.Database with{VerifiedAtUtc=f.Now.AddSeconds(-61)}};
        Assert.Throws<InvalidOperationException>(()=>f.Prepare(d));
        Assert.False(File.Exists(R.CertificatePath(f.Path)));
    }
    [Theory]
    [InlineData("stale")][InlineData("wrong-fault")][InlineData("cycle")][InlineData("send")]
    public void StaleDeclarationOrExecutionActivityCannotUseNoSendRetirement(string defect)
    {
        using var f=new Fixture(defect);Assert.ThrowsAny<Exception>(()=>f.Prepare());
    }
    [Fact]
    public void CertificateCannotBeOverwrittenAndChangedProofInvalidatesIt()
    {
        using var f=new Fixture();var c=f.Prepare();R.WriteUnderOwnerLease(f.Path,c,f.Now,true);
        Assert.Throws<IOException>(()=>R.WriteUnderOwnerLease(f.Path,c,f.Now,true));
        File.AppendAllText(f.Proof,"changed");using var j=LmaxDemoSessionJournal.OpenForInspection(f.Path);
        Assert.Throws<InvalidOperationException>(()=>R.IsValidated(f.Path,j,f.Now,true));
    }
    [Fact]
    public void SimulationCannotAuthorizeRealOwnership()
    {
        using var f=new Fixture();Assert.Throws<InvalidOperationException>(()=>R.Prepare(f.Path,f.Proof,f.Hash,f.Database,f.Now));
    }
    [Theory]
    [InlineData("valid")][InlineData("refresh")][InlineData("account")][InlineData("response")][InlineData("automated")]
    public void NewOwnerReplyKeepsItsExactTimestampAndProvenance(string defect)
    {
        var d=new Dictionary<string,object>{["schema"]="lmax_demo_owner_confirmation_v3",["source"]=LmaxDemoOwnerConfirmedOpening.Source,
            ["accountId"]="1754288005",["owner"]="Philippe",["ownerApprovalReference"]=LmaxDemoOwnerConfirmedOpening.ResumeApproval,
            ["approvalQuestion"]=LmaxDemoOwnerConfirmedOpening.ResumeQuestion,["approvalResponse"]="oui",
            ["recordedAtUtc"]=LmaxDemoOwnerConfirmedOpening.ResumeRecordedAt,["flat"]=true,["noWorkingOrders"]=true,
            ["exclusiveOrderActivityDeclared"]=true,["automatedBrokerObservation"]=false,["stopReceiptSha256"]=LmaxDemoOwnerConfirmedOpening.StopReceiptHash};
        if(defect=="refresh")d["recordedAtUtc"]=LmaxDemoOwnerConfirmedOpening.ResumeRecordedAt.AddSeconds(1);
        if(defect=="account")d["accountId"]="other";if(defect=="response")d["approvalResponse"]="non";if(defect=="automated")d["automatedBrokerObservation"]=true;
        using var j=JsonDocument.Parse(JsonSerializer.Serialize(d));
        if(defect=="valid")LmaxDemoOwnerConfirmedOpening.ValidateResumeDeclaration(j.RootElement,LmaxDemoOwnerConfirmedOpening.ResumeRecordedAt);
        else Assert.Throws<InvalidOperationException>(()=>LmaxDemoOwnerConfirmedOpening.ValidateResumeDeclaration(j.RootElement,LmaxDemoOwnerConfirmedOpening.ResumeRecordedAt));
    }
    private sealed class Fixture:IDisposable
    {
        public string Root{get;}=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"SIMULATED-risk-retirement-"+Guid.NewGuid().ToString("N"));
        public string Path=>System.IO.Path.Combine(Root,"SIMULATED-fault.journal.jsonl");
        public string Proof=>System.IO.Path.Combine(Root,"SIMULATED-owner.json");
        public string Hash=>LmaxDemoRecoveredSendRetirement.HashFile(Proof);
        public DateTimeOffset Now{get;}=DateTimeOffset.Parse("2026-09-18T15:10:00Z");
        public LmaxDemoSessionStart Next{get;}
        public LmaxDemoRetirementDatabaseEvidence Database{get;}
        public Fixture(string defect="")
        {
            Directory.CreateDirectory(Root);var at=Now.AddMinutes(-5);
            var start=new LmaxDemoSessionStart("SIMULATED-fault","1754288005","Demo",LmaxDemoOwnerConfirmedOpening.ResumeApproval,
                at,Now.AddHours(2),true,true,true,true,[new("GBPUSD","4002",10000m)],900,"LMAX_DEMO_LOCAL");
            Next=start with{SessionId="SIMULATED-new",ObservedAtUtc=Now};
            using(var j=LmaxDemoSessionJournal.CreateNew(Path))
            {
                var s=LmaxDemoControlledSession.Begin(j,start,at);s.RecordInboundControl(1,"A",at);
                if(defect is "cycle" or "send")s.BeginCycle("cycle",new Dictionary<string,decimal>{{"GBPUSD",1000m}},at);
                if(defect=="send")s.RecordSendIntent(new("cycle","parent","child","D",null,"GBPUSD","4002","BUY",.1m,new string('a',64),"2","0",1m),at);
                s.RecordRuntimeFault(defect=="wrong-fault"?"OTHER":"COORDINATOR_RECONCILIATION_REQUIRED",at.AddSeconds(1));
                s.RecordInboundControl(2,"0",at.AddSeconds(20));
            }
            File.WriteAllText(Proof,JsonSerializer.Serialize(new{simulated=true,recordedAtUtc=defect=="stale"?Now.AddMinutes(-20):at.AddSeconds(10)}));
            Database=new(Now,"LMAX_DEMO_LOCAL",R.FailedModelRunId,false,14,1,1,1,0,0,0,0,0,0);
        }
        public LmaxDemoRiskRejectedCertificate Prepare(LmaxDemoRetirementDatabaseEvidence? database=null)=>R.Prepare(Path,Proof,Hash,database??Database,Now,true);
        public void Dispose()=>Directory.Delete(Root,true);
    }
}
