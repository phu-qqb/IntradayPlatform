param(
  [string]$BaseUrl = "http://localhost:5000"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "GET /health"
Invoke-RestMethod "$BaseUrl/health"

$startUtc = "2026-04-29T09:15:00Z"
Write-Host "POST /market-data/fake-snapshots"
Invoke-RestMethod "$BaseUrl/market-data/fake-snapshots" -Method Post -ContentType "application/json" -Body (@{
  instrumentSymbol = "EURUSD"
  venueName = "LMAX"
  startUtc = $startUtc
  intervalSeconds = 60
  count = 15
  bid = 1.1000
  ask = 1.1002
  bidStep = 0.00001
  askStep = 0.00001
} | ConvertTo-Json)

Write-Host "POST /market-data/build-bars"
Invoke-RestMethod "$BaseUrl/market-data/build-bars" -Method Post -ContentType "application/json" -Body (@{
  venueName = "LMAX"
  timeframe = "FifteenMinutes"
  startUtc = $startUtc
  endUtc = "2026-04-29T09:30:00Z"
} | ConvertTo-Json)

Write-Host "GET /market-data/bars"
Invoke-RestMethod "$BaseUrl/market-data/bars?instrument=EURUSD&venue=LMAX&timeframe=FifteenMinutes"

Write-Host "POST /model-runs"
$modelRun = Invoke-RestMethod "$BaseUrl/model-runs" -Method Post -ContentType "application/json" -Body (@{
  modelName = "IntradayFxModel"
  asOfUtc = $startUtc
  effectiveAtUtc = $startUtc
  navUsd = 1000000
  frequencyMinutes = 15
  targetQuantityMode = "PortfolioBaseCurrencyNotional"
  weights = @(@{ symbol = "EURUSD"; weight = -0.10; rawSecurityId = "EURUSD" })
} | ConvertTo-Json -Depth 5)

Write-Host "GET /model-runs"
Invoke-RestMethod "$BaseUrl/model-runs"
if ($null -ne $modelRun.id.value) {
  Write-Host "POST /model-runs/{id}/process"
  Invoke-RestMethod "$BaseUrl/model-runs/$($modelRun.id.value)/process" -Method Post
}
Write-Host "GET /trade-intents"
Invoke-RestMethod "$BaseUrl/trade-intents"
Write-Host "GET /orders"
Invoke-RestMethod "$BaseUrl/orders"
Write-Host "GET /fills"
Invoke-RestMethod "$BaseUrl/fills"
Write-Host "GET /positions/internal"
Invoke-RestMethod "$BaseUrl/positions/internal"
Write-Host "GET /positions/broker"
Invoke-RestMethod "$BaseUrl/positions/broker"
Write-Host "GET /reconciliation/breaks"
Invoke-RestMethod "$BaseUrl/reconciliation/breaks"
