using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Application.CanonicalRecorder;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Infrastructure.Lmax.MarketDataOnly;

public sealed record LmaxMarketDataOnlyCatalogInstrument(string Symbol,string SecurityId,string SecurityIdSource,string LmaxSlashSymbol,string EvidenceSource,string PermissionScope);
public sealed record LmaxMarketDataOnlyCapturedFixFrame(byte[] FrameBytes,string RawFixMessage,DateTimeOffset SocketReceiveUtc,long LocalMonotonicTicks,long LocalEventOrder);
public sealed record LmaxMarketDataOnlyFrameExtractionResult(IReadOnlyList<LmaxMarketDataOnlyCapturedFixFrame> Frames,bool Malformed,string? MalformedReason);
public sealed record LmaxMarketDataOnlyCaptureSummary(string Status,string RecorderRunId,string RunRoot,string ConfigHash,int MarketDataReceived,int BboUpdated,int GapEvents,int HealthEvents,int WriterEvents,bool InboundExecutionReportObserved,string? StopReason,string ReplayStatus,long WriterErrorCount,long DroppedEventCount,IReadOnlyDictionary<string,long> EventCounts);

public sealed class LmaxMarketDataOnlyApprovedInstrumentCatalog
{
    private readonly Dictionary<string,LmaxMarketDataOnlyCatalogInstrument> bySymbol;
    public LmaxMarketDataOnlyApprovedInstrumentCatalog(IEnumerable<LmaxMarketDataOnlyCatalogInstrument> instruments)=>bySymbol=instruments.ToDictionary(x=>x.Symbol,StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<LmaxMarketDataOnlyCatalogInstrument> Instruments=>bySymbol.Values.OrderBy(x=>x.Symbol,StringComparer.Ordinal).ToArray();
    public LmaxMarketDataOnlyCatalogInstrument ResolveApproved(string symbol)=>bySymbol.TryGetValue(symbol,out var i)?i:throw new InvalidOperationException($"market_data_only_instrument_not_approved:{symbol}");
    public static LmaxMarketDataOnlyApprovedInstrumentCatalog LoadFromConnectivityLab(string repoRoot)
    {
        var path=Path.Combine(repoRoot,"tools","QQ.Production.Intraday.Lmax.ConnectivityLab","appsettings.json");
        using var doc=JsonDocument.Parse(File.ReadAllText(path));
        var root=doc.RootElement.GetProperty("LmaxConnectivityLab");
        return new LmaxMarketDataOnlyApprovedInstrumentCatalog([new(
            (root.GetProperty("InstrumentSymbol").GetString()??throw new InvalidOperationException("InstrumentSymbol_missing")).ToUpperInvariant(),
            root.GetProperty("LmaxInstrumentId").GetString()??throw new InvalidOperationException("LmaxInstrumentId_missing"),
            root.GetProperty("FixSecurityIdSource").GetString()??"8",
            root.GetProperty("LmaxSlashSymbol").GetString()??throw new InvalidOperationException("LmaxSlashSymbol_missing"),
            path.Replace('\\','/'),
            "M2C1B_EXPLICIT_DEMO_MARKET_DATA_ONLY_SCOPE")]);
    }
}

public sealed class LmaxMarketDataOnlyFixFrameBuffer
{
    private const byte Soh=0x01;
    private readonly List<byte> pending=[];
    private long localEventOrder;
    public int PendingByteCount=>pending.Count;
    public LmaxMarketDataOnlyFrameExtractionResult Append(ReadOnlySpan<byte> bytes,DateTimeOffset socketReceiveUtc,long monotonicTicks)
    {
        for(var i=0;i<bytes.Length;i++)pending.Add(bytes[i]);
        var frames=new List<LmaxMarketDataOnlyCapturedFixFrame>();
        while(true)
        {
            var status=TryTakeFrame(out var frameBytes,out var reason);
            if(status==FrameStatus.Incomplete)return new(frames,false,null);
            if(status==FrameStatus.Malformed){pending.Clear();return new(frames,true,reason);}
            frames.Add(new(frameBytes!,Encoding.ASCII.GetString(frameBytes!),socketReceiveUtc,monotonicTicks,++localEventOrder));
        }
    }
    private FrameStatus TryTakeFrame(out byte[]? frameBytes,out string? reason)
    {
        frameBytes=null;reason=null;
        if(pending.Count==0)return FrameStatus.Incomplete;
        var begin=IndexOfAscii(pending,"8=FIX.",0);
        if(begin<0)return pending.Count>4096?Malformed("begin_string_not_found",out reason):FrameStatus.Incomplete;
        if(begin>0)return Malformed("bytes_before_begin_string",out reason);
        var beginEnd=IndexOfByte(pending,Soh,0);
        if(beginEnd<0)return FrameStatus.Incomplete;
        var bodyLengthStart=beginEnd+1;
        if(!StartsWithAscii(pending,bodyLengthStart,"9="))return Malformed("body_length_missing",out reason);
        var bodyLengthEnd=IndexOfByte(pending,Soh,bodyLengthStart);
        if(bodyLengthEnd<0)return FrameStatus.Incomplete;
        var bodyLengthText=Encoding.ASCII.GetString(pending.Skip(bodyLengthStart+2).Take(bodyLengthEnd-bodyLengthStart-2).ToArray());
        if(!int.TryParse(bodyLengthText,NumberStyles.Integer,CultureInfo.InvariantCulture,out var bodyLength)||bodyLength<0)return Malformed("body_length_invalid",out reason);
        var bodyStart=bodyLengthEnd+1;var checksumStart=bodyStart+bodyLength;var frameEnd=checksumStart+7;
        if(pending.Count<frameEnd)return FrameStatus.Incomplete;
        if(!StartsWithAscii(pending,checksumStart,"10=")||pending[frameEnd-1]!=Soh)return Malformed("checksum_field_missing_or_misaligned",out reason);
        frameBytes=pending.Take(frameEnd).ToArray();pending.RemoveRange(0,frameEnd);return FrameStatus.Complete;
    }
    private static FrameStatus Malformed(string text,out string reason){reason=text;return FrameStatus.Malformed;}
    private static int IndexOfAscii(IReadOnlyList<byte> bytes,string needle,int start){var n=Encoding.ASCII.GetBytes(needle);for(var i=start;i<=bytes.Count-n.Length;i++){var ok=true;for(var j=0;j<n.Length;j++)if(bytes[i+j]!=n[j]){ok=false;break;}if(ok)return i;}return -1;}
    private static bool StartsWithAscii(IReadOnlyList<byte> bytes,int start,string value){var n=Encoding.ASCII.GetBytes(value);if(start<0||start+n.Length>bytes.Count)return false;for(var i=0;i<n.Length;i++)if(bytes[start+i]!=n[i])return false;return true;}
    private static int IndexOfByte(IReadOnlyList<byte> bytes,byte value,int start){for(var i=start;i<bytes.Count;i++)if(bytes[i]==value)return i;return -1;}
    private enum FrameStatus{Incomplete,Complete,Malformed}
}

public static class LmaxMarketDataOnlyConfigHash
{
    public static string Compute(LmaxMarketDataOnlyPreflightConfig config)=>CanonicalRecorderV2.Sha256Text(JsonSerializer.Serialize(config with{ConfigHash=string.Empty},CanonicalRecorderV2Constants.JsonOptions));
    public static bool Matches(LmaxMarketDataOnlyPreflightConfig config)=>string.Equals(Compute(config),config.ConfigHash,StringComparison.OrdinalIgnoreCase);
}

public sealed partial class LmaxMarketDataOnlyCaptureRunner
{
    private const string Component="LMAX_MARKET_DATA_CAPTURE_ONLY";
    private readonly LmaxMarketDataOnlyApprovedInstrumentCatalog catalog;
    public LmaxMarketDataOnlyCaptureRunner(LmaxMarketDataOnlyApprovedInstrumentCatalog catalog)=>this.catalog=catalog;

    public async Task<LmaxMarketDataOnlyCaptureSummary> CaptureSyntheticAsync(LmaxMarketDataOnlyPreflightConfig config,IReadOnlyList<string> rawFixMessages,CancellationToken cancellationToken=default)
    {
        var state=await CaptureState.CreateAsync(config,Resolve(config),"M2C1B_SYNTHETIC_"+Guid.NewGuid().ToString("N",CultureInfo.InvariantCulture)[..12].ToUpperInvariant(),cancellationToken).ConfigureAwait(false);
        await using var _=state.Recorder.ConfigureAwait(false);
        await state.RecordRunStartedAsync(cancellationToken).ConfigureAwait(false);
        await state.RecordSessionStateAsync("synthetic_connected",cancellationToken).ConfigureAwait(false);
        await state.RecordSubscriptionStateAsync("synthetic_subscribed",cancellationToken).ConfigureAwait(false);
        foreach(var raw in rawFixMessages)
        {
            await state.RecordFrameAsync(new(Encoding.ASCII.GetBytes(raw),raw,DateTimeOffset.UtcNow,Stopwatch.GetTimestamp(),state.NextLocalEventOrder()),cancellationToken).ConfigureAwait(false);
            if(state.TotalEvents>=config.MaxEvents)state.MarkStopped("max_events_reached");
            if(state.TotalBytes>=config.MaxTotalBytes)state.MarkStopped("max_total_bytes_reached");
            if(state.ShouldStop)break;
        }
        await state.RecordRunStoppedAsync(cancellationToken).ConfigureAwait(false);
        return await FinalizeAsync(state,cancellationToken).ConfigureAwait(false);
    }

    public static string BuildMarketDataRequest(LmaxMarketDataOnlyPreflightConfig config,LmaxMarketDataOnlyCatalogInstrument instrument,string senderCompId,string targetCompId,int sequenceNumber=2)
    {
        if(config.Instruments.Contains(instrument.Symbol,StringComparer.OrdinalIgnoreCase)==false)throw new InvalidOperationException("instrument_not_in_capture_config");
        var options=new LmaxFixMarketDataRequestOptions(instrument.Symbol,instrument.SecurityId,instrument.LmaxSlashSymbol,1,LmaxFixMarketDataRequestMode.SnapshotPlusUpdates,config.MaxDurationSeconds,config.MaxEvents,LmaxFixMarketDataSymbolEncodingMode.SecurityId,instrument.SecurityIdSource,false);
        return LmaxFixMarketDataCodec.BuildMarketDataRequest(senderCompId,targetCompId,sequenceNumber,"M2C1B_"+Guid.NewGuid().ToString("N",CultureInfo.InvariantCulture)[..16].ToUpperInvariant(),options);
    }

    private IReadOnlyList<LmaxMarketDataOnlyCatalogInstrument> Resolve(LmaxMarketDataOnlyPreflightConfig config)=>config.Instruments.Select(catalog.ResolveApproved).ToArray();
    private static async Task<LmaxMarketDataOnlyCaptureSummary> FinalizeAsync(CaptureState state,CancellationToken cancellationToken)
    {
        await state.Recorder.FlushCheckpointAsync(cancellationToken).ConfigureAwait(false);
        var manifest=await state.Recorder.CompleteAsync(cancellationToken).ConfigureAwait(false);
        var replay=await new CanonicalRecorderV2Replayer().ReplaySnapshotAsync(state.Recorder.RunRoot,cancellationToken).ConfigureAwait(false);
        var summary=new LmaxMarketDataOnlyCaptureSummary(state.Status,manifest.RecorderRunId,state.Recorder.RunRoot,state.Config.ConfigHash,state.MarketDataReceived,state.BboUpdated,state.GapEvents,state.HealthEvents,state.WriterEvents,state.InboundExecutionReportObserved,state.StopReason,replay.ReplayReport.Status,manifest.WriterErrors,manifest.EventsDropped,manifest.EventCounts);
        await File.WriteAllTextAsync(Path.Combine(state.Recorder.RunRoot,"m2c1b_capture_manifest.json"),JsonSerializer.Serialize(summary,CanonicalRecorderV2Constants.JsonOptions),cancellationToken).ConfigureAwait(false);
        return summary;
    }

    private sealed class CaptureState
    {
        private readonly LmaxMarketDataOnlyObservationMapper mapper;
        private readonly HashSet<string> subscriptions;
        private long localEventOrder;
        private long? previousSeqNum;
        public CaptureState(LmaxMarketDataOnlyPreflightConfig config,IReadOnlyList<LmaxMarketDataOnlyCatalogInstrument> instruments,CanonicalRecorderV2 recorder){Config=config;Instruments=instruments;Recorder=recorder;mapper=new LmaxMarketDataOnlyObservationMapper(instruments.Select(x=>new LmaxMarketDataOnlyInstrument(x.SecurityId,x.Symbol,x.LmaxSlashSymbol,x.SecurityIdSource)).ToArray());subscriptions=instruments.Select(x=>x.Symbol).ToHashSet(StringComparer.OrdinalIgnoreCase);}
        public LmaxMarketDataOnlyPreflightConfig Config{get;} public IReadOnlyList<LmaxMarketDataOnlyCatalogInstrument> Instruments{get;} public CanonicalRecorderV2 Recorder{get;}
        public int MarketDataReceived{get;private set;} public int BboUpdated{get;private set;} public int GapEvents{get;private set;} public int HealthEvents{get;private set;} public int WriterEvents{get;private set;} public bool InboundExecutionReportObserved{get;private set;} public string? StopReason{get;private set;} public long TotalEvents{get;private set;} public long TotalBytes{get;private set;}
        public string Status=>IsSuccessfulStopReason(StopReason)?"GO_M2C2_CAPTURE_VALIDATED":"NO_GO_M2C1B";
        public bool ShouldStop=>string.IsNullOrWhiteSpace(StopReason)==false;
        private static bool IsSuccessfulStopReason(string? reason)=>string.IsNullOrWhiteSpace(reason)||reason is "normal_stop" or "max_duration_reached" or "max_events_reached" or "max_total_bytes_reached";
        public static async Task<CaptureState> CreateAsync(LmaxMarketDataOnlyPreflightConfig config,IReadOnlyList<LmaxMarketDataOnlyCatalogInstrument> instruments,string runId,CancellationToken cancellationToken)
        {
            var recorder=await CanonicalRecorderV2.CreateAsync(new CanonicalRecorderV2Options(config.OutputRoot,runId,config.Environment,config.ToolCommit,"git","M2C1B_FINAL_BRIDGE",config.ConfigHash,[Component],config.Instruments,[],[],[],RotateAfterBytes:config.RotateAfterBytes,FlushInterval:TimeSpan.FromMilliseconds(config.FlushIntervalMs)),new SystemRecorderClock(),cancellationToken).ConfigureAwait(false);
            return new CaptureState(config,instruments,recorder);
        }
        public long NextLocalEventOrder()=>++localEventOrder;
        public void MarkStopped(string reason)=>StopReason??=reason;
        public Task RecordRunStartedAsync(CancellationToken ct)=>RecordAsync(new CanonicalRecorderV2Event("RECORDER_RUN_STARTED",Component,"M2C1BMarketDataCapture","v1",new{mode=Config.Mode,environment=Config.Environment,instruments=Config.Instruments,max_duration_seconds=Config.MaxDurationSeconds,max_events=Config.MaxEvents,max_total_bytes=Config.MaxTotalBytes}),ct);
        public Task RecordRunStoppedAsync(CancellationToken ct)=>RecordAsync(new CanonicalRecorderV2Event("RECORDER_RUN_STOPPED",Component,"M2C1BMarketDataCapture","v1",new{stop_reason=StopReason??"normal_stop",market_data_received=MarketDataReceived,bbo_updated=BboUpdated,inbound_execution_report_observed=InboundExecutionReportObserved}),ct);
        public Task RecordSessionStateAsync(string state,CancellationToken ct)=>RecordAsync(new CanonicalRecorderV2Event("MARKET_DATA_SESSION_STATE",Component,"LmaxFixSessionState","v1",new{state}),ct);
        public async Task RecordSubscriptionStateAsync(string state,CancellationToken ct){foreach(var i in Instruments)await RecordAsync(new CanonicalRecorderV2Event("MARKET_DATA_SUBSCRIPTION_STATE",Component,"LmaxMarketDataSubscription","v1",new{state,symbol=i.Symbol,security_id=i.SecurityId,security_id_source=i.SecurityIdSource,slash_symbol=i.LmaxSlashSymbol},InstrumentId:i.SecurityId,Symbol:i.Symbol,Venue:"LMAX_DEMO_READ_ONLY"),ct).ConfigureAwait(false);}
        public async Task RecordHealthAsync(string reason,string detail,CancellationToken ct){HealthEvents++;await RecordAsync(new CanonicalRecorderV2Event("HEALTH_EVENT",Component,"M2C1BMarketDataCaptureHealth","v1",new{reason,detail}),ct).ConfigureAwait(false);}
        public async Task RecordFrameAsync(LmaxMarketDataOnlyCapturedFixFrame frame,CancellationToken ct)
        {
            TotalEvents++;TotalBytes+=frame.FrameBytes.Length;localEventOrder=Math.Max(localEventOrder,frame.LocalEventOrder);
            var msgType=LmaxFixMarketDataCodec.GetMsgType(frame.RawFixMessage)??"(missing)";
            var seq=long.TryParse(LmaxFixMarketDataCodec.GetTag(frame.RawFixMessage,"34"),NumberStyles.Integer,CultureInfo.InvariantCulture,out var s)?s:0;
            var possDup=string.Equals(LmaxFixMarketDataCodec.GetTag(frame.RawFixMessage,"43"),"Y",StringComparison.OrdinalIgnoreCase);
            if(msgType=="8"){InboundExecutionReportObserved=true;MarkStopped("forbidden_inbound_execution_report_35_8");}
            if(msgType is "W" or "X")
            {
                MarketDataReceived++;var rawFrame=new LmaxMarketDataOnlyRawFixFrame(frame.RawFixMessage,frame.SocketReceiveUtc,frame.LocalMonotonicTicks,SessionInstanceId:"M2C1B_CAPTURE");var obs=mapper.Map(rawFrame,subscriptions,previousSeqNum);previousSeqNum=Math.Max(previousSeqNum??0,obs.FixMsgSeqNum);
                await RecordAsync(new CanonicalRecorderV2Event("MARKET_DATA_RECEIVED",Component,"ReadOnlyMarketDataObservationV2","v2",new{local_event_order=frame.LocalEventOrder,obs.SourceMessageType,obs.Symbol,security_id=obs.InstrumentId,bid=obs.BidPrice,ask=obs.AskPrice,obs.BookValid,obs.GapStatus,obs.ParserVersion,obs.RawPayloadSha256},InstrumentId:obs.InstrumentId,Symbol:obs.Symbol,Venue:obs.Venue,SourceTimestampUtc:obs.SourceTimestampUtc,SessionId:obs.SessionInstanceId,FixMsgSeqNum:obs.FixMsgSeqNum,PossDup:obs.PossDup,QuoteEventId:obs.QuoteEventId,BidPrice:obs.BidPrice,BidQuantity:obs.BidQuantity,AskPrice:obs.AskPrice,AskQuantity:obs.AskQuantity,BookValid:obs.BookValid,SourceReceiveSequence:frame.LocalEventOrder),ct).ConfigureAwait(false);
                if(obs.GapStatus is "GAP" or "OUT_OF_ORDER" or "DUPLICATE_SEQ"){GapEvents++;await RecordAsync(new CanonicalRecorderV2Event("MARKET_DATA_GAP",Component,"LmaxFixMarketDataGap","v1",new{local_event_order=frame.LocalEventOrder,obs.GapStatus,obs.FixMsgSeqNum,obs.PossDup},InstrumentId:obs.InstrumentId,Symbol:obs.Symbol,Venue:obs.Venue,FixMsgSeqNum:obs.FixMsgSeqNum,PossDup:obs.PossDup,SourceReceiveSequence:frame.LocalEventOrder),ct).ConfigureAwait(false);}
                if(obs.BookValid){BboUpdated++;await RecordAsync(new CanonicalRecorderV2Event("BBO_UPDATED",Component,"ReadOnlyMarketDataObservationV2","v2",new{local_event_order=frame.LocalEventOrder,obs.QuoteEventId,bid=obs.BidPrice,bid_qty=obs.BidQuantity,ask=obs.AskPrice,ask_qty=obs.AskQuantity,mid=(obs.BidPrice+obs.AskPrice)/2m},InstrumentId:obs.InstrumentId,Symbol:obs.Symbol,Venue:obs.Venue,SourceTimestampUtc:obs.SourceTimestampUtc,SessionId:obs.SessionInstanceId,FixMsgSeqNum:obs.FixMsgSeqNum,PossDup:obs.PossDup,QuoteEventId:obs.QuoteEventId,BidPrice:obs.BidPrice,BidQuantity:obs.BidQuantity,AskPrice:obs.AskPrice,AskQuantity:obs.AskQuantity,BookValid:true,SourceReceiveSequence:frame.LocalEventOrder),ct).ConfigureAwait(false);} else await RecordHealthAsync("invalid_market_data_book",obs.GapStatus,ct).ConfigureAwait(false);
                return;
            }
            HealthEvents++;await RecordAsync(new CanonicalRecorderV2Event("HEALTH_EVENT",Component,"LmaxInboundFixSessionFrame","v1",new{local_event_order=frame.LocalEventOrder,msg_type=msgType,fix_msg_seq_num=seq,poss_dup=possDup,category=msgType=="8"?"FORBIDDEN_INBOUND_EXECUTION_REPORT":"SESSION_OR_NON_BBO_FRAME"},FixMsgSeqNum:seq,PossDup:possDup,SourceReceiveSequence:frame.LocalEventOrder),ct).ConfigureAwait(false);
        }
        private async Task RecordAsync(CanonicalRecorderV2Event e,CancellationToken ct){if(await Recorder.RecordAsync(e,ct).ConfigureAwait(false)==false){WriterEvents++;MarkStopped("canonical_recorder_write_rejected");}}
    }
}
