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
            var mode = Required(options, "mode");
            RequireModeSafety(options, mode);
            if (mode == "fake-child")
                return await Arch7bQualificationFakeChild.RunAsync(options).ConfigureAwait(false);
            object result = mode switch
            {
                "qualify-static-authorities" => await QualifyStaticAsync(options).ConfigureAwait(false),
                "validate-one-shot-plan" => ValidatePlan(),
                "simulate-one-shot" => await SimulateAsync(options).ConfigureAwait(false),
                "materialize-supervisor-candidate-packet" => await MaterializeAsync(options).ConfigureAwait(false),
                "validate-live-execution-contract" => ValidateLiveExecutionContract(options),
                "simulate-live-command-execution" => await SimulateLiveCommandExecutionAsync(options).ConfigureAwait(false),
                "materialize-live-execution-candidate-packet" => await MaterializeLiveCandidateAsync(options).ConfigureAwait(false),
                "run-one-shot" => await RunOneShotAsync(options).ConfigureAwait(false),
                _ => throw new Arch7bQualificationException(Arch7bBlockers.SupervisorModeUnknown, mode)
            };
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return 0;
        }
        catch (Arch7bQualificationException exception)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                status = "NO_GO",
                blocker = exception.BlockerCode,
                detail = exception.Message
            }, JsonOptions));
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new
            {
                status = "NO_GO",
                blocker = "ARCH7B_ONE_SHOT_SUPERVISOR_UNEXPECTED_FAILURE",
                detail = exception.Message
            }, JsonOptions));
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
            if (!result.TryAdd(values[index][2..], values[index + 1]))
                throw new Arch7bQualificationException(Arch7bBlockers.DuplicateArgument, values[index]);
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
            sloRegistryEvidenceSha256 = registry.EvidenceSha256,
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
            sloRegistryEvidenceSha256 = registry.EvidenceSha256,
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
        return new
        {
            verdict = "ARCH7B_ONE_SHOT_SYNTHETIC_QUALIFIED",
            qualificationOnly = true,
            qualification,
            safety = Arch7bNoLiveSafetyCounters.Zero
        };
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
        return new
        {
            verdict = Arch7bOneShotContracts.SuccessVerdict,
            qualificationOnly = true,
            files,
            safety = Arch7bNoLiveSafetyCounters.Zero
        };
    }

    private static object ValidateLiveExecutionContract(IReadOnlyDictionary<string, string> options)
    {
        var executable = Path.GetFullPath(Required(options, "executable"));
        var root = Path.Combine(Path.GetTempPath(), "qq-arch7b-contract-validation", Guid.NewGuid().ToString("N"));
        var fixture = Arch7bSyntheticLiveExecutionFactory.Create(executable, root, "contract-validation");
        Arch7bOneShotAuthorityLoader.ValidatePlan(fixture.Plan);
        fixture.Authority.Validate(fixture.Plan, fixture.Authority.EvidenceSha256,
            fixture.Plan.OperatorAuthorizationId, DateTimeOffset.UtcNow);
        return new
        {
            verdict = "ARCH7B_ONE_SHOT_LIVE_EXECUTION_CONTRACT_QUALIFIED",
            qualificationOnly = true,
            runtime = Arch7bOneShotContracts.LiveExecutionRuntimeVersion,
            runner = Arch7bOneShotContracts.CommandRunnerVersion,
            authority = Arch7bOneShotContracts.LiveExecutionAuthorityVersion,
            plan = Arch7bOneShotContracts.LivePlanVersion,
            commandCount = fixture.Plan.Commands.Count,
            runRootCreated = Directory.Exists(root),
            safety = Arch7bNoLiveSafetyCounters.Zero
        };
    }

    private static async Task<object> SimulateLiveCommandExecutionAsync(
        IReadOnlyDictionary<string, string> options)
    {
        var qualification = await Arch7bLiveProcessQualifier.RunAsync(
            Path.GetFullPath(Required(options, "executable")),
            ParsePositive(options.GetValueOrDefault("runs"), 1),
            ParseNonNegative(options.GetValueOrDefault("campaigns"), 0),
            ParsePositive(options.GetValueOrDefault("runs-per-campaign"), 3),
            ParseBoolean(options.GetValueOrDefault("failure-matrix"), false)).ConfigureAwait(false);
        return new
        {
            verdict = "ARCH7B_ONE_SHOT_LIVE_PROCESS_QUALIFIED",
            qualificationOnly = true,
            qualification,
            safety = Arch7bNoLiveSafetyCounters.Zero,
            operationalOneShotStateCount = 0
        };
    }

    private static async Task<object> MaterializeLiveCandidateAsync(
        IReadOnlyDictionary<string, string> options)
    {
        var qualification = new Arch7bLiveProcessQualification(
            ParsePositive(Required(options, "independent-runs"), 30),
            ParsePositive(Required(options, "independent-passes"), 30),
            ParsePositive(Required(options, "campaigns"), 10),
            ParsePositive(Required(options, "campaign-passes"), 10),
            ParsePositive(Required(options, "runs-per-campaign"), 3),
            ParsePositive(Required(options, "failure-count"), Arch7bStages.All.Count),
            ParsePositive(Required(options, "failure-passes"), Arch7bStages.All.Count),
            0, 0, RequiredSha(options, "qualification-evidence-sha256"));
        var primaryValidation = SplitShas(Required(options, "primary-validation-sha256"));
        var primarySimulation = SplitShas(Required(options, "primary-simulation-sha256"));
        var files = await Arch7bLiveCandidatePacketWriter.WriteAsync(Required(options, "output-root"),
            Required(options, "repository-root"), Required(options, "base-master-commit"),
            Required(options, "base-master-tree"), Required(options, "candidate-commit"),
            Required(options, "candidate-tree"), Required(options, "executable"), qualification,
            primaryValidation, primarySimulation).ConfigureAwait(false);
        return new
        {
            verdict = Arch7bOneShotContracts.LiveRuntimeCandidateVerdict,
            qualificationOnly = true,
            files,
            safety = Arch7bNoLiveSafetyCounters.Zero
        };
    }

    private static async Task<object> RunOneShotAsync(IReadOnlyDictionary<string, string> options)
    {
        if (!ParseBoolean(Required(options, "no-order"), false))
            throw new Arch7bQualificationException(Arch7bBlockers.NoOrderRequired);
        foreach (var key in new[] { "freeze-root", "live-execution-authority-path", "run-root",
                     "core-repository", "intraday-runtime", "git-executable", "root-certificate" })
            Arch7bOneShotAuthorityLoader.RequireAbsolute(Required(options, key));
        foreach (var key in new[] { "freeze-root", "core-repository", "intraday-runtime" })
            if (!Directory.Exists(Required(options, key)))
                throw new Arch7bQualificationException(Arch7bBlockers.LiveCommandAuthorityIncomplete, key);
        foreach (var key in new[] { "live-execution-authority-path", "git-executable", "root-certificate" })
            if (!File.Exists(Required(options, key)))
                throw new Arch7bQualificationException(Arch7bBlockers.LiveCommandAuthorityIncomplete, key);
        var expectedAuthorityFileSha = RequiredSha(options, "expected-live-execution-authority-sha256");
        var loaded = await Arch7bOneShotAuthorityLoader.LoadAuthorityAsync(
            Required(options, "live-execution-authority-path"), expectedAuthorityFileSha).ConfigureAwait(false);
        var plan = await Arch7bOneShotAuthorityLoader.LoadPlanAsync(Required(options, "freeze-root"),
            RequiredSha(options, "expected-freeze-manifest-sha256")).ConfigureAwait(false);
        if (Path.GetFullPath(plan.RunRoot) != Path.GetFullPath(Required(options, "run-root")))
            throw new Arch7bQualificationException(Arch7bBlockers.RunRootReused);
        var runtime = new Arch7bOneShotLiveExecutionRuntime(new Arch7bOneShotProcessCommandRunner());
        return await runtime.RunOneShotAsync(plan, loaded.Authority, loaded.Authority.EvidenceSha256,
            Required(options, "operator-authorization-id"), DateTimeOffset.UtcNow).ConfigureAwait(false);
    }

    private static void RequireModeSafety(IReadOnlyDictionary<string, string> options, string mode)
    {
        var qualificationOnly = ParseBoolean(options.GetValueOrDefault("qualification-only"), true);
        if (mode == "run-one-shot")
        {
            if (qualificationOnly) throw new Arch7bQualificationException(Arch7bBlockers.QualificationModeMismatch);
        }
        else if (!qualificationOnly)
        {
            throw new Arch7bQualificationException(Arch7bBlockers.QualificationModeMismatch);
        }
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

    private static bool ParseBoolean(string? value, bool fallback) => value is null ? fallback :
        bool.TryParse(value, out var parsed) ? parsed : throw new ArgumentException("BOOLEAN_REQUIRED");

    private static string RequiredSha(IReadOnlyDictionary<string, string> options, string key)
    {
        var value = Required(options, key);
        if (!Arch7bOneShotContracts.IsSha256(value))
            throw new Arch7bQualificationException(Arch7bBlockers.CommandAuthorityMismatch, key);
        return value;
    }

    private static IReadOnlyList<string> SplitShas(string values)
    {
        var result = values.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (result.Length != 3 || result.Any(value => !Arch7bOneShotContracts.IsSha256(value)))
            throw new Arch7bQualificationException(Arch7bBlockers.CommandAuthorityMismatch);
        return result;
    }
}
