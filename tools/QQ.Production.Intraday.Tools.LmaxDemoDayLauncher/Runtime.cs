using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.LmaxDemoDayLauncher;

internal static class Runtime
{
    internal const string Root = @"D:\data\lmax-demo-orchestration";
    internal const string Dotnet = @"C:\deploy\IntradayPlatform\toolchains\dotnet-sdk-10\dotnet.exe";
    internal const string Collector = @"C:\deploy\IntradayPlatform\staging\intraday-f5b5a01-capture\publish\lmax-market-data-capture\QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly.dll";
    internal const string Template = @"C:\deploy\IntradayPlatform\operator\m2c1b-241eef6f-20260910\config\m2c1b-capture-config.json";
    internal const string Catalog = @"C:\deploy\IntradayPlatform\operator\m2c1b-241eef6f-20260910\config\lmax_demo_market_data_instrument_catalog.json";
    internal const string Writer = @"C:\deploy\IntradayPlatform\releases\qq84-execdesk-static-v1-20260914T100000Z\ExecDeskCompat.exe";
    internal const string Mapping = @"C:\home\data\static_mapping.txt";
    internal const string Bucket = "qq-fund-platform-anubis-benchmark-stage-761018894194-eu-west-2";
    internal const string Gpu = "i-019ec3c94b9d234f6";
    internal const string Document = "QQ-LMAX-Demo-Anubis-Cycle-v1";
    internal const string DocumentHash = "59ec2b5b3d87cc257e1c52b0e38947391dbbb67952cb050403e38fa11e467883";
    internal const string Role = "arn:aws:sts::761018894194:assumed-role/qq-role-ec2-intraday/i-05626133ca7892fb8";
    internal static readonly Dictionary<string, string> Pins = new()
    {
        [Collector] = "0d0234dbb9166587238d778d54665a51588c675c762e054646d26724bcfd8481",
        [Template] = "01fe316c0a185e6df3ac6016627a280ae70b925e3698d7d8bf0ab61528fb2b64",
        [Catalog] = "c98176f8aeeac8756049d51468e7367658b94ccccb81e08e314b9e0387e1840d",
        [Writer] = "c9dc31b7a79d9d14822e6a32e62e3dbefca0897f16cf4b635af29297606b5a87",
        [Mapping] = "0b251b48e47875ad33431fc8b9b4609c6acf7f82a25bb99decfcf41b0720ebe6"
    };
    internal static void VerifyLocal()
    {
        Files.Require(OperatingSystem.IsWindows() && Environment.MachineName.Equals("EC2AMAZ-1QPHTD8", StringComparison.OrdinalIgnoreCase)
            && Environment.UserName.Equals("Administrator", StringComparison.OrdinalIgnoreCase), "DEMO_OWNER_CONTEXT_REQUIRED");
        foreach (var pin in Pins) Files.Require(File.Exists(pin.Key) && Files.Hash(pin.Key) == pin.Value, "RUNTIME_PIN_MISMATCH");
    }
    internal static ProcessStartInfo StartInfo(string executable, IEnumerable<string> args, IReadOnlyDictionary<string, string>? environment = null)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        // A credential-bound child must not inherit a higher-priority stale Lab override.
        if (environment is not null)
            foreach (var key in info.Environment.Keys.Where(k => k.StartsWith("QQ_LMAX_", StringComparison.OrdinalIgnoreCase)
                || k.StartsWith("LmaxConnectivityLab__", StringComparison.OrdinalIgnoreCase)).ToArray()) info.Environment.Remove(key);
        foreach (var name in new[] { "AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY", "AWS_SESSION_TOKEN", "AWS_PROFILE", "AWS_DEFAULT_PROFILE",
            "AWS_SHARED_CREDENTIALS_FILE", "AWS_CONFIG_FILE", "AWS_EC2_METADATA_DISABLED", "AWS_WEB_IDENTITY_TOKEN_FILE", "AWS_ROLE_ARN",
            "AWS_CONTAINER_CREDENTIALS_RELATIVE_URI", "AWS_CONTAINER_CREDENTIALS_FULL_URI" }) info.Environment.Remove(name);
        info.Environment["AWS_SHARED_CREDENTIALS_FILE"] = @"C:\deploy\IntradayPlatform\staging\role-only-no-credentials";
        info.Environment["AWS_CONFIG_FILE"] = @"C:\deploy\IntradayPlatform\staging\role-only-no-config";
        info.Environment["AWS_EC2_METADATA_DISABLED"] = "false";
        info.Environment["AWS_PAGER"] = "";
        if (Path.GetFileName(executable).Equals("aws.exe", StringComparison.OrdinalIgnoreCase))
        {
            info.StandardOutputEncoding = new UTF8Encoding(false, true);
            info.StandardErrorEncoding = new UTF8Encoding(false, true);
            info.Environment["PYTHONIOENCODING"] = "utf-8";
        }
        if (environment is not null) foreach (var pair in environment) info.Environment[pair.Key] = pair.Value;
        return info;
    }
    internal static async Task<string> Run(string executable, IEnumerable<string> args, DateTimeOffset deadline,
        string stage, string? privateLog = null, IReadOnlyDictionary<string, string>? environment = null)
    {
        Files.Require(deadline > DateTimeOffset.UtcNow, "DEADLINE_EXPIRED_" + stage);
        using var process = Process.Start(StartInfo(executable, args, environment)) ?? throw new InvalidOperationException("PROCESS_START_FAILED_" + stage);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(deadline - DateTimeOffset.UtcNow > TimeSpan.Zero ? deadline - DateTimeOffset.UtcNow : TimeSpan.FromMilliseconds(1));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch (InvalidOperationException) { }
            throw new InvalidOperationException("PROCESS_DEADLINE_EXPIRED_" + stage);
        }
        var output = await stdout;
        var error = await stderr;
        if (privateLog is not null)
        {
            await File.WriteAllTextAsync(privateLog + ".stdout.txt", output);
            await File.WriteAllTextAsync(privateLog + ".stderr.txt", error);
        }
        // Never include command arguments or child output in exceptions: AWS SecretString is in memory only.
        Files.Require(process.ExitCode == 0, "PROCESS_FAILED_" + stage + "_" + process.ExitCode);
        return output;
    }
    internal static Task<string> Aws(DateTimeOffset deadline, string stage, params string[] args)
        => Run("aws.exe", args.Concat(["--region", "eu-west-2", "--output", "json"]), deadline, stage);
    internal static async Task VerifyRole()
    {
        using var identity = JsonDocument.Parse(await Aws(DateTimeOffset.UtcNow.AddSeconds(30), "IDENTITY", "sts", "get-caller-identity"));
        Files.Require(identity.RootElement.GetProperty("Arn").GetString() == Role, "DEMO_INSTANCE_ROLE_REQUIRED");
    }
    internal static async Task VerifyTransport()
    {
        await VerifyRole();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        using var document = JsonDocument.Parse(await Aws(deadline, "SSM_DOCUMENT", "ssm", "describe-document", "--name", Document, "--document-version", "1"));
        var description = document.RootElement.GetProperty("Document");
        Files.Require(description.GetProperty("Status").GetString() == "Active" && description.GetProperty("Hash").GetString() == DocumentHash,
            "SSM_DOCUMENT_PIN_MISMATCH");
        using var instance = JsonDocument.Parse(await Aws(deadline, "GPU_STATE", "ec2", "describe-instances", "--instance-ids", Gpu));
        var instances = instance.RootElement.GetProperty("Reservations").EnumerateArray().SelectMany(x => x.GetProperty("Instances").EnumerateArray()).ToArray();
        Files.Require(instances.Length == 1 && instances[0].GetProperty("InstanceId").GetString() == Gpu
            && instances[0].GetProperty("State").GetProperty("Name").GetString() == "running", "EXACT_ANUBIS_INSTANCE_MUST_BE_RUNNING");
        await Aws(deadline, "SSM_STATUS_READ", "ssm", "list-command-invocations", "--instance-id", Gpu, "--max-results", "1");
    }
    internal static FileStream AcquireLauncherLease(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33)
        { throw new InvalidOperationException("DEMO_DAY_LAUNCHER_ALREADY_RUNNING"); }
    }
    private static JsonDocument ParseCaptureSecret(string json)
    {
        try { return JsonDocument.Parse(json); }
        catch (JsonException) { throw new InvalidOperationException("MD_SECRET_JSON_INVALID"); }
    }
    internal static async Task<Dictionary<string, string>> CaptureCredentials()
    {
        using var response = JsonDocument.Parse(await Aws(DateTimeOffset.UtcNow.AddSeconds(45), "MD_SECRET", "secretsmanager", "get-secret-value", "--secret-id", "qq/fund-platform/demo/lmax/market-data"));
        return BindCaptureCredentials(response.RootElement.GetProperty("SecretString").GetString()!);
    }
    internal static Dictionary<string, string> BindCaptureCredentials(string secretString)
    {
        var json = secretString.Trim();
        // SecretString is already decoded text. Remove only its optional native BOM.
        if (json.StartsWith('\uFEFF')) json = json[1..];
        using var secret = ParseCaptureSecret(json);
        Files.Require(secret.RootElement.ValueKind == JsonValueKind.Object, "MD_SECRET_JSON_OBJECT_REQUIRED");
        string Get(params string[] candidates)
        {
            foreach (var property in secret.RootElement.EnumerateObject())
            {
                var name = new string(property.Name.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
                if (candidates.Contains(name))
                {
                    var value = property.Value.GetString();
                    Files.Require(!string.IsNullOrWhiteSpace(value), "MD_SECRET_FIELD_EMPTY");
                    return value!;
                }
            }
            throw new InvalidOperationException("MD_SECRET_FIELD_MISSING");
        }
        return new()
        {
            ["LMAX_DEMO_SENDER_COMP_ID"] = Get("LMAXDEMOSENDERCOMPID", "SENDERCOMPID", "SENDERCOMP", "FIXSENDERCOMPID"),
            ["LMAX_DEMO_TARGET_COMP_ID"] = Get("LMAXDEMOTARGETCOMPID", "TARGETCOMPID", "TARGETCOMP", "FIXTARGETCOMPID"),
            ["LMAX_DEMO_FIX_USERNAME"] = Get("LMAXDEMOFIXUSERNAME", "FIXUSERNAME", "USERNAME"),
            ["LMAX_DEMO_FIX_PASSWORD"] = Get("LMAXDEMOFIXPASSWORD", "FIXPASSWORD", "PASSWORD")
        };
    }
}
