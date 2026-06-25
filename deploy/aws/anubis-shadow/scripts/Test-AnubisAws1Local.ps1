param(
    [string]$RepoRoot = (Resolve-Path ".").Path,
    [string]$ReportPath = "artifacts\readiness\anubis-aws1-read-only-shadow-foundation-no-apply\AWS1_TEST_REPORT.generated.json"
)

$ErrorActionPreference = "Stop"

function New-Result {
    param([string]$Name, [string]$Status, [string]$Detail = "")
    [ordered]@{ name = $Name; status = $Status; detail = $Detail }
}

function Test-NoRegexMatch {
    param([string]$Name, [string[]]$Paths, [string]$Pattern)
    $matches = @()
    foreach ($path in $Paths) {
        if (Test-Path -LiteralPath $path) {
            $files = Get-ChildItem -LiteralPath $path -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -in @(".tf", ".ps1", ".tftpl") -and $_.Name -ne "Test-AnubisAws1Local.ps1" }
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

$repo = [System.IO.Path]::GetFullPath($RepoRoot)
$results = New-Object System.Collections.Generic.List[object]

$tfDir = Join-Path $repo "infra\aws\anubis-shadow"
if (Get-Command terraform -ErrorAction SilentlyContinue) {
    Push-Location $tfDir
    try {
        & terraform fmt -check -recursive
        $fmtCode = $LASTEXITCODE
        $results.Add((New-Result "terraform_fmt" ($(if ($fmtCode -eq 0) { "PASS" } else { "FAIL" })) "exit=$fmtCode"))
        & terraform init -backend=false
        $initCode = $LASTEXITCODE
        if ($initCode -eq 0) {
            & terraform validate
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
}
else {
    $results.Add((New-Result "terraform_fmt" "SKIP" "terraform executable not installed"))
    $results.Add((New-Result "terraform_validate" "SKIP" "terraform executable not installed"))
}

$psErrors = @()
Get-ChildItem -LiteralPath (Join-Path $repo "deploy\aws\anubis-shadow\scripts") -Filter "*.ps1" -File | ForEach-Object {
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$tokens, [ref]$errors) | Out-Null
    foreach ($err in $errors) { $psErrors += "$($_.Name):$($err.Extent.StartLineNumber):$($err.Message)" }
}
$results.Add((New-Result "powershell_parse" ($(if ($psErrors.Count -eq 0) { "PASS" } else { "FAIL" })) ($psErrors -join "; ")))

$tfBlocks = @()
Get-ChildItem -LiteralPath $tfDir -Filter "*.tf" -File | ForEach-Object {
    $content = Get-Content -Raw -LiteralPath $_.FullName
    $tfBlocks += ($content -split '(?m)(?=^resource\s+"aws_)')
}
$recorderIngress = @($tfBlocks | Where-Object { $_ -match 'resource\s+"aws_vpc_security_group_ingress_rule"' -and $_ -match '(?m)^\s*security_group_id\s*=\s*aws_security_group\.recorder\.id' }).Count -gt 0
$recorderSgBlock = @($tfBlocks | Where-Object { $_ -match 'resource\s+"aws_security_group"\s+"recorder"' }) -join "`n"
$inlineIngress = $recorderSgBlock -match '(?m)^\s*ingress\s*\{'
$results.Add((New-Result "no_ec2_ingress" ($(if (-not $recorderIngress -and -not $inlineIngress) { "PASS" } else { "FAIL" })) "recorder security group must not expose ingress"))

$infraDeployPaths = @((Join-Path $repo "infra\aws\anubis-shadow"), (Join-Path $repo "deploy\aws\anubis-shadow"))
$results.Add((Test-NoRegexMatch "no_aws_apply_commands" $infraDeployPaths "(?i)\bterraform\s+apply\b|\baws\s+cloudformation\s+deploy\b"))
$results.Add((Test-NoRegexMatch "no_rds_initial_path" $infraDeployPaths "(?i)\baws_db_|\brds\b|RelationalDatabase"))
$results.Add((Test-NoRegexMatch "no_order_mutation_surface" $infraDeployPaths "(?i)NewOrderSingle|OrderCancelRequest|CancelReplace|BuildNewOrderSingle|DemoOrderLifecycle"))
$results.Add((Test-NoRegexMatch "no_forbidden_data_vendor_paths" $infraDeployPaths "(?i)Databento|Bloomberg EMSX|Morgan Stanley"))
$results.Add((Test-NoRegexMatch "no_secret_values" $infraDeployPaths "(?i)AKIA[0-9A-Z]{16}|BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY|secret_string\s*="))

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
$missingDocs = $requiredDocs | Where-Object { -not (Test-Path -LiteralPath (Join-Path $repo "docs\aws\$_")) }
$results.Add((New-Result "deliverable_docs_present" ($(if ($missingDocs.Count -eq 0) { "PASS" } else { "FAIL" })) ($missingDocs -join ", ")))

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$skipped = @($results | Where-Object { $_.status -eq "SKIP" })
$gate = if ($failed.Count -eq 0 -and $skipped.Count -eq 0) { "GO_AWS1_APPLY_READ_ONLY" } else { "NO_GO_AWS1" }
$resultsArray = @($results.ToArray())

$report = [ordered]@{
    artifact_type = "aws1_local_test_report"
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    baseline_commit = "7e87f3b17c84ac8a0aeb79422e4caa97b915fbb6"
    no_aws_contact = $true
    gate = $gate
    results = $resultsArray
    skipped_count = [int]$skipped.Count
    failure_count = [int]$failed.Count
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent (Join-Path $repo $ReportPath)) | Out-Null
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $repo $ReportPath) -Encoding UTF8
$report | ConvertTo-Json -Depth 8
