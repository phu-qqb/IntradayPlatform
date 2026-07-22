using System.Security.Cryptography;
using System.Text;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxFixOpenOrderCapabilityCategory
{
    LMAX_FIX_MASS_OPEN_ORDER_SNAPSHOT_SUPPORTED,
    LMAX_FIX_KNOWN_ORDER_STATUS_ONLY,
    LMAX_FIX_EXECUTION_REPORT_REPLAY_COMPLETE,
    LMAX_FIX_DROP_COPY_RECONSTRUCTION_COMPLETE,
    LMAX_FIX_PLATFORM_ORDERS_ONLY,
    LMAX_FIX_OPEN_ORDER_DISCOVERY_UNSUPPORTED,
    INCONCLUSIVE
}

public sealed record LmaxFixProfileInventory(
    string Specification,
    string FixVersion,
    string BeginString,
    string Service,
    string SenderCompIdSource,
    string TargetCompId,
    IReadOnlyList<string> ClientToLmaxApplicationMessages,
    IReadOnlyList<string> LmaxToClientApplicationMessages,
    bool OrderStatusRequestSupported,
    bool OrderMassStatusRequestSupported,
    IReadOnlyList<int> SupportedMassStatusReqTypes,
    bool ReplayAtLogonAvailable,
    int ReplayQueueLimit,
    bool ReplaySurvivesGatewayFailure,
    bool ReplaySurvivesSequenceReset,
    bool InitialSnapshotCompletionDocumented,
    bool DropCopyDocumented,
    string AccountScope,
    string ExternalOrManualOrderCoverage);

public sealed record LmaxFixOpenOrderCapabilityDecision(
    LmaxFixOpenOrderCapabilityCategory Category,
    string Reason,
    bool BrokerAuthority,
    bool SnapshotComplete,
    bool EmptyStateMayBeAuthoritative,
    string ExternalOrManualOrderCoverage,
    IReadOnlyList<string> Evidence);

public static class LmaxFixOpenOrderCapabilityClassifier
{
    public static LmaxFixProfileInventory AuthoritativeBrokerFix44Profile() => new(
        Specification: "LMAX Broker FIX Trading API release 99 / supplied FIX 4.4 dictionary",
        FixVersion: "4.4",
        BeginString: "FIX.4.4",
        Service: "Broker FIX Trading",
        SenderCompIdSource: "LMAX_DEMO_SENDER_COMP_ID (value deliberately not recorded)",
        TargetCompId: "LMXBD",
        ClientToLmaxApplicationMessages:
        [
            "NewOrderSingle(35=D)",
            "OrderCancelRequest(35=F)",
            "OrderCancelReplaceRequest(35=G)",
            "OrderStatusRequest(35=H)",
            "TradeCaptureReportRequest(35=AD)"
        ],
        LmaxToClientApplicationMessages:
        [
            "ExecutionReport(35=8)",
            "OrderCancelReject(35=9)",
            "TradeCaptureReport(35=AE)",
            "TradeCaptureReportRequestAck(35=AQ)"
        ],
        OrderStatusRequestSupported: true,
        OrderMassStatusRequestSupported: false,
        SupportedMassStatusReqTypes: [],
        ReplayAtLogonAvailable: true,
        ReplayQueueLimit: 512,
        ReplaySurvivesGatewayFailure: false,
        ReplaySurvivesSequenceReset: false,
        InitialSnapshotCompletionDocumented: false,
        DropCopyDocumented: false,
        AccountScope: "FIX session / LMAX account associated with supplied credentials",
        ExternalOrManualOrderCoverage: "UNPROVEN");

    public static LmaxFixOpenOrderCapabilityDecision Classify(LmaxFixProfileInventory profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.BeginString != "FIX.4.4" || profile.FixVersion != "4.4")
            return Inconclusive("Supplied profile is not the documented LMAX Broker FIX 4.4 profile.");

        if (profile.OrderMassStatusRequestSupported &&
            profile.SupportedMassStatusReqTypes.Count > 0 &&
            profile.InitialSnapshotCompletionDocumented)
            return new(
                LmaxFixOpenOrderCapabilityCategory.LMAX_FIX_MASS_OPEN_ORDER_SNAPSHOT_SUPPORTED,
                "A documented mass request and explicit completion marker are available.",
                BrokerAuthority: true,
                SnapshotComplete: true,
                EmptyStateMayBeAuthoritative: true,
                profile.ExternalOrManualOrderCoverage,
                ["OrderMassStatusRequest", "MassStatusReqType", "explicit snapshot completion"]);

        if (profile.OrderStatusRequestSupported)
            return new(
                LmaxFixOpenOrderCapabilityCategory.LMAX_FIX_KNOWN_ORDER_STATUS_ONLY,
                "LMAX documents OrderStatusRequest(35=H) for one order identified by required ClOrdID(11), Instrument and Side. The supplied dictionary contains no OrderMassStatusRequest(35=AF), no supported MassStatusReqType(585), and no complete account snapshot terminator.",
                BrokerAuthority: false,
                SnapshotComplete: false,
                EmptyStateMayBeAuthoritative: false,
                ExternalOrManualOrderCoverage: "UNPROVEN",
                [
                    "brokerFixTradingGateway-QuickFix-DataDictionary.xml: OrderStatusRequest(35=H)",
                    "OrderStatusRequest requires ClOrdID(11), Instrument and Side",
                    "OrderMassStatusRequest(35=AF) absent",
                    "FIX replay retains at most the most recent 512 messages",
                    "Replay unavailable after gateway failure or sequence reset",
                    "No documented drop-copy session"
                ]);

        return new(
            LmaxFixOpenOrderCapabilityCategory.LMAX_FIX_OPEN_ORDER_DISCOVERY_UNSUPPORTED,
            "No documented complete open-order discovery mechanism is available.",
            BrokerAuthority: false,
            SnapshotComplete: false,
            EmptyStateMayBeAuthoritative: false,
            ExternalOrManualOrderCoverage: "UNPROVEN",
            ["No complete snapshot request or replay contract"]);

        static LmaxFixOpenOrderCapabilityDecision Inconclusive(string reason) => new(
            LmaxFixOpenOrderCapabilityCategory.INCONCLUSIVE,
            reason,
            false,
            false,
            false,
            "UNPROVEN",
            []);
    }
}

public sealed record LmaxFixExecutionReportOrderObservation(
    string AccountId,
    string OrderId,
    string ClOrdId,
    string? OrigClOrdId,
    string SecurityId,
    string Side,
    decimal OrderQty,
    decimal CumQty,
    decimal LeavesQty,
    string OrdStatus,
    string ExecType,
    decimal? Price,
    string? TimeInForce,
    DateTimeOffset TransactTimeUtc,
    long SequenceNumber,
    string SourceSession,
    string SourceMessageSha256,
    bool PossDupFlag);

public sealed record LmaxFixWorkingOrderState(
    string AccountId,
    string OrderId,
    string ClOrdId,
    string? OrigClOrdId,
    string SecurityId,
    string Side,
    decimal OrderQty,
    decimal CumQty,
    decimal LeavesQty,
    string OrdStatus,
    string ExecType,
    decimal? Price,
    string? TimeInForce,
    DateTimeOffset TransactTimeUtc,
    long LastSequenceNumber,
    string SourceSession,
    string SourceMessageSha256,
    string AuthorityClassification,
    bool Working);

public sealed record LmaxFixOrderStateReconstruction(
    IReadOnlyList<LmaxFixWorkingOrderState> Orders,
    IReadOnlyDictionary<string, decimal> SignedReservedWorkingLeavesBySecurityId,
    IReadOnlyList<string> Issues,
    bool SequenceGap,
    bool SnapshotComplete,
    bool EmptyStateWasExplicitlyObserved,
    bool EmptyStateWasInferred,
    bool BrokerAuthority,
    string ExternalOrManualOrderCoverage,
    string ReconstructionSha256);

public sealed class LmaxFixExecutionReportOrderStateMachine
{
    public LmaxFixOrderStateReconstruction Reconstruct(
        IEnumerable<LmaxFixExecutionReportOrderObservation> observations,
        LmaxFixOpenOrderCapabilityDecision capability,
        string expectedAccount,
        string expectedSession,
        bool explicitSnapshotCompletionObserved = false)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(capability);
        var input = observations
            .OrderBy(value => value.SequenceNumber)
            .ThenBy(value => value.SourceMessageSha256, StringComparer.Ordinal)
            .ToArray();
        var issues = new List<string>();
        var seenHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenSequences = new Dictionary<long, string>();
        var states = new Dictionary<string, LmaxFixWorkingOrderState>(StringComparer.OrdinalIgnoreCase);
        long? previousSequence = null;

        foreach (var value in input)
        {
            Validate(value, expectedAccount, expectedSession, issues);
            if (!seenHashes.Add(value.SourceMessageSha256))
                continue;

            if (seenSequences.TryGetValue(value.SequenceNumber, out var existingHash))
            {
                if (!existingHash.Equals(value.SourceMessageSha256, StringComparison.OrdinalIgnoreCase))
                    issues.Add($"CONFLICTING_FIX_SEQUENCE:{value.SequenceNumber}");
                continue;
            }
            seenSequences.Add(value.SequenceNumber, value.SourceMessageSha256);

            if (previousSequence is not null && value.SequenceNumber > previousSequence + 1)
                issues.Add($"FIX_SEQUENCE_GAP:{previousSequence + 1}-{value.SequenceNumber - 1}");
            previousSequence = value.SequenceNumber;

            var chainKey = ResolveChainKey(value, states);
            states[chainKey] = new LmaxFixWorkingOrderState(
                value.AccountId,
                value.OrderId,
                value.ClOrdId,
                value.OrigClOrdId,
                value.SecurityId,
                value.Side,
                value.OrderQty,
                value.CumQty,
                value.LeavesQty,
                value.OrdStatus,
                value.ExecType,
                value.Price,
                value.TimeInForce,
                value.TransactTimeUtc,
                value.SequenceNumber,
                value.SourceSession,
                value.SourceMessageSha256,
                capability.Category == LmaxFixOpenOrderCapabilityCategory.LMAX_FIX_KNOWN_ORDER_STATUS_ONLY
                    ? "KNOWN_PLATFORM_ORDER_ONLY"
                    : "RECONSTRUCTED",
                IsWorking(value.OrdStatus, value.LeavesQty));
        }

        var sequenceGap = issues.Any(value =>
            value.StartsWith("FIX_SEQUENCE_GAP", StringComparison.Ordinal) ||
            value.StartsWith("CONFLICTING_FIX_SEQUENCE", StringComparison.Ordinal));
        var snapshotComplete = explicitSnapshotCompletionObserved &&
            issues.Count == 0 &&
            capability.Category is
                LmaxFixOpenOrderCapabilityCategory.LMAX_FIX_MASS_OPEN_ORDER_SNAPSHOT_SUPPORTED or
                LmaxFixOpenOrderCapabilityCategory.LMAX_FIX_EXECUTION_REPORT_REPLAY_COMPLETE or
                LmaxFixOpenOrderCapabilityCategory.LMAX_FIX_DROP_COPY_RECONSTRUCTION_COMPLETE;
        var ordered = states.Values.OrderBy(value => value.SecurityId, StringComparer.Ordinal)
            .ThenBy(value => value.ClOrdId, StringComparer.Ordinal).ToArray();
        var reserved = ordered.Where(value => value.Working)
            .GroupBy(value => value.SecurityId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(value =>
                    value.Side == "1" ? value.LeavesQty : -value.LeavesQty),
                StringComparer.Ordinal);
        var hash = Sha256(string.Join("\n",
            capability.Category,
            string.Join("|", ordered.Select(value =>
                $"{value.AccountId}:{value.OrderId}:{value.ClOrdId}:{value.OrigClOrdId}:{value.SecurityId}:{value.Side}:{value.OrderQty:G29}:{value.CumQty:G29}:{value.LeavesQty:G29}:{value.OrdStatus}:{value.ExecType}:{value.LastSequenceNumber}:{value.SourceMessageSha256}")),
            string.Join("|", issues.Order(StringComparer.Ordinal))));

        return new(
            ordered,
            reserved,
            issues.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            sequenceGap,
            snapshotComplete,
            EmptyStateWasExplicitlyObserved: snapshotComplete && ordered.Length == 0,
            EmptyStateWasInferred: false,
            BrokerAuthority: snapshotComplete && capability.BrokerAuthority,
            capability.ExternalOrManualOrderCoverage,
            hash);
    }

    private static void Validate(
        LmaxFixExecutionReportOrderObservation value,
        string expectedAccount,
        string expectedSession,
        ICollection<string> issues)
    {
        if (!value.AccountId.Equals(expectedAccount, StringComparison.Ordinal))
            issues.Add($"ACCOUNT_SCOPE_MISMATCH:{value.AccountId}");
        if (!value.SourceSession.Equals(expectedSession, StringComparison.Ordinal))
            issues.Add($"SOURCE_SESSION_MISMATCH:{value.SourceSession}");
        if (string.IsNullOrWhiteSpace(value.OrderId) ||
            string.IsNullOrWhiteSpace(value.ClOrdId) ||
            string.IsNullOrWhiteSpace(value.SecurityId))
            issues.Add("ORDER_IDENTITY_INCOMPLETE");
        if (value.SourceMessageSha256.Length != 64 ||
            !value.SourceMessageSha256.All(Uri.IsHexDigit))
            issues.Add("SOURCE_MESSAGE_SHA256_INVALID");
        if (value.OrderQty < 0m || value.CumQty < 0m || value.LeavesQty < 0m ||
            Math.Abs(value.OrderQty - value.CumQty - value.LeavesQty) > 0.00000001m)
            issues.Add($"LEAVES_QTY_INCONSISTENT:{value.ClOrdId}");
        if (value.SequenceNumber <= 0)
            issues.Add($"FIX_SEQUENCE_INVALID:{value.SequenceNumber}");
    }

    private static string ResolveChainKey(
        LmaxFixExecutionReportOrderObservation value,
        IReadOnlyDictionary<string, LmaxFixWorkingOrderState> states)
    {
        if (!string.IsNullOrWhiteSpace(value.OrigClOrdId))
        {
            var existing = states.FirstOrDefault(pair =>
                pair.Value.ClOrdId.Equals(value.OrigClOrdId, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(existing.Key))
                return existing.Key;
        }

        var byOrder = states.FirstOrDefault(pair =>
            pair.Value.OrderId.Equals(value.OrderId, StringComparison.OrdinalIgnoreCase));
        return !string.IsNullOrWhiteSpace(byOrder.Key) ? byOrder.Key : value.ClOrdId;
    }

    private static bool IsWorking(string ordStatus, decimal leavesQty)
        => leavesQty > 0m && ordStatus is "0" or "1" or "6" or "A" or "E";

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}