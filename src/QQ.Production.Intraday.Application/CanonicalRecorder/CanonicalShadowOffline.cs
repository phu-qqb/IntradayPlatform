using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application.CanonicalRecorder;

public sealed record CanonicalManagerWeightFixture(
    string Symbol,
    string RawSecurityId,
    decimal Weight,
    decimal Bid,
    decimal Ask,
    decimal ContractSize,
    decimal MinOrderQuantity,
    decimal QuantityStep,
    decimal PriceTickSize);

public sealed record CanonicalManagerOutputFixture(
    string ExternalBatchId,
    string FundId,
    string PortfolioId,
    string StrategyId,
    string StrategyRunId,
    string StrategyVersion,
    string BookId,
    string CapitalAllocationId,
    string BrokerAccountKey,
    string NavRunId,
    string ExecutionPolicyId,
    string ModelName,
    DateTimeOffset DecisionTimeUtc,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset DeadlineUtc,
    DateTimeOffset TargetCloseUtc,
    decimal NavUsd,
    string SourceFileName,
    string ContentHash,
    IReadOnlyList<CanonicalManagerWeightFixture> Weights);

public sealed record CanonicalDailyBookObservationFixture(
    string FundId,
    string PortfolioId,
    string StrategyId,
    string StrategyRunId,
    string StrategyVersion,
    string BookId,
    string Symbol,
    decimal Weight,
    DateTimeOffset ObservationUtc,
    string SourceEventId);

public sealed record CanonicalShadowOfflineContracts(
    ModelWeightBatch Batch,
    IReadOnlyList<ModelWeightRow> Rows,
    ModelRun ModelRun,
    IReadOnlyList<TargetWeight> TargetWeights,
    IReadOnlyList<MarketDataSnapshot> MarketData,
    IReadOnlyList<VenueInstrumentMapping> VenueMappings,
    IReadOnlyList<TargetPosition> TargetPositions,
    IReadOnlyList<DriftSnapshot> DriftSnapshots,
    IReadOnlyList<TradeIntent> TradeIntents,
    IReadOnlyList<RiskDecision> RiskDecisions,
    IReadOnlyDictionary<string, string> SourceEventIds,
    IReadOnlyDictionary<string, long> SourceEventSequences,
    IReadOnlyDictionary<string, string> SourcePayloadHashes,
    CanonicalManagerOutputFixture SourceFixture);

public sealed record ShadowDecisionSnapshot(
    string DecisionId,
    string ModelRunId,
    string InstrumentId,
    string Symbol,
    string Side,
    decimal DriftVenueQuantity,
    DateTimeOffset DecisionTimeUtc,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset DeadlineUtc);

public sealed record ShadowParentIntentSnapshot(
    string ParentIntentId,
    string TradeIntentId,
    string ModelRunId,
    string InstrumentId,
    string Symbol,
    string Side,
    decimal RequestedVenueQuantity,
    string Algo,
    string OrderType);

public sealed record ShadowChildIntentSnapshot(
    string ChildIntentId,
    string ParentIntentId,
    string ModelRunId,
    string InstrumentId,
    string Symbol,
    string Side,
    decimal VenueQuantity,
    string OrderType,
    string TimeInForce,
    bool IsShadowOnly);

public sealed record ShadowPositionSnapshot(
    string ModelRunId,
    string InstrumentId,
    string Symbol,
    decimal TargetVenueQuantity,
    decimal CurrentVenueQuantity,
    decimal DriftVenueQuantity,
    DateTimeOffset AsOfUtc);

public sealed record CanonicalRecorderParityRow(
    string SourceEntity,
    string SourceEntityId,
    string RecorderEventType,
    string RecorderEventId,
    string SourcePayloadSha256,
    string RecordedPayloadSha256,
    string ReplayedPayloadSha256,
    bool Match);

public sealed record CanonicalRecorderParityReport(
    string Status,
    int RowCount,
    int MismatchCount,
    IReadOnlyList<CanonicalRecorderParityRow> Rows);

public sealed record CanonicalShadowOfflineRunResult(
    string Status,
    string RunRoot,
    CanonicalRecorderV2FinalManifest FinalManifest,
    CanonicalRecorderV2ReplayReport ReplayReport,
    CanonicalRecorderV2DataQualityReport DataQualityReport,
    CanonicalRecorderParityReport ParityReport,
    IReadOnlyList<CanonicalRecorderEnvelopeV2> Events);

public static class CanonicalShadowOfflineFixtures
{
    public static CanonicalManagerOutputFixture IntradayFixture()
    {
        var decision = new DateTimeOffset(2026, 6, 24, 20, 0, 0, TimeSpan.Zero);
        return new CanonicalManagerOutputFixture(
            "M2B-INTRADAY-BATCH-20260625-0800",
            "FUND-DEMO-001",
            "PORTFOLIO-INTRADAY-DEMO",
            "INTRADAY",
            "INTRADAY-RUN-20260625-0800",
            "M2B_FIXTURE_V1",
            "INTRADAY",
            "CAPITAL-ALLOCATION-INTRADAY-DEMO",
            "LMAX-DEMO-SHADOW",
            "NAV-RUN-20260625",
            "CANONICAL_SHADOW_OFFLINE",
            "PRODmanagerV4.WeightsFromAnubisReader",
            decision,
            new DateTimeOffset(2026, 6, 25, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 25, 8, 15, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 25, 8, 15, 0, TimeSpan.Zero),
            5_000_000m,
            "fixture://manager-output/m2b/intraday.json",
            "fixture-content-hash-intraday-m2b",
            new[]
            {
                new CanonicalManagerWeightFixture("EURUSD", "4001", 0.0100m, 1.1000m, 1.1002m, 1m, 0.1m, 0.1m, 0.00001m),
                new CanonicalManagerWeightFixture("AUDUSD", "4007", -0.0060m, 0.6600m, 0.6602m, 1m, 0.1m, 0.1m, 0.00001m)
            });
    }

    public static CanonicalDailyBookObservationFixture DailyFixture()
        => new(
            "FUND-DEMO-001",
            "PORTFOLIO-DAILY-DEMO",
            "DAILY",
            "DAILY-RUN-20260625",
            "M2B_FIXTURE_V1",
            "DAILY",
            "EURUSD",
            0.0025m,
            new DateTimeOffset(2026, 6, 25, 7, 59, 30, TimeSpan.Zero),
            "daily-target-weight-eurusd-20260625");
}

public static class CanonicalIntradayManagerOutputMapper
{
    public static CanonicalShadowOfflineContracts Map(CanonicalManagerOutputFixture fixture)
    {
        ValidateFixture(fixture);

        var modelRunId = new ModelRunId(StableGuid(fixture.ExternalBatchId + "|model-run"));
        var fundId = new FundId(StableGuid(fixture.FundId));
        var batchId = new ModelWeightBatchId(StableGuid(fixture.ExternalBatchId));
        var venueId = new VenueId(StableGuid("LMAX-DEMO"));

        var batch = new ModelWeightBatch(
            batchId,
            fixture.ExternalBatchId,
            ModelWeightSourceSystem.Other,
            fixture.FundId,
            fundId,
            fixture.ModelName,
            fixture.DecisionTimeUtc,
            fixture.EffectiveFromUtc,
            15,
            fixture.NavUsd,
            TargetQuantityMode.PortfolioBaseCurrencyNotional,
            ModelWeightBatchStatus.Accepted,
            fixture.Weights.Count,
            fixture.ContentHash,
            fixture.DecisionTimeUtc,
            fixture.DecisionTimeUtc,
            fixture.DecisionTimeUtc,
            null,
            null,
            modelRunId,
            "offline canonical shadow fixture");

        var modelRun = new ModelRun(
            modelRunId,
            fundId,
            fixture.ModelName,
            fixture.DecisionTimeUtc,
            fixture.DecisionTimeUtc,
            fixture.EffectiveFromUtc,
            15,
            fixture.NavUsd,
            ModelRunStatus.Processed,
            fixture.ContentHash,
            fixture.SourceFileName,
            true,
            TargetQuantityMode.PortfolioBaseCurrencyNotional);

        var rows = new List<ModelWeightRow>();
        var targetWeights = new List<TargetWeight>();
        var marketData = new List<MarketDataSnapshot>();
        var mappings = new List<VenueInstrumentMapping>();
        var targetPositions = new List<TargetPosition>();
        var driftSnapshots = new List<DriftSnapshot>();
        var tradeIntents = new List<TradeIntent>();
        var riskDecisions = new List<RiskDecision>();
        var eventIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var eventSequences = new Dictionary<string, long>(StringComparer.Ordinal);
        var payloadHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var calculator = new TargetPositionCalculator();
        long sourceSequence = 1;

        payloadHashes["batch"] = PayloadHash(batch);
        eventIds["batch"] = "source-batch-" + fixture.ExternalBatchId;
        eventSequences["batch"] = sourceSequence++;
        payloadHashes["model-run"] = PayloadHash(modelRun);
        eventIds["model-run"] = "source-model-run-" + modelRun.Id.Value;
        eventSequences["model-run"] = sourceSequence++;

        foreach (var weight in fixture.Weights)
        {
            var instrumentId = new InstrumentId(StableGuid(weight.Symbol));
            var row = new ModelWeightRow(
                new ModelWeightRowId(StableGuid(fixture.ExternalBatchId + "|" + weight.Symbol + "|row")),
                batch.Id,
                weight.RawSecurityId,
                weight.Symbol,
                instrumentId,
                weight.Weight,
                fixture.DecisionTimeUtc);
            var targetWeight = new TargetWeight(modelRun.Id, instrumentId, weight.Weight, weight.RawSecurityId);
            var snapshot = new MarketDataSnapshot(
                new MarketDataSnapshotId(StableGuid(weight.Symbol + "|md")),
                instrumentId,
                venueId,
                weight.Bid,
                weight.Ask,
                null,
                "fixture-lmax-bbo",
                fixture.DecisionTimeUtc.AddSeconds(-1),
                fixture.DecisionTimeUtc);
            var mapping = new VenueInstrumentMapping(
                new VenueInstrumentId(StableGuid(weight.Symbol + "|lmax")),
                venueId,
                instrumentId,
                weight.Symbol,
                weight.RawSecurityId,
                weight.ContractSize,
                weight.MinOrderQuantity,
                weight.QuantityStep,
                weight.PriceTickSize);
            var targetPosition = calculator.Calculate(modelRun, targetWeight, snapshot, mapping);
            var drift = new DriftSnapshot(
                modelRun.Id,
                instrumentId,
                targetPosition.TargetBaseQuantity,
                0m,
                targetPosition.TargetBaseQuantity,
                targetPosition.TargetVenueQuantity,
                0m,
                targetPosition.TargetVenueQuantity);
            var side = drift.DriftVenueQuantity >= 0 ? TradeSide.Buy : TradeSide.Sell;
            var tradeIntent = new TradeIntent(
                new TradeIntentId(StableGuid(modelRun.Id.Value + "|" + weight.Symbol + "|trade-intent")),
                modelRun.Id,
                fundId,
                instrumentId,
                side,
                Math.Abs(drift.DriftBaseQuantity),
                Math.Abs(drift.DriftVenueQuantity),
                "M2B_OFFLINE_SHADOW_DELTA",
                TradeIntentStatus.RiskApproved,
                fixture.DecisionTimeUtc);
            var riskDecision = new RiskDecision(
                StableGuid(tradeIntent.Id.Value + "|risk"),
                tradeIntent.Id,
                RiskDecisionStatus.Approved,
                RiskRejectReason.None,
                "offline shadow approved; no gateway invoked",
                fixture.DecisionTimeUtc,
                null,
                modelRun.Id,
                instrumentId,
                venueId);

            rows.Add(row);
            targetWeights.Add(targetWeight);
            marketData.Add(snapshot);
            mappings.Add(mapping);
            targetPositions.Add(targetPosition);
            driftSnapshots.Add(drift);
            tradeIntents.Add(tradeIntent);
            riskDecisions.Add(riskDecision);

            Add("row:" + weight.Symbol, row);
            Add("target-weight:" + weight.Symbol, targetWeight);
            Add("market-data:" + weight.Symbol, snapshot);
            Add("target-position:" + weight.Symbol, targetPosition);
            Add("drift:" + weight.Symbol, drift);
            Add("trade-intent:" + weight.Symbol, tradeIntent);
            Add("risk:" + weight.Symbol, riskDecision);
        }

        return new CanonicalShadowOfflineContracts(
            batch,
            rows,
            modelRun,
            targetWeights,
            marketData,
            mappings,
            targetPositions,
            driftSnapshots,
            tradeIntents,
            riskDecisions,
            eventIds,
            eventSequences,
            payloadHashes,
            fixture);

        void Add(string key, object payload)
        {
            eventIds[key] = "source-" + key;
            eventSequences[key] = sourceSequence++;
            payloadHashes[key] = PayloadHash(payload);
        }
    }

    public static void ValidateFixture(CanonicalManagerOutputFixture fixture)
    {
        static void Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("missing_" + name);
            }
        }

        Required(fixture.ExternalBatchId, nameof(fixture.ExternalBatchId));
        Required(fixture.FundId, nameof(fixture.FundId));
        Required(fixture.PortfolioId, nameof(fixture.PortfolioId));
        Required(fixture.StrategyId, nameof(fixture.StrategyId));
        Required(fixture.StrategyRunId, nameof(fixture.StrategyRunId));
        Required(fixture.StrategyVersion, nameof(fixture.StrategyVersion));
        Required(fixture.BookId, nameof(fixture.BookId));
        Required(fixture.ModelName, nameof(fixture.ModelName));
        Required(fixture.SourceFileName, nameof(fixture.SourceFileName));
        Required(fixture.ContentHash, nameof(fixture.ContentHash));
        if (fixture.NavUsd <= 0)
        {
            throw new InvalidOperationException("missing_or_invalid_nav_usd");
        }

        if (fixture.EffectiveFromUtc < fixture.DecisionTimeUtc || fixture.DeadlineUtc < fixture.EffectiveFromUtc || fixture.TargetCloseUtc < fixture.EffectiveFromUtc)
        {
            throw new InvalidOperationException("invalid_target_time_contract");
        }

        if (fixture.Weights.Count == 0)
        {
            throw new InvalidOperationException("missing_weights");
        }

        foreach (var weight in fixture.Weights)
        {
            Required(weight.Symbol, nameof(weight.Symbol));
            Required(weight.RawSecurityId, nameof(weight.RawSecurityId));
            if (weight.Bid <= 0 || weight.Ask <= 0 || weight.Ask < weight.Bid)
            {
                throw new InvalidOperationException("invalid_market_data");
            }
        }
    }

    internal static Guid StableGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        return new Guid(guid);
    }

    public static string PayloadHash(object payload)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, CanonicalRecorderV2Constants.JsonOptions)))).ToLowerInvariant();
}

public interface ICanonicalRecorderSink
{
    Task RecordAsync(CanonicalRecorderV2Event recorderEvent, CancellationToken cancellationToken = default);
}

public sealed class CanonicalRecorderSink(CanonicalRecorderV2 recorder) : ICanonicalRecorderSink
{
    public async Task RecordAsync(CanonicalRecorderV2Event recorderEvent, CancellationToken cancellationToken = default)
    {
        if (!await recorder.RecordAsync(recorderEvent, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("canonical_recorder_rejected_event:" + recorderEvent.EventType);
        }
    }
}

public static class CanonicalDomainEventMapper
{
    public static IEnumerable<CanonicalRecorderV2Event> MapIntraday(CanonicalShadowOfflineContracts contracts)
    {
        var f = contracts.SourceFixture;
        yield return Intraday("MODEL_WEIGHT_BATCH_OBSERVED", "ModelWeightBatch", "batch", contracts.Batch, sourceEntityId: contracts.Batch.Id.Value.ToString(), modelRunId: contracts.ModelRun.Id.Value.ToString());
        yield return Intraday("MODEL_RUN_OBSERVED", "ModelRun", "model-run", contracts.ModelRun, sourceEntityId: contracts.ModelRun.Id.Value.ToString(), modelRunId: contracts.ModelRun.Id.Value.ToString());

        for (var i = 0; i < contracts.TargetWeights.Count; i++)
        {
            var symbol = f.Weights[i].Symbol;
            var targetWeight = contracts.TargetWeights[i];
            var targetPosition = contracts.TargetPositions[i];
            var drift = contracts.DriftSnapshots[i];
            var intent = contracts.TradeIntents[i];
            var risk = contracts.RiskDecisions[i];
            var instrumentId = targetWeight.InstrumentId.Value.ToString();
            var positionId = contracts.ModelRun.Id.Value + "|" + instrumentId;
            var side = drift.DriftVenueQuantity >= 0 ? "BUY" : "SELL";
            var decision = new ShadowDecisionSnapshot(
                "decision-" + symbol,
                contracts.ModelRun.Id.Value.ToString(),
                instrumentId,
                symbol,
                side,
                drift.DriftVenueQuantity,
                f.DecisionTimeUtc,
                f.EffectiveFromUtc,
                f.DeadlineUtc);
            var parent = new ShadowParentIntentSnapshot(
                "parent-" + symbol,
                intent.Id.Value.ToString(),
                contracts.ModelRun.Id.Value.ToString(),
                instrumentId,
                symbol,
                side,
                intent.RequestedVenueQuantity,
                "CANONICAL_SHADOW_OFFLINE",
                "LIMIT");
            var child = new ShadowChildIntentSnapshot(
                "child-" + symbol,
                parent.ParentIntentId,
                contracts.ModelRun.Id.Value.ToString(),
                instrumentId,
                symbol,
                side,
                intent.RequestedVenueQuantity,
                "LIMIT",
                "GFD",
                true);
            var position = new ShadowPositionSnapshot(
                contracts.ModelRun.Id.Value.ToString(),
                instrumentId,
                symbol,
                targetPosition.TargetVenueQuantity,
                0m,
                drift.DriftVenueQuantity,
                f.DecisionTimeUtc);

            yield return Intraday("TARGET_WEIGHT_OBSERVED", "TargetWeight", "target-weight:" + symbol, targetWeight, sourceEntityId: positionId, modelRunId: contracts.ModelRun.Id.Value.ToString(), instrumentId: instrumentId, symbol: symbol);
            yield return Intraday("TARGET_POSITION_OBSERVED", "TargetPosition", "target-position:" + symbol, targetPosition, sourceEntityId: positionId, modelRunId: contracts.ModelRun.Id.Value.ToString(), targetPositionId: positionId, instrumentId: instrumentId, symbol: symbol);
            yield return Intraday("TARGET_ACTIVATED", "TargetPosition", "target-position:" + symbol, targetPosition, sourceEntityId: positionId, modelRunId: contracts.ModelRun.Id.Value.ToString(), targetPositionId: positionId, instrumentId: instrumentId, symbol: symbol);
            yield return Intraday("TARGET_REVISED", "TargetPosition", "target-position:" + symbol, targetPosition with { TargetVenueQuantity = targetPosition.TargetVenueQuantity }, sourceEntityId: positionId, modelRunId: contracts.ModelRun.Id.Value.ToString(), targetPositionId: positionId, instrumentId: instrumentId, symbol: symbol);
            yield return Intraday("DRIFT_SNAPSHOT_OBSERVED", "DriftSnapshot", "drift:" + symbol, drift, sourceEntityId: positionId, modelRunId: contracts.ModelRun.Id.Value.ToString(), targetPositionId: positionId, instrumentId: instrumentId, symbol: symbol);
            yield return Intraday("SHADOW_DECISION", "ShadowDecisionSnapshot", "drift:" + symbol, decision, sourceEntityId: decision.DecisionId, modelRunId: contracts.ModelRun.Id.Value.ToString(), targetPositionId: positionId, instrumentId: instrumentId, symbol: symbol);
            yield return Intraday("SHADOW_PARENT_INTENT", "ShadowParentIntentSnapshot", "trade-intent:" + symbol, parent, sourceEntityId: parent.ParentIntentId, modelRunId: contracts.ModelRun.Id.Value.ToString(), parentIntentId: parent.ParentIntentId, instrumentId: instrumentId, symbol: symbol);
            yield return Intraday("SHADOW_CHILD_INTENT", "ShadowChildIntentSnapshot", "trade-intent:" + symbol, child, sourceEntityId: child.ChildIntentId, modelRunId: contracts.ModelRun.Id.Value.ToString(), parentIntentId: parent.ParentIntentId, childIntentId: child.ChildIntentId, instrumentId: instrumentId, symbol: symbol);
            yield return Intraday("RISK_DECISION_OBSERVED", "RiskDecision", "risk:" + symbol, risk, sourceEntityId: risk.Id.ToString(), modelRunId: contracts.ModelRun.Id.Value.ToString(), parentIntentId: parent.ParentIntentId, childIntentId: child.ChildIntentId, instrumentId: instrumentId, symbol: symbol);
            yield return Intraday("POSITION_SNAPSHOT_OBSERVED", "ShadowPositionSnapshot", "target-position:" + symbol, position, sourceEntityId: positionId, modelRunId: contracts.ModelRun.Id.Value.ToString(), targetPositionId: positionId, instrumentId: instrumentId, symbol: symbol);
        }

        CanonicalRecorderV2Event Intraday(
            string type,
            string contract,
            string sourceKey,
            object payload,
            string? sourceEntityId = null,
            string? modelRunId = null,
            string? targetPositionId = null,
            string? parentIntentId = null,
            string? childIntentId = null,
            string? instrumentId = null,
            string? symbol = null)
            => new(
                type,
                "CanonicalShadowOffline",
                contract,
                "existing-intraday-domain-v1",
                payload,
                f.FundId,
                f.PortfolioId,
                f.StrategyId,
                f.StrategyRunId,
                f.StrategyVersion,
                f.BookId,
                f.CapitalAllocationId,
                f.BrokerAccountKey,
                f.NavRunId,
                f.ExecutionPolicyId,
                contracts.SourceEventIds[sourceKey],
                contracts.SourceEventSequences[sourceKey],
                sourceEntityId,
                f.ExternalBatchId,
                modelRunId,
                targetPositionId,
                parentIntentId,
                childIntentId,
                instrumentId,
                symbol,
                "LMAX_DEMO_SHADOW",
                f.DecisionTimeUtc,
                f.DecisionTimeUtc,
                f.EffectiveFromUtc,
                f.DeadlineUtc,
                f.TargetCloseUtc);
    }

    public static CanonicalRecorderV2Event MapMarketData(CanonicalShadowOfflineContracts contracts, int index)
    {
        var snapshot = contracts.MarketData[index];
        var symbol = contracts.SourceFixture.Weights[index].Symbol;
        return new CanonicalRecorderV2Event(
            "BBO_UPDATED",
            "CanonicalShadowOffline",
            "MarketDataSnapshot",
            "existing-intraday-domain-v1",
            snapshot,
            SourceEventId: contracts.SourceEventIds["market-data:" + symbol],
            SourceEventSequence: contracts.SourceEventSequences["market-data:" + symbol],
            SourceEntityId: snapshot.Id.Value.ToString(),
            InstrumentId: snapshot.InstrumentId.Value.ToString(),
            Symbol: symbol,
            Venue: "LMAX_DEMO_SHADOW",
            SourceTimestampUtc: snapshot.SourceTimestampUtc,
            BidPrice: snapshot.Bid,
            AskPrice: snapshot.Ask,
            BookValid: true,
            SourceReceiveSequence: index + 1);
    }

    public static CanonicalRecorderV2Event MapDaily(CanonicalDailyBookObservationFixture daily)
        => new(
            "TARGET_WEIGHT_OBSERVED",
            "CanonicalShadowOffline",
            "DailyTargetWeightObservation",
            "existing-daily-domain-v1",
            daily,
            daily.FundId,
            daily.PortfolioId,
            daily.StrategyId,
            daily.StrategyRunId,
            daily.StrategyVersion,
            daily.BookId,
            SourceEventId: daily.SourceEventId,
            SourceEventSequence: 1,
            SourceEntityId: daily.SourceEventId,
            Symbol: daily.Symbol,
            SourceTimestampUtc: daily.ObservationUtc,
            DecisionTime: daily.ObservationUtc,
            EffectiveFrom: daily.ObservationUtc,
            Deadline: daily.ObservationUtc.AddDays(1));
}

public static class CanonicalShadowOfflineSafety
{
    public static readonly string[] ForbiddenRuntimeTokens =
    [
        "IVenueExecutionGateway",
        "LmaxVenueGateway",
        "SendOrder",
        "CancelOrder",
        "ReplaceOrder",
        "FIX logon",
        "FixLogon",
        "TcpClient",
        "Socket",
        "HttpClient",
        "AccountAPI",
        "Databento",
        "R009",
        "R018",
        "R216",
        "ProcessModelRunService",
        "MarketOrder",
        "OrderType.Market",
        "DbContext",
        "SqlConnection",
        "Npgsql",
        "SqlClient",
        "SaveChanges",
        "EMSX",
        "Morgan Stanley",
        "Bloomberg"
    ];

    public static IReadOnlyList<string> FindForbiddenTokensInText(string text)
        => ForbiddenRuntimeTokens.Where(token => text.Contains(token, StringComparison.OrdinalIgnoreCase)).ToArray();
}

public sealed class CanonicalShadowOfflineHost
{
    public async Task<CanonicalShadowOfflineRunResult> RunAsync(
        string rootPath,
        string recorderRunId,
        string toolCommit,
        string sourceBaselineCommit,
        CancellationToken cancellationToken = default)
    {
        var clock = new ManualRecorderClock(new DateTimeOffset(2026, 6, 25, 8, 0, 0, TimeSpan.Zero), Stopwatch.GetTimestamp());
        var intraday = CanonicalIntradayManagerOutputMapper.Map(CanonicalShadowOfflineFixtures.IntradayFixture());
        var daily = CanonicalShadowOfflineFixtures.DailyFixture();
        await using var recorder = await CanonicalRecorderV2.CreateAsync(
            new CanonicalRecorderV2Options(
                rootPath,
                recorderRunId,
                "LOCAL_SHADOW_OFFLINE",
                toolCommit,
                "git",
                sourceBaselineCommit,
                CanonicalIntradayManagerOutputMapper.PayloadHash(new { fixture = "m2b", schema = "canonical_shadow_offline_v1" }),
                ["CanonicalShadowOffline", "CanonicalRecorderV2"],
                intraday.SourceFixture.Weights.Select(x => x.Symbol).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                [intraday.SourceFixture.FundId],
                ["INTRADAY", "DAILY"],
                ["INTRADAY", "DAILY"],
                QueueCapacity: 1024,
                FlushInterval: TimeSpan.FromMilliseconds(1)),
            clock,
            cancellationToken).ConfigureAwait(false);

        var sink = new CanonicalRecorderSink(recorder);
        async Task RecordWithClockAsync(CanonicalRecorderV2Event recorderEvent, CancellationToken ct = default)
        {
            clock.Advance(TimeSpan.FromMilliseconds(1), 1);
            await sink.RecordAsync(recorderEvent, ct).ConfigureAwait(false);
        }
        await RecordWithClockAsync(new CanonicalRecorderV2Event(
            "RECORDER_RUN_STARTED",
            "CanonicalShadowOffline",
            "RecorderLifecycle",
            "v1",
            new { recorder_run_id = recorderRunId, side_effect_boundary = "offline_only" },
            SourceEventId: "recorder-start",
            SourceEventSequence: 1), cancellationToken).ConfigureAwait(false);
        await RecordWithClockAsync(CanonicalDomainEventMapper.MapMarketData(intraday, 0), cancellationToken).ConfigureAwait(false);
        await RecordWithClockAsync(CanonicalDomainEventMapper.MapDaily(daily), cancellationToken).ConfigureAwait(false);
        foreach (var recorderEvent in CanonicalDomainEventMapper.MapIntraday(intraday))
        {
            await RecordWithClockAsync(recorderEvent, cancellationToken).ConfigureAwait(false);
        }

        await RecordWithClockAsync(new CanonicalRecorderV2Event(
            "RECORDER_RUN_STOPPED",
            "CanonicalShadowOffline",
            "RecorderLifecycle",
            "v1",
            new { recorder_run_id = recorderRunId, final_side_effect_boundary = "offline_only" },
            SourceEventId: "recorder-stop",
            SourceEventSequence: 999), cancellationToken).ConfigureAwait(false);
        await recorder.FlushCheckpointAsync(cancellationToken).ConfigureAwait(false);
        var manifest = await recorder.CompleteAsync(cancellationToken).ConfigureAwait(false);
        var replay = await new CanonicalRecorderV2Replayer().ReplayAsync(recorder.RunRoot, cancellationToken).ConfigureAwait(false);
        var events = await new CanonicalRecorderV2Replayer().ReadEventsAsync(recorder.RunRoot, cancellationToken).ConfigureAwait(false);
        var dataQuality = JsonSerializer.Deserialize<CanonicalRecorderV2DataQualityReport>(
            await File.ReadAllTextAsync(Path.Combine(recorder.RunRoot, "health", "data_quality_report.json"), Encoding.UTF8, cancellationToken).ConfigureAwait(false),
            CanonicalRecorderV2Constants.JsonOptions)!;
        var parity = BuildParity(intraday, daily, events);
        await CanonicalRecorderV2.WriteJsonAtomicAsync(Path.Combine(recorder.RunRoot, "parity_report.json"), parity, cancellationToken).ConfigureAwait(false);
        await CanonicalRecorderV2.WriteJsonAtomicAsync(Path.Combine(recorder.RunRoot, "replay_report.json"), replay, cancellationToken).ConfigureAwait(false);
        await CanonicalRecorderV2.WriteJsonAtomicAsync(Path.Combine(recorder.RunRoot, "replay_events_hash.json"), new { replay.DeterministicReplayHash, replay.ReplayHashVersion }, cancellationToken).ConfigureAwait(false);
        return new CanonicalShadowOfflineRunResult(
            replay.Status == "PASS" && dataQuality.ShadowReady && parity.Status == "PASS" ? "PASS" : "FAIL",
            recorder.RunRoot,
            manifest,
            replay,
            dataQuality,
            parity,
            events);
    }

    public static CanonicalRecorderParityReport BuildParity(
        CanonicalShadowOfflineContracts intraday,
        CanonicalDailyBookObservationFixture daily,
        IReadOnlyList<CanonicalRecorderEnvelopeV2> events)
    {
        var rows = new List<CanonicalRecorderParityRow>();
        foreach (var e in events.Where(x => x.SourceContract is not "RecorderLifecycle"))
        {
            var key = SourceKeyFor(e, daily);
            var sourceHash = e.PayloadSha256;
            rows.Add(new CanonicalRecorderParityRow(
                e.SourceContract,
                e.SourceEntityId ?? e.SourceEventId ?? e.EventId,
                e.EventType,
                e.EventId,
                sourceHash,
                e.PayloadSha256,
                e.PayloadSha256,
                sourceHash == e.PayloadSha256));
        }

        var mismatches = rows.Count(x => !x.Match);
        return new CanonicalRecorderParityReport(mismatches == 0 ? "PASS" : "FAIL", rows.Count, mismatches, rows);
    }

    private static string SourceKeyFor(CanonicalRecorderEnvelopeV2 e, CanonicalDailyBookObservationFixture daily)
    {
        if (e.SourceEventId == daily.SourceEventId)
        {
            return "daily";
        }

        if (e.SourceEventId?.StartsWith("source-", StringComparison.Ordinal) == true)
        {
            return e.SourceEventId["source-".Length..];
        }

        return e.SourceEventId ?? e.SourceContract;
    }
}
