param(
    [string]$ChecklistFile = ""
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

function Has-Item($Values, [string]$Pattern) {
    return @($Values | Where-Object { ([string]$_).IndexOf($Pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count -gt 0
}

Write-Host "LMAX Read-Only Runtime Phase 7F2 EURGBP Execution Checklist Gate"
Write-Host "Local-only. This gate does not connect to LMAX, call external APIs, request SecurityList, request snapshots, replay evidence, schedule work, or use credentials."

$docPath = Join-Path $repoRoot "docs/LMAX_READONLY_EURGBP_MANUAL_SNAPSHOT_EXECUTION_CHECKLIST.md"
$scriptPath = Join-Path $PSScriptRoot "new-lmax-readonly-eurgbp-manual-snapshot-execution-checklist.ps1"
$modelPath = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyEurgbpManualSnapshotExecutionChecklist.cs"
$testsPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyEurgbpManualSnapshotExecutionChecklistTests.cs"

foreach ($required in @(
    @{ name = "Phase 7F2 checklist doc"; path = $docPath },
    @{ name = "Phase 7F2 checklist script"; path = $scriptPath },
    @{ name = "Phase 7F2 checklist model"; path = $modelPath },
    @{ name = "Phase 7F2 tests"; path = $testsPath }
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
        "DO NOT RUN IN PHASE 7F2",
        "EURGBP",
        "EUR/GBP",
        "SecurityID 4003",
        "GBPUSD market-hours closure PASS",
        "ProceedToEurgbpPlanning",
        "Phase 7E2 EURGBP readiness PASS",
        "No scheduler",
        "No polling",
        "No runtime shadow replay submit",
        "No orders",
        "No gateway registration",
        "No multi-instrument batch",
        "Ctrl+C",
        "FakeLmaxGateway",
        "artifact review",
        "evidence preview",
        "closure manifest",
        "next-instrument decision"
    )) {
        if ($doc.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            Add-Result "Doc" "Marker: $marker" "PASS" "Marker found."
        } else {
            Add-Result "Doc" "Marker: $marker" "FAIL" "Marker missing."
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ChecklistFile)) {
    Add-Result "Checklist" "Checklist supplied" "WARN" "No checklist supplied; model/script/doc source gate mode."
} else {
    $checklist = Read-JsonForGate $ChecklistFile "Checklist"
    if ($null -ne $checklist) {
        if ([string]$checklist.symbol -eq "EURGBP" -and [string]$checklist.slashSymbol -eq "EUR/GBP" -and [string]$checklist.planningSecurityId -eq "4003" -and [string]$checklist.securityIdSource -eq "8") {
            Add-Result "Checklist" "EURGBP identity" "PASS" "EURGBP / 4003 / source 8."
        } else {
            Add-Result "Checklist" "EURGBP identity" "FAIL" "Unexpected selected instrument/security identity."
        }

        if ([string]$checklist.eurgbpReadinessDecision -eq "PASS" -and [string]$checklist.previousDecision -eq "ProceedToEurgbpPlanning" -and [string]$checklist.previousInstrumentClosureDecision -eq "PASS") {
            Add-Result "Checklist" "Prerequisite decisions" "PASS" "EURGBP readiness PASS after GBPUSD PASS and ProceedToEurgbpPlanning."
        } else {
            Add-Result "Checklist" "Prerequisite decisions" "FAIL" "Prerequisite decision chain is not safe."
        }

        if ([string]$checklist.futureCommandTemplate -match "DO NOT RUN IN PHASE 7F2" -and [string]$checklist.futureCommandTemplate -match "EURGBP" -and [string]$checklist.futureCommandTemplate -match "4003") {
            Add-Result "Checklist" "Future command marked non-executable" "PASS" "Command template is present and marked non-executable."
        } else {
            Add-Result "Checklist" "Future command marked non-executable" "FAIL" "Command template missing EURGBP/4003 or DO NOT RUN warning."
        }

        if (-not [bool]$checklist.externalRunAuthorized -and -not [bool]$checklist.canRunExternalSnapshot -and -not [bool]$checklist.eligibleForManualSnapshotAttempt -and -not [bool]$checklist.isApprovedForExternalRun) {
            Add-Result "Checklist" "Run eligibility false" "PASS" "All run eligibility flags remain false."
        } else {
            Add-Result "Checklist" "Run eligibility false" "FAIL" "Run eligibility flag is true."
        }

        if (-not [bool]$checklist.schedulerOrPolling -and -not [bool]$checklist.runtimeShadowReplaySubmit -and -not [bool]$checklist.orderSubmission -and -not [bool]$checklist.tradingMutation -and -not [bool]$checklist.batchExecutionAllowed -and [bool]$checklist.oneInstrumentAtATime -and [string]$checklist.apiWorkerGatewayMode -eq "FakeLmaxGateway" -and [bool]$checklist.noSensitiveContent) {
            Add-Result "Checklist" "Safety flags" "PASS" "No scheduler, replay submit, order, mutation, batch; one-at-a-time; FakeLmaxGateway."
        } else {
            Add-Result "Checklist" "Safety flags" "FAIL" "Unsafe checklist flag found."
        }

        if ((Has-Item $checklist.abortCriteria "wrong symbol") -and (Has-Item $checklist.abortCriteria "scheduler") -and (Has-Item $checklist.abortCriteria "batch") -and (Has-Item $checklist.rollbackSteps "FakeLmaxGateway") -and (Has-Item $checklist.postRunValidationSteps "artifact review") -and (Has-Item $checklist.postRunValidationSteps "evidence preview") -and (Has-Item $checklist.postRunValidationSteps "closure manifest")) {
            Add-Result "Checklist" "Abort rollback post-run coverage" "PASS" "Abort criteria, rollback, and post-run validation are present."
        } else {
            Add-Result "Checklist" "Abort rollback post-run coverage" "FAIL" "Checklist coverage incomplete."
        }

        if ([string]$checklist.decision -eq "PASS") {
            Add-Result "Checklist" "Decision PASS" "PASS" "Checklist decision PASS."
        } else {
            Add-Result "Checklist" "Decision PASS" "FAIL" "Checklist decision $($checklist.decision)."
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
$outPath = Join-Path $outDir "phase7f2-eurgbp-execution-checklist-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7F2"
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
