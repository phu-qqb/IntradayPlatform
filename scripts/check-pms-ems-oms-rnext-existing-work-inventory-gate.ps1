param(
    [string]$ArtifactDirectory = "artifacts/readiness/pms-ems-oms-integration"
)

$ErrorActionPreference = "Stop"

function Fail-Gate {
    param(
        [string]$Classification,
        [string]$Message
    )

    Write-Error "$Classification`: $Message"
    exit 1
}

function Read-JsonArtifact {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_EXISTING_WORK_INVENTORY_MISSING" "Missing required artifact: $Path"
    }

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_EXISTING_WORK_INVENTORY_MISSING" "Artifact is not valid JSON: $Path"
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot $ArtifactDirectory

$requiredArtifacts = @(
    "phase-pms-ems-oms-rnext-existing-work-inventory-summary.md",
    "phase-pms-ems-oms-rnext-existing-work-inventory.json",
    "phase-pms-ems-oms-rnext-existing-code-map.json",
    "phase-pms-ems-oms-rnext-existing-tests-map.json",
    "phase-pms-ems-oms-rnext-qubes-weights-status.json",
    "phase-pms-ems-oms-rnext-pnl-status.json",
    "phase-pms-ems-oms-rnext-reconciliation-status.json",
    "phase-pms-ems-oms-rnext-theoretical-vs-real-status.json",
    "phase-pms-ems-oms-rnext-ems-oms-status.json",
    "phase-pms-ems-oms-rnext-lmax-readonly-baseline-integration.json",
    "phase-pms-ems-oms-rnext-gap-analysis.json",
    "phase-pms-ems-oms-rnext-next-action-decision.json",
    "phase-pms-ems-oms-rnext-no-external-audit.json",
    "phase-pms-ems-oms-rnext-forbidden-actions-audit.json",
    "phase-pms-ems-oms-rnext-next-phase-recommendation.json",
    "phase-pms-ems-oms-rnext-build-validator-evidence.json"
)

foreach ($name in $requiredArtifacts) {
    $path = Join-Path $artifactRoot $name
    if (-not (Test-Path -LiteralPath $path)) {
        $classification = switch -Regex ($name) {
            "qubes" { "PMS_EMS_OMS_RNEXT_FAIL_QUBES_WEIGHTS_STATUS_MISSING"; break }
            "pnl" { "PMS_EMS_OMS_RNEXT_FAIL_PNL_STATUS_MISSING"; break }
            "reconciliation" { "PMS_EMS_OMS_RNEXT_FAIL_RECONCILIATION_STATUS_MISSING"; break }
            "theoretical-vs-real" { "PMS_EMS_OMS_RNEXT_FAIL_THEORETICAL_REAL_STATUS_MISSING"; break }
            "ems-oms" { "PMS_EMS_OMS_RNEXT_FAIL_EMS_OMS_STATUS_MISSING"; break }
            "lmax-readonly-baseline" { "PMS_EMS_OMS_RNEXT_FAIL_LMAX_BASELINE_MAPPING_MISSING"; break }
            "next-action" { "PMS_EMS_OMS_RNEXT_FAIL_EXISTING_WORK_INVENTORY_MISSING"; break }
            "build-validator" { "PMS_EMS_OMS_RNEXT_FAIL_BUILD_OR_VALIDATOR"; break }
            default { "PMS_EMS_OMS_RNEXT_FAIL_EXISTING_WORK_INVENTORY_MISSING" }
        }
        Fail-Gate $classification "Missing required artifact: $name"
    }
}

$inventory = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-rnext-existing-work-inventory.json")
$qubes = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-rnext-qubes-weights-status.json")
$pnl = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-rnext-pnl-status.json")
$reconciliation = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-rnext-reconciliation-status.json")
$theoretical = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-rnext-theoretical-vs-real-status.json")
$emsOms = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-rnext-ems-oms-status.json")
$lmax = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-rnext-lmax-readonly-baseline-integration.json")
$decision = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-rnext-next-action-decision.json")
$noExternal = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-rnext-no-external-audit.json")
$forbidden = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-rnext-forbidden-actions-audit.json")
$evidence = Read-JsonArtifact (Join-Path $artifactRoot "phase-pms-ems-oms-rnext-build-validator-evidence.json")

if (-not [bool]$inventory.inventoryCreated) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_EXISTING_WORK_INVENTORY_MISSING" "Inventory marker is missing."
}

if (-not [bool]$qubes.statusArtifactCreated) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_QUBES_WEIGHTS_STATUS_MISSING" "Qubes weights status marker is missing."
}

if (-not [bool]$pnl.statusArtifactCreated) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_PNL_STATUS_MISSING" "PnL status marker is missing."
}

if (-not [bool]$reconciliation.statusArtifactCreated) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_RECONCILIATION_STATUS_MISSING" "Reconciliation status marker is missing."
}

if (-not [bool]$theoretical.statusArtifactCreated) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_THEORETICAL_REAL_STATUS_MISSING" "Theoretical-vs-real status marker is missing."
}

if (-not [bool]$emsOms.statusArtifactCreated) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_EMS_OMS_STATUS_MISSING" "EMS/OMS status marker is missing."
}

if (-not [bool]$lmax.mappingArtifactCreated) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_LMAX_BASELINE_MAPPING_MISSING" "LMAX baseline mapping marker is missing."
}

if (-not [bool]$decision.decisionArtifactCreated -or [string]$decision.selectedNextAction -ne "Qubes weights fixture ingestion into existing PMS model") {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_EXISTING_WORK_INVENTORY_MISSING" "Next action decision is missing or not concrete."
}

if ([bool]$noExternal.externalBrokerActivationDetected -or [bool]$noExternal.socketTlsFixMarketDataRuntimeActionDetected) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "No-external audit detected an external runtime action."
}

if ([bool]$noExternal.orderSubmissionIntroduced -or [bool]$noExternal.liveTradingPathIntroduced -or [bool]$noExternal.tradingStateMutated) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Order/trading mutation audit failed."
}

if ([bool]$noExternal.rawFixSerialized -or [bool]$noExternal.rawEndpointTlsValuesSerialized -or [bool]$noExternal.sessionIdsSerialized -or [bool]$noExternal.compIdsSerialized -or [bool]$noExternal.rawMdReqIdSerialized -or [bool]$noExternal.rawMarketDataPayloadsSerialized -or [bool]$noExternal.rawMarketDataPricesSerialized) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Sanitization audit detected unsafe serialization."
}

foreach ($item in $forbidden.forbiddenActions) {
    if ([bool]$item.detected) {
        if ([string]$item.action -match "order|trading") {
            Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Forbidden action detected: $($item.action)"
        }
        Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "Forbidden action detected: $($item.action)"
    }
}

if (-not [bool]$lmax.baseline.gbpusd.readOnlyMarketDataProven -or [string]$lmax.baseline.gbpusd.marketDataResponseCategory -ne "Succeeded" -or [int]$lmax.baseline.gbpusd.sanitizedEntryCount -ne 2) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_LMAX_BASELINE_MAPPING_MISSING" "GBPUSD read-only baseline mapping is incomplete."
}

if (-not [bool]$lmax.baseline.eurgbp.readOnlyMarketDataProven -or [string]$lmax.baseline.eurgbp.marketDataResponseCategory -ne "Succeeded" -or [int]$lmax.baseline.eurgbp.sanitizedEntryCount -ne 2) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_LMAX_BASELINE_MAPPING_MISSING" "EURGBP read-only baseline mapping is incomplete."
}

if (-not [bool]$lmax.baseline.usdjpy.caveatPreserved -or [bool]$lmax.baseline.usdjpy.classifiedAsFailed -or [string]$lmax.baseline.usdjpy.securityId -ne "4004" -or [string]$lmax.baseline.usdjpy.securityIdSource -ne "8") {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat was weakened."
}

if ([bool]$lmax.baseline.audusd.classifiedAsFailed -or [bool]$lmax.baseline.audusd.marketDataTested) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD was misclassified."
}

$apiSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Api/appsettings.json")
$workerSettings = Read-JsonArtifact (Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/appsettings.json")

if ([bool]$apiSettings.Safety.AllowLiveTrading -or [bool]$workerSettings.Safety.AllowLiveTrading) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "AllowLiveTrading is enabled."
}

if ([bool]$apiSettings.Safety.AllowExternalConnections -or [bool]$workerSettings.Safety.AllowExternalConnections) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "AllowExternalConnections is enabled."
}

if (-not [bool]$apiSettings.Safety.RequireFakeExecutionGateway -or -not [bool]$workerSettings.Safety.RequireFakeExecutionGateway) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_ORDER_OR_TRADING_PATH_INTRODUCED" "Fake execution gateway is not required."
}

if ([bool]$apiSettings.LmaxReadOnlyRuntime.Enabled -or [bool]$apiSettings.LmaxReadOnlyRuntime.AllowExternalConnections -or [bool]$apiSettings.LmaxReadOnlyRuntime.AllowOrderSubmission -or [bool]$apiSettings.LmaxReadOnlyRuntime.SchedulerEnabled -or [bool]$apiSettings.LmaxReadOnlyRuntime.SubmitToShadowReplay -or [bool]$apiSettings.LmaxReadOnlyRuntime.PersistRawFixMessages) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_NEW_EXTERNAL_ACTION_DETECTED" "LMAX read-only runtime is not fully disabled/safe."
}

$artifactText = Get-ChildItem -LiteralPath $artifactRoot -File | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = [string]::Join("`n", $artifactText)
$unsafePatterns = @(
    "\u0001",
    "35=",
    "MDReqID\s*[:=]",
    "SenderCompID\s*[:=]",
    "TargetCompID\s*[:=]",
    "BeginString\s*[:=]",
    "SocketHost\s*[:=]",
    "TlsHost\s*[:=]",
    "Password\s*[:=]",
    "ApiKey\s*[:=]",
    "Secret\s*[:=]",
    "Bearer\s+[A-Za-z0-9_\.-]+"
)

foreach ($pattern in $unsafePatterns) {
    if ($combined -match $pattern) {
        Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_SANITIZATION_OR_SECRET_LEAK_RISK" "Unsafe serialized content pattern found: $pattern"
    }
}

if ([string]$evidence.build.status -ne "PASS") {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_BUILD_OR_VALIDATOR" "Build evidence is missing or not PASS."
}

if (-not (Test-Path -LiteralPath (Join-Path $repoRoot "scripts/check-pms-ems-oms-rnext-existing-work-inventory-gate.ps1"))) {
    Fail-Gate "PMS_EMS_OMS_RNEXT_FAIL_BUILD_OR_VALIDATOR" "Validator script is missing."
}

Write-Host "PMS_EMS_OMS_RNEXT_PASS_EXISTING_WORK_INVENTORY_READY_NO_EXTERNAL"
Write-Host "PMS_EMS_OMS_RNEXT_PASS_NEXT_INTEGRATION_STEP_IDENTIFIED_NO_EXTERNAL"
