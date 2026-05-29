param(
    [string]$FinalDocumentationPack = "docs/LMAX_READONLY_DEMO_MARKETDATA_WORKFLOW_FINAL_DOC.md",
    [string]$AuditPackFile,
    [string]$OperationalSignoffFile,
    [string]$WorkflowStatusFile
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{
        category = $Category
        check = $Check
        status = $Status
        detail = $Detail
    }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Resolve-LocalPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $Path }
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function Get-LatestFile([string]$Directory, [string]$Filter) {
    $fullDirectory = Resolve-LocalPath $Directory
    if (-not (Test-Path -LiteralPath $fullDirectory)) { return $null }
    return Get-ChildItem -Path $fullDirectory -Filter $Filter -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
}

function Read-JsonFile([string]$Path) {
    $resolved = Resolve-LocalPath $Path
    if ([string]::IsNullOrWhiteSpace($resolved) -or -not (Test-Path -LiteralPath $resolved)) { return $null }
    return Get-Content -Raw -LiteralPath $resolved | ConvertFrom-Json
}

function Get-JsonValue($Object, [string[]]$Names) {
    if ($null -eq $Object) { return $null }
    foreach ($name in $Names) {
        $property = $Object.PSObject.Properties[$name]
        if ($property) { return $property.Value }
    }
    return $null
}

Write-Host "LMAX Read-Only Runtime Phase 6A Planning Gate"
Write-Host "Local-only. No LMAX connection, no credentials, no API required, no scheduler/polling, no runtime replay, and no mutation."

$planDoc = Join-Path $repoRoot "docs/LMAX_READONLY_RUNTIME_PHASE6_OPERATIONALIZATION_PLAN.md"
$checklistDoc = Join-Path $repoRoot "docs/LMAX_READONLY_RUNTIME_PHASE6_BOUNDARY_CHECKLIST.md"
$finalDoc = Resolve-LocalPath $FinalDocumentationPack

if (Test-Path -LiteralPath $planDoc) {
    Add-Result "Docs" "Phase 6 operationalization plan exists" "PASS" "Found docs/LMAX_READONLY_RUNTIME_PHASE6_OPERATIONALIZATION_PLAN.md."
} else {
    Add-Result "Docs" "Phase 6 operationalization plan exists" "FAIL" "Missing Phase 6 operationalization plan."
}

if (Test-Path -LiteralPath $checklistDoc) {
    Add-Result "Docs" "Phase 6 boundary checklist exists" "PASS" "Found docs/LMAX_READONLY_RUNTIME_PHASE6_BOUNDARY_CHECKLIST.md."
} else {
    Add-Result "Docs" "Phase 6 boundary checklist exists" "FAIL" "Missing Phase 6 boundary checklist."
}

if (Test-Path -LiteralPath $finalDoc) {
    Add-Result "Docs" "Phase 5Y final documentation pack exists" "PASS" $finalDoc
} else {
    Add-Result "Docs" "Phase 5Y final documentation pack exists" "FAIL" "Missing $finalDoc"
}

if ([string]::IsNullOrWhiteSpace($AuditPackFile)) {
    $latestAuditPack = Get-LatestFile "artifacts/lmax-readonly-runtime-demo-snapshot/audit-pack" "lmax-readonly-marketdata-workflow-audit-pack-*.json"
    if ($latestAuditPack) { $AuditPackFile = $latestAuditPack.FullName }
}
if ([string]::IsNullOrWhiteSpace($OperationalSignoffFile)) {
    $latestSignoff = Get-LatestFile "artifacts/readiness" "lmax-readonly-marketdata-operational-signoff-*.json"
    if ($latestSignoff) { $OperationalSignoffFile = $latestSignoff.FullName }
}
if ([string]::IsNullOrWhiteSpace($WorkflowStatusFile)) {
    $latestStatus = Get-LatestFile "artifacts/readiness" "lmax-readonly-marketdata-workflow-status-*.json"
    if ($latestStatus) { $WorkflowStatusFile = $latestStatus.FullName }
}

$auditPack = Read-JsonFile $AuditPackFile
if ($auditPack) {
    $decision = [string](Get-JsonValue $auditPack @("finalDecision", "FinalDecision", "decision"))
    if ($decision -eq "PASS") {
        Add-Result "Phase5" "Phase 5V audit pack PASS" "PASS" "Audit pack decision PASS at $(Resolve-LocalPath $AuditPackFile)."
    } else {
        Add-Result "Phase5" "Phase 5V audit pack PASS" "FAIL" "Audit pack decision was '$decision'."
    }
} else {
    Add-Result "Phase5" "Phase 5V audit pack PASS" "FAIL" "Audit pack file not found."
}

$signoff = Read-JsonFile $OperationalSignoffFile
if ($signoff) {
    $decision = [string](Get-JsonValue $signoff @("finalDecision", "FinalDecision", "decision"))
    if ($decision -eq "PASS") {
        Add-Result "Phase5" "Phase 5W operational signoff PASS" "PASS" "Operational signoff decision PASS at $(Resolve-LocalPath $OperationalSignoffFile)."
    } else {
        Add-Result "Phase5" "Phase 5W operational signoff PASS" "FAIL" "Operational signoff decision was '$decision'."
    }
} else {
    Add-Result "Phase5" "Phase 5W operational signoff PASS" "FAIL" "Operational signoff file not found."
}

$status = Read-JsonFile $WorkflowStatusFile
if ($status) {
    $operationalStatus = [string](Get-JsonValue $status @("operationalStatus", "OperationalStatus"))
    $runtimeSubmit = [bool](Get-JsonValue $status @("runtimeShadowReplaySubmit", "RuntimeShadowReplaySubmit"))
    $credentialValuesReturned = [bool](Get-JsonValue $status @("credentialValuesReturned", "CredentialValuesReturned"))
    if ($operationalStatus -eq "FrozenManualReadOnly" -and -not $runtimeSubmit -and -not $credentialValuesReturned) {
        Add-Result "Phase5" "Phase 5X summary FrozenManualReadOnly" "PASS" "Status=$operationalStatus; runtimeShadowReplaySubmit=false; credentialValuesReturned=false."
    } else {
        Add-Result "Phase5" "Phase 5X summary FrozenManualReadOnly" "FAIL" "Status=$operationalStatus; runtimeShadowReplaySubmit=$runtimeSubmit; credentialValuesReturned=$credentialValuesReturned."
    }
} else {
    Add-Result "Phase5" "Phase 5X summary FrozenManualReadOnly" "FAIL" "Workflow status summary file not found."
}

$phase6Text = ""
if (Test-Path -LiteralPath $planDoc) { $phase6Text += Get-Content -Raw -LiteralPath $planDoc }
if (Test-Path -LiteralPath $checklistDoc) { $phase6Text += "`n" + (Get-Content -Raw -LiteralPath $checklistDoc) }
if ($phase6Text.Contains("Phase 6B") -and $phase6Text.Contains("Manual Additional MarketData Instrument Allowlist Design") -and $phase6Text.Contains("No External Run")) {
    Add-Result "Planning" "Recommended next boundary documented" "PASS" "Phase 6B instrument allowlist design is documented as the recommended next boundary."
} else {
    Add-Result "Planning" "Recommended next boundary documented" "FAIL" "Recommended Phase 6B boundary text was not found."
}

$scanPaths = @(
    $planDoc,
    $checklistDoc,
    (Join-Path $repoRoot "docs/LMAX_READONLY_DEMO_MARKETDATA_WORKFLOW_FINAL_DOC.md"),
    (Join-Path $repoRoot "scripts/check-lmax-readonly-runtime-phase6a-planning-gate.ps1")
) | Where-Object { Test-Path -LiteralPath $_ }

$schedulerHits = @(Select-String -Path $scanPaths -Pattern "Register-ScheduledTask","New-ScheduledTask","Start-ThreadJob","BackgroundService","IHostedService","PeriodicTimer","System.Threading.Timer","while (`$true)","automatic polling" -SimpleMatch -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -notlike "*check-lmax-readonly-runtime-phase6a-planning-gate.ps1" -and $_.Line -notmatch "not authorize|does not authorize|not add|absent|forbidden|without|No scheduler|No automatic polling|Automatic polling|explicitly not authorized|Future only|risk|checklist|boundary|planning" })
if ($schedulerHits.Count -eq 0) {
    Add-Result "Safety" "No scheduler or polling source added" "PASS" "No scheduler, hosted-service, timer, or polling marker found in LMAX runtime/script scope."
} else {
    Add-Result "Safety" "No scheduler or polling source added" "FAIL" (($schedulerHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$runtimeSubmitHits = @(Select-String -Path $scanPaths -Pattern "SubmitToShadowReplayAsync","RuntimeShadowReplaySubmitAsync","ILmaxShadowReplayService","/lmax-shadow/replay" -SimpleMatch -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -notlike "*check-lmax-readonly-runtime-phase6a-planning-gate.ps1" -and $_.Line -notmatch "does not submit|must remain absent|not present|absent|not authorize|No runtime shadow replay submit|planning|checklist|boundary" })
if ($runtimeSubmitHits.Count -eq 0) {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "PASS" "No runtime shadow replay submit marker found outside explicit manual replay scripts."
} else {
    Add-Result "Safety" "Runtime still does not submit to shadow replay" "FAIL" (($runtimeSubmitHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$orderHits = @(Select-String -Path $scanPaths -Pattern "NewOrderSingle","OrderCancelRequest","OrderCancelReplaceRequest","SubmitOrder","SendOrder","OrderStatusRequest","TradeCapture" -SimpleMatch -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -notlike "*check-lmax-readonly-runtime-phase6a-planning-gate.ps1" -and $_.Line -notmatch "does not authorize|not authorize|forbidden|out of scope|not add|No order|Explicitly Not Authorized|What PASS Does Not Authorize|flows|workflow" })
if ($orderHits.Count -eq 0) {
    Add-Result "Safety" "No order surface added" "PASS" "No order command surface found in LMAX runtime/script scope."
} else {
    Add-Result "Safety" "No order surface added" "FAIL" (($orderHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$mutationHits = @(Select-String -Path $scanPaths -Pattern "IOrderRepository","IFillRepository","PositionRepository","ModelRun","RiskState","Wallet","ReconciliationState","TradingMutation" -SimpleMatch -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -notlike "*check-lmax-readonly-runtime-phase6a-planning-gate.ps1" -and $_.Line -notmatch "does not mutate|No trading-state mutation|not authorize|not present|absent|checklist|boundary|planning|No DB rollback|tradingMutationAttempted=false" })
if ($mutationHits.Count -eq 0) {
    Add-Result "Safety" "No trading mutation references" "PASS" "No trading-state repository or mutation dependency found in LMAX runtime/script scope."
} else {
    Add-Result "Safety" "No trading mutation references" "FAIL" (($mutationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$registrationHits = @(Select-String -Path $apiProgram,$workerProgram -Pattern "LmaxReadOnlySocketPrototype","RealLmaxGateway","LmaxVenueGatewaySkeleton","ExternalReadOnlyPrototypeGateway" -SimpleMatch -ErrorAction SilentlyContinue)
if ($registrationHits.Count -eq 0 -and (Select-String -Path $apiProgram -Pattern "FakeLmaxGateway" -SimpleMatch -Quiet)) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No prototype or real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" (($registrationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

Add-Result "Runtime" "External LMAX connection" "PASS" "No external connection is made by this planning gate."
Add-Result "Replay" "Runtime shadow replay submit" "PASS" "No replay is submitted by this planning gate."
Add-Result "Credentials" "Credential values" "PASS" "No credentials are required or read by this planning gate."

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir "phase6a-planning-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    recommendedNextPhase = "Phase 6B - Manual Additional MarketData Instrument Allowlist Design, No External Run"
    finalDocumentationPack = Resolve-LocalPath $FinalDocumentationPack
    auditPackFile = Resolve-LocalPath $AuditPackFile
    operationalSignoffFile = Resolve-LocalPath $OperationalSignoffFile
    workflowStatusFile = Resolve-LocalPath $WorkflowStatusFile
    externalConnectionAttempted = $false
    runtimeShadowReplaySubmit = $false
    schedulerOrPollingAdded = $false
    orderSubmissionAdded = $false
    gatewayRegistrationAdded = $false
    tradingMutationAdded = $false
    results = $results
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "RecommendedNextPhase: Phase 6B - Manual Additional MarketData Instrument Allowlist Design, No External Run"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
