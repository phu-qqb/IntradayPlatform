param([string]$PipelineManifestFile = "")

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$results = @()
$expected = @{ GBPUSD="4002"; EURGBP="4003"; USDJPY="4004"; AUDUSD="4007" }

function Add($c,$k,$s,$d){$script:results += [ordered]@{category=$c;check=$k;status=$s;detail=$d}; Write-Host ("{0}: {1} / {2} - {3}" -f $s,$c,$k,$d)}
function Resolve-LocalPath([string]$p){if([string]::IsNullOrWhiteSpace($p)){return $p}; if([IO.Path]::IsPathRooted($p)){$p}else{Join-Path $repoRoot $p}}
function Hits($paths,$patterns){$e=@($paths|Where-Object{Test-Path -LiteralPath $_}); if($e.Count -eq 0){return @()}; @(Select-String -Path $e -Pattern $patterns -SimpleMatch -ErrorAction SilentlyContinue)}

Write-Host "LMAX Read-Only Runtime Phase 6Z-A Additional Instrument Planning Pipeline Gate"
Write-Host "Local-only. This gate does not connect to LMAX, request snapshots, replay, schedule work, or use credentials."

foreach($i in @(
    @{n="Pipeline model";p="src/QQ.Production.Intraday.Infrastructure.Lmax/LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifest.cs"},
    @{n="Pipeline builder";p="scripts/build-lmax-readonly-additional-instrument-planning-pipeline.ps1"},
    @{n="Pipeline tests";p="tests/QQ.Production.Intraday.Tests.Unit/LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifestTests.cs"}
)){
    $p=Join-Path $repoRoot $i.p
    if(Test-Path -LiteralPath $p){Add "Files" "$($i.n) exists" "PASS" $p}else{Add "Files" "$($i.n) exists" "FAIL" "Missing $p"}
}

if([string]::IsNullOrWhiteSpace($PipelineManifestFile)){
    Add "Pipeline" "Pipeline manifest supplied" "WARN" "No pipeline manifest supplied; source-only gate mode."
} else {
    $path=Resolve-LocalPath $PipelineManifestFile
    if(-not(Test-Path -LiteralPath $path)){
        Add "Pipeline" "Pipeline manifest exists" "FAIL" "Missing $path"
    } else {
        $raw=Get-Content -LiteralPath $path -Raw
        $m=$raw|ConvertFrom-Json
        if($raw -match '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=)'){Add "Pipeline" "No sensitive content" "FAIL" "Credential-shaped content found."}else{Add "Pipeline" "No sensitive content" "PASS" "No credential-shaped content."}
        if([string]$m.finalDecision -eq "PASS" -and [int]$m.instrumentCount -eq 4 -and [int]$m.readyForFutureManualConsiderationCount -eq 4 -and [int]$m.executableCount -eq 0){Add "Pipeline" "Aggregate decision" "PASS" "PASS; instrumentCount=4; executableCount=0."}else{Add "Pipeline" "Aggregate decision" "FAIL" "Expected PASS with four instruments and executableCount=0."}
        if(-not[bool]$m.isApprovedForExternalRun -and -not[bool]$m.canRunExternalSnapshot -and -not[bool]$m.eligibleForManualSnapshotAttempt -and -not[bool]$m.externalConnectionAttempted -and -not[bool]$m.snapshotAttempted -and -not[bool]$m.replayAttempted -and -not[bool]$m.schedulerStarted -and -not[bool]$m.orderSubmissionAttempted -and -not[bool]$m.shadowReplaySubmitAttempted -and -not[bool]$m.tradingMutationAttempted -and [string]$m.apiWorkerGatewayMode -eq "FakeLmaxGateway"){Add "Pipeline" "Aggregate non-executable flags" "PASS" "All aggregate execution and mutation flags are false."}else{Add "Pipeline" "Aggregate non-executable flags" "FAIL" "Unsafe aggregate flag detected."}
        foreach($symbol in $expected.Keys){
            $entry=@($m.instruments|Where-Object symbol -eq $symbol)[0]
            if($null -eq $entry){Add "Pipeline" "$symbol present" "FAIL" "Missing $symbol"; continue}
            $paths=@($entry.approvalEnvelopePath,$entry.dryRunReportPath,$entry.attemptGatePath,$entry.executionPlanPath,$entry.operatorSignoffPath,$entry.finalReadinessPath)
            $missing=@($paths|Where-Object{[string]::IsNullOrWhiteSpace($_) -or -not(Test-Path -LiteralPath $_)})
            if([string]$entry.planningSecurityId -eq $expected[$symbol] -and [string]$entry.securityIdSource -eq "8" -and [string]$entry.safetyGateDecision -eq "PASS" -and [string]$entry.preflightDecision -eq "PASS" -and [string]$entry.approvalEnvelopeDecision -eq "AcceptedForPlanning" -and [string]$entry.dryRunDecision -eq "PASS" -and [string]$entry.attemptGateDecision -eq "PASS" -and [string]$entry.executionPlanDecision -eq "PASS" -and [string]$entry.operatorSignoffDecision -eq "SignedForPlanning" -and [string]$entry.finalReadinessDecision -eq "PASS" -and $missing.Count -eq 0){Add "Pipeline" "$symbol artifact chain" "PASS" "$symbol chain complete and decisions safe."}else{Add "Pipeline" "$symbol artifact chain" "FAIL" "$symbol chain missing or decision mismatch."}
            if(-not[bool]$entry.isApprovedForExternalRun -and -not[bool]$entry.eligibleForManualSnapshotAttempt -and -not[bool]$entry.canRunExternalSnapshot -and -not[bool]$entry.externalConnectionAttempted -and -not[bool]$entry.snapshotAttempted -and -not[bool]$entry.replayAttempted -and -not[bool]$entry.orderSubmissionAttempted -and -not[bool]$entry.shadowReplaySubmitAttempted -and -not[bool]$entry.tradingMutationAttempted -and -not[bool]$entry.schedulerStarted -and [bool]$entry.noSensitiveContent){Add "Pipeline" "$symbol non-executable" "PASS" "No executable or unsafe flags."}else{Add "Pipeline" "$symbol non-executable" "FAIL" "Unsafe flag detected."}
        }
    }
}

$api=Join-Path $repoRoot "src/QQ.Production.Intraday.Api/Program.cs"; $worker=Join-Path $repoRoot "src/QQ.Production.Intraday.Worker/Program.cs"; $aw=@($api,$worker)
$reg=Hits $aw @("RealLmaxGateway","ExternalReadOnlyPrototypeGateway","LmaxVenueGatewaySkeleton","SecurityListRequest")
if($reg.Count -eq 0 -and (Select-String -Path $api -Pattern "FakeLmaxGateway" -SimpleMatch -Quiet)){Add "Registration" "API/Worker remain FakeLmaxGateway only" "PASS" "No real gateway registration found."}else{Add "Registration" "API/Worker remain FakeLmaxGateway only" "FAIL" (($reg|ForEach-Object{"$($_.Path):$($_.LineNumber)"}) -join "; ")}
foreach($scan in @(
    @{c="Scheduler";k="No scheduler/polling added";p=@("PeriodicTimer","System.Threading.Timer","IHostedService","BackgroundService","SecurityListPoll")},
    @{c="Replay";k="Runtime still does not submit to shadow replay";p=@("SubmitToShadowReplay = true","SubmittedToShadowReplay = true","ReplaySubmitAsync")},
    @{c="Orders";k="No order surface";p=@("NewOrderSingle","OrderCancelRequest","OrderCancelReplaceRequest","OrderStatusRequest","SubmitOrder")},
    @{c="Mutation";k="No trading-state mutation references";p=@("PersistTrade","TradingState")}
)){
    $h=Hits $aw $scan.p
    if($h.Count -eq 0){Add $scan.c $scan.k "PASS" "No marker found in API/Worker startup."}else{Add $scan.c $scan.k "FAIL" (($h|ForEach-Object{"$($_.Path):$($_.LineNumber)"}) -join "; ")}
}
Add "Runtime" "External LMAX connection" "PASS" "This gate does not connect to LMAX."
Add "Snapshot" "MarketData snapshot" "PASS" "This gate does not request snapshots."
Add "Replay" "Manual replay" "PASS" "This gate does not replay evidence."
Add "Credentials" "Credential values" "PASS" "This gate does not require or print credential values."

$failed=@($results|Where-Object status -eq "FAIL"); $warn=@($results|Where-Object status -eq "WARN")
$decision=if($failed.Count -gt 0){"FAIL"}elseif($warn.Count -gt 0){"PASS_WITH_KNOWN_WARNINGS"}else{"PASS"}
$dir=Join-Path $repoRoot "artifacts/readiness"; New-Item -ItemType Directory -Path $dir -Force|Out-Null
$out=Join-Path $dir "phase6za-additional-instrument-pipeline-gate.json"
[ordered]@{generatedAtUtc=[DateTimeOffset]::UtcNow.ToString("o");phase="6Z-A";finalDecision=$decision;executableCount=0;externalConnectionAttempted=$false;snapshotAttempted=$false;replayAttempted=$false;schedulerStarted=$false;orderSubmissionAttempted=$false;shadowReplaySubmitAttempted=$false;tradingMutationAttempted=$false;results=$results}|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $out -Encoding UTF8
Write-Host ""; Write-Host "FinalDecision: $decision"; Write-Host "Report: $out"; if($decision -eq "FAIL"){exit 1}
