using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Application;

public static class Arch7bKnownOrderQualificationPolicy
{
    public const string Gate =
        "ARCH7B_REQUALIFY_EXISTING_LMAX_DEMO_FIX_ORDER_ENTRY_SINGLE_BOUNDED_KNOWN_ORDER_LIFECYCLE_FLATTEN_AND_RECONCILIATION";
    public const string Scope = "DEMO_EXCLUSIVE_KNOWN_ORDER_QUALIFICATION_WINDOW";
    public const string DemoAccountId = "1754288005";
    public const string ForbiddenRealAccountId = "921640160";
    public const string Environment = "TEST";
    public const string Venue = "LMAX_DEMO";
    public const string Symbol = "GBPUSD";
    public const string SecurityId = "4002";
    public const string SecurityIdSource = "8";
    public const string OpeningSide = "BUY";
    public const decimal VenueQuantity = 0.1m;
    public const decimal QuantityIncrement = 0.1m;
    public const decimal PriceIncrement = 0.00001m;
    public const decimal CollarPips = 2m;
    public const decimal MaximumSpreadPips = CollarPips;
    public const int MaximumBboAgeSeconds = 5;
    public const int MaximumMarketDataCleanupMilliseconds = 1000;
    public const int MaximumLifecycleSeconds = 180;
    public const int MaximumNewOrderSingleCount = 2;
    public const int MaximumCancelCount = 1;
    public const int MaximumReplaceCount = 0;
    public const int MaximumOrderStatusRequestCount = 4;
    public const int MaximumFlattenBboAcquisitionAttempts = 3;
    public const string OpeningLimitPolicy = "LMAX_CURRENT_BBO_TOUCH_LIMIT";
    public const string FlattenLimitPolicy = "LMAX_CURRENT_BBO_TOUCH_LIMIT_OPPOSITE_SIDE";
    public const string ExternalOrManualOrderCoverage = "UNPROVEN";
}

/// <summary>
/// The immutable execution binding for one bounded known-order lifecycle.
/// Demo uses <see cref="Demo"/>; production callers must construct and bind a
/// separate profile rather than relaxing the Demo policy.
/// </summary>
public sealed record Arch7bKnownOrderExecutionProfile(
    string Gate,
    string Scope,
    string Environment,
    string AccountId,
    string Symbol,
    string SecurityId,
    string SecurityIdSource,
    string OpeningSide,
    decimal VenueQuantity,
    decimal QuantityIncrement,
    decimal PriceIncrement,
    decimal CollarPips,
    int MaximumBboAgeSeconds,
    int MaximumLifecycleSeconds,
    int MaximumNewOrderSingleCount,
    int MaximumCancelCount,
    int MaximumReplaceCount,
    int MaximumOrderStatusRequestCount,
    string OpeningLimitPolicy,
    string FlattenLimitPolicy,
    string ExternalOrManualOrderCoverage)
{
    public static Arch7bKnownOrderExecutionProfile Demo { get; } = new(
        Arch7bKnownOrderQualificationPolicy.Gate,
        Arch7bKnownOrderQualificationPolicy.Scope,
        Arch7bKnownOrderQualificationPolicy.Environment,
        Arch7bKnownOrderQualificationPolicy.DemoAccountId,
        Arch7bKnownOrderQualificationPolicy.Symbol,
        Arch7bKnownOrderQualificationPolicy.SecurityId,
        Arch7bKnownOrderQualificationPolicy.SecurityIdSource,
        Arch7bKnownOrderQualificationPolicy.OpeningSide,
        Arch7bKnownOrderQualificationPolicy.VenueQuantity,
        Arch7bKnownOrderQualificationPolicy.QuantityIncrement,
        Arch7bKnownOrderQualificationPolicy.PriceIncrement,
        Arch7bKnownOrderQualificationPolicy.CollarPips,
        Arch7bKnownOrderQualificationPolicy.MaximumBboAgeSeconds,
        Arch7bKnownOrderQualificationPolicy.MaximumLifecycleSeconds,
        Arch7bKnownOrderQualificationPolicy.MaximumNewOrderSingleCount,
        Arch7bKnownOrderQualificationPolicy.MaximumCancelCount,
        Arch7bKnownOrderQualificationPolicy.MaximumReplaceCount,
        Arch7bKnownOrderQualificationPolicy.MaximumOrderStatusRequestCount,
        Arch7bKnownOrderQualificationPolicy.OpeningLimitPolicy,
        Arch7bKnownOrderQualificationPolicy.FlattenLimitPolicy,
        Arch7bKnownOrderQualificationPolicy.ExternalOrManualOrderCoverage);
}

public sealed record Arch7bSelectedChildOrder(
    Guid TradeIntentId,
    Guid ParentOrderId,
    Guid ChildOrderId,
    string ClientOrderId,
    string SourceSessionId,
    string SlotId,
    Guid EconomicRevisionId,
    int EconomicRevisionNumber,
    string MarketDataSnapshotSha256,
    string SourceLineageSha256,
    string PlanSha256,
    string Environment,
    string AccountScope,
    string Symbol,
    string SecurityId,
    string SecurityIdSource,
    string Side,
    decimal SourceQuantity,
    DateTimeOffset TargetCloseUtc,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset DeadlineUtc,
    string TradeIntentClassification,
    string ParentStatus,
    string ChildStatus,
    bool LatestQualifyingRevision,
    bool SourceCompleted,
    bool SourceFresh,
    bool SourceSuperseded,
    bool LmaxMarketData,
    bool PolygonOrderPrice,
    bool LineageComplete);

public sealed record Arch7bLmaxBbo(
    string Symbol,
    string SecurityId,
    decimal Bid,
    decimal Ask,
    DateTimeOffset ObservedAtUtc,
    string Source,
    string SnapshotSha256,
    DateTimeOffset AcquisitionStartedAtUtc = default,
    bool SequenceIntegrityProven = false,
    bool PolygonUsed = false)
{
    public decimal Spread => Ask - Bid;
}

public sealed record Arch7bExclusivityDeclaration(
    string OwnerId,
    DateTimeOffset AcquiredAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool AdvisoryLeaseHeld,
    bool NoManualOrdersDeclared,
    bool NoOtherBotDeclared,
    bool NoOtherGatewayDeclared,
    bool NoOtherUserDeclared,
    bool NoOtherTestDeclared,
    bool NoConcurrentFixOrderEntrySessionDeclared,
    bool OneRunOnly,
    bool OneDemoAccountOnly);

public sealed record Arch7bPreflightInput(
    Arch7bSelectedChildOrder ChildOrder,
    Arch7bLmaxBbo Bbo,
    Arch7bExclusivityDeclaration Exclusivity,
    string ConfiguredEnvironment,
    string ConfiguredAccountId,
    decimal CurrentKnownPosition,
    int PlatformKnownWorkingOrderCount,
    bool ExactOperatorAuthorizationPresent,
    bool KillSwitchArmed,
    DateTimeOffset EvaluationTimeUtc);

public sealed record Arch7bPreflightDecision(
    bool Allowed,
    IReadOnlyList<string> Blockers,
    string OpeningClientOrderId,
    string FlattenClientOrderId,
    string CancelClientOrderId,
    decimal OpeningLimitPrice,
    decimal MaximumOpeningPrice,
    decimal MinimumOpeningPrice,
    string PolicySha256);

public sealed record Arch7bApplicationMessageBudget(
    int NewOrderSingleCount,
    int CancelCount,
    int ReplaceCount,
    int OrderStatusRequestCount)
{
    public bool WithinPolicy =>
        NewOrderSingleCount <= Arch7bKnownOrderQualificationPolicy.MaximumNewOrderSingleCount &&
        CancelCount <= Arch7bKnownOrderQualificationPolicy.MaximumCancelCount &&
        ReplaceCount <= Arch7bKnownOrderQualificationPolicy.MaximumReplaceCount &&
        OrderStatusRequestCount <= Arch7bKnownOrderQualificationPolicy.MaximumOrderStatusRequestCount;
}

public sealed record Arch7bExecutionReportEvent(
    string SessionId,
    long SequenceNumber,
    string AccountId,
    string OrderId,
    string ClOrdId,
    string? OrigClOrdId,
    string ExecId,
    string ExecType,
    string OrdStatus,
    string Symbol,
    string SecurityId,
    string Side,
    decimal OrderQty,
    decimal CumQty,
    decimal LeavesQty,
    decimal LastQty,
    decimal LastPx,
    decimal AvgPx,
    decimal? Price,
    DateTimeOffset TransactTimeUtc,
    bool PossDup,
    string RawMessageSha256);

public sealed record Arch7bValidatedFill(
    string FillId,
    string ExecId,
    string OrderId,
    string ClOrdId,
    string Symbol,
    string SecurityId,
    string Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset TransactTimeUtc,
    string RawMessageSha256,
    decimal SignedQuantity);

public sealed record Arch7bKnownOrderState(
    string ClOrdId,
    string OrderId,
    string OrdStatus,
    string ExecType,
    decimal OrderQty,
    decimal CumQty,
    decimal LeavesQty,
    bool Terminal,
    bool Working);

public sealed record Arch7bLifecycleEvaluation(
    IReadOnlyList<Arch7bExecutionReportEvent> AcceptedExecutionReports,
    IReadOnlyList<Arch7bValidatedFill> Fills,
    IReadOnlyList<Arch7bKnownOrderState> Orders,
    decimal InternalPosition,
    decimal OpeningFilledQuantity,
    decimal FlattenFilledQuantity,
    decimal ResidualQuantity,
    decimal? RealizedPnlBeforeFees,
    string FeeStatus,
    int KnownWorkingOrderCount,
    int CriticalBreakCount,
    IReadOnlyList<string> Issues,
    string EvaluationSha256)
{
    public bool Flat =>
        ResidualQuantity == 0m &&
        InternalPosition == 0m &&
        KnownWorkingOrderCount == 0;

    public bool Qualified =>
        Fills.Any() &&
        OpeningFilledQuantity > 0m &&
        FlattenFilledQuantity == OpeningFilledQuantity &&
        Flat &&
        CriticalBreakCount == 0 &&
        Issues.Count == 0;
}

public static class Arch7bKnownOrderQualification
{
    public static Arch7bPreflightDecision EvaluatePreflight(Arch7bPreflightInput input)
        => EvaluatePreflight(input, Arch7bKnownOrderExecutionProfile.Demo);

    public static Arch7bPreflightDecision EvaluatePreflight(
        Arch7bPreflightInput input, Arch7bKnownOrderExecutionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(profile);
        var demoProfile = profile == Arch7bKnownOrderExecutionProfile.Demo;
        var blockers = new List<string>();
        var child = input.ChildOrder;
        var bbo = input.Bbo;
        Require(input.ConfiguredEnvironment == profile.Environment,
            demoProfile ? "ARCH7B_ENVIRONMENT_NOT_TEST" : "ARCH7B_ENVIRONMENT_BINDING_MISMATCH");
        Require(input.ConfiguredAccountId == profile.AccountId,
            demoProfile ? "ARCH7B_DEMO_ACCOUNT_IDENTITY_MISMATCH" : "ARCH7B_ACCOUNT_BINDING_MISMATCH");
        if (demoProfile)
        {
            Require(input.ConfiguredAccountId != Arch7bKnownOrderQualificationPolicy.ForbiddenRealAccountId,
                "ARCH7B_REAL_ACCOUNT_FORBIDDEN");
            Require(child.AccountScope != Arch7bKnownOrderQualificationPolicy.ForbiddenRealAccountId,
                "ARCH7B_REAL_ACCOUNT_FORBIDDEN");
        }
        Require(child.Environment == profile.Environment, "ARCH7B_CHILD_ENVIRONMENT_MISMATCH");
        Require(child.AccountScope == profile.AccountId, "ARCH7B_CHILD_ACCOUNT_SCOPE_MISMATCH");
        Require(child.AccountScope == input.ConfiguredAccountId, "ARCH7B_CHILD_CONFIGURED_ACCOUNT_MISMATCH");
        Require(child.Symbol == profile.Symbol, "ARCH7B_SELECTED_SYMBOL_MISMATCH");
        Require(child.SecurityId == profile.SecurityId, "ARCH7B_SECURITY_ID_MISMATCH");
        Require(child.SecurityIdSource == profile.SecurityIdSource, "ARCH7B_SECURITY_ID_SOURCE_MISMATCH");
        Require(child.Side == profile.OpeningSide, "ARCH7B_OPENING_SIDE_MISMATCH");
        Require(child.SourceQuantity >= profile.VenueQuantity, "ARCH7B_CHILD_QUANTITY_TOO_SMALL");
        Require(child.LatestQualifyingRevision, "ARCH7B_SOURCE_NOT_LATEST_QUALIFYING_REVISION");
        Require(child.EconomicRevisionNumber == 2, "ARCH7B_ECONOMIC_REVISION_TWO_REQUIRED");
        Require(child.SourceCompleted, "ARCH7B_SOURCE_NOT_COMPLETED");
        Require(child.SourceFresh, "ARCH7B_SOURCE_NOT_FRESH");
        Require(!child.SourceSuperseded, "ARCH7B_SOURCE_SUPERSEDED");
        Require(child.LmaxMarketData, "ARCH7B_SOURCE_NOT_LMAX_MARKET_DATA");
        Require(!child.PolygonOrderPrice, "ARCH7B_POLYGON_ORDER_PRICE_FORBIDDEN");
        Require(child.LineageComplete, "ARCH7B_SOURCE_LINEAGE_INCOMPLETE");
        Require(
            child.TradeIntentClassification ==
                Arch7aPmsShadowExecutionContract.ShadowTradeIntentClassification,
            "ARCH7B_TRADE_INTENT_CLASSIFICATION_MISMATCH");
        Require(child.ParentStatus == "SHADOW_PLANNED", "ARCH7B_PARENT_STATUS_MISMATCH");
        Require(child.ChildStatus == "SHADOW_ONLY", "ARCH7B_CHILD_STATUS_MISMATCH");
        Require(bbo.Source == "LMAX", "ARCH7B_BBO_SOURCE_NOT_LMAX");
        Require(bbo.Symbol == profile.Symbol && bbo.SecurityId == profile.SecurityId, "ARCH7B_BBO_INSTRUMENT_MISMATCH");
        Require(bbo.Bid > 0m && bbo.Ask >= bbo.Bid, "ARCH7B_BBO_INVALID");
        Require(bbo.SequenceIntegrityProven, "ARCH7B_BBO_SEQUENCE_INTEGRITY_UNPROVEN");
        Require(!bbo.PolygonUsed, "ARCH7B_POLYGON_ORDER_PRICE_FORBIDDEN");
        Require(IsTickAligned(bbo.Bid, profile) && IsTickAligned(bbo.Ask, profile), "ARCH7B_BBO_NOT_TICK_ALIGNED");
        Require(bbo.Spread <= MaximumSpread(profile), "ARCH7B_BBO_SPREAD_TOO_WIDE");
        Require(bbo.AcquisitionStartedAtUtc <= bbo.ObservedAtUtc, "ARCH7B_BBO_TIME_ORDER_INVALID");
        Require(input.EvaluationTimeUtc - bbo.ObservedAtUtc <= TimeSpan.FromSeconds(profile.MaximumBboAgeSeconds),
            "ARCH7B_BBO_STALE");
        Require(input.CurrentKnownPosition == 0m, "ARCH7B_INITIAL_POSITION_NOT_FLAT");
        Require(input.PlatformKnownWorkingOrderCount == 0, "ARCH7B_PLATFORM_KNOWN_WORKING_ORDER_PRESENT");
        Require(input.ExactOperatorAuthorizationPresent, "ARCH7B_EXACT_OPERATOR_AUTHORIZATION_MISSING");
        Require(input.KillSwitchArmed, "ARCH7B_KILL_SWITCH_NOT_ARMED");
        Require(input.Exclusivity.AdvisoryLeaseHeld, "ARCH7B_EXCLUSIVITY_LEASE_NOT_HELD");
        Require(input.Exclusivity.ExpiresAtUtc > input.EvaluationTimeUtc, "ARCH7B_EXCLUSIVITY_LEASE_EXPIRED");
        Require(input.Exclusivity.NoManualOrdersDeclared &&
                input.Exclusivity.NoOtherBotDeclared &&
                input.Exclusivity.NoOtherGatewayDeclared &&
                input.Exclusivity.NoOtherUserDeclared &&
                input.Exclusivity.NoOtherTestDeclared &&
                input.Exclusivity.NoConcurrentFixOrderEntrySessionDeclared &&
                input.Exclusivity.OneRunOnly &&
                input.Exclusivity.OneDemoAccountOnly,
            "ARCH7B_EXCLUSIVITY_DECLARATION_INCOMPLETE");

        var runIdentity = string.Join("|",
            profile.Gate,
            profile.Environment,
            profile.AccountId,
            child.SlotId,
            child.TradeIntentId.ToString("D"),
            child.ParentOrderId.ToString("D"),
            child.ChildOrderId.ToString("D"),
            "v1");
        var openingId = DeterministicClientOrderId("A7BO", runIdentity);
        var flattenId = DeterministicClientOrderId("A7BF", runIdentity);
        var cancelId = DeterministicClientOrderId("A7BC", runIdentity);
        var openingLimit = child.Side == "BUY" ? bbo.Ask : bbo.Bid;
        var collar = profile.CollarPips * profile.PriceIncrement * 10m;
        var maximumOpeningPrice = bbo.Ask + collar;
        var minimumOpeningPrice = Math.Max(profile.PriceIncrement, bbo.Bid - collar);
        var policySha = Sha256(string.Join("|",
            runIdentity,
            openingId,
            flattenId,
            cancelId,
            openingLimit.ToString("G29", CultureInfo.InvariantCulture),
            maximumOpeningPrice.ToString("G29", CultureInfo.InvariantCulture),
            minimumOpeningPrice.ToString("G29", CultureInfo.InvariantCulture),
            profile.OpeningLimitPolicy,
            profile.FlattenLimitPolicy));

        return new(
            blockers.Count == 0,
            blockers,
            openingId,
            flattenId,
            cancelId,
            openingLimit,
            maximumOpeningPrice,
            minimumOpeningPrice,
            policySha);

        void Require(bool condition, string blocker)
        {
            if (!condition)
                blockers.Add(blocker);
        }
    }

    public static Arch7bLifecycleEvaluation EvaluateLifecycle(
        IEnumerable<Arch7bExecutionReportEvent> reports,
        string openingClientOrderId,
        string flattenClientOrderId,
        string? cancelClientOrderId = null,
        bool fullFixSessionSequenceValidated = false)
        => EvaluateLifecycle(reports, openingClientOrderId, flattenClientOrderId,
            Arch7bKnownOrderExecutionProfile.Demo, cancelClientOrderId,
            fullFixSessionSequenceValidated);

    public static Arch7bLifecycleEvaluation EvaluateLifecycle(
        IEnumerable<Arch7bExecutionReportEvent> reports,
        string openingClientOrderId,
        string flattenClientOrderId,
        Arch7bKnownOrderExecutionProfile profile,
        string? cancelClientOrderId = null,
        bool fullFixSessionSequenceValidated = false)
    {
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentNullException.ThrowIfNull(profile);
        var knownIds = new HashSet<string>(StringComparer.Ordinal)
        {
            openingClientOrderId,
            flattenClientOrderId
        };
        if (!string.IsNullOrWhiteSpace(cancelClientOrderId))
            knownIds.Add(cancelClientOrderId);

        var issues = new List<string>();
        var accepted = new List<Arch7bExecutionReportEvent>();
        var fills = new List<Arch7bValidatedFill>();
        var seenHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var seenExecIds = new Dictionary<string, Arch7bExecutionReportEvent>(StringComparer.Ordinal);
        var sequences = new Dictionary<string, SortedDictionary<long, Arch7bExecutionReportEvent>>(StringComparer.Ordinal);

        foreach (var report in reports
                     .OrderBy(value => value.TransactTimeUtc)
                     .ThenBy(value => value.SessionId, StringComparer.Ordinal)
                     .ThenBy(value => value.SequenceNumber)
                     .ThenBy(value => value.RawMessageSha256, StringComparer.Ordinal))
        {
            ValidateReport(report, knownIds, issues, profile);
            if (seenHashes.TryGetValue(report.RawMessageSha256, out var existingHashExec))
            {
                if (!existingHashExec.Equals(report.ExecId, StringComparison.Ordinal))
                    issues.Add("ARCH7B_DUPLICATE_MESSAGE_SHA_CONFLICT");
                continue;
            }
            seenHashes[report.RawMessageSha256] = report.ExecId;

            if (!sequences.TryGetValue(report.SessionId, out var sessionSequences))
            {
                sessionSequences = [];
                sequences[report.SessionId] = sessionSequences;
            }
            if (sessionSequences.TryGetValue(report.SequenceNumber, out var existingSequenceReport))
            {
                if (!(report.PossDup || existingSequenceReport.PossDup) ||
                    !SemanticallyEquivalent(existingSequenceReport, report))
                    issues.Add($"ARCH7B_CONFLICTING_FIX_SEQUENCE:{report.SessionId}:{report.SequenceNumber}");
                continue;
            }
            sessionSequences[report.SequenceNumber] = report;

            if (seenExecIds.TryGetValue(report.ExecId, out var existingExecReport))
            {
                if (!(report.PossDup || existingExecReport.PossDup) ||
                    !SemanticallyEquivalent(existingExecReport, report))
                    issues.Add($"ARCH7B_DUPLICATE_EXEC_ID_CONFLICT:{report.ExecId}");
                continue;
            }
            seenExecIds[report.ExecId] = report;
            accepted.Add(report);

            if (!IsFill(report))
                continue;
            var signed = report.Side == "BUY" ? report.LastQty : -report.LastQty;
            fills.Add(new(
                DeterministicFillId(report.ExecId, report.RawMessageSha256),
                report.ExecId,
                report.OrderId,
                CorrelatedLifecycleClientOrderId(
                    report,
                    openingClientOrderId,
                    flattenClientOrderId),
                report.Symbol,
                report.SecurityId,
                report.Side,
                report.LastQty,
                report.LastPx,
                report.TransactTimeUtc,
                report.RawMessageSha256,
                signed));
        }

        foreach (var pair in sequences.Where(_ => !fullFixSessionSequenceValidated))
        {
            var values = pair.Value.Keys.ToArray();
            for (var index = 1; index < values.Length; index++)
                if (values[index] > values[index - 1] + 1)
                    issues.Add($"ARCH7B_FIX_SEQUENCE_GAP:{pair.Key}:{values[index - 1] + 1}-{values[index] - 1}");
        }

        var orders = new[] { openingClientOrderId, flattenClientOrderId }
            .Select(clientOrderId =>
            {
                var latest = accepted
                    .Where(value => value.ClOrdId == clientOrderId ||
                                    value.OrigClOrdId == clientOrderId)
                    .OrderBy(value => value.TransactTimeUtc)
                    .ThenBy(value => value.SequenceNumber)
                    .LastOrDefault();
                return latest is null
                    ? null
                    : new Arch7bKnownOrderState(
                        clientOrderId, latest.OrderId, latest.OrdStatus, latest.ExecType,
                        latest.OrderQty, latest.CumQty, latest.LeavesQty,
                        IsTerminal(latest.OrdStatus),
                        !IsTerminal(latest.OrdStatus) && latest.LeavesQty > 0m);
            })
            .OfType<Arch7bKnownOrderState>()
            .ToArray();

        var openingFills = fills.Where(value => value.ClOrdId == openingClientOrderId).ToArray();
        var flattenFills = fills.Where(value => value.ClOrdId == flattenClientOrderId).ToArray();
        var openingQuantity = openingFills.Sum(value => value.Quantity);
        var flattenQuantity = flattenFills.Sum(value => value.Quantity);
        var internalPosition = fills.Sum(value => value.SignedQuantity);
        var residual = Math.Abs(internalPosition);
        var knownWorking = orders.Count(value => value.Working);
        if (openingQuantity == 0m)
            issues.Add("ARCH7B_OPENING_ORDER_NOT_FILLED");
        if (openingQuantity > 0m && flattenQuantity != openingQuantity)
            issues.Add("ARCH7B_FLATTEN_NOT_CONFIRMED");
        if (knownWorking > 0)
            issues.Add("ARCH7B_KNOWN_WORKING_LEAVES_REMAIN");
        if (internalPosition != 0m)
            issues.Add("ARCH7B_INTERNAL_POSITION_NOT_FLAT");
        if (orders.Any(value => !value.Terminal))
            issues.Add("ARCH7B_KNOWN_ORDER_NOT_TERMINAL");

        var openingOrder = orders.SingleOrDefault(value => value.ClOrdId == openingClientOrderId);
        var flattenOrder = orders.SingleOrDefault(value => value.ClOrdId == flattenClientOrderId);
        if (openingOrder is not null &&
            Math.Abs(openingOrder.CumQty - openingQuantity) > 0.00000001m)
            issues.Add("ARCH7B_OPENING_FILL_CUMQTY_DIVERGENCE_EMERGENCY_STOP");
        if (flattenOrder is not null &&
            Math.Abs(flattenOrder.CumQty - flattenQuantity) > 0.00000001m)
            issues.Add("ARCH7B_FLATTEN_FILL_CUMQTY_DIVERGENCE_EMERGENCY_STOP");

        decimal? realized = null;
        if (openingQuantity > 0m && flattenQuantity == openingQuantity)
        {
            var openingAverage = WeightedAverage(openingFills);
            var flattenAverage = WeightedAverage(flattenFills);
            realized = openingFills[0].Side == "BUY"
                ? (flattenAverage - openingAverage) * openingQuantity
                : (openingAverage - flattenAverage) * openingQuantity;
        }

        var distinctIssues = issues.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var criticalBreaks = distinctIssues.Length;
        var evaluationSha = Sha256(JsonSerializer.Serialize(new
        {
            reports = accepted.Select(value => new
            {
                value.SessionId,
                value.SequenceNumber,
                value.AccountId,
                value.OrderId,
                value.ClOrdId,
                value.OrigClOrdId,
                value.ExecId,
                value.ExecType,
                value.OrdStatus,
                value.Symbol,
                value.SecurityId,
                value.Side,
                value.OrderQty,
                value.CumQty,
                value.LeavesQty,
                value.LastQty,
                value.LastPx,
                value.AvgPx,
                value.Price,
                value.TransactTimeUtc,
                value.PossDup,
                value.RawMessageSha256
            }),
            fills,
            orders,
            internalPosition,
            openingQuantity,
            flattenQuantity,
            residual,
            realized,
            issues = distinctIssues
        }));

        return new(
            accepted,
            fills,
            orders,
            internalPosition,
            openingQuantity,
            flattenQuantity,
            residual,
            realized,
            "BROKER_FEES_UNAVAILABLE_NOT_ASSUMED_ZERO",
            knownWorking,
            criticalBreaks,
            distinctIssues,
            evaluationSha);
    }

    public static void ValidateBudget(Arch7bApplicationMessageBudget budget)
        => ValidateBudget(budget, Arch7bKnownOrderExecutionProfile.Demo);

    public static void ValidateBudget(
        Arch7bApplicationMessageBudget budget, Arch7bKnownOrderExecutionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(profile);
        if (budget.NewOrderSingleCount > profile.MaximumNewOrderSingleCount ||
            budget.CancelCount > profile.MaximumCancelCount ||
            budget.ReplaceCount > profile.MaximumReplaceCount ||
            budget.OrderStatusRequestCount > profile.MaximumOrderStatusRequestCount)
            throw new InvalidOperationException("ARCH7B_APPLICATION_MESSAGE_BUDGET_EXCEEDED");
    }

    public static string DeterministicClientOrderId(string prefix, string identity)
    {
        if (prefix.Length != 4 || prefix.Any(value => !char.IsLetterOrDigit(value)))
            throw new ArgumentException("ARCH7B_CLIENT_ORDER_ID_PREFIX_INVALID", nameof(prefix));
        return prefix.ToUpperInvariant() + Sha256(identity)[..16].ToUpperInvariant();
    }

    public static string ComputeAuthorizationPacketSha256(object packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        return Sha256(JsonSerializer.Serialize(packet));
    }

    public static decimal TouchLimit(Arch7bLmaxBbo bbo, string side)
        => TouchLimit(bbo, side, Arch7bKnownOrderExecutionProfile.Demo);

    public static decimal TouchLimit(
        Arch7bLmaxBbo bbo, string side, Arch7bKnownOrderExecutionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(bbo);
        ArgumentNullException.ThrowIfNull(profile);
        if (bbo.Bid <= 0m || bbo.Ask <= 0m || bbo.Bid > bbo.Ask)
            throw new InvalidOperationException("ARCH7B_BBO_INVALID");
        if (!IsTickAligned(bbo.Bid, profile) || !IsTickAligned(bbo.Ask, profile))
            throw new InvalidOperationException("ARCH7B_BBO_NOT_TICK_ALIGNED");
        if (bbo.Spread > MaximumSpread(profile))
            throw new InvalidOperationException("ARCH7B_BBO_SPREAD_TOO_WIDE");
        return side switch
        {
            "BUY" => bbo.Ask,
            "SELL" => bbo.Bid,
            _ => throw new InvalidOperationException("ARCH7B_ORDER_SIDE_INVALID")
        };
    }

    private static decimal MaximumSpread(Arch7bKnownOrderExecutionProfile profile)
        => profile.CollarPips * profile.PriceIncrement * 10m;

    private static bool IsTickAligned(decimal value, Arch7bKnownOrderExecutionProfile profile)
        => value % profile.PriceIncrement == 0m;

    private static void ValidateReport(
        Arch7bExecutionReportEvent report,
        IReadOnlySet<string> knownIds,
        ICollection<string> issues,
        Arch7bKnownOrderExecutionProfile profile)
    {
        if (!knownIds.Contains(report.ClOrdId) &&
            (string.IsNullOrWhiteSpace(report.OrigClOrdId) || !knownIds.Contains(report.OrigClOrdId)))
            issues.Add($"ARCH7B_UNKNOWN_CLORDID:{report.ClOrdId}");
        if (report.AccountId != profile.AccountId)
            issues.Add(profile == Arch7bKnownOrderExecutionProfile.Demo
                ? "ARCH7B_DEMO_ACCOUNT_IDENTITY_MISMATCH"
                : "ARCH7B_EXECUTION_REPORT_ACCOUNT_BINDING_MISMATCH");
        if (report.Symbol != profile.Symbol || report.SecurityId != profile.SecurityId)
            issues.Add("ARCH7B_EXECUTION_REPORT_INSTRUMENT_MISMATCH");
        if (report.SequenceNumber <= 0)
            issues.Add("ARCH7B_FIX_SEQUENCE_INVALID");
        if (report.RawMessageSha256.Length != 64 || !report.RawMessageSha256.All(Uri.IsHexDigit))
            issues.Add("ARCH7B_RAW_MESSAGE_SHA256_INVALID");
        if (string.IsNullOrWhiteSpace(report.OrderId) ||
            string.IsNullOrWhiteSpace(report.ClOrdId) ||
            string.IsNullOrWhiteSpace(report.ExecId))
            issues.Add("ARCH7B_EXECUTION_REPORT_IDENTITY_INCOMPLETE");
        if (report.OrderQty < 0m || report.CumQty < 0m || report.LeavesQty < 0m ||
            report.LastQty < 0m || report.LastPx < 0m)
            issues.Add("ARCH7B_EXECUTION_REPORT_NUMERIC_INVALID");
        if (report.OrderQty > 0m)
        {
            var quantityValid = report.OrdStatus switch
            {
                "2" => report.LeavesQty == 0m && report.CumQty == report.OrderQty,
                "4" or "8" or "C" =>
                    report.LeavesQty == 0m && report.CumQty <= report.OrderQty,
                _ => Math.Abs(report.OrderQty - report.CumQty - report.LeavesQty) <= 0.00000001m
            };
            if (!quantityValid)
                issues.Add("ARCH7B_EXECUTION_REPORT_QUANTITY_INCONSISTENT");
        }
    }

    private static bool IsFill(Arch7bExecutionReportEvent report)
        => report.ExecType == "F" &&
           report.OrdStatus is "1" or "2" &&
           report.LastQty > 0m &&
           report.LastPx > 0m;

    private static string CorrelatedLifecycleClientOrderId(
        Arch7bExecutionReportEvent report,
        string openingClientOrderId,
        string flattenClientOrderId)
    {
        if (report.ClOrdId == openingClientOrderId ||
            report.OrigClOrdId == openingClientOrderId)
            return openingClientOrderId;
        if (report.ClOrdId == flattenClientOrderId ||
            report.OrigClOrdId == flattenClientOrderId)
            return flattenClientOrderId;
        return report.ClOrdId;
    }

    private static bool SemanticallyEquivalent(
        Arch7bExecutionReportEvent left,
        Arch7bExecutionReportEvent right)
        => left.AccountId == right.AccountId &&
           left.OrderId == right.OrderId &&
           left.ClOrdId == right.ClOrdId &&
           left.OrigClOrdId == right.OrigClOrdId &&
           left.ExecId == right.ExecId &&
           left.ExecType == right.ExecType &&
           left.OrdStatus == right.OrdStatus &&
           left.Symbol == right.Symbol &&
           left.SecurityId == right.SecurityId &&
           left.Side == right.Side &&
           left.OrderQty == right.OrderQty &&
           left.CumQty == right.CumQty &&
           left.LeavesQty == right.LeavesQty &&
           left.LastQty == right.LastQty &&
           left.LastPx == right.LastPx &&
           left.AvgPx == right.AvgPx &&
           left.Price == right.Price &&
           left.TransactTimeUtc == right.TransactTimeUtc;

    private static bool IsTerminal(string ordStatus)
        => ordStatus is "2" or "4" or "8" or "C";

    private static decimal WeightedAverage(IReadOnlyList<Arch7bValidatedFill> fills)
    {
        var quantity = fills.Sum(value => value.Quantity);
        return quantity == 0m ? 0m : fills.Sum(value => value.Quantity * value.Price) / quantity;
    }

    private static string DeterministicFillId(string execId, string rawMessageSha256)
        => "A7BFL" + Sha256($"{execId}|{rawMessageSha256}")[..27].ToUpperInvariant();

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
