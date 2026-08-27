using System.Data;
using System.Net.Sockets;
using System.Security.Authentication;
using Npgsql;

namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

public sealed partial class RawLmaxFixSessionClient
{
    private sealed record PersistenceProbe(
        LmaxFixArch7bProductionReadinessPersistence Result,
        string? Blocker);

    private static readonly string[] RequiredArch7bLifecycleTables =
    [
        "arch7b_qualification_runs",
        "arch7b_fix_session_events",
        "arch7b_order_send_ledger",
        "arch7b_execution_reports",
        "arch7b_fills",
        "arch7b_position_ledger_events",
        "arch7b_final_reconciliations"
    ];

    public async Task<LmaxFixArch7bProductionReadinessResult> Arch7bProductionReadinessAsync(
        LmaxConnectivityLabOptions options,
        LmaxFixArch7bKnownOrderRequest request,
        bool explicitReadinessConfirmation,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var blockers = LmaxFixArch7bProductionReadinessContract.Validate(
            options, request, explicitReadinessConfirmation, startedAt);
        if (blockers.Count != 0)
            return Result("Skipped", new(false, false, false, false),
                new(false, false, false, false, false, false, false, false),
                new(false, false, false, false), blockers[0], blockers);

        var diagnostics = new List<string>();
        var persistenceProbe = await ValidatePersistenceReadinessAsync(request, cancellationToken, diagnostics);
        var persistence = persistenceProbe.Result;
        if (!persistence.RequiredSchemaPresent)
            return Result("Failed", persistence,
                new(false, false, false, false, false, false, false, false),
                new(false, false, false, false), persistenceProbe.Blocker ?? "ARCH7B_PRODUCTION_PERSISTENCE_READINESS_FAILED", diagnostics);

        var profile = request.ExecutionProfile;
        var marketDataOptions = CreateArch7bReadOnlyMarketDataOptions(options, profile);
        var marketData = await Arch7bProductionReadOnlyMarketDataSnapshotAsync(
            marketDataOptions, request, request.DeadlineUtc, explicitReadinessConfirmation, cancellationToken);
        var observation = LmaxFixArch7bKnownOrderContract.EvaluateFreshFlattenObservation(
            marketDataOptions,
            marketData,
            startedAt,
            DateTimeOffset.UtcNow,
            request.OpeningMarketObservationId,
            profile);
        var marketDataReadiness = new LmaxFixArch7bProductionReadinessMarketData(
            marketData.TcpConnected,
            marketData.TlsHandshakeCompleted,
            marketData.FixLoggedOn,
            marketData.MarketDataRequestSent,
            marketData.CompleteTopOfBook,
            observation.Allowed,
            marketData.InboundSequenceIntegrityProven,
            marketData.LogoutSent);
        if (!observation.Allowed)
        {
            diagnostics.AddRange(observation.Blockers);
            return Result("Failed", persistence, marketDataReadiness,
                new(false, false, false, false),
                "ARCH7B_PRODUCTION_MARKET_DATA_READINESS_FAILED", diagnostics);
        }

        var orderEntryBlockers = LmaxFixArch7bProductionReadinessContract.Validate(
            options, request, explicitReadinessConfirmation, DateTimeOffset.UtcNow);
        if (orderEntryBlockers.Count != 0)
        {
            diagnostics.AddRange(orderEntryBlockers);
            return Result("Failed", persistence, marketDataReadiness,
                new(false, false, false, false), orderEntryBlockers[0], diagnostics);
        }

        var orderEntry = await ValidateOrderEntryLogonReadinessAsync(options, cancellationToken, diagnostics);
        var ready = persistence.RequiredSchemaPresent &&
                    marketDataReadiness.TcpConnected && marketDataReadiness.TlsHandshakeCompleted &&
                    marketDataReadiness.FixLoggedOn && marketDataReadiness.MarketDataRequestSent &&
                    marketDataReadiness.BboReceived && marketDataReadiness.InstrumentValidated &&
                    marketDataReadiness.SequenceIntegrityValidated && marketDataReadiness.LoggedOut &&
                    orderEntry.TcpConnected && orderEntry.TlsHandshakeCompleted &&
                    orderEntry.FixLoggedOn && orderEntry.LoggedOut;
        return Result(ready ? "Ok" : "Failed", persistence, marketDataReadiness, orderEntry,
            ready ? null : "ARCH7B_PRODUCTION_ORDER_ENTRY_READINESS_FAILED", diagnostics);

        LmaxFixArch7bProductionReadinessResult Result(
            string status,
            LmaxFixArch7bProductionReadinessPersistence persistenceResult,
            LmaxFixArch7bProductionReadinessMarketData marketDataResult,
            LmaxFixArch7bProductionReadinessOrderEntry orderEntryResult,
            string? blocker,
            IReadOnlyList<string> resultDiagnostics)
            => new(
                "fix-arch7b-production-readiness",
                status,
                startedAt,
                DateTimeOffset.UtcNow,
                persistenceResult,
                marketDataResult,
                orderEntryResult,
                status == "Ok",
                blocker,
                resultDiagnostics);
    }

    private static async Task<PersistenceProbe> ValidatePersistenceReadinessAsync(
        LmaxFixArch7bKnownOrderRequest request,
        CancellationToken cancellationToken,
        ICollection<string> diagnostics)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            Arch7bPostgreSqlPersistenceTarget.ProductionConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            diagnostics.Add("ARCH7B_PRODUCTION_PERSISTENCE_CONNECTION_STRING_MISSING");
            return new(new(false, false, false, false), "ARCH7B_PRODUCTION_PERSISTENCE_CONNECTION_STRING_MISSING");
        }

        var bindingValidated = false;
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            Arch7bPostgreSqlPersistenceTarget.ValidateResolvedConnection(
                request, builder.Host, builder.Port, builder.Database);
            bindingValidated = true;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, cancellationToken);
            await using (var readOnly = connection.CreateCommand())
            {
                readOnly.Transaction = transaction;
                readOnly.CommandText = "SET TRANSACTION READ ONLY";
                await readOnly.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var selectOne = connection.CreateCommand())
            {
                selectOne.Transaction = transaction;
                selectOne.CommandText = "SELECT 1";
                if (await selectOne.ExecuteScalarAsync(cancellationToken) is not 1)
                    return new(new(true, true, false, false), "ARCH7B_PRODUCTION_PERSISTENCE_SELECT_ONE_FAILED");
            }
            foreach (var table in RequiredArch7bLifecycleTables)
            {
                await using var schemaProbe = connection.CreateCommand();
                schemaProbe.Transaction = transaction;
                schemaProbe.CommandText = $"SELECT 1 FROM pms_shadow.{table} LIMIT 0";
                await schemaProbe.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.RollbackAsync(CancellationToken.None);
            return new(new(true, true, true, true), null);
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException or ArgumentException or OperationCanceledException)
        {
            diagnostics.Add($"ARCH7B_PRODUCTION_PERSISTENCE_READINESS:{exception.GetType().Name}");
            var blocker = exception is InvalidOperationException { Message: var message } &&
                          message.StartsWith("ARCH7B_", StringComparison.Ordinal)
                ? message
                : "ARCH7B_PRODUCTION_PERSISTENCE_READINESS_FAILED";
            return new(new(bindingValidated, false, false, false), blocker);
        }
    }

    private static async Task<LmaxFixArch7bProductionReadinessOrderEntry> ValidateOrderEntryLogonReadinessAsync(
        LmaxConnectivityLabOptions options,
        CancellationToken cancellationToken,
        ICollection<string> diagnostics)
    {
        var tcpConnected = false;
        var tlsHandshakeCompleted = false;
        var fixLoggedOn = false;
        var loggedOut = false;
        try
        {
            using var tcp = new TcpClient();
            using (var connectTimeout = CreateTimeout(options.ConnectTimeoutSeconds, cancellationToken))
            {
                await tcp.ConnectAsync(options.FixOrderHost!, options.FixOrderPort!.Value, connectTimeout.Token);
                tcpConnected = true;
            }
            Stream rawStream;
            using (var tlsTimeout = CreateTimeout(options.ConnectTimeoutSeconds, cancellationToken))
                rawStream = await CreateTlsStreamAsync(tcp, options.FixOrderHost!, tlsTimeout.Token);
            await using var stream = rawStream;
            tlsHandshakeCompleted = true;
            var target = (options.FixOrderTargetCompId ?? options.FixTargetCompId)!;
            var logon = LmaxFixMarketDataCodec.BuildMessage("A", 1, options.FixSenderCompId!, target,
            [
                ("98", "0"),
                ("108", "30"),
                ("141", "Y"),
                ("553", options.FixUsername!),
                ("554", options.FixPassword!)
            ]);
            using (var logonTimeout = CreateTimeout(options.LogonTimeoutSeconds, cancellationToken))
            {
                await WriteAsciiAsync(stream, logon, logonTimeout.Token);
                var logonConfirmation = await ConfirmReadinessLogonAsync(
                    stream, options, target, logonTimeout.Token);
                fixLoggedOn = logonConfirmation.LoggedOn;
                if (fixLoggedOn)
                    loggedOut = await TrySendLogoutAsync(
                        stream,
                        options,
                        target,
                        logonConfirmation.NextOutboundSequenceNumber,
                        diagnostics,
                        "ProductionReadinessOrderEntry");
            }
        }
        catch (Exception exception) when (exception is SocketException or IOException or AuthenticationException or OperationCanceledException)
        {
            diagnostics.Add($"ARCH7B_PRODUCTION_ORDER_ENTRY_READINESS:{exception.GetType().Name}");
        }
        return new(tcpConnected, tlsHandshakeCompleted, fixLoggedOn, loggedOut);
    }

    private static async Task<(bool LoggedOn, int NextOutboundSequenceNumber)> ConfirmReadinessLogonAsync(
        Stream stream,
        LmaxConnectivityLabOptions options,
        string target,
        CancellationToken cancellationToken)
    {
        var sequenceNumber = 2;
        while (!cancellationToken.IsCancellationRequested)
        {
            var response = await ReadAnyFixMessageAsync(stream, cancellationToken);
            var messageType = LmaxFixMarketDataCodec.GetMsgType(response);
            if (messageType == "A")
                return (true, sequenceNumber);
            if (messageType == "5" || string.IsNullOrWhiteSpace(messageType))
                return (false, sequenceNumber);
            if (messageType == "1")
            {
                var testRequestId = LmaxFixMarketDataCodec.GetTag(response, "112");
                IReadOnlyList<(string Tag, string Value)> heartbeatFields = string.IsNullOrWhiteSpace(testRequestId)
                    ? []
                    : [("112", testRequestId)];
                var heartbeat = LmaxFixMarketDataCodec.BuildMessage(
                    "0", sequenceNumber++, options.FixSenderCompId!, target, heartbeatFields);
                await WriteAsciiAsync(stream, heartbeat, cancellationToken);
            }
        }
        return (false, sequenceNumber);
    }
}
