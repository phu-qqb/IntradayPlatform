param(
  [string]$BaseUrl = "http://localhost:5050"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function ConvertTo-JsonBody($Body) {
  if ($null -eq $Body) { return $null }
  return ($Body | ConvertTo-Json -Depth 8)
}

function Invoke-LocalApi {
  param(
    [Parameter(Mandatory = $true)][string]$Method,
    [Parameter(Mandatory = $true)][string]$Path,
    [object]$Body = $null
  )

  $uri = "$BaseUrl$Path"
  $jsonBody = ConvertTo-JsonBody $Body
  Write-Host "$Method $Path"
  try {
    if ($null -eq $jsonBody) {
      return Invoke-RestMethod $uri -Method $Method
    }

    return Invoke-RestMethod $uri -Method $Method -ContentType "application/json" -Body $jsonBody
  }
  catch {
    Write-Host "Local API call failed." -ForegroundColor Red
    Write-Host "Endpoint: $Method $uri" -ForegroundColor Red
    if ($null -ne $jsonBody) {
      Write-Host "Request body:" -ForegroundColor Yellow
      Write-Host $jsonBody
    }

    if ($_.Exception.Response) {
      Write-Host "HTTP status: $([int]$_.Exception.Response.StatusCode) $($_.Exception.Response.StatusDescription)" -ForegroundColor Red
      try {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = [System.IO.StreamReader]::new($stream)
        $responseBody = $reader.ReadToEnd()
        if ($responseBody) {
          Write-Host "Response body:" -ForegroundColor Yellow
          Write-Host $responseBody
        }
      }
      catch {
        Write-Host "Unable to read response body: $($_.Exception.Message)" -ForegroundColor Yellow
      }
    }
    else {
      Write-Host $_.Exception.Message -ForegroundColor Red
    }

    throw
  }
}

function Get-ApiId {
  param(
    [Parameter(Mandatory = $false)][AllowNull()][object]$Value
  )

  if ($null -eq $Value) { return $null }

  if ($Value -is [string]) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    return $Value
  }

  if ($Value -is [Guid]) {
    return $Value.ToString()
  }

  $valueProperty = $Value.PSObject.Properties["value"]
  if ($null -ne $valueProperty -and $null -ne $valueProperty.Value) {
    return Get-ApiId $valueProperty.Value
  }

  $idProperty = $Value.PSObject.Properties["id"]
  if ($null -ne $idProperty -and $null -ne $idProperty.Value) {
    return Get-ApiId $idProperty.Value
  }

  $text = [string]$Value
  if ([string]::IsNullOrWhiteSpace($text)) { return $null }
  return $text
}

$now = [DateTime]::UtcNow
$floorMinute = [int]([Math]::Floor($now.Minute / 15) * 15)
$barEnd = [DateTime]::new($now.Year, $now.Month, $now.Day, $now.Hour, $floorMinute, 0, [DateTimeKind]::Utc)
$barStart = $barEnd.AddMinutes(-15)
$freshStart = $now.AddMinutes(-1)

$health = Invoke-LocalApi Get "/health"
$health

$barSnapshots = Invoke-LocalApi Post "/market-data/fake-snapshots" @{
  instrumentSymbol = "EURUSD"
  venueName = "LMAX"
  startUtc = $barStart.ToString("o")
  intervalSeconds = 60
  count = 15
  bid = 1.1000
  ask = 1.1002
  bidStep = 0.00001
  askStep = 0.00001
}
$barSnapshots

$barsBuilt = Invoke-LocalApi Post "/market-data/build-bars" @{
  venueName = "LMAX"
  timeframe = "FifteenMinutes"
  startUtc = $barStart.ToString("o")
  endUtc = $barEnd.ToString("o")
}
$barsBuilt

$freshSnapshots = Invoke-LocalApi Post "/market-data/fake-snapshots" @{
  instrumentSymbol = "EURUSD"
  venueName = "LMAX"
  startUtc = $freshStart.ToString("o")
  intervalSeconds = 60
  count = 2
  bid = 1.1000
  ask = 1.1002
  bidStep = 0.00001
  askStep = 0.00001
}
$freshSnapshots

Invoke-LocalApi Get "/market-data/bars?instrument=EURUSD&venue=LMAX&timeframe=FifteenMinutes"

$modelRun = Invoke-LocalApi Post "/model-runs" @{
  modelName = "IntradayFxModel"
  asOfUtc = $now.ToString("o")
  effectiveAtUtc = $now.ToString("o")
  navUsd = 1000000
  frequencyMinutes = 15
  targetQuantityMode = "PortfolioBaseCurrencyNotional"
  weights = @(@{ symbol = "EURUSD"; weight = -0.10; rawSecurityId = "EURUSD" })
}
$modelRun

Invoke-LocalApi Get "/model-runs"
$modelRunId = Get-ApiId $modelRun.id
if ($null -ne $modelRunId) {
  $processResult = Invoke-LocalApi Post "/model-runs/$modelRunId/process"
  $processResult
}

Invoke-LocalApi Get "/trade-intents"
Invoke-LocalApi Get "/orders"
Invoke-LocalApi Get "/fills"
Invoke-LocalApi Get "/positions/internal"
Invoke-LocalApi Get "/positions/broker"
Invoke-LocalApi Get "/reconciliation/breaks"
