Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --project .\src\QQ.Production.Intraday.Worker
