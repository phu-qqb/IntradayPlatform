using System.Security.Cryptography;
using System.Text;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class SandboxQubesOperatorApprovalTests
{
    private const string MarketDataSnapshotId = "canonical-marketdata-golden-source-r001:polygon-offline-bbo:20251217T020000Z:AUDUSD-EURUSD-GBPUSD";
    private const string QubesOutputId = "qubes-operationalization-r005:prototype-output:20251217T020000Z:001";
    private const string QubesOutputHash = "5AB433ED36E08CFD8DCA7A8B02138E7CC81280F62E56D894E239D3F75F4DF79A";
    private const string RiskReviewHash = "C5AB0301860982A1A6922A434E877AB8A6CC3C6AE5A8A6A125C20DC8F8D6C658";
    private const string ExpectedOperatorApprovalId = "pms-qubes-sandbox-operator-approval-r010:5d3a9b7aac941102";

    [Fact]
    public void Approval_requires_exact_r008_r009_candidate_references()
    {
        var approval = CreateApproval();

        Assert.Equal(MarketDataSnapshotId, approval.MarketDataSnapshotId);
        Assert.Equal(QubesOutputId, approval.QubesOutputId);
        Assert.Equal(QubesOutputHash, approval.QubesOutputHash);
        Assert.Equal(RiskReviewHash, approval.RiskReviewArtifactHash);
        Assert.Equal(6_000_000m, approval.TargetNotionalAmount);
        Assert.Equal("SandboxPreviewSizingOnly", approval.TargetNotionalScope);
        Assert.Equal(3, approval.Lines.Count);
    }

    [Fact]
    public void Operator_approval_id_is_deterministic_from_candidate_and_risk_hash()
    {
        var approval = CreateApproval();

        Assert.Equal(ExpectedOperatorApprovalId, approval.OperatorApprovalId);
        Assert.Equal(ExpectedOperatorApprovalId, CreateApproval().OperatorApprovalId);
    }

    [Fact]
    public void Approval_preserves_exact_r008_quantities()
    {
        var approval = CreateApproval();

        Assert.Equal(48.7m, approval.Lines.Single(line => line.Symbol == "AUDUSD").Quantity);
        Assert.Equal(7.0m, approval.Lines.Single(line => line.Symbol == "EURUSD").Quantity);
        Assert.Equal(17.5m, approval.Lines.Single(line => line.Symbol == "GBPUSD").Quantity);
    }

    [Fact]
    public void Approval_rejects_changed_quantities_snapshot_or_output_id()
    {
        var approval = CreateApproval();

        Assert.False(approval.MatchesExactCandidate(approval with { Lines = [new ApprovalLine("AUDUSD", "SELL", 48.8m), new ApprovalLine("EURUSD", "SELL", 7.0m), new ApprovalLine("GBPUSD", "BUY", 17.5m)] }));
        Assert.False(approval.MatchesExactCandidate(approval with { MarketDataSnapshotId = "changed-snapshot" }));
        Assert.False(approval.MatchesExactCandidate(approval with { QubesOutputId = "changed-output" }));
    }

    [Fact]
    public void Approval_is_future_bounded_sandbox_only_and_does_not_execute()
    {
        var approval = CreateApproval();

        Assert.Equal("FutureBoundedSandboxExecutionOnly", approval.ApprovalScope);
        Assert.True(approval.NotImmediateExecution);
        Assert.True(approval.RequiresSeparateExecutionPackage);
        Assert.True(approval.NoExecutionInThisPackage);
        Assert.True(approval.SandboxOnly);
        Assert.True(approval.NotProduction);
        Assert.True(approval.NotAccounting);
        Assert.True(approval.NotExecuted);
        Assert.True(approval.NotLedgerCommit);
    }

    [Fact]
    public void Approval_rejects_production_live_scope_and_requires_no_ledger_commit()
    {
        var approval = CreateApproval();

        Assert.NotEqual("ProductionLive", approval.ApprovalScope);
        Assert.True(approval.NoProduction);
        Assert.True(approval.NoLedgerCommit);
        Assert.False(approval.ProductionLiveReadinessClaimed);
        Assert.False(approval.LedgerCommitAllowed);
    }

    [Fact]
    public void Approval_keeps_offline_bbo_sandbox_preview_only_and_does_not_unlock_pnl_layers()
    {
        var approval = CreateApproval();

        Assert.Equal("OperatorProvidedLocalOfflinePolygonBbo", approval.MarketDataSource);
        Assert.Equal("SandboxPreviewSizingOnly", approval.PriceBasisScope);
        Assert.False(approval.TheoreticalPnlUnlocked);
        Assert.False(approval.NetPnlUnlocked);
        Assert.False(approval.AccountingPnlUnlocked);
        Assert.False(approval.ProductionPnlUnlocked);
    }

    [Fact]
    public void Approval_does_not_invent_accounting_identity_fields()
    {
        var approval = CreateApproval();

        Assert.Null(approval.AccountId);
        Assert.Null(approval.PortfolioId);
        Assert.Null(approval.StrategyId);
        Assert.Null(approval.SourceExecutionIntentId);
        Assert.Null(approval.AccountCurrency);
    }

    private static OperatorApproval CreateApproval()
    {
        var lines = new[]
        {
            new ApprovalLine("AUDUSD", "SELL", 48.7m),
            new ApprovalLine("EURUSD", "SELL", 7.0m),
            new ApprovalLine("GBPUSD", "BUY", 17.5m),
        };

        return new OperatorApproval(
            OperatorApprovalId: GenerateApprovalId(lines),
            MarketDataSnapshotId: MarketDataSnapshotId,
            QubesOutputId: QubesOutputId,
            QubesOutputHash: QubesOutputHash,
            RiskReviewArtifactHash: RiskReviewHash,
            TargetNotionalAmount: 6_000_000m,
            TargetNotionalScope: "SandboxPreviewSizingOnly",
            ApprovalScope: "FutureBoundedSandboxExecutionOnly",
            MarketDataSource: "OperatorProvidedLocalOfflinePolygonBbo",
            PriceBasisScope: "SandboxPreviewSizingOnly",
            NotImmediateExecution: true,
            RequiresSeparateExecutionPackage: true,
            NoExecutionInThisPackage: true,
            SandboxOnly: true,
            NotProduction: true,
            NotAccounting: true,
            NotExecuted: true,
            NotLedgerCommit: true,
            NoProduction: true,
            NoLedgerCommit: true,
            LedgerCommitAllowed: false,
            ProductionLiveReadinessClaimed: false,
            TheoreticalPnlUnlocked: false,
            NetPnlUnlocked: false,
            AccountingPnlUnlocked: false,
            ProductionPnlUnlocked: false,
            AccountId: null,
            PortfolioId: null,
            StrategyId: null,
            SourceExecutionIntentId: null,
            AccountCurrency: null,
            Lines: lines);
    }

    private static string GenerateApprovalId(IReadOnlyList<ApprovalLine> lines)
    {
        var hashInput = string.Join(
            "|",
            "PMS-QUBES-SANDBOX-OPERATOR-APPROVAL-R010",
            MarketDataSnapshotId,
            QubesOutputId,
            "6000000",
            string.Join("|", lines.Select(line => $"{line.Symbol}:{line.Side}:{line.Quantity:0.0}")),
            RiskReviewHash);

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(hashInput));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"pms-qubes-sandbox-operator-approval-r010:{hex[..16]}";
    }

    private sealed record OperatorApproval(
        string OperatorApprovalId,
        string MarketDataSnapshotId,
        string QubesOutputId,
        string QubesOutputHash,
        string RiskReviewArtifactHash,
        decimal TargetNotionalAmount,
        string TargetNotionalScope,
        string ApprovalScope,
        string MarketDataSource,
        string PriceBasisScope,
        bool NotImmediateExecution,
        bool RequiresSeparateExecutionPackage,
        bool NoExecutionInThisPackage,
        bool SandboxOnly,
        bool NotProduction,
        bool NotAccounting,
        bool NotExecuted,
        bool NotLedgerCommit,
        bool NoProduction,
        bool NoLedgerCommit,
        bool LedgerCommitAllowed,
        bool ProductionLiveReadinessClaimed,
        bool TheoreticalPnlUnlocked,
        bool NetPnlUnlocked,
        bool AccountingPnlUnlocked,
        bool ProductionPnlUnlocked,
        string? AccountId,
        string? PortfolioId,
        string? StrategyId,
        string? SourceExecutionIntentId,
        string? AccountCurrency,
        IReadOnlyList<ApprovalLine> Lines)
    {
        public bool MatchesExactCandidate(OperatorApproval other)
        {
            return MarketDataSnapshotId == other.MarketDataSnapshotId
                && QubesOutputId == other.QubesOutputId
                && QubesOutputHash == other.QubesOutputHash
                && TargetNotionalAmount == other.TargetNotionalAmount
                && Lines.SequenceEqual(other.Lines);
        }
    }

    private sealed record ApprovalLine(string Symbol, string Side, decimal Quantity);
}
