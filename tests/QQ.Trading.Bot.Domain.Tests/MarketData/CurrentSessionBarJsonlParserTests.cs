using System.Text.Json;
using QQ.Trading.Bot.Domain.MarketData.Live;

namespace QQ.Trading.Bot.Domain.Tests.MarketData;

public sealed class CurrentSessionBarJsonlParserTests
{
    [Fact]
    public void Valid_parser_contract_sample_parses_but_is_not_real_market_data()
    {
        var result = new CurrentSessionBarJsonlParser().ParseFile(
            Path.Combine(FindRepoRoot(), "fixtures", "live11d", "parser-contract-sample-valid-not-market-real.jsonl"));

        Assert.True(result.IsValid);
        Assert.True(result.IsParserContractSampleNotMarketReal);
        Assert.False(result.RealLocalCurrentSessionBars);
        Assert.Equal(2, result.Records.Count);
        Assert.True(result.MonitoringOnly);
        Assert.True(result.NonExecutable);
        Assert.False(result.LevelPackProduced);
        Assert.False(result.TheoreticalTargetProduced);
        Assert.False(result.CandidateProduced);
        Assert.False(result.SignalProduced);
        Assert.False(result.OrderProduced);
        Assert.False(result.TradingReadinessProduced);
    }

    [Fact]
    public void Missing_required_fields_are_rejected()
    {
        var result = Parse(["{\"sourceSystem\":\"nt8_local_bar_export\"}"]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "PHASE_OR_SCHEMA_VERSION_REQUIRED");
        Assert.Contains(result.Issues, issue => issue.Code == "ARTIFACT_TYPE_REQUIRED");
        Assert.Contains(result.Issues, issue => issue.Code == "INSTRUMENT_REQUIRED");
        Assert.Contains(result.Issues, issue => issue.Code == "TIMESTAMP_UTC_REQUIRED");
    }

    [Fact]
    public void Inconsistent_file_level_fields_are_rejected()
    {
        var result = Parse([
            ValidLine(instrument: "MNQ", sessionDate: "2026-05-14", tickSize: 0.25m, barPeriodType: "Minute", barPeriodValue: 1, timestampUtc: "2026-05-14T13:30:00.0000000+00:00"),
            ValidLine(instrument: "NQ", sessionDate: "2026-05-15", tickSize: 0.50m, barPeriodType: "Second", barPeriodValue: 5, timestampUtc: "2026-05-14T13:31:00.0000000+00:00")
        ]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "INSTRUMENT_INCONSISTENT");
        Assert.Contains(result.Issues, issue => issue.Code == "SESSION_DATE_INCONSISTENT");
        Assert.Contains(result.Issues, issue => issue.Code == "TICK_SIZE_INCONSISTENT");
        Assert.Contains(result.Issues, issue => issue.Code == "BAR_PERIOD_TYPE_INCONSISTENT");
        Assert.Contains(result.Issues, issue => issue.Code == "BAR_PERIOD_VALUE_INCONSISTENT");
    }

    [Fact]
    public void Non_increasing_timestamps_are_rejected()
    {
        var result = new CurrentSessionBarJsonlParser().ParseFile(
            Path.Combine(FindRepoRoot(), "fixtures", "live11d", "parser-contract-sample-invalid-timestamp-order.jsonl"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "TIMESTAMP_NOT_STRICTLY_INCREASING");
    }

    [Fact]
    public void Invalid_ohlc_is_rejected()
    {
        var result = Parse([ValidLine(high: 29188.00m, low: 29189.00m)]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "OHLC_HIGH_BELOW_LOW");
    }

    [Fact]
    public void Negative_volume_is_rejected()
    {
        var result = Parse([ValidLine(volume: -1m)]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "VOLUME_NEGATIVE");
    }

    [Fact]
    public void Price_not_aligned_to_tick_size_is_rejected()
    {
        var result = Parse([ValidLine(open: 29188.10m)]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "PRICE_NOT_ALIGNED_TO_TICK_SIZE");
    }

    [Theory]
    [InlineData("signal")]
    [InlineData("order")]
    [InlineData("recommendation")]
    [InlineData("executionIntent")]
    [InlineData("quantity")]
    [InlineData("positionSize")]
    [InlineData("broker")]
    [InlineData("Tradovate")]
    [InlineData("credential")]
    [InlineData("apiKey")]
    [InlineData("websocket")]
    [InlineData("http")]
    [InlineData("polling")]
    [InlineData("scheduler")]
    [InlineData("watcher")]
    public void Unsafe_fields_are_rejected(string fieldName)
    {
        var json = InsertProperty(ValidLine(), fieldName, "forbidden");
        var result = Parse([json]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "UNSAFE_EXECUTION_OR_SIGNAL_FIELD_PRESENT");
    }

    [Fact]
    public void Export12_observation_snapshot_shape_is_rejected()
    {
        var result = new CurrentSessionBarJsonlParser().ParseLines([
            File.ReadAllText(Path.Combine(
                FindRepoRoot(),
                "fixtures",
                "live9nt8export12",
                "qq_sim101_monitoring_export_MNQ_20260514T092000Z_seq4819.json"))
        ]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "OBSERVATION_SNAPSHOT_SHAPE_REJECTED");
    }

    [Fact]
    public void Live10a_monitoring_target_shape_is_rejected()
    {
        var result = new CurrentSessionBarJsonlParser().ParseLines([
            File.ReadAllText(Path.Combine(FindRepoRoot(), "fixtures", "live10a", "current-date-monitoring-target.json"))
        ]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "LIVE10A_MONITORING_TARGET_SHAPE_REJECTED");
    }

    [Fact]
    public void Live11b_synthetic_plumbing_fixture_is_not_accepted_as_real_bars()
    {
        var result = new CurrentSessionBarJsonlParser().ParseFile(
            Path.Combine(FindRepoRoot(), "fixtures", "live11b", "current-session-bar-fixture.json"));

        Assert.False(result.IsValid);
        Assert.False(result.RealLocalCurrentSessionBars);
        Assert.Contains(result.Issues, issue => issue.Code == "LIVE11B_SYNTHETIC_PLUMBING_BAR_REJECTED");
    }

    [Fact]
    public void Parser_does_not_produce_level_pack_candidate_target_signal_order_or_trading_readiness()
    {
        var result = Parse([ValidLine()]);

        Assert.True(result.IsValid);
        Assert.True(result.RealLocalCurrentSessionBars);
        Assert.False(result.LevelPackProduced);
        Assert.False(result.TheoreticalTargetProduced);
        Assert.False(result.CandidateProduced);
        Assert.False(result.SignalProduced);
        Assert.False(result.OrderProduced);
        Assert.False(result.TradingReadinessProduced);
    }

    private static CurrentSessionBarJsonlParseResult Parse(string[] lines)
        => new CurrentSessionBarJsonlParser().ParseLines(lines);

    private static string ValidLine(
        string instrument = "MNQ",
        string sessionDate = "2026-05-14",
        decimal tickSize = 0.25m,
        string barPeriodType = "Minute",
        int barPeriodValue = 1,
        string timestampUtc = "2026-05-14T13:30:00.0000000+00:00",
        decimal open = 29188.25m,
        decimal high = 29189.00m,
        decimal low = 29187.25m,
        decimal close = 29188.00m,
        decimal? volume = 1000m)
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["phase"] = "BOT-LIVE11D",
            ["sourceSystem"] = "nt8_local_bar_export",
            ["artifactType"] = "current_session_strategy_bar",
            ["monitoringOnly"] = true,
            ["nonExecutable"] = true,
            ["instrument"] = instrument,
            ["instrumentFullName"] = "MNQ contract test",
            ["masterInstrumentName"] = instrument,
            ["tickSize"] = tickSize,
            ["sessionDate"] = sessionDate,
            ["sessionTimezone"] = "America/New_York",
            ["barPeriodType"] = barPeriodType,
            ["barPeriodValue"] = barPeriodValue,
            ["timestampUtc"] = timestampUtc,
            ["open"] = open,
            ["high"] = high,
            ["low"] = low,
            ["close"] = close,
            ["volume"] = volume,
            ["isHistorical"] = true,
            ["isRealtime"] = false,
            ["barsInProgress"] = 0,
            ["tradingHoursName"] = "CME US Index Futures ETH",
            ["calculateMode"] = "OnBarClose"
        });

    private static string InsertProperty(string json, string propertyName, string value)
        => json.Insert(json.LastIndexOf('}'), $",\"{propertyName}\":\"{value}\"");

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "QQ.Trading.Bot.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root.");
    }
}
