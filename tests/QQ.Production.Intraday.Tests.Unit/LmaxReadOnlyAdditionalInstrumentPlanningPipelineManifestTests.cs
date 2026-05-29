using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifestTests
{
    [Fact]
    public void Aggregate_pipeline_manifest_passes_when_all_instruments_have_safe_artifacts()
        => Assert.Equal(
            LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision.PASS,
            LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifestValidator.Validate(Manifest()).FinalDecision);

    [Theory]
    [InlineData("EURGBP", "4999")]
    [InlineData("USDJPY", "4003")]
    [InlineData("AUDUSD", "TBD")]
    public void Wrong_security_id_fails(string symbol, string securityId)
    {
        var manifest = Manifest(mutate: i => i.Symbol == symbol ? i with { PlanningSecurityId = securityId } : i);

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision.FAIL, LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifestValidator.Validate(manifest).FinalDecision);
    }

    [Fact]
    public void Missing_required_artifact_fails()
    {
        var manifest = Manifest(mutate: i => i.Symbol == "EURGBP" ? i with { FinalReadinessPath = "" } : i);

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision.FAIL, LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifestValidator.Validate(manifest).FinalDecision);
    }

    [Fact]
    public void Unexpected_source_decision_fails()
    {
        var manifest = Manifest(mutate: i => i.Symbol == "USDJPY" ? i with { DryRunDecision = "FAIL" } : i);

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision.FAIL, LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifestValidator.Validate(manifest).FinalDecision);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Executable_instrument_flags_fail(bool approved, bool eligible, bool canRun)
    {
        var manifest = Manifest(mutate: i => i.Symbol == "AUDUSD"
            ? i with { IsApprovedForExternalRun = approved, EligibleForManualSnapshotAttempt = eligible, CanRunExternalSnapshot = canRun }
            : i);

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision.FAIL, LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifestValidator.Validate(manifest).FinalDecision);
    }

    [Fact]
    public void Executable_count_must_be_zero()
        => Assert.Equal(
            LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision.FAIL,
            LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifestValidator.Validate(Manifest() with { ExecutableCount = 1 }).FinalDecision);

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("NewOrderSingle")]
    public void Sensitive_or_order_surface_text_fails(string rawText)
        => Assert.Equal(
            LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision.FAIL,
            LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifestValidator.Validate(Manifest(), rawText).FinalDecision);

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase6za()
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
        Assert.DoesNotContain("BackgroundService", combined);
        Assert.DoesNotContain("NewOrderSingle", combined);
        Assert.DoesNotContain("OrderCancelRequest", combined);
        Assert.DoesNotContain("OrderCancelReplaceRequest", combined);
        Assert.DoesNotContain("OrderStatusRequest", combined);
        Assert.DoesNotContain("SubmitOrder", combined);
        Assert.DoesNotContain("ReplaySubmitAsync", combined);
    }

    private static LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifest Manifest(Func<LmaxReadOnlyAdditionalInstrumentPlanningPipelineInstrument, LmaxReadOnlyAdditionalInstrumentPlanningPipelineInstrument>? mutate = null)
    {
        var instruments = new[]
        {
            Instrument("GBPUSD", "GBP/USD", "4002"),
            Instrument("EURGBP", "EUR/GBP", "4003"),
            Instrument("USDJPY", "USD/JPY", "4004"),
            Instrument("AUDUSD", "AUD/USD", "4007")
        }.Select(x => mutate?.Invoke(x) ?? x).ToList();

        return new(
            "manifest",
            DateTimeOffset.UtcNow,
            "local-operator",
            "local-operator",
            "Phase 6Z-A non-executable additional instrument planning pipeline replication",
            "planning.json",
            "safety.json",
            "preflight.json",
            instruments,
            InstrumentCount: instruments.Count,
            ReadyForFutureManualConsiderationCount: instruments.Count,
            ExecutableCount: 0,
            IsApprovedForExternalRun: false,
            CanRunExternalSnapshot: false,
            EligibleForManualSnapshotAttempt: false,
            ExternalConnectionAttempted: false,
            SnapshotAttempted: false,
            ReplayAttempted: false,
            SchedulerStarted: false,
            OrderSubmissionAttempted: false,
            ShadowReplaySubmitAttempted: false,
            TradingMutationAttempted: false,
            "FakeLmaxGateway",
            NoSensitiveContent: true,
            LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision.PASS);
    }

    private static LmaxReadOnlyAdditionalInstrumentPlanningPipelineInstrument Instrument(string symbol, string slashSymbol, string securityId)
        => new(
            symbol,
            slashSymbol,
            securityId,
            "8",
            PlanningValuePresent: true,
            "PASS",
            "PASS",
            "AcceptedForPlanning",
            "PASS",
            "PASS",
            "PASS",
            "SignedForPlanning",
            "PASS",
            $"{symbol}-approval.json",
            $"{symbol}-dryrun.json",
            $"{symbol}-attempt.json",
            $"{symbol}-execution.json",
            $"{symbol}-signoff.json",
            $"{symbol}-readiness.json",
            IsApprovedForExternalRun: false,
            EligibleForManualSnapshotAttempt: false,
            CanRunExternalSnapshot: false,
            ExternalConnectionAttempted: false,
            SnapshotAttempted: false,
            ReplayAttempted: false,
            OrderSubmissionAttempted: false,
            ShadowReplaySubmitAttempted: false,
            TradingMutationAttempted: false,
            SchedulerStarted: false,
            NoSensitiveContent: true);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
