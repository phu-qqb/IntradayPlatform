Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root 'artifacts/readiness/pms-qubes-sandbox-operator-approval-r010'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Read-Json {
    param([string]$Path)
    Get-Content -Raw -Path $Path | ConvertFrom-Json
}

function Get-Sha256Hex {
    param([string]$Text)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        $hash = $sha.ComputeHash($bytes)
        return [System.BitConverter]::ToString($hash).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

$required = @(
    'phase-pms-qubes-sandbox-operator-approval-r010-summary.md',
    'phase-pms-qubes-sandbox-operator-approval-r010-candidate-binding-evidence.json',
    'phase-pms-qubes-sandbox-operator-approval-r010-operator-approval-statement.json',
    'phase-pms-qubes-sandbox-operator-approval-r010-operator-approval-id.json',
    'phase-pms-qubes-sandbox-operator-approval-r010-approval-scope-guardrails.json',
    'phase-pms-qubes-sandbox-operator-approval-r010-future-execution-preconditions.json',
    'phase-pms-qubes-sandbox-operator-approval-r010-readiness-impact.json',
    'phase-pms-qubes-sandbox-operator-approval-r010-contract-status-update.json',
    'phase-pms-qubes-sandbox-operator-approval-r010-boundary-safety-evidence.json',
    'phase-pms-qubes-sandbox-operator-approval-r010-test-evidence.json'
)

foreach ($name in $required) {
    Assert-True (Test-Path (Join-Path $artifactDir $name)) "Missing required artifact: $name"
}

$allText = ($required | ForEach-Object { Get-Content -Raw -Path (Join-Path $artifactDir $_) }) -join "`n"
Assert-True ($allText -notmatch '(?i)password\s*[:=]\s*["''][^"'']+["'']') 'Credential-like password value printed.'
Assert-True ($allText -notmatch '(?i)api[_-]?key\s*[:=]\s*["''][^"'']+["'']') 'Credential-like API key value printed.'
Assert-True ($allText -notmatch '(?i)secret\s*[:=]\s*["''][^"'']+["'']') 'Credential-like secret value printed.'

$snapshotId = 'canonical-marketdata-golden-source-r001:polygon-offline-bbo:20251217T020000Z:AUDUSD-EURUSD-GBPUSD'
$qubesOutputId = 'qubes-operationalization-r005:prototype-output:20251217T020000Z:001'
$qubesOutputHash = '5AB433ED36E08CFD8DCA7A8B02138E7CC81280F62E56D894E239D3F75F4DF79A'
$riskHash = 'C5AB0301860982A1A6922A434E877AB8A6CC3C6AE5A8A6A125C20DC8F8D6C658'
$operatorApprovalId = 'pms-qubes-sandbox-operator-approval-r010:5d3a9b7aac941102'

$binding = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-operator-approval-r010-candidate-binding-evidence.json')
$statement = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-operator-approval-r010-operator-approval-statement.json')
$id = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-operator-approval-r010-operator-approval-id.json')
$guardrails = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-operator-approval-r010-approval-scope-guardrails.json')
$preconditions = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-operator-approval-r010-future-execution-preconditions.json')
$impact = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-operator-approval-r010-readiness-impact.json')
$contracts = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-operator-approval-r010-contract-status-update.json')
$boundary = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-operator-approval-r010-boundary-safety-evidence.json')
$tests = Read-Json (Join-Path $artifactDir 'phase-pms-qubes-sandbox-operator-approval-r010-test-evidence.json')

Assert-True ($binding.classification -eq 'R008_R009_CANDIDATE_EXACTLY_BOUND_FOR_OPERATOR_APPROVAL') 'Candidate binding classification mismatch.'
Assert-True ($binding.marketDataSnapshotId -eq $snapshotId) 'MarketDataSnapshotId mismatch.'
Assert-True ($binding.qubesOutputId -eq $qubesOutputId) 'QubesOutputId mismatch.'
Assert-True ($binding.qubesOutputHash -eq $qubesOutputHash) 'QubesOutputHash mismatch.'
Assert-True ($binding.r009RiskReviewArtifactHash -eq $riskHash) 'R009 risk review hash mismatch.'
Assert-True ([decimal]$binding.quantities.AUDUSD -eq [decimal]'48.7') 'AUDUSD quantity mismatch.'
Assert-True ([decimal]$binding.quantities.EURUSD -eq [decimal]'7.0') 'EURUSD quantity mismatch.'
Assert-True ([decimal]$binding.quantities.GBPUSD -eq [decimal]'17.5') 'GBPUSD quantity mismatch.'

Assert-True ($statement.classification -eq 'OPERATOR_APPROVAL_EXPLICIT_FOR_EXACT_CANDIDATE') 'Approval statement classification mismatch.'
Assert-True ($statement.approvalDecision -eq 'APPROVED_FOR_FUTURE_BOUNDED_SANDBOX_EXECUTION') 'Approval decision mismatch.'
Assert-True ($statement.approvalSource -eq 'OperatorProvidedInCentralThread') 'Approval source mismatch.'
Assert-True ($statement.approvalScope -eq 'FutureBoundedSandboxExecutionOnly') 'Approval scope mismatch.'
Assert-True ($statement.notImmediateExecution -eq $true) 'Approval must not be immediate execution.'
Assert-True ($statement.requiresSeparateExecutionPackage -eq $true) 'Separate execution package must be required.'
Assert-True ($statement.noExecutionInThisPackage -eq $true) 'R010 must not execute.'
Assert-True ($statement.operatorApprovalId -eq $operatorApprovalId) 'OperatorApprovalId mismatch in approval statement.'
Assert-True ($statement.marketDataSnapshotId -eq $snapshotId) 'Approval statement snapshot mismatch.'
Assert-True ($statement.qubesOutputId -eq $qubesOutputId) 'Approval statement QubesOutputId mismatch.'
Assert-True ($statement.qubesOutputHash -eq $qubesOutputHash) 'Approval statement QubesOutputHash mismatch.'
Assert-True ($statement.r009RiskReviewArtifactHash -eq $riskHash) 'Approval statement risk hash mismatch.'
Assert-True ([decimal]$statement.quantities.AUDUSD.quantity -eq [decimal]'48.7') 'Approval AUDUSD quantity mismatch.'
Assert-True ([decimal]$statement.quantities.EURUSD.quantity -eq [decimal]'7.0') 'Approval EURUSD quantity mismatch.'
Assert-True ([decimal]$statement.quantities.GBPUSD.quantity -eq [decimal]'17.5') 'Approval GBPUSD quantity mismatch.'
Assert-True ($statement.noProduction -eq $true) 'Approval must prohibit production.'
Assert-True ($statement.noLedgerCommit -eq $true) 'Approval must prohibit ledger commit.'

$computedHash = Get-Sha256Hex $id.hashInputs.canonicalString
Assert-True ($id.classification -eq 'OPERATOR_APPROVAL_ID_CREATED') 'OperatorApprovalId classification mismatch.'
Assert-True ($id.fullHash -eq $computedHash) 'OperatorApprovalId full hash is not deterministic from canonical input.'
Assert-True ($id.operatorApprovalId -eq "pms-qubes-sandbox-operator-approval-r010:$($computedHash.Substring(0,16))") 'OperatorApprovalId does not match hash prefix.'
Assert-True ($id.operatorApprovalId -eq $operatorApprovalId) 'Unexpected OperatorApprovalId.'
Assert-True ($id.scope -eq 'FutureBoundedSandboxExecutionOnly') 'OperatorApprovalId scope mismatch.'
Assert-True ($id.notExecuted -eq $true) 'OperatorApprovalId artifact must remain non-executed.'

Assert-True ($guardrails.classification -eq 'APPROVAL_GUARDRAILS_READY_FOR_FUTURE_BOUNDED_SANDBOX_EXECUTION') 'Guardrails classification mismatch.'
Assert-True (($guardrails.forbidden -contains 'using production/live LMAX')) 'Production/live LMAX must be forbidden.'
Assert-True (($guardrails.forbidden -contains 'using offline Polygon BBO as accounting/production source')) 'Offline BBO production/accounting promotion must be forbidden.'

Assert-True ($preconditions.classification -eq 'FUTURE_EXECUTION_PRECONDITIONS_READY_WITH_WARNINGS') 'Future preconditions classification mismatch.'
Assert-True ($preconditions.operatorApprovalId -eq $operatorApprovalId) 'Precondition OperatorApprovalId mismatch.'
Assert-True ($preconditions.requiredExactCandidate.marketDataSnapshotId -eq $snapshotId) 'Precondition snapshot mismatch.'
Assert-True ($preconditions.requiredExactCandidate.qubesOutputId -eq $qubesOutputId) 'Precondition QubesOutputId mismatch.'
Assert-True ([decimal]$preconditions.requiredExactCandidate.symbolsSidesQuantities[0].quantity -eq [decimal]'48.7') 'Precondition AUDUSD quantity mismatch.'

Assert-True ($impact.classification -eq 'APPROVAL_CAPTURED_NO_EXECUTION_READINESS_ONLY') 'Readiness impact classification mismatch.'
Assert-True ($impact.r010RetroactivelyRelabelsR014AsQubesDriven -eq $false) 'R014 retroactively relabelled.'
Assert-True ($impact.acceptedGrossSandboxPnlV0Changed -eq $false) 'Gross sandbox PnL V0 changed.'
Assert-True ($impact.r010CreatesFills -eq $false) 'R010 created fills.'
Assert-True ($impact.r010CreatesOrders -eq $false) 'R010 created orders.'
Assert-True ($impact.r010CreatesLedgerCommits -eq $false) 'R010 created ledger commits.'
Assert-True ($impact.theoreticalPnlUnlocked -eq $false) 'Theoretical PnL unlocked.'
Assert-True ($impact.netPnlUnlocked -eq $false) 'Net PnL unlocked.'
Assert-True ($impact.accountingPnlUnlocked -eq $false) 'Accounting PnL unlocked.'
Assert-True ($impact.productionLiveReadinessUnlocked -eq $false) 'Production/live unlocked.'

function Get-ContractStatus {
    param([string]$Name)
    ($contracts.contracts | Where-Object { $_.contract -eq $Name } | Select-Object -First 1).status
}

Assert-True ((Get-ContractStatus 'pms-qubes-operator-approval.v1') -eq 'YES') 'pms-qubes-operator-approval.v1 must be YES.'
Assert-True ((Get-ContractStatus 'pms-qubes-risk-approval.v1') -eq 'YES') 'pms-qubes-risk-approval.v1 must be YES for future bounded sandbox scope.'
Assert-True ((Get-ContractStatus 'pms-execution-candidate.v1') -eq 'YES') 'pms-execution-candidate.v1 must be YES.'
Assert-True ((Get-ContractStatus 'pms-qubes-handoff.v1') -eq 'WITH_WARNINGS') 'pms-qubes-handoff.v1 must remain WITH_WARNINGS.'
Assert-True ((Get-ContractStatus 'canonical-marketdata-source.v1') -eq 'WITH_WARNINGS') 'canonical-marketdata-source.v1 must remain WITH_WARNINGS.'
Assert-True ((Get-ContractStatus 'marketdata-snapshot-contract.v1') -eq 'YES') 'marketdata-snapshot-contract.v1 must be YES.'
Assert-True ((Get-ContractStatus 'accounting-attribution.v1') -eq 'BLOCKED') 'accounting-attribution.v1 must remain BLOCKED.'
Assert-True ((Get-ContractStatus 'production-readiness.v1') -eq 'BLOCKED') 'production-readiness.v1 must remain BLOCKED.'
Assert-True ($contracts.internallyConsistent -eq $true) 'Contract statuses inconsistent.'

Assert-True ($tests.testStatus -eq 'PASSED') 'Focused R010 tests must be PASSED.'

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
Assert-True ($boundary.noInventedAccountId -eq $true) 'Invented AccountId.'
Assert-True ($boundary.noInventedPortfolioId -eq $true) 'Invented PortfolioId.'
Assert-True ($boundary.noInventedStrategyId -eq $true) 'Invented StrategyId.'
Assert-True ($boundary.noInventedSourceExecutionIntentId -eq $true) 'Invented SourceExecutionIntentId.'
Assert-True ($boundary.noInventedAccountCurrency -eq $true) 'Invented AccountCurrency.'
Assert-True ($boundary.noAccountingNetProductionPnlReadinessClaim -eq $true) 'Accounting/net/production PnL readiness claimed.'
Assert-True ($boundary.credentialValuesPrintedOrPersisted -eq $false) 'Credential values printed or persisted.'
Assert-True ($boundary.unsafeBoundaryCrossed -eq $false) 'Unsafe boundary crossed.'

Write-Host 'PMS-QUBES-SANDBOX-OPERATOR-APPROVAL-R010 gate passed.'
