Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project .\src\QQ.Production.Intraday.Api
