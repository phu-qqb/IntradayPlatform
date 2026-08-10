using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bOneShotRunArtifactPathTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),
        "arch7b-run-artifact-path", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Contract_and_filename_are_exact()
    {
        var fact = Reserve(root, "run-a");

        Assert.Equal("arch7b_one_shot_run_artifact_path_v1", fact.ContractVersion);
        Assert.Equal("position-market-slot-binding-draft.json", fact.CanonicalFilename);
        Assert.True(fact.Reserved);
        Assert.False(fact.FileExpectedToExistNow);
    }

    [Fact]
    public void Reservation_produces_absolute_path_inside_run_root()
    {
        var fact = Reserve(root, "run-a");

        Assert.True(Path.IsPathFullyQualified(fact.Path));
        Assert.Equal(Path.GetFullPath(root), Path.GetDirectoryName(fact.Path));
    }

    [Fact]
    public void Reservation_uses_exact_canonical_filename()
    {
        var fact = Reserve(root, "run-a");

        Assert.Equal(Arch7bOneShotRunArtifactPath.PositionMarketDraftFilename,
            Path.GetFileName(fact.Path));
    }

    [Fact]
    public void Reservation_does_not_create_draft_file()
    {
        var fact = Reserve(root, "run-a");

        Assert.False(File.Exists(fact.Path));
    }

    [Fact]
    public void Distinct_run_roots_produce_distinct_paths()
    {
        var first = Reserve(Path.Combine(root, "first"), "run-a");
        var second = Reserve(Path.Combine(root, "second"), "run-a");

        Assert.NotEqual(first.Path, second.Path);
    }

    [Fact]
    public void Same_run_root_and_run_id_are_deterministic()
    {
        var first = Reserve(root, "run-a");
        var second = Reserve(root, "run-a");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Run_root_alone_is_rejected_as_substitution()
    {
        var fact = Reserve(root, "run-a") with { Path = Path.GetFullPath(root) };

        Assert.Throws<Arch7bQualificationException>(() => fact.Validate(root, "run-a"));
    }

    [Fact]
    public void Relative_path_is_rejected()
    {
        var fact = Reserve(root, "run-a") with
        {
            Path = Arch7bOneShotRunArtifactPath.PositionMarketDraftFilename
        };

        Assert.Throws<Arch7bQualificationException>(() => fact.Validate(root, "run-a"));
    }

    [Fact]
    public void Path_outside_run_root_is_rejected()
    {
        var outside = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"),
            Arch7bOneShotRunArtifactPath.PositionMarketDraftFilename);
        var fact = Reserve(root, "run-a") with { Path = outside };

        Assert.Throws<Arch7bQualificationException>(() => fact.Validate(root, "run-a"));
    }

    [Fact]
    public void Different_run_id_is_rejected()
    {
        var fact = Reserve(root, "run-a");

        Assert.Throws<Arch7bQualificationException>(() => fact.Validate(root, "run-b"));
    }

    [Fact]
    public void Preexisting_draft_is_rejected_at_reservation()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root,
            Arch7bOneShotRunArtifactPath.PositionMarketDraftFilename), "{}");

        Assert.Throws<Arch7bQualificationException>(() => Reserve(root, "run-a"));
    }

    [Fact]
    public void Evidence_hash_binds_root_path_filename_and_run_id()
    {
        var first = Reserve(root, "run-a");
        var differentRun = Reserve(root, "run-b");
        var differentRoot = Reserve(Path.Combine(root, "other"), "run-a");

        Assert.NotEqual(first.EvidenceSha256, differentRun.EvidenceSha256);
        Assert.NotEqual(first.EvidenceSha256, differentRoot.EvidenceSha256);
    }

    private static Arch7bOneShotRunArtifactPath Reserve(string path, string runId)
    {
        Directory.CreateDirectory(path);
        return Arch7bOneShotRunArtifactPath.ReservePositionMarketDraft(path, runId);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
