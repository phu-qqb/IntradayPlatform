using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlySecurityListDiscoveryTests
{
    [Fact]
    public void Parse_security_list_matches_all_candidates()
    {
        var result = LmaxReadOnlySecurityListDiscovery.CreateResult(
            LmaxReadOnlySecurityListDiscoveryStatus.Completed,
            new(),
            [SecurityListMessage()],
            externalConnectionAttempted: true,
            credentialReadAttempted: true,
            logonAttempted: true,
            logonSucceeded: true,
            securityListRequestAttempted: true,
            securityListReceived: true,
            logoutAttempted: true,
            logoutSucceeded: true);

        Assert.Equal(LmaxReadOnlySecurityListDiscoveryStatus.Completed, result.Status);
        Assert.Equal(4, result.CandidateMatches.Count);
        Assert.Empty(result.UnmatchedCandidates);
        Assert.Contains(result.CandidateMatches, x => x.Symbol == "GBPUSD" && x.SecurityId == "4101" && x.SecurityIdSource == "8");
        Assert.Contains(result.CandidateMatches, x => x.Symbol == "USDJPY" && x.SecurityId == "4102");
        Assert.Contains(result.CandidateMatches, x => x.Symbol == "EURGBP" && x.SecurityId == "4103");
        Assert.Contains(result.CandidateMatches, x => x.Symbol == "AUDUSD" && x.SecurityId == "4104");
        Assert.All(result.CandidateMatches, x => Assert.False(x.IsApprovedForExternalRun));
    }

    [Fact]
    public void Unmatched_candidates_are_reported()
    {
        var message = Fix("35=y", "146=1", "55=GBP/USD", "48=4101", "22=8", "167=FOR", "15=GBP", "120=USD");

        var result = LmaxReadOnlySecurityListDiscovery.CreateResult(
            LmaxReadOnlySecurityListDiscoveryStatus.Completed,
            new(),
            [message],
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true);

        Assert.Equal(LmaxReadOnlySecurityListDiscoveryStatus.CompletedWithWarnings, result.Status);
        Assert.Equal(["USDJPY", "EURGBP", "AUDUSD"], result.UnmatchedCandidates);
    }

    [Fact]
    public void Conflicting_security_ids_are_detected()
    {
        var message = Fix(
            "35=y",
            "146=2",
            "55=GBP/USD", "48=4101", "22=8",
            "55=GBP/USD", "48=4999", "22=8");

        var result = LmaxReadOnlySecurityListDiscovery.CreateResult(
            LmaxReadOnlySecurityListDiscoveryStatus.Completed,
            new(),
            [message],
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true);

        Assert.Equal(LmaxReadOnlySecurityListDiscoveryStatus.CompletedWithWarnings, result.Status);
        Assert.Contains("GBPUSD", LmaxReadOnlySecurityListDiscovery.FindConflicts(result.CandidateMatches));
        Assert.Contains(result.Warnings, x => x.Contains("Conflicting SecurityID", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("j", false, LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListBusinessReject)]
    [InlineData("3", false, LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListSessionReject)]
    [InlineData("5", false, LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListRequestRejected)]
    [InlineData(null, true, LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListTimeout)]
    public void Response_classification_is_safe(string? messageType, bool timedOut, LmaxReadOnlySecurityListDiscoveryStatus expected)
    {
        Assert.Equal(expected, LmaxReadOnlySecurityListDiscovery.ClassifyResponse(messageType, timedOut));
    }

    [Fact]
    public void Security_list_request_profiles_are_explicit()
    {
        var all = LmaxReadOnlySecurityListDiscovery.BuildSecurityListRequestFields(new(), "REQ1");
        var forex = LmaxReadOnlySecurityListDiscovery.BuildSecurityListRequestFields(new() { RequestProfile = LmaxReadOnlySecurityListDiscoveryRequestProfile.SecurityTypeFx }, "REQ2");
        var symbol = LmaxReadOnlySecurityListDiscovery.BuildSecurityListRequestFields(new() { RequestProfile = LmaxReadOnlySecurityListDiscoveryRequestProfile.SymbolExact, SymbolFilter = "GBP/USD" }, "REQ3");

        Assert.Contains(("559", "4"), all);
        Assert.Contains(("559", "1"), forex);
        Assert.Contains(("167", "FOR"), forex);
        Assert.Contains(("559", "0"), symbol);
        Assert.Contains(("55", "GBP/USD"), symbol);
    }

    [Fact]
    public void Failed_artifact_validates_as_safe_failure()
    {
        var json = """
        {
          "status": "FailedSafeSecurityListRequestRejected",
          "requestProfile": "AllInstruments",
          "credentialValuesReturned": false,
          "logonSucceeded": false,
          "securityListRequestAttempted": false,
          "logoutSucceeded": false,
          "errors": ["SecurityList discovery failed safe: SessionStateUnauthorizedAccessException"],
          "noSensitiveContent": true,
          "isApprovedForExternalRun": false,
          "orderSubmissionAttempted": false,
          "shadowReplaySubmitAttempted": false,
          "tradingMutationAttempted": false,
          "schedulerStarted": false
        }
        """;

        var result = LmaxReadOnlySecurityListDiscoveryArtifactValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS, result.Decision);
        Assert.Equal(LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListRequestRejected, result.Diagnostics.ResponseClassification);
        Assert.False(result.Diagnostics.IsApprovedForExternalRun);
        Assert.False(result.Diagnostics.OrderSubmissionAttempted);
    }

    [Theory]
    [InlineData("3", "559", "Unsupported SecurityListRequestType", LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListRequestTypeUnsupported)]
    [InlineData("3", "55", "Unsupported symbol filter", LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListUnsupportedSymbolFilter)]
    [InlineData("3", "320", "Unsupported request", LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListUnsupportedByVenue)]
    [InlineData("9", null, "No mapping", LmaxReadOnlySecurityListDiscoveryStatus.FailedSafeSecurityListUnknownReject)]
    public void Reject_details_map_to_specific_classification(string messageType, string? rejectTag, string rejectText, LmaxReadOnlySecurityListDiscoveryStatus expected)
    {
        Assert.Equal(expected, LmaxReadOnlySecurityListDiscovery.ClassifyReject(messageType, rejectTag, rejectText, timedOut: false));
    }

    [Fact]
    public void Unknown_reject_artifact_review_returns_safe_vendor_fallback()
    {
        var result = LmaxReadOnlySecurityListDiscoveryFallbackDecisionValidator.ValidateJson(UnknownRejectArtifact());

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS, result.Decision);
        Assert.Equal(LmaxReadOnlySecurityListFallbackDecisionKind.UseVendorSupportConfirmation, result.FallbackDecision.RecommendedDecision);
        Assert.True(result.FallbackDecision.MissingRejectDiagnostics);
        Assert.Empty(result.FallbackDecision.Attempts);
        Assert.Equal(0, result.FallbackDecision.CandidateMatchCount);
        Assert.Equal(["GBPUSD", "USDJPY", "EURGBP", "AUDUSD"], result.FallbackDecision.UnmatchedCandidates);
        Assert.False(result.FallbackDecision.IsApprovedForExternalRun);
        Assert.False(result.FallbackDecision.ExternalRunAuthorized);
    }

    [Fact]
    public void Artifact_with_all_candidates_unmatched_remains_non_authorizing()
    {
        var result = LmaxReadOnlySecurityListDiscoveryFallbackDecisionValidator.ValidateJson(UnknownRejectArtifact());

        Assert.True(result.FallbackDecision.RecommendedDecision is
            LmaxReadOnlySecurityListFallbackDecisionKind.UseVendorSupportConfirmation or
            LmaxReadOnlySecurityListFallbackDecisionKind.BlockedPendingEvidence);
        Assert.False(result.FallbackDecision.ExternalRunAuthorized);
        Assert.False(result.FallbackDecision.IsApprovedForExternalRun);
    }

    [Fact]
    public void Fallback_artifact_with_sensitive_content_fails()
    {
        var json = UnknownRejectArtifact().Replace("\"errors\": []", "\"errors\": [\"password=sentinel\"]", StringComparison.Ordinal);

        var result = LmaxReadOnlySecurityListDiscoveryFallbackDecisionValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "SensitiveContentDetected");
    }

    [Fact]
    public void Fallback_artifact_with_external_run_approval_fails()
    {
        var json = UnknownRejectArtifact().Replace("\"isApprovedForExternalRun\": false", "\"isApprovedForExternalRun\": true", StringComparison.Ordinal);

        var result = LmaxReadOnlySecurityListDiscoveryFallbackDecisionValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ExternalRunApprovalForbidden");
    }

    [Fact]
    public void Known_rejected_profiles_are_skipped_by_auto_sequence_unless_allowed()
    {
        var safe = LmaxReadOnlySecurityListDiscovery.GetAutoSequenceProfiles(allowKnownRejectedDiagnostics: false);
        var withDiagnostics = LmaxReadOnlySecurityListDiscovery.GetAutoSequenceProfiles(allowKnownRejectedDiagnostics: true);

        Assert.DoesNotContain(safe, x => x.KnownRejectedByLmaxDemo);
        Assert.Contains(withDiagnostics, x => x.Profile == LmaxReadOnlySecurityListDiscoveryRequestProfile.SymbolExact);
        Assert.Contains(safe, x => x.Profile == LmaxReadOnlySecurityListDiscoveryRequestProfile.MinimalRequest);
    }

    [Fact]
    public void Redactor_removes_sentinel_credential_values()
    {
        var redacted = LmaxReadOnlySecurityListDiscoveryRedactor.Redact(
            "host=demo.example 553=sentinel-user 554=sentinel-password token=abc account=123",
            ["sentinel-user", "sentinel-password"]);

        Assert.DoesNotContain("sentinel-user", redacted);
        Assert.DoesNotContain("sentinel-password", redacted);
        Assert.DoesNotContain("abc", redacted);
        Assert.DoesNotContain("123", redacted);
        Assert.Contains("[REDACTED]", redacted);
    }

    [Fact]
    public void Artifact_contains_no_credential_values_and_safe_flags_remain_false()
    {
        var result = LmaxReadOnlySecurityListDiscovery.CreateResult(
            LmaxReadOnlySecurityListDiscoveryStatus.Completed,
            new(),
            [SecurityListMessage()],
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true);

        var json = LmaxReadOnlySecurityListDiscoveryArtifact.ToSanitizedJson(result, ["sentinel-password"]);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.DoesNotContain("sentinel-password", json);
        Assert.False(root.GetProperty("credentialValuesReturned").GetBoolean());
        Assert.False(root.GetProperty("orderSubmissionAttempted").GetBoolean());
        Assert.False(root.GetProperty("shadowReplaySubmitAttempted").GetBoolean());
        Assert.False(root.GetProperty("tradingMutationAttempted").GetBoolean());
        Assert.False(root.GetProperty("schedulerStarted").GetBoolean());
        Assert.True(root.GetProperty("noSensitiveContent").GetBoolean());
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_securitylist_discovery()
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
    }

    [Fact]
    public void Api_and_worker_do_not_add_order_scheduler_replay_or_mutation_surface()
    {
        var repoRoot = FindRepoRoot();
        var combined = string.Join(Environment.NewLine, new[]
        {
            Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Api", "Program.cs"),
            Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Worker", "Program.cs")
        }.Where(File.Exists).Select(File.ReadAllText));

        Assert.DoesNotContain("PeriodicTimer", combined);
        Assert.DoesNotContain("SubmitToShadowReplay = true", combined);
        Assert.DoesNotContain("NewOrderSingle", combined);
        Assert.DoesNotContain("OrderCancelRequest", combined);
        Assert.DoesNotContain("OrderCancelReplaceRequest", combined);
        Assert.DoesNotContain("SubmitOrder", combined);
        Assert.DoesNotContain("PersistTrade", combined);
        Assert.DoesNotContain("TradingState", combined);
    }

    private static string SecurityListMessage()
        => Fix(
            "35=y",
            "320=REQ1",
            "146=4",
            "55=GBP/USD", "48=4101", "22=8", "167=FOR", "15=GBP", "120=USD",
            "55=USD/JPY", "48=4102", "22=8", "167=FOR", "15=USD", "120=JPY",
            "55=EUR/GBP", "48=4103", "22=8", "167=FOR", "15=EUR", "120=GBP",
            "55=AUD/USD", "48=4104", "22=8", "167=FOR", "15=AUD", "120=USD");

    private static string UnknownRejectArtifact()
        => """
        {
          "discoveryId": "lmax-securitylist-discovery-20260509-145908",
          "createdAtUtc": "2026-05-09T12:59:08.8623007+00:00",
          "status": "FailedSafeSecurityListUnknownReject",
          "environmentName": "Demo",
          "credentialProfileName": "LmaxDemoReadOnlyProfile",
          "requestProfile": "AutoSequence",
          "finalStatus": "FailedSafeSecurityListUnknownReject",
          "selectedSuccessfulProfile": null,
          "externalConnectionAttempted": false,
          "credentialReadAttempted": true,
          "credentialValuesReturned": false,
          "logonAttempted": false,
          "logonSucceeded": false,
          "securityListRequestAttempted": false,
          "securityListReceived": false,
          "logoutAttempted": false,
          "logoutSucceeded": false,
          "totalInstrumentCount": 0,
          "candidateMatches": {},
          "unmatchedCandidates": ["GBPUSD", "USDJPY", "EURGBP", "AUDUSD"],
          "instruments": [],
          "attempts": [],
          "warnings": [],
          "errors": [],
          "noSensitiveContent": true,
          "redactionStatus": "Redacted",
          "isApprovedForExternalRun": false,
          "orderSubmissionAttempted": false,
          "shadowReplaySubmitAttempted": false,
          "tradingMutationAttempted": false,
          "schedulerStarted": false
        }
        """;

    private static string Fix(params string[] fields)
        => string.Join('\u0001', fields) + '\u0001';

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
