using System.Security.Cryptography;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public static class Arch7bProgramV2Modes
{
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
        if (runs == 1 && campaigns == 0)
        {
            var single = await Arch7bV2ProcessQualifier.RunSingleAsync(executable,
                "single").ConfigureAwait(false);
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
            campaigns, runsPerCampaign).ConfigureAwait(false);
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

    public static async Task<object> RunOneShotAsync(IReadOnlyDictionary<string, string> options)
    {
        if (!Boolean(Required(options, "no-order")))
            throw new Arch7bQualificationException(Arch7bBlockers.NoOrderRequired);

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

        BindCliAuthority(template.Value, "core_repository", Required(options, "core-repository"),
            mustBeFile: false, template.Value.CoreRepositoryAuthoritySha256);
        BindCliAuthority(template.Value, "intraday_runtime", Required(options, "intraday-runtime"),
            mustBeFile: false, template.Value.RuntimeInventorySha256);
        BindCliAuthority(template.Value, "git_executable", Required(options, "git-executable"),
            mustBeFile: true, null);
        BindCliAuthority(template.Value, "root_certificate", Required(options, "root-certificate"),
            mustBeFile: true, template.Value.RootCaAuthoritySha256);

        var adapters = new Arch7bRealCommandAdapterRegistry();
        var runtime = new Arch7bOneShotLiveExecutionRuntimeV2(new(),
            new Arch7bOneShotProcessRunnerV2(adapters), adapters);
        return await runtime.RunAsync(template.Value, authority.Value, authorization.Value,
            template.FileSha256, runRoot, TimeProvider.System, new Arch7bCoreOwnedSecretLease())
            .ConfigureAwait(false);
    }

    private static void BindCliAuthority(Arch7bOneShotLivePlanTemplate template,
        string authorityId, string cliPath, bool mustBeFile, string? expectedAuthoritySha)
    {
        var path = FullPath(cliPath);
        if (mustBeFile) RequireFile(path, authorityId); else RequireDirectory(path, authorityId);
        if (!template.FileAuthorities.TryGetValue(authorityId, out var authority) ||
            !string.Equals(FullPath(authority.Path), path, StringComparison.OrdinalIgnoreCase) ||
            expectedAuthoritySha is not null && authority.Sha256 != expectedAuthoritySha)
            throw new Arch7bQualificationException(Arch7bV2Blockers.AuthorityBindingMismatch,
                authorityId);
        if (mustBeFile && FileSha(path) != authority.Sha256)
            throw new Arch7bQualificationException(Arch7bV2Blockers.AuthorityBindingMismatch,
                authorityId);
    }

    private static void BindHash(string name, string expected, params string[] actual)
    {
        if (actual.Any(value => value != expected))
            throw new Arch7bQualificationException(Arch7bV2Blockers.AuthorityBindingMismatch, name);
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
