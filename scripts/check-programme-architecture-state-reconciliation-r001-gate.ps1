param()

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root "artifacts\readiness\programme-architecture-state-reconciliation-r001"

$required = @(
  "canonical-glossary.json",
  "canonical-rail-inventory.json",
  "approval-transferability-matrix.json",
  "status-correction.json",
  "blocker-map.json",
  "next-package-decision-tree.json",
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

$glossary = Read-Json "canonical-glossary.json"
$rails = Read-Json "canonical-rail-inventory.json"
$matrix = Read-Json "approval-transferability-matrix.json"
$correction = Read-Json "status-correction.json"
$blockers = Read-Json "blocker-map.json"
$tree = Read-Json "next-package-decision-tree.json"
$safety = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $artifactDir "summary.md")

if (-not $glossary.Entries -or $glossary.Entries.Count -lt 20) { throw "Glossary is missing or too small." }
if (-not ($rails.Rails | Where-Object { $_.Name -eq "CROSS-RAIL-R014" -and $_.Driver -eq "PMS intent" -and $_.NotRetroactivelyQubesDriven -eq $true })) {
  throw "CROSS-RAIL-R014 PMS-intent classification missing."
}
if ($matrix.GlobalRule -notmatch "not transferable") { throw "R010 non-transferability missing from matrix." }
if (-not ($matrix.GlobalRule -match "output id" -and $matrix.GlobalRule -match "risk review hash")) { throw "R010 exact-match requirements incomplete." }
if (-not ($correction.Corrections | Where-Object { $_.CorrectedStatus -match "real Core/Anubis chain found" })) { throw "Anubis status correction missing." }
if (-not ($correction.Corrections | Where-Object { $_.CorrectedStatus -match "MarketData blocks sizing/PnL marks; Core/Anubis weights calculation is separate" })) {
  throw "MarketData/weights separation correction missing."
}
if (-not ($blockers.CoreBlockers -contains "4E StratTaken compatibility")) { throw "Core 4E blocker missing." }
if ($tree.IfWorkingInCore.Next -ne "Increment 4E StratTaken compatibility review") { throw "Next Core package decision incorrect." }
if ($tree.IfCoreProducesAcceptedWeightsArtifact.Next -ne "CORE-ANUBIS-WEIGHTS-INTRADAY-HANDOFF-CONSUMER-R002") { throw "Next Intraday handoff package decision incorrect." }

$safetyFlags = @(
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
  "NoAccountingNetPnlReadinessClaim"
)
foreach ($flag in $safetyFlags) {
  if ($safety.$flag -ne $true) { throw "Boundary flag is not true: $flag" }
}

if ($summary -notmatch "PROGRAMME_ARCHITECTURE_STATE_RECONCILIATION_R001_PASS_CONFUSIONS_ELIMINATED") {
  throw "Summary missing final classification."
}
if ($summary -notmatch "Fallback/test/prototype only") { throw "Prototype disposition missing from summary." }
if ($summary -notmatch "R010 is not transferable") { throw "R010 non-transferability missing from summary." }
if ($summary -notmatch "Core/Anubis-driven R009") { throw "R009 launch boundary missing from summary." }

Write-Host "PROGRAMME-ARCHITECTURE-STATE-RECONCILIATION-R001 gate passed."
