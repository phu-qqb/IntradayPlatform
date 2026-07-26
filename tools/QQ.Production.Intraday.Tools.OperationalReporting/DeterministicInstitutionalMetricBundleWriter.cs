using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class DeterministicInstitutionalMetricBundleWriter
{
    private static readonly UTF8Encoding Utf8 = new(false);
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Default
    };

    public static InstitutionalBundleResult Write(
        InstitutionalMetricReportSet report,
        string outputDirectory,
        bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(report);
        var root = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any() && !overwrite)
            throw new InvalidOperationException("RPT2_OUTPUT_DIRECTORY_NOT_EMPTY");
        Directory.CreateDirectory(root);

        WriteJson(root, "institutional-reporting-roadmap.json", new
        {
            manifest_id = InstitutionalMetricContract.RoadmapId,
            manifest_version = InstitutionalMetricContract.RoadmapVersion,
            manifest_sha256 = report.RoadmapSha256,
            status = "AUTHORITATIVE_REPORTING_ROADMAP",
            phases = new { rpt1 = "COMPLETED", rpt2 = "IN_PROGRESS", rpt3 = "NOT_STARTED", rpt4 = "NOT_STARTED" },
            layers = new[] { "reporting_source", "reporting_mart", "reporting_control", "reporting_publication" },
            power_bi_csv_contracts = report.PowerBiContracts
        });
        WriteJson(root, "institutional-metric-catalog.json", new
        {
            catalog_version = InstitutionalMetricContract.CatalogVersion,
            metrics = report.Catalog
        });
        WriteCsv(root, "metric-availability.csv", AvailabilityHeaders,
            report.Availability.Select(AvailabilityRow));
        WriteCsv(root, "target-exposure-by-revision.csv", ExposureHeaders,
            report.ExposureByRevision.Select(ExposureRow));
        WriteCsv(root, "target-exposure-by-strategy.csv", ExposureHeaders,
            report.ExposureByStrategy.Select(ExposureRow));
        WriteCsv(root, "target-exposure-by-model.csv", ExposureHeaders,
            report.ExposureByModel.Select(ExposureRow));
        WriteCsv(root, "target-exposure-by-pair.csv", ExposureHeaders,
            report.ExposureByPair.Select(ExposureRow));
        WriteCsv(root, "target-exposure-by-currency.csv", CurrencyHeaders,
            report.ExposureByCurrency.Select(CurrencyRow));
        WriteCsv(root, "target-gross-net.csv", ExposureHeaders,
            report.ExposureByRevision.Select(ExposureRow));
        WriteCsv(root, "target-concentration.csv", ConcentrationHeaders,
            report.Concentrations.Select(ConcentrationRow));
        WriteCsv(root, "target-turnover.csv", TurnoverHeaders,
            report.Turnover.Select(TurnoverRow));
        WriteCsv(root, "drift-by-strategy.csv", DriftHeaders,
            report.DriftByStrategy.Select(DriftRow));
        WriteCsv(root, "drift-by-model.csv", DriftHeaders,
            report.DriftByModel.Select(DriftRow));
        WriteCsv(root, "drift-by-pair.csv", DriftHeaders,
            report.DriftByPair.Select(DriftRow));
        WriteJson(root, "pms-risk-summary.json", report.RiskSummary);
        WriteJson(root, "performance-availability.json", new
        {
            as_of_utc = report.AsOfUtc,
            metrics = report.Availability.Where(value =>
                report.Catalog.Single(definition =>
                    definition.MetricCode == value.MetricCode).Category
                is "PERFORMANCE" or "COST" or "TCA").ToArray()
        });
        WriteJson(root, "data-quality.json", report.DataQuality);
        WriteCsv(root, "active-breaks.csv", BreakHeaders,
            report.ActiveBreaks.Select(BreakRow));
        WriteHtml(root, report);

        var files = Directory.EnumerateFiles(root)
            .Select(path => Describe(root, path))
            .OrderBy(value => value.Path, StringComparer.Ordinal).ToArray();
        var bundleSha = Hash(string.Join('\n', files.Select(value =>
            $"{value.Path}\t{value.SizeBytes.ToString(CultureInfo.InvariantCulture)}\t{value.Sha256}")));
        var sourceSnapshotId = Hash(string.Join('\n',
            report.RepositoryCommit,
            report.Database.TargetFingerprint,
            report.AsOfUtc.ToString("O", CultureInfo.InvariantCulture),
            report.DataQuality.LatestEconomicRevisionId?.ToString("D") ??
            InstitutionalMetricContract.NullCsvValue));
        var manifest = new InstitutionalBundleManifest(
            InstitutionalMetricContract.CatalogVersion,
            InstitutionalMetricContract.RoadmapId,
            InstitutionalMetricContract.RoadmapVersion,
            report.RoadmapSha256,
            report.AsOfUtc,
            report.RepositoryCommit,
            sourceSnapshotId,
            report.Database.TargetProfileId,
            report.Database.TargetFingerprint,
            report.Catalog.Select(value => value.FormulaVersion)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            files,
            bundleSha,
            NoOrder: true,
            ReadOnly: true,
            NoSecrets: true);
        WriteJson(root, "manifest.json", manifest);
        var allFiles = Directory.EnumerateFiles(root)
            .Select(path => Describe(root, path))
            .OrderBy(value => value.Path, StringComparer.Ordinal).ToArray();
        Require(allFiles.Length == 20, "RPT2_BUNDLE_FILE_COUNT_INVALID");
        return new(root, bundleSha, allFiles);
    }

    private static readonly string[] AvailabilityHeaders =
    [
        "MetricCode", "AvailabilityStatus", "Value", "Unit", "Currency",
        "MissingRequiredFacts", "ActivationCondition", "Caveat", "AuthorityStatus",
        "DataQualityStatus"
    ];

    private static object?[] AvailabilityRow(InstitutionalMetricAvailability value) =>
    [
        value.MetricCode, value.AvailabilityStatus, value.Value, value.Unit, value.Currency,
        string.Join('|', value.MissingRequiredFacts), value.ActivationCondition, value.Caveat,
        value.AuthorityStatus, value.DataQualityStatus
    ];

    private static readonly string[] ExposureHeaders =
    [
        "EconomicRevisionId", "RevisionNumber", "SlotId", "AsOfUtc", "DimensionType",
        "DimensionId", "StrategyId", "ModelRunId", "TargetCloseUtc", "InstrumentId",
        "PmsSecurityId", "LmaxInstrumentId", "CanonicalSymbol", "GrossTargetNotionalUsd",
        "NetTargetNotionalUsd", "LongTargetNotionalUsd", "ShortTargetNotionalUsd",
        "AbsoluteWeight", "FormulaVersion", "AuthorityStatus", "EvidenceSha256"
    ];

    private static object?[] ExposureRow(TargetExposureRow value) =>
    [
        value.EconomicRevisionId, value.RevisionNumber, value.SlotId, value.AsOfUtc,
        value.DimensionType, value.DimensionId, value.StrategyId, value.ModelRunId,
        value.TargetCloseUtc, value.InstrumentId, value.PmsSecurityId, value.LmaxInstrumentId,
        value.CanonicalSymbol, value.GrossTargetNotionalUsd, value.NetTargetNotionalUsd,
        value.LongTargetNotionalUsd, value.ShortTargetNotionalUsd, value.AbsoluteWeight,
        value.FormulaVersion, value.AuthorityStatus, value.EvidenceSha256
    ];

    private static readonly string[] CurrencyHeaders =
    [
        "EconomicRevisionId", "RevisionNumber", "SlotId", "AsOfUtc", "Currency",
        "SignedTargetExposureUsd", "AbsoluteTargetExposureUsd", "SourceTargetCount",
        "FormulaVersion", "AuthorityStatus", "EvidenceSha256"
    ];

    private static object?[] CurrencyRow(TargetCurrencyExposureRow value) =>
    [
        value.EconomicRevisionId, value.RevisionNumber, value.SlotId, value.AsOfUtc,
        value.Currency, value.SignedTargetExposureUsd, value.AbsoluteTargetExposureUsd,
        value.SourceTargetCount, value.FormulaVersion, value.AuthorityStatus,
        value.EvidenceSha256
    ];

    private static readonly string[] ConcentrationHeaders =
    [
        "EconomicRevisionId", "DimensionType", "DimensionId", "Concentration", "Rank",
        "TopNConcentration", "Hhi", "GrossNetRatio", "FormulaVersion",
        "DataQualityStatus", "Caveat"
    ];

    private static object?[] ConcentrationRow(TargetConcentrationRow value) =>
    [
        value.EconomicRevisionId, value.DimensionType, value.DimensionId, value.Concentration,
        value.Rank, value.TopNConcentration, value.Hhi, value.GrossNetRatio,
        value.FormulaVersion, value.DataQualityStatus, value.Caveat
    ];

    private static readonly string[] TurnoverHeaders =
    [
        "PreviousEconomicRevisionId", "EconomicRevisionId", "PeriodStartUtc",
        "PeriodEndUtc", "DimensionType", "DimensionId", "TargetTurnoverUsd",
        "NewTargetCount", "ClosedTargetCount", "IncreaseCount", "ReductionCount",
        "InversionCount", "MetricCode", "FormulaVersion", "AvailabilityStatus",
        "EvidenceSha256"
    ];

    private static object?[] TurnoverRow(TargetTurnoverRow value) =>
    [
        value.PreviousEconomicRevisionId, value.EconomicRevisionId, value.PeriodStartUtc,
        value.PeriodEndUtc, value.DimensionType, value.DimensionId, value.TargetTurnoverUsd,
        value.NewTargetCount, value.ClosedTargetCount, value.IncreaseCount,
        value.ReductionCount, value.InversionCount, value.MetricCode, value.FormulaVersion,
        value.AvailabilityStatus, value.EvidenceSha256
    ];

    private static readonly string[] DriftHeaders =
    [
        "EconomicRevisionId", "DimensionType", "DimensionId", "SignedDrift",
        "AbsoluteDrift", "SourceDriftCount", "PositionAuthority", "AvailabilityStatus",
        "FormulaVersion", "EvidenceSha256"
    ];

    private static object?[] DriftRow(DriftSummaryRow value) =>
    [
        value.EconomicRevisionId, value.DimensionType, value.DimensionId, value.SignedDrift,
        value.AbsoluteDrift, value.SourceDriftCount, value.PositionAuthority,
        value.AvailabilityStatus, value.FormulaVersion, value.EvidenceSha256
    ];

    private static readonly string[] BreakHeaders =
    [
        "BreakId", "ExactCode", "Category", "Severity", "Status", "Component",
        "ScopeType", "ScopeId", "AuthorityStatus", "BlocksTrading", "BlocksAccounting",
        "FirstObservedAtUtc", "LastObservedAtUtc", "EvidenceSha256"
    ];

    private static object?[] BreakRow(OperationalBreak value) =>
    [
        value.BreakId, value.ExactCode, value.Category, value.Severity, value.Status,
        value.Component, value.ScopeType, value.ScopeId, value.AuthorityStatus,
        value.BlocksTrading, value.BlocksAccounting, value.FirstObservedAtUtc,
        value.LastObservedAtUtc, value.EvidenceSha256
    ];

    private static void WriteHtml(string root, InstitutionalMetricReportSet report)
    {
        static string N(decimal? value) => value?.ToString("0.############################",
            CultureInfo.InvariantCulture) ?? InstitutionalMetricContract.NullCsvValue;
        var risk = report.RiskSummary;
        var blocked = report.Availability.Count(value =>
            value.AvailabilityStatus == MetricAvailabilityStatus.BlockedMissingSource);
        var html = $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <title>RPT2 Institutional Metric Authority Foundation</title>
              <style>
                body{font-family:Segoe UI,Arial,sans-serif;margin:32px;color:#162028;background:#fff}
                h1,h2{letter-spacing:0} table{border-collapse:collapse;width:100%;margin:12px 0 28px}
                th,td{border:1px solid #ccd3d8;padding:7px;text-align:left}
                th{background:#eef2f4}.blocked{color:#8b1e1e}.ok{color:#17643a}
                code{font-family:Consolas,monospace}
              </style>
            </head>
            <body>
              <h1>RPT2 Institutional Metric Authority Foundation</h1>
              <p>As of <code>{{report.AsOfUtc:O}}</code>. Read-only, no-order source snapshot.</p>
              <h2>Target risk summary</h2>
              <table><tbody>
                <tr><th>Economic revision</th><td>{{risk.EconomicRevisionId?.ToString("D") ?? "NULL"}}</td></tr>
                <tr><th>Gross target exposure USD</th><td>{{N(risk.GrossTargetExposureUsd)}}</td></tr>
                <tr><th>Net target exposure USD</th><td>{{N(risk.NetTargetExposureUsd)}}</td></tr>
                <tr><th>Max pair concentration</th><td>{{N(risk.MaxPairConcentration)}}</td></tr>
                <tr><th>Max strategy concentration</th><td>{{N(risk.MaxStrategyConcentration)}}</td></tr>
                <tr><th>Target turnover USD</th><td>{{N(risk.TargetTurnoverUsd)}}</td></tr>
              </tbody></table>
              <h2>Authority</h2>
              <p class="ok">Target metrics preserve their PMS economic revision authority.</p>
              <p class="blocked">{{blocked}} metrics are explicitly blocked and carry no fabricated value.</p>
              <p>Leverage is unavailable without authoritative AUM or NAV. Target turnover is not executed turnover.</p>
            </body>
            </html>
            """;
        WriteText(Path.Combine(root, "report.html"), html + "\n");
    }

    private static void WriteJson(string root, string name, object value) =>
        WriteText(Path.Combine(root, name), JsonSerializer.Serialize(value, Json) + "\n");

    private static void WriteCsv(
        string root,
        string name,
        IReadOnlyList<string> headers,
        IEnumerable<object?[]> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', headers.Select(Escape)));
        foreach (var row in rows)
        {
            Require(row.Length == headers.Count, $"RPT2_CSV_ROW_WIDTH_INVALID:{name}");
            builder.AppendLine(string.Join(',', row.Select(value => Escape(Format(value)))));
        }
        WriteText(Path.Combine(root, name), builder.ToString());
    }

    private static string Format(object? value) => value switch
    {
        null => InstitutionalMetricContract.NullCsvValue,
        DateTimeOffset date => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        DateTime date => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        decimal number => number.ToString("0.############################", CultureInfo.InvariantCulture),
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        float number => number.ToString("R", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        Guid id => id.ToString("D"),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? InstitutionalMetricContract.NullCsvValue
    };

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static void WriteText(string path, string value) =>
        File.WriteAllText(path, value.Replace("\r\n", "\n", StringComparison.Ordinal), Utf8);

    private static InstitutionalBundleFile Describe(string root, string path)
    {
        var bytes = File.ReadAllBytes(path);
        return new(Path.GetRelativePath(root, path).Replace('\\', '/'), bytes.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Utf8.GetBytes(value)));

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
