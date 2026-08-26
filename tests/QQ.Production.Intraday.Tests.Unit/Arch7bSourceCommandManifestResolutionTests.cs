using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bSourceCommandManifestResolutionTests
{
    [Fact]
    public void Repository_fallback_returns_the_manifest_file_not_the_repository_root()
    {
        var path = Arch7bGovernedSourceTemplateMaterializer.SourceCommandManifestPath();

        Assert.Equal("arch7b-position-market-live-command-manifest.json", Path.GetFileName(path));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Packaged_manifest_relative_path_is_stable()
    {
        Assert.Equal("static/arch7b-position-market-live-command-manifest.json",
            Arch7bGovernedSourceTemplateMaterializer.PackagedSourceCommandManifestRelativePath);
    }
}
