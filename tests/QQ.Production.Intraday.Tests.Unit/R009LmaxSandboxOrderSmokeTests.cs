using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009LmaxSandboxOrderSmokeTests
{
    private readonly R009LmaxSandboxOrderPathSmokeGate _gate = new();

    [Fact]
    public void Missing_sandbox_config_blocks_before_connection()
    {
        var discovery = _gate.DiscoverSandboxConfig(new Dictionary<string, string?>());

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, discovery.Status);
        Assert.False(discovery.ReadyForSandboxSubmission);
        Assert.Contains("LmaxSandboxConfigMissing", discovery.Reasons);
    }

    [Fact]
    public void Production_config_is_rejected()
    {
        var discovery = _gate.DiscoverSandboxConfig(ValidConfig(new()
        {
            ["LmaxSandbox:Environment"] = "Production",
            ["LmaxSandbox:BrokerVenue"] = "LMAXProduction",
            ["LmaxSandbox:ProductionVenueAllowed"] = "true",
            ["LmaxSandbox:ProductionCredentialsAllowed"] = "true",
            ["LmaxSandbox:CredentialProfileName"] = "prod-live-profile"
        }));

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, discovery.Status);
        Assert.Contains("EnvironmentMustBeSandbox", discovery.Reasons);
        Assert.Contains("BrokerVenueMustBeLMAXSandbox", discovery.Reasons);
        Assert.Contains("ProductionCredentialLabelDetected", discovery.Reasons);
    }

    [Fact]
    public void Valid_sandbox_config_and_tiny_eurusd_intent_can_create_sandbox_order_intent_only()
    {
        var discovery = _gate.DiscoverSandboxConfig(ValidConfig());
        var result = _gate.TryCreateSandboxOrderIntent(ValidIntent(), discovery, R009SandboxGuardrailContract.Default, requestedOrderCount: 1);

        Assert.True(discovery.ReadyForSandboxSubmission);
        Assert.Equal(R009SandboxOrderPathStatus.Ready, result.RiskCheck.Status);
        Assert.NotNull(result.OrderIntent);
        Assert.NotNull(result.Route);
        Assert.True(result.OrderIntent!.SandboxOnly);
        Assert.False(result.OrderIntent.ProductionOrder);
        Assert.False(result.Route!.ProductionRoute);
        Assert.True(result.OrderIntent.NoProductionLedgerCommit);
        Assert.Equal(R009SandboxSubmissionStatus.NotSubmittedBlocked, result.Submission.Status);
        Assert.Equal(0, result.Submission.SubmittedOrderCount);
    }

    [Fact]
    public void Direct_cross_execution_intent_is_rejected()
    {
        var result = ReadyResult(ValidIntent(symbol: "EURGBP", executionSymbol: "EURGBP", normalizedSymbol: "EURGBP"));

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, result.RiskCheck.Status);
        Assert.Contains("SymbolNotWhitelistedOrDirectCross", result.RiskCheck.Reasons);
        Assert.True(result.RiskCheck.DirectCrossRejected);
        Assert.Null(result.OrderIntent);
    }

    [Fact]
    public void Non_whitelisted_symbol_is_rejected()
    {
        var result = ReadyResult(ValidIntent(symbol: "USDCNH", executionSymbol: "USDCNH", normalizedSymbol: "USDCNH"));

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, result.RiskCheck.Status);
        Assert.Contains("SymbolNotWhitelistedOrDirectCross", result.RiskCheck.Reasons);
    }

    [Fact]
    public void Legacy_06_target_close_is_rejected()
    {
        var result = ReadyResult(ValidIntent(targetClose: new DateTimeOffset(2026, 5, 26, 15, 6, 0, TimeSpan.Zero)));

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, result.RiskCheck.Status);
        Assert.Contains("CanonicalQuarterHourTargetCloseRequired", result.RiskCheck.Reasons);
    }

    [Fact]
    public void Usdjpy_caveat_is_required_and_preserved()
    {
        var result = ReadyResult(ValidIntent(
            symbol: "USDJPY",
            executionSymbol: "USDJPY",
            normalizedSymbol: "JPYUSD",
            requiresInversion: true,
            securityId: "4004",
            securityIdSource: "8"));

        Assert.Equal(R009SandboxOrderPathStatus.Ready, result.RiskCheck.Status);
        Assert.NotNull(result.OrderIntent);
        Assert.Equal("JPYUSD", result.OrderIntent!.NormalizedPortfolioSymbol);
        Assert.Equal("USDJPY", result.OrderIntent.ExecutionTradableSymbol);
        Assert.True(result.OrderIntent.RequiresInversion);
        Assert.Equal("4004", result.OrderIntent.SecurityID);
        Assert.Equal("8", result.OrderIntent.SecurityIDSource);
    }

    [Fact]
    public void Bad_usdjpy_caveat_is_rejected()
    {
        var result = ReadyResult(ValidIntent(symbol: "USDJPY", executionSymbol: "USDJPY", normalizedSymbol: "USDJPY"));

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, result.RiskCheck.Status);
        Assert.Contains("USDJPYCaveatRequired", result.RiskCheck.Reasons);
    }

    [Fact]
    public void Max_order_count_is_enforced()
    {
        var discovery = _gate.DiscoverSandboxConfig(ValidConfig());
        var result = _gate.TryCreateSandboxOrderIntent(ValidIntent(), discovery, R009SandboxGuardrailContract.Default, requestedOrderCount: 2);

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, result.RiskCheck.Status);
        Assert.Contains("MaxSandboxOrderCountExceeded", result.RiskCheck.Reasons);
    }

    [Fact]
    public void Max_notional_is_enforced()
    {
        var result = ReadyResult(ValidIntent(targetNotional: 101m));

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, result.RiskCheck.Status);
        Assert.Contains("MaxSandboxNotionalExceeded", result.RiskCheck.Reasons);
    }

    [Fact]
    public void Kill_switch_must_be_open_for_sandbox_only()
    {
        var result = ReadyResult(ValidIntent(killSwitchOpen: false));

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, result.RiskCheck.Status);
        Assert.Contains("KillSwitchMustBeOpenForSandboxOnly", result.RiskCheck.Reasons);
    }

    [Fact]
    public void Readiness_must_be_present_or_explicitly_waived_for_sandbox_smoke_test()
    {
        var result = ReadyResult(ValidIntent(readinessPresent: false, readinessWaived: false));

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, result.RiskCheck.Status);
        Assert.Contains("ReadinessRequiredOrExplicitSandboxWaiverRequired", result.RiskCheck.Reasons);
    }

    [Fact]
    public void Sandbox_artifacts_are_marked_sandbox_only_and_production_paths_impossible()
    {
        var result = ReadyResult(ValidIntent());

        Assert.NotNull(result.OrderIntent);
        Assert.NotNull(result.Route);
        Assert.True(result.OrderIntent!.SandboxOnly);
        Assert.False(result.OrderIntent.ProductionOrder);
        Assert.False(result.OrderIntent.IsLiveProduction);
        Assert.False(result.Route!.ProductionRoute);
        Assert.False(result.Route.NonSandboxBrokerRoute);
        Assert.False(result.Route.ProductionCredentialsUsed);
        Assert.False(result.Submission.ProductionSubmission);
        Assert.False(result.Reconciliation.ProductionLedgerMutation);
        Assert.False(result.Reconciliation.ProductionStateMutation);
    }

    [Fact]
    public void R011_maps_pms_paper_r015_decrease_line_to_sell_sandbox_intent()
    {
        var result = _gate.MapPmsPaperR015LineToSandboxIntent(
            new R011PmsPaperR015SourceLine(
                "pms-paper-r010-delta-fields-20260525-001",
                "pms-paper-r010-delta-fields-20260525-001:oms-intent-preview:AUDUSD",
                "pms-paper-r010-delta-fields-20260525-001:oms-preview-state:AUDUSD",
                "AUDUSD",
                "Decrease",
                -0.13231m,
                -132310m),
            new DateTimeOffset(2026, 5, 26, 15, 15, 0, TimeSpan.Zero),
            0.1m,
            "R009ContractVersion=EXEC-SANDBOX-R011",
            "ExistingLmaxDemoProfile",
            "Demo");

        Assert.Equal(R009SandboxOrderPathStatus.Ready, result.Status);
        Assert.Equal("Sell", result.SideDerivation.PortfolioSide);
        Assert.Equal("Sell", result.SideDerivation.ExecutionSide);
        Assert.Equal("AUDUSD", result.BrokerSymbolMapping.ExecutionTradableSymbol);
        Assert.False(result.BrokerSymbolMapping.AudusdMisclassified);
        Assert.NotNull(result.ExecutionIntent);
        Assert.True(result.ExecutionIntent!.SandboxOnly);
        Assert.False(result.ExecutionIntent.ProductionOrder);
        Assert.Equal(0.1m, result.ExecutionIntent.TargetQuantity);
    }

    [Fact]
    public void R011_rejects_conflicting_side_evidence_before_order_intent()
    {
        var result = _gate.MapPmsPaperR015LineToSandboxIntent(
            new R011PmsPaperR015SourceLine(
                "cycle",
                "intent",
                "state",
                "EURUSD",
                "Increase",
                0.1m,
                -100m),
            new DateTimeOffset(2026, 5, 26, 15, 15, 0, TimeSpan.Zero),
            0.1m,
            "R009ContractVersion=EXEC-SANDBOX-R011",
            "ExistingLmaxDemoProfile",
            "Demo");

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, result.Status);
        Assert.Contains("DirectionAndDeltaNotionalSideConflict", result.Reasons);
        Assert.Null(result.ExecutionIntent);
    }

    [Fact]
    public void R011_preserves_usdjpy_inversion_when_mapping_portfolio_side()
    {
        var result = _gate.MapPmsPaperR015LineToSandboxIntent(
            new R011PmsPaperR015SourceLine(
                "cycle",
                "intent",
                "state",
                "JPYUSD",
                "Increase",
                0.1m,
                100m),
            new DateTimeOffset(2026, 5, 26, 15, 15, 0, TimeSpan.Zero),
            0.1m,
            "R009ContractVersion=EXEC-SANDBOX-R011",
            "ExistingLmaxDemoProfile",
            "Demo");

        Assert.Equal(R009SandboxOrderPathStatus.Ready, result.Status);
        Assert.Equal("Buy", result.SideDerivation.PortfolioSide);
        Assert.Equal("Sell", result.SideDerivation.ExecutionSide);
        Assert.Equal("USDJPY", result.BrokerSymbolMapping.ExecutionTradableSymbol);
        Assert.Equal("JPYUSD", result.BrokerSymbolMapping.NormalizedPortfolioSymbol);
        Assert.True(result.BrokerSymbolMapping.RequiresInversion);
        Assert.True(result.BrokerSymbolMapping.UsdjpyCaveatPreserved);
        Assert.Equal("4004", result.BrokerSymbolMapping.SecurityID);
        Assert.Equal("8", result.BrokerSymbolMapping.SecurityIDSource);
    }

    private R009SandboxOrderIntentResult ReadyResult(R009SandboxExecutionIntent intent)
    {
        var discovery = _gate.DiscoverSandboxConfig(ValidConfig());
        return _gate.TryCreateSandboxOrderIntent(intent, discovery, R009SandboxGuardrailContract.Default, requestedOrderCount: 1);
    }

    private static Dictionary<string, string?> ValidConfig(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["LmaxSandbox:Environment"] = "Sandbox",
            ["LmaxSandbox:BrokerVenue"] = "LMAXSandbox",
            ["LmaxSandbox:ProductionVenueAllowed"] = "false",
            ["LmaxSandbox:ProductionCredentialsAllowed"] = "false",
            ["LmaxSandbox:SandboxCredentialsRequired"] = "true",
            ["LmaxSandbox:CredentialProfileName"] = "sandbox-smoke-profile",
            ["LmaxSandbox:AllowSandboxOrderSubmission"] = "true",
            ["LmaxSandbox:SchedulerEnabled"] = "false",
            ["LmaxSandbox:BackgroundWorkerEnabled"] = "false",
            ["LmaxSandbox:MaxSandboxOrderCount"] = "1",
            ["LmaxSandbox:MaxSandboxNotional"] = "100"
        };

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                values[key] = value;
            }
        }

        return values;
    }

    private static R009SandboxExecutionIntent ValidIntent(
        string symbol = "EURUSD",
        string executionSymbol = "EURUSD",
        string normalizedSymbol = "EURUSD",
        bool requiresInversion = false,
        string? securityId = null,
        string? securityIdSource = null,
        DateTimeOffset? targetClose = null,
        decimal targetNotional = 10m,
        bool readinessPresent = true,
        bool readinessWaived = false,
        bool killSwitchOpen = true)
    {
        return new R009SandboxExecutionIntent(
            ExecutionIntentId: $"r001-{symbol.ToLowerInvariant()}-intent",
            SourceDecisionPreviewId: "r012-disabled-preview-decision",
            Symbol: symbol,
            ExecutionTradableSymbol: executionSymbol,
            NormalizedPortfolioSymbol: normalizedSymbol,
            RequiresInversion: requiresInversion,
            SecurityID: securityId,
            SecurityIDSource: securityIdSource,
            Side: "Buy",
            TargetQuantity: 1m,
            TargetNotional: targetNotional,
            CanonicalTargetCloseUtc: targetClose ?? new DateTimeOffset(2026, 5, 26, 15, 15, 0, TimeSpan.Zero),
            BarRole: "IntradayRebalance",
            ReadinessPresent: readinessPresent,
            ReadinessWaivedForSandboxSmokeTest: readinessWaived,
            OperatorSandboxApproval: true,
            KillSwitchOpenForSandboxOnly: killSwitchOpen,
            R009DecisionStatus: "PreviewReady",
            SandboxOnly: true,
            ProductionOrder: false,
            IsLiveProduction: false,
            NoProductionLedgerCommit: true);
    }
}
