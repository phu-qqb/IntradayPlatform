namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

public sealed class LmaxConnectivityLabOptions
{
    public string InstrumentSymbol { get; init; } = "EURUSD";
    public string LmaxInstrumentId { get; init; } = "4001";
    public string LmaxSlashSymbol { get; init; } = "EUR/USD";
    public int MarketDepth { get; init; } = 1;
    public LmaxFixMarketDataRequestMode MarketDataRequestMode { get; init; } = LmaxFixMarketDataRequestMode.SnapshotPlusUpdates;
    public int MarketDataMaxWaitSeconds { get; init; } = 300;
    public int MarketDataMaxMessages { get; init; } = 100000;
    public LmaxFixMarketDataSymbolEncodingMode MarketDataSymbolEncodingMode { get; init; } = LmaxFixMarketDataSymbolEncodingMode.SecurityIdAndSymbolWithIdSource;
    public string? FixSecurityIdSource { get; init; } = "8";
    public bool ShowFixMessages { get; init; }
}
