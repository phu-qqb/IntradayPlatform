param(
    [Parameter(Mandatory=$true)][string]$FilePath,
    [string]$BaseUrl = "http://localhost:5050",
    [string]$ReportDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd"),
    [string]$VenueName = "LMAX",
    [string]$BrokerAccountCode = "LMAX_DEMO_LOCAL"
)

$body = @{
    filePath = $FilePath
    reportDate = $ReportDate
    venueName = $VenueName
    brokerAccountCode = $BrokerAccountCode
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri "$BaseUrl/lmax-eod/import-currency-wallets" -ContentType "application/json" -Body $body
