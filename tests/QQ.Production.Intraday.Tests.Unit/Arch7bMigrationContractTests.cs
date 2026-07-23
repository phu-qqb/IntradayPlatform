using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bMigrationContractTests
{
    private static readonly string[] Tables =
    [
        "arch7b_qualification_runs",
        "arch7b_fix_session_events",
        "arch7b_order_send_ledger",
        "arch7b_execution_reports",
        "arch7b_fills",
        "arch7b_position_ledger_events",
        "arch7b_final_reconciliations"
    ];

    [Fact]
    public void Ef_model_has_no_pending_changes_and_arch7b_is_the_latest_migration()
    {
        using var context = Context();

        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(PmsShadowStateContract.Arch7bMigrationId, context.Database.GetMigrations().Last());
        Assert.Equal(PmsShadowStateContract.MigrationIds, context.Database.GetMigrations().ToArray());
    }

    [Fact]
    public void Arch7b_up_is_additive_and_contains_only_the_seven_new_tables()
    {
        var sql = Script(
            PmsShadowStateContract.Arch7aCorrectiveMigrationId,
            PmsShadowStateContract.Arch7bMigrationId);

        Assert.All(Tables, table => Assert.Contains(
            $"CREATE TABLE pms_shadow.{table}",
            sql,
            StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("ALTER TABLE pms_shadow.shadow_", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE pms_shadow", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM pms_shadow", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO pms_shadow", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("REFERENCES pms_shadow.shadow_child_orders", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1754288005", sql, StringComparison.Ordinal);
        Assert.Contains("921640160", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Arch7b_down_drops_only_the_seven_arch7b_tables()
    {
        var sql = Script(
            PmsShadowStateContract.Arch7bMigrationId,
            PmsShadowStateContract.Arch7aCorrectiveMigrationId);

        Assert.All(Tables, table => Assert.Contains(
            $"DROP TABLE pms_shadow.{table}",
            sql,
            StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("DROP SCHEMA", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE pms_shadow.shadow_child_orders", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE pms_shadow.ingestions", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Arch7b_sql_generation_is_byte_deterministic_and_secret_free()
    {
        var first = Script(
            PmsShadowStateContract.Arch7aCorrectiveMigrationId,
            PmsShadowStateContract.Arch7bMigrationId,
            MigrationsSqlGenerationOptions.Idempotent);
        var second = Script(
            PmsShadowStateContract.Arch7aCorrectiveMigrationId,
            PmsShadowStateContract.Arch7bMigrationId,
            MigrationsSqlGenerationOptions.Idempotent);

        Assert.Equal(first, second);
        Assert.DoesNotContain("Password=", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Host=", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Username=", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", first, StringComparison.OrdinalIgnoreCase);
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
