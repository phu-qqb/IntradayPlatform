param(
    [string]$EnvironmentName = "Demo",
    [switch]$AllowExternalConnections,
    [ValidateSet("Auto", "BasicAuth", "BearerApiKey", "HeaderApiKey", "None")]
    [string]$AuthMode = "Auto",
    [switch]$ShowResponseExcerpt,
    [string]$AccountApiBaseUrl = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "tools\QQ.Production.Intraday.Lmax.ConnectivityLab\QQ.Production.Intraday.Lmax.ConnectivityLab.csproj"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_home"
$labArgs = @("account-api-discover", "--environment=$EnvironmentName", "--allow-external-connections=$($AllowExternalConnections.IsPresent)", "--account-api-auth-mode=$AuthMode", "--show-response-excerpt=$($ShowResponseExcerpt.IsPresent)")
if ($AccountApiBaseUrl) { $labArgs += "--account-api-base-url=$AccountApiBaseUrl" }

dotnet run --project $project --no-build --no-restore -- @labArgs
