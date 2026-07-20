using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public static class Arch6aQubesLmaxMappingContracts
{
    public const string MappingV1 = "qubes_security_id_to_lmax_market_instrument_mapping_v1";
    public const string CoverageV1 = "qubes_to_lmax_mapping_coverage_v1";
    public const string SubscriptionPlanV1 = "lmax_market_data_subscription_plan_v1";
    public const string Direct = "DIRECT_LMAX_INSTRUMENT";
    public const string UsdLegReconstruction = "LMAX_USD_LEG_RECONSTRUCTION";
    public const string DirectOrientation = "DIRECT_BASE_QUOTE";
    public const string InvertedOrientation = "INVERTED_QUOTE_BASE";
    public const string DirectToUsd = "DIRECT_TO_USD";
    public const string InvertedToUsd = "INVERTED_TO_USD";
    public const string ExactIdentityMatch = "EXACT_SECURITY_ID_AND_CANONICAL_SYMBOL";
    public const string Authority = "QUBES_IDENTITY_AND_LMAX_REFERENCE_CROSS_VALIDATED";
    public const string Valid = "VALID";

    public static readonly IReadOnlyDictionary<string, string> PinnedSourceHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ARCH5D1V_FRESH_LINEAGE_MANIFEST"] = "91e5cd0de456f8849382b447b98c9b3bd243d3be51a15b7fbdb97c75001a43ce",
            ["QUBES_STATIC_MAPPING"] = "0b251b48e47875ad33431fc8b9b4609c6acf7f82a25bb99decfcf41b0720ebe6",
            ["QUBES_CORE_SECURITY_EXPORT"] = "7ea8acd27f38aa7c79b775f117d40ae18dfbc1ca41457647d93f031297bbb04b",
            ["LMAX_INSTRUMENT_REFERENCE_20260528"] = "7ff5889abb8b1712c1651dd27a932f4924388fc8675f375978515bd4d1e1df01"
        };
}

public sealed record Arch6aMappingSourceV1(
    string SourceContract,
    string LogicalName,
    string Sha256,
    string AuthorityClassification);

public sealed record Arch6aLmaxMappingLegV1(
    string InstrumentId,
    string InstrumentName,
    string Orientation);

public sealed record QubesSecurityIdToLmaxMarketInstrumentMappingEntryV1(
    string QubesSecurityId,
    string QubesInstrumentKey,
    string CanonicalPairOrSymbol,
    string QuoteCurrency,
    string BaseCurrency,
    string MappingMode,
    string? LmaxDirectInstrumentId,
    string? LmaxDirectInstrumentName,
    string? LmaxDirectOrientation,
    [property: JsonPropertyName("lmax_leg_1")] Arch6aLmaxMappingLegV1? LmaxLeg1,
    [property: JsonPropertyName("lmax_leg_2")] Arch6aLmaxMappingLegV1? LmaxLeg2,
    string? ReconstructionFormula,
    IReadOnlyList<string> SourceContracts,
    IReadOnlyList<string> SourceArtifactSha256,
    string IdentityMatchMethod,
    string AuthorityClassification,
    string ValidationStatus);

public sealed record QubesSecurityIdToLmaxMarketInstrumentMappingV1(
    string ContractVersion,
    string MappingSha256,
    IReadOnlyList<string> RequiredSecurityIdOccurrences,
    IReadOnlyList<Arch6aMappingSourceV1> Sources,
    IReadOnlyList<QubesSecurityIdToLmaxMarketInstrumentMappingEntryV1> Entries);

public sealed record Arch6aQubesLmaxMappingValidation(
    bool IsValid,
    IReadOnlyList<string> Issues);

public sealed record Arch6aQubesLmaxMappingCoverageV1(
    string ContractVersion,
    int RequiredOccurrences,
    int RequiredUniqueSecurityIds,
    int DirectMappingCount,
    int UsdLegReconstructionCount,
    int MappedOccurrences,
    int MappedUniqueSecurityIds,
    IReadOnlyList<string> MissingSecurityIds,
    IReadOnlyList<string> AmbiguousSecurityIds,
    IReadOnlyList<string> DuplicateSecurityIds,
    IReadOnlyList<string> UnavailableLmaxLegs,
    decimal CoveragePercent,
    bool FinalSuccess);

public sealed record Arch6aLmaxSubscriptionV1(
    string InstrumentId,
    string InstrumentName,
    IReadOnlyList<string> RequiredByQubesSecurityIds);

public sealed record Arch6aLmaxSubscriptionPlanV1(
    string ContractVersion,
    string MappingSha256,
    string SubscriptionPlanSha256,
    int RequestedInstrumentReferenceCount,
    int UniqueSubscriptionCount,
    int DuplicateSubscriptionCount,
    IReadOnlyList<Arch6aLmaxSubscriptionV1> Subscriptions);

public static class Arch6aQubesLmaxMappingLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static QubesSecurityIdToLmaxMarketInstrumentMappingV1 Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("ARCH6A_QUBES_LMAX_MAPPING_FILE_NOT_FOUND", path);
        }

        return JsonSerializer.Deserialize<QubesSecurityIdToLmaxMarketInstrumentMappingV1>(
                   File.ReadAllText(path),
                   JsonOptions)
               ?? throw new InvalidDataException("ARCH6A_QUBES_LMAX_MAPPING_DESERIALIZATION_FAILED");
    }
}

public static class Arch6aQubesLmaxMappingValidator
{
    public static Arch6aQubesLmaxMappingValidation Validate(
        QubesSecurityIdToLmaxMarketInstrumentMappingV1 mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        var issues = new List<string>();

        Require(mapping.ContractVersion == Arch6aQubesLmaxMappingContracts.MappingV1, "UNKNOWN_CONTRACT_VERSION", issues);
        Require(mapping.MappingSha256 == ComputeMappingSha256(mapping), "MAPPING_SHA256_MISMATCH", issues);
        ValidateSources(mapping.Sources, issues);

        var required = mapping.RequiredSecurityIdOccurrences
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        Require(required.Length == mapping.RequiredSecurityIdOccurrences.Count && required.Length > 0, "REQUIRED_SECURITY_ID_ABSENT", issues);

        var duplicateSecurityIds = mapping.Entries
            .GroupBy(value => value.QubesSecurityId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBySecurityId()
            .ToArray();
        Require(duplicateSecurityIds.Length == 0, "QUBES_SECURITY_ID_DUPLICATED", issues);
        Require(
            mapping.Entries.GroupBy(value => value.CanonicalPairOrSymbol, StringComparer.Ordinal)
                .All(group => group.Count() == 1),
            "CANONICAL_PAIR_AMBIGUOUS",
            issues);

        foreach (var entry in mapping.Entries)
        {
            ValidateEntry(entry, mapping.Sources, issues);
        }

        var mappedIds = mapping.Entries.Select(value => value.QubesSecurityId).ToHashSet(StringComparer.Ordinal);
        var requiredIds = required.ToHashSet(StringComparer.Ordinal);
        Require(requiredIds.SetEquals(mappedIds), "MAPPING_COVERAGE_BELOW_100_PERCENT", issues);

        return new(issues.Count == 0, issues.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    public static Arch6aQubesLmaxMappingCoverageV1 BuildCoverage(
        QubesSecurityIdToLmaxMarketInstrumentMappingV1 mapping)
    {
        var validation = Validate(mapping);
        var required = mapping.RequiredSecurityIdOccurrences;
        var requiredIds = required.Distinct(StringComparer.Ordinal).OrderBySecurityId().ToArray();
        var duplicateIds = mapping.Entries.GroupBy(value => value.QubesSecurityId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).OrderBySecurityId().ToArray();
        var uniqueEntries = mapping.Entries.GroupBy(value => value.QubesSecurityId, StringComparer.Ordinal)
            .Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var missing = requiredIds.Where(id => !uniqueEntries.ContainsKey(id)).ToArray();
        var ambiguous = validation.Issues.Any(issue => issue.Contains("AMBIGUOUS", StringComparison.Ordinal))
            ? requiredIds.Where(id => mapping.Entries.Count(entry => entry.QubesSecurityId == id) != 1).ToArray()
            : [];
        var unavailable = validation.Issues.Where(issue => issue.StartsWith("LMAX_LEG_UNAVAILABLE:", StringComparison.Ordinal))
            .Select(issue => issue["LMAX_LEG_UNAVAILABLE:".Length..]).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var mappedOccurrences = required.Count(id => uniqueEntries.ContainsKey(id));
        var coverage = required.Count == 0 ? 0m : decimal.Round(100m * mappedOccurrences / required.Count, 8);

        return new(
            Arch6aQubesLmaxMappingContracts.CoverageV1,
            required.Count,
            requiredIds.Length,
            uniqueEntries.Values.Count(value => value.MappingMode == Arch6aQubesLmaxMappingContracts.Direct),
            uniqueEntries.Values.Count(value => value.MappingMode == Arch6aQubesLmaxMappingContracts.UsdLegReconstruction),
            mappedOccurrences,
            requiredIds.Length - missing.Length,
            missing,
            ambiguous,
            duplicateIds,
            unavailable,
            coverage,
            validation.IsValid && coverage == 100m && missing.Length == 0 && duplicateIds.Length == 0 && unavailable.Length == 0);
    }

    public static Arch6aLmaxSubscriptionPlanV1 BuildSubscriptionPlan(
        QubesSecurityIdToLmaxMarketInstrumentMappingV1 mapping)
    {
        var validation = Validate(mapping);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join(";", validation.Issues));
        }

        var references = mapping.Entries.SelectMany(entry => InstrumentReferences(entry)
            .Select(reference => new { Entry = entry, reference.Id, reference.Name })).ToArray();
        var conflictingIds = references.GroupBy(value => value.Id, StringComparer.Ordinal)
            .Where(group => group.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() != 1)
            .Select(group => group.Key).ToArray();
        if (conflictingIds.Length > 0)
        {
            throw new InvalidDataException("ARCH6A_LMAX_INSTRUMENT_IDENTITY_AMBIGUOUS:" + string.Join(",", conflictingIds));
        }

        var subscriptions = references
            .GroupBy(value => value.Id, StringComparer.Ordinal)
            .Select(group => new Arch6aLmaxSubscriptionV1(
                group.Key,
                group.First().Name,
                group.Select(value => value.Entry.QubesSecurityId).Distinct(StringComparer.Ordinal).OrderBySecurityId().ToArray()))
            .OrderBy(value => ParseSecurityId(value.InstrumentId))
            .ThenBy(value => value.InstrumentId, StringComparer.Ordinal)
            .ToArray();
        var draft = new Arch6aLmaxSubscriptionPlanV1(
            Arch6aQubesLmaxMappingContracts.SubscriptionPlanV1,
            mapping.MappingSha256,
            string.Empty,
            references.Length,
            subscriptions.Length,
            references.Length - subscriptions.Length,
            subscriptions);
        return draft with { SubscriptionPlanSha256 = ComputeSubscriptionPlanSha256(draft) };
    }

    public static string ComputeMappingSha256(QubesSecurityIdToLmaxMarketInstrumentMappingV1 mapping)
    {
        var lines = new List<string> { mapping.ContractVersion };
        lines.AddRange(mapping.Sources.OrderBy(value => value.SourceContract, StringComparer.Ordinal)
            .Select(value => Join(value.SourceContract, value.LogicalName, value.Sha256, value.AuthorityClassification)));
        lines.AddRange(mapping.RequiredSecurityIdOccurrences.OrderBySecurityId().Select(value => "O|" + value));
        foreach (var entry in mapping.Entries.OrderBy(value => ParseSecurityId(value.QubesSecurityId)).ThenBy(value => value.QubesSecurityId, StringComparer.Ordinal))
        {
            lines.Add(Join(
                "E",
                entry.QubesSecurityId,
                entry.QubesInstrumentKey,
                entry.CanonicalPairOrSymbol,
                entry.BaseCurrency,
                entry.QuoteCurrency,
                entry.MappingMode,
                entry.LmaxDirectInstrumentId,
                entry.LmaxDirectInstrumentName,
                entry.LmaxDirectOrientation,
                Leg(entry.LmaxLeg1),
                Leg(entry.LmaxLeg2),
                entry.ReconstructionFormula,
                string.Join(",", entry.SourceContracts.OrderBy(value => value, StringComparer.Ordinal)),
                string.Join(",", entry.SourceArtifactSha256.OrderBy(value => value, StringComparer.Ordinal)),
                entry.IdentityMatchMethod,
                entry.AuthorityClassification,
                entry.ValidationStatus));
        }

        return Arch5bHashing.Sha256Hex(string.Join("\n", lines));
    }

    public static string ComputeSubscriptionPlanSha256(Arch6aLmaxSubscriptionPlanV1 plan)
    {
        var lines = new List<string>
        {
            plan.ContractVersion,
            plan.MappingSha256,
            plan.RequestedInstrumentReferenceCount.ToString(CultureInfo.InvariantCulture),
            plan.UniqueSubscriptionCount.ToString(CultureInfo.InvariantCulture),
            plan.DuplicateSubscriptionCount.ToString(CultureInfo.InvariantCulture)
        };
        lines.AddRange(plan.Subscriptions.Select(value => Join(
            value.InstrumentId,
            value.InstrumentName,
            string.Join(",", value.RequiredByQubesSecurityIds.OrderBySecurityId()))));
        return Arch5bHashing.Sha256Hex(string.Join("\n", lines));
    }

    private static void ValidateSources(IReadOnlyList<Arch6aMappingSourceV1> sources, ICollection<string> issues)
    {
        Require(sources.Count == Arch6aQubesLmaxMappingContracts.PinnedSourceHashes.Count, "SOURCE_CONTRACT_SET_INCOMPLETE", issues);
        Require(sources.Select(value => value.SourceContract).Distinct(StringComparer.Ordinal).Count() == sources.Count, "SOURCE_CONTRACT_DUPLICATED", issues);
        foreach (var expected in Arch6aQubesLmaxMappingContracts.PinnedSourceHashes)
        {
            var source = sources.SingleOrDefault(value => value.SourceContract == expected.Key);
            Require(source is not null, $"SOURCE_CONTRACT_MISSING:{expected.Key}", issues);
            if (source is null) continue;
            Require(source.Sha256 == expected.Value, $"SOURCE_ARTIFACT_SHA256_MISMATCH:{expected.Key}", issues);
            Require(!string.IsNullOrWhiteSpace(source.LogicalName) && !Path.IsPathRooted(source.LogicalName), $"SOURCE_LOGICAL_NAME_INVALID:{expected.Key}", issues);
            Require(!string.IsNullOrWhiteSpace(source.AuthorityClassification), $"SOURCE_AUTHORITY_MISSING:{expected.Key}", issues);
        }
    }

    private static void ValidateEntry(
        QubesSecurityIdToLmaxMarketInstrumentMappingEntryV1 entry,
        IReadOnlyList<Arch6aMappingSourceV1> sources,
        ICollection<string> issues)
    {
        var id = entry.QubesSecurityId;
        Require(ParseSecurityId(id) != int.MaxValue, $"QUBES_SECURITY_ID_INVALID:{id}", issues);
        Require(entry.QubesInstrumentKey == $"QUBES_SECURITY_ID:{id}", $"QUBES_INSTRUMENT_KEY_INVALID:{id}", issues);
        Require(IsCurrency(entry.BaseCurrency) && IsCurrency(entry.QuoteCurrency) && entry.BaseCurrency != entry.QuoteCurrency, $"CURRENCY_PAIR_INVALID:{id}", issues);
        Require(entry.CanonicalPairOrSymbol == entry.BaseCurrency + entry.QuoteCurrency, $"CANONICAL_SYMBOL_ORIENTATION_INVALID:{id}", issues);
        Require(entry.IdentityMatchMethod == Arch6aQubesLmaxMappingContracts.ExactIdentityMatch, $"FUZZY_OR_UNCONFIRMED_MAPPING:{id}", issues);
        Require(entry.AuthorityClassification == Arch6aQubesLmaxMappingContracts.Authority, $"MANUAL_OR_UNPROVEN_MAPPING:{id}", issues);
        Require(entry.ValidationStatus == Arch6aQubesLmaxMappingContracts.Valid, $"MAPPING_STATUS_INVALID:{id}", issues);

        var sourceContracts = entry.SourceContracts.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var declaredContracts = sources.Select(value => value.SourceContract).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        Require(sourceContracts.SequenceEqual(declaredContracts, StringComparer.Ordinal), $"ENTRY_SOURCE_CONTRACTS_INCOMPLETE:{id}", issues);
        var hashes = entry.SourceArtifactSha256.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var declaredHashes = sources.Select(value => value.Sha256).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        Require(hashes.SequenceEqual(declaredHashes, StringComparer.Ordinal), $"ENTRY_SOURCE_ARTIFACT_SHA256_MISMATCH:{id}", issues);

        if (entry.MappingMode == Arch6aQubesLmaxMappingContracts.Direct)
        {
            ValidateDirect(entry, issues);
        }
        else if (entry.MappingMode == Arch6aQubesLmaxMappingContracts.UsdLegReconstruction)
        {
            ValidateReconstruction(entry, issues);
        }
        else
        {
            issues.Add($"MAPPING_MODE_UNKNOWN:{id}");
        }
    }

    private static void ValidateDirect(
        QubesSecurityIdToLmaxMarketInstrumentMappingEntryV1 entry,
        ICollection<string> issues)
    {
        var id = entry.QubesSecurityId;
        Require(!string.IsNullOrWhiteSpace(entry.LmaxDirectInstrumentId) && !string.IsNullOrWhiteSpace(entry.LmaxDirectInstrumentName), $"LMAX_DIRECT_INSTRUMENT_MISSING:{id}", issues);
        Require(entry.LmaxLeg1 is null && entry.LmaxLeg2 is null && entry.ReconstructionFormula is null, $"RECURSIVE_OR_MIXED_MAPPING_FORBIDDEN:{id}", issues);
        var expectedDirectName = entry.BaseCurrency + "/" + entry.QuoteCurrency;
        var expectedInverseName = entry.QuoteCurrency + "/" + entry.BaseCurrency;
        if (entry.LmaxDirectOrientation == Arch6aQubesLmaxMappingContracts.DirectOrientation)
        {
            Require(entry.LmaxDirectInstrumentName == expectedDirectName, $"DIRECT_BASE_QUOTE_ORIENTATION_INVALID:{id}", issues);
        }
        else if (entry.LmaxDirectOrientation == Arch6aQubesLmaxMappingContracts.InvertedOrientation)
        {
            Require(entry.LmaxDirectInstrumentName == expectedInverseName, $"DIRECT_INVERSE_ORIENTATION_INVALID:{id}", issues);
        }
        else
        {
            issues.Add($"DIRECT_ORIENTATION_UNKNOWN:{id}");
        }
    }

    private static void ValidateReconstruction(
        QubesSecurityIdToLmaxMarketInstrumentMappingEntryV1 entry,
        ICollection<string> issues)
    {
        var id = entry.QubesSecurityId;
        Require(entry.BaseCurrency != "USD" && entry.QuoteCurrency != "USD", $"RECONSTRUCTION_FOR_USD_PAIR_FORBIDDEN:{id}", issues);
        Require(entry.LmaxDirectInstrumentId is null && entry.LmaxDirectInstrumentName is null && entry.LmaxDirectOrientation is null, $"RECURSIVE_OR_MIXED_MAPPING_FORBIDDEN:{id}", issues);
        if (entry.LmaxLeg1 is null)
        {
            issues.Add($"LMAX_LEG_UNAVAILABLE:{id}:LEG1");
        }
        if (entry.LmaxLeg2 is null)
        {
            issues.Add($"LMAX_LEG_UNAVAILABLE:{id}:LEG2");
        }
        if (entry.LmaxLeg1 is null || entry.LmaxLeg2 is null) return;

        var leg1Expression = ValidateUsdLeg(entry.LmaxLeg1, entry.BaseCurrency, id, "LEG1", issues);
        var leg2Expression = ValidateUsdLeg(entry.LmaxLeg2, entry.QuoteCurrency, id, "LEG2", issues);
        Require(entry.LmaxLeg1.InstrumentId != entry.LmaxLeg2.InstrumentId, $"RECONSTRUCTION_LOOP_OR_DUPLICATE_LEG:{id}", issues);
        Require(entry.ReconstructionFormula == $"({leg1Expression}) / ({leg2Expression})", $"RECONSTRUCTION_FORMULA_UNKNOWN:{id}", issues);
    }

    private static string ValidateUsdLeg(
        Arch6aLmaxMappingLegV1 leg,
        string currency,
        string id,
        string label,
        ICollection<string> issues)
    {
        Require(!string.IsNullOrWhiteSpace(leg.InstrumentId) && !string.IsNullOrWhiteSpace(leg.InstrumentName), $"LMAX_LEG_UNAVAILABLE:{id}:{label}", issues);
        if (leg.Orientation == Arch6aQubesLmaxMappingContracts.DirectToUsd)
        {
            Require(leg.InstrumentName == $"{currency}/USD", $"USD_LEG_ORIENTATION_INVALID:{id}:{label}", issues);
            return $"MID({currency}/USD)";
        }
        if (leg.Orientation == Arch6aQubesLmaxMappingContracts.InvertedToUsd)
        {
            Require(leg.InstrumentName == $"USD/{currency}", $"USD_LEG_ORIENTATION_INVALID:{id}:{label}", issues);
            return $"1 / MID(USD/{currency})";
        }

        issues.Add($"USD_LEG_ORIENTATION_UNKNOWN:{id}:{label}");
        return "UNKNOWN";
    }

    private static IEnumerable<(string Id, string Name)> InstrumentReferences(
        QubesSecurityIdToLmaxMarketInstrumentMappingEntryV1 entry)
    {
        if (entry.MappingMode == Arch6aQubesLmaxMappingContracts.Direct)
        {
            yield return (entry.LmaxDirectInstrumentId!, entry.LmaxDirectInstrumentName!);
            yield break;
        }

        yield return (entry.LmaxLeg1!.InstrumentId, entry.LmaxLeg1.InstrumentName);
        yield return (entry.LmaxLeg2!.InstrumentId, entry.LmaxLeg2.InstrumentName);
    }

    private static string Join(params string?[] values)
        => string.Join("|", values.Select(value => value ?? "-"));

    private static string Leg(Arch6aLmaxMappingLegV1? leg)
        => leg is null ? "-" : Join(leg.InstrumentId, leg.InstrumentName, leg.Orientation);

    private static bool IsCurrency(string value)
        => value.Length == 3 && value.All(character => character is >= 'A' and <= 'Z');

    private static int ParseSecurityId(string value)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id
            : int.MaxValue;

    private static void Require(bool condition, string issue, ICollection<string> issues)
    {
        if (!condition) issues.Add(issue);
    }

    private static IOrderedEnumerable<string> OrderBySecurityId(this IEnumerable<string> values)
        => values.OrderBy(ParseSecurityId).ThenBy(value => value, StringComparer.Ordinal);
}
