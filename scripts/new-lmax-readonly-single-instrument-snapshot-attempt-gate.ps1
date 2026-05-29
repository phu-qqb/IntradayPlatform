param(
    [Parameter(Mandatory=$true)][string]$PlanningManifestFile,
    [Parameter(Mandatory=$true)][string]$SafetyGateManifestFile,
    [Parameter(Mandatory=$true)][string]$PreflightManifestFile,
    [Parameter(Mandatory=$true)][string]$ApprovalEnvelopeFile,
    [Parameter(Mandatory=$true)][string]$DryRunReportFile,
    [Parameter(Mandatory=$true)][string]$RequestedByOperatorId,
    [Parameter(Mandatory=$true)][string]$Reason,
    [string]$OutputDirectory="artifacts/lmax-readonly-runtime-securityid-planning/attempt-gates",
    [switch]$WhatIfPreview,
    [switch]$Force
)
$ErrorActionPreference="Stop";$repoRoot=Split-Path -Parent $PSScriptRoot
function Resolve-LocalPath([string]$p){if([IO.Path]::IsPathRooted($p)){$p}else{Join-Path $repoRoot $p}}
function Bad($v){$v -match "(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)"}
function Auth($v){$v -match "(?i)(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)"}
Write-Host "LMAX read-only Phase 6S single-instrument snapshot attempt gate"
Write-Host "Local-only. No LMAX connection, no external API, no SecurityListRequest, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."
$pp=Resolve-LocalPath $PlanningManifestFile;$sp=Resolve-LocalPath $SafetyGateManifestFile;$fp=Resolve-LocalPath $PreflightManifestFile;$ap=Resolve-LocalPath $ApprovalEnvelopeFile;$dp=Resolve-LocalPath $DryRunReportFile
foreach($p in @($pp,$sp,$fp,$ap,$dp)){if(-not(Test-Path $p)){Write-Host "FinalDecision: FAIL";Write-Host "Missing input: $p";exit 1}}
$planning=Get-Content -Raw $pp|ConvertFrom-Json;$safety=Get-Content -Raw $sp|ConvertFrom-Json;$preflight=Get-Content -Raw $fp|ConvertFrom-Json;$approval=Get-Content -Raw $ap|ConvertFrom-Json;$dryrun=Get-Content -Raw $dp|ConvertFrom-Json
$raw = (Get-Content -Raw $ap)+" "+(Get-Content -Raw $dp)+" "+$RequestedByOperatorId+" "+$Reason
if(Bad $raw){Write-Host "FinalDecision: FAIL";Write-Host "Sensitive-shaped content found.";exit 1}
if(Auth ($RequestedByOperatorId+" "+$Reason)){Write-Host "FinalDecision: FAIL";Write-Host "Authorization language found.";exit 1}
$symbol=[string]$dryrun.symbol
if($symbol -ne [string]$approval.symbol){Write-Host "FinalDecision: FAIL";Write-Host "Approval/dry-run symbol mismatch.";exit 1}
$plan=@($planning.instruments|? symbol -eq $symbol)[0];$safe=@($safety.instruments|? symbol -eq $symbol)[0];$pre=@($preflight.results|? symbol -eq $symbol)[0]
$stamp=[DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$gate=[ordered]@{
 gateId="lmax-readonly-single-instrument-snapshot-attempt-gate-$symbol-$stamp"; createdAtUtc=[DateTimeOffset]::UtcNow.ToString("o"); requestedByOperatorId=$RequestedByOperatorId; reason=$Reason
 symbol=$symbol; slashSymbol=[string]$dryrun.slashSymbol; planningSecurityId=[string]$dryrun.planningSecurityId; securityIdSource="8"; environmentName=[string]$dryrun.environmentName; venueProfileName=[string]$dryrun.venueProfileName
 requestMode=[string]$dryrun.requestMode; symbolEncodingMode=[string]$dryrun.symbolEncodingMode; marketDepth=[int]$dryrun.marketDepth
 sourcePlanningManifestPath=$pp; sourceSafetyGateManifestPath=$sp; sourcePreflightManifestPath=$fp; sourceApprovalEnvelopePath=$ap; sourceDryRunReportPath=$dp
 planningDecision=[string]$plan.decision; safetyGateDecision=[string]$safe.finalDecision; preflightDecision=[string]$pre.finalDecision; approvalEnvelopeDecision=[string]$approval.decision; dryRunDecision=[string]$dryrun.dryRunDecision; gateDecision="PASS"
 isApprovedForExternalRun=$false; eligibleForManualSnapshotAttempt=$false; canRunExternalSnapshot=$false; externalConnectionAttempted=$false; snapshotAttempted=$false; replayAttempted=$false; orderSubmissionAttempted=$false; shadowReplaySubmitAttempted=$false; tradingMutationAttempted=$false; schedulerStarted=$false; noSensitiveContent=$true
 requiredFutureStep="explicit future operator-approved manual execution phase"; blockingReason="Phase 6S is a gate only; external snapshot not authorized."
}
$issues=@()
if($symbol -ne "GBPUSD"){$issues+="WrongSymbol"};if($gate.planningSecurityId -ne "4002"){$issues+="WrongSecurityID"};if($gate.securityIdSource -ne "8"){$issues+="WrongSecurityIDSource"}
if($gate.slashSymbol -ne "GBP/USD"){$issues+="WrongSlashSymbol"};if($gate.environmentName -ne "Demo"){$issues+="WrongEnvironment"};if($gate.venueProfileName -ne "DemoLondon"){$issues+="WrongVenueProfile"}
if($gate.requestMode -ne "SnapshotPlusUpdates"){$issues+="WrongRequestMode"};if($gate.symbolEncodingMode -ne "SecurityIdOnly"){$issues+="WrongSymbolEncoding"};if($gate.marketDepth -ne 1){$issues+="WrongMarketDepth"}
foreach($x in @("safetyGateDecision","preflightDecision","dryRunDecision")){if($gate[$x] -ne "PASS"){$issues+=$x}}
if($gate.approvalEnvelopeDecision -ne "AcceptedForPlanning"){$issues+="ApprovalNotAccepted"};if($gate.planningDecision -ne "AcceptedForPlanning"){$issues+="PlanningNotAccepted"}
foreach($b in @("isApprovedForExternalRun","eligibleForManualSnapshotAttempt","canRunExternalSnapshot","externalConnectionAttempted","snapshotAttempted","replayAttempted","orderSubmissionAttempted","shadowReplaySubmitAttempted","tradingMutationAttempted","schedulerStarted")){if([bool]$gate[$b]){$issues+=$b}}
if($issues.Count){Write-Host "FinalDecision: FAIL";$issues|%{Write-Host $_};exit 1}
$outDir=Resolve-LocalPath $OutputDirectory;New-Item -ItemType Directory -Path $outDir -Force|Out-Null;$out=Join-Path $outDir "$($gate.gateId).json"
if((Test-Path $out)-and-not$Force){Write-Host "FinalDecision: FAIL";Write-Host "Output exists: $out";exit 1}
if($WhatIfPreview){$gate|ConvertTo-Json -Depth 10}else{$gate|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $out -Encoding UTF8}
Write-Host "FinalDecision: PASS";Write-Host "Symbol: $symbol";Write-Host "PlanningSecurityId: $($gate.planningSecurityId)";Write-Host "GateDecision: PASS";Write-Host "CanRunExternalSnapshot: false";if(-not$WhatIfPreview){Write-Host "AttemptGate: $out"}
