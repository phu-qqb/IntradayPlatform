param(
    [string]$BaseUrl = "http://localhost:5050",
    [string]$ReportDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd")
)

$ErrorActionPreference = "Stop"

function Get-ApiId {
    param([object]$Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [string]) { return $Value }
    if ($Value -is [guid]) { return $Value.ToString() }
    if ($Value.PSObject.Properties.Name -contains "value") { return [string]$Value.value }
    return [string]$Value
}

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

        $json = $Body | ConvertTo-Json -Depth 20
        return Invoke-RestMethod -Method $Method -Uri $uri -ContentType "application/json" -Body $json
    }
    catch {
        Write-Host "Request failed: $Method $uri" -ForegroundColor Red
        if ($null -ne $Body) {
            Write-Host "Request body:"
            Write-Host ($Body | ConvertTo-Json -Depth 20)
        }

        if ($_.Exception.Response) {
            Write-Host "HTTP status: $([int]$_.Exception.Response.StatusCode)"
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                if ($stream) {
                    $reader = New-Object System.IO.StreamReader($stream)
                    $responseBody = $reader.ReadToEnd()
                    if ($responseBody) {
                        Write-Host "Response body:"
                        Write-Host $responseBody
                    }
                }
            }
            catch {
                Write-Host "Could not read response body."
            }
        }

        Write-Host $_.Exception.Message
        throw
    }
}

Write-Host "Health"
Invoke-LocalApi Get "/health" | Format-List

Write-Host "Reference data integrity"
$integrity = Invoke-LocalApi Get "/admin/reference-data/integrity"
$integrity | Format-List
if ($integrity.blockingIssueCount -gt 0) {
    throw "Reference data has blocking integrity issues."
}

$positions = Invoke-LocalApi Get "/positions/internal"
$eurPosition = @($positions | Where-Object { $_.symbol -eq "EURUSD" -or $_.instrument -eq "EURUSD" }) | Select-Object -First 1
$currentBase = 0.0
if ($null -ne $eurPosition -and $null -ne $eurPosition.baseQuantity) {
    $currentBase = [decimal]$eurPosition.baseQuantity
}

$targetWeight = -0.10
if ($currentBase -lt 0) { $targetWeight = 0.10 }
if ($currentBase -gt 0) { $targetWeight = -0.10 }

Write-Host "Creating fake weight batch with target weight $targetWeight from current EURUSD base position $currentBase"
$now = [DateTime]::UtcNow
$batch = Invoke-LocalApi Post "/model-weight-batches/fake" @{
    sourceSystem = "Fake"
    fundCode = "QQ_MASTER"
    modelName = "IntradayFxModel"
    asOfUtc = $now.ToString("o")
    effectiveAtUtc = $now.ToString("o")
    frequencyMinutes = 15
    navUsd = 1000000
    targetQuantityMode = "PortfolioBaseCurrencyNotional"
    status = "Ready"
    weights = @(@{ rawSecurityId = "EURUSD"; symbol = "EURUSD"; weight = $targetWeight })
}

$batchId = Get-ApiId $batch.id
$promotion = Invoke-LocalApi Post "/model-weight-batches/$batchId/promote"
$modelRunId = Get-ApiId $promotion.modelRunId
if (-not $modelRunId) { $modelRunId = Get-ApiId $promotion.promotedModelRunId }
if (-not $modelRunId) { throw "Promotion did not return a modelRunId." }

Write-Host "Creating fresh fake market data"
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

Write-Host "Processing promoted model run $modelRunId"
$processResult = Invoke-LocalApi Post "/model-runs/$modelRunId/process"
$processResult | Format-List
if ([int]$processResult.fillCount -le 0) {
    throw "Expected the EOD smoke to create a fresh fill, but process fillCount was $($processResult.fillCount)."
}

$fills = Invoke-LocalApi Get "/fills"
if (@($fills).Count -eq 0) { throw "Expected at least one fill before generating EOD reports." }

Write-Host "Generating and importing clean LMAX EOD report set"
Invoke-LocalApi Post "/lmax-eod/generate-fake" @{ reportDate = $ReportDate; venueName = "LMAX"; brokerAccountCode = "LMAX_DEMO_LOCAL"; mutationMode = "None" } | Format-List
Invoke-LocalApi Post "/lmax-eod/import-generated" @{ reportDate = $ReportDate; venueName = "LMAX"; brokerAccountCode = "LMAX_DEMO_LOCAL" } | Format-List

Write-Host "Running EOD reconciliation"
$cleanRecon = Invoke-LocalApi Post "/eod-reconciliation/run" @{ reportDate = $ReportDate; venueName = "LMAX"; brokerAccountCode = "LMAX_DEMO_LOCAL" }
$cleanRecon | Format-List
if ([int]$cleanRecon.blockingBreakCount -ne 0) { throw "Expected zero blocking EOD breaks for clean generated report." }

Write-Host "PnL summary"
$pnl = Invoke-LocalApi Get "/eod-pnl/summary?reportDate=$ReportDate&venueName=LMAX&brokerAccountCode=LMAX_DEMO_LOCAL"
$pnl | Format-List
if ($null -eq $pnl -or $null -eq $pnl.totalNetPnlUsd) { throw "Expected EOD PnL summary totals." }

Write-Host "Generating mutated report"
Invoke-LocalApi Post "/lmax-eod/generate-fake" @{ reportDate = $ReportDate; venueName = "LMAX"; brokerAccountCode = "LMAX_DEMO_LOCAL"; mutationMode = "AddUnknownExecution" } | Out-Null
Invoke-LocalApi Post "/lmax-eod/import-generated" @{ reportDate = $ReportDate; venueName = "LMAX"; brokerAccountCode = "LMAX_DEMO_LOCAL" } | Out-Null
$mutatedRecon = Invoke-LocalApi Post "/eod-reconciliation/run" @{ reportDate = $ReportDate; venueName = "LMAX"; brokerAccountCode = "LMAX_DEMO_LOCAL" }
$mutatedRecon | Format-List
if ([int]$mutatedRecon.blockingBreakCount -eq 0) { throw "Expected blocking EOD break for mutated report." }

Write-Host "LMAX EOD smoke completed." -ForegroundColor Green
