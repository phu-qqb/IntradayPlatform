using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bV2NegativeMatrixTests
{
    [Fact]
    public void Extended_matrix_is_versioned_complete_unique_and_bound_to_exact_blockers()
    {
        var cases = Arch7bV2NegativeMatrix.Cases;

        Assert.Equal(31, cases.Count);
        Assert.Equal(Enumerable.Range(1, 31), cases.Select(value => value.Id));
        Assert.Equal(31, cases.Select(value => value.Scenario).Distinct(StringComparer.Ordinal).Count());
        Assert.All(cases, value =>
        {
            Assert.StartsWith("ARCH7B_", value.ExpectedBlocker, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(value.ValidatorId));
        });
        Assert.Equal(["ADAPTER", "AUTHORITY", "CHRONOLOGY", "PLAN", "PROCESS", "SECRET"],
            cases.Select(value => value.Category).Distinct(StringComparer.Ordinal).Order().ToArray());
        Assert.True(Arch7bOneShotContracts.IsSha256(Arch7bV2NegativeMatrix.EvidenceSha256));
    }
}
