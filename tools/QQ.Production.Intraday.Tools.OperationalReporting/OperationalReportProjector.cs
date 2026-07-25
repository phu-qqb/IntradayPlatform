using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class OperationalReportProjector
{
    public static OperationalReportSet Build(OperationalReportingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        RequireUtc(snapshot.AsOfUtc);

        var breaks = BuildBreaks(snapshot)
            .OrderByDescending(value => value.Severity)
            .ThenBy(value => value.Category, StringComparer.Ordinal)
            .ThenBy(value => value.ExactCode, StringComparer.Ordinal)
            .ThenBy(value => value.ScopeId, StringComparer.Ordinal)
            .ThenBy(value => value.BreakId, StringComparer.Ordinal)
            .ToArray();
        var active = breaks.Where(value =>
            value.Status is OperationalBreakStatus.Active or OperationalBreakStatus.Unknown).ToArray();
        var latestRevision = snapshot.EconomicRevisions
            .Where(value => value.Qualifying)
            .OrderByDescending(value => value.CompletedAtUtc)
            .ThenByDescending(value => value.EconomicRevisionId)
            .FirstOrDefault();
        var latestArch7a = snapshot.Arch7a
            .OrderByDescending(value => value.EconomicRevisionId)
            .ThenByDescending(value => value.TradeIntentId)
            .FirstOrDefault();
        var latestArch7b = snapshot.Arch7b
            .Where(value => value.QualificationRunId.HasValue)
            .OrderByDescending(value => value.CompletedAtUtc)
            .ThenByDescending(value => value.QualificationRunId)
            .FirstOrDefault();
        var latestSlot = snapshot.Slots
            .OrderByDescending(value => value.SlotEndUtc)
            .ThenByDescending(value => value.SlotId, StringComparer.Ordinal)
            .FirstOrDefault();
        var latestModels = snapshot.ModelRuns
            .Where(value => OperationalReportingContract.Strategies.Contains(
                value.StrategyId, StringComparer.Ordinal))
            .GroupBy(value => value.StrategyId, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(value => value.TargetCloseUtc)
                .ThenByDescending(value => value.ModelRunId)
                .First())
            .OrderBy(value => value.StrategyId, StringComparer.Ordinal)
            .Select(value => $"{value.StrategyId}:{value.ModelRunId:D}")
            .ToArray();
        var authorityGaps = active
            .Where(value => value.AuthorityStatus != ReportingAuthority.Proven)
            .Select(value => value.ExactCode)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var activeBySeverity = Enum.GetValues<OperationalBreakSeverity>()
            .ToDictionary(value => value.ToString().ToUpperInvariant(),
                value => active.Count(item => item.Severity == value),
                StringComparer.Ordinal);
        var highest = active.Select(value => value.Severity).DefaultIfEmpty().Max();
        var globalStatus = active.Length == 0
            ? ReportingAuthority.Proven
            : highest switch
            {
                OperationalBreakSeverity.Critical => "CRITICAL",
                OperationalBreakSeverity.Error => "ERROR",
                OperationalBreakSeverity.Warning => "WARNING",
                _ => ReportingAuthority.Probable
            };
        var tradingReadiness = active.Any(value => value.BlocksTrading)
            ? "BLOCKED"
            : latestArch7a is null
                ? ReportingAuthority.Absent
                : latestArch7a.ExecutionAllowed && latestArch7a.BrokerSendAllowed
                    ? "AUTHORIZED_FACT_PRESENT"
                    : "SHADOW_ONLY";
        var reconciliation = BuildReconciliation(latestArch7b);
        var freshness = latestSlot is null
            ? ReportingAuthority.Absent
            : snapshot.AsOfUtc - latestSlot.SlotEndUtc >
              TimeSpan.FromMinutes(PmsShadowIntradayCadenceContract.StaleMinutes)
                ? ReportingAuthority.Stale
                : ReportingAuthority.Proven;
        var migrationIdentity = string.Join(',', snapshot.Database.AppliedMigrations);
        var summary = new OperationalSummary(
            snapshot.AsOfUtc,
            snapshot.RepositoryCommit,
            snapshot.Database.TargetProfileId,
            snapshot.Database.TargetFingerprint,
            snapshot.Database.Database,
            migrationIdentity,
            latestSlot?.SlotId,
            latestRevision?.EconomicRevisionId,
            latestModels,
            latestArch7a?.EconomicRevisionId,
            latestArch7b?.QualificationRunId,
            activeBySeverity,
            globalStatus,
            tradingReadiness,
            reconciliation.Status,
            freshness,
            authorityGaps);
        return new(
            summary,
            OperationalStatusCodeCatalog.All,
            breaks,
            snapshot.ModelRuns.OrderBy(value => value.StrategyId, StringComparer.Ordinal)
                .ThenBy(value => value.TargetCloseUtc).ThenBy(value => value.ModelRunId).ToArray(),
            snapshot.Slots.OrderBy(value => value.SlotStartUtc)
                .ThenBy(value => value.SlotId, StringComparer.Ordinal).ToArray(),
            snapshot.EconomicRevisions.OrderBy(value => value.CompletedAtUtc)
                .ThenBy(value => value.EconomicRevisionId).ToArray(),
            snapshot.FxLines.OrderBy(value => value.CanonicalSymbol, StringComparer.Ordinal)
                .ThenBy(value => value.StrategyId, StringComparer.Ordinal).ToArray(),
            snapshot.Arch7a.OrderBy(value => value.EconomicRevisionId)
                .ThenBy(value => value.TradeIntentId).ToArray(),
            snapshot.Arch7b.OrderBy(value => value.QualificationRunId).ToArray(),
            reconciliation);
    }

    public static string BreakId(
        string exactCode,
        string component,
        string scopeType,
        string scopeId,
        string? slotId = null,
        Guid? economicRevisionId = null,
        Guid? tradeIntentId = null,
        Guid? qualificationRunId = null,
        string? orderId = null,
        string? evidenceSha256 = null)
    {
        var material = string.Join('\n',
            $"contract={OperationalReportingContract.BreakVersion}",
            $"code={exactCode}",
            $"component={component}",
            $"scope_type={scopeType}",
            $"scope_id={scopeId}",
            $"slot_id={slotId ?? string.Empty}",
            $"economic_revision_id={economicRevisionId?.ToString("D") ?? string.Empty}",
            $"trade_intent_id={tradeIntentId?.ToString("D") ?? string.Empty}",
            $"qualification_run_id={qualificationRunId?.ToString("D") ?? string.Empty}",
            $"order_id={orderId ?? string.Empty}",
            $"evidence_sha256={evidenceSha256 ?? string.Empty}");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static IEnumerable<OperationalBreak> BuildBreaks(OperationalReportingSnapshot snapshot)
    {
        var latestSlot = snapshot.Slots
            .OrderByDescending(value => value.SlotEndUtc)
            .ThenByDescending(value => value.SlotId, StringComparer.Ordinal)
            .FirstOrDefault();

        foreach (var slot in snapshot.Slots)
        {
            var historical = latestSlot is not null && slot.SlotEndUtc < latestSlot.SlotEndUtc;
            var status = historical ? OperationalBreakStatus.Historical : OperationalBreakStatus.Active;
            if (slot.Status == "MISSED")
                yield return Create("INTRADAY_SLOT_MISSING", "SLOT", "Slot", slot.SlotId,
                    slot.SlotEndUtc, slot.CompletedAtUtc ?? snapshot.AsOfUtc, status,
                    slot.ManifestSha256, slot.ClockAuthorityStatus, "pms_shadow.intraday_slots",
                    slot.ContractVersion, slotId: slot.SlotId);
            if (slot.Status == "FAILED_CLOSED")
                yield return Create(slot.FailureCode ?? "INTRADAY_SLOT_FAILED_CLOSED", "SLOT",
                    "Slot", slot.SlotId, slot.SlotEndUtc, slot.CompletedAtUtc ?? snapshot.AsOfUtc,
                    status, slot.ManifestSha256, ReportingAuthority.Proven,
                    "pms_shadow.intraday_slots", slot.ContractVersion, slotId: slot.SlotId);
            if (slot.Status == "COMPLETED" &&
                (slot.ManifestSha256 is null || !slot.NoOrder || slot.Qualifying == false))
                yield return Create("INTRADAY_SLOT_INCOMPLETE", "SLOT", "Slot", slot.SlotId,
                    slot.SlotEndUtc, slot.CompletedAtUtc ?? snapshot.AsOfUtc, status,
                    slot.ManifestSha256, slot.ManifestSha256 is null
                        ? ReportingAuthority.Absent : ReportingAuthority.Proven,
                    "pms_shadow.intraday_slots", slot.ContractVersion, slotId: slot.SlotId);
            if (slot.ClockAuthorityStatus is ReportingAuthority.Absent or ReportingAuthority.Unknown)
                yield return Create("ARCH7B_CAPTURE_HOST_CLOCK_NOT_QUALIFIED", "CLOCK", "Slot",
                    slot.SlotId, slot.SlotEndUtc, slot.CompletedAtUtc ?? snapshot.AsOfUtc,
                    historical ? OperationalBreakStatus.Historical : OperationalBreakStatus.Unknown,
                    slot.ManifestSha256, slot.ClockAuthorityStatus, "pms_shadow.intraday_slots",
                    slot.ContractVersion, slotId: slot.SlotId);
            if (slot.ImportStartLatencySeconds is > PmsShadowFreshSlotHandoffContract.AbsoluteStartDeadlineSeconds)
                yield return Create("HANDOFF_IMPORT_ABSOLUTE_DEADLINE_EXCEEDED", "HANDOFF",
                    "Slot", slot.SlotId, slot.SlotEndUtc, slot.CompletedAtUtc ?? snapshot.AsOfUtc,
                    status, slot.ManifestSha256, ReportingAuthority.Proven,
                    "pms_shadow.intraday_slots", slot.ContractVersion, slotId: slot.SlotId);
        }

        if (latestSlot is null)
            yield return Create("INTRADAY_SLOT_MISSING", "SLOT", "Database",
                snapshot.Database.Database, snapshot.AsOfUtc, snapshot.AsOfUtc,
                OperationalBreakStatus.Unknown, null, ReportingAuthority.Absent,
                "pms_shadow.intraday_slots", OperationalReportingContract.Version);
        else if (snapshot.AsOfUtc - latestSlot.SlotEndUtc >
                 TimeSpan.FromMinutes(PmsShadowIntradayCadenceContract.StaleMinutes))
            yield return Create("INTRADAY_SLOT_STALE", "SLOT", "Slot", latestSlot.SlotId,
                latestSlot.SlotEndUtc, snapshot.AsOfUtc, OperationalBreakStatus.Active,
                latestSlot.ManifestSha256, ReportingAuthority.Proven,
                "pms_shadow.intraday_slots", latestSlot.ContractVersion, slotId: latestSlot.SlotId);

        var latestRevision = snapshot.EconomicRevisions
            .Where(value => value.Qualifying)
            .OrderByDescending(value => value.CompletedAtUtc)
            .ThenByDescending(value => value.EconomicRevisionId)
            .FirstOrDefault();
        if (latestRevision is not null)
        {
            if (latestRevision.ObservationCount != OperationalReportingContract.ExpectedMarketObservationCount)
                yield return RevisionBreak("MARKET_DATA_OBSERVATION_COUNT_MISMATCH", latestRevision,
                    "pms_shadow.intraday_market_data_observations");
            if (latestRevision.TargetPositionCount != OperationalReportingContract.ExpectedTargetPositionCount)
                yield return RevisionBreak("TARGET_POSITION_COUNT_MISMATCH", latestRevision,
                    "pms_shadow.intraday_target_positions");
            if (latestRevision.PositionOnlyDriftCount !=
                OperationalReportingContract.ExpectedPositionOnlyDriftCount)
                yield return RevisionBreak("POSITION_ONLY_DRIFT_COUNT_MISMATCH", latestRevision,
                    "pms_shadow.intraday_position_only_drifts");
            if (latestRevision.ModelRunCount != OperationalReportingContract.ExpectedModelRunCount)
                yield return RevisionBreak("FOUR_REQUIRED_MODEL_RUNS_MISSING", latestRevision,
                    "pms_shadow.model_runs");
        }

        foreach (var model in snapshot.ModelRuns.Where(value => !value.LineageComplete))
            yield return Create("MODEL_RUN_LINEAGE_INCOMPLETE", "ANUBIS_INFX", "ModelRun",
                model.ModelRunId.ToString("D"), model.AsOfUtc, snapshot.AsOfUtc,
                OperationalBreakStatus.Active, model.OutputSha256, ReportingAuthority.Unknown,
                "pms_shadow.model_runs", model.SourceContractVersion, strategyId: model.StrategyId);

        var latestStrategies = snapshot.ModelRuns
            .GroupBy(value => value.StrategyId, StringComparer.Ordinal)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (OperationalReportingContract.Strategies.Any(value => !latestStrategies.Contains(value)))
            yield return Create("FOUR_REQUIRED_MODEL_RUNS_MISSING", "ANUBIS_INFX", "Database",
                snapshot.Database.Database, snapshot.AsOfUtc, snapshot.AsOfUtc,
                OperationalBreakStatus.Active, null, ReportingAuthority.Absent,
                "pms_shadow.model_runs", PmsShadowStateContract.ContractVersion);

        foreach (var item in snapshot.Arch7a.Where(value =>
                     value.Environment != OperationalReportingContract.TestEnvironment ||
                     value.AccountScope != OperationalReportingContract.RequiredAccountScope ||
                     value.Classification != OperationalReportingContract.ShadowClassification ||
                     value.BrokerRouteAllowed || value.BrokerSendAllowed))
            yield return Create("ARCH7A_NO_ORDER_INVARIANT_REQUIRED", "ARCH7A", "TradeIntent",
                item.TradeIntentId.ToString("D"), snapshot.AsOfUtc, snapshot.AsOfUtc,
                OperationalBreakStatus.Active, item.PlanSha256, ReportingAuthority.Proven,
                "pms_shadow.shadow_trade_intents", "arch7a_shadow_execution_v1",
                economicRevisionId: item.EconomicRevisionId, tradeIntentId: item.TradeIntentId);

        foreach (var lifecycle in snapshot.Arch7b.Where(value => value.QualificationRunId.HasValue))
        {
            if (lifecycle.ReconciliationCount == 0)
                yield return Create("ARCH7B_FLATTEN_NOT_CONFIRMED", "ARCH7B", "QualificationRun",
                    lifecycle.QualificationRunId!.Value.ToString("D"), snapshot.AsOfUtc, snapshot.AsOfUtc,
                    OperationalBreakStatus.Unknown, lifecycle.AuthorizationPacketSha256,
                    ReportingAuthority.Unknown, "pms_shadow.arch7b_qualification_runs",
                    "arch7b_known_order_lifecycle_v1",
                    qualificationRunId: lifecycle.QualificationRunId);
            if (lifecycle.KnownLeaves is not 0m || lifecycle.FinalLedgerQuantity is not 0m ||
                lifecycle.BrokerResidualQuantity is not 0m ||
                lifecycle.CriticalBreakCount is > 0)
                yield return Create("ARCH7B_FINAL_RECONCILIATION_NOT_FLAT", "RECONCILIATION",
                    "QualificationRun", lifecycle.QualificationRunId!.Value.ToString("D"),
                    lifecycle.CompletedAtUtc ?? snapshot.AsOfUtc, snapshot.AsOfUtc,
                    OperationalBreakStatus.Active, lifecycle.AuthorizationPacketSha256,
                    lifecycle.ReconciliationCount == 0 ? ReportingAuthority.Unknown : ReportingAuthority.Proven,
                    "pms_shadow.arch7b_final_reconciliations", "arch7b_known_order_lifecycle_v1",
                    qualificationRunId: lifecycle.QualificationRunId);
        }

        foreach (var code in snapshot.ObservedCodes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var cataloged = OperationalStatusCodeCatalog.All.Any(item => item.ExactCode == code);
            var exactCode = cataloged ? code : "REPORTING_UNCATALOGUED_SOURCE_CODE";
            yield return Create(exactCode, cataloged ? "SOURCE_FACT" : "REPORTING", "SourceCode",
                code, snapshot.AsOfUtc, snapshot.AsOfUtc,
                cataloged ? OperationalBreakStatus.Active : OperationalBreakStatus.Unknown,
                null, ReportingAuthority.Proven, "pms_shadow",
                OperationalReportingContract.Version);
        }
    }

    private static OperationalBreak RevisionBreak(
        string code,
        ReportingEconomicRevisionFact revision,
        string sourceTable)
        => Create(code, "ECONOMIC_PROJECTION", "EconomicRevision",
            revision.EconomicRevisionId.ToString("D"), revision.CompletedAtUtc,
            revision.CompletedAtUtc, OperationalBreakStatus.Active,
            revision.ManifestSha256, ReportingAuthority.Proven, sourceTable,
            OperationalReportingContract.Version, slotId: revision.SlotId,
            economicRevisionId: revision.EconomicRevisionId);

    private static OperationalBreak Create(
        string exactCode,
        string component,
        string scopeType,
        string scopeId,
        DateTimeOffset firstObservedAtUtc,
        DateTimeOffset lastObservedAtUtc,
        OperationalBreakStatus status,
        string? evidenceSha256,
        string authorityStatus,
        string sourceTable,
        string sourceContractVersion,
        string? slotId = null,
        string? strategyId = null,
        Guid? economicRevisionId = null,
        Guid? tradeIntentId = null,
        Guid? qualificationRunId = null,
        string? orderId = null)
    {
        var catalog = OperationalStatusCodeCatalog.Get(exactCode);
        var cataloged = catalog.ExactCode == exactCode;
        var effectiveCode = cataloged ? exactCode : "REPORTING_UNCATALOGUED_SOURCE_CODE";
        var id = BreakId(effectiveCode, component, scopeType, scopeId, slotId,
            economicRevisionId, tradeIntentId, qualificationRunId, orderId, evidenceSha256);
        return new(
            id,
            effectiveCode,
            catalog.Category,
            catalog.Severity,
            status,
            component,
            scopeType,
            scopeId,
            slotId,
            strategyId,
            economicRevisionId,
            null,
            null,
            tradeIntentId,
            qualificationRunId,
            orderId,
            firstObservedAtUtc,
            lastObservedAtUtc,
            evidenceSha256,
            authorityStatus,
            catalog.BlocksTrading,
            catalog.BlocksAccounting,
            catalog.OperatorMeaning,
            catalog.OperatorMeaning,
            sourceTable,
            sourceContractVersion);
    }

    private static ReconciliationReport BuildReconciliation(ReportingArch7bFact? latest)
    {
        if (latest is null)
            return new(ReportingAuthority.Absent, ReportingAuthority.Absent, null,
                null, null, null, 0, ReportingAuthority.Absent, null, null);
        var flat = latest.ReconciliationCount > 0 &&
                   latest.KnownLeaves == 0m &&
                   latest.FinalLedgerQuantity == 0m &&
                   latest.BrokerResidualQuantity == 0m &&
                   latest.CriticalBreakCount == 0;
        return new(flat ? "FLAT_RECONCILED" : latest.ReconciliationCount == 0
                ? ReportingAuthority.Unknown : "NOT_FLAT",
            latest.ReconciliationCount == 0 ? ReportingAuthority.Unknown : ReportingAuthority.Proven,
            latest.QualificationRunId,
            latest.KnownLeaves,
            latest.FinalLedgerQuantity,
            latest.BrokerResidualQuantity,
            latest.CriticalBreakCount ?? 0,
            latest.FinalGate,
            latest.AuthorizationPacketSha256,
            latest.CompletedAtUtc);
    }

    private static void RequireUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new InvalidDataException("REPORTING_AS_OF_NOT_UTC");
    }
}
