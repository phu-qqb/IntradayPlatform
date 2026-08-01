using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class Arch7bOfflineCoreConsumerBridgeContract
{
    public const string Version =
        "arch7b_core_to_position_consumer_offline_bridge_v1";
    public const string Result =
        "ARCH7B_CORE_TO_POSITION_CONSUMER_OFFLINE_BRIDGE_QUALIFIED";
    public const string HistoricalFixtureVersion =
        "arch7b_historical_pms_required_universe_fixture_v1";
    public const string HistoricalFixtureProvenance =
        "EXTRACTED_FROM_ARCH6C_AND_ARCH7B_VERSIONED_UNIT_FIXTURES";
    public const string SourceSessionId =
        Arch6dPmsShadowEvidencePackageReader.SourceSessionId;
    public const string TargetFingerprint =
        "72fa569ee28e4dec6272db0d69c7594b2be8853e9607dff3e78066378a0b5ee4";
}

public sealed record Arch7bHistoricalPmsUniverseFixture(
    string ContractVersion,
    string DataProvenance,
    PmsShadowPersistencePlan Plan,
    Arch7bRequiredPmsUniverse Universe,
    string FixtureEvidenceSha256);

public static class Arch7bHistoricalPmsUniverseFixtureFactory
{
    private static readonly DateTimeOffset AsOfUtc =
        new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    public static Arch7bHistoricalPmsUniverseFixture Build()
    {
        var plan = BuildPlan();
        var venueId = plan.SecurityMappings[0].VenueId;
        var lineageVersion = plan.TargetWeights[0].LineageVersion;
        var mappings = Enumerable.Range(1, 99).Select(index =>
        {
            var securityId = index.ToString();
            var symbol = TestPair(index - 1);
            return new PmsShadowSecurityMappingRow(
                plan.Ingestion.IngestionId,
                Arch5bHashing.GuidFromSha256($"instrument:{securityId}"),
                venueId,
                Arch5bHashing.GuidFromSha256($"venue-instrument:{securityId}"),
                securityId,
                symbol,
                "lmax-" + symbol,
                1m,
                1m,
                0.00001m,
                Arch5bHashing.Sha256Hex($"mapping:{securityId}"));
        }).ToArray();
        var weights = plan.ModelRuns.SelectMany(model =>
            StrategySecurityIds(model.StrategyId).Select((index, sourceOrder) =>
            {
                var securityId = index.ToString();
                return new PmsShadowTargetWeightRow(
                    model.ModelRunId,
                    Arch5bHashing.GuidFromSha256($"instrument:{securityId}"),
                    securityId,
                    0.001m,
                    model.TargetCloseUtc,
                    $"{model.StrategyId}:{securityId}",
                    sourceOrder,
                    model.OutputSha256,
                    lineageVersion);
            })).ToArray();
        plan = plan with { SecurityMappings = mappings, TargetWeights = weights };
        var universe = Arch7bRequiredPmsUniverseBuilder.Build(
            plan.Ingestion,
            plan.AccountSnapshot,
            plan.ModelRuns,
            plan.QubesInputSnapshots,
            plan.TargetWeights,
            plan.SecurityMappings,
            Arch7bBracketedGlobalFlatContract.TargetProfile,
            Arch7bOfflineCoreConsumerBridgeContract.TargetFingerprint,
            transactionReadOnly: true,
            pendingModelChanges: false);
        var fixtureSha = Arch5bHashing.HashCanonical(new
        {
            ContractVersion =
                Arch7bOfflineCoreConsumerBridgeContract.HistoricalFixtureVersion,
            DataProvenance =
                Arch7bOfflineCoreConsumerBridgeContract.HistoricalFixtureProvenance,
            plan.RowsetSha256,
            plan.Ingestion.SourceSessionId,
            universe.RequiredUniverseSha256
        });
        return new(
            Arch7bOfflineCoreConsumerBridgeContract.HistoricalFixtureVersion,
            Arch7bOfflineCoreConsumerBridgeContract.HistoricalFixtureProvenance,
            plan,
            universe,
            fixtureSha);
    }

    private static PmsShadowPersistencePlan BuildPlan()
    {
        var bundle = ValidBundle();
        var result = new Arch6aOperationalPositionShadowService().Build(bundle);
        var bindings = result.Preview.Runs
            .OrderBy(value => value.ModelRun.StrategyId, StringComparer.Ordinal)
            .Select(run =>
            {
                var strategy = run.ModelRun.StrategyId;
                return new Arch6cQubesInputBinding(
                    strategy,
                    Hash($"source:{strategy}"),
                    Hash($"overlay:{strategy}"),
                    null,
                    bundle.QubesToLmaxMappingSha256,
                    Hash($"input:{strategy}"),
                    72,
                    0,
                    AsOfUtc,
                    "ARCH6B_QUALIFIED_INPUT");
            }).ToArray();
        var artifacts = new List<Arch6cArtifactReference>();
        foreach (var binding in bindings)
        {
            artifacts.Add(Artifact("QUBES_SOURCE_SNAPSHOT",
                binding.SourceSnapshotSha256,
                $"inputs/{binding.StrategyId}/source.json"));
            artifacts.Add(Artifact("CONTENT_ADDRESSED_OVERLAY",
                binding.OverlaySha256,
                $"inputs/{binding.StrategyId}/overlay.json"));
            artifacts.Add(Artifact("QUBES_INPUT_SNAPSHOT",
                binding.InputSnapshotSha256,
                $"inputs/{binding.StrategyId}/input.json"));
        }
        foreach (var run in result.Preview.Runs)
            artifacts.Add(Artifact("QUBES_WEIGHTS_OUTPUT",
                run.Lineage.OutputSha256, run.Lineage.OutputRelativePath));

        return Arch6cPmsShadowPersistencePlanner.Build(new(
            "GO_ARCH6B_BIND_LMAX_OPERATIONAL_MARKET_DATA_TO_QUBES_INPUT_AND_QUALIFY_FRESH_DAILY_MODEL_POSITION_SHADOW_NO_ORDER",
            Arch7bOfflineCoreConsumerBridgeContract.SourceSessionId,
            Sha('8'),
            AsOfUtc.AddMinutes(-3),
            AsOfUtc,
            artifacts,
            bindings,
            result));
    }

    private static OperationalPositionShadowInputBundleV1 ValidBundle()
    {
        var lineage = ValidLineage();
        var securityIds = lineage.Runs.SelectMany(run => run.TargetCloseWeights)
            .Select(value => value.SecurityId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var sources = new[]
        {
            new Arch6aSourceFileEvidence(
                "lmax/eod/account.csv", Sha('a'), AsOfUtc)
        };
        var account = new OperationalAccountSnapshotV1(
            Arch6aOperationalPositionShadowContracts.AccountV1,
            Arch5bLineageContractVersions.TestAccountId,
            Arch5bLineageContractVersions.TestAccountScope,
            "USD",
            1_000_000m,
            DateOnly.FromDateTime(AsOfUtc.UtcDateTime),
            AsOfUtc,
            sources,
            Sha('b'),
            "BROKER_PORTAL_EOD",
            "HISTORICAL");
        var positions = new OperationalPositionSnapshotV1(
            Arch6aOperationalPositionShadowContracts.PositionV1,
            Arch5bLineageContractVersions.TestAccountId,
            account.ReportDate,
            AsOfUtc,
            [],
            true,
            false,
            true,
            [new Arch6aSourceFileEvidence(
                "lmax/eod/open-positions.csv", Sha('c'), AsOfUtc)],
            Sha('d'));
        var quotes = securityIds.Select((securityId, index) =>
            new OperationalMarketDataQuoteV1(
                securityId,
                $"lmax-{securityId}",
                $"FX{int.Parse(securityId):000}",
                1m + index / 10_000m,
                1.0001m + index / 10_000m,
                AsOfUtc.AddMilliseconds(-10),
                AsOfUtc,
                10,
                "lmax-capture-20260701",
                Sha('e'),
                "LMAX",
                "LMAX_DIRECT",
                [securityId])).ToArray();
        var market = new OperationalMarketDataSnapshotV1(
            Arch6aOperationalPositionShadowContracts.MarketDataV1,
            AsOfUtc,
            quotes,
            Sha('f'),
            0,
            0,
            0);
        var mappings = securityIds.Select(securityId =>
            new OperationalSecurityMappingV1(
                securityId,
                Arch5bHashing.GuidFromSha256($"instrument:{securityId}"),
                Arch5bHashing.GuidFromSha256("venue:lmax"),
                Arch5bHashing.GuidFromSha256($"venue-instrument:{securityId}"),
                $"FX{int.Parse(securityId):000}",
                $"lmax-{securityId}",
                1m,
                1m,
                0.00001m)).ToArray();
        var leaves = new BrokerWorkingLeavesObservationV1(
            Arch6aOperationalPositionShadowContracts.WorkingLeavesV1,
            Arch6aOperationalPositionShadowContracts.WorkingLeavesUnavailable,
            "LMAX",
            false,
            false,
            false,
            false,
            Arch6aOperationalPositionShadowContracts.WorkingLeavesReason,
            Arch6aOperationalPositionShadowContracts.WorkingLeavesImpact);
        var temporal = new Arch6aTemporalPolicyV1(
            Arch6aOperationalShadowMode.HISTORICAL_LMAX_OPERATIONAL_POSITION_SHADOW,
            AsOfUtc,
            AsOfUtc,
            false,
            false,
            Arch6aOperationalPositionShadowContracts.WorkingLeavesUnavailable);
        var safety = new Arch6aNoOrderSafetyV1(
            false, false, true, true, true, false, 0,
            PmsShadowStateContract.DisabledBrokerSend,
            0, 0, 0, 0, 0, 0, 0, 0);
        var draft = new OperationalPositionShadowInputBundleV1(
            Arch6aOperationalPositionShadowContracts.BundleV1,
            string.Empty,
            Arch6aOperationalPositionShadowContracts.Classification,
            Arch6aOperationalPositionShadowContracts.WorkingLeavesClassification,
            Arch6aOperationalPositionShadowContracts.EvidenceClassification,
            Arch6aOperationalPositionShadowContracts.NoOrderClassification,
            lineage,
            288,
            Arch6aOperationalPositionShadowContracts.QubesToLmaxMappingV1,
            Sha('9'),
            account,
            positions,
            market,
            leaves,
            mappings,
            temporal,
            safety);
        return draft with
        {
            BundleSha256 =
                Arch6aOperationalPositionShadowValidator.ComputeBundleSha256(draft)
        };
    }

    private static Arch5bSessionLineageContractV1 ValidLineage()
    {
        var strategies = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["INFX7"] = 4.5m,
            ["INFX8"] = 2.1m,
            ["INFX9"] = 1.4m,
            ["INFX10"] = 0.6m
        };
        var runs = strategies.OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(entry =>
            {
                var weights = Enumerable.Range(1, 72).Select(index =>
                    new Arch5bTargetCloseWeightV1(
                        index.ToString(),
                        "0.001",
                        0.001d,
                        index - 1,
                        $"202607011200:{index}",
                        Hash($"{entry.Key}:{index}"))).ToArray();
                return new Arch5bRunLineageContractV1(
                    Arch5bLineageContractVersions.LineageV1,
                    Arch5bLineageContractVersions.SourceQubesWeightsOutputV1,
                    "arch6b-session",
                    $"arch6b-{entry.Key}",
                    $"arch6b-{entry.Key}",
                    entry.Key,
                    entry.Value,
                    new string('a', 40),
                    Sha('1'),
                    Sha('2'),
                    "arch6b-bundle",
                    Sha('3'),
                    Hash($"output:{entry.Key}"),
                    100,
                    $"outputs/{entry.Key}/AggregatedWeights.txt",
                    Arch5bLineageContractVersions.OutputQubesWeightsOutputV1,
                    AsOfUtc,
                    AsOfUtc,
                    AsOfUtc,
                    "202607011200",
                    "PRODMANAGERV4_LAST_CHRONOLOGICAL_DATA_ROW",
                    "PASS",
                    0,
                    0,
                    true,
                    null,
                    null,
                    Arch5bLineageContractVersions.MissingMarketDataSnapshot,
                    Arch5bLineageContractVersions.EvidenceOnlyClassification,
                    true,
                    false,
                    false,
                    weights);
            }).ToArray();
        return new Arch5bSessionLineageContractV1(
            Arch5bLineageContractVersions.LineageV1,
            Arch5bLineageContractVersions.SourceQubesWeightsOutputV1,
            "arch6b-session",
            Arch5bLineageContractVersions.TestAccountId,
            Arch5bLineageContractVersions.TestAccountScope,
            new string('a', 40),
            Sha('1'),
            Sha('2'),
            "arch6b-bundle",
            AsOfUtc,
            Arch5bLineageContractVersions.EvidenceOnlyClassification,
            true,
            false,
            false,
            runs);
    }

    private static Arch6cArtifactReference Artifact(
        string type, string sha, string uri) => new(
        type,
        sha,
        100,
        uri.Replace('\\', '/'),
        "v1",
        AsOfUtc,
        "ARCH6B_EVIDENCE",
        PmsShadowStateContract.EvidenceClassification);

    private static IEnumerable<int> StrategySecurityIds(string strategyId) =>
        strategyId switch
        {
            "INFX7" => Enumerable.Range(1, 66),
            "INFX8" => Enumerable.Range(34, 66),
            "INFX9" => Enumerable.Range(1, 78),
            "INFX10" => Enumerable.Range(22, 78),
            _ => throw new InvalidDataException("ARCH7B_FIXTURE_UNKNOWN_STRATEGY")
        };

    private static string TestPair(int index)
    {
        var currencies = new[]
        {
            "USD", "EUR", "GBP", "JPY", "AUD", "CAD", "CHF",
            "NZD", "NOK", "SEK", "DKK", "SGD", "HKD"
        };
        return (from baseCurrency in currencies
                from quoteCurrency in currencies
                where baseCurrency != quoteCurrency
                select baseCurrency + quoteCurrency).ElementAt(index);
    }

    private static string Hash(string value) => Arch5bHashing.Sha256Hex(value);
    private static string Sha(char value) => new(value, 64);
}

public sealed record Arch7bOfflineCoreConsumerBridgeRequest(
    string EvidenceRoot,
    string OutputDirectory,
    string CoreRepositoryCommit,
    string CoreTree,
    string IntradayRepositoryCommit,
    string IntradayTree,
    string ExpectedCoreEvidenceSha256,
    string ExpectedContractFileSha256,
    string ExpectedFinalIndexSha256,
    string ExpectedAcquisitionManifestSha256,
    string ExpectedBracketContractVersion,
    string ExpectedDownloaderVersion,
    string ExpectedAccount,
    string ExpectedSourceSessionId,
    string ExpectedExecutionSemanticSha256,
    string ExpectedPositionSemanticSha256,
    Guid ExpectedSourceIngestionId,
    string ExpectedRequiredUniverseSha256,
    DateTimeOffset ExpectedPositionReportP2Utc,
    int ExpectedNormalizedCount,
    int ExpectedDerivedZeroCount,
    int ExpectedUnknownCount,
    string ConsumerExecutablePath,
    string ExpectedConsumerExecutableSha256,
    string SyntheticRunId,
    string SyntheticOwnerId,
    string SyntheticFutureAuthorizationId);

public sealed record Arch7bOfflineCoreConsumerBridgeResult(
    string Result,
    string ContractVersion,
    string BridgeManifestPath,
    string EvidenceSha256,
    int NormalizedCount,
    int DerivedZeroCount,
    int UnknownCount,
    int PositionSnapshotRowsToAdd,
    int PositionSnapshotLineRowsToAdd,
    Guid RuntimeSelectedPositionSnapshotId,
    string PackageManifestSha256,
    string NormalizedLineSetSha256,
    string RequiredUniverseSha256,
    bool NoSecret,
    bool NoDatabase,
    bool NoLmax,
    bool NoFix,
    bool NoOrder);

public static class Arch7bOfflineCoreConsumerBridgeRunner
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static Arch7bOfflineCoreConsumerBridgeResult Run(
        Arch7bOfflineCoreConsumerBridgeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireSha(request.CoreRepositoryCommit, 40, "ARCH7B_BRIDGE_CORE_COMMIT_MISMATCH");
        RequireSha(request.CoreTree, 40, "ARCH7B_BRIDGE_CORE_TREE_MISMATCH");
        RequireSha(request.IntradayRepositoryCommit, 40,
            "ARCH7B_BRIDGE_INTRADAY_COMMIT_MISMATCH");
        RequireSha(request.IntradayTree, 40, "ARCH7B_BRIDGE_INTRADAY_TREE_MISMATCH");
        RequireUtc(request.ExpectedPositionReportP2Utc);
        Require(!string.IsNullOrWhiteSpace(request.SyntheticRunId) &&
                !string.IsNullOrWhiteSpace(request.SyntheticOwnerId) &&
                !string.IsNullOrWhiteSpace(request.SyntheticFutureAuthorizationId),
            "ARCH7B_BRIDGE_SYNTHETIC_IDENTITY_REQUIRED");

        var executablePath = Path.GetFullPath(request.ConsumerExecutablePath);
        Require(File.Exists(executablePath), "ARCH7B_BRIDGE_CONSUMER_EXECUTABLE_MISSING");
        var executableSha = FileSha(executablePath);
        Require(executableSha == request.ExpectedConsumerExecutableSha256,
            "ARCH7B_BRIDGE_CONSUMER_EXECUTABLE_SHA_MISMATCH");
        var productVersion = FileVersionInfo.GetVersionInfo(executablePath)
            .ProductVersion ?? string.Empty;
        Require(productVersion.EndsWith(
                "+" + request.IntradayRepositoryCommit, StringComparison.Ordinal),
            "ARCH7B_BRIDGE_INTRADAY_COMMIT_MISMATCH");
        Require(request.ExpectedNormalizedCount == 99 &&
                request.ExpectedDerivedZeroCount == 99 &&
                request.ExpectedUnknownCount == 0,
            "ARCH7B_BRIDGE_EXPECTED_COUNTS_MISMATCH");

        var core = Arch7bCoreBracketEvidencePackageReader.Read(
            request.EvidenceRoot,
            new(
                request.CoreRepositoryCommit,
                request.ExpectedCoreEvidenceSha256,
                request.ExpectedContractFileSha256,
                request.ExpectedFinalIndexSha256));
        var qualificationPath = Path.Combine(Path.GetFullPath(request.EvidenceRoot),
            "validation", "core-master-qualification-summary.json");
        using var qualification = JsonDocument.Parse(
            File.ReadAllBytes(qualificationPath));
        Require(qualification.RootElement.GetProperty("tree").GetString() ==
                request.CoreTree,
            "ARCH7B_BRIDGE_CORE_TREE_MISMATCH");
        var acquisitionManifestSha = FileSha(Path.Combine(
            Path.GetFullPath(request.EvidenceRoot), "acquisition-manifest.json"));
        Require(acquisitionManifestSha == request.ExpectedAcquisitionManifestSha256,
            "ARCH7B_BRIDGE_ACQUISITION_MANIFEST_SHA_MISMATCH");
        Require(core.CoreContractVersion == request.ExpectedBracketContractVersion,
            "ARCH7B_BRIDGE_BRACKET_CONTRACT_MISMATCH");
        Require(core.DownloaderVersion == request.ExpectedDownloaderVersion,
            "ARCH7B_BRIDGE_DOWNLOADER_VERSION_MISMATCH");
        Require(core.AccountId == request.ExpectedAccount,
            "ARCH7B_BRIDGE_ACCOUNT_MISMATCH");
        Require(core.PositionReportP2Utc == request.ExpectedPositionReportP2Utc,
            "ARCH7B_BRIDGE_POSITION_REPORT_P2_MISMATCH");
        var executionSemanticSha = core.RecomputedSemantics?.ExecutionReports["T2"]
            .SemanticSha256 ?? throw new InvalidDataException(
                "ARCH7B_BRIDGE_EXECUTION_SEMANTIC_SHA_MISSING");
        var positionSemanticSha = core.RecomputedSemantics?.PositionReports["P2"]
            .SemanticSha256 ?? throw new InvalidDataException(
                "ARCH7B_BRIDGE_POSITION_SEMANTIC_SHA_MISSING");
        Require(executionSemanticSha == request.ExpectedExecutionSemanticSha256,
            "ARCH7B_BRIDGE_EXECUTION_SEMANTIC_SHA_MISMATCH");
        Require(positionSemanticSha == request.ExpectedPositionSemanticSha256,
            "ARCH7B_BRIDGE_POSITION_SEMANTIC_SHA_MISMATCH");

        var fixture = Arch7bHistoricalPmsUniverseFixtureFactory.Build();
        var universe = fixture.Universe;
        Require(universe.SourceIngestionId == request.ExpectedSourceIngestionId,
            "ARCH7B_BRIDGE_SOURCE_INGESTION_MISMATCH");
        Require(universe.SourceSessionId == request.ExpectedSourceSessionId &&
                request.ExpectedSourceSessionId ==
                    Arch7bOfflineCoreConsumerBridgeContract.SourceSessionId,
            "ARCH7B_BRIDGE_SOURCE_SESSION_MISMATCH");
        Require(universe.RequiredUniverseSha256 ==
                request.ExpectedRequiredUniverseSha256,
            "ARCH7B_BRIDGE_REQUIRED_UNIVERSE_SHA_MISMATCH");

        var snapshot = Arch7bGlobalFlatPositionSnapshotBuilder.Build(core, universe);
        Require(snapshot.NormalizedLineCount == request.ExpectedNormalizedCount,
            "ARCH7B_BRIDGE_NORMALIZED_COUNT_MISMATCH");
        Require(snapshot.DerivedZeroCount == request.ExpectedDerivedZeroCount,
            "ARCH7B_BRIDGE_DERIVED_ZERO_COUNT_MISMATCH");
        Require(snapshot.UnknownCount == request.ExpectedUnknownCount,
            "ARCH7B_BRIDGE_UNKNOWN_COUNT_MISMATCH");
        var smokeA = Arch7bGlobalFlatEconomicSmokeRunner.Run(snapshot, universe);
        var smokeB = Arch7bGlobalFlatEconomicSmokeRunner.Run(snapshot, universe);
        var bundle = Arch7bGlobalFlatOutputWriter.Write(
            request.OutputDirectory, core, universe, snapshot, smokeA, smokeB);
        var package = Arch7bPositionImportPackageReader.Read(bundle.OutputDirectory);
        var freshness = Arch7bPositionImportFreshnessPolicy.Evaluate(
            package,
            package.Snapshot.PositionSnapshotAsOfUtc,
            historicalFixture: true);
        var plan = Arch7bPositionImportPlanner.Build(
            package,
            freshness,
            new(
                true,
                true,
                4,
                288,
                99,
                null,
                null,
                [],
                0,
                0,
                true,
                false));
        Require(plan.Status == Arch7bPositionImportContract.New &&
                plan.PositionSnapshotRowsToAdd == 1 &&
                plan.PositionSnapshotLineRowsToAdd == 99,
            "ARCH7B_BRIDGE_IMPORT_PLAN_MISMATCH");

        var positionRow = new PmsShadowPositionSnapshotRow(
            snapshot.PositionSnapshotId,
            universe.SourceIngestionId,
            universe.SourceAccountSnapshotId,
            DateOnly.FromDateTime(snapshot.PositionSnapshotAsOfUtc.UtcDateTime),
            snapshot.PositionSnapshotAsOfUtc,
            snapshot.EvidenceSha256,
            true,
            false,
            true,
            Arch7bBracketedGlobalFlatContract.PositionAuthorityCode);
        var selected = PmsShadowPositionSnapshotForSlotSelection.Select(
            [positionRow], snapshot.PositionSnapshotAsOfUtc);
        PmsShadowPositionSnapshotForSlotSelection.ValidateSelected(
            selected,
            fixture.Plan.Ingestion,
            fixture.Plan.AccountSnapshot,
            snapshot.Lines.Select(value => new PmsShadowPositionSnapshotLineRow(
                value.PositionSnapshotId,
                value.InstrumentId,
                value.SecurityId,
                value.Symbol,
                value.CurrentBaseQuantity)).ToArray(),
            fixture.Plan.SecurityMappings);

        var manifestCore = new
        {
            ContractVersion = Arch7bOfflineCoreConsumerBridgeContract.Version,
            QualificationOnly = true,
            request.CoreRepositoryCommit,
            request.CoreTree,
            request.IntradayRepositoryCommit,
            request.IntradayTree,
            CoreFinalIndexPath = "validation/final-evidence-index.json",
            CoreFinalIndexSha256 = core.FinalIndexSha256,
            AcquisitionManifestSha256 = acquisitionManifestSha,
            BracketContractVersion = core.CoreContractVersion,
            BracketContractSha256 = core.ContractFileSha256,
            core.DownloaderVersion,
            Account = core.AccountId,
            SourceSessionId = universe.SourceSessionId,
            PositionReportP2Utc = core.PositionReportP2Utc,
            ExecutionSemanticSha256 = executionSemanticSha,
            PositionSemanticSha256 = positionSemanticSha,
            SourceIngestionId = universe.SourceIngestionId,
            universe.RequiredUniverseSha256,
            FixtureContractVersion = fixture.ContractVersion,
            fixture.DataProvenance,
            fixture.FixtureEvidenceSha256,
            ConsumerExecutablePath = Path.GetFileName(executablePath),
            ConsumerExecutableSha256 = executableSha,
            ExpectedNormalizedCount = 99,
            ExpectedDerivedZeroCount = 99,
            ExpectedUnknownCount = 0,
            ActualNormalizedCount = snapshot.NormalizedLineCount,
            ActualDerivedZeroCount = snapshot.DerivedZeroCount,
            ActualUnknownCount = snapshot.UnknownCount,
            BridgeOutputManifestSha256 = bundle.ManifestSha256,
            PositionPackageManifestSha256 = package.ManifestSha256,
            snapshot.NormalizedLineSetSha256,
            ProjectedSnapshotIdFixture = snapshot.PositionSnapshotId,
            ImportResult = plan.Status,
            plan.PositionSnapshotRowsToAdd,
            plan.PositionSnapshotLineRowsToAdd,
            RuntimeSelectionResult = "SELECTED",
            RuntimeSelectedPositionSnapshotId = selected.PositionSnapshotId,
            Authority = Arch7bBracketedGlobalFlatContract.PositionAuthorityCode,
            FreshnessClassification = Arch7bPositionImportContract.HistoricalFixture,
            SyntheticIdentityPresent = true,
            NoSecret = true,
            NoDatabase = true,
            NoLmax = true,
            NoFix = true,
            NoOrder = true
        };
        var evidenceSha = Arch5bHashing.HashCanonical(manifestCore);
        var manifest = new { Bridge = manifestCore, EvidenceSha256 = evidenceSha };
        var manifestPath = Path.Combine(bundle.OutputDirectory,
            "offline-core-consumer-bridge-manifest.json");
        File.WriteAllBytes(manifestPath,
            JsonSerializer.SerializeToUtf8Bytes(manifest, Json));

        return new(
            Arch7bOfflineCoreConsumerBridgeContract.Result,
            Arch7bOfflineCoreConsumerBridgeContract.Version,
            manifestPath,
            evidenceSha,
            snapshot.NormalizedLineCount,
            snapshot.DerivedZeroCount,
            snapshot.UnknownCount,
            plan.PositionSnapshotRowsToAdd,
            plan.PositionSnapshotLineRowsToAdd,
            selected.PositionSnapshotId,
            package.ManifestSha256,
            snapshot.NormalizedLineSetSha256,
            universe.RequiredUniverseSha256,
            true,
            true,
            true,
            true,
            true);
    }

    private static string FileSha(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));

    private static void RequireSha(string value, int length, string code) =>
        Require(value.Length == length && value.All(character =>
                char.IsAsciiHexDigit(character) && !char.IsUpper(character)), code);

    private static void RequireUtc(DateTimeOffset value) =>
        Require(value.Offset == TimeSpan.Zero, "ARCH7B_BRIDGE_P2_NOT_UTC");

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
