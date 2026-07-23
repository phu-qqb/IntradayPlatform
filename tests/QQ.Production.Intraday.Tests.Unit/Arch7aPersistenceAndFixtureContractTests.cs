using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7aPersistenceAndFixtureContractTests
{
    [Fact]
    public void Ef_model_has_no_pending_changes_and_includes_arch7a_migration()
    {
        using var context = Context();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(PmsShadowStateContract.Arch7aCorrectiveMigrationId, context.Database.GetMigrations().Last());
        Assert.Equal(PmsShadowStateContract.MigrationIds, context.Database.GetMigrations().ToArray());
    }

    [Fact]
    public void Child_simulated_price_uses_decimal_safe_precision_for_three_integer_digits()
    {
        using var context = Context();
        var property = context.Model.FindEntityType(typeof(PmsShadowChildOrderRow))!
            .FindProperty(nameof(PmsShadowChildOrderRow.SimulatedLimitPrice))!;
        Assert.Equal(28, property.GetPrecision());
        Assert.Equal(12, property.GetScale());
    }

    [Fact]
    public async Task Serialization_retry_unwraps_40001_and_retries_exactly_once()
    {
        var attempts = 0;
        var result = await Arch7aPostgreSqlSerializationRetry.ExecuteAsync(() =>
        {
            attempts++;
            if (attempts == 1) throw WrappedSerializationFailure();
            return Task.FromResult("replayed-identical");
        });

        Assert.Equal("replayed-identical", result);
        Assert.Equal(2, attempts);
        Assert.Equal(1, Arch7aPostgreSqlSerializationRetry.MaxRetries);
    }

    [Fact]
    public async Task Serialization_retry_propagates_a_second_40001()
    {
        var attempts = 0;
        Task<int> AlwaysFails()
        {
            attempts++;
            throw WrappedSerializationFailure();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Arch7aPostgreSqlSerializationRetry.ExecuteAsync(AlwaysFails));
        Assert.Equal(2, attempts);
    }

    [Theory]
    [InlineData(typeof(PmsShadowTradeIntentRow), "shadow_trade_intents")]
    [InlineData(typeof(PmsShadowRiskDecisionRow), "shadow_risk_decisions")]
    [InlineData(typeof(PmsShadowParentOrderRow), "shadow_parent_orders")]
    [InlineData(typeof(PmsShadowChildOrderRow), "shadow_child_orders")]
    [InlineData(typeof(PmsShadowExecutionQualificationRunRow), "shadow_execution_qualification_runs")]
    public void Shadow_execution_entities_map_only_to_dedicated_pms_shadow_tables(Type type, string table)
    {
        using var context = Context();
        var entity = context.Model.FindEntityType(type);
        Assert.NotNull(entity);
        Assert.Equal(PmsShadowStateContract.SchemaName, entity.GetSchema());
        Assert.Equal(table, entity.GetTableName());
    }

    [Theory]
    [InlineData("LMAX_TEST_EOD_ONLY", "TEST")]
    [InlineData("TEST", "TEST")]
    [InlineData("PRODUCTION", "PRODUCTION")]
    public void PostgreSql_reader_normalizes_only_the_authoritative_test_environment(
        string sourceEnvironment, string expected)
        => Assert.Equal(expected, EfArch7aPmsExecutionSourceReader.NormalizeExecutionEnvironment(sourceEnvironment));
    [Fact]
    public void Arch7a_up_is_additive_and_contains_no_data_mutation()
    {
        var sql = Script(PmsShadowStateContract.IntradayEconomicRevisionMigrationId,
            PmsShadowStateContract.Arch7aMigrationId);
        Assert.Contains("CREATE TABLE pms_shadow.shadow_trade_intents", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE pms_shadow.shadow_risk_decisions", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE pms_shadow.shadow_parent_orders", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE pms_shadow.shadow_child_orders", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE pms_shadow.shadow_execution_qualification_runs", sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fk_shadow_trade_intents_economic_revision", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fk_shadow_execution_qualification_runs_economic_revision", sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE pms_shadow.model_runs", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE pms_shadow", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM pms_shadow", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO pms_shadow", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Arch7a_corrective_up_changes_only_child_price_precision()
    {
        var sql = Script(PmsShadowStateContract.Arch7aMigrationId,
            PmsShadowStateContract.Arch7aCorrectiveMigrationId);
        Assert.Contains("ALTER COLUMN simulated_limit_price TYPE numeric(28,12)", sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE pms_shadow", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM pms_shadow", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO pms_shadow", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Arch7a_down_drops_only_the_five_additive_shadow_tables()
    {
        var sql = Script(PmsShadowStateContract.Arch7aMigrationId,
            PmsShadowStateContract.IntradayEconomicRevisionMigrationId);
        Assert.Contains("DROP TABLE pms_shadow.shadow_child_orders", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DROP TABLE pms_shadow.shadow_parent_orders", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DROP TABLE pms_shadow.shadow_risk_decisions", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DROP TABLE pms_shadow.shadow_trade_intents", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DROP TABLE pms_shadow.shadow_execution_qualification_runs", sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP SCHEMA", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE pms_shadow.ingestions", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration_sql_generation_is_byte_deterministic_and_secret_free()
    {
        var first = Script(PmsShadowStateContract.IntradayEconomicRevisionMigrationId,
            PmsShadowStateContract.Arch7aMigrationId, MigrationsSqlGenerationOptions.Idempotent);
        var second = Script(PmsShadowStateContract.IntradayEconomicRevisionMigrationId,
            PmsShadowStateContract.Arch7aMigrationId, MigrationsSqlGenerationOptions.Idempotent);
        Assert.Equal(first, second);
        Assert.DoesNotContain("Password=", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Host=", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", first, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Versioned_execution_fixture_covers_required_shadow_scenarios()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepoRoot(), "tests", "fixtures", "arch7a", "execution-shadow-scenarios.json")));
        var scenarios = document.RootElement.GetProperty("scenarios").EnumerateArray()
            .Select(value => value.GetProperty("id").GetString()).ToArray();
        Assert.Equal(10, scenarios.Length);
        Assert.Contains("eurusd-direct", scenarios);
        Assert.Contains("usdjpy-inversion", scenarios);
        Assert.Contains("eurgbp-cross", scenarios);
        Assert.Contains("no-order", scenarios);
    }

    [Fact]
    public void Versioned_fix_fixture_covers_all_twenty_offline_scenarios()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepoRoot(), "tests", "fixtures", "arch7a", "lmax-fix-order-state-scenarios.json")));
        var scenarios = document.RootElement.GetProperty("scenarios").EnumerateArray()
            .Select(value => value.GetProperty("id").GetString()).ToArray();
        Assert.Equal(20, scenarios.Length);
        Assert.Contains("explicit-empty-snapshot", scenarios);
        Assert.Contains("35h-reject", scenarios);
        Assert.Contains("35af-reject", scenarios);
        Assert.Contains("external-unknown", scenarios);
        Assert.Contains("no-mutation-no-send", scenarios);
    }

    [Fact]
    public void Every_arch7a_json_manifest_is_well_formed()
    {
        var directory = Path.Combine(RepoRoot(), "docs", "architecture", "arch7a");
        var files = Directory.GetFiles(directory, "*.json");
        Assert.True(files.Length >= 20);
        foreach (var file in files)
            using (JsonDocument.Parse(File.ReadAllText(file))) { }
    }

    private static InvalidOperationException WrappedSerializationFailure() => new(
        "transient",
        new DbUpdateException(
            "write",
            new PostgresException("serialization", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure)));

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

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "tests")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("ARCH7A_REPOSITORY_ROOT_NOT_FOUND");
    }
}
