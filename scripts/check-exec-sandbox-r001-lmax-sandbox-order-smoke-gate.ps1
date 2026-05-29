$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"

function Fail {
    param([string]$Message)
    throw "EXEC-SANDBOX-R001 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-sandbox-r001-summary.md",
    "phase-exec-sandbox-r001-r009-contract-reference.json",
    "phase-exec-sandbox-r001-lmax-sandbox-config-discovery.json",
    "phase-exec-sandbox-r001-production-config-rejection-check.json",
    "phase-exec-sandbox-r001-sandbox-guardrail-contract.json",
    "phase-exec-sandbox-r001-r009-sandbox-execution-intent.json",
    "phase-exec-sandbox-r001-r009-sandbox-order-intent.json",
    "phase-exec-sandbox-r001-pretrade-sandbox-risk-check.json",
    "phase-exec-sandbox-r001-sandbox-route-contract.json",
    "phase-exec-sandbox-r001-sandbox-submission-result.json",
    "phase-exec-sandbox-r001-sandbox-execution-report.json",
    "phase-exec-sandbox-r001-sandbox-fill-report.json",
    "phase-exec-sandbox-r001-sandbox-reconciliation-result.json",
    "phase-exec-sandbox-r001-sandbox-audit-record.json",
    "phase-exec-sandbox-r001-no-production-broker-audit.json",
    "phase-exec-sandbox-r001-no-production-order-audit.json",
    "phase-exec-sandbox-r001-no-production-route-audit.json",
    "phase-exec-sandbox-r001-no-production-ledger-audit.json",
    "phase-exec-sandbox-r001-no-production-state-mutation-audit.json",
    "phase-exec-sandbox-r001-direct-cross-exclusion-preservation.json",
    "phase-exec-sandbox-r001-usd-pair-whitelist-preservation.json",
    "phase-exec-sandbox-r001-usdjpy-caveat-preservation.json",
    "phase-exec-sandbox-r001-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sandbox-r001-legacy-compatibility-preservation.json",
    "phase-exec-sandbox-r001-cost-guidance-preservation.json",
    "phase-exec-sandbox-r001-nonmajor-calibration-preservation.json",
    "phase-exec-sandbox-r001-forbidden-actions-audit.json",
    "phase-exec-sandbox-r001-next-phase-recommendation.json",
    "phase-exec-sandbox-r001-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$sourcePath = Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009LmaxSandboxOrderSmoke.cs"
$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009LmaxSandboxOrderSmokeTests.cs"
if (-not (Test-Path -LiteralPath $sourcePath)) { Fail "Sandbox order smoke source missing" }
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Focused sandbox tests missing" }
$source = Get-Content -LiteralPath $sourcePath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($token in @(
    "R009SandboxExecutionIntent",
    "R009SandboxOrderIntent",
    "R009SandboxRoute",
    "R009SandboxSubmission",
    "R009SandboxExecutionReport",
    "R009SandboxFill",
    "R009SandboxReconciliationResult",
    "R009SandboxAuditRecord",
    "R009LmaxSandboxOrderPathSmokeGate"
)) {
    if ($source -notmatch [regex]::Escape($token)) { Fail "Source missing sandbox contract token $token" }
}
foreach ($token in @(
    "Missing_sandbox_config_blocks_before_connection",
    "Production_config_is_rejected",
    "Direct_cross_execution_intent_is_rejected",
    "Legacy_06_target_close_is_rejected",
    "Usdjpy_caveat_is_required_and_preserved",
    "Max_order_count_is_enforced",
    "Max_notional_is_enforced",
    "Kill_switch_must_be_open_for_sandbox_only"
)) {
    if ($tests -notmatch [regex]::Escape($token)) { Fail "Focused tests missing scenario $token" }
}

$config = Read-Json "phase-exec-sandbox-r001-lmax-sandbox-config-discovery.json"
if ($config.Status -ne "Blocked") { Fail "Config discovery should be blocked in this workspace" }
if ($config.ConnectionAttempted -ne $false -or $config.SocketOpened -ne $false) { Fail "Connection or socket was attempted despite missing sandbox config" }
if ($config.SandboxConfigPresent -ne $false) { Fail "Validator expected no local LmaxSandbox config for blocked R001" }
if (@($config.Reasons) -notcontains "LmaxSandboxConfigMissing") { Fail "Missing sandbox config reason absent" }

$guardrail = Read-Json "phase-exec-sandbox-r001-sandbox-guardrail-contract.json"
if ($guardrail.Environment -ne "Sandbox" -or $guardrail.BrokerVenue -ne "LMAXSandbox") { Fail "Sandbox guardrail not scoped to LMAXSandbox" }
if ($guardrail.ProductionVenueAllowed -ne $false -or $guardrail.ProductionCredentialsAllowed -ne $false -or $guardrail.SandboxCredentialsRequired -ne $true) { Fail "Sandbox guardrail allows production or weakens sandbox credential requirement" }
if ($guardrail.MaxSandboxOrderCount -gt 3 -or $guardrail.MaxSandboxOrderCount -lt 1) { Fail "Max sandbox order count is not small" }
if ($guardrail.MaxSandboxNotional -le 0) { Fail "Max sandbox notional missing" }
if ($guardrail.DirectCrossExecutionAllowed -ne $false -or $guardrail.Legacy06AcceptedAsFutureCanonical -ne $false) { Fail "Direct cross or legacy target close guardrail weakened" }

$risk = Read-Json "phase-exec-sandbox-r001-pretrade-sandbox-risk-check.json"
if ($risk.Status -ne "Blocked") { Fail "Risk check should block before submission" }
if ($risk.NoProductionRoute -ne $true -or $risk.NoProductionLedger -ne $true -or $risk.NoScheduler -ne $true) { Fail "Risk check does not block production route/ledger/scheduler" }

$orderIntent = Read-Json "phase-exec-sandbox-r001-r009-sandbox-order-intent.json"
if ($orderIntent.Created -ne $false -or $orderIntent.ProductionOrder -ne $false -or $orderIntent.IsLiveProduction -ne $false -or $orderIntent.NoProductionLedgerCommit -ne $true) {
    Fail "Blocked order-intent artifact is unsafe"
}

$route = Read-Json "phase-exec-sandbox-r001-sandbox-route-contract.json"
if ($route.RouteCreated -ne $false -or $route.ProductionRoute -ne $false -or $route.NonSandboxBrokerRoute -ne $false -or $route.ProductionCredentialsUsed -ne $false) {
    Fail "Route artifact allows production or non-sandbox broker route"
}

$submission = Read-Json "phase-exec-sandbox-r001-sandbox-submission-result.json"
if ($submission.SubmissionAttempted -ne $false -or $submission.SubmittedOrderCount -ne 0 -or $submission.SubmittedNotional -ne 0 -or $submission.ProductionSubmission -ne $false -or $submission.SandboxOnly -ne $true) {
    Fail "Submission artifact is not blocked sandbox-only"
}
if ($submission.SubmittedOrderCount -gt $submission.MaxSandboxOrderCount) { Fail "More sandbox orders than allowed submitted" }
if ($submission.SubmittedNotional -gt $submission.MaxSandboxNotional) { Fail "Sandbox order notional exceeds cap" }

$execReport = Read-Json "phase-exec-sandbox-r001-sandbox-execution-report.json"
if ($execReport.ProductionExecutionReport -ne $false -or $execReport.SandboxOnly -ne $true) { Fail "Execution report artifact is not sandbox-only" }
$fill = Read-Json "phase-exec-sandbox-r001-sandbox-fill-report.json"
if ($fill.ProductionFill -ne $false -or $fill.SandboxOnly -ne $true) { Fail "Fill artifact is not sandbox-only" }
$reconciliation = Read-Json "phase-exec-sandbox-r001-sandbox-reconciliation-result.json"
if ($reconciliation.ProductionLedgerMutation -ne $false -or $reconciliation.ProductionStateMutation -ne $false -or $reconciliation.SandboxOnly -ne $true) { Fail "Reconciliation allows production ledger/state mutation" }

$direct = Read-Json "phase-exec-sandbox-r001-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false -or $direct.DirectCrossExecutionIntentRejected -ne $true) { Fail "Direct-cross exclusion weakened" }
$whitelist = Read-Json "phase-exec-sandbox-r001-usd-pair-whitelist-preservation.json"
if ($whitelist.NonWhitelistedSymbolAllowed -ne $false -or $whitelist.AudusdStatus -ne "SupportedAndNotFailed") { Fail "Whitelist/AUDUSD preservation failed" }
foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")) {
    if (@($whitelist.WhitelistedSymbols) -notcontains $symbol) { Fail "Whitelist missing $symbol" }
}
$usdjpy = Read-Json "phase-exec-sandbox-r001-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8") {
    Fail "USDJPY caveat weakened"
}
$legacy = Read-Json "phase-exec-sandbox-r001-legacy-compatibility-preservation.json"
if ($legacy.Legacy06AcceptedAsFutureCanonical -ne $false -or $legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 accepted as future canonical" }
$cost = Read-Json "phase-exec-sandbox-r001-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$nonmajor = Read-Json "phase-exec-sandbox-r001-nonmajor-calibration-preservation.json"
if ($nonmajor.SandboxExecutionAllowed -ne $false) { Fail "Nonmajor/EM/scandi/CNH execution allowed" }

$forbidden = Read-Json "phase-exec-sandbox-r001-forbidden-actions-audit.json"
foreach ($property in @(
    "LmaxProductionUsed",
    "ProductionCredentialsUsed",
    "NonSandboxBrokerRouteUsed",
    "PolygonCalled",
    "UnrelatedExternalApiCalled",
    "SchedulerServicePollingBackgroundJobIntroduced",
    "MoreSandboxOrdersThanAllowedSubmitted",
    "OrderNotionalExceedsSandboxCap",
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
if ($forbidden.SandboxArtifactsClearlyMarkedSandboxOnly -ne $true) { Fail "Sandbox artifacts are not clearly marked SandboxOnly" }

$evidence = Read-Json "phase-exec-sandbox-r001-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedSandboxTests -ne "Passed") { Fail "Focused sandbox test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-SANDBOX-R001 validator passed."
