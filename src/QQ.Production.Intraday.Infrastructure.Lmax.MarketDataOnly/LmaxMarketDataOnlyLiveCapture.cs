using System.Diagnostics;
using System.Globalization;
using System.Text;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Infrastructure.Lmax.MarketDataOnly;

public sealed partial class LmaxMarketDataOnlyCaptureRunner
{
    public async Task<LmaxMarketDataOnlyCaptureSummary> CaptureLiveAsync(LmaxMarketDataOnlyPreflightConfig config,CancellationToken cancellationToken=default)
    {
        var state=await CaptureState.CreateAsync(config,Resolve(config),"M2C1B_LMAX_DEMO_MD_"+DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff",CultureInfo.InvariantCulture),cancellationToken).ConfigureAwait(false);
        await using var _=state.Recorder.ConfigureAwait(false);
        System.Net.Sockets.TcpClient? client=null;Stream? stream=null;LmaxMarketDataOnlyGuardedWriteStream? guarded=null;
        var started=DateTimeOffset.UtcNow;
        try
        {
            await state.RecordRunStartedAsync(cancellationToken).ConfigureAwait(false);
            var endpoint=QQ.Production.Intraday.Tools.LmaxReadOnlyActivation.LmaxReadOnlyActivationManualDemoEndpointBinding.CreateApprovedDemoMarketData();
            await state.RecordSessionStateAsync("tcp_connecting",cancellationToken).ConfigureAwait(false);
            client=new System.Net.Sockets.TcpClient();
            await client.ConnectAsync(endpoint.RuntimeHost,endpoint.RuntimePort,cancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(15),cancellationToken).ConfigureAwait(false);
            var ssl=new System.Net.Security.SslStream(client.GetStream(),leaveInnerStreamOpen:false);
            await ssl.AuthenticateAsClientAsync(endpoint.RuntimeHost).WaitAsync(TimeSpan.FromSeconds(15),cancellationToken).ConfigureAwait(false);
            stream=ssl;guarded=new LmaxMarketDataOnlyGuardedWriteStream(stream);
            await state.RecordSessionStateAsync("tls_authenticated",cancellationToken).ConfigureAwait(false);
            var outbound=new OutboundFixSession(RequiredEnv("LMAX_DEMO_SENDER_COMP_ID"),RequiredEnv("LMAX_DEMO_TARGET_COMP_ID"));
            var logon=outbound.Build("A",[("98","0"),("108","30"),("141","Y"),("553",RequiredEnv("LMAX_DEMO_FIX_USERNAME")),("554",RequiredEnv("LMAX_DEMO_FIX_PASSWORD"))]);
            await guarded.WriteAsync(Encoding.ASCII.GetBytes(logon),cancellationToken).ConfigureAwait(false);await guarded.FlushAsync(cancellationToken).ConfigureAwait(false);
            await state.RecordSessionStateAsync("fix_logon_sent",cancellationToken).ConfigureAwait(false);
            await ReadUntilLogonAckAsync(guarded,state,cancellationToken).ConfigureAwait(false);
            if(state.ShouldStop==false)
            {
                await guarded.WriteAsync(Encoding.ASCII.GetBytes(BuildMarketDataRequest(config,state.Instruments.Single(),outbound.SenderCompId,outbound.TargetCompId,outbound.NextSequence())),cancellationToken).ConfigureAwait(false);await guarded.FlushAsync(cancellationToken).ConfigureAwait(false);
                await state.RecordSubscriptionStateAsync("md_request_sent",cancellationToken).ConfigureAwait(false);
                await ReadCaptureLoopAsync(config,guarded,state,started,outbound,cancellationToken).ConfigureAwait(false);
                await guarded.WriteAsync(Encoding.ASCII.GetBytes(outbound.Build("5",[])),cancellationToken).ConfigureAwait(false);await guarded.FlushAsync(cancellationToken).ConfigureAwait(false);
                await state.RecordSessionStateAsync("logout_sent_and_socket_closing",cancellationToken).ConfigureAwait(false);
            }
        }
        catch(Exception ex) when(ex is IOException or System.Net.Sockets.SocketException or TimeoutException or OperationCanceledException or InvalidOperationException or System.Security.Authentication.AuthenticationException)
        {state.MarkStopped("transport_exception_sanitized:"+ex.GetType().Name);await state.RecordHealthAsync("transport_exception_sanitized",ex.GetType().Name,cancellationToken).ConfigureAwait(false);}
        finally{guarded?.Dispose();stream?.Dispose();client?.Dispose();}
        await state.RecordRunStoppedAsync(cancellationToken).ConfigureAwait(false);
        return await FinalizeAsync(state,cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReadCaptureLoopAsync(LmaxMarketDataOnlyPreflightConfig config,Stream stream,CaptureState state,DateTimeOffset started,OutboundFixSession outbound,CancellationToken cancellationToken)
    {
        var buffer=new byte[8192];var frames=new LmaxMarketDataOnlyFixFrameBuffer();
        while(state.ShouldStop==false&&cancellationToken.IsCancellationRequested==false)
        {
            if((DateTimeOffset.UtcNow-started).TotalSeconds>=config.MaxDurationSeconds){state.MarkStopped("max_duration_reached");break;}
            var read=await stream.ReadAsync(buffer.AsMemory(0,buffer.Length),cancellationToken).ConfigureAwait(false);
            if(read==0){state.MarkStopped("socket_remote_closed");break;}
            var extracted=frames.Append(buffer.AsSpan(0,read),DateTimeOffset.UtcNow,Stopwatch.GetTimestamp());
            if(extracted.Malformed){state.MarkStopped("malformed_inbound_fix_frame:"+extracted.MalformedReason);break;}
            foreach(var frame in extracted.Frames)
            {
                await state.RecordFrameAsync(frame,cancellationToken).ConfigureAwait(false);
                if(LmaxFixMarketDataCodec.GetMsgType(frame.RawFixMessage)=="1")
                {
                    var testReqId=LmaxFixMarketDataCodec.GetTag(frame.RawFixMessage,"112");
                    var fields=string.IsNullOrWhiteSpace(testReqId)?Array.Empty<(string Tag,string Value)>():[("112",testReqId)];
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(outbound.Build("0",fields)),cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    await state.RecordSessionStateAsync("heartbeat_sent_for_test_request",cancellationToken).ConfigureAwait(false);
                }
                if(state.TotalEvents>=config.MaxEvents){state.MarkStopped("max_events_reached");break;}
                if(state.TotalBytes>=config.MaxTotalBytes){state.MarkStopped("max_total_bytes_reached");break;}
            }
        }
    }

    private static async Task ReadUntilLogonAckAsync(Stream stream,CaptureState state,CancellationToken cancellationToken)
    {
        var deadline=DateTimeOffset.UtcNow.AddSeconds(15);var buffer=new byte[8192];var frames=new LmaxMarketDataOnlyFixFrameBuffer();
        while(DateTimeOffset.UtcNow<deadline)
        {
            var read=await stream.ReadAsync(buffer.AsMemory(0,buffer.Length),cancellationToken).ConfigureAwait(false);
            if(read==0){state.MarkStopped("socket_closed_before_logon_ack");return;}
            var extracted=frames.Append(buffer.AsSpan(0,read),DateTimeOffset.UtcNow,Stopwatch.GetTimestamp());
            foreach(var frame in extracted.Frames)
            {
                await state.RecordFrameAsync(frame,cancellationToken).ConfigureAwait(false);
                var msgType=LmaxFixMarketDataCodec.GetMsgType(frame.RawFixMessage);
                if(msgType=="A"){await state.RecordSessionStateAsync("fix_logon_acknowledged",cancellationToken).ConfigureAwait(false);return;}
                if(msgType is "5" or "3"){state.MarkStopped("fix_logon_rejected_or_logged_out");return;}
            }
        }
        state.MarkStopped("fix_logon_ack_timeout");
    }

    private sealed class OutboundFixSession
    {
        public string SenderCompId{get;}
        public string TargetCompId{get;}
        private int sequenceNumber=1;
        public OutboundFixSession(string senderCompId,string targetCompId){SenderCompId=senderCompId;TargetCompId=targetCompId;}
        public int NextSequence()=>sequenceNumber++;
        public string Build(string messageType,IReadOnlyList<(string Tag,string Value)> fields)=>LmaxFixMarketDataCodec.BuildMessage(messageType,NextSequence(),SenderCompId,TargetCompId,fields);
    }
    private static string RequiredEnv(string name)=>Environment.GetEnvironmentVariable(name) is {Length:>0} value?value:throw new InvalidOperationException("required_environment_credential_label_missing:"+name);
}
