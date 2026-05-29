param()

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix|sendercompid|targetcompid)'

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Get-TextHit([string[]]$Path, [string[]]$Pattern) {
    $existing = @($Path | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Pattern -SimpleMatch -ErrorAction SilentlyContinue)
}

function Test-Doc([string]$Path, [string]$Label, [string[]]$Markers) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Add-Result "Docs" "$Label exists" "FAIL" "Missing $Path"
        return
    }

    Add-Result "Docs" "$Label exists" "PASS" $Path
    $text = Get-Content -Raw -LiteralPath $Path
    if ($text -match $script:sensitivePattern) {
        Add-Result "Docs" "$Label sanitized" "FAIL" "Sensitive-shaped content found."
    } else {
        Add-Result "Docs" "$Label sanitized" "PASS" "No credential-shaped or raw FIX content."
    }

    foreach ($marker in $Markers) {
        if ($text.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            Add-Result "Docs" "$Label marker: $marker" "PASS" "Marker found."
        } else {
            Add-Result "Docs" "$Label marker: $marker" "FAIL" "Marker missing."
        }
    }
}

Write-Host "LMAX Read-Only Runtime Phase 7A Next Boundary Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, request SecurityList, replay evidence, schedule work, or use credentials."

$adr = Join-Path $repoRoot "docs/LMAX_READONLY_RUNTIME_PHASE7_NEXT_BOUNDARY_ADR.md"
$checklist = Join-Path $repoRoot "docs/LMAX_READONLY_RUNTIME_PHASE7_BOUNDARY_CHECKLIST.md"

Test-Doc $adr "Phase 7A ADR" @(
    "Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run",
    "scheduler",
    "runtime shadow replay submit",
    "order path",
    "production/UAT",
    "multi-instrument batch execution",
    "FakeLmaxGateway"
)

Test-Doc $checklist "Phase 7 boundary checklist" @(
    "Phase 5Y",
    "Phase 5W",
    "Phase 5X",
    "Phase 6Z-D",
    "Phase 6Z-E",
    "executableCount=0",
    "IsApprovedForExternalRun=false",
    "canRunExternalSnapshot=false",
    "FakeLmaxGateway"
)

$apiWorkerFiles = @(
    (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"),
    (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs")
)
$apiWorkerText = ($apiWorkerFiles | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"

if ($apiWorkerText.Contains("FakeLmaxGateway") -and -not ($apiWorkerText.Contains("RealLmaxGateway") -or $apiWorkerText.Contains("LmaxVenueGatewaySkeleton"))) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker."
}

if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("PeriodicTimer", "LmaxScheduler", "SecurityListPolling", "MarketDataPolling", "SnapshotPolling")).Count -eq 0) {
    Add-Result "Scheduler" "No scheduler/polling added" "PASS" "No LMAX scheduler/polling marker found in API/Worker startup."
} else {
    Add-Result "Scheduler" "No scheduler/polling added" "FAIL" "LMAX scheduler/polling marker found."
}

if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("SubmitToShadowReplay = true", "SubmittedToShadowReplay = true", "ReplaySubmitAsync")).Count -eq 0) {
    Add-Result "Replay" "Runtime still does not submit to shadow replay" "PASS" "No runtime replay submit marker found."
} else {
    Add-Result "Replay" "Runtime still does not submit to shadow replay" "FAIL" "Runtime replay submit marker found."
}

if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "OrderStatusRequest", "TradeCaptureReportRequest", "SubmitOrder")).Count -eq 0) {
    Add-Result "Orders" "No order surface" "PASS" "No order marker found in API/Worker startup."
} else {
    Add-Result "Orders" "No order surface" "FAIL" "Order marker found."
}

if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("PersistTrade", "TradingState", "IOrderRepository", "IFillRepository", "IPositionRepository", "PersistLiveFix")).Count -eq 0) {
    Add-Result "Mutation" "No trading-state mutation references" "PASS" "No mutation marker found in API/Worker startup."
} else {
    Add-Result "Mutation" "No trading-state mutation references" "FAIL" "Mutation marker found."
}

$newRuntimeCapabilityFiles = @(Get-ChildItem -Path (Join-Path $repoRoot "src") -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Name -match 'Phase7' -and
        $_.FullName -notmatch '\\bin\\|\\obj\\|\\dist\\'
    })
if ($newRuntimeCapabilityFiles.Count -eq 0) {
    Add-Result "RuntimeCapability" "No new Phase 7 runtime capability files" "PASS" "No Phase 7 runtime/source capability files found."
} else {
    Add-Result "RuntimeCapability" "No new Phase 7 runtime capability files" "FAIL" (($newRuntimeCapabilityFiles | Select-Object -First 10 -ExpandProperty FullName) -join "; ")
}

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "SecurityList" "SecurityListRequest" "PASS" "This gate does not request SecurityList."
Add-Result "Replay" "Manual replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$final = if ($results.status -contains "FAIL") { "FAIL" } elseif ($results.status -contains "WARN") { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }
$report = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    phase = "7A"
    finalDecision = $final
    recommendedNextPhase = "Phase 7B - Controlled Manual Multi-Instrument Read-Only Snapshot Workflow Plan, No External Run"
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    securityListRequestAttempted = $false
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
$outFile = Join-Path $outDir "phase7a-next-boundary-gate.json"
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $outFile -Encoding UTF8
Write-Host ""
Write-Host "FinalDecision: $final"
Write-Host "Report: $outFile"
if ($final -eq "FAIL") { exit 1 }
