using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

const string ConnectionEnvironmentVariable = "QQ_PMS_SHADOW_ARCH6F_CONNECTION_STRING";
if (Arch7bPrearmedFreshSlotHandoffCli.Handles(args))
{
    Environment.ExitCode = await Arch7bPrearmedFreshSlotHandoffCli.RunAsync(args);
    return;
}

var values = args.Chunk(2).ToDictionary(value => value[0], value =>
    value.Length == 2 ? value[1] : throw new InvalidOperationException($"ARGUMENT_VALUE_MISSING:{value[0]}"),
    StringComparer.Ordinal);
var mode = Required("--mode");
var captureRoot = Path.GetFullPath(Required("--capture-root"));
var sourceSessionId = Required("--source-session-id");
var captures = Directory.GetFiles(captureRoot, "slot_manifest.json", SearchOption.AllDirectories)
    .Select(PmsShadowRealSlotCaptureReader.Read)
    .OrderBy(value => value.SlotStartUtc)
    .ToArray();
Require(captures.Length == PmsShadowIntradayCadenceContract.MinimumRealConsecutiveQualificationSlots,
    "EXACTLY_THREE_REAL_CAPTURES_REQUIRED");
for (var index = 1; index < captures.Length; index++)
    Require(captures[index - 1].SlotEndUtc == captures[index].SlotStartUtc,
        "REAL_CAPTURES_NOT_CONSECUTIVE");
Require(captures.Select(value => value.ArtifactSha256).Distinct(StringComparer.Ordinal).Count() == captures.Length,
    "REAL_CAPTURE_SHA_NOT_DISTINCT");

if (mode == "preflight")
{
    Write(new
    {
        status = "PREFLIGHT_OK",
        migration_id = PmsShadowStateContract.IntradayEconomicRevisionMigrationId,
        source_session_id = sourceSessionId,
        captures = captures.Select(CaptureSummary),
        lmax_sessions = 0,
        polygon_calls = 0,
        gpu_invocations = 0,
        no_order = true
    });
    return;
}

Require(mode is "project-only" or "apply-and-replay" or "read", "UNKNOWN_MODE");
var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
Require(!string.IsNullOrWhiteSpace(connectionString), $"{ConnectionEnvironmentVariable}_REQUIRED");
var options = new DbContextOptionsBuilder<PmsShadowDbContext>()
    .UseNpgsql(connectionString, npgsql => npgsql.SetPostgresVersion(16, 0)).Options;
var factory = new SingleContextFactory(options);
var store = new EfPmsShadowIntradayEconomicProjectionStore(factory);

if (mode == "project-only")
{
    var source = await store.LoadSourceAsync(sourceSessionId);
    var projected = new List<PmsShadowIntradayEconomicProjection>();
    foreach (var capture in captures)
    {
        var superseded = await store.LoadSupersededManifestShaAsync(capture.SlotId);
        projected.Add(new PmsShadowIntradayEconomicProjectionBuilder().Build(capture, source, superseded));
    }
    Require(projected.All(value => value.MarketData.Count == source.Mappings.Count &&
        value.TargetPositions.Count == 288 && value.PositionOnlyDrifts.Count == 288),
        "PROJECT_ONLY_FACT_COUNT_MISMATCH");
    Write(new
    {
        status = "PROJECT_ONLY_OK",
        source_mappings = source.Mappings.Count,
        reused_model_runs = source.Models.Select(value => value.ModelRunId),
        revisions = projected.Select(value => new { value.SlotId, value.RawCaptureSha256,
            value.MarketDataSnapshotSha256, value.InputSha256, value.TargetPositionsSha256,
            value.DriftsSha256, market_observations = value.MarketData.Count,
            target_positions = value.TargetPositions.Count, position_only_drifts = value.PositionOnlyDrifts.Count }),
        lmax_sessions = 0,
        polygon_calls = 0,
        gpu_invocations = 0,
        no_order = true
    });
    return;
}

if (mode == "apply-and-replay")
{
    await using (var context = factory.CreateDbContext())
        await context.Database.MigrateAsync();
    var source = await store.LoadSourceAsync(sourceSessionId);
    var firstPass = new List<PmsShadowEconomicApplyOutcome>();
    var secondPass = new List<PmsShadowEconomicApplyOutcome>();
    foreach (var capture in captures)
    {
        var superseded = await store.LoadSupersededManifestShaAsync(capture.SlotId);
        var projection = new PmsShadowIntradayEconomicProjectionBuilder()
            .Build(capture, source, superseded);
        firstPass.Add(await store.ApplyAsync(projection));
    }
    foreach (var capture in captures)
    {
        var superseded = await store.LoadSupersededManifestShaAsync(capture.SlotId);
        var projection = new PmsShadowIntradayEconomicProjectionBuilder()
            .Build(capture, source, superseded);
        secondPass.Add(await store.ApplyAsync(projection));
    }
    Require(firstPass.All(value => value.Result == PmsShadowEconomicApplyResult.Completed),
        "FIRST_REPLAY_NOT_APPEND_ONLY_COMPLETED");
    Require(secondPass.All(value => value.Result == PmsShadowEconomicApplyResult.AlreadyAppliedIdentical),
        "SECOND_REPLAY_NOT_IDEMPOTENT");
}

var projections = await store.ReadAllAsync();
var campaign = projections.Where(value => captures.Any(capture => capture.SlotId == value.SlotId))
    .OrderBy(value => value.SlotStartUtc).ToArray();
Require(campaign.Length == 3, "QUALIFYING_REVISION_COUNT_MISMATCH");
Require(campaign.All(value => value.TargetPositions.Count == 288 &&
    value.PositionOnlyDrifts.Count == 288 && value.Qualifying && value.NoOrder),
    "QUALIFYING_REVISION_FACTS_INCOMPLETE");
Require(campaign.Select(value => value.MarketDataSnapshotSha256).Distinct(StringComparer.Ordinal).Count() == 3,
    "MARKET_DATA_SNAPSHOT_SHA_NOT_DISTINCT");
Require(campaign.Select(value => value.InputSha256).Distinct(StringComparer.Ordinal).Count() == 3,
    "ECONOMIC_INPUT_SHA_NOT_DISTINCT");
Require(campaign.Select(value => value.TargetPositionsSha256).Distinct(StringComparer.Ordinal).Count() == 3,
    "TARGET_POSITION_SHA_NOT_DISTINCT");
Require(campaign.Select(value => value.DriftsSha256).Distinct(StringComparer.Ordinal).Count() == 3,
    "DRIFT_SHA_NOT_DISTINCT");
Require(campaign.SelectMany(value => value.ReusedModelRunIds).Distinct().Count() == 4,
    "REUSED_D1_MODEL_SET_MISMATCH");

var latest = captures[^1];
var reads = new EfPmsShadowIntradayReadService(new EfPmsShadowIntradaySlotStore(factory),
    new EfPmsShadowOperationalReadService(factory), store);
var snapshot = await reads.GetAsync(latest.SlotEndUtc.AddMinutes(1));
Require(snapshot.SlotFreshnessAndCompleteness.Freshness == PmsShadowIntradayFreshness.Fresh &&
    snapshot.LatestTargetPositionBySlot.Count == 288 &&
    snapshot.LatestPositionOnlyDriftBySlot.Count == 288,
    "LATEST_READ_MODEL_NOT_QUALIFYING");
Write(new
{
    status = "ARCH6F_ECONOMIC_REPLAY_QUALIFIED",
    migration_id = PmsShadowStateContract.IntradayEconomicRevisionMigrationId,
    source_session_id = sourceSessionId,
    revisions = campaign.Select(value => new
    {
        value.ProjectionRevisionId,
        value.RevisionNumber,
        value.SlotId,
        value.RawCaptureSha256,
        value.MarketDataSnapshotSha256,
        value.InputSha256,
        value.TargetPositionsSha256,
        value.DriftsSha256,
        value.ManifestSha256,
        value.SupersedesSlotManifestSha256,
        target_positions = value.TargetPositions.Count,
        position_only_drifts = value.PositionOnlyDrifts.Count,
        model_runs_reused = value.ReusedModelRunIds,
        value.NoOrder
    }),
    latest = snapshot,
    lmax_sessions = 0,
    polygon_calls = 0,
    gpu_invocations = 0,
    no_order = true
});

string Required(string name) => values.GetValueOrDefault(name) ??
    throw new InvalidOperationException($"ARGUMENT_REQUIRED:{name}");
static void Require(bool condition, string code)
{
    if (!condition) throw new InvalidDataException(code);
}
static object CaptureSummary(PmsShadowRealSlotCapture value) => new
{
    value.SlotId,
    value.SlotStartUtc,
    value.SlotEndUtc,
    value.ArtifactSha256,
    bbo_count = value.Bbo.Count,
    value.LmaxPrimary,
    value.PolygonCallCount,
    value.NoOrder
};
static void Write(object value) => Console.WriteLine(JsonSerializer.Serialize(value,
    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true }));

sealed class SingleContextFactory(DbContextOptions<PmsShadowDbContext> options)
    : IDbContextFactory<PmsShadowDbContext>
{
    public PmsShadowDbContext CreateDbContext() => new(options);
}
