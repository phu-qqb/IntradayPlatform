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

function Get-EquivalentSecretField {
    param(
        [Parameter(Mandatory)][psobject]$Secret,
        [Parameter(Mandatory)][string]$EnvironmentName,
        [Parameter(Mandatory)][string]$OrderName,
        [Parameter(Mandatory)][string]$MarketDataName
    )

    $orderValue = Get-SecretField -Secret $Secret -EnvironmentName $EnvironmentName -Names @($OrderName)
    $marketDataValue = Get-SecretField -Secret $Secret -EnvironmentName $EnvironmentName -Names @($MarketDataName)
    if (-not [string]::Equals($orderValue, $marketDataValue, [System.StringComparison]::Ordinal)) {
        throw "session_credential_mismatch:$EnvironmentName"
    }

    return $orderValue
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

$processOverrides = [ordered]@{
    "QQ_LMAX_FIX_ORDER_HOST" = Get-SecretField -Secret $secret -EnvironmentName "QQ_LMAX_FIX_ORDER_HOST" -Names @("QQ_LMAX_FIX_ORDER_HOST")
    "QQ_LMAX_FIX_ORDER_PORT" = Get-SecretField -Secret $secret -EnvironmentName "QQ_LMAX_FIX_ORDER_PORT" -Names @("QQ_LMAX_FIX_ORDER_PORT")
    "QQ_LMAX_FIX_ORDER_TARGET_COMP_ID" = Get-SecretField -Secret $secret -EnvironmentName "QQ_LMAX_FIX_ORDER_TARGET_COMP_ID" -Names @("QQ_LMAX_FIX_ORDER_TARGET_COMP_ID")
    "QQ_LMAX_FIX_MARKET_DATA_HOST" = Get-SecretField -Secret $secret -EnvironmentName "QQ_LMAX_FIX_MARKET_DATA_HOST" -Names @("QQ_LMAX_FIX_MARKETDATA_HOST")
    "QQ_LMAX_FIX_MARKET_DATA_PORT" = Get-SecretField -Secret $secret -EnvironmentName "QQ_LMAX_FIX_MARKET_DATA_PORT" -Names @("QQ_LMAX_FIX_MARKETDATA_PORT")
    "QQ_LMAX_FIX_MARKET_DATA_TARGET_COMP_ID" = Get-SecretField -Secret $secret -EnvironmentName "QQ_LMAX_FIX_MARKET_DATA_TARGET_COMP_ID" -Names @("QQ_LMAX_FIX_MARKETDATA_TARGET_COMP_ID")
    "QQ_LMAX_FIX_SENDER_COMP_ID" = Get-EquivalentSecretField -Secret $secret -EnvironmentName "QQ_LMAX_FIX_SENDER_COMP_ID" -OrderName "QQ_LMAX_FIX_ORDER_SENDER_COMP_ID" -MarketDataName "QQ_LMAX_FIX_MARKETDATA_SENDER_COMP_ID"
    "QQ_LMAX_FIX_USERNAME" = Get-EquivalentSecretField -Secret $secret -EnvironmentName "QQ_LMAX_FIX_USERNAME" -OrderName "QQ_LMAX_FIX_ORDER_USERNAME" -MarketDataName "QQ_LMAX_FIX_MARKETDATA_USERNAME"
    "QQ_LMAX_FIX_PASSWORD" = Get-EquivalentSecretField -Secret $secret -EnvironmentName "QQ_LMAX_FIX_PASSWORD" -OrderName "QQ_LMAX_FIX_ORDER_PASSWORD" -MarketDataName "QQ_LMAX_FIX_MARKETDATA_PASSWORD"
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
