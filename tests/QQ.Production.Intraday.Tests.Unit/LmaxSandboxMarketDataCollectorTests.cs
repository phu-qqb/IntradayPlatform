using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxSandboxMarketDataCollectorTests
{
    [Fact]
    public void Preflight_passes_demo_market_data_endpoint()
    {
        var report = LmaxSandboxMarketDataPreflight.Evaluate(Options());

        Assert.Equal("PASS", report.LmaxSandboxPreflightGate);
        Assert.True(report.MarketDataEndpointOnly);
        Assert.True(report.TradingEndpointBlocked);
        Assert.Single(report.InstrumentMappings);
    }

    [Fact]
    public void Preflight_fails_order_host()
    {
        var report = LmaxSandboxMarketDataPreflight.Evaluate(Options(host: LmaxSandboxMarketDataOptions.DemoOrderHost));

        Assert.Equal("FAIL", report.LmaxSandboxPreflightGate);
        Assert.False(report.TradingEndpointBlocked);
    }

    [Fact]
    public void Preflight_fails_trading_target_comp_id()
    {
        var report = LmaxSandboxMarketDataPreflight.Evaluate(Options(targetCompId: LmaxSandboxMarketDataOptions.DemoOrderTargetCompId));

        Assert.Equal("FAIL", report.LmaxSandboxPreflightGate);
        Assert.False(report.TargetCompIdMarketDataOnly);
    }

    [Fact]
    public void Preflight_fails_no_execution_false()
    {
        var report = LmaxSandboxMarketDataPreflight.Evaluate(Options(noExecution: false));

        Assert.Equal("FAIL", report.LmaxSandboxPreflightGate);
        Assert.Contains(report.Failures, x => x.Contains("NoExecution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Preflight_fails_disable_order_path_false()
    {
        var report = LmaxSandboxMarketDataPreflight.Evaluate(Options(disableOrderPath: false));

        Assert.Equal("FAIL", report.LmaxSandboxPreflightGate);
        Assert.False(report.OrderPathDisabled);
    }

    [Fact]
    public void Preflight_fails_external_approval_without_phrase()
    {
        var report = LmaxSandboxMarketDataPreflight.Evaluate(Options(noExternal: false, externalApproved: true, useFake: false, approval: null));

        Assert.Equal("FAIL", report.LmaxSandboxPreflightGate);
        Assert.False(report.ExternalApprovalValid);
    }

    [Fact]
    public void Preflight_fails_unbounded_duration()
    {
        var report = LmaxSandboxMarketDataPreflight.Evaluate(Options(durationSeconds: 121));

        Assert.Equal("FAIL", report.LmaxSandboxPreflightGate);
        Assert.False(report.DurationBounded);
    }

    [Fact]
    public void Preflight_fails_excessive_max_messages()
    {
        var report = LmaxSandboxMarketDataPreflight.Evaluate(Options(maxMessages: 10001));

        Assert.Equal("FAIL", report.LmaxSandboxPreflightGate);
        Assert.False(report.MaxMessagesBounded);
    }

    [Fact]
    public void Preflight_fails_invalid_md_update_type()
    {
        var report = LmaxSandboxMarketDataPreflight.Evaluate(Options(mdUpdateType: 2));

        Assert.Equal("FAIL", report.LmaxSandboxPreflightGate);
        Assert.False(report.MdUpdateTypeValid);
        Assert.Equal("FAIL", report.MdUpdateTypeGate);
    }

    [Fact]
    public void Preflight_warns_when_md_update_type_one_is_explicit()
    {
        var report = LmaxSandboxMarketDataPreflight.Evaluate(Options(mdUpdateType: 1));

        Assert.Equal("PASS", report.LmaxSandboxPreflightGate);
        Assert.True(report.MdUpdateTypeValid);
        Assert.Equal("WARN", report.MdUpdateTypeGate);
        Assert.Contains(report.Warnings, x => x.Contains("md-update-type 1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Preflight_fails_credential_shaped_secret_in_report()
    {
        var report = LmaxSandboxMarketDataPreflight.Evaluate(Options(passwordSecretRef: "password=do-not-print"));

        Assert.Equal("FAIL", report.LmaxSandboxPreflightGate);
        Assert.False(report.SecretsNotExposed);
    }

    [Fact]
    public void Preflight_fails_unknown_instrument_mapping()
    {
        var report = LmaxSandboxMarketDataPreflight.Evaluate(Options(instruments: ["NQ"]));

        Assert.Equal("FAIL", report.LmaxSandboxPreflightGate);
        Assert.False(report.InstrumentsAllowListed);
    }

    [Fact]
    public async Task Fake_transport_is_used_with_no_external_and_opens_no_socket()
    {
        var transport = new FakeFixTransport();
        var result = await new LmaxSandboxMarketDataCollector(transport).RunAsync(Options(), CancellationToken.None);

        Assert.False(result.ExternalCallsAttempted);
        Assert.Equal("fake", result.Summary.Source);
        Assert.True(result.Summary.MessagesRead > 0);
    }

    [Fact]
    public async Task No_external_without_fake_fails_before_transport_socket()
    {
        var result = await new LmaxSandboxMarketDataCollector(new TlsTcpFixTransport()).RunAsync(Options(useFake: false), CancellationToken.None);

        Assert.Equal("FAIL", result.Preflight.LmaxSandboxPreflightGate);
        Assert.False(result.ExternalCallsAttempted);
    }

    [Fact]
    public void Fix_policy_allows_market_data_request()
    {
        var decision = LmaxMarketDataFixMessagePolicy.ValidateOutgoing(FakeFixTransport.FixMessage("35=V"), Options());

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Fix_policy_allows_logon_only_when_external_approved()
    {
        var blocked = LmaxMarketDataFixMessagePolicy.ValidateOutgoing(FakeFixTransport.FixMessage("35=A"), Options());
        var allowed = LmaxMarketDataFixMessagePolicy.ValidateOutgoing(
            FakeFixTransport.FixMessage("35=A"),
            Options(noExternal: false, externalApproved: true, useFake: false, approval: LmaxSandboxMarketDataOptions.ApprovalPhrase));

        Assert.False(blocked.Allowed);
        Assert.True(allowed.Allowed);
    }

    [Theory]
    [InlineData("D")]
    [InlineData("F")]
    [InlineData("G")]
    public void Fix_policy_blocks_trading_messages(string msgType)
    {
        var decision = LmaxMarketDataFixMessagePolicy.ValidateOutgoing(FakeFixTransport.FixMessage($"35={msgType}"), Options());

        Assert.False(decision.Allowed);
        Assert.Equal("TradingMessageForbidden", decision.Category);
    }

    [Fact]
    public void Lmax_market_data_request_defaults_md_update_type_to_zero()
    {
        var request = LmaxSandboxMarketDataCollector.BuildMarketDataRequest(
            Options(),
            [LmaxSandboxInstrumentMapping.Find("GBPUSD")!]);

        Assert.Equal("1", LmaxFixMessageClassifier.GetField(request, "263"));
        Assert.Equal("0", LmaxFixMessageClassifier.GetField(request, "264"));
        Assert.Equal("0", LmaxFixMessageClassifier.GetField(request, "265"));
        Assert.Equal("2", LmaxFixMessageClassifier.GetField(request, "267"));
        Assert.Equal("1", LmaxFixMessageClassifier.GetField(request, "146"));
        Assert.Equal("4002", LmaxFixMessageClassifier.GetField(request, "48"));
        Assert.Equal("8", LmaxFixMessageClassifier.GetField(request, "22"));
    }

    [Fact]
    public void Lmax_market_data_request_emits_single_instrument_wire_shape()
    {
        var request = LmaxSandboxMarketDataCollector.BuildMarketDataRequest(
            Options(),
            [LmaxSandboxInstrumentMapping.Find("GBPUSD")!]);
        var order = LmaxFixMessageClassifier.GetTagOrder(request).ToArray();
        var tag146 = Array.IndexOf(order, "146");
        var tag48 = Array.IndexOf(order, "48");
        var tag22 = Array.IndexOf(order, "22");
        var tag267 = Array.IndexOf(order, "267");
        var tag269Values = LmaxFixMessageClassifier.GetFields(request, "269");

        Assert.True(tag146 > -1);
        Assert.True(tag48 > tag146);
        Assert.True(tag22 > tag48);
        Assert.True(tag267 > tag22);
        Assert.Equal("1", LmaxFixMessageClassifier.GetField(request, "146"));
        Assert.Equal("4002", LmaxFixMessageClassifier.GetField(request, "48"));
        Assert.Equal("8", LmaxFixMessageClassifier.GetField(request, "22"));
        Assert.Equal("2", LmaxFixMessageClassifier.GetField(request, "267"));
        Assert.Equal(["0", "1"], tag269Values);
        Assert.DoesNotContain("55", order);

        var diagnosis = LmaxSandboxMarketDataCollector.DiagnoseMarketDataRequestGroup(
            Options(),
            [LmaxSandboxInstrumentMapping.Find("GBPUSD")!]);
        Assert.Equal("YES", diagnosis.DidOutgoingRequestContainExactlyOneValidInstrumentGroupAfter146);
        Assert.Equal(1, diagnosis.ComputedInstrumentGroupsAfter146);
        Assert.Equal(2, diagnosis.ComputedMdEntryTypeGroupsAfter267);
    }

    [Fact]
    public void Lmax_market_data_request_requires_security_id()
    {
        var mapping = new LmaxSandboxInstrumentMapping("GBPUSD", "", "8", "test", "LOW");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            LmaxSandboxMarketDataCollector.BuildMarketDataRequest(Options(), [mapping]));

        Assert.Contains("SecurityID", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Lmax_market_data_request_requires_security_id_source()
    {
        var mapping = new LmaxSandboxInstrumentMapping("GBPUSD", "4002", "", "test", "LOW");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            LmaxSandboxMarketDataCollector.BuildMarketDataRequest(Options(), [mapping]));

        Assert.Contains("SecurityIDSource", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Lmax_market_data_request_honors_explicit_md_update_type_override()
    {
        var request = LmaxSandboxMarketDataCollector.BuildMarketDataRequest(
            Options(mdUpdateType: 1),
            [LmaxSandboxInstrumentMapping.Find("GBPUSD")!]);

        Assert.Equal("1", LmaxFixMessageClassifier.GetField(request, "265"));
    }

    [Theory]
    [InlineData("W", "Snapshot")]
    [InlineData("X", "Incremental")]
    [InlineData("Y", "Reject")]
    [InlineData("3", "Reject")]
    [InlineData("A", "Logon")]
    public void Parser_classifies_market_data_messages(string msgType, string expected)
    {
        var result = LmaxFixMessageClassifier.Classify(FakeFixTransport.FixMessage($"35={msgType}", "55=GBPUSD", "268=1", "269=0", "270=1.25", "271=100"));

        Assert.Equal(expected, result.Classification);
    }

    [Fact]
    public void Parser_does_not_classify_logon_as_unknown()
    {
        var result = LmaxFixMessageClassifier.Classify(FakeFixTransport.FixMessage("35=A", "49=LMXBDM", "56=QQPRODMD"));

        Assert.Equal("Logon", result.Classification);
        Assert.NotEqual("Unknown", result.Classification);
    }

    [Fact]
    public void Parser_classifies_timeout()
    {
        var result = LmaxFixMessageClassifier.Classify(null);

        Assert.Equal("Timeout", result.Classification);
    }

    [Fact]
    public void Parser_classifies_malformed_frame()
    {
        var result = LmaxFixMessageClassifier.Classify("this-is-not-fix");

        Assert.Equal("Malformed frame", result.Classification);
    }

    [Fact]
    public void Parser_classifies_bad_credentials_logout()
    {
        var result = LmaxFixMessageClassifier.Classify(FakeFixTransport.FixMessage("35=5", "58=BAD_CREDENTIALS"));

        Assert.Equal("5", result.FixMsgType);
        Assert.Equal("CredentialsRejected", result.Classification);
    }

    [Fact]
    public void Parser_classifies_md_update_type_value_out_of_range_session_reject()
    {
        var result = LmaxFixMessageClassifier.Classify(FakeFixTransport.FixMessage(
            "35=3",
            "371=265",
            "372=V",
            "58=Error code ValueOutOfRange Tag ID is 265"));

        Assert.Equal("3", result.FixMsgType);
        Assert.Equal("Reject", result.Classification);
        Assert.Equal("265", result.RefTagId);
        Assert.Equal("V", result.RefMsgType);
        Assert.Contains("ValueOutOfRange", result.RejectText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parser_classifies_related_sym_group_mismatch_session_reject()
    {
        var result = LmaxFixMessageClassifier.Classify(FakeFixTransport.FixMessage(
            "35=3",
            "371=146",
            "372=V",
            "58=Error code RepeatingGroupNumInGroupMismatch occurred while parsing a FIX message. Tag ID is 146."));

        Assert.Equal("3", result.FixMsgType);
        Assert.Equal("Reject", result.Classification);
        Assert.Equal("146", result.RefTagId);
        Assert.Equal("V", result.RefMsgType);
        Assert.Contains("RepeatingGroupNumInGroupMismatch", result.RejectText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parser_redacts_username_secret_when_echoed_in_session_fields()
    {
        WithTemporaryEnv("LMAX_DEMO_MD_USERNAME", "demo-user-for-test", out var restoreUser);
        try
        {
            var result = LmaxFixMessageClassifier.Classify(FakeFixTransport.FixMessage("35=3", "49=LMXBDM", "56=demo-user-for-test", "58=reject"));

            Assert.DoesNotContain("demo-user-for-test", result.RawRedactedFix, StringComparison.Ordinal);
            Assert.Contains("56=[redacted]", result.RawRedactedFix, StringComparison.Ordinal);
        }
        finally
        {
            restoreUser();
        }
    }

    [Fact]
    public async Task External_logon_uses_lmax_username_for_tag_49_and_553()
    {
        WithTemporaryEnv("LMAX_DEMO_MD_USERNAME", "demo-user-for-test", out var restoreUser);
        WithTemporaryEnv("LMAX_DEMO_MD_PASSWORD", "demo-password-for-test", out var restorePassword);
        try
        {
            var transport = new CapturingFixTransport([FakeFixTransport.FixMessage("35=5", "58=BAD_CREDENTIALS")]);
            var options = Options(noExternal: false, externalApproved: true, useFake: false, approval: LmaxSandboxMarketDataOptions.ApprovalPhrase);

            await new LmaxSandboxMarketDataCollector(transport).RunAsync(options, CancellationToken.None);

            var logon = transport.SentMessages.First();
            Assert.Equal("A", LmaxFixMessageClassifier.GetField(logon, "35"));
            Assert.Equal("demo-user-for-test", LmaxFixMessageClassifier.GetField(logon, "49"));
            Assert.Equal("LMXBDM", LmaxFixMessageClassifier.GetField(logon, "56"));
            Assert.Equal("demo-user-for-test", LmaxFixMessageClassifier.GetField(logon, "553"));
            Assert.Equal("demo-password-for-test", LmaxFixMessageClassifier.GetField(logon, "554"));
        }
        finally
        {
            restoreUser();
            restorePassword();
        }
    }

    [Fact]
    public async Task Bad_credentials_logout_sets_summary_cause_without_hiding_it_behind_timeout()
    {
        WithTemporaryEnv("LMAX_DEMO_MD_USERNAME", "demo-user-for-test", out var restoreUser);
        WithTemporaryEnv("LMAX_DEMO_MD_PASSWORD", "demo-password-for-test", out var restorePassword);
        try
        {
            var transport = new CapturingFixTransport([FakeFixTransport.FixMessage("35=5", "58=BAD_CREDENTIALS")]);
            var options = Options(noExternal: false, externalApproved: true, useFake: false, approval: LmaxSandboxMarketDataOptions.ApprovalPhrase);

            var result = await new LmaxSandboxMarketDataCollector(transport).RunAsync(options, CancellationToken.None);

            Assert.Equal(1, result.Summary.LogoutCount);
            Assert.Equal(1, result.Summary.CredentialsRejectedCount);
            Assert.Equal("BAD_CREDENTIALS", result.Summary.PrimaryFailureReason);
            Assert.Equal("LogoutCredentialsRejected", result.Summary.FinalFixSessionState);
            Assert.Equal("FAIL", result.Summary.Status);
        }
        finally
        {
            restoreUser();
            restorePassword();
        }
    }

    [Fact]
    public async Task Timeout_after_market_data_request_session_reject_does_not_mask_primary_failure()
    {
        WithTemporaryEnv("LMAX_DEMO_MD_USERNAME", "demo-user-for-test", out var restoreUser);
        WithTemporaryEnv("LMAX_DEMO_MD_PASSWORD", "demo-password-for-test", out var restorePassword);
        try
        {
            var transport = new CapturingFixTransport([
                FakeFixTransport.FixMessage("35=3", "371=265", "372=V", "58=Error code ValueOutOfRange Tag ID is 265")
            ]);
            var options = Options(noExternal: false, externalApproved: true, useFake: false, approval: LmaxSandboxMarketDataOptions.ApprovalPhrase);

            var result = await new LmaxSandboxMarketDataCollector(transport).RunAsync(options, CancellationToken.None);

            Assert.Equal(1, result.Summary.SessionRejectCount);
            Assert.Equal(1, result.Summary.MarketDataRequestRejectCount);
            Assert.Equal("265", result.Summary.RefTagId);
            Assert.Equal("V", result.Summary.RefMsgType);
            Assert.Equal("MARKET_DATA_REQUEST_MD_UPDATE_TYPE_VALUE_OUT_OF_RANGE", result.Summary.PrimaryFailureReason);
            Assert.Equal(1, result.Summary.TimeoutAfterPrimaryFailureCount);
            Assert.Equal("WARN", result.Summary.Status);
        }
        finally
        {
            restoreUser();
            restorePassword();
        }
    }

    [Fact]
    public async Task Timeout_after_related_sym_group_session_reject_does_not_mask_primary_failure()
    {
        WithTemporaryEnv("LMAX_DEMO_MD_USERNAME", "demo-user-for-test", out var restoreUser);
        WithTemporaryEnv("LMAX_DEMO_MD_PASSWORD", "demo-password-for-test", out var restorePassword);
        try
        {
            var transport = new CapturingFixTransport([
                FakeFixTransport.FixMessage(
                    "35=3",
                    "371=146",
                    "372=V",
                    "58=Error code RepeatingGroupNumInGroupMismatch occurred while parsing a FIX message. Tag ID is 146.")
            ]);
            var options = Options(noExternal: false, externalApproved: true, useFake: false, approval: LmaxSandboxMarketDataOptions.ApprovalPhrase);

            var result = await new LmaxSandboxMarketDataCollector(transport).RunAsync(options, CancellationToken.None);

            Assert.Equal(1, result.Summary.SessionRejectCount);
            Assert.Equal(1, result.Summary.MarketDataRequestRejectCount);
            Assert.Equal("146", result.Summary.RefTagId);
            Assert.Equal("V", result.Summary.RefMsgType);
            Assert.Equal("MARKET_DATA_REQUEST_RELATED_SYM_GROUP_MISMATCH", result.Summary.PrimaryFailureReason);
            Assert.Equal(1, result.Summary.TimeoutAfterPrimaryFailureCount);
            Assert.Equal("WARN", result.Summary.Status);
        }
        finally
        {
            restoreUser();
            restorePassword();
        }
    }

    [Fact]
    public async Task Snapshot_after_logon_with_terminal_timeout_remains_successful_marketdata()
    {
        WithTemporaryEnv("LMAX_DEMO_MD_USERNAME", "demo-user-for-test", out var restoreUser);
        WithTemporaryEnv("LMAX_DEMO_MD_PASSWORD", "demo-password-for-test", out var restorePassword);
        try
        {
            var transport = new CapturingFixTransport([
                FakeFixTransport.FixMessage("35=A", "49=LMXBDM", "56=demo-user-for-test"),
                FakeFixTransport.FixMessage("35=W", "55=GBPUSD", "48=4002", "22=8", "268=2", "269=0", "270=1.25", "271=100", "269=1", "270=1.26", "271=100")
            ]);
            var options = Options(noExternal: false, externalApproved: true, useFake: false, approval: LmaxSandboxMarketDataOptions.ApprovalPhrase);

            var result = await new LmaxSandboxMarketDataCollector(transport).RunAsync(options, CancellationToken.None);

            Assert.Equal(1, result.Summary.LogonCount);
            Assert.Equal(0, result.Summary.Unknown);
            Assert.Equal(1, result.Summary.Snapshots);
            Assert.Equal(1, result.Summary.TerminalTimeoutCount);
            Assert.True(result.Summary.BoundedCaptureEndedCleanly);
            Assert.Equal("NONE", result.Summary.PrimaryFailureReason);
            Assert.Equal("MarketDataObserved", result.Summary.FinalFixSessionState);
            Assert.Equal("PASS", result.Summary.Status);
            Assert.Equal(0, result.Summary.TimeoutAfterPrimaryFailureCount);
        }
        finally
        {
            restoreUser();
            restorePassword();
        }
    }

    [Fact]
    public async Task Missing_demo_marketdata_secret_refs_do_not_attempt_external_capture()
    {
        ClearTemporaryEnv("LMAX_DEMO_MD_USERNAME", out var restoreUser);
        ClearTemporaryEnv("LMAX_DEMO_MD_PASSWORD", out var restorePassword);
        try
        {
            var transport = new CapturingFixTransport([]);
            var options = Options(noExternal: false, externalApproved: true, useFake: false, approval: LmaxSandboxMarketDataOptions.ApprovalPhrase);

            var result = await new LmaxSandboxMarketDataCollector(transport).RunAsync(options, CancellationToken.None);

            Assert.Equal("FAIL", result.Preflight.LmaxSandboxPreflightGate);
            Assert.False(result.Preflight.CredentialsAvailable);
            Assert.False(result.ExternalCallsAttempted);
            Assert.Equal("MISSING_DEMO_MARKETDATA_SECRET_REFS", result.Summary.PrimaryFailureReason);
            Assert.Equal("FAIL", result.Summary.Status);
        }
        finally
        {
            restoreUser();
            restorePassword();
        }
    }

    [Fact]
    public async Task Artifact_writer_outputs_jsonl_summary_reports_manifest_and_redacts_secrets()
    {
        var root = TempRoot("lmax-sandbox-md-r001-fake");
        var options = Options(outputRoot: root, passwordSecretRef: "LMAX_DEMO_MD_PASSWORD");
        var result = await new LmaxSandboxMarketDataCollector(new FakeFixTransport()).RunAsync(options, CancellationToken.None);

        await new LmaxSandboxMarketDataArtifactWriter().WriteAsync(options, result, CancellationToken.None);

        var eventsPath = Path.Combine(root, "marketdata", "lmax_marketdata_events.jsonl");
        Assert.True(File.Exists(eventsPath));
        Assert.True(File.Exists(Path.Combine(root, "marketdata", "lmax_marketdata_summary.json")));
        Assert.True(File.Exists(Path.Combine(root, "10_validation", "lmax_sandbox_marketdata_capture_report.json")));
        Assert.True(File.Exists(Path.Combine(root, "10_validation", "lmax_marketdata_request_group_diagnosis.json")));
        Assert.True(File.Exists(Path.Combine(root, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(root, "hashes.json")));
        Assert.True(File.Exists(Path.Combine(root, "manifest.sha256")));
        Assert.Equal(result.Events.Count, File.ReadAllLines(eventsPath).Length);

        var groupDiagnosis = File.ReadAllText(Path.Combine(root, "10_validation", "lmax_marketdata_request_group_diagnosis.json"));
        Assert.Contains("\"didOutgoingRequestContainExactlyOneValidInstrumentGroupAfter146\": \"YES\"", groupDiagnosis, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "10_validation", "lmax_sandbox_marketdata_success_classification.json")));

        var allText = string.Join(Environment.NewLine, Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain("do-not-print", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("554=", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("553=", allText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFiles(root, "A.txt", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(root, "H.txt", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(root, "I.txt", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Boundary_report_keeps_db_and_trading_disabled()
    {
        var root = TempRoot("lmax-sandbox-md-r001-boundary");
        var options = Options(runKey: "lmax-sandbox-md-r001-boundary", outputRoot: root);
        var result = await new LmaxSandboxMarketDataCollector(new FakeFixTransport()).RunAsync(options, CancellationToken.None);

        await new LmaxSandboxMarketDataArtifactWriter().WriteAsync(options, result, CancellationToken.None);

        var report = await File.ReadAllTextAsync(Path.Combine(root, "10_validation", "lmax_sandbox_marketdata_capture_report.json"));
        Assert.Contains("\"lmaxDbPersistenceEnabled\": \"NO\"", report, StringComparison.Ordinal);
        Assert.Contains("\"lmaxOrderPathDisabled\": \"YES\"", report, StringComparison.Ordinal);
        Assert.Contains("\"lmaxContinuousUnboundedFeed\": \"NO\"", report, StringComparison.Ordinal);
        Assert.DoesNotContain("MarketDataSnapshots", File.ReadAllText(Path.Combine(root, "marketdata", "lmax_marketdata_summary.json")), StringComparison.Ordinal);
        Assert.DoesNotContain("PMS", File.ReadAllText(Path.Combine(root, "marketdata", "lmax_marketdata_summary.json")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logon_audit_report_documents_marketdata_mapping_without_secret_values()
    {
        WithTemporaryEnv("LMAX_DEMO_MD_USERNAME", "demo-user-for-test", out var restoreUser);
        WithTemporaryEnv("LMAX_DEMO_MD_PASSWORD", "demo-password-for-test", out var restorePassword);
        try
        {
            var root = TempRoot("lmax-sandbox-md-r002-demo-test");
            var options = Options(runKey: "lmax-sandbox-md-r002-demo-test", outputRoot: root, noExternal: false, externalApproved: true, useFake: false, approval: LmaxSandboxMarketDataOptions.ApprovalPhrase);
            var transport = new CapturingFixTransport([FakeFixTransport.FixMessage("35=5", "58=BAD_CREDENTIALS")]);
            var result = await new LmaxSandboxMarketDataCollector(transport).RunAsync(options, CancellationToken.None);

            await new LmaxSandboxMarketDataArtifactWriter().WriteAsync(options, result, CancellationToken.None);

            var audit = await File.ReadAllTextAsync(Path.Combine(root, "10_validation", "lmax_sandbox_marketdata_logon_audit.md"));
            Assert.Contains("FIX_LOGON_TAG_49_SOURCE = `LMAX_DEMO_MD_USERNAME`", audit, StringComparison.Ordinal);
            Assert.Contains("FIX_LOGON_TAG_553_SOURCE = `LMAX_DEMO_MD_USERNAME`", audit, StringComparison.Ordinal);
            Assert.Contains("FIX_LOGON_TAG_554_SOURCE = `LMAX_DEMO_MD_PASSWORD`", audit, StringComparison.Ordinal);
            Assert.Contains("FIX_LOGON_TAG_56_VALUE = `LMXBDM`", audit, StringComparison.Ordinal);

            var allText = string.Join(Environment.NewLine, Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
            Assert.DoesNotContain("demo-user-for-test", allText, StringComparison.Ordinal);
            Assert.DoesNotContain("demo-password-for-test", allText, StringComparison.Ordinal);
        }
        finally
        {
            restoreUser();
            restorePassword();
        }
    }

    private static LmaxSandboxMarketDataOptions Options(
        string runKey = "lmax-sandbox-md-r001-fake",
        string? outputRoot = null,
        string environment = "demo",
        string host = LmaxSandboxMarketDataOptions.DemoMarketDataHost,
        string targetCompId = LmaxSandboxMarketDataOptions.DemoMarketDataTargetCompId,
        bool noExecution = true,
        bool disableOrderPath = true,
        bool noExternal = true,
        bool externalApproved = false,
        bool useFake = true,
        string? approval = null,
        int durationSeconds = 30,
        int maxMessages = 100,
        int mdUpdateType = 0,
        string? passwordSecretRef = "LMAX_DEMO_MD_PASSWORD",
        IReadOnlyList<string>? instruments = null)
        => new(
            runKey,
            outputRoot ?? Path.Combine(Path.GetTempPath(), runKey),
            environment,
            host,
            443,
            targetCompId,
            "QQPRODMD",
            "LMAX_DEMO_MD_USERNAME",
            passwordSecretRef,
            instruments ?? ["GBPUSD"],
            durationSeconds,
            maxMessages,
            10,
            10,
            30,
            mdUpdateType,
            noExternal,
            externalApproved,
            approval,
            noExecution,
            disableOrderPath,
            useFake);

    private static string TempRoot(string runKey)
    {
        var root = Path.Combine(Path.GetTempPath(), "qq-lmax-md-tests", Guid.NewGuid().ToString("N"), runKey);
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WithTemporaryEnv(string name, string value, out Action restore)
    {
        var previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
        restore = () => Environment.SetEnvironmentVariable(name, previous);
    }

    private static void ClearTemporaryEnv(string name, out Action restore)
    {
        var previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, null);
        restore = () => Environment.SetEnvironmentVariable(name, previous);
    }

    private sealed class CapturingFixTransport(IEnumerable<string> frames) : IFixTransport
    {
        private readonly Queue<string> frames = new(frames);
        public string Source => "lmax-demo";
        public bool ExternalCallAttempted => false;
        public List<string> SentMessages { get; } = [];

        public Task ConnectAsync(LmaxSandboxMarketDataOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task SendAsync(string redactedFixMessage, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SentMessages.Add(redactedFixMessage);
            return Task.CompletedTask;
        }

        public Task<string?> ReadAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(frames.Count == 0 ? null : frames.Dequeue());
        }

        public Task CloseAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
