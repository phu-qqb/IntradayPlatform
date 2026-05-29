$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-quantity-derivation-r009"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Dec($Value) {
    [decimal]::Parse([string]$Value, [Globalization.CultureInfo]::InvariantCulture)
}

$evidence = Read-Json "quantity-derivation-evidence.json"
$candidate = Read-Json "pms-core-candidate-with-quantities.json"
$policy = Read-Json "quantity-transformation-policy.json"
$future = Read-Json "future-package-decision.json"

$cad = $evidence.Rows | Where-Object { $_.CoreSymbol -eq "CADUSD" } | Select-Object -First 1
Assert ($cad.QuantityStatus -eq "QUANTITY_DERIVED") "CADUSD should derive a rounded quantity."
$expectedCadRaw = (Dec $cad.TargetSymbolNotionalUsd) / ((Dec $cad.Price) * (Dec $cad.ContractMultiplier))
Assert ([Math]::Abs([double]((Dec $cad.RawQuantity) - $expectedCadRaw)) -lt 0.00000001) "CADUSD raw quantity formula mismatch."
Assert ((Dec $cad.RoundedQuantity) -eq 0.2) "CADUSD should round down to 0.2."

$aud = $evidence.Rows | Where-Object { $_.CoreSymbol -eq "AUDUSD" } | Select-Object -First 1
Assert ($aud.QuantityStatus -eq "QUANTITY_BELOW_MIN") "AUDUSD should be below min."
Assert ((Dec $aud.RoundedQuantity) -eq 0) "Below-min quantity should be zeroed by policy."
Assert ($policy.BelowMinPolicy -match "0.0") "Below-min zero policy missing."

$jpy = $candidate.Rows | Where-Object { $_.Symbol -eq "JPYUSD" } | Select-Object -First 1
Assert ($null -ne $jpy) "JPYUSD model symbol missing."
Assert (-not ($candidate.Symbols -contains "USDJPY")) "USDJPY must not be emitted as Core model symbol."
Assert ($candidate.R009Ready -eq $false) "Candidate must not be R009 ready."
Assert ($candidate.R010Transferability -eq $false) "R010 must not transfer."
Assert ($future.Decision -eq "NEXT_CORE_ANUBIS_INTRADAY_QUANTITY_REFINEMENT_R010") "Below-min warnings should route to quantity refinement."

Write-Host "CORE_ANUBIS_INTRADAY_QUANTITY_DERIVATION_R009_FOCUSED_TEST_PASS"
