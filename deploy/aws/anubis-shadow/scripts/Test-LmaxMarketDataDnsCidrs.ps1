param(
    [string]$HostName = "fix-marketdata.london-demo.lmax.com",
    [int]$Port = 443,
    [string[]]$ExpectedCidrs = @(),
    [string]$ExpectedCidrsCsv = "",
    [string]$ExpectedCidrsJson = "",
    [switch]$Json
)

$ErrorActionPreference = "Stop"

function Fail([string]$Reason, [object]$Evidence) {
    $payload = [ordered]@{
        status = "FAIL_LMAX_MARKETDATA_DNS_CIDR_REVALIDATION"
        reason = $Reason
        source = "DNS_RESOLVED_CURRENT_LMAX_DEMO_MARKETDATA_ENDPOINT"
        stability = "NOT_CONTRACTUALLY_GUARANTEED"
        apply_requires_revalidation = $true
        evidence = $Evidence
    }
    $payload | ConvertTo-Json -Depth 8 | Write-Output
    exit 2
}

if ($ExpectedCidrsJson) {
    $ExpectedCidrs = @($ExpectedCidrsJson | ConvertFrom-Json)
} elseif ($ExpectedCidrsCsv) {
    $ExpectedCidrs = @($ExpectedCidrsCsv -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

$expected = @($ExpectedCidrs | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Sort-Object -Unique)
if ($HostName.ToLowerInvariant() -ne "fix-marketdata.london-demo.lmax.com") {
    Fail "UnexpectedEndpointHost" @{ host = $HostName }
}
if ($HostName -match "(?i)fix-order") {
    Fail "OrderEntryEndpointRejected" @{ host = $HostName }
}
if ($Port -ne 443) {
    Fail "UnexpectedEndpointPort" @{ host = $HostName; port = $Port }
}
if ($expected.Count -eq 0) {
    Fail "ExpectedCidrsRequired" @{ host = $HostName; port = $Port }
}
if ($expected | Where-Object { $_ -eq "0.0.0.0/0" -or $_ -notmatch "^\d{1,3}(\.\d{1,3}){3}/32$" }) {
    Fail "ExpectedCidrsMustBeExplicitIpv4Slash32" @{ expected_cidrs = $expected }
}

$addresses = [System.Net.Dns]::GetHostAddresses($HostName) |
    Where-Object { $_.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork } |
    ForEach-Object { $_.IPAddressToString } |
    Sort-Object -Unique

$resolvedCidrs = @($addresses | ForEach-Object { "$PSItem/32" } | Sort-Object -Unique)
if ($resolvedCidrs.Count -eq 0) {
    Fail "NoARecordsResolved" @{ host = $HostName; port = $Port }
}

$expectedJoined = $expected -join ","
$resolvedJoined = $resolvedCidrs -join ","
$status = if ($expectedJoined -eq $resolvedJoined) { "PASS_LMAX_MARKETDATA_DNS_CIDR_REVALIDATION" } else { "FAIL_LMAX_MARKETDATA_DNS_CIDR_REVALIDATION" }
$result = [ordered]@{
    status = $status
    host = $HostName
    port = $Port
    resolved_at_utc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")
    resolved_a_records = $addresses
    resolved_cidrs = $resolvedCidrs
    expected_cidrs = $expected
    source = "DNS_RESOLVED_CURRENT_LMAX_DEMO_MARKETDATA_ENDPOINT"
    stability = "NOT_CONTRACTUALLY_GUARANTEED"
    apply_requires_revalidation = $true
}

$result | ConvertTo-Json -Depth 8 | Write-Output
if ($status -ne "PASS_LMAX_MARKETDATA_DNS_CIDR_REVALIDATION") { exit 2 }
