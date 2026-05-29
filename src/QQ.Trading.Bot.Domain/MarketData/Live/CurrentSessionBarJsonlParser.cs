using System.Text.Json;

namespace QQ.Trading.Bot.Domain.MarketData.Live;

public sealed class CurrentSessionBarJsonlParser
{
    public const string Phase = "BOT-LIVE11D";
    public const string ExpectedInputPath = "fixtures/incoming/current-session-bars/nt8_mnq_current_session_bars.jsonl";
    public const string ParserContractSampleLabel = "parser_contract_sample_not_market_real";
    private const string SourceSystem = "nt8_local_bar_export";
    private const string ArtifactType = "current_session_strategy_bar";

    private static readonly HashSet<string> UnsafeFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "signal",
        "order",
        "recommendation",
        "executionIntent",
        "quantity",
        "positionSize",
        "positionSizing",
        "broker",
        "Tradovate",
        "credential",
        "apiKey",
        "websocket",
        "http",
        "polling",
        "scheduler",
        "watcher"
    };

    private static readonly HashSet<string> ObservationSnapshotFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "records",
        "observationId",
        "observationTimestampUtc",
        "lastPrice",
        "barTimestampUtc",
        "barOpen",
        "barHigh",
        "barLow",
        "barClose",
        "barVolume",
        "sourceAccountLabel",
        "exportSafetyNotice"
    };

    private static readonly HashSet<string> MonitoringTargetFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "targetType",
        "targetLabels",
        "strategyLineage",
        "referencePrice",
        "zoneLow",
        "zoneHigh",
        "zoneMid",
        "entryBoundary",
        "farBoundary",
        "directionForMonitoringOnly"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CurrentSessionBarJsonlParseResult ParseFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Invalid("INPUT_PATH_REQUIRED", "An explicit local JSONL file path is required.");
        }

        if (Directory.Exists(path))
        {
            return Invalid("DIRECTORY_INPUT_REJECTED", "Directory input is rejected; provide the explicit JSONL file.");
        }

        if (!File.Exists(path))
        {
            return Invalid("INPUT_FILE_NOT_FOUND", "The explicit current-session bar JSONL file was not found.");
        }

        return ParseLines(File.ReadAllLines(path), path);
    }

    public CurrentSessionBarJsonlParseResult ParseLines(IEnumerable<string> lines, string sourcePath = ExpectedInputPath)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var issues = new List<CurrentSessionBarJsonlValidationIssue>();
        var records = new List<CurrentSessionStrategyBarRecord>();
        var sawParserSampleLabel = false;
        var lineNumber = 0;

        foreach (var line in lines)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(Issue(lineNumber, "JSONL_RECORD_MUST_BE_OBJECT", "Each JSONL line must be one JSON object."));
                    continue;
                }

                var root = document.RootElement;
                RejectKnownNonStrategyShapes(root, lineNumber, issues);
                RejectUnsafeFields(root, lineNumber, issues);

                var record = ParseRecord(root, lineNumber, issues);
                if (record is not null)
                {
                    records.Add(record);
                    sawParserSampleLabel |= string.Equals(record.FixtureLabel, ParserContractSampleLabel, StringComparison.Ordinal);
                }

                if (record is not null &&
                    IsLive11BSyntheticShape(root, record) &&
                    !string.Equals(record.FixtureLabel, ParserContractSampleLabel, StringComparison.Ordinal))
                {
                    issues.Add(Issue(lineNumber, "LIVE11B_SYNTHETIC_PLUMBING_BAR_REJECTED", "LIVE11B synthetic plumbing bars are rejected as real local current-session strategy bars unless explicitly labelled as parser contract samples."));
                }
            }
            catch (JsonException exception)
            {
                issues.Add(Issue(lineNumber, "JSONL_RECORD_MALFORMED", exception.Message));
            }
        }

        if (records.Count == 0 && issues.All(issue => issue.Code != "INPUT_FILE_EMPTY"))
        {
            issues.Add(Issue(null, "NO_CURRENT_SESSION_BARS", "No current-session strategy bar records were parsed."));
        }

        ValidateFileLevel(records, sawParserSampleLabel, issues);

        var valid = issues.Count == 0;
        return new CurrentSessionBarJsonlParseResult(
            Phase,
            sourcePath,
            valid,
            sawParserSampleLabel && valid,
            valid && !sawParserSampleLabel,
            records,
            issues,
            MonitoringOnly: true,
            NonExecutable: true,
            LevelPackProduced: false,
            TheoreticalTargetProduced: false,
            CandidateProduced: false,
            SignalProduced: false,
            OrderProduced: false,
            TradingReadinessProduced: false);
    }

    private static CurrentSessionStrategyBarRecord? ParseRecord(
        JsonElement root,
        int lineNumber,
        List<CurrentSessionBarJsonlValidationIssue> issues)
    {
        var phase = OptionalString(root, "phase");
        var exportSchemaVersion = OptionalString(root, "exportSchemaVersion");
        if (string.IsNullOrWhiteSpace(phase) && string.IsNullOrWhiteSpace(exportSchemaVersion))
        {
            issues.Add(Issue(lineNumber, "PHASE_OR_SCHEMA_VERSION_REQUIRED", "Each record must include phase or exportSchemaVersion."));
        }

        var sourceSystem = RequiredString(root, "sourceSystem", lineNumber, issues);
        var artifactType = RequiredString(root, "artifactType", lineNumber, issues);
        var instrument = RequiredString(root, "instrument", lineNumber, issues);
        var sessionDate = RequiredString(root, "sessionDate", lineNumber, issues);
        var sessionTimezone = RequiredString(root, "sessionTimezone", lineNumber, issues);
        var barPeriodType = RequiredString(root, "barPeriodType", lineNumber, issues);
        var timestampUtc = RequiredDateTimeOffset(root, "timestampUtc", lineNumber, issues);
        var open = RequiredDecimal(root, "open", lineNumber, issues);
        var high = RequiredDecimal(root, "high", lineNumber, issues);
        var low = RequiredDecimal(root, "low", lineNumber, issues);
        var close = RequiredDecimal(root, "close", lineNumber, issues);
        var tickSize = RequiredDecimal(root, "tickSize", lineNumber, issues);
        var barPeriodValue = RequiredInt(root, "barPeriodValue", lineNumber, issues);
        var volume = OptionalDecimal(root, "volume", lineNumber, issues);
        var monitoringOnly = RequiredBool(root, "monitoringOnly", lineNumber, issues);
        var nonExecutable = RequiredBool(root, "nonExecutable", lineNumber, issues);
        var isHistorical = RequiredBool(root, "isHistorical", lineNumber, issues);
        var isRealtime = RequiredBool(root, "isRealtime", lineNumber, issues);

        if (!string.Equals(sourceSystem, SourceSystem, StringComparison.Ordinal))
        {
            issues.Add(Issue(lineNumber, "SOURCE_SYSTEM_REJECTED", "sourceSystem must be nt8_local_bar_export."));
        }

        if (!string.Equals(artifactType, ArtifactType, StringComparison.Ordinal))
        {
            issues.Add(Issue(lineNumber, "ARTIFACT_TYPE_REJECTED", "artifactType must be current_session_strategy_bar."));
        }

        if (monitoringOnly != true)
        {
            issues.Add(Issue(lineNumber, "MONITORING_ONLY_REQUIRED", "monitoringOnly must be true."));
        }

        if (nonExecutable != true)
        {
            issues.Add(Issue(lineNumber, "NON_EXECUTABLE_REQUIRED", "nonExecutable must be true."));
        }

        if (tickSize <= 0)
        {
            issues.Add(Issue(lineNumber, "TICK_SIZE_INVALID", "tickSize must be greater than zero."));
        }

        if (barPeriodValue <= 0)
        {
            issues.Add(Issue(lineNumber, "BAR_PERIOD_VALUE_INVALID", "barPeriodValue must be greater than zero."));
        }

        if (timestampUtc is not null && timestampUtc.Value.Offset != TimeSpan.Zero)
        {
            issues.Add(Issue(lineNumber, "TIMESTAMP_UTC_OFFSET_INVALID", "timestampUtc must use UTC offset zero."));
        }

        if (volume < 0)
        {
            issues.Add(Issue(lineNumber, "VOLUME_NEGATIVE", "volume cannot be negative when present."));
        }

        if (high < low)
        {
            issues.Add(Issue(lineNumber, "OHLC_HIGH_BELOW_LOW", "high must be greater than or equal to low."));
        }

        if (high < Math.Max(open, close))
        {
            issues.Add(Issue(lineNumber, "OHLC_HIGH_BELOW_OPEN_OR_CLOSE", "high must be greater than or equal to open and close."));
        }

        if (low > Math.Min(open, close))
        {
            issues.Add(Issue(lineNumber, "OHLC_LOW_ABOVE_OPEN_OR_CLOSE", "low must be less than or equal to open and close."));
        }

        if (tickSize > 0)
        {
            ValidateTickAlignment(open, tickSize, "open", lineNumber, issues);
            ValidateTickAlignment(high, tickSize, "high", lineNumber, issues);
            ValidateTickAlignment(low, tickSize, "low", lineNumber, issues);
            ValidateTickAlignment(close, tickSize, "close", lineNumber, issues);
        }

        return new CurrentSessionStrategyBarRecord(
            phase,
            exportSchemaVersion,
            sourceSystem,
            artifactType,
            monitoringOnly,
            nonExecutable,
            instrument,
            OptionalString(root, "instrumentFullName"),
            OptionalString(root, "masterInstrumentName"),
            tickSize,
            sessionDate,
            sessionTimezone,
            barPeriodType,
            barPeriodValue,
            timestampUtc,
            open,
            high,
            low,
            close,
            volume,
            isHistorical,
            isRealtime,
            OptionalInt(root, "barsInProgress", lineNumber, issues),
            OptionalString(root, "tradingHoursName"),
            OptionalString(root, "calculateMode"),
            OptionalString(root, "fixtureLabel"));
    }

    private static void ValidateFileLevel(
        IReadOnlyList<CurrentSessionStrategyBarRecord> records,
        bool sawParserSampleLabel,
        List<CurrentSessionBarJsonlValidationIssue> issues)
    {
        if (records.Count == 0)
        {
            return;
        }

        ValidateConsistent(records.Select(record => record.Instrument), "INSTRUMENT_INCONSISTENT", "All records must have the same instrument.", issues);
        ValidateConsistent(records.Select(record => record.SessionDate), "SESSION_DATE_INCONSISTENT", "All records must have the same sessionDate.", issues);
        ValidateConsistent(records.Select(record => record.TickSize.ToString(System.Globalization.CultureInfo.InvariantCulture)), "TICK_SIZE_INCONSISTENT", "All records must have the same tickSize.", issues);
        ValidateConsistent(records.Select(record => record.BarPeriodType), "BAR_PERIOD_TYPE_INCONSISTENT", "All records must have the same barPeriodType.", issues);
        ValidateConsistent(records.Select(record => record.BarPeriodValue.ToString(System.Globalization.CultureInfo.InvariantCulture)), "BAR_PERIOD_VALUE_INCONSISTENT", "All records must have the same barPeriodValue.", issues);

        for (var index = 1; index < records.Count; index++)
        {
            var previous = records[index - 1].TimestampUtc;
            var current = records[index].TimestampUtc;
            if (previous is not null && current is not null && current <= previous)
            {
                issues.Add(Issue(index + 1, "TIMESTAMP_NOT_STRICTLY_INCREASING", "timestampUtc must be strictly increasing across JSONL records."));
            }
        }

        if (sawParserSampleLabel &&
            records.Any(record => !string.Equals(record.FixtureLabel, ParserContractSampleLabel, StringComparison.Ordinal)))
        {
            issues.Add(Issue(null, "PARSER_SAMPLE_LABEL_INCONSISTENT", "If any record is labelled as a parser contract sample, all records must carry that label."));
        }
    }

    private static void ValidateConsistent(IEnumerable<string?> values, string code, string message, List<CurrentSessionBarJsonlValidationIssue> issues)
    {
        var distinct = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
        if (distinct.Length > 1)
        {
            issues.Add(Issue(null, code, message));
        }
    }

    private static void RejectKnownNonStrategyShapes(
        JsonElement element,
        int lineNumber,
        List<CurrentSessionBarJsonlValidationIssue> issues)
    {
        var fieldNames = EnumeratePropertyNames(element).ToArray();
        if (fieldNames.Any(name => ObservationSnapshotFields.Contains(name)))
        {
            issues.Add(Issue(lineNumber, "OBSERVATION_SNAPSHOT_SHAPE_REJECTED", "EXPORT12/NT8 monitoring observation snapshots are not current-session strategy bars."));
        }

        if (fieldNames.Any(name => MonitoringTargetFields.Contains(name)))
        {
            issues.Add(Issue(lineNumber, "LIVE10A_MONITORING_TARGET_SHAPE_REJECTED", "LIVE10A monitoring target shapes are not current-session strategy bars."));
        }
    }

    private static void RejectUnsafeFields(
        JsonElement element,
        int lineNumber,
        List<CurrentSessionBarJsonlValidationIssue> issues)
    {
        foreach (var propertyName in EnumeratePropertyNames(element))
        {
            if (UnsafeFieldNames.Contains(propertyName))
            {
                issues.Add(Issue(lineNumber, "UNSAFE_EXECUTION_OR_SIGNAL_FIELD_PRESENT", $"Forbidden field '{propertyName}' is not allowed in current-session strategy bar JSONL."));
            }
        }
    }

    private static IEnumerable<string> EnumeratePropertyNames(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var property in element.EnumerateObject())
        {
            yield return property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var child in EnumeratePropertyNames(property.Value))
                {
                    yield return child;
                }
            }
            else if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                {
                    foreach (var child in EnumeratePropertyNames(item))
                    {
                        yield return child;
                    }
                }
            }
        }
    }

    private static bool IsLive11BSyntheticShape(JsonElement root, CurrentSessionStrategyBarRecord record)
        => string.IsNullOrWhiteSpace(record.SourceSystem) &&
           string.IsNullOrWhiteSpace(record.ArtifactType) &&
           root.TryGetProperty("symbol", out _) &&
           root.TryGetProperty("tradingDate", out _) &&
           root.TryGetProperty("minutesFromCoreOpen", out _);

    private static void ValidateTickAlignment(decimal value, decimal tickSize, string field, int lineNumber, List<CurrentSessionBarJsonlValidationIssue> issues)
    {
        var ticks = value / tickSize;
        if (ticks != decimal.Truncate(ticks))
        {
            issues.Add(Issue(lineNumber, "PRICE_NOT_ALIGNED_TO_TICK_SIZE", $"{field} must align to tickSize where possible."));
        }
    }

    private static string? OptionalString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    private static string? RequiredString(JsonElement root, string propertyName, int lineNumber, List<CurrentSessionBarJsonlValidationIssue> issues)
    {
        var value = OptionalString(root, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(Issue(lineNumber, $"{ToCode(propertyName)}_REQUIRED", $"{propertyName} is required."));
        }

        return value;
    }

    private static decimal RequiredDecimal(JsonElement root, string propertyName, int lineNumber, List<CurrentSessionBarJsonlValidationIssue> issues)
    {
        var value = OptionalDecimal(root, propertyName, lineNumber, issues);
        if (value is null)
        {
            issues.Add(Issue(lineNumber, $"{ToCode(propertyName)}_REQUIRED", $"{propertyName} is required."));
        }

        return value ?? 0m;
    }

    private static decimal? OptionalDecimal(JsonElement root, string propertyName, int lineNumber, List<CurrentSessionBarJsonlValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var result))
        {
            return result;
        }

        issues.Add(Issue(lineNumber, $"{ToCode(propertyName)}_INVALID", $"{propertyName} must be numeric."));
        return null;
    }

    private static int RequiredInt(JsonElement root, string propertyName, int lineNumber, List<CurrentSessionBarJsonlValidationIssue> issues)
    {
        var value = OptionalInt(root, propertyName, lineNumber, issues);
        if (value is null)
        {
            issues.Add(Issue(lineNumber, $"{ToCode(propertyName)}_REQUIRED", $"{propertyName} is required."));
        }

        return value ?? 0;
    }

    private static int? OptionalInt(JsonElement root, string propertyName, int lineNumber, List<CurrentSessionBarJsonlValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result))
        {
            return result;
        }

        issues.Add(Issue(lineNumber, $"{ToCode(propertyName)}_INVALID", $"{propertyName} must be an integer."));
        return null;
    }

    private static bool? RequiredBool(JsonElement root, string propertyName, int lineNumber, List<CurrentSessionBarJsonlValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            issues.Add(Issue(lineNumber, $"{ToCode(propertyName)}_REQUIRED", $"{propertyName} is required."));
            return null;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        issues.Add(Issue(lineNumber, $"{ToCode(propertyName)}_INVALID", $"{propertyName} must be boolean."));
        return null;
    }

    private static DateTimeOffset? RequiredDateTimeOffset(JsonElement root, string propertyName, int lineNumber, List<CurrentSessionBarJsonlValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            issues.Add(Issue(lineNumber, $"{ToCode(propertyName)}_REQUIRED", $"{propertyName} is required."));
            return null;
        }

        try
        {
            return value.Deserialize<DateTimeOffset>(JsonOptions);
        }
        catch (JsonException exception)
        {
            issues.Add(Issue(lineNumber, $"{ToCode(propertyName)}_INVALID", exception.Message));
            return null;
        }
    }

    private static CurrentSessionBarJsonlParseResult Invalid(string code, string message)
        => new(
            Phase,
            ExpectedInputPath,
            IsValid: false,
            IsParserContractSampleNotMarketReal: false,
            RealLocalCurrentSessionBars: false,
            Records: [],
            Issues: [Issue(null, code, message)],
            MonitoringOnly: true,
            NonExecutable: true,
            LevelPackProduced: false,
            TheoreticalTargetProduced: false,
            CandidateProduced: false,
            SignalProduced: false,
            OrderProduced: false,
            TradingReadinessProduced: false);

    private static CurrentSessionBarJsonlValidationIssue Issue(int? lineNumber, string code, string message)
        => new(lineNumber, code, message);

    private static string ToCode(string value)
    {
        var chars = new List<char>();
        foreach (var character in value)
        {
            if (char.IsUpper(character) && chars.Count > 0)
            {
                chars.Add('_');
            }

            chars.Add(char.ToUpperInvariant(character));
        }

        return new string(chars.ToArray());
    }
}

public sealed record CurrentSessionBarJsonlParseResult(
    string Phase,
    string SourcePath,
    bool IsValid,
    bool IsParserContractSampleNotMarketReal,
    bool RealLocalCurrentSessionBars,
    IReadOnlyList<CurrentSessionStrategyBarRecord> Records,
    IReadOnlyList<CurrentSessionBarJsonlValidationIssue> Issues,
    bool MonitoringOnly,
    bool NonExecutable,
    bool LevelPackProduced,
    bool TheoreticalTargetProduced,
    bool CandidateProduced,
    bool SignalProduced,
    bool OrderProduced,
    bool TradingReadinessProduced);

public sealed record CurrentSessionBarJsonlValidationIssue(
    int? LineNumber,
    string Code,
    string Message);

public sealed record CurrentSessionStrategyBarRecord(
    string? Phase,
    string? ExportSchemaVersion,
    string? SourceSystem,
    string? ArtifactType,
    bool? MonitoringOnly,
    bool? NonExecutable,
    string? Instrument,
    string? InstrumentFullName,
    string? MasterInstrumentName,
    decimal TickSize,
    string? SessionDate,
    string? SessionTimezone,
    string? BarPeriodType,
    int BarPeriodValue,
    DateTimeOffset? TimestampUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal? Volume,
    bool? IsHistorical,
    bool? IsRealtime,
    int? BarsInProgress,
    string? TradingHoursName,
    string? CalculateMode,
    string? FixtureLabel);
