param([string]$EnvironmentName = "Demo", [switch]$AllowExternalConnections, [ValidateSet("Auto", "BasicAuth", "BearerApiKey", "HeaderApiKey", "None")] [string]$AuthMode = "Auto", [switch]$ShowResponseExcerpt)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "tools\QQ.Production.Intraday.Lmax.ConnectivityLab\QQ.Production.Intraday.Lmax.ConnectivityLab.csproj"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"; $env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_home"
dotnet run --project $project --no-build --no-restore -- "account-api-trade-history-smoke" "--environment=$EnvironmentName" "--allow-external-connections=$($AllowExternalConnections.IsPresent)" "--account-api-auth-mode=$AuthMode" "--show-response-excerpt=$($ShowResponseExcerpt.IsPresent)"
