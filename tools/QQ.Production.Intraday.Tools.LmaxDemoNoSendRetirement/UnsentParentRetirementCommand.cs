using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using Retirement = QQ.Production.Intraday.Application.LmaxDemoUnsentParentRetirement;

namespace QQ.Production.Intraday.Tools.LmaxDemoNoSendRetirement;

// No broker client exists in this command. Expiry is an explicitly audited local
// disposition of exact unsent internal records, never a fabricated venue report.
internal static class UnsentParentRetirementCommand
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    internal static async Task<int> Run(string[] args)
    {
        string Arg(string key)
        {
            var indexes = Enumerable.Range(0, args.Length).Where(i => args[i] == key).ToArray();
            if (indexes.Length != 1 || indexes[0] + 1 >= args.Length) throw new InvalidOperationException("ARGUMENT_REQUIRED:" + key);
            return args[indexes[0] + 1];
        }
        var root = LmaxDemoSessionOwnership.RealAccountRoot;
        var path = Path.Combine(root, Retirement.RealSessionId + ".journal.jsonl");
        foreach (var file in new[] { root, path, Path.Combine(root, "owner.lock"), Arg("--observation") })
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("REPARSE_POINT_REJECTED");
        using var lease = new FileStream(Path.Combine(root, "owner.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var journal = LmaxDemoSessionJournal.OpenForInspection(path);
        var session = Retirement.RequireEligibleJournal(journal);
        var observation = JsonSerializer.Deserialize<LmaxDemoRetirementObservation>(File.ReadAllBytes(Arg("--observation")), Json)
            ?? throw new InvalidOperationException("OBSERVATION_REQUIRED");
        await using var connection = new SqlConnection(@"Server=(localdb)\MSSQLLocalDB;Database=QQProductionIntraday;Integrated Security=true;TrustServerCertificate=true;Application Name=QQ84ExactUnsentLocalExpiry");
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        var now = DateTimeOffset.UtcNow;
        var before = await ReadDatabase(connection, transaction, session.Start.ObservedAtUtc, now);
        if (before.ParentStatus != "Created" || before.ChildStatus != "PendingNew" || before.OpenChildOrders != 1)
            throw new InvalidOperationException("EXACT_PRE_SEND_INTERNAL_STATES_REQUIRED");
        // Validate every journal, observation and database condition BEFORE any write.
        var proposed = before with { ParentStatus = "Expired", ChildStatus = "Expired", OpenChildOrders = 0 };
        _ = Retirement.Prepare(path, Arg("--expected-journal-sha256"), Arg("--approval-reference"), observation, proposed, DateTimeOffset.UtcNow);
        var intentPath = path + ".local-expiry-intent.json";
        using (var audit = new FileStream(intentPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            audit.Write(JsonSerializer.SerializeToUtf8Bytes(new { marker = "LOCAL_PRE_SEND_EXPIRY_INTENT", atUtc = now,
                journalSha256 = Retirement.HashFile(path), ownerApprovalReference = Arg("--approval-reference"), before,
                after = proposed, brokerConnectionOpened = false, executionReportsInvented = 0 }, Json));
            audit.Flush(true);
        }
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandTimeout = 20;
            update.CommandText = """
                UPDATE ChildOrders SET Status=@expired
                WHERE Id='97abae50-3e10-4bcc-a846-f830ca0c2faa'
                  AND ParentOrderId='ffaf6e28-a6ef-46fc-988f-c93675725534' AND Status=@pending;
                IF @@ROWCOUNT<>1 THROW 51000, 'EXACT_CHILD_EXPIRY_FAILED', 1;
                UPDATE ParentOrders SET Status=@expired
                WHERE Id='ffaf6e28-a6ef-46fc-988f-c93675725534'
                  AND TradeIntentId='d5ab6876-2b78-497d-9a2b-6db62d44b6f7' AND Status=@created;
                IF @@ROWCOUNT<>1 THROW 51000, 'EXACT_PARENT_EXPIRY_FAILED', 1;
                """;
            update.Parameters.AddWithValue("@expired", (int)OrderStatus.Expired);
            update.Parameters.AddWithValue("@pending", (int)OrderStatus.PendingNew);
            update.Parameters.AddWithValue("@created", (int)OrderStatus.Created);
            await update.ExecuteNonQueryAsync();
        }
        var after = await ReadDatabase(connection, transaction, session.Start.ObservedAtUtc, now);
        var certificate = Retirement.Prepare(path, Arg("--expected-journal-sha256"), Arg("--approval-reference"), observation, after, DateTimeOffset.UtcNow);
        await transaction.CommitAsync();
        // If certificate creation fails after commit, ownership stays blocked. Never retry automatically.
        Retirement.WriteUnderOwnerLease(path, certificate, DateTimeOffset.UtcNow);
        using var check = LmaxDemoSessionJournal.OpenForInspection(path);
        if (!Retirement.IsValidated(path, check, DateTimeOffset.UtcNow)) throw new InvalidOperationException("RETIREMENT_READBACK_FAILED");
        Console.WriteLine(JsonSerializer.Serialize(new { marker = "EXACT_UNSENT_PARENT_RETIRED", certificate,
            certificatePath = Retirement.CertificatePath(path), intentPath, originalJournalChanged = false,
            databaseRowsLocallyExpired = 2, brokerConnectionOpened = false, brokerReportsInvented = 0, newSessionStarted = false }, Json));
        return 0;
    }

    private static async Task<LmaxDemoUnsentParentDatabaseEvidence> ReadDatabase(SqlConnection connection,
        SqlTransaction transaction, DateTimeOffset observedAt, DateTimeOffset dispositionAt)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = 20;
        command.CommandText = """
            SELECT b.AccountCode, m.Id, m.IsProcessed,
              (SELECT COUNT(*) FROM TargetWeights WHERE ModelRunId=m.Id) AS WeightCount,
              (SELECT COUNT(*) FROM TargetPositions WHERE ModelRunId=m.Id) AS Targets,
              (SELECT COUNT(*) FROM TradeIntents WHERE ModelRunId=m.Id) AS Intents,
              (SELECT COUNT(*) FROM RiskDecisions WHERE ModelRunId=m.Id) AS RiskDecisions,
              (SELECT COUNT(*) FROM ParentOrders p JOIN TradeIntents t ON t.Id=p.TradeIntentId
                 WHERE t.FundId=f.Id AND p.CreatedAtUtc>=@start) AS NewParents,
              (SELECT COUNT(*) FROM ChildOrders c JOIN ParentOrders p ON p.Id=c.ParentOrderId
                 JOIN TradeIntents t ON t.Id=p.TradeIntentId WHERE t.FundId=f.Id AND c.CreatedAtUtc>=@start) AS NewChildren,
              (SELECT COUNT(*) FROM ExecutionReports r JOIN ChildOrders c ON c.Id=r.ChildOrderId
                 JOIN ParentOrders p ON p.Id=c.ParentOrderId JOIN TradeIntents t ON t.Id=p.TradeIntentId
                 WHERE t.FundId=f.Id AND r.ReceivedAtUtc>=@start) AS NewReports,
              (SELECT COUNT(*) FROM Fills r JOIN ChildOrders c ON c.Id=r.ChildOrderId
                 JOIN ParentOrders p ON p.Id=c.ParentOrderId JOIN TradeIntents t ON t.Id=p.TradeIntentId
                 WHERE t.FundId=f.Id AND r.ReceivedAtUtc>=@start) AS NewFills,
              (SELECT COUNT(*) FROM ChildOrders c JOIN ParentOrders p ON p.Id=c.ParentOrderId
                 JOIN TradeIntents t ON t.Id=p.TradeIntentId WHERE t.FundId=f.Id
                 AND c.Status NOT IN (@filled,@cancelled,@rejected,@expired)) AS OpenChildren,
              (SELECT COUNT(*) FROM (SELECT InstrumentId FROM PositionLedgerEvents
                 WHERE FundId=f.Id GROUP BY InstrumentId HAVING SUM(BaseQuantityDelta)<>0) positions) AS NonZeroPositions, p.Id, c.Id, p.Status, c.Status, c.VenueQuantity, t.RequestedVenueQuantity
            FROM Funds f JOIN BrokerAccounts b ON b.FundId=f.Id JOIN ModelRuns m ON m.FundId=f.Id
            JOIN TradeIntents t ON t.ModelRunId=m.Id JOIN ParentOrders p ON p.TradeIntentId=t.Id
            JOIN ChildOrders c ON c.ParentOrderId=p.Id
            WHERE f.Name='QQ Intraday Fund' AND f.IsEnabled=1 AND b.AccountCode='LMAX_DEMO_LOCAL'
              AND b.IsEnabled=1 AND m.Id=@model AND m.ModelName='IntradayFxModel'
              AND m.AsOfUtc='2026-09-17T15:15:00+00:00' AND m.EffectiveAtUtc='2026-09-17T15:30:00+00:00' AND m.NavUsd=1000000
              AND t.Id='d5ab6876-2b78-497d-9a2b-6db62d44b6f7'
              AND p.Id='ffaf6e28-a6ef-46fc-988f-c93675725534'
              AND c.Id='97abae50-3e10-4bcc-a846-f830ca0c2faa'
            """;
        command.Parameters.AddWithValue("@start", observedAt);
        command.Parameters.AddWithValue("@model", Guid.Parse(Retirement.FailedModelRunId));
        command.Parameters.AddWithValue("@filled", (int)OrderStatus.Filled);
        command.Parameters.AddWithValue("@cancelled", (int)OrderStatus.Cancelled);
        command.Parameters.AddWithValue("@rejected", (int)OrderStatus.Rejected);
        command.Parameters.AddWithValue("@expired", (int)OrderStatus.Expired);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("EXACT_UNSENT_MODEL_RUN_NOT_FOUND");
        if (reader.GetDecimal(17) != 2m || reader.GetDecimal(18) != 2m)
            throw new InvalidOperationException("EXACT_UNSENT_QUANTITY_REQUIRED");
        var evidence = new LmaxDemoUnsentParentDatabaseEvidence(DateTimeOffset.UtcNow, reader.GetString(0),
            reader.GetGuid(1).ToString(), reader.GetBoolean(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5),
            reader.GetInt32(6), reader.GetInt32(7), reader.GetInt32(8), reader.GetInt32(9), reader.GetInt32(10),
            reader.GetInt32(11), reader.GetInt32(12), reader.GetGuid(13).ToString(), reader.GetGuid(14).ToString(),
            ((OrderStatus)reader.GetInt32(15)).ToString(), ((OrderStatus)reader.GetInt32(16)).ToString(),
            "LOCAL_PRE_SEND_DEADLINE_EXPIRED", dispositionAt);
        if (await reader.ReadAsync()) throw new InvalidOperationException("AMBIGUOUS_DATABASE_ACCOUNT_SCOPE");
        return evidence;
    }
}
