using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class NqM1StorageAuditTests
{
    [Fact]
    public void No_nq_m1_data_reports_no_locations()
    {
        var inventory = BuildInventory([], []);

        Assert.Equal("NO", inventory.NqM1DataExists);
        Assert.Equal(0, inventory.NqM1DataLocationCount);
        Assert.Equal("NO", inventory.SafeToExportM1ForYannik);
    }

    [Fact]
    public void Market_data_bars_with_nq_m1_are_detected()
    {
        var table = Table("M1_NQ_CANDIDATE", "HIGH", "TIMEFRAME_COLUMN", [Sample("NQH25", "NQ")], rowCount: 10);
        var inventory = BuildInventory([table], []);

        Assert.Equal("YES", inventory.NqM1DataExists);
        Assert.Equal("QQProductionIntraday:dbo.MarketDataBars", inventory.NqM1CanonicalLocation);
    }

    [Fact]
    public void Mnq_m1_only_is_not_nq()
    {
        var table = Table("M1_NON_NQ", "HIGH", "TIMEFRAME_COLUMN", [Sample("MNQH25", "MNQ")], rowCount: 10);
        var inventory = BuildInventory([table], []);

        Assert.Equal("NO", inventory.NqM1DataExists);
    }

    [Fact]
    public void Nq_m5_is_not_m1()
    {
        var table = Table("NQ_NOT_M1", "NONE", "UNKNOWN", [Sample("NQH25", "NQ")], rowCount: 10);
        var inventory = BuildInventory([table], []);

        Assert.Equal("NO", inventory.NqM1DataExists);
    }

    [Fact]
    public void Csv_nq_m1_file_is_detected()
    {
        var file = FileReport("data/NQH25_M1.csv", "NQ_M1_FILE_FOUND", "NQH25", rowCount: 2, sampleHash: "abc");
        var inventory = BuildInventory([], [file]);

        Assert.Equal("YES", inventory.NqM1DataExists);
        Assert.Equal("data/NQH25_M1.csv", inventory.NqM1CanonicalLocation);
    }

    [Fact]
    public void Parquet_nq_m1_metadata_only_is_ambiguous_not_counted_as_found()
    {
        var file = FileReport("data/NQH25_M1.parquet", "POSSIBLE_NQ_BUT_AMBIGUOUS", "NQH25", rowCount: null, sampleHash: null);
        var inventory = BuildInventory([], [file]);

        Assert.Equal("NO", inventory.NqM1DataExists);
    }

    [Fact]
    public void Db_and_file_with_same_sample_are_confirmed_duplicates()
    {
        var locations = new[]
        {
            new NqM1StorageLocation("DB", "db:bars", "M1_NQ_CANDIDATE", 2, "2026-01-01T00:00:00Z", "2026-01-01T00:01:00Z", ["NQH25"], "HIGH", "TIMEFRAME_COLUMN", "same", []),
            new NqM1StorageLocation("FILE", "data/NQH25_M1.csv", "NQ_M1_FILE_FOUND", 2, "2026-01-01T00:00:00Z", "2026-01-01T00:01:00Z", ["NQH25"], "HIGH", "FILE_NAME", "same", [])
        };

        var duplicate = NqM1StorageAuditService.BuildDuplicateReport("run", locations);

        Assert.Equal("CONFIRMED", duplicate.DuplicateStorageStatus);
    }

    [Fact]
    public void Db_and_file_with_different_periods_are_not_duplicates()
    {
        var locations = new[]
        {
            new NqM1StorageLocation("DB", "db:bars", "M1_NQ_CANDIDATE", 2, "2026-01-01T00:00:00Z", "2026-01-01T00:01:00Z", ["NQH25"], "HIGH", "TIMEFRAME_COLUMN", "left", []),
            new NqM1StorageLocation("FILE", "data/NQH25_M1.csv", "NQ_M1_FILE_FOUND", 2, "2026-02-01T00:00:00Z", "2026-02-01T00:01:00Z", ["NQH25"], "HIGH", "FILE_NAME", "right", [])
        };

        var duplicate = NqM1StorageAuditService.BuildDuplicateReport("run", locations);

        Assert.Equal("UNKNOWN", duplicate.DuplicateStorageStatus);
    }

    [Theory]
    [InlineData("NQH25", "NQ")]
    [InlineData("NQH2025", "NQ")]
    [InlineData("/NQ", "NQ")]
    [InlineData("NQ1!", "NQ")]
    [InlineData("MNQH25", "MNQ")]
    public void Symbol_classifier_separates_nq_and_mnq(string symbol, string expected)
    {
        Assert.Equal(expected, NqM1SymbolClassifier.Classify(symbol).Kind);
    }

    [Fact]
    public void M1_can_be_inferred_from_timestamp_delta()
    {
        var timestamps = new[]
        {
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-01-01T00:01:00Z"),
            DateTimeOffset.Parse("2026-01-01T00:02:00Z"),
            DateTimeOffset.Parse("2026-01-01T00:03:00Z")
        };

        var m1 = NqM1StorageAuditService.InferM1("bars", new Dictionary<string, string?>(), timestamps);

        Assert.Equal("MEDIUM", m1.Confidence);
        Assert.Equal("TIMESTAMP_DELTA", m1.Evidence);
    }

    [Fact]
    public void Production_tool_source_avoids_mutation_and_execution_surfaces()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "tools/QQ.Production.Intraday.Tools.NqM1StorageAudit/Program.cs"));

        Assert.DoesNotContain("ExecuteNonQuery", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migrate", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EnsureCreated", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Anubis", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PMS", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OMS", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EMS", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("A.txt", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("H.txt", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("I.txt", source, StringComparison.OrdinalIgnoreCase);
    }

    private static NqM1StorageInventoryReport BuildInventory(IReadOnlyList<NqM1DbTableReport> tables, IReadOnlyList<NqM1FileReport> files)
    {
        var config = new NqM1ConfigDiscovery([], [], [], [], [], []);
        var db = new NqM1DbDiscoveryReport("run", DateTimeOffset.UtcNow, ["QQProductionIntraday"], tables);
        var file = new NqM1FileDiscoveryReport("run", DateTimeOffset.UtcNow, ".", [], files);
        return NqM1StorageAuditService.BuildInventory("run", config, db, file, "NO");
    }

    private static NqM1DbTableReport Table(string status, string confidence, string evidence, IReadOnlyList<NqM1SymbolSample> samples, long rowCount)
        => new("QQProductionIntraday", "dbo.MarketDataBars", rowCount, ["InstrumentId", "Timeframe", "BarStartUtc", "Open", "High", "Low", "Close"], ["BarStartUtc"], ["InstrumentId"], ["Timeframe"], ["Open", "High", "Low", "Close"], ["Volume"], "2026-01-01T00:00:00Z", "2026-01-01T00:01:00Z", samples, confidence, evidence, status, []);

    private static NqM1SymbolSample Sample(string value, string classification)
        => new("Symbol", value, 1, [value], classification, false, "test");

    private static NqM1FileReport FileReport(string path, string status, string symbol, long? rowCount, string? sampleHash)
        => new(path, 10, Path.GetExtension(path).TrimStart('.'), ["timestamp", "symbol", "open", "high", "low", "close"], symbol, "M1", rowCount, "2026-01-01T00:00:00Z", "2026-01-01T00:01:00Z", "HIGH", "TIMEFRAME_COLUMN", status, sampleHash, []);

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
