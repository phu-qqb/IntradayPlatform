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
        var expectation = ReportingOperationalCalendar.Project(snapshot.AsOfUtc, snapshot.Slots);
        var breaks = BuildBreaks(snapshot, expectation)
            .GroupBy(value => string.Join('|', value.ExactCode, value.SourceExactCode,
                value.ScopeType, value.ScopeId, value.FactKind), StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(value => value.Status == OperationalBreakStatus.Unknown)
                .ThenByDescending(value => value.LastObservedAtUtc)
                .First())
            .OrderByDescending(value => value.Severity)
            .ThenBy(value => value.Category, StringComparer.Ordinal)
            .ThenBy(value => value.ExactCode, StringComparer.Ordinal)
            .ThenBy(value => value.ScopeId, StringComparer.Ordinal)
            .ThenBy(value => value.BreakId, StringComparer.Ordinal)
            .ToArray();
        var active = breaks.Where(value =>
            value.Status is OperationalBreakStatus.Active or OperationalBreakStatus.Unknown).ToArray();
        var latestRevision = LatestRevision(snapshot);
        var latestArch7a = snapshot.Arch7a
            .Where(value =>
                value.EconomicRevisionId == latestRevision?.EconomicRevisionId &&
                value.IsAuthoritativeForEconomicRevision &&
                value.QualificationRunId.HasValue)
            .OrderByDescending(value => value.QualificationCompletedAtUtc)
            .ThenBy(value => value.QualificationRunId)
            .ThenBy(value => value.TradeIntentId)
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
            .OrderBy(value => Array.IndexOf(OperationalReportingContract.Strategies, value.StrategyId))
            .ThenBy(value => value.ModelRunId)
            .Select(value => $"{value.StrategyId}:{value.ModelRunId:D}")
            .ToArray();
        var authorityGaps = active
            .Where(value => value.AuthorityStatus != ReportingAuthority.Proven)
            .Select(value => value.SourceExactCode ?? value.ExactCode)
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
        var freshness = expectation.SlotDueStatus switch
        {
            ReportingSlotDueStatuses.Current => ReportingAuthority.Proven,
            ReportingSlotDueStatuses.OutsideOperationalCalendar =>
                ReportingSlotDueStatuses.OutsideOperationalCalendar,
            ReportingSlotDueStatuses.NotDue => ReportingSlotDueStatuses.NotDue,
            ReportingSlotDueStatuses.Due => ReportingSlotDueStatuses.Due,
            ReportingSlotDueStatuses.Missed => "MISSED",
            _ => ReportingAuthority.Stale
        };
        var summary = new OperationalSummary(
            snapshot.AsOfUtc,
            snapshot.RepositoryCommit,
            snapshot.Database.TargetProfileId,
            snapshot.Database.TargetFingerprint,
            snapshot.Database.Database,
            string.Join(',', snapshot.Database.AppliedMigrations),
            latestSlot?.SlotId,
            latestRevision?.EconomicRevisionId,
            latestModels,
            latestArch7a?.QualificationRunId,
            latestArch7b?.QualificationRunId,
            activeBySeverity,
            globalStatus,
            tradingReadiness,
            reconciliation.Status,
            freshness,
            authorityGaps)
        {
            LatestArch7aQualificationCompletedAtUtc =
                latestArch7a?.QualificationCompletedAtUtc,
            LatestArch7aQualificationStatus =
                latestArch7a?.QualificationRunStatus ?? ReportingAuthority.Absent,
            LatestArch7aEconomicRevisionId = latestArch7a?.EconomicRevisionId
        };
        return new(
            summary,
            OperationalStatusCodeCatalog.All,
            breaks,
            expectation,
            snapshot.ModelRuns.OrderBy(value => Array.IndexOf(OperationalReportingContract.Strategies, value.StrategyId))
                .ThenBy(value => value.ModelRunId).ToArray(),
            snapshot.Slots.OrderBy(value => value.SlotStartUtc)
                .ThenBy(value => value.SlotId, StringComparer.Ordinal).ToArray(),
            snapshot.EconomicRevisions.OrderBy(value => value.CompletedAtUtc)
                .ThenBy(value => value.EconomicRevisionId).ToArray(),
            snapshot.FxNetLines.OrderBy(value => value.CanonicalSymbol, StringComparer.Ordinal).ToArray(),
            snapshot.FxStrategyContributions
                .OrderBy(value => value.CanonicalSymbol, StringComparer.Ordinal)
                .ThenBy(value => value.StrategyId, StringComparer.Ordinal).ToArray(),
            snapshot.Arch7a.OrderBy(value => value.EconomicRevisionId)
                .ThenBy(value => value.TradeIntentId).ToArray(),
            snapshot.Arch7b.OrderBy(value => value.QualificationRunId).ToArray(),
            reconciliation,
            snapshot.ObservedCodeFacts.OrderBy(value => value.SourceTable, StringComparer.Ordinal)
                .ThenBy(value => value.ScopeId, StringComparer.Ordinal)
                .ThenBy(value => value.SourceExactCode, StringComparer.Ordinal).ToArray())
        {
            PositionMarketLineage = snapshot.PositionMarketLineage ??
                Arch7bPositionMarketReporting.Absent()
        };
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

    private static IEnumerable<OperationalBreak> BuildBreaks(
        OperationalReportingSnapshot snapshot,
        ReportingOperationalExpectation expectation)
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
                yield return Create("INTRADAY_SLOT_MISSING", slot.FailureCode,
                    OperationalFactKinds.SlotFailureCode, "SLOT", "Slot", slot.SlotId,
                    slot.SlotEndUtc, slot.CompletedAtUtc ?? snapshot.AsOfUtc, status,
                    slot.ManifestSha256, ReportingAuthority.Proven,
                    "pms_shadow.intraday_slots", slot.ContractVersion, slotId: slot.SlotId);
            if (slot.Status == "FAILED_CLOSED")
                yield return Create(slot.FailureCode ?? "INTRADAY_SLOT_FAILED_CLOSED",
                    slot.FailureCode, OperationalFactKinds.SlotFailureCode, "SLOT",
                    "Slot", slot.SlotId, slot.SlotEndUtc,
                    slot.CompletedAtUtc ?? snapshot.AsOfUtc, status, slot.ManifestSha256,
                    ReportingAuthority.Proven, "pms_shadow.intraday_slots",
                    slot.ContractVersion, slotId: slot.SlotId);
            if (slot.Status == "COMPLETED" &&
                (slot.ManifestSha256 is null || !slot.NoOrder || slot.Qualifying == false))
                yield return Create("INTRADAY_SLOT_INCOMPLETE", null,
                    OperationalFactKinds.StatusCode, "SLOT", "Slot", slot.SlotId,
                    slot.SlotEndUtc, slot.CompletedAtUtc ?? snapshot.AsOfUtc, status,
                    slot.ManifestSha256, slot.ManifestSha256 is null
                        ? ReportingAuthority.Absent : ReportingAuthority.Proven,
                    "pms_shadow.intraday_slots", slot.ContractVersion, slotId: slot.SlotId);
            if (slot.Manifest.ContractVersion is null)
                yield return Create("SLOT_MANIFEST_CONTRACT_UNKNOWN", null,
                    OperationalFactKinds.StatusCode, "MANIFEST", "Slot", slot.SlotId,
                    slot.SlotEndUtc, slot.CompletedAtUtc ?? snapshot.AsOfUtc,
                    historical ? OperationalBreakStatus.Historical : OperationalBreakStatus.Unknown,
                    slot.ManifestSha256, ReportingAuthority.Unknown,
                    "pms_shadow.intraday_slots.manifest_json", slot.ContractVersion,
                    slotId: slot.SlotId);
            else if (slot.Manifest.AuthorityStatus != ReportingAuthority.Proven)
                yield return Create("SLOT_MANIFEST_REQUIRED_FIELD_MISSING", null,
                    OperationalFactKinds.StatusCode, "MANIFEST", "Slot", slot.SlotId,
                    slot.SlotEndUtc, slot.CompletedAtUtc ?? snapshot.AsOfUtc,
                    historical ? OperationalBreakStatus.Historical : OperationalBreakStatus.Unknown,
                    slot.ManifestSha256, slot.Manifest.AuthorityStatus,
                    "pms_shadow.intraday_slots.manifest_json",
                    slot.Manifest.ContractVersion, slotId: slot.SlotId);
            if (slot.ReadyMarker.AuthorityStatus != ReportingAuthority.Proven)
                yield return Create("READY_MARKER_NOT_PROVEN", null,
                    OperationalFactKinds.StatusCode, "HANDOFF", "Slot", slot.SlotId,
                    slot.SlotEndUtc, slot.CompletedAtUtc ?? snapshot.AsOfUtc,
                    historical ? OperationalBreakStatus.Historical : OperationalBreakStatus.Unknown,
                    slot.ReadyMarker.ArtifactSha256, slot.ReadyMarker.AuthorityStatus,
                    "external.ready_marker", slot.ReadyMarker.SourceContractVersion,
                    slotId: slot.SlotId);
            if (slot.ImportStartLatencySeconds is >
                PmsShadowFreshSlotHandoffContract.AbsoluteStartDeadlineSeconds)
                yield return Create("HANDOFF_IMPORT_ABSOLUTE_DEADLINE_EXCEEDED", null,
                    OperationalFactKinds.StatusCode, "HANDOFF", "Slot", slot.SlotId,
                    slot.SlotEndUtc, slot.CompletedAtUtc ?? snapshot.AsOfUtc, status,
                    slot.ManifestSha256, ReportingAuthority.Proven,
                    "pms_shadow.intraday_slots", slot.ContractVersion, slotId: slot.SlotId);
        }

        if (expectation.SlotDueStatus == ReportingSlotDueStatuses.Missed &&
            latestSlot?.Status != "MISSED")
            yield return Create("INTRADAY_SLOT_MISSING", null,
                OperationalFactKinds.StatusCode, "SLOT", "ExpectedSlot",
                expectation.LatestExpectedClosedSlotId ?? snapshot.Database.Database,
                expectation.LatestExpectedClosedSlotEndUtc ?? snapshot.AsOfUtc,
                snapshot.AsOfUtc, OperationalBreakStatus.Active, null,
                ReportingAuthority.Absent, "pms_shadow.intraday_slots",
                expectation.ContractVersion, slotId: expectation.LatestExpectedClosedSlotId);
        else if (expectation.SlotDueStatus == ReportingSlotDueStatuses.StaleAfterDueTime &&
                 latestSlot?.Status != "MISSED")
            yield return Create("INTRADAY_SLOT_STALE", null,
                OperationalFactKinds.StatusCode, "SLOT", "Slot",
                latestSlot?.SlotId ?? snapshot.Database.Database,
                latestSlot?.SlotEndUtc ?? snapshot.AsOfUtc, snapshot.AsOfUtc,
                OperationalBreakStatus.Active, latestSlot?.ManifestSha256,
                ReportingAuthority.Proven, "pms_shadow.intraday_slots",
                latestSlot?.ContractVersion ?? expectation.ContractVersion,
                slotId: latestSlot?.SlotId);

        var positionMarket = snapshot.PositionMarketLineage ??
            Arch7bPositionMarketReporting.Absent();
        if (positionMarket.PositionMarketLineageStatus == ReportingAuthority.Absent)
            yield return PositionMarketBreak("POSITION_MARKET_SLOT_LINEAGE_MISSING",
                positionMarket, snapshot);
        else if (positionMarket.PositionMarketLineageStatus == ReportingAuthority.Contradictory)
            yield return PositionMarketBreak("POSITION_MARKET_SLOT_LINEAGE_CONTRADICTORY",
                positionMarket, snapshot);
        if (positionMarket.EconomicRevisionInputBindingStatus == ReportingAuthority.Absent)
            yield return PositionMarketBreak("POSITION_MARKET_REVISION_BINDING_MISSING",
                positionMarket, snapshot);
        if (positionMarket.Arch7aRevisionBindingStatus == ReportingAuthority.Contradictory)
            yield return PositionMarketBreak("POSITION_MARKET_ARCH7A_BINDING_MISMATCH",
                positionMarket, snapshot);

        var latestRevision = LatestRevision(snapshot);
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
                    "pms_shadow.intraday_projection_revisions");
        }

        foreach (var strategy in OperationalReportingContract.Strategies)
        {
            var selected = snapshot.ModelRuns.Where(value => value.StrategyId == strategy).ToArray();
            if (selected.Length == 0)
                yield return ModelSetBreak("SELECTED_STRATEGY_MISSING", strategy, snapshot);
            if (selected.Length > 1)
                yield return ModelSetBreak("SELECTED_STRATEGY_DUPLICATED", strategy, snapshot);
        }
        foreach (var model in snapshot.ModelRuns.Where(value =>
                     !OperationalReportingContract.Strategies.Contains(
                         value.StrategyId, StringComparer.Ordinal)))
            yield return ModelSetBreak("SELECTED_STRATEGY_UNEXPECTED", model.StrategyId, snapshot);
        foreach (var model in snapshot.ModelRuns)
        {
            if (!model.LineageComplete || model.QubesInputSnapshotId == Guid.Empty)
                yield return Create(model.QubesInputSnapshotId == Guid.Empty
                        ? "SELECTED_QUBES_INPUT_MISSING" : "MODEL_RUN_LINEAGE_INCOMPLETE",
                    null, OperationalFactKinds.StatusCode, "ANUBIS_INFX", "ModelRun",
                    model.ModelRunId.ToString("D"), model.AsOfUtc, snapshot.AsOfUtc,
                    OperationalBreakStatus.Active, model.OutputSha256,
                    ReportingAuthority.Unknown, "pms_shadow.model_runs",
                    model.SourceContractVersion, strategyId: model.StrategyId);
            if (OperationalReportingContract.ExpectedPerModelCounts.TryGetValue(
                    model.StrategyId, out var expected) &&
                (model.WeightCount != expected || model.TargetCount != expected))
                yield return Create("SELECTED_MODEL_WEIGHT_TARGET_COUNT_MISMATCH", null,
                    OperationalFactKinds.StatusCode, "ANUBIS_INFX", "ModelRun",
                    model.ModelRunId.ToString("D"), model.AsOfUtc, snapshot.AsOfUtc,
                    OperationalBreakStatus.Active, model.OutputSha256,
                    ReportingAuthority.Proven, "pms_shadow.target_weights",
                    model.SourceContractVersion, strategyId: model.StrategyId);
            if (model.TargetCount != model.DriftCount)
                yield return Create("SELECTED_MODEL_TARGET_DRIFT_COUNT_MISMATCH", null,
                    OperationalFactKinds.StatusCode, "ANUBIS_INFX", "ModelRun",
                    model.ModelRunId.ToString("D"), model.AsOfUtc, snapshot.AsOfUtc,
                    OperationalBreakStatus.Active, model.OutputSha256,
                    ReportingAuthority.Proven,
                    "pms_shadow.intraday_position_only_drifts",
                    model.SourceContractVersion, strategyId: model.StrategyId);
            if (model.ScheduleStatus is "DUE_MISSING" or "STALE_AFTER_DUE" or "UNKNOWN")
                yield return Create("MODEL_SCHEDULE_INCOMPLETE", null,
                    OperationalFactKinds.StatusCode, "ANUBIS_INFX", "ModelRun",
                    model.ModelRunId.ToString("D"), model.ExpectedTargetCloseUtc,
                    snapshot.AsOfUtc, OperationalBreakStatus.Active, model.OutputSha256,
                    ReportingAuthority.Unknown, "pms_shadow.model_runs",
                    ReportingInfxSchedules.ContractVersion, strategyId: model.StrategyId);
        }

        foreach (var item in snapshot.Arch7a.Where(value =>
                     value.Environment != OperationalReportingContract.TestEnvironment ||
                     value.AccountScope != OperationalReportingContract.RequiredAccountScope ||
                     value.Classification != OperationalReportingContract.ShadowClassification ||
                     value.BrokerRouteAllowed || value.BrokerSendAllowed))
            yield return Create("ARCH7A_NO_ORDER_INVARIANT_REQUIRED", null,
                OperationalFactKinds.StatusCode, "ARCH7A", "TradeIntent",
                item.TradeIntentId.ToString("D"), snapshot.AsOfUtc, snapshot.AsOfUtc,
                OperationalBreakStatus.Active, item.PlanSha256, ReportingAuthority.Proven,
                "pms_shadow.shadow_trade_intents", "arch7a_shadow_execution_v1",
                economicRevisionId: item.EconomicRevisionId,
                tradeIntentId: item.TradeIntentId,
                riskDecisionId: item.RiskDecisionId);

        foreach (var lifecycle in snapshot.Arch7b.Where(value => value.QualificationRunId.HasValue))
        {
            if (lifecycle.ReconciliationCount == 0)
                yield return Create("ARCH7B_FLATTEN_NOT_CONFIRMED", null,
                    OperationalFactKinds.LifecycleBreak, "ARCH7B", "QualificationRun",
                    lifecycle.QualificationRunId!.Value.ToString("D"),
                    lifecycle.CompletedAtUtc ?? snapshot.AsOfUtc, snapshot.AsOfUtc,
                    OperationalBreakStatus.Unknown, lifecycle.AuthorizationPacketSha256,
                    ReportingAuthority.Unknown, "pms_shadow.arch7b_qualification_runs",
                    "arch7b_known_order_lifecycle_v1",
                    qualificationRunId: lifecycle.QualificationRunId);
            if (lifecycle.KnownLeaves is not 0m || lifecycle.FinalLedgerQuantity is not 0m ||
                lifecycle.BrokerResidualQuantity is not 0m || lifecycle.CriticalBreakCount is > 0)
                yield return Create("ARCH7B_FINAL_RECONCILIATION_NOT_FLAT", null,
                    OperationalFactKinds.ReconciliationBreak, "RECONCILIATION",
                    "QualificationRun", lifecycle.QualificationRunId!.Value.ToString("D"),
                    lifecycle.CompletedAtUtc ?? snapshot.AsOfUtc, snapshot.AsOfUtc,
                    OperationalBreakStatus.Active, lifecycle.AuthorizationPacketSha256,
                    lifecycle.ReconciliationCount == 0
                        ? ReportingAuthority.Unknown : ReportingAuthority.Proven,
                    "pms_shadow.arch7b_final_reconciliations",
                    "arch7b_known_order_lifecycle_v1",
                    qualificationRunId: lifecycle.QualificationRunId);
        }

        foreach (var fact in snapshot.ObservedCodeFacts.Where(value => value.IsBlockingSourceFact))
        {
            var cataloged = OperationalStatusCodeCatalog.All.Any(
                item => item.ExactCode == fact.SourceExactCode);
            var exact = cataloged
                ? fact.SourceExactCode : "REPORTING_UNCATALOGUED_SOURCE_CODE";
            var status = fact.DerivedOperationalStatus switch
            {
                "HISTORICAL" => OperationalBreakStatus.Historical,
                "RESOLVED_BY_LATER_FACT" => OperationalBreakStatus.ResolvedByLaterFact,
                "UNKNOWN" => OperationalBreakStatus.Unknown,
                _ => cataloged ? OperationalBreakStatus.Active : OperationalBreakStatus.Unknown
            };
            yield return Create(exact, fact.SourceExactCode,
                cataloged ? fact.FactKind : OperationalFactKinds.UnknownSourceCode,
                fact.SourceComponent, fact.ScopeType, fact.ScopeId,
                fact.FirstObservedAtUtc, fact.LastObservedAtUtc, status,
                fact.EvidenceSha256, fact.AuthorityStatus, fact.SourceTable,
                fact.SourceContractVersion, fact.SlotId, fact.StrategyId,
                fact.EconomicRevisionId, fact.TradeIntentId, fact.RiskDecisionId,
                fact.QualificationRunId, fact.OrderId);
        }
    }

    private static ReportingEconomicRevisionFact? LatestRevision(
        OperationalReportingSnapshot snapshot) =>
        snapshot.EconomicRevisions.Where(value => value.Qualifying)
            .OrderByDescending(value => value.CompletedAtUtc)
            .ThenByDescending(value => value.EconomicRevisionId)
            .FirstOrDefault();

    private static OperationalBreak ModelSetBreak(
        string code,
        string strategy,
        OperationalReportingSnapshot snapshot) =>
        Create(code, null, OperationalFactKinds.StatusCode, "ANUBIS_INFX",
            "Strategy", strategy, snapshot.AsOfUtc, snapshot.AsOfUtc,
            OperationalBreakStatus.Active, null, ReportingAuthority.Unknown,
            "pms_shadow.intraday_projection_revisions",
            OperationalReportingContract.Version, strategyId: strategy);

    private static OperationalBreak RevisionBreak(
        string code,
        ReportingEconomicRevisionFact revision,
        string sourceTable) =>
        Create(code, null, OperationalFactKinds.StatusCode, "ECONOMIC_PROJECTION",
            "EconomicRevision", revision.EconomicRevisionId.ToString("D"),
            revision.CompletedAtUtc, revision.CompletedAtUtc,
            OperationalBreakStatus.Active, revision.ManifestSha256,
            ReportingAuthority.Proven, sourceTable, OperationalReportingContract.Version,
            slotId: revision.SlotId, economicRevisionId: revision.EconomicRevisionId);

    private static OperationalBreak PositionMarketBreak(
        string code,
        ReportingPositionMarketLineageFact lineage,
        OperationalReportingSnapshot snapshot) =>
        Create(code, null, OperationalFactKinds.StatusCode, "POSITION_MARKET_LINEAGE",
            "EconomicRevision",
            lineage.ProjectionRevisionId?.ToString("D") ?? snapshot.Database.Database,
            snapshot.AsOfUtc, snapshot.AsOfUtc, OperationalBreakStatus.Active,
            lineage.PositionMarketLineageEvidenceSha256 ??
            lineage.EconomicRevisionInputBindingSha256,
            code.Contains("MISSING", StringComparison.Ordinal)
                ? ReportingAuthority.Absent : ReportingAuthority.Contradictory,
            "external.position_market_lineage",
            Arch7bPositionMarketSlotLineageContract.Version,
            economicRevisionId: lineage.ProjectionRevisionId);

    private static OperationalBreak Create(
        string exactCode,
        string? sourceExactCode,
        string factKind,
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
        Guid? riskDecisionId = null,
        Guid? qualificationRunId = null,
        string? orderId = null)
    {
        var catalog = OperationalStatusCodeCatalog.Get(exactCode);
        var cataloged = catalog.ExactCode == exactCode;
        var effectiveCode = cataloged ? exactCode : "REPORTING_UNCATALOGUED_SOURCE_CODE";
        var id = BreakId(effectiveCode, component, scopeType, scopeId, slotId,
            economicRevisionId, tradeIntentId, qualificationRunId, orderId, evidenceSha256);
        return new(
            id, effectiveCode, sourceExactCode, factKind, catalog.Category,
            catalog.Severity, status, component, scopeType, scopeId, slotId,
            strategyId, economicRevisionId, null, null, tradeIntentId,
            riskDecisionId, qualificationRunId, orderId, firstObservedAtUtc,
            lastObservedAtUtc, evidenceSha256, authorityStatus,
            catalog.BlocksTrading, catalog.BlocksAccounting, catalog.OperatorMeaning,
            catalog.OperatorMeaning, sourceTable, sourceContractVersion);
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
            latest.ReconciliationCount == 0
                ? ReportingAuthority.Unknown : ReportingAuthority.Proven,
            latest.QualificationRunId, latest.KnownLeaves, latest.FinalLedgerQuantity,
            latest.BrokerResidualQuantity, latest.CriticalBreakCount ?? 0,
            latest.FinalGate, latest.AuthorizationPacketSha256, latest.CompletedAtUtc);
    }

    private static void RequireUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new InvalidDataException("REPORTING_AS_OF_NOT_UTC");
    }
}
