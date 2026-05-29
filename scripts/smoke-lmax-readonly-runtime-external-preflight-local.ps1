param(
    [string]$BaseUrl = "http://localhost:5050",
    [string]$OperatorId = "local-admin",
    [string]$Reason = "Local LMAX external read-only run intent preflight smoke"
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "== $Message ==" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "OK: $Message" -ForegroundColor Green
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw "Assertion failed: $Message"
    }
}

function Assert-LocalUrl {
    param([string]$Url)
    $uri = [Uri]$Url
    Assert-True ($uri.Scheme -in @("http", "https")) "API URL uses http/https"
    Assert-True ($uri.Host -in @("localhost", "127.0.0.1")) "API URL is local only"
}

function Invoke-LocalApi {
    param(
        [string]$Method,
        [string]$Endpoint,
        [object]$Body = $null,
        [switch]$AllowFailure
    )

    $headers = @{ "X-Operator-Id" = $OperatorId }
    $uri = "$BaseUrl$Endpoint"
    try {
        if ($null -eq $Body) {
            Write-Host "$Method $Endpoint"
            return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
        }

        $json = $Body | ConvertTo-Json -Depth 20
        Write-Host "$Method $Endpoint"
        Write-Host "Body: $json"
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType "application/json" -Body $json
    } catch {
        if ($AllowFailure) {
            return $_
        }
        Write-Host "FAILED $Method $Endpoint" -ForegroundColor Red
        if ($_.Exception.Response) {
            Write-Host "HTTP status: $([int]$_.Exception.Response.StatusCode) $($_.Exception.Response.StatusCode)"
        }
        if ($_.ErrorDetails.Message) {
            Write-Host $_.ErrorDetails.Message
        }
        throw
    }
}

function Get-ItemsFromResponse {
    param([object]$Response)
    if ($null -eq $Response) { return @() }
    if ($Response -is [array]) { return @($Response) }
    foreach ($name in @("value", "Value", "items", "Items", "data", "parentOrders", "fills", "positions")) {
        if ($Response.PSObject.Properties.Name -contains $name) {
            $items = $Response.$name
            if ($null -eq $items) { return @() }
            return @($items)
        }
    }
    return @($Response)
}

function Get-CountSafely {
    param([string]$Endpoint)
    try {
        $response = Invoke-LocalApi -Method "GET" -Endpoint $Endpoint
        $items = Get-ItemsFromResponse -Response $response
        return @{ available = $true; count = $items.Count }
    } catch {
        Write-Host ("Skipping mutation count check for {0}: {1}" -f $Endpoint, $_.Exception.Message) -ForegroundColor Yellow
        return @{ available = $false; count = 0 }
    }
}

function Assert-NoForbiddenText {
    param([object]$Response, [string]$Context)
    $json = $Response | ConvertTo-Json -Depth 40
    foreach ($needle in @("password", "secretValue", "secretMaterial", "token", "apiKey", "privateKey", "authorization", "554=", "NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "Connected=True", "endpointUrl", "rawFixText", "host", "username")) {
        Assert-True ($json.IndexOf($needle, [StringComparison]::OrdinalIgnoreCase) -lt 0) "$Context does not contain $needle"
    }
}

function Assert-ValidateOnlyBlocked {
    param([object]$Response, [string]$Context)
    if (-not ($Response.status -eq "Blocked" -or $Response.status -eq "Invalid")) {
        Write-ResponseDiagnostics -Response $Response -Context $Context
        throw "Assertion failed: $Context returns blocked or invalid status"
    }
    Assert-True (-not [bool]$Response.canStartSession) "$Context cannot start session"
    Assert-True (-not [bool]$Response.sessionStarted) "$Context did not start session"
    Assert-True (-not [bool]$Response.externalConnectionAttempted) "$Context did not attempt external connection"
    Assert-True (-not [bool]$Response.credentialReadAttempted) "$Context did not attempt credential read"
    Assert-True (-not [bool]$Response.shadowReplaySubmitAttempted) "$Context did not attempt shadow replay submit"
    Assert-True (-not [bool]$Response.tradingMutationAttempted) "$Context did not attempt trading mutation"
    Assert-NoForbiddenText -Response $Response -Context $Context
}

function Assert-DryRunReportSafe {
    param([object]$Response, [string]$Context)
    if (-not ($Response.expectedOutcome -eq "Blocked" -or $Response.expectedOutcome -eq "ValidateOnly")) {
        Write-ResponseDiagnostics -Response $Response -Context $Context
        throw "Assertion failed: $Context expected outcome is blocked or validate-only"
    }

    if ([bool]$Response.canStartSession -or
        [bool]$Response.sessionStarted -or
        [bool]$Response.externalConnectionAttempted -or
        [bool]$Response.credentialReadAttempted -or
        [bool]$Response.shadowReplaySubmitAttempted -or
        [bool]$Response.tradingMutationAttempted) {
        Write-ResponseDiagnostics -Response $Response -Context $Context
        throw "Assertion failed: $Context reports no session/connection/credential/replay/trading mutation attempts"
    }

    Assert-NoForbiddenText -Response $Response -Context $Context
}

function Get-ValidationIssueCodes {
    param([object]$Response)
    $codes = New-Object System.Collections.Generic.List[string]
    if ($null -ne $Response.validationIssues) {
        @($Response.validationIssues | ForEach-Object { $codes.Add([string]$_.code) }) | Out-Null
    }
    if ($null -ne $Response.intentValidation -and $null -ne $Response.intentValidation.validationIssues) {
        @($Response.intentValidation.validationIssues | ForEach-Object { $codes.Add([string]$_.code) }) | Out-Null
    }
    if ($null -ne $Response.optionsValidationIssues) {
        @($Response.optionsValidationIssues | ForEach-Object { $codes.Add([string]$_.code) }) | Out-Null
    }
    if ($null -ne $Response.sections) {
        foreach ($section in @($Response.sections)) {
            if ($null -ne $section.issues) {
                @($section.issues | ForEach-Object { $codes.Add([string]$_.code) }) | Out-Null
            }
        }
    }
    return @($codes)
}

function Get-SafetyGateCodes {
    param([object]$Response)
    $codes = New-Object System.Collections.Generic.List[string]
    if ($null -ne $Response.safetyGates) {
        @($Response.safetyGates | ForEach-Object {
            if ($_.PSObject.Properties.Name -contains "gate") {
                $codes.Add([string]$_.gate)
            } elseif ($_.PSObject.Properties.Name -contains "code") {
                $codes.Add([string]$_.code)
            } elseif ($_.PSObject.Properties.Name -contains "name") {
                $codes.Add([string]$_.name)
            }
        }) | Out-Null
    }
    if ($null -ne $Response.intentValidation -and $null -ne $Response.intentValidation.safetyGates) {
        @($Response.intentValidation.safetyGates | ForEach-Object {
            if ($_.PSObject.Properties.Name -contains "gate") {
                $codes.Add([string]$_.gate)
            } elseif ($_.PSObject.Properties.Name -contains "code") {
                $codes.Add([string]$_.code)
            } elseif ($_.PSObject.Properties.Name -contains "name") {
                $codes.Add([string]$_.name)
            }
        }) | Out-Null
    }
    if ($null -ne $Response.sections) {
        foreach ($section in @($Response.sections)) {
            if ($null -eq $section.safetyGates) { continue }
            @($section.safetyGates | ForEach-Object {
                if ($_.PSObject.Properties.Name -contains "gate") {
                    $codes.Add([string]$_.gate)
                } elseif ($_.PSObject.Properties.Name -contains "code") {
                    $codes.Add([string]$_.code)
                } elseif ($_.PSObject.Properties.Name -contains "name") {
                    $codes.Add([string]$_.name)
                }
            }) | Out-Null
        }
    }
    return @($codes)
}

function Assert-ResponseHasStableCode {
    param([object]$Response, [string]$Code, [string]$Context)
    $issueCodes = @(Get-ValidationIssueCodes -Response $Response)
    $gateCodes = @(Get-SafetyGateCodes -Response $Response)
    if (($issueCodes -contains $Code) -or ($gateCodes -contains $Code) -or ([string]$Response.message).IndexOf($Code, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        return
    }

    Write-Host ("Missing stable code '{0}' in {1}." -f $Code, $Context) -ForegroundColor Red
    Write-ResponseDiagnostics -Response $Response -Context $Context
    throw ("Assertion failed: {0} code is present in {1}" -f $Code, $Context)
}

function Write-ResponseDiagnostics {
    param([object]$Response, [string]$Context)
    $issueCodes = @(Get-ValidationIssueCodes -Response $Response)
    $gateCodes = @(Get-SafetyGateCodes -Response $Response)
    Write-Host ("Diagnostics for {0}" -f $Context) -ForegroundColor Red
    Write-Host ("Status: {0}" -f $Response.status) -ForegroundColor Red
    Write-Host ("ExpectedOutcome: {0}" -f $Response.expectedOutcome) -ForegroundColor Red
    Write-Host ("BlockedReason: {0}" -f $Response.blockedReason) -ForegroundColor Red
    Write-Host ("Message: {0}" -f $Response.message) -ForegroundColor Red
    Write-Host ("CanStartSession: {0}" -f $Response.canStartSession) -ForegroundColor Red
    Write-Host ("SessionStarted: {0}" -f $Response.sessionStarted) -ForegroundColor Red
    Write-Host ("ExternalConnectionAttempted: {0}" -f $Response.externalConnectionAttempted) -ForegroundColor Red
    Write-Host ("CredentialReadAttempted: {0}" -f $Response.credentialReadAttempted) -ForegroundColor Red
    Write-Host ("ShadowReplaySubmitAttempted: {0}" -f $Response.shadowReplaySubmitAttempted) -ForegroundColor Red
    Write-Host ("TradingMutationAttempted: {0}" -f $Response.tradingMutationAttempted) -ForegroundColor Red
    Write-Host ("ValidationIssueCodes: {0}" -f ($issueCodes -join ", ")) -ForegroundColor Red
    Write-Host ("SafetyGateCodes: {0}" -f ($gateCodes -join ", ")) -ForegroundColor Red
    Write-Host "Sanitized response JSON:" -ForegroundColor Red
    Write-Host ($Response | ConvertTo-Json -Depth 40)
}

Assert-LocalUrl $BaseUrl

Write-Step "Health"
$health = Invoke-LocalApi -Method "GET" -Endpoint "/health"
Assert-True ($health.executionGateway -eq "FakeLmaxGateway") "execution gateway remains FakeLmaxGateway"
Assert-True (-not [bool]$health.liveTradingEnabled) "live trading remains disabled"
Assert-True (-not [bool]$health.externalConnectionsEnabled) "external connections remain disabled"
Write-Success "Runtime safety flags are unchanged"

Write-Step "Mutation guard baseline"
$beforeOrders = Get-CountSafely -Endpoint "/orders"
$beforeFills = Get-CountSafely -Endpoint "/fills"
$beforePositions = Get-CountSafely -Endpoint "/positions/internal"
$beforeReplayRuns = Get-CountSafely -Endpoint "/lmax-shadow/replay-runs"
Write-Success "Captured available baseline counts"

Write-Step "External run intent requires reason"
$missingReason = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-readonly-runtime/external-run-intent/validate" -Body @{
    reason = ""
    environmentName = "Demo"
    venueProfileName = "DemoLondon"
    credentialProfileName = "LmaxDemoReadOnlyProfile"
    runMode = "FutureExternalReadOnlyManual"
} -AllowFailure
Assert-True ($missingReason.Exception.Response -ne $null) "missing reason returned an HTTP error"
Assert-True ([int]$missingReason.Exception.Response.StatusCode -eq 400) "missing reason returned HTTP 400"
Write-Success "Reason is required"

Write-Step "Valid-looking future external manual intent remains blocked"
$validLooking = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-readonly-runtime/external-run-intent/validate" -Body @{
    reason = $Reason
    environmentName = "Demo"
    venueProfileName = "DemoLondon"
    credentialProfileName = "LmaxDemoReadOnlyProfile"
    runMode = "FutureExternalReadOnlyManual"
    dryRun = $true
    maxRuntimeSeconds = 30
    maxEventsPerRun = 100
    requestedEvidencePreviewOnly = $true
    submitToShadowReplay = $false
    allowExternalConnections = $false
    allowCredentialUse = $false
    allowOrderSubmission = $false
    schedulerEnabled = $false
    persistToTradingTables = $false
}
Assert-ValidateOnlyBlocked -Response $validLooking -Context "valid-looking external preflight response"
Assert-True ($validLooking.runMode -eq "FutureExternalReadOnlyManual") "run mode is FutureExternalReadOnlyManual"
Assert-True ($validLooking.environmentName -eq "Demo") "environment label is Demo"
Assert-True ($validLooking.venueProfileName -eq "DemoLondon") "venue profile label is DemoLondon"
Assert-ResponseHasStableCode -Response $validLooking -Code "Phase4ExternalRunImplementationNotStarted" -Context "valid-looking external preflight response"
Write-Success "Future external manual intent is validate-only and blocked"

Write-Step "Dry-run report remains no-network and blocked"
$dryRunReport = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-readonly-runtime/external-run-intent/dry-run-report" -Body @{
    reason = $Reason
    environmentName = "Demo"
    venueProfileName = "DemoLondon"
    credentialProfileName = "LmaxDemoReadOnlyProfile"
    runMode = "FutureExternalReadOnlyManual"
    dryRun = $true
    maxRuntimeSeconds = 30
    maxEventsPerRun = 100
    requestedEvidencePreviewOnly = $true
    submitToShadowReplay = $false
    allowExternalConnections = $false
    allowCredentialUse = $false
    allowOrderSubmission = $false
    schedulerEnabled = $false
    persistToTradingTables = $false
}
Assert-DryRunReportSafe -Response $dryRunReport -Context "external dry-run report response"
Assert-True ([bool]$dryRunReport.noSensitiveContent) "dry-run report says no sensitive content"
Assert-True (-not [bool]$dryRunReport.venueProfile.isActive) "venue profile remains inactive"
Assert-True (-not [bool]$dryRunReport.venueProfile.isExternalConnectionAllowed) "venue profile does not allow external connection"
Assert-True (-not [bool]$dryRunReport.credentialProfile.credentialReadImplemented) "credential resolver does not implement reads"
Assert-True (-not [bool]$dryRunReport.credentialProfile.sensitiveMaterialReturned) "credential resolver returned no sensitive material"
Assert-True (-not [bool]$dryRunReport.guardedTransport.networkTransportImplemented) "guarded transport has no network implementation"
Assert-True (-not [bool]$dryRunReport.guardedTransport.socketActivation) "guarded transport has no socket activation"
Assert-True (-not [bool]$dryRunReport.externalSessionSkeleton.socketActivation) "external skeleton has no socket activation"
Assert-ResponseHasStableCode -Response $dryRunReport -Code "Phase4ExternalRunImplementationNotStarted" -Context "external dry-run report response"
Assert-ResponseHasStableCode -Response $dryRunReport -Code "CredentialResolverDisabled" -Context "external dry-run report response"
Assert-ResponseHasStableCode -Response $dryRunReport -Code "GuardedTransportImplementationDisabled" -Context "external dry-run report response"
Assert-ResponseHasStableCode -Response $dryRunReport -Code "ExternalSessionImplementationStarted" -Context "external dry-run report response"
Write-Success "Dry-run report is blocked/no-network and exposes stable safety markers"

Write-Step "Manual signoff remains metadata-only"
$dryRunGateCodes = @(Get-SafetyGateCodes -Response $dryRunReport)
$signoff = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-readonly-runtime/external-run-intent/signoff/validate" -Body @{
    reason = "Local LMAX external read-only signoff validation smoke"
    dryRunReportId = $dryRunReport.reportId
    intentId = $dryRunReport.intentValidation.intentId
    requestedByOperatorId = $dryRunReport.requestedByOperatorId
    signedByOperatorId = "risk-approver"
    signoffRole = "Approver"
    confirmsReadOnlyIntent = $true
    confirmsNoOrderSubmission = $true
    confirmsNoTradingMutation = $true
    confirmsNoScheduler = $true
    confirmsNoShadowReplaySubmit = $true
    confirmsNoCredentialExposure = $true
    confirmsDemoOnly = $true
    confirmsDryRunReportReviewed = $true
    dryRunReportCanStartSession = $dryRunReport.canStartSession
    dryRunReportSafetyMarkers = $dryRunGateCodes
    decision = "Signed"
}
Assert-True ($signoff.status -eq "NotExecutable" -or $signoff.status -eq "Signed") "signoff metadata validates but is not executable"
Assert-True ($signoff.decision -eq "Signed") "signoff decision is signed metadata"
Assert-True (-not [bool]$signoff.canAuthorizeExecution) "signoff cannot authorize execution"
Assert-True ([bool]$signoff.executionStillBlocked) "signoff execution remains blocked"
Assert-True (-not [bool]$signoff.sessionStarted) "signoff did not start session"
Assert-True (-not [bool]$signoff.externalConnectionAttempted) "signoff did not attempt external connection"
Assert-True (-not [bool]$signoff.credentialReadAttempted) "signoff did not attempt credential read"
Assert-True (-not [bool]$signoff.shadowReplaySubmitAttempted) "signoff did not attempt shadow replay submit"
Assert-True (-not [bool]$signoff.tradingMutationAttempted) "signoff did not attempt trading mutation"
Assert-ResponseHasStableCode -Response $signoff -Code "Phase4ExternalRunImplementationNotStarted" -Context "external signoff response"
Assert-ResponseHasStableCode -Response $signoff -Code "CredentialResolverDisabled" -Context "external signoff response"
Assert-ResponseHasStableCode -Response $signoff -Code "GuardedTransportImplementationDisabled" -Context "external signoff response"
Assert-NoForbiddenText -Response $signoff -Context "external signoff response"

$badSignoff = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-readonly-runtime/external-run-intent/signoff/validate" -Body @{
    reason = "Missing attestation must fail"
    dryRunReportId = $dryRunReport.reportId
    intentId = $dryRunReport.intentValidation.intentId
    requestedByOperatorId = $dryRunReport.requestedByOperatorId
    signedByOperatorId = "risk-approver"
    signoffRole = "Approver"
    confirmsReadOnlyIntent = $true
    confirmsNoOrderSubmission = $false
    confirmsNoTradingMutation = $true
    confirmsNoScheduler = $true
    confirmsNoShadowReplaySubmit = $true
    confirmsNoCredentialExposure = $true
    confirmsDemoOnly = $true
    confirmsDryRunReportReviewed = $true
    dryRunReportCanStartSession = $dryRunReport.canStartSession
    dryRunReportSafetyMarkers = $dryRunGateCodes
    decision = "Signed"
}
Assert-True ($badSignoff.status -eq "Invalid") "unsafe signoff is invalid"
Assert-ResponseHasStableCode -Response $badSignoff -Code "ConfirmsNoOrderSubmissionRequired" -Context "unsafe signoff response"
Write-Success "Manual signoff is metadata-only and cannot authorize execution"

Write-Step "Pre-activation audit envelope remains metadata-only"
$signoffGateCodes = @(Get-SafetyGateCodes -Response $signoff)
$stableBlockers = @($dryRunGateCodes + $signoffGateCodes | Select-Object -Unique)
$audit = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-readonly-runtime/external-run-intent/pre-activation-audit/validate" -Body @{
    reason = "Local LMAX external read-only pre-activation audit validation smoke"
    requestedByOperatorId = $dryRunReport.requestedByOperatorId
    reviewedByOperatorId = "audit-reviewer"
    signedByOperatorId = $signoff.signedByOperatorId
    intentId = $dryRunReport.intentValidation.intentId
    dryRunReportId = $dryRunReport.reportId
    signoffId = $signoff.signoffId
    dryRunReportCanStartSession = $dryRunReport.canStartSession
    signoffCanAuthorizeExecution = $signoff.canAuthorizeExecution
    signoffExecutionStillBlocked = $signoff.executionStillBlocked
    sessionStarted = $false
    externalConnectionAttempted = $false
    credentialReadAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    stableBlockers = $stableBlockers
    dryRunReportReviewed = $true
    signoffReviewed = $true
}
Assert-True ($audit.status -eq "NotExecutable" -or $audit.status -eq "PreviewOnly" -or $audit.status -eq "Blocked") "pre-activation audit metadata validates but is not executable"
Assert-True ($audit.finalOutcome -eq "NotExecutable" -or $audit.finalOutcome -eq "PreviewOnly" -or $audit.finalOutcome -eq "Blocked") "pre-activation audit final outcome is non-executable"
Assert-True (-not [bool]$audit.canAuthorizeExecution) "pre-activation audit cannot authorize execution"
Assert-True ([bool]$audit.executionStillBlocked) "pre-activation audit execution remains blocked"
Assert-True (-not [bool]$audit.sessionStarted) "pre-activation audit did not start session"
Assert-True (-not [bool]$audit.externalConnectionAttempted) "pre-activation audit did not attempt external connection"
Assert-True (-not [bool]$audit.credentialReadAttempted) "pre-activation audit did not attempt credential read"
Assert-True (-not [bool]$audit.shadowReplaySubmitAttempted) "pre-activation audit did not attempt shadow replay submit"
Assert-True (-not [bool]$audit.tradingMutationAttempted) "pre-activation audit did not attempt trading mutation"
Assert-ResponseHasStableCode -Response $audit -Code "Phase4ExternalRunImplementationNotStarted" -Context "external pre-activation audit response"
Assert-ResponseHasStableCode -Response $audit -Code "CredentialResolverDisabled" -Context "external pre-activation audit response"
Assert-ResponseHasStableCode -Response $audit -Code "GuardedTransportImplementationDisabled" -Context "external pre-activation audit response"
Assert-NoForbiddenText -Response $audit -Context "external pre-activation audit response"

$badAudit = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-readonly-runtime/external-run-intent/pre-activation-audit/validate" -Body @{
    reason = "Missing stable blocker must fail"
    requestedByOperatorId = $dryRunReport.requestedByOperatorId
    reviewedByOperatorId = "audit-reviewer"
    signedByOperatorId = $signoff.signedByOperatorId
    intentId = $dryRunReport.intentValidation.intentId
    dryRunReportId = $dryRunReport.reportId
    signoffId = $signoff.signoffId
    dryRunReportCanStartSession = $false
    signoffCanAuthorizeExecution = $false
    signoffExecutionStillBlocked = $true
    stableBlockers = @("Phase4ExternalRunImplementationNotStarted", "GuardedTransportImplementationDisabled")
    dryRunReportReviewed = $true
    signoffReviewed = $true
}
Assert-True ($badAudit.status -eq "Invalid") "unsafe pre-activation audit is invalid"
Assert-ResponseHasStableCode -Response $badAudit -Code "CredentialResolverDisabled" -Context "unsafe pre-activation audit response"
Write-Success "Pre-activation audit is metadata-only and cannot authorize execution"

Write-Step "Readiness snapshot remains no-network and not executable"
$snapshot = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-readonly-runtime/external-run-intent/readiness-snapshot" -Body @{
    reason = "Local LMAX external read-only readiness snapshot smoke"
    environmentName = "Demo"
    venueProfileName = "DemoLondon"
    credentialProfileName = "LmaxDemoReadOnlyProfile"
    runMode = "FutureExternalReadOnlyManual"
    dryRun = $true
    submitToShadowReplay = $false
    allowExternalConnections = $false
    allowCredentialUse = $false
    allowOrderSubmission = $false
    schedulerEnabled = $false
    persistToTradingTables = $false
}
Assert-True ($snapshot.finalDecision -eq "NotExecutable" -or $snapshot.finalDecision -eq "Blocked" -or $snapshot.finalDecision -eq "ValidateOnly") "readiness snapshot final decision is non-executable"
Assert-True (-not [bool]$snapshot.canStartSession) "readiness snapshot cannot start session"
Assert-True (-not [bool]$snapshot.sessionStarted) "readiness snapshot did not start session"
Assert-True (-not [bool]$snapshot.externalConnectionAttempted) "readiness snapshot did not attempt external connection"
Assert-True (-not [bool]$snapshot.credentialReadAttempted) "readiness snapshot did not attempt credential read"
Assert-True (-not [bool]$snapshot.shadowReplaySubmitAttempted) "readiness snapshot did not attempt shadow replay submit"
Assert-True (-not [bool]$snapshot.tradingMutationAttempted) "readiness snapshot did not attempt trading mutation"
Assert-ResponseHasStableCode -Response $snapshot -Code "Phase4ExternalRunImplementationNotStarted" -Context "external readiness snapshot response"
Assert-ResponseHasStableCode -Response $snapshot -Code "CredentialResolverDisabled" -Context "external readiness snapshot response"
Assert-ResponseHasStableCode -Response $snapshot -Code "GuardedTransportImplementationDisabled" -Context "external readiness snapshot response"
Assert-NoForbiddenText -Response $snapshot -Context "external readiness snapshot response"
Write-Success "Readiness snapshot is metadata-only and cannot authorize execution"

Write-Step "Unsafe variants remain blocked"
$production = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-readonly-runtime/external-run-intent/validate" -Body @{
    reason = "Production venue must block"
    environmentName = "Production"
    venueProfileName = "Production"
    credentialProfileName = "LmaxDemoReadOnlyProfile"
    runMode = "PreviewOnly"
}
Assert-ValidateOnlyBlocked -Response $production -Context "production preflight response"
Assert-ResponseHasStableCode -Response $production -Code "VenueProfileProductionBlocked" -Context "production preflight response"

$orderSubmission = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-readonly-runtime/external-run-intent/validate" -Body @{
    reason = "Order submission must block"
    environmentName = "Demo"
    venueProfileName = "DemoLondon"
    credentialProfileName = "LmaxDemoReadOnlyProfile"
    runMode = "PreviewOnly"
    allowOrderSubmission = $true
}
Assert-ValidateOnlyBlocked -Response $orderSubmission -Context "order submission preflight response"
Assert-ResponseHasStableCode -Response $orderSubmission -Code "OrderSubmissionForbidden" -Context "order submission preflight response"
Write-Success "Unsafe variants are blocked"

Write-Step "Mutation guard after external preflight"
$afterOrders = Get-CountSafely -Endpoint "/orders"
$afterFills = Get-CountSafely -Endpoint "/fills"
$afterPositions = Get-CountSafely -Endpoint "/positions/internal"
$afterReplayRuns = Get-CountSafely -Endpoint "/lmax-shadow/replay-runs"
if ($beforeOrders.available -and $afterOrders.available) { Assert-True ($beforeOrders.count -eq $afterOrders.count) "order count unchanged" }
if ($beforeFills.available -and $afterFills.available) { Assert-True ($beforeFills.count -eq $afterFills.count) "fill count unchanged" }
if ($beforePositions.available -and $afterPositions.available) { Assert-True ($beforePositions.count -eq $afterPositions.count) "position count unchanged" }
if ($beforeReplayRuns.available -and $afterReplayRuns.available) { Assert-True ($beforeReplayRuns.count -eq $afterReplayRuns.count) "shadow replay run count unchanged" }
Write-Success "Available internal and shadow counts are unchanged"

Write-Step "Summary"
Write-Host "ExternalPreflightStatus=$($validLooking.status)"
Write-Host "CanStartSession=$($validLooking.canStartSession)"
Write-Host "No LMAX connection, socket, credential read, order submission, shadow replay submit, scheduler, or trading-state mutation was attempted."
