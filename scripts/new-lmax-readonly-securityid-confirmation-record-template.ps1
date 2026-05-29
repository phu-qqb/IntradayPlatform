param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("GBPUSD", "USDJPY", "EURGBP", "AUDUSD", "All")]
    [string]$Symbol,
    [string]$OutputDirectory = "artifacts/lmax-readonly-runtime-securityid-confirmations/templates",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$allowlist = [ordered]@{
    GBPUSD = "GBP/USD"
    USDJPY = "USD/JPY"
    EURGBP = "EUR/GBP"
    AUDUSD = "AUD/USD"
}

function Resolve-LocalPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

Write-Host "LMAX read-only SecurityID confirmation record template generator"
Write-Host "Local-only. No LMAX connection, no external API, no snapshot, no replay, no credentials, no scheduler/polling, no orders, and no mutation."

$symbols = if ($Symbol -eq "All") { @($allowlist.Keys) } else { @($Symbol) }
$outDir = Resolve-LocalPath $OutputDirectory
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$written = @()

foreach ($candidate in $symbols) {
    $template = [ordered]@{
        recordId = "lmax-readonly-securityid-confirmation-$($candidate.ToLowerInvariant())-template"
        createdAtUtc = $null
        symbol = $candidate
        slashSymbol = $allowlist[$candidate]
        proposedSecurityId = ""
        evidenceSourceType = "OperatorManualConfirmation"
        evidenceReference = ""
        capturedBy = ""
        reviewedBy = ""
        reviewedAtUtc = $null
        reviewReason = ""
        confidence = "Low"
        decision = "Draft"
        isApprovedForExternalRun = $false
        noSensitiveContent = $true
        notes = "Template only. Do not include credentials, endpoints, account identifiers, raw FIX, order authorization, Production/UAT approval, or external-run approval."
        externalConnectionAttempted = $false
        externalApiCallsAttempted = $false
        marketDataSnapshotAttempted = $false
        replayAttempted = $false
        runtimeShadowReplaySubmit = $false
        schedulerOrPollingAdded = $false
        orderSubmissionAdded = $false
        gatewayRegistrationAdded = $false
        tradingMutationAdded = $false
    }

    $path = Join-Path $outDir "lmax-readonly-securityid-confirmation-$($candidate.ToLowerInvariant())-template.json"
    if ((Test-Path -LiteralPath $path) -and -not $Force.IsPresent) {
        Write-Host "SKIP: Template exists. Use -Force to overwrite: $path"
        continue
    }

    $template | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding UTF8
    $written += $path
    Write-Host "WROTE: $path"
}

Write-Host "TemplateCount: $($written.Count)"
