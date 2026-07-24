using System.Data;
using System.Globalization;
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
        bool RequiredBoolean(string name) => bool.TryParse(Required(name), out var parsed)
            ? parsed
            : throw new InvalidDataException($"ARGUMENT_BOOLEAN_INVALID:{name}");

        var mode = Required("--mode");
        var closeUtc = DateTimeOffset.Parse(Required("--slot-close-utc"),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        var slot = PmsShadowIntradayCadenceContract.WindowEnding(closeUtc);
        var handoffRoot = Required("--handoff-root");
        var requestedTargetProfileId = Required("--target-profile-id");

        string targetProfileId;
        string targetFingerprint;
        PmsShadowPostgreSqlTarget? target = null;
        string? connectionString = null;
        if (mode == "prearm-and-import")
        {
            targetProfileId = requestedTargetProfileId;
            connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
            Require(!string.IsNullOrWhiteSpace(connectionString),
                $"{ConnectionEnvironmentVariable}_REQUIRED");
            target = PmsShadowPostgreSqlTargetContract.Validate(connectionString!,
                new(
                    Required("--expected-environment"),
                    Required("--expected-database"),
                    values.GetValueOrDefault("--expected-schema") ?? PmsShadowStateContract.SchemaName,
                    int.Parse(Required("--expected-postgres-major"),
                        CultureInfo.InvariantCulture),
                    RequiredBoolean("--require-tls"),
                    RequiredBoolean("--allow-loopback"),
                    targetProfileId));
            targetFingerprint = target.TargetFingerprint;
        }
        else
        {
            var armedPath = Path.Combine(Path.GetFullPath(handoffRoot), slot.SlotId,
                "importer.armed.json");
            Require(File.Exists(armedPath), "HANDOFF_IMPORTER_NOT_PREARMED");
            using var document = JsonDocument.Parse(File.ReadAllBytes(armedPath));
            var armed = document.RootElement;
            targetProfileId = armed.GetProperty("target_profile_id").GetString()
                ?? throw new InvalidDataException("HANDOFF_ARMED_TARGET_PROFILE_MISSING");
            targetFingerprint = armed.GetProperty("target_fingerprint").GetString()
                ?? throw new InvalidDataException("HANDOFF_ARMED_TARGET_FINGERPRINT_MISSING");
            Require(targetProfileId == requestedTargetProfileId,
                "HANDOFF_ARMED_TARGET_PROFILE_MISMATCH");
            PmsShadowIntradayCadenceContract.RequireSha(
                targetFingerprint, nameof(targetFingerprint));
        }

        var options = PmsShadowFreshSlotHandoffOptions.Create(
            handoffRoot, slot, Required("--source-session-id"),
            Required("--run-id"), Required("--repository-commit"),
            targetProfileId, targetFingerprint,
            TimeSpan.FromMilliseconds(values.TryGetValue("--poll-interval-ms", out var poll)
                ? int.Parse(poll, CultureInfo.InvariantCulture)
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
            Require(armed.GetProperty("target_profile_id").GetString() == options.TargetProfileId &&
                armed.GetProperty("target_fingerprint").GetString() == options.TargetFingerprint,
                "HANDOFF_ARMED_TARGET_MISMATCH");
            Require(armed.GetProperty("prearmed_at_utc").GetDateTimeOffset() < closeUtc,
                "HANDOFF_NOT_PREARMED_BEFORE_SLOT_CLOSE");
            Require(armed.GetProperty("no_order").GetBoolean(), "HANDOFF_NO_ORDER_MISSING");
            timeline.Record(PmsShadowFreshSlotHandoffEvents.CaptureStarted);
            Write(new
            {
                status = "HANDOFF_PREARMED_CAPTURE_MAY_START",
                options.SlotId,
                options.RunId,
                options.TargetProfileId,
                options.TargetFingerprint,
                no_order = true
            });
            return 0;
        }

        if (mode == "publish-ready")
        {
            Require(File.Exists(options.ArmedStatePath), "HANDOFF_IMPORTER_NOT_PREARMED");
            Require(File.Exists(options.OwnershipPath), "HANDOFF_ORCHESTRATOR_OWNER_NOT_ACTIVE");
            var marker = PmsShadowFreshSlotReadyMarkerStore.Build(options,
                Required("--artifact-path"), Required("--manifest-path"), timeline: timeline);
            var status = PmsShadowFreshSlotReadyMarkerStore.PublishAtomic(options, marker, timeline);
            Write(new
            {
                status,
                marker.SlotId,
                marker.ArtifactSha256,
                marker.ManifestSha256,
                marker.CreatedAtUtc,
                marker.TargetProfileId,
                marker.TargetFingerprint,
                marker.NoOrder
            });
            return 0;
        }

        var configuredTarget = target
            ?? throw new InvalidOperationException("POSTGRESQL_TARGET_NOT_CONFIGURED");
        var configuredConnectionString = connectionString
            ?? throw new InvalidOperationException("POSTGRESQL_CONNECTION_STRING_NOT_CONFIGURED");
        var dbOptions = new DbContextOptionsBuilder<PmsShadowDbContext>()
            .UseNpgsql(configuredConnectionString).Options;
        var factory = new HandoffContextFactory(dbOptions);
        var captureRoot = Path.GetFullPath(Required("--capture-root"));
        var coordinatorId = values.GetValueOrDefault("--coordinator-id")
            ?? $"arch7b-prearmed-{Environment.MachineName}";
        var result = await new PmsShadowFreshSlotHandoffRunner(options).RunAsync(
            async cancellationToken =>
            {
                await using (var context = factory.CreateDbContext())
                    Require(!context.Database.HasPendingModelChanges(),
                        "POSTGRESQL_PENDING_MODEL_CHANGES");
                await using var connection = new NpgsqlConnection(configuredConnectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT current_database(),
                           current_setting('server_version_num')::integer,
                           current_setting('TimeZone'),
                           current_setting('transaction_read_only'),
                           to_regnamespace(@expected_schema) IS NOT NULL,
                           EXISTS (
                               SELECT 1
                               FROM "__EFMigrationsHistory"
                               WHERE "MigrationId" = @expected_migration
                           )
                    """;
                command.Parameters.AddWithValue("expected_schema", configuredTarget.ExpectedSchema);
                command.Parameters.AddWithValue(
                    "expected_migration", PmsShadowStateContract.Arch7bMigrationId);
                await using var reader = await command.ExecuteReaderAsync(
                    CommandBehavior.SingleRow, cancellationToken);
                Require(await reader.ReadAsync(cancellationToken),
                    "POSTGRESQL_PREFLIGHT_EMPTY");
                Require(reader.GetString(0) == configuredTarget.Database,
                    "POSTGRESQL_PREFLIGHT_DATABASE_MISMATCH");
                var serverVersionNumber = reader.GetInt32(1);
                Require(serverVersionNumber > 0,
                    "POSTGRESQL_PREFLIGHT_VERSION_INVALID");
                Require(serverVersionNumber / 10000 == configuredTarget.ExpectedPostgresMajor,
                    "POSTGRESQL_PREFLIGHT_MAJOR_MISMATCH");
                Require(IsUtcTimeZone(reader.GetString(2)),
                    "POSTGRESQL_PREFLIGHT_TIMEZONE_NOT_UTC");
                Require(reader.GetString(3) is "off" or "false",
                    "POSTGRESQL_PREFLIGHT_TRANSACTION_READ_ONLY");
                Require(reader.GetBoolean(4),
                    "POSTGRESQL_PREFLIGHT_SCHEMA_MISSING");
                Require(reader.GetBoolean(5),
                    "POSTGRESQL_PREFLIGHT_MIGRATION_MISSING");
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
            target = configuredTarget.ObservableIdentity,
            result.NoOrder
        });
        return 0;
    }

    private static bool IsUtcTimeZone(string value) =>
        value is "UTC" or "Etc/UTC" or "GMT" or "Etc/GMT";

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
