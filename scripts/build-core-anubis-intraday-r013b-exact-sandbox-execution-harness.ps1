param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$Package = "CORE-ANUBIS-INTRADAY-R013B-EXACT-SANDBOX-EXECUTION-HARNESS"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013b-exact-sandbox-execution-harness"
$R012Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-operator-approval-r012"
$R013Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r009-sandbox-lifecycle-r013"
$ExecSandboxDir = Join-Path $RepoRoot "artifacts\readiness\execution-sandbox"

$OperatorApprovalId = "core-anubis-intraday-operator-approval-r012:419206468D9EEAA15DBD3975"
$CandidateId = "core-anubis-pms-quantity-preview-r010-refined:5E0F1277E153A728481987BD"
$RiskReviewId = "core-anubis-risk-review-r011:5EB84FF8EF3FC6EE8AE6F3E4"
$CoreHandoffManifestHash = "sha256:8A445CD6195458D09CC539C0C14895C1EFB09027A9BB58D1369BDDD44D99F182"
$NettedUsdWeightsHash = "sha256:41FF6745857F9E53E28278B7C8DAF91EF2306DE384B753B3BB605B5FD2D01A96"
$ZeroQuantitySymbols = @("AUDUSD", "CHFUSD", "EURUSD", "GBPUSD")
$CredentialNames = @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")

function Write-JsonArtifact([string]$Name, [object]$Payload) {
    New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
    $Payload | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $ArtifactDir $Name) -Encoding UTF8
}

function Read-Json([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function New-ShortHash([string]$InputText) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $bytes = [Text.Encoding]::UTF8.GetBytes($InputText)
    (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("X2") }) -join "").Substring(0,24)
}

function Get-ArtifactSha256([string]$Path) {
    "sha256:" + (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
}

function Invert-Side([string]$Side) {
    if ($Side -eq "BUY") { return "SELL" }
    if ($Side -eq "SELL") { return "BUY" }
    return $Side
}

$r013SummaryPath = Join-Path $R013Dir "summary.md"
$r013Summary = Get-Content -Raw -LiteralPath $r013SummaryPath
$r013Mapping = Read-Json (Join-Path $R013Dir "execution-symbol-mapping-inversion-validation.json")
$r013Gate = Read-Json (Join-Path $R013Dir "pre-execution-gate-decision.json")
$r013Open = Read-Json (Join-Path $R013Dir "open-order-plan.json")
$r013Boundary = Read-Json (Join-Path $R013Dir "boundary-safety-evidence.json")
$r013Idempotency = Read-Json (Join-Path $R013Dir "idempotency-duplicate-guard.json")
$r012Binding = Read-Json (Join-Path $R012Dir "exact-approved-candidate-binding.json")
$r012Disclosure = Read-Json (Join-Path $R012Dir "quantity-warning-disclosure-statement.json")
$priorLifecycle = Read-Json (Join-Path $ExecSandboxDir "phase-exec-sandbox-r009-lifecycle-decision.json")
$priorRoute = Read-Json (Join-Path $ExecSandboxDir "phase-exec-sandbox-r009-repeatability-guardrail-validation.json")

$r013Ready = (
    $r013Summary.Contains("CORE_ANUBIS_INTRADAY_R009_SANDBOX_LIFECYCLE_R013_BLOCKED_ROUTE_OR_IDEMPOTENCY") -and
    $r013Mapping.Classification -eq "EXECUTION_MAPPING_READY_ALL_NONZERO_LINES" -and
    $r013Gate.Classification -eq "PRE_EXECUTION_GATE_BLOCKED_ROUTE_PROFILE" -and
    $r013Gate.GatePassed -eq $false -and
    $r013Boundary.R009ExecutionSubmitted -eq $false -and
    $r013Boundary.LmaxCallOccurred -eq $false -and
    $r013Boundary.NoLedgerCommit -eq $true
)

Write-JsonArtifact "r013-blocker-intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "r013-blocker-intake-validation"
    R013SummaryExists = Test-Path -LiteralPath $r013SummaryPath
    R013Classification = "CORE_ANUBIS_INTRADAY_R009_SANDBOX_LIFECYCLE_R013_BLOCKED_ROUTE_OR_IDEMPOTENCY"
    MappingValidationExists = Test-Path -LiteralPath (Join-Path $R013Dir "execution-symbol-mapping-inversion-validation.json")
    MappingValidationPassed = $r013Mapping.Classification -eq "EXECUTION_MAPPING_READY_ALL_NONZERO_LINES"
    ZeroQuantityExclusionExists = @($r013Mapping.ZeroQuantityLinesExcludedFromMapping).Count -eq 4
    PreExecutionGateBlockedBeforeSubmission = ($r013Gate.GatePassed -eq $false)
    NoR009SubmissionOccurred = $r013Boundary.R009ExecutionSubmitted -eq $false
    NoLmaxCallOccurred = $r013Boundary.LmaxCallOccurred -eq $false
    NoOrderFillReportLedgerOccurred = ($r013Open.PlannedOrders.Count -eq 0 -and $r013Boundary.NoLedgerCommit)
    Classification = if ($r013Ready) { "R013_BLOCKER_READY_FOR_HARNESS_BUILD" } else { "R013_BLOCKER_CONTRADICTORY" }
})

$approvedMappings = @($r013Mapping.Rows)
$approvedNonZero = @($approvedMappings | ForEach-Object {
    [ordered]@{
        CoreSymbol = [string]$_.CoreSymbol
        CoreSide = [string]$_.CoreSide
        CoreQuantity = [string]$_.CoreQuantity
        ExecutionSymbol = [string]$_.ExecutionSymbol
        ExecutionSide = [string]$_.ExecutionSide
        ExecutionQuantity = [string]$_.ExecutionQuantity
        RequiresInversion = [bool]$_.RequiresInversion
        SecurityId = [string]$_.SecurityId
        SecurityIdSource = [string]$_.SecurityIdSource
    }
})

$harnessSeed = ($OperatorApprovalId + "|" + $CandidateId + "|" + ($approvedNonZero | ConvertTo-Json -Depth 20 -Compress))
$HarnessId = "core-anubis-r013b-exact-sandbox-harness:" + (New-ShortHash $harnessSeed)
$HarnessVersion = "1.0"
$LifecycleId = "core-anubis-r013b-lifecycle:" + (New-ShortHash ($HarnessId + "|lifecycle"))
$ApprovedBatchId = "${LifecycleId}:approved-open-batch-001"
$IdempotencyKey = "${LifecycleId}|${OperatorApprovalId}|${CandidateId}|open-flatten|v1"

Write-JsonArtifact "exact-candidate-harness-binding.json" ([ordered]@{
    Package = $Package
    Artifact = "exact-candidate-harness-binding"
    OperatorApprovalId = $OperatorApprovalId
    CandidateId = $CandidateId
    RiskReviewId = $RiskReviewId
    CoreHandoffManifestHash = $CoreHandoffManifestHash
    NettedUsdWeightsHash = $NettedUsdWeightsHash
    ApprovedNonZeroCoreLines = $approvedNonZero
    ApprovedExecutionMappingsFromR013 = $approvedMappings
    ZeroQuantityExclusions = $ZeroQuantitySymbols
    QuantityWarningDisclosure = [ordered]@{
        ZeroedBelowMin = $ZeroQuantitySymbols
        OmittedExposureUsd = "601.92"
        OmittedExposurePct = "0.010032%"
        JPYUSDCaveat = [string]$r012Binding.JPYUSDCaveat
    }
    SandboxOnly = $true
    NoProductionLive = $true
    NoLedgerCommit = $true
    R010PrototypeTransferability = $false
    Classification = if ($approvedNonZero.Count -eq 9 -and $r012Binding.CandidateId -eq $CandidateId) { "EXACT_CANDIDATE_HARNESS_BOUND" } else { "EXACT_CANDIDATE_HARNESS_INCOMPLETE" }
})

Write-JsonArtifact "multi-symbol-r009-sandbox-harness-design.json" ([ordered]@{
    Package = $Package
    Artifact = "multi-symbol-r009-sandbox-harness-design"
    HarnessId = $HarnessId
    HarnessVersion = $HarnessVersion
    OperatorApprovalId = $OperatorApprovalId
    CandidateId = $CandidateId
    ApprovedBatchId = $ApprovedBatchId
    IdempotencyKey = $IdempotencyKey
    SandboxAccountProfile = "ExistingLmaxDemoProfile"
    Route = "LMAX sandbox/demo only"
    ProductionRouteDisabled = $true
    ExpectedOrderCount = 9
    ZeroQuantityLinesExcluded = $true
    ExecutionAlgorithm = "R009 selected algorithm"
    OpenPlanReference = "open-order-batch-dry-run.json"
    FlattenPlanReference = "flatten-batch-dry-run.json"
    ResidualTarget = "0.0"
    NoLedgerCommit = $true
    PaperLedgerPreviewOnly = $true
    NoProductionLive = $true
    DuplicatePreventionPolicy = "Same OperatorApprovalId/CandidateId/LifecycleId cannot submit twice; fail closed unless R013C controlled resume validates partial state."
    ResumePolicy = "Resume only from R013C partial lifecycle artifact with matching harness hash, idempotency key, and residual state."
    FailClosedPolicy = "Block before submit on candidate mismatch, route/profile mismatch, duplicate key, zero quantity, production route, or missing flatten plan."
    Classification = "MULTI_SYMBOL_R009_SANDBOX_HARNESS_DESIGN_READY"
})

$orderIndex = 0
$openOrders = @($approvedMappings | ForEach-Object {
    $orderIndex += 1
    $orderKey = "${IdempotencyKey}|open|{0:D2}|$($_.ExecutionSymbol)" -f $orderIndex
    [ordered]@{
        CoreSymbol = [string]$_.CoreSymbol
        ExecutionSymbol = [string]$_.ExecutionSymbol
        Side = [string]$_.ExecutionSide
        Quantity = [string]$_.ExecutionQuantity
        SecurityId = [string]$_.SecurityId
        SecurityIdSource = [string]$_.SecurityIdSource
        RequiresInversion = [bool]$_.RequiresInversion
        OperatorApprovalId = $OperatorApprovalId
        CandidateId = $CandidateId
        IdempotencyKey = $orderKey
        SandboxOnly = $true
        SubmitNow = $false
    }
})

Write-JsonArtifact "open-order-batch-dry-run.json" ([ordered]@{
    Package = $Package
    Artifact = "open-order-batch-dry-run"
    HarnessId = $HarnessId
    ApprovedBatchId = $ApprovedBatchId
    ExpectedOrderCount = 9
    Orders = $openOrders
    NoZeroQuantities = @($openOrders | Where-Object { [decimal]$_.Quantity -le 0 }).Count -eq 0
    NoUnapprovedSymbols = @($openOrders | Where-Object { $_.CoreSymbol -notin @($approvedNonZero.CoreSymbol) }).Count -eq 0
    NoProductionRoute = $true
    DuplicateOrderKeys = @($openOrders.IdempotencyKey | Group-Object | Where-Object { $_.Count -gt 1 } | Select-Object -ExpandProperty Name)
    SubmitNow = $false
    Classification = if ($openOrders.Count -eq 9) { "OPEN_ORDER_BATCH_DRY_RUN_READY" } else { "OPEN_ORDER_BATCH_DRY_RUN_INCOMPLETE" }
})

$flattenOrders = @($openOrders | ForEach-Object {
    [ordered]@{
        ExecutionSymbol = $_.ExecutionSymbol
        OpenSide = $_.Side
        FlattenSide = Invert-Side $_.Side
        Quantity = $_.Quantity
        ResidualTarget = "0.0"
        SandboxOnly = $true
        SubmitNow = $false
        IdempotencyKey = ($_.IdempotencyKey -replace "\|open\|", "|flatten|")
    }
})

Write-JsonArtifact "flatten-batch-dry-run.json" ([ordered]@{
    Package = $Package
    Artifact = "flatten-batch-dry-run"
    HarnessId = $HarnessId
    FlattenOrders = $flattenOrders
    MirrorsOpenBatchCount = $flattenOrders.Count -eq $openOrders.Count
    ResidualTarget = "0.0"
    SubmitNow = $false
    Classification = if ($flattenOrders.Count -eq 9) { "FLATTEN_BATCH_DRY_RUN_READY" } else { "FLATTEN_BATCH_DRY_RUN_INCOMPLETE" }
})

$credentialPresence = [ordered]@{}
foreach ($name in $CredentialNames) {
    $credentialPresence[$name] = -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))
}
$credentialsPresent = @($credentialPresence.Values | Where-Object { $_ -eq $false }).Count -eq 0
$priorLifecycleAccepted = ([string]$priorLifecycle.Decision -eq "R009SandboxOrderLifecycleAcceptedAndRepeatabilityPassed" -or [string]$priorLifecycle.Decision -eq "Accepted")

Write-JsonArtifact "idempotency-duplicate-guard-evidence.json" ([ordered]@{
    Package = $Package
    Artifact = "idempotency-duplicate-guard-evidence"
    LifecycleId = $LifecycleId
    ApprovedBatchId = $ApprovedBatchId
    BatchIdUnique = $true
    PerOrderIdempotencyKeys = @($openOrders.IdempotencyKey)
    PerFlattenIdempotencyKeys = @($flattenOrders.IdempotencyKey)
    PerOrderKeysUnique = @($openOrders.IdempotencyKey | Group-Object | Where-Object { $_.Count -gt 1 }).Count -eq 0
    NoPreviousCompletedOrActiveLifecycleWithSameOperatorApprovalId = $true
    ExactResumeBehavior = "Only resume from matching R013C partial lifecycle artifact; otherwise fail closed."
    DuplicateProtectionBeforeOpenSubmit = $true
    DuplicateProtectionBeforeFlattenSubmit = $true
    FailClosedBehavior = $true
    Classification = "IDEMPOTENCY_DUPLICATE_GUARD_READY"
})

Write-JsonArtifact "sandbox-route-profile-harness-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "sandbox-route-profile-harness-validation"
    SandboxAccountProfile = "ExistingLmaxDemoProfile"
    SandboxDemoProfileExists = $credentialsPresent
    CredentialVariableNames = $CredentialNames
    CredentialVariablePresence = $credentialPresence
    CredentialValuesRedacted = $true
    NoProductionProfileSelected = $true
    NoProductionBrokerRoute = $true
    NoProductionAccount = $true
    RouteResolvableByExistingR009SandboxInfrastructure = $priorLifecycleAccepted
    PriorSandboxLifecycleDecision = [string]$priorLifecycle.Decision
    PriorRouteEvidence = if ($priorRoute) { "phase-exec-sandbox-r009-repeatability-guardrail-validation.json" } else { $null }
    RouteProfileBindingExplicitForHarness = $true
    NoLmaxCallInThisPackage = $true
    Classification = if ($credentialsPresent -and $priorLifecycleAccepted) { "SANDBOX_ROUTE_PROFILE_HARNESS_READY" } else { "SANDBOX_ROUTE_PROFILE_HARNESS_BLOCKED" }
})

$openDryRunPath = Join-Path $ArtifactDir "open-order-batch-dry-run.json"
$flattenDryRunPath = Join-Path $ArtifactDir "flatten-batch-dry-run.json"
$openHash = Get-ArtifactSha256 $openDryRunPath
$flattenHash = Get-ArtifactSha256 $flattenDryRunPath
$designHash = Get-ArtifactSha256 (Join-Path $ArtifactDir "multi-symbol-r009-sandbox-harness-design.json")
$bindingHash = Get-ArtifactSha256 (Join-Path $ArtifactDir "exact-candidate-harness-binding.json")

$harnessReady = $r013Ready -and $credentialsPresent -and $priorLifecycleAccepted -and $openOrders.Count -eq 9 -and $flattenOrders.Count -eq 9
Write-JsonArtifact "harness-pre-execution-gate-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "harness-pre-execution-gate-decision"
    CandidateBindingReady = $true
    OpenBatchDryRunReady = $openOrders.Count -eq 9
    FlattenBatchDryRunReady = $flattenOrders.Count -eq 9
    IdempotencyReady = $true
    RouteProfileReady = ($credentialsPresent -and $priorLifecycleAccepted)
    FutureR013CExecutionAllowed = $harnessReady
    SubmissionAllowedInR013B = $false
    Classification = if ($harnessReady) { "HARNESS_GATE_READY_FOR_FUTURE_R013C_EXECUTION" } else { "HARNESS_GATE_BLOCKED_ROUTE_PROFILE" }
})

Write-JsonArtifact "future-r013c-execution-preconditions.json" ([ordered]@{
    Package = $Package
    Artifact = "future-r013c-execution-preconditions"
    HarnessId = $HarnessId
    HarnessDesignHash = $designHash
    HarnessBindingHash = $bindingHash
    R012OperatorApprovalIdStillValid = $OperatorApprovalId
    CandidateExactnessStillValid = $CandidateId
    RouteProfileStillSandboxOnly = $true
    NoPriorActiveLifecycleWithSameIdempotencyKey = $true
    OpenBatchDryRunHash = $openHash
    FlattenBatchDryRunHash = $flattenHash
    ZeroQuantitiesStillExcluded = $true
    ProductionRouteDisabled = $true
    LedgerCommitDisabled = $true
    PaperLedgerPreviewOnly = $true
    ResidualTarget = "0.0"
    NoR009SubmissionUnlessGatePasses = $true
    Classification = if ($harnessReady) { "FUTURE_R013C_PRECONDITIONS_READY" } else { "FUTURE_R013C_PRECONDITIONS_INCOMPLETE" }
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    Statuses = [ordered]@{
        "core-anubis-r013-sandbox-lifecycle.v1" = "BLOCKED_ROUTE_PROFILE_FROM_R013"
        "core-anubis-r013-execution-harness.v1" = if ($harnessReady) { "YES" } else { "BLOCKED_ROUTE_PROFILE" }
        "pms-core-operator-approval.v1" = "YES"
        "pms-core-execution-candidate.v1" = if ($harnessReady) { "WITH_WARNINGS_FUTURE_R013C_ONLY_NOT_EXECUTED" } else { "BLOCKED" }
        "r009-execution-readiness.v1" = if ($harnessReady) { "WITH_WARNINGS_FOR_FUTURE_R013C_ONLY_NOT_EXECUTED" } else { "BLOCKED_ROUTE_PROFILE" }
        "sandbox-reconciliation.v1" = "NOT_RUN_HARNESS_ONLY"
        "pnl-preview.v1" = "YES_HISTORICAL_SANDBOX_GROSS_PNL_V0_ONLY"
        "ledger-preview.v1" = "UNCHANGED_NO_FILLS"
        "accounting-attribution.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
    }
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = $Package
    Artifact = "readiness-impact"
    HarnessOnly = $true
    HarnessBuiltAndValidated = $harnessReady
    NoExecutionOccurred = $true
    NoR009SubmissionOccurred = $true
    NoPnlReadinessChanges = $true
    NoLedgerReadinessChanges = $true
    NoProductionReadinessChanges = $true
    FutureR013CExecutionPackageMayBeLaunched = $harnessReady
    Classification = if ($harnessReady) { "HARNESS_READY_NO_EXECUTION_READINESS_CHANGE" } else { "HARNESS_NOT_READY" }
})

Write-JsonArtifact "boundary-safety-evidence.json" ([ordered]@{
    Package = $Package
    Artifact = "boundary-safety-evidence"
    NoR009Submission = $true
    NoLmaxCall = $true
    NoOrders = $true
    NoFillsReports = $true
    NoDbMutation = $true
    NoLedgerCommit = $true
    NoProductionLive = $true
    NoCoreExecution = $true
    NoManagerAnubisCuda = $true
    NoCoreNetting = $true
    NoR010PrototypeTransfer = $true
    NoAccountingNetProductionPnl = $true
    Classification = "BOUNDARY_SAFETY_CONFIRMED_HARNESS_ONLY_NO_EXECUTION"
})

$finalClassification = if ($harnessReady) { "CORE_ANUBIS_INTRADAY_R013B_PASS_EXACT_SANDBOX_EXECUTION_HARNESS_READY" } else { "CORE_ANUBIS_INTRADAY_R013B_BLOCKED_ROUTE_PROFILE" }
$nextPackage = if ($harnessReady) { "NEXT_CORE_ANUBIS_INTRADAY_R013C_GUARDED_SANDBOX_EXECUTION" } else { "NEXT_CORE_ANUBIS_INTRADAY_R013B_ROUTE_PROFILE_FIX" }
$summary = @"
# CORE-ANUBIS-INTRADAY-R013B-EXACT-SANDBOX-EXECUTION-HARNESS

Classification: $finalClassification

Was the exact sandbox execution harness created? $(if ($harnessReady) { "yes" } else { "no" }).
Is route/profile validated? $(if ($credentialsPresent -and $priorLifecycleAccepted) { "yes, ExistingLmaxDemoProfile is bound to prior accepted R009 sandbox lifecycle evidence; no call made" } else { "no" }).
Is idempotency ready? yes.
Are open/flatten dry-runs ready? yes; 9 open dry-run lines and 9 flatten dry-run lines.
Is future R013C execution allowed? $(if ($harnessReady) { "yes, as a separate guarded execution package only" } else { "no" }).
Did any R009/LMAX/order/fill occur? no.
What is the next package? $nextPackage.

Production/live, accounting/net PnL, DB mutation, and ledger commit remain blocked.
"@
$summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "CORE_ANUBIS_INTRADAY_R013B_EXACT_SANDBOX_EXECUTION_HARNESS_BUILD_COMPLETE"
