param(
    [string]$BaseUrl = "http://localhost:5050",
    [string]$ReportDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd"),
    [string]$VenueName = "LMAX",
    [string]$BrokerAccountCode = "LMAX_DEMO_LOCAL"
)

$ErrorActionPreference = "Stop"

$query = "reportDate=$ReportDate&venueName=$VenueName&brokerAccountCode=$BrokerAccountCode"
try {
    Invoke-RestMethod -Method Get -Uri "$BaseUrl/eod-pnl/summary?$query"
}
catch {
    Write-Host "Request failed: GET $BaseUrl/eod-pnl/summary?$query" -ForegroundColor Red
    if ($_.Exception.Response) { Write-Host "HTTP status: $([int]$_.Exception.Response.StatusCode)" }
    Write-Host $_.Exception.Message
    throw
}
