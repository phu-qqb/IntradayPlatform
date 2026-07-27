using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public sealed record Arch7bCoreRecomputedReport(
    string Label,
    int RowCount,
    int EconomicRecordCount,
    int DuplicateIdenticalCount,
    string RawSha256,
    string HeaderSetSha256,
    string SemanticSha256,
    DateTimeOffset? LatestExecutionTimeUtc,
    IReadOnlyList<string> AccountIds);

public sealed record Arch7bCoreBracketReportSemanticVerification(
    string ContractVersion,
    int SuccessfulAttemptNumber,
    IReadOnlyDictionary<string, Arch7bCoreRecomputedReport> ExecutionReports,
    IReadOnlyDictionary<string, Arch7bCoreRecomputedReport> PositionReports);

public static class Arch7bCoreBracketReportSemanticVerifier
{
    public const string ContractVersion =
        "arch7b_core_bracket_report_semantic_verifier_v1";

    private const string CsvParserVersion = "lmax_portal_report_csv_parser_v2";
    private static readonly JsonSerializerOptions CanonicalJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string[] ExecutionRequiredHeaders =
    [
        "Execution ID", "Timestamp", "Trade Quantity", "Trade Price", "Trade Date",
        "Instrument ID", "Symbol", "Instruction ID", "Order ID", "Type",
        "Total Commission", "Account Id", "Units Bought/Sold", "Notional Value",
        "Trade UTI"
    ];

    private static readonly string[] PositionRequiredHeaders =
    [
        "Instrument", "CCY", "Open Quantity", "Margin on Open Position",
        "Average Opening Price", "Closing Price", "Open Profit / Loss",
        "MTM Valuation Rate to Base CCY", "LMAX Symbol", "Account Id",
        "Position UTI"
    ];

    public static Arch7bCoreBracketReportSemanticVerification Verify(
        string evidenceRoot,
        JsonObject contract,
        IReadOnlySet<string> indexedFiles)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(indexedFiles);
        var attempts = contract["Attempts"]?.AsArray()
            ?? throw new InvalidDataException("ARCH7B_CORE_ATTEMPTS_MISSING");
        Require(attempts.Count is >= 1 and <= 3,
            "ARCH7B_CORE_ATTEMPT_COUNT_INVALID");

        var expectedAttemptFiles = new HashSet<string>(StringComparer.Ordinal);
        var finalExecutionReports =
            new Dictionary<string, Arch7bCoreRecomputedReport>(StringComparer.Ordinal);
        var finalPositionReports =
            new Dictionary<string, Arch7bCoreRecomputedReport>(StringComparer.Ordinal);

        for (var index = 0; index < attempts.Count; index++)
        {
            var expectedNumber = index + 1;
            var attempt = attempts[index]?.AsObject()
                ?? throw new InvalidDataException("ARCH7B_CORE_ATTEMPT_INVALID");
            Require(Int32(attempt, "Attempt") == expectedNumber,
                "ARCH7B_CORE_ATTEMPT_NUMBER_SEQUENCE_INVALID");
            var stable = Boolean(attempt, "Stable");
            Require(stable == (index == attempts.Count - 1),
                stable
                    ? "ARCH7B_CORE_STABLE_ATTEMPT_NOT_TERMINAL"
                    : "ARCH7B_CORE_FINAL_ATTEMPT_UNSTABLE");

            var attemptPrefix = $"attempt-{expectedNumber}";
            var manifestRelative = $"{attemptPrefix}/attempt-manifest.json";
            expectedAttemptFiles.Add(manifestRelative);
            var manifestPath = Path.Combine(evidenceRoot,
                manifestRelative.Replace('/', Path.DirectorySeparatorChar));
            Require(File.Exists(manifestPath),
                $"ARCH7B_CORE_INDEXED_ARTIFACT_MISSING:{manifestRelative}");
            var manifest = ParseObject(manifestPath,
                "ARCH7B_CORE_ATTEMPT_MANIFEST_JSON_INVALID");
            Require(JsonNode.DeepEquals(manifest, attempt),
                "ARCH7B_CORE_ATTEMPT_MANIFEST_CONTRACT_MISMATCH");

            var executions =
                new Dictionary<string, Arch7bCoreRecomputedReport>(StringComparer.Ordinal);
            var positions =
                new Dictionary<string, Arch7bCoreRecomputedReport>(StringComparer.Ordinal);
            foreach (var label in new[] { "T0", "P1", "T1", "P2", "T2" })
            {
                var kind = label[0] == 'T' ? "individual-trades" : "open-positions";
                var relative = $"{attemptPrefix}/{label}-{kind}.csv";
                expectedAttemptFiles.Add(relative);
                Require(indexedFiles.Contains(relative),
                    $"ARCH7B_CORE_INDEX_REQUIRED_FILE_MISSING:{relative}");
                var path = Path.Combine(evidenceRoot,
                    relative.Replace('/', Path.DirectorySeparatorChar));
                var declared = Object(attempt, label);
                var recomputed = label[0] == 'T'
                    ? RecomputeExecution(label, path)
                    : RecomputePosition(label, path);
                CompareDeclaredReport(declared, recomputed,
                    label[0] == 'T'
                        ? "ARCH7B_CORE_EXECUTION_REPORT_SEMANTIC_MISMATCH"
                        : "ARCH7B_CORE_POSITION_REPORT_SEMANTIC_MISMATCH");
                if (label[0] == 'T') executions.Add(label, recomputed);
                else positions.Add(label, recomputed);
            }

            if (stable)
            {
                Require(executions["T0"].SemanticSha256 ==
                        executions["T1"].SemanticSha256 &&
                        executions["T1"].SemanticSha256 ==
                        executions["T2"].SemanticSha256,
                    "ARCH7B_CORE_EXECUTION_REPORT_SEMANTIC_MISMATCH");
                Require(positions["P1"].SemanticSha256 ==
                        positions["P2"].SemanticSha256,
                    "ARCH7B_CORE_POSITION_REPORT_SEMANTIC_MISMATCH");
                finalExecutionReports = executions;
                finalPositionReports = positions;
            }
        }

        var actualAttemptFiles = indexedFiles
            .Where(path => Regex.IsMatch(path, @"^attempt-\d+/",
                RegexOptions.CultureInvariant))
            .ToHashSet(StringComparer.Ordinal);
        Require(actualAttemptFiles.SetEquals(expectedAttemptFiles),
            "ARCH7B_CORE_ATTEMPT_INVENTORY_MISMATCH");

        var t2 = finalExecutionReports["T2"];
        var p1 = finalPositionReports["P1"];
        var p2 = finalPositionReports["P2"];
        Require(Int32(contract, "ExecutionCount") == t2.EconomicRecordCount &&
                Int32(contract, "DuplicateIdenticalExecutionCount") ==
                t2.DuplicateIdenticalCount &&
                OptionalDate(contract, "LatestExecutionTime") ==
                t2.LatestExecutionTimeUtc,
            "ARCH7B_CORE_EXECUTION_REPORT_SEMANTIC_MISMATCH");
        Require(Int32(contract, "PositionCount") == p2.EconomicRecordCount,
            "ARCH7B_CORE_POSITION_REPORT_SEMANTIC_MISMATCH");
        if (Int32(contract, "PositionCount") == 0)
        {
            Require(p1.RowCount == 0 && p2.RowCount == 0 &&
                    p1.SemanticSha256 == p2.SemanticSha256,
                "ARCH7B_CORE_POSITION_REPORT_SEMANTIC_MISMATCH");
        }

        return new(
            ContractVersion,
            attempts.Count,
            finalExecutionReports,
            finalPositionReports);
    }

    private static Arch7bCoreRecomputedReport RecomputeExecution(
        string label,
        string path)
    {
        var document = ParseCsv(path);
        ValidateHeaders(document, ExecutionRequiredHeaders,
            "ARCH7B_LMAX_INDIVIDUAL_TRADES_HEADER_CONTRACT_MISMATCH");
        var records = new Dictionary<string, ExecutionRecord>(StringComparer.Ordinal);
        var duplicateIdentical = 0;
        foreach (var row in document.Rows)
        {
            var accountId = RequireAccount(document, row);
            var executionId = Required(Field(document, row, "Execution ID"),
                "ARCH7B_LMAX_EXECUTION_ID_EMPTY");
            var timestamp = ParseTimestamp(Field(document, row, "Timestamp"));
            var record = new ExecutionRecord(
                accountId,
                executionId,
                Text(Field(document, row, "Mtf Execution ID")),
                Required(Field(document, row, "Order ID"),
                    "ARCH7B_LMAX_ORDER_ID_EMPTY"),
                Required(Field(document, row, "Instruction ID"),
                    "ARCH7B_LMAX_INSTRUCTION_ID_EMPTY"),
                Required(Field(document, row, "Trade UTI"),
                    "ARCH7B_LMAX_TRADE_UTI_EMPTY"),
                timestamp,
                ParseTradeDate(Field(document, row, "Trade Date")),
                Required(Field(document, row, "Instrument ID"),
                    "ARCH7B_LMAX_INSTRUMENT_ID_EMPTY"),
                Required(Field(document, row, "Symbol"),
                    "ARCH7B_LMAX_SYMBOL_EMPTY"),
                ParseSide(Field(document, row, "Type")),
                RequiredDecimal(Field(document, row, "Trade Quantity")),
                RequiredDecimal(Field(document, row, "Units Bought/Sold")),
                RequiredDecimal(Field(document, row, "Trade Price")),
                OptionalDecimal(Field(document, row, "Total Commission")),
                RequiredDecimal(Field(document, row, "Notional Value")));
            _ = OptionalDecimal(Field(document, row, "Stop Price"));
            _ = OptionalDecimal(Field(document, row, "Limit Price"));
            _ = OptionalDecimal(Field(document, row, "Total Profit Loss"));
            if (records.TryGetValue(executionId, out var existing))
            {
                Require(existing == record, "ARCH7B_LMAX_EXECUTION_ID_CONFLICT");
                duplicateIdentical++;
            }
            else
            {
                records.Add(executionId, record);
            }
        }

        var ordered = records.Values
            .OrderBy(value => value.ExecutionId, StringComparer.Ordinal)
            .Select(ExecutionJson)
            .ToArray();
        var latest = records.Values
            .Select(value => value.ExecutionTimestampUtc)
            .DefaultIfEmpty()
            .Max();
        return new(
            label,
            document.Rows.Count,
            ordered.Length,
            duplicateIdentical,
            FileSha(path),
            HeaderSha(document),
            SemanticSha(ordered),
            latest == default ? null : latest,
            records.Values.Select(value => value.AccountId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    private static Arch7bCoreRecomputedReport RecomputePosition(
        string label,
        string path)
    {
        var document = ParseCsv(path);
        ValidateHeaders(document, PositionRequiredHeaders,
            "ARCH7B_LMAX_OPEN_POSITIONS_HEADER_CONTRACT_MISMATCH");
        var records = new Dictionary<string, PositionRecord>(StringComparer.Ordinal);
        foreach (var row in document.Rows)
        {
            var accountId = RequireAccount(document, row);
            var positionUti = Required(Field(document, row, "Position UTI"),
                "ARCH7B_LMAX_POSITION_IDENTITY_EMPTY");
            var quantityText = Field(document, row, "Open Quantity");
            Require(!string.IsNullOrWhiteSpace(quantityText),
                "ARCH7B_LMAX_OPEN_POSITION_QUANTITY_MISSING");
            var quantity = RequiredDecimal(quantityText);
            var record = new PositionRecord(
                accountId,
                positionUti,
                Required(Field(document, row, "Instrument"),
                    "ARCH7B_LMAX_POSITION_INSTRUMENT_EMPTY"),
                Required(Field(document, row, "LMAX Symbol"),
                    "ARCH7B_LMAX_POSITION_SYMBOL_EMPTY"),
                Text(Field(document, row, "CCY")),
                quantity == "0" ? "FLAT" :
                    quantity.StartsWith("-", StringComparison.Ordinal) ? "SELL" : "BUY",
                quantity,
                OptionalDecimal(Field(document, row, "Margin on Open Position")),
                OptionalDecimal(Field(document, row, "Average Opening Price")),
                OptionalDecimal(Field(document, row, "Closing Price")),
                OptionalDecimal(Field(document, row, "Open Profit / Loss")),
                OptionalDecimal(Field(document, row,
                    "MTM Valuation Rate to Base CCY")));
            Require(records.TryAdd(positionUti, record),
                "ARCH7B_LMAX_POSITION_IDENTITY_DUPLICATE");
        }

        var ordered = records.Values
            .OrderBy(value => value.PositionIdentity, StringComparer.Ordinal)
            .Select(PositionJson)
            .ToArray();
        return new(
            label,
            document.Rows.Count,
            ordered.Length,
            0,
            FileSha(path),
            HeaderSha(document),
            SemanticSha(ordered),
            null,
            records.Values.Select(value => value.AccountId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    private static void CompareDeclaredReport(
        JsonObject declared,
        Arch7bCoreRecomputedReport recomputed,
        string blocker)
    {
        Require(Text(declared, "RawSha256") == recomputed.RawSha256,
            blocker);
        Require(Int32(declared, "RowCount") == recomputed.RowCount &&
                Text(declared, "HeaderSetSha256") ==
                recomputed.HeaderSetSha256 &&
                Text(declared, "SemanticSha256") ==
                recomputed.SemanticSha256,
            blocker);
        Require(recomputed.AccountIds.Count == 0 ||
                recomputed.AccountIds.SequenceEqual(
                    [Arch7bBracketedGlobalFlatContract.AccountId],
                    StringComparer.Ordinal),
            blocker);
    }

    private static CsvDocument ParseCsv(string path)
    {
        Require(File.Exists(path), "ARCH7B_CORE_REPORT_FILE_MISSING");
        var source = Encoding.UTF8.GetString(File.ReadAllBytes(path));
        if (source.StartsWith('\uFEFF')) source = source[1..];
        var rawRows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < source.Length && source[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(character);
                }
            }
            else if (character == '"')
            {
                quoted = true;
            }
            else if (character == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (character == '\n')
            {
                row.Add(TrimTrailingCarriageReturn(field.ToString()));
                rawRows.Add(row);
                row = [];
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }
        Require(!quoted, "ARCH7B_LMAX_REPORT_PARTIAL_RESPONSE");
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(TrimTrailingCarriageReturn(field.ToString()));
            rawRows.Add(row);
        }
        while (rawRows.Count > 0 && rawRows[^1].All(value => value == ""))
            rawRows.RemoveAt(rawRows.Count - 1);
        Require(rawRows.Count >= 1 && rawRows[0].Count >= 1,
            "ARCH7B_LMAX_REPORT_PARTIAL_RESPONSE");
        var headers = rawRows[0].Select(Text).ToArray();
        Require(headers.All(value => value.Length > 0),
            "ARCH7B_LMAX_REPORT_HEADER_INVALID");
        var normalized = headers.Select(NormalizeHeader).ToArray();
        Require(normalized.Distinct(StringComparer.Ordinal).Count() ==
                normalized.Length,
            "ARCH7B_LMAX_REPORT_HEADER_DUPLICATE");
        var rows = rawRows.Skip(1).Select(values =>
        {
            Require(values.Count == headers.Length,
                "ARCH7B_LMAX_REPORT_PARTIAL_RESPONSE");
            return values.Select(Text).ToArray();
        }).ToArray();
        return new(headers, normalized, rows);
    }

    private static void ValidateHeaders(
        CsvDocument document,
        IReadOnlyList<string> required,
        string blocker)
    {
        var observed = document.NormalizedHeaders.ToHashSet(StringComparer.Ordinal);
        Require(required.Select(NormalizeHeader).All(observed.Contains), blocker);
    }

    private static string Field(
        CsvDocument document,
        IReadOnlyList<string> row,
        string expectedHeader)
    {
        var normalized = NormalizeHeader(expectedHeader);
        var index = Array.IndexOf(document.NormalizedHeaders, normalized);
        return index < 0 ? "" : row[index];
    }

    private static string RequireAccount(
        CsvDocument document,
        IReadOnlyList<string> row)
    {
        var value = Text(Field(document, row, "Account Id"));
        Require(value == Arch7bBracketedGlobalFlatContract.AccountId,
            "ARCH7B_LMAX_REPORT_ACCOUNT_MISMATCH");
        return value;
    }

    private static string Required(string value, string blocker)
    {
        var text = Text(value);
        Require(text.Length > 0, blocker);
        return text;
    }

    private static string ParseSide(string value)
    {
        var side = Required(value, "ARCH7B_LMAX_REPORT_SIDE_INVALID")
            .ToUpperInvariant();
        Require(side is "BUY" or "SELL", "ARCH7B_LMAX_REPORT_SIDE_INVALID");
        return side;
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        var text = Text(value);
        var validShape = Regex.IsMatch(text,
            @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var parsed = default(DateTimeOffset);
        var parsedExactly = validShape && DateTimeOffset.TryParseExact(text,
            ["yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out parsed);
        Require(parsedExactly, "ARCH7B_LMAX_REPORT_TIMESTAMP_INVALID");
        return parsed.ToUniversalTime();
    }

    private static string ParseTradeDate(string value)
    {
        var text = Text(value);
        Require(DateOnly.TryParseExact(text, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "ARCH7B_LMAX_REPORT_TRADE_DATE_INVALID");
        return text;
    }

    private static string RequiredDecimal(string value)
    {
        Require(Text(value).Length > 0,
            "ARCH7B_LMAX_REPORT_REQUIRED_DECIMAL_MISSING");
        return CanonicalDecimal(value);
    }

    private static string OptionalDecimal(string value) =>
        Text(value).Length == 0 ? "" : CanonicalDecimal(value);

    private static string CanonicalDecimal(string value)
    {
        var text = Text(value);
        var match = Regex.Match(text,
            @"^([+-]?)(?:(\d+)|(\d{1,3}(?:,\d{3})+))(?:\.(\d+))?(?:[eE]([+-]?\d+))?$",
            RegexOptions.CultureInvariant);
        Require(match.Success, "ARCH7B_LMAX_REPORT_DECIMAL_INVALID");
        var invariant = text.Replace(",", "", StringComparison.Ordinal);
        Require(double.TryParse(invariant, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var finite) &&
                double.IsFinite(finite),
            "ARCH7B_LMAX_REPORT_DECIMAL_INVALID");
        var integer = (match.Groups[2].Success
            ? match.Groups[2].Value
            : match.Groups[3].Value.Replace(",", "", StringComparison.Ordinal));
        var fraction = match.Groups[4].Value;
        Require(int.TryParse(match.Groups[5].Success
                    ? match.Groups[5].Value
                    : "0",
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var exponent),
            "ARCH7B_LMAX_REPORT_DECIMAL_INVALID");
        var digits = integer + fraction;
        var decimalIndex = integer.Length + exponent;
        var leadingZeroCount = digits.TakeWhile(character => character == '0').Count();
        digits = digits[leadingZeroCount..];
        decimalIndex -= leadingZeroCount;
        if (digits.Length == 0) return "0";
        if (decimalIndex <= 0)
        {
            digits = new string('0', -decimalIndex) + digits;
            decimalIndex = 0;
        }
        else if (decimalIndex >= digits.Length)
        {
            digits += new string('0', decimalIndex - digits.Length);
            decimalIndex = digits.Length;
        }
        var result = decimalIndex == digits.Length
            ? digits
            : (decimalIndex == 0 ? "0" : digits[..decimalIndex]) +
              "." + digits[decimalIndex..];
        result = Regex.Replace(result, @"^0+(?=\d)", "");
        result = Regex.Replace(result, @"(\.\d*?)0+$", "$1");
        result = result.TrimEnd('.');
        if (result.Length == 0 || Regex.IsMatch(result, @"^0(?:\.0*)?$"))
            return "0";
        return match.Groups[1].Value == "-" ? "-" + result : result;
    }

    private static JsonObject ExecutionJson(ExecutionRecord value) => new()
    {
        ["AccountId"] = value.AccountId,
        ["ExecutionId"] = value.ExecutionId,
        ["MtfExecutionId"] = value.MtfExecutionId,
        ["OrderId"] = value.OrderId,
        ["InstructionId"] = value.InstructionId,
        ["TradeUti"] = value.TradeUti,
        ["ExecutionTimestampUtc"] =
            value.ExecutionTimestampUtc.ToUniversalTime()
                .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
        ["TradeDate"] = value.TradeDate,
        ["InstrumentId"] = value.InstrumentId,
        ["Symbol"] = value.Symbol,
        ["Side"] = value.Side,
        ["Quantity"] = value.Quantity,
        ["UnitsBoughtSold"] = value.UnitsBoughtSold,
        ["Price"] = value.Price,
        ["Commission"] = value.Commission,
        ["Notional"] = value.Notional
    };

    private static JsonObject PositionJson(PositionRecord value) => new()
    {
        ["AccountId"] = value.AccountId,
        ["PositionIdentity"] = value.PositionIdentity,
        ["PositionUti"] = value.PositionIdentity,
        ["RawSymbol"] = value.RawSymbol,
        ["MappedSymbol"] = value.MappedSymbol,
        ["Currency"] = value.Currency,
        ["Side"] = value.Side,
        ["Quantity"] = value.Quantity,
        ["MarginOnOpenPosition"] = value.MarginOnOpenPosition,
        ["AverageOpeningPrice"] = value.AverageOpeningPrice,
        ["ClosingPrice"] = value.ClosingPrice,
        ["OpenProfitLoss"] = value.OpenProfitLoss,
        ["MtmValuationRateToBaseCurrency"] =
            value.MtmValuationRateToBaseCurrency
    };

    private static string HeaderSha(CsvDocument document) =>
        Sha(JsonSerializer.Serialize(document.NormalizedHeaders
            .Order(StringComparer.Ordinal), CanonicalJson));

    private static string SemanticSha(IReadOnlyList<JsonObject> records) =>
        Sha(new JsonArray(records.Select(value => value.DeepClone()).ToArray())
            .ToJsonString(CanonicalJson));

    private static JsonObject ParseObject(string path, string blocker)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(path))?.AsObject()
                ?? throw new InvalidDataException(blocker);
        }
        catch (JsonException)
        {
            throw new InvalidDataException(blocker);
        }
    }

    private static JsonObject Object(JsonObject value, string name) =>
        value[name]?.AsObject()
        ?? throw new InvalidDataException($"ARCH7B_CORE_FIELD_MISSING:{name}");
    private static string Text(JsonObject value, string name) =>
        value[name]?.GetValue<string>()
        ?? throw new InvalidDataException($"ARCH7B_CORE_FIELD_MISSING:{name}");
    private static int Int32(JsonObject value, string name) =>
        value[name]?.GetValue<int>()
        ?? throw new InvalidDataException($"ARCH7B_CORE_FIELD_MISSING:{name}");
    private static bool Boolean(JsonObject value, string name) =>
        value[name]?.GetValue<bool>()
        ?? throw new InvalidDataException($"ARCH7B_CORE_FIELD_MISSING:{name}");
    private static DateTimeOffset? OptionalDate(JsonObject value, string name)
    {
        var node = value[name];
        if (node is null || node.GetValueKind() == JsonValueKind.Null) return null;
        return ParseTimestamp(node.GetValue<string>());
    }

    private static string Text(string? value) => (value ?? string.Empty).Trim();
    private static string NormalizeHeader(string value) =>
        Regex.Replace(Text(value), @"\s+", " ").ToLowerInvariant();
    private static string TrimTrailingCarriageReturn(string value) =>
        value.EndsWith('\r') ? value[..^1] : value;
    private static string Sha(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string FileSha(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Require(bool condition, string blocker)
    {
        if (!condition) throw new InvalidDataException(blocker);
    }

    private sealed record CsvDocument(
        string[] Headers,
        string[] NormalizedHeaders,
        IReadOnlyList<string[]> Rows);

    private sealed record ExecutionRecord(
        string AccountId,
        string ExecutionId,
        string MtfExecutionId,
        string OrderId,
        string InstructionId,
        string TradeUti,
        DateTimeOffset ExecutionTimestampUtc,
        string TradeDate,
        string InstrumentId,
        string Symbol,
        string Side,
        string Quantity,
        string UnitsBoughtSold,
        string Price,
        string Commission,
        string Notional);

    private sealed record PositionRecord(
        string AccountId,
        string PositionIdentity,
        string RawSymbol,
        string MappedSymbol,
        string Currency,
        string Side,
        string Quantity,
        string MarginOnOpenPosition,
        string AverageOpeningPrice,
        string ClosingPrice,
        string OpenProfitLoss,
        string MtmValuationRateToBaseCurrency);
}
