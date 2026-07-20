using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch6aOperationalPositionShadowTests
{
    private static readonly DateTimeOffset HistoricalAsOf = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Historical_operational_position_shadow_completes_without_external_action()
    {
        var result = new Arch6aOperationalPositionShadowService().Build(ValidBundle());

        Assert.Equal(4, result.TargetPositionStageCount);
        Assert.Equal(4, result.PositionOnlyDriftStageCount);
        Assert.Equal(4, result.BrokerAdjustedDriftBlockedStageCount);
        Assert.True(result.CompletedNoExternal);
        Assert.Equal(0, result.TradeIntentCount);
        Assert.False(result.AccountingEligible);
        Assert.False(result.ExecutionAllowed);
        Assert.All(result.Preview.Runs, run =>
        {
            Assert.Equal(Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW, run.TargetPositions.ComputationStatus);
            Assert.Equal(72, run.TargetPositions.Positions.Count);
            Assert.Equal(Arch5bComputationStatus.BLOCKED_BROKER_WORKING_LEAVES_UNOBSERVABLE, run.DriftSnapshot.ComputationStatus);
            Assert.Equal(Arch5bWorkingLeavesStatus.UNAVAILABLE_WITH_CURRENT_LMAX_INTERFACES, run.DriftSnapshot.WorkingLeavesStatus);
            Assert.Equal(72, run.DriftSnapshot.PositionOnlyDrifts!.Count);
            Assert.Empty(run.DriftSnapshot.Drifts);
            Assert.True(run.DriftSnapshot.PositionOnlyDriftCalculated);
            Assert.False(run.DriftSnapshot.BrokerAdjustedDriftCalculated);
            Assert.Equal("BROKER_WORKING_LEAVES_UNOBSERVABLE", run.DriftSnapshot.BrokerAdjustedDriftBlocker);
            Assert.True(run.ManualPaperCycle.CompletedNoExternal);
            Assert.Equal("CompletedNoExternal", run.R009.Status);
            Assert.Equal(0, run.R009.ExecutionIntentCount);
            Assert.False(run.R009.ExecutionAllowed);
            Assert.True(run.R009.NotAnOrder);
            Assert.True(run.R009.NoBrokerRoute);
            Assert.True(run.R009.NoFixMessage);
            Assert.False(run.R009.OrderEntryEnabled);
            Assert.Equal("DISABLED_NO_ORDER_ENTRY", run.R009.BrokerSendStatus);
        });
    }

    [Fact]
    public void Start_of_day_mode_preserves_prior_eod_classification()
    {
        var bundle = ValidBundle(Arch6aOperationalShadowMode.LMAX_START_OF_DAY_POSITION_SHADOW);
        var validation = Arch6aOperationalPositionShadowValidator.Validate(bundle);
        var result = new Arch6aOperationalPositionShadowService().Build(bundle);

        Assert.True(validation.IsValid, string.Join(";", validation.Issues));
        Assert.False(bundle.TemporalPolicy.AccountStateIsCurrent);
        Assert.True(bundle.TemporalPolicy.StartOfDayAssumption);
        Assert.Equal(4, result.PositionOnlyDriftStageCount);
    }

    [Fact]
    public void Explicit_nonempty_positions_feed_position_only_drift()
    {
        var result = new Arch6aOperationalPositionShadowService().Build(ValidBundle(nonEmptyPositions: true));

        Assert.All(result.Preview.Runs, run =>
        {
            var drift = run.DriftSnapshot.PositionOnlyDrifts!.Single(value => value.Symbol == "FX001");
            Assert.Equal(drift.TargetBaseQuantity - 25_000m, drift.PositionOnlyDeltaBaseQuantity);
        });
    }

    [Fact]
    public void Repeated_build_is_deterministic()
    {
        var service = new Arch6aOperationalPositionShadowService();
        var bundle = ValidBundle();
        var first = service.Build(bundle);
        var second = service.Build(bundle);

        Assert.Equal(first.ResultSha256, second.ResultSha256);
        Assert.Equal(first.Preview.PreviewSha256, second.Preview.PreviewSha256);
    }

    [Theory]
    [InlineData("account-report-absent", "ACCOUNT_SOURCE_EVIDENCE_INVALID")]
    [InlineData("wrong-account", "REAL_OR_UNAPPROVED_ACCOUNT_REJECTED")]
    [InlineData("real-account", "REAL_OR_UNAPPROVED_ACCOUNT_REJECTED")]
    [InlineData("nav-absent", "ACCOUNT_NAV_OR_CURRENCY_MISSING")]
    [InlineData("position-empty-inferred", "POSITION_EMPTY_STATE_INFERRED")]
    [InlineData("position-empty-not-observed", "POSITION_EMPTY_STATE_NOT_EXPLICIT")]
    [InlineData("market-partial", "MARKET_DATA_COVERAGE_INCOMPLETE")]
    [InlineData("mapping-incomplete", "SECURITY_MAPPING_COVERAGE_INCOMPLETE")]
    [InlineData("staleness-undeclared", "MARKET_DATA_STALENESS_UNDECLARED")]
    [InlineData("working-leaves-empty", "WORKING_LEAVES_FALSE_EMPTY_OR_AUTHORITY")]
    [InlineData("working-leaves-authority", "WORKING_LEAVES_FALSE_EMPTY_OR_AUTHORITY")]
    [InlineData("account-api", "FORBIDDEN_EXTERNAL_OPERATION_OBSERVED")]
    [InlineData("api-key", "FORBIDDEN_EXTERNAL_OPERATION_OBSERVED")]
    [InlineData("polygon", "FORBIDDEN_EXTERNAL_OPERATION_OBSERVED")]
    [InlineData("databento", "FORBIDDEN_EXTERNAL_OPERATION_OBSERVED")]
    [InlineData("trade-intent", "NO_ORDER_BOUNDARY_INVALID")]
    [InlineData("broker-send", "FORBIDDEN_EXTERNAL_OPERATION_OBSERVED")]
    [InlineData("fix-order-entry", "FORBIDDEN_EXTERNAL_OPERATION_OBSERVED")]
    [InlineData("db-apply", "FORBIDDEN_EXTERNAL_OPERATION_OBSERVED")]
    [InlineData("unknown-version", "UNKNOWN_CONTRACT_VERSION")]
    public void Fail_closed_matrix_rejects_unsafe_or_incomplete_inputs(string mutation, string expectedIssue)
    {
        var invalid = Mutate(ValidBundle(), mutation);
        var validation = Arch6aOperationalPositionShadowValidator.Validate(invalid);

        Assert.False(validation.IsValid);
        Assert.Contains(expectedIssue, validation.Issues);
        Assert.Throws<InvalidDataException>(() => new Arch6aOperationalPositionShadowService().Build(invalid));
    }

    [Fact]
    public void Direct_quote_wins_over_available_usd_cross()
    {
        var projector = new Arch6aLmaxUsdCrossRateProjector();
        var result = projector.Project("EUR", "GBP",
        [
            Quote("eur-usd", "1", "EUR", "USD", 1.10m, 1.11m, HistoricalAsOf),
            Quote("gbp-usd", "2", "GBP", "USD", 1.29m, 1.30m, HistoricalAsOf),
            Quote("eur-gbp", "3", "EUR", "GBP", 0.85m, 0.86m, HistoricalAsOf)
        ], TimeSpan.FromSeconds(1));

        Assert.Equal("LMAX_DIRECT", result.ProjectionMethod);
        Assert.False(result.IsReconstructed);
        Assert.Single(result.Provenance);
    }

    [Fact]
    public void Missing_pair_is_reconstructed_from_contemporaneous_lmax_usd_legs()
    {
        var result = new Arch6aLmaxUsdCrossRateProjector().Project("EUR", "GBP",
        [
            Quote("eur-usd", "1", "EUR", "USD", 1.1000m, 1.1002m, HistoricalAsOf),
            Quote("gbp-usd", "2", "GBP", "USD", 1.2940m, 1.2942m, HistoricalAsOf.AddMilliseconds(250))
        ], TimeSpan.FromSeconds(1));

        Assert.Equal(1.1000m / 1.2942m, result.Bid);
        Assert.Equal(1.1002m / 1.2940m, result.Ask);
        Assert.Equal("LMAX_USD_TWO_LEG_CROSS_V1", result.ProjectionMethod);
        Assert.True(result.IsReconstructed);
        Assert.Equal(2, result.Provenance.Count);
    }

    [Fact]
    public void Usd_projection_rejects_stale_missing_or_non_lmax_legs()
    {
        var projector = new Arch6aLmaxUsdCrossRateProjector();
        var eur = Quote("eur-usd", "1", "EUR", "USD", 1.1m, 1.2m, HistoricalAsOf);
        var gbp = Quote("gbp-usd", "2", "GBP", "USD", 1.2m, 1.3m, HistoricalAsOf.AddSeconds(2));

        Assert.Contains("SKEW_EXCEEDED", Assert.Throws<InvalidOperationException>(() =>
            projector.Project("EUR", "GBP", [eur, gbp], TimeSpan.FromSeconds(1))).Message, StringComparison.Ordinal);
        Assert.Equal("ARCH6A_LMAX_NON_LMAX_QUOTE_REJECTED", Assert.Throws<InvalidOperationException>(() =>
            projector.Project("EUR", "GBP", [eur with { SourceSystem = "OTHER" }, gbp], TimeSpan.FromSeconds(3))).Message);
        Assert.Contains("USD_LEG_MISSING", Assert.Throws<InvalidOperationException>(() =>
            projector.Project("EUR", "GBP", [eur], TimeSpan.FromSeconds(3))).Message, StringComparison.Ordinal);
    }

    private static OperationalPositionShadowInputBundleV1 ValidBundle(
        Arch6aOperationalShadowMode mode = Arch6aOperationalShadowMode.HISTORICAL_LMAX_OPERATIONAL_POSITION_SHADOW,
        bool nonEmptyPositions = false)
    {
        var lineage = ValidLineage();
        var securityIds = lineage.Runs.SelectMany(run => run.TargetCloseWeights)
            .Select(weight => weight.SecurityId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var accountAsOf = mode == Arch6aOperationalShadowMode.HISTORICAL_LMAX_OPERATIONAL_POSITION_SHADOW
            ? HistoricalAsOf
            : HistoricalAsOf.AddDays(-1);
        var source = new[] { new Arch6aSourceFileEvidence("lmax/eod/account.csv", Sha('a'), accountAsOf) };
        var account = new OperationalAccountSnapshotV1(
            Arch6aOperationalPositionShadowContracts.AccountV1,
            Arch5bLineageContractVersions.TestAccountId,
            Arch5bLineageContractVersions.TestAccountScope,
            "USD",
            1_000_000m,
            DateOnly.FromDateTime(accountAsOf.UtcDateTime),
            accountAsOf,
            source,
            Sha('b'),
            "BROKER_PORTAL_EOD",
            "HISTORICAL");
        var observedPositions = nonEmptyPositions
            ? new[] { new OperationalPositionV1("1", "FX001", 25_000m) }
            : [];
        var positions = new OperationalPositionSnapshotV1(
            Arch6aOperationalPositionShadowContracts.PositionV1,
            Arch5bLineageContractVersions.TestAccountId,
            account.ReportDate,
            accountAsOf,
            observedPositions,
            EmptyStateWasExplicitlyObserved: !nonEmptyPositions,
            EmptyStateWasInferred: false,
            BrokerAuthority: true,
            [new Arch6aSourceFileEvidence("lmax/eod/open-positions.csv", Sha('c'), accountAsOf)],
            Sha('d'));
        var quotes = securityIds.Select((securityId, index) => new OperationalMarketDataQuoteV1(
            securityId,
            $"lmax-{securityId}",
            $"FX{int.Parse(securityId):000}",
            1m + index / 10_000m,
            1.0001m + index / 10_000m,
            HistoricalAsOf.AddMilliseconds(-10),
            HistoricalAsOf,
            10,
            "lmax-capture-20260701",
            Sha('e'),
            "LMAX",
            "LMAX_DIRECT",
            [securityId])).ToArray();
        var market = new OperationalMarketDataSnapshotV1(
            Arch6aOperationalPositionShadowContracts.MarketDataV1,
            HistoricalAsOf,
            quotes,
            Sha('f'),
            0,
            0,
            0);
        var mappings = securityIds.Select(securityId => new OperationalSecurityMappingV1(
            securityId,
            Arch5bHashing.GuidFromSha256($"instrument:{securityId}"),
            Arch5bHashing.GuidFromSha256("venue:lmax"),
            Arch5bHashing.GuidFromSha256($"venue-instrument:{securityId}"),
            $"FX{int.Parse(securityId):000}",
            $"lmax-{securityId}",
            1m,
            1m,
            0.00001m)).ToArray();
        var workingLeaves = new BrokerWorkingLeavesObservationV1(
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
            mode,
            accountAsOf,
            HistoricalAsOf,
            AccountStateIsCurrent: false,
            StartOfDayAssumption: mode == Arch6aOperationalShadowMode.LMAX_START_OF_DAY_POSITION_SHADOW,
            Arch6aOperationalPositionShadowContracts.WorkingLeavesUnavailable);
        var safety = SafeBoundary();
        var draft = new OperationalPositionShadowInputBundleV1(
            Arch6aOperationalPositionShadowContracts.BundleV1,
            string.Empty,
            Arch6aOperationalPositionShadowContracts.Classification,
            Arch6aOperationalPositionShadowContracts.WorkingLeavesClassification,
            Arch6aOperationalPositionShadowContracts.EvidenceClassification,
            Arch6aOperationalPositionShadowContracts.NoOrderClassification,
            lineage,
            288,
            account,
            positions,
            market,
            workingLeaves,
            mappings,
            temporal,
            safety);
        return Rehash(draft);
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
        var runs = strategies.OrderBy(value => value.Key, StringComparer.Ordinal).Select(entry =>
        {
            var weights = Enumerable.Range(1, 72).Select(index =>
                new Arch5bTargetCloseWeightV1(
                    index.ToString(),
                    "0.001",
                    0.001d,
                    index - 1,
                    $"202607011200:{index}",
                    Arch5bHashing.Sha256Hex($"{entry.Key}:{index}"))).ToArray();
            return new Arch5bRunLineageContractV1(
                Arch5bLineageContractVersions.LineageV1,
                Arch5bLineageContractVersions.SourceQubesWeightsOutputV1,
                "arch5d1v-session",
                $"arch5d1v-{entry.Key}",
                $"arch5d1v-{entry.Key}",
                entry.Key,
                entry.Value,
                new string('a', 40),
                Sha('1'),
                Sha('2'),
                "arch5d1v-bundle",
                Sha('3'),
                Sha('4'),
                100,
                $"outputs/{entry.Key}/AggregatedWeights.txt",
                Arch5bLineageContractVersions.OutputQubesWeightsOutputV1,
                HistoricalAsOf,
                HistoricalAsOf,
                HistoricalAsOf,
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
            "arch5d1v-session",
            Arch5bLineageContractVersions.TestAccountId,
            Arch5bLineageContractVersions.TestAccountScope,
            new string('a', 40),
            Sha('1'),
            Sha('2'),
            "arch5d1v-bundle",
            HistoricalAsOf,
            Arch5bLineageContractVersions.EvidenceOnlyClassification,
            true,
            false,
            false,
            runs);
    }

    private static Arch6aNoOrderSafetyV1 SafeBoundary() => new(
        false, false, true, true, true, false, 0, "DISABLED_NO_ORDER_ENTRY",
        0, 0, 0, 0, 0, 0, 0, 0);

    private static OperationalPositionShadowInputBundleV1 Mutate(
        OperationalPositionShadowInputBundleV1 bundle,
        string mutation)
    {
        var firstQuote = bundle.MarketData.Quotes[0];
        var safety = bundle.Safety;
        bundle = mutation switch
        {
            "account-report-absent" => bundle with { Account = bundle.Account with { SourceFiles = [] } },
            "wrong-account" => bundle with { Account = bundle.Account with { AccountId = "other" } },
            "real-account" => bundle with { Account = bundle.Account with { AccountId = Arch5bLineageContractVersions.RealAccountId } },
            "nav-absent" => bundle with { Account = bundle.Account with { NavOrEquity = 0m } },
            "position-empty-inferred" => bundle with { Positions = bundle.Positions with { EmptyStateWasInferred = true } },
            "position-empty-not-observed" => bundle with { Positions = bundle.Positions with { EmptyStateWasExplicitlyObserved = false } },
            "market-partial" => bundle with { MarketData = bundle.MarketData with { Quotes = bundle.MarketData.Quotes.Skip(1).ToArray() } },
            "mapping-incomplete" => bundle with { SecurityMappings = bundle.SecurityMappings.Skip(1).ToArray() },
            "staleness-undeclared" => bundle with { MarketData = bundle.MarketData with { Quotes = [firstQuote with { StalenessMilliseconds = -1 }, .. bundle.MarketData.Quotes.Skip(1)] } },
            "working-leaves-empty" => bundle with { BrokerWorkingLeaves = bundle.BrokerWorkingLeaves with { EmptyStateObserved = true } },
            "working-leaves-authority" => bundle with { BrokerWorkingLeaves = bundle.BrokerWorkingLeaves with { BrokerAuthority = true } },
            "account-api" => bundle with { Safety = safety with { AccountApiCallCount = 1 } },
            "api-key" => bundle with { Safety = safety with { ApiKeyOperationCount = 1 } },
            "polygon" => bundle with { Safety = safety with { PolygonCallCount = 1 } },
            "databento" => bundle with { Safety = safety with { DatabentoCallCount = 1 } },
            "trade-intent" => bundle with { Safety = safety with { TradeIntentCount = 1 } },
            "broker-send" => bundle with { Safety = safety with { BrokerSendCount = 1 } },
            "fix-order-entry" => bundle with { Safety = safety with { FixOrderEntryCount = 1 } },
            "db-apply" => bundle with { Safety = safety with { DbApplyCount = 1 } },
            "unknown-version" => bundle with { ContractVersion = "unknown-v99" },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        return Rehash(bundle);
    }

    private static OperationalPositionShadowInputBundleV1 Rehash(OperationalPositionShadowInputBundleV1 bundle)
        => bundle with { BundleSha256 = Arch6aOperationalPositionShadowValidator.ComputeBundleSha256(bundle) };

    private static Arch6aLmaxFxQuote Quote(
        string instrumentId,
        string securityId,
        string baseCurrency,
        string quoteCurrency,
        decimal bid,
        decimal ask,
        DateTimeOffset timestamp)
        => new(instrumentId, securityId, baseCurrency, quoteCurrency, bid, ask, timestamp, timestamp, Sha('8'));

    private static string Sha(char value) => new(value, 64);
}
