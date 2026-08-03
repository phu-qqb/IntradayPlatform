namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public static class Arch7bStages
{
    public static IReadOnlyList<string> All { get; } =
    [
        "STATIC_AUTHORITY_VALIDATION", "CALENDAR_LOADED", "SLOT_SELECTED", "SLOT_LOCKED",
        "CORE_PREQUALIFICATION", "CLOCK_PREFLIGHT", "PORTAL_SESSION_PROVEN", "ONE_SHOT_IDENTITIES_CREATED",
        "RDS_READ_1", "ARM_IMPORT", "RDS_READ_2", "PRELOADED_LEASE_READY", "BRACKET_T0", "BRACKET_P1",
        "BRACKET_T1", "BRACKET_P2", "BRACKET_T2", "COMPLEMENTARY_REPORTS", "CORE_FAST_SEAL", "HANDOFF_V3",
        "POSITION_PACKAGE", "POSITION_READY", "POSITION_PLAN", "POSITION_APPLY", "RUNTIME_SELECTION",
        "POSITION_MARKET_DRAFT", "MARKET_PREARM", "CLOCK_CAPTURE_START", "MARKET_CAPTURE", "CLOCK_POST_CLOSE",
        "MARKET_FINALIZATION", "POSITION_MARKET_LINEAGE", "MARKET_READY_MARKER", "PMS_IMPORT",
        "ECONOMIC_REVISION", "REVISION_BINDING", "ARCH7A_QUALIFY_SHADOW", "REPORTING",
        "FINAL_WORKING_ORDER_PREFLIGHT", "TERMINAL_CLEANUP"
    ];

    public static int IndexOf(string stage)
    {
        for (var index = 0; index < All.Count; index++)
            if (All[index] == stage) return index;
        return -1;
    }
}

public sealed record Arch7bChronologyEdge(
    string From,
    string To,
    string RequiredEvidence,
    string? SloId,
    string BlockerCode,
    IReadOnlyList<string> CleanupRegistrations);

public sealed record Arch7bChronologyValidation(
    bool IsValid,
    int StageCount,
    int EdgeCount,
    IReadOnlyList<string> TopologicalOrder,
    IReadOnlyList<string> Blockers,
    int PreSlotCriticalPathSloSeconds,
    string EvidenceSha256);

public static class Arch7bCrossRepositoryChronology
{
    public static IReadOnlyList<Arch7bChronologyEdge> CreateDefault()
    {
        var stages = Arch7bStages.All;
        var sloByTarget = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["RDS_READ_1"] = "RDS_SECRET_CLIENT_DEADLINE_SECONDS",
            ["RDS_READ_2"] = "PINNED_POSTGRESQL_COLD_OPEN_SECONDS",
            ["BRACKET_T2"] = "BRACKET_MAXIMUM_SPAN_SECONDS",
            ["CORE_FAST_SEAL"] = "FAST_SEAL_FINAL_EVIDENCE_INDEX_SECONDS",
            ["POSITION_PACKAGE"] = "POSITION_PACKAGE_READY_SECONDS",
            ["POSITION_READY"] = "POSITION_READY_SECONDS",
            ["POSITION_PLAN"] = "POSITION_PLAN_SECONDS",
            ["POSITION_APPLY"] = "POSITION_APPLY_START_SECONDS"
        };
        var cleanupByTarget = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["CORE_PREQUALIFICATION"] = ["core-prequalification-process-env"],
            ["PORTAL_SESSION_PROVEN"] = ["portal-browser-context"],
            ["RDS_READ_1"] = ["secret-client", "secret-reference"],
            ["ARM_IMPORT"] = ["arm-import-child", "armed-state", "owner-lock"],
            ["PRELOADED_LEASE_READY"] = ["preloaded-lease-process", "lease-marker"],
            ["BRACKET_T0"] = ["bracket-downloader-process"],
            ["CORE_FAST_SEAL"] = ["fast-seal-process"],
            ["HANDOFF_V3"] = ["handoff-child"],
            ["POSITION_APPLY"] = ["position-importer-process"],
            ["MARKET_PREARM"] = ["market-data-recorder", "market-data-subscriptions"],
            ["PMS_IMPORT"] = ["pms-importer"],
            ["ARCH7A_QUALIFY_SHADOW"] = ["arch7a-child", "set-role-state"],
            ["REPORTING"] = ["reporting-process", "transient-output-roots"]
        };

        return Enumerable.Range(0, stages.Count - 1).Select(index =>
        {
            var target = stages[index + 1];
            return new Arch7bChronologyEdge(stages[index], target, $"{target}_EVIDENCE_SHA256",
                sloByTarget.GetValueOrDefault(target), $"ARCH7B_{target}_FAILED",
                cleanupByTarget.GetValueOrDefault(target) ?? []);
        }).ToArray();
    }

    public static Arch7bChronologyValidation Validate(IEnumerable<Arch7bChronologyEdge> source,
        Arch7bGlobalSloRegistry registry)
    {
        var edges = source.ToArray();
        var blockers = new List<string>();
        var known = Arch7bStages.All.ToHashSet(StringComparer.Ordinal);
        if (edges.Any(edge => !known.Contains(edge.From) || !known.Contains(edge.To)))
            blockers.Add(Arch7bBlockers.ChronologyUnknownStage);
        if (edges.Any(edge => string.IsNullOrWhiteSpace(edge.RequiredEvidence)))
            blockers.Add(Arch7bBlockers.ChronologyEvidenceMissing);
        if (edges.SelectMany(edge => new[] { edge.From, edge.To }).Where(known.Contains)
            .ToHashSet(StringComparer.Ordinal).Count != known.Count)
            blockers.Add(Arch7bBlockers.ChronologyEvidenceMissing);
        foreach (var edge in edges.Where(edge => edge.SloId is not null))
        {
            if (!registry.Entries.Any(value => value.SloId == edge.SloId))
                blockers.Add(Arch7bBlockers.CriticalPathSloMissing);
        }

        var order = TopologicalOrder(edges, known, blockers);
        RequireBefore(order, "RDS_READ_2", "BRACKET_T0", Arch7bBlockers.RdsRead2AfterBracket, blockers);
        RequireBefore(order, "REVISION_BINDING", "ARCH7A_QUALIFY_SHADOW", Arch7bBlockers.Arch7aBeforeRevisionBinding, blockers);
        RequireBefore(order, "SLOT_LOCKED", "ONE_SHOT_IDENTITIES_CREATED", "ARCH7B_IDENTITY_CREATED_BEFORE_SLOT_LOCK", blockers);
        RequireBefore(order, "POSITION_APPLY", "RUNTIME_SELECTION", "ARCH7B_RUNTIME_SELECTION_BEFORE_POSITION_APPLY", blockers);
        RequireBefore(order, "RUNTIME_SELECTION", "POSITION_MARKET_DRAFT", "ARCH7B_POSITION_MARKET_DRAFT_BEFORE_RUNTIME_SELECTION", blockers);
        RequireBefore(order, "POSITION_MARKET_DRAFT", "MARKET_PREARM", "ARCH7B_MARKET_PREARM_BEFORE_DRAFT", blockers);
        RequireBefore(order, "POSITION_MARKET_LINEAGE", "PMS_IMPORT", "ARCH7B_PMS_BEFORE_POSITION_MARKET_LINEAGE", blockers);
        RequireBefore(order, "ARCH7A_QUALIFY_SHADOW", "REPORTING", "ARCH7B_REPORTING_BEFORE_ARCH7A", blockers);
        RequireBefore(order, "REPORTING", "FINAL_WORKING_ORDER_PREFLIGHT", "ARCH7B_PREFLIGHT_BEFORE_REPORTING", blockers);

        var criticalPath = blockers.Contains(Arch7bBlockers.ChronologyCycle, StringComparer.Ordinal) ? 0 :
            CalculateCriticalPathSeconds(edges, registry, "MARKET_PREARM");
        var canonical = string.Join('\n', edges.Select(edge => string.Join('|', edge.From, edge.To,
            edge.RequiredEvidence, edge.SloId ?? string.Empty, edge.BlockerCode,
            string.Join(',', edge.CleanupRegistrations))));
        return new(blockers.Count == 0, known.Count, edges.Length, order,
            blockers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), criticalPath,
            Arch7bOneShotContracts.Sha256(canonical));
    }

    public static void ValidatePrearm(DateTimeOffset observedUtc, DateTimeOffset slotStartUtc)
    {
        if (observedUtc >= slotStartUtc)
            throw new Arch7bQualificationException(Arch7bBlockers.MarketPrearmAfterSlotStart);
    }

    private static IReadOnlyList<string> TopologicalOrder(IReadOnlyList<Arch7bChronologyEdge> edges,
        IReadOnlySet<string> known, ICollection<string> blockers)
    {
        var usable = edges.Where(edge => known.Contains(edge.From) && known.Contains(edge.To)).ToArray();
        var incoming = known.ToDictionary(stage => stage, _ => 0, StringComparer.Ordinal);
        foreach (var edge in usable) incoming[edge.To]++;
        var queue = new PriorityQueue<string, int>();
        foreach (var stage in known.Where(stage => incoming[stage] == 0))
            queue.Enqueue(stage, Arch7bStages.IndexOf(stage));
        var order = new List<string>();
        while (queue.TryDequeue(out var stage, out _))
        {
            order.Add(stage);
            foreach (var edge in usable.Where(edge => edge.From == stage))
            {
                incoming[edge.To]--;
                if (incoming[edge.To] == 0) queue.Enqueue(edge.To, Arch7bStages.IndexOf(edge.To));
            }
        }
        if (order.Count != known.Count) blockers.Add(Arch7bBlockers.ChronologyCycle);
        return order;
    }

    private static int CalculateCriticalPathSeconds(IReadOnlyList<Arch7bChronologyEdge> edges,
        Arch7bGlobalSloRegistry registry, string terminal)
    {
        var values = Arch7bStages.All.ToDictionary(stage => stage, _ => 0, StringComparer.Ordinal);
        foreach (var stage in Arch7bStages.All)
        {
            foreach (var edge in edges.Where(edge => edge.To == stage && values.ContainsKey(edge.From)))
            {
                var seconds = edge.SloId is null ? 0 : ToSeconds(registry.Required(edge.SloId));
                values[stage] = Math.Max(values[stage], checked(values[edge.From] + seconds));
            }
            if (stage == terminal) break;
        }
        return values[terminal];
    }

    private static int ToSeconds(Arch7bSloDefinition value) => value.Unit switch
    {
        "seconds" => decimal.ToInt32(decimal.Ceiling(value.Threshold)),
        "milliseconds" => decimal.ToInt32(decimal.Ceiling(value.Threshold / 1000m)),
        "minutes" => decimal.ToInt32(decimal.Ceiling(value.Threshold * 60m)),
        _ => throw new Arch7bQualificationException(Arch7bBlockers.CriticalPathSloMissing, value.SloId)
    };

    private static void RequireBefore(IReadOnlyList<string> order, string before, string after,
        string blocker, ICollection<string> blockers)
    {
        var first = order.ToList().IndexOf(before);
        var second = order.ToList().IndexOf(after);
        if (first < 0 || second < 0 || first >= second) blockers.Add(blocker);
    }
}
