using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxDemoOfficialExecutionRecoveryTests
{
    [Fact]
    public async Task OfficialRecoveryReconcilesWithoutInventingFixOrRiskApproval()
    {
        using var f = new Fixture();
        var bytes = File.ReadAllBytes(f.Path);
        var p = f.Prepare();
        Assert.Equal(p.Sha256(), f.Prepare().Sha256());
        Assert.Equal(p.Sha256(), JsonSerializer.Deserialize<LmaxDemoOfficialRecoveryPlan>(JsonSerializer.Serialize(p))!.Sha256());
        Assert.Equal(4, p.Fills.Count);
        Assert.Equal(0m, p.Ledger.Sum(x => x.BaseQuantityDelta));
        Assert.Equal("TEST-PHYSICAL-ENTRY", p.RecoveredChild.ClientOrderId.Value);
        Assert.Equal(OrderType.Limit, p.RecoveredChild.OrderType);
        Assert.Equal(TimeInForce.GFD, p.RecoveredChild.TimeInForce);
        Assert.Equal("TEST-PORTAL-CLOSE", p.ManualChild.ClientOrderId.Value);
        Assert.Equal(TimeInForce.Unknown, p.ManualChild.TimeInForce);
        Assert.Equal(ExecutionAlgo.ExternalManual, p.ManualParent.Algo);
        Assert.Equal(TradeIntentStatus.ExternalExecutionBooked, p.ManualBooking.Status);
        Assert.Contains("no_QQ_order_submission_or_risk_approval", p.ManualBooking.Reason);
        Assert.Equal(ModelRunStatus.RecoveredFromOfficialReport, p.RecoveredModel.Status);
        f.ApplyToMemory(p);
        LmaxDemoOfficialExecutionRecovery.VerifyApplied(f.State, p);
        Assert.Empty(f.State.ExecutionReports);
        Assert.Empty(f.State.RiskDecisions);
        Assert.Null(await new InMemoryIntradayRepository(f.State).GetNextUnprocessedModelRunAsync(default));
        var result = await new EodReconciliationService(new InMemoryIntradayRepository(f.State),
            new InMemoryLmaxEodReportRepository(f.State), new FixedClock(f.Now)).RunAsync(f.Date, "LMAX", "LMAX_DEMO_LOCAL", default);
        Assert.Empty(result.Breaks);
        Assert.Equal(bytes, File.ReadAllBytes(f.Path));
        Assert.Equal(4, f.State.EodReconciliationBreaks.Count); // Historical evidence is retained.
        Assert.Throws<InvalidOperationException>(() => f.Prepare()); // No second import.
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("price")]
    [InlineData("account")]
    [InlineData("instruction")]
    [InlineData("order")]
    [InlineData("missing_close_time")]
    [InlineData("extra_row")]
    [InlineData("duplicate_id")]
    public void ContradictoryOfficialEvidenceCannotChangeTheLedger(string scenario)
    {
        using var f = new Fixture();
        var r = f.State.LmaxIndividualTrades;
        switch (scenario)
        {
            case "quantity": r[0] = r[0] with { UnitsBoughtSold = 999m }; break;
            case "price": r[0] = r[0] with { TradePrice = 9m }; break;
            case "account": r[0] = r[0] with { AccountId = "WRONG-DEMO" }; break;
            case "instruction": r[0] = r[0] with { InstructionId = "different-order" }; break;
            case "order": r[0] = r[0] with { OrderId = "different-order" }; break;
            case "missing_close_time": r[3] = r[3] with { OrderPlacementTimestampUtc = null }; break;
            case "extra_row": r.Add(r[0] with { ExecutionId = "TEST-EXTRA" }); break;
            case "duplicate_id": r[0] = r[0] with { ExecutionId = r[1].ExecutionId }; break;
        }
        Assert.Throws<InvalidOperationException>(() => f.Prepare());
        Assert.Empty(f.State.Fills);
        Assert.Empty(f.State.PositionLedger);
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("same_day")]
    [InlineData("wrong_model")]
    [InlineData("no_owner")]
    [InlineData("bad_hash")]
    public void BoundRequestAndHistoricalScopeAreMandatory(string scenario)
    {
        using var f = new Fixture();
        var request = scenario switch
        {
            "stale" => f.Request with { AcceptedAtUtc = f.Now.AddMinutes(-15) },
            "future" => f.Request with { AcceptedAtUtc = f.Now.AddSeconds(1) },
            "same_day" => f.Request with { ReportDate = DateOnly.FromDateTime(f.Now.UtcDateTime) },
            "wrong_model" => f.Request with { ModelRunId = ModelRunId.New() },
            "no_owner" => f.Request with { OwnerAuthorizationReference = "" },
            _ => f.Request with { ReportSha256 = "not-a-hash" }
        };
        Assert.Throws<InvalidOperationException>(() => f.Prepare(request));
    }

    [Theory]
    [InlineData("partial_import")]
    [InlineData("active_other_order")]
    [InlineData("second_account")]
    [InlineData("position")]
    [InlineData("break_resolved")]
    [InlineData("child_changed")]
    [InlineData("report_alias")]
    [InlineData("mapping")]
    public void ChangedInternalStateRequiresAnotherRecoveryDecision(string scenario)
    {
        using var f = new Fixture();
        var p = f.Prepare();
        switch (scenario)
        {
            case "partial_import": f.State.Fills.Add(p.Fills[0]); break;
            case "active_other_order": f.State.ChildOrders.Add(p.ManualChild with { Status = OrderStatus.Acked }); break;
            case "second_account": f.State.BrokerAccounts.Add(f.State.BrokerAccounts[0] with { Id = BrokerAccountId.New(), ExternalAccountId = "other" }); break;
            case "position": f.State.PositionLedger.Add(p.Ledger[0]); break;
            case "break_resolved": f.State.EodReconciliationBreaks[0] = f.State.EodReconciliationBreaks[0] with { Status = ReconciliationBreakStatus.Resolved }; break;
            case "child_changed": f.State.ChildOrders[0] = f.State.ChildOrders[0] with { Status = OrderStatus.Cancelled }; break;
            case "report_alias": f.State.InstrumentAliases[0] = f.State.InstrumentAliases[0] with { ExternalInstrumentId = "WRONG" }; break;
            case "mapping": f.State.VenueInstrumentMappings[0] = f.State.VenueInstrumentMappings[0] with { VenueInstrumentCode = "OTHER/PAIR" }; break;
        }
        Assert.Throws<InvalidOperationException>(() => f.Prepare());
    }

    [Fact]
    public void ReadbackDetectsTamperingInsteadOfAcceptingADuplicateRecovery()
    {
        using var f = new Fixture();
        var p = f.Prepare();
        f.ApplyToMemory(p);
        f.State.Fills[0] = f.State.Fills[0] with { Price = 2m };
        Assert.Throws<InvalidOperationException>(() => LmaxDemoOfficialExecutionRecovery.VerifyApplied(f.State, p));
    }

    // Entirely synthetic fixture: no private broker rows or real order IDs.
    private sealed class Fixture : IDisposable
    {
        internal PlatformState State { get; } = new();
        internal DateTimeOffset StartAt { get; } = new(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
        internal DateTimeOffset Now => StartAt.AddDays(1);
        internal DateOnly Date => DateOnly.FromDateTime(StartAt.UtcDateTime);
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TEST-recovery-" + Guid.NewGuid().ToString("N") + ".jsonl");
        internal LmaxDemoOfficialRecoveryRequest Request { get; }

        internal Fixture()
        {
            var fund = new Fund(FundId.New(), "TEST", Currency.Usd);
            var account = new BrokerAccount(BrokerAccountId.New(), fund.Id, "LMAX_DEMO_LOCAL", true, "1754288005");
            var venue = new Venue(VenueId.New(), "LMAX", VenueType.Broker);
            var instrument = new Instrument(InstrumentId.New(), "EURUSD", AssetClass.FxSpot, Currency.Eur, Currency.Usd, 5, 2);
            var mapping = new VenueInstrumentMapping(VenueInstrumentId.New(), venue.Id, instrument.Id, "EURUSD", "EUR/USD", 10000m, .1m, .1m, .00001m);
            State.InstrumentAliases.Add(new(InstrumentAliasId.New(), instrument.Id, "LMAX_REPORT", "EUR/USD", "4001", true, StartAt));
            var model = new ModelRun(ModelRunId.New(), fund.Id, "TEST", StartAt, StartAt, StartAt, 15, 100000m, ModelRunStatus.Received, "TEST", "TEST", false);
            var intent = new TradeIntent(TradeIntentId.New(), model.Id, fund.Id, instrument.Id, TradeSide.Buy, 5000m, .5m, "TEST", TradeIntentStatus.Created, StartAt);
            var parent = new ParentOrder(ParentOrderId.New(), intent.Id, new("TEST-PARENT"), OrderSide.Buy, 5000m, ExecutionAlgo.MarketImmediate, OrderStatus.Created, StartAt);
            var child = new ChildOrder(ChildOrderId.New(), parent.Id, venue.Id, new("TEST-PLACEHOLDER"), OrderSide.Buy, OrderType.Market, TimeInForce.IOC, 5000m, .5m, OrderStatus.PendingNew, StartAt);
            State.Funds.Add(fund); State.BrokerAccounts.Add(account); State.Venues.Add(venue); State.Instruments.Add(instrument);
            State.VenueInstrumentMappings.Add(mapping); State.ModelRuns.Add(model); State.TradeIntents.Add(intent);
            State.ParentOrders.Add(parent); State.ChildOrders.Add(child);
            var start = new LmaxDemoSessionStart("TEST-session", "1754288005", "Demo", "TEST-owner-approval", StartAt,
                StartAt.AddHours(2), true, true, true, false, [new("EURUSD", "4001", 10000m)]);
            using (var journal = LmaxDemoSessionJournal.CreateNew(Path))
            {
                var session = LmaxDemoControlledSession.Begin(journal, start, StartAt);
                session.RecordInboundControl(1, "A", StartAt);
                session.BeginCycle(model.Id.Value.ToString("N"), new Dictionary<string, decimal> { ["EURUSD"] = 5000m }, StartAt);
                session.RecordSendIntent(new(model.Id.Value.ToString("N"), child.Id.Value.ToString("N"), "TEST-PHYSICAL-ENTRY", "D", null,
                    "EURUSD", "4001", "BUY", .5m, new string('a', 64), "2", "0", 1.2m), StartAt);
                session.RecordSendCompleted("TEST-PHYSICAL-ENTRY", StartAt.AddSeconds(1));
            }
            decimal[] quantities = [1000m, 1000m, 3000m, -5000m];
            for (int i = 0; i < quantities.Length; i++)
            {
                var close = i == 3;
                State.LmaxIndividualTrades.Add(new(LmaxIndividualTradeId.New(), LmaxReportImportRunId.New(), Date, venue.Id, account.Id,
                    "TEST-EXEC-" + i, null, StartAt.AddSeconds(10 + i), quantities[i] / 10000m, close ? 1.199m : 1.2m, Date,
                    "4001", "EUR/USD", instrument.Id, close ? "TEST-PORTAL-CLOSE" : "TEST-PHYSICAL-ENTRY", close ? "TEST-CLOSE" : "TEST-ENTRY",
                    null, close ? null : 1.2m, StartAt.AddSeconds(close ? 13 : 2), close ? "Market" : "Limit", null, null,
                    null, -.1m, "1754288005", quantities[i], quantities[i] * 1.2m, "", "TEST-RAW-" + i, Now.AddMinutes(-2)));
            }
            var run = new EodReconciliationRun(Guid.NewGuid(), Date, venue.Id, account.Id, Now.AddMinutes(-1), true);
            State.EodReconciliationRuns.Add(run);
            State.EodReconciliationBreaks.AddRange(State.LmaxIndividualTrades.Select(x => new EodReconciliationBreak(Guid.NewGuid(), run.Id,
                ReconciliationBreakType.BrokerFillMissingInternally, ReconciliationBreakSeverity.Blocking, ReconciliationBreakStatus.Open,
                instrument.Id, "TEST missing", x.ExecutionId, null, run.CreatedAtUtc)));
            Request = new("1754288005", start.SessionId, Date, model.Id, child.Id, "TEST-ENTRY", "TEST-CLOSE",
                State.LmaxIndividualTrades.Select(x => x.ExecutionId).ToArray(), new string('b', 64), new string('c', 64),
                "https://github.com/phu-qqb/IntradayPlatform/issues/84#issuecomment-12345", Now);
        }
        internal LmaxDemoOfficialRecoveryPlan Prepare(LmaxDemoOfficialRecoveryRequest? request = null)
        {
            using var journal = LmaxDemoSessionJournal.OpenForInspection(Path);
            return LmaxDemoOfficialExecutionRecovery.Prepare(State, request ?? Request, State.LmaxIndividualTrades,
                LmaxDemoControlledSession.Inspect(journal), journal.Entries, Now);
        }
        internal void ApplyToMemory(LmaxDemoOfficialRecoveryPlan p)
        {
            State.ModelRuns[0] = p.RecoveredModel; State.TradeIntents[0] = p.RecoveredIntent;
            State.ParentOrders[0] = p.RecoveredParent; State.ChildOrders[0] = p.RecoveredChild;
            State.TradeIntents.Add(p.ManualBooking); State.ParentOrders.Add(p.ManualParent); State.ChildOrders.Add(p.ManualChild);
            State.Fills.AddRange(p.Fills); State.PositionLedger.AddRange(p.Ledger);
        }
        public void Dispose() => File.Delete(Path);
    }
}
