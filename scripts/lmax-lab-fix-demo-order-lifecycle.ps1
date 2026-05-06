param(
    [switch]$AllowExternalConnections,
    [switch]$AllowOrderSubmission,
    [switch]$ConfirmDemoOrder,
    [bool]$DryRun = $true,
    [string]$Side = "Buy",
    [string]$OrderType = "Market",
    [string]$TimeInForce = "IOC",
    [decimal]$VenueQuantity = 0.1,
    [string]$LimitPrice = "",
    [decimal]$MaxNotionalUsd = 5000,
    [string]$ClientOrderId = "",
    [string]$Account = "",
    [int]$MaxWaitSeconds = 10,
    [switch]$ShowFixMessages,
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

if (-not $DryRun -and (-not $AllowExternalConnections -or -not $AllowOrderSubmission -or -not $ConfirmDemoOrder)) {
    throw "Live demo order lifecycle requires -AllowExternalConnections, -AllowOrderSubmission, -ConfirmDemoOrder, and -DryRun:`$false."
}

$labArgs = @(
    "fix-demo-order-lifecycle",
    "--allow-external-connections=$($AllowExternalConnections.IsPresent.ToString().ToLowerInvariant())",
    "--allow-order-submission=$($AllowOrderSubmission.IsPresent.ToString().ToLowerInvariant())",
    "--dry-run=$($DryRun.ToString().ToLowerInvariant())",
    "--side=$Side",
    "--order-type=$OrderType",
    "--time-in-force=$TimeInForce",
    "--venue-quantity=$quantityText",
    "--max-notional-usd=$maxNotionalText",
    "--max-wait-seconds=$MaxWaitSeconds"
)

if ($ConfirmDemoOrder) { $labArgs += "--confirm-demo-order" }
if ($LimitPrice) { $labArgs += "--limit-price=$LimitPrice" }
if ($ClientOrderId) { $labArgs += "--client-order-id=$ClientOrderId" }
if ($Account) { $labArgs += "--account=$Account" }
if ($ShowFixMessages) { $labArgs += "--show-fix-messages=true" }
if ($IncludeHandlInst) { $labArgs += "--include-handl-inst=true" }

dotnet run --project $project --no-build --no-restore -- @labArgs
