param(
    [string]$ArtifactsRoot = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "EXEC-SIM-R053 gate failed: $Message"
}

function Read-Artifact([string]$Name) {
    $path = Join-Path $ArtifactsRoot $Name
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "missing artifact $Name"
    }
    if ($Name.EndsWith(".json")) {
        return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    }
    return Get-Content -LiteralPath $path -Raw
}

$required = @(
    "phase-exec-sim-r053-summary.md",
    "phase-exec-sim-r053-r051-plan-reference.json",
    "phase-exec-sim-r053-r052-preflight-reference.json",
    "phase-exec-sim-r053-file-intake-contract.json",
    "phase-exec-sim-r053-expected-file-entry-list.json",
    "phase-exec-sim-r053-accepted-file-entries.json",
    "phase-exec-sim-r053-missing-file-diagnostics.json",
    "phase-exec-sim-r053-manifest-validation-contract.json",
    "phase-exec-sim-r053-manifest-validation-results.json",
    "phase-exec-sim-r053-file-level-validation-results.json",
    "phase-exec-sim-r053-accepted-manifest-validation-outputs.json",
    "phase-exec-sim-r053-quarantined-manifest-validation-outputs.json",
    "phase-exec-sim-r053-row-level-validation-contract.json",
    "phase-exec-sim-r053-row-level-validation-results.json",
    "phase-exec-sim-r053-row-count-comparison.json",
    "phase-exec-sim-r053-rejected-row-summary.json",
    "phase-exec-sim-r053-duplicate-out-of-order-handling.json",
    "phase-exec-sim-r053-quote-window-readiness-results.json",
    "phase-exec-sim-r053-close-benchmark-readiness-results.json",
    "phase-exec-sim-r053-feed-quality-readiness-results.json",
    "phase-exec-sim-r053-sanitized-import-readiness-metadata.json",
    "phase-exec-sim-r053-backtest-authorization-contract.json",
    "phase-exec-sim-r053-backtest-authorization-result.json",
    "phase-exec-sim-r053-r054-expected-scope.json",
    "phase-exec-sim-r053-r054-expected-report-list.json",
    "phase-exec-sim-r053-canonical-session-coverage-validation.json",
    "phase-exec-sim-r053-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sim-r053-legacy-compatibility-preservation.json",
    "phase-exec-sim-r053-symbol-provider-mapping-validation.json",
    "phase-exec-sim-r053-inversion-validation.json",
    "phase-exec-sim-r053-direct-cross-exclusion-preservation.json",
    "phase-exec-sim-r053-cost-guidance-preservation.json",
    "phase-exec-sim-r053-nonmajor-calibration-preservation.json",
    "phase-exec-sim-r053-no-db-import-audit.json",
    "phase-exec-sim-r053-no-persisted-sanitized-row-audit.json",
    "phase-exec-sim-r053-no-backtest-simulation-audit.json",
    "phase-exec-sim-r053-no-tca-result-lines-audit.json",
    "phase-exec-sim-r053-no-order-fill-report-route-audit.json",
    "phase-exec-sim-r053-no-polygon-api-call-audit.json",
    "phase-exec-sim-r053-no-lmax-call-audit.json",
    "phase-exec-sim-r053-no-external-api-call-audit.json",
    "phase-exec-sim-r053-no-broker-marketdata-runtime-audit.json",
    "phase-exec-sim-r053-usdjpy-caveat-preservation.json",
    "phase-exec-sim-r053-lmax-readonly-baseline-reference.json",
    "phase-exec-sim-r053-no-external-audit.json",
    "phase-exec-sim-r053-forbidden-actions-audit.json",
    "phase-exec-sim-r053-next-phase-recommendation.json",
    "phase-exec-sim-r053-build-test-validator-evidence.json"
)

foreach ($name in $required) {
    [void](Read-Artifact $name)
}

$expectedFiles = Read-Artifact "phase-exec-sim-r053-expected-file-entry-list.json"
$acceptedFiles = Read-Artifact "phase-exec-sim-r053-accepted-file-entries.json"
$missingFiles = Read-Artifact "phase-exec-sim-r053-missing-file-diagnostics.json"
$manifest = Read-Artifact "phase-exec-sim-r053-manifest-validation-results.json"
$rows = Read-Artifact "phase-exec-sim-r053-row-level-validation-results.json"
$rowCounts = Read-Artifact "phase-exec-sim-r053-row-count-comparison.json"
$quoteWindows = Read-Artifact "phase-exec-sim-r053-quote-window-readiness-results.json"
$closeBenchmarks = Read-Artifact "phase-exec-sim-r053-close-benchmark-readiness-results.json"
$feedQuality = Read-Artifact "phase-exec-sim-r053-feed-quality-readiness-results.json"
$authorization = Read-Artifact "phase-exec-sim-r053-backtest-authorization-result.json"
$canonical = Read-Artifact "phase-exec-sim-r053-canonical-quarter-hour-policy-preservation.json"
$legacy = Read-Artifact "phase-exec-sim-r053-legacy-compatibility-preservation.json"
$directCross = Read-Artifact "phase-exec-sim-r053-direct-cross-exclusion-preservation.json"
$cost = Read-Artifact "phase-exec-sim-r053-cost-guidance-preservation.json"
$usdJpy = Read-Artifact "phase-exec-sim-r053-usdjpy-caveat-preservation.json"
$noExternal = Read-Artifact "phase-exec-sim-r053-no-external-audit.json"
$forbidden = Read-Artifact "phase-exec-sim-r053-forbidden-actions-audit.json"
$evidence = Read-Artifact "phase-exec-sim-r053-build-test-validator-evidence.json"

if ([int]$expectedFiles.ExpectedFileEntries -ne 140) { Fail "expected file entries must be 140" }
if ([int]$acceptedFiles.AcceptedFileEntries -ne 140) { Fail "accepted file entries must be 140" }
if ([int]$missingFiles.MissingFileCount -ne 0) { Fail "missing files detected" }

if ([int]$manifest.ManifestCount -ne 140) { Fail "manifest validation must represent 140 manifests" }
if ([int]$manifest.AcceptedManifestCount -ne 140) { Fail "accepted manifest count must be 140" }
if ([int]$manifest.QuarantinedManifestCount -ne 0) { Fail "quarantined manifests detected" }

if ([int]$rows.ValidatedFileCount -ne 140) { Fail "row validation must represent 140 files" }
if ([int64]$rows.DeclaredRows -ne [int64]$rows.ObservedRows) { Fail "declared/observed row count mismatch" }
if ([int64]$rows.AcceptedRows -ne [int64]$rows.ObservedRows) { Fail "accepted/observed row count mismatch" }
if ([int64]$rows.RejectedRows -ne 0) { Fail "unexpected rejected rows" }
if ($rowCounts.AllCountsMatched -ne $true) { Fail "row-count comparison did not pass" }

if ([int]$quoteWindows.QuoteWindowReadinessRecords -ne 3780) { Fail "quote-window readiness count must be 3780" }
if ([int]$quoteWindows.ReadyRecords -ne 3780) { Fail "quote-window ready count must be 3780" }
if ([int]$closeBenchmarks.CloseBenchmarkReadinessRecords -ne 3780) { Fail "close-benchmark readiness count must be 3780" }
if ([int]$closeBenchmarks.ReadyRecords -ne 3780) { Fail "close-benchmark ready count must be 3780" }
if ([int]$feedQuality.FeedQualityReadinessRecords -ne 140) { Fail "feed-quality readiness count must be 140" }
if ([int]$feedQuality.ReadyRecords -ne 140) { Fail "feed-quality ready count must be 140" }

if ($authorization.FutureBacktestAuthorized -ne $true) { Fail "future backtest authorization missing" }
if ($authorization.BacktestExecuted -ne $false) { Fail "backtest execution detected" }

if ($canonical.Legacy06UsedAsFutureCanonical -ne $false) { Fail "legacy :06 used as future canonical" }
if ($legacy.CompatibilityOnly -ne $true) { Fail "legacy compatibility-only policy weakened" }
if ($directCross.DirectCrossExecutionEnabled -ne $false) { Fail "direct-cross exclusion weakened" }
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
if ($usdJpy.CaveatPreserved -ne $true -or $usdJpy.SecurityID -ne "4004" -or $usdJpy.SecurityIDSource -ne "8") {
    Fail "USDJPY caveat weakened"
}

if ($noExternal.PolygonCalled -ne $false -or $noExternal.LmaxCalled -ne $false -or $noExternal.ExternalApiCalled -ne $false -or $noExternal.DownloadsExecuted -ne $false) {
    Fail "external action detected"
}
if ($forbidden.ForbiddenActionsOccurred -ne $false) { Fail "forbidden action audit failed" }
if ($forbidden.DbImportOccurred -ne $false -or $forbidden.BacktestOrSimulationExecuted -ne $false -or $forbidden.TcaResultLinesProduced -ne $false) {
    Fail "forbidden DB/backtest/TCA action detected"
}

if ($evidence.DotnetBuildNoRestoreSucceeded -ne $true) { Fail "dotnet build evidence missing or failed" }
if ($evidence.FocusedR053StaticChecksSucceeded -ne $true) { Fail "focused R053 checks evidence missing or failed" }
if ($evidence.UnitTestsFeasible -eq $true -and $evidence.UnitTestsSucceeded -ne $true) { Fail "unit test evidence missing or failed" }

Write-Host "EXEC-SIM-R053 validation passed"
