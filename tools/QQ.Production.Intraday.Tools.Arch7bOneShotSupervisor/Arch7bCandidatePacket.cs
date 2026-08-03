using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bRepositoryIdentity(string Repository, string Commit, string Tree);

public sealed record Arch7bCandidatePacket(
    string ContractVersion,
    Arch7bRepositoryIdentity Intraday,
    Arch7bRepositoryIdentity Core,
    string CalendarAuthority,
    string CalendarSource,
    IReadOnlyList<Arch7bSloDefinition> SloRegistry,
    int GlobalSloCount,
    int LocalSloCount,
    int PreSlotCriticalPathSloSeconds,
    int RequiredPreparationMarginSeconds,
    Arch7bChronologyValidation Chronology,
    string CleanupAuthority,
    int CleanupResourceTypeCount,
    Arch7bCoreStaticAuthorityBinding CoreCommandAuthority,
    string SupervisorContract,
    Arch7bSimulationQualification Simulations,
    IReadOnlyList<string> PrimaryRoundtripEvidenceSha256,
    string ExecutableSha256,
    IReadOnlyList<string> DependencyClosure,
    bool HistoricalPromptDependency,
    int UnversionedSourceCount,
    int UnresolvedReferenceCount,
    int ContradictionCount,
    Arch7bNoLiveSafetyCounters Safety,
    string EvidenceSha256);

public sealed record Arch7bCandidatePacketFiles(string JsonPath, string JsonSha256, string MarkdownPath,
    string MarkdownSha256);

public static class Arch7bCandidatePacketWriter
{
    private const string SupervisorProjectRoot =
        "tools/QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<Arch7bCandidatePacketFiles> WriteAsync(string outputRoot,
        string intradayCommit, string intradayTree, string executablePath,
        string intradayRepositoryPath,
        Arch7bCoreStaticAuthorityBinding coreBinding, Arch7bSimulationQualification simulations,
        IReadOnlyList<string>? primaryRoundtrips = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputRoot);
        var sourceHashes = await ResolveSourceHashesAsync(intradayRepositoryPath, coreBinding, cancellationToken)
            .ConfigureAwait(false);
        var registry = Arch7bGlobalSloRegistry.CreateDefault(sourceHashes, intradayCommit);
        var chronology = Arch7bCrossRepositoryChronology.Validate(Arch7bCrossRepositoryChronology.CreateDefault(), registry);
        if (!chronology.IsValid) throw new InvalidDataException(string.Join(',', chronology.Blockers));
        var margin = Math.Max(Arch7bGlobalSloRegistry.GlobalMinimumPreparationMarginSeconds,
            chronology.PreSlotCriticalPathSloSeconds + Arch7bGlobalSloRegistry.GlobalPreparationSafetyReserveSeconds);
        var executableSha = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(executablePath,
            cancellationToken).ConfigureAwait(false)));
        var roundtrips = primaryRoundtrips ?? [];
        var dependencyClosure = registry.Entries.Select(value => $"{value.SourceRepository}:{value.SourceCommit}:{value.SourceFile}:{value.SourceFileSha256}")
            .Concat(coreBinding.Commands.SelectMany(value => value.Sources)
                .Select(value => $"{Arch7bOneShotContracts.CoreRepository}:{Arch7bOneShotContracts.CoreCommit}:{value.SourceFile}:{value.SourceFileSha256}"))
            .Concat(sourceHashes.Where(value => value.Key.StartsWith(SupervisorProjectRoot, StringComparison.Ordinal))
                .Select(value => $"{Arch7bOneShotContracts.IntradayRepository}:{intradayCommit}:" +
                    $"{value.Key}:{value.Value}"))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var canonical = string.Join('\n', intradayCommit, intradayTree, coreBinding.EvidenceSha256,
            chronology.EvidenceSha256, simulations.EvidenceSha256, executableSha, string.Join('|', roundtrips));
        var packet = new Arch7bCandidatePacket(Arch7bOneShotContracts.SupervisorEvidenceVersion,
            new(Arch7bOneShotContracts.IntradayRepository, intradayCommit, intradayTree),
            new(Arch7bOneShotContracts.CoreRepository, coreBinding.CoreCommit, coreBinding.CoreTree),
            Arch7bOneShotContracts.OperationalSlotSelectionPolicyVersion,
            "PmsShadowIntradayCadenceContract", registry.Entries,
            registry.Entries.Count(value => value.SloId.StartsWith("GLOBAL_", StringComparison.Ordinal)),
            registry.Entries.Count(value => !value.SloId.StartsWith("GLOBAL_", StringComparison.Ordinal)),
            chronology.PreSlotCriticalPathSloSeconds, margin, chronology,
            Arch7bOneShotContracts.TerminalCleanupSupervisorVersion,
            Arch7bTerminalCleanupSupervisor.RequiredResourceTypes.Count,
            coreBinding, Arch7bOneShotContracts.LiveSupervisorVersion, simulations, roundtrips,
            executableSha, dependencyClosure, false, 0, 0, 0, Arch7bNoLiveSafetyCounters.Zero,
            Arch7bOneShotContracts.Sha256(canonical));

        var jsonPath = Path.Combine(outputRoot, "arch7b-one-shot-live-supervisor-candidate-v1.json");
        var markdownPath = Path.Combine(outputRoot, "arch7b-one-shot-live-supervisor-candidate-v1.md");
        var json = JsonSerializer.Serialize(packet, JsonOptions) + Environment.NewLine;
        var markdown = BuildMarkdown(packet);
        await File.WriteAllTextAsync(jsonPath, json, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(markdownPath, markdown, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        return new(jsonPath, Arch7bOneShotContracts.Sha256(json), markdownPath,
            Arch7bOneShotContracts.Sha256(markdown));
    }

    private static async Task<IReadOnlyDictionary<string, string>> ResolveSourceHashesAsync(
        string intradayRepositoryPath, Arch7bCoreStaticAuthorityBinding coreBinding,
        CancellationToken cancellationToken)
    {
        var hashes = coreBinding.Commands.SelectMany(value => value.Sources)
            .GroupBy(value => value.SourceFile, StringComparer.Ordinal)
            .ToDictionary(value => value.Key, value => value.First().SourceFileSha256, StringComparer.Ordinal);
        var unresolved = Arch7bGlobalSloRegistry.CreateDefault().Entries
            .Where(value => value.SourceRepository == Arch7bOneShotContracts.IntradayRepository)
            .Select(value => value.SourceFile).Distinct(StringComparer.Ordinal);
        foreach (var sourceFile in unresolved)
        {
            var path = Path.Combine(intradayRepositoryPath, sourceFile.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new FileNotFoundException("INTRADAY_SOURCE_AUTHORITY_MISSING", path);
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            hashes[sourceFile] = Convert.ToHexStringLower(SHA256.HashData(bytes));
        }

        var supervisorDirectory = Path.Combine(intradayRepositoryPath,
            SupervisorProjectRoot.Replace('/', Path.DirectorySeparatorChar));
        foreach (var path in Directory.EnumerateFiles(supervisorDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var sourceFile = Path.GetRelativePath(intradayRepositoryPath, path)
                .Replace(Path.DirectorySeparatorChar, '/');
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            hashes[sourceFile] = Convert.ToHexStringLower(SHA256.HashData(bytes));
        }

        if (hashes.Values.Any(value => !Arch7bOneShotContracts.IsSha256(value)))
            throw new InvalidDataException("SOURCE_AUTHORITY_SHA256_INVALID");
        return hashes;
    }

    private static string BuildMarkdown(Arch7bCandidatePacket packet) => $"""
        # ARCH7B one-shot live supervisor candidate v1

        - Intraday: `{packet.Intraday.Commit}` / `{packet.Intraday.Tree}`
        - Core: `{packet.Core.Commit}` / `{packet.Core.Tree}`
        - Calendar: `{packet.CalendarAuthority}` from `{packet.CalendarSource}`
        - SLOs: {packet.SloRegistry.Count} ({packet.GlobalSloCount} global, {packet.LocalSloCount} sourced)
        - Pre-slot critical path: {packet.PreSlotCriticalPathSloSeconds} seconds
        - Required preparation margin: {packet.RequiredPreparationMarginSeconds} seconds
        - Chronology: {packet.Chronology.StageCount} stages / {packet.Chronology.EdgeCount} edges / DAG={packet.Chronology.IsValid}
        - Cleanup resources: {packet.CleanupResourceTypeCount}
        - Independent simulations: {packet.Simulations.IndependentPassCount}/{packet.Simulations.IndependentRunCount}
        - Sequential campaigns: {packet.Simulations.SequentialCampaignPassCount}/{packet.Simulations.SequentialCampaignCount}
        - Primary static roundtrips: {packet.PrimaryRoundtripEvidenceSha256.Count}/3
        - Executable SHA-256: `{packet.ExecutableSha256}`
        - HistoricalPromptDependency: {packet.HistoricalPromptDependency.ToString().ToLowerInvariant()}
        - UnversionedSourceCount: {packet.UnversionedSourceCount}
        - UnresolvedReferenceCount: {packet.UnresolvedReferenceCount}
        - ContradictionCount: {packet.ContradictionCount}
        - Evidence SHA-256: `{packet.EvidenceSha256}`
        """ + Environment.NewLine;
}
