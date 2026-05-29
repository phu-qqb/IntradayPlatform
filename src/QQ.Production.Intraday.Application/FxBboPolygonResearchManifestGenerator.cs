using System.Text.Json;
using System.Text.Json.Serialization;

namespace QQ.Production.Intraday.Application;

public enum FxBboPolygonResearchManifestGeneratorIssueCode
{
    WarningSha256NotPinned,
    MissingDatasetName,
    MissingFilePath,
    MissingTimestampColumn,
    MissingBidColumn,
    MissingAskColumn,
    MissingSymbolMapping,
    MissingTimestampSemantics,
    MissingAvailableAtColumn,
    MissingReceivedAtColumn,
    NonPositiveAvailabilityDelay,
    EventTimestampAsAvailabilityProxyRejected
}

public sealed record FxBboPolygonResearchManifestGeneratorFileRequest(
    string Path,
    FxBboResearchFileFormat Format,
    string TimestampColumn,
    string BidColumn,
    string AskColumn,
    string? Symbol = null,
    string? SymbolColumn = null,
    string? AvailableAtColumn = null,
    string? ReceivedAtColumn = null,
    string? SequenceIdColumn = null,
    string TimeZone = "UTC",
    string TimestampSemantics = "",
    FxBboResearchAvailabilityMode AvailabilityMode = FxBboResearchAvailabilityMode.Unknown,
    TimeSpan? AssumedAvailabilityDelay = null,
    int? MaxAllowedReadRows = 100000,
    string? Sha256 = null);

public sealed record FxBboPolygonResearchManifestGeneratorRequest(
    string DatasetName,
    IReadOnlyList<FxBboPolygonResearchManifestGeneratorFileRequest> Files,
    string DatasetVendor = "Polygon",
    string DatasetKind = "FxBboOfflineQuotes",
    string ManifestVersion = "fx-bbo-research-auth.v1");

public sealed record FxBboPolygonResearchManifestGeneratorIssue(
    FxBboPolygonResearchManifestGeneratorIssueCode Code,
    string Message,
    string? FilePath = null,
    bool IsBlocking = true);

public sealed record FxBboPolygonResearchManifestGeneratorResult(
    FxBboResearchDataAuthorizationManifest? Manifest,
    string? ManifestJson,
    IReadOnlyList<FxBboPolygonResearchManifestGeneratorIssue> Issues)
{
    public bool IsSuccess => Manifest is not null && Issues.All(x => !x.IsBlocking);
}

public sealed class FxBboPolygonResearchManifestGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FxBboPolygonResearchManifestGeneratorResult Generate(FxBboPolygonResearchManifestGeneratorRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var issues = Validate(request).ToList();
        if (issues.Any(x => x.IsBlocking))
        {
            return new(null, null, issues);
        }

        var manifest = new FxBboResearchDataAuthorizationManifest(
            ManifestVersion: request.ManifestVersion,
            DatasetName: request.DatasetName,
            DatasetVendor: string.IsNullOrWhiteSpace(request.DatasetVendor) ? "Polygon" : request.DatasetVendor.Trim(),
            DatasetKind: string.IsNullOrWhiteSpace(request.DatasetKind) ? "FxBboOfflineQuotes" : request.DatasetKind.Trim(),
            AuthorizedForResearch: false,
            AuthorizedBy: null,
            AuthorizationTimestampUtc: null,
            AuthorizationExpiresUtc: null,
            Files: request.Files.Select(file => new FxBboResearchAuthorizedFileEntry(
                Path: file.Path,
                Sha256: string.IsNullOrWhiteSpace(file.Sha256) ? null : file.Sha256,
                Symbol: BlankToNull(file.Symbol),
                Format: file.Format,
                TimestampColumn: file.TimestampColumn,
                BidColumn: file.BidColumn,
                AskColumn: file.AskColumn,
                SymbolColumn: BlankToNull(file.SymbolColumn),
                AvailableAtColumn: BlankToNull(file.AvailableAtColumn),
                ReceivedAtColumn: BlankToNull(file.ReceivedAtColumn),
                SequenceIdColumn: BlankToNull(file.SequenceIdColumn),
                TimeZone: string.IsNullOrWhiteSpace(file.TimeZone) ? "UTC" : file.TimeZone,
                TimestampSemantics: file.TimestampSemantics,
                AvailabilityMode: file.AvailabilityMode,
                AssumedAvailabilityDelay: file.AssumedAvailabilityDelay,
                MaxAllowedReadRows: file.MaxAllowedReadRows,
                Approved: false)).ToArray());

        return new(manifest, JsonSerializer.Serialize(manifest, JsonOptions), issues);
    }

    private static IEnumerable<FxBboPolygonResearchManifestGeneratorIssue> Validate(FxBboPolygonResearchManifestGeneratorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DatasetName))
        {
            yield return Blocking(FxBboPolygonResearchManifestGeneratorIssueCode.MissingDatasetName, "DatasetName is required.");
        }

        foreach (var file in request.Files)
        {
            if (string.IsNullOrWhiteSpace(file.Path))
            {
                yield return Blocking(FxBboPolygonResearchManifestGeneratorIssueCode.MissingFilePath, "File path is required.", file.Path);
            }

            if (string.IsNullOrWhiteSpace(file.TimestampColumn))
            {
                yield return Blocking(FxBboPolygonResearchManifestGeneratorIssueCode.MissingTimestampColumn, "Timestamp column is required.", file.Path);
            }

            if (string.IsNullOrWhiteSpace(file.BidColumn))
            {
                yield return Blocking(FxBboPolygonResearchManifestGeneratorIssueCode.MissingBidColumn, "Bid column is required.", file.Path);
            }

            if (string.IsNullOrWhiteSpace(file.AskColumn))
            {
                yield return Blocking(FxBboPolygonResearchManifestGeneratorIssueCode.MissingAskColumn, "Ask column is required.", file.Path);
            }

            if (string.IsNullOrWhiteSpace(file.Symbol) && string.IsNullOrWhiteSpace(file.SymbolColumn))
            {
                yield return Blocking(FxBboPolygonResearchManifestGeneratorIssueCode.MissingSymbolMapping, "Either Symbol or SymbolColumn is required.", file.Path);
            }

            if (string.IsNullOrWhiteSpace(file.TimestampSemantics))
            {
                yield return Blocking(FxBboPolygonResearchManifestGeneratorIssueCode.MissingTimestampSemantics, "TimestampSemantics must be explicit.", file.Path);
            }

            switch (file.AvailabilityMode)
            {
                case FxBboResearchAvailabilityMode.ExplicitAvailableAtColumn:
                    if (string.IsNullOrWhiteSpace(file.AvailableAtColumn))
                    {
                        yield return Blocking(FxBboPolygonResearchManifestGeneratorIssueCode.MissingAvailableAtColumn, "ExplicitAvailableAtColumn requires AvailableAtColumn.", file.Path);
                    }

                    break;
                case FxBboResearchAvailabilityMode.ExplicitReceivedAtColumn:
                    if (string.IsNullOrWhiteSpace(file.ReceivedAtColumn))
                    {
                        yield return Blocking(FxBboPolygonResearchManifestGeneratorIssueCode.MissingReceivedAtColumn, "ExplicitReceivedAtColumn requires ReceivedAtColumn.", file.Path);
                    }

                    break;
                case FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay:
                    if (file.AssumedAvailabilityDelay is null || file.AssumedAvailabilityDelay.Value <= TimeSpan.Zero)
                    {
                        yield return Blocking(FxBboPolygonResearchManifestGeneratorIssueCode.NonPositiveAvailabilityDelay, "EventTimestampPlusConfiguredDelay requires a positive delay.", file.Path);
                    }

                    break;
                case FxBboResearchAvailabilityMode.EventTimestampAsAvailabilityProxy:
                    yield return Blocking(FxBboPolygonResearchManifestGeneratorIssueCode.EventTimestampAsAvailabilityProxyRejected, "EventTimestampAsAvailabilityProxy is rejected by the generator default.", file.Path);
                    break;
            }

            if (string.IsNullOrWhiteSpace(file.Sha256))
            {
                yield return new(
                    FxBboPolygonResearchManifestGeneratorIssueCode.WarningSha256NotPinned,
                    "Sha256 is not pinned; manifest remains unapproved and should be hash-pinned before approval.",
                    file.Path,
                    IsBlocking: false);
            }
        }
    }

    private static FxBboPolygonResearchManifestGeneratorIssue Blocking(
        FxBboPolygonResearchManifestGeneratorIssueCode code,
        string message,
        string? filePath = null)
        => new(code, message, filePath, IsBlocking: true);

    private static string? BlankToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
