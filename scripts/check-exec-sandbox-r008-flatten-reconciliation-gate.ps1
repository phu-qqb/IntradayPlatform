$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"

function Fail {
    param([string]$Message)
    throw "EXEC-SANDBOX-R008 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$required = @(
    "phase-exec-sandbox-r008-summary.md",
    "phase-exec-sandbox-r008-r007-reference.json",
    "phase-exec-sandbox-r008-r007-fill-review.json",
    "phase-exec-sandbox-r008-pre-flatten-position-reconciliation.json",
    "phase-exec-sandbox-r008-position-source-classification.json",
    "phase-exec-sandbox-r008-flatten-order-plan.json",
    "phase-exec-sandbox-r008-sandbox-guardrail-validation.json",
    "phase-exec-sandbox-r008-fix-logon-confirmation.json",
    "phase-exec-sandbox-r008-sandbox-flatten-order-intents.json",
    "phase-exec-sandbox-r008-sandbox-flatten-routes.json",
    "phase-exec-sandbox-r008-sandbox-flatten-submission-results.json",
    "phase-exec-sandbox-r008-sandbox-flatten-ack-reject-results.json",
    "phase-exec-sandbox-r008-sandbox-flatten-execution-reports.json",
    "phase-exec-sandbox-r008-sandbox-flatten-fill-reports.json",
    "phase-exec-sandbox-r008-post-flatten-reconciliation.json",
    "phase-exec-sandbox-r008-flat-state-audit.json",
    "phase-exec-sandbox-r008-residual-diagnostics.json",
    "phase-exec-sandbox-r008-flatten-decision.json",
    "phase-exec-sandbox-r008-sandbox-audit-record.json",
    "phase-exec-sandbox-r008-no-secret-persistence-audit.json",
    "phase-exec-sandbox-r008-no-production-broker-audit.json",
    "phase-exec-sandbox-r008-no-production-order-audit.json",
    "phase-exec-sandbox-r008-no-production-route-audit.json",
    "phase-exec-sandbox-r008-no-production-fill-report-audit.json",
    "phase-exec-sandbox-r008-no-production-ledger-audit.json",
    "phase-exec-sandbox-r008-no-production-state-mutation-audit.json",
    "phase-exec-sandbox-r008-direct-cross-exclusion-preservation.json",
    "phase-exec-sandbox-r008-usd-pair-whitelist-preservation.json",
    "phase-exec-sandbox-r008-usdjpy-caveat-preservation.json",
    "phase-exec-sandbox-r008-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sandbox-r008-legacy-compatibility-preservation.json",
    "phase-exec-sandbox-r008-cost-guidance-preservation.json",
    "phase-exec-sandbox-r008-nonmajor-calibration-preservation.json",
    "phase-exec-sandbox-r008-forbidden-actions-audit.json",
    "phase-exec-sandbox-r008-next-phase-recommendation.json",
    "phase-exec-sandbox-r008-build-test-validator-evidence.json"
)
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) { Fail "Required artifact missing: $name" }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009LmaxSandboxOrderSmoke.cs") -Raw
$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009SandboxFlattenReconciliationTests.cs") -Raw
foreach ($token in @(
    "R009SandboxR007FillReview",
    "R009SandboxPositionReconciliation",
    "R009SandboxFlattenOrderPlan",
    "R009SandboxFlattenGuardrailValidation",
    "R009SandboxPostFlattenReconciliation",
    "ReviewR007SandboxFills",
    "PlanSandboxFlattenOrders",
    "ReconcilePostFlatten"
)) {
    if ($source -notmatch [regex]::Escape($token)) { Fail "Source missing R008 token $token" }
}
foreach ($token in @(
    "R007_fill_review_requires_seven_sandbox_whitelisted_fills",
    "Fill_report_derived_reconciliation_builds_open_positions",
    "Flatten_plan_uses_opposite_side_for_r007_buy_positions",
    "Flatten_guardrails_reject_quantity_above_original_fill",
    "Post_flatten_reconciliation_marks_flat_when_all_positions_filled_opposite"
)) {
    if ($tests -notmatch [regex]::Escape($token)) { Fail "Focused R008 test missing $token" }
}

$fillReview = Read-Json "phase-exec-sandbox-r008-r007-fill-review.json"
if ($fillReview.FillCount -ne 7 -or $fillReview.SevenWhitelistedSymbolsFilled -ne $true -or $fillReview.QuantityPointOnePerSymbol -ne $true -or $fillReview.SandboxOnly -ne $true -or $fillReview.ProductionArtifactDetected -ne $false) { Fail "R007 fill review not ready" }

$pre = Read-Json "phase-exec-sandbox-r008-pre-flatten-position-reconciliation.json"
if ($pre.PositionSource -ne "FillReportDerived" -or $pre.ProductionPositionQueryUsed -ne $false -or @($pre.Lines).Count -ne 7 -or [decimal]$pre.GrossOpenQuantity -ne 0.7) { Fail "Pre-flatten reconciliation invalid" }
$sourceClass = Read-Json "phase-exec-sandbox-r008-position-source-classification.json"
if ($sourceClass.ProductionPositionQueryUsed -ne $false -or $sourceClass.PositionSource -ne "FillReportDerived") { Fail "Position source classification unsafe" }

$plan = Read-Json "phase-exec-sandbox-r008-flatten-order-plan.json"
if ($plan.PlannedOrderCount -ne 7 -or [decimal]$plan.PlannedTotalQuantity -ne 0.7 -or $plan.OneFlattenOrderPerOpenPosition -ne $true) { Fail "Flatten plan count/quantity invalid" }
foreach ($line in $plan.Lines) {
    if ($line.FlattenSide -ne "Sell") { Fail "Flatten side must be Sell for R007 Buy position $($line.Symbol)" }
    if ([decimal]$line.FlattenQuantity -ne 0.1) { Fail "Flatten quantity must match R007 fill for $($line.Symbol)" }
    if ($line.SandboxOnly -ne $true -or $line.ProductionOrder -ne $false) { Fail "Flatten plan line unsafe for $($line.Symbol)" }
}

$guardrail = Read-Json "phase-exec-sandbox-r008-sandbox-guardrail-validation.json"
if ($guardrail.SandboxCredentialPresent -ne $true -or $guardrail.CredentialValuesRedacted -ne $true -or $guardrail.ProductionCredentialDetected -ne $false) { Fail "Credential guardrail failed" }
if ($guardrail.MaxSandboxFlattenOrderCount -ne 7 -or [decimal]$guardrail.MaxFlattenQuantityPerSymbol -ne 0.1 -or [decimal]$guardrail.MaxTotalFlattenQuantity -ne 0.7) { Fail "Flatten caps incorrect" }
if ($guardrail.PlannedFlattenOrderCount -gt 7 -or [decimal]$guardrail.PlannedTotalFlattenQuantity -gt 0.7) { Fail "Planned flatten cap exceeded" }
foreach ($property in @("SchedulerAllowed", "DirectCrossExecutionAllowed", "NonmajorExecutionAllowed", "Legacy06AcceptedAsFutureCanonical", "SecretsPersisted")) {
    if ($guardrail.$property -ne $false) { Fail "Forbidden guardrail enabled: $property" }
}

$submissions = Read-Json "phase-exec-sandbox-r008-sandbox-flatten-submission-results.json"
if ($submissions.SubmittedOrderCount -gt 7 -or [decimal]$submissions.TotalSubmittedQuantity -gt 0.7) { Fail "Flatten submission caps exceeded" }
if ($submissions.SandboxOnly -ne $true -or $submissions.ProductionSubmission -ne $false) { Fail "Flatten submissions unsafe" }
$logon = Read-Json "phase-exec-sandbox-r008-fix-logon-confirmation.json"
if ($submissions.SubmittedOrderCount -gt 0 -and ($logon.LogonConfirmed -ne $true -or $logon.FlattenOrderSentBeforeLogonConfirmed -ne $false)) { Fail "Flatten order sent before confirmed logon" }

$post = Read-Json "phase-exec-sandbox-r008-post-flatten-reconciliation.json"
if ($post.ProductionMutationDetected -ne $false -or $post.SandboxOnly -ne $true) { Fail "Post-flatten reconciliation unsafe" }
$flat = Read-Json "phase-exec-sandbox-r008-flat-state-audit.json"
if ($flat.ProductionMutationDetected -ne $false -or $flat.SandboxOnly -ne $true) { Fail "Flat-state audit unsafe" }
$decision = Read-Json "phase-exec-sandbox-r008-flatten-decision.json"
if ($decision.NotProductionApproval -ne $true) { Fail "Decision implies production approval" }
if ($post.ExpectedResidualQuantity -eq 0 -and $decision.Decision -ne "R009SandboxPositionsFlattened") { Fail "Flat decision mismatch" }

$routes = Read-Json "phase-exec-sandbox-r008-sandbox-flatten-routes.json"
if ($routes.ProductionRouteCreated -ne $false -or $routes.SandboxOnly -ne $true) { Fail "Route artifact unsafe" }
$reports = Read-Json "phase-exec-sandbox-r008-sandbox-flatten-execution-reports.json"
if ($reports.ProductionExecutionReport -ne $false -or $reports.SandboxOnly -ne $true) { Fail "Execution report artifact unsafe" }
$fills = Read-Json "phase-exec-sandbox-r008-sandbox-flatten-fill-reports.json"
if ($fills.ProductionFill -ne $false -or $fills.SandboxOnly -ne $true) { Fail "Fill artifact unsafe" }

$direct = Read-Json "phase-exec-sandbox-r008-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false -or $direct.EURGBPFlattened -ne $false) { Fail "Direct-cross exclusion weakened" }
$whitelist = Read-Json "phase-exec-sandbox-r008-usd-pair-whitelist-preservation.json"
if ($whitelist.NonWhitelistedSymbolAllowed -ne $false -or $whitelist.AudusdStatus -ne "SupportedAndNotFailed" -or $whitelist.AudusdMisclassified -ne $false) { Fail "Whitelist/AUDUSD preservation failed" }
$usdjpy = Read-Json "phase-exec-sandbox-r008-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8" -or $usdjpy.CaveatPreserved -ne $true) { Fail "USDJPY caveat weakened" }
$legacy = Read-Json "phase-exec-sandbox-r008-legacy-compatibility-preservation.json"
if ($legacy.Legacy06AcceptedAsFutureCanonical -ne $false -or $legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 accepted as future canonical" }
$cost = Read-Json "phase-exec-sandbox-r008-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$nonmajor = Read-Json "phase-exec-sandbox-r008-nonmajor-calibration-preservation.json"
if ($nonmajor.SandboxExecutionAllowed -ne $false) { Fail "Nonmajor execution allowed" }

$forbidden = Read-Json "phase-exec-sandbox-r008-forbidden-actions-audit.json"
foreach ($property in @(
    "LmaxProductionUsed",
    "ProductionCredentialsUsed",
    "NonSandboxBrokerRouteUsed",
    "CredentialValuesPrintedOrPersisted",
    "PolygonCalled",
    "UnrelatedExternalApiCalled",
    "SchedulerServicePollingBackgroundJobIntroduced",
    "FlattenOrderSentBeforeLogonConfirmed",
    "MoreThanMaxSandboxFlattenOrderCountSubmitted",
    "FlattenQuantityExceedsOriginalFilledQuantity",
    "FlattenedSymbolNotPresentInR007FillsWithoutApproval",
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

$secretAudit = Read-Json "phase-exec-sandbox-r008-no-secret-persistence-audit.json"
if ($secretAudit.SecretValuesSerialized -ne $false -or $secretAudit.CredentialVariableNamesOnly -ne $true -or $secretAudit.CredentialValuesRedacted -ne $true) { Fail "Secret persistence audit failed" }
$combined = (Get-ChildItem -LiteralPath $artifactDir -File -Filter "phase-exec-sandbox-r008-*.json" | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
foreach ($banned in @("QQ_LMAX_FIX_PASSWORD", "FixPassword`":`"", "FixUsername`":`"", "Password`":`"", "Username`":`"")) {
    if ($combined -match [regex]::Escape($banned)) { Fail "Possible credential value field persisted: $banned" }
}

$evidence = Read-Json "phase-exec-sandbox-r008-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedSandboxTests -ne "Passed") { Fail "Focused sandbox tests missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-SANDBOX-R008 validator passed."
