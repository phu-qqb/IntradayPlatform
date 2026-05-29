using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyAdditionalInstrumentManualSnapshotWorkflowTests
{
    [Theory]
    [InlineData("EURGBP", "EUR/GBP", "4003")]
    [InlineData("USDJPY", "USD/JPY", "4004")]
    [InlineData("AUDUSD", "AUD/USD", "4007")]
    public void Supported_additional_instrument_definitions_resolve(string symbol, string slashSymbol, string securityId)
    {
        Assert.True(LmaxReadOnlyAdditionalInstrumentSnapshotClosureValidator.TryGetDefinition(symbol, out var definition));
        Assert.Equal(slashSymbol, definition.SlashSymbol);
        Assert.Equal(securityId, definition.SecurityId);
        Assert.Equal("8", definition.SecurityIdSource);
    }

    [Fact]
    public void Unknown_symbol_is_not_supported()
        => Assert.False(LmaxReadOnlyAdditionalInstrumentSnapshotClosureValidator.TryGetDefinition("NZDUSD", out _));

    [Fact]
    public void Completed_with_book_validates_pass_for_eurgbp()
    {
        var review = LmaxReadOnlyAdditionalInstrumentSnapshotClosureValidator.ReviewArtifact(Result("EURGBP", "EUR/GBP", "4003"));

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.PASS, review.Decision);
        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotClosureClassification.CompletedWithBook, review.Classification);
    }

    [Fact]
    public void Empty_book_validates_with_known_warning()
    {
        var review = LmaxReadOnlyAdditionalInstrumentSnapshotClosureValidator.ReviewArtifact(EmptyBookResult());

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.PASS_WITH_KNOWN_WARNINGS, review.Decision);
        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotClosureClassification.CompletedWithEmptyBook, review.Classification);
    }

    [Fact]
    public void Failed_safe_validates_with_known_warning_when_flags_are_safe()
    {
        var review = LmaxReadOnlyAdditionalInstrumentSnapshotClosureValidator.ReviewArtifact(Result("EURGBP", "EUR/GBP", "4003") with
        {
            Status = "FailedSafeSnapshotTimeout",
            SnapshotReceived = false,
            BestBid = null,
            BestAsk = null,
            Mid = null,
            EntryCount = 0,
            MarketDataSnapshotCount = 0
        });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.PASS_WITH_KNOWN_WARNINGS, review.Decision);
        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotClosureClassification.FailedSafe, review.Classification);
    }

    [Fact]
    public void Wrong_security_id_fails()
    {
        var review = LmaxReadOnlyAdditionalInstrumentSnapshotClosureValidator.ReviewArtifact(Result("EURGBP", "EUR/GBP", "4999"));

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.FAIL, review.Decision);
    }

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public void Unsafe_flags_fail(bool order, bool shadow, bool mutation, bool scheduler, bool credentials)
    {
        var review = LmaxReadOnlyAdditionalInstrumentSnapshotClosureValidator.ReviewArtifact(Result("EURGBP", "EUR/GBP", "4003") with
        {
            OrderSubmissionAttempted = order,
            ShadowReplaySubmitAttempted = shadow,
            TradingMutationAttempted = mutation,
            SchedulerStarted = scheduler,
            CredentialValuesReturned = credentials
        });

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.FAIL, review.Decision);
    }

    [Theory]
    [InlineData("password=sentinel")]
    [InlineData("NewOrderSingle")]
    public void Sensitive_or_order_text_fails(string rawText)
    {
        var review = LmaxReadOnlyAdditionalInstrumentSnapshotClosureValidator.ReviewArtifact(Result("EURGBP", "EUR/GBP", "4003"), rawText);

        Assert.Equal(LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.FAIL, review.Decision);
    }

    [Fact]
    public void Completed_book_maps_to_marketdata_only_preview_for_eurgbp()
    {
        var mapped = LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.MapAdditionalInstrumentJson(ResultJson("EURGBP", "EUR/GBP", "4003"));

        Assert.True(mapped.IsValid, mapped.Message);
        Assert.Equal(
            LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.PASS,
            LmaxReadOnlyAdditionalInstrumentSnapshotClosureValidator.ValidateMarketDataOnlyPreviewJson(mapped.NormalizedEvidenceJson, "EURGBP"));
    }

    [Theory]
    [InlineData("USDJPY", "USD/JPY", "4004")]
    [InlineData("AUDUSD", "AUD/USD", "4007")]
    public void Evidence_preview_mapping_supports_remaining_additional_instruments(string symbol, string slashSymbol, string securityId)
    {
        var mapped = LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.MapAdditionalInstrumentJson(ResultJson(symbol, slashSymbol, securityId));

        Assert.True(mapped.IsValid, mapped.Message);
        Assert.Equal(
            LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.PASS,
            LmaxReadOnlyAdditionalInstrumentSnapshotClosureValidator.ValidateMarketDataOnlyPreviewJson(mapped.NormalizedEvidenceJson, symbol));
    }

    [Fact]
    public void Empty_book_maps_to_marketdata_only_preview_with_warning()
    {
        var mapped = LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.MapAdditionalInstrumentJson(EmptyBookJson());

        Assert.True(mapped.IsValid, mapped.Message);
        Assert.Equal(
            LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.PASS_WITH_KNOWN_WARNINGS,
            LmaxReadOnlyAdditionalInstrumentSnapshotClosureValidator.ValidateMarketDataOnlyPreviewJson(mapped.NormalizedEvidenceJson, "EURGBP", expectEmptyBookWarning: true));
    }

    [Fact]
    public void Wrapper_script_contains_one_symbol_allowlist_and_requires_final_gate()
    {
        var repoRoot = FindRepoRoot();
        var wrapper = File.ReadAllText(Path.Combine(repoRoot, "scripts", "run-lmax-readonly-runtime-demo-additional-instrument-snapshot-once.ps1"));

        Assert.Contains("GBPUSD", wrapper);
        Assert.Contains("EURGBP", wrapper);
        Assert.Contains("USDJPY", wrapper);
        Assert.Contains("AUDUSD", wrapper);
        Assert.Contains("FinalPreRunGateFile", wrapper);
        Assert.Contains("batch/multiple instruments are refused", wrapper);
        Assert.Contains("run-lmax-readonly-runtime-demo-snapshot-prototype.ps1", wrapper);
        Assert.DoesNotContain("Start-Job", wrapper);
        Assert.DoesNotContain("Register-ScheduledTask", wrapper);
        Assert.DoesNotContain("PeriodicTimer", wrapper);
    }

    [Fact]
    public void Preview_script_uses_review_report_decision_not_stale_last_exit_code()
    {
        var repoRoot = FindRepoRoot();
        var preview = File.ReadAllText(Path.Combine(repoRoot, "scripts", "preview-lmax-readonly-additional-instrument-snapshot-evidence.ps1"));
        var review = File.ReadAllText(Path.Combine(repoRoot, "scripts", "review-lmax-readonly-additional-instrument-snapshot-result.ps1"));

        Assert.Contains("$reviewDecision", preview);
        Assert.Contains("Get-Content -Raw -LiteralPath $reviewPath", preview);
        Assert.DoesNotContain("Additional-instrument artifact review failed.", preview);
        Assert.Contains("$global:LASTEXITCODE = 0", review);
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_phase7h()
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

    private static LmaxReadOnlyAdditionalInstrumentManualSnapshotResult Result(string symbol, string slashSymbol, string securityId)
        => new(
            "run",
            DateTimeOffset.UtcNow.AddSeconds(-1),
            DateTimeOffset.UtcNow,
            "Completed",
            symbol,
            slashSymbol,
            securityId,
            "8",
            "Demo",
            "DemoLondon",
            "SnapshotPlusUpdates",
            "SecurityIdOnly",
            1,
            ExternalConnectionAttempted: true,
            CredentialReadAttempted: true,
            CredentialValuesReturned: false,
            LogonAttempted: true,
            LogonSucceeded: true,
            SnapshotRequestAttempted: true,
            SnapshotReceived: true,
            LogoutAttempted: true,
            LogoutSucceeded: true,
            OrderSubmissionAttempted: false,
            ShadowReplaySubmitAttempted: false,
            TradingMutationAttempted: false,
            SchedulerStarted: false,
            BestBid: 0.8512,
            BestAsk: 0.8514,
            Mid: 0.8513,
            EntryCount: 2,
            WaitDurationMs: 1000,
            NoSensitiveContent: true,
            RedactionStatus: "Redacted",
            SourcePreRunGateFile: "final-prerun.json",
            MarketDataSnapshotCount: 1,
            MarketDataRequestRejectCount: 0,
            BusinessMessageRejectCount: 0,
            RejectCount: 0,
            Warnings: [],
            Errors: []);

    private static LmaxReadOnlyAdditionalInstrumentManualSnapshotResult EmptyBookResult()
        => Result("EURGBP", "EUR/GBP", "4003") with
        {
            Status = "CompletedWithEmptyBook",
            EntryCount = 0,
            BestBid = null,
            BestAsk = null,
            Mid = null,
            Warnings = ["Market data snapshot was received with no entries."]
        };

    private static string ResultJson(string symbol, string slashSymbol, string securityId)
        => $$"""
             {
               "runId": "run",
               "startedAtUtc": "2026-05-11T08:00:00Z",
               "completedAtUtc": "2026-05-11T08:00:01Z",
               "status": "Completed",
               "symbol": "{{symbol}}",
               "slashSymbol": "{{slashSymbol}}",
               "securityId": "{{securityId}}",
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
               "bestBid": 0.8512,
               "bestAsk": 0.8514,
               "mid": 0.8513,
               "entryCount": 2,
               "noSensitiveContent": true,
               "redactionStatus": "Redacted",
               "sourceFinalReadinessFile": "final-prerun.json",
               "marketDataSnapshotCount": 1,
               "marketDataRequestRejectCount": 0,
               "businessMessageRejectCount": 0,
               "rejectCount": 0,
               "warnings": [],
               "errors": []
             }
             """;

    private static string EmptyBookJson()
        => ResultJson("EURGBP", "EUR/GBP", "4003")
            .Replace("\"status\": \"Completed\"", "\"status\": \"CompletedWithEmptyBook\"", StringComparison.Ordinal)
            .Replace("\"bestBid\": 0.8512", "\"bestBid\": null", StringComparison.Ordinal)
            .Replace("\"bestAsk\": 0.8514", "\"bestAsk\": null", StringComparison.Ordinal)
            .Replace("\"mid\": 0.8513", "\"mid\": null", StringComparison.Ordinal)
            .Replace("\"entryCount\": 2", "\"entryCount\": 0", StringComparison.Ordinal)
            .Replace("\"warnings\": []", "\"warnings\": [\"Market data snapshot was received with no entries.\"]", StringComparison.Ordinal);

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
