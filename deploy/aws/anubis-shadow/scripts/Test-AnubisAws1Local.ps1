param(
    [string]$RepoRoot = (Resolve-Path ".").Path,
    [string]$ReportPath = "artifacts\readiness\anubis-aws1-read-only-shadow-foundation-plan-ready\AWS1_TEST_REPORT.generated.json",
    [string]$TerraformPath = ""
)

$ErrorActionPreference = "Stop"

function New-Result {
    param([string]$Name, [string]$Status, [string]$Detail = "")
    [ordered]@{ name = $Name; status = $Status; detail = $Detail }
}

function Get-RepoText {
    param([string]$RelativePath)
    return Get-Content -Raw -LiteralPath (Join-Path $script:repo $RelativePath)
}

function Test-ContainsPattern {
    param([string]$Name, [string]$RelativePath, [string[]]$Patterns)
    $text = Get-RepoText -RelativePath $RelativePath
    $missing = @($Patterns | Where-Object { $text -notmatch $_ })
    if ($missing.Count -gt 0) { return New-Result $Name "FAIL" ("missing=" + ($missing -join ", ")) }
    return New-Result $Name "PASS" $RelativePath
}

function Test-NotContainsPattern {
    param([string]$Name, [string]$RelativePath, [string[]]$Patterns)
    $text = Get-RepoText -RelativePath $RelativePath
    $hits = @($Patterns | Where-Object { $text -match $_ })
    if ($hits.Count -gt 0) { return New-Result $Name "FAIL" ("matched=" + ($hits -join ", ")) }
    return New-Result $Name "PASS" $RelativePath
}

function Test-NoRegexMatch {
    param([string]$Name, [string[]]$Paths, [string]$Pattern)
    $matches = @()
    foreach ($path in $Paths) {
        if (Test-Path -LiteralPath $path) {
            $files = Get-ChildItem -LiteralPath $path -Recurse -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Extension -in @(".tf", ".ps1", ".tftpl", ".json") -and $_.Name -ne "Test-AnubisAws1Local.ps1" }
            foreach ($file in $files) {
                $matches += Select-String -LiteralPath $file.FullName -Pattern $Pattern -ErrorAction SilentlyContinue
            }
        }
    }
    if ($matches.Count -gt 0) {
        return New-Result $Name "FAIL" (($matches | Select-Object -First 10 | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; ")
    }
    return New-Result $Name "PASS"
}

function Resolve-TerraformExecutable {
    if (-not [string]::IsNullOrWhiteSpace($TerraformPath)) {
        $full = [System.IO.Path]::GetFullPath((Join-Path $script:repo $TerraformPath))
        if (Test-Path -LiteralPath $full) { return $full }
        if (Test-Path -LiteralPath $TerraformPath) { return $TerraformPath }
        return $null
    }
    $cmd = Get-Command terraform -ErrorAction SilentlyContinue
    if ($null -eq $cmd) { return $null }
    return $cmd.Source
}

function Invoke-TerraformChecks {
    param([string]$TerraformExe, [string]$TfDir)
    $results = New-Object System.Collections.Generic.List[object]
    if ([string]::IsNullOrWhiteSpace($TerraformExe)) {
        $results.Add((New-Result "terraform_fmt" "SKIP" "terraform executable not provided"))
        $results.Add((New-Result "terraform_init_backend_false" "SKIP" "terraform executable not provided"))
        $results.Add((New-Result "terraform_validate" "SKIP" "terraform executable not provided"))
        return @($results.ToArray())
    }

    Push-Location $TfDir
    try {
        $fmtOutput = & $TerraformExe fmt -check -recursive 2>&1
        $fmtCode = $LASTEXITCODE
        $results.Add((New-Result "terraform_fmt" ($(if ($fmtCode -eq 0) { "PASS" } else { "FAIL" })) "exit=$fmtCode"))

        $initOutput = & $TerraformExe init -backend=false 2>&1
        $initCode = $LASTEXITCODE
        $results.Add((New-Result "terraform_init_backend_false" ($(if ($initCode -eq 0) { "PASS" } else { "FAIL" })) "exit=$initCode"))

        if ($initCode -eq 0) {
            $validateOutput = & $TerraformExe validate 2>&1
            $validateCode = $LASTEXITCODE
            $results.Add((New-Result "terraform_validate" ($(if ($validateCode -eq 0) { "PASS" } else { "FAIL" })) "exit=$validateCode"))
        }
        else {
            $results.Add((New-Result "terraform_validate" "FAIL" "terraform init -backend=false exit=$initCode"))
        }
    }
    finally {
        Pop-Location
    }
    return @($results.ToArray())
}

function Test-PowerShellParse {
    $psErrors = @()
    Get-ChildItem -LiteralPath (Join-Path $script:repo "deploy\aws\anubis-shadow\scripts") -Filter "*.ps1" -File | ForEach-Object {
        $tokens = $null
        $errors = $null
        [System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$tokens, [ref]$errors) | Out-Null
        foreach ($err in $errors) { $psErrors += "$($_.Name):$($err.Extent.StartLineNumber):$($err.Message)" }
    }
    return New-Result "powershell_parse" ($(if ($psErrors.Count -eq 0) { "PASS" } else { "FAIL" })) ($psErrors -join "; ")
}

function Test-JsonFile {
    param([string]$Name, [string]$RelativePath)
    try {
        Get-Content -Raw -LiteralPath (Join-Path $script:repo $RelativePath) | ConvertFrom-Json | Out-Null
        return New-Result $Name "PASS" $RelativePath
    }
    catch {
        return New-Result $Name "FAIL" $_.Exception.Message
    }
}

function Test-StatusFixture {
    $fixtureRoot = Join-Path $script:repo ("artifacts\tmp\aws1-status-fixture-" + [guid]::NewGuid().ToString("N"))
    $runRoot = Join-Path $fixtureRoot "run-001"
    $chunkDir = Join-Path $runRoot "chunks"
    $healthDir = Join-Path $runRoot "health"
    New-Item -ItemType Directory -Force -Path $chunkDir, $healthDir | Out-Null

    $now = (Get-Date).ToUniversalTime()
    $event = [ordered]@{
        schema_version = "canonical-recorder-v2-event"
        recorder_run_id = "run-001"
        event_id = "fixture-event-1"
        process_event_sequence = 1
        event_type = "BBO_UPDATED"
        environment = "demo"
        source_component = "LMAX_MARKET_DATA_CAPTURE_ONLY"
        source_contract = "ReadOnlyMarketDataObservationV2"
        source_contract_version = "v2"
        local_receive_utc = $now.AddSeconds(-5).ToString("o")
        recorded_utc = $now.AddSeconds(-4).ToString("o")
        payload_sha256 = "fixture"
        payload_json = @{ bid = 1.1; ask = 1.2 }
        code_commit = "fixture"
        config_hash = "fixture"
        host_id = "fixture"
        process_id = 1
    }
    $chunkPath = Join-Path $chunkDir "events-000001.jsonl"
    Set-Content -LiteralPath $chunkPath -Encoding UTF8 -Value ($event | ConvertTo-Json -Depth 6 -Compress)
    $chunkInfo = Get-Item -LiteralPath $chunkPath
    $chunkSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $chunkPath).Hash.ToUpperInvariant()

    [ordered]@{
        recorder_manifest_version = "canonical-recorder-v2"
        recorder_run_id = "run-001"
        environment = "demo"
        mode = "SMOKE_CAPTURE_BOUNDED"
        start_utc = $now.AddMinutes(-1).ToString("o")
        end_utc = $now.ToString("o")
        finalized = $true
        chunks = @([ordered]@{
            file = "chunks/events-000001.jsonl"
            size_bytes = [int64]$chunkInfo.Length
            sha256 = $chunkSha
            first_sequence = 1
            last_sequence = 1
            event_count = 1
            finalized_utc = $now.ToString("o")
        })
        event_counts = [ordered]@{ BBO_UPDATED = 1 }
        events_enqueued = 1
        events_written = 1
        events_rejected = 0
        events_dropped = 0
        writer_errors = 0
        writer_state = "FINALIZED"
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $runRoot "final_manifest.json") -Encoding UTF8

    [ordered]@{
        status = "GO_M2C2_CAPTURE_VALIDATED"
        recorder_run_id = "run-001"
        bbo_updated = 1
        writer_error_count = 0
        dropped_event_count = 0
        event_counts = [ordered]@{ BBO_UPDATED = 1 }
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot "m2c1b_capture_manifest.json") -Encoding UTF8

    [ordered]@{
        run_status = "FINALIZED"
        event_counts = [ordered]@{ BBO_UPDATED = 1 }
        sequence_out_of_order_count = 0
        sequence_gap_count = 0
        writer_error_count = 0
        dropped_event_count = 0
        manifest_validation_status = "VALID"
        run_integrity_status = "VALID"
        recorder_health_status = "VALID"
        shadow_readiness_status = "READY"
        shadow_ready = $true
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $healthDir "data_quality_report.json") -Encoding UTF8

    $statusJson = & (Join-Path $script:repo "deploy\aws\anubis-shadow\scripts\Get-AnubisAws1Status.ps1") -RecorderRoot $fixtureRoot -Environment "demo" -StateRoot (Join-Path $fixtureRoot "state")
    $status = $statusJson | ConvertFrom-Json
    $lastQuoteMetric = $status.metrics | Where-Object { $_.name -eq "LastQuoteAgeSeconds" } | Select-Object -First 1
    $ok = $status.session_state_ok -eq 1 -and
        $status.bbo_count -eq 1 -and
        $status.sequence_gap_status -eq 0 -and
        $status.recorder_shadow_ready -eq 1 -and
        $lastQuoteMetric.evaluation_status -eq "EVALUATED" -and
        $status.not_evaluated_metrics -notcontains "SessionStateOk"
    return New-Result "status_fixture_real_artifacts" ($(if ($ok) { "PASS" } else { "FAIL" })) ($statusJson -join "")
}

$script:repo = [System.IO.Path]::GetFullPath($RepoRoot)
$results = New-Object System.Collections.Generic.List[object]
$tfDir = Join-Path $script:repo "infra\aws\anubis-shadow"
$terraformExe = Resolve-TerraformExecutable

foreach ($r in (Invoke-TerraformChecks -TerraformExe $terraformExe -TfDir $tfDir)) { $results.Add($r) }
$results.Add((New-Result "terraform_lock_file_present" ($(if (Test-Path -LiteralPath (Join-Path $tfDir ".terraform.lock.hcl")) { "PASS" } else { "FAIL" })) ".terraform.lock.hcl"))
$results.Add((Test-PowerShellParse))
$results.Add((Test-JsonFile "ssm_runbook_json_valid" "deploy\aws\anubis-shadow\ssm\aws1-install-runbook.json"))
$results.Add((Test-StatusFixture))

$tfBlocks = @()
Get-ChildItem -LiteralPath $tfDir -Filter "*.tf" -File | ForEach-Object {
    $content = Get-Content -Raw -LiteralPath $_.FullName
    $tfBlocks += ($content -split '(?m)(?=^resource\s+"aws_)')
}
$recorderIngress = @($tfBlocks | Where-Object { $_ -match 'resource\s+"aws_vpc_security_group_ingress_rule"' -and $_ -match '(?m)^\s*security_group_id\s*=\s*aws_security_group\.recorder\.id' }).Count -gt 0
$recorderSgBlock = @($tfBlocks | Where-Object { $_ -match 'resource\s+"aws_security_group"\s+"recorder"' }) -join "`n"
$inlineIngress = $recorderSgBlock -match '(?m)^\s*ingress\s*\{'
$results.Add((New-Result "no_ec2_ingress" ($(if (-not $recorderIngress -and -not $inlineIngress) { "PASS" } else { "FAIL" })) "recorder security group must not expose ingress"))

$infraDeployPaths = @((Join-Path $script:repo "infra\aws\anubis-shadow"), (Join-Path $script:repo "deploy\aws\anubis-shadow"))
$results.Add((Test-NoRegexMatch "no_aws_apply_commands" $infraDeployPaths "(?i)\bterraform\s+apply\b|\baws\s+cloudformation\s+deploy\b"))
$results.Add((Test-NoRegexMatch "no_rds_initial_path" $infraDeployPaths "(?i)\baws_db_|\brds\b|RelationalDatabase"))
$results.Add((Test-NoRegexMatch "no_order_mutation_surface" $infraDeployPaths "(?i)NewOrderSingle|OrderCancelRequest|CancelReplace|BuildNewOrderSingle|DemoOrderLifecycle"))
$results.Add((Test-NoRegexMatch "no_forbidden_data_vendor_paths" $infraDeployPaths "(?i)Databento|Bloomberg EMSX|Morgan Stanley"))
$results.Add((Test-NoRegexMatch "no_secret_values" $infraDeployPaths "(?i)AKIA[0-9A-Z]{16}|BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY|secret_string\s*="))
$results.Add((Test-NoRegexMatch "no_start_job" $infraDeployPaths "(?i)\bStart-Job\b"))
$results.Add((Test-NoRegexMatch "no_aws_s3_cp_downloads" $infraDeployPaths "(?i)\baws\s+s3\s+cp\b"))

$results.Add((Test-ContainsPattern "package_self_contained_win_x64" "deploy\aws\anubis-shadow\scripts\Package-M2CaptureHost.ps1" @("--self-contained\s+true", "-r\s+win-x64", "framework_dependent_runtimeconfig_detected", "anubis_aws1_read_only_shadow_foundation_plan_ready\.zip")))
$results.Add((Test-ContainsPattern "launcher_uses_verified_exe" "deploy\aws\anubis-shadow\scripts\Start-AnubisAws1Recorder.ps1" @("app_manifest\.json", "executable_sha256", '\$psi\.FileName\s*=\s*\$appExe', "process_start_time_utc")))
$results.Add((Test-NotContainsPattern "launcher_does_not_shell_dotnet" "deploy\aws\anubis-shadow\scripts\Start-AnubisAws1Recorder.ps1" @("(?i)\bdotnet\b")))
$results.Add((Test-ContainsPattern "scheduled_task_system_disabled_by_default" "deploy\aws\anubis-shadow\scripts\Install-AnubisAws1Host.ps1" @('New-ScheduledTaskPrincipal\s+-UserId\s+"SYSTEM"', "Disable-ScheduledTask", "AnubisAws1M2SmokeCaptureOnly")))
$results.Add((Test-ContainsPattern "pid_state_verifies_reuse" "deploy\aws\anubis-shadow\scripts\Stop-AnubisAws1Recorder.ps1" @("executable_path", "process_start_time_utc", "pid_reused_executable_mismatch", "STOP_TIMEOUT_NO_FORCE")))
$results.Add((Test-ContainsPattern "status_metrics_not_synthetic" "deploy\aws\anubis-shadow\scripts\Get-AnubisAws1Status.ps1" @("NOT_EVALUATED", "metrics", "ClockHealthOk", "LastQuoteAgeSeconds")))
$results.Add((Test-NotContainsPattern "status_no_fake_clock_or_age" "deploy\aws\anubis-shadow\scripts\Get-AnubisAws1Status.ps1" @("Local CMOS", "999999")))
$results.Add((Test-ContainsPattern "metrics_publish_only_evaluated" "deploy\aws\anubis-shadow\scripts\Publish-AnubisAws1Metrics.ps1" @('evaluation_status\s+-eq\s+"EVALUATED"', "skipped_metrics")))
$results.Add((Test-ContainsPattern "upload_final_manifest_only_checksum" "deploy\aws\anubis-shadow\scripts\Invoke-AnubisAws1ChunkUpload.ps1" @("final_manifest_and_manifest_listed_chunks_only", "--checksum-algorithm\s+SHA256", "--checksum-mode\s+ENABLED", "environment=\{1\}/date=\{2\}/recorder_run=\{3\}")))
$results.Add((Test-NotContainsPattern "upload_no_metadata_only_hash" "deploy\aws\anubis-shadow\scripts\Invoke-AnubisAws1ChunkUpload.ps1" @("(?i)Metadata\.sha256", "--metadata")))
$results.Add((Test-ContainsPattern "watchdog_smoke_observer_only" "deploy\aws\anubis-shadow\scripts\Invoke-AnubisAws1Watchdog.ps1" @("NO_GO_CONTINUOUS_WATCHDOG_OUT_OF_SCOPE", 'continuous_recorder_supported\s*=\s*\$false', 'restart_performed\s*=\s*\$false')))
$results.Add((Test-ContainsPattern "ssm_downloads_verified_artifacts" "infra\aws\anubis-shadow\ssm.tf" @("aws:downloadContent", "AwsCliMsiSha256", "InstallCaptureHost")))
$results.Add((Test-ContainsPattern "backend_remote_s3_documented" "infra\aws\anubis-shadow\backend.tf" @('backend\s+"s3"', "encrypt\s*=\s*true", "use_lockfile\s*=\s*true")))
$results.Add((Test-ContainsPattern "s3_archive_protected_tls_checks" "infra\aws\anubis-shadow\storage.tf" @("prevent_destroy\s*=\s*true", "abort_incomplete_multipart_upload", "DenyInsecureTransport", "aws_s3_bucket_policy")))
$results.Add((Test-ContainsPattern "ami_fail_closed_data_source" "infra\aws\anubis-shadow\main.tf" @('data\s+"aws_ami"\s+"recorder"', "owners\s*=\s*var\.allowed_ami_owner_ids", "data\.aws_ami\.recorder\.id")))
$results.Add((Test-ContainsPattern "alarm_actions_required_when_enabled" "infra\aws\anubis-shadow\variables.tf" @("enable_cloudwatch_alarms", "default\s*=\s*false", "alarm_action_arns")))
$results.Add((Test-ContainsPattern "broker_cidrs_reject_zero" "infra\aws\anubis-shadow\variables.tf" @("lmax_market_data_egress_cidrs", "cidrhost", "0\.0\.0\.0/0", 'endswith\(cidr, "/0"\)')))
$results.Add((Test-ContainsPattern "bootstrap_spool_fail_closed" "infra\aws\anubis-shadow\user_data.ps1.tftpl" @("spool_disk_not_ready_before_timeout", "Assert-DriveAvailableOrOwned", 'target_drive_conflict:\$\$\{targetDriveLetter\}', "spool_label_verification_failed", "aws1_bootstrap\.json")))

$requiredDocs = @(
    "AWS1_EXISTING_INFRA_AUDIT.md",
    "AWS1_ARCHITECTURE.md",
    "AWS1_SECURITY_MODEL.md",
    "AWS1_NETWORK_MODEL.md",
    "AWS1_STORAGE_AND_RETENTION.md",
    "AWS1_MONITORING_AND_ALERTS.md",
    "AWS1_DEPLOYMENT_RUNBOOK.md",
    "AWS1_ROLLBACK_RUNBOOK.md",
    "AWS1_APPLY_CHECKLIST.md",
    "AWS1_COST_COMPONENTS.md",
    "AWS1_TEST_REPORT.md",
    "gate_report.md"
)
$missingDocs = @($requiredDocs | Where-Object { -not (Test-Path -LiteralPath (Join-Path $script:repo "docs\aws\$_")) })
$results.Add((New-Result "deliverable_docs_present" ($(if ($missingDocs.Count -eq 0) { "PASS" } else { "FAIL" })) ($missingDocs -join ", ")))

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$skipped = @($results | Where-Object { $_.status -eq "SKIP" })
$gate = if ($failed.Count -eq 0 -and $skipped.Count -eq 0) { "GO_AWS1_PLAN_READ_ONLY" } else { "NO_GO_AWS1_PLAN" }
$resultsArray = @($results.ToArray())

$report = [ordered]@{
    artifact_type = "aws1_plan_ready_local_test_report"
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    baseline_commit = "9f606beacbaab1a963a0f060e1ca3ebd09e6aba2"
    app_source_commit = "7e87f3b17c84ac8a0aeb79422e4caa97b915fbb6"
    no_aws_apply = $true
    no_aws_contact_except_terraform_provider_init = $true
    terraform_executable = $terraformExe
    gate = $gate
    results = $resultsArray
    skipped_count = [int]$skipped.Count
    failure_count = [int]$failed.Count
}

$fullReportPath = Join-Path $script:repo $ReportPath
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $fullReportPath) | Out-Null
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $fullReportPath -Encoding UTF8
$report | ConvertTo-Json -Depth 8
