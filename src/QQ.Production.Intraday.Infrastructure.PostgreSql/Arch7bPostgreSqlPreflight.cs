using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public sealed record Arch7bIntradayMarketObservationExpectation(
    Guid EconomicRevisionId,
    int EconomicRevisionNumber,
    string SlotId,
    Guid IngestionId,
    string SourceSessionId,
    string MarketDataSnapshotSha256,
    string SecurityId,
    string Symbol);

public sealed record Arch7bIntradayMarketObservationReadRow(
    Guid EconomicRevisionId,
    int EconomicRevisionNumber,
    string SlotId,
    Guid SourceIngestionId,
    string SourceSessionId,
    string MarketDataSnapshotSha256,
    string RevisionStatus,
    string ExternalCompletionStatus,
    bool Qualifying,
    bool RevisionNoOrder,
    DateTimeOffset SlotStartUtc,
    DateTimeOffset SlotEndUtc,
    string SlotStatus,
    bool SlotNoOrder,
    Guid InstrumentId,
    string SecurityId,
    string Symbol,
    string LmaxInstrumentId,
    decimal Bid,
    decimal Ask,
    DateTimeOffset EventTimeUtc,
    string ProjectionMethod,
    string ProjectionLegSecurityIdsJson);

public static class Arch7bIntradayMarketObservationResolver
{
    public static Arch7bIntradayMarketObservationReadRow Resolve(
        Arch7bIntradayMarketObservationExpectation expected,
        IReadOnlyList<Arch7bIntradayMarketObservationReadRow> rows)
    {
        if (rows.Count == 0)
            throw new InvalidOperationException(
                "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_MISSING");
        if (rows.Any(value =>
                value.EconomicRevisionId != expected.EconomicRevisionId ||
                value.EconomicRevisionNumber != expected.EconomicRevisionNumber))
            throw new InvalidOperationException(
                "ARCH7B_POSTGRESQL_PREFLIGHT_ECONOMIC_REVISION_MISMATCH");
        if (rows.Any(value => value.SlotId != expected.SlotId))
            throw new InvalidOperationException(
                "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_SLOT_MISMATCH");
        if (rows.Any(value =>
                value.SourceIngestionId != expected.IngestionId ||
                value.SourceSessionId != expected.SourceSessionId))
            throw new InvalidOperationException(
                "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_LINEAGE_INCOMPLETE");
        if (rows.Any(value =>
                !value.MarketDataSnapshotSha256.Equals(
                    expected.MarketDataSnapshotSha256,
                    StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_SHA_MISMATCH");
        if (rows.Any(value =>
                value.RevisionStatus != "COMPLETED" ||
                value.ExternalCompletionStatus != PmsShadowStateContract.CompletedNoExternal ||
                !value.Qualifying ||
                !value.RevisionNoOrder ||
                value.SlotStatus != "COMPLETED" ||
                !value.SlotNoOrder))
            throw new InvalidOperationException(
                "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_LINEAGE_INCOMPLETE");

        var candidates = rows
            .Where(value => value.SecurityId == expected.SecurityId)
            .ToArray();
        if (candidates.Length == 0)
            throw new InvalidOperationException(
                "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_INSTRUMENT_MISMATCH");
        if (candidates.Length != 1)
            throw new InvalidOperationException(
                "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_AMBIGUOUS");

        var selected = candidates[0];
        if (NormalizeSymbol(selected.Symbol) != NormalizeSymbol(expected.Symbol) ||
            selected.LmaxInstrumentId != expected.SecurityId)
            throw new InvalidOperationException(
                "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_INSTRUMENT_MISMATCH");
        if (selected.EventTimeUtc < selected.SlotStartUtc ||
            selected.EventTimeUtc > selected.SlotEndUtc)
            throw new InvalidOperationException(
                "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_SLOT_MISMATCH");
        if (selected.ProjectionMethod is not ("LMAX_DIRECT" or "LMAX_DIRECT_INVERTED"))
            throw new InvalidOperationException(
                "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_NON_LMAX");
        if (selected.Bid <= 0m || selected.Ask < selected.Bid ||
            !HasCompleteLmaxLegLineage(selected))
            throw new InvalidOperationException(
                "ARCH7B_POSTGRESQL_PREFLIGHT_INTRADAY_MARKET_OBSERVATION_LINEAGE_INCOMPLETE");

        return selected;
    }

    private static bool HasCompleteLmaxLegLineage(
        Arch7bIntradayMarketObservationReadRow value)
    {
        try
        {
            var legs = JsonSerializer.Deserialize<string[]>(
                value.ProjectionLegSecurityIdsJson);
            return legs is { Length: > 0 } &&
                   legs.All(leg =>
                       !string.IsNullOrWhiteSpace(leg) &&
                       !leg.Contains("POLYGON", StringComparison.OrdinalIgnoreCase)) &&
                   (value.ProjectionMethod == "LMAX_DIRECT"
                       ? legs.Contains(value.LmaxInstrumentId, StringComparer.Ordinal)
                       : true);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string NormalizeSymbol(string value)
        => new(value.Where(char.IsAsciiLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
}

public sealed record Arch7bPostgreSqlPreflightSnapshot(
    Arch7bSelectedChildOrder ChildOrder,
    decimal CurrentKnownPosition,
    int PlatformKnownWorkingOrderCount,
    PmsArch7bQualificationRunRow? ExistingRun,
    bool OpeningSendIntentExists);

public sealed class EfArch7bPostgreSqlPreflightReader(
    IDbContextFactory<PmsShadowDbContext> contextFactory)
{
    public const string CanonicalMarketObservationSql = """
        SELECT pr.projection_revision_id,
               pr.revision_number,
               pr.slot_id,
               pr.source_ingestion_id,
               pr.source_session_id,
               pr.market_data_snapshot_sha256,
               pr.status,
               pr.external_completion_status,
               pr.qualifying,
               pr.no_order,
               slot.slot_start_utc,
               slot.slot_end_utc,
               slot.status,
               slot.no_order,
               observation.instrument_id,
               observation.security_id,
               observation.symbol,
               observation.lmax_instrument_id,
               observation.bid,
               observation.ask,
               observation.event_time_utc,
               observation.projection_method,
               observation.projection_leg_security_ids_json::text
          FROM pms_shadow.intraday_projection_revisions AS pr
          JOIN pms_shadow.intraday_slots AS slot
            ON slot.slot_id = pr.slot_id
          JOIN pms_shadow.intraday_market_data_observations AS observation
            ON observation.projection_revision_id = pr.projection_revision_id
         WHERE pr.projection_revision_id = @economic_revision_id
         ORDER BY observation.instrument_id
        """;

    public async Task<Arch7bPostgreSqlPreflightSnapshot> ReadAsync(
        Guid childOrderId,
        Guid qualificationRunId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (childOrderId == Guid.Empty || qualificationRunId == Guid.Empty)
            throw new InvalidOperationException("ARCH7B_PREFLIGHT_IDENTITY_MISSING");
        if (nowUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("ARCH7B_PREFLIGHT_TIME_NOT_UTC");

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var child = await context.ShadowChildOrders.AsNoTracking()
            .SingleAsync(value => value.ChildOrderId == childOrderId, cancellationToken);
        var parent = await context.ShadowParentOrders.AsNoTracking()
            .SingleAsync(value => value.ParentOrderId == child.ParentOrderId, cancellationToken);
        var intent = await context.ShadowTradeIntents.AsNoTracking()
            .SingleAsync(value => value.TradeIntentId == parent.TradeIntentId, cancellationToken);
        var risk = await context.ShadowRiskDecisions.AsNoTracking()
            .SingleAsync(value => value.RiskDecisionId == parent.RiskDecisionId, cancellationToken);
        var sourceRun = await context.ShadowExecutionQualificationRuns.AsNoTracking()
            .SingleAsync(value => value.EconomicRevisionId == intent.EconomicRevisionId, cancellationToken);
        var latestSourceRun = await context.ShadowExecutionQualificationRuns.AsNoTracking()
            .Where(value => value.Status == "COMPLETED")
            .OrderByDescending(value => value.CompletedAtUtc)
            .ThenByDescending(value => value.QualificationRunId)
            .FirstOrDefaultAsync(cancellationToken);
        var existingRun = await context.Arch7bQualificationRuns.AsNoTracking()
            .SingleOrDefaultAsync(value => value.QualificationRunId == qualificationRunId, cancellationToken);
        var openingSendIntentExists = await context.Arch7bOrderSendLedger.AsNoTracking()
            .AnyAsync(value =>
                value.QualificationRunId == qualificationRunId &&
                value.MessageType == "D" &&
                value.LifecycleRole == "OPEN", cancellationToken);

        var marketObservationRows = await ReadCanonicalMarketObservationsAsync(
            context,
            intent.EconomicRevisionId,
            cancellationToken);
        var marketObservation = Arch7bIntradayMarketObservationResolver.Resolve(
            new(
                intent.EconomicRevisionId,
                intent.EconomicRevisionNumber,
                intent.SlotId,
                intent.IngestionId,
                intent.SourceSessionId,
                intent.MarketDataSnapshotSha256,
                intent.SecurityId,
                intent.ExecutionTradableSymbol),
            marketObservationRows);
        var positionSnapshot = await context.PositionSnapshots.AsNoTracking()
            .SingleAsync(value => value.IngestionId == intent.IngestionId, cancellationToken);
        var positionLine = await context.PositionSnapshotLines.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.PositionSnapshotId == positionSnapshot.PositionSnapshotId &&
                value.SecurityId == intent.SecurityId, cancellationToken);
        var sourcePosition = positionLine?.CurrentBaseQuantity ??
            (positionSnapshot.EmptyStateWasExplicitlyObserved &&
             !positionSnapshot.EmptyStateWasInferred
                ? 0m
                : throw new InvalidOperationException("ARCH7B_POSITION_SOURCE_MISSING"));
        var subsequentOtherRunPosition = await context.Arch7bPositionLedgerEvents.AsNoTracking()
            .Where(value =>
                value.QualificationRunId != qualificationRunId &&
                value.SecurityId == intent.SecurityId &&
                value.EventTimeUtc > intent.CreatedAtUtc)
            .SumAsync(value => value.SignedQuantity, cancellationToken);
        var platformKnownWorkingOrderCount = await CountOtherKnownWorkingOrdersAsync(
            context,
            qualificationRunId,
            cancellationToken);

        var slotRows = await new EfPmsShadowIntradaySlotStore(contextFactory)
            .ReadAllAsync(cancellationToken);
        var intraday = PmsShadowIntradayProjection.Build(slotRows, nowUtc);
        var latestSlot = intraday.LatestIntradayShadowSlot.Slot;
        var sourceFresh = latestSlot?.SlotId == intent.SlotId &&
                          intraday.SlotFreshnessAndCompleteness.Freshness ==
                          PmsShadowIntradayFreshness.Fresh &&
                          intraday.SlotFreshnessAndCompleteness.Complete;
        var sourceSuperseded = latestSourceRun is null ||
                               latestSourceRun.EconomicRevisionId != intent.EconomicRevisionId;
        var lmaxMarketData = marketObservation.ProjectionMethod is
            "LMAX_DIRECT" or "LMAX_DIRECT_INVERTED";
        var sourceCompleted =
            sourceRun.Status == "COMPLETED" &&
            sourceRun.NoFixLogon &&
            sourceRun.NoBrokerSend &&
            sourceRun.NoFill &&
            sourceRun.NoPositionLedgerEvent &&
            sourceRun.IntentCount == 7 &&
            sourceRun.RiskDecisionCount == 7 &&
            sourceRun.ParentOrderCount == 7 &&
            sourceRun.ChildOrderCount == 7 &&
            risk.SourceComplete &&
            risk.NoOrderInvariant &&
            !risk.BrokerSendAllowed &&
            !parent.RouteAllowed &&
            !child.BrokerSendAllowed;
        var lineageComplete =
            IsSha256(intent.MarketDataSnapshotSha256) &&
            IsSha256(intent.SourceLineageSha256) &&
            IsSha256(intent.LineageSha256) &&
            IsSha256(intent.PlanSha256) &&
            intent.PlanSha256 == risk.PlanSha256 &&
            intent.PlanSha256 == parent.PlanSha256 &&
            intent.PlanSha256 == child.PlanSha256 &&
            intent.PlanSha256 == sourceRun.PlanSha256 &&
            intent.SourceLineageSha256 == sourceRun.SourceLineageSha256 &&
            intent.CurrentQuantity == sourcePosition &&
            marketObservation.MarketDataSnapshotSha256 ==
            intent.MarketDataSnapshotSha256;

        var selected = new Arch7bSelectedChildOrder(
            intent.TradeIntentId,
            parent.ParentOrderId,
            child.ChildOrderId,
            child.ClientOrderId,
            intent.SourceSessionId,
            intent.SlotId,
            intent.EconomicRevisionId,
            intent.EconomicRevisionNumber,
            intent.MarketDataSnapshotSha256,
            intent.SourceLineageSha256,
            intent.PlanSha256,
            intent.Environment,
            intent.AccountScope,
            parent.Symbol,
            intent.SecurityId,
            intent.SecurityIdSource,
            child.Side,
            child.Quantity,
            intent.TargetCloseUtc,
            intent.EffectiveFromUtc,
            intent.DeadlineUtc,
            intent.Classification,
            parent.Status,
            child.Status,
            LatestQualifyingRevision: !sourceSuperseded,
            sourceCompleted,
            sourceFresh,
            sourceSuperseded,
            lmaxMarketData,
            PolygonOrderPrice: !lmaxMarketData,
            lineageComplete);

        return new(
            selected,
            sourcePosition + subsequentOtherRunPosition,
            platformKnownWorkingOrderCount,
            existingRun,
            openingSendIntentExists);
    }

    private static async Task<int> CountOtherKnownWorkingOrdersAsync(
        PmsShadowDbContext context,
        Guid qualificationRunId,
        CancellationToken cancellationToken)
    {
        var sends = await context.Arch7bOrderSendLedger.AsNoTracking()
            .Where(value =>
                value.QualificationRunId != qualificationRunId &&
                value.MessageType == "D")
            .ToArrayAsync(cancellationToken);
        if (sends.Length == 0)
            return await context.ShadowChildOrders.AsNoTracking()
                .CountAsync(value => value.BrokerSendAllowed, cancellationToken);

        var otherRunIds = sends.Select(value => value.QualificationRunId).Distinct().ToArray();
        var reports = await context.Arch7bExecutionReports.AsNoTracking()
            .Where(value => otherRunIds.Contains(value.QualificationRunId))
            .ToArrayAsync(cancellationToken);
        var unresolved = sends.Count(send =>
        {
            var latest = reports
                .Where(value =>
                    value.QualificationRunId == send.QualificationRunId &&
                    (value.ClientOrderId == send.ClientOrderId ||
                     value.OriginalClientOrderId == send.ClientOrderId))
                .OrderBy(value => value.TransactTimeUtc)
                .ThenBy(value => value.FixSequenceNumber)
                .LastOrDefault();
            return latest is null || latest.OrderStatus is not ("2" or "4" or "8" or "C");
        });
        var routedShadowOrders = await context.ShadowChildOrders.AsNoTracking()
            .CountAsync(value => value.BrokerSendAllowed, cancellationToken);
        return unresolved + routedShadowOrders;
    }

    private static async Task<IReadOnlyList<Arch7bIntradayMarketObservationReadRow>>
        ReadCanonicalMarketObservationsAsync(
            PmsShadowDbContext context,
            Guid economicRevisionId,
            CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
            await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = CanonicalMarketObservationSql;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "economic_revision_id";
            parameter.Value = economicRevisionId;
            command.Parameters.Add(parameter);

            var rows = new List<Arch7bIntradayMarketObservationReadRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new(
                    reader.GetGuid(0),
                    reader.GetInt32(1),
                    reader.GetString(2),
                    reader.GetGuid(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.GetBoolean(8),
                    reader.GetBoolean(9),
                    reader.GetFieldValue<DateTimeOffset>(10),
                    reader.GetFieldValue<DateTimeOffset>(11),
                    reader.GetString(12),
                    reader.GetBoolean(13),
                    reader.GetGuid(14),
                    reader.GetString(15),
                    reader.GetString(16),
                    reader.GetString(17),
                    reader.GetDecimal(18),
                    reader.GetDecimal(19),
                    reader.GetFieldValue<DateTimeOffset>(20),
                    reader.GetString(21),
                    reader.GetString(22)));
            }
            return rows;
        }
        finally
        {
            if (closeConnection)
                await connection.CloseAsync();
        }
    }

    private static bool IsSha256(string value)
        => value.Length == 64 && value.All(Uri.IsHexDigit);
}
