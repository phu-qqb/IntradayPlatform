Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$gate = Join-Path $repoRoot 'artifacts/readiness/anubis-qubes-integration-audit-r002/scripts/check-anubis-qubes-integration-audit-r002-gate.ps1'
& $gate
