param(
    [string]$BaseUrl = "http://localhost:5050",
    [string]$ReportDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd"),
    [string]$VenueName = "LMAX",
    [string]$BrokerAccountCode = "LMAX_DEMO_LOCAL"
)

$body = @{
    reportDate = $ReportDate
    venueName = $VenueName
    brokerAccountCode = $BrokerAccountCode
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri "$BaseUrl/lmax-eod/import-generated" -ContentType "application/json" -Body $body
