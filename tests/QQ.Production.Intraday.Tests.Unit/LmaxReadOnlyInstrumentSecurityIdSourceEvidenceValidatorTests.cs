using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidatorTests
{
    [Fact]
    public void Accepted_evidence_for_allowlisted_symbol_validates()
    {
        var review = ReviewWith([Accepted("GBPUSD", "GBP/USD", "11001")]);

        var result = LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator.Validate(review);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReviewDecision.PASS_WITH_KNOWN_WARNINGS, result.Decision);
        Assert.DoesNotContain(result.Errors, x => x.Path.Contains("GBPUSD", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Unknown_symbol_fails()
    {
        var review = ReviewWith([Accepted("USDCAD", "USD/CAD", "12001")]);

        var result = LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator.Validate(review);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReviewDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "UnknownSymbol");
    }

    [Fact]
    public void Accepted_placeholder_security_id_fails()
    {
        var review = ReviewWith([Accepted("GBPUSD", "GBP/USD", "PHASE6D-DISCOVERY-PENDING-GBPUSD")]);

        var result = LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator.Validate(review);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReviewDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "PlaceholderSecurityIdNotAccepted");
    }

    [Fact]
    public void Accepted_low_confidence_fails()
    {
        var review = ReviewWith([Accepted("GBPUSD", "GBP/USD", "11001") with { Confidence = LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence.Low }]);

        var result = LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator.Validate(review);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReviewDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ConfidenceTooLow");
    }

    [Fact]
    public void Missing_evidence_reference_fails()
    {
        var review = ReviewWith([Accepted("GBPUSD", "GBP/USD", "11001") with { EvidenceReference = "" }]);

        var result = LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator.Validate(review);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReviewDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "EvidenceReferenceRequired");
    }

    [Fact]
    public void External_run_approval_true_fails()
    {
        var review = ReviewWith([Accepted("GBPUSD", "GBP/USD", "11001") with { IsApprovedForExternalRun = true }]);

        var result = LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator.Validate(review);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReviewDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ExternalRunApprovalForbidden");
    }

    [Fact]
    public void Sensitive_sentinel_content_fails()
    {
        var review = ReviewWith([Accepted("GBPUSD", "GBP/USD", "11001") with { EvidenceReference = "operator note contains password=sentinel" }]);

        var result = LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator.Validate(review);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReviewDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "SensitiveContentDetected");
    }

    [Fact]
    public void Order_or_trading_authorization_language_fails()
    {
        var review = ReviewWith([Accepted("GBPUSD", "GBP/USD", "11001") with { ReviewReason = "Confirmed and approve NewOrderSingle in UAT" }]);

        var result = LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator.Validate(review);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReviewDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "TradingAuthorizationImplied");
    }

    [Fact]
    public void Default_pending_manifest_returns_pass_with_known_warnings()
    {
        var result = LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator.Validate();

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReviewDecision.PASS_WITH_KNOWN_WARNINGS, result.Decision);
        Assert.Empty(result.Errors);
        Assert.Contains(result.Warnings, x => x.Code == "EvidencePending");
    }

    [Fact]
    public void Runtime_action_flags_fail()
    {
        var review = LmaxReadOnlyInstrumentSecurityIdEvidenceReviewManifest.CreateDefault() with
        {
            ExternalConnectionAttempted = true,
            ExternalApiCallAttempted = true,
            MarketDataSnapshotAttempted = true,
            ReplayAttempted = true,
            SchedulerOrPollingAdded = true,
            RuntimeShadowReplaySubmit = true,
            OrderSubmissionAdded = true,
            GatewayRegistrationAdded = true,
            TradingMutationAdded = true
        };

        var result = LmaxReadOnlyInstrumentSecurityIdSourceEvidenceValidator.Validate(review);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReviewDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ExternalConnectionForbidden");
        Assert.Contains(result.Errors, x => x.Code == "ExternalApiCallForbidden");
        Assert.Contains(result.Errors, x => x.Code == "SnapshotForbidden");
        Assert.Contains(result.Errors, x => x.Code == "ReplayForbidden");
        Assert.Contains(result.Errors, x => x.Code == "SchedulerPollingForbidden");
        Assert.Contains(result.Errors, x => x.Code == "RuntimeShadowReplaySubmitForbidden");
        Assert.Contains(result.Errors, x => x.Code == "OrderSubmissionForbidden");
        Assert.Contains(result.Errors, x => x.Code == "GatewayRegistrationForbidden");
        Assert.Contains(result.Errors, x => x.Code == "TradingMutationForbidden");
    }

    private static LmaxReadOnlyInstrumentSecurityIdSourceEvidence Accepted(
        string symbol,
        string slashSymbol,
        string proposedSecurityId)
        => new(
            Symbol: symbol,
            SlashSymbol: slashSymbol,
            ProposedSecurityId: proposedSecurityId,
            EvidenceSourceType: LmaxReadOnlyInstrumentSecurityIdSourceEvidenceType.OperatorManualConfirmation,
            EvidenceReference: "sanitized-local-review-reference",
            ReviewedBy: "unit-test-reviewer",
            ReviewedAtUtc: DateTimeOffset.Parse("2026-05-08T00:00:00Z"),
            ReviewReason: "Sanitized planning review only. No external run approval.",
            Confidence: LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence.High,
            Decision: LmaxReadOnlyInstrumentSecurityIdSourceEvidenceDecision.AcceptedForPlanning,
            IsApprovedForExternalRun: false,
            NoSensitiveContent: true);

    private static LmaxReadOnlyInstrumentSecurityIdSourceEvidenceReview ReviewWith(
        IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdSourceEvidence> overrides)
    {
        var defaults = LmaxReadOnlyInstrumentSecurityIdEvidenceReviewManifest.CreateDefault();
        var entries = defaults.Evidence
            .Where(entry => overrides.All(x => !x.Symbol.Equals(entry.Symbol, StringComparison.OrdinalIgnoreCase)))
            .Concat(overrides)
            .ToArray();

        return defaults with { Evidence = entries };
    }
}
