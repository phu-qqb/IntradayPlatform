param(
    [string]$BaseUrl = "http://localhost:5050",
    [Parameter(Mandatory = $true)]
    [ValidateSet("StartOfDay", "IntradayCycle", "EndOfDay", "Manual", "Custom")]
    [string]$RunbookType,
    [Parameter(Mandatory = $true)]
    [string]$Reason,
    [string]$OperatorId = "local-admin"
)

$ErrorActionPreference = "Stop"

if ($BaseUrl -notmatch '^https?://(localhost|127\.0\.0\.1)(:\d+)?/?$') {
    throw "run-runbook.ps1 is local-only. Refusing non-local BaseUrl '$BaseUrl'."
}

$headers = @{ "X-Operator-Id" = $OperatorId }
$body = @{ runbookType = $RunbookType; reason = $Reason; input = @{} } | ConvertTo-Json -Depth 20

try {
    $result = Invoke-RestMethod -Method Post -Uri "$($BaseUrl.TrimEnd('/'))/ops/runbooks/run" -Headers $headers -ContentType "application/json" -Body $body
    $result | ConvertTo-Json -Depth 20
} catch {
    Write-Host "POST /ops/runbooks/run failed" -ForegroundColor Red
    Write-Host "Body: $body" -ForegroundColor DarkGray
    if ($_.ErrorDetails.Message) { Write-Host $_.ErrorDetails.Message -ForegroundColor Red }
    throw
}
