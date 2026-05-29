param(
    [Parameter(Mandatory=$true)][string]$PlanningManifestFile,
    [Parameter(Mandatory=$true)][string]$SafetyGateManifestFile,
    [Parameter(Mandatory=$true)][string]$PreflightManifestFile,
    [Parameter(Mandatory=$true)][string]$ApprovalEnvelopeFile,
    [Parameter(Mandatory=$true)][string]$DryRunReportFile,
    [Parameter(Mandatory=$true)][string]$AttemptGateFile,
    [Parameter(Mandatory=$true)][string]$ExecutionPlanFile,
    [Parameter(Mandatory=$true)][string]$OperatorSignoffFile,
    [string]$Phase6TGateReportFile = "",
    [string]$Phase6UGateReportFile = "",
    [Parameter(Mandatory=$true)][string]$RequestedByOperatorId,
    [Parameter(Mandatory=$true)][string]$Reason,
    [string]$OutputDirectory="artifacts/lmax-readonly-runtime-securityid-planning/final-readiness",
    [switch]$WhatIfPreview,
    [switch]$Force
)
$ErrorActionPreference="Stop";$repoRoot=Split-Path -Parent $PSScriptRoot
function Resolve-LocalPath([string]$p){if([string]::IsNullOrWhiteSpace($p)){return $p};if([IO.Path]::IsPathRooted($p)){$p}else{Join-Path $repoRoot $p}}
function Bad($v){$v -match "(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)"}
function Auth($v){$v -match "(?i)(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized|currently authorized|is authorized|authorizes execution)"}
Write-Host "LMAX read-only Phase 6V GBPUSD final readiness"
Write-Host "Local-only. No LMAX connection, no external API, no SecurityListRequest, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."
$paths=@{}
foreach($k in @("PlanningManifestFile","SafetyGateManifestFile","PreflightManifestFile","ApprovalEnvelopeFile","DryRunReportFile","AttemptGateFile","ExecutionPlanFile","OperatorSignoffFile","Phase6TGateReportFile","Phase6UGateReportFile")){
 $v=(Get-Variable $k -ValueOnly); $paths[$k]=Resolve-LocalPath $v
 if(-not[string]::IsNullOrWhiteSpace($paths[$k]) -and -not(Test-Path $paths[$k])){Write-Host "FinalDecision: FAIL";Write-Host "Missing input: $($paths[$k])";exit 1}
}
$planning=Get-Content -Raw $paths.PlanningManifestFile|ConvertFrom-Json;$safety=Get-Content -Raw $paths.SafetyGateManifestFile|ConvertFrom-Json;$preflight=Get-Content -Raw $paths.PreflightManifestFile|ConvertFrom-Json;$approval=Get-Content -Raw $paths.ApprovalEnvelopeFile|ConvertFrom-Json;$dryrun=Get-Content -Raw $paths.DryRunReportFile|ConvertFrom-Json;$attempt=Get-Content -Raw $paths.AttemptGateFile|ConvertFrom-Json;$plan=Get-Content -Raw $paths.ExecutionPlanFile|ConvertFrom-Json;$signoff=Get-Content -Raw $paths.OperatorSignoffFile|ConvertFrom-Json
$tGateDecision="";if($paths.Phase6TGateReportFile){$tGateDecision=[string](Get-Content -Raw $paths.Phase6TGateReportFile|ConvertFrom-Json).finalDecision}
$uGateDecision="";if($paths.Phase6UGateReportFile){$uGateDecision=[string](Get-Content -Raw $paths.Phase6UGateReportFile|ConvertFrom-Json).finalDecision}
$raw = (Get-Content -Raw $paths.OperatorSignoffFile)+" "+(Get-Content -Raw $paths.ExecutionPlanFile)+" "+$RequestedByOperatorId+" "+$Reason
if(Bad $raw){Write-Host "FinalDecision: FAIL";Write-Host "Sensitive-shaped content found.";exit 1}
if(Auth ($RequestedByOperatorId+" "+$Reason)){Write-Host "FinalDecision: FAIL";Write-Host "Authorization language found.";exit 1}
$symbol=[string]$signoff.symbol;$planEntry=@($planning.instruments|? symbol -eq $symbol)[0];$safe=@($safety.instruments|? symbol -eq $symbol)[0];$pre=@($preflight.results|? symbol -eq $symbol)[0]
$stamp=[DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$ready=[ordered]@{
 readinessId="lmax-readonly-gbpusd-manual-snapshot-final-readiness-$stamp"; createdAtUtc=[DateTimeOffset]::UtcNow.ToString("o"); requestedByOperatorId=$RequestedByOperatorId; reason=$Reason
 symbol=$symbol; slashSymbol=[string]$signoff.slashSymbol; planningSecurityId=[string]$signoff.planningSecurityId; securityIdSource=[string]$signoff.securityIdSource; environmentName=[string]$signoff.environmentName; venueProfileName=[string]$signoff.venueProfileName; requestMode=[string]$signoff.requestMode; symbolEncodingMode=[string]$signoff.symbolEncodingMode; marketDepth=[int]$signoff.marketDepth
 sourcePlanningManifestPath=$paths.PlanningManifestFile; sourceSafetyGateManifestPath=$paths.SafetyGateManifestFile; sourcePreflightManifestPath=$paths.PreflightManifestFile; sourceApprovalEnvelopePath=$paths.ApprovalEnvelopeFile; sourceDryRunReportPath=$paths.DryRunReportFile; sourceAttemptGatePath=$paths.AttemptGateFile; sourceExecutionPlanPath=$paths.ExecutionPlanFile; sourceOperatorSignoffPath=$paths.OperatorSignoffFile; sourcePhase6TGatePath=$paths.Phase6TGateReportFile; sourcePhase6UGatePath=$paths.Phase6UGateReportFile
 planningDecision=[string]$planEntry.decision; safetyGateDecision=[string]$safe.finalDecision; preflightDecision=[string]$pre.finalDecision; approvalEnvelopeDecision=[string]$approval.decision; dryRunDecision=[string]$dryrun.dryRunDecision; attemptGateDecision=[string]$attempt.gateDecision; executionPlanDecision=[string]$plan.decision; operatorSignoffDecision=[string]$signoff.signoffDecision; readinessDecision="PASS"
 isApprovedForExternalRun=$false; eligibleForManualSnapshotAttempt=$false; canRunExternalSnapshot=$false; externalConnectionAttempted=$false; snapshotAttempted=$false; replayAttempted=$false; orderSubmissionAttempted=$false; shadowReplaySubmitAttempted=$false; tradingMutationAttempted=$false; schedulerStarted=$false; runtimeShadowReplaySubmit=$false; apiWorkerGatewayMode="FakeLmaxGateway"; noSensitiveContent=$true
 requiredFutureStep="Phase 6W operator-approved manual GBPUSD snapshot attempt"; blockingReason="Phase 6V is final readiness only; external snapshot not authorized."
}
$issues=@()
if($symbol -ne "GBPUSD"){$issues+="WrongSymbol"};if($ready.planningSecurityId -ne "4002"){$issues+="WrongSecurityID"};if($ready.securityIdSource -ne "8"){$issues+="WrongSecurityIDSource"}
foreach($x in @("safetyGateDecision","preflightDecision","dryRunDecision","attemptGateDecision","executionPlanDecision")){if($ready[$x] -ne "PASS"){$issues+=$x}}
if($ready.planningDecision -ne "AcceptedForPlanning"){$issues+="PlanningNotAccepted"};if($ready.approvalEnvelopeDecision -ne "AcceptedForPlanning"){$issues+="ApprovalNotAccepted"};if($ready.operatorSignoffDecision -ne "SignedForPlanning"){$issues+="SignoffNotSignedForPlanning"}
if($paths.Phase6TGateReportFile -and $tGateDecision -ne "PASS"){$issues+="Phase6TGateNotPass"};if($paths.Phase6UGateReportFile -and $uGateDecision -ne "PASS"){$issues+="Phase6UGateNotPass"}
foreach($b in @("isApprovedForExternalRun","eligibleForManualSnapshotAttempt","canRunExternalSnapshot","externalConnectionAttempted","snapshotAttempted","replayAttempted","orderSubmissionAttempted","shadowReplaySubmitAttempted","tradingMutationAttempted","schedulerStarted","runtimeShadowReplaySubmit")){if([bool]$ready[$b]){$issues+=$b}}
if($issues.Count){Write-Host "FinalDecision: FAIL";$issues|%{Write-Host $_};exit 1}
$outDir=Resolve-LocalPath $OutputDirectory;New-Item -ItemType Directory -Path $outDir -Force|Out-Null
$jsonPath=Join-Path $outDir "$($ready.readinessId).json";$mdPath=Join-Path $outDir "$($ready.readinessId).md"
if(((Test-Path $jsonPath)-or(Test-Path $mdPath))-and-not$Force){Write-Host "FinalDecision: FAIL";Write-Host "Output exists: $jsonPath";exit 1}
if($WhatIfPreview){$ready|ConvertTo-Json -Depth 10}else{
 $ready|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $jsonPath -Encoding UTF8
 @("# Phase 6V GBPUSD Final Readiness","","Decision: PASS","","PASS means pre-execution readiness only. It does not authorize execution.","","- Symbol: GBPUSD / GBP/USD","- SecurityID: 4002","- IsApprovedForExternalRun: false","- eligibleForManualSnapshotAttempt: false","- canRunExternalSnapshot: false")|Set-Content -LiteralPath $mdPath -Encoding UTF8
}
Write-Host "FinalDecision: PASS";Write-Host "Symbol: $symbol";Write-Host "PlanningSecurityId: $($ready.planningSecurityId)";Write-Host "ReadinessDecision: PASS";Write-Host "CanRunExternalSnapshot: false";if(-not$WhatIfPreview){Write-Host "FinalReadiness: $jsonPath";Write-Host "FinalReadinessSummary: $mdPath"}
