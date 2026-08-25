using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bCoreR38AuthorityTests
{
    [Fact]
    public void Current_governed_core_and_r38_authority_is_accepted()
    {
        Arch7bCoreR38Authority.Validate(Arch7bOneShotContracts.CoreCommit,
            Arch7bOneShotContracts.CoreTree,
            Arch7bOneShotContracts.CoreRepositoryAuthoritySha256,
            Arch7bOneShotContracts.CoreTrackedInventorySha256);

        Assert.Equal("dd9bbe988b3a086ce1d5adf21c8d6ff5d200f825",
            Arch7bOneShotContracts.CoreCommit);
        Assert.Equal("94c9352797afaa75e23099e0c14dc84597e1951d",
            Arch7bOneShotContracts.CoreTree);
        Assert.Equal("2527ec6517cd1086cc49fd433f862f29ef57ac6a909bd8d07ac447dfd17577dd",
            Arch7bOneShotContracts.CoreR38RuntimeSha256);
        Assert.Equal(2941, Arch7bOneShotContracts.CoreR38RuntimeInventoryCount);
    }

    [Theory]
    [InlineData("core-commit")]
    [InlineData("core-tree")]
    [InlineData("source-provenance")]
    [InlineData("runtime-payload")]
    public void Historical_or_mutated_core_authority_is_rejected(string mutation)
    {
        var commit = Arch7bOneShotContracts.CoreCommit;
        var tree = Arch7bOneShotContracts.CoreTree;
        var repositoryAuthority = Arch7bOneShotContracts.CoreRepositoryAuthoritySha256;
        var inventory = Arch7bOneShotContracts.CoreTrackedInventorySha256;
        switch (mutation)
        {
            case "core-commit": commit = new string('0', 40); break;
            case "core-tree": tree = new string('0', 40); break;
            case "source-provenance": repositoryAuthority = new string('0', 64); break;
            case "runtime-payload": inventory = new string('0', 64); break;
        }

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bCoreR38Authority.Validate(commit, tree, repositoryAuthority, inventory));

        Assert.Equal(Arch7bV2Blockers.CoreR38AuthorityMismatch, error.BlockerCode);
    }
}
