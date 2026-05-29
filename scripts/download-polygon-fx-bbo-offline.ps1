param(
    [Parameter(Mandatory=$true)]
    [string]$FromUtc,

    [Parameter(Mandatory=$true)]
    [string]$ToUtc,

    [string[]]$Symbols = @("C:EUR-USD", "C:USD-JPY", "C:AUD-USD"),

    [string]$OutDir = "data/offline-quotes/polygon/incoming",

    [int]$Limit = 50000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$apiKey = $env:POLYGON_API_KEY
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    throw "Missing POLYGON_API_KEY environment variable. Set it with: `$env:POLYGON_API_KEY = '...'"
}

function Convert-ToUnixNanos([string]$isoUtc) {
    $dto = [DateTimeOffset]::Parse($isoUtc).ToUniversalTime()
    return [int64]($dto.ToUnixTimeMilliseconds() * 1000000)
}

function Convert-NanosToIsoUtc([Int64]$nanos) {
    $millis = [int64][math]::Floor($nanos / 1000000)
    return [DateTimeOffset]::FromUnixTimeMilliseconds($millis).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
}

function Get-CleanSymbol([string]$providerSymbol) {
    return $providerSymbol.Replace("C:", "").Replace("-", "").ToUpperInvariant()
}

function Get-NormalizedPortfolioSymbol([string]$executionSymbol) {
    switch ($executionSymbol) {
        "USDJPY" { return "JPYUSD" }
        "USDCAD" { return "CADUSD" }
        "USDCHF" { return "CHFUSD" }
        "USDMXN" { return "MXNUSD" }
        "USDCNH" { return "CNHUSD" }
        "USDNOK" { return "NOKUSD" }
        "USDSEK" { return "SEKUSD" }
        "USDZAR" { return "ZARUSD" }
        default { return $executionSymbol }
    }
}

function Get-RequiresInversion([string]$executionSymbol) {
    return @("USDJPY","USDCAD","USDCHF","USDMXN","USDCNH","USDNOK","USDSEK","USDZAR") -contains $executionSymbol
}

function Add-ApiKeyToNextUrl([string]$nextUrl, [string]$apiKey) {
    if ($nextUrl -match "apiKey=") { return $nextUrl }
    if ($nextUrl -match "\?") { return "$nextUrl&apiKey=$apiKey" }
    return "$nextUrl?apiKey=$apiKey"
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$fromNs = Convert-ToUnixNanos $FromUtc
$toNs = Convert-ToUnixNanos $ToUtc

$createdFiles = @()

foreach ($symbol in $Symbols) {
    $executionSymbol = Get-CleanSymbol $symbol
    $normalizedSymbol = Get-NormalizedPortfolioSymbol $executionSymbol
    $requiresInversion = Get-RequiresInversion $executionSymbol

    $safeRange = (
        ([DateTimeOffset]::Parse($FromUtc).UtcDateTime.ToString("yyyyMMddHHmmss")) +
        "-" +
        ([DateTimeOffset]::Parse($ToUtc).UtcDateTime.ToString("yyyyMMddHHmmss"))
    )

    $quoteFile = Join-Path $OutDir ("{0}-{1}.ndjson" -f $executionSymbol.ToLowerInvariant(), $safeRange)
    $manifestFile = Join-Path $OutDir ("{0}-{1}.manifest.json" -f $executionSymbol.ToLowerInvariant(), $safeRange)

    if (Test-Path $quoteFile) { Remove-Item $quoteFile -Force }
    if (Test-Path $manifestFile) { Remove-Item $manifestFile -Force }

    $encodedSymbol = [System.Uri]::EscapeDataString($symbol)
    $url = "https://api.polygon.io/v3/quotes/$encodedSymbol" +
           "?timestamp.gte=$fromNs" +
           "&timestamp.lt=$toNs" +
           "&order=asc" +
           "&sort=timestamp" +
           "&limit=$Limit" +
           "&apiKey=$apiKey"

    $rowCount = 0
    $page = 0

    Write-Host "Downloading $symbol -> $quoteFile"

    while (-not [string]::IsNullOrWhiteSpace($url)) {
        $page += 1
        Write-Host "  page $page"

        $response = Invoke-RestMethod -Method Get -Uri $url

        if ($null -ne $response.results) {
            foreach ($r in $response.results) {
                if ($null -eq $r.participant_timestamp) { continue }
                if ($null -eq $r.bid_price) { continue }
                if ($null -eq $r.ask_price) { continue }

                $bid = [decimal]$r.bid_price
                $ask = [decimal]$r.ask_price

                if ($bid -le 0 -or $ask -le 0 -or $ask -lt $bid) { continue }

                $mid = ($bid + $ask) / 2
                $spread = $ask - $bid
                $spreadBps = if ($mid -ne 0) { [double](($spread / $mid) * 10000) } else { $null }

                $row = [ordered]@{
                    provider = "PolygonOfflineFile"
                    providerSymbol = $symbol
                    executionTradableSymbol = $executionSymbol
                    normalizedPortfolioSymbol = $normalizedSymbol
                    requiresInversion = $requiresInversion
                    timestampUtc = Convert-NanosToIsoUtc ([int64]$r.participant_timestamp)
                    timestampUnixNanos = [int64]$r.participant_timestamp
                    bid = [double]$bid
                    ask = [double]$ask
                    mid = [double]$mid
                    spread = [double]$spread
                    spreadBps = $spreadBps
                    bidExchange = $r.bid_exchange
                    askExchange = $r.ask_exchange
                    rawPayloadSerialized = $false
                }

                ($row | ConvertTo-Json -Compress -Depth 5) | Add-Content -Path $quoteFile -Encoding UTF8
                $rowCount += 1
            }
        }

        $nextUrlProperty = $response.PSObject.Properties["next_url"]

		if ($null -ne $nextUrlProperty -and -not [string]::IsNullOrWhiteSpace([string]$nextUrlProperty.Value)) {
			$url = Add-ApiKeyToNextUrl ([string]$nextUrlProperty.Value) $apiKey
		} else {
			$url = $null
		}
    }

    if (-not (Test-Path $quoteFile)) {
        New-Item -ItemType File -Path $quoteFile | Out-Null
    }

    $hash = (Get-FileHash -Algorithm SHA256 -Path $quoteFile).Hash

    $manifest = [ordered]@{
        QuoteFileManifestId = "polygon-$($executionSymbol.ToLowerInvariant())-$safeRange-manifest"
        ProviderName = "PolygonOfflineFile"
        ProviderDatasetType = "HistoricalBboQuotes"
        ProviderSymbol = $symbol
        ExecutionTradableSymbol = $executionSymbol
        NormalizedPortfolioSymbol = $normalizedSymbol
        RequiresInversion = $requiresInversion
        FilePath = $quoteFile
        FileFormat = "NDJSON"
        FileHash = $hash
        FileSizeBytes = (Get-Item $quoteFile).Length
        RowCountDeclared = $rowCount
        TimeRangeStartUtc = ([DateTimeOffset]::Parse($FromUtc).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"))
        TimeRangeEndUtc = ([DateTimeOffset]::Parse($ToUtc).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"))
        CreatedAtUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        ProvidedBySanitized = "operator"
        ContainsRawProviderPayload = $false
        ContainsSecrets = $false
        IntakeStatus = "OperatorProvidedPendingAuthorization"
    }

    $manifest | ConvertTo-Json -Depth 10 | Set-Content -Path $manifestFile -Encoding UTF8

    $createdFiles += [ordered]@{
        Symbol = $symbol
        QuoteFile = $quoteFile
        ManifestFile = $manifestFile
        Rows = $rowCount
    }
}

Write-Host ""
Write-Host "Created files:"
foreach ($f in $createdFiles) {
    Write-Host ("- {0}: {1} rows" -f $f.Symbol, $f.Rows)
    Write-Host ("  quote:    {0}" -f $f.QuoteFile)
    Write-Host ("  manifest: {0}" -f $f.ManifestFile)
}