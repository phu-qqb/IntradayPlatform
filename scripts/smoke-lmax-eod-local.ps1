param(
    [string]$BaseUrl = "http://localhost:5050",
    [string]$ReportDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd")
)

function Invoke-LocalApi {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    $uri = "$BaseUrl$Path"
    try {
        if ($null -eq $Body) {
            return Invoke-RestMethod -Method $Method -Uri $uri
        }

        $json = $Body | ConvertTo-Json -Depth 10
        return Invoke-RestMethod -Method $Method -Uri $uri -ContentType "application/json" -Body $json
    }
    catch {
        Write-Host "Request failed: $Method $uri" -ForegroundColor Red
        if ($null -ne $Body) { Write-Host ($Body | ConvertTo-Json -Depth 10) }
        if ($_.Exception.Response) {
            Write-Host "HTTP status: $([int]$_.Exception.Response.StatusCode)"
        }
        Write-Host $_.Exception.Message
        throw
    }
}

Write-Host "Health"
Invoke-LocalApi Get "/health" | Format-List

Write-Host "Reference data integrity"
Invoke-LocalApi Get "/admin/reference-data/integrity" | Format-List

Write-Host "Creating fake weight batch"
$batch = Invoke-LocalApi Post "/model-weight-batches/fake" @{
    sourceSystem = "Fake"
    fundCode = "QQ_MASTER"
    modelName = "IntradayFxModel"
    asOfUtc = (Get-Date).ToUniversalTime().ToString("o")
    effectiveAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    frequencyMinutes = 15
    navUsd = 1000000
    targetQuantityMode = "PortfolioBaseCurrencyNotional"
    status = "Ready"
    weights = @(@{ rawSecurityId = "EURUSD"; symbol = "EURUSD"; weight = -0.10 })
}

$promotion = Invoke-LocalApi Post "/model-weight-batches/$($batch.id)/promote"
$modelRunId = if ($promotion.modelRunId) { $promotion.modelRunId } else { $promotion.promotedModelRunId }

Write-Host "Creating fresh fake market data"
$now = [DateTime]::UtcNow
Invoke-LocalApi Post "/market-data/fake-snapshots" @{
    instrumentSymbol = "EURUSD"
    venueName = "LMAX"
    startUtc = $now.AddMinutes(-1).ToString("o")
    intervalSeconds = 60
    count = 2
    bid = 1.10000
    ask = 1.10020
    bidStep = 0.00001
    askStep = 0.00001
} | Out-Null

Write-Host "Processing promoted model run"
Invoke-LocalApi Post "/model-runs/$modelRunId/process" | Format-List

$fills = Invoke-LocalApi Get "/fills"
if ($fills.Count -eq 0) { throw "Expected at least one fill before generating EOD reports." }

Write-Host "Generating and importing clean LMAX EOD report set"
Invoke-LocalApi Post "/lmax-eod/generate-fake" @{ reportDate = $ReportDate; venueName = "LMAX"; brokerAccountCode = "LMAX_DEMO_LOCAL"; mutationMode = "None" } | Format-List
Invoke-LocalApi Post "/lmax-eod/import-generated" @{ reportDate = $ReportDate; venueName = "LMAX"; brokerAccountCode = "LMAX_DEMO_LOCAL" } | Format-List

Write-Host "Running EOD reconciliation"
$cleanRecon = Invoke-LocalApi Post "/eod-reconciliation/run" @{ reportDate = $ReportDate; venueName = "LMAX"; brokerAccountCode = "LMAX_DEMO_LOCAL" }
$cleanRecon | Format-List
if ($cleanRecon.blockingBreakCount -ne 0) { throw "Expected zero blocking EOD breaks for clean generated report." }

Write-Host "PnL summary"
Invoke-LocalApi Get "/eod-pnl/summary?reportDate=$ReportDate&venueName=LMAX&brokerAccountCode=LMAX_DEMO_LOCAL" | Format-List

Write-Host "Generating mutated report"
Invoke-LocalApi Post "/lmax-eod/generate-fake" @{ reportDate = $ReportDate; venueName = "LMAX"; brokerAccountCode = "LMAX_DEMO_LOCAL"; mutationMode = "AddUnknownExecution" } | Out-Null
Invoke-LocalApi Post "/lmax-eod/import-generated" @{ reportDate = $ReportDate; venueName = "LMAX"; brokerAccountCode = "LMAX_DEMO_LOCAL" } | Out-Null
$mutatedRecon = Invoke-LocalApi Post "/eod-reconciliation/run" @{ reportDate = $ReportDate; venueName = "LMAX"; brokerAccountCode = "LMAX_DEMO_LOCAL" }
$mutatedRecon | Format-List
if ($mutatedRecon.blockingBreakCount -eq 0) { throw "Expected blocking EOD break for mutated report." }

Write-Host "LMAX EOD smoke completed." -ForegroundColor Green
