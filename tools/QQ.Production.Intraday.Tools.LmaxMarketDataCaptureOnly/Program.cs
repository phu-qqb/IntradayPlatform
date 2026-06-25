using System.Reflection;
using System.Text.Json;
using QQ.Production.Intraday.Application.CanonicalRecorder;
using QQ.Production.Intraday.Infrastructure.Lmax.MarketDataOnly;

namespace QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly;

public static class LmaxMarketDataCaptureOnlyPreflightCommand
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0 || !string.Equals(args[0], "preflight", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("usage: lmax-market-data-capture preflight --config <path> --output <dir> --network-disabled --no-order-entry --no-account-api --no-db");
            return 2;
        }

        var parsed = ParseArgs(args.Skip(1).ToArray());
        if (!parsed.TryGetValue("config", out var configPath) || !parsed.TryGetValue("output", out var outputRoot))
        {
            Console.Error.WriteLine("missing --config or --output");
            return 2;
        }

        var config = JsonSerializer.Deserialize<LmaxMarketDataOnlyPreflightConfig>(await File.ReadAllTextAsync(configPath, cancellationToken), CanonicalRecorderV2Constants.JsonOptions)
            ?? throw new InvalidOperationException("config_deserialize_failed");
        Directory.CreateDirectory(outputRoot);
        var report = LmaxMarketDataOnlyPreflight.Validate(
            config,
            networkDisabled: parsed.ContainsKey("network-disabled"),
            noOrderEntry: parsed.ContainsKey("no-order-entry"),
            noAccountApi: parsed.ContainsKey("no-account-api"),
            noDb: parsed.ContainsKey("no-db"));
        var whitelist = new
        {
            allowed_outbound_fix_msg_types = LmaxMarketDataOnlyOutboundFixMessageGuard.AllowedOutboundMsgTypes.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            forbidden_examples = LmaxMarketDataOnlyOutboundFixMessageGuard.ForbiddenOutboundMsgTypes.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            new_order_single = LmaxMarketDataOnlyOutboundFixMessageGuard.InspectMsgType("D"),
            market_data_request = LmaxMarketDataOnlyOutboundFixMessageGuard.InspectMsgType("V"),
            unknown = LmaxMarketDataOnlyOutboundFixMessageGuard.InspectMsgType("ZZ")
        };
        var dependency = new
        {
            status = "STRUCTURAL_SCAN_PRELIGHT",
            assembly = typeof(LmaxMarketDataOnlyPreflight).Assembly.GetName().Name,
            references = typeof(LmaxMarketDataOnlyPreflight).Assembly.GetReferencedAssemblies().Select(x => x.Name).OrderBy(x => x).ToArray(),
            forbidden_reference_terms_found = typeof(LmaxMarketDataOnlyPreflight).Assembly.GetReferencedAssemblies()
                .Select(x => x.Name ?? string.Empty)
                .Where(x => x.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) || x.Contains("Simulator", StringComparison.OrdinalIgnoreCase))
                .ToArray()
        };
        var binary = new
        {
            tool = Assembly.GetExecutingAssembly().GetName().Name,
            assembly_version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
            location = Assembly.GetExecutingAssembly().Location,
            sha256 = File.Exists(Assembly.GetExecutingAssembly().Location) ? CanonicalRecorderV2.Sha256File(Assembly.GetExecutingAssembly().Location) : "MISSING"
        };

        await WriteJson(Path.Combine(outputRoot, "m2c1a_preflight_report.json"), report, cancellationToken);
        await WriteJson(Path.Combine(outputRoot, "m2c1a_dependency_gate.json"), dependency, cancellationToken);
        await WriteJson(Path.Combine(outputRoot, "m2c1a_fix_whitelist_report.json"), whitelist, cancellationToken);
        await WriteJson(Path.Combine(outputRoot, "m2c1a_binary_fingerprint.json"), binary, cancellationToken);
        var operatorCommand = "# DO NOT RUN IN M2C1A. Future M2C1B operator-only market-data FIX logon, no order-entry:\n" +
                              "# dotnet run --project tools/QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly -- capture --config <reviewed-config.json> --operator-approved-market-data-fix-logon\n";
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "m2c1b_operator_command.txt"), operatorCommand, cancellationToken);
        Console.WriteLine(report.Status);
        return report.Issues.Count == 0 ? 0 : 1;
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                result[key] = args[++i];
            }
            else
            {
                result[key] = "true";
            }
        }

        return result;
    }

    private static async Task WriteJson<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, CanonicalRecorderV2Constants.JsonOptions), cancellationToken);
    }
}

internal static class Program
{
    public static Task<int> Main(string[] args)
        => LmaxMarketDataCaptureOnlyPreflightCommand.RunAsync(args);
}
