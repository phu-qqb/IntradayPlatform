using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

const string ConnectionEnvironmentVariable = "QQ_PMS_SHADOW_ARCH7A_CONNECTION_STRING";
var values = args.Chunk(2).ToDictionary(value => value[0], value =>
    value.Length == 2 ? value[1] : throw new InvalidOperationException($"ARGUMENT_VALUE_MISSING:{value[0]}"),
    StringComparer.Ordinal);
var mode = Required("--mode");
Require(mode is "preflight" or "apply-and-qualify" or "resume-and-qualify" or "read", "UNKNOWN_MODE");
var sourceSessionId = Required("--source-session-id");
var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
Require(!string.IsNullOrWhiteSpace(connectionString), $"{ConnectionEnvironmentVariable}_REQUIRED");
var connection = new NpgsqlConnectionStringBuilder(connectionString);
Require(connection.Database == "qq_pms_shadow_arch6d_test", "ARCH7A_TEST_DATABASE_REQUIRED");
Require(connection.Host is "127.0.0.1" or "localhost" or "::1", "ARCH7A_LOOPBACK_DATABASE_REQUIRED");

var options = new DbContextOptionsBuilder<PmsShadowDbContext>()
    .UseNpgsql(connectionString, npgsql => npgsql.SetPostgresVersion(16, 0)).Options;
var factory = new ContextFactory(options);
var economicStore = new EfPmsShadowIntradayEconomicProjectionStore(factory);
var projections = await economicStore.ReadAllAsync();
var latestCandidate = projections
    .Where(value => value.Status == "COMPLETED" && value.Qualifying && value.NoOrder)
    .OrderBy(value => value.SlotEndUtc)
    .ThenBy(value => value.RevisionNumber)
    .ThenBy(value => value.ProjectionRevisionId)
    .LastOrDefault() ?? throw new InvalidDataException("ARCH7A_QUALIFYING_SOURCE_NOT_FOUND");
var selected = EfArch7aPmsExecutionSourceReader.SelectLatestQualifyingRevision(
    projections, latestCandidate.SlotId);
Require(selected.SourceSessionId == sourceSessionId, "ARCH7A_SOURCE_SESSION_MISMATCH");

var slot = new Arch7aExecutionSlot(
    selected.SlotId,
    DateOnly.FromDateTime(selected.SlotEndUtc.UtcDateTime),
    selected.SlotEndUtc,
    selected.SlotStartUtc,
    selected.SlotEndUtc);
var evaluationAsOfUtc = selected.SlotEndUtc.AddMinutes(1);
var reader = new EfArch7aPmsExecutionSourceReader(factory);
var source = await reader.ReadAsync(sourceSessionId, slot, evaluationAsOfUtc);
var pipeline = new Arch7aPmsShadowExecutionPipeline();
var plan = pipeline.Build(source);
Require(source.Freshness == Arch7aSourceFreshness.Fresh, "QUALIFICATION_CLOCK_NOT_FRESH");
if (plan.Units.Count == 0)
    throw new InvalidDataException($"ARCH7A_NO_SHADOW_INTENTS_DERIVED:BLOCKERS={string.Join(',', plan.Blockers)}:UNSUPPORTED={string.Join(',', plan.Netting.UnsupportedCurrencies)}:LINES={plan.Netting.ExecutionLines.Count}");
Require(plan.Units.All(NoSendUnit), "ARCH7A_SHADOW_UNIT_ROUTABLE");
Require(plan.NetworkLedger.Count == 0 && plan.NoFixLogon && plan.NoBrokerSend &&
    plan.NoAccountApi && plan.NoDatabento && plan.NoRealAccount &&
    plan.NoFill && plan.NoPositionLedgerEvent, "ARCH7A_NO_EXTERNAL_INVARIANT_FAILED");

var runtimeNow = DateTimeOffset.UtcNow;
var runtimeSource = await reader.ReadAsync(sourceSessionId, slot, runtimeNow);
var runtimePlan = pipeline.Build(runtimeSource);
Require(runtimeSource.Freshness == Arch7aSourceFreshness.Stale, "RUNTIME_CLOCK_EXPECTED_STALE");
Require(runtimePlan.Units.Count == 0, "STALE_RUNTIME_SOURCE_DID_NOT_BLOCK_INTENTS");

await using (var context = factory.CreateDbContext())
{
    var applied = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
    if (mode is "preflight" or "apply-and-qualify")
        Require(applied.SequenceEqual(PmsShadowStateContract.MigrationIds.Take(5), StringComparer.Ordinal),
            "ARCH7A_EXPECTED_EXACTLY_FIVE_ARCH6F_MIGRATIONS_BEFORE_APPLY");
    else if (mode == "resume-and-qualify")
        Require(applied.SequenceEqual(PmsShadowStateContract.MigrationIds.Take(6), StringComparer.Ordinal),
            "ARCH7A_RESUME_EXPECTED_ORIGINAL_MIGRATION_ONLY");
    else
        Require(applied.SequenceEqual(PmsShadowStateContract.MigrationIds, StringComparer.Ordinal),
            "ARCH7A_READ_EXPECTED_ALL_SEVEN_MIGRATIONS");
}

var sourceManifest = new
{
    selected.SlotId,
    operational_date = slot.OperationalDate,
    slot_close_utc = selected.SlotEndUtc,
    economic_revision_id = selected.ProjectionRevisionId,
    selected.RevisionNumber,
    market_observation_sha256 = selected.MarketDataSnapshotSha256,
    model_run_ids = selected.ReusedModelRunIds.Order(),
    target_position_count = selected.TargetPositions.Count,
    drift_count = selected.PositionOnlyDrifts.Count,
    lineage_sha256 = selected.ManifestSha256,
    qualification_status = selected.Status,
    evaluation_as_of_utc = evaluationAsOfUtc,
    qualification_freshness = source.Freshness.ToString().ToUpperInvariant(),
    runtime_observed_at_utc = runtimeNow,
    runtime_freshness = runtimeSource.Freshness.ToString().ToUpperInvariant()
};
var planManifest = new
{
    plan.PlanSha256,
    plan.Netting.NettingSha256,
    expected_trade_intents = plan.Units.Count,
    expected_risk_decisions = plan.Units.Count,
    expected_parent_orders = plan.Units.Count,
    expected_child_orders = plan.Units.Count,
    execution_symbols = plan.Units.Select(value => value.TradeIntent.ExecutionTradableSymbol)
        .Order(StringComparer.Ordinal),
    netting_lines = plan.Netting.ExecutionLines.Select(value => new
    {
        value.ExecutionTradableSymbol,
        value.RequiresInversion,
        value.TargetExecutionQuantity,
        value.CurrentExecutionQuantity,
        value.SignedDesiredDelta,
        value.QuantityIncrement
    }),
    direct_crosses_excluded = plan.Netting.DirectCrossesExcluded,
    blockers = plan.Blockers,
    no_fix_logon = plan.NoFixLogon,
    no_broker_send = plan.NoBrokerSend,
    no_fill = plan.NoFill,
    no_position_ledger_event = plan.NoPositionLedgerEvent,
    network_ledger_count = plan.NetworkLedger.Count
};

if (mode == "preflight")
{
    Write(new
    {
        status = "ARCH7A_PREFLIGHT_QUALIFIED_READ_ONLY",
        migration_id = PmsShadowStateContract.Arch7aCorrectiveMigrationId,
        source = sourceManifest,
        execution = planManifest,
        database_mutated = false,
        external_connections = 0
    });
    return;
}

if (mode is "apply-and-qualify" or "resume-and-qualify")
{
    await using (var context = factory.CreateDbContext())
        await context.Database.MigrateAsync();

    var store = new EfArch7aShadowExecutionStore(factory);
    var concurrent = await Task.WhenAll(store.PersistAsync(plan), store.PersistAsync(plan));
    if (mode == "apply-and-qualify")
        Require(concurrent.Count(value => value == Arch7aShadowStoreResult.Persisted) == 1 &&
            concurrent.Count(value => value == Arch7aShadowStoreResult.AlreadyPersistedIdentical) == 1,
            "ARCH7A_CONCURRENT_FIRST_APPLY_NOT_SINGLE_WRITER");
    else
        Require(concurrent.All(value => value == Arch7aShadowStoreResult.AlreadyPersistedIdentical),
            "ARCH7A_RESUME_NOT_IDENTICAL_REPLAY");
    var replay = await store.PersistAsync(plan);
    Require(replay == Arch7aShadowStoreResult.AlreadyPersistedIdentical,
        "ARCH7A_REPLAY_NOT_ALREADY_APPLIED_IDENTICAL");

    var conflicts = await VerifyConflicts(store, plan);
    Require(conflicts.All(value => value.Value == "ARCH7A_IDEMPOTENCY_CONFLICT"),
        "ARCH7A_CONFLICT_NOT_FAILED_CLOSED");

    var superseded = projections.Where(value => value.ProjectionRevisionId != selected.ProjectionRevisionId)
        .OrderBy(value => value.SlotEndUtc).LastOrDefault();
    if (superseded is not null)
    {
        var code = CaptureError(() => EfArch7aPmsExecutionSourceReader.SelectLatestQualifyingRevision(
            projections, superseded.SlotId));
        Require(code == "ARCH7A_SOURCE_NOT_LATEST_QUALIFYING_REVISION",
            "ARCH7A_SUPERSEDED_SOURCE_NOT_REJECTED");
    }
    var nonQualifying = selected with { Qualifying = false };
    Require(CaptureError(() => EfArch7aPmsExecutionSourceReader.SelectLatestQualifyingRevision(
        [nonQualifying], nonQualifying.SlotId)) == "ARCH7A_QUALIFYING_ECONOMIC_REVISION_NOT_FOUND",
        "ARCH7A_NONQUALIFYING_SOURCE_NOT_REJECTED");
}

var readback = await Readback(factory, selected, plan);
Write(new
{
    status = mode == "read" ? "ARCH7A_POSTGRESQL_READBACK_QUALIFIED" :
        mode == "resume-and-qualify" ? "ARCH7A_REMEDIATED_AND_QUALIFIED" :
        "ARCH7A_APPLIED_AND_QUALIFIED",
    migration_id = PmsShadowStateContract.Arch7aCorrectiveMigrationId,
    source = sourceManifest,
    execution = planManifest,
    readback,
    external_connections = 0
});

string Required(string name) => values.GetValueOrDefault(name) ??
    throw new InvalidOperationException($"ARGUMENT_REQUIRED:{name}");
static void Require(bool condition, string code)
{
    if (!condition) throw new InvalidDataException(code);
}
static bool NoSendUnit(Arch7aShadowExecutionUnit value) =>
    !value.TradeIntent.Actionable && !value.TradeIntent.ExecutionAllowed &&
    !value.TradeIntent.BrokerRouteAllowed &&
    value.RiskDecision.Outcome == Arch7aShadowRiskOutcome.BLOCK_NEW_ORDERS &&
    value.RiskDecision.ReasonCodes.Contains("BROKER_WORKING_LEAVES_UNOBSERVABLE") &&
    !value.RiskDecision.BrokerSendAllowed && !value.ParentOrder.RouteAllowed &&
    !value.ChildOrder.BrokerSendAllowed;
static string CaptureError(Action action)
{
    try { action(); }
    catch (InvalidOperationException error) { return error.Message; }
    return "NO_ERROR";
}
static async Task<IReadOnlyDictionary<string, string>> VerifyConflicts(
    EfArch7aShadowExecutionStore store, Arch7aShadowExecutionPlan original)
{
    var unit = original.Units[0];
    Arch7aShadowExecutionPlan Rehash(Arch7aShadowExecutionPlan value) => value with
    {
        PlanSha256 = Arch7aPmsShadowExecutionPipeline.ComputePlanSha256(
            value.Netting, value.Units, value.Blockers)
    };
    async Task<string> Reject(Arch7aShadowExecutionPlan value)
    {
        try { await store.PersistAsync(Rehash(value)); }
        catch (InvalidOperationException error) { return error.Message; }
        return "NO_ERROR";
    }

    var otherLineage = new string('e', 64);
    var values = new Dictionary<string, Arch7aShadowExecutionPlan>(StringComparer.Ordinal)
    {
        ["trade_intent_quantity"] = original with { Units = [unit with
            { TradeIntent = unit.TradeIntent with
                { SignedDesiredDelta = unit.TradeIntent.SignedDesiredDelta + 1m } }] },
        ["risk_reason"] = original with { Units = [unit with
            { RiskDecision = unit.RiskDecision with { ReasonCodes = ["OTHER_RISK_REASON"] } }] },
        ["parent_symbol"] = original with { Units = [unit with
            { ParentOrder = unit.ParentOrder with { Symbol = "CONFLICT" } }] },
        ["child_parent"] = original with { Units = [unit with
            { ChildOrder = unit.ChildOrder with { Canonical = unit.ChildOrder.Canonical with
                { ParentOrderId = new ParentOrderId(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")) } } }] },
        ["source_lineage"] = original with
        {
            Netting = original.Netting with { SourceLineageSha256 = otherLineage },
            Units = [unit with { TradeIntent = unit.TradeIntent with
                { SourceLineageSha256 = otherLineage } }]
        }
    };
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var value in values)
        result[value.Key] = await Reject(value.Value);
    return result;
}
static async Task<object> Readback(ContextFactory factory,
    PmsShadowIntradayEconomicProjection selected, Arch7aShadowExecutionPlan plan)
{
    await using var context = factory.CreateDbContext();
    var intents = await context.ShadowTradeIntents.AsNoTracking()
        .Where(value => value.EconomicRevisionId == selected.ProjectionRevisionId)
        .OrderBy(value => value.ExecutionTradableSymbol).ToArrayAsync();
    var intentIds = intents.Select(value => value.TradeIntentId).ToArray();
    var risks = await context.ShadowRiskDecisions.AsNoTracking()
        .Where(value => intentIds.Contains(value.TradeIntentId)).ToArrayAsync();
    var parents = await context.ShadowParentOrders.AsNoTracking()
        .Where(value => intentIds.Contains(value.TradeIntentId)).ToArrayAsync();
    var parentIds = parents.Select(value => value.ParentOrderId).ToArray();
    var children = await context.ShadowChildOrders.AsNoTracking()
        .Where(value => parentIds.Contains(value.ParentOrderId)).ToArrayAsync();
    var run = await context.ShadowExecutionQualificationRuns.AsNoTracking()
        .SingleAsync(value => value.EconomicRevisionId == selected.ProjectionRevisionId);

    Require(intents.Length == plan.Units.Count && risks.Length == intents.Length &&
        parents.Length == intents.Length && children.Length == intents.Length,
        "ARCH7A_READBACK_OBJECT_COUNT_MISMATCH");
    Require(intents.All(value => value.EconomicRevisionNumber == 2 &&
        value.PlanSha256 == plan.PlanSha256 && !value.Actionable &&
        !value.ExecutionAllowed && !value.BrokerRouteAllowed),
        "ARCH7A_READBACK_INTENT_INVARIANT_FAILED");
    Require(risks.All(value => value.Outcome == "BLOCK_NEW_ORDERS" &&
        value.ReasonCodesJson.Contains("BROKER_WORKING_LEAVES_UNOBSERVABLE", StringComparison.Ordinal) &&
        value.NoOrderInvariant && !value.BrokerSendAllowed),
        "ARCH7A_READBACK_RISK_INVARIANT_FAILED");
    Require(parents.All(value => value.Status == "SHADOW_PLANNED" && !value.RouteAllowed),
        "ARCH7A_READBACK_PARENT_INVARIANT_FAILED");
    Require(children.All(value => value.Status == "SHADOW_ONLY" && !value.BrokerSendAllowed),
        "ARCH7A_READBACK_CHILD_INVARIANT_FAILED");
    Require(run.Status == "COMPLETED" && run.PlanSha256 == plan.PlanSha256 &&
        run.NoFixLogon && run.NoBrokerSend && run.NoFill && run.NoPositionLedgerEvent,
        "ARCH7A_READBACK_QUALIFICATION_RUN_INCOMPLETE");

    var targetIds = selected.TargetPositions.Select(value => value.TargetPositionId).ToHashSet();
    var driftIds = selected.PositionOnlyDrifts.Select(value => value.DriftId).ToHashSet();
    Require(intents.SelectMany(value => JsonSerializer.Deserialize<Guid[]>(value.TargetPositionIdsJson) ?? [])
        .All(targetIds.Contains), "ARCH7A_READBACK_TARGET_LINEAGE_ORPHAN");
    Require(intents.SelectMany(value => JsonSerializer.Deserialize<Guid[]>(value.DriftIdsJson) ?? [])
        .All(driftIds.Contains), "ARCH7A_READBACK_DRIFT_LINEAGE_ORPHAN");

    var rawTargetCount = await Scalar(context, """
        SELECT count(*) FROM pms_shadow.intraday_target_positions
        WHERE projection_revision_id = @revision
        """, selected.ProjectionRevisionId);
    var rawDriftCount = await Scalar(context, """
        SELECT count(*) FROM pms_shadow.intraday_position_only_drifts
        WHERE projection_revision_id = @revision
        """, selected.ProjectionRevisionId);
    Require(rawTargetCount == 288 && rawDriftCount == 288,
        "ARCH7A_READBACK_ARCH6F_FACT_COUNT_MISMATCH");

    return new
    {
        economic_revision_id = selected.ProjectionRevisionId,
        trade_intents = intents.Length,
        risk_decisions = risks.Length,
        parent_orders = parents.Length,
        child_orders = children.Length,
        qualification_runs = 1,
        raw_arch6f_targets = rawTargetCount,
        raw_arch6f_drifts = rawDriftCount,
        deterministic_ids = plan.Units.Select(value => new
        {
            trade_intent_id = value.TradeIntent.Canonical.Id.Value,
            risk_decision_id = value.RiskDecision.Canonical.Id,
            parent_order_id = value.ParentOrder.Canonical.Id.Value,
            child_order_id = value.ChildOrder.Canonical.Id.Value
        }),
        execution_symbols = intents.Select(value => value.ExecutionTradableSymbol),
        unsupported_crosses_persisted = intents.Count(value =>
            value.ExecutionTradableSymbol is "EURGBP" or "AUDCNH" or "EURZAR" or "MXNNOK"),
        usd_jpy_requires_inversion = intents.Where(value => value.ExecutionTradableSymbol == "USDJPY")
            .Select(value => value.RequiresInversion).SingleOrDefault(),
        no_orphans = true,
        no_arch6f_mutation = true,
        network_ledger_count = 0
    };
}
static async Task<long> Scalar(PmsShadowDbContext context, string sql, Guid revision)
{
    DbConnection connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
        await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    var parameter = command.CreateParameter();
    parameter.ParameterName = "revision";
    parameter.Value = revision;
    command.Parameters.Add(parameter);
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}
static void Write(object value) => Console.WriteLine(JsonSerializer.Serialize(value,
    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true }));

sealed class ContextFactory(DbContextOptions<PmsShadowDbContext> options)
    : IDbContextFactory<PmsShadowDbContext>
{
    public PmsShadowDbContext CreateDbContext() => new(options);
}
