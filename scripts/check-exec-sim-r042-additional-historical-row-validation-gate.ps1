param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "EXEC-SIM-R042 validation failed: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing artifact: $Path"
    }
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-FalseField($Object, [string]$Field, [string]$Message) {
    if ($null -eq $Object.$Field) {
        Fail "Missing field $Field in $Message"
    }
    if ([bool]$Object.$Field) {
        Fail $Message
    }
}

$requiredFiles = @(
    "phase-exec-sim-r042-summary.md",
    "phase-exec-sim-r042-r041-accepted-files-reference.json",
    "phase-exec-sim-r042-row-level-validation-contract.json",
    "phase-exec-sim-r042-row-level-validation-results.json",
    "phase-exec-sim-r042-row-count-comparison.json",
    "phase-exec-sim-r042-rejected-row-summary.json",
    "phase-exec-sim-r042-duplicate-out-of-order-handling.json",
    "phase-exec-sim-r042-per-date-row-validation-results.json",
    "phase-exec-sim-r042-per-symbol-row-validation-results.json",
    "phase-exec-sim-r042-quote-window-readiness-results.json",
    "phase-exec-sim-r042-per-date-quote-window-readiness.json",
    "phase-exec-sim-r042-per-symbol-quote-window-readiness.json",
    "phase-exec-sim-r042-close-benchmark-readiness-results.json",
    "phase-exec-sim-r042-per-date-close-benchmark-readiness.json",
    "phase-exec-sim-r042-per-symbol-close-benchmark-readiness.json",
    "phase-exec-sim-r042-feed-quality-readiness-results.json",
    "phase-exec-sim-r042-per-date-feed-quality-readiness.json",
    "phase-exec-sim-r042-per-symbol-feed-quality-readiness.json",
    "phase-exec-sim-r042-sanitized-import-readiness-metadata.json",
    "phase-exec-sim-r042-canonical-session-coverage-validation.json",
    "phase-exec-sim-r042-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r042-legacy-compatibility-preservation.json",
    "phase-exec-sim-r042-symbol-provider-mapping-validation.json",
    "phase-exec-sim-r042-inversion-validation.json",
    "phase-exec-sim-r042-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r042-cost-guidance-preservation.json",
    "phase-exec-sim-r042-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r042-no-db-import-audit.json",
    "phase-exec-sim-r042-no-persisted-sanitized-row-audit.json",
    "phase-exec-sim-r042-no-backtest-simulation-audit.json",
    "phase-exec-sim-r042-no-tca-result-lines-audit.json",
    "phase-exec-sim-r042-no-polygon-api-call-audit.json",
    "phase-exec-sim-r042-no-lmax-call-audit.json",
    "phase-exec-sim-r042-no-external-api-call-audit.json",
    "phase-exec-sim-r042-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r042-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r042-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r042-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r042-no-external-audit.json",
    "phase-exec-sim-r042-forbidden-actions-audit.json",
    "phase-exec-sim-r042-next-phase-recommendation.json",
    "phase-exec-sim-r042-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactsRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required artifact $file"
    }
}

$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-row-level-validation-contract.json")
if ($contract.ContractId -ne "EXEC-SIM-R042-ROW-LEVEL-VALIDATION-CONTRACT") {
    Fail "Row-level validation contract id mismatch"
}
if ($contract.LegacyCompatibilityOnly.LegacyIsFutureCanonical -ne $false) {
    Fail "Legacy :06 labels are used as future canonical timestamps"
}

$acceptedRef = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-r041-accepted-files-reference.json")
if ($acceptedRef.AcceptedManifestCount -ne 35) {
    Fail "R041 accepted file reference does not preserve 35 accepted manifests"
}
Assert-FalseField $acceptedRef "ExternalCalls" "R041 accepted reference indicates external calls"
Assert-FalseField $acceptedRef "Downloads" "R041 accepted reference indicates downloads"

$rows = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-row-level-validation-results.json")
if ($rows.FileEntryCount -ne 35 -or $rows.Results.Count -ne 35) {
    Fail "Row-level validation results do not represent all 35 entries"
}
if ($rows.TotalRowCountObserved -le 0 -or $rows.TotalAcceptedRowCount -le 0) {
    Fail "Row-level validation did not record observed/accepted rows"
}
if ($rows.TotalRowCountDeclared -ne $rows.TotalRowCountObserved) {
    Fail "Declared and observed row totals differ"
}

$rowCounts = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-row-count-comparison.json")
if ($rowCounts.FileEntryCount -ne 35 -or $rowCounts.Comparisons.Count -ne 35) {
    Fail "Row-count comparison does not represent all 35 entries"
}
if ($rowCounts.AllDeclaredCountsMatched -ne $true) {
    Fail "Not all declared row counts matched observed row counts"
}

$quoteWindows = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-quote-window-readiness-results.json")
if ($quoteWindows.RecordCount -ne 945 -or $quoteWindows.ExpectedRecordCount -ne 945) {
    Fail "Quote-window readiness results missing expected 945 records"
}

$closeBenchmarks = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-close-benchmark-readiness-results.json")
if ($closeBenchmarks.RecordCount -ne 945 -or $closeBenchmarks.ExpectedRecordCount -ne 945) {
    Fail "Close-benchmark readiness results missing expected 945 records"
}

$feed = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-feed-quality-readiness-results.json")
if ($feed.RecordCount -ne 35 -or $feed.Results.Count -ne 35) {
    Fail "Feed-quality readiness results missing expected 35 records"
}

$coverage = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-canonical-session-coverage-validation.json")
if ($coverage.FullCoverage -ne $true) {
    Fail "Canonical session coverage is not full"
}
if ($coverage.ExpectedQuoteWindowRecords -ne 945 -or $coverage.ActualQuoteWindowRecords -ne 945) {
    Fail "Canonical session quote-window coverage mismatch"
}

$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-canonical-quarter-hour-policy-preservation.json")
if ($canonical.Legacy06UsedAsFutureCanonical -ne $false -or $canonical.Preserved -ne $true) {
    Fail "Canonical quarter-hour policy was not preserved"
}

$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-legacy-compatibility-preservation.json")
if ($legacy.LegacyCompatibilityOnly -ne $true -or $legacy.Legacy06UsedAsFutureCanonical -ne $false) {
    Fail "Legacy compatibility preservation is weakened"
}

$symbols = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-symbol-provider-mapping-validation.json")
if ($symbols.DirectCrossesIncluded -ne $false -or $symbols.Results.Count -ne 7) {
    Fail "Symbol mapping validation failed or direct crosses are included"
}
$audusd = $symbols.Results | Where-Object { $_.Symbol -eq "AUDUSD" }
if (-not $audusd -or $audusd.ValidationStatus -ne "MappingValidated") {
    Fail "AUDUSD is missing or misclassified"
}

$inversion = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-inversion-validation.json")
$usdjpy = $inversion.Results | Where-Object { $_.Symbol -eq "USDJPY" }
if (-not $usdjpy -or $usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or "$($usdjpy.SecurityID)" -ne "4004" -or "$($usdjpy.SecurityIDSource)" -ne "8") {
    Fail "USDJPY caveat was weakened"
}

$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-cost-guidance-preservation.json")
if ($cost.FiveUsdPerMillionGuidance -ne "BestCaseMajorOnly" -or $cost.Universalized -ne $false) {
    Fail "5 USD/million guidance was universalized"
}

$direct = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-direct-cross-exclusion-preservation.json")
if ($direct.DirectCrossExecutionDisabled -ne $true -or $direct.ExecutionUniverse -ne "USD-pair-only") {
    Fail "Direct-cross exclusion or USD-pair-only execution was weakened"
}

$sanitized = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-sanitized-import-readiness-metadata.json")
if ($sanitized.MetadataOnly -ne $true -or $sanitized.DbImportExecuted -ne $false -or $sanitized.PersistedSanitizedRowsCreated -ne $false -or $sanitized.RecordCount -ne 35) {
    Fail "Sanitized import-readiness metadata violates metadata-only/no-import constraints"
}

$noDb = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-no-db-import-audit.json")
Assert-FalseField $noDb "DbImportExecuted" "DB import occurred"
$noSanitized = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-no-persisted-sanitized-row-audit.json")
Assert-FalseField $noSanitized "PersistedSanitizedRowsCreated" "Persisted sanitized quote rows were created"
$noBacktest = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-no-backtest-simulation-audit.json")
Assert-FalseField $noBacktest "BacktestExecuted" "Backtest executed"
Assert-FalseField $noBacktest "SimulationExecuted" "Simulation executed"
$noTca = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-no-tca-result-lines-audit.json")
Assert-FalseField $noTca "TcaResultLinesCreated" "TCA result lines were created"
$noPolygon = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-no-polygon-api-call-audit.json")
Assert-FalseField $noPolygon "PolygonApiCalled" "Polygon API was called"
$noLmax = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-no-lmax-call-audit.json")
Assert-FalseField $noLmax "LmaxCalled" "LMAX was called"
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-no-external-api-call-audit.json")
Assert-FalseField $noExternal "ExternalApiCalled" "External API was called"
$noRuntime = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-no-broker-marketdata-runtime-audit.json")
Assert-FalseField $noRuntime "BrokerMarketDataRuntimeStarted" "Broker/market-data runtime was started"
$noOrder = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-no-order-fill-report-route-audit.json")
Assert-FalseField $noOrder "OrdersCreated" "Orders were created"
Assert-FalseField $noOrder "FillsCreated" "Fills were created"
Assert-FalseField $noOrder "ExecutionReportsCreated" "Execution reports were created"
Assert-FalseField $noOrder "RoutesCreated" "Routes were created"
Assert-FalseField $noOrder "SubmissionsCreated" "Submissions were created"

$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r042-build-test-validator-evidence.json")
if ($evidence.DotnetBuild.Status -notin @("PASS", "PASS_WITH_WARNINGS")) {
    Fail "Build evidence is missing or not passing"
}
if ($evidence.FocusedR042StaticChecks.Status -ne "PASS") {
    Fail "Focused R042 static-check evidence is missing or not passing"
}
if ($evidence.UnitTests.Status -notin @("PASS", "PASS_WITH_WARNINGS", "NOT_FEASIBLE")) {
    Fail "Unit test evidence is missing"
}

Write-Host "EXEC-SIM-R042 validator passed."
Write-Host "Classifications:"
Write-Host "EXEC_SIM_R042_PASS_ADDITIONAL_HISTORICAL_ROW_VALIDATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R042_PASS_CANONICAL_SESSION_QUOTE_WINDOW_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R042_PASS_CLOSE_BENCHMARK_FEED_QUALITY_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R042_PASS_SANITIZED_IMPORT_READINESS_METADATA_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R042_PASS_NO_IMPORT_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
