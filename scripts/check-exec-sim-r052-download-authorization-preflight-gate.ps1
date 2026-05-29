$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactsRoot = Join-Path $RepoRoot "artifacts\readiness\execution-sim"

function Fail([string]$Message) {
    Write-Error "EXEC-SIM-R052 validation failed: $Message"
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

function Assert-True($Value, [string]$Message) {
    if ($Value -ne $true) { Fail $Message }
}

function Assert-False($Value, [string]$Message) {
    if ($Value -ne $false) { Fail $Message }
}

function Assert-FalseIfPresent($Object, [string]$Name, [string]$Message) {
    if ((Has-Prop $Object $Name) -and $Object.$Name -ne $false) {
        Fail $Message
    }
}

function Assert-ContainsValue($Values, [string]$Expected, [string]$Message) {
    if ($Values -notcontains $Expected) {
        Fail $Message
    }
}

function Get-HashObject($Object) {
    $json = $Object | ConvertTo-Json -Depth 30 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    $sha = [Security.Cryptography.SHA256]::Create()
    return (($sha.ComputeHash($bytes) | ForEach-Object ToString x2) -join "")
}

$requiredR052Artifacts = @(
    "phase-exec-sim-r052-summary.md",
    "phase-exec-sim-r052-r051-reference.json",
    "phase-exec-sim-r052-download-authorization-preflight.json",
    "phase-exec-sim-r052-command-bundle-freeze.json",
    "phase-exec-sim-r052-expected-output-layout.json",
    "phase-exec-sim-r052-post-download-validation-plan.json",
    "phase-exec-sim-r052-stop-go-criteria.json",
    "phase-exec-sim-r052-no-external-audit.json",
    "phase-exec-sim-r052-build-test-validator-evidence.json"
)

$requiredR051Artifacts = @(
    "phase-exec-sim-r051-summary.md",
    "phase-exec-sim-r051-date-selection-plan.json",
    "phase-exec-sim-r051-download-command-plan.json",
    "phase-exec-sim-r051-validation-gate-plan.json",
    "phase-exec-sim-r051-no-external-audit.json",
    "phase-exec-sim-r051-build-test-validator-evidence.json"
)

foreach ($artifact in ($requiredR052Artifacts + $requiredR051Artifacts)) {
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsRoot $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactsRoot "phase-exec-sim-r052-summary.md")
foreach ($classification in @(
    "EXEC_SIM_R052_PASS_DOWNLOAD_AUTHORIZATION_PREFLIGHT_READY_NO_EXTERNAL",
    "EXEC_SIM_R052_PASS_COMMAND_BUNDLE_FROZEN_NO_EXTERNAL",
    "EXEC_SIM_R052_PASS_EXPECTED_OUTPUT_LAYOUT_READY_NO_EXTERNAL",
    "EXEC_SIM_R052_PASS_POST_DOWNLOAD_VALIDATION_PLAN_READY_NO_EXTERNAL",
    "EXEC_SIM_R052_PASS_NO_DOWNLOAD_NO_IMPORT_NO_SIMULATION_GATE_READY_NO_EXTERNAL"
)) {
    if ($summary -notmatch [regex]::Escape($classification)) {
        Fail "R052 summary missing classification: $classification"
    }
}

$r051Summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactsRoot "phase-exec-sim-r051-summary.md")
foreach ($classification in @(
    "EXEC_SIM_R051_PASS_ADDITIONAL_DATE_SELECTION_READY_NO_EXTERNAL",
    "EXEC_SIM_R051_PASS_DOWNLOAD_COMMAND_PLAN_READY_NO_EXTERNAL",
    "EXEC_SIM_R051_PASS_VALIDATION_GATE_PLAN_READY_NO_EXTERNAL",
    "EXEC_SIM_R051_PASS_NO_DOWNLOAD_NO_SIMULATION_GATE_READY_NO_EXTERNAL"
)) {
    if ($r051Summary -notmatch [regex]::Escape($classification)) {
        Fail "R051 summary missing accepted classification: $classification"
    }
}

$datePlan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-date-selection-plan.json")
$downloadPlan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-download-command-plan.json")
$validationPlan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-validation-gate-plan.json")
$r051Audit = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r051-no-external-audit.json")

if ($datePlan.SelectedAdditionalTradingDateCount -lt 20) { Fail "R051 selected fewer than 20 additional dates" }
if ($datePlan.SelectedAdditionalTradingDateCount -ne 25) { Fail "R051 selected date count is not 25 and no safe replacement is documented" }
if ($datePlan.SelectedDates.Count -ne 25) { Fail "R051 selected date list count is not 25" }

$expectedSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
foreach ($symbol in $expectedSymbols) {
    Assert-ContainsValue $datePlan.CoreUsdPairUniverse $symbol "R051 missing core symbol: $symbol"
    Assert-ContainsValue $downloadPlan.Symbols $symbol "R051 download plan missing symbol: $symbol"
}
if ($datePlan.CoreUsdPairUniverse.Count -ne 7 -or $downloadPlan.Symbols.Count -ne 7) { Fail "Seven-symbol universe changed" }
if ($datePlan.CanonicalSession -ne "14:15-21:00 America/New_York") { Fail "R051 canonical session changed" }
if ($downloadPlan.CanonicalSession -ne "14:15-21:00 America/New_York") { Fail "R051 download canonical session changed" }
Assert-True $datePlan.CanonicalQuarterHourClosePolicyPreserved "R051 canonical close policy not preserved"
Assert-True $downloadPlan.CanonicalQuarterHourClosePolicyPreserved "R051 download close policy not preserved"
Assert-True $datePlan.NonMajorEmScandiCnhDeferred "R051 did not defer nonmajor / EM / scandi / CNH"
Assert-True $downloadPlan.NonMajorEmScandiCnhDeferred "R051 download plan did not defer nonmajor / EM / scandi / CNH"
Assert-True $datePlan.DirectCrossesExecutionDisabled "R051 did not disable direct-cross execution"
Assert-True $downloadPlan.DirectCrossesExecutionDisabled "R051 download plan did not disable direct-cross execution"

Assert-True $downloadPlan.AllCommandsMarkedNotExecuted "R051 commands not all marked NotExecuted"
foreach ($command in $downloadPlan.FutureCommands) {
    if ($command.ExecutionStatus -ne "NotExecuted") { Fail "R051 future command is marked executed" }
    if ($command.RequiresFutureOperatorApproval -ne $true) { Fail "R051 future command lacks future operator approval requirement" }
}

foreach ($doc in @($datePlan, $downloadPlan, $validationPlan, $r051Audit)) {
    foreach ($flag in @(
        "DownloadsExecutedInR051",
        "DownloadCommandsExecutedInR051",
        "ExternalApiCalled",
        "PolygonApiCalled",
        "MassiveApiCalled",
        "LmaxCalled",
        "FilesDownloaded",
        "DownloadCommandsExecuted",
        "QuotesImported",
        "QuoteValidationExecutedInR051",
        "QuoteValidationExecutedOnNewData",
        "ValidationExecutedInR051",
        "SimulationExecutedInR051",
        "SimulationExecuted",
        "BacktestExecutedInR051",
        "BacktestExecuted",
        "TcaResultLinesProducedInR051",
        "TcaResultLinesProduced",
        "ExecutableSchedulesCreated",
        "RoutesCreated",
        "OrdersCreated",
        "FillsCreated",
        "SubmissionsCreated",
        "TradingStateMutated",
        "R009PromotedToExecutable",
        "ExecutablePromotionAuthorized",
        "QQProductionCoreQubesTouched"
    )) {
        Assert-FalseIfPresent $doc $flag "R051 forbidden action detected: $flag"
    }
}

$r051Ref = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r052-r051-reference.json")
$preflight = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r052-download-authorization-preflight.json")
$bundle = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r052-command-bundle-freeze.json")
$layout = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r052-expected-output-layout.json")
$postPlan = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r052-post-download-validation-plan.json")
$stopGo = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r052-stop-go-criteria.json")
$audit = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r052-no-external-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-sim-r052-build-test-validator-evidence.json")

Assert-True $r051Ref.SelectedDatesAreFutureDownloadCandidatesOnly "R052 does not record R051 dates as future candidates only"
if ($r051Ref.SelectedDates.Count -ne 25) { Fail "R052 R051 reference selected date count is not 25" }
foreach ($selected in $r051Ref.SelectedDates) {
    Assert-True $selected.FutureDownloadCandidateOnly "R052 selected date is not future-candidate-only: $($selected.Date)"
}

Assert-True $r051Ref.R009ContractPreservedAsNonExecutable "R009 non-executable status not preserved"
Assert-True $r051Ref.R049CompactGridResultsReferenceOnly "R049 reference-only status not preserved"
Assert-True $r051Ref.UniverseUnchanged "R052 changed the seven-symbol universe"
Assert-True $r051Ref.CanonicalQuarterHourClosePolicyPreserved "R052 changed canonical close policy"
Assert-True $r051Ref.NonMajorEmScandiCnhDeferred "R052 changed nonmajor / EM / scandi / CNH deferral"
Assert-True $r051Ref.DirectCrossesSignalOnlyNettingFirstExecutionDisabled "R052 enabled direct crosses"

foreach ($flag in @(
    "--execute",
    "--allow-external-download",
    "--allow-polygon-or-massive-download",
    "--expected-r052-command-bundle-hash ee44012777d8e40ac37ae28ea312d49d0d5d678fd3502189378b289849280846",
    "--expected-date-set-hash 5250aec967cc968a67246e33a6a189290871e6fd49b609144d1ad505a323a092",
    "--expected-symbol-universe-hash 91ff0a953e138f7fcf5a75666ed17711d34d0872f80c67eae67c6fde8af2ec66",
    "--no-import",
    "--no-simulation",
    "--no-backtest",
    "--no-tca",
    "--no-executable-promotion"
)) {
    Assert-ContainsValue $preflight.RequiredFutureAuthorizationFlags $flag "Missing future authorization flag: $flag"
}

foreach ($command in $bundle.FrozenCommands) {
    if ($command.PlanStatus -ne "PlannedOnly") { Fail "Future command is not PlannedOnly" }
    if ($command.ExecutionStatus -ne "NotExecuted") { Fail "Future command is marked executed" }
    if ($command.RequiresFutureOperatorApproval -ne $true) { Fail "Future command does not require future operator approval" }
    if ($command.MayBeExecutedInR052 -ne $false) { Fail "Future command may be executed in R052" }
}
Assert-True $bundle.EveryFutureCommandMarkedPlannedOnly "Not every future command is PlannedOnly"
Assert-True $bundle.EveryFutureCommandMarkedNotExecuted "Not every future command is NotExecuted"
Assert-True $bundle.EveryFutureCommandRequiresFutureOperatorApproval "Not every future command requires future operator approval"

if ($layout.DownloadedQuoteFilesCreatedInR052 -ne $false) { Fail "Expected output layout created downloaded quote files" }
foreach ($field in @("date", "symbol", "source", "timezoneConvention", "closePolicy", "rawQuoteFileHashAfterFutureDownload", "validationStatusAfterFutureValidation")) {
    Assert-ContainsValue $layout.RequiredMetadataFields $field "Expected output metadata missing field: $field"
}

foreach ($gate in @(
    "all 25 dates present",
    "all seven symbols present",
    "canonical 14:15-21:00 America/New_York quarter-hour close grid complete",
    "timestamps parseable",
    "timezone conversion correct",
    "no malformed timestamps",
    "no duplicate bars unless explicitly allowed and resolved",
    "missing quote stop criteria",
    "symbol mismatch stop criteria",
    "unsupported spread/cost stop criteria",
    "no direct-cross execution enablement"
)) {
    Assert-ContainsValue $postPlan.Gates $gate "Missing post-download validation gate: $gate"
}

foreach ($criterion in @(
    "fewer than 20 additional dates available after future download",
    "any selected date missing all quotes",
    "any required symbol missing",
    "wrong timezone",
    "wrong close-policy grid",
    "malformed timestamp",
    "unparseable quote file",
    "spread/cost assumption unsupported",
    "any accidental import/simulation/backtest/TCA line/executable schedule/order/fill/route/submission"
)) {
    Assert-ContainsValue $postPlan.StopCriteria $criterion "Missing post-download stop criterion: $criterion"
    Assert-ContainsValue $stopGo.StopCriteria $criterion "Missing stop/go stop criterion: $criterion"
}

foreach ($criterion in @(
    "future download command executed only with required flags",
    "at least 20 additional dates successfully downloaded",
    "all seven symbols covered",
    "close grid complete",
    "outputs remain non-executable quote artifacts only",
    "no simulations/backtests/TCA/execution promotion"
)) {
    Assert-ContainsValue $stopGo.SuccessCriteriaForLaterDownloadPhase $criterion "Missing later download success criterion: $criterion"
}

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
    Assert-False $audit.$flag "R052 no-external audit forbidden action detected: $flag"
}
Assert-True $audit.NoExternal "R052 no-external audit missing"

$dates = @($datePlan.SelectedDates | ForEach-Object { $_.Date } | Sort-Object)
$symbols = @($datePlan.CoreUsdPairUniverse)
$selectedDateSet = [ordered]@{
    phase = "EXEC-SIM-R052"
    source = "EXEC-SIM-R051"
    futureDownloadCandidateOnly = $true
    minimumAdditionalTradingDatesRequired = 20
    selectedDateCount = $dates.Count
    dates = $dates
}
$symbolUniverse = [ordered]@{
    phase = "EXEC-SIM-R052"
    source = "EXEC-SIM-R051"
    universe = "core-usd-pairs"
    symbolCount = $symbols.Count
    symbols = $symbols
    nonMajorEmScandiCnhDeferred = $true
    directCrossesExecutionDisabled = $true
}
$closePolicy = [ordered]@{
    phase = "EXEC-SIM-R052"
    policyName = "canonical-quarter-hour-close-policy"
    session = "14:15-21:00 America/New_York"
    localCloseStart = "14:15"
    localCloseEnd = "21:00"
    timezone = "America/New_York"
    interval = "PT15M"
    legacy06LabelsFutureCanonical = $false
}
$futureFlags = @(
    "--execute",
    "--allow-external-download",
    "--allow-polygon-or-massive-download",
    "--expected-r052-command-bundle-hash <hash>",
    "--expected-date-set-hash <hash>",
    "--expected-symbol-universe-hash <hash>",
    "--no-import",
    "--no-simulation",
    "--no-backtest",
    "--no-tca",
    "--no-executable-promotion"
)
$frozenCommands = @($downloadPlan.FutureCommands | ForEach-Object {
    [ordered]@{
        commandId = $_.CommandId
        phase = "EXEC-SIM-R052"
        planStatus = "PlannedOnly"
        executionStatus = "NotExecuted"
        requiresFutureOperatorApproval = $true
        mayBeExecutedInR052 = $false
        sourceR051ExecutionStatus = $_.ExecutionStatus
        requiredFutureAuthorizationFlags = $futureFlags
        commandTemplate = $_.CommandTemplate
    }
})
$bundleHashInput = [ordered]@{
    phase = "EXEC-SIM-R052"
    source = "EXEC-SIM-R051"
    commandPlanOnly = $true
    commandCount = $frozenCommands.Count
    selectedDateCount = $dates.Count
    symbols = $symbols
    dates = $dates
    canonicalSession = "14:15-21:00 America/New_York"
    commands = $frozenCommands
}
$postPlanHashInput = [ordered]@{
    phase = "EXEC-SIM-R052"
    source = "EXEC-SIM-R051"
    planOnly = $true
    validationExecutedInR052 = $false
    gates = $postPlan.Gates
    stopCriteria = $postPlan.StopCriteria
    selectedDateCount = $postPlan.SelectedDateCount
    symbolCount = $postPlan.SymbolCount
    canonicalSession = $postPlan.CanonicalSession
    directCrossesExecutionDisabled = $postPlan.DirectCrossesExecutionDisabled
}

$selectedDateSetHash = Get-HashObject $selectedDateSet
$symbolUniverseHash = Get-HashObject $symbolUniverse
$closePolicyHash = Get-HashObject $closePolicy
$commandBundleHash = Get-HashObject $bundleHashInput
$postPlanHash = Get-HashObject $postPlanHashInput

if ($r051Ref.Hashes.SelectedDateSetHash -ne $selectedDateSetHash -or $preflight.SelectedDateSetHash -ne $selectedDateSetHash -or $bundle.SelectedDateSetHash -ne $selectedDateSetHash) {
    Fail "Selected-date set hash mismatch"
}
if ($r051Ref.Hashes.SymbolUniverseHash -ne $symbolUniverseHash -or $preflight.SymbolUniverseHash -ne $symbolUniverseHash -or $bundle.SymbolUniverseHash -ne $symbolUniverseHash) {
    Fail "Symbol universe hash mismatch"
}
if ($r051Ref.Hashes.ClosePolicyContractHash -ne $closePolicyHash -or $preflight.ClosePolicyContractHash -ne $closePolicyHash -or $bundle.ClosePolicyContractHash -ne $closePolicyHash) {
    Fail "Close-policy contract hash mismatch"
}
if ($preflight.DownloadCommandBundleHash -ne $commandBundleHash -or $bundle.CommandBundleHash -ne $commandBundleHash) {
    Fail "Download command bundle hash mismatch"
}
if ($preflight.PostDownloadValidationPlanHash -ne $postPlanHash -or $postPlan.PostDownloadValidationPlanHash -ne $postPlanHash) {
    Fail "Post-download validation plan hash mismatch"
}

if ($evidence.DotnetBuild.Status -notin @("PASS", "PASS_WITH_WARNINGS", "PENDING")) { Fail "Build evidence missing or failing" }
if ($evidence.DotnetTest.Status -notin @("PASS", "PASS_WITH_WARNINGS", "NOT_FEASIBLE", "PENDING")) { Fail "Test evidence missing or failing" }
if ($evidence.Validator.Status -notin @("PASS", "PENDING")) { Fail "Validator evidence missing or failing" }

Write-Host "EXEC-SIM-R052 validator passed."
Write-Host "Classifications:"
Write-Host "EXEC_SIM_R052_PASS_DOWNLOAD_AUTHORIZATION_PREFLIGHT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R052_PASS_COMMAND_BUNDLE_FROZEN_NO_EXTERNAL"
Write-Host "EXEC_SIM_R052_PASS_EXPECTED_OUTPUT_LAYOUT_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R052_PASS_POST_DOWNLOAD_VALIDATION_PLAN_READY_NO_EXTERNAL"
Write-Host "EXEC_SIM_R052_PASS_NO_DOWNLOAD_NO_IMPORT_NO_SIMULATION_GATE_READY_NO_EXTERNAL"
