using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Application;

public sealed record LmaxDemoRecoveredSendDatabaseEvidence(string AccountCode, string ModelRunId,
    Guid RecoveryId, string RecoveryPlanSha256, string RecoveryAuditSha256, DateTimeOffset VerifiedAtUtc,
    Guid ReconciliationRunId, bool RecoveryReadbackVerified, bool RecoveryAuditVerified,
    int RecoveredFills, int RecoveredLedgerEvents, decimal RecoveredNetQuantity,
    int OpenChildOrders, int NonZeroPositions, int OpenReconciliationBreaks, int OriginalFixReports);
public sealed record LmaxDemoRecoveredSendRetirementCertificate(string Schema, string SessionId, string AccountId,
    string OwnerApprovalReference, DateTimeOffset RetiredAtUtc, string JournalSha256, int JournalEntries,
    string LastEntrySha256, LmaxDemoRetirementObservation Observation, string ObservationEvidencePath,
    string ObservationEvidenceSha256, LmaxDemoRecoveredSendDatabaseEvidence Database);

/// <summary>Retire the exact recovered actual-send incident without rewriting its
/// journal or pretending that recovered accounting rows were FIX messages.
/// This certificate never resumes an old cycle and never proves today's account state.</summary>
public static class LmaxDemoRecoveredSendRetirement
{
    public const string Schema = "lmax_demo_recovered_send_retirement_v1";
    public const string SessionId = "lmax-demo-20260917-streaming-bbo";
    public const string ModelRunId = "d2534530-1335-48d6-b026-d2e898c56d77";
    public const string JournalSha256 = "f133311e31c47aaf9fcbd817712b27d26e47ce3e30f3bedeb7c21118da83af49";
    public const string PlanSha256 = "3111d77bd12ef971a55898401e38c7368f34b553a640d6f1ca4aa008fe5c1b0f";
    public static readonly Guid RecoveryId = Guid.Parse("57841f87-129a-44f7-2dc2-f53f8959dccf");
    public static string CertificatePath(string journalPath) => journalPath + ".recovered-send-retirement.json";
    public static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    public static LmaxDemoRecoveredSendRetirementCertificate Prepare(string journalPath, string approval,
        LmaxDemoRetirementObservation observation, string evidencePath, string evidenceHash,
        LmaxDemoRecoveredSendDatabaseEvidence database, DateTimeOffset now, bool simulated = false)
    {
        using var journal = LmaxDemoSessionJournal.OpenForInspection(journalPath);
        var session = LmaxDemoControlledSession.Inspect(journal);
        var certificate = new LmaxDemoRecoveredSendRetirementCertificate(Schema, session.Start.SessionId,
            session.Start.AccountId, approval, now, HashFile(journalPath), journal.Entries.Count,
            journal.Entries[^1].Sha256, observation, evidencePath, evidenceHash, database);
        Validate(journalPath, journal, certificate, now, simulated);
        return certificate;
    }
    public static void WriteUnderOwnerLease(string journalPath, LmaxDemoRecoveredSendRetirementCertificate certificate,
        DateTimeOffset now, bool simulated = false)
    {
        using var journal = LmaxDemoSessionJournal.OpenForInspection(journalPath);
        Validate(journalPath, journal, certificate, now, simulated);
        Require(now - certificate.RetiredAtUtc <= TimeSpan.FromSeconds(30), "WRITE_EXPIRED");
        using var file = new FileStream(CertificatePath(journalPath), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(file, certificate, new JsonSerializerOptions { WriteIndented = true });
        file.Flush(true);
    }
    public static bool IsValidated(string journalPath, LmaxDemoSessionJournal journal, DateTimeOffset now, bool simulated = false)
    {
        var path = CertificatePath(journalPath);
        if (!File.Exists(path)) return false;
        Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 && new FileInfo(path).Length is > 0 and <= 32768, "CERTIFICATE_FILE_INVALID");
        var c = JsonSerializer.Deserialize<LmaxDemoRecoveredSendRetirementCertificate>(File.ReadAllBytes(path));
        Require(c is not null, "CERTIFICATE_INVALID");
        Validate(journalPath, journal, c!, now, simulated);
        return true;
    }
    private static void Validate(string path, LmaxDemoSessionJournal journal, LmaxDemoRecoveredSendRetirementCertificate c,
        DateTimeOffset now, bool simulated)
    {
        var session = LmaxDemoControlledSession.Inspect(journal);
        var entries = journal.Entries;
        Require(!session.IsClosed && session.Start.Simulated == simulated && session.Start.Environment == "Demo"
            && session.Start.AccountId == LmaxDemoControlledSession.DemoAccountId && session.Start.InternalBrokerAccountCode == "LMAX_DEMO_LOCAL"
            && session.BlockingReason == "INBOUND_CONTINUITY_LOST"
            && entries.Count(x => x.Kind == "SendIntent") == 1 && entries.Count(x => x.Kind == "SendCompleted") == 1
            && entries.All(x => x.Kind is not ("Report" or "DuplicateReport" or "ExecutionReport"))
            && session.KnownOrders.Count == 1 && session.KnownOrders[0].SendCompleted, "EXACT_RECOVERED_SEND_REQUIRED");
        var send = session.KnownOrders[0].Intent;
        Require(send.MessageType == "D" && send.ClientOrderId == "DSc9de92ad4c1cP01"
            && send.CycleId == Guid.Parse(ModelRunId).ToString("N") && send.Symbol == "EURUSD" && send.SecurityId == "4001"
            && send.Side == "BUY" && send.VenueQuantity == 3m && send.LimitPrice == 1.14771m
            && send.OrderTypeRaw == "2" && send.TimeInForceRaw == "0", "SENT_ORDER_BINDING_CHANGED");
        Require(c.Schema == Schema && c.SessionId == session.Start.SessionId && c.AccountId == session.Start.AccountId
            && c.JournalEntries == entries.Count && c.LastEntrySha256 == entries[^1].Sha256
            && c.JournalSha256 == HashFile(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0,
            "JOURNAL_BINDING_CHANGED");
        if (!simulated) Require(c.SessionId == SessionId && c.JournalSha256 == JournalSha256, "REAL_INCIDENT_BINDING_CHANGED");
        Require(Regex.IsMatch(c.OwnerApprovalReference ?? "", @"^https://github\.com/phu-qqb/IntradayPlatform/issues/84#issuecomment-[1-9][0-9]*$"), "OWNER_AUTHORITY_REQUIRED");
        Require(now.Offset == TimeSpan.Zero && c.RetiredAtUtc.Offset == TimeSpan.Zero
            && c.RetiredAtUtc >= entries[^1].AtUtc && c.RetiredAtUtc <= now, "INVALID_RETIREMENT_TIME");
        var o = c.Observation;
        Require(o.AccountId == c.AccountId && o.SessionId == c.SessionId && o.Flat && o.NoWorkingOrders && o.ExclusiveOrderActivityDeclared
            && !string.IsNullOrWhiteSpace(o.EvidenceReference) && o.ObservedAtUtc.Offset == TimeSpan.Zero
            && o.ObservedAtUtc >= entries[^1].AtUtc && o.ObservedAtUtc <= c.RetiredAtUtc
            && c.RetiredAtUtc - o.ObservedAtUtc <= TimeSpan.FromSeconds(900), "CURRENT_OFFICIAL_ACCOUNT_OBSERVATION_REQUIRED");
        Require(File.Exists(c.ObservationEvidencePath) && (File.GetAttributes(c.ObservationEvidencePath) & FileAttributes.ReparsePoint) == 0
            && new FileInfo(c.ObservationEvidencePath).Length > 0 && HashFile(c.ObservationEvidencePath) == c.ObservationEvidenceSha256,
            "RETAINED_OBSERVATION_EVIDENCE_REQUIRED");
        using var capture = JsonDocument.Parse(File.ReadAllBytes(c.ObservationEvidencePath));
        var proof = capture.RootElement;
        if (proof.GetProperty("schema").GetString() == "lmax_demo_owner_confirmation_v2")
        {
            Require(o.EvidenceReference == LmaxDemoOwnerConfirmedOpening.Approval, "OWNER_DECLARATION_REFERENCE_REQUIRED");
            LmaxDemoOwnerConfirmedOpening.ValidateFile(c.ObservationEvidencePath, c.ObservationEvidenceSha256, o.ObservedAtUtc, c.RetiredAtUtc);
        }
        else
        {
        Require(proof.GetProperty("schema").GetString() == "lmax-demo-ui-inspection-v1"
            && proof.GetProperty("origin").GetString() == "https://web-order.london-demo.lmax.com"
            && proof.GetProperty("inspectedAtUtc").GetDateTimeOffset() == o.ObservedAtUtc
            && proof.GetProperty("account1754288005Visible").GetBoolean() && !proof.GetProperty("signInRequired").GetBoolean()
            && proof.GetProperty("observationEstablished").GetBoolean() && proof.GetProperty("explicitNoOpenPositionsVisible").GetBoolean()
            && proof.GetProperty("explicitNoWorkingOrdersVisible").GetBoolean() && proof.GetProperty("ordersSubmitted").GetInt32() == 0
            && !proof.GetProperty("accountRestCalled").GetBoolean(), "AUTHENTIC_OFFICIAL_UI_OBSERVATION_NOT_ESTABLISHED");
        }
        var d = c.Database;
        Require(d.AccountCode == "LMAX_DEMO_LOCAL" && d.ModelRunId == ModelRunId && d.RecoveryId == RecoveryId
            && d.RecoveryPlanSha256 == PlanSha256 && Regex.IsMatch(d.RecoveryAuditSha256 ?? "", "^[a-f0-9]{64}$")
            && d.RecoveryReadbackVerified && d.RecoveryAuditVerified && d.ReconciliationRunId != Guid.Empty
            && d.RecoveredFills == 4 && d.RecoveredLedgerEvents == 4 && d.RecoveredNetQuantity == 0m
            && d.OpenChildOrders == 0 && d.NonZeroPositions == 0 && d.OpenReconciliationBreaks == 0 && d.OriginalFixReports == 0
            && d.VerifiedAtUtc.Offset == TimeSpan.Zero && d.VerifiedAtUtc >= entries[^1].AtUtc && d.VerifiedAtUtc <= c.RetiredAtUtc
            && c.RetiredAtUtc - d.VerifiedAtUtc <= TimeSpan.FromSeconds(60), "AUDITED_RECOVERY_AND_CURRENT_DATABASE_REQUIRED");
    }
    private static void Require(bool ok, string code) { if (!ok) throw new InvalidOperationException("DEMO_RECOVERED_SEND_RETIREMENT_" + code); }
}
