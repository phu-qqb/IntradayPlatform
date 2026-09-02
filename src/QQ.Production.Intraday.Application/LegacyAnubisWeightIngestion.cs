using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public sealed record LegacyAnubisWeightIngestionRequest(
    string ProgramName,
    string ExecDeskWeightFilePath,
    string ExpectedExecDeskWeightFileSha256,
    string AggregatedWeightsFilePath,
    string ExpectedAggregatedWeightsFileSha256,
    string FundCode,
    string ModelName,
    DateTimeOffset AsOfUtc,
    DateTimeOffset EffectiveAtUtc,
    int FrequencyMinutes,
    decimal NavUsd,
    TargetQuantityMode TargetQuantityMode);

public sealed record LegacyAnubisWeightIngestionResult(
    ModelWeightBatch Batch,
    int SourceRowCount,
    int ExecutableRowCount,
    string ExecDeskWeightFileSha256,
    string AggregatedWeightsFileSha256,
    bool AlreadyExisted);

public interface ILegacyAnubisWeightIngestionService
{
    Task<LegacyAnubisWeightIngestionResult> IngestAsync(
        LegacyAnubisWeightIngestionRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Imports the manager-owned legacy Anubis exec-desk contract. The manager has already
/// performed security-id and OTC-direction mapping; this service only binds Bloomberg
/// ticker syntax to instruments that are already enabled in the execution platform.
/// </summary>
public sealed class LegacyAnubisWeightIngestionService(
    IModelWeightBatchRepository repository,
    IIntradayRepository intradayRepository,
    IClock clock) : ILegacyAnubisWeightIngestionService
{
    public async Task<LegacyAnubisWeightIngestionResult> IngestAsync(
        LegacyAnubisWeightIngestionRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var execDeskBytes = await File.ReadAllBytesAsync(request.ExecDeskWeightFilePath, cancellationToken);
        var aggregatedBytes = await File.ReadAllBytesAsync(request.AggregatedWeightsFilePath, cancellationToken);
        var execDeskSha256 = Sha256(execDeskBytes);
        var aggregatedSha256 = Sha256(aggregatedBytes);
        RequireHash("exec-desk weight file", request.ExpectedExecDeskWeightFileSha256, execDeskSha256);
        RequireHash("AggregatedWeights file", request.ExpectedAggregatedWeightsFileSha256, aggregatedSha256);

        var parsed = Parse(execDeskBytes);
        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        var enabledBySymbol = state.Instruments
            .Where(x => x.IsEnabled && x.IsTradingEnabled)
            .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        if (enabledBySymbol.Any(x => x.Value.Count != 1))
            throw new DomainRuleViolationException("Enabled execution instrument symbols are ambiguous.");

        var executable = parsed
            .Where(x => enabledBySymbol.ContainsKey(x.Symbol))
            .ToList();
        if (executable.Count == 0)
            throw new DomainRuleViolationException("The manager-owned weight file has no rows for enabled execution instruments.");
        if (executable.GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() != 1))
            throw new DomainRuleViolationException("The manager-owned weight file maps more than once to an enabled execution instrument.");

        var program = NormalizeIdentifier(request.ProgramName);
        var externalBatchId = $"legacy_anubis_{program}_{execDeskSha256[..16]}";
        var existing = await repository.GetBatchByExternalIdAsync(
            ModelWeightSourceSystem.LegacyAnubis,
            externalBatchId,
            cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.ContentHash, execDeskSha256, StringComparison.OrdinalIgnoreCase))
                throw new DomainRuleViolationException("The legacy Anubis batch id already exists with different source content.");
            return new(existing, parsed.Count, executable.Count, execDeskSha256, aggregatedSha256, true);
        }

        var now = clock.UtcNow;
        var batch = new ModelWeightBatch(
            ModelWeightBatchId.New(),
            externalBatchId,
            ModelWeightSourceSystem.LegacyAnubis,
            request.FundCode.Trim(),
            null,
            request.ModelName.Trim(),
            request.AsOfUtc,
            request.EffectiveAtUtc,
            request.FrequencyMinutes,
            request.NavUsd,
            request.TargetQuantityMode,
            ModelWeightBatchStatus.Ready,
            executable.Count,
            execDeskSha256,
            now,
            now,
            null,
            null,
            null,
            null,
            $"Genuine legacy Anubis manager import; Program={program}; SourceRows={parsed.Count}; ExecutableRows={executable.Count}; ExecDeskSha256={execDeskSha256}; AggregatedWeightsSha256={aggregatedSha256}.");
        var rows = executable.Select(x => new ModelWeightRow(
            ModelWeightRowId.New(),
            batch.Id,
            x.BloombergTicker,
            x.Symbol,
            enabledBySymbol[x.Symbol].Single().Id,
            x.Weight,
            now)).ToList();
        await repository.AddBatchAsync(batch, rows, cancellationToken);
        return new(batch, parsed.Count, rows.Count, execDeskSha256, aggregatedSha256, false);
    }

    private static IReadOnlyList<ParsedWeight> Parse(byte[] bytes)
    {
        var lines = Encoding.UTF8.GetString(bytes)
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rows = new List<ParsedWeight>(lines.Length);
        for (var index = 0; index < lines.Length; index++)
        {
            var parts = lines[index].Split(';');
            if (parts.Length != 2)
                throw new DomainRuleViolationException($"Legacy Anubis weight row {index + 1} is not '<BloombergTicker>;<weight>'.");
            var ticker = parts[0].Trim().TrimStart('\uFEFF');
            if (!decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var weight))
                throw new DomainRuleViolationException($"Legacy Anubis weight row {index + 1} has an invalid invariant-culture weight.");
            rows.Add(new ParsedWeight(ticker, NormalizeBloombergTicker(ticker), weight));
        }

        if (rows.Count == 0)
            throw new DomainRuleViolationException("The manager-owned weight file is empty.");
        if (rows.GroupBy(x => x.BloombergTicker, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() != 1))
            throw new DomainRuleViolationException("The manager-owned weight file contains duplicate Bloomberg tickers.");
        return rows;
    }

    private static string NormalizeBloombergTicker(string ticker)
    {
        const string suffix = " Curncy";
        if (!ticker.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleViolationException($"Legacy Anubis ticker '{ticker}' does not use the manager Bloomberg Curncy contract.");
        var pair = ticker[..^suffix.Length].Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
        if (pair.Length != 6 || !pair.All(char.IsAsciiLetterUpper))
            throw new DomainRuleViolationException($"Legacy Anubis ticker '{ticker}' is not a canonical FX pair.");
        return pair;
    }

    private static void ValidateRequest(LegacyAnubisWeightIngestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProgramName) || string.IsNullOrWhiteSpace(request.FundCode) || string.IsNullOrWhiteSpace(request.ModelName))
            throw new DomainRuleViolationException("Legacy Anubis program, fund, and model identifiers are required.");
        if (request.AsOfUtc.Offset != TimeSpan.Zero || request.EffectiveAtUtc.Offset != TimeSpan.Zero || request.EffectiveAtUtc < request.AsOfUtc)
            throw new DomainRuleViolationException("Legacy Anubis as-of/effective timestamps must be ordered UTC values.");
        if (request.FrequencyMinutes <= 0 || request.NavUsd <= 0)
            throw new DomainRuleViolationException("Legacy Anubis frequency and NAV must be positive.");
        if (!File.Exists(request.ExecDeskWeightFilePath) || !File.Exists(request.AggregatedWeightsFilePath))
            throw new DomainRuleViolationException("Legacy Anubis lineage files are missing.");
        ValidateHash("exec-desk weight", request.ExpectedExecDeskWeightFileSha256);
        ValidateHash("AggregatedWeights", request.ExpectedAggregatedWeightsFileSha256);
    }

    private static void ValidateHash(string label, string value)
    {
        if (value.Length != 64 || !value.All(Uri.IsHexDigit))
            throw new DomainRuleViolationException($"Expected {label} SHA-256 is invalid.");
    }

    private static void RequireHash(string label, string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleViolationException($"The {label} SHA-256 does not match governed lineage.");
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string NormalizeIdentifier(string value)
        => new(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private sealed record ParsedWeight(string BloombergTicker, string Symbol, decimal Weight);
}
