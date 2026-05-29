param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactFile
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$fullPath = if ([IO.Path]::IsPathRooted($ArtifactFile)) {
    [IO.Path]::GetFullPath($ArtifactFile)
} else {
    [IO.Path]::GetFullPath((Join-Path (Get-Location) $ArtifactFile))
}

function Add-Issue([string]$Code, [string]$Path, [string]$Message) {
    $script:issues += [ordered]@{ severity = "Error"; code = $Code; path = $Path; message = $Message }
}

function Get-Bool($Object, [string]$Name) {
    if ($null -eq $Object.PSObject.Properties[$Name]) { return $false }
    return [bool]$Object.$Name
}

$issues = @()
if (-not (Test-Path -LiteralPath $fullPath)) {
    Add-Issue "ArtifactFileMissing" "`$" "Artifact file is required and must exist."
} else {
    $expectedRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts/lmax-readonly-runtime-demo-snapshot"))
    if (-not $fullPath.StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        Add-Issue "ArtifactOutsideSnapshotDirectory" "`$.artifactFile" "Artifact must live under artifacts/lmax-readonly-runtime-demo-snapshot."
    }

    $gitignore = Join-Path $repoRoot ".gitignore"
    if (-not (Test-Path -LiteralPath $gitignore) -or -not (Select-String -Path $gitignore -Pattern "artifacts/" -SimpleMatch -Quiet)) {
        Add-Issue "ArtifactDirectoryNotIgnored" "`$.artifactFile" "The artifacts directory must be ignored by git."
    }

    $jsonText = Get-Content -LiteralPath $fullPath -Raw
    $scanText = $jsonText
    foreach ($allowed in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID", "passwordPresent", "usernamePresent")) {
        $scanText = $scanText.Replace($allowed, "")
    }

    $forbiddenPatterns = @(
        "(?i)554\s*=",
        "(?i)553\s*=",
        "(?i)49\s*=[^\s,;]+",
        "(?i)56\s*=[^\s,;]+",
        "(?i)password\s*[:=]\s*(?!\[REDACTED\])[^\s,;`"}]+",
        "(?i)secret\s*[:=]\s*(?!\[REDACTED\])[^\s,;`"}]+",
        "(?i)token\s*[:=]\s*(?!\[REDACTED\])[^\s,;`"}]+",
        "(?i)apiKey\s*[:=]\s*(?!\[REDACTED\])[^\s,;`"}]+",
        "(?i)privateKey\s*[:=]\s*(?!\[REDACTED\])[^\s,;`"}]+",
        "(?i)bearer\s+(?!\[REDACTED\])[^\s,;`"}]+",
        "(?i)authorization\s*[:=]\s*(?!\[REDACTED\])[^`r`n,;`"}]+",
        "(?i)rawFix",
        "(?i)NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatusRequest"
    )
    foreach ($pattern in $forbiddenPatterns) {
        if ($scanText -match $pattern) {
            Add-Issue "ForbiddenSensitiveContent" "`$" "Artifact contains forbidden sensitive/order content pattern: $pattern"
        }
    }

    $artifact = $jsonText | ConvertFrom-Json
    if ($artifact.status -notin @("Completed", "CompletedWithWarnings")) { Add-Issue "StatusNotSuccessful" "`$.status" "Successful closure requires Completed or CompletedWithWarnings." }
    if (-not (Get-Bool $artifact "snapshotReceived")) { Add-Issue "SnapshotMissing" "`$.snapshotReceived" "snapshotReceived must be true." }
    if (-not (Get-Bool $artifact "logonSucceeded")) { Add-Issue "LogonNotSucceeded" "`$.logonSucceeded" "logonSucceeded must be true." }
    if (-not (Get-Bool $artifact "logoutSucceeded")) { Add-Issue "LogoutNotSucceeded" "`$.logoutSucceeded" "logoutSucceeded must be true." }
    if (Get-Bool $artifact "orderSubmissionAttempted") { Add-Issue "OrderSubmissionAttempted" "`$.orderSubmissionAttempted" "orderSubmissionAttempted must be false." }
    if (Get-Bool $artifact "shadowReplaySubmitAttempted") { Add-Issue "ShadowReplaySubmitAttempted" "`$.shadowReplaySubmitAttempted" "shadowReplaySubmitAttempted must be false." }
    if (Get-Bool $artifact "tradingMutationAttempted") { Add-Issue "TradingMutationAttempted" "`$.tradingMutationAttempted" "tradingMutationAttempted must be false." }
    if (Get-Bool $artifact "schedulerStarted") { Add-Issue "SchedulerStarted" "`$.schedulerStarted" "schedulerStarted must be false." }
    if (Get-Bool $artifact "credentialValuesReturned") { Add-Issue "CredentialValuesReturned" "`$.credentialValuesReturned" "credentialValuesReturned must be false." }
    if (-not (Get-Bool $artifact "noSensitiveContent")) { Add-Issue "SensitiveContentFlagFalse" "`$.noSensitiveContent" "noSensitiveContent must be true." }
    if ($artifact.redactionStatus -ne "Redacted") { Add-Issue "RedactionStatusNotRedacted" "`$.redactionStatus" "redactionStatus must be Redacted." }
    if ($artifact.instrument -ne "EURUSD") { Add-Issue "UnexpectedInstrument" "`$.instrument" "instrument must be EURUSD." }
    if ([string]$artifact.securityId -ne "4001") { Add-Issue "UnexpectedSecurityId" "`$.securityId" "securityId must be 4001." }
    if ($null -eq $artifact.bestBid) { Add-Issue "MissingBestBid" "`$.bestBid" "bestBid must be present." }
    if ($null -eq $artifact.bestAsk) { Add-Issue "MissingBestAsk" "`$.bestAsk" "bestAsk must be present." }
    if ($null -eq $artifact.mid) { Add-Issue "MissingMid" "`$.mid" "mid must be present." }
}

$decision = if ($issues.Count -eq 0) { "PASS" } else { "FAIL" }
$summary = [ordered]@{
    artifactFile = $fullPath
    decision = $decision
    status = if ($artifact) { $artifact.status } else { $null }
    snapshotReceived = if ($artifact) { [bool]$artifact.snapshotReceived } else { $false }
    noSensitiveContent = if ($artifact) { [bool]$artifact.noSensitiveContent } else { $false }
    redactionStatus = if ($artifact) { $artifact.redactionStatus } else { $null }
    orderSubmissionAttempted = if ($artifact) { [bool]$artifact.orderSubmissionAttempted } else { $false }
    shadowReplaySubmitAttempted = if ($artifact) { [bool]$artifact.shadowReplaySubmitAttempted } else { $false }
    tradingMutationAttempted = if ($artifact) { [bool]$artifact.tradingMutationAttempted } else { $false }
    issues = @($issues)
}

$summary | ConvertTo-Json -Depth 8 | Write-Host
if ($decision -eq "FAIL") { exit 1 }
