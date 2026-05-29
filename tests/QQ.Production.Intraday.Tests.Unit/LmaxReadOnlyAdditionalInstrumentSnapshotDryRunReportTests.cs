using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReportTests
{
    [Fact]
    public void Valid_gbpusd_dryrun_passes_but_remains_non_executable()
    {
        var result = Validate(Report());

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.PASS, result.FinalDecision);
        Assert.False(result.Report.CanRunExternalSnapshot);
        Assert.False(result.Report.IsApprovedForExternalRun);
        Assert.False(result.Report.EligibleForManualSnapshotAttempt);
    }

    [Fact]
    public void Missing_or_non_accepted_approval_fails()
    {
        var planning = Planning();
        var safety = LmaxReadOnlyAdditionalInstrumentSafetyGateManifestBuilder.FromPlanningManifest(planning, "planning.json");
        var preflight = LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifestBuilder.FromPlanningAndSafetyGates(planning, safety, "planning.json", "safety.json", "local-operator", "Phase 6P design");

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.FAIL, LmaxReadOnlyAdditionalInstrumentSnapshotDryRunValidator.Validate(Report(), planning, safety, preflight, null).FinalDecision);
        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.FAIL, Validate(Report(), Envelope() with { Decision = LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.Draft }).FinalDecision);
    }

    [Theory]
    [InlineData("EURGBP", "4002")]
    [InlineData("GBPUSD", "4999")]
    public void Wrong_symbol_or_securityid_fails(string symbol, string securityId)
    {
        var result = Validate(Report() with { Symbol = symbol, PlanningSecurityId = securityId });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.FAIL, result.FinalDecision);
    }

    [Theory]
    [InlineData("9", "SecurityIdSource8")]
    [InlineData("Production", "DemoEnvironment")]
    [InlineData("DemoTokyo", "DemoLondonVenueProfile")]
    public void Wrong_source_or_scope_fails(string value, string check)
    {
        var report = check switch
        {
            "SecurityIdSource8" => Report() with { SecurityIdSource = value },
            "DemoEnvironment" => Report() with { EnvironmentName = value },
            _ => Report() with { VenueProfileName = value }
        };

        var result = Validate(report);

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == check);
    }

    [Theory]
    [InlineData(true, false, false, false, false, false, false, false, false, false)]
    [InlineData(false, true, false, false, false, false, false, false, false, false)]
    [InlineData(false, false, true, false, false, false, false, false, false, false)]
    [InlineData(false, false, false, true, false, false, false, false, false, false)]
    [InlineData(false, false, false, false, true, false, false, false, false, false)]
    [InlineData(false, false, false, false, false, true, false, false, false, false)]
    [InlineData(false, false, false, false, false, false, true, false, false, false)]
    [InlineData(false, false, false, false, false, false, false, true, false, false)]
    [InlineData(false, false, false, false, false, false, false, false, true, false)]
    [InlineData(false, false, false, false, false, false, false, false, false, true)]
    public void Executable_or_attempt_flags_fail(bool approved, bool eligible, bool canRun, bool external, bool snapshot, bool replay, bool order, bool shadow, bool mutation, bool scheduler)
    {
        var result = Validate(Report() with
        {
            IsApprovedForExternalRun = approved,
            EligibleForManualSnapshotAttempt = eligible,
            CanRunExternalSnapshot = canRun,
            ExternalConnectionAttempted = external,
            SnapshotAttempted = snapshot,
            ReplayAttempted = replay,
            OrderSubmissionAttempted = order,
            ShadowReplaySubmitAttempted = shadow,
            TradingMutationAttempted = mutation,
            SchedulerStarted = scheduler
        });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.FAIL, result.FinalDecision);
    }

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("production authorization")]
    [InlineData("order submission")]
    public void Sensitive_or_authorization_language_fails(string reason)
        => Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.FAIL, Validate(Report() with { Reason = reason }).FinalDecision);

    [Fact]
    public void Review_returns_warning_with_no_reports_and_pass_with_valid_report()
    {
        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.PASS_WITH_KNOWN_WARNINGS, LmaxReadOnlyAdditionalInstrumentSnapshotDryRunValidator.Review([]).FinalDecision);
        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.PASS, LmaxReadOnlyAdditionalInstrumentSnapshotDryRunValidator.Review([Report()]).FinalDecision);
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase6r()
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

    private static LmaxReadOnlyAdditionalInstrumentSnapshotDryRunResult Validate(LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReport report, LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope? envelope = null)
    {
        var planning = Planning();
        var safety = LmaxReadOnlyAdditionalInstrumentSafetyGateManifestBuilder.FromPlanningManifest(planning, "planning.json");
        var preflight = LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifestBuilder.FromPlanningAndSafetyGates(planning, safety, "planning.json", "safety.json", "local-operator", "Phase 6P design");
        return LmaxReadOnlyAdditionalInstrumentSnapshotDryRunValidator.Validate(report, planning, safety, preflight, envelope ?? Envelope());
    }

    private static LmaxReadOnlyAdditionalInstrumentSnapshotDryRunReport Report()
        => new("dryrun-GBPUSD", DateTimeOffset.UtcNow, "local-operator", "Phase 6R dry-run report", "GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1, 30, 30, 25, "planning.json", "safety.json", "preflight.json", "approval.json", "AcceptedForPlanning", "PASS", "PASS", "AcceptedForPlanning", LmaxReadOnlyAdditionalInstrumentSnapshotDryRunDecision.PASS, false, false, false, false, false, false, false, false, false, false, true, "explicit future operator-approved manual run phase", "Phase 6R is dry-run only; external snapshot not authorized.");

    private static LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope Envelope()
        => new("approval-GBPUSD", DateTimeOffset.UtcNow, "local-operator", "local-operator", "Phase 6Q planning", "GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1, 30, 30, 25, "preflight.json", "PASS", true, true, true, true, true, true, true, true, false, false, false, true, LmaxReadOnlyAdditionalInstrumentSnapshotApprovalDecision.AcceptedForPlanning);

    private static LmaxReadOnlyInstrumentSecurityIdPlanningManifest Planning()
        => new("planning", DateTimeOffset.UtcNow, "Demo", "DemoLondon", [new("GBPUSD", "GBP/USD", "4002", "8", "OfficialLmaxDocument", "LMAX CSV", "record-GBPUSD", "record.json", LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.AcceptedForPlanning, false, "Demo", "DemoLondon", true)], false, false, false, false, false, false, false, false, false, false, false, true);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
