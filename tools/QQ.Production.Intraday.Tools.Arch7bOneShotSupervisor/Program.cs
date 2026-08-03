using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = Parse(args);
            RequireQualificationOnly(options);
            var mode = Required(options, "mode");
            object result = mode switch
            {
                "qualify-static-authorities" => await QualifyStaticAsync(options).ConfigureAwait(false),
                "validate-one-shot-plan" => ValidatePlan(),
                "simulate-one-shot" => await SimulateAsync(options).ConfigureAwait(false),
                "materialize-supervisor-candidate-packet" => await MaterializeAsync(options).ConfigureAwait(false),
                _ => throw new Arch7bQualificationException(Arch7bBlockers.SupervisorModeUnknown, mode)
            };
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return 0;
        }
        catch (Arch7bQualificationException exception)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new { status = "NO_GO", blocker = exception.BlockerCode,
                detail = exception.Message }, JsonOptions));
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new { status = "NO_GO",
                blocker = "ARCH7B_ONE_SHOT_SUPERVISOR_UNEXPECTED_FAILURE", detail = exception.Message }, JsonOptions));
            return 1;
        }
    }

    public static IReadOnlyDictionary<string, string> Parse(IEnumerable<string> args)
    {
        var values = args.ToArray();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < values.Length; index += 2)
        {
            if (!values[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= values.Length)
                throw new ArgumentException("ARGUMENT_NAME_VALUE_PAIRS_REQUIRED");
            result.Add(values[index][2..], values[index + 1]);
        }
        return result;
    }

    private static async Task<object> QualifyStaticAsync(IReadOnlyDictionary<string, string> options)
    {
        var reader = new Arch7bGitCoreRepositoryReader(Required(options, "core-repository"),
            options.GetValueOrDefault("git-executable") ?? "git");
        var core = await Arch7bCoreStaticAuthorityQualifier.QualifyAsync(reader).ConfigureAwait(false);
        var registry = Arch7bGlobalSloRegistry.CreateDefault();
        var chronology = Arch7bCrossRepositoryChronology.Validate(Arch7bCrossRepositoryChronology.CreateDefault(), registry);
        if (!chronology.IsValid) throw new InvalidDataException(string.Join(',', chronology.Blockers));
        return new
        {
            verdict = Arch7bOneShotContracts.StaticQualificationVerdict,
            qualificationOnly = true,
            calendar = Arch7bOneShotContracts.OperationalSlotSelectionPolicyVersion,
            sloRegistry = Arch7bOneShotContracts.GlobalSloRegistryVersion,
            sloCount = registry.Entries.Count,
            globalSloCount = registry.Entries.Count(value => value.SloId.StartsWith("GLOBAL_", StringComparison.Ordinal)),
            chronology = Arch7bOneShotContracts.CrossRepositoryChronologyVersion,
            chronology.StageCount,
            chronology.EdgeCount,
            chronology.PreSlotCriticalPathSloSeconds,
            cleanup = Arch7bOneShotContracts.TerminalCleanupSupervisorVersion,
            coreBinding = core,
            safety = Arch7bNoLiveSafetyCounters.Zero,
            oneShotStateCount = 0
        };
    }

    private static object ValidatePlan()
    {
        var registry = Arch7bGlobalSloRegistry.CreateDefault();
        var chronology = Arch7bCrossRepositoryChronology.Validate(Arch7bCrossRepositoryChronology.CreateDefault(), registry);
        if (!chronology.IsValid) throw new InvalidDataException(string.Join(',', chronology.Blockers));
        var margin = Math.Max(Arch7bGlobalSloRegistry.GlobalMinimumPreparationMarginSeconds,
            chronology.PreSlotCriticalPathSloSeconds + Arch7bGlobalSloRegistry.GlobalPreparationSafetyReserveSeconds);
        return new
        {
            contractVersion = Arch7bOneShotContracts.LiveSupervisorVersion,
            qualificationOnly = true,
            syntheticPlan = true,
            currentSlot = (string?)null,
            oneShotIdentity = (string?)null,
            chronology.StageCount,
            chronology.EdgeCount,
            chronology.PreSlotCriticalPathSloSeconds,
            requiredPreparationMarginSeconds = margin,
            maximumSlots = Arch7bOneShotContracts.MaximumSlots,
            maximumCaptures = Arch7bOneShotContracts.MaximumCaptures,
            maximumRdsReads = Arch7bOneShotContracts.MaximumRdsReads,
            maximumRetries = Arch7bOneShotContracts.MaximumRetries,
            safety = Arch7bNoLiveSafetyCounters.Zero
        };
    }

    private static async Task<object> SimulateAsync(IReadOnlyDictionary<string, string> options)
    {
        var runs = ParsePositive(options.GetValueOrDefault("runs"), 50);
        var campaigns = ParsePositive(options.GetValueOrDefault("campaigns"), 10);
        var runsPerCampaign = ParsePositive(options.GetValueOrDefault("runs-per-campaign"), 3);
        var seedOffset = ParseNonNegative(options.GetValueOrDefault("seed-offset"), 0);
        var qualification = await Arch7bSimulationQualifier.RunAsync(runs, campaigns, runsPerCampaign, seedOffset)
            .ConfigureAwait(false);
        return new { verdict = "ARCH7B_ONE_SHOT_SYNTHETIC_QUALIFIED", qualificationOnly = true,
            qualification, safety = Arch7bNoLiveSafetyCounters.Zero };
    }

    private static async Task<object> MaterializeAsync(IReadOnlyDictionary<string, string> options)
    {
        var reader = new Arch7bGitCoreRepositoryReader(Required(options, "core-repository"),
            options.GetValueOrDefault("git-executable") ?? "git");
        var core = await Arch7bCoreStaticAuthorityQualifier.QualifyAsync(reader).ConfigureAwait(false);
        var simulations = await Arch7bSimulationQualifier.RunAsync().ConfigureAwait(false);
        var roundtrips = options.TryGetValue("primary-roundtrip-sha256", out var values)
            ? values.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [];
        var files = await Arch7bCandidatePacketWriter.WriteAsync(Required(options, "output-root"),
            Required(options, "intraday-commit"), Required(options, "intraday-tree"),
            Required(options, "executable"), Required(options, "intraday-repository"), core, simulations,
            roundtrips).ConfigureAwait(false);
        return new { verdict = Arch7bOneShotContracts.SuccessVerdict, qualificationOnly = true, files,
            safety = Arch7bNoLiveSafetyCounters.Zero };
    }

    private static void RequireQualificationOnly(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("qualification-only", out var value) ||
            !string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ARCH7B_QUALIFICATION_ONLY_REQUIRED");
    }

    private static string Required(IReadOnlyDictionary<string, string> options, string key) =>
        options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"MISSING_REQUIRED_ARGUMENT:{key}");

    private static int ParsePositive(string? value, int fallback) => value is null ? fallback :
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : throw new ArgumentException("POSITIVE_INTEGER_REQUIRED");

    private static int ParseNonNegative(string? value, int fallback) => value is null ? fallback :
        int.TryParse(value, out var parsed) && parsed >= 0 ? parsed : throw new ArgumentException("NON_NEGATIVE_INTEGER_REQUIRED");
}
