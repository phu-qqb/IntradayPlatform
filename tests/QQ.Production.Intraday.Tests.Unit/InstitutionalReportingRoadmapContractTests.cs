namespace QQ.Production.Intraday.Tests.Unit;

public sealed class InstitutionalReportingRoadmapContractTests
{
    private const string RelativePath =
        "docs/architecture/reporting/hedge-fund-institutional-reporting-roadmap-v1.md";

    [Fact]
    public void Authoritative_roadmap_contract_is_materialized()
    {
        var path = Path.Combine(RepositoryRoot(), RelativePath);
        Assert.True(File.Exists(path), $"Missing roadmap: {RelativePath}");

        var text = File.ReadAllText(path);
        Require(text,
            "hedge_fund_institutional_reporting_roadmap",
            "ManifestVersion | `v1`",
            "AUTHORITATIVE_REPORTING_ROADMAP",
            "reporting_source",
            "reporting_mart",
            "reporting_control",
            "reporting_publication",
            "RPT1 - Operational Reporting And Breaks",
            "RPT2 - Performance And Risk Mart",
            "RPT3 - Daily Management Pack",
            "RPT4 - Monthly Investment Committee Pack",
            "RPT1 | COMPLETED",
            "60c79bfbd5827919eaf1299e045ef9918baef720",
            "PDF",
            "PPTX",
            "XLSX",
            "Missing, absent or unknown data is never converted to zero",
            "No ModelRun, Fill, position or ledger event may be invented",
            "No Databento API request is permitted",
            "No new Databento download is permitted",
            "Existing Databento data already stored on `D:` may be used only",
            "This RPT2 packet makes no Databento API request",
            "uses no Databento data or path",
            "`SOURCE_PROVEN` is reserved for a directly persisted fact",
            "Gross and net concentration are distinct metric families",
            "Base quantities from different instruments or currencies are not additive",
            "source-snapshot.json",
            "candidates at the same authoritative rank fails closed",
            "General Manifest Traceability",
            "Phase Completion Criteria");
    }

    [Fact]
    public void Roadmap_has_exact_required_identity()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), RelativePath));
        Assert.Contains(
            "CurrentMasterAtCreation | `60c79bfbd5827919eaf1299e045ef9918baef720`",
            text, StringComparison.Ordinal);
        Assert.Contains(
            "Status | `AUTHORITATIVE_REPORTING_ROADMAP`",
            text, StringComparison.Ordinal);
        Assert.Contains(
            "Next action: `RPT2_METRIC_AUTHORITY_AND_AVAILABILITY_FOUNDATION`",
            text, StringComparison.Ordinal);
    }

    [Fact]
    public void Databento_contract_forbids_network_but_not_authorized_local_lineage()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), RelativePath));
        Assert.Contains("No Databento API request is permitted", text, StringComparison.Ordinal);
        Assert.Contains("No new Databento download is permitted", text, StringComparison.Ordinal);
        Assert.Contains("Existing Databento data already stored on `D:` may be used only", text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Databento is prohibited", text, StringComparison.Ordinal);
    }

    private static void Require(string text, params string[] required)
    {
        foreach (var value in required)
            Assert.Contains(value, text, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("test repository root not found");
    }
}
