using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyAdditionalInstrumentFinalPreRunGateTests
{
    [Theory]
    [InlineData("USDJPY", "USD/JPY", "4004")]
    [InlineData("AUDUSD", "AUD/USD", "4007")]
    public void Valid_additional_instrument_final_pre_run_gate_validates_pass(string symbol, string slashSymbol, string securityId)
    {
        var result = LmaxReadOnlyAdditionalInstrumentFinalPreRunGateValidator.Validate(Gate(symbol, slashSymbol, securityId));

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision.PASS, result.FinalDecision);
        Assert.False(result.Gate.CanRunExternalSnapshot);
        Assert.False(result.Gate.IsApprovedForExternalRun);
        Assert.False(result.Gate.EligibleForManualSnapshotAttempt);
    }

    [Fact]
    public void Unknown_symbol_fails()
        => AssertFail(Gate("NZDUSD", "NZD/USD", "4999"));

    [Theory]
    [InlineData("USDJPY", "USD/JPY", "4999")]
    [InlineData("USDJPY", "USD/JPY", "6004")]
    public void Wrong_or_tokyo_security_id_fails(string symbol, string slashSymbol, string securityId)
        => AssertFail(Gate(symbol, slashSymbol, securityId));

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void Run_authorization_flags_fail(bool external, bool canRun, bool eligible, bool approved)
        => AssertFail(Gate() with
        {
            ExternalRunAuthorized = external,
            CanRunExternalSnapshot = canRun,
            EligibleForManualSnapshotAttempt = eligible,
            IsApprovedForExternalRun = approved
        });

    [Fact]
    public void Batch_execution_allowed_fails()
        => AssertFail(Gate() with { BatchExecutionAllowed = true });

    [Fact]
    public void One_instrument_false_fails()
        => AssertFail(Gate() with { OneInstrumentAtATime = false });

    [Fact]
    public void Executable_count_style_generic_readiness_json_is_rejected()
    {
        var phase6ZaReadinessShape = """
                                    {
                                      "readinessId": "lmax-readonly-additional-instrument-final-readiness-USDJPY",
                                      "createdAtUtc": "2026-05-09T17:58:50Z",
                                      "requestedByOperatorId": "local-operator",
                                      "reason": "Phase 6Z-A non-executable additional instrument planning pipeline replication",
                                      "symbol": "USDJPY",
                                      "slashSymbol": "USD/JPY",
                                      "planningSecurityId": "4004",
                                      "securityIdSource": "8",
                                      "environmentName": "Demo",
                                      "venueProfileName": "DemoLondon",
                                      "requestMode": "SnapshotPlusUpdates",
                                      "symbolEncodingMode": "SecurityIdOnly",
                                      "marketDepth": 1,
                                      "readinessDecision": "PASS",
                                      "executionPlanDecision": "PASS",
                                      "operatorSignoffDecision": "SignedForPlanning",
                                      "isApprovedForExternalRun": false,
                                      "eligibleForManualSnapshotAttempt": false,
                                      "canRunExternalSnapshot": false,
                                      "schedulerStarted": false,
                                      "runtimeShadowReplaySubmit": false,
                                      "orderSubmissionAttempted": false,
                                      "tradingMutationAttempted": false,
                                      "apiWorkerGatewayMode": "FakeLmaxGateway",
                                      "noSensitiveContent": true
                                    }
                                    """;

        var result = LmaxReadOnlyAdditionalInstrumentFinalPreRunGateValidator.ValidateJson(phase6ZaReadinessShape);

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == "ManualSingleInstrumentOnly" && x.Decision == LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision.FAIL);
    }

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public void Runtime_power_flags_fail(bool scheduler, bool shadow, bool order, bool mutation, bool gateway)
        => AssertFail(Gate() with
        {
            SchedulerOrPolling = scheduler,
            RuntimeShadowReplaySubmit = shadow,
            OrderSubmission = order,
            TradingMutation = mutation,
            GatewayRegistration = gateway
        });

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("NewOrderSingle")]
    [InlineData("production run is authorized")]
    public void Sensitive_or_authorization_language_fails(string rawText)
    {
        var result = LmaxReadOnlyAdditionalInstrumentFinalPreRunGateValidator.Validate(Gate(), rawText);

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision.FAIL, result.FinalDecision);
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase7h2()
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
        Assert.DoesNotContain("NewOrderSingle", combined);
        Assert.DoesNotContain("OrderCancelRequest", combined);
        Assert.DoesNotContain("OrderCancelReplaceRequest", combined);
        Assert.DoesNotContain("OrderStatusRequest", combined);
        Assert.DoesNotContain("SubmitOrder", combined);
        Assert.DoesNotContain("ReplaySubmitAsync", combined);
    }

    private static void AssertFail(LmaxReadOnlyAdditionalInstrumentFinalPreRunGate gate)
    {
        var result = LmaxReadOnlyAdditionalInstrumentFinalPreRunGateValidator.Validate(gate);

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision.FAIL, result.FinalDecision);
    }

    private static LmaxReadOnlyAdditionalInstrumentFinalPreRunGate Gate(
        string symbol = "USDJPY",
        string slashSymbol = "USD/JPY",
        string securityId = "4004")
        => new(
            "phase7h2-additional-final-prerun",
            DateTimeOffset.UtcNow,
            "local-operator",
            "Phase 7H2 additional-instrument final pre-run gate",
            symbol,
            slashSymbol,
            securityId,
            "8",
            "Demo",
            "DemoLondon",
            "SnapshotPlusUpdates",
            "SecurityIdOnly",
            1,
            "final-readiness.json",
            "execution-plan.json",
            "operator-signoff.json",
            "",
            "PASS",
            "PASS",
            "SignedForPlanning",
            "",
            OneInstrumentAtATime: true,
            BatchExecutionAllowed: false,
            ExternalRunAuthorized: false,
            CanRunExternalSnapshot: false,
            EligibleForManualSnapshotAttempt: false,
            IsApprovedForExternalRun: false,
            SchedulerOrPolling: false,
            RuntimeShadowReplaySubmit: false,
            OrderSubmission: false,
            TradingMutation: false,
            GatewayRegistration: false,
            ApiWorkerGatewayMode: "FakeLmaxGateway",
            NoSensitiveContent: true,
            LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision.PASS);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "QQ.Production.Intraday.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
