using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyGbpusdMarketHoursSnapshotClosureTests
{
    [Fact]
    public void Completed_with_book_reviews_pass()
    {
        var review = LmaxReadOnlyGbpusdMarketHoursSnapshotClosureValidator.ReviewArtifact(Result());

        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.PASS, review.Decision);
        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureClassification.CompletedWithBook, review.Classification);
    }

    [Fact]
    public void Real_like_completed_with_book_artifact_reviews_pass_with_sanitized_credential_metadata()
    {
        var rawText = """
                      {
                        "status": "Completed",
                        "symbol": "GBPUSD",
                        "slashSymbol": "GBP/USD",
                        "securityId": "4002",
                        "securityIdSource": "8",
                        "snapshotReceived": true,
                        "entryCount": 2,
                        "bestBid": 1.36133,
                        "bestAsk": 1.36142,
                        "mid": 1.361375,
                        "redactionStatus": "Redacted",
                        "targetCompIdPresent": true,
                        "senderCompIdPresent": true,
                        "usernamePresent": true,
                        "passwordPresent": true,
                        "senderCompIdLength": 11,
                        "targetCompIdLength": 6,
                        "usernameLength": 11,
                        "passwordLength": 10,
                        "keyLabels": [
                          "LMAX_DEMO_FIX_USERNAME",
                          "LMAX_DEMO_FIX_PASSWORD",
                          "LMAX_DEMO_SENDER_COMP_ID",
                          "LMAX_DEMO_TARGET_COMP_ID"
                        ],
                        "warnings": [],
                        "errors": []
                      }
                      """;

        var review = LmaxReadOnlyGbpusdMarketHoursSnapshotClosureValidator.ReviewArtifact(Result(), rawText);

        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.PASS, review.Decision);
        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureClassification.CompletedWithBook, review.Classification);
        Assert.Empty(review.Issues);
    }

    [Fact]
    public void Redaction_status_redacted_is_accepted_for_completed_with_book()
    {
        var review = LmaxReadOnlyGbpusdMarketHoursSnapshotClosureValidator.ReviewArtifact(Result() with { RedactionStatus = "Redacted" });

        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.PASS, review.Decision);
    }

    [Fact]
    public void Completed_with_empty_book_reviews_pass_with_known_warnings()
    {
        var review = LmaxReadOnlyGbpusdMarketHoursSnapshotClosureValidator.ReviewArtifact(EmptyBookResult());

        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.PASS_WITH_KNOWN_WARNINGS, review.Decision);
        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureClassification.CompletedWithEmptyBook, review.Classification);
    }

    [Fact]
    public void Failed_safe_artifact_reviews_as_safe_warning()
    {
        var review = LmaxReadOnlyGbpusdMarketHoursSnapshotClosureValidator.ReviewArtifact(Result() with
        {
            Status = "FailedSafeSnapshotTimeout",
            SnapshotReceived = false,
            BestBid = null,
            BestAsk = null,
            Mid = null,
            EntryCount = 0
        });

        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.PASS_WITH_KNOWN_WARNINGS, review.Decision);
        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureClassification.FailedSafe, review.Classification);
    }

    [Theory]
    [InlineData("EURUSD", "4002")]
    [InlineData("GBPUSD", "4999")]
    public void Wrong_symbol_or_securityid_fails(string symbol, string securityId)
    {
        var review = LmaxReadOnlyGbpusdMarketHoursSnapshotClosureValidator.ReviewArtifact(Result() with
        {
            Symbol = symbol,
            SecurityId = securityId
        });

        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.FAIL, review.Decision);
        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureClassification.UnsafeFail, review.Classification);
    }

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public void Unsafe_flags_fail(bool order, bool shadow, bool mutation, bool scheduler, bool credentials)
    {
        var review = LmaxReadOnlyGbpusdMarketHoursSnapshotClosureValidator.ReviewArtifact(Result() with
        {
            OrderSubmissionAttempted = order,
            ShadowReplaySubmitAttempted = shadow,
            TradingMutationAttempted = mutation,
            SchedulerStarted = scheduler,
            CredentialValuesReturned = credentials
        });

        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.FAIL, review.Decision);
    }

    [Fact]
    public void Completed_with_book_with_reject_count_fails()
    {
        var review = LmaxReadOnlyGbpusdMarketHoursSnapshotClosureValidator.ReviewArtifact(Result() with { RejectCount = 1 });

        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.FAIL, review.Decision);
        Assert.Contains(review.Issues, issue => issue.Contains("CompletedBookHasOneSnapshotAndNoRejects", StringComparison.Ordinal));
    }

    [Fact]
    public void Completed_with_book_with_missing_bid_or_ask_fails()
    {
        var review = LmaxReadOnlyGbpusdMarketHoursSnapshotClosureValidator.ReviewArtifact(Result() with { BestBid = null });

        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.FAIL, review.Decision);
        Assert.Contains(review.Issues, issue => issue.Contains("CompletedHasSnapshot", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("NewOrderSingle")]
    public void Sensitive_or_authorization_content_fails(string rawText)
    {
        var review = LmaxReadOnlyGbpusdMarketHoursSnapshotClosureValidator.ReviewArtifact(Result(), rawText);

        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.FAIL, review.Decision);
    }

    [Fact]
    public void Completed_with_book_maps_to_marketdata_only_evidence_preview()
    {
        var mapped = LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.MapGbpusdMarketHoursJson(ResultJson());

        Assert.True(mapped.IsValid, mapped.Message);
        Assert.Equal("MarketDataOnly", mapped.EvidenceMode);
        Assert.Equal(
            LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.PASS,
            LmaxReadOnlyGbpusdMarketHoursSnapshotClosureValidator.ValidateMarketDataOnlyPreviewJson(mapped.NormalizedEvidenceJson, expectEmptyBookWarning: false));
    }

    [Fact]
    public void Empty_book_maps_to_marketdata_only_evidence_preview_with_warning()
    {
        var mapped = LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.MapGbpusdMarketHoursJson(EmptyBookResultJson());

        Assert.True(mapped.IsValid, mapped.Message);
        Assert.Equal("MarketDataOnly", mapped.EvidenceMode);
        Assert.Equal(
            LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.PASS_WITH_KNOWN_WARNINGS,
            LmaxReadOnlyGbpusdMarketHoursSnapshotClosureValidator.ValidateMarketDataOnlyPreviewJson(mapped.NormalizedEvidenceJson, expectEmptyBookWarning: true));
    }

    [Fact]
    public void Replay_report_with_nonzero_observations_fails()
    {
        var decision = LmaxReadOnlyGbpusdMarketHoursSnapshotClosureValidator.ValidateReplayReport(ReplayReport() with { ObservationCount = 1 });

        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.FAIL, decision);
    }

    [Fact]
    public void Replay_report_with_mutation_guard_changed_fails()
    {
        var decision = LmaxReadOnlyGbpusdMarketHoursSnapshotClosureValidator.ValidateReplayReport(ReplayReport() with { MutationGuard = "Changed" });

        Assert.Equal(LmaxReadOnlyGbpusdMarketHoursSnapshotClosureDecision.FAIL, decision);
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase7c()
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

    private static LmaxReadOnlyGbpusdManualSnapshotResult Result()
        => new("run", DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow, "Completed", "GBPUSD", "GBP/USD", "4002", "8", "Demo", "DemoLondon", "SnapshotPlusUpdates", "SecurityIdOnly", 1, true, true, false, true, true, true, true, true, true, false, false, false, false, 1.2501, 1.2503, 1.2502, 2, 1000, true, "Redacted", "final-readiness.json", 1, 0, 0, 0, [], []);

    private static LmaxReadOnlyGbpusdManualSnapshotResult EmptyBookResult()
        => Result() with
        {
            Status = "CompletedWithEmptyBook",
            BestBid = null,
            BestAsk = null,
            Mid = null,
            EntryCount = 0,
            Warnings = ["Market data snapshot was received with no entries."]
        };

    private static LmaxReadOnlyGbpusdMarketHoursReplayReport ReplayReport()
        => new("Completed", 0, 0, 0, "Unchanged", RuntimeShadowReplaySubmit: false, ExternalConnectionAttempted: false, NoSensitiveContent: true);

    private static string ResultJson()
        => """
           {
             "runId": "run",
             "startedAtUtc": "2026-05-10T08:00:00Z",
             "completedAtUtc": "2026-05-10T08:00:01Z",
             "status": "Completed",
             "symbol": "GBPUSD",
             "slashSymbol": "GBP/USD",
             "securityId": "4002",
             "securityIdSource": "8",
             "environmentName": "Demo",
             "venueProfileName": "DemoLondon",
             "requestMode": "SnapshotPlusUpdates",
             "symbolEncodingMode": "SecurityIdOnly",
             "marketDepth": 1,
             "externalConnectionAttempted": true,
             "credentialReadAttempted": true,
             "credentialValuesReturned": false,
             "logonAttempted": true,
             "logonSucceeded": true,
             "snapshotRequestAttempted": true,
             "snapshotReceived": true,
             "logoutAttempted": true,
             "logoutSucceeded": true,
             "orderSubmissionAttempted": false,
             "shadowReplaySubmitAttempted": false,
             "tradingMutationAttempted": false,
             "schedulerStarted": false,
             "bestBid": 1.2501,
             "bestAsk": 1.2503,
             "mid": 1.2502,
             "entryCount": 2,
             "noSensitiveContent": true,
             "redactionStatus": "Redacted",
             "sourceFinalReadinessFile": "final-readiness.json",
             "warnings": [],
             "errors": [],
             "diagnostics": {
               "request": { "waitDurationMs": 1000 },
               "messageCounters": {
                 "marketDataSnapshot": 1,
                 "marketDataRequestReject": 0,
                 "businessMessageReject": 0,
                 "reject": 0
               }
             }
           }
           """;

    private static string EmptyBookResultJson()
        => """
           {
             "runId": "run",
             "startedAtUtc": "2026-05-10T08:00:00Z",
             "completedAtUtc": "2026-05-10T08:00:01Z",
             "status": "CompletedWithEmptyBook",
             "symbol": "GBPUSD",
             "slashSymbol": "GBP/USD",
             "securityId": "4002",
             "securityIdSource": "8",
             "environmentName": "Demo",
             "venueProfileName": "DemoLondon",
             "requestMode": "SnapshotPlusUpdates",
             "symbolEncodingMode": "SecurityIdOnly",
             "marketDepth": 1,
             "externalConnectionAttempted": true,
             "credentialReadAttempted": true,
             "credentialValuesReturned": false,
             "logonAttempted": true,
             "logonSucceeded": true,
             "snapshotRequestAttempted": true,
             "snapshotReceived": true,
             "logoutAttempted": true,
             "logoutSucceeded": true,
             "orderSubmissionAttempted": false,
             "shadowReplaySubmitAttempted": false,
             "tradingMutationAttempted": false,
             "schedulerStarted": false,
             "bestBid": null,
             "bestAsk": null,
             "mid": null,
             "entryCount": 0,
             "noSensitiveContent": true,
             "redactionStatus": "Redacted",
             "sourceFinalReadinessFile": "final-readiness.json",
             "warnings": ["Market data snapshot was received with no entries."],
             "errors": [],
             "diagnostics": {
               "request": { "waitDurationMs": 1000 },
               "messageCounters": {
                 "marketDataSnapshot": 1,
                 "marketDataRequestReject": 0,
                 "businessMessageReject": 0,
                 "reject": 0
               }
             }
           }
           """;

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
