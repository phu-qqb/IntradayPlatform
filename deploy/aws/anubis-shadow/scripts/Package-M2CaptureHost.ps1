param(
    [string]$RepoRoot = (Resolve-Path ".").Path,
    [string]$OutputRoot = "artifacts\readiness\anubis-aws1-read-only-shadow-foundation-no-apply\package",
    [string]$BuildCommit = "7e87f3b17c84ac8a0aeb79422e4caa97b915fbb6",
    [string]$SourcePackageSha256 = "F1F024563F29544124049A1CF7A980A93C7ED25842F71752F9F6A04E862163C8",
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
                try {
                    $fileStream.CopyTo($entryStream)
                }
                finally {
                    $fileStream.Dispose()
                    $entryStream.Dispose()
                }
            }
    }
    finally {
        $zip.Dispose()
    }
}

$repo = [System.IO.Path]::GetFullPath($RepoRoot)
$output = [System.IO.Path]::GetFullPath((Join-Path $repo $OutputRoot))
Assert-UnderRoot -Root $repo -Path $output

$stage = Join-Path $output "stage"
$zipPath = Join-Path $output "anubis_aws1_read_only_shadow_foundation_no_apply.zip"
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
    & dotnet publish $project -c Release -o $appOut /p:ContinuousIntegrationBuild=true
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet_publish_failed:$LASTEXITCODE"
    }
}

$deploySource = Join-Path $repo "deploy\aws\anubis-shadow"
$deployDest = Join-Path $stage "deploy\aws\anubis-shadow"
Copy-Item -LiteralPath $deploySource -Destination $deployDest -Recurse -Force

$infraSource = Join-Path $repo "infra\aws\anubis-shadow"
$infraDest = Join-Path $stage "infra\aws\anubis-shadow"
Copy-Item -LiteralPath $infraSource -Destination $infraDest -Recurse -Force

$docsSource = Join-Path $repo "docs\aws"
if (Test-Path -LiteralPath $docsSource) {
    Copy-Item -LiteralPath $docsSource -Destination (Join-Path $stage "docs\aws") -Recurse -Force
}

$fileHashes = Get-ChildItem -LiteralPath $stage -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
        $rel = ([System.IO.Path]::GetFullPath($_.FullName).Substring(([System.IO.Path]::GetFullPath($stage).TrimEnd("\")).Length + 1)).Replace("\", "/")
        [ordered]@{
            path = $rel
            sha256 = Get-FileSha256 $_.FullName
            bytes = $_.Length
        }
    }

$manifest = [ordered]@{
    artifact_type = "anubis_aws1_read_only_shadow_foundation_no_apply"
    created_utc = $DeterministicTimestampUtc
    baseline_commit = "7e87f3b17c84ac8a0aeb79422e4caa97b915fbb6"
    packaging_commit = $BuildCommit
    source_m2_package_sha256 = $SourcePackageSha256
    host_project = "tools/QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly"
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
Set-Content -LiteralPath "$zipPath.sha256" -Value "$zipHash  anubis_aws1_read_only_shadow_foundation_no_apply.zip" -Encoding ASCII

[ordered]@{
    status = "AWS1_PACKAGE_READY"
    artifact_zip = $zipPath
    artifact_sha256 = $zipHash
    manifest = $manifestPath
    skip_publish = [bool]$SkipPublish
} | ConvertTo-Json -Depth 4
