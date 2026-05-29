using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidatorTests
{
    [Fact]
    public void Valid_accepted_record_validates()
    {
        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.Validate(ValidRecord("GBPUSD", "GBP/USD", "11001"));

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS, result.Decision);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Draft_record_with_placeholder_security_id_validates()
    {
        var record = ValidRecord("GBPUSD", "GBP/USD", "PHASE6D-DISCOVERY-PENDING-GBPUSD") with
        {
            Decision = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.Draft,
            ReviewedBy = "",
            ReviewedAtUtc = null,
            ReviewReason = "",
            Confidence = LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence.Low
        };

        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.Validate(record);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS, result.Decision);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("PHASE6C-DEMO-SECURITYID-GBPUSD")]
    [InlineData("PHASE6D-DISCOVERY-PENDING-GBPUSD")]
    [InlineData("TBD-LMAX-DEMO-GBPUSD")]
    public void Accepted_placeholder_security_id_fails(string placeholder)
    {
        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.Validate(
            ValidRecord("GBPUSD", "GBP/USD", placeholder));

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "PlaceholderSecurityIdNotAccepted");
    }

    [Theory]
    [InlineData(LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence.Low)]
    [InlineData(LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence.Medium)]
    public void Accepted_low_or_medium_confidence_fails(LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence confidence)
    {
        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.Validate(
            ValidRecord("GBPUSD", "GBP/USD", "11001") with { Confidence = confidence });

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ConfidenceTooLow");
    }

    [Fact]
    public void Accepted_without_reviewer_timestamp_or_reason_fails()
    {
        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.Validate(
            ValidRecord("GBPUSD", "GBP/USD", "11001") with
            {
                ReviewedBy = "",
                ReviewedAtUtc = null,
                ReviewReason = ""
            });

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ReviewedByRequired");
        Assert.Contains(result.Errors, x => x.Code == "ReviewedAtRequired");
        Assert.Contains(result.Errors, x => x.Code == "ReviewReasonRequired");
    }

    [Fact]
    public void Accepted_without_evidence_reference_fails()
    {
        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.Validate(
            ValidRecord("GBPUSD", "GBP/USD", "11001") with { EvidenceReference = "" });

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "EvidenceReferenceRequired");
    }

    [Fact]
    public void Unknown_symbol_fails()
    {
        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.Validate(
            ValidRecord("USDCAD", "USD/CAD", "12001"));

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "UnknownSymbol");
    }

    [Fact]
    public void Slash_symbol_mismatch_fails()
    {
        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.Validate(
            ValidRecord("GBPUSD", "GBP-USD", "11001"));

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "SlashSymbolMismatch");
    }

    [Fact]
    public void External_run_approval_true_fails()
    {
        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.Validate(
            ValidRecord("GBPUSD", "GBP/USD", "11001") with { IsApprovedForExternalRun = true });

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ExternalRunApprovalForbidden");
    }

    [Fact]
    public void Sensitive_credential_like_content_fails()
    {
        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.Validate(
            ValidRecord("GBPUSD", "GBP/USD", "11001") with { Notes = "contains token=sentinel" });

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "SensitiveContentDetected");
    }

    [Fact]
    public void Order_or_production_authorization_language_fails()
    {
        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.Validate(
            ValidRecord("GBPUSD", "GBP/USD", "11001") with { Notes = "approve external run and NewOrderSingle in Production" });

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "TradingAuthorizationImplied");
    }

    [Fact]
    public void Review_missing_records_returns_pass_with_known_warnings()
    {
        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.ReviewRecords([]);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS_WITH_KNOWN_WARNINGS, result.Decision);
        Assert.Empty(result.Errors);
        Assert.Contains(result.Warnings, x => x.Code == "AcceptedRecordMissing");
    }

    [Fact]
    public void Review_partial_records_returns_pass_with_known_warnings()
    {
        var records = new[]
        {
            ValidRecord("GBPUSD", "GBP/USD", "11001"),
            ValidRecord("USDJPY", "USD/JPY", "11002")
        };

        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.ReviewRecords(records);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS_WITH_KNOWN_WARNINGS, result.Decision);
        Assert.Empty(result.Errors);
        Assert.Contains(result.Warnings, x => x.Code == "AcceptedRecordMissing");
    }

    [Fact]
    public void Review_detects_conflicting_proposed_security_ids()
    {
        var records = new[]
        {
            ValidRecord("GBPUSD", "GBP/USD", "11001"),
            ValidRecord("GBPUSD", "GBP/USD", "11002") with { RecordId = "record-gbpusd-2" },
            ValidRecord("USDJPY", "USD/JPY", "11003"),
            ValidRecord("EURGBP", "EUR/GBP", "11004"),
            ValidRecord("AUDUSD", "AUD/USD", "11005")
        };

        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.ReviewRecords(records);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ConflictingProposedSecurityIds");
    }

    [Fact]
    public void Review_allows_duplicate_same_security_id_for_same_symbol()
    {
        var records = new[]
        {
            ValidRecord("GBPUSD", "GBP/USD", "11001"),
            ValidRecord("GBPUSD", "GBP/USD", "11001") with { RecordId = "record-gbpusd-2" },
            ValidRecord("USDJPY", "USD/JPY", "11003"),
            ValidRecord("EURGBP", "EUR/GBP", "11004"),
            ValidRecord("AUDUSD", "AUD/USD", "11005")
        };

        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.ReviewRecords(records);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS, result.Decision);
        Assert.DoesNotContain(result.Errors, x => x.Code == "ConflictingProposedSecurityIds");
    }

    [Fact]
    public void Review_passes_when_all_four_have_valid_accepted_records()
    {
        var records = new[]
        {
            ValidRecord("GBPUSD", "GBP/USD", "11001"),
            ValidRecord("USDJPY", "USD/JPY", "11002"),
            ValidRecord("EURGBP", "EUR/GBP", "11003"),
            ValidRecord("AUDUSD", "AUD/USD", "11004")
        };

        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.ReviewRecords(records);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS, result.Decision);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Creation_script_forces_external_run_approval_false()
    {
        var script = File.ReadAllText(Path.Combine(FindRepoRoot(), "scripts", "new-lmax-readonly-securityid-confirmation-record.ps1"));

        Assert.Contains("isApprovedForExternalRun = $false", script);
        Assert.DoesNotContain("[switch]$IsApprovedForExternalRun", script);
        Assert.Contains("artifacts/lmax-readonly-runtime-securityid-confirmations/real", script);
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only()
    {
        var repoRoot = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgramPath = Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Worker", "Program.cs");
        var workerProgram = File.Exists(workerProgramPath) ? File.ReadAllText(workerProgramPath) : string.Empty;
        var combined = apiProgram + Environment.NewLine + workerProgram;

        Assert.Contains("FakeLmaxGateway", apiProgram);
        Assert.DoesNotContain("RealLmaxGateway", combined);
        Assert.DoesNotContain("LmaxVenueGatewaySkeleton", combined);
        Assert.DoesNotContain("ExternalReadOnlyPrototypeGateway", combined);
    }

    [Fact]
    public void Api_and_worker_do_not_add_scheduler_replay_order_or_trading_mutation_surface()
    {
        var repoRoot = FindRepoRoot();
        var paths = new[]
        {
            Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Api", "Program.cs"),
            Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Worker", "Program.cs")
        };
        var combined = string.Join(Environment.NewLine, paths.Where(File.Exists).Select(File.ReadAllText));

        Assert.DoesNotContain("PeriodicTimer", combined);
        Assert.DoesNotContain("System.Threading.Timer", combined);
        Assert.DoesNotContain("SubmitToShadowReplay = true", combined);
        Assert.DoesNotContain("ReplaySubmitAsync", combined);
        Assert.DoesNotContain("NewOrderSingle", combined);
        Assert.DoesNotContain("OrderCancelRequest", combined);
        Assert.DoesNotContain("OrderCancelReplaceRequest", combined);
        Assert.DoesNotContain("SubmitOrder", combined);
        Assert.DoesNotContain("PersistTrade", combined);
        Assert.DoesNotContain("TradingState", combined);
    }

    private static LmaxReadOnlyInstrumentSecurityIdConfirmationRecord ValidRecord(
        string symbol,
        string slashSymbol,
        string proposedSecurityId)
        => new(
            RecordId: $"record-{symbol.ToLowerInvariant()}",
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-08T00:00:00Z"),
            Symbol: symbol,
            SlashSymbol: slashSymbol,
            ProposedSecurityId: proposedSecurityId,
            EvidenceSourceType: LmaxReadOnlyInstrumentSecurityIdSourceEvidenceType.OperatorManualConfirmation,
            EvidenceReference: "sanitized-local-confirmation-reference",
            CapturedBy: "unit-test-capturer",
            ReviewedBy: "unit-test-reviewer",
            ReviewedAtUtc: DateTimeOffset.Parse("2026-05-08T00:05:00Z"),
            ReviewReason: "Sanitized planning confirmation only.",
            Confidence: LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence.High,
            Decision: LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.AcceptedForPlanning,
            IsApprovedForExternalRun: false,
            NoSensitiveContent: true,
            Notes: "Planning record only; execution remains blocked.");

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not find repository root.");
        }

        return directory.FullName;
    }
}
