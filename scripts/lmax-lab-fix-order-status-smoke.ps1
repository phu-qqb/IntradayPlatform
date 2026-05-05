param(
    [string]$EnvironmentName = "Demo",
    [switch]$AllowExternalConnections,
    [string]$ClOrdId = "",
    [string]$Account = "",
    [switch]$ShowFixMessages
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "tools\QQ.Production.Intraday.Lmax.ConnectivityLab\QQ.Production.Intraday.Lmax.ConnectivityLab.csproj"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_home"

$labArgs = @(
    "fix-order-status-smoke",
    "--environment=$EnvironmentName",
    "--allow-external-connections=$($AllowExternalConnections.IsPresent)",
    "--show-fix-messages=$($ShowFixMessages.IsPresent)"
)

if ($ClOrdId) { $labArgs += "--cl-ord-id=$ClOrdId" }
if ($Account) { $labArgs += "--account=$Account" }

dotnet run --project $project --no-build --no-restore -- @labArgs
