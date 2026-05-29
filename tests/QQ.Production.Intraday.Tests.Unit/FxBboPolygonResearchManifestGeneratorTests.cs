using System.Text.Json;
using System.Text.Json.Serialization;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class FxBboPolygonResearchManifestGeneratorTests
{
    private static readonly DateTimeOffset ValidationTime = new(2026, 01, 05, 12, 00, 00, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Generator_creates_manifest_with_authorized_for_research_false_by_default()
    {
        var result = Generate(SafeFile());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Manifest);
        Assert.False(result.Manifest.AuthorizedForResearch);
        Assert.Null(result.Manifest.AuthorizationTimestampUtc);
        Assert.Null(result.Manifest.AuthorizationExpiresUtc);
    }

    [Fact]
    public void Generator_creates_every_file_entry_with_approved_false_by_default()
    {
        var result = Generate(SafeFile(), SafeFile(path: "C:\\data\\gbpusd.csv", symbol: "GBPUSD"));

        Assert.True(result.IsSuccess);
        Assert.All(result.Manifest!.Files, file => Assert.False(file.Approved));
    }

    [Fact]
    public void Generator_defaults_availability_mode_to_unknown_unless_explicit_safe_mode_is_provided()
    {
        var result = Generate(SafeFile(availabilityMode: FxBboResearchAvailabilityMode.Unknown, availableAtColumn: null));

        Assert.True(result.IsSuccess);
        Assert.Equal(FxBboResearchAvailabilityMode.Unknown, result.Manifest!.Files.Single().AvailabilityMode);
    }

    [Fact]
    public void Event_timestamp_as_availability_proxy_is_rejected_by_default()
    {
        var result = Generate(SafeFile(
            availabilityMode: FxBboResearchAvailabilityMode.EventTimestampAsAvailabilityProxy,
            availableAtColumn: null));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, x => x.Code == FxBboPolygonResearchManifestGeneratorIssueCode.EventTimestampAsAvailabilityProxyRejected && x.IsBlocking);
        Assert.Null(result.Manifest);
    }

    [Fact]
    public void Explicit_available_at_mode_requires_available_at_column()
    {
        var result = Generate(SafeFile(availableAtColumn: null));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, x => x.Code == FxBboPolygonResearchManifestGeneratorIssueCode.MissingAvailableAtColumn);
    }

    [Fact]
    public void Explicit_received_at_mode_requires_received_at_column()
    {
        var result = Generate(SafeFile(
            availabilityMode: FxBboResearchAvailabilityMode.ExplicitReceivedAtColumn,
            availableAtColumn: null,
            receivedAtColumn: null));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, x => x.Code == FxBboPolygonResearchManifestGeneratorIssueCode.MissingReceivedAtColumn);
    }

    [Fact]
    public void Event_timestamp_plus_configured_delay_requires_positive_delay()
    {
        var result = Generate(SafeFile(
            availabilityMode: FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay,
            availableAtColumn: null,
            assumedAvailabilityDelay: TimeSpan.Zero));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, x => x.Code == FxBboPolygonResearchManifestGeneratorIssueCode.NonPositiveAvailabilityDelay);
    }

    [Fact]
    public void Generator_does_not_approve_files_even_when_all_columns_are_supplied()
    {
        var result = Generate(SafeFile(sha256: new string('A', 64)));

        Assert.True(result.IsSuccess);
        Assert.False(result.Manifest!.AuthorizedForResearch);
        Assert.False(result.Manifest.Files.Single().Approved);
    }

    [Fact]
    public void Generated_manifest_parses_with_r003_loader_contract_but_blocks_local_evaluation_until_manual_approval()
    {
        var result = Generate(SafeFile());
        var parsed = JsonSerializer.Deserialize<FxBboResearchDataAuthorizationManifest>(result.ManifestJson!, JsonOptions);

        var validation = new FxBboResearchDataAuthorizationValidator().ValidateForLocalEvaluation(parsed, ValidationTime);

        Assert.NotNull(parsed);
        Assert.False(validation.CanRun);
        Assert.Contains("AuthorizedForResearch is false", validation.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Sha256_omission_is_warning_not_blocking()
    {
        var result = Generate(SafeFile(sha256: null));

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Issues, x => x.Code == FxBboPolygonResearchManifestGeneratorIssueCode.WarningSha256NotPinned && !x.IsBlocking);
    }

    [Fact]
    public void Generator_code_does_not_bind_execution_sizing_or_production_outputs()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxBboPolygonResearchManifestGenerator.cs"));

        Assert.DoesNotContain("MarketDataSnapshot", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetNotional", source, StringComparison.Ordinal);
        Assert.DoesNotContain("QuantityPolicy", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetWeight", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CoreExecution", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CoreNetting", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lmax", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generator_is_not_referenced_from_production_execution_or_sizing_paths()
    {
        var root = FindRepoRoot();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxBboPolygonResearchManifestGenerator.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxBboPolygonResearchManifestGeneratorTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceResearchTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceBboSamplingResearchTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxBboOfflineResearchQuoteLoaderTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceLocalEvaluationR006Tests.cs"))
        };

        var references = Directory
            .GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("FxBboPolygonResearchManifestGenerator", StringComparison.Ordinal))
            .Select(Path.GetFullPath)
            .Where(path => !allowed.Contains(path))
            .ToArray();

        Assert.Empty(references);
    }

    private static FxBboPolygonResearchManifestGeneratorResult Generate(params FxBboPolygonResearchManifestGeneratorFileRequest[] files)
        => new FxBboPolygonResearchManifestGenerator().Generate(new(
            DatasetName: "Polygon FX BBO Generated Scaffold",
            Files: files));

    private static FxBboPolygonResearchManifestGeneratorFileRequest SafeFile(
        string path = "C:\\data\\eurusd.csv",
        string symbol = "EURUSD",
        FxBboResearchAvailabilityMode availabilityMode = FxBboResearchAvailabilityMode.ExplicitAvailableAtColumn,
        string? availableAtColumn = "availableAtUtc",
        string? receivedAtColumn = null,
        TimeSpan? assumedAvailabilityDelay = null,
        string? sha256 = null)
        => new(
            Path: path,
            Format: FxBboResearchFileFormat.Csv,
            TimestampColumn: "timestampUtc",
            BidColumn: "bid",
            AskColumn: "ask",
            Symbol: symbol,
            AvailableAtColumn: availableAtColumn,
            ReceivedAtColumn: receivedAtColumn,
            SequenceIdColumn: "sequenceId",
            TimestampSemantics: "Source quote event timestamp UTC.",
            AvailabilityMode: availabilityMode,
            AssumedAvailabilityDelay: assumedAvailabilityDelay,
            Sha256: sha256);

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
