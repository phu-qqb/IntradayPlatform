using System.Text.RegularExpressions;

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
/// Keeps the production readiness command packet-bound while requiring every trading permission to be disabled.
/// </summary>
public static class LmaxFixArch7bProductionReadinessContract
{
    private static readonly Regex Sha256 = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> Validate(
        LmaxConnectivityLabOptions options,
        LmaxFixArch7bKnownOrderRequest request,
        bool explicitReadinessConfirmation,
        DateTimeOffset nowUtc)
    {
        var blockers = new List<string>();
        var binding = request.ProductionBinding;
        Require(explicitReadinessConfirmation, "ARCH7B_PRODUCTION_READINESS_CLI_CONFIRMATION_MISSING");
        Require(request.Activation == LmaxFixArch7bActivation.ProductionAuthorizedOnce,
            "ARCH7B_PRODUCTION_READINESS_ACTIVATION_REQUIRED");
        Require(binding is not null, "ARCH7B_PRODUCTION_BINDING_MISSING");
        if (binding is null)
            return blockers;

        Require(binding.EnvironmentName.Equals("Production", StringComparison.Ordinal) &&
                options.EnvironmentName.Equals("Production", StringComparison.Ordinal),
            "ARCH7B_PRODUCTION_ENVIRONMENT_BINDING_MISMATCH");
        Require(request.AccountId == binding.AccountId,
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
        Require(options.AllowExternalConnections && !options.AllowOrderSubmission &&
                !options.AllowLiveTrading && !options.DryRun,
            "ARCH7B_PRODUCTION_READINESS_OPTIONS_INVALID");
        Require(options.UseTls, "ARCH7B_PRODUCTION_FIX_TLS_REQUIRED");
        Require(!string.IsNullOrWhiteSpace(options.FixUsername) && !string.IsNullOrWhiteSpace(options.FixPassword),
            "ARCH7B_PRODUCTION_FIX_IDENTITY_MISSING");
        Require(request.ExactOperatorAuthorizationPresent && request.KillSwitchArmed && request.ExclusivityDeclared &&
                !string.IsNullOrWhiteSpace(binding.OperatorAuthorizationId),
            "ARCH7B_EXACT_OPERATOR_AUTHORIZATION_MISSING");
        Require(Sha256.IsMatch(request.PolicySha256) && Sha256.IsMatch(request.AuthorizationPacketSha256) &&
                request.AuthorizationPacketSha256.Equals(
                    LmaxFixArch7bKnownOrderContract.ComputeProductionAuthorizationPacketSha256(request, binding),
                    StringComparison.OrdinalIgnoreCase),
            "ARCH7B_AUTHORIZATION_PACKET_SHA256_MISMATCH");
        Require(request.DeadlineUtc.Offset == TimeSpan.Zero && request.DeadlineUtc > nowUtc &&
                request.DeadlineUtc - nowUtc <= TimeSpan.FromSeconds(binding.MaximumLifecycleSeconds),
            "ARCH7B_DEADLINE_EXCEEDED");
        Require(options.MarketDataRequestMode == LmaxFixMarketDataRequestMode.SnapshotPlusUpdates &&
                options.MarketDataSymbolEncodingMode != LmaxFixMarketDataSymbolEncodingMode.Auto &&
                options.MarketDepth == 1 && options.MarketDataMaxWaitSeconds is > 0 and <= 5,
            "ARCH7B_PRODUCTION_MARKET_DATA_READINESS_OPTIONS_INVALID");
        return blockers;

        void Require(bool condition, string blocker)
        {
            if (!condition)
                blockers.Add(blocker);
        }
    }
}
