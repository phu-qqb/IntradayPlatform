param(
    [string]$BaseUrl = "http://localhost:5050"
)

$ErrorActionPreference = "Stop"

function Invoke-LocalApi {
    param(
        [string]$Method,
        [string]$Path
    )

    $uri = "$BaseUrl$Path"
    try {
        return Invoke-RestMethod -Method $Method -Uri $uri
    }
    catch {
        Write-Host "Reference data check request failed." -ForegroundColor Red
        Write-Host "Endpoint: $Method $uri"
        if ($_.Exception.Response) {
            Write-Host "HTTP status: $([int]$_.Exception.Response.StatusCode) $($_.Exception.Response.StatusCode)"
            try {
                $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
                Write-Host "Response body:"
                Write-Host $reader.ReadToEnd()
            }
            catch {
                Write-Host "Response body could not be read."
            }
        }

        throw
    }
}

$result = Invoke-LocalApi -Method "GET" -Path "/admin/reference-data/integrity"

Write-Host "Reference data integrity checked at $($result.checkedAtUtc)"
Write-Host "Blocking issues: $($result.blockingIssueCount)"
Write-Host "Warning issues:  $($result.warningIssueCount)"

if ($result.issues.Count -gt 0) {
    Write-Host ""
    foreach ($issue in $result.issues) {
        $color = if ($issue.severity -eq "Blocking") { "Red" } elseif ($issue.severity -eq "Warning") { "Yellow" } else { "Gray" }
        Write-Host "[$($issue.severity)] $($issue.type) $($issue.key): $($issue.description)" -ForegroundColor $color
    }
}

if ($result.blockingIssueCount -gt 0) {
    exit 1
}

exit 0
