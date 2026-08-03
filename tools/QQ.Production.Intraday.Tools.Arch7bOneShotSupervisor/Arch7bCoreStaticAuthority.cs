using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bCoreSourceAuthority(
    string Authority,
    string SourceFile,
    string SourceFileSha256,
    string Role,
    IReadOnlyList<string> RequiredTokens);

public sealed record Arch7bCoreCommandAuthority(
    string Authority,
    string Runtime,
    string Entrypoint,
    string ExecutableSha256,
    IReadOnlyList<string> StaticArguments,
    IReadOnlyList<string> DeclaredLivePlaceholders,
    bool NoOrder,
    IReadOnlyList<Arch7bCoreSourceAuthority> Sources,
    string EvidenceSha256);

public sealed record Arch7bCoreStaticAuthorityBinding(
    string ContractVersion,
    string CoreCommit,
    string CoreTree,
    string RepositoryAuthoritySha256,
    IReadOnlyList<Arch7bCoreCommandAuthority> Commands,
    int SecretReads,
    int DatabaseConnections,
    int PortalHttpRequests,
    string EvidenceSha256);

public interface IArch7bCoreRepositoryReader
{
    Task<string> HeadAsync(CancellationToken cancellationToken = default);
    Task<string> TreeAsync(CancellationToken cancellationToken = default);
    Task<string> ReadTextAsync(string commit, string path, CancellationToken cancellationToken = default);
}

public sealed class Arch7bGitCoreRepositoryReader(string repositoryPath, string gitExecutable = "git")
    : IArch7bCoreRepositoryReader
{
    public Task<string> HeadAsync(CancellationToken cancellationToken = default) =>
        RunAsync(["-C", repositoryPath, "rev-parse", "HEAD"], cancellationToken);

    public Task<string> TreeAsync(CancellationToken cancellationToken = default) =>
        RunAsync(["-C", repositoryPath, "rev-parse", "HEAD^{tree}"], cancellationToken);

    public Task<string> ReadTextAsync(string commit, string path, CancellationToken cancellationToken = default) =>
        RunAsync(["-C", repositoryPath, "show", $"{commit}:{path}"], cancellationToken, trim: false);

    private async Task<string> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken,
        bool trim = true)
    {
        var start = new ProcessStartInfo(gitExecutable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("GIT_PROCESS_START_FAILED");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"GIT_AUTHORITY_READ_FAILED:{await error.ConfigureAwait(false)}");
        var value = await output.ConfigureAwait(false);
        return trim ? value.Trim() : value;
    }
}

public static class Arch7bCoreStaticAuthorityQualifier
{
    private sealed record SourceSpec(string Authority, string Path, string Role, string[] Tokens);

    private static IReadOnlyList<SourceSpec> SourceSpecs { get; } =
    [
        new("PORTAL", "tools/lmax_portal_reports_downloader/src/downloader.mjs", "EXECUTABLE", ["export"]),
        new("PORTAL", "tools/lmax_portal_reports_downloader/src/policy.mjs", "PARSER", ["export"]),
        new("PORTAL", "tools/lmax_portal_reports_downloader/src/automated-bootstrap.mjs", "PARSER", ["export"]),
        new("PORTAL", "tools/lmax_portal_reports_downloader/test/automated-bootstrap.test.mjs", "TEST", ["test"]),
        new("RDS", "tools/lmax_portal_reports_downloader/src/rds-secret-client.mjs", "EXECUTABLE", ["RDS_SECRET_DEADLINE_MS"]),
        new("RDS", "tools/lmax_portal_reports_downloader/src/rds-secret-lease.mjs", "PARSER", ["export"]),
        new("RDS", "tools/lmax_portal_reports_downloader/src/arch7b-operational-orchestrator.mjs", "PARSER", ["export"]),
        new("RDS", "tools/lmax_portal_reports_downloader/src/arch7b-operational-orchestrator-cli.mjs", "EXECUTABLE", ["process.argv"]),
        new("RDS", "tools/lmax_portal_reports_downloader/test-fast-seal/rds-secret-client.test.mjs", "TEST", ["test"]),
        new("BRACKET", "tools/lmax_portal_reports_downloader/src/bracketed-snapshot.mjs", "EXECUTABLE", ["export"]),
        new("BRACKET", "tools/lmax_portal_reports_downloader/src/bracket-fast-seal.mjs", "PARSER", ["export"]),
        new("BRACKET", "tools/lmax_portal_reports_downloader/src/fast-seal-cli.mjs", "PARSER", ["process.argv"]),
        new("BRACKET", "tools/lmax_portal_reports_downloader/test/bracketed-snapshot.test.mjs", "TEST", ["test"]),
        new("BRACKET", "tools/lmax_portal_reports_downloader/test-fast-seal/fast-seal.test.mjs", "TEST", ["test"]),
        new("CROSS_BINDING", "tools/lmax_portal_reports_downloader/package.json", "MANIFEST", ["qualify-arm-import-operational-orchestrator", "run-bracket-fast-seal-and-hand-off"]),
        new("CROSS_BINDING", "tools/lmax_portal_reports_downloader/src/core-runtime-prequalification.mjs", "PARSER", ["PREQUALIFICATION_MAX_AGE_SECONDS"])
    ];

    public static async Task<Arch7bCoreStaticAuthorityBinding> QualifyAsync(IArch7bCoreRepositoryReader reader,
        CancellationToken cancellationToken = default)
    {
        var head = await reader.HeadAsync(cancellationToken).ConfigureAwait(false);
        var tree = await reader.TreeAsync(cancellationToken).ConfigureAwait(false);
        if (head != Arch7bOneShotContracts.CoreCommit)
            throw new InvalidDataException($"CORE_HEAD_MISMATCH:{head}");
        if (tree != Arch7bOneShotContracts.CoreTree)
            throw new InvalidDataException($"CORE_TREE_MISMATCH:{tree}");

        var sources = new List<Arch7bCoreSourceAuthority>();
        foreach (var spec in SourceSpecs)
        {
            var text = await reader.ReadTextAsync(head, spec.Path, cancellationToken).ConfigureAwait(false);
            if (spec.Tokens.Any(token => !text.Contains(token, StringComparison.Ordinal)))
                throw new Arch7bQualificationException(Arch7bBlockers.CoreParserAuthorityMissing, spec.Path);
            var sha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
            sources.Add(new(spec.Authority, spec.Path, sha, spec.Role, spec.Tokens));
        }

        var commands = new[]
        {
            Command("PORTAL", "node", "tools/lmax_portal_reports_downloader/src/downloader.mjs",
                ["--qualification-only", "true", "--environment", "test", "--output-root", "<RUN_ROOT>"],
                ["<RUN_ROOT>"], sources),
            Command("RDS", "node", "tools/lmax_portal_reports_downloader/src/arch7b-operational-orchestrator-cli.mjs",
                ["qualify-arm-import-operational-orchestrator", "--config", "<RDS_CONFIG>"],
                ["<RDS_CONFIG>"], sources),
            Command("BRACKET", "node", "tools/lmax_portal_reports_downloader/src/fast-seal-cli.mjs",
                ["run-bracket-fast-seal-and-hand-off", "--config", "<BRACKET_CONFIG>"],
                ["<BRACKET_CONFIG>"], sources)
        };
        foreach (var command in commands)
        {
            if (!command.Sources.Any(value => value.Role == "PARSER") ||
                !command.Sources.Any(value => value.Role == "TEST"))
                throw new Arch7bQualificationException(Arch7bBlockers.CoreParserAuthorityMissing, command.Authority);
            ValidateDeclaredPlaceholders(command.StaticArguments, command.DeclaredLivePlaceholders);
        }
        var canonical = string.Join('\n', commands.Select(command => command.EvidenceSha256));
        return new(Arch7bOneShotContracts.CoreStaticCommandAuthorityBindingVersion, head, tree,
            Arch7bOneShotContracts.CoreRepositoryAuthoritySha256, commands, 0, 0, 0,
            Arch7bOneShotContracts.Sha256(canonical));
    }

    public static void ValidateDeclaredPlaceholders(IEnumerable<string> arguments,
        IReadOnlyCollection<string> declaredPlaceholders)
    {
        var placeholders = arguments.Where(value => value.StartsWith('<') && value.EndsWith('>')).ToArray();
        if (placeholders.Any(value => !declaredPlaceholders.Contains(value, StringComparer.Ordinal)) ||
            declaredPlaceholders.Any(value => !placeholders.Contains(value, StringComparer.Ordinal)))
            throw new Arch7bQualificationException(Arch7bBlockers.CorePlaceholderUnresolved);
    }

    public static void ValidateExecutableSha(string expected, string actual)
    {
        if (!Arch7bOneShotContracts.IsSha256(expected) || !string.Equals(expected, actual, StringComparison.Ordinal))
            throw new Arch7bQualificationException(Arch7bBlockers.ExecutableShaMismatch);
    }

    private static Arch7bCoreCommandAuthority Command(string authority, string runtime, string entrypoint,
        IReadOnlyList<string> arguments, IReadOnlyList<string> placeholders,
        IReadOnlyList<Arch7bCoreSourceAuthority> sources)
    {
        var selected = sources.Where(value => value.Authority == authority || value.Authority == "CROSS_BINDING").ToArray();
        var executable = selected.Single(value => value.SourceFile == entrypoint);
        var evidence = Arch7bOneShotContracts.Sha256(string.Join('\n', authority, runtime, entrypoint,
            executable.SourceFileSha256, string.Join('|', arguments), string.Join('|', selected.Select(value => value.SourceFileSha256))));
        return new(authority, runtime, entrypoint, executable.SourceFileSha256, arguments, placeholders, true,
            selected, evidence);
    }
}
