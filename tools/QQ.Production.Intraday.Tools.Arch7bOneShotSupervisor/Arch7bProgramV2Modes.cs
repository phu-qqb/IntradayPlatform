using System.Security.Cryptography;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

internal sealed record Arch7bOneShotStaticPreflightEvidence(
    string Verdict,
    bool QualificationOnly,
    string Stage,
    Arch7bTargetCommandEnvironmentValidation TargetCommandEnvironment,
    Arch7bOperationalExecutionAuthorityValidation Validation,
    Arch7bChildEntrypointValidation ChildEntrypoints,
    Arch7bLiveCliAuthorityBindingValidation CliAuthorityBindings,
    bool SlotSelected,
    bool SlotLocked,
    bool OneShotIdentityCreated,
    bool LiveExecutionAuthorityLoaded,
    bool OperatorAuthorizationLoaded,
    int ResidualProcessCount,
    int ResidualMarkerCount,
    Arch7bNoLiveSafetyCounters Safety,
    Arch7bTargetBoundBrowserRuntimeAuthorityValidation? BrowserRuntimeAuthority = null);

public static class Arch7bProgramV2Modes
{
    private const string Arch7bRdsTestProfile = "ARCH7B_RDS_TEST";
    private const string Arch7bRdsTestFingerprint =
        "72fa569ee28e4dec6272db0d69c7594b2be8853e9607dff3e78066378a0b5ee4";
    private const string Arch7bPositionImporterSecretArn =
        "arn:aws:secretsmanager:eu-west-2:761018894194:secret:" +
        "qq-intraday-test-credentials-5YHOCV";
    private const string Arch7bDemoAccountId = "1754288005";

    public static object ValidateLiveTemplate(IReadOnlyDictionary<string, string> options)
    {
        var executable = FullPath(Required(options, "executable"));
        RequireFile(executable, "executable");
        var root = Path.Combine(Path.GetTempPath(), "qq-arch7b-v2-template-validation",
            Guid.NewGuid().ToString("N"));
        var fixture = Arch7bV2QualificationFactory.Create(executable, root);
        var adapters = new Arch7bRealCommandAdapterRegistry();
        Arch7bLiveTemplateValidator.Validate(fixture.Template, adapters);
        fixture.Authority.Validate(fixture.Template, fixture.OperatorAuthorization,
            fixture.TemplateFileSha256, DateTimeOffset.UtcNow);
        return new
        {
            verdict = "ARCH7B_REAL_LIVE_PLAN_TEMPLATE_QUALIFIED",
            qualificationOnly = true,
            templateContract = Arch7bV2Contracts.LivePlanTemplateVersion,
            factStoreContract = Arch7bV2Contracts.LiveFactStoreVersion,
            commandTemplateContract = Arch7bV2Contracts.CommandTemplateVersion,
            materializedCommandContract = Arch7bV2Contracts.MaterializedCommandVersion,
            adapterSetSha256 = adapters.EvidenceSha256,
            stageCount = fixture.Template.StageContracts.Count,
            commandCount = fixture.Template.CommandTemplates.Count,
            secretOwnership = Arch7bV2Contracts.SecretLifecycleClassification,
            safety = Arch7bNoLiveSafetyCounters.Zero
        };
    }

    public static async Task<object> QualifyLiveRuntimeAsync(
        IReadOnlyDictionary<string, string> options)
    {
        var executable = FullPath(Required(options, "executable"));
        RequireFile(executable, "executable");
        var runs = Positive(options.GetValueOrDefault("runs"), 20);
        var campaigns = NonNegative(options.GetValueOrDefault("campaigns"), 10);
        var runsPerCampaign = Positive(options.GetValueOrDefault("runs-per-campaign"), 3);
        var dotnetRoot = options.GetValueOrDefault("dotnet-root") ??
                         Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (runs == 1 && campaigns == 0)
        {
            var single = await Arch7bV2ProcessQualifier.RunSingleAsync(executable,
                "single", dotnetRoot: dotnetRoot).ConfigureAwait(false);
            return new
            {
                verdict = single.Passed ? "PASS" : "NO_GO",
                qualificationOnly = true,
                single,
                secretOwnership = Arch7bV2Contracts.SecretLifecycleClassification,
                safety = Arch7bNoLiveSafetyCounters.Zero,
                operationalOneShotStateCount = 0
            };
        }
        var qualification = await Arch7bV2ProcessQualifier.RunAsync(executable, runs,
            campaigns, runsPerCampaign, dotnetRoot: dotnetRoot).ConfigureAwait(false);
        if (qualification.IndependentPasses != qualification.IndependentRuns ||
            qualification.CampaignPasses != qualification.Campaigns ||
            qualification.ResidualProcesses != 0 || qualification.ResidualMarkers != 0)
            throw new Arch7bQualificationException(Arch7bBlockers.ChildProcessFailedUncatalogued);
        return new
        {
            verdict = "ARCH7B_REAL_COMMAND_ADAPTER_PROCESS_REHEARSAL_QUALIFIED",
            qualificationOnly = true,
            qualification,
            secretOwnership = Arch7bV2Contracts.SecretLifecycleClassification,
            safety = Arch7bNoLiveSafetyCounters.Zero,
            operationalOneShotStateCount = 0
        };
    }

    public static async Task<object> MaterializeLiveRunAuthoritiesAsync(
        IReadOnlyDictionary<string, string> options)
    {
        var issuedAtUtc = Utc(options, "issued-at-utc");
        var expiresAtUtc = Utc(options, "expires-at-utc");
        var materialization = await Arch7bLiveAuthorityMaterializer.MaterializeAsync(
            FullPath(Required(options, "freeze-root")),
            Sha(options, "expected-freeze-manifest-sha256"),
            Sha(options, "expected-freeze-packet-sha256"),
            Sha(options, "expected-live-plan-template-sha256"),
            Required(options, "operator-authorization-id"), issuedAtUtc, expiresAtUtc,
            FullPath(Required(options, "output-root")), Required(options, "target-environment"),
            Required(options, "account-id"), Boolean(Required(options, "no-order"))).ConfigureAwait(false);
        return new
        {
            verdict = "ARCH7B_LIVE_RUN_AUTHORITIES_MATERIALIZED",
            qualificationOnly = false,
            materialization,
            safety = Arch7bNoLiveSafetyCounters.Zero
        };
    }
    public static async Task<object> RunOneShotAsync(IReadOnlyDictionary<string, string> options)
    {
        if (!Boolean(Required(options, "no-order")))
            throw new Arch7bQualificationException(Arch7bBlockers.NoOrderRequired);

        if (Boolean(options.GetValueOrDefault("static-preflight-only") ?? "false"))
            return await RunOneShotStaticPreflightAsync(options).ConfigureAwait(false);

        var freezeRoot = FullPath(Required(options, "freeze-root"));
        var authorityPath = FullPath(Required(options, "live-execution-authority-path"));
        var authorizationPath = FullPath(Required(options, "operator-authorization-path"));
        var runRoot = FullPath(Required(options, "run-root"));
        var templatePath = Path.Combine(freezeRoot, "arch7b-one-shot-live-plan-template.json");
        RequireDirectory(freezeRoot, "freeze-root");
        RequireFile(authorityPath, "live-execution-authority-path");
        RequireFile(authorizationPath, "operator-authorization-path");
        RequireFile(templatePath, "live-plan-template");

        var expectedTemplate = Sha(options, "expected-live-plan-template-sha256");
        var expectedAuthority = Sha(options, "expected-live-execution-authority-sha256");
        var expectedAuthorization = Sha(options, "expected-operator-authorization-sha256");
        var template = await Arch7bLiveAuthorityLoaderV2.LoadTemplateAsync(templatePath,
            expectedTemplate).ConfigureAwait(false);
        var authority = await Arch7bLiveAuthorityLoaderV2.LoadAuthorityAsync(authorityPath,
            expectedAuthority).ConfigureAwait(false);
        var authorization = await Arch7bLiveAuthorityLoaderV2.LoadOperatorAsync(authorizationPath,
            expectedAuthorization).ConfigureAwait(false);

        var operationalManifestPath = Path.Combine(freezeRoot,
            "arch7b-operational-execution-authority-manifest-v1.json");
        RequireFile(operationalManifestPath, "operational-execution-authority-manifest");
        var operationalManifestBytes = await File.ReadAllBytesAsync(operationalManifestPath)
            .ConfigureAwait(false);
        var operationalManifest = Arch7bOperationalExecutionAuthorityManifestParser
            .ParseStrict(operationalManifestBytes);

        var requiredInventory = Arch7bRequiredOperationalExecutionAuthorityInventoryBuilder
            .Build(template.Value);
        _ = Arch7bTargetCommandEnvironmentValidator.Validate(template.Value);
        var staticEvidenceRoot = runRoot + "-static-authority";
        Arch7bOperationalExecutionAuthorityValidator.ValidateStatic(requiredInventory,
            operationalManifest, template.Value.FileAuthorities, authority.Value.FileAuthorities,
            Path.Combine(staticEvidenceRoot,
                Arch7bOperationalExecutionAuthorityValidator.ValidationFileName));
        Arch7bChildEntrypointValidator.Validate(template.Value, operationalManifest,
            Path.Combine(staticEvidenceRoot, Arch7bChildEntrypointValidator.ValidationFileName));
        BindHash("freeze-manifest", Sha(options, "expected-freeze-manifest-sha256"),
            template.Value.FreezeManifestSha256, authority.Value.FreezeManifestSha256);
        BindHash("freeze-packet", Sha(options, "expected-freeze-packet-sha256"),
            template.Value.FreezePacketSha256, authority.Value.FreezePacketSha256);
        BindHash("live-plan-template", expectedTemplate, authority.Value.LivePlanTemplateSha256,
            template.FileSha256);
        BindHash("command-template-set", Sha(options, "expected-command-template-set-sha256"),
            template.Value.CommandTemplateSetSha256, authority.Value.CommandTemplateSetSha256);
        BindHash("adapter-set", Sha(options, "expected-adapter-set-sha256"),
            template.Value.AdapterSetSha256, authority.Value.AdapterSetSha256);

        _ = ValidateCliAuthorities(options, template.Value);
        _ = Arch7bTargetBoundBrowserRuntimeAuthorityGate
            .Qualify(template.Value, authority.Value.FileAuthorities);

        var adapters = new Arch7bRealCommandAdapterRegistry();
        var brokerClient = BuildBrokerClient(options, template.Value, adapters);
        var runtime = new Arch7bOneShotLiveExecutionRuntimeV2(new(),
            new Arch7bOneShotProcessRunnerV2(adapters, authority.Value.FileAuthorities),
            adapters, brokerClient);
        return await runtime.RunAsync(template.Value, authority.Value, authorization.Value,
            template.FileSha256, runRoot, TimeProvider.System, new Arch7bCoreOwnedSecretLease())
            .ConfigureAwait(false);
    }

    private static async Task<object> RunOneShotStaticPreflightAsync(
        IReadOnlyDictionary<string, string> options)
    {
        var freezeRoot = FullPath(Required(options, "freeze-root"));
        var runRoot = FullPath(Required(options, "run-root"));
        var templatePath = Path.Combine(freezeRoot, "arch7b-one-shot-live-plan-template.json");
        var operationalManifestPath = Path.Combine(freezeRoot,
            "arch7b-operational-execution-authority-manifest-v1.json");
        RequireDirectory(freezeRoot, "freeze-root");
        RequireFile(templatePath, "live-plan-template");
        RequireFile(operationalManifestPath, "operational-execution-authority-manifest");
        if (Directory.Exists(runRoot) || File.Exists(runRoot))
            throw new Arch7bQualificationException(Arch7bBlockers.RunRootReused);

        var expectedTemplate = Sha(options, "expected-live-plan-template-sha256");
        var template = await Arch7bLiveAuthorityLoaderV2.LoadTemplateAsync(templatePath,
            expectedTemplate).ConfigureAwait(false);
        var operationalManifest = Arch7bOperationalExecutionAuthorityManifestParser.ParseStrict(
            await File.ReadAllBytesAsync(operationalManifestPath).ConfigureAwait(false));
        var requiredInventory = Arch7bRequiredOperationalExecutionAuthorityInventoryBuilder
            .Build(template.Value);
        var targetCommandEnvironment = Arch7bTargetCommandEnvironmentValidator
            .Validate(template.Value);
        var staticEvidenceRoot = runRoot + "-static-authority";
        if (Directory.Exists(staticEvidenceRoot) || File.Exists(staticEvidenceRoot))
            throw new Arch7bQualificationException(Arch7bBlockers.RunRootReused);
        var validation = Arch7bOperationalExecutionAuthorityValidator.ValidateStatic(
            requiredInventory, operationalManifest, template.Value.FileAuthorities,
            template.Value.FileAuthorities, Path.Combine(staticEvidenceRoot,
                Arch7bOperationalExecutionAuthorityValidator.ValidationFileName));
        var childEntrypoints = Arch7bChildEntrypointValidator.Validate(template.Value,
            operationalManifest, Path.Combine(staticEvidenceRoot,
                Arch7bChildEntrypointValidator.ValidationFileName));
        var cliAuthorityBindings = ValidateCliAuthorities(options, template.Value);
        var browserRuntimeAuthority = Arch7bTargetBoundBrowserRuntimeAuthorityGate
            .Qualify(template.Value, template.Value.FileAuthorities);

        return new Arch7bOneShotStaticPreflightEvidence(
            "ARCH7B_ONE_SHOT_STATIC_PREFLIGHT_QUALIFIED", true,
            "TARGET_COMMAND_ENVIRONMENT_VALIDATION", targetCommandEnvironment,
            validation, childEntrypoints, cliAuthorityBindings,
            false, false, false, false, false, 0, 0,
            Arch7bNoLiveSafetyCounters.Zero, browserRuntimeAuthority);
    }

    public static async Task<object> QualifyCoreBrokerCrossRepositoryAsync(
        IReadOnlyDictionary<string, string> options)
    {
        var executable = FullPath(Required(options, "executable"));
        var coreRepository = FullPath(Required(options, "core-repository"));
        var nodeExecutable = FullPath(Required(options, "node-executable"));
        RequireFile(executable, "executable");
        RequireDirectory(coreRepository, "core-repository");
        RequireFile(nodeExecutable, "node-executable");
        var runs = Positive(options.GetValueOrDefault("runs"), 20);
        var campaigns = NonNegative(options.GetValueOrDefault("campaigns"), 10);
        var runsPerCampaign = Positive(options.GetValueOrDefault("runs-per-campaign"), 3);
        var dotnetRoot = options.GetValueOrDefault("dotnet-root") ?? Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(dotnetRoot) && options.ContainsKey("expected-dotnet-executable-sha256"))
        {
            var dotnetExecutable = Path.Combine(FullPath(dotnetRoot),
                OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            if (!File.Exists(dotnetExecutable) || FileSha(dotnetExecutable) !=
                Sha(options, "expected-dotnet-executable-sha256"))
                throw new Arch7bQualificationException(Arch7bV2Blockers.CommandDotnetExecutableShaMismatch);
        }
        var qualification = await Arch7bCrossRepositoryBrokerQualifier.RunAsync(
            executable, coreRepository, nodeExecutable, dotnetRoot,
            Commit(options, "core-commit"), Commit(options, "core-tree"),
            runs, campaigns, runsPerCampaign).ConfigureAwait(false);
        var expectedTotal = checked(runs + campaigns * runsPerCampaign);
        if (qualification.IndependentPasses != runs ||
            qualification.CampaignPasses != campaigns ||
            qualification.SequenceOneToFourPasses != expectedTotal ||
            qualification.FourAdapterPasses != expectedTotal ||
            qualification.TerminalCleanupPasses != expectedTotal ||
            qualification.TransientPayloadPersistenceCount != 0 ||
            qualification.SecretLeakCount != 0 ||
            qualification.ResidualProcessCount != 0 ||
            qualification.Safety != Arch7bNoLiveSafetyCounters.Zero)
            throw new Arch7bQualificationException(
                Arch7bBlockers.ChildProcessFailedUncatalogued);
        return new
        {
            verdict = "ARCH7B_CORE_BROKER_INTRADAY_SUPERVISOR_CROSS_REPO_QUALIFIED",
            qualificationOnly = true,
            qualification,
            safety = Arch7bNoLiveSafetyCounters.Zero,
            operationalOneShotStateCount = 0
        };
    }

    private static Arch7bCoreRdsSecretBrokerClient BuildBrokerClient(
        IReadOnlyDictionary<string, string> options,
        Arch7bOneShotLivePlanTemplate template,
        Arch7bRealCommandAdapterRegistry adapters)
    {
        RequireExact(options, "target-profile", Arch7bRdsTestProfile);
        RequireExact(options, "target-fingerprint", Arch7bRdsTestFingerprint);
        RequireExact(options, "secret-arn", Arch7bPositionImporterSecretArn);
        RequireExact(options, "account-id", Arch7bDemoAccountId);
        var module = BoundFile(options, "core-broker-module",
            "expected-core-broker-module-sha256");
        var cli = BoundFile(options, "core-broker-cli",
            "expected-core-broker-cli-sha256");
        var node = BoundFile(options, "node-executable",
            "expected-node-executable-sha256");
        var executable = BoundFile(options, "executable",
            "expected-intraday-binary-sha256");
        var dotnetRoot = options.GetValueOrDefault("dotnet-root");
        string? dotnetSha = null;
        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            dotnetRoot = FullPath(dotnetRoot);
            RequireDirectory(dotnetRoot, "dotnet-root");
            dotnetSha = Sha(options, "expected-dotnet-executable-sha256");
        }
        var staticAuthority = new Arch7bCoreRdsSecretBrokerStaticAuthority(
            template.CoreCommit, template.CoreTree,
            module.Path, module.Sha256, cli.Path, cli.Sha256,
            node.Path, node.Sha256, template.RuntimeInventorySha256,
            executable.Sha256, Arch7bRdsTestProfile, Arch7bRdsTestFingerprint,
            GuidValue(options, "read1-version-id"), Arch7bPositionImporterSecretArn,
            Arch7bDemoAccountId, false, true, dotnetRoot, dotnetSha);
        staticAuthority.Validate();
        return new Arch7bCoreRdsSecretBrokerClient(staticAuthority, adapters);
    }

    private static (string Path, string Sha256) BoundFile(
        IReadOnlyDictionary<string, string> options, string pathKey, string shaKey)
    {
        var path = FullPath(Required(options, pathKey));
        RequireFile(path, pathKey);
        var expected = Sha(options, shaKey);
        if (FileSha(path) != expected)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.AuthorityBindingMismatch, pathKey);
        return (path, expected);
    }

    private static void RequireExact(IReadOnlyDictionary<string, string> options,
        string key, string expected)
    {
        if (Required(options, key) != expected)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.AuthorityBindingMismatch, key);
    }

    private static string GuidValue(IReadOnlyDictionary<string, string> options, string key)
    {
        var value = Required(options, key);
        return Guid.TryParseExact(value, "D", out _) ? value :
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.AuthorityBindingMismatch, key);
    }

    internal static Arch7bLiveCliAuthorityBindingValidation ValidateCliAuthorities(
        IReadOnlyDictionary<string, string> options, Arch7bOneShotLivePlanTemplate template) =>
        Arch7bLiveCliAuthorityBindingValidator.Validate(template,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["core_repository"] = Required(options, "core-repository"),
                ["intraday_runtime"] = Required(options, "intraday-runtime"),
                ["git_executable"] = Required(options, "git-executable"),
                ["root_certificate"] = Required(options, "root-certificate")
            });

    private static void BindHash(string name, string expected, params string[] actual)
    {
        if (actual.Any(value => value != expected))
            throw new Arch7bQualificationException(Arch7bV2Blockers.AuthorityBindingMismatch, name);
    }

    private static DateTimeOffset Utc(IReadOnlyDictionary<string, string> options, string key)
    {
        var value = Required(options, key);
        return DateTimeOffset.TryParse(value, out var parsed) && parsed.Offset == TimeSpan.Zero
            ? parsed : throw new Arch7bQualificationException(Arch7bV2Blockers.AuthorityBindingMismatch, key);
    }
    private static string Required(IReadOnlyDictionary<string, string> options, string key) =>
        options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"MISSING_REQUIRED_ARGUMENT:{key}");

    private static string Sha(IReadOnlyDictionary<string, string> options, string key)
    {
        var value = Required(options, key);
        return Arch7bOneShotContracts.IsSha256(value) ? value :
            throw new Arch7bQualificationException(Arch7bV2Blockers.AuthorityBindingMismatch, key);
    }


    private static string Commit(IReadOnlyDictionary<string, string> options, string key)
    {
        var value = Required(options, key);
        Arch7bCoreRdsSecretBrokerStaticAuthority.RequireCommit(value);
        return value;
    }
    private static string FullPath(string value)
    {
        Arch7bOneShotAuthorityLoader.RequireAbsolute(value);
        return Path.GetFullPath(value);
    }

    private static void RequireFile(string path, string name)
    {
        if (!File.Exists(path))
            throw new Arch7bQualificationException(Arch7bBlockers.LiveCommandAuthorityIncomplete, name);
    }

    private static void RequireDirectory(string path, string name)
    {
        if (!Directory.Exists(path))
            throw new Arch7bQualificationException(Arch7bBlockers.LiveCommandAuthorityIncomplete, name);
    }

    private static string FileSha(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));

    private static int Positive(string? value, int fallback) => value is null ? fallback :
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed :
            throw new ArgumentException("POSITIVE_INTEGER_REQUIRED");

    private static int NonNegative(string? value, int fallback) => value is null ? fallback :
        int.TryParse(value, out var parsed) && parsed >= 0 ? parsed :
            throw new ArgumentException("NON_NEGATIVE_INTEGER_REQUIRED");

    private static bool Boolean(string value) => bool.TryParse(value, out var parsed) ? parsed :
        throw new ArgumentException("BOOLEAN_REQUIRED");
}
