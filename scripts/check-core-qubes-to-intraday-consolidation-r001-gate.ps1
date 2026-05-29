param()

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root "artifacts\readiness\core-qubes-to-intraday-consolidation-r001"

$required = @(
  "core-chain-acceptance-inventory.json",
  "runkey-package-contract.json",
  "core-to-intraday-handoff-design.json",
  "sandbox-prototype-disposition.json",
  "intraday-integration-requirements.json",
  "next-core-increment-decision.json",
  "readiness-reconciliation.json",
  "boundary-safety-evidence.json",
  "summary.md"
)

foreach ($file in $required) {
  $path = Join-Path $artifactDir $file
  if (-not (Test-Path -LiteralPath $path)) {
    throw "Missing required artifact: $file"
  }
}

function Read-Json($name) {
  Get-Content -Raw -LiteralPath (Join-Path $artifactDir $name) | ConvertFrom-Json
}

$inventory = Read-Json "core-chain-acceptance-inventory.json"
$runkey = Read-Json "runkey-package-contract.json"
$handoff = Read-Json "core-to-intraday-handoff-design.json"
$prototype = Read-Json "sandbox-prototype-disposition.json"
$requirements = Read-Json "intraday-integration-requirements.json"
$next = Read-Json "next-core-increment-decision.json"
$reconcile = Read-Json "readiness-reconciliation.json"
$safety = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $artifactDir "summary.md")

if (-not $inventory.Classification) { throw "Core chain inventory classification missing." }
if (-not $runkey.Classification) { throw "RunKey contract classification missing." }
if ($handoff.Classification -ne "HANDOFF_DESIGN_READY_MANIFEST_BASED") { throw "Preferred handoff design is not manifest-based." }
if ($prototype.Classification -ne "SANDBOX_PROTOTYPE_RETAIN_AS_TEST_FALLBACK") { throw "Prototype disposition is not explicit." }
if ($requirements.Classification -ne "INTRADAY_INTEGRATION_REQUIREMENTS_READY") { throw "Intraday integration requirements are not ready." }
if ($next.Decision -ne "NEXT_CORE_INCREMENT_4E_STRATTAKEN_COMPATIBILITY_REVIEW") { throw "Next Core increment decision is not 4E StratTaken compatibility review." }
if ($reconcile.Classification -ne "READINESS_RECONCILED_CORE_UPSTREAM_DESIGNATED") { throw "Readiness reconciliation is not explicit." }
if ($prototype.R010Transferability.TransferableToCoreAnubisOutput -ne $false) { throw "R010 transferability must be false." }

$actions = $safety.ThisPackageActions
$mustBeTrue = @(
  "NoProdExecutableRun",
  "NoManagerRun",
  "NoAnubisRun",
  "NoCuda",
  "NoR009Submission",
  "NoLmaxCall",
  "NoOrderFillReport",
  "NoDbMutation",
  "NoMigration",
  "NoSeed",
  "NoLedgerCommit",
  "NoTradingStateMutation",
  "NoProductionStateMutation",
  "NoProductionLiveReadinessClaim",
  "NoAccountingNetPnlReadinessClaim",
  "NoCredentialsPrinted"
)

foreach ($name in $mustBeTrue) {
  if ($actions.$name -ne $true) {
    throw "Boundary safety flag is not true: $name"
  }
}

if ($summary -notmatch "CORE_QUBES_TO_INTRADAY_CONSOLIDATION_R001_PASS_HANDOFF_DESIGN_READY") {
  throw "Summary missing final classification."
}
if ($summary -notmatch "R010 approval applies only to the exact prototype candidate and is not transferable") {
  throw "Summary missing R010 transferability statement."
}
if (($summary -notmatch "CROSS-RAIL-R014") -or ($summary -notmatch "PMS-intent-driven")) {
  throw "Summary missing CROSS-RAIL-R014 preservation statement."
}

Write-Host "CORE-QUBES-TO-INTRADAY-CONSOLIDATION-R001 gate passed."
