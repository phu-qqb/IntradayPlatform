param(
    [string]$EnvironmentName = "Local",
    [switch]$AllowExternalConnections,
    [string]$PublicDataApiBaseUrl = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "tools\QQ.Production.Intraday.Lmax.ConnectivityLab\QQ.Production.Intraday.Lmax.ConnectivityLab.csproj"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_home"
$labArgs = @("public-data-smoke", "--environment=$EnvironmentName", "--allow-external-connections=$($AllowExternalConnections.IsPresent)")
if ($PublicDataApiBaseUrl) { $labArgs += "--public-data-api-base-url=$PublicDataApiBaseUrl" }

dotnet run --project $project --no-build --no-restore -- @labArgs
