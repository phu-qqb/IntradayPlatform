using System.Security.Cryptography;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bCorePrequalificationConfigV1(
    string RepositoryRoot,
    string OutputRoot,
    string ExpectedCommit,
    string ExpectedTree,
    string BrowserExecutablePath,
    string ExpectedBrowserExecutableSha256)
{
    public const string ContractVersion = "arch7b_core_prequalification_config_v1";
}

public sealed record Arch7bCorePrequalificationConfigValidationContext(
    string CoreRepositoryRoot,
    string CoreNodeRuntimeRoot,
    string RunRoot,
    string ExpectedCommit,
    string ExpectedTree,
    string BrowserExecutablePath,
    string ExpectedBrowserExecutableSha256,
    bool RequireOutputRootAbsent = true);

public sealed record Arch7bPreparedCorePrequalificationConfig(
    Arch7bCorePrequalificationConfigV1 Config,
    byte[] Bytes,
    string Sha256,
    string EvidenceSha256);

public static class Arch7bCorePrequalificationConfigSerializer
{
    public static byte[] Serialize(Arch7bCorePrequalificationConfigV1 value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
               {
                   Indented = false,
                   SkipValidation = false
               }))
        {
            writer.WriteStartObject();
            writer.WriteString("repositoryRoot", value.RepositoryRoot);
            writer.WriteString("outputRoot", value.OutputRoot);
            writer.WriteString("expectedCommit", value.ExpectedCommit);
            writer.WriteString("expectedTree", value.ExpectedTree);
            writer.WriteString("browserExecutablePath", value.BrowserExecutablePath);
            writer.WriteString("expectedBrowserExecutableSha256",
                value.ExpectedBrowserExecutableSha256);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }
}

public static class Arch7bCorePrequalificationConfigParser
{
    private static readonly string[] PropertyNames =
    [
        "repositoryRoot",
        "outputRoot",
        "expectedCommit",
        "expectedTree",
        "browserExecutablePath",
        "expectedBrowserExecutableSha256"
    ];

    public static Arch7bCorePrequalificationConfigV1 ParseAndValidate(
        ReadOnlySpan<byte> bytes,
        Arch7bCorePrequalificationConfigValidationContext context)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf)
            Fail(Arch7bV2Blockers.CorePrequalificationConfigNamingMismatch, "utf8-bom");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8
            });
        }
        catch (JsonException exception)
        {
            Fail(Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch,
                exception.GetType().Name);
            throw;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                Fail(Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch, "root");

            var properties = root.EnumerateObject().ToArray();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in properties)
            {
                if (!names.Add(property.Name))
                    Fail(Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch,
                        "duplicate-property");
                if (!PropertyNames.Contains(property.Name, StringComparer.Ordinal) &&
                    (PropertyNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase) ||
                     property.Name.Contains('_', StringComparison.Ordinal)))
                    Fail(Arch7bV2Blockers.CorePrequalificationConfigNamingMismatch, property.Name);
            }
            if (properties.Length != PropertyNames.Length || !names.SetEquals(PropertyNames) ||
                !properties.Select(value => value.Name).SequenceEqual(PropertyNames,
                    StringComparer.Ordinal))
                Fail(Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch,
                    "property-set");

            var config = new Arch7bCorePrequalificationConfigV1(
                String(root, "repositoryRoot"),
                String(root, "outputRoot"),
                String(root, "expectedCommit"),
                String(root, "expectedTree"),
                String(root, "browserExecutablePath"),
                String(root, "expectedBrowserExecutableSha256"));
            Validate(config, context);
            return config;
        }
    }

    public static Arch7bPreparedCorePrequalificationConfig Prepare(
        Arch7bOneShotLivePlanTemplate template, string runRoot)
    {
        var repository = Authority(template, "core_repository");
        var chrome = Authority(template, "chrome_executable");
        var config = new Arch7bCorePrequalificationConfigV1(
            Path.GetFullPath(repository.Path),
            Path.Combine(Path.GetFullPath(runRoot), "core-prequalification-output"),
            template.CoreCommit,
            template.CoreTree,
            Path.GetFullPath(chrome.Path),
            chrome.Sha256);
        var bytes = Arch7bCorePrequalificationConfigSerializer.Serialize(config);
        ParseAndValidate(bytes, Context(template, runRoot));
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var evidence = Arch7bOneShotContracts.Sha256(string.Join('\n',
            Arch7bCorePrequalificationConfigV1.ContractVersion, sha,
            config.RepositoryRoot, config.OutputRoot, config.ExpectedCommit,
            config.ExpectedTree, config.BrowserExecutablePath,
            config.ExpectedBrowserExecutableSha256));
        return new(config, bytes, sha, evidence);
    }

    public static Arch7bCorePrequalificationConfigValidationContext Context(
        Arch7bOneShotLivePlanTemplate template, string runRoot,
        bool requireOutputRootAbsent = true)
    {
        var repository = Authority(template, "core_repository");
        var coreNode = Authority(template, "core_node_runtime");
        var chrome = Authority(template, "chrome_executable");
        return new(Path.GetFullPath(repository.Path), Path.GetFullPath(coreNode.Path),
            Path.GetFullPath(runRoot), template.CoreCommit, template.CoreTree,
            Path.GetFullPath(chrome.Path), chrome.Sha256, requireOutputRootAbsent);
    }

    private static void Validate(Arch7bCorePrequalificationConfigV1 value,
        Arch7bCorePrequalificationConfigValidationContext context)
    {
        if (!AbsoluteEquals(value.RepositoryRoot, context.CoreRepositoryRoot) ||
            AbsoluteEquals(value.RepositoryRoot, context.CoreNodeRuntimeRoot) ||
            !Directory.Exists(value.RepositoryRoot) ||
            (!Directory.Exists(Path.Combine(value.RepositoryRoot, ".git")) &&
             !File.Exists(Path.Combine(value.RepositoryRoot, ".git"))) ||
            value.ExpectedCommit != context.ExpectedCommit ||
            value.ExpectedTree != context.ExpectedTree ||
            !IsGitObjectId(value.ExpectedCommit) || !IsGitObjectId(value.ExpectedTree))
            Fail(Arch7bV2Blockers.CorePrequalificationConfigRepositoryAuthorityMismatch,
                "core-repository");

        var expectedOutput = Path.Combine(Path.GetFullPath(context.RunRoot),
            "core-prequalification-output");
        if (!AbsoluteEquals(value.OutputRoot, expectedOutput) ||
            !Inside(context.RunRoot, value.OutputRoot) ||
            (context.RequireOutputRootAbsent &&
             (Directory.Exists(value.OutputRoot) || File.Exists(value.OutputRoot))))
            Fail(Arch7bV2Blockers.CorePrequalificationConfigOutputRootInvalid,
                "output-root");

        if (!AbsoluteEquals(value.BrowserExecutablePath, context.BrowserExecutablePath) ||
            !File.Exists(value.BrowserExecutablePath) ||
            value.ExpectedBrowserExecutableSha256 != context.ExpectedBrowserExecutableSha256 ||
            !Arch7bOneShotContracts.IsSha256(value.ExpectedBrowserExecutableSha256) ||
            Convert.ToHexStringLower(SHA256.HashData(
                File.ReadAllBytes(value.BrowserExecutablePath))) !=
            value.ExpectedBrowserExecutableSha256)
            Fail(Arch7bV2Blockers.CorePrequalificationConfigBrowserAuthorityMismatch,
                "chrome-executable");
    }

    private static Arch7bFileAuthority Authority(Arch7bOneShotLivePlanTemplate template,
        string authorityId) => template.FileAuthorities.TryGetValue(authorityId, out var authority)
        ? authority
        : throw new Arch7bQualificationException(
            authorityId == "chrome_executable"
                ? Arch7bV2Blockers.CorePrequalificationConfigBrowserAuthorityMismatch
                : Arch7bV2Blockers.CorePrequalificationConfigRepositoryAuthorityMismatch,
            authorityId);

    private static string String(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
            Fail(Arch7bV2Blockers.CorePrequalificationConfigPropertySetMismatch, name);
        return property.GetString()!;
    }

    private static bool AbsoluteEquals(string left, string right)
    {
        if (!Path.IsPathFullyQualified(left) || !Path.IsPathFullyQualified(right)) return false;
        return string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static bool Inside(string root, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar,
            StringComparison.Ordinal) && !Path.IsPathFullyQualified(relative);
    }

    private static bool IsGitObjectId(string value) => value.Length == 40 &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void Fail(string blocker, string detail) =>
        throw new Arch7bQualificationException(blocker, detail);
}

public sealed record Arch7bCorePrequalificationPreSpawnFailureEvidence(
    string ContractVersion,
    string BlockerCode,
    string StageId,
    string ConfigPath,
    string ConfigSha256,
    string PreSlotConfigSha256,
    bool ChildProcessStarted,
    bool ChildReceiptPresent,
    string EvidenceSha256);