param(
  [string]$BaseUrl = "http://localhost:5050",
  [string]$ExternalBatchId = "",
  [decimal]$Weight = -0.10
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-LocalApi {
  param([string]$Method, [string]$Path, [object]$Body = $null)
  $uri = "$BaseUrl$Path"
  Write-Host "$Method $Path"
  if ($null -eq $Body) { return Invoke-RestMethod $uri -Method $Method }
  return Invoke-RestMethod $uri -Method $Method -ContentType "application/json" -Body ($Body | ConvertTo-Json -Depth 8)
}

$now = [DateTime]::UtcNow.ToString("o")
$body = @{
  externalBatchId = $(if ([string]::IsNullOrWhiteSpace($ExternalBatchId)) { $null } else { $ExternalBatchId })
  sourceSystem = "Fake"
  fundCode = "QQ_MASTER"
  modelName = "IntradayFxModel"
  asOfUtc = $now
  effectiveAtUtc = $now
  frequencyMinutes = 15
  navUsd = 1000000
  targetQuantityMode = "PortfolioBaseCurrencyNotional"
  status = "Ready"
  weights = @(@{ rawSecurityId = "EURUSD"; symbol = "EURUSD"; weight = $Weight })
}

Invoke-LocalApi Post "/model-weight-batches/fake" $body
