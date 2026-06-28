param(
    [string]$RepoRoot = (Resolve-Path ".").Path,
    [string]$OutputPath = "artifacts\readiness\qq-fund-platform-aws3h-aws-cli-msi-vs-exe-sha-fix\AWS3H_HOST_PREREQUISITES_CONTRACT_TEST_REPORT.generated.json"
)

$ErrorActionPreference = "Stop"
$repo = [System.IO.Path]::GetFullPath($RepoRoot)
$hostPrereqScript = Join-Path $repo "deploy\aws\anubis-shadow\scripts\Test-AnubisAws1HostPrerequisites.ps1"
$installScript = Join-Path $repo "deploy\aws\anubis-shadow\scripts\Install-AnubisAws1Host.ps1"
$wrapperScript = Join-Path $repo "deploy\aws\anubis-shadow\scripts\Start-AnubisAws1Recorder.ps1"
$tmpRoot = Join-Path $repo ("artifacts\tmp\aws3h-host-prerequisites-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $tmpRoot | Out-Null

function New-Result {
    param([string]$Name, [bool]$Pass, [object]$Detail)
    [ordered]@{ name = $Name; status = $(if ($Pass) { "PASS" } else { "FAIL" }); detail = $Detail }
}

function Assert-NonEmptySha256 {
    param([string]$Value)
    return ($Value -match "^[A-F0-9]{64}$")
}

function Write-FakeAwsCli {
    param([string]$Path)
    Set-Content -LiteralPath $Path -Encoding ASCII -Value @(
        "@echo off",
        "echo aws-cli/2.0.0 Python/fixture Windows/fixture exe/fixture",
        "exit /b 0"
    )
}

function Invoke-Prereq {
    param([hashtable]$Arguments)
    $output = @(& $hostPrereqScript @Arguments -Json 2>&1)
    $exitCode = $LASTEXITCODE
    $json = ($output -join "`n") | ConvertFrom-Json
    [ordered]@{ exit_code = $exitCode; output = $output; result = $json }
}

$fakeAws = Join-Path $tmpRoot "aws.cmd"
Write-FakeAwsCli -Path $fakeAws
$fakeAwsSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $fakeAws).Hash.ToUpperInvariant()
$msiSha = "A" * 64
$wrongExeSha = "B" * 64
$legacyMsiSha = "C" * 64
$results = New-Object System.Collections.Generic.List[object]

$case1 = Invoke-Prereq -Arguments @{ AwsCliPath = $fakeAws; ExpectedAwsCliMsiSha256 = $msiSha }
$results.Add((New-Result "msi_sha_without_exe_expected_passes_and_reports_observed_exe_sha" (
    $case1.result.status -eq "PASS" -and
    $case1.result.aws_cli_msi_sha256_expected -eq $msiSha -and
    [string]::IsNullOrWhiteSpace([string]$case1.result.aws_cli_exe_sha256_expected) -and
    $case1.result.aws_cli_exe_sha256_observed -eq $fakeAwsSha -and
    (Assert-NonEmptySha256 ([string]$case1.result.aws_cli_sha256))
) $case1))

$case2 = Invoke-Prereq -Arguments @{ AwsCliPath = $fakeAws; ExpectedAwsCliExeSha256 = $fakeAwsSha }
$results.Add((New-Result "exe_expected_sha_match_passes" (
    $case2.result.status -eq "PASS" -and
    $case2.result.aws_cli_exe_sha256_expected -eq $fakeAwsSha -and
    $case2.result.aws_cli_exe_sha256_observed -eq $fakeAwsSha
) $case2))

$case3 = Invoke-Prereq -Arguments @{ AwsCliPath = $fakeAws; ExpectedAwsCliExeSha256 = $wrongExeSha }
$results.Add((New-Result "exe_expected_sha_mismatch_no_go" (
    $case3.result.status -eq "FAIL" -and
    @($case3.result.issues | Where-Object { $_ -eq "aws_cli_exe_sha256_mismatch" }).Count -eq 1
) $case3))

$case4 = Invoke-Prereq -Arguments @{ AwsCliPath = $fakeAws; ExpectedAwsCliSha256 = $legacyMsiSha }
$results.Add((New-Result "legacy_expected_sha_is_not_used_as_exe_sha" (
    $case4.result.status -eq "PASS" -and
    $case4.result.aws_cli_msi_sha256_expected -eq $legacyMsiSha -and
    [string]::IsNullOrWhiteSpace([string]$case4.result.aws_cli_exe_sha256_expected) -and
    [bool]$case4.result.legacy_expected_aws_cli_sha256_ignored -eq $true -and
    @($case4.result.issues | Where-Object { $_ -like "*sha256_mismatch*" }).Count -eq 0
) $case4))

$installText = Get-Content -Raw -LiteralPath $installScript
$wrapperText = Get-Content -Raw -LiteralPath $wrapperScript
$taskArgsLine = (($installText -split "`r?`n") | Where-Object { $_ -like '$taskArgs = *' } | Select-Object -First 1)
$results.Add((New-Result "scheduled_task_passes_msi_and_exe_sha_separately" (
    $taskArgsLine -match "ExpectedAwsCliMsiSha256" -and
    $taskArgsLine -match "ExpectedAwsCliExeSha256" -and
    $taskArgsLine -notmatch "ExpectedAwsCliSha256"
) ([ordered]@{ task_args_line = $taskArgsLine })))

$results.Add((New-Result "install_manifest_distinguishes_msi_and_observed_exe_sha" (
    $installText -match "aws_cli_msi_sha256" -and
    $installText -match "aws_cli_exe_sha256_observed" -and
    $installText -match "aws_cli_exe_sha256_expected"
) ([ordered]@{ install_script = $installScript })))

$results.Add((New-Result "wrapper_last_run_distinguishes_msi_and_observed_exe_sha" (
    $wrapperText -match "aws_cli_msi_sha256" -and
    $wrapperText -match "aws_cli_exe_sha256_observed" -and
    $wrapperText -match "aws_cli_exe_sha256_expected"
) ([ordered]@{ wrapper_script = $wrapperScript })))

$failed = @($results.ToArray() | Where-Object { $_.status -ne "PASS" })
$report = [ordered]@{
    gate = if ($failed.Count -eq 0) { "GO_AWS3H_HOST_PREREQUISITES_CONTRACT_TESTS" } else { "NO_GO_AWS3H_HOST_PREREQUISITES_CONTRACT_TESTS" }
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    no_package_install = $true
    no_ssm_command = $true
    no_capture = $true
    no_archive_upload = $true
    fake_aws_cli = $true
    fixture_root = $tmpRoot
    results = @($results.ToArray())
    failure_count = $failed.Count
}

$fullOutput = [System.IO.Path]::GetFullPath((Join-Path $repo $OutputPath))
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $fullOutput) | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $fullOutput -Encoding UTF8
$report | ConvertTo-Json -Depth 12
if ($failed.Count -eq 0) { exit 0 }
exit 1