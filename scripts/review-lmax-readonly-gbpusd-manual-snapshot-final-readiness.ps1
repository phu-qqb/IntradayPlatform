param([string[]]$ReadinessFile=@(), [string]$InputDirectory="artifacts/lmax-readonly-runtime-securityid-planning/final-readiness")
$ErrorActionPreference="Stop";$repoRoot=Split-Path -Parent $PSScriptRoot
function Resolve-LocalPath([string]$p){if([IO.Path]::IsPathRooted($p)){$p}else{Join-Path $repoRoot $p}}
Write-Host "LMAX read-only Phase 6V final readiness review"
Write-Host "Local-only. No LMAX connection, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."
$files=@();if($ReadinessFile.Count){$files=@($ReadinessFile|%{Resolve-LocalPath $_})}else{$dir=Resolve-LocalPath $InputDirectory;if(Test-Path $dir){$files=@(Get-ChildItem -LiteralPath $dir -Filter '*.json'|% FullName)}}
$items=@();$issues=@()
foreach($f in $files){if(-not(Test-Path $f)){$issues+="Missing readiness file: $f";continue};$text=Get-Content -Raw $f;if($text -match '(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)'){$issues+="Sensitive-shaped content in $f";continue};$r=$text|ConvertFrom-Json;$items+=$r;foreach($b in @("isApprovedForExternalRun","eligibleForManualSnapshotAttempt","canRunExternalSnapshot","externalConnectionAttempted","snapshotAttempted","replayAttempted","orderSubmissionAttempted","shadowReplaySubmitAttempted","runtimeShadowReplaySubmit","tradingMutationAttempted","schedulerStarted")){if([bool]$r.$b){$issues+="Unsafe $b in $f"}};if([string]$r.readinessDecision -ne "PASS"){$issues+="Non-pass readiness in $f"}}
$decision=if($issues.Count){"FAIL"}elseif($items.Count){"PASS"}else{"PASS_WITH_KNOWN_WARNINGS"}
$dir=Join-Path $repoRoot "artifacts/readiness";New-Item -ItemType Directory -Path $dir -Force|Out-Null;$path=Join-Path $dir "phase6v-gbpusd-final-readiness-review.json"
[ordered]@{generatedAtUtc=[DateTimeOffset]::UtcNow.ToString("o");phase="6V";finalDecision=$decision;readinessCount=$items.Count;unsafeCount=$issues.Count;symbols=@($items|% symbol);issues=$issues}|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $path -Encoding UTF8
Write-Host "FinalDecision: $decision";Write-Host "ReadinessCount: $($items.Count)";Write-Host "UnsafeCount: $($issues.Count)";Write-Host "Report: $path";if($decision -eq "FAIL"){exit 1}
