using System.Text.Json;
using System.Text.Json.Serialization;

namespace QQ.Production.Intraday.Application;

public sealed record FxBboPolygonManifestOnboardingResult(
    bool ApprovedManifestFound,
    IReadOnlyList<string> CandidateManifestPaths,
    IReadOnlyList<FxBboOfflineResearchQuoteLoadDiagnostic> Diagnostics,
    bool RealDataFilesMayBeOpened,
    string BlockerReason);

public sealed class FxBboPolygonResearchManifestOnboarding
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly FxBboResearchDataAuthorizationValidator validator = new();

    public FxBboResearchDataAuthorizationManifest CreateTemplateManifest()
        => new(
            ManifestVersion: "fx-bbo-research-auth.v1",
            DatasetName: "Polygon FX BBO Research Authorization",
            DatasetVendor: "Polygon",
            DatasetKind: "FxBboOfflineQuotes",
            AuthorizedForResearch: false,
            AuthorizedBy: null,
            AuthorizationTimestampUtc: null,
            AuthorizationExpiresUtc: null,
            Files:
            [
                new(
                    Path: "<absolute path to approved offline file>",
                    Sha256: "<optional but recommended>",
                    Symbol: "<symbol if file-specific>",
                    Format: FxBboResearchFileFormat.Csv,
                    TimestampColumn: "<quote/event timestamp column>",
                    BidColumn: "<bid column>",
                    AskColumn: "<ask column>",
                    SymbolColumn: "<symbol column if applicable>",
                    AvailableAtColumn: "<preferred if available>",
                    ReceivedAtColumn: "<preferred alternative if available>",
                    SequenceIdColumn: "<optional deterministic event/sequence id>",
                    TimeZone: "UTC",
                    TimestampSemantics: "<must be explicit>",
                    AvailabilityMode: FxBboResearchAvailabilityMode.ExplicitAvailableAtColumn,
                    AssumedAvailabilityDelay: null,
                    MaxAllowedReadRows: 100000,
                    Approved: false)
            ]);

    public string CreateTemplateJson()
        => JsonSerializer.Serialize(CreateTemplateManifest(), JsonOptions);

    public FxBboPolygonManifestOnboardingResult ValidateManifestCandidates(
        IEnumerable<string> manifestPaths,
        DateTimeOffset validationTimestampUtc)
    {
        ArgumentNullException.ThrowIfNull(manifestPaths);

        var candidatePaths = new List<string>();
        var diagnostics = new List<FxBboOfflineResearchQuoteLoadDiagnostic>();
        var approvedManifestFound = false;

        foreach (var manifestPath in manifestPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
            {
                continue;
            }

            FxBboResearchDataAuthorizationManifest? manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<FxBboResearchDataAuthorizationManifest>(
                    File.ReadAllText(manifestPath),
                    JsonOptions);
            }
            catch (JsonException exception)
            {
                diagnostics.Add(new(manifestPath, null, FxBboOfflineResearchQuoteLoadRejectReason.ManifestParseFailed, exception.Message));
                continue;
            }

            if (!IsPolygonFxBboManifest(manifest))
            {
                continue;
            }

            candidatePaths.Add(manifestPath);
            var manifestDiagnostics = validator.ValidateManifest(manifest, validationTimestampUtc);
            diagnostics.AddRange(manifestDiagnostics.Select(x => x with { FilePath = x.FilePath ?? manifestPath }));
            if (manifestDiagnostics.Count == 0 && validator.ValidateForLocalEvaluation(manifest, validationTimestampUtc).CanRun)
            {
                approvedManifestFound = true;
            }
        }

        if (approvedManifestFound)
        {
            return new(true, candidatePaths, diagnostics, true, "Approved Polygon FX BBO research authorization manifest found.");
        }

        var blocker = candidatePaths.Count == 0
            ? "No Polygon FX BBO research authorization manifest was found."
            : "Polygon FX BBO authorization manifests were found, but none passed authorization and availability validation.";

        return new(false, candidatePaths, diagnostics, false, blocker);
    }

    private static bool IsPolygonFxBboManifest(FxBboResearchDataAuthorizationManifest? manifest)
        => manifest is not null &&
           string.Equals(manifest.DatasetVendor, "Polygon", StringComparison.OrdinalIgnoreCase) &&
           string.Equals(manifest.DatasetKind, "FxBboOfflineQuotes", StringComparison.OrdinalIgnoreCase);
}
