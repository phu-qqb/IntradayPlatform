param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim",
    [string]$FixtureDirectory = "data/qubes-fixtures/broader-paper-eval"
)

$ErrorActionPreference = "Stop"

function Fail([string]$classification, [string]$message) {
    Write-Error "$classification $message"
    exit 1
}

function Read-Json([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "EXEC_PAPER_R010_FAIL_BUILD_OR_TESTS" "Missing required artifact: $path"
    }

    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function As-Array($value) {
    if ($null -eq $value) {
        return @()
    }

    if ($value -is [System.Array]) {
        return $value
    }

    return @($value)
}

function Assert-NoForbiddenAudit([object]$audit) {
    if ($audit.ForbiddenActionsDetected -or
        $audit.BrokerActivation -or
        $audit.LiveMarketData -or
        $audit.SchedulerServicePolling -or
        $audit.PMSCycleRun -or
        $audit.BacktestRun -or
        $audit.SimulationRun -or
        $audit.TcaResultLinesCreated -or
        $audit.ExecutableSchedulesCreated -or
        $audit.ChildSlicesCreated -or
        $audit.ChildOrdersCreated -or
        $audit.OrdersCreated -or
        $audit.FillsCreated -or
        $audit.ExecutionReportsCreated -or
        $audit.RoutesCreated -or
        $audit.SubmissionsCreated -or
        $audit.PaperLedgerCommit -or
        $audit.StateMutation -or
        $audit.R009PromotedToExecutable -or
        $audit.CommandsExecuted) {
        Fail "EXEC_PAPER_R010_FAIL_FORBIDDEN_ACTION_DETECTED" "Forbidden action audit reports a blocked action."
    }
}

function Assert-FixtureRows([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "EXEC_PAPER_R010_FAIL_FIXTURE_MISSING" "Generated fixture is missing: $path"
    }

    $rows = Get-Content -LiteralPath $path
    if ((As-Array $rows).Count -lt 1) {
        Fail "EXEC_PAPER_R010_FAIL_FIXTURE_INVALID" "Generated fixture is empty: $path"
    }

    foreach ($row in $rows) {
        if ([string]::IsNullOrWhiteSpace($row)) {
            Fail "EXEC_PAPER_R010_FAIL_FIXTURE_INVALID" "Generated fixture contains a blank row: $path"
        }

        $parts = $row.Split(";")
        if ($parts.Count -ne 2) {
            Fail "EXEC_PAPER_R010_FAIL_FIXTURE_INVALID" "Generated fixture row is not <BloombergTicker>;<weight>: $row"
        }

        if ($parts[0] -match "^\d{12}$" -or $parts[0] -match "^\d{8,14}") {
            Fail "EXEC_PAPER_R010_FAIL_FIXTURE_CONTAINS_TIMESTAMP_ROWS" "Generated fixture row contains a timestamp in the ticker column: $row"
        }

        $parsed = 0.0
        if (-not [double]::TryParse($parts[1], [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$parsed)) {
            Fail "EXEC_PAPER_R010_FAIL_FIXTURE_INVALID" "Generated fixture row has an unparsable weight: $row"
        }

        if ([double]::IsNaN($parsed) -or [double]::IsInfinity($parsed)) {
            Fail "EXEC_PAPER_R010_FAIL_FIXTURE_INVALID" "Generated fixture row has a non-finite weight: $row"
        }
    }
}

function Assert-CanonicalTargetClose([object]$entry) {
    if ([string]::IsNullOrWhiteSpace($entry.CanonicalTargetCloseLocal) -or [string]::IsNullOrWhiteSpace($entry.CanonicalTargetCloseUtc)) {
        Fail "EXEC_PAPER_R010_FAIL_BATCH_MANIFEST_TARGET_CLOSE_MISSING" "Batch manifest entry is missing target close metadata: $($entry.BatchEntryId)"
    }

    $localText = [string]$entry.CanonicalTargetCloseLocal
    if ($localText -match "T\d{2}:(06|21|36|51):00") {
        Fail "EXEC_PAPER_R010_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Batch manifest uses a legacy minute as future canonical: $localText"
    }

    $localIso = $localText.Substring(0, 19)
    $localClose = [datetime]::ParseExact($localIso, "yyyy-MM-ddTHH:mm:ss", [System.Globalization.CultureInfo]::InvariantCulture)
    if ($localClose.Second -ne 0 -or @(0, 15, 30, 45) -notcontains $localClose.Minute) {
        Fail "EXEC_PAPER_R010_FAIL_BATCH_MANIFEST_TARGET_CLOSE_NOT_CANONICAL" "Batch manifest target close is not a canonical quarter-hour: $localText"
    }

    if ($entry.LegacyCompatibilityMapping.LegacyNextBarExecutionCloseCanonical -ne $entry.CanonicalTargetCloseLocal) {
        Fail "EXEC_PAPER_R010_FAIL_LEGACY_COMPATIBILITY_MAPPING_INVALID" "Legacy next-bar execution close does not match target close for $($entry.BatchEntryId)."
    }

    if ($entry.LegacyCompatibilityMapping.Rule -notmatch "LegacyNextBarExecutionCloseCanonical = LegacyOutputTimestamp \+ 9 minutes") {
        Fail "EXEC_PAPER_R010_FAIL_LEGACY_COMPATIBILITY_MAPPING_INVALID" "Legacy compatibility mapping rule is missing or changed."
    }
}

$requiredArtifacts = @(
    "phase-exec-paper-r010-summary.md",
    "phase-exec-paper-r010-r009-template-reference.json",
    "phase-exec-paper-r010-r057-plan-reference.json",
    "phase-exec-paper-r010-aggregatedweights-source-analysis.json",
    "phase-exec-paper-r010-fixture-extraction-contract.json",
    "phase-exec-paper-r010-selected-legacy-groups.json",
    "phase-exec-paper-r010-generated-fixture-inventory.json",
    "phase-exec-paper-r010-generated-fixture-validation.json",
    "phase-exec-paper-r010-batch-manifest.json",
    "phase-exec-paper-r010-batch-manifest-validation.json",
    "phase-exec-paper-r010-manual-noexternal-command-plan.md",
    "phase-exec-paper-r010-manual-noexternal-command-plan.json",
    "phase-exec-paper-r010-next-operator-action-package.json",
    "phase-exec-paper-r010-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r010-legacy-compatibility-preservation.json",
    "phase-exec-paper-r010-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r010-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r010-cost-guidance-preservation.json",
    "phase-exec-paper-r010-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r010-no-broker-activation-audit.json",
    "phase-exec-paper-r010-no-live-marketdata-audit.json",
    "phase-exec-paper-r010-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r010-no-executable-schedule-audit.json",
    "phase-exec-paper-r010-no-child-slices-audit.json",
    "phase-exec-paper-r010-no-child-orders-audit.json",
    "phase-exec-paper-r010-no-order-created-audit.json",
    "phase-exec-paper-r010-no-real-fill-audit.json",
    "phase-exec-paper-r010-no-execution-report-audit.json",
    "phase-exec-paper-r010-no-route-no-submission-audit.json",
    "phase-exec-paper-r010-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r010-no-polygon-api-call-audit.json",
    "phase-exec-paper-r010-no-lmax-call-audit.json",
    "phase-exec-paper-r010-no-external-api-call-audit.json",
    "phase-exec-paper-r010-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r010-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r010-no-external-audit.json",
    "phase-exec-paper-r010-forbidden-actions-audit.json",
    "phase-exec-paper-r010-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    $path = Join-Path $ArtifactsRoot $artifact
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "EXEC_PAPER_R010_FAIL_BUILD_OR_TESTS" "Missing required artifact: $artifact"
    }
}

$sourceAnalysis = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-aggregatedweights-source-analysis.json")
$contract = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-fixture-extraction-contract.json")
$selectedGroups = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-selected-legacy-groups.json")
$inventory = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-generated-fixture-inventory.json")
$fixtureValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-generated-fixture-validation.json")
$manifest = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-batch-manifest.json")
$manifestValidation = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-batch-manifest-validation.json")
$commands = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-manual-noexternal-command-plan.json")
$canonical = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-canonical-quarter-hour-policy-preservation.json")
$legacy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-legacy-compatibility-preservation.json")
$usdPair = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-usd-pair-normalization-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-cost-guidance-preservation.json")
$nonmajor = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-nonmajor-calibration-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-usdjpy-caveat-preservation.json")
$noExternal = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-no-external-audit.json")
$forbidden = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsRoot "phase-exec-paper-r010-build-test-validator-evidence.json")

if ($noExternal.PolygonCalled -or $noExternal.LmaxCalled -or $noExternal.ExternalApiCalled -or $noExternal.FilesDownloaded -or -not $noExternal.NoExternal) {
    Fail "EXEC_PAPER_R010_FAIL_API_CALL_DETECTED" "No-external audit reports external activity."
}

Assert-NoForbiddenAudit $forbidden

if (-not $sourceAnalysis.Exists -or
    $sourceAnalysis.Delimiter -ne ";" -or
    $sourceAnalysis.TimestampFormat -ne "yyyyMMddHHmm" -or
    $sourceAnalysis.HeaderColumnCount -lt 1 -or
    $sourceAnalysis.DataRowCount -lt 1 -or
    $sourceAnalysis.CommandsExecuted) {
    Fail "EXEC_PAPER_R010_FAIL_SOURCE_ANALYSIS_INVALID" "AggregatedWeights source analysis is missing or not text-only matrix parsing."
}

if ($contract.FixtureFormat -notmatch "<BloombergTicker>;<weight>" -or
    $contract.TimestampRowsInFixtureAllowed -or
    $contract.ManualNoExternalCommandsExecuted -or
    -not $contract.NonExecutable -or
    -not $contract.NoPaperLedgerCommit) {
    Fail "EXEC_PAPER_R010_FAIL_FIXTURE_EXTRACTION_CONTRACT_INVALID" "Fixture extraction contract does not preserve the no-timestamp fixture format."
}

$selected = As-Array $selectedGroups.Groups
if ($selected.Count -lt 1 -or $selected.Count -gt 20) {
    Fail "EXEC_PAPER_R010_FAIL_SELECTED_GROUPS_INVALID" "Selected legacy group count is invalid: $($selected.Count)"
}

if ($inventory.FixtureCount -ne $selected.Count -or $fixtureValidation.FixtureCount -ne $selected.Count) {
    Fail "EXEC_PAPER_R010_FAIL_FIXTURE_COUNT_MISMATCH" "Generated fixture counts do not match selected legacy groups."
}

if ($fixtureValidation.InvalidFixtureCount -ne 0 -or $fixtureValidation.ValidFixtureCount -ne $fixtureValidation.FixtureCount -or $fixtureValidation.TimestampRowsInFixtures) {
    Fail "EXEC_PAPER_R010_FAIL_FIXTURE_VALIDATION_INVALID" "Fixture validation reports invalid fixtures or timestamp rows."
}

foreach ($fixture in (As-Array $inventory.Fixtures)) {
    Assert-FixtureRows ([string]$fixture.QubesFixturePath)
}

if ($manifest.ManifestStatus -notin @("FullBatchReady", "PartialBatchReady") -or $manifest.BatchEntryCount -ne $selected.Count) {
    Fail "EXEC_PAPER_R010_FAIL_BATCH_MANIFEST_INVALID" "Batch manifest status or count is invalid."
}

if ($manifest.Legacy06UsedAsFutureCanonical -or -not $manifest.LegacyTimestampsCompatibilityOnly) {
    Fail "EXEC_PAPER_R010_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Batch manifest weakens legacy compatibility policy."
}

foreach ($entry in (As-Array $manifest.Entries)) {
    Assert-CanonicalTargetClose $entry

    if (-not $entry.NoPaperLedgerCommit -or $entry.OvernightAllowed) {
        Fail "EXEC_PAPER_R010_FAIL_BATCH_MANIFEST_INVALID" "Batch manifest weakens no-ledger/no-overnight policy for $($entry.BatchEntryId)."
    }

    if ([string]::IsNullOrWhiteSpace($entry.QubesRunId) -or [string]::IsNullOrWhiteSpace($entry.RequestedCycleRunId)) {
        Fail "EXEC_PAPER_R010_FAIL_BATCH_MANIFEST_INVALID" "Batch manifest entry is missing run IDs: $($entry.BatchEntryId)"
    }
}

if (-not $manifestValidation.Valid -or $manifestValidation.EntryCount -ne $manifest.BatchEntryCount -or $manifestValidation.EntriesWithTargetClose -ne $manifest.BatchEntryCount -or $manifestValidation.EntriesCanonicalQuarterHour -ne $manifest.BatchEntryCount) {
    Fail "EXEC_PAPER_R010_FAIL_BATCH_MANIFEST_VALIDATION_INVALID" "Batch manifest validation is missing or failed."
}

if ($manifestValidation.Legacy06UsedAsFutureCanonical) {
    Fail "EXEC_PAPER_R010_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Batch manifest validation reports legacy future canonical timestamps."
}

if ($commands.CommandsExecutedByR010 -or $commands.CommandCount -ne $manifest.BatchEntryCount) {
    Fail "EXEC_PAPER_R010_FAIL_COMMAND_PLAN_INVALID" "Command plan count is invalid or reports command execution."
}

foreach ($command in (As-Array $commands.Commands)) {
    if (-not $command.OperatorRunOnly -or $command.CommandExecuted) {
        Fail "EXEC_PAPER_R010_FAIL_COMMAND_PLAN_EXECUTED" "Command plan is not operator-run-only or reports command execution."
    }

    if ($command.Mode -ne "ManualNoExternal" -or $command.CommandLine -notmatch "--mode ManualNoExternal") {
        Fail "EXEC_PAPER_R010_FAIL_COMMAND_PLAN_INVALID" "Command plan omits ManualNoExternal mode."
    }

    if (-not $command.NoPaperLedgerCommit -or $command.CommandLine -notmatch "--no-paper-ledger-commit true") {
        Fail "EXEC_PAPER_R010_FAIL_COMMAND_PLAN_OMITS_NO_LEDGER_COMMIT" "Command plan omits --no-paper-ledger-commit true."
    }

    if ($command.CommandLine -match "--mode no-external-paper-cycle" -or $command.CommandLine -match "\s--output\s") {
        Fail "EXEC_PAPER_R010_FAIL_COMMAND_PLAN_DEPRECATED_ARGS" "Command plan uses deprecated ManualNoExternal arguments."
    }
}

if ($canonical.Legacy06UsedAsFutureCanonical -or -not $canonical.FutureTimestampsUseCanonicalQuarterHour -or $legacy.Legacy06UsedAsFutureCanonical -or -not $legacy.LegacyTimestampsCompatibilityOnly) {
    Fail "EXEC_PAPER_R010_FAIL_LEGACY_06_USED_AS_FUTURE_CANONICAL" "Canonical or legacy preservation artifact is weakened."
}

if ($directCross.DirectCrossExecutionEnabled -or -not $directCross.DirectCrossesAllowedAsSignals -or -not $directCross.NettingFirst) {
    Fail "EXEC_PAPER_R010_FAIL_DIRECT_CROSS_EXCLUSION_WEAKENED" "Direct-cross exclusion is weakened."
}

if ($cost.FiveUsdPerMillionUniversalized -or -not $cost.FiveUsdPerMillionBestCaseMajorOnly) {
    Fail "EXEC_PAPER_R010_FAIL_5USD_PER_MILLION_UNIVERSALIZED" "5 USD/million guidance is universalized."
}

if (-not $nonmajor.NonmajorEMScandiCNHDeferred -or -not $nonmajor.RequiresLiquidityCalibration) {
    Fail "EXEC_PAPER_R010_FAIL_NONMAJOR_CALIBRATION_WEAKENED" "Nonmajor calibration guard is weakened."
}

if (-not $usdjpy.RequiresInversion -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8" -or $usdjpy.USDJPYCaveatWeakened) {
    Fail "EXEC_PAPER_R010_FAIL_USDJPY_CAVEAT_WEAKENED" "USDJPY caveat is weakened."
}

if (-not $usdPair.AUDUSDNotFailed) {
    Fail "EXEC_PAPER_R010_FAIL_AUDUSD_MISCLASSIFIED" "AUDUSD is misclassified."
}

if ($evidence.DotnetBuild -ne "Passed" -or
    $evidence.FocusedR010Tests -ne "Passed" -or
    $evidence.UnitTests -ne "Passed" -or
    $evidence.R010Validator -ne "Passed" -or
    -not $evidence.EvidenceComplete) {
    Fail "EXEC_PAPER_R010_FAIL_BUILD_OR_TESTS" "Build/tests/validator evidence is missing or not passed."
}

if ($manifest.BatchEntryCount -ge 20) {
    Write-Output "EXEC_PAPER_R010_PASS_LEGACY_AGGREGATED_WEIGHTS_FIXTURES_EXTRACTED_NO_EXTERNAL"
    Write-Output "EXEC_PAPER_R010_PASS_BROADER_BATCH_MANIFEST_READY_NO_EXTERNAL"
    Write-Output "EXEC_PAPER_R010_PASS_MANUAL_NOEXTERNAL_COMMAND_PLAN_READY_NO_EXTERNAL"
    Write-Output "EXEC_PAPER_R010_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
}
else {
    Write-Output "EXEC_PAPER_R010_PARTIAL_FIXTURE_EXTRACTION_NEEDS_OPERATOR_ADDITIONAL_FIXTURES_NO_EXTERNAL"
    Write-Output "EXEC_PAPER_R010_PASS_BROADER_BATCH_MANIFEST_READY_NO_EXTERNAL"
    Write-Output "EXEC_PAPER_R010_PASS_NO_EXECUTABLE_SCHEDULE_NO_ORDER_GATE_READY_NO_EXTERNAL"
}
