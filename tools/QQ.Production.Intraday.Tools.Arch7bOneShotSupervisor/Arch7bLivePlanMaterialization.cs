using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public enum Arch7bExecutionKind
{
    Internal,
    ChildInvoke,
    ChildStartLongLived,
    ChildAwaitEvidence,
    ChildSignal,
    ChildStop,
    FilesystemGate,
    ExpectedBlockerGate
}

public enum Arch7bPlaceholderValueKind
{
    Literal,
    String,
    Sha256,
    AbsolutePath,
    UtcTimestamp,
    Integer,
    Guid,
    GitCommit,
    Boolean
}

public sealed record Arch7bOneShotFact(
    string ContractVersion,
    string FactType,
    string ProducerStage,
    string ValueJson,
    string EvidenceSha256,
    bool Immutable,
    DateTimeOffset ProducedAtUtc,
    string FactSha256);

public sealed class Arch7bOneShotLiveFactStore
{
    private readonly Dictionary<string, Arch7bOneShotFact> facts = new(StringComparer.Ordinal);
    private readonly string journalPath;

    public Arch7bOneShotLiveFactStore(string runRoot)
    {
        Arch7bOneShotAuthorityLoader.RequireAbsolute(runRoot);
        journalPath = Path.Combine(Path.GetFullPath(runRoot), "live-facts.jsonl");
    }

    public IReadOnlyCollection<Arch7bOneShotFact> Facts => facts.Values;

    public Arch7bOneShotFact Append(string factType, string producerStage, object value,
        string evidenceSha256, DateTimeOffset producedAtUtc)
    {
        if (!Arch7bStages.All.Contains(producerStage, StringComparer.Ordinal) ||
            string.IsNullOrWhiteSpace(factType) || !Arch7bOneShotContracts.IsSha256(evidenceSha256))
            throw new Arch7bQualificationException(Arch7bV2Blockers.FactInvalid, factType);
        if (facts.ContainsKey(factType))
            throw new Arch7bQualificationException(Arch7bV2Blockers.FactReplacementForbidden, factType);
        var valueJson = JsonSerializer.Serialize(value, Arch7bJson.CanonicalOptions);
        var canonical = string.Join('\n', Arch7bV2Contracts.LiveFactStoreVersion, factType,
            producerStage, valueJson, evidenceSha256, true, producedAtUtc.ToUniversalTime().ToString("O"));
        var fact = new Arch7bOneShotFact(Arch7bV2Contracts.LiveFactStoreVersion, factType,
            producerStage, valueJson, evidenceSha256, true, producedAtUtc,
            Arch7bOneShotContracts.Sha256(canonical));
        facts.Add(factType, fact);
        Directory.CreateDirectory(Path.GetDirectoryName(journalPath)!);
        File.AppendAllText(journalPath,
            JsonSerializer.Serialize(fact, Arch7bJson.CanonicalOptions) + Environment.NewLine,
            new UTF8Encoding(false));
        return fact;
    }

    public Arch7bOneShotFact Require(string factType, string expectedProducerStage,
        DateTimeOffset observedUtc, int maximumAgeSeconds)
    {
        if (!facts.TryGetValue(factType, out var fact))
            throw new Arch7bQualificationException(Arch7bV2Blockers.RequiredFactMissing, factType);
        if (!fact.Immutable)
            throw new Arch7bQualificationException(Arch7bV2Blockers.MutableFactForbidden, factType);
        if (!string.Equals(fact.ProducerStage, expectedProducerStage, StringComparison.Ordinal))
            throw new Arch7bQualificationException(Arch7bV2Blockers.FactProducerMismatch, factType);
        if (maximumAgeSeconds >= 0 && observedUtc - fact.ProducedAtUtc > TimeSpan.FromSeconds(maximumAgeSeconds))
            throw new Arch7bQualificationException(Arch7bV2Blockers.FactStale, factType);
        return fact;
    }
}

public sealed record Arch7bFileAuthority(
    string AuthorityId,
    string Path,
    string Sha256,
    bool MustExist,
    bool MustBeInsideRunRoot);

public sealed record Arch7bCommandTemplateArgument(
    string Value,
    Arch7bPlaceholderValueKind ValueKind,
    string? ExpectedProducerStage,
    int MaximumAgeSeconds,
    bool MustBeInsideRunRoot);

public sealed record Arch7bOneShotCommandTemplate(
    string ContractVersion,
    string CommandId,
    string StageId,
    Arch7bExecutionKind ExecutionKind,
    string ExecutableAuthorityId,
    IReadOnlyList<Arch7bCommandTemplateArgument> ArgumentTemplates,
    string WorkingDirectoryAuthorityId,
    string AdapterId,
    string AdapterContractVersion,
    string ExpectedNativeOutputContract,
    int TimeoutSeconds,
    int StandardOutputLimitBytes,
    int StandardErrorLimitBytes,
    string CleanupResourceType,
    bool CausesRdsRead,
    bool CausesCapture,
    bool ReadsSecret,
    IReadOnlyList<string> SecretVariableNames,
    IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> NonSecretEnvironment,
    string? LongLivedProcessKey,
    string EvidenceSha256);

public sealed record Arch7bOneShotStageContract(
    string StageId,
    Arch7bExecutionKind ExecutionKind,
    IReadOnlyList<string> Predecessors,
    IReadOnlyList<string> RequiredFactTypes,
    IReadOnlyList<string> ProducedFactTypes,
    string? SloId,
    string ValidatorId,
    string EvidenceSha256);

public sealed record Arch7bOneShotLivePlanTemplate(
    string ContractVersion,
    string SupervisorCommit,
    string SupervisorTree,
    string CoreCommit,
    string CoreTree,
    string IntradayCommit,
    string IntradayTree,
    string FreezeManifestSha256,
    string FreezePacketSha256,
    string RuntimeInventorySha256,
    string CoreRepositoryAuthoritySha256,
    string CoreTrackedInventorySha256,
    string StaticAuthoritySetSha256,
    string CommandTemplateSetSha256,
    string AdapterSetSha256,
    string RootCaAuthoritySha256,
    string PrivilegeAuthoritySha256,
    string CalendarAuthoritySha256,
    string SloRegistrySha256,
    string ChronologySha256,
    string CleanupAuthoritySha256,
    string TargetEnvironment,
    string AccountId,
    bool NoOrder,
    int MaximumSlots,
    int MaximumRdsReads,
    int MaximumCaptures,
    int MaximumRetries,
    IReadOnlyDictionary<string, Arch7bFileAuthority> FileAuthorities,
    IReadOnlyList<Arch7bOneShotCommandTemplate> CommandTemplates,
    IReadOnlyList<Arch7bOneShotStageContract> StageContracts,
    string EvidenceSha256)
{
    public string? SelectedBrowser { get; init; }

    public string Canonical()
    {
        var canonical = string.Join('\n', ContractVersion, SupervisorCommit, SupervisorTree,
            CoreCommit, CoreTree, IntradayCommit, IntradayTree, FreezeManifestSha256, FreezePacketSha256,
            RuntimeInventorySha256, CoreRepositoryAuthoritySha256, CoreTrackedInventorySha256,
            StaticAuthoritySetSha256, CommandTemplateSetSha256, AdapterSetSha256, RootCaAuthoritySha256,
            PrivilegeAuthoritySha256, CalendarAuthoritySha256, SloRegistrySha256, ChronologySha256,
            CleanupAuthoritySha256, TargetEnvironment, AccountId, NoOrder, MaximumSlots, MaximumRdsReads,
            MaximumCaptures, MaximumRetries,
            string.Join('|', FileAuthorities.OrderBy(value => value.Key, StringComparer.Ordinal).Select(value =>
                $"{value.Key}:{value.Value.AuthorityId}:{value.Value.Path}:{value.Value.Sha256}:{value.Value.MustExist}:{value.Value.MustBeInsideRunRoot}")),
            string.Join('|', CommandTemplates.Select(value => value.EvidenceSha256)),
            string.Join('|', StageContracts.Select(value => value.EvidenceSha256)));
        return SelectedBrowser is null ? canonical : string.Join('\n', canonical, SelectedBrowser);
    }

    public void ValidateEvidence()
    {
        if (EvidenceSha256 != Arch7bOneShotContracts.Sha256(Canonical()))
            throw new Arch7bQualificationException(Arch7bV2Blockers.AuthorityBindingMismatch,
                "live-plan-template-evidence");
    }
}

public sealed record Arch7bOneShotMaterializedCommand(
    string ContractVersion,
    string CommandId,
    string StageId,
    Arch7bExecutionKind ExecutionKind,
    string ExecutablePath,
    string ExecutableSha256,
    IReadOnlyList<string> ArgumentList,
    string WorkingDirectory,
    string AdapterId,
    string AdapterContractVersion,
    string ExpectedNativeOutputContract,
    int TimeoutSeconds,
    int StandardOutputLimitBytes,
    int StandardErrorLimitBytes,
    string CleanupResourceType,
    bool CausesRdsRead,
    bool CausesCapture,
    bool ReadsSecret,
    IReadOnlyList<string> SecretVariableNames,
    IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> NonSecretEnvironment,
    string? LongLivedProcessKey,
    string AuthorityPath,
    string AuthorityFileSha256,
    string EvidenceSha256);

public static partial class Arch7bTypedPlaceholder
{
    [GeneratedRegex("^\\$\\{(?<scope>fact|artifact|authority):(?<name>[a-z0-9_]+)\\.(?<field>[a-z0-9_]+)\\}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static (string Scope, string Name, string Field)? Parse(string value)
    {
        var match = Pattern().Match(value);
        return match.Success
            ? (match.Groups["scope"].Value, match.Groups["name"].Value, match.Groups["field"].Value)
            : null;
    }
}

public sealed class Arch7bOneShotCommandMaterializer
{
    public async Task<Arch7bOneShotMaterializedCommand> MaterializeAsync(
        Arch7bOneShotCommandTemplate template,
        Arch7bOneShotLiveFactStore factStore,
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities,
        string runRoot,
        DateTimeOffset observedUtc,
        CancellationToken cancellationToken = default)
    {
        if (template.ContractVersion != Arch7bV2Contracts.CommandTemplateVersion)
            throw new Arch7bQualificationException(Arch7bV2Blockers.CommandTemplateInvalid, template.CommandId);
        var executable = RequireAuthority(authorities, template.ExecutableAuthorityId, runRoot);
        var workingDirectory = RequireAuthority(authorities, template.WorkingDirectoryAuthorityId, runRoot);
        var arguments = template.ArgumentTemplates.Select(argument => Resolve(argument, factStore,
            authorities, runRoot, observedUtc)).ToArray();
        var nonSecretEnvironment = Arch7bSealedNonSecretEnvironment.ValidateTemplate(
            template.NonSecretEnvironment, authorities, template.CommandId,
            template.StageId);
        if (arguments.Any(Arch7bV2ArgumentSafety.IsSecretArgumentValue))
            throw new Arch7bQualificationException(Arch7bBlockers.SecretInArgument, template.CommandId);
        var commandCore = string.Join('\n', Arch7bV2Contracts.MaterializedCommandVersion,
            template.CommandId, template.StageId, template.ExecutionKind, executable.Path, executable.Sha256,
            string.Join('|', arguments), workingDirectory.Path, template.AdapterId,
            template.AdapterContractVersion, template.ExpectedNativeOutputContract, template.TimeoutSeconds,
            template.StandardOutputLimitBytes, template.StandardErrorLimitBytes, template.CleanupResourceType,
            template.CausesRdsRead, template.CausesCapture, template.ReadsSecret,
            string.Join('|', template.SecretVariableNames),
            Arch7bSealedNonSecretEnvironment.Canonical(nonSecretEnvironment),
            template.LongLivedProcessKey ?? string.Empty);
        var evidenceSha = Arch7bOneShotContracts.Sha256(commandCore);
        var directory = Path.Combine(Path.GetFullPath(runRoot), "commands", template.StageId, template.CommandId);
        Directory.CreateDirectory(directory);
        var authorityPath = Path.Combine(directory, "stage-command-authority.json");
        if (File.Exists(authorityPath))
            throw new Arch7bQualificationException(Arch7bV2Blockers.MaterializedCommandAlreadyExists,
                template.CommandId);
        var provisional = new Arch7bOneShotMaterializedCommand(
            Arch7bV2Contracts.MaterializedCommandVersion, template.CommandId, template.StageId,
            template.ExecutionKind, executable.Path, executable.Sha256, arguments, workingDirectory.Path,
            template.AdapterId, template.AdapterContractVersion, template.ExpectedNativeOutputContract,
            template.TimeoutSeconds, template.StandardOutputLimitBytes, template.StandardErrorLimitBytes,
            template.CleanupResourceType, template.CausesRdsRead, template.CausesCapture,
            template.ReadsSecret, template.SecretVariableNames, nonSecretEnvironment, template.LongLivedProcessKey,
            authorityPath, string.Empty, evidenceSha);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(provisional, Arch7bJson.CanonicalOptions);
        await File.WriteAllBytesAsync(authorityPath, bytes, cancellationToken).ConfigureAwait(false);
        var fileSha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return provisional with { AuthorityFileSha256 = fileSha };
    }

    private static string Resolve(Arch7bCommandTemplateArgument argument,
        Arch7bOneShotLiveFactStore facts, IReadOnlyDictionary<string, Arch7bFileAuthority> authorities,
        string runRoot, DateTimeOffset observedUtc)
    {
        var placeholder = Arch7bTypedPlaceholder.Parse(argument.Value);
        if (placeholder is null)
        {
            if (argument.ValueKind != Arch7bPlaceholderValueKind.Literal)
                throw new Arch7bQualificationException(Arch7bV2Blockers.PlaceholderUnknown, argument.Value);
            return argument.Value;
        }
        var (scope, name, field) = placeholder.Value;
        string value;
        if (scope == "authority")
        {
            var authority = RequireAuthority(authorities, name, runRoot);
            value = field switch
            {
                "path" => authority.Path,
                "sha256" => authority.Sha256,
                _ => throw new Arch7bQualificationException(Arch7bV2Blockers.PlaceholderUnknown, argument.Value)
            };
        }
        else
        {
            var producer = argument.ExpectedProducerStage ?? throw new Arch7bQualificationException(
                Arch7bV2Blockers.FactProducerMismatch, name);
            var fact = facts.Require(name, producer, observedUtc, argument.MaximumAgeSeconds);
            using var document = JsonDocument.Parse(fact.ValueJson);
            if (!document.RootElement.TryGetProperty(field, out var element))
                throw new Arch7bQualificationException(Arch7bV2Blockers.RequiredFactMissing, argument.Value);
            value = element.ValueKind == JsonValueKind.String
                ? element.GetString() ?? string.Empty : element.GetRawText();
        }
        ValidateValue(value, argument.ValueKind);
        if (argument.MustBeInsideRunRoot)
        {
            Arch7bOneShotAuthorityLoader.RequireAbsolute(value);
            Arch7bOneShotAuthorityLoader.RequireInside(runRoot, value);
        }
        return value;
    }

    private static Arch7bFileAuthority RequireAuthority(
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities, string authorityId, string runRoot)
    {
        if (!authorities.TryGetValue(authorityId, out var authority))
            throw new Arch7bQualificationException(Arch7bV2Blockers.AuthorityBindingMismatch, authorityId);
        if (authorityId == "chrome_executable")
            Arch7bSealedNonSecretEnvironment.ValidateChromeAuthority(authority);
        Arch7bOneShotAuthorityLoader.RequireAbsolute(authority.Path);
        if (authority.MustBeInsideRunRoot) Arch7bOneShotAuthorityLoader.RequireInside(runRoot, authority.Path);
        if (authority.MustExist && !File.Exists(authority.Path) && !Directory.Exists(authority.Path))
            throw new Arch7bQualificationException(Arch7bV2Blockers.AuthorityBindingMismatch, authorityId);
        if (!Arch7bOneShotContracts.IsSha256(authority.Sha256))
            throw new Arch7bQualificationException(Arch7bV2Blockers.AuthorityBindingMismatch, authorityId);
        return authority;
    }

    private static void ValidateValue(string value, Arch7bPlaceholderValueKind kind)
    {
        var valid = kind switch
        {
            Arch7bPlaceholderValueKind.Literal or Arch7bPlaceholderValueKind.String => !string.IsNullOrWhiteSpace(value),
            Arch7bPlaceholderValueKind.Sha256 => Arch7bOneShotContracts.IsSha256(value),
            Arch7bPlaceholderValueKind.AbsolutePath => Path.IsPathFullyQualified(value),
            Arch7bPlaceholderValueKind.UtcTimestamp => DateTimeOffset.TryParse(value, out var date) && date.Offset == TimeSpan.Zero,
            Arch7bPlaceholderValueKind.Integer => int.TryParse(value, out _),
            Arch7bPlaceholderValueKind.Guid => System.Guid.TryParseExact(value, "D", out var guid) &&
                guid != System.Guid.Empty,
            Arch7bPlaceholderValueKind.GitCommit => value.Length == 40 &&
                value.All(character => char.IsAsciiHexDigit(character) && !char.IsUpper(character)),
            Arch7bPlaceholderValueKind.Boolean => value is "true" or "false",
            _ => false
        };
        if (!valid) throw new Arch7bQualificationException(Arch7bV2Blockers.PlaceholderTypeMismatch, value);
    }
}

internal static class Arch7bJson
{
    public static JsonSerializerOptions CanonicalOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };
}
