using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch6aQubesLmaxMappingTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void P01_versioned_mapping_validates_from_pinned_local_authorities()
    {
        var mapping = Load();
        var validation = Arch6aQubesLmaxMappingValidator.Validate(mapping);

        Assert.True(validation.IsValid, string.Join(";", validation.Issues));
        Assert.Equal(Arch6aQubesLmaxMappingContracts.MappingV1, mapping.ContractVersion);
        Assert.Equal("8cc46b113815eba940e27eda2fde04c530d5ea27986fbe15293a4dee224cec1c", mapping.MappingSha256);
    }

    [Fact]
    public void P02_direct_lmax_pair_is_bound_by_exact_id_and_orientation()
    {
        var entry = Load().Entries.Single(value => value.CanonicalPairOrSymbol == "AUDUSD");

        Assert.Equal(Arch6aQubesLmaxMappingContracts.Direct, entry.MappingMode);
        Assert.Equal("4007", entry.LmaxDirectInstrumentId);
        Assert.Equal("AUD/USD", entry.LmaxDirectInstrumentName);
        Assert.Equal(Arch6aQubesLmaxMappingContracts.DirectOrientation, entry.LmaxDirectOrientation);
    }

    [Fact]
    public void P03_inverse_lmax_pair_remains_a_single_direct_instrument()
    {
        var entry = Load().Entries.First(value => value.LmaxDirectOrientation == Arch6aQubesLmaxMappingContracts.InvertedOrientation);

        Assert.Equal(Arch6aQubesLmaxMappingContracts.Direct, entry.MappingMode);
        Assert.Equal(entry.QuoteCurrency + "/" + entry.BaseCurrency, entry.LmaxDirectInstrumentName);
        Assert.Null(entry.LmaxLeg1);
        Assert.Null(entry.LmaxLeg2);
    }

    [Fact]
    public void P04_missing_direct_cross_uses_two_observable_lmax_usd_legs()
    {
        var entry = Load().Entries.Single(value => value.CanonicalPairOrSymbol == "PLNHUF");

        Assert.Equal("278", entry.QubesSecurityId);
        Assert.Equal(Arch6aQubesLmaxMappingContracts.UsdLegReconstruction, entry.MappingMode);
        Assert.NotNull(entry.LmaxLeg1);
        Assert.NotNull(entry.LmaxLeg2);
        Assert.Contains("MID(", entry.ReconstructionFormula, StringComparison.Ordinal);
    }

    [Fact]
    public void P05_first_leg_inversion_is_explicit()
    {
        var entry = Load().Entries.First(value =>
            value.MappingMode == Arch6aQubesLmaxMappingContracts.UsdLegReconstruction &&
            value.LmaxLeg1!.Orientation == Arch6aQubesLmaxMappingContracts.InvertedToUsd);

        Assert.Equal($"USD/{entry.BaseCurrency}", entry.LmaxLeg1!.InstrumentName);
    }

    [Fact]
    public void P06_second_leg_inversion_is_explicit()
    {
        var entry = Load().Entries.First(value =>
            value.MappingMode == Arch6aQubesLmaxMappingContracts.UsdLegReconstruction &&
            value.LmaxLeg2!.Orientation == Arch6aQubesLmaxMappingContracts.InvertedToUsd);

        Assert.Equal($"USD/{entry.QuoteCurrency}", entry.LmaxLeg2!.InstrumentName);
    }

    [Fact]
    public void P07_economically_equivalent_source_orientations_project_the_same_quote()
    {
        var projector = new Arch6aLmaxUsdCrossRateProjector();
        var direct = projector.Project("EUR", "USD",
            [Quote("4001", "EUR", "USD", 1.10m, 1.11m)], TimeSpan.Zero);
        var inverse = projector.Project("EUR", "USD",
            [Quote("inverse", "USD", "EUR", 1m / 1.11m, 1m / 1.10m)], TimeSpan.Zero);

        Assert.InRange(Math.Abs(direct.Bid - inverse.Bid), 0m, 0.000000000000000000000000001m);
        Assert.InRange(Math.Abs(direct.Ask - inverse.Ask), 0m, 0.000000000000000000000000001m);
    }

    [Fact]
    public void P08_entry_order_does_not_change_mapping_sha()
    {
        var mapping = Load();
        var reordered = mapping with { Entries = mapping.Entries.Reverse().ToArray() };

        Assert.Equal(mapping.MappingSha256, Arch6aQubesLmaxMappingValidator.ComputeMappingSha256(reordered));
    }

    [Fact]
    public void P09_same_input_produces_same_mapping_and_subscription_hashes()
    {
        var mapping = Load();
        var first = Arch6aQubesLmaxMappingValidator.BuildSubscriptionPlan(mapping);
        var second = Arch6aQubesLmaxMappingValidator.BuildSubscriptionPlan(mapping);

        Assert.Equal(first.SubscriptionPlanSha256, second.SubscriptionPlanSha256);
        Assert.Equal("87871a026fe14e1325c8acf5ef8f21e7340a1fc5860eac9f1f153213aa41e313", first.SubscriptionPlanSha256);
    }

    [Fact]
    public void P10_all_required_occurrences_map_and_subscriptions_are_deduplicated()
    {
        var mapping = Load();
        var coverage = Arch6aQubesLmaxMappingValidator.BuildCoverage(mapping);
        var plan = Arch6aQubesLmaxMappingValidator.BuildSubscriptionPlan(mapping);

        Assert.True(coverage.FinalSuccess);
        Assert.Equal(mapping.RequiredSecurityIdOccurrences.Count, coverage.RequiredOccurrences);
        Assert.Equal(coverage.RequiredOccurrences, coverage.MappedOccurrences);
        Assert.Equal(coverage.RequiredUniqueSecurityIds, coverage.MappedUniqueSecurityIds);
        Assert.Empty(coverage.MissingSecurityIds);
        Assert.Empty(coverage.AmbiguousSecurityIds);
        Assert.Empty(coverage.DuplicateSecurityIds);
        Assert.Empty(coverage.UnavailableLmaxLegs);
        Assert.Equal(100m, coverage.CoveragePercent);
        Assert.Equal(49, coverage.DirectMappingCount);
        Assert.Equal(50, coverage.UsdLegReconstructionCount);
        Assert.Equal(149, plan.RequestedInstrumentReferenceCount);
        Assert.Equal(49, plan.UniqueSubscriptionCount);
        Assert.Equal(100, plan.DuplicateSubscriptionCount);
    }

    [Theory]
    [InlineData("security-id-absent", "MAPPING_COVERAGE_BELOW_100_PERCENT")]
    [InlineData("security-id-duplicated", "QUBES_SECURITY_ID_DUPLICATED")]
    [InlineData("symbol-absent", "CANONICAL_SYMBOL_ORIENTATION_INVALID")]
    [InlineData("pair-ambiguous", "CANONICAL_PAIR_AMBIGUOUS")]
    [InlineData("direct-instrument-absent", "LMAX_DIRECT_INSTRUMENT_MISSING")]
    [InlineData("usd-leg-absent", "LMAX_LEG_UNAVAILABLE")]
    [InlineData("orientation-unknown", "USD_LEG_ORIENTATION_UNKNOWN")]
    [InlineData("formula-unknown", "RECONSTRUCTION_FORMULA_UNKNOWN")]
    [InlineData("fuzzy-match", "FUZZY_OR_UNCONFIRMED_MAPPING")]
    [InlineData("non-lmax-source", "SOURCE_CONTRACT_MISSING")]
    [InlineData("manual-provenance", "MANUAL_OR_UNPROVEN_MAPPING")]
    [InlineData("recursive-reconstruction", "RECURSIVE_OR_MIXED_MAPPING_FORBIDDEN")]
    [InlineData("wrong-base-quote", "CANONICAL_SYMBOL_ORIENTATION_INVALID")]
    [InlineData("wrong-source-sha", "SOURCE_ARTIFACT_SHA256_MISMATCH")]
    [InlineData("unknown-version", "UNKNOWN_CONTRACT_VERSION")]
    public void N01_to_N15_mapping_fail_closed_matrix(string mutation, string expectedIssue)
    {
        var mapping = Mutate(Load(), mutation);
        var validation = Arch6aQubesLmaxMappingValidator.Validate(mapping);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, issue => issue.StartsWith(expectedIssue, StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => Arch6aQubesLmaxMappingValidator.BuildSubscriptionPlan(mapping));
    }

    [Theory]
    [InlineData("division-by-zero", "ARCH6A_LMAX_QUOTE_INVALID")]
    [InlineData("nan", "ARCH6A_LMAX_PRICE_NOT_FINITE_OR_POSITIVE")]
    [InlineData("infinity", "ARCH6A_LMAX_PRICE_NOT_FINITE_OR_POSITIVE")]
    [InlineData("timestamp-order", "ARCH6A_LMAX_QUOTE_TIMESTAMP_ORDER_INVALID")]
    [InlineData("stale", "ARCH6A_LMAX_QUOTE_STALE")]
    public void N16_to_N20_market_projection_fail_closed_matrix(string mutation, string expectedIssue)
    {
        Exception exception = mutation switch
        {
            "division-by-zero" => Assert.Throws<InvalidOperationException>(() =>
                new Arch6aLmaxUsdCrossRateProjector().Project(
                    "EUR", "USD", [Quote("4001", "EUR", "USD", 0m, 1m)], TimeSpan.Zero)),
            "nan" => Assert.Throws<InvalidDataException>(() =>
                Arch6aLmaxUsdCrossRateProjector.ParseObservedPrice("NaN")),
            "infinity" => Assert.Throws<InvalidDataException>(() =>
                Arch6aLmaxUsdCrossRateProjector.ParseObservedPrice("Infinity")),
            "timestamp-order" => Assert.Throws<InvalidOperationException>(() =>
                new Arch6aLmaxUsdCrossRateProjector().Project(
                    "EUR", "USD", [Quote("4001", "EUR", "USD", 1m, 2m) with { SourceTimestampUtc = Now.AddSeconds(1) }], TimeSpan.Zero)),
            "stale" => Assert.Throws<InvalidOperationException>(() =>
                new Arch6aLmaxUsdCrossRateProjector().Project(
                    "EUR", "USD", [Quote("4001", "EUR", "USD", 1m, 2m) with { ReceivedAtUtc = Now.AddSeconds(2) }],
                    TimeSpan.Zero, TimeSpan.FromSeconds(1))),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };

        Assert.StartsWith(expectedIssue, exception.Message, StringComparison.Ordinal);
    }

    private static QubesSecurityIdToLmaxMarketInstrumentMappingV1 Mutate(
        QubesSecurityIdToLmaxMarketInstrumentMappingV1 mapping,
        string mutation)
    {
        var entries = mapping.Entries.ToArray();
        var directIndex = Array.FindIndex(entries, value => value.MappingMode == Arch6aQubesLmaxMappingContracts.Direct);
        var crossIndex = Array.FindIndex(entries, value => value.MappingMode == Arch6aQubesLmaxMappingContracts.UsdLegReconstruction);
        var direct = entries[directIndex];
        var cross = entries[crossIndex];

        switch (mutation)
        {
            case "security-id-absent":
                entries = entries.Skip(1).ToArray();
                break;
            case "security-id-duplicated":
                entries = [.. entries, entries[0]];
                break;
            case "symbol-absent":
                entries[directIndex] = direct with { CanonicalPairOrSymbol = string.Empty };
                break;
            case "pair-ambiguous":
                entries[1] = entries[1] with
                {
                    CanonicalPairOrSymbol = entries[0].CanonicalPairOrSymbol,
                    BaseCurrency = entries[0].BaseCurrency,
                    QuoteCurrency = entries[0].QuoteCurrency
                };
                break;
            case "direct-instrument-absent":
                entries[directIndex] = direct with { LmaxDirectInstrumentId = null };
                break;
            case "usd-leg-absent":
                entries[crossIndex] = cross with { LmaxLeg1 = null };
                break;
            case "orientation-unknown":
                entries[crossIndex] = cross with { LmaxLeg1 = cross.LmaxLeg1! with { Orientation = "UNKNOWN" } };
                break;
            case "formula-unknown":
                entries[crossIndex] = cross with { ReconstructionFormula = "UNKNOWN" };
                break;
            case "fuzzy-match":
                entries[directIndex] = direct with { IdentityMatchMethod = "FUZZY" };
                break;
            case "manual-provenance":
                entries[directIndex] = direct with { AuthorityClassification = "MANUAL" };
                break;
            case "recursive-reconstruction":
                entries[directIndex] = direct with { LmaxLeg1 = cross.LmaxLeg1 };
                break;
            case "wrong-base-quote":
                entries[directIndex] = direct with { BaseCurrency = direct.QuoteCurrency, QuoteCurrency = direct.BaseCurrency };
                break;
            case "non-lmax-source":
            {
                var sources = mapping.Sources.Select(source =>
                    source.SourceContract == "LMAX_INSTRUMENT_REFERENCE_20260528"
                        ? source with { SourceContract = "THIRD_PARTY_VENDOR" }
                        : source).ToArray();
                return Rehash(mapping with { Sources = sources });
            }
            case "wrong-source-sha":
            {
                var sources = mapping.Sources.Select((source, index) =>
                    index == 0 ? source with { Sha256 = new string('0', 64) } : source).ToArray();
                return Rehash(mapping with { Sources = sources });
            }
            case "unknown-version":
                return Rehash(mapping with { ContractVersion = "unknown_v99" });
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        return Rehash(mapping with { Entries = entries });
    }

    private static QubesSecurityIdToLmaxMarketInstrumentMappingV1 Rehash(
        QubesSecurityIdToLmaxMarketInstrumentMappingV1 mapping)
        => mapping with { MappingSha256 = Arch6aQubesLmaxMappingValidator.ComputeMappingSha256(mapping) };

    private static Arch6aLmaxFxQuote Quote(
        string instrumentId,
        string baseCurrency,
        string quoteCurrency,
        decimal bid,
        decimal ask)
        => new(instrumentId, instrumentId, baseCurrency, quoteCurrency, bid, ask, Now, Now, new string('a', 64));

    private static QubesSecurityIdToLmaxMarketInstrumentMappingV1 Load()
        => Arch6aQubesLmaxMappingLoader.Load(Path.Combine(
            FindRepoRoot(),
            "deploy",
            "aws",
            "anubis-shadow",
            "config",
            "arch6a_qubes_security_id_to_lmax_market_instrument_mapping.v1.json"));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("QQ.Production.Intraday.sln not found.");
    }
}
