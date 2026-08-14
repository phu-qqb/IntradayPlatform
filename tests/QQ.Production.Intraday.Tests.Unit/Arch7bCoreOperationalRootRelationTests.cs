using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bCoreOperationalRootRelationTests : IDisposable
{
    private readonly List<string> roots = [];

    [Fact]
    public void Combined_git_and_node_root_is_accepted_deterministically()
    {
        var fixture = CreateFixture();

        var first = Arch7bOperationalExecutionAuthorityValidator
            .ValidateCoreOperationalRootRelation(
                fixture.Repository, fixture.NodeRuntime);
        var second = Arch7bOperationalExecutionAuthorityValidator
            .ValidateCoreOperationalRootRelation(
                fixture.Repository, fixture.NodeRuntime);

        Assert.Equal(Arch7bV2Contracts.CoreOperationalRootRelationVersion,
            first.ContractVersion);
        Assert.Equal(first, second);
        Assert.True(first.PlaywrightPresent);
        Assert.True(first.AwsSdkPresent);
        Assert.Equal(0, first.ReparsePointCount);
    }

    [Fact]
    public void Separate_git_and_node_roots_are_rejected()
    {
        var fixture = CreateFixture(combined: false);

        var failure = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalExecutionAuthorityValidator
                .ValidateCoreOperationalRootRelation(
                    fixture.Repository, fixture.NodeRuntime));

        Assert.Equal(Arch7bV2Blockers.CoreRuntimeRootRelationMismatch,
            failure.BlockerCode);
    }

    [Theory]
    [InlineData("playwright")]
    [InlineData("aws")]
    [InlineData("node_modules")]
    public void Missing_node_closure_is_rejected(string missing)
    {
        var fixture = CreateFixture();
        if (missing == "playwright")
            Directory.Delete(Path.Combine(fixture.NodeRuntime.Path,
                "node_modules", "playwright"), true);
        else if (missing == "aws")
            Directory.Delete(Path.Combine(fixture.NodeRuntime.Path,
                "node_modules", "@aws-sdk", "client-secrets-manager"), true);
        else
            Directory.Delete(Path.Combine(fixture.NodeRuntime.Path,
                "node_modules"), true);

        var failure = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalExecutionAuthorityValidator
                .ValidateCoreOperationalRootRelation(
                    fixture.Repository, fixture.NodeRuntime));

        Assert.Equal(Arch7bV2Blockers.CoreRuntimeNodeClosureMissing,
            failure.BlockerCode);
    }

    [Fact]
    public void Static_gate_rejects_missing_closure_before_calendar_or_slot()
    {
        var fixture = CreateFixture();
        Directory.Delete(Path.Combine(fixture.NodeRuntime.Path,
            "node_modules"), true);
        var references = new[]
        {
            Reference("core_repository",
                Arch7bOperationalAuthorityKind.GitRepository),
            Reference("core_node_runtime",
                Arch7bOperationalAuthorityKind.NodePackageRuntime)
        };
        var inventory = Inventory(references);
        var manifest = Manifest(inventory, fixture.Repository,
            fixture.NodeRuntime);
        var budget = new Arch7bOneShotBudget();

        var failure = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalExecutionAuthorityValidator.ValidateStatic(
                inventory, manifest));

        Assert.Equal(Arch7bV2Blockers.CoreRuntimeNodeClosureMissing,
            failure.BlockerCode);
        Assert.True(Arch7bStages.IndexOf("STATIC_AUTHORITY_VALIDATION") <
                    Arch7bStages.IndexOf("CALENDAR_LOADED"));
        Assert.True(Arch7bStages.IndexOf("CALENDAR_LOADED") <
                    Arch7bStages.IndexOf("SLOT_SELECTED"));
        Assert.Equal(0, budget.Slots);
        Assert.Equal(0, budget.Captures);
        Assert.Equal(0, budget.RdsReads);
        Assert.Equal(0, budget.Retries);
        Assert.Equal(0, Arch7bNoLiveSafetyCounters.Zero.LiveSlots);
    }

    [Fact]
    public void Package_lock_mismatch_is_rejected()
    {
        var fixture = CreateFixture();
        var nodeRuntime = Seal(fixture.NodeRuntime with
        {
            PackageLockSha256 = new string('0', 64)
        });

        var failure = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalExecutionAuthorityValidator
                .ValidateCoreOperationalRootRelation(
                    fixture.Repository, nodeRuntime));

        Assert.Equal(Arch7bV2Blockers.CoreRuntimeNodeClosureMismatch,
            failure.BlockerCode);
    }

    [Fact]
    public void Reparse_point_inside_node_closure_is_rejected_when_supported()
    {
        var fixture = CreateFixture();
        var target = Root("reparse-target");
        Directory.CreateDirectory(target);
        var link = Path.Combine(fixture.NodeRuntime.Path, "node_modules",
            "linked-runtime");
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception unsupported) when (unsupported is UnauthorizedAccessException or
                                        IOException or PlatformNotSupportedException)
        {
            return;
        }

        var failure = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bOperationalExecutionAuthorityValidator
                .ValidateCoreOperationalRootRelation(
                    fixture.Repository, fixture.NodeRuntime));

        Assert.Equal(Arch7bV2Blockers.CoreRuntimeNodeClosureMismatch,
            failure.BlockerCode);
    }

    private Fixture CreateFixture(bool combined = true)
    {
        var repositoryRoot = Root("repository");
        Directory.CreateDirectory(repositoryRoot);
        var packageRoot = combined
            ? Path.Combine(repositoryRoot, "tools",
                "lmax_portal_reports_downloader")
            : Root("separate-node-runtime");
        Directory.CreateDirectory(packageRoot);
        var packageJson = Path.Combine(packageRoot, "package.json");
        var packageLock = Path.Combine(packageRoot, "package-lock.json");
        File.WriteAllText(packageJson,
            "{\"name\":\"arch7b-core-runtime\",\"version\":\"1.0.0\"}");
        File.WriteAllText(packageLock,
            "{\"name\":\"arch7b-core-runtime\",\"version\":\"1.0.0\"," +
            "\"lockfileVersion\":3}");
        Directory.CreateDirectory(Path.Combine(packageRoot, "node_modules",
            "playwright"));
        Directory.CreateDirectory(Path.Combine(packageRoot, "node_modules",
            "@aws-sdk", "client-secrets-manager"));

        var closure = Arch7bOperationalExecutionAuthorityValidator
            .DirectoryInventory("core_node_runtime-node-runtime", packageRoot);
        var repositoryInventory = Arch7bOperationalExecutionAuthorityValidator
            .DirectoryInventory("core_repository", repositoryRoot);
        var repository = Seal(Authority("core_repository",
            Arch7bOperationalAuthorityKind.GitRepository, repositoryRoot) with
        {
            DirectoryInventorySha256 = repositoryInventory.EvidenceSha256
        });
        var nodeRuntime = Seal(Authority("core_node_runtime",
            Arch7bOperationalAuthorityKind.NodePackageRuntime, packageRoot) with
        {
            DirectoryInventorySha256 = closure.EvidenceSha256,
            PackageJsonSha256 = Arch7bOperationalExecutionAuthorityValidator
                .FileSha(packageJson),
            PackageLockSha256 = Arch7bOperationalExecutionAuthorityValidator
                .FileSha(packageLock),
            RuntimeClosureSha256 = closure.EvidenceSha256
        });
        return new(repository, nodeRuntime);
    }

    private static Arch7bOperationalExecutionAuthority Authority(string id,
        Arch7bOperationalAuthorityKind kind, string path) => new(
        Arch7bV2Contracts.OperationalExecutionAuthorityEntryVersion, id, kind,
        Path.GetFullPath(path), null, null, null, null, null, null, null,
        null, null, null, null, true, false, "test", string.Empty);

    private static Arch7bOperationalAuthorityReference Reference(string id,
        Arch7bOperationalAuthorityKind kind)
    {
        var provisional = new Arch7bOperationalAuthorityReference(
            Arch7bV2Contracts.OperationalExecutionAuthorityReferenceVersion,
            id, Arch7bOperationalAuthorityReferenceKind.StaticPreSpawn,
            "STATIC_AUTHORITY_VALIDATION", "test", "authority", kind,
            true, true, false, string.Empty);
        return provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(
                provisional.Canonical())
        };
    }

    private static Arch7bRequiredOperationalExecutionAuthorityInventory Inventory(
        IReadOnlyList<Arch7bOperationalAuthorityReference> references)
    {
        var provisional = new Arch7bRequiredOperationalExecutionAuthorityInventory(
            Arch7bV2Contracts.OperationalExecutionAuthorityInventoryVersion,
            Arch7bStages.All.Count,
            Arch7bFinalStageExecutionCatalog.CommandTemplateCount,
            references.Count, references.Count, 0, 0, 0, references,
            string.Empty);
        return provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(
                provisional.Canonical())
        };
    }

    private static Arch7bOperationalExecutionAuthorityManifest Manifest(
        Arch7bRequiredOperationalExecutionAuthorityInventory inventory,
        params Arch7bOperationalExecutionAuthority[] authorities)
    {
        var provisional = new Arch7bOperationalExecutionAuthorityManifest(
            Arch7bV2Contracts.OperationalExecutionAuthorityManifestVersion,
            new string('a', 64), inventory.EvidenceSha256, authorities.Length,
            authorities, string.Empty);
        return provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(
                provisional.Canonical())
        };
    }

    private static Arch7bOperationalExecutionAuthority Seal(
        Arch7bOperationalExecutionAuthority authority) => authority with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(
                authority.Canonical())
        };

    private string Root(string suffix)
    {
        var path = Path.Combine(Path.GetTempPath(),
            "qq-arch7b-core-operational-root-tests",
            suffix + "-" + Guid.NewGuid().ToString("N"));
        roots.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var root in roots.OrderByDescending(value => value.Length))
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed record Fixture(
        Arch7bOperationalExecutionAuthority Repository,
        Arch7bOperationalExecutionAuthority NodeRuntime);
}
