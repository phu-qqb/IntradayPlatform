param(
    [string]$EnvironmentName = "Local",
    [string]$FixOrderHost = "",
    [int]$FixOrderPort = 0,
    [string]$FixSenderCompId = "",
    [string]$FixTargetCompId = "",
    [string]$FixUsername = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "tools\QQ.Production.Intraday.Lmax.ConnectivityLab\QQ.Production.Intraday.Lmax.ConnectivityLab.csproj"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_home"
$labArgs = @("fix-session-dry-run", "--environment=$EnvironmentName")
if ($FixOrderHost) { $labArgs += "--fix-order-host=$FixOrderHost" }
if ($FixOrderPort -gt 0) { $labArgs += "--fix-order-port=$FixOrderPort" }
if ($FixSenderCompId) { $labArgs += "--fix-sender-comp-id=$FixSenderCompId" }
if ($FixTargetCompId) { $labArgs += "--fix-target-comp-id=$FixTargetCompId" }
if ($FixUsername) { $labArgs += "--fix-username=$FixUsername" }

dotnet run --project $project --no-build --no-restore -- @labArgs
