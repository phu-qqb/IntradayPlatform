using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public enum LegacyAnubisProgrammeContributionState
{
    Present,
    Absent,
    Failed
}

public sealed record LegacyAnubisProgrammeContribution(
    string ProgramName,
    int UniverseId,
    int ModelId,
    string Session,
    int FrequencyMinutes,
    decimal Coefficient,
    LegacyAnubisProgrammeContributionState State,
    DateTimeOffset? AsOfUtc = null,
    string? ExecDeskWeightFilePath = null,
    string? ExpectedExecDeskWeightFileSha256 = null,
    string? AggregatedWeightsFilePath = null,
    string? ExpectedAggregatedWeightsFileSha256 = null,
    string? Reason = null);

public sealed record LegacyAnubisPortfolioWeightIngestionRequest(
    IReadOnlyList<LegacyAnubisProgrammeContribution> Programmes,
    string FundCode,
    string ModelName,
    DateTimeOffset DecisionAtUtc,
    DateTimeOffset EffectiveAtUtc,
    decimal NavUsd,
    TargetQuantityMode TargetQuantityMode);

public sealed record LegacyAnubisPortfolioWeightIngestionResult(
    ModelWeightBatch Batch,
    int PresentProgrammeCount,
    int AbsentProgrammeCount,
    int SourceRowCount,
    int ExecutableRowCount,
    string PortfolioLineageSha256,
    bool AlreadyExisted);

public interface ILegacyAnubisPortfolioWeightIngestionService
{
    Task<LegacyAnubisPortfolioWeightIngestionResult> IngestAsync(
        LegacyAnubisPortfolioWeightIngestionRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Imports the executable four-programme Intraday FX portfolio. Each programme is
/// evaluated at the portfolio decision timestamp: a genuine weight is included,
/// a genuine absence contributes zero, and an execution failure is never zeroed.
/// Manager-owned weights already contain the authoritative V1 coefficient.
/// </summary>
public sealed class LegacyAnubisPortfolioWeightIngestionService(
    IModelWeightBatchRepository repository,
    IIntradayRepository intradayRepository,
    IClock clock) : ILegacyAnubisPortfolioWeightIngestionService
{
    private const int PortfolioFrequencyMinutes = 15;

    private static readonly IReadOnlyDictionary<string, ProgrammeContract> Contracts =
        new Dictionary<string, ProgrammeContract>(StringComparer.OrdinalIgnoreCase)
        {
            ["INFX7"] = new(54, 10, "US", 15, 4.5m),
            ["INFX8"] = new(57, 11, "US", 30, 2.1m),
            ["INFX9"] = new(58, 12, "EU", 15, 1.4m),
            ["INFX10"] = new(59, 13, "EU", 60, 0.6m)
        };

    public async Task<LegacyAnubisPortfolioWeightIngestionResult> IngestAsync(
        LegacyAnubisPortfolioWeightIngestionRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var parsedByProgramme = new Dictionary<string, IReadOnlyList<ParsedWeight>>(StringComparer.OrdinalIgnoreCase);
        var lineage = new List<ProgrammeLineage>(Contracts.Count);
        foreach (var contribution in Order(request.Programmes))
        {
            if (contribution.State == LegacyAnubisProgrammeContributionState.Absent)
            {
                lineage.Add(new(contribution.ProgramName.ToUpperInvariant(), contribution.State, null, null,
                    contribution.Reason!.Trim()));
                continue;
            }

            var execDeskBytes = await File.ReadAllBytesAsync(contribution.ExecDeskWeightFilePath!, cancellationToken);
            var aggregatedBytes = await File.ReadAllBytesAsync(contribution.AggregatedWeightsFilePath!, cancellationToken);
            var execDeskSha256 = Sha256(execDeskBytes);
            var aggregatedSha256 = Sha256(aggregatedBytes);
            RequireHash($"{contribution.ProgramName} exec-desk weight file",
                contribution.ExpectedExecDeskWeightFileSha256!, execDeskSha256);
            RequireHash($"{contribution.ProgramName} AggregatedWeights file",
                contribution.ExpectedAggregatedWeightsFileSha256!, aggregatedSha256);
            parsedByProgramme.Add(contribution.ProgramName, Parse(contribution.ProgramName, execDeskBytes));
            lineage.Add(new(contribution.ProgramName.ToUpperInvariant(), contribution.State, execDeskSha256,
                aggregatedSha256, null));
        }

        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        var enabledBySymbol = state.Instruments
            .Where(x => x.IsEnabled && x.IsTradingEnabled)
            .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
        if (enabledBySymbol.Any(x => x.Value.Count != 1))
            throw new DomainRuleViolationException("Enabled execution instrument symbols are ambiguous.");

        var executableContributions = parsedByProgramme.Values
            .SelectMany(x => x)
            .Where(x => enabledBySymbol.ContainsKey(x.Symbol))
            .ToList();
        if (executableContributions.Count == 0)
            throw new DomainRuleViolationException("The four-programme manager portfolio has no rows for enabled execution instruments.");

        // No coefficient is applied here: each manager-owned programme weight is already scaled by V1.
        var aggregated = executableContributions
            .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(x => new AggregatedWeight(
                x.First().Symbol,
                $"{x.First().Symbol} Curncy",
                x.Sum(row => row.Weight)))
            .OrderBy(x => x.Symbol, StringComparer.Ordinal)
            .ToList();

        var portfolioLineageSha256 = PortfolioLineageSha256(request, lineage);
        var externalBatchId = $"legacy_anubis_portfolio_{portfolioLineageSha256[..16]}";
        var existing = await repository.GetBatchByExternalIdAsync(
            ModelWeightSourceSystem.LegacyAnubis,
            externalBatchId,
            cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.ContentHash, portfolioLineageSha256, StringComparison.OrdinalIgnoreCase))
                throw new DomainRuleViolationException("The legacy Anubis portfolio batch id already exists with different lineage.");
            return Result(existing, request, parsedByProgramme, aggregated.Count, portfolioLineageSha256, true);
        }

        var now = clock.UtcNow;
        var batch = new ModelWeightBatch(
            ModelWeightBatchId.New(),
            externalBatchId,
            ModelWeightSourceSystem.LegacyAnubis,
            request.FundCode.Trim(),
            null,
            request.ModelName.Trim(),
            request.DecisionAtUtc,
            request.EffectiveAtUtc,
            PortfolioFrequencyMinutes,
            request.NavUsd,
            request.TargetQuantityMode,
            ModelWeightBatchStatus.Ready,
            aggregated.Count,
            portfolioLineageSha256,
            now,
            now,
            null,
            null,
            null,
            null,
            BuildMessage(request, lineage, parsedByProgramme, aggregated.Count, portfolioLineageSha256));
        var rows = aggregated.Select(x => new ModelWeightRow(
            ModelWeightRowId.New(),
            batch.Id,
            x.BloombergTicker,
            x.Symbol,
            enabledBySymbol[x.Symbol].Single().Id,
            x.Weight,
            now)).ToList();
        await repository.AddBatchAsync(batch, rows, cancellationToken);
        return Result(batch, request, parsedByProgramme, rows.Count, portfolioLineageSha256, false);
    }

    private static void ValidateRequest(LegacyAnubisPortfolioWeightIngestionRequest request)
    {
        if (request.Programmes.Count != Contracts.Count ||
            request.Programmes.Select(x => x.ProgramName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Contracts.Count ||
            Contracts.Keys.Any(name => request.Programmes.All(x => !string.Equals(x.ProgramName, name, StringComparison.OrdinalIgnoreCase))))
            throw new DomainRuleViolationException("The portfolio requires exactly one contribution for each of INFX7, INFX8, INFX9, and INFX10.");
        if (string.IsNullOrWhiteSpace(request.FundCode) || string.IsNullOrWhiteSpace(request.ModelName))
            throw new DomainRuleViolationException("Legacy Anubis portfolio fund and model identifiers are required.");
        if (request.DecisionAtUtc.Offset != TimeSpan.Zero || request.EffectiveAtUtc.Offset != TimeSpan.Zero ||
            request.EffectiveAtUtc < request.DecisionAtUtc)
            throw new DomainRuleViolationException("Portfolio decision/effective timestamps must be ordered UTC values.");
        if (request.NavUsd <= 0)
            throw new DomainRuleViolationException("Legacy Anubis portfolio NAV must be positive.");
        if (request.Programmes.All(x => x.State == LegacyAnubisProgrammeContributionState.Absent))
            throw new DomainRuleViolationException("A portfolio decision cannot contain four absent programme contributions.");

        foreach (var contribution in request.Programmes)
        {
            var contract = Contracts[contribution.ProgramName];
            if (contribution.UniverseId != contract.UniverseId || contribution.ModelId != contract.ModelId ||
                !string.Equals(contribution.Session, contract.Session, StringComparison.OrdinalIgnoreCase) ||
                contribution.FrequencyMinutes != contract.FrequencyMinutes || contribution.Coefficient != contract.Coefficient)
                throw new DomainRuleViolationException($"{contribution.ProgramName} does not match its authoritative universe/model/session/timeframe/coefficient contract.");
            if (contribution.State == LegacyAnubisProgrammeContributionState.Failed)
                throw new DomainRuleViolationException($"{contribution.ProgramName} failed at its expected decision timestamp and cannot contribute zero: {contribution.Reason}");
            if (contribution.State == LegacyAnubisProgrammeContributionState.Absent)
            {
                if (string.IsNullOrWhiteSpace(contribution.Reason))
                    throw new DomainRuleViolationException($"{contribution.ProgramName} zero contribution requires an explicit genuine-absence reason.");
                if (contribution.AsOfUtc is not null || contribution.ExecDeskWeightFilePath is not null ||
                    contribution.AggregatedWeightsFilePath is not null)
                    throw new DomainRuleViolationException($"{contribution.ProgramName} absent contribution cannot carry forward files or an earlier timestamp.");
                continue;
            }

            if (contribution.AsOfUtc != request.DecisionAtUtc)
                throw new DomainRuleViolationException($"{contribution.ProgramName} present contribution must be genuine for the current decision timestamp.");
            if (string.IsNullOrWhiteSpace(contribution.ExecDeskWeightFilePath) ||
                string.IsNullOrWhiteSpace(contribution.AggregatedWeightsFilePath) ||
                !File.Exists(contribution.ExecDeskWeightFilePath) || !File.Exists(contribution.AggregatedWeightsFilePath))
                throw new DomainRuleViolationException($"{contribution.ProgramName} present contribution is missing manager-owned lineage files.");
            ValidateHash($"{contribution.ProgramName} exec-desk weight", contribution.ExpectedExecDeskWeightFileSha256);
            ValidateHash($"{contribution.ProgramName} AggregatedWeights", contribution.ExpectedAggregatedWeightsFileSha256);
        }
    }

    private static IReadOnlyList<ParsedWeight> Parse(string programName, byte[] bytes)
    {
        var lines = Encoding.UTF8.GetString(bytes)
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rows = new List<ParsedWeight>(lines.Length);
        for (var index = 0; index < lines.Length; index++)
        {
            var parts = lines[index].Split(';');
            if (parts.Length != 2)
                throw new DomainRuleViolationException($"{programName} manager weight row {index + 1} is not '<BloombergTicker>;<weight>'.");
            var ticker = parts[0].Trim().TrimStart('\uFEFF');
            if (!decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var weight))
                throw new DomainRuleViolationException($"{programName} manager weight row {index + 1} has an invalid invariant-culture weight.");
            rows.Add(new(ticker, NormalizeBloombergTicker(ticker), weight));
        }
        if (rows.Count == 0)
            throw new DomainRuleViolationException($"{programName} present manager weight file is empty; classify a genuine no-weight result as absent.");
        if (rows.GroupBy(x => x.BloombergTicker, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() != 1))
            throw new DomainRuleViolationException($"{programName} manager weight file contains duplicate Bloomberg tickers.");
        return rows;
    }

    private static string NormalizeBloombergTicker(string ticker)
    {
        const string suffix = " Curncy";
        // Preserve the manager's historical static-mapping typo verbatim in RawSecurityId,
        // while still allowing its non-executable PLNHUF row to be parsed deterministically.
        if (string.Equals(ticker, "PLNHUF Cunrcy", StringComparison.OrdinalIgnoreCase))
            return "PLNHUF";
        if (!ticker.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleViolationException($"Legacy Anubis ticker '{ticker}' does not use the manager Bloomberg Curncy contract.");
        var pair = ticker[..^suffix.Length].Replace("/", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
        if (pair.Length != 6 || !pair.All(char.IsAsciiLetterUpper))
            throw new DomainRuleViolationException($"Legacy Anubis ticker '{ticker}' is not a canonical FX pair.");
        return pair;
    }

    private static LegacyAnubisPortfolioWeightIngestionResult Result(
        ModelWeightBatch batch,
        LegacyAnubisPortfolioWeightIngestionRequest request,
        IReadOnlyDictionary<string, IReadOnlyList<ParsedWeight>> parsed,
        int executableRows,
        string lineageSha256,
        bool alreadyExisted) => new(
            batch,
            request.Programmes.Count(x => x.State == LegacyAnubisProgrammeContributionState.Present),
            request.Programmes.Count(x => x.State == LegacyAnubisProgrammeContributionState.Absent),
            parsed.Values.Sum(x => x.Count),
            executableRows,
            lineageSha256,
            alreadyExisted);

    private static string PortfolioLineageSha256(
        LegacyAnubisPortfolioWeightIngestionRequest request,
        IReadOnlyList<ProgrammeLineage> lineage)
    {
        var canonical = new StringBuilder()
            .Append("FundCode=").Append(request.FundCode.Trim()).Append('\n')
            .Append("ModelName=").Append(request.ModelName.Trim()).Append('\n')
            .Append("DecisionAtUtc=").Append(request.DecisionAtUtc.ToString("O", CultureInfo.InvariantCulture)).Append('\n')
            .Append("EffectiveAtUtc=").Append(request.EffectiveAtUtc.ToString("O", CultureInfo.InvariantCulture)).Append('\n')
            .Append("NavUsd=").Append(request.NavUsd.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append("TargetQuantityMode=").Append(request.TargetQuantityMode).Append('\n');
        foreach (var item in lineage)
        {
            var contract = Contracts[item.ProgramName];
            canonical.Append(item.ProgramName).Append('|').Append(contract.UniverseId).Append('|').Append(contract.ModelId)
                .Append('|').Append(contract.Session).Append('|').Append(contract.FrequencyMinutes).Append('|')
                .Append(contract.Coefficient.ToString(CultureInfo.InvariantCulture)).Append('|').Append(item.State).Append('|')
                .Append(item.ExecDeskSha256 ?? "ZERO").Append('|').Append(item.AggregatedSha256 ?? "ZERO").Append('|')
                .Append(item.Reason ?? string.Empty).Append('\n');
        }
        return Sha256(Encoding.UTF8.GetBytes(canonical.ToString()));
    }

    private static string BuildMessage(
        LegacyAnubisPortfolioWeightIngestionRequest request,
        IReadOnlyList<ProgrammeLineage> lineage,
        IReadOnlyDictionary<string, IReadOnlyList<ParsedWeight>> parsed,
        int executableRows,
        string lineageSha256)
    {
        var contributions = string.Join(", ", lineage.Select(x =>
        {
            var contract = Contracts[x.ProgramName];
            return $"{x.ProgramName}=U{contract.UniverseId}/M{contract.ModelId}/{contract.Session}/{contract.FrequencyMinutes}m/c{contract.Coefficient.ToString(CultureInfo.InvariantCulture)}/{x.State}";
        }));
        return $"Genuine four-programme legacy Anubis portfolio import; Decision={request.DecisionAtUtc:O}; {contributions}; " +
               $"ZeroIfAbsent=true; CarryForward=false; AdditionalNormalization=false; SourceRows={parsed.Values.Sum(x => x.Count)}; " +
               $"ExecutableRows={executableRows}; PortfolioLineageSha256={lineageSha256}.";
    }

    private static IEnumerable<LegacyAnubisProgrammeContribution> Order(
        IEnumerable<LegacyAnubisProgrammeContribution> programmes) => programmes.OrderBy(x => x.ProgramName.ToUpperInvariant() switch
        {
            "INFX7" => 7,
            "INFX8" => 8,
            "INFX9" => 9,
            "INFX10" => 10,
            _ => int.MaxValue
        });

    private static void ValidateHash(string label, string? value)
    {
        if (value is null || value.Length != 64 || !value.All(Uri.IsHexDigit))
            throw new DomainRuleViolationException($"Expected {label} SHA-256 is invalid.");
    }

    private static void RequireHash(string label, string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleViolationException($"The {label} SHA-256 does not match governed lineage.");
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private sealed record ProgrammeContract(int UniverseId, int ModelId, string Session, int FrequencyMinutes, decimal Coefficient);
    private sealed record ProgrammeLineage(string ProgramName, LegacyAnubisProgrammeContributionState State,
        string? ExecDeskSha256, string? AggregatedSha256, string? Reason);
    private sealed record ParsedWeight(string BloombergTicker, string Symbol, decimal Weight);
    private sealed record AggregatedWeight(string Symbol, string BloombergTicker, decimal Weight);
}
