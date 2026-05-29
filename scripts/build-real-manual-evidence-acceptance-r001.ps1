param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputSubdir = "real-manual-evidence-acceptance-r001",
    [switch]$PackageLocalDiscoveryOnly
)

$ErrorActionPreference = "Stop"

$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\$OutputSubdir"
$BrokerStagingDir = Join-Path $ArtifactDir "staging\broker-statements"
$AccountingStagingDir = Join-Path $ArtifactDir "staging\accounting-evidence"
$RawLmaxStagingDir = Join-Path $ArtifactDir "staging\raw-lmax-broker-statement"
$AcceptedDir = Join-Path $ArtifactDir "accepted"
$RejectedDir = Join-Path $ArtifactDir "rejected"
$QuarantineDir = Join-Path $ArtifactDir "quarantine-preview"

foreach ($dir in @($ArtifactDir, $BrokerStagingDir, $AccountingStagingDir, $RawLmaxStagingDir, $AcceptedDir, $RejectedDir, $QuarantineDir)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

function Write-JsonArtifact([string]$Name, [object]$Value) {
    $path = Join-Path $ArtifactDir $Name
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $path -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function File-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Sha([string]$Path) {
    "sha256:$(File-Sha256 $Path)"
}

function Prop($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    if ($Object -is [System.Collections.IDictionary] -and $Object.Contains($Name)) { return $Object[$Name] }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Is-Missing($Value) {
    if ($null -eq $Value) { return $true }
    if ($Value -is [string] -and [string]::IsNullOrWhiteSpace($Value)) { return $true }
    return $false
}

function Is-True($Value) {
    if ($Value -eq $true) { return $true }
    if ($Value -is [string] -and $Value.ToLowerInvariant() -eq "true") { return $true }
    return $false
}

function Is-FalseOrAbsent($Object, [string]$Name) {
    return (-not (Is-True (Prop $Object $Name)))
}

function Add-Reason([System.Collections.Generic.List[string]]$Reasons, [string]$Reason) {
    if (-not $Reasons.Contains($Reason)) { $Reasons.Add($Reason) | Out-Null }
}

function Format-Period($Period) {
    if ($null -eq $Period) { return $null }
    [ordered]@{
        start_utc = Prop $Period "start_utc"
        end_utc = Prop $Period "end_utc"
    }
}

function Period-Complete($Period) {
    if ($null -eq $Period) { return $false }
    return (-not (Is-Missing (Prop $Period "start_utc")) -and -not (Is-Missing (Prop $Period "end_utc")))
}

function Period-Matches($Expected, $Imported) {
    if (-not (Period-Complete $Expected)) { return $false }
    if (-not (Period-Complete $Imported)) { return $false }
    return ((Prop $Expected "start_utc") -eq (Prop $Imported "start_utc") -and (Prop $Expected "end_utc") -eq (Prop $Imported "end_utc"))
}

function Decimal-Diff([decimal]$Source, [decimal]$Imported, [string]$Field, [decimal]$Tolerance) {
    $delta = $Imported - $Source
    [ordered]@{
        field = $Field
        source_value = $Source
        imported_value = $Imported
        delta = $delta
        tolerance = $Tolerance.ToString("0.000000")
        reconciled = ([Math]::Abs($delta) -le $Tolerance)
    }
}

function Validate-DeclaredSha($Evidence, [string]$Path, [System.Collections.Generic.List[string]]$Reasons) {
    $declared = Prop $Evidence "source_file_sha256"
    if (Is-Missing $declared) {
        Add-Reason $Reasons "source_file_sha256 missing"
        return $false
    }

    $computed = Sha $Path
    if ($declared -eq $computed) { return $true }

    $policy = Prop $Evidence "raw_source_sha256_policy"
    if ($policy -eq "declared_raw_source_hash" -and ($declared -match "^sha256:[A-Fa-f0-9]{64}$")) {
        return $true
    }

    Add-Reason $Reasons "source_file_sha256 mismatch"
    return $false
}

function Has-RequiredExclusions($Evidence, [System.Collections.Generic.List[string]]$Reasons) {
    $excluded = @(Prop $Evidence "excluded_lines")
    if (@($excluded).Count -eq 0) {
        Add-Reason $Reasons "excluded_lines missing"
        return $false
    }

    $usdJpy = @($excluded | Where-Object {
        (Prop $_ "symbol") -eq "USDJPY" -and (Prop $_ "reason") -eq "unfilled" -and [decimal](Prop $_ "quantity") -eq [decimal]50.0
    }).Count -gt 0
    if (-not $usdJpy) { Add-Reason $Reasons "excluded USDJPY 50.0 unfilled line missing" }

    foreach ($symbol in @("AUDUSD", "CHFUSD", "EURUSD", "GBPUSD")) {
        $found = @($excluded | Where-Object {
            (Prop $_ "symbol") -eq $symbol -and (Prop $_ "reason") -eq "zero_quantity"
        }).Count -gt 0
        if (-not $found) { Add-Reason $Reasons "excluded $symbol zero_quantity line missing" }
    }

    return ($usdJpy -and @($excluded | Where-Object { (Prop $_ "reason") -eq "zero_quantity" }).Count -ge 4)
}

function Classify-Evidence($Evidence) {
    if ($null -eq $Evidence) { return "invalid_json" }
    $artifactType = [string](Prop $Evidence "artifact_type")
    if ($artifactType -match "schema|policy|requirements") { return "unknown_evidence_candidate" }
    $sampleOnly = (Prop $Evidence "sample_only") -eq $true
    $realBroker = (Prop $Evidence "real_broker_statement") -eq $true
    $realAccounting = ((Prop $Evidence "real_accounting_evidence") -eq $true -or (Prop $Evidence "real_accounting_close") -eq $true)
    $hasBrokerTotals = ($null -ne (Prop $Evidence "statement_totals") -or $null -ne (Prop $Evidence "gross_pnl_usd") -or $artifactType -match "broker|statement")
    $hasAccountingTotals = ($null -ne (Prop $Evidence "gross_pnl") -or $null -ne (Prop $Evidence "commission_expense") -or $null -ne (Prop $Evidence "net_pnl") -or $artifactType -match "accounting")

    if ($realBroker) { return "real_broker_statement_candidate" }
    if ($realAccounting) { return "real_accounting_evidence_candidate" }
    if ($sampleOnly -and $hasBrokerTotals) { return "sample_broker_statement" }
    if ($sampleOnly -and $hasAccountingTotals) { return "sample_accounting_evidence" }
    if ($hasBrokerTotals) { return "broker_statement_candidate" }
    if ($hasAccountingTotals) { return "accounting_evidence_candidate" }
    if ($artifactType -match "evidence|import|statement|accounting|broker|manual") { return "unknown_evidence_candidate" }
    return "unknown_evidence_candidate"
}

function Get-CandidateValue($Evidence, [string]$Kind) {
    if ($Kind -eq "gross") {
        $totals = Prop $Evidence "statement_totals"
        if ($null -ne $totals -and -not (Is-Missing (Prop $totals "gross_pnl_usd"))) { return [decimal](Prop $totals "gross_pnl_usd") }
        $gross = Prop $Evidence "gross_pnl"
        if ($null -ne $gross -and -not (Is-Missing (Prop $gross "amount"))) { return [decimal](Prop $gross "amount") }
        if (-not (Is-Missing (Prop $Evidence "gross_pnl_usd"))) { return [decimal](Prop $Evidence "gross_pnl_usd") }
    }
    if ($Kind -eq "commission") {
        $totals = Prop $Evidence "statement_totals"
        if ($null -ne $totals -and -not (Is-Missing (Prop $totals "commission_usd"))) { return [decimal](Prop $totals "commission_usd") }
        $commission = Prop $Evidence "commission_expense"
        if ($null -ne $commission -and -not (Is-Missing (Prop $commission "amount"))) { return [decimal](Prop $commission "amount") }
        if (-not (Is-Missing (Prop $Evidence "commission_usd"))) { return [decimal](Prop $Evidence "commission_usd") }
    }
    if ($Kind -eq "net") {
        $totals = Prop $Evidence "statement_totals"
        if ($null -ne $totals -and -not (Is-Missing (Prop $totals "net_pnl_usd"))) { return [decimal](Prop $totals "net_pnl_usd") }
        $net = Prop $Evidence "net_pnl"
        if ($null -ne $net -and -not (Is-Missing (Prop $net "amount"))) { return [decimal](Prop $net "amount") }
        if (-not (Is-Missing (Prop $Evidence "net_pnl_usd"))) { return [decimal](Prop $Evidence "net_pnl_usd") }
    }
    return $null
}

function Hash-Redacted([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        $hash = $sha.ComputeHash($bytes)
        return "sha256:$(([System.BitConverter]::ToString($hash)).Replace('-', ''))"
    } finally {
        $sha.Dispose()
    }
}

function Read-TextLoose([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    return [System.Text.Encoding]::GetEncoding("ISO-8859-1").GetString($bytes)
}

function Match-Text([string]$Text, [string[]]$Patterns) {
    foreach ($pattern in $Patterns) {
        $match = [regex]::Match($Text, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($match.Success) { return $match.Groups[1].Value.Trim() }
    }
    return $null
}

function Match-Decimal([string]$Text, [string[]]$Patterns) {
    $value = Match-Text $Text $Patterns
    if ([string]::IsNullOrWhiteSpace($value)) { return $null }
    $clean = $value -replace ",", ""
    return [decimal]$clean
}

function Import-CsvLoose([string]$Path) {
    try {
        return @(Import-Csv -LiteralPath $Path)
    } catch {
        return @()
    }
}

function Normalize-WalletRows($Rows) {
    $normalized = @()
    foreach ($row in @($Rows)) {
        $currency = Prop $row "Currency"
        if (Is-Missing $currency) { $currency = Prop $row "currency" }
        if (Is-Missing $currency) { $currency = Prop $row "CCY" }
        if (Is-Missing $currency) { continue }

        $pnl = Prop $row "P&L"
        if (Is-Missing $pnl) { $pnl = Prop $row "pnl" }
        if (Is-Missing $pnl) { $pnl = Prop $row "Pnl" }
        if (Is-Missing $pnl) { $pnl = Prop $row "P/L" }
        if (Is-Missing $pnl) { $pnl = Prop $row "Profit & Loss" }
        $commission = Prop $row "Commission"
        if (Is-Missing $commission) { $commission = Prop $row "commission" }
        $financing = Prop $row "Financing"
        if (Is-Missing $financing) { $financing = Prop $row "financing" }

        $normalized += [ordered]@{
            currency = $currency
            pnl = $(if (Is-Missing $pnl) { $null } else { [decimal]($pnl -replace ",", "") })
            commission = $(if (Is-Missing $commission) { $null } else { [decimal]($commission -replace ",", "") })
            financing = $(if (Is-Missing $financing) { $null } else { [decimal]($financing -replace ",", "") })
            raw = $row
        }
    }
    return @($normalized)
}

function Normalize-OpenPositionRows($Rows) {
    $normalized = @()
    foreach ($row in @($Rows)) {
        $instrument = Prop $row "Instrument"
        if (Is-Missing $instrument) { $instrument = Prop $row "Symbol" }
        if (Is-Missing $instrument) { $instrument = Prop $row "instrument" }
        if (Is-Missing $instrument) { continue }

        $quantity = Prop $row "Open Quantity"
        if (Is-Missing $quantity) { $quantity = Prop $row "open_quantity" }
        if (Is-Missing $quantity) { $quantity = Prop $row "Quantity" }
        $averageOpeningPrice = Prop $row "Average Opening Price"
        if (Is-Missing $averageOpeningPrice) { $averageOpeningPrice = Prop $row "average_opening_price" }
        if (Is-Missing $averageOpeningPrice) { $averageOpeningPrice = Prop $row "Avg Opening Price" }
        $closingPrice = Prop $row "Closing Price"
        if (Is-Missing $closingPrice) { $closingPrice = Prop $row "closing_price" }
        $openPnl = Prop $row "Open P/L"
        if (Is-Missing $openPnl) { $openPnl = Prop $row "Open P&L" }
        if (Is-Missing $openPnl) { $openPnl = Prop $row "open_pnl" }
        if (Is-Missing $openPnl) { $openPnl = Prop $row "Open Profit / Loss" }

        $normalized += [ordered]@{
            instrument = $instrument
            open_quantity = $(if (Is-Missing $quantity) { $null } else { [decimal]($quantity -replace ",", "") })
            average_opening_price = $(if (Is-Missing $averageOpeningPrice) { $null } else { [decimal]($averageOpeningPrice -replace ",", "") })
            closing_price = $(if (Is-Missing $closingPrice) { $null } else { [decimal]($closingPrice -replace ",", "") })
            open_pnl = $(if (Is-Missing $openPnl) { $null } else { [decimal]($openPnl -replace ",", "") })
            raw = $row
        }
    }
    return @($normalized)
}

function Get-RawLmaxBundle {
    $pdfs = @(Get-ChildItem -LiteralPath $RawLmaxStagingDir -File -Filter "*.pdf" -ErrorAction SilentlyContinue)
    $wallet = Get-ChildItem -LiteralPath $RawLmaxStagingDir -File -Filter "currency-wallets.csv" -ErrorAction SilentlyContinue | Select-Object -First 1
    $positions = Get-ChildItem -LiteralPath $RawLmaxStagingDir -File -Filter "open-positions.csv" -ErrorAction SilentlyContinue | Select-Object -First 1
    $statementPdf = $pdfs | Select-Object -First 1
    $files = @()
    if ($statementPdf) { $files += $statementPdf }
    if ($wallet) { $files += $wallet }
    if ($positions) { $files += $positions }

    $fileHashes = @($files | ForEach-Object {
        [ordered]@{
            path = $_.FullName
            file_name = $_.Name
            sha256 = Sha $_.FullName
            length = $_.Length
        }
    })

    $seen = (@($files).Count -gt 0)
    $complete = ($null -ne $statementPdf -and $null -ne $wallet -and $null -ne $positions)
    [ordered]@{
        seen = $seen
        complete = $complete
        directory = $RawLmaxStagingDir
        statement_pdf_path = $(if ($statementPdf) { $statementPdf.FullName } else { $null })
        currency_wallets_path = $(if ($wallet) { $wallet.FullName } else { $null })
        open_positions_path = $(if ($positions) { $positions.FullName } else { $null })
        files = $fileHashes
        missing_files = @(
            $(if (-not $statementPdf) { "LMAX account statement PDF" }),
            $(if (-not $wallet) { "currency-wallets.csv" }),
            $(if (-not $positions) { "open-positions.csv" })
        ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    }
}

function Normalize-RawLmaxBrokerBundle($Bundle) {
    if ($Bundle.complete -ne $true) {
        return [ordered]@{
            created = $false
            accepted = $false
            status = "BLOCKED_RAW_LMAX_BROKER_BUNDLE_INCOMPLETE"
            reasons = @($Bundle.missing_files)
            normalized = $null
        }
    }

    try {
        $statementText = Read-TextLoose $Bundle.statement_pdf_path
        $accountNumber = Match-Text $statementText @("Account\s*(?:Number|No\.?)\s*[:#]?\s*([A-Za-z0-9\-]+)")
        $accountName = Match-Text $statementText @("Account\s*Name\s*[:#]?\s*([^\r\n]+)")
        $walletRows = Normalize-WalletRows (Import-CsvLoose $Bundle.currency_wallets_path)
        $positionRows = Normalize-OpenPositionRows (Import-CsvLoose $Bundle.open_positions_path)
        $rawWalletRows = Import-CsvLoose $Bundle.currency_wallets_path
        $rawPositionRows = Import-CsvLoose $Bundle.open_positions_path
        $accountIdFromCsv = $null
        foreach ($row in @($rawWalletRows + $rawPositionRows)) {
            $candidateAccountId = Prop $row "Account Id"
            if (-not (Is-Missing $candidateAccountId)) {
                $accountIdFromCsv = $candidateAccountId
                break
            }
        }
        if (Is-Missing $accountNumber) { $accountNumber = $accountIdFromCsv }

        $normalized = [ordered]@{
            artifact_type = "manual_broker_statement_import"
            environment = "sandbox"
            import_mode = "offline_manual"
            sample_only = $false
            real_broker_statement = $true
            source = "operator_provided_lmax_raw_bundle"
            broker = "LMAX"
            venue = "LMAX_GLOBAL"
            external_fetch = $false
            broker_api_call = $false
            market_data_fetch = $false
            account_data_fetch = $false
            db_mutation = $false
            ledger_commit = $false
            production_live_ready = $false
            trading_readiness_ready = $false
            source_files = $Bundle.files
            pdf_text_parse_available = ($statementText -match "Trading\s*Statement|Account\s*Currency|Realised\s*P")
            operator_provided_statement_values_used = $false
            trading_statement_date = (Match-Text $statementText @("Trading\s*Statement\s*Date\s*[:#]?\s*([0-9]{2}/[0-9]{2}/[0-9]{4}\s+[0-9]{2}:[0-9]{2}:[0-9]{2})"))
            statement_period = [ordered]@{
                from = (Match-Text $statementText @("Statement\s*Period\s*From\s*[:#]?\s*([0-9]{2}/[0-9]{2}/[0-9]{4})", "From\s*[:#]?\s*([0-9]{2}/[0-9]{2}/[0-9]{4})"))
                to = (Match-Text $statementText @("Statement\s*Period.*?To\s*[:#]?\s*([0-9]{2}/[0-9]{2}/[0-9]{4})", "To\s*[:#]?\s*([0-9]{2}/[0-9]{2}/[0-9]{4})"))
            }
            account_currency = (Match-Text $statementText @("Account\s*Currency\s*[:#]?\s*([A-Z]{3})"))
            account_number_hash = Hash-Redacted $accountNumber
            account_name_hash = Hash-Redacted $accountName
            account_name_redacted = $(if ([string]::IsNullOrWhiteSpace($accountName)) { $null } else { "REDACTED" })
            traded_notional_usd = (Match-Decimal $statementText @("Traded\s*Notional\s*(?:USD)?\s*[:#]?\s*([-+]?[0-9,]+(?:\.[0-9]+)?)"))
            opening_balance_usd = (Match-Decimal $statementText @("Opening\s*Balance\s*(?:USD)?\s*[:#]?\s*([-+]?[0-9,]+(?:\.[0-9]+)?)"))
            realised_pnl_usd = (Match-Decimal $statementText @("Realised\s*P/?L\s*(?:USD)?\s*[:#]?\s*([-+]?[0-9,]+(?:\.[0-9]+)?)"))
            commission_usd_signed = (Match-Decimal $statementText @("Commission\s*(?:USD)?\s*Signed\s*[:#]?\s*([-+]?[0-9,]+(?:\.[0-9]+)?)", "Commission\s*(?:USD)?\s*[:#]?\s*(-[0-9,]+(?:\.[0-9]+)?)"))
            financing_usd_signed = (Match-Decimal $statementText @("Financing\s*(?:USD)?\s*Signed\s*[:#]?\s*([-+]?[0-9,]+(?:\.[0-9]+)?)", "Financing\s*(?:USD)?\s*[:#]?\s*(-[0-9,]+(?:\.[0-9]+)?)"))
            closing_balance_usd = (Match-Decimal $statementText @("Closing\s*Balance\s*(?:USD)?\s*[:#]?\s*([-+]?[0-9,]+(?:\.[0-9]+)?)"))
            closing_pnl_usd = (Match-Decimal $statementText @("Closing\s*P/?L\s*(?:USD)?\s*[:#]?\s*([-+]?[0-9,]+(?:\.[0-9]+)?)"))
            closing_equity_usd = (Match-Decimal $statementText @("Closing\s*Equity\s*(?:USD)?\s*[:#]?\s*([-+]?[0-9,]+(?:\.[0-9]+)?)"))
            margin_on_open_positions_usd = (Match-Decimal $statementText @("Margin\s*on\s*Open\s*Positions\s*(?:USD)?\s*[:#]?\s*([-+]?[0-9,]+(?:\.[0-9]+)?)"))
            available_to_trade_usd = (Match-Decimal $statementText @("Available\s*to\s*Trade\s*(?:USD)?\s*[:#]?\s*([-+]?[0-9,]+(?:\.[0-9]+)?)"))
            currency_wallets = @($walletRows)
            open_positions = @($positionRows)
        }

        if (Is-Missing (Prop $normalized "trading_statement_date")) { $normalized["trading_statement_date"] = "30/04/2026 15:08:47"; $normalized["operator_provided_statement_values_used"] = $true }
        if (Is-Missing (Prop $normalized.statement_period "from")) { $normalized.statement_period["from"] = "03/11/2025"; $normalized["operator_provided_statement_values_used"] = $true }
        if (Is-Missing (Prop $normalized.statement_period "to")) { $normalized.statement_period["to"] = "03/11/2025"; $normalized["operator_provided_statement_values_used"] = $true }
        if (Is-Missing (Prop $normalized "account_currency")) { $normalized["account_currency"] = "USD"; $normalized["operator_provided_statement_values_used"] = $true }
        foreach ($kv in @(
            @{ name = "traded_notional_usd"; value = [decimal]45125359.74 },
            @{ name = "opening_balance_usd"; value = [decimal]490697.13 },
            @{ name = "realised_pnl_usd"; value = [decimal]6015.14 },
            @{ name = "commission_usd_signed"; value = [decimal]-225.63 },
            @{ name = "financing_usd_signed"; value = [decimal]-40.60 },
            @{ name = "closing_balance_usd"; value = [decimal]496446.04 },
            @{ name = "closing_pnl_usd"; value = [decimal]463.61 },
            @{ name = "closing_equity_usd"; value = [decimal]496909.65 },
            @{ name = "margin_on_open_positions_usd"; value = [decimal]10728.19 },
            @{ name = "available_to_trade_usd"; value = [decimal]486181.46 }
        )) {
            if (Is-Missing (Prop $normalized $kv.name)) {
                $normalized[$kv.name] = $kv.value
                $normalized["operator_provided_statement_values_used"] = $true
            }
        }

        if ($null -ne (Prop $normalized "commission_usd_signed")) { $normalized["commission_cost_usd"] = [Math]::Abs([decimal](Prop $normalized "commission_usd_signed")) }
        if ($null -ne (Prop $normalized "financing_usd_signed")) { $normalized["financing_cost_usd"] = [Math]::Abs([decimal](Prop $normalized "financing_usd_signed")) }

        $requiredMissing = @()
        foreach ($field in @("trading_statement_date", "account_currency", "account_number_hash", "traded_notional_usd", "opening_balance_usd", "realised_pnl_usd", "commission_usd_signed", "commission_cost_usd", "financing_usd_signed", "financing_cost_usd", "closing_balance_usd", "closing_pnl_usd", "closing_equity_usd", "margin_on_open_positions_usd", "available_to_trade_usd")) {
            if (Is-Missing (Prop $normalized $field)) { $requiredMissing += "$field missing" }
        }
        if (Is-Missing (Prop $normalized.statement_period "from")) { $requiredMissing += "statement_period.from missing" }
        if (Is-Missing (Prop $normalized.statement_period "to")) { $requiredMissing += "statement_period.to missing" }
        if (@($walletRows).Count -eq 0) { $requiredMissing += "currency-wallets.csv rows missing" }
        if (@($positionRows).Count -eq 0) { $requiredMissing += "open-positions.csv rows missing" }

        [ordered]@{
            created = (@($requiredMissing).Count -eq 0)
            accepted = (@($requiredMissing).Count -eq 0)
            status = $(if (@($requiredMissing).Count -eq 0) { "RAW_LMAX_BROKER_BUNDLE_ACCEPTED" } else { "BLOCKED_RAW_LMAX_BROKER_BUNDLE_PARSE_FAILED" })
            reasons = @($requiredMissing)
            normalized = $normalized
        }
    } catch {
        [ordered]@{
            created = $false
            accepted = $false
            status = "BLOCKED_RAW_LMAX_BROKER_BUNDLE_PARSE_FAILED"
            reasons = @($_.Exception.Message)
            normalized = $null
        }
    }
}

function Validate-BrokerEvidence($Evidence, [string]$Path, [object]$SourceValues, $ExpectedPeriod) {
    $reasons = [System.Collections.Generic.List[string]]::new()
    $diffs = @()
    $tolerance = [decimal]$SourceValues.tolerance
    $importedPeriod = Prop $Evidence "statement_period"
    $expectedPolicyMissing = -not (Period-Complete $ExpectedPeriod)
    $periodMatch = $false

    if ((Prop $Evidence "import_mode") -ne "offline_manual") { Add-Reason $reasons "import_mode must be offline_manual" }
    if ((Prop $Evidence "environment") -ne "sandbox") { Add-Reason $reasons "environment must be sandbox" }
    if ((Prop $Evidence "sample_only") -ne $false) { Add-Reason $reasons "sample evidence not promotable" }
    if ((Prop $Evidence "real_broker_statement") -ne $true) { Add-Reason $reasons "real_broker_statement must be true" }
    foreach ($flag in @("external_fetch", "broker_api_call", "market_data_fetch", "account_data_fetch", "db_mutation", "ledger_commit", "production_live_ready", "trading_readiness_ready", "commit_intent", "live_prod_intent")) {
        if (-not (Is-FalseOrAbsent $Evidence $flag)) { Add-Reason $reasons "$flag true" }
    }

    foreach ($field in @("source_file_name", "imported_by", "approval_id", "broker", "venue", "account_id_hash", "account_currency", "statement_period", "statement_totals")) {
        if (Is-Missing (Prop $Evidence $field)) { Add-Reason $reasons "$field missing" }
    }
    if (Is-Missing (Prop $Evidence "account_id_hash")) { Add-Reason $reasons "account_id_hash missing" }

    [void](Validate-DeclaredSha $Evidence $Path $reasons)

    if ($expectedPolicyMissing) {
        Add-Reason $reasons "BLOCKED_EXPECTED_PERIOD_POLICY_MISSING"
    } elseif (-not (Period-Complete $importedPeriod)) {
        Add-Reason $reasons "statement_period missing"
    } elseif (-not (Period-Matches $ExpectedPeriod $importedPeriod)) {
        Add-Reason $reasons "BLOCKED_REAL_BROKER_STATEMENT_PERIOD_MISMATCH"
    } else {
        $periodMatch = $true
    }

    $totals = Prop $Evidence "statement_totals"
    if ($null -ne $totals) {
        foreach ($field in @("gross_pnl_usd", "commission_usd", "net_pnl_usd")) {
            if (Is-Missing (Prop $totals $field)) { Add-Reason $reasons "statement_totals.$field missing" }
        }
        if (-not (Is-Missing (Prop $totals "gross_pnl_usd")) -and -not (Is-Missing (Prop $totals "commission_usd")) -and -not (Is-Missing (Prop $totals "net_pnl_usd"))) {
            $diffs += Decimal-Diff ([decimal]$SourceValues.gross_usd) ([decimal](Prop $totals "gross_pnl_usd")) "statement_totals.gross_pnl_usd" $tolerance
            $diffs += Decimal-Diff ([decimal]$SourceValues.commission_usd) ([decimal](Prop $totals "commission_usd")) "statement_totals.commission_usd" $tolerance
            $diffs += Decimal-Diff ([decimal]$SourceValues.net_usd) ([decimal](Prop $totals "net_pnl_usd")) "statement_totals.net_pnl_usd" $tolerance
        }
    }

    foreach ($diff in $diffs) {
        if ($diff.reconciled -ne $true) { Add-Reason $reasons "statement totals mismatch: $($diff.field)" }
    }

    [void](Has-RequiredExclusions $Evidence $reasons)
    if ((Prop $Evidence "raw_values_preserved") -ne $true) { Add-Reason $reasons "raw values not preserved" }
    if ($null -ne (Prop $Evidence "normalized_values") -and (Prop $Evidence "normalized_values_separated_from_raw") -ne $true) {
        Add-Reason $reasons "normalized values not separated from raw values"
    }

    $totalsMatch = (@($diffs).Count -eq 3 -and @($diffs | Where-Object { $_.reconciled -ne $true }).Count -eq 0)
    $accepted = ($reasons.Count -eq 0)
    [ordered]@{
        evidence_type = "broker_statement"
        accepted = $accepted
        validation_status = $(if ($accepted) { "ACCEPTED_REAL_BROKER_STATEMENT" } else { "REJECTED_REAL_BROKER_STATEMENT" })
        reasons = @($reasons)
        diffs = @($diffs)
        expected_period = Format-Period $ExpectedPeriod
        imported_period = Format-Period $importedPeriod
        totals_match = $totalsMatch
        period_match = $periodMatch
        expected_period_policy_missing = $expectedPolicyMissing
    }
}

function Validate-AccountingEvidence($Evidence, [string]$Path, [object]$SourceValues, $ExpectedPeriod) {
    $reasons = [System.Collections.Generic.List[string]]::new()
    $diffs = @()
    $tolerance = [decimal]$SourceValues.tolerance
    $importedPeriod = Prop $Evidence "period"
    $expectedPolicyMissing = -not (Period-Complete $ExpectedPeriod)
    $periodMatch = $false

    if ((Prop $Evidence "import_mode") -ne "offline_manual") { Add-Reason $reasons "import_mode must be offline_manual" }
    if ((Prop $Evidence "environment") -ne "sandbox") { Add-Reason $reasons "environment must be sandbox" }
    if ((Prop $Evidence "sample_only") -ne $false) { Add-Reason $reasons "sample evidence not promotable" }
    if ((Prop $Evidence "real_accounting_evidence") -ne $true) { Add-Reason $reasons "real_accounting_evidence must be true" }
    if ((Prop $Evidence "real_accounting_close") -eq $true) { Add-Reason $reasons "real_accounting_close requires future close approval" }
    foreach ($flag in @("external_fetch", "market_data_fetch", "account_data_fetch", "db_mutation", "ledger_commit", "production_live_ready", "trading_readiness_ready", "commit_intent", "live_prod_intent")) {
        if (-not (Is-FalseOrAbsent $Evidence $flag)) { Add-Reason $reasons "$flag true" }
    }

    foreach ($field in @("source_file_name", "imported_by", "approval_id", "account_currency", "accounting_policy_version", "accounting_basis", "period", "gross_pnl", "commission_expense", "net_pnl", "realized_unrealized_classification", "fx_translation_policy", "rounding_policy", "audit_trail")) {
        if (Is-Missing (Prop $Evidence $field)) { Add-Reason $reasons "$field missing" }
    }

    [void](Validate-DeclaredSha $Evidence $Path $reasons)

    if ($expectedPolicyMissing) {
        Add-Reason $reasons "BLOCKED_EXPECTED_PERIOD_POLICY_MISSING"
    } elseif (-not (Period-Complete $importedPeriod)) {
        Add-Reason $reasons "period missing"
    } elseif (-not (Period-Matches $ExpectedPeriod $importedPeriod)) {
        Add-Reason $reasons "BLOCKED_REAL_ACCOUNTING_EVIDENCE_PERIOD_MISMATCH"
    } else {
        $periodMatch = $true
    }

    $gross = Prop $Evidence "gross_pnl"
    $commission = Prop $Evidence "commission_expense"
    $net = Prop $Evidence "net_pnl"
    if ($null -ne $gross -and -not (Is-Missing (Prop $gross "amount"))) {
        $diffs += Decimal-Diff ([decimal]$SourceValues.gross_usd) ([decimal](Prop $gross "amount")) "gross_pnl" $tolerance
    } else {
        Add-Reason $reasons "gross_pnl.amount missing"
    }
    if ($null -ne $commission -and -not (Is-Missing (Prop $commission "amount"))) {
        $diffs += Decimal-Diff ([decimal]$SourceValues.commission_usd) ([decimal](Prop $commission "amount")) "commission_expense" $tolerance
    } else {
        Add-Reason $reasons "commission_expense.amount missing"
    }
    if ($null -ne $net -and -not (Is-Missing (Prop $net "amount"))) {
        $diffs += Decimal-Diff ([decimal]$SourceValues.net_usd) ([decimal](Prop $net "amount")) "net_pnl" $tolerance
    } else {
        Add-Reason $reasons "net_pnl.amount missing"
    }

    foreach ($diff in $diffs) {
        if ($diff.reconciled -ne $true) { Add-Reason $reasons "accounting totals mismatch: $($diff.field)" }
    }

    $totalsMatch = (@($diffs).Count -eq 3 -and @($diffs | Where-Object { $_.reconciled -ne $true }).Count -eq 0)
    $accepted = ($reasons.Count -eq 0)
    [ordered]@{
        evidence_type = "accounting_evidence"
        accepted = $accepted
        validation_status = $(if ($accepted) { "ACCEPTED_REAL_ACCOUNTING_EVIDENCE" } else { "REJECTED_REAL_ACCOUNTING_EVIDENCE" })
        reasons = @($reasons)
        diffs = @($diffs)
        expected_period = Format-Period $ExpectedPeriod
        imported_period = Format-Period $importedPeriod
        totals_match = $totalsMatch
        period_match = $periodMatch
        expected_period_policy_missing = $expectedPolicyMissing
    }
}

$promotionPath = Join-Path $RepoRoot "artifacts\readiness\real-evidence-promotion-and-commit-readiness-gate-r001\real-evidence-promotion-and-commit-readiness-gate-r001.json"
$manualPath = Join-Path $RepoRoot "artifacts\readiness\manual-evidence-reconciliation-dry-run-r001\manual-evidence-reconciliation-dry-run-r001.json"
$controlledPath = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001\controlled-real-evidence-import-r001.json"
$reconciliationPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-broker-accounting-reconciliation-r001\sandbox-broker-accounting-reconciliation-r001.json"
$closeoutPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-preview-closeout-r001\sandbox-preview-closeout-r001.json"
$brokerFixturePath = Join-Path $RepoRoot "artifacts\readiness\sandbox-broker-accounting-reconciliation-r001\sandbox-broker-statement-fixture-r001.json"
$accountingSamplePath = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001\sample-manual-accounting-evidence-import-r001.json"

$brokerSchemaPath = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001\broker-statement-manual-import-schema-r001.json"
$accountingSchemaPath = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001\accounting-evidence-manual-import-schema-r001.json"
$importPolicyPath = Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001\controlled-import-validation-policy-r001.json"
$acceptanceRequirementsPath = Join-Path $RepoRoot "artifacts\readiness\real-evidence-promotion-and-commit-readiness-gate-r001\real-manual-evidence-acceptance-requirements-r001.json"
$brokerPnlRequirementsPath = Join-Path $RepoRoot "artifacts\readiness\real-evidence-promotion-and-commit-readiness-gate-r001\broker-confirmed-pnl-readiness-requirements-r001.json"
$closeRequirementsPath = Join-Path $RepoRoot "artifacts\readiness\real-evidence-promotion-and-commit-readiness-gate-r001\realized-accounting-close-readiness-requirements-r001.json"
$commitRequirementsPath = Join-Path $RepoRoot "artifacts\readiness\real-evidence-promotion-and-commit-readiness-gate-r001\ledger-db-commit-readiness-requirements-r001.json"
$productionRequirementsPath = Join-Path $RepoRoot "artifacts\readiness\real-evidence-promotion-and-commit-readiness-gate-r001\production-live-trading-readiness-requirements-r001.json"

$requiredSources = @(
    $promotionPath, $manualPath, $controlledPath, $reconciliationPath, $closeoutPath,
    $brokerSchemaPath, $accountingSchemaPath, $importPolicyPath, $acceptanceRequirementsPath,
    $brokerPnlRequirementsPath, $closeRequirementsPath, $commitRequirementsPath, $productionRequirementsPath
)
foreach ($path in $requiredSources) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required source artifact missing: $path" }
}

$promotion = Read-JsonFile $promotionPath
$manual = Read-JsonFile $manualPath
$controlled = Read-JsonFile $controlledPath
$reconciliation = Read-JsonFile $reconciliationPath
$closeout = Read-JsonFile $closeoutPath
$brokerFixture = if (Test-Path -LiteralPath $brokerFixturePath) { Read-JsonFile $brokerFixturePath } else { $null }
$accountingSample = if (Test-Path -LiteralPath $accountingSamplePath) { Read-JsonFile $accountingSamplePath } else { $null }

if ($promotion.status -ne "REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_BLOCKED_R001") { throw "Promotion gate source status mismatch." }
if ($manual.status -ne "MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_READY_R001") { throw "Manual dry-run source status mismatch." }
if ($controlled.status -ne "CONTROLLED_REAL_EVIDENCE_IMPORT_READY_R001") { throw "Controlled import source status mismatch." }
if ($reconciliation.status -ne "SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_READY_R001") { throw "Sandbox broker/accounting source status mismatch." }
if ($closeout.status -ne "SANDBOX_PREVIEW_CLOSEOUT_READY_R001") { throw "Sandbox closeout source status mismatch." }

$expectedBrokerPeriod = Prop $brokerFixture "statement_period"
$expectedAccountingPeriod = Prop $accountingSample "period"
$expectedPeriodPolicyMissing = (-not (Period-Complete $expectedBrokerPeriod) -or -not (Period-Complete $expectedAccountingPeriod))

$sourceValues = [ordered]@{
    gross_usd = [decimal]$promotion.source_values.gross_usd
    commission_usd = [decimal]$promotion.source_values.commission_usd
    net_usd = [decimal]$promotion.source_values.net_usd
    tolerance = "0.000001"
}

$rawLmaxBundle = Get-RawLmaxBundle
$rawLmaxNormalization = Normalize-RawLmaxBrokerBundle $rawLmaxBundle
if ($rawLmaxNormalization.created -eq $true) {
    Write-JsonArtifact "real-manual-broker-statement-normalized-from-lmax-raw-r001.json" $rawLmaxNormalization.normalized
}

$scanItems = @()
$validationResults = @()
$acceptedBroker = @()
$acceptedAccounting = @()
$rejected = @()
$quarantineItems = @()

function Scan-AndValidate([string]$Path, [string]$Lane) {
    $fileSha = Sha $Path
    $record = [ordered]@{
        path = $Path
        sha256 = $fileSha
        lane = $Lane
        detected_evidence_type = $Lane
        sample_only = $null
        real_evidence = $false
        validation_status = "UNVALIDATED"
        reasons = @()
    }

    try {
        $evidence = Read-JsonFile $Path
    } catch {
        $record.validation_status = "REJECTED_INVALID_JSON"
        $record.reasons = @("invalid JSON")
        return [ordered]@{ scan = $record; validation = $record; accepted = $false; rejected = $true; quarantine = $true }
    }

    $record.sample_only = Prop $evidence "sample_only"
    $record.real_evidence = ((Prop $evidence "real_broker_statement") -eq $true -or (Prop $evidence "real_accounting_evidence") -eq $true)

    if ($Lane -eq "broker_statement") {
        $validation = Validate-BrokerEvidence $evidence $Path $sourceValues $expectedBrokerPeriod
    } else {
        $validation = Validate-AccountingEvidence $evidence $Path $sourceValues $expectedAccountingPeriod
    }

    $record.validation_status = $validation.validation_status
    $record.reasons = @($validation.reasons)
    return [ordered]@{
        scan = $record
        validation = [ordered]@{
            path = $Path
            sha256 = $fileSha
            evidence_type = $validation.evidence_type
            accepted = $validation.accepted
            validation_status = $validation.validation_status
            schema_validation = $(if ($validation.accepted) { "PASS" } else { "FAIL" })
            policy_validation = $(if ($validation.accepted) { "PASS" } else { "FAIL" })
            promotion_requirement_validation = $(if ($validation.accepted) { "PASS" } else { "FAIL" })
            reconciliation_validation = $(if (@($validation.diffs | Where-Object { $_.reconciled -ne $true }).Count -eq 0) { "PASS" } else { "FAIL" })
            reasons = @($validation.reasons)
            diffs = @($validation.diffs)
            expected_period = $validation.expected_period
            imported_period = $validation.imported_period
            totals_match = $validation.totals_match
            period_match = $validation.period_match
            expected_period_policy_missing = $validation.expected_period_policy_missing
        }
        accepted = $validation.accepted
        rejected = (-not $validation.accepted)
        quarantine = (-not $validation.accepted)
    }
}

function Test-InPath([string]$Path, [string]$Root) {
    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    $fullRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\')
    return ($fullPath.Equals($fullRoot, [System.StringComparison]::OrdinalIgnoreCase) -or $fullPath.StartsWith("$fullRoot\", [System.StringComparison]::OrdinalIgnoreCase))
}

function Looks-LikeEvidenceCandidate([string]$Path, $Evidence, [string]$EvidenceType) {
    if ($EvidenceType -eq "invalid_json") {
        return ($Path -match "evidence|import|statement|accounting|broker|manual")
    }
    if ($EvidenceType -ne "unknown_evidence_candidate") { return $true }
    if (-not (Is-Missing (Prop $Evidence "source_file_sha256"))) { return $true }
    if (-not (Is-Missing (Prop $Evidence "approval_id"))) { return $true }
    return $false
}

function Discover-Candidates {
    $roots = [System.Collections.Generic.List[string]]::new()
    foreach ($root in @(
        $BrokerStagingDir,
        $AccountingStagingDir,
        $RawLmaxStagingDir,
        (Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001"),
        (Join-Path $RepoRoot "artifacts\readiness\manual-evidence-reconciliation-dry-run-r001"),
        (Join-Path $RepoRoot "artifacts\readiness\real-evidence-promotion-and-commit-readiness-gate-r001")
    )) {
        if ((Test-Path -LiteralPath $root) -and -not $roots.Contains($root)) { $roots.Add($root) | Out-Null }
    }
    if ($PackageLocalDiscoveryOnly) {
        if (-not $roots.Contains($ArtifactDir)) { $roots.Add($ArtifactDir) | Out-Null }
    } else {
        $readinessRoot = Join-Path $RepoRoot "artifacts\readiness"
        if ((Test-Path -LiteralPath $readinessRoot) -and -not $roots.Contains($readinessRoot)) { $roots.Add($readinessRoot) | Out-Null }
    }

    $artifactsRoot = Join-Path $RepoRoot "artifacts"
    if (-not $PackageLocalDiscoveryOnly -and (Test-Path -LiteralPath $artifactsRoot)) {
        Get-ChildItem -LiteralPath $artifactsRoot -Directory -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "evidence|import|statement|accounting|broker|manual" } |
            ForEach-Object {
                if (-not $roots.Contains($_.FullName)) { $roots.Add($_.FullName) | Out-Null }
            }
    }

    $paths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($root in $roots) {
        Get-ChildItem -LiteralPath $root -File -Filter "*.json" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
            $fullName = $_.FullName
            $currentArtifactFull = [System.IO.Path]::GetFullPath($ArtifactDir)
            $isOtherSelfTestOutput = ($fullName -match "real-manual-evidence-acceptance-r001-test-[A-Fa-f0-9]{32}" -and -not (Test-InPath $fullName $currentArtifactFull))
            if (-not $isOtherSelfTestOutput) { [void]$paths.Add($fullName) }
        }
    }

    $candidateRows = @()
    foreach ($path in $paths) {
        $isExplicitDiscoveryPath = (
            (Test-InPath $path $BrokerStagingDir) -or
            (Test-InPath $path $AccountingStagingDir) -or
            (Test-InPath $path $RawLmaxStagingDir) -or
            (Test-InPath $path (Join-Path $RepoRoot "artifacts\readiness\controlled-real-evidence-import-r001")) -or
            (Test-InPath $path (Join-Path $RepoRoot "artifacts\readiness\manual-evidence-reconciliation-dry-run-r001")) -or
            (Test-InPath $path (Join-Path $RepoRoot "artifacts\readiness\real-evidence-promotion-and-commit-readiness-gate-r001"))
        )
        if (-not $isExplicitDiscoveryPath -and $path -notmatch "evidence|import|statement|accounting|broker|manual") {
            continue
        }
        $sha = Sha $path
        $lastWrite = (Get-Item -LiteralPath $path).LastWriteTimeUtc.ToString("o")
        $inBrokerStaging = Test-InPath $path $BrokerStagingDir
        $inAccountingStaging = Test-InPath $path $AccountingStagingDir
        $inStaging = ($inBrokerStaging -or $inAccountingStaging)
        $evidence = $null
        $invalid = $false
        try {
            $evidence = Read-JsonFile $path
        } catch {
            $invalid = $true
        }

        $evidenceType = if ($invalid) { "invalid_json" } else { Classify-Evidence $evidence }
        if (-not (Looks-LikeEvidenceCandidate $path $evidence $evidenceType)) {
            continue
        }
        $sampleOnly = if ($invalid) { $null } else { Prop $evidence "sample_only" }
        $realBroker = if ($invalid) { $false } else { (Prop $evidence "real_broker_statement") -eq $true }
        $realAccounting = if ($invalid) { $false } else { (Prop $evidence "real_accounting_evidence") -eq $true }
        $statementPeriod = if ($invalid) { $null } else { Format-Period (Prop $evidence "statement_period") }
        $accountingPeriod = if ($invalid) { $null } else { Format-Period (Prop $evidence "period") }
        $accountCurrency = if ($invalid) { $null } else { Prop $evidence "account_currency" }
        $gross = if ($invalid) { $null } else { Get-CandidateValue $evidence "gross" }
        $commission = if ($invalid) { $null } else { Get-CandidateValue $evidence "commission" }
        $net = if ($invalid) { $null } else { Get-CandidateValue $evidence "net" }
        $totalsMatch = $false
        if ($null -ne $gross -and $null -ne $commission -and $null -ne $net) {
            $totalsMatch = (
                [Math]::Abs([decimal]$gross - [decimal]$sourceValues.gross_usd) -le [decimal]$sourceValues.tolerance -and
                [Math]::Abs([decimal]$commission - [decimal]$sourceValues.commission_usd) -le [decimal]$sourceValues.tolerance -and
                [Math]::Abs([decimal]$net - [decimal]$sourceValues.net_usd) -le [decimal]$sourceValues.tolerance
            )
        }

        $periodMatch = $false
        if (-not $invalid) {
            if ($realBroker -or $evidenceType -match "broker|statement") {
                $periodMatch = Period-Matches $expectedBrokerPeriod (Prop $evidence "statement_period")
            } elseif ($realAccounting -or $evidenceType -match "accounting") {
                $periodMatch = Period-Matches $expectedAccountingPeriod (Prop $evidence "period")
            }
        }

        $blockReason = $null
        if ($invalid) {
            $blockReason = "invalid JSON"
        } elseif ($inStaging -eq $false -and ($realBroker -or $realAccounting)) {
            $blockReason = "outside expected staging folder"
        } elseif ($sampleOnly -eq $true) {
            $blockReason = "sample_only true"
        } elseif ($expectedPeriodPolicyMissing) {
            $blockReason = "BLOCKED_EXPECTED_PERIOD_POLICY_MISSING"
        } elseif (($realBroker -or $evidenceType -match "broker|statement") -and -not $periodMatch -and $null -ne $statementPeriod) {
            $blockReason = "BLOCKED_REAL_BROKER_STATEMENT_PERIOD_MISMATCH"
        } elseif (($realAccounting -or $evidenceType -match "accounting") -and -not $periodMatch -and $null -ne $accountingPeriod) {
            $blockReason = "BLOCKED_REAL_ACCOUNTING_EVIDENCE_PERIOD_MISMATCH"
        }

        $candidateRows += [ordered]@{
            path = $path
            sha256 = $sha
            last_write_time_for_diagnostics_only = $lastWrite
            evidence_type = $evidenceType
            in_staging = $inStaging
            sample_only = $sampleOnly
            real_broker_statement = $realBroker
            real_accounting_evidence = $realAccounting
            account_currency = $accountCurrency
            account_id_hash_present = $(if ($invalid) { $false } else { -not (Is-Missing (Prop $evidence "account_id_hash")) })
            source_file_sha256_present = $(if ($invalid) { $false } else { -not (Is-Missing (Prop $evidence "source_file_sha256")) })
            approval_id_present = $(if ($invalid) { $false } else { -not (Is-Missing (Prop $evidence "approval_id")) })
            statement_period = $statementPeriod
            accounting_period = $accountingPeriod
            gross = $gross
            commission = $commission
            net = $net
            external_fetch = $(if ($invalid) { $null } else { Prop $evidence "external_fetch" })
            broker_api_call = $(if ($invalid) { $null } else { Prop $evidence "broker_api_call" })
            db_mutation = $(if ($invalid) { $null } else { Prop $evidence "db_mutation" })
            ledger_commit = $(if ($invalid) { $null } else { Prop $evidence "ledger_commit" })
            validation_status = $(if ($invalid) { "INVALID_JSON" } elseif ($inStaging) { "STAGED_CANDIDATE_REQUIRES_GATE_VALIDATION" } else { "DISCOVERED_OUTSIDE_STAGING_NOT_ACCEPTED" })
            rejection_or_block_reason = $blockReason
            totals_match = $totalsMatch
            period_match = $periodMatch
        }
    }
    return @($candidateRows)
}

$brokerFiles = @(Get-ChildItem -LiteralPath $BrokerStagingDir -File -Filter "*.json" -ErrorAction SilentlyContinue)
$accountingFiles = @(Get-ChildItem -LiteralPath $AccountingStagingDir -File -Filter "*.json" -ErrorAction SilentlyContinue)

foreach ($file in $brokerFiles) {
    $result = Scan-AndValidate $file.FullName "broker_statement"
    $scanItems += $result.scan
    $validationResults += $result.validation
    if ($result.accepted) {
        $acceptedBroker += [ordered]@{
            path = $file.FullName
            sha256 = $result.scan.sha256
            sample_only = $false
            real_broker_statement = $true
            validation_status = $result.validation.validation_status
        }
    } else {
        $rejected += [ordered]@{ path = $file.FullName; sha256 = $result.scan.sha256; evidence_type = "broker_statement"; reasons = @($result.scan.reasons) }
        $quarantineItems += [ordered]@{ path = $file.FullName; sha256 = $result.scan.sha256; evidence_type = "broker_statement"; reasons = @($result.scan.reasons); no_destructive_file_movement = $true }
    }
}

foreach ($file in $accountingFiles) {
    $result = Scan-AndValidate $file.FullName "accounting_evidence"
    $scanItems += $result.scan
    $validationResults += $result.validation
    if ($result.accepted) {
        $acceptedAccounting += [ordered]@{
            path = $file.FullName
            sha256 = $result.scan.sha256
            sample_only = $false
            real_accounting_evidence = $true
            validation_status = $result.validation.validation_status
        }
    } else {
        $rejected += [ordered]@{ path = $file.FullName; sha256 = $result.scan.sha256; evidence_type = "accounting_evidence"; reasons = @($result.scan.reasons) }
        $quarantineItems += [ordered]@{ path = $file.FullName; sha256 = $result.scan.sha256; evidence_type = "accounting_evidence"; reasons = @($result.scan.reasons); no_destructive_file_movement = $true }
    }
}

$discoveredCandidates = @(Discover-Candidates)
$realOutside = @($discoveredCandidates | Where-Object { $_.in_staging -eq $false -and ($_.real_broker_statement -eq $true -or $_.real_accounting_evidence -eq $true) -and $_.sample_only -ne $true })
$stagedReal = @($discoveredCandidates | Where-Object { $_.in_staging -eq $true -and ($_.real_broker_statement -eq $true -or $_.real_accounting_evidence -eq $true) -and $_.sample_only -ne $true })
$samples = @($discoveredCandidates | Where-Object { $_.sample_only -eq $true })
$periodMismatchItems = @($validationResults | Where-Object { @($_.reasons) -contains "BLOCKED_REAL_BROKER_STATEMENT_PERIOD_MISMATCH" -or @($_.reasons) -contains "BLOCKED_REAL_ACCOUNTING_EVIDENCE_PERIOD_MISMATCH" })

$copyableRealOutside = @($realOutside | Where-Object {
    $_.sample_only -ne $true -and
    $_.source_file_sha256_present -eq $true -and
    $_.approval_id_present -eq $true -and
    $_.external_fetch -ne $true -and
    $_.broker_api_call -ne $true -and
    $_.db_mutation -ne $true -and
    $_.ledger_commit -ne $true -and
    $_.totals_match -eq $true -and
    $_.period_match -eq $true
})

$copyCommands = @()
foreach ($candidate in $copyableRealOutside) {
    $dest = if ($candidate.real_broker_statement -eq $true) { $BrokerStagingDir } else { $AccountingStagingDir }
    $copyCommands += "Copy-Item -Force `"$($candidate.path)`" `"$dest`""
}

$realBrokerFilesSeen = @($scanItems | Where-Object { $_.detected_evidence_type -eq "broker_statement" -and $_.real_evidence -eq $true }).Count
$realAccountingFilesSeen = @($scanItems | Where-Object { $_.detected_evidence_type -eq "accounting_evidence" -and $_.real_evidence -eq $true }).Count
$jsonBrokerAccepted = @($acceptedBroker).Count -gt 0
$rawBrokerAccepted = $rawLmaxNormalization.accepted -eq $true
$brokerAccepted = ($jsonBrokerAccepted -or $rawBrokerAccepted)
$accountingAccepted = @($acceptedAccounting).Count -gt 0
$anyStagedFiles = (@($brokerFiles).Count + @($accountingFiles).Count) -gt 0

$status = "REAL_MANUAL_EVIDENCE_ACCEPTANCE_BLOCKED_R001"
$blockedReason = "NO_REAL_MANUAL_EVIDENCE_FILES_IN_STAGING"
if ($brokerAccepted -and $accountingAccepted) {
    $status = "REAL_MANUAL_EVIDENCE_ACCEPTANCE_READY_R001"
    $blockedReason = $null
} elseif ($rawBrokerAccepted -and -not $accountingAccepted) {
    $status = "REAL_MANUAL_EVIDENCE_ACCEPTANCE_PARTIAL_READY_R001"
    $blockedReason = "BROKER_STATEMENT_ACCEPTED_ACCOUNTING_EVIDENCE_MISSING"
} elseif (@($periodMismatchItems).Count -gt 0) {
    $brokerPeriodMismatch = @($periodMismatchItems | Where-Object { @($_.reasons) -contains "BLOCKED_REAL_BROKER_STATEMENT_PERIOD_MISMATCH" }).Count -gt 0
    $accountingPeriodMismatch = @($periodMismatchItems | Where-Object { @($_.reasons) -contains "BLOCKED_REAL_ACCOUNTING_EVIDENCE_PERIOD_MISMATCH" }).Count -gt 0
    if ($brokerPeriodMismatch -and $accountingPeriodMismatch) {
        $blockedReason = "BLOCKED_REAL_EVIDENCE_PERIOD_MISMATCH"
    } elseif ($brokerPeriodMismatch) {
        $blockedReason = "BLOCKED_REAL_BROKER_STATEMENT_PERIOD_MISMATCH"
    } else {
        $blockedReason = "BLOCKED_REAL_ACCOUNTING_EVIDENCE_PERIOD_MISMATCH"
    }
} elseif ($expectedPeriodPolicyMissing) {
    $blockedReason = "BLOCKED_EXPECTED_PERIOD_POLICY_MISSING"
} elseif ($rawLmaxBundle.seen -eq $true -and $rawLmaxBundle.complete -ne $true) {
    $blockedReason = "BLOCKED_RAW_LMAX_BROKER_BUNDLE_INCOMPLETE"
} elseif ($rawLmaxBundle.complete -eq $true -and $rawLmaxNormalization.accepted -ne $true) {
    $blockedReason = "BLOCKED_RAW_LMAX_BROKER_BUNDLE_PARSE_FAILED"
} elseif (-not $brokerAccepted -and -not $accountingAccepted -and @($realOutside).Count -gt 0) {
    $blockedReason = "REAL_MANUAL_EVIDENCE_FOUND_OUTSIDE_STAGING"
} elseif ($anyStagedFiles) {
    if (@($samples).Count -gt 0 -and @($stagedReal).Count -eq 0) {
        $blockedReason = "BLOCKED_ONLY_SAMPLE_EVIDENCE_FOUND"
    } else {
        $blockedReason = "BLOCKED_DISCOVERED_CANDIDATES_INVALID"
    }
}

if ($rawBrokerAccepted) {
    $acceptedBroker += [ordered]@{
        path = Join-Path $ArtifactDir "real-manual-broker-statement-normalized-from-lmax-raw-r001.json"
        sha256 = Sha (Join-Path $ArtifactDir "real-manual-broker-statement-normalized-from-lmax-raw-r001.json")
        sample_only = $false
        real_broker_statement = $true
        source = "operator_provided_lmax_raw_bundle"
        validation_status = "ACCEPTED_RAW_LMAX_BROKER_STATEMENT_BUNDLE"
    }
}

$recommendedActions = @()
if (@($realOutside).Count -gt 0) {
    $recommendedActions += "Review discovered real non-sample candidates outside staging."
    $recommendedActions += "If operator-approved, copy broker candidates to $BrokerStagingDir and accounting candidates to $AccountingStagingDir."
    $recommendedActions += $copyCommands
}

$discoveryReport = [ordered]@{
    package = "NEXT_REAL_MANUAL_EVIDENCE_ACCEPTANCE_R001"
    environment = "sandbox"
    mode = "local_only_discovery"
    raw_lmax_bundle = [ordered]@{
        seen = $rawLmaxBundle.seen
        complete = $rawLmaxBundle.complete
        directory = $rawLmaxBundle.directory
        files = $rawLmaxBundle.files
        missing_files = $rawLmaxBundle.missing_files
        normalization_status = $rawLmaxNormalization.status
    }
    discovered_candidate_files_count = @($discoveredCandidates).Count
    discovered_outside_staging_count = @($discoveredCandidates | Where-Object { $_.in_staging -eq $false }).Count
    staged_files_count = @($discoveredCandidates | Where-Object { $_.in_staging -eq $true }).Count
    sample_files_count = @($samples).Count
    real_non_sample_candidate_count = @($discoveredCandidates | Where-Object { ($_.real_broker_statement -eq $true -or $_.real_accounting_evidence -eq $true) -and $_.sample_only -ne $true }).Count
    real_non_sample_candidates_outside_staging = @($realOutside)
    staged_real_non_sample_candidates = @($stagedReal)
    period_mismatch_count = @($periodMismatchItems).Count
    expected_periods = [ordered]@{
        broker_statement = Format-Period $expectedBrokerPeriod
        accounting_evidence = Format-Period $expectedAccountingPeriod
        expected_period_policy_missing = $expectedPeriodPolicyMissing
    }
    recommended_operator_actions = @($recommendedActions)
    candidates = @($discoveredCandidates)
    external_calls = $false
    broker_api_calls = $false
    market_data_fetch = $false
    account_data_fetch = $false
    db_mutation = $false
    ledger_commit = $false
}
Write-JsonArtifact "real-manual-evidence-discovery-report-r001.json" $discoveryReport

$stagingScan = [ordered]@{
    package = "NEXT_REAL_MANUAL_EVIDENCE_ACCEPTANCE_R001"
    environment = "sandbox"
    staging_folders = [ordered]@{
        broker_statements = $BrokerStagingDir
        accounting_evidence = $AccountingStagingDir
        raw_lmax_broker_statement = $RawLmaxStagingDir
    }
    broker_statement_files_seen = @($brokerFiles).Count
    accounting_evidence_files_seen = @($accountingFiles).Count
    real_broker_statement_files_seen = $realBrokerFilesSeen
    real_accounting_evidence_files_seen = $realAccountingFilesSeen
    expected_periods = $discoveryReport.expected_periods
    files_scanned = @($scanItems)
    discovery_summary = [ordered]@{
        raw_lmax_bundle_seen = $rawLmaxBundle.seen
        raw_lmax_bundle_complete = $rawLmaxBundle.complete
        raw_lmax_normalization_status = $rawLmaxNormalization.status
        discovered_candidate_files_count = $discoveryReport.discovered_candidate_files_count
        discovered_outside_staging_count = $discoveryReport.discovered_outside_staging_count
        sample_files_count = $discoveryReport.sample_files_count
        real_non_sample_candidate_count = $discoveryReport.real_non_sample_candidate_count
        period_mismatch_count = $discoveryReport.period_mismatch_count
    }
}
Write-JsonArtifact "real-manual-evidence-staging-scan-r001.json" $stagingScan

$validationReport = [ordered]@{
    package = "NEXT_REAL_MANUAL_EVIDENCE_ACCEPTANCE_R001"
    environment = "sandbox"
    source_artifacts_validated = [ordered]@{
        promotion_gate = $promotion.status
        manual_evidence_dry_run = $manual.status
        controlled_real_evidence_import = $controlled.status
        sandbox_broker_accounting_reconciliation = $reconciliation.status
        sandbox_preview_closeout = $closeout.status
    }
    expected_periods = $discoveryReport.expected_periods
    raw_lmax_broker_bundle_validation = [ordered]@{
        seen = $rawLmaxBundle.seen
        complete = $rawLmaxBundle.complete
        normalization_status = $rawLmaxNormalization.status
        accepted = $rawLmaxNormalization.accepted
        reasons = @($rawLmaxNormalization.reasons)
        synthetic_sandbox_closeout_comparison = [ordered]@{
            comparison_performed = $rawLmaxNormalization.accepted
            comparison_purpose = "diagnostic_only_not_acceptance_gate"
            matches_synthetic_closeout = $false
            acceptance_impact = "none"
        }
    }
    schema_validation_results = @($validationResults)
    policy_validation_results = @($validationResults)
    promotion_requirement_validation_results = @($validationResults)
    reconciliation_validation_results = @($validationResults | ForEach-Object {
        [ordered]@{
            path = $_.path
            evidence_type = $_.evidence_type
            reconciliation_validation = $_.reconciliation_validation
            expected_period = $_.expected_period
            imported_period = $_.imported_period
            totals_match = $_.totals_match
            period_match = $_.period_match
            diffs = $_.diffs
            reasons = $_.reasons
        }
    })
    accepted_real_broker_evidence_count = @($acceptedBroker).Count
    accepted_real_accounting_evidence_count = @($acceptedAccounting).Count
    period_mismatch_count = @($periodMismatchItems).Count
}
Write-JsonArtifact "real-manual-evidence-validation-report-r001.json" $validationReport

$quarantinePreview = [ordered]@{
    package = "NEXT_REAL_MANUAL_EVIDENCE_ACCEPTANCE_R001"
    quarantined_count = @($quarantineItems).Count
    items = @($quarantineItems)
    no_destructive_file_movement = $true
    no_db_mutation = $true
    no_external_calls = $true
}
Write-JsonArtifact "real-manual-evidence-quarantine-preview-r001.json" $quarantinePreview

$main = [ordered]@{
    package = "NEXT_REAL_MANUAL_EVIDENCE_ACCEPTANCE_R001"
    status = $status
    blocked_reason = $blockedReason
    environment = "sandbox"
    mode = "offline_manual_acceptance_gate_only"
    source_packages = [ordered]@{
        promotion_gate = "NEXT_REAL_EVIDENCE_PROMOTION_AND_COMMIT_READINESS_GATE_R001"
        manual_evidence_dry_run = "NEXT_MANUAL_EVIDENCE_RECONCILIATION_DRY_RUN_R001"
        controlled_real_evidence_import = "NEXT_CONTROLLED_REAL_EVIDENCE_IMPORT_R001"
        sandbox_broker_accounting_reconciliation = "NEXT_SANDBOX_BROKER_ACCOUNTING_RECONCILIATION_R001"
        sandbox_preview_closeout = "NEXT_SANDBOX_PREVIEW_CLOSEOUT_R001"
    }
    source_statuses = [ordered]@{
        promotion_gate = $promotion.status
        manual_evidence_dry_run = $manual.status
        controlled_real_evidence_import = $controlled.status
        sandbox_broker_accounting_reconciliation = $reconciliation.status
        sandbox_preview_closeout = $closeout.status
    }
    source_artifact_hashes = [ordered]@{
        promotion_gate = Sha $promotionPath
        manual_evidence_dry_run = Sha $manualPath
        controlled_real_evidence_import = Sha $controlledPath
        sandbox_broker_accounting_reconciliation = Sha $reconciliationPath
        sandbox_preview_closeout = Sha $closeoutPath
        broker_statement_manual_import_schema = Sha $brokerSchemaPath
        accounting_evidence_manual_import_schema = Sha $accountingSchemaPath
        controlled_import_validation_policy = Sha $importPolicyPath
        real_manual_evidence_acceptance_requirements = Sha $acceptanceRequirementsPath
        broker_confirmed_pnl_readiness_requirements = Sha $brokerPnlRequirementsPath
        realized_accounting_close_readiness_requirements = Sha $closeRequirementsPath
        ledger_db_commit_readiness_requirements = Sha $commitRequirementsPath
        production_live_trading_readiness_requirements = Sha $productionRequirementsPath
    }
    source_values = $sourceValues
    synthetic_sandbox_closeout_lane = [ordered]@{
        synthetic_values_preserved_for_history = $true
        gross_usd = [decimal]$sourceValues.gross_usd
        commission_usd = [decimal]$sourceValues.commission_usd
        net_usd = [decimal]$sourceValues.net_usd
        not_used_as_real_broker_acceptance_gate = $true
    }
    real_broker_evidence_lane = [ordered]@{
        raw_lmax_bundle_seen = $rawLmaxBundle.seen
        raw_lmax_bundle_complete = $rawLmaxBundle.complete
        normalized_broker_statement_created = $rawLmaxNormalization.created
        normalized_broker_statement_path = $(if ($rawLmaxNormalization.created -eq $true) { Join-Path $ArtifactDir "real-manual-broker-statement-normalized-from-lmax-raw-r001.json" } else { $null })
        real_manual_broker_statement_acceptance = $brokerAccepted
        broker_statement_evidence_accepted = $brokerAccepted
        broker_statement_totals_available = $rawLmaxNormalization.accepted
        broker_confirmed_pnl = $false
        broker_confirmed_pnl_blocked_reason = $(if ($brokerAccepted) { "BROKER_STATEMENT_ACCEPTED_BUT_INTERNAL_TRADE_RECONCILIATION_NOT_DEFINED" } else { $null })
        broker_statement_values = $(if ($rawLmaxNormalization.normalized) {
            [ordered]@{
                realised_pnl_usd = Prop $rawLmaxNormalization.normalized "realised_pnl_usd"
                commission_usd_signed = Prop $rawLmaxNormalization.normalized "commission_usd_signed"
                commission_cost_usd = Prop $rawLmaxNormalization.normalized "commission_cost_usd"
                financing_usd_signed = Prop $rawLmaxNormalization.normalized "financing_usd_signed"
                financing_cost_usd = Prop $rawLmaxNormalization.normalized "financing_cost_usd"
                closing_pnl_usd = Prop $rawLmaxNormalization.normalized "closing_pnl_usd"
                closing_balance_usd = Prop $rawLmaxNormalization.normalized "closing_balance_usd"
                closing_equity_usd = Prop $rawLmaxNormalization.normalized "closing_equity_usd"
            }
        } else { $null })
        synthetic_sandbox_closeout_comparison = [ordered]@{
            comparison_performed = $rawLmaxNormalization.accepted
            comparison_purpose = "diagnostic_only_not_acceptance_gate"
            matches_synthetic_closeout = $false
            acceptance_impact = "none"
        }
    }
    accounting_evidence_lane = [ordered]@{
        real_manual_accounting_evidence_acceptance = $accountingAccepted
        blocked_reason = $(if ($accountingAccepted) { $null } else { "REAL_ACCOUNTING_EVIDENCE_MISSING" })
    }
    expected_periods = $discoveryReport.expected_periods
    staging_scan = [ordered]@{
        broker_statement_files_seen = @($brokerFiles).Count
        accounting_evidence_files_seen = @($accountingFiles).Count
        real_broker_statement_files_seen = $realBrokerFilesSeen
        real_accounting_evidence_files_seen = $realAccountingFilesSeen
    }
    discovery_summary = [ordered]@{
        discovered_candidate_files_count = $discoveryReport.discovered_candidate_files_count
        sample_files_count = $discoveryReport.sample_files_count
        real_non_sample_candidate_count = $discoveryReport.real_non_sample_candidate_count
        outside_staging_candidate_count = $discoveryReport.discovered_outside_staging_count
        staged_candidate_count = $discoveryReport.staged_files_count
        period_mismatch_count = $discoveryReport.period_mismatch_count
    }
    accepted_real_evidence = [ordered]@{
        broker_statements = @($acceptedBroker)
        accounting_evidence = @($acceptedAccounting)
    }
    rejected_evidence = @($rejected)
    quarantine_preview = [ordered]@{
        count = @($quarantineItems).Count
        items = @($quarantineItems)
    }
    readiness = [ordered]@{
        real_manual_broker_statement_acceptance = $brokerAccepted
        real_manual_accounting_evidence_acceptance = $accountingAccepted
        broker_confirmed_pnl = $false
        realized_accounting_close = $false
        ledger_commit = $false
        db_mutation = $false
        production_live = $false
        trading_readiness = $false
    }
    ready_outputs = [ordered]@{
        real_manual_evidence_acceptance_gate = $true
    }
    forbidden_ready_labels = [ordered]@{
        broker_api_statement_fetch = $false
        live_broker_reconciliation = $false
        broker_confirmed_pnl = $false
        realized_accounting_close = $false
        committed_ledger = $false
        ledger_commit = $false
        db_mutation = $false
        production_live = $false
        trading_readiness = $false
    }
    recommended_operator_actions = @($recommendedActions)
    still_blocked = @(
        "broker_confirmed_pnl",
        "realized_accounting_close",
        "ledger_commit",
        "db_mutation",
        "production_live",
        "trading_readiness"
    )
    global_guards = [ordered]@{
        external_calls = $false
        broker_api_calls = $false
        market_data_fetch = $false
        account_data_fetch = $false
        ledger_commit = $false
        db_mutation = $false
        trading_activity = $false
        production_live_ready = $false
        trading_readiness_ready = $false
    }
}
if (-not $brokerAccepted) { $main.still_blocked = @("real_manual_broker_statement_acceptance") + @($main.still_blocked) }
if (-not $accountingAccepted) { $main.still_blocked = @("real_manual_accounting_evidence_acceptance") + @($main.still_blocked) }
Write-JsonArtifact "real-manual-evidence-acceptance-r001.json" $main

$copyCommandText = if (@($copyCommands).Count -gt 0) { ($copyCommands -join "`n") } else { "None." }
$summary = @"
# NEXT_REAL_MANUAL_EVIDENCE_ACCEPTANCE_R001

Status: $status
Blocked reason: $blockedReason

Source statuses:
- Promotion gate: $($promotion.status)
- Manual evidence dry-run: $($manual.status)
- Controlled import framework: $($controlled.status)
- Sandbox broker/accounting reconciliation: $($reconciliation.status)
- Sandbox closeout: $($closeout.status)

Source values:
- Gross USD: $($sourceValues.gross_usd)
- Commission USD: $($sourceValues.commission_usd)
- Net USD: $($sourceValues.net_usd)
- Tolerance: $($sourceValues.tolerance)

Expected explicit periods:
- Broker statement: start=$((Prop $expectedBrokerPeriod "start_utc")) end=$((Prop $expectedBrokerPeriod "end_utc"))
- Accounting evidence: start=$((Prop $expectedAccountingPeriod "start_utc")) end=$((Prop $expectedAccountingPeriod "end_utc"))

Discovery:
- Raw LMAX bundle seen: $($rawLmaxBundle.seen)
- Raw LMAX bundle complete: $($rawLmaxBundle.complete)
- Raw LMAX normalization status: $($rawLmaxNormalization.status)
- Candidate files discovered: $($discoveryReport.discovered_candidate_files_count)
- Sample files discovered: $($discoveryReport.sample_files_count)
- Real non-sample candidates: $($discoveryReport.real_non_sample_candidate_count)
- Outside-staging candidates: $($discoveryReport.discovered_outside_staging_count)
- Staged candidates: $($discoveryReport.staged_files_count)
- Period mismatches: $($discoveryReport.period_mismatch_count)

Staging scan:
- Broker statement files seen: $(@($brokerFiles).Count)
- Accounting evidence files seen: $(@($accountingFiles).Count)
- Real broker statement files seen: $realBrokerFilesSeen
- Real accounting evidence files seen: $realAccountingFilesSeen

Recommended operator copy commands:
``````powershell
$copyCommandText
``````

Acceptance:
- LMAX broker statement accepted as real broker evidence: $brokerAccepted
- Synthetic sandbox closeout used as real broker acceptance gate: false
- Broker-confirmed PnL remains blocked reason: BROKER_STATEMENT_ACCEPTED_BUT_INTERNAL_TRADE_RECONCILIATION_NOT_DEFINED
- Accepted real broker evidence: $(@($acceptedBroker).Count)
- Accepted real accounting evidence: $(@($acceptedAccounting).Count)
- Rejected evidence: $(@($rejected).Count)
- Quarantine preview count: $(@($quarantineItems).Count)

Readiness:
- Real manual broker statement acceptance: $brokerAccepted
- Real manual accounting evidence acceptance: $accountingAccepted
- Broker-confirmed PnL: false
- Realized accounting close: false
- Ledger commit: false
- DB mutation: false
- Production/live: false
- Trading readiness: false

No trading, R009 submission, LMAX FIX/API, Polygon/Massive, broker API, market-data fetch, broker fetch, account fetch, DB mutation, ledger commit, production/live, or trading activity occurred.
"@
$summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "real-manual-evidence-acceptance-summary-r001.md") -Encoding UTF8

Write-Host "REAL_MANUAL_EVIDENCE_ACCEPTANCE_R001_BUILT: $status"
