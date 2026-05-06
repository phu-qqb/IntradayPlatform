param(
    [string]$Side = "Buy",
    [string]$OrderType = "Market",
    [string]$TimeInForce = "IOC",
    [decimal]$VenueQuantity = 0.1,
    [string]$LimitPrice = "",
    [decimal]$MaxNotionalUsd = 5000,
    [string]$ClientOrderId = "",
    [string]$Account = "",
    [switch]$IncludeHandlInst
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "tools\QQ.Production.Intraday.Lmax.ConnectivityLab\QQ.Production.Intraday.Lmax.ConnectivityLab.csproj"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_home"
$culture = [Globalization.CultureInfo]::InvariantCulture
$quantityText = $VenueQuantity.ToString($culture)
$maxNotionalText = $MaxNotionalUsd.ToString($culture)

$labArgs = @(
    "fix-demo-order-lifecycle",
    "--dry-run=true",
    "--side=$Side",
    "--order-type=$OrderType",
    "--time-in-force=$TimeInForce",
    "--venue-quantity=$quantityText",
    "--max-notional-usd=$maxNotionalText"
)

if ($LimitPrice) { $labArgs += "--limit-price=$LimitPrice" }
if ($ClientOrderId) { $labArgs += "--client-order-id=$ClientOrderId" }
if ($Account) { $labArgs += "--account=$Account" }
if ($IncludeHandlInst) { $labArgs += "--include-handl-inst=true" }

dotnet run --project $project --no-build --no-restore -- @labArgs
