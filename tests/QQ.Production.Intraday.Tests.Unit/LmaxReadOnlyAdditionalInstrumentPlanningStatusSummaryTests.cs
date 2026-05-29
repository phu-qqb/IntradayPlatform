using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyAdditionalInstrumentPlanningStatusSummaryTests
{
    [Fact]
    public void Summary_from_valid_pipeline_manifest_returns_pass_and_zero_executable_count()
    {
        var validation = LmaxReadOnlyAdditionalInstrumentPlanningStatusSummaryValidator.FromPipelineManifest(Manifest(), "", "FakeLmaxGateway");

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentPlanningStatusDecision.PASS, validation.FinalDecision);
        Assert.Equal("PASS", validation.Summary.AggregateDecision);
        Assert.Equal(4, validation.Summary.InstrumentCount);
        Assert.Equal(0, validation.Summary.ExecutableCount);
        Assert.False(validation.Summary.RuntimeShadowReplaySubmit);
        Assert.False(validation.Summary.SchedulerOrPolling);
        Assert.False(validation.Summary.OrderSubmission);
        Assert.False(validation.Summary.GatewayRegistration);
        Assert.False(validation.Summary.TradingMutation);
    }

    [Fact]
    public void Summary_contains_all_four_instruments()
    {
        var symbols = LmaxReadOnlyAdditionalInstrumentPlanningStatusSummaryValidator
            .FromPipelineManifest(Manifest(), "", "FakeLmaxGateway")
            .Summary
            .Instruments
            .Select(x => x.Symbol)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("GBPUSD", symbols);
        Assert.Contains("EURGBP", symbols);
        Assert.Contains("USDJPY", symbols);
        Assert.Contains("AUDUSD", symbols);
    }

    [Fact]
    public void Summary_fails_if_any_instrument_can_run_external_snapshot()
    {
        var manifest = Manifest(mutate: x => x.Symbol == "EURGBP" ? x with { CanRunExternalSnapshot = true } : x);

        Assert.Equal(
            LmaxReadOnlyAdditionalInstrumentPlanningStatusDecision.FAIL,
            LmaxReadOnlyAdditionalInstrumentPlanningStatusSummaryValidator.FromPipelineManifest(manifest, "", "FakeLmaxGateway").FinalDecision);
    }

    [Fact]
    public void Summary_fails_if_any_instrument_is_approved_for_external_run()
    {
        var manifest = Manifest(mutate: x => x.Symbol == "USDJPY" ? x with { IsApprovedForExternalRun = true } : x);

        Assert.Equal(
            LmaxReadOnlyAdditionalInstrumentPlanningStatusDecision.FAIL,
            LmaxReadOnlyAdditionalInstrumentPlanningStatusSummaryValidator.FromPipelineManifest(manifest, "", "FakeLmaxGateway").FinalDecision);
    }

    [Fact]
    public void Summary_fails_if_executable_count_is_non_zero()
        => Assert.Equal(
            LmaxReadOnlyAdditionalInstrumentPlanningStatusDecision.FAIL,
            LmaxReadOnlyAdditionalInstrumentPlanningStatusSummaryValidator.FromPipelineManifest(Manifest() with { ExecutableCount = 1 }, "", "FakeLmaxGateway").FinalDecision);

    [Fact]
    public void Summary_fails_if_api_worker_gateway_is_not_fake()
        => Assert.Equal(
            LmaxReadOnlyAdditionalInstrumentPlanningStatusDecision.FAIL,
            LmaxReadOnlyAdditionalInstrumentPlanningStatusSummaryValidator.FromPipelineManifest(Manifest(), "", "RealLmaxGateway").FinalDecision);

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("bearer sentinel")]
    public void Sensitive_content_fails(string rawText)
        => Assert.Equal(
            LmaxReadOnlyAdditionalInstrumentPlanningStatusDecision.FAIL,
            LmaxReadOnlyAdditionalInstrumentPlanningStatusSummaryValidator.FromPipelineManifest(Manifest(), rawText, "FakeLmaxGateway").FinalDecision);

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase6zc()
    {
        var repoRoot = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgramPath = Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Worker", "Program.cs");
        var workerProgram = File.Exists(workerProgramPath) ? File.ReadAllText(workerProgramPath) : string.Empty;
        var combined = apiProgram + Environment.NewLine + workerProgram;

        Assert.Contains("FakeLmaxGateway", apiProgram);
        Assert.DoesNotContain("RealLmaxGateway", combined);
        Assert.DoesNotContain("LmaxVenueGatewaySkeleton", combined);
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
            "Phase 6Z-C status summary test",
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
