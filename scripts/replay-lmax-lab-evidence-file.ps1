param(
    [Parameter(Mandatory = $true)]
    [string]$EvidenceFile,
    [string]$BaseUrl = "http://localhost:5050",
    [string]$OperatorId = "local-admin",
    [string]$Reason = "Replay LMAX read-only lab evidence into shadow mode",
    [switch]$AllowInvalidEvidence
)

$ErrorActionPreference = "Stop"
trap {
    exit 1
}

function Assert-LocalUrl {
    param([string]$Url)
    $uri = [Uri]$Url
    if ($uri.Scheme -notin @("http", "https") -or $uri.Host -notin @("localhost", "127.0.0.1")) {
        throw "Refusing non-local API URL: $Url"
    }
}

function Invoke-LocalApi {
    param(
        [string]$Method,
        [string]$Endpoint,
        [object]$Body = $null
    )

    $headers = @{ "X-Operator-Id" = $OperatorId }
    $uri = "$BaseUrl$Endpoint"
    try {
        if ($null -eq $Body) {
            Write-Host "$Method $Endpoint"
            return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
        }

        $json = $Body | ConvertTo-Json -Depth 30
        Write-Host "$Method $Endpoint"
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType "application/json" -Body $json
    } catch {
        Write-Host "FAILED $Method $Endpoint" -ForegroundColor Red
        if ($_.Exception.Response) {
            Write-Host "HTTP status: $([int]$_.Exception.Response.StatusCode) $($_.Exception.Response.StatusCode)"
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                if ($stream) {
                    $reader = [System.IO.StreamReader]::new($stream)
                    $responseBody = $reader.ReadToEnd()
                    if ($responseBody) {
                        Write-Host "Response body:"
                        Write-Host $responseBody
                    }
                }
            } catch {
                Write-Host ("Could not read response body: {0}" -f $_.Exception.Message)
            }
        }
        if ($_.ErrorDetails.Message) {
            Write-Host "Error details:"
            Write-Host $_.ErrorDetails.Message
        }
        throw
    }
}

function Get-ItemsFromResponse {
    param([object]$Response)
    if ($null -eq $Response) { return @() }
    if ($Response -is [array]) { return @($Response) }
    foreach ($name in @("value", "Value", "items", "Items", "observations", "replayRuns", "data")) {
        if ($Response.PSObject.Properties.Name -contains $name) {
            $items = $Response.$name
            if ($null -eq $items) { return @() }
            return @($items)
        }
    }
    return @($Response)
}

function Get-CountSafely {
    param([string]$Endpoint)
    try {
        $response = Invoke-LocalApi -Method "GET" -Endpoint $Endpoint
        return @{ available = $true; count = (Get-ItemsFromResponse -Response $response).Count }
    } catch {
        Write-Host ("Skipping mutation count check for {0}: {1}" -f $Endpoint, $_.Exception.Message) -ForegroundColor Yellow
        return @{ available = $false; count = 0 }
    }
}

function Convert-EvidenceToReplayBody {
    param([object]$Evidence)

    function Convert-ToJsonArray {
        param([object]$Value)
        if ($null -eq $Value) { return @() }
        return ,@($Value)
    }

    function Normalize-TradeCaptureReports {
        param([object]$Value)
        $reports = Convert-ToJsonArray $Value
        foreach ($report in $reports) {
            if ($null -eq $report) { continue }
            if ($report.PSObject.Properties.Name -contains "tradeDate" -and $report.tradeDate -is [string] -and $report.tradeDate -match '^\d{8}$') {
                $report.tradeDate = "{0}-{1}-{2}" -f $report.tradeDate.Substring(0, 4), $report.tradeDate.Substring(4, 2), $report.tradeDate.Substring(6, 2)
            }
            if (-not ($report.PSObject.Properties.Name -contains "tradeUti")) {
                $report | Add-Member -NotePropertyName "tradeUti" -NotePropertyValue $null
            }
        }
        return ,$reports
    }

    return [ordered]@{
        inputSource = "LabEvidenceFile"
        reason = $Reason
        evidenceMode = $null
        executionReports = if ($Evidence.PSObject.Properties.Name -contains "executionReports") { Convert-ToJsonArray $Evidence.executionReports } else { @() }
        tradeCaptureReports = if ($Evidence.PSObject.Properties.Name -contains "tradeCaptureReports") { Normalize-TradeCaptureReports $Evidence.tradeCaptureReports } else { @() }
        orderStatuses = if ($Evidence.PSObject.Properties.Name -contains "orderStatuses") { Convert-ToJsonArray $Evidence.orderStatuses } elseif ($Evidence.PSObject.Properties.Name -contains "orderStatusReports") { Convert-ToJsonArray $Evidence.orderStatusReports } else { @() }
        protocolRejects = if ($Evidence.PSObject.Properties.Name -contains "protocolRejects") { Convert-ToJsonArray $Evidence.protocolRejects } else { @() }
    }
}

function Get-EvidenceMode {
    param(
        [object]$Evidence,
        [object]$ReplayBody
    )

    $executionCount = @($ReplayBody.executionReports).Count
    $orderStatusCount = @($ReplayBody.orderStatuses).Count
    $tradeCaptureCount = @($ReplayBody.tradeCaptureReports).Count
    $protocolRejectCount = @($ReplayBody.protocolRejects).Count
    $hasMarketData = $false
    if ($Evidence.PSObject.Properties.Name -contains "marketData" -and $null -ne $Evidence.marketData) {
        $md = $Evidence.marketData
        $hasMarketData = (($md.PSObject.Properties.Name -contains "snapshotReceived") -and [bool]$md.snapshotReceived) `
            -or (($md.PSObject.Properties.Name -contains "entryCount") -and ([int]$md.entryCount -gt 0)) `
            -or (($md.PSObject.Properties.Name -contains "entries") -and (@($md.entries).Count -gt 0)) `
            -or ($md.PSObject.Properties.Name -contains "bestBid") `
            -or ($md.PSObject.Properties.Name -contains "bestAsk") `
            -or ($md.PSObject.Properties.Name -contains "mid")
    }

    $captureMode = if ($Evidence.PSObject.Properties.Name -contains "captureMode") { [string]$Evidence.captureMode } else { "" }
    if ($captureMode -match "Lifecycle" -or ($executionCount -gt 0 -and $orderStatusCount -gt 0 -and $tradeCaptureCount -gt 0)) { return "SyntheticLifecycle" }
    if ($protocolRejectCount -gt 0 -and $executionCount -eq 0 -and $orderStatusCount -eq 0 -and $tradeCaptureCount -eq 0) { return "ProtocolRejectOnly" }
    if ($tradeCaptureCount -gt 0 -and $executionCount -eq 0 -and $orderStatusCount -eq 0 -and $protocolRejectCount -eq 0) { return "TradeCaptureOnly" }
    if ($orderStatusCount -gt 0 -and $executionCount -eq 0 -and $tradeCaptureCount -eq 0 -and $protocolRejectCount -eq 0) { return "OrderStatusOnly" }
    if ($executionCount -eq 0 -and $orderStatusCount -eq 0 -and $tradeCaptureCount -eq 0 -and $protocolRejectCount -eq 0) {
        if ($hasMarketData) { return "MarketDataOnly" }
        return "EmptyReadOnly"
    }
    return "MixedReadOnly"
}

function Add-ValidationIssue {
    param(
        [System.Collections.Generic.List[object]]$Issues,
        [string]$Severity,
        [string]$Path,
        [string]$Code,
        [string]$Message
    )
    $Issues.Add([pscustomobject]@{ severity = $Severity; path = $Path; code = $Code; message = $Message }) | Out-Null
}

function Test-LmaxEvidenceContract {
    param([object]$Evidence)
    $issues = [System.Collections.Generic.List[object]]::new()
    foreach ($field in @("schemaVersion", "source", "inputSource", "reason", "captureMode", "redaction")) {
        if (-not ($Evidence.PSObject.Properties.Name -contains $field) -or [string]::IsNullOrWhiteSpace([string]$Evidence.$field)) {
            Add-ValidationIssue $issues "Error" "`$.$field" "RequiredFieldMissing" "$field is required."
        }
    }
    if (($Evidence.PSObject.Properties.Name -contains "schemaVersion") -and $Evidence.schemaVersion -ne "lmax-fix-lifecycle-evidence-v1") {
        Add-ValidationIssue $issues "Error" '$.schemaVersion' "UnsupportedSchemaVersion" "schemaVersion must be lmax-fix-lifecycle-evidence-v1."
    }
    if ($Evidence.PSObject.Properties.Name -contains "orderStatusReports") {
        Add-ValidationIssue $issues "Warning" '$.orderStatusReports' "LegacyOrderStatusReports" "Legacy orderStatusReports will be normalized to orderStatuses."
    }
    foreach ($field in @("executionReports", "tradeCaptureReports", "orderStatuses", "protocolRejects")) {
        if ($field -eq "orderStatuses" -and $Evidence.PSObject.Properties.Name -contains "orderStatusReports") {
            continue
        }
        if (-not ($Evidence.PSObject.Properties.Name -contains $field)) {
            Add-ValidationIssue $issues "Warning" "`$.$field" "MissingArrayNormalized" "$field is missing and will be normalized to an empty array."
        }
    }
    $tradeReports = if ($Evidence.PSObject.Properties.Name -contains "tradeCaptureReports") { @($Evidence.tradeCaptureReports) } else { @() }
    for ($i = 0; $i -lt $tradeReports.Count; $i++) {
        $report = $tradeReports[$i]
        if ($null -eq $report) { continue }
        if ($report.PSObject.Properties.Name -contains "tradeDate") {
            $date = [string]$report.tradeDate
            if ($date -match '^\d{8}$') {
                Add-ValidationIssue $issues "Warning" "`$.tradeCaptureReports[$i].tradeDate" "CompactTradeDateNormalized" "Compact tradeDate will be normalized to yyyy-MM-dd."
            } elseif ($date -and $date -notmatch '^\d{4}-\d{2}-\d{2}$') {
                Add-ValidationIssue $issues "Error" "`$.tradeCaptureReports[$i].tradeDate" "InvalidTradeDate" "tradeDate must use yyyy-MM-dd."
            }
        }
        if (-not ($report.PSObject.Properties.Name -contains "tradeUti")) {
            Add-ValidationIssue $issues "Info" "`$.tradeCaptureReports[$i].tradeUti" "MissingTradeUtiAdded" "Missing tradeUti will be normalized to explicit null."
        }
        if ($report.PSObject.Properties.Name -contains "side" -and $report.side -notin @("Buy", "Sell", "1", "2")) {
            Add-ValidationIssue $issues "Error" "`$.tradeCaptureReports[$i].side" "InvalidSide" "side must be Buy or Sell."
        }
    }
    return $issues
}

Assert-LocalUrl $BaseUrl
$resolvedPath = Resolve-Path -LiteralPath $EvidenceFile
$raw = Get-Content -LiteralPath $resolvedPath -Raw
foreach ($forbidden in @("554=", "password", "authorization", "secret", "token", "bearer ", "x-api-key")) {
    if ($raw.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "Evidence file appears to contain forbidden sensitive text: $forbidden"
    }
}

$evidence = $raw | ConvertFrom-Json
$validationIssues = Test-LmaxEvidenceContract -Evidence $evidence
$errorIssues = @($validationIssues | Where-Object { $_.severity -eq "Error" })
$warningIssues = @($validationIssues | Where-Object { $_.severity -eq "Warning" })
$infoIssues = @($validationIssues | Where-Object { $_.severity -eq "Info" })
Write-Host ("Evidence validation: Errors={0} Warnings={1} Info={2}" -f $errorIssues.Count, $warningIssues.Count, $infoIssues.Count)
foreach ($issue in $validationIssues) {
    Write-Host ("{0}: {1} {2} - {3}" -f $issue.severity, $issue.path, $issue.code, $issue.message)
}
if ($errorIssues.Count -gt 0 -and -not $AllowInvalidEvidence.IsPresent) {
    throw "Evidence validation failed. Use -AllowInvalidEvidence only for diagnostics."
}

$beforeOrders = Get-CountSafely -Endpoint "/orders"
$beforeFills = Get-CountSafely -Endpoint "/fills"
$beforePositions = Get-CountSafely -Endpoint "/positions/internal"

$body = Convert-EvidenceToReplayBody -Evidence $evidence
$evidenceMode = Get-EvidenceMode -Evidence $evidence -ReplayBody $body
$body.evidenceMode = $evidenceMode
Write-Host "Replaying evidence file: $resolvedPath"
Write-Host ("EvidenceMode={0}" -f $evidenceMode)
Write-Host "ExecutionReports=$(@($body.executionReports).Count) OrderStatuses=$(@($body.orderStatuses).Count) TradeCaptureReports=$(@($body.tradeCaptureReports).Count) ProtocolRejects=$(@($body.protocolRejects).Count)"
$result = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-shadow/replay" -Body $body
$replayRunId = if ($result.PSObject.Properties.Name -contains "id") { $result.id } elseif ($result.PSObject.Properties.Name -contains "replayRunId") { $result.replayRunId } else { $null }
Write-Host "ReplayRunId: $replayRunId"
Write-Host "Replay $($result.status): $($result.observationCount) observations, $($result.blockingObservationCount) blocking, $($result.warningObservationCount) warnings" -ForegroundColor Green

if ($replayRunId) {
    $observations = Invoke-LocalApi -Method "GET" -Endpoint "/lmax-shadow/observations?replayRunId=$replayRunId&limit=100"
    $observationItems = @(Get-ItemsFromResponse -Response $observations)
    Write-Host ("Observations fetched: {0}" -f $observationItems.Count)
    $observationTypes = @($observationItems | ForEach-Object { $_.type } | Where-Object { $_ } | Sort-Object -Unique)
    Write-Host ("ObservationTypes: {0}" -f ($(if ($observationTypes.Count -gt 0) { $observationTypes -join ", " } else { "(none)" })))
    $observationSeverities = @($observationItems | ForEach-Object { $_.severity } | Where-Object { $_ } | Sort-Object -Unique)
    Write-Host ("ObservationSeverities: {0}" -f ($(if ($observationSeverities.Count -gt 0) { $observationSeverities -join ", " } else { "(none)" })))
    $policyCodes = @($observationItems | ForEach-Object { $_.policyCode } | Where-Object { $_ } | Sort-Object -Unique)
    Write-Host ("PolicyCodes: {0}" -f ($(if ($policyCodes.Count -gt 0) { $policyCodes -join ", " } else { "(none)" })))
}

$audit = Invoke-LocalApi -Method "GET" -Endpoint "/audit/events?limit=100"
Write-Host ("Audit events fetched: {0}" -f (Get-ItemsFromResponse -Response $audit).Count)

$afterOrders = Get-CountSafely -Endpoint "/orders"
$afterFills = Get-CountSafely -Endpoint "/fills"
$afterPositions = Get-CountSafely -Endpoint "/positions/internal"
if ($beforeOrders.available -and $afterOrders.available -and $beforeOrders.count -ne $afterOrders.count) { throw "Order count changed during evidence replay." }
if ($beforeFills.available -and $afterFills.available -and $beforeFills.count -ne $afterFills.count) { throw "Fill count changed during evidence replay." }
if ($beforePositions.available -and $afterPositions.available -and $beforePositions.count -ne $afterPositions.count) { throw "Position count changed during evidence replay." }
Write-Host "MutationGuard: Unchanged" -ForegroundColor Green

$result
