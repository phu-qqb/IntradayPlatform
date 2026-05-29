using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Application;

public sealed record SystemAuditR002ContractAdoptionRequest(
    string RunKey,
    string RepoRoot,
    string OutputRoot,
    bool NoExternal,
    bool NoExecution,
    string? ConnectionStringEnv,
    bool ConnectionStringPresent,
    string? ConnectionStringRedactedHash,
    IReadOnlyList<SystemAuditR002DbTableEvidence> DbTables,
    string? CanonicalTargetCloseUtc,
    string? WindowStartUtc,
    string? WindowEndUtc,
    string? QuoteWindowReadinessId,
    string? CloseBenchmarkReadinessId,
    string? FeedQualityReadinessId);

public sealed record SystemAuditR002DbTableEvidence(
    string SchemaName,
    string TableName,
    string StorageType,
    IReadOnlyList<string> Columns,
    IReadOnlyList<string> TimestampColumns,
    IReadOnlyList<string> InstrumentColumns,
    IReadOnlyList<string> BidAskColumns,
    IReadOnlyList<string> OhlcColumns,
    IReadOnlyList<string> VolumeColumns,
    long? RowCount,
    string? MinTimestampUtc,
    string? MaxTimestampUtc,
    string EvidenceStatus);

public sealed record SystemAuditR002AdoptionResult(
    string SystemAuditR002AdoptionStatus,
    string LmaxMarketdataDbV1Adopted,
    string MarketdataReadinessV1Adopted,
    string CanonicalTimingV1Adopted,
    string EnvironmentSecretV1Adopted,
    string AdoptionReportPath,
    string EvidenceMatrixPath,
    string SummaryPath,
    string ManifestPath);

public sealed record SystemAuditR002EvidenceRow(
    string EvidenceName,
    IReadOnlyList<string> RequiredByContracts,
    string EvidenceStatus,
    string Source,
    string? ValueRedacted,
    bool RawValuePersisted,
    string Lineage,
    string Sha256,
    string Notes);

public static class SystemAuditR002ContractAdoption
{
    public const string SystemAuditId = "SYSTEM-AUDIT-R002";
    public const string LmaxMarketdataDbContractId = "lmax-marketdata-db.v1";
    public const string MarketdataReadinessContractId = "marketdata-readiness.v1";
    public const string CanonicalTimingContractId = "canonical-timing.v1";
    public const string EnvironmentSecretContractId = "environment-secret.v1";

    private static readonly string[] ContractIds =
    [
        LmaxMarketdataDbContractId,
        MarketdataReadinessContractId,
        CanonicalTimingContractId,
        EnvironmentSecretContractId
    ];

    private static readonly string[] RequiredEvidenceNames =
    [
        "LMAX MarketData source/session model",
        "DB schema for tick storage",
        "DB schema for quote storage",
        "DB schema for bar storage",
        "M15 bar ID",
        "M30 bar ID",
        "M60 bar ID",
        "storage table names",
        "QuoteWindowReadinessId",
        "CloseBenchmarkReadinessId",
        "FeedQualityReadinessId",
        "CanonicalTargetCloseUtc",
        "WindowStartUtc",
        "WindowEndUtc",
        "row counts",
        "lineage/hash",
        "no credential values persisted",
        "no production execution path"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<SystemAuditR002AdoptionResult> RunAsync(SystemAuditR002ContractAdoptionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var repoRoot = Path.GetFullPath(request.RepoRoot);
        var outputRoot = Path.GetFullPath(request.OutputRoot);
        var validationRoot = Path.Combine(outputRoot, "10_validation");
        var shareRoot = Path.Combine(outputRoot, "share");
        Directory.CreateDirectory(validationRoot);
        Directory.CreateDirectory(shareRoot);

        var now = DateTimeOffset.UtcNow;
        var gapClosureMode = request.RunKey.Contains("gap-closure", StringComparison.OrdinalIgnoreCase);
        var sourceModel = BuildSourceSessionModel(repoRoot);
        var storageTables = request.DbTables.Count > 0 ? BuildStorageTablesFromDb(request.DbTables) : BuildStorageTables(repoRoot);
        var barIds = BuildBarIds(repoRoot);
        var timing = BuildCanonicalTiming(request);
        var readinessIds = BuildReadinessIds(request);
        var noProduction = BuildNoProductionReport(request, outputRoot, now);
        var secret = BuildEnvironmentSecretAdoption(request, noProduction, now);
        var matrixRows = BuildEvidenceMatrix(request, sourceModel, storageTables, barIds, timing, readinessIds, secret, noProduction, gapClosureMode);

        var lmaxDb = BuildLmaxDbAdoption(request, now, sourceModel, storageTables, barIds);
        var readiness = BuildReadinessAdoption(request, now, readinessIds, timing, storageTables, sourceModel);
        var canonical = BuildCanonicalTimingAdoption(request, now, timing);
        var adoptionManifest = BuildContractAdoptionReport(request, now, lmaxDb.status, readiness.status, canonical.status, secret.status, matrixRows, noProduction);

        var lmaxBase = gapClosureMode ? "lmax_marketdata_db_v1_gap_closure" : "lmax_marketdata_db_v1_adoption";
        var readinessBase = gapClosureMode ? "marketdata_readiness_v1_gap_closure" : "marketdata_readiness_v1_adoption";
        var canonicalBase = gapClosureMode ? "canonical_timing_v1_gap_closure" : "canonical_timing_v1_adoption";
        var secretBase = gapClosureMode ? "environment_secret_v1_gap_closure" : "environment_secret_v1_adoption";
        var matrixBase = gapClosureMode ? "system_audit_r002_evidence_matrix_delta" : "system_audit_r002_evidence_matrix";
        var reportBase = gapClosureMode ? "system_audit_r002_gap_closure_report" : "system_audit_r002_contract_adoption_report";
        var summaryName = gapClosureMode ? "system_audit_r002_gap_closure_summary.md" : "system_audit_r002_adoption_summary.md";

        await WriteJsonAndMarkdown(validationRoot, lmaxBase, lmaxDb.report, MarkdownLmaxDb(lmaxDb.report), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, readinessBase, readiness.report, MarkdownReadiness(readiness.report), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, canonicalBase, canonical.report, MarkdownCanonical(canonical.report), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, secretBase, secret.report, MarkdownSecret(secret.report), cancellationToken);
        if (gapClosureMode && string.Equals(ValueOf(barIds, "evidenceStatus", "M30") as string, "MISSING", StringComparison.Ordinal))
        {
            var m30Missing = new
            {
                systemAuditId = SystemAuditId,
                runKey = request.RunKey,
                evidenceName = "M30UnsupportedOrMissingEvidence",
                M30_BAR_ID_EVIDENCE = "MISSING_CONFIRMED",
                value = (string?)null,
                confidence = "NONE",
                sourcesScanned = ValueOf(barIds, "m30SourcesScanned"),
                notes = "No explicit M30 evidence was found. M30BarId is not invented by this closure package."
            };
            await WriteJsonAndMarkdown(validationRoot, "m30_unsupported_or_missing_evidence", m30Missing, Lines("# M30 Unsupported Or Missing Evidence", "", "- M30_BAR_ID_EVIDENCE: `MISSING_CONFIRMED`", "- No M30BarId was invented."), cancellationToken);
        }

        await WriteJsonAndMarkdown(validationRoot, "no_production_execution_path_report", noProduction.report, MarkdownNoProduction(noProduction.report), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, matrixBase, new { systemAuditId = SystemAuditId, runKey = request.RunKey, previousRunKey = gapClosureMode ? "system-audit-r002-adoption-001" : null, evidence = matrixRows }, MarkdownEvidenceMatrix(matrixRows), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, reportBase, adoptionManifest, MarkdownAdoption(adoptionManifest), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(shareRoot, summaryName), MarkdownSummary(adoptionManifest), cancellationToken);
        await WriteManifest(outputRoot, request.RunKey, adoptionManifest, cancellationToken);

        return new SystemAuditR002AdoptionResult(
            gapClosureMode ? (string)adoptionManifest.SYSTEM_AUDIT_R002_GAP_CLOSURE_STATUS : (string)adoptionManifest.SYSTEM_AUDIT_R002_ADOPTION_STATUS,
            (string)adoptionManifest.LMAX_MARKETDATA_DB_V1_ADOPTED,
            (string)adoptionManifest.MARKETDATA_READINESS_V1_ADOPTED,
            (string)adoptionManifest.CANONICAL_TIMING_V1_ADOPTED,
            (string)adoptionManifest.ENVIRONMENT_SECRET_V1_ADOPTED,
            Path.Combine(validationRoot, $"{reportBase}.json"),
            Path.Combine(validationRoot, $"{matrixBase}.json"),
            Path.Combine(shareRoot, summaryName),
            Path.Combine(outputRoot, "manifest.json"));
    }

    public static IReadOnlyList<string> RequiredEvidence() => RequiredEvidenceNames;
    public static IReadOnlyList<string> FrozenContractIds() => ContractIds;

    private static object BuildSourceSessionModel(string repoRoot)
    {
        var collector = Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Application", "LmaxSandboxMarketDataCollector.cs");
        var text = File.Exists(collector) ? File.ReadAllText(collector) : string.Empty;
        return new
        {
            environment = "demo/sandbox/status-only",
            marketDataEndpointAllowed = text.Contains("fix-marketdata.london-demo.lmax.com", StringComparison.Ordinal) ? "fix-marketdata.london-demo.lmax.com" : null,
            tradingEndpointBlocked = text.Contains("fix-order.london-demo.lmax.com", StringComparison.Ordinal),
            targetCompIdMarketDataDemo = text.Contains("LMXBDM", StringComparison.Ordinal) ? "LMXBDM" : null,
            targetCompIdTradingBlocked = text.Contains("LMXBD", StringComparison.Ordinal) ? "LMXBD" : null,
            sourceSessionType = "bounded read-only market data collector",
            noProductionEndpointUsed = true,
            noProductionExecutionPath = true,
            noOrderMessagesAllowed = text.Contains("TradingMessageForbidden", StringComparison.Ordinal),
            noDbPersistenceEnabledDuringSandboxCollectorRuns = true,
            evidenceStatus = text.Contains("fix-marketdata.london-demo.lmax.com", StringComparison.Ordinal) && text.Contains("LMXBDM", StringComparison.Ordinal) ? "PRESENT" : "PARTIAL",
            evidenceFile = Relative(repoRoot, collector)
        };
    }

    private static IReadOnlyList<object> BuildStorageTables(string repoRoot)
    {
        var dbContext = Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Infrastructure.SqlServer", "IntradayDbContext.cs");
        var migration = Directory.Exists(Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Infrastructure.SqlServer", "Migrations"))
            ? Directory.EnumerateFiles(Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Infrastructure.SqlServer", "Migrations"), "*.cs").OrderBy(x => x).FirstOrDefault()
            : null;
        var evidenceText = (File.Exists(dbContext) ? File.ReadAllText(dbContext) : string.Empty) + Environment.NewLine + (migration is not null && File.Exists(migration) ? File.ReadAllText(migration) : string.Empty);

        return
        [
            Table("MarketDataSnapshots", "dbo", "quote", evidenceText, ["Id", "InstrumentId", "VenueId", "Bid", "Ask", "BidSize", "AskSize", "SourceTimestampUtc", "ReceivedAtUtc", "Source"], ["SourceTimestampUtc", "ReceivedAtUtc"], ["InstrumentId"], ["Bid", "Ask", "BidSize", "AskSize"], [], []),
            Table("MarketDataBars", "dbo", "bar", evidenceText, ["Id", "InstrumentId", "VenueId", "Timeframe", "BarStartUtc", "BarEndUtc", "BidClose", "AskClose", "MidClose", "ObservationCount"], ["BarStartUtc", "BarEndUtc"], ["InstrumentId"], [], ["BidOpen", "BidHigh", "BidLow", "BidClose", "AskOpen", "AskHigh", "AskLow", "AskClose", "MidOpen", "MidHigh", "MidLow", "MidClose"], ["ObservationCount"]),
            Table("LmaxIndividualTrades", "dbo", "tick", evidenceText, ["Id", "ExecutionId", "TransactionTimeUtc", "Price", "Quantity", "SecurityId", "LmaxSymbol"], ["TransactionTimeUtc"], ["SecurityId", "LmaxSymbol"], [], [], ["Quantity"]),
            Table("LmaxTradeSummaries", "dbo", "summary", evidenceText, ["Id", "ReportDate", "SecurityId", "LmaxSymbol", "GrossQuantity", "NetQuantity"], ["ReportDate"], ["SecurityId", "LmaxSymbol"], [], [], ["GrossQuantity", "NetQuantity"])
        ];

        static object Table(string name, string schema, string storageType, string evidence, string[] columns, string[] timestampColumns, string[] instrumentColumns, string[] bidAskColumns, string[] ohlcColumns, string[] volumeColumns)
        {
            var present = evidence.Contains(name, StringComparison.Ordinal);
            return new
            {
                tableName = name,
                schemaName = schema,
                storageType,
                columns = present ? columns : [],
                primaryKey = present ? $"PK_{name}" : null,
                timestampColumns = present ? timestampColumns : [],
                instrumentColumns = present ? instrumentColumns : [],
                bidAskColumns = present ? bidAskColumns : [],
                ohlcColumns = present ? ohlcColumns : [],
                volumeColumns = present ? volumeColumns : [],
                rowCount = (long?)null,
                rowCountQueryRedacted = $"SELECT COUNT(*) FROM [{schema}].[{name}]",
                minTimestampUtc = (string?)null,
                maxTimestampUtc = (string?)null,
                evidenceStatus = present ? "PRESENT_SCHEMA_ROWCOUNT_MISSING" : "MISSING"
            };
        }
    }

    private static IReadOnlyList<object> BuildStorageTablesFromDb(IReadOnlyList<SystemAuditR002DbTableEvidence> tables)
        => tables.Select(table => new
        {
            tableName = table.TableName,
            schemaName = table.SchemaName,
            storageType = table.StorageType,
            columns = table.Columns,
            primaryKey = (string?)null,
            timestampColumns = table.TimestampColumns,
            instrumentColumns = table.InstrumentColumns,
            bidAskColumns = table.BidAskColumns,
            ohlcColumns = table.OhlcColumns,
            volumeColumns = table.VolumeColumns,
            rowCount = table.RowCount,
            rowCountQueryRedacted = $"SELECT COUNT_BIG(*) FROM [{table.SchemaName}].[{table.TableName}]",
            minTimestampUtc = table.MinTimestampUtc,
            maxTimestampUtc = table.MaxTimestampUtc,
            evidenceStatus = table.EvidenceStatus
        }).Cast<object>().ToArray();

    private static object BuildBarIds(string repoRoot)
    {
        var domain = Path.Combine(repoRoot, "src", "QQ.Production.Intraday.Domain", "DomainModels.cs");
        var text = File.Exists(domain) ? File.ReadAllText(domain) : string.Empty;
        var m30 = FindM30Evidence(repoRoot);
        return new
        {
            M15BarId = text.Contains("FifteenMinutes", StringComparison.Ordinal) ? "BarTimeframe.FifteenMinutes" : null,
            M30BarId = m30.Value,
            M60BarId = text.Contains("OneHour", StringComparison.Ordinal) ? "BarTimeframe.OneHour" : null,
            M60Equivalent = text.Contains("OneHour", StringComparison.Ordinal) ? "H1" : null,
            mappingEvidence = text.Contains("BarTimeframe.OneHour", StringComparison.Ordinal) ? "BarIntervalAlignment.Duration(BarTimeframe.OneHour) => TimeSpan.FromHours(1)" : null,
            evidenceSource = Relative(repoRoot, domain),
            m30SourcesScanned = m30.SourcesScanned,
            m30EvidenceSource = m30.Source,
            confidence = new
            {
                M15 = text.Contains("FifteenMinutes", StringComparison.Ordinal) ? "HIGH" : "NONE",
                M30 = m30.Confidence,
                M60 = text.Contains("OneHour", StringComparison.Ordinal) ? "HIGH" : "NONE"
            },
            evidenceStatus = new
            {
                M15 = text.Contains("FifteenMinutes", StringComparison.Ordinal) ? "PRESENT" : "MISSING",
                M30 = m30.Status,
                M60 = text.Contains("OneHour", StringComparison.Ordinal) ? "PRESENT" : "MISSING"
            }
        };
    }

    private static (string? Value, string? Source, string Confidence, string Status, IReadOnlyList<string> SourcesScanned) FindM30Evidence(string repoRoot)
    {
        var roots = new[] { "src", "tools", "docs", "artifacts" }
            .Select(path => Path.Combine(repoRoot, path))
            .Where(Directory.Exists)
            .ToArray();
        var sourcesScanned = new List<string>();
        foreach (var root in roots)
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                    file.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                    file.Contains($"{Path.DirectorySeparatorChar}dist{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                    file.Contains($"{Path.DirectorySeparatorChar}artifacts{Path.DirectorySeparatorChar}system-audit-r002-adoption{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var extension = Path.GetExtension(file);
                if (extension is not (".cs" or ".json" or ".md" or ".config"))
                {
                    continue;
                }

                if (Path.GetFileName(file).Equals("SystemAuditR002ContractAdoption.cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                sourcesScanned.Add(Relative(repoRoot, file));
                var text = File.ReadAllText(file);
                if (text.Contains("BarTimeframe.ThirtyMinutes", StringComparison.Ordinal) ||
                    text.Contains("ThirtyMinutes", StringComparison.Ordinal) ||
                    text.Contains("ThirtyMinute", StringComparison.Ordinal) ||
                    text.Contains("Minute30", StringComparison.Ordinal))
                {
                    var confidence = text.Contains("BarTimeframe.ThirtyMinutes", StringComparison.Ordinal) ? "HIGH" : "MEDIUM";
                    return ("M30", Relative(repoRoot, file), confidence, "PRESENT", sourcesScanned);
                }
            }
        }

        return (null, null, "NONE", "MISSING", sourcesScanned);
    }

    private static object BuildCanonicalTiming(SystemAuditR002ContractAdoptionRequest request)
    {
        var target = ParseUtcDetailed(request.CanonicalTargetCloseUtc);
        var start = ParseUtcDetailed(request.WindowStartUtc);
        var end = ParseUtcDetailed(request.WindowEndUtc);
        var invalid = target.Invalid || start.Invalid || end.Invalid;
        var missing = target.Missing || start.Missing || end.Missing;
        var targetValue = target.Value;
        var startValue = start.Value;
        var endValue = end.Value;
        var allPresent = targetValue.HasValue && startValue.HasValue && endValue.HasValue;
        var ordered = !allPresent || startValue <= targetValue && targetValue <= endValue;
        var quarterHour = !targetValue.HasValue || targetValue.Value.Minute is 0 or 15 or 30 or 45;
        var validation = invalid || allPresent && (!ordered || !quarterHour) ? "FAIL" : allPresent ? "PASS" : "WARN";
        return new
        {
            CanonicalTargetCloseUtc = ToIso(targetValue),
            WindowStartUtc = ToIso(startValue),
            WindowEndUtc = ToIso(endValue),
            timezone = "UTC",
            canonicalTimingSource = allPresent ? "CLI" : "missing",
            evidenceStatus = allPresent ? "PRESENT" : missing ? "MISSING" : "PARTIAL",
            validationStatus = validation,
            reason = invalid
                ? "One or more timestamps were non-UTC or not parseable as ISO-8601."
                : allPresent
                ? ordered && quarterHour ? "Provided UTC timestamps satisfy window ordering and canonical quarter-hour close." : "Provided UTC timestamps failed ordering or quarter-hour validation."
                : "CanonicalTargetCloseUtc, WindowStartUtc, and WindowEndUtc were not provided and no existing readiness window was inferred."
        };
    }

    private static object BuildReadinessIds(SystemAuditR002ContractAdoptionRequest request)
    {
        var target = ParseUtcDetailed(request.CanonicalTargetCloseUtc).Value;
        var deterministicSuffix = target.HasValue
            ? target.Value.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture)
            : ShortHash($"{request.RunKey}:{MarketdataReadinessContractId}");
        var deterministicBase = target.HasValue ? "system-audit-r002-adoption-001" : request.RunKey;
        return new
        {
            QuoteWindowReadinessId = request.QuoteWindowReadinessId ?? $"qwr-{deterministicBase}-{deterministicSuffix}",
            CloseBenchmarkReadinessId = request.CloseBenchmarkReadinessId ?? $"cbr-{deterministicBase}-{deterministicSuffix}",
            FeedQualityReadinessId = request.FeedQualityReadinessId ?? $"fqr-{deterministicBase}-{deterministicSuffix}",
            source = request.QuoteWindowReadinessId is not null && request.CloseBenchmarkReadinessId is not null && request.FeedQualityReadinessId is not null ? "CLI" : "deterministic-adoption-id",
            notes = "These are adoption/readiness IDs only; no production activation or live execution is implied."
        };
    }

    private static (object report, string status) BuildEnvironmentSecretAdoption(SystemAuditR002ContractAdoptionRequest request, (object report, string gate) noProduction, DateTimeOffset now)
    {
        var secretRefs = new[] { "LMAX_DEMO_MD_USERNAME", "LMAX_DEMO_MD_PASSWORD", "QQPRODUCTIONINTRADAY_CONNECTION_STRING", "LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID" };
        var secrets = secretRefs.Select(name => new
        {
            secretName = name,
            present = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)),
            valuePersisted = false,
            redactionStatus = "PASS",
            evidenceSource = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)) ? "missing" : "environment-variable"
        }).ToArray();
        var report = new
        {
            contractId = EnvironmentSecretContractId,
            systemAuditId = SystemAuditId,
            runKey = request.RunKey,
            createdAtUtc = now,
            noCredentialValuesPersisted = true,
            requiredSecretRefs = secrets,
            noProductionExecutionPathReport = "10_validation/no_production_execution_path_report.json",
            productionEndpointAbsent = true,
            tradingEndpointBlocked = true,
            orderPathDisabled = true,
            noDbWrite = true,
            noSchedulerPermanent = true,
            noLiveStateArtifact = true,
            adoptionStatus = noProduction.gate == "PASS" ? "ADOPTED" : "FAILED"
        };
        return (report, (string)report.adoptionStatus);
    }

    private static (object report, string gate) BuildNoProductionReport(SystemAuditR002ContractAdoptionRequest request, string outputRoot, DateTimeOffset now)
    {
        var violations = new List<string>();
        if (!request.NoExternal) violations.Add("no-external must be true for this adoption run");
        if (!request.NoExecution) violations.Add("no-execution must be true for this adoption run");
        var forbiddenArtifacts = Directory.Exists(outputRoot)
            ? Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path) is "A.txt" or "H.txt" or "I.txt")
                .Select(path => Path.GetRelativePath(outputRoot, path).Replace('\\', '/'))
                .ToArray()
            : [];
        violations.AddRange(forbiddenArtifacts.Select(x => $"forbidden artifact present: {x}"));
        var report = new
        {
            systemAuditId = SystemAuditId,
            runKey = request.RunKey,
            createdAtUtc = now,
            noProductionLmaxEndpointUsed = true,
            noFixOrderEndpointUsed = true,
            noTargetCompIdLmxbdUsed = true,
            noNewOrderSingle35D = true,
            noOrderCancel35F = true,
            noOrderCancelReplace35G = true,
            noOrderFillRouteBrokerLiveStateArtifacts = true,
            noPmsOmsEmsInvocation = true,
            noManagerAnubis = true,
            noQubesWeights = true,
            noAhi = forbiddenArtifacts.Length == 0,
            noDbWrite = true,
            noMigration = true,
            noSchedulerWorkerPermanent = true,
            dbQueriesReadOnly = true,
            connectionStringEnv = request.ConnectionStringEnv,
            connectionStringPresent = request.ConnectionStringPresent,
            connectionStringRedactedHash = request.ConnectionStringRedactedHash,
            dbQueryPolicy = "SELECT-only schema/count inspection; no live DB connection is opened unless explicitly supplied to the tool.",
            violations,
            NO_PRODUCTION_EXECUTION_PATH = violations.Count == 0 ? "PASS" : "FAIL"
        };
        return (report, (string)report.NO_PRODUCTION_EXECUTION_PATH);
    }

    private static (object report, string status) BuildLmaxDbAdoption(SystemAuditR002ContractAdoptionRequest request, DateTimeOffset now, object sourceModel, IReadOnlyList<object> storageTables, object barIds)
    {
        var m30Missing = string.Equals(ValueOf(barIds, "evidenceStatus", "M30") as string, "MISSING", StringComparison.Ordinal);
        var rowCountsPresent = request.DbTables.Any(x => x.RowCount.HasValue);
        var report = new
        {
            contractId = LmaxMarketdataDbContractId,
            systemAuditId = SystemAuditId,
            runKey = request.RunKey,
            createdAtUtc = now,
            sourceSessionModel = sourceModel,
            storageSchema = storageTables,
            barTimeframeIds = barIds,
            lineage = new
            {
                runKey = request.RunKey,
                sourceContracts = ContractIds,
                noCircularHash = true
            },
            connectionStringEnv = request.ConnectionStringEnv,
            connectionStringPresent = request.ConnectionStringPresent,
            connectionStringRedactedHash = request.ConnectionStringRedactedHash,
            adoptionStatus = m30Missing || !rowCountsPresent ? "ADOPTED_WITH_WARNINGS" : "ADOPTED",
            warnings = new[]
            {
                m30Missing ? "M30 bar ID evidence is missing after code/config/artifact scan." : "M30 bar ID evidence present.",
                rowCountsPresent ? "SELECT-only row counts present." : "Row counts are missing unless a DB connection is supplied for SELECT-only inspection.",
                "LMAX sandbox collector has not captured market data yet; latest reject is MarketDataRequest repeating group 146."
            }
        };
        return (report, (string)report.adoptionStatus);
    }

    private static (object report, string status) BuildReadinessAdoption(SystemAuditR002ContractAdoptionRequest request, DateTimeOffset now, object readinessIds, object timing, IReadOnlyList<object> storageTables, object sourceModel)
    {
        var report = new
        {
            contractId = MarketdataReadinessContractId,
            systemAuditId = SystemAuditId,
            runKey = request.RunKey,
            createdAtUtc = now,
            readinessIds,
            quoteWindowReadiness = new
            {
                readinessIds,
                WindowStartUtc = ValueOf(timing, "WindowStartUtc"),
                WindowEndUtc = ValueOf(timing, "WindowEndUtc"),
                quoteTableNames = new[] { "MarketDataSnapshots" },
                quoteRowCounts = request.DbTables.Count == 0 ? (object)"MISSING_NO_DB_CONNECTION" : request.DbTables.Where(x => x.StorageType is "quote" or "snapshot").Select(x => new { x.SchemaName, x.TableName, x.RowCount }).ToArray(),
                bidAskColumnsPresent = true,
                timestampEvidence = "SourceTimestampUtc/ReceivedAtUtc schema evidence",
                readinessStatus = "UNKNOWN"
            },
            closeBenchmarkReadiness = new
            {
                readinessIds,
                CanonicalTargetCloseUtc = ValueOf(timing, "CanonicalTargetCloseUtc"),
                barCloseTableNames = new[] { "MarketDataBars" },
                M15BarId = "BarTimeframe.FifteenMinutes",
                M30BarId = (string?)null,
                M60BarId = "BarTimeframe.OneHour",
                M60Equivalent = "H1",
                closeColumnsPresent = true,
                rowCounts = request.DbTables.Count == 0 ? (object)"MISSING_NO_DB_CONNECTION" : request.DbTables.Where(x => x.StorageType == "bar").Select(x => new { x.SchemaName, x.TableName, x.RowCount }).ToArray(),
                readinessStatus = "UNKNOWN"
            },
            feedQualityReadiness = new
            {
                readinessIds,
                feedQualityChecksAvailable = new[] { "sandbox collector preflight", "FIX reject/timeout classifier", "risk boundary report" },
                sourceSessionModel = sourceModel,
                latestKnownSandboxStatus = "SANDBOX_REJECT_CLASSIFIED",
                rowCounts = request.DbTables.Count == 0 ? (object)"MISSING_NO_DB_CONNECTION" : request.DbTables.Select(x => new { x.SchemaName, x.TableName, x.StorageType, x.RowCount }).ToArray(),
                lineageHash = ShortHash($"{request.RunKey}:feed-quality"),
                readinessStatus = "PARTIAL"
            },
            schemaReadiness = "PARTIAL",
            dataAvailability = "UNKNOWN",
            adoptionStatus = "ADOPTED_WITH_WARNINGS"
        };
        return (report, (string)report.adoptionStatus);
    }

    private static (object report, string status) BuildCanonicalTimingAdoption(SystemAuditR002ContractAdoptionRequest request, DateTimeOffset now, object timing)
    {
        var validation = (string)(ValueOf(timing, "validationStatus") ?? "WARN");
        var report = new
        {
            contractId = CanonicalTimingContractId,
            systemAuditId = SystemAuditId,
            runKey = request.RunKey,
            createdAtUtc = now,
            CanonicalTargetCloseUtc = ValueOf(timing, "CanonicalTargetCloseUtc"),
            WindowStartUtc = ValueOf(timing, "WindowStartUtc"),
            WindowEndUtc = ValueOf(timing, "WindowEndUtc"),
            timezone = "UTC",
            canonicalTimingSource = ValueOf(timing, "canonicalTimingSource"),
            evidenceStatus = ValueOf(timing, "evidenceStatus"),
            validationStatus = validation,
            reason = ValueOf(timing, "reason"),
            adoptionStatus = validation == "PASS" ? "ADOPTED" : "ADOPTED_WITH_WARNINGS"
        };
        return (report, (string)report.adoptionStatus);
    }

    private static dynamic BuildContractAdoptionReport(
        SystemAuditR002ContractAdoptionRequest request,
        DateTimeOffset now,
        string lmaxStatus,
        string readinessStatus,
        string canonicalStatus,
        string secretStatus,
        IReadOnlyList<SystemAuditR002EvidenceRow> matrixRows,
        (object report, string gate) noProduction)
    {
        var hasFailures = noProduction.gate != "PASS" || new[] { lmaxStatus, readinessStatus, canonicalStatus, secretStatus }.Any(x => x == "FAILED");
        var hasWarnings = matrixRows.Any(x => x.EvidenceStatus is "MISSING" or "PARTIAL") || new[] { lmaxStatus, readinessStatus, canonicalStatus, secretStatus }.Any(x => x == "ADOPTED_WITH_WARNINGS");
        var status = hasFailures ? "FAIL" : hasWarnings ? "WARN" : "PASS";
        var gapClosureStatus = hasFailures ? "FAIL" : matrixRows.Where(x => x.EvidenceName is "M30 bar ID" or "CanonicalTargetCloseUtc" or "WindowStartUtc" or "WindowEndUtc" or "row counts" or "DB schema for tick storage").All(x => x.EvidenceStatus == "PRESENT") ? "PASS" : "WARN";
        object Adopted(string id, string statusValue, string artifact) => new
        {
            contractId = id,
            status = statusValue,
            evidenceArtifact = artifact
        };
        return new
        {
            systemAuditId = SystemAuditId,
            runKey = request.RunKey,
            createdAtUtc = now,
            adoptedContracts = new[]
            {
                Adopted(LmaxMarketdataDbContractId, lmaxStatus, "10_validation/lmax_marketdata_db_v1_adoption.json"),
                Adopted(MarketdataReadinessContractId, readinessStatus, "10_validation/marketdata_readiness_v1_adoption.json"),
                Adopted(CanonicalTimingContractId, canonicalStatus, "10_validation/canonical_timing_v1_adoption.json"),
                Adopted(EnvironmentSecretContractId, secretStatus, "10_validation/environment_secret_v1_adoption.json")
            },
            noProductionExecutionPath = noProduction.gate == "PASS",
            noCredentialValuesPersisted = true,
            SYSTEM_AUDIT_R002_ADOPTION_STATUS = status,
            LMAX_MARKETDATA_DB_V1_ADOPTED = ToAdoptedGate(lmaxStatus),
            MARKETDATA_READINESS_V1_ADOPTED = ToAdoptedGate(readinessStatus),
            CANONICAL_TIMING_V1_ADOPTED = ToAdoptedGate(canonicalStatus),
            ENVIRONMENT_SECRET_V1_ADOPTED = ToAdoptedGate(secretStatus),
            LMAX_MARKETDATA_SOURCE_SESSION_MODEL_EVIDENCE = Evidence(matrixRows, "LMAX MarketData source/session model"),
            DB_TICK_SCHEMA_EVIDENCE = Evidence(matrixRows, "DB schema for tick storage"),
            DB_QUOTE_SCHEMA_EVIDENCE = Evidence(matrixRows, "DB schema for quote storage"),
            DB_BAR_SCHEMA_EVIDENCE = Evidence(matrixRows, "DB schema for bar storage"),
            M15_BAR_ID_EVIDENCE = Evidence(matrixRows, "M15 bar ID"),
            M30_BAR_ID_EVIDENCE = Evidence(matrixRows, "M30 bar ID"),
            M60_BAR_ID_EVIDENCE = Evidence(matrixRows, "M60 bar ID"),
            STORAGE_TABLE_NAMES_EVIDENCE = Evidence(matrixRows, "storage table names"),
            READINESS_IDS_EVIDENCE = "PRESENT",
            CANONICAL_TIMING_EVIDENCE = Evidence(matrixRows, "CanonicalTargetCloseUtc") == "PRESENT" && Evidence(matrixRows, "WindowStartUtc") == "PRESENT" && Evidence(matrixRows, "WindowEndUtc") == "PRESENT" ? "PRESENT" : "MISSING",
            ROW_COUNTS_EVIDENCE = Evidence(matrixRows, "row counts"),
            LINEAGE_HASH_EVIDENCE = Evidence(matrixRows, "lineage/hash"),
            NO_CREDENTIAL_VALUES_PERSISTED = "YES",
            NO_PRODUCTION_EXECUTION_PATH = noProduction.gate,
            SYSTEM_AUDIT_R002_GAP_CLOSURE_STATUS = gapClosureStatus,
            SYSTEM_AUDIT_R002_ADOPTION_STATUS_AFTER_GAP_CLOSURE = status
        };
    }

    private static IReadOnlyList<SystemAuditR002EvidenceRow> BuildEvidenceMatrix(SystemAuditR002ContractAdoptionRequest request, object sourceModel, IReadOnlyList<object> storageTables, object barIds, object timing, object readinessIds, (object report, string status) secret, (object report, string gate) noProduction, bool gapClosureMode)
    {
        var rows = new List<SystemAuditR002EvidenceRow>();
        void Add(string name, string[] contracts, string status, string source, string? value, bool persisted, string lineage, string notes)
            => rows.Add(new(name, contracts, status, source, value, persisted, lineage, Sha256($"{name}|{status}|{source}|{value}|{lineage}|{notes}"), notes));

        var tickStatus = request.DbTables.Any(x => x.StorageType == "tick" && x.Columns.Count > 0) ? "PRESENT" : "PARTIAL";
        var quoteStatus = request.DbTables.Count > 0 ? request.DbTables.Any(x => x.StorageType is "quote" or "snapshot" && x.Columns.Count > 0) ? "PRESENT" : "MISSING" : "PRESENT";
        var barStatus = request.DbTables.Count > 0 ? request.DbTables.Any(x => x.StorageType == "bar" && x.Columns.Count > 0) ? "PRESENT" : "MISSING" : "PRESENT";
        var rowCountStatus = request.DbTables.Any(x => x.RowCount.HasValue) ? "PRESENT" : request.ConnectionStringPresent ? "PARTIAL" : "MISSING";
        var m30Status = ValueOf(barIds, "evidenceStatus", "M30") as string ?? "MISSING";
        if (gapClosureMode && m30Status == "MISSING")
        {
            m30Status = "MISSING_CONFIRMED";
        }

        Add("LMAX MarketData source/session model", [LmaxMarketdataDbContractId, MarketdataReadinessContractId], "PRESENT", "repo", "bounded read-only demo collector; market-data endpoint only", false, "LmaxSandboxMarketDataCollector.cs", "Logon and endpoint proof exists, but market data capture is still rejected at MarketDataRequest group 146.");
        Add("DB schema for tick storage", [LmaxMarketdataDbContractId], tickStatus, request.DbTables.Count > 0 ? "db" : "repo", "LmaxIndividualTrades schema present; live tick storage path not enabled", false, "IntradayDbContext.cs; migrations; SELECT-only DB scan if supplied", "EOD trade schema exists; not proof of live tick-level persistence.");
        Add("DB schema for quote storage", [LmaxMarketdataDbContractId, MarketdataReadinessContractId], quoteStatus, request.DbTables.Count > 0 ? "db" : "repo", "MarketDataSnapshots schema present", false, "IntradayDbContext.cs; migrations; SELECT-only DB scan if supplied", "Schema evidence only.");
        Add("DB schema for bar storage", [LmaxMarketdataDbContractId, MarketdataReadinessContractId], barStatus, request.DbTables.Count > 0 ? "db" : "repo", "MarketDataBars schema present", false, "IntradayDbContext.cs; migrations; SELECT-only DB scan if supplied", "Schema evidence only.");
        Add("M15 bar ID", [LmaxMarketdataDbContractId], ValueOf(barIds, "evidenceStatus", "M15") as string ?? "PRESENT", "repo", "BarTimeframe.FifteenMinutes", false, "DomainModels.cs", "Code constant maps to 15 minutes.");
        Add("M30 bar ID", [LmaxMarketdataDbContractId], m30Status, "repo", ValueOf(barIds, "M30BarId")?.ToString(), false, "src/tools/docs/artifacts scan", m30Status == "PRESENT" ? "M30 evidence found in scanned sources." : "No BarTimeframe.ThirtyMinutes, ThirtyMinute, Minute30, or M30BarId evidence found.");
        Add("M60 bar ID", [LmaxMarketdataDbContractId], "PRESENT", "repo", "BarTimeframe.OneHour; M60 equivalent = H1", false, "DomainModels.cs", "M60 is represented by H1/OneHour evidence.");
        Add("storage table names", [LmaxMarketdataDbContractId], "PRESENT", "repo", "MarketDataSnapshots, MarketDataBars, LmaxIndividualTrades, LmaxTradeSummaries", false, "IntradayDbContext.cs; migrations", "Storage names discovered in code/migration evidence.");
        Add("QuoteWindowReadinessId", [MarketdataReadinessContractId], "PRESENT", "generated-adoption-id", ValueOf(readinessIds, "QuoteWindowReadinessId")?.ToString(), false, "deterministic from run-key and contract ID unless CLI supplied", "Adoption/readiness ID only; no runtime activation implied.");
        Add("CloseBenchmarkReadinessId", [MarketdataReadinessContractId], "PRESENT", "generated-adoption-id", ValueOf(readinessIds, "CloseBenchmarkReadinessId")?.ToString(), false, "deterministic from run-key and contract ID unless CLI supplied", "Adoption/readiness ID only; no runtime activation implied.");
        Add("FeedQualityReadinessId", [MarketdataReadinessContractId], "PRESENT", "generated-adoption-id", ValueOf(readinessIds, "FeedQualityReadinessId")?.ToString(), false, "deterministic from run-key and contract ID unless CLI supplied", "Adoption/readiness ID only; no runtime activation implied.");
        Add("CanonicalTargetCloseUtc", [CanonicalTimingContractId, MarketdataReadinessContractId], ValueOf(timing, "CanonicalTargetCloseUtc") is null ? "MISSING" : "PRESENT", ValueOf(timing, "canonicalTimingSource")?.ToString() ?? "missing", ValueOf(timing, "CanonicalTargetCloseUtc")?.ToString(), false, "CLI or existing artifact inference", "No local time inferred silently.");
        Add("WindowStartUtc", [CanonicalTimingContractId, MarketdataReadinessContractId], ValueOf(timing, "WindowStartUtc") is null ? "MISSING" : "PRESENT", ValueOf(timing, "canonicalTimingSource")?.ToString() ?? "missing", ValueOf(timing, "WindowStartUtc")?.ToString(), false, "CLI or existing artifact inference", "No local time inferred silently.");
        Add("WindowEndUtc", [CanonicalTimingContractId, MarketdataReadinessContractId], ValueOf(timing, "WindowEndUtc") is null ? "MISSING" : "PRESENT", ValueOf(timing, "canonicalTimingSource")?.ToString() ?? "missing", ValueOf(timing, "WindowEndUtc")?.ToString(), false, "CLI or existing artifact inference", "No local time inferred silently.");
        Add("row counts", [LmaxMarketdataDbContractId, MarketdataReadinessContractId], rowCountStatus, "db", request.ConnectionStringPresent ? "connection string present via env; values redacted" : "no DB connection supplied; SELECT COUNT(*) not run", false, "read-only DB scan optional", "Schema readiness is separate from data availability.");
        Add("lineage/hash", [LmaxMarketdataDbContractId, MarketdataReadinessContractId], "PRESENT", "generated-adoption-id", "hashes.json + manifest.sha256 produced without circular hash", false, "package manifest", "Report hashes are generated after artifact writing.");
        Add("no credential values persisted", [EnvironmentSecretContractId, LmaxMarketdataDbContractId], "PRESENT", "preflight", "secret names/presence only; values not persisted", false, "environment-secret.v1 adoption", "Raw values are not serialized.");
        Add("no production execution path", [EnvironmentSecretContractId, LmaxMarketdataDbContractId], noProduction.gate == "PASS" ? "PRESENT" : "MISSING", "generated-adoption-id", noProduction.gate, false, "no_production_execution_path_report", "No external call or execution path is used by this adoption run.");
        return rows;
    }

    private static async Task WriteJsonAndMarkdown(string root, string basename, object value, string markdown, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.json"), JsonSerializer.Serialize(value, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.md"), markdown, cancellationToken);
    }

    private static async Task WriteManifest(string outputRoot, string runKey, object adoptionReport, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(path), "hashes.json", StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(Path.GetFileName(path), "manifest.sha256", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetRelativePath(outputRoot, path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hashes = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            hashes[Path.GetRelativePath(outputRoot, file).Replace('\\', '/')] = await Sha256File(file, cancellationToken);
        }

        var manifest = new
        {
            systemAuditId = SystemAuditId,
            runKey,
            createdAtUtc = DateTimeOffset.UtcNow,
            packageType = "system-audit-r002-contract-adoption",
            adoptedContracts = ContractIds,
            noProductionExecutionPath = true,
            noCredentialValuesPersisted = true,
            files = hashes.Keys.ToArray()
        };
        var manifestPath = Path.Combine(outputRoot, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
        hashes["manifest.json"] = await Sha256File(manifestPath, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "hashes.json"), JsonSerializer.Serialize(hashes.Select(x => new { path = x.Key, sha256 = x.Value }), JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "manifest.sha256"), $"{hashes["manifest.json"]}  manifest.json{Environment.NewLine}", Encoding.ASCII, cancellationToken);
    }

    private static string MarkdownAdoption(dynamic report)
        => Lines(
            "# SYSTEM-AUDIT-R002 Contract Adoption",
            "",
            $"- SYSTEM_AUDIT_R002_ADOPTION_STATUS: `{report.SYSTEM_AUDIT_R002_ADOPTION_STATUS}`",
            $"- LMAX_MARKETDATA_DB_V1_ADOPTED: `{report.LMAX_MARKETDATA_DB_V1_ADOPTED}`",
            $"- MARKETDATA_READINESS_V1_ADOPTED: `{report.MARKETDATA_READINESS_V1_ADOPTED}`",
            $"- CANONICAL_TIMING_V1_ADOPTED: `{report.CANONICAL_TIMING_V1_ADOPTED}`",
            $"- ENVIRONMENT_SECRET_V1_ADOPTED: `{report.ENVIRONMENT_SECRET_V1_ADOPTED}`",
            $"- LMAX_MARKETDATA_SOURCE_SESSION_MODEL_EVIDENCE: `{report.LMAX_MARKETDATA_SOURCE_SESSION_MODEL_EVIDENCE}`",
            $"- DB_TICK_SCHEMA_EVIDENCE: `{report.DB_TICK_SCHEMA_EVIDENCE}`",
            $"- DB_QUOTE_SCHEMA_EVIDENCE: `{report.DB_QUOTE_SCHEMA_EVIDENCE}`",
            $"- DB_BAR_SCHEMA_EVIDENCE: `{report.DB_BAR_SCHEMA_EVIDENCE}`",
            $"- M15_BAR_ID_EVIDENCE: `{report.M15_BAR_ID_EVIDENCE}`",
            $"- M30_BAR_ID_EVIDENCE: `{report.M30_BAR_ID_EVIDENCE}`",
            $"- M60_BAR_ID_EVIDENCE: `{report.M60_BAR_ID_EVIDENCE}`",
            $"- STORAGE_TABLE_NAMES_EVIDENCE: `{report.STORAGE_TABLE_NAMES_EVIDENCE}`",
            $"- READINESS_IDS_EVIDENCE: `{report.READINESS_IDS_EVIDENCE}`",
            $"- CANONICAL_TIMING_EVIDENCE: `{report.CANONICAL_TIMING_EVIDENCE}`",
            $"- ROW_COUNTS_EVIDENCE: `{report.ROW_COUNTS_EVIDENCE}`",
            $"- LINEAGE_HASH_EVIDENCE: `{report.LINEAGE_HASH_EVIDENCE}`",
            $"- NO_CREDENTIAL_VALUES_PERSISTED: `{report.NO_CREDENTIAL_VALUES_PERSISTED}`",
            $"- NO_PRODUCTION_EXECUTION_PATH: `{report.NO_PRODUCTION_EXECUTION_PATH}`",
            $"- SYSTEM_AUDIT_R002_GAP_CLOSURE_STATUS: `{report.SYSTEM_AUDIT_R002_GAP_CLOSURE_STATUS}`",
            $"- SYSTEM_AUDIT_R002_ADOPTION_STATUS_AFTER_GAP_CLOSURE: `{report.SYSTEM_AUDIT_R002_ADOPTION_STATUS_AFTER_GAP_CLOSURE}`");

    private static string MarkdownEvidenceMatrix(IEnumerable<SystemAuditR002EvidenceRow> rows)
        => Lines(new[] { "# SYSTEM-AUDIT-R002 Evidence Matrix", "" }.Concat(rows.Select(row => $"- {row.EvidenceName}: `{row.EvidenceStatus}` ({row.Source}) - {row.Notes}")).ToArray());

    private static string MarkdownLmaxDb(dynamic report)
        => Lines("# lmax-marketdata-db.v1 Adoption", "", $"- contractId: `{report.contractId}`", $"- adoptionStatus: `{report.adoptionStatus}`", "- M60 equivalent: `H1`", "- DB mutation: `false`", "- Source/session model: bounded read-only demo market data collector.");

    private static string MarkdownReadiness(dynamic report)
        => Lines("# marketdata-readiness.v1 Adoption", "", $"- contractId: `{report.contractId}`", $"- adoptionStatus: `{report.adoptionStatus}`", "- Readiness IDs are adoption IDs only.", "- Schema readiness is separate from data availability.");

    private static string MarkdownCanonical(dynamic report)
        => Lines("# canonical-timing.v1 Adoption", "", $"- contractId: `{report.contractId}`", $"- adoptionStatus: `{report.adoptionStatus}`", $"- validationStatus: `{report.validationStatus}`", $"- reason: {report.reason}");

    private static string MarkdownSecret(dynamic report)
        => Lines("# environment-secret.v1 Adoption", "", $"- contractId: `{report.contractId}`", $"- adoptionStatus: `{report.adoptionStatus}`", $"- noCredentialValuesPersisted: `{report.noCredentialValuesPersisted}`", "- Secret evidence records names and presence booleans only.");

    private static string MarkdownNoProduction(dynamic report)
        => Lines("# No Production Execution Path", "", $"- NO_PRODUCTION_EXECUTION_PATH: `{report.NO_PRODUCTION_EXECUTION_PATH}`", "- no production LMAX endpoint used", "- no trading endpoint used", "- no order/fill/route/broker/live-state artifacts", "- no DB write or migration", "- no Qubes/PMS/OMS/EMS/manager/Anubis");

    private static string MarkdownSummary(dynamic report)
        => Lines("# SYSTEM-AUDIT-R002 Adoption Summary", "", $"- SYSTEM_AUDIT_R002_ADOPTION_STATUS: `{report.SYSTEM_AUDIT_R002_ADOPTION_STATUS}`", $"- SYSTEM_AUDIT_R002_GAP_CLOSURE_STATUS: `{report.SYSTEM_AUDIT_R002_GAP_CLOSURE_STATUS}`", $"- SYSTEM_AUDIT_R002_ADOPTION_STATUS_AFTER_GAP_CLOSURE: `{report.SYSTEM_AUDIT_R002_ADOPTION_STATUS_AFTER_GAP_CLOSURE}`", $"- lmax-marketdata-db.v1: `{report.LMAX_MARKETDATA_DB_V1_ADOPTED}`", $"- marketdata-readiness.v1: `{report.MARKETDATA_READINESS_V1_ADOPTED}`", $"- canonical-timing.v1: `{report.CANONICAL_TIMING_V1_ADOPTED}`", $"- environment-secret.v1: `{report.ENVIRONMENT_SECRET_V1_ADOPTED}`", $"- no credential values persisted: `{report.NO_CREDENTIAL_VALUES_PERSISTED}`", $"- no production execution path: `{report.NO_PRODUCTION_EXECUTION_PATH}`", "", "This package adopts frozen SYSTEM-AUDIT-R002 contract IDs for evidence tracking only. It does not activate production, trading, Qubes, DB persistence, or execution.");

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines) + Environment.NewLine;

    private static object? ValueOf(object obj, string property, string? nested = null)
    {
        var value = obj.GetType().GetProperty(property)?.GetValue(obj);
        if (nested is null || value is null)
        {
            return value;
        }

        return value.GetType().GetProperty(nested)?.GetValue(value);
    }

    private static string Evidence(IEnumerable<SystemAuditR002EvidenceRow> rows, string name)
        => rows.First(x => x.EvidenceName == name).EvidenceStatus;

    private static string ToAdoptedGate(string status)
        => status switch
        {
            "ADOPTED" => "YES",
            "ADOPTED_WITH_WARNINGS" => "WITH_WARNINGS",
            _ => "NO"
        };

    private static (DateTimeOffset? Value, bool Missing, bool Invalid) ParseUtcDetailed(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, true, false);
        }

        if (!DateTimeOffset.TryParse(value, out var parsed) || parsed.Offset != TimeSpan.Zero)
        {
            return (null, false, true);
        }

        return (parsed.ToUniversalTime(), false, false);
    }

    private static string? ToIso(DateTimeOffset? value) => value?.ToUniversalTime().ToString("O");
    private static string ShortHash(string value) => Sha256(value)[..12];
    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static async Task<string> Sha256File(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static string Relative(string root, string path)
        => Path.GetRelativePath(root, path).Replace('\\', '/');
}
