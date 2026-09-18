using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.SqlServer;
using Retirement = QQ.Production.Intraday.Application.LmaxDemoRecoveredSendRetirement;

internal static class RetireRecoveredSession
{
    internal sealed record ObservationBundle(LmaxDemoRetirementObservation Observation, string CapturePath, string CaptureSha256, string SourceUrl);
    internal static async Task Run(string planPath, string expectedPlanHash, string? observationPath)
    {
        Require(Path.GetFullPath(planPath).StartsWith(@"D:\data\lmax-eod\recoveries\", StringComparison.OrdinalIgnoreCase), "PLAN_PATH_INVALID");
        var envelope = JsonSerializer.Deserialize<RecoveryEnvelope>(File.ReadAllBytes(planPath)) ?? throw new InvalidOperationException("RECOVERY_PLAN_REQUIRED");
        var plan = envelope.Plan;
        Require(expectedPlanHash == Retirement.PlanSha256 && plan.Sha256() == expectedPlanHash && envelope.PlanSha256 == expectedPlanHash,
            "EXACT_AUDITED_RECOVERY_REQUIRED");
        var journalPath = Path.Combine(LmaxDemoSessionOwnership.RealAccountRoot, Retirement.SessionId + ".journal.jsonl");
        Require(Retirement.HashFile(journalPath) == Retirement.JournalSha256 && envelope.Input.JournalPath == journalPath
            && Retirement.HashFile(envelope.Input.ReportPath) == plan.Request.ReportSha256, "RECOVERY_SOURCE_CHANGED");
        using var owner = new FileStream(Path.Combine(LmaxDemoSessionOwnership.RealAccountRoot, "owner.lock"), FileMode.Open, FileAccess.Read, FileShare.None);
        using var daily = new DailyLease();
        await using var db = new IntradayDbContext(new DbContextOptionsBuilder<IntradayDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=QQProductionIntraday;Integrated Security=true;TrustServerCertificate=true;Application Name=QQ84RecoveredSendRetirement").Options);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var state = await new SqlServerIntradayRepository(db).LoadStateAsync(default);
        LmaxDemoOfficialExecutionRecovery.VerifyApplied(state, plan);
        var audit = state.OperatorAuditEvents.Single(x => x.Id.Value == plan.RecoveryId);
        var authoritative = audit;
        if (LmaxDemoOfficialExecutionRecovery.IsKnownAuditProjectionDefect(audit, plan))
            authoritative = state.OperatorAuditEvents.Single(x => x.Id.Value == LmaxDemoOfficialExecutionRecovery.AuditCorrectionId(plan.RecoveryId)
                && x.Source == LmaxDemoOfficialExecutionRecovery.AuditCorrectionSource);
        Require(audit.Source == LmaxDemoOfficialRecoveryPlan.Source && audit.Result == OperatorAuditResult.Succeeded
            && authoritative.Result == OperatorAuditResult.Succeeded && authoritative.BeforeJson == JsonSerializer.Serialize(plan.Before)
            && authoritative.AfterJson == JsonSerializer.Serialize(plan), "RECOVERY_AUDIT_NOT_VERIFIED");
        var account = state.BrokerAccounts.Single(x => x.IsEnabled && x.ExternalAccountId == plan.Request.AccountId);
        Require(state.BrokerAccounts.Count(x => x.IsEnabled) == 1 && account.AccountCode == "LMAX_DEMO_LOCAL", "EXACT_ACCOUNT_REQUIRED");
        var runs = state.EodReconciliationRuns.Where(x => x.BrokerAccountId == account.Id && x.ReportDate == plan.Request.ReportDate).ToArray();
        var latest = runs.OrderByDescending(x => x.CreatedAtUtc).First();
        var runIds = runs.Select(x => x.Id).ToHashSet();
        var evidence = new LmaxDemoRecoveredSendDatabaseEvidence(account.AccountCode, plan.Request.ModelRunId.Value.ToString("D"),
            plan.RecoveryId, plan.Sha256(), LmaxDemoOfficialExecutionRecovery.Hash(JsonSerializer.Serialize(authoritative)), DateTimeOffset.UtcNow,
            latest.Id, true, true, plan.Fills.Count, plan.Ledger.Count, plan.Ledger.Sum(x => x.BaseQuantityDelta),
            state.ChildOrders.Count(x => x.Status is not (OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Rejected or OrderStatus.Expired)),
            state.PositionLedger.Where(x => x.FundId == account.FundId).GroupBy(x => x.InstrumentId).Count(g => g.Sum(x => x.BaseQuantityDelta) != 0m),
            state.EodReconciliationBreaks.Count(x => runIds.Contains(x.RunId) && x.Status != ReconciliationBreakStatus.Resolved),
            state.ExecutionReports.Count(x => x.ChildOrderId == plan.RecoveredChild.Id));
        if (observationPath is null)
        {
            await tx.RollbackAsync();
            Console.WriteLine(JsonSerializer.Serialize(new { marker = "RECOVERED_SESSION_DATABASE_INSPECTED", evidence,
                officialAccountObservation = false, retirementPerformed = false, databaseWrites = 0, tradingStarted = false }));
            return;
        }
        Require(Path.GetFullPath(observationPath).StartsWith(@"D:\data\", StringComparison.OrdinalIgnoreCase), "OBSERVATION_PATH_INVALID");
        var observation = JsonSerializer.Deserialize<ObservationBundle>(File.ReadAllBytes(observationPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("OFFICIAL_OBSERVATION_REQUIRED");
        Require(new Uri(observation.SourceUrl).GetLeftPart(UriPartial.Authority) == "https://web-order.london-demo.lmax.com"
            && Path.GetFullPath(observation.CapturePath).StartsWith(@"D:\data\lmax-demo-ui\", StringComparison.OrdinalIgnoreCase), "OFFICIAL_DEMO_UI_CAPTURE_REQUIRED");
        var certificate = Retirement.Prepare(journalPath, plan.Request.OwnerAuthorizationReference, observation.Observation,
            observation.CapturePath, observation.CaptureSha256, evidence, DateTimeOffset.UtcNow);
        // No accounting mutation is needed. Keep the read transaction and owner lease
        // through the certificate write, so concurrent mutation cannot race validation.
        Retirement.WriteUnderOwnerLease(journalPath, certificate, DateTimeOffset.UtcNow);
        using var journal = LmaxDemoSessionJournal.OpenForInspection(journalPath);
        Require(Retirement.IsValidated(journalPath, journal, DateTimeOffset.UtcNow), "CERTIFICATE_READBACK_FAILED");
        await tx.RollbackAsync();
        Console.WriteLine(JsonSerializer.Serialize(new { marker = "EXACT_RECOVERED_SEND_SESSION_RETIRED",
            certificatePath = Retirement.CertificatePath(journalPath), certificateSha256 = Retirement.HashFile(Retirement.CertificatePath(journalPath)),
            journalUnchanged = true, databaseWrites = 0, fixReportsInvented = 0, tradingStarted = false }));
    }
    private static void Require(bool ok, string code) { if (!ok) throw new InvalidOperationException(code); }
}
