using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyAdditionalInstrumentSnapshotPreflightTests
{
    [Theory]
    [InlineData("GBPUSD", "GBP/USD", "4002")]
    [InlineData("EURGBP", "EUR/GBP", "4003")]
    [InlineData("USDJPY", "USD/JPY", "4004")]
    [InlineData("AUDUSD", "AUD/USD", "4007")]
    public void Valid_preflight_passes_but_remains_non_executable(string symbol, string slashSymbol, string securityId)
    {
        var result = Validate(Request(symbol, slashSymbol, securityId));

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.PASS, result.FinalDecision);
        Assert.False(result.CanRunExternalSnapshot);
        Assert.False(result.IsApprovedForExternalRun);
        Assert.False(result.EligibleForManualSnapshotAttempt);
        Assert.True(result.RequiresFutureExplicitOperatorPrompt);
    }

    [Fact]
    public void Unknown_symbol_fails()
    {
        var result = Validate(Request("NZDUSD", "NZD/USD", "4999"));

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == "SymbolExistsInSafetyGateManifest" && x.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL);
    }

    [Theory]
    [InlineData("4999", "PlanningSecurityIdMatches")]
    [InlineData("TBD-LMAX-DEMO-GBPUSD", "PlanningSecurityIdMatches")]
    public void Wrong_or_placeholder_securityid_fails(string securityId, string checkName)
    {
        var result = Validate(Request("GBPUSD", "GBP/USD", securityId));

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == checkName && x.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL);
    }

    [Fact]
    public void Wrong_securityid_source_fails()
    {
        var result = Validate(Request("GBPUSD", "GBP/USD", "4002") with { SecurityIdSource = "9" });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == "SecurityIdSource8" && x.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL);
    }

    [Theory]
    [InlineData("Production", "Demo", "DemoEnvironment")]
    [InlineData("Demo", "DemoTokyo", "DemoLondonVenueProfile")]
    public void Wrong_environment_or_venue_fails(string environmentName, string venueProfileName, string checkName)
    {
        var result = Validate(Request("GBPUSD", "GBP/USD", "4002") with { EnvironmentName = environmentName, VenueProfileName = venueProfileName });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == checkName && x.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL);
    }

    [Theory]
    [InlineData(true, false, false, false, false, false, false, "AllowOrderSubmissionFalse")]
    [InlineData(false, true, false, false, false, false, false, "SchedulerEnabledFalse")]
    [InlineData(false, false, true, false, false, false, false, "SubmitToShadowReplayFalse")]
    [InlineData(false, false, false, true, false, false, false, "PersistToTradingTablesFalse")]
    [InlineData(false, false, false, false, true, false, false, "IsApprovedForExternalRunFalse")]
    [InlineData(false, false, false, false, false, true, false, "EligibleForManualSnapshotAttemptFalse")]
    [InlineData(false, false, false, false, false, false, true, "CanRunExternalSnapshotFalse")]
    public void Executable_flags_fail(
        bool allowOrderSubmission,
        bool schedulerEnabled,
        bool submitToShadowReplay,
        bool persistToTradingTables,
        bool isApprovedForExternalRun,
        bool eligibleForManualSnapshotAttempt,
        bool canRunExternalSnapshot,
        string checkName)
    {
        var request = Request("GBPUSD", "GBP/USD", "4002") with
        {
            AllowOrderSubmission = allowOrderSubmission,
            SchedulerEnabled = schedulerEnabled,
            SubmitToShadowReplay = submitToShadowReplay,
            PersistToTradingTables = persistToTradingTables,
            IsApprovedForExternalRun = isApprovedForExternalRun,
            EligibleForManualSnapshotAttempt = eligibleForManualSnapshotAttempt,
            CanRunExternalSnapshot = canRunExternalSnapshot
        };

        var result = Validate(request);

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == checkName && x.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL);
    }

    [Fact]
    public void Missing_reason_fails()
    {
        var result = Validate(Request("GBPUSD", "GBP/USD", "4002") with { Reason = "" });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == "ReasonRequired" && x.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL);
    }

    [Fact]
    public void Sensitive_content_fails()
    {
        var result = Validate(Request("GBPUSD", "GBP/USD", "4002") with { Reason = "password=sentinel" });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == "NoSensitiveContent" && x.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL);
    }

    [Theory]
    [InlineData("production authorization")]
    [InlineData("order submission")]
    [InlineData("newordersingle")]
    public void Authorization_language_fails(string reason)
    {
        var result = Validate(Request("GBPUSD", "GBP/USD", "4002") with { Reason = reason });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL, result.FinalDecision);
        Assert.Contains(result.Checks, x => x.Name == "NoTradingOrExternalAuthorizationLanguage" && x.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL);
    }

    [Fact]
    public void Aggregate_manifest_includes_all_four_instruments()
    {
        var planning = PlanningManifest();
        var safety = SafetyGateManifest(planning);
        var manifest = LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifestBuilder.FromPlanningAndSafetyGates(
            planning,
            safety,
            "planning.json",
            "safety.json",
            "local-operator",
            "Phase 6P additional instrument snapshot preflight design");
        var result = LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifestValidator.Validate(manifest);

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.PASS, result.Decision);
        Assert.Equal(4, manifest.InstrumentCount);
        Assert.Equal(4, manifest.PassCount);
        Assert.False(manifest.AnyCanRunExternalSnapshot);
        Assert.False(manifest.AnyApprovedForExternalRun);
        Assert.False(manifest.AnyEligibleForManualSnapshotAttempt);
    }

    [Fact]
    public void Aggregate_manifest_fails_if_any_instrument_missing()
    {
        var planning = PlanningManifest();
        planning = planning with { Instruments = planning.Instruments.Where(x => x.Symbol != "AUDUSD").ToArray() };
        var safety = SafetyGateManifest(PlanningManifest());
        var manifest = LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifestBuilder.FromPlanningAndSafetyGates(planning, safety, "planning.json", "safety.json", "local-operator", "Phase 6P design");

        var result = LmaxReadOnlyAdditionalInstrumentSnapshotPreflightManifestValidator.Validate(manifest);

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotPreflightDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code is "InstrumentPreflightFailed");
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase6p()
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

    private static LmaxReadOnlyAdditionalInstrumentSnapshotPreflightResult Validate(LmaxReadOnlyAdditionalInstrumentSnapshotPreflightRequest request)
        => LmaxReadOnlyAdditionalInstrumentSnapshotPreflightValidator.Validate(request, PlanningManifest(), SafetyGateManifest(PlanningManifest()));

    private static LmaxReadOnlyAdditionalInstrumentSnapshotPreflightRequest Request(string symbol, string slashSymbol, string securityId)
        => new(
            PreflightId: $"preflight-{symbol}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            RequestedByOperatorId: "local-operator",
            Reason: "Phase 6P additional instrument snapshot preflight design",
            Symbol: symbol,
            SlashSymbol: slashSymbol,
            PlanningSecurityId: securityId,
            SecurityIdSource: "8",
            EnvironmentName: "Demo",
            VenueProfileName: "DemoLondon",
            RequestMode: "SnapshotPlusUpdates",
            SymbolEncodingMode: "SecurityIdOnly",
            MarketDepth: 1,
            MaxRuntimeSeconds: 30,
            MaxWaitSeconds: 30,
            MaxEventsPerRun: 25,
            AllowExternalConnections: false,
            ConfirmDemoReadOnly: false,
            AllowOrderSubmission: false,
            SchedulerEnabled: false,
            SubmitToShadowReplay: false,
            PersistToTradingTables: false,
            IsApprovedForExternalRun: false,
            EligibleForManualSnapshotAttempt: false,
            CanRunExternalSnapshot: false,
            NoSensitiveContent: true);

    private static LmaxReadOnlyAdditionalInstrumentSafetyGateManifest SafetyGateManifest(LmaxReadOnlyInstrumentSecurityIdPlanningManifest planning)
        => LmaxReadOnlyAdditionalInstrumentSafetyGateManifestBuilder.FromPlanningManifest(planning, "planning.json");

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

    private static LmaxReadOnlyInstrumentSecurityIdPlanningEntry Entry(string symbol, string slashSymbol, string securityId)
        => new(
            Symbol: symbol,
            SlashSymbol: slashSymbol,
            PlanningSecurityId: securityId,
            SecurityIdSource: "8",
            EvidenceSource: "OfficialLmaxDocument",
            EvidenceReference: "LMAX instrument CSV",
            ConfirmationRecordId: $"record-{symbol}",
            ConfirmationRecordPath: $"artifacts/real/record-{symbol}.json",
            Decision: LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.AcceptedForPlanning,
            IsApprovedForExternalRun: false,
            EnvironmentName: "Demo",
            VenueProfileName: "DemoLondon",
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
