namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxDemoOwnerContextDiagnosticContractTests
{
    [Fact]
    public void DiagnosticPersistsSanitizedReadOnlyOwnerContextEvidence()
    {
        var source = File.ReadAllText(RepositoryFile(
            "deploy", "windows", "lmax-demo", "Test-LmaxDemoOwnerContext.ps1"));

        Assert.Contains("lmax_demo_owner_context_result_v1", source, StringComparison.Ordinal);
        Assert.Contains("owner-context-result.json", source, StringComparison.Ordinal);
        Assert.Contains("Normalize-LocalDbInstance", source, StringComparison.Ordinal);
        Assert.Contains("sqllocaldb.exe", source, StringComparison.Ordinal);
        Assert.Contains("SELECT DB_NAME();", source, StringComparison.Ordinal);
        Assert.Contains("LMAX_DEMO_OWNER_CONTEXT_SQL_SELECT_FAILED", source, StringComparison.Ordinal);
        Assert.Contains("sql_access_verified'] = $true", source, StringComparison.Ordinal);
        Assert.Contains("broker_gui_authenticated = $false", source, StringComparison.Ordinal);
        Assert.Contains("no_worker_started = $true", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-LmaxDemoFullCycle.ps1", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticTaskIsPasswordLogonOnlyAndCannotScheduleTheWorker()
    {
        var document = System.Xml.Linq.XDocument.Load(RepositoryFile(
            "deploy", "windows", "lmax-demo", "QQ-LMAX-Demo-OwnerContext-Diagnostic.xml"));
        System.Xml.Linq.XNamespace task = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        Assert.Equal(@"\QQ-LMAX-Demo-OwnerContext-Diagnostic",
            document.Descendants(task + "URI").Single().Value);
        Assert.Equal("Administrator", document.Descendants(task + "UserId").Single().Value);
        Assert.Equal("Password", document.Descendants(task + "LogonType").Single().Value);
        Assert.Empty(document.Descendants(task + "CalendarTrigger"));

        var arguments = document.Descendants(task + "Arguments").Single().Value;
        Assert.Contains("-ExecutionPolicy Bypass", arguments, StringComparison.Ordinal);
        Assert.DoesNotContain("-ExecutionPolicy AllSigned", arguments, StringComparison.Ordinal);
        Assert.Contains("Test-LmaxDemoOwnerContext.ps1", arguments, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-LmaxDemoFullCycle.ps1", arguments, StringComparison.Ordinal);
        Assert.Equal(
            @"C:\deploy\IntradayPlatform\operator\lmax-demo-orchestration\activation",
            document.Descendants(task + "WorkingDirectory").Single().Value);
    }

    private static string RepositoryFile(params string[] segments)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", ".."));

        foreach (var segment in segments)
        {
            path = Path.Combine(path, segment);
        }

        return path;
    }
}
