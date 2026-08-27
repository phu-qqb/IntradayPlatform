namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

/// <summary>
/// Read-only evidence gathered before a separately authorized production canary.
/// This is deliberately not an order-lifecycle result: it cannot authorize or send an order.
/// </summary>
public sealed record LmaxFixArch7bProductionReadinessPersistence(
    bool BindingValidated,
    bool Connected,
    bool SelectOneSucceeded,
    bool RequiredSchemaPresent);

public sealed record LmaxFixArch7bProductionReadinessMarketData(
    bool TcpConnected,
    bool TlsHandshakeCompleted,
    bool FixLoggedOn,
    bool MarketDataRequestSent,
    bool BboReceived,
    bool InstrumentValidated,
    bool SequenceIntegrityValidated,
    bool LoggedOut);

public sealed record LmaxFixArch7bProductionReadinessOrderEntry(
    bool TcpConnected,
    bool TlsHandshakeCompleted,
    bool FixLoggedOn,
    bool LoggedOut);

public sealed record LmaxFixArch7bProductionReadinessResult(
    string Command,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    LmaxFixArch7bProductionReadinessPersistence Persistence,
    LmaxFixArch7bProductionReadinessMarketData MarketData,
    LmaxFixArch7bProductionReadinessOrderEntry OrderEntry,
    bool ReadyForProductionCanary,
    string? Blocker,
    IReadOnlyList<string> Diagnostics)
{
    public bool ValidateOnly { get; init; }

    public bool ZeroIo =>
        !Persistence.Connected &&
        !MarketData.TcpConnected &&
        !OrderEntry.TcpConnected;

    public static LmaxFixArch7bProductionReadinessResult Skipped(string blocker)
    {
        var now = DateTimeOffset.UtcNow;
        return new(
            "fix-arch7b-production-readiness",
            "Skipped",
            now,
            now,
            new(false, false, false, false),
            new(false, false, false, false, false, false, false, false),
            new(false, false, false, false),
            false,
            blocker,
            []);
    }
}

/// <summary>
/// Non-secret immutable binding for infrastructure readiness. It is deliberately
/// separate from the trading authorization packet and contains no order economics.
/// </summary>
public sealed record LmaxFixArch7bProductionReadinessBinding(
    string EnvironmentName,
    string AccountId,
    string FixOrderHost,
    int FixOrderPort,
    string FixOrderTargetCompId,
    string FixSenderCompId,
    string FixMarketDataHost,
    int FixMarketDataPort,
    string FixMarketDataTargetCompId,
    string InstrumentSymbol,
    string SecurityId,
    string SecurityIdSource,
    string PersistenceHost,
    int PersistencePort,
    string PersistenceDatabase,
    string OperatorAuthorizationId,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset DeadlineUtc);

/// <summary>
/// Validates the separate, zero-order production readiness binding.
/// </summary>
public static class LmaxFixArch7bProductionReadinessContract
{
    public static IReadOnlyList<string> Validate(
        LmaxConnectivityLabOptions options,
        LmaxFixArch7bProductionReadinessBinding binding,
        bool explicitReadinessConfirmation,
        bool validateOnly,
        DateTimeOffset nowUtc)
    {
        var blockers = new List<string>();
        Require(explicitReadinessConfirmation, "ARCH7B_PRODUCTION_READINESS_CLI_CONFIRMATION_MISSING");
        Require(!string.IsNullOrWhiteSpace(options.AccountCode), "QQ_LMAX_ACCOUNT_CODE");
        Require(!string.IsNullOrWhiteSpace(options.FixOrderHost), "QQ_LMAX_FIX_ORDER_HOST");
        Require(options.FixOrderPort is > 0 and <= 65535, "QQ_LMAX_FIX_ORDER_PORT");
        Require(!string.IsNullOrWhiteSpace(options.FixOrderTargetCompId ?? options.FixTargetCompId),
            "QQ_LMAX_FIX_ORDER_TARGET_COMP_ID");
        Require(!string.IsNullOrWhiteSpace(options.FixMarketDataHost), "QQ_LMAX_FIX_MARKET_DATA_HOST");
        Require(options.FixMarketDataPort is > 0 and <= 65535, "QQ_LMAX_FIX_MARKET_DATA_PORT");
        Require(!string.IsNullOrWhiteSpace(options.FixMarketDataTargetCompId),
            "QQ_LMAX_FIX_MARKET_DATA_TARGET_COMP_ID");
        Require(!string.IsNullOrWhiteSpace(options.FixSenderCompId), "QQ_LMAX_FIX_SENDER_COMP_ID");
        Require(!string.IsNullOrWhiteSpace(options.InstrumentSymbol), "QQ_LMAX_INSTRUMENT_SYMBOL");
        Require(!string.IsNullOrWhiteSpace(options.LmaxInstrumentId), "QQ_LMAX_INSTRUMENT_ID");
        Require(!string.IsNullOrWhiteSpace(options.FixSecurityIdSource), "QQ_LMAX_FIX_SECURITY_ID_SOURCE");
        Require(!string.IsNullOrWhiteSpace(options.FixUsername), "QQ_LMAX_FIX_USERNAME");
        Require(!string.IsNullOrWhiteSpace(options.FixPassword), "QQ_LMAX_FIX_PASSWORD");
        Require(binding.EnvironmentName.Equals("Production", StringComparison.Ordinal) &&
                options.EnvironmentName.Equals("Production", StringComparison.Ordinal),
            "ARCH7B_PRODUCTION_ENVIRONMENT_BINDING_MISMATCH");
        Require(options.AccountCode == binding.AccountId,
            "ARCH7B_PRODUCTION_ACCOUNT_BINDING_MISMATCH");
        Require(options.FixOrderHost == binding.FixOrderHost &&
                options.FixOrderPort == binding.FixOrderPort &&
                (options.FixOrderTargetCompId ?? options.FixTargetCompId) == binding.FixOrderTargetCompId,
            "ARCH7B_PRODUCTION_FIX_ENDPOINT_BINDING_MISMATCH");
        Require(options.FixMarketDataHost == binding.FixMarketDataHost &&
                options.FixMarketDataPort == binding.FixMarketDataPort &&
                options.FixMarketDataTargetCompId == binding.FixMarketDataTargetCompId,
            "ARCH7B_PRODUCTION_MARKET_DATA_ENDPOINT_BINDING_MISMATCH");
        Require(options.FixSenderCompId == binding.FixSenderCompId,
            "ARCH7B_PRODUCTION_FIX_SENDER_BINDING_MISMATCH");
        Require(options.InstrumentSymbol == binding.InstrumentSymbol &&
                options.LmaxInstrumentId == binding.SecurityId &&
                options.FixSecurityIdSource == binding.SecurityIdSource,
            "ARCH7B_PRODUCTION_INSTRUMENT_BINDING_MISMATCH");
        Require(!string.IsNullOrWhiteSpace(binding.PersistenceHost) &&
                binding.PersistencePort is > 0 and <= 65535 &&
                !string.IsNullOrWhiteSpace(binding.PersistenceDatabase),
            "ARCH7B_PRODUCTION_PERSISTENCE_BINDING_MISSING");
        Require(options.UseTls, "ARCH7B_PRODUCTION_FIX_TLS_REQUIRED");
        Require(!string.IsNullOrWhiteSpace(binding.OperatorAuthorizationId),
            "ARCH7B_EXACT_OPERATOR_AUTHORIZATION_MISSING");
        Require(binding.IssuedAtUtc.Offset == TimeSpan.Zero && binding.DeadlineUtc.Offset == TimeSpan.Zero &&
                binding.IssuedAtUtc <= nowUtc && binding.DeadlineUtc > nowUtc &&
                binding.DeadlineUtc - binding.IssuedAtUtc <= TimeSpan.FromSeconds(180),
            "ARCH7B_DEADLINE_EXCEEDED");
        Require(options.MarketDataRequestMode == LmaxFixMarketDataRequestMode.SnapshotPlusUpdates &&
                options.MarketDataSymbolEncodingMode != LmaxFixMarketDataSymbolEncodingMode.Auto &&
                options.MarketDepth == 1 && options.MarketDataMaxWaitSeconds is > 0 and <= 5,
            "ARCH7B_PRODUCTION_MARKET_DATA_READINESS_OPTIONS_INVALID");
        if (validateOnly)
        {
            Require(!options.AllowExternalConnections && !options.AllowOrderSubmission &&
                    !options.AllowLiveTrading && options.DryRun,
                "ARCH7B_PRODUCTION_READINESS_VALIDATE_ONLY_OPTIONS_INVALID");
        }
        else
        {
            Require(options.AllowExternalConnections && !options.AllowOrderSubmission &&
                    !options.AllowLiveTrading && !options.DryRun,
                "ARCH7B_PRODUCTION_READINESS_OPTIONS_INVALID");
        }
        return blockers;

        void Require(bool condition, string blocker)
        {
            if (!condition)
                blockers.Add(blocker);
        }
    }

    public static IReadOnlyList<string> ValidateMarketDataObservation(
        LmaxConnectivityLabOptions options,
        LmaxFixMarketDataSmokeResult result,
        DateTimeOffset notBeforeUtc,
        DateTimeOffset nowUtc,
        LmaxFixArch7bProductionReadinessBinding binding)
    {
        var blockers = new List<string>();
        var observedAtUtc = result.ObservationCompletedAtUtc ?? result.CompletedAtUtc;
        Require(!options.AllowOrderSubmission && !options.AllowLiveTrading,
            "ARCH7B_READINESS_MARKET_DATA_NOT_READ_ONLY");
        Require(options.InstrumentSymbol == binding.InstrumentSymbol &&
                options.LmaxInstrumentId == binding.SecurityId &&
                options.FixSecurityIdSource == binding.SecurityIdSource,
            "ARCH7B_PRODUCTION_INSTRUMENT_BINDING_MISMATCH");
        Require(result.Status == "Ok" && result.FixLoggedOn && result.MarketDataRequestSent &&
                result.MarketDataSnapshotReceived && !result.MarketDataRejectReceived && result.CompleteTopOfBook,
            "ARCH7B_PRODUCTION_READINESS_BBO_UNAVAILABLE");
        Require(result.RequestMode == LmaxFixMarketDataRequestMode.SnapshotPlusUpdates &&
                !string.IsNullOrWhiteSpace(result.MdReqId),
            "ARCH7B_PRODUCTION_READINESS_MARKET_DATA_REQUEST_INVALID");
        Require(result.InboundSequenceIntegrityProven,
            "ARCH7B_PRODUCTION_READINESS_SEQUENCE_INTEGRITY_UNPROVEN");
        Require(result.StartedAtUtc >= notBeforeUtc && observedAtUtc >= result.StartedAtUtc &&
                observedAtUtc <= nowUtc && nowUtc - observedAtUtc <=
                TimeSpan.FromSeconds(options.MarketDataMaxWaitSeconds),
            "ARCH7B_PRODUCTION_READINESS_BBO_STALE");
        Require(result.BestBid is > 0m && result.BestAsk is > 0m && result.BestAsk >= result.BestBid,
            "ARCH7B_PRODUCTION_READINESS_BBO_INVALID");
        return blockers;

        void Require(bool condition, string blocker)
        {
            if (!condition)
                blockers.Add(blocker);
        }
    }
}
