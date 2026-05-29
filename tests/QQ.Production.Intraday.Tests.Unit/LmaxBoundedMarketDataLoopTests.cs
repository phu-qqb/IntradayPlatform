using System.Security.Cryptography;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxBoundedMarketDataLoopTests
{
    [Fact]
    public async Task Fake_bounded_loop_produces_logon_snapshot_incremental_and_pass_summary()
    {
        var output = TempRoot("lmax-bounded-md-loop-fake-test");
        var result = await new LmaxBoundedMarketDataLoopRunner().RunAsync(Options(output), CancellationToken.None);

        Assert.Equal("PASS", result.Preflight.Gate);
        Assert.Equal("PASS", result.Summary.Status);
        Assert.Equal("fake", result.Summary.Source);
        Assert.True(result.Summary.MessagesRead >= 3);
        Assert.True(result.Summary.Logons >= 1);
        Assert.True(result.Summary.Snapshots >= 1);
        Assert.True(result.Summary.Incrementals >= 1);
        Assert.Equal(0, result.Summary.SessionRejects);
        Assert.Equal(0, result.Summary.CredentialsRejected);
        Assert.Equal("NONE", result.Summary.PrimaryFailureReason);
        Assert.Equal("MarketDataObserved", result.Summary.FinalFixSessionState);
        Assert.Equal("YES", result.Summary.BoundedCaptureEndedCleanly);
        Assert.Contains(result.Events, x => x.Classification == "Logon" && x.FixMsgType == "A");
        Assert.Contains(result.Events, x => x.Classification == "Snapshot" && x.Bid is not null && x.Ask is not null);
        Assert.Contains(result.Events, x => x.Classification == "Incremental" && x.BidSize is not null && x.AskSize is not null);
    }

    [Fact]
    public async Task Terminal_timeout_after_market_data_is_not_primary_failure()
    {
        var output = TempRoot("lmax-bounded-md-loop-timeout-test");
        var result = await new LmaxBoundedMarketDataLoopRunner().RunAsync(Options(output), CancellationToken.None);

        Assert.Equal(1, result.Summary.TerminalTimeouts);
        Assert.Equal("NONE", result.Summary.PrimaryFailureReason);
        Assert.Equal("PASS", result.Summary.Status);
    }

    [Fact]
    public async Task Writer_produces_required_artifacts_jsonl_fields_and_manifest_hashes()
    {
        var output = TempRoot("lmax-bounded-md-loop-writer-test");
        var options = Options(output);
        var result = await new LmaxBoundedMarketDataLoopRunner().RunAsync(options, CancellationToken.None);
        await new LmaxBoundedMarketDataLoopReports().WriteAsync(options, result, CancellationToken.None);

        var eventsPath = Path.Combine(output, "marketdata", "lmax_marketdata_events.jsonl");
        var summaryPath = Path.Combine(output, "marketdata", "lmax_marketdata_summary.json");
        var capturePath = Path.Combine(output, "10_validation", "lmax_marketdata_loop_capture_report.json");
        var boundaryPath = Path.Combine(output, "10_validation", "lmax_marketdata_loop_boundary_report.json");
        var fakeValidationPath = Path.Combine(output, "10_validation", "lmax_marketdata_loop_fake_validation_report.json");
        Assert.True(File.Exists(eventsPath));
        Assert.True(File.Exists(summaryPath));
        Assert.True(File.Exists(capturePath));
        Assert.True(File.Exists(boundaryPath));
        Assert.True(File.Exists(fakeValidationPath));
        Assert.True(File.Exists(Path.Combine(output, "share", "lmax_marketdata_loop_fake_summary.md")));

        var firstEvent = JsonDocument.Parse(File.ReadLines(eventsPath).First()).RootElement;
        Assert.Equal(Path.GetFileName(output), firstEvent.GetProperty("run_key").GetString());
        Assert.Equal("fake", firstEvent.GetProperty("source").GetString());
        Assert.Equal("GBPUSD", firstEvent.GetProperty("instrument").GetString());
        Assert.Equal("4002", firstEvent.GetProperty("security_id").GetString());
        Assert.Equal("8", firstEvent.GetProperty("security_id_source").GetString());
        Assert.True(firstEvent.TryGetProperty("fix_msg_type", out _));
        Assert.True(firstEvent.TryGetProperty("classification", out _));
        Assert.True(firstEvent.TryGetProperty("raw_redacted_fix", out _));

        var summary = JsonDocument.Parse(File.ReadAllText(summaryPath)).RootElement;
        Assert.Equal("PASS", summary.GetProperty("status").GetString());
        Assert.Equal("NONE", summary.GetProperty("primary_failure_reason").GetString());
        Assert.Equal("MarketDataObserved", summary.GetProperty("final_fix_session_state").GetString());

        var capture = File.ReadAllText(capturePath);
        Assert.Contains("\"LMAX_BOUNDED_LOOP_FAKE_STATUS\": \"PASS\"", capture, StringComparison.Ordinal);
        Assert.Contains("\"LMAX_BOUNDED_LOOP_DEMO_STATUS\": \"PASS\"", capture, StringComparison.Ordinal);
        Assert.Contains("\"LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED\": \"NO\"", capture, StringComparison.Ordinal);
        Assert.Contains("\"LMAX_BOUNDED_LOOP_ARTIFACTS_ONLY\": \"YES\"", capture, StringComparison.Ordinal);
        Assert.Contains("\"LMAX_BOUNDED_LOOP_NO_DB_WRITE\": \"YES\"", capture, StringComparison.Ordinal);
        Assert.Contains("\"LMAX_BOUNDED_LOOP_NO_EXECUTION\": \"YES\"", capture, StringComparison.Ordinal);
        Assert.Contains("\"LMAX_BOUNDED_LOOP_NO_PRODUCTION_ENDPOINT\": \"YES\"", capture, StringComparison.Ordinal);
        Assert.Contains("\"LMAX_BOUNDED_LOOP_NO_TRADING_ENDPOINT\": \"YES\"", capture, StringComparison.Ordinal);
        Assert.Contains("\"LMAX_BOUNDED_LOOP_NO_SCHEDULER\": \"YES\"", capture, StringComparison.Ordinal);
        Assert.Contains("\"MARKETDATA_LMAX_DB_STATUS\": \"AdoptedWithWarnings\"", capture, StringComparison.Ordinal);

        var manifest = File.ReadAllText(Path.Combine(output, "manifest.json"));
        Assert.Contains("marketdata/lmax_marketdata_events.jsonl", manifest, StringComparison.Ordinal);
        Assert.Contains("marketdata/lmax_marketdata_summary.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/lmax_marketdata_loop_preflight_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/lmax_marketdata_loop_capture_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/lmax_marketdata_loop_boundary_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/lmax_marketdata_loop_fake_validation_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("10_validation/lmax_marketdata_loop_secret_scan_report.json", manifest, StringComparison.Ordinal);
        Assert.Contains("share/lmax_marketdata_loop_fake_summary.md", manifest, StringComparison.Ordinal);

        var manifestHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(output, "manifest.json")))).ToLowerInvariant();
        Assert.Equal($"{manifestHash}  manifest.json", File.ReadAllText(Path.Combine(output, "manifest.sha256")).Trim());

        var hashes = File.ReadAllText(Path.Combine(output, "hashes.json"));
        Assert.Contains("manifest.json", hashes, StringComparison.Ordinal);
        Assert.DoesNotContain("hashes.json", hashes, StringComparison.Ordinal);
        Assert.DoesNotContain("manifest.sha256", hashes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Boundary_blocks_external_production_trading_db_execution_scheduler_and_qubes()
    {
        var output = TempRoot("lmax-bounded-md-loop-boundary-test");
        var options = Options(output);
        var result = await new LmaxBoundedMarketDataLoopRunner().RunAsync(options, CancellationToken.None);
        await new LmaxBoundedMarketDataLoopReports().WriteAsync(options, result, CancellationToken.None);

        Assert.Equal("NO", result.BoundaryReport["LMAX_EXTERNAL_CALLS_ATTEMPTED"]);
        Assert.Equal("NO", result.BoundaryReport["PRODUCTION_ENDPOINT_USED"]);
        Assert.Equal("NO", result.BoundaryReport["TRADING_ENDPOINT_USED"]);
        Assert.Equal("NO", result.BoundaryReport["ORDER_MESSAGES_SENT"]);
        Assert.Equal("NO", result.BoundaryReport["DB_WRITE_ATTEMPTED"]);
        Assert.Equal("NO", result.BoundaryReport["MIGRATION_ATTEMPTED"]);
        Assert.Equal("NO", result.BoundaryReport["QUBES_EXECUTED"]);
        Assert.Equal("NO", result.BoundaryReport["PMS_OMS_EMS_TOUCHED"]);
        Assert.Equal("NO", result.BoundaryReport["MANAGER_ANUBIS_TOUCHED"]);
        Assert.Equal("NO", result.BoundaryReport["A_H_I_CREATED"]);
        Assert.Equal("NO", result.BoundaryReport["SCHEDULER_STARTED"]);
        Assert.Equal("NO", result.BoundaryReport["WORKER_SERVICE_STARTED"]);
        Assert.Equal("NO", result.BoundaryReport["CREDENTIAL_VALUES_PERSISTED"]);
        Assert.Equal("AdoptedWithWarnings", result.BoundaryReport["MARKETDATA_LMAX_DB_STATUS"]);

        Assert.Empty(Directory.EnumerateFiles(output, "A.txt", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "H.txt", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "I.txt", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "*order*", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "*route*", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "*broker*", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(output, "*live-state*", SearchOption.AllDirectories));

        var allText = string.Join(Environment.NewLine, Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain("fix-order.london-demo.lmax.com", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fix-live", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("35=D", allText, StringComparison.Ordinal);
        Assert.DoesNotContain("35=F", allText, StringComparison.Ordinal);
        Assert.DoesNotContain("35=G", allText, StringComparison.Ordinal);
        Assert.DoesNotContain("554=", allText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_secrets_block_external_demo_without_socket_attempt()
    {
        var result = await new LmaxBoundedMarketDataLoopRunner().RunAsync(
            Options(
                TempRoot("lmax-bounded-md-loop-missing-secrets"),
                noExternal: false,
                useFakeFixServer: false,
                externalApproved: true,
                approval: LmaxBoundedMarketDataLoopOptions.ApprovalPhrase,
                usernameSecretRef: "QQ_TEST_MISSING_LMAX_USER",
                passwordSecretRef: "QQ_TEST_MISSING_LMAX_PASSWORD"),
            CancellationToken.None);

        Assert.Equal("FAIL", result.Preflight.Gate);
        Assert.Equal("NOT_ATTEMPTED", result.Summary.Status);
        Assert.Equal("MISSING_DEMO_MARKETDATA_SECRET_REFS", result.Summary.PrimaryFailureReason);
        Assert.Equal("NO", result.CaptureReport["LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED"]);
    }

    [Fact]
    public async Task Missing_operator_approval_blocks_external_demo_without_socket_attempt()
    {
        Environment.SetEnvironmentVariable("QQ_TEST_LMAX_USER", "redacted-test-user");
        Environment.SetEnvironmentVariable("QQ_TEST_LMAX_PASSWORD", "redacted-test-password");
        try
        {
            var result = await new LmaxBoundedMarketDataLoopRunner().RunAsync(
                Options(
                    TempRoot("lmax-bounded-md-loop-missing-approval"),
                    noExternal: false,
                    useFakeFixServer: false,
                    externalApproved: true,
                    approval: "WRONG_APPROVAL",
                    usernameSecretRef: "QQ_TEST_LMAX_USER",
                    passwordSecretRef: "QQ_TEST_LMAX_PASSWORD"),
                CancellationToken.None);

            Assert.Equal("FAIL", result.Preflight.Gate);
            Assert.Equal("NOT_ATTEMPTED", result.Summary.Status);
            Assert.Equal("MISSING_OPERATOR_APPROVAL", result.Summary.PrimaryFailureReason);
            Assert.Equal("NO", result.CaptureReport["LMAX_BOUNDED_LOOP_EXTERNAL_CALLS_ATTEMPTED"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("QQ_TEST_LMAX_USER", null);
            Environment.SetEnvironmentVariable("QQ_TEST_LMAX_PASSWORD", null);
        }
    }

    [Fact]
    public void Use_fake_fix_server_false_fails_when_no_external_is_true()
    {
        var report = LmaxBoundedMarketDataLoopRunner.Evaluate(Options(TempRoot("lmax-bounded-md-loop-fake-fail"), useFakeFixServer: false));

        Assert.Equal("FAIL", report.Gate);
        Assert.Contains(report.Failures, x => x.Contains("useFakeFixServer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Preflight_blocks_trading_endpoint_and_target_comp_id()
    {
        var report = LmaxBoundedMarketDataLoopRunner.Evaluate(
            Options(
                TempRoot("lmax-bounded-md-loop-trading-endpoint-fail"),
                host: LmaxBoundedMarketDataLoopOptions.DemoOrderHost,
                targetCompId: LmaxBoundedMarketDataLoopOptions.DemoOrderTargetCompId));

        Assert.Equal("FAIL", report.Gate);
        Assert.False(report.MarketDataEndpointOnly);
        Assert.False(report.TradingEndpointBlocked);
        Assert.False(report.TargetCompIdMarketDataOnly);
    }

    [Fact]
    public void Preflight_blocks_no_execution_false()
    {
        var report = LmaxBoundedMarketDataLoopRunner.Evaluate(
            Options(
                TempRoot("lmax-bounded-md-loop-no-execution-fail"),
                noExecution: false));

        Assert.Equal("FAIL", report.Gate);
        Assert.False(report.NoExecution);
    }

    private static LmaxBoundedMarketDataLoopOptions Options(
        string output,
        bool noExternal = true,
        bool noExecution = true,
        bool useFakeFixServer = true,
        bool externalApproved = false,
        string? approval = null,
        string host = LmaxBoundedMarketDataLoopOptions.DemoMarketDataHost,
        string targetCompId = LmaxBoundedMarketDataLoopOptions.DemoMarketDataTargetCompId,
        string usernameSecretRef = "LMAX_DEMO_MD_USERNAME",
        string passwordSecretRef = "LMAX_DEMO_MD_PASSWORD")
        => new(
            Path.GetFileName(output),
            output,
            "demo",
            host,
            443,
            targetCompId,
            "QQPRODMD",
            usernameSecretRef,
            passwordSecretRef,
            noExternal,
            externalApproved,
            approval,
            noExecution,
            useFakeFixServer,
            30,
            100,
            0,
            ["GBPUSD"]);

    private static string TempRoot(string leaf)
    {
        var root = Path.Combine(Path.GetTempPath(), "qq-lmax-bounded-md-loop-tests", Guid.NewGuid().ToString("N"), leaf);
        Directory.CreateDirectory(root);
        return root;
    }
}
