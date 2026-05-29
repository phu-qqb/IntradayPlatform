param(
    [Parameter(Mandatory=$true)][string]$FinalReadinessFile,
    [switch]$AllowExternalConnections,
    [switch]$ConfirmDemoReadOnly,
    [Parameter(Mandatory=$true)][string]$Reason,
    [string]$OperatorId = "local-operator",
    [string]$Instrument = "GBPUSD",
    [string]$SlashSymbol = "GBP/USD",
    [string]$SecurityId = "4002",
    [string]$SecurityIdSource = "8",
    [string]$RequestMode = "SnapshotPlusUpdates",
    [string]$SymbolEncodingMode = "SecurityIdOnly",
    [int]$MarketDepth = 1,
    [int]$MaxWaitSeconds = 15,
    [int]$MaxRuntimeSeconds = 15,
    [int]$MaxEventsPerRun = 5
)
$ErrorActionPreference="Stop";$repoRoot=Split-Path -Parent $PSScriptRoot
function Resolve-LocalPath([string]$p){if([IO.Path]::IsPathRooted($p)){$p}else{Join-Path $repoRoot $p}}
function Fail($m){Write-Host "FinalDecision: FAIL";Write-Host $m;exit 1}
Write-Host "LMAX Phase 6W GBPUSD one-time Demo read-only snapshot wrapper"
Write-Host "WARNING: exactly one manual GBPUSD / 4002 read-only attempt only."
Write-Host "WARNING: no orders, no scheduler/polling, no runtime shadow replay submit, no trading mutation, no retry."
Write-Host "Rollback: stop process, verify FakeLmaxGateway, run Phase 6V gate, inspect sanitized artifacts."
if(-not$AllowExternalConnections){Fail "Missing -AllowExternalConnections."}
if(-not$ConfirmDemoReadOnly){Fail "Missing -ConfirmDemoReadOnly."}
if([string]::IsNullOrWhiteSpace($Reason)){Fail "Reason is required."}
if($Instrument -ne "GBPUSD" -or $SlashSymbol -ne "GBP/USD"){Fail "Only GBPUSD / GBP/USD is allowed."}
if($SecurityId -ne "4002" -or $SecurityIdSource -ne "8"){Fail "Only SecurityID 4002 / SecurityIDSource 8 is allowed."}
if($RequestMode -ne "SnapshotPlusUpdates" -or $SymbolEncodingMode -ne "SecurityIdOnly" -or $MarketDepth -ne 1){Fail "Only SnapshotPlusUpdates / SecurityIdOnly / MarketDepth 1 is allowed."}
$readinessPath=Resolve-LocalPath $FinalReadinessFile
if(-not(Test-Path -LiteralPath $readinessPath)){Fail "Final readiness file is required."}
$readiness=Get-Content -Raw -LiteralPath $readinessPath|ConvertFrom-Json
if([string]$readiness.readinessDecision -ne "PASS"){Fail "Final readiness must be PASS."}
if([string]$readiness.symbol -ne "GBPUSD" -or [string]$readiness.planningSecurityId -ne "4002" -or [string]$readiness.securityIdSource -ne "8"){Fail "Final readiness does not match GBPUSD / 4002 / source 8."}
if([bool]$readiness.isApprovedForExternalRun -or [bool]$readiness.eligibleForManualSnapshotAttempt -or [bool]$readiness.canRunExternalSnapshot -or [bool]$readiness.schedulerStarted -or [bool]$readiness.orderSubmissionAttempted -or [bool]$readiness.shadowReplaySubmitAttempted -or [bool]$readiness.tradingMutationAttempted){Fail "Final readiness contains executable or unsafe flags."}
& (Join-Path $PSScriptRoot "run-lmax-readonly-runtime-demo-snapshot-prototype.ps1") `
  -AllowExternalConnections:$AllowExternalConnections `
  -ConfirmDemoReadOnly:$ConfirmDemoReadOnly `
  -Reason $Reason `
  -OperatorId $OperatorId `
  -Instrument "GBPUSD" `
  -SlashSymbol "GBP/USD" `
  -LmaxInstrumentId "4002" `
  -RequestMode "SnapshotPlusUpdates" `
  -SymbolEncodingMode "SecurityIdOnly" `
  -MarketDepth 1 `
  -MaxWaitSeconds $MaxWaitSeconds `
  -MaxRuntimeSeconds $MaxRuntimeSeconds `
  -MaxEventsPerRun $MaxEventsPerRun `
  -SourceFinalReadinessFile $readinessPath
exit $LASTEXITCODE
