param(
    [string]$EnvironmentName = "Demo",
    [switch]$AllowExternalConnections,
    [switch]$AllowOrderSubmission,
    [switch]$ConfirmDemoOrder,
    [bool]$DryRun = $true,
    [ValidateSet("Buy", "Sell")]
    [string]$Side = "Buy",
    [decimal]$VenueQuantity = 0.1,
    [ValidateSet("Market", "Limit")]
    [string]$OrderType = "Market",
    [ValidateSet("IOC", "FOK")]
    [string]$TimeInForce = "IOC",
    [Nullable[decimal]]$LimitPrice = $null,
    [decimal]$MaxNotionalUsd = 5000,
    [int]$TradeCaptureLookbackMinutes = 1440,
    [int]$MaxReports = 50,
    [int]$MaxWaitSeconds = 10,
    [string]$OutputJsonPath,
    [switch]$ShowFixMessages
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "tools/QQ.Production.Intraday.Lmax.ConnectivityLab/QQ.Production.Intraday.Lmax.ConnectivityLab.csproj"

$arguments = @(
    "run",
    "--project",
    $project,
    "--no-build",
    "--no-restore",
    "--",
    "fix-demo-lifecycle-evidence",
    "--environment=$EnvironmentName",
    "--allow-external-connections=$($AllowExternalConnections.IsPresent.ToString().ToLowerInvariant())",
    "--allow-order-submission=$($AllowOrderSubmission.IsPresent.ToString().ToLowerInvariant())",
    "--allow-live-trading=false",
    "--dry-run=$($DryRun.ToString().ToLowerInvariant())",
    "--side=$Side",
    "--venue-quantity=$VenueQuantity",
    "--order-type=$OrderType",
    "--time-in-force=$TimeInForce",
    "--max-notional-usd=$MaxNotionalUsd",
    "--trade-capture-lookback-minutes=$TradeCaptureLookbackMinutes",
    "--max-reports=$MaxReports",
    "--max-wait-seconds=$MaxWaitSeconds",
    "--show-fix-messages=$($ShowFixMessages.IsPresent.ToString().ToLowerInvariant())"
)

if ($LimitPrice.HasValue) {
    $arguments += "--limit-price=$($LimitPrice.Value)"
}

if ($OutputJsonPath) {
    $resolvedOutputPath = [IO.Path]::GetFullPath($OutputJsonPath)
    if ($resolvedOutputPath.StartsWith("\\")) {
        throw "Refusing to write lifecycle evidence JSON to a UNC path: $OutputJsonPath"
    }

    $arguments += "--output-json-path=$resolvedOutputPath"
}

if ($ConfirmDemoOrder.IsPresent) {
    $arguments += "--confirm-demo-order=true"
    $arguments += "--confirm-demo-order"
}

if (-not $DryRun -and (-not $AllowExternalConnections -or -not $AllowOrderSubmission -or -not $ConfirmDemoOrder)) {
    Write-Host "Live demo lifecycle evidence requires -AllowExternalConnections, -AllowOrderSubmission, -ConfirmDemoOrder, and -DryRun:`$false."
    exit 2
}

Write-Host "Running lab-only FIX lifecycle evidence command."
Write-Host "DryRun=$DryRun AllowExternalConnections=$($AllowExternalConnections.IsPresent) AllowOrderSubmission=$($AllowOrderSubmission.IsPresent) ConfirmDemoOrder=$($ConfirmDemoOrder.IsPresent)"
if ($OutputJsonPath) {
    Write-Host "OutputJsonPath=$resolvedOutputPath"
}
Write-Host "No command in this script stores credentials, persists live FIX data into the main DB, or wires LMAX into API/Worker."

& dotnet @arguments
exit $LASTEXITCODE
