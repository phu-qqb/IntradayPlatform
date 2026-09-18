using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Application;

public sealed record LmaxDemoSessionInstrument(string Symbol, string SecurityId, decimal ContractSize);

public sealed record LmaxDemoSessionStart(
    string SessionId,
    string AccountId,
    string Environment,
    string OwnerApprovalId,
    DateTimeOffset ObservedAtUtc,
    DateTimeOffset DeadlineUtc,
    bool ObservedFlat,
    bool ObservedNoWorkingOrders,
    bool ExclusiveOrderActivityDeclared,
    bool Simulated,
    IReadOnlyList<LmaxDemoSessionInstrument> Instruments,
    int InitialObservationMaxAgeSeconds = 900,
    string? InternalBrokerAccountCode = null,
    string ObservationSource = "OFFICIAL_UI",
    string? ObservationEvidencePath = null,
    string? ObservationEvidenceSha256 = null);

public sealed record LmaxDemoSessionSendIntent(
    string CycleId,
    string ParentId,
    string ClientOrderId,
    string MessageType,
    string? OriginalClientOrderId,
    string Symbol,
    string SecurityId,
    string Side,
    decimal VenueQuantity,
    string PayloadSha256,
    string? OrderTypeRaw = null,
    string? TimeInForceRaw = null,
    decimal? LimitPrice = null);

public sealed record LmaxDemoSessionOrder(
    LmaxDemoSessionSendIntent Intent,
    string? BrokerOrderId,
    decimal CumulativeQuantity,
    decimal LeavesQuantity,
    string Status,
    bool SendCompleted,
    bool Acknowledged,
    bool Terminal);

public sealed record LmaxDemoSessionTargetDelta(
    decimal TargetBaseQuantity,
    decimal FilledBaseQuantity,
    decimal KnownWorkingBaseQuantity,
    decimal DeltaBaseQuantity,
    bool RequiresWorkingOrderCancellation);

/// <summary>
/// Append-only, flush-to-disk evidence. A pre-existing journal can only be opened
/// for inspection. Starting another process never recreates an empty session.
/// This store contains normalized order/report facts, never logon frames or secrets.
/// </summary>
public sealed class LmaxDemoSessionJournal : IDisposable
{
    public sealed record Entry(long Index, string PreviousSha256, string Kind, DateTimeOffset AtUtc, string Data, string Sha256);
    private readonly FileStream stream;
    private readonly List<Entry> entries = [];
    public bool Writable { get; }
    public IReadOnlyList<Entry> Entries => entries.ToArray();

    private LmaxDemoSessionJournal(string path, bool create)
    {
        Writable = create;
        stream = new FileStream(path, create ? FileMode.CreateNew : FileMode.Open,
            create ? FileAccess.ReadWrite : FileAccess.Read, create ? FileShare.Read : FileShare.ReadWrite);
        if (create) return;
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                var entry = JsonSerializer.Deserialize<Entry>(line)
                    ?? throw new InvalidDataException("DEMO_SESSION_JOURNAL_INVALID");
                if (entry.Index != entries.Count + 1 || entry.PreviousSha256 != LastHash
                    || entry.Sha256 != Hash(entry.Index, entry.PreviousSha256, entry.Kind, entry.AtUtc, entry.Data))
                    throw new InvalidDataException("DEMO_SESSION_JOURNAL_CHAIN_INVALID");
                entries.Add(entry);
            }
            if (entries.Count == 0) throw new InvalidDataException("DEMO_SESSION_JOURNAL_EMPTY");
        }
        catch { stream.Dispose(); throw; }
    }

    public static LmaxDemoSessionJournal CreateNew(string path) => new(path, true);
    public static LmaxDemoSessionJournal OpenForInspection(string path) => new(path, false);
    private string LastHash => entries.Count == 0 ? new string('0', 64) : entries[^1].Sha256;

    internal Entry Append<T>(string kind, T value, DateTimeOffset atUtc)
    {
        if (!Writable) throw new InvalidOperationException("DEMO_SESSION_RESTART_REQUIRES_RECONCILIATION");
        var data = JsonSerializer.Serialize(value);
        var entry = new Entry(entries.Count + 1, LastHash, kind, atUtc, data,
            Hash(entries.Count + 1, LastHash, kind, atUtc, data));
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry) + "\n");
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
        entries.Add(entry);
        return entry;
    }

    private static string Hash(long index, string previous, string kind, DateTimeOffset atUtc, string data)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { index, previous, kind, atUtc, data }))));

    public void Dispose() => stream.Dispose();
}

/// <summary>
/// A separate continuing-session projection, not a replacement for the original
/// fresh-observation validator. It consumes existing mapped FIX report events.
/// Runtime transport/Worker integration must own continuous reception and call
/// RecordTransportLost on any loss; this class itself opens no connection.
/// The caller must serialize all operations in one session event loop.
/// </summary>
public sealed class LmaxDemoControlledSession
{
    public const string DemoAccountId = "1754288005";
    public static readonly TimeSpan MaximumInboundSilence = TimeSpan.FromSeconds(90);
    private readonly LmaxDemoSessionJournal journal;
    private readonly Dictionary<string, LmaxDemoSessionOrder> orders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LmaxDemoSessionSendIntent> cancels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> executions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, decimal> positions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> parents = new(StringComparer.Ordinal);
    private readonly HashSet<string> cycleIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> completedSends = new(StringComparer.Ordinal);
    private readonly HashSet<string> sendIds = new(StringComparer.Ordinal);
    private Dictionary<string, decimal> targets = new(StringComparer.Ordinal);
    private string? activeCycle;
    private string? blocker;
    private long lastSequence;
    private DateTimeOffset lastInbound;
    private DateTimeOffset lastEvent;
    private DateTimeOffset lastLifecycleEvidence;
    private bool closed;
    public LmaxDemoSessionStart Start { get; private set; } = null!;
    public string? BlockingReason => blocker;
    public bool IsClosed => closed;
    public IReadOnlyList<LmaxDemoSessionOrder> KnownOrders => orders.Values.ToArray();
    public IReadOnlyDictionary<string, decimal> FillDerivedPositions => new Dictionary<string, decimal>(positions);

    private LmaxDemoControlledSession(LmaxDemoSessionJournal journal) => this.journal = journal;

    public static LmaxDemoControlledSession Begin(LmaxDemoSessionJournal journal, LmaxDemoSessionStart start, DateTimeOffset now)
    {
        if (!journal.Writable || journal.Entries.Count != 0) throw Error("RESTART_REQUIRES_RECONCILIATION");
        ValidateInitial(start, now);
        var session = new LmaxDemoControlledSession(journal);
        session.Save("Start", start with { Instruments = start.Instruments.ToArray() }, now);
        return session;
    }

    public static LmaxDemoControlledSession Inspect(LmaxDemoSessionJournal journal)
    {
        if (journal.Writable) throw Error("INSPECTION_REQUIRES_READ_ONLY_JOURNAL");
        var session = new LmaxDemoControlledSession(journal);
        foreach (var entry in journal.Entries) session.Apply(entry);
        if (session.Start is null) throw Error("JOURNAL_START_MISSING");
        return session;
    }

    public void RecordInboundControl(long sequence, string messageType, DateTimeOffset now)
    {
        CheckWritableTime(now);
        CheckInboundContinuity(now);
        if (!new[] { "A", "0", "1" }.Contains(messageType)
            || (messageType == "A") != (lastSequence == 0))
        {
            Fault("UNSUPPORTED_SESSION_TRANSITION", now);
            throw Error("UNSUPPORTED_SESSION_TRANSITION");
        }
        ValidateSequence(sequence, false, now);
        Save("Inbound", new Inbound(sequence, messageType), now);
    }

    public void BeginCycle(string cycleId, IReadOnlyDictionary<string, decimal> targetBaseQuantities, DateTimeOffset now)
    {
        RequireContinuity(now);
        if (string.IsNullOrWhiteSpace(cycleId) || cycleIds.Contains(cycleId)) throw Error("CYCLE_REPLAY_BLOCKED");
        if (targetBaseQuantities.Count == 0 || targetBaseQuantities.Keys.Any(symbol => !Start.Instruments.Any(x => x.Symbol == symbol)))
            throw Error("TARGET_SCOPE_MISMATCH");
        Save("Cycle", new Cycle(cycleId, new Dictionary<string, decimal>(targetBaseQuantities, StringComparer.Ordinal)), now);
    }

    public LmaxDemoSessionTargetDelta GetTargetDelta(string symbol, DateTimeOffset now)
    {
        RequireContinuity(now);
        if (!targets.TryGetValue(symbol, out var target)) throw Error("TARGET_NOT_REGISTERED");
        var binding = Binding(symbol);
        var filled = positions.GetValueOrDefault(symbol);
        var working = orders.Values.Where(x => x.Intent.Symbol == symbol && !x.Terminal)
            .Sum(x => Signed(x.Intent.Side, x.LeavesQuantity * binding.ContractSize));
        var delta = target - filled - working;
        return new(target, filled, working, delta, working != 0m && delta != 0m);
    }

    /// <summary>Must complete durably BEFORE the transport attempts the socket write.</summary>
    public void RecordSendIntent(LmaxDemoSessionSendIntent intent, DateTimeOffset now)
    {
        RequireContinuity(now);
        if (intent.CycleId != activeCycle || string.IsNullOrWhiteSpace(intent.ParentId)
            || string.IsNullOrWhiteSpace(intent.ClientOrderId) || sendIds.Contains(intent.ClientOrderId))
            throw Error("SEND_IDENTITY_OR_REPLAY_INVALID");
        var binding = Binding(intent.Symbol);
        if (binding.SecurityId != intent.SecurityId || intent.Side is not ("BUY" or "SELL")
            || intent.VenueQuantity <= 0m || !IsHash(intent.PayloadSha256)) throw Error("SEND_BINDING_INVALID");
        if (intent.MessageType == "D")
        {
            var delta = GetTargetDelta(intent.Symbol, now);
            if (intent.OriginalClientOrderId is not null || orders.Values.Any(x => x.Intent.Symbol == intent.Symbol && !x.Terminal)
                || Signed(intent.Side, 1m) != Math.Sign(delta.DeltaBaseQuantity)
                || intent.VenueQuantity * binding.ContractSize > Math.Abs(delta.DeltaBaseQuantity))
                throw Error("SEND_NOT_RECONCILED_TO_TARGET");
            var parentKey = intent.CycleId + "|" + intent.Symbol;
            if (parents.TryGetValue(parentKey, out var parent) && parent != intent.ParentId) throw Error("DUPLICATE_ECONOMIC_PARENT");
        }
        else if (intent.MessageType == "F")
        {
            if (intent.OriginalClientOrderId is null || !orders.TryGetValue(intent.OriginalClientOrderId, out var original)
                || original.Terminal || original.Intent.Symbol != intent.Symbol || original.Intent.Side != intent.Side
                || original.Intent.ParentId != intent.ParentId || original.LeavesQuantity != intent.VenueQuantity)
                throw Error("CANCEL_BINDING_INVALID");
        }
        else throw Error("MESSAGE_TYPE_NOT_SUPPORTED");
        Save("SendIntent", intent, now);
    }

    /// <summary>Records successful socket completion, never an acknowledgement or fill.</summary>
    public void RecordSendCompleted(string clientOrderId, DateTimeOffset now)
    {
        CheckWritableTime(now);
        if (!sendIds.Contains(clientOrderId) || completedSends.Contains(clientOrderId))
            throw Error("SEND_COMPLETION_WITHOUT_UNIQUE_INTENT");
        Save("SendCompleted", clientOrderId, now);
    }

    public void RecordExecutionReport(Arch7bExecutionReportEvent report, DateTimeOffset now)
    {
        CheckWritableTime(now);
        var semantic = SemanticHash(report);
        if (!IsHash(report.RawMessageSha256)) RejectReport(report, "REPORT_BINDING_MISMATCH", now);
        if (lastSequence > 0 && now - lastInbound > MaximumInboundSilence)
            RejectReport(report, "INBOUND_CONTINUITY_LOST", now);
        if (executions.TryGetValue(report.ExecId, out var prior))
        {
            if (prior != semantic) RejectReport(report, "CONFLICTING_EXECUTION_ID", now);
            if (!SequenceValid(report.SequenceNumber, report.PossDup)) RejectReport(report, "FIX_SEQUENCE_GAP", now);
            Save("DuplicateReport", report, now);
            return;
        }
        if (report.PossDup && report.SequenceNumber <= lastSequence)
            RejectReport(report, "UNSEEN_REPLAY_REQUIRES_RECONCILIATION", now);
        if (!SequenceValid(report.SequenceNumber, report.PossDup)) RejectReport(report, "FIX_SEQUENCE_GAP", now);
        var key = ResolveOriginal(report);
        var reason = ValidateReport(report, key, now);
        if (reason is not null) RejectReport(report, reason, now);
        Save("Report", report, now);
    }

    private void RejectReport(Arch7bExecutionReportEvent report, string reason, DateTimeOffset now)
    {
        Save("RejectedReport", new RejectedReport(report, reason), now);
        throw Error(reason);
    }

    public void RecordTransportLost(DateTimeOffset now) => Fault("TRANSPORT_LOST_RECONCILIATION_REQUIRED", now);

    // Diagnostic evidence does not acknowledge a message or advance FIX/position state.
    public void RecordProtocolFailure(string code, string messageType, long? sequence,
        string frameSha256, IReadOnlyDictionary<string, string> fields, DateTimeOffset now)
    {
        CheckWritableTime(now);
        Save("ProtocolFailure", new { Code = code, MessageType = messageType, Sequence = sequence,
            FrameSha256 = frameSha256, Fields = fields }, now);
        Fault("FIX_PROTOCOL_RECONCILIATION_REQUIRED", now);
    }

    public void RecordRuntimeFault(string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 80 || reason.Any(c => c is not (>= 'A' and <= 'Z') and not '_'))
            throw Error("RUNTIME_FAULT_CODE_INVALID");
        Fault(reason, now);
    }

    public void RequireContinuity(DateTimeOffset now)
        => RequireReconciledState(now, allowAfterDeadline: false);

    private void RequireReconciledState(DateTimeOffset now, bool allowAfterDeadline)
    {
        CheckWritableTime(now);
        if (closed) throw Error("CLOSED");
        if (blocker is not null) throw Error(blocker);
        if (!allowAfterDeadline && now >= Start.DeadlineUtc) throw Error("DAY_DEADLINE_REACHED");
        CheckInboundContinuity(now);
        if (lastSequence == 0) throw Error("INBOUND_CONTINUITY_UNPROVEN");
        if (orders.Values.Any(x => !x.Acknowledged) || cancels.Count != 0) throw Error("SEND_OR_CANCEL_UNRESOLVED");
    }

    public void CloseAfterFinalObservation(DateTimeOffset observedAtUtc, string approvalId, bool flat, bool noWorkingOrders, DateTimeOffset now)
    {
        RequireReconciledState(now, allowAfterDeadline: true);
        if (!flat || !noWorkingOrders || !ValidApproval(approvalId) || observedAtUtc.Offset != TimeSpan.Zero
            || observedAtUtc > now || observedAtUtc < Start.ObservedAtUtc || observedAtUtc < lastLifecycleEvidence
            || now - observedAtUtc > TimeSpan.FromSeconds(900)
            || positions.Values.Any(x => x != 0m) || orders.Values.Any(x => !x.Terminal)) throw Error("FINAL_RECONCILIATION_REQUIRED");
        Save("Closed", new { observedAtUtc, approvalId, flat, noWorkingOrders }, now);
    }

    private string? ValidateReport(Arch7bExecutionReportEvent r, string key, DateTimeOffset now)
    {
        if (r.SessionId != Start.SessionId || r.AccountId != Start.AccountId || !orders.TryGetValue(key, out var order)) return "UNTRACKED_REPORT";
        if (cancels.TryGetValue(r.ClOrdId, out var cancel))
        {
            if (r.OrigClOrdId != cancel.OriginalClientOrderId) return "REPORT_CLIENT_ID_MISMATCH";
        }
        else if (r.ClOrdId != key || (r.OrigClOrdId is not null && r.OrigClOrdId != key)) return "REPORT_CLIENT_ID_MISMATCH";
        if (r.Symbol != order.Intent.Symbol || r.SecurityId != order.Intent.SecurityId || r.Side != order.Intent.Side
            || r.OrderQty != order.Intent.VenueQuantity || string.IsNullOrWhiteSpace(r.OrderId) || string.IsNullOrWhiteSpace(r.ExecId)
            || (order.BrokerOrderId is not null && order.BrokerOrderId != r.OrderId) || !IsHash(r.RawMessageSha256)) return "REPORT_BINDING_MISMATCH";
        if (r.TransactTimeUtc.Offset != TimeSpan.Zero || r.TransactTimeUtc > now || r.TransactTimeUtc < Start.ObservedAtUtc) return "REPORT_TIME_INVALID";
        if (!new[] { "0", "F", "4", "8", "C", "I", "A", "6" }.Contains(r.ExecType)
            || !new[] { "0", "1", "2", "4", "8", "C", "A", "6" }.Contains(r.OrdStatus)) return "REPORT_STATE_UNSUPPORTED";
        if (order.Terminal) return "REPORT_AFTER_TERMINAL";
        var trade = r.ExecType == "F";
        var stateMatches = r.ExecType switch
        {
            "0" => r.OrdStatus == "0",
            "A" => r.OrdStatus == "A",
            "F" => r.OrdStatus is "1" or "2" or "6",
            "4" => r.OrdStatus == "4",
            "8" => r.OrdStatus == "8",
            "C" => r.OrdStatus == "C",
            "6" => r.OrdStatus == "6",
            "I" => true,
            _ => false
        };
        if (!stateMatches || (r.OrdStatus is "0" or "A" or "8" && r.CumQty != 0m)
            || (r.OrdStatus == "A" && order.Status is not ("PENDING_SEND" or "A"))
            || (r.OrdStatus == "6" && !cancels.Values.Any(x => x.OriginalClientOrderId == key))) return "REPORT_STATE_INCONSISTENT";
        if (trade && (r.LastQty <= 0m || r.LastPx <= 0m)) return "FILL_INVALID";
        if (!trade && r.LastQty != 0m) return "UNMAPPED_FILL";
        if (r.CumQty != order.CumulativeQuantity + (trade ? r.LastQty : 0m)
            || r.CumQty < 0m || r.CumQty > r.OrderQty || r.LeavesQty < 0m) return "CUMULATIVE_FILL_GAP";
        var terminal = IsTerminal(r.OrdStatus);
        if ((terminal && r.LeavesQty != 0m) || (!terminal && r.CumQty + r.LeavesQty != r.OrderQty)
            || (r.OrdStatus == "2" && r.CumQty != r.OrderQty)
            || (r.OrdStatus == "1" && (r.CumQty <= 0m || r.LeavesQty <= 0m))) return "LEAVES_STATE_INCONSISTENT";
        return null;
    }

    private void ValidateSequence(long sequence, bool possDup, DateTimeOffset now)
    {
        if (SequenceValid(sequence, possDup)) return;
        Fault("FIX_SEQUENCE_GAP", now);
        throw Error("FIX_SEQUENCE_GAP");
    }

    private bool SequenceValid(long sequence, bool possDup)
        => sequence == lastSequence + 1 || (possDup && sequence > 0 && sequence <= lastSequence);

    private void CheckWritableTime(DateTimeOffset now)
    {
        if (!journal.Writable) throw Error("RESTART_REQUIRES_RECONCILIATION");
        if (now.Offset != TimeSpan.Zero || now < lastEvent) throw Error("CLOCK_REGRESSION");
    }

    private void CheckInboundContinuity(DateTimeOffset now)
    {
        if (lastSequence > 0 && now - lastInbound > MaximumInboundSilence)
        {
            Fault("INBOUND_CONTINUITY_LOST", now);
            throw Error("INBOUND_CONTINUITY_LOST");
        }
    }

    private void Fault(string reason, DateTimeOffset now) { CheckWritableTime(now); Save("Fault", reason, now); }
    private LmaxDemoSessionInstrument Binding(string symbol) => Start.Instruments.SingleOrDefault(x => x.Symbol == symbol) ?? throw Error("INSTRUMENT_OUTSIDE_SCOPE");
    private string ResolveOriginal(Arch7bExecutionReportEvent report)
        => cancels.TryGetValue(report.ClOrdId, out var cancel) ? cancel.OriginalClientOrderId! : report.OrigClOrdId ?? report.ClOrdId;
    private static bool IsTerminal(string status) => status is "2" or "4" or "8" or "C";
    private static decimal Signed(string side, decimal quantity) => side == "BUY" ? quantity : -quantity;
    private static bool IsHash(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    private static bool ValidApproval(string value) => !string.IsNullOrWhiteSpace(value)
        && !new[] { "NONE", "N/A", "PLACEHOLDER", "TBD", "UNKNOWN" }.Contains(value.Trim().ToUpperInvariant());
    private static InvalidOperationException Error(string code) => new("DEMO_SESSION_" + code);

    private static void ValidateInitial(LmaxDemoSessionStart s, DateTimeOffset now)
    {
        if (s.Environment != "Demo" || s.AccountId != DemoAccountId) throw Error("DEMO_ACCOUNT_REQUIRED");
        if (string.IsNullOrWhiteSpace(s.SessionId) || !ValidApproval(s.OwnerApprovalId)) throw Error("OWNER_APPROVAL_REQUIRED");
        if (!s.ObservedFlat || !s.ObservedNoWorkingOrders || !s.ExclusiveOrderActivityDeclared) throw Error("INITIAL_STATE_OR_EXCLUSIVITY_UNPROVEN");
        if (s.InitialObservationMaxAgeSeconds is < 1 or > 900 || now.Offset != TimeSpan.Zero || s.ObservedAtUtc.Offset != TimeSpan.Zero
            || s.DeadlineUtc.Offset != TimeSpan.Zero || s.ObservedAtUtc > now || now - s.ObservedAtUtc > TimeSpan.FromSeconds(s.InitialObservationMaxAgeSeconds)
            || s.DeadlineUtc <= now || s.DeadlineUtc - now > TimeSpan.FromHours(15)
            || s.DeadlineUtc.UtcDateTime.Date != now.UtcDateTime.Date) throw Error("INITIAL_TIME_BOUNDARY_INVALID");
        if (s.ObservationSource != "OFFICIAL_UI")
        {
            if (s.ObservationSource != LmaxDemoOwnerConfirmedOpening.Source || s.OwnerApprovalId != LmaxDemoOwnerConfirmedOpening.Approval)
                throw Error("UNKNOWN_OBSERVATION_SOURCE");
            LmaxDemoOwnerConfirmedOpening.ValidateFile(s.ObservationEvidencePath!, s.ObservationEvidenceSha256!, s.ObservedAtUtc, now);
        }
        if (s.Instruments.Count == 0 || s.Instruments.Select(x => x.Symbol).Distinct(StringComparer.Ordinal).Count() != s.Instruments.Count
            || s.Instruments.Any(x => string.IsNullOrWhiteSpace(x.Symbol) || string.IsNullOrWhiteSpace(x.SecurityId) || x.ContractSize <= 0m)) throw Error("INSTRUMENT_SCOPE_INVALID");
    }

    private void Save<T>(string kind, T data, DateTimeOffset now)
    {
        try { Apply(journal.Append(kind, data, now)); }
        catch { blocker = "JOURNAL_OR_PROJECTION_FAILED"; throw; }
    }

    private void Apply(LmaxDemoSessionJournal.Entry entry)
    {
        T Read<T>() => JsonSerializer.Deserialize<T>(entry.Data) ?? throw Error("JOURNAL_EVENT_INVALID");
        if (entry.AtUtc.Offset != TimeSpan.Zero || entry.AtUtc < lastEvent) throw Error("JOURNAL_TIME_INVALID");
        switch (entry.Kind)
        {
            case "Start":
                if (Start is not null) throw Error("DUPLICATE_SESSION_START");
                Start = Read<LmaxDemoSessionStart>(); ValidateInitial(Start, entry.AtUtc);
                Start = Start with { Instruments = Array.AsReadOnly(Start.Instruments.ToArray()) }; break;
            case "Inbound":
                var inbound = Read<Inbound>(); lastSequence = inbound.Sequence; lastInbound = entry.AtUtc; break;
            case "Cycle":
                var cycle = Read<Cycle>(); activeCycle = cycle.Id; cycleIds.Add(cycle.Id); targets = cycle.Targets; break;
            case "SendIntent":
                var intent = Read<LmaxDemoSessionSendIntent>();
                sendIds.Add(intent.ClientOrderId);
                if (intent.MessageType == "F") cancels.Add(intent.ClientOrderId, intent);
                else { orders.Add(intent.ClientOrderId, new(intent, null, 0m, intent.VenueQuantity, "PENDING_SEND", false, false, false)); parents[intent.CycleId + "|" + intent.Symbol] = intent.ParentId; }
                break;
            case "SendCompleted":
                var id = Read<string>(); completedSends.Add(id);
                if (orders.TryGetValue(id, out var sent)) orders[id] = sent with { SendCompleted = true };
                break;
            case "Report":
                var report = Read<Arch7bExecutionReportEvent>(); var key = ResolveOriginal(report); var order = orders[key];
                var terminal = IsTerminal(report.OrdStatus);
                orders[key] = order with { BrokerOrderId = report.OrderId, CumulativeQuantity = report.CumQty, LeavesQuantity = report.LeavesQty, Status = report.OrdStatus, Acknowledged = true, Terminal = terminal };
                if (report.ExecType == "F") positions[report.Symbol] = positions.GetValueOrDefault(report.Symbol) + Signed(report.Side, report.LastQty * Binding(report.Symbol).ContractSize);
                if (terminal) foreach (var cancel in cancels.Values.Where(x => x.OriginalClientOrderId == key).ToArray()) cancels.Remove(cancel.ClientOrderId);
                executions.Add(report.ExecId, SemanticHash(report)); lastSequence = Math.Max(lastSequence, report.SequenceNumber);
                lastInbound = entry.AtUtc; lastLifecycleEvidence = entry.AtUtc; break;
            case "DuplicateReport":
                var duplicate = Read<Arch7bExecutionReportEvent>(); lastSequence = Math.Max(lastSequence, duplicate.SequenceNumber); lastInbound = entry.AtUtc; break;
            case "RejectedReport": blocker = Read<RejectedReport>().Reason; break;
            case "ProtocolFailure": blocker = "FIX_PROTOCOL_RECONCILIATION_REQUIRED"; break;
            case "Fault": blocker = Read<string>(); break;
            case "Closed": closed = true; break;
            default: throw Error("JOURNAL_EVENT_KIND_UNKNOWN");
        }
        lastEvent = entry.AtUtc;
    }

    private static string SemanticHash(Arch7bExecutionReportEvent r)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r with { SequenceNumber = 0, PossDup = false, RawMessageSha256 = string.Empty }))));
    private sealed record Inbound(long Sequence, string MessageType);
    private sealed record Cycle(string Id, Dictionary<string, decimal> Targets);
    private sealed record RejectedReport(Arch7bExecutionReportEvent Report, string Reason);
}
