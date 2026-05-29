using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyInstrumentSecurityIdPlanningManifestTests
{
    [Fact]
    public void Accepted_records_apply_into_planning_manifest()
    {
        var records = AcceptedRecords();

        var manifest = LmaxReadOnlyInstrumentSecurityIdPlanningManifestBuilder.FromAcceptedRecords(records, RecordPaths(records));
        var result = LmaxReadOnlyInstrumentSecurityIdPlanningManifestValidator.Validate(manifest, records);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS, result.Decision);
        AssertEntry(manifest, "GBPUSD", "4002");
        AssertEntry(manifest, "EURGBP", "4003");
        AssertEntry(manifest, "USDJPY", "4004");
        AssertEntry(manifest, "AUDUSD", "4007");
        Assert.All(manifest.Instruments, x =>
        {
            Assert.Equal("8", x.SecurityIdSource);
            Assert.Equal("Demo", x.EnvironmentName);
            Assert.Equal("DemoLondon", x.VenueProfileName);
            Assert.False(x.IsApprovedForExternalRun);
            Assert.True(x.NoSensitiveContent);
        });
        Assert.False(manifest.IsApprovedForExternalRun);
    }

    [Fact]
    public void Missing_accepted_record_fails()
    {
        var records = AcceptedRecords().Where(x => x.Symbol != "AUDUSD").ToArray();
        var manifest = LmaxReadOnlyInstrumentSecurityIdPlanningManifestBuilder.FromAcceptedRecords(records, RecordPaths(records));

        var result = LmaxReadOnlyInstrumentSecurityIdPlanningManifestValidator.Validate(manifest, records);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "PlanningSecurityIdInvalid");
        Assert.Contains(result.Errors, x => x.Code == "AcceptedRecordMissing");
    }

    [Fact]
    public void Conflicting_records_fail()
    {
        var records = AcceptedRecords().Concat([Record("GBPUSD", "GBP/USD", "4999")]).ToArray();
        var manifest = LmaxReadOnlyInstrumentSecurityIdPlanningManifestBuilder.FromAcceptedRecords(records, RecordPaths(records));

        var result = LmaxReadOnlyInstrumentSecurityIdPlanningManifestValidator.Validate(manifest, records);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ConflictingAcceptedRecords");
    }

    [Fact]
    public void Placeholder_values_fail()
    {
        var records = AcceptedRecords().Select(x => x.Symbol == "GBPUSD" ? Record("GBPUSD", "GBP/USD", "PHASE6D-DISCOVERY-PENDING-GBPUSD") : x).ToArray();
        var manifest = LmaxReadOnlyInstrumentSecurityIdPlanningManifestBuilder.FromAcceptedRecords(records, RecordPaths(records));

        var result = LmaxReadOnlyInstrumentSecurityIdPlanningManifestValidator.Validate(manifest, records);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "PlanningSecurityIdInvalid");
    }

    [Fact]
    public void Low_confidence_or_non_accepted_records_fail_through_missing_accepted_record()
    {
        var records = AcceptedRecords()
            .Select(x => x.Symbol == "GBPUSD"
                ? x with { Decision = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.Draft, Confidence = LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence.Low }
                : x)
            .ToArray();
        var manifest = LmaxReadOnlyInstrumentSecurityIdPlanningManifestBuilder.FromAcceptedRecords(records, RecordPaths(records));

        var result = LmaxReadOnlyInstrumentSecurityIdPlanningManifestValidator.Validate(manifest, records);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "AcceptedRecordMissing");
    }

    [Fact]
    public void Sensitive_content_fails()
    {
        var records = AcceptedRecords();
        var manifest = LmaxReadOnlyInstrumentSecurityIdPlanningManifestBuilder.FromAcceptedRecords(records, RecordPaths(records));
        manifest = manifest with
        {
            Instruments = manifest.Instruments.Select(x => x.Symbol == "GBPUSD" ? x with { EvidenceReference = "password=sentinel" } : x).ToArray()
        };

        var result = LmaxReadOnlyInstrumentSecurityIdPlanningManifestValidator.Validate(manifest, records);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "SensitiveContentDetected");
    }

    [Theory]
    [InlineData("production authorization")]
    [InlineData("order submission")]
    [InlineData("newordersingle")]
    public void Authorization_language_fails(string text)
    {
        var records = AcceptedRecords();
        var manifest = LmaxReadOnlyInstrumentSecurityIdPlanningManifestBuilder.FromAcceptedRecords(records, RecordPaths(records));
        manifest = manifest with
        {
            Instruments = manifest.Instruments.Select(x => x.Symbol == "GBPUSD" ? x with { EvidenceReference = text } : x).ToArray()
        };

        var result = LmaxReadOnlyInstrumentSecurityIdPlanningManifestValidator.Validate(manifest, records);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "TradingAuthorizationImplied");
    }

    [Fact]
    public void External_run_approval_cannot_be_changed()
    {
        var records = AcceptedRecords();
        var manifest = LmaxReadOnlyInstrumentSecurityIdPlanningManifestBuilder.FromAcceptedRecords(records, RecordPaths(records));
        manifest = manifest with { IsApprovedForExternalRun = true };

        var result = LmaxReadOnlyInstrumentSecurityIdPlanningManifestValidator.Validate(manifest, records);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ExternalRunApprovalForbidden");
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_planning_manifest()
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
    }

    private static void AssertEntry(LmaxReadOnlyInstrumentSecurityIdPlanningManifest manifest, string symbol, string securityId)
        => Assert.Contains(manifest.Instruments, x => x.Symbol == symbol && x.PlanningSecurityId == securityId);

    private static IReadOnlyList<LmaxReadOnlyInstrumentSecurityIdConfirmationRecord> AcceptedRecords()
        =>
        [
            Record("GBPUSD", "GBP/USD", "4002"),
            Record("EURGBP", "EUR/GBP", "4003"),
            Record("USDJPY", "USD/JPY", "4004"),
            Record("AUDUSD", "AUD/USD", "4007")
        ];

    private static Dictionary<string, string> RecordPaths(IEnumerable<LmaxReadOnlyInstrumentSecurityIdConfirmationRecord> records)
        => records.ToDictionary(x => x.RecordId, x => $"artifacts/real/{x.RecordId}.json");

    private static LmaxReadOnlyInstrumentSecurityIdConfirmationRecord Record(string symbol, string slashSymbol, string securityId)
        => new(
            RecordId: $"record-{symbol}-{securityId}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Symbol: symbol,
            SlashSymbol: slashSymbol,
            ProposedSecurityId: securityId,
            EvidenceSourceType: LmaxReadOnlyInstrumentSecurityIdSourceEvidenceType.OfficialLmaxDocument,
            EvidenceReference: "LMAX instrument CSV, Instrument Name + LMAX ID + LMAX symbol columns",
            CapturedBy: "local-operator",
            ReviewedBy: "local-operator",
            ReviewedAtUtc: DateTimeOffset.UtcNow,
            ReviewReason: "Phase 6N planning manifest test",
            Confidence: LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence.Confirmed,
            Decision: LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.AcceptedForPlanning,
            IsApprovedForExternalRun: false,
            NoSensitiveContent: true,
            Notes: "Planning-only.");

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
