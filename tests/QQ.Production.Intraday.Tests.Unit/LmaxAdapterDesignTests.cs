using QQ.Production.Intraday.Infrastructure.Lmax;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxAdapterDesignTests
{
    [Fact]
    public void Adapter_safety_defaults_are_disabled()
    {
        var evaluation = new LmaxFixSafetyGate().Evaluate(new LmaxAdapterSafetyOptions(), LmaxAdapterSafetyIntent.DesignOnly);

        Assert.False(evaluation.Passed);
        Assert.Contains(evaluation.Decisions, x => x.Gate == "Enabled" && !x.Passed);
    }

    [Fact]
    public void Adapter_safety_rejects_allow_live_trading()
    {
        var options = new LmaxAdapterSafetyOptions
        {
            Enabled = true,
            EnvironmentName = "Demo",
            AllowLiveTrading = true
        };

        var evaluation = new LmaxFixSafetyGate().Evaluate(options, LmaxAdapterSafetyIntent.DesignOnly);

        Assert.False(evaluation.Passed);
        Assert.Contains(evaluation.Decisions, x => x.Gate == "AllowLiveTrading" && !x.Passed);
    }

    [Fact]
    public void Adapter_safety_blocks_order_submission_from_non_lab_code()
    {
        var options = new LmaxAdapterSafetyOptions
        {
            Enabled = true,
            EnvironmentName = "Demo",
            AllowExternalConnections = true,
            AllowOrderSubmission = true,
            GovernanceApproved = true,
            Host = "fix-order.london-demo.lmax.com"
        };

        var evaluation = new LmaxFixSafetyGate().Evaluate(options, LmaxAdapterSafetyIntent.OrderSubmission);

        Assert.False(evaluation.Passed);
        Assert.Contains(evaluation.Decisions, x => x.Gate == "AllowOrderSubmission" && !x.Passed);
    }

    [Fact]
    public void New_order_single_mapping_omits_handl_inst()
    {
        var request = DemoOrderRequest() with { ClientOrderId = "DL26050607454402" };

        var message = LmaxFixRecoveryCodec.BuildNewOrderSingle("SENDER", "LMXBD", 2, request, request.ClientOrderId!, "8");

        Assert.Contains($"{LmaxFixMarketDataCodec.Soh}35=D{LmaxFixMarketDataCodec.Soh}", message, StringComparison.Ordinal);
        Assert.DoesNotContain($"{LmaxFixMarketDataCodec.Soh}21=", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Trade_request_id_respects_lmax_length_limit()
    {
        var id = LmaxFixRecoveryCodec.GenerateTradeRequestId(new DateTimeOffset(2026, 5, 6, 8, 24, 19, TimeSpan.Zero), 2);

        Assert.True(id.Length <= 16);
        Assert.Equal("TC26050608241902", id);
    }

    [Fact]
    public void Execution_report_trade_maps_to_fill_candidate()
    {
        var report = new LmaxFixExecutionReportNormalizer().Normalize(FixFields(
            ("35", "8"),
            ("17", "EXEC-FILL"),
            ("37", "ORDER-1"),
            ("11", "DL26050607454402"),
            ("150", "F"),
            ("39", "2"),
            ("48", "4001"),
            ("22", "8"),
            ("54", "1"),
            ("32", "0.1"),
            ("31", "1.17361")));

        Assert.Equal(LmaxNormalizedExecutionType.Trade, report.ExecType);
        Assert.Equal(LmaxNormalizedOrderStatusValue.Filled, report.OrderStatus);
        Assert.True(report.IsFillCandidate);
        Assert.Equal("EURUSD", report.InternalSymbol);
        Assert.Equal(0.1m, report.LastQty);
        Assert.Equal(1.17361m, report.LastPx);
    }

    [Fact]
    public void Execution_report_order_status_does_not_map_to_fill()
    {
        var report = new LmaxFixExecutionReportNormalizer().Normalize(FixFields(
            ("35", "8"),
            ("17", "STATUS-1"),
            ("37", "ORDER-1"),
            ("11", "DL26050607454402"),
            ("150", "I"),
            ("39", "2"),
            ("48", "4001"),
            ("22", "8"),
            ("14", "0.1"),
            ("151", "0")));

        Assert.Equal(LmaxNormalizedExecutionType.OrderStatus, report.ExecType);
        Assert.Equal(LmaxNormalizedOrderStatusValue.Filled, report.OrderStatus);
        Assert.False(report.IsFillCandidate);
    }

    [Fact]
    public void Trade_capture_maps_to_recovery_fill_and_warns_when_trade_uti_missing()
    {
        var report = new LmaxFixTradeCaptureNormalizer().Normalize(FixFields(
            ("35", "AE"),
            ("568", "TC260506074501"),
            ("17", "EXEC-FILL"),
            ("527", "MTF-1"),
            ("37", "ORDER-1"),
            ("11", "DL26050607454402"),
            ("48", "4001"),
            ("22", "8"),
            ("32", "0.1"),
            ("31", "1.17361"),
            ("75", "20260506"),
            ("60", "20260506-07:45:45.000")));

        Assert.True(report.IsRecoveryFillCandidate);
        Assert.Equal("EXEC-FILL", report.ExecId);
        Assert.Equal("EURUSD", report.InternalSymbol);
        Assert.Contains("TradeUTI", report.MissingForEodComparison);
        Assert.Contains(report.Warnings, x => x.Contains("TradeUTI", StringComparison.Ordinal));
    }

    [Fact]
    public void Lifecycle_evidence_consistency_checks_pass_for_matching_er_order_status_and_trade_capture()
    {
        var checks = new[]
        {
            new LmaxLifecycleConsistencyCheck("ClOrdID", LmaxLifecycleConsistencyStatus.Passed, "ClOrdID matches across lifecycle."),
            new LmaxLifecycleConsistencyCheck("OrderID", LmaxLifecycleConsistencyStatus.Passed, "OrderID matches between ExecutionReport and OrderStatus."),
            new LmaxLifecycleConsistencyCheck("TradeCaptureExecId", LmaxLifecycleConsistencyStatus.Passed, "Fill ExecID appears in TradeCaptureReport."),
            new LmaxLifecycleConsistencyCheck("TradeUTI", LmaxLifecycleConsistencyStatus.Warning, "FIX AE does not provide TradeUTI; EOD remains official source.")
        };

        var evidence = new LmaxNormalizedOrderLifecycleEvidence(
            "DL26050607454402",
            "AAAESQAAAABd6+b7",
            "4001",
            "EURUSD",
            0.1m,
            LmaxNormalizedOrderStatusValue.Filled,
            LmaxNormalizedExecutionType.Trade,
            0.1m,
            0m,
            1.17361m,
            "EXEC-FILL",
            true,
            true,
            checks,
            ["FIX AE does not provide TradeUTI; EOD remains official source."]);

        Assert.All(evidence.ConsistencyChecks.Where(x => x.Name != "TradeUTI"), x => Assert.Equal(LmaxLifecycleConsistencyStatus.Passed, x.Status));
        Assert.Contains(evidence.ConsistencyChecks, x => x.Name == "TradeUTI" && x.Status == LmaxLifecycleConsistencyStatus.Warning);
    }

    [Fact]
    public void Shadow_mode_does_not_mutate_internal_references_and_reports_missing_fill()
    {
        var executionReport = new LmaxFixExecutionReportNormalizer().Normalize(FixFields(
            ("17", "MISSING-EXEC"),
            ("37", "ORDER-1"),
            ("11", "CLIENT-1"),
            ("150", "F"),
            ("39", "2"),
            ("48", "4001"),
            ("32", "0.1"),
            ("31", "1.17361")));
        var internalFills = new List<LmaxShadowInternalFillReference>();

        var observations = new LmaxShadowModeService().Compare([executionReport], [], internalFills, []);

        Assert.Empty(internalFills);
        Assert.Contains(observations, x => x.Type == LmaxShadowObservationType.ExecutionReportMissingInternalFill);
    }

    [Fact]
    public void Shadow_mode_produces_observation_for_matching_internal_fill()
    {
        var executionReport = new LmaxFixExecutionReportNormalizer().Normalize(FixFields(
            ("17", "EXEC-FILL"),
            ("37", "ORDER-1"),
            ("11", "CLIENT-1"),
            ("150", "F"),
            ("39", "2"),
            ("48", "4001"),
            ("32", "0.1"),
            ("31", "1.17361")));
        var internalFills = new[]
        {
            new LmaxShadowInternalFillReference("internal-fill-1", "EXEC-FILL", "CLIENT-1", "ORDER-1", 0.1m, 1.17361m)
        };

        var observations = new LmaxShadowModeService().Compare([executionReport], [], internalFills, []);

        Assert.Contains(observations, x => x.Type == LmaxShadowObservationType.ExecutionReportMatchesInternalFill && x.InternalEntityId == "internal-fill-1");
    }

    [Fact]
    public void Normalized_diagnostics_do_not_expose_credentials()
    {
        var raw = "35=8\u000158=password-secret\u0001Authorization=Bearer top-secret-token\u0001";

        var report = new LmaxFixExecutionReportNormalizer().Normalize(FixFields(("150", "F"), ("39", "2")), raw);

        Assert.DoesNotContain("password-secret", report.RawFixMessageSanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("top-secret-token", report.RawFixMessageSanitized, StringComparison.Ordinal);
        Assert.Contains("********", report.RawFixMessageSanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_and_worker_do_not_register_lmax_adapter_services_by_default()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));

        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", apiProgram, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ILmaxFixOrderGateway", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ILmaxFixOrderGateway", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ILmaxShadowModeService", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ILmaxShadowModeService", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton<IVenueExecutionGateway, LmaxVenueGateway>", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton<IVenueExecutionGateway, LmaxVenueGateway>", workerProgram, StringComparison.Ordinal);
    }

    private static LmaxFixDemoOrderRequest DemoOrderRequest()
        => new(
            "EURUSD",
            "4001",
            LmaxFixDemoOrderSide.Buy,
            LmaxFixDemoOrderType.Market,
            LmaxFixDemoOrderTimeInForce.IOC,
            0.1m,
            null,
            5000m,
            null,
            null,
            ConfirmDemoOrder: false,
            DryRun: true,
            MaxWaitSeconds: 10,
            ShowFixMessages: false);

    private static IReadOnlyDictionary<string, string> FixFields(params (string Tag, string Value)[] fields)
        => fields.ToDictionary(x => x.Tag, x => x.Value, StringComparer.Ordinal);

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "QQ.Production.Intraday.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
