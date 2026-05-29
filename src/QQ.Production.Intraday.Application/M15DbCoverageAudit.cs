using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QQ.Production.Intraday.Application;

public enum M15DbCoverageGate { PASS, WARN, FAIL }
public enum M15TimestampRole { BarCloseUtc, BarOpenUtc, UnknownUtcGridTimestamp }

public sealed record M15DbCandidateTable(
    string Schema,
    string Table,
    IReadOnlyList<string> Columns,
    bool LooksLikeIntradayCandles,
    bool SupportedByAudit,
    string Reason);

public sealed record M15DbInventory(
    string RunKey,
    DateTimeOffset ProducedAtUtc,
    string DbConfigurationSource,
    string ConnectionStringRedacted,
    IReadOnlyList<M15DbCandidateTable> CandidateTables,
    IReadOnlyList<string> SupportedSources,
    IReadOnlyList<string> SafetyAssertions);

public sealed record M15DbCandleRow(
    string Instrument,
    string? Venue,
    DateTimeOffset TimestampUtc,
    M15TimestampRole TimestampRole,
    decimal? Close,
    int RowOrdinal,
    string SourceTable = "MarketDataBars");

public sealed record M15DbCoverageAuditRequest(
    string RunKey,
    DateTimeOffset ProducedAtUtc,
    IReadOnlyList<M15DbCandidateTable> CandidateTables,
    IReadOnlyList<M15DbCandleRow> Rows,
    IReadOnlyList<string> AvailableInstruments,
    string DbConfigurationSource,
    string ConnectionStringRedacted);

public sealed record M15DbSessionCoverage(
    string Session,
    string TimeZoneId,
    string LocalWindow,
    int ObservedLocalDates,
    int RowCount,
    int ExpectedRowsOnObservedDates,
    decimal CoverageRatio);

public sealed record M15DbAuditedSource(string Schema, string Table, string Reason);

public sealed record M15DbInstrumentCoverage(
    string Instrument,
    string? Venue,
    int RowCount,
    DateTimeOffset? FirstTimestampUtc,
    DateTimeOffset? LastTimestampUtc,
    bool TimestampGridCompatibleM15,
    string BarCloseSemantics,
    string TimestampAssessment,
    int DuplicateTimestampCount,
    int NonMonotoneTimestampCount,
    int OffGridTimestampCount,
    int CloseNullCount,
    int CloseNonPositiveCount,
    int EstimatedGapCount,
    int FxWeekendExcludedGapCount,
    decimal EstimatedGapRatio,
    IReadOnlyList<M15DbSessionCoverage> Sessions,
    IReadOnlyList<string> Issues);

public sealed record M15DbCoverageReport(
    string RunKey,
    DateTimeOffset ProducedAtUtc,
    string M15_DB_COVERAGE_GATE,
    IReadOnlyList<M15DbCandidateTable> CandidateTablesDiscovered,
    IReadOnlyList<M15DbAuditedSource> AuditedSources,
    IReadOnlyList<M15DbInstrumentCoverage> Instruments,
    IReadOnlyList<string> InstrumentsWithoutM15,
    IReadOnlyList<string> Thresholds,
    IReadOnlyList<string> SafetyAssertions,
    IReadOnlyList<string> GlobalIssues);

public sealed record M15DbAuditPackage(M15DbInventory Inventory, M15DbCoverageReport Report);

public static class M15DbCoverageAudit
{
    private static readonly TimeSpan M15 = TimeSpan.FromMinutes(15);
    private static readonly TimeOnly SessionStartAsia = new(9, 0);
    private static readonly TimeOnly SessionEndAsia = new(18, 0);
    private static readonly TimeOnly SessionStartLondon = new(8, 0);
    private static readonly TimeOnly SessionEndLondon = new(17, 0);
    private static readonly TimeOnly SessionStartNy = new(8, 0);
    private static readonly TimeOnly SessionEndNy = new(17, 0);

    public static M15DbAuditPackage Audit(M15DbCoverageAuditRequest request)
    {
        var safety = SafetyAssertions();
        var inventory = new M15DbInventory(
            request.RunKey,
            request.ProducedAtUtc,
            request.DbConfigurationSource,
            request.ConnectionStringRedacted,
            request.CandidateTables,
            request.CandidateTables.Where(x => x.SupportedByAudit).Select(x => $"{x.Schema}.{x.Table}").ToArray(),
            safety);

        var rows = request.Rows
            .Where(x => x.Instrument.Length > 0)
            .ToArray();
        var instruments = rows
            .GroupBy(x => $"{x.Instrument.ToUpperInvariant()}\u001f{x.Venue ?? string.Empty}", StringComparer.OrdinalIgnoreCase)
            .Select(x =>
            {
                var parts = x.Key.Split('\u001f');
                return AuditInstrument(parts[0], parts[1].Length == 0 ? null : parts[1], x.OrderBy(r => r.RowOrdinal).ToArray());
            })
            .OrderBy(x => x.Instrument, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Venue, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var instrumentsWithM15 = instruments.Select(x => x.Instrument).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var instrumentsWithoutM15 = request.AvailableInstruments
            .Where(x => !instrumentsWithM15.Contains(x))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var globalIssues = BuildGlobalIssues(instruments, instrumentsWithoutM15, request.CandidateTables);
        var gate = ComputeGate(instruments, instrumentsWithoutM15, globalIssues);
        var auditedSources = request.CandidateTables
            .Where(x => x.SupportedByAudit)
            .Select(x => new M15DbAuditedSource(x.Schema, x.Table, x.Reason))
            .ToArray();
        var report = new M15DbCoverageReport(
            request.RunKey,
            request.ProducedAtUtc,
            gate.ToString(),
            request.CandidateTables,
            auditedSources,
            instruments,
            instrumentsWithoutM15,
            Thresholds(),
            safety,
            globalIssues);

        return new M15DbAuditPackage(inventory, report);
    }

    public static IReadOnlyList<M15DbCandidateTable> InferCandidateTables(IEnumerable<(string Schema, string Table, string Column)> columns)
    {
        return columns
            .GroupBy(x => $"{x.Schema}\u001f{x.Table}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var parts = group.Key.Split('\u001f');
                var names = group.Select(x => x.Column).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
                var hasTime = names.Any(IsTimestampColumn);
                var hasClose = names.Any(x => x.Contains("Close", StringComparison.OrdinalIgnoreCase));
                var hasInstrument = names.Any(x => x.Contains("Instrument", StringComparison.OrdinalIgnoreCase) || x.Equals("Symbol", StringComparison.OrdinalIgnoreCase));
                var hasTimeframe = names.Any(x => x.Contains("Timeframe", StringComparison.OrdinalIgnoreCase) || x.Contains("TimeFrame", StringComparison.OrdinalIgnoreCase) || x.Equals("Interval", StringComparison.OrdinalIgnoreCase));
                var supported = parts[1].Equals("MarketDataBars", StringComparison.OrdinalIgnoreCase) &&
                                names.Contains("BarEndUtc", StringComparer.OrdinalIgnoreCase) &&
                                names.Contains("Timeframe", StringComparer.OrdinalIgnoreCase) &&
                                names.Contains("MidClose", StringComparer.OrdinalIgnoreCase);
                var looksLikeCandles = hasTime && hasClose && hasInstrument && hasTimeframe;
                var reason = supported
                    ? "Supported canonical intraday bar table; audited with BarEndUtc as UTC bar-close and MidClose as raw close proxy."
                    : looksLikeCandles
                        ? "Candidate intraday candle shape discovered, but no safe table-specific reader is enabled in this increment."
                        : "Not selected; required intraday candle columns were not found.";
                return new M15DbCandidateTable(parts[0], parts[1], names, looksLikeCandles, supported, reason);
            })
            .Where(x => x.LooksLikeIntradayCandles || x.SupportedByAudit)
            .OrderByDescending(x => x.SupportedByAudit)
            .ThenBy(x => x.Schema, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Table, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static async Task WritePackageAsync(string validationDirectory, M15DbAuditPackage package, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(validationDirectory);
        var jsonOptions = JsonOptions();
        var artifacts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["m15_db_inventory.json"] = JsonSerializer.Serialize(package.Inventory, jsonOptions),
            ["m15_db_inventory.md"] = RenderInventoryMarkdown(package.Inventory),
            ["m15_db_coverage_report.json"] = JsonSerializer.Serialize(package.Report, jsonOptions),
            ["m15_db_coverage_report.md"] = RenderReportMarkdown(package.Report)
        };

        foreach (var artifact in artifacts)
        {
            await File.WriteAllTextAsync(Path.Combine(validationDirectory, artifact.Key), artifact.Value, Encoding.UTF8, cancellationToken);
        }

        var hashes = artifacts.Keys
            .Select(name => new { path = name, sha256 = Sha256File(Path.Combine(validationDirectory, name)) })
            .ToList();
        var manifest = new
        {
            packageKind = "QubesIntradayM15DbCoverageAudit",
            packageVersion = "1.0",
            packageIdentity = "RunKey",
            RunKey = package.Inventory.RunKey,
            ProducedAtUtc = package.Report.ProducedAtUtc,
            M15_DB_COVERAGE_GATE = package.Report.M15_DB_COVERAGE_GATE,
            readOnly = true,
            noLegacyAhiGenerated = true,
            entries = artifacts.Keys.Select(name => new { path = name, role = name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "artifact" : "report" }).ToArray(),
            supportFiles = new[] { "hashes.json", "manifest.sha256" }
        };
        var manifestText = JsonSerializer.Serialize(manifest, jsonOptions);
        await File.WriteAllTextAsync(Path.Combine(validationDirectory, "manifest.json"), manifestText, Encoding.UTF8, cancellationToken);

        hashes.Add(new { path = "manifest.json", sha256 = Sha256File(Path.Combine(validationDirectory, "manifest.json")) });
        await File.WriteAllTextAsync(Path.Combine(validationDirectory, "hashes.json"), JsonSerializer.Serialize(hashes, jsonOptions), Encoding.UTF8, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(validationDirectory, "manifest.sha256"), hashes.Single(x => x.path == "manifest.json").sha256 + "  manifest.json" + Environment.NewLine, Encoding.ASCII, cancellationToken);
    }

    private static M15DbInstrumentCoverage AuditInstrument(string instrument, string? venue, IReadOnlyList<M15DbCandleRow> rows)
    {
        var closeTimestamps = rows.Select(EffectiveBarCloseUtc).ToArray();
        var sortedDistinct = closeTimestamps.Distinct().OrderBy(x => x).ToArray();
        var duplicateCount = closeTimestamps.Length - closeTimestamps.Distinct().Count();
        var nonMonotoneCount = CountNonMonotone(closeTimestamps);
        var offGridCount = closeTimestamps.Count(x => !IsOnM15Grid(x));
        var nullClose = rows.Count(x => x.Close is null);
        var nonPositiveClose = rows.Count(x => x.Close <= 0m);
        var gapEstimate = EstimateGaps(sortedDistinct);
        var estimatedGaps = gapEstimate.CountedGaps;
        var first = sortedDistinct.FirstOrDefault();
        var last = sortedDistinct.LastOrDefault();
        var gapRatio = sortedDistinct.Length + estimatedGaps == 0 ? 0m : decimal.Round(estimatedGaps / (decimal)(sortedDistinct.Length + estimatedGaps), 6);
        var issues = new List<string>();

        if (rows.Any(x => x.TimestampUtc.Offset != TimeSpan.Zero)) issues.Add("TimestampOffsetNotUtc");
        if (rows.Any(x => x.TimestampRole == M15TimestampRole.UnknownUtcGridTimestamp)) issues.Add("TimestampRoleAmbiguousBarOpenVsBarClose");
        if (duplicateCount > 0) issues.Add("DuplicateTimestamps");
        if (nonMonotoneCount > 0) issues.Add("NonMonotoneTimestamps");
        if (offGridCount > 0) issues.Add("OffGridM15Timestamps");
        if (nullClose > 0) issues.Add("CloseNull");
        if (nonPositiveClose > 0) issues.Add("CloseNonPositive");
        if (gapRatio > 0.01m) issues.Add("EstimatedGapsAbovePassThreshold");

        return new M15DbInstrumentCoverage(
            instrument,
            venue,
            rows.Count,
            sortedDistinct.Length == 0 ? null : first,
            sortedDistinct.Length == 0 ? null : last,
            offGridCount == 0,
            BarCloseSemantics(rows),
            TimestampAssessment(rows),
            duplicateCount,
            nonMonotoneCount,
            offGridCount,
            nullClose,
            nonPositiveClose,
            estimatedGaps,
            gapEstimate.FxWeekendExcludedGaps,
            gapRatio,
            BuildSessionCoverage(sortedDistinct),
            issues);
    }

    private static DateTimeOffset EffectiveBarCloseUtc(M15DbCandleRow row)
        => row.TimestampRole == M15TimestampRole.BarOpenUtc ? row.TimestampUtc.Add(M15) : row.TimestampUtc;

    private static string TimestampAssessment(IReadOnlyList<M15DbCandleRow> rows)
    {
        if (rows.Count == 0) return "No M15 rows.";
        if (rows.Any(x => x.TimestampUtc.Offset != TimeSpan.Zero)) return "FAIL: timestamp offsets are not UTC.";
        if (rows.All(x => x.TimestampRole == M15TimestampRole.BarCloseUtc)) return "PASS: timestamps are explicit UTC bar-close.";
        if (rows.All(x => x.TimestampRole == M15TimestampRole.BarOpenUtc)) return "PASS_WITH_DOCUMENTED_CONVERSION: source timestamps are UTC bar-open; audit converts by +15 minutes to UTC bar-close.";
        return "WARN: timestamps are on a UTC 15-minute grid, but bar-open versus bar-close semantics are ambiguous.";
    }

    private static string BarCloseSemantics(IReadOnlyList<M15DbCandleRow> rows)
    {
        if (rows.Count == 0) return "NoM15Rows";
        if (rows.All(x => x.TimestampRole == M15TimestampRole.BarCloseUtc)) return "ConfirmedUtcBarClose";
        if (rows.All(x => x.TimestampRole == M15TimestampRole.BarOpenUtc)) return "DocumentedUtcBarOpenConvertedToBarClose";
        return "UnknownUtcGridTimestamp";
    }

    private static IReadOnlyList<M15DbSessionCoverage> BuildSessionCoverage(IReadOnlyList<DateTimeOffset> barCloseUtc)
        =>
        [
            BuildSession("Asia", "Asia/Tokyo", SessionStartAsia, SessionEndAsia, barCloseUtc),
            BuildSession("London", "Europe/London", SessionStartLondon, SessionEndLondon, barCloseUtc),
            BuildSession("NY", "America/New_York", SessionStartNy, SessionEndNy, barCloseUtc)
        ];

    private static M15DbSessionCoverage BuildSession(string name, string timeZoneId, TimeOnly start, TimeOnly end, IReadOnlyList<DateTimeOffset> barCloseUtc)
    {
        var zone = ResolveTimeZone(timeZoneId);
        var localRows = barCloseUtc
            .Select(x => TimeZoneInfo.ConvertTime(x, zone))
            .Where(x =>
            {
                var localTime = TimeOnly.FromDateTime(x.DateTime);
                return localTime > start && localTime <= end;
            })
            .ToArray();
        var observedDates = localRows.Select(x => DateOnly.FromDateTime(x.DateTime)).Distinct().Count();
        var expected = observedDates * 36;
        var ratio = expected == 0 ? 0m : decimal.Round(localRows.Length / (decimal)expected, 6);
        return new M15DbSessionCoverage(name, timeZoneId, $"{start:HH:mm}-{end:HH:mm}", observedDates, localRows.Length, expected, ratio);
    }

    private static TimeZoneInfo ResolveTimeZone(string iana)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(iana);
        }
        catch (TimeZoneNotFoundException)
        {
            var windowsId = iana switch
            {
                "Asia/Tokyo" => "Tokyo Standard Time",
                "Europe/London" => "GMT Standard Time",
                "America/New_York" => "Eastern Standard Time",
                _ => iana
            };
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
    }

    private static IReadOnlyList<string> BuildGlobalIssues(
        IReadOnlyList<M15DbInstrumentCoverage> instruments,
        IReadOnlyList<string> instrumentsWithoutM15,
        IReadOnlyList<M15DbCandidateTable> candidateTables)
    {
        var issues = new List<string>();
        if (instruments.Count == 0) issues.Add("NoM15DataPresent");
        if (instrumentsWithoutM15.Count > 0) issues.Add("SomeAvailableInstrumentsHaveNoM15");
        if (candidateTables.Any(x => x.LooksLikeIntradayCandles && !x.SupportedByAudit)) issues.Add("UnsupportedCandleCandidateTablesRequireManualMapping");
        foreach (var issue in instruments.SelectMany(x => x.Issues).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(issue);
        }

        return issues;
    }

    private static M15DbCoverageGate ComputeGate(
        IReadOnlyList<M15DbInstrumentCoverage> instruments,
        IReadOnlyList<string> instrumentsWithoutM15,
        IReadOnlyList<string> globalIssues)
    {
        if (instruments.Count == 0) return M15DbCoverageGate.FAIL;
        if (instruments.Any(x => x.OffGridTimestampCount > 0 || x.CloseNullCount > 0 || x.CloseNonPositiveCount > 0)) return M15DbCoverageGate.FAIL;
        if (instruments.Any(x => x.EstimatedGapRatio > 0.05m)) return M15DbCoverageGate.FAIL;
        if (instruments.Any(x => x.DuplicateTimestampCount / (decimal)Math.Max(1, x.RowCount) > 0.005m)) return M15DbCoverageGate.FAIL;
        if (globalIssues.Any(x => x is "TimestampOffsetNotUtc")) return M15DbCoverageGate.FAIL;
        if (instruments.Any(x => x.DuplicateTimestampCount > 0 || x.NonMonotoneTimestampCount > 0 || x.EstimatedGapCount > 0 || x.Issues.Contains("TimestampRoleAmbiguousBarOpenVsBarClose")) ||
            instrumentsWithoutM15.Count > 0 ||
            globalIssues.Contains("UnsupportedCandleCandidateTablesRequireManualMapping"))
        {
            return M15DbCoverageGate.WARN;
        }

        return M15DbCoverageGate.PASS;
    }

    private static int CountNonMonotone(IReadOnlyList<DateTimeOffset> timestamps)
    {
        var count = 0;
        for (var i = 1; i < timestamps.Count; i++)
        {
            if (timestamps[i] < timestamps[i - 1])
            {
                count++;
            }
        }

        return count;
    }

    private static (int CountedGaps, int FxWeekendExcludedGaps) EstimateGaps(IReadOnlyList<DateTimeOffset> sortedDistinct)
    {
        if (sortedDistinct.Count < 2) return (0, 0);
        var gaps = 0;
        var fxWeekendExcludedGaps = 0;
        for (var i = 1; i < sortedDistinct.Count; i++)
        {
            var expected = sortedDistinct[i - 1].Add(M15);
            while (expected < sortedDistinct[i])
            {
                if (IsFxWeekendClosure(expected))
                {
                    fxWeekendExcludedGaps++;
                }
                else
                {
                    gaps++;
                }

                expected = expected.Add(M15);
            }
        }

        return (gaps, fxWeekendExcludedGaps);
    }

    private static bool IsFxWeekendClosure(DateTimeOffset timestampUtc)
    {
        var utc = timestampUtc.ToUniversalTime();
        var time = TimeOnly.FromDateTime(utc.DateTime);
        return utc.DayOfWeek == DayOfWeek.Saturday ||
               utc.DayOfWeek == DayOfWeek.Friday && time >= new TimeOnly(22, 0) ||
               utc.DayOfWeek == DayOfWeek.Sunday && time <= new TimeOnly(22, 0);
    }

    private static bool IsOnM15Grid(DateTimeOffset timestamp)
        => timestamp.Offset == TimeSpan.Zero &&
           timestamp.Second == 0 &&
           timestamp.Millisecond == 0 &&
           timestamp.Ticks % M15.Ticks == 0;

    private static bool IsTimestampColumn(string column)
        => column.Contains("Utc", StringComparison.OrdinalIgnoreCase) ||
           column.Contains("Timestamp", StringComparison.OrdinalIgnoreCase) ||
           column.Contains("Time", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Thresholds()
        =>
        [
            "PASS requires at least one M15 instrument, explicit UTC bar-close timestamps or documented bar-open +15m conversion, zero null/non-positive closes, zero off-grid timestamps, duplicate ratio 0, and estimated non-weekend gap ratio <= 1%.",
            "WARN allows present data with timestamp role ambiguity, duplicate ratio <= 0.5%, estimated non-weekend gap ratio <= 5%, non-monotone source order, unsupported candidate tables, or some configured instruments without M15.",
            "FAIL is emitted for no M15 data, non-UTC/off-grid timestamps, null or non-positive closes, estimated non-weekend gap ratio > 5%, or duplicate ratio > 0.5%. Missing bars inside the standard FX weekend closure Friday 22:00 UTC through Sunday 22:00 UTC are counted separately and do not create critical gaps."
        ];

    private static IReadOnlyList<string> SafetyAssertions()
        =>
        [
            "Read-only audit: database access is SELECT-only.",
            "No A.txt, H.txt, I.txt, FlatBar, replay, simulation, manager, Anubis, PMS, OMS, EMS, DB migration, or state mutation is performed.",
            "RunKey is the package identity; ProgramID remains legacy metadata only."
        ];

    private static string RenderInventoryMarkdown(M15DbInventory inventory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# M15 DB Inventory");
        builder.AppendLine();
        builder.AppendLine($"RunKey: `{inventory.RunKey}`");
        builder.AppendLine($"ProducedAtUtc: `{inventory.ProducedAtUtc:O}`");
        builder.AppendLine($"DB configuration source: `{inventory.DbConfigurationSource}`");
        builder.AppendLine($"Connection string: `{inventory.ConnectionStringRedacted}`");
        builder.AppendLine();
        builder.AppendLine("| Schema | Table | Candle Candidate | Supported | Reason |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var table in inventory.CandidateTables)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"| {table.Schema} | {table.Table} | {table.LooksLikeIntradayCandles} | {table.SupportedByAudit} | {table.Reason} |");
        }

        builder.AppendLine();
        builder.AppendLine("Safety assertions:");
        foreach (var assertion in inventory.SafetyAssertions)
        {
            builder.AppendLine($"- {assertion}");
        }

        return builder.ToString();
    }

    private static string RenderReportMarkdown(M15DbCoverageReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# M15 DB Coverage Report");
        builder.AppendLine();
        builder.AppendLine($"M15_DB_COVERAGE_GATE: `{report.M15_DB_COVERAGE_GATE}`");
        builder.AppendLine($"RunKey: `{report.RunKey}`");
        builder.AppendLine();
        builder.AppendLine("Audited sources:");
        foreach (var source in report.AuditedSources)
        {
            builder.AppendLine($"- {source.Schema}.{source.Table}: {source.Reason}");
        }

        builder.AppendLine();
        builder.AppendLine("Candidate tables discovered:");
        foreach (var table in report.CandidateTablesDiscovered)
        {
            builder.AppendLine($"- {table.Schema}.{table.Table}: candidate={table.LooksLikeIntradayCandles}, supported={table.SupportedByAudit}, reason={table.Reason}");
        }

        builder.AppendLine();
        builder.AppendLine("| Instrument | Venue | Rows | First UTC close | Last UTC close | Grid M15 | Bar-close semantics | Duplicates | Non-monotone | Off-grid | Close null | Close <= 0 | Est. gaps | FX weekend excluded | Gap ratio | Timestamp assessment |");
        builder.AppendLine("| --- | --- | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |");
        foreach (var item in report.Instruments)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"| {item.Instrument} | {item.Venue ?? ""} | {item.RowCount} | {item.FirstTimestampUtc:O} | {item.LastTimestampUtc:O} | {item.TimestampGridCompatibleM15} | {item.BarCloseSemantics} | {item.DuplicateTimestampCount} | {item.NonMonotoneTimestampCount} | {item.OffGridTimestampCount} | {item.CloseNullCount} | {item.CloseNonPositiveCount} | {item.EstimatedGapCount} | {item.FxWeekendExcludedGapCount} | {item.EstimatedGapRatio} | {item.TimestampAssessment} |");
        }

        builder.AppendLine();
        builder.AppendLine("Session coverage uses UTC bar-close timestamps converted into fixed local session windows: Asia/Tokyo 09:00-18:00, Europe/London 08:00-17:00, America/New_York 08:00-17:00.");
        foreach (var instrument in report.Instruments)
        {
            builder.AppendLine();
            builder.AppendLine($"## {instrument.Instrument} Sessions");
            builder.AppendLine("| Session | Time zone | Local window | Observed dates | Rows | Expected rows | Coverage |");
            builder.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: |");
            foreach (var session in instrument.Sessions)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"| {session.Session} | {session.TimeZoneId} | {session.LocalWindow} | {session.ObservedLocalDates} | {session.RowCount} | {session.ExpectedRowsOnObservedDates} | {session.CoverageRatio} |");
            }
        }

        if (report.InstrumentsWithoutM15.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Instruments without M15:");
            foreach (var instrument in report.InstrumentsWithoutM15)
            {
                builder.AppendLine($"- {instrument}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Thresholds:");
        foreach (var threshold in report.Thresholds)
        {
            builder.AppendLine($"- {threshold}");
        }

        builder.AppendLine();
        builder.AppendLine("Global issues:");
        foreach (var issue in report.GlobalIssues)
        {
            builder.AppendLine($"- {issue}");
        }

        builder.AppendLine();
        builder.AppendLine("Safety assertions:");
        foreach (var assertion in report.SafetyAssertions)
        {
            builder.AppendLine($"- {assertion}");
        }

        return builder.ToString();
    }

    private static JsonSerializerOptions JsonOptions()
        => new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

    private static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
