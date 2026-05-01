param(
    [Parameter(Mandatory=$true)][string]$IndividualTradesPath,
    [Parameter(Mandatory=$true)][string]$TradesSummaryPath,
    [Parameter(Mandatory=$true)][string]$CurrencyWalletsPath,
    [string]$BaseUrl = "http://localhost:5050",
    [string]$ReportDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd"),
    [string]$VenueName = "LMAX",
    [string]$BrokerAccountCode = "LMAX_DEMO_LOCAL"
)

$body = @{
    individualTradesPath = $IndividualTradesPath
    tradesSummaryPath = $TradesSummaryPath
    currencyWalletsPath = $CurrencyWalletsPath
    reportDate = $ReportDate
    venueName = $VenueName
    brokerAccountCode = $BrokerAccountCode
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri "$BaseUrl/lmax-eod/import-report-set" -ContentType "application/json" -Body $body
