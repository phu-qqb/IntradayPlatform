using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tools.OperationalReporting;

public sealed class PmsShadowReadOnlyReportingReader(
    DbContextOptions<PmsShadowDbContext> options,
    PmsShadowPostgreSqlTarget target)
{
    private static readonly JsonSerializerOptions ProjectionJson =
        new(JsonSerializerDefaults.Web);

    public async Task<OperationalReportingSnapshot> ReadAsync(
        DateTimeOffset asOfUtc,
        string repositoryCommit,
        int includeHistory = 64,
        CancellationToken cancellationToken = default)
    {
        Require(asOfUtc.Offset == TimeSpan.Zero, "REPORTING_AS_OF_NOT_UTC");
        Require(IsGitCommit(repositoryCommit), "REPORTING_REPOSITORY_COMMIT_INVALID");
        Require(includeHistory is >= 1 and <= 1000, "REPORTING_INCLUDE_HISTORY_OUT_OF_RANGE");

        await using var context = new PmsShadowDbContext(options);
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        var pendingModelChanges = context.Database.HasPendingModelChanges();
        await context.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            await ExecuteAsync(context.Database.GetDbConnection(), transaction.GetDbTransaction(),
                "SET TRANSACTION READ ONLY", cancellationToken);
            var readOnly = await ScalarStringAsync(context.Database.GetDbConnection(),
                transaction.GetDbTransaction(), "SHOW transaction_read_only", cancellationToken);
            Require(readOnly == "on", "REPORTING_TRANSACTION_NOT_READ_ONLY");

            var database = await ReadDatabaseIdentityAsync(
                context, transaction.GetDbTransaction(), pendingModelChanges, cancellationToken);
            var ingestions = await context.Ingestions.AsNoTracking().ToArrayAsync(cancellationToken);
            var qubes = await context.QubesInputSnapshots.AsNoTracking().ToArrayAsync(cancellationToken);
            var mappings = await context.SecurityMappings.AsNoTracking().ToArrayAsync(cancellationToken);
            var modelRows = await context.ModelRuns.AsNoTracking().ToArrayAsync(cancellationToken);
            var weights = await context.TargetWeights.AsNoTracking().ToArrayAsync(cancellationToken);
            var slotRows = (await ReadSlotsAsync(
                    context.Database.GetDbConnection(), transaction.GetDbTransaction(), cancellationToken))
                .OrderByDescending(value => value.SlotStartUtc)
                .ThenByDescending(value => value.SlotId, StringComparer.Ordinal)
                .Take(includeHistory)
                .OrderBy(value => value.SlotStartUtc)
                .ThenBy(value => value.SlotId, StringComparer.Ordinal)
                .ToArray();
            var includedSlotIds = slotRows.Select(value => value.SlotId).ToHashSet(StringComparer.Ordinal);
            var revisions = (await ReadEconomicRevisionsAsync(
                    context.Database.GetDbConnection(), transaction.GetDbTransaction(), cancellationToken))
                .Where(value => includedSlotIds.Contains(value.SlotId)).ToArray();
            var intents = await context.ShadowTradeIntents.AsNoTracking().ToArrayAsync(cancellationToken);
            var risks = await context.ShadowRiskDecisions.AsNoTracking().ToArrayAsync(cancellationToken);
            var parents = await context.ShadowParentOrders.AsNoTracking().ToArrayAsync(cancellationToken);
            var children = await context.ShadowChildOrders.AsNoTracking().ToArrayAsync(cancellationToken);
            var arch7aRuns = await context.ShadowExecutionQualificationRuns.AsNoTracking()
                .ToArrayAsync(cancellationToken);
            var qualificationRuns = await context.Arch7bQualificationRuns.AsNoTracking()
                .ToArrayAsync(cancellationToken);
            var fixEvents = await context.Arch7bFixSessionEvents.AsNoTracking()
                .ToArrayAsync(cancellationToken);
            var sends = await context.Arch7bOrderSendLedger.AsNoTracking()
                .ToArrayAsync(cancellationToken);
            var executionReports = await context.Arch7bExecutionReports.AsNoTracking()
                .ToArrayAsync(cancellationToken);
            var fills = await context.Arch7bFills.AsNoTracking().ToArrayAsync(cancellationToken);
            var ledger = await context.Arch7bPositionLedgerEvents.AsNoTracking()
                .ToArrayAsync(cancellationToken);
            var reconciliations = await context.Arch7bFinalReconciliations.AsNoTracking()
                .ToArrayAsync(cancellationToken);

            var reportingRevisions = revisions.Select(ProjectRevision).ToArray();
            var reportingSlots = slotRows.Select(row => ProjectSlot(
                row,
                revisions.Where(value => value.SlotId == row.SlotId)
                    .OrderByDescending(value => value.RevisionNumber).FirstOrDefault())).ToArray();
            var reportingModels = ProjectModelRuns(revisions, modelRows, weights, qubes, asOfUtc);
            var reportingFxLines = ProjectFxLines(revisions, mappings, intents, asOfUtc);
            var reportingArch7a = ProjectArch7a(
                intents, risks, parents, children, mappings, arch7aRuns);
            var reportingArch7b = ProjectArch7b(
                qualificationRuns, fixEvents, sends, executionReports, fills, ledger, reconciliations);
            var observedCodes = CollectObservedCodes(slotRows, risks, reconciliations);

            await transaction.CommitAsync(cancellationToken);
            return new(
                asOfUtc,
                repositoryCommit,
                database,
                reportingSlots,
                reportingModels,
                reportingRevisions,
                reportingFxLines,
                reportingArch7a,
                reportingArch7b,
                observedCodes);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private async Task<ReportingDatabaseIdentity> ReadDatabaseIdentityAsync(
        PmsShadowDbContext context,
        DbTransaction transaction,
        bool pendingModelChanges,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var database = await ScalarStringAsync(
            connection, transaction, "SELECT current_database()", cancellationToken);
        var version = await ScalarStringAsync(
            connection, transaction, "SELECT version()", cancellationToken);
        var versionNumber = int.Parse(await ScalarStringAsync(connection, transaction,
            "SHOW server_version_num", cancellationToken), CultureInfo.InvariantCulture);
        var major = versionNumber / 10000;
        var tables = await ReadStringsAsync(connection, transaction, """
            SELECT table_name
              FROM information_schema.tables
             WHERE table_schema = 'pms_shadow' AND table_type = 'BASE TABLE'
             ORDER BY table_name
            """, cancellationToken);
        long rowCount = 0;
        foreach (var table in tables)
            rowCount += await ScalarLongAsync(connection, transaction,
                $"SELECT count(*) FROM pms_shadow.\"{table.Replace("\"", "\"\"")}\"",
                cancellationToken);
        var migrations = (await context.Database.GetAppliedMigrationsAsync(cancellationToken))
            .Order(StringComparer.Ordinal).ToArray();
        return new(
            database,
            version,
            major,
            PmsShadowStateContract.SchemaName,
            tables.Count,
            rowCount,
            migrations,
            true,
            pendingModelChanges,
            target.TargetProfileId,
            target.TargetFingerprint,
            target.TargetKind,
            target.TlsPolicy);
    }

    private static async Task<IReadOnlyList<PmsShadowIntradaySlotRow>> ReadSlotsAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, """
            SELECT slot_id, slot_start_utc, slot_end_utc, operational_date, status,
                   contract_version, cadence_mode, coordinator_id, claimed_at_utc,
                   completed_at_utc, manifest_json::text, manifest_sha256, ingestion_id,
                   source_session_id, failure_code, no_order
              FROM pms_shadow.intraday_slots
             ORDER BY slot_start_utc, slot_id
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<PmsShadowIntradaySlotRow>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(
                reader.GetString(0),
                reader.GetFieldValue<DateTimeOffset>(1),
                reader.GetFieldValue<DateTimeOffset>(2),
                reader.GetFieldValue<DateOnly>(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetFieldValue<DateTimeOffset>(8),
                reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetGuid(12),
                reader.IsDBNull(13) ? null : reader.GetString(13),
                reader.IsDBNull(14) ? null : reader.GetString(14),
                reader.GetBoolean(15)));
        return result;
    }

    private static async Task<IReadOnlyList<PmsShadowIntradayEconomicProjection>>
        ReadEconomicRevisionsAsync(
            DbConnection connection,
            DbTransaction transaction,
            CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, """
            SELECT projection_json::text
              FROM pms_shadow.intraday_projection_revisions
             ORDER BY completed_at_utc, projection_revision_id
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<PmsShadowIntradayEconomicProjection>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var projection = JsonSerializer.Deserialize<PmsShadowIntradayEconomicProjection>(
                reader.GetString(0), ProjectionJson)
                ?? throw new InvalidDataException("REPORTING_ECONOMIC_PROJECTION_JSON_INVALID");
            result.Add(projection);
        }
        return result;
    }

    private static ReportingEconomicRevisionFact ProjectRevision(
        PmsShadowIntradayEconomicProjection value)
        => new(
            value.ProjectionRevisionId,
            value.RevisionNumber,
            value.SlotId,
            value.SourceIngestionId,
            value.SourceSessionId,
            value.MarketDataSnapshotSha256,
            value.SupersedesSlotManifestSha256,
            value.Status,
            value.Qualifying,
            value.NoOrder,
            value.MarketData.Count,
            value.TargetPositions.Count,
            value.PositionOnlyDrifts.Count,
            value.SelectedModelRuns.Count,
            value.TargetPositionsSha256,
            value.DriftsSha256,
            value.ManifestSha256,
            value.CompletedAtUtc);

    private static ReportingSlotFact ProjectSlot(
        PmsShadowIntradaySlotRow row,
        PmsShadowIntradayEconomicProjection? revision)
    {
        JsonElement? manifest = null;
        if (!string.IsNullOrWhiteSpace(row.ManifestJson))
        {
            using var document = JsonDocument.Parse(row.ManifestJson);
            manifest = document.RootElement.Clone();
        }

        var artifactSha = JsonString(manifest, "LmaxCaptureSha256", "lmax_capture_sha256");
        var clock = JsonString(manifest, "ClockPreflightStatus", "clock_preflight_status");
        var polygon = JsonInt(manifest, "PolygonCallCount", "polygon_call_count");
        var finalizedAt = JsonDate(manifest, "FinalizedAtUtc", "finalized_at_utc");
        var handoffSha = JsonString(manifest, "HandoffSha256", "handoff_sha256");
        return new(
            row.SlotId,
            row.SlotStartUtc,
            row.SlotEndUtc,
            row.Status,
            row.ClaimedAtUtc,
            row.CompletedAtUtc,
            row.SourceSessionId,
            artifactSha,
            clock is null ? ReportingAuthority.Absent :
                clock == "PASS" ? ReportingAuthority.Proven : ReportingAuthority.Unknown,
            JsonInt(manifest, "BboCoverageCount", "bbo_coverage_count"),
            JsonInt(manifest, "InSlotEventCount", "in_slot_event_count"),
            JsonInt(manifest, "PostCloseExclusionCount", "post_close_exclusion_count"),
            polygon,
            handoffSha is null ? ReportingAuthority.Absent : ReportingAuthority.Proven,
            null,
            row.CompletedAtUtc.HasValue
                ? (row.CompletedAtUtc.Value - row.SlotEndUtc).TotalSeconds
                : finalizedAt.HasValue
                    ? (finalizedAt.Value - row.SlotEndUtc).TotalSeconds
                    : null,
            revision?.RevisionNumber,
            revision?.Qualifying,
            row.NoOrder,
            row.ManifestSha256,
            row.FailureCode,
            row.ContractVersion);
    }

    private static IReadOnlyList<ReportingModelRunFact> ProjectModelRuns(
        IReadOnlyList<PmsShadowIntradayEconomicProjection> revisions,
        IReadOnlyList<PmsShadowModelRunRow> modelRows,
        IReadOnlyList<PmsShadowTargetWeightRow> weights,
        IReadOnlyList<PmsShadowQubesInputSnapshotRow> qubes,
        DateTimeOffset asOfUtc)
    {
        var latest = revisions.Where(value => value.Qualifying)
            .OrderByDescending(value => value.CompletedAtUtc)
            .ThenByDescending(value => value.ProjectionRevisionId)
            .FirstOrDefault();
        var targetCountByModel = latest?.TargetPositions
            .GroupBy(value => value.ModelRunId)
            .ToDictionary(group => group.Key, group => group.Count()) ?? [];
        var driftCountByModel = latest?.PositionOnlyDrifts
            .GroupBy(value => value.ModelRunId)
            .ToDictionary(group => group.Key, group => group.Count()) ?? [];
        var selectedById = latest?.SelectedModelRuns.ToDictionary(value => value.ModelRunId) ?? [];
        var qubesIds = qubes.Select(value => value.SnapshotId).ToHashSet();
        return modelRows
            .Where(value => OperationalReportingContract.Strategies.Contains(
                value.StrategyId, StringComparer.Ordinal))
            .Select(value =>
            {
                var selected = selectedById.GetValueOrDefault(value.ModelRunId);
                var weightCount = weights.Count(item => item.ModelRunId == value.ModelRunId);
                var lineage = IsSha(value.OutputSha256) &&
                              IsGitCommit(value.CoreMasterCommitId) &&
                              qubesIds.Contains(value.QubesInputSnapshotId);
                return new ReportingModelRunFact(
                    value.StrategyId,
                    value.ModelRunId,
                    value.QubesInputSnapshotId,
                    value.TargetCloseUtc,
                    value.AsOfUtc,
                    value.OutputSha256,
                    value.CoreMasterCommitId,
                    selected?.Classification ?? value.Classification,
                    selected is null ? "FINALIZED_NOT_SELECTED" : selected.Classification,
                    value.TargetCloseUtc > asOfUtc ? "NOT_DUE" : "DUE_OR_FINALIZED",
                    weightCount,
                    targetCountByModel.GetValueOrDefault(value.ModelRunId),
                    driftCountByModel.GetValueOrDefault(value.ModelRunId),
                    lineage,
                    value.ContractVersion);
            })
            .ToArray();
    }

    private static IReadOnlyList<ReportingFxLineFact> ProjectFxLines(
        IReadOnlyList<PmsShadowIntradayEconomicProjection> revisions,
        IReadOnlyList<PmsShadowSecurityMappingRow> mappings,
        IReadOnlyList<PmsShadowTradeIntentRow> intents,
        DateTimeOffset asOfUtc)
    {
        var latest = revisions.Where(value => value.Qualifying)
            .OrderByDescending(value => value.CompletedAtUtc)
            .ThenByDescending(value => value.ProjectionRevisionId)
            .FirstOrDefault();
        if (latest is null) return [];
        var sourceMappings = mappings
            .Where(value => value.IngestionId == latest.SourceIngestionId)
            .ToArray();
        var mappingByInstrument = sourceMappings.ToDictionary(value => value.InstrumentId);
        var mappingByLmax = sourceMappings
            .GroupBy(value => value.LmaxInstrumentId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var observationByInstrument = latest.MarketData.ToDictionary(value => value.InstrumentId);
        var executionIntents = intents
            .Where(value => value.EconomicRevisionId == latest.ProjectionRevisionId)
            .OrderBy(value => value.ExecutionTradableSymbol, StringComparer.Ordinal)
            .ToArray();
        var result = new List<ReportingFxLineFact>(
            executionIntents.Length * OperationalReportingContract.Strategies.Length);
        foreach (var intent in executionIntents)
        {
            var mapping = mappingByLmax.GetValueOrDefault(intent.SecurityId);
            var observation = mapping is null
                ? latest.MarketData.SingleOrDefault(value =>
                    NormalizeSymbol(value.Symbol) ==
                    NormalizeSymbol(intent.ExecutionTradableSymbol))
                : observationByInstrument.GetValueOrDefault(mapping.InstrumentId);
            var targetIds = DeserializeGuids(intent.TargetPositionIdsJson).ToHashSet();
            var sourceTargets = latest.TargetPositions
                .Where(value => targetIds.Contains(value.TargetPositionId)).ToArray();
            var portfolioSymbol = NormalizeSymbol(intent.NormalizedPortfolioSymbol);
            Require(portfolioSymbol.Length == 6, "ARCH7A_PORTFOLIO_SYMBOL_INVALID");
            var portfolioCurrency = portfolioSymbol[..3];
            var contributionByStrategy = sourceTargets
                .GroupBy(value => value.StrategyId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(value => CurrencyContributionUsd(
                        value, portfolioCurrency, mappingByInstrument)),
                    StringComparer.Ordinal);
            var contributionTotal = contributionByStrategy.Values.Sum();
            Require(contributionTotal != 0m, "ARCH7A_EXECUTION_CONTRIBUTION_TOTAL_ZERO");
            var allocated = 0m;
            for (var index = 0; index < OperationalReportingContract.Strategies.Length; index++)
            {
                var strategy = OperationalReportingContract.Strategies[index];
                var target = index == OperationalReportingContract.Strategies.Length - 1
                    ? intent.TargetQuantity - allocated
                    : intent.TargetQuantity *
                      contributionByStrategy.GetValueOrDefault(strategy) / contributionTotal;
                allocated += target;
                result.Add(new(
                    latest.ProjectionRevisionId,
                    mapping?.InstrumentId ?? Guid.Empty,
                    mapping?.SecurityId ?? string.Empty,
                    NormalizeSymbol(intent.ExecutionTradableSymbol),
                    intent.SecurityId,
                    intent.SecurityIdSource,
                    strategy,
                    target,
                    target,
                    intent.CurrentQuantity,
                    intent.CurrentQuantity == 0m ? target : null,
                    intent.TargetQuantity,
                    intent.SignedDesiredDelta,
                    mapping is null ? ReportingAuthority.Absent : ReportingAuthority.Proven,
                    observation?.Bid,
                    observation?.Ask,
                    observation?.EventTimeUtc,
                    observation is null ? ReportingAuthority.Absent :
                        asOfUtc - observation.EventTimeUtc >
                        TimeSpan.FromMinutes(PmsShadowIntradayCadenceContract.StaleMinutes)
                            ? ReportingAuthority.Stale : ReportingAuthority.Proven));
            }
        }
        return result;
    }

    private static IReadOnlyList<Guid> DeserializeGuids(string json)
        => JsonSerializer.Deserialize<Guid[]>(json, ProjectionJson)
           ?? throw new InvalidDataException("REPORTING_ID_LIST_JSON_INVALID");

    private static decimal CurrencyContributionUsd(
        PmsShadowSlotTargetPosition target,
        string portfolioCurrency,
        IReadOnlyDictionary<Guid, PmsShadowSecurityMappingRow> mappingByInstrument)
    {
        var mapping = mappingByInstrument.GetValueOrDefault(target.InstrumentId)
            ?? throw new InvalidDataException("SOURCE_SECURITY_MAPPING_INCOMPLETE");
        var symbol = NormalizeSymbol(mapping.Symbol);
        Require(symbol.Length == 6, "ARCH7A_PORTFOLIO_SYMBOL_INVALID");
        var contribution = 0m;
        if (symbol[..3] == portfolioCurrency)
            contribution += target.TargetNotionalUsd;
        if (symbol[3..] == portfolioCurrency)
            contribution -= target.TargetNotionalUsd;
        return contribution;
    }

    private static IReadOnlyList<ReportingArch7aFact> ProjectArch7a(
        IReadOnlyList<PmsShadowTradeIntentRow> intents,
        IReadOnlyList<PmsShadowRiskDecisionRow> risks,
        IReadOnlyList<PmsShadowParentOrderRow> parents,
        IReadOnlyList<PmsShadowChildOrderRow> children,
        IReadOnlyList<PmsShadowSecurityMappingRow> mappings,
        IReadOnlyList<PmsShadowExecutionQualificationRunRow> runs)
    {
        var riskByIntent = risks.ToDictionary(value => value.TradeIntentId);
        var parentByIntent = parents.ToDictionary(value => value.TradeIntentId);
        var childByParent = children.ToDictionary(value => value.ParentOrderId);
        var mappingBySource = mappings.GroupBy(value => (value.IngestionId, value.LmaxInstrumentId))
            .ToDictionary(group => group.Key, group => group.First());
        var runIds = runs.Select(value => value.EconomicRevisionId).ToHashSet();
        return intents.Select(intent =>
        {
            var risk = riskByIntent.GetValueOrDefault(intent.TradeIntentId)
                ?? throw new InvalidDataException("ARCH7A_QUALIFYING_REVISION_FACTS_INCOMPLETE");
            var parent = parentByIntent.GetValueOrDefault(intent.TradeIntentId)
                ?? throw new InvalidDataException("ARCH7A_QUALIFYING_REVISION_FACTS_INCOMPLETE");
            var child = childByParent.GetValueOrDefault(parent.ParentOrderId)
                ?? throw new InvalidDataException("ARCH7A_QUALIFYING_REVISION_FACTS_INCOMPLETE");
            var mapping = mappingBySource.GetValueOrDefault((intent.IngestionId, intent.SecurityId));
            return new ReportingArch7aFact(
                intent.EconomicRevisionId,
                intent.TradeIntentId,
                risk.RiskDecisionId,
                parent.ParentOrderId,
                child.ChildOrderId,
                intent.AccountScope,
                intent.Environment,
                intent.Classification,
                parent.Status,
                child.Status,
                intent.Actionable,
                intent.ExecutionAllowed,
                parent.RouteAllowed,
                child.BrokerSendAllowed,
                intent.PlanSha256,
                runIds.Contains(intent.EconomicRevisionId)
                    ? "QUALIFICATION_RUN_PRESENT" : "QUALIFICATION_RUN_ABSENT",
                NormalizeSymbol(parent.Symbol),
                mapping?.InstrumentId ?? Guid.Empty);
        }).ToArray();
    }

    private static IReadOnlyList<ReportingArch7bFact> ProjectArch7b(
        IReadOnlyList<PmsArch7bQualificationRunRow> runs,
        IReadOnlyList<PmsArch7bFixSessionEventRow> fixEvents,
        IReadOnlyList<PmsArch7bOrderSendLedgerRow> sends,
        IReadOnlyList<PmsArch7bExecutionReportRow> reports,
        IReadOnlyList<PmsArch7bFillRow> fills,
        IReadOnlyList<PmsArch7bPositionLedgerEventRow> ledger,
        IReadOnlyList<PmsArch7bFinalReconciliationRow> reconciliations)
    {
        if (runs.Count == 0)
            return [new(null, ReportingAuthority.Absent, ReportingAuthority.Absent,
                null, null, 0, 0, 0, 0, 0, 0, null, null, null, null,
                ReportingAuthority.Absent, null)];

        return runs.Select(run =>
        {
            var reconciliation = reconciliations
                .Where(value => value.QualificationRunId == run.QualificationRunId)
                .OrderByDescending(value => value.CompletedAtUtc)
                .ThenByDescending(value => value.ReconciliationId)
                .FirstOrDefault();
            return new ReportingArch7bFact(
                run.QualificationRunId,
                reconciliation?.Status ?? "LIFECYCLE_REGISTERED",
                ReportingAuthority.Proven,
                run.AuthorizationPacketSha256,
                run.LeaseExpiresAtUtc,
                fixEvents.Count(value => value.QualificationRunId == run.QualificationRunId),
                sends.Count(value => value.QualificationRunId == run.QualificationRunId),
                reports.Count(value => value.QualificationRunId == run.QualificationRunId),
                fills.Count(value => value.QualificationRunId == run.QualificationRunId),
                ledger.Count(value => value.QualificationRunId == run.QualificationRunId),
                reconciliations.Count(value => value.QualificationRunId == run.QualificationRunId),
                reconciliation?.KnownWorkingLeaves,
                reconciliation?.InternalLedgerQuantity,
                reconciliation?.BrokerResidualQuantity,
                reconciliation?.CriticalBreakCount,
                reconciliation?.Status == "FLAT_RECONCILED"
                    ? "GO_ARCH7B_KNOWN_ORDER_LIFECYCLE_FLAT"
                    : ReportingAuthority.Unknown,
                reconciliation?.CompletedAtUtc);
        }).ToArray();
    }

    private static IReadOnlyList<string> CollectObservedCodes(
        IReadOnlyList<PmsShadowIntradaySlotRow> slots,
        IReadOnlyList<PmsShadowRiskDecisionRow> risks,
        IReadOnlyList<PmsArch7bFinalReconciliationRow> reconciliations)
    {
        var result = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var value in slots.Select(value => value.FailureCode))
            AddCode(result, value);
        foreach (var value in risks.SelectMany(value =>
                     new[] { value.ReasonCodesJson, value.BlockingBreaksJson }))
            AddJsonCodes(result, value);
        foreach (var value in reconciliations.Select(value => value.BreaksJson))
            AddJsonCodes(result, value);
        return result.ToArray();
    }

    private static void AddJsonCodes(ISet<string> result, string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            foreach (var text in EnumerateStrings(document.RootElement))
                AddCode(result, text);
        }
        catch (JsonException)
        {
            AddCode(result, value);
        }
    }

    private static IEnumerable<string> EnumerateStrings(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            yield return value.GetString()!;
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray())
                foreach (var text in EnumerateStrings(item))
                    yield return text;
        else if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject())
                foreach (var text in EnumerateStrings(property.Value))
                    yield return text;
    }

    private static void AddCode(ISet<string> result, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var normalized = value.Trim();
        if (normalized.Length >= 5 &&
            normalized.All(character =>
                char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character) ||
                character is '_' or ':' or '-'))
            result.Add(normalized.TrimEnd(':'));
    }

    private static string? JsonString(JsonElement? root, params string[] names)
    {
        var property = JsonProperty(root, names);
        return property is { ValueKind: JsonValueKind.String } ? property.Value.GetString() : null;
    }

    private static int? JsonInt(JsonElement? root, params string[] names)
    {
        var property = JsonProperty(root, names);
        return property is { ValueKind: JsonValueKind.Number } && property.Value.TryGetInt32(out var value)
            ? value : null;
    }

    private static DateTimeOffset? JsonDate(JsonElement? root, params string[] names)
    {
        var property = JsonProperty(root, names);
        return property is { ValueKind: JsonValueKind.String } &&
               property.Value.TryGetDateTimeOffset(out var value) ? value : null;
    }

    private static JsonElement? JsonProperty(JsonElement? root, params string[] names)
    {
        if (root is null || root.Value.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in root.Value.EnumerateObject())
            if (names.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                return property.Value;
        return null;
    }

    private static string NormalizeSymbol(string value)
        => new(value.Where(char.IsAsciiLetterOrDigit)
            .Select(char.ToUpperInvariant).ToArray());

    private static bool IsSha(string? value)
        => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

    private static bool IsGitCommit(string? value)
        => value is not null &&
           value.Length is 40 or 64 &&
           value.All(character => char.IsAsciiHexDigit(character) && !char.IsUpper(character));

    private static async Task ExecuteAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, sql);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string> ScalarStringAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, sql);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken),
                   CultureInfo.InvariantCulture)
               ?? throw new InvalidDataException("REPORTING_DATABASE_VALUE_MISSING");
    }

    private static async Task<long> ScalarLongAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, sql);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<string>> ReadStringsAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var values = new List<string>();
        while (await reader.ReadAsync(cancellationToken)) values.Add(reader.GetString(0));
        return values;
    }

    private static DbCommand Command(
        DbConnection connection,
        DbTransaction transaction,
        string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
