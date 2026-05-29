using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyInstrumentAllowlistValidatorTests
{
    [Fact]
    public void Default_candidate_allowlist_validates_as_planning_pass()
    {
        var result = LmaxReadOnlyInstrumentAllowlistValidator.Validate();

        Assert.Equal(LmaxReadOnlyInstrumentAllowlistDecision.PASS, result.Decision);
        Assert.NotEmpty(result.Entries);
        Assert.DoesNotContain(result.Entries, x => x.Instrument == "EURUSD" || x.SecurityId == "4001");
        Assert.All(result.Entries, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Instrument));
            Assert.False(string.IsNullOrWhiteSpace(entry.Symbol));
            Assert.False(string.IsNullOrWhiteSpace(entry.SlashSymbol));
            Assert.False(string.IsNullOrWhiteSpace(entry.SecurityId));
            Assert.False(string.IsNullOrWhiteSpace(entry.SecurityIdSource));
            Assert.Equal("Demo", entry.EnvironmentName);
            Assert.Equal("LMAX Demo", entry.Venue);
            Assert.False(string.IsNullOrWhiteSpace(entry.LiquidityTier));
            Assert.Equal("MarketDataOnly", entry.EvidenceMode);
            Assert.True(entry.IsAllowlistedForPlanning);
            Assert.False(entry.IsApprovedForExternalRun);
            Assert.Equal(LmaxReadOnlyInstrumentDemoReadiness.CandidateRequiresDemoSecurityIdConfirmation, entry.DemoReadiness);
        });
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Missing_metadata_fails()
    {
        var entries = LmaxReadOnlyInstrumentAllowlist.CandidateEntries
            .Select((entry, index) => index == 0 ? entry with { SecurityId = "" } : entry)
            .ToArray();

        var result = LmaxReadOnlyInstrumentAllowlistValidator.Validate(entries);

        Assert.Equal(LmaxReadOnlyInstrumentAllowlistDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "SecurityIdRequired");
    }

    [Fact]
    public void Baseline_eurusd_is_not_allowed_in_additional_instrument_list()
    {
        var entries = LmaxReadOnlyInstrumentAllowlist.CandidateEntries
            .Append(LmaxReadOnlyInstrumentAllowlist.CandidateEntries[0] with
            {
                Instrument = "EURUSD",
                Symbol = "EURUSD",
                SlashSymbol = "EUR/USD",
                SecurityId = "4001"
            })
            .ToArray();

        var result = LmaxReadOnlyInstrumentAllowlistValidator.Validate(entries);

        Assert.Equal(LmaxReadOnlyInstrumentAllowlistDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "BaselineInstrumentNotAdditional");
    }

    [Fact]
    public void Unsafe_runtime_capability_flags_fail()
    {
        var unsafeRules = LmaxReadOnlyInstrumentAllowlist.Phase6BPlanningSafetyRules with
        {
            SchedulerAllowed = true,
            RuntimeShadowReplaySubmitAllowed = true,
            OrderSubmissionAllowed = true,
            GatewayRegistrationAllowed = true,
            TradingMutationAllowed = true,
            ExternalConnectionAllowedByThisPhase = true
        };

        var result = LmaxReadOnlyInstrumentAllowlistValidator.Validate(safetyRules: unsafeRules);

        Assert.Equal(LmaxReadOnlyInstrumentAllowlistDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "SchedulerForbidden");
        Assert.Contains(result.Errors, x => x.Code == "RuntimeShadowReplaySubmitForbidden");
        Assert.Contains(result.Errors, x => x.Code == "OrderSubmissionForbidden");
        Assert.Contains(result.Errors, x => x.Code == "GatewayRegistrationForbidden");
        Assert.Contains(result.Errors, x => x.Code == "TradingMutationForbidden");
        Assert.Contains(result.Errors, x => x.Code == "ExternalConnectionForbidden");
    }

    [Fact]
    public void Only_allowlisted_instruments_validate_for_planning()
    {
        var allowed = LmaxReadOnlyInstrumentAllowlistValidator.ValidatePlannedRequest("GBPUSD", null);
        var blocked = LmaxReadOnlyInstrumentAllowlistValidator.ValidatePlannedRequest("NZDCHF", null);

        Assert.True(allowed.IsAllowlisted);
        Assert.False(allowed.CanRunExternallyInThisPhase);
        Assert.NotNull(allowed.Entry);
        Assert.Contains(allowed.Issues, x => x.Code == "InstrumentPlanningOnly");

        Assert.False(blocked.IsAllowlisted);
        Assert.False(blocked.CanRunExternallyInThisPhase);
        Assert.Contains(blocked.Issues, x => x.Code == "InstrumentNotAllowlisted");
    }

    [Fact]
    public void Non_marketdata_evidence_mode_fails()
    {
        var entries = LmaxReadOnlyInstrumentAllowlist.CandidateEntries
            .Select((entry, index) => index == 0 ? entry with { EvidenceMode = "MixedReadOnly" } : entry)
            .ToArray();

        var result = LmaxReadOnlyInstrumentAllowlistValidator.Validate(entries);

        Assert.Equal(LmaxReadOnlyInstrumentAllowlistDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "EvidenceModeMustBeMarketDataOnly");
    }
}
