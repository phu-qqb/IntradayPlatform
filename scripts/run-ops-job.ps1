param(
    [string]$BaseUrl = "http://localhost:5050",
    [Parameter(Mandatory = $true)]
    [string]$JobType,
    [Parameter(Mandatory = $true)]
    [string]$Reason,
    [string]$OperatorId = "local-admin"
)

$ErrorActionPreference = "Stop"

function Assert-LocalUrl {
    param([string]$Url)
    $uri = [Uri]$Url
    if ($uri.Host -notin @("localhost", "127.0.0.1")) {
        throw "Only localhost API URLs are allowed. Refusing $Url"
    }
}

function Invoke-LocalApi {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    $headers = @{ "X-Operator-Id" = $OperatorId }
    $params = @{
        Method = $Method
        Uri = "$BaseUrl$Path"
        Headers = $headers
    }
    if ($null -ne $Body) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 20)
    }

    try {
        Invoke-RestMethod @params
    }
    catch {
        Write-Host "API call failed: $Method $Path" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host $_.ErrorDetails.Message -ForegroundColor Red
        }
        throw
    }
}

Assert-LocalUrl $BaseUrl

Write-Host "Running local operational job $JobType as $OperatorId..."
$result = Invoke-LocalApi -Method "POST" -Path "/ops/jobs/run" -Body @{
    jobType = $JobType
    reason = $Reason
    input = @{}
}

Write-Host "JobRunId: $($result.id)"
Write-Host "Status: $($result.status)"
Write-Host "StartedAtUtc: $($result.startedAtUtc)"
Write-Host "CompletedAtUtc: $($result.completedAtUtc)"
if ($result.errorMessage) {
    Write-Host "Error: $($result.errorMessage)" -ForegroundColor Yellow
}
