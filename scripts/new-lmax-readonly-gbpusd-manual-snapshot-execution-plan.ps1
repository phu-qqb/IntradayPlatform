param(
    [Parameter(Mandatory=$true)][string]$AttemptGateFile,
    [Parameter(Mandatory=$true)][string]$RequestedByOperatorId,
    [Parameter(Mandatory=$true)][string]$Reason,
    [string]$OutputDirectory="artifacts/lmax-readonly-runtime-securityid-planning/execution-plans",
    [switch]$WhatIfPreview,
    [switch]$Force
)
$ErrorActionPreference="Stop";$repoRoot=Split-Path -Parent $PSScriptRoot
function Resolve-LocalPath([string]$p){if([IO.Path]::IsPathRooted($p)){$p}else{Join-Path $repoRoot $p}}
function Bad($v){$v -match "(?i)(password|secret|token|apikey|api_key|privatekey|private_key|bearer|\b553=|\b554=|host=|user=|account)"}
function Auth($v){$v -match "(?i)(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized|currently authorized|is authorized)"}
Write-Host "LMAX read-only Phase 6T GBPUSD manual snapshot execution plan / kill-rollback plan"
Write-Host "Local-only. No LMAX connection, no external API, no SecurityListRequest, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."
$gp=Resolve-LocalPath $AttemptGateFile
if(-not(Test-Path $gp)){Write-Host "FinalDecision: FAIL";Write-Host "Missing attempt gate: $gp";exit 1}
$gateText=Get-Content -Raw $gp;$gate=$gateText|ConvertFrom-Json
if(Bad ($gateText+" "+$RequestedByOperatorId+" "+$Reason)){Write-Host "FinalDecision: FAIL";Write-Host "Sensitive-shaped content found.";exit 1}
if(Auth ($RequestedByOperatorId+" "+$Reason)){Write-Host "FinalDecision: FAIL";Write-Host "Authorization language found.";exit 1}
$symbol=[string]$gate.symbol;$stamp=[DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$futureCommand=@"
DO NOT RUN IN PHASE 6T. Future command template only:
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-lmax-readonly-runtime-demo-snapshot-prototype.ps1 `
  -AllowExternalConnections `
  -ConfirmDemoReadOnly `
  -Reason "<future explicit operator signoff reason>" `
  -Symbol GBPUSD `
  -SecurityId 4002 `
  -SecurityIdSource 8 `
  -RequestMode SnapshotPlusUpdates `
  -SymbolEncodingMode SecurityIdOnly `
  -MarketDepth 1
"@
$plan=[ordered]@{
 planId="lmax-readonly-gbpusd-manual-snapshot-execution-plan-$stamp"; createdAtUtc=[DateTimeOffset]::UtcNow.ToString("o"); requestedByOperatorId=$RequestedByOperatorId; reason=$Reason
 symbol=$symbol; slashSymbol=[string]$gate.slashSymbol; planningSecurityId=[string]$gate.planningSecurityId; securityIdSource=[string]$gate.securityIdSource; environmentName=[string]$gate.environmentName; venueProfileName=[string]$gate.venueProfileName
 requestMode=[string]$gate.requestMode; symbolEncodingMode=[string]$gate.symbolEncodingMode; marketDepth=[int]$gate.marketDepth; sourceAttemptGatePath=$gp; attemptGateDecision=[string]$gate.gateDecision
 futureCommandTemplate=$futureCommand
 externalRunAuthorized=$false; canRunExternalSnapshot=$false; eligibleForManualSnapshotAttempt=$false; isApprovedForExternalRun=$false; schedulerOrPolling=$false; runtimeShadowReplaySubmit=$false; orderSubmission=$false; tradingMutation=$false; apiWorkerGatewayMode="FakeLmaxGateway"; noSensitiveContent=$true
 abortCriteria=@("Wrong symbol or SecurityID","Any order flag is true","Scheduler or polling is detected","Runtime shadow replay submit is true","Credential exposure is detected","Unknown failure classification occurs","Environment is not Demo","Gateway registration changes","Mutation guard changes","Multi-instrument batch is attempted")
 rollbackSteps=@("Stop the manual process","Clear shell variables if needed","Verify API health still reports FakeLmaxGateway","Run the Phase 6S gate again","Inspect artifacts for noSensitiveContent","Confirm no DB rollback is expected because mutation is prohibited")
 postRunValidationSteps=@("Run the artifact validator","Generate evidence preview mapping","Run optional manual replay only in an explicitly approved later phase","Confirm no observations or mutation guard changes","Complete operator review","Confirm no credential values in artifacts")
 decision="PASS"
}
$issues=@()
if($symbol -ne "GBPUSD"){$issues+="WrongSymbol"};if($plan.planningSecurityId -ne "4002"){$issues+="WrongSecurityID"};if($plan.securityIdSource -ne "8"){$issues+="WrongSecurityIDSource"}
if($plan.attemptGateDecision -ne "PASS"){$issues+="AttemptGateNotPass"};if($plan.futureCommandTemplate -notmatch "DO NOT RUN IN PHASE 6T"){$issues+="CommandTemplateNotMarked"}
foreach($b in @("externalRunAuthorized","canRunExternalSnapshot","eligibleForManualSnapshotAttempt","isApprovedForExternalRun","schedulerOrPolling","runtimeShadowReplaySubmit","orderSubmission","tradingMutation")){if([bool]$plan[$b]){$issues+=$b}}
if($issues.Count){Write-Host "FinalDecision: FAIL";$issues|%{Write-Host $_};exit 1}
$outDir=Resolve-LocalPath $OutputDirectory;New-Item -ItemType Directory -Path $outDir -Force|Out-Null
$jsonPath=Join-Path $outDir "$($plan.planId).json";$mdPath=Join-Path $outDir "$($plan.planId).md"
if(((Test-Path $jsonPath)-or(Test-Path $mdPath))-and-not$Force){Write-Host "FinalDecision: FAIL";Write-Host "Output exists: $jsonPath";exit 1}
if($WhatIfPreview){$plan|ConvertTo-Json -Depth 10}else{
 $plan|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $jsonPath -Encoding UTF8
 @(
 "# Phase 6T GBPUSD Manual Snapshot Execution Plan",
 "",
 "Decision: PASS",
 "",
 "This is a planning-only summary. DO NOT RUN IN PHASE 6T.",
 "",
 "- Symbol: GBPUSD / GBP/USD",
 "- SecurityID: 4002",
 "- SecurityIDSource: 8",
 "- External run authorized: false",
 "- canRunExternalSnapshot: false",
 "- eligibleForManualSnapshotAttempt: false",
 "- IsApprovedForExternalRun: false"
 )|Set-Content -LiteralPath $mdPath -Encoding UTF8
}
Write-Host "FinalDecision: PASS";Write-Host "Symbol: $symbol";Write-Host "PlanningSecurityId: $($plan.planningSecurityId)";Write-Host "PlanDecision: PASS";Write-Host "CanRunExternalSnapshot: false";if(-not$WhatIfPreview){Write-Host "ExecutionPlan: $jsonPath";Write-Host "ExecutionPlanSummary: $mdPath"}
