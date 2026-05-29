param(
    [string[]]$SignoffFile = @(),
    [string]$InputDirectory = "artifacts/lmax-readonly-runtime-securityid-planning/operator-signoffs"
)
$ErrorActionPreference="Stop";$repoRoot=Split-Path -Parent $PSScriptRoot
function Resolve-LocalPath([string]$p){if([IO.Path]::IsPathRooted($p)){$p}else{Join-Path $repoRoot $p}}
Write-Host "LMAX read-only Phase 6U operator signoff review"
Write-Host "Local-only. No LMAX connection, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."
$files=@();if($SignoffFile.Count){$files=@($SignoffFile|%{Resolve-LocalPath $_})}else{$dir=Resolve-LocalPath $InputDirectory;if(Test-Path $dir){$files=@(Get-ChildItem -LiteralPath $dir -Filter '*.json'|% FullName)}}
$items=@();$issues=@()
foreach($f in $files){if(-not(Test-Path $f)){$issues+="Missing signoff file: $f";continue};$text=Get-Content -Raw $f;if($text -match '(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)'){$issues+="Sensitive-shaped content in $f";continue};$s=$text|ConvertFrom-Json;$items+=$s;foreach($b in @("isApprovedForExternalRun","eligibleForManualSnapshotAttempt","canRunExternalSnapshot","externalConnectionAttempted","snapshotAttempted","replayAttempted","orderSubmissionAttempted","shadowReplaySubmitAttempted","tradingMutationAttempted","schedulerStarted")){if([bool]$s.$b){$issues+="Unsafe $b in $f"}}}
$signed=@($items|?{[string]$_.signoffDecision -eq "SignedForPlanning"});$decision=if($issues.Count){"FAIL"}elseif($signed.Count){"PASS"}else{"PASS_WITH_KNOWN_WARNINGS"}
$dir=Join-Path $repoRoot "artifacts/readiness";New-Item -ItemType Directory -Path $dir -Force|Out-Null;$path=Join-Path $dir "phase6u-gbpusd-operator-signoff-review.json"
[ordered]@{generatedAtUtc=[DateTimeOffset]::UtcNow.ToString("o");phase="6U";finalDecision=$decision;signoffCount=$items.Count;signedForPlanningCount=$signed.Count;unsafeCount=$issues.Count;latestSignoffDecision=if($items.Count){[string]$items[-1].signoffDecision}else{""};signedByOperatorId=if($items.Count){[string]$items[-1].signedByOperatorId}else{""};issues=$issues}|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $path -Encoding UTF8
Write-Host "FinalDecision: $decision";Write-Host "SignoffCount: $($items.Count)";Write-Host "SignedForPlanningCount: $($signed.Count)";Write-Host "UnsafeCount: $($issues.Count)";Write-Host "Report: $path";if($decision -eq "FAIL"){exit 1}
