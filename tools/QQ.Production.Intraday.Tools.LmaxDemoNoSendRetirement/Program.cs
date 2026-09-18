using System.Text.Json;
using Microsoft.Data.SqlClient;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using Retirement = QQ.Production.Intraday.Application.LmaxDemoNoSendRetirement;

namespace QQ.Production.Intraday.Tools.LmaxDemoNoSendRetirement;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    internal static async Task<int> Main(string[] args)
    {
        try
        {
            if (!OperatingSystem.IsWindows() || Environment.MachineName != "EC2AMAZ-1QPHTD8"
                || !Environment.UserName.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("EXACT_DEMO_OPERATOR_REQUIRED");
            var mode = args.FirstOrDefault() ?? "inspect";
            if (mode == "retire-bbo-no-send") return await BboNoSendRetirementCommand.Run(args);
            if (mode == "retire-unsent-parent") return await UnsentParentRetirementCommand.Run(args);
            if (mode == "retire-duplicate-id") return await DuplicateOrderRetirementCommand.Run(args);
            if (mode is not ("inspect" or "retire")) throw new InvalidOperationException("UNKNOWN_MODE");
            string Arg(string key)
            {
                var indexes = Enumerable.Range(0, args.Length).Where(i => args[i] == key).ToArray();
                if (indexes.Length != 1 || indexes[0] + 1 >= args.Length) throw new InvalidOperationException("ARGUMENT_REQUIRED:" + key);
                return args[indexes[0] + 1];
            }
            var root = LmaxDemoSessionOwnership.RealAccountRoot;
            var journalPath = Path.Combine(root, Retirement.RealSessionId + ".journal.jsonl");
            foreach (var path in new[] { root, journalPath, Path.Combine(root, "owner.lock") })
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("REPARSE_POINT_REJECTED");
            // Retirement cannot run while the original Worker (or any other owner) holds this lock.
            using var owner = mode == "retire"
                ? new FileStream(Path.Combine(root, "owner.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                : null;
            using var journal = LmaxDemoSessionJournal.OpenForInspection(journalPath);
            var session = Retirement.RequireEligibleJournal(journal);
            var database = await ReadDatabase(session.Start.ObservedAtUtc);
            if (mode == "inspect")
            {
                Console.WriteLine(JsonSerializer.Serialize(new { marker = "NO_SEND_RETIREMENT_INSPECTION_ONLY",
                    sessionId = session.Start.SessionId, eligibleJournal = true, database,
                    journalEntries = journal.Entries.Count, lastEntrySha256 = journal.Entries[^1].Sha256,
                    certificateExists = File.Exists(Retirement.CertificatePath(journalPath)),
                    retirementPerformed = false, workerStopped = false, brokerConnectionOpened = false, databaseWrites = 0 }, Json));
                return 0;
            }
            var observationPath = Path.GetFullPath(Arg("--observation"));
            if ((File.GetAttributes(observationPath) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("OBSERVATION_REPARSE_POINT_REJECTED");
            var observation = JsonSerializer.Deserialize<LmaxDemoRetirementObservation>(File.ReadAllBytes(observationPath), Json)
                ?? throw new InvalidOperationException("OBSERVATION_REQUIRED");
            var certificate = Retirement.Prepare(journalPath, Arg("--expected-journal-sha256"),
                Arg("--approval-reference"), observation, database, DateTimeOffset.UtcNow);
            Retirement.WriteUnderOwnerLease(journalPath, certificate, DateTimeOffset.UtcNow);
            using var check = LmaxDemoSessionJournal.OpenForInspection(journalPath);
            if (!Retirement.IsValidated(journalPath, check, DateTimeOffset.UtcNow))
                throw new InvalidOperationException("RETIREMENT_READBACK_FAILED");
            Console.WriteLine(JsonSerializer.Serialize(new { marker = "EXACT_NO_SEND_SESSION_RETIRED",
                certificate, certificatePath = Retirement.CertificatePath(journalPath),
                originalJournalChanged = false, workerStoppedByThisTool = false, brokerConnectionOpened = false,
                databaseWrites = 0, newSessionStarted = false }, Json));
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new { status = "Blocked", type = error.GetType().Name,
                code = error is InvalidOperationException ? error.Message : "RETIREMENT_PRECONDITION_OR_IO_FAILED",
                automaticRetryAllowed = false }));
            return 1;
        }
    }

    private static async Task<LmaxDemoRetirementDatabaseEvidence> ReadDatabase(DateTimeOffset observedAt)
    {
        await using var connection = new SqlConnection(@"Server=(localdb)\MSSQLLocalDB;Database=QQProductionIntraday;Integrated Security=true;TrustServerCertificate=true;Application Name=QQ84NoSendRetirementReadOnly");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
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
                 WHERE FundId=f.Id GROUP BY InstrumentId HAVING SUM(BaseQuantityDelta)<>0) positions) AS NonZeroPositions
            FROM Funds f JOIN BrokerAccounts b ON b.FundId=f.Id JOIN ModelRuns m ON m.FundId=f.Id
            WHERE f.Name='QQ Intraday Fund' AND f.IsEnabled=1 AND b.AccountCode='LMAX_DEMO_LOCAL'
              AND b.IsEnabled=1 AND m.Id=@model AND m.ModelName='IntradayFxModel'
              AND m.AsOfUtc='2026-09-17T13:00:00+00:00' AND m.NavUsd=1000000
            """;
        command.Parameters.AddWithValue("@start", observedAt);
        command.Parameters.AddWithValue("@model", Guid.Parse(Retirement.FailedModelRunId));
        command.Parameters.AddWithValue("@filled", (int)OrderStatus.Filled);
        command.Parameters.AddWithValue("@cancelled", (int)OrderStatus.Cancelled);
        command.Parameters.AddWithValue("@rejected", (int)OrderStatus.Rejected);
        command.Parameters.AddWithValue("@expired", (int)OrderStatus.Expired);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("EXACT_FAILED_MODEL_RUN_NOT_FOUND");
        var evidence = new LmaxDemoRetirementDatabaseEvidence(DateTimeOffset.UtcNow, reader.GetString(0),
            reader.GetGuid(1).ToString(), reader.GetBoolean(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5),
            reader.GetInt32(6), reader.GetInt32(7), reader.GetInt32(8), reader.GetInt32(9), reader.GetInt32(10),
            reader.GetInt32(11), reader.GetInt32(12));
        if (await reader.ReadAsync()) throw new InvalidOperationException("AMBIGUOUS_DATABASE_ACCOUNT_SCOPE");
        return evidence;
    }
}
