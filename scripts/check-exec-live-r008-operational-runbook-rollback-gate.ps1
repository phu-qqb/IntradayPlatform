$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"

function Fail {
    param([string]$Message)
    throw "EXEC-LIVE-R008 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-live-r008-summary.md",
    "phase-exec-live-r008-r007-operator-review-reference.json",
    "phase-exec-live-r008-r009-contract-reference.json",
    "phase-exec-live-r008-operational-runbook.md",
    "phase-exec-live-r008-operational-runbook.json",
    "phase-exec-live-r008-rollback-disable-plan.md",
    "phase-exec-live-r008-rollback-disable-plan.json",
    "phase-exec-live-r008-incident-stop-rules.json",
    "phase-exec-live-r008-operator-checklist.md",
    "phase-exec-live-r008-operator-checklist.json",
    "phase-exec-live-r008-safe-use-examples.json",
    "phase-exec-live-r008-unsafe-use-rejection-examples.json",
    "phase-exec-live-r008-feature-flag-review.json",
    "phase-exec-live-r008-consumer-access-review.json",
    "phase-exec-live-r008-audit-artifact-retention-plan.json",
    "phase-exec-live-r008-no-broker-activation-audit.json",
    "phase-exec-live-r008-no-live-marketdata-audit.json",
    "phase-exec-live-r008-no-scheduler-service-polling-audit.json",
    "phase-exec-live-r008-no-order-created-audit.json",
    "phase-exec-live-r008-no-child-order-audit.json",
    "phase-exec-live-r008-no-executable-schedule-audit.json",
    "phase-exec-live-r008-no-route-no-submission-audit.json",
    "phase-exec-live-r008-no-fill-execution-report-audit.json",
    "phase-exec-live-r008-no-paper-ledger-commit-audit.json",
    "phase-exec-live-r008-no-state-mutation-audit.json",
    "phase-exec-live-r008-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-live-r008-legacy-compatibility-preservation.json",
    "phase-exec-live-r008-direct-cross-exclusion-preservation.json",
    "phase-exec-live-r008-usd-pair-netting-requirement.json",
    "phase-exec-live-r008-usdjpy-caveat-preservation.json",
    "phase-exec-live-r008-cost-guidance-preservation.json",
    "phase-exec-live-r008-nonmajor-calibration-preservation.json",
    "phase-exec-live-r008-no-external-audit.json",
    "phase-exec-live-r008-forbidden-actions-audit.json",
    "phase-exec-live-r008-next-phase-recommendation.json",
    "phase-exec-live-r008-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$runbookText = Get-Content -LiteralPath (Join-Path $artifactDir "phase-exec-live-r008-operational-runbook.md") -Raw
$rollbackText = Get-Content -LiteralPath (Join-Path $artifactDir "phase-exec-live-r008-rollback-disable-plan.md") -Raw
$checklistText = Get-Content -LiteralPath (Join-Path $artifactDir "phase-exec-live-r008-operator-checklist.md") -Raw

foreach ($needle in @(
    "Not executable approval",
    "Not broker-ready",
    "Not live-ready",
    "Not an order generator",
    "ListAuditRecords",
    "ShowAuditRecord",
    "SummarizeBatch",
    "ExportOperatorReport",
    "PreviewReady",
    "HeldMissingReadiness",
    "Rejected",
    "ExecutableApproval=false",
    "Operator approval is review-only"
)) {
    if ($runbookText -notmatch [regex]::Escape($needle)) { Fail "Runbook missing $needle" }
}

foreach ($needle in @(
    "Disable operator access",
    "Preserve audit artifacts",
    "Confirm all kill-switch flags remain disabled",
    "Confirm no scheduler",
    "Confirm no broker route registration",
    "Rollback preserves evidence"
)) {
    if ($rollbackText -notmatch [regex]::Escape($needle)) { Fail "Rollback plan missing $needle" }
}

foreach ($needle in @(
    "Verify feature flags disabled",
    "Verify ReviewOnly=true",
    "Verify NonExecutable=true",
    "Verify NotAnOrder=true",
    "Verify NoBrokerRoute=true",
    "Verify NoPaperLedgerCommit=true",
    "Verify direct-cross exclusion",
    "Verify USDJPY caveat",
    "Verify canonical quarter-hour close",
    "Verify held lines are not orders",
    "Verify audit hash present",
    "Verify output path is artifact-only"
)) {
    if ($checklistText -notmatch [regex]::Escape($needle)) { Fail "Operator checklist missing $needle" }
}

$runbook = Read-Json "phase-exec-live-r008-operational-runbook.json"
if ($runbook.ReviewOnly -ne $true -or $runbook.ExecutableApproval -ne $false -or $runbook.BrokerApproval -ne $false -or $runbook.LiveApproval -ne $false) { Fail "Runbook implies executable/broker/live approval" }
foreach ($mode in @("Execute", "Submit", "Route", "Fill", "CommitLedger", "ActivateBroker", "StartScheduler", "PromoteLive")) {
    if (@($runbook.ForbiddenModes) -notcontains $mode) { Fail "Runbook forbidden mode missing $mode" }
}

$rollback = Read-Json "phase-exec-live-r008-rollback-disable-plan.json"
if ($rollback.AuditArtifactsPreserved -ne $true -or $rollback.BrokerRouteRegistrationAllowed -ne $false -or $rollback.OrderDomainPersistenceAllowed -ne $false -or $rollback.SchedulerServicePollingAllowed -ne $false) { Fail "Rollback plan weakens disabled state" }

$stopRules = Read-Json "phase-exec-live-r008-incident-stop-rules.json"
foreach ($expected in @(
    "Any order-like artifact appears",
    "Any route, submission, fill, or execution report appears",
    "Any executable schedule appears",
    "Any broker or live market data path appears",
    "Any scheduler, service, timer, polling, or background job path appears",
    "Any ledger commit appears",
    "Any live, broker, production, trading, or paper-ledger state mutation appears",
    "Direct-cross execution intent is accepted",
    "Legacy :06/:21/:36/:51 timestamp is accepted as future canonical",
    "USDJPY caveat is weakened",
    "Preview output is consumed by a forbidden consumer",
    "Operator approval is treated as executable approval"
)) {
    if (@($stopRules.HardStopRules) -notcontains $expected) { Fail "Hard stop rule missing: $expected" }
}
if ($stopRules.OperatorApprovalExecutableApproval -ne $false) { Fail "Stop rules allow operator approval as executable approval" }

$checklist = Read-Json "phase-exec-live-r008-operator-checklist.json"
if ($checklist.MustPassAll -ne $true -or $checklist.ExecutableApproval -ne $false) { Fail "Operator checklist weakens executable approval boundary" }
if (@($checklist.Checklist).Count -lt 12) { Fail "Operator checklist incomplete" }

$safe = Read-Json "phase-exec-live-r008-safe-use-examples.json"
foreach ($mode in @("ListAuditRecords", "ShowAuditRecord", "SummarizeBatch", "ExportOperatorReport")) {
    if (-not (@($safe.Examples) | Where-Object { $_.CommandMode -eq $mode -and $_.Safe -eq $true })) { Fail "Safe example missing $mode" }
}

$unsafe = Read-Json "phase-exec-live-r008-unsafe-use-rejection-examples.json"
foreach ($scenario in @("Submit preview as order", "Convert preview to route", "Start scheduler", "Commit ledger", "Activate broker", "Use legacy :06 as canonical")) {
    $match = @($unsafe.Examples | Where-Object { $_.Scenario -eq $scenario -and $_.Rejected -eq $true })
    if ($match.Count -eq 0) { Fail "Unsafe rejection example missing $scenario" }
}

$flags = Read-Json "phase-exec-live-r008-feature-flag-review.json"
if ($flags.LiveTradingEnabled -ne $false -or
    $flags.BrokerRoutingEnabled -ne $false -or
    $flags.OrderSubmissionEnabled -ne $false -or
    $flags.ExecutableScheduleEnabled -ne $false -or
    $flags.PaperLedgerCommitEnabled -ne $false -or
    $flags.SchedulerEnabled -ne $false -or
    $flags.BackgroundWorkerEnabled -ne $false -or
    $flags.DryRunOnly -ne $true) { Fail "Feature flags weakened" }

$consumer = Read-Json "phase-exec-live-r008-consumer-access-review.json"
foreach ($forbiddenConsumer in @("BrokerGateway", "OrderRouter", "Scheduler", "PaperLedgerCommitter", "ExecutionReportHandler", "ProductionTradingRuntime")) {
    if (@($consumer.ForbiddenConsumers) -notcontains $forbiddenConsumer) { Fail "Forbidden consumer missing $forbiddenConsumer" }
    if (@($consumer.AllowedConsumers) -contains $forbiddenConsumer) { Fail "Forbidden consumer allowed $forbiddenConsumer" }
}
if ($consumer.OperatorReviewToolOnlyForReview -ne $true -or $consumer.ExecutableApproval -ne $false) { Fail "Consumer access permits executable approval" }

$retention = Read-Json "phase-exec-live-r008-audit-artifact-retention-plan.json"
if ($retention.DeleteOnRollback -ne $false -or $retention.TradingStatePersistence -ne $false -or $retention.LedgerPersistence -ne $false) { Fail "Audit retention plan weakens safety" }

$r009 = Read-Json "phase-exec-live-r008-r009-contract-reference.json"
if ($r009.NonExecutable -ne $true -or $r009.NotAnOrder -ne $true -or $r009.NoBrokerRoute -ne $true) { Fail "R009 contract weakens non-executable status" }
if ($r009.BrokerReady -ne $false -or $r009.LiveReady -ne $false -or $r009.ExecutablePromotionAuthorized -ne $false) { Fail "R009 promoted or live/broker ready" }

$legacy = Read-Json "phase-exec-live-r008-legacy-compatibility-preservation.json"
if ($legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 used as future canonical" }
$direct = Read-Json "phase-exec-live-r008-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false -or $direct.DirectCrossRejectedNotOrder -ne $true) { Fail "Direct-cross exclusion weakened" }
$cost = Read-Json "phase-exec-live-r008-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$usdPair = Read-Json "phase-exec-live-r008-usd-pair-netting-requirement.json"
if ($usdPair.AudusdStatus -ne "SupportedAndNotFailed") { Fail "AUDUSD misclassified" }
$usdjpy = Read-Json "phase-exec-live-r008-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8") {
    Fail "USDJPY caveat weakened"
}

$forbidden = Read-Json "phase-exec-live-r008-forbidden-actions-audit.json"
foreach ($property in @(
    "ExternalApiCallsMade",
    "PolygonCallsMade",
    "LmaxCallsMade",
    "BrokerActivationOccurred",
    "LiveMarketDataRequested",
    "SchedulerServicePollingBackgroundJobIntroduced",
    "PmsEmsOmsCycleRun",
    "ManualNoExternalCommandRun",
    "BacktestSimulationRun",
    "TcaResultLinesCreated",
    "ExecutableScheduleCreated",
    "OrdersChildOrdersRoutesSubmissionsFillsReportsCreated",
    "PaperLedgerCommitCreated",
    "StateMutationOccurred",
    "R009PromotedToExecutableUse",
    "RunbookImpliesExecutableApproval",
    "RunbookAllowsBrokerOrderRouteFillScheduleLedger"
)) {
    if ($forbidden.$property -ne $false) { Fail "Forbidden action observed or allowed: $property" }
}

$evidence = Read-Json "phase-exec-live-r008-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedR008Checks -ne "Passed") { Fail "Focused R008 checks evidence missing or not passed" }
if ($evidence.UnitTests -ne "Passed") { Fail "Unit test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-LIVE-R008 validator passed."
