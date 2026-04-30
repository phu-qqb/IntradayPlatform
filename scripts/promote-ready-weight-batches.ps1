param(
  [string]$BaseUrl = "http://localhost:5050",
  [int]$Limit = 10
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "Post /model-weight-batches/promote-ready"
Invoke-RestMethod "$BaseUrl/model-weight-batches/promote-ready" -Method Post -ContentType "application/json" -Body (@{ limit = $Limit } | ConvertTo-Json)
