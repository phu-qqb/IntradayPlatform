param(
  [switch]$SeedDemoData
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Warning "This will DROP the local QQProductionIntraday LocalDB database."
$confirmation = Read-Host "Type RESET to continue"
if ($confirmation -ne "RESET") {
  Write-Host "Reset cancelled."
  exit 0
}

dotnet tool restore
dotnet tool run dotnet-ef database drop --force --project .\src\QQ.Production.Intraday.Infrastructure.SqlServer --startup-project .\src\QQ.Production.Intraday.Api
dotnet tool run dotnet-ef database update --project .\src\QQ.Production.Intraday.Infrastructure.SqlServer --startup-project .\src\QQ.Production.Intraday.Api

$argsList = @("--init-db")
if ($SeedDemoData) { $argsList += "--seed-demo" }
dotnet run --project .\src\QQ.Production.Intraday.Api -- @argsList
