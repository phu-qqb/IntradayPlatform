using QQ.Production.Intraday.Application;
using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class CanonicalMarketDataGoldenSourceTests
{
    [Fact]
    public void Snapshot_contract_validates_required_symbols_source_artifacts_and_boundaries()
    {
        var validation = CanonicalMarketDataSnapshotValidator.Validate(CreateSnapshotContract());

        Assert.True(validation.IsValid);
        Assert.Empty(validation.Issues);
    }

    [Fact]
    public void Source_selection_cannot_choose_output_weights_as_marketdata_source()
    {
        var selection = new CanonicalMarketDataSourceSelector().Select(
        [
            new CanonicalMarketDataSourceCandidate(
                "weights",
                "OUTPUT_WEIGHTS_ONLY",
                ContainsMarketPrices: false,
                ContainsWeights: true,
                InstrumentMetadataOnly: false,
                FixtureOrPrototypeOnly: false,
                SandboxOnly: true,
                ProductionReady: false,
                Symbols: ["AUDUSD", "EURUSD", "GBPUSD"])
        ]);

        Assert.NotEqual("TRUE_GOLDEN_SOURCE_SELECTED", selection.Classification);
        Assert.Null(selection.SelectedCandidateId);
    }

    [Fact]
    public void Source_selection_cannot_choose_instrument_metadata_as_price_source()
    {
        var selection = new CanonicalMarketDataSourceSelector().Select(
        [
            new CanonicalMarketDataSourceCandidate(
                "lmax-instruments",
                "INSTRUMENT_METADATA_ONLY",
                ContainsMarketPrices: false,
                ContainsWeights: false,
                InstrumentMetadataOnly: true,
                FixtureOrPrototypeOnly: false,
                SandboxOnly: true,
                ProductionReady: false,
                Symbols: ["AUDUSD", "EURUSD", "GBPUSD"])
        ]);

        Assert.NotEqual("TRUE_GOLDEN_SOURCE_SELECTED", selection.Classification);
        Assert.Null(selection.SelectedCandidateId);
    }

    [Fact]
    public void Fixture_or_prototype_data_cannot_be_promoted_to_production()
    {
        var selection = new CanonicalMarketDataSourceSelector().Select(
        [
            new CanonicalMarketDataSourceCandidate(
                "prototype",
                "FIXTURE_OR_PROTOTYPE_ONLY",
                ContainsMarketPrices: false,
                ContainsWeights: false,
                InstrumentMetadataOnly: false,
                FixtureOrPrototypeOnly: true,
                SandboxOnly: true,
                ProductionReady: false,
                Symbols: ["AUDUSD", "EURUSD", "GBPUSD"])
        ]);

        Assert.Equal("ONLY_FIXTURE_OR_PROTOTYPE_SOURCE_AVAILABLE", selection.Classification);
        Assert.Null(selection.SelectedCandidateId);
    }

    [Fact]
    public void Consumer_binding_requires_marketdata_snapshot_id()
    {
        Assert.False(CanonicalMarketDataConsumerRules.ConsumerBindingHasSnapshotId(null));
        Assert.True(CanonicalMarketDataConsumerRules.ConsumerBindingHasSnapshotId("canonical-marketdata-golden-source-r001:polygon-offline-bbo:20251217T020000Z"));
    }

    [Fact]
    public void Pms_sizing_remains_blocked_if_selected_source_lacks_prices()
    {
        Assert.Equal("BLOCKED_MISSING_PRICE", CanonicalMarketDataConsumerRules.SizingPriceBasisStatus(false, false));
        Assert.Equal("READY_WITH_WARNINGS", CanonicalMarketDataConsumerRules.SizingPriceBasisStatus(true, true));
    }

    [Fact]
    public void Db_is_projection_layer_when_backed_by_source_manifest()
    {
        Assert.Equal("DB_PROJECTION_LAYER_OVER_GOLDEN_SOURCE", CanonicalMarketDataConsumerRules.DbRoleStatus(true));
        Assert.Equal("DB_UNAVAILABLE_BUT_NOT_BLOCKING_GOLDEN_SOURCE", CanonicalMarketDataConsumerRules.DbRoleStatus(false));
    }

    [Fact]
    public void Sandbox_selection_does_not_claim_accounting_or_production_readiness()
    {
        var selection = new CanonicalMarketDataSourceSelector().Select(
        [
            new CanonicalMarketDataSourceCandidate(
                "polygon-offline",
                "TRUE_MARKETDATA_SOURCE_CANDIDATE",
                ContainsMarketPrices: true,
                ContainsWeights: false,
                InstrumentMetadataOnly: false,
                FixtureOrPrototypeOnly: false,
                SandboxOnly: true,
                ProductionReady: false,
                Symbols: ["AUDUSD", "EURUSD", "GBPUSD"])
        ]);

        Assert.Equal("SANDBOX_GOLDEN_SOURCE_SELECTED_WITH_WARNINGS", selection.Classification);
        Assert.Equal("polygon-offline", selection.SelectedCandidateId);
        Assert.False(selection.Warnings.Count == 0);
    }

    [Fact]
    public void R001_artifacts_do_not_promote_weights_metadata_accounting_or_production()
    {
        using var inventory = ReadR001Artifact("phase-canonical-marketdata-golden-source-r001-candidate-inventory.json");
        using var selection = ReadR001Artifact("phase-canonical-marketdata-golden-source-r001-source-selection.json");
        using var usage = ReadR001Artifact("phase-canonical-marketdata-golden-source-r001-usage-policy.json");
        using var contracts = ReadR001Artifact("phase-canonical-marketdata-golden-source-r001-contract-status-update.json");

        var candidates = inventory.RootElement.GetProperty("candidates").EnumerateArray().ToArray();
        Assert.NotEmpty(candidates);

        var selectedCandidateId = selection.RootElement.GetProperty("selectedCandidateId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(selectedCandidateId));

        var selectedCandidate = candidates.Single(candidate =>
            candidate.GetProperty("candidateId").GetString() == selectedCandidateId);

        Assert.True(selectedCandidate.GetProperty("containsMarketPrices").GetBoolean());
        Assert.False(selectedCandidate.GetProperty("containsWeights").GetBoolean());
        Assert.NotEqual("OUTPUT_WEIGHTS_ONLY", selectedCandidate.GetProperty("classification").GetString());
        Assert.NotEqual("INSTRUMENT_METADATA_ONLY", selectedCandidate.GetProperty("classification").GetString());

        Assert.False(usage.RootElement.GetProperty("canUseForNetPnl").GetBoolean());
        Assert.False(usage.RootElement.GetProperty("canUseForAccountingPnl").GetBoolean());
        Assert.False(usage.RootElement.GetProperty("canUseForLedgerCommit").GetBoolean());
        Assert.False(usage.RootElement.GetProperty("canUseForProductionLive").GetBoolean());

        var contractStatuses = contracts.RootElement.GetProperty("contractStatuses").EnumerateArray()
            .ToDictionary(
                entry => entry.GetProperty("contract").GetString()!,
                entry => entry.GetProperty("status").GetString()!);

        Assert.Equal("BLOCKED", contractStatuses["accounting-attribution.v1"]);
        Assert.Equal("BLOCKED", contractStatuses["production-readiness.v1"]);
    }

    [Fact]
    public void R001_coverage_is_explicit_for_required_symbols_and_uses_nearest_before_close()
    {
        using var coverage = ReadR001Artifact("phase-canonical-marketdata-golden-source-r001-coverage-evidence.json");

        var entries = coverage.RootElement.GetProperty("coverage").EnumerateArray()
            .ToDictionary(
                entry => entry.GetProperty("symbol").GetString()!,
                entry => entry);

        foreach (var symbol in new[] { "AUDUSD", "EURUSD", "GBPUSD" })
        {
            Assert.True(entries.ContainsKey(symbol));
            Assert.Equal("NEAREST_BEFORE_CLOSE_PRICE_PRESENT", entries[symbol].GetProperty("classification").GetString());
            Assert.Equal(0, entries[symbol].GetProperty("exactCloseRows").GetInt32());
            Assert.True(entries[symbol].GetProperty("rowCountInWindow").GetInt32() > 0);
        }
    }

    private static JsonDocument ReadR001Artifact(string fileName)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "artifacts",
            "readiness",
            "canonical-marketdata-golden-source-r001",
            fileName);

        return JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(path)));
    }

    private static CanonicalMarketDataSnapshotContract CreateSnapshotContract()
    {
        return new CanonicalMarketDataSnapshotContract(
            MarketDataSnapshotId: "canonical-marketdata-golden-source-r001:polygon-offline-bbo:20251217T020000Z:AUDUSD-EURUSD-GBPUSD",
            SnapshotType: "SANDBOX_STATIC_MARKETDATA_SNAPSHOT",
            SnapshotScope: "SandboxResearchPreview",
            SandboxOnly: true,
            NotProduction: true,
            NotAccounting: true,
            CanonicalTargetCloseUtc: new DateTimeOffset(2025, 12, 17, 2, 0, 0, TimeSpan.Zero),
            WindowStartUtc: new DateTimeOffset(2025, 12, 17, 1, 47, 0, TimeSpan.Zero),
            WindowEndUtc: new DateTimeOffset(2025, 12, 17, 2, 0, 0, TimeSpan.Zero),
            Symbols: ["AUDUSD", "EURUSD", "GBPUSD"],
            ContainsMarketPrices: true,
            ContainsQuotes: true,
            ContainsMarks: false,
            SourceArtifacts:
            [
                "data/offline-quotes/polygon/incoming/audusd-20251216191500-20251217020000.ndjson",
                "data/offline-quotes/polygon/incoming/eurusd-20251216191500-20251217020000.ndjson",
                "data/offline-quotes/polygon/incoming/gbpusd-20251216191500-20251217020000.ndjson"
            ]);
    }
}
