namespace QQ.Production.Intraday.Application;

public sealed record SandboxTargetNotionalPolicyEvidence(
    string PolicyId,
    string PolicySource,
    decimal TargetNotionalAmount,
    string TargetNotionalCurrency,
    string TargetNotionalScope,
    bool SandboxOnly,
    bool NotProduction,
    bool NotAccounting,
    bool NotAccountCurrency,
    bool NotAumAccounting,
    bool NotNav,
    bool NotLedgerCapital);

public sealed record SandboxTargetNotionalPolicyValidation(
    string Classification,
    bool ReadyForSandboxPreviewSizing,
    IReadOnlyList<string> Blockers);

public static class SandboxTargetNotionalPolicyValidator
{
    public static SandboxTargetNotionalPolicyValidation Validate(SandboxTargetNotionalPolicyEvidence policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var blockers = new List<string>();

        if (policy.TargetNotionalAmount <= 0m)
        {
            blockers.Add("TargetNotionalMustBePositive");
        }

        if (!string.Equals(policy.TargetNotionalCurrency, "USD", StringComparison.Ordinal))
        {
            blockers.Add("TargetNotionalCurrencyMustBeUsdForR007");
        }

        if (!string.Equals(policy.TargetNotionalScope, "SandboxPreviewSizingOnly", StringComparison.Ordinal))
        {
            blockers.Add("TargetNotionalScopeMustBeSandboxPreviewSizingOnly");
        }

        if (!policy.SandboxOnly || !policy.NotProduction || !policy.NotAccounting || !policy.NotAccountCurrency || !policy.NotAumAccounting || !policy.NotNav || !policy.NotLedgerCapital)
        {
            blockers.Add("TargetNotionalBoundaryFlagsIncomplete");
        }

        if (blockers.Count > 0)
        {
            return new SandboxTargetNotionalPolicyValidation(
                "SANDBOX_TARGET_NOTIONAL_POLICY_CONTRADICTORY",
                ReadyForSandboxPreviewSizing: false,
                Blockers: blockers);
        }

        return new SandboxTargetNotionalPolicyValidation(
            "SANDBOX_TARGET_NOTIONAL_POLICY_READY_OPERATOR_PROVIDED",
            ReadyForSandboxPreviewSizing: true,
            Blockers: []);
    }
}

public sealed record SandboxInstrumentMetadata(
    string Symbol,
    decimal ContractMultiplier,
    decimal MinOrderSize,
    string QuotedCurrency);

public sealed record SandboxPriceBasis(
    string Symbol,
    decimal Price,
    string Source);

public sealed record SandboxTargetNotionalSizingPolicy(
    decimal? SandboxTargetNotional,
    string PolicyStatus,
    string RoundingPolicy);

public sealed record SandboxQuantityTransformRequest(
    SandboxQubesExecutionTransformResult Transform,
    SandboxTargetNotionalSizingPolicy SizingPolicy,
    IReadOnlyDictionary<string, SandboxInstrumentMetadata> InstrumentMetadata,
    IReadOnlyDictionary<string, SandboxPriceBasis> PriceBasis);

public sealed record SandboxQuantityTransformLine(
    string Symbol,
    string Side,
    decimal SourceWeight,
    decimal? Quantity,
    decimal? RoundedQuantity,
    string QuantityStatus);

public sealed record SandboxQuantityTransformResult(
    string Classification,
    bool ExecutionReadyPreview,
    IReadOnlyList<SandboxQuantityTransformLine> Lines,
    IReadOnlyList<string> Blockers);

public sealed class SandboxQubesQuantityTransformer
{
    public SandboxQuantityTransformResult Transform(SandboxQuantityTransformRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Transform);
        ArgumentNullException.ThrowIfNull(request.SizingPolicy);

        if (request.SizingPolicy.SandboxTargetNotional is null or <= 0m)
        {
            return Blocked(
                "QUANTITY_TRANSFORMATION_BLOCKED_MISSING_TARGET_NOTIONAL",
                request.Transform.ExecutionLines,
                ["MissingExplicitSandboxTargetNotional"]);
        }

        var blockers = new List<string>();
        var lines = new List<SandboxQuantityTransformLine>();

        foreach (var line in request.Transform.ExecutionLines.Where(line => line.Side is "BUY" or "SELL"))
        {
            if (!request.InstrumentMetadata.TryGetValue(line.ExecutionTradableSymbol, out var metadata))
            {
                blockers.Add($"MissingInstrumentMetadata:{line.ExecutionTradableSymbol}");
                lines.Add(new SandboxQuantityTransformLine(line.ExecutionTradableSymbol, line.Side, line.SourceWeight, null, null, "MissingInstrumentMetadata"));
                continue;
            }

            if (!request.PriceBasis.TryGetValue(line.ExecutionTradableSymbol, out var priceBasis) || priceBasis.Price <= 0m)
            {
                blockers.Add($"MissingPriceOrMarkSource:{line.ExecutionTradableSymbol}");
                lines.Add(new SandboxQuantityTransformLine(line.ExecutionTradableSymbol, line.Side, line.SourceWeight, null, null, "MissingPriceOrMarkSource"));
                continue;
            }

            var targetQuoteNotional = Math.Abs(line.SourceWeight) * request.SizingPolicy.SandboxTargetNotional.Value;
            var rawQuantity = targetQuoteNotional / (metadata.ContractMultiplier * priceBasis.Price);
            var roundedQuantity = RoundDownToStep(rawQuantity, metadata.MinOrderSize);

            if (rawQuantity > 0m && roundedQuantity < metadata.MinOrderSize)
            {
                roundedQuantity = metadata.MinOrderSize;
            }

            lines.Add(new SandboxQuantityTransformLine(
                line.ExecutionTradableSymbol,
                line.Side,
                line.SourceWeight,
                rawQuantity,
                roundedQuantity,
                "ReadyWithExplicitTargetNotionalPriceAndMetadata"));
        }

        if (blockers.Any(blocker => blocker.StartsWith("MissingInstrumentMetadata:", StringComparison.Ordinal)))
        {
            return new SandboxQuantityTransformResult(
                "QUANTITY_TRANSFORMATION_BLOCKED_MISSING_INSTRUMENT_METADATA",
                ExecutionReadyPreview: false,
                Lines: lines.OrderBy(line => line.Symbol, StringComparer.Ordinal).ToArray(),
                Blockers: blockers.Order(StringComparer.Ordinal).ToArray());
        }

        if (blockers.Any(blocker => blocker.StartsWith("MissingPriceOrMarkSource:", StringComparison.Ordinal)))
        {
            return new SandboxQuantityTransformResult(
                "QUANTITY_TRANSFORMATION_BLOCKED_MISSING_PRICE_OR_MARK_SOURCE",
                ExecutionReadyPreview: false,
                Lines: lines.OrderBy(line => line.Symbol, StringComparer.Ordinal).ToArray(),
                Blockers: blockers.Order(StringComparer.Ordinal).ToArray());
        }

        return new SandboxQuantityTransformResult(
            "QUANTITY_TRANSFORMATION_READY_WITH_EXPLICIT_TARGET_NOTIONAL_AND_METADATA",
            ExecutionReadyPreview: true,
            Lines: lines.OrderBy(line => line.Symbol, StringComparer.Ordinal).ToArray(),
            Blockers: []);
    }

    private static SandboxQuantityTransformResult Blocked(
        string classification,
        IReadOnlyList<SandboxQubesExecutionTransformLine> executionLines,
        IReadOnlyList<string> blockers)
    {
        return new SandboxQuantityTransformResult(
            classification,
            ExecutionReadyPreview: false,
            Lines: executionLines
                .Where(line => line.Side is "BUY" or "SELL")
                .Select(line => new SandboxQuantityTransformLine(line.ExecutionTradableSymbol, line.Side, line.SourceWeight, null, null, "Blocked"))
                .OrderBy(line => line.Symbol, StringComparer.Ordinal)
                .ToArray(),
            Blockers: blockers);
    }

    private static decimal RoundDownToStep(decimal value, decimal step)
    {
        if (step <= 0m)
        {
            throw new InvalidOperationException("Min order size must be positive for sandbox quantity transformation.");
        }

        return Math.Floor(value / step) * step;
    }
}
