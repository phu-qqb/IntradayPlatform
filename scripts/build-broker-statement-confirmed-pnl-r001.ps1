param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\broker-statement-confirmed-pnl-r001"
New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null

function Write-JsonArtifact([string]$Name, [object]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $path -Encoding UTF8
}

function Write-TextArtifact([string]$Name, [string]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | Set-Content -LiteralPath $path -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Sha([string]$Path) {
    "sha256:$((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash)"
}

function Prop($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    if ($Object -is [System.Collections.IDictionary] -and $Object.Contains($Name)) { return $Object[$Name] }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function As-Decimal($Value, [string]$Name) {
    if ($null -eq $Value -or ($Value -is [string] -and [string]::IsNullOrWhiteSpace($Value))) {
        throw "Required decimal value missing: $Name"
    }
    return [decimal]$Value
}

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) { throw "$Message Expected=[$Expected] Actual=[$Actual]" }
}

function Assert-False($Actual, [string]$Message) {
    if ($Actual -ne $false) { throw "$Message Expected=[False] Actual=[$Actual]" }
}

function Assert-True($Actual, [string]$Message) {
    if ($Actual -ne $true) { throw "$Message Expected=[True] Actual=[$Actual]" }
}

$SourceDir = Join-Path $RepoRoot "artifacts\readiness\real-manual-evidence-acceptance-r001"
$AcceptancePath = Join-Path $SourceDir "real-manual-evidence-acceptance-r001.json"
$NormalizedPath = Join-Path $SourceDir "real-manual-broker-statement-normalized-from-lmax-raw-r001.json"
$ValidationPath = Join-Path $SourceDir "real-manual-evidence-validation-report-r001.json"
$DiscoveryPath = Join-Path $SourceDir "real-manual-evidence-discovery-report-r001.json"

foreach ($path in @($AcceptancePath, $NormalizedPath, $ValidationPath, $DiscoveryPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required source artifact missing: $path" }
}

$acceptance = Read-JsonFile $AcceptancePath
$normalized = Read-JsonFile $NormalizedPath

Assert-Equal $acceptance.status "REAL_MANUAL_EVIDENCE_ACCEPTANCE_PARTIAL_READY_R001" "Source acceptance status mismatch."
Assert-True $acceptance.readiness.real_manual_broker_statement_acceptance "Source broker statement acceptance must be true."
Assert-False $acceptance.readiness.real_manual_accounting_evidence_acceptance "Accounting evidence acceptance must remain false."
Assert-Equal $normalized.environment "sandbox" "Normalized broker statement environment mismatch."
Assert-Equal $normalized.import_mode "offline_manual" "Normalized broker statement import mode mismatch."
Assert-False $normalized.sample_only "Normalized broker statement must be non-sample."
Assert-True $normalized.real_broker_statement "Normalized broker statement must be real broker evidence."
Assert-False $normalized.external_fetch "Normalized broker statement must be local-only."
Assert-False $normalized.broker_api_call "Normalized broker statement must not use broker API."
Assert-False $normalized.market_data_fetch "Normalized broker statement must not fetch market data."
Assert-False $normalized.account_data_fetch "Normalized broker statement must not fetch account data."
Assert-False $normalized.db_mutation "Normalized broker statement must not mutate DB."
Assert-False $normalized.ledger_commit "Normalized broker statement must not commit ledger."
Assert-False $normalized.production_live_ready "Normalized broker statement must not mark production/live ready."
Assert-False $normalized.trading_readiness_ready "Normalized broker statement must not mark trading ready."

$tolerance = [decimal]0.000001
$openingBalance = As-Decimal (Prop $normalized "opening_balance_usd") "opening_balance_usd"
$realisedPnl = As-Decimal (Prop $normalized "realised_pnl_usd") "realised_pnl_usd"
$commissionSigned = As-Decimal (Prop $normalized "commission_usd_signed") "commission_usd_signed"
$commissionCost = As-Decimal (Prop $normalized "commission_cost_usd") "commission_cost_usd"
$financingSigned = As-Decimal (Prop $normalized "financing_usd_signed") "financing_usd_signed"
$financingCost = As-Decimal (Prop $normalized "financing_cost_usd") "financing_cost_usd"
$closingBalance = As-Decimal (Prop $normalized "closing_balance_usd") "closing_balance_usd"
$closingPnl = As-Decimal (Prop $normalized "closing_pnl_usd") "closing_pnl_usd"
$closingEquity = As-Decimal (Prop $normalized "closing_equity_usd") "closing_equity_usd"

$realisedNetAfterCosts = $realisedPnl + $commissionSigned + $financingSigned
$equityPnlIncludingOpen = $realisedNetAfterCosts + $closingPnl
$balanceCalculated = $openingBalance + $realisedPnl + $commissionSigned + $financingSigned
$equityCalculated = $closingBalance + $closingPnl
$balanceDelta = $balanceCalculated - $closingBalance
$equityDelta = $equityCalculated - $closingEquity
$balanceReconciled = ([Math]::Abs($balanceDelta) -le $tolerance)
$equityReconciled = ([Math]::Abs($equityDelta) -le $tolerance)

if (-not $balanceReconciled) { throw "Broker statement balance reconciliation failed." }
if (-not $equityReconciled) { throw "Broker statement equity reconciliation failed." }

$policy = [ordered]@{
    policy_type = "broker_statement_confirmed_pnl_policy"
    policy_version = "R001"
    environment = "sandbox"
    source_of_truth = "accepted_offline_manual_lmax_broker_statement"
    scope = "broker_statement_totals_only"
    external_fetch_allowed = $false
    broker_api_allowed = $false
    internal_trade_reconciliation_required_for_this_label = $false
    internal_trade_reconciliation_still_required_for_trade_level_attribution = $true
    accounting_evidence_required_for_broker_statement_confirmed_pnl = $false
    accounting_evidence_still_required_for_accounting_close = $true
    ledger_commit_allowed = $false
    db_mutation_allowed = $false
    production_live_allowed = $false
    trading_allowed = $false
}
Write-JsonArtifact "broker-statement-confirmed-pnl-policy-r001.json" $policy
$PolicyPath = Join-Path $ArtifactDir "broker-statement-confirmed-pnl-policy-r001.json"

$reconciliation = [ordered]@{
    opening_balance_usd = $openingBalance
    realised_pnl_usd = $realisedPnl
    commission_usd_signed = $commissionSigned
    commission_cost_usd = $commissionCost
    financing_usd_signed = $financingSigned
    financing_cost_usd = $financingCost
    closing_balance_usd = $closingBalance
    closing_pnl_usd = $closingPnl
    closing_equity_usd = $closingEquity
    balance_formula = "opening_balance_usd + realised_pnl_usd + commission_usd_signed + financing_usd_signed = closing_balance_usd"
    balance_calculated_usd = $balanceCalculated
    balance_delta_usd = $balanceDelta
    realised_net_after_commission_financing_usd = $realisedNetAfterCosts
    equity_formula = "closing_balance_usd + closing_pnl_usd = closing_equity_usd"
    equity_calculated_usd = $equityCalculated
    equity_delta_usd = $equityDelta
    equity_pnl_including_open_pnl_usd = $equityPnlIncludingOpen
    balance_reconciled = $balanceReconciled
    equity_reconciled = $equityReconciled
    tolerance = "0.000001"
}
Write-JsonArtifact "broker-statement-balance-reconciliation-r001.json" $reconciliation
$ReconciliationPath = Join-Path $ArtifactDir "broker-statement-balance-reconciliation-r001.json"

$main = [ordered]@{
    package = "NEXT_BROKER_STATEMENT_CONFIRMED_PNL_R001"
    status = "BROKER_STATEMENT_CONFIRMED_PNL_READY_R001"
    environment = "sandbox"
    mode = "offline_manual_broker_statement_confirmation_only"
    source_package = "NEXT_REAL_MANUAL_EVIDENCE_ACCEPTANCE_R001"
    source_status = $acceptance.status
    source_artifacts = [ordered]@{
        real_manual_evidence_acceptance_r001 = $AcceptancePath
        normalized_lmax_broker_statement_r001 = $NormalizedPath
        real_manual_evidence_validation_report_r001 = $ValidationPath
        real_manual_evidence_discovery_report_r001 = $DiscoveryPath
        broker_statement_confirmed_pnl_policy_r001 = $PolicyPath
        broker_statement_balance_reconciliation_r001 = $ReconciliationPath
    }
    source_artifact_hashes = [ordered]@{
        real_manual_evidence_acceptance_r001 = Sha $AcceptancePath
        normalized_lmax_broker_statement_r001 = Sha $NormalizedPath
        real_manual_evidence_validation_report_r001 = Sha $ValidationPath
        real_manual_evidence_discovery_report_r001 = Sha $DiscoveryPath
        broker_statement_confirmed_pnl_policy_r001 = Sha $PolicyPath
        broker_statement_balance_reconciliation_r001 = Sha $ReconciliationPath
    }
    broker_statement_scope = [ordered]@{
        broker = $normalized.broker
        venue = $normalized.venue
        account_currency = $normalized.account_currency
        statement_period = [ordered]@{
            from = $normalized.statement_period.from
            to = $normalized.statement_period.to
        }
        trading_statement_date = $normalized.trading_statement_date
        scope = "broker_statement_totals_only"
    }
    broker_statement_confirmed_pnl = [ordered]@{
        ready = $true
        realised_pnl_usd = $realisedPnl
        commission_usd_signed = $commissionSigned
        commission_cost_usd = $commissionCost
        financing_usd_signed = $financingSigned
        financing_cost_usd = $financingCost
        realised_net_after_commission_financing_usd = $realisedNetAfterCosts
        closing_pnl_usd = $closingPnl
        equity_pnl_including_open_pnl_usd = $equityPnlIncludingOpen
        closing_balance_usd = $closingBalance
        closing_equity_usd = $closingEquity
    }
    broker_statement_reconciliation = [ordered]@{
        balance_reconciled = $balanceReconciled
        equity_reconciled = $equityReconciled
        tolerance = "0.000001"
    }
    synthetic_sandbox_closeout_comparison = [ordered]@{
        comparison_performed = $true
        comparison_purpose = "diagnostic_only_not_acceptance_gate"
        synthetic_gross_usd = [decimal]-50.308800
        synthetic_commission_usd = [decimal]26.268029
        synthetic_net_usd = [decimal]-76.576829
        used_as_broker_acceptance_gate = $false
        used_as_broker_confirmed_pnl_gate = $false
    }
    ready_outputs = [ordered]@{
        broker_statement_confirmed_pnl_ready = $true
    }
    still_blocked = @(
        "internal_trade_reconciliation",
        "accounting_evidence_acceptance",
        "realized_accounting_close",
        "ledger_commit",
        "db_mutation",
        "production_live",
        "trading_readiness"
    )
    readiness = [ordered]@{
        broker_statement_confirmed_pnl_ready = $true
        internal_trade_reconciliation_ready = $false
        accounting_evidence_acceptance = $false
        realized_accounting_close = $false
        ledger_commit = $false
        db_mutation = $false
        production_live = $false
        trading_readiness = $false
    }
    forbidden_ready_labels = [ordered]@{
        internal_trade_reconciliation_ready = $false
        accounting_evidence_acceptance = $false
        realized_accounting_close = $false
        committed_ledger = $false
        ledger_commit = $false
        db_mutation = $false
        production_live = $false
        trading_readiness = $false
    }
    global_guards = [ordered]@{
        external_calls = $false
        broker_api_calls = $false
        market_data_fetch = $false
        account_data_fetch = $false
        ledger_commit = $false
        db_mutation = $false
        trading_activity = $false
        production_live_ready = $false
        trading_readiness_ready = $false
    }
}
Write-JsonArtifact "broker-statement-confirmed-pnl-r001.json" $main

$summary = @"
# Broker Statement Confirmed PnL R001

Source package: NEXT_REAL_MANUAL_EVIDENCE_ACCEPTANCE_R001
Source status: $($acceptance.status)

LMAX statement period: $($normalized.statement_period.from) to $($normalized.statement_period.to)
Trading statement date: $($normalized.trading_statement_date)
Account currency: $($normalized.account_currency)

Broker-statement-confirmed PnL, statement totals only:
- Realised PnL USD: $realisedPnl
- Commission signed USD: $commissionSigned
- Commission cost USD: $commissionCost
- Financing signed USD: $financingSigned
- Financing cost USD: $financingCost
- Realised net after commission and financing USD: $realisedNetAfterCosts
- Closing PnL USD: $closingPnl
- Equity PnL including open PnL USD: $equityPnlIncludingOpen
- Closing balance USD: $closingBalance
- Closing equity USD: $closingEquity

Balance reconciliation:
opening_balance_usd + realised_pnl_usd + commission_usd_signed + financing_usd_signed = closing_balance_usd
$openingBalance + $realisedPnl + $commissionSigned + $financingSigned = $closingBalance
Balance reconciled: $balanceReconciled

Equity reconciliation:
closing_balance_usd + closing_pnl_usd = closing_equity_usd
$closingBalance + $closingPnl = $closingEquity
Equity reconciled: $equityReconciled

The synthetic sandbox closeout remains diagnostic only and is not used as a broker statement acceptance gate or a broker-statement-confirmed PnL gate.

Still blocked:
- internal trade reconciliation
- accounting evidence acceptance
- realized accounting close
- ledger commit
- DB mutation
- production/live
- trading readiness

No trading, R009 submission, LMAX FIX/API call, Polygon/Massive call, broker API call, market-data fetch, broker fetch, account-data fetch, DB mutation, ledger commit, production/live action, or trading activity occurred.
"@
Write-TextArtifact "broker-statement-confirmed-pnl-summary-r001.md" $summary

Write-Host "BROKER_STATEMENT_CONFIRMED_PNL_R001_BUILD_READY"
