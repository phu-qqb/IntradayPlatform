param([string]$ReportDirectory="artifacts/lmax-readonly-runtime-securityid-planning/dry-run-reports",[string[]]$ReportFile=@())
$ErrorActionPreference="Stop";$repoRoot=Split-Path -Parent $PSScriptRoot
function Resolve-LocalPath([string]$p){if([IO.Path]::IsPathRooted($p)){$p}else{Join-Path $repoRoot $p}}
Write-Host "LMAX read-only Phase 6R dry-run report review"
Write-Host "Local-only. No LMAX connection, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."
$files=if($ReportFile.Count){@($ReportFile|%{Resolve-LocalPath $_})}else{$d=Resolve-LocalPath $ReportDirectory;if(Test-Path $d){@(Get-ChildItem $d -Filter *.json|% FullName)}else{@()}}
$reports=@($files|%{Get-Content -Raw $_|ConvertFrom-Json})
$unsafe=@($reports|?{[string]$_.dryRunDecision -ne "PASS" -or [bool]$_.canRunExternalSnapshot -or [bool]$_.isApprovedForExternalRun -or [bool]$_.eligibleForManualSnapshotAttempt -or [bool]$_.externalConnectionAttempted -or [bool]$_.snapshotAttempted -or [bool]$_.replayAttempted -or [bool]$_.orderSubmissionAttempted -or [bool]$_.shadowReplaySubmitAttempted -or [bool]$_.tradingMutationAttempted -or [bool]$_.schedulerStarted})
$decision=if($unsafe.Count){"FAIL"}elseif($reports.Count){"PASS"}else{"PASS_WITH_KNOWN_WARNINGS"}
$dir=Join-Path $repoRoot "artifacts/readiness";New-Item -ItemType Directory -Path $dir -Force|Out-Null;$path=Join-Path $dir "phase6r-dryrun-report-review.json"
[ordered]@{generatedAtUtc=[DateTimeOffset]::UtcNow.ToString("o");finalDecision=$decision;reportCount=$reports.Count;symbols=@($reports|% symbol);decisions=@($reports|% dryRunDecision);canRunExternalSnapshot=@($reports|% canRunExternalSnapshot);unsafeCount=$unsafe.Count}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $path -Encoding UTF8
Write-Host "FinalDecision: $decision";Write-Host "ReportCount: $($reports.Count)";Write-Host "UnsafeCount: $($unsafe.Count)";Write-Host "Report: $path";if($decision-eq"FAIL"){exit 1}
