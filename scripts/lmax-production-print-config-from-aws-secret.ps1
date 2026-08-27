[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$SecretId,

    [string]$AwsCliPath = "aws",

    [string]$AwsProfile,

    [string]$AwsRegion
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-SecretField {
    param(
        [Parameter(Mandatory)]
        [psobject]$Secret,

        [Parameter(Mandatory)]
        [string]$EnvironmentName,

        [Parameter(Mandatory)]
        [string[]]$Names,

        [bool]$Required = $true
    )

    $values = [System.Collections.Generic.List[string]]::new()
    foreach ($name in $Names) {
        $property = $Secret.PSObject.Properties[$name]
        if ($null -eq $property -or $null -eq $property.Value) {
            continue
        }

        $value = [string]$property.Value
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $values.Add($value)
        }
    }

    if ($values.Count -eq 0) {
        if ($Required) {
            throw "required_secret_field_missing:$EnvironmentName"
        }

        return $null
    }

    if (($values | Select-Object -Unique).Count -ne 1) {
        throw "conflicting_secret_field_values:$EnvironmentName"
    }

    return $values[0]
}

function Get-SecretFieldNames {
    param([Parameter(Mandatory)][string]$Name)

    $logicalName = switch ($Name) {
        "QQ_LMAX_ENVIRONMENT" { "EnvironmentName" }
        "QQ_LMAX_ACCOUNT_CODE" { "AccountCode" }
        "QQ_LMAX_FIX_ORDER_HOST" { "FixOrderHost" }
        "QQ_LMAX_FIX_ORDER_PORT" { "FixOrderPort" }
        "QQ_LMAX_FIX_MARKET_DATA_HOST" { "FixMarketDataHost" }
        "QQ_LMAX_FIX_MARKET_DATA_PORT" { "FixMarketDataPort" }
        "QQ_LMAX_FIX_SENDER_COMP_ID" { "FixSenderCompId" }
        "QQ_LMAX_FIX_USERNAME" { "FixUsername" }
        "QQ_LMAX_FIX_PASSWORD" { "FixPassword" }
        "QQ_LMAX_USE_TLS" { "UseTls" }
        "QQ_LMAX_INSTRUMENT_SYMBOL" { "InstrumentSymbol" }
        "QQ_LMAX_INSTRUMENT_ID" { "LmaxInstrumentId" }
        "QQ_LMAX_FIX_SECURITY_ID_SOURCE" { "FixSecurityIdSource" }
        "QQ_LMAX_MARKET_DATA_SYMBOL_ENCODING_MODE" { "MarketDataSymbolEncodingMode" }
        default { throw "unsupported_environment_name:$Name" }
    }

    return @($Name, "LmaxConnectivityLab__$logicalName", "LmaxConnectivityLab:$logicalName", $logicalName)
}

$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "tools\QQ.Production.Intraday.Lmax.ConnectivityLab\QQ.Production.Intraday.Lmax.ConnectivityLab.csproj"
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
    throw "connectivity_lab_project_not_found"
}

$awsArguments = [System.Collections.Generic.List[string]]@(
    "secretsmanager",
    "get-secret-value",
    "--secret-id",
    $SecretId,
    "--query",
    "SecretString",
    "--output",
    "text"
)
if (-not [string]::IsNullOrWhiteSpace($AwsProfile)) {
    $awsArguments.Add("--profile")
    $awsArguments.Add($AwsProfile)
}
if (-not [string]::IsNullOrWhiteSpace($AwsRegion)) {
    $awsArguments.Add("--region")
    $awsArguments.Add($AwsRegion)
}

$secretOutput = & $AwsCliPath @awsArguments 2>$null
if ($LASTEXITCODE -ne 0) {
    throw "aws_secretsmanager_get_secret_value_failed:$LASTEXITCODE"
}

$secretText = [string]::Join([Environment]::NewLine, [string[]]$secretOutput)
try {
    $secret = $secretText | ConvertFrom-Json -ErrorAction Stop
}
catch {
    throw "aws_secret_string_not_json"
}

if ($null -eq $secret) {
    throw "aws_secret_string_not_json"
}

$requiredEnvironmentNames = @(
    "QQ_LMAX_ENVIRONMENT",
    "QQ_LMAX_ACCOUNT_CODE",
    "QQ_LMAX_FIX_ORDER_HOST",
    "QQ_LMAX_FIX_ORDER_PORT",
    "QQ_LMAX_FIX_MARKET_DATA_HOST",
    "QQ_LMAX_FIX_MARKET_DATA_PORT",
    "QQ_LMAX_FIX_SENDER_COMP_ID",
    "QQ_LMAX_FIX_USERNAME",
    "QQ_LMAX_FIX_PASSWORD",
    "QQ_LMAX_USE_TLS",
    "QQ_LMAX_INSTRUMENT_SYMBOL",
    "QQ_LMAX_INSTRUMENT_ID",
    "QQ_LMAX_FIX_SECURITY_ID_SOURCE",
    "QQ_LMAX_MARKET_DATA_SYMBOL_ENCODING_MODE"
)

$processOverrides = [ordered]@{}
foreach ($environmentName in $requiredEnvironmentNames) {
    $processOverrides[$environmentName] = Get-SecretField `
        -Secret $secret `
        -EnvironmentName $environmentName `
        -Names (Get-SecretFieldNames -Name $environmentName)
}

$orderTarget = Get-SecretField -Secret $secret -EnvironmentName "QQ_LMAX_FIX_ORDER_TARGET_COMP_ID" -Names @(
    "QQ_LMAX_FIX_ORDER_TARGET_COMP_ID",
    "LmaxConnectivityLab__FixOrderTargetCompId",
    "LmaxConnectivityLab:FixOrderTargetCompId",
    "FixOrderTargetCompId"
) -Required $false
$marketDataTarget = Get-SecretField -Secret $secret -EnvironmentName "QQ_LMAX_FIX_MARKET_DATA_TARGET_COMP_ID" -Names @(
    "QQ_LMAX_FIX_MARKET_DATA_TARGET_COMP_ID",
    "LmaxConnectivityLab__FixMarketDataTargetCompId",
    "LmaxConnectivityLab:FixMarketDataTargetCompId",
    "FixMarketDataTargetCompId"
) -Required $true
$sharedTarget = Get-SecretField -Secret $secret -EnvironmentName "QQ_LMAX_FIX_TARGET_COMP_ID" -Names @(
    "QQ_LMAX_FIX_TARGET_COMP_ID",
    "LmaxConnectivityLab__FixTargetCompId",
    "LmaxConnectivityLab:FixTargetCompId",
    "FixTargetCompId"
) -Required $false

if ($null -eq $orderTarget -and $null -eq $sharedTarget) {
    throw "required_secret_field_missing:QQ_LMAX_FIX_ORDER_TARGET_COMP_ID"
}
if ($null -ne $orderTarget) {
    $processOverrides["QQ_LMAX_FIX_ORDER_TARGET_COMP_ID"] = $orderTarget
}
else {
    $processOverrides["QQ_LMAX_FIX_TARGET_COMP_ID"] = $sharedTarget
}
$processOverrides["QQ_LMAX_FIX_MARKET_DATA_TARGET_COMP_ID"] = $marketDataTarget

if (-not [string]::Equals($processOverrides["QQ_LMAX_ENVIRONMENT"], "Production", [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "secret_environment_not_production"
}
if (-not [bool]::Parse($processOverrides["QQ_LMAX_USE_TLS"])) {
    throw "secret_tls_not_enabled"
}

$processOverrides["QQ_LMAX_ALLOW_EXTERNAL_CONNECTIONS"] = "false"
$processOverrides["QQ_LMAX_ALLOW_ORDER_SUBMISSION"] = "false"
$processOverrides["QQ_LMAX_ALLOW_LIVE_TRADING"] = "false"
$processOverrides["QQ_LMAX_DRY_RUN"] = "true"

$originalValues = @{}
foreach ($environmentName in $processOverrides.Keys) {
    $originalValues[$environmentName] = [Environment]::GetEnvironmentVariable($environmentName, "Process")
}

try {
    foreach ($environmentName in $processOverrides.Keys) {
        [Environment]::SetEnvironmentVariable($environmentName, $processOverrides[$environmentName], "Process")
    }

    & dotnet run --project $project --no-build --no-restore -- print-config
    if ($LASTEXITCODE -ne 0) {
        throw "connectivity_lab_print_config_failed:$LASTEXITCODE"
    }
}
finally {
    foreach ($environmentName in $processOverrides.Keys) {
        [Environment]::SetEnvironmentVariable($environmentName, $originalValues[$environmentName], "Process")
    }

    $secret = $null
    $secretText = $null
    $secretOutput = $null
}
