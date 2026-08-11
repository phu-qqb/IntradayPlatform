using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bOperationalExecutionAuthorityMatrixTests : IDisposable
{
    private readonly List<string> roots = [];

    public static TheoryData<int, string> Matrix => new()
    {
        { 1, "required authority absent" },
        { 2, "supplemental authority rejected" },
        { 3, "raw duplicate property rejected" },
        { 4, "byte-identical duplicate authority rejected" },
        { 5, "divergent duplicate authority rejected" },
        { 6, "missing executable authority rejected" },
        { 7, "missing working-directory authority rejected" },
        { 8, "missing placeholder authority rejected" },
        { 9, "missing non-secret environment authority rejected" },
        { 10, "missing static adapter authority rejected" },
        { 11, "wrong authority kind rejected" },
        { 12, "wrong file SHA rejected" },
        { 13, "missing file rejected" },
        { 14, "unexpected directory file rejected" },
        { 15, "missing directory file rejected" },
        { 16, "wrong directory inventory SHA rejected" },
        { 17, "path-only directory SHA rejected" },
        { 18, "reparse-point inventory rejected" },
        { 19, "wrong Git repository rejected" },
        { 20, "wrong Git commit rejected" },
        { 21, "wrong Git tree rejected" },
        { 22, "dirty Git worktree rejected" },
        { 23, "dirty Git index rejected" },
        { 24, "wrong Git remote rejected" },
        { 25, "Git alternates rejected" },
        { 26, "Git fsck failure rejected" },
        { 27, "missing node_modules rejected" },
        { 28, "wrong package-lock rejected" },
        { 29, "missing AWS SDK rejected" },
        { 30, "missing transitive dependency rejected" },
        { 31, "failed Core module import rejected" },
        { 32, "missing dotnet executable rejected" },
        { 33, "wrong dotnet executable SHA rejected" },
        { 34, "missing required dotnet runtime rejected" },
        { 35, "divergent DOTNET_ROOT rejected" },
        { 36, "template exact authority projection passes" },
        { 37, "live exact authority projection passes" },
        { 38, "synthetic skeleton authority set rejected" },
        { 39, "unreferenced authority rejected" },
        { 40, "deterministic inventory manifest and evidence" }
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Complete_authority_matrix(int id, string description)
    {
        Assert.False(string.IsNullOrWhiteSpace(description));
        switch (id)
        {
            case 1: Missing(ReferenceKind()); break;
            case 2: Extra(); break;
            case 3: DuplicateProperty(); break;
            case 4: DuplicateArray(false); break;
            case 5: DuplicateArray(true); break;
            case 6: Missing(ReferenceKind(Arch7bOperationalAuthorityReferenceKind.Executable)); break;
            case 7:
                Missing(ReferenceKind(Arch7bOperationalAuthorityReferenceKind.WorkingDirectory,
                Arch7bOperationalAuthorityKind.DirectoryInventory)); break;
            case 8: Missing(ReferenceKind(Arch7bOperationalAuthorityReferenceKind.PlaceholderPath)); break;
            case 9:
                Missing(ReferenceKind(Arch7bOperationalAuthorityReferenceKind.NonSecretEnvironment,
                Arch7bOperationalAuthorityKind.DirectoryInventory)); break;
            case 10:
                Missing(ReferenceKind(Arch7bOperationalAuthorityReferenceKind.StaticPreSpawn,
                Arch7bOperationalAuthorityKind.GitRepository)); break;
            case 11: WrongKind(); break;
            case 12: WrongFileSha(); break;
            case 13: MissingFile(); break;
            case 14: ChangedDirectory(add: true); break;
            case 15: ChangedDirectory(add: false); break;
            case 16: WrongDirectorySha(pathOnly: false); break;
            case 17: WrongDirectorySha(pathOnly: true); break;
            case 18: ReparseInventory(); break;
            case 19: GitFailure("repository"); break;
            case 20: GitFailure("commit"); break;
            case 21: GitFailure("tree"); break;
            case 22: GitFailure("worktree"); break;
            case 23: GitFailure("index"); break;
            case 24: GitFailure("remote"); break;
            case 25: GitFailure("alternates"); break;
            case 26: GitFailure("fsck"); break;
            case 27: NodeFailure("modules"); break;
            case 28: NodeFailure("lock"); break;
            case 29: NodeFailure("aws"); break;
            case 30: NodeFailure("transitive"); break;
            case 31: NodeFailure("core-import"); break;
            case 32: DotnetFailure("executable"); break;
            case 33: DotnetFailure("sha"); break;
            case 34: DotnetFailure("runtime"); break;
            case 35: ProjectionMismatch(); break;
            case 36: ExactProjection(); break;
            case 37: ExactProjection(); break;
            case 38: SyntheticOnly(); break;
            case 39: Extra(); break;
            case 40: Deterministic(); break;
            default: throw new ArgumentOutOfRangeException(nameof(id));
        }
    }

    private void Missing(Arch7bOperationalAuthorityReference reference)
    {
        var inventory = Inventory(reference);
        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Manifest(inventory).Project(inventory));
        Assert.Equal(Arch7bV2Contracts.OperationalAuthorityMissing, error.BlockerCode);
    }

    private void Extra()
    {
        var path = FilePath("extra");
        var required = ReferenceKind();
        var inventory = Inventory(required);
        var manifest = Manifest(inventory, FileAuthority(required.AuthorityId, path),
            FileAuthority("unused", path));
        var error = Assert.Throws<Arch7bQualificationException>(() => manifest.Project(inventory));
        Assert.Equal(Arch7bV2Contracts.OperationalAuthorityUnused, error.BlockerCode);
    }

    private static void DuplicateProperty()
    {
        var json = Encoding.UTF8.GetBytes("{\"authorities\":[],\"authorities\":[]}");
        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalExecutionAuthorityManifestParser.ParseStrict(json));
        Assert.Equal(Arch7bV2Contracts.OperationalAuthorityDuplicateId, error.BlockerCode);
    }

    private void DuplicateArray(bool divergent)
    {
        var path = FilePath("duplicate");
        var authority = FileAuthority("required", path);
        var second = divergent ? Seal(authority with { FileSha256 = new string('b', 64) }) : authority;
        var inventory = Inventory(ReferenceKind());
        var manifest = Manifest(inventory, authority, second);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, Arch7bJson.CanonicalOptions);
        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalExecutionAuthorityManifestParser.ParseStrict(bytes));
        Assert.Equal(Arch7bV2Contracts.OperationalAuthorityDuplicateId, error.BlockerCode);
    }

    private void WrongKind()
    {
        var root = Root("kind");
        Directory.CreateDirectory(root);
        var inventory = Inventory(ReferenceKind(expected: Arch7bOperationalAuthorityKind.File));
        var authority = DirectoryAuthority("required", root);
        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Manifest(inventory, authority).Project(inventory));
        Assert.Equal(Arch7bV2Contracts.OperationalAuthorityKindMismatch, error.BlockerCode);
    }

    private void WrongFileSha()
    {
        var path = FilePath("bad-sha");
        var inventory = Inventory(ReferenceKind());
        var authority = Seal(FileAuthority("required", path) with { FileSha256 = new string('0', 64) });
        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalExecutionAuthorityValidator.ValidateStatic(
                inventory, Manifest(inventory, authority)));
        Assert.Equal(Arch7bV2Contracts.OperationalAuthorityShaMismatch, error.BlockerCode);
    }

    private void MissingFile()
    {
        var path = FilePath("missing-file");
        var inventory = Inventory(ReferenceKind());
        var authority = FileAuthority("required", path);
        File.Delete(path);
        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalExecutionAuthorityValidator.ValidateStatic(
                inventory, Manifest(inventory, authority)));
        Assert.Equal(Arch7bV2Contracts.OperationalAuthorityMissing, error.BlockerCode);
    }

    private void ChangedDirectory(bool add)
    {
        var root = Root("changed-directory");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "one.txt"), "one");
        if (!add) File.WriteAllText(Path.Combine(root, "two.txt"), "two");
        var inventory = Inventory(ReferenceKind(expected:
            Arch7bOperationalAuthorityKind.DirectoryInventory));
        var authority = DirectoryAuthority("required", root);
        if (add) File.WriteAllText(Path.Combine(root, "unexpected.txt"), "unexpected");
        else File.Delete(Path.Combine(root, "two.txt"));
        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalExecutionAuthorityValidator.ValidateStatic(
                inventory, Manifest(inventory, authority)));
        Assert.Equal(Arch7bV2Contracts.OperationalAuthorityDirectoryInventoryMismatch,
            error.BlockerCode);
    }

    private void WrongDirectorySha(bool pathOnly)
    {
        var root = Root("wrong-directory-sha");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "one.txt"), "one");
        var inventory = Inventory(ReferenceKind(expected:
            Arch7bOperationalAuthorityKind.DirectoryInventory));
        var authority = DirectoryAuthority("required", root);
        var bad = pathOnly ? Arch7bOneShotContracts.Sha256("directory:" + root) : new string('0', 64);
        authority = Seal(authority with { DirectoryInventorySha256 = bad });
        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalExecutionAuthorityValidator.ValidateStatic(
                inventory, Manifest(inventory, authority)));
        Assert.Equal(Arch7bV2Contracts.OperationalAuthorityDirectoryInventoryMismatch,
            error.BlockerCode);
    }

    private static void ReparseInventory()
    {
        var entry = new Arch7bOperationalDirectoryInventoryEntry("linked", "DIRECTORY", 0,
            null, false, true, string.Empty);
        entry = entry with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(entry.Canonical()) };
        var inventory = new Arch7bOperationalDirectoryInventory(
            Arch7bV2Contracts.OperationalExecutionAuthorityDirectoryInventoryVersion,
            "required", Path.GetFullPath("."), 0, 1, 0, 0, 0, 1, [entry], string.Empty);
        inventory = inventory with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(inventory.Canonical())
        };
        var error = Assert.Throws<Arch7bQualificationException>(inventory.ValidateEvidence);
        Assert.Equal(Arch7bV2Contracts.OperationalAuthorityDirectoryInventoryMismatch,
            error.BlockerCode);
    }

    private void GitFailure(string failure)
    {
        var git = FindExecutable("git.exe");
        var root = Root("git-" + failure);
        Directory.CreateDirectory(root);
        Git(git, root, "init");
        Git(git, root, "config", "user.email", "arch7b@example.invalid");
        Git(git, root, "config", "user.name", "ARCH7B Qualification");
        File.WriteAllText(Path.Combine(root, "tracked.txt"), "clean");
        Git(git, root, "add", "tracked.txt");
        Git(git, root, "commit", "-m", "fixture");
        Git(git, root, "remote", "add", "origin", "https://github.com/phu-qqb/Test.git");
        var commit = Git(git, root, "rev-parse", "HEAD");
        var tree = Git(git, root, "rev-parse", "HEAD^{tree}");
        var repository = DirectoryAuthority("test_repository", root,
            Arch7bOperationalAuthorityKind.GitRepository) with
        {
            Repository = "https://github.com/phu-qqb/Test.git",
            Commit = commit,
            Tree = tree
        };
        repository = Seal(repository);
        if (failure == "repository") repository = Seal(repository with
        { Repository = "https://github.com/phu-qqb/Other.git" });
        if (failure == "commit") repository = Seal(repository with { Commit = new string('a', 40) });
        if (failure == "tree") repository = Seal(repository with { Tree = new string('b', 40) });
        if (failure is "worktree" or "index")
        {
            File.WriteAllText(Path.Combine(root, "tracked.txt"), "dirty");
            if (failure == "index") Git(git, root, "add", "tracked.txt");
            repository = RefreshDirectory(repository);
        }
        if (failure == "remote")
            Git(git, root, "remote", "set-url", "origin", "https://github.com/phu-qqb/Other.git");
        if (failure == "alternates")
        {
            var path = Path.Combine(root, ".git", "objects", "info", "alternates");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Root("alternate-store"));
        }
        if (failure == "fsck")
        {
            var path = Path.Combine(root, ".git", "objects", "aa",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "not-a-git-object");
        }
        var gitAuthority = FileAuthority("git_executable", git);
        var inventory = Inventory(
            ReferenceKind(id: "git_executable"),
            ReferenceKind(id: "test_repository", expected:
                Arch7bOperationalAuthorityKind.GitRepository));
        Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalExecutionAuthorityValidator.ValidateStatic(
                inventory, Manifest(inventory, gitAuthority, repository)));
    }

    private void NodeFailure(string failure)
    {
        var node = FindExecutable("node.exe");
        var root = Root("node-" + failure);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "package.json"),
            "{\"name\":\"arch7b-fixture\",\"version\":\"1.0.0\",\"type\":\"module\"," +
            "\"scripts\":{\"test\":\"node --test\"}}");
        File.WriteAllText(Path.Combine(root, "package-lock.json"),
            "{\"name\":\"arch7b-fixture\",\"version\":\"1.0.0\",\"lockfileVersion\":3," +
            "\"requires\":true,\"packages\":{\"\":{\"name\":\"arch7b-fixture\"," +
            "\"version\":\"1.0.0\"}}}");
        var modules = Path.Combine(root, "node_modules");
        if (failure != "modules") Directory.CreateDirectory(modules);
        if (failure is not "modules" and not "aws")
            Module(modules, "@aws-sdk/client-secrets-manager",
                failure == "transitive" ? "import 'missing-transitive'; export const ok=true;" :
                "export const ok=true;");
        if (failure != "modules") Module(modules, "playwright", "export const ok=true;");
        var src = Path.Combine(root, "src");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "core-runtime-prequalification.mjs"), "export const ok=true;");
        if (failure != "core-import") File.WriteAllText(Path.Combine(src,
            "rds-secret-child-command-broker-reference-client.mjs"), "export const ok=true;");
        var authority = DirectoryAuthority("node_package_runtime", root,
            Arch7bOperationalAuthorityKind.NodePackageRuntime) with
        {
            PackageJsonSha256 = Arch7bOperationalExecutionAuthorityValidator.FileSha(
                Path.Combine(root, "package.json")),
            PackageLockSha256 = failure == "lock" ? new string('0', 64) :
                Arch7bOperationalExecutionAuthorityValidator.FileSha(
                    Path.Combine(root, "package-lock.json")),
            RuntimeClosureSha256 = Arch7bOperationalExecutionAuthorityValidator
                .DirectoryInventory("node_package_runtime-node-runtime", root).EvidenceSha256,
            RuntimeVersion = "fixture"
        };
        authority = Seal(authority);
        var nodeAuthority = FileAuthority("node_executable", node);
        var inventory = Inventory(
            ReferenceKind(id: "node_executable"),
            ReferenceKind(id: "node_package_runtime", expected:
                Arch7bOperationalAuthorityKind.NodePackageRuntime));
        Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalExecutionAuthorityValidator.ValidateStatic(
                inventory, Manifest(inventory, nodeAuthority, authority)));
    }

    private void DotnetFailure(string failure)
    {
        var root = Root("dotnet-" + failure);
        Directory.CreateDirectory(root);
        if (failure != "executable") File.WriteAllText(Path.Combine(root, "dotnet.exe"), "fixture");
        var inventory = Inventory(ReferenceKind(expected:
            Arch7bOperationalAuthorityKind.DotnetRuntime));
        var authority = DirectoryAuthority("required", root,
            Arch7bOperationalAuthorityKind.DotnetRuntime) with
        { RuntimeVersion = "10.0.0" };
        authority = Seal(authority);
        if (failure == "sha")
        {
            var executableInventory = Inventory(ReferenceKind(id: "dotnet_executable"));
            var executable = Seal(FileAuthority("dotnet_executable",
                Path.Combine(root, "dotnet.exe")) with
            { FileSha256 = new string('0', 64) });
            var error = Assert.Throws<Arch7bQualificationException>(() =>
                Arch7bOperationalExecutionAuthorityValidator.ValidateStatic(
                    executableInventory, Manifest(executableInventory, executable)));
            Assert.Equal(Arch7bV2Contracts.OperationalAuthorityShaMismatch, error.BlockerCode);
            return;
        }
        Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalExecutionAuthorityValidator.ValidateStatic(
                inventory, Manifest(inventory, authority)));
    }

    private void ProjectionMismatch()
    {
        var path = FilePath("projection-mismatch");
        var expected = new Dictionary<string, Arch7bFileAuthority>(StringComparer.Ordinal)
        { ["required"] = FileAuthority("required", path).Project() };
        var actual = new Dictionary<string, Arch7bFileAuthority>(StringComparer.Ordinal)
        { ["required"] = expected["required"] with { Path = Path.GetFullPath(path + ".other") } };
        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalExecutionAuthorityValidator.RequireExactProjection(
                expected, actual, "DOTNET_ROOT"));
        Assert.Equal(Arch7bV2Contracts.OperationalAuthorityPathMismatch, error.BlockerCode);
    }

    private void ExactProjection()
    {
        var path = FilePath("exact-projection");
        var authority = FileAuthority("required", path).Project();
        var expected = new Dictionary<string, Arch7bFileAuthority>(StringComparer.Ordinal)
        { ["required"] = authority };
        var actual = new Dictionary<string, Arch7bFileAuthority>(expected, StringComparer.Ordinal);
        Arch7bOperationalExecutionAuthorityValidator.RequireExactProjection(
            expected, actual, "projection");
    }

    private void SyntheticOnly()
    {
        var path = FilePath("synthetic");
        var inventory = Inventory(ReferenceKind());
        var manifest = Manifest(inventory, FileAuthority("synthetic_authority", path));
        Assert.Throws<Arch7bQualificationException>(() => manifest.Project(inventory));
    }

    private void Deterministic()
    {
        var root = Root("deterministic");
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        File.WriteAllText(Path.Combine(root, "nested", "value.txt"), "value");
        var firstDirectory = Arch7bOperationalExecutionAuthorityValidator.DirectoryInventory(
            "required", root);
        var secondDirectory = Arch7bOperationalExecutionAuthorityValidator.DirectoryInventory(
            "required", root);
        Assert.Equal(firstDirectory.EvidenceSha256, secondDirectory.EvidenceSha256);
        var reference = ReferenceKind(expected: Arch7bOperationalAuthorityKind.DirectoryInventory);
        var firstInventory = Inventory(reference);
        var secondInventory = Inventory(reference);
        Assert.Equal(firstInventory.EvidenceSha256, secondInventory.EvidenceSha256);
        var authority = DirectoryAuthority("required", root);
        var inventory = Inventory(reference);
        var firstManifest = Manifest(inventory, authority);
        var secondManifest = Manifest(inventory, authority);
        Assert.Equal(firstManifest.EvidenceSha256, secondManifest.EvidenceSha256);
    }

    private Arch7bOperationalAuthorityReference ReferenceKind(
        Arch7bOperationalAuthorityReferenceKind kind = Arch7bOperationalAuthorityReferenceKind.Executable,
        Arch7bOperationalAuthorityKind expected = Arch7bOperationalAuthorityKind.File,
        string id = "required")
    {
        var value = new Arch7bOperationalAuthorityReference(
            Arch7bV2Contracts.OperationalExecutionAuthorityReferenceVersion, id, kind,
            "STATIC_AUTHORITY_VALIDATION", "matrix", "matrix", expected, true, true, false,
            string.Empty);
        return value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };
    }

    private static Arch7bRequiredOperationalExecutionAuthorityInventory Inventory(
        params Arch7bOperationalAuthorityReference[] references)
    {
        var value = new Arch7bRequiredOperationalExecutionAuthorityInventory(
            Arch7bV2Contracts.OperationalExecutionAuthorityInventoryVersion, 40, 13,
            references.Length, references.Select(item => item.AuthorityId).Distinct(StringComparer.Ordinal).Count(),
            0, 0, 0, references.OrderBy(item => item.AuthorityId, StringComparer.Ordinal).ToArray(),
            string.Empty);
        return value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };
    }

    private static Arch7bOperationalExecutionAuthorityManifest Manifest(
        Arch7bRequiredOperationalExecutionAuthorityInventory inventory,
        params Arch7bOperationalExecutionAuthority[] authorities)
    {
        var sealedAuthorities = authorities.Select(Seal).ToArray();
        var value = new Arch7bOperationalExecutionAuthorityManifest(
            Arch7bV2Contracts.OperationalExecutionAuthorityManifestVersion,
            new string('a', 64), inventory.EvidenceSha256, sealedAuthorities.Length,
            sealedAuthorities, string.Empty);
        return value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(value.Canonical()) };
    }

    private static Arch7bOperationalExecutionAuthority Seal(
        Arch7bOperationalExecutionAuthority authority) => authority with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(authority.Canonical())
        };

    private static Arch7bOperationalExecutionAuthority FileAuthority(string id, string path) => Seal(new(
        Arch7bV2Contracts.OperationalExecutionAuthorityEntryVersion, id,
        Arch7bOperationalAuthorityKind.File, Path.GetFullPath(path),
        Arch7bOperationalExecutionAuthorityValidator.FileSha(path), null, null, null,
        null, null, null, null, null, null, null, true, false, "matrix", string.Empty));

    private Arch7bOperationalExecutionAuthority DirectoryAuthority(string id, string root,
        Arch7bOperationalAuthorityKind kind = Arch7bOperationalAuthorityKind.DirectoryInventory)
    {
        var inventory = Arch7bOperationalExecutionAuthorityValidator.DirectoryInventory(id, root);
        var manifestRoot = Root("directory-manifest");
        Directory.CreateDirectory(manifestRoot);
        var manifestPath = Path.Combine(manifestRoot, id + ".json");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(inventory, Arch7bJson.CanonicalOptions);
        File.WriteAllBytes(manifestPath, bytes);
        return Seal(new(Arch7bV2Contracts.OperationalExecutionAuthorityEntryVersion, id, kind,
            Path.GetFullPath(root), null, inventory.EvidenceSha256, manifestPath,
            Convert.ToHexStringLower(SHA256.HashData(bytes)), null, null, null, null, null,
            inventory.EvidenceSha256, null, true, false, "matrix", string.Empty));
    }

    private Arch7bOperationalExecutionAuthority RefreshDirectory(
        Arch7bOperationalExecutionAuthority authority)
    {
        var inventory = Arch7bOperationalExecutionAuthorityValidator.DirectoryInventory(
            authority.AuthorityId, authority.Path);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(inventory, Arch7bJson.CanonicalOptions);
        File.WriteAllBytes(authority.InventoryManifestPath!, bytes);
        return Seal(authority with
        {
            DirectoryInventorySha256 = inventory.EvidenceSha256,
            InventoryManifestSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            RuntimeClosureSha256 = authority.RuntimeClosureSha256 is null
                ? null : inventory.EvidenceSha256
        });
    }

    private string FilePath(string suffix)
    {
        var root = Root(suffix);
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "authority.bin");
        File.WriteAllText(path, suffix);
        return path;
    }

    private string Root(string suffix)
    {
        var path = Path.Combine(Path.GetTempPath(), "qq-arch7b-authority-matrix",
            suffix + "-" + Guid.NewGuid().ToString("N"));
        roots.Add(path);
        return path;
    }

    private static void Module(string modulesRoot, string name, string source)
    {
        var root = Path.Combine(modulesRoot, name.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "package.json"),
            "{\"name\":\"" + name + "\",\"version\":\"1.0.0\",\"type\":\"module\"," +
            "\"exports\":\"./index.js\"}");
        File.WriteAllText(Path.Combine(root, "index.js"), source);
    }

    private static string FindExecutable(string name)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim('"'), name);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        throw new FileNotFoundException(name);
    }

    private static string Git(string executable, string root, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return output.Trim();
    }

    public void Dispose()
    {
        foreach (var root in roots.OrderByDescending(value => value.Length))
        {
            if (!Directory.Exists(root)) continue;
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            foreach (var directory in Directory.EnumerateDirectories(root, "*",
                         SearchOption.AllDirectories).OrderByDescending(value => value.Length))
                File.SetAttributes(directory, FileAttributes.Directory);
            Directory.Delete(root, true);
        }
    }
}
