param(
    [string]$ApiBaseUrl = "http://localhost:5050"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "OK  $Message" -ForegroundColor Green
}

function Write-Failure {
    param([string]$Message)
    Write-Host "FAIL $Message" -ForegroundColor Red
}

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Equals {
    param(
        [object]$Expected,
        [object]$Actual,
        [string]$Message
    )
    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

function Get-ApiId {
    param([object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [string]) {
        return $Value
    }

    if ($Value -is [guid]) {
        return $Value.ToString("D")
    }

    if ($Value.PSObject.Properties.Name -contains "value") {
        return Get-ApiId $Value.value
    }

    if ($Value.PSObject.Properties.Name -contains "id") {
        return Get-ApiId $Value.id
    }

    return [string]$Value
}

function Get-ErrorBody {
    param([object]$ErrorRecord)

    try {
        $response = $ErrorRecord.Exception.Response
        if ($null -eq $response) {
            return $ErrorRecord.Exception.Message
        }

        $stream = $response.GetResponseStream()
        if ($null -eq $stream) {
            return $ErrorRecord.Exception.Message
        }

        $reader = New-Object System.IO.StreamReader($stream)
        return $reader.ReadToEnd()
    }
    catch {
        return $ErrorRecord.Exception.Message
    }
}

function Invoke-LocalApi {
    param(
        [ValidateSet("GET", "POST", "PUT", "DELETE")]
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [string]$OperatorId = $null,
        [switch]$AllowFailure
    )

    if (-not $ApiBaseUrl.StartsWith("http://localhost") -and -not $ApiBaseUrl.StartsWith("http://127.0.0.1")) {
        throw "Refusing non-local API base URL: $ApiBaseUrl"
    }

    $uri = "$ApiBaseUrl$Path"
    $headers = @{}
    if ($OperatorId) {
        $headers["X-Operator-Id"] = $OperatorId
    }

    $safeBody = $null
    if ($null -ne $Body) {
        $safeBody = $Body | ConvertTo-Json -Depth 10
    }

    Write-Host "$Method $Path" -ForegroundColor DarkGray
    if ($safeBody) {
        Write-Host "body: $safeBody" -ForegroundColor DarkGray
    }

    try {
        if ($null -ne $Body) {
            return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType "application/json" -Body $safeBody
        }

        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
    }
    catch {
        $statusCode = $null
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }
        $errorBody = Get-ErrorBody $_
        Write-Host "HTTP status: $statusCode" -ForegroundColor Yellow
        Write-Host "response: $errorBody" -ForegroundColor Yellow

        if ($AllowFailure) {
            return [pscustomobject]@{
                failed = $true
                statusCode = $statusCode
                body = $errorBody
            }
        }

        throw
    }
}

try {
    Write-Step "Health and safety"
    $health = Invoke-LocalApi -Method GET -Path "/health"
    Assert-True ([bool]$health.databaseReachable) "Database must be reachable."
    Assert-Equals "FakeLmaxGateway" $health.executionGateway "Execution gateway must remain FakeLmaxGateway."
    Assert-Equals $false ([bool]$health.liveTradingEnabled) "Live trading must be false."
    Assert-Equals $false ([bool]$health.externalConnectionsEnabled) "External connections must be false."
    Write-Success "Health confirms FakeLmax-only local runtime."

    Write-Step "Seeded operators"
    $operators = Invoke-LocalApi -Method GET -Path "/operators"
    foreach ($operatorId in @("local-risk", "local-approver", "local-admin")) {
        Assert-True (@($operators | Where-Object { $_.operatorId -eq $operatorId }).Count -eq 1) "Missing seeded operator '$operatorId'."
    }
    Assert-Equals "local-risk" (Invoke-LocalApi -Method GET -Path "/operators/current" -OperatorId "local-risk").operatorId "Current operator should resolve local-risk."
    Assert-Equals "local-approver" (Invoke-LocalApi -Method GET -Path "/operators/current" -OperatorId "local-approver").operatorId "Current operator should resolve local-approver."
    Write-Success "Seeded local operators resolve through X-Operator-Id."

    Write-Step "Risk activation maker/checker"
    $active = Invoke-LocalApi -Method GET -Path "/risk/limit-sets/active?fundCode=QQ_MASTER&modelName=IntradayFxModel" -OperatorId "local-risk"
    $activeId = Get-ApiId $active.id
    Assert-True ($null -ne $activeId -and $activeId.Length -gt 0) "Active risk set id was not returned."
    Write-Success "Active risk set is $activeId."

    $draft = Invoke-LocalApi -Method POST -Path "/risk/limit-sets/$activeId/clone" -OperatorId "local-risk" -Body @{ reason = "Governance smoke test draft clone" }
    $draftId = Get-ApiId $draft.id
    Assert-True ($draftId -and $draft.status -eq "Draft") "Clone did not create a draft risk set."
    Write-Success "Draft risk set is $draftId."

    $activation = Invoke-LocalApi -Method POST -Path "/risk/limit-sets/$draftId/activate" -OperatorId "local-risk" -Body @{ reason = "Governance smoke test activation request" }
    Assert-Equals $true ([bool]$activation.approvalRequired) "Risk activation should require approval."
    $riskApprovalId = Get-ApiId $activation.approvalRequestId
    Assert-True ($riskApprovalId -and $riskApprovalId.Length -gt 0) "Risk activation approval id was not returned."
    $stillActive = Invoke-LocalApi -Method GET -Path "/risk/limit-sets/active?fundCode=QQ_MASTER&modelName=IntradayFxModel" -OperatorId "local-risk"
    Assert-Equals $activeId (Get-ApiId $stillActive.id) "Draft risk set should not be active before approval execution."
    Write-Success "Risk activation request is pending as approval $riskApprovalId."

    $selfApproval = Invoke-LocalApi -Method POST -Path "/approvals/$riskApprovalId/approve" -OperatorId "local-risk" -Body @{ reason = "Attempt self approval should fail" } -AllowFailure
    Assert-True ([bool]$selfApproval.failed) "Requester self-approval unexpectedly succeeded."
    Write-Success "Requester self-approval was blocked."

    $approvedRisk = Invoke-LocalApi -Method POST -Path "/approvals/$riskApprovalId/approve" -OperatorId "local-approver" -Body @{ reason = "Approved by local approver in governance smoke test" }
    Assert-Equals "Approved" $approvedRisk.status "Risk activation approval should be Approved."
    $executedRisk = Invoke-LocalApi -Method POST -Path "/approvals/$riskApprovalId/execute" -OperatorId "local-approver"
    Assert-Equals $true ([bool]$executedRisk.executed) "Risk activation approval should execute."
    $newActive = Invoke-LocalApi -Method GET -Path "/risk/limit-sets/active?fundCode=QQ_MASTER&modelName=IntradayFxModel" -OperatorId "local-risk"
    Assert-Equals $draftId (Get-ApiId $newActive.id) "Draft risk set should be active after execution."
    $secondExecute = Invoke-LocalApi -Method POST -Path "/approvals/$riskApprovalId/execute" -OperatorId "local-approver" -AllowFailure
    Assert-True ([bool]$secondExecute.failed) "Second approval execution unexpectedly succeeded."
    Write-Success "Risk activation approval executed once only."

    Write-Step "Kill switch clear maker/checker"
    $activatedKill = Invoke-LocalApi -Method POST -Path "/admin/kill-switch" -OperatorId "local-operator" -Body @{ reason = "Governance smoke test activate kill switch" }
    Assert-True ([bool]$activatedKill.active) "Kill switch should be active."

    $clearRequest = Invoke-LocalApi -Method POST -Path "/admin/kill-switch/clear" -OperatorId "local-risk" -Body @{ reason = "Governance smoke test clear request" }
    Assert-Equals $true ([bool]$clearRequest.approvalRequired) "Kill switch clear should require approval."
    $killApprovalId = Get-ApiId $clearRequest.approvalRequestId
    Assert-True ($killApprovalId -and $killApprovalId.Length -gt 0) "Kill switch clear approval id was not returned."
    $stillActiveKill = Invoke-LocalApi -Method GET -Path "/admin/kill-switch" -OperatorId "local-risk"
    Assert-True ([bool]$stillActiveKill.isActive) "Kill switch should remain active before approval execution."

    $approvedKill = Invoke-LocalApi -Method POST -Path "/approvals/$killApprovalId/approve" -OperatorId "local-approver" -Body @{ reason = "Approved kill switch clear for smoke test" }
    Assert-Equals "Approved" $approvedKill.status "Kill switch clear approval should be Approved."
    $executedKill = Invoke-LocalApi -Method POST -Path "/approvals/$killApprovalId/execute" -OperatorId "local-approver"
    Assert-Equals $true ([bool]$executedKill.executed) "Kill switch clear approval should execute."
    $clearedKill = Invoke-LocalApi -Method GET -Path "/admin/kill-switch" -OperatorId "local-risk"
    Assert-Equals $false ([bool]$clearedKill.isActive) "Kill switch should be cleared after approval execution."
    Write-Success "Kill switch clear approval executed and cleared the switch."

    Write-Step "Audit events"
    $auditEvents = @(Invoke-LocalApi -Method GET -Path "/audit/events?limit=100" -OperatorId "local-admin")
    foreach ($eventType in @("ApprovalRequestCreated", "ApprovalRequestApproved", "ApprovalRequestExecuted", "RiskLimitSetActivated", "KillSwitchActivated")) {
        Assert-True (@($auditEvents | Where-Object { $_.eventType -eq $eventType }).Count -gt 0) "Missing audit event '$eventType'."
    }
    $hasGovernedClear = @($auditEvents | Where-Object { $_.eventType -eq "KillSwitchCleared" -or ($_.eventType -eq "ApprovalRequestExecuted" -and $_.description -like "*Kill switch*") }).Count -gt 0
    Assert-True $hasGovernedClear "Missing kill switch clear or governed clear audit event."
    Assert-True (@($auditEvents | Where-Object { $_.eventType -eq "PermissionDenied" }).Count -gt 0) "Missing PermissionDenied audit event for failed self-approval."
    Write-Success "Approval and sensitive action audit events found."

    Write-Step "Approval list"
    $approvals = @(Invoke-LocalApi -Method GET -Path "/approvals?limit=100" -OperatorId "local-admin")
    Assert-True (@($approvals | Where-Object { $_.status -eq "Executed" }).Count -gt 0) "Expected at least one Executed approval."
    Assert-True (@($approvals | Where-Object { $_.id -eq $riskApprovalId -and $_.status -eq "Executed" }).Count -eq 1) "Risk activation approval should be Executed."
    Assert-True (@($approvals | Where-Object { $_.id -eq $killApprovalId -and $_.status -eq "Executed" }).Count -eq 1) "Kill switch clear approval should be Executed."
    Write-Success "Approval list contains executed risk and kill-switch requests."

    Write-Step "Governance smoke complete"
    Write-Success "Local maker/checker governance smoke passed."
    exit 0
}
catch {
    Write-Failure $_.Exception.Message
    exit 1
}
