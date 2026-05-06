param(
    [string]$EnvironmentName = "Demo",
    [switch]$AllowExternalConnections,
    [string]$ClOrdID = "",
    [string]$LmaxInstrumentId = "4001",
    [string]$Side = "",
    [string]$Account = "",
    [int]$MaxWaitSeconds = 10,
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
    "--allow-external-connections=$($AllowExternalConnections.IsPresent.ToString().ToLowerInvariant())",
    "--show-fix-messages=$($ShowFixMessages.IsPresent.ToString().ToLowerInvariant())",
    "--max-wait-seconds=$MaxWaitSeconds"
)

if ($ClOrdID) { $labArgs += "--cl-ord-id=$ClOrdID" }
if ($LmaxInstrumentId) { $labArgs += "--lmax-instrument-id=$LmaxInstrumentId" }
if ($Side) { $labArgs += "--side=$Side" }
if ($Account) { $labArgs += "--account=$Account" }

dotnet run --project $project --no-build --no-restore -- @labArgs
