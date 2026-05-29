Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Set-Location "C:\Users\phili\source\repos\QQ.Production.Intraday"

$symbols = @(
    "C:EUR-USD",
    "C:USD-JPY",
    "C:AUD-USD",
    "C:GBP-USD",
    "C:NZD-USD",
    "C:USD-CAD",
    "C:USD-CHF"
)

$windows = @(
    @{
        FromUtc = "2025-10-14T18:15:00Z"
        ToUtc   = "2025-10-15T01:00:00Z"
    },
    @{
        FromUtc = "2025-10-15T18:15:00Z"
        ToUtc   = "2025-10-16T01:00:00Z"
    },
    @{
        FromUtc = "2025-10-16T18:15:00Z"
        ToUtc   = "2025-10-17T01:00:00Z"
    },
    @{
        FromUtc = "2025-10-17T18:15:00Z"
        ToUtc   = "2025-10-18T01:00:00Z"
    },
    @{
        FromUtc = "2025-10-20T18:15:00Z"
        ToUtc   = "2025-10-21T01:00:00Z"
    }
)

foreach ($window in $windows) {
    Write-Host ""
    Write-Host "============================================================"
    Write-Host "Downloading window $($window.FromUtc) -> $($window.ToUtc)"
    Write-Host "============================================================"

    & .\scripts\download-polygon-fx-bbo-offline.ps1 `
        -FromUtc $window.FromUtc `
        -ToUtc   $window.ToUtc `
        -Symbols $symbols
}