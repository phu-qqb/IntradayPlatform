using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class NqTickExportTests
{
    [Fact]
    public async Task No_nq_ticks_produces_fail_metadata_without_fake_empty_data_file()
    {
        using var fixture = ExportFixture.Create();
        var result = await ExportAsync(fixture, []);

        Assert.Equal("FAIL", result.Metadata.ExportStatus);
        Assert.Equal(0, result.Metadata.DataFile.RowCount);
        Assert.Null(result.DataFilePath);
    }

    [Fact]
    public async Task Simple_nq_trade_ticks_are_exported()
    {
        using var fixture = ExportFixture.Create();
        var result = await ExportAsync(fixture, [Trade("NQH25", "2026-05-26T12:00:00Z", "18750.25", "2")]);

        Assert.Equal("PASS", result.Metadata.ExportStatus);
        Assert.Equal(1, result.Metadata.DataFile.RowCount);
        Assert.Contains("NQH25", result.Metadata.Instrument.ContractsIncluded);
    }

    [Fact]
    public async Task Mnq_only_is_excluded_and_fails_nq_export()
    {
        using var fixture = ExportFixture.Create();
        var result = await ExportAsync(fixture, [Trade("MNQH25", "2026-05-26T12:00:00Z", "18750.25", "2")]);

        Assert.Equal("FAIL", result.Metadata.ExportStatus);
        Assert.Equal(0, result.Metadata.DataFile.RowCount);
        Assert.Contains("MNQH25", result.Metadata.Instrument.ContractsDetectedButExcluded);
    }

    [Fact]
    public async Task Mixed_nq_and_mnq_exports_only_nq_and_reports_mnq()
    {
        using var fixture = ExportFixture.Create();
        var result = await ExportAsync(fixture,
        [
            Trade("NQH25", "2026-05-26T12:00:00Z", "18750.25", "2"),
            Trade("MNQH25", "2026-05-26T12:00:01Z", "18751.00", "1")
        ]);

        Assert.Equal("WARN", result.Metadata.ExportStatus);
        Assert.Equal(1, result.Metadata.DataFile.RowCount);
        Assert.Contains("MNQH25", result.Metadata.Instrument.ContractsDetectedButExcluded);
    }

    [Fact]
    public async Task Multiple_nq_contracts_are_reported()
    {
        using var fixture = ExportFixture.Create();
        var result = await ExportAsync(fixture,
        [
            Trade("NQH25", "2026-05-26T12:00:00Z", "18750.25", "2"),
            Trade("NQM25", "2026-05-26T12:00:01Z", "18751.00", "1")
        ]);

        Assert.Equal(["NQH25", "NQM25"], result.Metadata.Instrument.ContractsIncluded);
        Assert.Equal(2, result.Metadata.Coverage.PerContract.Count);
    }

    [Fact]
    public async Task Quote_bbo_rows_are_exported_when_tick_kind_all()
    {
        using var fixture = ExportFixture.Create();
        var result = await ExportAsync(fixture,
        [
            Quote("NQH25", "2026-05-26T12:00:00Z", "18750.00", "3", "18750.25", "4")
        ], tickKind: NqTickKind.All);

        Assert.Equal("PASS", result.Metadata.ExportStatus);
        Assert.Equal(1, result.Metadata.Coverage.PerContract.Single().EventTypeCounts["bbo"]);
    }

    [Fact]
    public async Task Unknown_timezone_without_assumption_fails()
    {
        using var fixture = ExportFixture.Create();
        var result = await ExportAsync(fixture, [Trade("NQH25", null, "18750.25", "2", sourceTimestamp: "2026-05-26 08:00:00", sourceTimezone: "unknown")]);

        Assert.Equal("FAIL", result.Metadata.ExportStatus);
        Assert.Contains(result.Metadata.Failures, x => x.Contains("Timezone", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Known_timezone_exports_with_assumption_flag()
    {
        using var fixture = ExportFixture.Create();
        var result = await ExportAsync(fixture, [Trade("NQH25", "2026-05-26T12:00:00Z", "18750.25", "2", sourceTimezone: "America/New_York")], assumeTimezone: "America/New_York");

        Assert.Equal("PASS", result.Metadata.ExportStatus);
        Assert.True(result.Metadata.Quality.TimezoneAssumptionUsed);
    }

    [Fact]
    public async Task From_to_filters_are_applied()
    {
        using var fixture = ExportFixture.Create();
        var result = await ExportAsync(fixture,
        [
            Trade("NQH25", "2026-05-26T11:59:00Z", "1", "1"),
            Trade("NQH25", "2026-05-26T12:00:00Z", "2", "1"),
            Trade("NQH25", "2026-05-26T12:01:00Z", "3", "1")
        ], from: DateTimeOffset.Parse("2026-05-26T12:00:00Z"), to: DateTimeOffset.Parse("2026-05-26T12:00:30Z"));

        Assert.Equal(1, result.Metadata.DataFile.RowCount);
    }

    [Fact]
    public async Task Contract_filter_is_applied()
    {
        using var fixture = ExportFixture.Create();
        var result = await ExportAsync(fixture,
        [
            Trade("NQH25", "2026-05-26T12:00:00Z", "1", "1"),
            Trade("NQM25", "2026-05-26T12:01:00Z", "2", "1")
        ], contract: "NQM25");

        Assert.Equal(["NQM25"], result.Metadata.Instrument.ContractsIncluded);
    }

    [Fact]
    public async Task Max_rows_marks_export_incomplete()
    {
        using var fixture = ExportFixture.Create();
        var result = await ExportAsync(fixture,
        [
            Trade("NQH25", "2026-05-26T12:00:00Z", "1", "1"),
            Trade("NQH25", "2026-05-26T12:01:00Z", "2", "1")
        ], maxRows: 1);

        Assert.Equal(1, result.Metadata.DataFile.RowCount);
        Assert.False(result.Metadata.ExportIsComplete);
    }

    [Fact]
    public async Task Csv_gzip_contains_expected_columns()
    {
        using var fixture = ExportFixture.Create();
        var result = await ExportAsync(fixture, [Trade("NQH25", "2026-05-26T12:00:00Z", "1", "1")]);

        var header = ReadGzipLines(result.DataFilePath!).First();

        Assert.Equal(string.Join(",", NqTickExportService.CsvColumns), header);
    }

    [Fact]
    public async Task Metadata_sha256_matches_data_file()
    {
        using var fixture = ExportFixture.Create();
        var result = await ExportAsync(fixture, [Trade("NQH25", "2026-05-26T12:00:00Z", "1", "1")]);

        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(result.DataFilePath!))).ToLowerInvariant();

        Assert.Equal(hash, result.Metadata.DataFile.Sha256);
    }

    [Fact]
    public async Task Export_never_creates_ahi_files()
    {
        using var fixture = ExportFixture.Create();
        await ExportAsync(fixture, [Trade("NQH25", "2026-05-26T12:00:00Z", "1", "1")]);

        Assert.False(File.Exists(Path.Combine(fixture.OutputRoot, "share", "A.txt")));
        Assert.False(File.Exists(Path.Combine(fixture.OutputRoot, "share", "H.txt")));
        Assert.False(File.Exists(Path.Combine(fixture.OutputRoot, "share", "I.txt")));
    }

    [Fact]
    public void Tool_source_does_not_call_manager_anubis_or_execution_surfaces()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "tools/QQ.Production.Intraday.Tools.NqTickExport/Program.cs"));

        Assert.DoesNotContain("Anubis", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Manager", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PMS", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OMS", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EMS", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migrate", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EnsureCreated", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExecuteNonQuery", source, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<NqTickExportResult> ExportAsync(
        ExportFixture fixture,
        IReadOnlyList<NqTickSourceRow> rows,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? contract = null,
        int? maxRows = null,
        NqTickKind? tickKind = NqTickKind.All,
        string? assumeTimezone = null)
    {
        var source = new FakeNqTickExportSource(rows);
        return await NqTickExportService.ExportAsync(source, new NqTickExportRequest(
            "nq-test-run",
            fixture.OutputRoot,
            from,
            to,
            contract,
            maxRows,
            tickKind,
            assumeTimezone,
            NoExecution: true), CancellationToken.None);
    }

    private static NqTickSourceRow Trade(string contract, string? timestampUtc, string price, string size, string sourceTimestamp = "", string sourceTimezone = "UTC")
        => new(
            timestampUtc is null ? null : DateTimeOffset.Parse(timestampUtc, CultureInfo.InvariantCulture),
            string.IsNullOrWhiteSpace(sourceTimestamp) ? timestampUtc ?? string.Empty : sourceTimestamp,
            sourceTimezone,
            contract.StartsWith("MNQ", StringComparison.OrdinalIgnoreCase) ? "MNQ" : "NQ",
            contract,
            "trade",
            price,
            size,
            null,
            null,
            null,
            null,
            "CME",
            "1",
            null,
            "dbo.NqTicks",
            Guid.NewGuid().ToString("N"));

    private static NqTickSourceRow Quote(string contract, string timestampUtc, string bid, string bidSize, string ask, string askSize)
        => new(
            DateTimeOffset.Parse(timestampUtc, CultureInfo.InvariantCulture),
            timestampUtc,
            "UTC",
            "NQ",
            contract,
            "bbo",
            null,
            null,
            bid,
            bidSize,
            ask,
            askSize,
            "CME",
            "1",
            null,
            "dbo.NqQuotes",
            Guid.NewGuid().ToString("N"));

    private static IReadOnlyList<string> ReadGzipLines(string path)
    {
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be found.");
    }

    private sealed class FakeNqTickExportSource(IReadOnlyList<NqTickSourceRow> rows) : INqTickExportSource
    {
        public string DatabaseDescription => "fake-test-db";

        public Task<IReadOnlyList<NqTickCandidateTable>> DiscoverTablesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<NqTickCandidateTable>>(
            [
                new("dbo", "NqTicks", ["timestamp", "root", "contract", "price", "size", "bid_price", "ask_price"], "timestamp", "root", "contract", "price", "size", "bid_price", "bid_size", "ask_price", "ask_size", "exchange", "sequence", "conditions", "source", "id", true, true, "fake")
            ]);

        public async IAsyncEnumerable<NqTickSourceRow> ReadRowsAsync(NqTickExportQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var row in rows.OrderBy(x => x.TimestampUtc).ThenBy(x => x.Contract).ThenBy(x => x.Sequence).ThenBy(x => x.SourceRowId))
            {
                yield return row;
                await Task.Yield();
            }
        }
    }

    private sealed class ExportFixture : IDisposable
    {
        private ExportFixture(string root)
        {
            Root = root;
            OutputRoot = Path.Combine(root, "out");
        }

        public string Root { get; }
        public string OutputRoot { get; }

        public static ExportFixture Create()
            => new(Path.Combine(Path.GetTempPath(), "nq-tick-export-tests", Guid.NewGuid().ToString("N")));

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
