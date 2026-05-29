using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyInstrumentSecurityIdDiscoveryManifestValidatorTests
{
    [Fact]
    public void Default_manifest_validates_as_pass()
    {
        var result = LmaxReadOnlyInstrumentSecurityIdDiscoveryManifestValidator.Validate();

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdDiscoveryDecision.PASS, result.Decision);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void All_allowlist_symbols_have_security_id_entries()
    {
        var manifest = new LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest();

        foreach (var symbol in LmaxReadOnlyInstrumentAllowlist.CandidateEntries.Select(x => x.Symbol))
        {
            var securityId = manifest.GetCandidateSecurityId(symbol);

            Assert.False(string.IsNullOrWhiteSpace(securityId));
        }
    }

    [Fact]
    public void All_entries_keep_external_run_approval_false()
    {
        var manifest = new LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest();

        Assert.All(manifest.Entries, entry => Assert.False(entry.IsApprovedForExternalRun));
    }

    [Fact]
    public void Missing_security_id_fails()
    {
        var entries = LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest.CreateDefaultEntries()
            .Select(entry => entry.Symbol == "GBPUSD" ? entry with { SecurityId = "" } : entry)
            .ToArray();
        var manifest = new LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest(
            entries,
            LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest.CreateDefaultSafety());

        var result = LmaxReadOnlyInstrumentSecurityIdDiscoveryManifestValidator.Validate(manifest);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdDiscoveryDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "SecurityIdMissing");
    }

    [Fact]
    public void External_run_approval_true_fails()
    {
        var entries = LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest.CreateDefaultEntries()
            .Select(entry => entry.Symbol == "USDJPY" ? entry with { IsApprovedForExternalRun = true } : entry)
            .ToArray();
        var manifest = new LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest(
            entries,
            LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest.CreateDefaultSafety());

        var result = LmaxReadOnlyInstrumentSecurityIdDiscoveryManifestValidator.Validate(manifest);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdDiscoveryDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ExternalRunApprovalForbidden");
    }

    [Fact]
    public void Runtime_action_flags_fail()
    {
        var unsafeSafety = LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest.CreateDefaultSafety() with
        {
            ExternalConnectionAttempted = true,
            ExternalApiCallAttempted = true,
            SchedulerOrPollingAdded = true,
            RuntimeShadowReplaySubmit = true,
            OrderSubmissionAdded = true,
            GatewayRegistrationAdded = true,
            TradingMutationAdded = true
        };
        var manifest = new LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest(
            LmaxReadOnlyInstrumentSecurityIdDiscoveryManifest.CreateDefaultEntries(),
            unsafeSafety);

        var result = LmaxReadOnlyInstrumentSecurityIdDiscoveryManifestValidator.Validate(manifest);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdDiscoveryDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ExternalConnectionForbidden");
        Assert.Contains(result.Errors, x => x.Code == "ExternalApiCallForbidden");
        Assert.Contains(result.Errors, x => x.Code == "SchedulerPollingForbidden");
        Assert.Contains(result.Errors, x => x.Code == "RuntimeShadowReplaySubmitForbidden");
        Assert.Contains(result.Errors, x => x.Code == "OrderSubmissionForbidden");
        Assert.Contains(result.Errors, x => x.Code == "GatewayRegistrationForbidden");
        Assert.Contains(result.Errors, x => x.Code == "TradingMutationForbidden");
    }
}
