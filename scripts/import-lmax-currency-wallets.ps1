param(
    [Parameter(Mandatory=$true)][string]$FilePath,
    [string]$BaseUrl = "http://localhost:5050",
    [string]$ReportDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd"),
    [string]$VenueName = "LMAX",
    [string]$BrokerAccountCode = "LMAX_DEMO_LOCAL"
)

$ErrorActionPreference = "Stop"

function Invoke-LocalApi {
    param([string]$Method, [string]$Uri, [object]$Body)
    try {
        $json = $Body | ConvertTo-Json -Depth 10
        return Invoke-RestMethod -Method $Method -Uri $Uri -ContentType "application/json" -Body $json
    }
    catch {
        Write-Host "Request failed: $Method $Uri" -ForegroundColor Red
        Write-Host ($Body | ConvertTo-Json -Depth 10)
        if ($_.Exception.Response) { Write-Host "HTTP status: $([int]$_.Exception.Response.StatusCode)" }
        Write-Host $_.Exception.Message
        throw
    }
}

$body = @{
    filePath = $FilePath
    reportDate = $ReportDate
    venueName = $VenueName
    brokerAccountCode = $BrokerAccountCode
}

Invoke-LocalApi Post "$BaseUrl/lmax-eod/import-currency-wallets" $body
