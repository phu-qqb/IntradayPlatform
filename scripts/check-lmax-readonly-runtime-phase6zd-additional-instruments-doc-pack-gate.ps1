param(
    [Parameter(Mandatory = $true)]
    [string]$PipelineManifestFile,

    [Parameter(Mandatory = $true)]
    [string]$PlanningStatusReportFile,

    [string]$DocPackFile = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$expected = [ordered]@{ GBPUSD = "4002"; EURGBP = "4003"; USDJPY = "4004"; AUDUSD = "4007" }
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix|sendercompid|targetcompid)'

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Resolve-LocalPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $Path }
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function Get-TextHit([string[]]$Path, [string[]]$Pattern) {
    $existing = @($Path | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Pattern -SimpleMatch -ErrorAction SilentlyContinue)
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

Write-Host "LMAX Read-Only Runtime Phase 6Z-D Additional Instruments Documentation Pack Gate"
Write-Host "Local-only. This gate does not connect to LMAX, call external APIs, request SecurityList, run snapshots, replay evidence, schedule work, or use credentials."

$finalDoc = Join-Path $repoRoot "docs/LMAX_READONLY_ADDITIONAL_INSTRUMENTS_PLANNING_FINAL_DOC.md"
$builder = Join-Path $PSScriptRoot "build-lmax-readonly-additional-instruments-planning-doc-pack.ps1"
foreach ($item in @(
    @{ Name = "Final additional instruments planning doc"; Path = $finalDoc },
    @{ Name = "Documentation pack builder"; Path = $builder }
)) {
    if (Test-Path -LiteralPath $item.Path) {
        Add-Result "Files" "$($item.Name) exists" "PASS" $item.Path
    } else {
        Add-Result "Files" "$($item.Name) exists" "FAIL" "Missing $($item.Path)"
    }
}

$pipeline = Read-JsonForGate $PipelineManifestFile "Pipeline"
if ($null -ne $pipeline) {
    if ([string]$pipeline.finalDecision -eq "PASS" -and [int]$pipeline.instrumentCount -eq 4 -and [int]$pipeline.readyForFutureManualConsiderationCount -eq 4 -and [int]$pipeline.executableCount -eq 0) {
        Add-Result "Pipeline" "Aggregate decision and counts" "PASS" "PASS; instrumentCount=4; executableCount=0."
    } else {
        Add-Result "Pipeline" "Aggregate decision and counts" "FAIL" "Expected PASS, instrumentCount=4, ready count=4, executableCount=0."
    }
    if (-not [bool]$pipeline.isApprovedForExternalRun -and -not [bool]$pipeline.canRunExternalSnapshot -and -not [bool]$pipeline.eligibleForManualSnapshotAttempt -and -not [bool]$pipeline.schedulerStarted -and -not [bool]$pipeline.orderSubmissionAttempted -and -not [bool]$pipeline.shadowReplaySubmitAttempted -and -not [bool]$pipeline.tradingMutationAttempted -and [string]$pipeline.apiWorkerGatewayMode -eq "FakeLmaxGateway") {
        Add-Result "Pipeline" "Aggregate non-executable flags" "PASS" "All aggregate run and mutation flags are safe."
    } else {
        Add-Result "Pipeline" "Aggregate non-executable flags" "FAIL" "Unsafe aggregate flag detected."
    }
    foreach ($symbol in $expected.Keys) {
        $instrument = @($pipeline.instruments | Where-Object { [string]$_.symbol -eq $symbol })[0]
        if ($null -eq $instrument) {
            Add-Result "Pipeline" "$symbol present" "FAIL" "Missing $symbol."
            continue
        }
        if ([string]$instrument.planningSecurityId -eq $expected[$symbol] -and [string]$instrument.securityIdSource -eq "8" -and [string]$instrument.finalReadinessDecision -eq "PASS") {
            Add-Result "Pipeline" "$symbol identity and readiness" "PASS" "$symbol / $($instrument.slashSymbol) / $($instrument.planningSecurityId)."
        } else {
            Add-Result "Pipeline" "$symbol identity and readiness" "FAIL" "Unexpected SecurityID/source/readiness."
        }
        if (-not [bool]$instrument.isApprovedForExternalRun -and -not [bool]$instrument.canRunExternalSnapshot -and -not [bool]$instrument.eligibleForManualSnapshotAttempt -and -not [bool]$instrument.externalConnectionAttempted -and -not [bool]$instrument.snapshotAttempted -and -not [bool]$instrument.replayAttempted -and -not [bool]$instrument.orderSubmissionAttempted -and -not [bool]$instrument.shadowReplaySubmitAttempted -and -not [bool]$instrument.tradingMutationAttempted -and -not [bool]$instrument.schedulerStarted -and [bool]$instrument.noSensitiveContent) {
            Add-Result "Pipeline" "$symbol non-executable flags" "PASS" "All run flags false."
        } else {
            Add-Result "Pipeline" "$symbol non-executable flags" "FAIL" "Unsafe instrument flag detected."
        }
    }
}

$status = Read-JsonForGate $PlanningStatusReportFile "PlanningStatus"
if ($null -ne $status) {
    if ([string]$status.finalDecision -eq "PASS" -and [string]$status.aggregateDecision -eq "PASS" -and [int]$status.instrumentCount -eq 4 -and [int]$status.executableCount -eq 0) {
        Add-Result "PlanningStatus" "Status summary decision and counts" "PASS" "PASS; instrumentCount=4; executableCount=0."
    } else {
        Add-Result "PlanningStatus" "Status summary decision and counts" "FAIL" "Unexpected status summary counts or decision."
    }
    if (-not [bool]$status.runtimeShadowReplaySubmit -and -not [bool]$status.schedulerOrPolling -and -not [bool]$status.orderSubmission -and -not [bool]$status.gatewayRegistration -and -not [bool]$status.tradingMutation -and [string]$status.apiWorkerGatewayMode -eq "FakeLmaxGateway") {
        Add-Result "PlanningStatus" "Read-only safety flags" "PASS" "All status safety flags are false."
    } else {
        Add-Result "PlanningStatus" "Read-only safety flags" "FAIL" "Unsafe status flag detected."
    }
}

if ([string]::IsNullOrWhiteSpace($DocPackFile)) {
    Add-Result "DocPack" "Doc pack supplied" "WARN" "No doc pack supplied; source/doc checks only."
} else {
    $docPack = Read-JsonForGate $DocPackFile "DocPack"
    if ($null -ne $docPack) {
        if ([string]$docPack.finalDecision -eq "PASS" -and [int]$docPack.instrumentCount -eq 4 -and [int]$docPack.executableCount -eq 0 -and [string]$docPack.apiWorkerGatewayMode -eq "FakeLmaxGateway") {
            Add-Result "DocPack" "Final decision and counts" "PASS" "PASS; instrumentCount=4; executableCount=0."
        } else {
            Add-Result "DocPack" "Final decision and counts" "FAIL" "Expected PASS, instrumentCount=4, executableCount=0, FakeLmaxGateway."
        }
        if (-not [bool]$docPack.isApprovedForExternalRun -and -not [bool]$docPack.canRunExternalSnapshot -and -not [bool]$docPack.eligibleForManualSnapshotAttempt -and -not [bool]$docPack.runtimeShadowReplaySubmit -and -not [bool]$docPack.schedulerOrPolling -and -not [bool]$docPack.orderSubmission -and -not [bool]$docPack.gatewayRegistration -and -not [bool]$docPack.tradingMutation -and [bool]$docPack.noSensitiveContent) {
            Add-Result "DocPack" "Non-executable safety flags" "PASS" "All doc pack safety flags are safe."
        } else {
            Add-Result "DocPack" "Non-executable safety flags" "FAIL" "Unsafe doc pack flag detected."
        }
        foreach ($instrument in @($docPack.instruments)) {
            if ([bool]$instrument.isApprovedForExternalRun -or [bool]$instrument.canRunExternalSnapshot -or [bool]$instrument.eligibleForManualSnapshotAttempt -or [bool]$instrument.executable) {
                Add-Result "DocPack" "$($instrument.symbol) non-executable flags" "FAIL" "Executable flag true."
            } else {
                Add-Result "DocPack" "$($instrument.symbol) non-executable flags" "PASS" "All run flags false."
            }
        }
    }
}

$apiWorkerFiles = @((Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"), (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"))
$apiText = (Get-Content -Raw -LiteralPath $apiWorkerFiles[0])
$apiWorkerText = ($apiWorkerFiles | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
if ($apiWorkerText.Contains("FakeLmaxGateway") -and -not ($apiWorkerText.Contains("RealLmaxGateway") -or $apiWorkerText.Contains("ExternalReadOnlyPrototypeGateway") -or $apiWorkerText.Contains("LmaxVenueGatewaySkeleton"))) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker."
}
if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("PeriodicTimer", "LmaxScheduler", "SecurityListPolling", "MarketDataPolling")).Count -eq 0) {
    Add-Result "Scheduler" "No scheduler/polling added" "PASS" "No LMAX scheduler/polling marker found in API/Worker startup."
} else {
    Add-Result "Scheduler" "No scheduler/polling added" "FAIL" "LMAX scheduler/polling marker found."
}
if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("SubmitToShadowReplay = true", "SubmittedToShadowReplay = true", "ReplaySubmitAsync")).Count -eq 0) {
    Add-Result "Replay" "Runtime still does not submit to shadow replay" "PASS" "No runtime replay submit marker found."
} else {
    Add-Result "Replay" "Runtime still does not submit to shadow replay" "FAIL" "Runtime replay submit marker found."
}
if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "OrderStatusRequest", "SubmitOrder")).Count -eq 0) {
    Add-Result "Orders" "No order surface" "PASS" "No order marker found in API/Worker startup."
} else {
    Add-Result "Orders" "No order surface" "FAIL" "Order marker found."
}
if ((Get-TextHit -Path $apiWorkerFiles -Pattern @("PersistTrade", "TradingState", "IOrderRepository", "IFillRepository", "IPositionRepository")).Count -eq 0) {
    Add-Result "Mutation" "No trading-state mutation references" "PASS" "No mutation marker found in API/Worker startup."
} else {
    Add-Result "Mutation" "No trading-state mutation references" "FAIL" "Mutation marker found."
}

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "Replay" "Manual replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$final = if ($results.status -contains "FAIL") { "FAIL" } elseif ($results.status -contains "WARN") { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }
$report = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
    phase = "6Z-D"
    finalDecision = $final
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
$outFile = Join-Path $outDir "phase6zd-additional-instruments-doc-pack-gate.json"
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $outFile -Encoding UTF8
Write-Host ""
Write-Host "FinalDecision: $final"
Write-Host "Report: $outFile"
if ($final -eq "FAIL") { exit 1 }
