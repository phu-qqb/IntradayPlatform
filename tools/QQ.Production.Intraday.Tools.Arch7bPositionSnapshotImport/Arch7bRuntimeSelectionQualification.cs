using System.Security.Cryptography;
using System.Text.Json;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public sealed record Arch7bRuntimeSelectionQualificationRequest(
    string PackageRoot,
    string OutputDirectory,
    string ExpectedAccountId,
    string ExpectedTargetFingerprint,
    string ExpectedSourceSessionId,
    Guid ExpectedSourceIngestionId,
    Guid ExpectedPositionSnapshotId);

public sealed record Arch7bRuntimeSelectionArtifact(
    string Contract,
    string Status,
    Guid SelectedPositionSnapshotId,
    string SelectedPositionSnapshotSha256,
    string PackageManifestSha256,
    string AccountId,
    string TargetProfile,
    string TargetFingerprint,
    string SourceSessionId,
    Guid SourceIngestionId,
    int RequiredInstrumentCount,
    int PositionLineCount,
    bool CurrentRunOnly,
    bool NoDatabaseRead,
    bool NoDatabaseWrite,
    bool NoSecretRead,
    bool NoFix,
    bool NoOrder,
    string EvidenceSha256);

public sealed record Arch7bRuntimeSelectionNativeArtifact(
    string Path,
    string Sha256,
    string ArtifactType);

public sealed record Arch7bRuntimeSelectionQualificationResult(
    string Contract,
    string Status,
    Guid SelectedPositionSnapshotId,
    string SelectedPositionSnapshotSha256,
    string AccountId,
    string TargetFingerprint,
    string SourceSessionId,
    Guid SourceIngestionId,
    int PositionLineCount,
    IReadOnlyList<Arch7bRuntimeSelectionNativeArtifact> Artifacts,
    bool NoDatabaseRead,
    bool NoDatabaseWrite,
    bool NoSecretRead,
    bool NoFix,
    bool NoOrder,
    string EvidenceSha256);

public static class Arch7bRuntimeSelectionQualificationRunner
{
    public const string ContractVersion =
        "arch7b_position_snapshot_runtime_selection_v1";
    public const string SuccessStatus =
        "ARCH7B_RUNTIME_POSITION_SNAPSHOT_SELECTED";
    public const string ArtifactFileName = "runtime-selection.json";

    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        };

    public static Arch7bRuntimeSelectionQualificationResult Run(
        Arch7bRuntimeSelectionQualificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var package = Arch7bPositionImportPackageReader.Read(request.PackageRoot);
        var snapshot = package.Snapshot;
        var universe = package.Universe;
        Require(snapshot.AccountId == request.ExpectedAccountId,
            "ARCH7B_RUNTIME_SELECTION_ACCOUNT_MISMATCH");
        Require(universe.TargetProfile == Arch7bBracketedGlobalFlatContract.TargetProfile &&
                universe.TargetFingerprint == request.ExpectedTargetFingerprint,
            "ARCH7B_RUNTIME_SELECTION_TARGET_MISMATCH");
        Require(universe.SourceSessionId == request.ExpectedSourceSessionId &&
                universe.SourceIngestionId == request.ExpectedSourceIngestionId,
            "ARCH7B_RUNTIME_SELECTION_SOURCE_SESSION_MISMATCH");
        Require(snapshot.PositionSnapshotId == request.ExpectedPositionSnapshotId,
            "ARCH7B_RUNTIME_SELECTION_SNAPSHOT_ID_MISMATCH");
        Require(snapshot.RequiredInstrumentCount == Arch7bPositionImportContract.RequiredLineCount &&
                snapshot.NormalizedLineCount == Arch7bPositionImportContract.RequiredLineCount &&
                snapshot.Lines.Count == Arch7bPositionImportContract.RequiredLineCount &&
                snapshot.Lines.Select(value => value.InstrumentId).Distinct().Count() ==
                Arch7bPositionImportContract.RequiredLineCount,
            "ARCH7B_RUNTIME_SELECTION_99_OF_99_REQUIRED");
        Require(snapshot.Lines.All(value =>
                value.PositionSnapshotId == snapshot.PositionSnapshotId &&
                value.SourceIngestionId == universe.SourceIngestionId &&
                value.PmsSourceSessionId == universe.SourceSessionId &&
                value.AccountId == snapshot.AccountId),
            "ARCH7B_RUNTIME_SELECTION_LINEAGE_MISMATCH");
        var mappingByInstrument = universe.Instruments.ToDictionary(value => value.InstrumentId);
        Require(snapshot.Lines.All(value => mappingByInstrument.TryGetValue(
                    value.InstrumentId, out var mapping) &&
                mapping.SecurityId == value.SecurityId && mapping.Symbol == value.Symbol &&
                mapping.LmaxInstrumentId == value.LmaxInstrumentId),
            "ARCH7B_RUNTIME_SELECTION_UNIVERSE_MISMATCH");

        var positionRow = new PmsShadowPositionSnapshotRow(
            snapshot.PositionSnapshotId,
            universe.SourceIngestionId,
            universe.SourceAccountSnapshotId,
            DateOnly.FromDateTime(snapshot.PositionSnapshotAsOfUtc.UtcDateTime),
            snapshot.PositionSnapshotAsOfUtc,
            snapshot.EvidenceSha256,
            true,
            false,
            true,
            Arch7bBracketedGlobalFlatContract.PositionAuthorityCode);
        var selected = PmsShadowPositionSnapshotForSlotSelection.Select(
            [positionRow], snapshot.PositionSnapshotAsOfUtc);
        Require(selected.PositionSnapshotId == request.ExpectedPositionSnapshotId,
            "ARCH7B_RUNTIME_SELECTION_SELECTED_ID_MISMATCH");

        var evidence = Sha256(string.Join('\n', ContractVersion, SuccessStatus,
            selected.PositionSnapshotId.ToString("D"), selected.SnapshotSha256,
            package.ManifestSha256, snapshot.AccountId, universe.TargetProfile,
            universe.TargetFingerprint, universe.SourceSessionId,
            universe.SourceIngestionId.ToString("D"), snapshot.RequiredInstrumentCount,
            snapshot.Lines.Count, true, true, true, true, true, true));
        var artifact = new Arch7bRuntimeSelectionArtifact(
            ContractVersion, SuccessStatus, selected.PositionSnapshotId,
            selected.SnapshotSha256, package.ManifestSha256, snapshot.AccountId,
            universe.TargetProfile, universe.TargetFingerprint, universe.SourceSessionId,
            universe.SourceIngestionId, snapshot.RequiredInstrumentCount,
            snapshot.Lines.Count, true, true, true, true, true, true, evidence);
        var outputDirectory = Path.GetFullPath(request.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, ArtifactFileName);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(artifact, Json);
        using (var stream = new FileStream(outputPath, FileMode.CreateNew,
                   FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            stream.Write(bytes);
            stream.Flush(true);
        }
        var outputSha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new(ContractVersion, SuccessStatus, selected.PositionSnapshotId,
            selected.SnapshotSha256, snapshot.AccountId, universe.TargetFingerprint,
            universe.SourceSessionId, universe.SourceIngestionId, snapshot.Lines.Count,
            [new(outputPath, outputSha, "runtime-selection")], true, true, true,
            true, true, evidence);
    }

    private static string Sha256(string value) => Convert.ToHexStringLower(
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
