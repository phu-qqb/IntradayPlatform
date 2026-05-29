using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using LmaxInfra = QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Integration;

public sealed class LmaxReadOnlyRuntimeApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 07, 09, 30, 00, TimeSpan.Zero);

    [Fact]
    public async Task Health_remains_fake_lmax_and_status_is_disabled_by_default()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var health = await GetAsync<HealthDto>(client, "/health");
        var status = await GetAsync<LmaxReadOnlyRuntimeStatusDto>(client, "/lmax-readonly-runtime/status");

        Assert.Equal("FakeLmaxGateway", health.ExecutionGateway);
        Assert.False(health.LiveTradingEnabled);
        Assert.False(health.ExternalConnectionsEnabled);
        Assert.Equal("Disabled", status.Status);
        Assert.False(status.Enabled);
        Assert.Contains(status.SafetyGates, x => x.Gate == "Enabled" && !x.Passed);
    }

    [Fact]
    public async Task Run_endpoint_requires_reason_and_is_blocked_by_default()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var bad = await client.PostAsJsonAsync("/lmax-readonly-runtime/run", new { reason = "" });
        var run = await PostAsync<LmaxReadOnlyRuntimeRunResultDto>(client, "/lmax-readonly-runtime/run", new { reason = "default blocked fake runtime smoke" });

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("Disabled", run.Status);
        Assert.Contains(run.SafetyGates, x => x.Gate == "Enabled" && !x.Passed);
        Assert.Contains("fake adapter run blocked", run.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Enabled_fake_config_runs_fixture_preview_without_mutating_trading_state()
    {
        var state = SeedData.Create(Now);
        await using var factory = CreateFactory(state, enabledFake: true);
        using var client = factory.CreateClient();
        var before = SnapshotCounts(state);

        var run = await PostAsync<LmaxReadOnlyRuntimeRunResultDto>(client, "/lmax-readonly-runtime/run", new { reason = "enabled fake fixture preview", fixtureFileName = "lmax-mixed-readonly-evidence-v1.json" });
        var after = SnapshotCounts(state);

        Assert.Equal("Completed", run.Status);
        Assert.Equal("FakeInMemoryFixtureOnly", run.RunMode);
        Assert.Equal("MixedReadOnly", run.EvidenceMode);
        Assert.Equal(0, run.ExecutionReportCount);
        Assert.Equal(1, run.OrderStatusCount);
        Assert.Equal(1, run.TradeCaptureReportCount);
        Assert.Equal(0, run.ProtocolRejectCount);
        Assert.Equal(1, run.MarketDataSnapshotCount);
        Assert.Equal(3, run.InputEventCount);
        Assert.Equal(0, run.ValidationErrorCount);
        Assert.Equal(0, run.ValidationWarningCount);
        Assert.Equal(1, run.ValidationInfoCount);
        Assert.NotNull(run.EvidencePreview);
        Assert.False(run.EvidencePreview!.SubmittedToShadowReplay);
        Assert.Null(run.ReplayRunId);
        Assert.Contains("No external connection", run.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no shadow replay", run.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no trading state", run.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, after);

        var runs = await GetAsync<LmaxReadOnlyRuntimeRunSummaryDto[]>(client, "/lmax-readonly-runtime/runs?limit=5");
        Assert.Contains(runs, x => x.RunId == run.RunId && x.EvidenceMode == "MixedReadOnly");

        var loaded = await GetAsync<LmaxReadOnlyRuntimeRunResultDto>(client, $"/lmax-readonly-runtime/runs/{run.RunId}");
        Assert.Equal(run.RunId, loaded.RunId);
    }

    [Theory]
    [InlineData("lmax-readonly-empty-evidence-v1.json", "EmptyReadOnly", 0, 0, 0, 0, 0, 0)]
    [InlineData("lmax-marketdata-only-evidence-v1.json", "MarketDataOnly", 0, 0, 0, 0, 1, 1)]
    [InlineData("lmax-tradecapture-only-evidence-v1.json", "TradeCaptureOnly", 0, 0, 1, 0, 0, 1)]
    [InlineData("lmax-orderstatus-only-evidence-v1.json", "OrderStatusOnly", 0, 1, 0, 0, 0, 1)]
    [InlineData("lmax-protocolreject-only-evidence-v1.json", "ProtocolRejectOnly", 0, 0, 0, 1, 0, 1)]
    [InlineData("lmax-mixed-readonly-evidence-v1.json", "MixedReadOnly", 0, 1, 1, 0, 1, 3)]
    [InlineData("lmax-fix-lifecycle-evidence-v1.json", "SyntheticLifecycle", 1, 1, 1, 0, 0, 3)]
    public async Task Fake_enabled_endpoint_previews_each_supported_fixture_mode(
        string fixtureFileName,
        string evidenceMode,
        int executionReports,
        int orderStatuses,
        int tradeCaptureReports,
        int protocolRejects,
        int marketData,
        int totalEvents)
    {
        await using var factory = CreateFactory(enabledFake: true);
        using var client = factory.CreateClient();

        var run = await PostAsync<LmaxReadOnlyRuntimeRunResultDto>(client, "/lmax-readonly-runtime/run", new { reason = $"preview {fixtureFileName}", fixtureFileName });

        Assert.Equal("Completed", run.Status);
        Assert.Equal(evidenceMode, run.EvidenceMode);
        Assert.Equal(executionReports, run.ExecutionReportCount);
        Assert.Equal(orderStatuses, run.OrderStatusCount);
        Assert.Equal(tradeCaptureReports, run.TradeCaptureReportCount);
        Assert.Equal(protocolRejects, run.ProtocolRejectCount);
        Assert.Equal(marketData, run.MarketDataSnapshotCount);
        Assert.Equal(totalEvents, run.InputEventCount);
        Assert.Equal(0, run.ValidationErrorCount);
        Assert.Equal(0, run.ValidationWarningCount);
        Assert.Equal(1, run.ValidationInfoCount);
        Assert.False(run.EvidencePreview!.SubmittedToShadowReplay);
        Assert.Null(run.ReplayRunId);
    }

    [Theory]
    [InlineData("../lmax-mixed-readonly-evidence-v1.json")]
    [InlineData("nested/lmax-mixed-readonly-evidence-v1.json")]
    [InlineData("C:\\temp\\lmax-mixed-readonly-evidence-v1.json")]
    public async Task Fixture_selector_rejects_path_traversal_and_absolute_paths(string fixtureFileName)
    {
        await using var factory = CreateFactory(enabledFake: true);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/lmax-readonly-runtime/run", new { reason = "bad fixture", fixtureFileName });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Fixture_selector_rejects_unknown_file()
    {
        await using var factory = CreateFactory(enabledFake: true);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/lmax-readonly-runtime/run", new { reason = "unknown fixture", fixtureFileName = "missing.json" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_to_shadow_replay_remains_blocked_in_phase_3()
    {
        await using var factory = CreateFactory(enabledFake: true, submitToShadowReplay: true);
        using var client = factory.CreateClient();

        var run = await PostAsync<LmaxReadOnlyRuntimeRunResultDto>(client, "/lmax-readonly-runtime/run", new { reason = "submit stays blocked", fixtureFileName = "lmax-tradecapture-only-evidence-v1.json" });

        Assert.Equal("Blocked", run.Status);
        Assert.Contains(run.SafetyGates, x => x.Gate == "SubmitToShadowReplay" && !x.Passed);
        Assert.Equal(0, run.ObservationCount);
    }

    [Fact]
    public async Task Fake_transport_preview_endpoint_is_blocked_by_default()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var run = await PostAsync<LmaxReadOnlyRuntimeFakeTransportPreviewDto>(client, "/lmax-readonly-runtime/fake-transport-preview", new
        {
            reason = "default blocked fake transport preview",
            scenario = "MixedReadOnly"
        });

        Assert.Equal("Disabled", run.Status);
        Assert.Equal("FakeTransportPreview", run.RunMode);
        Assert.Equal("MixedReadOnly", run.Scenario);
        Assert.False(run.SubmitToShadowReplay);
        Assert.Equal(0, run.TotalEventCount);
        Assert.Null(run.EvidencePreview);
        Assert.Contains(run.SafetyGates, x => x.Gate == "Enabled" && !x.Passed);
    }

    [Fact]
    public async Task Fake_transport_preview_enabled_config_runs_mixed_readonly_preview_without_mutating_trading_state()
    {
        var state = SeedData.Create(Now);
        await using var factory = CreateFactory(state, enabledFake: true, fakeTransportPreview: true);
        using var client = factory.CreateClient();
        var before = SnapshotCounts(state);

        var run = await PostAsync<LmaxReadOnlyRuntimeFakeTransportPreviewDto>(client, "/lmax-readonly-runtime/fake-transport-preview", new
        {
            reason = "enabled fake transport preview",
            scenario = "MixedReadOnly",
            maxEvents = 20,
            dryRun = true,
            submitToShadowReplay = false
        });
        var after = SnapshotCounts(state);

        Assert.Equal("Completed", run.Status);
        Assert.Equal("FakeTransportPreview", run.RunMode);
        Assert.Equal("MixedReadOnly", run.Scenario);
        Assert.Equal("MixedReadOnly", run.EvidenceMode);
        Assert.Equal("RuntimeFakeTransport", run.Source);
        Assert.Equal("FakeRuntimePreview", run.CaptureMode);
        Assert.Equal(1, run.MarketDataSnapshotCount);
        Assert.Equal(1, run.TradeCaptureReportCount);
        Assert.Equal(1, run.OrderStatusReportCount);
        Assert.Equal(1, run.ProtocolRejectCount);
        Assert.Equal(0, run.SessionWarningCount);
        Assert.Equal(0, run.SessionErrorCount);
        Assert.Equal(4, run.TotalEventCount);
        Assert.Equal(0, run.ExecutionReportCount);
        Assert.Equal(1, run.OrderStatusCount);
        Assert.Equal(1, run.TradeCaptureReportEvidenceCount);
        Assert.Equal(1, run.ProtocolRejectEvidenceCount);
        Assert.Equal(1, run.MarketDataEvidenceCount);
        Assert.Equal(0, run.ValidationErrorCount);
        Assert.Equal(0, run.ValidationWarningCount);
        Assert.Equal(1, run.ValidationInfoCount);
        Assert.True(run.NoSensitiveContent);
        Assert.False(run.SubmitToShadowReplay);
        Assert.NotNull(run.EvidencePreview);
        Assert.Equal("lmax-fix-lifecycle-evidence-v1", run.EvidencePreview!.SchemaVersion);
        Assert.True(run.EvidencePreview.Sanitized);
        Assert.False(run.EvidencePreview.ContainsRawFix);
        Assert.Contains("nothing was submitted to shadow replay", run.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, after);

        var runs = await GetAsync<LmaxReadOnlyRuntimeRunSummaryDto[]>(client, "/lmax-readonly-runtime/runs?limit=5");
        Assert.Contains(runs, x => x.RunId == run.RunId && x.RunMode == "FakeTransportPreview" && x.EvidenceMode == "MixedReadOnly");
    }

    [Theory]
    [InlineData("EmptyReadOnly", "EmptyReadOnly", 0, 0, 0, 0, 0, 0, 0)]
    [InlineData("MarketDataOnly", "MarketDataOnly", 1, 0, 0, 0, 0, 0, 1)]
    [InlineData("TradeCaptureOnly", "TradeCaptureOnly", 0, 1, 0, 0, 0, 0, 1)]
    [InlineData("OrderStatusOnly", "OrderStatusOnly", 0, 0, 1, 0, 0, 0, 1)]
    [InlineData("ProtocolRejectOnly", "ProtocolRejectOnly", 0, 0, 0, 1, 0, 0, 1)]
    [InlineData("MixedReadOnly", "MixedReadOnly", 1, 1, 1, 1, 0, 0, 4)]
    [InlineData("WarningOnly", "EmptyReadOnly", 0, 0, 0, 0, 1, 0, 1)]
    [InlineData("ErrorOnly", "EmptyReadOnly", 0, 0, 0, 0, 0, 1, 1)]
    public async Task Fake_transport_preview_supports_predefined_no_network_scenarios(
        string scenario,
        string evidenceMode,
        int marketData,
        int tradeCapture,
        int orderStatus,
        int protocolReject,
        int warning,
        int error,
        int totalEvents)
    {
        await using var factory = CreateFactory(enabledFake: true, fakeTransportPreview: true);
        using var client = factory.CreateClient();

        var run = await PostAsync<LmaxReadOnlyRuntimeFakeTransportPreviewDto>(client, "/lmax-readonly-runtime/fake-transport-preview", new
        {
            reason = $"preview {scenario}",
            scenario
        });

        Assert.Equal("Completed", run.Status);
        Assert.Equal(scenario, run.Scenario);
        Assert.Equal(evidenceMode, run.EvidenceMode);
        Assert.Equal(marketData, run.MarketDataSnapshotCount);
        Assert.Equal(tradeCapture, run.TradeCaptureReportCount);
        Assert.Equal(orderStatus, run.OrderStatusReportCount);
        Assert.Equal(protocolReject, run.ProtocolRejectCount);
        Assert.Equal(warning, run.SessionWarningCount);
        Assert.Equal(error, run.SessionErrorCount);
        Assert.Equal(totalEvents, run.TotalEventCount);
        Assert.Equal(0, run.ValidationErrorCount);
        Assert.True(run.NoSensitiveContent);
        Assert.False(run.SubmitToShadowReplay);
    }

    [Fact]
    public async Task Fake_transport_preview_rejects_missing_reason_unknown_scenario_and_shadow_submit()
    {
        await using var factory = CreateFactory(enabledFake: true, fakeTransportPreview: true);
        using var client = factory.CreateClient();

        var missingReason = await client.PostAsJsonAsync("/lmax-readonly-runtime/fake-transport-preview", new { reason = "", scenario = "MixedReadOnly" });
        var unknownScenario = await client.PostAsJsonAsync("/lmax-readonly-runtime/fake-transport-preview", new { reason = "unknown scenario", scenario = "NewOrderSingle" });
        var shadowSubmit = await client.PostAsJsonAsync("/lmax-readonly-runtime/fake-transport-preview", new { reason = "shadow submit forbidden", scenario = "MixedReadOnly", submitToShadowReplay = true });

        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownScenario.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, shadowSubmit.StatusCode);
    }

    [Fact]
    public async Task External_run_intent_validate_endpoint_requires_reason()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var missingReason = await client.PostAsJsonAsync("/lmax-readonly-runtime/external-run-intent/validate", new
        {
            reason = "",
            environmentName = "Demo",
            venueProfileName = "DemoLondon",
            credentialProfileName = "LmaxDemoReadOnlyProfile",
            runMode = "FutureExternalReadOnlyManual"
        });

        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
    }

    [Fact]
    public async Task External_run_intent_validate_blocks_future_manual_run_without_starting_session_or_mutating_state()
    {
        var state = SeedData.Create(Now);
        await using var factory = CreateFactory(state);
        using var client = factory.CreateClient();
        var before = SnapshotCounts(state);
        var shadowBefore = (state.LmaxShadowReplayRuns.Count, state.LmaxShadowObservations.Count);

        var health = await GetAsync<HealthDto>(client, "/health");
        var result = await PostAsync<LmaxReadOnlyRuntimeExternalRunIntentValidationDto>(client, "/lmax-readonly-runtime/external-run-intent/validate", new
        {
            reason = "validate future external manual intent only",
            environmentName = "Demo",
            venueProfileName = "DemoLondon",
            credentialProfileName = "LmaxDemoReadOnlyProfile",
            runMode = "FutureExternalReadOnlyManual",
            dryRun = true,
            maxRuntimeSeconds = 30,
            maxEventsPerRun = 100,
            requestedEvidencePreviewOnly = true,
            submitToShadowReplay = false,
            allowExternalConnections = false,
            allowCredentialUse = false,
            allowOrderSubmission = false,
            schedulerEnabled = false,
            persistToTradingTables = false
        });
        var after = SnapshotCounts(state);
        var shadowAfter = (state.LmaxShadowReplayRuns.Count, state.LmaxShadowObservations.Count);

        Assert.Equal("FakeLmaxGateway", health.ExecutionGateway);
        Assert.Equal("Blocked", result.Status);
        Assert.Equal("FutureExternalReadOnlyManual", result.RunMode);
        Assert.False(result.CanStartSession);
        Assert.False(result.SessionStarted);
        Assert.False(result.ExternalConnectionAttempted);
        Assert.False(result.CredentialReadAttempted);
        Assert.False(result.ShadowReplaySubmitAttempted);
        Assert.False(result.TradingMutationAttempted);
        Assert.Equal("Demo", result.EnvironmentName);
        Assert.Equal("DemoLondon", result.VenueProfileName);
        Assert.Equal("LmaxDemoReadOnlyProfile", result.CredentialProfileName);
        Assert.Contains(result.ValidationIssues, x => x.Code == "Phase4ExternalRunImplementationNotStarted");
        Assert.Contains(result.SafetyGates, x => x.Gate == "Phase4ExternalRunImplementationNotStarted" && !x.Passed);
        Assert.Contains(result.ValidationIssues, x => x.Code == "CredentialResolverDisabled");
        Assert.Contains("No external session was started", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, after);
        Assert.Equal(shadowBefore, shadowAfter);
    }

    [Fact]
    public async Task External_run_intent_validate_only_never_starts_session()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var result = await PostAsync<LmaxReadOnlyRuntimeExternalRunIntentValidationDto>(client, "/lmax-readonly-runtime/external-run-intent/validate", new
        {
            reason = "validate only request",
            environmentName = "Demo",
            venueProfileName = "DemoLondon",
            credentialProfileName = "LmaxDemoReadOnlyProfile",
            runMode = "ValidateOnly"
        });

        Assert.Equal("Blocked", result.Status);
        Assert.Equal("ValidateOnly", result.RunMode);
        Assert.False(result.CanStartSession);
        Assert.False(result.SessionStarted);
        Assert.False(result.ExternalConnectionAttempted);
        Assert.False(result.CredentialReadAttempted);
        Assert.False(result.ShadowReplaySubmitAttempted);
        Assert.False(result.TradingMutationAttempted);
        Assert.DoesNotContain(result.ValidationIssues, x => x.Code == "Phase4ExternalRunImplementationNotStarted");
        Assert.Contains(result.ValidationIssues, x => x.Code == "CredentialResolverDisabled");
    }

    [Theory]
    [InlineData("UnknownVenue", "VenueProfileUnknown")]
    [InlineData("Production", "VenueProfileProductionBlocked")]
    [InlineData("Uat", "VenueProfileUatBlocked")]
    [InlineData("AllowExternalConnections", "ExternalConnectionBlocked")]
    [InlineData("AllowCredentialUse", "CredentialUseBlocked")]
    [InlineData("AllowOrderSubmission", "OrderSubmissionForbidden")]
    [InlineData("SchedulerEnabled", "SchedulerForbidden")]
    [InlineData("PersistToTradingTables", "TradingTablePersistenceForbidden")]
    [InlineData("SubmitToShadowReplay", "ShadowReplaySubmitDeferred")]
    [InlineData("InvalidRuntime", "MaxRuntimeSecondsOutOfRange")]
    [InlineData("InvalidEvents", "MaxEventsPerRunOutOfRange")]
    public async Task External_run_intent_validate_endpoint_blocks_unsafe_conditions(string condition, string expectedCode)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var result = await PostAsync<LmaxReadOnlyRuntimeExternalRunIntentValidationDto>(client, "/lmax-readonly-runtime/external-run-intent/validate", new
        {
            reason = $"validate unsafe condition {condition}",
            environmentName = condition == "Production" ? "Production" : condition == "Uat" ? "UAT" : "Demo",
            venueProfileName = condition switch
            {
                "UnknownVenue" => "UnknownFutureVenue",
                "Production" => "Production",
                "Uat" => "Uat",
                _ => "DemoLondon"
            },
            credentialProfileName = "LmaxDemoReadOnlyProfile",
            runMode = "PreviewOnly",
            allowExternalConnections = condition == "AllowExternalConnections",
            allowCredentialUse = condition == "AllowCredentialUse",
            allowOrderSubmission = condition == "AllowOrderSubmission",
            schedulerEnabled = condition == "SchedulerEnabled",
            persistToTradingTables = condition == "PersistToTradingTables",
            submitToShadowReplay = condition == "SubmitToShadowReplay",
            maxRuntimeSeconds = condition == "InvalidRuntime" ? 0 : 30,
            maxEventsPerRun = condition == "InvalidEvents" ? 0 : 100
        });

        Assert.Equal("Blocked", result.Status);
        Assert.False(result.CanStartSession);
        Assert.False(result.SessionStarted);
        Assert.Contains(result.ValidationIssues, x => x.Code == expectedCode);
    }

    [Fact]
    public async Task External_dry_run_report_blocks_future_manual_run_without_starting_session_or_mutating_state()
    {
        var state = SeedData.Create(Now);
        await using var factory = CreateFactory(state);
        using var client = factory.CreateClient();
        var before = SnapshotCounts(state);
        var shadowBefore = (state.LmaxShadowReplayRuns.Count, state.LmaxShadowObservations.Count);

        var report = await PostAsync<LmaxReadOnlyRuntimeExternalDryRunReportDto>(client, "/lmax-readonly-runtime/external-run-intent/dry-run-report", new
        {
            reason = "dry-run report for future external manual intent only",
            environmentName = "Demo",
            venueProfileName = "DemoLondon",
            credentialProfileName = "LmaxDemoReadOnlyProfile",
            runMode = "FutureExternalReadOnlyManual",
            dryRun = true,
            maxRuntimeSeconds = 30,
            maxEventsPerRun = 100,
            requestedEvidencePreviewOnly = true,
            submitToShadowReplay = false,
            allowExternalConnections = false,
            allowCredentialUse = false,
            allowOrderSubmission = false,
            schedulerEnabled = false,
            persistToTradingTables = false
        });
        var after = SnapshotCounts(state);
        var shadowAfter = (state.LmaxShadowReplayRuns.Count, state.LmaxShadowObservations.Count);

        Assert.Equal("Blocked", report.ExpectedOutcome);
        Assert.Equal("FutureExternalReadOnlyManual", report.RunMode);
        Assert.False(report.CanStartSession);
        Assert.False(report.SessionStarted);
        Assert.False(report.ExternalConnectionAttempted);
        Assert.False(report.CredentialReadAttempted);
        Assert.False(report.ShadowReplaySubmitAttempted);
        Assert.False(report.TradingMutationAttempted);
        Assert.True(report.NoSensitiveContent);
        Assert.Contains(report.SafetyGates, x => x.Gate == "Phase4ExternalRunImplementationNotStarted" && !x.Passed);
        Assert.Contains(report.SafetyGates, x => x.Gate == "CredentialResolverDisabled" && !x.Passed);
        Assert.Contains(report.SafetyGates, x => x.Gate == "GuardedTransportImplementationDisabled" && !x.Passed);
        Assert.Contains(report.SafetyGates, x => x.Gate == "ExternalSessionImplementationStarted" && !x.Passed);
        Assert.False(report.VenueProfile.IsActive);
        Assert.False(report.VenueProfile.IsExternalConnectionAllowed);
        Assert.False(report.CredentialProfile.CredentialReadImplemented);
        Assert.False(report.CredentialProfile.SensitiveMaterialReturned);
        Assert.False(report.GuardedTransport.NetworkTransportImplemented);
        Assert.False(report.GuardedTransport.SocketActivation);
        Assert.False(report.ExternalSessionSkeleton.SocketActivation);
        Assert.Equal(before, after);
        Assert.Equal(shadowBefore, shadowAfter);
    }

    [Fact]
    public async Task External_dry_run_report_requires_reason()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var missingReason = await client.PostAsJsonAsync("/lmax-readonly-runtime/external-run-intent/dry-run-report", new
        {
            reason = "",
            environmentName = "Demo",
            venueProfileName = "DemoLondon",
            credentialProfileName = "LmaxDemoReadOnlyProfile",
            runMode = "FutureExternalReadOnlyManual"
        });

        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
    }

    [Theory]
    [InlineData("AllowExternalConnections", "ExternalConnectionBlocked")]
    [InlineData("AllowCredentialUse", "CredentialUseBlocked")]
    [InlineData("AllowOrderSubmission", "OrderSubmissionForbidden")]
    [InlineData("SchedulerEnabled", "SchedulerForbidden")]
    [InlineData("PersistToTradingTables", "TradingTablePersistenceForbidden")]
    [InlineData("SubmitToShadowReplay", "ShadowReplaySubmitDeferred")]
    public async Task External_dry_run_report_surfaces_unsafe_flags(string condition, string expectedCode)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var report = await PostAsync<LmaxReadOnlyRuntimeExternalDryRunReportDto>(client, "/lmax-readonly-runtime/external-run-intent/dry-run-report", new
        {
            reason = $"dry-run report unsafe condition {condition}",
            environmentName = "Demo",
            venueProfileName = "DemoLondon",
            credentialProfileName = "LmaxDemoReadOnlyProfile",
            runMode = "PreviewOnly",
            allowExternalConnections = condition == "AllowExternalConnections",
            allowCredentialUse = condition == "AllowCredentialUse",
            allowOrderSubmission = condition == "AllowOrderSubmission",
            schedulerEnabled = condition == "SchedulerEnabled",
            persistToTradingTables = condition == "PersistToTradingTables",
            submitToShadowReplay = condition == "SubmitToShadowReplay"
        });

        Assert.Equal("Blocked", report.ExpectedOutcome);
        Assert.False(report.CanStartSession);
        Assert.Contains(report.SafetyGates, x => x.Gate == expectedCode && !x.Passed);
    }

    [Fact]
    public async Task External_signoff_validate_accepts_metadata_but_cannot_authorize_execution_or_mutate_state()
    {
        var state = SeedData.Create(Now);
        await using var factory = CreateFactory(state);
        using var client = factory.CreateClient();
        var before = SnapshotCounts(state);
        var shadowBefore = (state.LmaxShadowReplayRuns.Count, state.LmaxShadowObservations.Count);

        var report = await PostAsync<LmaxReadOnlyRuntimeExternalDryRunReportDto>(client, "/lmax-readonly-runtime/external-run-intent/dry-run-report", new
        {
            reason = "dry-run report for signoff validation",
            environmentName = "Demo",
            venueProfileName = "DemoLondon",
            credentialProfileName = "LmaxDemoReadOnlyProfile",
            runMode = "FutureExternalReadOnlyManual"
        });

        var signoff = await PostAsync<LmaxReadOnlyRuntimeExternalSignoffDto>(client, "/lmax-readonly-runtime/external-run-intent/signoff/validate", new
        {
            reason = "manual signoff metadata validation only",
            dryRunReportId = report.ReportId,
            intentId = report.IntentValidation.IntentId,
            requestedByOperatorId = report.RequestedByOperatorId,
            signedByOperatorId = "risk-approver",
            signoffRole = "Approver",
            confirmsReadOnlyIntent = true,
            confirmsNoOrderSubmission = true,
            confirmsNoTradingMutation = true,
            confirmsNoScheduler = true,
            confirmsNoShadowReplaySubmit = true,
            confirmsNoCredentialExposure = true,
            confirmsDemoOnly = true,
            confirmsDryRunReportReviewed = true,
            dryRunReportCanStartSession = report.CanStartSession,
            dryRunReportSafetyMarkers = report.SafetyGates.Select(x => x.Gate).ToArray(),
            decision = "Signed"
        });
        var after = SnapshotCounts(state);
        var shadowAfter = (state.LmaxShadowReplayRuns.Count, state.LmaxShadowObservations.Count);

        Assert.Equal("NotExecutable", signoff.Status);
        Assert.Equal("Signed", signoff.Decision);
        Assert.False(signoff.CanAuthorizeExecution);
        Assert.True(signoff.ExecutionStillBlocked);
        Assert.False(signoff.SessionStarted);
        Assert.False(signoff.ExternalConnectionAttempted);
        Assert.False(signoff.CredentialReadAttempted);
        Assert.False(signoff.ShadowReplaySubmitAttempted);
        Assert.False(signoff.TradingMutationAttempted);
        Assert.Contains(signoff.SafetyGates, x => x.Gate == "Phase4ExternalRunImplementationNotStarted" && !x.Passed);
        Assert.Contains(signoff.SafetyGates, x => x.Gate == "CredentialResolverDisabled" && !x.Passed);
        Assert.Contains(signoff.SafetyGates, x => x.Gate == "GuardedTransportImplementationDisabled" && !x.Passed);
        Assert.Equal(before, after);
        Assert.Equal(shadowBefore, shadowAfter);
    }

    [Fact]
    public async Task External_signoff_validate_requires_reason()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var missingReason = await client.PostAsJsonAsync("/lmax-readonly-runtime/external-run-intent/signoff/validate", new
        {
            reason = "",
            signedByOperatorId = "approver"
        });

        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
    }

    [Theory]
    [InlineData("MissingSigner", "SignedByOperatorIdRequired")]
    [InlineData("MissingReport", "IntentOrDryRunReportRequired")]
    [InlineData("MissingAttestation", "ConfirmsNoOrderSubmissionRequired")]
    [InlineData("SelfSignoff", "MakerCheckerSelfSignoffBlocked")]
    public async Task External_signoff_validate_blocks_invalid_metadata(string condition, string expectedCode)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var signoff = await PostAsync<LmaxReadOnlyRuntimeExternalSignoffDto>(client, "/lmax-readonly-runtime/external-run-intent/signoff/validate", new
        {
            reason = $"signoff invalid condition {condition}",
            dryRunReportId = condition == "MissingReport" ? (Guid?)null : Guid.NewGuid(),
            intentId = condition == "MissingReport" ? Guid.Empty : Guid.NewGuid(),
            requestedByOperatorId = condition == "SelfSignoff" ? "same-operator" : "requesting-operator",
            signedByOperatorId = condition == "MissingSigner" ? "" : condition == "SelfSignoff" ? "same-operator" : "approver",
            signoffRole = "Approver",
            confirmsReadOnlyIntent = true,
            confirmsNoOrderSubmission = condition != "MissingAttestation",
            confirmsNoTradingMutation = true,
            confirmsNoScheduler = true,
            confirmsNoShadowReplaySubmit = true,
            confirmsNoCredentialExposure = true,
            confirmsDemoOnly = true,
            confirmsDryRunReportReviewed = true,
            dryRunReportCanStartSession = false,
            dryRunReportSafetyMarkers = new[]
            {
                "Phase4ExternalRunImplementationNotStarted",
                "CredentialResolverDisabled",
                "GuardedTransportImplementationDisabled"
            },
            decision = "Signed"
        });

        Assert.Equal("Invalid", signoff.Status);
        Assert.False(signoff.CanAuthorizeExecution);
        Assert.True(signoff.ExecutionStillBlocked);
        Assert.Contains(signoff.ValidationIssues, x => x.Code == expectedCode);
    }

    [Fact]
    public async Task External_pre_activation_audit_validate_accepts_metadata_but_cannot_authorize_execution_or_mutate_state()
    {
        var state = SeedData.Create(Now);
        await using var factory = CreateFactory(state);
        using var client = factory.CreateClient();
        var before = SnapshotCounts(state);
        var shadowBefore = (state.LmaxShadowReplayRuns.Count, state.LmaxShadowObservations.Count);

        var report = await PostAsync<LmaxReadOnlyRuntimeExternalDryRunReportDto>(client, "/lmax-readonly-runtime/external-run-intent/dry-run-report", new
        {
            reason = "dry-run report for pre-activation audit validation",
            environmentName = "Demo",
            venueProfileName = "DemoLondon",
            credentialProfileName = "LmaxDemoReadOnlyProfile",
            runMode = "FutureExternalReadOnlyManual"
        });
        var signoff = await PostAsync<LmaxReadOnlyRuntimeExternalSignoffDto>(client, "/lmax-readonly-runtime/external-run-intent/signoff/validate", new
        {
            reason = "manual signoff metadata validation for pre-activation audit",
            dryRunReportId = report.ReportId,
            intentId = report.IntentValidation.IntentId,
            requestedByOperatorId = report.RequestedByOperatorId,
            signedByOperatorId = "risk-approver",
            signoffRole = "Approver",
            confirmsReadOnlyIntent = true,
            confirmsNoOrderSubmission = true,
            confirmsNoTradingMutation = true,
            confirmsNoScheduler = true,
            confirmsNoShadowReplaySubmit = true,
            confirmsNoCredentialExposure = true,
            confirmsDemoOnly = true,
            confirmsDryRunReportReviewed = true,
            dryRunReportCanStartSession = report.CanStartSession,
            dryRunReportSafetyMarkers = report.SafetyGates.Select(x => x.Gate).ToArray(),
            decision = "Signed"
        });

        var audit = await PostAsync<LmaxReadOnlyRuntimeExternalPreActivationAuditDto>(client, "/lmax-readonly-runtime/external-run-intent/pre-activation-audit/validate", new
        {
            reason = "pre-activation audit metadata validation only",
            requestedByOperatorId = report.RequestedByOperatorId,
            reviewedByOperatorId = "audit-reviewer",
            signedByOperatorId = signoff.SignedByOperatorId,
            intentId = report.IntentValidation.IntentId,
            dryRunReportId = report.ReportId,
            signoffId = signoff.SignoffId,
            dryRunReportCanStartSession = report.CanStartSession,
            signoffCanAuthorizeExecution = signoff.CanAuthorizeExecution,
            signoffExecutionStillBlocked = signoff.ExecutionStillBlocked,
            sessionStarted = false,
            externalConnectionAttempted = false,
            credentialReadAttempted = false,
            shadowReplaySubmitAttempted = false,
            tradingMutationAttempted = false,
            stableBlockers = report.SafetyGates.Select(x => x.Gate).Concat(signoff.SafetyGates.Select(x => x.Gate)).Distinct().ToArray(),
            dryRunReportReviewed = true,
            signoffReviewed = true
        });
        var after = SnapshotCounts(state);
        var shadowAfter = (state.LmaxShadowReplayRuns.Count, state.LmaxShadowObservations.Count);

        Assert.Equal("NotExecutable", audit.Status);
        Assert.Equal("NotExecutable", audit.FinalOutcome);
        Assert.False(audit.CanAuthorizeExecution);
        Assert.True(audit.ExecutionStillBlocked);
        Assert.False(audit.SessionStarted);
        Assert.False(audit.ExternalConnectionAttempted);
        Assert.False(audit.CredentialReadAttempted);
        Assert.False(audit.ShadowReplaySubmitAttempted);
        Assert.False(audit.TradingMutationAttempted);
        Assert.True(audit.NoSensitiveContent);
        Assert.Contains(audit.SafetyGates, x => x.Gate == "Phase4ExternalRunImplementationNotStarted" && !x.Passed);
        Assert.Contains(audit.SafetyGates, x => x.Gate == "CredentialResolverDisabled" && !x.Passed);
        Assert.Contains(audit.SafetyGates, x => x.Gate == "GuardedTransportImplementationDisabled" && !x.Passed);
        Assert.Equal(before, after);
        Assert.Equal(shadowBefore, shadowAfter);
    }

    [Fact]
    public async Task External_pre_activation_audit_validate_requires_reason()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var missingReason = await client.PostAsJsonAsync("/lmax-readonly-runtime/external-run-intent/pre-activation-audit/validate", new
        {
            reason = "",
            intentId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
    }

    [Theory]
    [InlineData("MissingIntent", "IntentSummaryRequired")]
    [InlineData("MissingDryRunReport", "DryRunReportSummaryRequired")]
    [InlineData("MissingSignoff", "SignoffSummaryRequired")]
    [InlineData("MissingBlocker", "CredentialResolverDisabled")]
    [InlineData("DryRunCanStart", "DryRunReportMustRemainBlocked")]
    [InlineData("SignoffCanAuthorize", "SignoffCannotAuthorizeExecution")]
    [InlineData("ExecutionUnblocked", "SignoffExecutionStillBlockedRequired")]
    [InlineData("SessionStarted", "SessionStartedMustRemainFalse")]
    [InlineData("ExternalConnectionAttempted", "ExternalConnectionAttemptedMustRemainFalse")]
    [InlineData("CredentialReadAttempted", "CredentialReadAttemptedMustRemainFalse")]
    [InlineData("ShadowReplaySubmitAttempted", "ShadowReplaySubmitAttemptedMustRemainFalse")]
    [InlineData("TradingMutationAttempted", "TradingMutationAttemptedMustRemainFalse")]
    public async Task External_pre_activation_audit_validate_blocks_invalid_metadata(string condition, string expectedCode)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var audit = await PostAsync<LmaxReadOnlyRuntimeExternalPreActivationAuditDto>(client, "/lmax-readonly-runtime/external-run-intent/pre-activation-audit/validate", new
        {
            reason = $"pre-activation audit invalid condition {condition}",
            requestedByOperatorId = "requesting-operator",
            reviewedByOperatorId = "audit-reviewer",
            signedByOperatorId = "risk-approver",
            intentId = condition == "MissingIntent" ? Guid.Empty : Guid.NewGuid(),
            dryRunReportId = condition == "MissingDryRunReport" ? Guid.Empty : Guid.NewGuid(),
            signoffId = condition == "MissingSignoff" ? Guid.Empty : Guid.NewGuid(),
            dryRunReportCanStartSession = condition == "DryRunCanStart",
            signoffCanAuthorizeExecution = condition == "SignoffCanAuthorize",
            signoffExecutionStillBlocked = condition != "ExecutionUnblocked",
            sessionStarted = condition == "SessionStarted",
            externalConnectionAttempted = condition == "ExternalConnectionAttempted",
            credentialReadAttempted = condition == "CredentialReadAttempted",
            shadowReplaySubmitAttempted = condition == "ShadowReplaySubmitAttempted",
            tradingMutationAttempted = condition == "TradingMutationAttempted",
            stableBlockers = condition == "MissingBlocker"
                ? new[] { "Phase4ExternalRunImplementationNotStarted", "GuardedTransportImplementationDisabled" }
                : new[] { "Phase4ExternalRunImplementationNotStarted", "CredentialResolverDisabled", "GuardedTransportImplementationDisabled" },
            dryRunReportReviewed = true,
            signoffReviewed = true
        });

        Assert.Equal("Invalid", audit.Status);
        Assert.False(audit.CanAuthorizeExecution);
        Assert.True(audit.ExecutionStillBlocked);
        Assert.Contains(audit.ValidationIssues, x => x.Code == expectedCode);
    }

    [Fact]
    public async Task External_readiness_snapshot_returns_not_executable_and_does_not_mutate_state()
    {
        var state = SeedData.Create(Now);
        await using var factory = CreateFactory(state);
        using var client = factory.CreateClient();
        var before = SnapshotCounts(state);
        var shadowBefore = (state.LmaxShadowReplayRuns.Count, state.LmaxShadowObservations.Count);

        var snapshot = await PostAsync<LmaxReadOnlyRuntimeExternalReadinessSnapshotDto>(client, "/lmax-readonly-runtime/external-run-intent/readiness-snapshot", new
        {
            reason = "readiness snapshot validation only",
            environmentName = "Demo",
            venueProfileName = "DemoLondon",
            credentialProfileName = "LmaxDemoReadOnlyProfile",
            runMode = "FutureExternalReadOnlyManual"
        });
        var after = SnapshotCounts(state);
        var shadowAfter = (state.LmaxShadowReplayRuns.Count, state.LmaxShadowObservations.Count);

        Assert.Equal("NotExecutable", snapshot.Status);
        Assert.Equal("NotExecutable", snapshot.FinalDecision);
        Assert.False(snapshot.CanStartSession);
        Assert.False(snapshot.SessionStarted);
        Assert.False(snapshot.ExternalConnectionAttempted);
        Assert.False(snapshot.CredentialReadAttempted);
        Assert.False(snapshot.ShadowReplaySubmitAttempted);
        Assert.False(snapshot.TradingMutationAttempted);
        Assert.True(snapshot.NoSensitiveContent);
        Assert.Contains(snapshot.StableBlockers, x => x == "Phase4ExternalRunImplementationNotStarted");
        Assert.Contains(snapshot.StableBlockers, x => x == "CredentialResolverDisabled");
        Assert.Contains(snapshot.StableBlockers, x => x == "GuardedTransportImplementationDisabled");
        Assert.False(snapshot.DryRun.VenueProfile.IsActive);
        Assert.False(snapshot.DryRun.CredentialProfile.CredentialReadImplemented);
        Assert.False(snapshot.DryRun.GuardedTransport.NetworkTransportImplemented);
        Assert.False(snapshot.DryRun.ExternalSessionSkeleton.SocketActivation);
        Assert.Equal(before, after);
        Assert.Equal(shadowBefore, shadowAfter);
    }

    [Fact]
    public async Task External_readiness_snapshot_requires_reason()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var missingReason = await client.PostAsJsonAsync("/lmax-readonly-runtime/external-run-intent/readiness-snapshot", new
        {
            reason = "",
            environmentName = "Demo",
            venueProfileName = "DemoLondon"
        });

        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
    }

    [Theory]
    [InlineData("AllowExternalConnections", "ExternalConnectionBlocked")]
    [InlineData("AllowCredentialUse", "CredentialUseBlocked")]
    [InlineData("AllowOrderSubmission", "OrderSubmissionForbidden")]
    [InlineData("SchedulerEnabled", "SchedulerForbidden")]
    [InlineData("PersistToTradingTables", "TradingTablePersistenceForbidden")]
    [InlineData("SubmitToShadowReplay", "ShadowReplaySubmitDeferred")]
    public async Task External_readiness_snapshot_surfaces_unsafe_flags(string condition, string expectedCode)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var snapshot = await PostAsync<LmaxReadOnlyRuntimeExternalReadinessSnapshotDto>(client, "/lmax-readonly-runtime/external-run-intent/readiness-snapshot", new
        {
            reason = $"readiness snapshot unsafe condition {condition}",
            environmentName = "Demo",
            venueProfileName = "DemoLondon",
            credentialProfileName = "LmaxDemoReadOnlyProfile",
            runMode = "PreviewOnly",
            allowExternalConnections = condition == "AllowExternalConnections",
            allowCredentialUse = condition == "AllowCredentialUse",
            allowOrderSubmission = condition == "AllowOrderSubmission",
            schedulerEnabled = condition == "SchedulerEnabled",
            persistToTradingTables = condition == "PersistToTradingTables",
            submitToShadowReplay = condition == "SubmitToShadowReplay"
        });

        Assert.False(snapshot.CanStartSession);
        Assert.Contains(snapshot.SafetyGates, x => x.Gate == expectedCode && !x.Passed);
    }

    [Fact]
    public async Task MarketData_workflow_status_endpoint_is_read_only_and_sanitized()
    {
        var state = SeedData.Create(Now);
        await using var factory = CreateFactory(state);
        using var client = factory.CreateClient();
        var before = SnapshotCounts(state);
        var shadowBefore = (state.LmaxShadowReplayRuns.Count, state.LmaxShadowObservations.Count);

        var status = await GetAsync<LmaxReadOnlyMarketDataWorkflowStatusSummaryDto>(client, "/lmax-readonly-runtime/marketdata-workflow/status");
        var after = SnapshotCounts(state);
        var shadowAfter = (state.LmaxShadowReplayRuns.Count, state.LmaxShadowObservations.Count);
        var json = System.Text.Json.JsonSerializer.Serialize(status);

        Assert.True(status.OperationalStatus is "FrozenManualReadOnly" or "NotAvailable");
        Assert.False(status.RuntimeShadowReplaySubmit);
        Assert.False(status.ExternalConnectionAttempted);
        Assert.False(status.CredentialValuesReturned);
        Assert.False(status.OrderSubmissionAttempted);
        Assert.False(status.TradingMutationAttempted);
        Assert.False(status.SchedulerStarted);
        Assert.Equal("FakeLmaxGateway", status.ApiWorkerGatewayMode);
        Assert.True(status.NoSensitiveContent);
        Assert.Contains(status.WhatIsNotAllowed, x => x.Contains("Scheduler", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(status.WhatIsNotAllowed, x => x.Contains("Order submission", StringComparison.OrdinalIgnoreCase));
        foreach (var sensitive in new[] { "password=", "554=", "rawFix", "NewOrderSingle", "host", "endpointUrl" })
        {
            Assert.DoesNotContain(sensitive, json, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(before, after);
        Assert.Equal(shadowBefore, shadowAfter);
    }

    [Fact]
    public async Task Responses_do_not_expose_sensitive_or_live_connection_fields()
    {
        await using var factory = CreateFactory(enabledFake: true, fakeTransportPreview: true);
        using var client = factory.CreateClient();

        var status = await GetAsync<LmaxReadOnlyRuntimeStatusDto>(client, "/lmax-readonly-runtime/status");
        var run = await PostAsync<LmaxReadOnlyRuntimeRunResultDto>(client, "/lmax-readonly-runtime/run", new { reason = "sensitive scan", fixtureFileName = "lmax-readonly-empty-evidence-v1.json" });
        var preview = await PostAsync<LmaxReadOnlyRuntimeFakeTransportPreviewDto>(client, "/lmax-readonly-runtime/fake-transport-preview", new { reason = "sensitive preview scan", scenario = "MixedReadOnly" });
        var externalIntent = await PostAsync<LmaxReadOnlyRuntimeExternalRunIntentValidationDto>(client, "/lmax-readonly-runtime/external-run-intent/validate", new
        {
            reason = "external intent sensitive scan",
            environmentName = "Demo",
            venueProfileName = "DemoLondon",
            credentialProfileName = "LmaxDemoReadOnlyProfile",
            runMode = "FutureExternalReadOnlyManual"
        });
        var dryRunReport = await PostAsync<LmaxReadOnlyRuntimeExternalDryRunReportDto>(client, "/lmax-readonly-runtime/external-run-intent/dry-run-report", new
        {
            reason = "external dry-run report sensitive scan",
            environmentName = "Demo",
            venueProfileName = "DemoLondon",
            credentialProfileName = "LmaxDemoReadOnlyProfile",
            runMode = "FutureExternalReadOnlyManual"
        });
        var signoff = await PostAsync<LmaxReadOnlyRuntimeExternalSignoffDto>(client, "/lmax-readonly-runtime/external-run-intent/signoff/validate", new
        {
            reason = "external signoff sensitive scan",
            dryRunReportId = dryRunReport.ReportId,
            intentId = dryRunReport.IntentValidation.IntentId,
            requestedByOperatorId = dryRunReport.RequestedByOperatorId,
            signedByOperatorId = "safe-approver",
            signoffRole = "Approver",
            confirmsReadOnlyIntent = true,
            confirmsNoOrderSubmission = true,
            confirmsNoTradingMutation = true,
            confirmsNoScheduler = true,
            confirmsNoShadowReplaySubmit = true,
            confirmsNoCredentialExposure = true,
            confirmsDemoOnly = true,
            confirmsDryRunReportReviewed = true,
            dryRunReportCanStartSession = false,
            dryRunReportSafetyMarkers = dryRunReport.SafetyGates.Select(x => x.Gate).ToArray(),
            decision = "Signed"
        });
        var audit = await PostAsync<LmaxReadOnlyRuntimeExternalPreActivationAuditDto>(client, "/lmax-readonly-runtime/external-run-intent/pre-activation-audit/validate", new
        {
            reason = "external pre-activation audit sensitive scan",
            requestedByOperatorId = dryRunReport.RequestedByOperatorId,
            reviewedByOperatorId = "audit-reviewer",
            signedByOperatorId = signoff.SignedByOperatorId,
            intentId = dryRunReport.IntentValidation.IntentId,
            dryRunReportId = dryRunReport.ReportId,
            signoffId = signoff.SignoffId,
            dryRunReportCanStartSession = dryRunReport.CanStartSession,
            signoffCanAuthorizeExecution = signoff.CanAuthorizeExecution,
            signoffExecutionStillBlocked = signoff.ExecutionStillBlocked,
            stableBlockers = dryRunReport.SafetyGates.Select(x => x.Gate).Concat(signoff.SafetyGates.Select(x => x.Gate)).Distinct().ToArray(),
            dryRunReportReviewed = true,
            signoffReviewed = true
        });
        var readiness = await PostAsync<LmaxReadOnlyRuntimeExternalReadinessSnapshotDto>(client, "/lmax-readonly-runtime/external-run-intent/readiness-snapshot", new
        {
            reason = "external readiness snapshot sensitive scan",
            environmentName = "Demo",
            venueProfileName = "DemoLondon",
            credentialProfileName = "LmaxDemoReadOnlyProfile",
            runMode = "FutureExternalReadOnlyManual"
        });
        var json = System.Text.Json.JsonSerializer.Serialize(new { status, run, preview, externalIntent, dryRunReport, signoff, audit, readiness });

        foreach (var sensitive in new[] { "password", "secretValue", "secretMaterial", "token", "apiKey", "privateKey", "authorization", "554=", "NewOrderSingle", "OrderSent=True", "Connected=True", "host", "username", "endpointUrl", "rawFixText" })
        {
            Assert.DoesNotContain(sensitive, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(PlatformState? state = null, bool enabledFake = false, bool submitToShadowReplay = false, bool fakeTransportPreview = false)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Persistence:Provider"] = "InMemory",
                    ["Safety:AllowExternalConnections"] = "false",
                    ["Safety:AllowLiveTrading"] = "false",
                    ["Safety:RequireFakeExecutionGateway"] = "true",
                    ["LmaxReadOnlyRuntime:Enabled"] = enabledFake ? "true" : "false",
                    ["LmaxReadOnlyRuntime:ImplementationMode"] = enabledFake ? "FakeInMemory" : "DesignOnly",
                    ["LmaxReadOnlyRuntime:ActivationLevel"] = enabledFake ? "Level2LocalManualNoExternal" : "Level1DisabledSkeleton",
                    ["LmaxReadOnlyRuntime:MaxAllowedActivationLevel"] = fakeTransportPreview ? "Level4RuntimeManualReadOnlyConnectionNoReplaySubmit" : "Level2LocalManualNoExternal",
                    ["LmaxReadOnlyRuntime:AllowExternalConnections"] = "false",
                    ["LmaxReadOnlyRuntime:AllowCredentialUse"] = "false",
                    ["LmaxReadOnlyRuntime:AllowOrderSubmission"] = "false",
                    ["LmaxReadOnlyRuntime:PersistToTradingTables"] = "false",
                    ["LmaxReadOnlyRuntime:PersistRawFixMessages"] = "false",
                    ["LmaxReadOnlyRuntime:SchedulerEnabled"] = "false",
                    ["LmaxReadOnlyRuntime:SubmitToShadowReplay"] = submitToShadowReplay ? "true" : "false",
                    ["LmaxReadOnlyRuntime:DryRun"] = "true",
                    ["LmaxReadOnlyRuntime:OperationalReadinessPassed"] = "true",
                    ["LmaxReadOnlyRuntime:GovernanceApproved"] = "true"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<PlatformState>();
                services.AddSingleton(state ?? SeedData.Create(Now));
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(new FixedClock(Now));
                services.RemoveAll<LmaxInfra.LmaxReadOnlyRuntimeAdapterOptions>();
                services.AddSingleton(CreateOptions(enabledFake, submitToShadowReplay, fakeTransportPreview));
            });
        });

    private static LmaxInfra.LmaxReadOnlyRuntimeAdapterOptions CreateOptions(bool enabledFake, bool submitToShadowReplay, bool fakeTransportPreview)
        => new()
        {
            Enabled = enabledFake,
            ImplementationMode = enabledFake ? LmaxInfra.LmaxReadOnlyRuntimeImplementationMode.FakeInMemory : LmaxInfra.LmaxReadOnlyRuntimeImplementationMode.DesignOnly,
            AllowExternalConnections = false,
            AllowCredentialUse = false,
            ReadOnly = true,
            AllowOrderSubmission = false,
            PersistRawFixMessages = false,
            PersistToTradingTables = false,
            SubmitToShadowReplay = submitToShadowReplay,
            SchedulerEnabled = false,
            EnvironmentName = "Testing",
            OperationalReadinessPassed = true,
            GovernanceApproved = true,
            LocalOnlyApi = true,
            DryRun = true,
            RequestedActivationLevel = enabledFake ? LmaxInfra.LmaxReadOnlyRuntimeActivationLevel.Level2LocalManualNoExternal : LmaxInfra.LmaxReadOnlyRuntimeActivationLevel.Level1DisabledSkeleton,
            MaxAllowedActivationLevel = fakeTransportPreview ? LmaxInfra.LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit : LmaxInfra.LmaxReadOnlyRuntimeActivationLevel.Level2LocalManualNoExternal,
            FixtureEvidenceFile = Path.Combine(FindRepoRoot(), "tests", "fixtures", "lmax-shadow", "lmax-mixed-readonly-evidence-v1.json")
        };

    private static (int ParentOrders, int ChildOrders, int Fills, int Positions, int ModelRuns, int RiskDecisions, int ReconciliationBreaks, int Wallets) SnapshotCounts(PlatformState state)
        => (state.ParentOrders.Count, state.ChildOrders.Count, state.Fills.Count, state.PositionLedger.Count, state.ModelRuns.Count, state.RiskDecisions.Count, state.ReconciliationBreaks.Count, state.LmaxCurrencyWallets.Count);

    private static async Task<T> GetAsync<T>(HttpClient client, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Operator-Id", "local-admin");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-Operator-Id", "local-admin");
        request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    public sealed record LmaxReadOnlyMarketDataWorkflowStatusSummaryDto(string SummaryId, DateTimeOffset CreatedAtUtc, string SignoffDecision, string AuditPackDecision, string GateDecision, int ArtifactCount, int EvidencePreviewCount, int ManualReplayCount, int TotalObservationCount, bool RuntimeShadowReplaySubmit, bool ExternalConnectionAttempted, bool CredentialValuesReturned, bool OrderSubmissionAttempted, bool TradingMutationAttempted, bool SchedulerStarted, string ApiWorkerGatewayMode, bool WorkflowFrozen, string OperationalStatus, IReadOnlyList<string> WhatIsAllowed, IReadOnlyList<string> WhatIsNotAllowed, bool NoSensitiveContent, IReadOnlyList<LmaxReadOnlyMarketDataWorkflowStatusIssueDto> Issues);
    public sealed record LmaxReadOnlyMarketDataWorkflowStatusIssueDto(string Severity, string Code, string Path, string Message);

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "QQ.Production.Intraday.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
