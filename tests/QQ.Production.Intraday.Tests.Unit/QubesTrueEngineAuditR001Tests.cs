using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesTrueEngineAuditR001Tests
{
    [Fact]
    public void Prototype_output_cannot_be_classified_as_true_qubes_engine()
    {
        var output = R005PrototypeOutput();

        var result = QubesProductionBoundaryGuard.ValidateSandboxPrototypeOutput(
            output,
            QubesProductionUsage.Production,
            QubesKnownPrototypeIds.R005OutputHash);

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, issue => issue.Code == QubesProductionValidationIssueCode.SandboxPrototypeBlocked);
        Assert.Contains(result.Issues, issue => issue.Code == QubesProductionValidationIssueCode.R005RunIdPrototypeOnly);
        Assert.Contains(result.Issues, issue => issue.Code == QubesProductionValidationIssueCode.R005OutputIdRejected);
        Assert.Contains(result.Issues, issue => issue.Code == QubesProductionValidationIssueCode.R005OutputHashRejected);
    }

    [Fact]
    public void Output_weights_alone_cannot_be_classified_as_qubes_input_snapshot()
    {
        var manifest = new QubesInputSnapshotManifest(
            QubesInputSnapshotId: "qubes-true-engine-audit-r001:weights-only",
            MarketDataSnapshotId: null,
            InputContractVersion: "qubes-input-snapshot.v1",
            ExpectedEngineId: "real-qubes-engine",
            CreatedAtUtc: DateTimeOffset.Parse("2025-12-17T02:00:00Z"),
            ManifestHash: "weights-only-not-input",
            Components: [],
            EngineKind: QubesEngineKind.RealEngine);

        var result = new QubesInputSnapshotManifestValidator().Validate(manifest);

        Assert.False(result.IsValid);
        Assert.False(result.SnapshotCanFeedRealEngine);
        Assert.Contains(result.Issues, issue => issue.Code == QubesInputSnapshotIssueCode.MissingMarketDataSnapshotId);
        Assert.Contains(result.Issues, issue => issue.Code == QubesInputSnapshotIssueCode.MissingComponents);
    }

    [Fact]
    public void Pms_approved_qubes_run_cannot_be_set_from_r005_prototype_ids()
    {
        var runLink = new QubesRunLinkManifest(
            QubesRunId: QubesKnownPrototypeIds.R005RunId,
            QubesInputSnapshotId: "qubes-operationalization-r005:prototype-input:20251217T020000Z:001",
            QubesWeightsOutputId: QubesKnownPrototypeIds.R005OutputId,
            MarketDataSnapshotId: null,
            EngineId: "SandboxQubesPrototype",
            EngineKind: QubesEngineKind.SandboxPrototype,
            RunContractVersion: "qubes-run-link.v1",
            InputContractVersion: "qubes-input-snapshot.v1",
            OutputContractVersion: "qubes-weights-output.v1",
            RunFingerprint: QubesKnownPrototypeIds.R005OutputHash,
            CreatedAtUtc: DateTimeOffset.Parse("2025-12-17T02:00:00Z"));

        var result = new QubesRunLinkManifestValidator().Validate(runLink);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == QubesRunLinkIssueCode.SandboxPrototypeBlocked);
        Assert.Contains(result.Issues, issue => issue.Code == QubesRunLinkIssueCode.R005RunIdRejected);
        Assert.Contains(result.Issues, issue => issue.Code == QubesRunLinkIssueCode.R005OutputIdRejected);
        Assert.Contains(result.Issues, issue => issue.Code == QubesRunLinkIssueCode.R005OutputHashRejected);
    }

    [Fact]
    public void R010_operator_approval_is_not_transferable_to_changed_qubes_output()
    {
        const string r010ApprovedOutputId = QubesKnownPrototypeIds.R005OutputId;
        const string trueQubesOutputId = "qubes-real-engine-output-20251217T020000Z-001";

        Assert.NotEqual(r010ApprovedOutputId, trueQubesOutputId);
    }

    [Fact]
    public void Direct_cross_execution_leakage_is_rejected_by_transformer()
    {
        var output = R005PrototypeOutput() with
        {
            Weights =
            [
                new SandboxQubesOutputWeight("EURGBP", 0.12m),
                new SandboxQubesOutputWeight("AUDUSD", -0.05m),
                new SandboxQubesOutputWeight("JPYUSD", 0.01m)
            ]
        };

        var result = new SandboxQubesExecutionUniverseTransformer().Transform(output);

        Assert.False(result.DirectCrossExecutionLeakageFound);
        Assert.Contains("EURGBP", result.DirectCrossSymbolsExcluded);
        Assert.DoesNotContain(result.ExecutionLines, line => line.ExecutionTradableSymbol == "EURGBP");
        Assert.Contains(result.ExecutionLines, line => line.ExecutionTradableSymbol == "USDJPY" && line.RequiresInversion && line.SecurityId == 4004 && line.SecurityIdSource == "8");
    }

    [Fact]
    public void Production_accounting_readiness_remains_blocked_without_required_identity_and_binding()
    {
        var request = new QubesProductionValidationRequest(
            QubesProductionUsage.Accounting,
            new QubesInputSnapshotRef(null, null, QubesEngineKind.ExternalEngineRequired),
            new QubesRunRef(null, QubesEngineKind.ExternalEngineRequired, []),
            new QubesWeightsOutputRef(null, null, null, null, null, QubesEngineKind.ExternalEngineRequired),
            RealEngineAdapterRegistered: false);

        var result = new QubesProductionUsageValidator().Validate(request);

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, issue => issue.Code == QubesProductionValidationIssueCode.ExternalEngineRequired);
        Assert.Contains(result.Issues, issue => issue.Code == QubesProductionValidationIssueCode.MissingQubesInputSnapshotId);
        Assert.Contains(result.Issues, issue => issue.Code == QubesProductionValidationIssueCode.MissingQubesRunId);
        Assert.Contains(result.Issues, issue => issue.Code == QubesProductionValidationIssueCode.MissingQubesWeightsOutputId);
        Assert.Contains(result.Issues, issue => issue.Code == QubesProductionValidationIssueCode.MissingMarketDataSnapshotId);
    }

    private static SandboxQubesOutput R005PrototypeOutput()
        => new(
            SandboxQubesRunId: QubesKnownPrototypeIds.R005RunId,
            QubesOutputId: QubesKnownPrototypeIds.R005OutputId,
            InputSnapshotId: "qubes-operationalization-r005:prototype-input:20251217T020000Z:001",
            MarketDataSnapshotId: null,
            CanonicalTargetCloseUtc: DateTimeOffset.Parse("2025-12-17T02:00:00Z"),
            Weights:
            [
                new SandboxQubesOutputWeight("AUDCNH", -0.036436m),
                new SandboxQubesOutputWeight("AUDUSD", -0.053856m),
                new SandboxQubesOutputWeight("CNHSGD", 0.336970m),
                new SandboxQubesOutputWeight("EURGBP", 0.094338m),
                new SandboxQubesOutputWeight("EURUSD", -0.013900m),
                new SandboxQubesOutputWeight("GBPUSD", 0.039348m),
                new SandboxQubesOutputWeight("JPYUSD", 0.001663m)
            ],
            WeightUnits: "PrototypeSignalWeight",
            DirectCrossesPresent: true,
            DirectCrossPolicy: "DirectCrossSignalOnlyNettingFirstExecutionDisabled",
            RunnerType: SandboxQubesPrototypeRunner.RunnerType,
            SandboxOnly: true,
            NotProduction: true,
            NotAccounting: true,
            NotExecuted: true,
            NotLedgerCommit: true);
}
