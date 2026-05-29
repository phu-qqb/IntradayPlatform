using QQ.Production.Intraday.Application;

var options = CliOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine("Usage: dotnet run --project tools/QQ.Production.Intraday.Tools.StratTakenPopulationAudit -- --run-key <RunKey> --package-root <path> --output-root <path> [--strattaken-path <path>] [--full-strattaken-reference <path>] [--no-execution true] [--suid-offset 0]");
    return 0;
}

if (!options.NoExecution)
{
    Console.Error.WriteLine("This audit is static/read-only. Pass --no-execution true.");
    return 2;
}

var runKey = string.IsNullOrWhiteSpace(options.RunKey)
    ? $"strattaken-population-audit-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}"
    : options.RunKey;
var outputRoot = string.IsNullOrWhiteSpace(options.OutputRoot)
    ? Path.Combine("artifacts", "qubes-intraday", runKey)
    : options.OutputRoot;
var validationDirectory = Path.Combine(outputRoot, "10_validation");
var repoRoot = FindRepoRoot();

var package = StratTakenPopulationAudit.Audit(new StratTakenPopulationAuditRequest(
    runKey,
    options.PackageRoot,
    options.StratTakenPath,
    options.FullStratTakenReferencePath,
    repoRoot,
    options.SuidOffsetBytes,
    options.NoExecution));

await StratTakenPopulationAudit.WritePackageAsync(validationDirectory, package, CancellationToken.None);
Console.WriteLine($"STRATTAKEN_BINARY_GATE={package.BinaryIntegrity.STRATTAKEN_BINARY_GATE}");
Console.WriteLine($"STRATTAKEN_POPULATION_GATE={package.Population.STRATTAKEN_POPULATION_GATE}");
Console.WriteLine($"STRATTAKEN_PACKAGE_COMPATIBILITY_GATE={package.Compatibility.STRATTAKEN_PACKAGE_COMPATIBILITY_GATE}");
Console.WriteLine($"STRATTAKEN_ATTRITION_GATE={package.Attrition.STRATTAKEN_ATTRITION_GATE}");
Console.WriteLine($"STRATTAKEN_UNDERPOPULATION_EXPLAINS_ZERO_WEIGHTS={package.Attrition.STRATTAKEN_UNDERPOPULATION_EXPLAINS_ZERO_WEIGHTS}");
Console.WriteLine($"Validation package: {Path.GetFullPath(validationDirectory)}");
return package.BinaryIntegrity.STRATTAKEN_BINARY_GATE == StratTakenGate.FAIL.ToString() ? 2 : 0;

static string FindRepoRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
    {
        directory = directory.Parent;
    }

    return directory?.FullName ?? Directory.GetCurrentDirectory();
}

internal sealed record CliOptions(
    string? RunKey,
    string? PackageRoot,
    string? OutputRoot,
    string? StratTakenPath,
    string? FullStratTakenReferencePath,
    int SuidOffsetBytes,
    bool NoExecution,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        string? runKey = null;
        string? packageRoot = null;
        string? outputRoot = null;
        string? stratTakenPath = null;
        string? fullReference = null;
        var suidOffset = 0;
        var noExecution = true;
        var showHelp = false;

        for (var index = 0; index < args.Length; index++)
        {
            var current = args[index];
            if (current is "--help" or "-h")
            {
                showHelp = true;
                continue;
            }

            var value = index + 1 < args.Length ? args[index + 1] : null;
            if (value is null || value.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            switch (current)
            {
                case "--run-key":
                    runKey = value;
                    index++;
                    break;
                case "--package-root":
                    packageRoot = value;
                    index++;
                    break;
                case "--output-root":
                    outputRoot = value;
                    index++;
                    break;
                case "--strattaken-path":
                    stratTakenPath = value;
                    index++;
                    break;
                case "--full-strattaken-reference":
                    fullReference = value;
                    index++;
                    break;
                case "--suid-offset":
                    suidOffset = int.TryParse(value, out var parsedOffset) ? parsedOffset : 0;
                    index++;
                    break;
                case "--no-execution":
                    noExecution = bool.TryParse(value, out var parsedNoExecution) && parsedNoExecution;
                    index++;
                    break;
            }
        }

        return new CliOptions(runKey, packageRoot, outputRoot, stratTakenPath, fullReference, suidOffset, noExecution, showHelp);
    }
}
