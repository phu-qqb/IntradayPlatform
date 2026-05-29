param(
    [Parameter(Mandatory=$true)][string]$ExecutionPlanFile,
    [string]$Phase6TGateReportFile = "",
    [Parameter(Mandatory=$true)][string]$RequestedByOperatorId,
    [Parameter(Mandatory=$true)][string]$SignedByOperatorId,
    [Parameter(Mandatory=$true)][string]$Reason,
    [Parameter(Mandatory=$true)][ValidateSet("Draft","SignedForPlanning","Rejected")][string]$SignoffDecision,
    [switch]$ConfirmAllPlanningAttestations,
    [switch]$ConfirmsExecutionPlanReviewed,
    [switch]$ConfirmsKillRollbackPlanReviewed,
    [switch]$ConfirmsDemoOnly,
    [switch]$ConfirmsReadOnlyMarketDataOnly,
    [switch]$ConfirmsSingleInstrumentOnly,
    [switch]$ConfirmsNoOrderSubmission,
    [switch]$ConfirmsNoSchedulerOrPolling,
    [switch]$ConfirmsNoRuntimeShadowReplaySubmit,
    [switch]$ConfirmsNoTradingMutation,
    [switch]$ConfirmsNoGatewayRegistration,
    [switch]$ConfirmsCredentialValuesMustRemainRedacted,
    [switch]$ConfirmsFutureManualExecutionPhaseRequired,
    [string]$OutputDirectory="artifacts/lmax-readonly-runtime-securityid-planning/operator-signoffs",
    [switch]$WhatIfPreview,
    [switch]$Force
)
$ErrorActionPreference="Stop";$repoRoot=Split-Path -Parent $PSScriptRoot
function Resolve-LocalPath([string]$p){if([string]::IsNullOrWhiteSpace($p)){return $p};if([IO.Path]::IsPathRooted($p)){$p}else{Join-Path $repoRoot $p}}
function Bad($v){$v -match "(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)"}
function Auth($v){$v -match "(?i)(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized|currently authorized|is authorized|authorizes execution)"}
Write-Host "LMAX read-only Phase 6U GBPUSD operator signoff"
Write-Host "Local-only. No LMAX connection, no external API, no SecurityListRequest, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."
$ep=Resolve-LocalPath $ExecutionPlanFile
if(-not(Test-Path $ep)){Write-Host "FinalDecision: FAIL";Write-Host "Missing execution plan: $ep";exit 1}
$planText=Get-Content -Raw $ep;$plan=$planText|ConvertFrom-Json
$gateDecision=""
$gatePath=Resolve-LocalPath $Phase6TGateReportFile
if(-not[string]::IsNullOrWhiteSpace($gatePath)){if(-not(Test-Path $gatePath)){Write-Host "FinalDecision: FAIL";Write-Host "Missing Phase 6T gate report: $gatePath";exit 1};$gate=Get-Content -Raw $gatePath|ConvertFrom-Json;$gateDecision=[string]$gate.finalDecision}
if(Bad ($planText+" "+$RequestedByOperatorId+" "+$SignedByOperatorId+" "+$Reason)){Write-Host "FinalDecision: FAIL";Write-Host "Sensitive-shaped content found.";exit 1}
if(Auth ($RequestedByOperatorId+" "+$SignedByOperatorId+" "+$Reason)){Write-Host "FinalDecision: FAIL";Write-Host "Authorization language found.";exit 1}
$all=[bool]$ConfirmAllPlanningAttestations
$stamp=[DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$signoff=[ordered]@{
 signoffId="lmax-readonly-gbpusd-manual-snapshot-operator-signoff-$stamp"; createdAtUtc=[DateTimeOffset]::UtcNow.ToString("o"); requestedByOperatorId=$RequestedByOperatorId; signedByOperatorId=$SignedByOperatorId; signoffRole="Operator"; reason=$Reason
 symbol=[string]$plan.symbol; slashSymbol=[string]$plan.slashSymbol; planningSecurityId=[string]$plan.planningSecurityId; securityIdSource=[string]$plan.securityIdSource; environmentName=[string]$plan.environmentName; venueProfileName=[string]$plan.venueProfileName
 requestMode=[string]$plan.requestMode; symbolEncodingMode=[string]$plan.symbolEncodingMode; marketDepth=[int]$plan.marketDepth; sourceExecutionPlanPath=$ep; sourceExecutionPlanDecision=[string]$plan.decision; sourcePhase6TGateReportPath=$gatePath; sourcePhase6TGateDecision=$gateDecision
 confirmsExecutionPlanReviewed=($all -or [bool]$ConfirmsExecutionPlanReviewed); confirmsKillRollbackPlanReviewed=($all -or [bool]$ConfirmsKillRollbackPlanReviewed); confirmsDemoOnly=($all -or [bool]$ConfirmsDemoOnly); confirmsReadOnlyMarketDataOnly=($all -or [bool]$ConfirmsReadOnlyMarketDataOnly); confirmsSingleInstrumentOnly=($all -or [bool]$ConfirmsSingleInstrumentOnly)
 confirmsNoOrderSubmission=($all -or [bool]$ConfirmsNoOrderSubmission); confirmsNoSchedulerOrPolling=($all -or [bool]$ConfirmsNoSchedulerOrPolling); confirmsNoRuntimeShadowReplaySubmit=($all -or [bool]$ConfirmsNoRuntimeShadowReplaySubmit); confirmsNoTradingMutation=($all -or [bool]$ConfirmsNoTradingMutation); confirmsNoGatewayRegistration=($all -or [bool]$ConfirmsNoGatewayRegistration); confirmsCredentialValuesMustRemainRedacted=($all -or [bool]$ConfirmsCredentialValuesMustRemainRedacted); confirmsFutureManualExecutionPhaseRequired=($all -or [bool]$ConfirmsFutureManualExecutionPhaseRequired)
 signoffDecision=$SignoffDecision; isApprovedForExternalRun=$false; eligibleForManualSnapshotAttempt=$false; canRunExternalSnapshot=$false; externalConnectionAttempted=$false; snapshotAttempted=$false; replayAttempted=$false; orderSubmissionAttempted=$false; shadowReplaySubmitAttempted=$false; tradingMutationAttempted=$false; schedulerStarted=$false; noSensitiveContent=$true
}
$issues=@()
if($signoff.symbol -ne "GBPUSD"){$issues+="WrongSymbol"};if($signoff.planningSecurityId -ne "4002"){$issues+="WrongSecurityID"};if($signoff.securityIdSource -ne "8"){$issues+="WrongSecurityIDSource"}
if($signoff.sourceExecutionPlanDecision -ne "PASS"){$issues+="ExecutionPlanNotPass"};if($gatePath -and $signoff.sourcePhase6TGateDecision -ne "PASS"){$issues+="Phase6TGateNotPass"}
if($SignoffDecision -eq "SignedForPlanning"){if([string]::IsNullOrWhiteSpace($SignedByOperatorId)){$issues+="MissingSignedBy"};foreach($a in @("confirmsExecutionPlanReviewed","confirmsKillRollbackPlanReviewed","confirmsDemoOnly","confirmsReadOnlyMarketDataOnly","confirmsSingleInstrumentOnly","confirmsNoOrderSubmission","confirmsNoSchedulerOrPolling","confirmsNoRuntimeShadowReplaySubmit","confirmsNoTradingMutation","confirmsNoGatewayRegistration","confirmsCredentialValuesMustRemainRedacted","confirmsFutureManualExecutionPhaseRequired")){if(-not[bool]$signoff[$a]){$issues+=$a}}}
foreach($b in @("isApprovedForExternalRun","eligibleForManualSnapshotAttempt","canRunExternalSnapshot","externalConnectionAttempted","snapshotAttempted","replayAttempted","orderSubmissionAttempted","shadowReplaySubmitAttempted","tradingMutationAttempted","schedulerStarted")){if([bool]$signoff[$b]){$issues+=$b}}
if($issues.Count){Write-Host "FinalDecision: FAIL";$issues|%{Write-Host $_};exit 1}
$outDir=Resolve-LocalPath $OutputDirectory;New-Item -ItemType Directory -Path $outDir -Force|Out-Null
$jsonPath=Join-Path $outDir "$($signoff.signoffId).json";$mdPath=Join-Path $outDir "$($signoff.signoffId).md"
if(((Test-Path $jsonPath)-or(Test-Path $mdPath))-and-not$Force){Write-Host "FinalDecision: FAIL";Write-Host "Output exists: $jsonPath";exit 1}
if($WhatIfPreview){$signoff|ConvertTo-Json -Depth 10}else{
 $signoff|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $jsonPath -Encoding UTF8
 @("# Phase 6U GBPUSD Operator Signoff","","Decision: $SignoffDecision","","SignedForPlanning is planning-only and does not authorize execution.","","- Symbol: GBPUSD / GBP/USD","- SecurityID: 4002","- IsApprovedForExternalRun: false","- eligibleForManualSnapshotAttempt: false","- canRunExternalSnapshot: false")|Set-Content -LiteralPath $mdPath -Encoding UTF8
}
Write-Host "FinalDecision: PASS";Write-Host "Symbol: $($signoff.symbol)";Write-Host "PlanningSecurityId: $($signoff.planningSecurityId)";Write-Host "SignoffDecision: $SignoffDecision";Write-Host "CanRunExternalSnapshot: false";if(-not$WhatIfPreview){Write-Host "Signoff: $jsonPath";Write-Host "SignoffSummary: $mdPath"}
