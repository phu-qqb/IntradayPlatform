using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public sealed class InMemoryLmaxEodReportRepository(PlatformState state) : ILmaxEodReportRepository
{
    private readonly object _sync = new();

    public Task AddImportRunAsync(LmaxReportImportRun run, CancellationToken cancellationToken)
    {
        lock (_sync) state.LmaxReportImportRuns.Add(run);
        return Task.CompletedTask;
    }

    public Task UpdateImportRunAsync(LmaxReportImportRun run, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var index = state.LmaxReportImportRuns.FindIndex(x => x.Id == run.Id);
            if (index >= 0) state.LmaxReportImportRuns[index] = run;
        }

        return Task.CompletedTask;
    }

    public Task AddValidationIssuesAsync(IReadOnlyList<LmaxReportValidationIssue> issues, CancellationToken cancellationToken)
    {
        lock (_sync) state.LmaxReportValidationIssues.AddRange(issues);
        return Task.CompletedTask;
    }

    public Task AddIndividualTradesAsync(IReadOnlyList<LmaxIndividualTrade> trades, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            foreach (var trade in trades.Where(trade => state.LmaxIndividualTrades.All(x => x.VenueId != trade.VenueId || x.AccountId != trade.AccountId || x.ExecutionId != trade.ExecutionId)))
            {
                state.LmaxIndividualTrades.Add(trade);
            }
        }

        return Task.CompletedTask;
    }

    public Task AddTradeSummariesAsync(IReadOnlyList<LmaxTradeSummary> summaries, CancellationToken cancellationToken)
    {
        lock (_sync) state.LmaxTradeSummaries.AddRange(summaries);
        return Task.CompletedTask;
    }

    public Task AddCurrencyWalletsAsync(IReadOnlyList<LmaxCurrencyWallet> wallets, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            foreach (var wallet in wallets)
            {
                var index = state.LmaxCurrencyWallets.FindIndex(x => x.ReportDate == wallet.ReportDate && x.VenueId == wallet.VenueId && x.BrokerAccountId == wallet.BrokerAccountId && x.Currency == wallet.Currency);
                if (index >= 0) state.LmaxCurrencyWallets[index] = wallet with { Id = state.LmaxCurrencyWallets[index].Id };
                else state.LmaxCurrencyWallets.Add(wallet);
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LmaxReportImportRun>> GetImportRunsAsync(int limit, DateOnly? reportDate, LmaxReportType? reportType, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var query = state.LmaxReportImportRuns.AsEnumerable();
            if (reportDate is not null) query = query.Where(x => x.ReportDate == reportDate);
            if (reportType is not null) query = query.Where(x => x.ReportType == reportType);
            return Task.FromResult((IReadOnlyList<LmaxReportImportRun>)query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(limit, 1, 500)).ToList());
        }
    }

    public Task<IReadOnlyList<LmaxReportValidationIssue>> GetValidationIssuesAsync(int limit, LmaxReportImportRunId? importRunId, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var query = state.LmaxReportValidationIssues.AsEnumerable();
            if (importRunId is not null) query = query.Where(x => x.ImportRunId == importRunId);
            return Task.FromResult((IReadOnlyList<LmaxReportValidationIssue>)query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(limit, 1, 500)).ToList());
        }
    }

    public Task<IReadOnlyList<LmaxIndividualTrade>> GetIndividualTradesAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var query = state.LmaxIndividualTrades.AsEnumerable();
            if (reportDate is not null) query = query.Where(x => x.ReportDate == reportDate);
            return Task.FromResult((IReadOnlyList<LmaxIndividualTrade>)query.OrderByDescending(x => x.TimestampUtc).Take(Math.Clamp(limit, 1, 500)).ToList());
        }
    }

    public Task<IReadOnlyList<LmaxTradeSummary>> GetTradeSummariesAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var query = state.LmaxTradeSummaries.AsEnumerable();
            if (reportDate is not null) query = query.Where(x => x.ReportDate == reportDate);
            return Task.FromResult((IReadOnlyList<LmaxTradeSummary>)query.OrderByDescending(x => x.DateTimeUtc).Take(Math.Clamp(limit, 1, 500)).ToList());
        }
    }

    public Task<IReadOnlyList<LmaxCurrencyWallet>> GetCurrencyWalletsAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var query = state.LmaxCurrencyWallets.AsEnumerable();
            if (reportDate is not null) query = query.Where(x => x.ReportDate == reportDate);
            return Task.FromResult((IReadOnlyList<LmaxCurrencyWallet>)query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(limit, 1, 500)).ToList());
        }
    }

    public Task AddEodReconciliationAsync(EodReconciliationRun run, IReadOnlyList<EodReconciliationBreak> breaks, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            state.EodReconciliationRuns.Add(run);
            state.EodReconciliationBreaks.AddRange(breaks);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EodReconciliationRun>> GetEodReconciliationRunsAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var query = state.EodReconciliationRuns.AsEnumerable();
            if (reportDate is not null) query = query.Where(x => x.ReportDate == reportDate);
            return Task.FromResult((IReadOnlyList<EodReconciliationRun>)query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(limit, 1, 500)).ToList());
        }
    }

    public Task<IReadOnlyList<EodReconciliationBreak>> GetEodReconciliationBreaksAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            var runIds = reportDate is null ? null : state.EodReconciliationRuns.Where(x => x.ReportDate == reportDate).Select(x => x.Id).ToHashSet();
            var query = runIds is null ? state.EodReconciliationBreaks : state.EodReconciliationBreaks.Where(x => runIds.Contains(x.RunId));
            return Task.FromResult((IReadOnlyList<EodReconciliationBreak>)query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(limit, 1, 500)).ToList());
        }
    }
}

public sealed class LmaxEodReportOptions
{
    public string TimestampTimeZone { get; set; } = "UTC";
    public decimal WalletBalanceTolerance { get; set; } = 0.01m;
    public decimal SummaryTolerance { get; set; } = 0.01m;
    public string DataRoot { get; set; } = "data/lmax-eod";
}

public static class LmaxEodHeaders
{
    public static readonly string[] Individual = ["Execution ID", "Mtf Execution ID", "Timestamp", "Trade Quantity", "Trade Price", "Trade Date", "Instrument ID", "Symbol", "Instruction ID", "Order ID", "Stop Price", "Limit Price", "Order Placement Timestamp", "Type", "Remote Venue", "User Placing Order", "Total Profit Loss", "Total Commission", "Account Id", "Units Bought/Sold", "Notional Value", "Trade UTI"];
    public static readonly string[] Summary = ["Date & Time", "Instrument", "Type", "Currency", "Contracts", "Average Price", "Commission", "Notional Value", "LMAX Symbol", "User Placing Order", "Commission (full precision)", "Account Id"];
    public static readonly string[] Wallet = ["CCY", "Balance + Net Deposits", "Adjustments", "Inter Account Transfers", "Profit & Loss", "Commission", "Dividends", "Financing", "Wallet Balance", "Rate to Base CCY", "Account Id"];
}

public sealed class LmaxEodReportImportService(
    IIntradayRepository intradayRepository,
    ILmaxEodReportRepository eodRepository,
    ILmaxReportPairConsistencyService consistencyService,
    IClock clock,
    LmaxEodReportOptions options) : ILmaxEodReportImportService
{
    public Task<LmaxReportImportResult> ImportIndividualTradesAsync(string filePath, DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken)
        => ImportAsync(filePath, reportDate, venueName, brokerAccountCode, LmaxReportType.IndividualTrades, LmaxEodHeaders.Individual, cancellationToken);

    public Task<LmaxReportImportResult> ImportTradesSummaryAsync(string filePath, DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken)
        => ImportAsync(filePath, reportDate, venueName, brokerAccountCode, LmaxReportType.TradesSummary, LmaxEodHeaders.Summary, cancellationToken);

    public Task<LmaxReportImportResult> ImportCurrencyWalletsAsync(string filePath, DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken)
        => ImportAsync(filePath, reportDate, venueName, brokerAccountCode, LmaxReportType.CurrencyWallets, LmaxEodHeaders.Wallet, cancellationToken);

    public async Task<LmaxReportImportResult> ImportReportSetAsync(string individualTradesPath, string tradesSummaryPath, string currencyWalletsPath, DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken)
    {
        var individual = await ImportIndividualTradesAsync(individualTradesPath, reportDate, venueName, brokerAccountCode, cancellationToken);
        var summary = await ImportTradesSummaryAsync(tradesSummaryPath, reportDate, venueName, brokerAccountCode, cancellationToken);
        var wallet = await ImportCurrencyWalletsAsync(currencyWalletsPath, reportDate, venueName, brokerAccountCode, cancellationToken);
        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        var venue = state.Venues.Single(x => x.Name.Equals(venueName, StringComparison.OrdinalIgnoreCase));
        var account = state.BrokerAccounts.Single(x => x.AccountCode.Equals(brokerAccountCode, StringComparison.OrdinalIgnoreCase));
        var run = NewRun(LmaxReportType.ReportSet, reportDate, venue.Id, account.Id, "report-set", null, null);
        await eodRepository.AddImportRunAsync(run, cancellationToken);
        var issues = individual.Issues.Concat(summary.Issues).Concat(wallet.Issues).ToList();
        if (individual.BlockingIssueCount == 0 && summary.BlockingIssueCount == 0)
        {
            issues.AddRange(await consistencyService.CheckAsync(run.Id, reportDate, venue.Id, account.Id, cancellationToken));
        }

        var status = issues.Any(x => x.Severity == LmaxReportValidationSeverity.Blocking) ? LmaxReportImportStatus.Rejected : LmaxReportImportStatus.Imported;
        var runIssues = issues.Select(x => x with { Id = Guid.NewGuid(), ImportRunId = run.Id }).ToList();
        await eodRepository.AddValidationIssuesAsync(runIssues, cancellationToken);
        await eodRepository.UpdateImportRunAsync(run with { Status = status, RowCount = individual.RowCount + summary.RowCount + wallet.RowCount, CompletedAtUtc = clock.UtcNow, Message = status == LmaxReportImportStatus.Imported ? "Report set imported." : "Report set rejected by validation." }, cancellationToken);
        return new LmaxReportImportResult(run.Id, status, individual.RowCount + summary.RowCount + wallet.RowCount, runIssues.Count(x => x.Severity == LmaxReportValidationSeverity.Blocking), runIssues, status.ToString());
    }

    private async Task<LmaxReportImportResult> ImportAsync(string filePath, DateOnly reportDate, string venueName, string brokerAccountCode, LmaxReportType type, IReadOnlyList<string> expectedHeaders, CancellationToken cancellationToken)
    {
        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        var venue = state.Venues.Single(x => x.Name.Equals(venueName, StringComparison.OrdinalIgnoreCase));
        var account = state.BrokerAccounts.Single(x => x.AccountCode.Equals(brokerAccountCode, StringComparison.OrdinalIgnoreCase));
        var fullPath = ValidateLocalPath(filePath);
        var hash = File.Exists(fullPath) ? Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(fullPath, cancellationToken))) : null;
        var run = NewRun(type, reportDate, venue.Id, account.Id, Path.GetFileName(fullPath), fullPath, hash);
        await eodRepository.AddImportRunAsync(run, cancellationToken);
        var issues = new List<LmaxReportValidationIssue>();
        var lines = File.Exists(fullPath) ? (await File.ReadAllLinesAsync(fullPath, cancellationToken)).ToList() : [];
        if (!File.Exists(fullPath)) issues.Add(Issue(run.Id, LmaxReportValidationIssueType.MissingFile, LmaxReportValidationSeverity.Blocking, "Report file is missing.", null, null));
        if (lines.Count == 0) issues.Add(Issue(run.Id, LmaxReportValidationIssueType.InvalidHeader, LmaxReportValidationSeverity.Blocking, "Report file is empty.", null, null));
        else if (!SplitCsv(lines[0]).SequenceEqual(expectedHeaders, StringComparer.Ordinal)) issues.Add(Issue(run.Id, LmaxReportValidationIssueType.InvalidHeader, LmaxReportValidationSeverity.Blocking, "Header does not match the actual LMAX report schema.", 1, lines[0]));

        var rows = lines.Skip(1).Where(x => !string.IsNullOrWhiteSpace(x)).Select(SplitCsv).ToList();
        if (!issues.Any(x => x.Severity == LmaxReportValidationSeverity.Blocking))
        {
            if (type == LmaxReportType.IndividualTrades) await eodRepository.AddIndividualTradesAsync(ParseIndividual(rows, lines.Skip(1).ToArray(), run, venue, account, state, issues), cancellationToken);
            if (type == LmaxReportType.TradesSummary) await eodRepository.AddTradeSummariesAsync(ParseSummary(rows, lines.Skip(1).ToArray(), run, venue, account, state, issues), cancellationToken);
            if (type == LmaxReportType.CurrencyWallets) await eodRepository.AddCurrencyWalletsAsync(ParseWallets(rows, lines.Skip(1).ToArray(), run, venue, account, state, issues), cancellationToken);
        }

        var status = issues.Any(x => x.Severity == LmaxReportValidationSeverity.Blocking) ? LmaxReportImportStatus.Rejected : LmaxReportImportStatus.Imported;
        await eodRepository.AddValidationIssuesAsync(issues, cancellationToken);
        await eodRepository.UpdateImportRunAsync(run with { Status = status, RowCount = rows.Count, CompletedAtUtc = clock.UtcNow, Message = status == LmaxReportImportStatus.Imported ? "Imported." : "Rejected by validation." }, cancellationToken);
        return new LmaxReportImportResult(run.Id, status, rows.Count, issues.Count(x => x.Severity == LmaxReportValidationSeverity.Blocking), issues, status.ToString());
    }

    private List<LmaxIndividualTrade> ParseIndividual(List<string[]> rows, string[] raw, LmaxReportImportRun run, Venue venue, BrokerAccount account, PlatformState state, List<LmaxReportValidationIssue> issues)
    {
        var trades = new List<LmaxIndividualTrade>();
        var executionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var utis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNumber = i + 2;
            if (row.Length != LmaxEodHeaders.Individual.Length) { issues.Add(Issue(run.Id, LmaxReportValidationIssueType.InvalidRow, LmaxReportValidationSeverity.Blocking, "Invalid column count.", rowNumber, raw.ElementAtOrDefault(i))); continue; }
            var executionId = row[0].Trim();
            var tradeUti = row[21].Trim();
            if (!executionIds.Add(executionId)) issues.Add(Issue(run.Id, LmaxReportValidationIssueType.DuplicateExecutionId, LmaxReportValidationSeverity.Blocking, $"Duplicate Execution ID '{executionId}'.", rowNumber, raw.ElementAtOrDefault(i)));
            if (!utis.Add(tradeUti)) issues.Add(Issue(run.Id, LmaxReportValidationIssueType.DuplicateTradeUti, LmaxReportValidationSeverity.Blocking, $"Duplicate Trade UTI '{tradeUti}'.", rowNumber, raw.ElementAtOrDefault(i)));
            var instrumentId = ResolveInstrument(state, row[7], row[6], run.Id, rowNumber, raw.ElementAtOrDefault(i), issues);
            ValidateAccount(account, row[18], run.Id, rowNumber, raw.ElementAtOrDefault(i), issues);
            var timestamp = ParseTimestamp(row[2], "dd-MM-yyyy HH:mm:ss.fff", LmaxReportValidationIssueType.InvalidTimestamp, run.Id, rowNumber, raw.ElementAtOrDefault(i), issues);
            var tradeDate = DateOnly.TryParseExact(row[5], "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate) ? parsedDate : default;
            if (tradeDate == default) issues.Add(Issue(run.Id, LmaxReportValidationIssueType.InvalidDate, LmaxReportValidationSeverity.Blocking, $"Invalid Trade Date '{row[5]}'.", rowNumber, raw.ElementAtOrDefault(i)));
            var quantity = ParseDecimal(row[3], LmaxReportValidationIssueType.InvalidQuantity, run.Id, rowNumber, raw.ElementAtOrDefault(i), issues);
            var price = ParseDecimal(row[4], LmaxReportValidationIssueType.InvalidPrice, run.Id, rowNumber, raw.ElementAtOrDefault(i), issues);
            var commission = ParseDecimal(row[17], LmaxReportValidationIssueType.InvalidCommission, run.Id, rowNumber, raw.ElementAtOrDefault(i), issues);
            var units = ParseDecimal(row[19], LmaxReportValidationIssueType.InvalidQuantity, run.Id, rowNumber, raw.ElementAtOrDefault(i), issues);
            var notional = ParseDecimal(row[20], LmaxReportValidationIssueType.InvalidNotional, run.Id, rowNumber, raw.ElementAtOrDefault(i), issues);
            trades.Add(new LmaxIndividualTrade(LmaxIndividualTradeId.New(), run.Id, run.ReportDate, venue.Id, account.Id, executionId, Empty(row[1]), timestamp, quantity, price, tradeDate, Empty(row[6]), row[7], instrumentId, Empty(row[8]), Empty(row[9]), TryOptionalDecimal(row[10]), TryOptionalDecimal(row[11]), string.IsNullOrWhiteSpace(row[12]) ? null : ParseTimestamp(row[12], "dd-MM-yyyy HH:mm:ss.fff", LmaxReportValidationIssueType.InvalidTimestamp, run.Id, rowNumber, raw.ElementAtOrDefault(i), issues), row[13], Empty(row[14]), Empty(row[15]), TryOptionalDecimal(row[16]), commission, row[18].Trim(), units, notional, tradeUti, raw.ElementAtOrDefault(i), clock.UtcNow));
        }

        return trades;
    }

    private List<LmaxTradeSummary> ParseSummary(List<string[]> rows, string[] raw, LmaxReportImportRun run, Venue venue, BrokerAccount account, PlatformState state, List<LmaxReportValidationIssue> issues)
    {
        var summaries = new List<LmaxTradeSummary>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNumber = i + 2;
            if (row.Length != LmaxEodHeaders.Summary.Length) { issues.Add(Issue(run.Id, LmaxReportValidationIssueType.InvalidRow, LmaxReportValidationSeverity.Blocking, "Invalid column count.", rowNumber, raw.ElementAtOrDefault(i))); continue; }
            ValidateAccount(account, row[11], run.Id, rowNumber, raw.ElementAtOrDefault(i), issues);
            summaries.Add(new LmaxTradeSummary(LmaxTradeSummaryId.New(), run.Id, run.ReportDate, venue.Id, account.Id, ParseTimestamp(row[0], "M/d/yyyy HH:mm", LmaxReportValidationIssueType.InvalidTimestamp, run.Id, rowNumber, raw.ElementAtOrDefault(i), issues), row[1], ResolveInstrument(state, row[8], null, run.Id, rowNumber, raw.ElementAtOrDefault(i), issues), row[2], row[3], ParseDecimal(row[4], LmaxReportValidationIssueType.InvalidQuantity, run.Id, rowNumber, raw.ElementAtOrDefault(i), issues), ParseDecimal(row[5], LmaxReportValidationIssueType.InvalidPrice, run.Id, rowNumber, raw.ElementAtOrDefault(i), issues), ParseDecimal(row[6], LmaxReportValidationIssueType.InvalidCommission, run.Id, rowNumber, raw.ElementAtOrDefault(i), issues), ParseDecimal(row[7], LmaxReportValidationIssueType.InvalidNotional, run.Id, rowNumber, raw.ElementAtOrDefault(i), issues), row[8], Empty(row[9]), ParseDecimal(row[10], LmaxReportValidationIssueType.InvalidCommission, run.Id, rowNumber, raw.ElementAtOrDefault(i), issues), row[11].Trim(), raw.ElementAtOrDefault(i), clock.UtcNow));
        }

        return summaries;
    }

    private List<LmaxCurrencyWallet> ParseWallets(List<string[]> rows, string[] raw, LmaxReportImportRun run, Venue venue, BrokerAccount account, PlatformState state, List<LmaxReportValidationIssue> issues)
    {
        var wallets = new List<LmaxCurrencyWallet>();
        var currencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNumber = i + 2;
            if (row.Length != LmaxEodHeaders.Wallet.Length) { issues.Add(Issue(run.Id, LmaxReportValidationIssueType.InvalidRow, LmaxReportValidationSeverity.Blocking, "Invalid column count.", rowNumber, raw.ElementAtOrDefault(i))); continue; }
            var ccy = row[0].Trim().ToUpperInvariant();
            if (!currencies.Add(ccy)) issues.Add(Issue(run.Id, LmaxReportValidationIssueType.DuplicateCurrencyWallet, LmaxReportValidationSeverity.Blocking, $"Duplicate currency wallet '{ccy}'.", rowNumber, raw.ElementAtOrDefault(i)));
            ValidateAccount(account, row[10], run.Id, rowNumber, raw.ElementAtOrDefault(i), issues);
            var values = Enumerable.Range(1, 9).Select(column => ParseDecimal(row[column], column == 9 ? LmaxReportValidationIssueType.InvalidRateToBaseCcy : LmaxReportValidationIssueType.InvalidRow, run.Id, rowNumber, raw.ElementAtOrDefault(i), issues)).ToArray();
            if (values[8] <= 0) issues.Add(Issue(run.Id, LmaxReportValidationIssueType.InvalidRateToBaseCcy, LmaxReportValidationSeverity.Blocking, "Rate to Base CCY must be positive.", rowNumber, raw.ElementAtOrDefault(i)));
            if (ccy == "USD" && Math.Abs(values[8] - 1m) > options.WalletBalanceTolerance) issues.Add(Issue(run.Id, LmaxReportValidationIssueType.InvalidRateToBaseCcy, LmaxReportValidationSeverity.Warning, "USD Rate to Base CCY is not 1.0.", rowNumber, raw.ElementAtOrDefault(i)));
            var expected = values[0] + values[1] + values[2] + values[3] + values[4] + values[5] + values[6];
            if (Math.Abs(expected - values[7]) > options.WalletBalanceTolerance) issues.Add(Issue(run.Id, LmaxReportValidationIssueType.WalletBalanceMismatch, LmaxReportValidationSeverity.Blocking, $"Wallet balance {values[7]} does not match component sum {expected}.", rowNumber, raw.ElementAtOrDefault(i)));
            var rate = values[8];
            wallets.Add(new LmaxCurrencyWallet(LmaxCurrencyWalletId.New(), run.Id, run.ReportDate, venue.Id, account.Id, ccy, values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], rate, "USD", values[0] * rate, values[1] * rate, values[2] * rate, values[3] * rate, values[4] * rate, values[5] * rate, values[6] * rate, values[7] * rate, row[10].Trim(), raw.ElementAtOrDefault(i), clock.UtcNow));
        }

        return wallets;
    }

    private LmaxReportImportRun NewRun(LmaxReportType type, DateOnly reportDate, VenueId venueId, BrokerAccountId accountId, string? fileName, string? filePath, string? fileHash)
        => new(LmaxReportImportRunId.New(), type, reportDate, venueId, accountId, LmaxReportImportStatus.Validating, fileName, filePath, fileHash, null, clock.UtcNow, clock.UtcNow, null, null, null, null);

    private InstrumentId? ResolveInstrument(PlatformState state, string symbol, string? instrumentId, LmaxReportImportRunId runId, int rowNumber, string? raw, List<LmaxReportValidationIssue> issues)
    {
        var alias = state.InstrumentAliases.FirstOrDefault(x => x.IsEnabled && x.Source == "LMAX_REPORT" && (x.ExternalSymbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrWhiteSpace(instrumentId) && x.ExternalInstrumentId == instrumentId)));
        if (alias is null)
        {
            issues.Add(Issue(runId, LmaxReportValidationIssueType.UnknownInstrument, LmaxReportValidationSeverity.Blocking, $"Unknown LMAX symbol '{symbol}' / '{instrumentId}'.", rowNumber, raw));
            return null;
        }

        var instrument = state.Instruments.FirstOrDefault(x => x.Id == alias.InstrumentId);
        if (instrument is null)
        {
            issues.Add(Issue(runId, LmaxReportValidationIssueType.UnknownInstrument, LmaxReportValidationSeverity.Blocking, $"Instrument for alias '{symbol}' is missing.", rowNumber, raw));
        }
        else if (!instrument.IsEnabled)
        {
            issues.Add(Issue(runId, LmaxReportValidationIssueType.DisabledInstrument, LmaxReportValidationSeverity.Warning, $"Instrument for alias '{symbol}' is disabled for trading; EOD report import is allowed.", rowNumber, raw));
        }
        return alias.InstrumentId;
    }

    private void ValidateAccount(BrokerAccount account, string rowAccountId, LmaxReportImportRunId runId, int rowNumber, string? raw, List<LmaxReportValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(rowAccountId) || (!string.IsNullOrWhiteSpace(account.ExternalAccountId) && !rowAccountId.Trim().Equals(account.ExternalAccountId, StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(Issue(runId, LmaxReportValidationIssueType.AccountIdMismatch, LmaxReportValidationSeverity.Blocking, "Report Account Id does not match configured broker account.", rowNumber, raw));
        }
    }

    private string ValidateLocalPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || filePath.Contains("..", StringComparison.Ordinal)) throw new DomainRuleViolationException("Report path is invalid.");
        var fullPath = Path.GetFullPath(filePath);
        var root = Path.GetFullPath(options.DataRoot);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new DomainRuleViolationException($"Report path must be under '{root}'.");
        return fullPath;
    }

    private DateTimeOffset ParseTimestamp(string text, string format, LmaxReportValidationIssueType type, LmaxReportImportRunId runId, int rowNumber, string? raw, List<LmaxReportValidationIssue> issues)
    {
        if (DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified), TimeSpan.Zero);
        issues.Add(Issue(runId, type, LmaxReportValidationSeverity.Blocking, $"Invalid timestamp '{text}'.", rowNumber, raw));
        return DateTimeOffset.UnixEpoch;
    }

    private static decimal ParseDecimal(string text, LmaxReportValidationIssueType type, LmaxReportImportRunId runId, int rowNumber, string? raw, List<LmaxReportValidationIssue> issues)
    {
        if (decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)) return value;
        issues.Add(Issue(runId, type, LmaxReportValidationSeverity.Blocking, $"Invalid decimal '{text}'.", rowNumber, raw));
        return 0m;
    }

    private static decimal? TryOptionalDecimal(string text)
        => decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static string? Empty(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string[] SplitCsv(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (ch == ',' && !quoted) { values.Add(current.ToString().Trim()); current.Clear(); }
            else current.Append(ch);
        }

        values.Add(current.ToString().Trim());
        return values.ToArray();
    }

    private static LmaxReportValidationIssue Issue(LmaxReportImportRunId runId, LmaxReportValidationIssueType type, LmaxReportValidationSeverity severity, string message, int? rowNumber, string? raw)
        => new(Guid.NewGuid(), runId, type, severity, message, rowNumber, raw, DateTimeOffset.UtcNow);
}

public sealed class LmaxReportPairConsistencyService(ILmaxEodReportRepository repository, IClock clock, LmaxEodReportOptions options) : ILmaxReportPairConsistencyService
{
    public async Task<IReadOnlyList<LmaxReportValidationIssue>> CheckAsync(LmaxReportImportRunId importRunId, DateOnly reportDate, VenueId venueId, BrokerAccountId brokerAccountId, CancellationToken cancellationToken)
    {
        var individual = (await repository.GetIndividualTradesAsync(reportDate, 500, cancellationToken)).Where(x => x.VenueId == venueId && x.BrokerAccountId == brokerAccountId).ToList();
        var summary = (await repository.GetTradeSummariesAsync(reportDate, 500, cancellationToken)).Where(x => x.VenueId == venueId && x.BrokerAccountId == brokerAccountId).ToList();
        var issues = new List<LmaxReportValidationIssue>();
        AddMismatch(issues, importRunId, "total notional", individual.Sum(x => Math.Abs(x.NotionalValue)), summary.Sum(x => Math.Abs(x.NotionalValue)));
        AddMismatch(issues, importRunId, "total commission", individual.Sum(x => Math.Abs(x.TotalCommission)), summary.Sum(x => Math.Abs(x.CommissionFullPrecision)));
        foreach (var group in individual.GroupBy(x => x.LmaxSymbol, StringComparer.OrdinalIgnoreCase))
        {
            var summaryGroup = summary.Where(x => x.LmaxSymbol.Equals(group.Key, StringComparison.OrdinalIgnoreCase)).ToList();
            AddMismatch(issues, importRunId, $"{group.Key} notional", group.Sum(x => Math.Abs(x.NotionalValue)), summaryGroup.Sum(x => Math.Abs(x.NotionalValue)));
            AddMismatch(issues, importRunId, $"{group.Key} commission", group.Sum(x => Math.Abs(x.TotalCommission)), summaryGroup.Sum(x => Math.Abs(x.CommissionFullPrecision)));
        }

        if (individual.Select(x => x.OrderId ?? x.InstructionId ?? x.ExecutionId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != summary.Count)
        {
            issues.Add(new LmaxReportValidationIssue(Guid.NewGuid(), importRunId, LmaxReportValidationIssueType.SummaryMismatch, LmaxReportValidationSeverity.Warning, "Individual trade group count does not match trades.csv row count.", null, null, clock.UtcNow));
        }

        return issues;
    }

    private void AddMismatch(List<LmaxReportValidationIssue> issues, LmaxReportImportRunId importRunId, string label, decimal individual, decimal summary)
    {
        if (Math.Abs(individual - summary) > options.SummaryTolerance)
        {
            issues.Add(new LmaxReportValidationIssue(Guid.NewGuid(), importRunId, LmaxReportValidationIssueType.SummaryMismatch, LmaxReportValidationSeverity.Blocking, $"Summary mismatch for {label}: individual={individual}, summary={summary}.", null, null, clock.UtcNow));
        }
    }
}

public sealed class EodReconciliationService(IIntradayRepository intradayRepository, ILmaxEodReportRepository eodRepository, IClock clock) : IEodReconciliationService
{
    public async Task<EodReconciliationResult> RunAsync(DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken)
    {
        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        var venue = state.Venues.Single(x => x.Name.Equals(venueName, StringComparison.OrdinalIgnoreCase));
        var account = state.BrokerAccounts.Single(x => x.AccountCode.Equals(brokerAccountCode, StringComparison.OrdinalIgnoreCase));
        var lmax = (await eodRepository.GetIndividualTradesAsync(reportDate, 500, cancellationToken)).Where(x => x.VenueId == venue.Id && x.BrokerAccountId == account.Id).ToList();
        var fills = state.Fills.Where(x => x.VenueId == venue.Id && DateOnly.FromDateTime(x.TradeDateUtc.UtcDateTime) == reportDate).ToList();
        var breaks = new List<EodReconciliationBreak>();
        foreach (var fill in fills)
        {
            var broker = lmax.FirstOrDefault(x => x.ExecutionId == fill.BrokerExecutionId);
            if (broker is null) { breaks.Add(NewBreak(ReconciliationBreakType.InternalFillMissingInBrokerReport, fill.InstrumentId, $"Internal fill {fill.BrokerExecutionId} is missing in individual-trades.csv.", fill.BrokerExecutionId, fill.Id.Value.ToString("D"))); continue; }
            if (broker.InstrumentId != fill.InstrumentId) breaks.Add(NewBreak(ReconciliationBreakType.InstrumentMismatch, fill.InstrumentId, $"Instrument mismatch for {fill.BrokerExecutionId}.", fill.BrokerExecutionId, fill.Id.Value.ToString("D")));
            if (Math.Abs(Math.Abs(broker.UnitsBoughtSold) - fill.BaseQuantity) > 0.0001m) breaks.Add(NewBreak(ReconciliationBreakType.QuantityMismatch, fill.InstrumentId, $"Quantity mismatch for {fill.BrokerExecutionId}.", fill.BrokerExecutionId, fill.Id.Value.ToString("D")));
            if (Math.Abs(broker.TradePrice - fill.Price) > 0.0000001m) breaks.Add(NewBreak(ReconciliationBreakType.PriceMismatch, fill.InstrumentId, $"Price mismatch for {fill.BrokerExecutionId}.", fill.BrokerExecutionId, fill.Id.Value.ToString("D")));
            if ((broker.UnitsBoughtSold >= 0 ? TradeSide.Buy : TradeSide.Sell) != fill.Side) breaks.Add(NewBreak(ReconciliationBreakType.SideMismatch, fill.InstrumentId, $"Side mismatch for {fill.BrokerExecutionId}.", fill.BrokerExecutionId, fill.Id.Value.ToString("D")));
        }

        breaks.AddRange(lmax.Where(x => fills.All(fill => fill.BrokerExecutionId != x.ExecutionId)).Select(x => NewBreak(ReconciliationBreakType.BrokerFillMissingInternally, x.InstrumentId, $"LMAX execution {x.ExecutionId} is missing internally.", x.ExecutionId, null)));
        foreach (var instrumentId in fills.Select(x => x.InstrumentId).Concat(lmax.Where(x => x.InstrumentId is not null).Select(x => x.InstrumentId!.Value)).Distinct())
        {
            var internalDelta = fills.Where(x => x.InstrumentId == instrumentId).Sum(x => x.Side == TradeSide.Buy ? x.BaseQuantity : -x.BaseQuantity);
            var brokerDelta = lmax.Where(x => x.InstrumentId == instrumentId).Sum(x => x.UnitsBoughtSold);
            if (Math.Abs(internalDelta - brokerDelta) > 0.0001m) breaks.Add(NewBreak(ReconciliationBreakType.PositionDeltaMismatch, instrumentId, $"Report-date position delta mismatch: internal={internalDelta}, LMAX-derived={brokerDelta}.", null, null));
        }

        var run = new EodReconciliationRun(Guid.NewGuid(), reportDate, venue.Id, account.Id, clock.UtcNow, breaks.Any(x => x.Severity == ReconciliationBreakSeverity.Blocking));
        breaks = breaks.Select(x => x with { RunId = run.Id }).ToList();
        await eodRepository.AddEodReconciliationAsync(run, breaks, cancellationToken);
        return new EodReconciliationResult(run.Id, reportDate, breaks.Count, breaks.Count(x => x.Severity == ReconciliationBreakSeverity.Blocking), breaks);

        EodReconciliationBreak NewBreak(ReconciliationBreakType type, InstrumentId? instrumentId, string description, string? brokerExecutionId, string? fillId)
            => new(Guid.NewGuid(), Guid.Empty, type, ReconciliationBreakSeverity.Blocking, ReconciliationBreakStatus.Open, instrumentId, description, brokerExecutionId, fillId, clock.UtcNow);
    }
}

public sealed class EodPnlSummaryService(IIntradayRepository intradayRepository, ILmaxEodReportRepository eodRepository) : IEodPnlSummaryService
{
    public async Task<EodPnlSummary?> GetSummaryAsync(DateOnly reportDate, string venueName, string brokerAccountCode, CancellationToken cancellationToken)
    {
        var state = await intradayRepository.LoadStateAsync(cancellationToken);
        var venue = state.Venues.FirstOrDefault(x => x.Name.Equals(venueName, StringComparison.OrdinalIgnoreCase));
        var account = state.BrokerAccounts.FirstOrDefault(x => x.AccountCode.Equals(brokerAccountCode, StringComparison.OrdinalIgnoreCase));
        if (venue is null || account is null) return null;
        var wallets = (await eodRepository.GetCurrencyWalletsAsync(reportDate, 500, cancellationToken)).Where(x => x.VenueId == venue.Id && x.BrokerAccountId == account.Id).ToList();
        var rows = wallets.Select(x => new EodPnlCurrencyRow(x.Currency, x.WalletBalance, x.RateToBaseCcy, x.WalletBalanceBaseUsd, x.ProfitLoss, x.ProfitLossBaseUsd, x.Commission, x.CommissionBaseUsd, x.Dividends, x.DividendsBaseUsd, x.Financing, x.FinancingBaseUsd)).ToList();
        var pnl = rows.Sum(x => x.ProfitLossBaseUsd);
        var commission = rows.Sum(x => x.CommissionBaseUsd);
        var dividends = rows.Sum(x => x.DividendsBaseUsd);
        var financing = rows.Sum(x => x.FinancingBaseUsd);
        return new EodPnlSummary(reportDate, venueName, brokerAccountCode, rows.Sum(x => x.WalletBalanceBaseUsd), pnl, commission, dividends, financing, pnl + commission + dividends + financing, rows);
    }
}

public sealed class FakeLmaxEodReportGenerator(IIntradayRepository repository, IClock clock, LmaxEodReportOptions options) : IFakeLmaxEodReportGenerator
{
    public async Task<FakeLmaxEodReportGenerationResult> GenerateAsync(DateOnly reportDate, string venueName, string brokerAccountCode, LmaxEodMutationMode mutationMode, CancellationToken cancellationToken)
    {
        var state = await repository.LoadStateAsync(cancellationToken);
        var venue = state.Venues.Single(x => x.Name.Equals(venueName, StringComparison.OrdinalIgnoreCase));
        var account = state.BrokerAccounts.Single(x => x.AccountCode.Equals(brokerAccountCode, StringComparison.OrdinalIgnoreCase));
        var root = Path.GetFullPath(Path.Combine(options.DataRoot, "generated"));
        Directory.CreateDirectory(root);
        var stamp = $"{reportDate:yyyyMMdd}_{clock.UtcNow:HHmmss}";
        var individualPath = Path.Combine(root, $"individual-trades-{stamp}.csv");
        var summaryPath = Path.Combine(root, $"trades-{stamp}.csv");
        var walletPath = Path.Combine(root, $"currency-wallets-{stamp}.csv");
        var fills = state.Fills.Where(x => x.VenueId == venue.Id && DateOnly.FromDateTime(x.TradeDateUtc.UtcDateTime) == reportDate).OrderBy(x => x.ReceivedAtUtc).ToList();
        var individualRows = fills.Select((fill, index) => IndividualRow(state, account, fill, index)).ToList();
        MutateIndividual(individualRows, mutationMode);
        var summaryRows = SummaryRows(individualRows);
        MutateSummary(summaryRows, mutationMode);
        var walletRows = WalletRows(individualRows, account.ExternalAccountId ?? account.AccountCode);
        MutateWallet(walletRows, mutationMode);
        await File.WriteAllLinesAsync(individualPath, [string.Join(",", LmaxEodHeaders.Individual), .. individualRows.Select(Csv)], cancellationToken);
        await File.WriteAllLinesAsync(summaryPath, [string.Join(",", LmaxEodHeaders.Summary), .. summaryRows.Select(Csv)], cancellationToken);
        await File.WriteAllLinesAsync(walletPath, [string.Join(",", LmaxEodHeaders.Wallet), .. walletRows.Select(Csv)], cancellationToken);
        return new FakeLmaxEodReportGenerationResult(reportDate, individualPath, summaryPath, walletPath, individualRows.Count, summaryRows.Count, walletRows.Count, mutationMode);
    }

    private static string[] IndividualRow(PlatformState state, BrokerAccount account, Fill fill, int index)
    {
        var instrument = state.Instruments.Single(x => x.Id == fill.InstrumentId);
        var alias = state.InstrumentAliases.FirstOrDefault(x => x.Source == "LMAX_REPORT" && x.InstrumentId == fill.InstrumentId && x.IsEnabled);
        var symbol = alias?.ExternalSymbol ?? $"{instrument.BaseCurrency.Code}/{instrument.QuoteCurrency.Code}";
        var signedVenue = fill.Side == TradeSide.Buy ? fill.VenueQuantity : -fill.VenueQuantity;
        var signedBase = fill.Side == TradeSide.Buy ? fill.BaseQuantity : -fill.BaseQuantity;
        var notional = -(signedBase * fill.Price);
        var commission = -Math.Round(Math.Abs(notional) * 0.00002m, 6);
        return [fill.BrokerExecutionId, $"MTF-{fill.BrokerExecutionId}", fill.ReceivedAtUtc.UtcDateTime.ToString("dd-MM-yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture), S(signedVenue), S(fill.Price), fill.TradeDateUtc.UtcDateTime.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture), alias?.ExternalInstrumentId ?? string.Empty, symbol, $"INST-{fill.ChildOrderId.Value:N}", state.ExecutionReports.FirstOrDefault(x => x.ChildOrderId == fill.ChildOrderId)?.BrokerOrderId ?? $"BO-{index + 1}", string.Empty, string.Empty, fill.ReceivedAtUtc.UtcDateTime.ToString("dd-MM-yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture), "Market", "LMAX", "LOCAL_SIM", "0", S(commission), account.ExternalAccountId ?? account.AccountCode, S(signedBase), S(notional), $"UTI-{fill.BrokerExecutionId}"];
    }

    private static List<string[]> SummaryRows(List<string[]> rows)
        => rows.GroupBy(x => x[9]).Select(group =>
        {
            var first = group.First();
            var contracts = group.Sum(x => D(x[3]));
            var absContracts = group.Sum(x => Math.Abs(D(x[3])));
            var notional = group.Sum(x => Math.Abs(D(x[20])));
            var commission = group.Sum(x => Math.Abs(D(x[17])));
            var average = absContracts == 0 ? 0m : group.Sum(x => D(x[4]) * Math.Abs(D(x[3]))) / absContracts;
            return new[] { DateTime.ParseExact(first[2], "dd-MM-yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture).ToString("M/d/yyyy HH:mm", CultureInfo.InvariantCulture), first[7], first[13], first[7].Contains('/') ? first[7].Split('/')[1] : "USD", S(contracts), S(average), S(Math.Round(commission, 2)), S(notional), first[7], first[15], S(commission), first[18] };
        }).ToList();

    private static List<string[]> WalletRows(List<string[]> rows, string accountId)
    {
        var commission = rows.Sum(x => D(x[17]));
        return [["USD", "1000000", "0", "0", "0", S(commission), "0", "0", S(1000000m + commission), "1", accountId]];
    }

    private static void MutateIndividual(List<string[]> rows, LmaxEodMutationMode mode)
    {
        if (mode == LmaxEodMutationMode.DropOneExecution && rows.Count > 0) rows.RemoveAt(0);
        if (mode == LmaxEodMutationMode.AddUnknownExecution && rows.Count > 0) { var copy = rows[0].ToArray(); copy[0] = $"UNKNOWN-{Guid.NewGuid():N}"; copy[21] = $"UTI-UNKNOWN-{Guid.NewGuid():N}"; rows.Add(copy); }
        if (mode == LmaxEodMutationMode.ChangeExecutionQuantity && rows.Count > 0) rows[0][19] = S(D(rows[0][19]) + 1000m);
        if (mode == LmaxEodMutationMode.ChangeExecutionPrice && rows.Count > 0) rows[0][4] = S(D(rows[0][4]) + 0.01m);
        if (mode == LmaxEodMutationMode.ChangeExecutionSide && rows.Count > 0) rows[0][19] = S(-D(rows[0][19]));
    }

    private static void MutateSummary(List<string[]> rows, LmaxEodMutationMode mode)
    {
        if (mode == LmaxEodMutationMode.DropOneSummaryRow && rows.Count > 0) rows.RemoveAt(0);
        if (mode == LmaxEodMutationMode.ChangeSummaryCommission && rows.Count > 0) rows[0][10] = S(D(rows[0][10]) + 10m);
        if (mode == LmaxEodMutationMode.ChangeSummaryNotional && rows.Count > 0) rows[0][7] = S(D(rows[0][7]) + 1000m);
    }

    private static void MutateWallet(List<string[]> rows, LmaxEodMutationMode mode)
    {
        if (mode == LmaxEodMutationMode.DropCurrencyWallet && rows.Count > 0) rows.RemoveAt(0);
        if (mode == LmaxEodMutationMode.ChangeWalletBalance && rows.Count > 0) rows[0][8] = S(D(rows[0][8]) + 123.45m);
        if (mode == LmaxEodMutationMode.ChangeWalletRate && rows.Count > 0) rows[0][9] = "1.2";
    }

    private static decimal D(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);
    private static string S(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Csv(string[] values) => string.Join(",", values.Select(x => x.Contains(',') || x.Contains('"') ? $"\"{x.Replace("\"", "\"\"")}\"" : x));
}
