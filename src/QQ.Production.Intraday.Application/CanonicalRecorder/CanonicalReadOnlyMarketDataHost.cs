using System.Runtime.CompilerServices;

namespace QQ.Production.Intraday.Application.CanonicalRecorder;

public enum ReadOnlyMarketDataFeedState
{
    Created,
    Starting,
    Connected,
    Subscribing,
    Synchronized,
    Stale,
    GapDetected,
    Recovering,
    Failed,
    Stopping,
    Stopped
}

public sealed record ReadOnlyMarketDataObservationV1(
    string Environment,
    string Venue,
    string SessionId,
    string InstrumentId,
    string Symbol,
    string SourceMessageType,
    DateTimeOffset SourceTimestampUtc,
    DateTimeOffset LocalReceiveUtc,
    long LocalMonotonicTicks,
    long FixMsgSeqNum,
    bool PossDup,
    string QuoteEventId,
    decimal BidPrice,
    decimal BidQuantity,
    decimal AskPrice,
    decimal AskQuantity,
    bool BookValid,
    string GapStatus,
    string SubscriptionState,
    string RawPayloadHash);

public sealed record ReadOnlyMarketDataHealth(ReadOnlyMarketDataFeedState State, bool Ready, string Reason);
public sealed record ReadOnlyMarketDataSubscription(string InstrumentId, string Symbol);
public sealed record M2C1ReadOnlyCaptureConfig(string MarketDataEndpointAlias, string MarketDataSessionAlias, IReadOnlyList<string> Instruments, string OutputRoot, TimeSpan QuoteAgeThreshold, long RotateAfterBytes, TimeSpan FlushInterval);

public interface IReadOnlyMarketDataSource
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task SubscribeAsync(IReadOnlyList<ReadOnlyMarketDataSubscription> subscriptions, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ReadOnlyMarketDataObservationV1> ReadMarketDataAsync(CancellationToken cancellationToken = default);
    ReadOnlyMarketDataHealth Health { get; }
    Task StopAsync(CancellationToken cancellationToken = default);
}

public sealed class ReadOnlyMarketDataFeedStateMachine
{
    public ReadOnlyMarketDataFeedState State { get; private set; } = ReadOnlyMarketDataFeedState.Created;
    public string Reason { get; private set; } = "created";

    public bool CanProduceShadowIntent(ReadOnlyMarketDataObservationV1 observation, DateTimeOffset nowUtc, TimeSpan quoteAgeThreshold, bool recorderReady)
    {
        if (!recorderReady || State != ReadOnlyMarketDataFeedState.Synchronized || !observation.BookValid)
        {
            return false;
        }

        return nowUtc - observation.SourceTimestampUtc <= quoteAgeThreshold;
    }

    public void OnStart() => Transition(ReadOnlyMarketDataFeedState.Starting, "start_requested");
    public void OnConnected() => Transition(ReadOnlyMarketDataFeedState.Connected, "connected_read_only_market_data");
    public void OnSubscribing() => Transition(ReadOnlyMarketDataFeedState.Subscribing, "subscriptions_pending");
    public void OnSynchronized() => Transition(ReadOnlyMarketDataFeedState.Synchronized, "feed_synchronized");
    public void OnGap() => Transition(ReadOnlyMarketDataFeedState.GapDetected, "source_sequence_gap_detected");
    public void OnStale() => Transition(ReadOnlyMarketDataFeedState.Stale, "quote_age_exceeded_threshold");
    public void OnRecovering() => Transition(ReadOnlyMarketDataFeedState.Recovering, "recovering_read_only_feed");
    public void OnFailed(string reason) => Transition(ReadOnlyMarketDataFeedState.Failed, reason);
    public void OnStopping() => Transition(ReadOnlyMarketDataFeedState.Stopping, "stop_requested");
    public void OnStopped() => Transition(ReadOnlyMarketDataFeedState.Stopped, "stopped");

    private void Transition(ReadOnlyMarketDataFeedState next, string reason)
    {
        if (State is ReadOnlyMarketDataFeedState.Failed && next is not ReadOnlyMarketDataFeedState.Stopped)
        {
            return;
        }

        State = next;
        Reason = reason;
    }
}

public sealed class PlaybackReadOnlyMarketDataSource(IReadOnlyList<ReadOnlyMarketDataObservationV1> observations) : IReadOnlyMarketDataSource
{
    private readonly ReadOnlyMarketDataFeedStateMachine machine = new();
    private IReadOnlyList<ReadOnlyMarketDataSubscription> subscriptions = Array.Empty<ReadOnlyMarketDataSubscription>();
    private bool started;

    public ReadOnlyMarketDataHealth Health => new(machine.State, machine.State == ReadOnlyMarketDataFeedState.Synchronized, machine.Reason);

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        machine.OnStart();
        machine.OnConnected();
        started = true;
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(IReadOnlyList<ReadOnlyMarketDataSubscription> requestedSubscriptions, CancellationToken cancellationToken = default)
    {
        if (!started)
        {
            machine.OnFailed("subscribe_before_start");
            return Task.CompletedTask;
        }

        machine.OnSubscribing();
        subscriptions = requestedSubscriptions.ToArray();
        machine.OnSynchronized();
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<ReadOnlyMarketDataObservationV1> ReadMarketDataAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (subscriptions.Count == 0)
        {
            machine.OnFailed("missing_subscription");
            yield break;
        }

        long? lastSeq = null;
        foreach (var observation in observations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!subscriptions.Any(x => string.Equals(x.Symbol, observation.Symbol, StringComparison.Ordinal)))
            {
                continue;
            }

            if (lastSeq.HasValue && observation.FixMsgSeqNum > lastSeq.Value + 1 && !observation.PossDup)
            {
                machine.OnGap();
            }
            else if (!observation.BookValid)
            {
                machine.OnFailed("invalid_book");
            }
            else if (!observation.PossDup)
            {
                machine.OnSynchronized();
            }

            lastSeq = Math.Max(lastSeq ?? 0, observation.FixMsgSeqNum);
            await Task.Yield();
            yield return observation;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        machine.OnStopping();
        machine.OnStopped();
        return Task.CompletedTask;
    }
}

public static class ReadOnlyMarketDataFixtures
{
    public static IReadOnlyList<ReadOnlyMarketDataObservationV1> EurUsdAudUsdPlayback()
    {
        var t = new DateTimeOffset(2026, 6, 25, 8, 0, 0, TimeSpan.Zero);
        return
        [
            New("EURUSD", "4001", 1, t, 1.1000m, 1_000_000m, 1.1002m, 1_000_000m, true, false, "OK"),
            New("AUDUSD", "4007", 2, t.AddMilliseconds(20), 0.6600m, 1_000_000m, 0.6602m, 1_000_000m, true, false, "OK"),
            New("EURUSD", "4001", 3, t.AddMilliseconds(40), 1.1000m, 1_000_000m, 1.1002m, 1_000_000m, true, true, "POSS_DUP"),
            New("AUDUSD", "4007", 5, t.AddMilliseconds(60), 0.6599m, 900_000m, 0.6601m, 900_000m, true, false, "GAP"),
            New("EURUSD", "4001", 6, t.AddSeconds(20), 1.0998m, 500_000m, 1.1004m, 500_000m, true, false, "STALE"),
            New("AUDUSD", "4007", 7, t.AddSeconds(21), 0.6603m, 0m, 0.6602m, 0m, false, false, "INVALID_BOOK"),
            New("EURUSD", "4001", 8, t.AddSeconds(22), 1.1001m, 1_100_000m, 1.1003m, 1_100_000m, true, false, "RECOVERED")
        ];

        static ReadOnlyMarketDataObservationV1 New(string symbol, string instrumentId, long seq, DateTimeOffset ts, decimal bid, decimal bidQty, decimal ask, decimal askQty, bool valid, bool possDup, string gap)
            => new("LOCAL_SHADOW_OFFLINE", "LMAX_DEMO_READ_ONLY", "PLAYBACK-M2C0", instrumentId, symbol, "35=W", ts, ts.AddMilliseconds(5), seq * 10, seq, possDup, $"quote-{symbol}-{seq}", bid, bidQty, ask, askQty, valid, gap, "SUBSCRIBED", CanonicalRecorderV2.Sha256Text($"{symbol}|{seq}|{bid}|{ask}"));
    }
}
