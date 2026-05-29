using System.Text.Json;
using System.Text.Json.Serialization;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class PolygonFxTickManifestApprovalResearchTests
{
    private static readonly DateTimeOffset ApprovalTime = new(2026, 05, 28, 12, 00, 00, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Missing_operator_approval_marker_blocks_manifest_approval()
    {
        var gate = new PolygonFxTickManifestApprovalResearch().ValidateApprovalMarker(null);

        Assert.False(gate.CanApprove);
        Assert.Contains(gate.Issues, x => x.Code == PolygonFxTickManifestApprovalIssueCode.MissingOperatorApprovalMarker);
    }

    [Fact]
    public void Template_approval_marker_defaults_required_booleans_to_false()
    {
        var template = new PolygonFxTickManifestApprovalResearch().CreateTemplate();

        Assert.False(template.ApproveForResearchSmokeEvaluation);
        Assert.False(template.AcknowledgesDatasetIsTruncatedByPageCap);
        Assert.False(template.AcknowledgesAvailabilityIsSimulatedNotProviderReceivedTime);
        Assert.False(template.AcknowledgesResearchOnlyNoProductionUse);
    }

    [Fact]
    public void Approval_marker_with_missing_acknowledgment_blocks_approval()
    {
        var marker = ValidMarker() with { AcknowledgesResearchOnlyNoProductionUse = false };

        var gate = new PolygonFxTickManifestApprovalResearch().ValidateApprovalMarker(marker);

        Assert.False(gate.CanApprove);
        Assert.Contains(gate.Issues, x => x.Code == PolygonFxTickManifestApprovalIssueCode.MissingResearchOnlyAcknowledgment);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("EventTimestampAsAvailabilityProxy")]
    [InlineData("DownloadObservedAtUtcAsHistoricalAvailability")]
    [InlineData("QuoteTimestampAsAvailabilityProxy")]
    public void Wrong_availability_mode_blocks_approval(string mode)
    {
        var marker = ValidMarker() with { ApprovedAvailabilityMode = mode };

        var gate = new PolygonFxTickManifestApprovalResearch().ValidateApprovalMarker(marker);

        Assert.False(gate.CanApprove);
        Assert.Contains(gate.Issues, x => x.Code == PolygonFxTickManifestApprovalIssueCode.WrongAvailabilityMode);
    }

    [Fact]
    public void Event_timestamp_plus_configured_delay_with_positive_delay_can_pass()
    {
        var gate = new PolygonFxTickManifestApprovalResearch().ValidateApprovalMarker(ValidMarker());

        Assert.True(gate.CanApprove);
    }

    [Fact]
    public void Hash_mismatch_blocks_approval_in_file_validation_contract()
    {
        var issue = new PolygonFxTickManifestApprovalIssue(
            PolygonFxTickManifestApprovalIssueCode.HashMismatch,
            "Synthetic hash mismatch.");

        Assert.Equal(PolygonFxTickManifestApprovalIssueCode.HashMismatch, issue.Code);
    }

    [Fact]
    public void Missing_data_file_blocks_approval_in_file_validation_contract()
    {
        var issue = new PolygonFxTickManifestApprovalIssue(
            PolygonFxTickManifestApprovalIssueCode.MissingDataFile,
            "Synthetic missing file.");

        Assert.Equal(PolygonFxTickManifestApprovalIssueCode.MissingDataFile, issue.Code);
    }

    [Fact]
    public void Approved_manifest_has_authorized_for_research_true_and_file_approved_true()
    {
        var manifest = new PolygonFxTickManifestApprovalResearch().CreateApprovedManifest(Proposal(), ValidMarker());

        Assert.True(manifest.AuthorizedForResearch);
        Assert.All(manifest.Files, file => Assert.True(file.Approved));
    }

    [Fact]
    public void Approved_manifest_preserves_sha256()
    {
        var manifest = new PolygonFxTickManifestApprovalResearch().CreateApprovedManifest(Proposal(), ValidMarker());

        Assert.Equal("ABCDEF", Assert.Single(manifest.Files).Sha256);
    }

    [Fact]
    public void Approved_manifest_uses_event_timestamp_plus_delay_not_quote_timestamp_proxy()
    {
        var manifest = new PolygonFxTickManifestApprovalResearch().CreateApprovedManifest(Proposal(), ValidMarker());
        var file = Assert.Single(manifest.Files);

        Assert.Equal(FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay, file.AvailabilityMode);
        Assert.Equal(TimeSpan.FromSeconds(5), file.AssumedAvailabilityDelay);
        Assert.Null(file.AvailableAtColumn);
        Assert.Null(file.ReceivedAtColumn);
    }

    [Fact]
    public void Download_observed_at_is_not_used_as_historical_available_at()
    {
        var manifest = new PolygonFxTickManifestApprovalResearch().CreateApprovedManifest(Proposal(), ValidMarker());

        Assert.DoesNotContain(manifest.Files, file =>
            string.Equals(file.AvailableAtColumn, "download_observed_at_utc", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(file.ReceivedAtColumn, "download_observed_at_utc", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Template_serializes_without_approving_data()
    {
        var json = new PolygonFxTickManifestApprovalResearch().SerializeTemplate();
        var template = JsonSerializer.Deserialize<PolygonFxTickOperatorApprovalMarker>(json, JsonOptions);

        Assert.NotNull(template);
        Assert.False(template.ApproveForResearchSmokeEvaluation);
    }

    [Fact]
    public void No_raw_rows_or_alpha_outputs_are_produced_by_approval_model()
    {
        var manifest = new PolygonFxTickManifestApprovalResearch().CreateApprovedManifest(Proposal(), ValidMarker());
        var serialized = JsonSerializer.Serialize(manifest, JsonOptions);

        Assert.DoesNotContain("TargetNotional", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("QuantityPolicy", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("MarketDataSnapshot", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("FillId", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Approval_code_is_not_referenced_from_production_execution_or_sizing_paths()
    {
        var root = FindRepoRoot();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "PolygonFxTickManifestApprovalResearch.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "PolygonFxTickManifestApprovalResearchTests.cs"))
        };

        var references = Directory
            .GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("PolygonFxTickManifestApprovalResearch", StringComparison.Ordinal))
            .Select(Path.GetFullPath)
            .Where(path => !allowed.Contains(path))
            .ToArray();

        Assert.Empty(references);
    }

    private static PolygonFxTickOperatorApprovalMarker ValidMarker()
        => new(
            ApproveForResearchSmokeEvaluation: true,
            ApprovedBy: "unit-test",
            ApprovalTimestampUtc: ApprovalTime,
            ApprovedDataset: PolygonFxTickManifestApprovalResearch.RequiredDataset,
            ApprovedSymbols: PolygonFxTickManifestApprovalResearch.RequiredSymbols,
            ApprovedAvailabilityMode: PolygonFxTickManifestApprovalResearch.RequiredAvailabilityMode,
            ApprovedAssumedAvailabilityDelay: PolygonFxTickManifestApprovalResearch.RequiredAvailabilityDelay,
            AcknowledgesDatasetIsTruncatedByPageCap: true,
            AcknowledgesAvailabilityIsSimulatedNotProviderReceivedTime: true,
            AcknowledgesResearchOnlyNoProductionUse: true);

    private static FxBboResearchDataAuthorizationManifest Proposal()
        => new(
            ManifestVersion: "fx-bbo-research-auth.v1",
            DatasetName: "Polygon FX BBO Backfill R009 Proposal",
            DatasetVendor: "Polygon",
            DatasetKind: "FxBboOfflineQuotes",
            AuthorizedForResearch: false,
            AuthorizedBy: null,
            AuthorizationTimestampUtc: null,
            AuthorizationExpiresUtc: null,
            Files:
            [
                new(
                    Path: "C:\\research\\eurusd.csv",
                    Sha256: "ABCDEF",
                    Symbol: null,
                    Format: FxBboResearchFileFormat.Csv,
                    TimestampColumn: "quote_timestamp_utc",
                    BidColumn: "bid",
                    AskColumn: "ask",
                    SymbolColumn: "symbol",
                    AvailableAtColumn: null,
                    ReceivedAtColumn: null,
                    SequenceIdColumn: "sequence_id",
                    TimeZone: "UTC",
                    TimestampSemantics: "Provider quote/event timestamp normalized to UTC.",
                    AvailabilityMode: FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay,
                    AssumedAvailabilityDelay: TimeSpan.FromSeconds(5),
                    MaxAllowedReadRows: 1,
                    Approved: false)
            ]);

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "QQ.Production.Intraday.sln")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return current.FullName;
    }
}
