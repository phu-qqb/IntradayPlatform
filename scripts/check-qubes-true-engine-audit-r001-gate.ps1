Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$gate = Join-Path $repoRoot 'artifacts/readiness/qubes-true-engine-audit-r001/scripts/check-qubes-true-engine-audit-r001-gate.ps1'
& $gate
