using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelopeTests
{
    [Fact]
    public void Valid_accepted_envelope_passes_but_remains_non_executable()
    {
        var result = Validate(Envelope());

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.PASS, result.FinalDecision);
        Assert.False(result.Envelope.CanRunExternalSnapshot);
        Assert.False(result.Envelope.EligibleForManualSnapshotAttempt);
        Assert.False(result.Envelope.IsApprovedForExternalRun);
    }

    [Fact]
    public void Draft_can_be_incomplete_but_non_executable()
    {
        var result = Validate(Envelope() with
        {
            Decision = LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.Draft,
            ReviewedByOperatorId = "",
            ConfirmsDemoOnly = false
        });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.PASS, result.FinalDecision);
        Assert.False(result.Envelope.CanRunExternalSnapshot);
    }

    [Fact]
    public void Missing_attestation_fails_accepted()
    {
        var result = Validate(Envelope() with { ConfirmsNoOrderSubmission = false });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == "ConfirmsNoOrderSubmission");
    }

    [Theory]
    [InlineData("NZDUSD", "4002", "SymbolExistsInPreflightManifest")]
    [InlineData("GBPUSD", "4999", "PlanningSecurityIdMatches")]
    [InlineData("GBPUSD", "TBD-LMAX-DEMO-GBPUSD", "PlanningSecurityIdMatches")]
    public void Unknown_or_wrong_securityid_fails(string symbol, string securityId, string check)
    {
        var result = Validate(Envelope() with { Symbol = symbol, PlanningSecurityId = securityId });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == check);
    }

    [Fact]
    public void Source_preflight_not_pass_fails()
    {
        var preflight = PreflightManifest();
        preflight = preflight with
        {
            Results = preflight.Results.Select(x => x.Symbol == "GBPUSD" ? x with { FinalDecision = LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL } : x).ToArray()
        };

        var result = LmaxReadOnlyAdditionalInstrumentSnapshotApprovalValidator.Validate(Envelope(), preflight);

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == "SourcePreflightDecisionPass");
    }

    [Theory]
    [InlineData(true, false, false, "IsApprovedForExternalRunFalse")]
    [InlineData(false, true, false, "EligibleForManualSnapshotAttemptFalse")]
    [InlineData(false, false, true, "CanRunExternalSnapshotFalse")]
    public void Executable_flags_fail(bool approved, bool eligible, bool canRun, string check)
    {
        var result = Validate(Envelope() with
        {
            IsApprovedForExternalRun = approved,
            EligibleForManualSnapshotAttempt = eligible,
            CanRunExternalSnapshot = canRun
        });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == check);
    }

    [Theory]
    [InlineData("production authorization", "NoTradingOrExternalAuthorizationLanguage")]
    [InlineData("order submission", "NoTradingOrExternalAuthorizationLanguage")]
    [InlineData("password=sentinel", "NoSensitiveContent")]
    public void Unsafe_language_fails(string reason, string check)
    {
        var result = Validate(Envelope() with { Reason = reason });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == check);
    }

    [Fact]
    public void Review_returns_warning_with_no_envelopes()
    {
        var review = LmaxReadOnlyAdditionalInstrumentSnapshotApprovalValidator.Review([], PreflightManifest());

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.PASS_WITH_KNOWN_WARNINGS, review.FinalDecision);
    }

    [Fact]
    public void Review_returns_pass_with_one_valid_accepted_envelope()
    {
        var review = LmaxReadOnlyAdditionalInstrumentSnapshotApprovalValidator.Review([Envelope()], PreflightManifest());

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.PASS, review.FinalDecision);
        Assert.Equal(1, review.AcceptedForPlanningCount);
    }

    [Fact]
    public void Conflicting_envelopes_fail()
    {
        var review = LmaxReadOnlyAdditionalInstrumentSnapshotApprovalValidator.Review([Envelope(), Envelope() with { PlanningSecurityId = "4999" }], PreflightManifest());

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.FAIL, review.FinalDecision);
        Assert.True(review.ConflictCount > 0 || review.InvalidEnvelopeCount > 0);
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase6q()
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

    private static LmaxReadOnlyAdditionalInstrumentSnapshotApprovalResult Validate(LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope envelope)
        => LmaxReadOnlyAdditionalInstrumentSnapshotApprovalValidator.Validate(envelope, PreflightManifest());

    private static LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope Envelope()
        => new(
            ApprovalEnvelopeId: "approval-GBPUSD",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            RequestedByOperatorId: "local-operator",
            ReviewedByOperatorId: "local-operator",
            Reason: "Phase 6Q planning envelope",
            Symbol: "GBPUSD",
            SlashSymbol: "GBP/USD",
            PlanningSecurityId: "4002",
            SecurityIdSource: "8",
            EnvironmentName: "Demo",
            VenueProfileName: "DemoLondon",
            RequestMode: "SnapshotPlusUpdates",
            SymbolEncodingMode: "SecurityIdOnly",
            MarketDepth: 1,
            MaxRuntimeSeconds: 30,
            MaxWaitSeconds: 30,
            MaxEventsPerRun: 25,
            SourcePreflightManifestPath: "preflight.json",
            SourcePreflightDecision: "PASS",
            ConfirmsDemoOnly: true,
            ConfirmsReadOnlyMarketDataOnly: true,
            ConfirmsNoOrderSubmission: true,
            ConfirmsNoSchedulerOrPolling: true,
            ConfirmsNoRuntimeShadowReplaySubmit: true,
            ConfirmsNoTradingMutation: true,
            ConfirmsSingleInstrumentOnly: true,
            ConfirmsFutureExplicitManualRunRequired: true,
            IsApprovedForExternalRun: false,
            EligibleForManualSnapshotAttempt: false,
            CanRunExternalSnapshot: false,
            NoSensitiveContent: true,
            Decision: LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.AcceptedForPlanning);

    private static LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifest PreflightManifest()
    {
        var planning = new LmaxReadOnlyInstrumentSecurityIdPlanningManifest(
            "planning", DateTimeOffset.UtcNow, "Demo", "DemoLondon",
            [Entry("GBPUSD", "GBP/USD", "4002")],
            false, false, false, false, false, false, false, false, false, false, false, true);
        var safety = LmaxReadOnlyAdditionalInstrumentSafetyGateManifestBuilder.FromPlanningManifest(planning, "planning.json");
        return LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifestBuilder.FromPlanningAndSafetyGates(planning, safety, "planning.json", "safety.json", "local-operator", "Phase 6P design");
    }

    private static LmaxReadOnlyInstrumentSecurityIdPlanningEntry Entry(string symbol, string slashSymbol, string securityId)
        => new(symbol, slashSymbol, securityId, "8", "OfficialLmaxDocument", "LMAX CSV", $"record-{symbol}", $"record-{symbol}.json", LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.AcceptedForPlanning, false, "Demo", "DemoLondon", true);

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
