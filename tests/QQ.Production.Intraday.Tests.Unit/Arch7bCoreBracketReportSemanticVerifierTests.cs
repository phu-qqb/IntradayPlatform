using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bCoreBracketReportSemanticVerifierTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Successful_attempt_is_discovered_dynamically(int attemptCount)
    {
        using var fixture = new SemanticFixture(attemptCount);
        Assert.Equal(attemptCount, fixture.Verify().SuccessfulAttemptNumber);
    }

    [Fact]
    public void Missing_attempt_number_is_rejected()
    {
        using var fixture = new SemanticFixture(2);
        fixture.SetAttemptNumber(2, 3);
        fixture.AssertCode("ARCH7B_CORE_ATTEMPT_NUMBER_SEQUENCE_INVALID");
    }

    [Fact]
    public void Duplicate_attempt_number_is_rejected()
    {
        using var fixture = new SemanticFixture(2);
        fixture.SetAttemptNumber(2, 1);
        fixture.AssertCode("ARCH7B_CORE_ATTEMPT_NUMBER_SEQUENCE_INVALID");
    }

    [Fact]
    public void Stable_attempt_must_be_terminal()
    {
        using var fixture = new SemanticFixture(2);
        fixture.SetStable(1, true);
        fixture.AssertCode("ARCH7B_CORE_STABLE_ATTEMPT_NOT_TERMINAL");
    }

    [Fact]
    public void Last_attempt_must_be_stable()
    {
        using var fixture = new SemanticFixture(2);
        fixture.SetStable(2, false);
        fixture.AssertCode("ARCH7B_CORE_FINAL_ATTEMPT_UNSTABLE");
    }

    [Fact]
    public void Invalid_successful_attempt_header_is_rejected()
    {
        using var fixture = new SemanticFixture(2);
        fixture.WriteReport(2, "T2", "Wrong,Header\n");
        fixture.AssertCode(
            "ARCH7B_LMAX_INDIVIDUAL_TRADES_HEADER_CONTRACT_MISMATCH");
    }

    [Fact]
    public void Declared_zero_position_with_real_row_is_rejected()
    {
        using var fixture = new SemanticFixture();
        fixture.WriteReport(1, "P2", fixture.PositionCsv(Position()));
        fixture.AssertCode("ARCH7B_CORE_POSITION_REPORT_SEMANTIC_MISMATCH");
    }

    [Fact]
    public void Declared_nonzero_position_with_empty_csv_is_rejected()
    {
        using var fixture = new SemanticFixture();
        fixture.Contract["PositionCount"] = 1;
        fixture.AssertCode("ARCH7B_CORE_POSITION_REPORT_SEMANTIC_MISMATCH");
    }

    [Fact]
    public void Execution_semantic_sha_mismatch_is_rejected()
    {
        using var fixture = new SemanticFixture();
        fixture.SetReportField(1, "T2", "SemanticSha256", Hash('9'));
        fixture.AssertCode("ARCH7B_CORE_EXECUTION_REPORT_SEMANTIC_MISMATCH");
    }

    [Fact]
    public void Position_semantic_sha_mismatch_is_rejected()
    {
        using var fixture = new SemanticFixture();
        fixture.SetReportField(1, "P2", "SemanticSha256", Hash('9'));
        fixture.AssertCode("ARCH7B_CORE_POSITION_REPORT_SEMANTIC_MISMATCH");
    }

    [Fact]
    public void Header_sha_mismatch_is_rejected()
    {
        using var fixture = new SemanticFixture();
        fixture.SetReportField(1, "T2", "HeaderSetSha256", Hash('9'));
        fixture.AssertCode("ARCH7B_CORE_EXECUTION_REPORT_SEMANTIC_MISMATCH");
    }

    [Fact]
    public void Account_mismatch_in_real_row_is_rejected()
    {
        using var fixture = new SemanticFixture();
        fixture.WriteReport(1, "T2",
            fixture.ExecutionCsv(Execution() with { AccountId = "wrong" }));
        fixture.AssertCode("ARCH7B_LMAX_REPORT_ACCOUNT_MISMATCH");
    }

    [Fact]
    public void Conflicting_duplicate_execution_id_is_rejected()
    {
        using var fixture = new SemanticFixture();
        fixture.WriteReport(1, "T2", fixture.ExecutionCsv(
            Execution(), Execution() with { Price = "2" }));
        fixture.AssertCode("ARCH7B_LMAX_EXECUTION_ID_CONFLICT");
    }

    [Fact]
    public void Duplicate_position_uti_is_rejected()
    {
        using var fixture = new SemanticFixture();
        fixture.WriteReport(1, "P2",
            fixture.PositionCsv(Position(), Position()));
        fixture.AssertCode("ARCH7B_LMAX_POSITION_IDENTITY_DUPLICATE");
    }

    [Fact]
    public void Invalid_decimal_is_rejected()
    {
        using var fixture = new SemanticFixture();
        fixture.WriteReport(1, "T2",
            fixture.ExecutionCsv(Execution() with { Price = "not-decimal" }));
        fixture.AssertCode("ARCH7B_LMAX_REPORT_DECIMAL_INVALID");
    }

    [Fact]
    public void Timestamp_without_explicit_offset_is_rejected()
    {
        using var fixture = new SemanticFixture();
        fixture.WriteReport(1, "T2", fixture.ExecutionCsv(
            Execution() with { Timestamp = "2026-07-27T11:23:45" }));
        fixture.AssertCode("ARCH7B_LMAX_REPORT_TIMESTAMP_INVALID");
    }

    private static ExecutionRow Execution() => new(
        "exec-1", "mtf-1", "2026-07-27T11:23:45Z", "1", "1.2500",
        "2026-07-27", "4001", "EURUSD", "instruction-1", "order-1",
        "", "", "", "BUY", "", "", "", "0",
        Arch7bBracketedGlobalFlatContract.AccountId, "1", "1.25", "trade-uti-1");

    private static PositionRow Position() => new(
        "EUR/USD", "USD", "1", "1", "1.2", "1.3", "0.1", "1",
        "EURUSD", Arch7bBracketedGlobalFlatContract.AccountId, "position-uti-1");

    private static string Hash(char value) => new(value, 64);

    private sealed class SemanticFixture : IDisposable
    {
        private static readonly string[] ExecutionHeaders =
        [
            "Execution ID", "Mtf Execution ID", "Timestamp", "Trade Quantity",
            "Trade Price", "Trade Date", "Instrument ID", "Symbol", "Instruction ID",
            "Order ID", "Stop Price", "Limit Price", "Order Placement Timestamp",
            "Type", "Remote Venue", "User Placing Order", "Total Profit Loss",
            "Total Commission", "Account Id", "Units Bought/Sold", "Notional Value",
            "Trade UTI"
        ];

        private static readonly string[] PositionHeaders =
        [
            "Instrument", "CCY", "Open Quantity", "Margin on Open Position",
            "Average Opening Price", "Closing Price", "Open Profit / Loss",
            "MTM Valuation Rate to Base CCY", "LMAX Symbol", "Account Id",
            "Position UTI"
        ];

        private const string EmptySemanticSha =
            "4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945";

        public SemanticFixture(int attemptCount = 1)
        {
            Root = Path.Combine(Path.GetTempPath(),
                "arch7b-semantic-verifier-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            IndexedFiles = new HashSet<string>(StringComparer.Ordinal);
            var attempts = new JsonArray();
            for (var number = 1; number <= attemptCount; number++)
            {
                var directory = Path.Combine(Root, $"attempt-{number}");
                Directory.CreateDirectory(directory);
                foreach (var label in new[] { "T0", "T1", "T2" })
                    WriteInitial(number, label, "individual-trades",
                        string.Join(',', ExecutionHeaders) + "\n",
                        Arch7bBracketedGlobalFlatContract.ExecutionHeaderSetSha256);
                foreach (var label in new[] { "P1", "P2" })
                    WriteInitial(number, label, "open-positions",
                        string.Join(',', PositionHeaders) + "\n",
                        Arch7bBracketedGlobalFlatContract.PositionHeaderSetSha256);
                var attempt = new JsonObject
                {
                    ["Attempt"] = number,
                    ["T0"] = Report(number, "T0", "individual-trades",
                        Arch7bBracketedGlobalFlatContract.ExecutionHeaderSetSha256),
                    ["P1"] = Report(number, "P1", "open-positions",
                        Arch7bBracketedGlobalFlatContract.PositionHeaderSetSha256),
                    ["T1"] = Report(number, "T1", "individual-trades",
                        Arch7bBracketedGlobalFlatContract.ExecutionHeaderSetSha256),
                    ["P2"] = Report(number, "P2", "open-positions",
                        Arch7bBracketedGlobalFlatContract.PositionHeaderSetSha256),
                    ["T2"] = Report(number, "T2", "individual-trades",
                        Arch7bBracketedGlobalFlatContract.ExecutionHeaderSetSha256),
                    ["Stable"] = number == attemptCount
                };
                attempts.Add(attempt);
                WriteManifest(number, attempt);
            }
            Contract = new JsonObject
            {
                ["Attempts"] = attempts,
                ["ExecutionCount"] = 0,
                ["DuplicateIdenticalExecutionCount"] = 0,
                ["LatestExecutionTime"] = null,
                ["PositionCount"] = 0
            };
        }

        public string Root { get; }
        public JsonObject Contract { get; }
        public HashSet<string> IndexedFiles { get; }

        public Arch7bCoreBracketReportSemanticVerification Verify() =>
            Arch7bCoreBracketReportSemanticVerifier.Verify(
                Root, Contract, IndexedFiles);

        public void AssertCode(string expected) =>
            Assert.Equal(expected,
                Assert.Throws<InvalidDataException>(() => Verify()).Message);

        public void SetAttemptNumber(int attempt, int value)
        {
            Attempt(attempt)["Attempt"] = value;
            WriteManifest(attempt, Attempt(attempt));
        }

        public void SetStable(int attempt, bool value)
        {
            Attempt(attempt)["Stable"] = value;
            WriteManifest(attempt, Attempt(attempt));
        }

        public void SetReportField(
            int attempt,
            string label,
            string field,
            string value)
        {
            Attempt(attempt)[label]![field] = value;
            WriteManifest(attempt, Attempt(attempt));
        }

        public void WriteReport(int attempt, string label, string content)
        {
            var kind = label[0] == 'T' ? "individual-trades" : "open-positions";
            File.WriteAllText(Path.Combine(Root, $"attempt-{attempt}",
                $"{label}-{kind}.csv"), content, new UTF8Encoding(false));
        }

        public string ExecutionCsv(params ExecutionRow[] rows) =>
            Csv(ExecutionHeaders, rows.Select(value => new[]
            {
                value.ExecutionId, value.MtfExecutionId, value.Timestamp,
                value.Quantity, value.Price, value.TradeDate, value.InstrumentId,
                value.Symbol, value.InstructionId, value.OrderId, value.StopPrice,
                value.LimitPrice, value.OrderPlacementTimestamp, value.Side,
                value.RemoteVenue, value.UserPlacingOrder, value.TotalProfitLoss,
                value.Commission, value.AccountId, value.UnitsBoughtSold,
                value.Notional, value.TradeUti
            }));

        public string PositionCsv(params PositionRow[] rows) =>
            Csv(PositionHeaders, rows.Select(value => new[]
            {
                value.Instrument, value.Currency, value.Quantity, value.Margin,
                value.AveragePrice, value.ClosingPrice, value.ProfitLoss,
                value.MtmRate, value.LmaxSymbol, value.AccountId, value.PositionUti
            }));

        private JsonObject Attempt(int attempt) =>
            Contract["Attempts"]!.AsArray()[attempt - 1]!.AsObject();

        private void WriteInitial(
            int attempt,
            string label,
            string kind,
            string content,
            string headerSha)
        {
            var relative = $"attempt-{attempt}/{label}-{kind}.csv";
            File.WriteAllText(Path.Combine(Root,
                relative.Replace('/', Path.DirectorySeparatorChar)),
                content, new UTF8Encoding(false));
            IndexedFiles.Add(relative);
        }

        private JsonObject Report(
            int attempt,
            string label,
            string kind,
            string headerSha)
        {
            var path = Path.Combine(Root, $"attempt-{attempt}", $"{label}-{kind}.csv");
            return new JsonObject
            {
                ["RawSha256"] = FileHash(path),
                ["SemanticSha256"] = EmptySemanticSha,
                ["HeaderSetSha256"] = headerSha,
                ["RowCount"] = 0
            };
        }

        private void WriteManifest(int attempt, JsonObject value)
        {
            var relative = $"attempt-{attempt}/attempt-manifest.json";
            File.WriteAllText(Path.Combine(Root,
                    relative.Replace('/', Path.DirectorySeparatorChar)),
                value.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
            IndexedFiles.Add(relative);
        }

        private static string Csv(
            IReadOnlyList<string> headers,
            IEnumerable<IReadOnlyList<string>> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine(string.Join(',', headers.Select(Escape)));
            foreach (var row in rows)
                builder.AppendLine(string.Join(',', row.Select(Escape)));
            return builder.ToString();
        }

        private static string Escape(string value) =>
            value.IndexOfAny([',', '"', '\r', '\n']) < 0
                ? value
                : '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';

        private static string FileHash(string path) =>
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }

    private sealed record ExecutionRow(
        string ExecutionId,
        string MtfExecutionId,
        string Timestamp,
        string Quantity,
        string Price,
        string TradeDate,
        string InstrumentId,
        string Symbol,
        string InstructionId,
        string OrderId,
        string StopPrice,
        string LimitPrice,
        string OrderPlacementTimestamp,
        string Side,
        string RemoteVenue,
        string UserPlacingOrder,
        string TotalProfitLoss,
        string Commission,
        string AccountId,
        string UnitsBoughtSold,
        string Notional,
        string TradeUti);

    private sealed record PositionRow(
        string Instrument,
        string Currency,
        string Quantity,
        string Margin,
        string AveragePrice,
        string ClosingPrice,
        string ProfitLoss,
        string MtmRate,
        string LmaxSymbol,
        string AccountId,
        string PositionUti);
}
