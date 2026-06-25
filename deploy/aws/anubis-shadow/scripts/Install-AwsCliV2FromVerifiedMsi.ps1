param(
    [Parameter(Mandatory = $true)] [string]$MsiPath,
    [Parameter(Mandatory = $true)] [string]$ExpectedSha256,
    [string]$InstallLog = "C:\Anubis\Logs\aws-cli-install.log"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $MsiPath)) { throw "aws_cli_msi_missing:$MsiPath" }
$actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $MsiPath).Hash.ToUpperInvariant()
if ($actual -ne $ExpectedSha256.ToUpperInvariant()) { throw "aws_cli_msi_sha256_mismatch:$actual" }

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $InstallLog) | Out-Null
$process = Start-Process -FilePath msiexec.exe -ArgumentList @('/i', $MsiPath, '/qn', '/norestart', "/log", $InstallLog) -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw "aws_cli_msi_install_failed:$($process.ExitCode)" }

& (Join-Path $PSScriptRoot 'Test-AnubisAws1HostPrerequisites.ps1') -Json
