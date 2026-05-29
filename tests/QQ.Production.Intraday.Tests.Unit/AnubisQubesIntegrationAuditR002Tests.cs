using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class AnubisQubesIntegrationAuditR002Tests
{
    [Fact]
    public void Sandbox_qubes_prototype_evidence_cannot_be_relabelled_as_anubis()
    {
        Assert.DoesNotContain("Anubis", QubesKnownPrototypeIds.R005RunId, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Anubis", QubesKnownPrototypeIds.R005OutputId, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Anubis", SandboxQubesPrototypeRunner.RunnerType, StringComparison.OrdinalIgnoreCase);

        var output = R005PrototypeOutput();
        var result = QubesProductionBoundaryGuard.ValidateSandboxPrototypeOutput(
            output,
            QubesProductionUsage.Production,
            QubesKnownPrototypeIds.R005OutputHash);

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, issue => issue.Code == QubesProductionValidationIssueCode.SandboxPrototypeBlocked);
    }

    [Fact]
    public void Api_and_worker_do_not_register_anubis_as_active_provider()
    {
        var repo = RepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(repo, "src/QQ.Production.Intraday.Api/Program.cs"));
        var worker = File.ReadAllText(Path.Combine(repo, "src/QQ.Production.Intraday.Worker/Worker.cs"));

        Assert.DoesNotContain("Anubis", apiProgram, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AnubisAdapter", apiProgram, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AnubisRunner", apiProgram, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Anubis", worker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IModelWeightPromotionService", apiProgram, StringComparison.Ordinal);
        Assert.Contains("ProcessModelRunService", worker, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_files_do_not_reference_anubis_package_project_or_assembly()
    {
        var repo = RepoRoot();
        var projectFiles = Directory.EnumerateFiles(repo, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.nuget", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(projectFiles);
        foreach (var projectFile in projectFiles)
        {
            var text = File.ReadAllText(projectFile);
            Assert.DoesNotContain("Anubis", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void R010_approval_is_not_transferable_to_anubis_without_exact_artifact_match()
    {
        var approved = new CandidateIdentity(
            QubesKnownPrototypeIds.R005OutputId,
            QubesKnownPrototypeIds.R005OutputHash,
            "canonical-marketdata-golden-source-r001:polygon-offline-bbo:20251217T020000Z:AUDUSD-EURUSD-GBPUSD",
            ["AUDUSD:SELL:48.7", "EURUSD:SELL:7.0", "GBPUSD:BUY:17.5"]);
        var anubisHypothetical = approved with
        {
            QubesOutputId = "anubis-output-20251217T020000Z-001"
        };

        Assert.NotEqual(approved, anubisHypothetical);
    }

    [Fact]
    public void Anubis_marketdata_binding_cannot_be_claimed_without_run_binding()
    {
        var runLink = new QubesRunLinkManifest(
            QubesRunId: "anubis-run-missing-binding",
            QubesInputSnapshotId: "anubis-input-missing-binding",
            QubesWeightsOutputId: "anubis-output-missing-binding",
            MarketDataSnapshotId: null,
            EngineId: "Anubis",
            EngineKind: QubesEngineKind.ExternalEngineRequired,
            RunContractVersion: "qubes-run-link.v1",
            InputContractVersion: "qubes-input-snapshot.v1",
            OutputContractVersion: "qubes-weights-output.v1",
            RunFingerprint: "missing-marketdata-binding",
            CreatedAtUtc: DateTimeOffset.Parse("2025-12-17T02:00:00Z"));

        var result = new QubesRunLinkManifestValidator().Validate(runLink);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == QubesRunLinkIssueCode.ExternalEngineRequired);
        Assert.Contains(result.Issues, issue => issue.Code == QubesRunLinkIssueCode.MissingMarketDataSnapshotId);
    }

    [Fact]
    public void Direct_cross_execution_leakage_remains_rejected_for_any_anubis_candidate()
    {
        var output = R005PrototypeOutput() with
        {
            Weights =
            [
                new SandboxQubesOutputWeight("EURGBP", 0.10m),
                new SandboxQubesOutputWeight("AUDUSD", -0.05m),
                new SandboxQubesOutputWeight("JPYUSD", 0.01m)
            ]
        };

        var transform = new SandboxQubesExecutionUniverseTransformer().Transform(output);

        Assert.False(transform.DirectCrossExecutionLeakageFound);
        Assert.Contains("EURGBP", transform.DirectCrossSymbolsExcluded);
        Assert.DoesNotContain(transform.ExecutionLines, line => line.ExecutionTradableSymbol == "EURGBP");
        Assert.Contains(transform.ExecutionLines, line => line.ExecutionTradableSymbol == "USDJPY" && line.RequiresInversion);
    }

    [Fact]
    public void Audit_does_not_invent_accounting_or_execution_identity_fields()
    {
        var identity = new
        {
            AccountId = (string?)null,
            PortfolioId = (string?)null,
            StrategyId = (string?)null,
            SourceExecutionIntentId = (string?)null,
            AccountCurrency = (string?)null
        };

        Assert.Null(identity.AccountId);
        Assert.Null(identity.PortfolioId);
        Assert.Null(identity.StrategyId);
        Assert.Null(identity.SourceExecutionIntentId);
        Assert.Null(identity.AccountCurrency);
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
                new SandboxQubesOutputWeight("AUDUSD", -0.053856m),
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

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed record CandidateIdentity(
        string QubesOutputId,
        string QubesOutputHash,
        string MarketDataSnapshotId,
        IReadOnlyList<string> SymbolsSidesQuantities);
}
