using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.SqlServer;

// Report tables only. No Worker, broker client, session repair or schema migration.
if (Environment.MachineName != "EC2AMAZ-1QPHTD8" || Environment.UserName != "Administrator") throw new Exception("DEMO_OWNER_REQUIRED");
if (args.Length != 1) throw new Exception("INPUT_JSON_REQUIRED");
var input = JsonSerializer.Deserialize<Input>(File.ReadAllText(args[0])) ?? throw new Exception("INVALID_INPUT");
var date = DateOnly.ParseExact(input.date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
if (date > DateOnly.FromDateTime(DateTime.UtcNow)) throw new Exception("FUTURE_REPORT_DATE");
var root = Path.GetFullPath(@"D:\data\lmax-eod") + Path.DirectorySeparatorChar;
string RequirePath(string value)
{
    var p = Path.GetFullPath(value);
    if (!p.StartsWith(root,StringComparison.OrdinalIgnoreCase) || value.Contains("..")) throw new Exception("REPORT_PATH_OUTSIDE_ROOT");
    return p;
}
var output = RequirePath(input.output);
if (File.Exists(output)) throw new Exception("RECEIPT_EXISTS");
foreach(var f in input.files.Values)
    if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(RequirePath(f.path)))).ToLowerInvariant() != f.sha256) throw new Exception("SOURCE_HASH_MISMATCH");
if (!input.files.ContainsKey("individual")) throw new Exception("INDIVIDUAL_REPORT_REQUIRED");
var options = new DbContextOptionsBuilder<IntradayDbContext>().UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=QQProductionIntraday;Integrated Security=true;TrustServerCertificate=true;Application Name=QQ84DailyEod").Options;
await using var db = new IntradayDbContext(options);
var repo = new SqlServerIntradayRepository(db);
var state = await repo.LoadStateAsync(default);
var account = state.BrokerAccounts.Single(x=>x.AccountCode=="LMAX_DEMO_LOCAL" && x.IsEnabled);
if(account.ExternalAccountId != "1754288005") throw new Exception("EXTERNAL_ACCOUNT_BINDING_REQUIRED");
var venue = state.Venues.Single(x=>x.Name=="LMAX");
// Existing reconciler scopes internal fills by venue; refuse mixed-account usage.
if(state.BrokerAccounts.Count(x=>x.IsEnabled) != 1) throw new Exception("MULTI_ACCOUNT_RECONCILER_NOT_QUALIFIED");
var clock = new EodClock();
var reportOptions = new LmaxEodReportOptions { DataRoot=root };
var memory = new InMemoryLmaxEodReportRepository(state);
LmaxEodReportImportService Importer(ILmaxEodReportRepository r) => new(repo,r,new LmaxReportPairConsistencyService(r,clock,reportOptions),clock,reportOptions);
bool full = input.files.ContainsKey("summary") && input.files.ContainsKey("wallet");
Task<LmaxReportImportResult> Import(ILmaxEodReportRepository r) => full
    ? Importer(r).ImportReportSetAsync(input.files["individual"].path,input.files["summary"].path,input.files["wallet"].path,date,"LMAX","LMAX_DEMO_LOCAL",default)
    : Importer(r).ImportIndividualTradesAsync(input.files["individual"].path,date,"LMAX","LMAX_DEMO_LOCAL",default);
var preview = await Import(memory);
if(preview.BlockingIssueCount>0) throw new Exception("SOURCE_VALIDATION_FAILED:"+preview.Message);
var incoming = await memory.GetIndividualTradesAsync(date,500,default);
if(incoming.Count==0 || incoming.Count>=500) throw new Exception("EMPTY_OR_REPOSITORY_LIMIT_REACHED");
await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
var existing = await db.LmaxIndividualTrades.AsNoTracking().Where(x=>x.VenueId==venue.Id && x.BrokerAccountId==account.Id).ToListAsync();
foreach(var t in incoming)
{
    var prior=existing.SingleOrDefault(x=>x.ExecutionId==t.ExecutionId);
    if(prior is not null && prior.RawLine!=t.RawLine) throw new Exception("CONFLICTING_EXECUTION_REQUIRES_AUDITED_CORRECTION");
    if(t.TradeUti!="" && existing.Any(x=>x.TradeUti==t.TradeUti && x.ExecutionId!=t.ExecutionId)) throw new Exception("CONFLICTING_UTI");
}
// The existing wallet repository updates in place. Reject revisions rather than erase evidence.
foreach(var s in await memory.GetTradeSummariesAsync(date,500,default))
{
    var prior=await db.LmaxTradeSummaries.AsNoTracking().Where(x=>x.ReportDate==date && x.VenueId==venue.Id && x.BrokerAccountId==account.Id && x.LmaxSymbol==s.LmaxSymbol && x.Type==s.Type && x.DateTimeUtc==s.DateTimeUtc).ToListAsync();
    if(prior.Any(x=>x.RawLine!=s.RawLine)) throw new Exception("SUMMARY_REVISION_REQUIRES_AUDITED_CORRECTION");
}
foreach(var w in await memory.GetCurrencyWalletsAsync(date,500,default))
{
    var prior=await db.LmaxCurrencyWallets.AsNoTracking().SingleOrDefaultAsync(x=>x.ReportDate==date && x.VenueId==venue.Id && x.BrokerAccountId==account.Id && x.Currency==w.Currency);
    if(prior is not null && prior.RawLine!=w.RawLine) throw new Exception("WALLET_REVISION_REQUIRES_AUDITED_CORRECTION");
}
var eod=new SqlServerLmaxEodReportRepository(db);
var imported=await Import(eod);
if(imported.BlockingIssueCount>0) throw new Exception("IMPORT_REJECTED");
var reconciled=await new EodReconciliationService(repo,eod,clock).RunAsync(date,"LMAX","LMAX_DEMO_LOCAL",default);
var after=await repo.LoadStateAsync(default);
if(after.Fills.Count!=state.Fills.Count || after.ExecutionReports.Count!=state.ExecutionReports.Count) throw new Exception("CONCURRENT_TRADING_ACTIVITY_RETRY_AFTER_CLOSE");
await tx.CommitAsync();
var receipt=new { schema="lmax_demo_daily_eod_v1", date=input.date, account_id="1754288005", at_utc=clock.UtcNow,
    individual_sha256=input.files["individual"].sha256, import_run_id=imported.ImportRunId.Value, import_performed=true,
    report_set_imported=full, reconciliation_run_id=reconciled.RunId, blocking_breaks=reconciled.BlockingBreakCount,
    official_rows=incoming.Count, internal_fills=after.Fills.Count, internal_execution_reports=after.ExecutionReports.Count,
    ledger_mutation_performed=false, trading_started=false };
using(var f=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.Read)) { JsonSerializer.Serialize(f,receipt,new JsonSerializerOptions{WriteIndented=true});f.Flush(true); }
Console.WriteLine(JsonSerializer.Serialize(receipt));
record Source(string path,string sha256);
record Input(string date,Dictionary<string,Source> files,string output);
sealed class EodClock:IClock {public DateTimeOffset UtcNow=>DateTimeOffset.UtcNow;}
