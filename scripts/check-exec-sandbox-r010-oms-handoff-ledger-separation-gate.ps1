$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"

function Fail {
    param([string]$Message)
    throw "EXEC-SANDBOX-R010 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$required = @(
    "phase-exec-sandbox-r010-summary.md",
    "phase-exec-sandbox-r010-r007-r008-r009-reference.json",
    "phase-exec-sandbox-r010-lifecycle-evidence-summary.json",
    "phase-exec-sandbox-r010-sandbox-oms-state-model-handoff.json",
    "phase-exec-sandbox-r010-state-transition-map.json",
    "phase-exec-sandbox-r010-allowed-state-transitions.json",
    "phase-exec-sandbox-r010-forbidden-state-transitions.json",
    "phase-exec-sandbox-r010-paper-ledger-separation-contract.json",
    "phase-exec-sandbox-r010-paper-ledger-preview-only-preservation.json",
    "phase-exec-sandbox-r010-idempotency-handoff-contract.json",
    "phase-exec-sandbox-r010-duplicate-prevention-handoff.json",
    "phase-exec-sandbox-r010-operator-handoff.json",
    "phase-exec-sandbox-r010-risk-handoff.json",
    "phase-exec-sandbox-r010-sandbox-expansion-prerequisites.json",
    "phase-exec-sandbox-r010-production-live-blockers.json",
    "phase-exec-sandbox-r010-handoff-decision.json",
    "phase-exec-sandbox-r010-no-secret-persistence-audit.json",
    "phase-exec-sandbox-r010-no-production-broker-audit.json",
    "phase-exec-sandbox-r010-no-production-order-audit.json",
    "phase-exec-sandbox-r010-no-production-route-audit.json",
    "phase-exec-sandbox-r010-no-production-fill-report-audit.json",
    "phase-exec-sandbox-r010-no-production-ledger-audit.json",
    "phase-exec-sandbox-r010-no-paper-ledger-commit-audit.json",
    "phase-exec-sandbox-r010-no-production-state-mutation-audit.json",
    "phase-exec-sandbox-r010-direct-cross-exclusion-preservation.json",
    "phase-exec-sandbox-r010-usd-pair-whitelist-preservation.json",
    "phase-exec-sandbox-r010-usdjpy-caveat-preservation.json",
    "phase-exec-sandbox-r010-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sandbox-r010-legacy-compatibility-preservation.json",
    "phase-exec-sandbox-r010-cost-guidance-preservation.json",
    "phase-exec-sandbox-r010-nonmajor-calibration-preservation.json",
    "phase-exec-sandbox-r010-forbidden-actions-audit.json",
    "phase-exec-sandbox-r010-next-phase-recommendation.json",
    "phase-exec-sandbox-r010-build-test-validator-evidence.json"
)
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $name))) { Fail "Required artifact missing: $name" }
}

$source = Get-Content -LiteralPath (Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009LmaxSandboxOrderSmoke.cs") -Raw
$tests = Get-Content -LiteralPath (Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009SandboxOmsHandoffLedgerSeparationTests.cs") -Raw
foreach ($token in @(
    "R009SandboxOmsHandoffContract",
    "R009SandboxStateTransitionMap",
    "R009SandboxPaperLedgerSeparationContract",
    "R009SandboxDuplicatePreventionHandoff",
    "BuildSandboxOmsHandoffContract",
    "BuildSandboxStateTransitionMap",
    "BuildSandboxPaperLedgerSeparationContract",
    "BuildDuplicatePreventionHandoff"
)) {
    if ($source -notmatch [regex]::Escape($token)) { Fail "Source missing R010 handoff token $token" }
}
foreach ($token in @(
    "Sandbox_oms_handoff_accepts_lifecycle_without_production_or_ledger_state",
    "State_transition_map_links_r007_r008_r009_evidence_to_sandbox_states_only",
    "Paper_ledger_separation_contract_allows_review_reference_but_blocks_mutation",
    "Duplicate_prevention_handoff_preserves_already_flattened_and_no_fallback_guards"
)) {
    if ($tests -notmatch [regex]::Escape($token)) { Fail "Focused R010 test missing $token" }
}

if (@(Get-ChildItem -LiteralPath $artifactDir -File -Filter "phase-exec-sandbox-r010-raw-*.json").Count -gt 0) { Fail "R010 created raw sandbox order artifacts despite artifact-only gate" }

$lifecycle = Read-Json "phase-exec-sandbox-r010-lifecycle-evidence-summary.json"
if ($lifecycle.SevenSymbolOpenCycle.Submitted -ne 7 -or $lifecycle.SevenSymbolOpenCycle.Filled -ne 7) { Fail "R007 open lifecycle summary invalid" }
if ($lifecycle.SevenSymbolFlattenCycle.Submitted -ne 7 -or $lifecycle.SevenSymbolFlattenCycle.Filled -ne 7 -or [decimal]$lifecycle.SevenSymbolFlattenCycle.ExpectedResidualQuantity -ne 0) { Fail "R008 flatten lifecycle summary invalid" }
if ($lifecycle.EurusdRepeatabilityCycle.OpenFilled -ne $true -or $lifecycle.EurusdRepeatabilityCycle.FlattenFilled -ne $true -or [decimal]$lifecycle.EurusdRepeatabilityCycle.ExpectedResidualQuantity -ne 0) { Fail "R009 repeatability summary invalid" }
if ($lifecycle.SandboxOnly -ne $true -or $lifecycle.ProductionOrderRouteFillReportLedgerStateMutation -ne $false) { Fail "Lifecycle summary unsafe" }

$handoff = Read-Json "phase-exec-sandbox-r010-sandbox-oms-state-model-handoff.json"
if ($handoff.Status -ne "Ready" -or $handoff.SandboxLifecycleAccepted -ne $true) { Fail "OMS handoff not ready" }
foreach ($property in @("ProductionOmsStateMutationAllowed", "PaperLedgerCommitAllowed", "ProductionLedgerCommitAllowed", "TradingStateMutationAllowed")) {
    if ($handoff.$property -ne $false) { Fail "OMS handoff enabled forbidden path $property" }
}
$map = Read-Json "phase-exec-sandbox-r010-state-transition-map.json"
if ($map.ProductionStateForbidden -ne $true -or $map.LedgerStateForbidden -ne $true -or $map.ProductionOmsStateMutationAllowed -ne $false) { Fail "State transition map unsafe" }
foreach ($state in @("SandboxIntentCreated", "SandboxSubmitted", "SandboxFilled", "SandboxFlattenSubmitted", "SandboxFlatConfirmed", "SandboxTerminal")) {
    if (-not $map.EvidenceByState.$state) { Fail "State transition map missing evidence for $state" }
}
$allowed = Read-Json "phase-exec-sandbox-r010-allowed-state-transitions.json"
if ($allowed.ProductionStateForbidden -ne $true -or $allowed.LedgerStateForbidden -ne $true -or @($allowed.AllowedTransitions).Count -lt 10) { Fail "Allowed transitions invalid" }
$forbiddenTransitions = Read-Json "phase-exec-sandbox-r010-forbidden-state-transitions.json"
foreach ($transition in @("SandboxFillToPaperLedgerCommit", "SandboxFillToProductionLedgerCommit", "SandboxFillToProductionTradingStateMutation", "SandboxTerminalToLiveProductionPromotion")) {
    if (@($forbiddenTransitions.ForbiddenTransitions) -notcontains $transition) { Fail "Forbidden transition missing: $transition" }
}

$paper = Read-Json "phase-exec-sandbox-r010-paper-ledger-separation-contract.json"
foreach ($property in @("PaperLedgerCommitAllowed", "ProductionLedgerCommitAllowed", "TradingStateMutationAllowed", "SandboxOrderLifecycleEqualsPaperLedgerCommit", "SandboxFillReportEqualsLedgerMutation", "SandboxFlatStateAuditEqualsLedgerCommit", "SandboxFillCanMutateLedger", "SandboxFillCanMutateProductionState")) {
    if ($paper.$property -ne $false) { Fail "Paper-ledger separation weakened: $property" }
}
if ($paper.SandboxFillCanBeReferencedForReview -ne $true -or $paper.PaperLedgerPreviewOnlyPreserved -ne $true) { Fail "Review-only paper-ledger preservation missing" }
$preview = Read-Json "phase-exec-sandbox-r010-paper-ledger-preview-only-preservation.json"
if ($preview.PaperLedgerPreviewOnlyPreserved -ne $true -or $preview.PaperLedgerCommitAllowed -ne $false -or $preview.LedgerMutationAllowed -ne $false -or $preview.TradingStateMutationAllowed -ne $false) { Fail "Paper-ledger preview-only preservation invalid" }

$idempotency = Read-Json "phase-exec-sandbox-r010-idempotency-handoff-contract.json"
foreach ($property in @("DuplicateClOrdIDPreventionPreserved", "SameIntentReplaySafe", "SameIntentDifferentQuantityConflict", "AlreadyFlattenedPositionRequiresExplicitNewSandboxApproval", "NoDuplicateSubmissionForSameIdempotencyKey", "NoProductionOrderFallback")) {
    if ($idempotency.$property -ne $true) { Fail "Idempotency handoff weakened: $property" }
}
$duplicate = Read-Json "phase-exec-sandbox-r010-duplicate-prevention-handoff.json"
foreach ($property in @("DuplicateClOrdIDRejected", "SameIntentReplaySafe", "SameIntentDifferentQuantityConflict", "AlreadyFlattenedReplayBlocked", "NoDuplicateSubmissionForSameIdempotencyKey", "NoProductionOrderFallback")) {
    if ($duplicate.$property -ne $true) { Fail "Duplicate-prevention handoff weakened: $property" }
}

$operator = Read-Json "phase-exec-sandbox-r010-operator-handoff.json"
if ($operator.ProductionStillBlocked -ne $true -or $operator.PaperLedgerCommitStillBlocked -ne $true -or $operator.ManualOperatorApprovalRequiredForFutureSandboxExpansion -ne $true -or $operator.NewOrdersSubmittedInR010 -ne $false) { Fail "Operator handoff unsafe" }
$risk = Read-Json "phase-exec-sandbox-r010-risk-handoff.json"
if ([decimal]$risk.SandboxQuantityValidated -ne 0.1 -or $risk.DirectCrossExecutionAllowed -ne $false -or $risk.NonmajorExecutionAllowed -ne $false -or $risk.ProductionRoutesBlocked -ne $true -or $risk.USDJPYCaveatPreserved -ne $true -or $risk.AudusdStatus -ne "SupportedAndNotFailed") { Fail "Risk handoff invalid" }
$blockers = Read-Json "phase-exec-sandbox-r010-production-live-blockers.json"
foreach ($property in @("ProductionLiveStillBlocked", "LmaxProductionBlocked", "ProductionCredentialsBlocked", "ProductionOrderBlocked", "ProductionRouteBlocked", "ProductionFillReportBlocked", "ProductionLedgerCommitBlocked", "PaperLedgerCommitBlocked", "ProductionStateMutationBlocked")) {
    if ($blockers.$property -ne $true) { Fail "Production blocker missing: $property" }
}
$decision = Read-Json "phase-exec-sandbox-r010-handoff-decision.json"
foreach ($expected in @("R009SandboxOmsHandoffReady", "R009SandboxPaperLedgerSeparationReady", "R009SandboxLifecycleAcceptedForFurtherSandboxExpansion", "ProductionLiveStillBlocked")) {
    if (@($decision.Decisions) -notcontains $expected) { Fail "Decision missing $expected" }
}
if ($decision.NewSandboxOrdersSubmitted -ne $false -or $decision.NotProductionApproval -ne $true -or $decision.NotPaperLedgerCommitApproval -ne $true) { Fail "Decision unsafe" }

$direct = Read-Json "phase-exec-sandbox-r010-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false -or $direct.EURGBPSubmitted -ne $false) { Fail "Direct-cross exclusion weakened" }
$whitelist = Read-Json "phase-exec-sandbox-r010-usd-pair-whitelist-preservation.json"
if ($whitelist.NonWhitelistedSymbolAllowed -ne $false -or $whitelist.AudusdStatus -ne "SupportedAndNotFailed" -or $whitelist.AudusdMisclassified -ne $false) { Fail "Whitelist/AUDUSD preservation failed" }
$usdjpy = Read-Json "phase-exec-sandbox-r010-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8" -or $usdjpy.CaveatPreserved -ne $true) { Fail "USDJPY caveat weakened" }
$legacy = Read-Json "phase-exec-sandbox-r010-legacy-compatibility-preservation.json"
if ($legacy.Legacy06AcceptedAsFutureCanonical -ne $false -or $legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 accepted as future canonical" }
$cost = Read-Json "phase-exec-sandbox-r010-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$nonmajor = Read-Json "phase-exec-sandbox-r010-nonmajor-calibration-preservation.json"
if ($nonmajor.SandboxExecutionAllowed -ne $false) { Fail "Nonmajor execution allowed" }

$forbidden = Read-Json "phase-exec-sandbox-r010-forbidden-actions-audit.json"
foreach ($property in @(
    "LmaxProductionUsed",
    "ProductionCredentialsUsed",
    "NonSandboxBrokerRouteUsed",
    "CredentialValuesPrintedOrPersisted",
    "PolygonCalled",
    "UnrelatedExternalApiCalled",
    "SchedulerServicePollingBackgroundJobIntroduced",
    "NewSandboxOrdersSubmittedWithoutExplicitNeed",
    "ProductionOrderArtifactCreated",
    "ProductionRouteArtifactCreated",
    "ProductionFillReportArtifactCreated",
    "ProductionLedgerCommitOccurred",
    "PaperLedgerCommitOccurred",
    "ProductionStateMutationOccurred",
    "SandboxFillsAllowedToMutateLedger",
    "SandboxFillsAllowedToMutateProductionState",
    "DuplicatePreventionWeakened",
    "AlreadyFlattenedProtectionWeakened",
    "DirectCrossExecutionAllowed",
    "NonWhitelistedSymbolAllowed",
    "Legacy06AcceptedAsFutureCanonical",
    "UsdjpyCaveatWeakened",
    "AudusdMisclassified"
)) {
    if ($forbidden.$property -ne $false) { Fail "Forbidden action observed or allowed: $property" }
}
if ($forbidden.SandboxArtifactsClearlyMarkedSandboxOnlyOrExistingLmaxDemoProfile -ne $true) { Fail "Sandbox artifacts not clearly marked" }

$secretAudit = Read-Json "phase-exec-sandbox-r010-no-secret-persistence-audit.json"
if ($secretAudit.SecretValuesSerialized -ne $false -or $secretAudit.CredentialVariableNamesOnly -ne $true -or $secretAudit.CredentialValuesRedacted -ne $true) { Fail "Secret persistence audit failed" }
$combined = (Get-ChildItem -LiteralPath $artifactDir -File -Filter "phase-exec-sandbox-r010-*.json" | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
foreach ($banned in @("QQ_LMAX_FIX_PASSWORD", "FixPassword`":`"", "FixUsername`":`"", "Password`":`"", "Username`":`"")) {
    if ($combined -match [regex]::Escape($banned)) { Fail "Possible credential value field persisted: $banned" }
}

$evidence = Read-Json "phase-exec-sandbox-r010-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedSandboxTests -ne "Passed") { Fail "Focused sandbox tests missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }
if ($evidence.NewSandboxOrdersSubmitted -ne $false) { Fail "R010 evidence says new sandbox orders were submitted" }

Write-Host "EXEC-SANDBOX-R010 validator passed."
