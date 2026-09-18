param([Parameter(Mandatory=$true)][string]$QualificationReceiptPath)
$ErrorActionPreference='Stop'
if([Environment]::MachineName -ne 'EC2AMAZ-1QPHTD8' -or [Environment]::UserName -ne 'Administrator'){throw 'DEMO_OWNER_REQUIRED'}
if((Get-TimeZone).Id -ne 'UTC'){throw 'HOST_UTC_REQUIRED'}
$root='C:\deploy\IntradayPlatform\operator\lmax-demo-orchestration\daily-eod-portal-20260918'
$script=Join-Path $root 'Run-LmaxDemoDailyEod.mjs'
$node='C:\Program Files\nodejs\node.exe'
& $node $script --verify-only
if($LASTEXITCODE -ne 0){throw 'QUALIFIED_RUNTIME_REQUIRED'}
$full=[IO.Path]::GetFullPath($QualificationReceiptPath)
if(-not $full.StartsWith('D:\data\lmax-eod\daily-runs\',[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($full) -ne 'pipeline-receipt.json'){throw 'QUALIFICATION_RECEIPT_PATH_INVALID'}
$receipt=Get-Content -LiteralPath $full -Raw | ConvertFrom-Json
if($receipt.schema -ne 'lmax_demo_daily_pipeline_v1' -or $receipt.acquisition -ne 'PORTAL_REPORTS_ACQUIRED' -or $receipt.import_performed -ne $true -or $null -ne $receipt.error -or $receipt.launcher_sha256 -ne (Get-FileHash -LiteralPath $script -Algorithm SHA256).Hash.ToLowerInvariant()){throw 'PIPELINE_QUALIFICATION_REQUIRED'}
if((Test-Path -LiteralPath 'D:\data\lmax-eod\daily-eod.lock') -or (Test-Path -LiteralPath 'D:\data\lmax-eod\portal-acquisition.lock')){throw 'REPORTING_OWNER_PRESENT'}
$name='QQ-LMAX-Demo-Daily-EOD'
$task=Get-ScheduledTask -TaskName $name
$oldRoot='C:\deploy\IntradayPlatform\operator\lmax-demo-orchestration\daily-eod-20260918'
$oldArgs='"'+(Join-Path $oldRoot 'Run-LmaxDemoDailyEod.mjs')+'" --scheduled'
if($task.State -eq 'Running' -or $task.Actions.Count -ne 1 -or $task.Actions[0].Execute -ne $node -or $task.Actions[0].Arguments -ne $oldArgs -or $task.Actions[0].WorkingDirectory -ne $oldRoot){throw 'TASK_ACTION_CHANGED_OR_RUNNING'}
if($task.Principal.UserId -ne 'Administrator' -or $task.Principal.LogonType -ne 'Interactive' -or $task.Principal.RunLevel -ne 'Limited' -or $task.Settings.ExecutionTimeLimit -ne 'PT10M' -or $task.Settings.MultipleInstances -ne 'IgnoreNew'){throw 'TASK_EXECUTION_CONTEXT_CHANGED'}
$before=Export-ScheduledTask -TaskName $name
$backup='D:\data\lmax-demo-orchestration\reconciliation-20260918\daily-eod-task-before-portal-'+[Guid]::NewGuid().ToString()+'.xml'
[IO.File]::WriteAllText($backup,$before,(New-Object Text.UTF8Encoding($false)))
$taskArguments='"'+$script+'" --scheduled'
$task.Actions=@(New-ScheduledTaskAction -Execute $node -Argument $taskArguments -WorkingDirectory $root)
$task.Description='LMAX Demo EOD: exact-date portal reports with AWS-secret recovery, import, reconciliation and provisional recap. No trading or email. Requires Administrator logged on.'
Set-ScheduledTask -InputObject $task | Out-Null
$after=Get-ScheduledTask -TaskName $name
[xml]$oldXml=$before
[xml]$newXml=Export-ScheduledTask -TaskName $name
if($after.Actions[0].Arguments -ne $taskArguments -or $after.Actions[0].WorkingDirectory -ne $root -or $oldXml.Task.Principals.OuterXml -ne $newXml.Task.Principals.OuterXml -or $oldXml.Task.Settings.OuterXml -ne $newXml.Task.Settings.OuterXml -or $oldXml.Task.Triggers.OuterXml -ne $newXml.Task.Triggers.OuterXml){throw 'TASK_READBACK_MISMATCH_INSPECT_BACKUP'}
$info=Get-ScheduledTaskInfo -TaskName $name
$result=[ordered]@{schema='lmax_demo_reporting_task_update_v1';observed_utc=[DateTime]::UtcNow.ToString('o');task_name=$name;backup_path=$backup;qualification_receipt=$full;qualification_sha256=(Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant();runtime=$root;arguments=$after.Actions[0].Arguments;state=[string]$after.State;next_run_utc=$info.NextRunTime.ToUniversalTime().ToString('o');principal_unchanged=$true;settings_unchanged=$true;triggers_unchanged=$true;trading_started=$false;email_sent=$false}
$output='D:\data\lmax-demo-orchestration\reconciliation-20260918\daily-eod-task-portal-update-'+[Guid]::NewGuid().ToString()+'.json'
[IO.File]::WriteAllText($output,($result|ConvertTo-Json -Depth 5),(New-Object Text.UTF8Encoding($false)))
$result|ConvertTo-Json -Depth 5
