using System.Text.Json;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch6fIntradayPmsShadowContractTests
{
    [Fact]
    public void AllRequiredAlertsHaveDeterministicPolicyMappings()
    {
        var issues = new[]
        {
            "INTRADAY_SLOT_MISSING", "INTRADAY_SLOT_STALE", "SLOT_HANDOFF_NOT_FINALIZED",
            "INTRADAY_SLOT_FAILED_CLOSED", "SLOT_OVERLAP_REJECTED", "RESTART_RECOVERY_REQUIRED",
            "LMAX_GAP_UNFILLED", "POLYGON_SOURCE_CONFLICT", "INGESTION_FAILED",
            "NO_ORDER_INVARIANT_VIOLATION"
        };
        var alerts = PmsShadowIntradayAlertPolicy.ForIssues("slot", new DateOnly(2026, 7, 21),
            Utc(2026, 7, 21, 13, 30), Hash('e'), issues);
        Assert.Equal(PmsShadowIntradayAlertCodes.Required.Order(StringComparer.Ordinal),
            alerts.Select(value => value.Code));
        Assert.Equal("CRITICAL", alerts.Single(value =>
            value.Code == "NO_ORDER_INVARIANT_VIOLATION").Severity);
    }

    [Fact]
    public void MigrationCreatesOneAppendOnlySlotRegistryWithRequiredConstraints()
    {
        using var context = new PmsShadowDesignTimeDbContextFactory().CreateDbContext([]);
        var migrator = context.GetService<IMigrator>();
        var up = migrator.GenerateScript(PmsShadowStateContract.CorrectiveMigrationId,
            PmsShadowStateContract.IntradayMigrationId);
        Assert.Contains("CREATE TABLE pms_shadow.intraday_slots", up, StringComparison.Ordinal);
        Assert.Contains("PRIMARY KEY", up, StringComparison.Ordinal);
        Assert.Contains("pg_advisory", File.ReadAllText(Source("PmsShadowIntradayPersistence.cs")),
            StringComparison.Ordinal);
        Assert.Contains("status IN ('MISSED','RUNNING','COMPLETED','FAILED_CLOSED')", up,
            StringComparison.Ordinal);
        Assert.Contains("FRESH_DRIFT_EVERY_15_MINUTES_WITH_MODEL_SCHEDULE", up,
            StringComparison.Ordinal);
        Assert.Contains("CHECK (no_order)", up, StringComparison.Ordinal);
        Assert.Contains("REFERENCES pms_shadow.ingestions", up, StringComparison.Ordinal);
    }

    [Fact]
    public void IdempotentAndFullDownScriptsContainIntradayMigrationBoundary()
    {
        using var context = new PmsShadowDesignTimeDbContextFactory().CreateDbContext([]);
        var migrator = context.GetService<IMigrator>();
        var idempotent = migrator.GenerateScript(null, PmsShadowStateContract.IntradayMigrationId,
            MigrationsSqlGenerationOptions.Idempotent);
        var down = migrator.GenerateScript(PmsShadowStateContract.IntradayMigrationId, "0");
        Assert.Contains(PmsShadowStateContract.IntradayMigrationId, idempotent, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE pms_shadow.intraday_slots", idempotent, StringComparison.Ordinal);
        Assert.Contains("DROP TABLE IF EXISTS pms_shadow.intraday_slots", down, StringComparison.Ordinal);
        Assert.Contains("DROP SCHEMA pms_shadow", down, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FinalizedHandoffPipelineRejectsMissingAndWrongSlotFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"arch6f-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var slot = PmsShadowIntradayCadenceContract.WindowEnding(Utc(2026, 7, 21, 13, 30));
            var pipeline = new FinalizedPmsShadowIntradayManifestPipeline(root);
            var missing = await Assert.ThrowsAsync<InvalidDataException>(() => pipeline.ExecuteAsync(slot));
            Assert.Equal("INTRADAY_SLOT_INCOMPLETE", missing.Message);

            var wrong = ValidManifest(slot) with { SlotId = "wrong-slot" };
            await File.WriteAllTextAsync(Path.Combine(root, $"{slot.SlotId}.json"),
                JsonSerializer.Serialize(wrong, new JsonSerializerOptions
                { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
            var mismatch = await Assert.ThrowsAsync<InvalidDataException>(() => pipeline.ExecuteAsync(slot));
            Assert.Equal("SLOT_WINDOW_IDENTITY_MISMATCH", mismatch.Message);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task FinalizedHandoffPipelineAcceptsOnlyCompleteNoOrderManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), $"arch6f-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var slot = PmsShadowIntradayCadenceContract.WindowEnding(Utc(2026, 7, 21, 13, 30));
            var manifest = ValidManifest(slot);
            await File.WriteAllTextAsync(Path.Combine(root, $"{slot.SlotId}.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions
                { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
            var parsed = await new FinalizedPmsShadowIntradayManifestPipeline(root).ExecuteAsync(slot);
            Assert.Equal(slot.SlotId, parsed.SlotId);
            Assert.True(parsed.NoOrderCounters.IsValid);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static PmsShadowIntradaySlotManifest ValidManifest(PmsShadowIntradaySlotWindow slot)
    {
        var ids = Enumerable.Range(1, 4).Select(index =>
            Guid.Parse($"00000000-0000-0000-0000-{index:D12}")).ToArray();
        return new(slot.SlotId, slot.SlotStartUtc, slot.SlotEndUtc, slot.OperationalDate,
            "lmax-capture", Hash('a'), 0, [], 0, [], false, ids, [], ids,
            new Dictionary<string, string>
            {
                ["INFX7"] = Hash('7'), ["INFX8"] = Hash('8'),
                ["INFX9"] = Hash('9'), ["INFX10"] = Hash('a')
            }, 288, 288, PmsShadowStateContract.BrokerAdjustedBlocker, Hash('b'),
            "arch6b-daily-tier1-20260721T130346Z-422530a8", Guid.NewGuid(),
            "ALREADY_APPLIED_IDENTICAL", new Dictionary<string, int> { ["ingestions"] = 1 },
            PmsShadowIntradayFreshness.Fresh, PmsShadowIntradayNoOrderCounters.Zero,
            true, slot.SlotEndUtc.AddMinutes(1));
    }

    private static string Source(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
            directory = directory.Parent;
        return Path.Combine(directory!.FullName, "src",
            "QQ.Production.Intraday.Infrastructure.PostgreSql", name);
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);
    private static string Hash(char value) => new(value, 64);
}
