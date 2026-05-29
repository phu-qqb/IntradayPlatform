$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"

function Fail {
    param([string]$Message)
    throw "EXEC-SANDBOX-R007 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-sandbox-r007-summary.md",
    "phase-exec-sandbox-r007-r006-reference.json",
    "phase-exec-sandbox-r007-quantity-rule-inventory.json",
    "phase-exec-sandbox-r007-per-symbol-quantity-calibration-results.json",
    "phase-exec-sandbox-r007-sandbox-guardrail-validation.json",
    "phase-exec-sandbox-r007-fix-logon-confirmation.json",
    "phase-exec-sandbox-r007-sandbox-order-intents.json",
    "phase-exec-sandbox-r007-sandbox-routes.json",
    "phase-exec-sandbox-r007-sandbox-submission-results.json",
    "phase-exec-sandbox-r007-sandbox-ack-reject-results.json",
    "phase-exec-sandbox-r007-sandbox-execution-reports.json",
    "phase-exec-sandbox-r007-sandbox-fill-reports.json",
    "phase-exec-sandbox-r007-sandbox-reconciliation-result.json",
    "phase-exec-sandbox-r007-quantity-calibration-decision.json",
    "phase-exec-sandbox-r007-no-secret-persistence-audit.json",
    "phase-exec-sandbox-r007-no-production-broker-audit.json",
    "phase-exec-sandbox-r007-no-production-order-audit.json",
    "phase-exec-sandbox-r007-no-production-route-audit.json",
    "phase-exec-sandbox-r007-no-production-fill-report-audit.json",
    "phase-exec-sandbox-r007-no-production-ledger-audit.json",
    "phase-exec-sandbox-r007-no-production-state-mutation-audit.json",
    "phase-exec-sandbox-r007-direct-cross-exclusion-preservation.json",
    "phase-exec-sandbox-r007-usd-pair-whitelist-preservation.json",
    "phase-exec-sandbox-r007-usdjpy-caveat-preservation.json",
    "phase-exec-sandbox-r007-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sandbox-r007-legacy-compatibility-preservation.json",
    "phase-exec-sandbox-r007-cost-guidance-preservation.json",
    "phase-exec-sandbox-r007-nonmajor-calibration-preservation.json",
    "phase-exec-sandbox-r007-forbidden-actions-audit.json",
    "phase-exec-sandbox-r007-next-phase-recommendation.json",
    "phase-exec-sandbox-r007-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$sourcePath = Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009LmaxSandboxOrderSmoke.cs"
$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009SandboxUsdPairQuantityCalibrationTests.cs"
if (-not (Test-Path -LiteralPath $sourcePath)) { Fail "Sandbox source missing" }
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Focused R007 tests missing" }
$source = Get-Content -LiteralPath $sourcePath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($token in @(
    "R009SandboxUsdPairQuantityRuleInventory",
    "R009SandboxPerSymbolQuantityCalibrationResult",
    "R009SandboxQuantityCalibrationPlanValidation",
    "BuildUsdPairQuantityRuleInventory",
    "ValidateUsdPairQuantityCalibrationPlan"
)) {
    if ($source -notmatch [regex]::Escape($token)) { Fail "Source missing R007 token $token" }
}
foreach ($token in @(
    "Quantity_inventory_preserves_all_supported_usd_pairs",
    "Calibration_plan_enforces_one_order_per_symbol_and_total_cap",
    "Calibration_plan_rejects_legacy_noncanonical_target_close",
    "Calibration_plan_preserves_usdjpy_security_id_caveat",
    "Calibration_plan_rejects_direct_cross_submission"
)) {
    if ($tests -notmatch [regex]::Escape($token)) { Fail "Focused R007 tests missing scenario $token" }
}

$allowedSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
$inventory = Read-Json "phase-exec-sandbox-r007-quantity-rule-inventory.json"
if (@($inventory.SupportedSymbols).Count -ne 7) { Fail "Supported symbol inventory must contain 7 symbols" }
foreach ($symbol in $allowedSymbols) {
    if (@($inventory.SupportedSymbols) -notcontains $symbol) { Fail "Supported symbol missing: $symbol" }
    if (@($inventory.Results | Where-Object { $_.Symbol -eq $symbol }).Count -ne 1) { Fail "Per-symbol inventory missing or duplicated: $symbol" }
}
if ($inventory.QuantityRulesInvented -ne $false) { Fail "Quantity rules were invented" }
if ($inventory.KnownEurusdRulePreserved -ne $true) { Fail "EURUSD known rule not preserved" }

$results = Read-Json "phase-exec-sandbox-r007-per-symbol-quantity-calibration-results.json"
if (@($results.Results).Count -ne 7) { Fail "Per-symbol calibration result must contain 7 rows" }
foreach ($row in $results.Results) {
    if (@($allowedSymbols) -notcontains $row.Symbol) { Fail "Unsupported symbol included: $($row.Symbol)" }
    if ($row.SandboxOnly -ne $true) { Fail "Symbol result not sandbox-only: $($row.Symbol)" }
    if ($row.ProductionOrderCreated -ne $false -or $row.ProductionRouteCreated -ne $false -or $row.ProductionLedgerMutation -ne $false) {
        Fail "Production artifact/mutation marked for $($row.Symbol)"
    }
    if ([decimal]$row.CandidateQuantity -gt 0.1) { Fail "Candidate quantity exceeds per-symbol cap for $($row.Symbol)" }
}

$guardrail = Read-Json "phase-exec-sandbox-r007-sandbox-guardrail-validation.json"
if ($guardrail.SandboxCredentialPresent -ne $true -or $guardrail.CredentialValuesRedacted -ne $true) { Fail "Sandbox credential presence/redaction failed" }
if ($guardrail.ProductionCredentialDetected -ne $false) { Fail "Production credential detected" }
if ($guardrail.SandboxKillSwitchOpen -ne $true -or $guardrail.ProductionRouteBlocked -ne $true) { Fail "Sandbox kill switch or production route guardrail failed" }
if ($guardrail.MaxSandboxOrderCount -ne 7 -or [decimal]$guardrail.MaxOrderQuantityPerSymbol -ne 0.1 -or [decimal]$guardrail.MaxTotalSandboxQuantity -ne 0.7) { Fail "R007 sandbox caps incorrect" }
if ($guardrail.SubmittedOrderCount -gt 7) { Fail "More than MaxSandboxOrderCount submitted" }
if ([decimal]$guardrail.TotalSubmittedQuantity -gt 0.7) { Fail "Total sandbox quantity exceeds cap" }
if ($guardrail.OneOrderPerSymbol -ne $true) { Fail "More than one order per symbol submitted" }
foreach ($property in @("DirectCrossExecutionAllowed", "NonmajorExecutionAllowed", "ProductionVenueAllowed", "ProductionCredentialsAllowed", "PaperLedgerCommitAllowed", "ProductionLedgerCommitAllowed", "StateMutationAllowed", "SchedulerAllowed", "Legacy06AcceptedAsFutureCanonical")) {
    if ($guardrail.$property -ne $false) { Fail "Forbidden guardrail enabled: $property" }
}

$logon = Read-Json "phase-exec-sandbox-r007-fix-logon-confirmation.json"
if ($guardrail.SubmittedOrderCount -gt 0) {
    if ($logon.LogonConfirmed -ne $true) { Fail "Orders submitted without confirmed sandbox logon" }
    if ($logon.NewOrderSingleSentBeforeLogonConfirmed -ne $false) { Fail "NewOrderSingle sent before confirmed logon" }
}

$submissions = Read-Json "phase-exec-sandbox-r007-sandbox-submission-results.json"
if ($submissions.SubmittedOrderCount -ne $guardrail.SubmittedOrderCount) { Fail "Submission count mismatch" }
if ([decimal]$submissions.TotalSubmittedQuantity -ne [decimal]$guardrail.TotalSubmittedQuantity) { Fail "Submitted quantity mismatch" }
if ($submissions.SubmittedOrderCount -gt 7) { Fail "Submission artifact exceeds order cap" }
if ([decimal]$submissions.TotalSubmittedQuantity -gt 0.7) { Fail "Submission artifact exceeds quantity cap" }
if ($submissions.SandboxOnly -ne $true -or $submissions.ProductionSubmission -ne $false) { Fail "Submission artifact unsafe" }

$routes = Read-Json "phase-exec-sandbox-r007-sandbox-routes.json"
if ($routes.ProductionRouteCreated -ne $false -or $routes.SandboxOnly -ne $true) { Fail "Route artifact unsafe" }
$reports = Read-Json "phase-exec-sandbox-r007-sandbox-execution-reports.json"
if ($reports.ProductionExecutionReport -ne $false -or $reports.SandboxOnly -ne $true) { Fail "Execution report artifact unsafe" }
$fills = Read-Json "phase-exec-sandbox-r007-sandbox-fill-reports.json"
if ($fills.ProductionFill -ne $false -or $fills.SandboxOnly -ne $true) { Fail "Fill artifact unsafe" }

$reconciliation = Read-Json "phase-exec-sandbox-r007-sandbox-reconciliation-result.json"
if ($reconciliation.SubmittedOrderCount -ne $guardrail.SubmittedOrderCount) { Fail "Reconciliation submitted count mismatch" }
if ($reconciliation.ProductionOrderCreated -ne $false -or $reconciliation.ProductionRouteCreated -ne $false -or $reconciliation.ProductionFillOrReportCreated -ne $false -or $reconciliation.ProductionLedgerMutation -ne $false -or $reconciliation.ProductionStateMutation -ne $false -or $reconciliation.SandboxOnly -ne $true) {
    Fail "Reconciliation artifact unsafe"
}

$decision = Read-Json "phase-exec-sandbox-r007-quantity-calibration-decision.json"
if ($decision.NotProductionApproval -ne $true) { Fail "Decision implies production approval" }
if ($results.AllValidated -eq $true -and $decision.Decision -ne "SandboxQuantityRulesValidatedForAllSupportedPairs") { Fail "All-validated decision mismatch" }
if ($results.Partial -eq $true -and $decision.Decision -ne "SandboxQuantityRulesPartiallyValidated") { Fail "Partial decision mismatch" }

$direct = Read-Json "phase-exec-sandbox-r007-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false -or $direct.DirectCrossExecutionIntentRejected -ne $true -or $direct.EURGBPSubmitted -ne $false) { Fail "Direct-cross exclusion weakened" }
$whitelist = Read-Json "phase-exec-sandbox-r007-usd-pair-whitelist-preservation.json"
if ($whitelist.NonWhitelistedSymbolAllowed -ne $false -or $whitelist.AudusdStatus -ne "SupportedAndNotFailed" -or $whitelist.AudusdMisclassified -ne $false) { Fail "Whitelist/AUDUSD preservation failed" }
$usdjpy = Read-Json "phase-exec-sandbox-r007-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8" -or $usdjpy.CaveatPreserved -ne $true) { Fail "USDJPY caveat weakened" }
$legacy = Read-Json "phase-exec-sandbox-r007-legacy-compatibility-preservation.json"
if ($legacy.Legacy06AcceptedAsFutureCanonical -ne $false -or $legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 accepted as future canonical" }
$cost = Read-Json "phase-exec-sandbox-r007-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$nonmajor = Read-Json "phase-exec-sandbox-r007-nonmajor-calibration-preservation.json"
if ($nonmajor.SandboxExecutionAllowed -ne $false) { Fail "Nonmajor/EM/scandi/CNH execution allowed" }

$forbidden = Read-Json "phase-exec-sandbox-r007-forbidden-actions-audit.json"
foreach ($property in @(
    "LmaxProductionUsed",
    "ProductionCredentialsUsed",
    "NonSandboxBrokerRouteUsed",
    "CredentialValuesPrintedOrPersisted",
    "PolygonCalled",
    "UnrelatedExternalApiCalled",
    "SchedulerServicePollingBackgroundJobIntroduced",
    "NewOrderSingleSentBeforeLogonConfirmed",
    "MoreThanMaxSandboxOrderCountSubmitted",
    "MoreThanOneOrderPerSymbolSubmitted",
    "TotalSandboxQuantityExceedsCap",
    "QuantityViolatesDiscoveredRule",
    "UnsupportedSymbolSubmitted",
    "DirectCrossExecutionAllowed",
    "NonWhitelistedSymbolAllowed",
    "Legacy06AcceptedAsFutureCanonical",
    "UsdjpyCaveatWeakened",
    "AudusdMisclassified",
    "ProductionOrderArtifactCreated",
    "ProductionRouteArtifactCreated",
    "ProductionFillReportArtifactCreated",
    "ProductionLedgerCommitOccurred",
    "ProductionStateMutationOccurred"
)) {
    if ($forbidden.$property -ne $false) { Fail "Forbidden action observed or allowed: $property" }
}
if ($forbidden.SandboxArtifactsClearlyMarkedSandboxOnlyOrExistingLmaxDemoProfile -ne $true) { Fail "Sandbox artifacts not clearly marked" }

$secretAudit = Read-Json "phase-exec-sandbox-r007-no-secret-persistence-audit.json"
if ($secretAudit.SecretValuesSerialized -ne $false -or $secretAudit.CredentialVariableNamesOnly -ne $true -or $secretAudit.CredentialValuesRedacted -ne $true) { Fail "Secret persistence audit failed" }

$combined = (Get-ChildItem -LiteralPath $artifactDir -File -Filter "phase-exec-sandbox-r007-*.json" | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
foreach ($banned in @("QQ_LMAX_FIX_PASSWORD", "FixPassword`":`"", "FixUsername`":`"", "Password`":`"", "Username`":`"")) {
    if ($combined -match [regex]::Escape($banned)) { Fail "Possible credential value field persisted: $banned" }
}

$evidence = Read-Json "phase-exec-sandbox-r007-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedSandboxTests -ne "Passed") { Fail "Focused sandbox test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-SANDBOX-R007 validator passed."
