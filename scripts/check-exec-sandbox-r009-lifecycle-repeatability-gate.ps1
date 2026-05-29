$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"

function Fail {
    param([string]$Message)
    throw "EXEC-SANDBOX-R009 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$required = @(
    "phase-exec-sandbox-r009-summary.md",
    "phase-exec-sandbox-r009-r007-r008-reference.json",
    "phase-exec-sandbox-r009-lifecycle-acceptance-review.json",
    "phase-exec-sandbox-r009-sandbox-oms-state-model.json",
    "phase-exec-sandbox-r009-state-transition-contract.json",
    "phase-exec-sandbox-r009-idempotency-contract.json",
    "phase-exec-sandbox-r009-duplicate-prevention-results.json",
    "phase-exec-sandbox-r009-repeatability-guardrail-validation.json",
    "phase-exec-sandbox-r009-repeatability-open-order-intent.json",
    "phase-exec-sandbox-r009-repeatability-open-submission-result.json",
    "phase-exec-sandbox-r009-repeatability-open-execution-report.json",
    "phase-exec-sandbox-r009-repeatability-open-fill-report.json",
    "phase-exec-sandbox-r009-repeatability-flatten-order-intent.json",
    "phase-exec-sandbox-r009-repeatability-flatten-submission-result.json",
    "phase-exec-sandbox-r009-repeatability-flatten-execution-report.json",
    "phase-exec-sandbox-r009-repeatability-flatten-fill-report.json",
    "phase-exec-sandbox-r009-repeatability-reconciliation-result.json",
    "phase-exec-sandbox-r009-final-sandbox-reconciliation.json",
    "phase-exec-sandbox-r009-lifecycle-decision.json",
    "phase-exec-sandbox-r009-no-secret-persistence-audit.json",
    "phase-exec-sandbox-r009-no-production-broker-audit.json",
    "phase-exec-sandbox-r009-no-production-order-audit.json",
    "phase-exec-sandbox-r009-no-production-route-audit.json",
    "phase-exec-sandbox-r009-no-production-fill-report-audit.json",
    "phase-exec-sandbox-r009-no-production-ledger-audit.json",
    "phase-exec-sandbox-r009-no-production-state-mutation-audit.json",
    "phase-exec-sandbox-r009-direct-cross-exclusion-preservation.json",
    "phase-exec-sandbox-r009-usd-pair-whitelist-preservation.json",
    "phase-exec-sandbox-r009-usdjpy-caveat-preservation.json",
    "phase-exec-sandbox-r009-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sandbox-r009-legacy-compatibility-preservation.json",
    "phase-exec-sandbox-r009-cost-guidance-preservation.json",
    "phase-exec-sandbox-r009-nonmajor-calibration-preservation.json",
    "phase-exec-sandbox-r009-forbidden-actions-audit.json",
    "phase-exec-sandbox-r009-next-phase-recommendation.json",
    "phase-exec-sandbox-r009-build-test-validator-evidence.json"
)
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) { Fail "Required artifact missing: $name" }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009LmaxSandboxOrderSmoke.cs") -Raw
$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009SandboxLifecycleRepeatabilityTests.cs") -Raw
foreach ($token in @(
    "R009SandboxOmsState",
    "R009SandboxOmsStateModel",
    "R009SandboxIdempotencyContract",
    "R009SandboxDuplicatePreventionResult",
    "BuildSandboxOmsStateModel",
    "BuildSandboxIdempotencyContract",
    "ValidateDuplicatePrevention"
)) {
    if ($source -notmatch [regex]::Escape($token)) { Fail "Source missing R009 lifecycle token $token" }
}
foreach ($token in @(
    "Sandbox_oms_state_model_forbids_production_order_and_ledger_states",
    "Idempotency_contract_rejects_duplicate_clordid_and_production_fallback",
    "Duplicate_prevention_blocks_already_flattened_replay_without_approval",
    "Duplicate_prevention_rejects_production_order_fallback"
)) {
    if ($tests -notmatch [regex]::Escape($token)) { Fail "Focused R009 test missing $token" }
}

$lifecycle = Read-Json "phase-exec-sandbox-r009-lifecycle-acceptance-review.json"
if ($lifecycle.R007SubmittedOrders -ne 7 -or $lifecycle.R007FilledOrders -ne 7 -or $lifecycle.R008FlattenSubmittedOrders -ne 7 -or $lifecycle.R008FlattenFilledOrders -ne 7) { Fail "R007/R008 lifecycle counts not accepted" }
if ([decimal]$lifecycle.R008ExpectedResidualQuantity -ne 0 -or $lifecycle.LifecycleAccepted -ne $true) { Fail "Lifecycle not accepted or residual not flat" }
if ($lifecycle.ProductionOrderRouteFillReportLedgerStateMutation -ne $false) { Fail "Lifecycle review detected production mutation" }

$model = Read-Json "phase-exec-sandbox-r009-sandbox-oms-state-model.json"
if ($model.ProductionOrderStateForbidden -ne $true -or $model.ProductionLedgerStateForbidden -ne $true -or $model.SupportsReconciliationState -ne $true -or $model.SupportsIdempotencyState -ne $true) { Fail "OMS state model boundary flags invalid" }
foreach ($state in @("SandboxSubmitted", "SandboxFilled", "SandboxFlattenSubmitted", "SandboxFlatConfirmed", "SandboxTerminal")) {
    if (@($model.Transitions | Where-Object { $_.To -eq $state -or $_.From -eq $state }).Count -eq 0) { Fail "OMS state missing $state" }
}
foreach ($transition in $model.Transitions) {
    if ($transition.ProductionOrderStateForbidden -ne $true -or $transition.LedgerStateForbidden -ne $true -or $transition.SandboxOrderState -ne $true) { Fail "Unsafe state transition $($transition.From) -> $($transition.To)" }
}

$idempotency = Read-Json "phase-exec-sandbox-r009-idempotency-contract.json"
if ($idempotency.DuplicateClOrdIDRejected -ne $true -or $idempotency.SameIntentReplaySafe -ne $true -or $idempotency.SameIntentDifferentQuantityConflict -ne $true -or $idempotency.AlreadyFlattenedPositionRequiresExplicitNewSandboxApproval -ne $true -or $idempotency.NoProductionOrderFallback -ne $true) { Fail "Idempotency contract unsafe" }
$dupe = Read-Json "phase-exec-sandbox-r009-duplicate-prevention-results.json"
if ($dupe.DuplicateClOrdIDRejected -ne $true -or $dupe.AlreadyFlattenedReplayBlocked -ne $true -or $dupe.NoDuplicateSubmissionForSameIdempotencyKey -ne $true -or $dupe.NoProductionOrderFallback -ne $true) { Fail "Duplicate prevention failed" }

$guardrail = Read-Json "phase-exec-sandbox-r009-repeatability-guardrail-validation.json"
if ($guardrail.SandboxCredentialPresent -ne $true -or $guardrail.CredentialValuesRedacted -ne $true -or $guardrail.ProductionCredentialDetected -ne $false) { Fail "Credential guardrail failed" }
if ($guardrail.MaxRepeatabilityOpenOrders -ne 1 -or $guardrail.MaxRepeatabilityFlattenOrders -ne 1 -or $guardrail.TotalRepeatabilityOrders -ne 2) { Fail "Repeatability order caps invalid" }
foreach ($property in @("DirectCrossExecutionAllowed", "NonmajorExecutionAllowed", "SchedulerAllowed", "AutomaticExecutionAllowed", "Legacy06AcceptedAsFutureCanonical")) {
    if ($guardrail.$property -ne $false) { Fail "Forbidden guardrail enabled: $property" }
}

$openIntent = Read-Json "phase-exec-sandbox-r009-repeatability-open-order-intent.json"
$flattenIntent = Read-Json "phase-exec-sandbox-r009-repeatability-flatten-order-intent.json"
if ($openIntent.Symbol -ne "EURUSD" -or $openIntent.Side -ne "Buy" -or [decimal]$openIntent.Quantity -ne 0.1 -or $openIntent.SandboxOnly -ne $true -or $openIntent.ProductionOrder -ne $false) { Fail "Open intent unsafe" }
if ($flattenIntent.Symbol -ne "EURUSD" -or $flattenIntent.Side -ne "Sell" -or [decimal]$flattenIntent.Quantity -ne 0.1 -or $flattenIntent.SandboxOnly -ne $true -or $flattenIntent.ProductionOrder -ne $false) { Fail "Flatten intent unsafe" }

$openSubmission = Read-Json "phase-exec-sandbox-r009-repeatability-open-submission-result.json"
$flattenSubmission = Read-Json "phase-exec-sandbox-r009-repeatability-flatten-submission-result.json"
$submittedOpenCount = if ($openSubmission.Submitted) { 1 } else { 0 }
$submittedFlattenCount = if ($flattenSubmission.Submitted) { 1 } else { 0 }
if ($submittedOpenCount -gt 1) { Fail "More than one repeatability open order submitted" }
if ($submittedFlattenCount -gt 1) { Fail "More than one repeatability flatten order submitted" }
if ($openSubmission.SandboxOnly -ne $true -or $openSubmission.ProductionSubmission -ne $false -or $flattenSubmission.SandboxOnly -ne $true -or $flattenSubmission.ProductionSubmission -ne $false) { Fail "Submission artifact unsafe" }
if ($submittedFlattenCount -gt 0 -and $openSubmission.AcceptedOrAcked -ne $true) { Fail "Flatten submitted before accepted open evidence" }

$openReport = Read-Json "phase-exec-sandbox-r009-repeatability-open-execution-report.json"
$flattenReport = Read-Json "phase-exec-sandbox-r009-repeatability-flatten-execution-report.json"
if ($openReport.ProductionExecutionReport -ne $false -or $openReport.SandboxOnly -ne $true -or $flattenReport.ProductionExecutionReport -ne $false -or $flattenReport.SandboxOnly -ne $true) { Fail "Execution report artifact unsafe" }
$openFill = Read-Json "phase-exec-sandbox-r009-repeatability-open-fill-report.json"
$flattenFill = Read-Json "phase-exec-sandbox-r009-repeatability-flatten-fill-report.json"
if ($openFill.ProductionFill -ne $false -or $openFill.SandboxOnly -ne $true -or $flattenFill.ProductionFill -ne $false -or $flattenFill.SandboxOnly -ne $true) { Fail "Fill artifact unsafe" }

$repeatRecon = Read-Json "phase-exec-sandbox-r009-repeatability-reconciliation-result.json"
if ($repeatRecon.ProductionMutationDetected -ne $false -or $repeatRecon.SandboxOnly -ne $true) { Fail "Repeatability reconciliation unsafe" }
if ($submittedOpenCount -eq 1 -and $submittedFlattenCount -eq 1 -and [decimal]$repeatRecon.ExpectedResidualQuantity -ne 0) { Fail "Repeatability residual not flat after open+flatten" }
$finalRecon = Read-Json "phase-exec-sandbox-r009-final-sandbox-reconciliation.json"
if ($finalRecon.ProductionMutationDetected -ne $false -or $finalRecon.SandboxOnly -ne $true) { Fail "Final reconciliation unsafe" }
$decision = Read-Json "phase-exec-sandbox-r009-lifecycle-decision.json"
if ($decision.NotProductionApproval -ne $true -or $decision.ProductionOrderRouteLedgerStateMutation -ne $false) { Fail "Decision implies production approval" }

$direct = Read-Json "phase-exec-sandbox-r009-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false -or $direct.EURGBPSubmitted -ne $false) { Fail "Direct-cross exclusion weakened" }
$whitelist = Read-Json "phase-exec-sandbox-r009-usd-pair-whitelist-preservation.json"
if ($whitelist.NonWhitelistedSymbolAllowed -ne $false -or $whitelist.AudusdStatus -ne "SupportedAndNotFailed" -or $whitelist.AudusdMisclassified -ne $false) { Fail "Whitelist/AUDUSD preservation failed" }
$usdjpy = Read-Json "phase-exec-sandbox-r009-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8" -or $usdjpy.CaveatPreserved -ne $true) { Fail "USDJPY caveat weakened" }
$legacy = Read-Json "phase-exec-sandbox-r009-legacy-compatibility-preservation.json"
if ($legacy.Legacy06AcceptedAsFutureCanonical -ne $false -or $legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 accepted as future canonical" }
$cost = Read-Json "phase-exec-sandbox-r009-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$nonmajor = Read-Json "phase-exec-sandbox-r009-nonmajor-calibration-preservation.json"
if ($nonmajor.SandboxExecutionAllowed -ne $false) { Fail "Nonmajor execution allowed" }

$forbidden = Read-Json "phase-exec-sandbox-r009-forbidden-actions-audit.json"
foreach ($property in @(
    "LmaxProductionUsed",
    "ProductionCredentialsUsed",
    "NonSandboxBrokerRouteUsed",
    "CredentialValuesPrintedOrPersisted",
    "PolygonCalled",
    "UnrelatedExternalApiCalled",
    "SchedulerServicePollingBackgroundJobIntroduced",
    "RepeatabilityOrderSentBeforeLogonConfirmed",
    "MoreThanMaxRepeatabilityOpenOrdersSubmitted",
    "MoreThanMaxRepeatabilityFlattenOrdersSubmitted",
    "AlreadyFlattenedPositionFlattenedTwiceWithoutExplicitApproval",
    "DuplicateClOrdIDAllowed",
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

$secretAudit = Read-Json "phase-exec-sandbox-r009-no-secret-persistence-audit.json"
if ($secretAudit.SecretValuesSerialized -ne $false -or $secretAudit.CredentialVariableNamesOnly -ne $true -or $secretAudit.CredentialValuesRedacted -ne $true) { Fail "Secret persistence audit failed" }
$combined = (Get-ChildItem -LiteralPath $artifactDir -File -Filter "phase-exec-sandbox-r009-*.json" | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
foreach ($banned in @("QQ_LMAX_FIX_PASSWORD", "FixPassword`":`"", "FixUsername`":`"", "Password`":`"", "Username`":`"")) {
    if ($combined -match [regex]::Escape($banned)) { Fail "Possible credential value field persisted: $banned" }
}

$evidence = Read-Json "phase-exec-sandbox-r009-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedSandboxTests -ne "Passed") { Fail "Focused sandbox tests missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-SANDBOX-R009 validator passed."
