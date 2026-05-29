using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyRuntimeAdapterDesignTests
{
    [Fact]
    public void Default_options_are_disabled_design_only_and_inert()
    {
        var options = new LmaxReadOnlyRuntimeAdapterOptions();

        Assert.False(options.Enabled);
        Assert.Equal(LmaxReadOnlyRuntimeImplementationMode.DesignOnly, options.ImplementationMode);
        Assert.False(options.AllowExternalConnections);
        Assert.False(options.AllowCredentialUse);
        Assert.True(options.ReadOnly);
        Assert.False(options.AllowOrderSubmission);
        Assert.False(options.PersistRawFixMessages);
        Assert.False(options.PersistToTradingTables);
        Assert.False(options.SubmitToShadowReplay);
        Assert.True(options.DryRun);
    }

    [Fact]
    public void Default_options_evaluate_to_disabled_and_blocked_by_explicit_gates()
    {
        var evaluation = LmaxReadOnlyRuntimeSafetyGate.Evaluate(new LmaxReadOnlyRuntimeAdapterOptions());

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Disabled, evaluation.RunStatus);
        Assert.False(evaluation.Passed);
        Assert.Contains("Enabled", evaluation.FailedGateNames);
        Assert.Contains("ImplementationMode", evaluation.FailedGateNames);
        Assert.Contains("AllowExternalConnections", evaluation.FailedGateNames);
        Assert.Contains("AllowCredentialUse", evaluation.FailedGateNames);
    }

    [Fact]
    public void Design_only_mode_blocks_runtime_execution_even_if_future_flags_are_toggled()
    {
        var options = FutureLookingOptions() with { ImplementationMode = LmaxReadOnlyRuntimeImplementationMode.DesignOnly };

        var evaluation = LmaxReadOnlyRuntimeSafetyGate.Evaluate(options);

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, evaluation.RunStatus);
        Assert.Contains("ImplementationMode", evaluation.FailedGateNames);
    }

    [Fact]
    public void Allow_order_submission_is_always_blocked()
    {
        var options = FutureLookingOptions() with { AllowOrderSubmission = true };

        var evaluation = LmaxReadOnlyRuntimeSafetyGate.Evaluate(options);

        Assert.Contains("AllowOrderSubmission", evaluation.FailedGateNames);
    }

    [Fact]
    public void Persist_to_trading_tables_is_always_blocked()
    {
        var options = FutureLookingOptions() with { PersistToTradingTables = true };

        var evaluation = LmaxReadOnlyRuntimeSafetyGate.Evaluate(options);

        Assert.Contains("PersistToTradingTables", evaluation.FailedGateNames);
    }

    [Fact]
    public void Runtime_connection_activation_levels_are_blocked_by_current_design()
    {
        var options = FutureLookingOptions() with
        {
            RequestedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit
        };

        var evaluation = LmaxReadOnlyRuntimeSafetyGate.Evaluate(options, new LmaxReadOnlyRuntimeRunRequest("phase 4 preflight"));

        Assert.Contains("ActivationLevel", evaluation.FailedGateNames);
        Assert.Contains("Phase4ImplementationNotStarted", evaluation.FailedGateNames);
    }

    [Fact]
    public void Phase4_activation_level_is_blocked_by_default()
    {
        var evaluation = LmaxReadOnlyRuntimeSafetyGate.Evaluate(
            new LmaxReadOnlyRuntimeAdapterOptions(),
            new LmaxReadOnlyRuntimeRunRequest("phase 4 attempt", RequestedActivationLevel: LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Disabled, evaluation.RunStatus);
        Assert.Contains("Enabled", evaluation.FailedGateNames);
        Assert.Contains("ImplementationMode", evaluation.FailedGateNames);
        Assert.Contains("ActivationLevel", evaluation.FailedGateNames);
        Assert.Contains("Phase4ImplementationNotStarted", evaluation.FailedGateNames);
    }

    [Fact]
    public void Phase4_remains_blocked_if_design_only()
    {
        var options = Phase4PreflightOptions() with { ImplementationMode = LmaxReadOnlyRuntimeImplementationMode.DesignOnly };

        var evaluation = LmaxReadOnlyRuntimeSafetyGate.Evaluate(options, new LmaxReadOnlyRuntimeRunRequest("phase 4 attempt"));

        Assert.Contains("ImplementationMode", evaluation.FailedGateNames);
        Assert.Contains("Phase4ImplementationNotStarted", evaluation.FailedGateNames);
    }

    [Theory]
    [InlineData("AllowExternalConnections")]
    [InlineData("AllowCredentialUse")]
    [InlineData("AllowOrderSubmission")]
    [InlineData("PersistToTradingTables")]
    [InlineData("SchedulerEnabled")]
    [InlineData("SubmitToShadowReplay")]
    [InlineData("NonDemoEnvironment")]
    [InlineData("MissingReason")]
    public void Phase4_boundary_blocks_unsafe_or_incomplete_conditions(string condition)
    {
        var options = Phase4PreflightOptions() with
        {
            AllowExternalConnections = condition != "AllowExternalConnections",
            AllowCredentialUse = condition != "AllowCredentialUse",
            AllowOrderSubmission = condition == "AllowOrderSubmission",
            PersistToTradingTables = condition == "PersistToTradingTables",
            SchedulerEnabled = condition == "SchedulerEnabled",
            SubmitToShadowReplay = condition == "SubmitToShadowReplay",
            EnvironmentName = condition == "NonDemoEnvironment" ? "UAT" : "Demo"
        };
        var reason = condition == "MissingReason" ? "" : "phase 4 preflight attempt";

        var evaluation = LmaxReadOnlyRuntimeSafetyGate.Evaluate(options, new LmaxReadOnlyRuntimeRunRequest(reason));

        var expectedGate = condition switch
        {
            "AllowExternalConnections" => "AllowExternalConnections",
            "AllowCredentialUse" => "AllowCredentialUse",
            "AllowOrderSubmission" => "AllowOrderSubmission",
            "PersistToTradingTables" => "PersistToTradingTables",
            "SchedulerEnabled" => "SchedulerEnabled",
            "SubmitToShadowReplay" => "SubmitToShadowReplay",
            "NonDemoEnvironment" => "Phase4EnvironmentName",
            "MissingReason" => "Phase4ReasonRequired",
            _ => throw new ArgumentOutOfRangeException(nameof(condition), condition, null)
        };

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, evaluation.RunStatus);
        Assert.Contains(expectedGate, evaluation.FailedGateNames);
        Assert.Contains("Phase4ImplementationNotStarted", evaluation.FailedGateNames);
    }

    [Fact]
    public void Phase4_is_still_not_runnable_even_when_preflight_gates_look_safe()
    {
        var evaluation = LmaxReadOnlyRuntimeSafetyGate.Evaluate(Phase4PreflightOptions(), new LmaxReadOnlyRuntimeRunRequest("manual demo read-only preflight"));

        Assert.Equal(LmaxReadOnlyRuntimeRunStatus.Blocked, evaluation.RunStatus);
        Assert.Contains("ActivationLevel", evaluation.FailedGateNames);
        Assert.Contains("Phase4ImplementationNotStarted", evaluation.FailedGateNames);
    }

    [Fact]
    public void Unsafe_limits_and_production_environment_are_blocked()
    {
        var options = FutureLookingOptions() with
        {
            EnvironmentName = "Production",
            MaxEventsPerRun = LmaxReadOnlyRuntimeAdapterOptions.SafeMaxEventsPerRun + 1,
            MaxRuntimeSeconds = LmaxReadOnlyRuntimeAdapterOptions.SafeMaxRuntimeSeconds + 1
        };

        var evaluation = LmaxReadOnlyRuntimeSafetyGate.Evaluate(options);

        Assert.Contains("Production", evaluation.FailedGateNames);
        Assert.Contains("MaxEventsPerRun", evaluation.FailedGateNames);
        Assert.Contains("MaxRuntimeSeconds", evaluation.FailedGateNames);
    }

    [Fact]
    public void Submit_to_shadow_replay_remains_blocked_for_design_contract()
    {
        var options = FutureLookingOptions() with { SubmitToShadowReplay = true };

        var evaluation = LmaxReadOnlyRuntimeSafetyGate.Evaluate(options);

        Assert.Contains("SubmitToShadowReplay", evaluation.FailedGateNames);
    }

    [Fact]
    public void Design_contract_dtos_do_not_expose_secret_fields()
    {
        var types = new[]
        {
            typeof(LmaxReadOnlyRuntimeAdapterOptions),
            typeof(LmaxReadOnlyRuntimeRunRequest),
            typeof(LmaxReadOnlyRuntimeEventEnvelope),
            typeof(LmaxReadOnlyRuntimeRunResult),
            typeof(LmaxReadOnlyRuntimeEvidenceBatchSummary)
        };

        foreach (var property in types.SelectMany(x => x.GetProperties(BindingFlags.Public | BindingFlags.Instance)))
        {
            Assert.DoesNotContain("password", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("apiKey", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("authorization", property.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static LmaxReadOnlyRuntimeAdapterOptions FutureLookingOptions()
        => new()
        {
            Enabled = true,
            ImplementationMode = LmaxReadOnlyRuntimeImplementationMode.DisabledNoOp,
            AllowExternalConnections = true,
            AllowCredentialUse = true,
            ReadOnly = true,
            AllowOrderSubmission = false,
            PersistRawFixMessages = false,
            PersistToTradingTables = false,
            SubmitToShadowReplay = false,
            EnvironmentName = "Demo",
            OperationalReadinessPassed = true,
            GovernanceApproved = true,
            LocalOnlyApi = true,
            DryRun = true,
            RequestedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level1DisabledSkeleton
        };

    private static LmaxReadOnlyRuntimeAdapterOptions Phase4PreflightOptions()
        => FutureLookingOptions() with
        {
            Enabled = true,
            ImplementationMode = LmaxReadOnlyRuntimeImplementationMode.FutureReadOnly,
            AllowExternalConnections = true,
            AllowCredentialUse = true,
            ReadOnly = true,
            AllowOrderSubmission = false,
            PersistRawFixMessages = false,
            PersistToTradingTables = false,
            SubmitToShadowReplay = false,
            SchedulerEnabled = false,
            EnvironmentName = "Demo",
            OperationalReadinessPassed = true,
            GovernanceApproved = true,
            LocalOnlyApi = true,
            DryRun = true,
            RequestedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit,
            MaxAllowedActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit
        };
}
