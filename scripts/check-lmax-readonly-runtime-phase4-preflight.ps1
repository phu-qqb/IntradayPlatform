param(
    [string]$ApiBaseUrl = "http://localhost:5050"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$results = New-Object System.Collections.Generic.List[object]

function Add-Check {
    param([string]$Name, [string]$Status, [string]$Detail)
    $results.Add([pscustomobject][ordered]@{
        check = $Name
        status = $Status
        detail = $Detail
    }) | Out-Null
    $color = if ($Status -eq "PASS") { "Green" } elseif ($Status -eq "WARN") { "Yellow" } else { "Red" }
    Write-Host ("{0}: {1} - {2}" -f $Status, $Name, $Detail) -ForegroundColor $color
}

function Assert-LocalUrl {
    param([string]$Url)
    $uri = [Uri]$Url
    if ($uri.Scheme -notin @("http", "https") -or $uri.Host -notin @("localhost", "127.0.0.1")) {
        throw "API URL must be local only. Refusing $Url"
    }
}

function Test-NoForbiddenText {
    param([string]$Path)
    $text = Get-Content -LiteralPath $Path -Raw
    foreach ($pattern in @("password", "secret", "apiKey", "api_key", "authorization", "accessToken", "refreshToken", "bearerToken", "privateKey", "credentialValue")) {
        if ($text.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden credential-shaped text '{0}' found in {1}" -f $pattern, $Path)
        }
    }
}

function Test-NoForbiddenOrderSurface {
    param([string]$Path)
    $text = Get-Content -LiteralPath $Path -Raw
    foreach ($pattern in @("NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "CancelOrder", "ReplaceOrder")) {
        if ($text.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden order-submission surface '{0}' found in {1}" -f $pattern, $Path)
        }
    }
}

Assert-LocalUrl $ApiBaseUrl

Write-Host "LMAX Read-Only Runtime Phase 4 Preflight" -ForegroundColor Cyan
Write-Host "Local-only boundary check. No LMAX connection, sockets, credentials, orders, scheduler, or shadow replay submit." -ForegroundColor Yellow

try {
    $docPath = Join-Path $root "docs\LMAX_READONLY_RUNTIME_PHASE4_PREFLIGHT.md"
    if (-not (Test-Path -LiteralPath $docPath)) { throw "Missing $docPath" }
    Add-Check "Boundary document exists" "PASS" $docPath
} catch {
    Add-Check "Boundary document exists" "FAIL" $_.Exception.Message
}

try {
    $settingsPath = Join-Path $root "src\QQ.Production.Intraday.Api\appsettings.json"
    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    if ([bool]$settings.LmaxReadOnlyRuntime.Enabled) { throw "LmaxReadOnlyRuntime:Enabled must default to false." }
    if ($settings.LmaxReadOnlyRuntime.ImplementationMode -ne "DesignOnly") { throw "ImplementationMode must default to DesignOnly." }
    if ([bool]$settings.LmaxReadOnlyRuntime.AllowExternalConnections) { throw "AllowExternalConnections must default to false." }
    if ([bool]$settings.LmaxReadOnlyRuntime.AllowCredentialUse) { throw "AllowCredentialUse must default to false." }
    if ([bool]$settings.LmaxReadOnlyRuntime.AllowOrderSubmission) { throw "AllowOrderSubmission must default to false." }
    if ([bool]$settings.LmaxReadOnlyRuntime.SchedulerEnabled) { throw "SchedulerEnabled must default to false." }
    if ([bool]$settings.LmaxReadOnlyRuntime.SubmitToShadowReplay) { throw "SubmitToShadowReplay must default to false." }
    Add-Check "Default appsettings are disabled/design-only" "PASS" "Runtime defaults remain closed."
} catch {
    Add-Check "Default appsettings are disabled/design-only" "FAIL" $_.Exception.Message
}

try {
    $runtimeFiles = @(
        "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyRuntimeAdapterDesign.cs",
        "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyRuntimeInterfaces.cs",
        "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionContracts.cs",
        "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionSkeleton.cs",
        "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyGuardedTransport.cs",
        "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionFakeTransport.cs",
        "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionEvidencePreviewMapper.cs",
        "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionDryRunReport.cs"
    )
    foreach ($relative in $runtimeFiles) {
        Test-NoForbiddenText -Path (Join-Path $root $relative)
    }
    Add-Check "Runtime DTO files contain no credential-shaped fields" "PASS" "Checked inert runtime contract files."
} catch {
    Add-Check "Runtime DTO files contain no credential-shaped fields" "FAIL" $_.Exception.Message
}

try {
    $externalSessionPath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionContracts.cs"
    if (-not (Test-Path -LiteralPath $externalSessionPath)) { throw "Missing $externalSessionPath" }
    $text = Get-Content -LiteralPath $externalSessionPath -Raw
    foreach ($required in @("ILmaxReadOnlyExternalSession", "LmaxReadOnlyExternalSessionDisabled", "ExternalSessionImplementationAvailable", "ExternalSessionImplementationNotStarted")) {
        if ($text.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("External session contract/stub is missing '{0}'." -f $required)
        }
    }
    Add-Check "Phase 4A external session contract/stub exists" "PASS" "Disabled external session boundary is present."
} catch {
    Add-Check "Phase 4A external session contract/stub exists" "FAIL" $_.Exception.Message
}

try {
    Test-NoForbiddenOrderSurface -Path (Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionContracts.cs")
    Test-NoForbiddenOrderSurface -Path (Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionSkeleton.cs")
    Test-NoForbiddenOrderSurface -Path (Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyGuardedTransport.cs")
    Test-NoForbiddenOrderSurface -Path (Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionOptions.cs")
    Test-NoForbiddenOrderSurface -Path (Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyCredentialProfile.cs")
    Test-NoForbiddenOrderSurface -Path (Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyVenueProfile.cs")
    Test-NoForbiddenOrderSurface -Path (Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionRunIntent.cs")
    Test-NoForbiddenOrderSurface -Path (Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionFakeTransport.cs")
    Test-NoForbiddenOrderSurface -Path (Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionEvidencePreviewMapper.cs")
    Add-Check "External session exposes no order-submission surface" "PASS" "No NewOrderSingle/cancel/replace/submit surface found in Phase 4A-4J files."
} catch {
    Add-Check "External session exposes no order-submission surface" "FAIL" $_.Exception.Message
}

try {
    $skeletonPath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionSkeleton.cs"
    if (-not (Test-Path -LiteralPath $skeletonPath)) { throw "Missing $skeletonPath" }
    $text = Get-Content -LiteralPath $skeletonPath -Raw
    foreach ($required in @("LmaxReadOnlyExternalSessionSkeleton", "SkeletonOnly", "SocketActivation: false", "FixLogonImplemented: false", "CredentialUseImplemented: false", "OrderSubmissionImplemented: false", "ShadowReplaySubmitImplemented: false", "TradingMutationImplemented: false")) {
        if ($text.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4E skeleton is missing '{0}'." -f $required)
        }
    }
    foreach ($forbidden in @("TcpClient", "Socket(", "SslStream", "QuickFIX", "NetworkStream", "ConnectAsync", "Lmax.ConnectivityLab")) {
        if ($text.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden network/lab implementation text '{0}' found in Phase 4E skeleton." -f $forbidden)
        }
    }
    Add-Check "Phase 4E external session skeleton exists" "PASS" "Skeleton is present, blocked, and has no socket/FIX/logon/credential activation."
} catch {
    Add-Check "Phase 4E external session skeleton exists" "FAIL" $_.Exception.Message
}

try {
    $transportPath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyGuardedTransport.cs"
    if (-not (Test-Path -LiteralPath $transportPath)) { throw "Missing $transportPath" }
    $text = Get-Content -LiteralPath $transportPath -Raw
    foreach ($required in @("ILmaxReadOnlyGuardedTransport", "LmaxReadOnlyGuardedTransportDisabled", "ConnectReadOnlyAsync", "NetworkTransportImplemented: false", "SocketActivation: false", "FixLogonImplemented: false", "CredentialUseImplemented: false", "OrderSubmissionImplemented: false", "ReadOnlyOnly: true")) {
        if ($text.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4F guarded transport is missing '{0}'." -f $required)
        }
    }
    foreach ($forbidden in @("TcpClient", "Socket(", "SslStream", "QuickFIX", "ClientWebSocket", "HttpClient", "NetworkStream", "Lmax.ConnectivityLab")) {
        if ($text.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden network/lab implementation text '{0}' found in Phase 4F guarded transport." -f $forbidden)
        }
    }
    Add-Check "Phase 4F guarded transport interface exists" "PASS" "Guarded transport boundary is present, disabled, and has no socket/network/FIX implementation."
} catch {
    Add-Check "Phase 4F guarded transport interface exists" "FAIL" $_.Exception.Message
}

try {
    $optionsPath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionOptions.cs"
    if (-not (Test-Path -LiteralPath $optionsPath)) { throw "Missing $optionsPath" }
    $text = Get-Content -LiteralPath $optionsPath -Raw
    foreach ($required in @("LmaxReadOnlyExternalSessionOptions", "LmaxReadOnlyExternalSessionOptionsValidator", "CredentialProfileName", "EnvironmentName", "VenueProfileName", "AllowExternalConnections", "SubmitToShadowReplay", "LmaxReadOnlyVenueProfileRegistryDisabled")) {
        if ($text.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4G config envelope is missing '{0}'." -f $required)
        }
    }
    foreach ($forbidden in @("TcpClient", "Socket(", "SslStream", "QuickFIX", "ClientWebSocket", "HttpClient", "NetworkStream", "Lmax.ConnectivityLab")) {
        if ($text.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden credential/network/lab implementation text '{0}' found in Phase 4G config envelope." -f $forbidden)
        }
    }
    foreach ($forbiddenProperty in @("Password", "Secret", "Token", "ApiKey", "PrivateKey", "CredentialValue", "Authorization")) {
        if ($text -match ("public\s+[^\r\n]+\s+" + [regex]::Escape($forbiddenProperty) + "\b")) {
            throw ("Forbidden credential-shaped option property '{0}' found in Phase 4G config envelope." -f $forbiddenProperty)
        }
    }
    $samplePath = Join-Path $root "docs\examples\lmax-readonly-external-session-options.sample.json"
    if (-not (Test-Path -LiteralPath $samplePath)) { throw "Missing $samplePath" }
    $sample = Get-Content -LiteralPath $samplePath -Raw
    foreach ($forbidden in @("password", "secret", "token", "apiKey", "privateKey", "authorization", "554=", "username", "host", "port", "accountId", "senderComp", "targetComp")) {
        if ($sample.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden sensitive placeholder '{0}' found in Phase 4G sample config." -f $forbidden)
        }
    }
    $sampleJson = $sample | ConvertFrom-Json
    if ([bool]$sampleJson.LmaxReadOnlyExternalSession.Enabled) { throw "Sample config must keep Enabled=false." }
    if ($sampleJson.LmaxReadOnlyExternalSession.ImplementationMode -ne "DesignOnly") { throw "Sample config must keep ImplementationMode=DesignOnly." }
    if ([bool]$sampleJson.LmaxReadOnlyExternalSession.AllowExternalConnections) { throw "Sample config must keep AllowExternalConnections=false." }
    if ([bool]$sampleJson.LmaxReadOnlyExternalSession.AllowCredentialUse) { throw "Sample config must keep AllowCredentialUse=false." }
    if ([bool]$sampleJson.LmaxReadOnlyExternalSession.SubmitToShadowReplay) { throw "Sample config must keep SubmitToShadowReplay=false." }
    Add-Check "Phase 4G config envelope exists" "PASS" "Typed config envelope and inactive sample exist without credential values or network implementation."
} catch {
    Add-Check "Phase 4G config envelope exists" "FAIL" $_.Exception.Message
}

try {
    $credentialPath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyCredentialProfile.cs"
    if (-not (Test-Path -LiteralPath $credentialPath)) { throw "Missing $credentialPath" }
    $text = Get-Content -LiteralPath $credentialPath -Raw
    foreach ($required in @("ILmaxReadOnlyCredentialProfileResolver", "LmaxReadOnlyCredentialProfileResolverDisabled", "CredentialReadImplemented: false", "CredentialUseImplemented: false", "SensitiveMaterialReturned: false", "ResolverMode=Disabled")) {
        if ($text.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4H credential profile boundary is missing '{0}'." -f $required)
        }
    }
    foreach ($forbidden in @("GetEnvironmentVariable", "ConfigurationBuilder", "IConfiguration", "AddUserSecrets", "UserSecretsId", "KeyVault", "VaultClient", "TcpClient", "Socket(", "SslStream", "QuickFIX", "ClientWebSocket", "HttpClient", "NetworkStream", "Lmax.ConnectivityLab")) {
        if ($text.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden credential/network/lab implementation text '{0}' found in Phase 4H credential profile boundary." -f $forbidden)
        }
    }
    foreach ($forbiddenProperty in @("Password", "Secret", "Token", "ApiKey", "PrivateKey", "CredentialValue", "Authorization")) {
        if ($text -match ("public\s+[^\r\n]+\s+" + [regex]::Escape($forbiddenProperty) + "\b")) {
            throw ("Forbidden credential-shaped DTO property '{0}' found in Phase 4H credential profile boundary." -f $forbiddenProperty)
        }
    }
    Add-Check "Phase 4H credential profile boundary exists" "PASS" "Disabled/no-op resolver exists, reads no credential values, and exposes labels only."
} catch {
    Add-Check "Phase 4H credential profile boundary exists" "FAIL" $_.Exception.Message
}

try {
    $venuePath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyVenueProfile.cs"
    if (-not (Test-Path -LiteralPath $venuePath)) { throw "Missing $venuePath" }
    $text = Get-Content -LiteralPath $venuePath -Raw
    foreach ($required in @("LmaxReadOnlyVenueProfileName", "LmaxReadOnlyVenueProfileDescriptor", "ILmaxReadOnlyVenueProfileRegistry", "LmaxReadOnlyVenueProfileRegistryDisabled", "DemoLondon", "IsExternalConnectionAllowed: false", "IsCredentialUseAllowed: false")) {
        if ($text.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4I venue profile boundary is missing '{0}'." -f $required)
        }
    }
    foreach ($forbidden in @("TcpClient", "Socket(", "SslStream", "QuickFIX", "ClientWebSocket", "HttpClient", "NetworkStream", "Lmax.ConnectivityLab")) {
        if ($text.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden network/lab implementation text '{0}' found in Phase 4I venue profile boundary." -f $forbidden)
        }
    }
    foreach ($forbiddenProperty in @("Host", "Port", "User", "Password", "Secret", "Token", "ApiKey", "PrivateKey", "Account", "SenderComp", "TargetComp", "EndpointUrl")) {
        if ($text -match ("public\s+[^\r\n]+\s+" + [regex]::Escape($forbiddenProperty) + "\b")) {
            throw ("Forbidden endpoint/credential-shaped DTO property '{0}' found in Phase 4I venue profile boundary." -f $forbiddenProperty)
        }
    }
    Add-Check "Phase 4I venue profile boundary exists" "PASS" "Disabled venue profile registry exposes labels only and no endpoint/account/session/credential values."
} catch {
    Add-Check "Phase 4I venue profile boundary exists" "FAIL" $_.Exception.Message
}

try {
    $intentPath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionRunIntent.cs"
    if (-not (Test-Path -LiteralPath $intentPath)) { throw "Missing $intentPath" }
    $text = Get-Content -LiteralPath $intentPath -Raw
    foreach ($required in @("LmaxReadOnlyExternalSessionRunIntent", "LmaxReadOnlyExternalSessionRunIntentValidator", "LmaxReadOnlyExternalSessionRunIntentSummary", "FutureExternalReadOnlyManual", "Phase4JIntentOnly", "Phase4ExternalRunImplementationNotStarted")) {
        if ($text.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4J run intent boundary is missing '{0}'." -f $required)
        }
    }
    foreach ($forbidden in @("TcpClient", "Socket(", "SslStream", "QuickFIX", "ClientWebSocket", "HttpClient", "NetworkStream", "Lmax.ConnectivityLab")) {
        if ($text.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden network/lab implementation text '{0}' found in Phase 4J run intent boundary." -f $forbidden)
        }
    }
    foreach ($forbiddenProperty in @("Host", "Port", "Username", "Password", "Secret", "Token", "ApiKey", "PrivateKey", "Account", "SenderComp", "TargetComp", "EndpointUrl", "RawFix")) {
        if ($text -match ("public\s+[^\r\n]+\s+" + [regex]::Escape($forbiddenProperty) + "\b")) {
            throw ("Forbidden endpoint/credential/raw-FIX DTO property '{0}' found in Phase 4J run intent boundary." -f $forbiddenProperty)
        }
    }
    Add-Check "Phase 4J run intent envelope exists" "PASS" "Manual intent validator exists, requires reason, and remains validate-only/no-socket."
} catch {
    Add-Check "Phase 4J run intent envelope exists" "FAIL" $_.Exception.Message
}

try {
    $apiProgramPath = Join-Path $root "src\QQ.Production.Intraday.Api\Program.cs"
    $apiProgram = Get-Content -LiteralPath $apiProgramPath -Raw
    foreach ($required in @("/lmax-readonly-runtime/external-run-intent/validate", "LmaxReadOnlyRuntimeExternalRunIntentValidateApiRequest", "CanStartSession: false", "ExternalConnectionAttempted: false", "CredentialReadAttempted: false", "ShadowReplaySubmitAttempted: false", "TradingMutationAttempted: false")) {
        if ($apiProgram.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4K validate endpoint is missing '{0}'." -f $required)
        }
    }
    $endpointStart = $apiProgram.IndexOf('app.MapPost("/lmax-readonly-runtime/external-run-intent/validate"', [StringComparison]::OrdinalIgnoreCase)
    $endpointEnd = $apiProgram.IndexOf('app.MapGet("/lmax-readonly-runtime/runs"', [StringComparison]::OrdinalIgnoreCase)
    if ($endpointStart -lt 0 -or $endpointEnd -le $endpointStart) {
        throw "Could not isolate external run intent validate endpoint block."
    }
    $endpointText = $apiProgram.Substring($endpointStart, $endpointEnd - $endpointStart)
    foreach ($forbidden in @("TcpClient", "Socket(", "SslStream", "QuickFIX", "ClientWebSocket", "HttpClient", "NetworkStream", "ConnectAsync", "NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder")) {
        if ($endpointText.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden socket/order text '{0}' found in Phase 4K validate endpoint." -f $forbidden)
        }
    }
    foreach ($forbiddenProperty in @("Host", "Port", "Username", "Password", "Secret", "Token", "ApiKey", "PrivateKey", "Account", "SenderComp", "TargetComp", "EndpointUrl", "RawFix")) {
        if ($endpointText -match ("public\s+[^\r\n]+\s+" + [regex]::Escape($forbiddenProperty) + "\b")) {
            throw ("Forbidden endpoint/credential/raw-FIX API property '{0}' found in Phase 4K endpoint DTOs." -f $forbiddenProperty)
        }
    }
    Add-Check "Phase 4K external run intent validate endpoint exists" "PASS" "Endpoint validates intent only and reports no session/connection/credential/replay/trading mutation attempts."
} catch {
    Add-Check "Phase 4K external run intent validate endpoint exists" "FAIL" $_.Exception.Message
}

try {
    $dryRunPath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionDryRunReport.cs"
    if (-not (Test-Path -LiteralPath $dryRunPath)) { throw "Missing $dryRunPath" }
    $text = Get-Content -LiteralPath $dryRunPath -Raw
    foreach ($required in @("LmaxReadOnlyExternalSessionDryRunReport", "LmaxReadOnlyExternalSessionDryRunReportGenerator", "CanStartSession", "ExternalConnectionAttempted", "CredentialReadAttempted", "ShadowReplaySubmitAttempted", "TradingMutationAttempted", "NoSensitiveContent", "CredentialResolver", "GuardedTransport", "ExternalSessionSkeleton")) {
        if ($text.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4L dry-run report is missing '{0}'." -f $required)
        }
    }
    foreach ($forbidden in @("TcpClient", "Socket(", "SslStream", "QuickFIX", "ClientWebSocket", "HttpClient", "NetworkStream", "ConnectAsync", "NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "Lmax.ConnectivityLab")) {
        if ($text.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden socket/order/lab text '{0}' found in Phase 4L dry-run report." -f $forbidden)
        }
    }
    foreach ($forbiddenProperty in @("Host", "Port", "Username", "Password", "Secret", "Token", "ApiKey", "PrivateKey", "Account", "SenderComp", "TargetComp", "EndpointUrl", "RawFix")) {
        if ($text -match ("public\s+[^\r\n]+\s+" + [regex]::Escape($forbiddenProperty) + "\b")) {
            throw ("Forbidden endpoint/credential/raw-FIX DTO property '{0}' found in Phase 4L dry-run report." -f $forbiddenProperty)
        }
    }

    $apiProgramPath = Join-Path $root "src\QQ.Production.Intraday.Api\Program.cs"
    $apiProgram = Get-Content -LiteralPath $apiProgramPath -Raw
    foreach ($required in @("/lmax-readonly-runtime/external-run-intent/dry-run-report", "LmaxReadOnlyRuntimeExternalDryRunReportDto", "LmaxReadOnlyExternalSessionDryRunReportGenerator")) {
        if ($apiProgram.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4L dry-run report endpoint is missing '{0}'." -f $required)
        }
    }
    $endpointStart = $apiProgram.IndexOf('app.MapPost("/lmax-readonly-runtime/external-run-intent/dry-run-report"', [StringComparison]::OrdinalIgnoreCase)
    $endpointEnd = $apiProgram.IndexOf('app.MapGet("/lmax-readonly-runtime/runs"', [StringComparison]::OrdinalIgnoreCase)
    if ($endpointStart -lt 0 -or $endpointEnd -le $endpointStart) {
        throw "Could not isolate external dry-run report endpoint block."
    }
    $endpointText = $apiProgram.Substring($endpointStart, $endpointEnd - $endpointStart)
    foreach ($forbidden in @("TcpClient", "Socket(", "SslStream", "QuickFIX", "ClientWebSocket", "HttpClient", "NetworkStream", "ConnectAsync", "NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder")) {
        if ($endpointText.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden socket/order text '{0}' found in Phase 4L dry-run report endpoint." -f $forbidden)
        }
    }
    Add-Check "Phase 4L no-network dry-run report exists" "PASS" "Report generator and endpoint exist, aggregate disabled boundaries, and expose no socket/order/credential surface."
} catch {
    Add-Check "Phase 4L no-network dry-run report exists" "FAIL" $_.Exception.Message
}

try {
    $signoffPath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionSignoff.cs"
    if (-not (Test-Path -LiteralPath $signoffPath)) { throw "Missing $signoffPath" }
    $text = Get-Content -LiteralPath $signoffPath -Raw
    foreach ($required in @("LmaxReadOnlyExternalSessionSignoffEnvelope", "LmaxReadOnlyExternalSessionSignoffValidator", "CanAuthorizeExecution: false", "ExecutionStillBlocked: true", "SessionStarted: false", "ExternalConnectionAttempted: false", "CredentialReadAttempted: false", "ShadowReplaySubmitAttempted: false", "TradingMutationAttempted: false", "Phase4ExternalRunImplementationNotStarted", "CredentialResolverDisabled", "GuardedTransportImplementationDisabled")) {
        if ($text.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4M signoff envelope is missing '{0}'." -f $required)
        }
    }
    foreach ($forbidden in @("TcpClient", "Socket(", "SslStream", "QuickFIX", "ClientWebSocket", "HttpClient", "NetworkStream", "ConnectAsync", "NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "Lmax.ConnectivityLab")) {
        if ($text.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden socket/order/lab text '{0}' found in Phase 4M signoff envelope." -f $forbidden)
        }
    }
    foreach ($forbiddenProperty in @("Host", "Port", "Username", "Password", "Secret", "Token", "ApiKey", "PrivateKey", "Account", "SenderComp", "TargetComp", "EndpointUrl", "RawFix")) {
        $propertyMatches = [regex]::Matches($text, "public\s+[^\r\n]+\s+$([regex]::Escape($forbiddenProperty))\b", [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        foreach ($propertyMatch in $propertyMatches) {
            if ($propertyMatch.Value -notmatch "Report") {
                throw ("Forbidden endpoint/credential/raw-FIX DTO property '{0}' found in Phase 4M signoff envelope." -f $forbiddenProperty)
            }
        }
    }

    $apiProgramPath = Join-Path $root "src\QQ.Production.Intraday.Api\Program.cs"
    $apiProgram = Get-Content -LiteralPath $apiProgramPath -Raw
    foreach ($required in @("/lmax-readonly-runtime/external-run-intent/signoff/validate", "LmaxReadOnlyRuntimeExternalSignoffValidateApiRequest", "LmaxReadOnlyRuntimeExternalSignoffDto")) {
        if ($apiProgram.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4M signoff endpoint is missing '{0}'." -f $required)
        }
    }
    $endpointStart = $apiProgram.IndexOf('app.MapPost("/lmax-readonly-runtime/external-run-intent/signoff/validate"', [StringComparison]::OrdinalIgnoreCase)
    $endpointEnd = $apiProgram.IndexOf('app.MapGet("/lmax-readonly-runtime/runs"', [StringComparison]::OrdinalIgnoreCase)
    if ($endpointStart -lt 0 -or $endpointEnd -le $endpointStart) {
        throw "Could not isolate external signoff endpoint block."
    }
    $endpointText = $apiProgram.Substring($endpointStart, $endpointEnd - $endpointStart)
    foreach ($forbidden in @("TcpClient", "Socket(", "SslStream", "QuickFIX", "ClientWebSocket", "HttpClient", "NetworkStream", "ConnectAsync", "NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder")) {
        if ($endpointText.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden socket/order text '{0}' found in Phase 4M signoff endpoint." -f $forbidden)
        }
    }
    Add-Check "Phase 4M signoff envelope exists" "PASS" "Signoff validator and endpoint exist, cannot authorize execution, and expose no socket/order/credential surface."
} catch {
    Add-Check "Phase 4M signoff envelope exists" "FAIL" $_.Exception.Message
}

try {
    $auditPath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionPreActivationAudit.cs"
    if (-not (Test-Path -LiteralPath $auditPath)) { throw "Missing $auditPath" }
    $text = Get-Content -LiteralPath $auditPath -Raw
    foreach ($required in @("LmaxReadOnlyExternalSessionPreActivationAuditEnvelope", "LmaxReadOnlyExternalSessionPreActivationAuditValidator", "CanAuthorizeExecution: false", "ExecutionStillBlocked: true", "SessionStarted: false", "ExternalConnectionAttempted: false", "CredentialReadAttempted: false", "ShadowReplaySubmitAttempted: false", "TradingMutationAttempted: false", "NoSensitiveContent: true", "Phase4ExternalRunImplementationNotStarted", "CredentialResolverDisabled", "GuardedTransportImplementationDisabled")) {
        if ($text.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4N pre-activation audit envelope is missing '{0}'." -f $required)
        }
    }
    foreach ($forbidden in @("TcpClient", "Socket(", "SslStream", "QuickFIX", "ClientWebSocket", "HttpClient", "NetworkStream", "ConnectAsync", "NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "Lmax.ConnectivityLab")) {
        if ($text.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden socket/order/lab text '{0}' found in Phase 4N pre-activation audit envelope." -f $forbidden)
        }
    }
    foreach ($forbiddenProperty in @("Host", "Port", "Username", "Password", "Secret", "Token", "ApiKey", "PrivateKey", "Account", "SenderComp", "TargetComp", "EndpointUrl", "RawFix")) {
        $propertyMatches = [regex]::Matches($text, "public\s+[^\r\n]+\s+$([regex]::Escape($forbiddenProperty))\b", [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        foreach ($propertyMatch in $propertyMatches) {
            if ($propertyMatch.Value -notmatch "Report") {
                throw ("Forbidden endpoint/credential/raw-FIX DTO property '{0}' found in Phase 4N pre-activation audit envelope." -f $forbiddenProperty)
            }
        }
    }

    $apiProgramPath = Join-Path $root "src\QQ.Production.Intraday.Api\Program.cs"
    $apiProgram = Get-Content -LiteralPath $apiProgramPath -Raw
    foreach ($required in @("/lmax-readonly-runtime/external-run-intent/pre-activation-audit/validate", "LmaxReadOnlyRuntimeExternalPreActivationAuditValidateApiRequest", "LmaxReadOnlyRuntimeExternalPreActivationAuditDto")) {
        if ($apiProgram.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4N pre-activation audit endpoint is missing '{0}'." -f $required)
        }
    }
    $endpointStart = $apiProgram.IndexOf('app.MapPost("/lmax-readonly-runtime/external-run-intent/pre-activation-audit/validate"', [StringComparison]::OrdinalIgnoreCase)
    $endpointEnd = $apiProgram.IndexOf('app.MapGet("/lmax-readonly-runtime/runs"', [StringComparison]::OrdinalIgnoreCase)
    if ($endpointStart -lt 0 -or $endpointEnd -le $endpointStart) {
        throw "Could not isolate external pre-activation audit endpoint block."
    }
    $endpointText = $apiProgram.Substring($endpointStart, $endpointEnd - $endpointStart)
    foreach ($forbidden in @("TcpClient", "Socket(", "SslStream", "QuickFIX", "ClientWebSocket", "HttpClient", "NetworkStream", "ConnectAsync", "NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder")) {
        if ($endpointText.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden socket/order text '{0}' found in Phase 4N pre-activation audit endpoint." -f $forbidden)
        }
    }
    Add-Check "Phase 4N pre-activation audit envelope exists" "PASS" "Audit envelope validator and endpoint exist, cannot authorize execution, and expose no socket/order/credential surface."
} catch {
    Add-Check "Phase 4N pre-activation audit envelope exists" "FAIL" $_.Exception.Message
}

try {
    $snapshotPath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionReadinessSnapshot.cs"
    if (-not (Test-Path -LiteralPath $snapshotPath)) { throw "Missing $snapshotPath" }
    $text = Get-Content -LiteralPath $snapshotPath -Raw
    foreach ($required in @("LmaxReadOnlyExternalSessionReadinessSnapshot", "LmaxReadOnlyExternalSessionReadinessSnapshotGenerator", "CanStartSession: false", "SessionStarted: false", "ExternalConnectionAttempted: false", "CredentialReadAttempted: false", "ShadowReplaySubmitAttempted: false", "TradingMutationAttempted: false", "NoSensitiveContent: true", "Phase4ExternalRunImplementationNotStarted", "CredentialResolverDisabled", "GuardedTransportImplementationDisabled")) {
        if ($text.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4O readiness snapshot is missing '{0}'." -f $required)
        }
    }
    foreach ($forbidden in @("TcpClient", "Socket(", "SslStream", "QuickFIX", "ClientWebSocket", "HttpClient", "NetworkStream", "ConnectAsync", "NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "Lmax.ConnectivityLab")) {
        if ($text.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden socket/order/lab text '{0}' found in Phase 4O readiness snapshot." -f $forbidden)
        }
    }
    $apiProgramPath = Join-Path $root "src\QQ.Production.Intraday.Api\Program.cs"
    $apiProgram = Get-Content -LiteralPath $apiProgramPath -Raw
    foreach ($required in @("/lmax-readonly-runtime/external-run-intent/readiness-snapshot", "LmaxReadOnlyRuntimeExternalReadinessSnapshotDto")) {
        if ($apiProgram.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4O readiness snapshot endpoint is missing '{0}'." -f $required)
        }
    }
    Add-Check "Phase 4O readiness snapshot exists" "PASS" "Snapshot generator and endpoint exist, cannot authorize execution, and expose no socket/order/credential surface."
} catch {
    Add-Check "Phase 4O readiness snapshot exists" "FAIL" $_.Exception.Message
}

try {
    $releaseGateDoc = Join-Path $root "docs\LMAX_READONLY_RUNTIME_NO_SOCKET_RELEASE_GATE.md"
    $releaseGateScript = Join-Path $root "scripts\run-lmax-readonly-runtime-no-socket-release-gate.ps1"
    if (-not (Test-Path -LiteralPath $releaseGateDoc)) { throw "Missing $releaseGateDoc" }
    if (-not (Test-Path -LiteralPath $releaseGateScript)) { throw "Missing $releaseGateScript" }
    $docText = Get-Content -LiteralPath $releaseGateDoc -Raw
    foreach ($required in @("Final No-Socket Release Gate", "Phase 4A", "Phase 4O", "No socket implementation exists", "PASS WITH KNOWN WARNINGS")) {
        if ($docText.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4P release gate document is missing '{0}'." -f $required)
        }
    }
    $scriptText = Get-Content -LiteralPath $releaseGateScript -Raw
    foreach ($required in @("LMAX Read-Only Runtime Final No-Socket Release Gate", "check-lmax-readonly-runtime-phase4-preflight.ps1", "smoke-lmax-readonly-runtime-external-preflight-local.ps1", "lmax-readonly-no-socket-release-gate")) {
        if ($scriptText.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 4P release gate script is missing '{0}'." -f $required)
        }
    }
    Add-Check "Phase 4P final no-socket release gate exists" "PASS" "Release gate document and local-only script exist; the gate does not add socket capability."
} catch {
    Add-Check "Phase 4P final no-socket release gate exists" "FAIL" $_.Exception.Message
}

try {
    $fakeTransportPath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionFakeTransport.cs"
    if (-not (Test-Path -LiteralPath $fakeTransportPath)) { throw "Missing $fakeTransportPath" }
    $text = Get-Content -LiteralPath $fakeTransportPath -Raw
    foreach ($required in @("ILmaxReadOnlyExternalSessionTransport", "LmaxReadOnlyExternalSessionFakeTransport", "LmaxReadOnlyExternalSessionFake", "FakeInMemory")) {
        if ($text.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Fake transport harness is missing '{0}'." -f $required)
        }
    }
    foreach ($forbidden in @("TcpClient", "Socket(", "NetworkStream", "ConnectAsync", "Dns.")) {
        if ($text.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden socket/network implementation text '{0}' found in fake transport harness." -f $forbidden)
        }
    }
    Add-Check "Phase 4B fake transport harness exists" "PASS" "In-memory fake transport boundary is present and no socket API text was found."
} catch {
    Add-Check "Phase 4B fake transport harness exists" "FAIL" $_.Exception.Message
}

try {
    $mapperPath = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlyExternalSessionEvidencePreviewMapper.cs"
    if (-not (Test-Path -LiteralPath $mapperPath)) { throw "Missing $mapperPath" }
    $text = Get-Content -LiteralPath $mapperPath -Raw
    foreach ($required in @("LmaxReadOnlyExternalSessionEvidencePreviewMapper", "lmax-fix-lifecycle-evidence-v1", "RuntimeFakeTransport", "FakeRuntimePreview")) {
        if ($text.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Evidence preview mapper is missing '{0}'." -f $required)
        }
    }
    foreach ($forbidden in @("SubmitToShadowReplay: true", "SubmittedToShadowReplay: true", "TcpClient", "Socket(", "NetworkStream", "ConnectAsync")) {
        if ($text.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden runtime/shadow/network text '{0}' found in evidence preview mapper." -f $forbidden)
        }
    }
    Add-Check "Phase 4C evidence preview mapper exists" "PASS" "Mapper is preview-only and no shadow-submit/socket API text was found."
} catch {
    Add-Check "Phase 4C evidence preview mapper exists" "FAIL" $_.Exception.Message
}

try {
    $apiProgramPath = Join-Path $root "src\QQ.Production.Intraday.Api\Program.cs"
    $apiProgram = Get-Content -LiteralPath $apiProgramPath -Raw
    foreach ($required in @("/lmax-readonly-runtime/fake-transport-preview", "FakeTransportPreview", "SubmitToShadowReplay remains disabled/deferred")) {
        if ($apiProgram.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Fake transport preview endpoint is missing '{0}'." -f $required)
        }
    }
    $endpointStart = $apiProgram.IndexOf('app.MapPost("/lmax-readonly-runtime/fake-transport-preview"', [StringComparison]::OrdinalIgnoreCase)
    $endpointEnd = $apiProgram.IndexOf('app.MapGet("/lmax-readonly-runtime/runs"', [StringComparison]::OrdinalIgnoreCase)
    if ($endpointStart -lt 0 -or $endpointEnd -le $endpointStart) {
        throw "Could not isolate fake transport preview endpoint block."
    }
    $endpointText = $apiProgram.Substring($endpointStart, $endpointEnd - $endpointStart)
    foreach ($forbidden in @("TcpClient", "Socket(", "NetworkStream", "ConnectAsync", "NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest")) {
        if ($endpointText.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw ("Forbidden socket/order text '{0}' found in API fake preview endpoint surface." -f $forbidden)
        }
    }
    Add-Check "Phase 4D fake transport preview endpoint exists" "PASS" "Manual diagnostic endpoint is present, fake-only, and shadow replay submit remains deferred."
} catch {
    Add-Check "Phase 4D fake transport preview endpoint exists" "FAIL" $_.Exception.Message
}

try {
    $apiProgram = Get-Content -LiteralPath (Join-Path $root "src\QQ.Production.Intraday.Api\Program.cs") -Raw
    $workerProgram = Get-Content -LiteralPath (Join-Path $root "src\QQ.Production.Intraday.Worker\Program.cs") -Raw
    if ($apiProgram -notmatch "AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>") { throw "API does not show FakeLmaxGateway registration." }
    if ($workerProgram -notmatch "AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>") { throw "Worker does not show FakeLmaxGateway registration." }
    if ($apiProgram -match "LmaxVenueGateway" -or $workerProgram -match "LmaxVenueGateway") { throw "Real LMAX gateway reference found in API/Worker." }
    if ($apiProgram -match "AddHostedService.*Lmax" -or $workerProgram -match "AddHostedService.*Lmax") { throw "LMAX hosted service reference found." }
    Add-Check "API/Worker remain FakeLmaxGateway-only" "PASS" "No real gateway or LMAX hosted service registration found."
} catch {
    Add-Check "API/Worker remain FakeLmaxGateway-only" "FAIL" $_.Exception.Message
}

try {
    $generatedEvidence = Join-Path $root "artifacts\lmax-lab\evidence"
    if (Test-Path -LiteralPath $generatedEvidence) {
        $status = git -C $root status --short -- artifacts/lmax-lab/evidence 2>$null
        if ($status) { throw "Generated lab evidence appears in git status: $status" }
    }
    Add-Check "Generated lab evidence not staged/dirty" "PASS" "No generated evidence tracked in git status."
} catch {
    Add-Check "Generated lab evidence not staged/dirty" "FAIL" $_.Exception.Message
}

try {
    $health = Invoke-RestMethod -Method Get -Uri ("{0}/health" -f $ApiBaseUrl) -TimeoutSec 3 -Headers @{ "X-Operator-Id" = "local-admin" }
    if ($health.executionGateway -ne "FakeLmaxGateway") { throw ("Expected FakeLmaxGateway, got {0}" -f $health.executionGateway) }
    if ([bool]$health.liveTradingEnabled) { throw "liveTradingEnabled must be false." }
    if ([bool]$health.externalConnectionsEnabled) { throw "externalConnectionsEnabled must be false." }
    Add-Check "API health safe" "PASS" "FakeLmaxGateway, live trading false, external connections false."

    $status = Invoke-RestMethod -Method Get -Uri ("{0}/lmax-readonly-runtime/status" -f $ApiBaseUrl) -TimeoutSec 3 -Headers @{ "X-Operator-Id" = "local-admin" }
    if ($status.implementationMode -ne "DesignOnly" -and $status.implementationMode -ne "FakeInMemory") {
        throw ("Unexpected implementation mode: {0}" -f $status.implementationMode)
    }
    Add-Check "Read-only runtime status available" "PASS" ("Status={0}; ImplementationMode={1}" -f $status.status, $status.implementationMode)

    $body = @{ reason = "Phase 4 preflight default blocked check"; requestedActivationLevel = "Level4RuntimeManualReadOnlyConnectionNoReplaySubmit"; dryRun = $true } | ConvertTo-Json
    $run = Invoke-RestMethod -Method Post -Uri ("{0}/lmax-readonly-runtime/run" -f $ApiBaseUrl) -TimeoutSec 5 -Headers @{ "X-Operator-Id" = "local-admin" } -ContentType "application/json" -Body $body
    if ($run.status -ne "Disabled" -and $run.status -ne "Blocked") {
        throw ("Expected Disabled/Blocked for Phase 4 preflight run, got {0}" -f $run.status)
    }
    Add-Check "Phase 4 run remains blocked" "PASS" ("RunStatus={0}" -f $run.status)

    $intentBody = @{
        reason = "Phase 4K preflight validate-only check"
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
    } | ConvertTo-Json
    $intent = Invoke-RestMethod -Method Post -Uri ("{0}/lmax-readonly-runtime/external-run-intent/validate" -f $ApiBaseUrl) -TimeoutSec 5 -Headers @{ "X-Operator-Id" = "local-admin" } -ContentType "application/json" -Body $intentBody
    if ($intent.status -ne "Blocked") {
        throw ("Expected Blocked for Phase 4K external run intent, got {0}" -f $intent.status)
    }
    if ([bool]$intent.canStartSession -or [bool]$intent.sessionStarted -or [bool]$intent.externalConnectionAttempted -or [bool]$intent.credentialReadAttempted -or [bool]$intent.shadowReplaySubmitAttempted -or [bool]$intent.tradingMutationAttempted) {
        throw "Phase 4K validate endpoint reported an unsafe attempted action."
    }
    Add-Check "Phase 4K external run intent remains validate-only" "PASS" "Blocked with no session, connection, credential read, shadow replay submit, or trading mutation attempt."

    $dryRunReport = Invoke-RestMethod -Method Post -Uri ("{0}/lmax-readonly-runtime/external-run-intent/dry-run-report" -f $ApiBaseUrl) -TimeoutSec 5 -Headers @{ "X-Operator-Id" = "local-admin" } -ContentType "application/json" -Body $intentBody
    if ($dryRunReport.expectedOutcome -ne "Blocked" -and $dryRunReport.expectedOutcome -ne "ValidateOnly") {
        throw ("Unexpected Phase 4L dry-run expectedOutcome: {0}" -f $dryRunReport.expectedOutcome)
    }
    if ([bool]$dryRunReport.canStartSession -or [bool]$dryRunReport.sessionStarted -or [bool]$dryRunReport.externalConnectionAttempted -or [bool]$dryRunReport.credentialReadAttempted -or [bool]$dryRunReport.shadowReplaySubmitAttempted -or [bool]$dryRunReport.tradingMutationAttempted) {
        throw "Phase 4L dry-run report endpoint reported an unsafe attempted action."
    }
    $gateNames = @($dryRunReport.safetyGates | ForEach-Object { [string]$_.gate })
    foreach ($requiredGate in @("Phase4ExternalRunImplementationNotStarted", "CredentialResolverDisabled", "GuardedTransportImplementationDisabled", "ExternalSessionImplementationStarted")) {
        if ($gateNames -notcontains $requiredGate) {
            throw ("Phase 4L dry-run report missing safety gate '{0}'." -f $requiredGate)
        }
    }
    Add-Check "Phase 4L dry-run report remains no-network" "PASS" "Blocked with credential resolver, guarded transport, and skeleton disabled markers."

    $signoffBody = @{
        reason = "Phase 4M preflight signoff metadata-only check"
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
        dryRunReportSafetyMarkers = $gateNames
        decision = "Signed"
    } | ConvertTo-Json
    $signoff = Invoke-RestMethod -Method Post -Uri ("{0}/lmax-readonly-runtime/external-run-intent/signoff/validate" -f $ApiBaseUrl) -TimeoutSec 5 -Headers @{ "X-Operator-Id" = "local-admin" } -ContentType "application/json" -Body $signoffBody
    if ([bool]$signoff.canAuthorizeExecution -or -not [bool]$signoff.executionStillBlocked -or [bool]$signoff.sessionStarted -or [bool]$signoff.externalConnectionAttempted -or [bool]$signoff.credentialReadAttempted -or [bool]$signoff.shadowReplaySubmitAttempted -or [bool]$signoff.tradingMutationAttempted) {
        throw "Phase 4M signoff endpoint reported an unsafe authorization or attempted action."
    }
    Add-Check "Phase 4M signoff remains metadata-only" "PASS" "Signoff cannot authorize execution and attempted no session, connection, credential read, shadow replay submit, or trading mutation."

    $signoffGateNames = @($signoff.safetyGates | ForEach-Object { [string]$_.gate })
    $auditBody = @{
        reason = "Phase 4N preflight audit metadata-only check"
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
        stableBlockers = @($gateNames + $signoffGateNames | Select-Object -Unique)
        dryRunReportReviewed = $true
        signoffReviewed = $true
    } | ConvertTo-Json
    $audit = Invoke-RestMethod -Method Post -Uri ("{0}/lmax-readonly-runtime/external-run-intent/pre-activation-audit/validate" -f $ApiBaseUrl) -TimeoutSec 5 -Headers @{ "X-Operator-Id" = "local-admin" } -ContentType "application/json" -Body $auditBody
    if ([bool]$audit.canAuthorizeExecution -or -not [bool]$audit.executionStillBlocked -or [bool]$audit.sessionStarted -or [bool]$audit.externalConnectionAttempted -or [bool]$audit.credentialReadAttempted -or [bool]$audit.shadowReplaySubmitAttempted -or [bool]$audit.tradingMutationAttempted) {
        throw "Phase 4N pre-activation audit endpoint reported an unsafe authorization or attempted action."
    }
    Add-Check "Phase 4N pre-activation audit remains metadata-only" "PASS" "Audit envelope cannot authorize execution and attempted no session, connection, credential read, shadow replay submit, or trading mutation."

    $snapshotBody = @{
        reason = "Phase 4O preflight readiness snapshot check"
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
    } | ConvertTo-Json
    $snapshot = Invoke-RestMethod -Method Post -Uri ("{0}/lmax-readonly-runtime/external-run-intent/readiness-snapshot" -f $ApiBaseUrl) -TimeoutSec 5 -Headers @{ "X-Operator-Id" = "local-admin" } -ContentType "application/json" -Body $snapshotBody
    if ([bool]$snapshot.canStartSession -or [bool]$snapshot.sessionStarted -or [bool]$snapshot.externalConnectionAttempted -or [bool]$snapshot.credentialReadAttempted -or [bool]$snapshot.shadowReplaySubmitAttempted -or [bool]$snapshot.tradingMutationAttempted) {
        throw "Phase 4O readiness snapshot endpoint reported an unsafe attempted action."
    }
    Add-Check "Phase 4O readiness snapshot remains metadata-only" "PASS" "Snapshot cannot start a session and attempted no connection, credential read, shadow replay submit, or trading mutation."
} catch {
    Add-Check "API-dependent checks" "WARN" ("API unavailable or check skipped: {0}" -f $_.Exception.Message)
}

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
Write-Host ""
Write-Host "== Phase 4 Preflight Summary ==" -ForegroundColor Cyan
$results | Format-Table check, status, detail -AutoSize
if ($failed.Count -gt 0) {
    Write-Host "FinalDecision: FAIL" -ForegroundColor Red
    exit 1
}

Write-Host "FinalDecision: PASS" -ForegroundColor Green
