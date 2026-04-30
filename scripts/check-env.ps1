Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "Working directory: $(Get-Location)"
Write-Host "dotnet version:"
dotnet --version
Write-Host "dotnet SDKs:"
dotnet --list-sdks

if (Test-Path ".\QQ.Production.Intraday.sln") {
  Write-Host "Solution found: QQ.Production.Intraday.sln"
} else {
  Write-Warning "Solution file not found."
}

$sqlLocalDb = Get-Command sqllocaldb -ErrorAction SilentlyContinue
if ($null -ne $sqlLocalDb) {
  Write-Host "LocalDB instances:"
  sqllocaldb info
} else {
  Write-Warning "sqllocaldb command not found. LocalDB-specific scripts/tests may be skipped."
}

if (Test-Path ".\.git") {
  Write-Host "Git status:"
  git status --short
}
