$ErrorActionPreference='Stop'
if([Environment]::MachineName -ne 'EC2AMAZ-1QPHTD8' -or [Environment]::UserName -ne 'Administrator'){throw 'DEMO_OWNER_REQUIRED'}
if((Get-TimeZone).Id -ne 'UTC'){throw 'HOST_UTC_REQUIRED_NO_TIMEZONE_CHANGE_PERFORMED'}
$root='C:\deploy\IntradayPlatform\operator\lmax-demo-orchestration\daily-eod-portal-20260918'
$script=Join-Path $root 'Run-LmaxDemoDailyEod.mjs'
if(-not(Test-Path $script) -or -not(Test-Path (Join-Path $root 'runtime-pin.json'))){throw 'QUALIFIED_RUNTIME_REQUIRED'}
& 'C:\Program Files\nodejs\node.exe' $script --verify-only
if($LASTEXITCODE -ne 0){throw 'QUALIFIED_RUNTIME_REQUIRED'}
$name='QQ-LMAX-Demo-Daily-EOD'
if(Get-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue){throw 'EXISTING_TASK_REQUIRES_REVIEW'}
$action=New-ScheduledTaskAction -Execute 'C:\Program Files\nodejs\node.exe' -Argument ('"'+$script+'" --scheduled') -WorkingDirectory $root
# UTC 20:15 is after the latest programme close, including UK/US DST transition weeks.
$trigger=New-ScheduledTaskTrigger -Daily -At '20:15'
$principal=New-ScheduledTaskPrincipal -UserId 'Administrator' -LogonType Interactive -RunLevel Limited
$settings=New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 10)
Register-ScheduledTask -TaskName $name -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description 'Demo EOD only: exact-date portal reports with AWS-secret recovery, import, reconciliation, provisional recap. No trading or email. Requires Administrator logged on.' | Out-Null
Get-ScheduledTask -TaskName $name | Select-Object TaskName,State,@{n='User';e={$_.Principal.UserId}},@{n='LogonType';e={$_.Principal.LogonType}},@{n='Executable';e={$_.Actions.Execute}},@{n='Arguments';e={$_.Actions.Arguments}} | ConvertTo-Json
Get-ScheduledTaskInfo -TaskName $name | Select-Object NextRunTime,LastTaskResult | ConvertTo-Json
