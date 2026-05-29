using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyPerInstrumentSafetyGateTests
{
    [Fact]
    public void Per_instrument_gate_passes_complete_planning_value_but_keeps_snapshot_eligibility_false()
    {
        var entry = Entry("GBPUSD", "GBP/USD", "4002");

        var result = LmaxReadOnlyPerInstrumentSafetyGateValidator.Validate(entry);

        Assert.Equal(LmaxReadOnlyPerInstrumentSafetyGateDecision.PASS, result.FinalDecision);
        Assert.False(result.IsApprovedForExternalRun);
        Assert.False(result.EligibleForManualSnapshotAttempt);
        Assert.Contains(result.Checks, x => x.Name == "RequiresFutureExplicitOperatorPrompt" && x.Decision == LmaxReadOnlyPerInstrumentSafetyGateDecision.PASS);
    }

    [Fact]
    public void Externally_approved_instrument_fails()
    {
        var result = LmaxReadOnlyPerInstrumentSafetyGateValidator.Validate(Entry("GBPUSD", "GBP/USD", "4002") with { IsApprovedForExternalRun = true });

        Assert.Equal(LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == "IsNotApprovedForExternalRun" && x.Decision == LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL);
    }

    [Theory]
    [InlineData("")]
    [InlineData("TBD-LMAX-DEMO-GBPUSD")]
    [InlineData("PHASE6C-CONFIRMATION-PENDING")]
    [InlineData("PHASE6D-DISCOVERY-PENDING")]
    public void Missing_or_placeholder_securityid_fails(string securityId)
    {
        var result = LmaxReadOnlyPerInstrumentSafetyGateValidator.Validate(Entry("GBPUSD", "GBP/USD", securityId));

        Assert.Equal(LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == "HasAcceptedSecurityIdPlanningValue" && x.Decision == LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL);
    }

    [Fact]
    public void Wrong_securityid_source_fails()
    {
        var result = LmaxReadOnlyPerInstrumentSafetyGateValidator.Validate(Entry("GBPUSD", "GBP/USD", "4002") with { SecurityIdSource = "TBD" });

        Assert.Equal(LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == "HasSecurityIdSource8" && x.Decision == LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL);
    }

    [Fact]
    public void Non_demo_environment_fails()
    {
        var result = LmaxReadOnlyPerInstrumentSafetyGateValidator.Validate(Entry("GBPUSD", "GBP/USD", "4002") with { EnvironmentName = "Production" });

        Assert.Equal(LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == "HasDemoEnvironment" && x.Decision == LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL);
    }

    [Fact]
    public void Non_demo_london_profile_fails()
    {
        var result = LmaxReadOnlyPerInstrumentSafetyGateValidator.Validate(Entry("GBPUSD", "GBP/USD", "4002") with { VenueProfileName = "DemoTokyo" });

        Assert.Equal(LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == "HasDemoLondonVenueProfile" && x.Decision == LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL);
    }

    [Fact]
    public void Missing_evidence_decision_fails()
    {
        var result = LmaxReadOnlyPerInstrumentSafetyGateValidator.Validate(Entry("GBPUSD", "GBP/USD", "4002") with { Decision = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.Draft });

        Assert.Equal(LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == "EvidenceDecisionAcceptedForPlanning" && x.Decision == LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL);
    }

    [Fact]
    public void Aggregate_manifest_includes_all_four_instruments()
    {
        var planning = PlanningManifest();

        var manifest = LmaxReadOnlyAdditionalInstrumentSafetyGateManifestBuilder.FromPlanningManifest(planning, "planning.json");
        var result = LmaxReadOnlyAdditionalInstrumentSafetyGateManifestValidator.Validate(manifest);

        Assert.Equal(LmaxReadOnlyPerInstrumentSafetyGateDecision.PASS, result.Decision);
        Assert.Equal(4, manifest.InstrumentCount);
        Assert.Equal(4, manifest.PassCount);
        Assert.False(manifest.AllApprovedForExternalRun);
        Assert.False(manifest.AnyEligibleForManualSnapshotAttempt);
        Assert.Contains(manifest.Instruments, x => x.Symbol == "GBPUSD" && x.PlanningSecurityId == "4002");
        Assert.Contains(manifest.Instruments, x => x.Symbol == "EURGBP" && x.PlanningSecurityId == "4003");
        Assert.Contains(manifest.Instruments, x => x.Symbol == "USDJPY" && x.PlanningSecurityId == "4004");
        Assert.Contains(manifest.Instruments, x => x.Symbol == "AUDUSD" && x.PlanningSecurityId == "4007");
    }

    [Fact]
    public void Aggregate_manifest_fails_if_instrument_missing()
    {
        var planning = PlanningManifest();
        planning = planning with { Instruments = planning.Instruments.Where(x => x.Symbol != "AUDUSD").ToArray() };
        var manifest = LmaxReadOnlyAdditionalInstrumentSafetyGateManifestBuilder.FromPlanningManifest(planning, "planning.json");

        var result = LmaxReadOnlyAdditionalInstrumentSafetyGateManifestValidator.Validate(manifest);

        Assert.Equal(LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "SafetyGateMissing");
    }

    [Fact]
    public void Aggregate_manifest_fails_if_any_instrument_is_externally_approved()
    {
        var planning = PlanningManifest();
        planning = planning with
        {
            Instruments = planning.Instruments.Select(x => x.Symbol == "GBPUSD" ? x with { IsApprovedForExternalRun = true } : x).ToArray()
        };
        var manifest = LmaxReadOnlyAdditionalInstrumentSafetyGateManifestBuilder.FromPlanningManifest(planning, "planning.json");

        var result = LmaxReadOnlyAdditionalInstrumentSafetyGateManifestValidator.Validate(manifest);

        Assert.Equal(LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ExternalRunApprovalForbidden");
    }

    [Fact]
    public void Aggregate_manifest_fails_if_any_instrument_is_manual_snapshot_eligible_in_phase6o()
    {
        var manifest = LmaxReadOnlyAdditionalInstrumentSafetyGateManifestBuilder.FromPlanningManifest(PlanningManifest(), "planning.json");
        manifest = manifest with
        {
            Instruments = manifest.Instruments.Select(x => x.Symbol == "GBPUSD" ? x with { EligibleForManualSnapshotAttempt = true } : x).ToArray(),
            AnyEligibleForManualSnapshotAttempt = true
        };

        var result = LmaxReadOnlyAdditionalInstrumentSafetyGateManifestValidator.Validate(manifest);

        Assert.Equal(LmaxReadOnlyPerInstrumentSafetyGateDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ManualSnapshotEligibilityForbidden");
        Assert.Contains(result.Errors, x => x.Code == "AggregateManualSnapshotEligibilityForbidden");
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase6o()
    {
        var repoRoot = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgramPath = Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Worker", "Program.cs");
        var workerProgram = File.Exists(workerProgramPath) ? File.ReadAllText(workerProgramPath) : string.Empty;
        var combined = apiProgram + Environment.NewLine + workerProgram;

        Assert.Contains("FakeLmaxGateway", apiProgram);
        Assert.DoesNotContain("RealLmaxGateway", combined);
        Assert.DoesNotContain("LmaxVenueGatewaySkeleton", combined);
        Assert.DoesNotContain("SecurityListRequest", combined);
        Assert.DoesNotContain("PeriodicTimer", combined);
        Assert.DoesNotContain("NewOrderSingle", combined);
        Assert.DoesNotContain("OrderCancelRequest", combined);
        Assert.DoesNotContain("OrderCancelReplaceRequest", combined);
        Assert.DoesNotContain("SubmitOrder", combined);
        Assert.DoesNotContain("ReplaySubmitAsync", combined);
    }

    private static LmaxReadOnlyInstrumentSecurityIdPlanningEntry Entry(string symbol, string slashSymbol, string securityId)
        => new(
            Symbol: symbol,
            SlashSymbol: slashSymbol,
            PlanningSecurityId: securityId,
            SecurityIdSource: "8",
            EvidenceSource: "OfficialLmaxDocument",
            EvidenceReference: "LMAX instrument CSV, Instrument Name + LMAX ID + LMAX symbol columns",
            ConfirmationRecordId: $"record-{symbol}",
            ConfirmationRecordPath: $"artifacts/real/record-{symbol}.json",
            Decision: LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.AcceptedForPlanning,
            IsApprovedForExternalRun: false,
            EnvironmentName: "Demo",
            VenueProfileName: "DemoLondon",
            NoSensitiveContent: true);

    private static LmaxReadOnlyInstrumentSecurityIdPlanningManifest PlanningManifest()
        => new(
            ManifestId: "planning-test",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            EnvironmentName: "Demo",
            VenueProfileName: "DemoLondon",
            Instruments:
            [
                Entry("GBPUSD", "GBP/USD", "4002"),
                Entry("EURGBP", "EUR/GBP", "4003"),
                Entry("USDJPY", "USD/JPY", "4004"),
                Entry("AUDUSD", "AUD/USD", "4007")
            ],
            IsApprovedForExternalRun: false,
            ExternalConnectionAttempted: false,
            ExternalApiCallAttempted: false,
            SecurityListRequestAttempted: false,
            MarketDataSnapshotAttempted: false,
            ReplayAttempted: false,
            RuntimeShadowReplaySubmit: false,
            SchedulerOrPollingAdded: false,
            OrderSubmissionAdded: false,
            GatewayRegistrationAdded: false,
            TradingMutationAdded: false,
            NoSensitiveContent: true);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
