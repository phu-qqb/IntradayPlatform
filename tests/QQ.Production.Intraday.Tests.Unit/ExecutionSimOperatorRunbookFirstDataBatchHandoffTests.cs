using System.Text.Json;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimOperatorRunbookFirstDataBatchHandoffTests
{
    [Fact]
    public void Required_runbook_and_handoff_artifacts_exist()
    {
        foreach (var artifact in RequiredArtifacts)
        {
            Assert.True(File.Exists(Path.Combine(ArtifactsDir(), artifact)), $"Missing R009 artifact {artifact}");
        }
    }

    [Fact]
    public void Runbook_states_no_api_lmax_broker_orders_routes_submissions_or_automatic_execution()
    {
        var runbook = ReadText("phase-exec-sim-r009-operator-runbook.md");
        var runbookJson = ReadJson("phase-exec-sim-r009-operator-runbook.json");

        Assert.Contains("This runbook does not authorize API calls", runbook);
        Assert.Contains("Do not call Polygon, LMAX, or any external API", runbook);
        Assert.Contains("Do not execute trades", runbook);
        Assert.Contains("Do not use CLI automation", runbook);
        Assert.True(runbookJson.RootElement.GetProperty("statesNoApiCalls").GetBoolean());
        Assert.True(runbookJson.RootElement.GetProperty("statesNoBrokerOrLmaxCalls").GetBoolean());
        Assert.True(runbookJson.RootElement.GetProperty("statesNoOrdersFillsReportsRoutesSubmissions").GetBoolean());
        Assert.True(runbookJson.RootElement.GetProperty("statesNoAutomaticExecution").GetBoolean());
        Assert.True(runbookJson.RootElement.GetProperty("statesNoValidationImportBacktestExecutionInR009").GetBoolean());
    }

    [Fact]
    public void First_data_batch_checklist_manifest_template_and_examples_exist()
    {
        var checklist = ReadJson("phase-exec-sim-r009-first-data-batch-checklist.json");
        var template = ReadJson("phase-exec-sim-r009-manifest-template.json");
        var valid = ReadJson("phase-exec-sim-r009-valid-manifest-example.json");
        var invalid = ReadJson("phase-exec-sim-r009-invalid-manifest-examples.json");

        Assert.Contains("EURUSD", checklist.RootElement.GetProperty("minimumRequiredFiles").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("USDJPY", checklist.RootElement.GetProperty("minimumRequiredFiles").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("AUDUSD", checklist.RootElement.GetProperty("minimumRequiredFiles").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("PolygonOfflineFile", template.RootElement.GetProperty("ProviderName").GetString());
        Assert.Equal("HistoricalBboQuotes", template.RootElement.GetProperty("ProviderDatasetType").GetString());
        Assert.False(template.RootElement.GetProperty("ContainsRawProviderPayload").GetBoolean());
        Assert.False(template.RootElement.GetProperty("ContainsSecrets").GetBoolean());
        Assert.Equal("C:EUR-USD", valid.RootElement.GetProperty("example").GetProperty("ProviderSymbol").GetString());
        Assert.True(invalid.RootElement.GetProperty("noInvalidExampleIsExecutable").GetBoolean());
    }

    [Fact]
    public void Interpretation_decision_troubleshooting_forbidden_and_handoff_guides_exist()
    {
        Assert.Contains("AcceptedForSanitizedImport", ReadText("phase-exec-sim-r009-validation-interpretation-guide.md"));
        Assert.Contains("Slippage vs close", ReadText("phase-exec-sim-r009-tca-interpretation-guide.md"));
        Assert.Contains("Do not approve", ReadText("phase-exec-sim-r009-operator-decision-guide.md"));
        Assert.Contains("Missing timestamp", ReadText("phase-exec-sim-r009-troubleshooting-guide.md"));
        Assert.Contains("No Polygon API call", ReadText("phase-exec-sim-r009-forbidden-actions-checklist.md"));
        Assert.Contains("No validation/import/backtest execution in R009", ReadText("phase-exec-sim-r009-forbidden-actions-checklist.md"));
        Assert.Contains("Next phase explicitly authorized", ReadText("phase-exec-sim-r009-handoff-checklist.md"));
    }

    [Fact]
    public void Cost_guidance_preserves_best_case_only_and_nonmajor_calibration()
    {
        var cost = ReadJson("phase-exec-sim-r009-cost-bucket-guidance.json");

        Assert.Equal(5, cost.RootElement.GetProperty("bestCaseMajorTargetUsdPerMillion").GetInt32());
        Assert.True(cost.RootElement.GetProperty("fiveUsdPerMillionBestCaseOnly").GetBoolean());
        Assert.False(cost.RootElement.GetProperty("fiveUsdPerMillionUniversalized").GetBoolean());
        Assert.True(cost.RootElement.GetProperty("nonMajorEmScandiCnhRequireLiquidityCalibration").GetBoolean());
    }

    [Fact]
    public void Direct_cross_guidance_preserves_signal_only_netting_first_rule()
    {
        var directCross = ReadJson("phase-exec-sim-r009-direct-cross-guidance.json");

        Assert.True(directCross.RootElement.GetProperty("rawDirectCrossesAreSignalInputsOnly").GetBoolean());
        Assert.True(directCross.RootElement.GetProperty("requiresNettingFirst").GetBoolean());
        Assert.Equal("USD-pair-only", directCross.RootElement.GetProperty("executionUniverse").GetString());
        Assert.False(directCross.RootElement.GetProperty("directCrossExecutionAllowedByDefault").GetBoolean());
        Assert.True(directCross.RootElement.GetProperty("futureEnablementRequiresExplicitCostComparisonGate").GetBoolean());
        Assert.False(directCross.RootElement.GetProperty("guidanceWeakened").GetBoolean());
    }

    [Fact]
    public void Audusd_and_usdjpy_caveats_remain_preserved()
    {
        var usdjpy = ReadJson("phase-exec-sim-r009-usdjpy-caveat-preservation.json");
        var lmax = ReadJson("phase-exec-sim-r009-lmax-readonly-baseline-reference.json");
        var symbol = ReadJson("phase-exec-sim-r009-symbol-coverage-guidance.json");

        Assert.True(usdjpy.RootElement.GetProperty("caveatPreserved").GetBoolean());
        Assert.Equal("4004", usdjpy.RootElement.GetProperty("securityId").GetString());
        Assert.Equal("8", usdjpy.RootElement.GetProperty("securityIdSource").GetString());
        Assert.Contains("not failed", lmax.RootElement.GetProperty("audusdStatus").GetString());
        Assert.Contains("not failed", symbol.RootElement.GetProperty("audusdStatus").GetString());
    }

    [Fact]
    public void No_validation_import_backtest_or_new_data_batch_is_executed_in_r009()
    {
        var audit = ReadJson("phase-exec-sim-r009-no-validation-import-backtest-execution-audit.json");
        var noExternal = ReadJson("phase-exec-sim-r009-no-external-audit.json");

        Assert.False(audit.RootElement.GetProperty("newQuoteFileValidationRunExecuted").GetBoolean());
        Assert.False(audit.RootElement.GetProperty("newImportExecuted").GetBoolean());
        Assert.False(audit.RootElement.GetProperty("newBacktestExecuted").GetBoolean());
        Assert.False(audit.RootElement.GetProperty("newDataBatchProcessed").GetBoolean());
        Assert.True(audit.RootElement.GetProperty("staticRunbookFocused").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("validationImportBacktestExecuted").GetBoolean());
        Assert.False(noExternal.RootElement.GetProperty("newDataBatchProcessed").GetBoolean());
    }

    [Fact]
    public void No_external_runtime_order_or_fill_audits_are_clean()
    {
        var api = ReadJson("phase-exec-sim-r009-no-api-call-audit.json");
        var runtime = ReadJson("phase-exec-sim-r009-no-broker-marketdata-runtime-audit.json");
        var order = ReadJson("phase-exec-sim-r009-no-order-fill-report-route-audit.json");
        var forbidden = ReadJson("phase-exec-sim-r009-forbidden-actions-audit.json");

        Assert.False(api.RootElement.GetProperty("polygonApiCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("lmaxCalled").GetBoolean());
        Assert.False(api.RootElement.GetProperty("externalApiCalled").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("brokerActivationDetected").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("socketOpened").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("marketDataRequestSent").GetBoolean());
        Assert.False(runtime.RootElement.GetProperty("automaticExecutionIntroduced").GetBoolean());
        Assert.False(order.RootElement.GetProperty("ordersCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("fillsCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("executionReportsCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("routesCreated").GetBoolean());
        Assert.False(order.RootElement.GetProperty("submissionsCreated").GetBoolean());
        Assert.False(forbidden.RootElement.GetProperty("forbiddenActionsDetected").GetBoolean());
    }

    private static readonly string[] RequiredArtifacts =
    [
        "phase-exec-sim-r009-summary.md",
        "phase-exec-sim-r009-operator-runbook.md",
        "phase-exec-sim-r009-operator-runbook.json",
        "phase-exec-sim-r009-first-data-batch-checklist.md",
        "phase-exec-sim-r009-first-data-batch-checklist.json",
        "phase-exec-sim-r009-manifest-template.json",
        "phase-exec-sim-r009-valid-manifest-example.json",
        "phase-exec-sim-r009-invalid-manifest-examples.json",
        "phase-exec-sim-r009-validation-interpretation-guide.md",
        "phase-exec-sim-r009-tca-interpretation-guide.md",
        "phase-exec-sim-r009-operator-decision-guide.md",
        "phase-exec-sim-r009-troubleshooting-guide.md",
        "phase-exec-sim-r009-forbidden-actions-checklist.md",
        "phase-exec-sim-r009-handoff-checklist.md",
        "phase-exec-sim-r009-direct-cross-guidance.json",
        "phase-exec-sim-r009-cost-bucket-guidance.json",
        "phase-exec-sim-r009-symbol-coverage-guidance.json",
        "phase-exec-sim-r009-no-validation-import-backtest-execution-audit.json"
    ];

    private static JsonDocument ReadJson(string fileName)
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(ArtifactsDir(), fileName)));

    private static string ReadText(string fileName)
        => File.ReadAllText(Path.Combine(ArtifactsDir(), fileName));

    private static string ArtifactsDir()
        => Path.Combine(RepoRoot(), "artifacts/readiness/execution-sim");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
