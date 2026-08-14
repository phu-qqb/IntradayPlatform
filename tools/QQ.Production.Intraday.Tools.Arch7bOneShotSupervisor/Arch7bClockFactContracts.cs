namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bClockFactContract(
    string ProducerStage,
    string ConsumerStage,
    string FactType,
    string LegacyAlias,
    string FileName);

public static class Arch7bClockFactContracts
{
    public const string PreflightFactType = "clock_authority_preflight_snapshot";
    public const string CaptureStartFactType = "clock_authority_capture_snapshot";
    public const string PostCloseFactType = "clock_authority_post_close_snapshot";

    public const string LegacyPreflightFactType = "clock_preflight_evidence";
    public const string LegacyCaptureStartFactType = "clock_capture_start_evidence";
    public const string LegacyPostCloseFactType = "clock_post_close_evidence";

    public static IReadOnlyList<Arch7bClockFactContract> All { get; } =
    [
        new("CLOCK_PREFLIGHT", "PORTAL_SESSION_PROVEN", PreflightFactType,
            LegacyPreflightFactType, "clock_authority_preflight.json"),
        new("CLOCK_CAPTURE_START", "MARKET_CAPTURE", CaptureStartFactType,
            LegacyCaptureStartFactType, "clock_authority_capture.json"),
        new("CLOCK_POST_CLOSE", "MARKET_FINALIZATION", PostCloseFactType,
            LegacyPostCloseFactType, "clock_authority_post_close.json")
    ];

    public static IReadOnlySet<string> LegacyAliases { get; } = All
        .Select(value => value.LegacyAlias)
        .ToHashSet(StringComparer.Ordinal);

    public static Arch7bClockFactContract RequireProducer(string stageId) =>
        All.SingleOrDefault(value => value.ProducerStage == stageId)
        ?? throw new Arch7bQualificationException(
            Arch7bV2Blockers.CommandTemplateInvalid, stageId);

    public static IReadOnlyList<string> NormalizeRequiredFacts(
        string stageId,
        IEnumerable<string> source)
    {
        var required = new List<string>();
        foreach (var factType in source)
        {
            var legacy = All.SingleOrDefault(value => value.LegacyAlias == factType);
            if (legacy is not null && legacy.ConsumerStage == stageId)
                AddDistinct(required, legacy.FactType);
            else
                AddDistinct(required, factType);
        }

        foreach (var contract in All.Where(value => value.ConsumerStage == stageId))
            AddDistinct(required, contract.FactType);
        return required;
    }

    public static IReadOnlyList<string> NormalizeProducedFacts(
        string stageId,
        IEnumerable<string> source)
    {
        var produced = new List<string>();
        foreach (var factType in source)
        {
            var legacy = All.SingleOrDefault(value => value.LegacyAlias == factType);
            if (legacy is not null && legacy.ProducerStage == stageId)
                AddDistinct(produced, legacy.FactType);
            else
                AddDistinct(produced, factType);
        }

        foreach (var contract in All.Where(value => value.ProducerStage == stageId))
            AddDistinct(produced, contract.FactType);
        return produced;
    }

    private static void AddDistinct(ICollection<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.Ordinal)) values.Add(value);
    }
}
