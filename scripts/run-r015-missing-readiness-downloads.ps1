param(
    [switch]$DryRun,
    [int]$MaxRetries = 2
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = "C:\Users\phili\source\repos\QQ.Production.Intraday"
Set-Location $repoRoot

$downloader = Join-Path $repoRoot "scripts\download-polygon-fx-bbo-offline.ps1"

$planJson = Join-Path $repoRoot "artifacts\readiness\execution-sim\phase-exec-paper-r015-missing-offline-quote-download-plan.json"
$planMd   = Join-Path $repoRoot "artifacts\readiness\execution-sim\phase-exec-paper-r015-missing-offline-quote-download-plan.md"

$logDir = Join-Path $repoRoot "artifacts\readiness\execution-sim"
$logPath = Join-Path $logDir ("r015-missing-readiness-download-run-{0}.log" -f (Get-Date -Format "yyyyMMdd-HHmmss"))

$allowedSymbols = @(
    "C:EUR-USD",
    "C:USD-JPY",
    "C:AUD-USD",
    "C:GBP-USD",
    "C:NZD-USD",
    "C:USD-CAD",
    "C:USD-CHF"
)

function Write-Log {
    param([string]$Message)
    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    Write-Host $line
    Add-Content -Path $logPath -Value $line
}

function Get-CommandBlocksFromText {
    param([string]$Text)

    $matches = [regex]::Matches(
        $Text,
        "download-polygon-fx-bbo-offline\.ps1",
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    )

    $blocks = New-Object System.Collections.Generic.List[string]

    for ($i = 0; $i -lt $matches.Count; $i++) {
        $start = $matches[$i].Index
        if ($i + 1 -lt $matches.Count) {
            $end = $matches[$i + 1].Index
        } else {
            $end = $Text.Length
        }

        $blocks.Add($Text.Substring($start, $end - $start))
    }

    return $blocks
}

function Convert-CommandBlockToDownloadCommand {
    param(
        [string]$Block,
        [string[]]$GlobalSymbols
    )

    $fromMatch = [regex]::Match(
        $Block,
        "-FromUtc[^\d]*(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z)",
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    )

    $toMatch = [regex]::Match(
        $Block,
        "-ToUtc[^\d]*(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z)",
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    )

    if (-not $fromMatch.Success -or -not $toMatch.Success) {
        return $null
    }

    $fromUtc = $fromMatch.Groups[1].Value
    $toUtc   = $toMatch.Groups[1].Value

    $rawSymbols = @(
        [regex]::Matches($Block, "C:[A-Z]{3}-[A-Z]{3}") |
            ForEach-Object { $_.Value } |
            Select-Object -Unique
    )

    if ($rawSymbols.Count -eq 0 -and $Block -match "-Symbols\s+\$symbols") {
        $rawSymbols = $GlobalSymbols
    }

    if ($rawSymbols.Count -eq 0) {
        return $null
    }

    $symbols = @()
    foreach ($allowed in $allowedSymbols) {
        if ($rawSymbols -contains $allowed) {
            $symbols += $allowed
        }
    }

    if ($symbols.Count -eq 0) {
        return $null
    }

    $fromParsed = [DateTimeOffset]::Parse($fromUtc)
    $toParsed   = [DateTimeOffset]::Parse($toUtc)

    if ($toParsed -le $fromParsed) {
        throw "Invalid command window: FromUtc=$fromUtc ToUtc=$toUtc"
    }

    return [pscustomobject]@{
        FromUtc = $fromUtc
        ToUtc   = $toUtc
        Symbols = $symbols
        Key     = "$fromUtc|$toUtc|$($symbols -join ',')"
    }
}

function Get-DownloadCommands {
    $contents = New-Object System.Collections.Generic.List[string]

    if (Test-Path $planMd) {
        $contents.Add((Get-Content -Path $planMd -Raw))
    }

    if (Test-Path $planJson) {
        $contents.Add((Get-Content -Path $planJson -Raw))
    }

    if ($contents.Count -eq 0) {
        throw "Cannot find R015 download plan. Missing both: $planMd and $planJson"
    }

    $combined = $contents -join "`n"

    $globalSymbols = @(
        [regex]::Matches($combined, "C:[A-Z]{3}-[A-Z]{3}") |
            ForEach-Object { $_.Value } |
            Select-Object -Unique
    )

    $commandsByKey = @{}

    foreach ($content in $contents) {
        $blocks = Get-CommandBlocksFromText -Text $content

        foreach ($block in $blocks) {
            $cmd = Convert-CommandBlockToDownloadCommand -Block $block -GlobalSymbols $globalSymbols

            if ($null -ne $cmd) {
                $commandsByKey[$cmd.Key] = $cmd
            }
        }
    }

    $commands = @($commandsByKey.Values | Sort-Object FromUtc, ToUtc, @{ Expression = { $_.Symbols -join "," } })

    if ($commands.Count -eq 0) {
        throw @"
No download commands could be parsed from the R015 plan.

Open this file and check whether the command format changed:
$planMd

Paste the first command block back to ChatGPT if needed.
"@
    }

    return $commands
}

function Convert-ProviderSymbolToFilePrefix {
    param([string]$ProviderSymbol)

    switch ($ProviderSymbol) {
        "C:EUR-USD" { return "eurusd" }
        "C:USD-JPY" { return "usdjpy" }
        "C:AUD-USD" { return "audusd" }
        "C:GBP-USD" { return "gbpusd" }
        "C:NZD-USD" { return "nzdusd" }
        "C:USD-CAD" { return "usdcad" }
        "C:USD-CHF" { return "usdchf" }
        default { throw "Unsupported provider symbol: $ProviderSymbol" }
    }
}

function Convert-UtcToSuffixTime {
    param([string]$UtcText)

    return ([DateTimeOffset]::Parse($UtcText)).UtcDateTime.ToString("yyyyMMddHHmmss")
}

function Get-ExpectedOutputPaths {
    param($Commands)

    $incoming = Join-Path $repoRoot "data\offline-quotes\polygon\incoming"
    $pathsByKey = @{}

    foreach ($cmd in $Commands) {
        $fromSuffix = Convert-UtcToSuffixTime -UtcText $cmd.FromUtc
        $toSuffix   = Convert-UtcToSuffixTime -UtcText $cmd.ToUtc

        foreach ($symbol in $cmd.Symbols) {
            $prefix = Convert-ProviderSymbolToFilePrefix -ProviderSymbol $symbol
            $base = "$prefix-$fromSuffix-$toSuffix"

            $quote = Join-Path $incoming "$base.ndjson"
            $manifest = Join-Path $incoming "$base.manifest.json"

            $pathsByKey[$quote] = [pscustomobject]@{
                Type = "Quote"
                Path = $quote
            }

            $pathsByKey[$manifest] = [pscustomobject]@{
                Type = "Manifest"
                Path = $manifest
            }
        }
    }

    return @($pathsByKey.Values | Sort-Object Type, Path)
}

function Invoke-DownloadCommand {
    param(
        [object]$Command,
        [int]$CommandIndex,
        [int]$CommandCount
    )

    $symbolsText = $Command.Symbols -join ", "

    Write-Log ""
    Write-Log "============================================================"
    Write-Log "Command $CommandIndex / $CommandCount"
    Write-Log "FromUtc: $($Command.FromUtc)"
    Write-Log "ToUtc:   $($Command.ToUtc)"
    Write-Log "Symbols: $symbolsText"
    Write-Log "============================================================"

    if ($DryRun) {
        Write-Log "DRY RUN: not executing download."
        return
    }

    $attempt = 0

    while ($true) {
        $attempt++

        try {
            Write-Log "Executing attempt $attempt..."

            & $downloader `
                -FromUtc $Command.FromUtc `
                -ToUtc   $Command.ToUtc `
                -Symbols  ([string[]]$Command.Symbols) 2>&1 |
                Tee-Object -FilePath $logPath -Append

            Write-Log "Command completed."
            return
        }
        catch {
            Write-Log ("Command failed on attempt {0}: {1}" -f $attempt, $_.Exception.Message)

            if ($attempt -gt $MaxRetries) {
                throw
            }

            Write-Log "Retrying after transient failure..."
            Start-Sleep -Seconds 3
        }
    }
}

if (-not (Test-Path $downloader)) {
    throw "Downloader script not found: $downloader"
}

New-Item -ItemType Directory -Force -Path $logDir | Out-Null
"R015 missing readiness download run" | Set-Content -Path $logPath

$commands = Get-DownloadCommands

Write-Log "Parsed download commands: $($commands.Count)"
Write-Log "Expected by R015: 91 templates. Parsed count may differ if R015 grouped symbols/windows."

$expectedPaths = Get-ExpectedOutputPaths -Commands $commands
$expectedQuotes = @($expectedPaths | Where-Object { $_.Type -eq "Quote" })
$expectedManifests = @($expectedPaths | Where-Object { $_.Type -eq "Manifest" })

Write-Log "Expected unique quote files:    $($expectedQuotes.Count)"
Write-Log "Expected unique manifest files: $($expectedManifests.Count)"

if ($DryRun) {
    Write-Log "Dry-run mode enabled. Commands will be printed but not executed."
}

for ($i = 0; $i -lt $commands.Count; $i++) {
    Invoke-DownloadCommand -Command $commands[$i] -CommandIndex ($i + 1) -CommandCount $commands.Count
}

Write-Log ""
Write-Log "Checking expected output files..."

$missingQuotes = @()
$missingManifests = @()

foreach ($item in $expectedPaths) {
    if (-not (Test-Path $item.Path)) {
        if ($item.Type -eq "Quote") {
            $missingQuotes += $item.Path
        } else {
            $missingManifests += $item.Path
        }
    }
}

$existingQuotes = $expectedQuotes.Count - $missingQuotes.Count
$existingManifests = $expectedManifests.Count - $missingManifests.Count

Write-Log "Existing expected quote files:    $existingQuotes / $($expectedQuotes.Count)"
Write-Log "Existing expected manifest files: $existingManifests / $($expectedManifests.Count)"

if ($missingQuotes.Count -gt 0 -or $missingManifests.Count -gt 0) {
    Write-Log ""
    Write-Log "Missing quote files: $($missingQuotes.Count)"
    foreach ($path in $missingQuotes) {
        Write-Log "MISSING QUOTE: $path"
    }

    Write-Log ""
    Write-Log "Missing manifest files: $($missingManifests.Count)"
    foreach ($path in $missingManifests) {
        Write-Log "MISSING MANIFEST: $path"
    }

    if (-not $DryRun) {
        throw "R015 downloads completed with missing expected files. See log: $logPath"
    }
}

Write-Log ""
Write-Log "Done."
Write-Log "Log file: $logPath"
