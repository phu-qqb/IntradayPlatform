using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class DeterministicReportingBundleWriter
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Default,
        Converters = { new JsonStringEnumConverter() }
    };

    public static ReportingBundleResult Write(
        OperationalReportSet report,
        string outputDirectory,
        bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(report);
        var root = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any() && !overwrite)
            throw new InvalidOperationException("REPORTING_OUTPUT_DIRECTORY_NOT_EMPTY");
        Directory.CreateDirectory(root);

        WriteJson(root, "operational-summary.json", report.Summary);
        WriteJson(root, "breaks.json", report.Breaks);
        WriteJson(root, "status-code-catalog.json", new
        {
            contract_version = OperationalReportingContract.Version,
            break_contract_version = OperationalReportingContract.BreakVersion,
            codes = report.StatusCodeCatalog
        });
        WriteJson(root, "reconciliation.json", report.Reconciliation);
        WriteCsv(root, "breaks.csv", BreakHeaders,
            report.Breaks.Select(BreakRow));
        WriteCsv(root, "infx-model-runs.csv", ModelHeaders,
            report.ModelRuns.Select(ModelRow));
        WriteCsv(root, "slots.csv", SlotHeaders,
            report.Slots.Select(SlotRow));
        WriteCsv(root, "economic-revisions.csv", RevisionHeaders,
            report.EconomicRevisions.Select(RevisionRow));
        WriteCsv(root, "fx-lines.csv", FxHeaders,
            report.FxLines.Select(FxRow));
        WriteCsv(root, "arch7a.csv", Arch7aHeaders,
            report.Arch7a.Select(Arch7aRow));
        WriteCsv(root, "arch7b-lifecycle.csv", Arch7bHeaders,
            report.Arch7b.Select(Arch7bRow));
        WriteHtml(root, report);

        var files = Directory.EnumerateFiles(root)
            .Select(path => Describe(root, path))
            .OrderBy(value => value.Path, StringComparer.Ordinal)
            .ToArray();
        var bundleSha = Hash(string.Join('\n', files.Select(value =>
            $"{value.Path}\t{value.SizeBytes.ToString(CultureInfo.InvariantCulture)}\t{value.Sha256}")));
        var manifest = new ReportingBundleManifest(
            OperationalReportingContract.Version,
            OperationalReportingContract.BreakVersion,
            report.Summary.GeneratedAtUtc,
            report.Summary.RepositoryCommit,
            report.Summary.TargetProfileId,
            report.Summary.TargetFingerprint,
            files,
            bundleSha,
            NoOrder: true,
            ReadOnly: true);
        WriteJson(root, "manifest.json", manifest);
        var allFiles = Directory.EnumerateFiles(root)
            .Select(path => Describe(root, path))
            .OrderBy(value => value.Path, StringComparer.Ordinal)
            .ToArray();
        return new(root, bundleSha, allFiles);
    }

    private static readonly string[] BreakHeaders =
    [
        "BreakId", "ExactCode", "Category", "Severity", "Status", "Component",
        "ScopeType", "ScopeId", "SlotId", "StrategyId", "EconomicRevisionId",
        "InstrumentId", "Symbol", "TradeIntentId", "QualificationRunId", "OrderId",
        "FirstObservedAtUtc", "LastObservedAtUtc", "EvidenceSha256", "AuthorityStatus",
        "BlocksTrading", "BlocksAccounting", "OperatorMeaning", "SuggestedInvestigation",
        "SourceTable", "SourceContractVersion"
    ];

    private static object?[] BreakRow(OperationalBreak value) =>
    [
        value.BreakId, value.ExactCode, value.Category, value.Severity, value.Status,
        value.Component, value.ScopeType, value.ScopeId, value.SlotId, value.StrategyId,
        value.EconomicRevisionId, value.InstrumentId, value.Symbol, value.TradeIntentId,
        value.QualificationRunId, value.OrderId, value.FirstObservedAtUtc,
        value.LastObservedAtUtc, value.EvidenceSha256, value.AuthorityStatus,
        value.BlocksTrading, value.BlocksAccounting, value.OperatorMeaning,
        value.SuggestedInvestigation, value.SourceTable, value.SourceContractVersion
    ];

    private static readonly string[] ModelHeaders =
    [
        "StrategyId", "ModelRunId", "QubesInputSnapshotId", "TargetCloseUtc", "AsOfUtc",
        "OutputSha256", "CoreCommitId", "Classification", "FreshOrReusedStatus",
        "ScheduleStatus", "WeightCount", "TargetCount", "DriftCount", "LineageComplete",
        "SourceContractVersion"
    ];

    private static object?[] ModelRow(ReportingModelRunFact value) =>
    [
        value.StrategyId, value.ModelRunId, value.QubesInputSnapshotId, value.TargetCloseUtc,
        value.AsOfUtc, value.OutputSha256, value.CoreCommitId, value.Classification,
        value.FreshOrReusedStatus, value.ScheduleStatus, value.WeightCount, value.TargetCount,
        value.DriftCount, value.LineageComplete, value.SourceContractVersion
    ];

    private static readonly string[] SlotHeaders =
    [
        "SlotId", "SlotStartUtc", "SlotEndUtc", "Status", "ClaimedAtUtc", "CompletedAtUtc",
        "SourceSessionId", "ArtifactSha256", "ClockAuthorityStatus", "BboCoverageCount",
        "InSlotEventCount", "PostCloseExclusionCount", "PolygonCount", "ReadyMarkerStatus",
        "ImportStartLatencySeconds", "ImportCompletionLatencySeconds", "RevisionNumber",
        "Qualifying", "NoOrder", "ManifestSha256", "FailureCode", "ContractVersion"
    ];

    private static object?[] SlotRow(ReportingSlotFact value) =>
    [
        value.SlotId, value.SlotStartUtc, value.SlotEndUtc, value.Status, value.ClaimedAtUtc,
        value.CompletedAtUtc, value.SourceSessionId, value.ArtifactSha256,
        value.ClockAuthorityStatus, value.BboCoverageCount, value.InSlotEventCount,
        value.PostCloseExclusionCount, value.PolygonCount, value.ReadyMarkerStatus,
        value.ImportStartLatencySeconds, value.ImportCompletionLatencySeconds,
        value.RevisionNumber, value.Qualifying, value.NoOrder, value.ManifestSha256,
        value.FailureCode, value.ContractVersion
    ];

    private static readonly string[] RevisionHeaders =
    [
        "EconomicRevisionId", "RevisionNumber", "SlotId", "SourceIngestionId",
        "SourceSessionId", "MarketDataSnapshotSha256", "SupersedesManifestSha256", "Status",
        "Qualifying", "NoOrder", "ObservationCount", "TargetPositionCount",
        "PositionOnlyDriftCount", "ModelRunCount", "TargetSha256", "DriftSha256",
        "ManifestSha256", "CompletedAtUtc"
    ];

    private static object?[] RevisionRow(ReportingEconomicRevisionFact value) =>
    [
        value.EconomicRevisionId, value.RevisionNumber, value.SlotId, value.SourceIngestionId,
        value.SourceSessionId, value.MarketDataSnapshotSha256, value.SupersedesManifestSha256,
        value.Status, value.Qualifying, value.NoOrder, value.ObservationCount,
        value.TargetPositionCount, value.PositionOnlyDriftCount, value.ModelRunCount,
        value.TargetSha256, value.DriftSha256, value.ManifestSha256, value.CompletedAtUtc
    ];

    private static readonly string[] FxHeaders =
    [
        "EconomicRevisionId", "InstrumentId", "PmsSecurityId", "CanonicalSymbol",
        "LmaxInstrumentId", "SecurityIdSource", "StrategyId", "TargetBaseQuantity",
        "TargetVenueQuantity", "CurrentBaseQuantity", "PositionOnlyDrift", "NetTarget",
        "NetDrift", "MappingAuthority", "Bid", "Ask", "PriceAsOfUtc", "Freshness"
    ];

    private static object?[] FxRow(ReportingFxLineFact value) =>
    [
        value.EconomicRevisionId, value.InstrumentId, value.PmsSecurityId,
        value.CanonicalSymbol, value.LmaxInstrumentId, value.SecurityIdSource,
        value.StrategyId, value.TargetBaseQuantity, value.TargetVenueQuantity,
        value.CurrentBaseQuantity, value.PositionOnlyDrift, value.NetTarget, value.NetDrift,
        value.MappingAuthority, value.Bid, value.Ask, value.PriceAsOfUtc, value.Freshness
    ];

    private static readonly string[] Arch7aHeaders =
    [
        "EconomicRevisionId", "TradeIntentId", "RiskDecisionId", "ParentOrderId",
        "ChildOrderId", "AccountScope", "Environment", "Classification", "ParentStatus",
        "ChildStatus", "Actionable", "ExecutionAllowed", "BrokerRouteAllowed",
        "BrokerSendAllowed", "PlanSha256", "ReplayResult", "Symbol", "InstrumentId"
    ];

    private static object?[] Arch7aRow(ReportingArch7aFact value) =>
    [
        value.EconomicRevisionId, value.TradeIntentId, value.RiskDecisionId,
        value.ParentOrderId, value.ChildOrderId, value.AccountScope, value.Environment,
        value.Classification, value.ParentStatus, value.ChildStatus, value.Actionable,
        value.ExecutionAllowed, value.BrokerRouteAllowed, value.BrokerSendAllowed,
        value.PlanSha256, value.ReplayResult, value.Symbol, value.InstrumentId
    ];

    private static readonly string[] Arch7bHeaders =
    [
        "QualificationRunId", "Status", "AuthorityStatus", "AuthorizationPacketSha256",
        "LeaseExpiresAtUtc", "FixSessionEventCount", "OrderSendCount", "ExecutionReportCount",
        "FillCount", "PositionLedgerEventCount", "ReconciliationCount", "KnownLeaves",
        "FinalLedgerQuantity", "BrokerResidualQuantity", "CriticalBreakCount", "FinalGate",
        "CompletedAtUtc"
    ];

    private static object?[] Arch7bRow(ReportingArch7bFact value) =>
    [
        value.QualificationRunId, value.Status, value.AuthorityStatus,
        value.AuthorizationPacketSha256, value.LeaseExpiresAtUtc, value.FixSessionEventCount,
        value.OrderSendCount, value.ExecutionReportCount, value.FillCount,
        value.PositionLedgerEventCount, value.ReconciliationCount, value.KnownLeaves,
        value.FinalLedgerQuantity, value.BrokerResidualQuantity, value.CriticalBreakCount,
        value.FinalGate, value.CompletedAtUtc
    ];

    private static void WriteJson(string root, string name, object value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        WriteBytes(root, name, AppendLf(bytes));
    }

    private static void WriteCsv(
        string root,
        string name,
        IReadOnlyList<string> headers,
        IEnumerable<object?[]> rows)
    {
        var text = new StringBuilder();
        text.AppendLine(string.Join(',', headers.Select(EscapeCsv)));
        foreach (var row in rows)
        {
            if (row.Length != headers.Count)
                throw new InvalidDataException("REPORTING_CSV_COLUMN_COUNT_MISMATCH");
            text.AppendLine(string.Join(',', row.Select(FormatCsv).Select(EscapeCsv)));
        }
        WriteBytes(root, name, new UTF8Encoding(false).GetBytes(
            text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal)));
    }

    private static void WriteHtml(string root, OperationalReportSet report)
    {
        var html = new StringBuilder();
        html.Append("""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Anubis / INFX operational report</title>
              <style>
                :root{color-scheme:light;font-family:Segoe UI,Arial,sans-serif}
                body{margin:0;background:#f4f6f8;color:#17202a}
                header{padding:24px 32px;background:#14213d;color:#fff}
                main{padding:24px 32px;max-width:1500px;margin:auto}
                .facts{display:grid;grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:12px;margin-bottom:24px}
                .fact{background:#fff;border:1px solid #d7dde5;border-radius:6px;padding:14px}
                .label{font-size:12px;color:#52606d;text-transform:uppercase}
                .value{font-size:18px;font-weight:650;margin-top:5px;overflow-wrap:anywhere}
                table{width:100%;border-collapse:collapse;background:#fff;font-size:13px}
                th,td{text-align:left;padding:9px;border-bottom:1px solid #e3e8ee;vertical-align:top}
                th{background:#e9eef5;position:sticky;top:0}
                .critical{color:#9b1c1c;font-weight:700}.error{color:#b54708}.warning{color:#7a5d00}
              </style>
            </head>
            <body>
            <header><h1>Anubis / INFX operational state</h1></header>
            <main>
            """);
        html.Append("<section class=\"facts\">");
        Fact(html, "As of UTC", FormatCsv(report.Summary.GeneratedAtUtc));
        Fact(html, "Database", report.Summary.Database);
        Fact(html, "Latest slot", report.Summary.LatestSlot ?? ReportingAuthority.Absent);
        Fact(html, "Operational status", report.Summary.GlobalOperationalStatus);
        Fact(html, "Trading readiness", report.Summary.GlobalTradingReadiness);
        Fact(html, "Reconciliation", report.Summary.GlobalReconciliationStatus);
        html.Append("</section><h2>Breaks</h2><table><thead><tr>");
        foreach (var header in new[] { "Severity", "Status", "Code", "Scope", "Authority", "Operator meaning" })
            html.Append("<th>").Append(HtmlEncoder.Default.Encode(header)).Append("</th>");
        html.Append("</tr></thead><tbody>");
        foreach (var item in report.Breaks)
        {
            var severity = item.Severity.ToString().ToLowerInvariant();
            html.Append("<tr><td class=\"").Append(severity).Append("\">")
                .Append(HtmlEncoder.Default.Encode(item.Severity.ToString().ToUpperInvariant()))
                .Append("</td><td>").Append(HtmlEncoder.Default.Encode(item.Status.ToString()))
                .Append("</td><td>").Append(HtmlEncoder.Default.Encode(item.ExactCode))
                .Append("</td><td>").Append(HtmlEncoder.Default.Encode($"{item.ScopeType}:{item.ScopeId}"))
                .Append("</td><td>").Append(HtmlEncoder.Default.Encode(item.AuthorityStatus))
                .Append("</td><td>").Append(HtmlEncoder.Default.Encode(item.OperatorMeaning))
                .Append("</td></tr>");
        }
        html.Append("</tbody></table></main></body></html>\n");
        WriteBytes(root, "report.html", new UTF8Encoding(false).GetBytes(html.ToString()));
    }

    private static void Fact(StringBuilder html, string label, string value)
        => html.Append("<div class=\"fact\"><div class=\"label\">")
            .Append(HtmlEncoder.Default.Encode(label))
            .Append("</div><div class=\"value\">")
            .Append(HtmlEncoder.Default.Encode(value))
            .Append("</div></div>");

    private static string FormatCsv(object? value) => value switch
    {
        null => OperationalReportingContract.NullCsvValue,
        DateTimeOffset timestamp => timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        float number => number.ToString("R", CultureInfo.InvariantCulture),
        bool boolean => boolean ? "true" : "false",
        Guid id => id.ToString("D"),
        Enum enumeration => enumeration.ToString().ToUpperInvariant(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ??
             OperationalReportingContract.NullCsvValue
    };

    private static string EscapeCsv(string value)
        => value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static byte[] AppendLf(byte[] bytes)
    {
        if (bytes.Length > 0 && bytes[^1] == (byte)'\n') return bytes;
        var result = new byte[bytes.Length + 1];
        Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
        result[^1] = (byte)'\n';
        return result;
    }

    private static void WriteBytes(string root, string name, byte[] bytes)
    {
        var destination = Path.Combine(root, name);
        var temporary = destination + ".tmp";
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, destination, true);
    }

    private static ReportingBundleFile Describe(string root, string path)
    {
        var bytes = File.ReadAllBytes(path);
        return new(
            Path.GetRelativePath(root, path).Replace('\\', '/'),
            bytes.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    private static string Hash(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
