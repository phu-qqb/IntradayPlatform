using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

public enum LmaxFixArch7bActivation
{
    Disabled = 0,
    DryRun = 1,
    AuthorizedOnce = 2,
    ProductionAuthorizedOnce = 3,
    ProductionDryRun = 4
}

/// <summary>
/// Explicit, packet-bound configuration for the first bounded production run.
/// It is intentionally absent from the Demo activation path.
/// </summary>
public sealed record LmaxFixArch7bProductionBinding(
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
    decimal VenueQuantity,
    decimal QuantityIncrement,
    decimal PriceIncrement,
    decimal CollarPips,
    int MaximumLifecycleSeconds,
    string PersistenceHost,
    int PersistencePort,
    string PersistenceDatabase,
    string OperatorAuthorizationId)
{
    public const decimal AbsoluteMaximumFirstProductionQuantity = 1m;

    public Arch7bKnownOrderExecutionProfile ExecutionProfile => new(
        "ARCH7B_FIRST_PRODUCTION_KNOWN_ORDER_LIFECYCLE",
        "PRODUCTION_EXCLUSIVE_KNOWN_ORDER_AUTHORIZATION",
        EnvironmentName,
        AccountId,
        InstrumentSymbol,
        SecurityId,
        SecurityIdSource,
        "BUY",
        VenueQuantity,
        QuantityIncrement,
        PriceIncrement,
        CollarPips,
        5,
        MaximumLifecycleSeconds,
        2,
        1,
        0,
        4,
        "LMAX_CURRENT_BBO_TOUCH_LIMIT",
        "LMAX_CURRENT_BBO_TOUCH_LIMIT_OPPOSITE_SIDE",
        "UNPROVEN");
}

public sealed record LmaxFixArch7bKnownOrderRequest(
    LmaxFixArch7bActivation Activation,
    Guid QualificationRunId,
    Guid ChildOrderId,
    string SessionId,
    string OwnerId,
    string AccountId,
    string OpeningClientOrderId,
    string CancelClientOrderId,
    string FlattenClientOrderId,
    decimal OpeningLimitPrice,
    decimal MinimumOpeningPrice,
    decimal MaximumOpeningPrice,
    decimal BboBid,
    decimal BboAsk,
    DateTimeOffset BboObservedAtUtc,
    string BboSource,
    string BboSnapshotSha256,
    DateTimeOffset RegisteredAtUtc,
    DateTimeOffset OpeningCancelAtUtc,
    DateTimeOffset DeadlineUtc,
    string PolicySha256,
    string AuthorizationPacketSha256,
    bool ExclusivityDeclared,
    bool ExactOperatorAuthorizationPresent,
    bool KillSwitchArmed,
    bool ShowFixMessages)
{
    public string OpeningMarketObservationId { get; init; } = string.Empty;
    public string BboSymbol { get; init; } = string.Empty;
    public string BboSecurityId { get; init; } = string.Empty;
    public DateTimeOffset BboAcquisitionStartedAtUtc { get; init; } = DateTimeOffset.MinValue;
    public bool BboSequenceIntegrityProven { get; init; }
    public bool BboPolygonUsed { get; init; }
    public LmaxFixArch7bProductionBinding? ProductionBinding { get; init; }
    public bool ProductionCommandConfirmed { get; init; }

    public Arch7bKnownOrderExecutionProfile ExecutionProfile =>
        Activation is LmaxFixArch7bActivation.ProductionAuthorizedOnce or LmaxFixArch7bActivation.ProductionDryRun
            ? ProductionBinding?.ExecutionProfile ?? throw new InvalidOperationException(
                "ARCH7B_PRODUCTION_BINDING_MISSING")
            : Arch7bKnownOrderExecutionProfile.Demo;

    public static LmaxFixArch7bKnownOrderRequest Disabled() =>
        new(
            LmaxFixArch7bActivation.Disabled,
            Guid.Empty,
            Guid.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            0m,
            0m,
            0m,
            0m,
            0m,
            DateTimeOffset.MinValue,
            string.Empty,
            string.Empty,
            DateTimeOffset.MinValue,
            DateTimeOffset.MinValue,
            DateTimeOffset.MinValue,
            string.Empty,
            string.Empty,
            false,
            false,
            false,
            false);
}

public sealed record LmaxFixArch7bKnownOrderResult(
    string Command,
    string Status,
    bool Connected,
    bool LoggedOn,
    bool OpeningSent,
    bool CancelSent,
    bool FlattenSent,
    int OrderStatusRequestCount,
    bool LogoutSent,
    string? Blocker,
    IReadOnlyList<LmaxFixExecutionReport> ExecutionReports,
    IReadOnlyList<string> Diagnostics,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc)
{
    public static LmaxFixArch7bKnownOrderResult Skipped(string blocker, IReadOnlyList<string>? diagnostics = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new(
            "fix-arch7b-known-order-lifecycle",
            "Skipped",
            false,
            false,
            false,
            false,
            false,
            0,
            false,
            blocker,
            [],
            diagnostics ?? [],
            now,
            now);
    }
}

public sealed record LmaxFixArch7bDryRunPlan(
    string OpeningNewOrderSingleSanitized,
    string OpeningCancelRequestSanitized,
    string FlattenNewOrderSingleSanitized,
    string OpeningOrderStatusRequestSanitized,
    string FlattenOrderStatusRequestSanitized,
    Arch7bApplicationMessageBudget MaximumBudget);

public sealed record LmaxFixArch7bMarketObservationDecision(
    bool Allowed,
    IReadOnlyList<string> Blockers,
    Arch7bLmaxBbo? Observation,
    decimal? LimitPrice);

public static class LmaxFixArch7bKnownOrderContract
{
    public static IReadOnlyList<string> Validate(
        LmaxConnectivityLabOptions options,
        LmaxFixArch7bKnownOrderRequest request,
        DateTimeOffset nowUtc)
    {
        var blockers = new List<string>();
        if (request.Activation == LmaxFixArch7bActivation.ProductionAuthorizedOnce)
            return ValidateProduction(options, request, nowUtc, requireExecutionAuthorization: true);
        if (request.Activation == LmaxFixArch7bActivation.ProductionDryRun)
            return ValidateProduction(options, request, nowUtc, requireExecutionAuthorization: false);

        Require(request.Activation == LmaxFixArch7bActivation.AuthorizedOnce, "ARCH7B_EXECUTION_DISABLED_BY_DEFAULT");
        Require(options.EnvironmentName.Equals("Demo", StringComparison.OrdinalIgnoreCase) ||
                options.EnvironmentName.Equals("UAT", StringComparison.OrdinalIgnoreCase),
            "ARCH7B_FIX_ENVIRONMENT_NOT_DEMO_OR_UAT");
        Require(!options.AllowLiveTrading, "ARCH7B_ALLOW_LIVE_TRADING_FORBIDDEN");
        Require(options.AllowExternalConnections, "ARCH7B_EXTERNAL_CONNECTION_DISABLED");
        Require(options.AllowOrderSubmission, "ARCH7B_ORDER_SUBMISSION_DISABLED");
        Require(!options.DryRun, "ARCH7B_OPTIONS_DRY_RUN_ENABLED");
        Require(request.ExactOperatorAuthorizationPresent, "ARCH7B_EXACT_OPERATOR_AUTHORIZATION_MISSING");
        Require(request.KillSwitchArmed, "ARCH7B_KILL_SWITCH_NOT_ARMED");
        Require(request.AccountId == Arch7bKnownOrderQualificationPolicy.DemoAccountId, "ARCH7B_DEMO_ACCOUNT_IDENTITY_MISMATCH");
        Require(request.AccountId != Arch7bKnownOrderQualificationPolicy.ForbiddenRealAccountId, "ARCH7B_REAL_ACCOUNT_FORBIDDEN");
        Require((options.FixOrderTargetCompId ?? options.FixTargetCompId) == "LMXBD", "ARCH7B_FIX_TARGET_NOT_LMXBD");
        Require(IsDemoOrUatHost(options.FixOrderHost), "ARCH7B_FIX_HOST_NOT_DEMO_OR_UAT");
        Require(options.FixOrderPort is > 0 and <= 65535, "ARCH7B_FIX_ORDER_PORT_INVALID");
        Require(!string.IsNullOrWhiteSpace(options.FixSenderCompId), "ARCH7B_FIX_SENDER_MISSING");
        Require(!string.IsNullOrWhiteSpace(options.FixUsername), "ARCH7B_FIX_USERNAME_MISSING");
        Require(!string.IsNullOrWhiteSpace(options.FixPassword), "ARCH7B_FIX_PASSWORD_MISSING");
        Require(request.OpeningLimitPrice > 0m, "ARCH7B_LIMIT_PRICE_INVALID");
        Require(request.BboBid > 0m && request.BboAsk >= request.BboBid, "ARCH7B_BBO_INVALID");
        Require(request.BboSource == "LMAX", "ARCH7B_BBO_SOURCE_NOT_LMAX");
        Require(request.BboSymbol == Arch7bKnownOrderQualificationPolicy.Symbol &&
                request.BboSecurityId == Arch7bKnownOrderQualificationPolicy.SecurityId,
            "ARCH7B_BBO_INSTRUMENT_MISMATCH");
        Require(options.InstrumentSymbol == Arch7bKnownOrderQualificationPolicy.Symbol &&
                options.LmaxInstrumentId == Arch7bKnownOrderQualificationPolicy.SecurityId &&
                options.FixSecurityIdSource == Arch7bKnownOrderQualificationPolicy.SecurityIdSource,
            "ARCH7B_MARKET_DATA_MAPPING_MISMATCH");
        Require(options.MarketDataRequestMode == LmaxFixMarketDataRequestMode.SnapshotPlusUpdates &&
                options.MarketDataSymbolEncodingMode != LmaxFixMarketDataSymbolEncodingMode.Auto,
            "ARCH7B_MARKET_DATA_SESSION_MODE_UNBOUNDED");
        Require(request.BboSequenceIntegrityProven, "ARCH7B_BBO_SEQUENCE_INTEGRITY_UNPROVEN");
        Require(!request.BboPolygonUsed, "ARCH7B_POLYGON_ORDER_PRICE_FORBIDDEN");
        Require(IsSha256(request.OpeningMarketObservationId) &&
                request.OpeningMarketObservationId.Equals(
                    request.BboSnapshotSha256, StringComparison.OrdinalIgnoreCase),
            "ARCH7B_OPENING_MARKET_OBSERVATION_ID_INVALID");
        Require(request.OpeningLimitPrice == request.BboAsk,
            "ARCH7B_OPENING_LIMIT_NOT_LMAX_ASK");
        Require(IsTickAligned(request.BboBid) && IsTickAligned(request.BboAsk),
            "ARCH7B_BBO_NOT_TICK_ALIGNED");
        Require(request.BboAsk - request.BboBid <= MaximumSpread(),
            "ARCH7B_BBO_SPREAD_TOO_WIDE");
        Require(request.MinimumOpeningPrice > 0m &&
                request.OpeningLimitPrice >= request.MinimumOpeningPrice &&
                request.OpeningLimitPrice <= request.MaximumOpeningPrice,
            "ARCH7B_OPENING_PRICE_OUTSIDE_AUTHORIZED_COLLAR");
        Require(IsSha256(request.PolicySha256), "ARCH7B_POLICY_SHA256_INVALID");
        Require(IsSha256(request.AuthorizationPacketSha256), "ARCH7B_AUTHORIZATION_PACKET_SHA256_INVALID");
        Require(request.AuthorizationPacketSha256.Equals(
                ComputeAuthorizationPacketSha256(request), StringComparison.OrdinalIgnoreCase),
            "ARCH7B_AUTHORIZATION_PACKET_SHA256_MISMATCH");
        Require(request.BboAcquisitionStartedAtUtc.Offset == TimeSpan.Zero &&
                request.BboObservedAtUtc.Offset == TimeSpan.Zero &&
                request.RegisteredAtUtc.Offset == TimeSpan.Zero &&
                request.OpeningCancelAtUtc.Offset == TimeSpan.Zero &&
                request.DeadlineUtc.Offset == TimeSpan.Zero, "ARCH7B_TIMESTAMPS_NOT_UTC");
        Require(request.RegisteredAtUtc <= nowUtc, "ARCH7B_REGISTERED_TIME_FROM_FUTURE");
        Require(request.BboAcquisitionStartedAtUtc >= request.RegisteredAtUtc &&
                request.BboObservedAtUtc >= request.BboAcquisitionStartedAtUtc,
            "ARCH7B_BBO_NOT_ACQUIRED_IN_AUTHORIZED_WINDOW");
        Require(nowUtc <= request.DeadlineUtc, "ARCH7B_DEADLINE_EXCEEDED");
        Require(request.OpeningCancelAtUtc > nowUtc, "ARCH7B_OPENING_CANCEL_DEADLINE_EXCEEDED");
        Require(request.OpeningCancelAtUtc < request.DeadlineUtc,
            "ARCH7B_OPENING_CANCEL_DEADLINE_NOT_BEFORE_FINAL_DEADLINE");
        Require(request.DeadlineUtc - nowUtc <= TimeSpan.FromSeconds(Arch7bKnownOrderQualificationPolicy.MaximumLifecycleSeconds),
            "ARCH7B_DEADLINE_EXCEEDS_MAXIMUM");
        Require(nowUtc - request.BboObservedAtUtc <= TimeSpan.FromSeconds(Arch7bKnownOrderQualificationPolicy.MaximumBboAgeSeconds),
            "ARCH7B_BBO_STALE");
        Require(request.BboObservedAtUtc <= nowUtc, "ARCH7B_BBO_FROM_FUTURE");
        Require(!string.IsNullOrWhiteSpace(request.SessionId), "ARCH7B_SESSION_ID_MISSING");
        Require(!string.IsNullOrWhiteSpace(request.OwnerId), "ARCH7B_OWNER_ID_MISSING");
        Require(request.QualificationRunId != Guid.Empty, "ARCH7B_QUALIFICATION_RUN_ID_MISSING");
        Require(request.ChildOrderId != Guid.Empty, "ARCH7B_CHILD_ORDER_ID_MISSING");
        Require(request.ExclusivityDeclared, "ARCH7B_EXCLUSIVITY_DECLARATION_MISSING");
        Require(IsSha256(request.BboSnapshotSha256), "ARCH7B_BBO_SNAPSHOT_SHA256_INVALID");
        ValidateKnownId(request.OpeningClientOrderId, "A7BO", "ARCH7B_OPENING_CLORDID_INVALID");
        ValidateKnownId(request.CancelClientOrderId, "A7BC", "ARCH7B_CANCEL_CLORDID_INVALID");
        ValidateKnownId(request.FlattenClientOrderId, "A7BF", "ARCH7B_FLATTEN_CLORDID_INVALID");
        return blockers;

        void Require(bool condition, string blocker)
        {
            if (!condition)
                blockers.Add(blocker);
        }

        void ValidateKnownId(string value, string prefix, string blocker)
            => Require(value.Length == 20 &&
                       value.StartsWith(prefix, StringComparison.Ordinal) &&
                       value.All(character => char.IsAsciiLetterOrDigit(character)),
                blocker);
    }

    public static string ComputeAuthorizationPacketSha256(
        LmaxFixArch7bKnownOrderRequest request)
        => Arch7bKnownOrderQualification.ComputeAuthorizationPacketSha256(new
        {
            Arch7bKnownOrderQualificationPolicy.Gate,
            Arch7bKnownOrderQualificationPolicy.Scope,
            request.Activation,
            request.QualificationRunId,
            request.ChildOrderId,
            request.SessionId,
            request.OwnerId,
            request.AccountId,
            request.OpeningClientOrderId,
            request.CancelClientOrderId,
            request.FlattenClientOrderId,
            request.OpeningLimitPrice,
            request.MinimumOpeningPrice,
            request.MaximumOpeningPrice,
            request.BboBid,
            request.BboAsk,
            request.BboObservedAtUtc,
            request.BboAcquisitionStartedAtUtc,
            request.BboSource,
            request.BboSnapshotSha256,
            request.OpeningMarketObservationId,
            request.BboSymbol,
            request.BboSecurityId,
            request.BboSequenceIntegrityProven,
            request.BboPolygonUsed,
            request.RegisteredAtUtc,
            request.OpeningCancelAtUtc,
            request.DeadlineUtc,
            request.PolicySha256,
            request.ExclusivityDeclared,
            request.KillSwitchArmed,
            Arch7bKnownOrderQualificationPolicy.DemoAccountId,
            Arch7bKnownOrderQualificationPolicy.ForbiddenRealAccountId,
            Arch7bKnownOrderQualificationPolicy.Symbol,
            Arch7bKnownOrderQualificationPolicy.SecurityId,
            Arch7bKnownOrderQualificationPolicy.SecurityIdSource,
            Arch7bKnownOrderQualificationPolicy.VenueQuantity,
            Arch7bKnownOrderQualificationPolicy.QuantityIncrement,
            Arch7bKnownOrderQualificationPolicy.PriceIncrement,
            Arch7bKnownOrderQualificationPolicy.MaximumNewOrderSingleCount,
            Arch7bKnownOrderQualificationPolicy.MaximumCancelCount,
            Arch7bKnownOrderQualificationPolicy.MaximumReplaceCount,
            Arch7bKnownOrderQualificationPolicy.MaximumOrderStatusRequestCount,
            Arch7bKnownOrderQualificationPolicy.ExternalOrManualOrderCoverage
        });

    private static IReadOnlyList<string> ValidateProduction(
        LmaxConnectivityLabOptions options,
        LmaxFixArch7bKnownOrderRequest request,
        DateTimeOffset nowUtc,
        bool requireExecutionAuthorization)
    {
        var blockers = new List<string>();
        var binding = request.ProductionBinding;
        Require(binding is not null, "ARCH7B_PRODUCTION_BINDING_MISSING");
        if (binding is null) return blockers;

        var profile = binding.ExecutionProfile;
        Require(binding.EnvironmentName.Equals("Production", StringComparison.Ordinal), "ARCH7B_PRODUCTION_ENVIRONMENT_BINDING_MISMATCH");
        Require(options.EnvironmentName.Equals(binding.EnvironmentName, StringComparison.Ordinal), "ARCH7B_PRODUCTION_ENVIRONMENT_BINDING_MISMATCH");
        Require(request.AccountId == binding.AccountId, "ARCH7B_PRODUCTION_ACCOUNT_BINDING_MISMATCH");
        Require(options.FixOrderHost == binding.FixOrderHost && options.FixOrderPort == binding.FixOrderPort &&
                (options.FixOrderTargetCompId ?? options.FixTargetCompId) == binding.FixOrderTargetCompId,
            "ARCH7B_PRODUCTION_FIX_ENDPOINT_BINDING_MISMATCH");
        Require(options.FixSenderCompId == binding.FixSenderCompId,
            "ARCH7B_PRODUCTION_FIX_SENDER_BINDING_MISMATCH");
        Require(options.FixMarketDataHost == binding.FixMarketDataHost &&
                options.FixMarketDataPort == binding.FixMarketDataPort &&
                options.FixMarketDataTargetCompId == binding.FixMarketDataTargetCompId,
            "ARCH7B_PRODUCTION_MARKET_DATA_ENDPOINT_BINDING_MISMATCH");
        Require(options.InstrumentSymbol == binding.InstrumentSymbol &&
                options.LmaxInstrumentId == binding.SecurityId &&
                options.FixSecurityIdSource == binding.SecurityIdSource,
            "ARCH7B_PRODUCTION_INSTRUMENT_BINDING_MISMATCH");
        Require(binding.QuantityIncrement > 0m, "ARCH7B_PRODUCTION_QUANTITY_INCREMENT_INVALID");
        Require(binding.VenueQuantity > 0m &&
                binding.VenueQuantity <= LmaxFixArch7bProductionBinding.AbsoluteMaximumFirstProductionQuantity &&
                binding.QuantityIncrement > 0m &&
                binding.VenueQuantity % binding.QuantityIncrement == 0m,
            "ARCH7B_PRODUCTION_QUANTITY_CAP_INVALID");
        Require(binding.PriceIncrement > 0m && binding.CollarPips > 0m && binding.MaximumLifecycleSeconds is > 0 and <= 180,
            "ARCH7B_PRODUCTION_BOUNDS_INVALID");
        Require(!string.IsNullOrWhiteSpace(binding.PersistenceHost) && binding.PersistencePort is > 0 and <= 65535 &&
                !string.IsNullOrWhiteSpace(binding.PersistenceDatabase),
            "ARCH7B_PRODUCTION_PERSISTENCE_BINDING_MISSING");
        Require(!string.IsNullOrWhiteSpace(binding.OperatorAuthorizationId), "ARCH7B_EXACT_OPERATOR_AUTHORIZATION_MISSING");
        Require(request.ExactOperatorAuthorizationPresent, "ARCH7B_EXACT_OPERATOR_AUTHORIZATION_MISSING");
        Require(request.KillSwitchArmed, "ARCH7B_KILL_SWITCH_NOT_ARMED");
        Require(request.ExclusivityDeclared, "ARCH7B_EXCLUSIVITY_DECLARATION_MISSING");
        if (requireExecutionAuthorization)
        {
            Require(request.ProductionCommandConfirmed, "ARCH7B_PRODUCTION_CLI_CONFIRMATION_MISSING");
            Require(options.AllowExternalConnections && options.AllowOrderSubmission && !options.AllowLiveTrading && !options.DryRun,
                "ARCH7B_PRODUCTION_EXECUTION_OPTIONS_INVALID");
            Require(options.UseTls, "ARCH7B_PRODUCTION_FIX_TLS_REQUIRED");
            Require(!string.IsNullOrWhiteSpace(options.FixUsername) && !string.IsNullOrWhiteSpace(options.FixPassword),
                "ARCH7B_PRODUCTION_FIX_IDENTITY_MISSING");
        }
        else
            Require(!options.AllowExternalConnections && !options.AllowOrderSubmission && options.DryRun,
                "ARCH7B_PRODUCTION_DRY_RUN_OPTIONS_INVALID");
        Require(request.BboSource == "LMAX" && !request.BboPolygonUsed && request.BboSequenceIntegrityProven,
            "ARCH7B_BBO_SOURCE_NOT_LMAX");
        Require(request.BboBid > 0m && request.BboAsk >= request.BboBid,
            "ARCH7B_BBO_INVALID");
        Require(request.BboSymbol == profile.Symbol && request.BboSecurityId == profile.SecurityId,
            "ARCH7B_PRODUCTION_BBO_BINDING_MISMATCH");
        Require(IsTickAligned(request.BboBid, profile) && IsTickAligned(request.BboAsk, profile) &&
                IsTickAligned(request.OpeningLimitPrice, profile) &&
                IsTickAligned(request.MinimumOpeningPrice, profile) &&
                IsTickAligned(request.MaximumOpeningPrice, profile),
            "ARCH7B_BBO_NOT_TICK_ALIGNED");
        Require(request.BboAsk - request.BboBid <= MaximumSpread(profile), "ARCH7B_BBO_SPREAD_TOO_WIDE");
        Require(IsSha256(request.BboSnapshotSha256) && IsSha256(request.OpeningMarketObservationId) &&
                request.OpeningMarketObservationId.Equals(request.BboSnapshotSha256, StringComparison.OrdinalIgnoreCase),
            "ARCH7B_OPENING_MARKET_OBSERVATION_ID_INVALID");
        Require(request.OpeningLimitPrice == request.BboAsk, "ARCH7B_OPENING_LIMIT_NOT_LMAX_ASK");
        Require(request.OpeningLimitPrice >= request.MinimumOpeningPrice && request.OpeningLimitPrice <= request.MaximumOpeningPrice,
            "ARCH7B_OPENING_PRICE_OUTSIDE_AUTHORIZED_COLLAR");
        Require(request.RegisteredAtUtc.Offset == TimeSpan.Zero && request.BboAcquisitionStartedAtUtc.Offset == TimeSpan.Zero &&
                request.BboObservedAtUtc.Offset == TimeSpan.Zero && request.OpeningCancelAtUtc.Offset == TimeSpan.Zero &&
                request.DeadlineUtc.Offset == TimeSpan.Zero, "ARCH7B_TIMESTAMPS_NOT_UTC");
        Require(request.RegisteredAtUtc <= nowUtc, "ARCH7B_REGISTERED_TIME_FROM_FUTURE");
        Require(request.BboAcquisitionStartedAtUtc >= request.RegisteredAtUtc &&
                request.BboObservedAtUtc >= request.BboAcquisitionStartedAtUtc && request.BboObservedAtUtc <= nowUtc,
            "ARCH7B_BBO_NOT_ACQUIRED_IN_AUTHORIZED_WINDOW");
        Require(nowUtc - request.BboObservedAtUtc <= TimeSpan.FromSeconds(profile.MaximumBboAgeSeconds), "ARCH7B_BBO_STALE");
        Require(request.OpeningCancelAtUtc > nowUtc, "ARCH7B_OPENING_CANCEL_DEADLINE_EXCEEDED");
        Require(request.OpeningCancelAtUtc < request.DeadlineUtc,
            "ARCH7B_OPENING_CANCEL_DEADLINE_NOT_BEFORE_FINAL_DEADLINE");
        Require(request.DeadlineUtc >= nowUtc && request.DeadlineUtc - nowUtc <= TimeSpan.FromSeconds(profile.MaximumLifecycleSeconds),
            "ARCH7B_DEADLINE_EXCEEDED");
        Require(IsSha256(request.PolicySha256) && IsSha256(request.AuthorizationPacketSha256) &&
                request.AuthorizationPacketSha256.Equals(ComputeProductionAuthorizationPacketSha256(request, binding), StringComparison.OrdinalIgnoreCase),
            "ARCH7B_AUTHORIZATION_PACKET_SHA256_MISMATCH");
        ValidateKnownId(request.OpeningClientOrderId, "A7BO", "ARCH7B_OPENING_CLORDID_INVALID");
        ValidateKnownId(request.CancelClientOrderId, "A7BC", "ARCH7B_CANCEL_CLORDID_INVALID");
        ValidateKnownId(request.FlattenClientOrderId, "A7BF", "ARCH7B_FLATTEN_CLORDID_INVALID");
        return blockers;

        void Require(bool condition, string blocker)
        {
            if (!condition) blockers.Add(blocker);
        }

        void ValidateKnownId(string value, string prefix, string blocker)
            => Require(value.Length == 20 && value.StartsWith(prefix, StringComparison.Ordinal) &&
                       value.All(char.IsAsciiLetterOrDigit), blocker);
    }

    public static IReadOnlyList<string> ValidateProductionReadOnlyMarketData(
        LmaxConnectivityLabOptions options,
        LmaxFixArch7bKnownOrderRequest request,
        DateTimeOffset cleanupDeadlineUtc,
        DateTimeOffset nowUtc)
    {
        var blockers = new List<string>();
        var binding = request.ProductionBinding;
        Require(request.Activation == LmaxFixArch7bActivation.ProductionAuthorizedOnce,
            "ARCH7B_PRODUCTION_READ_ONLY_MARKET_DATA_ACTIVATION_REQUIRED");
        Require(binding is not null, "ARCH7B_PRODUCTION_BINDING_MISSING");
        if (binding is null)
            return blockers;

        var profile = binding.ExecutionProfile;
        Require(request.ProductionCommandConfirmed, "ARCH7B_PRODUCTION_CLI_CONFIRMATION_MISSING");
        Require(binding.EnvironmentName.Equals("Production", StringComparison.Ordinal) &&
                options.EnvironmentName.Equals(binding.EnvironmentName, StringComparison.Ordinal),
            "ARCH7B_PRODUCTION_ENVIRONMENT_BINDING_MISMATCH");
        Require(IsSha256(request.AuthorizationPacketSha256) && request.AuthorizationPacketSha256.Equals(
                ComputeProductionAuthorizationPacketSha256(request, binding), StringComparison.OrdinalIgnoreCase),
            "ARCH7B_AUTHORIZATION_PACKET_SHA256_MISMATCH");
        Require(options.AllowExternalConnections, "ARCH7B_PRODUCTION_READ_ONLY_EXTERNAL_CONNECTIONS_REQUIRED");
        Require(!options.AllowOrderSubmission, "ARCH7B_PRODUCTION_READ_ONLY_ORDER_SUBMISSION_FORBIDDEN");
        Require(!options.AllowLiveTrading, "ARCH7B_PRODUCTION_READ_ONLY_LIVE_TRADING_FORBIDDEN");
        Require(!options.DryRun, "ARCH7B_PRODUCTION_READ_ONLY_DRY_RUN_FORBIDDEN");
        Require(options.UseTls, "ARCH7B_PRODUCTION_FIX_TLS_REQUIRED");
        Require(options.FixMarketDataHost == binding.FixMarketDataHost,
            "ARCH7B_PRODUCTION_MARKET_DATA_HOST_BINDING_MISMATCH");
        Require(options.FixMarketDataPort == binding.FixMarketDataPort,
            "ARCH7B_PRODUCTION_MARKET_DATA_PORT_BINDING_MISMATCH");
        Require(options.FixMarketDataTargetCompId == binding.FixMarketDataTargetCompId,
            "ARCH7B_PRODUCTION_MARKET_DATA_TARGET_BINDING_MISMATCH");
        Require(options.FixSenderCompId == binding.FixSenderCompId,
            "ARCH7B_PRODUCTION_FIX_SENDER_BINDING_MISMATCH");
        Require(!string.IsNullOrWhiteSpace(options.FixUsername) && !string.IsNullOrWhiteSpace(options.FixPassword),
            "ARCH7B_PRODUCTION_FIX_IDENTITY_MISSING");
        Require(options.InstrumentSymbol == profile.Symbol && options.LmaxInstrumentId == profile.SecurityId &&
                options.FixSecurityIdSource == profile.SecurityIdSource,
            "ARCH7B_PRODUCTION_INSTRUMENT_BINDING_MISMATCH");
        Require(options.MarketDataRequestMode == LmaxFixMarketDataRequestMode.SnapshotPlusUpdates,
            "ARCH7B_PRODUCTION_MARKET_DATA_REQUEST_MODE_INVALID");
        Require(options.MarketDataSymbolEncodingMode != LmaxFixMarketDataSymbolEncodingMode.Auto,
            "ARCH7B_PRODUCTION_MARKET_DATA_SYMBOL_ENCODING_INVALID");
        Require(options.MarketDepth == 1, "ARCH7B_PRODUCTION_MARKET_DATA_DEPTH_INVALID");
        Require(options.MarketDataMaxWaitSeconds is > 0 &&
                options.MarketDataMaxWaitSeconds <= profile.MaximumBboAgeSeconds,
            "ARCH7B_PRODUCTION_MARKET_DATA_WAIT_BUDGET_INVALID");
        Require(nowUtc < request.DeadlineUtc && cleanupDeadlineUtc <= request.DeadlineUtc && cleanupDeadlineUtc > nowUtc,
            "ARCH7B_PRODUCTION_MARKET_DATA_DEADLINE_EXCEEDED");
        return blockers;

        void Require(bool condition, string blocker)
        {
            if (!condition)
                blockers.Add(blocker);
        }
    }

    public static string ComputeProductionAuthorizationPacketSha256(
        LmaxFixArch7bKnownOrderRequest request, LmaxFixArch7bProductionBinding binding)
        => Arch7bKnownOrderQualification.ComputeAuthorizationPacketSha256(new
        {
            request.Activation,
            request.QualificationRunId,
            request.ChildOrderId,
            request.SessionId,
            request.OwnerId,
            request.AccountId,
            request.OpeningClientOrderId,
            request.CancelClientOrderId,
            request.FlattenClientOrderId,
            request.OpeningLimitPrice,
            request.MinimumOpeningPrice,
            request.MaximumOpeningPrice,
            request.BboBid,
            request.BboAsk,
            request.BboObservedAtUtc,
            request.BboAcquisitionStartedAtUtc,
            request.BboSource,
            request.BboSnapshotSha256,
            request.OpeningMarketObservationId,
            request.BboSymbol,
            request.BboSecurityId,
            request.BboSequenceIntegrityProven,
            request.BboPolygonUsed,
            request.RegisteredAtUtc,
            request.OpeningCancelAtUtc,
            request.DeadlineUtc,
            request.PolicySha256,
            request.ExclusivityDeclared,
            request.ExactOperatorAuthorizationPresent,
            request.KillSwitchArmed,
            binding.EnvironmentName,
            BoundAccountId = binding.AccountId,
            binding.FixOrderHost,
            binding.FixOrderPort,
            binding.FixOrderTargetCompId,
            binding.FixSenderCompId,
            binding.FixMarketDataHost,
            binding.FixMarketDataPort,
            binding.FixMarketDataTargetCompId,
            binding.InstrumentSymbol,
            binding.SecurityId,
            binding.SecurityIdSource,
            binding.VenueQuantity,
            binding.QuantityIncrement,
            binding.PriceIncrement,
            binding.CollarPips,
            binding.MaximumLifecycleSeconds,
            binding.PersistenceHost,
            binding.PersistencePort,
            binding.PersistenceDatabase,
            binding.OperatorAuthorizationId
        });

    public static LmaxFixArch7bDryRunPlan BuildDryRunPlan(
        LmaxConnectivityLabOptions options,
        LmaxFixArch7bKnownOrderRequest request)
    {
        var profile = request.Activation is LmaxFixArch7bActivation.ProductionAuthorizedOnce or LmaxFixArch7bActivation.ProductionDryRun
            ? request.ExecutionProfile
            : Arch7bKnownOrderExecutionProfile.Demo;
        var sender = string.IsNullOrWhiteSpace(options.FixSenderCompId) ? "ARCH7B-DRYRUN" : options.FixSenderCompId;
        var target = options.FixOrderTargetCompId ?? options.FixTargetCompId ?? "LMXBD";
        var opening = LimitRequest(
            profile,
            request.AccountId,
            LmaxFixDemoOrderSide.Buy,
            profile.VenueQuantity,
            request.OpeningLimitPrice,
            request.OpeningClientOrderId);
        var openD = LmaxFixRecoveryCodec.BuildNewOrderSingle(sender!, target, 2, opening, request.OpeningClientOrderId, profile.SecurityIdSource);
        var cancelF = LmaxFixRecoveryCodec.BuildOrderCancelRequest(
            sender!,
            target,
            3,
            request.CancelClientOrderId,
            request.OpeningClientOrderId,
            profile.Symbol,
            "1",
            profile.VenueQuantity,
            profile.SecurityId,
            profile.SecurityIdSource);
        var openH = LmaxFixRecoveryCodec.BuildOrderStatusRequest(
            sender!,
            target,
            5,
            request.OpeningClientOrderId,
            request.AccountId,
            profile.SecurityId,
            profile.SecurityIdSource,
            "1");
        var flattenH = LmaxFixRecoveryCodec.BuildOrderStatusRequest(
            sender!,
            target,
            6,
            request.FlattenClientOrderId,
            request.AccountId,
            profile.SecurityId,
            profile.SecurityIdSource,
            "2");

        return new(
            LmaxFixMarketDataCodec.SanitizeMessage(openD),
            LmaxFixMarketDataCodec.SanitizeMessage(cancelF),
            "ARCH7B_FLATTEN_DYNAMIC_AFTER_TERMINAL_FRESH_LMAX_BBO_NO_MESSAGE_BUILT",
            LmaxFixMarketDataCodec.SanitizeMessage(openH),
            LmaxFixMarketDataCodec.SanitizeMessage(flattenH),
            new(
                profile.MaximumNewOrderSingleCount,
                profile.MaximumCancelCount,
                profile.MaximumReplaceCount,
                profile.MaximumOrderStatusRequestCount));
    }

    public static LmaxFixDemoOrderRequest DemoLimitRequest(
        string accountId,
        LmaxFixDemoOrderSide side,
        decimal quantity,
        decimal limitPrice,
        string clientOrderId)
        => new(
            Arch7bKnownOrderQualificationPolicy.Symbol,
            Arch7bKnownOrderQualificationPolicy.SecurityId,
            side,
            LmaxFixDemoOrderType.Limit,
            LmaxFixDemoOrderTimeInForce.Day,
            quantity,
            limitPrice,
            null,
            clientOrderId,
            accountId,
            true,
            false,
            Arch7bKnownOrderQualificationPolicy.MaximumLifecycleSeconds,
            false,
            false);

    public static LmaxFixDemoOrderRequest LimitRequest(
        Arch7bKnownOrderExecutionProfile profile,
        string accountId,
        LmaxFixDemoOrderSide side,
        decimal quantity,
        decimal limitPrice,
        string clientOrderId)
        => new(
            profile.Symbol,
            profile.SecurityId,
            side,
            LmaxFixDemoOrderType.Limit,
            LmaxFixDemoOrderTimeInForce.Day,
            quantity,
            limitPrice,
            null,
            clientOrderId,
            accountId,
            true,
            false,
            profile.MaximumLifecycleSeconds,
            false,
            false);

    public static LmaxFixArch7bMarketObservationDecision EvaluateFreshFlattenObservation(
        LmaxConnectivityLabOptions options,
        LmaxFixMarketDataSmokeResult result,
        DateTimeOffset notBeforeUtc,
        DateTimeOffset nowUtc,
        string openingMarketObservationId)
        => EvaluateFreshFlattenObservation(options, result, notBeforeUtc, nowUtc,
            openingMarketObservationId, Arch7bKnownOrderExecutionProfile.Demo);

    public static LmaxFixArch7bMarketObservationDecision EvaluateFreshFlattenObservation(
        LmaxConnectivityLabOptions options,
        LmaxFixMarketDataSmokeResult result,
        DateTimeOffset notBeforeUtc,
        DateTimeOffset nowUtc,
        string openingMarketObservationId,
        Arch7bKnownOrderExecutionProfile profile)
    {
        var blockers = new List<string>();
        var observedAtUtc = result.ObservationCompletedAtUtc ?? result.CompletedAtUtc;
        Require(!options.AllowOrderSubmission && !options.AllowLiveTrading,
            "ARCH7B_FLATTEN_MARKET_DATA_SESSION_NOT_READ_ONLY");
        Require(options.InstrumentSymbol == profile.Symbol &&
                options.LmaxInstrumentId == profile.SecurityId &&
                options.FixSecurityIdSource == profile.SecurityIdSource,
            "ARCH7B_FLATTEN_BBO_INSTRUMENT_MISMATCH");
        Require(options.MarketDataRequestMode ==
                LmaxFixMarketDataRequestMode.SnapshotPlusUpdates &&
                options.MarketDataRequestMode != LmaxFixMarketDataRequestMode.Auto &&
                options.MarketDepth == 1 &&
                options.MarketDataMaxWaitSeconds <=
                profile.MaximumBboAgeSeconds,
            "ARCH7B_FLATTEN_BBO_REQUEST_MODE_INVALID");
        Require(result.Status == "Ok" && result.FixLoggedOn &&
                result.MarketDataRequestSent && result.MarketDataSnapshotReceived &&
                !result.MarketDataRejectReceived && result.CompleteTopOfBook,
            "ARCH7B_FLATTEN_BBO_UNAVAILABLE_KILL_SWITCH");
        Require(result.RequestMode ==
                LmaxFixMarketDataRequestMode.SnapshotPlusUpdates &&
                !string.IsNullOrWhiteSpace(result.MdReqId),
            "ARCH7B_FLATTEN_BBO_REQUEST_IDENTITY_UNPROVEN");
        Require(result.InboundSequenceIntegrityProven,
            "ARCH7B_FLATTEN_BBO_SEQUENCE_INTEGRITY_UNPROVEN");
        Require(result.StartedAtUtc >= notBeforeUtc &&
                observedAtUtc >= result.StartedAtUtc &&
                observedAtUtc <= nowUtc,
            "ARCH7B_FLATTEN_BBO_NOT_POST_OPENING_TERMINAL");
        Require(nowUtc - observedAtUtc <=
                TimeSpan.FromSeconds(profile.MaximumBboAgeSeconds),
            "ARCH7B_FLATTEN_BBO_STALE");
        Require(IsSha256(result.SnapshotSha256 ?? string.Empty) &&
                !result.SnapshotSha256!.Equals(
                    openingMarketObservationId, StringComparison.OrdinalIgnoreCase),
            "ARCH7B_FLATTEN_MARKET_OBSERVATION_ID_NOT_DISTINCT");
        Require(result.BestBid is > 0m && result.BestAsk is > 0m &&
                result.BestBid <= result.BestAsk,
            "ARCH7B_FLATTEN_BBO_INVALID");

        if (blockers.Count != 0)
            return new(false, blockers, null, null);

        var observation = new Arch7bLmaxBbo(
            profile.Symbol,
            profile.SecurityId,
            result.BestBid!.Value,
            result.BestAsk!.Value,
            observedAtUtc,
            "LMAX",
            result.SnapshotSha256!,
            result.StartedAtUtc,
            result.InboundSequenceIntegrityProven,
            PolygonUsed: false);
        try
        {
            return new(
                true,
                [],
                observation,
                Arch7bKnownOrderQualification.TouchLimit(observation, "SELL", profile));
        }
        catch (InvalidOperationException exception)
        {
            return new(false, [exception.Message], null, null);
        }

        void Require(bool condition, string blocker)
        {
            if (!condition)
                blockers.Add(blocker);
        }
    }

    private static bool IsSha256(string value)
        => value.Length == 64 && value.All(Uri.IsHexDigit);

    private static decimal MaximumSpread()
        => Arch7bKnownOrderQualificationPolicy.MaximumSpreadPips *
           Arch7bKnownOrderQualificationPolicy.PriceIncrement * 10m;

    private static decimal MaximumSpread(Arch7bKnownOrderExecutionProfile profile)
        => Arch7bKnownOrderQualificationPolicy.MaximumSpreadPips *
           profile.PriceIncrement * 10m;

    private static bool IsTickAligned(decimal value)
        => value % Arch7bKnownOrderQualificationPolicy.PriceIncrement == 0m;

    private static bool IsTickAligned(decimal value, Arch7bKnownOrderExecutionProfile profile)
        => profile.PriceIncrement > 0m && value % profile.PriceIncrement == 0m;

    private static bool IsDemoOrUatHost(string? host)
        => !string.IsNullOrWhiteSpace(host) &&
           host.EndsWith(".lmax.com", StringComparison.OrdinalIgnoreCase) &&
           (host.Contains("demo", StringComparison.OrdinalIgnoreCase) ||
            host.Contains("uat", StringComparison.OrdinalIgnoreCase));
}
