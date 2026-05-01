param(
    [string]$BaseUrl = "http://localhost:5050",
    [string]$ReportDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd"),
    [string]$VenueName = "LMAX",
    [string]$BrokerAccountCode = "LMAX_DEMO_LOCAL"
)

$query = "reportDate=$ReportDate&venueName=$VenueName&brokerAccountCode=$BrokerAccountCode"
Invoke-RestMethod -Method Get -Uri "$BaseUrl/eod-pnl/summary?$query"
