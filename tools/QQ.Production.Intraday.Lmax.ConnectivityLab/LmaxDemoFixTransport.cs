using System.Globalization;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

/// <summary>One reader retains partial and coalesced frames across reads.</summary>
public sealed class LmaxDemoFixFrames
{
    private const int MaximumBody = 65536;
    private readonly List<byte> pending = [];
    public void Append(ReadOnlySpan<byte> bytes)
    {
        if (pending.Count + bytes.Length > MaximumBody * 2 || bytes.ContainsAnyInRange((byte)128, byte.MaxValue))
            throw new InvalidDataException("DEMO_FIX_FRAME_LIMIT_OR_ENCODING");
        pending.AddRange(bytes.ToArray());
    }
    public bool TryRead(out string? frame)
    {
        frame = null;
        const string prefix = "8=FIX.4.4\u00019=";
        for (var i = 0; i < Math.Min(pending.Count, prefix.Length); i++)
            if (pending[i] != prefix[i]) throw new InvalidDataException("DEMO_FIX_HEADER_INVALID");
        if (pending.Count <= prefix.Length) return false;
        var lengthEnd = pending.IndexOf(1, prefix.Length);
        if (lengthEnd < 0)
        {
            if (pending.Count > prefix.Length + 6) throw new InvalidDataException("DEMO_FIX_LENGTH_INVALID");
            return false;
        }
        var lengthText = Encoding.ASCII.GetString(pending.GetRange(prefix.Length, lengthEnd - prefix.Length).ToArray());
        if (lengthText.Length == 0 || lengthText.Any(c => c is < '0' or > '9')
            || !int.TryParse(lengthText, NumberStyles.None, CultureInfo.InvariantCulture, out var bodyLength)
            || bodyLength < 5 || bodyLength > MaximumBody)
            throw new InvalidDataException("DEMO_FIX_LENGTH_INVALID");
        var checksumAt = lengthEnd + 1 + bodyLength;
        var total = checksumAt + 7;
        if (pending.Count < total) return false;
        if (pending[checksumAt - 1] != 1 || pending[checksumAt] != '1' || pending[checksumAt + 1] != '0'
            || pending[checksumAt + 2] != '=' || pending[total - 1] != 1)
            throw new InvalidDataException("DEMO_FIX_TRAILER_INVALID");
        var checkText = Encoding.ASCII.GetString(pending.GetRange(checksumAt + 3, 3).ToArray());
        if (checkText.Any(c => c is < '0' or > '9') || !int.TryParse(checkText, out var check)
            || pending.Take(checksumAt).Sum(b => (int)b) % 256 != check)
            throw new InvalidDataException("DEMO_FIX_CHECKSUM_INVALID");
        frame = Encoding.ASCII.GetString(pending.GetRange(0, total).ToArray());
        pending.RemoveRange(0, total);
        return true;
    }
}

public interface ILmaxDemoFixTransport : IAsyncDisposable
{
    bool IsSimulated { get; }
    Task ConnectAsync(CancellationToken cancellationToken);
    Task<string> ReadAsync(CancellationToken cancellationToken);
    Task WriteAsync(string message, CancellationToken cancellationToken);
}

public sealed class LmaxDemoTlsTransport(LmaxConnectivityLabOptions options) : ILmaxDemoFixTransport
{
    private readonly TcpClient tcp = new();
    private readonly LmaxDemoFixFrames frames = new();
    private SslStream? stream;
    public bool IsSimulated => false;
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (options.EnvironmentName != "Demo" || options.AllowLiveTrading || !options.AllowExternalConnections
            || !options.AllowOrderSubmission || options.DryRun || !options.UseTls
            || options.FixOrderHost != "fix-order.london-demo.lmax.com" || options.FixOrderPort != 443)
            throw new InvalidOperationException("DEMO_CONTINUING_TRANSPORT_CONFIGURATION_INVALID");
        await tcp.ConnectAsync(options.FixOrderHost, options.FixOrderPort.Value, cancellationToken);
        stream = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false);
        await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = options.FixOrderHost,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CertificateRevocationCheckMode = options.CheckCertificateRevocation
                ? System.Security.Cryptography.X509Certificates.X509RevocationMode.Online
                : System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck
        }, cancellationToken);
    }
    public async Task<string> ReadAsync(CancellationToken cancellationToken)
    {
        var connected = stream ?? throw new InvalidOperationException("DEMO_FIX_NOT_CONNECTED");
        var buffer = new byte[4096];
        while (true)
        {
            if (frames.TryRead(out var frame)) return frame!;
            var count = await connected.ReadAsync(buffer, cancellationToken);
            if (count == 0) throw new EndOfStreamException("DEMO_FIX_CONNECTION_ENDED");
            frames.Append(buffer.AsSpan(0, count));
        }
    }
    public async Task WriteAsync(string message, CancellationToken cancellationToken)
    {
        var connected = stream ?? throw new InvalidOperationException("DEMO_FIX_NOT_CONNECTED");
        await connected.WriteAsync(Encoding.ASCII.GetBytes(message), cancellationToken);
        await connected.FlushAsync(cancellationToken);
    }
    public async ValueTask DisposeAsync()
    {
        if (stream is not null) await stream.DisposeAsync();
        tcp.Dispose();
    }
}
