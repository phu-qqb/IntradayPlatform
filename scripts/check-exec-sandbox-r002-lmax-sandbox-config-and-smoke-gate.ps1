$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"

function Fail {
    param([string]$Message)
    throw "EXEC-SANDBOX-R002 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-sandbox-r002-summary.md",
    "phase-exec-sandbox-r002-r001-reference.json",
    "phase-exec-sandbox-r002-lmax-sandbox-config-contract.json",
    "phase-exec-sandbox-r002-lmax-sandbox-config-validation.json",
    "phase-exec-sandbox-r002-credential-profile-validation.json",
    "phase-exec-sandbox-r002-production-route-blocking-check.json",
    "phase-exec-sandbox-r002-sandbox-guardrail-contract.json",
    "phase-exec-sandbox-r002-operator-sandbox-approval.json",
    "phase-exec-sandbox-r002-r009-sandbox-execution-intent.json",
    "phase-exec-sandbox-r002-pretrade-sandbox-risk-check.json",
    "phase-exec-sandbox-r002-r009-sandbox-decision.json",
    "phase-exec-sandbox-r002-sandbox-order-intent.json",
    "phase-exec-sandbox-r002-sandbox-route.json",
    "phase-exec-sandbox-r002-sandbox-submission-result.json",
    "phase-exec-sandbox-r002-sandbox-ack-reject.json",
    "phase-exec-sandbox-r002-sandbox-execution-report.json",
    "phase-exec-sandbox-r002-sandbox-fill-report.json",
    "phase-exec-sandbox-r002-sandbox-reconciliation-result.json",
    "phase-exec-sandbox-r002-sandbox-audit-record.json",
    "phase-exec-sandbox-r002-no-production-broker-audit.json",
    "phase-exec-sandbox-r002-no-production-order-audit.json",
    "phase-exec-sandbox-r002-no-production-route-audit.json",
    "phase-exec-sandbox-r002-no-production-fill-report-audit.json",
    "phase-exec-sandbox-r002-no-production-ledger-audit.json",
    "phase-exec-sandbox-r002-no-production-state-mutation-audit.json",
    "phase-exec-sandbox-r002-direct-cross-exclusion-preservation.json",
    "phase-exec-sandbox-r002-usd-pair-whitelist-preservation.json",
    "phase-exec-sandbox-r002-usdjpy-caveat-preservation.json",
    "phase-exec-sandbox-r002-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sandbox-r002-legacy-compatibility-preservation.json",
    "phase-exec-sandbox-r002-cost-guidance-preservation.json",
    "phase-exec-sandbox-r002-nonmajor-calibration-preservation.json",
    "phase-exec-sandbox-r002-forbidden-actions-audit.json",
    "phase-exec-sandbox-r002-next-phase-recommendation.json",
    "phase-exec-sandbox-r002-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$sourcePath = Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009LmaxSandboxOrderSmoke.cs"
$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009LmaxSandboxConfigCompletionTests.cs"
if (-not (Test-Path -LiteralPath $sourcePath)) { Fail "Sandbox order smoke source missing" }
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Focused R002 tests missing" }
$source = Get-Content -LiteralPath $sourcePath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($token in @(
    "R009LmaxSandboxConfigContract",
    "R009SandboxCredentialProfileValidation",
    "R009LmaxSandboxConfigValidation",
    "ValidateSandboxConfigContract",
    "ValidateCredentialProfile"
)) {
    if ($source -notmatch [regex]::Escape($token)) { Fail "Source missing R002 token $token" }
}
foreach ($token in @(
    "Missing_sandbox_config_contract_blocks_smoke_order",
    "Sandbox_credential_profile_validation_redacts_values",
    "Production_credential_profile_is_rejected",
    "Complete_sandbox_contract_is_ready_for_one_bounded_order",
    "Contract_requires_open_sandbox_kill_switch"
)) {
    if ($tests -notmatch [regex]::Escape($token)) { Fail "Focused R002 tests missing scenario $token" }
}

$config = Read-Json "phase-exec-sandbox-r002-lmax-sandbox-config-validation.json"
if ($config.Status -ne "Blocked") { Fail "Config should be blocked in this workspace" }
if ($config.ExplicitSandboxConfigPresent -ne $false -or $config.SafeForOneBoundedSandboxOrder -ne $false -or $config.ConnectionAttempted -ne $false -or $config.SubmissionAllowed -ne $false) {
    Fail "Config validation is not safely blocked"
}
if (@($config.Reasons) -notcontains "LmaxSandboxConfigMissing") { Fail "Missing LmaxSandboxConfigMissing reason" }

$credential = Read-Json "phase-exec-sandbox-r002-credential-profile-validation.json"
if ($credential.CredentialValuesRedacted -ne $true -or $credential.CredentialValuesPrintedOrPersisted -ne $false) { Fail "Credential values are not redacted" }
if ($credential.CredentialSourceType -ne "EnvVars") { Fail "Credential source should be EnvVars after operator clarification" }
if ($credential.SandboxCredentialPresent -ne $true) { Fail "Sandbox credential environment-variable presence was not detected" }
foreach ($name in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    if ($credential.CredentialVariablePresence.$name -ne $true) { Fail "Credential variable presence missing for $name" }
}
if (@($credential.MissingProfileNames).Count -ne 0) { Fail "Credential variable names should not be missing in this environment" }
if ($credential.ProductionCredentialDetected -ne $false) { Fail "Unexpected production credential detected in blocked artifact" }

$contract = Read-Json "phase-exec-sandbox-r002-lmax-sandbox-config-contract.json"
if ($contract.Environment -ne "Sandbox" -or $contract.BrokerVenue -ne "LMAXSandbox") { Fail "Sandbox contract is not scoped to LMAXSandbox" }
if ($contract.ProductionVenueAllowed -ne $false -or $contract.ProductionCredentialsAllowed -ne $false -or $contract.SandboxCredentialsRequired -ne $true) { Fail "Sandbox contract allows production or weakens sandbox credential requirement" }
if ($contract.MaxSandboxOrderCount -ne 1 -or $contract.MaxSandboxNotional -le 0 -or $contract.MaxSandboxNotional -gt 100) { Fail "Sandbox contract does not cap one tiny order" }
if ($contract.DirectCrossExecutionAllowed -ne $false -or $contract.NonmajorExecutionAllowed -ne $false -or $contract.PaperLedgerCommitAllowed -ne $false -or $contract.ProductionLedgerCommitAllowed -ne $false -or $contract.StateMutationAllowed -ne $false) {
    Fail "Sandbox contract allows forbidden execution/ledger/state path"
}

$production = Read-Json "phase-exec-sandbox-r002-production-route-blocking-check.json"
foreach ($property in @("NoProductionEndpoint", "NoProductionAccount", "NoProductionCredentialProfile", "NoProductionRoute", "NoProductionLedger", "NoProductionStateMutation", "ProductionRouteBlocked")) {
    if ($production.$property -ne $true) { Fail "Production blocking check failed: $property" }
}

$risk = Read-Json "phase-exec-sandbox-r002-pretrade-sandbox-risk-check.json"
if ($risk.Status -ne "Blocked") { Fail "Pretrade risk should be blocked" }
if ($risk.SymbolWhitelisted -ne $true -or $risk.DirectCrossRejected -ne $true -or $risk.CanonicalQuarterHourTargetClose -ne $true -or $risk.Legacy06Rejected -ne $true) { Fail "Risk gate weakened symbol/timing checks" }
if ($risk.NoProductionRoute -ne $true -or $risk.NoProductionLedger -ne $true) { Fail "Risk gate does not block production route/ledger" }

$decision = Read-Json "phase-exec-sandbox-r002-r009-sandbox-decision.json"
if ($decision.R009DecisionProduced -ne $false -or $decision.DecisionStatus -ne "BlockedBeforeR009SandboxDecision") { Fail "R009 sandbox decision should be blocked before conversion" }
$order = Read-Json "phase-exec-sandbox-r002-sandbox-order-intent.json"
if ($order.Created -ne $false -or $order.ProductionOrder -ne $false -or $order.IsLiveProduction -ne $false -or $order.NoProductionLedgerCommit -ne $true -or $order.NoProductionStateMutation -ne $true) {
    Fail "Blocked sandbox order intent artifact unsafe"
}
$route = Read-Json "phase-exec-sandbox-r002-sandbox-route.json"
if ($route.RouteCreated -ne $false -or $route.ProductionRoute -ne $false -or $route.NonSandboxBrokerRoute -ne $false -or $route.ProductionCredentialsUsed -ne $false) { Fail "Route artifact unsafe" }
$submission = Read-Json "phase-exec-sandbox-r002-sandbox-submission-result.json"
if ($submission.SubmissionAttempted -ne $false -or $submission.SubmittedOrderCount -ne 0 -or $submission.SubmittedNotional -ne 0 -or $submission.ProductionSubmission -ne $false -or $submission.SandboxOnly -ne $true) { Fail "Submission artifact unsafe" }
if ($submission.SubmittedOrderCount -gt $submission.MaxSandboxOrderCount) { Fail "More sandbox orders than allowed submitted" }

$ack = Read-Json "phase-exec-sandbox-r002-sandbox-ack-reject.json"
if ($ack.ProductionAckReject -ne $false -or $ack.SandboxOnly -ne $true) { Fail "Ack/reject artifact unsafe" }
$execReport = Read-Json "phase-exec-sandbox-r002-sandbox-execution-report.json"
if ($execReport.ProductionExecutionReport -ne $false -or $execReport.SandboxOnly -ne $true) { Fail "Execution report artifact unsafe" }
$fill = Read-Json "phase-exec-sandbox-r002-sandbox-fill-report.json"
if ($fill.ProductionFill -ne $false -or $fill.SandboxOnly -ne $true) { Fail "Fill artifact unsafe" }
$reconciliation = Read-Json "phase-exec-sandbox-r002-sandbox-reconciliation-result.json"
if ($reconciliation.ProductionOrderCreated -ne $false -or $reconciliation.ProductionRouteCreated -ne $false -or $reconciliation.ProductionFillOrReportCreated -ne $false -or $reconciliation.ProductionLedgerMutation -ne $false -or $reconciliation.ProductionStateMutation -ne $false -or $reconciliation.SandboxOnly -ne $true) {
    Fail "Reconciliation artifact unsafe"
}

$direct = Read-Json "phase-exec-sandbox-r002-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false -or $direct.DirectCrossExecutionIntentRejected -ne $true) { Fail "Direct-cross exclusion weakened" }
$whitelist = Read-Json "phase-exec-sandbox-r002-usd-pair-whitelist-preservation.json"
if ($whitelist.NonWhitelistedSymbolAllowed -ne $false -or $whitelist.AudusdStatus -ne "SupportedAndNotFailed") { Fail "Whitelist/AUDUSD preservation failed" }
$usdjpy = Read-Json "phase-exec-sandbox-r002-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8") { Fail "USDJPY caveat weakened" }
$legacy = Read-Json "phase-exec-sandbox-r002-legacy-compatibility-preservation.json"
if ($legacy.Legacy06AcceptedAsFutureCanonical -ne $false -or $legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 accepted as future canonical" }
$cost = Read-Json "phase-exec-sandbox-r002-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$nonmajor = Read-Json "phase-exec-sandbox-r002-nonmajor-calibration-preservation.json"
if ($nonmajor.SandboxExecutionAllowed -ne $false) { Fail "Nonmajor/EM/scandi/CNH execution allowed" }

$forbidden = Read-Json "phase-exec-sandbox-r002-forbidden-actions-audit.json"
foreach ($property in @(
    "LmaxProductionUsed",
    "ProductionCredentialsUsed",
    "NonSandboxBrokerRouteUsed",
    "CredentialValuesPrintedOrPersisted",
    "PolygonCalled",
    "UnrelatedExternalApiCalled",
    "SchedulerServicePollingBackgroundJobIntroduced",
    "MoreSandboxOrdersThanAllowedSubmitted",
    "SandboxNotionalExceedsConfiguredCap",
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
if ($forbidden.SandboxArtifactsClearlyMarkedSandboxOnly -ne $true) { Fail "Sandbox artifacts not clearly marked SandboxOnly" }

$evidence = Read-Json "phase-exec-sandbox-r002-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedSandboxTests -ne "Passed") { Fail "Focused sandbox test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-SANDBOX-R002 validator passed."
