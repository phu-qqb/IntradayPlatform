using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class SystemAuditR002ContractAdoptionTests
{
    [Fact]
    public void Frozen_contract_ids_are_referenced_exactly()
    {
        Assert.Equal(
            [
                "lmax-marketdata-db.v1",
                "marketdata-readiness.v1",
                "canonical-timing.v1",
                "environment-secret.v1"
            ],
            SystemAuditR002ContractAdoption.FrozenContractIds());
    }

    [Fact]
    public async Task Adoption_package_writes_required_artifacts_manifest_and_hashes()
    {
        var root = TempRoot("system-audit-r002-adoption-test");

        var result = await SystemAuditR002ContractAdoption.RunAsync(Request(root), CancellationToken.None);

        Assert.Equal("WARN", result.SystemAuditR002AdoptionStatus);
        var validation = Path.Combine(root, "10_validation");
        foreach (var basename in RequiredBasenames())
        {
            Assert.True(File.Exists(Path.Combine(validation, $"{basename}.json")), basename);
            Assert.True(File.Exists(Path.Combine(validation, $"{basename}.md")), basename);
        }

        Assert.True(File.Exists(Path.Combine(root, "share", "system_audit_r002_adoption_summary.md")));
        Assert.True(File.Exists(Path.Combine(root, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(root, "manifest.sha256")));
        Assert.True(File.Exists(Path.Combine(root, "hashes.json")));

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
        var files = manifest.RootElement.GetProperty("files").EnumerateArray().Select(x => x.GetString()).ToArray();
        foreach (var basename in RequiredBasenames())
        {
            Assert.Contains($"10_validation/{basename}.json", files);
            Assert.Contains($"10_validation/{basename}.md", files);
        }
    }

    [Fact]
    public async Task Gap_closure_uses_operator_supplied_timing_and_deterministic_readiness_ids()
    {
        var root = TempRoot("system-audit-r002-gap-closure-operator-values");
        var request = Request(root) with
        {
            RunKey = "system-audit-r002-gap-closure-operator-values",
            CanonicalTargetCloseUtc = "2025-12-17T02:00:00Z",
            WindowStartUtc = "2025-12-17T01:47:00Z",
            WindowEndUtc = "2025-12-17T02:00:00Z"
        };

        await SystemAuditR002ContractAdoption.RunAsync(request, CancellationToken.None);

        var canonical = File.ReadAllText(Path.Combine(root, "10_validation", "canonical_timing_v1_gap_closure.json"));
        Assert.Contains("2025-12-17T02:00:00.0000000", canonical, StringComparison.Ordinal);
        Assert.Contains("2025-12-17T01:47:00.0000000", canonical, StringComparison.Ordinal);
        Assert.Contains("\"validationStatus\": \"PASS\"", canonical, StringComparison.Ordinal);

        var readiness = File.ReadAllText(Path.Combine(root, "10_validation", "marketdata_readiness_v1_gap_closure.json"));
        Assert.Contains("qwr-system-audit-r002-adoption-001-20251217T020000Z", readiness, StringComparison.Ordinal);
        Assert.Contains("cbr-system-audit-r002-adoption-001-20251217T020000Z", readiness, StringComparison.Ordinal);
        Assert.Contains("fqr-system-audit-r002-adoption-001-20251217T020000Z", readiness, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Evidence_matrix_contains_every_required_evidence_name()
    {
        var root = TempRoot("system-audit-r002-adoption-matrix");

        await SystemAuditR002ContractAdoption.RunAsync(Request(root), CancellationToken.None);

        using var matrix = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "10_validation", "system_audit_r002_evidence_matrix.json")));
        var names = matrix.RootElement.GetProperty("evidence")
            .EnumerateArray()
            .Select(row => row.GetProperty("EvidenceName").GetString())
            .ToArray();

        foreach (var required in SystemAuditR002ContractAdoption.RequiredEvidence())
        {
            Assert.Contains(required, names);
        }
    }

    [Fact]
    public async Task Manifest_sha_and_hashes_avoid_circular_hashes()
    {
        var root = TempRoot("system-audit-r002-adoption-hashes");

        await SystemAuditR002ContractAdoption.RunAsync(Request(root), CancellationToken.None);

        var manifestHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(root, "manifest.json")))).ToLowerInvariant();
        Assert.Equal($"{manifestHash}  manifest.json", File.ReadAllText(Path.Combine(root, "manifest.sha256")).Trim());

        using var hashes = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "hashes.json")));
        var hashedPaths = hashes.RootElement.EnumerateArray().Select(x => x.GetProperty("path").GetString()).ToArray();
        Assert.Contains("manifest.json", hashedPaths);
        Assert.DoesNotContain("hashes.json", hashedPaths);
        Assert.DoesNotContain("manifest.sha256", hashedPaths);
    }

    [Fact]
    public async Task Secrets_are_presence_only_and_raw_values_are_not_persisted()
    {
        WithTemporaryEnv("LMAX_DEMO_MD_USERNAME", "raw-user-secret-for-r002-test", out var restoreUser);
        WithTemporaryEnv("LMAX_DEMO_MD_PASSWORD", "raw-password-secret-for-r002-test", out var restorePassword);
        try
        {
            var root = TempRoot("system-audit-r002-adoption-secrets");

            await SystemAuditR002ContractAdoption.RunAsync(Request(root), CancellationToken.None);

            var allText = string.Join(Environment.NewLine, Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
            Assert.DoesNotContain("raw-user-secret-for-r002-test", allText, StringComparison.Ordinal);
            Assert.DoesNotContain("raw-password-secret-for-r002-test", allText, StringComparison.Ordinal);
            Assert.Contains("\"noCredentialValuesPersisted\": true", allText, StringComparison.Ordinal);
        }
        finally
        {
            restoreUser();
            restorePassword();
        }
    }

    [Fact]
    public async Task No_production_execution_path_and_forbidden_artifacts_are_absent()
    {
        var root = TempRoot("system-audit-r002-adoption-boundary");

        await SystemAuditR002ContractAdoption.RunAsync(Request(root), CancellationToken.None);

        var report = File.ReadAllText(Path.Combine(root, "10_validation", "no_production_execution_path_report.json"));
        Assert.Contains("\"NO_PRODUCTION_EXECUTION_PATH\": \"PASS\"", report, StringComparison.Ordinal);
        Assert.Contains("\"noFixOrderEndpointUsed\": true", report, StringComparison.Ordinal);
        Assert.Contains("\"noNewOrderSingle35D\": true", report, StringComparison.Ordinal);
        Assert.Contains("\"noOrderCancel35F\": true", report, StringComparison.Ordinal);
        Assert.Contains("\"noOrderCancelReplace35G\": true", report, StringComparison.Ordinal);
        Assert.Contains("\"dbQueriesReadOnly\": true", report, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(root, "A.txt", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(root, "H.txt", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(root, "I.txt", SearchOption.AllDirectories));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(root, "*order*", SearchOption.AllDirectories),
            x => !x.Contains("no_production_execution_path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Bar_ids_and_canonical_timing_are_explicitly_present_or_missing()
    {
        var root = TempRoot("system-audit-r002-adoption-timing");

        await SystemAuditR002ContractAdoption.RunAsync(Request(root), CancellationToken.None);

        var adoption = File.ReadAllText(Path.Combine(root, "10_validation", "system_audit_r002_contract_adoption_report.json"));
        Assert.Contains("\"M15_BAR_ID_EVIDENCE\": \"PRESENT\"", adoption, StringComparison.Ordinal);
        Assert.Contains("\"M30_BAR_ID_EVIDENCE\": \"MISSING\"", adoption, StringComparison.Ordinal);
        Assert.Contains("\"M60_BAR_ID_EVIDENCE\": \"PRESENT\"", adoption, StringComparison.Ordinal);
        Assert.Contains("\"CANONICAL_TIMING_EVIDENCE\": \"MISSING\"", adoption, StringComparison.Ordinal);

        var canonical = File.ReadAllText(Path.Combine(root, "10_validation", "canonical_timing_v1_adoption.json"));
        Assert.Contains("\"CanonicalTargetCloseUtc\": null", canonical, StringComparison.Ordinal);
        Assert.Contains("\"WindowStartUtc\": null", canonical, StringComparison.Ordinal);
        Assert.Contains("\"WindowEndUtc\": null", canonical, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Row_counts_are_reported_as_missing_without_db_connection()
    {
        var root = TempRoot("system-audit-r002-adoption-rowcounts");

        await SystemAuditR002ContractAdoption.RunAsync(Request(root), CancellationToken.None);

        var readiness = File.ReadAllText(Path.Combine(root, "10_validation", "marketdata_readiness_v1_adoption.json"));
        Assert.Contains("MISSING_NO_DB_CONNECTION", readiness, StringComparison.Ordinal);
        var matrix = File.ReadAllText(Path.Combine(root, "10_validation", "system_audit_r002_evidence_matrix.json"));
        Assert.Contains("\"EvidenceName\": \"row counts\"", matrix, StringComparison.Ordinal);
        Assert.Contains("\"EvidenceStatus\": \"MISSING\"", matrix, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gap_closure_marks_m30_missing_confirmed_after_scan()
    {
        var root = TempRoot("system-audit-r002-gap-closure-m30-missing");
        var request = Request(root) with { RunKey = "system-audit-r002-gap-closure-m30-missing" };

        await SystemAuditR002ContractAdoption.RunAsync(request, CancellationToken.None);

        var report = File.ReadAllText(Path.Combine(root, "10_validation", "system_audit_r002_gap_closure_report.json"));
        Assert.Contains("\"M30_BAR_ID_EVIDENCE\": \"MISSING_CONFIRMED\"", report, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "10_validation", "m30_unsupported_or_missing_evidence.json")));
    }

    [Fact]
    public async Task Gap_closure_can_find_m30_from_scanned_config()
    {
        var repo = Path.Combine(Path.GetTempPath(), "qq-system-audit-r002-tests", Guid.NewGuid().ToString("N"), "repo");
        Directory.CreateDirectory(Path.Combine(repo, "src"));
        File.WriteAllText(Path.Combine(repo, "src", "Timeframes.cs"), "public enum TestFrame { ThirtyMinutes }");
        var root = TempRoot("system-audit-r002-gap-closure-m30-present");
        var request = Request(root) with { RunKey = "system-audit-r002-gap-closure-m30-present", RepoRoot = repo };

        await SystemAuditR002ContractAdoption.RunAsync(request, CancellationToken.None);

        var report = File.ReadAllText(Path.Combine(root, "10_validation", "system_audit_r002_gap_closure_report.json"));
        Assert.Contains("\"M30_BAR_ID_EVIDENCE\": \"PRESENT\"", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gap_closure_valid_utc_canonical_timing_is_present()
    {
        var root = TempRoot("system-audit-r002-gap-closure-timing-valid");
        var request = Request(root) with
        {
            RunKey = "system-audit-r002-gap-closure-timing-valid",
            CanonicalTargetCloseUtc = "2026-05-26T14:30:00Z",
            WindowStartUtc = "2026-05-26T14:15:00Z",
            WindowEndUtc = "2026-05-26T14:45:00Z"
        };

        await SystemAuditR002ContractAdoption.RunAsync(request, CancellationToken.None);

        var canonical = File.ReadAllText(Path.Combine(root, "10_validation", "canonical_timing_v1_gap_closure.json"));
        Assert.Contains("\"validationStatus\": \"PASS\"", canonical, StringComparison.Ordinal);
        var report = File.ReadAllText(Path.Combine(root, "10_validation", "system_audit_r002_gap_closure_report.json"));
        Assert.Contains("\"CANONICAL_TIMING_EVIDENCE\": \"PRESENT\"", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gap_closure_invalid_window_order_fails_canonical_timing()
    {
        var root = TempRoot("system-audit-r002-gap-closure-timing-invalid");
        var request = Request(root) with
        {
            RunKey = "system-audit-r002-gap-closure-timing-invalid",
            CanonicalTargetCloseUtc = "2026-05-26T14:00:00Z",
            WindowStartUtc = "2026-05-26T14:15:00Z",
            WindowEndUtc = "2026-05-26T14:45:00Z"
        };

        await SystemAuditR002ContractAdoption.RunAsync(request, CancellationToken.None);

        var canonical = File.ReadAllText(Path.Combine(root, "10_validation", "canonical_timing_v1_gap_closure.json"));
        Assert.Contains("\"validationStatus\": \"FAIL\"", canonical, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gap_closure_non_utc_canonical_timing_fails()
    {
        var root = TempRoot("system-audit-r002-gap-closure-timing-non-utc");
        var request = Request(root) with
        {
            RunKey = "system-audit-r002-gap-closure-timing-non-utc",
            CanonicalTargetCloseUtc = "2026-05-26T14:30:00+02:00",
            WindowStartUtc = "2026-05-26T14:15:00Z",
            WindowEndUtc = "2026-05-26T14:45:00Z"
        };

        await SystemAuditR002ContractAdoption.RunAsync(request, CancellationToken.None);

        var canonical = File.ReadAllText(Path.Combine(root, "10_validation", "canonical_timing_v1_gap_closure.json"));
        Assert.Contains("\"validationStatus\": \"FAIL\"", canonical, StringComparison.Ordinal);
        Assert.Contains("non-UTC", canonical, StringComparison.Ordinal);
    }

    [Fact]
    public void Cli_db_scanner_uses_select_only_queries()
    {
        var program = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "QQ.Production.Intraday.Tools.SystemAuditR002ContractAdoption", "Program.cs"));

        Assert.Contains("SELECT COUNT_BIG(*)", program, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT ", program, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", program, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", program, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MERGE ", program, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TRUNCATE ", program, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP ", program, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER ", program, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migrate(", program, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureCreated(", program, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gap_closure_row_counts_and_tick_schema_can_be_present_from_select_only_scan_results()
    {
        var root = TempRoot("system-audit-r002-gap-closure-db");
        var request = Request(root) with
        {
            RunKey = "system-audit-r002-gap-closure-db",
            ConnectionStringEnv = "QQPRODUCTIONINTRADAY_CONNECTION_STRING",
            ConnectionStringPresent = true,
            ConnectionStringRedactedHash = "abc123",
            DbTables =
            [
                new("dbo", "LmaxIndividualTrades", "tick", ["Id", "TransactionTimeUtc", "Price", "Quantity", "SecurityId"], ["TransactionTimeUtc"], ["SecurityId"], [], [], ["Quantity"], 5, "2026-05-26T10:00:00Z", "2026-05-26T10:01:00Z", "PRESENT"),
                new("dbo", "MarketDataSnapshots", "quote", ["Id", "SourceTimestampUtc", "Bid", "Ask", "InstrumentId"], ["SourceTimestampUtc"], ["InstrumentId"], ["Bid", "Ask"], [], [], 7, "2026-05-26T10:00:00Z", "2026-05-26T10:01:00Z", "PRESENT"),
                new("dbo", "MarketDataBars", "bar", ["Id", "BarStartUtc", "Open", "High", "Low", "Close"], ["BarStartUtc"], ["InstrumentId"], [], ["Open", "High", "Low", "Close"], ["ObservationCount"], 3, "2026-05-26T10:00:00Z", "2026-05-26T10:30:00Z", "PRESENT")
            ]
        };

        await SystemAuditR002ContractAdoption.RunAsync(request, CancellationToken.None);

        var report = File.ReadAllText(Path.Combine(root, "10_validation", "system_audit_r002_gap_closure_report.json"));
        Assert.Contains("\"ROW_COUNTS_EVIDENCE\": \"PRESENT\"", report, StringComparison.Ordinal);
        Assert.Contains("\"DB_TICK_SCHEMA_EVIDENCE\": \"PRESENT\"", report, StringComparison.Ordinal);
        Assert.Contains("\"DB_QUOTE_SCHEMA_EVIDENCE\": \"PRESENT\"", report, StringComparison.Ordinal);
        Assert.Contains("\"DB_BAR_SCHEMA_EVIDENCE\": \"PRESENT\"", report, StringComparison.Ordinal);
        var allText = string.Join(Environment.NewLine, Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain("Server=", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", allText, StringComparison.OrdinalIgnoreCase);
    }

    private static SystemAuditR002ContractAdoptionRequest Request(string root)
        => new(
            Path.GetFileName(root),
            RepoRoot(),
            root,
            NoExternal: true,
            NoExecution: true,
            ConnectionStringEnv: null,
            ConnectionStringPresent: false,
            ConnectionStringRedactedHash: null,
            DbTables: [],
            CanonicalTargetCloseUtc: null,
            WindowStartUtc: null,
            WindowEndUtc: null,
            QuoteWindowReadinessId: null,
            CloseBenchmarkReadinessId: null,
            FeedQualityReadinessId: null);

    private static string[] RequiredBasenames()
        =>
        [
            "system_audit_r002_contract_adoption_report",
            "system_audit_r002_evidence_matrix",
            "lmax_marketdata_db_v1_adoption",
            "marketdata_readiness_v1_adoption",
            "canonical_timing_v1_adoption",
            "environment_secret_v1_adoption",
            "no_production_execution_path_report"
        ];

    private static string TempRoot(string runKey)
    {
        var root = Path.Combine(Path.GetTempPath(), "qq-system-audit-r002-tests", Guid.NewGuid().ToString("N"), runKey);
        Directory.CreateDirectory(root);
        return root;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }

    private static void WithTemporaryEnv(string name, string value, out Action restore)
    {
        var previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
        restore = () => Environment.SetEnvironmentVariable(name, previous);
    }
}
