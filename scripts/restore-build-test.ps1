Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (Test-Path ".\NuGet.Config") {
  dotnet restore .\QQ.Production.Intraday.sln --configfile .\NuGet.Config -m:1 /p:RestoreUseStaticGraphEvaluation=false
} else {
  dotnet restore .\QQ.Production.Intraday.sln -m:1 /p:RestoreUseStaticGraphEvaluation=false
}

dotnet build .\QQ.Production.Intraday.sln --no-restore -m:1 /p:BuildInParallel=false
dotnet test .\QQ.Production.Intraday.sln --no-build -m:1 /p:BuildInParallel=false
