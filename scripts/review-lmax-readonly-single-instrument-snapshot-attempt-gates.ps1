param(
    [string[]]$GateFile = @(),
    [string]$InputDirectory = "artifacts/lmax-readonly-runtime-securityid-planning/attempt-gates"
)
$ErrorActionPreference="Stop";$repoRoot=Split-Path -Parent $PSScriptRoot
function Resolve-LocalPath([string]$p){if([IO.Path]::IsPathRooted($p)){$p}else{Join-Path $repoRoot $p}}
Write-Host "LMAX read-only Phase 6S attempt gate review"
Write-Host "Local-only. No LMAX connection, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."
$files=@()
if($GateFile.Count){$files=@($GateFile|%{Resolve-LocalPath $_})}else{$dir=Resolve-LocalPath $InputDirectory;if(Test-Path $dir){$files=@(Get-ChildItem -LiteralPath $dir -Filter '*.json'|% FullName)}}
$gates=@();$issues=@()
foreach($f in $files){
 if(-not(Test-Path $f)){$issues+="Missing gate file: $f";continue}
 $text=Get-Content -Raw $f
 if($text -match '(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)'){$issues+="Sensitive-shaped content in $f";continue}
 $g=$text|ConvertFrom-Json;$gates+=$g
 foreach($b in @("isApprovedForExternalRun","eligibleForManualSnapshotAttempt","canRunExternalSnapshot","externalConnectionAttempted","snapshotAttempted","replayAttempted","orderSubmissionAttempted","shadowReplaySubmitAttempted","tradingMutationAttempted","schedulerStarted")){if([bool]$g.$b){$issues+="Unsafe $b in $f"}}
 if([string]$g.gateDecision -ne "PASS"){$issues+="Non-pass gate decision in $f"}
}
$decision=if($issues.Count){"FAIL"}elseif($gates.Count -eq 0){"PASS_WITH_KNOWN_WARNINGS"}else{"PASS"}
$dir=Join-Path $repoRoot "artifacts/readiness";New-Item -ItemType Directory -Path $dir -Force|Out-Null;$path=Join-Path $dir "phase6s-attempt-gate-review.json"
[ordered]@{generatedAtUtc=[DateTimeOffset]::UtcNow.ToString("o");phase="6S";finalDecision=$decision;gateCount=$gates.Count;unsafeCount=$issues.Count;symbols=@($gates|% symbol);issues=$issues;gates=@($gates|%{[ordered]@{symbol=$_.symbol;gateDecision=$_.gateDecision;isApprovedForExternalRun=$_.isApprovedForExternalRun;eligibleForManualSnapshotAttempt=$_.eligibleForManualSnapshotAttempt;canRunExternalSnapshot=$_.canRunExternalSnapshot}})}|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $path -Encoding UTF8
Write-Host "FinalDecision: $decision";Write-Host "GateCount: $($gates.Count)";Write-Host "UnsafeCount: $($issues.Count)";Write-Host "Report: $path";if($decision -eq "FAIL"){exit 1}
