param(
    [string]$PreflightManifestFile = "artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-additional-instrument-snapshot-preflights-20260509-144924.json",
    [string]$EnvelopeDirectory = "artifacts/lmax-readonly-runtime-securityid-planning/approval-envelopes",
    [string[]]$EnvelopeFile = @()
)
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
function Resolve-LocalPath([string]$Path) { if ([IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path $repoRoot $Path } }
function Test-Sensitive([string]$Value) { $Value -match "(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)" }
function Test-AuthorizationLanguage([string]$Value) { $Value -match "(?i)(newordersingle|ordercancelrequest|ordercancelreplacerequest|tradecapture|orderstatus|order submission|submit order|external run authorized|approve external|approved for external|production|uat|execution authorized|run authorized)" }

Write-Host "LMAX read-only Phase 6Q approval envelope review"
Write-Host "Local-only. No LMAX connection, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."
$preflightPath = Resolve-LocalPath $PreflightManifestFile
$preflight = (Get-Content -Raw -LiteralPath $preflightPath | ConvertFrom-Json)
$files = if ($EnvelopeFile.Count -gt 0) { @($EnvelopeFile | ForEach-Object { Resolve-LocalPath $_ }) } else { $dir=Resolve-LocalPath $EnvelopeDirectory; if(Test-Path $dir){@(Get-ChildItem $dir -Filter *.json | Select-Object -ExpandProperty FullName)}else{@()} }
$envelopes = @($files | ForEach-Object { Get-Content -Raw -LiteralPath $_ | ConvertFrom-Json })
$issues = @()
foreach($e in $envelopes){
  $src=@($preflight.results|Where-Object{[string]$_.symbol -eq [string]$e.symbol})
  $safe=@($e.approvalEnvelopeId,$e.requestedByOperatorId,$e.reviewedByOperatorId,$e.reason,$e.symbol,$e.slashSymbol,$e.planningSecurityId,$e.securityIdSource,$e.environmentName,$e.venueProfileName,$e.requestMode,$e.symbolEncodingMode,$e.sourcePreflightDecision,$e.decision) -join " "
  if($src.Count -ne 1){$issues+="UnknownSymbol:$($e.symbol)"}
  elseif([string]$src[0].finalDecision -ne "PASS" -or [string]$e.sourcePreflightDecision -ne "PASS"){$issues+="SourcePreflightNotPass:$($e.symbol)"}
  elseif([string]$src[0].planningSecurityId -ne [string]$e.planningSecurityId){$issues+="SecurityIdMismatch:$($e.symbol)"}
  if([bool]$e.isApprovedForExternalRun -or [bool]$e.eligibleForManualSnapshotAttempt -or [bool]$e.canRunExternalSnapshot){$issues+="ExecutableFlag:$($e.symbol)"}
  if(Test-Sensitive $safe){$issues+="SensitiveContent:$($e.symbol)"}
  if(Test-AuthorizationLanguage $safe){$issues+="AuthorizationLanguage:$($e.symbol)"}
  if([string]$e.decision -eq "AcceptedForPlanning"){
    foreach($a in @("confirmsDemoOnly","confirmsReadOnlyMarketDataOnly","confirmsNoOrderSubmission","confirmsNoSchedulerOrPolling","confirmsNoRuntimeShadowReplaySubmit","confirmsNoTradingMutation","confirmsSingleInstrumentOnly","confirmsFutureExplicitManualRunRequired")){
      if(-not [bool]$e.$a){$issues+="MissingAttestation:$($e.symbol):$a"}
    }
  }
}
$accepted=@($envelopes|Where-Object{[string]$_.decision -eq "AcceptedForPlanning"})
$conflicts=@($accepted|Group-Object symbol|Where-Object{($_.Group|Select-Object -ExpandProperty planningSecurityId -Unique).Count -gt 1})
foreach($c in $conflicts){$issues+="Conflict:$($c.Name)"}
$decision=if($issues.Count -gt 0){"FAIL"}elseif($accepted.Count -gt 0){"PASS"}else{"PASS_WITH_KNOWN_WARNINGS"}
$reportDir=Join-Path $repoRoot "artifacts/readiness"; New-Item -ItemType Directory -Path $reportDir -Force|Out-Null
$reportPath=Join-Path $reportDir "phase6q-approval-envelope-review.json"
[ordered]@{generatedAtUtc=[DateTimeOffset]::UtcNow.ToString("o"); finalDecision=$decision; totalEnvelopeCount=$envelopes.Count; acceptedForPlanningCount=$accepted.Count; conflictCount=$conflicts.Count; invalidEnvelopeCount=$issues.Count; isApprovedForExternalRun=$false; eligibleForManualSnapshotAttempt=$false; canRunExternalSnapshot=$false; issues=$issues; envelopes=@($envelopes|ForEach-Object{[ordered]@{symbol=$_.symbol; decision=$_.decision; reviewedBy=$_.reviewedByOperatorId; isApprovedForExternalRun=$_.isApprovedForExternalRun; canRunExternalSnapshot=$_.canRunExternalSnapshot}})}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Host "FinalDecision: $decision"
Write-Host "TotalEnvelopeCount: $($envelopes.Count)"
Write-Host "AcceptedForPlanningCount: $($accepted.Count)"
Write-Host "ConflictCount: $($conflicts.Count)"
Write-Host "InvalidEnvelopeCount: $($issues.Count)"
Write-Host "Report: $reportPath"
if($decision -eq "FAIL"){exit 1}
