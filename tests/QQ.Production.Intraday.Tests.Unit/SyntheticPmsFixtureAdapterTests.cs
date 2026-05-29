using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class SyntheticPmsFixtureAdapterTests
{
    [Fact]
    public void Adapter_accepts_canonical_symbol_weight_decimal_f8()
    {
        var result = Adapt(["EURUSD;0.10000000"]);

        Assert.True(result.Succeeded);
        Assert.True(result.NotQubesEconomicOutput);
        Assert.True(result.PaperOnly);
        Assert.True(result.NonExecutable);
        Assert.Equal("CanonicalModelSymbol;WeightDecimalF8", result.InputFormat);
        Assert.Equal("SyntheticOperatorFixture", result.SourceType);
        Assert.Equal(["EURUSD Curncy;0.10000000"], result.InternalPaperInputLines);
    }

    [Fact]
    public void Adapter_requires_not_qubes_economic_output_allowance()
    {
        var result = new SyntheticPmsFixtureAdapter().Adapt(new SyntheticPmsFixtureAdapterRequest(
            ["EURUSD;0.10000000"],
            AllowSyntheticPmsFixture: true,
            AllowNotQubesEconomicOutputFixture: false));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, x => x.Code == SyntheticPmsFixtureAdapterIssueCode.MissingAllowance);
    }

    [Theory]
    [InlineData("EURUSD Curncy;0.10000000", SyntheticPmsFixtureAdapterIssueCode.BloombergTickerRejected)]
    [InlineData("GBPUSD;0.10000000", SyntheticPmsFixtureAdapterIssueCode.UnsupportedSymbol)]
    [InlineData("EURUSD;NaN", SyntheticPmsFixtureAdapterIssueCode.InvalidWeight)]
    [InlineData("EURUSD;Infinity", SyntheticPmsFixtureAdapterIssueCode.InvalidWeight)]
    [InlineData("EURUSD;0.1", SyntheticPmsFixtureAdapterIssueCode.NonF8Weight)]
    [InlineData("EURUSD;0.1000000", SyntheticPmsFixtureAdapterIssueCode.NonF8Weight)]
    [InlineData("EURUSD;0.100000000", SyntheticPmsFixtureAdapterIssueCode.NonF8Weight)]
    [InlineData("EURUSD,0.10000000", SyntheticPmsFixtureAdapterIssueCode.InvalidRowShape)]
    public void Adapter_rejects_invalid_synthetic_fixture_rows(
        string row,
        SyntheticPmsFixtureAdapterIssueCode expectedIssue)
    {
        var result = Adapt([row]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, x => x.Code == expectedIssue);
    }

    [Fact]
    public void Adapter_rejects_duplicate_symbols()
    {
        var result = Adapt(["EURUSD;0.10000000", "EURUSD;0.20000000"]);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, x => x.Code == SyntheticPmsFixtureAdapterIssueCode.DuplicateSymbol);
    }

    private static SyntheticPmsFixtureAdapterResult Adapt(IReadOnlyList<string> rows)
        => new SyntheticPmsFixtureAdapter().Adapt(new SyntheticPmsFixtureAdapterRequest(
            rows,
            AllowSyntheticPmsFixture: true,
            AllowNotQubesEconomicOutputFixture: true));
}
