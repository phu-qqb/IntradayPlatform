param(
    [string]$EnvironmentName = "Demo",
    [switch]$AllowExternalConnections,
    [string]$Instrument = "EURUSD",
    [string]$LmaxInstrumentId = "4001",
    [string]$SlashSymbol = "EUR/USD",
    [int]$TradeCaptureLookbackMinutes = 60,
    [int]$MaxReports = 20,
    [string]$OutputDirectory = "artifacts/lmax-lab/evidence",
    [string]$ClOrdID = "",
    [string]$Account = "",
    [int]$MaxWaitSeconds = 10,
    [switch]$ShowFixMessages
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "tools\QQ.Production.Intraday.Lmax.ConnectivityLab\QQ.Production.Intraday.Lmax.ConnectivityLab.csproj"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_home"

Write-Host "LMAX Connectivity Lab read-only evidence capture." -ForegroundColor Yellow
Write-Host "LAB ONLY. This command may connect externally only with -AllowExternalConnections."
Write-Host "No NewOrderSingle is built or sent. No orders are submitted. No main DB persistence occurs."

if (-not $AllowExternalConnections.IsPresent) {
    Write-Host "Skipped: -AllowExternalConnections is required for live read-only FIX capture." -ForegroundColor Yellow
    exit 0
}

$labArgs = @(
    "fix-readonly-evidence-capture",
    "--environment=$EnvironmentName",
    "--allow-external-connections=true",
    "--allow-order-submission=false",
    "--allow-live-trading=false",
    "--instrument=$Instrument",
    "--lmax-instrument-id=$LmaxInstrumentId",
    "--slash-symbol=$SlashSymbol",
    "--trade-capture-lookback-minutes=$TradeCaptureLookbackMinutes",
    "--max-reports=$MaxReports",
    "--max-wait-seconds=$MaxWaitSeconds",
    "--output-directory=$OutputDirectory",
    "--show-fix-messages=$($ShowFixMessages.IsPresent.ToString().ToLowerInvariant())"
)

if ($ClOrdID) { $labArgs += "--cl-ord-id=$ClOrdID" }
if ($Account) { $labArgs += "--account=$Account" }

dotnet run --project $project --no-build --no-restore -- @labArgs
