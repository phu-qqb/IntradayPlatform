Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root 'artifacts/readiness/pms-qubes-sandbox-price-basis-r008'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Read-Json {
    param([string]$Path)
    Get-Content -Raw -Path $Path | ConvertFrom-Json
}

$required = @(
    'phase-pms-qubes-sandbox-price-basis-r008-summary.md',
    'phase-pms-qubes-sandbox-price-basis-r008-intake.json',
    'phase-pms-qubes-sandbox-price-basis-r008-marketdata-binding.json',
    'phase-pms-qubes-sandbox-price-basis-r008-quantity-transformation-evidence.json',
    'phase-pms-qubes-sandbox-price-basis-r008-direct-cross-execution-validation.json',
    'phase-pms-qubes-sandbox-price-basis-r008-pms-rebalance-intent-candidate.json',
    'phase-pms-qubes-sandbox-price-basis-r008-execution-candidate-readiness.json',
    'phase-pms-qubes-sandbox-price-basis-r008-active-sandbox-handoff-manifest.json',
    'phase-pms-qubes-sandbox-price-basis-r008-impact-evidence.json',
    'phase-pms-qubes-sandbox-price-basis-r008-test-evidence.json',
    'phase-pms-qubes-sandbox-price-basis-r008-contract-status-update.json',
    'phase-pms-qubes-sandbox-price-basis-r008-boundary-safety-evidence.json'
)

foreach ($name in $required) {
    Assert-True (Test-Path (Join-Path $artifactDir $name)) "Missing required artifact: $name"
}

$expectedSnapshotId = 'canonical-marketdata-golden-source-r001:polygon-offline-bbo:20251217T020000Z:AUDUSD-EURUSD-GBPUSD'
$expectedQubesHash = '5AB433ED36E08CFD8DCA7A8B02138E7CC81280F62E56D894E239D3F75F4DF79A'

$allText = ($required | ForEach-Object { Get-Content -Raw -Path (Join-Path $artifactDir $_) }) -join "`n"
Assert-True ($allText -notmatch '(?i)password\s*[:=]\s*["''][^"'']+["'']') 'Credential-like password value printed.'
Assert-True ($allText -notmatch '(?i)api[_-]?key\s*[:=]\s*["''][^"'']+["'']') 'Credential-like API key value printed.'
Assert-True ($allText -notmatch '(?i)secret\s*[:=]\s*["''][^"'']+["'']') 'Credential-like secret value printed.'

$intake = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-price-basis-r008-intake.json')
$binding = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-price-basis-r008-marketdata-binding.json')
$qty = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-price-basis-r008-quantity-transformation-evidence.json')
$direct = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-price-basis-r008-direct-cross-execution-validation.json')
$candidate = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-price-basis-r008-pms-rebalance-intent-candidate.json')
$readiness = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-price-basis-r008-execution-candidate-readiness.json')
$manifest = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-price-basis-r008-active-sandbox-handoff-manifest.json')
$impact = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-price-basis-r008-impact-evidence.json')
$tests = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-price-basis-r008-test-evidence.json')
$contracts = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-price-basis-r008-contract-status-update.json')
$boundary = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-price-basis-r008-boundary-safety-evidence.json')

Assert-True ($intake.classification -eq 'R001_R005_R006_R007_INTAKE_READY_FOR_QUANTITY_SIZING') 'R008 intake was not ready.'
Assert-True ($binding.classification -eq 'MARKETDATA_SNAPSHOT_BOUND_FOR_SANDBOX_SIZING_WITH_WARNINGS') 'MarketData binding classification missing or wrong.'
Assert-True ($binding.marketDataSnapshotId -eq $expectedSnapshotId) 'Unexpected MarketDataSnapshotId.'
Assert-True ($binding.qubesOutputHash -eq $expectedQubesHash) 'Unexpected Qubes output hash.'
Assert-True ($binding.targetNotionalAmount -eq 6000000) 'Target notional must equal USD 6,000,000.'
Assert-True ($binding.targetNotionalCurrency -eq 'USD') 'Target notional currency must be USD.'
Assert-True ($binding.targetNotionalScope -eq 'SandboxPreviewSizingOnly') 'Target notional scope must remain sandbox preview sizing only.'
Assert-True ($binding.marketDataSnapshotSource -eq 'OperatorProvidedLocalOfflinePolygonBbo') 'Unexpected MarketData source.'
Assert-True ($binding.priceBasisType -eq 'NearestBeforeCloseQuoteMid') 'Unexpected price basis type.'

Assert-True ([decimal]$binding.priceValueBySymbol.AUDUSD -eq [decimal]'0.6632') 'AUDUSD price mismatch.'
Assert-True ([decimal]$binding.priceValueBySymbol.EURUSD -eq [decimal]'1.174725') 'EURUSD price mismatch.'
Assert-True ([decimal]$binding.priceValueBySymbol.GBPUSD -eq [decimal]'1.342475') 'GBPUSD price mismatch.'
Assert-True ($binding.priceTimestampBySymbol.AUDUSD -eq '2025-12-17T01:59:59Z') 'AUDUSD timestamp mismatch.'
Assert-True ($binding.priceTimestampBySymbol.EURUSD -eq '2025-12-17T01:59:57Z') 'EURUSD timestamp mismatch.'
Assert-True ($binding.priceTimestampBySymbol.GBPUSD -eq '2025-12-17T01:59:57Z') 'GBPUSD timestamp mismatch.'
Assert-True ($binding.sourceHashBySymbol.AUDUSD -eq 'D168D981E1012A671F5B8DF48B266F0BA970E6825A507AADA014AFB7C25B5C79') 'AUDUSD source hash mismatch.'
Assert-True ($binding.sourceHashBySymbol.EURUSD -eq 'DA211C76F1CD136FF079C4546203A63E699C5ABB6BECD0F1315243011E682D60') 'EURUSD source hash mismatch.'
Assert-True ($binding.sourceHashBySymbol.GBPUSD -eq 'D3F2D687F1769F501408657C3DCFE1A3C6C429F17E52369E7EE8362AA302506D') 'GBPUSD source hash mismatch.'

Assert-True ($qty.classification -eq 'QUANTITY_TRANSFORMATION_READY_WITH_WARNINGS_SANDBOX_PREVIEW_ONLY') 'Quantity transformation classification mismatch.'
Assert-True ($qty.allQuantitiesFormulaDerivedFromExplicitWeightsPricesAndMetadata -eq $true) 'Quantities must be formula-derived.'
Assert-True ($qty.inventedPrices -eq $false) 'Invented prices are not allowed.'
Assert-True ($qty.inventedQuantities -eq $false) 'Invented quantities are not allowed.'
Assert-True ($qty.executionReadyPreview -eq $true) 'Quantity evidence should be execution-ready preview.'

$aud = $qty.lines | Where-Object { $_.symbol -eq 'AUDUSD' } | Select-Object -First 1
$eur = $qty.lines | Where-Object { $_.symbol -eq 'EURUSD' } | Select-Object -First 1
$gbp = $qty.lines | Where-Object { $_.symbol -eq 'GBPUSD' } | Select-Object -First 1
Assert-True ([decimal]$aud.roundedQuantity -eq [decimal]'48.7') 'AUDUSD rounded quantity mismatch.'
Assert-True ([decimal]$eur.roundedQuantity -eq [decimal]'7.0') 'EURUSD rounded quantity mismatch.'
Assert-True ([decimal]$gbp.roundedQuantity -eq [decimal]'17.5') 'GBPUSD rounded quantity mismatch.'
Assert-True ([decimal]$aud.roundedQuantity -le [decimal]$aud.rawQuantity) 'AUDUSD exposure was rounded up.'
Assert-True ([decimal]$eur.roundedQuantity -le [decimal]$eur.rawQuantity) 'EURUSD exposure was rounded up.'
Assert-True ([decimal]$gbp.roundedQuantity -le [decimal]$gbp.rawQuantity) 'GBPUSD exposure was rounded up.'

Assert-True ($direct.classification -eq 'DIRECT_CROSS_POLICY_PRESERVED_EXECUTION_UNIVERSE_READY') 'Direct-cross validation classification mismatch.'
Assert-True ($direct.directCrossExecutionLeakageFound -eq $false) 'Direct-cross execution leakage found.'
Assert-True (($direct.emittedSymbols -join ',') -eq 'AUDUSD,EURUSD,GBPUSD') 'Unexpected emitted symbols.'
Assert-True ($direct.usdJpyCaveat.preserved -eq $true) 'USDJPY caveat must be preserved.'
Assert-True ($direct.usdJpyCaveat.emittedInR008Candidate -eq $false) 'USDJPY should not be emitted without R001 price basis.'

Assert-True ($candidate.candidateStatus -eq 'PMS_REBALANCE_INTENT_CANDIDATE_READY_WITH_WARNINGS_SANDBOX_PREVIEW_ONLY') 'PMS candidate status mismatch.'
Assert-True ($candidate.marketDataSnapshotId -eq $expectedSnapshotId) 'Candidate MarketDataSnapshotId mismatch.'
Assert-True ($candidate.sandboxOnly -eq $true) 'Candidate must be sandbox-only.'
Assert-True ($candidate.notProduction -eq $true) 'Candidate must not be production.'
Assert-True ($candidate.notAccounting -eq $true) 'Candidate must not be accounting.'
Assert-True ($candidate.notExecuted -eq $true) 'Candidate must not be executed.'
Assert-True ($candidate.notLedgerCommit -eq $true) 'Candidate must not be ledger commit.'
Assert-True ($candidate.executionReadyPreview -eq $true) 'Candidate must be execution-ready preview when quantities exist.'
Assert-True ($candidate.accountId -eq $null) 'AccountId must not be invented.'
Assert-True ($candidate.portfolioId -eq $null) 'PortfolioId must not be invented.'
Assert-True ($candidate.strategyId -eq $null) 'StrategyId must not be invented.'
Assert-True ($candidate.sourceExecutionIntentId -eq $null) 'SourceExecutionIntentId must not be invented.'
Assert-True ($candidate.accountCurrency -eq $null) 'AccountCurrency must not be invented.'
Assert-True ([decimal]$candidate.quantities.AUDUSD -eq [decimal]'48.7') 'Candidate AUDUSD quantity mismatch.'
Assert-True ([decimal]$candidate.quantities.EURUSD -eq [decimal]'7.0') 'Candidate EURUSD quantity mismatch.'
Assert-True ([decimal]$candidate.quantities.GBPUSD -eq [decimal]'17.5') 'Candidate GBPUSD quantity mismatch.'

Assert-True ($readiness.classification -eq 'SANDBOX_QUBES_PMS_EXECUTION_CANDIDATE_READY_WITH_WARNINGS_PREVIEW_ONLY') 'Readiness classification mismatch.'
Assert-True ($manifest.handoffType -eq 'SANDBOX_QUBES_PROTOTYPE_TO_PMS_EXECUTION_CANDIDATE_WITH_WARNINGS_PREVIEW_ONLY') 'Manifest handoff type mismatch.'
Assert-True ($manifest.executionReadyPreview -eq $true) 'Manifest should mark execution-ready preview.'

Assert-True ($impact.r008RetroactivelyRelabelsR014AsQubesDriven -eq $false) 'R014 must not be retroactively relabelled.'
Assert-True ($impact.acceptedGrossSandboxPnlV0Changed -eq $false) 'Gross sandbox PnL V0 must remain unchanged.'
Assert-True ($impact.r008CreatesFills -eq $false) 'R008 must not create fills.'
Assert-True ($impact.r008CreatesLedgerCommits -eq $false) 'R008 must not create ledger commits.'
Assert-True ($impact.theoreticalPnlReadinessClaimed -eq $false) 'Theoretical PnL readiness must not be claimed.'
Assert-True ($impact.netPnlReadinessClaimed -eq $false) 'Net PnL readiness must not be claimed.'
Assert-True ($impact.accountingPnlReadinessClaimed -eq $false) 'Accounting PnL readiness must not be claimed.'
Assert-True ($impact.productionPnlReadinessClaimed -eq $false) 'Production PnL readiness must not be claimed.'

Assert-True ($tests.testStatus -eq 'PASSED') 'Focused R008 test evidence must be PASSED.'

function Get-ContractStatus {
    param([string]$Name)
    ($contracts.contracts | Where-Object { $_.contract -eq $Name } | Select-Object -First 1).status
}

Assert-True ((Get-ContractStatus 'marketdata-snapshot-contract.v1') -eq 'YES') 'marketdata-snapshot-contract.v1 must be YES.'
Assert-True ((Get-ContractStatus 'pms-quantity-policy.v1') -eq 'YES') 'pms-quantity-policy.v1 must be YES.'
Assert-True ((Get-ContractStatus 'pms-execution-candidate.v1') -eq 'YES') 'pms-execution-candidate.v1 must be YES.'
Assert-True ((Get-ContractStatus 'marketdata-readiness.v1') -eq 'WITH_WARNINGS') 'marketdata-readiness.v1 must remain WITH_WARNINGS.'
Assert-True ((Get-ContractStatus 'lmax-marketdata-db.v1') -eq 'WITH_WARNINGS') 'lmax-marketdata-db.v1 must remain WITH_WARNINGS.'
Assert-True ((Get-ContractStatus 'accounting-attribution.v1') -eq 'BLOCKED') 'accounting-attribution.v1 must remain BLOCKED.'
Assert-True ((Get-ContractStatus 'production-readiness.v1') -eq 'BLOCKED') 'production-readiness.v1 must remain BLOCKED.'

Assert-True ($boundary.noLmaxCall -eq $true) 'LMAX call boundary crossed.'
Assert-True ($boundary.noR009Submission -eq $true) 'R009 submission boundary crossed.'
Assert-True ($boundary.noOrderFillOrReport -eq $true) 'Order/fill/report boundary crossed.'
Assert-True ($boundary.noExternalMarketData -eq $true) 'External market data boundary crossed.'
Assert-True ($boundary.noFreshPolygonOrMassiveCall -eq $true) 'Fresh Polygon/Massive call boundary crossed.'
Assert-True ($boundary.noDbMutation -eq $true) 'DB mutation boundary crossed.'
Assert-True ($boundary.noMigration -eq $true) 'Migration boundary crossed.'
Assert-True ($boundary.noSeed -eq $true) 'Seed boundary crossed.'
Assert-True ($boundary.noLedgerCommit -eq $true) 'Ledger commit boundary crossed.'
Assert-True ($boundary.noTradingStateMutation -eq $true) 'Trading-state mutation boundary crossed.'
Assert-True ($boundary.noProductionStateMutation -eq $true) 'Production-state mutation boundary crossed.'
Assert-True ($boundary.noInventedPrices -eq $true) 'Invented prices boundary crossed.'
Assert-True ($boundary.noInventedMarks -eq $true) 'Invented marks boundary crossed.'
Assert-True ($boundary.noInventedFxRates -eq $true) 'Invented FX rates boundary crossed.'
Assert-True ($boundary.noInventedAccountId -eq $true) 'AccountId invented.'
Assert-True ($boundary.noInventedPortfolioId -eq $true) 'PortfolioId invented.'
Assert-True ($boundary.noInventedStrategyId -eq $true) 'StrategyId invented.'
Assert-True ($boundary.noInventedSourceExecutionIntentId -eq $true) 'SourceExecutionIntentId invented.'
Assert-True ($boundary.noInventedAccountCurrency -eq $true) 'AccountCurrency invented.'
Assert-True ($boundary.noAccountingNetProductionPnlReadinessClaim -eq $true) 'Accounting/net/production PnL readiness claimed.'
Assert-True ($boundary.credentialValuesPrintedOrPersisted -eq $false) 'Credential values printed or persisted.'
Assert-True ($boundary.unsafeBoundaryCrossed -eq $false) 'Unsafe boundary crossed.'

Write-Host 'PMS-QUBES-SANDBOX-PRICE-BASIS-R008 gate passed.'
