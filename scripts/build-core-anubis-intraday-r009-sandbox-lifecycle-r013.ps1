param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$Package = "CORE-ANUBIS-INTRADAY-R009-SANDBOX-LIFECYCLE-R013"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r009-sandbox-lifecycle-r013"
$R012Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-operator-approval-r012"
$R008Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-lmax-fx-metadata-catalog-r008"
$ExecSandboxDir = Join-Path $RepoRoot "artifacts\readiness\execution-sandbox"
$OperatorApprovalId = "core-anubis-intraday-operator-approval-r012:419206468D9EEAA15DBD3975"
$CandidateId = "core-anubis-pms-quantity-preview-r010-refined:5E0F1277E153A728481987BD"
$RiskReviewId = "core-anubis-risk-review-r011:5EB84FF8EF3FC6EE8AE6F3E4"
$CoreHandoffManifestHash = "sha256:8A445CD6195458D09CC539C0C14895C1EFB09027A9BB58D1369BDDD44D99F182"
$NettedUsdWeightsHash = "sha256:41FF6745857F9E53E28278B7C8DAF91EF2306DE384B753B3BB605B5FD2D01A96"

function Write-JsonArtifact([string]$Name, [object]$Payload) {
    New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
    $Payload | ConvertTo-Json -Depth 80 | Set-Content -LiteralPath (Join-Path $ArtifactDir $Name) -Encoding UTF8
}

function Read-Json([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Dec($Value) {
    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) { return [decimal]0 }
    [decimal]::Parse([string]$Value, [Globalization.CultureInfo]::InvariantCulture)
}

function New-ShortHash([string]$InputText) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $bytes = [Text.Encoding]::UTF8.GetBytes($InputText)
    (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("X2") }) -join "").Substring(0,24)
}

function Find-Metadata($manifest, [string]$CoreSymbol) {
    $all = @()
    if ($manifest.DirectMetadata) { $all += @($manifest.DirectMetadata) }
    if ($manifest.InverseOrExecutionPairMetadata) { $all += @($manifest.InverseOrExecutionPairMetadata) }
    $all | Where-Object { $_.CoreSymbol -eq $CoreSymbol } | Select-Object -First 1
}

function Invert-Side([string]$Side) {
    if ($Side -eq "BUY") { return "SELL" }
    if ($Side -eq "SELL") { return "BUY" }
    return $Side
}

$r012Summary = Join-Path $R012Dir "summary.md"
$approvalId = Read-Json (Join-Path $R012Dir "operator-approval-id.json")
$binding = Read-Json (Join-Path $R012Dir "exact-approved-candidate-binding.json")
$disclosure = Read-Json (Join-Path $R012Dir "quantity-warning-disclosure-statement.json")
$preconditions = Read-Json (Join-Path $R012Dir "future-r013-execution-preconditions.json")
$guardrails = Read-Json (Join-Path $R012Dir "approval-guardrails.json")
$r012Boundary = Read-Json (Join-Path $R012Dir "boundary-safety-evidence.json")
$metadata = Read-Json (Join-Path $R008Dir "completed-core-fx-metadata-manifest.json")
$priorLifecycle = Read-Json (Join-Path $ExecSandboxDir "phase-exec-sandbox-r009-lifecycle-decision.json")
$priorIdempotency = Read-Json (Join-Path $ExecSandboxDir "phase-exec-sandbox-r009-idempotency-contract.json")

$r012Ready = (
    (Test-Path -LiteralPath $r012Summary) -and
    ([string]$approvalId.OperatorApprovalId -eq $OperatorApprovalId) -and
    ([string]$binding.CandidateId -eq $CandidateId) -and
    ([string]$binding.RiskReviewId -eq $RiskReviewId) -and
    ([string]$binding.CoreHandoffManifestHash -eq $CoreHandoffManifestHash) -and
    ([string]$binding.NettedUsdWeightsHash -eq $NettedUsdWeightsHash) -and
    ($r012Boundary.NoR009 -and $r012Boundary.NoDbMutation -and $r012Boundary.NoLedger -and $r012Boundary.NoR010PrototypeApprovalTransfer)
)

Write-JsonArtifact "r012-approval-intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "r012-approval-intake-validation"
    R012SummaryExists = Test-Path -LiteralPath $r012Summary
    OperatorApprovalId = [string]$approvalId.OperatorApprovalId
    OperatorApprovalIdMatchesExpected = ([string]$approvalId.OperatorApprovalId -eq $OperatorApprovalId)
    ExactApprovedCandidateBindingExists = Test-Path -LiteralPath (Join-Path $R012Dir "exact-approved-candidate-binding.json")
    QuantityWarningDisclosureExists = Test-Path -LiteralPath (Join-Path $R012Dir "quantity-warning-disclosure-statement.json")
    FutureR013PreconditionsExist = Test-Path -LiteralPath (Join-Path $R012Dir "future-r013-execution-preconditions.json")
    ApprovalGuardrailsExist = Test-Path -LiteralPath (Join-Path $R012Dir "approval-guardrails.json")
    R012DidNotExecuteR009 = $r012Boundary.NoR009
    R012DidNotMutateDbOrLedger = ($r012Boundary.NoDbMutation -and $r012Boundary.NoLedger)
    R012DidNotTransferR010PrototypeApproval = $r012Boundary.NoR010PrototypeApprovalTransfer
    Classification = if ($r012Ready) { "R012_APPROVAL_READY_FOR_R013_EXECUTION_GATE" } else { "R012_APPROVAL_INCOMPLETE" }
})

$expectedQuantityMap = [ordered]@{
    AUDUSD = "0"; CADUSD = "0.2"; CHFUSD = "0"; CNHUSD = "0.2"; EURUSD = "0"; GBPUSD = "0"; JPYUSD = "88.4"; MXNUSD = "1.1"; NOKUSD = "3.1"; NZDUSD = "0.1"; SEKUSD = "0.4"; SGDUSD = "0.5"; ZARUSD = "7.1"
}
$quantityMatches = $true
foreach ($symbol in $expectedQuantityMap.Keys) {
    $row = $binding.Quantities | Where-Object { $_.Symbol -eq $symbol } | Select-Object -First 1
    if ($null -eq $row -or [string]$row.Quantity -ne $expectedQuantityMap[$symbol]) { $quantityMatches = $false }
}
$zeroed = @("AUDUSD","CHFUSD","EURUSD","GBPUSD")
Write-JsonArtifact "approved-candidate-exactness-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "approved-candidate-exactness-validation"
    CandidateId = [string]$binding.CandidateId
    RiskReviewId = [string]$binding.RiskReviewId
    CoreHandoffManifestHash = [string]$binding.CoreHandoffManifestHash
    NettedUsdWeightsHash = [string]$binding.NettedUsdWeightsHash
    TargetNotionalAmount = $binding.TargetNotionalAmount
    Symbols = $binding.Symbols
    Sides = $binding.Sides
    Quantities = $binding.Quantities
    QuantitiesMatchExpected = $quantityMatches
    ZeroedBelowMinLines = $binding.ZeroedBelowMinSymbols
    OmittedExposureDisclosure = [ordered]@{ Usd = [string]$binding.OmittedExposureUsd; Pct = [string]$binding.OmittedExposurePct }
    JPYUSDCaveat = [string]$binding.JPYUSDCaveat
    R010Transferability = $binding.R010PrototypeTransferability
    Classification = if ($r012Ready -and $quantityMatches) { "APPROVED_CANDIDATE_EXACT_MATCH" } else { "APPROVED_CANDIDATE_MISMATCH" }
})

$nonZero = @()
foreach ($q in $binding.Quantities) {
    if ((Dec $q.Quantity) -gt 0) {
        $side = ($binding.Sides | Where-Object { $_.Symbol -eq $q.Symbol } | Select-Object -First 1).Side
        $nonZero += [ordered]@{ CoreSymbol = [string]$q.Symbol; CoreSide = [string]$side; CoreQuantity = [string]$q.Quantity }
    }
}

$mappingRows = @()
foreach ($line in $nonZero) {
    $meta = Find-Metadata $metadata $line.CoreSymbol
    $requiresInversion = $meta.Relationship -eq "INVERSE_OR_EXECUTION_PAIR"
    $executionSymbol = if ($requiresInversion) { [string]$meta.MetadataSourceSymbol } else { [string]$line.CoreSymbol }
    $executionSide = if ($requiresInversion) { Invert-Side $line.CoreSide } else { [string]$line.CoreSide }
    $ready = $null -ne $meta -and -not [string]::IsNullOrWhiteSpace([string]$meta.SecurityId)
    $mappingRows += [ordered]@{
        CoreSymbol = $line.CoreSymbol
        CoreSide = $line.CoreSide
        CoreQuantity = $line.CoreQuantity
        ExecutionSymbol = $executionSymbol
        ExecutionSide = $executionSide
        ExecutionQuantity = $line.CoreQuantity
        RequiresInversion = $requiresInversion
        MappingRule = if ($requiresInversion) { "Core $($line.CoreSide) $($line.CoreSymbol) maps to $executionSide $executionSymbol with same approved quantity." } else { "Direct Core model symbol is tradable as execution symbol." }
        SecurityId = if ($meta) { [string]$meta.SecurityId } else { $null }
        SecurityIdSource = if ($meta) { [string]$meta.SecurityIdSource } else { $null }
        ContractMultiplier = if ($meta) { $meta.ContractMultiplier } else { $null }
        MinOrderSize = if ($meta) { $meta.MinOrderSize } else { $null }
        MetadataSource = if ($meta) { [string]$metadata.FullCatalogPath } else { $null }
        ValidationStatus = if ($ready) { if ($requiresInversion) { "EXECUTION_MAPPING_READY_INVERSE" } else { "EXECUTION_MAPPING_READY_DIRECT" } } else { "EXECUTION_MAPPING_BLOCKED_MISSING_SECURITY" }
    }
}
$mappingBlocked = @($mappingRows | Where-Object { $_.ValidationStatus -like "EXECUTION_MAPPING_BLOCKED*" })
Write-JsonArtifact "execution-symbol-mapping-inversion-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "execution-symbol-mapping-inversion-validation"
    Rows = $mappingRows
    ZeroQuantityLinesExcludedFromMapping = $zeroed
    Classification = if ($mappingBlocked.Count -eq 0) { "EXECUTION_MAPPING_READY_ALL_NONZERO_LINES" } else { "EXECUTION_MAPPING_BLOCKED" }
})

$credentialNames = @("LMAX_DEMO_FIX_USERNAME","LMAX_DEMO_FIX_PASSWORD","LMAX_DEMO_SENDER_COMP_ID","LMAX_DEMO_TARGET_COMP_ID")
$credentialPresence = [ordered]@{}
foreach ($name in $credentialNames) {
    $credentialPresence[$name] = -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))
}
$credentialReady = @($credentialPresence.Values | Where-Object { $_ -eq $false }).Count -eq 0
$exactHarnessReady = $false
Write-JsonArtifact "sandbox-route-account-profile-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "sandbox-route-account-profile-validation"
    SandboxAccountProfile = "ExistingLmaxDemoProfile"
    Route = "LMAX sandbox/demo only"
    ProductionRouteDisabled = $true
    NoProductionAccount = $true
    CredentialVariableNames = $credentialNames
    CredentialVariablePresence = $credentialPresence
    CredentialValuesRedacted = $true
    ExactCoreAnubisMultiSymbolR013HarnessValidated = $exactHarnessReady
    NoAccountIdInvented = $true
    NoPortfolioIdInvented = $true
    NoStrategyIdInvented = $true
    NoSourceExecutionIntentIdInvented = $true
    NoAccountCurrencyInvented = $true
    ExecutionRemainsSandboxOnly = $true
    BlockReason = "Existing sandbox smoke lifecycle is not a validated exact-candidate R013 multi-symbol submission harness for this OperatorApprovalId."
    Classification = "SANDBOX_ROUTE_PROFILE_BLOCKED"
})

$lifecycleId = "core-anubis-r013:" + (New-ShortHash ($OperatorApprovalId + "|" + $CandidateId))
$idempotencyKey = $lifecycleId + "|open-flatten|v1"
Write-JsonArtifact "idempotency-duplicate-guard.json" ([ordered]@{
    Package = $Package
    Artifact = "idempotency-duplicate-guard"
    LifecycleId = $lifecycleId
    IdempotencyKey = $idempotencyKey
    OperatorApprovalId = $OperatorApprovalId
    CandidateId = $CandidateId
    OrderBatchId = "$lifecycleId:batch-001"
    PreviousR013RunWithSameKey = $false
    PriorSandboxLifecycleEvidence = [ordered]@{ Decision = $priorLifecycle.Decision; ExistingGenericIdempotency = $priorIdempotency.NoDuplicateSubmissionForSameIdempotencyKey }
    DuplicatePreventionPolicy = "Fail closed on same OperatorApprovalId/CandidateId lifecycle key unless controlled resume artifact exists."
    ResumePolicy = "No resume unless prior R013 partial state exists and is explicitly classified."
    FailClosed = $true
    Classification = "IDEMPOTENCY_DUPLICATE_GUARD_READY"
})

$gateClassification = "PRE_EXECUTION_GATE_BLOCKED_ROUTE_PROFILE"
Write-JsonArtifact "pre-execution-gate-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "pre-execution-gate-decision"
    ApprovalReady = $r012Ready
    CandidateExact = $quantityMatches
    ExecutionMappingReady = ($mappingBlocked.Count -eq 0)
    SandboxRouteProfileReady = $false
    IdempotencyReady = $true
    GatePassed = $false
    BlockReason = "Sandbox route/profile lacks validated exact-candidate R013 multi-symbol execution harness for this approval; no orders submitted."
    Classification = $gateClassification
})

Write-JsonArtifact "open-order-plan.json" ([ordered]@{
    Package = $Package
    Artifact = "open-order-plan"
    PlannedOrders = @()
    CandidateOrdersIfGateLaterPasses = $mappingRows
    ZeroQuantityExcluded = $true
    OperatorApprovalId = $OperatorApprovalId
    CandidateId = $CandidateId
    IdempotencyKey = $idempotencyKey
    Classification = "OPEN_ORDER_PLAN_NOT_CREATED_GATE_BLOCKED"
})

Write-JsonArtifact "flatten-plan.json" ([ordered]@{
    Package = $Package
    Artifact = "flatten-plan"
    FlattenOrders = @()
    PotentialFlattenIfGateLaterPasses = @($mappingRows | ForEach-Object {
        [ordered]@{
            CoreSymbol = $_.CoreSymbol
            ExecutionSymbol = $_.ExecutionSymbol
            FlattenSide = (Invert-Side $_.ExecutionSide)
            FlattenQuantity = $_.ExecutionQuantity
            ResidualTarget = "0.0"
            SandboxOnly = $true
            NoLedgerCommit = $true
        }
    })
    ResidualTarget = "0.0"
    Classification = "FLATTEN_PLAN_NOT_CREATED_GATE_BLOCKED"
})

Write-JsonArtifact "guarded-r009-sandbox-open-execution.json" ([ordered]@{
    Package = $Package
    Artifact = "guarded-r009-sandbox-open-execution"
    Started = $false
    OrdersSubmitted = @()
    ZeroQuantityOrdersSubmitted = 0
    Reason = "Pre-execution gate blocked route/profile before submission."
    Classification = "R009_SANDBOX_OPEN_NOT_EXECUTED_GATE_BLOCKED"
})

Write-JsonArtifact "guarded-sandbox-flatten-execution.json" ([ordered]@{
    Package = $Package
    Artifact = "guarded-sandbox-flatten-execution"
    Started = $false
    FlattenOrdersSubmitted = @()
    ResidualTarget = "0.0"
    Reason = "No open execution occurred."
    Classification = "SANDBOX_FLATTEN_NOT_EXECUTED_GATE_BLOCKED"
})

Write-JsonArtifact "sandbox-reconciliation.json" ([ordered]@{
    Package = $Package
    Artifact = "sandbox-reconciliation"
    ExpectedNonZeroOpenOrders = $mappingRows.Count
    ActualOpenOrders = 0
    ActualOpenFills = 0
    ActualFlattenOrders = 0
    ActualFlattenFills = 0
    Residuals = @()
    Breaks = @()
    Rejects = @()
    ZeroQuantityLinesCorrectlyExcluded = $true
    Classification = "SANDBOX_RECONCILIATION_NOT_RUN_GATE_BLOCKED"
})

Write-JsonArtifact "sandbox-gross-pnl-preview-r013.json" ([ordered]@{
    Package = $Package
    Artifact = "sandbox-gross-pnl-preview-r013"
    FillsExist = $false
    GrossOnly = $true
    QuoteCurrencyOnly = $true
    NoCosts = $true
    NoCommissions = $true
    NoFxConversion = $true
    NoAccountCurrencyAggregation = $true
    NoAccountingPnl = $true
    NoProductionPnl = $true
    NoLedgerCommit = $true
    Classification = "SANDBOX_GROSS_PNL_R013_NOT_APPLICABLE_NO_FILLS"
})

Write-JsonArtifact "paper-ledger-preview-update.json" ([ordered]@{
    Package = $Package
    Artifact = "paper-ledger-preview-update"
    PreviewLines = @()
    Commit = $false
    DbMutation = $false
    OperatorApprovalId = $OperatorApprovalId
    CandidateId = $CandidateId
    RiskReviewId = $RiskReviewId
    SandboxOnly = $true
    ProductionFill = $false
    Classification = "PAPER_LEDGER_PREVIEW_NOT_APPLICABLE_NO_FILLS"
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    Statuses = [ordered]@{
        "core-anubis-r013-sandbox-lifecycle.v1" = "BLOCKED_ROUTE_PROFILE"
        "pms-core-operator-approval.v1" = "YES"
        "pms-core-execution-candidate.v1" = "BLOCKED"
        "r009-execution-readiness.v1" = "BLOCKED_ROUTE_PROFILE"
        "sandbox-reconciliation.v1" = "NOT_RUN_GATE_BLOCKED"
        "pnl-preview.v1" = "YES_HISTORICAL_SANDBOX_GROSS_PNL_V0_ONLY"
        "ledger-preview.v1" = "NOT_APPLICABLE_NO_FILLS"
        "accounting-attribution.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
    }
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = $Package
    Artifact = "readiness-impact"
    R013Executed = $false
    GateBlocked = $true
    SandboxLifecycleSucceeded = $false
    ResidualsZero = $null
    GrossSandboxPnlPreviewComputed = $false
    LedgerPreviewCreated = $false
    NoAccountingNetProductionPnl = $true
    NoLedgerCommit = $true
    ProductionLiveRemainsBlocked = $true
    SandboxProgrammeAcceptedWithGrossPnlV0Ready = "unchanged"
    Classification = "R013_GATE_BLOCKED_NO_EXECUTION_READINESS_CHANGE"
})

Write-JsonArtifact "boundary-safety-evidence.json" ([ordered]@{
    Package = $Package
    Artifact = "boundary-safety-evidence"
    SandboxDemoOnly = $true
    NoProductionLiveLmax = $true
    NoProductionBrokerRoute = $true
    NoProductionOrderFillReport = $true
    NoLedgerCommit = $true
    NoAccountingLedgerMutation = $true
    NoProductionStateMutation = $true
    NoDbMutationOutsideAcceptedSandboxAuditPath = $true
    NoZeroQuantityOrderSubmitted = $true
    R010PrototypeApprovalNotReused = $true
    JPYUSDInversionHandledOrGateBlocked = $true
    NoAccountCurrencyAggregation = $true
    NoNetPnl = $true
    NoAccountingPnl = $true
    NoProductionPnl = $true
    R009ExecutionSubmitted = $false
    LmaxCallOccurred = $false
    Classification = "BOUNDARY_SAFETY_CONFIRMED_GATE_BLOCKED_NO_EXECUTION"
})

$summary = @"
# CORE-ANUBIS-INTRADAY-R009-SANDBOX-LIFECYCLE-R013

Classification: CORE_ANUBIS_INTRADAY_R009_SANDBOX_LIFECYCLE_R013_BLOCKED_ROUTE_OR_IDEMPOTENCY

Did pre-execution gate pass? no; blocked at sandbox route/profile because no validated exact-candidate R013 multi-symbol execution harness exists for this OperatorApprovalId.
Did R009 sandbox open orders run? no.
Did Anubis/Core candidate execute in sandbox? no.
Which orders/fills occurred? none.
Did flatten run? no.
Residuals? not applicable; no open fills.
Was gross sandbox PnL preview computed? no, not applicable without fills.
Was paper-ledger preview created? no, not applicable without fills; no commit.
Is production/live still blocked? yes.
What is the next package? NEXT_CORE_ANUBIS_INTRADAY_R013B_EXACT_SANDBOX_EXECUTION_HARNESS.
"@
$summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "CORE_ANUBIS_INTRADAY_R009_SANDBOX_LIFECYCLE_R013_BUILD_COMPLETE"
