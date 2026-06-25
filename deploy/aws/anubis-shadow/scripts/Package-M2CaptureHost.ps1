param(
    [string]$RepoRoot = (Resolve-Path ".").Path,
    [string]$OutputRoot = "artifacts\readiness\anubis-aws1-read-only-shadow-foundation-plan-ready\package",
    [string]$ArtifactName = "anubis_aws1_read_only_shadow_foundation_plan_ready.zip",
    [string]$AppSourceCommit = "7e87f3b17c84ac8a0aeb79422e4caa97b915fbb6",
    [string]$InfrastructureCommit = "",
    [string]$PackagingCommit = "",
    [string]$SourcePackageSha256 = "1B71CB16966AF525456A270C8AD2020931EF1829FF13C699180741C62FE89B84",
    [string]$DeterministicTimestampUtc = "2026-06-25T00:00:00Z",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

function Assert-UnderRoot {
    param([string]$Root, [string]$Path)
    $rootFull = [System.IO.Path]::GetFullPath($Root)
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    if (-not $pathFull.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "path_outside_repo:$pathFull"
    }
}

function Get-FileSha256 {
    param([string]$Path)
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

function Get-GitValue {
    param([string]$Repo, [string[]]$GitArgs, [string]$Fallback)
    $output = & git -C $Repo @GitArgs 2>$null
    if ($LASTEXITCODE -ne 0) { return $Fallback }
    return (($output -join "`n").Trim())
}

function New-DeterministicZip {
    param([string]$SourceDirectory, [string]$ZipPath)
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path -LiteralPath $ZipPath) {
        Remove-Item -LiteralPath $ZipPath -Force
    }
    $fixedTime = [System.DateTimeOffset]::Parse("2026-06-25T00:00:00Z")
    $zip = [System.IO.Compression.ZipFile]::Open($ZipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $rootFull = [System.IO.Path]::GetFullPath($SourceDirectory).TrimEnd("\")
        Get-ChildItem -LiteralPath $SourceDirectory -Recurse -File |
            Sort-Object FullName |
            ForEach-Object {
                $full = [System.IO.Path]::GetFullPath($_.FullName)
                $relative = $full.Substring($rootFull.Length + 1).Replace("\", "/")
                $entry = $zip.CreateEntry($relative, [System.IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = $fixedTime
                $entryStream = $entry.Open()
                $fileStream = [System.IO.File]::OpenRead($full)
                try { $fileStream.CopyTo($entryStream) }
                finally {
                    $fileStream.Dispose()
                    $entryStream.Dispose()
                }
            }
    }
    finally { $zip.Dispose() }
}

$repo = [System.IO.Path]::GetFullPath($RepoRoot)
$output = [System.IO.Path]::GetFullPath((Join-Path $repo $OutputRoot))
Assert-UnderRoot -Root $repo -Path $output

if ([string]::IsNullOrWhiteSpace($InfrastructureCommit)) {
    $InfrastructureCommit = Get-GitValue -Repo $repo -GitArgs @("rev-parse", "HEAD") -Fallback "UNVERIFIED_DIRTY_WORKTREE"
}
if ([string]::IsNullOrWhiteSpace($PackagingCommit)) {
    $PackagingCommit = $InfrastructureCommit
}
$gitStatus = Get-GitValue -Repo $repo -GitArgs @("status", "--short") -Fallback "GIT_STATUS_UNAVAILABLE"
if ([string]::IsNullOrWhiteSpace($gitStatus)) { $gitStatus = "CLEAN" }

$stage = Join-Path $output "stage"
$zipPath = Join-Path $output $ArtifactName
$manifestPath = Join-Path $output "deployment_manifest.json"

if (Test-Path -LiteralPath $stage) {
    Assert-UnderRoot -Root $output -Path $stage
    Remove-Item -LiteralPath $stage -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $stage | Out-Null

$project = Join-Path $repo "tools\QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly\QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly.csproj"
$appOut = Join-Path $stage "app"
New-Item -ItemType Directory -Force -Path $appOut | Out-Null

if ($SkipPublish) {
    Set-Content -LiteralPath (Join-Path $appOut "PUBLISH_SKIPPED.txt") -Value "dotnet publish skipped by operator; package is dry-run only." -Encoding UTF8
}
else {
    & dotnet publish $project -c Release -r win-x64 --self-contained true -o $appOut /p:ContinuousIntegrationBuild=true /p:PublishSingleFile=false
    if ($LASTEXITCODE -ne 0) { throw "dotnet_publish_failed:$LASTEXITCODE" }
}

$appExe = Join-Path $appOut "QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly.exe"
if (-not $SkipPublish -and -not (Test-Path -LiteralPath $appExe)) {
    throw "self_contained_exe_missing:$appExe"
}

$runtimeConfig = Join-Path $appOut "QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly.runtimeconfig.json"
$runtimeConfigHasFramework = $false
if (Test-Path -LiteralPath $runtimeConfig) {
    $runtimeConfigHasFramework = (Get-Content -Raw -LiteralPath $runtimeConfig) -match '"framework"'
    if ($runtimeConfigHasFramework) { throw "framework_dependent_runtimeconfig_detected" }
}

$appManifest = [ordered]@{
    artifact_type = "aws1_self_contained_app_manifest"
    app_source_commit = $AppSourceCommit
    runtime_identifier = "win-x64"
    self_contained = -not [bool]$SkipPublish
    dotnet_runtime_included = -not [bool]$SkipPublish
    executable = "QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly.exe"
    executable_sha256 = if (Test-Path -LiteralPath $appExe) { Get-FileSha256 $appExe } else { "PUBLISH_SKIPPED" }
    runtimeconfig_has_framework = $runtimeConfigHasFramework
    published_utc = $DeterministicTimestampUtc
}
$appManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $appOut "app_manifest.json") -Encoding UTF8

$deploySource = Join-Path $repo "deploy\aws\anubis-shadow"
$deployDest = Join-Path $stage "deploy\aws\anubis-shadow"
Copy-Item -LiteralPath $deploySource -Destination $deployDest -Recurse -Force

$infraSource = Join-Path $repo "infra\aws\anubis-shadow"
$infraDest = Join-Path $stage "infra\aws\anubis-shadow"
Copy-Item -LiteralPath $infraSource -Destination $infraDest -Recurse -Force
$terraformCache = Join-Path $infraDest ".terraform"
if (Test-Path -LiteralPath $terraformCache) {
    Remove-Item -LiteralPath $terraformCache -Recurse -Force
}

$docsSource = Join-Path $repo "docs\aws"
if (Test-Path -LiteralPath $docsSource) {
    Copy-Item -LiteralPath $docsSource -Destination (Join-Path $stage "docs\aws") -Recurse -Force
}

$readinessReportSource = Join-Path $repo "artifacts\readiness\anubis-aws1-read-only-shadow-foundation-plan-ready\AWS1_TEST_REPORT.generated.json"
$readinessReportPackagePath = "artifacts/readiness/AWS1_TEST_REPORT.generated.json"
if (Test-Path -LiteralPath $readinessReportSource) {
    $readinessDest = Join-Path $stage "artifacts\readiness"
    New-Item -ItemType Directory -Force -Path $readinessDest | Out-Null
    Copy-Item -LiteralPath $readinessReportSource -Destination (Join-Path $readinessDest "AWS1_TEST_REPORT.generated.json") -Force
}

Set-Content -LiteralPath (Join-Path $stage "git_status.txt") -Value $gitStatus -Encoding UTF8

$fileHashes = Get-ChildItem -LiteralPath $stage -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
        $rel = ([System.IO.Path]::GetFullPath($_.FullName).Substring(([System.IO.Path]::GetFullPath($stage).TrimEnd("\")).Length + 1)).Replace("\", "/")
        [ordered]@{ path = $rel; sha256 = Get-FileSha256 $_.FullName; bytes = $_.Length }
    }

$manifest = [ordered]@{
    artifact_type = "anubis_aws1_read_only_shadow_foundation_plan_ready"
    created_utc = $DeterministicTimestampUtc
    app_source_commit = $AppSourceCommit
    infrastructure_commit = $InfrastructureCommit
    packaging_commit = $PackagingCommit
    source_m2_package_sha256 = $SourcePackageSha256
    host_project = "tools/QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly"
    runtime_identifier = "win-x64"
    self_contained = -not [bool]$SkipPublish
    operation_mode = "SMOKE_CAPTURE_BOUNDED"
    terraform_version_tested = "1.10.5"
    aws_provider_version_locked = "5.100.0"
    terraform_lock_file = "infra/aws/anubis-shadow/.terraform.lock.hcl"
    local_test_report = if (Test-Path -LiteralPath $readinessReportSource) { $readinessReportPackagePath } else { "NOT_PRESENT_AT_PACKAGE_TIME" }
    no_apply = $true
    no_credentials = $true
    no_order_entry = $true
    no_account_api = $true
    no_rds = $true
    files = @($fileHashes)
}

$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $stage "deployment_manifest.json") -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $stage "deployment_manifest.json") -Destination $manifestPath -Force

New-DeterministicZip -SourceDirectory $stage -ZipPath $zipPath
$zipHash = Get-FileSha256 $zipPath
Set-Content -LiteralPath "$zipPath.sha256" -Value "$zipHash  $ArtifactName" -Encoding ASCII

[ordered]@{
    status = "AWS1_PLAN_READY_PACKAGE_READY"
    artifact_zip = $zipPath
    artifact_sha256 = $zipHash
    manifest = $manifestPath
    app_executable_sha256 = $appManifest.executable_sha256
    self_contained = $manifest.self_contained
    skip_publish = [bool]$SkipPublish
} | ConvertTo-Json -Depth 5
