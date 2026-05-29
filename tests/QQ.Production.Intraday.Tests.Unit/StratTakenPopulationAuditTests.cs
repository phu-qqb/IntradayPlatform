using System.Buffers.Binary;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class StratTakenPopulationAuditTests
{
    [Fact]
    public void Empty_strattaken_warns_binary_and_fails_population()
    {
        using var fixture = AuditFixture.Create();
        var path = fixture.WriteStratTaken("MaatStratTaken.bin", []);

        var package = Run(fixture, path);

        Assert.Equal("WARN", package.BinaryIntegrity.STRATTAKEN_BINARY_GATE);
        Assert.Equal(0, package.BinaryIntegrity.StrategyCount);
        Assert.Equal("FAIL", package.Population.STRATTAKEN_POPULATION_GATE);
        Assert.Equal("YES", package.Attrition.STRATTAKEN_UNDERPOPULATION_EXPLAINS_ZERO_WEIGHTS);
    }

    [Fact]
    public void Strategy_count_incoherent_fails_binary_gate()
    {
        using var fixture = AuditFixture.Create();
        var path = fixture.WriteRaw("MaatStratTaken.bin", BuildRaw(declaredCount: 2, suids: [0]));

        var package = Run(fixture, path);

        Assert.Equal("FAIL", package.BinaryIntegrity.STRATTAKEN_BINARY_GATE);
        Assert.Contains("StratTakenTruncated", package.BinaryIntegrity.Issues);
    }

    [Fact]
    public void Truncated_file_fails_binary_gate()
    {
        using var fixture = AuditFixture.Create();
        var path = fixture.WriteRaw("MaatStratTaken.bin", [1, 0, 0, 0, 0, 0, 0]);

        var package = Run(fixture, path);

        Assert.Equal("FAIL", package.BinaryIntegrity.STRATTAKEN_BINARY_GATE);
        Assert.Contains("StratTakenTruncated", package.BinaryIntegrity.Issues);
    }

    [Fact]
    public void Correct_size_strattaken_passes_binary_gate()
    {
        using var fixture = AuditFixture.Create();
        var path = fixture.WriteStratTaken("MaatStratTaken.bin", Enumerable.Range(0, 120).Select(_ => 0).ToArray());

        var package = Run(fixture, path);

        Assert.Equal("PASS", package.BinaryIntegrity.STRATTAKEN_BINARY_GATE);
        Assert.Equal(4 + 120 * 108, package.BinaryIntegrity.FileSizeBytes);
    }

    [Fact]
    public void Single_suid_compatible_counts_survivors()
    {
        using var fixture = AuditFixture.Create();
        var path = fixture.WriteStratTaken("MaatStratTaken.bin", Enumerable.Range(0, 120).Select(_ => 0).ToArray());

        var package = Run(fixture, path);

        Assert.Equal(120, package.Population.StrategyCount);
        Assert.Equal(1, package.Population.DistinctSuidCount);
        Assert.Equal(120, package.Compatibility.SuidCompatibleStrategyCount);
        Assert.Equal("PASS", package.Compatibility.STRATTAKEN_PACKAGE_COMPATIBILITY_GATE);
    }

    [Fact]
    public void Suid_out_of_range_fails_compatibility_gate()
    {
        using var fixture = AuditFixture.Create(subUniverseCount: 1);
        var path = fixture.WriteStratTaken("MaatStratTaken.bin", [3]);

        var package = Run(fixture, path);

        Assert.Equal("FAIL", package.Compatibility.STRATTAKEN_PACKAGE_COMPATIBILITY_GATE);
        Assert.Equal(1, package.Compatibility.SuidRejectedStrategyCount);
        Assert.Contains("SuidOutOfRangeForPackageShape", package.Compatibility.Issues);
    }

    [Fact]
    public void Very_reduced_strattaken_is_flagged_as_smoke_fixture()
    {
        using var fixture = AuditFixture.Create();
        var path = fixture.WriteStratTaken("MaatStratTaken_reduced.bin", [0, 0, 0]);

        var package = Run(fixture, path);

        Assert.True(package.Population.LooksLikeSmokeFixture);
        Assert.Contains("CurrentStratTakenLooksLikeSmokeFixture", package.Population.Issues);
        Assert.Equal("WARN", package.Population.STRATTAKEN_POPULATION_GATE);
    }

    [Fact]
    public void Current_vs_full_reference_reports_retention_and_missing_suids()
    {
        using var fixture = AuditFixture.Create();
        var current = fixture.WriteStratTaken("MaatStratTaken.bin", [0, 0, 1, 1]);
        var full = fixture.WriteStratTaken("MaatStratTaken_All.bin", Enumerable.Range(0, 150).ToArray());

        var package = Run(fixture, current, full);

        Assert.NotNull(package.Population.FullReferenceComparison);
        Assert.Equal(4, package.Population.FullReferenceComparison!.CurrentCount);
        Assert.Equal(150, package.Population.FullReferenceComparison.FullCount);
        Assert.Equal(148, package.Population.FullReferenceComparison.MissingSuidCount);
    }

    [Fact]
    public void One_su_package_with_full_reference_suid_0_to_149_is_incompatible_if_used_raw()
    {
        using var fixture = AuditFixture.Create(subUniverseCount: 1);
        var path = fixture.WriteStratTaken("MaatStratTaken_All.bin", Enumerable.Range(0, 150).ToArray());

        var package = Run(fixture, path);

        Assert.Equal("FAIL", package.Compatibility.STRATTAKEN_PACKAGE_COMPATIBILITY_GATE);
        Assert.Equal(149, package.Compatibility.SuidRejectedStrategyCount);
        Assert.Contains("SuidOutOfRangeForPackageShape", package.Compatibility.Issues);
    }

    [Fact]
    public async Task Package_writer_creates_reports_under_validation_without_circular_hashes_or_ahi()
    {
        using var fixture = AuditFixture.Create();
        var path = fixture.WriteStratTaken("MaatStratTaken.bin", Enumerable.Range(0, 120).Select(_ => 0).ToArray());
        var package = Run(fixture, path);
        var validation = Path.Combine(fixture.Root, "out", "10_validation");

        await StratTakenPopulationAudit.WritePackageAsync(validation, package, CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(validation, "strattaken_binary_integrity_report.json")));
        Assert.True(File.Exists(Path.Combine(validation, "strattaken_population_report.md")));
        Assert.True(File.Exists(Path.Combine(validation, "strattaken_attrition_report.json")));
        Assert.True(File.Exists(Path.Combine(validation, "strattaken_code_path_report.md")));
        Assert.True(File.Exists(Path.Combine(validation, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(validation, "manifest.sha256")));
        Assert.True(File.Exists(Path.Combine(validation, "hashes.json")));
        Assert.False(File.Exists(Path.Combine(validation, "A.txt")));
        Assert.False(File.Exists(Path.Combine(validation, "H.txt")));
        Assert.False(File.Exists(Path.Combine(validation, "I.txt")));

        using var hashes = JsonDocument.Parse(File.ReadAllText(Path.Combine(validation, "hashes.json")));
        var hashedPaths = hashes.RootElement.EnumerateArray().Select(x => x.GetProperty("path").GetString()).ToArray();
        Assert.DoesNotContain("hashes.json", hashedPaths);
        Assert.DoesNotContain("manifest.sha256", hashedPaths);
    }

    [Fact]
    public void Tool_source_does_not_reference_manager_anubis_or_runtime_execution()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "tools/QQ.Production.Intraday.Tools.StratTakenPopulationAudit/Program.cs"));

        Assert.DoesNotContain("Anubis", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Manager", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PMS", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OMS", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EMS", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("A.txt", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("H.txt", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("I.txt", source, StringComparison.OrdinalIgnoreCase);
    }

    private static StratTakenPopulationAuditPackage Run(AuditFixture fixture, string path, string? fullReference = null)
        => StratTakenPopulationAudit.Audit(new StratTakenPopulationAuditRequest(
            "test-run-key",
            fixture.PackageRoot,
            path,
            fullReference,
            fixture.Root));

    private static byte[] BuildRaw(int declaredCount, IReadOnlyList<int> suids)
    {
        var bytes = new byte[4 + suids.Count * 108];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, 4), declaredCount);
        for (var index = 0; index < suids.Count; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + index * 108, 4), suids[index]);
        }

        return bytes;
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

    private sealed class AuditFixture : IDisposable
    {
        private AuditFixture(string root, string packageRoot)
        {
            Root = root;
            PackageRoot = packageRoot;
        }

        public string Root { get; }
        public string PackageRoot { get; }

        public static AuditFixture Create(int subUniverseCount = 1, int instrumentCount = 2, int variableCount = 3)
        {
            var root = Path.Combine(Path.GetTempPath(), "strattaken-audit-tests", Guid.NewGuid().ToString("N"));
            var packageRoot = Path.Combine(root, "package");
            Directory.CreateDirectory(packageRoot);
            File.WriteAllText(Path.Combine(packageRoot, "manifest.json"), $$"""
                {
                  "subUniverseCount": {{subUniverseCount}},
                  "instruments": [{{string.Join(",", Enumerable.Range(0, instrumentCount).Select(i => $"\"SYM{i}\""))}}],
                  "variables": [{{string.Join(",", Enumerable.Range(0, variableCount).Select(i => $"\"VAR{i}\""))}}]
                }
                """);
            File.WriteAllText(Path.Combine(packageRoot, "m15_time_series.csv"), "timestamp,value");
            return new AuditFixture(root, packageRoot);
        }

        public string WriteStratTaken(string name, IReadOnlyList<int> suids)
            => WriteRaw(name, BuildRaw(suids.Count, suids));

        public string WriteRaw(string name, byte[] bytes)
        {
            var path = Path.Combine(PackageRoot, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
