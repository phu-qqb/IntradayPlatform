param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "CORE_ANUBIS_INTRADAY_SIZING_R003_TEST_FAIL: $Message"
}

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing artifact under test: $Name"
    }
    Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-sizing-r003"

$Inventory = Read-Json "core-netted-weights-symbol-inventory.json"
$Target = Read-Json "core-sandbox-target-notional-policy.json"
$Prices = Read-Json "marketdata-price-basis-coverage.json"
$Metadata = Read-Json "instrument-metadata-coverage.json"
$Candidate = Read-Json "pms-core-candidate-preview-sizing-status.json"
$R009 = Read-Json "r009-approval-readiness-decision.json"

if ($Inventory.AllSymbolsCoreCanonicalXXXUSD -ne $true) { Fail "Core handoff weights parse/symbol inventory failed." }
if ($Target.TargetNotionalScope -ne "SandboxPreviewSizingOnly" -or $Target.NotAccounting -ne $true -or $Target.NotProduction -ne $true) { Fail "target notional policy scope failed." }
if (@($Prices.MissingCoreSymbolPrices).Count -le 0 -or $Prices.Classification -ne "MARKETDATA_PRICE_BASIS_BLOCKED_MISSING_CORE_SYMBOL_PRICES") { Fail "missing price did not block quantity." }
if (@($Metadata.MissingCoreSymbolMetadata).Count -le 0) { Fail "missing metadata not detected." }
if ($Inventory.JPYUSDPresent -ne $true) { Fail "JPYUSD caveat not preserved." }
if ($R009.R010PrototypeApprovalReusable -ne $false) { Fail "R010 transferability failed." }
if ($Candidate.R009Ready -ne $false -or $Candidate.ExecutionReadyPreview -ne $false) { Fail "R009-ready candidate incorrectly created." }

Write-Host "CORE_ANUBIS_INTRADAY_SIZING_R003_TEST_PASS"
