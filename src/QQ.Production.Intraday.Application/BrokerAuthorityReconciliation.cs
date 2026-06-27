using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public sealed record BrokerAuthorityReconciliationInput(
    BrokerAuthorityScope Scope,
    DateTimeOffset AsOfUtc,
    BrokerAuthoritySourceState ExecutionSource,
    BrokerAuthoritySourceState PositionSource,
    BrokerAuthoritySourceState OpenOrderSource,
    IReadOnlyList<Fill> InternalFills,
    IReadOnlyList<InternalPositionSnapshot> InternalPositions,
    IReadOnlyList<BrokerInternalWorkingOrderSnapshot> InternalWorkingOrders,
    IReadOnlyList<BrokerExecutionEvent> BrokerExecutions,
    IReadOnlyList<BrokerPositionSnapshotEvidence> BrokerPositions,
    IReadOnlyList<BrokerOpenOrderSnapshot> BrokerOpenOrders,
    IReadOnlyList<BrokerManualEvidence> ManualEvidence,
    IReadOnlyList<BrokerTargetPosition> TargetPositions,
    TimeSpan MaxSourceAge)
{
    public static BrokerAuthorityReconciliationInput Empty(
        BrokerAuthorityScope scope,
        DateTimeOffset asOfUtc,
        BrokerAuthoritySourceState executionSource,
        BrokerAuthoritySourceState positionSource,
        BrokerAuthoritySourceState openOrderSource,
        TimeSpan maxSourceAge)
        => new(scope, asOfUtc, executionSource, positionSource, openOrderSource, [], [], [], [], [], [], [], [], maxSourceAge);
}

public sealed class BrokerAuthorityReconciler
{
    public BrokerReconciliationResult Reconcile(BrokerAuthorityReconciliationInput input)
    {
        var runId = Guid.NewGuid();
        var breaks = new List<BrokerReconciliationBreak>();
        AddSourceBreaks(input, runId, breaks);
        AddDuplicateExecutionBreaks(input, runId, breaks);
        AddSequenceBreaks(input, runId, breaks);
        AddFillBreaks(input, runId, breaks);
        AddPositionBreaks(input, runId, breaks);
        AddOpenOrderBreaks(input, runId, breaks);

        var remainingDeltas = ComputeRemainingDeltas(input);
        var hasBlocking = breaks.Any(x => x.Blocking);
        var hasCritical = breaks.Any(x => x.Severity == BrokerReconciliationSeverity.Critical);
        var run = new BrokerReconciliationRun(
            runId,
            input.Scope,
            input.AsOfUtc,
            input.ExecutionSource,
            input.PositionSource,
            input.OpenOrderSource,
            ComputeInputHash(input),
            hasBlocking,
            hasCritical);

        return new BrokerReconciliationResult(run, breaks, remainingDeltas, DetermineReadiness(input, breaks));
    }

    private static void AddSourceBreaks(BrokerAuthorityReconciliationInput input, Guid runId, List<BrokerReconciliationBreak> breaks)
    {
        AddSourceBreak(input, runId, breaks, input.ExecutionSource, "execution feed", BrokerAuthoritySourceRole.ExecutionFeed);
        AddSourceBreak(input, runId, breaks, input.PositionSource, "broker position snapshot", BrokerAuthoritySourceRole.BrokerPositionSnapshot);
        AddSourceBreak(input, runId, breaks, input.OpenOrderSource, "broker open-order snapshot", BrokerAuthoritySourceRole.BrokerOpenOrderSnapshot);
        AddNonAuthoritativePositionEvidenceBreaks(input, runId, breaks);
        AddNonAuthoritativeOpenOrderEvidenceBreaks(input, runId, breaks);

        if (input.BrokerExecutions.Any(x => IsSameScope(x.Scope, input.Scope) && x.SourceQuality == BrokerSourceQuality.MANUAL_EVIDENCE))
        {
            AddBreak(input, runId, breaks, BrokerReconciliationBreakType.MISSING_BROKER_FILL, BrokerReconciliationSeverity.Warning, false, null, null,
                "Manual broker UI/export evidence is present but is not authoritative execution-feed evidence.",
                input.BrokerExecutions.Where(x => IsSameScope(x.Scope, input.Scope) && x.SourceQuality == BrokerSourceQuality.MANUAL_EVIDENCE).Select(x => x.SourceHash));
        }
    }

    private static void AddSourceBreak(BrokerAuthorityReconciliationInput input, Guid runId, List<BrokerReconciliationBreak> breaks, BrokerAuthoritySourceState source, string label, BrokerAuthoritySourceRole role)
    {
        if (source.Quality is BrokerSourceQuality.UNKNOWN or BrokerSourceQuality.INCOMPLETE)
        {
            AddBreak(input, runId, breaks, BrokerReconciliationBreakType.UNKNOWN_ACCOUNT_SCOPE, BrokerReconciliationSeverity.Critical, true, null, null,
                $"Missing or incomplete {label}: {source.Reason ?? source.Quality.ToString()}.",
                SourceHash(source));
            return;
        }

        if (!BrokerAuthoritySourcePolicy.IsAcceptedQuality(role, source.Quality))
        {
            AddBreak(input, runId, breaks, BrokerReconciliationBreakType.UNACCEPTABLE_BROKER_AUTHORITY_SOURCE, BrokerReconciliationSeverity.Critical, true, null, null,
                $"{source.Quality} cannot be used as {label}; required {BrokerAuthoritySourcePolicy.AcceptedQualityDescription(role)}.",
                SourceHash(source));
            return;
        }

        if (source.IsStale(input.MaxSourceAge, input.AsOfUtc))
        {
            AddBreak(input, runId, breaks, BrokerReconciliationBreakType.STALE_BROKER_SNAPSHOT, BrokerReconciliationSeverity.Blocking, true, null, null,
                $"Stale {label}: source as-of {source.AsOfUtc:O}, run as-of {input.AsOfUtc:O}.",
                SourceHash(source));
        }
    }

    private static void AddNonAuthoritativePositionEvidenceBreaks(BrokerAuthorityReconciliationInput input, Guid runId, List<BrokerReconciliationBreak> breaks)
    {
        foreach (var position in input.BrokerPositions.Where(x => IsSameScope(x.Scope, input.Scope) && x.SourceQuality != BrokerSourceQuality.AUTHORITATIVE))
        {
            AddBreak(input, runId, breaks, BrokerReconciliationBreakType.UNACCEPTABLE_BROKER_AUTHORITY_SOURCE, BrokerReconciliationSeverity.Critical, true, position.Snapshot.InstrumentId, position.Symbol,
                $"Broker position evidence from {position.SourceName} has quality {position.SourceQuality}; broker position authority requires AUTHORITATIVE only.",
                [position.SourceHash]);
        }
    }

    private static void AddNonAuthoritativeOpenOrderEvidenceBreaks(BrokerAuthorityReconciliationInput input, Guid runId, List<BrokerReconciliationBreak> breaks)
    {
        foreach (var order in input.BrokerOpenOrders.Where(x => IsSameScope(x.Scope, input.Scope) && x.SourceQuality != BrokerSourceQuality.AUTHORITATIVE))
        {
            AddBreak(input, runId, breaks, BrokerReconciliationBreakType.UNACCEPTABLE_BROKER_AUTHORITY_SOURCE, BrokerReconciliationSeverity.Critical, true, order.InstrumentId, order.Symbol,
                $"Broker open-order evidence from {order.SourceName} has quality {order.SourceQuality}; broker open-order authority requires AUTHORITATIVE only.",
                [order.SourceHash],
                [$"clordid:{order.ClientOrderId ?? "missing"}", $"broker-order:{order.BrokerOrderId ?? "missing"}"]);
        }
    }

    private static void AddDuplicateExecutionBreaks(BrokerAuthorityReconciliationInput input, Guid runId, List<BrokerReconciliationBreak> breaks)
    {
        foreach (var group in input.BrokerExecutions
                     .Where(x => IsSameScope(x.Scope, input.Scope))
                     .Where(x => !string.IsNullOrWhiteSpace(x.BrokerExecutionId) && x.IsAuthoritativeExecution)
                     .GroupBy(x => x.ExecutionScopeKey, StringComparer.OrdinalIgnoreCase)
                     .Where(x => x.Count() > 1))
        {
            var distinctPayloads = group.Select(x => x.RawPayloadHash ?? x.ComparablePayload).Distinct(StringComparer.Ordinal).ToList();
            if (distinctPayloads.Count <= 1)
            {
                continue;
            }

            var first = group.First();
            AddBreak(input, runId, breaks, BrokerReconciliationBreakType.DUPLICATE_EXECUTION, BrokerReconciliationSeverity.Critical, true, first.InstrumentId, first.Symbol,
                $"Conflicting duplicate BrokerExecutionId {first.BrokerExecutionId} in scope {first.Scope.ScopeKey}.",
                group.Select(x => x.SourceHash),
                group.Select(x => $"exec:{x.BrokerExecutionId}:seq:{x.SequenceNumber?.ToString() ?? "none"}"));
        }
    }

    private static void AddSequenceBreaks(BrokerAuthorityReconciliationInput input, Guid runId, List<BrokerReconciliationBreak> breaks)
    {
        var sequenceNumbers = input.BrokerExecutions
            .Where(x => IsSameScope(x.Scope, input.Scope))
            .Where(x => x.IsAuthoritativeExecution && x.SequenceNumber is not null)
            .OrderBy(x => x.SequenceNumber!.Value)
            .Select(x => x.SequenceNumber!.Value)
            .Distinct()
            .ToList();

        for (var i = 1; i < sequenceNumbers.Count; i++)
        {
            if (sequenceNumbers[i] == sequenceNumbers[i - 1] + 1)
            {
                continue;
            }

            AddBreak(input, runId, breaks, BrokerReconciliationBreakType.SEQUENCE_GAP, BrokerReconciliationSeverity.Critical, true, null, null,
                $"Broker execution sequence gap detected between {sequenceNumbers[i - 1]} and {sequenceNumbers[i]}.",
                input.BrokerExecutions.Where(x => IsSameScope(x.Scope, input.Scope)).Select(x => x.SourceHash));
            return;
        }
    }

    private static void AddFillBreaks(BrokerAuthorityReconciliationInput input, Guid runId, List<BrokerReconciliationBreak> breaks)
    {
        var brokerFillsByExecId = input.BrokerExecutions
            .Where(x => IsSameScope(x.Scope, input.Scope))
            .Where(x => x.IsFillLike && x.IsAuthoritativeExecution && !string.IsNullOrWhiteSpace(x.BrokerExecutionId))
            .GroupBy(x => x.BrokerExecutionId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var internalFillsByExecId = input.InternalFills
            .Where(x => !string.IsNullOrWhiteSpace(x.BrokerExecutionId))
            .GroupBy(x => x.BrokerExecutionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var fill in input.InternalFills)
        {
            if (string.IsNullOrWhiteSpace(fill.BrokerExecutionId) || brokerFillsByExecId.ContainsKey(fill.BrokerExecutionId))
            {
                continue;
            }

            AddBreak(input, runId, breaks, BrokerReconciliationBreakType.MISSING_BROKER_FILL, BrokerReconciliationSeverity.Blocking, true, fill.InstrumentId, null,
                $"Internal fill {fill.BrokerExecutionId} has no matching broker execution-feed fill.",
                SourceHash(input.ExecutionSource),
                [$"internal-fill:{fill.Id.Value:D}", $"exec:{fill.BrokerExecutionId}"]);
        }

        foreach (var brokerFill in brokerFillsByExecId.Values)
        {
            if (brokerFill.BrokerExecutionId is not null && internalFillsByExecId.ContainsKey(brokerFill.BrokerExecutionId))
            {
                continue;
            }

            AddBreak(input, runId, breaks, BrokerReconciliationBreakType.MISSING_INTERNAL_FILL, BrokerReconciliationSeverity.Blocking, true, brokerFill.InstrumentId, brokerFill.Symbol,
                $"Broker execution-feed fill {brokerFill.BrokerExecutionId ?? "(missing exec id)"} has no matching internal fill.",
                [brokerFill.SourceHash],
                [$"broker-exec:{brokerFill.BrokerExecutionId ?? "missing"}"]);
        }
    }

    private static void AddPositionBreaks(BrokerAuthorityReconciliationInput input, Guid runId, List<BrokerReconciliationBreak> breaks)
    {
        var brokerPositions = input.BrokerPositions
            .Where(x => IsSameScope(x.Scope, input.Scope))
            .Where(x => x.SourceQuality == BrokerSourceQuality.AUTHORITATIVE)
            .ToDictionary(x => x.Snapshot.InstrumentId);
        var internalPositions = input.InternalPositions.ToDictionary(x => x.InstrumentId);
        var instruments = internalPositions.Keys.Concat(brokerPositions.Keys).Distinct().ToList();

        foreach (var instrumentId in instruments)
        {
            var hasInternal = internalPositions.TryGetValue(instrumentId, out var internalPosition);
            var hasBroker = brokerPositions.TryGetValue(instrumentId, out var brokerPosition);
            if (!hasInternal || !hasBroker)
            {
                AddBreak(input, runId, breaks, BrokerReconciliationBreakType.POSITION_INSTRUMENT_MISMATCH, BrokerReconciliationSeverity.Blocking, true, instrumentId, brokerPosition?.Symbol,
                    $"Position instrument exists internal={hasInternal} broker={hasBroker}.",
                    hasBroker ? [brokerPosition!.SourceHash] : SourceHash(input.PositionSource));
                continue;
            }

            if (Math.Abs(internalPosition!.BaseQuantity - brokerPosition!.Snapshot.BaseQuantity) > 0.0001m)
            {
                AddBreak(input, runId, breaks, BrokerReconciliationBreakType.POSITION_QUANTITY_MISMATCH, BrokerReconciliationSeverity.Critical, true, instrumentId, brokerPosition.Symbol,
                    $"Internal position {internalPosition.BaseQuantity} vs broker position {brokerPosition.Snapshot.BaseQuantity}.",
                    [brokerPosition.SourceHash]);
            }
        }
    }

    private static void AddOpenOrderBreaks(BrokerAuthorityReconciliationInput input, Guid runId, List<BrokerReconciliationBreak> breaks)
    {
        var brokerByClient = input.BrokerOpenOrders
            .Where(x => IsSameScope(x.Scope, input.Scope))
            .Where(x => x.SourceQuality == BrokerSourceQuality.AUTHORITATIVE)
            .Where(x => !string.IsNullOrWhiteSpace(x.ClientOrderId))
            .ToDictionary(x => x.ClientOrderId!, StringComparer.OrdinalIgnoreCase);
        var internalByClient = input.InternalWorkingOrders.ToDictionary(x => x.ClientOrderId, StringComparer.OrdinalIgnoreCase);

        foreach (var internalOrder in input.InternalWorkingOrders)
        {
            if (!brokerByClient.TryGetValue(internalOrder.ClientOrderId, out var brokerOrder))
            {
                AddBreak(input, runId, breaks, BrokerReconciliationBreakType.OPEN_ORDER_MISSING_BROKER, BrokerReconciliationSeverity.Blocking, true, internalOrder.InstrumentId, internalOrder.Symbol,
                    $"Internal working order {internalOrder.ClientOrderId} is missing in broker open-order source.",
                    SourceHash(input.OpenOrderSource),
                    [$"child:{internalOrder.ChildOrderId.Value:D}", $"clordid:{internalOrder.ClientOrderId}"]);
                continue;
            }

            if (Math.Abs(internalOrder.LeavesQuantity - brokerOrder.LeavesQuantity) > 0.0001m)
            {
                AddBreak(input, runId, breaks, BrokerReconciliationBreakType.LEAVES_MISMATCH, BrokerReconciliationSeverity.Blocking, true, internalOrder.InstrumentId, internalOrder.Symbol,
                    $"Working leaves mismatch for {internalOrder.ClientOrderId}: internal={internalOrder.LeavesQuantity}, broker={brokerOrder.LeavesQuantity}.",
                    [brokerOrder.SourceHash]);
            }

            if (internalOrder.Status == BrokerOrderLifecycleStatus.PendingCancel)
            {
                AddBreak(input, runId, breaks, BrokerReconciliationBreakType.UNRESOLVED_CANCEL_PENDING, BrokerReconciliationSeverity.Blocking, true, internalOrder.InstrumentId, internalOrder.Symbol,
                    $"Cancel pending order {internalOrder.ClientOrderId} still reserves leaves until terminal broker evidence arrives.",
                    [brokerOrder.SourceHash]);
            }

            if (IsTerminal(brokerOrder.Status) && !IsTerminal(internalOrder.Status))
            {
                AddBreak(input, runId, breaks, BrokerReconciliationBreakType.TERMINAL_STATE_MISMATCH, BrokerReconciliationSeverity.Blocking, true, internalOrder.InstrumentId, internalOrder.Symbol,
                    $"Broker terminal state {brokerOrder.Status} differs from internal non-terminal state {internalOrder.Status}.",
                    [brokerOrder.SourceHash]);
            }
        }

        foreach (var brokerOrder in input.BrokerOpenOrders.Where(x => IsSameScope(x.Scope, input.Scope) && x.SourceQuality == BrokerSourceQuality.AUTHORITATIVE))
        {
            if (!string.IsNullOrWhiteSpace(brokerOrder.ClientOrderId) && internalByClient.ContainsKey(brokerOrder.ClientOrderId))
            {
                continue;
            }

            AddBreak(input, runId, breaks, BrokerReconciliationBreakType.OPEN_ORDER_MISSING_INTERNAL, BrokerReconciliationSeverity.Blocking, true, brokerOrder.InstrumentId, brokerOrder.Symbol,
                $"Broker open order {brokerOrder.ClientOrderId ?? brokerOrder.BrokerOrderId ?? "(missing id)"} is missing internally.",
                [brokerOrder.SourceHash]);
        }
    }

    private static IReadOnlyList<BrokerRemainingDelta> ComputeRemainingDeltas(BrokerAuthorityReconciliationInput input)
    {
        var currentByInstrument = input.InternalPositions.ToDictionary(x => x.InstrumentId, x => x.BaseQuantity);
        var reservedByInstrument = input.InternalWorkingOrders
            .Where(x => x.ReservesLeaves)
            .GroupBy(x => x.InstrumentId)
            .ToDictionary(
                x => x.Key,
                x => x.Sum(order => order.Side == TradeSide.Buy ? order.LeavesQuantity : -order.LeavesQuantity));

        return input.TargetPositions
            .Select(target =>
            {
                var current = currentByInstrument.GetValueOrDefault(target.InstrumentId);
                var reserved = reservedByInstrument.GetValueOrDefault(target.InstrumentId);
                return new BrokerRemainingDelta(target.InstrumentId, target.Symbol, target.TargetQuantity, current, reserved, target.TargetQuantity - current - reserved);
            })
            .ToList();
    }

    private static BrokerAuthorityReadiness DetermineReadiness(BrokerAuthorityReconciliationInput input, IReadOnlyList<BrokerReconciliationBreak> breaks)
    {
        var executionReady = BrokerAuthoritySourcePolicy.IsUsableFor(BrokerAuthoritySourceRole.ExecutionFeed, input.ExecutionSource, input.MaxSourceAge, input.AsOfUtc);
        var positionReady = BrokerAuthoritySourcePolicy.IsUsableFor(BrokerAuthoritySourceRole.BrokerPositionSnapshot, input.PositionSource, input.MaxSourceAge, input.AsOfUtc);
        var openOrderReady = BrokerAuthoritySourcePolicy.IsUsableFor(BrokerAuthoritySourceRole.BrokerOpenOrderSnapshot, input.OpenOrderSource, input.MaxSourceAge, input.AsOfUtc);
        var sourceReady = executionReady && positionReady && openOrderReady;

        if (!sourceReady)
        {
            return new BrokerAuthorityReadiness(
                BrokerAuthorityOperationalGate.NO_GO,
                BrokerAuthorityReadinessDecision.BLOCK_NEW_ORDERS,
                "NO_GO_BROKER_AUTHORITY_SOURCE_NOT_READY",
                "Execution, position, and open-order sources must be usable before trading readiness.",
                positionReady,
                openOrderReady,
                executionReady);
        }

        if (breaks.Any(x => x.Severity == BrokerReconciliationSeverity.Critical))
        {
            return new BrokerAuthorityReadiness(
                BrokerAuthorityOperationalGate.NO_GO,
                BrokerAuthorityReadinessDecision.EMERGENCY_STOP,
                "NO_GO_CRITICAL_BROKER_BREAK",
                "Critical broker reconciliation breaks are unresolved.",
                positionReady,
                openOrderReady,
                executionReady);
        }

        if (breaks.Any(x => x.Blocking))
        {
            return new BrokerAuthorityReadiness(
                BrokerAuthorityOperationalGate.NO_GO,
                BrokerAuthorityReadinessDecision.BLOCK_NEW_ORDERS,
                "NO_GO_BLOCKING_BROKER_BREAK",
                "Blocking broker reconciliation breaks are unresolved.",
                positionReady,
                openOrderReady,
                executionReady);
        }

        return new BrokerAuthorityReadiness(
            BrokerAuthorityOperationalGate.GO,
            BrokerAuthorityReadinessDecision.CAN_TRADE,
            "BROKER_AUTHORITY_CAN_TRADE",
            "Broker authority inputs reconcile cleanly.",
            positionReady,
            openOrderReady,
            executionReady);
    }

    private static bool IsTerminal(BrokerOrderLifecycleStatus status)
        => status is BrokerOrderLifecycleStatus.Filled
            or BrokerOrderLifecycleStatus.Cancelled
            or BrokerOrderLifecycleStatus.Rejected
            or BrokerOrderLifecycleStatus.Expired;

    private static bool IsSameScope(BrokerAuthorityScope left, BrokerAuthorityScope right)
        => string.Equals(left.Environment, right.Environment, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Account, right.Account, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Venue, right.Venue, StringComparison.OrdinalIgnoreCase);

    private static void AddBreak(
        BrokerAuthorityReconciliationInput input,
        Guid runId,
        ICollection<BrokerReconciliationBreak> breaks,
        BrokerReconciliationBreakType type,
        BrokerReconciliationSeverity severity,
        bool blocking,
        InstrumentId? instrumentId,
        string? symbol,
        string description,
        IEnumerable<string?> sourceHashes,
        IEnumerable<string>? evidenceRefs = null)
        => breaks.Add(new BrokerReconciliationBreak(
            Guid.NewGuid(),
            runId,
            type,
            severity,
            blocking,
            BrokerReconciliationResolutionStatus.Open,
            input.Scope,
            instrumentId,
            symbol,
            input.AsOfUtc,
            sourceHashes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct(StringComparer.Ordinal).ToList(),
            (evidenceRefs ?? []).Distinct(StringComparer.Ordinal).ToList(),
            description));

    private static IReadOnlyList<string?> SourceHash(BrokerAuthoritySourceState source) => [source.SourceHash];

    private static string ComputeInputHash(BrokerAuthorityReconciliationInput input)
    {
        var canonical = string.Join("\n",
            input.Scope.ScopeKey,
            input.AsOfUtc.UtcDateTime.ToString("O"),
            input.ExecutionSource.SourceHash ?? "",
            input.PositionSource.SourceHash ?? "",
            input.OpenOrderSource.SourceHash ?? "",
            string.Join("|", input.InternalFills.Select(x => x.BrokerExecutionId).Order(StringComparer.Ordinal)),
            string.Join("|", input.BrokerExecutions.Select(x => x.RawPayloadHash ?? x.ComparablePayload).Order(StringComparer.Ordinal)),
            string.Join("|", input.BrokerOpenOrders.Select(x => x.OrderScopeKey).Order(StringComparer.Ordinal)),
            string.Join("|", input.BrokerPositions.Select(x => $"{x.Snapshot.InstrumentId.Value:D}:{x.Snapshot.BaseQuantity:G29}").Order(StringComparer.Ordinal)),
            string.Join("|", input.TargetPositions.Select(x => $"{x.InstrumentId.Value:D}:{x.TargetQuantity:G29}").Order(StringComparer.Ordinal)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}

