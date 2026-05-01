namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

using System.Globalization;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

public sealed class RawLmaxFixSessionClient(LmaxConnectivityLabSafetyValidator safety) : ILmaxFixSessionClient
{
    private const char Soh = '\x01';

    public LabCommandResult Validate(LmaxConnectivityLabOptions options, bool marketData)
    {
        var missing = MissingStructuralFixFields(options, marketData).ToList();
        var command = marketData ? "fix-market-data-smoke" : "fix-session-dry-run";
        if (missing.Count > 0)
        {
            return LabCommandResult.Skipped(command, $"Missing FIX config: {string.Join(", ", missing)}.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options));
        }

        return LabCommandResult.Ok(command, "FIX configuration is structurally complete. No socket connection was opened.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options));
    }

    public Task<LabCommandResult> SmokeAsync(LmaxConnectivityLabOptions options, bool marketData, CancellationToken cancellationToken)
        => Task.FromResult(Validate(options, marketData));

    public async Task<LabCommandResult> LogonSmokeAsync(LmaxConnectivityLabOptions options, bool marketData, CancellationToken cancellationToken)
    {
        var command = marketData ? "fix-marketdata-logon-smoke" : "fix-order-logon-smoke";
        var sessionType = marketData ? "MarketData" : "Order";
        var startedAt = DateTimeOffset.UtcNow;
        var issues = safety.ValidateForFixLogon(options, marketData).ToList();
        if (issues.Count > 0)
        {
            return new LabCommandResult(command, "Skipped", string.Join(" ", issues), LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), sessionType, false, false, startedAt, DateTimeOffset.UtcNow);
        }

        var host = marketData ? options.FixMarketDataHost! : options.FixOrderHost!;
        var port = marketData ? options.FixMarketDataPort!.Value : options.FixOrderPort!.Value;
        var target = marketData ? (options.FixMarketDataTargetCompId ?? options.FixTargetCompId)! : (options.FixOrderTargetCompId ?? options.FixTargetCompId)!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.RequestTimeoutSeconds)));

        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, timeout.Token);
            await using var stream = options.UseTls ? await CreateTlsStreamAsync(tcp, host, timeout.Token) : tcp.GetStream();

            var logon = BuildMessage("A", 1, options.FixSenderCompId!, target, [
                ("98", "0"),
                ("108", "30"),
                ("141", "Y"),
                ("553", options.FixUsername!),
                ("554", options.FixPassword!)
            ]);
            await WriteAsciiAsync(stream, logon, timeout.Token);

            var response = await ReadFixResponseAsync(stream, timeout.Token);
            var loggedOn = ContainsTag(response, "35", "A");
            var receivedLogout = ContainsTag(response, "35", "5");
            var status = loggedOn ? "Ok" : "Failed";
            var message = loggedOn
                ? "FIX logon succeeded and logout was sent. No orders or subscriptions were submitted."
                : receivedLogout
                    ? "FIX session returned Logout before logon was confirmed."
                    : "FIX logon was not confirmed before timeout or session response.";

            if (loggedOn)
            {
                var logout = BuildMessage("5", 2, options.FixSenderCompId!, target, [("58", "Connectivity lab logoff")]);
                await WriteAsciiAsync(stream, logout, CancellationToken.None);
            }

            return new LabCommandResult(command, status, message, LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), sessionType, true, loggedOn, startedAt, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
        {
            return new LabCommandResult(command, "Failed", "FIX logon smoke timed out.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), sessionType, false, false, startedAt, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException)
        {
            return new LabCommandResult(command, "Failed", $"FIX logon smoke failed: {ex.GetType().Name}: {ex.Message}", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options), sessionType, false, false, startedAt, DateTimeOffset.UtcNow);
        }
    }

    public LabCommandResult SnapshotSmoke(LmaxConnectivityLabOptions options)
    {
        var issues = safety.ValidateForFixLogon(options, marketData: true).ToList();
        if (issues.Count > 0)
        {
            return LabCommandResult.Skipped("fix-marketdata-snapshot-smoke", string.Join(" ", issues), LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options));
        }

        return LabCommandResult.Skipped("fix-marketdata-snapshot-smoke", "Read-only market data snapshot request is not implemented yet. Use fix-marketdata-logon-smoke first.", LmaxConnectivityLabSafetyValidator.DecisionsForExternalCommand(options));
    }

    private static async Task<Stream> CreateTlsStreamAsync(TcpClient tcp, string host, CancellationToken cancellationToken)
    {
        var ssl = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false);
        await ssl.AuthenticateAsClientAsync(host, null, System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13, checkCertificateRevocation: true).WaitAsync(cancellationToken);
        return ssl;
    }

    private static async Task WriteAsciiAsync(Stream stream, string message, CancellationToken cancellationToken)
    {
        var bytes = Encoding.ASCII.GetBytes(message);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<string> ReadFixResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var builder = new StringBuilder();
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            builder.Append(Encoding.ASCII.GetString(buffer, 0, read));
            var text = builder.ToString();
            if (ContainsTag(text, "35", "A") || ContainsTag(text, "35", "5"))
            {
                return text;
            }
        }

        return builder.ToString();
    }

    private static string BuildMessage(string messageType, int sequenceNumber, string senderCompId, string targetCompId, IReadOnlyList<(string Tag, string Value)> fields)
    {
        var body = new StringBuilder();
        body.Append("35=").Append(messageType).Append(Soh);
        body.Append("34=").Append(sequenceNumber.ToString(CultureInfo.InvariantCulture)).Append(Soh);
        body.Append("49=").Append(senderCompId).Append(Soh);
        body.Append("52=").Append(DateTimeOffset.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append(Soh);
        body.Append("56=").Append(targetCompId).Append(Soh);
        foreach (var (tag, value) in fields)
        {
            body.Append(tag).Append('=').Append(value).Append(Soh);
        }

        var head = $"8=FIX.4.4{Soh}9={Encoding.ASCII.GetByteCount(body.ToString())}{Soh}";
        var withoutChecksum = head + body;
        var checksum = Encoding.ASCII.GetBytes(withoutChecksum).Sum(x => x) % 256;
        return withoutChecksum + $"10={checksum.ToString("000", CultureInfo.InvariantCulture)}{Soh}";
    }

    private static bool ContainsTag(string message, string tag, string value)
        => message.Contains($"{Soh}{tag}={value}{Soh}", StringComparison.Ordinal) || message.StartsWith($"{tag}={value}{Soh}", StringComparison.Ordinal);

    private static IEnumerable<string> MissingStructuralFixFields(LmaxConnectivityLabOptions options, bool marketData)
    {
        if (marketData)
        {
            if (string.IsNullOrWhiteSpace(options.FixMarketDataHost)) yield return nameof(options.FixMarketDataHost);
            if (options.FixMarketDataPort is null) yield return nameof(options.FixMarketDataPort);
            if (string.IsNullOrWhiteSpace(options.FixMarketDataTargetCompId ?? options.FixTargetCompId)) yield return nameof(options.FixMarketDataTargetCompId);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(options.FixOrderHost)) yield return nameof(options.FixOrderHost);
            if (options.FixOrderPort is null) yield return nameof(options.FixOrderPort);
            if (string.IsNullOrWhiteSpace(options.FixOrderTargetCompId ?? options.FixTargetCompId)) yield return nameof(options.FixOrderTargetCompId);
        }

        if (string.IsNullOrWhiteSpace(options.FixSenderCompId)) yield return nameof(options.FixSenderCompId);
        if (string.IsNullOrWhiteSpace(options.FixUsername)) yield return nameof(options.FixUsername);
    }
}
