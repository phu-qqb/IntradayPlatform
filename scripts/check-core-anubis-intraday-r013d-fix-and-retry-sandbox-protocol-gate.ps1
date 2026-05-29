param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013d-fix-and-retry-sandbox-protocol"
$Required = @(
    "r013c-intake-and-reject-evidence-validation.json",
    "rejected-fix-field-extraction.json",
    "prior-successful-fix-comparison.json",
    "r009-order-builder-adapter-protocol-audit.json",
    "lmax-metadata-vs-submitted-order-fields-audit.json",
    "root-cause-decision.json",
    "fix-design-and-approval-applicability.json",
    "technical-fix-application-evidence.json",
    "focused-tests-build-evidence.json",
    "corrected-exact-candidate-dry-run.json",
    "conditional-retry-pre-execution-gate.json",
    "guarded-sandbox-retry-open-execution.json",
    "guarded-sandbox-retry-flatten-execution.json",
    "sandbox-retry-reconciliation.json",
    "sandbox-gross-pnl-preview-r013d.json",
    "paper-ledger-preview-update.json",
    "contract-status-update.json",
    "readiness-impact.json",
    "boundary-safety-evidence.json",
    "summary.md"
)

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

Assert-True (Test-Path -LiteralPath $ArtifactDir) "R013D artifact directory missing."
foreach ($name in $Required) {
    Assert-True (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required R013D artifact: $name"
}

$intake = Read-Json "r013c-intake-and-reject-evidence-validation.json"
$extract = Read-Json "rejected-fix-field-extraction.json"
$comparison = Read-Json "prior-successful-fix-comparison.json"
$protocolAudit = Read-Json "r009-order-builder-adapter-protocol-audit.json"
$metadataAudit = Read-Json "lmax-metadata-vs-submitted-order-fields-audit.json"
$rootCause = Read-Json "root-cause-decision.json"
$fixDesign = Read-Json "fix-design-and-approval-applicability.json"
$fixEvidence = Read-Json "technical-fix-application-evidence.json"
$tests = Read-Json "focused-tests-build-evidence.json"
$dryRun = Read-Json "corrected-exact-candidate-dry-run.json"
$gate = Read-Json "conditional-retry-pre-execution-gate.json"
$retryOpen = Read-Json "guarded-sandbox-retry-open-execution.json"
$retryFlatten = Read-Json "guarded-sandbox-retry-flatten-execution.json"
$recon = Read-Json "sandbox-retry-reconciliation.json"
$pnl = Read-Json "sandbox-gross-pnl-preview-r013d.json"
$paperLedger = Read-Json "paper-ledger-preview-update.json"
$contract = Read-Json "contract-status-update.json"
$readiness = Read-Json "readiness-impact.json"
$boundary = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

Assert-True ($intake.Classification -in @("R013C_REJECT_EVIDENCE_READY","R013C_REJECT_EVIDENCE_READY_WITH_WARNINGS")) "R013C intake/reject evidence is not ready."
Assert-True ($intake.OpenSubmissionsAttempted -eq 9) "R013C must have attempted exactly 9 open submissions."
Assert-True ($intake.OpenFills -eq 0) "R013C must have zero fills for one safe retry."
Assert-True ($intake.ZeroQuantityOrdersSubmitted -eq 0) "R013C zero-quantity submission boundary failed."
Assert-True ($intake.NoLedgerCommit -eq $true -and $intake.NoProductionLive -eq $true) "R013C crossed ledger or production/live boundary."

Assert-True ($extract.OverallClassification -eq "REJECT_FIELDS_EXTRACTED_ALL_ORDERS") "Rejected FIX extraction incomplete."
Assert-True (@($extract.Orders).Count -eq 9) "Expected 9 rejected field rows."
Assert-True (@($extract.Orders | Where-Object { $_.SecurityIDSourceTag22Sent -ne "LMAX" }).Count -eq 0) "Expected prior R013C tag 22 value LMAX on all rows."
Assert-True (@($extract.Orders | Where-Object { [decimal]::Parse([string]$_.Quantity, [Globalization.CultureInfo]::InvariantCulture) -le 0 }).Count -eq 0) "Rejected field extraction contains zero quantity."

Assert-True ($comparison.Classification -eq "PRIOR_SUCCESS_COMPARISON_IDENTIFIES_TAG22_DELTA") "Prior successful comparison did not isolate tag22 delta."
Assert-True ($comparison.PriorSuccessfulTag22Value -eq "8" -and $comparison.R013CTag22Value -eq "LMAX") "Prior/R013C tag22 values are inconsistent."
Assert-True ($protocolAudit.Classification -in @("ORDER_BUILDER_TAG22_SERIALIZATION_BUG_FOUND","ORDER_BUILDER_METADATA_TO_FIX_MAPPING_GAP_FOUND")) "Protocol audit did not identify tag22/mapping bug."
Assert-True ($metadataAudit.Classification -eq "LMAX_METADATA_ORDER_FIELDS_MATCH_EXCEPT_TAG22_FORMAT") "Metadata/order audit must show only tag22 format mismatch."

Assert-True ($rootCause.Classification -eq "ROOT_CAUSE_TAG22_SECURITYIDSOURCE_FORMAT") "Root cause is not tag22 SecurityIDSource format."
Assert-True ($rootCause.CandidateEconomicsChange -eq $false -and $rootCause.OperatorApprovalChanges -eq $false -and $rootCause.R012ApprovalRemainsApplicable -eq $true) "Root cause changed economics/approval."
Assert-True ($fixDesign.Classification -eq "FIX_DESIGN_READY_R012_APPROVAL_STILL_APPLIES") "Fix design does not preserve R012 approval."
Assert-True ($fixDesign.NewOperatorApprovalRequired -eq $false -and $fixDesign.RetryAllowedInThisCombinedPackage -eq $true) "Fix requires new approval or blocks retry."
Assert-True ($fixEvidence.Classification -in @("TECHNICAL_FIX_APPLIED","TECHNICAL_FIX_APPLIED_WITH_WARNINGS")) "Technical fix not applied."
Assert-True ($fixEvidence.CandidateEconomicsUnchanged -eq $true -and $fixEvidence.RouteProfileUnchanged -eq $true -and $fixEvidence.NoSecretsAdded -eq $true) "Fix changed economics/route or added secrets."

Assert-True ($tests.Classification -in @("FOCUSED_TESTS_BUILD_PASS","FOCUSED_TESTS_BUILD_PASS_WITH_WARNINGS")) "Focused tests/build did not pass."
Assert-True ($dryRun.Classification -eq "CORRECTED_DRY_RUN_READY_FOR_CONDITIONAL_RETRY") "Corrected dry-run not ready."
Assert-True (@($dryRun.Orders).Count -eq 9) "Corrected dry-run must contain 9 orders."
Assert-True (@($dryRun.Orders | Where-Object { $_.CorrectedTag22Value -ne "8" -or $_.PriorR013CTag22Value -ne "LMAX" }).Count -eq 0) "Corrected dry-run has wrong tag22 values."
Assert-True ($dryRun.NoZeroQuantityLines -eq $true -and $dryRun.ExactApprovedQuantitiesUnchanged -eq $true -and $dryRun.NoUnapprovedSymbols -eq $true) "Corrected dry-run changed candidate or included zero/unapproved orders."

if ($gate.Classification -eq "CONDITIONAL_RETRY_GATE_PASS_READY_TO_SUBMIT_ONCE") {
    Assert-True ($retryOpen.Classification -ne "RETRY_OPEN_NOT_EXECUTED_GATE_BLOCKED") "Retry gate passed but open retry did not execute."
} else {
    Assert-True ($retryOpen.Classification -eq "RETRY_OPEN_NOT_EXECUTED_GATE_BLOCKED") "Retry open artifact inconsistent with blocked gate."
}

Assert-True ($retryOpen.ZeroQuantityOrdersSubmitted -eq 0) "Retry submitted a zero-quantity order."
if ($retryOpen.Started -eq $true) {
    Assert-True (@($retryOpen.Results).Count -eq 9) "Retry must submit exactly 9 open orders if it starts."
    Assert-True (@($retryOpen.Results | Where-Object { $_.CorrectedFixSecurityIdSource -ne "8" }).Count -eq 0) "Retry did not use corrected tag22=8."
}
if ($retryFlatten.Started -eq $true) {
    Assert-True (@($retryFlatten.Results).Count -le 9) "Retry flatten attempted too many orders."
}

if ($recon.Classification -eq "RETRY_RECONCILIATION_PASS_RESIDUAL_ZERO") {
    Assert-True (@($recon.Residuals | Where-Object { [decimal]::Parse([string]$_.ResidualSignedQuantity, [Globalization.CultureInfo]::InvariantCulture) -ne 0 }).Count -eq 0) "Residual-zero reconciliation has non-zero residual."
}
Assert-True ($pnl.GrossOnly -eq $true -and $pnl.QuoteCurrencyOnly -eq $true -and $pnl.NoAccountingPnl -eq $true -and $pnl.NoProductionPnl -eq $true -and $pnl.NoLedgerCommit -eq $true) "Gross-only PnL boundary failed."
Assert-True ($paperLedger.Commit -eq $false -and $paperLedger.ProductionFill -eq $false) "Paper ledger boundary failed."

Assert-True ($contract.Statuses."accounting-attribution.v1" -eq "BLOCKED" -and $contract.Statuses."production-readiness.v1" -eq "BLOCKED") "Accounting/production statuses must remain blocked."
if (($fixEvidence.CandidateEconomicsUnchanged -ne $true) -or ($fixDesign.NewOperatorApprovalRequired -eq $true)) {
    Assert-True ($contract.FinalClassification -eq "CORE_ANUBIS_INTRADAY_R013D_BLOCKED_NEW_APPROVAL_REQUIRED") "Economics/security route change must block and require new approval."
}
Assert-True ($readiness.NoAccountingNetProductionPnl -eq $true -and $readiness.NoLedgerCommit -eq $true -and $readiness.ProductionLiveRemainsBlocked -eq $true) "Readiness impact crossed forbidden boundary."

Assert-True ($boundary.NoProductionLiveLmax -eq $true -and $boundary.NoProductionBrokerRoute -eq $true -and $boundary.NoProductionOrderFillReport -eq $true) "Production/live boundary failed."
Assert-True ($boundary.NoLedgerCommit -eq $true -and $boundary.NoAccountingLedgerMutation -eq $true -and $boundary.NoProductionStateMutation -eq $true) "Ledger/production mutation boundary failed."
Assert-True ($boundary.NoZeroQuantityOrderSubmitted -eq $true) "Zero quantity boundary failed."
Assert-True ($boundary.R010PrototypeApprovalNotReused -eq $true -and $boundary.JPYUSDInversionHandled -eq $true) "R010/JPYUSD boundary failed."
Assert-True ($boundary.NoAccountCurrencyAggregation -eq $true -and $boundary.NoNetPnl -eq $true -and $boundary.NoAccountingPnl -eq $true -and $boundary.NoProductionPnl -eq $true) "Forbidden PnL/account aggregation boundary failed."

Assert-True ($summary.Contains("CORE_ANUBIS_INTRADAY_R013D_")) "Summary missing R013D final classification."
Assert-True ($summary.Contains("Is production/live still blocked? yes.")) "Summary must confirm production/live blocked."

Write-Host "CORE_ANUBIS_INTRADAY_R013D_FIX_AND_RETRY_SANDBOX_PROTOCOL_GATE_PASS"
