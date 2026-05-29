using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyInstrumentCsvSecurityIdExtractorTests
{
    [Fact]
    public void Csv_extractor_finds_400x_ids_from_london_and_newyork_shape()
    {
        var result = LmaxReadOnlyInstrumentCsvSecurityIdExtractor.Extract(
            [
                new("LMAX-Instruments.csv", "DemoLondon", LondonCsv()),
                new("LMAX-NewYork-Instruments.csv", "DemoLondon", LondonCsv())
            ]);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS, result.Decision);
        AssertCandidate(result, "GBPUSD", "4002");
        AssertCandidate(result, "EURGBP", "4003");
        AssertCandidate(result, "USDJPY", "4004");
        AssertCandidate(result, "AUDUSD", "4007");
        Assert.All(result.Candidates, x => Assert.False(x.IsApprovedForExternalRun));
        Assert.False(result.ExternalConnectionAttempted);
        Assert.False(result.SecurityListRequestAttempted);
        Assert.False(result.OrderSubmissionAdded);
        Assert.False(result.GatewayRegistrationAdded);
    }

    [Fact]
    public void Csv_extractor_marks_tokyo_600x_ids_not_selected_for_demo_london()
    {
        var result = LmaxReadOnlyInstrumentCsvSecurityIdExtractor.Extract(
            [
                new("LMAX-Instruments.csv", "DemoLondon", LondonCsv()),
                new("LMAX-Tokyo-Instruments.csv", "DemoTokyo", TokyoCsv())
            ]);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS, result.Decision);
        Assert.Contains(result.Candidates, x => x.Symbol == "GBPUSD" && x.SelectedSecurityId == "4002" && x.ObservedTokyoSecurityIds.Contains("6002"));
        Assert.Contains(result.Candidates, x => x.Symbol == "AUDUSD" && x.SelectedSecurityId == "4007" && x.ObservedTokyoSecurityIds.Contains("6007"));
    }

    [Fact]
    public void Csv_extractor_fails_on_conflicting_400x_values()
    {
        var conflicting = LondonCsv() + Environment.NewLine + "GBP/USD,40021,GBP/USD";

        var result = LmaxReadOnlyInstrumentCsvSecurityIdExtractor.Extract([new("LMAX-Instruments.csv", "DemoLondon", conflicting)]);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ConflictingSelectedProfileIds");
    }

    [Fact]
    public void Csv_extractor_fails_on_missing_candidate_rows()
    {
        var missing = """
        Instrument Name,LMAX ID,LMAX symbol
        GBP/USD,4002,GBP/USD
        """;

        var result = LmaxReadOnlyInstrumentCsvSecurityIdExtractor.Extract([new("LMAX-Instruments.csv", "DemoLondon", missing)]);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "CandidateRowMissing");
    }

    [Fact]
    public void Csv_extractor_fails_on_placeholder_or_unexpected_values()
    {
        var placeholder = LondonCsv().Replace("4002", "PHASE6D-GBPUSD", StringComparison.Ordinal);

        var result = LmaxReadOnlyInstrumentCsvSecurityIdExtractor.Extract([new("LMAX-Instruments.csv", "DemoLondon", placeholder)]);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "SelectedProfileIdMissing");
    }

    [Fact]
    public void Csv_extractor_fails_on_sensitive_content()
    {
        var result = LmaxReadOnlyInstrumentCsvSecurityIdExtractor.Extract([new("LMAX-Instruments.csv", "DemoLondon", LondonCsv() + Environment.NewLine + "password=sentinel")]);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "SensitiveContentDetected");
    }

    [Fact]
    public void Generated_record_shape_validates_as_accepted_planning_only()
    {
        var record = new LmaxReadOnlyInstrumentSecurityIdConfirmationRecord(
            RecordId: "lmax-readonly-securityid-confirmation-GBPUSD-test",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Symbol: "GBPUSD",
            SlashSymbol: "GBP/USD",
            ProposedSecurityId: "4002",
            EvidenceSourceType: LmaxReadOnlyInstrumentSecurityIdSourceEvidenceType.OfficialLmaxDocument,
            EvidenceReference: "LMAX-Instruments.csv / LMAX-NewYork-Instruments.csv, Instrument Name + LMAX ID columns",
            CapturedBy: "local-operator",
            ReviewedBy: "local-operator",
            ReviewedAtUtc: DateTimeOffset.UtcNow,
            ReviewReason: "Phase 6M accepted planning values from uploaded LMAX instrument CSVs",
            Confidence: LmaxReadOnlyInstrumentSecurityIdEvidenceConfidence.Confirmed,
            Decision: LmaxReadOnlyInstrumentSecurityIdConfirmationRecordDecision.AcceptedForPlanning,
            IsApprovedForExternalRun: false,
            NoSensitiveContent: true,
            Notes: "Tokyo 600x IDs intentionally not selected for current DemoLondon profile.");

        var result = LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidator.Validate(record);

        Assert.Equal(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS, result.Decision);
        Assert.False(result.Record.IsApprovedForExternalRun);
    }

    [Fact]
    public void Api_and_worker_remain_fake_gateway_only_for_csv_securityid_records()
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

    private static void AssertCandidate(LmaxReadOnlyInstrumentCsvSecurityIdExtractionResult result, string symbol, string securityId)
        => Assert.Contains(result.Candidates, x => x.Symbol == symbol && x.SelectedSecurityId == securityId && x.IsApprovedForExternalRun == false);

    private static string LondonCsv()
        => """
        Instrument Name,LMAX ID,LMAX symbol
        EUR/USD,4001,EUR/USD
        GBP/USD,4002,GBP/USD
        EUR/GBP,4003,EUR/GBP
        USD/JPY,4004,USD/JPY
        AUD/USD,4007,AUD/USD
        """;

    private static string TokyoCsv()
        => """
        Instrument Name,LMAX ID,LMAX symbol
        EUR/USD,6001,EUR/USD
        GBP/USD,6002,GBP/USD
        EUR/GBP,6003,EUR/GBP
        USD/JPY,6004,USD/JPY
        AUD/USD,6007,AUD/USD
        """;

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
