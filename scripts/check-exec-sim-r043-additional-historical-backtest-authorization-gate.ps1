param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "EXEC-SIM-R043 validation failed: $Message"
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

function Assert-TrueField($Object, [string]$Field, [string]$Message) {
    if ($null -eq $Object.$Field) {
        Fail "Missing field $Field in $Message"
    }
    if (-not [bool]$Object.$Field) {
        Fail $Message
    }
}

$requiredFiles = @(
    "phase-exec-sim-r043-summary.md",
    "phase-exec-sim-r043-additional-historical-backtest-authorization-contract.json",
    "phase-exec-sim-r043-additional-historical-backtest-authorization-request.json",
    "phase-exec-sim-r043-additional-historical-backtest-preflight-contract.json",
    "phase-exec-sim-r043-additional-historical-backtest-authorization-result.json",
    "phase-exec-sim-r043-r042-row-validation-reference.json",
    "phase-exec-sim-r043-authorized-file-entries.json",
    "phase-exec-sim-r043-authorized-quote-window-readiness.json",
    "phase-exec-sim-r043-authorized-close-benchmark-readiness.json",
    "phase-exec-sim-r043-authorized-feed-quality-readiness.json",
    "phase-exec-sim-r043-sanitized-import-readiness-authorized.json",
    "phase-exec-sim-r043-accepted-rejected-row-summary.json",
    "phase-exec-sim-r043-duplicate-handling-acknowledgement.json",
    "phase-exec-sim-r043-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r043-inversion-preservation.json",
    "phase-exec-sim-r043-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r043-legacy-compatibility-preservation.json",
    "phase-exec-sim-r043-expected-r044-policy-list.json",
    "phase-exec-sim-r043-expected-r044-report-list.json",
    "phase-exec-sim-r043-cost-guidance-preservation.json",
    "phase-exec-sim-r043-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r043-no-row-revalidation-audit.json",
    "phase-exec-sim-r043-no-db-import-audit.json",
    "phase-exec-sim-r043-no-sanitized-quote-row-creation-audit.json",
    "phase-exec-sim-r043-no-backtest-simulation-audit.json",
    "phase-exec-sim-r043-no-tca-result-lines-audit.json",
    "phase-exec-sim-r043-no-executable-schedule-audit.json",
    "phase-exec-sim-r043-no-child-slices-audit.json",
    "phase-exec-sim-r043-no-child-orders-audit.json",
    "phase-exec-sim-r043-no-polygon-api-call-audit.json",
    "phase-exec-sim-r043-no-lmax-call-audit.json",
    "phase-exec-sim-r043-no-external-api-call-audit.json",
    "phase-exec-sim-r043-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r043-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r043-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r043-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r043-no-external-audit.json",
    "phase-exec-sim-r043-forbidden-actions-audit.json",
    "phase-exec-sim-r043-next-phase-recommendation.json",
    "phase-exec-sim-r043-build-test-validator-evidence.json"
)

foreach ($file in $requiredFiles) {
    $path = Join-Path $ArtifactsRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required artifact $file"
    }
}

$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-additional-historical-backtest-authorization-contract.json")
if ($contract.AuthorizationContractId -ne "EXEC-SIM-R043-ADDITIONAL-HISTORICAL-TCA-BACKTEST-AUTHORIZATION-CONTRACT") {
    Fail "Authorization contract id mismatch"
}
if ($contract.RequiredFileEntries -ne 35 -or $contract.RequiredQuoteWindowReadinessRecords -ne 945 -or $contract.RequiredCloseBenchmarkReadinessRecords -ne 945 -or $contract.RequiredFeedQualityReadinessRecords -ne 35) {
    Fail "Authorization contract expected counts are incorrect"
}
if ($contract.ExpectedPolicies.Count -ne 11) {
    Fail "Expected R044 policy list is incomplete in contract"
}
if ($contract.ExpectedReports.Count -lt 15) {
    Fail "Expected R044 report list is incomplete in contract"
}
Assert-TrueField $contract "NoRowRevalidation" "Contract does not prohibit row revalidation"
Assert-TrueField $contract "NoBacktest" "Contract does not prohibit backtest"

$request = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-additional-historical-backtest-authorization-request.json")
if ($request.ProviderName -ne "PolygonOfflineFile" -or $request.DatasetType -ne "HistoricalBboQuotes") {
    Fail "Authorization request provider/dataset mismatch"
}
if ($request.Symbols.Count -ne 7 -or $request.ConfirmedDates.Count -ne 5) {
    Fail "Authorization request symbol/date coverage mismatch"
}
if ($request.AcceptedValidationResultIds.Count -ne 35 -or $request.QuoteWindowReadinessIds.Count -ne 945 -or $request.CloseBenchmarkReadinessIds.Count -ne 945 -or $request.FeedQualityReadinessIds.Count -ne 35) {
    Fail "Authorization request readiness id counts are incorrect"
}
Assert-TrueField $request "NoApiCall" "Authorization request does not prohibit API calls"
Assert-TrueField $request "NoBacktest" "Authorization request does not prohibit backtest"
Assert-TrueField $request "NoImport" "Authorization request does not prohibit import"
Assert-TrueField $request "NoTcaResultLines" "Authorization request does not prohibit TCA result lines"

$preflight = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-additional-historical-backtest-preflight-contract.json")
if ($preflight.AllPassed -ne $true) {
    Fail "Backtest authorization preflight did not pass"
}

$result = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-additional-historical-backtest-authorization-result.json")
if ($result.AuthorizationReady -ne $true) {
    Fail "Authorization result is not ready"
}
if ($result.RejectedRowCount -ne 0) {
    Fail "Rejected row count is not zero"
}
if ($result.OutOfOrderRowCount -ne 0) {
    Fail "Out-of-order row count is not zero"
}
if ($result.QuarantinedFilesIncluded -ne 0 -or $result.DirectCrossesIncluded -ne $false) {
    Fail "Quarantined files or direct crosses are included"
}
Assert-TrueField $result "NoRowRevalidation" "Authorization result does not prohibit row revalidation"
Assert-TrueField $result "NoBacktest" "Authorization result does not prohibit backtest"

$r042Ref = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-r042-row-validation-reference.json")
if ($r042Ref.FileEntryCount -ne 35 -or $r042Ref.QuoteWindowReadinessRecords -ne 945 -or $r042Ref.CloseBenchmarkReadinessRecords -ne 945 -or $r042Ref.FeedQualityReadinessRecords -ne 35) {
    Fail "R042 row validation reference count mismatch"
}
if ($r042Ref.TotalRejectedRows -ne 0 -or $r042Ref.RowsRevalidatedInR043 -ne $false) {
    Fail "R042 reference indicates rejected rows or R043 row revalidation"
}

$files = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-authorized-file-entries.json")
if ($files.AuthorizedFileEntryCount -ne 35 -or $files.Entries.Count -ne 35) {
    Fail "Authorized file entries count mismatch"
}
if (($files.Entries | Where-Object { $_.EligibleForR044Backtest -ne $true }).Count -ne 0) {
    Fail "At least one file entry is not eligible for R044"
}

$qw = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-authorized-quote-window-readiness.json")
if ($qw.AuthorizedQuoteWindowReadinessCount -ne 945 -or $qw.ExpectedCount -ne 945) {
    Fail "Authorized quote-window readiness count mismatch"
}
$cb = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-authorized-close-benchmark-readiness.json")
if ($cb.AuthorizedCloseBenchmarkReadinessCount -ne 945 -or $cb.ExpectedCount -ne 945) {
    Fail "Authorized close-benchmark readiness count mismatch"
}
$fq = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-authorized-feed-quality-readiness.json")
if ($fq.AuthorizedFeedQualityReadinessCount -ne 35 -or $fq.ExpectedCount -ne 35) {
    Fail "Authorized feed-quality readiness count mismatch"
}
$sanitized = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-sanitized-import-readiness-authorized.json")
if ($sanitized.AuthorizedSanitizedImportReadinessMetadataCount -ne 35 -or $sanitized.MetadataOnly -ne $true -or $sanitized.DbImportExecuted -ne $false -or $sanitized.PersistedSanitizedRowsCreated -ne $false) {
    Fail "Sanitized import readiness authorization violates metadata-only/no-import constraints"
}

$dup = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-duplicate-handling-acknowledgement.json")
if ($dup.DuplicateHandlingAcknowledged -ne $true) {
    Fail "Duplicate handling acknowledgement is missing"
}
if ($dup.OutOfOrderCountConfirmedZero -ne $true) {
    Fail "Out-of-order zero acknowledgement is missing"
}

$direct = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-direct-cross-exclusion-preservation.json")
if ($direct.DirectCrossExecutionDisabled -ne $true -or $direct.DirectCrossesIncluded -ne $false -or $direct.ExecutionUniverse -ne "USD-pair-only") {
    Fail "Direct-cross exclusion was weakened"
}

$inversion = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-inversion-preservation.json")
$usdjpy = $inversion.Results | Where-Object { $_.Symbol -eq "USDJPY" }
if (-not $usdjpy -or $usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or "$($usdjpy.SecurityID)" -ne "4004" -or "$($usdjpy.SecurityIDSource)" -ne "8") {
    Fail "USDJPY caveat was weakened"
}
$usdcad = $inversion.Results | Where-Object { $_.Symbol -eq "USDCAD" }
$usdchf = $inversion.Results | Where-Object { $_.Symbol -eq "USDCHF" }
if (-not $usdcad -or $usdcad.RequiresInversion -ne $true -or $usdcad.NormalizedPortfolioSymbol -ne "CADUSD") {
    Fail "USDCAD inversion was weakened"
}
if (-not $usdchf -or $usdchf.RequiresInversion -ne $true -or $usdchf.NormalizedPortfolioSymbol -ne "CHFUSD") {
    Fail "USDCHF inversion was weakened"
}

$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-canonical-quarter-hour-policy-preservation.json")
if ($canonical.Legacy06UsedAsFutureCanonical -ne $false -or $canonical.Preserved -ne $true) {
    Fail "Canonical quarter-hour policy was weakened"
}
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-legacy-compatibility-preservation.json")
if ($legacy.LegacyCompatibilityOnly -ne $true -or $legacy.Legacy06UsedAsFutureCanonical -ne $false) {
    Fail "Legacy :06 compatibility-only mapping was weakened"
}

$policies = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-expected-r044-policy-list.json")
if ($policies.ExpectedR044Policies.Count -ne 11 -or -not ($policies.ExpectedR044Policies -contains "CloseSeeking15mAdaptive") -or -not ($policies.ExpectedR044Policies -contains "ControlledResidualCross")) {
    Fail "Expected R044 policy list is missing required policies"
}
$reports = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-expected-r044-report-list.json")
if ($reports.ExpectedR044Reports.Count -lt 15 -or -not ($reports.ExpectedR044Reports -contains "comparison versus previous R025/R031 results if available")) {
    Fail "Expected R044 report list is missing required reports"
}

$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-cost-guidance-preservation.json")
if ($cost.FiveUsdPerMillionGuidance -ne "BestCaseMajorOnly" -or $cost.Universalized -ne $false) {
    Fail "5 USD/million guidance was universalized"
}

$noRow = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-no-row-revalidation-audit.json")
Assert-FalseField $noRow "QuoteRowsRevalidated" "Quote rows were revalidated"
$noDb = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-no-db-import-audit.json")
Assert-FalseField $noDb "DbImportExecuted" "DB import occurred"
$noSanitized = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-no-sanitized-quote-row-creation-audit.json")
Assert-FalseField $noSanitized "PersistedSanitizedQuoteRowsCreated" "Persisted sanitized quote rows were created"
$noBacktest = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-no-backtest-simulation-audit.json")
Assert-FalseField $noBacktest "BacktestExecuted" "Backtest executed"
Assert-FalseField $noBacktest "SimulationExecuted" "Simulation executed"
$noTca = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-no-tca-result-lines-audit.json")
Assert-FalseField $noTca "TcaResultLinesCreated" "TCA result lines were created"
$noSchedule = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-no-executable-schedule-audit.json")
Assert-FalseField $noSchedule "ExecutableSchedulesCreated" "Executable schedules were created"
$noChildSlices = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-no-child-slices-audit.json")
Assert-FalseField $noChildSlices "ChildSlicesCreated" "Child slices were created"
$noChildOrders = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-no-child-orders-audit.json")
Assert-FalseField $noChildOrders "ChildOrdersCreated" "Child orders were created"
$noPolygon = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-no-polygon-api-call-audit.json")
Assert-FalseField $noPolygon "PolygonApiCalled" "Polygon API was called"
$noLmax = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-no-lmax-call-audit.json")
Assert-FalseField $noLmax "LmaxCalled" "LMAX was called"
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-no-external-api-call-audit.json")
Assert-FalseField $noExternal "ExternalApiCalled" "External API was called"
$noRuntime = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-no-broker-marketdata-runtime-audit.json")
Assert-FalseField $noRuntime "BrokerMarketDataRuntimeStarted" "Broker/market-data runtime started"
$noOrder = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-no-order-fill-report-route-audit.json")
Assert-FalseField $noOrder "OrdersCreated" "Orders were created"
Assert-FalseField $noOrder "FillsCreated" "Fills were created"
Assert-FalseField $noOrder "ExecutionReportsCreated" "Execution reports were created"
Assert-FalseField $noOrder "RoutesCreated" "Routes were created"
Assert-FalseField $noOrder "SubmissionsCreated" "Submissions were created"

$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r043-build-test-validator-evidence.json")
if ($evidence.DotnetBuild.Status -notin @("PASS", "PASS_WITH_WARNINGS")) {
    Fail "Build evidence is missing or not passing"
}
if ($evidence.FocusedR043StaticChecks.Status -ne "PASS") {
    Fail "Focused R043 static checks are missing or not passing"
}
if ($evidence.UnitTests.Status -notin @("PASS", "PASS_WITH_WARNINGS", "NOT_FEASIBLE")) {
    Fail "Unit test evidence is missing"
}

Write-Host "EXEC-SIM-R043 validator passed."
Write-Host "Classifications:"
Write-Host "EXEC_SIM_R043_PASS_ADDITIONAL_HISTORICAL_BACKTEST_AUTHORIZATION_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R043_PASS_ROW_VALIDATED_CANONICAL_SESSION_AUTHORIZED_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R043_PASS_NO_REVALIDATION_NO_BACKTEST_GATE_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R043_PASS_ADDITIONAL_HISTORICAL_BACKTEST_AUTHORIZATION_WITH_DUPLICATE_WARNINGS_NO_EXTERNAL"
