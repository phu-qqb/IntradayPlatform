param(
  [switch]$SeedDemoData
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (Test-Path ".\NuGet.Config") {
  dotnet restore .\QQ.Production.Intraday.sln --configfile .\NuGet.Config -m:1 /p:RestoreUseStaticGraphEvaluation=false
}

dotnet tool restore
dotnet tool run dotnet-ef database update --project .\src\QQ.Production.Intraday.Infrastructure.SqlServer --startup-project .\src\QQ.Production.Intraday.Api

$argsList = @("--init-db")
if ($SeedDemoData) { $argsList += "--seed-demo" }
dotnet run --project .\src\QQ.Production.Intraday.Api -- @argsList
