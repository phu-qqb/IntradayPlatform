using System.Diagnostics;
using System.Security.Cryptography;
using QQ.Production.Intraday.Infrastructure.PostgreSql;
using CorePackageFixture =
    QQ.Production.Intraday.Tests.Unit.Arch7bBracketedGlobalFlatPositionSnapshotTests.CorePackageFixture;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bOfflineCoreConsumerBridgeTests
{
    [Fact]
    public void Bridge_uses_real_reader_builder_import_planner_and_runtime_selector_twenty_times()
    {
        using var fixture = new CorePackageFixture();
        string? evidenceSha = null;
        for (var index = 0; index < 20; index++)
        {
            var output = TempRoot("arch7b-offline-bridge-positive");
            try
            {
                var request = Request(fixture, output, index);
                var result = Arch7bOfflineCoreConsumerBridgeRunner.Run(request);
                Assert.Equal(Arch7bOfflineCoreConsumerBridgeContract.Result, result.Result);
                Assert.Equal(99, result.NormalizedCount);
                Assert.Equal(99, result.DerivedZeroCount);
                Assert.Equal(0, result.UnknownCount);
                Assert.Equal(1, result.PositionSnapshotRowsToAdd);
                Assert.Equal(99, result.PositionSnapshotLineRowsToAdd);
                Assert.True(result.NoSecret);
                Assert.True(result.NoDatabase);
                Assert.True(result.NoLmax);
                Assert.True(result.NoFix);
                Assert.True(result.NoOrder);
                evidenceSha ??= result.EvidenceSha256;
                Assert.Equal(evidenceSha, result.EvidenceSha256);
            }
            finally
            {
                DeleteRoot(output);
            }
        }
    }

    [Theory]
    [InlineData("final_index")]
    [InlineData("acquisition")]
    [InlineData("bracket_contract")]
    [InlineData("downloader")]
    [InlineData("account")]
    [InlineData("source_session")]
    [InlineData("p2")]
    [InlineData("execution_semantic")]
    [InlineData("position_semantic")]
    [InlineData("source_ingestion")]
    [InlineData("required_universe")]
    [InlineData("consumer_binary")]
    [InlineData("normalized_count")]
    [InlineData("zero_count")]
    [InlineData("unknown_count")]
    [InlineData("core_commit")]
    [InlineData("intraday_commit")]
    [InlineData("core_tree")]
    public void Negative_matrix_blocks_before_qualification(string mismatch)
    {
        using var fixture = new CorePackageFixture();
        var output = TempRoot("arch7b-offline-bridge-negative");
        try
        {
            var request = Request(fixture, output, 0);
            request = mismatch switch
            {
                "final_index" => request with { ExpectedFinalIndexSha256 = Hash('9') },
                "acquisition" => request with
                {
                    ExpectedAcquisitionManifestSha256 = Hash('9')
                },
                "bracket_contract" => request with
                {
                    ExpectedBracketContractVersion = "wrong"
                },
                "downloader" => request with { ExpectedDownloaderVersion = "wrong" },
                "account" => request with { ExpectedAccount = "wrong" },
                "source_session" => request with { ExpectedSourceSessionId = "wrong" },
                "p2" => request with
                {
                    ExpectedPositionReportP2Utc =
                        request.ExpectedPositionReportP2Utc.AddSeconds(1)
                },
                "execution_semantic" => request with
                {
                    ExpectedExecutionSemanticSha256 = Hash('9')
                },
                "position_semantic" => request with
                {
                    ExpectedPositionSemanticSha256 = Hash('9')
                },
                "source_ingestion" => request with
                {
                    ExpectedSourceIngestionId = Guid.Empty
                },
                "required_universe" => request with
                {
                    ExpectedRequiredUniverseSha256 = Hash('9')
                },
                "consumer_binary" => request with
                {
                    ExpectedConsumerExecutableSha256 = Hash('9')
                },
                "normalized_count" => request with { ExpectedNormalizedCount = 98 },
                "zero_count" => request with { ExpectedDerivedZeroCount = 98 },
                "unknown_count" => request with { ExpectedUnknownCount = 1 },
                "core_commit" => request with { CoreRepositoryCommit = Sha('9', 40) },
                "intraday_commit" => request with
                {
                    IntradayRepositoryCommit = Sha('9', 40)
                },
                "core_tree" => request with { CoreTree = Sha('9', 40) },
                _ => throw new InvalidOperationException(mismatch)
            };
            var exception = Assert.Throws<InvalidDataException>(
                () => Arch7bOfflineCoreConsumerBridgeRunner.Run(request));
            Assert.Equal(ExpectedCode(mismatch), exception.Message);
            Assert.False(File.Exists(Path.Combine(output,
                "offline-core-consumer-bridge-manifest.json")));
        }
        finally
        {
            DeleteRoot(output);
        }
    }

    private static Arch7bOfflineCoreConsumerBridgeRequest Request(
        CorePackageFixture fixture,
        string output,
        int identity)
    {
        var core = Arch7bCoreBracketEvidencePackageReader.Read(
            fixture.Root, fixture.Expectations);
        var historical = Arch7bHistoricalPmsUniverseFixtureFactory.Build();
        var executable = typeof(Arch7bOfflineCoreConsumerBridgeRunner)
            .Assembly.Location;
        var productVersion = FileVersionInfo.GetVersionInfo(executable).ProductVersion
            ?? throw new InvalidDataException("Product version is absent.");
        var separator = productVersion.LastIndexOf('+');
        Assert.True(separator >= 0);
        var intradayCommit = productVersion[(separator + 1)..];
        var executionSemanticSha = core.RecomputedSemantics!.ExecutionReports["T2"]
            .SemanticSha256;
        var positionSemanticSha = core.RecomputedSemantics.PositionReports["P2"]
            .SemanticSha256;
        return new(
            fixture.Root,
            output,
            fixture.Expectations.CoreRepositoryCommit,
            Sha('b', 40),
            intradayCommit,
            Sha('c', 40),
            fixture.Expectations.EvidenceSha256,
            fixture.Expectations.ContractFileSha256,
            fixture.Expectations.FinalIndexSha256,
            FileHash(Path.Combine(fixture.Root, "acquisition-manifest.json")),
            core.CoreContractVersion,
            core.DownloaderVersion,
            core.AccountId,
            historical.Universe.SourceSessionId,
            executionSemanticSha,
            positionSemanticSha,
            historical.Universe.SourceIngestionId,
            historical.Universe.RequiredUniverseSha256,
            core.PositionReportP2Utc,
            99,
            99,
            0,
            executable,
            FileHash(executable),
            $"run-{identity}",
            $"owner-{identity}",
            $"authorization-{identity}");
    }

    private static string ExpectedCode(string mismatch) => mismatch switch
    {
        "final_index" => Code("ARCH7B_CORE_FINAL_INDEX_SHA_", "MIS", "MATCH"),
        "acquisition" => Code("ARCH7B_BRIDGE_ACQUISITION_MANIFEST_SHA_", "MIS", "MATCH"),
        "bracket_contract" => Code("ARCH7B_BRIDGE_BRACKET_CONTRACT_", "MIS", "MATCH"),
        "downloader" => Code("ARCH7B_BRIDGE_DOWNLOADER_VERSION_", "MIS", "MATCH"),
        "account" => Code("ARCH7B_BRIDGE_ACCOUNT_", "MIS", "MATCH"),
        "source_session" => Code("ARCH7B_BRIDGE_SOURCE_SESSION_", "MIS", "MATCH"),
        "p2" => Code("ARCH7B_BRIDGE_POSITION_REPORT_P2_", "MIS", "MATCH"),
        "execution_semantic" => Code("ARCH7B_BRIDGE_EXECUTION_SEMANTIC_SHA_", "MIS", "MATCH"),
        "position_semantic" => Code("ARCH7B_BRIDGE_POSITION_SEMANTIC_SHA_", "MIS", "MATCH"),
        "source_ingestion" => Code("ARCH7B_BRIDGE_SOURCE_INGESTION_", "MIS", "MATCH"),
        "required_universe" => Code("ARCH7B_BRIDGE_REQUIRED_UNIVERSE_SHA_", "MIS", "MATCH"),
        "consumer_binary" => Code("ARCH7B_BRIDGE_CONSUMER_EXECUTABLE_SHA_", "MIS", "MATCH"),
        "normalized_count" or "zero_count" or "unknown_count" =>
            Code("ARCH7B_BRIDGE_EXPECTED_COUNTS_", "MIS", "MATCH"),
        "core_commit" => Code("ARCH7B_CORE_REPOSITORY_COMMIT_", "MIS", "MATCH"),
        "intraday_commit" => Code("ARCH7B_BRIDGE_INTRADAY_COMMIT_", "MIS", "MATCH"),
        "core_tree" => Code("ARCH7B_BRIDGE_CORE_TREE_", "MIS", "MATCH"),
        _ => throw new InvalidOperationException(mismatch)
    };

    private static string Code(params string[] parts) => string.Concat(parts);

    private static string FileHash(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));

    private static string Hash(char value) => new(value, 64);
    private static string Sha(char value, int length) => new(value, length);

    private static string TempRoot(string prefix) => Path.Combine(
        Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
