using System.Security.Cryptography;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch5c1CanonicalInputBundleTests
{
    [Fact]
    public void Materializer_builds_four_time_aligned_canonical_previews_without_orders()
    {
        using var fixture = new PriceMatrixFixture();
        var materialized = new Arch5c1CanonicalInputMaterializer().Materialize(ValidContract(), fixture.Root);
        var preview = new Arch5bQubesLineagePreviewService().Build(
            materialized.BoundContract,
            materialized.PreviewInputsByStrategy);

        Assert.True(Arch5c1CanonicalInputBundleValidator.Validate(materialized.Bundle).IsValid);
        Assert.Equal(4, materialized.Bundle.Runs.Count);
        Assert.All(materialized.Bundle.Runs, run =>
        {
            Assert.Equal(run.TargetCloseUtc, run.WeightsAsOfUtc);
            Assert.Equal(run.TargetCloseUtc, run.SnapshotAsOfUtc);
            Assert.Equal(1, run.UniqueSecurityIds);
            Assert.Equal(run.UniqueSecurityIds, run.MappedSecurityIds);
            Assert.True(run.PositionSnapshotExplicit);
            Assert.True(run.WorkingLeavesSnapshotExplicit);
            Assert.True(run.EmptyStateWasExplicitlyDeclared);
            Assert.False(run.EmptyStateWasInferred);
            Assert.False(run.BrokerAuthority);
        });
        Assert.Equal(4, preview.Runs.Count);
        Assert.All(preview.Runs, run =>
        {
            Assert.Equal(Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW, run.TargetPositions.ComputationStatus);
            Assert.Single(run.TargetPositions.Positions);
            Assert.Equal(Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW, run.DriftSnapshot.ComputationStatus);
            Assert.Single(run.DriftSnapshot.Drifts);
            Assert.False(run.DriftSnapshot.ProducedTradeIntent);
            Assert.False(run.DriftSnapshot.ProducedExecutableQuantity);
            Assert.Equal("CompletedNoExternal", run.R009.Status);
            Assert.False(run.R009.ExecutionAllowed);
            Assert.False(run.R009.OrderEntryEnabled);
            Assert.Equal("DISABLED_NO_ORDER_ENTRY", run.R009.BrokerSendStatus);
        });
    }

    [Fact]
    public void Materialization_is_byte_stable_and_registration_is_idempotent()
    {
        using var fixture = new PriceMatrixFixture();
        var materializer = new Arch5c1CanonicalInputMaterializer();
        var first = materializer.Materialize(ValidContract(), fixture.Root);
        var second = materializer.Materialize(ValidContract(), fixture.Root);
        var service = new Arch5bQubesLineagePreviewService();
        var firstPreview = service.Build(first.BoundContract, first.PreviewInputsByStrategy);
        var secondPreview = service.Build(second.BoundContract, second.PreviewInputsByStrategy);
        var registry = new Arch5bLineagePreviewRegistry();

        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(first.Bundle), JsonSerializer.SerializeToUtf8Bytes(second.Bundle));
        Assert.Equal(first.Bundle.BundleSha256, second.Bundle.BundleSha256);
        Assert.Equal(firstPreview.PreviewSha256, secondPreview.PreviewSha256);
        foreach (var run in firstPreview.Runs)
        {
            Assert.Same(run, registry.Register(run));
            Assert.Same(run, registry.Register(run));
        }
    }

    [Fact]
    public void Content_addressed_market_data_and_bundle_ids_change_when_price_changes()
    {
        using var fixture = new PriceMatrixFixture();
        var materializer = new Arch5c1CanonicalInputMaterializer();
        var first = materializer.Materialize(ValidContract(), fixture.Root);
        fixture.Write("INFX10", TargetCloses["INFX10"], 2.5d);
        var second = materializer.Materialize(ValidContract(), fixture.Root);

        Assert.NotEqual(first.Bundle.BundleSha256, second.Bundle.BundleSha256);
        Assert.NotEqual(first.Bundle.MarketDataSnapshotId, second.Bundle.MarketDataSnapshotId);
        Assert.NotEqual(
            first.Bundle.Runs.Single(run => run.StrategyId == "INFX10").MarketDataSnapshotId,
            second.Bundle.Runs.Single(run => run.StrategyId == "INFX10").MarketDataSnapshotId);
    }

    [Theory]
    [InlineData("unknown-version", "UNKNOWN_BUNDLE_CONTRACT_VERSION")]
    [InlineData("real-account", "REAL_OR_UNAPPROVED_ACCOUNT_REJECTED")]
    [InlineData("date-mismatch", "RUN_TEMPORAL_ALIGNMENT_INVALID")]
    [InlineData("partial-market", "MARKET_DATA_COVERAGE_INCOMPLETE")]
    [InlineData("missing-mapping", "INSTRUMENT_MAPPING_COVERAGE_INCOMPLETE")]
    [InlineData("wrong-price-type", "MARKET_DATA_PRICE_TYPE_INVALID")]
    [InlineData("absolute-path", "ABSOLUTE_SOURCE_PATH_REJECTED")]
    [InlineData("missing-position", "POSITION_SNAPSHOT_NOT_EXPLICIT_EMPTY")]
    [InlineData("missing-working-leaves", "WORKING_LEAVES_NOT_EXPLICIT_EMPTY")]
    [InlineData("inferred-working-leaves", "WORKING_LEAVES_NOT_EXPLICIT_EMPTY")]
    [InlineData("broker-authority", "BROKER_AUTHORITY_CLAIM_INVALID")]
    [InlineData("nav-missing", "TRACKED_TEST_ACCOUNT_FIXTURE_INVALID")]
    [InlineData("execution-enabled", "NO_ORDER_BOUNDARY_INVALID")]
    [InlineData("db-apply", "EXTERNAL_OR_AUTHORITATIVE_ACTION_ENABLED")]
    [InlineData("databento-call", "RUNTIME_BOUNDARY_INVALID")]
    [InlineData("snapshot-missing", "TEMPORAL_FIELD_MISSING")]
    [InlineData("weights-missing", "TEMPORAL_FIELD_MISSING")]
    [InlineData("ambiguous-mapping", "INSTRUMENT_MAPPING_COVERAGE_INCOMPLETE")]
    [InlineData("wrong-market-id", "MARKET_DATA_SNAPSHOT_ID_INVALID")]
    [InlineData("price-after-hash", "MARKET_DATA_SNAPSHOT_SHA_INVALID")]
    [InlineData("stale-price", "MARKET_DATA_TIMESTAMP_OR_STALENESS_INVALID")]
    [InlineData("currency-missing", "TRACKED_TEST_ACCOUNT_FIXTURE_INVALID")]
    [InlineData("fx-policy-missing", "FX_CONVERSION_POLICY_INVALID")]
    [InlineData("account-date-mismatch", "RUN_TEMPORAL_ALIGNMENT_INVALID")]
    [InlineData("same-bundle-id-content-diff", "BUNDLE_SHA256_MISMATCH")]
    [InlineData("broker-send", "EXTERNAL_OR_AUTHORITATIVE_ACTION_ENABLED")]
    [InlineData("accountapi-call", "EXTERNAL_OR_AUTHORITATIVE_ACTION_ENABLED")]
    [InlineData("production-mutation", "RUNTIME_BOUNDARY_INVALID")]
    [InlineData("aggregate-market-id", "AGGREGATE_MARKET_DATA_SNAPSHOT_ID_INVALID")]
    [InlineData("market-source-sha", "MARKET_DATA_SOURCE_SHA_INVALID")]
    public void Bundle_validator_fails_closed_for_incomplete_ambiguous_or_external_inputs(string mutation, string issue)
    {
        using var fixture = new PriceMatrixFixture();
        var bundle = new Arch5c1CanonicalInputMaterializer().Materialize(ValidContract(), fixture.Root).Bundle;
        var first = bundle.Runs[0];
        var observation = first.MarketData[0];
        bundle = mutation switch
        {
            "unknown-version" => bundle with { ContractVersion = "unknown" },
            "real-account" => bundle with { AccountId = Arch5bLineageContractVersions.RealAccountId },
            "date-mismatch" => ReplaceFirst(bundle, first with { SnapshotAsOfUtc = first.SnapshotAsOfUtc.AddMinutes(1) }),
            "partial-market" => ReplaceFirst(bundle, first with { MarketData = [] }),
            "missing-mapping" => ReplaceFirst(bundle, first with { MappedSecurityIds = 0, MissingSecurityIds = [observation.SecurityId] }),
            "wrong-price-type" => ReplaceFirst(bundle, first with { MarketData = [observation with { PriceType = "UNKNOWN" }] }),
            "absolute-path" => ReplaceFirst(bundle, first with { MarketData = [observation with { SourceFile = @"D:\forbidden\100.bin" }] }),
            "missing-position" => ReplaceFirst(bundle, first with { PositionSnapshotExplicit = false }),
            "missing-working-leaves" => ReplaceFirst(bundle, first with { WorkingLeavesSnapshotExplicit = false }),
            "inferred-working-leaves" => ReplaceFirst(bundle, first with { EmptyStateWasInferred = true }),
            "broker-authority" => ReplaceFirst(bundle, first with { BrokerAuthority = true }),
            "nav-missing" => ReplaceFirst(bundle, first with { NavUsd = 0 }),
            "execution-enabled" => bundle with { ExecutionAllowed = true },
            "db-apply" => bundle with { DbApply = true },
            "databento-call" => bundle with { DatabentoApiCalls = 1 },
            "snapshot-missing" => bundle with { SnapshotAsOf = default },
            "weights-missing" => bundle with { WeightsAsOf = default },
            "ambiguous-mapping" => ReplaceFirst(bundle, first with { AmbiguousSecurityIds = [observation.SecurityId] }),
            "wrong-market-id" => ReplaceFirst(bundle, first with { MarketDataSnapshotId = Guid.NewGuid().ToString("D") }),
            "price-after-hash" => ReplaceFirst(bundle, first with { MarketData = [observation with { ExactPriceText = "2.5" }] }),
            "stale-price" => ReplaceFirst(bundle, first with { MarketData = [observation with { StalenessMilliseconds = 1 }] }),
            "currency-missing" => ReplaceFirst(bundle, first with { BaseCurrency = string.Empty }),
            "fx-policy-missing" => bundle with { FxConversionPolicy = string.Empty },
            "account-date-mismatch" => ReplaceFirst(bundle, first with { SnapshotAsOfUtc = first.SnapshotAsOfUtc.AddDays(1) }),
            "same-bundle-id-content-diff" => bundle with { ScenarioClassification = "MUTATED_WITH_ORIGINAL_BUNDLE_ID" },
            "broker-send" => bundle with { BrokerSend = true, BrokerSendAttempts = 1 },
            "accountapi-call" => bundle with { AccountApiCalls = 1 },
            "production-mutation" => bundle with { ProductionMutation = true },
            "aggregate-market-id" => bundle with { MarketDataSnapshotId = "arch5c1:market:sha256:" + Sha('9') },
            "market-source-sha" => ReplaceFirst(bundle, first with { MarketData = [observation with { SourceFileSha256 = "invalid" }] }),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };

        var result = Arch5c1CanonicalInputBundleValidator.Validate(bundle);

        Assert.False(result.IsValid);
        Assert.Contains(issue, result.Issues);
    }

    [Fact]
    public void Reader_rejects_partial_universe_missing_price_and_wrong_snapshot_time()
    {
        using var fixture = new PriceMatrixFixture();
        var reader = new Arch5c1QubesPriceMatrixReader();

        Assert.Equal("QUBES_PRICE_MATRIX_COVERAGE_MISMATCH", Assert.Throws<InvalidDataException>(() =>
            reader.Read(fixture.Root, "INFX7", TargetCloses["INFX7"], ["58", "59"])).Message);

        fixture.Write("INFX7", TargetCloses["INFX7"], -999d);
        Assert.Equal("QUBES_PRICE_MATRIX_PRICE_INVALID", Assert.Throws<InvalidDataException>(() =>
            reader.Read(fixture.Root, "INFX7", TargetCloses["INFX7"], ["58"])).Message);

        fixture.Write("INFX7", TargetCloses["INFX7"], 1.25d);
        Assert.Equal("QUBES_PRICE_MATRIX_SNAPSHOT_AS_OF_MISMATCH", Assert.Throws<InvalidDataException>(() =>
            reader.Read(fixture.Root, "INFX7", TargetCloses["INFX7"].AddMinutes(1), ["58"])).Message);
    }

    [Fact]
    public void Per_run_ARCH5B_overload_rejects_missing_strategy_input()
    {
        using var fixture = new PriceMatrixFixture();
        var materialized = new Arch5c1CanonicalInputMaterializer().Materialize(ValidContract(), fixture.Root);
        var partial = materialized.PreviewInputsByStrategy
            .Where(entry => entry.Key != "INFX10")
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

        var error = Assert.Throws<InvalidDataException>(() =>
            new Arch5bQubesLineagePreviewService().Build(materialized.BoundContract, partial));

        Assert.Equal("CANONICAL_RUN_INPUT_SET_MISMATCH", error.Message);
    }

    private static Arch5c1CanonicalTestInputBundle ReplaceFirst(
        Arch5c1CanonicalTestInputBundle bundle,
        Arch5c1CanonicalRunInput first)
        => bundle with { Runs = [first, .. bundle.Runs.Skip(1)] };

    private static readonly IReadOnlyDictionary<string, DateTimeOffset> TargetCloses =
        new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
        {
            ["INFX7"] = new(2026, 6, 11, 19, 36, 0, TimeSpan.Zero),
            ["INFX8"] = new(2026, 6, 11, 19, 6, 0, TimeSpan.Zero),
            ["INFX9"] = new(2026, 6, 11, 12, 36, 0, TimeSpan.Zero),
            ["INFX10"] = new(2026, 6, 11, 11, 6, 0, TimeSpan.Zero)
        };

    private static Arch5bSessionLineageContractV1 ValidContract()
    {
        var benchmarks = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["INFX7"] = 4.5m,
            ["INFX8"] = 2.1m,
            ["INFX9"] = 1.4m,
            ["INFX10"] = 0.6m
        };
        var runs = benchmarks.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry =>
        {
            var target = TargetCloses[entry.Key];
            var timestamp = target.ToString("yyyyMMddHHmm");
            return new Arch5bRunLineageContractV1(
                Arch5bLineageContractVersions.LineageV1,
                Arch5bLineageContractVersions.SourceQubesWeightsOutputV1,
                "arch5c1-test-session",
                "arch5c1-test-run",
                $"arch5c1-test-run:{entry.Key}",
                entry.Key,
                entry.Value,
                new string('a', 40),
                Sha('b'),
                Sha('c'),
                "fixture-v1",
                Sha('d'),
                Sha(entry.Key[^1]),
                1,
                $"outputs/{entry.Key}/AggregatedWeights.txt",
                Arch5bLineageContractVersions.OutputQubesWeightsOutputV1,
                new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero),
                target,
                target,
                timestamp,
                "PRODMANAGERV4_LAST_CHRONOLOGICAL_DATA_ROW",
                "PASS",
                0,
                0,
                true,
                null,
                null,
                Arch5bLineageContractVersions.MissingMarketDataSnapshot,
                Arch5bLineageContractVersions.EvidenceOnlyClassification,
                true,
                false,
                false,
                [new Arch5bTargetCloseWeightV1("58", "0.1", 0.1d, 0, $"{timestamp}:58", Sha('e'))]);
        }).ToArray();
        return new Arch5bSessionLineageContractV1(
            Arch5bLineageContractVersions.LineageV1,
            Arch5bLineageContractVersions.SourceQubesWeightsOutputV1,
            "arch5c1-test-session",
            Arch5bLineageContractVersions.TestAccountId,
            Arch5bLineageContractVersions.TestAccountScope,
            new string('a', 40),
            Sha('b'),
            Sha('c'),
            "fixture-v1",
            new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero),
            Arch5bLineageContractVersions.EvidenceOnlyClassification,
            true,
            false,
            false,
            runs);
    }

    private static string Sha(char value) => new(value, 64);

    private sealed class PriceMatrixFixture : IDisposable
    {
        public PriceMatrixFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "arch5c1-" + Guid.NewGuid().ToString("N"));
            foreach (var strategy in TargetCloses.Keys)
            {
                Write(strategy, TargetCloses[strategy], 1.25d);
            }
        }

        public string Root { get; }

        public void Write(string strategy, DateTimeOffset timestamp, double price)
        {
            var specs = Directory.CreateDirectory(Path.Combine(Root, strategy, "specs"));
            var data = Directory.CreateDirectory(Path.Combine(Root, strategy, "data"));
            File.WriteAllText(Path.Combine(specs.FullName, "ticker.txt"), "58" + Environment.NewLine);
            using var stream = File.Create(Path.Combine(data.FullName, "100.bin"));
            using var writer = new BinaryWriter(stream);
            writer.Write(1);
            writer.Write(1);
            writer.Write(long.Parse(timestamp.ToString("yyyyMMddHHmm")));
            writer.Write(price);
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
