namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bOneShotRunArtifactPath(
    string ContractVersion,
    string Path,
    string CanonicalFilename,
    string RunId,
    bool Reserved,
    bool FileExpectedToExistNow,
    string EvidenceSha256)
{
    public const string Version = "arch7b_one_shot_run_artifact_path_v1";
    public const string PositionMarketDraftFilename = "position-market-slot-binding-draft.json";
    public const string PositionMarketLineageFilename = "position-market-slot-lineage.json";
    public const string PositionMarketRevisionBindingFilename =
        "position-market-revision-input-binding.json";

    public static Arch7bOneShotRunArtifactPath ReservePositionMarketDraft(
        string runRoot, string runId) =>
        Reserve(runRoot, runId, PositionMarketDraftFilename);

    public static Arch7bOneShotRunArtifactPath ReservePositionMarketLineage(
        string runRoot, string runId) =>
        Reserve(runRoot, runId, PositionMarketLineageFilename);

    public static Arch7bOneShotRunArtifactPath ReservePositionMarketRevisionBinding(
        string runRoot, string runId) =>
        Reserve(runRoot, runId, PositionMarketRevisionBindingFilename);

    public void Validate(string runRoot, string expectedRunId) =>
        Validate(runRoot, expectedRunId, PositionMarketDraftFilename,
            "position_market_draft_output_path");

    public void ValidatePositionMarketLineage(string runRoot, string expectedRunId) =>
        Validate(runRoot, expectedRunId, PositionMarketLineageFilename,
            "position_market_lineage_output_path");

    public void ValidatePositionMarketRevisionBinding(string runRoot, string expectedRunId) =>
        Validate(runRoot, expectedRunId, PositionMarketRevisionBindingFilename,
            "position_market_revision_binding_output_path");

    private static Arch7bOneShotRunArtifactPath Reserve(
        string runRoot, string runId, string canonicalFilename)
    {
        Arch7bOneShotAuthorityLoader.RequireAbsolute(runRoot);
        var absoluteRoot = System.IO.Path.GetFullPath(runRoot);
        var path = System.IO.Path.Combine(absoluteRoot, canonicalFilename);
        Arch7bOneShotAuthorityLoader.RequireInside(absoluteRoot, path);
        if (File.Exists(path))
            throw new Arch7bQualificationException(Arch7bBlockers.RunRootNotEmpty, path);
        var canonical = string.Join('\n', Version, runId, absoluteRoot, path,
            canonicalFilename);
        return new(Version, path, canonicalFilename, runId, true, false,
            Arch7bOneShotContracts.Sha256(canonical));
    }

    private void Validate(string runRoot, string expectedRunId,
        string expectedFilename, string factType)
    {
        Arch7bOneShotAuthorityLoader.RequireAbsolute(runRoot);
        var absoluteRoot = System.IO.Path.GetFullPath(runRoot);
        var expectedPath = System.IO.Path.Combine(absoluteRoot, expectedFilename);
        var canonical = string.Join('\n', Version, expectedRunId, absoluteRoot, expectedPath,
            expectedFilename);
        if (ContractVersion != Version || RunId != expectedRunId || !Reserved ||
            FileExpectedToExistNow || CanonicalFilename != expectedFilename ||
            !System.IO.Path.IsPathFullyQualified(Path) ||
            !string.Equals(System.IO.Path.GetFullPath(Path), expectedPath,
                StringComparison.OrdinalIgnoreCase) ||
            EvidenceSha256 != Arch7bOneShotContracts.Sha256(canonical))
            throw new Arch7bQualificationException(Arch7bV2Blockers.FactInvalid, factType);
        Arch7bOneShotAuthorityLoader.RequireInside(absoluteRoot, Path);
    }
}

public sealed record Arch7bPositionMarketDraftArtifactFact(
    string Path,
    string Sha256,
    string EvidenceSha256,
    Guid SelectedPositionSnapshotId,
    string MarketCaptureSessionId);

public sealed record Arch7bContentAddressedArtifactFact(
    string Path,
    string Sha256,
    string EvidenceSha256);
