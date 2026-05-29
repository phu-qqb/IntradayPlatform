using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class M15DbCoverageAuditTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 25, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Empty_db_fails()
    {
        var package = Audit([]);

        Assert.Equal("FAIL", package.Report.M15_DB_COVERAGE_GATE);
        Assert.Contains("NoM15DataPresent", package.Report.GlobalIssues);
    }

    [Fact]
    public void Clean_m15_data_passes()
    {
        var rows = Enumerable.Range(0, 36)
            .Select(i => Row("EURUSD", new DateTimeOffset(2026, 05, 25, 8, 15, 00, TimeSpan.Zero).AddMinutes(i * 15), 1.1m, i))
            .ToArray();

        var package = Audit(rows, availableInstruments: ["EURUSD"]);

        Assert.Equal("PASS", package.Report.M15_DB_COVERAGE_GATE);
        Assert.Single(package.Report.Instruments);
        Assert.Equal(0, package.Report.Instruments.Single().EstimatedGapCount);
        Assert.True(package.Report.Instruments.Single().TimestampGridCompatibleM15);
        Assert.Equal("ConfirmedUtcBarClose", package.Report.Instruments.Single().BarCloseSemantics);
        Assert.Equal("PASS: timestamps are explicit UTC bar-close.", package.Report.Instruments.Single().TimestampAssessment);
    }

    [Fact]
    public void Duplicate_timestamps_warn_when_under_documented_threshold()
    {
        var rows = Enumerable.Range(0, 399)
            .Select(i => Row("EURUSD", new DateTimeOffset(2026, 05, 25, 8, 15, 00, TimeSpan.Zero).AddMinutes(i * 15), 1.1m, i))
            .Append(Row("EURUSD", new DateTimeOffset(2026, 05, 25, 8, 15, 00, TimeSpan.Zero), 1.1m, 399))
            .ToArray();

        var package = Audit(rows);

        Assert.Equal("WARN", package.Report.M15_DB_COVERAGE_GATE);
        Assert.Equal(1, package.Report.Instruments.Single().DuplicateTimestampCount);
        Assert.Contains("DuplicateTimestamps", package.Report.GlobalIssues);
    }

    [Fact]
    public void M15_gaps_warn_when_under_fail_threshold()
    {
        var rows =
            new[]
            {
                Row("EURUSD", "2026-05-25T08:15:00Z", 1.1m, 0),
                Row("EURUSD", "2026-05-25T08:30:00Z", 1.1m, 1),
                Row("EURUSD", "2026-05-25T09:00:00Z", 1.1m, 2),
                Row("EURUSD", "2026-05-25T09:15:00Z", 1.1m, 3),
                Row("EURUSD", "2026-05-25T09:30:00Z", 1.1m, 4),
                Row("EURUSD", "2026-05-25T09:45:00Z", 1.1m, 5),
                Row("EURUSD", "2026-05-25T10:00:00Z", 1.1m, 6),
                Row("EURUSD", "2026-05-25T10:15:00Z", 1.1m, 7),
                Row("EURUSD", "2026-05-25T10:30:00Z", 1.1m, 8),
                Row("EURUSD", "2026-05-25T10:45:00Z", 1.1m, 9),
                Row("EURUSD", "2026-05-25T11:00:00Z", 1.1m, 10),
                Row("EURUSD", "2026-05-25T11:15:00Z", 1.1m, 11),
                Row("EURUSD", "2026-05-25T11:30:00Z", 1.1m, 12),
                Row("EURUSD", "2026-05-25T11:45:00Z", 1.1m, 13),
                Row("EURUSD", "2026-05-25T12:00:00Z", 1.1m, 14),
                Row("EURUSD", "2026-05-25T12:15:00Z", 1.1m, 15),
                Row("EURUSD", "2026-05-25T12:30:00Z", 1.1m, 16),
                Row("EURUSD", "2026-05-25T12:45:00Z", 1.1m, 17),
                Row("EURUSD", "2026-05-25T13:00:00Z", 1.1m, 18),
                Row("EURUSD", "2026-05-25T13:15:00Z", 1.1m, 19),
                Row("EURUSD", "2026-05-25T13:30:00Z", 1.1m, 20),
                Row("EURUSD", "2026-05-25T13:45:00Z", 1.1m, 21),
                Row("EURUSD", "2026-05-25T14:00:00Z", 1.1m, 22),
                Row("EURUSD", "2026-05-25T14:15:00Z", 1.1m, 23),
                Row("EURUSD", "2026-05-25T14:30:00Z", 1.1m, 24),
                Row("EURUSD", "2026-05-25T14:45:00Z", 1.1m, 25),
                Row("EURUSD", "2026-05-25T15:00:00Z", 1.1m, 26),
                Row("EURUSD", "2026-05-25T15:15:00Z", 1.1m, 27),
                Row("EURUSD", "2026-05-25T15:30:00Z", 1.1m, 28),
                Row("EURUSD", "2026-05-25T15:45:00Z", 1.1m, 29)
            };

        var package = Audit(rows);

        Assert.Equal("WARN", package.Report.M15_DB_COVERAGE_GATE);
        Assert.Equal(1, package.Report.Instruments.Single().EstimatedGapCount);
        Assert.Contains("EstimatedGapsAbovePassThreshold", package.Report.GlobalIssues);
    }

    [Fact]
    public void Close_null_fails()
    {
        var package = Audit([Row("EURUSD", "2026-05-25T08:15:00Z", null, 0)]);

        Assert.Equal("FAIL", package.Report.M15_DB_COVERAGE_GATE);
        Assert.Equal(1, package.Report.Instruments.Single().CloseNullCount);
    }

    [Fact]
    public void Close_non_positive_fails()
    {
        var package = Audit([Row("EURUSD", "2026-05-25T08:15:00Z", 0m, 0)]);

        Assert.Equal("FAIL", package.Report.M15_DB_COVERAGE_GATE);
        Assert.Equal(1, package.Report.Instruments.Single().CloseNonPositiveCount);
        Assert.Contains("CloseNonPositive", package.Report.GlobalIssues);
    }

    [Fact]
    public void Ambiguous_bar_open_vs_bar_close_warns()
    {
        var rows = Enumerable.Range(0, 36)
            .Select(i => Row("EURUSD", new DateTimeOffset(2026, 05, 25, 8, 00, 00, TimeSpan.Zero).AddMinutes(i * 15), 1.1m, i, M15TimestampRole.UnknownUtcGridTimestamp))
            .ToArray();

        var package = Audit(rows);

        Assert.Equal("WARN", package.Report.M15_DB_COVERAGE_GATE);
        Assert.True(package.Report.Instruments.Single().TimestampGridCompatibleM15);
        Assert.Equal("UnknownUtcGridTimestamp", package.Report.Instruments.Single().BarCloseSemantics);
        Assert.Contains("bar-open versus bar-close semantics are ambiguous", package.Report.Instruments.Single().TimestampAssessment);
    }

    [Fact]
    public void Instrument_without_m15_warns_when_other_m15_exists()
    {
        var rows = Enumerable.Range(0, 36)
            .Select(i => Row("EURUSD", new DateTimeOffset(2026, 05, 25, 8, 15, 00, TimeSpan.Zero).AddMinutes(i * 15), 1.1m, i))
            .ToArray();

        var package = Audit(rows, availableInstruments: ["EURUSD", "GBPUSD"]);

        Assert.Equal("WARN", package.Report.M15_DB_COVERAGE_GATE);
        Assert.Contains("GBPUSD", package.Report.InstrumentsWithoutM15);
    }

    [Fact]
    public void Fx_weekend_closure_is_not_counted_as_critical_gap()
    {
        var rows =
            new[]
            {
                Row("EURUSD", "2026-05-22T21:45:00Z", 1.1m, 0),
                Row("EURUSD", "2026-05-24T22:15:00Z", 1.1m, 1)
            };

        var package = Audit(rows, availableInstruments: ["EURUSD"]);
        var instrument = package.Report.Instruments.Single();

        Assert.Equal("PASS", package.Report.M15_DB_COVERAGE_GATE);
        Assert.Equal(0, instrument.EstimatedGapCount);
        Assert.True(instrument.FxWeekendExcludedGapCount > 0);
    }

    [Fact]
    public void Report_exposes_candidate_tables_audited_sources_and_marketdatabars_selection_reason()
    {
        var package = Audit([Row("EURUSD", "2026-05-25T08:15:00Z", 1.1m, 0)]);

        Assert.Contains(package.Report.CandidateTablesDiscovered, x => x.Table == "MarketDataBars");
        var audited = Assert.Single(package.Report.AuditedSources);
        Assert.Equal("MarketDataBars", audited.Table);
        Assert.Contains("canonical", audited.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Package_writer_creates_validation_artifacts_manifest_hashes_and_no_ahi()
    {
        var directory = Path.Combine(Path.GetTempPath(), "m15-db-audit-tests", Guid.NewGuid().ToString("N"), "10_validation");
        var package = Audit([Row("EURUSD", "2026-05-25T08:15:00Z", 1.1m, 0)]);

        await M15DbCoverageAudit.WritePackageAsync(directory, package, CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(directory, "m15_db_inventory.json")));
        Assert.True(File.Exists(Path.Combine(directory, "m15_db_inventory.md")));
        Assert.True(File.Exists(Path.Combine(directory, "m15_db_coverage_report.json")));
        Assert.True(File.Exists(Path.Combine(directory, "m15_db_coverage_report.md")));
        Assert.True(File.Exists(Path.Combine(directory, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(directory, "manifest.sha256")));
        Assert.True(File.Exists(Path.Combine(directory, "hashes.json")));
        Assert.False(File.Exists(Path.Combine(directory, "A.txt")));
        Assert.False(File.Exists(Path.Combine(directory, "H.txt")));
        Assert.False(File.Exists(Path.Combine(directory, "I.txt")));

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
        Assert.True(manifest.RootElement.GetProperty("readOnly").GetBoolean());
        Assert.True(manifest.RootElement.GetProperty("noLegacyAhiGenerated").GetBoolean());

        using var hashes = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "hashes.json")));
        var hashedPaths = hashes.RootElement.EnumerateArray().Select(x => x.GetProperty("path").GetString()).ToArray();
        Assert.DoesNotContain("hashes.json", hashedPaths);
        Assert.DoesNotContain("manifest.sha256", hashedPaths);
    }

    [Fact]
    public void Command_source_keeps_database_path_read_only()
    {
        var source = File.ReadAllText(ToolProgramPath());

        Assert.Contains("ApplicationIntent.ReadOnly", source, StringComparison.Ordinal);
        Assert.Contains("ExecuteReaderAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteNonQuery", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SaveChanges", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migrate(", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EnsureCreated", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT ", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MERGE ", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TRUNCATE ", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP ", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER ", source, StringComparison.OrdinalIgnoreCase);
    }

    private static M15DbAuditPackage Audit(
        IReadOnlyList<M15DbCandleRow> rows,
        IReadOnlyList<string>? availableInstruments = null)
        => M15DbCoverageAudit.Audit(new M15DbCoverageAuditRequest(
            "test-run-key",
            ProducedAt,
            CandidateTables(),
            rows,
            availableInstruments ?? rows.Select(x => x.Instrument).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            "test",
            "Server=.;Database=test;Trusted_Connection=True"));

    private static IReadOnlyList<M15DbCandidateTable> CandidateTables()
        =>
        [
            new(
                "dbo",
                "MarketDataBars",
                ["InstrumentId", "VenueId", "Timeframe", "BarStartUtc", "BarEndUtc", "MidClose"],
                LooksLikeIntradayCandles: true,
                SupportedByAudit: true,
                "Supported canonical intraday bar table.")
        ];

    private static M15DbCandleRow Row(string instrument, string timestampUtc, decimal? close, int ordinal)
        => Row(instrument, DateTimeOffset.Parse(timestampUtc), close, ordinal);

    private static M15DbCandleRow Row(
        string instrument,
        DateTimeOffset timestampUtc,
        decimal? close,
        int ordinal,
        M15TimestampRole role = M15TimestampRole.BarCloseUtc)
        => new(instrument, "TestVenue", timestampUtc, role, close, ordinal);

    private static string ToolProgramPath()
        => Path.Combine(RepoRoot(), "tools/QQ.Production.Intraday.Tools.M15DbCoverageAudit/Program.cs");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be found.");
    }
}
