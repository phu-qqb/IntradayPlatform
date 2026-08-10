using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bFinalStageExecutionClassificationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),
        "arch7b-final-stage-classification", Guid.NewGuid().ToString("N"));

    [Fact]
    public void All_40_stages_are_classified_and_command_count_is_derived()
    {
        var document = Arch7bFinalStageExecutionCatalog.Document();

        Assert.Equal(Arch7bStages.All.Count, document.StageCount);
        Assert.Equal(Arch7bStages.All, document.Stages.Select(value => value.StageId));
        Assert.Equal(document.Stages.Count(value => value.HasCommandTemplate),
            document.CommandTemplateCount);
        Assert.Equal(13, document.CommandTemplateCount);
        Assert.All(document.Stages.Where(value => value.HasCommandTemplate), value =>
        {
            Assert.False(string.IsNullOrWhiteSpace(value.CommandId));
            Assert.False(string.IsNullOrWhiteSpace(value.Repository));
            Assert.False(string.IsNullOrWhiteSpace(value.ExecutableOrModule));
            Assert.False(string.IsNullOrWhiteSpace(value.Mode));
            Assert.False(string.IsNullOrWhiteSpace(value.NativeContract));
            Assert.False(string.IsNullOrWhiteSpace(value.AdapterId));
        });
        Assert.All(document.Stages.Where(value => value.ExecutionClass is
            Arch7bFinalStageExecutionClasses.Internal or
            Arch7bFinalStageExecutionClasses.FilesystemGate or
            Arch7bFinalStageExecutionClasses.ExpectedBlockerGate), value =>
        {
            Assert.False(value.HasCommandTemplate);
            Assert.Null(value.CommandId);
            Assert.Null(value.AdapterId);
        });
    }

    [Fact]
    public void Four_reclassified_stages_have_the_exact_execution_routes()
    {
        var core = Arch7bFinalStageExecutionCatalog.Require("CORE_PREQUALIFICATION");
        Assert.Equal(Arch7bFinalStageExecutionClasses.CoreChild, core.ExecutionClass);
        Assert.Equal(Arch7bCoreRuntimePrequalificationAdapter.NativeContract,
            core.NativeContract);
        Assert.Equal("core-prequalification-v1", core.AdapterId);

        var portal = Arch7bFinalStageExecutionCatalog.Require("PORTAL_SESSION_PROVEN");
        Assert.Equal("prove-portal-session", portal.Mode);
        Assert.Equal(Arch7bPortalSessionProofAdapter.NativeContract,
            portal.NativeContract);

        var selection = Arch7bFinalStageExecutionCatalog.Require("RUNTIME_SELECTION");
        Assert.Equal("qualify-runtime-selection", selection.Mode);
        Assert.Equal(Arch7bRuntimeSelectionAdapter.NativeContract,
            selection.NativeContract);

        var final = Arch7bFinalStageExecutionCatalog.Require(
            "FINAL_WORKING_ORDER_PREFLIGHT");
        Assert.Equal(Arch7bFinalStageExecutionClasses.ExpectedBlockerGate,
            final.ExecutionClass);
        Assert.Equal(Arch7bExecutionKind.ExpectedBlockerGate, final.ExecutionKind);
        Assert.False(final.HasCommandTemplate);
        Assert.Null(final.CommandId);
        Assert.Null(final.AdapterId);
    }

    [Fact]
    public async Task Versioned_classification_is_byte_deterministic()
    {
        var first = Path.Combine(root, "first", Arch7bFinalStageExecutionCatalog.FileName);
        var second = Path.Combine(root, "second", Arch7bFinalStageExecutionCatalog.FileName);
        var firstSha = await Arch7bFinalStageExecutionCatalog.WriteAsync(first);
        var secondSha = await Arch7bFinalStageExecutionCatalog.WriteAsync(second);

        Assert.Equal(firstSha, secondSha);
        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(Path.Combine(
            RepositoryRoot(), "docs", "architecture", "arch7b",
            Arch7bFinalStageExecutionCatalog.FileName)));
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(
                   current.FullName, "QQ.Production.Intraday.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
