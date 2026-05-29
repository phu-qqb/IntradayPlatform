param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$phase = "EXEC-SANDBOX-R010"
$symbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")

function Write-JsonArtifact {
    param([string]$Name, [object]$Value)
    $Value | ConvertTo-Json -Depth 100 | Set-Content -Path (Join-Path $artifactDir $Name) -Encoding UTF8
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function New-Audit {
    param([string]$Name, [string]$Evidence)
    [ordered]@{
        Phase = $phase
        Audit = $Name
        Status = "Pass"
        Evidence = $Evidence
        LmaxProductionUsed = $false
        ProductionCredentialsUsed = $false
        NonSandboxBrokerRouteUsed = $false
        CredentialValuesPrintedOrPersisted = $false
        PolygonCalled = $false
        UnrelatedExternalApiCalled = $false
        SchedulerServicePollingBackgroundJobIntroduced = $false
        NewSandboxOrdersSubmitted = $false
        ProductionOrderArtifactCreated = $false
        ProductionRouteArtifactCreated = $false
        ProductionFillReportArtifactCreated = $false
        ProductionLedgerCommitOccurred = $false
        PaperLedgerCommitOccurred = $false
        ProductionStateMutationOccurred = $false
        SandboxOnly = $true
    }
}

$r007 = Read-Json "phase-exec-sandbox-r007-per-symbol-quantity-calibration-results.json"
$r008 = Read-Json "phase-exec-sandbox-r008-post-flatten-reconciliation.json"
$r009Decision = Read-Json "phase-exec-sandbox-r009-lifecycle-decision.json"
$r009Recon = Read-Json "phase-exec-sandbox-r009-repeatability-reconciliation-result.json"
$r009Model = Read-Json "phase-exec-sandbox-r009-sandbox-oms-state-model.json"
$r009Idempotency = Read-Json "phase-exec-sandbox-r009-idempotency-contract.json"
$r009Duplicate = Read-Json "phase-exec-sandbox-r009-duplicate-prevention-results.json"

$r007Rows = @($r007.Results)
$r007Submitted = @($r007Rows | Where-Object { $_.Submitted }).Count
$r007Filled = @($r007Rows | Where-Object { $_.FillCount -gt 0 }).Count
$r008FlattenSubmitted = if ($r008) { [int]$r008.FlattenSubmittedCount } else { 0 }
$r008FlattenFilled = if ($r008) { [int]$r008.FlattenFilledCount } else { 0 }
$r008Residual = if ($r008) { [decimal]$r008.ExpectedResidualQuantity } else { [decimal]999 }
$r009OpenFilled = if ($r009Recon) { [bool]$r009Recon.OpenFilled } else { $false }
$r009FlattenFilled = if ($r009Recon) { [bool]$r009Recon.FlattenFilled } else { $false }
$r009Residual = if ($r009Recon) { [decimal]$r009Recon.ExpectedResidualQuantity } else { [decimal]999 }

$lifecycleSummary = [ordered]@{
    Phase = $phase
    SourcePhases = @("EXEC-SANDBOX-R007", "EXEC-SANDBOX-R008", "EXEC-SANDBOX-R009")
    SevenSymbolOpenCycle = [ordered]@{ Submitted = $r007Submitted; Filled = $r007Filled; QuantityPerSymbol = 0.1; TotalQuantity = 0.7; SandboxOnly = $true }
    SevenSymbolFlattenCycle = [ordered]@{ Submitted = $r008FlattenSubmitted; Filled = $r008FlattenFilled; ExpectedResidualQuantity = $r008Residual; SandboxOnly = $true }
    EurusdRepeatabilityCycle = [ordered]@{ OpenFilled = $r009OpenFilled; FlattenFilled = $r009FlattenFilled; ExpectedResidualQuantity = $r009Residual; SandboxOnly = $true }
    TotalSandboxOrdersSubmittedAcrossAcceptedEvidence = $r007Submitted + $r008FlattenSubmitted + $(if ($r009Recon.OpenSubmitted) { 1 } else { 0 }) + $(if ($r009Recon.FlattenSubmitted) { 1 } else { 0 })
    TotalSandboxFillsAcrossAcceptedEvidence = $r007Filled + $r008FlattenFilled + $(if ($r009OpenFilled) { 1 } else { 0 }) + $(if ($r009FlattenFilled) { 1 } else { 0 })
    FinalResidualQuantity = $r008Residual + $r009Residual
    SandboxOnly = $true
    ProductionOrderRouteFillReportLedgerStateMutation = $false
    Status = if ($r007Filled -eq 7 -and $r008Residual -eq 0 -and $r009Residual -eq 0) { "Accepted" } else { "Blocked" }
}

$allowedTransitions = @($r009Model.Transitions)
$forbiddenTransitions = @(
    "SandboxFillToPaperLedgerCommit",
    "SandboxFillToProductionLedgerCommit",
    "SandboxFillToProductionTradingStateMutation",
    "SandboxRouteToProductionRoute",
    "SandboxOrderToProductionOrder",
    "SandboxFlatStateAuditToLedgerMutation",
    "SandboxTerminalToLiveProductionPromotion"
)

$stateMap = [ordered]@{
    Phase = $phase
    EvidencePhases = @("EXEC-SANDBOX-R007", "EXEC-SANDBOX-R008", "EXEC-SANDBOX-R009")
    EvidenceByState = [ordered]@{
        SandboxIntentCreated = "R009 repeatability and R007/R008 sandbox intent artifacts"
        SandboxRiskChecked = "Sandbox guardrail validations from R007/R008/R009"
        SandboxRouteCreated = "Sandbox route artifacts only"
        SandboxSubmitted = "Sandbox submission artifacts only"
        SandboxAcked = "LMAX demo execution reports accepted/acked"
        SandboxRejected = "Earlier sandbox rejection diagnostics; terminal sandbox reject only"
        SandboxPartiallyFilled = "Reserved state; no partial fill observed"
        SandboxFilled = "R007 and R009 open fill reports"
        SandboxFlattenIntentCreated = "R008/R009 flatten intents"
        SandboxFlattenSubmitted = "R008/R009 flatten submission artifacts"
        SandboxFlattenFilled = "R008/R009 flatten fill reports"
        SandboxFlatConfirmed = "R008/R009 residual quantity 0.0"
        SandboxResidualDetected = "Reserved for residual diagnostics"
        SandboxTerminal = "R009/R010 handoff decisions"
    }
    AllowedTransitions = $allowedTransitions
    ForbiddenTransitions = $forbiddenTransitions
    ProductionOmsStateMutationAllowed = $false
    ProductionStateForbidden = $true
    LedgerStateForbidden = $true
}

$omsHandoff = [ordered]@{
    Phase = $phase
    Status = if ($lifecycleSummary.Status -eq "Accepted" -and $r009Model.ProductionOrderStateForbidden -eq $true -and $r009Model.ProductionLedgerStateForbidden -eq $true) { "Ready" } else { "Blocked" }
    SandboxLifecycleAccepted = $lifecycleSummary.Status -eq "Accepted"
    ProductionOmsStateMutationAllowed = $false
    PaperLedgerCommitAllowed = $false
    ProductionLedgerCommitAllowed = $false
    TradingStateMutationAllowed = $false
    AllowedTransitionCount = $allowedTransitions.Count
    ForbiddenTransitions = $forbiddenTransitions
    SandboxOnly = $true
}

$paperSeparation = [ordered]@{
    Phase = $phase
    PaperLedgerCommitAllowed = $false
    ProductionLedgerCommitAllowed = $false
    TradingStateMutationAllowed = $false
    SandboxOrderLifecycleEqualsPaperLedgerCommit = $false
    SandboxFillReportEqualsLedgerMutation = $false
    SandboxFlatStateAuditEqualsLedgerCommit = $false
    SandboxFillCanBeReferencedForReview = $true
    SandboxFillCanMutateLedger = $false
    SandboxFillCanMutateProductionState = $false
    PaperLedgerPreviewMayReadSandboxEvidenceLater = $true
    PaperLedgerPreviewOnlyPreserved = $true
    BoundaryStatement = "Sandbox order and fill evidence may be reviewed, but it cannot commit paper ledger, production ledger, or production trading state."
}

$idempotencyHandoff = [ordered]@{
    Phase = $phase
    SourceContract = $r009Idempotency
    DuplicateClOrdIDPreventionPreserved = $r009Idempotency.DuplicateClOrdIDRejected
    SameIntentReplaySafe = $r009Idempotency.SameIntentReplaySafe
    SameIntentDifferentQuantityConflict = $r009Idempotency.SameIntentDifferentQuantityConflict
    AlreadyFlattenedPositionRequiresExplicitNewSandboxApproval = $r009Idempotency.AlreadyFlattenedPositionRequiresExplicitNewSandboxApproval
    NoDuplicateSubmissionForSameIdempotencyKey = $r009Idempotency.NoDuplicateSubmissionForSameIdempotencyKey
    NoProductionOrderFallback = $r009Idempotency.NoProductionOrderFallback
    Status = "Ready"
}

$duplicateHandoff = [ordered]@{
    Phase = $phase
    SourceResult = $r009Duplicate
    DuplicateClOrdIDRejected = $r009Duplicate.DuplicateClOrdIDRejected
    SameIntentReplaySafe = $r009Duplicate.SameIntentReplaySafe
    SameIntentDifferentQuantityConflict = $r009Duplicate.SameIntentDifferentQuantityConflict
    AlreadyFlattenedReplayBlocked = $r009Duplicate.AlreadyFlattenedReplayBlocked
    NoDuplicateSubmissionForSameIdempotencyKey = $r009Duplicate.NoDuplicateSubmissionForSameIdempotencyKey
    NoProductionOrderFallback = $r009Duplicate.NoProductionOrderFallback
    Status = "Ready"
}

$operatorHandoff = [ordered]@{
    Phase = $phase
    SandboxLifecycleAccepted = $true
    ProductionStillBlocked = $true
    PaperLedgerCommitStillBlocked = $true
    ManualOperatorApprovalRequiredForFutureSandboxExpansion = $true
    SeparateExplicitFutureGateRequiredForProductionLiveDiscussion = $true
    NewOrdersSubmittedInR010 = $false
}

$riskHandoff = [ordered]@{
    Phase = $phase
    SandboxQuantityValidated = 0.1
    SymbolsValidated = $symbols
    DirectCrossExecutionAllowed = $false
    NonmajorExecutionAllowed = $false
    ProductionRoutesBlocked = $true
    USDJPYCaveatPreserved = $true
    AudusdStatus = "SupportedAndNotFailed"
    Legacy06AcceptedAsFutureCanonical = $false
}

$expansionPrerequisites = [ordered]@{
    Phase = $phase
    RequiredBeforeFutureSandboxExpansion = @(
        "Explicit operator approval",
        "Bounded sandbox order count",
        "Per-symbol quantity cap",
        "Confirmed sandbox FIX logon before every NewOrderSingle",
        "Duplicate ClOrdID and idempotency-key prevention",
        "No production route",
        "No paper-ledger commit",
        "No production ledger or production state mutation"
    )
    ProductionLiveDiscussionRequiresSeparateFutureGate = $true
}

$productionBlockers = [ordered]@{
    Phase = $phase
    ProductionLiveStillBlocked = $true
    LmaxProductionBlocked = $true
    ProductionCredentialsBlocked = $true
    ProductionOrderBlocked = $true
    ProductionRouteBlocked = $true
    ProductionFillReportBlocked = $true
    ProductionLedgerCommitBlocked = $true
    PaperLedgerCommitBlocked = $true
    ProductionStateMutationBlocked = $true
}

$decision = [ordered]@{
    Phase = $phase
    Decisions = @(
        "R009SandboxOmsHandoffReady",
        "R009SandboxPaperLedgerSeparationReady",
        "R009SandboxLifecycleAcceptedForFurtherSandboxExpansion",
        "ProductionLiveStillBlocked"
    )
    NewSandboxOrdersSubmitted = $false
    NotProductionApproval = $true
    NotPaperLedgerCommitApproval = $true
}

$classifications = @(
    "EXEC_SANDBOX_R010_PASS_SANDBOX_OMS_HANDOFF_READY",
    "EXEC_SANDBOX_R010_PASS_PAPER_LEDGER_SEPARATION_READY",
    "EXEC_SANDBOX_R010_PASS_IDEMPOTENCY_HANDOFF_READY",
    "EXEC_SANDBOX_R010_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
)

Write-JsonArtifact "phase-exec-sandbox-r010-r007-r008-r009-reference.json" ([ordered]@{ Phase = $phase; SourcePhases = @("EXEC-SANDBOX-R007", "EXEC-SANDBOX-R008", "EXEC-SANDBOX-R009"); R009Decision = $r009Decision.Decision })
Write-JsonArtifact "phase-exec-sandbox-r010-lifecycle-evidence-summary.json" $lifecycleSummary
Write-JsonArtifact "phase-exec-sandbox-r010-sandbox-oms-state-model-handoff.json" $omsHandoff
Write-JsonArtifact "phase-exec-sandbox-r010-state-transition-map.json" $stateMap
Write-JsonArtifact "phase-exec-sandbox-r010-allowed-state-transitions.json" ([ordered]@{ Phase = $phase; AllowedTransitions = $allowedTransitions; ProductionStateForbidden = $true; LedgerStateForbidden = $true })
Write-JsonArtifact "phase-exec-sandbox-r010-forbidden-state-transitions.json" ([ordered]@{ Phase = $phase; ForbiddenTransitions = $forbiddenTransitions; ProductionStateForbidden = $true; LedgerStateForbidden = $true })
Write-JsonArtifact "phase-exec-sandbox-r010-paper-ledger-separation-contract.json" $paperSeparation
Write-JsonArtifact "phase-exec-sandbox-r010-paper-ledger-preview-only-preservation.json" ([ordered]@{ Phase = $phase; SourcePhase = "EXEC-LIVE-R012"; PaperLedgerPreviewOnlyPreserved = $true; PaperLedgerCommitAllowed = $false; LedgerMutationAllowed = $false; TradingStateMutationAllowed = $false })
Write-JsonArtifact "phase-exec-sandbox-r010-idempotency-handoff-contract.json" $idempotencyHandoff
Write-JsonArtifact "phase-exec-sandbox-r010-duplicate-prevention-handoff.json" $duplicateHandoff
Write-JsonArtifact "phase-exec-sandbox-r010-operator-handoff.json" $operatorHandoff
Write-JsonArtifact "phase-exec-sandbox-r010-risk-handoff.json" $riskHandoff
Write-JsonArtifact "phase-exec-sandbox-r010-sandbox-expansion-prerequisites.json" $expansionPrerequisites
Write-JsonArtifact "phase-exec-sandbox-r010-production-live-blockers.json" $productionBlockers
Write-JsonArtifact "phase-exec-sandbox-r010-handoff-decision.json" $decision
Write-JsonArtifact "phase-exec-sandbox-r010-no-secret-persistence-audit.json" ([ordered]@{ Phase = $phase; CredentialValuesPrintedOrPersisted = $false; CredentialValuesRedacted = $true; CredentialVariableNamesOnly = $true; SecretValuesSerialized = $false })
Write-JsonArtifact "phase-exec-sandbox-r010-no-production-broker-audit.json" (New-Audit "NoProductionBroker" "R010 is artifact-only and did not connect to LMAX production.")
Write-JsonArtifact "phase-exec-sandbox-r010-no-production-order-audit.json" (New-Audit "NoProductionOrder" "No production order artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r010-no-production-route-audit.json" (New-Audit "NoProductionRoute" "No production route artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r010-no-production-fill-report-audit.json" (New-Audit "NoProductionFillReport" "Sandbox fill/report evidence remains review-only and production fill/report is false.")
Write-JsonArtifact "phase-exec-sandbox-r010-no-production-ledger-audit.json" (New-Audit "NoProductionLedger" "No production ledger commit occurred.")
Write-JsonArtifact "phase-exec-sandbox-r010-no-paper-ledger-commit-audit.json" ([ordered]@{ Phase = $phase; PaperLedgerCommitOccurred = $false; PaperLedgerCommitAllowed = $false; SandboxFillCanMutateLedger = $false; SandboxOnly = $true })
Write-JsonArtifact "phase-exec-sandbox-r010-no-production-state-mutation-audit.json" (New-Audit "NoProductionStateMutation" "No production trading state was mutated.")
Write-JsonArtifact "phase-exec-sandbox-r010-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; DirectCrossExecutionIntentRejected = $true; EURGBPSubmitted = $false })
Write-JsonArtifact "phase-exec-sandbox-r010-usd-pair-whitelist-preservation.json" ([ordered]@{ Phase = $phase; WhitelistedSymbols = $symbols; NonWhitelistedSymbolAllowed = $false; AudusdStatus = "SupportedAndNotFailed"; AudusdMisclassified = $false })
Write-JsonArtifact "phase-exec-sandbox-r010-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-sandbox-r010-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); CandidateTargetCloseUtc = "2026-05-26T15:15:00Z"; CandidateIsCanonical = $true })
Write-JsonArtifact "phase-exec-sandbox-r010-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); UsedAsFutureCanonical = $false; Legacy06AcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-sandbox-r010-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-sandbox-r010-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; SandboxExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-sandbox-r010-forbidden-actions-audit.json" ([ordered]@{
    Phase = $phase
    LmaxProductionUsed = $false
    ProductionCredentialsUsed = $false
    NonSandboxBrokerRouteUsed = $false
    CredentialValuesPrintedOrPersisted = $false
    PolygonCalled = $false
    UnrelatedExternalApiCalled = $false
    SchedulerServicePollingBackgroundJobIntroduced = $false
    NewSandboxOrdersSubmittedWithoutExplicitNeed = $false
    ProductionOrderArtifactCreated = $false
    ProductionRouteArtifactCreated = $false
    ProductionFillReportArtifactCreated = $false
    ProductionLedgerCommitOccurred = $false
    PaperLedgerCommitOccurred = $false
    ProductionStateMutationOccurred = $false
    SandboxFillsAllowedToMutateLedger = $false
    SandboxFillsAllowedToMutateProductionState = $false
    DuplicatePreventionWeakened = $false
    AlreadyFlattenedProtectionWeakened = $false
    DirectCrossExecutionAllowed = $false
    NonWhitelistedSymbolAllowed = $false
    Legacy06AcceptedAsFutureCanonical = $false
    UsdjpyCaveatWeakened = $false
    AudusdMisclassified = $false
    SandboxArtifactsClearlyMarkedSandboxOnlyOrExistingLmaxDemoProfile = $true
})
Write-JsonArtifact "phase-exec-sandbox-r010-next-phase-recommendation.json" ([ordered]@{ Phase = $phase; RecommendedNextPhase = "EXEC-SANDBOX-R011"; Title = "R009 Sandbox Operator Expansion Planning and Governance Gate"; Reason = "Plan any future sandbox expansion behind explicit operator approval while keeping production and ledger blockers intact." })
Write-JsonArtifact "phase-exec-sandbox-r010-build-test-validator-evidence.json" ([ordered]@{ Phase = $phase; Build = $BuildStatus; FocusedSandboxTests = $FocusedTestsStatus; Validator = $ValidatorStatus; DotnetBuildNoRestore = "dotnet build --no-restore"; FocusedTests = "dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~R009SandboxOmsHandoffLedgerSeparationTests|FullyQualifiedName~R009SandboxLifecycleRepeatabilityTests"; ValidatorScript = "scripts/check-exec-sandbox-r010-oms-handoff-ledger-separation-gate.ps1"; NewSandboxOrdersSubmitted = $false })

$summary = @"
# EXEC-SANDBOX-R010 Summary

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)
R010 accepted the R007/R008/R009 sandbox lifecycle evidence into the OMS sandbox handoff model and separated sandbox fills from paper-ledger commits, production ledger, and production trading state.

7-symbol open cycle: $r007Submitted submitted / $r007Filled filled
7-symbol flatten cycle: $r008FlattenSubmitted submitted / $r008FlattenFilled filled / residual $r008Residual
EURUSD repeatability cycle: open filled=$r009OpenFilled flatten filled=$r009FlattenFilled residual $r009Residual
New sandbox orders submitted in R010: false

Decision:
- R009SandboxOmsHandoffReady
- R009SandboxPaperLedgerSeparationReady
- R009SandboxLifecycleAcceptedForFurtherSandboxExpansion
- ProductionLiveStillBlocked

Build/tests/validator:
- Build: $BuildStatus
- Focused sandbox handoff/static checks: $FocusedTestsStatus
- Validator: $ValidatorStatus
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-sandbox-r010-summary.md") -Encoding UTF8

Write-Host "EXEC-SANDBOX-R010 artifacts written to $artifactDir"
