using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bStageFactGraphTests
{
    [Fact]
    public void Clock_fact_contract_authority_is_closed_and_exact()
    {
        Assert.Equal(3, Arch7bClockFactContracts.All.Count);
        Assert.Equal(new[]
        {
            Arch7bClockFactContracts.PreflightFactType,
            Arch7bClockFactContracts.CaptureStartFactType,
            Arch7bClockFactContracts.PostCloseFactType
        }, Arch7bClockFactContracts.All.Select(value => value.FactType));
        Assert.Equal(3, Arch7bClockFactContracts.LegacyAliases.Count);
    }

    [Theory]
    [InlineData("CLOCK_PREFLIGHT", "clock_authority_preflight_snapshot")]
    [InlineData("CLOCK_CAPTURE_START", "clock_authority_capture_snapshot")]
    [InlineData("CLOCK_POST_CLOSE", "clock_authority_post_close_snapshot")]
    public void Clock_stage_produces_its_canonical_fact(string stageId, string factType)
    {
        var stage = ValidStages().Single(value => value.StageId == stageId);

        Assert.Contains(factType, stage.ProducedFactTypes);
        Assert.Equal(factType,
            Arch7bClockFactContracts.RequireProducer(stageId).FactType);
    }

    [Theory]
    [InlineData("PORTAL_SESSION_PROVEN", "clock_authority_preflight_snapshot")]
    [InlineData("MARKET_CAPTURE", "clock_authority_capture_snapshot")]
    [InlineData("MARKET_FINALIZATION", "clock_authority_post_close_snapshot")]
    public void Clock_consumer_requires_its_canonical_fact(string stageId, string factType)
    {
        var stage = ValidStages().Single(value => value.StageId == stageId);

        Assert.Contains(factType, stage.RequiredFactTypes);
    }

    [Theory]
    [InlineData("PORTAL_SESSION_PROVEN", "clock_preflight_evidence")]
    [InlineData("MARKET_CAPTURE", "clock_capture_start_evidence")]
    [InlineData("MARKET_FINALIZATION", "clock_post_close_evidence")]
    public void Legacy_clock_alias_is_rejected(string stageId, string alias)
    {
        var stages = ReplaceStage(ValidStages(), stageId, stage => stage with
        {
            RequiredFactTypes = stage.RequiredFactTypes.Append(alias).ToArray()
        });

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bStageFactGraphValidator.RequireValid(stages));

        Assert.Equal(Arch7bV2Blockers.LegacyStageFactAliasPresent,
            error.BlockerCode);
    }

    [Fact]
    public void Intended_legacy_requirement_is_replaced_by_the_canonical_fact()
    {
        var normalized = Arch7bClockFactContracts.NormalizeRequiredFacts(
            "PORTAL_SESSION_PROVEN",
            [Arch7bClockFactContracts.LegacyPreflightFactType]);

        Assert.Equal([Arch7bClockFactContracts.PreflightFactType], normalized);
    }

    [Fact]
    public void Legacy_alias_on_an_unrelated_stage_is_not_silently_removed()
    {
        var normalized = Arch7bClockFactContracts.NormalizeRequiredFacts(
            "REPORTING", [Arch7bClockFactContracts.LegacyPreflightFactType]);

        Assert.Equal([Arch7bClockFactContracts.LegacyPreflightFactType], normalized);
    }

    [Fact]
    public void Canonical_requirement_is_not_duplicated_by_normalization()
    {
        var normalized = Arch7bClockFactContracts.NormalizeRequiredFacts(
            "MARKET_CAPTURE",
            [Arch7bClockFactContracts.CaptureStartFactType,
                Arch7bClockFactContracts.CaptureStartFactType]);

        Assert.Equal([Arch7bClockFactContracts.CaptureStartFactType], normalized);
    }

    [Fact]
    public void Required_fact_without_a_producer_is_rejected()
    {
        var stages = ReplaceStage(ValidStages(), "PORTAL_SESSION_PROVEN", stage =>
            stage with { RequiredFactTypes = stage.RequiredFactTypes.Append(
                "missing_fact").ToArray() });

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bStageFactGraphValidator.RequireValid(stages));

        Assert.Equal(Arch7bV2Blockers.StageFactRequiredProducerMissing,
            error.BlockerCode);
    }

    [Fact]
    public void Duplicate_fact_producer_is_rejected()
    {
        var stages = ReplaceStage(ValidStages(), "STATIC_AUTHORITY_VALIDATION", stage =>
            stage with { ProducedFactTypes = stage.ProducedFactTypes.Append(
                Arch7bClockFactContracts.PreflightFactType).ToArray() });

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bStageFactGraphValidator.RequireValid(stages));

        Assert.Equal(Arch7bV2Blockers.StageFactDuplicateProducer,
            error.BlockerCode);
    }

    [Fact]
    public void Producer_at_or_after_consumer_is_rejected()
    {
        var stages = ReplaceStage(ValidStages(), "CLOCK_PREFLIGHT", stage => stage with
        {
            ProducedFactTypes = stage.ProducedFactTypes.Where(value => value !=
                Arch7bClockFactContracts.PreflightFactType).ToArray()
        });
        stages = ReplaceStage(stages, "PORTAL_SESSION_PROVEN", stage => stage with
        {
            ProducedFactTypes = stage.ProducedFactTypes.Append(
                Arch7bClockFactContracts.PreflightFactType).ToArray()
        });

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bStageFactGraphValidator.RequireValid(stages));

        Assert.Equal(Arch7bV2Blockers.StageFactProducerNotBeforeConsumer,
            error.BlockerCode);
    }

    [Fact]
    public void Final_forty_stage_graph_is_accepted()
    {
        var result = Arch7bStageFactGraphValidator.RequireValid(ValidStages());

        Assert.Equal(40, result.StageCount);
        Assert.Equal("PASS", result.ValidationStatus);
        Assert.All(result.Stages, value => Assert.Equal("PASS", value.Status));
    }

    [Fact]
    public void Final_graph_has_no_missing_duplicate_future_or_legacy_reference()
    {
        var result = Arch7bStageFactGraphValidator.RequireValid(ValidStages());

        Assert.Equal(0, result.MissingProducerCount);
        Assert.Equal(0, result.DuplicateProducerCount);
        Assert.Equal(0, result.FutureProducerCount);
        Assert.Equal(0, result.LegacyAliasCount);
    }

    [Fact]
    public void Every_final_requirement_has_exactly_one_earlier_producer()
    {
        var result = Arch7bStageFactGraphValidator.RequireValid(ValidStages());

        Assert.All(result.Stages.SelectMany(value => value.Requirements), value =>
        {
            Assert.Equal("PASS", value.Status);
            Assert.NotNull(value.ProducerStage);
            Assert.True(value.ProducerIndex < value.ConsumerIndex);
        });
    }

    [Fact]
    public void Validation_evidence_is_byte_deterministic()
    {
        var first = Arch7bStageFactGraphValidator.RequireValid(ValidStages());
        var second = Arch7bStageFactGraphValidator.RequireValid(ValidStages());

        Assert.Equal(first.EvidenceSha256, second.EvidenceSha256);
        Assert.Equal(Arch7bStageFactGraphValidator.SerializeInventory(first),
            Arch7bStageFactGraphValidator.SerializeInventory(second));
    }

    [Fact]
    public void Invalid_graph_is_rejected_before_any_run_root_side_effect()
    {
        var runRoot = Path.Combine(Path.GetTempPath(), "arch7b-invalid-graph-" +
            Guid.NewGuid().ToString("N"));
        var fixture = Arch7bV2QualificationFactory.Create(
            typeof(QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor.Program)
                .Assembly.Location, runRoot);
        var stages = ReplaceStage(fixture.Template.StageContracts,
            "PORTAL_SESSION_PROVEN", stage => stage with
            {
                RequiredFactTypes = stage.RequiredFactTypes.Append(
                    "missing_pre_calendar_fact").ToArray()
            });
        var template = fixture.Template with
        {
            StageContracts = stages,
            EvidenceSha256 = string.Empty
        };
        template = template with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(template.Canonical())
        };

        var error = Assert.Throws<Arch7bQualificationException>(() =>
            Arch7bLiveTemplateValidator.Validate(template,
                new Arch7bRealCommandAdapterRegistry()));

        Assert.Equal(Arch7bV2Blockers.StageFactRequiredProducerMissing,
            error.BlockerCode);
        Assert.False(Directory.Exists(runRoot));
    }

    private static IReadOnlyList<Arch7bOneShotStageContract> ValidStages()
    {
        var root = Path.Combine(Path.GetTempPath(), "arch7b-valid-graph-" +
            Guid.NewGuid().ToString("N"));
        return Arch7bV2QualificationFactory.Create(
            typeof(QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor.Program)
                .Assembly.Location, root).Template.StageContracts;
    }

    private static IReadOnlyList<Arch7bOneShotStageContract> ReplaceStage(
        IReadOnlyList<Arch7bOneShotStageContract> source,
        string stageId,
        Func<Arch7bOneShotStageContract, Arch7bOneShotStageContract> replace) =>
        source.Select(stage => stage.StageId == stageId ? replace(stage) : stage)
            .ToArray();
}
