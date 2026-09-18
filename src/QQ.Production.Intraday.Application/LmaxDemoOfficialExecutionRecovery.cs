using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public sealed record LmaxDemoOfficialRecoveryRequest(
    string AccountId, string SessionId, DateOnly ReportDate, ModelRunId ModelRunId, ChildOrderId InitialChildId,
    string EntryBrokerOrderId, string ManualCloseBrokerOrderId, IReadOnlyList<string> ExpectedExecutionIds,
    string ReportSha256, string JournalSha256, string OwnerAuthorizationReference, DateTimeOffset AcceptedAtUtc);

public sealed record LmaxDemoOfficialRecoveryBefore(ModelRun Model, TradeIntent Intent, ParentOrder Parent, ChildOrder Child,
    BrokerAccount Account, VenueInstrumentMapping Mapping, InstrumentAlias ReportAlias, IReadOnlyList<EodReconciliationBreak> OpenBreaks);

public sealed record LmaxDemoOfficialRecoveryPlan(
    Guid RecoveryId, LmaxDemoOfficialRecoveryRequest Request, LmaxDemoOfficialRecoveryBefore Before,
    ModelRun RecoveredModel, TradeIntent RecoveredIntent, ParentOrder RecoveredParent, ChildOrder RecoveredChild,
    TradeIntent ManualBooking, ParentOrder ManualParent, ChildOrder ManualChild,
    IReadOnlyList<Fill> Fills, IReadOnlyList<PositionLedgerEvent> Ledger,
    IReadOnlyList<LmaxIndividualTrade> OfficialRows)
{
    public const string Source = "LMAX_DEMO_OFFICIAL_REPORT_EXECUTION_RECOVERY_V1";
    public string Sha256() => LmaxDemoOfficialExecutionRecovery.Hash(JsonSerializer.Serialize(this));
}

/// <summary>
/// Pure planning for the explicitly authorized one-entry/one-manual-close Demo
/// recovery. Official report rows become labelled recovered fills, never invented
/// FIX ExecutionReports. The manual booking is a terminal representation of an
/// already executed external order; it is not a new order or risk approval.
/// </summary>
public static class LmaxDemoOfficialExecutionRecovery
{
    private static void Require(bool condition, string code)
    { if (!condition) throw new InvalidOperationException(code); }
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    public static Guid RecoveryId(LmaxDemoOfficialRecoveryRequest r)
        => Identity(r, "recovery", r.ModelRunId.Value.ToString("N"));
    private static Guid Identity(LmaxDemoOfficialRecoveryRequest r, string kind, string id)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes($"{LmaxDemoOfficialRecoveryPlan.Source}|{r.AccountId}|{r.SessionId}|{r.ReportSha256}|{kind}|{id}")).AsSpan(0, 16));

    public static LmaxDemoOfficialRecoveryPlan Prepare(PlatformState state, LmaxDemoOfficialRecoveryRequest r,
        IReadOnlyList<LmaxIndividualTrade> official, LmaxDemoControlledSession session,
        IReadOnlyList<LmaxDemoSessionJournal.Entry> journal, DateTimeOffset now)
    {
        Require(r.AccountId == LmaxDemoControlledSession.DemoAccountId && r.SessionId == session.Start.SessionId
            && session.Start.AccountId == r.AccountId && session.Start.Environment == "Demo" && !session.Start.Simulated,
            "RECOVERY_DEMO_SCOPE_REQUIRED");
        Require(!string.IsNullOrWhiteSpace(r.OwnerAuthorizationReference) && r.OwnerAuthorizationReference.StartsWith("https://github.com/phu-qqb/IntradayPlatform/issues/84#issuecomment-", StringComparison.Ordinal),
            "RECOVERY_OWNER_REFERENCE_REQUIRED");
        Require(r.ReportSha256.Length == 64 && r.JournalSha256.Length == 64
            && r.ReportSha256.All(Uri.IsHexDigit) && r.JournalSha256.All(Uri.IsHexDigit), "RECOVERY_HASH_REQUIRED");
        Require(r.AcceptedAtUtc <= now && now - r.AcceptedAtUtc < TimeSpan.FromMinutes(15)
            && r.ReportDate < DateOnly.FromDateTime(now.UtcDateTime), "RECOVERY_HISTORICAL_DAY_AND_FRESH_PLAN_REQUIRED");
        var account = state.BrokerAccounts.Single(x => x.IsEnabled && x.ExternalAccountId == r.AccountId);
        Require(state.BrokerAccounts.Count(x => x.IsEnabled) == 1 && account.AccountCode == "LMAX_DEMO_LOCAL", "RECOVERY_EXACT_SINGLE_ACCOUNT_REQUIRED");
        var model = state.ModelRuns.Single(x => x.Id == r.ModelRunId);
        var intent = state.TradeIntents.Single(x => x.ModelRunId == model.Id);
        var parent = state.ParentOrders.Single(x => x.TradeIntentId == intent.Id);
        var child = state.ChildOrders.Single(x => x.ParentOrderId == parent.Id);
        var venue = state.Venues.Single(x => x.Name == "LMAX" && x.IsEnabled);
        var instrument = state.Instruments.Single(x => x.Id == intent.InstrumentId);
        var mapping = state.VenueInstrumentMappings.Single(x => x.IsEnabled && x.VenueId == venue.Id && x.InstrumentId == instrument.Id);
        var alias = state.InstrumentAliases.Single(x => x.IsEnabled && x.Source == "LMAX_REPORT" && x.InstrumentId == instrument.Id);
        var binding = session.Start.Instruments.Single(x => x.Symbol == instrument.Symbol);
        Require(model.FundId == account.FundId && intent.FundId == account.FundId && !model.IsProcessed
            && DateOnly.FromDateTime(model.AsOfUtc.UtcDateTime) == r.ReportDate && child.Id == r.InitialChildId
            && child.VenueId == venue.Id && intent.Side == TradeSide.Buy && parent.Side == OrderSide.Buy && child.Side == OrderSide.Buy
            && instrument.Symbol == "EURUSD" && mapping.VenueSymbol == "EURUSD" && mapping.VenueInstrumentCode == "EUR/USD"
            && alias.ExternalSymbol == "EUR/USD" && alias.ExternalInstrumentId == "4001"
            && binding.SecurityId == alias.ExternalInstrumentId && binding.ContractSize == mapping.ContractSize && mapping.ContractSize == 10_000m,
            "RECOVERY_INTERNAL_BINDING_MISMATCH");
        Require(parent.Status == OrderStatus.Created && child.Status == OrderStatus.PendingNew
            && model.Status == ModelRunStatus.Received && intent.Status == TradeIntentStatus.Created && intent.RequestedBaseQuantity == parent.BaseQuantity
            && parent.BaseQuantity == child.BaseQuantity && intent.RequestedVenueQuantity == child.VenueQuantity
            && child.BaseQuantity == child.VenueQuantity * mapping.ContractSize,
            "RECOVERY_ORIGINAL_STATE_CHANGED");
        Require(state.ChildOrders.All(x => x.Id == child.Id || Terminal(x.Status))
            && !state.ExecutionReports.Any(x => x.ChildOrderId == child.Id)
            && !state.Fills.Any(x => x.ChildOrderId == child.Id)
            && state.PositionLedger.Where(x => x.FundId == account.FundId).GroupBy(x => x.InstrumentId).All(g => g.Sum(x => x.BaseQuantityDelta) == 0m),
            "RECOVERY_CONCURRENT_OR_PREEXISTING_ACTIVITY");
        var sent = session.KnownOrders.Single();
        var send = sent.Intent;
        Require(sent.SendCompleted && send.MessageType == "D" && send.OriginalClientOrderId is null
            && send.CycleId == model.Id.Value.ToString("N") && send.ParentId == child.Id.Value.ToString("N")
            && send.Side == "BUY" && send.Symbol == instrument.Symbol && send.SecurityId == binding.SecurityId
            && send.VenueQuantity == child.VenueQuantity && send.OrderTypeRaw == "2" && send.TimeInForceRaw == "0" && send.LimitPrice > 0m
            && journal.Count(x => x.Kind == "SendIntent") == 1 && journal.Count(x => x.Kind == "SendCompleted") == 1
            && journal.All(x => x.Kind != "ExecutionReport"), "RECOVERY_DURABLE_SEND_BINDING_MISMATCH");
        Require(session.Start.ObservedFlat && session.Start.ObservedNoWorkingOrders && session.Start.ExclusiveOrderActivityDeclared,
            "RECOVERY_HISTORICAL_START_SCOPE_MISSING");
        var sentAt = journal.Single(x => x.Kind == "SendCompleted").AtUtc;
        Require(official.Count == 4 && r.ExpectedExecutionIds.Count == 4 && r.ExpectedExecutionIds.Distinct().Count() == 4
            && official.Select(x => x.ExecutionId).Distinct().Count() == 4
            && official.Select(x => x.ExecutionId).ToHashSet(StringComparer.Ordinal).SetEquals(r.ExpectedExecutionIds),
            "RECOVERY_EXACT_EXECUTION_SET_REQUIRED");
        Require(official.All(x => x.ReportDate == r.ReportDate && x.TradeDate == r.ReportDate
            && x.AccountId == r.AccountId && x.BrokerAccountId == account.Id && x.VenueId == venue.Id
            && x.InstrumentId == instrument.Id && x.LmaxSymbol == "EUR/USD" && x.LmaxInstrumentId == "4001"
            && x.UnitsBoughtSold != 0m && x.TradeQuantity * mapping.ContractSize == x.UnitsBoughtSold
            && x.TradePrice > 0m && x.TimestampUtc >= sentAt && DateOnly.FromDateTime(x.TimestampUtc.UtcDateTime) == r.ReportDate
            && !string.IsNullOrWhiteSpace(x.RawLine)), "RECOVERY_OFFICIAL_ROW_SCOPE_MISMATCH");
        var entry = official.Where(x => x.OrderId == r.EntryBrokerOrderId).OrderBy(x => x.TimestampUtc).ToArray();
        var close = official.Single(x => x.OrderId == r.ManualCloseBrokerOrderId);
        Require(r.EntryBrokerOrderId != r.ManualCloseBrokerOrderId && entry.Length == 3
            && entry.All(x => x.UnitsBoughtSold > 0 && x.InstructionId == send.ClientOrderId && x.OrderType == "Limit"
                && x.OrderPlacementTimestampUtc >= sentAt && x.OrderPlacementTimestampUtc <= x.TimestampUtc
                && x.TradePrice <= send.LimitPrice)
            && entry.Sum(x => x.UnitsBoughtSold) == intent.RequestedBaseQuantity
            && close.UnitsBoughtSold == -intent.RequestedBaseQuantity && close.TimestampUtc >= entry.Max(x => x.TimestampUtc)
            && close.OrderType == "Market" && !string.IsNullOrWhiteSpace(close.InstructionId)
            && close.InstructionId != send.ClientOrderId && close.OrderPlacementTimestampUtc.HasValue
            && close.OrderPlacementTimestampUtc >= entry.Max(x => x.TimestampUtc) && close.OrderPlacementTimestampUtc <= close.TimestampUtc,
            "RECOVERY_ENTRY_OR_MANUAL_CLOSE_INCONSISTENT");
        var executions = r.ExpectedExecutionIds.ToHashSet(StringComparer.Ordinal);
        Require(!state.Fills.Any(x => executions.Contains(x.BrokerExecutionId))
            && !state.ExecutionReports.Any(x => x.BrokerExecutionId is not null && executions.Contains(x.BrokerExecutionId))
            && !state.PositionLedger.Any(x => executions.Contains(x.ReferenceId)), "RECOVERY_PARTIAL_OR_DUPLICATE_IMPORT");
        var latest = state.EodReconciliationRuns.Where(x => x.ReportDate == r.ReportDate && x.BrokerAccountId == account.Id && x.VenueId == venue.Id)
            .OrderByDescending(x => x.CreatedAtUtc).First();
        var latestBreaks = state.EodReconciliationBreaks.Where(x => x.RunId == latest.Id).ToArray();
        Require(latestBreaks.Length == 4 && latestBreaks.All(x => x.Type == ReconciliationBreakType.BrokerFillMissingInternally
            && x.Status == ReconciliationBreakStatus.Open && x.BrokerExecutionId is not null && executions.Contains(x.BrokerExecutionId)),
            "RECOVERY_CURRENT_BREAK_SET_CHANGED");
        var runs = state.EodReconciliationRuns.Where(x => x.ReportDate == r.ReportDate && x.BrokerAccountId == account.Id && x.VenueId == venue.Id).Select(x => x.Id).ToHashSet();
        var oldBreaks = state.EodReconciliationBreaks.Where(x => runs.Contains(x.RunId) && x.Type == ReconciliationBreakType.BrokerFillMissingInternally
            && x.Status == ReconciliationBreakStatus.Open && x.BrokerExecutionId is not null && executions.Contains(x.BrokerExecutionId)).OrderBy(x => x.Id).ToArray();
        Require(!state.ExceptionCaseLinks.Any(x => oldBreaks.Any(b => x.SourceEntityId == b.Id.ToString("D"))),
            "RECOVERY_LINKED_EXCEPTION_REQUIRES_EXISTING_CASE_WORKFLOW");
        var id = RecoveryId(r);
        var manualIntent = new TradeIntent(new TradeIntentId(Identity(r,"manual-intent",close.OrderId!)), model.Id, account.FundId,
            instrument.Id, TradeSide.Sell, -close.UnitsBoughtSold, -close.TradeQuantity,
            $"EXTERNAL_MANUAL_CLOSE_BOOKING_FROM_OFFICIAL_REPORT;recovery={id:D};brokerOrder={close.OrderId};no_QQ_order_submission_or_risk_approval",
            TradeIntentStatus.ExternalExecutionBooked, r.AcceptedAtUtc);
        var manualParent = new ParentOrder(new ParentOrderId(Identity(r,"manual-parent",close.OrderId!)),manualIntent.Id,
            new ClientOrderId("EOD-MANUAL-"+close.OrderId),OrderSide.Sell,-close.UnitsBoughtSold,ExecutionAlgo.ExternalManual,OrderStatus.Filled,r.AcceptedAtUtc);
        var manualChild = new ChildOrder(new ChildOrderId(Identity(r,"manual-child",close.OrderId!)),manualParent.Id,venue.Id,
            new ClientOrderId(close.InstructionId!),OrderSide.Sell,OrderType.Market,TimeInForce.Unknown,
            -close.UnitsBoughtSold,-close.TradeQuantity,OrderStatus.Filled,close.OrderPlacementTimestampUtc!.Value);
        Require(!state.ChildOrders.Any(x => x.ClientOrderId == manualChild.ClientOrderId || x.ClientOrderId.Value == send.ClientOrderId)
            && !state.ParentOrders.Any(x => x.ClientOrderId == manualParent.ClientOrderId), "RECOVERY_ORDER_ID_COLLISION");
        var recoveredChild = child with {ClientOrderId = new ClientOrderId(send.ClientOrderId),OrderType=OrderType.Limit,
            TimeInForce=TimeInForce.GFD,Status=OrderStatus.Filled};
        var fills = official.OrderBy(x => x.TimestampUtc).Select(x => new Fill(new FillId(Identity(r,"fill",x.ExecutionId)),x.ExecutionId,
            x.OrderId==r.EntryBrokerOrderId?child.Id:manualChild.Id,instrument.Id,venue.Id,x.UnitsBoughtSold>0?TradeSide.Buy:TradeSide.Sell,
            Math.Abs(x.UnitsBoughtSold),Math.Abs(x.TradeQuantity),x.TradePrice,x.TimestampUtc,r.AcceptedAtUtc)).ToArray();
        var ledger = fills.Select(x => new PositionLedgerEvent(Identity(r,"ledger",x.BrokerExecutionId),account.FundId,instrument.Id,
            PositionLedgerEventType.Fill,x.Side==TradeSide.Buy?x.BaseQuantity:-x.BaseQuantity,x.BrokerExecutionId,x.TradeDateUtc)).ToArray();
        return new(id,r,new(model,intent,parent,child,account,mapping,alias,oldBreaks),
            model with {IsProcessed=true,Status=ModelRunStatus.RecoveredFromOfficialReport},intent with {Status=TradeIntentStatus.Ordered},
            parent with {Status=OrderStatus.Filled},recoveredChild,manualIntent,manualParent,manualChild,fills,ledger,
            official.OrderBy(x => x.TimestampUtc).ToArray());
    }

    public static void VerifyApplied(PlatformState state, LmaxDemoOfficialRecoveryPlan p)
    {
        Require(state.ModelRuns.Single(x=>x.Id==p.RecoveredModel.Id)==p.RecoveredModel
            && state.TradeIntents.Single(x=>x.Id==p.RecoveredIntent.Id)==p.RecoveredIntent
            && state.ParentOrders.Single(x=>x.Id==p.RecoveredParent.Id)==p.RecoveredParent
            && state.ChildOrders.Single(x=>x.Id==p.RecoveredChild.Id)==p.RecoveredChild
            && state.TradeIntents.Single(x=>x.Id==p.ManualBooking.Id)==p.ManualBooking
            && state.ParentOrders.Single(x=>x.Id==p.ManualParent.Id)==p.ManualParent
            && state.ChildOrders.Single(x=>x.Id==p.ManualChild.Id)==p.ManualChild,"RECOVERY_ORDER_READBACK_MISMATCH");
        foreach(var f in p.Fills)Require(state.Fills.Single(x=>x.BrokerExecutionId==f.BrokerExecutionId)==f,"RECOVERY_FILL_READBACK_MISMATCH");
        foreach(var e in p.Ledger)Require(state.PositionLedger.Single(x=>x.ReferenceId==e.ReferenceId)==e,"RECOVERY_LEDGER_READBACK_MISMATCH");
        Require(state.ExecutionReports.All(x=>!p.Fills.Any(f=>x.BrokerExecutionId==f.BrokerExecutionId)),"RECOVERY_MUST_NOT_CREATE_FIX_REPORTS");
    }
    private static bool Terminal(OrderStatus s) => s is OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Rejected or OrderStatus.Expired;
}
