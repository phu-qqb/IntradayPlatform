param(
    [Parameter(Mandatory=$true)][string]$PlanningManifestFile,
    [Parameter(Mandatory=$true)][string]$SafetyGateManifestFile,
    [Parameter(Mandatory=$true)][string]$PreflightManifestFile,
    [Parameter(Mandatory=$true)][string]$ApprovalEnvelopeFile,
    [Parameter(Mandatory=$true)][string]$RequestedByOperatorId,
    [Parameter(Mandatory=$true)][string]$Reason,
    [string]$OutputDirectory="artifacts/lmax-readonly-runtime-securityid-planning/dry-run-reports",
    [switch]$WhatIfPreview,
    [switch]$Force
)
$ErrorActionPreference="Stop";$repoRoot=Split-Path -Parent $PSScriptRoot
function Resolve-LocalPath([string]$p){if([IO.Path]::IsPathRooted($p)){$p}else{Join-Path $repoRoot $p}}
function Bad($v){$v -match "(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)"}
function Auth($v){$v -match "(?i)(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)"}
Write-Host "LMAX read-only Phase 6R single-instrument dry-run report"
Write-Host "Local-only. No LMAX connection, no external API, no SecurityListRequest, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."
$pp=Resolve-LocalPath $PlanningManifestFile;$sp=Resolve-LocalPath $SafetyGateManifestFile;$fp=Resolve-LocalPath $PreflightManifestFile;$ap=Resolve-LocalPath $ApprovalEnvelopeFile
foreach($p in @($pp,$sp,$fp,$ap)){if(-not(Test-Path $p)){Write-Host "FinalDecision: FAIL";Write-Host "Missing input: $p";exit 1}}
$planning=Get-Content -Raw $pp|ConvertFrom-Json;$safety=Get-Content -Raw $sp|ConvertFrom-Json;$preflight=Get-Content -Raw $fp|ConvertFrom-Json;$approval=Get-Content -Raw $ap|ConvertFrom-Json
if(Bad ((Get-Content -Raw $ap)+" "+$RequestedByOperatorId+" "+$Reason)){Write-Host "FinalDecision: FAIL";Write-Host "Sensitive-shaped content found.";exit 1}
if(Auth ($RequestedByOperatorId+" "+$Reason)){Write-Host "FinalDecision: FAIL";Write-Host "Authorization language found.";exit 1}
$symbol=[string]$approval.symbol
$plan=@($planning.instruments|? symbol -eq $symbol)[0];$safe=@($safety.instruments|? symbol -eq $symbol)[0];$pre=@($preflight.results|? symbol -eq $symbol)[0];$req=@($preflight.requests|? symbol -eq $symbol)[0]
$stamp=[DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$report=[ordered]@{
 dryRunReportId="lmax-readonly-additional-snapshot-dryrun-$symbol-$stamp"; createdAtUtc=[DateTimeOffset]::UtcNow.ToString("o"); requestedByOperatorId=$RequestedByOperatorId; reason=$Reason
 symbol=$symbol; slashSymbol=[string]$approval.slashSymbol; planningSecurityId=[string]$approval.planningSecurityId; securityIdSource="8"; environmentName=[string]$approval.environmentName; venueProfileName=[string]$approval.venueProfileName
 requestMode=[string]$approval.requestMode; symbolEncodingMode=[string]$approval.symbolEncodingMode; marketDepth=[int]$approval.marketDepth; maxRuntimeSeconds=[int]$approval.maxRuntimeSeconds; maxWaitSeconds=[int]$approval.maxWaitSeconds; maxEventsPerRun=[int]$approval.maxEventsPerRun
 sourcePlanningManifestPath=$pp; sourceSafetyGateManifestPath=$sp; sourcePreflightManifestPath=$fp; sourceApprovalEnvelopePath=$ap
 planningDecision=[string]$plan.decision; safetyGateDecision=[string]$safe.finalDecision; preflightDecision=[string]$pre.finalDecision; approvalEnvelopeDecision=[string]$approval.decision; dryRunDecision="PASS"
 isApprovedForExternalRun=$false; eligibleForManualSnapshotAttempt=$false; canRunExternalSnapshot=$false; externalConnectionAttempted=$false; snapshotAttempted=$false; replayAttempted=$false; orderSubmissionAttempted=$false; shadowReplaySubmitAttempted=$false; tradingMutationAttempted=$false; schedulerStarted=$false; noSensitiveContent=$true
 requiredFutureStep="explicit future operator-approved manual run phase"; blockingReason="Phase 6R is dry-run only; external snapshot not authorized."
}
$issues=@()
if($symbol -ne "GBPUSD"){$issues+="WrongSymbol"};if($report.planningSecurityId -ne "4002"){$issues+="WrongSecurityID"};if($report.securityIdSource -ne "8"){$issues+="WrongSecurityIDSource"}
foreach($x in @("safetyGateDecision","preflightDecision")){if($report[$x] -ne "PASS"){$issues+=$x}}
if($report.approvalEnvelopeDecision -ne "AcceptedForPlanning"){$issues+="ApprovalNotAccepted"}
foreach($b in @("isApprovedForExternalRun","eligibleForManualSnapshotAttempt","canRunExternalSnapshot","externalConnectionAttempted","snapshotAttempted","replayAttempted","orderSubmissionAttempted","shadowReplaySubmitAttempted","tradingMutationAttempted","schedulerStarted")){if([bool]$report[$b]){$issues+=$b}}
if($issues.Count){Write-Host "FinalDecision: FAIL";$issues|%{Write-Host $_};exit 1}
$outDir=Resolve-LocalPath $OutputDirectory;New-Item -ItemType Directory -Path $outDir -Force|Out-Null;$out=Join-Path $outDir "$($report.dryRunReportId).json"
if((Test-Path $out)-and-not$Force){Write-Host "FinalDecision: FAIL";Write-Host "Output exists: $out";exit 1}
if($WhatIfPreview){$report|ConvertTo-Json -Depth 10}else{$report|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $out -Encoding UTF8}
Write-Host "FinalDecision: PASS";Write-Host "Symbol: $symbol";Write-Host "PlanningSecurityId: $($report.planningSecurityId)";Write-Host "DryRunDecision: PASS";Write-Host "CanRunExternalSnapshot: false";if(-not$WhatIfPreview){Write-Host "DryRunReport: $out"}
