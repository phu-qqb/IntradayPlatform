param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\account-specific-commission-confirmation-r001"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Clone-JsonObject([object]$Value) {
    return ($Value | ConvertTo-Json -Depth 30 | ConvertFrom-Json)
}

function Test-Confirmation([object]$Confirmation) {
    $requiredSymbols = @("USDCAD", "USDCNH", "USDJPY", "USDMXN", "USDNOK", "NZDUSD", "USDSEK", "USDSGD", "USDZAR")
    if ($null -eq $Confirmation) { return "BLOCKED_ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION_MISSING" }
    if ($Confirmation.commission_policy.source -ne "account_specific_confirmation") { return "BLOCKED_PUBLIC_ONLY_COMMISSION_EVIDENCE" }
    if ($null -eq $Confirmation.account_scope) { return "BLOCKED_ACCOUNT_SCOPE_MISSING" }
    if ([string]::IsNullOrWhiteSpace($Confirmation.account_scope.account_id_hash)) { return "BLOCKED_ACCOUNT_ID_HASH_MISSING" }
    if ($Confirmation.account_scope.account_currency -ne "USD") { return "BLOCKED_ACCOUNT_CURRENCY_MISMATCH" }
    foreach ($symbol in $requiredSymbols) {
        if (@($Confirmation.account_scope.symbols | Where-Object { $_ -eq $symbol }).Count -ne 1) { return "BLOCKED_SYMBOL_SCOPE_MISMATCH" }
    }
    if ([decimal]$Confirmation.commission_policy.rate_percent -ne [decimal]0.0025) { return "BLOCKED_COMMISSION_RATE_MISMATCH" }
    if ($Confirmation.commission_policy.basis -ne "notional_traded") { return "BLOCKED_COMMISSION_BASIS_MISMATCH" }
    if ($Confirmation.commission_policy.charge_currency -ne "second_named_currency") { return "BLOCKED_CHARGE_CURRENCY_POLICY_MISMATCH" }
    if ($null -eq $Confirmation.approval -or [string]::IsNullOrWhiteSpace($Confirmation.approval.approved_by)) { return "BLOCKED_APPROVAL_MISSING" }
    if ($Confirmation.approval.approval_scope -ne "sandbox_preview_only") { return "BLOCKED_APPROVAL_SCOPE_INVALID" }
    if ($Confirmation.compatibility.matches_r003_public_policy -ne $true -or $Confirmation.compatibility.matches_r001_account_currency_preview_inputs -ne $true) { return "BLOCKED_SOURCE_ARTIFACT_HASH_MISMATCH" }
    return "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001"
}

$input = Read-Json "account-specific-commission-confirmation-input.json"
$output = Read-Json "account-specific-commission-confirmation-output.json"
$compat = Read-Json "compatibility-validation.json"
$readiness = Read-Json "sandbox-full-net-pnl-preview-readiness.json"
$boundary = Read-Json "boundary-safety-evidence.json"

Assert-True ((Test-Confirmation $null) -eq "BLOCKED_ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION_MISSING") "Missing confirmation must block."

$case = Clone-JsonObject $input
$case.commission_policy.source = "public_lmax_charges"
Assert-True ((Test-Confirmation $case) -eq "BLOCKED_PUBLIC_ONLY_COMMISSION_EVIDENCE") "Public-only evidence must block."

$case = Clone-JsonObject $input
$case.account_scope = $null
Assert-True ((Test-Confirmation $case) -eq "BLOCKED_ACCOUNT_SCOPE_MISSING") "Missing account scope must block."

$case = Clone-JsonObject $input
$case.account_scope.account_id_hash = ""
Assert-True ((Test-Confirmation $case) -eq "BLOCKED_ACCOUNT_ID_HASH_MISSING") "Missing account_id_hash must block."

$case = Clone-JsonObject $input
$case.account_scope.account_currency = "EUR"
Assert-True ((Test-Confirmation $case) -eq "BLOCKED_ACCOUNT_CURRENCY_MISMATCH") "Account currency mismatch must block."

$case = Clone-JsonObject $input
$case.account_scope.symbols = @("USDCAD")
Assert-True ((Test-Confirmation $case) -eq "BLOCKED_SYMBOL_SCOPE_MISMATCH") "Symbol scope mismatch must block."

$case = Clone-JsonObject $input
$case.commission_policy.rate_percent = 0.003
Assert-True ((Test-Confirmation $case) -eq "BLOCKED_COMMISSION_RATE_MISMATCH") "Commission rate mismatch must block."

$case = Clone-JsonObject $input
$case.commission_policy.basis = "notional_estimated"
Assert-True ((Test-Confirmation $case) -eq "BLOCKED_COMMISSION_BASIS_MISMATCH") "Commission basis mismatch must block."

$case = Clone-JsonObject $input
$case.commission_policy.charge_currency = "account_currency"
Assert-True ((Test-Confirmation $case) -eq "BLOCKED_CHARGE_CURRENCY_POLICY_MISMATCH") "Charge currency policy mismatch must block."

$case = Clone-JsonObject $input
$case.approval.approved_by = ""
Assert-True ((Test-Confirmation $case) -eq "BLOCKED_APPROVAL_MISSING") "Missing approval must block."

$case = Clone-JsonObject $input
$case.approval.approval_scope = "production"
Assert-True ((Test-Confirmation $case) -eq "BLOCKED_APPROVAL_SCOPE_INVALID") "Invalid approval scope must block."

$case = Clone-JsonObject $input
$case.compatibility.matches_r003_public_policy = $false
Assert-True ((Test-Confirmation $case) -eq "BLOCKED_SOURCE_ARTIFACT_HASH_MISMATCH") "Source artifact/hash compatibility mismatch must block."

Assert-True ((Test-Confirmation $input) -eq "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001") "Successful explicit fixture confirmation must pass."
Assert-True ($output.status -eq "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMED_R001") "Output status should confirm account-specific commission."
Assert-True ($output.sandbox_full_net_pnl_preview.ready -eq $true) "Sandbox full net PnL preview should be ready after confirmation."
Assert-True ($output.sandbox_full_net_pnl_preview.gross_pnl_account_currency -eq -50.308800) "Gross USD preview must be preserved."
Assert-True ($output.sandbox_full_net_pnl_preview.commission_account_currency -eq 26.268029) "Commission USD preview must be preserved."
Assert-True ($output.sandbox_full_net_pnl_preview.net_pnl_account_currency -eq -76.576829) "Net USD preview must be preserved."
Assert-True ($compat.unfilled_usdjpy_50_excluded -eq $true -and $compat.zero_quantity_lines_excluded -eq $true) "Unfilled USDJPY and zero lines must remain excluded."
Assert-True (@($output.still_blocked | Where-Object { $_ -eq "accounting_pnl_attribution" }).Count -eq 1) "Accounting PnL attribution must remain blocked."
Assert-True ($output.ledger_commit -eq $false -and $output.db_mutation -eq $false -and $output.external_calls -eq $false -and $output.trading_activity -eq $false) "Ledger/DB/external/trading must remain false."
Assert-True ($output.production_live_ready -eq $false) "Production/live readiness must remain false."
Assert-True ($readiness.sandbox_full_net_pnl_preview.ready -eq $true) "Readiness artifact should mark sandbox full net preview ready."
Assert-True ($boundary.no_lmax_fix_api_call -eq $true -and $boundary.no_polygon_massive_call -eq $true -and $boundary.no_broker_api_call -eq $true -and $boundary.no_market_data_fetch -eq $true) "No LMAX/Polygon/Massive/broker/API/market-data call path may be introduced."

Write-Host "ACCOUNT_SPECIFIC_COMMISSION_CONFIRMATION_R001_TESTS_PASS"
