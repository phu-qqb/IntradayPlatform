param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013d-fix-and-retry-sandbox-protocol"
$R013CScript = Join-Path $RepoRoot "scripts\build-core-anubis-intraday-r013c-guarded-sandbox-execution.ps1"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$intake = Read-Json "r013c-intake-and-reject-evidence-validation.json"
$extract = Read-Json "rejected-fix-field-extraction.json"
$comparison = Read-Json "prior-successful-fix-comparison.json"
$audit = Read-Json "r009-order-builder-adapter-protocol-audit.json"
$metadataAudit = Read-Json "lmax-metadata-vs-submitted-order-fields-audit.json"
$rootCause = Read-Json "root-cause-decision.json"
$fixDesign = Read-Json "fix-design-and-approval-applicability.json"
$fixEvidence = Read-Json "technical-fix-application-evidence.json"
$dryRun = Read-Json "corrected-exact-candidate-dry-run.json"
$gate = Read-Json "conditional-retry-pre-execution-gate.json"
$boundary = Read-Json "boundary-safety-evidence.json"

Assert-True ($intake.Classification -eq "R013C_REJECT_EVIDENCE_READY") "R013C reject evidence is not ready."
Assert-True ($intake.OpenSubmissionsAttempted -eq 9 -and $intake.OpenFills -eq 0) "R013C intake must show 9 rejected open attempts and zero fills."
Assert-True ($intake.ZeroQuantityOrdersSubmitted -eq 0) "R013C submitted a zero quantity order."
Assert-True ($intake.RejectPatternMentionsTag22SecurityIdSource -eq $true) "R013C reject pattern does not isolate tag 22."

Assert-True ($extract.OverallClassification -eq "REJECT_FIELDS_EXTRACTED_ALL_ORDERS") "Rejected FIX fields were not fully extracted."
Assert-True (@($extract.Orders).Count -eq 9) "Expected 9 rejected order field rows."
Assert-True (@($extract.Orders | Where-Object { $_.SecurityIDSourceTag22Sent -ne "LMAX" }).Count -eq 0) "Prior R013C tag 22 should be LMAX for every row."
Assert-True (@($extract.Orders | Where-Object { [decimal]::Parse([string]$_.Quantity, [Globalization.CultureInfo]::InvariantCulture) -le 0 }).Count -eq 0) "Rejected extraction contains a zero quantity row."

Assert-True ($comparison.Classification -eq "PRIOR_SUCCESS_COMPARISON_IDENTIFIES_TAG22_DELTA") "Prior success comparison did not identify tag22 delta."
Assert-True ($comparison.PriorSuccessfulTag22Value -eq "8") "Prior successful tag 22 must be numeric 8."
Assert-True ($comparison.R013CTag22Value -eq "LMAX") "R013C tag 22 should be LMAX before the fix."

Assert-True ($audit.Classification -eq "ORDER_BUILDER_METADATA_TO_FIX_MAPPING_GAP_FOUND") "Protocol audit did not classify metadata-to-FIX mapping gap."
Assert-True ($audit.SecurityIDSourceSerializedExactlyAsCliValue -eq $true) "Adapter should serialize tag22 exactly as supplied."
Assert-True ($audit.R013CBuilderNowContainsResolverPatch -eq $true) "R013C builder resolver patch missing."
Assert-True ($metadataAudit.Classification -eq "LMAX_METADATA_ORDER_FIELDS_MATCH_EXCEPT_TAG22_FORMAT") "Metadata/order audit should show only tag22 mismatch."

Assert-True ($rootCause.Classification -eq "ROOT_CAUSE_TAG22_SECURITYIDSOURCE_FORMAT") "Root cause must be tag22 SecurityIDSource format."
Assert-True ($rootCause.Confidence -eq "HIGH") "Root cause confidence should be HIGH."
Assert-True ($rootCause.CandidateEconomicsChange -eq $false -and $rootCause.R012ApprovalRemainsApplicable -eq $true) "Root cause must leave economics and R012 approval intact."

Assert-True ($fixDesign.Classification -eq "FIX_DESIGN_READY_R012_APPROVAL_STILL_APPLIES") "Fix design must preserve R012 approval."
Assert-True ($fixDesign.NewOperatorApprovalRequired -eq $false) "Fix must not require new operator approval."
Assert-True ($fixDesign.ExpectedFixTag22Value -eq "8") "Expected FIX tag 22 must be 8."
Assert-True ($fixEvidence.Classification -eq "TECHNICAL_FIX_APPLIED") "Technical fix must be applied."
Assert-True ($fixEvidence.OnlyTag22OrFixSerializationPathChanged -eq $true) "Fix must be limited to tag22/FIX serialization."
Assert-True ($fixEvidence.CandidateEconomicsUnchanged -eq $true -and $fixEvidence.RouteProfileUnchanged -eq $true) "Fix changed economics or route."

Assert-True ($dryRun.Classification -eq "CORRECTED_DRY_RUN_READY_FOR_CONDITIONAL_RETRY") "Corrected dry-run not ready."
Assert-True (@($dryRun.Orders).Count -eq 9) "Corrected dry-run must contain exactly 9 orders."
Assert-True (@($dryRun.Orders | Where-Object { $_.PriorR013CTag22Value -ne "LMAX" -or $_.CorrectedTag22Value -ne "8" }).Count -eq 0) "Corrected dry-run tag22 values are wrong."
Assert-True (@($dryRun.Orders | Where-Object { [decimal]::Parse([string]$_.Quantity, [Globalization.CultureInfo]::InvariantCulture) -le 0 }).Count -eq 0) "Corrected dry-run contains zero quantity."
Assert-True ($dryRun.ExactApprovedQuantitiesUnchanged -eq $true -and $dryRun.NoUnapprovedSymbols -eq $true -and $dryRun.SandboxProfileUnchanged -eq $true) "Corrected dry-run changed candidate binding."

$scriptText = Get-Content -Raw -LiteralPath $R013CScript
Assert-True ($scriptText.Contains("Resolve-FixSecurityIdSource")) "R013C script missing SecurityIDSource resolver."
Assert-True ($scriptText.Contains('if ($SecurityIdSource -eq "LMAX") { return "8" }')) "R013C resolver does not map LMAX to 8."

if ($gate.TestsPass -eq $true) {
    Assert-True ($gate.Classification -eq "CONDITIONAL_RETRY_GATE_PASS_READY_TO_SUBMIT_ONCE" -or $gate.Classification -eq "CONDITIONAL_RETRY_GATE_BLOCKED_IDEMPOTENCY") "Retry gate has inconsistent pass/test state."
}

Assert-True ($boundary.NoProductionLiveLmax -eq $true -and $boundary.NoProductionBrokerRoute -eq $true) "Production/live boundary failed."
Assert-True ($boundary.NoLedgerCommit -eq $true -and $boundary.NoAccountingPnl -eq $true -and $boundary.NoProductionPnl -eq $true) "Ledger/accounting/production boundary failed."
Assert-True ($boundary.NoZeroQuantityOrderSubmitted -eq $true -and $boundary.R010PrototypeApprovalNotReused -eq $true) "Zero quantity or R010 transfer boundary failed."

Write-Host "CORE_ANUBIS_INTRADAY_R013D_FIX_AND_RETRY_SANDBOX_PROTOCOL_TESTS_PASS"
