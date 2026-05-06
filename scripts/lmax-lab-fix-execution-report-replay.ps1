param(
    [string]$Fixture = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "tools\QQ.Production.Intraday.Lmax.ConnectivityLab\QQ.Production.Intraday.Lmax.ConnectivityLab.csproj"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_home"

$labArgs = @("fix-execution-report-replay")
if ($Fixture) { $labArgs += "--fixture=$Fixture" }

dotnet run --project $project --no-build --no-restore -- @labArgs
