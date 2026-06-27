using System.Reflection;
using System.Text.Json;
using QQ.Production.Intraday.Application.CanonicalRecorder;
using QQ.Production.Intraday.Infrastructure.Lmax.MarketDataOnly;

namespace QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly;

public static class LmaxMarketDataCaptureOnlyPreflightCommand
{
    private static readonly string[] RequiredCredentialLabels=["LMAX_DEMO_SENDER_COMP_ID","LMAX_DEMO_TARGET_COMP_ID","LMAX_DEMO_FIX_USERNAME","LMAX_DEMO_FIX_PASSWORD"];

    public static async Task<int> RunAsync(string[] args,CancellationToken cancellationToken=default)
    {
        if(args.Length==0){Usage();return 2;}
        var command=args[0];var parsed=ParseArgs(args.Skip(1).ToArray());
        if(!parsed.TryGetValue("config",out var configPath)){Console.Error.WriteLine("missing --config");return 2;}
        var configJson=await File.ReadAllTextAsync(configPath,cancellationToken).ConfigureAwait(false);
        using var configDocument=JsonDocument.Parse(configJson);
        var config=JsonSerializer.Deserialize<LmaxMarketDataOnlyPreflightConfig>(configJson,CanonicalRecorderV2Constants.JsonOptions)??throw new InvalidOperationException("config_deserialize_failed");
        if(string.Equals(command,"preflight",StringComparison.OrdinalIgnoreCase))
        {
            if(!parsed.TryGetValue("output",out var outputRoot)){Console.Error.WriteLine("missing --output");return 2;}
            Directory.CreateDirectory(outputRoot);
            var report=LmaxMarketDataOnlyPreflight.Validate(config,parsed.ContainsKey("network-disabled"),parsed.ContainsKey("no-order-entry"),parsed.ContainsKey("no-account-api"),parsed.ContainsKey("no-db"),outputRootMustBeEmpty:false,configDocument:configDocument.RootElement);
            await WriteCommonReports(outputRoot,config,report,cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(outputRoot,"m2c1b_operator_command.txt"),OperatorCommand(configPath),cancellationToken).ConfigureAwait(false);
            Console.WriteLine(report.Status);return report.Issues.Count==0?0:1;
        }
        if(!string.Equals(command,"capture",StringComparison.OrdinalIgnoreCase)){Usage();return 2;}
        if(parsed.ContainsKey("operator-approved-market-data-fix-logon")==false||parsed.ContainsKey("no-order-entry")==false||parsed.ContainsKey("no-account-api")==false||parsed.ContainsKey("no-db")==false){Console.Error.WriteLine("capture_requires_operator_and_no_mutation_flags");return 2;}
        if(LmaxMarketDataOnlyConfigHash.Matches(config)==false){Console.Error.WriteLine("config_hash_mismatch");return 1;}
        var preflight=LmaxMarketDataOnlyPreflight.Validate(config,networkDisabled:true,noOrderEntry:true,noAccountApi:true,noDb:true,outputRootMustBeEmpty:false,configDocument:configDocument.RootElement);
        Directory.CreateDirectory(config.OutputRoot);
        await WriteCommonReports(config.OutputRoot,config,preflight,cancellationToken).ConfigureAwait(false);
        if(preflight.Issues.Count>0){Console.WriteLine("NO_GO_M2C1B");return 1;}
        var configDirectory=Path.GetDirectoryName(Path.GetFullPath(configPath))??throw new InvalidOperationException("config_directory_not_found");
        var catalog=LmaxMarketDataOnlyApprovedInstrumentCatalog.LoadFromConfigDirectory(configDirectory);
        var runner=new LmaxMarketDataOnlyCaptureRunner(catalog);
        if(parsed.TryGetValue("synthetic-replay",out var replayPath))
        {
            var rows=(await File.ReadAllLinesAsync(replayPath,cancellationToken)).Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray();
            var synthetic=await runner.CaptureSyntheticAsync(config,rows,cancellationToken).ConfigureAwait(false);
            await WriteJson(Path.Combine(config.OutputRoot,"m2c1b_capture_command_result.json"),synthetic,cancellationToken).ConfigureAwait(false);
            Console.WriteLine(synthetic.Status);return synthetic.Status=="GO_M2C2_CAPTURE_VALIDATED"?0:1;
        }
        var missing=RequiredCredentialLabels.Where(x=>string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(x))).ToArray();
        if(missing.Length>0)
        {
            var blocked=new{status="GO_OPERATOR_RUN_M2C1B",reason="credentials_not_available_to_codex_process",missing_labels=missing,operator_command=OperatorCommand(configPath)};
            await WriteJson(Path.Combine(config.OutputRoot,"m2c1b_operator_run_required.json"),blocked,cancellationToken).ConfigureAwait(false);
            Console.WriteLine("GO_OPERATOR_RUN_M2C1B");return 3;
        }
        var result=await runner.CaptureLiveAsync(config,cancellationToken).ConfigureAwait(false);
        await WriteJson(Path.Combine(config.OutputRoot,"m2c1b_capture_command_result.json"),result,cancellationToken).ConfigureAwait(false);
        Console.WriteLine(result.Status);return result.Status=="GO_M2C2_CAPTURE_VALIDATED"?0:1;
    }

    private static async Task WriteCommonReports(string outputRoot,LmaxMarketDataOnlyPreflightConfig config,LmaxMarketDataOnlyPreflightReport report,CancellationToken ct)
    {
        var whitelist=new{allowed_outbound_fix_msg_types=LmaxMarketDataOnlyOutboundFixMessageGuard.AllowedOutboundMsgTypes.OrderBy(x=>x,StringComparer.Ordinal).ToArray(),new_order_single=LmaxMarketDataOnlyOutboundFixMessageGuard.InspectMsgType("D"),market_data_request=LmaxMarketDataOnlyOutboundFixMessageGuard.InspectMsgType("V")};
        var dependency=new{status="STRUCTURAL_SCAN_PREFLIGHT",assembly=typeof(LmaxMarketDataOnlyPreflight).Assembly.GetName().Name,references=typeof(LmaxMarketDataOnlyPreflight).Assembly.GetReferencedAssemblies().Select(x=>x.Name).OrderBy(x=>x).ToArray(),forbidden_reference_terms_found=typeof(LmaxMarketDataOnlyPreflight).Assembly.GetReferencedAssemblies().Select(x=>x.Name??string.Empty).Where(x=>x.Contains("SqlServer",StringComparison.OrdinalIgnoreCase)||x.Contains("Simulator",StringComparison.OrdinalIgnoreCase)).ToArray()};
        var binary=new{tool=Assembly.GetExecutingAssembly().GetName().Name,assembly_version=Assembly.GetExecutingAssembly().GetName().Version?.ToString(),location=Assembly.GetExecutingAssembly().Location,sha256=File.Exists(Assembly.GetExecutingAssembly().Location)?CanonicalRecorderV2.Sha256File(Assembly.GetExecutingAssembly().Location):"MISSING"};
        await WriteJson(Path.Combine(outputRoot,"m2c1b_preflight_report.json"),report,ct).ConfigureAwait(false);
        await WriteJson(Path.Combine(outputRoot,"m2c1b_dependency_gate.json"),dependency,ct).ConfigureAwait(false);
        await WriteJson(Path.Combine(outputRoot,"m2c1b_fix_whitelist_report.json"),whitelist,ct).ConfigureAwait(false);
        await WriteJson(Path.Combine(outputRoot,"m2c1b_binary_fingerprint.json"),binary,ct).ConfigureAwait(false);
    }

    private static string OperatorCommand(string configPath)=>$"dotnet run --project tools/QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly -- capture --config \"{configPath}\" --operator-approved-market-data-fix-logon --no-order-entry --no-account-api --no-db";
    private static Dictionary<string,string> ParseArgs(string[] args){var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);for(var i=0;i<args.Length;i++){if(!args[i].StartsWith("--",StringComparison.Ordinal))continue;var key=args[i][2..];result[key]=(i+1<args.Length&&args[i+1].StartsWith("--",StringComparison.Ordinal)==false)?args[++i]:"true";}return result;}
    private static async Task WriteJson<T>(string path,T value,CancellationToken ct){Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllTextAsync(path,JsonSerializer.Serialize(value,CanonicalRecorderV2Constants.JsonOptions),ct).ConfigureAwait(false);}
    private static void Usage()=>Console.Error.WriteLine("usage: lmax-market-data-capture preflight|capture --config <path> [--output <dir>] --no-order-entry --no-account-api --no-db");
}

internal static class Program{public static Task<int> Main(string[] args)=>LmaxMarketDataCaptureOnlyPreflightCommand.RunAsync(args);}
