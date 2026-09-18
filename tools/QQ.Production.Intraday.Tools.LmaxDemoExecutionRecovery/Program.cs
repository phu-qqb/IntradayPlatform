using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.SqlServer;

// Historical Demo accounting only. No broker client, secret, migration, journal
// append, lock takeover, session retirement, or trading-start capability.
if (Environment.MachineName != "EC2AMAZ-1QPHTD8" || Environment.UserName != "Administrator")
    throw new InvalidOperationException("RECOVERY_DEMO_HOST_OWNER_REQUIRED");
if (args.Length != 2 && args.Length != 4) throw new ArgumentException("plan INPUT_JSON | apply/verify PLAN_JSON EXPECTED_PLAN_SHA256 NEW_RECEIPT_JSON");
if (args[0] == "plan" && args.Length == 2) await Plan(args[1]);
else if ((args[0] == "apply" || args[0] == "verify") && args.Length == 4) await Apply(args[1], args[2], args[3], args[0] == "apply");
else throw new ArgumentException("RECOVERY_INVALID_COMMAND");

static string PathUnder(string path, string root)
{
    var full = Path.GetFullPath(path);
    if (!full.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || path.Contains(".."))
        throw new InvalidOperationException("RECOVERY_PATH_OUTSIDE_SCOPE");
    return full;
}
static string PrivatePath(string path) => PathUnder(path, @"D:\data\lmax-eod");
static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
static IntradayDbContext Database() => new(new DbContextOptionsBuilder<IntradayDbContext>()
    .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=QQProductionIntraday;Integrated Security=true;TrustServerCertificate=true;Application Name=QQ84OfficialExecutionRecovery").Options);
static void WriteNew<T>(string path, T value)
{
    using var file = new FileStream(PrivatePath(path), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
    JsonSerializer.Serialize(file, value, new JsonSerializerOptions { WriteIndented = true });
    file.Flush(true);
}
static FileStream OwnerLease() => new(Path.Combine(LmaxDemoSessionOwnership.RealAccountRoot, "owner.lock"), FileMode.Open, FileAccess.Read, FileShare.None);
static string JournalPath(string path, LmaxDemoOfficialRecoveryRequest request)
{
    var full = PathUnder(path, LmaxDemoSessionOwnership.RealAccountRoot);
    if (Path.GetFileName(full) != request.SessionId + ".journal.jsonl") throw new InvalidOperationException("RECOVERY_JOURNAL_SESSION_MISMATCH");
    return full;
}
static void VerifySources(RecoveryInput input)
{
    if (HashFile(PrivatePath(input.ReportPath)) != input.Request.ReportSha256
        || HashFile(JournalPath(input.JournalPath, input.Request)) != input.Request.JournalSha256)
        throw new InvalidOperationException("RECOVERY_SOURCE_HASH_MISMATCH");
}
static async Task<LmaxDemoOfficialRecoveryPlan> Prepare(IntradayDbContext db, RecoveryInput input)
{
    VerifySources(input);
    var repo = new SqlServerIntradayRepository(db);
    var state = await repo.LoadStateAsync(default);
    // Parse the exact pinned file into a fresh report store. Existing DB rows must
    // agree field-for-field apart from import bookkeeping, not merely RawLine.
    var previewState = new PlatformState();
    var memory = new InMemoryLmaxEodReportRepository(previewState);
    var clock = new SystemClock();
    var options = new LmaxEodReportOptions { DataRoot = @"D:\data\lmax-eod" };
    var importer = new LmaxEodReportImportService(repo, memory, new LmaxReportPairConsistencyService(memory, clock, options), clock, options);
    var imported = await importer.ImportIndividualTradesAsync(input.ReportPath, input.Request.ReportDate, "LMAX", "LMAX_DEMO_LOCAL", default);
    if (imported.BlockingIssueCount != 0) throw new InvalidOperationException("RECOVERY_OFFICIAL_SOURCE_INVALID");
    var preview = await memory.GetIndividualTradesAsync(input.Request.ReportDate, 500, default);
    var official = state.LmaxIndividualTrades.Where(x => x.ReportDate == input.Request.ReportDate && x.AccountId == input.Request.AccountId).ToArray();
    if (preview.Count != 4 || official.Length != 4) throw new InvalidOperationException("RECOVERY_SOURCE_SET_MISMATCH");
    foreach (var row in official)
    {
        var parsed = preview.Single(x => x.ExecutionId == row.ExecutionId);
        if (row != parsed with { Id = row.Id, ImportRunId = row.ImportRunId, CreatedAtUtc = row.CreatedAtUtc })
            throw new InvalidOperationException("RECOVERY_IMPORTED_ROW_DIFFERS_FROM_PINNED_FILE");
    }
    using var journal = LmaxDemoSessionJournal.OpenForInspection(input.JournalPath);
    return LmaxDemoOfficialExecutionRecovery.Prepare(state, input.Request, official, LmaxDemoControlledSession.Inspect(journal), journal.Entries, clock.UtcNow);
}
static async Task Plan(string path)
{
    var input = JsonSerializer.Deserialize<RecoveryInput>(File.ReadAllText(PrivatePath(path))) ?? throw new InvalidOperationException("RECOVERY_INPUT_INVALID");
    if (File.Exists(PrivatePath(input.PlanPath))) throw new InvalidOperationException("RECOVERY_PLAN_EXISTS");
    using var owner = OwnerLease();
    using var daily = new DailyLease();
    await using var db = Database();
    await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    var plan = await Prepare(db, input);
    VerifySources(input);
    await tx.RollbackAsync(); // Plan mode cannot leave any database mutation.
    var envelope = new RecoveryEnvelope(input, plan, plan.Sha256());
    WriteNew(input.PlanPath, envelope);
    Console.WriteLine(JsonSerializer.Serialize(new { status = "PLAN_ONLY", recovery_id = plan.RecoveryId, plan_sha256 = envelope.PlanSha256,
        official_rows = plan.OfficialRows.Count, fills_to_add = plan.Fills.Count, manual_bookings_to_add = 1,
        ledger_net_delta = plan.Ledger.Sum(x => x.BaseQuantityDelta), historical_breaks_to_resolve = plan.Before.OpenBreaks.Count,
        fix_reports_to_add = 0, journal_mutation = false, trading_started = false }));
}
static async Task Apply(string path, string expectedHash, string receiptPath, bool commit)
{
    var envelope = JsonSerializer.Deserialize<RecoveryEnvelope>(File.ReadAllText(PrivatePath(path))) ?? throw new InvalidOperationException("RECOVERY_PLAN_INVALID");
    var plan = envelope.Plan;
    if (expectedHash != envelope.PlanSha256 || expectedHash != plan.Sha256()
        || JsonSerializer.Serialize(envelope.Input.Request) != JsonSerializer.Serialize(plan.Request))
        throw new InvalidOperationException("RECOVERY_REVIEWED_PLAN_HASH_REQUIRED");
    if (File.Exists(PrivatePath(receiptPath))) throw new InvalidOperationException("RECOVERY_RECEIPT_EXISTS");
    using var owner = OwnerLease(); // Existing lock contents are never changed.
    using var daily = new DailyLease();
    // Read through our exclusive read-only handle; another process cannot start.
    var ownerHash = Convert.ToHexString(SHA256.HashData(owner));
    await using var db = Database();
    await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    VerifySources(envelope.Input);
    var repo = new SqlServerIntradayRepository(db);
    var priorAudit = await db.OperatorAuditEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == new OperatorAuditEventId(plan.RecoveryId));
    if (priorAudit is not null)
    {
        if (priorAudit.Source != LmaxDemoOfficialRecoveryPlan.Source || priorAudit.AfterJson != JsonSerializer.Serialize(plan)
            || priorAudit.Result != OperatorAuditResult.Succeeded) throw new InvalidOperationException("RECOVERY_AUDIT_CONFLICT");
        LmaxDemoOfficialExecutionRecovery.VerifyApplied(await repo.LoadStateAsync(default), plan);
        await tx.RollbackAsync();
        WriteNew(receiptPath, new { status = "ALREADY_APPLIED_VERIFIED", recovery_id = plan.RecoveryId, plan_sha256 = expectedHash,
            at_utc = DateTimeOffset.UtcNow, database_mutation = false, trading_started = false });
        Console.WriteLine("ALREADY_APPLIED_VERIFIED");
        return;
    }
    var current = await Prepare(db, envelope.Input);
    if (current.Sha256() != expectedHash) throw new InvalidOperationException("RECOVERY_PLAN_STALE_OR_STATE_CHANGED");
    Set(plan.Before.Model, plan.RecoveredModel);
    Set(plan.Before.Intent, plan.RecoveredIntent);
    Set(plan.Before.Parent, plan.RecoveredParent);
    Set(plan.Before.Child, plan.RecoveredChild);
    db.TradeIntents.Add(plan.ManualBooking);
    db.ParentOrders.Add(plan.ManualParent);
    db.ChildOrders.Add(plan.ManualChild);
    db.Fills.AddRange(plan.Fills);
    db.PositionLedgerEvents.AddRange(plan.Ledger);
    await db.SaveChangesAsync();
    var reconciled = await new EodReconciliationService(repo, new SqlServerLmaxEodReportRepository(db), new SystemClock())
        .RunAsync(plan.Request.ReportDate, "LMAX", "LMAX_DEMO_LOCAL", default);
    if (reconciled.BreakCount != 0) throw new InvalidOperationException("RECOVERY_RECONCILIATION_NOT_CLEAN_ROLLBACK");
    foreach (var old in plan.Before.OpenBreaks) Set(old, old with { Status = ReconciliationBreakStatus.Resolved });
    var metadata = JsonSerializer.Serialize(new { plan_sha256 = expectedHash, reconciliation_run_id = reconciled.RunId,
        blocking_breaks = 0, recovery_source = "AUTHENTIC_OFFICIAL_REPORT_NOT_FIX", source_report_sha256 = plan.Request.ReportSha256,
        source_journal_sha256 = plan.Request.JournalSha256, owner_lock_sha256 = ownerHash.ToLowerInvariant(),
        historical_break_ids = plan.Before.OpenBreaks.Select(x => x.Id),
        resolved_breaks = plan.Before.OpenBreaks.Select(x => x with { Status = ReconciliationBreakStatus.Resolved }),
        current_account_observation = false, session_retired = false, trading_started = false });
    db.OperatorAuditEvents.Add(new(new(plan.RecoveryId), DateTimeOffset.UtcNow, OperatorAuditActorType.Operator,
        "philippe-authorized-demo-recovery", "Philippe-authorized automated Demo recovery", OperatorAuditEventType.OfficialExecutionRecovered,
        OperatorAuditSeverity.Warning, OperatorAuditResult.Succeeded, "ModelRun", plan.Request.ModelRunId.Value.ToString("D"),
        plan.RecoveryId.ToString("D"), plan.Request.SessionId, null, LmaxDemoOfficialRecoveryPlan.Source,
        "Recovered three sent-order fills and one external manual close from the pinned authentic official report; no FIX reports created.",
        plan.Request.OwnerAuthorizationReference, JsonSerializer.Serialize(plan.Before), JsonSerializer.Serialize(plan), metadata));
    await db.SaveChangesAsync();
    LmaxDemoOfficialExecutionRecovery.VerifyApplied(await repo.LoadStateAsync(default), plan);
    VerifySources(envelope.Input);
    owner.Position = 0;
    if (Convert.ToHexString(SHA256.HashData(owner)) != ownerHash) throw new InvalidOperationException("RECOVERY_OWNER_LOCK_CHANGED");
    if (commit) await tx.CommitAsync();
    else await tx.RollbackAsync();
    // Database audit is the durable receipt if the process dies before this file.
    var receipt = new { status = commit ? "APPLIED_AND_RECONCILED" : "TRANSACTION_VERIFIED_ROLLED_BACK", database_committed = commit, recovery_id = plan.RecoveryId, plan_sha256 = expectedHash,
        at_utc = DateTimeOffset.UtcNow, reconciliation_run_id = reconciled.RunId, blocking_breaks = 0,
        recovered_fills = plan.Fills.Count, ledger_events_added = plan.Ledger.Count, ledger_net_delta = plan.Ledger.Sum(x => x.BaseQuantityDelta),
        manual_bookings_added = 1, historical_breaks_resolved = plan.Before.OpenBreaks.Count,
        source = LmaxDemoOfficialRecoveryPlan.Source, fix_reports_created = 0,
        journal_mutation = false, owner_lock_mutation = false, session_retired = false, trading_started = false };
    WriteNew(receiptPath, receipt);
    Console.WriteLine(JsonSerializer.Serialize(receipt));

    void Set<T>(T before, T after) where T : class
    {
        db.Attach(before);
        db.Entry(before).CurrentValues.SetValues(after);
    }
}
record RecoveryInput(LmaxDemoOfficialRecoveryRequest Request, string ReportPath, string JournalPath, string PlanPath);
record RecoveryEnvelope(RecoveryInput Input, LmaxDemoOfficialRecoveryPlan Plan, string PlanSha256);
sealed class DailyLease : IDisposable
{
    private const string Path = @"D:\data\lmax-eod\daily-eod.lock";
    private readonly FileStream stream = new(Path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
    public void Dispose() { stream.Dispose(); File.Delete(Path); } // Only our own successfully acquired lock.
}
