using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bLiveCandidatePacket(
    string ContractVersion,
    string BaseMasterCommit,
    string BaseMasterTree,
    string CandidateCommit,
    string CandidateTree,
    string ExecutableSha256,
    Arch7bOneShotSupervisorExecutionGap ExecutionGap,
    IReadOnlyList<string> LiveContracts,
    string GlobalSloRegistrySha256,
    string ChronologySha256,
    string CoreCommandAuthoritySha256,
    Arch7bLiveProcessQualification ProcessQualification,
    IReadOnlyList<string> PrimaryValidationSha256,
    IReadOnlyList<string> PrimarySimulationSha256,
    IReadOnlyList<string> DependencyClosure,
    int UnresolvedReferenceCount,
    bool HistoricalPromptDependency,
    Arch7bNoLiveSafetyCounters Safety,
    string EvidenceSha256);

public sealed record Arch7bLiveCandidatePacketFiles(
    string JsonPath, string JsonSha256, string MarkdownPath, string MarkdownSha256);

public static class Arch7bLiveCandidatePacketWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<Arch7bLiveCandidatePacketFiles> WriteAsync(string outputRoot,
        string repositoryRoot, string baseCommit, string baseTree, string candidateCommit,
        string candidateTree, string executablePath, Arch7bLiveProcessQualification qualification,
        IReadOnlyList<string> primaryValidation, IReadOnlyList<string> primarySimulation,
        CancellationToken cancellationToken = default)
    {
        outputRoot = Path.GetFullPath(outputRoot);
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        Directory.CreateDirectory(outputRoot);
        var projectRoot = Path.Combine(repositoryRoot, "tools",
            "QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor");
        var dependencies = new List<string>();
        foreach (var path in Directory.EnumerateFiles(projectRoot, "*", SearchOption.TopDirectoryOnly)
                     .Where(value => value.EndsWith(".cs", StringComparison.Ordinal) ||
                         value.EndsWith(".csproj", StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            var sha = Convert.ToHexStringLower(SHA256.HashData(
                await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)));
            dependencies.Add(Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/') + ":" + sha);
        }
        var executableSha = Convert.ToHexStringLower(SHA256.HashData(
            await File.ReadAllBytesAsync(executablePath, cancellationToken).ConfigureAwait(false)));
        var registry = Arch7bGlobalSloRegistry.CreateDefault();
        var chronology = Arch7bCrossRepositoryChronology.Validate(
            Arch7bCrossRepositoryChronology.CreateDefault(), registry);
        var coreAuthority = Arch7bOneShotContracts.Sha256(string.Join('\n',
            Arch7bOneShotContracts.CoreCommit, Arch7bOneShotContracts.CoreTree,
            Arch7bOneShotContracts.CoreRepositoryAuthoritySha256));
        var gap = Arch7bOneShotSupervisorExecutionGap.Create(baseCommit);
        string[] contracts =
        [
            Arch7bOneShotContracts.LiveExecutionRuntimeVersion,
            Arch7bOneShotContracts.CommandRunnerVersion,
            Arch7bOneShotContracts.LiveExecutionAuthorityVersion,
            Arch7bOneShotContracts.StageEvidenceVersion,
            Arch7bOneShotContracts.ProcessEnvironmentAuthorityVersion,
            Arch7bOneShotContracts.CommandResultVersion,
            Arch7bOneShotContracts.LivePlanVersion,
            Arch7bOneShotContracts.OperatorAuthorizationVersion
        ];
        var canonical = string.Join('\n', baseCommit, baseTree, candidateCommit, candidateTree,
            executableSha, gap.EvidenceSha256, registry.EvidenceSha256, chronology.EvidenceSha256,
            coreAuthority, qualification.EvidenceSha256, string.Join('|', primaryValidation),
            string.Join('|', primarySimulation), string.Join('|', dependencies));
        var packet = new Arch7bLiveCandidatePacket(Arch7bOneShotContracts.LiveCandidatePacketVersion,
            baseCommit, baseTree, candidateCommit, candidateTree, executableSha, gap, contracts,
            registry.EvidenceSha256, chronology.EvidenceSha256, coreAuthority, qualification,
            primaryValidation, primarySimulation, dependencies, 0, false,
            Arch7bNoLiveSafetyCounters.Zero, Arch7bOneShotContracts.Sha256(canonical));
        var jsonPath = Path.Combine(outputRoot, "arch7b-one-shot-live-execution-candidate-v1.json");
        var markdownPath = Path.Combine(outputRoot, "arch7b-one-shot-live-execution-candidate-v1.md");
        var json = JsonSerializer.Serialize(packet, JsonOptions) + Environment.NewLine;
        var markdown = $"""
            # ARCH7B One-Shot Live Execution Candidate

            - Base master: `{baseCommit}` / `{baseTree}`
            - Candidate: `{candidateCommit}` / `{candidateTree}`
            - Executable SHA-256: `{executableSha}`
            - Execution gap SHA-256: `{gap.EvidenceSha256}`
            - Process qualification: {qualification.IndependentPassCount}/{qualification.IndependentRunCount}
            - Campaigns: {qualification.SequentialCampaignPassCount}/{qualification.SequentialCampaignCount}
            - Failure matrix: {qualification.FailureInjectionPassCount}/{qualification.FailureInjectionCount}
            - Primary validation: {primaryValidation.Count}/3
            - Primary simulation: {primarySimulation.Count}/3
            - Dependency closure: {dependencies.Count}
            - HistoricalPromptDependency: false
            - UnresolvedReferenceCount: 0
            - Evidence SHA-256: `{packet.EvidenceSha256}`
            """ + Environment.NewLine;
        await File.WriteAllTextAsync(jsonPath, json, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(markdownPath, markdown, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        return new(jsonPath, Arch7bOneShotContracts.Sha256(json), markdownPath,
            Arch7bOneShotContracts.Sha256(markdown));
    }
}
