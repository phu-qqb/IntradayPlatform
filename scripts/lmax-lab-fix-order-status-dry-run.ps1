param(
    [string]$EnvironmentName = "Demo",
    [string]$ClOrdId = "DRYRUN-CLORDID",
    [string]$Account = "",
    [string]$SecurityId = "",
    [string]$SecurityIdSource = "",
    [string]$Side = "",
    [string]$OrdStatusReqId = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "tools\QQ.Production.Intraday.Lmax.ConnectivityLab\QQ.Production.Intraday.Lmax.ConnectivityLab.csproj"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_home"

$labArgs = @(
    "fix-order-status-dry-run",
    "--environment=$EnvironmentName",
    "--cl-ord-id=$ClOrdId"
)

if ($Account) { $labArgs += "--account=$Account" }
if ($SecurityId) { $labArgs += "--security-id=$SecurityId" }
if ($SecurityIdSource) { $labArgs += "--security-id-source=$SecurityIdSource" }
if ($Side) { $labArgs += "--side=$Side" }
if ($OrdStatusReqId) { $labArgs += "--ord-status-req-id=$OrdStatusReqId" }

dotnet run --project $project --no-build --no-restore -- @labArgs
