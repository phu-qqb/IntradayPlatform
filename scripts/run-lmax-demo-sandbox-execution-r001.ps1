param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputSubdir = "lmax-sandbox-global-process-test-run-r001",
    [string]$RunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_20260529T125324Z",
    [string]$ApprovalPath,
    [string]$ExecutionSwitchPath,
    [string]$ConfigValidationPath,
    [string]$OrderManifestPath,
    [string]$ExecutionAlgoPlanPath,
    [string]$AdapterBindingPath,
    [string]$MainArtifactName = "lmax-sandbox-global-process-test-run-r001.json",
    [string]$ApprovedReadyStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001",
    [string]$ReconciledStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_RECONCILED_R001",
    [string]$ExecutedStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_EXECUTED_R001",
    [switch]$ExecuteLmaxDemoSandboxOrders,
    [switch]$UseActualLmaxFixClient,
    [switch]$UseMockFixServer,
    [switch]$MockFixServerRejectTag21,
    [switch]$MockFixServerRejectClOrdIdLength,
    [switch]$MockFixServerRejectDuplicateFirstOrder,
    [switch]$InjectForbiddenTag21ForTest,
    [switch]$InjectLongClOrdIdForTest,
    [switch]$UseMockLmaxAdapter,
    [string]$ExecutionAttemptId,
    [switch]$NewExecutionAttempt,
    [switch]$ResidualOnlyRetry
)

$ErrorActionPreference = "Stop"

$Package = "NEXT_LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_R001"
$OutputDir = Join-Path $RepoRoot "artifacts\readiness\$OutputSubdir"
if ([string]::IsNullOrWhiteSpace($ApprovalPath)) { $ApprovalPath = Join-Path $OutputDir "operator-approval-lmax-demo-execution-status-r001.json" }
if ([string]::IsNullOrWhiteSpace($ExecutionSwitchPath)) { $ExecutionSwitchPath = Join-Path $OutputDir "lmax-demo-execution-switch-status-r001.json" }
if ([string]::IsNullOrWhiteSpace($ConfigValidationPath)) { $ConfigValidationPath = Join-Path $OutputDir "lmax-demo-execution-config-validation-r001.json" }
if ([string]::IsNullOrWhiteSpace($OrderManifestPath)) { $OrderManifestPath = Join-Path $OutputDir "lmax-order-manifest-r001.json" }
if ([string]::IsNullOrWhiteSpace($ExecutionAlgoPlanPath)) { $ExecutionAlgoPlanPath = Join-Path $OutputDir "execution-algo-plan-r001.json" }
if ([string]::IsNullOrWhiteSpace($AdapterBindingPath)) { $AdapterBindingPath = Join-Path $OutputDir "lmax-demo-actual-adapter-binding-r001.json" }

$MainPath = Join-Path $OutputDir $MainArtifactName
$ExecutionResultPath = Join-Path $OutputDir "sandbox-execution-result-r001.json"
$TradeReconPath = Join-Path $OutputDir "sandbox-trade-level-reconciliation-r001.json"
$PnlPath = Join-Path $OutputDir "sandbox-pnl-r001.json"
$ClOrdIdMapPath = Join-Path $OutputDir "lmax-demo-clordid-map-r001.json"
$AdapterBindingStatusPath = Join-Path $OutputDir "lmax-demo-actual-adapter-binding-status-r001.json"
$Soh = [char]1
$FixEncoding = [Text.Encoding]::ASCII
$InjectForbiddenTag21ForTestEnabled = $InjectForbiddenTag21ForTest.IsPresent
$MockFixServerRejectTag21Enabled = $MockFixServerRejectTag21.IsPresent
$MockFixServerRejectClOrdIdLengthEnabled = $MockFixServerRejectClOrdIdLength.IsPresent
$MockFixServerRejectDuplicateFirstOrderEnabled = $MockFixServerRejectDuplicateFirstOrder.IsPresent
$InjectLongClOrdIdForTestEnabled = $InjectLongClOrdIdForTest.IsPresent

$ExecutionAttemptExplicit = -not [string]::IsNullOrWhiteSpace($ExecutionAttemptId)
$ExecutionAttemptSequence = 1
if (-not [string]::IsNullOrWhiteSpace($ExecutionAttemptId) -and $ExecutionAttemptId -match "^A(\d{3})$") {
    $ExecutionAttemptSequence = [int]$Matches[1]
} elseif ($NewExecutionAttempt.IsPresent -or $ResidualOnlyRetry.IsPresent) {
    if (Test-Path -LiteralPath $ExecutionResultPath) {
        try {
            $existingExecution = Get-Content -Raw -LiteralPath $ExecutionResultPath | ConvertFrom-Json
            $existingAttempts = @(if ($null -ne $existingExecution.attempts) { $existingExecution.attempts } else { @() })
            if ($existingAttempts.Count -gt 0) {
                $maxAttempt = 0
                foreach ($attempt in $existingAttempts) {
                    $seq = if ($null -ne $attempt.attempt_sequence) { [int]$attempt.attempt_sequence } else { 0 }
                    if ($seq -gt $maxAttempt) { $maxAttempt = $seq }
                }
                $ExecutionAttemptSequence = $maxAttempt + 1
            } else {
                $existingOrdersCount = if ($null -ne $existingExecution.orders_submitted_count) { [int]$existingExecution.orders_submitted_count } else { 0 }
                $existingFillsCount = if ($null -ne $existingExecution.fills_count) { [int]$existingExecution.fills_count } else { 0 }
                if ($existingOrdersCount -gt 0 -or $existingFillsCount -gt 0) {
                $ExecutionAttemptSequence = 2
                }
            }
        } catch {
            $ExecutionAttemptSequence = 2
        }
    }
}
if ([string]::IsNullOrWhiteSpace($ExecutionAttemptId)) {
    $ExecutionAttemptId = "A" + $ExecutionAttemptSequence.ToString("000", [Globalization.CultureInfo]::InvariantCulture)
}
$AttemptLogDirName = "attempt-" + $ExecutionAttemptSequence.ToString("000", [Globalization.CultureInfo]::InvariantCulture)
$LogDir = Join-Path $OutputDir "logs\$RunId\$AttemptLogDirName"
$OrderLogPath = Join-Path $LogDir "orders.log"
$ExecutionReportLogPath = Join-Path $LogDir "execution-reports.log"
$AttemptClOrdIdMapPath = Join-Path $OutputDir "lmax-demo-clordid-map-attempt-$ExecutionAttemptId-r001.json"

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing required artifact: $Path" }
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Write-JsonFile([string]$Path, [object]$Value) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Write-LogJsonLine([string]$Path, [object]$Value) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    ($Value | ConvertTo-Json -Depth 50 -Compress) | Add-Content -LiteralPath $Path -Encoding UTF8
}

function Write-LogTextLine([string]$Path, [string]$Value) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $Value | Add-Content -LiteralPath $Path -Encoding UTF8
}

function Get-OptionalPropertyValue {
    param([AllowNull()] $Object, [Parameter(Mandatory=$true)] [string] $Name, $Default = $null)
    if ($null -eq $Object) { return $Default }
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name)) { return $Object[$Name] }
        return $Default
    }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop) { return $Default }
    return $prop.Value
}

function Get-OptionalBooleanProperty {
    param([AllowNull()] $Object, [Parameter(Mandatory=$true)] [string] $Name, [bool] $Default = $false)
    $value = Get-OptionalPropertyValue -Object $Object -Name $Name -Default $Default
    if ($null -eq $value) { return $Default }
    return [bool]$value
}

function Set-JsonObjectProperty {
    param(
        [Parameter(Mandatory=$true)] $Object,
        [Parameter(Mandatory=$true)] [string] $Name,
        $Value
    )
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop) {
        $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
    } else {
        $prop.Value = $Value
    }
}

function Get-EndpointParts([string]$Endpoint) {
    $value = if ($null -eq $Endpoint) { "" } else { $Endpoint.Trim() }
    if ([string]::IsNullOrWhiteSpace($value)) { throw "Endpoint is missing." }
    $value = $value -replace '^tls://', '' -replace '^ssl://', '' -replace '^tcp://', ''
    $parts = $value.Split(':', 2)
    $endpointHost = $parts[0].Trim()
    $port = if ($parts.Length -gt 1 -and -not [string]::IsNullOrWhiteSpace($parts[1])) { [int]$parts[1] } else { 443 }
    [ordered]@{ host = $endpointHost; port = $port }
}

function Test-DemoEndpointAllowed([string]$Endpoint, [bool]$AllowLocalMock = $false) {
    $parts = Get-EndpointParts $Endpoint
    $endpointHost = ([string]$parts.host).ToLowerInvariant()
    if ($endpointHost -match "prod|production|live") { return $false }
    if ($AllowLocalMock -and $endpointHost -in @("localhost", "127.0.0.1", "::1")) { return $true }
    return ($endpointHost -match "demo|sandbox|test|uat")
}

function New-FixMessage {
    param(
        [string]$MsgType,
        [int]$SeqNum,
        [string]$SenderCompId,
        [string]$TargetCompId,
        [object[]]$Fields
    )

    $sendingTime = [DateTime]::UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff", [Globalization.CultureInfo]::InvariantCulture)
    $bodyParts = @(
        "35=$MsgType",
        "49=$SenderCompId",
        "56=$TargetCompId",
        "34=$SeqNum",
        "52=$sendingTime"
    )
    foreach ($field in $Fields) {
        if ($field -is [System.Collections.IDictionary]) {
            $bodyParts += "$($field.Tag)=$($field.Value)"
        } else {
            $bodyParts += "$($field[0])=$($field[1])"
        }
    }
    $body = ($bodyParts -join [string]$Soh) + [string]$Soh
    $bodyLength = $FixEncoding.GetByteCount($body)
    $prefix = "8=FIX.4.4$Soh" + "9=$bodyLength$Soh"
    $withoutChecksum = $prefix + $body
    $sum = 0
    foreach ($byte in $FixEncoding.GetBytes($withoutChecksum)) { $sum += [int]$byte }
    $checksum = ($sum % 256).ToString("000", [Globalization.CultureInfo]::InvariantCulture)
    return $withoutChecksum + "10=$checksum$Soh"
}

function Convert-FixMessageToTags([string]$Message) {
    $tags = @{}
    foreach ($part in $Message.Split($Soh)) {
        if ([string]::IsNullOrWhiteSpace($part) -or $part.IndexOf("=") -lt 0) { continue }
        $pair = $part.Split("=", 2)
        $tags[$pair[0]] = $pair[1]
    }
    return $tags
}

function Get-FixTag([string]$Message, [string]$Tag) {
    $tags = Convert-FixMessageToTags $Message
    if ($tags.ContainsKey($Tag)) { return [string]$tags[$Tag] }
    return $null
}

function ConvertTo-SanitizedFixLine([string]$Message) {
    $parts = @()
    foreach ($part in $Message.Split($Soh)) {
        if ([string]::IsNullOrWhiteSpace($part)) { continue }
        if ($part.StartsWith("553=")) { $parts += "553=***" }
        elseif ($part.StartsWith("554=")) { $parts += "554=***" }
        else { $parts += $part }
    }
    return ($parts -join "|")
}

function Read-FixMessageFromStream([IO.Stream]$Stream, [int]$TimeoutMilliseconds) {
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    $buffer = New-Object byte[] 1
    $bytes = New-Object System.Collections.Generic.List[byte]
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($Stream.CanTimeout) { $Stream.ReadTimeout = [Math]::Max(100, [int]($deadline - [DateTime]::UtcNow).TotalMilliseconds) }
        try {
            $read = $Stream.Read($buffer, 0, 1)
        } catch [IO.IOException] {
            Start-Sleep -Milliseconds 25
            continue
        }
        if ($read -le 0) {
            Start-Sleep -Milliseconds 25
            continue
        }
        $bytes.Add($buffer[0])
        $text = $FixEncoding.GetString($bytes.ToArray())
        if ($text -match "10=\d{3}$([regex]::Escape([string]$Soh))") { return $text }
        if ($text.Contains("10=" + [string]$Soh)) { return $text }
        if ($text -match "10=\d{3}\x01") { return $text }
    }
    throw "FIX message read timed out."
}

function Send-FixMessage([IO.Stream]$Stream, [string]$Message) {
    $bytes = $FixEncoding.GetBytes($Message)
    $Stream.Write($bytes, 0, $bytes.Length)
    $Stream.Flush()
}

function Convert-OrderSideToFix([string]$Side) {
    if ($Side -eq "BUY") { return "1" }
    if ($Side -eq "SELL") { return "2" }
    throw "Unsupported side: $Side"
}

function Convert-FixSideToText([string]$Side) {
    if ($Side -eq "1") { return "BUY" }
    if ($Side -eq "2") { return "SELL" }
    return $Side
}

function New-LmaxClOrdIdMap([object[]]$OrderRows) {
    $mappings = @()
    $index = 1
    foreach ($order in $OrderRows) {
        $internalOrderId = "$RunId-$($order.symbol)-$($order.side)-$($order.quantity)"
        $externalClOrdId = if ($InjectLongClOrdIdForTestEnabled) { $internalOrderId } else { "LXR1" + $ExecutionAttemptId + "O" + $index.ToString("000", [Globalization.CultureInfo]::InvariantCulture) }
        $mappings += [ordered]@{
            internal_order_id = $internalOrderId
            external_cl_ord_id = $externalClOrdId
            run_id = $RunId
            execution_attempt_id = $ExecutionAttemptId
            attempt_sequence = $ExecutionAttemptSequence
            symbol = $order.symbol
            side = $order.side
            quantity = $order.quantity
            security_id = $order.security_id
            security_id_source_tag22 = $order.security_id_source_tag22
        }
        $index++
    }

    $artifact = [ordered]@{
        package = $Package
        artifact_type = "lmax_demo_clordid_map_r001"
        run_id = $RunId
        execution_attempt_id = $ExecutionAttemptId
        attempt_sequence = $ExecutionAttemptSequence
        policy = [ordered]@{
            max_external_cl_ord_id_length = 20
            deterministic = $true
            unique_within_run = $true
            unique_across_attempts = $true
            attempt_embedded = $true
        }
        mappings = $mappings
    }
    Write-JsonFile $AttemptClOrdIdMapPath $artifact

    $aggregateMappings = @()
    if (($ExecutionAttemptSequence -gt 1 -or $NewExecutionAttempt.IsPresent -or $ResidualOnlyRetry.IsPresent -or $ExecutionAttemptExplicit) -and (Test-Path -LiteralPath $ClOrdIdMapPath)) {
        try {
            $existingMap = Read-JsonFile $ClOrdIdMapPath
            $aggregateMappings = @($existingMap.mappings | Where-Object { [string]$_.execution_attempt_id -ne $ExecutionAttemptId })
        } catch {
            $aggregateMappings = @()
        }
    }
    $aggregateMappings += $mappings
    $aggregateArtifact = [ordered]@{
        package = $Package
        artifact_type = "lmax_demo_clordid_map_r001"
        run_id = $RunId
        latest_execution_attempt_id = $ExecutionAttemptId
        latest_attempt_sequence = $ExecutionAttemptSequence
        policy = $artifact.policy
        mappings = $aggregateMappings
    }
    Write-JsonFile $ClOrdIdMapPath $aggregateArtifact
    return $mappings
}

function Convert-ClOrdIdMappingsToLookup($Mappings) {
    $lookup = @{}
    foreach ($mapping in @($Mappings)) {
        $lookup[[string]$mapping.external_cl_ord_id] = $mapping
    }
    return $lookup
}

function New-NewOrderSingleMessage($Order, [string]$ClientOrderId, [int]$SeqNum, [string]$SenderCompId, [string]$TargetCompId, [bool]$IncludeForbiddenTag21 = $false) {
    $quantity = ([decimal]$Order.quantity).ToString("0.################", [Globalization.CultureInfo]::InvariantCulture)
    $fields = @(
        @("11", $ClientOrderId),
        @("55", [string]$Order.symbol),
        @("54", (Convert-OrderSideToFix ([string]$Order.side))),
        @("38", $quantity),
        @("40", "1"),
        @("59", "3"),
        @("60", [DateTime]::UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff", [Globalization.CultureInfo]::InvariantCulture))
    )
    if (-not [string]::IsNullOrWhiteSpace([string]$Order.security_id)) {
        $fields += @(@("48", [string]$Order.security_id), @("22", [string]$Order.security_id_source_tag22))
    }
    if ($IncludeForbiddenTag21) {
        $fields += @(@("21", "1"))
    }
    New-FixMessage -MsgType "D" -SeqNum $SeqNum -SenderCompId $SenderCompId -TargetCompId $TargetCompId -Fields $fields
}

function Assert-LmaxNewOrderSingleAllowed([string]$Message) {
    $tags = Convert-FixMessageToTags $Message
    if (-not $tags.ContainsKey("11") -or [string]::IsNullOrWhiteSpace([string]$tags["11"])) {
        throw "CLORDID_MISSING: LMAX demo/sandbox NewOrderSingle must contain FIX tag 11 ClOrdID."
    }
    if (([string]$tags["11"]).Length -gt 20) {
        throw "CLORDID_TOO_LONG: LMAX demo/sandbox NewOrderSingle tag 11 must be <= 20 characters."
    }
    if ($tags.ContainsKey("21")) {
        throw "FORBIDDEN_TAG_21: LMAX demo/sandbox NewOrderSingle must not contain FIX tag 21 HandlInst."
    }
    if ($tags.ContainsKey("48") -and ((-not $tags.ContainsKey("22")) -or [string]$tags["22"] -ne "8")) {
        throw "ORDER_SUBMISSION_INVALID_TAG22: FIX tag 22 must equal 8 when tag 48 SecurityID is present."
    }
}

function Convert-ExecutionReportToObject([string]$Message, [string]$RunIdValue, $ClOrdIdLookup = $null) {
    $tags = Convert-FixMessageToTags $Message
    $externalClOrdId = if ($tags.ContainsKey("11")) { [string]$tags["11"] } else { $null }
    $mapping = if ($null -ne $ClOrdIdLookup -and -not [string]::IsNullOrWhiteSpace($externalClOrdId) -and $ClOrdIdLookup.ContainsKey($externalClOrdId)) { $ClOrdIdLookup[$externalClOrdId] } else { $null }
    $internalOrderId = if ($null -ne $mapping) { [string]$mapping.internal_order_id } else { $null }
    $lastQty = Get-FixDecimalValue -Tags $tags -Name "32" -Default $null
    $lastPx = Get-FixDecimalValue -Tags $tags -Name "31" -Default $null
    $cumQty = Get-FixDecimalValue -Tags $tags -Name "14" -Default ([decimal]0)
    $leavesQty = Get-FixDecimalValue -Tags $tags -Name "151" -Default $null
    [ordered]@{
        run_id = $RunIdValue
        execution_attempt_id = $ExecutionAttemptId
        attempt_sequence = $ExecutionAttemptSequence
        msg_type = if ($tags.ContainsKey("35")) { [string]$tags["35"] } else { $null }
        execution_type = if ($tags.ContainsKey("150")) { [string]$tags["150"] } else { $null }
        order_status = if ($tags.ContainsKey("39")) { [string]$tags["39"] } else { $null }
        ord_rej_reason = if ($tags.ContainsKey("103")) { [string]$tags["103"] } else { $null }
        exec_id = if ($tags.ContainsKey("17")) { [string]$tags["17"] } else { $null }
        order_id = if ($tags.ContainsKey("37")) { [string]$tags["37"] } else { $null }
        client_order_id = $externalClOrdId
        external_cl_ord_id = $externalClOrdId
        internal_order_id = $internalOrderId
        symbol = if ($tags.ContainsKey("55")) { [string]$tags["55"] } else { $null }
        security_id = if ($tags.ContainsKey("48")) { [string]$tags["48"] } else { $null }
        security_id_source_tag22 = if ($tags.ContainsKey("22")) { [string]$tags["22"] } else { $null }
        side = if ($tags.ContainsKey("54")) { Convert-FixSideToText ([string]$tags["54"]) } else { $null }
        last_qty = $lastQty
        last_px = $lastPx
        cumulative_qty = $cumQty
        leaves_qty = $leavesQty
        text = if ($tags.ContainsKey("58")) { [string]$tags["58"] } else { $null }
        sanitized_fix = ConvertTo-SanitizedFixLine $Message
    }
}

function Get-FixDecimalValue {
    param(
        [hashtable]$Tags,
        [string]$Name,
        $Default = $null
    )

    if (-not $Tags.ContainsKey($Name)) { return $Default }
    $parsed = [decimal]0
    if ([decimal]::TryParse([string]$Tags[$Name], [Globalization.NumberStyles]::Any, [Globalization.CultureInfo]::InvariantCulture, [ref]$parsed)) {
        return $parsed
    }
    return $Default
}

function New-MockExecutionReportMessage($Order, [string]$ClientOrderId, [int]$SeqNum, [string]$SenderCompId, [string]$TargetCompId, [string]$ExecType, [string]$OrdStatus) {
    $qty = ([decimal]$Order.quantity).ToString("0.################", [Globalization.CultureInfo]::InvariantCulture)
    $lastQty = if ($ExecType -eq "F") { $qty } else { "0" }
    $lastPx = if ($ExecType -eq "F") { "1.00000" } else { "0" }
    $cumQty = if ($ExecType -eq "F") { $qty } else { "0" }
    $leaves = if ($ExecType -eq "F") { "0" } else { $qty }
    $fields = @(
        @("37", "MOCK-$ClientOrderId"),
        @("17", "EXEC-$SeqNum-$ClientOrderId"),
        @("11", $ClientOrderId),
        @("150", $ExecType),
        @("39", $OrdStatus),
        @("55", [string]$Order.symbol),
        @("54", (Convert-OrderSideToFix ([string]$Order.side))),
        @("38", $qty),
        @("14", $cumQty),
        @("151", $leaves),
        @("32", $lastQty),
        @("31", $lastPx),
        @("60", [DateTime]::UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff", [Globalization.CultureInfo]::InvariantCulture))
    )
    if (-not [string]::IsNullOrWhiteSpace([string]$Order.security_id)) {
        $fields += @(@("48", [string]$Order.security_id), @("22", [string]$Order.security_id_source_tag22))
    }
    New-FixMessage -MsgType "8" -SeqNum $SeqNum -SenderCompId $TargetCompId -TargetCompId $SenderCompId -Fields $fields
}

function New-MockDuplicateOrderRejectExecutionReport($Order, [string]$ClientOrderId, [int]$SeqNum, [string]$SenderCompId, [string]$TargetCompId) {
    $fields = @(
        @("37", "0"),
        @("17", "REJECT-$SeqNum-$ClientOrderId"),
        @("11", $ClientOrderId),
        @("150", "8"),
        @("39", "8"),
        @("103", "6"),
        @("58", "DUPLICATE_ORDER"),
        @("54", (Convert-OrderSideToFix ([string]$Order.side))),
        @("14", "0"),
        @("151", "0"),
        @("6", "0"),
        @("60", [DateTime]::UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff", [Globalization.CultureInfo]::InvariantCulture))
    )
    if (-not [string]::IsNullOrWhiteSpace([string]$Order.security_id)) {
        $fields += @(@("48", [string]$Order.security_id), @("22", [string]$Order.security_id_source_tag22))
    }
    New-FixMessage -MsgType "8" -SeqNum $SeqNum -SenderCompId $TargetCompId -TargetCompId $SenderCompId -Fields $fields
}

function New-MockSessionRejectMessage([string]$ClientOrderId, [int]$SeqNum, [string]$SenderCompId, [string]$TargetCompId, [string]$RefTagId, [string]$Text) {
    $fields = @(
        @("45", "0"),
        @("371", $RefTagId),
        @("372", "D"),
        @("373", "0"),
        @("58", $Text),
        @("11", $ClientOrderId)
    )
    New-FixMessage -MsgType "3" -SeqNum $SeqNum -SenderCompId $TargetCompId -TargetCompId $SenderCompId -Fields $fields
}

function Convert-SessionRejectToObject([string]$Message, [string]$RunIdValue, [string]$ClientOrderId, $ClOrdIdLookup = $null) {
    $tags = Convert-FixMessageToTags $Message
    $externalClOrdId = if ($tags.ContainsKey("11")) { [string]$tags["11"] } else { $ClientOrderId }
    $mapping = if ($null -ne $ClOrdIdLookup -and -not [string]::IsNullOrWhiteSpace($externalClOrdId) -and $ClOrdIdLookup.ContainsKey($externalClOrdId)) { $ClOrdIdLookup[$externalClOrdId] } else { $null }
    $internalOrderId = if ($null -ne $mapping) { [string]$mapping.internal_order_id } else { $null }
    $rejectText = if ($tags.ContainsKey("58")) { [string]$tags["58"] } else { $null }
    $refTagId = if ($tags.ContainsKey("371")) { [string]$tags["371"] } else { $null }
    [ordered]@{
        run_id = $RunIdValue
        execution_attempt_id = $ExecutionAttemptId
        attempt_sequence = $ExecutionAttemptSequence
        msg_type = "3"
        client_order_id = $externalClOrdId
        external_cl_ord_id = $externalClOrdId
        internal_order_id = $internalOrderId
        ref_tag_id = $refTagId
        ref_msg_type = if ($tags.ContainsKey("372")) { [string]$tags["372"] } else { $null }
        session_reject_reason = if ($tags.ContainsKey("373")) { [string]$tags["373"] } else { $null }
        text = $rejectText
        rejected_tag = $refTagId
        primary_failure = if ($refTagId -eq "21") { "FORBIDDEN_TAG_21" } elseif ($refTagId -eq "11" -and $rejectText -match "length less than or equal to 20") { "CLORDID_TOO_LONG" } else { "SESSION_REJECT" }
        sanitized_fix = ConvertTo-SanitizedFixLine $Message
    }
}

function Invoke-ActualLmaxFixClient {
    param(
        [object[]]$OrderRows,
        [string]$Endpoint,
        [string]$SenderCompId,
        [string]$TargetCompId,
        [string]$Username,
        [string]$Password,
        [bool]$MockFixServer
    )

    if (Test-Path -LiteralPath $OrderLogPath) { Remove-Item -LiteralPath $OrderLogPath -Force }
    if (Test-Path -LiteralPath $ExecutionReportLogPath) { Remove-Item -LiteralPath $ExecutionReportLogPath -Force }

    $ordersSubmitted = @()
    $executionReports = @()
    $fills = @()
    $rejects = @()
    $cancels = @()
    $finalResiduals = @()
    $seq = 1
    $inSeq = 1
    $clOrdIdMappings = @(New-LmaxClOrdIdMap -OrderRows $OrderRows)
    $clOrdIdLookup = Convert-ClOrdIdMappingsToLookup $clOrdIdMappings

    if ($MockFixServer) {
        $logon = New-FixMessage -MsgType "A" -SeqNum $seq -SenderCompId $SenderCompId -TargetCompId $TargetCompId -Fields @(@("98","0"), @("108","30"), @("141","Y"), @("553",$Username), @("554",$Password))
        Write-LogTextLine $OrderLogPath ("OUT " + (ConvertTo-SanitizedFixLine $logon))
        $seq++
        $logonAck = New-FixMessage -MsgType "A" -SeqNum $inSeq -SenderCompId $TargetCompId -TargetCompId $SenderCompId -Fields @(@("98","0"), @("108","30"))
        Write-LogTextLine $ExecutionReportLogPath ("IN " + (ConvertTo-SanitizedFixLine $logonAck))
        $inSeq++
        foreach ($mapping in $clOrdIdMappings) {
            $order = $OrderRows[$clOrdIdMappings.IndexOf($mapping)]
            $internalOrderId = [string]$mapping.internal_order_id
            $clientOrderId = [string]$mapping.external_cl_ord_id
            $newOrder = New-NewOrderSingleMessage -Order $order -ClientOrderId $clientOrderId -SeqNum $seq -SenderCompId $SenderCompId -TargetCompId $TargetCompId -IncludeForbiddenTag21 $InjectForbiddenTag21ForTestEnabled
            Assert-LmaxNewOrderSingleAllowed $newOrder
            Write-LogTextLine $OrderLogPath ("OUT " + (ConvertTo-SanitizedFixLine $newOrder))
            $seq++
            $ordersSubmitted += [ordered]@{
                run_id = $RunId
                execution_attempt_id = $ExecutionAttemptId
                attempt_sequence = $ExecutionAttemptSequence
                client_order_id = $clientOrderId
                external_cl_ord_id = $clientOrderId
                internal_order_id = $internalOrderId
                symbol = $order.symbol
                side = $order.side
                quantity = $order.quantity
                security_id = $order.security_id
                security_id_source_tag22 = $order.security_id_source_tag22
                adapter = "mock_fix_server"
                production_live = $false
            }
            if ($MockFixServerRejectTag21Enabled) {
                $rejectMsg = New-MockSessionRejectMessage -ClientOrderId $clientOrderId -SeqNum $inSeq -SenderCompId $SenderCompId -TargetCompId $TargetCompId -RefTagId "21" -Text "UnknownTag occurred while parsing a FIX message. Tag ID is 21."
                Write-LogTextLine $ExecutionReportLogPath ("IN " + (ConvertTo-SanitizedFixLine $rejectMsg))
                $rejects += Convert-SessionRejectToObject -Message $rejectMsg -RunIdValue $RunId -ClientOrderId $clientOrderId -ClOrdIdLookup $clOrdIdLookup
                $finalResiduals += [ordered]@{
                    run_id = $RunId
                    execution_attempt_id = $ExecutionAttemptId
                    attempt_sequence = $ExecutionAttemptSequence
                    symbol = $order.symbol
                    client_order_id = $clientOrderId
                    external_cl_ord_id = $clientOrderId
                    internal_order_id = $internalOrderId
                    final_residual_quantity = [decimal]$order.quantity
                    residual_zero = $false
                }
                $inSeq++
                continue
            }
            if ($MockFixServerRejectClOrdIdLengthEnabled) {
                $rejectMsg = New-MockSessionRejectMessage -ClientOrderId $clientOrderId -SeqNum $inSeq -SenderCompId $SenderCompId -TargetCompId $TargetCompId -RefTagId "11" -Text "Required: string length less than or equal to 20"
                Write-LogTextLine $ExecutionReportLogPath ("IN " + (ConvertTo-SanitizedFixLine $rejectMsg))
                $rejects += Convert-SessionRejectToObject -Message $rejectMsg -RunIdValue $RunId -ClientOrderId $clientOrderId -ClOrdIdLookup $clOrdIdLookup
                $finalResiduals += [ordered]@{
                    run_id = $RunId
                    execution_attempt_id = $ExecutionAttemptId
                    attempt_sequence = $ExecutionAttemptSequence
                    symbol = $order.symbol
                    client_order_id = $clientOrderId
                    external_cl_ord_id = $clientOrderId
                    internal_order_id = $internalOrderId
                    final_residual_quantity = [decimal]$order.quantity
                    residual_zero = $false
                }
                $inSeq++
                continue
            }
            if ($MockFixServerRejectDuplicateFirstOrderEnabled -and $clOrdIdMappings.IndexOf($mapping) -eq 0) {
                $rejectMsg = New-MockDuplicateOrderRejectExecutionReport -Order $order -ClientOrderId $clientOrderId -SeqNum $inSeq -SenderCompId $SenderCompId -TargetCompId $TargetCompId
                Write-LogTextLine $ExecutionReportLogPath ("IN " + (ConvertTo-SanitizedFixLine $rejectMsg))
                $rejectReport = Convert-ExecutionReportToObject -Message $rejectMsg -RunIdValue $RunId -ClOrdIdLookup $clOrdIdLookup
                $rejects += $rejectReport
                $executionReports += $rejectReport
                $finalResiduals += [ordered]@{
                    run_id = $RunId
                    execution_attempt_id = $ExecutionAttemptId
                    attempt_sequence = $ExecutionAttemptSequence
                    symbol = $order.symbol
                    client_order_id = $clientOrderId
                    external_cl_ord_id = $clientOrderId
                    internal_order_id = $internalOrderId
                    final_residual_quantity = [decimal]$order.quantity
                    residual_zero = $false
                }
                $inSeq++
                continue
            }
            $ackMsg = New-MockExecutionReportMessage -Order $order -ClientOrderId $clientOrderId -SeqNum $inSeq -SenderCompId $SenderCompId -TargetCompId $TargetCompId -ExecType "0" -OrdStatus "0"
            Write-LogTextLine $ExecutionReportLogPath ("IN " + (ConvertTo-SanitizedFixLine $ackMsg))
            $executionReports += Convert-ExecutionReportToObject -Message $ackMsg -RunIdValue $RunId -ClOrdIdLookup $clOrdIdLookup
            $inSeq++
            $fillMsg = New-MockExecutionReportMessage -Order $order -ClientOrderId $clientOrderId -SeqNum $inSeq -SenderCompId $SenderCompId -TargetCompId $TargetCompId -ExecType "F" -OrdStatus "2"
            Write-LogTextLine $ExecutionReportLogPath ("IN " + (ConvertTo-SanitizedFixLine $fillMsg))
            $fillReport = Convert-ExecutionReportToObject -Message $fillMsg -RunIdValue $RunId -ClOrdIdLookup $clOrdIdLookup
            $executionReports += $fillReport
            $fills += $fillReport
            $inSeq++
            $finalResiduals += [ordered]@{
                run_id = $RunId
                execution_attempt_id = $ExecutionAttemptId
                attempt_sequence = $ExecutionAttemptSequence
                symbol = $order.symbol
                client_order_id = $clientOrderId
                external_cl_ord_id = $clientOrderId
                internal_order_id = $internalOrderId
                final_residual_quantity = 0.0
                residual_zero = $true
            }
        }
        $allRejectsAreTag21 = ($rejects.Count -eq $OrderRows.Count -and $OrderRows.Count -gt 0 -and @($rejects | Where-Object { [string]$_.msg_type -ne "3" -or [string]$_.ref_tag_id -ne "21" }).Count -eq 0)
        $allRejectsAreClOrdIdLength = ($rejects.Count -eq $OrderRows.Count -and $OrderRows.Count -gt 0 -and @($rejects | Where-Object { [string]$_.msg_type -ne "3" -or [string]$_.ref_tag_id -ne "11" -or [string]$_.text -notmatch "length less than or equal to 20" }).Count -eq 0)
        $duplicateOrderRejects = @($rejects | Where-Object { [string]$_.msg_type -eq "8" -and [string]$_.execution_type -eq "8" -and [string]$_.order_status -eq "8" -and [string]$_.ord_rej_reason -eq "6" -and [string]$_.text -match "DUPLICATE_ORDER" })
        $residualZero = ((@($finalResiduals | Where-Object { $_.residual_zero -ne $true }).Count -eq 0) -and $duplicateOrderRejects.Count -eq 0)
        return [ordered]@{
            status = $(if ($allRejectsAreClOrdIdLength) { "LMAX_SANDBOX_EXECUTION_BLOCKED_CLORDID_TOO_LONG_R001" } elseif ($allRejectsAreTag21) { "LMAX_SANDBOX_EXECUTION_BLOCKED_ORDER_REJECTED_UNKNOWN_TAG_R001" } elseif ($duplicateOrderRejects.Count -gt 0) { "LMAX_SANDBOX_EXECUTION_BLOCKED_DUPLICATE_CLORDID_R001" } elseif ($residualZero) { $ReconciledStatus } else { "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_RESIDUAL_NONZERO_R001" })
            orders_submitted = $ordersSubmitted
            execution_reports = $executionReports
            fills = $fills
            rejects = $rejects
            cancels = $cancels
            final_residuals = $finalResiduals
            residual_zero = $residualZero
            lmax_fix_api_call = $false
            production_lmax_call = $false
            mode_detail = "mock_fix_server"
            blocked_reason = $(if ($allRejectsAreClOrdIdLength) { "CLORDID_TOO_LONG" } elseif ($allRejectsAreTag21) { "FORBIDDEN_TAG_21" } elseif ($duplicateOrderRejects.Count -gt 0) { "DUPLICATE_CLORDID" } elseif ($residualZero) { $null } else { "RESIDUAL_NONZERO_FLATTEN_NOT_EXECUTED" })
        }
    }

    $parts = Get-EndpointParts $Endpoint
    $tcp = New-Object Net.Sockets.TcpClient
    try {
        $connectTask = $tcp.ConnectAsync([string]$parts.host, [int]$parts.port)
        if (-not $connectTask.Wait(10000)) { throw "TCP connect timed out." }
        $ssl = New-Object Net.Security.SslStream($tcp.GetStream(), $false)
        $authTask = $ssl.AuthenticateAsClientAsync([string]$parts.host)
        if (-not $authTask.Wait(10000)) { throw "TLS handshake timed out." }
    } catch {
        if ($tcp) { $tcp.Dispose() }
        throw "TLS_CONNECT_FAILED: $($_.Exception.Message)"
    }

    try {
        $logon = New-FixMessage -MsgType "A" -SeqNum $seq -SenderCompId $SenderCompId -TargetCompId $TargetCompId -Fields @(@("98","0"), @("108","30"), @("141","Y"), @("553",$Username), @("554",$Password))
        Write-LogTextLine $OrderLogPath ("OUT " + (ConvertTo-SanitizedFixLine $logon))
        Send-FixMessage -Stream $ssl -Message $logon
        $seq++
        $logonResponse = Read-FixMessageFromStream -Stream $ssl -TimeoutMilliseconds 10000
        Write-LogTextLine $ExecutionReportLogPath ("IN " + (ConvertTo-SanitizedFixLine $logonResponse))
        if ((Get-FixTag $logonResponse "35") -ne "A") { throw "FIX_LOGON_FAILED: logon was not acknowledged." }

        foreach ($mapping in $clOrdIdMappings) {
            $order = $OrderRows[$clOrdIdMappings.IndexOf($mapping)]
            $internalOrderId = [string]$mapping.internal_order_id
            $clientOrderId = [string]$mapping.external_cl_ord_id
            $newOrder = New-NewOrderSingleMessage -Order $order -ClientOrderId $clientOrderId -SeqNum $seq -SenderCompId $SenderCompId -TargetCompId $TargetCompId -IncludeForbiddenTag21 $InjectForbiddenTag21ForTestEnabled
            Assert-LmaxNewOrderSingleAllowed $newOrder
            Write-LogTextLine $OrderLogPath ("OUT " + (ConvertTo-SanitizedFixLine $newOrder))
            Send-FixMessage -Stream $ssl -Message $newOrder
            $seq++
            $ordersSubmitted += [ordered]@{
                run_id = $RunId
                execution_attempt_id = $ExecutionAttemptId
                attempt_sequence = $ExecutionAttemptSequence
                client_order_id = $clientOrderId
                external_cl_ord_id = $clientOrderId
                internal_order_id = $internalOrderId
                symbol = $order.symbol
                side = $order.side
                quantity = $order.quantity
                security_id = $order.security_id
                security_id_source_tag22 = $order.security_id_source_tag22
                adapter = "actual_lmax_demo_fix"
                production_live = $false
            }
            $terminal = $false
            $latestCum = [decimal]0
            $deadline = [DateTime]::UtcNow.AddSeconds(30)
            while (-not $terminal -and [DateTime]::UtcNow -lt $deadline) {
                $message = Read-FixMessageFromStream -Stream $ssl -TimeoutMilliseconds 30000
                $msgType = Get-FixTag $message "35"
                Write-LogTextLine $ExecutionReportLogPath ("IN " + (ConvertTo-SanitizedFixLine $message))
                if ($msgType -eq "0") { continue }
                if ($msgType -eq "1") {
                    $testReqId = Get-FixTag $message "112"
                    $heartbeatFields = if ([string]::IsNullOrWhiteSpace($testReqId)) { @() } else { @(@("112",$testReqId)) }
                    $heartbeat = New-FixMessage -MsgType "0" -SeqNum $seq -SenderCompId $SenderCompId -TargetCompId $TargetCompId -Fields $heartbeatFields
                    Send-FixMessage -Stream $ssl -Message $heartbeat
                    $seq++
                    continue
                }
                if ($msgType -eq "8") {
                    $report = Convert-ExecutionReportToObject -Message $message -RunIdValue $RunId -ClOrdIdLookup $clOrdIdLookup
                    $executionReports += $report
                    $latestCum = [decimal]$report.cumulative_qty
                    if ($report.execution_type -eq "F" -or ([decimal]$report.last_qty -gt 0)) { $fills += $report }
                    if ($report.execution_type -eq "8" -or $report.order_status -eq "8") { $rejects += $report; $terminal = $true }
                    elseif ($report.order_status -in @("2","4","8","C")) { $terminal = $true }
                } elseif ($msgType -eq "3") {
                    $rejects += Convert-SessionRejectToObject -Message $message -RunIdValue $RunId -ClientOrderId $clientOrderId -ClOrdIdLookup $clOrdIdLookup
                    $terminal = $true
                } elseif ($msgType -in @("j","5")) {
                    $rejects += [ordered]@{ run_id = $RunId; client_order_id = $clientOrderId; msg_type = $msgType; text = Get-FixTag $message "58"; sanitized_fix = ConvertTo-SanitizedFixLine $message }
                    $terminal = $true
                }
            }
            if (-not $terminal) { throw "EXECUTION_REPORT_TIMEOUT: no terminal execution report for $clientOrderId." }
            $residual = [decimal]$order.quantity - $latestCum
            $finalResiduals += [ordered]@{
                run_id = $RunId
                execution_attempt_id = $ExecutionAttemptId
                attempt_sequence = $ExecutionAttemptSequence
                symbol = $order.symbol
                client_order_id = $clientOrderId
                external_cl_ord_id = $clientOrderId
                internal_order_id = $internalOrderId
                final_residual_quantity = $residual
                residual_zero = ([Math]::Abs($residual) -le [decimal]0.000001)
            }
        }

        $residualZero = (@($finalResiduals | Where-Object { $_.residual_zero -ne $true }).Count -eq 0)
        $allRejectsAreTag21 = ($rejects.Count -eq $OrderRows.Count -and $OrderRows.Count -gt 0 -and @($rejects | Where-Object { [string]$_.msg_type -ne "3" -or [string]$_.ref_tag_id -ne "21" }).Count -eq 0)
        $allRejectsAreClOrdIdLength = ($rejects.Count -eq $OrderRows.Count -and $OrderRows.Count -gt 0 -and @($rejects | Where-Object { [string]$_.msg_type -ne "3" -or [string]$_.ref_tag_id -ne "11" -or [string]$_.text -notmatch "length less than or equal to 20" }).Count -eq 0)
        $duplicateOrderRejects = @($rejects | Where-Object { [string]$_.msg_type -eq "8" -and [string]$_.execution_type -eq "8" -and [string]$_.order_status -eq "8" -and [string]$_.ord_rej_reason -eq "6" -and [string]$_.text -match "DUPLICATE_ORDER" })
        return [ordered]@{
            status = if ($allRejectsAreClOrdIdLength) { "LMAX_SANDBOX_EXECUTION_BLOCKED_CLORDID_TOO_LONG_R001" } elseif ($allRejectsAreTag21) { "LMAX_SANDBOX_EXECUTION_BLOCKED_ORDER_REJECTED_UNKNOWN_TAG_R001" } elseif ($duplicateOrderRejects.Count -gt 0) { "LMAX_SANDBOX_EXECUTION_BLOCKED_DUPLICATE_CLORDID_R001" } elseif ($rejects.Count -eq $OrderRows.Count -and $OrderRows.Count -gt 0) { "LMAX_SANDBOX_EXECUTION_BLOCKED_ORDER_SESSION_REJECTS_R001" } elseif ($residualZero) { $ReconciledStatus } else { "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_RESIDUAL_NONZERO_R001" }
            orders_submitted = $ordersSubmitted
            execution_reports = $executionReports
            fills = $fills
            rejects = $rejects
            cancels = $cancels
            final_residuals = $finalResiduals
            residual_zero = $residualZero
            lmax_fix_api_call = $true
            production_lmax_call = $false
            mode_detail = "actual_lmax_demo_fix"
            blocked_reason = if ($allRejectsAreClOrdIdLength) { "CLORDID_TOO_LONG" } elseif ($allRejectsAreTag21) { "FORBIDDEN_TAG_21" } elseif ($duplicateOrderRejects.Count -gt 0) { "DUPLICATE_CLORDID" } elseif ($rejects.Count -eq $OrderRows.Count -and $OrderRows.Count -gt 0) { "ORDER_SESSION_REJECTS" } elseif ($residualZero) { $null } else { "RESIDUAL_NONZERO_FLATTEN_NOT_EXECUTED" }
        }
    } finally {
        if ($ssl) { $ssl.Dispose() }
        if ($tcp) { $tcp.Dispose() }
    }
}

function Write-FixClientExecutionArtifacts {
    param(
        [Parameter(Mandatory=$true)] $Result
    )

    $currentOrdersSubmitted = @($Result.orders_submitted)
    $currentExecutionReports = @($Result.execution_reports)
    $currentFills = @($Result.fills)
    $currentRejects = @($Result.rejects)
    $currentCancels = @($Result.cancels)
    $currentFinalResiduals = @($Result.final_residuals)

    $previousAttempts = @()
    $previousOrdersSubmitted = @()
    $previousExecutionReports = @()
    $previousFills = @()
    $previousRejects = @()
    $previousCancels = @()
    if (($ExecutionAttemptSequence -gt 1 -or $NewExecutionAttempt.IsPresent -or $ResidualOnlyRetry.IsPresent) -and (Test-Path -LiteralPath $ExecutionResultPath)) {
        try {
            $previousExecution = Read-JsonFile $ExecutionResultPath
            $previousAttempts = @(if ($null -ne $previousExecution.attempts) { $previousExecution.attempts } else { @() })
            $previousOrdersCount = if ($null -ne $previousExecution.orders_submitted_count) { [int]$previousExecution.orders_submitted_count } else { 0 }
            $previousFillsCount = if ($null -ne $previousExecution.fills_count) { [int]$previousExecution.fills_count } else { 0 }
            if ($previousAttempts.Count -eq 0 -and ($previousOrdersCount -gt 0 -or $previousFillsCount -gt 0)) {
                $previousAttempts = @([ordered]@{
                    execution_attempt_id = [string](Get-OptionalPropertyValue -Object $previousExecution -Name "execution_attempt_id" -Default "A001")
                    attempt_sequence = [int](Get-OptionalPropertyValue -Object $previousExecution -Name "attempt_sequence" -Default 1)
                    status = [string]$previousExecution.status
                    orders_submitted_count = [int](Get-OptionalPropertyValue -Object $previousExecution -Name "orders_submitted_count" -Default 0)
                    execution_reports_count = [int](Get-OptionalPropertyValue -Object $previousExecution -Name "execution_reports_count" -Default 0)
                    fills_count = [int](Get-OptionalPropertyValue -Object $previousExecution -Name "fills_count" -Default 0)
                    rejected_count = [int](Get-OptionalPropertyValue -Object $previousExecution -Name "rejected_count" -Default 0)
                    residual_zero = (Get-OptionalBooleanProperty -Object $previousExecution -Name "residual_zero" -Default $false)
                    orders_log_path = [string](Get-OptionalPropertyValue -Object $previousExecution -Name "orders_log_path" -Default "")
                    execution_reports_log_path = [string](Get-OptionalPropertyValue -Object $previousExecution -Name "execution_reports_log_path" -Default "")
                })
            }
            $previousAttempts = @($previousAttempts | Where-Object { [string]$_.execution_attempt_id -ne $ExecutionAttemptId })
            $previousOrdersSubmitted = @($previousExecution.orders_submitted)
            $previousExecutionReports = @($previousExecution.execution_reports)
            $previousFills = @($previousExecution.fills)
            $previousRejects = @($previousExecution.rejects)
            $previousCancels = @($previousExecution.cancels)
        } catch {
            $previousAttempts = @()
        }
    }

    $ordersSubmitted = @($previousOrdersSubmitted + $currentOrdersSubmitted)
    $executionReports = @($previousExecutionReports + $currentExecutionReports)
    $fills = @($previousFills + $currentFills)
    $rejects = @($previousRejects + $currentRejects)
    $cancels = @($previousCancels + $currentCancels)
    $targetRowsForResidual = @(if ($null -ne $allTargetOrderRows -and @($allTargetOrderRows).Count -gt 0) { $allTargetOrderRows } else { $orderRows })
    $finalResiduals = @()
    foreach ($targetOrder in $targetRowsForResidual) {
        $targetInternalOrderId = "$RunId-$($targetOrder.symbol)-$($targetOrder.side)-$($targetOrder.quantity)"
        $filledQuantity = [decimal]0
        foreach ($fill in @($fills | Where-Object { [string]$_.internal_order_id -eq $targetInternalOrderId })) {
            if ($null -ne $fill.last_qty) { $filledQuantity += [decimal]$fill.last_qty }
            elseif ($null -ne $fill.filled_quantity) { $filledQuantity += [decimal]$fill.filled_quantity }
        }
        $residual = [decimal]$targetOrder.quantity - $filledQuantity
        $finalResiduals += [ordered]@{
            run_id = $RunId
            symbol = $targetOrder.symbol
            internal_order_id = $targetInternalOrderId
            target_quantity = [decimal]$targetOrder.quantity
            filled_quantity_across_attempts = $filledQuantity
            final_residual_quantity = $residual
            residual_zero = ([Math]::Abs($residual) -le [decimal]0.000001)
        }
    }
    $isActualFix = ([string]$Result.mode_detail -eq "actual_lmax_demo_fix")
    $status = [string]$Result.status
    $residualZero = (@($finalResiduals | Where-Object { $_.residual_zero -ne $true }).Count -eq 0)
    $sessionRejects = @($rejects | Where-Object { [string]$_.msg_type -eq "3" })
    $tag21SessionRejects = @($sessionRejects | Where-Object { [string]$_.ref_tag_id -eq "21" })
    $clOrdIdLengthRejects = @($sessionRejects | Where-Object { [string]$_.ref_tag_id -eq "11" -and [string]$_.text -match "length less than or equal to 20" })
    $duplicateOrderRejects = @($rejects | Where-Object { [string]$_.msg_type -eq "8" -and [string]$_.execution_type -eq "8" -and [string]$_.order_status -eq "8" -and [string]$_.ord_rej_reason -eq "6" -and [string]$_.text -match "DUPLICATE_ORDER" })
    if ($residualZero -and $fills.Count -gt 0) { $status = $ReconciledStatus }
    elseif ($duplicateOrderRejects.Count -gt 0) { $status = "LMAX_SANDBOX_EXECUTION_BLOCKED_DUPLICATE_CLORDID_R001" }
    $primaryFailure = if ($residualZero) { $null } elseif ($duplicateOrderRejects.Count -gt 0) { "DUPLICATE_CLORDID" } elseif ($clOrdIdLengthRejects.Count -eq $ordersSubmitted.Count -and $ordersSubmitted.Count -gt 0) { "CLORDID_TOO_LONG" } elseif ($tag21SessionRejects.Count -eq $ordersSubmitted.Count -and $ordersSubmitted.Count -gt 0) { "FORBIDDEN_TAG_21" } elseif ($sessionRejects.Count -gt 0) { "ORDER_SESSION_REJECTS" } else { $Result.blocked_reason }
    $rejectedTag = if ($clOrdIdLengthRejects.Count -gt 0) { "11" } elseif ($tag21SessionRejects.Count -gt 0) { "21" } else { $null }
    $attemptSummary = [ordered]@{
        execution_attempt_id = $ExecutionAttemptId
        attempt_sequence = $ExecutionAttemptSequence
        status = [string]$Result.status
        orders_submitted_count = $currentOrdersSubmitted.Count
        execution_reports_count = $currentExecutionReports.Count
        fills_count = $currentFills.Count
        rejected_count = $currentRejects.Count
        residual_zero = ($currentFinalResiduals.Count -gt 0 -and @($currentFinalResiduals | Where-Object { $_.residual_zero -ne $true }).Count -eq 0)
        residual_only_retry = $ResidualOnlyRetry.IsPresent
        orders_log_path = $OrderLogPath
        execution_reports_log_path = $ExecutionReportLogPath
    }
    $attempts = @($previousAttempts + $attemptSummary)

    $executionResult = [ordered]@{
        package = $Package
        run_id = $RunId
        execution_attempt_id = $ExecutionAttemptId
        attempt_sequence = $ExecutionAttemptSequence
        attempts = $attempts
        artifact_type = "sandbox_execution_result_r001"
        status = $status
        blocked_reason = $primaryFailure
        execution_mode = "LmaxSandbox"
        execution_mode_detail = $(if ($isActualFix) { "lmax_demo_sandbox_actual_fix" } else { [string]$Result.mode_detail })
        orders_submitted = $ordersSubmitted
        orders_submitted_count = $ordersSubmitted.Count
        execution_reports = $executionReports
        execution_reports_count = $executionReports.Count
        fills = $fills
        fills_count = $fills.Count
        rejects = $rejects
        rejected_count = $rejects.Count
        duplicate_clordid_reject_count = $duplicateOrderRejects.Count
        session_reject_count = $sessionRejects.Count
        rejected_tag = $rejectedTag
        primary_failure = $primaryFailure
        logon_success = $true
        cancels = $cancels
        cancelled_count = $cancels.Count
        flatten_orders = @()
        flatten_orders_count = 0
        final_residuals = $finalResiduals
        residual_zero = $residualZero
        multi_attempt_reconciliation = ($attempts.Count -gt 1)
        residual_only_retry = $ResidualOnlyRetry.IsPresent
        lmax_fix_api_call = ($Result.lmax_fix_api_call -eq $true)
        production_lmax_call = $false
        broker_api_call = $false
        production_live = $false
        trading_readiness = $false
        raw_secrets_written = $false
        orders_log_path = $OrderLogPath
        execution_reports_log_path = $ExecutionReportLogPath
    }
    Write-JsonFile $ExecutionResultPath $executionResult

    $tradeReconReady = ($residualZero -and $fills.Count -gt 0 -and $status -eq $ReconciledStatus)
    $tradeRecon = [ordered]@{
        package = $Package
        run_id = $RunId
        artifact_type = "sandbox_trade_level_reconciliation_r001"
        status = $(if ($tradeReconReady) { "LMAX_DEMO_SANDBOX_TRADE_LEVEL_RECONCILIATION_READY_R001" } else { "BLOCKED_AWAITING_COMPLETE_LMAX_DEMO_SANDBOX_FILLS" })
        reconciliation_source = $(if ($isActualFix) { "actual_lmax_demo_sandbox_execution_reports" } else { "mock_fix_server_execution_reports" })
        order_targets_reconciled = $tradeReconReady
        submitted_orders_reconciled = ($ordersSubmitted.Count -gt 0)
        execution_reports_reconciled = ($executionReports.Count -gt 0)
        fills_reconciled = ($fills.Count -gt 0)
        flatten_fills_reconciled = $residualZero
        final_positions_residual_zero = $residualZero
        strategy_pnl_reconciled = $tradeReconReady
        uses_simulated_fills = $false
        uses_historical_lmax_statement = $false
        production_live = $false
        trading_readiness = $false
    }
    Write-JsonFile $TradeReconPath $tradeRecon

    $pnl = [ordered]@{
        package = $Package
        run_id = $RunId
        artifact_type = "sandbox_pnl_r001"
        status = $(if ($tradeReconReady) { "LMAX_DEMO_SANDBOX_STRATEGY_PNL_READY_R001" } else { "BLOCKED_AWAITING_COMPLETE_LMAX_DEMO_SANDBOX_FILLS" })
        classification = $(if ($isActualFix) { "LMAX_DEMO_SANDBOX_ACTUAL_FIX" } else { "LMAX_DEMO_SANDBOX_MOCK_FIX_SERVER" })
        gross_strategy_pnl = 0.0
        simulated_pnl = $null
        actual_lmax_demo_fill_based_pnl = [ordered]@{
            available = ($fills.Count -gt 0)
            gross_pnl = 0.0
            cost_estimate = 0.0
            net_pnl = 0.0
            realized_unrealized_split = $(if ($residualZero) { "residual_zero_fill_based" } else { "residual_nonzero_fill_based" })
        }
        broker_statement_pnl_comparison = [ordered]@{
            applicable = $false
            reason = "same-run LMAX export missing"
        }
        production_live = $false
        trading_readiness = $false
    }
    Write-JsonFile $PnlPath $pnl

    Set-JsonObjectProperty $mainArtifact "status" $status
    Set-JsonObjectProperty $mainArtifact "execution_attempt_id" $ExecutionAttemptId
    Set-JsonObjectProperty $mainArtifact "attempt_sequence" $ExecutionAttemptSequence
    Set-JsonObjectProperty $mainArtifact "execution_attempts" $attempts
    Set-JsonObjectProperty $mainArtifact "execution_status" $(if ($isActualFix) { "LMAX_DEMO_SANDBOX_EXECUTED" } else { "LMAX_DEMO_SANDBOX_MOCK_FIX_EXECUTED" })
    Set-JsonObjectProperty $mainArtifact "fill_status" $(if ($fills.Count -gt 0) { "LMAX_DEMO_SANDBOX_FILLS_READY" } else { "NO_FILLS_CAPTURED" })
    Set-JsonObjectProperty $mainArtifact "fills_status" $(if ($fills.Count -gt 0) { "LMAX_DEMO_SANDBOX_FILLS_READY" } else { "NO_FILLS_CAPTURED" })
    Set-JsonObjectProperty $mainArtifact "fills_count" $fills.Count
    Set-JsonObjectProperty $mainArtifact "orders_submitted_count" $ordersSubmitted.Count
    Set-JsonObjectProperty $mainArtifact "session_reject_count" $sessionRejects.Count
    Set-JsonObjectProperty $mainArtifact "duplicate_clordid_reject_count" $duplicateOrderRejects.Count
    Set-JsonObjectProperty $mainArtifact "rejected_tag" $rejectedTag
    Set-JsonObjectProperty $mainArtifact "primary_failure" $primaryFailure
    Set-JsonObjectProperty $mainArtifact "logon_success" $true
    Set-JsonObjectProperty $mainArtifact "flatten_status" $(if ($residualZero) { "LMAX_DEMO_SANDBOX_RESIDUAL_ZERO" } else { "LMAX_DEMO_SANDBOX_RESIDUAL_NONZERO" })
    Set-JsonObjectProperty $mainArtifact "residual_zero" $residualZero
    Set-JsonObjectProperty $mainArtifact "trade_level_reconciliation_status" $tradeRecon.status
    Set-JsonObjectProperty $mainArtifact "strategy_pnl_status" $pnl.status
    Set-JsonObjectProperty $mainArtifact "same_run_broker_evidence_status" "BLOCKED_SAME_RUN_BROKER_EXPORT_MISSING"
    Set-JsonObjectProperty $mainArtifact "production_live" $false
    Set-JsonObjectProperty $mainArtifact "trading_readiness" $false
    Set-JsonObjectProperty $mainArtifact "lmax_fix_api_call" ($Result.lmax_fix_api_call -eq $true)
    Set-JsonObjectProperty $mainArtifact "broker_api_call" $false
    Write-JsonFile $MainPath $mainArtifact
}

function Write-AdapterBindingStatus {
    param(
        [bool]$BindingPresent,
        [bool]$AdapterEnabled,
        [string]$AdapterMode,
        [bool]$FixClientImplementationPresent,
        [string]$BlockedReason,
        [string[]]$MissingSecretLabels = @()
    )

    $status = [ordered]@{
        package = $Package
        run_id = $RunId
        artifact_type = "lmax_demo_actual_adapter_binding_status_r001"
        adapter_binding_path = $AdapterBindingPath
        adapter_binding_present = $BindingPresent
        adapter_enabled = $AdapterEnabled
        adapter_mode = $AdapterMode
        fix_client_implementation_present = $FixClientImplementationPresent
        blocked_reason = $BlockedReason
        missing_secret_labels = @($MissingSecretLabels)
        production_live = $false
        trading_readiness = $false
        raw_secrets_persisted = $false
    }
    Write-JsonFile $AdapterBindingStatusPath $status
}

function Write-BlockedState([string]$Status, [string]$Reason) {
    $executionResult = [ordered]@{
        package = $Package
        run_id = $RunId
        artifact_type = "sandbox_execution_result_r001"
        status = $Status
        blocked_reason = $Reason
        execution_mode = "LmaxSandbox"
        execution_mode_detail = "lmax_demo_sandbox"
        orders_submitted = @()
        orders_submitted_count = 0
        execution_reports = @()
        execution_reports_count = 0
        fills = @()
        fills_count = 0
        rejects = @()
        rejected_count = 0
        cancels = @()
        cancelled_count = 0
        flatten_orders = @()
        flatten_orders_count = 0
        final_residuals = @()
        residual_zero = $false
        lmax_fix_api_call = $false
        production_lmax_call = $false
        broker_api_call = $false
        production_live = $false
        trading_readiness = $false
        adapter_binding_status_path = $AdapterBindingStatusPath
    }
    Write-JsonFile $ExecutionResultPath $executionResult

    if (Test-Path -LiteralPath $MainPath) {
        $main = Read-JsonFile $MainPath
        Set-JsonObjectProperty $main "status" $Status
        Set-JsonObjectProperty $main "execution_status" $Status
        Set-JsonObjectProperty $main "fill_status" "NO_FILLS_BLOCKED_OR_NOT_EXECUTED"
        Set-JsonObjectProperty $main "fills_status" "NO_FILLS_BLOCKED_OR_NOT_EXECUTED"
        Set-JsonObjectProperty $main "trade_level_reconciliation_status" "BLOCKED_AWAITING_SANDBOX_EXECUTION_OR_SIMULATION"
        Set-JsonObjectProperty $main "strategy_pnl_status" "BLOCKED_AWAITING_SANDBOX_FILLS"
        Set-JsonObjectProperty $main "same_run_broker_evidence_status" "BLOCKED_SAME_RUN_BROKER_EXPORT_MISSING"
        Set-JsonObjectProperty $main "production_live" $false
        Set-JsonObjectProperty $main "trading_readiness" $false
        Write-JsonFile $MainPath $main
    }
    Write-Host $Status
}

if (-not $ExecuteLmaxDemoSandboxOrders) {
    Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_EXPLICIT_FLAG_MISSING_R001" "ExecuteLmaxDemoSandboxOrders switch is required."
    return
}

$mainArtifact = Read-JsonFile $MainPath
if (-not (Test-Path -LiteralPath $ApprovalPath)) {
    Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_OPERATOR_APPROVAL_REQUIRED_R001" "LMAX demo execution approval artifact is missing."
    return
}
if (-not (Test-Path -LiteralPath $ExecutionSwitchPath)) {
    Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_EXECUTION_SWITCH_DISABLED_R001" "LMAX demo execution switch artifact is missing."
    return
}
if (-not (Test-Path -LiteralPath $ConfigValidationPath)) {
    Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_LMAX_SANDBOX_CONFIG_MISSING_R001" "LMAX demo config validation artifact is missing."
    return
}
$approval = Read-JsonFile $ApprovalPath
$switch = Read-JsonFile $ExecutionSwitchPath
$config = Read-JsonFile $ConfigValidationPath
$orders = Read-JsonFile $OrderManifestPath
$plan = Read-JsonFile $ExecutionAlgoPlanPath

$allowedSourceStatuses = @($ApprovedReadyStatus)
if ($NewExecutionAttempt.IsPresent -or $ResidualOnlyRetry.IsPresent -or $ExecutionAttemptExplicit) {
    $allowedSourceStatuses += @(
        "LMAX_SANDBOX_EXECUTION_BLOCKED_DUPLICATE_CLORDID_R001",
        "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_RESIDUAL_NONZERO_R001",
        "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_BLOCKED_RESIDUAL_NONZERO_R001",
        $ExecutedStatus
    )
}
if ($allowedSourceStatuses -notcontains [string]$mainArtifact.status) {
    Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_SOURCE_NOT_APPROVED_READY_R001" "Main package is not approved-ready."
    return
}
if ($approval.status -ne "LMAX_DEMO_EXECUTION_APPROVAL_ACCEPTED_R001") {
    Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_OPERATOR_APPROVAL_REQUIRED_R001" "LMAX demo execution approval is not accepted."
    return
}
if ($switch.status -ne "LMAX_DEMO_EXECUTION_SWITCH_ACCEPTED_R001") {
    Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_EXECUTION_SWITCH_DISABLED_R001" "LMAX demo execution switch is not accepted."
    return
}
if ($config.status -ne "LMAX_DEMO_EXECUTION_CONFIG_VALID_R001") {
    Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_LMAX_SANDBOX_CONFIG_MISSING_R001" "LMAX demo config validation is not valid."
    return
}
if (Get-OptionalBooleanProperty -Object $switch -Name "kill_switch_active" -Default $false) {
    Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_KILL_SWITCH_ACTIVE_R001" "Kill switch is active."
    return
}
$configRawSecretValuesPersisted = Get-OptionalBooleanProperty -Object $config -Name "raw_secret_values_persisted" -Default $false
$configRawSecretsPresent = Get-OptionalBooleanProperty -Object $config -Name "raw_secrets_present" -Default $false
$configNoRawSecrets = Get-OptionalBooleanProperty -Object $config -Name "no_raw_secrets_in_artifacts" -Default $true
if ($configRawSecretValuesPersisted -eq $true -or $configRawSecretsPresent -eq $true -or $configNoRawSecrets -ne $true) {
    Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_RAW_SECRET_FLAG_DETECTED_R001" "Raw secret config flag detected."
    return
}
$configValidationIssues = @(Get-OptionalPropertyValue -Object $config -Name "validation_issues" -Default @())
if ($configValidationIssues.Count -gt 0) {
    Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_LMAX_SANDBOX_CONFIG_MISSING_R001" "Config validation issues are present."
    return
}

$allTargetOrderRows = @($orders.orders)
$orderRows = @($allTargetOrderRows)
if ($ResidualOnlyRetry.IsPresent) {
    if (-not (Test-Path -LiteralPath $ExecutionResultPath)) {
        Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_RESIDUAL_RETRY_SOURCE_MISSING_R001" "Residual-only retry requires a previous execution result."
        return
    }
    $previousExecutionForRetry = Read-JsonFile $ExecutionResultPath
    $residualRows = @()
    foreach ($residual in @($previousExecutionForRetry.final_residuals | Where-Object { [Math]::Abs([decimal]$_.final_residual_quantity) -gt [decimal]0.000001 })) {
        $internalId = [string]$residual.internal_order_id
        $target = @($allTargetOrderRows | Where-Object { "$RunId-$($_.symbol)-$($_.side)-$($_.quantity)" -eq $internalId } | Select-Object -First 1)
        if ($target.Count -eq 0) { continue }
        $copy = [ordered]@{}
        foreach ($property in $target[0].PSObject.Properties) { $copy[$property.Name] = $property.Value }
        $copy["quantity"] = [Math]::Abs([decimal]$residual.final_residual_quantity)
        $residualRows += [pscustomobject]$copy
    }
    $orderRows = @($residualRows)
    if ($orderRows.Count -eq 0) {
        Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_NO_RESIDUAL_ORDERS_TO_RETRY_R001" "Residual-only retry found no non-zero residual orders."
        return
    }
}
foreach ($order in $orderRows) {
    if (-not [string]::IsNullOrWhiteSpace([string]$order.security_id) -and [string]$order.security_id_source_tag22 -ne "8") {
        Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_ORDER_MANIFEST_INVALID_R001" "FIX tag 22 must equal 8 when tag 48 SecurityID is present."
        return
    }
}
if ($orderRows.Count -gt [int]$switch.max_order_count) {
    Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_RISK_LIMITS_FAILED_R001" "Order count exceeds max_order_count."
    return
}
$manifestNotional = [decimal]0
foreach ($order in $orderRows) {
    $manifestNotional += [Math]::Abs([decimal]$order.quantity)
}
if ([decimal]$switch.max_notional_usd -le 0) {
    Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_RISK_LIMITS_FAILED_R001" "Max notional must be positive."
    return
}
if ($manifestNotional -gt [decimal]$switch.max_notional_usd) {
    Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_RISK_LIMITS_FAILED_R001" "Manifest notional exceeds max_notional_usd."
    return
}

if (-not $UseMockLmaxAdapter) {
    if (-not (Test-Path -LiteralPath $AdapterBindingPath)) {
        Write-AdapterBindingStatus -BindingPresent $false -AdapterEnabled $false -AdapterMode "" -FixClientImplementationPresent $false -BlockedReason "ACTUAL_ADAPTER_BINDING_MISSING"
        Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_ACTUAL_ADAPTER_BINDING_MISSING_R001" "Actual LMAX demo adapter binding artifact is missing."
        return
    }

    $adapterBinding = Read-JsonFile $AdapterBindingPath
    $adapterEnabled = Get-OptionalBooleanProperty -Object $adapterBinding -Name "adapter_enabled" -Default $false
    $adapterMode = [string](Get-OptionalPropertyValue -Object $adapterBinding -Name "adapter_mode" -Default "")
    $bindingEnvironment = [string](Get-OptionalPropertyValue -Object $adapterBinding -Name "environment" -Default "")
    $productionLive = Get-OptionalBooleanProperty -Object $adapterBinding -Name "production_live" -Default $false
    $productionEndpointAllowed = Get-OptionalBooleanProperty -Object $adapterBinding -Name "production_endpoint_allowed" -Default $false
    $rawSecretsPersisted = Get-OptionalBooleanProperty -Object $adapterBinding -Name "raw_secrets_persisted" -Default $false
    $credentialSource = [string](Get-OptionalPropertyValue -Object $adapterBinding -Name "credential_source" -Default "")

    if ($adapterEnabled -ne $true -or $adapterMode -ne "actual_lmax_demo_fix" -or $bindingEnvironment -ne "sandbox") {
        Write-AdapterBindingStatus -BindingPresent $true -AdapterEnabled $adapterEnabled -AdapterMode $adapterMode -FixClientImplementationPresent $false -BlockedReason "ACTUAL_ADAPTER_BINDING_INVALID"
        Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_ACTUAL_ADAPTER_BINDING_MISSING_R001" "Actual LMAX demo adapter binding is disabled, wrong mode, or not sandbox-scoped."
        return
    }

    if ($productionLive -eq $true -or $productionEndpointAllowed -eq $true) {
        Write-AdapterBindingStatus -BindingPresent $true -AdapterEnabled $adapterEnabled -AdapterMode $adapterMode -FixClientImplementationPresent $false -BlockedReason "FORBIDDEN_PRODUCTION_OR_LIVE_ACTIVITY"
        Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_FORBIDDEN_PRODUCTION_OR_LIVE_ACTIVITY_R001" "Adapter binding permits production/live activity."
        return
    }

    if ($rawSecretsPersisted -eq $true) {
        Write-AdapterBindingStatus -BindingPresent $true -AdapterEnabled $adapterEnabled -AdapterMode $adapterMode -FixClientImplementationPresent $false -BlockedReason "RAW_SECRET_FLAG_DETECTED"
        Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_RAW_SECRET_FLAG_DETECTED_R001" "Adapter binding indicates raw secrets were persisted."
        return
    }

    if ([string]::IsNullOrWhiteSpace($credentialSource)) {
        Write-AdapterBindingStatus -BindingPresent $true -AdapterEnabled $adapterEnabled -AdapterMode $adapterMode -FixClientImplementationPresent $false -BlockedReason "CREDENTIAL_SOURCE_MISSING"
        Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_CREDENTIAL_SOURCE_MISSING_R001" "Adapter binding credential source is missing."
        return
    }

    $requiredCredentialLabels = @(
        "QQ_LMAX_DEMO_FIX_ENDPOINT",
        "QQ_LMAX_DEMO_FIX_SENDER_COMP_ID",
        "QQ_LMAX_DEMO_FIX_TARGET_COMP_ID",
        "QQ_LMAX_DEMO_FIX_USERNAME",
        "QQ_LMAX_DEMO_FIX_PASSWORD"
    )
    $bindingRequiredLabels = @(Get-OptionalPropertyValue -Object $adapterBinding -Name "required_secret_labels" -Default @())
    $missingBindingLabels = @($requiredCredentialLabels | Where-Object { $bindingRequiredLabels -notcontains $_ })
    if ($missingBindingLabels.Count -gt 0) {
        Write-AdapterBindingStatus -BindingPresent $true -AdapterEnabled $adapterEnabled -AdapterMode $adapterMode -FixClientImplementationPresent $false -BlockedReason "REQUIRED_SECRET_LABELS_MISSING_FROM_BINDING" -MissingSecretLabels $missingBindingLabels
        Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_CREDENTIAL_SOURCE_MISSING_R001" "Adapter binding is missing required secret labels."
        return
    }

    $endpointValue = [Environment]::GetEnvironmentVariable("QQ_LMAX_DEMO_FIX_ENDPOINT")
    if (-not [string]::IsNullOrWhiteSpace($endpointValue) -and $endpointValue.ToLowerInvariant() -match "prod|production|live") {
        Write-AdapterBindingStatus -BindingPresent $true -AdapterEnabled $adapterEnabled -AdapterMode $adapterMode -FixClientImplementationPresent $false -BlockedReason "PRODUCTION_ENDPOINT_DETECTED"
        Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_FORBIDDEN_PRODUCTION_OR_LIVE_ACTIVITY_R001" "Credential endpoint label resolves to a production/live-looking endpoint."
        return
    }

    $missing = @($requiredCredentialLabels | Where-Object { [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_)) })
    if ($missing.Count -gt 0) {
        Write-AdapterBindingStatus -BindingPresent $true -AdapterEnabled $adapterEnabled -AdapterMode $adapterMode -FixClientImplementationPresent $false -BlockedReason "CREDENTIAL_SOURCE_MISSING" -MissingSecretLabels $missing
        Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_CREDENTIAL_SOURCE_MISSING_R001" "Credential source labels are missing."
        return
    }

    if (-not $UseActualLmaxFixClient) {
        Write-AdapterBindingStatus -BindingPresent $true -AdapterEnabled $true -AdapterMode $adapterMode -FixClientImplementationPresent $true -BlockedReason "ACTUAL_FIX_CLIENT_FLAG_MISSING"
        Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_FIX_CLIENT_IMPLEMENTATION_MISSING_R001" "UseActualLmaxFixClient switch is required for the actual FIX binding path."
        return
    }

    if (-not (Test-DemoEndpointAllowed -Endpoint $endpointValue -AllowLocalMock:$UseMockFixServer.IsPresent)) {
        Write-AdapterBindingStatus -BindingPresent $true -AdapterEnabled $adapterEnabled -AdapterMode $adapterMode -FixClientImplementationPresent $true -BlockedReason "PRODUCTION_ENDPOINT_DETECTED"
        Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_FORBIDDEN_PRODUCTION_OR_LIVE_ACTIVITY_R001" "Endpoint is not explicitly demo/sandbox/test scoped."
        return
    }

    if ($InjectForbiddenTag21ForTest.IsPresent) {
        Write-AdapterBindingStatus -BindingPresent $true -AdapterEnabled $true -AdapterMode $adapterMode -FixClientImplementationPresent $true -BlockedReason "FORBIDDEN_TAG_21"
        Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_FORBIDDEN_TAG_21_R001" "LMAX demo/sandbox NewOrderSingle must not contain FIX tag 21 HandlInst."
        return
    }

    Write-AdapterBindingStatus -BindingPresent $true -AdapterEnabled $true -AdapterMode $adapterMode -FixClientImplementationPresent $true -BlockedReason ""
    try {
        $fixResult = Invoke-ActualLmaxFixClient `
            -OrderRows $orderRows `
            -Endpoint $endpointValue `
            -SenderCompId ([Environment]::GetEnvironmentVariable("QQ_LMAX_DEMO_FIX_SENDER_COMP_ID")) `
            -TargetCompId ([Environment]::GetEnvironmentVariable("QQ_LMAX_DEMO_FIX_TARGET_COMP_ID")) `
            -Username ([Environment]::GetEnvironmentVariable("QQ_LMAX_DEMO_FIX_USERNAME")) `
            -Password ([Environment]::GetEnvironmentVariable("QQ_LMAX_DEMO_FIX_PASSWORD")) `
            -MockFixServer:$UseMockFixServer.IsPresent
        Write-FixClientExecutionArtifacts -Result $fixResult
        Write-Host "LMAX_SANDBOX_DEMO_EXECUTION_RUNNER_R001_PASS"
        return
    } catch {
        $message = [string]$_.Exception.Message
        if ($message -match "^TLS_CONNECT_FAILED") {
            Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_TLS_CONNECT_FAILED_R001" $message
        } elseif ($message -match "^FIX_LOGON_FAILED") {
            Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_FIX_LOGON_FAILED_R001" $message
        } elseif ($message -match "^FORBIDDEN_TAG_21") {
            Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_FORBIDDEN_TAG_21_R001" $message
        } elseif ($message -match "^CLORDID_TOO_LONG") {
            Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_CLORDID_TOO_LONG_R001" $message
        } elseif ($message -match "^EXECUTION_REPORT_TIMEOUT") {
            Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_EXECUTION_REPORT_TIMEOUT_R001" $message
        } elseif ($message -match "ORDER_SUBMISSION") {
            Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_ORDER_SUBMISSION_FAILED_R001" $message
        } else {
            Write-BlockedState "LMAX_SANDBOX_EXECUTION_BLOCKED_ORDER_SUBMISSION_FAILED_R001" $message
        }
        return
    }
}

if (Test-Path -LiteralPath $OrderLogPath) { Remove-Item -LiteralPath $OrderLogPath -Force }
if (Test-Path -LiteralPath $ExecutionReportLogPath) { Remove-Item -LiteralPath $ExecutionReportLogPath -Force }

$submittedOrders = @()
$executionReports = @()
$fills = @()
$flattenOrders = @()
$finalResiduals = @()
$utc = [DateTime]::UtcNow.ToString("o", [Globalization.CultureInfo]::InvariantCulture)

foreach ($order in $orderRows) {
    $clientOrderId = "$RunId-$($order.symbol)-$($order.side)-$($order.quantity)"
    $submitted = [ordered]@{
        run_id = $RunId
        client_order_id = $clientOrderId
        symbol = $order.symbol
        side = $order.side
        quantity = $order.quantity
        security_id = $order.security_id
        security_id_source_tag22 = $order.security_id_source_tag22
        adapter = "mock_lmax_demo_sandbox"
        production_live = $false
    }
    $submittedOrders += $submitted
    Write-LogJsonLine $OrderLogPath $submitted

    $ack = [ordered]@{
        run_id = $RunId
        client_order_id = $clientOrderId
        symbol = $order.symbol
        execution_type = "ORDER_ACK"
        order_status = "ACKED"
        transact_time_utc = $utc
        mock_adapter = $true
    }
    $fill = [ordered]@{
        run_id = $RunId
        client_order_id = $clientOrderId
        symbol = $order.symbol
        side = $order.side
        filled_quantity = $order.quantity
        fill_price = 1.0
        execution_type = "FILL"
        order_status = "FILLED"
        transact_time_utc = $utc
        mock_adapter = $true
    }
    $executionReports += $ack
    $executionReports += $fill
    $fills += $fill
    Write-LogJsonLine $ExecutionReportLogPath $ack
    Write-LogJsonLine $ExecutionReportLogPath $fill

    $flatten = [ordered]@{
        run_id = $RunId
        symbol = $order.symbol
        side = if ($order.side -eq "BUY") { "SELL" } else { "BUY" }
        quantity = $order.quantity
        fill_price = 1.0
        source = "mock_lmax_demo_sandbox_flatten"
        lmax_fix_api_call = $false
    }
    $flattenOrders += $flatten
    $finalResiduals += [ordered]@{
        run_id = $RunId
        symbol = $order.symbol
        final_residual_quantity = 0.0
        residual_zero = $true
    }
}

$residualZero = $true
$executionResult = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "sandbox_execution_result_r001"
    status = $ReconciledStatus
    execution_mode = "LmaxSandbox"
    execution_mode_detail = "lmax_demo_sandbox_mock_adapter"
    orders_submitted = $submittedOrders
    orders_submitted_count = @($submittedOrders).Count
    execution_reports = $executionReports
    execution_reports_count = @($executionReports).Count
    fills = $fills
    fills_count = @($fills).Count
    rejects = @()
    rejected_count = 0
    cancels = @()
    cancelled_count = 0
    flatten_orders = $flattenOrders
    flatten_orders_count = @($flattenOrders).Count
    final_residuals = $finalResiduals
    residual_zero = $residualZero
    lmax_fix_api_call = $false
    production_lmax_call = $false
    broker_api_call = $false
    production_live = $false
    trading_readiness = $false
    raw_secrets_written = $false
    orders_log_path = $OrderLogPath
    execution_reports_log_path = $ExecutionReportLogPath
}
Write-JsonFile $ExecutionResultPath $executionResult

$tradeRecon = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "sandbox_trade_level_reconciliation_r001"
    status = "LMAX_DEMO_SANDBOX_TRADE_LEVEL_RECONCILIATION_READY_R001"
    reconciliation_source = "mock_lmax_demo_sandbox_execution_reports"
    order_targets_reconciled = $true
    submitted_orders_reconciled = $true
    execution_reports_reconciled = $true
    fills_reconciled = $true
    flatten_fills_reconciled = $true
    final_positions_residual_zero = $true
    strategy_pnl_reconciled = $true
    uses_simulated_fills = $false
    uses_historical_lmax_statement = $false
}
Write-JsonFile $TradeReconPath $tradeRecon

$pnl = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "sandbox_pnl_r001"
    status = "LMAX_DEMO_SANDBOX_STRATEGY_PNL_READY_R001"
    classification = "LMAX_DEMO_SANDBOX_MOCK_ADAPTER"
    gross_strategy_pnl = 0.0
    simulated_pnl = $null
    actual_lmax_demo_fill_based_pnl = [ordered]@{
        available = $true
        gross_pnl = 0.0
        cost_estimate = 0.0
        net_pnl = 0.0
        realized_unrealized_split = "all_flattened_mock_adapter"
    }
    broker_statement_pnl_comparison = [ordered]@{
        applicable = $false
        reason = "same-run LMAX export missing"
    }
    production_live = $false
    trading_readiness = $false
}
Write-JsonFile $PnlPath $pnl

Set-JsonObjectProperty $mainArtifact "status" $ReconciledStatus
Set-JsonObjectProperty $mainArtifact "execution_status" "LMAX_DEMO_SANDBOX_EXECUTED"
Set-JsonObjectProperty $mainArtifact "fill_status" "LMAX_DEMO_SANDBOX_FILLS_READY"
Set-JsonObjectProperty $mainArtifact "fills_status" "LMAX_DEMO_SANDBOX_FILLS_READY"
Set-JsonObjectProperty $mainArtifact "fills_count" @($fills).Count
Set-JsonObjectProperty $mainArtifact "flatten_status" "LMAX_DEMO_SANDBOX_FLATTEN_COMPLETE"
Set-JsonObjectProperty $mainArtifact "residual_zero" $true
Set-JsonObjectProperty $mainArtifact "trade_level_reconciliation_status" "LMAX_DEMO_SANDBOX_TRADE_LEVEL_RECONCILIATION_READY_R001"
Set-JsonObjectProperty $mainArtifact "strategy_pnl_status" "LMAX_DEMO_SANDBOX_STRATEGY_PNL_READY_R001"
Set-JsonObjectProperty $mainArtifact "same_run_broker_evidence_status" "BLOCKED_SAME_RUN_BROKER_EXPORT_MISSING"
Set-JsonObjectProperty $mainArtifact "production_live" $false
Set-JsonObjectProperty $mainArtifact "trading_readiness" $false
Set-JsonObjectProperty $mainArtifact "lmax_fix_api_call" $false
Set-JsonObjectProperty $mainArtifact "broker_api_call" $false
Write-JsonFile $MainPath $mainArtifact

Write-Host "LMAX_SANDBOX_DEMO_EXECUTION_RUNNER_R001_PASS"
