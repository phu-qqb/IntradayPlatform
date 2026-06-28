param(
    [string]$ExpectedAwsCliMsiSha256 = "",
    [string]$ExpectedAwsCliExeSha256 = "",
    [string]$ExpectedAwsCliSha256 = "",
    [string]$AwsCliPath = "C:\Program Files\Amazon\AWSCLIV2\aws.exe",
    [switch]$Json
)

$ErrorActionPreference = "Stop"

function Normalize-Sha256OrEmpty {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    return $Value.Trim().ToUpperInvariant()
}

$expectedMsiSha = Normalize-Sha256OrEmpty $ExpectedAwsCliMsiSha256
$expectedExeSha = Normalize-Sha256OrEmpty $ExpectedAwsCliExeSha256
$legacyExpectedSha = Normalize-Sha256OrEmpty $ExpectedAwsCliSha256
$legacyIgnored = $false
$warnings = @()

if (-not [string]::IsNullOrWhiteSpace($legacyExpectedSha)) {
    $legacyIgnored = $true
    if ([string]::IsNullOrWhiteSpace($expectedMsiSha)) { $expectedMsiSha = $legacyExpectedSha }
    $warnings += "legacy_expected_aws_cli_sha256_treated_as_msi_sha256_not_exe_sha256"
}

$result = [ordered]@{
    status = "PASS"
    aws_cli_path = $AwsCliPath
    aws_cli_present = $false
    aws_cli_version = $null
    aws_cli_sha256 = $null
    aws_cli_exe_sha256_observed = $null
    aws_cli_exe_sha256_expected = if ([string]::IsNullOrWhiteSpace($expectedExeSha)) { $null } else { $expectedExeSha }
    aws_cli_msi_sha256_expected = if ([string]::IsNullOrWhiteSpace($expectedMsiSha)) { $null } else { $expectedMsiSha }
    expected_aws_cli_sha256 = if ([string]::IsNullOrWhiteSpace($legacyExpectedSha)) { $null } else { $legacyExpectedSha }
    legacy_expected_aws_cli_sha256_ignored = [bool]$legacyIgnored
    warnings = @($warnings)
    issues = @()
}

if (-not (Test-Path -LiteralPath $AwsCliPath)) {
    $cmd = Get-Command aws -ErrorAction SilentlyContinue
    if ($null -ne $cmd) { $AwsCliPath = $cmd.Source; $result.aws_cli_path = $AwsCliPath }
}

if (-not (Test-Path -LiteralPath $AwsCliPath)) {
    $result.status = "FAIL"
    $result.issues += "aws_cli_missing"
}
else {
    $result.aws_cli_present = $true
    $observedExeSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $AwsCliPath).Hash.ToUpperInvariant()
    $result.aws_cli_sha256 = $observedExeSha
    $result.aws_cli_exe_sha256_observed = $observedExeSha
    if (-not [string]::IsNullOrWhiteSpace($expectedExeSha) -and $observedExeSha -ne $expectedExeSha) {
        $result.status = "FAIL"
        $result.issues += "aws_cli_exe_sha256_mismatch"
    }
    $version = & $AwsCliPath --version 2>&1
    if ($LASTEXITCODE -ne 0) {
        $result.status = "FAIL"
        $result.issues += "aws_cli_version_failed"
    }
    else {
        $result.aws_cli_version = ($version -join "`n").Trim()
    }
}

if ($Json) { $result | ConvertTo-Json -Depth 5 }
else { if ($result.status -ne "PASS") { throw ($result | ConvertTo-Json -Depth 5) }; $result | ConvertTo-Json -Depth 5 }