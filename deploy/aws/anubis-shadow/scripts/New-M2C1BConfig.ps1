param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [Parameter(Mandatory = $true)]
    [string]$RecorderRoot,

    [string]$MarketDataEndpointAlias = "LMAX_DEMO_MARKET_DATA_ONLY",

    [string]$CredentialReference = "aws-secretsmanager:market-data-only",

    [string]$TemplatePath = ""
)

$ErrorActionPreference = "Stop"

function ConvertTo-M2Json {
    param([Parameter(Mandatory = $true)] [object]$Value)
    return ($Value | ConvertTo-Json -Depth 8 -Compress)
}

function Get-Sha256Text {
    param([Parameter(Mandatory = $true)] [string]$Text)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes)) -replace "-", "").ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

if ([string]::IsNullOrWhiteSpace($TemplatePath)) {
    $TemplatePath = Join-Path (Split-Path -Parent $PSScriptRoot) "config\m2c1b_aws_capture_config.template.json"
}

if (-not (Test-Path -LiteralPath $TemplatePath)) {
    throw "template_not_found:$TemplatePath"
}

$template = Get-Content -Raw -LiteralPath $TemplatePath | ConvertFrom-Json

$config = [ordered]@{
    mode = [string]$template.mode
    environment = [string]$template.environment
    venue = [string]$template.venue
    market_data_endpoint_alias = $MarketDataEndpointAlias
    market_data_session_alias = [string]$template.market_data_session_alias
    market_data_credential_reference = $CredentialReference
    credential_scope = [string]$template.credential_scope
    instruments = @($template.instruments)
    output_root = $RecorderRoot
    max_duration_seconds = [int]$template.max_duration_seconds
    max_events = [int]$template.max_events
    max_total_bytes = [int64]$template.max_total_bytes
    minimum_free_disk_bytes = [int64]$template.minimum_free_disk_bytes
    quote_age_threshold_ms = [int]$template.quote_age_threshold_ms
    rotate_after_bytes = [int64]$template.rotate_after_bytes
    flush_interval_ms = [int]$template.flush_interval_ms
    allowed_outbound_fix_msg_types = @($template.allowed_outbound_fix_msg_types)
    tool_commit = [string]$template.tool_commit
    config_hash = ""
}

$hashPayload = ConvertTo-M2Json $config
$config.config_hash = Get-Sha256Text $hashPayload
$json = ConvertTo-M2Json $config

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
Set-Content -LiteralPath $OutputPath -Value $json -Encoding UTF8

[ordered]@{
    status = "CONFIG_WRITTEN"
    output_path = $OutputPath
    recorder_root = $RecorderRoot
    endpoint_alias = $MarketDataEndpointAlias
    credential_reference = $CredentialReference
    config_hash = $config.config_hash
    tool_commit = $config.tool_commit
} | ConvertTo-Json -Depth 4
