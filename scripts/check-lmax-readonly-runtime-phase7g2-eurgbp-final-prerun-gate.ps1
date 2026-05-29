param(
    [string]$FinalPreRunGateFile = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix|sendercompid|targetcompid)'

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{ category = $Category; check = $Check; status = $Status; detail = $Detail }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Read-JsonForGate([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if ([string]::IsNullOrWhiteSpace($resolved) -or -not (Test-Path -LiteralPath $resolved)) {
        Add-Result $Label "File exists" "FAIL" "Missing $resolved"
        return $null
    }
    Add-Result $Label "File exists" "PASS" $resolved
    $raw = Get-Content -LiteralPath $resolved -Raw
    if ($raw -match $script:sensitivePattern) {
        Add-Result $Label "No sensitive content" "FAIL" "Credential-shaped or raw FIX content found."
    } else {
        Add-Result $Label "No sensitive content" "PASS" "No credential-shaped or raw FIX content."
    }
    return ($raw | ConvertFrom-Json)
}

function Get-Hits($Paths, $Patterns) {
    $existing = @($Paths | Where-Object { Test-Path -LiteralPath $_ })
    if ($existing.Count -eq 0) { return @() }
    return @(Select-String -Path $existing -Pattern $Patterns -SimpleMatch -ErrorAction SilentlyContinue)
}

Write-Host "LMAX Read-Only Runtime Phase 7G2 EURGBP Final Pre-Run Gate"
Write-Host "Local-only. This gate does not connect to LMAX, call external APIs, request SecurityList, request snapshots, replay evidence, schedule work, or use credentials."

$docPath = Join-Path $repoRoot "docs/LMAX_READONLY_EURGBP_MANUAL_SNAPSHOT_EXECUTION_CHECKLIST.md"
$builderPath = Join-Path $PSScriptRoot "new-lmax-readonly-eurgbp-final-pre-run-gate.ps1"
$gatePath = Join-Path $PSScriptRoot "check-lmax-readonly-runtime-phase7g2-eurgbp-final-prerun-gate.ps1"
$modelPath = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGate.cs"
$testsPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyEurgbpManualSnapshotFinalPreRunGateTests.cs"

foreach ($required in @(
    @{ name = "Phase 7G2 EURGBP checklist doc"; path = $docPath },
    @{ name = "Phase 7G2 final pre-run builder"; path = $builderPath },
    @{ name = "Phase 7G2 gate script"; path = $gatePath },
    @{ name = "Phase 7G2 model"; path = $modelPath },
    @{ name = "Phase 7G2 tests"; path = $testsPath }
)) {
    if (Test-Path -LiteralPath $required.path) {
        Add-Result "Files" "$($required.name) exists" "PASS" $required.path
    } else {
        Add-Result "Files" "$($required.name) exists" "FAIL" "Missing $($required.path)"
    }
}

if (Test-Path -LiteralPath $docPath) {
    $doc = Get-Content -LiteralPath $docPath -Raw
    if ($doc -match $sensitivePattern) { Add-Result "Doc" "No sensitive content" "FAIL" "Credential-shaped content found." } else { Add-Result "Doc" "No sensitive content" "PASS" "No credential-shaped content." }
    foreach ($marker in @(
        "Phase 7G2",
        "EURGBP",
        "SecurityID 4003",
        "does not authorize execution",
        "one-instrument-at-a-time",
        "canRunExternalSnapshot=false",
        "IsApprovedForExternalRun=false",
        "eligibleForManualSnapshotAttempt=false",
        "No scheduler",
        "No polling",
        "No runtime shadow replay submit",
        "No orders"
    )) {
        if ($doc.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            Add-Result "Doc" "Marker: $marker" "PASS" "Marker found."
        } else {
            Add-Result "Doc" "Marker: $marker" "FAIL" "Marker missing."
        }
    }
}

if ([string]::IsNullOrWhiteSpace($FinalPreRunGateFile)) {
    Add-Result "FinalPreRunGate" "Final pre-run artifact supplied" "WARN" "No final pre-run gate supplied; model/script/doc source gate mode."
} else {
    $gate = Read-JsonForGate $FinalPreRunGateFile "FinalPreRunGate"
    if ($null -ne $gate) {
        if ([string]$gate.symbol -eq "EURGBP" -and [string]$gate.slashSymbol -eq "EUR/GBP" -and [string]$gate.planningSecurityId -eq "4003" -and [string]$gate.securityIdSource -eq "8") {
            Add-Result "FinalPreRunGate" "EURGBP identity" "PASS" "EURGBP / 4003 / source 8."
        } else {
            Add-Result "FinalPreRunGate" "EURGBP identity" "FAIL" "Unexpected instrument/security identity."
        }

        if ([string]$gate.environmentName -eq "Demo" -and [string]$gate.venueProfileName -eq "DemoLondon" -and [string]$gate.requestMode -eq "SnapshotPlusUpdates" -and [string]$gate.symbolEncodingMode -eq "SecurityIdOnly" -and [int]$gate.marketDepth -eq 1) {
            Add-Result "FinalPreRunGate" "Demo snapshot profile" "PASS" "DemoLondon / SnapshotPlusUpdates / SecurityIdOnly / depth 1."
        } else {
            Add-Result "FinalPreRunGate" "Demo snapshot profile" "FAIL" "Unexpected environment, profile, mode, encoding, or depth."
        }

        if ([string]$gate.previousInstrument -eq "GBPUSD" -and [string]$gate.previousInstrumentClosureDecision -eq "PASS" -and [string]$gate.previousDecision -eq "ProceedToEurgbpPlanning" -and [string]$gate.sourceEurgbpReadinessDecision -eq "PASS" -and [string]$gate.sourceExecutionChecklistDecision -eq "PASS") {
            Add-Result "FinalPreRunGate" "Prerequisite decisions" "PASS" "GBPUSD PASS, Phase 7D proceed, readiness PASS, checklist PASS."
        } else {
            Add-Result "FinalPreRunGate" "Prerequisite decisions" "FAIL" "Prerequisite decision chain is not safe."
        }

        if ([bool]$gate.oneInstrumentAtATime -and -not [bool]$gate.batchExecutionAllowed) {
            Add-Result "FinalPreRunGate" "Manual sequencing" "PASS" "One-instrument-at-a-time; batch disabled."
        } else {
            Add-Result "FinalPreRunGate" "Manual sequencing" "FAIL" "One-instrument or batch flag is unsafe."
        }

        if (-not [bool]$gate.externalRunAuthorized -and -not [bool]$gate.canRunExternalSnapshot -and -not [bool]$gate.eligibleForManualSnapshotAttempt -and -not [bool]$gate.isApprovedForExternalRun) {
            Add-Result "FinalPreRunGate" "Run eligibility false" "PASS" "All run authorization and eligibility flags remain false."
        } else {
            Add-Result "FinalPreRunGate" "Run eligibility false" "FAIL" "Run eligibility flag is true."
        }

        if (-not [bool]$gate.schedulerOrPolling -and -not [bool]$gate.runtimeShadowReplaySubmit -and -not [bool]$gate.orderSubmission -and -not [bool]$gate.tradingMutation -and -not [bool]$gate.gatewayRegistration -and [string]$gate.apiWorkerGatewayMode -eq "FakeLmaxGateway" -and [bool]$gate.noSensitiveContent) {
            Add-Result "FinalPreRunGate" "Safety flags" "PASS" "No scheduler, replay submit, order, mutation, gateway registration; FakeLmaxGateway."
        } else {
            Add-Result "FinalPreRunGate" "Safety flags" "FAIL" "Unsafe final pre-run flag found."
        }

        if ([string]$gate.finalDecision -eq "PASS") {
            Add-Result "FinalPreRunGate" "Decision PASS" "PASS" "Final pre-run decision PASS."
        } else {
            Add-Result "FinalPreRunGate" "Decision PASS" "FAIL" "Final pre-run decision $($gate.finalDecision)."
        }
    }
}

$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"
$startupFiles = @($apiProgram, $workerProgram)
$startupText = ($startupFiles | Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"

if ($startupText.Contains("FakeLmaxGateway") -and -not ($startupText.Contains("RealLmaxGateway") -or $startupText.Contains("LmaxVenueGatewaySkeleton"))) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" "Unexpected gateway registration marker."
}

foreach ($scan in @(
    @{ category = "Scheduler"; check = "No scheduler/polling added"; patterns = @("PeriodicTimer", "System.Threading.Timer", "LmaxScheduler", "SecurityListPolling", "MarketDataPolling", "SnapshotPolling") },
    @{ category = "Replay"; check = "Runtime still does not submit to shadow replay"; patterns = @("SubmitToShadowReplay = true", "SubmittedToShadowReplay = true", "ReplaySubmitAsync") },
    @{ category = "Orders"; check = "No order surface"; patterns = @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "OrderStatusRequest", "TradeCaptureReportRequest", "SubmitOrder") },
    @{ category = "Mutation"; check = "No trading-state mutation references"; patterns = @("PersistTrade", "TradingState", "IOrderRepository", "IFillRepository", "IPositionRepository", "PersistLiveFix") }
)) {
    $hits = Get-Hits $startupFiles $scan.patterns
    if ($hits.Count -eq 0) {
        Add-Result $scan.category $scan.check "PASS" "No marker found in API/Worker startup."
    } else {
        Add-Result $scan.category $scan.check "FAIL" (($hits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
    }
}

Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "SecurityList" "SecurityListRequest" "PASS" "This gate does not request SecurityList."
Add-Result "Replay" "Automatic replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed = @($results | Where-Object status -eq "FAIL")
$warnings = @($results | Where-Object status -eq "WARN")
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir "phase7g2-eurgbp-final-prerun-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7G2"
    finalDecision = $decision
    selectedInstrument = "EURGBP"
    securityId = "4003"
    externalRunAuthorized = $false
    canRunExternalSnapshot = $false
    isApprovedForExternalRun = $false
    eligibleForManualSnapshotAttempt = $false
    externalConnectionAttempted = $false
    snapshotAttempted = $false
    replayAttempted = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    schedulerStarted = $false
    apiWorkerGatewayMode = "FakeLmaxGateway"
    results = $results
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
