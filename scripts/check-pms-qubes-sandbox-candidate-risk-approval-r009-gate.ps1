Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root 'artifacts/readiness/pms-qubes-sandbox-candidate-risk-approval-r009'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Read-Json {
    param([string]$Path)
    Get-Content -Raw -Path $Path | ConvertFrom-Json
}

$required = @(
    'phase-pms-qubes-sandbox-candidate-risk-approval-r009-summary.md',
    'phase-pms-qubes-sandbox-candidate-risk-approval-r009-intake.json',
    'phase-pms-qubes-sandbox-candidate-risk-approval-r009-weight-semantics-evidence.json',
    'phase-pms-qubes-sandbox-candidate-risk-approval-r009-exposure-review.json',
    'phase-pms-qubes-sandbox-candidate-risk-approval-r009-risk-policy-evidence.json',
    'phase-pms-qubes-sandbox-candidate-risk-approval-r009-approval-decision.json',
    'phase-pms-qubes-sandbox-candidate-risk-approval-r009-future-execution-preconditions.json',
    'phase-pms-qubes-sandbox-candidate-risk-approval-r009-direct-cross-execution-review.json',
    'phase-pms-qubes-sandbox-candidate-risk-approval-r009-marketdata-usage-review.json',
    'phase-pms-qubes-sandbox-candidate-risk-approval-r009-readiness-impact.json',
    'phase-pms-qubes-sandbox-candidate-risk-approval-r009-contract-status-update.json',
    'phase-pms-qubes-sandbox-candidate-risk-approval-r009-boundary-safety-evidence.json',
    'phase-pms-qubes-sandbox-candidate-risk-approval-r009-test-evidence.json'
)

foreach ($name in $required) {
    Assert-True (Test-Path (Join-Path $artifactDir $name)) "Missing required artifact: $name"
}

$allText = ($required | ForEach-Object { Get-Content -Raw -Path (Join-Path $artifactDir $_) }) -join "`n"
Assert-True ($allText -notmatch '(?i)password\s*[:=]\s*["''][^"'']+["'']') 'Credential-like password value printed.'
Assert-True ($allText -notmatch '(?i)api[_-]?key\s*[:=]\s*["''][^"'']+["'']') 'Credential-like API key value printed.'
Assert-True ($allText -notmatch '(?i)secret\s*[:=]\s*["''][^"'']+["'']') 'Credential-like secret value printed.'

$snapshotId = 'canonical-marketdata-golden-source-r001:polygon-offline-bbo:20251217T020000Z:AUDUSD-EURUSD-GBPUSD'

$intake = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-candidate-risk-approval-r009-intake.json')
$semantics = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-candidate-risk-approval-r009-weight-semantics-evidence.json')
$exposure = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-candidate-risk-approval-r009-exposure-review.json')
$riskPolicy = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-candidate-risk-approval-r009-risk-policy-evidence.json')
$decision = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-candidate-risk-approval-r009-approval-decision.json')
$preconditions = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-candidate-risk-approval-r009-future-execution-preconditions.json')
$direct = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-candidate-risk-approval-r009-direct-cross-execution-review.json')
$marketdata = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-candidate-risk-approval-r009-marketdata-usage-review.json')
$impact = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-candidate-risk-approval-r009-readiness-impact.json')
$contracts = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-candidate-risk-approval-r009-contract-status-update.json')
$boundary = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-candidate-risk-approval-r009-boundary-safety-evidence.json')
$tests = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-candidate-risk-approval-r009-test-evidence.json')

Assert-True ($intake.classification -eq 'R008_CANDIDATE_READY_FOR_RISK_APPROVAL_REVIEW') 'Intake classification mismatch.'
Assert-True ($intake.r001MarketDataSnapshot.marketDataSnapshotId -eq $snapshotId) 'MarketDataSnapshotId mismatch.'
Assert-True ([decimal]$intake.r008Candidate.quantities.AUDUSD -eq [decimal]'48.7') 'AUDUSD quantity mismatch.'
Assert-True ([decimal]$intake.r008Candidate.quantities.EURUSD -eq [decimal]'7.0') 'EURUSD quantity mismatch.'
Assert-True ([decimal]$intake.r008Candidate.quantities.GBPUSD -eq [decimal]'17.5') 'GBPUSD quantity mismatch.'

Assert-True ($semantics.classification -eq 'WEIGHTS_CONFIRMED_TARGET_PORTFOLIO_WEIGHTS') 'Weight semantics not confirmed.'
Assert-True ($semantics.notScores -eq $true) 'Weights must not be scores.'
Assert-True ($semantics.notSignalsRequiringNormalization -eq $true) 'Weights must not require further normalization.'

Assert-True ($exposure.classification -eq 'EXPOSURE_REVIEW_PASS_BOUNDED_SANDBOX_PREVIEW') 'Exposure review classification mismatch.'
Assert-True ([decimal]$exposure.aggregate.grossQuoteNotional -eq [decimal]'640142.275') 'Gross exposure mismatch.'
Assert-True ([decimal]$exposure.aggregate.signedNetQuoteExposure -eq [decimal]'-170276.025') 'Net exposure mismatch.'
Assert-True ($exposure.aggregate.allQuantitiesRespectMinOrderSize -eq $true) 'Min order compliance failed.'
Assert-True ($exposure.lowGrossDeploymentIsNotFailure -eq $true) 'Low gross deployment should not fail target-weight candidate.'

Assert-True ($riskPolicy.classification -eq 'SANDBOX_RISK_POLICY_READY_WITH_WARNINGS_OPERATOR_APPROVAL_REQUIRED') 'Risk policy classification mismatch.'
Assert-True ($riskPolicy.futureExecutionOperatorApprovalRequired -eq $true) 'Future operator approval should be required.'
Assert-True ($riskPolicy.autoApprovalGranted -eq $false) 'Auto approval must not be granted.'

Assert-True ($decision.decision -eq 'APPROVED_PREVIEW_ONLY_OPERATOR_APPROVAL_REQUIRED_BEFORE_EXECUTION') 'Approval decision mismatch.'
Assert-True ($decision.approvedForFutureBoundedSandboxExecutionWithoutAdditionalApproval -eq $false) 'Full execution approval without additional approval must not be granted.'
Assert-True ($decision.operatorApprovalForFutureR009LmaxSubmissionPresent -eq $false) 'Future execution operator approval should not be claimed present.'

Assert-True ($preconditions.thisPackageExecutesAnything -eq $false) 'This package must not execute.'
Assert-True ($preconditions.approvalReferencesRequired.marketDataSnapshotId -eq $snapshotId) 'Precondition MarketDataSnapshotId mismatch.'
Assert-True ($preconditions.approvalReferencesRequired.riskReviewArtifactSha256 -ne 'TO_BE_FILLED_AFTER_ARTIFACT_WRITE') 'Risk review artifact hash not filled.'

Assert-True ($direct.classification -eq 'DIRECT_CROSS_POLICY_PRESERVED_EXECUTION_UNIVERSE_READY') 'Direct-cross review classification mismatch.'
Assert-True ($direct.directCrossExecutionLeakageFound -eq $false) 'Direct-cross leakage found.'
Assert-True (($direct.emittedExecutionSymbols -join ',') -eq 'AUDUSD,EURUSD,GBPUSD') 'Unexpected emitted execution symbols.'
Assert-True ($direct.usdJpyCaveatPreserved.preserved -eq $true) 'USDJPY caveat not preserved.'

Assert-True ($marketdata.classification -eq 'MARKETDATA_USAGE_READY_WITH_WARNINGS_OFFLINE_SOURCE') 'MarketData usage classification mismatch.'
Assert-True ($marketdata.marketDataSnapshotId -eq $snapshotId) 'MarketData usage snapshot mismatch.'
Assert-True ($marketdata.polygonOfflineBboProductionLiveSource -eq $false) 'Offline BBO promoted to production/live.'
Assert-True ($marketdata.nearestBeforeCloseMidTheoreticalPnlMarkPolicy -eq $false) 'Nearest-before-close mid promoted to theoretical mark policy.'
Assert-True ($marketdata.nearestBeforeCloseMidAccountingPriceEvidence -eq $false) 'Nearest-before-close mid promoted to accounting price evidence.'

Assert-True ($impact.existingCrossRailR014RemainsPmsIntentDriven -eq $true) 'R014 must remain PMS-intent-driven.'
Assert-True ($impact.r009PackageRetroactivelyRelabelsR014AsQubesDriven -eq $false) 'R014 retroactively relabelled.'
Assert-True ($impact.acceptedGrossSandboxPnlV0Changed -eq $false) 'Gross PnL V0 changed.'
Assert-True ($impact.fillsCreated -eq $false) 'Fills created.'
Assert-True ($impact.ledgerCommitCreated -eq $false) 'Ledger commit created.'
Assert-True ($impact.theoreticalPnlUnlocked -eq $false) 'Theoretical PnL unlocked.'
Assert-True ($impact.netPnlUnlocked -eq $false) 'Net PnL unlocked.'
Assert-True ($impact.accountingPnlUnlocked -eq $false) 'Accounting PnL unlocked.'
Assert-True ($impact.productionLiveReadinessUnlocked -eq $false) 'Production/live unlocked.'

function Get-ContractStatus {
    param([string]$Name)
    ($contracts.contracts | Where-Object { $_.contract -eq $Name } | Select-Object -First 1).status
}

Assert-True ((Get-ContractStatus 'pms-execution-candidate.v1') -eq 'YES') 'pms-execution-candidate.v1 must be YES.'
Assert-True ((Get-ContractStatus 'pms-qubes-risk-approval.v1') -eq 'WITH_WARNINGS') 'pms-qubes-risk-approval.v1 must be WITH_WARNINGS.'
Assert-True ((Get-ContractStatus 'pms-qubes-handoff.v1') -eq 'WITH_WARNINGS') 'pms-qubes-handoff.v1 must be WITH_WARNINGS.'
Assert-True ((Get-ContractStatus 'marketdata-readiness.v1') -eq 'WITH_WARNINGS') 'marketdata-readiness.v1 must remain WITH_WARNINGS.'
Assert-True ((Get-ContractStatus 'lmax-marketdata-db.v1') -eq 'WITH_WARNINGS') 'lmax-marketdata-db.v1 must remain WITH_WARNINGS.'
Assert-True ((Get-ContractStatus 'pnl-preview.v1') -eq 'YES') 'pnl-preview.v1 should remain YES only for existing gross V0.'
Assert-True ((Get-ContractStatus 'accounting-attribution.v1') -eq 'BLOCKED') 'accounting-attribution.v1 must remain BLOCKED.'
Assert-True ((Get-ContractStatus 'production-readiness.v1') -eq 'BLOCKED') 'production-readiness.v1 must remain BLOCKED.'
Assert-True ($contracts.internallyConsistent -eq $true) 'Contract statuses inconsistent.'

Assert-True ($tests.testStatus -eq 'PASSED') 'Focused R009 test evidence must be PASSED.'

Assert-True ($boundary.noLmaxCall -eq $true) 'LMAX call boundary crossed.'
Assert-True ($boundary.noR009Submission -eq $true) 'R009 submission boundary crossed.'
Assert-True ($boundary.noOrderFillOrReport -eq $true) 'Order/fill/report boundary crossed.'
Assert-True ($boundary.noExternalMarketData -eq $true) 'External market data boundary crossed.'
Assert-True ($boundary.noFreshPolygonOrMassiveCall -eq $true) 'Fresh Polygon/Massive boundary crossed.'
Assert-True ($boundary.noDbMutation -eq $true) 'DB mutation boundary crossed.'
Assert-True ($boundary.noMigration -eq $true) 'Migration boundary crossed.'
Assert-True ($boundary.noSeed -eq $true) 'Seed boundary crossed.'
Assert-True ($boundary.noLedgerCommit -eq $true) 'Ledger commit boundary crossed.'
Assert-True ($boundary.noTradingStateMutation -eq $true) 'Trading-state mutation boundary crossed.'
Assert-True ($boundary.noProductionStateMutation -eq $true) 'Production-state mutation boundary crossed.'
Assert-True ($boundary.noInventedTargetNotionalBeyondOperatorProvidedUsd6000000 -eq $true) 'Invented target notional.'
Assert-True ($boundary.noInventedPrices -eq $true) 'Invented prices.'
Assert-True ($boundary.noInventedQuantitiesBeyondR008FormulaDerivedQuantities -eq $true) 'Invented quantities.'
Assert-True ($boundary.noInventedMarks -eq $true) 'Invented marks.'
Assert-True ($boundary.noInventedFxRates -eq $true) 'Invented FX rates.'
Assert-True ($boundary.noInventedAccountId -eq $true) 'Invented AccountId.'
Assert-True ($boundary.noInventedPortfolioId -eq $true) 'Invented PortfolioId.'
Assert-True ($boundary.noInventedStrategyId -eq $true) 'Invented StrategyId.'
Assert-True ($boundary.noInventedSourceExecutionIntentId -eq $true) 'Invented SourceExecutionIntentId.'
Assert-True ($boundary.noInventedAccountCurrency -eq $true) 'Invented AccountCurrency.'
Assert-True ($boundary.noAccountingNetProductionPnlReadinessClaim -eq $true) 'Accounting/net/production PnL readiness claimed.'
Assert-True ($boundary.credentialValuesPrintedOrPersisted -eq $false) 'Credential values printed or persisted.'
Assert-True ($boundary.unsafeBoundaryCrossed -eq $false) 'Unsafe boundary crossed.'

Write-Host 'PMS-QUBES-SANDBOX-CANDIDATE-RISK-APPROVAL-R009 gate passed.'
