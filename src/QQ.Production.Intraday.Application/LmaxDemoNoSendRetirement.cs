using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Application;

public sealed record LmaxDemoRetirementObservation(string SessionId, string AccountId,
    DateTimeOffset ObservedAtUtc, bool Flat, bool NoWorkingOrders,
    bool ExclusiveOrderActivityDeclared, string EvidenceReference);

public sealed record LmaxDemoRetirementDatabaseEvidence(DateTimeOffset VerifiedAtUtc,
    string AccountCode, string FailedModelRunId, bool FailedRunProcessed,
    int FullPortfolioWeightCount, int FailedRunTargets, int FailedRunTradeIntents,
    int FailedRunRiskDecisions, int NewParentOrders, int NewChildOrders,
    int NewExecutionReports, int NewFills, int OpenChildOrders, int NonZeroPositions);

public sealed record LmaxDemoNoSendRetirementCertificate(string Schema, string SessionId,
    string AccountId, string OwnerApprovalReference, DateTimeOffset RetiredAtUtc,
    string JournalSha256, int JournalEntryCount, string LastEntrySha256,
    string FaultEntrySha256, LmaxDemoRetirementObservation Observation,
    LmaxDemoRetirementDatabaseEvidence DatabaseEvidence);

/// <summary>A separately approved retirement of the exact 17 September no-send
/// coordinator failure. The original journal remains immutable and faulted.
/// This does not resume a session, forgive an ambiguous send, or replay a cycle.</summary>
public static class LmaxDemoNoSendRetirement
{
    public const string RealSessionId = "lmax-demo-20260917T090200Z";
    public const string FailedModelRunId = "2c0a2ec7-549a-403c-8a05-7ff47e4f5323";
    public const string RealStartHash = "2372A778E639369C78A20EC7EAA3EFEB7E93F974F310A5439FD5823A0FF52AA4";
    public const string RealFaultHash = "C52841607B69F82031A793F3F488964972AA4EDEC9F303AC303A77C2B6F3EE94";
    public const string Schema = "lmax_demo_no_send_retirement_v1";

    public static string CertificatePath(string journalPath) => journalPath + ".no-send-retirement.json";

    public static LmaxDemoNoSendRetirementCertificate Prepare(string journalPath,
        string expectedJournalSha256, string approvalReference,
        LmaxDemoRetirementObservation observation, LmaxDemoRetirementDatabaseEvidence database,
        DateTimeOffset now, bool simulated = false)
    {
        using var journal = LmaxDemoSessionJournal.OpenForInspection(journalPath);
        var session = RequireEligibleJournal(journal, simulated);
        var hash = HashFile(journalPath);
        Require(hash.Equals(expectedJournalSha256, StringComparison.OrdinalIgnoreCase), "JOURNAL_HASH_CHANGED");
        var certificate = new LmaxDemoNoSendRetirementCertificate(Schema, session.Start.SessionId,
            session.Start.AccountId, approvalReference, now, hash, journal.Entries.Count,
            journal.Entries[^1].Sha256, journal.Entries.Single(x => x.Kind == "Fault").Sha256,
            observation, database);
        Validate(journalPath, journal, certificate, now, simulated);
        return certificate;
    }

    // The caller must hold the same exclusive account owner lock used by Begin.
    // The operator CLI acquires it before the final database check and this write.
    public static void WriteUnderOwnerLease(string journalPath, LmaxDemoNoSendRetirementCertificate certificate,
        DateTimeOffset now, bool simulated = false)
    {
        using var journal = LmaxDemoSessionJournal.OpenForInspection(journalPath);
        Validate(journalPath, journal, certificate, now, simulated);
        Require(now - certificate.RetiredAtUtc <= TimeSpan.FromSeconds(30), "RETIREMENT_WRITE_EXPIRED");
        var path = CertificatePath(journalPath);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(certificate, new JsonSerializerOptions { WriteIndented = true });
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(bytes);
        output.Flush(true);
    }

    public static bool IsValidated(string journalPath, LmaxDemoSessionJournal journal,
        DateTimeOffset now, bool simulated = false)
    {
        var path = CertificatePath(journalPath);
        if (!File.Exists(path)) return false;
        Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0
            && new FileInfo(path).Length is > 0 and <= 16384, "CERTIFICATE_FILE_INVALID");
        var certificate = JsonSerializer.Deserialize<LmaxDemoNoSendRetirementCertificate>(File.ReadAllBytes(path))
            ?? throw Error("CERTIFICATE_INVALID");
        Validate(journalPath, journal, certificate, now, simulated);
        return true;
    }

    public static LmaxDemoControlledSession RequireEligibleJournal(LmaxDemoSessionJournal journal, bool simulated = false)
    {
        var session = LmaxDemoControlledSession.Inspect(journal);
        var entries = journal.Entries;
        Require(!session.IsClosed && session.Start.Simulated == simulated
            && session.Start.Environment == "Demo" && session.Start.AccountId == LmaxDemoControlledSession.DemoAccountId
            && session.Start.InternalBrokerAccountCode == "LMAX_DEMO_LOCAL"
            && session.Start.ObservedFlat && session.Start.ObservedNoWorkingOrders && session.Start.ExclusiveOrderActivityDeclared,
            "SESSION_SCOPE_INVALID");
        Require(entries.Count(x => x.Kind == "Start") == 1
            && entries.Count(x => x.Kind == "Fault") == 1
            && entries.All(x => x.Kind is "Start" or "Inbound" or "Fault")
            && session.KnownOrders.Count == 0 && session.FillDerivedPositions.All(x => x.Value == 0m)
            && session.BlockingReason == "COORDINATOR_RECONCILIATION_REQUIRED"
            && JsonSerializer.Deserialize<string>(entries.Single(x => x.Kind == "Fault").Data) == "COORDINATOR_RECONCILIATION_REQUIRED",
            "NOT_THE_NO_SEND_COORDINATOR_FAILURE");
        if (!simulated)
            Require(session.Start.SessionId == RealSessionId && entries[0].Sha256 == RealStartHash
                && entries.Single(x => x.Kind == "Fault") is { Index: 493, Sha256: RealFaultHash }, "EXACT_REAL_FAILURE_BINDING_REQUIRED");
        return session;
    }

    private static void Validate(string path, LmaxDemoSessionJournal journal,
        LmaxDemoNoSendRetirementCertificate c, DateTimeOffset now, bool simulated)
    {
        var session = RequireEligibleJournal(journal, simulated);
        var last = journal.Entries[^1];
        Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, "JOURNAL_REPARSE_POINT_REJECTED");
        Require(c.Schema == Schema && c.SessionId == session.Start.SessionId && c.AccountId == session.Start.AccountId
            && c.JournalEntryCount == journal.Entries.Count && c.LastEntrySha256 == last.Sha256
            && c.FaultEntrySha256 == journal.Entries.Single(x => x.Kind == "Fault").Sha256
            && c.JournalSha256.Equals(HashFile(path), StringComparison.OrdinalIgnoreCase), "CERTIFICATE_JOURNAL_BINDING_INVALID");
        Require(Regex.IsMatch(c.OwnerApprovalReference ?? "", @"^https://github\.com/phu-qqb/IntradayPlatform/issues/84#issuecomment-[1-9][0-9]*$"),
            "EXPLICIT_OWNER_APPROVAL_REFERENCE_REQUIRED");
        Require(now.Offset == TimeSpan.Zero && c.RetiredAtUtc.Offset == TimeSpan.Zero
            && c.RetiredAtUtc <= now && c.RetiredAtUtc >= last.AtUtc, "RETIREMENT_TIME_INVALID");
        var o = c.Observation;
        Require(o is not null && o.SessionId == c.SessionId && o.AccountId == c.AccountId
            && o.Flat && o.NoWorkingOrders && o.ExclusiveOrderActivityDeclared && !string.IsNullOrWhiteSpace(o.EvidenceReference)
            && o.ObservedAtUtc.Offset == TimeSpan.Zero && o.ObservedAtUtc >= last.AtUtc
            && o.ObservedAtUtc <= c.RetiredAtUtc && c.RetiredAtUtc - o.ObservedAtUtc <= TimeSpan.FromSeconds(900),
            "GENUINE_POST_STOP_FLAT_OBSERVATION_REQUIRED");
        var d = c.DatabaseEvidence;
        Require(d is not null && d.AccountCode == "LMAX_DEMO_LOCAL" && d.FailedModelRunId == FailedModelRunId
            && !d.FailedRunProcessed && d.FullPortfolioWeightCount == 39
            && d.FailedRunTargets == 0 && d.FailedRunTradeIntents == 0 && d.FailedRunRiskDecisions == 0
            && d.NewParentOrders == 0 && d.NewChildOrders == 0 && d.NewExecutionReports == 0 && d.NewFills == 0
            && d.OpenChildOrders == 0 && d.NonZeroPositions == 0
            && d.VerifiedAtUtc.Offset == TimeSpan.Zero && d.VerifiedAtUtc >= last.AtUtc
            && d.VerifiedAtUtc <= c.RetiredAtUtc && c.RetiredAtUtc - d.VerifiedAtUtc <= TimeSpan.FromSeconds(60),
            "CURRENT_ZERO_SEND_DATABASE_RECONCILIATION_REQUIRED");
    }

    public static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Require(bool condition, string code) { if (!condition) throw Error(code); }
    private static InvalidOperationException Error(string code) => new("DEMO_NO_SEND_RETIREMENT_" + code);
}
