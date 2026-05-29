param(
    [string]$PreflightManifestFile = "artifacts/lmax-readonly-runtime-securityid-planning/lmax-readonly-additional-instrument-snapshot-preflights-20260509-144924.json",
    [string[]]$ApprovalEnvelopeFile = @()
)
$ErrorActionPreference="Stop"
$repoRoot=Split-Path -Parent $PSScriptRoot
$results=@()
function Add-Result($Category,$Check,$Status,$Detail){$script:results+=[ordered]@{category=$Category;check=$Check;status=$Status;detail=$Detail};Write-Host ("{0}: {1} / {2} - {3}" -f $Status,$Category,$Check,$Detail)}
function Resolve-LocalPath([string]$Path){if([string]::IsNullOrWhiteSpace($Path)){return $Path};if([IO.Path]::IsPathRooted($Path)){$Path}else{Join-Path $repoRoot $Path}}
function Get-TextHit([string[]]$Path,[string[]]$Pattern){$existing=@($Path|Where-Object{Test-Path -LiteralPath $_});if($existing.Count-eq 0){return @()};@(Select-String -Path $existing -Pattern $Pattern -SimpleMatch -ErrorAction SilentlyContinue)}
Write-Host "LMAX Read-Only Runtime Phase 6Q Approval Envelope Gate"
Write-Host "Local-only gate. No LMAX connection, no SecurityListRequest, no snapshots, no replay, no credentials, no orders, and no mutation."
$model=Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelope.cs"
$newScript=Join-Path $PSScriptRoot "new-lmax-readonly-additional-instrument-snapshot-approval-envelope.ps1"
$reviewScript=Join-Path $PSScriptRoot "review-lmax-readonly-additional-instrument-snapshot-approval-envelopes.ps1"
$test=Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyAdditionalInstrumentSnapshotApprovalEnvelopeTests.cs"
foreach($i in @(@{n="Approval model";p=$model},@{n="Creation script";p=$newScript},@{n="Review script";p=$reviewScript},@{n="Approval tests";p=$test})){if(Test-Path $i.p){Add-Result "Files" "$($i.n) exists" "PASS" $i.p}else{Add-Result "Files" "$($i.n) exists" "FAIL" "Missing $($i.p)"}}
$preflightPath=Resolve-LocalPath $PreflightManifestFile
if(Test-Path $preflightPath){$preflight=Get-Content -Raw -LiteralPath $preflightPath|ConvertFrom-Json; if([string]$preflight.finalDecision -eq "PASS" -and -not [bool]$preflight.anyCanRunExternalSnapshot){Add-Result "Preflight" "Source preflight safe" "PASS" "PASS and non-executable."}else{Add-Result "Preflight" "Source preflight safe" "FAIL" "Expected PASS/non-executable."}}else{Add-Result "Preflight" "Source preflight exists" "FAIL" "Missing $preflightPath"}
if($ApprovalEnvelopeFile.Count -eq 0){
  Add-Result "Envelopes" "Approval envelope supplied" "WARN" "No envelope supplied; source checks only."
}else{
  foreach($f in $ApprovalEnvelopeFile){
    $path=Resolve-LocalPath $f
    if(-not(Test-Path $path)){Add-Result "Envelopes" "Envelope exists" "FAIL" "Missing $path";continue}
    $text=Get-Content -Raw -LiteralPath $path
    if($text -match '(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)'){Add-Result "Envelopes" "No sensitive content" "FAIL" "Sensitive-shaped content found."}else{Add-Result "Envelopes" "No sensitive content" "PASS" "No credential-shaped content found."}
    $e=$text|ConvertFrom-Json
    if([bool]$e.isApprovedForExternalRun -or [bool]$e.eligibleForManualSnapshotAttempt -or [bool]$e.canRunExternalSnapshot){Add-Result "Envelopes" "$($e.symbol) non-executable flags" "FAIL" "Executable flag true."}else{Add-Result "Envelopes" "$($e.symbol) non-executable flags" "PASS" "All run flags false."}
    if([string]$e.decision -eq "AcceptedForPlanning"){Add-Result "Envelopes" "$($e.symbol) accepted planning envelope" "PASS" "AcceptedForPlanning is planning-only."}
  }
}
$apiProgram=Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs";$workerProgram=Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs";$apiWorker=@($apiProgram,$workerProgram)
$registrationHits=Get-TextHit $apiWorker @("RealLmaxGateway","ExternalReadOnlyPrototypeGateway","LmaxVenueGatewaySkeleton","SecurityListRequest")
if($registrationHits.Count -eq 0 -and (Select-String -Path $apiProgram -Pattern "FakeLmaxGateway" -SimpleMatch -Quiet)){Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No SecurityList or real gateway registration found."}else{Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" (($registrationHits|%{"$($_.Path):$($_.LineNumber)"})-join"; ")}
foreach($scan in @(@{c="Scheduler";k="No scheduler/polling added";p=@("PeriodicTimer","System.Threading.Timer","SecurityListPoll")},@{c="Replay";k="Runtime does not submit to shadow replay";p=@("SubmitToShadowReplay = true","SubmittedToShadowReplay = true","ReplaySubmitAsync")},@{c="Orders";k="No order surface";p=@("NewOrderSingle","OrderCancelRequest","OrderCancelReplaceRequest","SubmitOrder")},@{c="Mutation";k="No trading-state mutation references";p=@("PersistTrade","TradingState")})){ $hits=Get-TextHit $apiWorker $scan.p; if($hits.Count-eq 0){Add-Result $scan.c $scan.k "PASS" "No marker found in API/Worker startup."}else{Add-Result $scan.c $scan.k "FAIL" (($hits|%{"$($_.Path):$($_.LineNumber)"})-join"; ")}}
Add-Result "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add-Result "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add-Result "Replay" "Manual replay" "PASS" "This gate does not replay evidence."
Add-Result "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."
$failed=@($results|? status -eq "FAIL");$warnings=@($results|? status -eq "WARN");$decision=if($failed.Count){"FAIL"}elseif($warnings.Count){"PASS_WITH_KNOWN_WARNINGS"}else{"PASS"}
$reportDir=Join-Path $repoRoot "artifacts/readiness";New-Item -ItemType Directory -Path $reportDir -Force|Out-Null
$reportPath=Join-Path $reportDir "phase6q-approval-envelope-gate.json"
[ordered]@{generatedAtUtc=[DateTimeOffset]::UtcNow.ToString("o");finalDecision=$decision;phase="6Q";isApprovedForExternalRun=$false;eligibleForManualSnapshotAttempt=$false;canRunExternalSnapshot=$false;externalConnectionAttempted=$false;marketDataSnapshotAttempted=$false;replayAttempted=$false;runtimeShadowReplaySubmit=$false;schedulerOrPollingAdded=$false;orderSubmissionAdded=$false;gatewayRegistrationAdded=$false;tradingMutationAdded=$false;results=$results}|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Host "";Write-Host "FinalDecision: $decision";Write-Host "Report: $reportPath";if($decision-eq"FAIL"){exit 1}
