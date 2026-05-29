$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot 'check-lmax-r177-mdupdatetype-required-after-r176-state-propagation-activation-gate.ps1'
if (!(Test-Path $script)) {
    throw 'Missing canonical R177 validator script.'
}

& $script
