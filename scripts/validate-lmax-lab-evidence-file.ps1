param(
    [Parameter(Mandatory = $true)]
    [string]$EvidenceFile,
    [switch]$WriteNormalizedCopy
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "tools\QQ.Production.Intraday.Lmax.ConnectivityLab\QQ.Production.Intraday.Lmax.ConnectivityLab.csproj"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_home"

$resolved = Resolve-Path -LiteralPath $EvidenceFile
$args = @(
    "validate-evidence-file",
    "--evidence-file=$resolved"
)

if ($WriteNormalizedCopy.IsPresent) {
    $args += "--write-normalized-copy=true"
}

dotnet run --project $project --no-build --no-restore -- @args
