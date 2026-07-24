using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class Arch7bPrearmedFreshSlotHandoffCli
{
    private const string ConnectionEnvironmentVariable =
        "QQ_PMS_SHADOW_ARCH7B_CONNECTION_STRING";

    public static bool Handles(string[] args)
    {
        var index = Array.IndexOf(args, "--mode");
        return index >= 0 && index + 1 < args.Length &&
            args[index + 1] is "prearm-and-import" or "publish-ready" or "assert-prearmed";
    }

    public static async Task<int> RunAsync(string[] args)
    {
        var values = args.Chunk(2).ToDictionary(
            value => value[0],
            value => value.Length == 2
                ? value[1]
                : throw new InvalidOperationException($"ARGUMENT_VALUE_MISSING:{value[0]}"),
            StringComparer.Ordinal);
        string Required(string name) => values.GetValueOrDefault(name)
            ?? throw new InvalidOperationException($"ARGUMENT_REQUIRED:{name}");
        static void Require(bool condition, string code)
        {
            if (!condition) throw new InvalidDataException(code);
        }

        var mode = Required("--mode");
        var closeUtc = DateTimeOffset.Parse(Required("--slot-close-utc"),
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal |
            System.Globalization.DateTimeStyles.AdjustToUniversal);
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(closeUtc);
        var options = PmsShadowFreshSlotHandoffOptions.Create(
            Required("--handoff-root"), slot, Required("--source-session-id"),
            Required("--run-id"), Required("--repository-commit"),
            TimeSpan.FromMilliseconds(values.TryGetValue("--poll-interval-ms", out var poll)
                ? int.Parse(poll, System.Globalization.CultureInfo.InvariantCulture)
                : 100));
        var timeline = new PmsShadowFreshSlotHandoffTimeline(options);

        if (mode == "assert-prearmed")
        {
            Require(File.Exists(options.ArmedStatePath), "HANDOFF_IMPORTER_NOT_PREARMED");
            Require(File.Exists(options.OwnershipPath), "HANDOFF_ORCHESTRATOR_OWNER_NOT_ACTIVE");
            Require(!File.Exists(options.ReadyMarkerPath), "HANDOFF_READY_MARKER_PREEXISTS_CAPTURE");
            using var document = JsonDocument.Parse(File.ReadAllBytes(options.ArmedStatePath));
            var armed = document.RootElement;
            Require(armed.GetProperty("slot_id").GetString() == options.SlotId,
                "HANDOFF_ARMED_SLOT_MISMATCH");
            Require(armed.GetProperty("run_id").GetString() == options.RunId,
                "HANDOFF_ARMED_RUN_MISMATCH");
            Require(armed.GetProperty("prearmed_at_utc").GetDateTimeOffset() < closeUtc,
                "HANDOFF_NOT_PREARMED_BEFORE_SLOT_CLOSE");
            Require(armed.GetProperty("no_order").GetBoolean(), "HANDOFF_NO_ORDER_MISSING");
            timeline.Record(PmsShadowFreshSlotHandoffEvents.CaptureStarted);
            Write(new { status = "HANDOFF_PREARMED_CAPTURE_MAY_START", options.SlotId,
                options.RunId, no_order = true });
            return 0;
        }

        if (mode == "publish-ready")
        {
            Require(File.Exists(options.ArmedStatePath), "HANDOFF_IMPORTER_NOT_PREARMED");
            Require(File.Exists(options.OwnershipPath), "HANDOFF_ORCHESTRATOR_OWNER_NOT_ACTIVE");
            var marker = PmsShadowFreshSlotReadyMarkerStore.Build(options, Required("--artifact-path"),
                Required("--manifest-path"), timeline: timeline);
            var status = PmsShadowFreshSlotReadyMarkerStore.PublishAtomic(options, marker, timeline);
            Write(new { status, marker.SlotId, marker.ArtifactSha256, marker.ManifestSha256,
                marker.CreatedAtUtc, marker.NoOrder });
            return 0;
        }

        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        Require(!string.IsNullOrWhiteSpace(connectionString),
            $"{ConnectionEnvironmentVariable}_REQUIRED");
        var identity = new NpgsqlConnectionStringBuilder(connectionString);
        Require(identity.Database == "qq_pms_shadow_arch6d_test",
            "ARCH7B_TEST_DATABASE_REQUIRED");
        Require(identity.Host is "127.0.0.1" or "localhost" or "::1",
            "ARCH7B_LOOPBACK_DATABASE_REQUIRED");
        var dbOptions = new DbContextOptionsBuilder<PmsShadowDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.SetPostgresVersion(16, 0)).Options;
        var factory = new HandoffContextFactory(dbOptions);
        var captureRoot = Path.GetFullPath(Required("--capture-root"));
        var coordinatorId = values.GetValueOrDefault("--coordinator-id")
            ?? $"arch7b-prearmed-{Environment.MachineName}";
        var result = await new PmsShadowFreshSlotHandoffRunner(options).RunAsync(
            async cancellationToken =>
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT current_database(), current_setting('transaction_read_only')";
                await using var reader = await command.ExecuteReaderAsync(
                    CommandBehavior.SingleRow, cancellationToken);
                Require(await reader.ReadAsync(cancellationToken),
                    "POSTGRESQL_PREFLIGHT_EMPTY");
                Require(reader.GetString(0) == "qq_pms_shadow_arch6d_test",
                    "POSTGRESQL_PREFLIGHT_DATABASE_MISMATCH");
            },
            async (marker, observer, cancellationToken) =>
            {
                var store = new EfPmsShadowIntradaySlotStore(factory, observer);
                var economicStore = new EfPmsShadowIntradayEconomicProjectionStore(factory);
                var pipeline = new PmsShadowIntradayEconomicRefreshPipeline(
                    captureRoot, options.SourceSessionId, economicStore);
                var tick = await new PmsShadowIntradayScheduler(store, pipeline)
                    .RunClosedSlotAsync(DateTimeOffset.UtcNow, coordinatorId, cancellationToken);
                Require(tick.Slot.SlotId == marker.SlotId,
                    "HANDOFF_SCHEDULER_SLOT_MISMATCH");
                return tick.FinalStatus.ToString().ToUpperInvariant();
            });
        Write(new
        {
            result.Status,
            result.Marker.SlotId,
            result.Marker.ArtifactSha256,
            result.Marker.ManifestSha256,
            result.DetectedAtUtc,
            result.ImportStartedAtUtc,
            result.CompletedAtUtc,
            result.DetectionLatencyMilliseconds,
            result.WithinAbsoluteStartDeadline,
            result.NoOrder
        });
        return 0;
    }

    private static void Write(object value) => Console.WriteLine(JsonSerializer.Serialize(value,
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        }));

    private sealed class HandoffContextFactory(DbContextOptions<PmsShadowDbContext> options)
        : IDbContextFactory<PmsShadowDbContext>
    {
        public PmsShadowDbContext CreateDbContext() => new(options);
    }
}
