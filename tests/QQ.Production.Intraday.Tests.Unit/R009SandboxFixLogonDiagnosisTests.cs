using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009SandboxFixLogonDiagnosisTests
{
    private readonly R009LmaxSandboxOrderPathSmokeGate _gate = new();

    [Fact]
    public void Diagnosis_identifies_generic_demo_target_override_candidate()
    {
        var diagnosis = _gate.DiagnoseFixTradingLogonFailure(
            priorLogonConfirmed: false,
            priorNewOrderSingleSent: false,
            senderCompIdVariablePresent: true,
            targetCompIdVariablePresent: true,
            localOrderTargetConfigured: true,
            localMarketDataTargetConfigured: true);

        Assert.Equal(R009SandboxOrderPathStatus.Ready, diagnosis.Status);
        Assert.True(diagnosis.CredentialValuesRedacted);
        Assert.True(diagnosis.GenericDemoTargetMayOverrideOrderTarget);
        Assert.Contains("PriorFixTradingLogonNotConfirmed", diagnosis.Findings);
        Assert.Contains("PriorNewOrderSingleNotSent", diagnosis.Findings);
        Assert.Contains("PreferLocalFixOrderTargetCompIdForTradingSession", diagnosis.RepairCandidates);
        Assert.Equal("35=A", diagnosis.ExpectedLogonAckMessageType);
    }

    [Fact]
    public void Non_secret_session_repair_uses_local_order_target()
    {
        var repair = _gate.CreateNonSecretSessionRepairResult(
            localOrderTargetConfigured: true,
            productionRouteBlocked: true);

        Assert.Equal(R009SandboxOrderPathStatus.Ready, repair.Status);
        Assert.True(repair.RepairApplied);
        Assert.True(repair.UsesLocalOrderTarget);
        Assert.True(repair.AvoidsGenericTargetOverride);
        Assert.True(repair.ProductionRouteBlocked);
        Assert.Empty(repair.MissingNonSecretFields);
    }

    [Fact]
    public void Non_secret_session_repair_blocks_when_order_target_is_not_discoverable()
    {
        var repair = _gate.CreateNonSecretSessionRepairResult(
            localOrderTargetConfigured: false,
            productionRouteBlocked: true);

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, repair.Status);
        Assert.False(repair.RepairApplied);
        Assert.Contains("LmaxConnectivityLab:FixOrderTargetCompId", repair.MissingNonSecretFields);
        Assert.Contains("MissingNonSecretConfig:LmaxConnectivityLab:FixOrderTargetCompId", repair.Reasons);
    }
}
