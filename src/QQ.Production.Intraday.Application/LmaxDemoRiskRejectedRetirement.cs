using System.Text.Json;

namespace QQ.Production.Intraday.Application;

public sealed record LmaxDemoRiskRejectedCertificate(LmaxDemoNoSendRetirementCertificate Evidence,
    string OwnerDeclarationPath,string OwnerDeclarationSha256,bool Simulated);

/// <summary>Exact 18 September risk-rejected, no-send incident. The owner's
/// pre-stop declaration is bridged to shutdown by the fully verified no-send
/// journal and post-stop zero-order database proof; its time is never refreshed.</summary>
public static class LmaxDemoRiskRejectedRetirement
{
    public const string SessionId="lmax-demo-20260918-full-usd-130841";
    public const string FailedModelRunId="4b375531-8d81-4560-ab55-eac6a172b321";
    public const string JournalHash="e5adde154edffe3f473d1bd32a98a4cc83a1276e36381b92898ac4401ae49dbc";
    public const string Schema="lmax_demo_risk_rejected_retirement_v1";
    public static string CertificatePath(string path)=>path+".risk-rejected-retirement.json";

    public static LmaxDemoRiskRejectedCertificate Prepare(string path,string declarationPath,string declarationHash,
        LmaxDemoRetirementDatabaseEvidence database,DateTimeOffset now,bool simulated=false)
    {
        using var j=LmaxDemoSessionJournal.OpenForInspection(path);
        var s=LmaxDemoControlledSession.Inspect(j);
        var observed=simulated?JsonDocument.Parse(File.ReadAllBytes(declarationPath)).RootElement.GetProperty("recordedAtUtc").GetDateTimeOffset()
            :LmaxDemoOwnerConfirmedOpening.ResumeRecordedAt;
        var o=new LmaxDemoRetirementObservation(s.Start.SessionId,s.Start.AccountId,observed,true,true,true,LmaxDemoOwnerConfirmedOpening.ResumeApproval);
        var e=new LmaxDemoNoSendRetirementCertificate(Schema,s.Start.SessionId,s.Start.AccountId,LmaxDemoOwnerConfirmedOpening.ResumeApproval,
            now,LmaxDemoRecoveredSendRetirement.HashFile(path),j.Entries.Count,j.Entries[^1].Sha256,
            j.Entries.Single(x=>x.Kind=="Fault").Sha256,o,database);
        var c=new LmaxDemoRiskRejectedCertificate(e,declarationPath,declarationHash,simulated);
        Validate(path,j,c,now,simulated); return c;
    }
    public static void WriteUnderOwnerLease(string path,LmaxDemoRiskRejectedCertificate c,DateTimeOffset now,bool simulated=false)
    {
        using var j=LmaxDemoSessionJournal.OpenForInspection(path); Validate(path,j,c,now,simulated);
        Require(now-c.Evidence.RetiredAtUtc<=TimeSpan.FromSeconds(30),"WRITE_EXPIRED");
        using var f=new FileStream(CertificatePath(path),FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        JsonSerializer.Serialize(f,c,new JsonSerializerOptions{WriteIndented=true});f.Flush(true);
    }
    public static bool IsValidated(string path,LmaxDemoSessionJournal j,DateTimeOffset now,bool simulated=false)
    {
        var p=CertificatePath(path);if(!File.Exists(p))return false;
        Require((File.GetAttributes(p)&FileAttributes.ReparsePoint)==0 && new FileInfo(p).Length is >0 and <=32768,"CERTIFICATE_FILE_INVALID");
        var c=JsonSerializer.Deserialize<LmaxDemoRiskRejectedCertificate>(File.ReadAllBytes(p))??throw new InvalidOperationException("CERTIFICATE_INVALID");
        Validate(path,j,c,now,simulated);return true;
    }
    private static void Validate(string path,LmaxDemoSessionJournal j,LmaxDemoRiskRejectedCertificate c,DateTimeOffset now,bool simulated)
    {
        var s=LmaxDemoControlledSession.Inspect(j);var e=c.Evidence;var entries=j.Entries;
        Require(!s.IsClosed && s.Start.Simulated==simulated && c.Simulated==simulated && s.Start.Environment=="Demo"
            && s.Start.AccountId=="1754288005" && s.Start.InternalBrokerAccountCode=="LMAX_DEMO_LOCAL"
            && s.Start.ObservedFlat && s.Start.ObservedNoWorkingOrders && s.Start.ExclusiveOrderActivityDeclared
            && s.BlockingReason=="COORDINATOR_RECONCILIATION_REQUIRED" && entries.Count(x=>x.Kind=="Fault")==1
            && entries.All(x=>x.Kind is "Start" or "Inbound" or "Fault") && s.KnownOrders.Count==0
            && s.FillDerivedPositions.Values.All(x=>x==0m),"EXACT_NO_SEND_FAILURE_REQUIRED");
        Require(e.Schema==Schema && e.SessionId==s.Start.SessionId && e.AccountId==s.Start.AccountId
            && e.JournalEntryCount==entries.Count && e.LastEntrySha256==entries[^1].Sha256
            && e.FaultEntrySha256==entries.Single(x=>x.Kind=="Fault").Sha256
            && e.JournalSha256==LmaxDemoRecoveredSendRetirement.HashFile(path)
            && (File.GetAttributes(path)&FileAttributes.ReparsePoint)==0,"JOURNAL_CHANGED");
        if(!simulated)Require(e.SessionId==SessionId && e.JournalSha256==JournalHash
            && entries[0].Sha256=="32DD0765C6204C60C57B1C023527F4CD8B169165B82216CDB3D997DF1C2E62D1"
            && e.FaultEntrySha256=="A39F328E598AE0FB175B3B586C3DD254AE6FF37A016BF9237DD64822B1FBA4BD","EXACT_REAL_INCIDENT_REQUIRED");
        var o=e.Observation;
        Require(now.Offset==TimeSpan.Zero && e.RetiredAtUtc.Offset==TimeSpan.Zero && e.RetiredAtUtc<=now
            && e.RetiredAtUtc>=entries[^1].AtUtc && e.OwnerApprovalReference==LmaxDemoOwnerConfirmedOpening.ResumeApproval
            && o.SessionId==e.SessionId && o.AccountId==e.AccountId && o.Flat && o.NoWorkingOrders && o.ExclusiveOrderActivityDeclared
            && o.EvidenceReference==e.OwnerApprovalReference && o.ObservedAtUtc.Offset==TimeSpan.Zero
            && o.ObservedAtUtc>=entries.Single(x=>x.Kind=="Fault").AtUtc && o.ObservedAtUtc<=e.RetiredAtUtc
            && e.RetiredAtUtc-o.ObservedAtUtc<=TimeSpan.FromSeconds(900),"DATED_OWNER_CONFIRMATION_REQUIRED");
        Require(File.Exists(c.OwnerDeclarationPath) && (File.GetAttributes(c.OwnerDeclarationPath)&FileAttributes.ReparsePoint)==0
            && LmaxDemoRecoveredSendRetirement.HashFile(c.OwnerDeclarationPath)==c.OwnerDeclarationSha256,"DECLARATION_CHANGED");
        if(!simulated)LmaxDemoOwnerConfirmedOpening.ValidateFile(c.OwnerDeclarationPath,c.OwnerDeclarationSha256,o.ObservedAtUtc,e.RetiredAtUtc);
        else {using var proof=JsonDocument.Parse(File.ReadAllBytes(c.OwnerDeclarationPath));Require(proof.RootElement.GetProperty("simulated").GetBoolean()
            && proof.RootElement.GetProperty("recordedAtUtc").GetDateTimeOffset()==o.ObservedAtUtc,"SIMULATED_PROOF_REQUIRED");}
        var d=e.DatabaseEvidence;
        Require(d.AccountCode=="LMAX_DEMO_LOCAL" && d.FailedModelRunId==FailedModelRunId && !d.FailedRunProcessed
            && d.FullPortfolioWeightCount==14 && d.FailedRunTargets==1 && d.FailedRunTradeIntents==1 && d.FailedRunRiskDecisions==1
            && d.NewParentOrders==0 && d.NewChildOrders==0 && d.NewExecutionReports==0 && d.NewFills==0 && d.OpenChildOrders==0
            && d.NonZeroPositions==0 && d.VerifiedAtUtc.Offset==TimeSpan.Zero && d.VerifiedAtUtc>=entries[^1].AtUtc
            && d.VerifiedAtUtc<=e.RetiredAtUtc && e.RetiredAtUtc-d.VerifiedAtUtc<=TimeSpan.FromSeconds(60),"CURRENT_ZERO_ORDER_DATABASE_PROOF_REQUIRED");
    }
    private static void Require(bool ok,string code){if(!ok)throw new InvalidOperationException("RISK_REJECTED_RETIREMENT_"+code);}
}
