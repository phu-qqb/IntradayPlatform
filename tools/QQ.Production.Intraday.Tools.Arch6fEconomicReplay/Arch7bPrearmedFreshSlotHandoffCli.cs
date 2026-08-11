using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class Arch7bPrearmedFreshSlotHandoffCli
{
    public const string NativeContractVersion =
        "arch7b_prearmed_fresh_slot_handoff_cli_v1";
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
            if (RequiredBoolean("--broker-password-only"))
            {
                targetProfileId = requestedTargetProfileId;
                using var brokerConnection = Arch7bPmsPasswordOnlyBrokerConnection.Create(
                    new(
                        Required("--expected-host"),
                        int.Parse(Required("--expected-port"), CultureInfo.InvariantCulture),
                        Required("--expected-database"),
                        Required("--expected-username"),
                        Required("--expected-environment"),
                        values.GetValueOrDefault("--expected-schema") ?? PmsShadowStateContract.SchemaName,
                        int.Parse(Required("--expected-postgres-major"), CultureInfo.InvariantCulture),
                        targetProfileId,
                        Required("--expected-target-fingerprint"),
                        Path.GetFullPath(Required("--root-certificate")),
                        Required("--expected-root-certificate-sha256"),
                        RequiredBoolean("--require-tls"),
                        RequiredBoolean("--allow-loopback"),
                        RequiredBoolean("--pooling"),
                        RequiredBoolean("--enlist"),
                        RequiredBoolean("--multiplexing")));
                connectionString = brokerConnection.ConnectionString;
                target = brokerConnection.Target;
                targetFingerprint = target.TargetFingerprint;
            }
            else
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

        var repositoryCommit = Required("--repository-commit");
        var coreCommit = Required("--core-commit");
        var marketCaptureSessionId = Required("--market-capture-session-id");
        var marketDataConfigPath = Path.GetFullPath(
            Required("--market-data-config-path"));
        var expectedMarketDataConfigSha =
            Required("--expected-market-data-config-sha256");
        var requiredMarketSymbols =
            Arch7bPositionMarketLineageFileStore.ReadRequiredMarketSymbols(
                marketDataConfigPath, expectedMarketDataConfigSha);
        var draftPath = Path.GetFullPath(
            Required("--position-market-draft-path"));
        var lineagePath = Path.GetFullPath(
            Required("--position-market-lineage-path"));
        var options = PmsShadowFreshSlotHandoffOptions.Create(
            handoffRoot, slot, Required("--source-session-id"),
            Required("--run-id"), repositoryCommit,
            targetProfileId, targetFingerprint,
            TimeSpan.FromMilliseconds(values.TryGetValue("--poll-interval-ms", out var poll)
                ? int.Parse(poll, CultureInfo.InvariantCulture)
                : 100));
        var timeline = new PmsShadowFreshSlotHandoffTimeline(options);
        var preflightClockPath = Path.Combine(options.SlotRoot,
            "clock_authority_preflight.json");
        var captureClockPath = Path.Combine(options.SlotRoot,
            "clock_authority_capture.json");
        var hostIdentity = Environment.MachineName;
        var expectedDraftSha = values.GetValueOrDefault(
            "--expected-position-market-draft-sha256");

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
            Require(File.Exists(preflightClockPath),
                PmsShadowCaptureClockAuthorityContract.Blocker);
            var preflightClock =
                PmsShadowCaptureClockAuthorityStore.Read(preflightClockPath);
            PmsShadowCaptureClockAuthorityValidator.RequireQualified(
                preflightClock, DateTimeOffset.UtcNow, hostIdentity, repositoryCommit);
            var captureClock = PmsShadowCaptureClockAuthorityStore.Read(
                Required("--clock-authority-capture-snapshot"));
            PmsShadowCaptureClockAuthorityValidator.RequireQualified(
                captureClock, DateTimeOffset.UtcNow, hostIdentity, repositoryCommit);
            Require(captureClock.SnapshotSha256 != preflightClock.SnapshotSha256 &&
                captureClock.CapturedAtUtc > preflightClock.CapturedAtUtc,
                PmsShadowCaptureClockAuthorityContract.Blocker);
            Require(captureClock.HostClockSource == preflightClock.HostClockSource &&
                captureClock.ReferenceClockSource == preflightClock.ReferenceClockSource,
                PmsShadowCaptureClockAuthorityContract.Blocker);
            PmsShadowCaptureClockAuthorityStore.WriteAtomic(
                captureClockPath, captureClock);
            Require(expectedDraftSha is not null,
                Arch7bPositionMarketRuntimeContract.DraftRequiredBeforeMarketCapture);
            var requiredDraftSha = expectedDraftSha ?? throw new InvalidDataException(
                Arch7bPositionMarketRuntimeContract.DraftRequiredBeforeMarketCapture);
            var draft = Arch7bPositionMarketLiveWiring.RequirePrearmedDraft(
                draftPath, requiredDraftSha, slot, marketCaptureSessionId,
                coreCommit, repositoryCommit, requiredMarketSymbols);
            timeline.Record(PmsShadowFreshSlotHandoffEvents.CaptureStarted);
            Write(new
            {
                status = Arch7bPositionMarketRuntimeContract.DraftReady,
                options.SlotId,
                options.RunId,
                options.TargetProfileId,
                options.TargetFingerprint,
                clock_authority_snapshot_sha256 = captureClock.SnapshotSha256,
                position_market_draft_path = draftPath,
                position_market_draft_sha256 = expectedDraftSha,
                draft.SelectedPositionSnapshotId,
                draft.MarketCaptureSessionId,
                no_order = true
            }, captureClockPath, "clock-authority-capture");
            return 0;
        }

        if (mode == "publish-ready")
        {
            Require(File.Exists(options.ArmedStatePath), "HANDOFF_IMPORTER_NOT_PREARMED");
            Require(File.Exists(options.OwnershipPath), "HANDOFF_ORCHESTRATOR_OWNER_NOT_ACTIVE");
            var artifactPath = Required("--artifact-path");
            var manifestPath = Required("--manifest-path");
            Require(File.Exists(captureClockPath),
                PmsShadowCaptureClockAuthorityContract.Blocker);
            var captureClock =
                PmsShadowCaptureClockAuthorityStore.Read(captureClockPath);
            var postCloseClock = PmsShadowCaptureClockAuthorityStore.Read(
                Required("--clock-authority-post-close-snapshot"));
            PmsShadowCaptureClockAuthorityValidator.RequireQualified(
                postCloseClock, DateTimeOffset.UtcNow,
                hostIdentity, repositoryCommit);
            var clockAuthority = new PmsShadowCaptureClockAuthorityEvidence(
                captureClock, postCloseClock);
            PmsShadowCaptureClockAuthorityValidator.RequireQualifiedForSlot(
                clockAuthority, slot, hostIdentity, repositoryCommit);
            Require(expectedDraftSha is not null,
                Arch7bPositionMarketRuntimeContract.DraftRequiredBeforeMarketCapture);
            var requiredDraftSha = expectedDraftSha ?? throw new InvalidDataException(
                Arch7bPositionMarketRuntimeContract.DraftRequiredBeforeMarketCapture);
            _ = Arch7bPositionMarketLiveWiring.RequirePrearmedDraft(draftPath,
                requiredDraftSha, slot, marketCaptureSessionId, coreCommit,
                repositoryCommit, requiredMarketSymbols);
            var selection = PmsShadowRealSlotManifestFinalizer.Finalize(
                manifestPath, artifactPath, slot, clockAuthority,
                hostIdentity, repositoryCommit);
            Require(selection.Qualifying, "RAW_SLOT_IN_WINDOW_BBO_COVERAGE_INCOMPLETE");
            var capture = PmsShadowRealSlotCaptureReader.Read(manifestPath);
            Require(capture.SlotId == options.SlotId &&
                capture.SlotEndUtc == options.SlotCloseUtc,
                "HANDOFF_MANIFEST_SLOT_MISMATCH");
            var finalization = Arch7bPositionMarketLiveWiring.FinalizeMarket(
                manifestPath, draftPath, expectedDraftSha, lineagePath,
                selection, clockAuthority);
            var marker = PmsShadowFreshSlotReadyMarkerStore.Build(options,
                artifactPath, manifestPath, timeline: timeline,
                positionMarketLineage: finalization.LineageFile);
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
                marker.ClockAuthoritySnapshotSha256,
                marker.ClockPostCloseSnapshotSha256,
                marker.PositionMarketLineagePath,
                marker.PositionMarketLineageSha256,
                finalization.Lineage.EvidenceSha256,
                finalization.PublishedMarketManifestSha256,
                marker.NoOrder
            }, lineagePath, "position-market-lineage");
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
        Arch7bContentAddressedFile? draftAuthority = null;
        var revisionBindingPath = Path.GetFullPath(
            Required("--position-market-revision-binding-path"));
        var result = await new PmsShadowFreshSlotHandoffRunner(options).RunAsync(
            async cancellationToken =>
            {
                var preflightClock = PmsShadowCaptureClockAuthorityStore.Read(
                    Required("--clock-authority-preflight-snapshot"));
                PmsShadowCaptureClockAuthorityValidator.RequireQualified(
                    preflightClock, DateTimeOffset.UtcNow,
                    hostIdentity, repositoryCommit);
                PmsShadowCaptureClockAuthorityStore.WriteAtomic(
                    preflightClockPath, preflightClock);
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
                var source = await new EfPmsShadowIntradayEconomicProjectionStore(
                    factory).LoadSourceAsync(options.SourceSessionId,
                    slot.SlotStartUtc, cancellationToken);
                var published = Arch7bPositionMarketLiveWiring.BuildAndPublishDraft(
                    draftPath, options.RunId, "1754288005", options.TargetProfileId,
                    coreCommit, repositoryCommit, source, slot,
                    marketCaptureSessionId, requiredMarketSymbols);
                draftAuthority = published.File;
            },
            async (marker, observer, cancellationToken) =>
            {
                Require(draftAuthority is not null,
                    Arch7bPositionMarketRuntimeContract.DraftRequiredBeforeMarketCapture);
                Require(marker.PositionMarketLineagePath is not null &&
                    marker.PositionMarketLineageSha256 is not null,
                    Arch7bPositionMarketRuntimeContract.LineageNotInReadyMarker);
                var requiredDraftAuthority = draftAuthority ??
                    throw new InvalidDataException(
                        Arch7bPositionMarketRuntimeContract.DraftRequiredBeforeMarketCapture);
                var requiredLineagePath = marker.PositionMarketLineagePath ??
                    throw new InvalidDataException(
                        Arch7bPositionMarketRuntimeContract.LineageNotInReadyMarker);
                var requiredLineageSha = marker.PositionMarketLineageSha256 ??
                    throw new InvalidDataException(
                        Arch7bPositionMarketRuntimeContract.LineageNotInReadyMarker);
                var store = new EfPmsShadowIntradaySlotStore(factory, observer);
                var economicStore = new EfPmsShadowIntradayEconomicProjectionStore(factory);
                var pipeline = new PmsShadowIntradayEconomicRefreshPipeline(
                    captureRoot, options.SourceSessionId, economicStore,
                    new Arch7bPositionMarketImportAuthority(
                        requiredDraftAuthority.Path, requiredDraftAuthority.Sha256,
                        requiredLineagePath, requiredLineageSha,
                        revisionBindingPath, marker));
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
            position_market_revision_binding_path = revisionBindingPath,
            position_market_revision_binding_sha256 =
                Arch7bPositionMarketLineageFileStore.Sha256File(revisionBindingPath),
            result.NoOrder
        }, revisionBindingPath, "position-market-revision-binding");
        return 0;
    }

    private static bool IsUtcTimeZone(string value) =>
        value is "UTC" or "Etc/UTC" or "GMT" or "Etc/GMT";

    private static readonly JsonSerializerOptions NativeJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    private static void Write(object value, string artifactPath,
        string artifactType)
    {
        artifactPath = Path.GetFullPath(artifactPath);
        if (!File.Exists(artifactPath))
            throw new InvalidDataException("HANDOFF_NATIVE_ARTIFACT_MISSING");
        var root = JsonSerializer.SerializeToNode(value, NativeJson)?.AsObject()
            ?? throw new InvalidDataException("HANDOFF_NATIVE_OUTPUT_INVALID");
        root["contract"] = NativeContractVersion;
        root["artifacts"] = new JsonArray
        {
            new JsonObject
            {
                ["path"] = artifactPath,
                ["sha256"] = Arch7bPositionMarketLineageFileStore.Sha256File(
                    artifactPath),
                ["artifact_type"] = artifactType
            }
        };
        root["evidence_sha256"] = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(root.ToJsonString())));
        Console.WriteLine(root.ToJsonString(NativeJson));
    }

    private sealed class HandoffContextFactory(DbContextOptions<PmsShadowDbContext> options)
        : IDbContextFactory<PmsShadowDbContext>
    {
        public PmsShadowDbContext CreateDbContext() => new(options);
    }
}
