using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public sealed class Arch7aPmsShadowExecutionPipeline
{
    private const string Venue = "LMAX";
    private const string IntentVersion = "arch7a-shadow-intent-v1";
    private const decimal ZeroTolerance = 0.0000000001m;

    private static readonly IReadOnlyDictionary<string, (string SecurityId, string SecurityIdSource)> ProvenSymbols =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["EURUSD"] = ("4001", "8"),
            ["GBPUSD"] = ("4002", "8"),
            ["AUDUSD"] = ("4007", "8"),
            ["USDJPY"] = ("4004", "8"),
            ["NZDUSD"] = ("100613", "8"),
            ["USDCAD"] = ("4013", "8"),
            ["USDCHF"] = ("4010", "8")
        };

    public Arch7aShadowExecutionPlan Build(Arch7aPmsExecutionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source.Slot.RequireCanonical();
        ValidateSourceShape(source);

        var blockers = SourceBlockers(source);
        var netting = BuildNetting(source);
        blockers.AddRange(netting.UnsupportedCurrencies.Select(value => $"UNSUPPORTED_EXECUTION_CURRENCY:{value}"));
        var sourceConstructionBlocked = blockers.Any(value => value is
            "SOURCE_SESSION_NOT_COMPLETED" or "SOURCE_SESSION_STALE" or "SOURCE_LINEAGE_INCOMPLETE" or
            "REAL_ACCOUNT_REJECTED" or "NON_TEST_ENVIRONMENT_REJECTED" or
            "WORKING_LEAVES_POLICY_FORBIDS_CONSTRUCTION") ||
            netting.UnsupportedCurrencies.Count > 0;

        var phases = ExecutionAlgoR001Foundation.CreateFixture().CloseSeeking15mPhases;
        Arch7aShadowExecutionUnit[] units = sourceConstructionBlocked
            ? []
            : netting.ExecutionLines
                .Where(line => Math.Abs(line.SignedDesiredDelta) > ZeroTolerance)
                .Select(line => BuildUnit(source, line, phases, blockers))
                .OrderBy(unit => unit.TradeIntent.ExecutionTradableSymbol, StringComparer.Ordinal)
                .ToArray();

        var planHash = Hash(string.Join("\n",
            source.IngestionId.ToString("D"),
            source.SourceSessionId,
            source.Slot.SlotId,
            source.Slot.TargetCloseUtc.UtcDateTime.ToString("O"),
            netting.NettingSha256,
            string.Join("|", units.Select(value => value.TradeIntent.IdempotencyKey)),
            string.Join("|", blockers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))));

        return new Arch7aShadowExecutionPlan(
            netting,
            units,
            phases,
            blockers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            NetworkLedger: [],
            planHash,
            NoFixLogon: true,
            NoBrokerSend: true,
            NoAccountApi: true,
            NoDatabento: true,
            NoRealAccount: true,
            NoFill: true,
            NoPositionLedgerEvent: true);
    }

    private static Arch7aExecutionNettingManifest BuildNetting(Arch7aPmsExecutionSource source)
    {
        var rawLines = source.Contributions
            .OrderBy(value => value.StrategyId, StringComparer.Ordinal)
            .ThenBy(value => value.SecurityId, StringComparer.Ordinal)
            .Select(value =>
                $"{NormalizeSymbol(value.PortfolioSymbol)} Curncy;{value.TargetWeight.ToString("G29", CultureInfo.InvariantCulture)}")
            .ToArray();

        var ingestion = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(
            new QubesFxWeightsIngestionRequest(
                new QubesRunId($"{source.SourceSessionId}:{source.Slot.SlotId}"),
                source.CompletedAtUtc,
                source.Slot.EffectiveFromUtc,
                15,
                source.AccountScope,
                "ARCH7A_SHADOW_EXECUTION",
                source.NavUsd,
                TargetQuantityMode.PortfolioBaseCurrencyNotional,
                rawLines));
        if (!ingestion.Succeeded)
            throw new InvalidOperationException(
                $"ARCH7A_EXISTING_NETTING_REJECTED:{string.Join(',', ingestion.Issues.Select(value => value.Code))}");

        var contributions = source.Contributions
            .SelectMany(ToCurrencyContributions)
            .OrderBy(value => value.Currency, StringComparer.Ordinal)
            .ThenBy(value => value.StrategyId, StringComparer.Ordinal)
            .ThenBy(value => value.SourceSymbol, StringComparer.Ordinal)
            .ToArray();
        var lines = new List<Arch7aExecutionNettingLine>();
        var unsupported = new List<string>();

        foreach (var normalized in ingestion.NormalizedWeights.OrderBy(value => value.Currency, StringComparer.Ordinal))
        {
            var mapping = ExecutionAlgoR002UsdPairSelectionPolicy.MapCurrency(normalized.Currency);
            if (mapping.ExecutionTradableSymbol is null ||
                !ProvenSymbols.TryGetValue(mapping.ExecutionTradableSymbol, out var identity))
            {
                unsupported.Add(normalized.Currency);
                continue;
            }

            if (!source.ExecutionMidPrices.TryGetValue(mapping.ExecutionTradableSymbol, out var mid) || mid <= 0m)
                throw new InvalidOperationException($"ARCH7A_EXECUTION_MID_MISSING:{mapping.ExecutionTradableSymbol}");

            var targetNotionalUsd = normalized.Weight * source.NavUsd;
            var targetQuantity = mapping.RequiresInversion ? -targetNotionalUsd : targetNotionalUsd / mid;
            var currentQuantity =
                source.ReconciledCurrentExecutionQuantities.GetValueOrDefault(mapping.ExecutionTradableSymbol);
            var increment = source.QuantityIncrements.GetValueOrDefault(mapping.ExecutionTradableSymbol, 0.0001m);
            var roundedTarget = QuantityRounding.RoundToStep(targetQuantity, increment);
            lines.Add(new Arch7aExecutionNettingLine(
                normalized.Currency,
                mapping.PortfolioNormalizedSymbol,
                mapping.ExecutionTradableSymbol,
                mapping.RequiresInversion,
                identity.SecurityId,
                identity.SecurityIdSource,
                normalized.Weight,
                roundedTarget,
                currentQuantity,
                roundedTarget - currentQuantity,
                increment,
                contributions
                    .Where(value => value.Currency.Equals(normalized.Currency, StringComparison.OrdinalIgnoreCase))
                    .ToArray()));
        }

        var excludedCrosses = source.Contributions
            .Select(value => NormalizeSymbol(value.PortfolioSymbol))
            .Where(SandboxQubesExecutionUniverseTransformer.IsDirectCross)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var hash = Hash(string.Join("\n",
            source.SourceSessionId,
            source.Slot.SlotId,
            string.Join("|", contributions.Select(value =>
                $"{value.ModelRunId:D}:{value.SourceSymbol}:{value.Currency}:{value.SignedWeightContribution:G29}")),
            string.Join("|", ingestion.CurrencyExposures.Select(value => $"{value.Key}:{value.Value:G29}")),
            string.Join("|", lines.Select(value =>
                $"{value.ExecutionTradableSymbol}:{value.TargetExecutionQuantity:G29}:{value.CurrentExecutionQuantity:G29}:{value.SignedDesiredDelta:G29}"))));

        return new Arch7aExecutionNettingManifest(
            source.SourceSessionId,
            source.Slot.SlotId,
            ingestion.CurrencyExposures,
            contributions,
            lines.OrderBy(value => value.ExecutionTradableSymbol, StringComparer.Ordinal).ToArray(),
            excludedCrosses,
            unsupported.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToArray(),
            hash,
            DirectCrossExecutionDisabled: true,
            Deterministic: true);
    }

    private static Arch7aShadowExecutionUnit BuildUnit(
        Arch7aPmsExecutionSource source,
        Arch7aExecutionNettingLine line,
        IReadOnlyList<CloseSeeking15mPhase> phases,
        ICollection<string> blockers)
    {
        var identity = string.Join("|",
            source.Environment,
            source.AccountScope,
            Venue,
            source.SourceSessionId,
            source.Slot.SlotId,
            source.Slot.TargetCloseUtc.UtcDateTime.ToString("O"),
            line.ExecutionTradableSymbol,
            IntentVersion);
        var intentId = new TradeIntentId(DeterministicGuid($"intent|{identity}"));
        var modelIds = line.Contributions.Select(value => value.ModelRunId).Distinct().Order().ToArray();
        var targetIds = line.Contributions.Select(value => value.TargetPositionId).Distinct().Order().ToArray();
        var driftIds = line.Contributions.Select(value => value.DriftId).Distinct().Order().ToArray();
        var lineageHash = Hash(
            string.Join("|", modelIds.Select(value => value.ToString("D"))) + "\n" +
            string.Join("|", targetIds.Select(value => value.ToString("D"))) + "\n" +
            string.Join("|", driftIds.Select(value => value.ToString("D"))));

        var workingUnknown =
            source.WorkingOrderAuthority != Arch7aWorkingOrderAuthority.AuthoritativeComplete;
        var riskOutcome = source.HasCriticalConflict
            ? Arch7aShadowRiskOutcome.EMERGENCY_STOP
            : workingUnknown || !source.PositionAuthority
                ? Arch7aShadowRiskOutcome.BLOCK_NEW_ORDERS
                : Arch7aShadowRiskOutcome.APPROVED_SHADOW;
        var blockingReason = source.HasCriticalConflict
            ? "CRITICAL_RECONCILIATION_CONFLICT"
            : workingUnknown
                ? "BROKER_WORKING_LEAVES_UNOBSERVABLE"
                : !source.PositionAuthority
                    ? "BROKER_POSITION_AUTHORITY_UNAVAILABLE"
                    : null;
        if (blockingReason is not null) blockers.Add(blockingReason);

        var canonicalIntent = new TradeIntent(
            intentId,
            new ModelRunId(modelIds[0]),
            new FundId(DeterministicGuid($"fund|{source.Environment}|{source.AccountScope}")),
            new InstrumentId(DeterministicGuid($"instrument|{line.SecurityId}")),
            line.SignedDesiredDelta > 0m ? TradeSide.Buy : TradeSide.Sell,
            Math.Abs(line.SignedDesiredDelta),
            Math.Abs(line.SignedDesiredDelta),
            "ARCH7A PMS authoritative shadow netted drift",
            TradeIntentStatus.ShadowOnly,
            source.Slot.EffectiveFromUtc);
        var actionable = riskOutcome == Arch7aShadowRiskOutcome.APPROVED_SHADOW && !workingUnknown;
        var intent = new Arch7aTradeIntentEnvelope(
            canonicalIntent,
            source.IngestionId,
            source.SourceSessionId,
            source.Slot.SlotId,
            source.Slot.OperationalDate,
            source.Slot.TargetCloseUtc,
            source.Slot.EffectiveFromUtc,
            source.Slot.DeadlineUtc,
            modelIds,
            targetIds,
            driftIds,
            line.SecurityId,
            line.SecurityIdSource,
            line.NormalizedPortfolioSymbol,
            line.ExecutionTradableSymbol,
            line.RequiresInversion,
            line.SignedDesiredDelta,
            line.TargetExecutionQuantity,
            line.CurrentExecutionQuantity,
            source.AccountScope,
            source.Environment,
            "SHADOW_ONLY",
            Actionable: actionable,
            ExecutionAllowed: false,
            BrokerRouteAllowed: false,
            blockingReason,
            Hash($"idempotency|{identity}"),
            lineageHash);

        var riskId = DeterministicGuid($"risk|{identity}");
        var riskStatus = riskOutcome switch
        {
            Arch7aShadowRiskOutcome.APPROVED_SHADOW => RiskDecisionStatus.ApprovedShadow,
            Arch7aShadowRiskOutcome.BLOCK_NEW_ORDERS => RiskDecisionStatus.BlockNewOrders,
            _ => RiskDecisionStatus.EmergencyStop
        };
        var rejectReason = riskOutcome switch
        {
            Arch7aShadowRiskOutcome.APPROVED_SHADOW => RiskRejectReason.None,
            Arch7aShadowRiskOutcome.EMERGENCY_STOP => RiskRejectReason.CriticalReconciliationConflict,
            _ when workingUnknown => RiskRejectReason.BrokerWorkingLeavesUnobservable,
            _ => RiskRejectReason.UnknownCurrentPosition
        };
        var canonicalRisk = new RiskDecision(
            riskId,
            intentId,
            riskStatus,
            rejectReason,
            blockingReason ?? "Shadow risk checks passed; broker route remains disabled.",
            source.Slot.EffectiveFromUtc,
            ModelRunId: new ModelRunId(modelIds[0]),
            InstrumentId: canonicalIntent.InstrumentId,
            VenueId: new VenueId(DeterministicGuid($"venue|{Venue}")));
        var risk = new Arch7aRiskDecisionEnvelope(
            canonicalRisk,
            riskOutcome,
            blockingReason is null ? ["SHADOW_ONLY_NO_BROKER_ROUTE"] : [blockingReason],
            blockingReason is null ? [] : [blockingReason],
            SourceComplete: true,
            source.PositionAuthority,
            !workingUnknown,
            source.Freshness,
            ["EXISTING_RISK_POLICY", "SOURCE_COMPLETENESS", "POSITION_AUTHORITY",
             "WORKING_ORDER_AUTHORITY", "FRESHNESS", "NO_ORDER_INVARIANT"],
            NoOrderInvariant: true,
            BrokerSendAllowed: false);

        var parentId = new ParentOrderId(DeterministicGuid($"parent|{identity}"));
        var parentClientId = new ClientOrderId($"A7P{Hash(identity)[..16].ToUpperInvariant()}");
        var canonicalParent = new ParentOrder(
            parentId,
            intentId,
            parentClientId,
            canonicalIntent.Side == TradeSide.Buy ? OrderSide.Buy : OrderSide.Sell,
            Math.Abs(line.SignedDesiredDelta),
            ExecutionAlgo.CloseSeeking15m,
            OrderStatus.ShadowPlanned,
            source.Slot.EffectiveFromUtc);
        var parent = new Arch7aParentOrderEnvelope(
            canonicalParent,
            riskId,
            line.ExecutionTradableSymbol,
            Math.Abs(line.SignedDesiredDelta),
            source.Slot.TargetCloseUtc,
            "SHADOW_PLANNED",
            RouteAllowed: false,
            Hash($"parent|{identity}"));

        var childId = new ChildOrderId(DeterministicGuid($"child|{identity}|whole-shadow-preview"));
        var childClientId = new ClientOrderId(
            $"A7C{Hash($"child|{identity}")[..16].ToUpperInvariant()}");
        var canonicalChild = new ChildOrder(
            childId,
            parentId,
            new VenueId(DeterministicGuid($"venue|{Venue}")),
            childClientId,
            canonicalParent.Side,
            OrderType.Limit,
            TimeInForce.GFD,
            canonicalParent.BaseQuantity,
            canonicalParent.BaseQuantity,
            OrderStatus.ShadowOnly,
            source.Slot.EffectiveFromUtc);
        var firstPhase = phases.Single(value =>
            value.PhaseName == CloseSeekingPhaseName.PassiveOpportunistic);
        var child = new Arch7aChildOrderEnvelope(
            canonicalChild,
            "WHOLE_SHADOW_PREVIEW",
            source.ExecutionMidPrices[line.ExecutionTradableSymbol],
            source.Slot.TargetCloseUtc - firstPhase.StartsBeforeClose,
            source.Slot.TargetCloseUtc,
            firstPhase.PhaseName,
            "SHADOW_ONLY",
            BrokerSendAllowed: false,
            Hash($"child|{identity}|whole-shadow-preview"));

        return new Arch7aShadowExecutionUnit(intent, risk, parent, child);
    }

    private static List<string> SourceBlockers(Arch7aPmsExecutionSource source)
    {
        var blockers = new List<string>();
        if (source.Status != Arch7aSourceStatus.Completed) blockers.Add("SOURCE_SESSION_NOT_COMPLETED");
        if (source.Freshness != Arch7aSourceFreshness.Fresh) blockers.Add("SOURCE_SESSION_STALE");
        if (!source.LineageComplete) blockers.Add("SOURCE_LINEAGE_INCOMPLETE");
        if (!source.Environment.Equals("TEST", StringComparison.OrdinalIgnoreCase))
            blockers.Add("NON_TEST_ENVIRONMENT_REJECTED");
        if (source.AccountScope.Contains("REAL", StringComparison.OrdinalIgnoreCase) ||
            source.AccountScope.Contains("PROD", StringComparison.OrdinalIgnoreCase))
            blockers.Add("REAL_ACCOUNT_REJECTED");
        if (source.WorkingOrderAuthority != Arch7aWorkingOrderAuthority.AuthoritativeComplete &&
            !source.AllowShadowSimulationWhenWorkingLeavesUnknown)
            blockers.Add("WORKING_LEAVES_POLICY_FORBIDS_CONSTRUCTION");
        if (source.HasCriticalConflict) blockers.Add("CRITICAL_RECONCILIATION_CONFLICT");
        return blockers;
    }

    private static IEnumerable<Arch7aFxCurrencyContribution> ToCurrencyContributions(
        Arch7aPmsTargetContribution value)
    {
        var symbol = NormalizeSymbol(value.PortfolioSymbol);
        if (!QubesFxWeightsFixtureIngestionService.TryParseBloombergFxTicker(
                $"{symbol} Curncy", out _, out var baseCurrency, out var quoteCurrency))
            throw new InvalidOperationException($"ARCH7A_INVALID_FX_SYMBOL:{value.PortfolioSymbol}");
        yield return new(
            value.StrategyId,
            value.ModelRunId,
            value.TargetPositionId,
            value.DriftId,
            symbol,
            baseCurrency,
            value.TargetWeight);
        yield return new(
            value.StrategyId,
            value.ModelRunId,
            value.TargetPositionId,
            value.DriftId,
            symbol,
            quoteCurrency,
            -value.TargetWeight);
    }

    private static void ValidateSourceShape(Arch7aPmsExecutionSource source)
    {
        if (source.IngestionId == Guid.Empty)
            throw new InvalidOperationException("ARCH7A_INGESTION_ID_REQUIRED");
        if (string.IsNullOrWhiteSpace(source.SourceSessionId))
            throw new InvalidOperationException("ARCH7A_SOURCE_SESSION_ID_REQUIRED");
        if (source.CompletedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("ARCH7A_COMPLETED_AT_MUST_BE_UTC");
        if (source.NavUsd <= 0m)
            throw new InvalidOperationException("ARCH7A_NAV_MUST_BE_POSITIVE");
        if (source.Contributions.Count == 0)
            throw new InvalidOperationException("ARCH7A_TARGET_CONTRIBUTIONS_REQUIRED");
        if (source.Contributions.Any(value =>
                value.ModelRunId == Guid.Empty ||
                value.TargetPositionId == Guid.Empty ||
                value.DriftId == Guid.Empty))
            throw new InvalidOperationException("ARCH7A_LINEAGE_ID_REQUIRED");
        if (source.Contributions.Any(value =>
                !IsSha256(value.InputSha256) || !IsSha256(value.OutputSha256)))
            throw new InvalidOperationException("ARCH7A_ARTIFACT_SHA256_REQUIRED");
        if (source.Contributions.Any(value =>
                value.CoreCommitId.Length is not (40 or 64) ||
                !value.CoreCommitId.All(Uri.IsHexDigit)))
            throw new InvalidOperationException("ARCH7A_FULL_GIT_COMMIT_ID_REQUIRED");
    }

    private static string NormalizeSymbol(string value)
        => value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].ToUpperInvariant();

    private static bool IsSha256(string value)
        => value.Length == 64 && value.All(Uri.IsHexDigit);

    public static Guid DeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0F) | 0x50);
        guid[8] = (byte)((guid[8] & 0x3F) | 0x80);
        return new Guid(guid);
    }

    public static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}