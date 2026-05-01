param(
    [string]$BaseUrl = "http://localhost:5050",
    [string]$ReportDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd"),
    [string]$VenueName = "LMAX",
    [string]$BrokerAccountCode = "LMAX_DEMO_LOCAL",
    [string]$MutationMode = "None"
)

$body = @{
    reportDate = $ReportDate
    venueName = $VenueName
    brokerAccountCode = $BrokerAccountCode
    mutationMode = $MutationMode
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri "$BaseUrl/lmax-eod/generate-fake" -ContentType "application/json" -Body $body
