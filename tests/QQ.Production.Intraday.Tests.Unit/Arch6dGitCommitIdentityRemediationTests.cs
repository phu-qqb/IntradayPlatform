using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch6dGitCommitIdentityRemediationTests
{
    [Theory]
    [InlineData(40, "sha1", true, false)]
    [InlineData(64, "sha256", true, false)]
    [InlineData(39, "sha1", false, false)]
    [InlineData(41, "sha1", false, false)]
    [InlineData(63, "sha256", false, false)]
    [InlineData(65, "sha256", false, false)]
    [InlineData(40, "sha1", false, true)]
    [InlineData(40, null, false, false)]
    [InlineData(40, "unknown", false, false)]
    [InlineData(64, "sha1", false, false)]
    [InlineData(40, "sha256", false, false)]
    [InlineData(8, "sha1", false, false)]
    public void Git_commit_identity_requires_exact_full_hex_for_declared_object_format(
        int length,
        string? objectFormat,
        bool expected,
        bool nonHex)
    {
        var commitId = new string('a', length);
        if (nonHex) commitId = commitId[..^1] + "g";

        Assert.Equal(expected, GitCommitIdentityContract.IsValid(commitId, objectFormat));
    }

    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "sha1")]
    [InlineData(" aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "sha1")]
    [InlineData("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "sha1")]
    public void Git_commit_identity_rejects_uppercase_whitespace_and_prefixes(string commitId, string objectFormat)
        => Assert.False(GitCommitIdentityContract.IsValid(commitId, objectFormat));

    [Fact]
    public void Planner_preserves_git_commit_identity_without_rehashing()
    {
        var plan = Arch6cPostgreSqlPmsShadowStateTests.BuildPlan();
        var expected = new string('a', 40);

        Assert.All(plan.ModelRuns, run =>
        {
            Assert.Equal(expected, run.CoreMasterCommitId);
            Assert.Equal(GitCommitIdentityContract.Sha1, run.CoreMasterObjectFormat);
            Assert.Equal(40, run.CoreMasterCommitId.Length);
        });
    }

    [Fact]
    public void Artifact_hashes_remain_sha256()
    {
        var plan = Arch6cPostgreSqlPmsShadowStateTests.BuildPlan();

        Assert.Equal(64, plan.Ingestion.SourceEvidenceSha256.Length);
        Assert.Equal(64, plan.RowsetSha256.Length);
        Assert.All(plan.SourceArtifacts, artifact => Assert.Equal(64, artifact.Sha256.Length));
        Assert.All(plan.ModelRuns, run =>
        {
            Assert.Equal(64, run.PackageSha256.Length);
            Assert.Equal(64, run.EngineSha256.Length);
            Assert.Equal(64, run.OutputSha256.Length);
        });
    }

    [Theory]
    [InlineData("commit")]
    [InlineData("format")]
    [InlineData("package")]
    [InlineData("output")]
    public void Plan_validator_rejects_invalid_git_or_artifact_identity(string mutation)
    {
        var plan = Arch6cPostgreSqlPmsShadowStateTests.BuildPlan();
        var first = plan.ModelRuns[0];
        var changed = mutation switch
        {
            "commit" => first with { CoreMasterCommitId = new string('a', 39) },
            "format" => first with { CoreMasterObjectFormat = GitCommitIdentityContract.Sha256 },
            "package" => first with { PackageSha256 = new string('1', 63) },
            "output" => first with { OutputSha256 = new string('2', 63) },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        plan = plan with { ModelRuns = [changed, .. plan.ModelRuns.Skip(1)] };

        Assert.False(Arch6cPmsShadowPersistencePlanner.Validate(plan).IsValid);
    }

    [Fact]
    public void Registry_rejects_same_session_with_another_core_commit_id()
    {
        var registry = new InMemoryPmsShadowAtomicIngestionRegistry();
        var plan = Arch6cPostgreSqlPmsShadowStateTests.BuildPlan();
        registry.Apply(plan);
        var changed = plan.ModelRuns[0] with { CoreMasterCommitId = new string('b', 40) };
        var conflicting = plan with { ModelRuns = [changed, .. plan.ModelRuns.Skip(1)] };

        Assert.Contains("MODEL_RUN_CORE_COMMIT_ID_CONFLICT",
            Assert.Throws<InvalidDataException>(() => registry.Apply(conflicting)).Message);
    }

    [Fact]
    public void Registry_rejects_same_commit_id_with_another_object_format()
    {
        var registry = new InMemoryPmsShadowAtomicIngestionRegistry();
        var plan = Arch6cPostgreSqlPmsShadowStateTests.BuildPlan();
        registry.Apply(plan);
        var changed = plan.ModelRuns[0] with { CoreMasterObjectFormat = GitCommitIdentityContract.Sha256 };
        var conflicting = plan with { ModelRuns = [changed, .. plan.ModelRuns.Skip(1)] };

        Assert.Contains("GIT_COMMIT_IDENTITY_INVALID",
            Assert.Throws<InvalidDataException>(() => registry.Apply(conflicting)).Message);
    }

    [Fact]
    public void Ef_model_has_no_pending_changes_and_knows_exact_migration_chain()
    {
        using var context = Context();

        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(PmsShadowStateContract.MigrationIds, context.Database.GetMigrations().ToArray());
    }

    [Fact]
    public void Corrective_up_renames_git_column_and_replaces_only_related_checks()
    {
        var sql = Script(PmsShadowStateContract.InitialMigrationId, PmsShadowStateContract.CorrectiveMigrationId);

        Assert.Contains("RENAME COLUMN core_master_sha256 TO core_master_commit_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("core_master_object_format", sql, StringComparison.Ordinal);
        Assert.Contains("ck_model_run_artifact_hashes", sql, StringComparison.Ordinal);
        Assert.Contains("ck_model_run_core_master_commit_identity", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ck_model_run_hashes CHECK", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE pms_shadow", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM pms_shadow", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO pms_shadow", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("trade_intent")]
    [InlineData("orders")]
    [InlineData("fills")]
    [InlineData("ledger")]
    public void Corrective_up_touches_no_execution_or_accounting_table(string fragment)
        => Assert.DoesNotContain(fragment,
            Script(PmsShadowStateContract.InitialMigrationId, PmsShadowStateContract.CorrectiveMigrationId),
            StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Corrective_down_restores_initial_contract()
    {
        var sql = Script(PmsShadowStateContract.CorrectiveMigrationId, PmsShadowStateContract.InitialMigrationId);

        Assert.Contains("RENAME COLUMN core_master_commit_id TO core_master_sha256", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ck_model_run_hashes", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP SCHEMA", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Full_down_drops_tables_before_dedicated_schema_without_cascade()
    {
        var sql = Script(PmsShadowStateContract.CorrectiveMigrationId, Migration.InitialDatabase);
        var lastTable = sql.LastIndexOf("DROP TABLE pms_shadow.ingestions", StringComparison.OrdinalIgnoreCase);
        var schema = sql.LastIndexOf("DROP SCHEMA pms_shadow", StringComparison.OrdinalIgnoreCase);

        Assert.True(lastTable >= 0 && schema > lastTable);
        Assert.DoesNotContain("DROP SCHEMA pms_shadow CASCADE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration_sql_generation_is_byte_deterministic()
    {
        var first = Script(Migration.InitialDatabase, PmsShadowStateContract.CorrectiveMigrationId,
            MigrationsSqlGenerationOptions.Idempotent);
        var second = Script(Migration.InitialDatabase, PmsShadowStateContract.CorrectiveMigrationId,
            MigrationsSqlGenerationOptions.Idempotent);

        Assert.Equal(first, second);
        Assert.DoesNotContain("C:\\", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", first, StringComparison.OrdinalIgnoreCase);
    }

    private static PmsShadowDbContext Context()
        => new PmsShadowDesignTimeDbContextFactory().CreateDbContext([]);

    private static string Script(
        string from,
        string to,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        using var context = Context();
        return context.GetService<IMigrator>().GenerateScript(from, to, options);
    }
}
