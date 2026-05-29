param()

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()

function Add-Result([string]$Category, [string]$Check, [string]$Status, [string]$Detail) {
    $script:results += [ordered]@{
        category = $Category
        check = $Check
        status = $Status
        detail = $Detail
    }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail)
}

Write-Host "LMAX Read-Only Runtime Phase 6B Instrument Allowlist Gate"
Write-Host "Planning-only. No LMAX connection, no credentials, no replay, no scheduler/polling, no orders, and no mutation."

$allowlistFile = Join-Path $repoRoot "src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyInstrumentAllowlist.cs"
$testFile = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyInstrumentAllowlistValidatorTests.cs"
$phase6Plan = Join-Path $repoRoot "docs/LMAX_READONLY_RUNTIME_PHASE6_OPERATIONALIZATION_PLAN.md"
$phase6Checklist = Join-Path $repoRoot "docs/LMAX_READONLY_RUNTIME_PHASE6_BOUNDARY_CHECKLIST.md"
$apiProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"
$workerProgram = Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"

if ((Test-Path -LiteralPath $allowlistFile) -and (Select-String -Path $allowlistFile -Pattern "LmaxReadOnlyInstrumentAllowlistValidator" -SimpleMatch -Quiet)) {
    Add-Result "Files" "Allowlist model and validator exist" "PASS" "LmaxReadOnlyInstrumentAllowlist.cs is present."
} else {
    Add-Result "Files" "Allowlist model and validator exist" "FAIL" "Missing allowlist model or validator."
}

if ((Test-Path -LiteralPath $testFile) -and (Select-String -Path $testFile -Pattern "Default_candidate_allowlist_validates_as_planning_pass" -SimpleMatch -Quiet)) {
    Add-Result "Tests" "Allowlist validator tests exist" "PASS" "Unit tests cover allowlist presence and planning validation."
} else {
    Add-Result "Tests" "Allowlist validator tests exist" "FAIL" "Missing allowlist validator tests."
}

$allowlistText = if (Test-Path -LiteralPath $allowlistFile) { Get-Content -Raw -LiteralPath $allowlistFile } else { "" }
$candidateSymbols = @("GBPUSD", "USDJPY", "EURGBP", "AUDUSD")
foreach ($symbol in $candidateSymbols) {
    if ($allowlistText.Contains($symbol)) {
        Add-Result "Allowlist" "Candidate $symbol exists" "PASS" "$symbol is documented as an additional planning candidate."
    } else {
        Add-Result "Allowlist" "Candidate $symbol exists" "FAIL" "$symbol is missing from the candidate allowlist."
    }
}

if ($allowlistText.Contains("TBD-LMAX-DEMO-") -and $allowlistText.Contains("CandidateRequiresDemoSecurityIdConfirmation")) {
    Add-Result "Allowlist" "SecurityID confirmation boundary" "PASS" "Additional instruments are planning candidates and require Demo SecurityID confirmation before any run."
} else {
    Add-Result "Allowlist" "SecurityID confirmation boundary" "FAIL" "Candidate SecurityID confirmation boundary is missing."
}

if ($allowlistText.Contains("EvidenceMode: RequiredEvidenceMode") -and $allowlistText.Contains('RequiredEvidenceMode = "MarketDataOnly"')) {
    Add-Result "Evidence" "MarketDataOnly evidence rule" "PASS" "Allowlist entries are constrained to MarketDataOnly evidence previews."
} else {
    Add-Result "Evidence" "MarketDataOnly evidence rule" "FAIL" "MarketDataOnly evidence rule is missing."
}

if ($allowlistText.Contains("IsApprovedForExternalRun: false") -and $allowlistText.Contains("ExternalConnectionAllowedByThisPhase: false")) {
    Add-Result "Runtime" "No external run approval" "PASS" "Phase 6B candidates are not approved for external runs."
} else {
    Add-Result "Runtime" "No external run approval" "FAIL" "External run approval guard is missing."
}

$unsafeMarkers = @{
    "SchedulerAllowed: false" = "Scheduler forbidden"
    "PollingAllowed: false" = "Polling forbidden"
    "RuntimeShadowReplaySubmitAllowed: false" = "Runtime shadow replay submit forbidden"
    "OrderSubmissionAllowed: false" = "Order submission forbidden"
    "GatewayRegistrationAllowed: false" = "Gateway registration forbidden"
    "TradingMutationAllowed: false" = "Trading mutation forbidden"
    "CredentialValuesAllowed: false" = "Credential values forbidden"
}
foreach ($marker in $unsafeMarkers.Keys) {
    if ($allowlistText.Contains($marker)) {
        Add-Result "Safety" $unsafeMarkers[$marker] "PASS" $marker
    } else {
        Add-Result "Safety" $unsafeMarkers[$marker] "FAIL" "Missing marker: $marker"
    }
}

if ((Select-String -Path $phase6Plan -Pattern "Manual Additional MarketData Instrument Allowlist Design" -SimpleMatch -Quiet) -and
    (Select-String -Path $phase6Plan -Pattern "No External Run" -SimpleMatch -Quiet)) {
    Add-Result "Docs" "Phase 6B plan section exists" "PASS" "Phase 6 plan documents the recommended allowlist boundary."
} else {
    Add-Result "Docs" "Phase 6B plan section exists" "FAIL" "Phase 6B plan section is missing."
}

if ((Select-String -Path $phase6Checklist -Pattern "allowlist" -CaseSensitive:$false -Quiet) -and
    (Select-String -Path $phase6Checklist -Pattern "No External Run" -SimpleMatch -Quiet)) {
    Add-Result "Docs" "Phase 6 checklist references allowlist" "PASS" "Boundary checklist references allowlist rules."
} else {
    Add-Result "Docs" "Phase 6 checklist references allowlist" "FAIL" "Boundary checklist does not reference Phase 6B allowlist rules."
}

$registrationHits = @(Select-String -Path $apiProgram,$workerProgram -Pattern "LmaxReadOnlySocketPrototype","RealLmaxGateway","LmaxVenueGatewaySkeleton","ExternalReadOnlyPrototypeGateway" -SimpleMatch -ErrorAction SilentlyContinue)
if ($registrationHits.Count -eq 0 -and (Select-String -Path $apiProgram -Pattern "FakeLmaxGateway" -SimpleMatch -Quiet)) {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No prototype or real gateway registration found."
} else {
    Add-Result "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" (($registrationHits | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
}

Add-Result "Runtime" "External LMAX connection" "PASS" "No external connection is made by this allowlist gate."
Add-Result "Replay" "Shadow replay" "PASS" "No replay is submitted by this allowlist gate."
Add-Result "Mutation" "Trading state" "PASS" "No trading state is mutated by this allowlist gate."

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) { "FAIL" } elseif ($warnings.Count -gt 0) { "PASS_WITH_KNOWN_WARNINGS" } else { "PASS" }

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir "phase6b-instrument-allowlist-gate.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    finalDecision = $decision
    phase = "6B"
    scope = "Manual Additional MarketData Instrument Allowlist Design, No External Run"
    candidateInstrumentCount = $candidateSymbols.Count
    candidateInstruments = $candidateSymbols
    externalConnectionAttempted = $false
    runtimeShadowReplaySubmit = $false
    schedulerOrPollingAdded = $false
    orderSubmissionAdded = $false
    gatewayRegistrationAdded = $false
    tradingMutationAdded = $false
    results = $results
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ""
Write-Host "FinalDecision: $decision"
Write-Host "Report: $reportPath"

if ($decision -eq "FAIL") { exit 1 }
