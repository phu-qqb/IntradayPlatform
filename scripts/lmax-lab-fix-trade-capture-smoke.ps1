param(
    [string]$EnvironmentName = "Demo",
    [switch]$AllowExternalConnections,
    [int]$LookbackMinutes = 1440,
    [string]$StartUtc = "",
    [string]$EndUtc = "",
    [string]$Account = "",
    [int]$MaxWaitSeconds = 10,
    [int]$MaxReports = 50,
    [switch]$ShowFixMessages
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "tools\QQ.Production.Intraday.Lmax.ConnectivityLab\QQ.Production.Intraday.Lmax.ConnectivityLab.csproj"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_home"

$labArgs = @(
    "fix-trade-capture-smoke",
    "--environment=$EnvironmentName",
    "--allow-external-connections=$($AllowExternalConnections.IsPresent)",
    "--lookback-minutes=$LookbackMinutes",
    "--max-wait-seconds=$MaxWaitSeconds",
    "--max-reports=$MaxReports",
    "--show-fix-messages=$($ShowFixMessages.IsPresent)"
)

if ($StartUtc) { $labArgs += "--start-utc=$StartUtc" }
if ($EndUtc) { $labArgs += "--end-utc=$EndUtc" }
if ($Account) { $labArgs += "--account=$Account" }

dotnet run --project $project --no-build --no-restore -- @labArgs
