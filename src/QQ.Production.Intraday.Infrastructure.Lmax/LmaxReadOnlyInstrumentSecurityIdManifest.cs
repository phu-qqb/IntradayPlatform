namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed class LmaxReadOnlyInstrumentSecurityIdManifest
{
    public LmaxReadOnlyInstrumentSecurityIdManifest()
        : this(CreateDefaultSymbolToSecurityId(), CreateDefaultExternalRunApproval())
    {
    }

    public LmaxReadOnlyInstrumentSecurityIdManifest(
        Dictionary<string, string> symbolToSecurityId,
        Dictionary<string, bool> isApprovedForExternalRun)
    {
        SymbolToSecurityId = new Dictionary<string, string>(symbolToSecurityId, StringComparer.OrdinalIgnoreCase);
        IsApprovedForExternalRun = new Dictionary<string, bool>(isApprovedForExternalRun, StringComparer.OrdinalIgnoreCase);
    }

    public Dictionary<string, string> SymbolToSecurityId { get; }

    public Dictionary<string, bool> IsApprovedForExternalRun { get; }

    public string? GetConfirmedSecurityId(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        return SymbolToSecurityId.TryGetValue(symbol, out var securityId)
            ? securityId
            : null;
    }

    public bool AllInstrumentsConfirmed()
        => LmaxReadOnlyInstrumentAllowlist.CandidateEntries.All(entry =>
            SymbolToSecurityId.TryGetValue(entry.Symbol, out var securityId)
            && !string.IsNullOrWhiteSpace(securityId));

    public bool AllExternalRunsBlocked()
        => LmaxReadOnlyInstrumentAllowlist.CandidateEntries.All(entry =>
            IsApprovedForExternalRun.TryGetValue(entry.Symbol, out var approved)
            && approved == false);

    public static Dictionary<string, string> CreateDefaultSymbolToSecurityId()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["GBPUSD"] = "PHASE6C-DEMO-SECURITYID-GBPUSD",
            ["USDJPY"] = "PHASE6C-DEMO-SECURITYID-USDJPY",
            ["EURGBP"] = "PHASE6C-DEMO-SECURITYID-EURGBP",
            ["AUDUSD"] = "PHASE6C-DEMO-SECURITYID-AUDUSD"
        };

    public static Dictionary<string, bool> CreateDefaultExternalRunApproval()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["GBPUSD"] = false,
            ["USDJPY"] = false,
            ["EURGBP"] = false,
            ["AUDUSD"] = false
        };
}
