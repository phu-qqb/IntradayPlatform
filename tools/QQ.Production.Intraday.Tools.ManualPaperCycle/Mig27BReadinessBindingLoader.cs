using System.Globalization;
using System.Text.Json;
using QQ.Production.Intraday.Application;

internal sealed record Mig27BReadinessBinding(
    string ContractPath,
    string PmsScope,
    string BrokerAccountId,
    string ReportDate,
    DateTimeOffset TargetCloseUtc,
    string TargetCloseBindingStatus,
    string QuoteWindowReadiness,
    string CloseBenchmarkReadiness,
    string FeedQualityReadiness,
    bool ReadinessLiveClaim,
    string RiskApproval,
    string OperatorApproval,
    IReadOnlyList<string> InternalPaperInputLines,
    IReadOnlyList<string> AcceptedSyntheticPmsSymbols,
    IReadOnlyList<Mig27BExcludedSyntheticPmsSymbol> ExcludedSyntheticPmsSymbols,
    int SecurityIdOnlyExcludedRowCount)
{
    public string CanonicalSession => "TEST_ENV_DESIGN_ONLY";
    public bool CanonicalQuarterHourTimestampConfirmed => TargetCloseUtc.Offset == TimeSpan.Zero &&
        TargetCloseUtc.Second == 0 &&
        TargetCloseUtc.Millisecond == 0 &&
        TargetCloseUtc.Minute % 15 == 0;
    public string ReadinessBindingStatus => "DESIGN_ONLY_ACCEPTED_FOR_NO_ORDER_PREVIEW";
    public string RiskReviewId => "risk_approval_design_only_v2";
    public string OperatorApprovalId => "operator_approval_design_only_v2";
    public bool RiskApprovalDesignOnlyAccepted => RiskApproval.Equals("DESIGN_ONLY_ACCEPTED_FOR_TEST_NO_ORDER_PREVIEW", StringComparison.Ordinal);
    public bool OperatorApprovalDesignOnlyAccepted => OperatorApproval.Equals("DESIGN_ONLY_ACCEPTED_FOR_TEST_NO_ORDER_PREVIEW", StringComparison.Ordinal);
    public bool SecurityIdOnlyRowsNotConsumed => SecurityIdOnlyExcludedRowCount > 0;
}

internal sealed record Mig27BExcludedSyntheticPmsSymbol(string Symbol, string Reason);

internal static class Mig27BReadinessBindingLoader
{
    private const string PmsContractOption = "--pms-input-contract-v3-path";
    private const string ExpectedContractVersion = "mig27b.test-env.pms-input-contract.v3";
    private const string ExpectedScope = "LMAX_TEST_EOD_ONLY";
    private const string ExpectedBrokerAccountId = "1754288005";
    private const string ForbiddenRealAccountId = "921640160";
    private const string ExpectedReportDate = "2026-06-30";
    private const string ExpectedTargetCloseBindingStatus = "READY_FOR_TEST_ENV_DESIGN_ONLY_PMS_PREVIEW";
    private const string ExpectedReadinessPlaceholder = "DESIGN_ONLY_PLACEHOLDER_ACCEPTED_FOR_NO_ORDER_PREVIEW";
    private const string ExpectedDesignOnlyApproval = "DESIGN_ONLY_ACCEPTED_FOR_TEST_NO_ORDER_PREVIEW";
    private const string ExpectedBrokerSendStatus = "DISABLED_NO_ORDER_ENTRY";

    private static readonly string[] RequiredReferenceNames =
    [
        "target_close_binding_v2",
        "quote_window_readiness_v2",
        "close_benchmark_readiness_v2",
        "feed_quality_readiness_v2",
        "risk_approval_design_only_v2",
        "operator_approval_design_only_v2"
    ];

    public static Mig27BReadinessBinding? Load(string[] args)
    {
        var contractPath = GetOption(args, PmsContractOption);
        if (string.IsNullOrWhiteSpace(contractPath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(contractPath);
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"MIG27B readiness contract does not exist: {fullPath}");
        }

        var contractText = File.ReadAllText(fullPath);
        RejectForbiddenRealAccount(contractText, fullPath);
        using var document = JsonDocument.Parse(contractText);
        var root = document.RootElement;

        RequireString(root, "contract_version", ExpectedContractVersion);
        RequireString(root, "pms_scope", ExpectedScope);
        RequireString(root, "broker_account_id", ExpectedBrokerAccountId);
        RequireString(root, "report_date", ExpectedReportDate);
        RequireFalse(root, "real_account_operational_use");
        RequireFalse(root, "pms_handoff_execution_allowed");
        RequireString(root, "target_close_binding_status", ExpectedTargetCloseBindingStatus);

        var targetCloseUtc = ParseUtcTargetClose(RequiredString(root, "target_close_utc"));
        var readiness = RequiredObject(root, "readiness");
        var quoteWindowReadiness = RequireString(readiness, "quote_window", ExpectedReadinessPlaceholder);
        var closeBenchmarkReadiness = RequireString(readiness, "close_benchmark", ExpectedReadinessPlaceholder);
        var feedQualityReadiness = RequireString(readiness, "feed_quality", ExpectedReadinessPlaceholder);
        RequireFalse(readiness, "readiness_live_claim");

        var approvals = RequiredObject(root, "approvals");
        var riskApproval = RequireString(approvals, "risk", ExpectedDesignOnlyApproval);
        var operatorApproval = RequireString(approvals, "operator", ExpectedDesignOnlyApproval);

        if (root.TryGetProperty("blockers", out var blockers) && blockers.ValueKind == JsonValueKind.Array && blockers.GetArrayLength() > 0)
        {
            throw new InvalidOperationException("MIG27B readiness contract still contains blockers.");
        }

        var safety = RequiredObject(root, "safety");
        RequireFalse(safety, "pms_handoff_execution");
        RequireFalse(safety, "broker_order_send");
        RequireFalse(safety, "fix_order_entry_logon");
        RequireFalse(safety, "order_entry");
        RequireFalse(safety, "accountapi");
        RequireFalse(safety, "db_apply");
        RequireFalse(safety, "databento");
        RequireFalse(safety, "lmax_portal_login");
        RequireFalse(safety, "report_download");
        RequireFalse(safety, "real_account_operational_use");
        RequireFalse(safety, "order_entry_enabled");
        RequireString(safety, "broker_send_status", ExpectedBrokerSendStatus);

        var references = RequiredObject(root, "references");
        foreach (var requiredReference in RequiredReferenceNames)
        {
            var referencePath = ResolveReferencedPath(fullPath, references, requiredReference);
            ReadAndValidateReferencedJson(referencePath);
        }

        var fixturePath = ResolveReferencedPath(fullPath, references, "manual_paper_cycle_test_fixture");
        var fixture = LoadSyntheticPmsFixture(fixturePath);
        var adapter = new SyntheticPmsFixtureAdapter().Adapt(new SyntheticPmsFixtureAdapterRequest(
            fixture.AdapterInputLines,
            true,
            true));

        if (!adapter.Succeeded)
        {
            var messages = string.Join("; ", adapter.Issues.Select(x => $"row={x.RowNumber?.ToString(CultureInfo.InvariantCulture) ?? "n/a"} code={x.Code} {x.Message}"));
            throw new InvalidOperationException($"MIG27B readiness contract produced unsupported synthetic PMS rows: {messages}");
        }

        return new Mig27BReadinessBinding(
            fullPath,
            ExpectedScope,
            ExpectedBrokerAccountId,
            ExpectedReportDate,
            targetCloseUtc,
            ExpectedTargetCloseBindingStatus,
            quoteWindowReadiness,
            closeBenchmarkReadiness,
            feedQualityReadiness,
            ReadinessLiveClaim: false,
            riskApproval,
            operatorApproval,
            adapter.InternalPaperInputLines,
            fixture.AcceptedSymbols,
            fixture.ExcludedSymbols,
            fixture.SecurityIdOnlyExcludedRowCount);
    }

    private static Mig27BSyntheticPmsFixture LoadSyntheticPmsFixture(string fixturePath)
    {
        var text = File.ReadAllText(fixturePath);
        RejectForbiddenRealAccount(text, fixturePath);
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        var consumedRows = RequiredArray(root, "consumed_rows")
            .EnumerateArray()
            .Select(row =>
            {
                var symbol = RequiredString(row, "symbol").ToUpperInvariant();
                var weight = row.TryGetProperty("weight", out var weightElement) && weightElement.ValueKind == JsonValueKind.Number
                    ? weightElement.GetDecimal()
                    : throw new InvalidOperationException($"MIG27B synthetic PMS fixture row for {symbol} is missing numeric weight.");
                return new Mig27BFixtureConsumedRow(symbol, weight);
            })
            .ToArray();

        var acceptedRows = consumedRows
            .Where(row => row.Symbol.Equals("EURUSD", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (acceptedRows.Length == 0)
        {
            throw new InvalidOperationException("MIG27B synthetic PMS fixture does not contain an EURUSD row accepted by the current ManualPaperCycle paper gate.");
        }

        var excludedSymbols = consumedRows
            .Where(row => !row.Symbol.Equals("EURUSD", StringComparison.OrdinalIgnoreCase))
            .Select(row => new Mig27BExcludedSyntheticPmsSymbol(row.Symbol, "SyntheticPmsFixtureAdapterCurrentlyAcceptsOnlyEURUSDInPaperGate"))
            .ToArray();
        var securityIdOnlyExcludedRowCount = root.TryGetProperty("excluded_row_count", out var excludedRowCount) && excludedRowCount.ValueKind == JsonValueKind.Number
            ? excludedRowCount.GetInt32()
            : 0;
        var adapterInputLines = acceptedRows
            .Select(row => $"{row.Symbol};{row.Weight.ToString("0.00000000", CultureInfo.InvariantCulture)}")
            .ToArray();

        return new Mig27BSyntheticPmsFixture(
            adapterInputLines,
            acceptedRows.Select(row => row.Symbol).ToArray(),
            excludedSymbols,
            securityIdOnlyExcludedRowCount);
    }

    private static DateTimeOffset ParseUtcTargetClose(string value)
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            throw new InvalidOperationException($"MIG27B target_close_utc is invalid: {value}");
        }

        if (parsed.Offset != TimeSpan.Zero || parsed.Second != 0 || parsed.Millisecond != 0 || parsed.Minute % 15 != 0)
        {
            throw new InvalidOperationException("MIG27B target_close_utc must be a UTC quarter-hour timestamp.");
        }

        return parsed;
    }

    private static string ResolveReferencedPath(string contractPath, JsonElement references, string referenceName)
    {
        var value = RequiredString(references, referenceName);
        var fullPath = Path.IsPathRooted(value)
            ? Path.GetFullPath(value)
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(contractPath) ?? Directory.GetCurrentDirectory(), value));
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"MIG27B readiness reference is missing: {referenceName} -> {fullPath}");
        }

        return fullPath;
    }

    private static void ReadAndValidateReferencedJson(string path)
    {
        var text = File.ReadAllText(path);
        RejectForbiddenRealAccount(text, path);
        using var _ = JsonDocument.Parse(text);
    }

    private static void RejectForbiddenRealAccount(string text, string source)
    {
        if (text.Contains(ForbiddenRealAccountId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"MIG27B readiness input contains forbidden real account {ForbiddenRealAccountId}: {source}");
        }
    }

    private static JsonElement RequiredObject(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"MIG27B readiness contract is missing object '{propertyName}'.");
        }

        return element;
    }

    private static JsonElement RequiredArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"MIG27B readiness contract is missing array '{propertyName}'.");
        }

        return element;
    }

    private static string RequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"MIG27B readiness contract is missing string '{propertyName}'.");
        }

        return element.GetString()!;
    }

    private static string RequireString(JsonElement root, string propertyName, string expected)
    {
        var actual = RequiredString(root, propertyName);
        if (!actual.Equals(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"MIG27B readiness contract field '{propertyName}' expected '{expected}' but found '{actual}'.");
        }

        return actual;
    }

    private static void RequireFalse(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind is not JsonValueKind.False)
        {
            throw new InvalidOperationException($"MIG27B readiness contract field '{propertyName}' must be false.");
        }
    }

    private static string? GetOption(string[] args, string option)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index].Equals(option, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private sealed record Mig27BFixtureConsumedRow(string Symbol, decimal Weight);

    private sealed record Mig27BSyntheticPmsFixture(
        IReadOnlyList<string> AdapterInputLines,
        IReadOnlyList<string> AcceptedSymbols,
        IReadOnlyList<Mig27BExcludedSyntheticPmsSymbol> ExcludedSymbols,
        int SecurityIdOnlyExcludedRowCount);
}

