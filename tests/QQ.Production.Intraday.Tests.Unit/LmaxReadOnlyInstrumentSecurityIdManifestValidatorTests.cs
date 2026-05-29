using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyInstrumentSecurityIdManifestValidatorTests
{
    [Fact]
    public void All_phase6b_allowlist_symbols_have_security_id_in_manifest()
    {
        var manifest = new LmaxReadOnlyInstrumentSecurityIdManifest();

        foreach (var entry in LmaxReadOnlyInstrumentAllowlist.CandidateEntries)
        {
            var securityId = manifest.GetConfirmedSecurityId(entry.Symbol);

            Assert.False(string.IsNullOrWhiteSpace(securityId));
        }
    }

    [Fact]
    public void All_external_run_approvals_are_false()
    {
        var manifest = new LmaxReadOnlyInstrumentSecurityIdManifest();

        Assert.All(manifest.IsApprovedForExternalRun, approval => Assert.False(approval.Value));
        Assert.True(manifest.AllExternalRunsBlocked());
    }

    [Theory]
    [InlineData("GBPUSD", "PHASE6C-DEMO-SECURITYID-GBPUSD")]
    [InlineData("USDJPY", "PHASE6C-DEMO-SECURITYID-USDJPY")]
    [InlineData("EURGBP", "PHASE6C-DEMO-SECURITYID-EURGBP")]
    [InlineData("AUDUSD", "PHASE6C-DEMO-SECURITYID-AUDUSD")]
    public void Get_confirmed_security_id_returns_expected_value_for_known_symbol(string symbol, string expectedSecurityId)
    {
        var manifest = new LmaxReadOnlyInstrumentSecurityIdManifest();

        var securityId = manifest.GetConfirmedSecurityId(symbol);

        Assert.Equal(expectedSecurityId, securityId);
    }

    [Fact]
    public void Get_confirmed_security_id_returns_null_for_unknown_symbol()
    {
        var manifest = new LmaxReadOnlyInstrumentSecurityIdManifest();

        var securityId = manifest.GetConfirmedSecurityId("NZDCHF");

        Assert.Null(securityId);
    }

    [Fact]
    public void All_instruments_confirmed_returns_true_when_manifest_is_complete()
    {
        var manifest = new LmaxReadOnlyInstrumentSecurityIdManifest();

        Assert.True(manifest.AllInstrumentsConfirmed());
    }
}
