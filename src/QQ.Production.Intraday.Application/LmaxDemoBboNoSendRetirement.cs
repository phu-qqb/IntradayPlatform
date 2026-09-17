using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Application;

/// <summary>A separately approved retirement of the exact 17 September no-send
/// pre-send market-data request failure after the 16:15 cycle. Only the exact two
/// internal orders may expire locally after their deadline; no venue event is invented. The original journal remains immutable and faulted.
/// This does not resume a session, forgive an ambiguous send, or replay a cycle.</summary>
public static class LmaxDemoBboNoSendRetirement
{
    public const string RealSessionId = "lmax-demo-20260917-no-demo-caps";
    public const string FailedModelRunId = "7bf43936-03da-4afa-be99-3db79081a786";
    public const string RealStartHash = "391478E28D2C6B751563E86FF82AF8965932D171D94D01E9B13178A326C42C0A";
    public const string RealFaultHash = "DFBCF1BBD82C3E2DDDC794DE9B63F4B635A98E0473E534661EB89096989C5716";
    public const string Schema = "lmax_demo_bbo_no_send_retirement_v1";

    public static string CertificatePath(string journalPath) => journalPath + ".bbo-no-send-retirement.json";

    public static LmaxDemoUnsentParentRetirementCertificate Prepare(string journalPath,
        string expectedJournalSha256, string approvalReference,
        LmaxDemoRetirementObservation observation, LmaxDemoUnsentParentDatabaseEvidence database,
        DateTimeOffset now, bool simulated = false)
    {
        using var journal = LmaxDemoSessionJournal.OpenForInspection(journalPath);
        var session = RequireEligibleJournal(journal, simulated);
        var hash = HashFile(journalPath);
        Require(hash.Equals(expectedJournalSha256, StringComparison.OrdinalIgnoreCase), "JOURNAL_HASH_CHANGED");
        var certificate = new LmaxDemoUnsentParentRetirementCertificate(Schema, session.Start.SessionId,
            session.Start.AccountId, approvalReference, now, hash, journal.Entries.Count,
            journal.Entries[^1].Sha256, journal.Entries.Single(x => x.Kind == "Fault").Sha256,
            observation, database);
        Validate(journalPath, journal, certificate, now, simulated);
        return certificate;
    }

    // The caller must hold the same exclusive account owner lock used by Begin.
    // The operator CLI acquires it before the final database check and this write.
    public static void WriteUnderOwnerLease(string journalPath, LmaxDemoUnsentParentRetirementCertificate certificate,
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
        var certificate = JsonSerializer.Deserialize<LmaxDemoUnsentParentRetirementCertificate>(File.ReadAllBytes(path))
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
            && entries.Count(x => x.Kind == "Cycle") == 1
            && entries.All(x => x.Kind is "Start" or "Inbound" or "Cycle" or "Fault")
            && session.KnownOrders.Count == 0 && session.FillDerivedPositions.All(x => x.Value == 0m)
            && session.BlockingReason == "PARENT_RECONCILIATION_REQUIRED"
            && JsonSerializer.Deserialize<string>(entries.Single(x => x.Kind == "Fault").Data) == "PARENT_RECONCILIATION_REQUIRED",
            "NOT_THE_NO_SEND_COORDINATOR_FAILURE");
        using var cycle = JsonDocument.Parse(entries.Single(x => x.Kind == "Cycle").Data);
        Require(cycle.RootElement.GetProperty("Id").GetString() == Guid.Parse(FailedModelRunId).ToString("N")
            && cycle.RootElement.GetProperty("Targets").EnumerateObject().Count() == 1
            && cycle.RootElement.GetProperty("Targets").GetProperty("EURUSD").GetDecimal() == -1000m,
            "EXACT_UNSENT_CYCLE_REQUIRED");
        if (!simulated)
            Require(session.Start.SessionId == RealSessionId && entries[0].Sha256 == RealStartHash
                && entries.Single(x => x.Kind == "Cycle") is { Index: 35, Sha256: "211551C7E36A5FE43A002CED29E613947E4C7E0D6D1E80B07C6C7A991757C2F5" }
                && entries.Single(x => x.Kind == "Fault") is { Index: 36, Sha256: RealFaultHash }, "EXACT_REAL_FAILURE_BINDING_REQUIRED");
        return session;
    }

    private static void Validate(string path, LmaxDemoSessionJournal journal,
        LmaxDemoUnsentParentRetirementCertificate c, DateTimeOffset now, bool simulated)
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
            && d.FailedRunTargets == 1 && d.FailedRunTradeIntents == 1 && d.FailedRunRiskDecisions == 1
            && d.NewParentOrders == 1 && d.NewChildOrders == 1 && d.NewExecutionReports == 0 && d.NewFills == 0
            && d.OpenChildOrders == 0 && d.NonZeroPositions == 0
            && d.ParentOrderId == "20c4d977-05e4-46b5-8dc3-34c887ed26a4"
            && d.ChildOrderId == "dd4194b6-01a0-4561-9708-a801b3973bd6"
            && d.ParentStatus == "Expired" && d.ChildStatus == "Expired"
            && d.Disposition == "LOCAL_PRE_SEND_DEADLINE_EXPIRED"
            && d.DispositionAtUtc > DateTimeOffset.Parse("2026-09-17T16:30:00Z")
            && d.DispositionAtUtc >= last.AtUtc && d.DispositionAtUtc <= d.VerifiedAtUtc
            && d.VerifiedAtUtc.Offset == TimeSpan.Zero && d.VerifiedAtUtc >= last.AtUtc
            && d.VerifiedAtUtc <= c.RetiredAtUtc && c.RetiredAtUtc - d.VerifiedAtUtc <= TimeSpan.FromSeconds(60),
            "CURRENT_ZERO_SEND_DATABASE_RECONCILIATION_REQUIRED");
    }

    public static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Require(bool condition, string code) { if (!condition) throw Error(code); }
    private static InvalidOperationException Error(string code) => new("DEMO_BBO_NO_SEND_RETIREMENT_" + code);
}
