using System.Text.Json;
using QQ.Production.Intraday.Application;
using Retirement = QQ.Production.Intraday.Application.LmaxDemoRecoveredSendRetirement;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxDemoRecoveredSendRetirementTests
{
    [Fact]
    public void AuditedRecoveryAllowsNewOwnerWithoutChangingFailedJournalOrInventingReports()
    {
        using var f = new Fixture();
        var before = File.ReadAllBytes(f.JournalPath);
        Assert.Throws<InvalidOperationException>(() => LmaxDemoSessionOwnership.Begin(f.Next, f.Now, f.Root));
        Retirement.WriteUnderOwnerLease(f.JournalPath, f.Prepare(), f.Now, true);
        Assert.Equal(before, File.ReadAllBytes(f.JournalPath));
        using var prior = LmaxDemoSessionJournal.OpenForInspection(f.JournalPath);
        Assert.Equal("INBOUND_CONTINUITY_LOST", LmaxDemoControlledSession.Inspect(prior).BlockingReason);
        Assert.DoesNotContain(prior.Entries, x => x.Kind == "Report");
        using var next = LmaxDemoSessionOwnership.Begin(f.Next, f.Now, f.Root);
        Assert.Throws<IOException>(() => LmaxDemoSessionOwnership.Begin(f.Next with { SessionId = "duplicate" }, f.Now, f.Root));
    }
    [Theory]
    [InlineData("stale")][InlineData("working")][InlineData("account")][InlineData("capture")]
    [InlineData("fills")][InlineData("audit")][InlineData("db-stale")][InlineData("position")]
    [InlineData("fix")][InlineData("break")][InlineData("plan")]
    public void UnresolvedEvidenceCannotRetireTheActualSend(string defect)
    {
        using var f = new Fixture();
        var observation = defect switch
        {
            "stale" => f.Observation with { ObservedAtUtc = f.Now.AddSeconds(-901) },
            "working" => f.Observation with { NoWorkingOrders = false },
            "account" => f.Observation with { AccountId = "other" },
            _ => f.Observation
        };
        var db = defect switch
        {
            "fills" => f.Database with { RecoveredFills = 3 },
            "audit" => f.Database with { RecoveryAuditVerified = false },
            "db-stale" => f.Database with { VerifiedAtUtc = f.Now.AddSeconds(-61) },
            "position" => f.Database with { NonZeroPositions = 1 },
            "fix" => f.Database with { OriginalFixReports = 4 },
            "break" => f.Database with { OpenReconciliationBreaks = 1 },
            "plan" => f.Database with { RecoveryPlanSha256 = new string('a',64) },
            _ => f.Database
        };
        if (defect == "capture") File.AppendAllText(f.EvidencePath, "CHANGED");
        Assert.ThrowsAny<Exception>(() => f.Prepare(observation, db));
        Assert.False(File.Exists(Retirement.CertificatePath(f.JournalPath)));
    }
    [Fact]
    public void ObservationTimestampCannotBeRefreshedAndSimulatedJournalCannotRetireRealAccount()
    {
        using var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() => f.Prepare(f.Observation with { ObservedAtUtc = f.Now.AddSeconds(-1) }));
        Assert.Throws<InvalidOperationException>(() => Retirement.Prepare(f.JournalPath, Fixture.Approval,
            f.Observation, f.EvidencePath, f.EvidenceHash, f.Database, f.Now));
    }
    [Fact]
    public void CertificateCannotBeOverwrittenOrOutliveChangedEvidence()
    {
        using var f = new Fixture();
        var c = f.Prepare();
        Retirement.WriteUnderOwnerLease(f.JournalPath, c, f.Now, true);
        Assert.Throws<IOException>(() => Retirement.WriteUnderOwnerLease(f.JournalPath, c, f.Now, true));
        File.AppendAllText(f.EvidencePath, "CHANGED");
        using var j = LmaxDemoSessionJournal.OpenForInspection(f.JournalPath);
        Assert.Throws<InvalidOperationException>(() => Retirement.IsValidated(f.JournalPath, j, f.Now.AddDays(1), true));
    }
    [Theory]
    [InlineData("valid")][InlineData("account")][InlineData("refresh")][InlineData("source")]
    [InlineData("working")][InlineData("automated")][InlineData("approval")][InlineData("report")]
    public void OwnerConfirmationIsExactDatedAndNeverMislabelledAsBrokerCapture(string defect)
    {
        var values = new Dictionary<string, object> {
            ["schema"]="lmax_demo_owner_confirmation_v1", ["source"]=LmaxDemoOwnerConfirmedOpening.Source,
            ["accountId"]="1754288005", ["owner"]="Philippe", ["ownerApprovalReference"]=LmaxDemoOwnerConfirmedOpening.Approval,
            ["quote"]=LmaxDemoOwnerConfirmedOpening.Quote, ["recordedAtUtc"]=LmaxDemoOwnerConfirmedOpening.RecordedAt,
            ["flat"]=true, ["noWorkingOrders"]=true, ["automatedBrokerObservation"]=false,
            ["openingReceiptSha256"]=LmaxDemoOwnerConfirmedOpening.OpeningReceiptHash };
        if (defect == "account") values["accountId"]="other";
        if (defect == "refresh") values["recordedAtUtc"]=LmaxDemoOwnerConfirmedOpening.RecordedAt.AddSeconds(1);
        if (defect == "source") values["source"]="OFFICIAL_UI";
        if (defect == "working") values["noWorkingOrders"]=false;
        if (defect == "automated") values["automatedBrokerObservation"]=true;
        if (defect == "approval") values["ownerApprovalReference"]="unrelated";
        if (defect == "report") values["openingReceiptSha256"]=new string('a',64);
        using var proof = JsonDocument.Parse(JsonSerializer.Serialize(values));
        if (defect == "valid") LmaxDemoOwnerConfirmedOpening.ValidateDeclaration(proof.RootElement, LmaxDemoOwnerConfirmedOpening.RecordedAt);
        else Assert.Throws<InvalidOperationException>(() => LmaxDemoOwnerConfirmedOpening.ValidateDeclaration(proof.RootElement, LmaxDemoOwnerConfirmedOpening.RecordedAt));
        Assert.Throws<InvalidOperationException>(() => LmaxDemoOwnerConfirmedOpening.ValidateFile("missing", "missing",
            LmaxDemoOwnerConfirmedOpening.RecordedAt, LmaxDemoOwnerConfirmedOpening.RecordedAt.AddSeconds(901)));
    }
    private sealed class Fixture : IDisposable
    {
        public const string Approval = "https://github.com/phu-qqb/IntradayPlatform/issues/84#issuecomment-12345";
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "SIMULATED-retirement-" + Guid.NewGuid().ToString("N"));
        public string JournalPath => Path.Combine(Root, "simulated-incident.journal.jsonl");
        public string EvidencePath => Path.Combine(Root, "SIMULATED-ui.json");
        public string EvidenceHash { get; }
        public DateTimeOffset Now { get; } = DateTimeOffset.Parse("2026-08-04T10:00:00Z");
        public LmaxDemoSessionStart Next { get; }
        public LmaxDemoRetirementObservation Observation { get; }
        public LmaxDemoRecoveredSendDatabaseEvidence Database { get; }
        public Fixture()
        {
            Directory.CreateDirectory(Root);
            var at = Now.AddDays(-1);
            var start = new LmaxDemoSessionStart("simulated-incident", "1754288005", "Demo", Approval, at, at.AddHours(2),
                true, true, true, true, [new("EURUSD", "4001", 10000m)], 900, "LMAX_DEMO_LOCAL");
            Next = start with { SessionId = "simulated-new-owner", ObservedAtUtc = Now, DeadlineUtc = Now.AddHours(2) };
            using (var j = LmaxDemoSessionJournal.CreateNew(JournalPath))
            {
                var s = LmaxDemoControlledSession.Begin(j, start, at);
                s.RecordInboundControl(1, "A", at);
                var cycle = Guid.Parse(Retirement.ModelRunId).ToString("N");
                s.BeginCycle(cycle, new Dictionary<string,decimal> { ["EURUSD"] = 30000m }, at);
                s.RecordSendIntent(new(cycle, "c9de92ad4c1c469dacb256842582c709", "DSc9de92ad4c1cP01", "D", null,
                    "EURUSD", "4001", "BUY", 3m, new string('a',64), "2", "0", 1.14771m), at);
                s.RecordSendCompleted("DSc9de92ad4c1cP01", at.AddSeconds(1));
                Assert.Throws<InvalidOperationException>(() => s.RecordInboundControl(2, "0", at.AddSeconds(100)));
            }
            File.WriteAllText(EvidencePath, JsonSerializer.Serialize(new { schema="lmax-demo-ui-inspection-v1", inspectedAtUtc=Now,
                origin="https://web-order.london-demo.lmax.com", account1754288005Visible=true, signInRequired=false,
                observationEstablished=true, explicitNoOpenPositionsVisible=true, explicitNoWorkingOrdersVisible=true,
                ordersSubmitted=0, accountRestCalled=false, simulated=true }));
            EvidenceHash = Retirement.HashFile(EvidencePath);
            Observation = new(start.SessionId, start.AccountId, Now, true, true, true, "SIMULATED TEST ONLY");
            Database = new("LMAX_DEMO_LOCAL", Retirement.ModelRunId, Retirement.RecoveryId, Retirement.PlanSha256,
                new string('b',64), Now, Guid.NewGuid(), true, true, 4, 4, 0m, 0, 0, 0, 0);
        }
        public LmaxDemoRecoveredSendRetirementCertificate Prepare(LmaxDemoRetirementObservation? observation = null,
            LmaxDemoRecoveredSendDatabaseEvidence? database = null) => Retirement.Prepare(JournalPath, Approval,
                observation ?? Observation, EvidencePath, EvidenceHash, database ?? Database, Now, true);
        public void Dispose() => Directory.Delete(Root, true);
    }
}
