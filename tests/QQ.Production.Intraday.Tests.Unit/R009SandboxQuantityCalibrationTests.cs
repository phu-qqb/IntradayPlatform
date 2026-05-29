using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009SandboxQuantityCalibrationTests
{
    private readonly R009LmaxSandboxOrderPathSmokeGate _gate = new();

    [Fact]
    public void Quantity_rejection_diagnosis_requires_quantity_not_valid()
    {
        var diagnosis = _gate.DiagnoseQuantityRejection(
            "artifacts/readiness/execution-sandbox/phase-exec-sandbox-r004-raw-lmax-demo-lifecycle-result.json",
            "EURUSD",
            "Buy",
            "Market",
            0.01m,
            10m,
            "QUANTITY_NOT_VALID");

        Assert.Equal(R009SandboxOrderPathStatus.Ready, diagnosis.Status);
        Assert.True(diagnosis.QuantityNotValidConfirmed);
        Assert.Equal("38", diagnosis.FixOrderQtyField);
        Assert.Contains("QuantityNotValidConfirmed", diagnosis.Findings);
    }

    [Fact]
    public void Local_quantity_rule_discovery_uses_seeded_mapping_and_lab_defaults()
    {
        var discovery = _gate.DiscoverLocalQuantityRule(
            "EURUSD",
            minOrderQuantity: 0.1m,
            quantityStep: 0.1m,
            contractSize: 10000m,
            quantityPrecision: 1,
            labDefaultMaxDemoOrderQuantity: 0.1m,
            labDefaultMaxDemoOrderNotionalUsd: 5000m,
            [
                "src/QQ.Production.Intraday.Application/ApplicationServices.cs:2547",
                "tools/QQ.Production.Intraday.Lmax.ConnectivityLab/LabModels.cs:51"
            ]);

        Assert.Equal(R009SandboxOrderPathStatus.Ready, discovery.Status);
        Assert.Equal(0.1m, discovery.MinOrderQuantity);
        Assert.Equal(0.1m, discovery.QuantityStep);
        Assert.Equal(10000m, discovery.ContractSize);
        Assert.Empty(discovery.MissingCalibrationFields);
    }

    [Fact]
    public void Calibration_selects_local_minimum_when_within_demo_quantity_cap()
    {
        var discovery = _gate.DiscoverLocalQuantityRule(
            "EURUSD",
            minOrderQuantity: 0.1m,
            quantityStep: 0.1m,
            contractSize: 10000m,
            quantityPrecision: 1,
            labDefaultMaxDemoOrderQuantity: 0.1m,
            labDefaultMaxDemoOrderNotionalUsd: 5000m,
            ["src/QQ.Production.Intraday.Application/ApplicationServices.cs:2547"]);

        var calibrated = _gate.CalibrateSandboxQuantity(discovery, maxSandboxOrderCount: 1, maxSandboxNotional: 10m);

        Assert.Equal(R009SandboxOrderPathStatus.Ready, calibrated.Status);
        Assert.Equal(0.1m, calibrated.CalibratedQuantity);
        Assert.True(calibrated.WithinSandboxQuantityCap);
        Assert.True(calibrated.WithinSandboxNotionalCap);
        Assert.False(calibrated.QuantityInventedWithoutLocalEvidence);
    }

    [Fact]
    public void Calibration_blocks_when_local_quantity_rule_is_missing()
    {
        var discovery = _gate.DiscoverLocalQuantityRule(
            "EURUSD",
            minOrderQuantity: null,
            quantityStep: 0.1m,
            contractSize: 10000m,
            quantityPrecision: 1,
            labDefaultMaxDemoOrderQuantity: 0.1m,
            labDefaultMaxDemoOrderNotionalUsd: 5000m,
            ["src/QQ.Production.Intraday.Application/ApplicationServices.cs:2547"]);

        var calibrated = _gate.CalibrateSandboxQuantity(discovery, maxSandboxOrderCount: 1, maxSandboxNotional: 10m);

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, calibrated.Status);
        Assert.True(calibrated.QuantityInventedWithoutLocalEvidence);
        Assert.Contains(calibrated.Reasons, x => x.Contains("QuantityInventedWithoutLocalEvidence", StringComparison.Ordinal));
    }
}
