$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactsRoot = Join-Path $RepoRoot "artifacts\readiness\execution-sim"

function Fail([string]$Message) {
    Write-Error "EXEC-SIM-R051 validation failed: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path"
    }
    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Has-Prop($Object, [string]$Name) {
    return $Object.PSObject.Properties.Name -contains $Name
}

function Field($Object, [string]$Name) {
    if (-not (Has-Prop $Object $Name)) {
        return $null
    }
    return $Object.$Name
}

function Assert-FalseIfPresent($Object, [string]$Name, [string]$Message) {
    if ((Has-Prop $Object $Name) -and $Object.$Name -ne $false) {
        Fail $Message
    }
}

function Assert-TrueIfPresent($Object, [string]$Name, [string]$Message) {
    if ((Has-Prop $Object $Name) -and $Object.$Name -ne $true) {
        Fail $Message
    }
}

function Assert-ContainsValue($Values, [string]$Expected, [string]$Message) {
    if ($Values -notcontains $Expected) {
        Fail $Message
    }
}

$requiredArtifacts = @(
    "phase-exec-sim-r051-summary.md",
    "phase-exec-sim-r051-date-selection-plan.json",
    "phase-exec-sim-r051-download-command-plan.json",
    "phase-exec-sim-r051-validation-gate-plan.json",
    "phase-exec-sim-r051-no-external-audit.json",
    "phase-exec-sim-r051-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$r050Summary = Join-Path $ArtifactsRoot "phase-exec-sim-r050-summary.md"
if (-not (Test-Path -LiteralPath $r050Summary)) {
    Fail "R050 summary missing; R051 must reuse R050 as source planning gate"
}

$r050Text = Get-Content -Raw -LiteralPath $r050Summary
foreach ($classification in @(
    "EXEC_SIM_R050_PASS_BROADER_OFFLINE_EVALUATION_PLAN_READY_NO_EXTERNAL",
    "EXEC_SIM_R050_PASS_ADDITIONAL_DATE_REGIME_REQUIREMENTS_READY_NO_EXTERNAL",
    "EXEC_SIM_R050_PASS_INSTRUMENT_COVERAGE_DECISION_READY_NO_EXTERNAL",
    "EXEC_SIM_R050_PASS_NO_DOWNLOAD_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
)) {
    if ($r050Text -notmatch [regex]::Escape($classification)) {
        Fail "R050 accepted classification missing: $classification"
    }
}

$r009 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-r009-contract-reference.json")
if ($r009.ContractVersion -ne "0.3.0-design-only-candidate") { Fail "R009 contract reference missing" }
Assert-TrueIfPresent $r009 "NonExecutable" "R009 non-executable status not preserved"
Assert-FalseIfPresent $r009 "ExecutablePromotionAuthorized" "R009 executable promotion authorized"

$r049 = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r050-r049-result-reference.json")
if ((Field $r049 "CandidateTcaResultCoverage") -ne "7560/7560") { Fail "R049 compact-grid reference missing" }
Assert-FalseIfPresent $r049 "ExecutablePromotionAuthorized" "R049 reference authorizes executable promotion"

$datePlan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-date-selection-plan.json")
if ($datePlan.SourcePlanningGate -ne "EXEC-SIM-R050") { Fail "R051 does not reuse R050 as source gate" }
if ($datePlan.SelectedAdditionalTradingDateCount -lt 20) { Fail "R051 selected fewer than 20 additional dates" }
if ($datePlan.SelectedDates.Count -lt 20) { Fail "R051 selected date list has fewer than 20 dates" }
Assert-TrueIfPresent $datePlan "R009ContractPreservedAsNonExecutable" "R009 non-executable preservation missing"
Assert-TrueIfPresent $datePlan "R049CompactGridResultsReferenceOnly" "R049 reference-only preservation missing"
Assert-TrueIfPresent $datePlan "UniverseUnchanged" "Seven-symbol universe not preserved"
Assert-TrueIfPresent $datePlan "NonMajorEmScandiCnhDeferred" "Nonmajor / EM / scandi / CNH not deferred"
Assert-TrueIfPresent $datePlan "DirectCrossesExecutionDisabled" "Direct crosses not execution-disabled"
Assert-TrueIfPresent $datePlan "CanonicalQuarterHourClosePolicyPreserved" "Canonical quarter-hour policy not preserved"
Assert-FalseIfPresent $datePlan "Legacy06LabelsFutureCanonical" "Legacy :06 labels used as future canonical"
Assert-FalseIfPresent $datePlan "DownloadsExecutedInR051" "Downloads executed in R051"
Assert-FalseIfPresent $datePlan "QuoteValidationExecutedInR051" "Quote validation executed in R051"
Assert-FalseIfPresent $datePlan "SimulationExecutedInR051" "Simulation executed in R051"
Assert-FalseIfPresent $datePlan "BacktestExecutedInR051" "Backtest executed in R051"
Assert-FalseIfPresent $datePlan "TcaResultLinesProducedInR051" "TCA result lines produced in R051"
Assert-FalseIfPresent $datePlan "ExecutablePromotionAuthorized" "Executable promotion authorized in date plan"

$regimeBuckets = $datePlan.MarketRegimeBucketsCovered
foreach ($bucket in @(
    "normal_liquidity",
    "higher_volatility_or_macro_like_operator_review",
    "lower_liquidity_holiday_adjacent",
    "month_end_quarter_end"
)) {
    Assert-ContainsValue $regimeBuckets $bucket "Missing regime bucket: $bucket"
}

$coreSymbols = $datePlan.CoreUsdPairUniverse
foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")) {
    Assert-ContainsValue $coreSymbols $symbol "Missing core symbol: $symbol"
}
if ($coreSymbols.Count -ne 7) { Fail "Core symbol universe changed" }

$downloadPlan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-download-command-plan.json")
Assert-TrueIfPresent $downloadPlan "CommandPlanOnly" "Download artifact is not command-plan-only"
Assert-TrueIfPresent $downloadPlan "AllCommandsMarkedNotExecuted" "Not all future commands are marked not executed"
Assert-FalseIfPresent $downloadPlan "DownloadsExecutedInR051" "Downloads executed in R051 download plan"
Assert-FalseIfPresent $downloadPlan "DownloadCommandsExecutedInR051" "Download commands executed in R051"
Assert-FalseIfPresent $downloadPlan "ExternalApiCalled" "External API called in R051"
Assert-FalseIfPresent $downloadPlan "PolygonApiCalled" "Polygon API called in R051"
Assert-FalseIfPresent $downloadPlan "MassiveApiCalled" "Massive API called in R051"
Assert-FalseIfPresent $downloadPlan "LmaxCalled" "LMAX called in R051"
Assert-FalseIfPresent $downloadPlan "ExecutablePromotionAuthorized" "Executable promotion authorized in download plan"
Assert-TrueIfPresent $downloadPlan "NonMajorEmScandiCnhDeferred" "Nonmajor instruments not deferred in download plan"
Assert-TrueIfPresent $downloadPlan "DirectCrossesExecutionDisabled" "Direct crosses not disabled in download plan"
Assert-ContainsValue $downloadPlan.RequiredFutureSafetyFlags "--execute" "Future command safety flag missing: --execute"
Assert-ContainsValue $downloadPlan.RequiredFutureSafetyFlags "--allow-offline-quote-download" "Future command safety flag missing: --allow-offline-quote-download"
Assert-ContainsValue $downloadPlan.RequiredFutureSafetyFlags "--no-import" "Future command safety flag missing: --no-import"
Assert-ContainsValue $downloadPlan.RequiredFutureSafetyFlags "--no-simulation" "Future command safety flag missing: --no-simulation"
Assert-ContainsValue $downloadPlan.RequiredFutureSafetyFlags "--no-backtest" "Future command safety flag missing: --no-backtest"
Assert-ContainsValue $downloadPlan.RequiredFutureSafetyFlags "--no-tca-lines" "Future command safety flag missing: --no-tca-lines"
foreach ($command in $downloadPlan.FutureCommands) {
    if ($command.ExecutionStatus -ne "NotExecuted") { Fail "Future command is not marked NotExecuted" }
    if ($command.MayBeExecutedInR051 -ne $false) { Fail "Future command may be executed in R051" }
    if ($command.RequiresFutureOperatorApproval -ne $true) { Fail "Future command lacks operator approval requirement" }
}

$validationPlan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-validation-gate-plan.json")
Assert-TrueIfPresent $validationPlan "ValidationPlanOnly" "Validation artifact is not plan-only"
Assert-FalseIfPresent $validationPlan "ValidationExecutedInR051" "Validation executed in R051"
Assert-FalseIfPresent $validationPlan "QuotesImportedInR051" "Quotes imported in R051"
Assert-FalseIfPresent $validationPlan "PersistedSanitizedRowsCreatedInR051" "Persisted rows created in R051"
Assert-FalseIfPresent $validationPlan "SimulationExecutedInR051" "Simulation executed in validation plan"
Assert-FalseIfPresent $validationPlan "BacktestExecutedInR051" "Backtest executed in validation plan"
Assert-FalseIfPresent $validationPlan "TcaResultLinesProducedInR051" "TCA lines produced in validation plan"
Assert-FalseIfPresent $validationPlan "ExecutablePromotionAuthorized" "Executable promotion authorized in validation plan"
foreach ($criterion in @(
    "missing quotes",
    "malformed timestamps",
    "timezone mismatch",
    "wrong close policy",
    "symbol coverage mismatch",
    "spread/cost assumptions unsupported",
    "insufficient additional dates"
)) {
    Assert-ContainsValue $validationPlan.StopCriteria $criterion "Missing stop criterion: $criterion"
}
foreach ($criterion in @(
    "at least 20 additional dates downloaded in a future phase",
    "all seven symbols covered",
    "canonical close grid complete",
    "no executable schedules created",
    "no orders created",
    "no fills created",
    "no routes created"
)) {
    Assert-ContainsValue $validationPlan.SuccessCriteria $criterion "Missing success criterion: $criterion"
}

$audit = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-no-external-audit.json")
foreach ($flag in @(
    "PolygonApiCalled",
    "MassiveApiCalled",
    "LmaxCalled",
    "ExternalApiCalled",
    "FilesDownloaded",
    "DownloadCommandsExecuted",
    "QuotesImported",
    "QuoteValidationExecutedOnNewData",
    "PersistedSanitizedRowsCreated",
    "SimulationExecuted",
    "BacktestExecuted",
    "TcaResultLinesProduced",
    "ExecutableSchedulesCreated",
    "RoutesCreated",
    "OrdersCreated",
    "FillsCreated",
    "SubmissionsCreated",
    "TradingStateMutated",
    "LiveTradingRun",
    "R009PromotedToExecutable",
    "ExecutablePromotionAuthorized",
    "QQProductionCoreQubesTouched"
)) {
    Assert-FalseIfPresent $audit $flag "No-external audit forbidden action detected: $flag"
}

$summaryPath = Join-Path $ArtifactsRoot "phase-exec-sim-r051-summary.md"
$summary = Get-Content -Raw -LiteralPath $summaryPath
foreach ($classification in @(
    "EXEC_SIM_R051_PASS_ADDITIONAL_DATE_SELECTION_READY_NO_EXTERNAL",
    "EXEC_SIM_R051_PASS_DOWNLOAD_COMMAND_PLAN_READY_NO_EXTERNAL",
    "EXEC_SIM_R051_PASS_VALIDATION_GATE_PLAN_READY_NO_EXTERNAL",
    "EXEC_SIM_R051_PASS_NO_DOWNLOAD_NO_SIMULATION_GATE_READY_NO_EXTERNAL"
)) {
    if ($summary -notmatch [regex]::Escape($classification)) {
        Fail "Summary missing classification: $classification"
    }
}

$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-build-test-validator-evidence.json")
if ($evidence.DotnetBuild.Status -notin @("PASS", "PASS_WITH_WARNINGS", "PENDING")) { Fail "Build evidence invalid" }
if ($evidence.DotnetTest.Status -notin @("PASS", "PASS_WITH_WARNINGS", "NOT_FEASIBLE", "PENDING")) { Fail "Test evidence invalid" }
if ($evidence.Validator.Status -notin @("PASS", "PENDING")) { Fail "Validator evidence invalid" }

Write-Host "EXEC-SIM-R051 validator passed."
Write-Host "Classifications:"
Write-Host "EXEC_SIM_R051_PASS_ADDITIONAL_DATE_SELECTION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R051_PASS_DOWNLOAD_COMMAND_PLAN_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R051_PASS_VALIDATION_GATE_PLAN_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R051_PASS_NO_DOWNLOAD_NO_SIMULATION_GATE_READY_NO_EXTERNAL"
