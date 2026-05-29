using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyMarketHoursNextActionSummaryTests
{
    [Fact]
    public void Summary_from_valid_artifacts_returns_selected_gbpusd_4002()
    {
        var validation = ValidSummary();

        Assert.Equal(LmaxReadOnlyMarketHoursNextActionDecision.PASS, validation.FinalDecision);
        Assert.Equal("GBPUSD", validation.Summary.SelectedInstrument.Symbol);
        Assert.Equal("4002", validation.Summary.SelectedInstrument.SecurityId);
        Assert.Equal(0, validation.Summary.ExecutableCount);
        Assert.False(validation.Summary.CanRunExternalSnapshot);
    }

    [Fact]
    public void Summary_marks_previous_empty_book_as_safe_outside_market_hours()
    {
        var summary = ValidSummary().Summary;

        Assert.Equal("CompletedWithEmptyBook", summary.PreviousAttempt.Status);
        Assert.True(summary.PreviousAttempt.OutsideMarketHours);
        Assert.True(summary.PreviousAttempt.Safe);
        Assert.Equal(0, summary.PreviousAttempt.EntryCount);
    }

    [Theory]
    [InlineData("canRunExternalSnapshot")]
    [InlineData("isApprovedForExternalRun")]
    [InlineData("eligibleForManualSnapshotAttempt")]
    public void Unsafe_final_readiness_run_flags_fail(string flag)
    {
        var validation = Summary(finalReadinessOverride: $"""
        "{flag}": true
        """);

        Assert.Equal(LmaxReadOnlyMarketHoursNextActionDecision.FAIL, validation.FinalDecision);
    }

    [Fact]
    public void Summary_fails_if_retry_can_run_automatically()
    {
        var validation = Summary(retryOverride: """
        "canRunAutomatically": true
        """);

        Assert.Equal(LmaxReadOnlyMarketHoursNextActionDecision.FAIL, validation.FinalDecision);
    }

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("bearer sentinel")]
    public void Sensitive_content_fails(string rawText)
    {
        using var finalReadiness = JsonDocument.Parse(FinalReadiness());
        using var retryReadiness = JsonDocument.Parse(RetryReadiness());
        using var review = JsonDocument.Parse(Review());
        using var docPack = JsonDocument.Parse(DocPack());

        var issue = new LmaxReadOnlyMarketHoursNextActionIssue("Error", "SensitiveContentDetected", "", rawText);
        var validation = LmaxReadOnlyMarketHoursNextActionSummaryValidator.FromArtifacts(
            finalReadiness.RootElement,
            "final.json",
            retryReadiness.RootElement,
            "retry.json",
            review.RootElement,
            "review.json",
            docPack.RootElement,
            "doc-pack.json",
            "FakeLmaxGateway",
            new[] { issue });

        Assert.Equal(LmaxReadOnlyMarketHoursNextActionDecision.FAIL, validation.FinalDecision);
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase6ze()
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

    private static LmaxReadOnlyMarketHoursNextActionValidation ValidSummary()
        => Summary();

    private static LmaxReadOnlyMarketHoursNextActionValidation Summary(string finalReadinessOverride = "", string retryOverride = "")
    {
        using var finalReadiness = JsonDocument.Parse(FinalReadiness(finalReadinessOverride));
        using var retryReadiness = JsonDocument.Parse(RetryReadiness(retryOverride));
        using var review = JsonDocument.Parse(Review());
        using var docPack = JsonDocument.Parse(DocPack());

        return LmaxReadOnlyMarketHoursNextActionSummaryValidator.FromArtifacts(
            finalReadiness.RootElement,
            "final.json",
            retryReadiness.RootElement,
            "retry.json",
            review.RootElement,
            "review.json",
            docPack.RootElement,
            "doc-pack.json",
            "FakeLmaxGateway");
    }

    private static string FinalReadiness(string extra = "")
        => $$"""
        {
          "symbol": "GBPUSD",
          "slashSymbol": "GBP/USD",
          "planningSecurityId": "4002",
          "securityIdSource": "8",
          "readinessDecision": "PASS",
          "isApprovedForExternalRun": false,
          "eligibleForManualSnapshotAttempt": false,
          "canRunExternalSnapshot": false,
          "runtimeShadowReplaySubmit": false,
          "orderSubmissionAttempted": false,
          "shadowReplaySubmitAttempted": false,
          "tradingMutationAttempted": false,
          "schedulerStarted": false,
          "noSensitiveContent": true{{Comma(extra)}}
          {{extra}}
        }
        """;

    private static string RetryReadiness(string extra = "")
        => $$"""
        {
          "symbol": "GBPUSD",
          "slashSymbol": "GBP/USD",
          "securityId": "4002",
          "securityIdSource": "8",
          "previousResultStatus": "CompletedWithEmptyBook",
          "previousAttemptWasOutsideMarketHours": true,
          "retryAllowedOnlyDuringMarketHours": true,
          "retryIsManualOnly": true,
          "canRunAutomatically": false,
          "externalConnectionAttempted": false,
          "snapshotAttempted": false,
          "replayAttempted": false,
          "schedulerStarted": false,
          "orderSubmissionAttempted": false,
          "shadowReplaySubmitAttempted": false,
          "tradingMutationAttempted": false,
          "noSensitiveContent": true,
          "decision": "PASS"{{Comma(extra)}}
          {{extra}}
        }
        """;

    private static string Review()
        => """
        {
          "status": "CompletedWithEmptyBook",
          "symbol": "GBPUSD",
          "slashSymbol": "GBP/USD",
          "securityId": "4002",
          "securityIdSource": "8",
          "snapshotReceived": true,
          "entryCount": 0,
          "warningClassification": "CompletedWithEmptyBook",
          "orderSubmissionAttempted": false,
          "shadowReplaySubmitAttempted": false,
          "tradingMutationAttempted": false,
          "schedulerStarted": false,
          "credentialValuesReturned": false,
          "noSensitiveContent": true,
          "finalDecision": "PASS_WITH_KNOWN_WARNINGS"
        }
        """;

    private static string DocPack()
        => """
        {
          "finalDecision": "PASS",
          "instrumentCount": 4,
          "executableCount": 0,
          "isApprovedForExternalRun": false,
          "canRunExternalSnapshot": false,
          "eligibleForManualSnapshotAttempt": false,
          "runtimeShadowReplaySubmit": false,
          "schedulerOrPolling": false,
          "orderSubmission": false,
          "gatewayRegistration": false,
          "tradingMutation": false,
          "externalConnectionAttempted": false,
          "snapshotAttempted": false,
          "replayAttempted": false,
          "noSensitiveContent": true
        }
        """;

    private static string Comma(string extra)
        => string.IsNullOrWhiteSpace(extra) ? "" : ",";

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
