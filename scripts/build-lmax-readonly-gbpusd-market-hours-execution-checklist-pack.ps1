param(
    [string]$FinalReadinessFile = "artifacts/lmax-readonly-runtime-securityid-planning/final-readiness/lmax-readonly-gbpusd-manual-snapshot-final-readiness-20260509-165343.json",
    [string]$RetryReadinessFile = "artifacts/lmax-readonly-runtime-securityid-planning/market-hours-retry/lmax-readonly-gbpusd-market-hours-retry-20260509-174442.json",
    [string]$Phase7CGateFile = "artifacts/readiness/phase7c-gbpusd-closure-gate.json",
    [string]$Phase7DDecisionFile = "artifacts/lmax-readonly-runtime-securityid-planning/next-instrument-decisions/lmax-readonly-post-gbpusd-next-instrument-decision-20260510-130655.json",
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-securityid-planning/execution-checklists",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix|sendercompid|targetcompid)'

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Read-OptionalJson([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if ([string]::IsNullOrWhiteSpace($resolved) -or -not (Test-Path -LiteralPath $resolved)) {
        return @{ Path = $resolved; Json = $null; Decision = "NotAvailable" }
    }
    $raw = Get-Content -LiteralPath $resolved -Raw
    if ($raw -match $script:sensitivePattern) { throw "$Label contains credential-shaped or raw FIX content." }
    return @{ Path = $resolved; Json = ($raw | ConvertFrom-Json); Decision = "Available" }
}

Write-Host "LMAX Read-Only Phase 7E GBPUSD Market-Hours Execution Checklist Pack"
Write-Host "Local-only. This script does not connect to LMAX, request snapshots, request SecurityList, replay evidence, schedule work, or use credentials."

$finalReadiness = Read-OptionalJson $FinalReadinessFile "Final readiness"
$retryReadiness = Read-OptionalJson $RetryReadinessFile "Retry readiness"
$phase7cGate = Read-OptionalJson $Phase7CGateFile "Phase 7C gate"
$phase7dDecision = Read-OptionalJson $Phase7DDecisionFile "Phase 7D decision"

$issues = @()
if ($null -ne $finalReadiness.Json -and [string]$finalReadiness.Json.readinessDecision -ne "PASS") { $issues += "Final readiness is not PASS." }
if ($null -ne $retryReadiness.Json -and [string]$retryReadiness.Json.decision -ne "PASS") { $issues += "Retry readiness is not PASS." }
if ($null -ne $phase7cGate.Json -and $phase7cGate.Json.finalDecision -notin @("PASS", "PASS_WITH_KNOWN_WARNINGS")) { $issues += "Phase 7C gate is not safe." }
if ($null -ne $phase7dDecision.Json -and [string]$phase7dDecision.Json.decision -ne "PendingGbpusdMarketHoursAttempt") { $issues += "Phase 7D decision is not pending GBPUSD market-hours attempt." }

$manualCommand = 'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-lmax-readonly-runtime-demo-gbpusd-snapshot-once.ps1 -FinalReadinessFile "artifacts\lmax-readonly-runtime-securityid-planning\final-readiness\lmax-readonly-gbpusd-manual-snapshot-final-readiness-20260509-165343.json" -AllowExternalConnections -ConfirmDemoReadOnly -Reason "Phase 6Z-B operator-approved GBPUSD market-hours read-only snapshot attempt"'
$stamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$finalDecision = if ($issues.Count -eq 0) { "PASS" } else { "PASS_WITH_WARNINGS" }

$pack = [ordered]@{
    checklistId = "lmax-readonly-gbpusd-market-hours-execution-checklist-$stamp"
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    selectedInstrument = "GBPUSD"
    symbol = "GBPUSD"
    slashSymbol = "GBP/USD"
    securityId = "4002"
    securityIdSource = "8"
    manualCommandWarning = "DO NOT RUN UNTIL MARKET HOURS."
    requiredManualCommand = $manualCommand
    sourceFinalReadinessFile = $finalReadiness.Path
    sourceRetryReadinessFile = $retryReadiness.Path
    sourcePhase7CGateFile = $phase7cGate.Path
    sourcePhase7DDecisionFile = $phase7dDecision.Path
    preRunChecks = @(
        "Confirm FX market hours.",
        "Confirm Demo-only intent.",
        "Confirm credentials presence only; do not print credential values.",
        "Confirm API/Worker FakeLmaxGateway only.",
        "Confirm no scheduler/polling.",
        "Confirm runtime still does not submit to shadow replay.",
        "Confirm no order path.",
        "Confirm final readiness PASS.",
        "Confirm Phase 6Y retry readiness PASS.",
        "Confirm Phase 7C closure scripts exist.",
        "Confirm Phase 7D decision is PendingGbpusdMarketHoursAttempt."
    )
    duringRunMonitoring = @(
        "One attempt only.",
        "No retry.",
        "No batch or additional instruments.",
        "Ctrl+C or close process as kill switch."
    )
    postRunSequence = @(
        "Review artifact with Phase 7C review script.",
        "Map evidence preview if safe.",
        "Optionally replay local only if appropriate and explicitly confirmed.",
        "Build closure manifest.",
        "Run Phase 7C gate.",
        "Run Phase 7D next-instrument decision."
    )
    abortCriteria = @(
        "Wrong instrument.",
        "Wrong SecurityID.",
        "Non-Demo environment.",
        "Scheduler/polling detected.",
        "Runtime shadow replay submit detected.",
        "Order path detected.",
        "Credential exposure.",
        "Unknown failure classification."
    )
    rollbackSteps = @(
        "Stop process.",
        "Clear shell-only variables if needed.",
        "Verify /health FakeLmaxGateway.",
        "Inspect artifact for noSensitiveContent=true.",
        "No DB rollback expected because mutation prohibited."
    )
    explicitNonAuthorizations = @(
        "No scheduler.",
        "No polling.",
        "No runtime shadow replay submit.",
        "No orders.",
        "No gateway registration.",
        "No production/UAT.",
        "No multi-instrument batch.",
        "No trading-state mutation.",
        "No automatic execution."
    )
    resultInterpretation = [ordered]@{
        CompletedWithBook = "Proceed to evidence/replay/closure; Phase 7D may allow EURGBP planning."
        CompletedWithEmptyBook = "Retry/diagnostics decision; do not proceed to EURGBP."
        FailedSafe = "Diagnostics; no retry without a new phase."
        UnsafeFail = "Stop."
    }
    canRunAutomatically = $false
    schedulerOrPolling = $false
    runtimeShadowReplaySubmit = $false
    orderSubmission = $false
    gatewayRegistration = $false
    tradingMutation = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    noSensitiveContent = $true
    issues = $issues
    finalDecision = $finalDecision
}

$json = $pack | ConvertTo-Json -Depth 12
if ($json -match $sensitivePattern) { throw "Generated checklist pack contains credential-shaped or raw FIX content." }

$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$jsonPath = Join-Path $outDir "lmax-readonly-gbpusd-market-hours-execution-checklist-$stamp.json"
$mdPath = Join-Path $outDir "lmax-readonly-gbpusd-market-hours-execution-checklist-$stamp.md"
if (((Test-Path -LiteralPath $jsonPath) -or (Test-Path -LiteralPath $mdPath)) -and -not $Force.IsPresent) { throw "Output already exists for stamp $stamp." }

$json | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$md = @"
# LMAX Read-Only GBPUSD Market-Hours Execution Checklist Pack

ChecklistId: $($pack.checklistId)

FinalDecision: $finalDecision

Selected instrument: GBPUSD / GBP/USD / 4002 / SecurityIDSource 8

Manual command warning: DO NOT RUN UNTIL MARKET HOURS.

Future manual command:

````powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-lmax-readonly-runtime-demo-gbpusd-snapshot-once.ps1 ``
  -FinalReadinessFile "artifacts\lmax-readonly-runtime-securityid-planning\final-readiness\lmax-readonly-gbpusd-manual-snapshot-final-readiness-20260509-165343.json" ``
  -AllowExternalConnections ``
  -ConfirmDemoReadOnly ``
  -Reason "Phase 6Z-B operator-approved GBPUSD market-hours read-only snapshot attempt"
````

This pack does not execute the command.

Post-run sequence:

1. Review artifact with Phase 7C review script.
2. Map evidence preview if safe.
3. Optionally replay local only if appropriate and explicitly confirmed.
4. Build closure manifest.
5. Run Phase 7C gate.
6. Run Phase 7D next-instrument decision.

Safety:

- canRunAutomatically=false
- schedulerOrPolling=false
- runtimeShadowReplaySubmit=false
- orderSubmission=false
- gatewayRegistration=false
- tradingMutation=false
- API/Worker FakeLmaxGateway only
"@
$md | Set-Content -LiteralPath $mdPath -Encoding UTF8

Write-Host "ChecklistPackJson: $jsonPath"
Write-Host "ChecklistPackMarkdown: $mdPath"
Write-Host "FinalDecision: $finalDecision"
Write-Host "SelectedInstrument: GBPUSD / 4002"
Write-Host "CanRunAutomatically: false"
Write-Host "SchedulerOrPolling: false"
Write-Host "RuntimeShadowReplaySubmit: false"
Write-Host "OrderSubmission: false"
Write-Host "GatewayRegistration: false"
Write-Host "TradingMutation: false"
