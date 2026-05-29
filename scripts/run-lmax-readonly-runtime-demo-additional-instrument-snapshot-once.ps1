param(
    [Parameter(Mandatory=$true)]
    [string]$Symbol,
    [Parameter(Mandatory=$true)]
    [string]$FinalPreRunGateFile,
    [Parameter(Mandatory=$true)]
    [switch]$AllowExternalConnections,
    [Parameter(Mandatory=$true)]
    [switch]$ConfirmDemoReadOnly,
    [Parameter(Mandatory=$true)]
    [string]$Reason,
    [string]$OperatorId = "local-operator",
    [int]$MaxWaitSeconds = 15,
    [int]$MaxRuntimeSeconds = 15,
    [int]$MaxEventsPerRun = 5
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Fail([string]$Message) { throw "Phase 7H additional-instrument one-shot gate failed: $Message" }
function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

$symbolKey = $Symbol.Trim().ToUpperInvariant()
if ($symbolKey -match '[,;\s]') { Fail "Exactly one symbol is required; batch/multiple instruments are refused." }

$instrumentMap = @{
    GBPUSD = @{ slashSymbol = "GBP/USD"; securityId = "4002" }
    EURGBP = @{ slashSymbol = "EUR/GBP"; securityId = "4003" }
    USDJPY = @{ slashSymbol = "USD/JPY"; securityId = "4004" }
    AUDUSD = @{ slashSymbol = "AUD/USD"; securityId = "4007" }
}
if (-not $instrumentMap.ContainsKey($symbolKey)) { Fail "Unsupported symbol '$Symbol'. Supported: GBPUSD, EURGBP, USDJPY, AUDUSD." }
if (-not $AllowExternalConnections.IsPresent) { Fail "AllowExternalConnections is required for the future manual run." }
if (-not $ConfirmDemoReadOnly.IsPresent) { Fail "ConfirmDemoReadOnly is required." }
if ([string]::IsNullOrWhiteSpace($Reason)) { Fail "Reason is required." }

$definition = $instrumentMap[$symbolKey]
$gatePath = Resolve-LocalPath $FinalPreRunGateFile
if (-not (Test-Path -LiteralPath $gatePath)) { Fail "Final pre-run gate file not found: $gatePath" }

$rawGate = Get-Content -Raw -LiteralPath $gatePath
$safeScanText = $rawGate -replace 'LMAX_DEMO_FIX_USERNAME|LMAX_DEMO_FIX_PASSWORD|LMAX_DEMO_SENDER_COMP_ID|LMAX_DEMO_TARGET_COMP_ID|credentialProfileName','SAFE_METADATA'
if ($safeScanText -match '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix|NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatusRequest|SubmitOrder)') {
    Fail "Final pre-run gate contains forbidden sensitive/order-shaped text."
}
$gate = $rawGate | ConvertFrom-Json
$sourceDecision = if ($gate.PSObject.Properties.Name -contains "finalDecision") { [string]$gate.finalDecision } elseif ($gate.PSObject.Properties.Name -contains "readinessDecision") { [string]$gate.readinessDecision } else { "" }
if ($sourceDecision -ne "PASS") { Fail "Final pre-run gate decision must be PASS; found '$sourceDecision'." }
if ([string]$gate.symbol -ne $symbolKey -or [string]$gate.slashSymbol -ne $definition.slashSymbol -or [string]$gate.planningSecurityId -ne $definition.securityId -or [string]$gate.securityIdSource -ne "8") {
    Fail "Final pre-run gate identity must match $symbolKey / $($definition.slashSymbol) / $($definition.securityId) / source 8."
}
if ([string]$gate.environmentName -ne "Demo" -or [string]$gate.venueProfileName -ne "DemoLondon" -or [string]$gate.requestMode -ne "SnapshotPlusUpdates" -or [string]$gate.symbolEncodingMode -ne "SecurityIdOnly" -or [int]$gate.marketDepth -ne 1) {
    Fail "Final pre-run gate must be Demo/DemoLondon/SnapshotPlusUpdates/SecurityIdOnly/depth 1."
}
if ($symbolKey -eq "EURGBP") {
    if ([string]$gate.previousInstrument -ne "GBPUSD" -or [string]$gate.previousInstrumentClosureDecision -ne "PASS" -or [string]$gate.previousDecision -ne "ProceedToEurgbpPlanning") {
        Fail "EURGBP requires GBPUSD closure PASS and Phase 7D ProceedToEurgbpPlanning."
    }
}
if (-not [bool]$gate.oneInstrumentAtATime -or [bool]$gate.batchExecutionAllowed) { Fail "One-instrument-at-a-time must be true and batch execution must be false." }
if ([bool]$gate.externalRunAuthorized -or [bool]$gate.canRunExternalSnapshot -or [bool]$gate.eligibleForManualSnapshotAttempt -or [bool]$gate.isApprovedForExternalRun) {
    Fail "Final pre-run gate must remain non-executable; run eligibility flags must be false."
}
if ([bool]$gate.schedulerOrPolling -or [bool]$gate.schedulerStarted -or [bool]$gate.runtimeShadowReplaySubmit -or [bool]$gate.shadowReplaySubmitAttempted -or [bool]$gate.orderSubmission -or [bool]$gate.orderSubmissionAttempted -or [bool]$gate.tradingMutation -or [bool]$gate.tradingMutationAttempted -or [bool]$gate.gatewayRegistration) {
    Fail "Final pre-run gate contains scheduler/shadow/order/mutation/gateway flags."
}
if (($gate.PSObject.Properties.Name -contains "apiWorkerGatewayMode") -and [string]$gate.apiWorkerGatewayMode -ne "FakeLmaxGateway") {
    Fail "API/Worker gateway mode must remain FakeLmaxGateway."
}

Write-Host "LMAX Phase 7H additional-instrument manual one-shot wrapper"
Write-Host "WARNING: This wrapper performs exactly one operator-approved Demo read-only MarketData attempt when run with explicit flags."
Write-Host "WARNING: Symbol=$symbolKey SlashSymbol=$($definition.slashSymbol) SecurityID=$($definition.securityId) SecurityIDSource=8."
Write-Host "WARNING: No batch, no loop, no retry, no scheduler, no polling, no runtime shadow replay submit, no orders, no mutation."
Write-Host "Kill switch: Ctrl+C or close this process. Rollback: verify API/Worker FakeLmaxGateway and inspect sanitized artifact."

& (Join-Path $PSScriptRoot "run-lmax-readonly-runtime-demo-snapshot-prototype.ps1") `
    -Instrument $symbolKey `
    -SlashSymbol $definition.slashSymbol `
    -LmaxInstrumentId $definition.securityId `
    -RequestMode SnapshotPlusUpdates `
    -SymbolEncodingMode SecurityIdOnly `
    -MarketDepth 1 `
    -AllowExternalConnections:$AllowExternalConnections `
    -ConfirmDemoReadOnly:$ConfirmDemoReadOnly `
    -Reason $Reason `
    -OperatorId $OperatorId `
    -MaxWaitSeconds $MaxWaitSeconds `
    -MaxRuntimeSeconds $MaxRuntimeSeconds `
    -MaxEventsPerRun $MaxEventsPerRun `
    -SourceFinalReadinessFile $FinalPreRunGateFile
