using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Application;

public sealed record LmaxDemoDuplicateOrderDatabaseEvidence(DateTimeOffset VerifiedAtUtc,
    string AccountCode, string FailedModelRunId, bool FailedRunProcessed,
    int FullPortfolioWeightCount, int FailedRunTargets, int FailedRunTradeIntents,
    int FailedRunRiskDecisions, int NewParentOrders, int NewChildOrders,
    int NewExecutionReports, int NewFills, int OpenChildOrders, int NonZeroPositions,
    string ParentOrderId, string ChildOrderId, string ParentStatus, string ChildStatus,
    string Disposition, DateTimeOffset DispositionAtUtc);

public sealed record LmaxDemoDuplicateOrderRetirementCertificate(string Schema, string SessionId,
    string AccountId, string OwnerApprovalReference, DateTimeOffset RetiredAtUtc,
    string JournalSha256, int JournalEntryCount, string LastEntrySha256,
    string FaultEntrySha256, LmaxDemoRetirementObservation Observation,
    LmaxDemoDuplicateOrderDatabaseEvidence DatabaseEvidence, string ObservationSource);

/// <summary>Exact 18 September duplicate-client-ID failure before batch dispatch.
/// Only the persisted, provably unsent parent/child may expire locally after
/// the cycle deadline. Retirement requires the normal owner lease, a fresh
/// owner declaration and audited database readback; it never emits a broker event.</summary>
public static class LmaxDemoDuplicateOrderRetirement
{
    public const string RealSessionId = "lmax-demo-20260918-gmv-152437";
    public const string FailedModelRunId = "6fa561c1-454d-49a2-bd58-a11f16b1ba70";
    public const string RealStartHash = "246544E7935C2AB0179E992849F67E825A91803DDCBE34D0283025F965D3EFC4";
    public const string RealFaultHash = "6A71A11EFFF76D1EC7EA1ED170128C10EDF68BD4B41917B8FCFEE26F044ECCE4";
    public const string Schema = "lmax_demo_duplicate_order_retirement_v1";

    public static string CertificatePath(string journalPath) => journalPath + ".duplicate-order-retirement.json";

    public static LmaxDemoDuplicateOrderRetirementCertificate Prepare(string journalPath,
        string expectedJournalSha256, string approvalReference,
        LmaxDemoRetirementObservation observation, LmaxDemoDuplicateOrderDatabaseEvidence database,
        DateTimeOffset now, bool simulated = false)
    {
        using var journal = LmaxDemoSessionJournal.OpenForInspection(journalPath);
        var session = RequireEligibleJournal(journal, simulated);
        var hash = HashFile(journalPath);
        Require(hash.Equals(expectedJournalSha256, StringComparison.OrdinalIgnoreCase), "JOURNAL_HASH_CHANGED");
        var certificate = new LmaxDemoDuplicateOrderRetirementCertificate(Schema, session.Start.SessionId,
            session.Start.AccountId, approvalReference, now, hash, journal.Entries.Count,
            journal.Entries[^1].Sha256, journal.Entries.Single(x => x.Kind == "Fault").Sha256,
            observation, database, simulated ? "SIMULATED_OWNER_CONFIRMATION" : "OWNER_CONFIRMED_ACCOUNT_STATE");
        Validate(journalPath, journal, certificate, now, simulated);
        return certificate;
    }

    // The caller must hold the same exclusive account owner lock used by Begin.
    // The operator CLI acquires it before the final database check and this write.
    public static void WriteUnderOwnerLease(string journalPath, LmaxDemoDuplicateOrderRetirementCertificate certificate,
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
        var certificate = JsonSerializer.Deserialize<LmaxDemoDuplicateOrderRetirementCertificate>(File.ReadAllBytes(path))
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
            && entries.Count(x => x.Kind == "Cycle") == 0
            && entries.All(x => x.Kind is "Start" or "Inbound" or "Fault")
            && session.KnownOrders.Count == 0 && session.FillDerivedPositions.All(x => x.Value == 0m)
            && session.BlockingReason == "COORDINATOR_RECONCILIATION_REQUIRED"
            && JsonSerializer.Deserialize<string>(entries.Single(x => x.Kind == "Fault").Data) == "COORDINATOR_RECONCILIATION_REQUIRED",
            "NOT_THE_NO_SEND_COORDINATOR_FAILURE");
        if (!simulated)
            Require(session.Start.SessionId == RealSessionId && entries[0].Sha256 == RealStartHash
                && entries.Single(x => x.Kind == "Fault") is { Index: 42, Sha256: RealFaultHash }, "EXACT_REAL_FAILURE_BINDING_REQUIRED");
        return session;
    }

    private static void Validate(string path, LmaxDemoSessionJournal journal,
        LmaxDemoDuplicateOrderRetirementCertificate c, DateTimeOffset now, bool simulated)
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
            && o.Flat && o.NoWorkingOrders && o.ExclusiveOrderActivityDeclared && o.EvidenceReference == c.OwnerApprovalReference
            && c.ObservationSource == (simulated ? "SIMULATED_OWNER_CONFIRMATION" : "OWNER_CONFIRMED_ACCOUNT_STATE")
            && o.ObservedAtUtc.Offset == TimeSpan.Zero && o.ObservedAtUtc >= journal.Entries.Single(x => x.Kind == "Fault").AtUtc
            && o.ObservedAtUtc <= c.RetiredAtUtc && c.RetiredAtUtc - o.ObservedAtUtc <= TimeSpan.FromSeconds(900),
            "GENUINE_CURRENT_OWNER_CONFIRMATION_REQUIRED");
        var d = c.DatabaseEvidence;
        Require(d is not null && d.AccountCode == "LMAX_DEMO_LOCAL" && d.FailedModelRunId == FailedModelRunId
            && !d.FailedRunProcessed && d.FullPortfolioWeightCount == 14
            && d.FailedRunTargets == 2 && d.FailedRunTradeIntents == 2 && d.FailedRunRiskDecisions == 2
            && d.NewParentOrders == 1 && d.NewChildOrders == 1 && d.NewExecutionReports == 0 && d.NewFills == 0
            && d.OpenChildOrders == 0 && d.NonZeroPositions == 0
            && d.ParentOrderId == "f63dba82-e46a-4a38-b773-92edd01dadfa"
            && d.ChildOrderId == "4f900cc0-ace7-42f7-be9a-ebb03ea8f95f"
            && d.ParentStatus == "Expired" && d.ChildStatus == "Expired"
            && d.Disposition == "LOCAL_PRE_SEND_DEADLINE_EXPIRED"
            && d.DispositionAtUtc > DateTimeOffset.Parse("2026-09-18T16:00:00Z")
            && d.DispositionAtUtc >= last.AtUtc && d.DispositionAtUtc <= d.VerifiedAtUtc
            && d.VerifiedAtUtc.Offset == TimeSpan.Zero && d.VerifiedAtUtc >= last.AtUtc
            && d.VerifiedAtUtc <= c.RetiredAtUtc && c.RetiredAtUtc - d.VerifiedAtUtc <= TimeSpan.FromSeconds(60),
            "CURRENT_ZERO_SEND_DATABASE_RECONCILIATION_REQUIRED");
    }

    public static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Require(bool condition, string code) { if (!condition) throw Error(code); }
    private static InvalidOperationException Error(string code) => new("DEMO_DUPLICATE_ORDER_RETIREMENT_" + code);
}
