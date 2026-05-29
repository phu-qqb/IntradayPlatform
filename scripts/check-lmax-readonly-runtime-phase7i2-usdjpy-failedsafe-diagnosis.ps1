param(
    [string]$DiagnosisReportFile = "artifacts/readiness/phase7i2-usdjpy-failedsafe-connection-diagnosis.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix)'

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Get-Hits($Paths, $Patterns) {
    $existing = @($Paths | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Patterns -SimpleMatch -ErrorAction SilentlyContinue)
}

Write-Host "LMAX Read-Only Runtime Phase 7I2 USDJPY FailedSafe Diagnosis Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$reportPath = Resolve-LocalPath $DiagnosisReportFile
if (Test-Path -LiteralPath $reportPath) {
    Add-Result "Diagnosis" "Report exists" "PASS" $reportPath
    $raw = Get-Content -LiteralPath $reportPath -Raw
    $safe = $raw -replace 'credentialProfileName|usernamePresent|passwordPresent|usernameLength|passwordLength','SAFE_METADATA'
    if ($safe -match $sensitivePattern) { Add-Result "Diagnosis" "No sensitive content" "FAIL" "Credential-shaped or raw FIX content found." } else { Add-Result "Diagnosis" "No sensitive content" "PASS" "No credential-shaped or raw FIX content." }
    $report = $raw | ConvertFrom-Json
} else {
    Add-Result "Diagnosis" "Report exists" "FAIL" "Missing $reportPath"
    $report = $null
}

if ($null -ne $report) {
    if ([string]$report.phase -eq "7I2" -and [string]$report.instrument -eq "USDJPY" -and [string]$report.securityId -eq "4004") { Add-Result "Diagnosis" "USDJPY identity" "PASS" "USDJPY / 4004." } else { Add-Result "Diagnosis" "USDJPY identity" "FAIL" "Unexpected identity." }
    if ([string]$report.finalDecision -eq "PASS_WITH_KNOWN_WARNINGS") { Add-Result "Diagnosis" "Safe warning decision" "PASS" $report.finalDecision } else { Add-Result "Diagnosis" "Safe warning decision" "FAIL" "Expected PASS_WITH_KNOWN_WARNINGS." }
    if ([string]$report.inferredFailureClass -in @("FailedSafeConnectionBeforeSessionEstablishment","FailedBeforeLogonConnectionLayer") -and [bool]$report.connectionAttempted -and -not [bool]$report.logonAttempted -and -not [bool]$report.snapshotRequestAttempted) {
        Add-Result "Diagnosis" "Connection-before-logon classification" "PASS" $report.inferredFailureClass
    } else {
        Add-Result "Diagnosis" "Connection-before-logon classification" "FAIL" "Unexpected classification or attempt flags."
    }
    if (-not [bool]$report.instrumentRejectsObserved) { Add-Result "Diagnosis" "Not instrument reject" "PASS" "No request/reject evidence observed." } else { Add-Result "Diagnosis" "Not instrument reject" "FAIL" "Instrument reject flag was set." }
    if ([bool]$report.noEvidencePreviewRequired -and -not [bool]$report.replayRun) { Add-Result "Diagnosis" "No preview/replay required" "PASS" "No MarketDataOnly preview required for FailedSafe no-snapshot artifact; replayRun=false." } else { Add-Result "Diagnosis" "No preview/replay required" "FAIL" "Unexpected preview/replay state." }
    if (-not [bool]$report.orderSubmissionAttempted -and -not [bool]$report.shadowReplaySubmitAttempted -and -not [bool]$report.tradingMutationAttempted -and -not [bool]$report.schedulerStarted -and -not [bool]$report.credentialValuesReturned -and [bool]$report.noSensitiveContent) {
        Add-Result "Diagnosis" "Unsafe flags false" "PASS" "No unsafe attempt flags."
    } else {
        Add-Result "Diagnosis" "Unsafe flags false" "FAIL" "Unsafe flag present."
    }
    $recommendation = [string]$report.recommendedNextAction
    $disallowed = ($report.disallowedActions | Out-String)
    if ($recommendation -match "future operator-approved USDJPY.*retry" -and $recommendation -match "Do not add retry automation" -and $disallowed -match "No automatic retry" -and $disallowed -match "No batch or loop") {
        Add-Result "Diagnosis" "Recommended action remains manual and controlled" "PASS" "Recommendation is bounded."
    } else {
        Add-Result "Diagnosis" "Recommended action remains manual and controlled" "FAIL" "Recommendation is missing bounded retry controls."
    }
    $ruledOut = ($report.ruledOutCauses | Out-String)
    if ($ruledOut -match "Not an instrument-level MarketDataRequestReject" -and $ruledOut -match "Not proven invalid SecurityID") {
        Add-Result "Diagnosis" "SecurityID not blamed without reject evidence" "PASS" "Ruled-out causes are explicit."
    } else {
        Add-Result "Diagnosis" "SecurityID not blamed without reject evidence" "FAIL" "Missing explicit reject/invalid-SecurityID guard."
    }
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$startupFiles = @($apiProgram, $workerProgram)
$startupText = ($startupFiles | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
if ($startupText.Contains("FakeLmaxGateway") -and -not ($startupText.Contains("RealLmaxGateway") -or $startupText.Contains("LmaxVenueGatewaySkeleton"))) { Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found." } else { Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker." }

foreach ($scan in @(
    @{ category = "Scheduler"; check = "No scheduler/polling added"; patterns = @("PeriodicTimer", "System.Threading.Timer", "LmaxScheduler", "SecurityListPolling", "MarketDataPolling", "SnapshotPolling") },
    @{ category = "Replay"; check = "Runtime still does not submit to shadow replay"; patterns = @("SubmitToShadowReplay = true", "SubmittedToShadowReplay = true", "ReplaySubmitAsync") },
    @{ category = "Orders"; check = "No order surface"; patterns = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "OrderStatusRequest", "TradeCaptureReportRequest", "SubmitOrder") },
    @{ category = "Mutation"; check = "No trading-state mutation references"; patterns = @("PersistTrade", "TradingState", "IOrderRepository", "IFillRepository", "IPositionRepository", "PersistLiveFix") }
)) {
    $hits = Get-Hits $startupFiles $scan.patterns
    if ($hits.Count -eq 0) { Add-Result $scan.category $scan.check "PASS" "No marker found in API/Worker startup." } else { Add-Result $scan.category $scan.check "FAIL" (($hits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ") }
}

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "Replay" "Automatic replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed = @($results | Where-Object status -eq "FAIL")
$decision = if ($failed.Count -gt 0) { "FAIL" } else { "PASS_WITH_KNOWN_WARNINGS" }
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7i2-usdjpy-failedsafe-diagnosis-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7I2"
    finalDecision = $decision
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    schedulerStarted = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
