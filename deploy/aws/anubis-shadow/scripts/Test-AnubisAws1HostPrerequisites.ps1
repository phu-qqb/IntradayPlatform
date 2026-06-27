param(
    [string]$ExpectedAwsCliSha256 = "",
    [string]$AwsCliPath = "C:\Program Files\Amazon\AWSCLIV2\aws.exe",
    [switch]$Json
)

$ErrorActionPreference = "Stop"

$result = [ordered]@{
    status = "PASS"
    aws_cli_path = $AwsCliPath
    aws_cli_present = $false
    aws_cli_version = $null
    aws_cli_sha256 = $null
    expected_aws_cli_sha256 = $ExpectedAwsCliSha256
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
    $result.aws_cli_sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $AwsCliPath).Hash.ToUpperInvariant()
    if (-not [string]::IsNullOrWhiteSpace($ExpectedAwsCliSha256) -and $result.aws_cli_sha256 -ne $ExpectedAwsCliSha256.ToUpperInvariant()) {
        $result.status = "FAIL"
        $result.issues += "aws_cli_sha256_mismatch"
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
