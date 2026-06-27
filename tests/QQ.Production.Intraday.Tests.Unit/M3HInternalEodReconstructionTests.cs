using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class M3HInternalEodReconstructionTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 26, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly ReportDate = new(2026, 6, 26);

    [Fact]
    public void M3H_source_taxonomy_classifies_lmax_file_as_candidate_and_internal_as_non_authority()
    {
        var lmax = M3EodSourceTaxonomyPolicy.Classify(M3EodEvidenceSourceKind.LMAX_OFFICIAL_EOD_REPORT_FILE);
        var internalReconstruction = M3EodSourceTaxonomyPolicy.Classify(M3EodEvidenceSourceKind.QQ_INTERNAL_EOD_RECONSTRUCTION);

        Assert.True(lmax.IsBrokerAuthorityCandidate);
        Assert.False(lmax.IsBrokerAuthority);
        Assert.False(internalReconstruction.IsBrokerAuthority);
        Assert.True(internalReconstruction.IsOperationalTruth);
        Assert.False(M3EodSourceTaxonomyPolicy.CanPromoteToBrokerPositionAuthority(M3EodEvidenceSourceKind.RECONSTRUCTED));
        Assert.False(M3EodSourceTaxonomyPolicy.CanPromoteToBrokerOpenOrderAuthority(M3EodEvidenceSourceKind.MANUAL_EVIDENCE));
    }

    [Fact]
    public void M3H_internal_reconstruction_from_fixture_fills_builds_fill_rows_and_summaries()
    {
        var state = StateWithFill();
        var venue = state.Venues.Single(x => x.Name == "LMAX");
        var account = state.BrokerAccounts.Single();
        var instrument = state.Instruments.Single(x => x.Symbol == "EURUSD");
        var anchors = new[] { Anchor(instrument.Id, 100m) };

        var result = InternalEodReconstructionBuilder.Build(ReportDate, venue, account, state.Instruments, state.InstrumentAliases, state.Fills, anchors);

        Assert.Equal(InternalEodReconstructionStatus.INTERNAL_EOD_RECONSTRUCTED_COMPLETE, result.Status);
        Assert.False(result.IsBrokerAuthority);
        var fill = Assert.Single(result.Fills);
        Assert.Equal("BRK-EXEC-M3H-1", fill.BrokerExecutionId);
        Assert.Equal("EUR/USD", fill.LmaxSymbol);
        Assert.Equal(-10000m, fill.SignedBaseQuantity);
        var summary = Assert.Single(result.TradeSummaries);
        Assert.Equal(10000m, summary.BaseQuantity);
        Assert.Equal(-10000m, summary.SignedBaseQuantity);
    }

    [Fact]
    public void M3H_partial_reconstruction_without_prior_anchor_remains_non_authority()
    {
        var state = StateWithFill();
        var venue = state.Venues.Single(x => x.Name == "LMAX");
        var account = state.BrokerAccounts.Single();

        var result = InternalEodReconstructionBuilder.Build(ReportDate, venue, account, state.Instruments, state.InstrumentAliases, state.Fills, []);

        Assert.Equal(InternalEodReconstructionStatus.INTERNAL_EOD_RECONSTRUCTION_BLOCKED_MISSING_ANCHOR, result.Status);
        Assert.False(result.IsBrokerAuthority);
        Assert.NotEmpty(result.Fills);
        Assert.Contains(result.BlockingReasons, x => x.Contains("prior position anchor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void M3H_complete_reconstruction_with_anchor_remains_internal_derived_not_broker_authority()
    {
        var state = StateWithFill();
        var venue = state.Venues.Single(x => x.Name == "LMAX");
        var account = state.BrokerAccounts.Single();
        var instrument = state.Instruments.Single(x => x.Symbol == "EURUSD");

        var result = InternalEodReconstructionBuilder.Build(ReportDate, venue, account, state.Instruments, state.InstrumentAliases, state.Fills, [Anchor(instrument.Id, 25000m)]);

        Assert.Equal(InternalEodReconstructionStatus.INTERNAL_EOD_RECONSTRUCTED_COMPLETE, result.Status);
        Assert.False(result.IsBrokerAuthority);
        Assert.False(result.CanProvideOpenOrderAuthority);
        var position = Assert.Single(result.DerivedPositions);
        Assert.Equal(15000m, position.DerivedEodBaseQuantity);
    }

    [Fact]
    public void M3H_missing_account_mapping_blocks_reconstruction()
    {
        var state = StateWithFill();
        var venue = state.Venues.Single(x => x.Name == "LMAX");

        var result = InternalEodReconstructionBuilder.Build(ReportDate, venue, null, state.Instruments, state.InstrumentAliases, state.Fills, []);

        Assert.Equal(InternalEodReconstructionStatus.INTERNAL_EOD_RECONSTRUCTION_BLOCKED_MISSING_ACCOUNT_MAPPING, result.Status);
        Assert.False(result.IsBrokerAuthority);
    }

    [Fact]
    public void M3H_missing_symbol_alias_blocks_reconstruction()
    {
        var state = StateWithFill();
        state.InstrumentAliases.RemoveAll(x => x.Source == "LMAX_REPORT");
        var venue = state.Venues.Single(x => x.Name == "LMAX");
        var account = state.BrokerAccounts.Single();

        var result = InternalEodReconstructionBuilder.Build(ReportDate, venue, account, state.Instruments, state.InstrumentAliases, state.Fills, []);

        Assert.Equal(InternalEodReconstructionStatus.INTERNAL_EOD_RECONSTRUCTION_BLOCKED_MISSING_SYMBOL_ALIAS, result.Status);
        Assert.Contains(result.BlockingReasons, x => x.Contains("LMAX_REPORT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void M3H_lmax_report_vs_internal_reconstruction_match_has_no_breaks()
    {
        var state = StateWithFill();
        var venue = state.Venues.Single(x => x.Name == "LMAX");
        var account = state.BrokerAccounts.Single();
        var instrument = state.Instruments.Single(x => x.Symbol == "EURUSD");
        var result = InternalEodReconstructionBuilder.Build(ReportDate, venue, account, state.Instruments, state.InstrumentAliases, state.Fills, [Anchor(instrument.Id, 0m)]);
        var lmax = LmaxTrade(state, "BRK-EXEC-M3H-1", 10000m, 1.10000m, totalCommission: 0m);

        var reconciliation = DualSourceEodReconciler.Compare(ReportDate, account, [lmax], result);

        Assert.Empty(reconciliation.Breaks);
    }

    [Fact]
    public void M3H_lmax_report_vs_internal_reconstruction_mismatch_returns_correct_break()
    {
        var state = StateWithFill();
        var venue = state.Venues.Single(x => x.Name == "LMAX");
        var account = state.BrokerAccounts.Single();
        var instrument = state.Instruments.Single(x => x.Symbol == "EURUSD");
        var result = InternalEodReconstructionBuilder.Build(ReportDate, venue, account, state.Instruments, state.InstrumentAliases, state.Fills, [Anchor(instrument.Id, 0m)]);
        var lmax = LmaxTrade(state, "BRK-EXEC-M3H-1", 10000m, 1.10010m, totalCommission: 0m);

        var reconciliation = DualSourceEodReconciler.Compare(ReportDate, account, [lmax], result);

        Assert.Contains(reconciliation.Breaks, x => x.BreakType == DualSourceEodBreakType.PRICE_MISMATCH);
    }

    [Fact]
    public void M3H_manual_and_reconstructed_sources_cannot_become_broker_position_authority()
    {
        Assert.False(BrokerAuthoritySourcePolicy.IsAcceptedQuality(BrokerAuthoritySourceRole.BrokerPositionSnapshot, BrokerSourceQuality.MANUAL_EVIDENCE));
        Assert.False(BrokerAuthoritySourcePolicy.IsAcceptedQuality(BrokerAuthoritySourceRole.BrokerPositionSnapshot, BrokerSourceQuality.RECONSTRUCTED));
        Assert.False(BrokerAuthoritySourcePolicy.IsAcceptedQuality(BrokerAuthoritySourceRole.BrokerOpenOrderSnapshot, BrokerSourceQuality.MANUAL_EVIDENCE));
        Assert.False(BrokerAuthoritySourcePolicy.IsAcceptedQuality(BrokerAuthoritySourceRole.BrokerOpenOrderSnapshot, BrokerSourceQuality.RECONSTRUCTED));
    }

    [Fact]
    public void M3H_future_acquisition_contract_has_no_runtime_implementation_or_order_entry_methods()
    {
        var serviceType = typeof(ILmaxEodReportAcquisitionService);
        var implementations = serviceType.Assembly.GetTypes()
            .Where(x => !x.IsInterface && !x.IsAbstract && serviceType.IsAssignableFrom(x))
            .ToList();

        Assert.Empty(implementations);
        Assert.All(serviceType.GetMethods(), method =>
        {
            Assert.DoesNotContain("Order", method.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Cancel", method.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Trade", method.Name, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains("NewOrderSingle 35=D", LmaxEodReportAcquisitionSafetyPolicy.DeniedOperations);
    }

    private static PlatformState StateWithFill()
    {
        var state = SeedData.Create(Now);
        var instrument = state.Instruments.Single(x => x.Symbol == "EURUSD");
        var venue = state.Venues.Single(x => x.Name == "LMAX");
        state.Fills.Add(new Fill(new FillId(Guid.Parse("99999999-8888-7777-6666-555555555555")), "BRK-EXEC-M3H-1", ChildOrderId.New(), instrument.Id, venue.Id, TradeSide.Sell, 10000m, 1m, 1.10000m, Now, Now));
        return state;
    }

    private static InternalEodPriorPositionAnchor Anchor(InstrumentId instrumentId, decimal baseQuantity)
        => new(instrumentId, baseQuantity, M3EodEvidenceSourceKind.LMAX_OFFICIAL_EOD_REPORT_FILE, "anchor-sha256", Now.AddDays(-1));

    private static LmaxIndividualTrade LmaxTrade(PlatformState state, string executionId, decimal unitsBoughtSoldAbs, decimal tradePrice, decimal totalCommission)
    {
        var venue = state.Venues.Single(x => x.Name == "LMAX");
        var account = state.BrokerAccounts.Single();
        var instrument = state.Instruments.Single(x => x.Symbol == "EURUSD");
        return new LmaxIndividualTrade(
            LmaxIndividualTradeId.New(),
            LmaxReportImportRunId.New(),
            ReportDate,
            venue.Id,
            account.Id,
            executionId,
            $"MTF-{executionId}",
            Now,
            -1m,
            tradePrice,
            ReportDate,
            "4001",
            "EUR/USD",
            instrument.Id,
            $"INST-{executionId}",
            $"ORD-{executionId}",
            null,
            null,
            Now,
            "Market",
            "LMAX",
            "LOCAL_TEST",
            0m,
            totalCommission,
            account.ExternalAccountId ?? account.AccountCode,
            -unitsBoughtSoldAbs,
            unitsBoughtSoldAbs * tradePrice,
            $"UTI-{executionId}",
            null,
            Now);
    }
}
