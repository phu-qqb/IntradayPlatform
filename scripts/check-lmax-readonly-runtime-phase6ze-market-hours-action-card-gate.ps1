param(
    [string]$FinalReadinessFile = "artifacts/lmax-readonly-runtime-securityid-planning/final-readiness/lmax-readonly-gbpusd-manual-snapshot-final-readiness-20260509-165343.json",
    [string]$MarketHoursRetryReadinessFile = "artifacts/lmax-readonly-runtime-securityid-planning/market-hours-retry/lmax-readonly-gbpusd-market-hours-retry-20260509-174442.json",
    [string]$Phase6XReviewFile = "artifacts/readiness/phase6x-gbpusd-snapshot-result-review.json",
    [string]$DocumentationPackFile = "artifacts/lmax-readonly-runtime-securityid-planning/documentation-pack/lmax-readonly-additional-instruments-planning-doc-pack-20260510-132804.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix|sendercompid|targetcompid)'

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Resolve-LocalPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function Read-JsonForGate([string]$Path, [string]$Label) {
    $resolved = Resolve-LocalPath $Path
    if (-not (Test-Path -LiteralPath $resolved)) {
        Add-Result $Label "File exists" "FAIL" "Missing $resolved"
        return $null
    }
    Add-Result $Label "File exists" "PASS" $resolved
    $raw = Get-Content -Raw -LiteralPath $resolved
    if ($raw -match $script:sensitivePattern) {
        Add-Result $Label "No sensitive content" "FAIL" "Sensitive-shaped content found."
    } else {
        Add-Result $Label "No sensitive content" "PASS" "No credential-shaped or raw FIX content."
    }
    return ($raw | ConvertFrom-Json)
}

function Get-TextHit([string[]]$Path, [string[]]$Pattern) {
    $existing = @($Path | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Pattern -SimpleMatch -ErrorAction SilentlyContinue)
}

Write-Host "LMAX Read-Only Runtime Phase 6Z-E Market-Hours Action Card Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, request SecurityList, replay evidence, schedule work, or use credentials."

$model = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyMarketHoursNextActionSummary.cs"
$script = Join-Path $PSScriptRoot "show-lmax-readonly-market-hours-next-action.ps1"
$test = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyMarketHoursNextActionSummaryTests.cs"
$api = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$ui = Join-Path $repoRoot "src/QQ.Production.Intraday.Ui/src/App.tsx"
$apiClient = Join-Path $repoRoot "src/QQ.Production.Intraday.Ui/src/api/apiClient.ts"
$uiTypes = Join-Path $repoRoot "src/QQ.Production.Intraday.Ui/src/api/types.ts"

foreach ($item in @(
    @{ n = "Market-hours next action model"; p = $model },
    @{ n = "Market-hours next action script"; p = $script },
    @{ n = "Market-hours next action tests"; p = $test }
)) {
    if (Test-Path -LiteralPath $item.p) { Add-Result "Files" "$($item.n) exists" "PASS" $item.p } else { Add-Result "Files" "$($item.n) exists" "FAIL" "Missing $($item.p)" }
}

$apiText = Get-Content -Raw -LiteralPath $api
if ($apiText.Contains('/lmax-readonly-runtime/market-hours-next-action')) {
    Add-Result "API" "Read-only market-hours next-action endpoint exists" "PASS" "GET endpoint present."
} else {
    Add-Result "API" "Read-only market-hours next-action endpoint exists" "FAIL" "Endpoint missing."
}
if ($apiText -match 'MapPost\("/lmax-readonly-runtime/market-hours-next-action' -or $apiText -match 'AllowExternalConnections.*market-hours-next-action') {
    Add-Result "API" "No live controls on next-action endpoint" "FAIL" "Endpoint must be GET/read-only only."
} else {
    Add-Result "API" "No live controls on next-action endpoint" "PASS" "No POST/live controls found for next-action endpoint."
}

$uiText = Get-Content -Raw -LiteralPath $ui
foreach ($marker in @("LMAX Market-Hours Next Action", "Wait for market hours", "What this does not authorize", "canRunExternalSnapshot=false", "IsApprovedForExternalRun=false")) {
    if ($uiText.Contains($marker)) { Add-Result "UI" "Panel marker $marker" "PASS" "Marker found." } else { Add-Result "UI" "Panel marker $marker" "FAIL" "Marker missing." }
}
if ((Get-Content -Raw -LiteralPath $apiClient).Contains("getLmaxReadOnlyMarketHoursNextAction") -and (Get-Content -Raw -LiteralPath $uiTypes).Contains("LmaxReadOnlyMarketHoursNextActionSummaryDto")) {
    Add-Result "UI" "API client/types added" "PASS" "Read-only DTO and client function present."
} else {
    Add-Result "UI" "API client/types added" "FAIL" "Missing DTO/client binding."
}
if ($uiText -match 'onClick=\{.*(Snapshot|Replay|Scheduler|Order|Connect)' -or $uiText -match 'market-hours-next-action.*button') {
    Add-Result "UI" "No run/replay/scheduler/order controls" "FAIL" "Potential live control marker found."
} else {
    Add-Result "UI" "No run/replay/scheduler/order controls" "PASS" "No next-action live controls found."
}

$finalReadiness = Read-JsonForGate $FinalReadinessFile "FinalReadiness"
$retry = Read-JsonForGate $MarketHoursRetryReadinessFile "MarketHoursRetry"
$review = Read-JsonForGate $Phase6XReviewFile "Phase6XReview"
$docPack = Read-JsonForGate $DocumentationPackFile "DocumentationPack"

if ($null -ne $finalReadiness) {
    if ([string]$finalReadiness.symbol -eq "GBPUSD" -and [string]$finalReadiness.planningSecurityId -eq "4002" -and [string]$finalReadiness.securityIdSource -eq "8" -and [string]$finalReadiness.readinessDecision -eq "PASS") { Add-Result "FinalReadiness" "GBPUSD readiness identity" "PASS" "GBPUSD / 4002 / PASS." } else { Add-Result "FinalReadiness" "GBPUSD readiness identity" "FAIL" "Unexpected final readiness identity." }
    if (-not [bool]$finalReadiness.isApprovedForExternalRun -and -not [bool]$finalReadiness.canRunExternalSnapshot -and -not [bool]$finalReadiness.eligibleForManualSnapshotAttempt -and -not [bool]$finalReadiness.runtimeShadowReplaySubmit -and -not [bool]$finalReadiness.orderSubmissionAttempted -and -not [bool]$finalReadiness.shadowReplaySubmitAttempted -and -not [bool]$finalReadiness.tradingMutationAttempted -and -not [bool]$finalReadiness.schedulerStarted) { Add-Result "FinalReadiness" "Non-executable flags" "PASS" "All final readiness run flags false." } else { Add-Result "FinalReadiness" "Non-executable flags" "FAIL" "Unsafe final readiness flag detected." }
}
if ($null -ne $retry) {
    if ([string]$retry.symbol -eq "GBPUSD" -and [string]$retry.securityId -eq "4002" -and [string]$retry.decision -eq "PASS" -and [bool]$retry.previousAttemptWasOutsideMarketHours -and [string]$retry.previousResultStatus -eq "CompletedWithEmptyBook") { Add-Result "MarketHoursRetry" "Retry readiness state" "PASS" "GBPUSD retry prepared after safe outside-market-hours empty book." } else { Add-Result "MarketHoursRetry" "Retry readiness state" "FAIL" "Unexpected retry readiness state." }
    if (-not [bool]$retry.canRunAutomatically -and -not [bool]$retry.schedulerStarted -and -not [bool]$retry.orderSubmissionAttempted -and -not [bool]$retry.shadowReplaySubmitAttempted -and -not [bool]$retry.tradingMutationAttempted) { Add-Result "MarketHoursRetry" "Manual-only safety flags" "PASS" "No automatic/scheduler/order/shadow/mutation flags." } else { Add-Result "MarketHoursRetry" "Manual-only safety flags" "FAIL" "Unsafe retry flag detected." }
}
if ($null -ne $review) {
    if ([string]$review.status -eq "CompletedWithEmptyBook" -and [string]$review.finalDecision -eq "PASS_WITH_KNOWN_WARNINGS" -and [bool]$review.snapshotReceived -and [int]$review.entryCount -eq 0) { Add-Result "Phase6XReview" "Previous empty-book review" "PASS" "CompletedWithEmptyBook safe warning." } else { Add-Result "Phase6XReview" "Previous empty-book review" "FAIL" "Unexpected Phase 6X review state." }
    if (-not [bool]$review.orderSubmissionAttempted -and -not [bool]$review.shadowReplaySubmitAttempted -and -not [bool]$review.tradingMutationAttempted -and -not [bool]$review.schedulerStarted -and -not [bool]$review.credentialValuesReturned) { Add-Result "Phase6XReview" "Safe result flags" "PASS" "No order/shadow/mutation/scheduler/credential leakage." } else { Add-Result "Phase6XReview" "Safe result flags" "FAIL" "Unsafe Phase 6X result flag detected." }
}
if ($null -ne $docPack) {
    if ([string]$docPack.finalDecision -eq "PASS" -and [int]$docPack.executableCount -eq 0 -and [int]$docPack.instrumentCount -eq 4) { Add-Result "DocumentationPack" "Planning freeze" "PASS" "PASS; instrumentCount=4; executableCount=0." } else { Add-Result "DocumentationPack" "Planning freeze" "FAIL" "Unexpected documentation pack state." }
    if (-not [bool]$docPack.isApprovedForExternalRun -and -not [bool]$docPack.canRunExternalSnapshot -and -not [bool]$docPack.eligibleForManualSnapshotAttempt -and -not [bool]$docPack.runtimeShadowReplaySubmit -and -not [bool]$docPack.schedulerOrPolling -and -not [bool]$docPack.orderSubmission -and -not [bool]$docPack.gatewayRegistration -and -not [bool]$docPack.tradingMutation) { Add-Result "DocumentationPack" "Non-executable freeze flags" "PASS" "All doc-pack run flags false." } else { Add-Result "DocumentationPack" "Non-executable freeze flags" "FAIL" "Unsafe doc-pack flag detected." }
}

$apiWorkerFiles = @((Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"), (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"))
$apiWorkerText = ($apiWorkerFiles | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
if ($apiWorkerText.Contains("FakeLmaxGateway") -and -not ($apiWorkerText.Contains("RealLmaxGateway") -or $apiWorkerText.Contains("LmaxVenueGatewaySkeleton"))) { Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found." } else { Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker." }
if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("PeriodicTimer", "LmaxScheduler", "SecurityListPolling", "MarketDataPolling")).Count -eq 0) { Add-Result "Scheduler" "No scheduler/polling added" "PASS" "No LMAX scheduler/polling marker found in API/Worker startup." } else { Add-Result "Scheduler" "No scheduler/polling added" "FAIL" "LMAX scheduler/polling marker found." }
if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("SubmitToShadowReplay = true", "SubmittedToShadowReplay = true", "ReplaySubmitAsync")).Count -eq 0) { Add-Result "Replay" "Runtime still does not submit to shadow replay" "PASS" "No runtime replay submit marker found." } else { Add-Result "Replay" "Runtime still does not submit to shadow replay" "FAIL" "Runtime replay submit marker found." }
if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "OrderStatusRequest", "SubmitOrder")).Count -eq 0) { Add-Result "Orders" "No order surface" "PASS" "No order marker found in API/Worker startup." } else { Add-Result "Orders" "No order surface" "FAIL" "Order marker found." }
if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("PersistTrade", "TradingState", "IOrderRepository", "IFillRepository", "IPositionRepository")).Count -eq 0) { Add-Result "Mutation" "No trading-state mutation references" "PASS" "No mutation marker found in API/Worker startup." } else { Add-Result "Mutation" "No trading-state mutation references" "FAIL" "Mutation marker found." }

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "Replay" "Manual replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$final = if ($results.status -contains "FAIL") { "FAIL" } elseif ($results.status -contains "WARN") { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }
$report = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    phase = "6Z-E"
    finalDecision = $final
    selectedInstrument = "GBPUSD"
    securityId = "4002"
    executableCount = 0
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    schedulerStarted = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
}
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outFile = Join-Path $outDir "phase6ze-market-hours-action-card-gate.json"
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $outFile -Encoding UTF8
Write-Host ""
Write-Host "FinalDecision: $final"
Write-Host "Report: $outFile"
if ($final -eq "FAIL") { exit 1 }
